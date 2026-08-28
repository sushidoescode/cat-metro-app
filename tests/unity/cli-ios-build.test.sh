#!/usr/bin/env bash
# iOS CLI build contract. The shell wrapper is executed against a fake Unity/Xcode
# toolchain so the test checks exit codes and produced artifacts without launching Unity.
set -eu
cd "$(git rev-parse --show-toplevel)"

fail() { echo "cli-ios-build.test.sh: FAIL — $*" >&2; exit 1; }

script="scripts/build-ios.sh"
builder="unity/Assets/Editor/CatMetroCliIosBuild.cs"
builder_meta="$builder.meta"
post="unity/Assets/Editor/CatMetroIosPostProcess.cs"
post_meta="$post.meta"
settings="unity/ProjectSettings/ProjectSettings.asset"

[ -f "$script" ] || fail "iOS shell wrapper is missing"
[ -f "$builder" ] && [ -f "$builder_meta" ] || fail "iOS builder or meta is missing"
[ -f "$post" ] && [ -f "$post_meta" ] || fail "iOS postprocessor or meta is missing"
grep -Eq '^guid: [0-9a-f]{32}$' "$builder_meta" || fail "builder meta GUID is missing"
grep -Eq '^guid: [0-9a-f]{32}$' "$post_meta" || fail "postprocessor meta GUID is missing"

# The override seams are what make the real wrapper executable under a fake toolchain.
# Check executable text, not prose, and stop before invoking it if they disappear: falling
# through to an installed Unity Editor would violate this test's purpose.
script_code="$(sed 's:#.*::' "$script")"
grep -Fq 'UNITY="${CM_UNITY_BIN:-' <<<"$script_code" \
  || fail "fakeable Unity binary seam is missing"
grep -Fq 'IOS_MODULE="${CM_IOS_MODULE_DIR:-' <<<"$script_code" \
  || fail "fakeable iOS module seam is missing"

# Static checks cover the Unity-only half that cannot execute headlessly here. Comments are
# stripped so prose cannot satisfy a positive gate.
grep -q '/\*' "$builder" && fail "block comments would evade line-comment stripping"
builder_code="$(sed 's://.*::' "$builder")"
has_builder() { grep -q "$1" <<<"$builder_code"; }
has_builder 'CM_IOS_OUT' || fail "builder output seam is missing"
has_builder 'PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS)' \
  || fail "builder does not read the iOS bundle identifier"
has_builder 'Assets/Scenes/Game.unity' || fail "shipped scene is not pinned"
has_builder 'target = BuildTarget.iOS' || fail "builder target is not iOS"
has_builder 'Directory.GetFileSystemEntries' \
  || fail "direct builder invocation does not refuse a non-empty output directory"
grep -Fq '!string.IsNullOrEmpty(Path.GetExtension(outPath))' <<<"$builder_code" \
  && fail "builder rejects valid dotted directory names instead of known file artifacts"
for extension in .ipa .app .xcarchive .xcodeproj; do
  grep -Fq '"'"$extension"'"' <<<"$builder_code" \
    || fail "builder does not reject $extension file-shaped output"
done
has_builder 'BuildPipeline.BuildPlayer' || fail "builder never emits an Xcode project"
grep -Fq 'EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);' \
  <<<"$builder_code" || fail "builder does not map the Unity build result to its process exit"
grep -Eq 'PlayerSettings\.[A-Za-z0-9_.]+[[:space:]]*=[^=]' "$builder" \
  && fail "builder must never write signing or other Player Settings"

grep -q '/\*' "$post" && fail "block comments would evade line-comment stripping"
post_code="$(sed 's://.*::' "$post")"
grep -q '^#if UNITY_IOS' <<<"$post_code" || fail "postprocessor is not iOS-only"
grep -q 'ITSAppUsesNonExemptEncryption' <<<"$post_code" \
  || fail "export-compliance plist declaration is missing"
grep -Fq 'Answer: no' <<<"$post_code" \
  && fail "postprocessor prescribes an export answer when the final binary is unproven"

grep -Eq '^    iPhone: com\.catmetro\.game$' "$settings" \
  || fail "committed iOS bundle identifier is missing"
grep -Eq '^  iOSTargetOSVersionString: 15\.0$' "$settings" \
  || fail "minimum iOS 15 setting is missing"

