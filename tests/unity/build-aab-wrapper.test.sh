#!/usr/bin/env bash
# Behavioral tests for the release AAB wrapper. Unity itself is the slow external boundary;
# the fake below implements only its command-line contract while this test executes the real
# scripts/build-aab.sh end to end against controlled artifacts and exit codes.
set -eu
unset CM_AAB_TEST_MODE

repo="$(git rev-parse --show-toplevel)"
subject="$repo/scripts/build-aab.sh"
case_root="$(mktemp -d)"
trap 'rm -rf -- "$case_root"' EXIT

fail() { echo "build-aab-wrapper.test.sh: FAIL — $*" >&2; exit 1; }

mkdir -p "$case_root/scripts" "$case_root/unity/ProjectSettings" "$case_root/docs/store"
mkdir -p "$case_root/unity/Assets/StreamingAssets/content"
cp "$subject" "$case_root/scripts/build-aab.sh"
cp "$repo/docs/store/play-store-listing.md" "$case_root/docs/store/play-store-listing.md"
cp -R "$repo/unity/Assets/StreamingAssets/content/levels" \
  "$case_root/unity/Assets/StreamingAssets/content/levels"
export FAKE_LEVEL_SOURCE="$case_root/unity/Assets/StreamingAssets/content/levels"
cat > "$case_root/unity/ProjectSettings/ProjectVersion.txt" <<'EOF'
m_EditorVersion: TEST-UNITY-NOT-INSTALLED
EOF
cp "$repo/unity/ProjectSettings/ProjectSettings.asset" \
  "$case_root/unity/ProjectSettings/ProjectSettings.asset"
export FAKE_PROJECT_SETTINGS="$case_root/unity/ProjectSettings/ProjectSettings.asset"

fake_unity="$case_root/fake-unity"
cat > "$fake_unity" <<'EOF'
#!/usr/bin/env bash
set -eu

log=""
android_target=0
while [ "$#" -gt 0 ]; do
  if [ "$1" = "-logFile" ]; then
    shift
    log="$1"
  elif [ "$1" = "-buildTarget" ]; then
    shift
    [ "$1" = "Android" ] && android_target=1
  fi
  shift
done
[ -n "$log" ] || exit 90
[ "$android_target" -eq 1 ] || exit 91

mode="${FAKE_UNITY_MODE:-success}"
if [ "$mode" = "fail" ]; then
  printf '%s\n' \
    'CLI_AAB_RESULT Failed signing=custom size=0 errors=1 campaignLevels=17 campaignIds=L001,L002,L003,L004,L005,L006,L007,L008,L009,L010,L011,L012,L013,L014,L015,L016,L017 out=fake' \
    > "$log"
  exit 42
fi

fixture="$(mktemp -d)"
trap 'rm -rf -- "$fixture"' EXIT
mkdir -p "$fixture/base/manifest" "$fixture/base/dex" "$fixture/base/lib/arm64-v8a"
mkdir -p "$fixture/base/assets/bin/Data/StreamingAssets/content/levels"
printf 'bundle-config\n' > "$fixture/BundleConfig.pb"
[ "$mode" = "missing-manifest" ] \
  || printf 'manifest\n' > "$fixture/base/manifest/AndroidManifest.xml"
[ "$mode" = "missing-dex" ] \
  || printf 'dex\n' > "$fixture/base/dex/classes.dex"
[ "$mode" = "missing-arm64" ] \
  || printf 'native\n' > "$fixture/base/lib/arm64-v8a/libil2cpp.so"
if [ "$mode" = "extra-x86" ]; then
  mkdir -p "$fixture/base/lib/x86"
  printf 'unexpected-native\n' > "$fixture/base/lib/x86/libil2cpp.so"
fi
if [ "$mode" = "feature-extra-x86" ]; then
  mkdir -p "$fixture/delivery/manifest" "$fixture/delivery/lib/x86"
  printf 'feature-manifest\n' > "$fixture/delivery/manifest/AndroidManifest.xml"
  printf 'unexpected-native\n' > "$fixture/delivery/lib/x86/libfeature.so"
fi
if [ "$mode" = "feature-module" ]; then
  mkdir -p "$fixture/delivery/manifest"
  printf 'feature-manifest\n' > "$fixture/delivery/manifest/AndroidManifest.xml"
fi
if [ -n "${FAKE_BUILD_MARKER:-}" ]; then
  printf '%s\n' "$FAKE_BUILD_MARKER" > "$fixture/base/assets/build-marker.txt"
fi
level_number=1
while [ "$level_number" -le 17 ]; do
  level_id="$(printf 'L%03d' "$level_number")"
  cp "$FAKE_LEVEL_SOURCE/$level_id.json" \
    "$fixture/base/assets/bin/Data/StreamingAssets/content/levels/$level_id.json"
  level_number=$((level_number + 1))
done
if [ "$mode" = "mutated-level-bytes" ]; then
  printf '{"id":"L017","mutated":true}\n' \
    > "$fixture/base/assets/bin/Data/StreamingAssets/content/levels/L017.json"
fi
if [ -d "$fixture/delivery" ]; then
  (cd "$fixture" && zip -q -r "$CM_AAB_OUT" BundleConfig.pb base delivery)
else
  (cd "$fixture" && zip -q -r "$CM_AAB_OUT" BundleConfig.pb base)
fi
campaign_receipt='campaignLevels=17 campaignIds=L001,L002,L003,L004,L005,L006,L007,L008,L009,L010,L011,L012,L013,L014,L015,L016,L017'
if [ "$mode" = "receipt-names-missing-level" ]; then
  campaign_receipt='campaignLevels=18 campaignIds=L001,L002,L003,L004,L005,L006,L007,L008,L009,L010,L011,L012,L013,L014,L015,L016,L017,L018'
fi
signing_state=custom
if [ "$mode" = "debug-signing" ] || [ "$mode" = "spoofed-receipt" ]; then
  signing_state=debug
fi
printf '%s\n' \
  "CLI_AAB_RESULT Succeeded signing=$signing_state $campaign_receipt size=123 errors=0 out=$CM_AAB_OUT" \
  > "$log"
