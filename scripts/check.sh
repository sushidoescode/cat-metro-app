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
    --root) purity_root="${2:?check.sh: --root requires a directory argument}"; shift; shift ;;
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
scan_banned() { # $1 = root dir, $2 = pattern, $3 = label — FAIL-CLOSED on a missing root:
  # a purity gate that reports OK after scanning nothing is the fail-open pattern this repo
  # already removed once (review F4; cf. commit ee637c9).
  if [ ! -d "$1" ]; then
    echo "check: FAIL — banned-symbol scan root '$1' does not exist ($3)"
    fail=1
    return 0
  fi
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

# --- CM-C2a: Content receives bytes + single serializer-settings site (criteria 2 & 4; ADR-0008 MUST 1) ---
# Content is byte-fed through IContentSource (ADR-0008:53-56): zero UnityEngine, zero System.IO.
# TypeNameHandling may appear ONLY at the single settings site — the exception is a PATH, so later
# contracts satisfy this block without editing it. --root scans the union (handoff A-C2a-11).
content_banned='\b(UnityEngine|System\.IO)\b'
tnh_scan() { # $1 = scan root; fail-closed like scan_banned
  if [ ! -d "$1" ]; then
    echo "check: FAIL — TypeNameHandling scan root '$1' does not exist"
    fail=1
    return 0
  fi
  local tnh_hits
  tnh_hits=$(grep -rln --include='*.cs' 'TypeNameHandling' "$1" 2>/dev/null | grep -v 'unity/Assets/Scripts/Content/ContentJson\.cs$' || true)
  if [ -n "$tnh_hits" ]; then
    echo "check: FAIL — TypeNameHandling outside the single settings site (ADR-0008 MUST 1):"
    echo "$tnh_hits"
    fail=1
  fi
}
if [ -n "$purity_root" ]; then
  scan_banned "$purity_root" "$content_banned" "--root override, Content ban list"
  tnh_scan "$purity_root"
else
  scan_banned unity/Assets/Scripts/Content "$content_banned" "Content purity, ADR-0008"
  tnh_scan unity/Assets/Scripts
fi

[ "$fail" -eq 0 ] && echo "check: OK (interim harness — real lint+typecheck arrive with the stack)"
exit "$fail"
