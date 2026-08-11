#!/usr/bin/env bash
# CM-C6 harness wrapper (criterion 10): exits 0 iff the Daily dotnet-test leg is green AND the
# two-run artifact diff holds (criterion 7) AND every [CI] grep of the contract holds. Each
# check is labelled; any failure exits 1 with the label. The scripts/test.sh summary-numbers
# comparison is the PR evidence procedure (CM-C2a criterion 13 precedent, handoff A-C2a-10) —
# this wrapper cannot invoke scripts/test.sh without recursing.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
tmp="${TMPDIR:-/tmp}/cm-c6-wrapper-$$"
mkdir -p "$tmp"
trap 'rm -rf "$tmp"' EXIT
fail() { echo "daily-pipeline.test.sh: FAIL — $1"; exit 1; }
daily_root="unity/Assets/Scripts/Content/Daily"
seed_rx='^DAILY_SEED [0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]+ [0-9]+$'

# Review F2: every grep below fails CLOSED — a moved scan root must fail this wrapper, not
# silence it (the check.sh scan_banned posture; cf. commit ee637c9's fail-open removal).
[ -d "$daily_root" ] || fail "scan root $daily_root does not exist (fail-closed)"
[ -n "$(ls unity/Assets/Tests/EditMode/Pure/Daily/*.cs 2>/dev/null)" ] \
  || fail "criterion 10: Daily NUnit sources missing (fail-closed)"

# Criterion 10: the dotnet leg is green — full suite, UNFILTERED (review F1: a namespace filter
# matching zero tests still exits 0, making the gate vacuous; the sibling wrappers set the
# unfiltered precedent, and the source-presence guard above closes the dropped-folder hole).
if ! dotnet test dotnet/CatMetro.sln -c Release --nologo > "$tmp/test.out" 2>&1; then
  tail -20 "$tmp/test.out"
  fail "criterion 10: dotnet test not green"
fi

# Criteria 6b + 7 (+ the criterion-3 smoke instance): two 30-date runs through the host are
# byte-identical, and each prints exactly one anchored DAILY_SEED line per date with nothing
# else DAILY_SEED-shaped.
if ! bash scripts/validate-dailies.sh --days 30 --out "$tmp/a.json" > "$tmp/run1.out" 2>"$tmp/run1.err"; then
  cat "$tmp/run1.err"
  fail "criterion 6: validate-dailies.sh run 1 exited non-zero"
fi
if ! bash scripts/validate-dailies.sh --days 30 --out "$tmp/b.json" > "$tmp/run2.out" 2>"$tmp/run2.err"; then
  cat "$tmp/run2.err"
  fail "criterion 6: validate-dailies.sh run 2 exited non-zero"
fi
cmp -s "$tmp/a.json" "$tmp/b.json" || fail "criterion 7: artifacts differ across two identical runs"

n=$(grep -cE "$seed_rx" "$tmp/run1.out") || true
[ "$n" -eq 30 ] || fail "criterion 6b: expected 30 anchored DAILY_SEED lines, found $n"
stray=$(grep -E '^DAILY_SEED' "$tmp/run1.out" | grep -vcE "$seed_rx") || true
[ "$stray" -eq 0 ] || fail "criterion 6b: a DAILY_SEED-prefixed line breaks the anchored format"
dk=$(grep -E "$seed_rx" "$tmp/run1.out" | awk '{print $2}' | sort -u | wc -l | tr -d ' ')
[ "$dk" -eq 30 ] || fail "criterion 6b: expected 30 distinct dateKeys, found $dk"

# Criterion 6a (file half): the artifact carries one record per date and the open NEW-Q21 pin
# (the curve file must not exist — criterion 9 / stop condition 3).
[ -s "$tmp/a.json" ] || fail "criterion 6: no artifact written"
recs=$(grep -c '"dateKey"' "$tmp/a.json")
[ "$recs" -eq 30 ] || fail "criterion 6a: expected 30 dateKey records in the artifact, found $recs"
[ ! -f config/daily_weekday_curve.json ] || fail "criterion 9: daily_weekday_curve.json exists — NEW-Q21 is open, no agent may commit a curve"
grep -q 'UNCONFIGURED(NEW-Q21)' "$tmp/a.json" || fail "criterion 9: the artifact must print the open NEW-Q21 pin"
grep -q '"exitFailure": false' "$tmp/a.json" || fail "criterion 5: exitFailure not false on the green stub corpus"

