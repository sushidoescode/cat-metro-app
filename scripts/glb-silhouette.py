#!/usr/bin/env python3
"""Render checked GLB scene geometry as a depth-shaded point silhouette."""

from __future__ import annotations

import sys

sys.dont_write_bytecode = True

import argparse
import math
import struct
import zlib
from pathlib import Path

from glb_metrics import GlbError, iter_world_positions


BACKGROUND = (250, 246, 236)
DEFAULT_SIZE = 520
DEFAULT_SPLAT_RADIUS = 2
DEFAULT_MINIMUM_COVERAGE = 0.01


class RenderError(ValueError):
    """Raised when the requested silhouette would not be valid evidence."""


def _png_chunk(kind: bytes, payload: bytes) -> bytes:
    checksum = zlib.crc32(kind + payload) & 0xFFFFFFFF
    return (
        struct.pack(">I", len(payload))
        + kind
        + payload
        + struct.pack(">I", checksum)
    )


def _write_png(path: Path, size: int, rgb: bytes) -> None:
    row_bytes = size * 3
    raw = b"".join(
        b"\0" + rgb[row * row_bytes:(row + 1) * row_bytes]
        for row in range(size)
    )
    header = struct.pack(">2I5B", size, size, 8, 2, 0, 0, 0)
    path.write_bytes(
        b"\x89PNG\r\n\x1a\n"
        + _png_chunk(b"IHDR", header)
        + _png_chunk(b"IDAT", zlib.compress(raw, 6))
        + _png_chunk(b"IEND", b"")
    )


def render(
    source: Path,
    output: Path,
    yaw_degrees: float = 25.0,
    *,
    size: int = DEFAULT_SIZE,
    splat_radius: int = DEFAULT_SPLAT_RADIUS,
    minimum_coverage: float = DEFAULT_MINIMUM_COVERAGE,
) -> tuple[int, int, float]:
    """Render *source* into *output* and return evidence density facts."""
    if size <= 0:
        raise RenderError("size must be positive")
    if splat_radius < 0:
        raise RenderError("splat radius cannot be negative")
    if not math.isfinite(minimum_coverage) or not 0.0 <= minimum_coverage <= 1.0:
        raise RenderError("minimum coverage must be between zero and one")
    if not math.isfinite(yaw_degrees):
        raise RenderError("yaw must be finite")

    yaw = math.radians(yaw_degrees)
    cosine = math.cos(yaw)
    sine = math.sin(yaw)
    projected_positions = [
        (x * cosine + z * sine, y, -x * sine + z * cosine)
        for x, y, z in iter_world_positions(source)
    ]
    if len(projected_positions) < 3:
        raise RenderError("at least three finite positions are required")
    if not all(
        math.isfinite(coordinate)
        for point in projected_positions
        for coordinate in point
    ):
        raise RenderError("positions must be finite")

    minimum_x = min(point[0] for point in projected_positions)
    maximum_x = max(point[0] for point in projected_positions)
    minimum_y = min(point[1] for point in projected_positions)
    maximum_y = max(point[1] for point in projected_positions)
    minimum_z = min(point[2] for point in projected_positions)
    maximum_z = max(point[2] for point in projected_positions)
    span = max(maximum_x - minimum_x, maximum_y - minimum_y)
    if span <= 0.0 or not math.isfinite(span):
        raise RenderError("projected positions have zero span")

    padding = size * 0.08
    scale = (size - 2.0 * padding) / span
    depth: list[float | None] = [None] * (size * size)
    radius_squared = splat_radius * splat_radius
    for x, y, z in projected_positions:
        px = round(padding + (x - minimum_x) * scale)
        py = round(size - padding - (y - minimum_y) * scale)
        for delta_y in range(-splat_radius, splat_radius + 1):
            for delta_x in range(-splat_radius, splat_radius + 1):
                if delta_x * delta_x + delta_y * delta_y > radius_squared:
                    continue
                target_x, target_y = px + delta_x, py + delta_y
                if 0 <= target_x < size and 0 <= target_y < size:
                    offset = target_y * size + target_x
                    if depth[offset] is None or z > depth[offset]:
                        depth[offset] = z
    filled_pixels = sum(value is not None for value in depth)
    coverage = filled_pixels / (size * size)
    if coverage < minimum_coverage:
        raise RenderError(f"coverage {coverage:.6f} below {minimum_coverage:.6f}")

    depth_span = maximum_z - minimum_z
    if depth_span == 0.0:
        depth_span = 1.0
    pixels = bytearray()
    for value in depth:
        if value is None:
            pixels.extend(BACKGROUND)
            continue
        nearness = (value - minimum_z) / depth_span
        shade = int(48 + 190 * nearness)
        pixels.extend(
            (
                min(255, int(shade * 1.02)),
                int(shade * 0.94),
                int(shade * 0.86),
            )
        )
    _write_png(output, size, bytes(pixels))
    return len(projected_positions), filled_pixels, coverage


def _main(arguments: list[str]) -> int:
    parser = argparse.ArgumentParser(
        description="Render a checked GLB scene as a depth-shaded silhouette"
    )
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("yaw", nargs="?", type=float, default=25.0)
    parser.add_argument("--size", type=int, default=DEFAULT_SIZE)
    parser.add_argument("--splat-radius", type=int, default=DEFAULT_SPLAT_RADIUS)
    parser.add_argument("--min-coverage", type=float, default=DEFAULT_MINIMUM_COVERAGE)
    args = parser.parse_args(arguments)
    try:
        vertices, filled_pixels, coverage = render(
            args.source,
            args.output,
            args.yaw,
            size=args.size,
            splat_radius=args.splat_radius,
            minimum_coverage=args.min_coverage,
        )
    except (GlbError, RenderError, OSError, struct.error) as exc:
        print(f"glb-silhouette: {exc}", file=sys.stderr)
        return 1
    print(
        f"{args.source} -> {args.output} "
        f"({vertices} vertices, {filled_pixels} filled pixels, {coverage:.6f} coverage)"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(_main(sys.argv[1:]))
