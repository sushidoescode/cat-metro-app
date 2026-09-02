#!/usr/bin/env bash
# Generate the Xcode project for an iOS build of this checkout.
#
# Run this yourself — Unity cannot run inside the agent sandbox: a cold Library resolves
# packages over the network and the editor writes to ~/Library/Unity, both outside the
# sandbox's allowlist. Same constraint as scripts/build-apk.sh.
#
#   bash scripts/build-ios.sh [output-directory]
#
# WHAT YOU GET, AND WHAT YOU DO NOT.
# Unity's iOS target emits an Xcode PROJECT, not an app. This script's output is a
# directory you open in Xcode. The archive, the signing and the upload are three further
# steps, they run under your Apple identity, and they are HUMAN-ONLY in this repo — no
# agent runs `xcodebuild archive` or touches App Store Connect. The human steps are printed
# at the end of a successful run, and explained in docs/release/ios-release-runbook.md.
#
# Default is a RELEASE project. CM_DEV_BUILD=1 makes a development project for on-device
# debugging; that one must never be archived for TestFlight or the App Store.
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
IOS_TEST_MODE="${CM_IOS_TEST_MODE:-0}"
case "$IOS_TEST_MODE" in
  0|1) ;;
  *) echo "FAIL: CM_IOS_TEST_MODE must be 0 or 1."; exit 1 ;;
esac
if [ "$IOS_TEST_MODE" != "1" ] \
  && { [ "${CM_UNITY_BIN+x}" = "x" ] || [ "${CM_IOS_MODULE_DIR+x}" = "x" ]; }
then
  echo "FAIL: CM_UNITY_BIN and CM_IOS_MODULE_DIR are test seams; production builds use the pinned Unity installation."
  exit 1
fi
UNITY_VERSION="$(grep -oE '^m_EditorVersion: .*' "$ROOT/unity/ProjectSettings/ProjectVersion.txt" 2>/dev/null | awk '{print $2}')"
UNITY="${CM_UNITY_BIN:-/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity}"
IOS_MODULE="${CM_IOS_MODULE_DIR:-/Applications/Unity/Hub/Editor/${UNITY_VERSION}/PlaybackEngines/iOSSupport}"
OUT="${1:-$ROOT/build/ios}"
LOG="$ROOT/build/unity-ios-build.log"

# Unity does not promise to keep the invoking shell's working directory. Resolve a caller's
# relative output before crossing that process boundary so the shell preflight, Unity builder,
# and post-run artifact check all refer to the same directory.
case "$OUT" in
  /*) ;;
  *) OUT="$PWD/$OUT" ;;
esac

# Apple's floor for anything uploaded to App Store Connect, effective 2026-04-28:
# "Apps uploaded to App Store Connect must be built with Xcode 26 or later using an SDK
# for iOS 26, iPadOS 26, tvOS 26, visionOS 26, or watchOS 26."
# https://developer.apple.com/news/upcoming-requirements/
MIN_XCODE_MAJOR=26

fail() { echo "FAIL: $*"; exit 1; }

# ---------------------------------------------------------------------------------------
# Preflight. Every check below costs a second; skipping them costs a 30-45 minute IL2CPP
# build that fails at the end, or worse, an archive that App Store Connect rejects on
# upload after you have already waited for it.
# The upload floor is intentionally enforced for development projects too: this release wrapper
# uses one target toolchain, so a debug build cannot conceal a release-toolchain blocker.
# ---------------------------------------------------------------------------------------
[ -n "$UNITY_VERSION" ] || fail "cannot read Unity version from ProjectVersion.txt"
[ -x "$UNITY" ] || fail "Unity $UNITY_VERSION not installed at $UNITY"

[ -d "$IOS_MODULE" ] || fail "Unity $UNITY_VERSION has no iOS Build Support module.
  Install it: Unity Hub > Installs > $UNITY_VERSION > gear icon > Add modules >
  'iOS Build Support'. Expected at: $IOS_MODULE"

command -v xcodebuild >/dev/null 2>&1 || fail "xcodebuild not found. Install Xcode from the
  Mac App Store, launch it once to accept the licence, then:
    sudo xcode-select -s /Applications/Xcode.app/Contents/Developer"

XCODE_VERSION="$(xcodebuild -version 2>/dev/null | grep -oE '^Xcode [0-9.]+' | awk '{print $2}')"
XCODE_MAJOR="${XCODE_VERSION%%.*}"
if [ -z "$XCODE_MAJOR" ]; then
  fail "could not parse an Xcode version from 'xcodebuild -version'; refusing an
  App Store build whose toolchain floor is unproven"
elif [ "$XCODE_MAJOR" -lt "$MIN_XCODE_MAJOR" ]; then
  fail "Xcode $XCODE_VERSION is below Apple's floor for App Store uploads.
  Since 2026-04-28 App Store Connect requires Xcode $MIN_XCODE_MAJOR or later, built
  against an iOS $MIN_XCODE_MAJOR SDK. A build from this Xcode would be rejected at
  upload, after you had already spent the build and the archive. Update Xcode first."
fi

command -v xcrun >/dev/null 2>&1 || fail "xcrun not found in the selected Xcode toolchain"
IOS_SDK_VERSION="$(xcrun --sdk iphoneos --show-sdk-version 2>/dev/null || true)"
IOS_SDK_MAJOR="${IOS_SDK_VERSION%%.*}"
if ! printf '%s\n' "$IOS_SDK_VERSION" | grep -Eq '^[0-9]+([.][0-9]+)*$'; then
  fail "could not find an iPhone device SDK through xcrun. Launch Xcode once, finish
  installing its platform components, and confirm 'xcrun --sdk iphoneos
  --show-sdk-version' prints a version."
elif [ "$IOS_SDK_MAJOR" -lt "$MIN_XCODE_MAJOR" ]; then
  fail "iOS SDK $IOS_SDK_VERSION is below Apple's App Store upload floor.
  Since 2026-04-28 the project must be built against an iOS $MIN_XCODE_MAJOR SDK or
  later. Install/select a current Xcode before generating the project."
fi

# The iOS bundle identifier is the one Player Setting that cannot be fixed downstream:
# an archive under the wrong bundle ID cannot be uploaded against the App Store Connect
# record. CatMetroCliIosBuild refuses on it too — this is just the fast, cheap check.
BUNDLE_ID="$(awk '
  /^  applicationIdentifier:[[:space:]]*$/ { in_identifiers = 1; next }
  in_identifiers && /^    iPhone:[[:space:]]*/ {
    sub(/^    iPhone:[[:space:]]*/, "")
    sub(/[[:space:]]+$/, "")
    print
    exit
  }
  in_identifiers && /^  [^ ]/ { exit }
