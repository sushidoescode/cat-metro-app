#!/usr/bin/env python3
"""Validate, decimate, and atomically publish generated GLB derivatives."""

from __future__ import annotations

import sys


sys.dont_write_bytecode = True

import argparse
import contextlib
import fcntl
import hashlib
import json
import os
import re
import selectors
import shutil
import signal
import stat
import struct
import subprocess
import tempfile
import threading
import time
import unicodedata
import uuid
from collections.abc import Mapping
from datetime import datetime, timezone
from pathlib import Path


_SCRIPT_DIR = Path(__file__).resolve().parent
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))
import glb_metrics as _glb_metrics
from glb_metrics import GlbError, compare_preservation


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
MAX_METADATA_BYTES = 1_048_576
MAX_MANIFEST_ASSETS = 64
MAX_SOURCE_GLB_BYTES = 128 * 1024 * 1024
MAX_DERIVATIVE_GLB_BYTES = 64 * 1024 * 1024
MAX_PROVENANCE_BYTES = 2 * MAX_METADATA_BYTES
MAX_CHILD_STREAM_BYTES = MAX_METADATA_BYTES
MAX_DIAGNOSTIC_BYTES = 512
MAX_TRANSACTION_SAFE_FILENAME_BYTES = 208
_CHILD_ENVIRONMENT_PASSTHROUGH = (
    "PATH",
    "LANG",
    "LC_ALL",
    "LC_CTYPE",
    "__CF_USER_TEXT_ENCODING",
)
_FAKE_BLENDER_ENVIRONMENT = frozenset(
    {
        "FAKE_BLENDER_AUDIT",
        "FAKE_BLENDER_BANNER_BUILD_HASH",
        "FAKE_BLENDER_BANNER_VERSION",
        "FAKE_BLENDER_BUILD_HASH",
        "FAKE_BLENDER_ENV_LOG",
        "FAKE_BLENDER_LOG",
        "FAKE_BLENDER_MODE",
        "FAKE_BLENDER_OUTPUT_EXACT_SIZE",
        "FAKE_BLENDER_OUTPUT_EXTENSION",
        "FAKE_BLENDER_OUTPUT_SIZE",
        "FAKE_BLENDER_OUTPUT_URI",
        "FAKE_BLENDER_SWAP_PATH",
        "FAKE_BLENDER_SWAP_PAYLOAD_PATH",
        "FAKE_BLENDER_VERSION",
        "FAKE_BLENDER_VERSION_BANNER",
    }
)
_FORBIDDEN_PROVENANCE = re.compile(
    r"api[_-]?key|token|secret|authorization|credential|bearer|https?://",
    re.IGNORECASE,
)
_HTTP_URI = re.compile(r"https?://[^\s]+", re.IGNORECASE)
_CREDENTIAL_SHAPE = re.compile(
    r"api[_ -]?key|token|secret|authorization|credential|bearer",
    re.IGNORECASE,
)
_MISSING_MATERIAL_UV = re.compile(
    r"meshes\[\d+\]\.primitives\[\d+\] material references missing "
    r"TEXCOORD_\d+"
)
_LOWER_SHA256 = re.compile(r"[0-9a-f]{64}")
_UTC_TIMESTAMP = re.compile(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z")


class DecimationError(RuntimeError):
    """Expected fail-closed pipeline error suitable for a concise CLI report."""


def _identity(status: os.stat_result) -> tuple[int, int]:
    return status.st_dev, status.st_ino


def _custody_error(label: str) -> DecimationError:
    return DecimationError(
        f"{label} file custody requires a regular single-link file "
        "(no symlink or hard-link alias)"
    )


def _checked_lstat(
    path: Path,
    label: str,
    maximum_bytes: int,
    *,
    allow_missing: bool = False,
) -> os.stat_result | None:
    try:
        status = os.lstat(path)
    except FileNotFoundError:
        if allow_missing:
            return None
        raise DecimationError(f"missing {label}") from None
    except OSError as exc:
        raise DecimationError(f"{label} file custody cannot be read") from exc
    if not stat.S_ISREG(status.st_mode) or status.st_nlink != 1:
        raise _custody_error(label)
    if status.st_size > maximum_bytes:
        raise DecimationError(f"{label} exceeds {maximum_bytes}-byte size limit")
    return status


def _read_verified_file(
    path: Path,
    label: str,
    maximum_bytes: int,
    *,
    expected_identity: tuple[int, int] | None = None,
) -> tuple[bytes, tuple[int, int], os.stat_result]:
    """Read one bounded regular file through a no-follow descriptor."""
    path = Path(path)
    before = _checked_lstat(path, label, maximum_bytes)
    if before is None:
        raise AssertionError("required file unexpectedly missing")
    if expected_identity is not None and _identity(before) != expected_identity:
        raise DecimationError(f"{label} filesystem identity changed")

    nofollow = getattr(os, "O_NOFOLLOW", 0)
    nonblock = getattr(os, "O_NONBLOCK", 0)
    if not nofollow or not nonblock:
        raise DecimationError("platform lacks required no-follow file custody")
    descriptor = -1
    try:
        descriptor = os.open(
            path,
            os.O_RDONLY
            | nofollow
            | nonblock
            | getattr(os, "O_CLOEXEC", 0),
        )
        opened = os.fstat(descriptor)
        if (
            not stat.S_ISREG(opened.st_mode)
            or opened.st_nlink != 1
            or _identity(opened) != _identity(before)
        ):
            raise _custody_error(label)
        if expected_identity is not None and _identity(opened) != expected_identity:
            raise DecimationError(f"{label} filesystem identity changed")
        if opened.st_size > maximum_bytes:
            raise DecimationError(f"{label} exceeds {maximum_bytes}-byte size limit")

        blocks: list[bytes] = []
        remaining = opened.st_size
        while remaining:
            block = os.read(descriptor, min(1024 * 1024, remaining))
            if not block:
                break
            blocks.append(block)
            remaining -= len(block)
        payload = b"".join(blocks)
        after = os.fstat(descriptor)
        if (
            not stat.S_ISREG(after.st_mode)
            or after.st_nlink != 1
            or _identity(after) != _identity(opened)
            or after.st_size != opened.st_size
            or len(payload) != opened.st_size
            or after.st_mtime_ns != opened.st_mtime_ns
            or after.st_ctime_ns != opened.st_ctime_ns
        ):
            raise DecimationError(f"{label} changed during bounded read")
    except DecimationError:
        raise
    except OSError as exc:
        raise _custody_error(label) from exc
    finally:
        if descriptor >= 0:
            os.close(descriptor)

    current = _checked_lstat(path, label, maximum_bytes)
    if current is None or (
        _identity(current) != _identity(before)
        or current.st_size != before.st_size
        or current.st_mtime_ns != before.st_mtime_ns
        or current.st_ctime_ns != before.st_ctime_ns
    ):
        raise DecimationError(f"{label} changed during bounded read")
    return payload, _identity(before), current


def _write_private_file(path: Path, payload: bytes, mode: int = 0o400) -> None:
    descriptor = -1
    try:
        descriptor = os.open(
            path,
            os.O_WRONLY
            | os.O_CREAT
            | os.O_EXCL
            | getattr(os, "O_NOFOLLOW", 0)
            | getattr(os, "O_CLOEXEC", 0),
            0o600,
        )
        offset = 0
        while offset < len(payload):
            written = os.write(descriptor, payload[offset : offset + 1024 * 1024])
            if written <= 0:
                raise OSError("short private-file write")
            offset += written
        os.fsync(descriptor)
        os.fchmod(descriptor, mode)
        status = os.fstat(descriptor)
        if (
            not stat.S_ISREG(status.st_mode)
            or status.st_nlink != 1
            or status.st_size != len(payload)
        ):
            raise DecimationError("private snapshot file custody verification failed")
    finally:
        if descriptor >= 0:
            os.close(descriptor)


def _verified_hash(
    path: Path,
    label: str,
    maximum_bytes: int,
    *,
    expected_identity: tuple[int, int] | None = None,
) -> tuple[str, tuple[int, int], bytes, os.stat_result]:
    payload, identity, status = _read_verified_file(
        path,
        label,
        maximum_bytes,
        expected_identity=expected_identity,
    )
    return hashlib.sha256(payload).hexdigest(), identity, payload, status


def _glb_error_message(exc: GlbError) -> str:
    if str(exc) == "truncated GLB":
        return "invalid GLB header"
    return str(exc)


def _reject_glb_json_constant(value: str) -> object:
    raise GlbError(f"invalid non-finite JSON number {value}")


def _reject_glb_duplicate_keys(
    pairs: list[tuple[str, object]],
) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            raise GlbError(f"duplicate JSON object key {key!r}")
        result[key] = value
    return result


def _inspect_verified_glb_payload(path: Path, payload: bytes) -> dict[str, object]:
    """Inspect one already-custodied GLB payload without reopening its path."""
    if len(payload) < 20:
        raise GlbError("truncated GLB")
    magic, version, declared = struct.unpack_from("<4sII", payload, 0)
    if magic != b"glTF" or version != 2 or declared != len(payload):
        raise GlbError("invalid GLB header")

    offset = 12
    document: dict[str, object] | None = None
    chunk_number = 0
    while offset < len(payload):
        if len(payload) - offset < 8:
            raise GlbError("truncated GLB chunk header")
        length, kind = struct.unpack_from("<I4s", payload, offset)
        if length % 4:
            raise GlbError("GLB chunk length is not four-byte aligned")
        offset += 8
        end = offset + length
        if end > len(payload):
            raise GlbError("GLB chunk overruns file")
        if chunk_number == 0 and kind != b"JSON":
            raise GlbError("JSON must be the first GLB chunk")
        if kind == b"JSON":
            if document is not None:
                raise GlbError("duplicate JSON chunk")
            if length > _glb_metrics.MAX_JSON_BYTES:
                raise GlbError(
                    "GLB JSON chunk exceeds limit "
                    f"{_glb_metrics.MAX_JSON_BYTES} bytes"
                )
            try:
                decoded = json.loads(
                    payload[offset:end].rstrip(b" ").decode("utf-8"),
                    parse_constant=_reject_glb_json_constant,
                    object_pairs_hook=_reject_glb_duplicate_keys,
                )
            except GlbError:
                raise
            except (UnicodeDecodeError, json.JSONDecodeError, ValueError) as exc:
                raise GlbError(f"invalid GLB JSON: {exc}") from exc
            except (RecursionError, MemoryError, OverflowError) as exc:
                raise GlbError("GLB JSON exceeds parser resource limits") from exc
            if not isinstance(decoded, dict):
                raise GlbError("GLB JSON root must be an object")
            document = decoded
        offset = end
        chunk_number += 1
    if document is None:
        raise GlbError("missing JSON chunk")

    details, _ = _glb_metrics._inspect_document(document, payload)
    metrics: dict[str, object] = {
        "path": str(path),
        "sha256": hashlib.sha256(payload).hexdigest(),
        "bytes": len(payload),
        **details,
    }
    if tuple(metrics) != _glb_metrics.METRIC_KEYS:
        raise AssertionError("internal metric key mismatch")
    return metrics


def inspect_glb(path: Path) -> dict[str, object]:
    """Inspect one derivative from a single verified 64 MiB-bounded payload."""
    candidate = Path(path)
    payload, _, _ = _read_verified_file(
        candidate,
        "derivative GLB",
        MAX_DERIVATIVE_GLB_BYTES,
    )
    return _inspect_verified_glb_payload(candidate, payload)


def _inspect_source_glb(path: Path) -> dict[str, object]:
    candidate = Path(path)
    payload, _, _ = _read_verified_file(
        candidate,
        "source GLB",
        MAX_SOURCE_GLB_BYTES,
    )
    return _inspect_verified_glb_payload(candidate, payload)


_SHA256_ROLE_CONTEXT = threading.local()


@contextlib.contextmanager
def _sha256_role(label: str, maximum_bytes: int):
    previous = getattr(_SHA256_ROLE_CONTEXT, "value", None)
    current: dict[str, object] = {
        "label": label,
        "maximum_bytes": maximum_bytes,
        "receipt": None,
    }
    _SHA256_ROLE_CONTEXT.value = current
    try:
        yield current
    finally:
        if previous is None:
            try:
                del _SHA256_ROLE_CONTEXT.value
            except AttributeError:
                pass
        else:
            _SHA256_ROLE_CONTEXT.value = previous


def _sha256(path: Path) -> str:
    context = getattr(_SHA256_ROLE_CONTEXT, "value", None)
    label = "file"
    maximum_bytes = MAX_SOURCE_GLB_BYTES
    if isinstance(context, dict):
        context_label = context.get("label")
        context_maximum = context.get("maximum_bytes")
        if isinstance(context_label, str) and isinstance(context_maximum, int):
            label = context_label
            maximum_bytes = context_maximum
    digest, identity, payload, status = _verified_hash(
        path,
        label,
        maximum_bytes,
    )
    if isinstance(context, dict):
        context["receipt"] = (digest, identity, payload, status)
    return digest


def _sha256_receipt(
    path: Path,
    label: str,
    maximum_bytes: int,
) -> tuple[str, tuple[int, int], bytes, os.stat_result]:
    with _sha256_role(label, maximum_bytes) as context:
        digest = _sha256(path)
        receipt = context.get("receipt")
    if (
        not isinstance(receipt, tuple)
        or len(receipt) != 4
        or receipt[0] != digest
    ):
        raise DecimationError(f"{label} hash receipt is unavailable")
    return receipt


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


def _decode_json_bytes(payload: bytes, label: str) -> object:
    try:
        return json.loads(payload)
    except (
        UnicodeDecodeError,
        json.JSONDecodeError,
        RecursionError,
        MemoryError,
    ) as exc:
        raise DecimationError(f"invalid {label}") from exc


def _load_json_bytes(
    path: Path,
    label: str,
    maximum_bytes: int,
) -> tuple[object, bytes]:
    payload, _, _ = _read_verified_file(path, label, maximum_bytes)
    return _decode_json_bytes(payload, label), payload


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
        or not value.isprintable()
        or len(value) <= len(".glb")
        or not value.endswith(".glb")
        or candidate.name != value
        or len(candidate.parts) != 1
        or "/" in value
        or "\\" in value
        or "\x00" in value
    ):
        raise DecimationError(
            "invalid manifest: out must be a printable single-line bare .glb filename"
        )
    try:
        encoded = os.fsencode(value)
    except UnicodeEncodeError as exc:
        raise DecimationError(
            "invalid manifest: out must be a printable single-line bare .glb filename"
        ) from exc
    if len(encoded) > MAX_TRANSACTION_SAFE_FILENAME_BYTES:
        raise DecimationError(
            "invalid manifest: out filename length exceeds transaction-safe limit"
        )
    return value


