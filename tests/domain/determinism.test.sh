#!/usr/bin/env bash
# CM-C1 harness wrapper (contract criteria 4 + 11b): discovered by scripts/test.sh as
# tests/**/*.test.sh; exits 0 iff `dotnet test` is green TWICE in independent processes AND both
# runs emit the same single REPLAY_HASH line (regex REPLAY_HASH=[0-9a-f]{64}).
# The golden comparison itself lives inside the NUnit suite (tests/contract/ is human-authored).
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"

run_once() {
  dotnet test dotnet/CatMetro.sln -c Release --nologo --logger "console;verbosity=detailed" 2>&1
}

out1="$(run_once)"; rc1=$?
out2="$(run_once)"; rc2=$?

# Criterion 11(b): count ANCHORED lines (the raw column-0 test-host passthrough), so a second
# emitter anywhere in the suite fails the wrapper (review F5). NUnit's indented "Standard Output
# Messages" copy of the same line is deliberately not counted — it never sits at column 0.
n1="$(printf '%s\n' "$out1" | grep -cE '^REPLAY_HASH=[0-9a-f]{64}$')" || true
n2="$(printf '%s\n' "$out2" | grep -cE '^REPLAY_HASH=[0-9a-f]{64}$')" || true
h1="$(printf '%s\n' "$out1" | grep -E '^REPLAY_HASH=[0-9a-f]{64}$' | head -1)"
h2="$(printf '%s\n' "$out2" | grep -E '^REPLAY_HASH=[0-9a-f]{64}$' | head -1)"

fail=0
if [ "$rc1" -ne 0 ] || [ "$rc2" -ne 0 ]; then
  echo "determinism: FAIL — dotnet test not green (run1=$rc1 run2=$rc2)"
  printf '%s\n' "$out1" | tail -40
  fail=1
fi
if [ "$n1" -ne 1 ] || [ "$n2" -ne 1 ]; then
  echo "determinism: FAIL — expected exactly one distinct REPLAY_HASH value per run (run1=$n1 run2=$n2)"
  fail=1
fi
if [ -n "$h1" ] && [ "$h1" != "$h2" ]; then
  echo "determinism: FAIL — replay hash differs across independent processes:"
  echo "  run1: $h1"
  echo "  run2: $h2"
  fail=1
fi
if [ "$fail" -eq 0 ]; then
  echo "determinism: OK — ${h1#REPLAY_HASH=} stable across two dotnet test processes"
fi
exit "$fail"
