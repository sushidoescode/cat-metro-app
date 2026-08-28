#!/usr/bin/env bash
# Build a RELEASE Android App Bundle (.aab) for a Google Play upload.
#
# Run this yourself — Unity cannot run inside the agent sandbox: a cold Library resolves
# packages over the network and the editor writes to ~/Library/Unity, both outside the
# sandbox's allowlist. This script never uploads anything; uploading to Play is human-only.
#
#   bash scripts/build-aab.sh [output.aab]
#
# Signing: this script never reads keystore material. A human configures the upload key in
# Unity (Project Settings > Player > Publishing Settings). Unity can serialize its local path
# and alias into the tracked ProjectSettings file, so inspect and sanitize the git diff after
# every signed build. Without custom signing the builder refuses to produce a release bundle,
# because a debug-signed .aab is not uploadable to Play.
#
# Escape hatch, for proving the pipeline works before a keystore exists:
#   CM_ALLOW_DEBUG_SIGNING=1 bash scripts/build-aab.sh build/CatMetro-debug-proof.aab
# That new bundle must end in -debug-proof.aab and is a pipeline proof ONLY. It cannot be uploaded.
# A debug proof refuses release-looking or pre-existing output paths and marks both its log and
# listing sidecar.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
AAB_TEST_MODE="${CM_AAB_TEST_MODE:-0}"
case "$AAB_TEST_MODE" in
  0|1) ;;
  *) echo "FAIL: CM_AAB_TEST_MODE must be 0 or 1."; exit 1 ;;
esac
if [ "$AAB_TEST_MODE" != "1" ]; then
  for override_name in \
    CM_UNITY_BIN CM_BUNDLETOOL_BIN CM_BUNDLETOOL_JAR CM_JAVA_BIN CM_JARSIGNER_BIN
  do
    case "$override_name" in
      CM_UNITY_BIN) override_is_set="${CM_UNITY_BIN+x}" ;;
      CM_BUNDLETOOL_BIN) override_is_set="${CM_BUNDLETOOL_BIN+x}" ;;
      CM_BUNDLETOOL_JAR) override_is_set="${CM_BUNDLETOOL_JAR+x}" ;;
      CM_JAVA_BIN) override_is_set="${CM_JAVA_BIN+x}" ;;
      CM_JARSIGNER_BIN) override_is_set="${CM_JARSIGNER_BIN+x}" ;;
    esac
    if [ "$override_is_set" = "x" ]; then
      echo "FAIL: $override_name is a test seam; production builds use the pinned Unity installation."
      exit 1
    fi
  done
fi
UNITY_VERSION="$(grep -oE '^m_EditorVersion: .*' "$ROOT/unity/ProjectSettings/ProjectVersion.txt" 2>/dev/null | awk '{print $2}')"
UNITY="${CM_UNITY_BIN:-/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity}"
OUT="${1:-$ROOT/build/CatMetro-release.aab}"

[ -n "$UNITY_VERSION" ] || { echo "FAIL: cannot read Unity version from ProjectVersion.txt"; exit 1; }
[ -x "$UNITY" ] || { echo "FAIL: Unity $UNITY_VERSION not installed at $UNITY"; exit 1; }

UNITY_EDITOR_ROOT="$(cd "$(dirname "$UNITY")/../../.." 2>/dev/null && pwd)" || UNITY_EDITOR_ROOT=""
if [ -n "${CM_BUNDLETOOL_BIN:-}" ]; then
  [ -x "$CM_BUNDLETOOL_BIN" ] \
    || { echo "FAIL: CM_BUNDLETOOL_BIN is not executable: $CM_BUNDLETOOL_BIN"; exit 1; }
  bundletool_command=("$CM_BUNDLETOOL_BIN")
else
  bundletool_jar="${CM_BUNDLETOOL_JAR:-}"
  if [ -z "$bundletool_jar" ]; then
    for candidate in "$UNITY_EDITOR_ROOT"/PlaybackEngines/AndroidPlayer/Tools/bundletool-all-*.jar; do
      if [ -f "$candidate" ]; then
        bundletool_jar="$candidate"
        break
      fi
    done
  fi
  java_bin="${CM_JAVA_BIN:-$UNITY_EDITOR_ROOT/PlaybackEngines/AndroidPlayer/OpenJDK/bin/java}"
  [ -f "$bundletool_jar" ] \
    || { echo "FAIL: bundletool JAR not found; install Android Build Support for the pinned Unity version (tool overrides are test-mode only)."; exit 1; }
  [ -x "$java_bin" ] \
    || { echo "FAIL: Java for bundletool is not executable: $java_bin"; exit 1; }
  bundletool_command=("$java_bin" -jar "$bundletool_jar")
