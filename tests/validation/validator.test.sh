#!/usr/bin/env bash
# CM-C5 fast-leg wrapper (criteria 15 & 17), discovered by scripts/test.sh. Every check is
# labelled; any failure exits 1 with the label.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
tmp="${TMPDIR:-/tmp}/cm-c5-wrapper-$$"
mkdir -p "$tmp"
trap 'rm -rf "$tmp"' EXIT
fail() { echo "validator.test.sh: FAIL — $1"; exit 1; }

# 17a: the gate run leaves every input byte-identical (SHA-256 before/after)...
before=$(shasum -a 256 content/levels/*.json docs/plan/data/stress_boards.json docs/plan/data/level_schema.json config/validator_thresholds.json | shasum -a 256)
# ...and 15a: it exits 0 on the current corpus, writing the JSON report.
if ! bash scripts/validate-content.sh --out "$tmp/report.json" > "$tmp/gate.out" 2>&1; then
  cat "$tmp/gate.out"
  fail "criterion 15a: validate-content.sh non-zero on the current corpus"
fi
after=$(shasum -a 256 content/levels/*.json docs/plan/data/stress_boards.json docs/plan/data/level_schema.json config/validator_thresholds.json | shasum -a 256)
[ "$before" = "$after" ] || fail "criterion 17a: the gate run modified an input file"

# 17c: no content path appears modified to git after a gate run.
dirty=$(git diff --name-only -- content docs/plan config/validator_thresholds.json)
[ -z "$dirty" ] || fail "criterion 17c: gate run left tracked changes: $dirty"

# 16: the machine-readable report exists and carries the load-bearing markers.
[ -s "$tmp/report.json" ] || fail "criterion 16: no JSON report written"
grep -q '"secondsVerdict": "PINNED(NEW-Q1)"' "$tmp/report.json" || fail "criterion 16: NEW-Q1 pin missing from report"
grep -q '"exitFailure": false' "$tmp/report.json" || fail "criterion 16: exitFailure not false on the green corpus"

# 15b: the deliberately broken level fails, naming the level and the stage (roadmap D9).
if bash scripts/validate-content.sh --corpus tests/validation/fixtures/broken-level.json > "$tmp/broken.out" 2>&1; then
  fail "criterion 15b: broken-level.json did not fail the gate"
fi
grep -q "L999" "$tmp/broken.out" || fail "criterion 15b: failing level not named"
grep -q "Solver" "$tmp/broken.out" || fail "criterion 15b: failing stage not named"

# 15c: the entry point references no secret and no Unity.
if grep -Enq 'secrets\.|UnityEngine|Unity ' scripts/validate-content.sh; then
  fail "criterion 15c: validate-content.sh references a secret or Unity"
fi

# 15d: zero file-API matches under the validation library (belt on top of check.sh's block).
if grep -rEnq --include='*.cs' '\bSystem\.IO\b' unity/Assets/Scripts/Content/Validation; then
  fail "criterion 15d: file API reference under Content/Validation"
fi

# 17b: --stamp writes exactly meta.validatedAt and no other byte, on a copy.
mkdir -p "$tmp/stamp-corpus"
cp content/levels/L001.json "$tmp/stamp-corpus/L001.json"
if ! bash scripts/validate-content.sh --corpus "$tmp/stamp-corpus" --stamp > "$tmp/stamp.out" 2>&1; then
  cat "$tmp/stamp.out"
  fail "criterion 17b: --stamp run failed"
fi
grep -q "stamped" "$tmp/stamp.out" || fail "criterion 17b: --stamp stamped nothing"
if ! dotnet run --project dotnet/CatMetro.Validator -c Release -- \
    --assert-stamp-diff content/levels/L001.json "$tmp/stamp-corpus/L001.json" > "$tmp/diff.out" 2>&1; then
  cat "$tmp/diff.out"
  fail "criterion 17b: stamp changed more than the one key"
fi
# ...and the stamped copy still imports (guards the E-C2a-2 date regression forever).
if ! bash scripts/validate-content.sh --corpus "$tmp/stamp-corpus" > "$tmp/restamp.out" 2>&1; then
  cat "$tmp/restamp.out"
  fail "criterion 17b: the stamped level no longer validates"
fi

echo "validator.test.sh: OK (15a-d, 16, 17a-c)"
exit 0