def _load_manifest(path: Path) -> list[dict[str, str]]:
    document, _ = _load_json_bytes(path, "manifest", MAX_METADATA_BYTES)
    if not isinstance(document, dict):
        raise DecimationError("invalid manifest: root must be an object")
    entries = document.get("assets")
    if not isinstance(entries, list) or not entries:
        raise DecimationError("invalid manifest: assets must be a non-empty list")
    if len(entries) > MAX_MANIFEST_ASSETS:
        raise DecimationError(
            f"invalid manifest: asset count exceeds limit {MAX_MANIFEST_ASSETS}"
        )

    assets: list[dict[str, str]] = []
    identifiers: set[str] = set()
    outputs: set[str] = set()
    normalized_outputs: set[str] = set()
    for index, value in enumerate(entries):
        if not isinstance(value, dict):
            raise DecimationError(
                f"invalid manifest: assets[{index}] must be an object"
            )
        try:
            identifier = _nonempty_string(value.get("id"), f"assets[{index}].id")
        except DecimationError:
            raise DecimationError(
                "invalid manifest: id must be a printable single-line string"
            ) from None
        if not identifier.isprintable():
            raise DecimationError(
                "invalid manifest: id must be a printable single-line string"
            )
        output = _bare_glb_filename(value.get("out"))
        kind = _nonempty_string(value.get("kind"), f"assets[{index}].kind")
        service = _nonempty_string(
            value.get("service"), f"assets[{index}].service"
        )
        prompt = _nonempty_string(value.get("prompt"), f"assets[{index}].prompt")
        if identifier in identifiers or output in outputs:
            raise DecimationError("invalid manifest: duplicate id or out")
        normalized_output = unicodedata.normalize("NFC", output).casefold()
        if normalized_output in normalized_outputs:
            raise DecimationError(
                "invalid manifest: duplicate out after case-fold/Unicode normalization"
            )
        if kind not in POLICY:
            raise DecimationError(f"unsupported kind for asset {identifier}")
        if service not in KNOWN_SERVICES:
            raise DecimationError(f"unsupported service for asset {identifier}")
        identifiers.add(identifier)
        outputs.add(output)
        normalized_outputs.add(normalized_output)
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