fi
jarsigner_bin="${CM_JARSIGNER_BIN:-$UNITY_EDITOR_ROOT/PlaybackEngines/AndroidPlayer/OpenJDK/bin/jarsigner}"
[ -x "$jarsigner_bin" ] \
  || { echo "FAIL: jarsigner is not executable: $jarsigner_bin"; exit 1; }

case "$OUT" in
  *.aab) ;;
  *) echo "FAIL: output must end in .aab (got: $OUT)"; exit 1 ;;
esac
if [ "$AAB_TEST_MODE" = "1" ]; then
  test_output_name="$(basename "$OUT")"
  case "$test_output_name" in
    *[Rr][Ee][Ll][Ee][Aa][Ss][Ee]*)
      echo "FAIL: test-mode output cannot use a release-looking name: $test_output_name"
      exit 1
      ;;
  esac
  case "$test_output_name" in
    *-test-proof.aab) ;;
    *)
      echo "FAIL: test-mode output must end in -test-proof.aab (got: $test_output_name)"
      exit 1
      ;;
  esac
fi

PROJECT_SETTINGS="$ROOT/unity/ProjectSettings/ProjectSettings.asset"
setting_value() {
  awk -v key="$1:" '$1 == key { print $2; exit }' "$PROJECT_SETTINGS"
}
nested_android_value() {
  awk -v section="$1" '
    $0 == "  " section ":" { found = 1; next }
    found && $1 == "Android:" { print $2; exit }
    found && /^  [^ ]/ { exit }
  ' "$PROJECT_SETTINGS"
}
target_api="$(setting_value AndroidTargetSdkVersion)"
case "$target_api" in
  ''|*[!0-9]*) echo "FAIL: Android target API is missing or invalid: $target_api"; exit 1 ;;
esac
if [ "$target_api" -lt 36 ]; then
  echo "FAIL: Google Play release builds must target API 36 or newer (got $target_api)."
  exit 1
fi
min_api="$(setting_value AndroidMinSdkVersion)"
case "$min_api" in
  ''|*[!0-9]*) echo "FAIL: Android minimum API is missing or invalid: $min_api"; exit 1 ;;
esac
if [ "$min_api" -gt "$target_api" ]; then
  echo "FAIL: Android minimum API $min_api exceeds target API $target_api."
  exit 1
fi
application_id="$(nested_android_value applicationIdentifier)"
if [ "$application_id" != "com.catmetro.game" ]; then
  echo "FAIL: permanent Android application ID must be com.catmetro.game (got $application_id)."
  exit 1
fi
target_architectures="$(setting_value AndroidTargetArchitectures)"
if [ "$target_architectures" != "2" ]; then
  echo "FAIL: Android release must be ARM64-only (target-architecture mask 2; got $target_architectures)."
  exit 1
fi
scripting_backend="$(nested_android_value scriptingBackend)"
if [ "$scripting_backend" != "1" ]; then
  echo "FAIL: Android release must use IL2CPP (scripting-backend value 1; got $scripting_backend)."
  exit 1
fi
force_sd_permission="$(setting_value ForceSDCardPermission)"
if [ "$force_sd_permission" != "0" ]; then
  echo "FAIL: Android release must not force the external-storage permission (got $force_sd_permission)."
  exit 1
fi
force_internet_permission="$(setting_value ForceInternetPermission)"
if [ "$force_internet_permission" != "0" ]; then
  echo "FAIL: Android release must not force the internet permission (got $force_internet_permission)."
  exit 1
fi
version_code="$(setting_value AndroidBundleVersionCode)"
case "$version_code" in
  ''|*[!0-9]*) echo "FAIL: Android version code is missing or invalid: $version_code"; exit 1 ;;
esac
if [ "$version_code" -lt 1 ]; then
  echo "FAIL: Android version code must be positive (got $version_code)."
  exit 1
fi
if [ "$version_code" -gt 2100000000 ]; then
  echo "FAIL: Android version code exceeds Google Play's 2100000000 maximum (got $version_code)."
  exit 1
