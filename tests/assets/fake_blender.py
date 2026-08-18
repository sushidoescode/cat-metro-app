#!/usr/bin/env python3
"""Deterministic Blender process double for the GLB decimation contract."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import stat
import struct
import sys
from pathlib import Path


sys.dont_write_bytecode = True
_HELPER_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(_HELPER_DIR))
from glb_fixture import write_glb  # noqa: E402  (path is fixed above)


_MODES = {
    "success",
    "over_budget",
    "under_budget",
    "malformed_output",
    "missing_uv",
    "missing_material",
    "missing_image",
    "bounds_drift",
    "external_image",
    "unsupported_extension",
    "unexpected_scene_content",
    "fail",
}

_FORBIDDEN_ENVIRONMENT_SENTINELS = (
    "PIPELINE_SENTINEL_KEY",
    "PIPELINE_SENTINEL_TOKEN",
    "PIPELINE_SENTINEL_SECRET",
    "PIPELINE_SENTINEL_AUTH",
    "PIPELINE_SENTINEL_CREDENTIAL",
    "PIPELINE_SENTINEL_BEARER",
)


def _reject_forbidden_environment() -> None:
    if any(name in os.environ for name in _FORBIDDEN_ENVIRONMENT_SENTINELS):
        print("fake-blender: forbidden environment sentinel present", file=sys.stderr)
        raise SystemExit(86)


def _audit_phase(phase: str) -> None:
    audit = os.environ.get("FAKE_BLENDER_AUDIT")
    if audit:
        with Path(audit).open("a", encoding="utf-8") as handle:
            handle.write(f"{phase}\n")


def _audit_environment(phase: str) -> None:
    """Record only names plus private-directory facts for environment tests."""
    destination = os.environ.get("FAKE_BLENDER_ENV_LOG")
    if not destination:
        return
    private_names = (
        "HOME",
        "TMPDIR",
        "TMP",
        "TEMP",
        "XDG_CONFIG_HOME",
        "XDG_CACHE_HOME",
        "XDG_DATA_HOME",
        "XDG_STATE_HOME",
    )
    private_paths = {}
    for name in private_names:
        value = os.environ.get(name)
        if value is None:
            continue
        path = Path(value)
        try:
            status = os.lstat(path)
        except OSError:
            private_paths[name] = {"path": value, "exists": False}
        else:
            private_paths[name] = {
                "path": value,
                "exists": True,
                "is_directory": stat.S_ISDIR(status.st_mode),
                "is_symlink": stat.S_ISLNK(status.st_mode),
                "mode": stat.S_IMODE(status.st_mode),
                "uid": status.st_uid,
            }
    record = {
        "phase": phase,
        "names": sorted(os.environ),
        "private_paths": private_paths,
    }
    with Path(destination).open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(record, sort_keys=True) + "\n")


def _source_observation(source: Path) -> dict[str, object]:
    status = os.lstat(source)
    parent_status = os.lstat(source.parent)
    return {
        "source": str(source),
        "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
        "source_lstat_regular": stat.S_ISREG(status.st_mode),
        "source_lstat_symlink": stat.S_ISLNK(status.st_mode),
        "source_nlink": status.st_nlink,
        "source_mode": stat.S_IMODE(status.st_mode),
        "source_uid": status.st_uid,
        "source_parent_mode": stat.S_IMODE(parent_status.st_mode),
        "source_parent_uid": parent_status.st_uid,
    }


def _replace_external_uri(path: Path, uri: str) -> None:
    """Rewrite the fixture's JSON chunk while retaining its BIN payload."""
    payload = path.read_bytes()
    magic, version, _ = struct.unpack_from("<4sII", payload, 0)
    if magic != b"glTF" or version != 2:
        raise RuntimeError("fake-blender: URI rewrite received a malformed GLB")
    json_length, chunk_type = struct.unpack_from("<I4s", payload, 12)
    if chunk_type != b"JSON":
        raise RuntimeError("fake-blender: URI rewrite found no leading JSON chunk")
    document = json.loads(payload[20 : 20 + json_length].rstrip(b" \t\r\n\0"))
    document["images"][0] = {"uri": uri}
    json_payload = json.dumps(
        document, separators=(",", ":"), sort_keys=True
    ).encode("utf-8")
    json_payload += b" " * (-len(json_payload) % 4)
    trailing_chunks = payload[20 + json_length :]
    rebuilt = (
        struct.pack(
            "<4sII", b"glTF", 2, 12 + 8 + len(json_payload) + len(trailing_chunks)
        )
        + struct.pack("<I4s", len(json_payload), b"JSON")
        + json_payload
        + trailing_chunks
    )
    path.write_bytes(rebuilt)


def _pad_glb_to_size(path: Path, size: int) -> None:
    """Append one valid unknown chunk so a boundary-sized GLB stays parseable."""
    current_size = path.stat().st_size
    padding_size = size - current_size - 8
    if size < current_size + 8 or padding_size % 4:
        raise SystemExit("fake-blender: exact GLB size cannot hold an aligned chunk")
    with path.open("r+b") as handle:
        magic, version, declared = struct.unpack("<4sII", handle.read(12))
        if magic != b"glTF" or version != 2 or declared != current_size:
            raise SystemExit("fake-blender: exact-size input has an invalid header")
        handle.seek(8)
        handle.write(struct.pack("<I", size))
        handle.seek(0, os.SEEK_END)
        handle.write(struct.pack("<I4s", padding_size, b"PAD "))
        handle.truncate(size)


