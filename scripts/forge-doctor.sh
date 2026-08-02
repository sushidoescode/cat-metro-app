#!/usr/bin/env bash
# forge-doctor: is the operating system actually operating?
# Run any time (any harness, or no harness). Checks configuration drift — the #1 way kits rot.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)" 2>/dev/null || { echo "FAIL not a git repo"; exit 1; }
pass=0; fail=0; skip=0
ok(){ echo "  OK         $1"; pass=$((pass+1)); }
bad(){ echo "  FAIL       $1"; fail=$((fail+1)); }
note(){ echo "  SKIP       $1"; skip=$((skip+1)); }
unverified(){ echo "  UNVERIFIED $1"; skip=$((skip+1)); }

echo "forge-doctor:"
echo
echo "local guardrails:"

# Universal layer (every harness)
[ -f AGENTS.md ] && ok "AGENTS.md present" || bad "AGENTS.md missing (universal instructions layer)"
[ -f docs/constitution.md ] && ok "constitution present" || bad "constitution missing"
[ "$(git config core.hooksPath)" = "scripts/git-hooks" ] && ok "git hooks wired (core.hooksPath)" || bad "git hooks NOT wired — run scripts/install-git-hooks.sh"
[ -x scripts/git-hooks/pre-commit ] && ok "pre-commit executable" || bad "pre-commit missing or not executable"

# Unresolved init tokens (pattern assembled by concatenation so this file never matches itself)
tok='[A-Z][A-Z0-9_]*'; pat='\{\{'"$tok"
if grep -rEq "$pat" --exclude-dir=.git --exclude-dir=node_modules --exclude-dir=Library . 2>/dev/null; then
  bad "unresolved init tokens remain (init incomplete) — grep -rEn '$pat' ."
else
  ok "no unresolved init tokens"
fi

[ -f state/PROJECT_STATE.md ] && ok "state file present" || bad "state/PROJECT_STATE.md missing"
[ -f .github/workflows/ci.yml ] && ok "CI workflow present" || bad "CI workflow missing"
if [ -f state/PROJECT_STATE.md ]; then
  state_line_count=$(wc -l < state/PROJECT_STATE.md 2>/dev/null | tr -d ' ')
  [ "${state_line_count:-0}" -le 200 ] \
    && ok "PROJECT_STATE.md bounded ($state_line_count lines)" \
    || bad "PROJECT_STATE.md is $state_line_count lines — a session-start context tax; rotate history to state/archive/ (cap ~150)"
fi

# Immutability probe (behavioral, not prose): the pre-commit hook must actually block.
# Guards: only run with a clean index (never commit user work) and an existing HEAD (reset target).
if ! git diff --cached --quiet 2>/dev/null; then
  note "immutability probe skipped: index not clean (commit or unstage first, then re-run)"
elif ! git rev-parse -q --verify HEAD >/dev/null 2>&1; then
  note "immutability probe skipped: no commits yet (re-run after the first commit)"
else
  probe="tests/contract/.forge-doctor-probe-$$"
  mkdir -p tests/contract && echo probe > "$probe" && git add "$probe" 2>/dev/null
  probe_report=$(git commit -q -m "forge-doctor probe (must be blocked)" 2>&1)
  probe_rc=$?
  if [ "$probe_rc" -eq 0 ]; then
    bad "pre-commit did NOT block an immutable-path commit — enforcement is theater"
    git reset -q --soft HEAD~1
  elif printf '%s\n' "$probe_report" | grep -Fq "forge pre-commit: BLOCKED" \
    && printf '%s\n' "$probe_report" | grep -Fq "$probe"; then
    ok "pre-commit blocks immutable-path commits (probe denied)"
  else
    probe_reason=$(printf '%s\n' "$probe_report" | head -n 1)
    bad "immutability probe commit failed without a Forge denial (${probe_reason:-no diagnostic})"
  fi
  git reset -q HEAD "$probe" >/dev/null 2>&1
  rm -f "$probe"
fi

# Claude Code layer (only if configured)
if [ -d .claude ]; then
  [ -f .claude/settings.json ] && python3 -c "import json;json.load(open('.claude/settings.json'))" 2>/dev/null \
    && ok "settings.json valid JSON" || bad "settings.json missing/invalid"
  for h in protect-files.sh protect-bash.sh; do
    [ -x ".claude/hooks/$h" ] && ok "$h executable" || bad "$h missing/not executable"
  done
  command -v python3 >/dev/null 2>&1 \
    && ok "python3 available (Claude hooks fail closed without it)" \
    || bad "python3 not installed — Claude hooks (protect-files/protect-bash) fail closed and block ALL edits"
  echo '{"tool_input":{"file_path":"docs/constitution.md"}}' | bash .claude/hooks/protect-files.sh 2>/dev/null | grep -q '"deny"' \
    && ok "protect-files denies constitution edit" || bad "protect-files did not deny (schema/logic drift?)"
fi

echo
echo "remote authority:"
[ -f .github/workflows/forge-policy.yml ] \
  && ok "local forge-policy workflow definition present" \
  || bad "forge-policy workflow missing"
[ -f CODEOWNERS ] \
  && ok "local CODEOWNERS policy present" \
  || bad "CODEOWNERS missing"
[ -x scripts/setup-rulesets.sh ] \
  && ok "ruleset installer/checker executable" \
  || bad "scripts/setup-rulesets.sh missing or not executable"

