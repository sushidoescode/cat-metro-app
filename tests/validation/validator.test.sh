#!/usr/bin/env bash
# CM-C5 fast-leg wrapper (criteria 15 & 17), discovered by scripts/test.sh. Every check is
# labelled; any failure exits 1 with the label.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
tmp="${TMPDIR:-/tmp}/cm-c5-wrapper-$$"
mkdir -p "$tmp"
trap 'rm -rf "$tmp"' EXIT
fail() { echo "validator.test.sh: FAIL — $1"; exit 1; }

# Review F14: an empty corpus glob would make the SHA belt vacuously true — refuse it.
[ -n "$(ls content/levels/*.json 2>/dev/null)" ] || fail "corpus glob content/levels/*.json matched nothing"

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
# Review F3: grep the machine-readable BLOCKING line — the stage-name grid row appears in every
# run, so a bare "Solver" grep can never fail.
if bash scripts/validate-content.sh --corpus tests/validation/fixtures/broken-level.json > "$tmp/broken.out" 2>&1; then
  fail "criterion 15b: broken-level.json did not fail the gate"
fi
grep -q "BLOCKING: L999 stage 4 Solver" "$tmp/broken.out" \
  || fail "criterion 15b: no BLOCKING line naming L999 + Solver"
# Review F4: the fixture path is outside content/levels/**, so the campaign assertions must NOT
# have judged it (no band-table noise in the D9 evidence).
if grep -q "BLOCKING: campaign" "$tmp/broken.out"; then
  fail "criterion 15b/14: campaign assertions ran over a non-campaign fixture path"
fi

# 15c: the entry point references no secret and no Unity.
if grep -Enq 'secrets\.|UnityEngine|Unity ' scripts/validate-content.sh; then
  fail "criterion 15c: validate-content.sh references a secret or Unity"
fi

# 15d: zero file-API matches under the validation library (belt on top of check.sh's block).
if grep -rEnq --include='*.cs' '\bSystem\.IO\b' unity/Assets/Scripts/Content/Validation; then
  fail "criterion 15d: file API reference under Content/Validation"
fi

# 17b: --stamp writes exactly meta.validatedAt and no other byte — on a MULTI-file corpus copy
# (review F12: one file proved nothing about the other N-1).
mkdir -p "$tmp/stamp-corpus"
cp content/levels/L001.json "$tmp/stamp-corpus/L001.json"
cp content/levels/L001.json "$tmp/stamp-corpus/L001b.json"
if ! bash scripts/validate-content.sh --corpus "$tmp/stamp-corpus" --stamp > "$tmp/stamp.out" 2>&1; then
  cat "$tmp/stamp.out"
  fail "criterion 17b: --stamp run failed"
fi
[ "$(grep -c "stamped" "$tmp/stamp.out")" = "2" ] || fail "criterion 17b: expected exactly 2 stamped files"
for f in L001.json L001b.json; do
  if ! dotnet run --project dotnet/CatMetro.Validator -c Release -- \
      --assert-stamp-diff content/levels/L001.json "$tmp/stamp-corpus/$f" > "$tmp/diff.out" 2>&1; then
    cat "$tmp/diff.out"
    fail "criterion 17b: stamp changed more than the one key in $f"
  fi
done
# ...and the stamped copy still imports (guards the E-C2a-2 date regression forever).
if ! bash scripts/validate-content.sh --corpus "$tmp/stamp-corpus" > "$tmp/restamp.out" 2>&1; then
  cat "$tmp/restamp.out"
  fail "criterion 17b: the stamped level no longer validates"
fi

echo "validator.test.sh: OK (15a-d, 16, 17a-c)"
exit 0
