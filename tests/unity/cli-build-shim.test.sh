#!/usr/bin/env bash
# ART-DIORAMA criterion 10: a licensed, locally hydrated clone can produce the APK;
# a clean public clone must fail before mutating build output or Editor state.
set -eu
cd "$(git rev-parse --show-toplevel)"

fail() { echo "cli-build-shim.test.sh: FAIL — $*" >&2; exit 1; }

shim="unity/Assets/Editor/CatMetroCliBuild.cs"
meta="$shim.meta"
[ -f "$shim" ] && [ -f "$meta" ] || fail "committed shim or meta is missing"
grep -q 'CM_APK_OUT' "$shim" || fail "output seam is missing"
grep -q 'CM_DEV_BUILD' "$shim" || fail "development-build seam is missing"
grep -q 'Path.GetFullPath' "$shim" || fail "output path is not normalized"
grep -q 'Path.GetExtension' "$shim" || fail "APK extension is not fail-closed"
grep -q 'RequirePolyforkLocalCustody' "$shim" \
  || fail "build does not require the licensed local asset pack"
grep -q 'Directory.CreateDirectory' "$shim" || fail "output parent is not created"
grep -q 'EditorUserBuildSettings.buildAppBundle = false' "$shim" \
  || fail "inherited editor state can still select AAB"
grep -q 'Assets/Scenes/Game.unity' "$shim" || fail "shipped scene is not pinned"
grep -q 'BuildOptions.Development' "$shim" || fail "dev APK cannot be requested"
grep -Eq '^guid: [0-9a-f]{32}$' "$meta" || fail "stable Unity meta GUID is missing"
if grep -Eq 'UNTRACKED|deleted after the session' "$shim"; then
  fail "committed shim still claims to be disposable"
fi

preflight_line=$(grep -n 'if (!RequirePolyforkLocalCustody())' "$shim" | cut -d: -f1)
mkdir_line=$(grep -n 'Directory.CreateDirectory' "$shim" | cut -d: -f1)
build_line=$(grep -n 'BuildPipeline.BuildPlayer' "$shim" | cut -d: -f1)
[ -n "$preflight_line" ] && [ "$preflight_line" -lt "$mkdir_line" ] \
  && [ "$preflight_line" -lt "$build_line" ] \
  || fail "licensed-pack preflight must run before output mutation and BuildPlayer"

echo "cli-build-shim.test.sh: OK"
