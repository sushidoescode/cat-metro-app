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
  if [ -n "${tmp:-}" ] && [ -d "$tmp" ] && [ ! -L "$tmp" ]; then
    case "$tmp" in
      "$tmp_parent"/cm-full-solution-cache-selftest.*) rm -rf -- "$tmp" ;;
    esac
  fi
}

finish() {
  rc=$?
  trap - EXIT HUP INT TERM
  cleanup
  exit "$rc"
}

interrupt() {
  rc=$1
  trap - EXIT HUP INT TERM
  cleanup
  exit "$rc"
}

trap finish EXIT
trap 'interrupt 129' HUP
trap 'interrupt 130' INT
trap 'interrupt 143' TERM

fixture="$tmp/repo"
fake_bin="$tmp/fake-bin"
fake_sdk="$tmp/fake-sdk/8.0.419"
fake_host="$tmp/host/fxr/8.0.25/libhostfxr.fake"
fake_pack="$tmp/packs/Microsoft.NETCore.App.Ref/8.0.25/ref/net8.0/System.Runtime.dll"
fake_workload_manifest="$tmp/sdk-manifests/8.0.400/fake/WorkloadManifest.json"
fake_home="$tmp/fake-home"
fake_packages="$fake_home/.nuget/packages"
fake_package="$fake_packages/fake.package/1.0.0/lib/net8.0/Fake.Package.dll"
fake_effective_packages="$tmp/effective-packages"
fake_effective_package="$fake_effective_packages/fake.package/1.0.0/lib/net8.0/Fake.Package.dll"
fake_restore_packages="$tmp/restore-packages"
fake_restore_package="$fake_restore_packages/fake.package/1.0.0/lib/net8.0/Fake.Package.dll"
fake_nuget_config="$fake_home/.nuget/NuGet/NuGet.Config"
fake_flap_sentinel="$tmp/flap-once"
cache="$tmp/cache"
cache_mutate_session="$tmp/mutate-session"
cache_mutate="$cache_mutate_session/cache"
cache_flap_session="$tmp/flap-session"
cache_flap="$cache_flap_session/cache"
cache_race_session="$tmp/race-session"
cache_race="$cache_race_session/cache"
calls="$tmp/dotnet.calls"
mkdir -p "$fixture/dotnet/Fake" "$fixture/dotnet/Restore" \
  "$fixture/unity/Assets/Scripts/Domain" "$fake_bin" \
  "$fake_sdk" "$(dirname "$fake_host")" "$(dirname "$fake_pack")" \
  "$(dirname "$fake_workload_manifest")" "$(dirname "$fake_package")" \
  "$(dirname "$fake_effective_package")" \
  "$(dirname "$fake_restore_package")" \
  "$(dirname "$fake_nuget_config")" \
  || fail "could not create self-test fixture"
mkdir -m 700 "$cache" "$cache_mutate_session" "$cache_flap_session" \
  "$cache_race_session" || fail "could not create private caches"
mkdir -m 700 "$cache_mutate" "$cache_flap" "$cache_race" \
  || fail "could not create focused caches"

printf '%s\n' 'Microsoft Visual Studio Solution File, Format Version 12.00' \
  > "$fixture/dotnet/CatMetro.sln"
printf '%s\n' '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>' \
  > "$fixture/dotnet/Fake/Fake.csproj"
printf '%s\n' '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework><RestorePackagesPath>configured-restore-root</RestorePackagesPath></PropertyGroup></Project>' \
  > "$fixture/dotnet/Restore/Restore.csproj"
printf '%s\n' 'PASS' > "$fixture/unity/Assets/Scripts/Domain/Fingerprint.cs"
printf '%s\n' 'dotnet/**/obj/' 'Directory.Build.props' > "$fixture/.gitignore"
printf '%s\n' '{"version":1,"dependencies":{"net8.0":{"Fake.Package":{"type":"Direct","requested":"[1.0.0, )","resolved":"1.0.0","contentHash":"fake"}}}}' \
  > "$fixture/dotnet/Fake/packages.lock.json"
cp "$fixture/dotnet/Fake/packages.lock.json" "$fixture/dotnet/Restore/packages.lock.json" \
  || fail "could not create restore-override lock fixture"
printf '%s\n' 'PASS' > "$fake_sdk/Fake.MSBuild.dll"
printf '%s\n' 'PASS' > "$fake_host"
printf '%s\n' 'PASS' > "$fake_pack"
printf '%s\n' 'PASS' > "$fake_workload_manifest"
printf '%s\n' 'PASS' > "$fake_package"
printf '%s\n' 'PASS' > "$fake_effective_package"
printf '%s\n' 'PASS' > "$fake_restore_package"
printf '%s\n' '<configuration><config><add key="globalPackagesFolder" value="unused-by-fake" /></config></configuration>' \
  > "$fake_nuget_config"

cat > "$fake_bin/dotnet" <<'FAKE_DOTNET'
#!/usr/bin/env bash
set -uo pipefail