tmp_root="$(mktemp -d)"
trap 'rm -rf "$tmp_root"' EXIT
fixture="$tmp_root/repo"
fake_bin="$tmp_root/bin"
fake_module="$tmp_root/iOSSupport"
mkdir -p "$fixture/scripts" "$fixture/unity/ProjectSettings" "$fake_bin" "$fake_module"
cp "$script" "$fixture/scripts/build-ios.sh"
printf 'm_EditorVersion: 6000.3.16f1\n' > "$fixture/unity/ProjectSettings/ProjectVersion.txt"

write_settings() {
  bundle_id="$1"
  {
    printf 'PlayerSettings:\n'
    printf '  applicationIdentifier:\n'
    printf '    Android: com.catmetro.game\n'
    if [ -n "$bundle_id" ]; then
      printf '    iPhone: %s\n' "$bundle_id"
    fi
  } > "$fixture/unity/ProjectSettings/ProjectSettings.asset"
}

write_settings_with_delayed_iphone() {
  {
    printf 'PlayerSettings:\n'
    printf '  applicationIdentifier:\n'
    printf '    Android: com.catmetro.game\n'
    printf '    Standalone: com.catmetro.desktop\n'
    printf '    VisionOS: com.catmetro.vision\n'
    printf '    tvOS: com.catmetro.tv\n'
    printf '    Switch: com.catmetro.switch\n'
    printf '    iPhone: com.catmetro.game\n'
    printf '  buildNumber:\n'
    printf '    iPhone: 1\n'
  } > "$fixture/unity/ProjectSettings/ProjectSettings.asset"
}

cat > "$fake_bin/xcodebuild" <<'FAKE_XCODEBUILD'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$CM_FAKE_XCODEBUILD_CALLS"
printf 'Xcode %s\n' "${CM_FAKE_XCODE_VERSION:-26.5}"
printf 'Build version TEST\n'
FAKE_XCODEBUILD

cat > "$fake_bin/xcrun" <<'FAKE_XCRUN'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$CM_FAKE_XCRUN_CALLS"
case "$*" in
  *--show-sdk-version*) printf '%s\n' "${CM_FAKE_IOS_SDK_VERSION:-26.5}" ;;
  *) exit 2 ;;
esac
FAKE_XCRUN

cat > "$fake_bin/Unity" <<'FAKE_UNITY'
#!/usr/bin/env bash
{
  printf 'CM_IOS_OUT=%s\n' "${CM_IOS_OUT:-}"
  printf 'CM_DEV_BUILD=%s\n' "${CM_DEV_BUILD:-0}"
  printf '%s\n' "$@"
} > "$CM_FAKE_UNITY_CALL"
case "${CM_FAKE_UNITY_MODE:-success}" in
  fail) exit 7 ;;
  fail-with-artifact)
    mkdir -p "$CM_IOS_OUT/Unity-iPhone.xcodeproj"
    printf '// fake generated project before failure\n' > "$CM_IOS_OUT/Unity-iPhone.xcodeproj/project.pbxproj"
    exit 7
    ;;
  success)
    if [ -n "${CM_FAKE_UNITY_CWD:-}" ]; then
      cd "$CM_FAKE_UNITY_CWD"
    fi
    mkdir -p "$CM_IOS_OUT/Unity-iPhone.xcodeproj"
    printf '// fake generated project\n' > "$CM_IOS_OUT/Unity-iPhone.xcodeproj/project.pbxproj"
    exit 0
    ;;
  no-artifact) exit 0 ;;
  *) exit 9 ;;
esac
FAKE_UNITY
chmod +x "$fake_bin/xcodebuild" "$fake_bin/xcrun" "$fake_bin/Unity"

run_build() {
  case_name="$1"
  out_path="$2"
  shift 2
  invoke_cwd="$PWD"
  if [ "${1:-}" = "--cwd" ]; then
    invoke_cwd="$2"
    shift 2
  fi
  case_dir="$tmp_root/$case_name"
  mkdir -p "$case_dir"
  set +e
  (
    cd "$invoke_cwd"
    env \
      PATH="$fake_bin:$PATH" \
      CM_UNITY_BIN="$fake_bin/Unity" \
      CM_IOS_MODULE_DIR="$fake_module" \
      CM_FAKE_UNITY_CALL="$case_dir/unity-call" \
      CM_FAKE_XCODEBUILD_CALLS="$case_dir/xcodebuild-calls" \
      CM_FAKE_XCRUN_CALLS="$case_dir/xcrun-calls" \
      "$@" \
      bash "$fixture/scripts/build-ios.sh" "$out_path" \
        > "$case_dir/output" 2>&1
  )
  build_rc=$?
  set -e
}

