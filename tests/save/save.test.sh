#!/usr/bin/env bash
# CM-C7 harness wrapper (criterion 15): exits 0 iff `dotnet test` is green AND the [CI] greps of
# criteria 13/14 hold. Fail-closed on missing scan roots; each check labelled. The scripts/test.sh
# summary-numbers comparison is the PR evidence procedure (CM-C2a criterion 13 precedent).
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
fail() { echo "save.test.sh: FAIL — $1"; exit 1; }
app_root="unity/Assets/Scripts/Application"

[ -d "$app_root/Save" ] || fail "scan root $app_root/Save does not exist (fail-closed)"
[ -n "$(ls unity/Assets/Tests/EditMode/Pure/Save/*.cs 2>/dev/null)" ] \
  || fail "criterion 15: Save NUnit sources missing (fail-closed)"

# Criterion 15's dotnet leg has MOVED to tests/suite/solution-suite.test.sh —
# it recomputed "the suite is green" at ~529s a time and discarded the output on
# success. The source-presence guard above STAYS: it is this contract's own
# fail-closed check that the Save cases still exist to be run.

# Criterion 13, kept in step with ADR-0003's table as engine rows land (CM-C2b armed the first
# one): the ENGINE-FREE assemblies (Domain/Content/Services/Application) may never name
# UnityEngine — the engine rows (Bootstrap, Presentation; later Integrations.*/Editor) may.
# The storage-path APIs stay BOOTSTRAP-ONLY across the entire tree (ADR-0003:102-105).
eng=$(grep -rEn --include='*.cs' --exclude-dir=Bootstrap --exclude-dir=Presentation '\bUnityEngine\b' unity/Assets/Scripts 2>/dev/null || true)
[ -z "$eng" ] || fail "criterion 13: engine reference in an engine-free assembly: $eng"
pathapi=$(grep -rEn --include='*.cs' --exclude-dir=Bootstrap '\b(persistentDataPath|temporaryCachePath)\b' unity/Assets/Scripts 2>/dev/null || true)
[ -z "$pathapi" ] || fail "criterion 13: storage-path API outside Bootstrap: $pathapi"
if grep -rEnq --include='*.cs' --exclude-dir=Bootstrap '#if UNITY_ANDROID' unity/Assets/Scripts 2>/dev/null; then
  fail "criterion 13: conditional compilation outside Bootstrap"
fi

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

echo "save.test.sh: OK (4-shape, 13, 14, 15-sources; 15-suite-green rides tests/suite/solution-suite.test.sh)"
exit 0