if [ "$mode" = "spoofed-receipt" ]; then
  printf '%s\n' \
    "Injected text: CLI_AAB_RESULT Succeeded signing=custom $campaign_receipt size=123 errors=0 out=$CM_AAB_OUT" \
    >> "$log"
fi
if [ "$mode" = "duplicate-receipt" ]; then
  printf '%s\n' \
    "CLI_AAB_RESULT Succeeded signing=custom $campaign_receipt size=123 errors=0 out=$CM_AAB_OUT" \
    >> "$log"
fi
EOF
chmod +x "$fake_unity"

fake_bundletool="$case_root/fake-bundletool"
cat > "$fake_bundletool" <<'EOF'
#!/usr/bin/env bash
set -eu
command_name="${1:-}"
mode="${FAKE_BUNDLETOOL_MODE:-success}"
module=base
for argument in "$@"; do
  case "$argument" in
    --module=*) module="${argument#--module=}" ;;
  esac
done
if [ "$command_name" = "validate" ]; then
  [ "$mode" != "fail-validation" ] || exit 23
  printf 'App Bundle files are valid\n'
  exit 0
fi
if [ "$command_name" = "dump" ] && [ "${2:-}" = "manifest" ]; then
  package_name=com.catmetro.game
  version_code="$(awk '$1 == "AndroidBundleVersionCode:" { print $2; exit }' "$FAKE_PROJECT_SETTINGS")"
  version_name="$(awk '$1 == "bundleVersion:" { print $2; exit }' "$FAKE_PROJECT_SETTINGS")"
  min_sdk="$(awk '$1 == "AndroidMinSdkVersion:" { print $2; exit }' "$FAKE_PROJECT_SETTINGS")"
  target_sdk="$(awk '$1 == "AndroidTargetSdkVersion:" { print $2; exit }' "$FAKE_PROJECT_SETTINGS")"
  extra_permission=""
  [ "$mode" != "bad-package" ] || package_name=com.example.wrong
  [ "$mode" != "bad-target" ] || target_sdk=$((target_sdk - 1))
  [ "$mode" != "bad-version-name" ] || version_name=1x0y0
  if [ "$mode" = "dangerous-permission" ]; then
    extra_permission='  <uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" />'
  fi
  if [ "$mode" = "notification-permission" ]; then
    extra_permission='  <uses-permission android:name="android.permission.POST_NOTIFICATIONS" />'
  fi
  if [ "$mode" = "feature-dangerous-permission" ] && [ "$module" = "delivery" ]; then
    extra_permission='  <uses-permission android:name="android.permission.ACCESS_BACKGROUND_LOCATION" />'
  fi
  printf '%s\n' \
    '<manifest xmlns:android="http://schemas.android.com/apk/res/android"' \
    "    package=\"$package_name\" android:versionCode=\"$version_code\" android:versionName=\"$version_name\">" \
    "  <uses-sdk android:minSdkVersion=\"$min_sdk\" android:targetSdkVersion=\"$target_sdk\" />" \
    "$extra_permission" \
    '  <application android:allowBackup="false" android:debuggable="false" />' \
    '</manifest>'
  exit 0
fi
exit 24
EOF
chmod +x "$fake_bundletool"
export CM_BUNDLETOOL_BIN="$fake_bundletool"

fake_jarsigner="$case_root/fake-jarsigner"
cat > "$fake_jarsigner" <<'EOF'
#!/usr/bin/env bash
set -eu
mode="${FAKE_JARSIGNER_MODE:-success}"
strict=0
for argument in "$@"; do
  [ "$argument" != "-strict" ] || strict=1
done
if [ "$mode" = "unsigned" ]; then
  printf 'jar is unsigned.\n'
  exit 0
fi
[ "$mode" != "partially-unsigned" ] || {
  printf '%s\n' 'jar verified.' 'This jar contains unsigned entries which have not been integrity-checked.'
  [ "$strict" -eq 0 ] || exit 20
  exit 0
}
[ "$mode" != "self-signed" ] || {
  printf '%s\n' \
    'jar verified, with signer errors.' \
    '' \
    'Error: ' \
    'This jar contains entries whose certificate chain is invalid. Reason: unable to find valid certification path to requested target' \
    'This jar contains entries whose signer certificate is self-signed.'
  [ "$strict" -eq 0 ] || exit 4
  exit 0
}
[ "$mode" != "expired-certificate" ] || {
  printf '%s\n' \
    'jar verified, with signer errors.' \
    '' \
    'Error: ' \
    'This jar contains entries whose signer certificate has expired.' \
    'This jar contains entries whose certificate chain is invalid. Reason: unable to find valid certification path to requested target' \
    'This jar contains entries whose signer certificate is self-signed.'
  [ "$strict" -eq 0 ] || exit 4
  exit 0
}
[ "$mode" != "future-certificate" ] || {
  printf '%s\n' \
    'jar verified, with signer errors.' \
    '' \
    'Error: ' \
    'This jar contains entries whose signer certificate is not yet valid.' \
    'This jar contains entries whose certificate chain is invalid. Reason: unable to find valid certification path to requested target' \
    'This jar contains entries whose signer certificate is self-signed.'
  [ "$strict" -eq 0 ] || exit 4
  exit 0
}
[ "$mode" != "disabled-algorithm" ] || {
  printf '%s\n' \
    'jar verified, with signer errors.' \
    '' \
    'Error: ' \
    'This jar contains entries whose signer certificate is self-signed.' \
    'The jar uses a signature algorithm that is disabled.'
  [ "$strict" -eq 0 ] || exit 4
  exit 0
}
[ "$mode" != "negative-summary" ] || {
  printf 'not jar verified.\n'
  exit 0
}
[ "$mode" != "fail" ] || exit 25
printf 'jar verified.\n'
EOF
chmod +x "$fake_jarsigner"
export CM_JARSIGNER_BIN="$fake_jarsigner"

# Tool-path overrides are test seams, not production knobs. An ambient override without an explicit
# wrapper-test marker must fail before Unity and publish nothing.
unguarded_override_out="$case_root/unguarded-override-test-proof.aab"
set +e
CM_UNITY_BIN="$fake_unity" bash "$case_root/scripts/build-aab.sh" "$unguarded_override_out" \
  > "$case_root/unguarded-override.log" 2>&1
