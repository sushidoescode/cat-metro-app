#!/usr/bin/env bash
# Build a RELEASE Android App Bundle (.aab) for a Google Play upload.
#
# Run this yourself — Unity cannot run inside the agent sandbox: a cold Library resolves
# packages over the network and the editor writes to ~/Library/Unity, both outside the
# sandbox's allowlist. This script never uploads anything; uploading to Play is human-only.
#
#   bash scripts/build-aab.sh [output.aab]
#
# Signing: this script never touches keystore material. Configure the upload keystore once
# in Unity (Project Settings > Player > Publishing Settings) — it lives in your local,
# uncommitted editor state and is never readable by an agent. Without it the builder refuses
# to produce a bundle, because a debug-signed .aab is not uploadable to Play.
#
# Escape hatch, for proving the pipeline works before a keystore exists:
#   CM_ALLOW_DEBUG_SIGNING=1 bash scripts/build-aab.sh
# That bundle is a pipeline proof ONLY. It cannot be uploaded. It is marked as such below.
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY_VERSION="$(grep -oE '^m_EditorVersion: .*' "$ROOT/unity/ProjectSettings/ProjectVersion.txt" 2>/dev/null | awk '{print $2}')"
UNITY="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity"
OUT="${1:-$ROOT/build/CatMetro-release.aab}"
LOG="$ROOT/build/unity-aab-build.log"

[ -n "$UNITY_VERSION" ] || { echo "FAIL: cannot read Unity version from ProjectVersion.txt"; exit 1; }
[ -x "$UNITY" ] || { echo "FAIL: Unity $UNITY_VERSION not installed at $UNITY"; exit 1; }

case "$OUT" in
  *.aab) ;;
  *) echo "FAIL: output must end in .aab (got: $OUT)"; exit 1 ;;
esac

# Release bundle: refuse to inherit a dev-build flag from the caller's environment. The
# C# entry point refuses CM_DEV_BUILD=1 too; unsetting it here makes the failure impossible
# rather than merely reported.
unset CM_DEV_BUILD

mkdir -p "$(dirname "$OUT")"
echo "Unity   : $UNITY_VERSION"
echo "AAB out : $OUT"
echo "Log     : $LOG"
echo "Version : $(grep -oE '^  bundleVersion: .*' "$ROOT/unity/ProjectSettings/ProjectSettings.asset" | awk '{print $2}') (versionCode $(grep -oE '^  AndroidBundleVersionCode: .*' "$ROOT/unity/ProjectSettings/ProjectSettings.asset" | awk '{print $2}'))"
echo "A cold IL2CPP/ARM64 release build takes 25-45 minutes. Follow it with: tail -f $LOG"
echo

# No -quit: CatMetroCliAabBuild exits with the real build result itself. Passing -quit here
# would exit before the build finished — the same trap that bites -runTests.
CM_AAB_OUT="$OUT" \
"$UNITY" -batchmode -nographics \
  -projectPath "$ROOT/unity" \
  -executeMethod CatMetroCliAabBuild.BuildAndroidAab \
  -logFile "$LOG"
rc=$?

echo
echo "Unity exit: $rc"
grep -E "CLI_AAB_RESULT" "$LOG" 2>/dev/null | tail -5
grep -E "error CS|BuildFailedException" "$LOG" 2>/dev/null | head -10

if [ ! -f "$OUT" ]; then
  echo
  echo "No AAB produced — see $LOG"
  exit 1
fi

echo
echo "AAB: $OUT  ($(ls -lh "$OUT" | awk '{print $5}'))"
echo "sha256: $(shasum -a 256 "$OUT" | awk '{print $1}')"

# Verify the artifact is REALLY a bundle, not an APK wearing an .aab name.
# EditorUserBuildSettings.buildAppBundle persists in unity/Library, and a mismatch between
# the flag and the filename is silent at build time and only surfaces as a Play upload
# rejection — the slowest possible place to learn about it. An AAB has a BundleConfig.pb and
# a base/ module; an APK has classes.dex and a root AndroidManifest.xml.
echo
if ! command -v unzip >/dev/null 2>&1; then
  echo "NOTE: unzip not found — skipping the bundle-shape check."
elif unzip -l "$OUT" 2>/dev/null | grep -qE 'BundleConfig\.pb'; then
  echo "Bundle shape: OK (BundleConfig.pb present — this is a real .aab)"
elif unzip -l "$OUT" 2>/dev/null | grep -qE 'classes\.dex'; then
  echo "Bundle shape: FAIL — this file is an APK named .aab."
  echo "  EditorUserBuildSettings.buildAppBundle was not applied. Close the Unity editor"
  echo "  (it can rewrite Library state on exit) and re-run this script."
  exit 1
else
  echo "Bundle shape: UNKNOWN — neither BundleConfig.pb nor classes.dex found. Inspect $OUT."
  exit 1
fi

signing="$(grep -oE 'CLI_AAB_RESULT [A-Za-z]+ signing=[a-z]+' "$LOG" 2>/dev/null | tail -1 | grep -oE 'signing=[a-z]+' | cut -d= -f2)"
echo
if [ "$signing" = "custom" ]; then
  echo "Signed with the upload keystore. This bundle is uploadable."
  echo
  echo "NEXT STEP IS HUMAN-ONLY — an agent must never run a Play upload:"
  echo "  Play Console > Testing > Closed testing > your track > Create new release"
  echo "  > Upload $OUT"
else
  echo "DEBUG-SIGNED PIPELINE PROOF — NOT UPLOADABLE."
  echo "  Play rejects debug-signed bundles. Configure the upload keystore in"
  echo "  Project Settings > Player > Publishing Settings, then rebuild."
fi