fi
bundle_version="$(setting_value bundleVersion)"
if [[ ! "$bundle_version" =~ ^[1-9][0-9]*\.[0-9]+\.[0-9]+$ ]]; then
  echo "FAIL: public release version must be semantic and 1.0.0 or newer (got $bundle_version)."
  exit 1
fi

# Release bundle: refuse to inherit a dev-build flag from the caller's environment. The
# C# entry point refuses CM_DEV_BUILD=1 too; unsetting it here makes the failure impossible
# rather than merely reported.
unset CM_DEV_BUILD

mkdir -p "$(dirname "$OUT")"
OUT_DIR="$(cd "$(dirname "$OUT")" && pwd)"
OUT="$OUT_DIR/$(basename "$OUT")"
LISTING_TEMPLATE="$ROOT/docs/store/play-store-listing.md"
LISTING_OUT="${OUT%.aab}-play-listing.md"
LOG_OUT="${OUT%.aab}-unity-build.log"
FAILED_LOG_OUT="${OUT%.aab}-failed-release-build.log"
path_occupied() {
  [ -e "$1" ] || [ -L "$1" ]
}
for destination in "$OUT" "$LISTING_OUT" "$LOG_OUT" "$FAILED_LOG_OUT"; do
  if [ -L "$destination" ]; then
    echo "FAIL: release output path is a symbolic link: $destination"
    exit 1
  fi
  if [ -d "$destination" ]; then
    echo "FAIL: release output path is a directory: $destination"
    exit 1
  fi
done
if path_occupied "$OUT" || path_occupied "$LISTING_OUT"; then
  echo "FAIL: release outputs are immutable; choose a new AAB path or move the existing pair aside."
  echo "  AAB: $OUT"
  echo "  Listing: $LISTING_OUT"
  exit 1
fi
[ -f "$LISTING_TEMPLATE" ] || {
  echo "FAIL: listing template is missing: $LISTING_TEMPLATE"
  exit 1
}
grep -qF '__CAMPAIGN_LEVEL_COUNT__' "$LISTING_TEMPLATE" || {
  echo "FAIL: campaign-count token is missing from the listing template."
  exit 1
}

LOCK_DIR="${OUT}.lock"
BUILD_TMP=""
LOG=""
lock_held=0
publication_started=0
publication_committed=0
published_aab_owned=0
published_listing_owned=0
# Invoked indirectly by the EXIT trap below.
# shellcheck disable=SC2329
cleanup() {
  local exit_code=$?
  trap - EXIT
  set +e
  if [ "$publication_started" -eq 1 ] && [ "$publication_committed" -eq 0 ]; then
    if [ "$published_aab_owned" -eq 1 ] && [ ! -L "$OUT" ] && [ "$OUT" -ef "$TMP_OUT" ]; then
      rm -f -- "$OUT"
    fi
    if [ "$published_listing_owned" -eq 1 ] \
      && [ ! -L "$LISTING_OUT" ] && [ "$LISTING_OUT" -ef "$TMP_LISTING" ]
    then
      rm -f -- "$LISTING_OUT"
    fi
    echo "Publish rollback: removed the incomplete new AAB/listing pair." >&2
  fi
  if [ -n "$LOG" ] && [ -f "$LOG" ]; then
    if [ "$exit_code" -eq 0 ]; then
      cp -f -- "$LOG" "$LOG_OUT"
    else
      cp -f -- "$LOG" "$FAILED_LOG_OUT"
    fi
  fi
  if [ -n "$BUILD_TMP" ]; then
    rm -rf -- "$BUILD_TMP"
  fi
  if [ "$lock_held" -eq 1 ]; then
    rmdir -- "$LOCK_DIR" 2>/dev/null
  fi
  exit "$exit_code"
}
trap cleanup EXIT
trap 'exit 129' HUP
trap 'exit 130' INT
trap 'exit 143' TERM
if ! mkdir "$LOCK_DIR" 2>/dev/null; then
  echo "FAIL: another build owns $LOCK_DIR; verify no build is running before removing a stale lock."
  exit 1
fi
lock_held=1
# Close the race between the early path check and lock acquisition. This script never replaces a
# release candidate; use a versioned output path for every attempt.
if path_occupied "$OUT" || path_occupied "$LISTING_OUT"; then
  echo "FAIL: release output appeared before lock acquisition; choose a new AAB path."
  exit 1
