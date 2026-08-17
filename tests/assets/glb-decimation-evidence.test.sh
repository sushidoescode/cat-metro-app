#!/usr/bin/env bash
# Keep the human-readable GLB evidence tied to the tracked renderer and the
# machine-readable custody authority. Ignored local artifacts are an optional
# stronger leg, never a CI prerequisite.
set -euo pipefail

repo_root=$(git rev-parse --show-toplevel)
evidence="$repo_root/docs/design/assets/GLB-DECIMATION-EVIDENCE.md"
metrics="$repo_root/docs/design/assets/GLB-DECIMATION-METRICS.json"
renderer="$repo_root/scripts/glb-silhouette.py"
artifact_root=${GLB_DECIMATION_ARTIFACT_ROOT:-$repo_root/unity/Assets/Art/Generated/incoming}
artifact_root_explicit=0
if [[ -n ${GLB_DECIMATION_ARTIFACT_ROOT+x} ]]; then
  artifact_root_explicit=1
fi

PYTHONDONTWRITEBYTECODE=1 python3 - \
  "$evidence" "$metrics" "$renderer" "$artifact_root" \
  "$artifact_root_explicit" <<'PY'
import hashlib
import json
import re
import stat
import sys
from pathlib import Path


evidence_path = Path(sys.argv[1])
metrics_path = Path(sys.argv[2])
renderer_path = Path(sys.argv[3])
artifact_root = Path(sys.argv[4])
artifact_root_explicit = sys.argv[5] == "1"
errors = []


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while block := handle.read(1024 * 1024):
            digest.update(block)
    return digest.hexdigest()


def require_regular_file(path, label):
    try:
        status = path.lstat()
    except FileNotFoundError:
        errors.append(f"{label} is missing: {path}")
        return False
    if not stat.S_ISREG(status.st_mode) or status.st_nlink != 1:
        errors.append(f"{label} must be a regular single-link file: {path}")
        return False
    return True


evidence = evidence_path.read_text(encoding="utf-8")
metrics = json.loads(metrics_path.read_text(encoding="utf-8"))
assets = metrics.get("assets")
if not isinstance(assets, list):
    raise AssertionError("metrics assets must be a list")

renderer_match = re.search(
    r"Renderer SHA-256:\s*\n`([0-9a-f]{64})`\.", evidence
)
if renderer_match is None:
    errors.append("evidence must contain exactly formatted Renderer SHA-256")
else:
    recorded_renderer_sha = renderer_match.group(1)
    actual_renderer_sha = sha256(renderer_path)
    if recorded_renderer_sha != actual_renderer_sha:
        errors.append(
            "renderer SHA mismatch: evidence="
            f"{recorded_renderer_sha} tracked={actual_renderer_sha}"
        )

heading = "## Source and derivative custody"
next_heading = "## Silhouette evidence"
if evidence.count(heading) != 1 or evidence.count(next_heading) != 1:
    raise AssertionError("custody table headings must each occur exactly once")
custody_section = evidence.split(heading, 1)[1].split(next_heading, 1)[0]
row_pattern = re.compile(
    r"^\| `([^`]+)` \| `([0-9a-f]{64})` \| `([0-9a-f]{64})` "
    r"\| `([0-9a-f]{64})` \| `([0-9a-f]{64})` \|$",
    re.MULTILINE,
)
markdown_rows = [
    {
        "id": match.group(1),
        "source_sha256": match.group(2),
        "source_sidecar_sha256": match.group(3),
        "derivative_sha256": match.group(4),
        "derivative_sidecar_sha256": match.group(5),
    }
    for match in row_pattern.finditer(custody_section)
]
if len(markdown_rows) != len(assets):
    errors.append(
        f"custody row count mismatch: markdown={len(markdown_rows)} "
        f"metrics={len(assets)}"
    )

markdown_ids = [row["id"] for row in markdown_rows]
metric_ids = [asset.get("id") for asset in assets]
if markdown_ids != metric_ids:
    errors.append(
        f"custody row order/identity mismatch: markdown={markdown_ids!r} "
        f"metrics={metric_ids!r}"
    )
if len(set(markdown_ids)) != len(markdown_ids):
    errors.append("custody table contains duplicate asset IDs")

metric_by_id = {asset.get("id"): asset for asset in assets}
if len(metric_by_id) != len(assets):
    errors.append("metrics contain duplicate asset IDs")

custody_fields = (
    "source_sha256",
    "source_sidecar_sha256",
    "derivative_sha256",
    "derivative_sidecar_sha256",
)
for row in markdown_rows:
    asset = metric_by_id.get(row["id"])
    if asset is None:
        continue
    for field in custody_fields:
        if row[field] != asset.get(field):
            errors.append(
                f"{row['id']} {field} mismatch: markdown={row[field]} "
                f"metrics={asset.get(field)!r}"
            )

expected_local_paths = []
for asset in assets:
    source_filename = asset.get("source_filename")
    derivative_filename = asset.get("derivative_filename")
    if not isinstance(source_filename, str) or not isinstance(
        derivative_filename, str
    ):
        errors.append(f"{asset.get('id')!r} has invalid machine filenames")
        continue
    expected_local_paths.extend(
        (
            artifact_root / source_filename,
            artifact_root / f"{source_filename}.json",
            artifact_root / "decimated" / derivative_filename,
            artifact_root / "decimated" / f"{derivative_filename}.json",
        )
    )

local_available = artifact_root_explicit or any(
    path.exists() or path.is_symlink() for path in expected_local_paths
)
if local_available:
    for row in markdown_rows:
        asset = metric_by_id.get(row["id"])
        if asset is None:
            continue
        local_members = (
            (
                artifact_root / asset["source_filename"],
                "source_sha256",
                "source GLB",
            ),
            (
                artifact_root / f"{asset['source_filename']}.json",
                "source_sidecar_sha256",
                "source JSON",
            ),
            (
                artifact_root / "decimated" / asset["derivative_filename"],
                "derivative_sha256",
                "derivative GLB",
            ),
            (
                artifact_root
                / "decimated"
                / f"{asset['derivative_filename']}.json",
                "derivative_sidecar_sha256",
                "derivative JSON",
            ),
        )
        for path, field, member_label in local_members:
            label = f"{row['id']} local {member_label}"
            if not require_regular_file(path, label):
                continue
            actual = sha256(path)
            if actual != row[field]:
                errors.append(
                    f"{row['id']} {field} mismatch: markdown={row[field]} "
                    f"local={actual}"
                )

if errors:
    raise AssertionError("GLB evidence drift:\n- " + "\n- ".join(errors))

local_status = "checked" if local_available else "skipped"
print(
    f"glb-decimation evidence: pass assets={len(assets)} "
    f"local_artifacts={local_status}"
)
PY