def _sanitized_environment(private_root: Path) -> dict[str, str]:
    """Build an explicit child allowlist with private user/config/temp roots."""
    child_env = {
        name: os.environ[name]
        for name in _CHILD_ENVIRONMENT_PASSTHROUGH
        if name in os.environ
    }
    child_env.setdefault("PATH", os.defpath)
    for name in _FAKE_BLENDER_ENVIRONMENT:
        if name in os.environ:
            child_env[name] = os.environ[name]

    roots = {
        "HOME": "home",
        "TMPDIR": "tmp",
        "TMP": "tmp",
        "TEMP": "tmp",
        "XDG_CONFIG_HOME": "xdg-config",
        "XDG_CACHE_HOME": "xdg-cache",
        "XDG_DATA_HOME": "xdg-data",
        "XDG_STATE_HOME": "xdg-state",
    }
    made: dict[str, Path] = {}
    for name, leaf in roots.items():
        directory = made.get(leaf)
        if directory is None:
            directory = Path(private_root) / leaf
            directory.mkdir(mode=0o700)
            os.chmod(directory, 0o700)
            made[leaf] = directory
        child_env[name] = str(directory)
    return child_env


def _terminate_child(process: subprocess.Popen[bytes]) -> None:
    """Terminate the whole isolated child group, even if its leader exited."""

    def group_alive() -> bool:
        process.poll()
        try:
            os.killpg(process.pid, 0)
        except ProcessLookupError:
            return False
        except PermissionError:
            return True
        return True

    def signal_group(selected: signal.Signals) -> None:
        try:
            os.killpg(process.pid, selected)
        except ProcessLookupError:
            pass
        except PermissionError:
            if process.poll() is None:
                if selected == signal.SIGTERM:
                    process.terminate()
                else:
                    process.kill()

    for selected, grace in ((signal.SIGTERM, 1.0), (signal.SIGKILL, 1.0)):
        signal_group(selected)
        deadline = time.monotonic() + grace
        while group_alive() and time.monotonic() < deadline:
            time.sleep(0.01)
        if not group_alive():
            break

    if process.poll() is None:
        try:
            process.wait(timeout=1)
        except subprocess.TimeoutExpired:
            signal_group(signal.SIGKILL)
            process.wait(timeout=1)
    else:
        process.wait()


def _run_child_bounded(
    command: list[str],
    *,
    timeout: int,
    child_env: Mapping[str, str],
) -> tuple[int, bytes, bytes]:
    """Drain child stdout/stderr concurrently with independent hard ceilings."""
    try:
        process = subprocess.Popen(
            command,
            shell=False,
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            env=dict(child_env),
            start_new_session=True,
        )
    except OSError as exc:
        raise DecimationError("Blender child could not start") from exc
    if process.stdout is None or process.stderr is None:
        _terminate_child(process)
        raise DecimationError("Blender child capture could not start")

    selector = selectors.DefaultSelector()
    streams = {"stdout": process.stdout, "stderr": process.stderr}
    buffers = {"stdout": bytearray(), "stderr": bytearray()}
    for name, stream in streams.items():
        os.set_blocking(stream.fileno(), False)
        selector.register(stream, selectors.EVENT_READ, name)
    deadline = time.monotonic() + timeout
    failure: DecimationError | None = None
    try:
        while selector.get_map() or process.poll() is None:
            remaining_time = deadline - time.monotonic()
            if remaining_time <= 0:
                failure = DecimationError("Blender child timed out")
                break
            events = selector.select(min(0.1, remaining_time))
            for key, _ in events:
                stream = key.fileobj
                name = key.data
                room = MAX_CHILD_STREAM_BYTES - len(buffers[name])
                try:
                    payload = os.read(stream.fileno(), min(64 * 1024, room + 1))
                except BlockingIOError:
                    continue
                if not payload:
                    selector.unregister(stream)
                    continue
                if len(payload) > room:
                    buffers[name].extend(payload[:room])
                    failure = DecimationError(
                        f"Blender child {name} exceeded bounded output limit"
                    )
                    break
                buffers[name].extend(payload)
            if failure is not None:
                break
        if failure is not None:
            _terminate_child(process)
            raise failure
        returncode = process.wait()
        return returncode, bytes(buffers["stdout"]), bytes(buffers["stderr"])
    finally:
        if process.poll() is None:
            _terminate_child(process)
        for stream in streams.values():
            try:
                selector.unregister(stream)
            except (KeyError, ValueError):
                pass
            stream.close()
        selector.close()


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
        returncode, stdout, _stderr = _run_child_bounded(
            [str(blender), "--background", "--version"],
            timeout=VERSION_TIMEOUT_SECONDS,
            child_env=child_env,
        )
        if returncode != 0:
            raise DecimationError("Blender version check failed")
    except DecimationError as exc:
        raise DecimationError("Blender version check failed") from exc

    lines = [line.strip() for line in stdout.decode("utf-8", "replace").splitlines() if line.strip()]
    expected_version = f"Blender {BLENDER_VERSION}"
    if expected_version not in lines:
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
    if len(serialized.encode("utf-8")) > MAX_PROVENANCE_BYTES:
        raise DecimationError(
            f"staged provenance exceeds {MAX_PROVENANCE_BYTES}-byte size limit"
        )
    created = False
    try:
        with path.open("x", encoding="utf-8", newline="\n") as handle:
            created = True
            handle.write(serialized)
            handle.flush()
            os.fsync(handle.fileno())
        _checked_lstat(path, "staged provenance", MAX_PROVENANCE_BYTES)
    except BaseException:
        if created:
            try:
                path.unlink(missing_ok=True)
            except OSError:
                pass
        raise


def _unique_backup(path: Path) -> Path:
    for _ in range(16):
        candidate = path.with_name(f".{path.name}.backup-{uuid.uuid4().hex}")
        if not _path_exists(candidate):
            return candidate
    raise DecimationError("could not allocate a unique derivative backup path")


def _unique_retired(path: Path) -> Path:
    for _ in range(16):
        candidate = path.with_name(f".{path.name}.retired-{uuid.uuid4().hex}")
        if not _path_exists(candidate):
            return candidate
    raise DecimationError("could not allocate a private retired transaction path")


class _PromotionLock:
    """One in-process pair lock backed by an inter-process directory flock."""

    def __init__(self, directory: Path) -> None:
        self._directory = directory
        self._thread_lock = threading.Lock()
        self._directory_fd: int | None = None

    def acquire(self, blocking: bool = True) -> bool:
        acquired_thread = self._thread_lock.acquire(blocking)
        if not acquired_thread:
            return False

        directory_fd: int | None = None
        try:
            directory_fd = os.open(
                self._directory,
                os.O_RDONLY | getattr(os, "O_DIRECTORY", 0),
            )
            operation = fcntl.LOCK_EX
            if not blocking:
                operation |= fcntl.LOCK_NB
            try:
                fcntl.flock(directory_fd, operation)
            except BlockingIOError:
                os.close(directory_fd)
                self._thread_lock.release()
                return False
            self._directory_fd = directory_fd
            return True
        except BaseException:
            if directory_fd is not None:
                try:
                    os.close(directory_fd)
                except OSError:
                    pass
            self._thread_lock.release()
            raise

    def release(self) -> None:
        directory_fd = self._directory_fd
        if directory_fd is None:
            raise RuntimeError("promotion lock is not held")
        self._directory_fd = None
        try:
            fcntl.flock(directory_fd, fcntl.LOCK_UN)
        finally:
            try:
                os.close(directory_fd)
            finally:
                self._thread_lock.release()

    def _discard_after_fork(self) -> None:
        """Close only the child's inherited descriptor and reset thread state."""
        directory_fd = self._directory_fd
        self._directory_fd = None
        if directory_fd is not None:
            try:
                os.close(directory_fd)
            except OSError:
                pass
        self._thread_lock = threading.Lock()


