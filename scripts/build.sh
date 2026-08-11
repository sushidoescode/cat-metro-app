#!/usr/bin/env bash
# Build gate. The credential-free public profile verifies staged content and custody without
# pretending to compile Unity; the explicit licensed-local profile performs the Android build.
set -euo pipefail
umask 077
if [ "${POLYFORK_KEY+x}" = "x" ]; then
  echo "build: refusing inherited POLYFORK_KEY; acquisition credentials must not enter build subprocesses" >&2
  exit 2
fi
if [ "${CM_UNITY_EDITOR+x}" = "x" ]; then
  echo "build: CM_UNITY_EDITOR overrides are forbidden; licensed-local execution authenticates the pinned editor" >&2
  exit 2
fi
build_script_dir="${BASH_SOURCE[0]%/*}"
[ "$build_script_dir" != "${BASH_SOURCE[0]}" ] || build_script_dir=.
. "$build_script_dir/reject-git-redirect-env.sh"
catmetro_reject_git_redirect_env "build" || exit 2
catmetro_require_checkout_root "$build_script_dir/.." "build" || exit 2

flow_token_dir=""
flow_token_file=""
cleanup_flow_token() {
  if [ -n "$flow_token_file" ] && [ -f "$flow_token_file" ]; then
    command unlink "$flow_token_file"
  fi
  if [ -n "$flow_token_dir" ] && [ -d "$flow_token_dir" ]; then
    rmdir "$flow_token_dir"
  fi
}
trap cleanup_flow_token EXIT
trap 'exit 130' HUP INT TERM

custody_profile="${CM_REQUIRE_POLYFORK_LOCAL:-0}"
case "$custody_profile" in
  0|1) ;;
  *)
    echo "build: CM_REQUIRE_POLYFORK_LOCAL must be 0 or 1" >&2
    exit 2
    ;;
esac
if [ "$custody_profile" = "1" ] && [ "$#" -ne 0 ]; then
  echo "build: licensed-local profile accepts no arguments; staging must verify the real checkout" >&2
  exit 2
fi

# CM-C10 criterion 9: fail closed on staged-tree drift before any build step. Check mode
# ONLY — the build gate never writes the staged tree, so the write-mode flag is refused
# outright. Forwarded args (e.g. --root <dir>) reach the stager untouched.
for arg in "$@"; do
  if [ "$arg" = "--apply" ]; then
    echo "build: refusing --apply — the build gate runs stage-content.sh check-only" >&2
    exit 1
  fi
done
bash "$(dirname "$0")/stage-content.sh" "$@"

if [ "$custody_profile" = "1" ]; then
  if [ -z "${CM_APK_OUT:-}" ]; then
    echo "build: CM_APK_OUT is required for the licensed-local Android profile" >&2
    exit 2
  fi
  case "$CM_APK_OUT" in
    /*) ;;
    *) export CM_APK_OUT="$(pwd)/$CM_APK_OUT" ;;
  esac
  if [ -e "$CM_APK_OUT" ] || [ -L "$CM_APK_OUT" ]; then
    echo "build: refusing existing CM_APK_OUT; choose a fresh output path" >&2
    exit 1
  fi
  unity_editor=$(bash scripts/verify-unity-editor.sh) || exit 1
fi

bash tests/unity/polyfork-custody.test.sh

if [ "$custody_profile" != "1" ]; then
  echo "build: OK (verification-only profile; Unity Android build deferred)"
  exit 0
fi

for local_cache in unity/Library unity/Temp unity/Logs unity/.utmp; do
  if [ ! -e "$local_cache" ]; then
    mkdir -m 700 "$local_cache"
  fi
done

flow_token_dir=$(mktemp -d "${TMPDIR:-/tmp}/catmetro-build-flow-token.XXXXXX")
flow_token_file="$flow_token_dir/token"
CM_POLYFORK_BUILD_FLOW_NONCE=$(python3 -c 'import secrets; print(secrets.token_hex(32))')
printf '%s\n' "$CM_POLYFORK_BUILD_FLOW_NONCE" > "$flow_token_file"
chmod 600 "$flow_token_file"
export CM_POLYFORK_BUILD_FLOW_TOKEN="$flow_token_file"
export CM_POLYFORK_BUILD_FLOW_NONCE

"$unity_editor" -batchmode -quit -projectPath "$(pwd)/unity" \
  -executeMethod CatMetroCliBuild.BuildAndroid -logFile -

if ! python3 scripts/verify-android-apk.py "$CM_APK_OUT"
then
  echo "build: Unity exited without producing an APK-shaped artifact at CM_APK_OUT" >&2
  exit 1
fi

echo "build: OK (licensed-local Android APK-shaped artifact verified at $CM_APK_OUT)"
