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
MAXIMUM_SELECTED_SCENE_WORK = 8_000_000
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


class _ArgumentParser(argparse.ArgumentParser):
    def error(self, message: str) -> None:
        raise RenderError(message)


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
    except BaseException as primary_error:
        cleanup_errors: list[OSError] = []
        if descriptor >= 0:
            try:
                os.close(descriptor)
            except OSError as exc:
                cleanup_errors.append(exc)
        if staged is not None:
            try:
                staged.unlink(missing_ok=True)
            except OSError as exc:
                cleanup_errors.append(exc)
        if cleanup_errors:
            raise RenderError(
                "PNG publication failed and temporary cleanup failed"
            ) from primary_error
        raise


def _source_snapshot(source: Path) -> tuple[bytes, os.stat_result]:
    """Read one immutable, regular, single-link source through one descriptor."""
    try:
        before_open = source.lstat()
    except FileNotFoundError as exc:
        raise RenderError("source GLB does not exist") from exc
    if not stat.S_ISREG(before_open.st_mode):
        raise RenderError("source GLB must be a regular file")
    if before_open.st_nlink != 1:
        raise RenderError("source GLB must have exactly one hard link")
    if before_open.st_size > MAXIMUM_SOURCE_BYTES:
        raise RenderError(
            f"source GLB exceeds {MAXIMUM_SOURCE_BYTES}-byte limit"
        )

    no_follow = getattr(os, "O_NOFOLLOW", None)
    if no_follow is None:
        raise RenderError("source no-follow reads are unavailable")
    flags = os.O_RDONLY | getattr(os, "O_NONBLOCK", 0)
    flags |= getattr(os, "O_CLOEXEC", 0) | no_follow
    descriptor = os.open(source, flags)
    try:
        opened = os.fstat(descriptor)
        if not stat.S_ISREG(opened.st_mode):
            raise RenderError("source GLB must be a regular file")
        if opened.st_nlink != 1:
            raise RenderError("source GLB must have exactly one hard link")
        if opened.st_size > MAXIMUM_SOURCE_BYTES:
            raise RenderError(
                f"source GLB exceeds {MAXIMUM_SOURCE_BYTES}-byte limit"
            )
        if (
            opened.st_dev,
            opened.st_ino,
            opened.st_size,
            opened.st_mtime_ns,
            opened.st_ctime_ns,
        ) != (
            before_open.st_dev,
            before_open.st_ino,
            before_open.st_size,
            before_open.st_mtime_ns,
            before_open.st_ctime_ns,
        ):
            raise RenderError("source GLB changed before it could be opened")

        with os.fdopen(descriptor, "rb", closefd=True) as handle:
            descriptor = -1
            data = handle.read(MAXIMUM_SOURCE_BYTES + 1)
            after_read = os.fstat(handle.fileno())
    finally:
        if descriptor >= 0:
            os.close(descriptor)

    if len(data) > MAXIMUM_SOURCE_BYTES:
        raise RenderError(
            f"source GLB exceeds {MAXIMUM_SOURCE_BYTES}-byte limit"
        )
    if len(data) != opened.st_size:
        raise RenderError("source GLB size changed while it was read")
    if (
        after_read.st_dev,
        after_read.st_ino,
        after_read.st_nlink,
        after_read.st_size,
        after_read.st_mtime_ns,
        after_read.st_ctime_ns,
    ) != (
        opened.st_dev,
        opened.st_ino,
        opened.st_nlink,
        opened.st_size,
        opened.st_mtime_ns,
        opened.st_ctime_ns,
    ):
        raise RenderError("source GLB changed while it was read")
    return data, opened


def _validate_output(
    source: Path, source_status: os.stat_result, output: Path
) -> None:
    if os.path.abspath(source) == os.path.abspath(output):
        raise RenderError("source and output refer to the same file")
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
    if (status.st_dev, status.st_ino) == (
        source_status.st_dev,
        source_status.st_ino,
    ):
        raise RenderError("source and output refer to the same file")