unguarded_override_rc=$?
set -e
[ "$unguarded_override_rc" -ne 0 ] \
  || fail "production invocation accepted validator/Unity override seams"
[ ! -e "$unguarded_override_out" ] \
  || fail "unguarded tool overrides published an AAB"
export CM_AAB_TEST_MODE=1

test_mode_release_out="$case_root/CatMetro-release.aab"
set +e
CM_UNITY_BIN="$fake_unity" bash "$case_root/scripts/build-aab.sh" "$test_mode_release_out" \
  > "$case_root/test-mode-release-name.log" 2>&1
test_mode_release_rc=$?
set -e
[ "$test_mode_release_rc" -ne 0 ] \
  || fail "test mode produced an upload-looking release AAB"
[ ! -e "$test_mode_release_out" ] \
  || fail "test mode left an upload-looking release AAB"

out="$case_root/CatMetro-wrapper-test-proof.aab"
run_log="$case_root/run.log"
if ! CM_UNITY_BIN="$fake_unity" bash "$case_root/scripts/build-aab.sh" "$out" \
  > "$run_log" 2>&1; then
  sed -n '1,160p' "$run_log" >&2
  fail "CM_UNITY_BIN did not drive the real wrapper to a successful AAB"
fi

[ -f "$out" ] || fail "successful fake Unity run did not produce the requested AAB"
unzip -tqq "$out" || fail "wrapper returned a corrupt AAB"
grep -q 'TEST MODE PIPELINE PROOF.*NOT UPLOADABLE' "$run_log" \
  || fail "test-seam artifact was not unmistakably marked non-uploadable"
if grep -q 'NEXT STEP IS HUMAN-ONLY' "$run_log"; then
  fail "test-seam artifact printed a Play upload instruction"
fi
listing_out="${out%.aab}-play-listing.md"
[ -f "$listing_out" ] || fail "exact AAB did not produce its count-bound listing sidecar"
grep -q 'COUNT-BOUND LISTING CANDIDATE' "$listing_out" \
  || fail "listing sidecar overstated automatic clearance of release-gated claims"
grep -q 'TEST MODE PIPELINE PROOF.*NOT UPLOADABLE' "$listing_out" \
  || fail "test-seam listing was not unmistakably marked non-uploadable"
grep -q 'Campaign levels in exact AAB: 17' "$listing_out" \
  || fail "listing receipt is not bound to the exact AAB campaign count"
grep -q '17 HANDCRAFTED LEVELS' "$listing_out" \
  || fail "listing copy did not render the artifact-derived campaign count"
grep -q "Listing fields: OK (title 23/30, short 79/80, full 1040/4000, what's-new 249/500)" "$run_log" \
  || fail "wrapper did not enforce and report Play listing field limits"
if grep -q '__CAMPAIGN_LEVEL_COUNT__' "$listing_out"; then
  fail "unrendered campaign-count token escaped into candidate copy"
fi

# Exercise default validator discovery instead of letting every behavioral case ride explicit
# validator overrides. Only Unity itself is overridden; its sibling PlaybackEngines tree mirrors
# the installed Unity Hub layout.
default_editor="$case_root/default-editor"
default_unity="$default_editor/Unity.app/Contents/MacOS/Unity"
default_tools="$default_editor/PlaybackEngines/AndroidPlayer/Tools"
default_jdk="$default_editor/PlaybackEngines/AndroidPlayer/OpenJDK/bin"
mkdir -p "$(dirname "$default_unity")" "$default_tools" "$default_jdk"
cp "$fake_unity" "$default_unity"
cp "$fake_bundletool" "$default_tools/bundletool-all-test.jar"
cp "$fake_jarsigner" "$default_jdk/jarsigner"
cat > "$default_jdk/java" <<'EOF'
#!/usr/bin/env bash
set -eu
[ "${1:-}" = "-jar" ] || exit 88
shift
jar_path="$1"
shift
exec "$jar_path" "$@"
EOF
chmod +x "$default_unity" "$default_tools/bundletool-all-test.jar" \
  "$default_jdk/java" "$default_jdk/jarsigner"
default_discovery_out="$case_root/default-tool-discovery-test-proof.aab"
if ! (unset CM_BUNDLETOOL_BIN CM_BUNDLETOOL_JAR CM_JAVA_BIN CM_JARSIGNER_BIN; \
  CM_UNITY_BIN="$default_unity" bash "$case_root/scripts/build-aab.sh" "$default_discovery_out") \
  > "$case_root/default-tool-discovery.log" 2>&1
then
  fail "wrapper did not discover validators beside the selected Unity installation"
fi
[ -f "$default_discovery_out" ] || fail "default tool discovery did not publish its test artifact"

# Release candidates are immutable. A second custom-signed invocation targeting an existing AAB
# must fail before it can replace the artifact, listing, or successful build log.
before_sha="$(shasum -a 256 "$out" | awk '{print $1}')"
before_listing_sha="$(shasum -a 256 "$listing_out" | awk '{print $1}')"
success_unity_log="${out%.aab}-unity-build.log"
[ -f "$success_unity_log" ] || fail "successful Unity log was not preserved beside the AAB"
before_success_log_sha="$(shasum -a 256 "$success_unity_log" | awk '{print $1}')"
no_clobber_log="$case_root/no-clobber.log"
set +e
FAKE_BUILD_MARKER=replacement CM_UNITY_BIN="$fake_unity" \
  bash "$case_root/scripts/build-aab.sh" "$out" > "$no_clobber_log" 2>&1
no_clobber_rc=$?
set -e

[ "$no_clobber_rc" -ne 0 ] || fail "release invocation overwrote an existing candidate"
after_sha="$(shasum -a 256 "$out" | awk '{print $1}')"
[ "$after_sha" = "$before_sha" ] || fail "no-clobber gate changed the previous AAB"
after_listing_sha="$(shasum -a 256 "$listing_out" | awk '{print $1}')"
[ "$after_listing_sha" = "$before_listing_sha" ] \
  || fail "no-clobber gate changed the previous listing receipt"
