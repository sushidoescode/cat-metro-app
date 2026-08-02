#!/usr/bin/env bash
# Test gate — runs every tests/**/*.test.sh; a test passes iff it exits 0.
# Stack-agnostic until the engine lands (TODO(stack): route to the engine's test runner here).
# evals/ is deliberately NOT a test source: benchmark fixtures fail by design.
# No tests found = green with a notice ("no tests yet"), so CI executes from day one.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
found=0; failed=0

while IFS= read -r t; do
  found=$((found+1))
  if bash "$t"; then
    echo "PASS $t"
  else
    echo "FAIL $t"
    failed=$((failed+1))
  fi
done < <(find tests -name '*.test.sh' -type f 2>/dev/null | sort)

if [ "$found" -eq 0 ]; then
  echo "test: no tests yet (add tests/**/*.test.sh)"
  exit 0
fi
echo "test: $((found-failed))/$found passed"
[ "$failed" -eq 0 ]
