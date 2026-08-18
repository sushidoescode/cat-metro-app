#!/usr/bin/env bash
# CM-C9 harness wrapper (criteria 7, 8-static, 9, 11-static, 13). Scan roots are explicit and
# repo-root-relative — NEVER `.` (a second full checkout may live under .claude/worktrees/ and
# would double every match). Fail-closed on missing roots; every guard proven to FIRE against
# tests/fixtures/taxonomy-bad/Banned.cs (the analytics-bad precedent).
set -uo pipefail
full_solution_cache=${CAT_METRO_FULL_SOLUTION_CACHE_DIR-}
full_solution_active=${CAT_METRO_FULL_SOLUTION_CACHE_ACTIVE-}
full_solution_artifacts=${CAT_METRO_FULL_SOLUTION_ARTIFACT_DIR-}
unset CAT_METRO_FULL_SOLUTION_CACHE_DIR CAT_METRO_FULL_SOLUTION_CACHE_ACTIVE \
  CAT_METRO_FULL_SOLUTION_ARTIFACT_DIR
cd "$(git rev-parse --show-toplevel)"
fail() { echo "taxonomy.test.sh: FAIL — $1"; exit 1; }
tax="unity/Assets/Scripts/Application/EventTaxonomy"
scripts_root="unity/Assets/Scripts"
badfx="tests/fixtures/taxonomy-bad/Banned.cs"

[ -d "$tax" ] || fail "criterion 8: taxonomy root missing (fail-closed)"
[ -f "$badfx" ] || fail "negative fixture missing (fail-closed)"

# --- criterion 7: ONE construction site; ZERO SDK namespaces in shipped code ---
sites=$(grep -rl --include='*.cs' 'new AnalyticsEvent(' "$scripts_root" 2>/dev/null || true)
count=$(printf '%s\n' "$sites" | grep -c . || true)
[ "$count" = "1" ] || fail "criterion 7: expected exactly 1 'new AnalyticsEvent(' file under $scripts_root, found $count: $sites"
printf '%s\n' "$sites" | grep -q "EventTaxonomy/Taxonomy.cs" \
  || fail "criterion 7: the one construction site is not the taxonomy builder: $sites"
grep -q 'new AnalyticsEvent(' "$badfx" || fail "criterion 7: construction pattern failed to fire on the fixture"
SDK='Firebase|OneSignalSDK|GoogleMobileAds|RevenueCat'
sdk=$(grep -rEn --include='*.cs' "$SDK" "$scripts_root" 2>/dev/null || true)
[ -z "$sdk" ] || fail "criterion 7: SDK namespace token in shipped code: $sdk"
grep -Eq "$SDK" "$badfx" || fail "criterion 7: SDK pattern failed to fire on the fixture"

# --- criterion 8: the metrics-only wall, re-applied over the new root ---
SAVE='ConsumableLedger|SaveStore|SaveState|SaveDefaults|MigrationTable|LoadResult|RealSaveFileSystem|SaveEventRecord'
wall=$(grep -rEn --include='*.cs' "$SAVE" "$tax" 2>/dev/null || true)
[ -z "$wall" ] || fail "criterion 8: save/ledger token under the taxonomy root: $wall"
grep -Eq "$SAVE" "$badfx" || fail "criterion 8: save-token pattern failed to fire on the fixture"

# --- criterion 9: the 15 dark factories are declared and UNREFERENCED outside the root ---
# (the NUnit case TaxonomyWallTests.WrapperDarkList_EqualsTheTablesDarkSet keeps this list in
# lockstep with the CSV's area ∈ {economy, ads, monetization} rows — edit BOTH or fail)
DARK='TicketEarned|TicketSpent|AdOfferViewed|AdOfferDeclined|RewardedAdStarted|RewardedAdCompleted|RewardedAdFailed|PaywallViewed|PaywallDismissed|PurchaseStarted|PurchaseCompleted|PurchaseFailed|RestoreStarted|RestoreCompleted|EntitlementChanged'
dark=$(grep -rEn --include='*.cs' "Events\.($DARK)" "$scripts_root" 2>/dev/null | grep -v "^$tax/" || true)
[ -z "$dark" ] || fail "criterion 9: a dark factory is referenced outside the taxonomy root: $dark"
grep -Eq "Events\.($DARK)" "$badfx" || fail "criterion 9: dark-reference pattern failed to fire on the fixture"
risky=$(find "$scripts_root" -type d \( -iname 'billing' -o -iname 'iap' -o -iname 'ads' \) 2>/dev/null || true)
[ -z "$risky" ] || fail "criterion 9: a risky-path directory exists under shipped code: $risky"

# --- criterion 11: the queue bounds are read from config, never re-declared ---
lits=$(grep -rEn --include='*.cs' '\b(2000|1048576|512|64)\b' "$tax" 2>/dev/null || true)
[ -z "$lits" ] || fail "criterion 11: queue-bound literal under the taxonomy root: $lits"
grep -Eq '\b512\b' "$badfx" || fail "criterion 11: bound-literal pattern failed to fire on the fixture"

# --- criterion 13: the dotnet leg, full and unfiltered (CM-C6 review F1 precedent) ---
# scripts/test.sh may consume its run-local green attestation; standalone execution still runs it.
tmp="${TMPDIR:-/tmp}/cm-c9-wrapper-$$"
mkdir -p "$tmp"
trap 'rm -rf "$tmp"' EXIT
if ! CAT_METRO_FULL_SOLUTION_CACHE_DIR="$full_solution_cache" \
  CAT_METRO_FULL_SOLUTION_CACHE_ACTIVE="$full_solution_active" \
  CAT_METRO_FULL_SOLUTION_ARTIFACT_DIR="$full_solution_artifacts" \
  python3 scripts/run-full-solution-test.py > "$tmp/test.out" 2>&1; then
  tail -15 "$tmp/test.out"
  fail "criterion 13: dotnet test not green"
fi

echo "taxonomy.test.sh: OK (7, 8-static, 9, 11-static, 13)"
exit 0
