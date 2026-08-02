#!/usr/bin/env bash
# PreToolUse hook (matcher: Edit|Write): deny writes to immutable paths, no matter what any prompt says.
# Immutable: tests/contract/, docs/constitution.md, .claude/hooks/, scripts/git-hooks/, state/mode,
# state/trust.json, evals/ EXCEPT evals/results/ (agents record claims) — but evals/results/attested/
# is human/CI only (trust-chain integrity). This is belt 2; the git pre-commit hook is belt 3.
# Note: this hook does NOT see Bash file writes — protect-bash.sh covers that lane.
#
# Python normalizes the path realpath-first (resolving .., //, trailing slashes, and symlinked roots
# to the true in-repo target); bash then matches case-INSENSITIVELY (macOS/APFS default). Fails CLOSED.

command -v python3 >/dev/null 2>&1 || { echo "protect-files.sh: python3 not found — blocking edit (fail-closed)" >&2; exit 2; }

input=$(cat)   # Edit/Write payload carries a single file_path — short, no ARG_MAX concern.
root="${CLAUDE_PROJECT_DIR:-}"
[ -z "$root" ] && root=$(git rev-parse --show-toplevel 2>/dev/null)
[ -z "$root" ] && root="$PWD"

# Emit the repo-relative, realpath-resolved path (empty = no path, or resolves outside the repo).
rel=$(FORGE_IN="$input" FORGE_ROOT="$root" python3 - <<'PY'
import json, os, sys
try:
    data = json.loads(os.environ.get("FORGE_IN", "") or "{}")
except Exception:
    sys.exit(2)  # unparseable → fail closed
ti = data.get("tool_input", {}) if isinstance(data, dict) else {}
path = ti.get("file_path") or ti.get("notebook_path")
if not path or not isinstance(path, str):
    sys.exit(0)
root = os.environ.get("FORGE_ROOT") or os.getcwd()
try:
    rel = os.path.relpath(os.path.realpath(path), os.path.realpath(root)).replace(os.sep, "/")
except Exception:
    sys.exit(2)
if rel == ".." or rel.startswith("../"):
    sys.exit(0)  # outside the repo — permissions/sandbox cover that depth
print(rel)
PY
)
pyrc=$?
[ "$pyrc" = "2" ] && { echo "protect-files.sh: could not resolve path — blocking (fail-closed)" >&2; exit 2; }
[ -z "$rel" ] && exit 0

deny() {
  printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"%s"}}\n' "$1"
  exit 0
}

shopt -s nocasematch   # case-insensitive: docs/CONSTITUTION.md and ATTESTED/ resolve to real targets

# Attested evidence: human/CI only — then the writable claims carve-out.
case "$rel" in
  evals/results/attested|evals/results/attested/*) deny "evals/results/attested/ is written by humans/CI only — agents record claims elsewhere under evals/results/" ;;
  evals/results|evals/results/*) exit 0 ;;
esac

case "$rel" in
  tests/contract/*)     deny "tests/contract/ is immutable; changes require a human-authored PR" ;;
  docs/constitution.md) deny "docs/constitution.md is immutable; changes require a human-authored PR" ;;
  .claude/hooks/*)      deny ".claude/hooks/ is immutable; changes require a human-authored PR" ;;
  scripts/git-hooks/*)  deny "scripts/git-hooks/ (universal enforcement) is immutable; changes require a human-authored PR" ;;
  .github/workflows/*)  deny ".github/workflows/ judges this branch (CI/review/deploy) — changes to the rules are human-authored (platform-engineer proposes; a human commits)" ;;
  evals|evals/*)        deny "evals/ definitions and rubrics are immutable (evals/results/ non-attested is the writable exception)" ;;
  state/mode)           deny "state/mode (stakes posture) is human-set; agents never dial down their own ceremony" ;;
  state/trust.json)     deny "state/trust.json is written by scripts/forge-trust.sh from evidence — never hand-edited (the autonomy dial cannot be self-set)" ;;
  codeowners|.github/codeowners|docs/codeowners) deny "CODEOWNERS anchors the review model (the server gate leans on it) — human-authored only" ;;
esac

# XR TRACK ONLY — forge-init uncomments (via sed, during the copy stage) for XR projects:
# case "$rel" in
#   *.unity|*.prefab) deny "Scene/prefab YAML is not agent-editable; use Editor-API MCP ops or hand to a human (see AGENTS.md XR rules)" ;;
#   *.meta)           deny ".meta files carry asset GUIDs; never create/edit/delete them by hand" ;;
# esac

exit 0
