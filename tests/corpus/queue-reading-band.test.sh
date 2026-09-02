#!/usr/bin/env bash
# CM-C12 wrapper — the queue-reading band (L011-L017) gate: mirrors
# tests/corpus/alternation-band.test.sh's structure and rationale. Re-runs
# `bash scripts/validate-content.sh`
# and independently parses its machine report — deliberate duplication of the ~40s+ gate cost,
# same tradeoff CM-C11's own wrapper accepted (an independently produced, independently parsed
# report is the point). Criteria the C# NUnit fixtures already cover (progression-row fields,
# BFS-exactness/Solved, static analysis, queue-liveness positive evidence, reachable-failure
# witnesses) ride the `dotnet test` legs invoked elsewhere in scripts/test.sh; this wrapper does
# not re-invoke `dotnet test` or `tests/unity/editmode.test.sh` for the same zero-new-evidence
# reason CM-C11's wrapper recorded.
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
fail() { echo "queue-reading-band.test.sh: FAIL — $1"; exit 1; }
tmp=$(mktemp -d "${TMPDIR:-/tmp}/cm-c12-wrapper-XXXXXX") || fail "mktemp failed"
trap 'rm -rf "$tmp"' EXIT

# Dirt is judged against a START snapshot, not an absolute-clean tree (CM-C10/CM-C11 precedent):
# earlier wrappers in scripts/test.sh legitimately rewrite tracked files on a cold checkout, and
# this criterion's read-only check-mode run cannot itself introduce dirt.
PORCELAIN_START="$(git status --porcelain)"
new_dirt() {
  comm -13 <(printf '%s\n' "$PORCELAIN_START" | LC_ALL=C sort) \
           <(git status --porcelain | LC_ALL=C sort)
}

BAND="L011 L012 L013 L014 L015 L016 L017"

# --- minActionWindowTicks == 12 declared in all seven authored files ---
for id in $BAND; do
  grep -qE '"minActionWindowTicks": 12([^0-9]|$)' "content/levels/$id.json" \
    || fail "content/levels/$id.json does not declare minActionWindowTicks: 12"
done

# --- band declared as queue-reading with mechanics switch+queue, no newMechanic, in all seven ---
for id in $BAND; do
  grep -qE '"band": "queue-reading"' "content/levels/$id.json" \
    || fail "content/levels/$id.json does not declare band: queue-reading"
  grep -qE '"newMechanic": null' "content/levels/$id.json" \
    || fail "content/levels/$id.json does not declare newMechanic: null"
done

# --- run the full gate (campaign + stress boards), parse the machine report ---
if ! bash scripts/validate-content.sh --out "$tmp/report.json" > "$tmp/gate.out" 2>&1; then
  cat "$tmp/gate.out"
  fail "the full corpus gate did not exit 0"
fi

BAND="$BAND" python3 - "$tmp/report.json" <<'PYEOF'
import json, os, re, sys
from pathlib import Path

report_path = sys.argv[1]
band = os.environ["BAND"].split()
with open(report_path) as f:
    report = json.load(f)

def fail(msg):
    print("queue-reading-band.test.sh: FAIL — " + msg)
    sys.exit(1)

authoritative_files = sorted(Path("content/levels").glob("L*.json"), key=lambda path: path.name)
if not authoritative_files:
    fail("no authoritative L*.json campaign files discovered")
invalid_names = [path.name for path in authoritative_files
                 if re.fullmatch(r"L[0-9]{3}\.json", path.name) is None]
if invalid_names:
    fail("campaign filenames do not match LNNN.json: " + ", ".join(invalid_names))

authoritative_ids = []
for path in authoritative_files:
    with path.open(encoding="utf-8") as source:
        declared_id = json.load(source).get("id")
    if declared_id != path.stem:
        fail(path.name + ": declared id does not match filename: " + str(declared_id))
    authoritative_ids.append(declared_id)
if len(set(authoritative_ids)) != len(authoritative_ids):
    fail("duplicate declared campaign ids: " + ", ".join(authoritative_ids))
expected_ids = ["L%03d" % number for number in range(1, len(authoritative_ids) + 1)]
if authoritative_ids != expected_ids:
    fail("campaign ids are not contiguous from L001: " + ", ".join(authoritative_ids))

staged_files = sorted(Path("unity/Assets/StreamingAssets/content/levels").glob("L*.json"),
                      key=lambda path: path.name)
