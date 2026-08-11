#!/usr/bin/env bash
# ART-DIORAMA criterion 10: a licensed, locally hydrated clone can produce the APK;
# a clean public clone must fail before mutating build output or Editor state.
set -eu
cd "$(git rev-parse --show-toplevel)"

fail() { echo "cli-build-shim.test.sh: FAIL — $*" >&2; exit 1; }

shim="unity/Assets/Editor/CatMetroCliBuild.cs"
meta="$shim.meta"
build="scripts/build.sh"
editor_verifier="scripts/verify-unity-editor.sh"
apk_verifier="scripts/verify-android-apk.py"
unity_test_driver="scripts/run-unity-editmode.sh"
test_harness="scripts/test.sh"
guard="unity/Assets/Editor/PolyforkCustodyBuildPreprocessor.cs"
guard_meta="$guard.meta"
[ -f "$shim" ] && [ -f "$meta" ] || fail "committed shim or meta is missing"
[ -f "$guard" ] && [ -f "$guard_meta" ] || fail "Android build custody guard or meta is missing"
[ -f "$editor_verifier" ] || fail "pinned Unity editor verifier is missing"
[ -f "$apk_verifier" ] || fail "APK artifact verifier is missing"
[ -f "$unity_test_driver" ] || fail "canonical Unity test driver is missing"
grep -Fq 'editor="/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity"' \
  "$editor_verifier" || fail "editor verifier does not pin the Unity executable path"
grep -Fq "requirement='anchor apple generic and identifier \"com.unity3d.UnityEditor5.x\" and certificate leaf[subject.OU] = \"9QW8UQUTAA\"'" \
  "$editor_verifier" || fail "editor verifier does not pin the Apple/team/identifier requirement"
grep -Fq '/usr/bin/codesign --verify --deep --strict -R="$requirement" "$app"' \
  "$editor_verifier" || fail "editor verifier does not apply the pinned signature requirement"
grep -q '/usr/bin/plutil -extract CFBundleVersion raw' "$editor_verifier" \
  || fail "editor verifier does not read the signed bundle version"
grep -Fq '[ "$version" = "6000.3.16f1" ]' "$editor_verifier" \
  || fail "editor verifier does not enforce the pinned bundle version"
grep -q 'CM_APK_OUT' "$shim" || fail "output seam is missing"
grep -q 'CM_DEV_BUILD' "$shim" || fail "development-build seam is missing"
grep -q 'Path.GetFullPath' "$shim" || fail "output path is not normalized"
grep -q 'Path.GetExtension' "$shim" || fail "APK extension is not fail-closed"
grep -q 'PolyforkLocalCustody.RequireExact' "$shim" \
  || fail "build does not invoke the cryptographic local-custody verifier"
grep -q 'Directory.CreateDirectory' "$shim" || fail "output parent is not created"
grep -q 'EditorUserBuildSettings.buildAppBundle = false' "$shim" \
  || fail "inherited editor state can still select AAB"
grep -q 'Assets/Scenes/Game.unity' "$shim" || fail "shipped scene is not pinned"
grep -q 'BuildOptions.Development' "$shim" || fail "dev APK cannot be requested"
grep -Eq '^guid: [0-9a-f]{32}$' "$meta" || fail "stable Unity meta GUID is missing"
if grep -Eq 'UNTRACKED|deleted after the session' "$shim"; then
  fail "committed shim still claims to be disposable"
fi
grep -q 'IPreprocessBuildWithReport' "$guard" \
  || fail "Unity GUI/BuildPipeline Android paths have no custody preprocessor"
grep -q 'BuildTarget.Android' "$guard" || fail "custody preprocessor is not pinned to Android"
grep -q 'PolyforkLocalCustody.RequireExact' "$guard" \
  || fail "custody preprocessor does not invoke the cryptographic verifier"
grep -q 'CM_POLYFORK_BUILD_FLOW_TOKEN' "$guard" \
  || fail "GUI/direct Android builds do not require a one-use build-flow token"
grep -q 'build-flow-token-' "$shim" \
  || fail "CLI diagnostics do not distinguish build-flow token rejection from custody failure"
grep -q 'CM_POLYFORK_BUILD_FLOW_TOKEN' "$build" \
  || fail "canonical build does not issue a one-use build-flow token"
grep -Eq '^guid: [0-9a-f]{32}$' "$guard_meta" \
  || fail "custody preprocessor has no stable Unity meta GUID"

preflight_line=$(grep -n 'PolyforkLocalCustody.RequireExact' "$shim" | cut -d: -f1)
flow_token_line=$(grep -n 'RequireCanonicalBuildFlowTokenPresent' "$shim" | cut -d: -f1)
mkdir_line=$(grep -n 'Directory.CreateDirectory' "$shim" | cut -d: -f1)
build_line=$(grep -n 'BuildPipeline.BuildPlayer' "$shim" | cut -d: -f1)
[ -n "$flow_token_line" ] && [ "$flow_token_line" -lt "$mkdir_line" ] \
  || fail "build-flow token validation must precede output mutation"