if [ "$#" -eq 1 ] && [ "$1" = "--info" ]; then
  [ -z "${CAT_METRO_FULL_SOLUTION_CACHE_DIR:-}" ] || exit 91
  [ -z "${CAT_METRO_FULL_SOLUTION_CACHE_ACTIVE:-}" ] || exit 91
  [ -z "${CAT_METRO_FULL_SOLUTION_ARTIFACT_DIR:-}" ] || exit 91
  printf '%s\n' '.NET SDK:' ' Version: 8.0.419' " Base Path: ${FAKE_SDK_ROOT:?}/" \
    'RID: fake-portable' 'Host:' '  Version: 8.0.25'
  exit 0
fi

if [ "$#" -ge 5 ] && [ "$1" = 'msbuild' ]; then
  case "$2" in
    dotnet/Restore/Restore.csproj)
      printf '{"Properties":{"NuGetPackageRoot":"%s/","RestorePackagesPath":"%s","MSBuildProjectDirectory":"%s"}}\n' \
        "${FAKE_EFFECTIVE_PACKAGE_ROOT:?}" "${FAKE_RESTORE_PACKAGE_ROOT:?}" \
        "${PWD:?}/dotnet/Restore"
      ;;
    *)
      printf '{"Properties":{"NuGetPackageRoot":"%s/","RestorePackagesPath":"","MSBuildProjectDirectory":"%s"}}\n' \
        "${FAKE_EFFECTIVE_PACKAGE_ROOT:?}" "${PWD:?}/dotnet/Fake"
      ;;
  esac
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
if [ -f 'Directory.Build.props' ] && grep -q '^FAIL$' 'Directory.Build.props'; then
  state='FAIL'
fi
if grep -q '^FAIL$' "${FAKE_SDK_ROOT:?}/Fake.MSBuild.dll"; then
  state='FAIL'
fi
if grep -q '^FAIL$' "${FAKE_HOST_FILE:?}"; then
  state='FAIL'
fi
if grep -q '^FAIL$' "${FAKE_PACK_FILE:?}"; then
  state='FAIL'
fi
if grep -q '^FAIL$' "${FAKE_WORKLOAD_MANIFEST:?}"; then
  state='FAIL'
fi
if grep -q '^FAIL$' "${FAKE_EFFECTIVE_PACKAGE_ROOT:?}/fake.package/1.0.0/lib/net8.0/Fake.Package.dll"; then
  state='FAIL'
fi
if grep -q '^FAIL$' "${FAKE_RESTORE_PACKAGE_ROOT:?}/fake.package/1.0.0/lib/net8.0/Fake.Package.dll"; then
  state='FAIL'
fi
if grep -q '^FAIL$' "${HOME:?}/.nuget/NuGet/NuGet.Config"; then
  state='FAIL'
fi
case "$state" in
  FAIL)
    printf 'FAKE_FAILURE\n' >&2
    exit 42
    ;;
  MUTATE)
    printf '%s\n' 'DONE' > "$fingerprint"
    ;;
  FLAP)
    if [ ! -e "${FAKE_FLAP_SENTINEL:?}" ]; then
      : > "$FAKE_FLAP_SENTINEL"
      printf '%s\n' 'PASS' > "$fingerprint"
      printf '%s\n' 'FLAP' > "$fingerprint"
    else
      printf 'FAKE_FAILURE\n' >&2
      exit 42
    fi
    ;;
  TERM)
    kill -TERM "$$"
    exit 99
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

hostile_hooks="$tmp/hostile-hooks"
hostile_config="$tmp/hostile.gitconfig"
mkdir "$hostile_hooks" || fail "could not create hostile hook fixture"
cat > "$hostile_hooks/pre-commit" <<'HOSTILE_HOOK'
#!/usr/bin/env bash
exit 86
HOSTILE_HOOK
chmod 700 "$hostile_hooks/pre-commit" || fail "could not activate hostile hook fixture"
printf '%s\n' '[commit]' '    gpgSign = true' '[core]' "    hooksPath = $hostile_hooks" \
  > "$hostile_config"

fixture_git() {
  GIT_CONFIG_NOSYSTEM=1 GIT_CONFIG_GLOBAL="$hostile_config" \
    git -C "$fixture" -c commit.gpgSign=false -c core.hooksPath=/dev/null "$@"
}

fixture_git init -q || fail "could not initialize fixture repository"
fixture_git add .gitignore dotnet/CatMetro.sln dotnet/Fake/Fake.csproj \
  dotnet/Fake/packages.lock.json dotnet/Restore/Restore.csproj \
  dotnet/Restore/packages.lock.json \
  unity/Assets/Scripts/Domain/Fingerprint.cs \
  || fail "could not stage fixture inputs"
