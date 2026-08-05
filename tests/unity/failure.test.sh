#!/usr/bin/env bash
# CM-C3 harness wrapper: the [CI] statics of criteria 1 and 10 (the PlayMode legs run inside
# tests/unity/editmode.test.sh's editor half — one editor invocation for the whole tree).
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
fail() { echo "failure.test.sh: FAIL — $1"; exit 1; }

# criterion 1: no shipped construction of the pinned reason in the render trees...
pin=$(grep -rEn --include='*.cs' 'FailReason\.PlatformOverflow' \
  unity/Assets/Scripts/Presentation unity/Assets/Scripts/Application unity/Assets/Scripts/Bootstrap 2>/dev/null || true)
[ -z "$pin" ] || fail "criterion 1: shipped PlatformOverflow construction: $pin"
# ...with the negative fixture proving the pattern fires.
grep -rEq 'FailReason\.PlatformOverflow' tests/fixtures/retry-bad \
  || fail "criterion 1: pin pattern dead"

# criterion 10: the three LOCKED strings live in ui.csv, zero literals in components.
grep -q 'fail.queueoverflow,Platform overflowed at {node}' unity/Assets/Resources/Strings/ui.csv \
  || fail "criterion 10: queue-overflow row missing"
grep -q 'fail.platformoverflow,{station} platform overflowed' unity/Assets/Resources/Strings/ui.csv \
  || fail "criterion 10: platform-overflow row missing"
grep -q 'fail.banner.timeout,The last train left the depot' unity/Assets/Resources/Strings/ui.csv \
  || fail "criterion 10: timeout row missing"
# review B4: case-INSENSITIVE — the LOCKED overflow string starts with a capital P and the
# old case-sensitive pattern provably let it through (mutation-proven).
lit=$(grep -riEn --include='*.cs' 'platform overflowed|all cats home|last train left' \
  unity/Assets/Scripts 2>/dev/null || true)
[ -z "$lit" ] || fail "criterion 10: literal UI string in a component: $lit"
grep -rEq 'Platform overflowed' tests/fixtures/retry-bad \
  || fail "criterion 10: capital-P literal pattern dead"

echo "failure.test.sh: OK (1-static+fixture, 10-static)"
exit 0