fi
BUILD_TMP="$(mktemp -d "$OUT_DIR/.catmetro-aab.XXXXXX")"
TMP_OUT="$BUILD_TMP/CatMetro-building.aab"
TMP_LISTING="$BUILD_TMP/CatMetro-play-listing.md"
LOG="$BUILD_TMP/unity-aab-build.log"

echo "Unity   : $UNITY_VERSION"
echo "AAB out : $OUT"
echo "Live log: $LOG"
echo "Success log: $LOG_OUT"
echo "Failure log: $FAILED_LOG_OUT"
echo "Version : $(grep -oE '^  bundleVersion: .*' "$ROOT/unity/ProjectSettings/ProjectSettings.asset" | awk '{print $2}') (versionCode $(grep -oE '^  AndroidBundleVersionCode: .*' "$ROOT/unity/ProjectSettings/ProjectSettings.asset" | awk '{print $2}'))"
echo "A cold IL2CPP/ARM64 release build takes 25-45 minutes. Follow it with: tail -f $LOG"
echo

# No -quit: CatMetroCliAabBuild exits with the real build result itself. Passing -quit here
# would exit before the build finished — the same trap that bites -runTests.
: > "$LOG"
if CM_AAB_OUT="$TMP_OUT" \
  "$UNITY" -batchmode -nographics \
    -projectPath "$ROOT/unity" \
    -buildTarget Android \
    -executeMethod CatMetroCliAabBuild.BuildAndroidAab \
    -logFile "$LOG"
then
  rc=0
else
  rc=$?
fi

echo
echo "Unity exit: $rc"
grep -E "CLI_AAB_RESULT" "$LOG" 2>/dev/null | tail -5 || true
grep -E "error CS|BuildFailedException" "$LOG" 2>/dev/null | head -10 || true

if [ "$rc" -ne 0 ]; then
  echo
  echo "Unity failed; the previous AAB, if any, was left untouched."
  echo "This invocation's log will be saved to $FAILED_LOG_OUT."
  exit "$rc"
fi

if [ ! -f "$TMP_OUT" ]; then
  echo
  echo "No AAB produced — see $LOG"
  exit 1
fi

# Verify the artifact is REALLY a bundle, not an APK wearing an .aab name.
# EditorUserBuildSettings.buildAppBundle persists in unity/Library, and a mismatch between
# the flag and the filename is silent at build time and only surfaces as a Play upload
# rejection — the slowest possible place to learn about it. An AAB has a BundleConfig.pb and
# a base/ module; an APK has classes.dex and a root AndroidManifest.xml.
echo
if ! command -v unzip >/dev/null 2>&1; then
  echo "Bundle shape: FAIL — unzip is required for mandatory AAB validation."
  exit 1
else
  bundle_entries="$(unzip -Z1 "$TMP_OUT" 2>/dev/null)" || {
    echo "Bundle shape: FAIL — unreadable or corrupt zip archive."
    exit 1
  }
  if grep -qx 'classes.dex' <<<"$bundle_entries"; then
    echo "Bundle shape: FAIL — this file is an APK named .aab."
    echo "  EditorUserBuildSettings.buildAppBundle was not applied. Close the Unity editor"
    echo "  (it can rewrite Library state on exit) and re-run this script."
    exit 1
  fi
  for required_entry in \
    BundleConfig.pb \
    base/manifest/AndroidManifest.xml \
    base/dex/classes.dex \
    base/lib/arm64-v8a/libil2cpp.so
  do
    if ! grep -qxF "$required_entry" <<<"$bundle_entries"; then
      echo "Bundle shape: FAIL — required AAB entry is missing: $required_entry"
      exit 1
    fi
  done
  native_abis="$(awk -F/ '$2 == "lib" && NF >= 4 { print $3 }' <<<"$bundle_entries" | sort -u)"
  while IFS= read -r native_abi; do
    [ -z "$native_abi" ] || [ "$native_abi" = "arm64-v8a" ] || {
      echo "Bundle shape: FAIL — native payload contains an ABI outside ARM64: $native_abi"
      exit 1
    }
  done <<<"$native_abis"
  unzip -tqq "$TMP_OUT" || {
    echo "Bundle shape: FAIL — archive CRC validation failed."
    exit 1
  }
  echo "Bundle shape: OK (config, base manifest, dex, and ARM64 IL2CPP payload present)"
fi

bundletool_log="$BUILD_TMP/bundletool-validate.log"
if ! "${bundletool_command[@]}" validate --bundle="$TMP_OUT" > "$bundletool_log" 2>&1; then
  echo "Bundle validation: FAIL — bundletool rejected the generated artifact."
  tail -20 "$bundletool_log" || true
  exit 1
