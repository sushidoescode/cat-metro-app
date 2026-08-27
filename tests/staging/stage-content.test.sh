#!/usr/bin/env bash
# CM-C10 wrapper — proves scripts/stage-content.sh (the single author of the staged derived
# tree, R1/HC-1) against the fixtures in tests/fixtures/staging/, and proves the committed
# staged tree IS the stager's output (criterion 3, anti-tautology) WITHOUT ever invoking the
# byte-identity verifier tests/unity/editmode.test.sh — author and verifier stay separate
# (criterion 8 / stop condition 2). Fixtures are copied to a temp dir before any write-mode
# run (fixtures stay pristine, A-6); the wrapper ends with git status --porcelain empty.
# CM_C10_FIXTURES / CM_C10_SELFTEST: internal knobs for the criterion-7 self-test only —
# the wrapper re-runs itself against a fixture set whose expected failure was repaired and
# asserts that this wrapper then exits non-zero.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
fail() { echo "stage-content.test.sh: FAIL — $1"; exit 1; }
# Dirt is judged against a START snapshot, not against a pristine repo: earlier wrappers in
# scripts/test.sh legitimately rewrite tracked files on a cold checkout (dotnet restore vs
# packages.lock.json), and an uncommitted stager edit must not mask the criterion under test.
# Only dirt that APPEARS during this wrapper's run fails, and the offending paths are printed.
PORCELAIN_START="$(git status --porcelain)"
new_dirt() {
  comm -13 <(printf '%s\n' "$PORCELAIN_START" | LC_ALL=C sort) \
           <(git status --porcelain | LC_ALL=C sort)
}
STAGER=scripts/stage-content.sh
FIX="${CM_C10_FIXTURES:-tests/fixtures/staging}"
TMP=$(mktemp -d "${TMPDIR:-/tmp}/cm-c10-XXXXXX")
trap 'rm -rf "$TMP"' EXIT

[ -f "$STAGER" ] || fail "criterion 1: $STAGER missing"

# Every write-mode invocation goes through this helper: --root "$1" always precedes --apply
apply_to() { bash "$STAGER" --root "$1" --apply; }
check_of() { bash "$STAGER" --root "$1"; }
sha() { shasum -a 256 "$1" | cut -d' ' -f1; }
seed_daily_sources() {
  mkdir -p "$1/config" "$1/content/daily"
  cp config/daily_live.json "$1/config/daily_live.json"
  cp content/daily/precomputed.json "$1/content/daily/precomputed.json"
}

# --- criterion 1a + A-2: default check mode is read-only and green on the clean tree ---
out=$(bash "$STAGER" 2>&1) || fail "criterion 1a: check mode non-zero on the clean tree: $out"
echo "$out" | grep -q "root=$(pwd) " || fail "A-2: effective root not printed (want root=$(pwd))"
echo "$out" | grep -q "dest=$(pwd)/unity/Assets/StreamingAssets" \
  || fail "A-2: default destination not printed"
echo "$out" | grep -q "rule 1: content/levels/\*.json" || fail "criterion 1: rule 1 line missing"
echo "$out" | grep -q "rule 2: config/runtime_bounds.json" || fail "criterion 1: rule 2 line missing"
nd="$(new_dirt)"
[ -z "$nd" ] || fail "criterion 1a: check mode dirtied the tree — new dirt: $nd"

# --- criterion 1b: drift fixture — named, non-zero, NOT repaired (check runs in a temp copy) ---
cp -R "$FIX/drift-bytes" "$TMP/drift"
seed_daily_sources "$TMP/drift"
if out=$(check_of "$TMP/drift" 2>&1); then
  fail "criterion 1b: check mode exited 0 on drift-bytes"
fi
echo "$out" | grep -q "D001.json" || fail "criterion 1b: drifted file not named: $out"
if cmp -s "$TMP/drift/content/levels/D001.json" \
          "$TMP/drift/unity/Assets/StreamingAssets/content/levels/D001.json"; then
  fail "criterion 1b: check mode repaired the fixture (it must write nothing)"
