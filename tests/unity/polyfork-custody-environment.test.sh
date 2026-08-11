#!/usr/bin/env bash
# ADR-0011 regression: inherited Git redirect state must not substitute a different repository,
# index, ref namespace, object database, or pathspec policy for the custody scan.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

fail() { echo "polyfork-custody-environment.test.sh: FAIL — $*" >&2; exit 1; }

for variable in \
  GIT_DIR \
  GIT_WORK_TREE \
  GIT_INDEX_FILE \
  GIT_OBJECT_DIRECTORY \
  GIT_ALTERNATE_OBJECT_DIRECTORIES \
  GIT_COMMON_DIR \
  GIT_NAMESPACE \
  GIT_SHALLOW_FILE \
  GIT_REPLACE_REF_BASE \
  GIT_GRAFT_FILE \
  GIT_CONFIG_GLOBAL \
  GIT_CONFIG_SYSTEM \
  GIT_CONFIG_NOSYSTEM \
  GIT_CONFIG_PARAMETERS \
  GIT_CONFIG_COUNT \
  GIT_LITERAL_PATHSPECS \
  GIT_GLOB_PATHSPECS \
  GIT_NOGLOB_PATHSPECS \
  GIT_ICASE_PATHSPECS
do
  if output=$(env "$variable=/private/tmp/catmetro-untrusted-git-redirect" \
      bash tests/unity/polyfork-custody.test.sh 2>&1); then
    fail "custody gate accepted inherited $variable"
  fi
  echo "$output" | grep -q 'refusing inherited Git redirect/configuration state' \
    || fail "custody gate did not fail $variable at the inherited-environment boundary"
done

redirect_repo=$(mktemp -d "${TMPDIR:-/tmp}/catmetro-git-redirect-repo.XXXXXX")
cleanup_redirect_repo() {
  command rm -rf -- "$redirect_repo"
}
trap cleanup_redirect_repo EXIT HUP INT TERM
git init -q "$redirect_repo"
real_root=$(pwd -P)
for entrypoint in scripts/test.sh scripts/build.sh scripts/run-unity-editmode.sh; do
  if output=$(GIT_DIR="$redirect_repo/.git" GIT_WORK_TREE="$redirect_repo" \
      bash "$entrypoint" 2>&1); then
    fail "$entrypoint accepted a substituted empty repository"
  fi
  echo "$output" | grep -q 'refusing inherited Git redirect/configuration state' \
    || fail "$entrypoint did not reject Git redirection before repository discovery"
  if echo "$output" | grep -q 'no tests yet'; then
    fail "$entrypoint returned the empty-repository false green"
  fi
done
for entrypoint in scripts/test.sh scripts/build.sh scripts/run-unity-editmode.sh; do
  if output=$(cd "$redirect_repo" && bash "$real_root/$entrypoint" 2>&1); then
    fail "$entrypoint accepted invocation from an unrelated repository"
  fi
  echo "$output" | grep -q 'must be invoked from its checkout root' \
    || fail "$entrypoint did not reject the unrelated working directory"
  if echo "$output" | grep -q 'no tests yet'; then
    fail "$entrypoint returned the unrelated-repository false green"
  fi
done

echo "polyfork-custody-environment.test.sh: OK"