fixture_git -c user.name='CI self-test' -c user.email='ci-selftest@example.invalid' \
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
    unset CAT_METRO_FULL_SOLUTION_CACHE_ACTIVE
    unset CAT_METRO_FULL_SOLUTION_ARTIFACT_DIR
    PATH="$fake_bin:$PATH" \
      HOME="$fake_home" \
      NUGET_PACKAGES="$fake_packages" \
      FAKE_SDK_ROOT="$fake_sdk" \
      FAKE_HOST_FILE="$fake_host" \
      FAKE_PACK_FILE="$fake_pack" \
      FAKE_WORKLOAD_MANIFEST="$fake_workload_manifest" \
      FAKE_EFFECTIVE_PACKAGE_ROOT="$fake_effective_packages" \
      FAKE_RESTORE_PACKAGE_ROOT="$fake_restore_packages" \
      FAKE_FLAP_SENTINEL="$fake_flap_sentinel" \
      FAKE_DOTNET_LOG="$calls" \
      FAKE_ARTIFACT_ROOT="$fixture/dotnet/CatMetro.Tests/obj/ci-full-solution" \
      CACHE_SECRET_SENTINEL='do-not-store-this-raw-value' \
      python3 "$helper"
  )
}

run_cached_at() {
  selected_cache=$1
  selected_active=$(dirname "$selected_cache")
  variant=${2:-stable}
  (
    cd "$fixture" || exit 1
    PATH="$fake_bin:$PATH" \
      HOME="$fake_home" \
      NUGET_PACKAGES="$fake_packages" \
      FAKE_SDK_ROOT="$fake_sdk" \
      FAKE_HOST_FILE="$fake_host" \
      FAKE_PACK_FILE="$fake_pack" \
      FAKE_WORKLOAD_MANIFEST="$fake_workload_manifest" \
      FAKE_EFFECTIVE_PACKAGE_ROOT="$fake_effective_packages" \
      FAKE_RESTORE_PACKAGE_ROOT="$fake_restore_packages" \
      FAKE_FLAP_SENTINEL="$fake_flap_sentinel" \
      FAKE_DOTNET_LOG="$calls" \
      FAKE_ARTIFACT_ROOT="$fixture/dotnet/CatMetro.Tests/obj/ci-full-solution" \
      CACHE_TEST_VARIANT="$variant" \
      CACHE_SECRET_SENTINEL='do-not-store-this-raw-value' \
      CAT_METRO_FULL_SOLUTION_CACHE_ACTIVE="$selected_active" \
      CAT_METRO_FULL_SOLUTION_CACHE_DIR="$selected_cache" \
      python3 "$helper"
  )
}

