#!/usr/bin/env bash
# Exercise the AAB signature path of scripts/verify-android-artifact.py against real controls.
#
# Passing the APK checks and rejecting an obsolete AAB does NOT establish that the bundle
# verifier accepts a valid release: the obsolete AAB fails on content, so its signature verdict
# is never the deciding one. These controls are built by re-signing a real bundle, so the
# signature path is exercised in both directions.
#
#   valid self-signed  must PASS integrity, health and extraction   (the release shape)
#   unsigned           must FAIL integrity
#   entry added later  must FAIL integrity
#   tampered byte      must FAIL integrity
#   expired signer     must FAIL health, while integrity still passes
#   wrong certificate  must FAIL identity when a fingerprint is supplied
#
# Needs a carrier bundle to re-sign. Defaults to build/CatMetro-1.0.0-2.aab; that bundle's
# CONTENT checks fail either way and must keep failing, which this asserts too.
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CARRIER="${1:-$ROOT/build/CatMetro-1.0.0-2.aab}"
AP=/Applications/Unity/Hub/Editor/6000.3.16f1/PlaybackEngines/AndroidPlayer
KEYTOOL="$AP/OpenJDK/bin/keytool"
JARSIGNER="$AP/OpenJDK/bin/jarsigner"
VERIFY="$ROOT/scripts/verify-android-artifact.py"
WORK="${CM_SIGCONTROL_DIR:-$(mktemp -d "${TMPDIR:-/tmp}/catmetro-sigcontrols.XXXXXX")}"

for tool in "$KEYTOOL" "$JARSIGNER"; do
  [ -x "$tool" ] || { echo "SKIP: pinned Android JDK not installed: $tool"; exit 0; }
done
[ -f "$CARRIER" ] || { echo "SKIP: no carrier bundle at $CARRIER"; exit 0; }

fails=0
say() { printf '%-6s %s\n' "$1" "$2"; [ "$1" = FAIL ] && fails=$((fails + 1)); return 0; }

mkdir -p "$WORK"
if [ ! -f "$WORK/keyA.jks" ]; then
  for k in A B; do
    "$KEYTOOL" -genkeypair -keystore "$WORK/key$k.jks" -storetype PKCS12 \
      -storepass control -keypass control -alias "up$k" -keyalg RSA -keysize 2048 \
      -sigalg SHA256withRSA -validity 10000 \
      -dname "CN=Control $k, OU=cat metro test, O=cat metro test, L=Test, ST=CA, C=US" >/dev/null 2>&1
  done
  # -startdate in the past with a short validity yields a certificate that already expired.
  "$KEYTOOL" -genkeypair -keystore "$WORK/keyE.jks" -storetype PKCS12 \
    -storepass control -keypass control -alias upE -keyalg RSA -keysize 2048 \
    -sigalg SHA256withRSA -startdate -800d -validity 30 \
    -dname "CN=Control Expired, OU=cat metro test, O=cat metro test, L=Test, ST=CA, C=US" >/dev/null 2>&1
fi

python3 - "$CARRIER" "$WORK/unsigned.aab" <<'PY'
import sys, zipfile
src, dst = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(src) as a, zipfile.ZipFile(dst, "w", zipfile.ZIP_DEFLATED) as b:
    for info in a.infolist():
        upper = info.filename.upper()
        if upper.startswith("META-INF/") and (upper.endswith((".SF", ".RSA", ".DSA", ".EC"))
                                              or upper == "META-INF/MANIFEST.MF"):
            continue
        b.writestr(info, a.read(info.filename))
PY

for k in A B E; do
  cp "$WORK/unsigned.aab" "$WORK/signed$k.aab"
  "$JARSIGNER" -keystore "$WORK/key$k.jks" -storepass control -keypass control \
    -sigalg SHA256withRSA -digestalg SHA-256 "$WORK/signed$k.aab" "up$k" >/dev/null 2>&1 \
    || { echo "FAIL: could not sign control $k"; exit 1; }
done

python3 - "$WORK/signedA.aab" "$WORK/tampered.aab" "$WORK/extraentry.aab" <<'PY'
import sys, zipfile
src, tampered, extra = sys.argv[1], sys.argv[2], sys.argv[3]
target = "base/assets/content/levels/L001.json"
with zipfile.ZipFile(src) as a, zipfile.ZipFile(tampered, "w", zipfile.ZIP_DEFLATED) as b:
    for info in a.infolist():
        data = a.read(info.filename)
        if info.filename == target:
            data = data.replace(b'"id"', b'"iD"', 1)   # one byte, after signing
        b.writestr(info, data)
with zipfile.ZipFile(src) as a, zipfile.ZipFile(extra, "w", zipfile.ZIP_DEFLATED) as b:
    for info in a.infolist():
        b.writestr(info, a.read(info.filename))
    b.writestr("base/assets/smuggled.txt", b"added after signing\n")
PY

shaA="$("$KEYTOOL" -list -v -keystore "$WORK/keyA.jks" -storepass control -alias upA 2>/dev/null \
        | grep -m1 'SHA256:' | tr -d ' ' | cut -d: -f2-)"

