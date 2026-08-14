#!/usr/bin/env bash
# RICH-ASSETS criterion 5: key-custody + dry-run behavior of scripts/gen-assets.sh.
# Pins: (a) dry-run succeeds with NO keys in the environment; (b) live mode without a key
# fails and NAMES the missing env var; (c) a sentinel key value never appears in any
# output (redaction); (d) the script text never references the human's key file;
# (e) queue --dry-run walks the real manifest and plans both services.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
fail=0
script=scripts/gen-assets.sh
manifest=docs/design/assets/CAT-MANIFEST.json
t() { # $1 = label, $2 = 0|1 (expected ok), $3 = actual exit code
  if [ "$3" -eq 0 ] && [ "$2" = "0" ]; then echo "  ok: $1"; return; fi
  if [ "$3" -ne 0 ] && [ "$2" = "1" ]; then echo "  ok: $1"; return; fi
  echo "  FAIL: $1 (exit=$3 expected-ok=$2)"; fail=1
}

[ -f "$script" ] || { echo "FAIL: $script missing"; exit 1; }
[ -f "$manifest" ] || { echo "FAIL: $manifest missing"; exit 1; }

# (a) dry-run needs no keys — both subcommands and queue
out_m=$(env -u MESHY_API_KEY -u TRIPO_API_KEY bash "$script" meshy "a test cat" test-cat.glb --dry-run 2>&1); t "meshy dry-run keyless" 0 $?
out_t=$(env -u MESHY_API_KEY -u TRIPO_API_KEY bash "$script" tripo "a test cat" test-cat.glb --dry-run 2>&1); t "tripo dry-run keyless" 0 $?
out_q=$(env -u MESHY_API_KEY -u TRIPO_API_KEY bash "$script" queue "$manifest" --dry-run 2>&1); t "queue dry-run keyless" 0 $?

# dry-run must show the redacted auth header shape and the real hosts
echo "$out_m" | grep -q 'api.meshy.ai' || { echo "  FAIL: meshy dry-run lacks api.meshy.ai"; fail=1; }
echo "$out_t" | grep -q 'tripo3d.ai' || { echo "  FAIL: tripo dry-run lacks tripo3d.ai host"; fail=1; }
echo "$out_m" | grep -q 'Authorization: Bearer \[REDACTED\]' || { echo "  FAIL: meshy dry-run lacks redacted auth header"; fail=1; }
echo "$out_t" | grep -q 'Authorization: Bearer \[REDACTED\]' || { echo "  FAIL: tripo dry-run lacks redacted auth header"; fail=1; }
# queue must plan BOTH services from the real manifest
echo "$out_q" | grep -q 'api.meshy.ai' || { echo "  FAIL: queue dry-run plans no meshy request"; fail=1; }
echo "$out_q" | grep -q 'tripo3d.ai' || { echo "  FAIL: queue dry-run plans no tripo request"; fail=1; }

# (b) live mode without the key fails and names the var
msg=$(env -u MESHY_API_KEY -u TRIPO_API_KEY bash "$script" meshy "a test cat" test-cat.glb 2>&1); rc=$?
t "meshy live keyless fails" 1 $rc
echo "$msg" | grep -q 'MESHY_API_KEY' || { echo "  FAIL: missing-key message does not name MESHY_API_KEY"; fail=1; }
msg=$(env -u MESHY_API_KEY -u TRIPO_API_KEY bash "$script" tripo "a test cat" test-cat.glb 2>&1); rc=$?
t "tripo live keyless fails" 1 $rc
echo "$msg" | grep -q 'TRIPO_API_KEY' || { echo "  FAIL: missing-key message does not name TRIPO_API_KEY"; fail=1; }

# (c) sentinel key values never appear in any output, dry-run or usage/error paths
sm="sentinel-meshy-0f3a9d1c"; st="sentinel-tripo-7be24c88"
leak=$(MESHY_API_KEY="$sm" TRIPO_API_KEY="$st" bash "$script" queue "$manifest" --dry-run 2>&1)
if echo "$leak" | grep -Eq "$sm|$st"; then echo "  FAIL: sentinel key leaked in dry-run output"; fail=1; else echo "  ok: no key leak in dry-run"; fi
leak2=$(MESHY_API_KEY="$sm" TRIPO_API_KEY="$st" bash "$script" 2>&1 || true)
if echo "$leak2" | grep -Eq "$sm|$st"; then echo "  FAIL: sentinel key leaked in usage output"; fail=1; else echo "  ok: no key leak in usage"; fi

