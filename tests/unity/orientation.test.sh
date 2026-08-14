#!/usr/bin/env bash
# Orientation lock gate (ORIENT-LOCK): the app must never rotate away from portrait on device —
# human directive 2026-08-14, in-session ("when I try to switch to a wide screen view on the
# phone it doesn't change orientation, it needs to stay straight"). Pins two YAML facts in
# ProjectSettings.asset: the default orientation is Portrait (enum 0, not AutoRotation's 4), and
# both landscape autorotate flags are off, so no runtime rotation path into landscape exists.
# Fail-closed on a missing file or a missing/duplicated field — a silent regression here
# re-enables rotation. See state/handoffs/ORIENT-LOCK-frozen-contract.md for the full contract.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
fail() { echo "orientation.test.sh: FAIL — $1"; exit 1; }
PS="unity/ProjectSettings/ProjectSettings.asset"

[ -f "$PS" ] || fail "ProjectSettings.asset missing (fail-closed)"

# --- criterion: default orientation is Portrait (enum 0), not AutoRotation (4) or any other value ---
count=$(grep -c '^  defaultScreenOrientation:' "$PS" || true)
[ "$count" = "1" ] || fail "defaultScreenOrientation field missing or duplicated (found $count)"
grep -q '^  defaultScreenOrientation: 0$' "$PS" \
  || fail "defaultScreenOrientation is not Portrait (0) — the app can rotate away from portrait"

# --- criterion: both landscape autorotate flags are off (irrelevant once locked, normalized anyway) ---
count=$(grep -c '^  allowedAutorotateToLandscapeRight:' "$PS" || true)
[ "$count" = "1" ] || fail "allowedAutorotateToLandscapeRight field missing or duplicated (found $count)"
grep -q '^  allowedAutorotateToLandscapeRight: 0$' "$PS" \
  || fail "allowedAutorotateToLandscapeRight is not 0 — landscape autorotation is still allowed"

count=$(grep -c '^  allowedAutorotateToLandscapeLeft:' "$PS" || true)
[ "$count" = "1" ] || fail "allowedAutorotateToLandscapeLeft field missing or duplicated (found $count)"
grep -q '^  allowedAutorotateToLandscapeLeft: 0$' "$PS" \
  || fail "allowedAutorotateToLandscapeLeft is not 0 — landscape autorotation is still allowed"

echo "orientation.test.sh: OK (portrait locked, landscape autorotate flags off)"
exit 0
