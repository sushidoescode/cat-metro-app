#!/usr/bin/env bash
# Artifact gate for the shipped queue quartet. It reads authored values and replay evidence
# instead of preserving pre-ladder golden numbers.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
fail() { echo "queue-reading-band.test.sh: FAIL — $1"; exit 1; }
tmp=$(mktemp -d "${TMPDIR:-/tmp}/cm-queue-XXXXXX") || fail "mktemp failed"
trap 'rm -rf "$tmp"' EXIT

if ! bash scripts/validate-content.sh --out "$tmp/report.json" > "$tmp/gate.out" 2>&1; then
  cat "$tmp/gate.out"
  fail "the canonical corpus gate did not exit 0"
fi
python3 tests/corpus/mechanic-band-artifact.py "$tmp/report.json" \
  queue onboarding L005 L006 L007 L008 || exit 1
bash scripts/stage-content.sh > "$tmp/stage.out" 2>&1 || {
  cat "$tmp/stage.out"
  fail "StreamingAssets staging check found drift"
}
echo "queue-reading-band.test.sh: PASS"