after_success_log_sha="$(shasum -a 256 "$success_unity_log" | awk '{print $1}')"
[ "$after_success_log_sha" = "$before_success_log_sha" ] \
  || fail "no-clobber gate changed the successful Unity log"

# Logs are release evidence too. A fresh AAB/listing path must not allow either successful or
# failed-log destination to overwrite an earlier attempt with the same stem.
for occupied_log_kind in unity-build failed-release-build; do
  log_collision_out="$case_root/log-collision-$occupied_log_kind-test-proof.aab"
  occupied_log="${log_collision_out%.aab}-$occupied_log_kind.log"
  printf 'historical-%s-log\n' "$occupied_log_kind" > "$occupied_log"
  occupied_log_sha="$(shasum -a 256 "$occupied_log" | awk '{print $1}')"
  set +e
  CM_UNITY_BIN="$fake_unity" bash "$case_root/scripts/build-aab.sh" "$log_collision_out" \
    > "$case_root/log-collision-$occupied_log_kind.log" 2>&1
  log_collision_rc=$?
  set -e
  [ "$log_collision_rc" -ne 0 ] \
    || fail "wrapper accepted an occupied $occupied_log_kind evidence path"
  [ ! -e "$log_collision_out" ] \
    && [ ! -e "${log_collision_out%.aab}-play-listing.md" ] \
    || fail "log collision published an AAB or listing"
  [ "$(shasum -a 256 "$occupied_log" | awk '{print $1}')" = "$occupied_log_sha" ] \
    || fail "wrapper overwrote the historical $occupied_log_kind evidence log"
done

# A fresh-path Unity failure propagates its exit code, publishes nothing, and preserves a distinct
# diagnostic log. This is the direct stale-artifact regression: failure can never fall through to
# artifact discovery or a success message.
failed_out="$case_root/fresh-unity-failure-test-proof.aab"
failed_log="$case_root/failed-run.log"
set +e
FAKE_UNITY_MODE=fail CM_UNITY_BIN="$fake_unity" \
  bash "$case_root/scripts/build-aab.sh" "$failed_out" > "$failed_log" 2>&1
failed_rc=$?
set -e
[ "$failed_rc" -eq 42 ] || fail "wrapper did not propagate Unity's failure exit code"
[ ! -e "$failed_out" ] && [ ! -e "${failed_out%.aab}-play-listing.md" ] \
  || fail "failed Unity invocation published an artifact or listing"
[ -f "${failed_out%.aab}-failed-release-build.log" ] \
  || fail "failed Unity invocation did not preserve its own diagnostic log"
if grep -qi 'uploadable' "$failed_log"; then
  fail "failed build described a stale artifact as uploadable"
fi

# Release validation is fail-closed: if the structural verifier is unavailable, no final
# artifact is published. Supply every command the wrapper/fake uses except unzip.
no_unzip_bin="$case_root/no-unzip-bin"
mkdir -p "$no_unzip_bin"
for tool in bash grep awk dirname mkdir mktemp basename tail head ls shasum cut mv rm rmdir cp zip; do
  tool_path="$(command -v "$tool")"
  ln -s "$tool_path" "$no_unzip_bin/$tool"
done
no_unzip_out="$case_root/no-unzip-test-proof.aab"
no_unzip_log="$case_root/no-unzip.log"
set +e
PATH="$no_unzip_bin" CM_UNITY_BIN="$fake_unity" \
  /bin/bash "$case_root/scripts/build-aab.sh" "$no_unzip_out" > "$no_unzip_log" 2>&1
no_unzip_rc=$?
set -e

[ "$no_unzip_rc" -ne 0 ] || fail "wrapper published an AAB without structural verification"
[ ! -e "$no_unzip_out" ] || fail "unverified AAB escaped the atomic staging directory"
grep -q 'unzip is required for mandatory AAB validation' "$no_unzip_log" \
  || fail "missing-unzip test did not reach the wrapper's structural-verifier gate"

for broken_mode in missing-manifest missing-dex missing-arm64; do
  broken_out="$case_root/$broken_mode-test-proof.aab"
  broken_log="$case_root/$broken_mode.log"
  set +e
  FAKE_UNITY_MODE="$broken_mode" CM_UNITY_BIN="$fake_unity" \
    bash "$case_root/scripts/build-aab.sh" "$broken_out" > "$broken_log" 2>&1
  broken_rc=$?
  set -e
  [ "$broken_rc" -ne 0 ] \
    || fail "wrapper accepted structurally incomplete bundle: $broken_mode"
  [ ! -e "$broken_out" ] \
    || fail "structurally incomplete bundle escaped staging: $broken_mode"
done

extra_arch_out="$case_root/extra-architecture-test-proof.aab"
set +e
FAKE_UNITY_MODE=extra-x86 CM_UNITY_BIN="$fake_unity" \
  bash "$case_root/scripts/build-aab.sh" "$extra_arch_out" \
  > "$case_root/extra-architecture.log" 2>&1
extra_arch_rc=$?
set -e
[ "$extra_arch_rc" -ne 0 ] || fail "wrapper accepted an architecture outside ARM64"
[ ! -e "$extra_arch_out" ] || fail "wrong-ABI bundle escaped staging"

feature_arch_out="$case_root/feature-extra-architecture-test-proof.aab"
set +e
FAKE_UNITY_MODE=feature-extra-x86 CM_UNITY_BIN="$fake_unity" \
  bash "$case_root/scripts/build-aab.sh" "$feature_arch_out" \
  > "$case_root/feature-extra-architecture.log" 2>&1
feature_arch_rc=$?
set -e
[ "$feature_arch_rc" -ne 0 ] \
  || fail "wrapper accepted a non-ARM64 native library outside the base module"
[ ! -e "$feature_arch_out" ] || fail "feature-module wrong-ABI bundle escaped staging"

invalid_bundle_out="$case_root/bundletool-invalid-test-proof.aab"
set +e
FAKE_BUNDLETOOL_MODE=fail-validation CM_UNITY_BIN="$fake_unity" \
  bash "$case_root/scripts/build-aab.sh" "$invalid_bundle_out" \
  > "$case_root/bundletool-invalid.log" 2>&1