def _source_document(data: bytes) -> dict[str, object]:
    """Parse the already-captured bytes without reopening the source path."""
    offset = glb_metrics._validated_header(data)
    document: dict[str, object] | None = None
    chunk_number = 0
    while offset < len(data):
        if len(data) - offset < 8:
            raise GlbError("truncated GLB chunk header")
        length, kind = struct.unpack_from("<I4s", data, offset)
        if length % 4:
            raise GlbError("GLB chunk length is not four-byte aligned")
        offset += 8
        end = offset + length
        if end > len(data):
            raise GlbError("GLB chunk overruns file")
        if chunk_number == 0 and kind != b"JSON":
            raise GlbError("JSON must be the first GLB chunk")
        if kind == b"JSON":
            if document is not None:
                raise GlbError("duplicate JSON chunk")
            if length > glb_metrics.MAX_JSON_BYTES:
                raise GlbError(
                    "GLB JSON chunk exceeds limit "
                    f"{glb_metrics.MAX_JSON_BYTES} bytes"
                )
            decoded = glb_metrics._decode_json_bytes(
                data[offset:end].rstrip(b" ")
            )
            if not isinstance(decoded, dict):
                raise GlbError("GLB JSON root must be an object")
            document = decoded
        offset = end
        chunk_number += 1
    if document is None:
        raise GlbError("missing JSON chunk")
    return document


def _binary_chunk(data: bytes) -> bytes:
    chunks = [
        payload
        for kind, payload in glb_metrics._chunks(data)
        if kind == b"BIN\0"
    ]
    if len(chunks) != 1:
        raise GlbError("GLB must contain one embedded BIN chunk")
    return chunks[0]


def _selected_scene_node_indices(
    document: Mapping[str, object],
) -> Iterator[int]:
    """Traverse the selected scene cheaply while rejecting repeated nodes."""
    nodes = glb_metrics._root_array(document, "nodes")
    scenes = glb_metrics._root_array(document, "scenes")
    if not scenes:
        raise GlbError("GLB has no scenes")
    scene_index = glb_metrics._index(document.get("scene", 0), len(scenes), "scene")
    scene = glb_metrics._object(scenes[scene_index], f"scenes[{scene_index}]")
    roots = [
        glb_metrics._index(value, len(nodes), f"scenes[{scene_index}].nodes")
        for value in glb_metrics._array(
            scene.get("nodes", []), f"scenes[{scene_index}].nodes"
        )
    ]
    if len(set(roots)) != len(roots):
        raise GlbError(f"scenes[{scene_index}].nodes contains duplicates")

    active: set[int] = set()
    visited: set[int] = set()
    traversal: list[tuple[bool, int]] = [
        (True, root) for root in reversed(roots)
    ]
    while traversal:
        entering, node_index = traversal.pop()
        if not entering:
            active.remove(node_index)
            visited.add(node_index)
            continue
        if node_index in active:
            raise GlbError("selected scene graph contains a cycle")
        if node_index in visited:
            raise GlbError("selected scene graph references a node more than once")
        active.add(node_index)
        yield node_index
        node = glb_metrics._object(nodes[node_index], f"nodes[{node_index}]")
        children = [
            glb_metrics._index(
                child, len(nodes), f"nodes[{node_index}].children"
            )
            for child in glb_metrics._array(
                node.get("children", []), f"nodes[{node_index}].children"
            )
        ]
        if len(set(children)) != len(children):
            raise GlbError(f"nodes[{node_index}].children contains duplicates")
        traversal.append((False, node_index))
        traversal.extend((True, child) for child in reversed(children))


