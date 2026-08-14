#!/usr/bin/env bash
# BUILD-AAB criterion: main carries a scripted Android App Bundle entry point so the
# closed-test upload path does not depend on GUI-only steps (the Lane 10 runbook's
# recorded gap). Static shape gates in the cli-build-shim.test.sh style, hardened per
# the #82 round-1 review: every gate greps a COMMENT-STRIPPED view of the source (F-1 —
# prose in comments must never satisfy or evade a gate), the keystore denylist covers
# names/aliases/passwords/env vars AND any PlayerSettings assignment (F-2 — the builder
# may only ever READ signing state; keystore fields serialize into the tracked
# ProjectSettings.asset), ordering line numbers come from the same stripped view
# (F-1 probe W), debug signing must be an explicit opt-in (F-6), and the build-result
# exit must sit below the finally restore (F-3). The stripped view lives in a shell
# variable, not a temp file — sandboxed shells may deny system-TMPDIR writes.
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
# The two SECURITY denylists run on the RAW source (R-1): a string literal carrying a
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

# The build-result exit (the LAST Exit — early refusals legitimately precede the try)
# must sit below the finally restore, so the restore always runs before process death.
exit_line=$(last_line 'EditorApplication.Exit')
finally_line=$(first_line 'finally')
[ -n "$exit_line" ] && [ "$finally_line" -lt "$exit_line" ] \
  || fail "the build-result exit must come after the finally restore (F-3)"

echo "cli-aab-build.test.sh: OK"
