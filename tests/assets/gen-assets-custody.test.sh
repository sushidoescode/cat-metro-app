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
[ -e ../ESCAPED.glb ] || [ -e ../ESCAPED ] && { echo "  FAIL: path escape created a file/dir outside the candidate area"; fail=1; rm -rf ../ESCAPED.glb ../ESCAPED; }

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