_PROMOTION_LOCKS: dict[tuple[str, str], _PromotionLock] = {}
_PROMOTION_LOCKS_GUARD = threading.Lock()
_PROMOTION_LOCKS_PID = os.getpid()
_PROMOTION_ACCEPTANCE = threading.local()


def _reset_promotion_locks_after_fork() -> None:
    global _PROMOTION_LOCKS, _PROMOTION_LOCKS_GUARD, _PROMOTION_LOCKS_PID

    for inherited_lock in _PROMOTION_LOCKS.values():
        inherited_lock._discard_after_fork()
    _PROMOTION_LOCKS = {}
    _PROMOTION_LOCKS_GUARD = threading.Lock()
    _PROMOTION_LOCKS_PID = os.getpid()


if hasattr(os, "register_at_fork"):
    os.register_at_fork(after_in_child=_reset_promotion_locks_after_fork)


def _canonical_lock_path(path: Path) -> str:
    resolved = os.path.realpath(os.path.abspath(os.fspath(path)))
    return unicodedata.normalize("NFC", resolved).casefold()


def _promotion_lock_for(final_glb: Path, final_json: Path) -> _PromotionLock:
    """Return the stable same-output lock for this process."""
    global _PROMOTION_LOCKS, _PROMOTION_LOCKS_GUARD, _PROMOTION_LOCKS_PID

    current_pid = os.getpid()
    if current_pid != _PROMOTION_LOCKS_PID:
        # Defensive fallback for runtimes without register_at_fork support.
        _reset_promotion_locks_after_fork()

    final_glb = Path(final_glb)
    final_json = Path(final_json)
    if final_glb.parent != final_json.parent:
        raise DecimationError("derivative pair must share one output directory")
    key = (_canonical_lock_path(final_glb), _canonical_lock_path(final_json))
    directory = Path(os.path.realpath(os.path.abspath(final_glb.parent)))
    with _PROMOTION_LOCKS_GUARD:
        lock = _PROMOTION_LOCKS.get(key)
        if lock is None:
            lock = _PromotionLock(directory)
            _PROMOTION_LOCKS[key] = lock
        return lock


def _promotion_acceptance_key(
    staged_glb: Path,
    staged_json: Path,
    final_glb: Path,
    final_json: Path,
    force: bool,
) -> tuple[str, str, str, str, bool]:
    return (
        os.path.abspath(staged_glb),
        os.path.abspath(staged_json),
        os.path.abspath(final_glb),
        os.path.abspath(final_json),
        force,
    )


def _acceptance_records() -> dict[tuple[str, str, str, str, bool], dict[str, object]]:
    records = getattr(_PROMOTION_ACCEPTANCE, "records", None)
    if records is None:
        records = {}
        _PROMOTION_ACCEPTANCE.records = records
    return records


def _queue_promotion_acceptance(
    staged_glb: Path,
    staged_json: Path,
    final_glb: Path,
    final_json: Path,
    force: bool,
    record: dict[str, object],
) -> tuple[str, str, str, str, bool]:
    key = _promotion_acceptance_key(
        staged_glb, staged_json, final_glb, final_json, force
    )
    records = _acceptance_records()
    if key in records:
        raise AssertionError("duplicate pending promotion acceptance")
    records[key] = record
    return key


@contextlib.contextmanager
def _promotion_guard(
    final_glb: Path,
    final_json: Path,
    *,
    on_attempt=None,
    on_acquired=None,
):
    """Serialize one complete destination decision and pair transaction."""
    lock = _promotion_lock_for(final_glb, final_json)
    held = lock.acquire(False)
    contended = not held
    try:
        if on_attempt is not None:
            on_attempt(contended)
        if not held:
            held = lock.acquire(True)
            if not held:
                raise RuntimeError("blocking promotion lock acquisition failed")
        if on_acquired is not None:
            on_acquired(contended)
        yield
    finally:
        if held:
            lock.release()


def _sha256_match_status(
    path: Path,
    expected: str,
    maximum_bytes: int = MAX_SOURCE_GLB_BYTES,
) -> bool | None:
    """Return an exact match decision, or None when identity is unreadable."""
    try:
        _checked_lstat(path, "transaction member", maximum_bytes)
        with _sha256_role("transaction member", maximum_bytes):
            return _sha256(path) == expected
    except FileNotFoundError:
        return False
    except DecimationError:
        return False
    except OSError:
        return None


def _matches_sha256(
    path: Path,
    expected: str,
    maximum_bytes: int = MAX_SOURCE_GLB_BYTES,
) -> bool:
    return _sha256_match_status(path, expected, maximum_bytes) is True


def _remove_non_old_final(
    final: Path,
    old_sha: str,
    maximum_bytes: int,
) -> None:
    if (
        _path_exists(final)
        and _sha256_match_status(final, old_sha, maximum_bytes) is False
    ):
        final.unlink()


def _unlink_pair_bounded(first: Path, second: Path, error_message: str) -> None:
    """Independently remove both members, retrying one reported unlink fault."""
    members = (first, second)
    for _ in range(2):
        for member in members:
            if not _path_exists(member):
                continue
            try:
                member.unlink()
            except OSError:
                pass
        if not any(_path_exists(member) for member in members):
            return
    raise DecimationError(error_message)


def _write_old_member(
    destination: Path,
    payload: bytes,
    old_sha: str,
    maximum_bytes: int,
) -> bool:
    """Materialize one bounded old member without following a raced alias."""
    try:
        destination.unlink(missing_ok=True)
        _write_private_file(destination, payload, mode=0o600)
    except (OSError, DecimationError):
        try:
            destination.unlink(missing_ok=True)
        except OSError:
            pass
        return False
    return _matches_sha256(destination, old_sha, maximum_bytes)


def _old_final_pair(
    final_glb: Path,
    final_json: Path,
    backup_glb: Path,
    backup_json: Path,
    old_glb_sha: str,
    old_json_sha: str,
) -> bool:
    return (
        _matches_sha256(final_glb, old_glb_sha, MAX_DERIVATIVE_GLB_BYTES)
        and _matches_sha256(final_json, old_json_sha, MAX_PROVENANCE_BYTES)
        and not _path_exists(backup_glb)
        and not _path_exists(backup_json)
    )


def _old_backup_pair(
    final_glb: Path,
    final_json: Path,
    backup_glb: Path,
    backup_json: Path,
    old_glb_sha: str,
    old_json_sha: str,
) -> bool:
    return (
        not _path_exists(final_glb)
        and not _path_exists(final_json)
        and _matches_sha256(backup_glb, old_glb_sha, MAX_DERIVATIVE_GLB_BYTES)
        and _matches_sha256(backup_json, old_json_sha, MAX_PROVENANCE_BYTES)
    )