[ -n "$preflight_line" ] && [ "$preflight_line" -lt "$mkdir_line" ] \
  && [ "$preflight_line" -lt "$build_line" ] \
  || fail "licensed-pack preflight must run before output mutation and BuildPlayer"

grep -q 'CM_REQUIRE_POLYFORK_LOCAL' "$build" \
  || fail "canonical build gate has no licensed-local profile"
grep -q 'CM_APK_OUT' "$build" || fail "canonical build gate does not require an APK output"
grep -q -- '-executeMethod CatMetroCliBuild.BuildAndroid' "$build" \
  || fail "canonical build gate does not invoke the Unity Android entrypoint"
custody_line=$(grep -n 'polyfork-custody.test.sh' "$build" | head -1 | cut -d: -f1)
credential_line=$(grep -n 'POLYFORK_KEY+x' "$build" | head -1 | cut -d: -f1)
guard_source_line=$(grep -n 'reject-git-redirect-env.sh' "$build" | head -1 | cut -d: -f1)
editor_verify_line=$(grep -n 'verify-unity-editor.sh' "$build" | head -1 | cut -d: -f1)
flow_token_issue_line=$(grep -n 'export CM_POLYFORK_BUILD_FLOW_TOKEN=' "$build" | head -1 | cut -d: -f1)
cache_line=$(grep -n 'mkdir -m 700 "$local_cache"' "$build" | head -1 | cut -d: -f1)
execute_line=$(grep -n -- '-executeMethod CatMetroCliBuild.BuildAndroid' "$build" | head -1 | cut -d: -f1)
artifact_verify_line=$(grep -n 'verify-android-apk.py' "$build" | head -1 | cut -d: -f1)
[ -n "$editor_verify_line" ] && [ "$editor_verify_line" -lt "$custody_line" ] \
  || fail "pinned Unity authentication must precede licensed custody"
[ -n "$custody_line" ] && [ -n "$execute_line" ] && [ "$custody_line" -lt "$execute_line" ] \
  || fail "cryptographic custody gate must precede the Unity Android entrypoint"
[ -n "$cache_line" ] && [ "$custody_line" -lt "$cache_line" ] \
  || fail "strict custody must fail before creating or changing Unity caches"
[ -n "$flow_token_issue_line" ] && [ "$custody_line" -lt "$flow_token_issue_line" ] \
  && [ "$flow_token_issue_line" -lt "$execute_line" ] \
  || fail "one-use Android flow token must be issued after custody and before Unity"
[ -n "$artifact_verify_line" ] && [ "$execute_line" -lt "$artifact_verify_line" ] \
  || fail "APK artifact validation must follow the Unity process"
[ -n "$credential_line" ] && [ -n "$guard_source_line" ] \
  && [ "$credential_line" -lt "$guard_source_line" ] \
  || fail "credential rejection must precede the first build subprocess"

credential_marker="catmetro-secret-must-not-echo"
if credential_output=$(POLYFORK_KEY="$credential_marker" bash "$build" 2>&1); then
  fail "canonical build accepted an inherited Polyfork credential"
fi
echo "$credential_output" | grep -q 'refusing inherited POLYFORK_KEY' \
  || fail "credential rejection did not identify the inherited-secret boundary"
if echo "$credential_output" | grep -q "$credential_marker"; then
  fail "credential rejection printed the credential value"
fi

arg_probe_out="${TMPDIR:-/tmp}/catmetro-build-arg-rejection.apk"
if arg_output=$(CM_REQUIRE_POLYFORK_LOCAL=1 CM_APK_OUT="$arg_probe_out" \
    bash "$build" --help 2>&1); then
  fail "licensed-local build accepted arguments that can redirect the staging gate"
fi
echo "$arg_output" | grep -q 'licensed-local profile accepts no arguments' \
  || fail "licensed-local argument rejection did not name the staging-integrity reason"

override_probe_out="${TMPDIR:-/tmp}/catmetro-build-editor-override.apk"
if override_output=$(CM_REQUIRE_POLYFORK_LOCAL=1 CM_APK_OUT="$override_probe_out" \
    CM_UNITY_EDITOR=/usr/bin/true bash "$build" 2>&1); then
  fail "licensed-local build accepted an arbitrary editor executable"
fi
echo "$override_output" | grep -q 'CM_UNITY_EDITOR overrides are forbidden' \
  || fail "build did not reject the editor override at the trust boundary"

