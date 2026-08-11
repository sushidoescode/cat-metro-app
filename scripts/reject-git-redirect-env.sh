# Source-only fail-closed guard for commands whose repository/object boundary is security-critical.
catmetro_reject_git_redirect_env() {
  local guard_label="${1:-command}"
  if [ "${GIT_DIR+x}" = x ] \
    || [ "${GIT_WORK_TREE+x}" = x ] \
    || [ "${GIT_INDEX_FILE+x}" = x ] \
    || [ "${GIT_OBJECT_DIRECTORY+x}" = x ] \
    || [ "${GIT_ALTERNATE_OBJECT_DIRECTORIES+x}" = x ] \
    || [ "${GIT_COMMON_DIR+x}" = x ] \
    || [ "${GIT_NAMESPACE+x}" = x ] \
    || [ "${GIT_SHALLOW_FILE+x}" = x ] \
    || [ "${GIT_REPLACE_REF_BASE+x}" = x ] \
    || [ "${GIT_GRAFT_FILE+x}" = x ] \
    || [ "${GIT_CONFIG_GLOBAL+x}" = x ] \
    || [ "${GIT_CONFIG_SYSTEM+x}" = x ] \
    || [ "${GIT_CONFIG_NOSYSTEM+x}" = x ] \
    || [ "${GIT_CONFIG_PARAMETERS+x}" = x ] \
    || [ "${GIT_CONFIG_COUNT+x}" = x ] \
    || [ "${GIT_LITERAL_PATHSPECS+x}" = x ] \
    || [ "${GIT_GLOB_PATHSPECS+x}" = x ] \
    || [ "${GIT_NOGLOB_PATHSPECS+x}" = x ] \
    || [ "${GIT_ICASE_PATHSPECS+x}" = x ]; then
    echo "$guard_label: refusing inherited Git redirect/configuration state" >&2
    return 1
  fi
}

catmetro_require_checkout_root() {
  local checkout_candidate="$1"
  local checkout_label="${2:-command}"
  local caller_root expected_root resolved_root
  caller_root=$(pwd -P) || return 1
  expected_root=$(cd "$checkout_candidate" 2>/dev/null && pwd -P) || {
    echo "$checkout_label: checkout root is unavailable" >&2
    return 1
  }
  if [ "$caller_root" != "$expected_root" ]; then
    echo "$checkout_label: must be invoked from its checkout root" >&2
    return 1
  fi
  cd "$expected_root" || return 1
  resolved_root=$(git rev-parse --show-toplevel 2>/dev/null) || {
    echo "$checkout_label: checkout root is not a Git worktree" >&2
    return 1
  }
  if [ "$resolved_root" != "$expected_root" ]; then
    echo "$checkout_label: script path and Git worktree root disagree" >&2
    return 1
  fi
}
