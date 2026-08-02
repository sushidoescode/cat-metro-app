#!/usr/bin/env bash
# Substrate smoke test: the operating substrate this repo runs on is present and coherent.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
[ -f AGENTS.md ]
[ -f docs/constitution.md ]
[ -f state/PROJECT_STATE.md ]
[ -f scripts/check.sh ] && [ -f scripts/test.sh ] && [ -f scripts/build.sh ]
grep -q '^mode=' state/mode
grep -q '^view=' state/gate-prefs
