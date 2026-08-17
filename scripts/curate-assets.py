#!/usr/bin/env python3
"""Stage, validate, back up, and publish one frozen Cat Metro source curation."""

from __future__ import annotations

import sys


sys.dont_write_bytecode = True

import argparse
import hashlib
import json
import os
import re
import shutil
import stat
import subprocess
import tempfile
import uuid
from collections.abc import Callable, Mapping
from pathlib import Path


_SCRIPT_DIR = Path(__file__).resolve().parent
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from glb_metrics import GlbError, inspect_glb  # noqa: E402


LOAF_ID = "cat-blue-siamese-loaf"
WAVE_ID = "cat-yellow-longhair-wave"
ALLOWED_SOURCE_SHA256 = {
    LOAF_ID: "e3015351ec9bda2aebeafcc0ff23f5aa35512af4234c168d79cac750118070e3",
    WAVE_ID: "8d7190fd24f552f874bf1d733f2870c44a24c27d6b50cfe1e32095f625fcc57c",
}
ASSET_FILENAMES = {
    LOAF_ID: "cat-blue-siamese-loaf.glb",
    WAVE_ID: "cat-yellow-longhair-wave.glb",
}
CURATION_NOTES = {
    LOAF_ID: "Cat Metro GLB-CURATION: removed ruled min-Y display plinth",
    WAVE_ID: "Cat Metro GLB-CURATION: removed ruled min-Y foot fragment",
}
EXPECTED_CURATED_TRIANGLES = {
    LOAF_ID: 773_061,
    WAVE_ID: 1_422_808,
}
REQUIRED_SOURCE_FIELDS = {
    "service",
    "task_id",
    "timestamp_utc",
    "plan_tier",
    "prompt",
    "note",
    "sha256",
}
MAX_METADATA_BYTES = 1_048_576
MAX_SOURCE_BYTES = 128 * 1024 * 1024
BLENDER_TIMEOUT_SECONDS = 1_800
_LOWER_SHA256 = re.compile(r"[0-9a-f]{64}\Z")
_CHILD_ENVIRONMENT_PASSTHROUGH = (
    "PATH",
    "LANG",
    "LC_ALL",
    "LC_CTYPE",
    "__CF_USER_TEXT_ENCODING",
)


class CurationError(RuntimeError):
    """Raised when source curation cannot complete without weakening custody."""


def _regular_single_link(path: Path, label: str) -> os.stat_result:
    try:
        status = path.lstat()
    except FileNotFoundError as exc:
        raise CurationError(f"{label} is missing") from exc
    if not stat.S_ISREG(status.st_mode) or status.st_nlink != 1:
        raise CurationError(f"{label} must be a regular single-link file")
    return status


def _sha256_file(path: Path, maximum_bytes: int, label: str) -> str:
    status = _regular_single_link(path, label)
    if status.st_size <= 0 or status.st_size > maximum_bytes:
        raise CurationError(f"{label} has an invalid byte size")
    digest = hashlib.sha256()
    total = 0
    with path.open("rb") as handle:
        while block := handle.read(1024 * 1024):
            total += len(block)
            if total > maximum_bytes:
                raise CurationError(f"{label} exceeds its byte limit")
            digest.update(block)
    if total != status.st_size:
        raise CurationError(f"{label} changed while hashing")
    current = path.lstat()
    if (current.st_dev, current.st_ino, current.st_size, current.st_mtime_ns) != (
        status.st_dev,
        status.st_ino,
        status.st_size,
        status.st_mtime_ns,
    ):
        raise CurationError(f"{label} changed while hashing")
    return digest.hexdigest()


def _load_source_record(path: Path) -> dict[str, object]:
    _regular_single_link(path, "source sidecar")
    if path.stat().st_size > MAX_METADATA_BYTES:
        raise CurationError("source sidecar exceeds its byte limit")
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, UnicodeError) as exc:
        raise CurationError("source sidecar is invalid JSON") from exc
    if not isinstance(value, dict):
        raise CurationError("source sidecar root must be an object")
    return value


def _validate_source_record(
    asset_id: str,
    source_record: Mapping[str, object],
    source_sha256: str,
    *,
    require_precuration_anchor: bool,
) -> None:
    missing = REQUIRED_SOURCE_FIELDS - set(source_record)
    if missing:
        raise CurationError("source sidecar is missing required fields")
    for name in REQUIRED_SOURCE_FIELDS:
        value = source_record[name]
        if not isinstance(value, str) or (name != "note" and not value):
            raise CurationError(f"source sidecar {name} must be a string")
    if source_record["sha256"] != source_sha256:
        raise CurationError("source sidecar SHA-256 mismatch")
    if (
        require_precuration_anchor
        and source_record["sha256"] != ALLOWED_SOURCE_SHA256[asset_id]
    ):
        raise CurationError("source sidecar is not at the frozen pre-curation anchor")
    if source_record["service"] != "tripo":
        raise CurationError("source service must remain tripo")
    if source_record["plan_tier"] != "paid":
        raise CurationError("source plan tier must remain paid")