fi

# --- criterion 1c: unknown flag → non-zero + usage ---
if out=$(bash "$STAGER" --frobnicate 2>&1); then fail "criterion 1c: unknown flag accepted"; fi
echo "$out" | grep -qi "usage" || fail "criterion 1c: no usage line on unknown flag"

# --- criterion 2: allowlist, never a sweep — non-shipping siblings must not stage ---
cp -R "$FIX/extra-sources" "$TMP/extra"
seed_daily_sources "$TMP/extra"
apply_to "$TMP/extra" >/dev/null 2>&1 || fail "criterion 2: write mode failed on extra-sources"
esa="$TMP/extra/unity/Assets/StreamingAssets"
leak=$(find "$esa" -name pins.json -o -name validator_thresholds.json -o -name generator_inputs.json)
[ -z "$leak" ] || fail "criterion 2: non-shipping file staged: $leak"
cfgset=$(cd "$esa/config" && ls | grep -v '\.meta$')
[ "$cfgset" = $'daily_live.json\nruntime_bounds.json' ] \
  || fail "criterion 2: staged config payload set is [$cfgset], expected Daily Live + runtime bounds"
[ -f "$esa/content/daily/precomputed.json" ] \
  || fail "criterion 2: Daily precomputed catalog was not staged"

# --- criterion 3: the committed staged tree IS the stager's output (anti-tautology) ---
# real sources -> temp root with an EMPTY destination; then compare payloads vs the real tree.
mkdir -p "$TMP/real/content/levels" "$TMP/real/content/daily" "$TMP/real/config"
cp content/levels/*.json "$TMP/real/content/levels/"
cp config/runtime_bounds.json "$TMP/real/config/"
cp config/daily_live.json "$TMP/real/config/"
cp content/daily/precomputed.json "$TMP/real/content/daily/"
# N1 grant census (both escape classes): snapshot the whole temp root and the source SHAs
# BEFORE the write-mode run — afterwards every ADDED path must live under the staged tree
# and no SOURCE byte may change. Criterion 4 alone cannot see a stager that rewrites its own
# inputs (it compares source vs staged copy, which stay equal), so the source-sha leg is load-bearing.
pre_paths=$(cd "$TMP/real" && find . -type f | LC_ALL=C sort)
pre_src=$(cd "$TMP/real" && find content config -type f -print0 | xargs -0 shasum -a 256 | LC_ALL=C sort)
apply_to "$TMP/real" >/dev/null 2>&1 || fail "criterion 3: write mode failed on the real-source copy"
post_paths=$(cd "$TMP/real" && find . -type f | LC_ALL=C sort)
post_src=$(cd "$TMP/real" && find content config -type f -print0 | xargs -0 shasum -a 256 | LC_ALL=C sort)
escapes=$(comm -13 <(printf '%s\n' "$pre_paths") <(printf '%s\n' "$post_paths") \
  | grep -v '^\./unity/Assets/StreamingAssets/' || true)
[ -z "$escapes" ] || fail "N1 grant: write mode escaped unity/Assets/StreamingAssets/: $escapes"
[ "$pre_src" = "$post_src" ] || fail "N1 grant: write mode modified source files under content/ or config/"
gen="$TMP/real/unity/Assets/StreamingAssets"
real=unity/Assets/StreamingAssets
genset=$(cd "$gen" && find content/levels content/daily config -type f ! -name '*.meta' | sort)
realset=$(cd "$real" && find content/levels content/daily config -type f ! -name '*.meta' | sort)
[ -n "$genset" ] || fail "criterion 3: stager produced nothing"
[ "$genset" = "$realset" ] \
  || fail "criterion 3: payload set drift — generated [$genset] vs committed [$realset]"
for p in $genset; do
  [ "$(sha "$gen/$p")" = "$(sha "$real/$p")" ] || fail "criterion 3: sha drift in $p"
done
# Managed rule-folder metadata is part of the generated artifact too. Compare it explicitly:
# find rooted inside content/daily cannot see the sibling content/daily.meta.
[ -f "$gen/content/daily.meta" ] \
  || fail "criterion 3: stager omitted generated content/daily.meta"
[ -f "$real/content/daily.meta" ] \
  || fail "criterion 3: committed content/daily.meta is missing"
[ "$(sha "$gen/content/daily.meta")" = "$(sha "$real/content/daily.meta")" ] \
  || fail "criterion 3: generated content/daily.meta differs from committed output"

# Artifact mutation: deletion must make check mode fail and apply mode must reconstruct the exact
# deterministic folder meta. Existence in this checkout alone would prove nothing.
cp "$gen/content/daily.meta" "$TMP/daily-folder-meta.first"
rm "$gen/content/daily.meta"
if mout=$(check_of "$TMP/real" 2>&1); then
  fail "criterion 3: check mode stayed green after content/daily.meta was deleted"
fi
echo "$mout" | grep -q 'content/daily.meta' \
  || fail "criterion 3: missing folder meta was not named: $mout"
apply_to "$TMP/real" >/dev/null 2>&1 \
  || fail "criterion 3: apply mode did not restore content/daily.meta"
cmp -s "$TMP/daily-folder-meta.first" "$gen/content/daily.meta" \
  || fail "criterion 3: restored content/daily.meta is not deterministic"
# N-1 (backlog staged-derived-tree class): the staged config payload set is exactly the two
# allowlisted runtime configs.
cfgreal=$(cd "$real/config" && ls | grep -v '\.meta$')
[ "$cfgreal" = $'daily_live.json\nruntime_bounds.json' ] \
  || fail "N-1: real staged config payload set is [$cfgreal], expected Daily Live + runtime bounds"
# .meta coverage on the real tree: every payload has a sibling; no orphan .meta in a rule dir
for p in $realset; do
  [ -f "$real/$p.meta" ] || fail "criterion 3: missing .meta sibling for $p"
done
for m in $(cd "$real" && find content/levels content/daily config -type f -name '*.meta' | sort); do
  [ -f "$real/${m%.meta}" ] || fail "criterion 3: orphan .meta in rule dir: $m"
done

# --- criterion 4: byte copy, never a re-serialization ---
cp -R "$FIX/odd-bytes" "$TMP/odd"
seed_daily_sources "$TMP/odd"
apply_to "$TMP/odd" >/dev/null 2>&1 || fail "criterion 4: write mode failed on odd-bytes"
[ "$(sha "$TMP/odd/content/levels/odd.json")" = "$(sha "$TMP/odd/unity/Assets/StreamingAssets/content/levels/odd.json")" ] \
  || fail "criterion 4: staged bytes differ from source (re-serialization?)"
if grep -Eq 'jq|python3 -m json|json\.dump' "$STAGER"; then
  fail "criterion 4: a JSON parser/formatter appears in the stager"
fi

# --- criterion 5: prune is scoped, both-directional, never destructive outside its pattern ---
cp -R "$FIX/stale-dest" "$TMP/stale"
seed_daily_sources "$TMP/stale"
if cout=$(check_of "$TMP/stale" 2>&1); then fail "criterion 5: check mode green on stale-dest"; fi
echo "$cout" | grep -q "F999.json" || fail "criterion 5a: stale file not reported by check mode"
echo "$cout" | grep -q "catalog.json" && fail "criterion 5c: out-of-rule-dir sentinel reported by check mode"
aout=$(apply_to "$TMP/stale" 2>&1); applyrc=$?
sd="$TMP/stale/unity/Assets/StreamingAssets"
[ ! -f "$sd/content/levels/F999.json" ] || fail "criterion 5a: stale payload not pruned"
[ ! -f "$sd/content/levels/F999.json.meta" ] || fail "criterion 5a: stale .meta not pruned"
[ -f "$sd/content/levels/notes.txt" ] || fail "criterion 5b: foreign file was deleted"
echo "$aout" | grep -q "notes.txt" || fail "criterion 5b: foreign file not reported as drift"
[ "$applyrc" -ne 0 ] || fail "criterion 5b: write mode exited 0 while foreign drift remains"
for s in catalog.json daily_backup_pool.json; do
  cmp -s "$sd/content/$s" "$FIX/stale-dest/unity/Assets/StreamingAssets/content/$s" \
    || fail "criterion 5c: sentinel $s changed"
  echo "$aout" | grep -q "$s" && fail "criterion 5c: sentinel $s reported"
done

# --- criterion 6: .meta preserved always; generated only when absent, deterministically ---
cp -R "$FIX/meta-cases" "$TMP/meta"
seed_daily_sources "$TMP/meta"
apply_to "$TMP/meta" >/dev/null 2>&1 || fail "criterion 6: write mode failed on meta-cases"
md="$TMP/meta/unity/Assets/StreamingAssets"
cmp -s "$md/content/levels/M001.json.meta" "$FIX/meta-cases/unity/Assets/StreamingAssets/content/levels/M001.json.meta" \
  || fail "criterion 6a: existing .meta rewritten (sentinel guid must survive)"
[ -f "$md/content/levels/M002.json.meta" ] || fail "criterion 6b: no .meta generated for new payload"
cp "$md/content/levels/M002.json.meta" "$TMP/m2.first"
apply_to "$TMP/meta" >/dev/null 2>&1 || fail "criterion 6b: second write-mode run failed"
cmp -s "$TMP/m2.first" "$md/content/levels/M002.json.meta" \
  || fail "criterion 6b: generated .meta unstable across consecutive runs"
head -1 "$md/content/levels/M002.json.meta" | grep -q 'fileFormatVersion: 2' \
  || fail "criterion 6b: generated .meta not in the shipped shape"
# 6c: second, independent implementation of the derivation (python3 hashlib vs shasum)
pyguid() {
  python3 -c 'import hashlib,sys; print(hashlib.sha256(("catmetro-meta-v1:"+sys.argv[1]).encode("utf-8")).hexdigest()[:32])' "$1"
}
mguid() { grep '^guid:' "$1" | cut -d' ' -f2; }
[ "$(mguid "$md/content/levels/M002.json.meta")" = "$(pyguid content/levels/M002.json)" ] \
  || fail "criterion 6c: guid derivation disagrees for content/levels/M002.json"
[ "$(mguid "$TMP/odd/unity/Assets/StreamingAssets/content/levels/odd.json.meta")" = "$(pyguid content/levels/odd.json)" ] \
  || fail "criterion 6c: guid derivation disagrees for content/levels/odd.json"
[ "$(mguid "$TMP/odd/unity/Assets/StreamingAssets/config/runtime_bounds.json.meta")" = "$(pyguid config/runtime_bounds.json)" ] \
  || fail "criterion 6c: guid derivation disagrees for config/runtime_bounds.json"
[ "$(mguid "$TMP/odd/unity/Assets/StreamingAssets/config/daily_live.json.meta")" = "$(pyguid config/daily_live.json)" ] \
  || fail "criterion 6c: guid derivation disagrees for config/daily_live.json"
[ "$(mguid "$TMP/odd/unity/Assets/StreamingAssets/content/daily/precomputed.json.meta")" = "$(pyguid content/daily/precomputed.json)" ] \
  || fail "criterion 6c: guid derivation disagrees for content/daily/precomputed.json"
[ "$(mguid "$TMP/odd/unity/Assets/StreamingAssets/content/daily.meta")" = "$(pyguid content/daily)" ] \
  || fail "criterion 6c: guid derivation disagrees for content/daily folder"
# 6d: guid uniqueness — explicit root unity/Assets, never "." (A-3: worktree checkouts duplicate guids)
dupes=$(find unity/Assets -name '*.meta' -exec grep -h '^guid:' {} + | sort | uniq -d)
[ -z "$dupes" ] || fail "criterion 6d: duplicate guids under unity/Assets: $dupes"

# --- criterion 8: the verifier is untouched and never calls the author ---
# Resolve the base ref in freshness order: origin/main (a clone may carry a stale local
# main), then main, then a shallow fetch (CI checks out a detached PR head with NO main
# ref at all — actions/checkout default). Never skip silently: no base is a hard fail.
base=$(git rev-parse -q --verify origin/main 2>/dev/null \
  || git rev-parse -q --verify main 2>/dev/null || true)
if [ -z "$base" ]; then
  git fetch --quiet --depth=1 origin main 2>/dev/null \
    || fail "criterion 8: no origin/main, no main, and 'git fetch origin main' failed — base unresolvable"
  base=$(git rev-parse -q --verify FETCH_HEAD) \
    || fail "criterion 8: FETCH_HEAD unresolvable after fetching origin main"
fi
if mb=$(git merge-base HEAD "$base" 2>/dev/null); then
  git diff --quiet "$mb" -- tests/unity/editmode.test.sh \
    || fail "criterion 8: tests/unity/editmode.test.sh differs from merge-base $mb"
else
  # Shallow CI clone: the base tip resolved but shares no fetched history, so no merge-base
  # exists. Compare the verifier blob at the two tips directly — strictly STRONGER (also
  # goes red if main itself moved the verifier since the branch's last rebase, which the
  # serial update-branch-before-merge protocol resolves).
  git diff --quiet HEAD "$base" -- tests/unity/editmode.test.sh \
    || fail "criterion 8: tests/unity/editmode.test.sh differs from the base tip $base (shallow clone — rebase over main or deepen history)"
fi
c1=$(grep -c 'stage-content' tests/unity/editmode.test.sh || true)
c2=$(grep -c 'stage-content' scripts/check.sh || true)
[ "$c1" = "0" ] && [ "$c2" = "0" ] \
  || fail "criterion 8: verifier or check.sh references the author (counts $c1/$c2)"
badapply=$(grep -n -- '--apply' tests/staging/*.test.sh | grep -v -- '--root' || true)
[ -z "$badapply" ] || fail "criterion 8: a write-mode line lacks --root: $badapply"

# --- criterion 9: the build gate fails closed on staged drift ---
bash scripts/build.sh >/dev/null 2>&1 || fail "criterion 9: build gate non-zero on the clean tree"
if bout=$(bash scripts/build.sh --root "$TMP/drift" 2>&1); then
  fail "criterion 9: build gate green on the drifted fixture"
fi
echo "$bout" | grep -q "D001.json" || fail "criterion 9: build gate does not name the drifted file"
if bash scripts/build.sh --apply --root "$TMP/extra" >/dev/null 2>&1; then
  fail "criterion 9: build gate accepted the write-mode flag (it must run check-only)"
fi

# --- criterion 7 self-test: turn a fixture's expected failure into a success -> wrapper goes red ---
if [ -z "${CM_C10_SELFTEST:-}" ]; then
  cp -R "$FIX" "$TMP/fixflip"
  cp "$TMP/fixflip/drift-bytes/content/levels/D001.json" \
     "$TMP/fixflip/drift-bytes/unity/Assets/StreamingAssets/content/levels/D001.json"
  if CM_C10_SELFTEST=1 CM_C10_FIXTURES="$TMP/fixflip" bash tests/staging/stage-content.test.sh >/dev/null 2>&1; then
    fail "criterion 7: wrapper stayed green after drift-bytes' expected failure became a success"
  fi
fi

nd="$(new_dirt)"
[ -z "$nd" ] || fail "criterion 7: wrapper left the tree dirty — new dirt: $nd"
echo "stage-content.test.sh: OK (criteria 1-6 fixture legs, 3 anti-tautology + N-1 config set, 7 self-test, 8 verifier-untouched, 9 build gate; tree clean)"
exit 0