if [path.name for path in staged_files] != [path.name for path in authoritative_files]:
    fail("StreamingAssets campaign filenames differ from the authoritative corpus")
for source, staged in zip(authoritative_files, staged_files):
    if source.read_bytes() != staged.read_bytes():
        fail(source.name + ": StreamingAssets bytes differ from authoritative content")

campaign_levels = [lvl for lvl in report["levels"] if lvl.get("campaign")]
report_campaign_ids = [lvl["id"] for lvl in campaign_levels]
if len(set(report_campaign_ids)) != len(report_campaign_ids):
    fail("the validator report contains duplicate campaign ids")
if sorted(report_campaign_ids) != authoritative_ids:
    fail("the validator campaign set differs from discovered authoritative content")
levels = {lvl["id"]: lvl for lvl in report["levels"]}

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
    # Tightened from Pass-or-Warn to Pass-only (round-1 review Minor-5, propagating PR #75's own
    # Important-3 tightening this wrapper had missed on branch): all seven levels are Warn-free.
    if st["code"] != "Pass":
        fail(lid + " static-analysis stage not Pass: " + st["detail"])

    solve = lvl.get("solve")
    if solve is None:
        fail(lid + ": report carries no solve block")
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
        fail(lid + ": report carries no stage-6 value")

    # retention=<x> (wins=W losses=L pinned=P) windows=[...]  (ValidationStages.cs:507-508)
    m = re.search(r"wins=(\d+) losses=(\d+) pinned=(\d+)", value)
    if not m:
        fail(lid + ": stage-6 value has no wins/losses/pinned triple: " + value)
    w, l, p = (int(x) for x in m.groups())
    if w + l == 0:
        fail(lid + ": all-pinned brittleness sample, cannot read retention: " + value)
    optimistic = w * 100 // (w + l)
    pessimistic = w * 100 // (w + l + p)
    # Unlike CM-C11's L006, none of L011-L017 is a byte-locked anchor to a pre-fix authored
    # source — no anchor exemption is needed here; both readings must clear 70% for all seven.
    if optimistic < 70:
        fail(lid + " optimistic retention %d%% < 70%% — %s" % (optimistic, value))
    if pessimistic < 70:
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
proof = next((v for v in report["campaign"] if v["value"] == "tag=CM-LADDER-solve-proof"), None)
if proof is None or proof["code"] != "Pass":
    fail("campaign exact-solve proof not Pass: " + (proof["detail"] if proof else "MISSING"))
count = next((v for v in report["campaign"] if v["value"] == "tag=CM-R09.1"), None)
expected_count = "%d/60" % len(authoritative_ids)
if count is None or expected_count not in count["detail"]:
    fail("campaign count row does not read " + expected_count + ": "
         + (count["detail"] if count else "MISSING"))

print("queue-reading-band.test.sh: python report checks OK")
PYEOF
py_rc=$?
[ "$py_rc" -eq 0 ] || fail "python report checks failed (rc=$py_rc, see FAIL line(s) above)"

# --- L001-L010 byte-unchanged by this band (the whole prior shipped corpus, onboarding+alternation) ---
c8_base=$(git merge-base HEAD origin/main 2>/dev/null || git merge-base HEAD main)
dirty=$(git diff --stat "$c8_base" -- content/levels/L001.json content/levels/L002.json \
  content/levels/L003.json content/levels/L004.json content/levels/L005.json \
  content/levels/L006.json content/levels/L007.json content/levels/L008.json \
  content/levels/L009.json content/levels/L010.json)
[ -z "$dirty" ] || fail "a shipped L001-L010 level moved: $dirty"

# --- the stager's check mode already equals the committed staged tree ---
if ! bash scripts/stage-content.sh > "$tmp/stage.out" 2>&1; then
  cat "$tmp/stage.out"
  fail "stage-content.sh check mode found drift against the committed staged tree"
fi
new=$(new_dirt)
[ -z "$new" ] || fail "check-mode run left new dirt (paths): $new"

echo "queue-reading-band.test.sh: OK (schema/gate/retention/windows/liveness/order/band-table/count/L001-L010-unchanged/staging all pass this run; progression-row fields, BFS-exactness, queue-liveness positive evidence and reachable-failure witnesses ride the dotnet Corpus suite)"
