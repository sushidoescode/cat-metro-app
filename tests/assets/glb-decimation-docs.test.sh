#!/usr/bin/env bash
# Keep the operator-facing GLB decimation runbook and compact project-state
# summary aligned with the hardened transaction and child-process contract.
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

PYTHONDONTWRITEBYTECODE=1 python3 - <<'PY'
from pathlib import Path

runbook = Path("docs/design/assets/DECIMATION.md").read_text(encoding="utf-8")
state = Path("state/PROJECT_STATE.md").read_text(encoding="utf-8")
lessons = Path("docs/lessons.md").read_text(encoding="utf-8")
evidence = Path("docs/design/assets/GLB-DECIMATION-EVIDENCE.md").read_text(
    encoding="utf-8"
)
production = Path("scripts/decimate-assets.py").read_text(encoding="utf-8")
metrics = Path("scripts/glb_metrics.py").read_text(encoding="utf-8")
silhouette = Path("scripts/glb-silhouette.py").read_text(encoding="utf-8")


def require(text: str, fragment: str, label: str) -> None:
    if " ".join(fragment.split()) not in " ".join(text.split()):
        raise SystemExit(f"glb-decimation-docs.test.sh: FAIL — {label}")


def forbid(text: str, fragment: str, label: str) -> None:
    if " ".join(fragment.split()) in " ".join(text.split()):
        raise SystemExit(f"glb-decimation-docs.test.sh: FAIL — {label}")


def require_lesson_row(stem: str, evidence: tuple[str, ...], label: str) -> None:
    matches = [
        line
        for line in lessons.splitlines()
        if " ".join(stem.split()) in " ".join(line.split())
    ]
    if len(matches) != 1:
        raise SystemExit(
            f"glb-decimation-docs.test.sh: FAIL — {label} row count={len(matches)}"
        )
    columns = [column.strip() for column in matches[0].split("|")]
    if len(columns) < 7 or columns[5] != "enforced":
        raise SystemExit(
            f"glb-decimation-docs.test.sh: FAIL — {label} is not enforced"
        )
    for fragment in evidence:
        require(matches[0], fragment, f"{label} omits evidence {fragment}")


# Fail closed if the implementation topology changes out from under this
# documentation oracle. These are behavior-bearing seams, not comments.
for marker in (
    "def _retire_absent_partial_pair(",
    "def _normalize_partial_retirement(",
    "def _restore_old_pair(",
    "def _prepare_assets(",
    "def _verify_snapshot_pair(",
    "def _verify_original_pair(",
    "def _commit_completed_publications(",
    "def _rollback_completed_publications(",
    "def _emit_completed_records(",
    "MAX_PUBLICATION_ROLLBACK_BYTES = 128 * 1024 * 1024",
    "stdout=subprocess.PIPE",
    "stderr=subprocess.PIPE",
    "MAX_CHILD_STREAM_BYTES = MAX_METADATA_BYTES",
    "def _sanitized_environment(private_root: Path)",
    "def _diagnostic_payload(message: object) -> bytes:",
):
    require(production, marker, f"production anchor disappeared: {marker}")

for marker in (
    "MAX_DOCUMENT_NESTING = 256",
    "MAX_JSON_BYTES = 16 * 1024 * 1024",
    "MAX_IMAGE_BYTES = 8 * 1024 * 1024",
    "MAX_GEOMETRY_WORK = 8_000_000",
    "MAX_SPARSE_ACCESSOR_WORK = 8_000_000",
    "MAX_WORLD_BOUNDS_WORK = 8_000_000",
    "MAX_WORLD_POSITION_WORK = 8_000_000",
    "MAX_IMAGE_WORK_BYTES = 64 * 1024 * 1024",
    "MAX_JSON_INTEGER_DIGITS = 4_300",
    "MAX_JSON_NUMBER_CHARACTERS = 4_300",
    "def _decode_json_bytes(payload: bytes) -> object:",
    "def _validate_sparse_work(accessors: list[object]) -> None:",
    "def _validate_geometry_work(",
    "def _public_external_uri(value: str) -> str:",
    "def _public_success_metrics(metrics: Mapping[str, object]) -> dict[str, object]:",
):
    require(metrics, marker, f"metrics production anchor disappeared: {marker}")

for marker in (
    "MAXIMUM_SELECTED_SCENE_WORK = 8_000_000",
    "def _validate_selected_scene_work(document: Mapping[str, object]) -> None:",
    "def _diagnostic_payload(message: object) -> bytes:",
    "def _public_path(path: Path) -> str:",
    "def _success_payload(",
):
    require(silhouette, marker, f"silhouette production anchor disappeared: {marker}")