# Missing identity must fail before the expensive Unity process starts.
write_settings ""
run_build no-bundle "$tmp_root/no-bundle-out" CM_FAKE_UNITY_MODE=success
[ "$build_rc" -ne 0 ] || fail "missing bundle identifier exited zero"
[ ! -e "$tmp_root/no-bundle/unity-call" ] || fail "Unity ran with no bundle identifier"

# The identifier scan must cover the YAML mapping, not a fixed number of following lines.
write_settings_with_delayed_iphone
run_build delayed-iphone "$tmp_root/delayed-iphone-out" CM_FAKE_UNITY_MODE=success
[ "$build_rc" -eq 0 ] || fail "valid delayed iPhone bundle identifier was not found"
[ -e "$tmp_root/delayed-iphone/unity-call" ] || fail "Unity did not run with a valid delayed ID"
grep -Fxq 'Bundle ID : com.catmetro.game' "$tmp_root/delayed-iphone/output" \
  || fail "bundle preflight selected a platform ID other than iPhone"

# Apple requires both a qualifying Xcode and a qualifying device SDK.
write_settings "com.catmetro.game"
run_build unknown-xcode "$tmp_root/unknown-xcode-out" \
  CM_FAKE_XCODE_VERSION=unknown CM_FAKE_IOS_SDK_VERSION=26.5 CM_FAKE_UNITY_MODE=success
[ "$build_rc" -ne 0 ] || fail "unparseable Xcode version exited zero"
[ ! -e "$tmp_root/unknown-xcode/unity-call" ] || fail "Unity ran with an unknown Xcode version"

run_build old-xcode "$tmp_root/old-xcode-out" \
  CM_FAKE_XCODE_VERSION=25.4 CM_FAKE_IOS_SDK_VERSION=26.5 CM_FAKE_UNITY_MODE=success
[ "$build_rc" -ne 0 ] || fail "Xcode below Apple's floor exited zero"
[ ! -e "$tmp_root/old-xcode/unity-call" ] || fail "Unity ran with an obsolete Xcode"

run_build unknown-sdk "$tmp_root/unknown-sdk-out" \
  CM_FAKE_XCODE_VERSION=26.5 CM_FAKE_IOS_SDK_VERSION=unknown CM_FAKE_UNITY_MODE=success
[ "$build_rc" -ne 0 ] || fail "unparseable iOS SDK version exited zero"
[ ! -e "$tmp_root/unknown-sdk/unity-call" ] || fail "Unity ran with an unknown iOS SDK"

run_build old-sdk "$tmp_root/old-sdk-out" \
  CM_FAKE_XCODE_VERSION=26.5 CM_FAKE_IOS_SDK_VERSION=25.4 CM_FAKE_UNITY_MODE=success
[ "$build_rc" -ne 0 ] || fail "iOS SDK below Apple's floor exited zero"
[ ! -e "$tmp_root/old-sdk/unity-call" ] || fail "Unity ran with an obsolete iOS SDK"

run_build file-output "$tmp_root/CatMetro.ipa" CM_FAKE_UNITY_MODE=success
[ "$build_rc" -ne 0 ] || fail "file-shaped .ipa output path exited zero"
[ ! -e "$tmp_root/file-output/unity-call" ] || fail "Unity ran for a file-shaped output path"

# A previous project must never turn a failed/no-op Unity run into a false success.
stale_out="$tmp_root/stale-out"
mkdir -p "$stale_out/Unity-iPhone.xcodeproj"
printf '// stale project\n' > "$stale_out/Unity-iPhone.xcodeproj/project.pbxproj"
run_build stale "$stale_out" CM_FAKE_UNITY_MODE=fail
[ "$build_rc" -ne 0 ] || fail "failed Unity run passed over a stale Xcode project"
[ ! -e "$tmp_root/stale/unity-call" ] || fail "Unity ran for a stale output directory"