if editmode_override_output=$(CM_REQUIRE_POLYFORK_LOCAL=1 \
    CM_UNITY_EDITOR=/usr/bin/true bash "$unity_test_driver" 2>&1); then
  fail "licensed-local test wrapper accepted an arbitrary editor executable"
fi
echo "$editmode_override_output" | grep -q 'CM_UNITY_EDITOR overrides are forbidden' \
  || fail "test wrapper did not reject the editor override at the trust boundary"
if driver_arg_output=$(bash "$unity_test_driver" --test-filter bypass 2>&1); then
  fail "Unity test driver accepted arguments outside the canonical profile"
fi
echo "$driver_arg_output" | grep -q 'accepts no arguments' \
  || fail "Unity test driver did not reject an argument-based bypass"
driver_credential_marker="catmetro-driver-secret-must-not-echo"
if driver_credential_output=$(POLYFORK_KEY="$driver_credential_marker" \
    bash "$unity_test_driver" 2>&1); then
  fail "Unity test driver accepted an inherited Polyfork credential"
fi
echo "$driver_credential_output" | grep -q 'refusing inherited POLYFORK_KEY' \
  || fail "Unity test driver did not reject the inherited credential"
if echo "$driver_credential_output" | grep -q "$driver_credential_marker"; then
  fail "Unity test driver printed the inherited credential value"
fi

test_credential_marker="catmetro-test-secret-must-not-echo"
if test_credential_output=$(POLYFORK_KEY="$test_credential_marker" \
    bash "$test_harness" 2>&1); then
  fail "canonical test harness accepted an inherited Polyfork credential"
fi
echo "$test_credential_output" | grep -q 'refusing inherited POLYFORK_KEY' \
  || fail "test harness did not reject the credential before test subprocesses"
if echo "$test_credential_output" | grep -q "$test_credential_marker"; then
  fail "test harness printed the inherited credential value"
fi
if test_override_output=$(CM_UNITY_EDITOR=/usr/bin/true bash "$test_harness" 2>&1); then
  fail "canonical test harness accepted an editor override"
fi
echo "$test_override_output" | grep -q 'CM_UNITY_EDITOR overrides are forbidden' \
  || fail "test harness did not reject the editor override before subprocesses"

if editor_override_output=$(bash "$editor_verifier" /usr/bin/true 2>&1); then
  fail "pinned editor verifier accepted a caller-selected executable"
fi
echo "$editor_override_output" | grep -q 'accepts no editor override' \
  || fail "editor verifier did not fail closed on a caller-selected executable"

pinned_editor="/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity"
if [ -x "$pinned_editor" ]; then
  verified_editor=$(bash "$editor_verifier") \
    || fail "installed pinned Unity editor failed signature/version authentication"
  [ "$verified_editor" = "$pinned_editor" ] \
    || fail "editor verifier returned an unpinned executable"
fi

artifact_probe=$(mktemp -d "${TMPDIR:-/tmp}/catmetro-build-artifact.XXXXXX")
artifact_path="$artifact_probe/missing.apk"
/usr/bin/true
if python3 "$apk_verifier" "$artifact_path" >/dev/null 2>&1; then
  rmdir "$artifact_probe"
  fail "APK verifier accepted the absent output of a zero-work executable"
fi
[ ! -e "$artifact_path" ] || fail "artifact probe unexpectedly created an APK"
printf '%s\n' 'not-a-zip' > "$artifact_probe/plain.apk"
if python3 "$apk_verifier" "$artifact_probe/plain.apk" >/dev/null 2>&1; then
  fail "APK verifier accepted a plain file"
fi
python3 - "$artifact_probe" <<'PY'
from pathlib import Path
import sys
import zipfile

root = Path(sys.argv[1])
with zipfile.ZipFile(root / "no-manifest.apk", "w") as package:
    package.writestr("classes.dex", b"dex\n")
with zipfile.ZipFile(root / "empty-manifest.apk", "w") as package:
    package.writestr("AndroidManifest.xml", b"")
with zipfile.ZipFile(root / "shaped.apk", "w") as package:
    package.writestr("AndroidManifest.xml", b"binary-manifest")
PY
for invalid_apk in "$artifact_probe/no-manifest.apk" "$artifact_probe/empty-manifest.apk"; do
  if python3 "$apk_verifier" "$invalid_apk" >/dev/null 2>&1; then
    fail "APK verifier accepted a ZIP without a non-empty manifest"
  fi
done
python3 "$apk_verifier" "$artifact_probe/shaped.apk" \
  || fail "APK verifier rejected an APK-shaped ZIP with a non-empty manifest"
for artifact in plain.apk no-manifest.apk empty-manifest.apk shaped.apk; do
  command unlink "$artifact_probe/$artifact"
done
rmdir "$artifact_probe"

echo "cli-build-shim.test.sh: OK"