invalid_bundle_rc=$?
set -e
[ "$invalid_bundle_rc" -ne 0 ] || fail "wrapper accepted an AAB rejected by bundletool"
[ ! -e "$invalid_bundle_out" ] || fail "bundletool-invalid artifact escaped staging"

unsigned_out="$case_root/unsigned-test-proof.aab"
set +e
FAKE_JARSIGNER_MODE=unsigned CM_UNITY_BIN="$fake_unity" \
  bash "$case_root/scripts/build-aab.sh" "$unsigned_out" \
  > "$case_root/unsigned.log" 2>&1
unsigned_rc=$?
set -e
[ "$unsigned_rc" -ne 0 ] || fail "wrapper accepted an artifact without a verified JAR signature"
[ ! -e "$unsigned_out" ] || fail "unsigned artifact escaped staging"

partial_signature_out="$case_root/partially-unsigned-test-proof.aab"
set +e
FAKE_JARSIGNER_MODE=partially-unsigned CM_UNITY_BIN="$fake_unity" \
  bash "$case_root/scripts/build-aab.sh" "$partial_signature_out" \
  > "$case_root/partially-unsigned.log" 2>&1
partial_signature_rc=$?
set -e
[ "$partial_signature_rc" -ne 0 ] \
  || fail "wrapper accepted an AAB containing unsigned entries"
[ ! -e "$partial_signature_out" ] || fail "partially unsigned artifact escaped staging"

self_signed_out="$case_root/self-signed-test-proof.aab"
if ! FAKE_JARSIGNER_MODE=self-signed CM_UNITY_BIN="$fake_unity" \
  bash "$case_root/scripts/build-aab.sh" "$self_signed_out" \
  > "$case_root/self-signed.log" 2>&1
then
  fail "wrapper rejected a fully signed upload-key AAB solely because its chain is self-signed"
fi
[ -f "$self_signed_out" ] || fail "fully signed upload-key AAB did not publish"

for bad_signer_mode in expired-certificate future-certificate disabled-algorithm; do
  bad_signer_out="$case_root/$bad_signer_mode-test-proof.aab"
  set +e
  FAKE_JARSIGNER_MODE="$bad_signer_mode" CM_UNITY_BIN="$fake_unity" \
    bash "$case_root/scripts/build-aab.sh" "$bad_signer_out" \
    > "$case_root/$bad_signer_mode.log" 2>&1
  bad_signer_rc=$?
  set -e
  [ "$bad_signer_rc" -ne 0 ] \
    || fail "wrapper accepted invalid signer state sharing strict status bit 4: $bad_signer_mode"
  [ ! -e "$bad_signer_out" ] || fail "invalid-signer artifact escaped staging: $bad_signer_mode"
done

negative_summary_out="$case_root/negative-signer-summary-test-proof.aab"
set +e
FAKE_JARSIGNER_MODE=negative-summary CM_UNITY_BIN="$fake_unity" \
  bash "$case_root/scripts/build-aab.sh" "$negative_summary_out" \
  > "$case_root/negative-signer-summary.log" 2>&1
negative_summary_rc=$?
set -e
[ "$negative_summary_rc" -ne 0 ] || fail "wrapper accepted a negated jarsigner success sentence"
[ ! -e "$negative_summary_out" ] || fail "negative-signer-summary artifact escaped staging"

for bad_manifest_mode in bad-package bad-target bad-version-name dangerous-permission notification-permission; do
  bad_manifest_out="$case_root/$bad_manifest_mode-test-proof.aab"
  set +e
  FAKE_BUNDLETOOL_MODE="$bad_manifest_mode" CM_UNITY_BIN="$fake_unity" \
    bash "$case_root/scripts/build-aab.sh" "$bad_manifest_out" \
    > "$case_root/$bad_manifest_mode.log" 2>&1
  bad_manifest_rc=$?
  set -e
  [ "$bad_manifest_rc" -ne 0 ] \
    || fail "wrapper accepted invalid built manifest: $bad_manifest_mode"
  [ ! -e "$bad_manifest_out" ] \
    || fail "invalid-built-manifest artifact escaped staging: $bad_manifest_mode"
done

feature_permission_out="$case_root/feature-dangerous-permission-test-proof.aab"
set +e
FAKE_UNITY_MODE=feature-module FAKE_BUNDLETOOL_MODE=feature-dangerous-permission \
  CM_UNITY_BIN="$fake_unity" bash "$case_root/scripts/build-aab.sh" "$feature_permission_out" \
  > "$case_root/feature-dangerous-permission.log" 2>&1
feature_permission_rc=$?
set -e
[ "$feature_permission_rc" -ne 0 ] \
  || fail "wrapper ignored a sensitive permission declared outside the base module"
[ ! -e "$feature_permission_out" ] \
  || fail "feature-permission bundle escaped staging"

missing_campaign_out="$case_root/missing-campaign-level-test-proof.aab"
set +e
FAKE_UNITY_MODE=receipt-names-missing-level CM_UNITY_BIN="$fake_unity" \
  bash "$case_root/scripts/build-aab.sh" "$missing_campaign_out" \
  > "$case_root/missing-campaign-level.log" 2>&1
missing_campaign_rc=$?
set -e
[ "$missing_campaign_rc" -ne 0 ] \
  || fail "wrapper trusted a campaign receipt naming content absent from the exact AAB"
[ ! -e "$missing_campaign_out" ] \
  || fail "campaign-mismatched bundle escaped staging"

mutated_level_out="$case_root/mutated-level-bytes-test-proof.aab"
set +e
FAKE_UNITY_MODE=mutated-level-bytes CM_UNITY_BIN="$fake_unity" \
  bash "$case_root/scripts/build-aab.sh" "$mutated_level_out" \
  > "$case_root/mutated-level-bytes.log" 2>&1
mutated_level_rc=$?
set -e
[ "$mutated_level_rc" -ne 0 ] \
  || fail "wrapper accepted campaign bytes different from the source validated before build"
[ ! -e "$mutated_level_out" ] \
  || fail "campaign-byte-mismatched bundle escaped staging"

