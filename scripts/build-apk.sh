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
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY_VERSION="$(grep -oE '^m_EditorVersion: .*' "$ROOT/unity/ProjectSettings/ProjectVersion.txt" 2>/dev/null | awk '{print $2}')"
UNITY="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity"
OUT="${1:-$ROOT/build/CatMetro-dev.apk}"
LOG="$ROOT/build/unity-build.log"

[ -n "$UNITY_VERSION" ] || { echo "FAIL: cannot read Unity version from ProjectVersion.txt"; exit 1; }
[ -x "$UNITY" ] || { echo "FAIL: Unity $UNITY_VERSION not installed at $UNITY"; exit 1; }

mkdir -p "$(dirname "$OUT")"
echo "Unity   : $UNITY_VERSION"
echo "APK out : $OUT"
echo "Log     : $LOG"
echo "A cold IL2CPP/ARM64 build takes 25-45 minutes. Follow it with: tail -f $LOG"
echo

# No -quit: CatMetroCliBuild exits with the real build result itself. Passing -quit here would
# exit before the build finished — the same trap that bites -runTests.
CM_APK_OUT="$OUT" CM_DEV_BUILD=1 \
"$UNITY" -batchmode -nographics \
  -projectPath "$ROOT/unity" \
  -executeMethod CatMetroCliBuild.BuildAndroid \
  -logFile "$LOG"
rc=$?

echo
echo "Unity exit: $rc"
grep -E "CLI_BUILD_RESULT|CATWIRE" "$LOG" 2>/dev/null | tail -10
grep -E "error CS|BuildFailedException" "$LOG" 2>/dev/null | head -10

if [ -f "$OUT" ]; then
  echo
  echo "APK: $OUT  ($(ls -lh "$OUT" | awk '{print $5}'))"
  echo "sha256: $(shasum -a 256 "$OUT" | awk '{print $1}')"
  echo
  echo "Install on the Pixel 9 Pro (verify the serial first with 'adb devices -l'):"
  echo "  ~/Library/Android/sdk/platform-tools/adb -s 48121FDAP006X4 install -r \"$OUT\""
else
  echo
  echo "No APK produced — see $LOG"
fi