run_inactive_at() {
  selected_cache=$1
  (
    cd "$fixture" || exit 1
    unset CAT_METRO_FULL_SOLUTION_CACHE_ACTIVE
    PATH="$fake_bin:$PATH" \
      HOME="$fake_home" \
      NUGET_PACKAGES="$fake_packages" \
      FAKE_SDK_ROOT="$fake_sdk" \
      FAKE_HOST_FILE="$fake_host" \
      FAKE_PACK_FILE="$fake_pack" \
      FAKE_WORKLOAD_MANIFEST="$fake_workload_manifest" \
      FAKE_EFFECTIVE_PACKAGE_ROOT="$fake_effective_packages" \
      FAKE_RESTORE_PACKAGE_ROOT="$fake_restore_packages" \
      FAKE_FLAP_SENTINEL="$fake_flap_sentinel" \
      FAKE_DOTNET_LOG="$calls" \
      FAKE_ARTIFACT_ROOT="$fixture/dotnet/CatMetro.Tests/obj/ci-full-solution" \
      CACHE_TEST_VARIANT='stable' \
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

# Cache-control paths inherited without scripts/test.sh's activation capability stay standalone.
reset_calls
run_inactive_at "$cache" > "$tmp/inactive-1.out" 2> "$tmp/inactive-1.err" \
  || fail "inactive standalone execution 1 failed"
run_inactive_at "$cache" > "$tmp/inactive-2.out" 2> "$tmp/inactive-2.err" \
  || fail "inactive standalone execution 2 failed"
[ "$(call_count)" -eq 2 ] || fail "inactive cache controls reused a result outside scripts/test.sh"
if grep -Fq 'cached-standard' "$calls"; then
  fail "inactive cache controls selected the cached artifacts command"
fi
echo "  ok: inherited cache paths without harness activation execute twice"

reset_calls
for wrapper_run in 1 2; do
  if ! (
    cd "$repo_root" || exit 1
    unset CAT_METRO_FULL_SOLUTION_CACHE_ACTIVE
    PATH="$fake_bin:$PATH" \
      HOME="$fake_home" \
      NUGET_PACKAGES="$fake_packages" \
      FAKE_SDK_ROOT="$fake_sdk" \
      FAKE_HOST_FILE="$fake_host" \
      FAKE_PACK_FILE="$fake_pack" \
      FAKE_WORKLOAD_MANIFEST="$fake_workload_manifest" \
      FAKE_EFFECTIVE_PACKAGE_ROOT="$fake_effective_packages" \
      FAKE_RESTORE_PACKAGE_ROOT="$fake_restore_packages" \
      FAKE_FLAP_SENTINEL="$fake_flap_sentinel" \
      FAKE_DOTNET_LOG="$calls" \
      FAKE_ARTIFACT_ROOT="$repo_root/dotnet/CatMetro.Tests/obj/ci-full-solution" \
      CAT_METRO_FULL_SOLUTION_CACHE_DIR="$cache" \
      CAT_METRO_FULL_SOLUTION_ARTIFACT_DIR="$fixture/dotnet/CatMetro.Tests/obj/ci-full-solution" \
      bash tests/content/importer.test.sh
  ) > "$tmp/inactive-wrapper-$wrapper_run.out" \
    2> "$tmp/inactive-wrapper-$wrapper_run.err"; then
    fail "standalone importer wrapper run $wrapper_run failed"
  fi
done
[ "$(call_count)" -eq 2 ] || fail "standalone importer wrapper reused inherited cache paths"
echo "  ok: standalone eligible wrapper executes the real command twice"

# 2. A stable, identical session snapshot executes once, then consumes one green attestation.
reset_calls
run_cached_at "$cache" > "$tmp/cache-miss.out" 2> "$tmp/cache-miss.err" \
  || fail "cache miss execution failed"
run_cached_at "$cache" > "$tmp/cache-hit.out" 2> "$tmp/cache-hit.err" \
  || fail "cache hit failed"
if [ "$(call_count)" -ne 1 ]; then
  cat "$tmp/cache-miss.err" "$tmp/cache-hit.err" >&2
  cat "$calls" >&2
  fail "stable miss+hit executed dotnet more than once"
fi
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
  || fail "byte-restored input did not execute green"
run_cached_at "$cache" > "$tmp/restored-hit.out" 2> "$tmp/restored-hit.err" \
  || fail "stable byte-restored input did not reuse its new green record"
[ "$(call_count)" -eq 3 ] \
  || fail "write+restore metadata did not force one conservative real green execution"
echo "  ok: tracked mutation is red; failures are not cached; byte restore revalidates"

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
while IFS= read -r -d '' manifest; do
  printf '%s\n' '{broken' > "$manifest"
done < <(find "$cache/records" -type f -name '*.json' -print0 2>/dev/null)
reset_calls
run_cached_at "$cache" > "$tmp/corrupt.out" 2> "$tmp/corrupt.err" \
  || fail "corrupt-record fallback execution failed"
[ "$(call_count)" -eq 1 ] || fail "corrupt record passed without real execution"
run_cached_at "$cache" > "$tmp/repaired.out" 2> "$tmp/repaired.err" \
  || fail "repaired record did not hit"
[ "$(call_count)" -eq 1 ] || fail "repaired record was not atomically reusable"
echo "  ok: corrupt record forces execution and is repaired"

# 6. A cache hit rechecks content after validating its record, closing the pre-hit mutation gap.
cat > "$tmp/hit-race-probe.py" <<'HIT_RACE_PROBE'
#!/usr/bin/env python3
import importlib.util
import os
from pathlib import Path
import sys

sys.dont_write_bytecode = True
helper, root_raw, cache = sys.argv[1:]
spec = importlib.util.spec_from_file_location("cat_metro_cache_helper", helper)
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)
root = Path(root_raw)
environment = dict(os.environ)
active = environment["CAT_METRO_FULL_SOLUTION_CACHE_ACTIVE"]
for name in (
    "CAT_METRO_FULL_SOLUTION_CACHE_DIR",
    "CAT_METRO_FULL_SOLUTION_CACHE_ACTIVE",
    "CAT_METRO_FULL_SOLUTION_ARTIFACT_DIR",
):
    environment.pop(name, None)
original = module._record_is_valid

def mutate_after_validation(path, expected):
    valid = original(path, expected)
    if valid:
        (root / "unity/Assets/Scripts/Domain/Fingerprint.cs").write_text("FAIL\n")
    return valid

module._record_is_valid = mutate_after_validation
raise SystemExit(module._cached(root, environment, active, cache, None))
HIT_RACE_PROBE

reset_calls
(
  cd "$fixture" || exit 1
  PATH="$fake_bin:$PATH" \
    HOME="$fake_home" \
    NUGET_PACKAGES="$fake_packages" \
    FAKE_SDK_ROOT="$fake_sdk" \
    FAKE_HOST_FILE="$fake_host" \
    FAKE_PACK_FILE="$fake_pack" \
    FAKE_WORKLOAD_MANIFEST="$fake_workload_manifest" \
    FAKE_EFFECTIVE_PACKAGE_ROOT="$fake_effective_packages" \
    FAKE_RESTORE_PACKAGE_ROOT="$fake_restore_packages" \
    FAKE_FLAP_SENTINEL="$fake_flap_sentinel" \
    FAKE_DOTNET_LOG="$calls" \
    FAKE_ARTIFACT_ROOT="$fixture/dotnet/CatMetro.Tests/obj/ci-full-solution" \
    CACHE_TEST_VARIANT='stable' \
    CACHE_SECRET_SENTINEL='do-not-store-this-raw-value' \
    CAT_METRO_FULL_SOLUTION_CACHE_ACTIVE="$tmp" \
    CAT_METRO_FULL_SOLUTION_CACHE_DIR="$cache" \
    python3 "$tmp/hit-race-probe.py" "$helper" "$fixture" "$cache"
) > "$tmp/hit-race.out" 2> "$tmp/hit-race.err"
rc=$?
[ "$rc" -eq 42 ] || fail "post-validation mutation returned $rc, expected a real failing run (42)"
[ "$(call_count)" -eq 1 ] || fail "post-validation mutation did not force exactly one real run"
printf '%s\n' 'PASS' > "$fixture/unity/Assets/Scripts/Domain/Fingerprint.cs"
run_cached_at "$cache" > "$tmp/hit-race-restored.out" 2> "$tmp/hit-race-restored.err" \
  || fail "post-validation mutation restore did not execute green"
run_cached_at "$cache" > "$tmp/hit-race-restored-hit.out" \
  2> "$tmp/hit-race-restored-hit.err" \
  || fail "post-validation mutation restore did not establish a stable hit"
[ "$(call_count)" -eq 2 ] \
  || fail "post-validation mutation restore did not execute exactly one new producer"
echo "  ok: post-validation mutation cannot consume stale green"

# 7. Ignored root policy, every consumed .NET pack, the effective package root, and NuGet config
# are toolchain inputs. In particular, the fake command consumes a globalPackagesFolder that is
# deliberately different from NUGET_PACKAGES, so hashing the conventional default is insufficient.
reset_calls
printf '%s\n' 'FAIL' > "$fixture/Directory.Build.props"
run_cached_at "$cache" > "$tmp/root-policy.out" 2> "$tmp/root-policy.err"
rc=$?
[ "$rc" -eq 42 ] || fail "ignored root build-policy mutation returned $rc, expected 42"
[ "$(call_count)" -eq 1 ] || fail "ignored root build-policy mutation did not execute"
printf '%s\n' 'PASS' > "$fixture/Directory.Build.props"
run_cached_at "$cache" > "$tmp/root-policy-pass.out" 2> "$tmp/root-policy-pass.err" \
  || fail "ignored root build-policy green miss failed"
run_cached_at "$cache" > "$tmp/root-policy-hit.out" 2> "$tmp/root-policy-hit.err" \
  || fail "ignored root build-policy green hit failed"
[ "$(call_count)" -eq 2 ] || fail "ignored root build-policy stable key did not hit"
rm -f -- "$fixture/Directory.Build.props"
run_cached_at "$cache" > "$tmp/root-policy-restored.out" 2> "$tmp/root-policy-restored.err" \
  || fail "root build-policy removal did not restore the original key"
if [ "$(call_count)" -ne 2 ]; then
  cat "$calls" >&2
  find "$cache/records" -type f -name '*.json' -print >&2 2>/dev/null || true
  fail "root build-policy removal missed the original record"
fi

for external_input in "$fake_sdk/Fake.MSBuild.dll" "$fake_host" "$fake_pack" \
  "$fake_workload_manifest" "$fake_effective_package" "$fake_restore_package" \
  "$fake_nuget_config"; do
  cp "$external_input" "$tmp/external-input.original" \
    || fail "could not preserve external build input $external_input"
  printf '%s\n' 'FAIL' > "$external_input"
  run_cached_at "$cache" > "$tmp/external-fail.out" 2> "$tmp/external-fail.err"
  rc=$?
  [ "$rc" -eq 42 ] || fail "external build input $external_input returned $rc, expected 42"
  cp "$tmp/external-input.original" "$external_input" \
    || fail "could not restore external build input $external_input"
  run_cached_at "$cache" > "$tmp/external-restored.out" 2> "$tmp/external-restored.err" \
    || fail "external build input $external_input did not execute green after restore"
  run_cached_at "$cache" > "$tmp/external-restored-hit.out" \
    2> "$tmp/external-restored-hit.err" \
    || fail "stable restored external build input $external_input did not hit"
done
[ "$(call_count)" -eq 16 ] \
  || fail "external build inputs were not each executed once per changed metadata key"
echo "  ok: ignored root, SDK, packs, effective package root, and NuGet inputs invalidate"

# 8. A signal-killed child preserves shell-compatible 128+signal status.
printf '%s\n' 'TERM' > "$fixture/unity/Assets/Scripts/Domain/Fingerprint.cs"
reset_calls
run_direct > "$tmp/term.out" 2> "$tmp/term.err"
rc=$?
[ "$rc" -eq 143 ] || fail "SIGTERM child returned $rc, expected 143"
[ "$(call_count)" -eq 1 ] || fail "SIGTERM child was not executed once"
printf '%s\n' 'PASS' > "$fixture/unity/Assets/Scripts/Domain/Fingerprint.cs"
echo "  ok: child SIGTERM maps to exit 143"

# 9. Session cleanup accepts only one exact ignored session child and never follows leaf links.
cleanup_parent="$fixture/dotnet/CatMetro.Tests/obj/ci-full-solution"
cleanup_session="$cleanup_parent/session.cleanup"
external_sentinel="$tmp/cleanup-external"
mkdir -p "$cleanup_session" "$external_sentinel" || fail "could not create cleanup fixture"
chmod 700 "$cleanup_session" || fail "could not make cleanup fixture private"
printf '%s\n' 'KEEP' > "$external_sentinel/keep.txt"
ln -s "$external_sentinel" "$cleanup_session/external-link" \
  || fail "could not create cleanup symlink fixture"
if (
  cd "$fixture" || exit 1
  python3 "$helper" --cleanup-session "$cleanup_parent"
) > "$tmp/cleanup-broad.out" 2> "$tmp/cleanup-broad.err"; then
  fail "cleanup accepted the broad session parent"
fi
[ -d "$cleanup_session" ] || fail "refused broad cleanup removed its child"
(
  cd "$fixture" || exit 1
  python3 "$helper" --cleanup-session "$cleanup_session"
) > "$tmp/cleanup.out" 2> "$tmp/cleanup.err" || {
  cat "$tmp/cleanup.err" >&2
  fail "validated session cleanup failed"
}
[ ! -e "$cleanup_session" ] && [ ! -L "$cleanup_session" ] \
  || fail "validated session cleanup left its target"
[ -f "$external_sentinel/keep.txt" ] || fail "session cleanup followed an external symlink"

# macOS ships Python 3.9 at this path. Keep the top-level gate's cleanup on that interpreter
# floor; Python 3.9's shutil.rmtree has no public dir_fd keyword.
if [ -x /usr/bin/python3 ]; then
  compatibility_session="$cleanup_parent/session.pythoncompat"
  mkdir -p "$compatibility_session/nested" \
    || fail "could not create Python compatibility cleanup fixture"
  chmod 700 "$compatibility_session" \
    || fail "could not make Python compatibility cleanup fixture private"
  printf '%s\n' 'REMOVE' > "$compatibility_session/nested/remove.txt"
  if ! (
    cd "$fixture" || exit 1
    /usr/bin/python3 "$helper" --cleanup-session "$compatibility_session"
  ) > "$tmp/cleanup-python-compat.out" 2> "$tmp/cleanup-python-compat.err"; then
    cat "$tmp/cleanup-python-compat.err" >&2
    fail "/usr/bin/python3 could not perform validated session cleanup"
  fi
  [ ! -e "$compatibility_session" ] && [ ! -L "$compatibility_session" ] \
    || fail "/usr/bin/python3 cleanup left its target"
fi

# An attacker with the same uid must not be able to swap a same-named real directory between
# validation and fd-open. The opened dev/inode must still match the entry that was approved.
rename_session="$cleanup_parent/session.rename"
rename_substitute="$cleanup_parent/session.substitute"
rename_held="$cleanup_parent/held-original"
mkdir -p "$rename_session" "$rename_substitute" \
  || fail "could not create cleanup rename-race fixtures"
chmod 700 "$rename_session" "$rename_substitute" \
  || fail "could not make cleanup rename-race fixtures private"
printf '%s\n' 'ORIGINAL' > "$rename_session/original.txt"
printf '%s\n' 'SUBSTITUTE' > "$rename_substitute/substitute.txt"
cat > "$tmp/cleanup-rename-race-probe.py" <<'CLEANUP_RENAME_RACE_PROBE'
#!/usr/bin/env python3
import importlib.util
import os
from pathlib import Path
import sys

sys.dont_write_bytecode = True
helper, target_raw, substitute_raw, held_raw = sys.argv[1:]
spec = importlib.util.spec_from_file_location("cat_metro_cache_helper", helper)
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)
target = Path(target_raw)
substitute = Path(substitute_raw)
held = Path(held_raw)
original_open = module._open_real_directory
swapped = False

