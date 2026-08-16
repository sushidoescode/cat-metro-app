#!/usr/bin/env python3
"""Render checked GLB scene geometry as a depth-shaded point silhouette."""

from __future__ import annotations

import sys

sys.dont_write_bytecode = True

import argparse
import math
import os
import re
import stat
import struct
import tempfile
import zlib
from collections.abc import Iterator, Mapping
from pathlib import Path

import glb_metrics
from glb_metrics import GlbError


BACKGROUND = (250, 246, 236)
DEFAULT_SIZE = 520
DEFAULT_SPLAT_RADIUS = 2
DEFAULT_MINIMUM_COVERAGE = 0.01
MAXIMUM_SOURCE_BYTES = 512 * 1024 * 1024
MAXIMUM_SIZE = 2048
MAXIMUM_SPLAT_RADIUS = 64
MAXIMUM_RASTER_WORK = 100_000_000
MAXIMUM_SURFACE_VERTICES = 2_000_000
MAXIMUM_DIAGNOSTIC_BYTES = 512

_CREDENTIAL_SHAPE = re.compile(
    r"api[_ -]?key|token|secret|authorization|credential|bearer|https?://",
    re.IGNORECASE,
)


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


def _png_bytes(size: int, rgb: bytes) -> bytes:
    row_bytes = size * 3
    raw = b"".join(
        b"\0" + rgb[row * row_bytes:(row + 1) * row_bytes]
        for row in range(size)
    )
    header = struct.pack(">2I5B", size, size, 8, 2, 0, 0, 0)
    return (
        b"\x89PNG\r\n\x1a\n"
        + _png_chunk(b"IHDR", header)
        + _png_chunk(b"IDAT", zlib.compress(raw, 6))
        + _png_chunk(b"IEND", b"")
    )


def _write_png(path: Path, size: int, rgb: bytes) -> None:
    """Publish one complete PNG without ever writing through *path*."""
    payload = _png_bytes(size, rgb)
    descriptor = -1
    staged: Path | None = None
    try:
        descriptor, staged_name = tempfile.mkstemp(
            prefix=".glb-silhouette-",
            suffix=".tmp",
            dir=path.parent,
        )
        staged = Path(staged_name)
        with os.fdopen(descriptor, "wb", closefd=True) as handle:
            descriptor = -1
            handle.write(payload)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(staged, path)
        staged = None
    finally:
        if descriptor >= 0:
            os.close(descriptor)
        if staged is not None:
            try:
                staged.unlink(missing_ok=True)
            except OSError:
                pass


def _source_status(source: Path) -> os.stat_result:
    try:
        status = source.stat()
    except FileNotFoundError as exc:
        raise RenderError("source GLB does not exist") from exc
    if not stat.S_ISREG(status.st_mode):
        raise RenderError("source GLB must be a regular file")
    if status.st_size > MAXIMUM_SOURCE_BYTES:
        raise RenderError(
            f"source GLB exceeds {MAXIMUM_SOURCE_BYTES}-byte limit"
        )
    return status


def _validate_output(output: Path) -> None:
    try:
        status = output.lstat()
    except FileNotFoundError:
        return
    if stat.S_ISLNK(status.st_mode):
        raise RenderError("output path cannot be a symbolic link")
    if not stat.S_ISREG(status.st_mode):
        raise RenderError("output path must be a regular file when it exists")
    if status.st_nlink != 1:
        raise RenderError("output path must have exactly one hard link")


def _source_is_output(source: Path, output: Path) -> bool:
    if os.path.abspath(source) == os.path.abspath(output):
        return True
    try:
        return source.samefile(output)
    except (FileNotFoundError, OSError):
        return False


def _binary_chunk(data: bytes) -> bytes:
    chunks = [
        payload
        for kind, payload in glb_metrics._chunks(data)
        if kind == b"BIN\0"
    ]
    if len(chunks) != 1:
        raise GlbError("GLB must contain one embedded BIN chunk")
    return chunks[0]