def build_curated_source_record(
    asset_id: str,
    source_record: Mapping[str, object],
    curated_sha256: str,
) -> dict[str, object]:
    """Preserve generation provenance while recording curation in schema-1 note."""

    if asset_id not in ALLOWED_SOURCE_SHA256:
        raise CurationError("asset ID is outside the frozen curation allowlist")
    if _LOWER_SHA256.fullmatch(curated_sha256) is None:
        raise CurationError("curated source SHA-256 must be lowercase hexadecimal")
    original_sha = source_record.get("sha256")
    if original_sha != ALLOWED_SOURCE_SHA256[asset_id]:
        raise CurationError("source record is not at the frozen pre-curation anchor")
    note = source_record.get("note")
    if not isinstance(note, str):
        raise CurationError("source note must be a string")
    if "Cat Metro GLB-CURATION:" in note:
        raise CurationError("source record is already curated")
    updated = dict(source_record)
    updated["sha256"] = curated_sha256
    updated["note"] = f"{note}; {CURATION_NOTES[asset_id]}" if note else CURATION_NOTES[asset_id]
    return updated


def _write_private_json(path: Path, value: Mapping[str, object]) -> None:
    payload = (
        json.dumps(value, allow_nan=False, indent=2, sort_keys=True) + "\n"
    ).encode("utf-8")
    if len(payload) > MAX_METADATA_BYTES:
        raise CurationError("curated source sidecar exceeds its byte limit")
    descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    try:
        with os.fdopen(descriptor, "wb", closefd=True) as handle:
            handle.write(payload)
            handle.flush()
            os.fsync(handle.fileno())
    except BaseException:
        try:
            path.unlink(missing_ok=True)
        except OSError:
            pass
        raise


def _copy_new(source: Path, destination: Path) -> None:
    _regular_single_link(source, "backup source")
    descriptor = os.open(
        destination,
        os.O_WRONLY | os.O_CREAT | os.O_EXCL,
        0o600,
    )
    try:
        with source.open("rb") as input_handle, os.fdopen(
            descriptor, "wb", closefd=True
        ) as output_handle:
            shutil.copyfileobj(input_handle, output_handle, length=1024 * 1024)
            output_handle.flush()
            os.fsync(output_handle.fileno())
    except BaseException:
        try:
            destination.unlink(missing_ok=True)
        except OSError:
            pass
        raise


def _fsync_directory(path: Path) -> None:
    descriptor = os.open(path, os.O_RDONLY)
    try:
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def _restore_from_backup(
    backup: Path,
    final: Path,
    replace_fn: Callable[[os.PathLike[str], os.PathLike[str]], object],
) -> None:
    restore = final.parent / f".{final.name}.rollback-{uuid.uuid4().hex}"
    try:
        _copy_new(backup, restore)
        replace_fn(restore, final)
        _fsync_directory(final.parent)
    finally:
        if restore.exists() and not restore.is_symlink():
            restore.unlink()


def publish_pair(
    *,
    staged_glb: Path,
    staged_sidecar: Path,
    final_glb: Path,
    final_sidecar: Path,
    backup_dir: Path,
    replace_fn: Callable[[os.PathLike[str], os.PathLike[str]], object] = os.replace,
) -> None:
    """Back up and pair-promote a source GLB/sidecar with complete rollback."""

    for path, label in (
        (staged_glb, "staged GLB"),
        (staged_sidecar, "staged sidecar"),
        (final_glb, "final GLB"),
        (final_sidecar, "final sidecar"),
    ):
        _regular_single_link(path, label)
    if final_glb.parent != final_sidecar.parent:
        raise CurationError("final source pair must share one directory")
    if staged_glb.parent != staged_sidecar.parent:
        raise CurationError("staged source pair must share one directory")
    if backup_dir.exists() or backup_dir.is_symlink():
        raise CurationError("backup directory already exists")
    if not backup_dir.parent.is_dir() or backup_dir.parent.is_symlink():
        raise CurationError("backup parent must be an existing real directory")

    backup_glb = backup_dir / final_glb.name
    backup_sidecar = backup_dir / final_sidecar.name
    backup_created = False
    try:
        backup_dir.mkdir(mode=0o700)
        backup_created = True
        _copy_new(final_glb, backup_glb)
        _copy_new(final_sidecar, backup_sidecar)
        _fsync_directory(backup_dir)
        _fsync_directory(backup_dir.parent)
    except (OSError, CurationError) as exc:
        if backup_created:
            for member in (backup_glb, backup_sidecar):
                if member.exists() and not member.is_symlink():
                    member.unlink()
            try:
                backup_dir.rmdir()
            except OSError:
                pass
        raise CurationError("could not create complete source backup") from exc

    try:
        replace_fn(staged_glb, final_glb)
        replace_fn(staged_sidecar, final_sidecar)
        _fsync_directory(final_glb.parent)
    except (OSError, CurationError) as exc:
        try:
            _restore_from_backup(backup_glb, final_glb, replace_fn)
            _restore_from_backup(backup_sidecar, final_sidecar, replace_fn)
        except (OSError, CurationError) as rollback_exc:
            raise CurationError(
                "promotion failed and source-pair rollback also failed"
            ) from rollback_exc
        raise CurationError("promotion failed; original source pair restored") from exc