# (c2) THE LIVE PATH must not leak the key either (the dry-run legs above never reach
# need_key or api_curl — they return before it). This drives a real, non-dry-run
# generation with sentinel keys against a base that refuses instantly (https://127.0.0.1:1
# satisfies the https:// guard and connection-refuses with no network), so api_curl runs
# and its whole stdout+stderr is searched for the sentinel. A future debug line in
# api_curl, or any un-redacted error path, turns this RED. The lead cat is meshy, so
# TRIPO_API_BASE only affects the tripo probe.
leak3=$(MESHY_API_KEY="$sm" MESHY_API_BASE="https://127.0.0.1:1" GEN_ASSETS_OUT_DIR="$(mktemp -d)" \
  bash "$script" meshy "a live cat" test-cat.glb 2>&1 || true)
if echo "$leak3" | grep -Eq "$sm"; then echo "  FAIL: sentinel key leaked on the live meshy path"; fail=1; else echo "  ok: no key leak on live meshy path"; fi
leak4=$(TRIPO_API_KEY="$st" TRIPO_API_BASE="https://127.0.0.1:1" GEN_ASSETS_OUT_DIR="$(mktemp -d)" \
  bash "$script" tripo "a live cat" test-cat.glb 2>&1 || true)
if echo "$leak4" | grep -Eq "$st"; then echo "  FAIL: sentinel key leaked on the live tripo path"; fail=1; else echo "  ok: no key leak on live tripo path"; fi

# (c3) the redactor is not vacuous: extract redact() and prove it masks the key value —
# if a future edit no-ops the redactor body (a real M4-class mutation), this turns RED.
redfn=$(mktemp)
sed -n '/^redact()/,/^}/p' "$script" > "$redfn"
grep -q 'REDACTED' "$redfn" || { echo "  FAIL: could not extract a working redact() (vacuity guard broke)"; fail=1; }
probe=$(MESHY_API_KEY="$sm" bash -c ". '$redfn'; redact \"leak \$MESHY_API_KEY here\"" 2>&1)
rm -f "$redfn"
if echo "$probe" | grep -q "$sm"; then echo "  FAIL: redactor does not mask the key (vacuity guard)"; fail=1
elif echo "$probe" | grep -q 'REDACTED'; then echo "  ok: redactor masks the key"
else echo "  FAIL: redactor probe produced no redacted output (did not run)"; fail=1; fi

# (f) manifest-driven path escape is refused (F5): an 'out' with .. or a separator must
# not create files outside the gitignored candidate dir, even keyless.
esc=$(env -u MESHY_API_KEY bash "$script" meshy "x" "../ESCAPED.glb" 2>&1); rc=$?
t "path-escape out refused" 1 $rc
echo "$esc" | grep -q 'bare filename' || { echo "  FAIL: escape refusal does not explain the bare-filename rule"; fail=1; }
if [ -e ../ESCAPED.glb ] || [ -e ../ESCAPED ]; then
  echo "  FAIL: path escape created a file/dir outside the candidate area"; fail=1
  # scoped cleanup: only the two exact names this leg could have produced (never rm -rf a
  # broad sibling-of-repo path)
  rm -f ../ESCAPED.glb; rmdir ../ESCAPED 2>/dev/null || true
fi

# (g) the download + base-redirect controls (F1/F4/F6) are pinned so a future edit cannot
# silently revert them with a green suite. Behavioral where possible (source-token checks
# run on a COMMENT-STRIPPED view — the EMU-RIG lesson: a token in a comment must not satisfy
# a gate; 'glTF' in this function's own comment would otherwise mask a dropped magic check).
dlfn=$(mktemp)
sed -n '/^download_to()/,/^}/p' "$script" > "$dlfn"

