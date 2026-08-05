#!/usr/bin/env bash
# CM-C2b harness wrapper (criteria 9-static, 10, 11; ADR-0005:93). Two halves:
#   ALWAYS-ON — file-level gates that need no editor: StreamingAssets set-equality +
#     byte-identity (criterion 10, closes Q-Y), the backup-off posture (criterion 11, RK-17
#     decided 2026-08-04: allowBackup=false, no backup-rules XML may exist), and the criterion-9
#     static greps (engine path APIs only in Bootstrap; StreamingAssets reads via the web-request
#     route, no plain-file reads outside an editor-only branch).
#   EDITOR HALF — the headless EditMode suite through the PINNED editor when it is installed
#     locally; on runners without an editor it reports the deferral loudly (ADR-0009's topology:
#     the unity-editmode CI job — Q-V, human .github/** — is the remote enforcement of this half).
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
fail() { echo "editmode.test.sh: FAIL — $1"; exit 1; }
sa="unity/Assets/StreamingAssets"
boot="unity/Assets/Scripts/Bootstrap"

# --- criterion 10: set-equality BOTH directions, then per-file byte-identity ---
src_set=$(cd content/levels && ls *.json | sort)
dst_set=$(cd "$sa/content/levels" 2>/dev/null && ls *.json | sort)
[ -n "$dst_set" ] || fail "criterion 10: staged corpus missing (fail-closed)"
[ "$src_set" = "$dst_set" ] || fail "criterion 10: corpus set drift — src[$src_set] vs staged[$dst_set]"
for f in $src_set; do
  cmp -s "content/levels/$f" "$sa/content/levels/$f" || fail "criterion 10: byte drift in $f"
done
cmp -s config/runtime_bounds.json "$sa/config/runtime_bounds.json" \
  || fail "criterion 10: runtime_bounds byte drift (Q-Y gate)"

# --- criterion 11 (decided branch, backup OFF) ---
grep -q 'android:allowBackup="false"' unity/Assets/Plugins/Android/LauncherManifest.xml \
  || fail "criterion 11: allowBackup=false missing from the launcher manifest (RK-17 decided posture)"
rules=$(find unity/Assets -name "*backup*rules*.xml" 2>/dev/null || true)
[ -z "$rules" ] || fail "criterion 11: backup-rules XML exists under backup-OFF posture: $rules"

# --- criterion 9 statics ---
[ -d "$boot" ] || fail "criterion 9: Bootstrap root missing (fail-closed)"
eng=$(grep -rEn --include='*.cs' 'persistentDataPath|temporaryCachePath' unity/Assets/Scripts --exclude-dir=Bootstrap 2>/dev/null || true)
[ -z "$eng" ] || fail "criterion 9: engine path API outside Bootstrap: $eng"
# the content reader routes StreamingAssets through the web-request API — a plain-file read of
# the streaming path passes in the editor and fails on device (evaluator D4)
if grep -rEn --include='*.cs' 'streamingAssetsPath' "$boot" 2>/dev/null | grep -vE '//' > /dev/null; then
  grep -rEnq --include='*.cs' 'UnityWebRequest' "$boot" \
    || fail "criterion 9: streaming reads without the web-request route"
  sfile=$(grep -rEl --include='*.cs' 'streamingAssetsPath' "$boot")
  if sed 's|//.*||' $sfile | grep -qE 'File\.(ReadAll|Open|Exists)'; then
    fail "criterion 9: plain-file read of the streaming path in $sfile"
  fi
fi

# --- editor half ---
ED="/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity"
if [ -x "$ED" ]; then
  tmp="${TMPDIR:-/tmp}/cm-c2b-editmode-$$"
  mkdir -p "$tmp"
  trap 'rm -rf "$tmp"' EXIT
  if ! "$ED" -batchmode -projectPath "$(pwd)/unity" -runTests -testPlatform EditMode \
      -testResults "$tmp/results.xml" -logFile "$tmp/editor.log" > /dev/null 2>&1; then
    tail -5 "$tmp/editor.log" 2>/dev/null
    fail "editor half: headless EditMode run exited non-zero"
  fi
  summary=$(python3 -c "
import xml.etree.ElementTree as ET
r = ET.parse('$tmp/results.xml').getroot()
print(r.get('total'), r.get('passed'), r.get('failed'))")
  total=$(echo "$summary" | cut -d' ' -f1); passed=$(echo "$summary" | cut -d' ' -f2); failed=$(echo "$summary" | cut -d' ' -f3)
  [ "$failed" = "0" ] && [ "$total" = "$passed" ] && [ "$total" != "0" ] \
    || fail "editor half: EditMode $passed/$total passed, $failed failed"
  echo "editmode.test.sh: OK (10, 11, 9-static; editor half $passed/$total)"
else
  echo "editmode.test.sh: OK (10, 11, 9-static; editor half DEFERRED — pinned editor absent, unity-editmode CI job is the remote enforcement, Q-V)"
fi
exit 0