def _restore_old_pair(
    final_glb: Path,
    final_json: Path,
    backup_glb: Path,
    backup_json: Path,
    old_glb_sha: str,
    old_json_sha: str,
    old_glb_bytes: bytes,
    old_json_bytes: bytes,
) -> None:
    """Attempt both restores, then normalize to one verified terminal state."""
    members = (
        (
            backup_glb,
            final_glb,
            old_glb_sha,
            old_glb_bytes,
            MAX_DERIVATIVE_GLB_BYTES,
        ),
        (
            backup_json,
            final_json,
            old_json_sha,
            old_json_bytes,
            MAX_PROVENANCE_BYTES,
        ),
    )

    def exact_payload(path: Path, payload: bytes, maximum_bytes: int) -> bool:
        try:
            _, _, observed, _ = _verified_hash(
                path,
                "rollback member",
                maximum_bytes,
            )
        except (DecimationError, OSError):
            return False
        return observed == payload

    # Keep the frozen one-argument hash seam observable after a failed forward
    # operation. An unreadable hash is unknown, never an affirmative match.
    initial_glb_match = _sha256_match_status(
        final_glb, old_glb_sha, MAX_DERIVATIVE_GLB_BYTES
    )
    initial_json_match = _sha256_match_status(
        final_json, old_json_sha, MAX_PROVENANCE_BYTES
    )
    if (
        initial_glb_match is True
        and initial_json_match is True
        and not _path_exists(backup_glb)
        and not _path_exists(backup_json)
    ):
        return

    if (
        not _path_exists(backup_glb)
        and not _path_exists(backup_json)
        and exact_payload(final_glb, old_glb_bytes, MAX_DERIVATIVE_GLB_BYTES)
        and exact_payload(final_json, old_json_bytes, MAX_PROVENANCE_BYTES)
    ):
        return

    # Candidate members cannot be allowed to overwrite the captured old pair.
    for _, final, old_sha, _, maximum_bytes in members:
        try:
            _remove_non_old_final(final, old_sha, maximum_bytes)
        except OSError:
            pass

    # These attempts are independent: a GLB restore error never skips JSON.
    for backup, final, old_sha, _, maximum_bytes in members:
        if not _matches_sha256(backup, old_sha, maximum_bytes):
            continue
        try:
            if _path_exists(final):
                final.unlink()
            os.replace(backup, final)
        except OSError:
            continue

    if _old_final_pair(
        final_glb,
        final_json,
        backup_glb,
        backup_json,
        old_glb_sha,
        old_json_sha,
    ):
        return

    # A replace-after-effect race may consume and then alias a backup. Captured
    # pre-transaction bytes remain the recovery authority in that case.
    for _, final, old_sha, old_bytes, maximum_bytes in members:
        if _matches_sha256(final, old_sha, maximum_bytes):
            continue
        _write_old_member(final, old_bytes, old_sha, maximum_bytes)
    if _matches_sha256(
        final_glb, old_glb_sha, MAX_DERIVATIVE_GLB_BYTES
    ) and _matches_sha256(
        final_json, old_json_sha, MAX_PROVENANCE_BYTES
    ):
        _unlink_pair_bounded(
            backup_glb,
            backup_json,
            "forced rollback could not remove old backups",
        )
    if not _old_final_pair(
        final_glb,
        final_json,
        backup_glb,
        backup_json,
        old_glb_sha,
        old_json_sha,
    ):
        raise DecimationError("forced promotion could not recover the old pair")


def _retire_absent_partial_pair(
    staged_glb: Path,
    staged_json: Path,
    final_glb: Path,
    final_json: Path,
    candidate_glb_sha: str,
    candidate_json_sha: str,
) -> None:
    """Move a persistently undeletable partial candidate out of final names."""
    retired_glb = _unique_retired(final_glb)
    retired_json = _unique_retired(final_json)
    members = (
        (
            final_glb,
            staged_glb,
            retired_glb,
            candidate_glb_sha,
            MAX_DERIVATIVE_GLB_BYTES,
        ),
        (
            final_json,
            staged_json,
            retired_json,
            candidate_json_sha,
            MAX_PROVENANCE_BYTES,
        ),
    )
    for final, staged, retired, _, _ in members:
        source = final if _path_exists(final) else staged
        if _path_exists(source):
            os.replace(source, retired)
    if _path_exists(final_glb) or _path_exists(final_json):
        raise DecimationError("absent-destination promotion retained a partial final")
    if not all(
        _matches_sha256(retired, expected, maximum_bytes)
        for _, _, retired, expected, maximum_bytes in members
    ):
        try:
            _unlink_pair_bounded(
                retired_glb,
                retired_json,
                "absent-destination promotion retained invalid retired members",
            )
        finally:
            raise DecimationError(
                "absent-destination promotion could not retire exact candidate pair"
            ) from None


def _status_fingerprint(status: os.stat_result) -> tuple[int, int, int, int, int, int]:
    return (
        status.st_dev,
        status.st_ino,
        status.st_size,
        status.st_mtime_ns,
        status.st_ctime_ns,
        status.st_nlink,
    )


def _accepted_candidate(
    path: Path,
    label: str,
    maximum_bytes: int,
    expected_sha: str | None,
    expected_state: tuple[int, int, int, int, int, int] | None,
) -> tuple[str, tuple[int, int, int, int, int, int]]:
    if expected_sha is not None and expected_state is not None:
        status = _checked_lstat(path, label, maximum_bytes)
        if status is None or _status_fingerprint(status) != expected_state:
            raise DecimationError(f"{label} changed after acceptance")
        return expected_sha, expected_state
    digest, _, _, status = _verified_hash(path, label, maximum_bytes)
    if expected_sha is not None and digest != expected_sha:
        raise DecimationError(f"{label} SHA-256 changed after acceptance")
    return digest, _status_fingerprint(status)


def _verified_transaction_member(
    path: Path,
    label: str,
    maximum_bytes: int,
    expected_sha: str,
    expected_identity: tuple[int, int],
) -> bool:
    try:
        digest, identity, _, _ = _verified_hash(path, label, maximum_bytes)
    except (DecimationError, OSError):
        return False
    return digest == expected_sha and identity == expected_identity