# A missing remote, unavailable plan, missing gh auth, or network failure is not proof that server
# enforcement is absent. setup-rulesets has a tri-state contract so only confirmed drift turns red.
if [ -x scripts/setup-rulesets.sh ]; then
  remote_report=$(bash scripts/setup-rulesets.sh --check 2>&1)
  remote_rc=$?
  case "$remote_rc" in
    0)
      ok "remote enforcement confirmed: rulesets match the declared posture (details below)"
      ;;
    1)
      bad "remote enforcement confirmed DRIFTED or MISSING"
      ;;
    3)
      doctor_stakes_mode=$(grep -E '^mode=' state/mode 2>/dev/null | tail -1 | cut -d= -f2 | tr -d '[:space:]')
      case "$doctor_stakes_mode" in
        production)
          bad "solo ruleset posture at production stakes — graduate to dual (second human CODEOWNER) before production work"
          ;;
        sprint|standard)
          note "remote enforcement PARTIAL: solo posture — no server-side human gate on default-branch merges (declared in state/mode)"
          ;;
        *)
          bad "solo ruleset posture with unrecognized stakes mode '${doctor_stakes_mode:-unset}' — fix state/mode (fail-closed)"
          ;;
      esac
      ;;
    *)
      unverified "remote enforcement could not be confirmed (no remote/auth/plan/network)"
      ;;
  esac
  if [ -n "$remote_report" ]; then
    while IFS= read -r line; do
      echo "    $line"
    done <<<"$remote_report"
  fi
else
  unverified "remote enforcement cannot be queried without setup-rulesets.sh"
fi

echo
echo "trust provenance:"

# Stakes mode is a human decision. Require both HEAD membership and a worktree/index match before
# calling it committed; an untracked file must never be described as trusted provenance.
if [ -f state/mode ]; then
  mode=$(grep -E '^mode=' state/mode | tail -1 | cut -d= -f2)
  if git cat-file -e HEAD:state/mode 2>/dev/null && git diff --quiet HEAD -- state/mode 2>/dev/null; then
    ok "stakes mode: ${mode:-unset} (committed, clean HEAD provenance)"
  else
    bad "state/mode is uncommitted or differs from HEAD — mode flips must be human-authored commits"
  fi
else
  note "state/mode missing (pre-3.0 project) — defaulting to standard; copy the template file to adopt modes"
fi

if [ -f evals/trust-policy.json ] \
  && python3 -c "import json;json.load(open('evals/trust-policy.json'))" 2>/dev/null; then
  if git cat-file -e HEAD:evals/trust-policy.json 2>/dev/null \
    && git diff --quiet HEAD -- evals/trust-policy.json 2>/dev/null; then
    ok "trust policy valid and matches committed HEAD"
  else
    bad "trust policy is uncommitted or differs from HEAD"
  fi
else
  bad "evals/trust-policy.json missing/invalid"
fi

# state/trust.json is intentionally absent until forge-trust computes the dial. If present, trust
# only a regular JSON file whose bytes match a committed HEAD blob.
if [ -e state/trust.json ] || [ -L state/trust.json ]; then
  if [ -L state/trust.json ] || [ ! -f state/trust.json ]; then
    bad "state/trust.json must be a regular file, never a symlink"
  elif ! python3 -c "import json;d=json.load(open('state/trust.json'));assert isinstance(d,dict) and isinstance(d.get('level'),int)" 2>/dev/null; then
    bad "state/trust.json missing/invalid trust level JSON"
  elif ! git cat-file -e HEAD:state/trust.json 2>/dev/null; then
    bad "state/trust.json is not committed — mutable worktree bytes are not provenance"
  elif ! git diff --quiet HEAD -- state/trust.json 2>/dev/null; then
    bad "state/trust.json differs from committed HEAD"
  else
    trust_level=$(python3 -c "import json;print(json.load(open('state/trust.json'))['level'])")
    ok "trust dial level $trust_level matches committed HEAD"
  fi
else
  note "state/trust.json not computed yet (run scripts/forge-trust.sh; human reviews its commit)"
fi

# Promotion evidence only counts when it is direct, regular, valid JSON committed under
# evals/results/attested/. Validate those provenance properties without rewriting the trust dial.
if [ -L evals/results/attested ]; then
  bad "evals/results/attested must be a real directory, never a symlink"
elif [ ! -d evals/results/attested ]; then
  note "no attested trust evidence directory yet"
else
  attested_report=$(python3 - <<'PY'
import glob
import json
import os
import subprocess
import sys

paths = sorted(
    set(glob.glob("evals/results/attested/metrics-*.json"))
    | set(glob.glob("evals/results/attested/benchmark-*.json"))
)
errors = []
valid = 0
for path in paths:
    if os.path.islink(path) or not os.path.isfile(path):
        errors.append(f"{path}: not a regular file")
        continue
    present = subprocess.run(
        ["git", "cat-file", "-e", f"HEAD:{path}"],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    ).returncode == 0
    if not present:
        errors.append(f"{path}: not committed in HEAD")
        continue
    clean = subprocess.run(
        ["git", "diff", "--quiet", "HEAD", "--", path],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    ).returncode == 0
    if not clean:
        errors.append(f"{path}: worktree/index differs from HEAD")
        continue
    blob = subprocess.run(
        ["git", "show", f"HEAD:{path}"],
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
    )
    try:
        json.loads(blob.stdout)
    except (UnicodeDecodeError, json.JSONDecodeError):
        errors.append(f"{path}: committed blob is invalid JSON")
        continue
    valid += 1

if errors:
    print("; ".join(errors))
    raise SystemExit(1)
print(valid)
PY
)
  attested_rc=$?
  if [ "$attested_rc" -ne 0 ]; then
    bad "attested evidence provenance invalid: $attested_report"
  elif [ "${attested_report:-0}" -eq 0 ]; then
    note "no direct attested metric/benchmark evidence yet"
  else
    ok "$attested_report direct attested evidence file(s) committed and clean"
  fi
fi

echo
echo "forge-doctor: $pass ok, $fail failing, $skip skipped/unverified"
[ "$fail" -eq 0 ]
