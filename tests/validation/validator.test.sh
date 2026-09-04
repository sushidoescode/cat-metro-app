#!/usr/bin/env bash
# CM-C5 fast-leg wrapper (criteria 15 & 17), discovered by scripts/test.sh. Every check is
# labelled; any failure exits 1 with the label.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)" || exit 1
tmp="${TMPDIR:-/tmp}/cm-c5-wrapper-$$"
mkdir -p "$tmp"
trap 'rm -rf "$tmp"' EXIT
fail() { echo "validator.test.sh: FAIL — $1"; exit 1; }

# Review F14: an empty corpus glob would make the SHA belt vacuously true — refuse it.
[ -n "$(ls content/levels/*.json 2>/dev/null)" ] || fail "corpus glob content/levels/*.json matched nothing"

# 17a: the gate run leaves every input byte-identical (SHA-256 before/after)...
before=$(shasum -a 256 content/levels/*.json docs/plan/data/stress_boards.json docs/plan/data/level_schema.json config/validator_thresholds.json | shasum -a 256)
# 17c: snapshot the exact tracked state before the run. Comparing the final tree to HEAD would
# falsely blame the validator for intentional pre-existing work; comparing exact binary diffs
# before/after proves attribution and also catches edits to a path that was already dirty.
before_git_diff="$tmp/tracked-before.diff"
after_git_diff="$tmp/tracked-after.diff"
git diff HEAD --binary -- content docs/plan config/validator_thresholds.json > "$before_git_diff" \
  || fail "criterion 17c: could not snapshot tracked inputs before the gate"
# ...and 15a: it exits 0 on the current corpus, writing the JSON report.
if ! bash scripts/validate-content.sh --out "$tmp/report.json" > "$tmp/gate.out" 2>&1; then
  cat "$tmp/gate.out"
  fail "criterion 15a: validate-content.sh non-zero on the current corpus"
fi
after=$(shasum -a 256 content/levels/*.json docs/plan/data/stress_boards.json docs/plan/data/level_schema.json config/validator_thresholds.json | shasum -a 256)
[ "$before" = "$after" ] || fail "criterion 17a: the gate run modified an input file"

# 17c: the exact tracked diff is unchanged by the gate run.
git diff HEAD --binary -- content docs/plan config/validator_thresholds.json > "$after_git_diff" \
  || fail "criterion 17c: could not snapshot tracked inputs after the gate"
if ! cmp -s "$before_git_diff" "$after_git_diff"; then
  diff -u "$before_git_diff" "$after_git_diff" | head -80 || true
  fail "criterion 17c: gate run changed the tracked input diff"
fi

# 16: the machine-readable report exists and carries the load-bearing markers.
[ -s "$tmp/report.json" ] || fail "criterion 16: no JSON report written"
grep -q '"secondsVerdict": "PINNED(NEW-Q1)"' "$tmp/report.json" || fail "criterion 16: NEW-Q1 pin missing from report"
grep -q '"exitFailure": false' "$tmp/report.json" || fail "criterion 16: exitFailure not false on the green corpus"

# 15b: the deliberately broken level fails, naming the level and the stage (roadmap D9).
# Review F3: grep the machine-readable BLOCKING line — the stage-name grid row appears in every
# run, so a bare "Solver" grep can never fail.
if bash scripts/validate-content.sh --corpus tests/validation/fixtures/broken-level.json \
    --out "$tmp/broken-report.json" > "$tmp/broken.out" 2>&1; then
  fail "criterion 15b: broken-level.json did not fail the gate"
fi
grep -q "BLOCKING: L999 stage 4 Solver" "$tmp/broken.out" \
  || fail "criterion 15b: no BLOCKING line naming L999 + Solver"
# Review F4: the fixture path is outside content/levels/**, so the campaign assertions must NOT
# have judged it. The exact-60 campaign-presence row still blocks an invocation containing zero
# campaign members; that global row is not evidence that it classified L999 as campaign content.
grep -q '"campaign": false' "$tmp/broken-report.json" \
  || fail "criterion 15b/14: report did not classify L999 as non-campaign"
grep -q 'SKIPPED(non-campaign)' "$tmp/broken.out" \
  || fail "criterion 15b/14: L999 did not skip campaign novelty"
[ "$(grep -c '^BLOCKING: campaign' "$tmp/broken.out")" = "1" ] \
  || fail "criterion 15b/14: unexpected campaign-specific blocking noise"
grep -q '^BLOCKING: campaign — campaign corpus 0/60' "$tmp/broken.out" \
  || fail "criterion 15b/14: the only campaign block was not the global empty-campaign count"
if grep -E -q '^BLOCKING: campaign.*L999' "$tmp/broken.out"; then
  fail "criterion 15b/14: a campaign assertion actually named non-campaign L999"
fi

# 15c: the entry point references no secret and no Unity.
if grep -Enq 'secrets\.|UnityEngine|Unity ' scripts/validate-content.sh; then
  fail "criterion 15c: validate-content.sh references a secret or Unity"
fi

# 15d: zero file-API matches under the validation library (belt on top of check.sh's block).
if grep -rEnq --include='*.cs' '\bSystem\.IO\b' unity/Assets/Scripts/Content/Validation; then
  fail "criterion 15d: file API reference under Content/Validation"
fi

# 17b: --stamp writes exactly meta.validatedAt and no other byte. The exact-60 campaign row is
# intentionally blocking, so exercise the real operation on a full campaign-shaped temp tree.
stamp_corpus="$tmp/stamp/content/levels"
mkdir -p "$stamp_corpus"
cp content/levels/L*.json "$stamp_corpus/"
if ! bash scripts/validate-content.sh --corpus "$stamp_corpus" --stamp > "$tmp/stamp.out" 2>&1; then
  cat "$tmp/stamp.out"
  fail "criterion 17b: --stamp run failed"
fi
[ "$(grep -c '^stamped ' "$tmp/stamp.out")" = "60" ] \
  || fail "criterion 17b: expected exactly 60 stamped campaign files"
for original in content/levels/L*.json; do
  f=$(basename "$original")
  if ! dotnet run --no-build --no-restore --project dotnet/CatMetro.Validator -c Release -- \
      --assert-stamp-diff "$original" "$stamp_corpus/$f" > "$tmp/diff.out" 2>&1; then
    cat "$tmp/diff.out"
    fail "criterion 17b: stamp changed more than the one key in $f"
  fi
done
# ...and the whole stamped campaign still imports and passes (guards the E-C2a-2 date
# regression on every member, not only a hand-picked row).
if ! bash scripts/validate-content.sh --corpus "$stamp_corpus" > "$tmp/restamp.out" 2>&1; then
  cat "$tmp/restamp.out"
  fail "criterion 17b: the stamped campaign no longer validates"
fi

# --- CM-C5.1 appended block (sanctioned by the CM-C5 inheritance ack): dead-newMechanic gate ---
# DM-1c: the Content tree reaches the sim only through LevelSolver/ReplayHasher — zero
# step-symbol references (the observer replays, it never re-implements the scheduler).
if grep -rEnq --include='*.cs' 'Simulation\.Step' unity/Assets/Scripts/Content; then
  fail "CM-C5.1 DM-1c: a step-symbol reference exists under unity/Assets/Scripts/Content"
fi
# DM-7a: the dead-queue fixture FAILS through the real CLI from a full temp campaign tree
# (campaign classification is path-derived), with both artifact-liveness rows naming the level
# and mechanic and no unrelated count/band/solve failure.
mkdir -p "$tmp/dm/content/levels"
cp content/levels/L*.json "$tmp/dm/content/levels/"
cp tests/validation/fixtures/dead-mechanic/L004-dead-queue.json "$tmp/dm/content/levels/L004.json"
if bash scripts/validate-content.sh --corpus "$tmp/dm/content/levels" > "$tmp/dm-dead.out" 2>&1; then
  cat "$tmp/dm-dead.out"
  fail "CM-C5.1 DM-7a: the dead-queue corpus did not fail the gate"
fi
grep -q "BLOCKING: campaign — newMechanic liveness violation .*L004.*queue" "$tmp/dm-dead.out" \
  || fail "CM-C5.1 DM-7a: introduction-liveness block does not name L004 + queue"
grep -q "BLOCKING: campaign — declared-mechanic liveness violation: L004.*queue" "$tmp/dm-dead.out" \
  || fail "CM-C5.1 DM-7a: declared-mechanic block does not name L004 + queue"
[ "$(grep -c '^BLOCKING:' "$tmp/dm-dead.out")" = "2" ] \
  || fail "CM-C5.1 DM-7a: expected exactly two liveness blocks, got $(grep -c '^BLOCKING:' "$tmp/dm-dead.out")"
# DM-7b: the live twin (same board, second wave restored) is the positive control — exit 0
# proves DM-7a fired for liveness and not for the band/order/count limbs. No --stamp here.
cp tests/validation/fixtures/dead-mechanic/L004-live-queue.json "$tmp/dm/content/levels/L004.json"
if ! bash scripts/validate-content.sh --corpus "$tmp/dm/content/levels" > "$tmp/dm-live.out" 2>&1; then
  cat "$tmp/dm-live.out"
  fail "CM-C5.1 DM-7b: the live-queue positive control did not exit 0"
fi
echo "validator.test.sh: CM-C5.1 block OK (DM-1c, DM-7a, DM-7b)"

echo "validator.test.sh: OK (15a-d, 16, 17a-c)"
exit 0
