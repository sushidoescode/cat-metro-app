#!/usr/bin/env bash
# Keep the human-readable GLB evidence tied to the tracked renderer and the
# machine-readable custody authority. Ignored local artifacts are an optional
# stronger leg, never a CI prerequisite.
set -euo pipefail

repo_root=$(git rev-parse --show-toplevel)
evidence="$repo_root/docs/design/assets/GLB-DECIMATION-EVIDENCE.md"
metrics="$repo_root/docs/design/assets/GLB-DECIMATION-METRICS.json"
renderer="$repo_root/scripts/glb-silhouette.py"
manifest="$repo_root/docs/design/assets/CAT-MANIFEST.json"
artifact_root=${GLB_DECIMATION_ARTIFACT_ROOT:-$repo_root/unity/Assets/Art/Generated/incoming}
artifact_root_explicit=0
if [[ -n ${GLB_DECIMATION_ARTIFACT_ROOT+x} ]]; then
  artifact_root_explicit=1
fi

PYTHONDONTWRITEBYTECODE=1 python3 - \
  "$evidence" "$metrics" "$renderer" "$manifest" "$artifact_root" \
  "$artifact_root_explicit" <<'PY'
import hashlib
import json
import math
import re
import stat
import sys
from pathlib import Path


evidence_path = Path(sys.argv[1])
metrics_path = Path(sys.argv[2])
renderer_path = Path(sys.argv[3])
manifest_path = Path(sys.argv[4])
artifact_root = Path(sys.argv[5])
artifact_root_explicit = sys.argv[6] == "1"
errors = []

# These are independent review anchors over the semantically authoritative
# fields, not whole-file byte hashes. Metadata prose may change without
# waiving the reviewed asset facts. An intentional evidence refresh must
# update the Markdown tables and these anchors in the same reviewed change.
EXPECTED_MACHINE_AUTHORITY_SHA256 = (
    "035bf603d56b89d24926e641d21798fe7f0ad984b3773283c8f5881948445189"
)
EXPECTED_SILHOUETTE_TABLE_SHA256 = (
    "9d6159dc9b0ee5c3ddc4adeb08e2b01c206d207d6ff5b77b408f554f8d992b87"
)

ASSET_KEYS = {
    "byte_reduction",
    "byte_reduction_percent",
    "custody_agreements",
    "derivative_filename",
    "derivative_sha256",
    "derivative_sidecar_sha256",
    "embedded_texture_preservation",
    "id",
    "kind",
    "output",
    "preservation_diagnostics",
    "service",
    "sidecar_derivative_sha256",
    "sidecar_hash_agreement",
    "source",
    "source_filename",
    "source_sha256",
    "source_sidecar_sha256",
    "triangle_reduction",
    "triangle_reduction_percent",
}
METRIC_KEYS = {
    "animations",
    "bytes",
    "cameras",
    "embedded_images",
    "extensions_required",
    "extensions_used",
    "external_uris",
    "images",
    "lights",
    "material_primitives",
    "materials",
    "meshes",
    "morph_targets",
    "primitives",
    "skins",
    "triangles",
    "uv_primitives",
    "vertices",
    "world_bounds",
}
CUSTODY_KEYS = {
    "derivative_record_derivative_filename_matches_derivative",
    "derivative_record_derivative_sha256_matches_derivative",
    "derivative_record_geometry_matches_inspection",
    "derivative_record_source_provenance_matches_source_sidecar",
    "derivative_record_source_sha256_matches_source",
    "derivative_record_source_sidecar_sha256_matches_source_sidecar",
    "source_sidecar_claim_matches_source",
}
TEXTURE_KEYS = {
    "output_payload_bytes",
    "output_payload_sha256",
    "output_texture_role_payload_sha256",
    "payload_bytes_identical",
    "source_payload_bytes",
    "source_payload_sha256",
    "source_texture_role_payload_sha256",
    "texture_role_bytes_identical",
}
TOTAL_KEYS = {
    "assets",
    "byte_reduction",
    "byte_reduction_percent",
    "cats",
    "output_bytes",
    "output_triangles",
    "output_vertices",
    "props",
    "source_bytes",
    "source_triangles",
    "source_vertices",
    "triangle_reduction",
    "triangle_reduction_percent",
    "vertex_reduction",
    "vertex_reduction_percent",
}
ZERO_STRUCTURE_FIELDS = {
    "animations",
    "cameras",
    "lights",
    "morph_targets",
    "skins",
}
ONE_STRUCTURE_FIELDS = {
    "material_primitives",
    "materials",
    "meshes",
    "primitives",
    "uv_primitives",
}
HEX64 = re.compile(r"[0-9a-f]{64}")


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


