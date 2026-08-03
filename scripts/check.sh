#!/usr/bin/env bash
# Check gate — stack-agnostic stand-in until the engine lands (TODO(stack): wire real lint+typecheck).
# The interface is permanent (AGENTS.md, CI, and .claude/settings.json all call `bash scripts/check.sh`);
# only the body changes when the stack arrives. Today it verifies what actually exists:
# shell syntax across the harness/tests, and zero unresolved init tokens.
# evals/ is deliberately out of scope — benchmark fixtures fail by design.
# --root <dir>: replaces the default scan roots for the Domain banned-symbol block ONLY
# (CM-C1 criterion 6; used by the negative fixture at tests/fixtures/purity-bad/).
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
fail=0

purity_root=""
while [ $# -gt 0 ]; do
  case "$1" in
    --root) purity_root="${2:-}"; shift 2 ;;
    *) shift ;;
  esac
done

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

# --- Domain purity: banned-symbol scan (CM-C1 criterion 6; ADR-0002 §3,5; ADR-0005:102-106) ---
# *.cs only; word-boundary matches; comments and string literals in scope BY DESIGN (a banned
# symbol named in a comment is still a review signal). The word boundary keeps "floating" and
# "doubled" from matching.
banned_full='\b(UnityEngine|DateTime|DateTimeOffset|Stopwatch|Environment\.TickCount|System\.Random|Guid\.NewGuid|RandomNumberGenerator|float|double|decimal|System\.Numerics)\b'
banned_pure='\b(UnityEngine|UnityEngine\.TestTools)\b'
scan_banned() { # $1 = root dir, $2 = pattern, $3 = label
  [ -d "$1" ] || return 0
  if grep -rEnq --include='*.cs' "$2" "$1" 2>/dev/null; then
    echo "check: FAIL — banned symbol(s) under $1 ($3):"
    grep -rEn --include='*.cs' "$2" "$1" | head -20
    echo "  offending symbols: $(grep -rEoh --include='*.cs' "$2" "$1" | sort -u | tr '\n' ' ')"
    fail=1
  fi
}
if [ -n "$purity_root" ]; then
  scan_banned "$purity_root" "$banned_full" "--root override, full Domain ban list"
else
  scan_banned unity/Assets/Scripts/Domain "$banned_full" "Domain purity, ADR-0002"
  scan_banned unity/Assets/Tests/EditMode/Pure "$banned_pure" "linked test purity, ADR-0005"
fi

[ "$fail" -eq 0 ] && echo "check: OK (interim harness — real lint+typecheck arrive with the stack)"
exit "$fail"