# Read one named check out of the receipt the verifier writes beside the artifact.
verdict() { # <artifact> <check substring>
  python3 - "$1.verify.json" "$2" <<'PY'
import json, sys
rows = json.load(open(sys.argv[1]))["checks"]
hit = [r for r in rows if sys.argv[2] in r["check"]]
print("PASS" if hit and hit[0]["pass"] else "FAIL" if hit else "ABSENT")
PY
}

run_verify() { python3 "$VERIFY" "$@" >/dev/null 2>&1; }

echo "carrier  $CARRIER"
echo "work     $WORK"
echo

run_verify "$WORK/signedA.aab" --expect-cert-sha256 "$shaA"
[ "$(verdict "$WORK/signedA.aab" 'signature integrity')" = PASS ] \
  && say PASS "valid self-signed bundle: integrity accepted (the release shape)" \
  || say FAIL "valid self-signed bundle was REJECTED on integrity"
[ "$(verdict "$WORK/signedA.aab" 'certificate is healthy')" = PASS ] \
  && say PASS "valid self-signed bundle: certificate health accepted" \
  || say FAIL "valid self-signed bundle was rejected on certificate health"
[ "$(verdict "$WORK/signedA.aab" 'certificate extracted')" = PASS ] \
  && say PASS "valid self-signed bundle: certificate extracted" \
  || say FAIL "certificate extraction failed on a signed bundle"
[ "$(verdict "$WORK/signedA.aab" 'matches the Console upload certificate')" = PASS ] \
  && say PASS "matching fingerprint confirms identity" \
  || say FAIL "matching fingerprint did not confirm identity"

run_verify "$WORK/unsigned.aab"
[ "$(verdict "$WORK/unsigned.aab" 'signature integrity')" = FAIL ] \
  && say PASS "unsigned bundle rejected on integrity" \
  || say FAIL "unsigned bundle was accepted"

run_verify "$WORK/tampered.aab"
[ "$(verdict "$WORK/tampered.aab" 'signature integrity')" = FAIL ] \
  && say PASS "tampered bundle rejected on integrity" \
  || say FAIL "tampered bundle was accepted"

run_verify "$WORK/extraentry.aab"
[ "$(verdict "$WORK/extraentry.aab" 'signature integrity')" = FAIL ] \
  && say PASS "entry added after signing rejected on integrity" \
  || say FAIL "an entry added after signing was accepted"

run_verify "$WORK/signedE.aab"
[ "$(verdict "$WORK/signedE.aab" 'certificate is healthy')" = FAIL ] \
  && say PASS "expired signer certificate rejected on health" \
  || say FAIL "an EXPIRED signer certificate was accepted"
[ "$(verdict "$WORK/signedE.aab" 'signature integrity')" = PASS ] \
  && say PASS "expired signer still has intact signature integrity (concerns stay separate)" \
  || say FAIL "expiry was conflated with signature integrity"

run_verify "$WORK/signedB.aab" --expect-cert-sha256 "$shaA"
[ "$(verdict "$WORK/signedB.aab" 'matches the Console upload certificate')" = FAIL ] \
  && say PASS "wrong certificate rejected on identity" \
  || say FAIL "a bundle signed by the WRONG certificate passed identity"
[ "$(verdict "$WORK/signedB.aab" 'signature integrity')" = PASS ] \
  && say PASS "wrong certificate still has intact signature integrity (concerns stay separate)" \
  || say FAIL "identity was conflated with signature integrity"

# Missing fingerprint must not be reported as upload-ready. The controls are built from an
# obsolete carrier whose CONTENT is bad, so the run exits 1 on those grounds and never reaches
# the exit-3 path; assert the property that is actually observable here — the identity row fails
# as NOT SUPPLIED, and the result line never says PASS.
no_fp_out="$(python3 "$VERIFY" "$WORK/signedA.aab" 2>&1)"
if [ "$(verdict "$WORK/signedA.aab" 'matches the Console upload certificate')" = FAIL ] \
   && grep -q 'NOT SUPPLIED' <<<"$no_fp_out" \
   && ! grep -q '^RESULT    PASS' <<<"$no_fp_out"; then
  say PASS "no fingerprint supplied: identity unconfirmed and never reported as PASS"
else
  say FAIL "a bundle with no Console fingerprint was declared upload-ready"
fi

# The obsolete bundle must still fail its genuine CONTENT checks.
python3 "$VERIFY" "$CARRIER" >/dev/null 2>&1
carrier_rc=$?
[ "$carrier_rc" = 1 ] \
  && say PASS "the obsolete carrier bundle still fails (exit 1)" \
  || say FAIL "the obsolete carrier bundle no longer fails (exit $carrier_rc)"
for content in 'all 60 levels' 'byte-identical' 'ad or push SDK class' 'exactly the five allowed'; do
  [ "$(verdict "$CARRIER" "$content")" = FAIL ] \
    && say PASS "carrier still fails content check: $content" \
    || say FAIL "carrier no longer fails content check: $content"
done

echo
if [ "$fails" -ne 0 ]; then
  echo "aab-signature-controls.test.sh: $fails FAILED"
  exit 1
fi
echo "aab-signature-controls.test.sh: OK"
