#!/usr/bin/env bash
# CM-C2b-DEVFIX always-on gates (criteria 1-5 statics; ADR-0005 wrapper style). Fail-closed on
# every missing scan root/asset; every gate proven to FIRE against a negative fixture. The
# PlayMode legs run through editmode.test.sh's editor half; this file is editor-free.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
fail() { echo "device-config.test.sh: FAIL — $1"; exit 1; }
QS="unity/ProjectSettings/QualitySettings.asset"
GS="unity/ProjectSettings/GraphicsSettings.asset"
FIX="tests/fixtures/device-config-bad"

# --- criterion 1(i): every quality-level pipeline override is CLEARED ---
[ -f "$QS" ] || fail "criterion 1: QualitySettings.asset missing (fail-closed)"
total=$(grep -c 'customRenderPipeline:' "$QS" || true)
cleared=$(grep -c 'customRenderPipeline: {fileID: 0}' "$QS" || true)
[ "$total" -ge 1 ] && [ "$total" = "$cleared" ] \
  || fail "criterion 1: a quality level still overrides the project pipeline ($cleared/$total cleared)"

# --- criterion 1(ii): the project pipeline reference resolves to a COMMITTED asset ---
# (this is the gate that would have caught the original dangling-guid defect)
resolve_pipeline_guid() { # $1 = graphics-settings-shaped file; echoes 0 resolved / 1 not
  local g
  g=$(sed -n 's/.*m_CustomRenderPipeline: {fileID: [0-9][0-9]*, guid: \([a-f0-9]*\),.*/\1/p' "$1" | head -1)
  if [ -z "$g" ]; then echo 1; return; fi
  if grep -rq "guid: $g" unity/Assets --include='*.meta' 2>/dev/null; then echo 0; else echo 1; fi
}
[ "$(resolve_pipeline_guid "$GS")" = "0" ] \
  || fail "criterion 1: m_CustomRenderPipeline is unset or its guid resolves to no committed asset"
[ -f "$FIX/GraphicsSettings-dangling.asset" ] || fail "criterion 1: dangling-guid fixture missing (fail-closed)"
[ "$(resolve_pipeline_guid "$FIX/GraphicsSettings-dangling.asset")" = "1" ] \
  || fail "criterion 1: the guid resolver failed to fire on the dangling fixture"

# --- criterion 2: pinned renderer YAML (typed asserts live in the PlayMode leg) ---
URPA="unity/Assets/Settings/CatMetro_URP.asset"
URPR="unity/Assets/Settings/CatMetro_Renderer.asset"
[ -f "$URPA" ] && [ -f "$URPA.meta" ] && [ -f "$URPR" ] && [ -f "$URPR.meta" ] \
  || fail "criterion 2: committed pipeline/renderer assets missing (fail-closed)"
grep -q 'm_RendererFeatures: \[\]' "$URPR" \
  || fail "criterion 2: renderer-features list is not empty (no hidden passes)"
grep -q 'm_DepthPrimingMode: 0' "$URPR" \
  || fail "criterion 2: depth priming is not disabled"

# --- criterion 3: vsync posture pinned; no dev-guard in the shipped policy files ---
vs_total=$(grep -c 'vSyncCount:' "$QS" || true)
vs_zero=$(grep -c 'vSyncCount: 0' "$QS" || true)
[ "$vs_total" -ge 1 ] && [ "$vs_total" = "$vs_zero" ] \
  || fail "criterion 3: a committed quality level carries a non-zero vsync count"
grep -A3 'm_PerPlatformDefaultQuality' "$QS" | grep -q 'Android: 0' \
  || fail "criterion 3: the Android default quality level drifted"
vsc=$(grep -rEn 'vSyncCount[[:space:]]*=[[:space:]]*[1-9]' --include='*.cs' unity/Assets/Scripts 2>/dev/null || true)
[ -z "$vsc" ] || fail "criterion 3: shipped code assigns a non-zero vsync count: $vsc"
for f in unity/Assets/Scripts/Bootstrap/FramePolicy.cs unity/Assets/Scripts/Presentation/Board/GreyboxMaterial.cs; do
  [ -f "$f" ] || fail "criterion 3: $f missing (fail-closed)"
  bad=$(grep -En '#if|#elif|#else|UNITY_EDITOR|isDebugBuild|isEditor' "$f" || true)
  [ -z "$bad" ] || fail "criterion 3: dev-guard token in a shipped policy file: $bad"
done
[ -f "$FIX/GuardedPolicy.cs" ] || fail "criterion 3: guard fixture missing (fail-closed)"
grep -Eq '#if|UNITY_EDITOR' "$FIX/GuardedPolicy.cs" \
  || fail "criterion 3: the guard pattern failed to fire on the fixture"
[ -f "$FIX/QualitySettings-vsync1.asset" ] || fail "criterion 3: vsync fixture missing (fail-closed)"
f_total=$(grep -c 'vSyncCount:' "$FIX/QualitySettings-vsync1.asset" || true)
f_zero=$(grep -c 'vSyncCount: 0' "$FIX/QualitySettings-vsync1.asset" || true)
[ "$f_total" != "$f_zero" ] || fail "criterion 3: the vsync gate failed to fire on the fixture"

# --- criterion 4: the committed material, shader reference non-null ---
MAT="unity/Assets/Resources/Materials/Greybox.mat"
[ -f "$MAT" ] && [ -f "$MAT.meta" ] || fail "criterion 4: committed material missing (fail-closed)"
grep -q 'm_Name: Greybox' "$MAT" || fail "criterion 4: material name drifted"
grep -q 'm_Shader:' "$MAT" || fail "criterion 4: material carries no shader reference"
if grep -q 'm_Shader: {fileID: 0}' "$MAT"; then
  fail "criterion 4: null shader reference — that is silent magenta"
fi

# --- criterion 5 static: primitive count == bind count, proven live ---
prim=$(grep -ro 'GameObject.CreatePrimitive' unity/Assets/Scripts/Presentation --include='*.cs' 2>/dev/null | wc -l | tr -d ' ')
bind=$(grep -ro 'GreyboxMaterial.Shared' unity/Assets/Scripts/Presentation --include='*.cs' 2>/dev/null | wc -l | tr -d ' ')
[ "$prim" = "$bind" ] && [ "$prim" != "0" ] \
  || fail "criterion 5: unbound runtime primitives ($prim CreatePrimitive vs $bind binds)"
fp=$(grep -ro 'GameObject.CreatePrimitive' "$FIX" --include='*.cs' 2>/dev/null | wc -l | tr -d ' ')
fb=$(grep -ro 'GreyboxMaterial.Shared' "$FIX" --include='*.cs' 2>/dev/null | wc -l | tr -d ' ')
[ "$fp" != "$fb" ] || fail "criterion 5: the counting gate failed to fire on the fixture"

echo "device-config.test.sh: OK (1, 2-yaml, 3, 4, 5-static)"
exit 0
