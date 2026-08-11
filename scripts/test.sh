#!/usr/bin/env bash
# Test gate — runs every tests/**/*.test.sh; a test passes iff it exits 0.
# Stack-agnostic until the engine lands (TODO(stack): route to the engine's test runner here).
# evals/ is deliberately NOT a test source: benchmark fixtures fail by design.
# No tests found = green with a notice ("no tests yet"), so CI executes from day one.
set -uo pipefail
if [ "${POLYFORK_KEY+x}" = "x" ]; then
  echo "test: refusing inherited POLYFORK_KEY before test subprocesses" >&2
  exit 2
fi
if [ "${CM_UNITY_EDITOR+x}" = "x" ]; then
  echo "test: CM_UNITY_EDITOR overrides are forbidden; licensed-local execution authenticates the pinned editor" >&2
  exit 2
fi
test_script_dir="${BASH_SOURCE[0]%/*}"
[ "$test_script_dir" != "${BASH_SOURCE[0]}" ] || test_script_dir=.
. "$test_script_dir/reject-git-redirect-env.sh"
catmetro_reject_git_redirect_env "test" || exit 2
catmetro_require_checkout_root "$test_script_dir/.." "test" || exit 2
unity_profile="${CM_REQUIRE_POLYFORK_LOCAL:-0}"
case "$unity_profile" in
  0|1) ;;
  *)
    echo "test: CM_REQUIRE_POLYFORK_LOCAL must be 0 or 1" >&2
    exit 2
    ;;
esac
unset CM_REQUIRE_POLYFORK_LOCAL
found=0; failed=0

while IFS= read -r t; do
  found=$((found+1))
  if [ "$t" = "tests/unity/editmode.test.sh" ]; then
    CM_REQUIRE_POLYFORK_LOCAL="$unity_profile" bash scripts/run-unity-editmode.sh
    test_status=$?
  else
    bash "$t"
    test_status=$?
  fi
  if [ "$test_status" -eq 0 ]; then
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