fi
echo "Bundle validation: OK (bundletool accepted the AAB)"

manifest_modules=()
seen_manifest_modules=","
while IFS= read -r bundle_entry; do
  case "$bundle_entry" in
    */manifest/AndroidManifest.xml)
      manifest_module="${bundle_entry%%/*}"
      [[ "$manifest_module" =~ ^[A-Za-z0-9_.-]+$ ]] || {
        echo "Built manifest: FAIL — malformed module name: $manifest_module"
        exit 1
      }
      if [[ "$seen_manifest_modules" != *",$manifest_module,"* ]]; then
        manifest_modules+=("$manifest_module")
        seen_manifest_modules="${seen_manifest_modules}${manifest_module},"
      fi
      ;;
  esac
done <<<"$bundle_entries"
[[ "$seen_manifest_modules" == *",base,"* ]] || {
  echo "Built manifest: FAIL — bundle has no base manifest module."
  exit 1
}
all_manifests="$BUILD_TMP/all-manifests.xml"
: > "$all_manifests"
manifest_dump="$BUILD_TMP/base-manifest.xml"
for manifest_module in "${manifest_modules[@]}"; do
  module_manifest="$BUILD_TMP/manifest-$manifest_module.xml"
  if ! "${bundletool_command[@]}" dump manifest \
    --bundle="$TMP_OUT" --module="$manifest_module" > "$module_manifest" 2>&1
  then
    echo "Built manifest: FAIL — bundletool could not dump module $manifest_module."
    exit 1
  fi
  printf '<!-- module: %s -->\n' "$manifest_module" >> "$all_manifests"
  sed -n '1,$p' "$module_manifest" >> "$all_manifests"
  if [ "$manifest_module" = "base" ]; then
    cp -- "$module_manifest" "$manifest_dump"
  fi
done
grep -qF 'package="com.catmetro.game"' "$manifest_dump" \
  || { echo "Built manifest: FAIL — package ID is not com.catmetro.game."; exit 1; }
grep -qF "android:versionCode=\"$version_code\"" "$manifest_dump" \
  || { echo "Built manifest: FAIL — version code does not match tracked settings."; exit 1; }
grep -qF "android:versionName=\"$bundle_version\"" "$manifest_dump" \
  || { echo "Built manifest: FAIL — version name does not match tracked settings."; exit 1; }
grep -qF "android:minSdkVersion=\"$min_api\"" "$manifest_dump" \
  || { echo "Built manifest: FAIL — minimum SDK does not match tracked settings."; exit 1; }
grep -qF "android:targetSdkVersion=\"$target_api\"" "$manifest_dump" \
  || { echo "Built manifest: FAIL — target SDK does not match tracked settings."; exit 1; }
grep -qF 'android:allowBackup="false"' "$manifest_dump" \
  || { echo "Built manifest: FAIL — application backup is not explicitly disabled."; exit 1; }
if grep -Eq 'android:debuggable="true"' "$all_manifests"; then
  echo "Built manifest: FAIL — release application is debuggable."
  exit 1
fi
permission_list="$(perl -0ne '
  while (/<uses-permission(?:-sdk-[0-9]+)?\b[^>]*\bandroid:name="([^"]+)"/g) {
    print "$1\n";
  }
' "$all_manifests" | sort -u)"
while IFS= read -r permission_name; do
  [ -z "$permission_name" ] && continue
  case "$permission_name" in
    android.permission.INTERNET|android.permission.ACCESS_NETWORK_STATE|com.android.vending.BILLING) ;;
    *)
      echo "Built manifest: FAIL — permission is outside Cat Metro's release allowlist: $permission_name"
      exit 1
      ;;
  esac
done <<<"$permission_list"
echo "Built manifest: OK (all modules; identity, versions, SDKs, backup, debug, and permission allowlist)"
echo "Built permissions:"
if [ -z "$permission_list" ]; then
  echo "  (none declared)"
else
  while IFS= read -r permission_name; do
    echo "  $permission_name"
  done <<<"$permission_list"
fi