# Complete-queue custody precedes the Blender probe and carries both immutable
# source members through a verified private snapshot to each publication.
for fragment, label in (
    (
        "full-queue path and filesystem-identity census",
        "complete-queue path/identity custody is undocumented",
    ),
    (
        "Before the Blender version probe",
        "complete custody is not documented as pre-version work",
    ),
    (
        "source GLB and source sidecar pair into a private mode-0700 snapshot",
        "complete source-and-sidecar snapshot is undocumented",
    ),
    (
        "reverifies both the immutable originals and private snapshots immediately before publication",
        "pre-publication source/snapshot reverification is undocumented",
    ),
):
    require(runbook, fragment, label)

# Parser and resource ceilings are inclusive and have distinct charging
# models. Requiring the semantic phrase beside each value prevents one generic
# occurrence of 8,000,000 from satisfying every documentation obligation.
for fragment, label in (
    (
        "Every ceiling in this subsection is inclusive: exact equality is accepted",
        "inclusive exact/+1 ceiling rule is undocumented",
    ),
    (
        "4,300 digits excluding a leading minus",
        "JSON integer digit ceiling is undocumented",
    ),
    (
        "4,300 characters including sign, decimal point, and exponent syntax",
        "JSON non-integer token ceiling is undocumented",
    ),
    (
        "NaN, Infinity, and duplicate object keys are rejected",
        "strict JSON rejection rules are undocumented",
    ),
    (
        "16 MiB GLB JSON chunk and 256 nesting levels",
        "GLB JSON byte/depth ceilings are undocumented",
    ),
    (
        "8,000,000 combined POSITION-value and index-reference units across all meshes, including unselected meshes",
        "all-mesh aggregate geometry ceiling is undocumented",
    ),
    (
        "Repeated primitives and shared accessors are charged per primitive occurrence",
        "geometry replay charging is undocumented",
    ),
    (
        "8,000,000 sparse values from sparse.count across all accessors, including unreferenced accessors",
        "aggregate sparse ceiling is undocumented",
    ),
    (
        "8 MiB per embedded image payload and 64 MiB aggregate embedded-image work",
        "per-image and aggregate image ceilings are undocumented",
    ),
    (
        "bufferView byteLength, data-URI encoded characters, and every repeated image entry is charged again",
        "aggregate image charging units are undocumented",
    ),
    (
        "8,000,000 world-bounds corner transforms, charged as eight per selected node/primitive instance",
        "selected world-bounds ceiling is undocumented",
    ),
    (
        "8,000,000 selected-instance POSITION values, precharged before an iterator is returned",
        "selected world-position ceiling is undocumented",
    ),
    (
        "8,000,000 combined selected index-reference and POSITION-value units",
        "silhouette selected-scene ceiling is undocumented",
    ),
    (
        "strict all-mesh geometry ceiling separately includes unselected meshes",
        "unselected strict-inspection closure is undocumented",
    ),
):
    require(runbook, fragment, label)

# Catchable pre-commit execution is batch-terminal, including forced old-byte retention
# and sequential pair normalization. Individual member renames remain atomic;
# the logical two-member and multi-asset terminals are explicit recovery work.
for fragment, label in (
    (
        "catchable pre-commit failure is batch-terminal",
        "catchable pre-commit batch terminal is undocumented",
    ),
    (
        "rolls every completed publication back in reverse manifest order",
        "reverse whole-batch rollback is undocumented",
    ),
    (
        "128 MiB aggregate rollback-retention budget",
        "force rollback-retention ceiling is undocumented",
    ),
    (
        "before either old member is read or either public name is renamed",
        "force retention cap is not documented as pre-read/pre-rename",
    ),
    (
        "Equality is accepted; a pair that would exceed the remaining budget is neither read nor renamed",
        "force retention exact/+1 behavior is undocumented",
    ),
    (
        "until every asset and private-root cleanup has succeeded",
        "old-pair receipts are not documented as whole-batch retained",
    ),
    (
        "a later backup-cleanup failure restores the whole batch",
        "whole-batch backup commit rollback is undocumented",
    ),
    (
        "first retirement rename succeeds but the second fails",
        "sequential retirement rename normalization is undocumented",
    ),
    (
        "re-forms the exact two-member private pair from bounded receipts before public cleanup",
        "partial retirement does not document receipt-first normalization",
    ),
    (
        "one retired-member unlink succeeds and its mate cannot be removed",
        "asymmetric retired cleanup terminal is undocumented",
    ),
):
    require(runbook, fragment, label)

forbid(
    runbook,
    "batch is not all-or-nothing",
    "runbook still claims per-asset terminal acceptance",
)
forbid(
    runbook,
    "an earlier accepted derivative pair remains accepted",
    "runbook still preserves an earlier publication after batch failure",
)

