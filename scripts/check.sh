#!/usr/bin/env bash
# Check gate — stack-agnostic stand-in until the engine lands (TODO(stack): wire real lint+typecheck).
# The interface is permanent (AGENTS.md, CI, and .claude/settings.json all call `bash scripts/check.sh`);
# only the body changes when the stack arrives. Today it verifies what actually exists:
# shell syntax across the harness/tests, and zero unresolved init tokens.
# evals/ is deliberately out of scope — benchmark fixtures fail by design.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
fail=0

while IFS= read -r f; do
  if ! bash -n "$f" 2>/dev/null; then
    echo "check: FAIL — shell syntax error in $f"
    bash -n "$f"
    fail=1
  fi
done < <(find scripts tests -name '*.sh' -type f 2>/dev/null)

# Token pattern assembled by concatenation so this file never matches itself (same trick as forge-doctor).
tok='[A-Z][A-Z0-9_]*'
if grep -rEq '\{\{'"$tok" --exclude-dir=.git --exclude-dir=node_modules --exclude-dir=Library . 2>/dev/null; then
  echo "check: FAIL — unresolved init tokens remain:"
  grep -rEn '\{\{'"$tok" --exclude-dir=.git --exclude-dir=node_modules --exclude-dir=Library .
  fail=1
fi

[ "$fail" -eq 0 ] && echo "check: OK (interim harness — real lint+typecheck arrive with the stack)"
exit "$fail"