signature_log="$BUILD_TMP/jarsigner-verify.log"
set +e
LC_ALL=C "$jarsigner_bin" -verify -strict -verbose "$TMP_OUT" > "$signature_log" 2>&1
signature_rc=$?
set -e
# jarsigner's strict exit code is a bit mask. Bit 16 proves one or more unsigned entries. Bit 4 is
# shared by several certificate errors, so it is accepted only when the English/C-locale Error block
# contains exactly the two expected diagnostics for a self-signed upload certificate with no public
# trust chain. The human fingerprint comparison still proves which upload certificate signed it.
if (( (signature_rc & 16) != 0 )); then
  echo "Signature: FAIL — the AAB contains entries not covered by its JAR signature."
  tail -20 "$signature_log" || true
  exit 1
fi
if (( (signature_rc & ~4) != 0 )); then
  echo "Signature: FAIL — jarsigner rejected the AAB (strict status $signature_rc)."
  tail -20 "$signature_log" || true
  exit 1
fi
strict_errors="$(awk '
  /^Error:[[:space:]]*$/ { in_error = 1; next }
  in_error && /^Warning:[[:space:]]*$/ { exit }
  in_error && NF { print }
' "$signature_log")"
if [ "$signature_rc" -eq 4 ]; then
  saw_invalid_chain=0
  saw_self_signed=0
  while IFS= read -r strict_error; do
    case "$strict_error" in
      'This jar contains entries whose certificate chain is invalid. Reason: '*)
        saw_invalid_chain=1
        ;;
      'This jar contains entries whose signer certificate is self-signed.')
        saw_self_signed=1
        ;;
      *)
        echo "Signature: FAIL — unexpected strict signer error: $strict_error"
        exit 1
        ;;
    esac
  done <<<"$strict_errors"
  if [ "$saw_invalid_chain" -ne 1 ] || [ "$saw_self_signed" -ne 1 ]; then
    echo "Signature: FAIL — strict status 4 was not solely the expected self-signed chain state."
    exit 1
  fi
elif [ -n "$strict_errors" ]; then
  echo "Signature: FAIL — jarsigner reported errors despite a zero strict status."
  exit 1
fi
if grep -Eqi 'has expired|not yet valid|will expire within six months|disabled|weak algorithm|security risk' "$signature_log"; then
  echo "Signature: FAIL — signer validity or algorithm warning is unacceptable for a release AAB."
  exit 1
fi
if ! grep -Eq '^jar verified(, with signer errors)?\.?$' "$signature_log"; then
  echo "Signature: FAIL — the AAB does not carry a verified JAR signature."
  exit 1
fi
if grep -Eqi 'unsigned entries|jar is unsigned' "$signature_log"; then
  echo "Signature: FAIL — jarsigner reported unsigned archive entries."
  exit 1
fi
echo "Signature: OK (all archive entries verify; fingerprint comparison remains human-only)"

receipt_marker_count="$(grep -cF 'CLI_AAB_RESULT' "$LOG" 2>/dev/null || true)"
if [ "$receipt_marker_count" != "1" ]; then
  echo "CLI_AAB_RESULT receipt is missing or ambiguous (found $receipt_marker_count markers); refusing the artifact."
  exit 1
fi
result_line="$(grep -F 'CLI_AAB_RESULT' "$LOG")"
canonical_receipt_pattern='^CLI_AAB_RESULT Succeeded signing=(custom|debug) campaignLevels=[0-9]+ campaignIds=L[0-9]{3}(,L[0-9]{3})* size=[0-9]+ errors=0 out=.+\.aab$'
if ! grep -Eq "$canonical_receipt_pattern" <<<"$result_line"; then
  echo "CLI_AAB_RESULT receipt is not one canonical full-line success record; refusing the artifact."
  exit 1
fi
case "$result_line" in
  *" out=$TMP_OUT") ;;
  *) echo "CLI_AAB_RESULT output path does not identify this invocation's staged AAB."; exit 1 ;;
esac
signing="$(grep -oE 'signing=(custom|debug)' <<<"$result_line" | cut -d= -f2)"
if [ "$signing" = "debug" ] && [ "${CM_ALLOW_DEBUG_SIGNING:-0}" != "1" ]; then
  echo "Signing: FAIL — debug signing was not explicitly approved for a pipeline proof."
  exit 1
