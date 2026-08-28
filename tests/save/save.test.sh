#!/usr/bin/env bash
# CM-C7 harness wrapper (criterion 15): exits 0 iff `dotnet test` is green AND the [CI] greps of
# criteria 13/14 hold. Fail-closed on missing scan roots; each check labelled. The scripts/test.sh
# summary-numbers comparison is the PR evidence procedure (CM-C2a criterion 13 precedent).
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
tmp="${TMPDIR:-/tmp}/cm-c7-wrapper-$$"
mkdir -p "$tmp"
trap 'rm -rf "$tmp"' EXIT
fail() { echo "save.test.sh: FAIL — $1"; exit 1; }
app_root="unity/Assets/Scripts/Application"

[ -d "$app_root/Save" ] || fail "scan root $app_root/Save does not exist (fail-closed)"
[ -n "$(ls unity/Assets/Tests/EditMode/Pure/Save/*.cs 2>/dev/null)" ] \
  || fail "criterion 15: Save NUnit sources missing (fail-closed)"

# Criterion 15: the dotnet leg is green — full suite, unfiltered (CM-C6 review F1 precedent).
if ! dotnet test dotnet/CatMetro.sln -c Release --nologo > "$tmp/test.out" 2>&1; then
  tail -20 "$tmp/test.out"
  fail "criterion 15: dotnet test not green"
fi

# Criterion 13, kept in step with ADR-0003's table as engine rows land (CM-C2b armed the first
# one): the ENGINE-FREE assemblies (Domain/Content/Services/Application) may never name
# UnityEngine — the engine rows (Bootstrap, Presentation, Integrations, Editor) may. Explicit
# roots prevent a nested directory named Integrations from hiding a leak in Application.
# The storage-path APIs stay BOOTSTRAP-ONLY across the entire tree (ADR-0003:102-105).
engine_free_roots=(unity/Assets/Scripts/Domain unity/Assets/Scripts/Content \
  unity/Assets/Scripts/Services unity/Assets/Scripts/Application)
non_bootstrap_roots=("${engine_free_roots[@]}" unity/Assets/Scripts/Integrations \
  unity/Assets/Scripts/Presentation)
for root in "${engine_free_roots[@]}"; do
  [ -d "$root" ] || fail "criterion 13: engine-free scan root missing: $root"
done
eng=$(grep -rEn --include='*.cs' '\bUnityEngine\b' "${engine_free_roots[@]}" 2>/dev/null || true)
[ -z "$eng" ] || fail "criterion 13: engine reference in an engine-free assembly: $eng"
pathapi=$(grep -rEn --include='*.cs' '\b(persistentDataPath|temporaryCachePath)\b' \
  "${non_bootstrap_roots[@]}" 2>/dev/null || true)
[ -z "$pathapi" ] || fail "criterion 13: storage-path API outside Bootstrap: $pathapi"
# Platform conditionals belong in engine-facing assemblies. Scan every engine-free root and both
# #if / #elif forms; an exact `#if UNITY_ANDROID` search misses real compound branches.
platform_conditional=$(grep -rEn --include='*.cs' \
  '#(if|elif)[[:space:]]+.*UNITY_[A-Z0-9_]+' \
  "${engine_free_roots[@]}" 2>/dev/null || true)
[ -z "$platform_conditional" ] \
  || fail "criterion 13: platform conditional in an engine-free assembly: $platform_conditional"
printf '%s\n' '#elif UNITY_ANDROID || UNITY_IOS' \
  | grep -Eq '#(if|elif)[[:space:]]+.*UNITY_[A-Z0-9_]+' \
  || fail "criterion 13: platform-conditional pattern missed the compound #elif fixture"

# Criterion 14 (Q-T): the ledger is a data structure — zero monetization tokens under Save.
mon=$(grep -rEn --include='*.cs' '/billing/|/iap/|/ads/|RevenueCat|Purchases\.|BillingClient|GoogleMobileAds' "$app_root/Save" 2>/dev/null || true)
[ -z "$mon" ] || fail "criterion 14: monetization token under Application/Save: $mon"
# ...with the pattern proven live against a negative fixture.
grep -rEq '/billing/|/iap/|/ads/|RevenueCat|Purchases\.|BillingClient|GoogleMobileAds' tests/fixtures/save-bad \
  || fail "criterion 14: monetization pattern failed to fire on the negative fixture"

# Criterion 4's real-filesystem half: the durable-write shape lives in exactly one file.
# Review F3: comments are STRIPPED first — a comment naming the call must never satisfy the
# guard for the single most load-bearing line in the contract (the inverted CM-C1 landmine).
decommented=$(sed 's|//.*||' "$app_root/Save/ISaveFileSystem.cs")
printf '%s\n' "$decommented" | grep -q 'Flush(flushToDisk: true)' \
  || fail "criterion 4: Flush(flushToDisk: true) call missing from the real filesystem seam"
printf '%s\n' "$decommented" | grep -q 'File\.Replace' \
  || fail "criterion 4: File.Replace call missing from the real filesystem seam"

echo "save.test.sh: OK (4-shape, 13, 14, 15)"
exit 0
