#!/usr/bin/env bash
# ADR-0011 regression: a forbidden payload reachable from any fetched ref must fail custody even
# when it is not reachable from the candidate HEAD.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

fail() { echo "polyfork-custody-any-ref.test.sh: FAIL — $*" >&2; exit 1; }

fixture_dir=$(mktemp -d "${TMPDIR:-/tmp}/catmetro-custody-any-ref.XXXXXX")
payload="$fixture_dir/public-payload.zip"
test_ref="refs/remotes/custody-fixture/public-payload-$$"
cleanup() {
  git update-ref -d "$test_ref" >/dev/null 2>&1 || true
  [ ! -f "$payload" ] || command unlink "$payload"
  [ ! -d "$fixture_dir" ] || rmdir "$fixture_dir"
}
trap cleanup EXIT HUP INT TERM

printf 'PK\003\004cat-metro-custody-fixture\n' > "$payload"
blob=$(git hash-object -w "$payload")
tree=$(printf '100644 blob %s\tpublic-payload.zip\n' "$blob" | git mktree)
commit=$(printf 'custody any-ref fixture\n' | \
  GIT_AUTHOR_NAME='Cat Metro Test' GIT_AUTHOR_EMAIL='test@invalid' \
  GIT_COMMITTER_NAME='Cat Metro Test' GIT_COMMITTER_EMAIL='test@invalid' \
  git commit-tree "$tree")
git update-ref "$test_ref" "$commit"

if output=$(CM_REQUIRE_POLYFORK_LOCAL=0 bash tests/unity/polyfork-custody.test.sh 2>&1); then
  fail "custody scanner accepted a standalone archive reachable from another ref"
fi
echo "$output" | grep -q 'standalone model/archive payload' \
  || fail "custody scanner failed without identifying the cross-ref payload"

echo "polyfork-custody-any-ref.test.sh: OK"