# The wrapper accepts one canonical, full-line success receipt only. A prefixed marker must not
# override the genuine signing state, and a second canonical marker makes the invocation ambiguous.
spoofed_receipt_out="$case_root/spoofed-receipt-test-proof.aab"
set +e
FAKE_UNITY_MODE=spoofed-receipt CM_UNITY_BIN="$fake_unity" \
  bash "$case_root/scripts/build-aab.sh" "$spoofed_receipt_out" \
  > "$case_root/spoofed-receipt.log" 2>&1
spoofed_receipt_rc=$?
set -e
[ "$spoofed_receipt_rc" -ne 0 ] || fail "prefixed receipt text overrode the real debug receipt"
[ ! -e "$spoofed_receipt_out" ] || fail "spoofed-receipt bundle escaped staging"

duplicate_receipt_out="$case_root/duplicate-receipt-test-proof.aab"
set +e
FAKE_UNITY_MODE=duplicate-receipt CM_UNITY_BIN="$fake_unity" \
  bash "$case_root/scripts/build-aab.sh" "$duplicate_receipt_out" \
  > "$case_root/duplicate-receipt.log" 2>&1
duplicate_receipt_rc=$?
set -e
[ "$duplicate_receipt_rc" -ne 0 ] || fail "wrapper accepted multiple canonical success receipts"
[ ! -e "$duplicate_receipt_out" ] || fail "multiple-receipt bundle escaped staging"

debug_out="$case_root/unapproved-debug-signing-test-proof.aab"
set +e
FAKE_UNITY_MODE=debug-signing CM_UNITY_BIN="$fake_unity" \
  bash "$case_root/scripts/build-aab.sh" "$debug_out" \
  > "$case_root/unapproved-debug-signing.log" 2>&1
debug_rc=$?
set -e
[ "$debug_rc" -ne 0 ] || fail "wrapper accepted debug signing without explicit opt-in"
[ ! -e "$debug_out" ] || fail "unapproved debug-signed bundle escaped staging"

production_looking_debug_out="$case_root/CatMetro-1.0.0-test-proof.aab"
set +e
CM_ALLOW_DEBUG_SIGNING=1 FAKE_UNITY_MODE=debug-signing CM_UNITY_BIN="$fake_unity" \
  bash "$case_root/scripts/build-aab.sh" "$production_looking_debug_out" \
  > "$case_root/production-looking-debug-proof.log" 2>&1
production_looking_debug_rc=$?
set -e
[ "$production_looking_debug_rc" -ne 0 ] \
  || fail "debug proof accepted a filename without the mandatory debug-proof suffix"
[ ! -e "$production_looking_debug_out" ] \
  || fail "production-looking debug proof escaped staging"

# A debug pipeline proof must never overwrite an existing candidate, even when the caller opts in.
# The proof uses a distinct, new, non-release filename and its sidecar carries the same warning.
release_before_debug_sha="$(shasum -a 256 "$out" | awk '{print $1}')"
release_listing_before_debug_sha="$(shasum -a 256 "$listing_out" | awk '{print $1}')"
set +e
CM_ALLOW_DEBUG_SIGNING=1 FAKE_UNITY_MODE=debug-signing CM_UNITY_BIN="$fake_unity" \
  bash "$case_root/scripts/build-aab.sh" "$out" \
  > "$case_root/debug-release-clobber.log" 2>&1
debug_release_rc=$?
set -e
[ "$debug_release_rc" -ne 0 ] \
  || fail "debug proof overwrote an uploadable-looking release output"
[ "$(shasum -a 256 "$out" | awk '{print $1}')" = "$release_before_debug_sha" ] \
  || fail "debug proof changed the previous release AAB"
[ "$(shasum -a 256 "$listing_out" | awk '{print $1}')" = "$release_listing_before_debug_sha" ] \
  || fail "debug proof changed the previous release listing"

debug_proof_out="$case_root/approved-debug-proof-test-proof.aab"
if ! CM_ALLOW_DEBUG_SIGNING=1 FAKE_UNITY_MODE=debug-signing CM_UNITY_BIN="$fake_unity" \
  bash "$case_root/scripts/build-aab.sh" "$debug_proof_out" \
  > "$case_root/approved-debug-proof.log" 2>&1
then
  fail "explicitly approved debug-signed pipeline proof was rejected"
fi
[ -f "$debug_proof_out" ] || fail "approved debug pipeline proof was not produced"
grep -q 'NOT UPLOADABLE' "$case_root/approved-debug-proof.log" \
  || fail "debug pipeline proof was not unmistakably marked non-uploadable"
grep -q 'DEBUG-SIGNED PIPELINE PROOF.*NOT UPLOADABLE' \
  "${debug_proof_out%.aab}-play-listing.md" \
  || fail "debug pipeline proof listing was not unmistakably marked non-uploadable"

preexisting_debug_out="$case_root/preexisting-debug-proof-test-proof.aab"
preexisting_debug_listing="${preexisting_debug_out%.aab}-play-listing.md"
cp "$out" "$preexisting_debug_out"
cp "$listing_out" "$preexisting_debug_listing"
preexisting_debug_sha="$(shasum -a 256 "$preexisting_debug_out" | awk '{print $1}')"
set +e
CM_ALLOW_DEBUG_SIGNING=1 FAKE_UNITY_MODE=debug-signing CM_UNITY_BIN="$fake_unity" \
  bash "$case_root/scripts/build-aab.sh" "$preexisting_debug_out" \
  > "$case_root/preexisting-debug-proof.log" 2>&1
preexisting_debug_rc=$?
set -e
[ "$preexisting_debug_rc" -ne 0 ] || fail "debug proof clobbered an existing proof output"
[ "$(shasum -a 256 "$preexisting_debug_out" | awk '{print $1}')" = "$preexisting_debug_sha" ] \
  || fail "debug proof changed a pre-existing proof artifact"

