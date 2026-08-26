#!/usr/bin/env bash
# STORE-RELEASE criterion: the APK entry point declares its own artifact kind instead of
# inheriting one. EditorUserBuildSettings.buildAppBundle persists in unity/Library, so once
# the AAB builder has set it true, an unguarded APK build silently emits a BUNDLE with a
# .apk filename — a failure whose only symptom is a confusing adb/Play rejection much later
# (.claude/rules/unity.md, "The AAB flag persists").
#
# Static shape gates in the cli-aab-build.test.sh style: every gate greps a COMMENT-STRIPPED
# view of the source, so prose in a comment can neither satisfy nor evade a gate. The
# stripped view lives in a shell variable, not a temp file — sandboxed shells may deny
# system-TMPDIR writes.
set -eu
cd "$(git rev-parse --show-toplevel)"

fail() { echo "cli-apk-build.test.sh: FAIL — $*" >&2; exit 1; }

src="unity/Assets/Editor/CatMetroCliBuild.cs"
[ -f "$src" ] || fail "committed APK builder is missing"

grep -q '/\*' "$src" && fail "block comments would evade the line-comment strip"
stripped="$(sed 's://.*::' "$src")"
has() { grep -q "$1" <<<"$stripped"; }
first_line() { grep -n "$1" <<<"$stripped" | head -1 | cut -d: -f1; }
last_line()  { grep -n "$1" <<<"$stripped" | tail -1 | cut -d: -f1; }

has 'CM_APK_OUT' || fail "output seam is missing"
has 'Assets/Scenes/Game.unity' || fail "shipped scene is not pinned"

# The core guard: set explicitly to false, never inherited.
has 'EditorUserBuildSettings.buildAppBundle = false' \
  || fail "the APK builder does not force buildAppBundle=false — a persisted true would emit an AAB named .apk"
has 'EditorUserBuildSettings.buildAppBundle = previousAppBundle' \
  || fail "the previous app-bundle state is not restored"
has 'finally' || fail "the app-bundle restore is not failure-proof"

# The inverse mistake: an .aab path handed to the APK entry point.
has 'Path.GetExtension' || fail "extension gate is missing"
has '".apk"' || fail "the .apk extension is not fail-closed"

# Neither builder may ever touch keystore material or WRITE Player Settings. Denylists run
# on the RAW source: a string literal carrying '//' would vanish from the stripped view, and
# for a denylist a prose false-positive is the desirable failure direction.
grep -Eq '\b(keystoreName|keyaliasName|keystorePass|keyaliasPass)\b|CM_KEYSTORE|KEYSTORE_PASS' "$src" \
  && fail "the builder must never touch keystore material"
grep -Eq 'PlayerSettings\.[A-Za-z0-9_.]+[[:space:]]*=[^=]' "$src" \
  && fail "the builder must never WRITE Player Settings"

# Ordering: the flag must be set before BuildPlayer, and the result exit must sit below the
# finally restore so the restore always runs before process death.
flag_line=$(first_line 'EditorUserBuildSettings.buildAppBundle = false')
build_line=$(first_line 'BuildPipeline.BuildPlayer')
[ -n "$flag_line" ] && [ "$flag_line" -lt "$build_line" ] \
  || fail "buildAppBundle must be forced before BuildPlayer runs"

exit_line=$(last_line 'EditorApplication.Exit')
finally_line=$(first_line 'finally')
[ -n "$exit_line" ] && [ "$finally_line" -lt "$exit_line" ] \
  || fail "the build-result exit must come after the finally restore"

# --- The AAB shell wrapper exists and is upload-safe ---
aab_sh="scripts/build-aab.sh"
[ -f "$aab_sh" ] || fail "scripts/build-aab.sh is missing — the AAB C# entry point has no shell path"

grep -q 'CatMetroCliAabBuild.BuildAndroidAab' "$aab_sh" \
  || fail "build-aab.sh does not call the AAB entry point"
grep -q 'CM_AAB_OUT' "$aab_sh" || fail "build-aab.sh does not set the output seam"
grep -q 'BundleConfig.pb' "$aab_sh" \
  || fail "build-aab.sh does not verify the artifact is really a bundle"

# -quit makes Unity exit before the build finishes (the same trap that bites -runTests).
# Comment lines are stripped first: this file's own prose explains why -quit is absent, and
# a gate that its own rationale trips is a gate nobody keeps.
aab_code="$(grep -vE '^[[:space:]]*#' "$aab_sh")"
grep -Eq '(^|[[:space:]])-quit\b' <<<"$aab_code" \
  && fail "-quit makes Unity exit before the build finishes (same trap as -runTests)"

# An agent must never run a Play upload (AGENTS.md, non-negotiable). The wrapper may print
# upload INSTRUCTIONS but must not invoke an upload tool.
grep -Eq '\b(bundletool|fastlane|gradlew?[[:space:]]+publish|google-play-cli|supply)\b' "$aab_sh" \
  && fail "build-aab.sh must never invoke an upload tool — uploading is human-only"

# --- Build outputs and keystore-password files are ignored ---
for pat in 'build/' '*.aab' '*.apk' 'keystore.properties'; do
  grep -qxF "$pat" .gitignore || fail ".gitignore does not cover '$pat'"
done

echo "cli-apk-build.test.sh: OK"
