#!/usr/bin/env bash
# BUILD-AAB criterion: main carries a scripted Android App Bundle entry point so the
# closed-test upload path does not depend on GUI-only steps (the Lane 10 runbook's
# recorded gap). Static shape gates in the cli-build-shim.test.sh style: greps over the
# committed source, CI-runnable with no editor. The builder must never touch keystore
# MATERIAL (signing config is the human's local, uncommitted Player Settings state —
# threat-model row: upload keystore never in repo, never agent-reachable); it must
# surface which signing state produced the artifact, machine-readably.
set -eu
cd "$(git rev-parse --show-toplevel)"

fail() { echo "cli-aab-build.test.sh: FAIL — $*" >&2; exit 1; }

src="unity/Assets/Editor/CatMetroCliAabBuild.cs"
meta="$src.meta"
[ -f "$src" ] && [ -f "$meta" ] || fail "committed AAB builder or meta is missing"
grep -Eq '^guid: [0-9a-f]{32}$' "$meta" || fail "stable Unity meta GUID is missing"

grep -q 'CM_AAB_OUT' "$src" || fail "output seam is missing"
grep -q 'Path.GetFullPath' "$src" || fail "output path is not normalized"
grep -q 'Path.GetExtension' "$src" || fail "extension gate is missing"
grep -q '".aab"' "$src" || fail "the .aab extension is not fail-closed"
grep -q 'Directory.CreateDirectory' "$src" || fail "output parent is not created"
grep -q 'Assets/Scenes/Game.unity' "$src" || fail "shipped scene is not pinned"

grep -q 'EditorUserBuildSettings.buildAppBundle = true' "$src" \
  || fail "the builder does not select AAB"
grep -q 'EditorUserBuildSettings.buildAppBundle = false' "$src" \
  || fail "AAB state is not restored — a later APK build would inherit it"
grep -q 'finally' "$src" || fail "the AAB-state restore is not failure-proof"

grep -q 'BuildOptions.Development' "$src" && fail "a store AAB path must not offer dev builds"
grep -q 'CM_DEV_BUILD' "$src" || fail "the dev-build refusal seam is missing"

grep -q 'useCustomKeystore' "$src" || fail "signing state is not surfaced"
grep -q 'signing=' "$src" || fail "machine-readable signing marker is missing"
grep -Eq 'keystorePass|keyaliasPass|CM_KEYSTORE' "$src" \
  && fail "the builder must never touch keystore material"

ext_line=$(grep -n 'Path.GetExtension' "$src" | head -1 | cut -d: -f1)
mkdir_line=$(grep -n 'Directory.CreateDirectory' "$src" | cut -d: -f1)
build_line=$(grep -n 'BuildPipeline.BuildPlayer' "$src" | cut -d: -f1)
[ -n "$ext_line" ] && [ "$ext_line" -lt "$mkdir_line" ] && [ "$mkdir_line" -lt "$build_line" ] \
  || fail "gates must run before output mutation and BuildPlayer"

echo "cli-aab-build.test.sh: OK"