# If either atomic hard-link publication fails, neither half of the new immutable pair may remain.
ln_shim_dir="$case_root/ln-shim"
mkdir "$ln_shim_dir"
real_ln_bin="$(command -v ln)"
cat > "$ln_shim_dir/ln" <<'EOF'
#!/usr/bin/env bash
set -eu
count=0
[ ! -f "$FAKE_LN_COUNT_FILE" ] || count="$(awk 'NR == 1 { print; exit }' "$FAKE_LN_COUNT_FILE")"
count=$((count + 1))
printf '%s\n' "$count" > "$FAKE_LN_COUNT_FILE"
if [ "${FAKE_LN_SIGNAL_AFTER_FIRST:-0}" = "1" ] && [ "$count" -eq 1 ]; then
  "$REAL_LN_BIN" "$@"
  kill -HUP "$PPID"
  exit 0
fi
if [ "$count" -eq 2 ]; then
  exit 47
fi
exec "$REAL_LN_BIN" "$@"
EOF
chmod +x "$ln_shim_dir/ln"
publish_rollback_out="$case_root/publish-rollback-test-proof.aab"
set +e
PATH="$ln_shim_dir:$PATH" REAL_LN_BIN="$real_ln_bin" \
  FAKE_LN_COUNT_FILE="$case_root/ln-count" FAKE_BUILD_MARKER=second-build \
  CM_UNITY_BIN="$fake_unity" bash "$case_root/scripts/build-aab.sh" "$publish_rollback_out" \
  > "$case_root/publish-rollback.log" 2>&1
publish_rollback_rc=$?
set -e
[ "$publish_rollback_rc" -ne 0 ] || fail "wrapper ignored a failed atomic AAB publication"
[ ! -e "$publish_rollback_out" ] \
  || fail "failed publication left a partial AAB"
[ ! -e "${publish_rollback_out%.aab}-play-listing.md" ] \
  || fail "failed publication left a partial listing"

signal_publish_out="$case_root/signal-publication-test-proof.aab"
set +e
PATH="$ln_shim_dir:$PATH" REAL_LN_BIN="$real_ln_bin" \
  FAKE_LN_COUNT_FILE="$case_root/ln-signal-count" FAKE_LN_SIGNAL_AFTER_FIRST=1 \
  CM_UNITY_BIN="$fake_unity" bash "$case_root/scripts/build-aab.sh" "$signal_publish_out" \
  > "$case_root/signal-publication.log" 2>&1
signal_publish_rc=$?
set -e
[ "$signal_publish_rc" -ne 0 ] || fail "wrapper ignored a signal during atomic publication"
[ ! -e "$signal_publish_out" ] \
  || fail "signal during publication left a partial AAB"
[ ! -e "${signal_publish_out%.aab}-play-listing.md" ] \
  || fail "signal during publication left a partial listing"

directory_destination_out="$case_root/listing-destination-test-proof.aab"
mkdir "${directory_destination_out%.aab}-play-listing.md"
set +e
CM_UNITY_BIN="$fake_unity" \
  bash "$case_root/scripts/build-aab.sh" "$directory_destination_out" \
  > "$case_root/listing-destination.log" 2>&1
directory_destination_rc=$?
set -e
[ "$directory_destination_rc" -ne 0 ] \
  || fail "wrapper reported success when the listing destination was a directory"
[ ! -e "$directory_destination_out" ] \
  || fail "AAB was published without its exact sibling listing"

dangling_out="$case_root/dangling-output-test-proof.aab"
ln -s "$case_root/missing-aab-target" "$dangling_out"
set +e
CM_UNITY_BIN="$fake_unity" bash "$case_root/scripts/build-aab.sh" "$dangling_out" \
  > "$case_root/dangling-output.log" 2>&1
dangling_rc=$?
set -e
[ "$dangling_rc" -ne 0 ] || fail "wrapper replaced a pre-existing dangling AAB symlink"
[ -L "$dangling_out" ] || fail "dangling AAB symlink was removed or replaced"
[ "$(readlink "$dangling_out")" = "$case_root/missing-aab-target" ] \
  || fail "dangling AAB symlink target changed"

dangling_listing_out="$case_root/dangling-listing-test-proof.aab"
dangling_listing="${dangling_listing_out%.aab}-play-listing.md"
ln -s "$case_root/missing-listing-target" "$dangling_listing"
set +e
CM_UNITY_BIN="$fake_unity" bash "$case_root/scripts/build-aab.sh" "$dangling_listing_out" \
  > "$case_root/dangling-listing.log" 2>&1
dangling_listing_rc=$?
set -e
[ "$dangling_listing_rc" -ne 0 ] \
  || fail "wrapper replaced a pre-existing dangling listing symlink"
[ -L "$dangling_listing" ] || fail "dangling listing symlink was removed or replaced"
[ "$(readlink "$dangling_listing")" = "$case_root/missing-listing-target" ] \
  || fail "dangling listing symlink target changed"

locked_out="$case_root/already-building-test-proof.aab"
mkdir "${locked_out}.lock"
set +e
CM_UNITY_BIN="$fake_unity" bash "$case_root/scripts/build-aab.sh" "$locked_out" \
  > "$case_root/already-building.log" 2>&1
locked_rc=$?
set -e
[ "$locked_rc" -ne 0 ] || fail "wrapper ignored an existing same-output build lock"
[ ! -e "$locked_out" ] || fail "concurrent-output artifact escaped staging"
rmdir "${locked_out}.lock"

# A store build must fail before Unity when a release-critical tracked setting drifts.
perl -0pi -e 's/AndroidTargetSdkVersion: 36/AndroidTargetSdkVersion: 35/' \
  "$case_root/unity/ProjectSettings/ProjectSettings.asset"
bad_api_out="$case_root/bad-api-test-proof.aab"
bad_api_log="$case_root/bad-api.log"
set +e
CM_UNITY_BIN="$fake_unity" bash "$case_root/scripts/build-aab.sh" "$bad_api_out" \
  > "$bad_api_log" 2>&1
bad_api_rc=$?
set -e
[ "$bad_api_rc" -ne 0 ] || fail "wrapper accepted target API 35 for a 2026 Play release"
[ ! -e "$bad_api_out" ] || fail "bad-settings build published an AAB"

