#!/usr/bin/env bash
# Point git at the repo's versioned hooks (universal enforcement layer).
# Run once per clone — forge-init does it; collaborators and CI checkouts re-run it.
# NOTE: git config is per-clone (not cloned with the repo), and client hooks are bypassable
# with --no-verify — server-side branch/push rulesets remain the real wall. This is the local belt.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
chmod +x scripts/git-hooks/* 2>/dev/null || true
git config core.hooksPath scripts/git-hooks
echo "forge: git hooks installed (core.hooksPath=scripts/git-hooks)"
echo "forge: prove it any time with: bash scripts/forge-doctor.sh  (runs a real blocked-commit probe)"
