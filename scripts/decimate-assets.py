#!/usr/bin/env python3
"""Validate, decimate, and atomically publish generated GLB derivatives."""

from __future__ import annotations

import sys


sys.dont_write_bytecode = True

import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import tempfile
import uuid
from collections.abc import Mapping
from datetime import datetime, timezone
from pathlib import Path


_SCRIPT_DIR = Path(__file__).resolve().parent
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))
from glb_metrics import GlbError, compare_preservation, inspect_glb


BLENDER_VERSION = "5.1.2"
BLENDER_BUILD_HASH = "ec6e62d40fa9"
POLICY = {
    "cat": {"target": 15_000, "minimum": 13_500, "maximum": 15_000},
    "prop": {"target": 10_000, "minimum": 9_000, "maximum": 10_000},
}
REQUIRED_SOURCE_FIELDS = {
    "service", "task_id", "timestamp_utc", "plan_tier", "prompt", "note", "sha256"
}
KNOWN_SERVICES = frozenset({"meshy", "tripo"})
METRIC_SUBSET_FIELDS = (
    "triangles",
    "vertices",
    "primitives",
    "materials",
    "material_primitives",
    "images",
    "embedded_images",
    "uv_primitives",
    "animations",
    "cameras",
    "lights",
    "skins",
    "morph_targets",
    "extensions_used",
    "extensions_required",
    "world_bounds",
)
GLOBAL_MINIMUM_TRIANGLES = 5_000
GLOBAL_MAXIMUM_TRIANGLES = 20_000
BLENDER_TIMEOUT_SECONDS = 1_800
VERSION_TIMEOUT_SECONDS = 60
_FORBIDDEN_ENVIRONMENT_PARTS = (
    "KEY",
    "TOKEN",
    "SECRET",
    "AUTH",
    "CREDENTIAL",
    "BEARER",
)
_FORBIDDEN_PROVENANCE = re.compile(
    r"api[_-]?key|token|secret|authorization|credential|bearer|https?://",
    re.IGNORECASE,
)
_LOWER_SHA256 = re.compile(r"[0-9a-f]{64}")
_UTC_TIMESTAMP = re.compile(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z")


class DecimationError(RuntimeError):
    """Expected fail-closed pipeline error suitable for a concise CLI report."""


def _glb_error_message(exc: GlbError) -> str:
    if str(exc) == "truncated GLB":
        return "invalid GLB header"
    return str(exc)


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _is_relative_to(path: Path, root: Path) -> bool:
    try:
        path.relative_to(root)
    except ValueError:
        return False
    return True


def _resolved_within(path: Path, root: Path, label: str) -> Path:
    try:
        resolved = path.resolve(strict=False)
    except (OSError, RuntimeError) as exc:
        raise DecimationError(f"{label} path cannot be resolved") from exc
    if not _is_relative_to(resolved, root):
        raise DecimationError(f"{label} path escapes selected root")
    return resolved


def _path_exists(path: Path) -> bool:
    return os.path.lexists(path)


def _load_json_bytes(path: Path, label: str) -> tuple[object, bytes]:
    try:
        payload = path.read_bytes()
        return json.loads(payload), payload
    except (OSError, UnicodeDecodeError, json.JSONDecodeError, RecursionError) as exc:
        raise DecimationError(f"invalid {label}") from exc


def _nonempty_string(value: object, label: str) -> str:
    if not isinstance(value, str) or not value or value.strip() != value:
        raise DecimationError(f"invalid manifest: {label} must be a non-empty string")
    return value


def _bare_glb_filename(value: object) -> str:
    if not isinstance(value, str):
        raise DecimationError("invalid manifest: out must be a bare .glb filename")
    candidate = Path(value)
    if (
        not value
        or value.strip() != value
        or len(value) <= len(".glb")
        or not value.endswith(".glb")
        or candidate.name != value
        or len(candidate.parts) != 1
        or "/" in value
        or "\\" in value
        or "\x00" in value
    ):
        raise DecimationError("invalid manifest: out must be a bare .glb filename")
    return value


def _load_manifest(path: Path) -> list[dict[str, str]]:
    document, _ = _load_json_bytes(path, "manifest")
    if not isinstance(document, dict):
        raise DecimationError("invalid manifest: root must be an object")
    entries = document.get("assets")
    if not isinstance(entries, list) or not entries:
        raise DecimationError("invalid manifest: assets must be a non-empty list")

    assets: list[dict[str, str]] = []
    identifiers: set[str] = set()
    outputs: set[str] = set()
    for index, value in enumerate(entries):
        if not isinstance(value, dict):
            raise DecimationError(
                f"invalid manifest: assets[{index}] must be an object"
            )
        identifier = _nonempty_string(value.get("id"), f"assets[{index}].id")
        output = _bare_glb_filename(value.get("out"))
        kind = _nonempty_string(value.get("kind"), f"assets[{index}].kind")
        service = _nonempty_string(
            value.get("service"), f"assets[{index}].service"
        )
        prompt = _nonempty_string(value.get("prompt"), f"assets[{index}].prompt")
        if identifier in identifiers or output in outputs:
            raise DecimationError("invalid manifest: duplicate id or out")
        if kind not in POLICY:
            raise DecimationError(f"unsupported kind for asset {identifier}")
        if service not in KNOWN_SERVICES:
            raise DecimationError(f"unsupported service for asset {identifier}")
        identifiers.add(identifier)
        outputs.add(output)
        assets.append(
            {
                "id": identifier,
                "out": output,
                "kind": kind,
                "service": service,
                "prompt": prompt,
            }
        )
    return assets


def _validate_source_record(
    source_record: object,
    asset: Mapping[str, str],
    source_sha: str,
) -> dict[str, str]:
    if not isinstance(source_record, dict):
        raise DecimationError("invalid source sidecar: root must be an object")
    missing = REQUIRED_SOURCE_FIELDS - set(source_record)
    if missing:
        raise DecimationError("invalid source sidecar: missing required fields")

    validated: dict[str, str] = {}
    for name in REQUIRED_SOURCE_FIELDS:
        value = source_record[name]
        if not isinstance(value, str):
            raise DecimationError(f"invalid source sidecar: {name} must be a string")
        if name != "note" and not value:
            raise DecimationError(
                f"invalid source sidecar: {name} must be non-empty"
            )
        validated[name] = value

    claimed_sha = validated["sha256"]
    if _LOWER_SHA256.fullmatch(claimed_sha) is None:
        raise DecimationError("source SHA-256 must be lowercase hexadecimal")
    if claimed_sha != source_sha:
        raise DecimationError("source SHA-256 mismatch")
    if validated["plan_tier"] != "paid":
        raise DecimationError("source plan_tier must be paid")
    if validated["service"] != asset["service"]:
        raise DecimationError("source service does not match manifest")
    if validated["prompt"] != asset["prompt"]:
        raise DecimationError("source prompt does not match manifest")
    if validated["service"] not in KNOWN_SERVICES:
        raise DecimationError("source service is unsupported")
    if _UTC_TIMESTAMP.fullmatch(validated["timestamp_utc"]) is None:
        raise DecimationError("source timestamp_utc must be UTC second precision")
    return validated


def _validate_source_structure(metrics: Mapping[str, object]) -> None:
    if metrics.get("meshes") != 1 or metrics.get("primitives") != 1:
        raise DecimationError("source must contain exactly one mesh and one primitive")
    if metrics.get("materials") != 1 or metrics.get("material_primitives") != 1:
        raise DecimationError("source must contain exactly one bound material")
    if metrics.get("uv_primitives") != 1:
        raise DecimationError("source must bind UVs on its primitive")
    images = metrics.get("images")
    embedded_images = metrics.get("embedded_images")
    if not isinstance(images, int) or images < 1 or embedded_images != images:
        raise DecimationError("source must contain only embedded texture images")
    if metrics.get("external_uris"):
        raise DecimationError("source contains an external URI")
    if metrics.get("extensions_used") or metrics.get("extensions_required"):
        raise DecimationError("source contains an unsupported extension")
    for name in ("animations", "cameras", "lights", "skins", "morph_targets"):
        if metrics.get(name) != 0:
            raise DecimationError("source contains animation/camera/light/skin/morph data")


def _sanitized_environment() -> dict[str, str]:
    child_env = os.environ.copy()
    for name in list(child_env):
        uppercase = name.upper()
        if any(part in uppercase for part in _FORBIDDEN_ENVIRONMENT_PARTS):
            del child_env[name]
    return child_env


def _resolve_blender(value: str | None) -> Path:
    if value is None:
        located = shutil.which("blender")
        if located is None:
            raise DecimationError("Blender executable was not found")
        candidate = Path(located)
    else:
        candidate = Path(value)
    try:
        blender = candidate.resolve(strict=True)
    except OSError as exc:
        raise DecimationError("Blender executable was not found") from exc
    if not blender.is_file() or not os.access(blender, os.X_OK):
        raise DecimationError("Blender executable is not executable")
    return blender


def _check_blender_version(blender: Path, child_env: Mapping[str, str]) -> None:
    try:
        result = subprocess.run(
            [str(blender), "--background", "--version"],
            check=True,
            shell=False,
            stdin=subprocess.DEVNULL,
            timeout=VERSION_TIMEOUT_SECONDS,
            env=dict(child_env),
            capture_output=True,
            text=True,
        )
    except (subprocess.CalledProcessError, subprocess.TimeoutExpired, OSError) as exc:
        raise DecimationError("Blender version check failed") from exc

    lines = [line.strip() for line in result.stdout.splitlines() if line.strip()]
    if not lines or lines[0] != f"Blender {BLENDER_VERSION}":
        raise DecimationError(f"requires Blender {BLENDER_VERSION}")
    expected_build = f"build hash: {BLENDER_BUILD_HASH}"
    if expected_build not in lines:
        raise DecimationError(f"requires Blender build {BLENDER_BUILD_HASH}")


def metric_subset(metrics: Mapping[str, object]) -> dict[str, object]:
    return {name: metrics[name] for name in METRIC_SUBSET_FIELDS}


def _reject_forbidden_provenance(value: object) -> None:
    pending = [value]
    while pending:
        current = pending.pop()
        if isinstance(current, dict):
            for key, child in current.items():
                if _FORBIDDEN_PROVENANCE.search(str(key)):
                    raise DecimationError("provenance contains a forbidden secret-shaped key")
                pending.append(child)
        elif isinstance(current, list):
            pending.extend(current)
        elif isinstance(current, str) and _FORBIDDEN_PROVENANCE.search(current):
            raise DecimationError("provenance contains a forbidden secret-shaped value")


def write_staged_provenance(path: Path, record: Mapping[str, object]) -> None:
    """Create one validated staged provenance file without replacing anything."""
    path = Path(path)
    _reject_forbidden_provenance(record)
    serialized = json.dumps(record, indent=2, sort_keys=True) + "\n"
    created = False
    try:
        with path.open("x", encoding="utf-8", newline="\n") as handle:
            created = True
            handle.write(serialized)
            handle.flush()
            os.fsync(handle.fileno())
    except BaseException:
        if created:
            try:
                path.unlink(missing_ok=True)
            except OSError:
                pass
        raise


def _unique_backup(path: Path) -> Path:
    for _ in range(100):
        candidate = path.with_name(f".{path.name}.backup-{uuid.uuid4().hex}")
        if not _path_exists(candidate):
            return candidate
    raise DecimationError("could not allocate a unique derivative backup path")


def _restore_backup(backup: Path, final: Path) -> None:
    if _path_exists(final):
        final.unlink()
    os.replace(backup, final)


def promote_pair(
    staged_glb: Path,
    staged_json: Path,
    final_glb: Path,
    final_json: Path,
    force: bool,
) -> None:
    """Promote a derivative/provenance pair, rolling back the complete old pair."""
    staged_glb = Path(staged_glb)
    staged_json = Path(staged_json)
    final_glb = Path(final_glb)
    final_json = Path(final_json)
    if not staged_glb.is_file() or not staged_json.is_file():
        raise DecimationError("staged derivative pair is incomplete")

    glb_exists = _path_exists(final_glb)
    json_exists = _path_exists(final_json)
    if glb_exists != json_exists:
        raise DecimationError("existing derivative lineage is inconsistent")
    if glb_exists and not force:
        raise DecimationError("refusing existing derivative without --force")

    if not glb_exists:
        try:
            os.replace(staged_glb, final_glb)
            try:
                os.replace(staged_json, final_json)
            except BaseException:
                final_glb.unlink(missing_ok=True)
                raise
        except BaseException:
            raise
        return

    backup_glb = _unique_backup(final_glb)
    backup_json = _unique_backup(final_json)
    moved_glb = False
    moved_json = False
    promoted_glb = False
    promoted_json = False
    try:
        os.replace(final_glb, backup_glb)
        moved_glb = True
        try:
            os.replace(final_json, backup_json)
            moved_json = True
        except BaseException:
            os.replace(backup_glb, final_glb)
            moved_glb = False
            raise

        try:
            os.replace(staged_glb, final_glb)
            promoted_glb = True
            os.replace(staged_json, final_json)
            promoted_json = True
        except BaseException:
            if promoted_glb and _path_exists(final_glb):
                final_glb.unlink()
            if promoted_json and _path_exists(final_json):
                final_json.unlink()
            _restore_backup(backup_glb, final_glb)
            moved_glb = False
            _restore_backup(backup_json, final_json)
            moved_json = False
            raise
    finally:
        if not moved_glb:
            backup_glb.unlink(missing_ok=True)
        if not moved_json:
            backup_json.unlink(missing_ok=True)

    backup_glb.unlink(missing_ok=True)
    backup_json.unlink(missing_ok=True)


def _destination_state(final_glb: Path, final_json: Path, force: bool) -> None:
    glb_exists = _path_exists(final_glb)
    json_exists = _path_exists(final_json)
    if glb_exists != json_exists:
        raise DecimationError("existing derivative lineage is inconsistent")
    if glb_exists and not force:
        raise DecimationError("refusing existing derivative without --force")


def _candidate_preservation(
    source_metrics: Mapping[str, object], output_metrics: Mapping[str, object]
) -> None:
    if output_metrics["uv_primitives"] != output_metrics["primitives"]:
        raise DecimationError("derivative lost UV bindings")
    extensions = set(output_metrics["extensions_used"]) | set(
        output_metrics["extensions_required"]
    )
    if extensions:
        raise DecimationError(
            "derivative contains unsupported extension: "
            + ", ".join(sorted(extensions))
        )
    reasons = compare_preservation(source_metrics, output_metrics)
    if reasons:
        raise DecimationError("; ".join(reasons))


def _provenance_record(
    source_path: Path,
    source_sha: str,
    source_sidecar_sha: str,
    source_record: Mapping[str, str],
    final_glb: Path,
    kind: str,
    policy: Mapping[str, int],
    source_metrics: Mapping[str, object],
    output_metrics: Mapping[str, object],
) -> dict[str, object]:
    return {
        "schema_version": 1,
        "source": {
            "filename": source_path.name,
            "sha256": source_sha,
            "sidecar_sha256": source_sidecar_sha,
            "provenance": {
                name: source_record[name]
                for name in sorted(REQUIRED_SOURCE_FIELDS - {"sha256"})
            },
        },
        "derivative": {
            "filename": final_glb.name,
            "sha256": output_metrics["sha256"],
        },
        "tool": {
            "name": "Blender",
            "version": BLENDER_VERSION,
            "build_hash": BLENDER_BUILD_HASH,
            "operation": "collapse-decimate",
            "timestamp_utc": datetime.now(timezone.utc).strftime(
                "%Y-%m-%dT%H:%M:%SZ"
            ),
        },
        "geometry": {
            "category": kind,
            "target_triangles": policy["target"],
            "accepted_minimum": policy["minimum"],
            "accepted_maximum": policy["maximum"],
            "source": metric_subset(source_metrics),
            "output": metric_subset(output_metrics),
        },
    }


def _process_asset(
    asset: Mapping[str, str],
    prepared: Mapping[str, object],
    blender: Path,
    driver: Path,
    child_env: Mapping[str, str],
    run_staging: Path,
    force: bool,
) -> None:
    identifier = asset["id"]
    kind = asset["kind"]
    policy = POLICY[kind]
    source_path = prepared["source_path"]
    source_sidecar_path = prepared["source_sidecar_path"]
    final_glb = prepared["final_glb"]
    final_json = prepared["final_json"]
    source_metrics = prepared["source_metrics"]
    source_record = prepared["source_record"]
    source_sha = prepared["source_sha"]
    source_sidecar_sha = prepared["source_sidecar_sha"]
    if not all(
        isinstance(path, Path)
        for path in (source_path, source_sidecar_path, final_glb, final_json)
    ):
        raise AssertionError("internal prepared path type mismatch")
    if not isinstance(source_metrics, dict) or not isinstance(source_record, dict):
        raise AssertionError("internal prepared source type mismatch")
    if not isinstance(source_sha, str) or not isinstance(source_sidecar_sha, str):
        raise AssertionError("internal prepared hash type mismatch")

    asset_staging = Path(tempfile.mkdtemp(prefix="asset-", dir=run_staging))
    os.chmod(asset_staging, 0o700)
    staged_glb = asset_staging / asset["out"]
    staged_json = asset_staging / f"{asset['out']}.json"
    print(
        "glb-decimation: "
        f"asset={identifier} category={kind} target={policy['target']} "
        f"source_triangles={source_metrics['triangles']}"
    )
    command = [
        str(blender),
        "--background",
        "--factory-startup",
        "--offline-mode",
        "--disable-autoexec",
        "--threads",
        "1",
        "--python-exit-code",
        "97",
        "--python",
        str(driver),
        "--",
        "--source",
        str(source_path),
        "--output",
        str(staged_glb),
        "--source-triangles",
        str(source_metrics["triangles"]),
        "--target-triangles",
        str(policy["target"]),
        "--minimum-triangles",
        str(policy["minimum"]),
        "--maximum-triangles",
        str(policy["maximum"]),
    ]
    try:
        subprocess.run(
            command,
            check=True,
            shell=False,
            stdin=subprocess.DEVNULL,
            timeout=BLENDER_TIMEOUT_SECONDS,
            env=dict(child_env),
        )
    except subprocess.CalledProcessError as exc:
        raise DecimationError(
            f"Blender failed for asset {identifier} with exit {exc.returncode}"
        ) from exc
    except subprocess.TimeoutExpired as exc:
        raise DecimationError(f"Blender failed for asset {identifier}: timeout") from exc
    except OSError as exc:
        raise DecimationError(f"Blender failed for asset {identifier}") from exc

    if not staged_glb.is_file() or staged_glb.stat().st_size == 0:
        raise DecimationError("Blender failed to produce a non-empty derivative")
    try:
        output_metrics = inspect_glb(staged_glb)
    except (GlbError, OSError) as exc:
        message = _glb_error_message(exc) if isinstance(exc, GlbError) else str(exc)
        raise DecimationError(message) from exc
    triangles = output_metrics["triangles"]
    if not isinstance(triangles, int) or not (
        policy["minimum"] <= triangles <= policy["maximum"]
    ):
        raise DecimationError(
            f"derivative triangle band miss for {kind}: {triangles}"
        )
    if not GLOBAL_MINIMUM_TRIANGLES <= triangles <= GLOBAL_MAXIMUM_TRIANGLES:
        raise DecimationError(f"derivative triangle band misses global range: {triangles}")
    _candidate_preservation(source_metrics, output_metrics)

    if _sha256(source_path) != source_sha:
        raise DecimationError("source changed during decimation")
    if _sha256(source_sidecar_path) != source_sidecar_sha:
        raise DecimationError("source sidecar changed during decimation")

    record = _provenance_record(
        source_path,
        source_sha,
        source_sidecar_sha,
        source_record,
        final_glb,
        kind,
        policy,
        source_metrics,
        output_metrics,
    )
    write_staged_provenance(staged_json, record)
    staged_record, _ = _load_json_bytes(staged_json, "staged provenance")
    _reject_forbidden_provenance(staged_record)
    if not isinstance(staged_record, dict):
        raise DecimationError("invalid staged provenance")
    derivative = staged_record.get("derivative")
    if not isinstance(derivative, dict) or derivative.get("sha256") != _sha256(
        staged_glb
    ):
        raise DecimationError("staged provenance derivative SHA-256 mismatch")
    promote_pair(staged_glb, staged_json, final_glb, final_json, force)
    print(
        "glb-decimation: "
        f"asset={identifier} output_triangles={output_metrics['triangles']} "
        f"output_vertices={output_metrics['vertices']}"
    )


def _prepare_assets(
    assets: list[dict[str, str]],
    input_base: Path,
    input_root: Path,
    output_root: Path,
    force: bool,
) -> list[dict[str, object]]:
    prepared_assets: list[dict[str, object]] = []
    source_paths: set[Path] = set()
    output_paths: set[Path] = set()
    for asset in assets:
        source_path = input_base / asset["out"]
        source_sidecar_path = input_base / f"{asset['out']}.json"
        final_glb = output_root / asset["out"]
        final_json = output_root / f"{asset['out']}.json"

        resolved_source = _resolved_within(source_path, input_root, "source")
        resolved_source_sidecar = _resolved_within(
            source_sidecar_path, input_root, "source sidecar"
        )
        resolved_final_glb = _resolved_within(final_glb, output_root, "output")
        resolved_final_json = _resolved_within(final_json, output_root, "output")
        if not source_path.is_file():
            raise DecimationError(f"missing source for asset {asset['id']}")
        if not source_sidecar_path.is_file():
            raise DecimationError(f"missing source sidecar for asset {asset['id']}")
        _destination_state(final_glb, final_json, force)

        if resolved_source in source_paths or resolved_source_sidecar in source_paths:
            raise DecimationError("source paths alias across manifest entries")
        source_paths.update((resolved_source, resolved_source_sidecar))
        if resolved_final_glb in output_paths or resolved_final_json in output_paths:
            raise DecimationError("output paths alias across manifest entries")
        output_paths.update((resolved_final_glb, resolved_final_json))

        source_sha = _sha256(source_path)
        source_record_value, source_sidecar_bytes = _load_json_bytes(
            source_sidecar_path, "source sidecar"
        )
        source_sidecar_sha = hashlib.sha256(source_sidecar_bytes).hexdigest()
        source_record = _validate_source_record(
            source_record_value, asset, source_sha
        )
        try:
            source_metrics = inspect_glb(source_path)
        except (GlbError, OSError) as exc:
            message = _glb_error_message(exc) if isinstance(exc, GlbError) else str(exc)
            raise DecimationError(message) from exc
        if source_metrics["sha256"] != source_sha:
            raise DecimationError("source changed during validation")
        _validate_source_structure(source_metrics)
        triangles = source_metrics["triangles"]
        if not isinstance(triangles, int) or triangles <= POLICY[asset["kind"]]["target"]:
            raise DecimationError(
                f"source already within budget for asset {asset['id']}"
            )
        prepared_assets.append(
            {
                "source_path": source_path,
                "source_sidecar_path": source_sidecar_path,
                "final_glb": final_glb,
                "final_json": final_json,
                "source_sha": source_sha,
                "source_sidecar_sha": source_sidecar_sha,
                "source_record": source_record,
                "source_metrics": source_metrics,
            }
        )

    if source_paths & output_paths:
        raise DecimationError("source and output paths alias")
    return prepared_assets


def _arguments(argv: list[str]) -> argparse.Namespace:
    repository = Path(__file__).resolve().parent.parent
    default_input = repository / "unity/Assets/Art/Generated/incoming"
    parser = argparse.ArgumentParser(prog="decimate-assets.py")
    parser.add_argument(
        "--manifest",
        type=Path,
        default=repository / "docs/design/assets/CAT-MANIFEST.json",
    )
    parser.add_argument("--input-dir", type=Path, default=default_input)
    parser.add_argument("--output-dir", type=Path, default=default_input / "decimated")
    parser.add_argument("--blender")
    parser.add_argument("--force", action="store_true")
    return parser.parse_args(argv)


def _run(argv: list[str]) -> None:
    args = _arguments(argv)
    manifest = args.manifest.resolve(strict=True)
    if not manifest.is_file():
        raise DecimationError("invalid manifest: path is not a file")
    input_base = Path(os.path.abspath(args.input_dir))
    input_root = input_base.resolve(strict=True)
    if not input_root.is_dir():
        raise DecimationError("input directory is missing")
    assets = _load_manifest(manifest)
    args.output_dir.mkdir(parents=True, exist_ok=True)
    output_root = Path(os.path.abspath(args.output_dir)).resolve(strict=True)
    if not output_root.is_dir():
        raise DecimationError("output directory is missing")

    prepared_assets = _prepare_assets(
        assets,
        input_base,
        input_root,
        output_root,
        args.force,
    )
    blender = _resolve_blender(args.blender)
    driver = Path(__file__).resolve().with_name("blender_decimate.py")
    if not driver.is_file():
        raise DecimationError("Blender decimation driver is missing")
    child_env = _sanitized_environment()
    _check_blender_version(blender, child_env)

    with tempfile.TemporaryDirectory(
        prefix=".glb-decimation-", dir=output_root
    ) as staging_name:
        run_staging = Path(staging_name)
        os.chmod(run_staging, 0o700)
        for asset, prepared in zip(assets, prepared_assets, strict=True):
            _process_asset(
                asset,
                prepared,
                blender,
                driver,
                child_env,
                run_staging,
                args.force,
            )


def main(argv: list[str]) -> int:
    """Run the orchestrator without exiting, for CLI and fault-injection use."""
    try:
        _run(argv)
    except (DecimationError, GlbError, OSError, ValueError) as exc:
        print(f"glb-decimation: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
