#!/usr/bin/env python3
"""Parse VSTest console summaries without accepting a vacuous green run.

The solution gate requests the detailed console logger, whose summary is a
multi-line block. Some SDK/VSTest combinations still emit the older compact
one-line form, so both formats are accepted, but never in the same payload.

On success, print: PASSED FAILED SKIPPED TOTAL RUNS.
"""

from __future__ import annotations

import re
import sys
from dataclasses import dataclass


STATUS = re.compile(r"^Test Run (Successful|Failed)\.$")
TOTAL = re.compile(r"^Total tests:\s*([0-9]+)\s*$")
METRIC = re.compile(r"^\s+(Passed|Failed|Skipped):\s*([0-9]+)\s*$")
COMPACT = re.compile(
    r"^(Passed|Failed)!\s*-\s*"
    r"Failed:\s*([0-9]+),\s*"
    r"Passed:\s*([0-9]+),\s*"
    r"Skipped:\s*([0-9]+),\s*"
    r"Total:\s*([0-9]+)(?:,.*)?$"
)


class SummaryError(ValueError):
    pass


@dataclass(frozen=True)
class Counts:
    passed: int
    failed: int
    skipped: int
    total: int

    def validate(self, status: str) -> None:
        if status != "Passed":
            raise SummaryError("test runner reported failure")
        if self.total <= 0 or self.passed <= 0:
            raise SummaryError("test run was vacuous (no positive passed count)")
        if self.failed != 0:
            raise SummaryError(f"test runner reported {self.failed} failed test(s)")
        if self.passed + self.failed + self.skipped != self.total:
            raise SummaryError(
                "summary counts disagree: "
                f"passed={self.passed} failed={self.failed} "
                f"skipped={self.skipped} total={self.total}"
            )


def parse_compact(lines: list[str]) -> list[Counts]:
    marker_lines = [line for line in lines if re.match(r"^(?:Passed|Failed)!", line)]
    matches = [COMPACT.fullmatch(line) for line in marker_lines]
    if not marker_lines or any(match is None for match in matches):
        raise SummaryError("compact VSTest summary is missing or malformed")

    runs: list[Counts] = []
    for match in matches:
        assert match is not None
        counts = Counts(
            failed=int(match.group(2)),
            passed=int(match.group(3)),
            skipped=int(match.group(4)),
            total=int(match.group(5)),
        )
        counts.validate(match.group(1))
        runs.append(counts)
    return runs


def one_value(name: str, values: list[int], default: int | None = None) -> int:
    if len(values) > 1:
        raise SummaryError(f"detailed summary repeats {name}")
    if values:
        return values[0]
    if default is not None:
        return default
    raise SummaryError(f"detailed summary omits {name}")


def parse_detailed(lines: list[str]) -> list[Counts]:
    status_indexes = [index for index, line in enumerate(lines) if STATUS.fullmatch(line)]
    if not status_indexes:
        raise SummaryError("detailed VSTest result marker is missing")

    runs: list[Counts] = []
    for position, start in enumerate(status_indexes):
        status_match = STATUS.fullmatch(lines[start])
        assert status_match is not None
        end = status_indexes[position + 1] if position + 1 < len(status_indexes) else len(lines)
        block = lines[start + 1 : end]

        totals = [int(match.group(1)) for line in block if (match := TOTAL.fullmatch(line))]
        metrics: dict[str, list[int]] = {"Passed": [], "Failed": [], "Skipped": []}
        for line in block:
            match = METRIC.fullmatch(line)
            if match:
                metrics[match.group(1)].append(int(match.group(2)))

        counts = Counts(
            passed=one_value("Passed", metrics["Passed"]),
            failed=one_value("Failed", metrics["Failed"], 0),
            skipped=one_value("Skipped", metrics["Skipped"], 0),
            total=one_value("Total tests", totals),
        )
        status = "Passed" if status_match.group(1) == "Successful" else "Failed"
        counts.validate(status)
        runs.append(counts)
    return runs


def parse(payload: str) -> list[Counts]:
    lines = payload.splitlines()
    has_detailed = any(STATUS.fullmatch(line) for line in lines)
    has_compact = any(re.match(r"^(?:Passed|Failed)!", line) for line in lines)
    if has_detailed and has_compact:
        raise SummaryError("mixed detailed and compact VSTest summaries")
    if has_detailed:
        return parse_detailed(lines)
    return parse_compact(lines)


def main() -> int:
    try:
        runs = parse(sys.stdin.read())
    except SummaryError as exc:
        print(f"dotnet-summary: FAIL — {exc}", file=sys.stderr)
        return 2

    print(
        sum(run.passed for run in runs),
        sum(run.failed for run in runs),
        sum(run.skipped for run in runs),
        sum(run.total for run in runs),
        len(runs),
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