# Start/progress records can survive a failed run; success records cannot begin
# until terminal commit. Post-commit sequential reporting can fail after an
# earlier line. All three public CLIs describe their actual redaction and
# byte-boundary behavior without falsely bounding the whole metrics JSON.
for fragment, label in (
    (
        "Start/progress records are attempted immediately before each Blender call",
        "start/progress record timing is undocumented",
    ),
    (
        "No success record is attempted before whole-batch terminal commit",
        "batch-terminal success-record timing is undocumented",
    ),
    (
        "A failure before terminal commit emits no success record",
        "pre-commit failure success-record exclusion is undocumented",
    ),
    (
        "success-record writes are best effort after exact commit",
        "post-commit success-record write behavior is undocumented",
    ),
    (
        "a later record-write failure followed by a failed exact-publication observation can return nonzero after earlier success lines were already written",
        "post-commit partial success-record reporting is undocumented",
    ),
    (
        "Every decimator public record and diagnostic is one line of at most 512 bytes",
        "decimator public byte boundary is undocumented",
    ),
    (
        "HTTP(S) substrings become [redacted-uri]",
        "decimator URI redaction is undocumented",
    ),
    (
        "remaining credential-shaped content collapses to fixed text",
        "decimator whole-message redaction is undocumented",
    ),
    (
        "Metrics failures write one diagnostic-only line of at most 512 bytes",
        "metrics failure output boundary is undocumented",
    ),
    (
        "success JSON recursively redacts unsafe string values",
        "metrics success sanitization is undocumented",
    ),
    (
        "unsafe external URIs while preserving benign relative leaves",
        "metrics external-URI sanitization is undocumented",
    ),
    (
        "The complete metrics success JSON is not subject to a 512-byte total limit",
        "metrics success total-size distinction is undocumented",
    ),
    (
        "Silhouette failures write one diagnostic-only line of at most 512 bytes",
        "silhouette failure output boundary is undocumented",
    ),
    (
        "silhouette success record is one printable line of at most 512 bytes",
        "silhouette success output boundary is undocumented",
    ),
    (
        "redacts unsafe or overlong source and output paths",
        "silhouette success path redaction is undocumented",
    ),
):
    require(runbook, fragment, label)

forbid(
    runbook,
    "Account for one start-format and one acceptance-format record per successful manifest entry",
    "checklist still requires an infallible post-commit success record",
)

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
require(
    state,
    "former deferred unselected-mesh issue is resolved",
    "project state does not retire the unselected-mesh issue",
)
forbid(
    state,
    "selected-scene silhouette cap does not yet bound expensive unselected meshes",
    "project state still defers the fixed unselected-mesh issue",
)
forbid(
    state,
    "Next: close exact-head review findings",
    "project state still says integrated findings remain to be closed",
)

# The whole-inventory metrics date remains the original queue date, while the
# two curated derivatives have later tool timestamps. Keep the local-date
# statement scoped to the 13 historical sidecars rather than all 15.
for fragment, label in (
    (
        "The 13 untouched derivative sidecars record",
        "historical sidecar date scope is undocumented",
    ),
    (
        "2026-08-17T10:33:30Z",
        "refreshed loaf derivative timestamp is undocumented",
    ),
    (
        "2026-08-17T10:33:48Z",
        "refreshed wave derivative timestamp is undocumented",
    ),
    (
        "metrics `run_date` remains `2026-08-16`",
        "whole-inventory run-date meaning is undocumented",
    ),
):
    require(evidence, fragment, label)
forbid(
    evidence,
    "Each sidecar records Blender `5.1.2`",
    "evidence still applies the historical local date to all 15 sidecars",
)

# Human-caught defect classes are ratcheted to the behavior tests that now pin
# them, rather than being left only as one-off implementation history.
require_lesson_row(
    "Per-item resource limits are mistaken for aggregate or replay limits",
    ("tests/assets/glb-metrics.test.sh", "tests/assets/glb-silhouette.test.sh"),
    "aggregate/replay resource-accounting lesson",
)
require_lesson_row(
    "Per-pair publication is mistaken for whole-batch commit and recovery",
    ("tests/assets/glb-decimation-pipeline.test.sh",),
    "whole-batch commit/recovery lesson",
)
require_lesson_row(
    "Private artifacts are kept confidential while public records still echo untrusted values",
    (
        "tests/assets/glb-decimation-pipeline.test.sh",
        "tests/assets/glb-metrics.test.sh",
        "tests/assets/glb-silhouette.test.sh",
    ),
    "private-artifact/public-record lesson",
)
require_lesson_row(
    "A sequential two-member move or removal is treated as atomic",
    ("tests/assets/glb-decimation-pipeline.test.sh",),
    "sequential two-member normalization lesson",
)

print("glb-decimation-docs.test.sh: pass")
PY
