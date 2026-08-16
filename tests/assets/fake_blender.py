#!/usr/bin/env python3
"""Deterministic Blender process double for the GLB decimation contract."""

from __future__ import annotations

import argparse
import json
import os
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
        _audit_phase("version")
        version = os.environ.get("FAKE_BLENDER_VERSION", "5.1.2")
        build_hash = os.environ.get("FAKE_BLENDER_BUILD_HASH", "ec6e62d40fa9")
        banner_enabled = os.environ.get("FAKE_BLENDER_VERSION_BANNER") == "1"
        if banner_enabled:
            print(
                f"Blender {version} (hash {build_hash} "
                "built 2026-05-19 01:30:33)"
            )
        print(f"Blender {version}")
        prefix = "\t" if banner_enabled else ""
        print(f"{prefix}build hash: {build_hash}")
        return 0

    _audit_phase("asset")
    args = _processing_arguments(argv)
    mode = os.environ.get("FAKE_BLENDER_MODE", "success")
    if mode not in _MODES:
        raise SystemExit(f"fake-blender: unsupported mode {mode!r}")

    log = os.environ.get("FAKE_BLENDER_LOG")
    if log:
        with Path(log).open("a", encoding="utf-8") as handle:
            handle.write(
                json.dumps(
                    {"argv": [str(value) for value in sys.argv], "target": args.target_triangles},
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
    write_glb(
        args.output,
        triangles=triangles,
        include_uv=mode != "missing_uv",
        include_material=mode != "missing_material",
        include_image=mode != "missing_image",
        external_image=mode == "external_image",
        extensions=("VENDOR_unreviewed",)
        if mode == "unsupported_extension"
        else (),
        add_scene_content=mode == "unexpected_scene_content",
        translation=(100.0, 0.0, 0.0)
        if mode == "bounds_drift"
        else (0.0, 0.0, 0.0),
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