# Criterion 2: the appended check.sh block fires on the negative fixture — and names itself.
if bash scripts/check.sh --root tests/fixtures/daily-bad > "$tmp/neg.out" 2>&1; then
  fail "criterion 2: check.sh --root tests/fixtures/daily-bad did not fail"
fi
grep -q "Daily clock ban" "$tmp/neg.out" || fail "criterion 2: the Daily clock-ban block did not fire"

# Criterion 6c: zero file-API matches under the Daily root (belt on top of check.sh's block).
if grep -rEnq --include='*.cs' '\bSystem\.IO\b' "$daily_root"; then
  fail "criterion 6c: file API reference under the Daily root"
fi

# Criterion 8b: Q3 authorizes exactly one shipped IBoardFactory implementation, owned by
# DailyBoardFactory.cs. Keep Review F3's whitespace-flattening so a wrapped base list cannot
# evade the declaration scan, count declarations rather than files, and fail closed on every
# scan/read error. Plain references and the interface declaration remain legal.
if ! find "$daily_root" -type f -name '*.cs' -print > "$tmp/daily-cs.list"; then
  fail "criterion 8b: could not enumerate Daily C# sources (fail-closed)"
fi
impl_count=0
impl_path=""
while IFS= read -r f; do
  [ -n "$f" ] || continue
  flat=$(tr '\n\t' '  ' < "$f") \
    || fail "criterion 8b: could not flatten $f (fail-closed)"
  matches=$(printf '%s\n' "$flat" \
    | grep -oE '(class|struct)[^:{]*:[^{]*IBoardFactory')
  scan_status=$?
  [ "$scan_status" -le 1 ] \
    || fail "criterion 8b: declaration scan failed for $f (fail-closed)"
  if [ "$scan_status" -eq 0 ]; then
    match_count=$(printf '%s\n' "$matches" | awk 'NF { n++ } END { print n + 0 }') \
      || fail "criterion 8b: could not count declarations in $f (fail-closed)"
    impl_count=$((impl_count + match_count))
    impl_path="$f"
  fi
done < "$tmp/daily-cs.list"
[ "$impl_count" -eq 1 ] \
  || fail "criterion 8b: expected exactly one IBoardFactory implementation, found $impl_count"
case "$impl_path" in
  */DailyBoardFactory.cs) ;;
  *) fail "criterion 8b: sole IBoardFactory implementation is not DailyBoardFactory.cs: $impl_path" ;;
esac

# Criterion 11: scope guard over the Daily root + the entry script — no backend, no store, no
# push, no analytics, no clock, no engine, no network...
guard='Http|WebRequest|UnityEngine|DateTime|IClock|OneSignal|RevenueCat|Firebase'
hits=$(grep -rEn "$guard" "$daily_root" scripts/validate-dailies.sh || true)
[ -z "$hits" ] || fail "criterion 11: scope-guard token found: $hits"
# ...with the negative fixture proving the pattern fires at all.
grep -rEq "$guard" tests/fixtures/daily-bad || fail "criterion 11: scope-guard pattern failed to fire on the negative fixture"

# Criterion 3c/3d: the horizon is read from the config row (which equals 90), never hard-coded.
grep -q '"DAILY_PREVALIDATION_DAYS": 90' config/daily_pipeline.json \
  || fail "criterion 3d: config row DAILY_PREVALIDATION_DAYS != 90"
lit=$(grep -rEn --include='*.cs' --exclude-dir=obj --exclude-dir=bin '\b90\b' \
  "$daily_root" dotnet/CatMetro.DailyTools || true)
[ -z "$lit" ] || fail "criterion 3c: hard-coded horizon literal: $lit"

echo "daily-pipeline.test.sh: OK (2, 3c-d smoke, 5, 6a-c, 7, 8b, 9-file, 10, 11)"
exit 0
