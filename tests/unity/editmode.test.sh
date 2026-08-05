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
# review F12: comments stripped so a commented-out attribute can never satisfy the gate
if ! sed 's|<!--.*-->||g' unity/Assets/Plugins/Android/LauncherManifest.xml \
    | grep -q 'android:allowBackup="false"'; then
  fail "criterion 11: allowBackup=false missing from the launcher manifest (RK-17 decided posture)"
fi
rules=$(find unity/Assets \( -name "*backup*rules*.xml" -o -name "data_extraction_rules*.xml" \) 2>/dev/null || true)
[ -z "$rules" ] || fail "criterion 11: backup-rules XML exists under backup-OFF posture: $rules"
# review F4a: the committed launcher manifest must actually be WIRED into the build
grep -q 'useCustomLauncherManifest: 1' unity/ProjectSettings/ProjectSettings.asset \
  || fail "criterion 11: useCustomLauncherManifest is off — the posture never reaches the merged manifest"

# --- criterion 7 (review F4b: the check the handoff claimed but nobody wrote) ---
grep -q 'AndroidMinSdkVersion: 25' unity/ProjectSettings/ProjectSettings.asset \
  || fail "criterion 7: minSdk != 25"
grep -q 'AndroidTargetSdkVersion: 36' unity/ProjectSettings/ProjectSettings.asset \
  || fail "criterion 7: targetSdk != 36"

# --- review F4c: the device session needs a scene + build entry ---
[ -f unity/Assets/Scenes/Game.unity ] || fail "F4c: Assets/Scenes/Game.unity missing"
grep -q 'Assets/Scenes/Game.unity' unity/ProjectSettings/EditorBuildSettings.asset \
  || fail "F4c: Game.unity not in EditorBuildSettings"

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

# --- criterion 6 static: Presentation never simulates (review F12: alias forms included) ---
sim=$(grep -rEn --include='*.cs' 'Simulation\.Step|using static.*Simulation|=\s*CatMetro\.Domain\.Simulation' unity/Assets/Scripts/Presentation unity/Assets/Scripts/Bootstrap 2>/dev/null || true)
[ -z "$sim" ] || fail "criterion 6: a render-side tree reaches the sim step: $sim"
# criterion 2's static half (review F6): ONE file consumes the input package, and NO input
# surface beyond it exists anywhere render-side — drag/pinch/long-press/UGUI pointer handlers
# and the raw touch APIs are all banned tokens.
consumers=$(grep -rEl --include='*.cs' 'UnityEngine\.InputSystem' unity/Assets/Scripts/Presentation 2>/dev/null | wc -l | tr -d ' ')
[ "$consumers" = "1" ] || fail "criterion 2: expected exactly 1 input-consuming Presentation file, found $consumers"
gestures=$(grep -rEn --include='*.cs' 'EventSystems|IPointerDownHandler|IDragHandler|IBeginDragHandler|Touchscreen|EnhancedTouch|OnMouse' unity/Assets/Scripts/Presentation unity/Assets/Scripts/Bootstrap 2>/dev/null || true)
[ -z "$gestures" ] || fail "criterion 2: a second input surface exists: $gestures"

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
  if ! "$ED" -batchmode -projectPath "$(pwd)/unity" -runTests -testPlatform PlayMode \
      -testResults "$tmp/pm.xml" -logFile "$tmp/pm.log" > /dev/null 2>&1; then
    tail -5 "$tmp/pm.log" 2>/dev/null
    fail "editor half: headless PlayMode run exited non-zero"
  fi
  pm=$(python3 -c "
import xml.etree.ElementTree as ET
r = ET.parse('$tmp/pm.xml').getroot()
print(r.get('total'), r.get('passed'), r.get('failed'))")
  pt=$(echo "$pm" | cut -d' ' -f1); pp=$(echo "$pm" | cut -d' ' -f2); pf=$(echo "$pm" | cut -d' ' -f3)
  [ "$pf" = "0" ] && [ "$pt" = "$pp" ] && [ "$pt" != "0" ] \
    || fail "editor half: PlayMode $pp/$pt passed, $pf failed"
  echo "editmode.test.sh: OK (10, 11, 9-static, 6-static, 2-static; EditMode $passed/$total; PlayMode $pp/$pt)"
else
  echo "editmode.test.sh: OK (10, 11, 9-static, 6-static, 2-static; editor half DEFERRED — pinned editor absent, unity-editmode CI job is the remote enforcement, Q-V)"
fi
exit 0
