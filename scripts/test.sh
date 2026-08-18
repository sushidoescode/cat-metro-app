#!/usr/bin/env bash
# Test gate — runs every tests/**/*.test.sh; a test passes iff it exits 0.
# Stack-agnostic until the engine lands (TODO(stack): route to the engine's test runner here).
# evals/ is deliberately NOT a test source: benchmark fixtures fail by design.
# No tests found = green with a notice ("no tests yet"), so CI executes from day one.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
found=0; failed=0

# Lane E: the run-local full-solution attestation is part of the harness, but not another
# product wrapper. Keep the tests/**/*.test.sh discovery/pass census byte-for-byte comparable.
if ! bash scripts/selftest/full-solution-cache.selftest.sh; then
  echo "test: FAIL — full-solution cache self-test"
  exit 1
fi

unset CAT_METRO_FULL_SOLUTION_CACHE_DIR
unset CAT_METRO_FULL_SOLUTION_CACHE_ACTIVE
unset CAT_METRO_FULL_SOLUTION_ARTIFACT_DIR
repo_root=$(pwd -P)
artifact_project="$repo_root/dotnet/CatMetro.Tests"
artifact_obj="$artifact_project/obj"
artifact_parent="$artifact_obj/ci-full-solution"

# The isolated output must remain below the repo: several NUnit fixtures discover repo-root by
# walking upward from VSTest's output/current directory. Refuse symlinked or non-ignored paths.
[ -d "$artifact_project" ] && [ ! -L "$artifact_project" ] || {
  echo "test: FAIL — full-solution artifact project is not a real directory"
  exit 1
}
if [ -e "$artifact_obj" ] || [ -L "$artifact_obj" ]; then
  [ -d "$artifact_obj" ] && [ ! -L "$artifact_obj" ] || {
    echo "test: FAIL — full-solution obj path is not a real directory"
    exit 1
  }
else
  mkdir "$artifact_obj" || {
    echo "test: FAIL — could not create full-solution obj directory"
    exit 1
  }
fi
git check-ignore -q -- "$artifact_parent/.ignore-probe" || {
  echo "test: FAIL — full-solution session path is not gitignored"
  exit 1
}
if [ -e "$artifact_parent" ] || [ -L "$artifact_parent" ]; then
  [ -d "$artifact_parent" ] && [ ! -L "$artifact_parent" ] || {
    echo "test: FAIL — full-solution session parent is not a real directory"
    exit 1
  }
else
  mkdir "$artifact_parent" || {
    echo "test: FAIL — could not create full-solution session parent"
    exit 1
  }
fi

session_dir=$(mktemp -d "$artifact_parent/session.XXXXXX") || {
  echo "test: FAIL — could not create full-solution session"
  exit 1
}
[ -n "$session_dir" ] && [ -d "$session_dir" ] && [ ! -L "$session_dir" ] || {
  echo "test: FAIL — mktemp returned an invalid full-solution session"
  exit 1
}
chmod 700 "$session_dir" || {
  echo "test: FAIL — could not make full-solution session private"
  exit 1
}
cache_dir="$session_dir/cache"
artifact_dir="$session_dir/artifacts"
mkdir -m 700 "$cache_dir" || {
  echo "test: FAIL — could not create private full-solution cache"
  exit 1
}

cleanup_full_solution_session() {
  if [ -n "${session_dir:-}" ]; then
    python3 "$repo_root/scripts/run-full-solution-test.py" \
      --cleanup-session "$session_dir" 2>&1 \
      || echo "test: WARN — private full-solution session cleanup was refused" >&2
  fi
}

finish_full_solution_session() {
  rc=$?
  trap - EXIT HUP INT TERM
  cleanup_full_solution_session
  exit "$rc"
}

interrupt_full_solution_session() {
  rc=$1
  trap - EXIT HUP INT TERM
  cleanup_full_solution_session
  exit "$rc"
}

trap finish_full_solution_session EXIT
trap 'interrupt_full_solution_session 129' HUP
trap 'interrupt_full_solution_session 130' INT
trap 'interrupt_full_solution_session 143' TERM

run_wrapper() {
  case "$1" in
    tests/analytics/queue.test.sh|tests/content/importer.test.sh|tests/daily/daily-pipeline.test.sh|tests/save/save.test.sh|tests/taxonomy/taxonomy.test.sh)
      CAT_METRO_FULL_SOLUTION_CACHE_DIR="$cache_dir" \
        CAT_METRO_FULL_SOLUTION_CACHE_ACTIVE="$session_dir" \
        CAT_METRO_FULL_SOLUTION_ARTIFACT_DIR="$artifact_dir" \
        bash "$1"
      ;;
    *)
      env -u CAT_METRO_FULL_SOLUTION_CACHE_DIR \
        -u CAT_METRO_FULL_SOLUTION_CACHE_ACTIVE \
        -u CAT_METRO_FULL_SOLUTION_ARTIFACT_DIR \
        bash "$1"
      ;;
  esac
}

while IFS= read -r t; do
  found=$((found+1))
  if run_wrapper "$t"; then
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
