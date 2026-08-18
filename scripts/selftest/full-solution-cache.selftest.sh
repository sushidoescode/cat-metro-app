#!/usr/bin/env bash
# Lane E harness self-test. It is called explicitly by scripts/test.sh and deliberately lives
# outside tests/**/*.test.sh so the product-wrapper census remains unchanged.
set -uo pipefail

repo_root=$(git rev-parse --show-toplevel 2>/dev/null) || {
  echo "full-solution-cache self-test: FAIL — not in a git worktree"
  exit 1
}
cd "$repo_root" || exit 1

fail() {
  echo "full-solution-cache self-test: FAIL — $1"
  exit 1
}

helper="$repo_root/scripts/run-full-solution-test.py"
[ -f "$helper" ] || fail "cache helper is missing (expected RED before implementation)"

tmp_parent_raw=${TMPDIR:-/tmp}
[ -d "$tmp_parent_raw" ] || fail "temporary parent does not exist: $tmp_parent_raw"
tmp_parent=$(cd "$tmp_parent_raw" 2>/dev/null && pwd -P) \
  || fail "could not resolve temporary parent"
tmp=$(mktemp -d "$tmp_parent/cm-full-solution-cache-selftest.XXXXXX") \
  || fail "mktemp failed (run this gate unsandboxed)"
[ -n "$tmp" ] && [ -d "$tmp" ] && [ ! -L "$tmp" ] \
  || fail "mktemp returned an invalid directory"
case "$tmp" in
  "$tmp_parent"/cm-full-solution-cache-selftest.*) ;;
  *) fail "temporary directory escaped its validated parent: $tmp" ;;
esac

cleanup() {
  rc=$?
  if [ -n "${tmp:-}" ] && [ -d "$tmp" ] && [ ! -L "$tmp" ]; then
    case "$tmp" in
      "$tmp_parent"/cm-full-solution-cache-selftest.*) rm -rf -- "$tmp" ;;
    esac
  fi
  exit "$rc"
}
trap cleanup EXIT HUP INT TERM

fixture="$tmp/repo"
fake_bin="$tmp/fake-bin"
cache="$tmp/cache"
cache_mutate="$tmp/cache-mutate"
calls="$tmp/dotnet.calls"
mkdir -p "$fixture/dotnet" "$fixture/unity/Assets/Scripts/Domain" "$fake_bin" \
  || fail "could not create self-test fixture"
mkdir -m 700 "$cache" "$cache_mutate" || fail "could not create private caches"

printf '%s\n' 'Microsoft Visual Studio Solution File, Format Version 12.00' \
  > "$fixture/dotnet/CatMetro.sln"
printf '%s\n' 'PASS' > "$fixture/unity/Assets/Scripts/Domain/Fingerprint.cs"
printf '%s\n' 'dotnet/**/obj/' > "$fixture/.gitignore"

cat > "$fake_bin/dotnet" <<'FAKE_DOTNET'
#!/usr/bin/env bash
set -uo pipefail

if [ "$#" -eq 1 ] && [ "$1" = "--info" ]; then
  [ -z "${CAT_METRO_FULL_SOLUTION_CACHE_DIR:-}" ] || exit 91
  printf '%s\n' '.NET SDK: fake-8.0.419' 'RID: fake-portable'
  exit 0
fi

log=${FAKE_DOTNET_LOG:?}
{
  printf 'CALL'
  for arg in "$@"; do
    printf '\t%s' "$arg"
  done
  printf '\n'
} >> "$log"

kind=''
if [ "$#" -eq 5 ] \
  && [ "$1" = 'test' ] && [ "$2" = 'dotnet/CatMetro.sln' ] \
  && [ "$3" = '-c' ] && [ "$4" = 'Release' ] && [ "$5" = '--nologo' ]; then
  kind='direct-standard'
