#!/usr/bin/env python3
"""Validate one ladder quartet against authored bytes and the canonical report."""

import json
import sys
from pathlib import Path


def fail(message):
    print(Path(sys.argv[0]).name + ": FAIL — " + message)
    raise SystemExit(1)


if len(sys.argv) < 6:
    fail("usage: helper REPORT MECHANIC BAND LEVEL...")

report_path, mechanic, expected_band, *band = sys.argv[1:]
with open(report_path, encoding="utf-8") as source:
    report = json.load(source)

authoritative = sorted(Path("content/levels").glob("L*.json"))
staged = sorted(Path("unity/Assets/StreamingAssets/content/levels").glob("L*.json"))
expected_names = ["L%03d.json" % number for number in range(1, 61)]
if [path.name for path in authoritative] != expected_names:
    fail("authoritative campaign is not exactly contiguous L001-L060")
if [path.name for path in staged] != expected_names:
    fail("StreamingAssets campaign is not exactly contiguous L001-L060")
for source, mirror in zip(authoritative, staged):
    if source.read_bytes() != mirror.read_bytes():
        fail(source.name + ": StreamingAssets bytes differ")

campaign = [level for level in report["levels"] if level.get("campaign")]
if len(campaign) != 60:
    fail("validator report does not contain exactly 60 campaign rows")
levels = {level["id"]: level for level in campaign}


def stage(level, name):
    rows = [row for row in level["stages"] if row["stage"] == name]
    if len(rows) != 1:
        fail(level["id"] + ": expected one " + name + " row")
    return rows[0]


declared_prefix = "tag=CM-LADDER-declared-mechanics:"
declared_rows = {
    row["value"][len(declared_prefix):].split(";", 1)[0]: row
    for row in report["campaign"]
    if row["value"].startswith(declared_prefix)
}
for level_id in band:
    with open("content/levels/" + level_id + ".json", encoding="utf-8") as source:
        authored = json.load(source)
    if authored["meta"]["band"] != expected_band:
        fail(level_id + ": wrong band " + str(authored["meta"]["band"]))
    if mechanic not in authored["meta"]["mechanics"]:
        fail(level_id + ": does not declare " + mechanic)

    level = levels.get(level_id)
    if level is None:
        fail(level_id + ": missing validator row")
    if any(row["blocks"] for row in level["stages"]):
        fail(level_id + ": a per-level stage blocks")
    if stage(level, "Schema")["code"] != "Pass":
        fail(level_id + ": schema did not pass")
    if stage(level, "StaticAnalysis")["code"] not in ("Pass", "Warn"):
        fail(level_id + ": static analysis is neither Pass nor non-blocking Warn")
    if stage(level, "TrivialityReject")["code"] != "Pass":
        fail(level_id + ": zero-input run was not rejected")
    if stage(level, "BrittlenessAccessibility")["code"] != "Pass":
        fail(level_id + ": brittleness did not pass")
    solve = level.get("solve")
    if not solve or solve["verdict"] != "Solved" or solve["beamWidthUsed"] != 0:
        fail(level_id + ": missing exact beam-0 winning proof")
    witness = declared_rows.get(level_id)
    if (not witness or witness["code"] != "Pass" or witness["blocks"]
            or "exercised=true" not in witness["value"]
            or mechanic + "=true(" not in witness["value"]):
        fail(level_id + ": winning replay does not exercise " + mechanic)

for tag in ("tag=CM-R06.2", "tag=CM-R09.1", "tag=CM-R09.3",
            "tag=CM-LADDER-solve-proof"):
    rows = [row for row in report["campaign"] if row["value"] == tag]
    if len(rows) != 1 or rows[0]["code"] != "Pass" or rows[0]["blocks"]:
        fail("campaign assertion is not Pass: " + tag)

print(Path(sys.argv[0]).name + ": PASS " + " ".join(band))
