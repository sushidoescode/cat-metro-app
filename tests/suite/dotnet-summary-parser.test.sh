#!/usr/bin/env bash
# Mutation tests for the VSTest summary parser used by solution-suite.test.sh.
# These exercise the parser as a process: no duplicate parsing logic lives here.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"

PARSER="tests/suite/dotnet-summary.py"
pass=0
fail=0

ok()  { pass=$((pass + 1)); echo "  ok   — $1"; }
bad() { fail=$((fail + 1)); echo "  FAIL — $1"; }

accept() {
  local name="$1" expected="$2" payload="$3" actual rc
  actual="$(printf '%s\n' "$payload" | python3 "$PARSER" 2>/dev/null)"; rc=$?
  if [ "$rc" -eq 0 ] && [ "$actual" = "$expected" ]; then
    ok "$name"
  else
    bad "$name: expected '$expected' at rc=0, got '${actual:-empty}' at rc=$rc"
  fi
}

reject() {
  local name="$1" payload="$2" rc
  printf '%s\n' "$payload" | python3 "$PARSER" >/dev/null 2>&1; rc=$?
  if [ "$rc" -ne 0 ]; then
    ok "$name"
  else
    bad "$name: malformed or red output was accepted"
  fi
}

detailed_good=$'NUnit Adapter 4.5.0.0: Test execution complete\nTest Run Successful.\nTotal tests: 5\n     Passed: 5\n Total time: 0.3202 Seconds'
accept "detailed VSTest summary" "5 0 0 5 1" "$detailed_good"

detailed_multiple=$'Test Run Successful.\nTotal tests: 3\n     Passed: 2\n    Skipped: 1\n Total time: 0.1 Seconds\nbuild chatter\nTest Run Successful.\nTotal tests: 4\n     Passed: 4\n Total time: 0.2 Seconds'
accept "multiple test projects are summed" "6 0 1 7 2" "$detailed_multiple"

compact_good=$'Passed!  - Failed:     0, Passed:   915, Skipped:     2, Total:   917, Duration: 9 m 2 s'
accept "compact VSTest summary remains supported" "915 0 2 917 1" "$compact_good"

reject "zero discovered tests" $'Test Run Successful.\nTotal tests: 0\n Total time: 0.1 Seconds'
reject "success prose without counts" $'Test Run Successful.\nEverything looks fine'
reject "counts without a runner result" $'Total tests: 5\n     Passed: 5'
reject "explicit failed test" $'Test Run Failed.\nTotal tests: 5\n     Failed: 1\n     Passed: 4\n Total time: 0.2 Seconds'
reject "failed compact run" $'Failed!  - Failed:     1, Passed:   4, Skipped:     0, Total:   5, Duration: 1 s'
reject "inconsistent total" $'Test Run Successful.\nTotal tests: 5\n     Passed: 4\n Total time: 0.2 Seconds'
reject "duplicate passed metric" $'Test Run Successful.\nTotal tests: 5\n     Passed: 5\n     Passed: 5\n Total time: 0.2 Seconds'
reject "skipped-only run is vacuous" $'Test Run Successful.\nTotal tests: 2\n    Skipped: 2\n Total time: 0.2 Seconds'
reject "mixed detailed and compact summaries" $'Test Run Successful.\nTotal tests: 5\n     Passed: 5\nPassed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 1 s'

if [ "$fail" -eq 0 ]; then
  echo "dotnet-summary-parser: OK — $pass parser mutations passed"
fi
exit "$fail"
