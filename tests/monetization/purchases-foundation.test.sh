#!/usr/bin/env bash
# CM-MONETIZATION-CODE Task 1: static/config gate for the SDK-free passive purchase foundation.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

fail() { echo "purchases-foundation.test.sh: FAIL — $1"; exit 1; }
services="unity/Assets/Scripts/Services/Purchases"
resources="unity/Assets/Resources/Monetization"

[ -d "$services" ] || fail "SDK-free Services/Purchases root missing (fail-closed)"
[ -f "$resources/product_catalog.json" ] || fail "product catalog missing (fail-closed)"
[ -f "$resources/rewarded_placements.json" ] || fail "rewarded placement catalog missing (fail-closed)"

python3 - "$resources/product_catalog.json" "$resources/rewarded_placements.json" <<'PY'
import json
import sys

products = json.load(open(sys.argv[1], encoding="utf-8"))["products"]
expected_products = {
    "cm_all_access": ("non_consumable", ["all_access", "theme_sakura", "theme_neon"]),
    "cm_supporter_pack": ("non_consumable", ["supporter", "all_access", "theme_sakura", "theme_neon"]),
    "cm_theme_sakura": ("non_consumable", ["theme_sakura"]),
    "cm_theme_neon": ("non_consumable", ["theme_neon"]),
    "cm_rewind_5": ("consumable", []),
    "cm_rewind_20": ("consumable", []),
}
actual_products = {p["id"]: (p["storeType"], p["entitlements"]) for p in products}
assert len(products) == len(actual_products) == 6 and actual_products == expected_products

rewards = json.load(open(sys.argv[2], encoding="utf-8"))["placements"]
expected_rewards = {
    "rewind_failure", "double_tickets", "daily_gift_double", "streak_saver", "theme_rental",
    "cat_skin_trial", "livery_trial", "district_guest_route",
}
assert len(rewards) == 8 and {r["id"] for r in rewards} == expected_rewards
expected_reward_rows = {
    "rewind_failure": ("one_rewind", {"session": 2, "localDate": 5}),
    "double_tickets": ("ticket_double", {"localDate": 3}),
    "daily_gift_double": ("gift_double", {"localDate": 1}),
    "streak_saver": ("streak_repair", {"localDate": 1}),
    "theme_rental": ("selected_theme_3_eligible_completed_levels", {"perThemeLocalDate": 1}),
    "cat_skin_trial": ("selected_skin_3_eligible_completed_levels", {"totalSkinLeaseLocalDate": 1}),
    "livery_trial": ("selected_livery_3_eligible_completed_levels", {"totalLiveryLeaseLocalDate": 1}),
    "district_guest_route": ("signed_practice_route", {"perDistrictLocalDate": 1, "session": 1}),
}
for row in rewards:
    assert (row["reward"], row["caps"]) == expected_reward_rows[row["id"]]
    assert row["sdkCallEnabled"] is False
    assert "NEW-Q45" in row["disabledReason"] and "device gate" in row["disabledReason"]
    if row["id"] in {"cat_skin_trial", "livery_trial", "district_guest_route"}:
        assert "ADR-0006 supersession" in row["disabledReason"]
    else:
        assert "ADR-0006" not in row["disabledReason"]
PY

forbidden='RevenueCat|UnityEngine|GoogleMobileAds|IPurchases|IAd|AdHandle|Configure|async|await|event|callback|CanShow|Request|Load|Show|Complete|Grant|Telemetry|Counter|Clock|Save|Persist|ofr_[[:alnum:]_]+'
if grep -rEn --include='*.cs' "$forbidden" "$services" >/dev/null 2>&1; then
  grep -rEn --include='*.cs' "$forbidden" "$services"
  fail "SDK, Unity, persistence, or operational token in passive DTO tree"
fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
printf '%s\n' 'RevenueCat GoogleMobileAds ofr_bad' > "$tmp/forbidden.cs"
grep -rEq --include='*.cs' "$forbidden" "$tmp" || fail "forbidden-token negative control is dead"

dotnet test dotnet/CatMetro.sln -c Release --nologo --filter 'FullyQualifiedName~CatMetro.Tests.Purchases'
echo "purchases-foundation.test.sh: OK (catalogs, disabled rewards, passive DTO scan, live negative control)"