def _processing_arguments(argv: list[str]) -> argparse.Namespace:
    try:
        separator = argv.index("--")
    except ValueError as exc:
        raise SystemExit("fake-blender: missing -- argument separator") from exc

    parser = argparse.ArgumentParser(prog="fake-blender")
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--source-triangles", type=int, required=True)
    parser.add_argument("--target-triangles", type=int, required=True)
    parser.add_argument("--minimum-triangles", type=int, required=True)
    parser.add_argument("--maximum-triangles", type=int, required=True)
    parsed = parser.parse_args(argv[separator + 1 :])
    if parsed.source_triangles <= parsed.target_triangles:
        parser.error("source triangles must exceed target triangles")
    if not (
        0 < parsed.minimum_triangles
        <= parsed.target_triangles
        <= parsed.maximum_triangles
    ):
        parser.error("triangle policy is inconsistent")
    return parsed


def main(argv: list[str]) -> int:
    _reject_forbidden_environment()
    if "--version" in argv:
        _audit_environment("version")
        _audit_phase("version")
        version = os.environ.get("FAKE_BLENDER_VERSION", "5.1.2")
        build_hash = os.environ.get("FAKE_BLENDER_BUILD_HASH", "ec6e62d40fa9")
        banner_enabled = os.environ.get("FAKE_BLENDER_VERSION_BANNER") == "1"
        if banner_enabled:
            banner_version = os.environ.get("FAKE_BLENDER_BANNER_VERSION", version)
            banner_build_hash = os.environ.get(
                "FAKE_BLENDER_BANNER_BUILD_HASH", build_hash
            )
            print(
                f"Blender {banner_version} (hash {banner_build_hash} "
                "built 2026-05-19 01:30:33)"
            )
        print(f"Blender {version}")
        prefix = "\t" if banner_enabled else ""
        print(f"{prefix}build hash: {build_hash}")
        return 0

    _audit_environment("asset")
    _audit_phase("asset")
    args = _processing_arguments(argv)
    mode = os.environ.get("FAKE_BLENDER_MODE", "success")
    if mode not in _MODES:
        raise SystemExit(f"fake-blender: unsupported mode {mode!r}")

    swap_path_value = os.environ.get("FAKE_BLENDER_SWAP_PATH")
    swap_payload_value = os.environ.get("FAKE_BLENDER_SWAP_PAYLOAD_PATH")
    if (swap_path_value is None) != (swap_payload_value is None):
        raise SystemExit("fake-blender: incomplete source-swap controls")
    swap_path = Path(swap_path_value) if swap_path_value is not None else None
    original_bytes = swap_path.read_bytes() if swap_path is not None else None
    try:
        if swap_path is not None and swap_payload_value is not None:
            swap_path.write_bytes(Path(swap_payload_value).read_bytes())

        observation = _source_observation(args.source)
        log = os.environ.get("FAKE_BLENDER_LOG")
        if log:
            with Path(log).open("a", encoding="utf-8") as handle:
                handle.write(
                    json.dumps(
                        {
                            "argv": [str(value) for value in sys.argv],
                            "target": args.target_triangles,
                            "source_swap_performed": swap_path is not None,
                            **observation,
                        },
                        sort_keys=True,
                    )
                    + "\n"
                )

        if mode == "fail":
            return 17
        if mode == "malformed_output":
            args.output.write_bytes(b"not glTF")
            return 0

        triangles = {
            "over_budget": args.target_triangles + 1,
            "under_budget": args.target_triangles - 2_001,
        }.get(mode, args.target_triangles)
        requested_extension = os.environ.get("FAKE_BLENDER_OUTPUT_EXTENSION")
        requested_uri = os.environ.get("FAKE_BLENDER_OUTPUT_URI")
        extensions = (
            (requested_extension,)
            if requested_extension is not None
            else (("VENDOR_unreviewed",) if mode == "unsupported_extension" else ())
        )
        write_glb(
            args.output,
            triangles=triangles,
            include_uv=mode != "missing_uv",
            include_material=mode != "missing_material",
            include_image=mode != "missing_image",
            external_image=mode == "external_image" or requested_uri is not None,
            extensions=extensions,
            add_scene_content=mode == "unexpected_scene_content",
            translation=(100.0, 0.0, 0.0)
            if mode == "bounds_drift"
            else (0.0, 0.0, 0.0),
        )
        if requested_uri is not None:
            _replace_external_uri(args.output, requested_uri)
        requested_size = os.environ.get("FAKE_BLENDER_OUTPUT_SIZE")
        exact_size = os.environ.get("FAKE_BLENDER_OUTPUT_EXACT_SIZE")
        if requested_size is not None and exact_size is not None:
            raise SystemExit("fake-blender: output size controls are mutually exclusive")
        if exact_size is not None:
            _pad_glb_to_size(args.output, int(exact_size))
        if requested_size is not None:
            size = int(requested_size)
            if size < 0:
                raise SystemExit("fake-blender: output size must be non-negative")
            with args.output.open("r+b") as handle:
                handle.truncate(size)
        return 0
    finally:
        if swap_path is not None and original_bytes is not None:
            swap_path.write_bytes(original_bytes)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