def swap_before_target_open(path, *, parent_fd=None):
    global swapped
    if not swapped and parent_fd is not None and os.fspath(path) == target.name:
        os.rename(target, held)
        os.rename(substitute, target)
        swapped = True
    return original_open(path, parent_fd=parent_fd)

module._open_real_directory = swap_before_target_open
try:
    module._cleanup_session(module._repository_root(), os.fspath(target))
except module.Uncacheable:
    raise SystemExit(0)
raise SystemExit(1)
CLEANUP_RENAME_RACE_PROBE
if ! (
  cd "$fixture" || exit 1
  python3 "$tmp/cleanup-rename-race-probe.py" "$helper" "$rename_session" \
    "$rename_substitute" "$rename_held"
) > "$tmp/cleanup-rename-race.out" 2> "$tmp/cleanup-rename-race.err"; then
  cat "$tmp/cleanup-rename-race.err" >&2
  fail "cleanup accepted a same-name directory substitution"
fi
[ -f "$rename_held/original.txt" ] \
  || fail "cleanup rename-race removed the originally validated directory"
[ -f "$rename_session/substitute.txt" ] \
  || fail "cleanup rename-race removed the substituted directory"
echo "  ok: cleanup is exact and does not follow symlinks"

# 10. A top-level SIGTERM is failure and still removes the exact private session.
signal_bin="$tmp/signal-bin"
signal_ready="$tmp/signal-ready"
mkdir "$signal_bin" || fail "could not create signal fake-bin"
cat > "$signal_bin/bash" <<'SIGNAL_BASH'
#!/bin/bash
case "${1-}" in
  scripts/selftest/full-solution-cache.selftest.sh)
    exit 0
    ;;
  tests/analytics/queue.test.sh)
    : > "${SIGNAL_READY:?}"
    sleep 1
    exit 0
    ;;
  *)
    exit 0
    ;;
