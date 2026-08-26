#!/usr/bin/env bash
# Static safeguards for the scripted Android App Bundle entry point. Checks use a
# comment-stripped source view so prose cannot satisfy a behavior claim. The raw-source
# denylist still covers keystore fields, credential names, and PlayerSettings assignments:
# the builder may report signing state but must never read credentials or write tracked
# PlayerSettings. Ordering checks keep validation ahead of mutation and keep the final
# process exit after the persistent bundle-format flag is restored.
set -eu
cd "$(git rev-parse --show-toplevel)"

fail() { echo "cli-aab-build.test.sh: FAIL — $*" >&2; exit 1; }

src="unity/Assets/Editor/CatMetroCliAabBuild.cs"
meta="$src.meta"
[ -f "$src" ] && [ -f "$meta" ] || fail "committed AAB builder or meta is missing"
grep -Eq '^guid: [0-9a-f]{32}$' "$meta" || fail "stable Unity meta GUID is missing"

grep -q '/\*' "$src" && fail "block comments would evade the line-comment strip"
stripped="$(sed 's://.*::' "$src")"
has()  { grep -q  "$1" <<<"$stripped"; }
hasE() { grep -Eq "$1" <<<"$stripped"; }
first_line() { grep -n "$1" <<<"$stripped" | head -1 | cut -d: -f1; }
last_line()  { grep -n "$1" <<<"$stripped" | tail -1 | cut -d: -f1; }

has 'CM_AAB_OUT' || fail "output seam is missing"
has 'Path.GetFullPath' || fail "output path is not normalized"
has 'Path.GetExtension' || fail "extension gate is missing"
has '".aab"' || fail "the .aab extension is not fail-closed"
has 'Directory.CreateDirectory' || fail "output parent is not created"
has 'Assets/Scenes/Game.unity' || fail "shipped scene is not pinned"

# Public campaign copy is derived at the same build decision site that selects the player
# content. The shell wrapper later proves each named JSON is physically present in the AAB.
has 'GameRoot.LevelBand' || fail "campaign receipt is not derived from normal progression"
has 'File.Exists(stagedLevel)' || fail "campaign receipt does not require staged level content"
has 'campaignLevels=' || fail "machine-readable campaign count is missing"
has 'campaignIds=' || fail "machine-readable campaign ID receipt is missing"

has 'EditorUserBuildSettings.buildAppBundle = true' \
  || fail "the builder does not select AAB"
has 'EditorUserBuildSettings.buildAppBundle = previousAppBundle' \
  || fail "AAB state is not restored to its previous value"
has 'finally' || fail "the AAB-state restore is not failure-proof"

has 'BuildOptions.Development' \
  && fail "a store AAB path must not offer dev builds"
has 'CM_DEV_BUILD' || fail "the dev-build refusal seam is missing"

has 'useCustomKeystore' || fail "signing state is not surfaced"
has 'CM_ALLOW_DEBUG_SIGNING' \
  || fail "debug signing must be an explicit opt-in, not a warning"
has 'signing=' || fail "machine-readable signing marker is missing"
# The two security denylists run on the raw source: a string literal carrying a
# '//' would vanish from the stripped view and could smuggle a violation past it, and
# for a denylist a prose false-positive is the desirable failure direction.
grep -Eq '\b(keystoreName|keyaliasName|keystorePass|keyaliasPass)\b|CM_KEYSTORE|KEYSTORE_PASS' "$src" \
  && fail "the builder must never touch keystore material"
grep -Eq 'PlayerSettings\.[A-Za-z0-9_.]+[[:space:]]*=[^=]' "$src" \
  && fail "the builder must never WRITE Player Settings"

ext_line=$(first_line 'Path.GetExtension')
mkdir_line=$(first_line 'Directory.CreateDirectory')
build_line=$(first_line 'BuildPipeline.BuildPlayer')
[ -n "$ext_line" ] && [ "$ext_line" -lt "$mkdir_line" ] && [ "$mkdir_line" -lt "$build_line" ] \
  || fail "gates must run before output mutation and BuildPlayer"
campaign_gate_line=$(first_line 'File.Exists(stagedLevel)')
[ -n "$campaign_gate_line" ] && [ "$campaign_gate_line" -lt "$build_line" ] \
  || fail "campaign staging must be checked before BuildPlayer"

# The build-result exit (the last Exit — early refusals legitimately precede the try)
# must sit below the finally restore, so the restore always runs before process death.
exit_line=$(last_line 'EditorApplication.Exit')
finally_line=$(first_line 'finally')
[ -n "$exit_line" ] && [ "$finally_line" -lt "$exit_line" ] \
  || fail "the build-result exit must come after the finally restore"

echo "cli-aab-build.test.sh: OK"
