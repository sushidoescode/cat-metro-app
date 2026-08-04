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

# --- CM-C4: solver discipline (criteria 2, 8, 13) ---
# The solver may only reach state through the ONE Step symbol: no tick-advancing writes, no
# score/chain reads (pins NEW-Q5/NEW-Q7), and no runtime tree may reference solver types
# (CM-R01.6 guard — placement inside Domain removed the assembly boundary, Q-M cost c).
# Review M2: compound assignments (+=, -=, *=, /=) are writes too; == stays excluded.
solver_writes='(\.Tick[[:space:]]*(\+\+|[-+*/]?=[^=])|Deliveries[[:space:]]*(\+\+|[-+*/]?=[^=])|OverloadTimers\[)'
solver_scores='\.(Score|Chain)\b'
solver_ref='CatMetro\.Domain\.Solver'
if [ -n "$purity_root" ]; then
  scan_banned "$purity_root" "$solver_writes" "--root override, solver tick-write ban"
  scan_banned "$purity_root" "$solver_scores" "--root override, solver score-read ban"
  scan_banned "$purity_root" "$solver_ref" "--root override, runtime-reference guard (review H2)"
  if grep -rEnq --include='*.cs' 'static void Step\(ref SimulationState' "$purity_root" 2>/dev/null; then
    echo "check: FAIL — a second Step definition under $purity_root (ADR-0002 §2: exactly one)"
    fail=1
  fi
else
  # Review M1: unconditional — scan_banned fails closed on a missing root, which is exactly
  # what must happen if the solver directory is ever moved without updating this gate.
  scan_banned unity/Assets/Scripts/Domain/Solver "$solver_writes" "solver reaches state only through Step, CM-R02.1"
  scan_banned unity/Assets/Scripts/Domain/Solver "$solver_scores" "scoring pinned out of the solver, NEW-Q5/NEW-Q7"
  step_defs=$(grep -rEo --include='*.cs' 'static void Step\(ref SimulationState' unity/Assets/Scripts 2>/dev/null | wc -l | tr -d ' ')
  if [ "$step_defs" -ne 1 ]; then
    echo "check: FAIL — expected exactly 1 'static void Step(ref SimulationState' definition, found $step_defs (ADR-0002 §2)"
    fail=1
  fi
  for runtime_root in unity/Assets/Scripts/Application unity/Assets/Scripts/Presentation unity/Assets/Scripts/Bootstrap; do
    # These trees are empty today; the guard arms itself the day they exist (contract crit 13).
    if [ -d "$runtime_root" ] && grep -rEnq --include='*.cs' "$solver_ref" "$runtime_root" 2>/dev/null; then
      echo "check: FAIL — runtime tree $runtime_root references solver types (CM-R01.6 guard):"
      grep -rEn --include='*.cs' "$solver_ref" "$runtime_root" | head -5
      fail=1
    fi
  done
fi

# --- CM-C6: the Daily root is clock-free (criterion 2) ---
# Date keys are INPUTS (yyyy-MM-dd strings) — never read from a clock. The two time types and
# the clock-seam interface may not appear under the Daily root even in prose. Scoped to
# Content/Daily ONLY: Content/Validation's staleness stage legally uses a time type OUTSIDE it
# (CM-C5 A-C5-4), and the Domain block above already covers the sim tree.
daily_banned='\b(DateTime|DateTimeOffset|IClock)\b'
if [ -n "$purity_root" ]; then
  scan_banned "$purity_root" "$daily_banned" "--root override, Daily clock ban (CM-C6)"
else
  scan_banned unity/Assets/Scripts/Content/Daily "$daily_banned" "Daily clock ban, CM-C6"
fi

[ "$fail" -eq 0 ] && echo "check: OK (interim harness — real lint+typecheck arrive with the stack)"
exit "$fail"