elif [ "$#" -eq 7 ] \
  && [ "$1" = 'test' ] && [ "$2" = 'dotnet/CatMetro.sln' ] \
  && [ "$3" = '-c' ] && [ "$4" = 'Release' ] && [ "$5" = '--nologo' ] \
  && [ "$6" = '--artifacts-path' ]; then
  case "$7" in
    "${FAKE_ARTIFACT_ROOT:?}"/*) kind='cached-standard' ;;
    *) exit 92 ;;
  esac
elif [ "$#" -eq 7 ] \
  && [ "$1" = 'test' ] && [ "$2" = 'dotnet/CatMetro.sln' ] \
  && [ "$3" = '-c' ] && [ "$4" = 'Release' ] && [ "$5" = '--nologo' ] \
  && [ "$6" = '--logger' ] && [ "$7" = 'console;verbosity=detailed' ]; then
  kind='protected-detailed'
else
  printf 'fake dotnet: unexpected argv\n' >&2
  exit 90
fi

if [ "$kind" = 'cached-standard' ] \
  && [ -n "${CAT_METRO_FULL_SOLUTION_CACHE_DIR:-}" ]; then
  printf 'fake dotnet: helper control variable leaked to child\n' >&2
  exit 91
fi

state='PASS'
fingerprint='unity/Assets/Scripts/Domain/Fingerprint.cs'
if [ -f "$fingerprint" ]; then
  state=$(tr -d '\r\n' < "$fingerprint")
fi
case "$state" in
  FAIL)
    printf 'FAKE_FAILURE\n' >&2
    exit 42
    ;;
  MUTATE)
    printf '%s\n' 'DONE' > "$fingerprint"
    ;;
esac

run=$(wc -l < "$log" | tr -d ' ')
printf 'FAKE_STDOUT run=%s kind=%s\n' "$run" "$kind"
printf 'REPLAY_HASH=%064d\n' 0
printf 'SOLVER_LOG=abcd\n'
printf 'FAKE_STDERR run=%s kind=%s\n' "$run" "$kind" >&2
exit 0
FAKE_DOTNET
chmod 700 "$fake_bin/dotnet" || fail "could not make fake dotnet executable"

git -C "$fixture" init -q || fail "could not initialize fixture repository"
git -C "$fixture" add .gitignore dotnet/CatMetro.sln unity/Assets/Scripts/Domain/Fingerprint.cs \
  || fail "could not stage fixture inputs"
git -C "$fixture" -c user.name='CI self-test' -c user.email='ci-selftest@example.invalid' \
  commit -qm 'fixture' || fail "could not commit fixture inputs"

reset_calls() {
  : > "$calls"
}

call_count() {
  wc -l < "$calls" | tr -d ' '
}

run_direct() {
  (
    cd "$fixture" || exit 1
    unset CAT_METRO_FULL_SOLUTION_CACHE_DIR
    PATH="$fake_bin:$PATH" \
      FAKE_DOTNET_LOG="$calls" \
      FAKE_ARTIFACT_ROOT="$fixture/dotnet/CatMetro.Tests/obj/ci-full-solution" \
      CACHE_SECRET_SENTINEL='do-not-store-this-raw-value' \
      python3 "$helper"
  )
}

run_cached_at() {
  selected_cache=$1
  variant=${2:-stable}
  (
    cd "$fixture" || exit 1
    PATH="$fake_bin:$PATH" \
      FAKE_DOTNET_LOG="$calls" \
      FAKE_ARTIFACT_ROOT="$fixture/dotnet/CatMetro.Tests/obj/ci-full-solution" \
      CACHE_TEST_VARIANT="$variant" \
      CACHE_SECRET_SENTINEL='do-not-store-this-raw-value' \
      CAT_METRO_FULL_SOLUTION_CACHE_DIR="$selected_cache" \
      python3 "$helper"
  )
}

# 1. Standalone wrappers/helpers have no session context and execute for real every time.
reset_calls
run_direct > "$tmp/direct-1.out" 2> "$tmp/direct-1.err" \
  || fail "standalone execution 1 failed"
run_direct > "$tmp/direct-2.out" 2> "$tmp/direct-2.err" \
  || fail "standalone execution 2 failed"
[ "$(call_count)" -eq 2 ] || fail "standalone path did not execute dotnet twice"
echo "  ok: standalone path executes twice"

# 2. A stable, identical session snapshot executes once, then consumes one green attestation.
reset_calls
run_cached_at "$cache" > "$tmp/cache-miss.out" 2> "$tmp/cache-miss.err" \
  || fail "cache miss execution failed"
run_cached_at "$cache" > "$tmp/cache-hit.out" 2> "$tmp/cache-hit.err" \
  || fail "cache hit failed"
[ "$(call_count)" -eq 1 ] || fail "stable miss+hit executed dotnet more than once"
manifest_count=$(find "$cache/records" -type f -name '*.json' 2>/dev/null | wc -l | tr -d ' ')
[ "$manifest_count" -eq 1 ] || fail "expected one atomic green record, found $manifest_count"
if grep -rFq 'do-not-store-this-raw-value' "$cache" 2>/dev/null; then
  fail "raw environment value leaked into the private cache"
fi
echo "  ok: stable miss then hit executes once; record contains no raw env value"

# 3. Dirty tracked bytes invalidate; a failing result is executed and never published.
printf '%s\n' 'FAIL' > "$fixture/unity/Assets/Scripts/Domain/Fingerprint.cs"
reset_calls
run_cached_at "$cache" > "$tmp/fail-1.out" 2> "$tmp/fail-1.err"
rc=$?
if [ "$rc" -eq 0 ]; then
  fail "dirty failing input consumed stale green (first run)"
fi
[ "$rc" -eq 42 ] || fail "dirty failing input returned $rc, expected 42"
run_cached_at "$cache" > "$tmp/fail-2.out" 2> "$tmp/fail-2.err"
rc=$?
if [ "$rc" -eq 0 ]; then
  fail "failing result was cached green (second run)"
fi
[ "$rc" -eq 42 ] || fail "second failing run returned $rc, expected 42"
[ "$(call_count)" -eq 2 ] || fail "failed command was not executed twice"
printf '%s\n' 'PASS' > "$fixture/unity/Assets/Scripts/Domain/Fingerprint.cs"
run_cached_at "$cache" > "$tmp/restored.out" 2> "$tmp/restored.err" \
  || fail "byte-restored input did not reuse its original green record"
[ "$(call_count)" -eq 2 ] || fail "byte restoration did not return to the content key"
echo "  ok: tracked mutation is red; failures are not cached; byte restore hits"

# 4. Nonignored untracked path membership and exact child environment are key inputs.
reset_calls
printf '%s\n' 'extra' > "$fixture/extra-input.txt"
run_cached_at "$cache" > "$tmp/untracked.out" 2> "$tmp/untracked.err" \
  || fail "untracked-input miss failed"
[ "$(call_count)" -eq 1 ] || fail "untracked path addition did not invalidate"
rm -f -- "$fixture/extra-input.txt"
run_cached_at "$cache" > "$tmp/untracked-restored.out" 2> "$tmp/untracked-restored.err" \
  || fail "untracked path removal did not restore original key"
[ "$(call_count)" -eq 1 ] || fail "untracked path removal missed original record"
run_cached_at "$cache" 'different-env' > "$tmp/env.out" 2> "$tmp/env.err" \
  || fail "environment-change miss failed"
[ "$(call_count)" -eq 2 ] || fail "effective child environment did not invalidate"
echo "  ok: untracked membership and child environment invalidate"

# 5. A corrupt record is never trusted; a real green execution repairs it atomically.
manifest=$(find "$cache/records" -type f -name '*.json' | head -1)
[ -n "$manifest" ] || fail "could not locate green record for corruption proof"
printf '%s\n' '{broken' > "$manifest"
reset_calls
run_cached_at "$cache" > "$tmp/corrupt.out" 2> "$tmp/corrupt.err" \
  || fail "corrupt-record fallback execution failed"
[ "$(call_count)" -eq 1 ] || fail "corrupt record passed without real execution"
run_cached_at "$cache" > "$tmp/repaired.out" 2> "$tmp/repaired.err" \
  || fail "repaired record did not hit"
[ "$(call_count)" -eq 1 ] || fail "repaired record was not atomically reusable"
echo "  ok: corrupt record forces execution and is repaired"

# 6. If the command changes a fingerprinted input while it runs, no record is published.
printf '%s\n' 'MUTATE' > "$fixture/unity/Assets/Scripts/Domain/Fingerprint.cs"
reset_calls
run_cached_at "$cache_mutate" > "$tmp/mutate.out" 2> "$tmp/mutate.err" \
  || fail "mid-run mutation command failed"
[ "$(call_count)" -eq 1 ] || fail "mid-run mutation did not execute"
mutated_records=$(find "$cache_mutate/records" -type f -name '*.json' 2>/dev/null | wc -l | tr -d ' ')
[ "$mutated_records" -eq 0 ] || fail "input-changing command published a green record"
run_cached_at "$cache_mutate" > "$tmp/post-mutate-miss.out" 2> "$tmp/post-mutate-miss.err" \
  || fail "stable post-mutation miss failed"
run_cached_at "$cache_mutate" > "$tmp/post-mutate-hit.out" 2> "$tmp/post-mutate-hit.err" \
  || fail "stable post-mutation hit failed"
[ "$(call_count)" -eq 2 ] || fail "stable post-mutation miss+hit count was not two total"
echo "  ok: mid-run input drift refuses publication"

# 7. The two repeatability wrappers remain direct even when a cache variable exists.
reset_calls
if ! (
  cd "$repo_root" || exit 1
  PATH="$fake_bin:$PATH" \
    FAKE_DOTNET_LOG="$calls" \
    FAKE_ARTIFACT_ROOT="$repo_root/dotnet/CatMetro.Tests/obj/ci-full-solution" \
    CAT_METRO_FULL_SOLUTION_CACHE_DIR="$cache" \
    bash tests/domain/determinism.test.sh
) > "$tmp/determinism.out" 2> "$tmp/determinism.err"; then
  cat "$tmp/determinism.out"
  cat "$tmp/determinism.err" >&2
  fail "determinism wrapper rejected the two-process fake"
fi
[ "$(call_count)" -eq 2 ] || fail "determinism wrapper did not execute two processes"
if grep -Fq 'run-full-solution-test.py' tests/domain/determinism.test.sh; then
  fail "determinism wrapper references the cache helper"
fi

reset_calls
if ! (
  cd "$repo_root" || exit 1
  PATH="$fake_bin:$PATH" \
    FAKE_DOTNET_LOG="$calls" \
    FAKE_ARTIFACT_ROOT="$repo_root/dotnet/CatMetro.Tests/obj/ci-full-solution" \
    CAT_METRO_FULL_SOLUTION_CACHE_DIR="$cache" \
    bash tests/solver/solver.test.sh
) > "$tmp/solver.out" 2> "$tmp/solver.err"; then
  cat "$tmp/solver.out"
  cat "$tmp/solver.err" >&2
  fail "solver wrapper rejected the two-process fake"
fi
[ "$(call_count)" -eq 2 ] || fail "solver wrapper did not execute two processes"
if grep -Fq 'run-full-solution-test.py' tests/solver/solver.test.sh; then
  fail "solver wrapper references the cache helper"
fi
echo "  ok: determinism and solver each execute two direct detailed processes"

echo "full-solution-cache self-test: OK"