def _scene_nodes(
    document: Mapping[str, object],
) -> Iterator[tuple[Mapping[str, object], tuple[float, ...]]]:
    nodes = glb_metrics._root_array(document, "nodes")
    scenes = glb_metrics._root_array(document, "scenes")
    scene_index = glb_metrics._index(document.get("scene", 0), len(scenes), "scene")
    scene = glb_metrics._object(scenes[scene_index], f"scenes[{scene_index}]")
    roots = [
        glb_metrics._index(value, len(nodes), f"scenes[{scene_index}].nodes")
        for value in glb_metrics._array(
            scene.get("nodes", []), f"scenes[{scene_index}].nodes"
        )
    ]
    traversal: list[tuple[int, tuple[float, ...]]] = [
        (root, glb_metrics._IDENTITY) for root in reversed(roots)
    ]
    while traversal:
        node_index, parent = traversal.pop()
        node = glb_metrics._object(nodes[node_index], f"nodes[{node_index}]")
        world = glb_metrics._matrix_multiply(
            parent,
            glb_metrics._node_matrix(node, f"nodes[{node_index}]"),
        )
        yield node, world
        children = glb_metrics._array(
            node.get("children", []), f"nodes[{node_index}].children"
        )
        traversal.extend(
            (
                glb_metrics._index(
                    child, len(nodes), f"nodes[{node_index}].children"
                ),
                world,
            )
            for child in reversed(children)
        )


def _accessor_layout(
    accessors: list[object],
    views: list[object],
    accessor_index: int,
    label: str,
) -> tuple[Mapping[str, object], int, int, int, int | None]:
    accessor = glb_metrics._object(accessors[accessor_index], label)
    view_index = glb_metrics._index(
        accessor.get("bufferView"), len(views), f"{label}.bufferView"
    )
    view = glb_metrics._object(views[view_index], f"{label}.bufferView")
    view_offset = glb_metrics._integer(
        view.get("byteOffset", 0), f"{label}.bufferView.byteOffset"
    )
    accessor_offset = glb_metrics._integer(
        accessor.get("byteOffset", 0), f"{label}.byteOffset"
    )
    count = glb_metrics._integer(accessor.get("count"), f"{label}.count")
    stride_value = view.get("byteStride")
    stride = (
        None
        if stride_value is None
        else glb_metrics._integer(stride_value, f"{label}.bufferView.byteStride")
    )
    return accessor, view_offset + accessor_offset, count, view_index, stride