def _candidate_structure(
    asset_id: str,
    source_metrics: Mapping[str, object],
    candidate_metrics: Mapping[str, object],
    report: Mapping[str, object],
) -> None:
    if report.get("asset_id") != asset_id:
        raise CurationError("Blender curation report asset mismatch")
    expected_triangles = EXPECTED_CURATED_TRIANGLES[asset_id]
    if (
        report.get("triangles_after") != expected_triangles
        or candidate_metrics.get("triangles") != expected_triangles
    ):
        raise CurationError("curated source triangle count mismatch")
    exact_counts = (
        "meshes",
        "primitives",
        "materials",
        "material_primitives",
        "images",
        "embedded_images",
        "uv_primitives",
    )
    for field in exact_counts:
        if candidate_metrics.get(field) != source_metrics.get(field):
            raise CurationError(f"curated source changed {field}")
    zero_counts = (
        "animations",
        "cameras",
        "lights",
        "skins",
        "morph_targets",
    )
    for field in zero_counts:
        if candidate_metrics.get(field) != 0:
            raise CurationError(f"curated source introduced {field}")
    if candidate_metrics.get("external_uris"):
        raise CurationError("curated source contains an external URI")
    if candidate_metrics.get("extensions_used") or candidate_metrics.get(
        "extensions_required"
    ):
        raise CurationError("curated source contains an unsupported extension")


def _child_environment() -> dict[str, str]:
    return {
        name: os.environ[name]
        for name in _CHILD_ENVIRONMENT_PASSTHROUGH
        if name in os.environ
    }


def _run_blender(
    blender: Path,
    driver: Path,
    asset_id: str,
    source: Path,
    output: Path,
    report: Path,
    working_directory: Path,
) -> None:
    command = [
        str(blender),
        "--background",
        "--factory-startup",
        "--python",
        str(driver),
        "--",
        "--operation",
        "curate",
        "--asset-id",
        asset_id,
        "--source",
        str(source),
        "--output",
        str(output),
        "--report",
        str(report),
    ]
    try:
        completed = subprocess.run(
            command,
            cwd=working_directory,
            env=_child_environment(),
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            timeout=BLENDER_TIMEOUT_SECONDS,
            check=False,
        )
    except (OSError, subprocess.TimeoutExpired) as exc:
        raise CurationError("Blender curation process could not complete") from exc
    if completed.returncode != 0:
        diagnostic = completed.stdout[-2000:].decode("utf-8", errors="replace")
        diagnostic = " ".join(diagnostic.split())
        raise CurationError(
            "Blender curation failed"
            + (f": {diagnostic}" if diagnostic else "")
        )


def _resolve_input_member(input_root: Path, member: str) -> Path:
    path = input_root / member
    _regular_single_link(path, "input member")
    resolved = path.resolve(strict=True)
    if resolved.parent != input_root:
        raise CurationError("input member escapes the input directory")
    return path


def _parse_arguments(arguments: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Curate one frozen Cat Metro source GLB transactionally."
    )
    parser.add_argument("--input-dir", type=Path, required=True)
    parser.add_argument("--backup-dir", type=Path, required=True)
    parser.add_argument("--blender", type=Path, required=True)
    parser.add_argument(
        "--asset-id", choices=tuple(ALLOWED_SOURCE_SHA256), required=True
    )
    return parser.parse_args(arguments)