run_build stale-no-artifact "$stale_out" CM_FAKE_UNITY_MODE=no-artifact
[ "$build_rc" -ne 0 ] || fail "zero-exit no-op passed over a stale Xcode project"
[ ! -e "$tmp_root/stale-no-artifact/unity-call" ] \
  || fail "Unity ran for a stale output directory in the no-artifact case"

# Post-Unity checks use fresh outputs so these cases really reach the Unity process.
failed_out="$tmp_root/failed-out"
run_build unity-failed "$failed_out" CM_FAKE_UNITY_MODE=fail
[ "$build_rc" -eq 7 ] || fail "Unity exit 7 was not propagated exactly (got $build_rc)"
[ -e "$tmp_root/unity-failed/unity-call" ] || fail "failure case never invoked Unity"

failed_artifact_out="$tmp_root/failed-artifact-out"
run_build unity-failed-with-artifact "$failed_artifact_out" CM_FAKE_UNITY_MODE=fail-with-artifact
[ "$build_rc" -eq 7 ] || fail "Unity failure with an artifact exited $build_rc instead of 7"
[ -f "$failed_artifact_out/Unity-iPhone.xcodeproj/project.pbxproj" ] \
  || fail "failure-with-artifact fixture did not exercise the intended state"

missing_artifact_out="$tmp_root/missing-artifact-out"
run_build unity-no-artifact "$missing_artifact_out" CM_FAKE_UNITY_MODE=no-artifact
[ "$build_rc" -ne 0 ] || fail "zero-exit Unity run with no artifact passed"
[ -e "$tmp_root/unity-no-artifact/unity-call" ] || fail "no-artifact case never invoked Unity"

# A clean fake build proves the command line and the artifact check end to end.
success_out="$tmp_root/success-out"
run_build success "$success_out" CM_FAKE_UNITY_MODE=success
[ "$build_rc" -eq 0 ] || fail "successful fake Unity run exited $build_rc"
[ -f "$success_out/Unity-iPhone.xcodeproj/project.pbxproj" ] \
  || fail "successful run did not preserve the generated Xcode project"
grep -Fxq 'CM_IOS_OUT='"$success_out" "$tmp_root/success/unity-call" \
  || fail "output directory was not passed to Unity"
grep -Fxq -- '-buildTarget' "$tmp_root/success/unity-call" \
  || fail "Unity invocation does not select a build target"
grep -Fxq 'iOS' "$tmp_root/success/unity-call" \
  || fail "Unity invocation does not select iOS"
grep -Fxq 'CatMetroCliIosBuild.BuildIos' "$tmp_root/success/unity-call" \
  || fail "Unity invocation does not call the iOS builder"
grep -Fxq -- '-quit' "$tmp_root/success/unity-call" \
  && fail "Unity invocation must not use -quit"

# Unity is allowed to change its working directory. A relative caller path must therefore
# be normalized before it crosses the process boundary.
caller_cwd="$tmp_root/caller-cwd"
mkdir -p "$caller_cwd"
run_build relative-output relative-out --cwd "$caller_cwd" \
  CM_FAKE_UNITY_CWD="$fixture/unity" CM_FAKE_UNITY_MODE=success
[ "$build_rc" -eq 0 ] || fail "relative output from another working directory exited $build_rc"
relative_out="$caller_cwd/relative-out"
[ -f "$relative_out/Unity-iPhone.xcodeproj/project.pbxproj" ] \
  || fail "relative output was not generated under the caller's working directory"
grep -Fxq 'CM_IOS_OUT='"$relative_out" "$tmp_root/relative-output/unity-call" \
  || fail "relative output was not made absolute before invoking Unity"

# Development intent is inherited by Unity, while xcodebuild is used for preflight only.
dev_out="$tmp_root/dev-out"
run_build dev "$dev_out" CM_DEV_BUILD=1 CM_FAKE_UNITY_MODE=success
[ "$build_rc" -eq 0 ] || fail "development fake build exited $build_rc"
grep -Fxq 'CM_DEV_BUILD=1' "$tmp_root/dev/unity-call" \
  || fail "development flag was not passed through to Unity"
grep -Fxq -- '-version' "$tmp_root/dev/xcodebuild-calls" \
  || fail "xcodebuild version preflight did not run"
[ "$(wc -l < "$tmp_root/dev/xcodebuild-calls" | tr -d ' ')" -eq 1 ] \
  || fail "wrapper did more than the xcodebuild version preflight"

echo "cli-ios-build.test.sh: OK"
