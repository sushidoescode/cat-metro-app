#!/usr/bin/env bash
# CM-C11 wrapper — the alternation band (L006-L010) gate: criteria 3, 4, 5, 8, 9(a)/9(c), 10 as
# one tool-invocation-level proof over `bash scripts/validate-content.sh`'s machine report, plus
# the staged derived tree's check-mode leg. Criteria 1, 2, 6 and 7 are NUnit cases in
# unity/Assets/Tests/EditMode/Pure/Corpus/*.cs, exercised by the `dotnet test` legs that already
# run elsewhere in this suite (tests/content/importer.test.sh and others invoke the whole
# solution) — this wrapper does not re-run `dotnet test` itself (would be a second full pass for
# no new evidence). It also does not re-invoke tests/unity/editmode.test.sh (criterion 9(b)'s
# Unity EditMode/PlayMode run): that wrapper already runs in the same `scripts/test.sh` pass and
# a second Unity batchmode boot would double an already multi-minute cost for zero new evidence
# (stop condition 7 — hostile wall clock). Criterion 9(c) (git diff --name-only naming exactly
# ten new StreamingAssets paths) is a PR-evidence check against the branch's merge-base, not a
# repeatable post-merge assertion, and is pasted in the PR description instead.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
fail() { echo "alternation-band.test.sh: FAIL — $1"; exit 1; }
tmp="${TMPDIR:-/tmp}/cm-c11-wrapper-$$"
mkdir -p "$tmp"
trap 'rm -rf "$tmp"' EXIT

# Dirt is judged against a START snapshot, not an absolute-clean tree (CM-C10's own wrapper
# precedent, tests/staging/stage-content.test.sh): earlier wrappers in scripts/test.sh legitimately
# rewrite tracked files on a cold checkout (dotnet restore vs packages.lock.json), and criterion
# 9a's read-only check-mode run cannot itself introduce dirt — only pre-existing dirt could make
# git status non-empty afterward, and that is not this criterion's concern.
PORCELAIN_START="$(git status --porcelain)"
new_dirt() {
  comm -13 <(printf '%s\n' "$PORCELAIN_START" | LC_ALL=C sort) \
           <(git status --porcelain | LC_ALL=C sort)
}

BAND="L006 L007 L008 L009 L010"

# --- criterion 5a: minActionWindowTicks == 12 declared in all five authored files ---
for id in $BAND; do
  grep -q '"minActionWindowTicks": 12' "content/levels/$id.json" \
    || fail "criterion 5: content/levels/$id.json does not declare minActionWindowTicks: 12"
done

# --- criteria 3/4/5b/8/10: run the full gate (campaign + stress boards), parse the report ---
if ! bash scripts/validate-content.sh --out "$tmp/report.json" > "$tmp/gate.out" 2>&1; then
  cat "$tmp/gate.out"
  fail "the full corpus gate did not exit 0"
fi

BAND="$BAND" python3 - "$tmp/report.json" <<'PYEOF'
import json, os, re, sys

report_path = sys.argv[1]
band = os.environ["BAND"].split()
with open(report_path) as f:
    report = json.load(f)

levels = {lvl["id"]: lvl for lvl in report["levels"]}

def fail(msg):
    print("alternation-band.test.sh: FAIL — " + msg)
    sys.exit(1)

def stage(lvl, name):
    for s in lvl["stages"]:
        if s["stage"] == name:
            return s
    fail(lvl["id"] + ": no stage row named " + name)

