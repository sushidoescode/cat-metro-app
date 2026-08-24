#!/usr/bin/env bash
# Remove Forge Kit from this repo.
#
# YOU have to run this, not an agent — the guardrails are self-protecting. The PreToolUse hooks
# and the git pre-commit hook both refuse agent modification of the very paths that need to go,
# which is the design working as intended and also exactly why it has to be your hand.
#
#   bash scripts/deforge.sh --dry-run     # list what would go (default)
#   bash scripts/deforge.sh --apply       # actually delete
#
# Nothing here touches the game. See KEEP below.
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT" || exit 1
MODE="${1:---dry-run}"

# ---------------------------------------------------------------------------
# KEEP — the actual product. Never listed below, stated here so it is explicit:
#   unity/                 the game
#   dotnet/                Domain, Application, Content, Services, Validator + tests
#   content/levels/        17 authored levels
#   config/                runtime config
#   scripts/               check.sh test.sh build-apk.sh stage-content.sh validate-*.sh
#                          gen-assets.sh decimate-assets.py blender_decimate.py
#                          glb_metrics.py glb-silhouette.py emu-selftest.sh devcap-report.sh
#   tests/                 everything except tests/contract/
#   AGENTS.md              rewritten, lean — Codex reads this natively, do NOT delete it
#   CLAUDE.md              a two-line import of AGENTS.md, per Anthropic's own guidance
#   .claude/rules/         path-scoped rules that load only when relevant
#   docs/LOOK.md           the visual target
#   docs/reference/        the concept art and current-state screenshots
#   docs/adr/              kept as plain engineering notes, not governance
#   docs/architecture/ docs/runbooks/ docs/lessons.md
#   .gitignore             keystore + bearer-token guards
# ---------------------------------------------------------------------------

PATHS=(
  # governance and process docs
  "docs/constitution.md"
  "docs/plan"
  "docs/prd"
  "docs/superpowers"
  "docs/store"
  "docs/release"
  "docs/security"
  "docs/perf"
  "docs/ux"
  "docs/design/assets/DECIMATION.md"

  # rubrics, benchmarks, mode policy
  "evals"

  # census, gate ledgers, frozen contracts, mode files, backlog
  "state/PROJECT_STATE.md"
  "state/backlog.md"
  "state/gate-ledger"
  "state/gate-prefs"
  "state/handoffs"
  "state/hybrid-escalations"
  "state/hybrid-runs"
  "state/run-records"
  "state/mode"
  "state/usage-ledger.jsonl"

  # forge tooling
  "scripts/forge-doctor.sh"
  "scripts/forge-gate-view.sh"
  "scripts/forge-metrics.sh"
  "scripts/forge-risk.sh"
  "scripts/forge-trust.sh"
  "scripts/forge-upgrade.sh"
  "scripts/setup-rulesets.sh"
  "scripts/install-git-hooks.sh"
  "scripts/git-hooks"

  # the agent guardrails
  ".claude/hooks"

  # immutable contract tests
  "tests/contract"

  # ownership gate
  "CODEOWNERS"

  # the stub that builds nothing (scripts/build-apk.sh replaces it)
  "scripts/build.sh"
)

echo "=== Forge Kit removal — mode: $MODE ==="
echo
total=0
for p in "${PATHS[@]}"; do
  if [ -e "$p" ]; then
    n=$(find "$p" -type f 2>/dev/null | wc -l | tr -d ' ')
    sz=$(du -sh "$p" 2>/dev/null | awk '{print $1}')
    printf "  %-46s %5s files  %6s\n" "$p" "$n" "$sz"
    total=$((total + n))
  fi
done

# root-level session handoffs
hn=$(ls HANDOFF-*.md ART-VERDICT-*.md 2>/dev/null | wc -l | tr -d ' ')
[ "$hn" != "0" ] && printf "  %-46s %5s files\n" "HANDOFF-*.md / ART-VERDICT-*.md" "$hn"
total=$((total + hn))

echo
echo "  TOTAL: ~$total files"
echo

if [ "$MODE" != "--apply" ]; then
  echo "Dry run. Re-run with --apply to delete."
  exit 0
fi

for p in "${PATHS[@]}"; do [ -e "$p" ] && rm -rf "$p"; done
rm -f HANDOFF-*.md ART-VERDICT-*.md 2>/dev/null

# The git pre-commit hook is installed into .git/hooks and blocks its own removal.
rm -f .git/hooks/pre-commit .git/hooks/pre-push .git/hooks/post-checkout \
      .git/hooks/post-commit .git/hooks/post-merge 2>/dev/null

# Retire the forge policy CI job (kept on disk, disabled, so CI stops gating on it).
if [ -f ".github/workflows/forge-policy.yml" ]; then
  mv .github/workflows/forge-policy.yml .github/workflows/forge-policy.yml.disabled 2>/dev/null \
    && echo "disabled .github/workflows/forge-policy.yml"
fi

echo
echo "Done. Still to do by hand:"
echo "  1. .claude/settings.json — remove the 'hooks' block that points at .claude/hooks/"
echo "  2. git add -A && git commit   (your commit; the agent hooks are gone but review the diff)"
echo "  3. GitHub branch protection / rulesets, if you want PRs to stop being required"