fi
if [ "$signing" = "debug" ]; then
  debug_output_name="$(basename "$OUT")"
  case "$debug_output_name" in
    *[Rr][Ee][Ll][Ee][Aa][Ss][Ee]*)
      echo "Signing: FAIL — a debug proof cannot use a release-looking output name."
      exit 1
      ;;
  esac
  if [ "$AAB_TEST_MODE" = "1" ]; then
    debug_name_suffix='-debug-proof-test-proof.aab'
  else
    debug_name_suffix='-debug-proof.aab'
  fi
  case "$AAB_TEST_MODE:$debug_output_name" in
    1:*-debug-proof-test-proof.aab|0:*-debug-proof.aab) ;;
    *)
      echo "Signing: FAIL — a debug proof must use the mandatory $debug_name_suffix suffix."
      exit 1
      ;;
  esac
  if path_occupied "$OUT" || path_occupied "$LISTING_OUT"; then
    echo "Signing: FAIL — a debug proof refuses to overwrite an existing AAB or listing."
    exit 1
  fi
fi

campaign_count="$(sed -nE 's/.* campaignLevels=([0-9]+) .*/\1/p' <<<"$result_line")"
campaign_ids="$(sed -nE 's/.* campaignIds=([^ ]+) .*/\1/p' <<<"$result_line")"
case "$campaign_count" in
  ''|*[!0-9]*) echo "Campaign receipt: FAIL — missing campaignLevels marker."; exit 1 ;;
esac
[ -n "$campaign_ids" ] || {
  echo "Campaign receipt: FAIL — missing campaignIds marker."
  exit 1
}
IFS=',' read -r -a campaign_id_list <<<"$campaign_ids"
if [ "${#campaign_id_list[@]}" -ne "$campaign_count" ]; then
  echo "Campaign receipt: FAIL — count $campaign_count does not match ID list $campaign_ids."
  exit 1
fi
seen_campaign_ids=","
for campaign_id in "${campaign_id_list[@]}"; do
  if [[ ! "$campaign_id" =~ ^L[0-9]{3}$ ]]; then
    echo "Campaign receipt: FAIL — malformed campaign ID: $campaign_id"
    exit 1
  fi
  if [[ "$seen_campaign_ids" == *",$campaign_id,"* ]]; then
    echo "Campaign receipt: FAIL — duplicate campaign ID: $campaign_id"
    exit 1
  fi
  seen_campaign_ids="${seen_campaign_ids}${campaign_id},"
  artifact_level="base/assets/bin/Data/StreamingAssets/content/levels/$campaign_id.json"
  if ! grep -qxF "$artifact_level" <<<"$bundle_entries"; then
    echo "Campaign receipt: FAIL — AAB does not contain reachable level $campaign_id."
    exit 1
  fi
  source_level="$ROOT/unity/Assets/StreamingAssets/content/levels/$campaign_id.json"
  if [ ! -f "$source_level" ]; then
    echo "Campaign receipt: FAIL — staged source level disappeared: $source_level"
    exit 1
  fi
  source_level_sha="$(shasum -a 256 "$source_level" | awk '{print $1}')"
  artifact_level_sha="$(unzip -p "$TMP_OUT" "$artifact_level" | shasum -a 256 | awk '{print $1}')" || {
    echo "Campaign receipt: FAIL — could not hash $artifact_level from the AAB."
    exit 1
  }
  if [ "$artifact_level_sha" != "$source_level_sha" ]; then
    echo "Campaign receipt: FAIL — AAB bytes differ from staged source for $campaign_id."
    exit 1
  fi
done
echo "Campaign receipt: OK ($campaign_count reachable levels match staged source in the exact AAB)"

aab_sha="$(shasum -a 256 "$TMP_OUT" | awk '{print $1}')"
{
  echo '<!-- Generated by scripts/build-aab.sh; do not edit this file. -->'
  echo '> **COUNT-BOUND LISTING CANDIDATE.** Before pasting, clear every release-gated claim against this exact AAB.'
  echo
  if [ "$AAB_TEST_MODE" = "1" ]; then
    echo '> **TEST MODE PIPELINE PROOF — NOT UPLOADABLE.** Validator and Unity test seams may have been used.'
    echo
  fi
  if [ "$signing" = "debug" ]; then
    echo '> **DEBUG-SIGNED PIPELINE PROOF — NOT UPLOADABLE.** Do not use this listing for a store submission.'
    echo
  fi
  echo "<!-- Exact AAB SHA-256: $aab_sha -->"
  echo "<!-- Campaign levels in exact AAB: $campaign_count -->"
  echo "<!-- Campaign IDs in exact AAB: $campaign_ids -->"
  echo
  sed "s/__CAMPAIGN_LEVEL_COUNT__/$campaign_count/g" "$LISTING_TEMPLATE"
} > "$TMP_LISTING"
if grep -qF '__CAMPAIGN_LEVEL_COUNT__' "$TMP_LISTING"; then
  echo "Listing render: FAIL — an unresolved campaign-count token remains."
  exit 1