perl -0pi -e 's/AndroidTargetSdkVersion: 35/AndroidTargetSdkVersion: 36/; s/Android: com\.catmetro\.game/Android: com.example.placeholder/' \
  "$case_root/unity/ProjectSettings/ProjectSettings.asset"
bad_id_out="$case_root/bad-id-test-proof.aab"
set +e
CM_UNITY_BIN="$fake_unity" bash "$case_root/scripts/build-aab.sh" "$bad_id_out" \
  > "$case_root/bad-id.log" 2>&1
bad_id_rc=$?
set -e
[ "$bad_id_rc" -ne 0 ] || fail "wrapper accepted the wrong permanent application ID"
[ ! -e "$bad_id_out" ] || fail "wrong-application-ID build published an AAB"

perl -0pi -e 's/Android: com\.example\.placeholder/Android: com.catmetro.game/; s/AndroidTargetArchitectures: 2/AndroidTargetArchitectures: 1/' \
  "$case_root/unity/ProjectSettings/ProjectSettings.asset"
bad_arch_out="$case_root/bad-arch-test-proof.aab"
set +e
CM_UNITY_BIN="$fake_unity" bash "$case_root/scripts/build-aab.sh" "$bad_arch_out" \
  > "$case_root/bad-arch.log" 2>&1
bad_arch_rc=$?
set -e
[ "$bad_arch_rc" -ne 0 ] || fail "wrapper accepted an ARMv7-only Play release"
[ ! -e "$bad_arch_out" ] || fail "wrong-architecture build published an AAB"

perl -0pi -e 's/AndroidTargetArchitectures: 1/AndroidTargetArchitectures: 2/; s/(scriptingBackend:\n    Android:) 1/$1 0/' \
  "$case_root/unity/ProjectSettings/ProjectSettings.asset"
bad_backend_out="$case_root/bad-backend-test-proof.aab"
set +e
CM_UNITY_BIN="$fake_unity" bash "$case_root/scripts/build-aab.sh" "$bad_backend_out" \
  > "$case_root/bad-backend.log" 2>&1
bad_backend_rc=$?
set -e
[ "$bad_backend_rc" -ne 0 ] || fail "wrapper accepted Mono instead of IL2CPP for Android"
[ ! -e "$bad_backend_out" ] || fail "wrong-scripting-backend build published an AAB"

perl -0pi -e 's/(scriptingBackend:\n    Android:) 0/$1 1/; s/AndroidBundleVersionCode: 1/AndroidBundleVersionCode: 0/' \
  "$case_root/unity/ProjectSettings/ProjectSettings.asset"
bad_code_out="$case_root/bad-version-code-test-proof.aab"
set +e
CM_UNITY_BIN="$fake_unity" bash "$case_root/scripts/build-aab.sh" "$bad_code_out" \
  > "$case_root/bad-version-code.log" 2>&1
bad_code_rc=$?
set -e
[ "$bad_code_rc" -ne 0 ] || fail "wrapper accepted non-positive Android version code"
[ ! -e "$bad_code_out" ] || fail "invalid-version-code build published an AAB"

perl -0pi -e 's/AndroidBundleVersionCode: 0/AndroidBundleVersionCode: 2100000001/' \
  "$case_root/unity/ProjectSettings/ProjectSettings.asset"
too_large_code_out="$case_root/too-large-version-code-test-proof.aab"
set +e
CM_UNITY_BIN="$fake_unity" bash "$case_root/scripts/build-aab.sh" "$too_large_code_out" \
  > "$case_root/too-large-version-code.log" 2>&1
too_large_code_rc=$?
set -e
[ "$too_large_code_rc" -ne 0 ] || fail "wrapper accepted Android version code above Play's limit"
[ ! -e "$too_large_code_out" ] || fail "too-large-version-code build published an AAB"

perl -0pi -e 's/AndroidBundleVersionCode: 2100000001/AndroidBundleVersionCode: 1/; s/bundleVersion: 1\.0\.0/bundleVersion: 0.1.0/' \
  "$case_root/unity/ProjectSettings/ProjectSettings.asset"
prerelease_out="$case_root/prepublic-version-test-proof.aab"
set +e
CM_UNITY_BIN="$fake_unity" bash "$case_root/scripts/build-aab.sh" "$prerelease_out" \
  > "$case_root/prerelease-version.log" 2>&1
prerelease_rc=$?
set -e
[ "$prerelease_rc" -ne 0 ] || fail "wrapper accepted a pre-1.0 public release version"
[ ! -e "$prerelease_out" ] || fail "pre-1.0 release build published an AAB"

perl -0pi -e 's/bundleVersion: 0\.1\.0/bundleVersion: 1.0.0/; s/ForceSDCardPermission: 0/ForceSDCardPermission: 1/' \
  "$case_root/unity/ProjectSettings/ProjectSettings.asset"
storage_permission_out="$case_root/forced-storage-permission-test-proof.aab"
set +e
CM_UNITY_BIN="$fake_unity" bash "$case_root/scripts/build-aab.sh" "$storage_permission_out" \
  > "$case_root/forced-storage-permission.log" 2>&1
storage_permission_rc=$?
set -e
[ "$storage_permission_rc" -ne 0 ] \
  || fail "wrapper accepted an unnecessary forced external-storage permission"
[ ! -e "$storage_permission_out" ] \
  || fail "forced-storage-permission build published an AAB"

perl -0pi -e 's/ForceSDCardPermission: 1/ForceSDCardPermission: 0/; s/ForceInternetPermission: 0/ForceInternetPermission: 1/' \
  "$case_root/unity/ProjectSettings/ProjectSettings.asset"
forced_internet_out="$case_root/forced-internet-permission-test-proof.aab"
set +e
CM_UNITY_BIN="$fake_unity" bash "$case_root/scripts/build-aab.sh" "$forced_internet_out" \
  > "$case_root/forced-internet-permission.log" 2>&1
forced_internet_rc=$?
set -e
[ "$forced_internet_rc" -ne 0 ] \
  || fail "wrapper accepted an unnecessarily forced internet permission"
[ ! -e "$forced_internet_out" ] \
  || fail "forced-internet-permission build published an AAB"

echo "build-aab-wrapper.test.sh: OK"
