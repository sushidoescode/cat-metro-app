#!/usr/bin/env bash
# CM-C2a harness wrapper (criterion 13): exits 0 iff `dotnet test` is green AND the [CI] greps
# hold: exactly one `new JsonSerializerSettings` construction in the tree (criterion 4), and no
# bare distinctive bound literal (262144|4000|70|40|30|12) outside ContentBounds.cs under
# unity/Assets/Scripts/Content/** EXCEPT Content/Validation/** and Content/Daily/** (criterion 5 —
# the EXCEPT roots mirror the ownership table so a correct CM-C5/C6 diff cannot break this).
# Criterion 13's scripts/test.sh summary-numbers comparison is the PR evidence procedure
# (handoff A-C2a-10) — this wrapper cannot invoke scripts/test.sh without recursing.
set -uo pipefail
full_solution_cache=${CAT_METRO_FULL_SOLUTION_CACHE_DIR-}
full_solution_artifacts=${CAT_METRO_FULL_SOLUTION_ARTIFACT_DIR-}
unset CAT_METRO_FULL_SOLUTION_CACHE_DIR CAT_METRO_FULL_SOLUTION_ARTIFACT_DIR
cd "$(git rev-parse --show-toplevel)"
fail=0

out="$(CAT_METRO_FULL_SOLUTION_CACHE_DIR="$full_solution_cache" CAT_METRO_FULL_SOLUTION_ARTIFACT_DIR="$full_solution_artifacts" python3 scripts/run-full-solution-test.py 2>&1)"; rc=$?
if [ "$rc" -ne 0 ]; then
  echo "importer: FAIL — dotnet test not green (rc=$rc)"
  printf '%s\n' "$out" | tail -20
  fail=1
fi

# Review F8: the criterion says "the tree" — scan all of unity/Assets (tests included), and
# count occurrences, not matching lines.
settings_count="$(grep -ro --include='*.cs' 'new JsonSerializerSettings' unity/Assets 2>/dev/null | wc -l | tr -d ' ')"
if [ "$settings_count" -ne 1 ]; then
  echo "importer: FAIL — expected exactly 1 'new JsonSerializerSettings' under unity/Assets, found $settings_count"
  fail=1
fi

lit="$(grep -rEn --include='*.cs' '\b(262144|4000|70|40|30|12)\b' unity/Assets/Scripts/Content 2>/dev/null | grep -v 'ContentBounds\.cs' | grep -v 'Content/Validation/' | grep -v 'Content/Daily/' || true)"
if [ -n "$lit" ]; then
  echo "importer: FAIL — bare distinctive bound literal outside ContentBounds (criterion 5):"
  echo "$lit"
  fail=1
fi

[ "$fail" -eq 0 ] && echo "importer: OK — suite green, one settings site, no stray bound literals"
exit "$fail"
