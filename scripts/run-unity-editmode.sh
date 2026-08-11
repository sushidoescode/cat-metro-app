#!/usr/bin/env bash
# Canonical custody-aware route to the immutable CM-C2b Unity verifier. A clean public checkout
# runs its static half and defers Unity; a licensed owner runs the exact full verifier.
set -euo pipefail
umask 077

fail() { echo "run-unity-editmode.sh: FAIL — $1" >&2; exit 1; }

if [ "${POLYFORK_KEY+x}" = "x" ]; then
  fail "refusing inherited POLYFORK_KEY before Unity test subprocesses"
fi
if [ "${CM_UNITY_EDITOR+x}" = "x" ]; then
  fail "CM_UNITY_EDITOR overrides are forbidden; licensed-local execution authenticates the pinned editor"
fi
[ "$#" -eq 0 ] || fail "accepts no arguments; use CM_REQUIRE_POLYFORK_LOCAL=0 or 1"
driver_script_dir="${BASH_SOURCE[0]%/*}"
[ "$driver_script_dir" != "${BASH_SOURCE[0]}" ] || driver_script_dir=.
. "$driver_script_dir/reject-git-redirect-env.sh"
catmetro_reject_git_redirect_env "run-unity-editmode.sh" || exit 2
catmetro_require_checkout_root "$driver_script_dir/.." "run-unity-editmode.sh" || exit 2

custody_profile="${CM_REQUIRE_POLYFORK_LOCAL:-0}"
case "$custody_profile" in
  0|1) ;;
  *) fail "CM_REQUIRE_POLYFORK_LOCAL must be 0 or 1" ;;
esac

raw_wrapper="tests/unity/editmode.test.sh"
base=$(git rev-parse -q --verify origin/main 2>/dev/null \
  || git rev-parse -q --verify main 2>/dev/null || true)
[ -n "$base" ] || fail "cannot verify immutable verifier because main is unresolved"
if merge_base=$(git merge-base HEAD "$base" 2>/dev/null); then
  git diff --quiet "$merge_base" -- "$raw_wrapper" \
    || fail "immutable verifier differs from its merge-base"
else
  git diff --quiet HEAD "$base" -- "$raw_wrapper" \
    || fail "immutable verifier differs from the base tip in a shallow clone"
  git diff --quiet HEAD -- "$raw_wrapper" \
    || fail "immutable verifier differs in the current worktree"
fi

model_root="unity/Assets/Art/Polyfork/Models"
local_pack_entry=$(find "$model_root" -mindepth 1 -maxdepth 1 -print -quit 2>/dev/null || true)
if [ "$custody_profile" = "1" ] || [ -n "$local_pack_entry" ]; then
  bash scripts/verify-unity-editor.sh >/dev/null \
    || fail "licensed-local profile requires the pinned Unity editor with matching signature/version"
  CM_REQUIRE_POLYFORK_LOCAL=1 bash tests/unity/polyfork-custody.test.sh \
    || fail "licensed-local custody preflight failed"
  for local_cache in unity/Library unity/Temp unity/Logs unity/.utmp; do
    if [ ! -e "$local_cache" ]; then
      mkdir -m 700 "$local_cache"
    fi
  done
  bash "$raw_wrapper"
  exit $?
fi

bash tests/unity/polyfork-custody.test.sh \
  || fail "clean-public custody preflight failed"
pinned_editor="/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity"
if [ ! -x "$pinned_editor" ]; then
  bash "$raw_wrapper"
  exit $?
fi

marker_count=$(grep -Fxc '# --- editor half ---' "$raw_wrapper" || true)
[ "$marker_count" = "1" ] \
  || fail "immutable verifier must contain exactly one editor-half sentinel"
projection_dir=$(mktemp -d "${TMPDIR:-/tmp}/catmetro-clean-public-editmode.XXXXXX")
projection="$projection_dir/static-verifier.sh"
cleanup_projection() {
  [ ! -f "$projection" ] || command unlink "$projection"
  [ ! -d "$projection_dir" ] || rmdir "$projection_dir"
}
trap cleanup_projection EXIT HUP INT TERM
sed '/^# --- editor half ---$/,$d' "$raw_wrapper" > "$projection"
printf '\nexit 0\n' >> "$projection"
chmod 600 "$projection"
bash "$projection"
echo "run-unity-editmode.sh: OK (clean-public static profile; editor half DEFERRED — licensed local pack absent)"
