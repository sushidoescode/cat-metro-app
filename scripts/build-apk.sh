#!/usr/bin/env bash
# Build a development APK from this checkout.
#
# Run this yourself — Unity cannot run inside the agent sandbox: a cold Library resolves
# packages over the network and the editor writes to ~/Library/Unity, both outside the
# sandbox's allowlist.
#
#   bash scripts/build-apk.sh [output.apk]
#
# The APK is ARM64, development, debug-signed — sideloadable, never a Play upload.
# Google Play accepts only .aab for new apps: for a store upload use scripts/build-aab.sh.
# The builder forces EditorUserBuildSettings.buildAppBundle=false, because that flag persists
# in unity/Library and an inherited `true` silently emits a bundle named .apk.
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APK_TEST_MODE="${CM_APK_TEST_MODE:-0}"
case "$APK_TEST_MODE" in
  0|1) ;;
  *) echo "FAIL: CM_APK_TEST_MODE must be 0 or 1."; exit 1 ;;
esac
if [ "$APK_TEST_MODE" != "1" ] && [ "${CM_UNITY_BIN+x}" = "x" ]; then
  echo "FAIL: CM_UNITY_BIN is a test seam; production builds use the pinned Unity installation."
  exit 1
fi
UNITY_VERSION="$(grep -oE '^m_EditorVersion: .*' "$ROOT/unity/ProjectSettings/ProjectVersion.txt" 2>/dev/null | awk '{print $2}')"
UNITY="${CM_UNITY_BIN:-/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity}"
OUT="${1:-$ROOT/build/CatMetro-dev.apk}"
LOG="$ROOT/build/unity-build.log"

[ -n "$UNITY_VERSION" ] || { echo "FAIL: cannot read Unity version from ProjectVersion.txt"; exit 1; }
[ -x "$UNITY" ] || { echo "FAIL: Unity $UNITY_VERSION not installed at $UNITY"; exit 1; }

case "$OUT" in
  *.apk) ;;
  *) echo "FAIL: output must end in .apk (got: $OUT)"; exit 1 ;;
esac
if [ -L "$OUT" ] || [ -d "$OUT" ]; then
  echo "FAIL: APK output must be a regular path, not a symbolic link or directory: $OUT"
  exit 1
fi
mkdir -p "$(dirname "$OUT")" "$(dirname "$LOG")"
OUT_DIR="$(cd "$(dirname "$OUT")" && pwd)"
OUT="$OUT_DIR/$(basename "$OUT")"
BUILD_TMP="$(mktemp -d "$OUT_DIR/.catmetro-apk.XXXXXX")"
TMP_OUT="$BUILD_TMP/CatMetro-dev-building.apk"
cleanup() { rm -rf -- "$BUILD_TMP"; }
trap cleanup EXIT
trap 'exit 129' HUP
trap 'exit 130' INT
trap 'exit 143' TERM
echo "Unity   : $UNITY_VERSION"
echo "APK out : $OUT"
echo "Log     : $LOG"
echo "A cold IL2CPP/ARM64 build takes 25-45 minutes. Follow it with: tail -f $LOG"
echo

# No -quit: CatMetroCliBuild exits with the real build result itself. Passing -quit here would
# exit before the build finished — the same trap that bites -runTests.
CM_APK_OUT="$TMP_OUT" CM_DEV_BUILD=1 \
"$UNITY" -batchmode -nographics \
  -projectPath "$ROOT/unity" \
  -executeMethod CatMetroCliBuild.BuildAndroid \
  -logFile "$LOG"
rc=$?

echo
echo "Unity exit: $rc"
grep -E "CLI_BUILD_RESULT|CATWIRE" "$LOG" 2>/dev/null | tail -10
grep -E "error CS|BuildFailedException" "$LOG" 2>/dev/null | head -10

if [ "$rc" -ne 0 ]; then
  echo
  echo "FAIL: Unity did not produce a new APK — see $LOG"
  exit "$rc"
fi
if [ ! -f "$TMP_OUT" ]; then
  echo
  echo "FAIL: Unity exited successfully but this invocation produced no staged APK — see $LOG"
  exit 1
fi
if [ -L "$OUT" ] || [ -d "$OUT" ]; then
  echo "FAIL: APK output path changed to a symbolic link or directory while Unity was running: $OUT"
  exit 1
fi
mv -f -- "$TMP_OUT" "$OUT" || { echo "FAIL: could not publish the freshly built APK: $OUT"; exit 1; }

echo
echo "APK: $OUT  ($(ls -lh "$OUT" | awk '{print $5}'))"
echo "sha256: $(shasum -a 256 "$OUT" | awk '{print $1}')"
echo
echo "Install on the Pixel 9 Pro (verify the serial first with 'adb devices -l'):"
echo "  ~/Library/Android/sdk/platform-tools/adb -s 48121FDAP006X4 install -r \"$OUT\""
