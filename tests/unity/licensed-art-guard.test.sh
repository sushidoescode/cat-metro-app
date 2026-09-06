#!/usr/bin/env bash
# LICENSED-ART GUARD: a player build must refuse to ship placeholder cats silently. The catalogs
# fail closed with no log line, so the guard is the only thing standing between a missing local
# install and a store binary full of flat cats (first Pixel dev build, 2026-09-05).
set -eu
cd "$(git rev-parse --show-toplevel)"
fail() { echo "licensed-art-guard.test.sh: FAIL — $*" >&2; exit 1; }
src="unity/Assets/Editor/CatMetroLicensedArtGuard.cs"
[ -f "$src" ] || fail "guard is missing"
grep -q '/\*' "$src" && fail "block comments would evade the line-comment strip"
stripped="$(sed 's://.*::' "$src")"
has() { grep -q "$1" <<<"$stripped"; }
has 'IPreprocessBuildWithReport' || fail "guard is not a build preprocess hook (must run for GUI and CLI builds)"
has '"CatRigs/BoardCatRig"' || fail "guard does not probe the licensed rig resource"
has '"CatMetroProps/' || fail "guard does not probe a licensed prop resource"
has 'BuildFailedException' || fail "guard does not fail the build"
has 'CM_ALLOW_PLACEHOLDER_ART' || fail "guard has no explicit placeholder override"
has 'CLI_BUILD_ASSETS' || fail "guard does not log the asset state for the build log"
cat_path="$(grep -o 'ResourcePath = "[^"]*"' unity/Assets/Scripts/Presentation/Cats/CatModelCatalog.cs | head -1 | cut -d'"' -f2)"
[ "$cat_path" = "CatRigs/BoardCatRig" ] || fail "CatModelCatalog.ResourcePath changed to '$cat_path'; update the guard"
prop_root="$(grep -o 'ResourceRoot = "[^"]*"' unity/Assets/Scripts/Presentation/Props/PropModelCatalog.cs | head -1 | cut -d'"' -f2)"
[ "$prop_root" = "CatMetroProps/" ] || fail "PropModelCatalog.ResourceRoot changed to '$prop_root'; update the guard"
echo "licensed-art-guard.test.sh: OK"
