#!/usr/bin/env bash
# CM-C8 harness wrapper (criterion 12): exits 0 iff `dotnet test` is green AND the [CI] greps of
# criteria 1, 2, 7 and 10 hold. Fail-closed on missing scan roots; each check labelled. The
# scripts/test.sh summary-numbers comparison is the PR evidence procedure (CM-C2a c13 precedent).
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
tmp="${TMPDIR:-/tmp}/cm-c8-wrapper-$$"
mkdir -p "$tmp"
trap 'rm -rf "$tmp"' EXIT
fail() { echo "queue.test.sh: FAIL — $1"; exit 1; }
an_root="unity/Assets/Scripts/Application/Analytics"

[ -d "$an_root" ] || fail "scan root $an_root does not exist (fail-closed)"
[ -n "$(ls unity/Assets/Tests/EditMode/Pure/Analytics/*.cs 2>/dev/null)" ] \
  || fail "criterion 12: Analytics NUnit sources missing (fail-closed)"

# Criterion 12: the dotnet leg is green — full suite, unfiltered (CM-C6 review F1 precedent).
if ! dotnet test dotnet/CatMetro.sln -c Release --nologo > "$tmp/test.out" 2>&1; then
  tail -20 "$tmp/test.out"
  fail "criterion 12: dotnet test not green"
fi

# Criterion 1: ONE write-path implementation — the queue names no file stream of its own; all
# disk access rides the CM-C7 seam. Comments stripped first (CM-C7 review F3 precedent).
dec() { sed 's|//.*||' "$1"; }
writepath='\bFileStream\b|\bStreamWriter\b|\bStreamReader\b|\bFile\.(Replace|Move|WriteAll|ReadAll|Copy|Open|Create|Append|Delete)'
for f in "$an_root"/*.cs; do
  if dec "$f" | grep -qE "$writepath"; then
    fail "criterion 1: a second write-path implementation in $f"
  fi
done
# ...with the pattern proven LIVE (review S1: the old trailing-\b form missed WriteAllBytes).
grep -rEq "$writepath" tests/fixtures/analytics-bad || fail "criterion 1: write-path pattern dead"


# Criterion 2: the five bound literals are hard-coded in no Analytics source (config rows only).
lit=$(grep -rEn --include='*.cs' '\b(2000|1048576|512|64)\b' "$an_root" 2>/dev/null || true)
[ -z "$lit" ] || fail "criterion 2: hard-coded queue bound literal: $lit"

# Criterion 7: metrics-only — no CM-C7 state type reachable from the queue sources...
state_guard='\b(ConsumableLedger|SaveStore|SaveState|ISave|SaveDefaults|SaveEventRecord|MigrationTable|RealSaveFileSystem|LoadResult)\b'
hits=$(grep -rEn --include='*.cs' "$state_guard" "$an_root" 2>/dev/null || true)
[ -z "$hits" ] || fail "criterion 7: CM-C7 state type referenced from the queue: $hits"

# Criterion 10: ...and no SDK namespace anywhere this contract writes.
sdk_guard='Firebase|OneSignalSDK|GoogleMobileAds|RevenueCat'
hits=$(grep -rEn "$sdk_guard" "$an_root" unity/Assets/Scripts/Services/Analytics 2>/dev/null || true)
[ -z "$hits" ] || fail "criterion 10: SDK token: $hits"
# ...with the negative fixture proving both patterns fire.
grep -rEq "$state_guard" tests/fixtures/analytics-bad || fail "criterion 7: state pattern dead"
grep -rEq "$sdk_guard" tests/fixtures/analytics-bad || fail "criterion 10: SDK pattern dead"

# Criterion 11 (grep half, review S2): no code path under the queue names the save file —
# the two writes can never be one operation if the queue cannot even address save.dat.
hits=$(grep -rEn --include='*.cs' 'save\.dat|SavePath' "$an_root" 2>/dev/null || true)
[ -z "$hits" ] || fail "criterion 11: the queue names the save file: $hits"
grep -rEq 'save\.dat' tests/fixtures/analytics-bad || fail "criterion 11: save-file pattern dead"

echo "queue.test.sh: OK (1-shape+fixture, 2, 7, 10, 11-grep, 12)"
exit 0