def _validate_selected_scene_work(document: Mapping[str, object]) -> None:
    """Reject aggregate decode work before strict geometry inspection."""
    accessors = glb_metrics._root_array(document, "accessors")
    meshes = glb_metrics._root_array(document, "meshes")
    nodes = glb_metrics._root_array(document, "nodes")
    mesh_work: list[int] = []
    for mesh_number, mesh_value in enumerate(meshes):
        mesh = glb_metrics._object(mesh_value, f"meshes[{mesh_number}]")
        primitives = glb_metrics._array(
            mesh.get("primitives"), f"meshes[{mesh_number}].primitives"
        )
        selected_work = 0
        for primitive_number, primitive_value in enumerate(primitives):
            label = f"meshes[{mesh_number}].primitives[{primitive_number}]"
            primitive = glb_metrics._object(primitive_value, label)
            attributes = glb_metrics._object(
                primitive.get("attributes"), f"{label}.attributes"
            )
            position_index = glb_metrics._index(
                attributes.get("POSITION"),
                len(accessors),
                f"{label}.attributes.POSITION",
            )
            position_accessor = glb_metrics._object(
                accessors[position_index], f"accessors[{position_index}]"
            )
            position_count = glb_metrics._integer(
                position_accessor.get("count"),
                f"accessors[{position_index}].count",
            )
            if "indices" in primitive:
                reference_index = glb_metrics._index(
                    primitive["indices"], len(accessors), f"{label}.indices"
                )
                reference_accessor = glb_metrics._object(
                    accessors[reference_index], f"accessors[{reference_index}]"
                )
                reference_count = glb_metrics._integer(
                    reference_accessor.get("count"),
                    f"accessors[{reference_index}].count",
                )
            else:
                reference_count = position_count
            selected_work += reference_count + position_count
        mesh_work.append(selected_work)

    selected_work = 0
    for node_index in _selected_scene_node_indices(document):
        node = glb_metrics._object(nodes[node_index], f"nodes[{node_index}]")
        mesh_value = node.get("mesh")
        if mesh_value is None:
            continue
        mesh_index = glb_metrics._index(
            mesh_value, len(meshes), f"nodes[{node_index}].mesh"
        )
        selected_work += mesh_work[mesh_index]
        if selected_work > MAXIMUM_SELECTED_SCENE_WORK:
            raise RenderError(
                "selected scene exceeds "
                f"{MAXIMUM_SELECTED_SCENE_WORK} combined index-reference "
                "and POSITION-value work limit"
            )


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
    document: dict[str, object], data: bytes
) -> Iterator[tuple[float, float, float]]:
    """Yield only vertices used by valid, non-degenerate surface triangles."""
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
                a = glb_metrics._transform_point(world, point_at(first))
                b = glb_metrics._transform_point(world, point_at(second))
                c = glb_metrics._transform_point(world, point_at(third))
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

    source_data, source_status = _source_snapshot(source)
    _validate_output(source, source_status, output)
    document = _source_document(source_data)
    _validate_selected_scene_work(document)
    # Run the strict inspector only after the cheap aggregate-work rejection.
    glb_metrics._inspect_document(document, source_data)

    yaw = math.radians(math.fmod(yaw_degrees, 360.0))
    cosine = math.cos(yaw)
    sine = math.sin(yaw)
    footprint = (2 * splat_radius + 1) ** 2
    maximum_by_work = MAXIMUM_RASTER_WORK // footprint
    maximum_positions = min(MAXIMUM_SURFACE_VERTICES, maximum_by_work)
    projected_positions: list[tuple[float, float, float]] = []
    for x, y, z in _surface_world_positions(document, source_data):
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


def _public_path(path: Path) -> str:
    raw = os.fspath(path)
    try:
        encoded = raw.encode("utf-8")
    except UnicodeError:
        return "[redacted]"
    if (
        not raw
        or not raw.isprintable()
        or len(encoded) > MAXIMUM_DIAGNOSTIC_BYTES
        or _CREDENTIAL_SHAPE.search(raw)
    ):
        return "[redacted]"
    return raw


def _success_payload(
    source: Path,
    output: Path,
    vertices: int,
    filled_pixels: int,
    coverage: float,
) -> bytes:
    def record(source_value: str, output_value: str) -> bytes:
        line = (
            f"{source_value} -> {output_value} "
            f"({vertices} vertices, {filled_pixels} filled pixels, "
            f"{coverage:.6f} coverage)"
        )
        return line.encode("utf-8") + b"\n"

    payload = record(_public_path(source), _public_path(output))
    if len(payload) > MAXIMUM_DIAGNOSTIC_BYTES:
        payload = record("[redacted]", "[redacted]")
    if len(payload) > MAXIMUM_DIAGNOSTIC_BYTES:
        raise AssertionError("internal success record exceeds public byte limit")
    return payload


def _emit_success(
    source: Path,
    output: Path,
    vertices: int,
    filled_pixels: int,
    coverage: float,
) -> None:
    payload = _success_payload(
        source,
        output,
        vertices,
        filled_pixels,
        coverage,
    )
    byte_stream = getattr(sys.stdout, "buffer", None)
    if byte_stream is not None:
        byte_stream.write(payload)
        byte_stream.flush()
    else:
        sys.stdout.write(payload.decode("utf-8"))
        sys.stdout.flush()


def _main(arguments: list[str]) -> int:
    parser = _ArgumentParser(
        description="Render a checked GLB scene as a depth-shaded silhouette"
    )
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("yaw", nargs="?", type=float, default=25.0)
    parser.add_argument("--size", type=int, default=DEFAULT_SIZE)
    parser.add_argument("--splat-radius", type=int, default=DEFAULT_SPLAT_RADIUS)
    parser.add_argument("--min-coverage", type=float, default=DEFAULT_MINIMUM_COVERAGE)
    try:
        args = parser.parse_args(arguments)
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
    _emit_success(
        args.source,
        args.output,
        vertices,
        filled_pixels,
        coverage,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(_main(sys.argv[1:]))
