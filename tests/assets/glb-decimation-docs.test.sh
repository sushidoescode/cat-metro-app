#!/usr/bin/env bash
# Keep the operator-facing GLB decimation runbook and compact project-state
# summary aligned with the hardened transaction and child-process contract.
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

PYTHONDONTWRITEBYTECODE=1 python3 - <<'PY'
from pathlib import Path

runbook = Path("docs/design/assets/DECIMATION.md").read_text(encoding="utf-8")
state = Path("state/PROJECT_STATE.md").read_text(encoding="utf-8")
production = Path("scripts/decimate-assets.py").read_text(encoding="utf-8")


def require(text: str, fragment: str, label: str) -> None:
    if " ".join(fragment.split()) not in " ".join(text.split()):
        raise SystemExit(f"glb-decimation-docs.test.sh: FAIL — {label}")


def forbid(text: str, fragment: str, label: str) -> None:
    if " ".join(fragment.split()) in " ".join(text.split()):
        raise SystemExit(f"glb-decimation-docs.test.sh: FAIL — {label}")


# Fail closed if the implementation topology changes out from under this
# documentation oracle. These are behavior-bearing seams, not comments.
for marker in (
    "def _retire_absent_partial_pair(",
    "def _restore_old_pair(",
    "stdout=subprocess.PIPE",
    "stderr=subprocess.PIPE",
    "MAX_CHILD_STREAM_BYTES = MAX_METADATA_BYTES",
    "def _sanitized_environment(private_root: Path)",
):
    require(production, marker, f"production anchor disappeared: {marker}")

# A failed forced cleanup restores the exact old public pair. The old runbook
# instead told operators that a verified new pair could remain beside backups.
require(
    runbook,
    "restores the exact old public pair",
    "force-cleanup recovery does not name the exact old-pair terminal",
)
require(
    runbook,
    "no backup residue",
    "force-cleanup recovery does not require backup cleanup",
)
forbid(
    runbook,
    "the verified new finals may coexist with backup residue",
    "runbook still documents the superseded force-cleanup terminal",
)

# A persistently undeletable absent-destination candidate is moved away from
# public final names as a complete private pair, which operators must preserve
# and inventory after a nonzero exit.
require(
    runbook,
    "privately retired candidate pair",
    "absent-destination retired-pair terminal is undocumented",
)
require(
    runbook,
    ".*.retired-*",
    "recovery inventory omits retired candidate members",
)

# Child output and environment handling are part of the offline/custody
# posture: two independent byte ceilings, no replay, and an allowlist with
# private mode-0700 home/config/temp roots.
require(
    runbook,
    "1 MiB per stream",
    "independent child-stream ceiling is undocumented",
)
require(
    runbook,
    "standard output and standard error are captured separately and are not replayed",
    "child-stream capture/non-replay behavior is undocumented",
)
forbid(
    runbook,
    "standard output/error is inherited",
    "runbook still claims child streams are inherited",
)
require(
    runbook,
    "explicit name allowlist",
    "child environment allowlist is undocumented",
)
require(
    runbook,
    "private mode-0700",
    "private child environment roots are undocumented",
)
forbid(
    runbook,
    "receive an environment copy with every variable",
    "runbook still describes blacklist filtering of a copied environment",
)

# The audited queue totals are file bytes for the exact 15 source/derivative
# GLBs. A whole-directory `du` number includes unrelated inventory and cannot
# stand in for this evidence.
require(
    state,
    "855,215,420→24,717,404 bytes",
    "project state omits the exact audited 15-asset byte totals",
)
forbid(
    state,
    "990MB→24MB",
    "project state still presents whole-directory usage as asset totals",
)

print("glb-decimation-docs.test.sh: pass")
PY
