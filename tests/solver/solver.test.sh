#!/usr/bin/env bash
# CM-C4 harness wrapper (criteria 7c + 12): exits 0 iff `dotnet test` is green TWICE in
# independent processes AND both runs emit exactly one anchored SOLVER_LOG line with identical
# values (cross-process determinism of the optimal command log).
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
fail=0

run_once() {
  dotnet test dotnet/CatMetro.sln -c Release --nologo --logger "console;verbosity=detailed" 2>&1
}

out1="$(run_once)"; rc1=$?
out2="$(run_once)"; rc2=$?

n1="$(printf '%s\n' "$out1" | grep -cE '^SOLVER_LOG=([0-9a-f]+|empty)$')" || true
n2="$(printf '%s\n' "$out2" | grep -cE '^SOLVER_LOG=([0-9a-f]+|empty)$')" || true
l1="$(printf '%s\n' "$out1" | grep -E '^SOLVER_LOG=([0-9a-f]+|empty)$' | head -1)"
l2="$(printf '%s\n' "$out2" | grep -E '^SOLVER_LOG=([0-9a-f]+|empty)$' | head -1)"

if [ "$rc1" -ne 0 ] || [ "$rc2" -ne 0 ]; then
  echo "solver: FAIL — dotnet test not green (run1=$rc1 run2=$rc2)"
  printf '%s\n' "$out1" | tail -25
  fail=1
fi
if [ "$n1" -ne 1 ] || [ "$n2" -ne 1 ]; then
  echo "solver: FAIL — expected exactly one anchored SOLVER_LOG line per run (run1=$n1 run2=$n2)"
  fail=1
fi
if [ -n "$l1" ] && [ "$l1" != "$l2" ]; then
  echo "solver: FAIL — optimal log differs across independent processes:"
  echo "  run1: $l1"
  echo "  run2: $l2"
  fail=1
fi
if [ "$fail" -eq 0 ]; then
  echo "solver: OK — ${l1#SOLVER_LOG=} stable across two dotnet test processes"
fi
exit "$fail"