esac
SIGNAL_BASH
chmod 700 "$signal_bin/bash" || fail "could not activate signal fake bash"
repo_session_parent="$repo_root/dotnet/CatMetro.Tests/obj/ci-full-solution"
sessions_before=$(find "$repo_session_parent" -mindepth 1 -maxdepth 1 -type d \
  -name 'session.*' -print 2>/dev/null | sort)
PATH="$signal_bin:$PATH" SIGNAL_READY="$signal_ready" \
  /bin/bash scripts/test.sh > "$tmp/top-signal.out" 2> "$tmp/top-signal.err" &
signal_pid=$!
ready=0
for _attempt in $(seq 1 100); do
  if [ -f "$signal_ready" ]; then
    ready=1
    break
  fi
  sleep 0.02
done
[ "$ready" -eq 1 ] || {
  kill -TERM "$signal_pid" 2>/dev/null || true
  wait "$signal_pid" 2>/dev/null || true
  fail "top-level signal probe never reached a wrapper"
}
kill -TERM "$signal_pid" || fail "could not signal top-level gate"
wait "$signal_pid"
rc=$?
[ "$rc" -eq 143 ] || fail "SIGTERM top-level gate returned $rc, expected 143"
sessions_after=$(find "$repo_session_parent" -mindepth 1 -maxdepth 1 -type d \
  -name 'session.*' -print 2>/dev/null | sort)