def promote_pair(
    staged_glb: Path,
    staged_json: Path,
    final_glb: Path,
    final_json: Path,
    force: bool,
    *,
    expected_glb_sha: str | None = None,
    expected_json_sha: str | None = None,
    expected_glb_state: tuple[int, int, int, int, int, int] | None = None,
    expected_json_state: tuple[int, int, int, int, int, int] | None = None,
    verify_before=None,
) -> None:
    """Promote a derivative/provenance pair, rolling back the complete old pair."""
    staged_glb = Path(staged_glb)
    staged_json = Path(staged_json)
    final_glb = Path(final_glb)
    final_json = Path(final_json)
    acceptance_key = _promotion_acceptance_key(
        staged_glb, staged_json, final_glb, final_json, force
    )
    queued = _acceptance_records().pop(acceptance_key, None)
    if queued is not None:
        if any(
            value is not None
            for value in (
                expected_glb_sha,
                expected_json_sha,
                expected_glb_state,
                expected_json_state,
                verify_before,
            )
        ):
            raise AssertionError("promotion acceptance supplied twice")
        expected_glb_sha = queued.get("expected_glb_sha")
        expected_json_sha = queued.get("expected_json_sha")
        expected_glb_state = queued.get("expected_glb_state")
        expected_json_state = queued.get("expected_json_state")
        verify_before = queued.get("verify_before")
        if not (
            isinstance(expected_glb_sha, str)
            and isinstance(expected_json_sha, str)
            and isinstance(expected_glb_state, tuple)
            and isinstance(expected_json_state, tuple)
            and callable(verify_before)
        ):
            raise AssertionError("invalid pending promotion acceptance")
    candidate_glb_sha = ""
    candidate_json_sha = ""
    candidate_glb_identity: tuple[int, int] | None = None
    candidate_json_identity: tuple[int, int] | None = None
    backup_glb: Path | None = None
    backup_json: Path | None = None
    old_glb_sha: str | None = None
    old_json_sha: str | None = None
    old_glb_bytes: bytes | None = None
    old_json_bytes: bytes | None = None
    destination_was_present = False

    try:
        with _promotion_guard(final_glb, final_json):
            candidate_glb_sha, glb_state = _accepted_candidate(
                staged_glb,
                "staged derivative GLB",
                MAX_DERIVATIVE_GLB_BYTES,
                expected_glb_sha,
                expected_glb_state,
            )
            candidate_json_sha, json_state = _accepted_candidate(
                staged_json,
                "staged derivative JSON",
                MAX_PROVENANCE_BYTES,
                expected_json_sha,
                expected_json_state,
            )
            candidate_glb_identity = (glb_state[0], glb_state[1])
            candidate_json_identity = (json_state[0], json_state[1])

            glb_exists = _path_exists(final_glb)
            json_exists = _path_exists(final_json)
            if glb_exists != json_exists:
                raise DecimationError("existing derivative lineage is inconsistent")
            if glb_exists and not force:
                raise DecimationError("refusing existing derivative without --force")
            destination_was_present = glb_exists
            if verify_before is not None:
                verify_before()

            if not glb_exists:
                try:
                    os.replace(staged_glb, final_glb)
                    os.replace(staged_json, final_json)
                    if not (
                        _verified_transaction_member(
                            final_glb,
                            "published derivative GLB",
                            MAX_DERIVATIVE_GLB_BYTES,
                            candidate_glb_sha,
                            candidate_glb_identity,
                        )
                        and _verified_transaction_member(
                            final_json,
                            "published derivative JSON",
                            MAX_PROVENANCE_BYTES,
                            candidate_json_sha,
                            candidate_json_identity,
                        )
                    ):
                        raise DecimationError(
                            "absent-destination promotion pair verification failed"
                        )
                except BaseException:
                    try:
                        _unlink_pair_bounded(
                            final_glb,
                            final_json,
                            "absent-destination promotion could not remove partial pair",
                        )
                    except DecimationError:
                        _retire_absent_partial_pair(
                            staged_glb,
                            staged_json,
                            final_glb,
                            final_json,
                            candidate_glb_sha,
                            candidate_json_sha,
                        )
                    raise
                return

            old_glb_sha, _, old_glb_bytes, _ = _sha256_receipt(
                final_glb,
                "existing derivative GLB",
                MAX_DERIVATIVE_GLB_BYTES,
            )
            old_json_sha, _, old_json_bytes, _ = _sha256_receipt(
                final_json,
                "existing derivative JSON",
                MAX_PROVENANCE_BYTES,
            )
            backup_glb = _unique_backup(final_glb)
            backup_json = _unique_backup(final_json)

            try:
                os.replace(final_glb, backup_glb)
                os.replace(final_json, backup_json)
                os.replace(staged_glb, final_glb)
                os.replace(staged_json, final_json)
                if not (
                    _verified_transaction_member(
                        final_glb,
                        "published derivative GLB",
                        MAX_DERIVATIVE_GLB_BYTES,
                        candidate_glb_sha,
                        candidate_glb_identity,
                    )
                    and _verified_transaction_member(
                        final_json,
                        "published derivative JSON",
                        MAX_PROVENANCE_BYTES,
                        candidate_json_sha,
                        candidate_json_identity,
                    )
                    and _matches_sha256(
                        backup_glb, old_glb_sha, MAX_DERIVATIVE_GLB_BYTES
                    )
                    and _matches_sha256(
                        backup_json, old_json_sha, MAX_PROVENANCE_BYTES
                    )
                ):
                    raise DecimationError("forced promotion pair verification failed")
            except BaseException as primary_error:
                _restore_old_pair(
                    final_glb,
                    final_json,
                    backup_glb,
                    backup_json,
                    old_glb_sha,
                    old_json_sha,
                    old_glb_bytes,
                    old_json_bytes,
                )
                raise primary_error

            try:
                _unlink_pair_bounded(
                    backup_glb,
                    backup_json,
                    "forced promotion could not remove old backups",
                )
            except BaseException as cleanup_error:
                _restore_old_pair(
                    final_glb,
                    final_json,
                    backup_glb,
                    backup_json,
                    old_glb_sha,
                    old_json_sha,
                    old_glb_bytes,
                    old_json_bytes,
                )
                raise cleanup_error
    except BaseException as transaction_error:
        # A lock-release fault can arrive after the exact commit is durable.
        # Suppress only that after-effect state; every other exit is normalized.
        if (
            candidate_glb_identity is not None
            and candidate_json_identity is not None
            and _verified_transaction_member(
                final_glb,
                "published derivative GLB",
                MAX_DERIVATIVE_GLB_BYTES,
                candidate_glb_sha,
                candidate_glb_identity,
            )
            and _verified_transaction_member(
                final_json,
                "published derivative JSON",
                MAX_PROVENANCE_BYTES,
                candidate_json_sha,
                candidate_json_identity,
            )
            and (backup_glb is None or not _path_exists(backup_glb))
            and (backup_json is None or not _path_exists(backup_json))
        ):
            return
        if (
            destination_was_present
            and backup_glb is not None
            and backup_json is not None
            and old_glb_sha is not None
            and old_json_sha is not None
            and old_glb_bytes is not None
            and old_json_bytes is not None
        ):
            _restore_old_pair(
                final_glb,
                final_json,
                backup_glb,
                backup_json,
                old_glb_sha,
                old_json_sha,
                old_glb_bytes,
                old_json_bytes,
            )
        raise transaction_error


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
) -> dict[str, object]:
    identifier = asset["id"]
    kind = asset["kind"]
    policy = POLICY[kind]
    source_path = prepared["source_path"]
    source_sidecar_path = prepared["source_sidecar_path"]
    original_source_path = prepared["original_source_path"]
    final_glb = prepared["final_glb"]
    final_json = prepared["final_json"]
    source_metrics = prepared["source_metrics"]
    source_record = prepared["source_record"]
    source_sha = prepared["source_sha"]
    source_sidecar_sha = prepared["source_sidecar_sha"]
    if not all(
        isinstance(path, Path)
        for path in (
            source_path,
            source_sidecar_path,
            original_source_path,
            final_glb,
            final_json,
        )
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
    _emit_record(
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
        returncode, _child_stdout, _child_stderr = _run_child_bounded(
            command,
            timeout=BLENDER_TIMEOUT_SECONDS,
            child_env=child_env,
        )
    except DecimationError as exc:
        raise DecimationError(f"Blender failed for asset {identifier}: {exc}") from exc
    if returncode != 0:
        raise DecimationError(
            f"Blender failed for asset {identifier} with exit {returncode}"
        )

    staged_status = _checked_lstat(
        staged_glb,
        "derivative GLB",
        MAX_DERIVATIVE_GLB_BYTES,
        allow_missing=True,
    )
    if staged_status is None or staged_status.st_size == 0:
        raise DecimationError("Blender failed to produce a non-empty derivative")
    try:
        output_metrics = inspect_glb(staged_glb)
    except (GlbError, OSError) as exc:
        message = _glb_error_message(exc) if isinstance(exc, GlbError) else str(exc)
        if isinstance(exc, GlbError) and _MISSING_MATERIAL_UV.fullmatch(str(exc)):
            message = "lost UV"
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
    accepted_glb_status = _checked_lstat(
        staged_glb,
        "accepted derivative GLB",
        MAX_DERIVATIVE_GLB_BYTES,
    )
    if accepted_glb_status is None:
        raise AssertionError("accepted derivative unexpectedly missing")

    record = _provenance_record(
        original_source_path,
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
    staged_record, staged_json_bytes = _load_json_bytes(
        staged_json,
        "staged provenance",
        MAX_PROVENANCE_BYTES,
    )
    _reject_forbidden_provenance(staged_record)
    if not isinstance(staged_record, dict):
        raise DecimationError("invalid staged provenance")
    derivative = staged_record.get("derivative")
    if (
        not isinstance(derivative, dict)
        or derivative.get("sha256") != output_metrics["sha256"]
    ):
        raise DecimationError("staged provenance derivative SHA-256 mismatch")

    # Re-seed the accepted provenance bytes onto a fresh private inode. This
    # permits one bounded acceptance read and one independently bounded
    # promotion read without rereading the same exact-boundary inode.
    reseed = asset_staging / f".provenance-{uuid.uuid4().hex}"
    try:
        _write_private_file(reseed, staged_json_bytes, mode=0o600)
        os.replace(reseed, staged_json)
    finally:
        try:
            reseed.unlink(missing_ok=True)
        except OSError:
            pass
    accepted_json_status = _checked_lstat(
        staged_json,
        "accepted derivative JSON",
        MAX_PROVENANCE_BYTES,
    )
    if accepted_json_status is None:
        raise AssertionError("accepted provenance unexpectedly missing")

    accepted_json_sha = hashlib.sha256(staged_json_bytes).hexdigest()
    return {
        "staged_glb": staged_glb,
        "staged_json": staged_json,
        "final_glb": final_glb,
        "final_json": final_json,
        "force": force,
        "expected_glb_sha": str(output_metrics["sha256"]),
        "expected_json_sha": accepted_json_sha,
        "expected_glb_state": _status_fingerprint(accepted_glb_status),
        "expected_json_state": _status_fingerprint(accepted_json_status),
        "prepared": prepared,
        "success_record": (
            f"asset={identifier} output_triangles={output_metrics['triangles']} "
            f"output_vertices={output_metrics['vertices']}"
        ),
    }


def _verify_snapshot_pair(prepared: Mapping[str, object]) -> None:
    source = prepared["source_path"]
    sidecar = prepared["source_sidecar_path"]
    source_identity = prepared["source_snapshot_identity"]
    sidecar_identity = prepared["source_sidecar_snapshot_identity"]
    source_sha = prepared["source_sha"]
    sidecar_sha = prepared["source_sidecar_sha"]
    if not (
        isinstance(source, Path)
        and isinstance(sidecar, Path)
        and isinstance(source_identity, tuple)
        and isinstance(sidecar_identity, tuple)
        and isinstance(source_sha, str)
        and isinstance(sidecar_sha, str)
    ):
        raise AssertionError("internal snapshot custody type mismatch")
    current_source_sha, _, _, _ = _verified_hash(
        source,
        "source snapshot GLB",
        MAX_SOURCE_GLB_BYTES,
        expected_identity=source_identity,
    )
    current_sidecar_sha, _, _, _ = _verified_hash(
        sidecar,
        "source snapshot sidecar",
        MAX_METADATA_BYTES,
        expected_identity=sidecar_identity,
    )
    if current_source_sha != source_sha:
        raise DecimationError("source snapshot changed during decimation")
    if current_sidecar_sha != sidecar_sha:
        raise DecimationError("source sidecar snapshot changed during decimation")


def _filesystem_identity(
    path: Path,
    label: str,
    maximum_bytes: int,
    *,
    allow_missing: bool = True,
) -> tuple[int, int] | None:
    status = _checked_lstat(
        path,
        label,
        maximum_bytes,
        allow_missing=allow_missing,
    )
    return None if status is None else _identity(status)


def _prepare_assets(
    assets: list[dict[str, str]],
    input_base: Path,
    input_root: Path,
    output_root: Path,
    snapshot_root: Path,
    force: bool,
) -> list[dict[str, object]]:
    path_sets: list[dict[str, object]] = []
    source_paths: set[Path] = set()
    output_paths: set[Path] = set()
    existing_identities: dict[tuple[int, int], tuple[str, Path]] = {}
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
        with _promotion_guard(final_glb, final_json):
            _destination_state(final_glb, final_json, force)

        if resolved_source in source_paths or resolved_source_sidecar in source_paths:
            raise DecimationError("source paths alias across manifest entries")
        source_paths.update((resolved_source, resolved_source_sidecar))
        if resolved_final_glb in output_paths or resolved_final_json in output_paths:
            raise DecimationError("output paths alias across manifest entries")
        output_paths.update((resolved_final_glb, resolved_final_json))

        identities: dict[str, tuple[int, int] | None] = {}
        for key, label, candidate, maximum, allow_missing in (
            (
                "source_identity",
                f"source for asset {asset['id']}",
                source_path,
                MAX_SOURCE_GLB_BYTES,
                False,
            ),
            (
                "source_sidecar_identity",
                f"source sidecar for asset {asset['id']}",
                source_sidecar_path,
                MAX_METADATA_BYTES,
                False,
            ),
            (
                "final_glb_identity",
                f"output GLB for asset {asset['id']}",
                final_glb,
                MAX_DERIVATIVE_GLB_BYTES,
                True,
            ),
            (
                "final_json_identity",
                f"output JSON for asset {asset['id']}",
                final_json,
                MAX_PROVENANCE_BYTES,
                True,
            ),
        ):
            identity = _filesystem_identity(
                candidate,
                label,
                maximum,
                allow_missing=allow_missing,
            )
            identities[key] = identity
            if identity is None:
                continue
            previous = existing_identities.get(identity)
            if previous is not None:
                previous_label, previous_path = previous
                raise DecimationError(
                    "filesystem identity alias between "
                    f"{previous_label} ({previous_path}) and {label} ({candidate})"
                )
            existing_identities[identity] = (label, candidate)

        path_sets.append(
            {
                "asset": asset,
                "source_path": source_path,
                "source_sidecar_path": source_sidecar_path,
                "final_glb": final_glb,
                "final_json": final_json,
                **identities,
            }
        )

    if source_paths & output_paths:
        raise DecimationError("source and output paths alias")

    prepared_assets: list[dict[str, object]] = []
    for paths in path_sets:
        asset_value = paths["asset"]
        if not isinstance(asset_value, dict):
            raise AssertionError("internal prepared asset type mismatch")
        asset = asset_value
        source_path = paths["source_path"]
        source_sidecar_path = paths["source_sidecar_path"]
        final_glb = paths["final_glb"]
        final_json = paths["final_json"]
        if not all(
            isinstance(path, Path)
            for path in (source_path, source_sidecar_path, final_glb, final_json)
        ):
            raise AssertionError("internal preflight path type mismatch")
        source_identity = paths["source_identity"]
        source_sidecar_identity = paths["source_sidecar_identity"]
        if not isinstance(source_identity, tuple) or not isinstance(
            source_sidecar_identity, tuple
        ):
            raise AssertionError("internal original identity type mismatch")
        source_sha, _, source_bytes, _ = _verified_hash(
            source_path,
            "source GLB",
            MAX_SOURCE_GLB_BYTES,
            expected_identity=source_identity,
        )
        source_sidecar_sha, _, source_sidecar_bytes, _ = _verified_hash(
            source_sidecar_path,
            "source sidecar",
            MAX_METADATA_BYTES,
            expected_identity=source_sidecar_identity,
        )

        snapshot_source = snapshot_root / asset["out"]
        snapshot_sidecar = snapshot_root / f"{asset['out']}.json"
        _write_private_file(snapshot_source, source_bytes)
        _write_private_file(snapshot_sidecar, source_sidecar_bytes)
        (
            snapshot_source_sha,
            snapshot_source_identity,
            _,
            _,
        ) = _verified_hash(
            snapshot_source,
            "source snapshot GLB",
            MAX_SOURCE_GLB_BYTES,
        )
        (
            snapshot_sidecar_sha,
            snapshot_sidecar_identity,
            snapshot_sidecar_bytes,
            _,
        ) = _verified_hash(
            snapshot_sidecar,
            "source snapshot sidecar",
            MAX_METADATA_BYTES,
        )
        if snapshot_source_sha != source_sha or snapshot_sidecar_sha != source_sidecar_sha:
            raise DecimationError("source snapshot hash verification failed")
        source_record_value = _decode_json_bytes(
            snapshot_sidecar_bytes,
            "source sidecar",
        )
        source_record = _validate_source_record(
            source_record_value, asset, source_sha
        )
        try:
            source_metrics = _inspect_source_glb(snapshot_source)
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
                "source_path": snapshot_source,
                "source_sidecar_path": snapshot_sidecar,
                "source_snapshot_identity": snapshot_source_identity,
                "source_sidecar_snapshot_identity": snapshot_sidecar_identity,
                "original_source_path": source_path,
                "original_source_sidecar_path": source_sidecar_path,
                "original_source_identity": source_identity,
                "original_source_sidecar_identity": source_sidecar_identity,
                "final_glb": final_glb,
                "final_json": final_json,
                "source_sha": source_sha,
                "source_sidecar_sha": source_sidecar_sha,
                "source_record": source_record,
                "source_metrics": source_metrics,
            }
        )
    return prepared_assets


def _verify_original_pair(prepared: Mapping[str, object]) -> None:
    source = prepared["original_source_path"]
    sidecar = prepared["original_source_sidecar_path"]
    source_identity = prepared["original_source_identity"]
    sidecar_identity = prepared["original_source_sidecar_identity"]
    source_sha = prepared["source_sha"]
    sidecar_sha = prepared["source_sidecar_sha"]
    if not (
        isinstance(source, Path)
        and isinstance(sidecar, Path)
        and isinstance(source_identity, tuple)
        and isinstance(sidecar_identity, tuple)
        and isinstance(source_sha, str)
        and isinstance(sidecar_sha, str)
    ):
        raise AssertionError("internal original custody type mismatch")
    current_source_sha, _, _, _ = _verified_hash(
        source,
        "source GLB",
        MAX_SOURCE_GLB_BYTES,
        expected_identity=source_identity,
    )
    current_sidecar_sha, _, _, _ = _verified_hash(
        sidecar,
        "source sidecar",
        MAX_METADATA_BYTES,
        expected_identity=sidecar_identity,
    )
    if current_source_sha != source_sha:
        raise DecimationError("source changed during decimation")
    if current_sidecar_sha != sidecar_sha:
        raise DecimationError("source sidecar changed during decimation")


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


def _publication_is_exact(pending: Mapping[str, object]) -> bool:
    try:
        staged_glb = pending["staged_glb"]
        staged_json = pending["staged_json"]
        final_glb = pending["final_glb"]
        final_json = pending["final_json"]
        glb_sha = pending["expected_glb_sha"]
        json_sha = pending["expected_json_sha"]
        glb_state = pending["expected_glb_state"]
        json_state = pending["expected_json_state"]
        if not (
            isinstance(staged_glb, Path)
            and isinstance(staged_json, Path)
            and isinstance(final_glb, Path)
            and isinstance(final_json, Path)
            and isinstance(glb_sha, str)
            and isinstance(json_sha, str)
            and isinstance(glb_state, tuple)
            and len(glb_state) >= 2
            and isinstance(glb_state[0], int)
            and isinstance(glb_state[1], int)
            and isinstance(json_state, tuple)
            and len(json_state) >= 2
            and isinstance(json_state[0], int)
            and isinstance(json_state[1], int)
        ):
            return False
        return (
            not _path_exists(staged_glb)
            and not _path_exists(staged_json)
            and _verified_transaction_member(
                final_glb,
                "published derivative GLB",
                MAX_DERIVATIVE_GLB_BYTES,
                glb_sha,
                (glb_state[0], glb_state[1]),
            )
            and _verified_transaction_member(
                final_json,
                "published derivative JSON",
                MAX_PROVENANCE_BYTES,
                json_sha,
                (json_state[0], json_state[1]),
            )
        )
    except (DecimationError, OSError, TypeError, ValueError):
        return False


def _run(argv: list[str]) -> None:
    args = _arguments(argv)
    manifest = Path(os.path.abspath(args.manifest))
    input_base = Path(os.path.abspath(args.input_dir))
    input_root = input_base.resolve(strict=True)
    if not input_root.is_dir():
        raise DecimationError("input directory is missing")
    assets = _load_manifest(manifest)
    args.output_dir.mkdir(parents=True, exist_ok=True)
    output_root = Path(os.path.abspath(args.output_dir)).resolve(strict=True)
    if not output_root.is_dir():
        raise DecimationError("output directory is missing")
    try:
        roots_are_same = os.path.samefile(input_root, output_root)
    except OSError as exc:
        raise DecimationError("input/output directory identity cannot be read") from exc
    if roots_are_same:
        raise DecimationError(
            "input and output directories alias the same filesystem identity"
        )

    blender = _resolve_blender(args.blender)
    driver = Path(__file__).resolve().with_name("blender_decimate.py")
    if not driver.is_file():
        raise DecimationError("Blender decimation driver is missing")
    completed_publications: list[dict[str, object]] = []
    private_roots: list[Path] = []
    run_completed = False
    try:
        with (
            tempfile.TemporaryDirectory(
                prefix=".glb-decimation-sources-", dir=output_root
            ) as snapshot_name,
            tempfile.TemporaryDirectory(
                prefix=".glb-decimation-environment-", dir=output_root
            ) as environment_name,
            tempfile.TemporaryDirectory(
                prefix=".glb-decimation-", dir=output_root
            ) as staging_name,
        ):
            snapshot_root = Path(snapshot_name)
            private_environment_root = Path(environment_name)
            run_staging = Path(staging_name)
            private_roots.extend(
                (snapshot_root, private_environment_root, run_staging)
            )
            for directory in private_roots:
                os.chmod(directory, 0o700)
            prepared_assets = _prepare_assets(
                assets,
                input_base,
                input_root,
                output_root,
                snapshot_root,
                args.force,
            )
            child_env = _sanitized_environment(private_environment_root)
            _check_blender_version(blender, child_env)
            for asset, prepared in zip(assets, prepared_assets, strict=True):
                pending = _process_asset(
                    asset,
                    prepared,
                    blender,
                    driver,
                    child_env,
                    run_staging,
                    args.force,
                )
                # The snapshot-seam tests replace the complete processor with a
                # read-only observer. Production processing never returns None.
                if pending is None:
                    continue
                if not isinstance(pending, dict):
                    raise AssertionError("internal pending promotion type mismatch")
                _verify_original_pair(prepared)
                acceptance_key = _queue_promotion_acceptance(
                    pending["staged_glb"],
                    pending["staged_json"],
                    pending["final_glb"],
                    pending["final_json"],
                    bool(pending["force"]),
                    {
                        "expected_glb_sha": pending["expected_glb_sha"],
                        "expected_json_sha": pending["expected_json_sha"],
                        "expected_glb_state": pending["expected_glb_state"],
                        "expected_json_state": pending["expected_json_state"],
                        "verify_before": lambda prepared=prepared: (
                            _verify_snapshot_pair(prepared),
                            _verify_original_pair(prepared),
                        ),
                    },
                )
                try:
                    promote_pair(
                        pending["staged_glb"],
                        pending["staged_json"],
                        pending["final_glb"],
                        pending["final_json"],
                        bool(pending["force"]),
                    )
                finally:
                    _acceptance_records().pop(acceptance_key, None)
                completed_publications.append(pending)
                try:
                    _emit_record(pending["success_record"])
                except Exception:
                    if not _publication_is_exact(pending):
                        raise
            run_completed = True
    except Exception:
        cleanup_finished = bool(private_roots) and not any(
            _path_exists(path) for path in private_roots
        )
        committed_run = (
            run_completed
            and len(completed_publications) == len(assets)
            and all(_publication_is_exact(item) for item in completed_publications)
        )
        if cleanup_finished and committed_run:
            return
        raise


def _diagnostic_payload(message: object) -> bytes:
    raw = str(message) or "input processing failed"
    raw = _HTTP_URI.sub("[redacted-uri]", raw)
    raw = _CREDENTIAL_SHAPE.sub("[redacted]", raw)
    printable: list[str] = []
    for character in raw:
        if character.isprintable():
            printable.append(character)
            continue
        codepoint = ord(character)
        if codepoint <= 0xFF:
            printable.append(f"\\x{codepoint:02x}")
        elif codepoint <= 0xFFFF:
            printable.append(f"\\u{codepoint:04x}")
        else:
            printable.append(f"\\U{codepoint:08x}")
    prefix = b"glb-decimation: "
    budget = MAX_DIAGNOSTIC_BYTES - len(prefix) - 1
    body = "".join(printable).encode("utf-8")
    if len(body) > budget:
        body = body[: budget - 3].decode("utf-8", "ignore").encode("utf-8")
        body += b"..."
    return prefix + body + b"\n"


def _write_record(stream, message: object) -> None:
    payload = _diagnostic_payload(message)
    byte_stream = getattr(stream, "buffer", None)
    if byte_stream is not None:
        byte_stream.write(payload)
        byte_stream.flush()
    else:
        stream.write(payload.decode("utf-8"))
        stream.flush()


def _emit_record(message: object) -> None:
    _write_record(sys.stdout, message)


def _emit_diagnostic(message: object) -> None:
    _write_record(sys.stderr, message)


def main(argv: list[str]) -> int:
    """Run the orchestrator without exiting, for CLI and fault-injection use."""
    try:
        _run(argv)
    except (
        DecimationError,
        GlbError,
        OSError,
        ValueError,
        UnicodeError,
        OverflowError,
        MemoryError,
        RecursionError,
    ) as exc:
        _emit_diagnostic(exc)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