def _safe_remove_stage(stage_directory: Path, input_root: Path) -> None:
    try:
        resolved_parent = stage_directory.parent.resolve(strict=True)
    except OSError:
        return
    if (
        resolved_parent != input_root
        or not stage_directory.name.startswith(".glb-curation-stage-")
        or stage_directory.is_symlink()
    ):
        raise CurationError("refusing unsafe curation-stage cleanup")
    if stage_directory.exists():
        shutil.rmtree(stage_directory)


def curate(arguments: argparse.Namespace) -> dict[str, object]:
    input_root = Path(os.path.abspath(arguments.input_dir)).resolve(strict=True)
    if not input_root.is_dir() or input_root.is_symlink():
        raise CurationError("input directory must be a real directory")
    backup_dir = Path(os.path.abspath(arguments.backup_dir))
    if backup_dir.exists() or backup_dir.is_symlink():
        raise CurationError("backup directory already exists")
    backup_parent = backup_dir.parent.resolve(strict=True)
    if not backup_parent.is_dir() or backup_parent.is_symlink():
        raise CurationError("backup parent must be a real directory")
    backup_dir = backup_parent / backup_dir.name
    blender = Path(os.path.abspath(arguments.blender)).resolve(strict=True)
    _regular_single_link(blender, "Blender executable")
    if not os.access(blender, os.X_OK):
        raise CurationError("Blender executable is not executable")
    driver = _SCRIPT_DIR / "blender_curate.py"
    _regular_single_link(driver, "Blender curation driver")

    asset_id = arguments.asset_id
    filename = ASSET_FILENAMES[asset_id]
    source = _resolve_input_member(input_root, filename)
    source_sidecar = _resolve_input_member(input_root, f"{filename}.json")
    source_sha = _sha256_file(source, MAX_SOURCE_BYTES, "source GLB")
    if source_sha != ALLOWED_SOURCE_SHA256[asset_id]:
        raise CurationError("source is not at the frozen pre-curation SHA-256")
    source_record = _load_source_record(source_sidecar)
    _validate_source_record(
        asset_id,
        source_record,
        source_sha,
        require_precuration_anchor=True,
    )
    try:
        source_metrics = inspect_glb(source)
    except (GlbError, OSError, ValueError) as exc:
        raise CurationError("source GLB inspection failed") from exc

    stage_directory = Path(
        tempfile.mkdtemp(prefix=".glb-curation-stage-", dir=input_root)
    )
    try:
        os.chmod(stage_directory, 0o700)
        staged_glb = stage_directory / filename
        staged_report = stage_directory / f"{filename}.curation.json"
        staged_sidecar = stage_directory / f"{filename}.json"
        _run_blender(
            blender,
            driver,
            asset_id,
            source,
            staged_glb,
            staged_report,
            stage_directory,
        )
        _regular_single_link(staged_report, "Blender curation report")
        if staged_report.stat().st_size > MAX_METADATA_BYTES:
            raise CurationError("Blender curation report exceeds its byte limit")
        try:
            report = json.loads(staged_report.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, UnicodeError) as exc:
            raise CurationError("Blender curation report is invalid") from exc
        if not isinstance(report, dict):
            raise CurationError("Blender curation report root must be an object")
        try:
            candidate_metrics = inspect_glb(staged_glb)
        except (GlbError, OSError, ValueError) as exc:
            raise CurationError("curated source GLB inspection failed") from exc
        _candidate_structure(asset_id, source_metrics, candidate_metrics, report)
        curated_sha = _sha256_file(
            staged_glb, MAX_SOURCE_BYTES, "curated source GLB"
        )
        curated_record = build_curated_source_record(
            asset_id, source_record, curated_sha
        )
        _write_private_json(staged_sidecar, curated_record)
        _validate_source_record(
            asset_id,
            curated_record,
            curated_sha,
            require_precuration_anchor=False,
        )
        publish_pair(
            staged_glb=staged_glb,
            staged_sidecar=staged_sidecar,
            final_glb=source,
            final_sidecar=source_sidecar,
            backup_dir=backup_dir,
        )
        return {
            "asset_id": asset_id,
            "source_sha256_before": source_sha,
            "source_sha256_after": curated_sha,
            "backup_dir": str(backup_dir),
            "triangles_before": source_metrics["triangles"],
            "triangles_after": candidate_metrics["triangles"],
            "report": report,
        }
    finally:
        _safe_remove_stage(stage_directory, input_root)


def main(arguments: list[str]) -> int:
    try:
        args = _parse_arguments(arguments)
        result = curate(args)
    except (CurationError, OSError, ValueError) as exc:
        message = str(exc) or "curation failed"
        if len(message) > 500:
            message = message[:497] + "..."
        print(f"curate-assets: {message}", file=sys.stderr)
        return 1
    print(json.dumps(result, allow_nan=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