if [ "$sessions_after" != "$sessions_before" ]; then
  cat "$tmp/top-signal.out"
  cat "$tmp/top-signal.err" >&2
  printf 'sessions before:\n%s\nsessions after:\n%s\n' "$sessions_before" "$sessions_after" >&2
  fail "SIGTERM top-level gate leaked a session"
fi
echo "  ok: top-level SIGTERM maps to 143 and removes its session"

# 11. If the command changes a fingerprinted input while it runs, no record is published.
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

# 12. A command that writes an input, observes the temporary bytes, and restores the original
# bytes before exit must not publish. Content-only before/after snapshots make this stale-green.
printf '%s\n' 'FLAP' > "$fixture/unity/Assets/Scripts/Domain/Fingerprint.cs"
rm -f -- "$fake_flap_sentinel"
reset_calls
run_cached_at "$cache_flap" > "$tmp/flap-producer.out" 2> "$tmp/flap-producer.err" \
  || fail "transient write+restore producer did not return its observed green result"
run_direct > "$tmp/flap-direct.out" 2> "$tmp/flap-direct.err"
rc=$?
[ "$rc" -eq 42 ] || fail "standalone FLAP control returned $rc, expected 42"
run_cached_at "$cache_flap" > "$tmp/flap-second.out" 2> "$tmp/flap-second.err"
rc=$?
[ "$rc" -eq 42 ] || fail "write+restore producer published stale green (rc=$rc)"
[ "$(call_count)" -eq 3 ] \
  || fail "write+restore proof expected producer, direct control, and real second cache call"
flap_records=$(find "$cache_flap/records" -type f -name '*.json' 2>/dev/null \
  | wc -l | tr -d ' ')
[ "$flap_records" -eq 0 ] || fail "write+restore producer left a green record"
printf '%s\n' 'PASS' > "$fixture/unity/Assets/Scripts/Domain/Fingerprint.cs"
rm -f -- "$fake_flap_sentinel"
echo "  ok: transient write+restore cannot publish stale green"

# 13. The final snapshot globally revalidates early-observed files. This probe mutates the
# solution only after its per-file hash has completed while a later input is being hashed.
cat > "$tmp/final-snapshot-race-probe.py" <<'FINAL_SNAPSHOT_RACE_PROBE'
#!/usr/bin/env python3
import importlib.util
import os
from pathlib import Path
import sys

sys.dont_write_bytecode = True
helper, root_raw, cache = sys.argv[1:]
spec = importlib.util.spec_from_file_location("cat_metro_cache_helper", helper)
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)
root = Path(root_raw)
environment = dict(os.environ)
active = environment["CAT_METRO_FULL_SOLUTION_CACHE_ACTIVE"]
for name in (
    "CAT_METRO_FULL_SOLUTION_CACHE_DIR",
    "CAT_METRO_FULL_SOLUTION_CACHE_ACTIVE",
    "CAT_METRO_FULL_SOLUTION_ARTIFACT_DIR",
):
    environment.pop(name, None)

original_snapshot = module._snapshot
snapshot_count = 0