fi
listing_field_count() {
  awk -v heading="$1" '
    $0 == "### " heading { found_heading = 1; next }
    found_heading && $0 == "```text" { capture = 1; next }
    capture && $0 == "```" {
      if (count > 0) count--
      print count
      emitted = 1
      exit
    }
    capture { count += length($0) + 1 }
    END { if (!emitted) print "" }
  ' "$TMP_LISTING"
}
title_count="$(listing_field_count 'App title')"
short_count="$(listing_field_count 'Short description')"
full_count="$(listing_field_count 'Full description')"
whats_new_count="$(listing_field_count "What's new")"
for field_spec in \
  "title:$title_count:30" \
  "short:$short_count:80" \
  "full:$full_count:4000" \
  "what's-new:$whats_new_count:500"
do
  IFS=: read -r field_name field_count field_limit <<<"$field_spec"
  case "$field_count" in
    ''|*[!0-9]*) echo "Listing render: FAIL — could not count $field_name."; exit 1 ;;
  esac
  if [ "$field_count" -gt "$field_limit" ]; then
    echo "Listing render: FAIL — $field_name is $field_count/$field_limit characters."
    exit 1
  fi
done
echo "Listing fields: OK (title $title_count/30, short $short_count/80, full $full_count/4000, what's-new $whats_new_count/500)"

# Outputs are immutable and were checked before the build. Recheck immediately before publication,
# then create hard links from the same-filesystem staging files. Link creation is an atomic
# no-replace operation: an external writer wins with EEXIST rather than being overwritten. The
# EXIT/signal trap removes only paths still linked to files created by this invocation.
if path_occupied "$OUT" || path_occupied "$LISTING_OUT"; then
  echo "Publish: FAIL — immutable output path appeared while the build was running."
  exit 1
fi
aab_bytes="$(wc -c < "$TMP_OUT" | tr -d '[:space:]')"
publication_started=1
published_listing_owned=1
if ! ln -- "$TMP_LISTING" "$LISTING_OUT"; then
  published_listing_owned=0
  echo "Publish: FAIL — could not atomically reserve the listing output."
  exit 1
fi
published_aab_owned=1
if ! ln -- "$TMP_OUT" "$OUT"; then
  published_aab_owned=0
  echo "Publish: FAIL — could not atomically reserve the AAB output."
  exit 1
fi
[ -f "$LISTING_OUT" ] || { echo "Publish: FAIL — listing sidecar is not a regular file."; exit 1; }
[ -f "$OUT" ] || { echo "Publish: FAIL — AAB is not a regular file."; exit 1; }
published_sha="$(shasum -a 256 "$OUT" | awk '{print $1}')"
[ "$published_sha" = "$aab_sha" ] \
  || { echo "Publish: FAIL — final AAB hash differs from its listing receipt."; exit 1; }
grep -qF "Exact AAB SHA-256: $aab_sha" "$LISTING_OUT" \
  || { echo "Publish: FAIL — final listing is not bound to the final AAB."; exit 1; }
publication_committed=1
set +e
echo
echo "AAB: $OUT ($aab_bytes bytes)"
echo "sha256: $aab_sha"
echo "Listing candidate: $LISTING_OUT"
echo
if [ "$AAB_TEST_MODE" = "1" ]; then
  echo "TEST MODE PIPELINE PROOF — NOT UPLOADABLE."
  echo "  Unity and validator override seams may have been used; rebuild with the default command."
elif [ "$signing" = "custom" ]; then
  echo "Unity reported custom signing and the AAB signature verifies."
  echo "Verify its certificate fingerprint against Play Console before upload."
  echo
  echo "NEXT STEP IS HUMAN-ONLY — an agent must never run a Play upload:"
  echo "  Play Console > Testing > Closed testing > your track > Create new release"
  echo "  > Upload $OUT"
else
  echo "DEBUG-SIGNED PIPELINE PROOF — NOT UPLOADABLE."
  echo "  Play rejects debug-signed bundles. Configure the upload keystore in"
  echo "  Project Settings > Player > Publishing Settings, then rebuild."
fi
exit 0
