#!/usr/bin/env bash
# SUITE-GREEN-ASSERTION — the one place the unfiltered .NET suite is proven green.
#
# WHY THIS FILE IS LOAD-BEARING (read before editing)
# ---------------------------------------------------
# Seven wrappers used to each run `dotnet test dotnet/CatMetro.sln` themselves,
# nine invocations in total, because seven contracts each wanted their own
# criterion number pointing at "the suite is green". One run is ~529s / 857
# tests on the reference machine, so ~4,760s of a ~6,860s suite was one command
# repeated. Five of those wrappers never even looked at the output -- they
# referenced it only inside their failure branch and discarded it on success,
# spending 529s to recompute a boolean.
#
# Those five legs are gone, and the two that genuinely needed the output
# (cross-process determinism of REPLAY_HASH and SOLVER_LOG) are merged here so
# a single pair of runs serves both. That is the minimum honest number of
# full-solution runs for this repo: TWO, because "stable across two independent
# processes" cannot be established with one.
#
# The consequence is that suite-green coverage for the WHOLE repository now
# rests on this file alone. Delete it, or let it stop asserting, and every
# other gate still reports green over code that was never run -- the exact
# fail-open class this repo removed once before (see scripts/check.sh
# scan_banned, review F4, cf. commit ee637c9). scripts/check.sh therefore
# guards this file's existence and its SUITE-GREEN-ASSERTION marker, so
# removing it fails the check gate loudly instead of going quietly green.
#
# Set CATMETRO_NO_TEST_CACHE=1 to force both runs to execute for real.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
fail=0

# Fail-closed: a suite that discovers zero tests exits 0 and proves nothing
# (review F1). The sources must be present before the run is allowed to count.
for src in \
  unity/Assets/Tests/EditMode/Pure/Analytics \
  unity/Assets/Tests/EditMode/Pure/Save \
  unity/Assets/Tests/EditMode/Pure/Content/Daily \
  unity/Assets/Tests/EditMode/Pure/Corpus; do
  [ -n "$(ls "$src"/*.cs 2>/dev/null)" ] \
    || { echo "solution-suite: FAIL — NUnit sources missing under $src (fail-closed)"; fail=1; }
done

# Two INDEPENDENT processes. The pair is what makes the determinism claims
# below meaningful, so it must stay a pair.
run_once() {
  dotnet test dotnet/CatMetro.sln -c Release --nologo --logger "console;verbosity=detailed" 2>&1
}

out1="$(run_once)"; rc1=$?
out2="$(run_once)"; rc2=$?

# --- the suite-green assertion itself ---------------------------------------
if [ "$rc1" -ne 0 ] || [ "$rc2" -ne 0 ]; then
  echo "solution-suite: FAIL — dotnet test not green (run1=$rc1 run2=$rc2)"
  printf '%s\n' "$out1" | tail -40
  fail=1
fi

# ...and a non-vacuity guard on top of the exit code: a filter matching zero
# tests also exits 0 (review F1), so a green run must additionally report a
# positive passed count and no failures.
summary="$(printf '%s\n' "$out1" | grep -E '^(Passed|Failed)!' | tail -1)"
passed="$(printf '%s\n' "$summary" | sed -nE 's/.*Passed:[[:space:]]*([0-9]+).*/\1/p')"
failed="$(printf '%s\n' "$summary" | sed -nE 's/.*Failed:[[:space:]]*([0-9]+).*/\1/p')"
if [ -z "${passed:-}" ] || [ "$passed" -le 0 ] 2>/dev/null; then
  echo "solution-suite: FAIL — no positive passed count in the run summary: '${summary:-none}'"
  fail=1
fi
if [ -n "${failed:-}" ] && [ "$failed" -ne 0 ] 2>/dev/null; then
  echo "solution-suite: FAIL — run reported $failed failed test(s)"
  fail=1
fi

# --- cross-process determinism of the replay hash (was CM-C1 crit 4 + 11b) ---
# Count ANCHORED lines only: NUnit's indented "Standard Output Messages" copy
# of the same line never sits at column 0, so a second emitter anywhere in the
# suite fails this wrapper (review F5).
n1="$(printf '%s\n' "$out1" | grep -cE '^REPLAY_HASH=[0-9a-f]{64}$')" || true
n2="$(printf '%s\n' "$out2" | grep -cE '^REPLAY_HASH=[0-9a-f]{64}$')" || true
h1="$(printf '%s\n' "$out1" | grep -E '^REPLAY_HASH=[0-9a-f]{64}$' | head -1)"
h2="$(printf '%s\n' "$out2" | grep -E '^REPLAY_HASH=[0-9a-f]{64}$' | head -1)"
if [ "$n1" -ne 1 ] || [ "$n2" -ne 1 ]; then
  echo "solution-suite: FAIL — expected exactly one REPLAY_HASH per run (run1=$n1 run2=$n2)"
  fail=1
fi
if [ -n "$h1" ] && [ "$h1" != "$h2" ]; then
  echo "solution-suite: FAIL — replay hash differs across independent processes:"
  echo "  run1: $h1"
  echo "  run2: $h2"
  fail=1
fi

# --- cross-process determinism of the optimal log (was CM-C4 crit 7c + 12) ---
s1="$(printf '%s\n' "$out1" | grep -cE '^SOLVER_LOG=([0-9a-f]+|empty)$')" || true
s2="$(printf '%s\n' "$out2" | grep -cE '^SOLVER_LOG=([0-9a-f]+|empty)$')" || true
l1="$(printf '%s\n' "$out1" | grep -E '^SOLVER_LOG=([0-9a-f]+|empty)$' | head -1)"
l2="$(printf '%s\n' "$out2" | grep -E '^SOLVER_LOG=([0-9a-f]+|empty)$' | head -1)"
if [ "$s1" -ne 1 ] || [ "$s2" -ne 1 ]; then
  echo "solution-suite: FAIL — expected exactly one anchored SOLVER_LOG per run (run1=$s1 run2=$s2)"
  fail=1
fi
if [ -n "$l1" ] && [ "$l1" != "$l2" ]; then
  echo "solution-suite: FAIL — optimal log differs across independent processes:"
  echo "  run1: $l1"
  echo "  run2: $l2"
  fail=1
fi

if [ "$fail" -eq 0 ]; then
  echo "solution-suite: OK — suite green ($passed passed), ${h1#REPLAY_HASH=} and ${l1#SOLVER_LOG=} stable across two independent processes"
fi
exit "$fail"