def racing_snapshot(snapshot_root, snapshot_environment):
    global snapshot_count
    snapshot_count += 1
    if snapshot_count != 3:
        return original_snapshot(snapshot_root, snapshot_environment)
    original_hash = module._hash_file

    def mutate_early_after_late_hash(hash_root, relative, aggregate, *args, **kwargs):
        result = original_hash(hash_root, relative, aggregate, *args, **kwargs)
        if hash_root == root and relative == "unity/Assets/Scripts/Domain/Fingerprint.cs":
            (root / "dotnet/CatMetro.sln").write_text("LATE MUTATION\n")
        return result

    module._hash_file = mutate_early_after_late_hash
    try:
        return original_snapshot(snapshot_root, snapshot_environment)
    finally:
        module._hash_file = original_hash

module._snapshot = racing_snapshot
raise SystemExit(module._cached(root, environment, active, cache, None))
FINAL_SNAPSHOT_RACE_PROBE

cp "$fixture/dotnet/CatMetro.sln" "$tmp/solution.original" \
  || fail "could not preserve solution for final-snapshot race"
reset_calls
(
  cd "$fixture" || exit 1
  PATH="$fake_bin:$PATH" \
    HOME="$fake_home" \
    NUGET_PACKAGES="$fake_packages" \
    FAKE_SDK_ROOT="$fake_sdk" \
    FAKE_HOST_FILE="$fake_host" \
    FAKE_PACK_FILE="$fake_pack" \
    FAKE_WORKLOAD_MANIFEST="$fake_workload_manifest" \
    FAKE_EFFECTIVE_PACKAGE_ROOT="$fake_effective_packages" \
    FAKE_RESTORE_PACKAGE_ROOT="$fake_restore_packages" \
    FAKE_FLAP_SENTINEL="$fake_flap_sentinel" \
    FAKE_DOTNET_LOG="$calls" \
    FAKE_ARTIFACT_ROOT="$fixture/dotnet/CatMetro.Tests/obj/ci-full-solution" \
    CACHE_TEST_VARIANT='stable' \
    CACHE_SECRET_SENTINEL='do-not-store-this-raw-value' \
    CAT_METRO_FULL_SOLUTION_CACHE_ACTIVE="$cache_race_session" \
    CAT_METRO_FULL_SOLUTION_CACHE_DIR="$cache_race" \
    python3 "$tmp/final-snapshot-race-probe.py" "$helper" "$fixture" "$cache_race"
) > "$tmp/final-snapshot-race.out" 2> "$tmp/final-snapshot-race.err" \
  || fail "final-snapshot race producer did not preserve its real green result"
race_records=$(find "$cache_race/records" -type f -name '*.json' 2>/dev/null \
  | wc -l | tr -d ' ')
[ "$race_records" -eq 0 ] || fail "late final-snapshot mutation published green"
cp "$tmp/solution.original" "$fixture/dotnet/CatMetro.sln" \
  || fail "could not restore solution after final-snapshot race"
echo "  ok: final snapshot globally revalidates early inputs"

# 14. The two repeatability wrappers remain direct even when cache controls exist.
reset_calls
if ! (
  cd "$repo_root" || exit 1
  PATH="$fake_bin:$PATH" \
    HOME="$fake_home" \
    NUGET_PACKAGES="$fake_packages" \
    FAKE_SDK_ROOT="$fake_sdk" \
    FAKE_HOST_FILE="$fake_host" \
    FAKE_PACK_FILE="$fake_pack" \
    FAKE_WORKLOAD_MANIFEST="$fake_workload_manifest" \
    FAKE_EFFECTIVE_PACKAGE_ROOT="$fake_effective_packages" \
    FAKE_RESTORE_PACKAGE_ROOT="$fake_restore_packages" \
    FAKE_FLAP_SENTINEL="$fake_flap_sentinel" \
    FAKE_DOTNET_LOG="$calls" \
    FAKE_ARTIFACT_ROOT="$repo_root/dotnet/CatMetro.Tests/obj/ci-full-solution" \
    CAT_METRO_FULL_SOLUTION_CACHE_ACTIVE=1 \
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
    HOME="$fake_home" \
    NUGET_PACKAGES="$fake_packages" \
    FAKE_SDK_ROOT="$fake_sdk" \
    FAKE_HOST_FILE="$fake_host" \
    FAKE_PACK_FILE="$fake_pack" \
    FAKE_WORKLOAD_MANIFEST="$fake_workload_manifest" \
    FAKE_EFFECTIVE_PACKAGE_ROOT="$fake_effective_packages" \
    FAKE_RESTORE_PACKAGE_ROOT="$fake_restore_packages" \
    FAKE_FLAP_SENTINEL="$fake_flap_sentinel" \
    FAKE_DOTNET_LOG="$calls" \
    FAKE_ARTIFACT_ROOT="$repo_root/dotnet/CatMetro.Tests/obj/ci-full-solution" \
    CAT_METRO_FULL_SOLUTION_CACHE_ACTIVE=1 \
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

helper_bytecode=$(find "$repo_root/scripts/__pycache__" -maxdepth 1 -type f \
  -name 'run-full-solution-test.cpython-*.pyc' -print 2>/dev/null || true)
[ -z "$helper_bytecode" ] || fail "helper import left Python bytecode in the repository"
echo "  ok: helper import leaves no repository bytecode"

echo "full-solution-cache self-test: OK"