for lid in band:
    if lid not in levels:
        fail(lid + " missing from the report")
    lvl = levels[lid]

    sch = stage(lvl, "Schema")
    if sch["code"] != "Pass":
        fail(lid + " schema stage not Pass: " + sch["detail"])

    st = stage(lvl, "StaticAnalysis")
    if st["code"] not in ("Pass", "Warn"):
        fail(lid + " static-analysis stage: " + st["detail"])

    solve = lvl.get("solve")
    if solve is None:
        fail(lid + ": report carries no solve block (criterion 10)")
    if solve["verdict"] != "Solved":
        fail(lid + " solve.verdict != Solved: " + solve["verdict"])
    if solve["beamWidthUsed"] != 0:
        fail(lid + " solve.beamWidthUsed != 0 (not BFS-exact): " + str(solve["beamWidthUsed"]))

    triv = stage(lvl, "TrivialityReject")
    if triv["code"] != "Pass":
        fail(lid + " triviality stage not Pass: " + triv["detail"])

    britt = stage(lvl, "BrittlenessAccessibility")
    if britt["code"] != "Pass":
        fail(lid + " brittleness stage not Pass: " + britt["detail"])
    value = britt.get("value")
    if not value:
        fail(lid + ": report carries no stage-6 value (criterion 10)")

    # retention=<x> (wins=W losses=L pinned=P) windows=[...]  (ValidationStages.cs:507-508)
    m = re.search(r"wins=(\d+) losses=(\d+) pinned=(\d+)", value)
    if not m:
        fail(lid + ": stage-6 value has no wins/losses/pinned triple: " + value)
    w, l, p = (int(x) for x in m.groups())
    if w + l == 0:
        fail(lid + ": all-pinned brittleness sample, cannot read retention: " + value)
    optimistic = w * 100 // (w + l)
    pessimistic = w * 100 // (w + l + p)
    if optimistic < 70:
        fail(lid + " optimistic retention %d%% < 70%% — %s" % (optimistic, value))
    if lid == "L006":
        # Human criterion-4(b) ruling, 2026-08-08 (CM-C11.md, RULING section — option 1):
        # the pessimistic bar applies to L007-L010; L006's reading is pinned as the
        # recorded characteristic of the authored anchor (mirrors the NUnit pin in
        # AlternationBandTests) so drift cannot pass silently.
        if (w, l, p) != (7, 0, 13):
            fail(lid + " anchor characteristic drifted (expected wins=7 losses=0 pinned=13): " + value)
        if pessimistic != 35:
            fail(lid + " pessimistic characteristic drifted (expected 35): " + value)
    elif pessimistic < 70:
        fail(lid + " pessimistic retention %d%% < 70%% — %s" % (pessimistic, value))
    print(lid + ": retention optimistic=%d%% pessimistic=%d%% (%s)" % (optimistic, pessimistic, value))

    wm = re.search(r"windows=\[([^\]]*)\]", value)
    if not wm:
        fail(lid + ": stage-6 value has no windows array: " + value)
    windows_str = wm.group(1)
    windows = [int(x) for x in windows_str.split(",")] if windows_str.strip() else []
    if not windows:
        fail(lid + ": windows array is empty (vacuous window law)")
    if any(w2 < 12 for w2 in windows):
        fail(lid + " a window < 12 ticks: " + value)

    liveness = None
    for v in report["campaign"]:
        if v["value"].startswith("tag=CM-R06.2-liveness:" + lid):
            liveness = v
            break
    if liveness is None:
        fail(lid + ": no CM-C5.1 liveness row in the campaign block")
    if liveness["detail"] != "SKIPPED(no declared newMechanic)":
        fail(lid + " liveness verdict is not SKIPPED(no declared newMechanic): " + liveness["detail"])
    if liveness["blocks"]:
        fail(lid + " liveness row blocks — the joint-note decision moved (stop condition 9)")

order = next((v for v in report["campaign"] if v["value"] == "tag=CM-R06.2"), None)
if order is None or order["code"] != "Pass":
    fail("campaign mechanic-order verdict not Pass: " + (order["detail"] if order else "MISSING"))
bandv = next((v for v in report["campaign"] if v["value"] == "tag=CM-R09.3"), None)
if bandv is None or bandv["code"] != "Pass":
    fail("campaign band-table verdict not Pass: " + (bandv["detail"] if bandv else "MISSING"))
count = next((v for v in report["campaign"] if v["value"] == "tag=CM-R09.1"), None)
if count is None or "10/30" not in count["detail"]:
    fail("campaign count row does not read 10/30: " + (count["detail"] if count else "MISSING"))

print("alternation-band.test.sh: python report checks OK (3,4,5b,8,10)")
PYEOF
py_rc=$?
[ "$py_rc" -eq 0 ] || fail "python report checks failed (rc=$py_rc, see FAIL line(s) above)"

# --- criterion 8: L001-L005 byte-unchanged by this band ---
dirty=$(git diff --stat -- content/levels/L001.json content/levels/L002.json content/levels/L003.json \
  content/levels/L004.json content/levels/L005.json)
[ -z "$dirty" ] || fail "criterion 8: a shipped L001-L005 level moved: $dirty"

# --- criterion 9(a): the stager's check mode already equals the committed staged tree ---
if ! bash scripts/stage-content.sh > "$tmp/stage.out" 2>&1; then
  cat "$tmp/stage.out"
  fail "criterion 9a: stage-content.sh check mode found drift against the committed staged tree"
fi
new=$(new_dirt)
[ -z "$new" ] || fail "criterion 9a: check-mode run left new dirt (paths): $new"

echo "alternation-band.test.sh: OK (3,4,5,8,9a rides this run; 1/2/6/7 ride the dotnet Corpus suite; 9b rides tests/unity/editmode.test.sh; 9c/10/11 are PR evidence)"