# g1: a stubbed curl must NEVER be reached for an option-injection or non-https url.
stubdir=$(mktemp -d)
printf '#!/bin/sh\necho CURL_INVOKED\nexit 22\n' > "$stubdir/curl"; chmod +x "$stubdir/curl"
gout=$(PATH="$stubdir:$PATH" DOWNLOAD_TIMEOUT=5 bash -c '
  err(){ :; }
  . "'"$dlfn"'"
  download_to "--config=/etc/passwd" "'"$stubdir"'/a.glb"; echo "inj_rc=$?"
  download_to "http://x/a.glb" "'"$stubdir"'/b.glb"; echo "http_rc=$?"
  download_to "file:///etc/passwd" "'"$stubdir"'/c.glb"; echo "file_rc=$?"
' 2>&1)
rm -rf "$stubdir"
if echo "$gout" | grep -q CURL_INVOKED; then
  echo "  FAIL: download_to reached curl on a bad url (F1 scheme guard regressed)"; fail=1
elif echo "$gout" | grep -q 'inj_rc=1' && echo "$gout" | grep -q 'http_rc=1' && echo "$gout" | grep -q 'file_rc=1'; then
  echo "  ok: download_to refuses bad urls before curl"
else echo "  FAIL: download_to guard probe did not run as expected ($gout)"; fail=1; fi

# g2: a curl that "succeeds" writing a NON-glTF payload for a valid https url must not let
# the file land (catches a dropped magic check even though 'glTF' stays in the comment).
magdir=$(mktemp -d); land="$magdir/out.glb"
printf '#!/bin/sh\no=""; while [ $# -gt 0 ]; do [ "$1" = "-o" ] && o="$2"; shift; done\nprintf NOTGLTF > "$o"\nexit 0\n' > "$magdir/curl"; chmod +x "$magdir/curl"
PATH="$magdir:$PATH" DOWNLOAD_TIMEOUT=5 bash -c 'err(){ :; }; . "'"$dlfn"'"; download_to "https://api.meshy.ai/x.glb" "'"$land"'"' >/dev/null 2>&1
if [ -e "$land" ]; then echo "  FAIL: a non-glTF payload landed (F6 magic check regressed)"; fail=1
else echo "  ok: download_to rejects non-glTF payloads"; fi
rm -rf "$magdir"

# g3: remaining F6 flags pinned on a COMMENT-STRIPPED view of download_to (code, not prose).
dlcode=$(sed 's/#.*//' "$dlfn")
echo "$dlcode" | grep -q 'max-filesize' || { echo "  FAIL: download_to lost its --max-filesize cap (F6)"; fail=1; }
echo "$dlcode" | grep -q 'proto-redir'  || { echo "  FAIL: download_to lost its protocol pin (F6)"; fail=1; }
echo "$dlcode" | grep -q -- '-- "\$url"' || { echo "  FAIL: download_to no longer passes url after -- (F1)"; fail=1; }
rm -f "$dlfn"

# g4: each vendor base rejects an attacker host (F4 allowlist).
badbase=$(MESHY_API_BASE="https://attacker.example/v2" bash "$script" meshy "x" y.glb --dry-run 2>&1); [ $? -ne 0 ] || { echo "  FAIL: MESHY_API_BASE accepts a non-Meshy host (F4 allowlist regressed)"; fail=1; }
echo "$badbase" | grep -q 'Meshy host' || { echo "  FAIL: meshy base rejection does not name the host rule"; fail=1; }
badtripo=$(TRIPO_API_BASE="https://attacker.example/v3" bash "$script" tripo "x" y.glb --dry-run 2>&1); [ $? -ne 0 ] || { echo "  FAIL: TRIPO_API_BASE accepts a non-Tripo host (F4 allowlist regressed)"; fail=1; }
echo "$badtripo" | grep -q 'Tripo host' || { echo "  FAIL: tripo base rejection does not name the host rule"; fail=1; }

# (d) the script never references the human's key file by name
if grep -q '\.env' "$script"; then echo "  FAIL: script references the key file path"; fail=1; else echo "  ok: no key-file reference in script"; fi

# (e) manifest sanity: 8-12 cats, 4-6 props, both services present
python3 - "$manifest" <<'PY' || fail=1
import json, sys
d = json.load(open(sys.argv[1]))
assets = d["assets"]
cats = [a for a in assets if a["kind"] == "cat"]
props = [a for a in assets if a["kind"] == "prop"]
svcs = {a["service"] for a in assets}
ok = True
if not (8 <= len(cats) <= 12): print(f"  FAIL: {len(cats)} cats (want 8-12)"); ok = False
if not (4 <= len(props) <= 6): print(f"  FAIL: {len(props)} props (want 4-6)"); ok = False
if svcs != {"meshy", "tripo"}: print(f"  FAIL: services {svcs} (want both)"); ok = False
for a in assets:
    for k in ("id", "service", "kind", "prompt", "out"):
        if not a.get(k): print(f"  FAIL: asset missing {k}: {a.get('id','?')}"); ok = False
    if not a["out"].endswith(".glb"): print(f"  FAIL: out not .glb: {a['out']}"); ok = False
ids = [a["id"] for a in assets]
if len(ids) != len(set(ids)): print("  FAIL: duplicate asset ids"); ok = False
print("  ok: manifest shape" if ok else "  manifest shape FAILED")
sys.exit(0 if ok else 1)
PY

[ "$fail" -eq 0 ] && echo "gen-assets custody: PASS"
exit "$fail"
