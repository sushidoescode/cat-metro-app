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
# agent runs `xcodebuild archive` or touches App Store Connect. The commands are printed
# at the end of a successful run, and explained in docs/release/ios-release-runbook.md.
#
# Default is a RELEASE project. CM_DEV_BUILD=1 makes a development project for on-device
# debugging; that one must never be archived for TestFlight or the App Store.
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY_VERSION="$(grep -oE '^m_EditorVersion: .*' "$ROOT/unity/ProjectSettings/ProjectVersion.txt" 2>/dev/null | awk '{print $2}')"
UNITY="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity"
IOS_MODULE="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/PlaybackEngines/iOSSupport"
OUT="${1:-$ROOT/build/ios}"
LOG="$ROOT/build/unity-ios-build.log"

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
  echo "WARN: could not parse an Xcode version from 'xcodebuild -version'. Continuing."
elif [ "$XCODE_MAJOR" -lt "$MIN_XCODE_MAJOR" ]; then
  fail "Xcode $XCODE_VERSION is below Apple's floor for App Store uploads.
  Since 2026-04-28 App Store Connect requires Xcode $MIN_XCODE_MAJOR or later, built
  against an iOS $MIN_XCODE_MAJOR SDK. A build from this Xcode would be rejected at
  upload, after you had already spent the build and the archive. Update Xcode first."
fi

# The iOS bundle identifier is the one Player Setting that cannot be fixed downstream:
# an archive under the wrong bundle ID cannot be uploaded against the App Store Connect
# record. CatMetroCliIosBuild refuses on it too — this is just the fast, cheap check.
BUNDLE_ID="$(grep -A4 '^  applicationIdentifier:' "$ROOT/unity/ProjectSettings/ProjectSettings.asset" 2>/dev/null | grep -oE '^    iPhone: .*' | sed 's/^    iPhone: //')"
if [ -z "$BUNDLE_ID" ]; then
  echo "WARN: no iPhone bundle identifier found in ProjectSettings.asset."
  echo "      Unity will invent one from companyName/productName and the build will be"
  echo "      refused. Set Player Settings > iOS > Bundle Identifier first."
fi

mkdir -p "$(dirname "$LOG")" "$OUT"

DEV="${CM_DEV_BUILD:-0}"
echo "Unity     : $UNITY_VERSION"
echo "Xcode     : ${XCODE_VERSION:-unknown}"
echo "iOS module: present"
echo "Bundle ID : ${BUNDLE_ID:-<unset — build will refuse>}"
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
Next steps are YOURS to run — this repo never archives, signs or uploads.

  1. Open it once, to pick a signing team:
       open "$OUT/Unity-iPhone.xcodeproj"
     Select the Unity-iPhone target > Signing & Capabilities > Team.
     "Automatically manage signing" is the right choice for a solo developer.

  2. Archive (~5-15 min):
       xcodebuild -project "$OUT/Unity-iPhone.xcodeproj" \\
         -scheme Unity-iPhone -configuration Release \\
         -archivePath "$ROOT/build/CatMetro.xcarchive" \\
         -destination 'generic/platform=iOS' archive

  3. Export the .ipa (needs an ExportOptions.plist — template in the runbook):
       xcodebuild -exportArchive \\
         -archivePath "$ROOT/build/CatMetro.xcarchive" \\
         -exportOptionsPlist "$ROOT/build/ExportOptions.plist" \\
         -exportPath "$ROOT/build/ipa"

  4. Upload with Transporter.app, or Xcode > Organizer > Distribute App.

Full walkthrough, including everything App Store Connect needs alongside the
binary: docs/release/ios-release-runbook.md
NEXT
