#!/usr/bin/env python3
"""Stage, validate, back up, and publish one frozen Cat Metro source curation."""

from __future__ import annotations

import sys


sys.dont_write_bytecode = True

import argparse
import fcntl
import hashlib
import json
import os
import re
import shutil
import stat
import subprocess
import tempfile
import uuid
from collections import Counter
from collections.abc import Callable, Iterator, Mapping
from contextlib import contextmanager
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
ALLOWED_SOURCE_SIDECAR_SHA256 = {
    LOAF_ID: "ce8ea067634f88ee9fc967ea5a0dbc58df890477d3e1dc1905cc3f77a92dcec4",
    WAVE_ID: "e65414b151fa1dd868e9086c0e274ac61743aef8f8f26bc7bcaa6f49f99c8936",
}
WAVE_CORRECTION_SOURCE_SHA256 = (
    "f91ccb7ff9b527ecef168d4285488ff647023fb70875f5403c31db8e2349d99d"
)
WAVE_CORRECTION_SOURCE_SIDECAR_SHA256 = (
    "bb787a4073833edfd54af3e401cfa00e73b5279592ba2d146b015d3f1ffe90e4"
)
ALLOWED_PRECURATION_PAIRS = {
    LOAF_ID: (
        (ALLOWED_SOURCE_SHA256[LOAF_ID], ALLOWED_SOURCE_SIDECAR_SHA256[LOAF_ID]),
    ),
    WAVE_ID: (
        (ALLOWED_SOURCE_SHA256[WAVE_ID], ALLOWED_SOURCE_SIDECAR_SHA256[WAVE_ID]),
        (WAVE_CORRECTION_SOURCE_SHA256, WAVE_CORRECTION_SOURCE_SIDECAR_SHA256),
    ),
}
ASSET_FILENAMES = {
    LOAF_ID: "cat-blue-siamese-loaf.glb",
    WAVE_ID: "cat-yellow-longhair-wave.glb",
}
CURATION_NOTES = {
    LOAF_ID: "Cat Metro GLB-CURATION: removed ruled min-Y display plinth",
    WAVE_ID: "Cat Metro GLB-CURATION: kept largest cat component; removed detached components",
}
SUPERSEDED_WAVE_CURATION_NOTE = (
    "Cat Metro GLB-CURATION: removed ruled min-Y foot fragment"
)
EXPECTED_CURATED_TRIANGLES = {
    LOAF_ID: 773_061,
    WAVE_ID: 1_383_894,
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
TRANSACTION_SCHEMA_VERSION = 1
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


@contextmanager
def source_root_lock(input_root: Path) -> Iterator[None]:
    """Hold an advisory exclusive lock on the source directory inode."""

    root = Path(input_root)
    try:
        status = root.lstat()
    except FileNotFoundError as exc:
        raise CurationError("source input directory is missing") from exc
    if not stat.S_ISDIR(status.st_mode) or root.is_symlink():
        raise CurationError("source input directory must be a real directory")
    descriptor = os.open(root, os.O_RDONLY)
    try:
        try:
            fcntl.flock(descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError as exc:
            raise CurationError("source input directory is locked by another curation") from exc
        yield
    finally:
        try:
            fcntl.flock(descriptor, fcntl.LOCK_UN)
        finally:
            os.close(descriptor)


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
        and source_record["sha256"]
        not in {pair[0] for pair in ALLOWED_PRECURATION_PAIRS[asset_id]}
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
    if original_sha not in {
        pair[0] for pair in ALLOWED_PRECURATION_PAIRS[asset_id]
    }:
        raise CurationError("source record is not at the frozen pre-curation anchor")
    note = source_record.get("note")
    if not isinstance(note, str):
        raise CurationError("source note must be a string")
    if asset_id == WAVE_ID and original_sha == WAVE_CORRECTION_SOURCE_SHA256:
        suffix = f"; {SUPERSEDED_WAVE_CURATION_NOTE}"
        if not note.endswith(suffix):
            raise CurationError("wave correction source note does not match its anchor")
        note = note[: -len(suffix)]
    elif "Cat Metro GLB-CURATION:" in note:
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
    fsync_directory_fn: Callable[[Path], None] = _fsync_directory,
) -> None:
    restore = final.parent / f".{final.name}.rollback-{uuid.uuid4().hex}"
    try:
        _copy_new(backup, restore)
        replace_fn(restore, final)
        fsync_directory_fn(final.parent)
    finally:
        if restore.exists() and not restore.is_symlink():
            restore.unlink()


def _transaction_next_path(journal_path: Path) -> Path:
    return journal_path.with_name(f".{journal_path.name}.next")


def _write_transaction_journal(
    journal_path: Path,
    record: Mapping[str, object],
    *,
    replace_existing: bool,
    fsync_directory_fn: Callable[[Path], None] = _fsync_directory,
) -> None:
    if journal_path.parent.is_symlink() or not journal_path.parent.is_dir():
        raise CurationError("transaction journal parent must be a real directory")
    if replace_existing:
        _regular_single_link(journal_path, "transaction journal")
        next_path = _transaction_next_path(journal_path)
        if next_path.exists() or next_path.is_symlink():
            raise CurationError("transaction journal update residue exists")
        try:
            _write_private_json(next_path, record)
            os.replace(next_path, journal_path)
            fsync_directory_fn(journal_path.parent)
        finally:
            if next_path.exists() and not next_path.is_symlink():
                next_path.unlink()
        return
    if journal_path.exists() or journal_path.is_symlink():
        raise CurationError("transaction journal already exists")
    _write_private_json(journal_path, record)
    fsync_directory_fn(journal_path.parent)


def _load_transaction_journal(journal_path: Path) -> dict[str, object]:
    _regular_single_link(journal_path, "transaction journal")
    if journal_path.stat().st_size > MAX_METADATA_BYTES:
        raise CurationError("transaction journal exceeds its byte limit")
    try:
        value = json.loads(journal_path.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, UnicodeError) as exc:
        raise CurationError("transaction journal is invalid JSON") from exc
    if not isinstance(value, dict):
        raise CurationError("transaction journal root must be an object")
    required = {
        "schema_version",
        "state",
        "final_directory",
        "final_glb",
        "final_sidecar",
        "backup_directory",
        "original_glb_sha256",
        "original_sidecar_sha256",
        "candidate_glb_sha256",
        "candidate_sidecar_sha256",
    }
    if set(value) != required:
        raise CurationError("transaction journal fields do not match schema")
    if value["schema_version"] != TRANSACTION_SCHEMA_VERSION:
        raise CurationError("transaction journal schema is unsupported")
    if value["state"] not in {"prepared", "committed"}:
        raise CurationError("transaction journal state is invalid")
    for field in (
        "final_directory",
        "final_glb",
        "final_sidecar",
        "backup_directory",
    ):
        if not isinstance(value[field], str) or not value[field]:
            raise CurationError(f"transaction journal {field} is invalid")
    for field in (
        "original_glb_sha256",
        "original_sidecar_sha256",
        "candidate_glb_sha256",
        "candidate_sidecar_sha256",
    ):
        if not isinstance(value[field], str) or _LOWER_SHA256.fullmatch(value[field]) is None:
            raise CurationError(f"transaction journal {field} is invalid")
    return value


def _remove_transaction_files(
    journal_path: Path,
    fsync_directory_fn: Callable[[Path], None] = _fsync_directory,
) -> None:
    for path in (journal_path, _transaction_next_path(journal_path)):
        if path.is_symlink():
            raise CurationError("transaction cleanup refuses a symbolic link")
        if path.exists():
            _regular_single_link(path, "transaction residue")
            path.unlink()
    fsync_directory_fn(journal_path.parent)


def _remove_failed_prepublication_files(
    *,
    journal_path: Path,
    backup_dir: Path,
    backup_glb: Path,
    backup_sidecar: Path,
    fsync_directory_fn: Callable[[Path], None] = _fsync_directory,
) -> None:
    """Remove the not-yet-authoritative journal and backup after setup fails."""

    for path in (journal_path, _transaction_next_path(journal_path)):
        if path.is_symlink():
            raise CurationError("pre-publication cleanup refuses a symbolic link")
        if path.exists():
            _regular_single_link(path, "pre-publication transaction residue")
            path.unlink()
    for path in (backup_glb, backup_sidecar):
        if path.is_symlink():
            raise CurationError("pre-publication cleanup refuses a symbolic link")
        if path.exists():
            _regular_single_link(path, "pre-publication backup residue")
            path.unlink()
    if backup_dir.is_symlink():
        raise CurationError("pre-publication cleanup refuses a symbolic link")
    if backup_dir.exists():
        status = backup_dir.lstat()
        if not stat.S_ISDIR(status.st_mode):
            raise CurationError("pre-publication backup residue must be a directory")
        backup_dir.rmdir()
    synced_directories: set[Path] = set()
    for directory in (journal_path.parent, backup_dir.parent):
        if directory in synced_directories:
            continue
        fsync_directory_fn(directory)
        synced_directories.add(directory)


def recover_interrupted_pair(
    *,
    journal_path: Path,
    final_glb: Path,
    final_sidecar: Path,
) -> bool:
    """Normalize a durable prepared/committed transaction after process death."""

    if not journal_path.exists() and not journal_path.is_symlink():
        next_path = _transaction_next_path(journal_path)
        if next_path.exists() or next_path.is_symlink():
            raise CurationError("orphan transaction journal update exists")
        return False
    record = _load_transaction_journal(journal_path)
    final_directory = final_glb.parent.resolve(strict=True)
    if final_sidecar.parent.resolve(strict=True) != final_directory:
        raise CurationError("recovery final pair must share one directory")
    if record["final_directory"] != str(final_directory):
        raise CurationError("transaction journal final directory mismatch")
    if record["final_glb"] != final_glb.name or record["final_sidecar"] != final_sidecar.name:
        raise CurationError("transaction journal final filename mismatch")
    backup_directory = Path(record["backup_directory"])
    if not backup_directory.is_absolute():
        raise CurationError("transaction backup path must be absolute")
    backup_status = backup_directory.lstat()
    if not stat.S_ISDIR(backup_status.st_mode) or backup_directory.is_symlink():
        raise CurationError("transaction backup directory must be real")
    resolved_backup = backup_directory.resolve(strict=True)
    try:
        resolved_backup.relative_to(final_directory)
    except ValueError as exc:
        raise CurationError("transaction backup escapes the source directory") from exc
    backup_glb = resolved_backup / final_glb.name
    backup_sidecar = resolved_backup / final_sidecar.name
    if _sha256_file(backup_glb, MAX_SOURCE_BYTES, "transaction backup GLB") != record["original_glb_sha256"]:
        raise CurationError("transaction backup GLB hash mismatch")
    if _sha256_file(backup_sidecar, MAX_METADATA_BYTES, "transaction backup sidecar") != record["original_sidecar_sha256"]:
        raise CurationError("transaction backup sidecar hash mismatch")

    if record["state"] == "prepared":
        _restore_from_backup(backup_glb, final_glb, os.replace)
        _restore_from_backup(backup_sidecar, final_sidecar, os.replace)
        if _sha256_file(final_glb, MAX_SOURCE_BYTES, "recovered source GLB") != record["original_glb_sha256"]:
            raise CurationError("recovered source GLB hash mismatch")
        if _sha256_file(final_sidecar, MAX_METADATA_BYTES, "recovered source sidecar") != record["original_sidecar_sha256"]:
            raise CurationError("recovered source sidecar hash mismatch")
    else:
        if _sha256_file(final_glb, MAX_SOURCE_BYTES, "committed source GLB") != record["candidate_glb_sha256"]:
            raise CurationError("committed source GLB hash mismatch")
        if _sha256_file(final_sidecar, MAX_METADATA_BYTES, "committed source sidecar") != record["candidate_sidecar_sha256"]:
            raise CurationError("committed source sidecar hash mismatch")
    _remove_transaction_files(journal_path)
    return True


def publish_pair(
    *,
    staged_glb: Path,
    staged_sidecar: Path,
    final_glb: Path,
    final_sidecar: Path,
    backup_dir: Path,
    journal_path: Path | None = None,
    replace_fn: Callable[[os.PathLike[str], os.PathLike[str]], object] = os.replace,
    rollback_replace_fn: Callable[[os.PathLike[str], os.PathLike[str]], object] = os.replace,
    fsync_directory_fn: Callable[[Path], None] = _fsync_directory,
    pair_validator: Callable[[Path, Path], None] | None = None,
    expected_final_glb_sha: str | None = None,
    expected_final_sidecar_sha: str | None = None,
    expected_staged_glb_sha: str | None = None,
    expected_staged_sidecar_sha: str | None = None,
) -> None:
    """Back up and pair-promote a source pair with rollback and crash recovery."""

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
    if journal_path is None:
        journal_path = final_glb.parent / f".{final_glb.name}.transaction.json"
    if journal_path.parent != final_glb.parent or journal_path.is_symlink():
        raise CurationError("transaction journal must stay beside the final pair")
    if journal_path.exists() or _transaction_next_path(journal_path).exists():
        raise CurationError("transaction journal already exists")
    if backup_dir.exists() or backup_dir.is_symlink():
        raise CurationError("backup directory already exists")
    if not backup_dir.parent.is_dir() or backup_dir.parent.is_symlink():
        raise CurationError("backup parent must be an existing real directory")
    try:
        backup_dir.parent.resolve(strict=True).relative_to(
            final_glb.parent.resolve(strict=True)
        )
    except ValueError as exc:
        raise CurationError("backup directory must stay under the source directory") from exc

    backup_glb = backup_dir / final_glb.name
    backup_sidecar = backup_dir / final_sidecar.name
    original_glb_sha = _sha256_file(final_glb, MAX_SOURCE_BYTES, "final GLB")
    original_sidecar_sha = _sha256_file(
        final_sidecar, MAX_METADATA_BYTES, "final sidecar"
    )
    candidate_glb_sha = _sha256_file(staged_glb, MAX_SOURCE_BYTES, "staged GLB")
    candidate_sidecar_sha = _sha256_file(
        staged_sidecar, MAX_METADATA_BYTES, "staged sidecar"
    )
    expected_hashes = (
        (expected_final_glb_sha, original_glb_sha, "final GLB"),
        (expected_final_sidecar_sha, original_sidecar_sha, "final sidecar"),
        (expected_staged_glb_sha, candidate_glb_sha, "staged GLB"),
        (expected_staged_sidecar_sha, candidate_sidecar_sha, "staged sidecar"),
    )
    for expected, actual, label in expected_hashes:
        if expected is not None and expected != actual:
            raise CurationError(f"{label} changed before publication")
    backup_created = False
    try:
        backup_dir.mkdir(mode=0o700)
        backup_created = True
        _copy_new(final_glb, backup_glb)
        _copy_new(final_sidecar, backup_sidecar)
        fsync_directory_fn(backup_dir)
        fsync_directory_fn(backup_dir.parent)
        if (
            _sha256_file(backup_glb, MAX_SOURCE_BYTES, "source backup GLB")
            != original_glb_sha
        ):
            raise CurationError("source backup GLB hash mismatch")
        if (
            _sha256_file(
                backup_sidecar,
                MAX_METADATA_BYTES,
                "source backup sidecar",
            )
            != original_sidecar_sha
        ):
            raise CurationError("source backup sidecar hash mismatch")
    except BaseException as exc:
        if backup_created:
            for member in (backup_glb, backup_sidecar):
                if member.exists() and not member.is_symlink():
                    member.unlink()
            try:
                backup_dir.rmdir()
            except OSError:
                pass
        if isinstance(exc, CurationError):
            raise
        if isinstance(exc, OSError):
            raise CurationError("could not create complete source backup") from exc
        raise

    transaction = {
        "schema_version": TRANSACTION_SCHEMA_VERSION,
        "state": "prepared",
        "final_directory": str(final_glb.parent.resolve(strict=True)),
        "final_glb": final_glb.name,
        "final_sidecar": final_sidecar.name,
        "backup_directory": str(backup_dir.resolve(strict=True)),
        "original_glb_sha256": original_glb_sha,
        "original_sidecar_sha256": original_sidecar_sha,
        "candidate_glb_sha256": candidate_glb_sha,
        "candidate_sidecar_sha256": candidate_sidecar_sha,
    }
    try:
        _write_transaction_journal(
            journal_path,
            transaction,
            replace_existing=False,
            fsync_directory_fn=fsync_directory_fn,
        )
    except BaseException as exc:
        try:
            _remove_failed_prepublication_files(
                journal_path=journal_path,
                backup_dir=backup_dir,
                backup_glb=backup_glb,
                backup_sidecar=backup_sidecar,
                fsync_directory_fn=fsync_directory_fn,
            )
        except BaseException as cleanup_exc:
            raise CurationError(
                "transaction journal creation failed and cleanup also failed"
            ) from cleanup_exc
        if isinstance(exc, (OSError, CurationError)):
            raise CurationError(
                "could not create durable transaction journal"
            ) from exc
        raise

    try:
        replace_fn(staged_glb, final_glb)
        fsync_directory_fn(final_glb.parent)
        replace_fn(staged_sidecar, final_sidecar)
        fsync_directory_fn(final_glb.parent)
        if _sha256_file(final_glb, MAX_SOURCE_BYTES, "published GLB") != candidate_glb_sha:
            raise CurationError("published GLB hash mismatch")
        if _sha256_file(final_sidecar, MAX_METADATA_BYTES, "published sidecar") != candidate_sidecar_sha:
            raise CurationError("published sidecar hash mismatch")
        if pair_validator is not None:
            pair_validator(final_glb, final_sidecar)
        transaction["state"] = "committed"
        _write_transaction_journal(
            journal_path,
            transaction,
            replace_existing=True,
            fsync_directory_fn=fsync_directory_fn,
        )
        _remove_transaction_files(journal_path, fsync_directory_fn)
    except BaseException as exc:
        try:
            _restore_from_backup(
                backup_glb,
                final_glb,
                rollback_replace_fn,
                fsync_directory_fn,
            )
            _restore_from_backup(
                backup_sidecar,
                final_sidecar,
                rollback_replace_fn,
                fsync_directory_fn,
            )
            if _sha256_file(final_glb, MAX_SOURCE_BYTES, "rolled-back GLB") != original_glb_sha:
                raise CurationError("rolled-back GLB hash mismatch")
            if _sha256_file(final_sidecar, MAX_METADATA_BYTES, "rolled-back sidecar") != original_sidecar_sha:
                raise CurationError("rolled-back sidecar hash mismatch")
            _remove_transaction_files(journal_path, fsync_directory_fn)
        except BaseException as rollback_exc:
            raise CurationError(
                "promotion failed and source-pair rollback also failed"
            ) from rollback_exc
        if isinstance(exc, (OSError, CurationError)):
            raise CurationError("promotion failed; original source pair restored") from exc
        raise


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
    source_payloads = source_metrics.get("image_payload_sha256")
    candidate_payloads = candidate_metrics.get("image_payload_sha256")
    if (
        not isinstance(source_payloads, list)
        or not isinstance(candidate_payloads, list)
        or not all(isinstance(value, str) for value in source_payloads)
        or not all(isinstance(value, str) for value in candidate_payloads)
    ):
        raise CurationError("curated source image payload custody is malformed")
    if Counter(source_payloads) != Counter(candidate_payloads):
        raise CurationError("curated source image payload multiset changed")
    source_bindings = source_metrics.get("material_texture_bindings")
    candidate_bindings = candidate_metrics.get("material_texture_bindings")
    if not isinstance(source_bindings, list) or not isinstance(candidate_bindings, list):
        raise CurationError("curated source material binding custody is malformed")
    if source_bindings != candidate_bindings:
        raise CurationError("curated source material texture bindings changed")
    if candidate_metrics.get("degenerate_triangles") != 0:
        raise CurationError("curated source contains degenerate triangles")
    if candidate_metrics.get("referenced_vertices") != candidate_metrics.get("vertices"):
        raise CurationError("curated source contains unreferenced vertices")
    if candidate_metrics.get("unique_triangles") != candidate_metrics.get("triangles"):
        raise CurationError("curated source contains duplicate triangles")


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


def _inspect_precuration_pair(
    asset_id: str,
    source: Path,
    source_sidecar: Path,
) -> tuple[str, str, dict[str, object], dict[str, object]]:
    source_sha = _sha256_file(source, MAX_SOURCE_BYTES, "source GLB")
    source_sidecar_sha = _sha256_file(
        source_sidecar,
        MAX_METADATA_BYTES,
        "source sidecar",
    )
    if (source_sha, source_sidecar_sha) not in ALLOWED_PRECURATION_PAIRS[asset_id]:
        raise CurationError("source pair is not at an allowed pre-curation SHA-256")
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
    if source_metrics.get("sha256") != source_sha:
        raise CurationError("source GLB inspection hash mismatch")
    return source_sha, source_sidecar_sha, source_record, source_metrics


def _validate_curated_pair(
    asset_id: str,
    expected_glb_sha: str,
    final_glb: Path,
    final_sidecar: Path,
) -> None:
    actual_glb_sha = _sha256_file(final_glb, MAX_SOURCE_BYTES, "published source GLB")
    if actual_glb_sha != expected_glb_sha:
        raise CurationError("published source GLB differs from staged candidate")
    record = _load_source_record(final_sidecar)
    _validate_source_record(
        asset_id,
        record,
        actual_glb_sha,
        require_precuration_anchor=False,
    )
    expected_note = CURATION_NOTES[asset_id]
    if expected_note not in record["note"]:
        raise CurationError("published source sidecar omits the curation note")


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


def _remove_orphan_stages(input_root: Path) -> None:
    for member in input_root.iterdir():
        if not member.name.startswith(".glb-curation-stage-"):
            continue
        if member.is_symlink() or not member.is_dir():
            raise CurationError("curation-stage residue has an unsafe type")
        _safe_remove_stage(member, input_root)


def curate(arguments: argparse.Namespace) -> dict[str, object]:
    input_root = Path(os.path.abspath(arguments.input_dir)).resolve(strict=True)
    if not input_root.is_dir() or input_root.is_symlink():
        raise CurationError("input directory must be a real directory")
    backup_dir = Path(os.path.abspath(arguments.backup_dir))
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
    source = input_root / filename
    source_sidecar = input_root / f"{filename}.json"
    journal_path = input_root / f".glb-curation-{asset_id}.transaction.json"

    with source_root_lock(input_root):
        for recovery_asset_id, recovery_filename in ASSET_FILENAMES.items():
            recover_interrupted_pair(
                journal_path=input_root
                / f".glb-curation-{recovery_asset_id}.transaction.json",
                final_glb=input_root / recovery_filename,
                final_sidecar=input_root / f"{recovery_filename}.json",
            )
        _remove_orphan_stages(input_root)
        if backup_dir.exists() or backup_dir.is_symlink():
            raise CurationError("backup directory already exists")
        source = _resolve_input_member(input_root, filename)
        source_sidecar = _resolve_input_member(input_root, f"{filename}.json")
        (
            source_sha,
            source_sidecar_sha,
            source_record,
            source_metrics,
        ) = _inspect_precuration_pair(asset_id, source, source_sidecar)

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
            if candidate_metrics.get("sha256") != curated_sha:
                raise CurationError("curated source inspection hash mismatch")
            curated_record = build_curated_source_record(
                asset_id, source_record, curated_sha
            )
            _write_private_json(staged_sidecar, curated_record)
            curated_sidecar_sha = _sha256_file(
                staged_sidecar,
                MAX_METADATA_BYTES,
                "curated source sidecar",
            )
            _validate_source_record(
                asset_id,
                curated_record,
                curated_sha,
                require_precuration_anchor=False,
            )

            final_source_sha, final_sidecar_sha, _, _ = _inspect_precuration_pair(
                asset_id,
                source,
                source_sidecar,
            )
            if final_source_sha != source_sha or final_sidecar_sha != source_sidecar_sha:
                raise CurationError("source pair changed during Blender curation")
            publish_pair(
                staged_glb=staged_glb,
                staged_sidecar=staged_sidecar,
                final_glb=source,
                final_sidecar=source_sidecar,
                backup_dir=backup_dir,
                journal_path=journal_path,
                pair_validator=lambda glb, sidecar: _validate_curated_pair(
                    asset_id,
                    curated_sha,
                    glb,
                    sidecar,
                ),
                expected_final_glb_sha=source_sha,
                expected_final_sidecar_sha=source_sidecar_sha,
                expected_staged_glb_sha=curated_sha,
                expected_staged_sidecar_sha=curated_sidecar_sha,
            )
            return {
                "asset_id": asset_id,
                "source_sha256_before": source_sha,
                "source_sidecar_sha256_before": source_sidecar_sha,
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
    except KeyboardInterrupt:
        print("curate-assets: interrupted; source pair normalized", file=sys.stderr)
        return 130
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