' "$ROOT/unity/ProjectSettings/ProjectSettings.asset" 2>/dev/null)"
if [ -z "$BUNDLE_ID" ]; then
  fail "no iPhone bundle identifier found in ProjectSettings.asset.
  Set Player Settings > iOS > Bundle Identifier before spending time on a build."
fi

case "$OUT" in
  *.[iI][pP][aA]|*.[aA][pP][pP]|*.[xX][cC][aA][rR][cC][hH][iI][vV][eE]|*.[xX][cC][oO][dD][eE][pP][rR][oO][jJ])
    fail "iOS output must be a directory that will contain the generated Xcode project,
  not an .ipa, .app, .xcarchive, or .xcodeproj path: $OUT"
    ;;
esac

if [ -e "$OUT" ] && [ ! -d "$OUT" ]; then
  fail "Xcode output path exists but is not a directory: $OUT"
fi
if [ -d "$OUT" ] && [ -n "$(find "$OUT" -mindepth 1 -maxdepth 1 -print -quit 2>/dev/null)" ]; then
  fail "Xcode output directory is not empty: $OUT
  Move or remove that generated build first, or choose a fresh output directory. Refusing
  prevents a failed Unity run from being mistaken for success because stale files remain."
fi

mkdir -p "$(dirname "$LOG")" "$OUT" || fail "could not create log/output directories"

DEV="${CM_DEV_BUILD:-0}"
echo "Unity     : $UNITY_VERSION"
echo "Xcode     : ${XCODE_VERSION:-unknown}"
echo "iOS SDK   : $IOS_SDK_VERSION"
echo "iOS module: present"
echo "Bundle ID : $BUNDLE_ID"
echo "Channel   : $([ "$DEV" = "1" ] && echo 'DEVELOPMENT — never upload this one' || echo 'release')"
echo "Xcode proj: $OUT"
echo "Log       : $LOG"
echo "A cold IL2CPP/ARM64 build takes 25-45 minutes. Follow it with: tail -f $LOG"
echo

# No -quit: CatMetroCliIosBuild exits with the real build result itself. Passing -quit here
# would exit before the build finished — the same trap that bites -runTests.
#
# -buildTarget iOS switches the project's active platform, which also makes UNITY_IOS
# defined so CatMetroIosPostProcess compiles in. Side effect worth knowing: your editor is
# left on iOS afterwards. Switch back in File > Build Settings if you were mid-Android work.
CM_IOS_OUT="$OUT" \
"$UNITY" -batchmode -nographics \
  -projectPath "$ROOT/unity" \
  -buildTarget iOS \
  -executeMethod CatMetroCliIosBuild.BuildIos \
  -logFile "$LOG"
rc=$?

echo
echo "Unity exit: $rc"
grep -E "CLI_IOS_RESULT|CLI_IOS_SIGNING|CM_IOS_POSTPROCESS" "$LOG" 2>/dev/null | tail -10
grep -E "error CS|BuildFailedException" "$LOG" 2>/dev/null | head -10

if [ "$rc" -ne 0 ]; then
  echo
  echo "Unity failed — see $LOG"
  exit "$rc"
fi

if [ ! -f "$OUT/Unity-iPhone.xcodeproj/project.pbxproj" ]; then
  echo
  echo "No Xcode project produced — see $LOG"
  exit 1
fi

echo
echo "Xcode project: $OUT/Unity-iPhone.xcodeproj"
echo

if [ "$DEV" = "1" ]; then
  cat <<'DEVWARN'
=======================================================================
 DEVELOPMENT BUILD. Do not archive this for TestFlight or the App Store.
 Rebuild without CM_DEV_BUILD=1 before any submission.
=======================================================================
DEVWARN
  exit 0
fi

cat <<NEXT
Next steps are YOURS to perform in Xcode — this repo never archives, signs or uploads.

  1. Open the generated project:
       open "$OUT/Unity-iPhone.xcodeproj"
     Select the Unity-iPhone target > Signing & Capabilities > Team.
     "Automatically manage signing" is the right choice for a solo developer.
     Re-check signing on every freshly generated project unless you have configured a
     persistent local signing workflow.

  2. After membership and signing are active, use Product > Archive, then Organizer >
     Validate App. Distribution/upload remains human-only.

Full walkthrough, including everything App Store Connect needs alongside the
binary: docs/release/ios-release-runbook.md
NEXT