def canonical_sha(value):
    payload = json.dumps(
        value,
        ensure_ascii=False,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()


def section_between(document, heading, next_heading):
    if document.count(heading) != 1 or document.count(next_heading) != 1:
        raise AssertionError(
            f"evidence headings must each occur exactly once: "
            f"{heading!r}, {next_heading!r}"
        )
    return document.split(heading, 1)[1].split(next_heading, 1)[0]


def comma_int(value):
    return int(value.replace(",", ""))


def rounded_percent(reduction, original):
    if not isinstance(original, int) or isinstance(original, bool) or original <= 0:
        return None
    return float(f"{(reduction / original) * 100:.6f}")


def valid_hash(value):
    return isinstance(value, str) and HEX64.fullmatch(value) is not None


def bounds_facts(source, output):
    try:
        source_min = [float(value) for value in source["world_bounds"]["min"]]
        source_max = [float(value) for value in source["world_bounds"]["max"]]
        output_min = [float(value) for value in output["world_bounds"]["min"]]
        output_max = [float(value) for value in output["world_bounds"]["max"]]
    except (KeyError, TypeError, ValueError):
        return None
    vectors = (source_min, source_max, output_min, output_max)
    if any(len(vector) != 3 for vector in vectors):
        return None
    if not all(math.isfinite(value) for vector in vectors for value in vector):
        return None
    if any(low > high for low, high in zip(source_min, source_max)):
        return None
    if any(low > high for low, high in zip(output_min, output_max)):
        return None
    source_center = [
        (low + high) / 2 for low, high in zip(source_min, source_max)
    ]
    output_center = [
        (low + high) / 2 for low, high in zip(output_min, output_max)
    ]
    source_extent = [high - low for low, high in zip(source_min, source_max)]
    output_extent = [high - low for low, high in zip(output_min, output_max)]
    source_longest = max(source_extent)
    output_longest = max(output_extent)
    if source_longest <= 0 or output_longest <= 0:
        return None
    center_drift = max(
        abs(after - before)
        for before, after in zip(source_center, output_center)
    ) / source_longest
    scale_drift = abs(output_longest / source_longest - 1)
    normalized_extent_drift = max(
        abs(after / output_longest - before / source_longest)
        for before, after in zip(source_extent, output_extent)
    )
    return center_drift, scale_drift, normalized_extent_drift


evidence = evidence_path.read_text(encoding="utf-8")
metrics = json.loads(metrics_path.read_text(encoding="utf-8"))
manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
assets = metrics.get("assets")
if not isinstance(assets, list):
    raise AssertionError("metrics assets must be a list")
manifest_assets = manifest.get("assets")
if not isinstance(manifest_assets, list):
    raise AssertionError("manifest assets must be a list")
if metrics.get("schema_version") != 1:
    errors.append(f"metrics schema_version must be 1, got {metrics.get('schema_version')!r}")
if metrics.get("manifest") != "docs/design/assets/CAT-MANIFEST.json":
    errors.append(f"metrics manifest path changed: {metrics.get('manifest')!r}")
if len(assets) != 15 or len(manifest_assets) != 15:
    errors.append(
        f"asset inventory must remain 15: metrics={len(assets)} "
        f"manifest={len(manifest_assets)}"
    )
if not all(isinstance(asset, dict) for asset in assets):
    raise AssertionError("every metrics asset must be an object")
if not all(isinstance(asset, dict) for asset in manifest_assets):
    raise AssertionError("every manifest asset must be an object")

metric_ids = [asset.get("id") for asset in assets]
manifest_ids = [asset.get("id") for asset in manifest_assets]
if metric_ids != manifest_ids:
    errors.append(
        f"metrics manifest order/identity mismatch: metrics={metric_ids!r} "
        f"manifest={manifest_ids!r}"
    )
if any(not isinstance(asset_id, str) for asset_id in metric_ids):
    raise AssertionError("every metrics asset ID must be a string")
if len(set(metric_ids)) != len(metric_ids):
    errors.append("metrics contain duplicate asset IDs")

for asset, manifest_asset in zip(assets, manifest_assets):
    asset_id = asset.get("id")
    if set(asset) != ASSET_KEYS:
        errors.append(
            f"{asset_id!r} machine asset fields changed: "
            f"{sorted(set(asset) ^ ASSET_KEYS)!r}"
        )
    expected_manifest_facts = {
        "id": manifest_asset.get("id"),
        "kind": manifest_asset.get("kind"),
        "service": manifest_asset.get("service"),
        "source_filename": manifest_asset.get("out"),
        "derivative_filename": manifest_asset.get("out"),
    }
    for field, expected in expected_manifest_facts.items():
        if asset.get(field) != expected:
            errors.append(
                f"{asset_id!r} {field} disagrees with manifest: "
                f"metrics={asset.get(field)!r} manifest={expected!r}"
            )
    for metric_role in ("source", "output"):
        metric = asset.get(metric_role)
        if not isinstance(metric, dict):
            errors.append(f"{asset_id!r} {metric_role} metrics must be an object")
            continue
        if set(metric) != METRIC_KEYS:
            errors.append(
                f"{asset_id!r} {metric_role} fields changed: "
                f"{sorted(set(metric) ^ METRIC_KEYS)!r}"
            )
        for field in ZERO_STRUCTURE_FIELDS:
            if metric.get(field) != 0:
                errors.append(f"{asset_id!r} {metric_role}.{field} must remain zero")
        for field in ONE_STRUCTURE_FIELDS:
            if metric.get(field) != 1:
                errors.append(f"{asset_id!r} {metric_role}.{field} must remain one")
        for field in ("extensions_required", "extensions_used", "external_uris"):
            if metric.get(field) != []:
                errors.append(f"{asset_id!r} {metric_role}.{field} must remain empty")
        for field in (
            "bytes", "embedded_images", "images", "triangles", "vertices"
        ):
            value = metric.get(field)
            if not isinstance(value, int) or isinstance(value, bool) or value < 0:
                errors.append(
                    f"{asset_id!r} {metric_role}.{field} must be a nonnegative integer"
                )
        if metric.get("images") != metric.get("embedded_images"):
            errors.append(f"{asset_id!r} {metric_role} image counts disagree")
    source = asset.get("source")
    output = asset.get("output")
    if not isinstance(source, dict) or not isinstance(output, dict):
        continue
    kind = asset.get("kind")
    if kind == "cat":
        accepted_minimum, accepted_maximum = 13_500, 15_000
    elif kind == "prop":
        accepted_minimum, accepted_maximum = 9_000, 10_000
    else:
        errors.append(f"{asset_id!r} has unsupported category {kind!r}")
        accepted_minimum, accepted_maximum = 5_000, 20_000
    output_triangles = output.get("triangles")
    if not isinstance(output_triangles, int) or isinstance(output_triangles, bool):
        errors.append(f"{asset_id!r} output.triangles must be an integer")
    elif not (
        accepted_minimum <= output_triangles <= accepted_maximum
        and 5_000 <= output_triangles <= 20_000
    ):
        errors.append(
            f"{asset_id!r} output triangles {output_triangles} miss "
            f"the {kind} and global bands"
        )
    for prefix, metric_field, reduction_field, percent_field in (
        ("byte", "bytes", "byte_reduction", "byte_reduction_percent"),
        (
            "triangle",
            "triangles",
            "triangle_reduction",
            "triangle_reduction_percent",
        ),
    ):
        original = source.get(metric_field)
        reduced = output.get(metric_field)
        if not all(
            isinstance(value, int) and not isinstance(value, bool)
            for value in (original, reduced)
        ):
            continue
        expected_reduction = original - reduced
        expected_percent = rounded_percent(expected_reduction, original)
        if asset.get(reduction_field) != expected_reduction:
            errors.append(f"{asset_id!r} {prefix} reduction math disagrees")
        if asset.get(percent_field) != expected_percent:
            errors.append(f"{asset_id!r} {prefix} reduction percent disagrees")
    custody = asset.get("custody_agreements")
    if not isinstance(custody, dict) or set(custody) != CUSTODY_KEYS:
        errors.append(f"{asset_id!r} custody agreement fields changed")
    elif any(value is not True for value in custody.values()):
        errors.append(f"{asset_id!r} custody agreement is not true")
    if asset.get("sidecar_hash_agreement") is not True:
        errors.append(f"{asset_id!r} sidecar hash agreement is not true")
    if asset.get("preservation_diagnostics") != []:
        errors.append(f"{asset_id!r} preservation diagnostics are not empty")
    if asset.get("sidecar_derivative_sha256") != asset.get("derivative_sha256"):
        errors.append(f"{asset_id!r} sidecar derivative hash disagrees")
    for field in (
        "source_sha256",
        "source_sidecar_sha256",
        "derivative_sha256",
        "derivative_sidecar_sha256",
    ):
        if not valid_hash(asset.get(field)):
            errors.append(f"{asset_id!r} {field} is not lowercase SHA-256")
    texture = asset.get("embedded_texture_preservation")
    if not isinstance(texture, dict) or set(texture) != TEXTURE_KEYS:
        errors.append(f"{asset_id!r} embedded texture fields changed")
        continue
    if texture.get("payload_bytes_identical") is not True:
        errors.append(f"{asset_id!r} payload identity is not true")
    if texture.get("texture_role_bytes_identical") is not True:
        errors.append(f"{asset_id!r} texture-role identity is not true")
    for source_field, output_field in (
        ("source_payload_bytes", "output_payload_bytes"),
        ("source_payload_sha256", "output_payload_sha256"),
        (
            "source_texture_role_payload_sha256",
            "output_texture_role_payload_sha256",
        ),
    ):
        if texture.get(source_field) != texture.get(output_field):
            errors.append(f"{asset_id!r} {source_field}/{output_field} disagree")
    payload_bytes = texture.get("source_payload_bytes")
    payload_hashes = texture.get("source_payload_sha256")
    roles = texture.get("source_texture_role_payload_sha256")
    expected_images = source.get("embedded_images")
    if not (
        isinstance(payload_bytes, list)
        and len(payload_bytes) == expected_images
        and all(
            isinstance(value, int) and not isinstance(value, bool) and value > 0
            for value in payload_bytes
        )
    ):
        errors.append(f"{asset_id!r} embedded payload byte inventory is invalid")
    if not (
        isinstance(payload_hashes, list)
        and len(payload_hashes) == expected_images
        and all(valid_hash(value) for value in payload_hashes)
    ):
        errors.append(f"{asset_id!r} embedded payload hash inventory is invalid")
    if not isinstance(roles, dict) or not all(
        isinstance(values, list) and all(valid_hash(value) for value in values)
        for values in roles.values()
    ):
        errors.append(f"{asset_id!r} texture-role hash inventory is invalid")
    elif isinstance(payload_hashes, list):
        role_hashes = sorted(value for values in roles.values() for value in values)
        if role_hashes != sorted(payload_hashes):
            errors.append(f"{asset_id!r} texture roles do not cover payload inventory")

machine_authority = {
    "schema_version": metrics.get("schema_version"),
    "manifest": metrics.get("manifest"),
    "assets": [
        {
            key: asset.get(key)
            for key in sorted(ASSET_KEYS)
        }
        for asset in assets
    ],
    "totals": metrics.get("totals"),
}
machine_authority_sha = canonical_sha(machine_authority)
if machine_authority_sha != EXPECTED_MACHINE_AUTHORITY_SHA256:
    errors.append(
        "machine evidence authority changed: "
        f"expected={EXPECTED_MACHINE_AUTHORITY_SHA256} actual={machine_authority_sha}"
    )

totals = metrics.get("totals")
if not isinstance(totals, dict):
    raise AssertionError("metrics totals must be an object")
if set(totals) != TOTAL_KEYS:
    errors.append(
        f"machine totals fields changed: {sorted(set(totals) ^ TOTAL_KEYS)!r}"
    )


def metric_sum(role, field):
    values = [asset.get(role, {}).get(field) for asset in assets]
    if not all(
        isinstance(value, int) and not isinstance(value, bool)
        for value in values
    ):
        errors.append(f"cannot recompute totals for {role}.{field}")
        return None
    return sum(values)


source_bytes = metric_sum("source", "bytes")
output_bytes = metric_sum("output", "bytes")
source_vertices = metric_sum("source", "vertices")
output_vertices = metric_sum("output", "vertices")
source_triangles = metric_sum("source", "triangles")
output_triangles = metric_sum("output", "triangles")
if None not in (
    source_bytes,
    output_bytes,
    source_vertices,
    output_vertices,
    source_triangles,
    output_triangles,
):
    byte_reduction = source_bytes - output_bytes
    vertex_reduction = source_vertices - output_vertices
    triangle_reduction = source_triangles - output_triangles
    recomputed_totals = {
        "assets": len(assets),
        "byte_reduction": byte_reduction,
        "byte_reduction_percent": rounded_percent(byte_reduction, source_bytes),
        "cats": sum(asset.get("kind") == "cat" for asset in assets),
        "output_bytes": output_bytes,
        "output_triangles": output_triangles,
        "output_vertices": output_vertices,
        "props": sum(asset.get("kind") == "prop" for asset in assets),
        "source_bytes": source_bytes,
        "source_triangles": source_triangles,
        "source_vertices": source_vertices,
        "triangle_reduction": triangle_reduction,
        "triangle_reduction_percent": rounded_percent(
            triangle_reduction, source_triangles
        ),
        "vertex_reduction": vertex_reduction,
        "vertex_reduction_percent": rounded_percent(
            vertex_reduction, source_vertices
        ),
    }
    for field, expected in recomputed_totals.items():
        if totals.get(field) != expected:
            errors.append(
                f"totals.{field} disagrees with assets: "
                f"metrics={totals.get(field)!r} recomputed={expected!r}"
            )

metrics_section = section_between(
    evidence,
    "## Exact reduction metrics",
    "## Bounds and structural preservation",
)
totals_pattern = re.compile(
    r"Totals:\s*\n\s*"
    r"- bytes: ([0-9,]+) → ([0-9,]+); reduction ([0-9,]+)\s*"
    r"\(\*\*([0-9]+\.[0-9]{6})%\*\*\);\s*"
    r"- vertices: ([0-9,]+) → ([0-9,]+); reduction ([0-9,]+)\s*"
    r"\(\*\*([0-9]+\.[0-9]{6})%\*\*\);\s*"
    r"- triangles: ([0-9,]+) → ([0-9,]+); reduction ([0-9,]+)\s*"
    r"\(\*\*([0-9]+\.[0-9]{6})%\*\*\);\s*"
    r"- inventory: ([0-9,]+) assets = ([0-9,]+) cats \+ ([0-9,]+) props\.",
)
totals_matches = list(totals_pattern.finditer(metrics_section))
if len(totals_matches) != 1:
    errors.append(f"Markdown totals record count must be one, got {len(totals_matches)}")
else:
    values = totals_matches[0].groups()
    markdown_totals = {
        "source_bytes": comma_int(values[0]),
        "output_bytes": comma_int(values[1]),
        "byte_reduction": comma_int(values[2]),
        "byte_reduction_percent": float(values[3]),
        "source_vertices": comma_int(values[4]),
        "output_vertices": comma_int(values[5]),
        "vertex_reduction": comma_int(values[6]),
        "vertex_reduction_percent": float(values[7]),
        "source_triangles": comma_int(values[8]),
        "output_triangles": comma_int(values[9]),
        "triangle_reduction": comma_int(values[10]),
        "triangle_reduction_percent": float(values[11]),
        "assets": comma_int(values[12]),
        "cats": comma_int(values[13]),
        "props": comma_int(values[14]),
    }
    for field, markdown_value in markdown_totals.items():
        if totals.get(field) != markdown_value:
            errors.append(
                f"totals.{field} mismatch: Markdown={markdown_value!r} "
                f"metrics={totals.get(field)!r}"
            )

reduction_pattern = re.compile(
    r"^\| `([^`]+)` \| (cat|prop) \| "
    r"([0-9,]+) → ([0-9,]+) \| "
    r"([0-9,]+) → ([0-9,]+) \| "
    r"([0-9,]+) → ([0-9,]+) \| "
    r"([0-9]+\.[0-9]{6})% \| ([0-9]+\.[0-9]{6})% \|$",
    re.MULTILINE,
)
reduction_rows = [
    {
        "id": match.group(1),
        "kind": match.group(2),
        "source_bytes": comma_int(match.group(3)),
        "output_bytes": comma_int(match.group(4)),
        "source_vertices": comma_int(match.group(5)),
        "output_vertices": comma_int(match.group(6)),
        "source_triangles": comma_int(match.group(7)),
        "output_triangles": comma_int(match.group(8)),
        "byte_reduction_percent": float(match.group(9)),
        "triangle_reduction_percent": float(match.group(10)),
    }
    for match in reduction_pattern.finditer(metrics_section)
]
if [row["id"] for row in reduction_rows] != metric_ids:
    errors.append("Markdown reduction rows do not match machine manifest order")
if len(reduction_rows) != len(assets):
    errors.append(
        f"Markdown reduction row count mismatch: "
        f"markdown={len(reduction_rows)} metrics={len(assets)}"
    )
for row, asset in zip(reduction_rows, assets):
    source = asset["source"]
    output = asset["output"]
    expected = {
        "id": asset["id"],
        "kind": asset["kind"],
        "source_bytes": source["bytes"],
        "output_bytes": output["bytes"],
        "source_vertices": source["vertices"],
        "output_vertices": output["vertices"],
        "source_triangles": source["triangles"],
        "output_triangles": output["triangles"],
        "byte_reduction_percent": asset["byte_reduction_percent"],
        "triangle_reduction_percent": asset["triangle_reduction_percent"],
    }
    if row != expected:
        errors.append(
            f"{asset['id']} reduction row mismatch: "
            f"Markdown={row!r} metrics={expected!r}"
        )

bounds_section = section_between(
    evidence,
    "## Bounds and structural preservation",
    "## Source and derivative custody",
)
bounds_pattern = re.compile(
    r"^\| `([^`]+)` \| ([0-9]+\.[0-9]{9}) \| "
    r"([0-9]+\.[0-9]{9}) \| ([0-9]+\.[0-9]{9}) \|$",
    re.MULTILINE,
)
bounds_rows = [
    (match.group(1), match.group(2), match.group(3), match.group(4))
    for match in bounds_pattern.finditer(bounds_section)
]
if [row[0] for row in bounds_rows] != metric_ids:
    errors.append("Markdown bounds rows do not match machine manifest order")
if len(bounds_rows) != len(assets):
    errors.append(
        f"Markdown bounds row count mismatch: "
        f"markdown={len(bounds_rows)} metrics={len(assets)}"
    )
for row, asset in zip(bounds_rows, assets):
    facts = bounds_facts(asset["source"], asset["output"])
    if facts is None:
        errors.append(f"{asset['id']} has invalid machine world bounds")
        continue
    center, scale, extent = facts
    expected = (
        asset["id"],
        f"{center:.9f}",
        f"{scale:.9f}",
        f"{extent:.9f}",
    )
    if row != expected:
        errors.append(
            f"{asset['id']} bounds row mismatch: "
            f"Markdown={row!r} metrics={expected!r}"
        )
    if center > 0.005 or scale > 0.01 or extent > 0.02:
        errors.append(f"{asset['id']} machine bounds miss preservation tolerances")

structure_pattern = re.compile(
    r"^\| `([^`]+)` \| ([0-9]+)/([0-9]+) \| exact \| "
    r"exact: ([a-z -]+(?:, [a-z -]+)*) \| ([0-9]+)/([0-9]+) \| none \|$",
    re.MULTILINE,
)
structure_rows = [
    {
        "id": match.group(1),
        "source_images": int(match.group(2)),
        "output_images": int(match.group(3)),
        "roles": [
            role.replace(" ", "_").replace("-", "_")
            for role in match.group(4).split(", ")
        ],
        "source_uv_material": int(match.group(5)),
        "output_uv_material": int(match.group(6)),
    }
    for match in structure_pattern.finditer(bounds_section)
]
if [row["id"] for row in structure_rows] != metric_ids:
    errors.append("Markdown preservation rows do not match machine manifest order")
if len(structure_rows) != len(assets):
    errors.append(
        f"Markdown preservation row count mismatch: "
        f"markdown={len(structure_rows)} metrics={len(assets)}"
    )
for row, asset in zip(structure_rows, assets):
    texture = asset["embedded_texture_preservation"]
    expected = {
        "id": asset["id"],
        "source_images": asset["source"]["embedded_images"],
        "output_images": asset["output"]["embedded_images"],
        "roles": list(texture["source_texture_role_payload_sha256"]),
        "source_uv_material": asset["source"]["uv_primitives"],
        "output_uv_material": asset["output"]["uv_primitives"],
    }
    if row != expected:
        errors.append(
            f"{asset['id']} preservation row mismatch: "
            f"Markdown={row!r} metrics={expected!r}"
        )

silhouette_section = section_between(
    evidence,
    "## Silhouette evidence — all 30 individual renders",
    "## Material-lit color evidence and visual verdict",
)
silhouette_pattern = re.compile(
    r"^\| `([^`]+)` \| (0\.[0-9]{12}) \| `([0-9a-f]{64})` \| "
    r"(0\.[0-9]{12}) \| `([0-9a-f]{64})` \|$",
    re.MULTILINE,
)
silhouette_rows = [
    {
        "id": match.group(1),
        "before_coverage": match.group(2),
        "before_sha256": match.group(3),
        "after_coverage": match.group(4),
        "after_sha256": match.group(5),
    }
    for match in silhouette_pattern.finditer(silhouette_section)
]
if [row["id"] for row in silhouette_rows] != metric_ids:
    errors.append("silhouette rows do not match machine manifest order")
if len(silhouette_rows) != 15:
    errors.append(f"silhouette pair inventory must be 15, got {len(silhouette_rows)}")
render_hashes = [
    row[field]
    for row in silhouette_rows
    for field in ("before_sha256", "after_sha256")
]
if len(render_hashes) != 30 or len(set(render_hashes)) != 30:
    errors.append("silhouette render hash inventory must contain 30 unique files")
if any(
    not (0.01 < float(row[field]) <= 1.0)
    for row in silhouette_rows
    for field in ("before_coverage", "after_coverage")
):
    errors.append("silhouette coverage must exceed the recorded 1% gate")
silhouette_table_sha = canonical_sha(silhouette_rows)
if silhouette_table_sha != EXPECTED_SILHOUETTE_TABLE_SHA256:
    errors.append(
        "silhouette evidence inventory changed: "
        f"expected={EXPECTED_SILHOUETTE_TABLE_SHA256} "
        f"actual={silhouette_table_sha}"
    )

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
