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

# Criterion 13: engine-free — zero UnityEngine / persistentDataPath outside Bootstrap (which
# does not exist yet; the grep covers the whole Scripts tree minus that future root).
eng=$(grep -rEn --include='*.cs' '\b(UnityEngine|persistentDataPath)\b' "$app_root" unity/Assets/Scripts/Services 2>/dev/null || true)
[ -z "$eng" ] || fail "criterion 13: engine reference outside Bootstrap: $eng"
if grep -rEnq --include='*.cs' '#if UNITY_ANDROID' "$app_root" 2>/dev/null; then
  fail "criterion 13: conditional compilation under Application"
fi

# Criterion 14 (Q-T): the ledger is a data structure — zero monetization tokens under Save.
mon=$(grep -rEn --include='*.cs' '/billing/|/iap/|/ads/|RevenueCat|Purchases\.|BillingClient|GoogleMobileAds' "$app_root/Save" 2>/dev/null || true)
[ -z "$mon" ] || fail "criterion 14: monetization token under Application/Save: $mon"
# ...with the pattern proven live against a negative fixture.
grep -rEq '/billing/|/iap/|/ads/|RevenueCat|Purchases\.|BillingClient|GoogleMobileAds' tests/fixtures/save-bad \
  || fail "criterion 14: monetization pattern failed to fire on the negative fixture"

# Criterion 4's real-filesystem half: the durable-write shape lives in exactly one file.
grep -q 'Flush(flushToDisk: true)' "$app_root/Save/ISaveFileSystem.cs" \
  || fail "criterion 4: Flush(flushToDisk: true) missing from the real filesystem seam"
grep -q 'File\.Replace' "$app_root/Save/ISaveFileSystem.cs" \
  || fail "criterion 4: File.Replace missing from the real filesystem seam"

echo "save.test.sh: OK (4-shape, 13, 14, 15)"
exit 0