def _triangle_faces(
    indices: Iterator[int], count: int, mode: int
) -> Iterator[tuple[int, int, int]]:
    if mode == 4:
        for _ in range(count // 3):
            yield next(indices), next(indices), next(indices)
        return
    if count < 3:
        return
    first = next(indices)
    second = next(indices)
    if mode == 5:
        previous_previous, previous = first, second
        for current in indices:
            yield previous_previous, previous, current
            previous_previous, previous = previous, current
        return
    previous = second
    for current in indices:
        yield first, previous, current
        previous = current


def _surface_world_positions(
    source: Path,
) -> Iterator[tuple[float, float, float]]:
    """Yield only vertices used by valid, non-degenerate surface triangles."""
    document, data = glb_metrics._read_glb(source)
    # Keep the metrics inspector as the single strict validation authority.
    glb_metrics._inspect_document(document, data)
    binary = _binary_chunk(data)
    accessors = glb_metrics._root_array(document, "accessors")
    views = glb_metrics._root_array(document, "bufferViews")
    meshes = glb_metrics._root_array(document, "meshes")

    for node, world in _scene_nodes(document):
        mesh_value = node.get("mesh")
        if mesh_value is None:
            continue
        mesh_index = glb_metrics._index(mesh_value, len(meshes), "node.mesh")
        mesh = glb_metrics._object(meshes[mesh_index], f"meshes[{mesh_index}]")
        primitives = glb_metrics._array(
            mesh.get("primitives"), f"meshes[{mesh_index}].primitives"
        )
        for primitive_index, primitive_value in enumerate(primitives):
            label = f"meshes[{mesh_index}].primitives[{primitive_index}]"
            primitive = glb_metrics._object(primitive_value, label)
            attributes = glb_metrics._object(
                primitive.get("attributes"), f"{label}.attributes"
            )
            position_index = glb_metrics._index(
                attributes.get("POSITION"), len(accessors), f"{label}.POSITION"
            )
            position, position_start, position_count, _, position_stride = (
                _accessor_layout(
                    accessors, views, position_index, f"{label}.POSITION"
                )
            )
            if position.get("componentType") != 5126 or position.get("type") != "VEC3":
                raise GlbError(f"{label}.POSITION must use a FLOAT VEC3 accessor")
            position_step = position_stride or 12

            def point_at(index: int) -> tuple[float, float, float]:
                return struct.unpack_from(
                    "<3f", binary, position_start + index * position_step
                )

            if "indices" in primitive:
                index_number = glb_metrics._index(
                    primitive["indices"], len(accessors), f"{label}.indices"
                )
                index, index_start, element_count, _, index_stride = (
                    _accessor_layout(
                        accessors, views, index_number, f"{label}.indices"
                    )
                )
                component_type = index.get("componentType")
                component = {5121: ("<B", 1), 5123: ("<H", 2), 5125: ("<I", 4)}[
                    component_type
                ]
                index_step = index_stride or component[1]

                def indices() -> Iterator[int]:
                    for number in range(element_count):
                        yield struct.unpack_from(
                            component[0], binary, index_start + number * index_step
                        )[0]

                index_values = indices()
            else:
                element_count = position_count
                index_values = iter(range(position_count))

            mode = glb_metrics._integer(primitive.get("mode", 4), f"{label}.mode")
            referenced = bytearray(position_count)
            for first, second, third in _triangle_faces(
                index_values, element_count, mode
            ):
                if first == second or second == third or first == third:
                    continue
                a = point_at(first)
                b = point_at(second)
                c = point_at(third)
                ab = tuple(b[axis] - a[axis] for axis in range(3))
                ac = tuple(c[axis] - a[axis] for axis in range(3))
                cross = (
                    ab[1] * ac[2] - ab[2] * ac[1],
                    ab[2] * ac[0] - ab[0] * ac[2],
                    ab[0] * ac[1] - ab[1] * ac[0],
                )
                if cross == (0.0, 0.0, 0.0):
                    continue
                referenced[first] = 1
                referenced[second] = 1
                referenced[third] = 1
            for index, is_referenced in enumerate(referenced):
                if is_referenced:
                    yield glb_metrics._transform_point(world, point_at(index))


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
    _source_status(source)
    if _source_is_output(source, output):
        raise RenderError("source and output refer to the same file")
    _validate_output(output)
    if not 1 <= size <= MAXIMUM_SIZE:
        raise RenderError(f"size must be between 1 and {MAXIMUM_SIZE}")
    if not 0 <= splat_radius <= MAXIMUM_SPLAT_RADIUS:
        raise RenderError(
            f"splat radius must be between 0 and {MAXIMUM_SPLAT_RADIUS}"
        )
    if not math.isfinite(minimum_coverage) or not 0.0 <= minimum_coverage <= 1.0:
        raise RenderError("minimum coverage must be between zero and one")
    if not math.isfinite(yaw_degrees):
        raise RenderError("yaw must be finite")

    yaw = math.radians(math.fmod(yaw_degrees, 360.0))
    cosine = math.cos(yaw)
    sine = math.sin(yaw)
    footprint = (2 * splat_radius + 1) ** 2
    maximum_by_work = MAXIMUM_RASTER_WORK // footprint
    maximum_positions = min(MAXIMUM_SURFACE_VERTICES, maximum_by_work)
    projected_positions: list[tuple[float, float, float]] = []
    for x, y, z in _surface_world_positions(source):
        if len(projected_positions) >= maximum_positions:
            raise RenderError(
                f"raster work exceeds {MAXIMUM_RASTER_WORK}-sample limit"
            )
        projected_positions.append(
            (x * cosine + z * sine, y, -x * sine + z * cosine)
        )
    if len(projected_positions) < 3:
        raise RenderError(
            "at least three referenced non-degenerate surface positions are required"
        )
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


def _diagnostic_payload(message: object) -> bytes:
    raw = str(message) or "rendering failed"
    if _CREDENTIAL_SHAPE.search(raw):
        raw = "input rejected (details redacted)"
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
    prefix = b"glb-silhouette: "
    body_budget = MAXIMUM_DIAGNOSTIC_BYTES - len(prefix) - 1
    body = "".join(printable).encode("utf-8")
    if len(body) > body_budget:
        body = body[:body_budget - 3].decode("utf-8", "ignore").encode("utf-8")
        body += b"..."
    return prefix + body + b"\n"


def _emit_diagnostic(message: object) -> None:
    payload = _diagnostic_payload(message)
    byte_stream = getattr(sys.stderr, "buffer", None)
    if byte_stream is not None:
        byte_stream.write(payload)
        byte_stream.flush()
    else:
        sys.stderr.write(payload.decode("utf-8"))
        sys.stderr.flush()


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
    except (
        GlbError,
        RenderError,
        OSError,
        struct.error,
        UnicodeError,
        ValueError,
        OverflowError,
        MemoryError,
        RecursionError,
    ) as exc:
        _emit_diagnostic(exc)
        return 1
    print(
        f"{args.source} -> {args.output} "
        f"({vertices} vertices, {filled_pixels} filled pixels, {coverage:.6f} coverage)"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(_main(sys.argv[1:]))
