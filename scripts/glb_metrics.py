#!/usr/bin/env python3
"""Strict, standard-library GLB inspection and preservation checks."""

from __future__ import annotations

import base64
import binascii
import hashlib
import json
import math
import os
import re
import stat
import struct
import sys
from collections import Counter
from collections.abc import Callable, Iterator, Mapping
from pathlib import Path


class GlbError(ValueError):
    """Raised when a GLB is malformed or outside the supported profile."""


METRIC_KEYS = (
    "path", "sha256", "bytes", "meshes", "primitives", "vertices",
    "triangles", "referenced_vertices", "unique_triangles",
    "degenerate_triangles", "materials", "material_primitives", "images",
    "embedded_images", "image_payload_sha256", "material_texture_bindings",
    "uv_primitives", "animations", "cameras", "lights", "skins",
    "morph_targets", "external_uris", "extensions_used",
    "extensions_required", "world_bounds",
)

DISALLOWED_EXTENSIONS = {"EXT_meshopt_compression", "KHR_draco_mesh_compression"}
ALLOWED_OUTPUT_EXTENSIONS = frozenset()
CENTER_DRIFT_MAX = 0.005
SCALE_DRIFT_MAX = 0.01
NORMALIZED_EXTENT_DRIFT_MAX = 0.02
MAX_DOCUMENT_NESTING = 256
MAX_GLB_BYTES = 128 * 1024 * 1024
MAX_JSON_BYTES = 16 * 1024 * 1024
MAX_ACCESSORS = 65_536
MAX_ACCESSOR_COUNT = 8_000_000
MAX_IMAGE_BYTES = 8 * 1024 * 1024
MAX_GEOMETRY_WORK = 8_000_000
MAX_SPARSE_ACCESSOR_WORK = 8_000_000
MAX_IMAGE_WORK_BYTES = 64 * 1024 * 1024
MAX_JSON_INTEGER_DIGITS = 4_300
MAX_JSON_NUMBER_CHARACTERS = 4_300
MAX_DIAGNOSTIC_BYTES = 512

_CREDENTIAL_SHAPE = re.compile(
    r"api[_ -]?key|token|secret|authorization|credential|bearer|https?://",
    re.IGNORECASE,
)

_COMPONENT_SIZES = {
    5120: 1,
    5121: 1,
    5122: 2,
    5123: 2,
    5125: 4,
    5126: 4,
}
_TYPE_COMPONENTS = {
    "SCALAR": 1,
    "VEC2": 2,
    "VEC3": 3,
    "VEC4": 4,
    "MAT2": 4,
    "MAT3": 9,
    "MAT4": 16,
}
_IDENTITY = (
    1.0, 0.0, 0.0, 0.0,
    0.0, 1.0, 0.0, 0.0,
    0.0, 0.0, 1.0, 0.0,
    0.0, 0.0, 0.0, 1.0,
)


def _validated_header(data: bytes) -> int:
    if len(data) < 20:
        raise GlbError("truncated GLB")
    magic, version, declared = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF" or version != 2 or declared != len(data):
        raise GlbError("invalid GLB header")
    return 12


def _reject_json_constant(value: str) -> object:
    raise GlbError(f"invalid non-finite JSON number {value}")


def _reject_duplicate_keys(
    pairs: list[tuple[str, object]],
) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            raise GlbError(f"duplicate JSON object key {key!r}")
        result[key] = value
    return result


def _parse_json_integer(value: str) -> int:
    digits = value[1:] if value.startswith("-") else value
    if len(digits) > MAX_JSON_INTEGER_DIGITS:
        raise GlbError(
            f"JSON integer exceeds {MAX_JSON_INTEGER_DIGITS}-digit limit"
        )
    number = 0
    for offset in range(0, len(digits), 9):
        block = digits[offset:offset + 9]
        number = number * (10 ** len(block)) + int(block)
    return -number if value.startswith("-") else number


def _parse_json_float(value: str) -> float:
    if len(value) > MAX_JSON_NUMBER_CHARACTERS:
        raise GlbError(
            "JSON number exceeds "
            f"{MAX_JSON_NUMBER_CHARACTERS}-character limit"
        )
    number = float(value)
    if not math.isfinite(number):
        raise GlbError("invalid non-finite JSON number")
    return number


def _decode_json_bytes(payload: bytes) -> object:
    """Decode strict bounded JSON independently of interpreter defaults."""
    try:
        return json.loads(
            payload.decode("utf-8"),
            parse_constant=_reject_json_constant,
            parse_int=_parse_json_integer,
            parse_float=_parse_json_float,
            object_pairs_hook=_reject_duplicate_keys,
        )
    except GlbError:
        raise
    except (UnicodeDecodeError, json.JSONDecodeError, ValueError) as exc:
        raise GlbError(f"invalid GLB JSON: {exc}") from exc
    except (RecursionError, MemoryError, OverflowError) as exc:
        raise GlbError("GLB JSON exceeds parser resource limits") from exc


def _read_glb(path: Path) -> tuple[dict[str, object], bytes]:
    before_open = path.stat()
    if not stat.S_ISREG(before_open.st_mode):
        raise GlbError("GLB source must be a regular file")
    if before_open.st_size > MAX_GLB_BYTES:
        raise GlbError(f"GLB file exceeds limit {MAX_GLB_BYTES} bytes")

    flags = os.O_RDONLY | getattr(os, "O_NONBLOCK", 0)
    flags |= getattr(os, "O_CLOEXEC", 0)
    descriptor = os.open(path, flags)
    try:
        opened = os.fstat(descriptor)
        if not stat.S_ISREG(opened.st_mode):
            raise GlbError("GLB source must be a regular file")
        if opened.st_size > MAX_GLB_BYTES:
            raise GlbError(f"GLB file exceeds limit {MAX_GLB_BYTES} bytes")
        with os.fdopen(descriptor, "rb", closefd=True) as handle:
            descriptor = -1
            data = handle.read(MAX_GLB_BYTES + 1)
    finally:
        if descriptor >= 0:
            os.close(descriptor)
    if len(data) > MAX_GLB_BYTES:
        raise GlbError(f"GLB file exceeds limit {MAX_GLB_BYTES} bytes")
    offset = _validated_header(data)
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
            if length > MAX_JSON_BYTES:
                raise GlbError(
                    f"GLB JSON chunk exceeds limit {MAX_JSON_BYTES} bytes"
                )
            decoded = _decode_json_bytes(data[offset:end].rstrip(b" "))
            if not isinstance(decoded, dict):
                raise GlbError("GLB JSON root must be an object")
            document = decoded
        offset = end
        chunk_number += 1
    if document is None:
        raise GlbError("missing JSON chunk")
    return document, data


def _chunks(data: bytes) -> list[tuple[bytes, bytes]]:
    offset = _validated_header(data)
    result: list[tuple[bytes, bytes]] = []
    while offset < len(data):
        if len(data) - offset < 8:
            raise GlbError("truncated GLB chunk header")
        length, kind = struct.unpack_from("<I4s", data, offset)
        offset += 8
        end = offset + length
        if end > len(data):
            raise GlbError("GLB chunk overruns file")
        result.append((kind, data[offset:end]))
        offset = end
    return result


def _object(value: object, label: str) -> dict[str, object]:
    if not isinstance(value, dict):
        raise GlbError(f"{label} must be an object")
    return value


def _array(value: object, label: str) -> list[object]:
    if not isinstance(value, list):
        raise GlbError(f"{label} must be an array")
    return value


def _root_array(document: Mapping[str, object], name: str) -> list[object]:
    value = document.get(name, [])
    return _array(value, name)


def _integer(value: object, label: str, *, minimum: int = 0) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < minimum:
        raise GlbError(f"{label} must be an integer >= {minimum}")
    return value


def _index(value: object, size: int, label: str) -> int:
    index = _integer(value, label)
    if index >= size:
        raise GlbError(f"{label} is out of range")
    return index


def _finite_vector(value: object, length: int, label: str) -> list[float]:
    values = _array(value, label)
    if len(values) != length:
        raise GlbError(f"{label} must contain {length} numbers")
    result: list[float] = []
    for item in values:
        if isinstance(item, bool) or not isinstance(item, (int, float)):
            raise GlbError(f"{label} must contain numbers")
        try:
            number = float(item)
        except OverflowError as exc:
            raise GlbError(f"{label} contains an out-of-range number") from exc
        if not math.isfinite(number):
            raise GlbError(f"{label} contains a non-finite number")
        result.append(number)
    return result


def _extension_names(document: Mapping[str, object], name: str) -> list[str]:
    raw = _root_array(document, name)
    result: set[str] = set()
    for value in raw:
        if not isinstance(value, str) or not value:
            raise GlbError(f"{name} entries must be non-empty strings")
        if value in result:
            raise GlbError(f"{name} contains duplicate {value}")
        result.add(value)
    return sorted(result)


def _validate_document_nesting(document: Mapping[str, object]) -> None:
    pending: list[tuple[object, int]] = [(document, 0)]
    while pending:
        value, depth = pending.pop()
        if depth > MAX_DOCUMENT_NESTING:
            raise GlbError(
                f"GLB JSON nesting exceeds limit {MAX_DOCUMENT_NESTING}"
            )
        if isinstance(value, dict):
            pending.extend((child, depth + 1) for child in value.values())
        elif isinstance(value, list):
            pending.extend((child, depth + 1) for child in value)


_ROOT_PROPERTY_ARRAYS = {
    "accessors": "accessor",
    "animations": "animation",
    "buffers": "property",
    "bufferViews": "property",
    "cameras": "camera",
    "images": "property",
    "materials": "material",
    "meshes": "mesh",
    "nodes": "property",
    "samplers": "property",
    "scenes": "property",
    "skins": "property",
    "textures": "property",
}

_PROPERTY_OBJECT_CHILDREN = {
    "document": (("asset", "property"),),
    "accessor": (("sparse", "accessor_sparse"),),
    "accessor_sparse": (
        ("indices", "property"),
        ("values", "property"),
    ),
    "animation_channel": (("target", "property"),),
    "camera": (
        ("orthographic", "property"),
        ("perspective", "property"),
    ),
    "light": (("spot", "property"),),
    "material": (
        ("pbrMetallicRoughness", "material_pbr"),
        ("normalTexture", "property"),
        ("occlusionTexture", "property"),
        ("emissiveTexture", "property"),
    ),
    "material_pbr": (
        ("baseColorTexture", "property"),
        ("metallicRoughnessTexture", "property"),
    ),
}

_PROPERTY_ARRAY_CHILDREN = {
    "document": tuple(_ROOT_PROPERTY_ARRAYS.items()),
    "animation": (
        ("channels", "animation_channel"),
        ("samplers", "property"),
    ),
    "mesh": (("primitives", "property"),),
}


def _extension_payload_names(document: Mapping[str, object]) -> set[str]:
    payload_names: set[str] = set()
    pending: list[tuple[object, str, int]] = [(document, "document", 0)]
    while pending:
        value, kind, depth = pending.pop()
        if depth > MAX_DOCUMENT_NESTING:
            raise GlbError(
                f"glTF property nesting exceeds limit {MAX_DOCUMENT_NESTING}"
            )
        property_object = _object(value, f"{kind} property")
        if "extensions" in property_object:
            extensions = _object(property_object["extensions"], "extensions")
            for name, payload in extensions.items():
                if not isinstance(name, str) or not name:
                    raise GlbError("extensions names must be non-empty strings")
                payload_names.add(name)
                if kind == "document" and name == "KHR_lights_punctual":
                    light_extension = _object(payload, "KHR_lights_punctual payload")
                    if "lights" in light_extension:
                        lights = _array(
                            light_extension["lights"],
                            "KHR_lights_punctual.lights",
                        )
                        pending.extend(
                            (light, "light", depth + 1) for light in lights
                        )
        for child_name, child_kind in _PROPERTY_OBJECT_CHILDREN.get(kind, ()):
            if child_name in property_object:
                pending.append(
                    (property_object[child_name], child_kind, depth + 1)
                )
        for child_name, child_kind in _PROPERTY_ARRAY_CHILDREN.get(kind, ()):
            if child_name in property_object:
                children = _array(property_object[child_name], child_name)
                pending.extend(
                    (child, child_kind, depth + 1) for child in children
                )
    return payload_names


def _matrix_multiply(left: tuple[float, ...], right: tuple[float, ...]) -> tuple[float, ...]:
    return tuple(
        sum(left[k * 4 + row] * right[column * 4 + k] for k in range(4))
        for column in range(4)
        for row in range(4)
    )


def _node_matrix(node: Mapping[str, object], label: str) -> tuple[float, ...]:
    has_matrix = "matrix" in node
    has_trs = any(name in node for name in ("translation", "rotation", "scale"))
    if has_matrix and has_trs:
        raise GlbError(f"{label} cannot contain both matrix and TRS")
    if has_matrix:
        return tuple(_finite_vector(node["matrix"], 16, f"{label}.matrix"))

    translation = _finite_vector(node.get("translation", [0.0, 0.0, 0.0]), 3, f"{label}.translation")
    rotation = _finite_vector(node.get("rotation", [0.0, 0.0, 0.0, 1.0]), 4, f"{label}.rotation")
    scale = _finite_vector(node.get("scale", [1.0, 1.0, 1.0]), 3, f"{label}.scale")
    x, y, z, w = rotation
    norm = math.sqrt(x * x + y * y + z * z + w * w)
    if norm == 0.0:
        raise GlbError(f"{label}.rotation has zero length")
    x, y, z, w = (value / norm for value in (x, y, z, w))
    sx, sy, sz = scale
    tx, ty, tz = translation
    matrix = (
        (1.0 - 2.0 * (y * y + z * z)) * sx,
        (2.0 * (x * y + z * w)) * sx,
        (2.0 * (x * z - y * w)) * sx,
        0.0,
        (2.0 * (x * y - z * w)) * sy,
        (1.0 - 2.0 * (x * x + z * z)) * sy,
        (2.0 * (y * z + x * w)) * sy,
        0.0,
        (2.0 * (x * z + y * w)) * sz,
        (2.0 * (y * z - x * w)) * sz,
        (1.0 - 2.0 * (x * x + y * y)) * sz,
        0.0,
        tx, ty, tz, 1.0,
    )
    if not all(math.isfinite(value) for value in matrix):
        raise GlbError(f"{label} produces a non-finite transform")
    return matrix


def _transform_point(matrix: tuple[float, ...], point: tuple[float, float, float]) -> tuple[float, float, float]:
    x, y, z = point
    transformed = (
        matrix[0] * x + matrix[4] * y + matrix[8] * z + matrix[12],
        matrix[1] * x + matrix[5] * y + matrix[9] * z + matrix[13],
        matrix[2] * x + matrix[6] * y + matrix[10] * z + matrix[14],
    )
    if not all(math.isfinite(value) for value in transformed):
        raise GlbError("world transform produced a non-finite position")
    return transformed


def _validate_sparse(
    sparse_value: object,
    accessor_count: int,
    validated_views: list[tuple[int, int, int, int | None]],
    component_size: int,
    element_size: int,
    label: str,
    binary: bytes | None,
    buffer_embedded: list[bool],
) -> None:
    sparse = _object(sparse_value, label)
    count = _integer(sparse.get("count"), f"{label}.count", minimum=1)
    if count > accessor_count:
        raise GlbError(f"{label}.count exceeds accessor count")
    indices = _object(sparse.get("indices"), f"{label}.indices")
    values = _object(sparse.get("values"), f"{label}.values")
    indices_view = _index(indices.get("bufferView"), len(validated_views), f"{label}.indices.bufferView")
    component_type = _integer(indices.get("componentType"), f"{label}.indices.componentType")
    if component_type not in (5121, 5123, 5125):
        raise GlbError(f"{label}.indices has unsupported component type")
    indices_offset = _integer(indices.get("byteOffset", 0), f"{label}.indices.byteOffset")
    values_view = _index(values.get("bufferView"), len(validated_views), f"{label}.values.bufferView")
    values_offset = _integer(values.get("byteOffset", 0), f"{label}.values.byteOffset")
    index_component_size = _COMPONENT_SIZES[component_type]
    if indices_offset % index_component_size:
        raise GlbError(f"{label}.indices.byteOffset is misaligned")
    if validated_views[indices_view][3] is not None or validated_views[values_view][3] is not None:
        raise GlbError(f"{label} bufferViews cannot have byteStride")
    if indices_offset + count * index_component_size > validated_views[indices_view][2]:
        raise GlbError(f"{label}.indices overruns its bufferView")
    if values_offset + count * element_size > validated_views[values_view][2]:
        raise GlbError(f"{label}.values overruns its bufferView")
    if (validated_views[indices_view][1] + indices_offset) % index_component_size:
        raise GlbError(f"{label}.indices has a misaligned effective offset")
    if (validated_views[values_view][1] + values_offset) % component_size:
        raise GlbError(f"{label}.values has a misaligned effective offset")
    indices_buffer, indices_view_offset, _, _ = validated_views[indices_view]
    values_buffer = validated_views[values_view][0]
    if (
        binary is None
        or indices_buffer != 0
        or values_buffer != 0
        or not buffer_embedded[indices_buffer]
        or not buffer_embedded[values_buffer]
    ):
        raise GlbError(f"{label} must use the embedded BIN chunk")
    unpack_format = {5121: "<B", 5123: "<H", 5125: "<I"}[component_type]
    start = indices_view_offset + indices_offset
    previous = -1
    for item in range(count):
        sparse_index = struct.unpack_from(
            unpack_format, binary, start + item * index_component_size
        )[0]
        if sparse_index >= accessor_count:
            raise GlbError(f"{label}.indices value is out of range")
        if sparse_index <= previous:
            raise GlbError(f"{label}.indices must be strictly increasing")
        previous = sparse_index


def _validate_sparse_work(accessors: list[object]) -> None:
    """Reject aggregate sparse validation work before reading any indices."""
    work = 0
    for number, value in enumerate(accessors):
        accessor = _object(value, f"accessors[{number}]")
        if "sparse" not in accessor:
            continue
        sparse = _object(accessor["sparse"], f"accessors[{number}].sparse")
        work += _integer(
            sparse.get("count"),
            f"accessors[{number}].sparse.count",
            minimum=1,
        )
        if work > MAX_SPARSE_ACCESSOR_WORK:
            raise GlbError(
                "GLB sparse accessor work exceeds limit "
                f"{MAX_SPARSE_ACCESSOR_WORK}"
            )


def _validate_texture_info(
    value: object, texture_count: int, label: str
) -> tuple[int, int]:
    info = _object(value, label)
    texture = _index(info.get("index"), texture_count, f"{label}.index")
    texcoord = _integer(info.get("texCoord", 0), f"{label}.texCoord")
    return texture, texcoord


def _validate_material_references(
    material: Mapping[str, object], texture_count: int, label: str
) -> list[tuple[str, int, int]]:
    references: list[tuple[str, int, int]] = []
    pbr_value = material.get("pbrMetallicRoughness")
    if pbr_value is not None:
        pbr = _object(pbr_value, f"{label}.pbrMetallicRoughness")
        for name, role in (
            ("baseColorTexture", "baseColor"),
            ("metallicRoughnessTexture", "metallicRoughness"),
        ):
            if name in pbr:
                texture, texcoord = _validate_texture_info(
                    pbr[name], texture_count,
                    f"{label}.pbrMetallicRoughness.{name}",
                )
                references.append((role, texture, texcoord))
    for name, role in (
        ("normalTexture", "normal"),
        ("occlusionTexture", "occlusion"),
        ("emissiveTexture", "emissive"),
    ):
        if name in material:
            texture, texcoord = _validate_texture_info(
                material[name], texture_count, f"{label}.{name}"
            )
            references.append((role, texture, texcoord))
    return references


def _validate_geometry_work(
    document: Mapping[str, object],
    accessors: list[object],
    validated_accessors: list[
        tuple[int | None, int, int, str, int, int]
    ],
) -> list[object]:
    """Reject aggregate all-mesh decode work before reading POSITION values."""
    meshes = _root_array(document, "meshes")
    work = 0
    for mesh_number, mesh_value in enumerate(meshes):
        mesh = _object(mesh_value, f"meshes[{mesh_number}]")
        primitives = _array(
            mesh.get("primitives"), f"meshes[{mesh_number}].primitives"
        )
        if not primitives:
            raise GlbError(f"meshes[{mesh_number}] has no primitives")
        for primitive_number, primitive_value in enumerate(primitives):
            label = f"meshes[{mesh_number}].primitives[{primitive_number}]"
            primitive = _object(primitive_value, label)
            attributes = _object(
                primitive.get("attributes"), f"{label}.attributes"
            )
            if "POSITION" not in attributes:
                raise GlbError(f"{label} has no POSITION accessor")
            position_index = _index(
                attributes["POSITION"],
                len(accessors),
                f"{label}.attributes.POSITION",
            )
            position_count = validated_accessors[position_index][2]
            reference_count = position_count
            if "indices" in primitive:
                index = _index(
                    primitive["indices"], len(accessors), f"{label}.indices"
                )
                reference_count = validated_accessors[index][2]
            work += position_count + reference_count
            if work > MAX_GEOMETRY_WORK:
                raise GlbError(
                    "GLB geometry exceeds "
                    f"{MAX_GEOMETRY_WORK} combined index-reference and "
                    "POSITION-value work limit"
                )
    return meshes


def _inspect_document(
    document: dict[str, object], data: bytes
) -> tuple[
    dict[str, object],
    Callable[[], Iterator[tuple[float, float, float]]],
]:
    chunks = _chunks(data)
    binary_chunks = [payload for kind, payload in chunks if kind == b"BIN\0"]
    if len(binary_chunks) > 1:
        raise GlbError("duplicate BIN chunk")
    binary = binary_chunks[0] if binary_chunks else None

    asset = _object(document.get("asset"), "asset")
    if asset.get("version") != "2.0":
        raise GlbError("asset.version must be exactly 2.0")

    extensions_used = _extension_names(document, "extensionsUsed")
    extensions_required = _extension_names(document, "extensionsRequired")
    if not set(extensions_required).issubset(extensions_used):
        raise GlbError("extensionsRequired must be a subset of extensionsUsed")
    _validate_document_nesting(document)
    extension_payloads = _extension_payload_names(document)
    undeclared_extensions = extension_payloads - set(extensions_used)
    if undeclared_extensions:
        names = ", ".join(sorted(undeclared_extensions))
        raise GlbError(f"extension payload is not declared in extensionsUsed: {names}")
    extensions_used = sorted(set(extensions_used) | extension_payloads)

    buffers = _root_array(document, "buffers")
    buffer_lengths: list[int] = []
    buffer_embedded: list[bool] = []
    external_uris: set[str] = set()
    for number, value in enumerate(buffers):
        buffer = _object(value, f"buffers[{number}]")
        length = _integer(buffer.get("byteLength"), f"buffers[{number}].byteLength")
        uri = buffer.get("uri")
        if uri is not None and (not isinstance(uri, str) or not uri):
            raise GlbError(f"buffers[{number}].uri must be a non-empty string")
        embedded = uri is None
        if embedded:
            if number != 0 or binary is None:
                raise GlbError(f"buffers[{number}] has no embedded BIN chunk")
            if length > len(binary) or len(binary) - length > 3:
                raise GlbError("embedded BIN length disagrees with buffer byteLength")
        elif not uri.startswith("data:"):
            external_uris.add(uri)
        buffer_lengths.append(length)
        buffer_embedded.append(embedded)

    buffer_views = _root_array(document, "bufferViews")
    validated_views: list[tuple[int, int, int, int | None]] = []
    for number, value in enumerate(buffer_views):
        view = _object(value, f"bufferViews[{number}]")
        buffer_index = _index(view.get("buffer"), len(buffers), f"bufferViews[{number}].buffer")
        offset = _integer(view.get("byteOffset", 0), f"bufferViews[{number}].byteOffset")
        length = _integer(view.get("byteLength"), f"bufferViews[{number}].byteLength")
        stride_value = view.get("byteStride")
        stride = None if stride_value is None else _integer(stride_value, f"bufferViews[{number}].byteStride", minimum=4)
        if stride is not None and (stride > 252 or stride % 4):
            raise GlbError(f"bufferViews[{number}].byteStride must be a multiple of four from 4 to 252")
        if offset + length > buffer_lengths[buffer_index]:
            raise GlbError(f"bufferViews[{number}] overruns its buffer")
        if buffer_embedded[buffer_index] and binary is not None and offset + length > len(binary):
            raise GlbError(f"bufferViews[{number}] overruns embedded BIN")
        validated_views.append((buffer_index, offset, length, stride))

    accessors = _root_array(document, "accessors")
    if len(accessors) > MAX_ACCESSORS:
        raise GlbError(f"accessor array exceeds limit {MAX_ACCESSORS}")
    _validate_sparse_work(accessors)
    validated_accessors: list[tuple[int | None, int, int, str, int, int]] = []
    for number, value in enumerate(accessors):
        accessor = _object(value, f"accessors[{number}]")
        component_type = _integer(accessor.get("componentType"), f"accessors[{number}].componentType")
        if component_type not in _COMPONENT_SIZES:
            raise GlbError(f"accessors[{number}] has unsupported component type")
        kind = accessor.get("type")
        if not isinstance(kind, str) or kind not in _TYPE_COMPONENTS:
            raise GlbError(f"accessors[{number}] has unsupported type")
        count = _integer(accessor.get("count"), f"accessors[{number}].count")
        if count > MAX_ACCESSOR_COUNT:
            raise GlbError(
                f"accessor count exceeds limit {MAX_ACCESSOR_COUNT}"
            )
        accessor_offset = _integer(accessor.get("byteOffset", 0), f"accessors[{number}].byteOffset")
        view_value = accessor.get("bufferView")
        view_index = None if view_value is None else _index(view_value, len(buffer_views), f"accessors[{number}].bufferView")
        element_size = _COMPONENT_SIZES[component_type] * _TYPE_COMPONENTS[kind]
        component_size = _COMPONENT_SIZES[component_type]
        if accessor_offset % component_size:
            raise GlbError(f"accessors[{number}].byteOffset is misaligned")
        if view_index is not None:
            _, view_offset, view_length, view_stride = validated_views[view_index]
            if (view_offset + accessor_offset) % component_size:
                raise GlbError(f"accessors[{number}] has a misaligned effective offset")
            stride = view_stride or element_size
            if stride < element_size:
                raise GlbError(f"accessors[{number}] byteStride is smaller than its element")
            required = accessor_offset if count == 0 else accessor_offset + (count - 1) * stride + element_size
            if required > view_length:
                raise GlbError(f"accessors[{number}] overruns its bufferView")
        elif "sparse" not in accessor:
            raise GlbError(f"accessors[{number}] has neither bufferView nor sparse storage")
        elif accessor_offset:
            raise GlbError(f"accessors[{number}].byteOffset requires a bufferView")
        if "sparse" in accessor:
            _validate_sparse(
                accessor["sparse"], count, validated_views, component_size,
                element_size,
                f"accessors[{number}].sparse",
                binary,
                buffer_embedded,
            )
        normalized = accessor.get("normalized", False)
        if not isinstance(normalized, bool):
            raise GlbError(f"accessors[{number}].normalized must be boolean")
        validated_accessors.append((view_index, accessor_offset, count, kind, component_type, element_size))

    meshes = _validate_geometry_work(
        document,
        accessors,
        validated_accessors,
    )

    def embedded_view_layout(
        view_value: object, label: str
    ) -> tuple[int, int]:
        view_index = _index(view_value, len(buffer_views), f"{label}.bufferView")
        buffer_index, offset, length, _ = validated_views[view_index]
        if length > MAX_IMAGE_BYTES:
            raise GlbError(
                f"{label} payload exceeds image limit {MAX_IMAGE_BYTES} bytes"
            )
        if (
            binary is None
            or buffer_index != 0
            or not buffer_embedded[buffer_index]
        ):
            raise GlbError(f"{label} must use the embedded BIN chunk")
        return offset, length

    def embedded_view_payload(view_value: object, label: str) -> bytes:
        offset, length = embedded_view_layout(view_value, label)
        if binary is None:
            raise AssertionError("validated embedded image has no BIN chunk")
        return binary[offset:offset + length]

    def data_image_parts(uri: str, label: str) -> str:
        header, separator, encoded = uri.partition(",")
        if not separator or header.lower() not in (
            "data:image/png;base64",
            "data:image/jpeg;base64",
        ):
            raise GlbError(f"{label} has an unsupported embedded image URI")
        maximum_encoded = 4 * ((MAX_IMAGE_BYTES + 2) // 3)
        if len(encoded) > maximum_encoded:
            raise GlbError(
                f"{label} payload exceeds image limit {MAX_IMAGE_BYTES} bytes"
            )
        return encoded

    def data_image_payload(uri: str, label: str) -> bytes:
        encoded = data_image_parts(uri, label)
        try:
            payload = base64.b64decode(encoded, validate=True)
        except (binascii.Error, ValueError) as exc:
            raise GlbError(f"{label} has invalid base64 image data") from exc
        if len(payload) > MAX_IMAGE_BYTES:
            raise GlbError(
                f"{label} payload exceeds image limit {MAX_IMAGE_BYTES} bytes"
            )
        return payload

    images = _root_array(document, "images")
    image_work = 0
    for number, value in enumerate(images):
        image = _object(value, f"images[{number}]")
        label = f"images[{number}]"
        has_uri = "uri" in image
        has_view = "bufferView" in image
        if has_uri == has_view:
            raise GlbError(f"{label} must contain exactly one of uri or bufferView")
        if has_view:
            mime = image.get("mimeType")
            if mime not in ("image/png", "image/jpeg"):
                raise GlbError(f"{label}.mimeType is not a supported image type")
            _, work = embedded_view_layout(image["bufferView"], label)
            image_work += work
        else:
            uri = image["uri"]
            if not isinstance(uri, str) or not uri:
                raise GlbError(f"{label}.uri must be a non-empty string")
            if uri.startswith("data:"):
                image_work += len(data_image_parts(uri, label))
        if image_work > MAX_IMAGE_WORK_BYTES:
            raise GlbError(
                "GLB embedded-image work exceeds "
                f"{MAX_IMAGE_WORK_BYTES}-byte limit"
            )

    embedded_images = 0
    image_payload_sha256: list[str | None] = []
    for number, value in enumerate(images):
        image = _object(value, f"images[{number}]")
        label = f"images[{number}]"
        has_uri = "uri" in image
        has_view = "bufferView" in image
        if has_uri == has_view:
            raise GlbError(f"{label} must contain exactly one of uri or bufferView")
        if has_view:
            mime = image.get("mimeType")
            if mime not in ("image/png", "image/jpeg"):
                raise GlbError(f"{label}.mimeType is not a supported image type")
            payload = embedded_view_payload(image["bufferView"], label)
            embedded_images += 1
            image_payload_sha256.append(hashlib.sha256(payload).hexdigest())
        else:
            uri = image["uri"]
            if not isinstance(uri, str) or not uri:
                raise GlbError(f"{label}.uri must be a non-empty string")
            if uri.startswith("data:"):
                payload = data_image_payload(uri, label)
                embedded_images += 1
                image_payload_sha256.append(hashlib.sha256(payload).hexdigest())
            else:
                external_uris.add(uri)
                image_payload_sha256.append(None)

    samplers = _root_array(document, "samplers")
    for number, value in enumerate(samplers):
        _object(value, f"samplers[{number}]")
    textures = _root_array(document, "textures")
    texture_sources: list[int | None] = []
    for number, value in enumerate(textures):
        texture = _object(value, f"textures[{number}]")
        source: int | None = None
        if "source" in texture:
            source = _index(
                texture["source"], len(images), f"textures[{number}].source"
            )
        if "sampler" in texture:
            _index(texture["sampler"], len(samplers), f"textures[{number}].sampler")
        texture_sources.append(source)

    materials = _root_array(document, "materials")
    material_texture_references: list[list[tuple[str, int, int]]] = []
    for number, value in enumerate(materials):
        material = _object(value, f"materials[{number}]")
        material_texture_references.append(
            _validate_material_references(
                material, len(textures), f"materials[{number}]"
            )
        )

    local_bounds: dict[tuple[int, int], tuple[list[float], list[float]]] = {}
    local_position_accessors: dict[tuple[int, int], int] = {}

    def validate_accessor_reference(value: object, label: str) -> int:
        return _index(value, len(accessors), label)

    def position_values(
        accessor_index: int, label: str
    ) -> Iterator[tuple[float, float, float]]:
        accessor = _object(accessors[accessor_index], f"accessors[{accessor_index}]")
        view_index, accessor_offset, count, kind, component_type, _ = validated_accessors[accessor_index]
        if kind != "VEC3" or component_type != 5126:
            raise GlbError(f"{label} must use a FLOAT VEC3 accessor")
        if accessor.get("normalized", False):
            raise GlbError(f"{label} cannot be normalized")
        if "sparse" in accessor:
            raise GlbError(f"{label} cannot use a sparse accessor")
        if count == 0 or view_index is None:
            raise GlbError(f"{label} must contain POSITION values")
        buffer_index, view_offset, _, view_stride = validated_views[view_index]
        if not buffer_embedded[buffer_index] or buffer_index != 0 or binary is None:
            raise GlbError(f"{label} must be stored in the embedded BIN chunk")
        stride = view_stride or 12
        start = view_offset + accessor_offset
        for item in range(count):
            point = struct.unpack_from("<3f", binary, start + item * stride)
            if not all(math.isfinite(value) for value in point):
                raise GlbError(f"{label} contains a non-finite POSITION")
            yield point

    def decode_position(accessor_index: int, label: str) -> tuple[list[float], list[float]]:
        accessor = _object(accessors[accessor_index], f"accessors[{accessor_index}]")
        minimum = [math.inf, math.inf, math.inf]
        maximum = [-math.inf, -math.inf, -math.inf]
        for point in position_values(accessor_index, label):
            for axis, value in enumerate(point):
                minimum[axis] = min(minimum[axis], value)
                maximum[axis] = max(maximum[axis], value)
        declared_min = accessor.get("min")
        declared_max = accessor.get("max")
        if (declared_min is None) != (declared_max is None):
            raise GlbError(f"{label} must declare both min and max or neither")
        if declared_min is not None:
            expected_min = _finite_vector(declared_min, 3, f"{label}.min")
            expected_max = _finite_vector(declared_max, 3, f"{label}.max")
            if any(a > b for a, b in zip(expected_min, expected_max)):
                raise GlbError(f"{label} has inverted declared bounds")
            if any(
                not math.isclose(actual, declared, rel_tol=1e-5, abs_tol=1e-6)
                for actual, declared in zip(minimum + maximum, expected_min + expected_max)
            ):
                raise GlbError(f"{label} declared bounds disagree with POSITION bytes")
        return minimum, maximum

    def index_values(
        accessor_index: int, position_count: int, label: str
    ) -> Iterator[int]:
        accessor = _object(accessors[accessor_index], f"accessors[{accessor_index}]")
        view_index, accessor_offset, count, kind, component_type, _ = validated_accessors[accessor_index]
        if kind != "SCALAR" or component_type not in (5121, 5123, 5125):
            raise GlbError(f"{label} must use an unsigned SCALAR accessor")
        if accessor.get("normalized", False):
            raise GlbError(f"{label} cannot be normalized")
        if "sparse" in accessor:
            raise GlbError(f"{label} cannot use a sparse accessor")
        if view_index is None:
            raise GlbError(f"{label} must have stored index values")
        buffer_index, view_offset, _, view_stride = validated_views[view_index]
        if not buffer_embedded[buffer_index] or buffer_index != 0 or binary is None:
            raise GlbError(f"{label} must be stored in the embedded BIN chunk")
        if view_stride is not None:
            raise GlbError(f"{label} bufferView cannot have byteStride")
        component_size = _COMPONENT_SIZES[component_type]
        start = view_offset + accessor_offset
        if start % component_size:
            raise GlbError(f"{label} is misaligned")
        unpack_format = {5121: "<B", 5123: "<H", 5125: "<I"}[component_type]
        for item in range(count):
            index = struct.unpack_from(unpack_format, binary, start + item * component_size)[0]
            if index >= position_count:
                raise GlbError(
                    f"{label} value {index} is outside POSITION count {position_count}"
                )
            yield index

    def position_at(
        accessor_index: int, position_index: int, label: str
    ) -> tuple[float, float, float]:
        view_index, accessor_offset, _, _, _, _ = validated_accessors[accessor_index]
        if view_index is None or binary is None:
            raise GlbError(f"{label} must contain POSITION values")
        _, view_offset, _, view_stride = validated_views[view_index]
        point = struct.unpack_from(
            "<3f", binary,
            view_offset + accessor_offset + position_index * (view_stride or 12),
        )
        if not all(math.isfinite(value) for value in point):
            raise GlbError(f"{label} contains a non-finite POSITION")
        return point

    def validate_texcoord_accessor(accessor_index: int, label: str) -> None:
        accessor = _object(accessors[accessor_index], f"accessors[{accessor_index}]")
        _, _, _, kind, component_type, _ = validated_accessors[accessor_index]
        normalized = accessor.get("normalized", False)
        if kind != "VEC2":
            raise GlbError(f"{label} must use a VEC2 accessor")
        if component_type == 5126:
            if normalized:
                raise GlbError(f"{label} FLOAT accessor cannot be normalized")
        elif component_type in (5121, 5123):
            if not normalized:
                raise GlbError(
                    f"{label} integer accessor must be normalized"
                )
        else:
            raise GlbError(f"{label} has an unsupported component type")

    def triangle_indices(
        values: Iterator[int], count: int, mode: int
    ) -> Iterator[tuple[int, int, int]]:
        if mode == 4:
            for _ in range(count // 3):
                yield next(values), next(values), next(values)
            return
        if count < 3:
            for _ in values:
                pass
            return
        first = next(values)
        second = next(values)
        if mode == 5:
            previous_previous, previous = first, second
            for current in values:
                yield previous_previous, previous, current
                previous_previous, previous = previous, current
        else:
            previous = second
            for current in values:
                yield first, previous, current
                previous = current

    primitive_count = 0
    vertices = 0
    triangles = 0
    referenced_vertices = 0
    unique_faces: set[bytes] = set()
    degenerate_triangles = 0
    material_primitives = 0
    uv_primitives = 0
    morph_targets = 0
    used_materials: set[int] = set()
    for mesh_number, value in enumerate(meshes):
        mesh = _object(value, f"meshes[{mesh_number}]")
        primitives = _array(mesh.get("primitives"), f"meshes[{mesh_number}].primitives")
        if not primitives:
            raise GlbError(f"meshes[{mesh_number}] has no primitives")
        for primitive_number, primitive_value in enumerate(primitives):
            label = f"meshes[{mesh_number}].primitives[{primitive_number}]"
            primitive = _object(primitive_value, label)
            attributes = _object(primitive.get("attributes"), f"{label}.attributes")
            if "POSITION" not in attributes:
                raise GlbError(f"{label} has no POSITION accessor")
            attribute_indices: dict[str, int] = {}
            for semantic, accessor_value in attributes.items():
                if not isinstance(semantic, str) or not semantic:
                    raise GlbError(f"{label}.attributes has an invalid semantic")
                attribute_indices[semantic] = validate_accessor_reference(accessor_value, f"{label}.attributes.{semantic}")
            position_index = attribute_indices["POSITION"]
            bounds = decode_position(position_index, f"{label}.POSITION")
            local_bounds[(mesh_number, primitive_number)] = bounds
            local_position_accessors[(mesh_number, primitive_number)] = position_index
            position_count = validated_accessors[position_index][2]
            for semantic, accessor_index in attribute_indices.items():
                if validated_accessors[accessor_index][2] != position_count:
                    raise GlbError(f"{label}.attributes.{semantic} count disagrees with POSITION")
                if semantic.startswith("TEXCOORD_"):
                    suffix = semantic.removeprefix("TEXCOORD_")
                    if not suffix.isdigit():
                        raise GlbError(f"{label}.attributes.{semantic} is invalid")
                    validate_texcoord_accessor(
                        accessor_index, f"{label}.attributes.{semantic}"
                    )
            mode = _integer(primitive.get("mode", 4), f"{label}.mode")
            if mode not in (4, 5, 6):
                raise GlbError(f"{label} uses unsupported primitive mode {mode}")
            element_count = position_count
            if "indices" in primitive:
                index_accessor = validate_accessor_reference(primitive["indices"], f"{label}.indices")
                element_count = validated_accessors[index_accessor][2]
                raw_indices = index_values(
                    index_accessor, position_count, f"{label}.indices"
                )
            else:
                raw_indices = iter(range(position_count))
            if mode == 4:
                if element_count % 3:
                    raise GlbError(f"{label} triangle count is not divisible by three")
                primitive_triangles = element_count // 3
            else:
                primitive_triangles = max(0, element_count - 2)
            triangles += primitive_triangles

            referenced = bytearray(position_count)

            def checked_indices() -> Iterator[int]:
                nonlocal referenced_vertices
                for index in raw_indices:
                    if not referenced[index]:
                        referenced[index] = 1
                        referenced_vertices += 1
                    yield index

            position_label = f"{label}.POSITION"
            for face in triangle_indices(
                checked_indices(), element_count, mode
            ):
                points = tuple(
                    position_at(position_index, index, position_label)
                    for index in face
                )
                ab = tuple(points[1][axis] - points[0][axis] for axis in range(3))
                ac = tuple(points[2][axis] - points[0][axis] for axis in range(3))
                cross = (
                    ab[1] * ac[2] - ab[2] * ac[1],
                    ab[2] * ac[0] - ab[0] * ac[2],
                    ab[0] * ac[1] - ab[1] * ac[0],
                )
                if len(set(points)) != 3 or cross == (0.0, 0.0, 0.0):
                    degenerate_triangles += 1
                    continue
                encoded_points = sorted(
                    struct.pack(
                        "<3f", *(0.0 if value == 0.0 else value for value in point)
                    )
                    for point in points
                )
                unique_faces.add(b"".join(encoded_points))
            if "material" in primitive:
                material_index = _index(
                    primitive["material"], len(materials), f"{label}.material"
                )
                used_materials.add(material_index)
                for _, _, texcoord in material_texture_references[
                    material_index
                ]:
                    semantic = f"TEXCOORD_{texcoord}"
                    if semantic not in attribute_indices:
                        raise GlbError(
                            f"{label} material references missing {semantic}"
                        )
                material_primitives += 1
            if "TEXCOORD_0" in attributes:
                uv_primitives += 1
            targets = _array(primitive.get("targets", []), f"{label}.targets")
            for target_number, target_value in enumerate(targets):
                target = _object(target_value, f"{label}.targets[{target_number}]")
                for semantic, accessor_value in target.items():
                    if not isinstance(semantic, str) or not semantic:
                        raise GlbError(f"{label}.targets[{target_number}] has an invalid semantic")
                    target_accessor = validate_accessor_reference(
                        accessor_value, f"{label}.targets[{target_number}].{semantic}"
                    )
                    if validated_accessors[target_accessor][2] != position_count:
                        raise GlbError(
                            f"{label}.targets[{target_number}].{semantic} count disagrees with POSITION"
                        )
            morph_targets += len(targets)
            primitive_count += 1
            vertices += position_count

    material_texture_bindings: list[dict[str, object]] = []
    for material_index in sorted(used_materials):
        for role, texture_index, texcoord in material_texture_references[
            material_index
        ]:
            image_index = texture_sources[texture_index]
            if image_index is None:
                raise GlbError(
                    f"textures[{texture_index}] used by material has no source"
                )
            material_texture_bindings.append(
                {
                    "material": material_index,
                    "role": role,
                    "texcoord": texcoord,
                    "payload_sha256": image_payload_sha256[image_index],
                }
            )

    cameras = _root_array(document, "cameras")
    for number, value in enumerate(cameras):
        _object(value, f"cameras[{number}]")
    skins = _root_array(document, "skins")
    animations = _root_array(document, "animations")
    nodes = _root_array(document, "nodes")

    root_extensions_value = document.get("extensions", {})
    root_extensions = _object(root_extensions_value, "extensions")
    lights: list[object] = []
    if "KHR_lights_punctual" in root_extensions:
        lights_extension = _object(root_extensions["KHR_lights_punctual"], "extensions.KHR_lights_punctual")
        lights = _array(lights_extension.get("lights"), "extensions.KHR_lights_punctual.lights")
        for number, value in enumerate(lights):
            _object(value, f"extensions.KHR_lights_punctual.lights[{number}]")

    node_objects: list[dict[str, object]] = []
    node_children: list[list[int]] = []
    for number, value in enumerate(nodes):
        node = _object(value, f"nodes[{number}]")
        _node_matrix(node, f"nodes[{number}]")
        children_raw = _array(node.get("children", []), f"nodes[{number}].children")
        children = [_index(child, len(nodes), f"nodes[{number}].children") for child in children_raw]
        if len(set(children)) != len(children):
            raise GlbError(f"nodes[{number}].children contains duplicates")
        if "mesh" in node:
            _index(node["mesh"], len(meshes), f"nodes[{number}].mesh")
        if "camera" in node:
            _index(node["camera"], len(cameras), f"nodes[{number}].camera")
        if "skin" in node:
            _index(node["skin"], len(skins), f"nodes[{number}].skin")
        node_extensions_value = node.get("extensions", {})
        node_extensions = _object(node_extensions_value, f"nodes[{number}].extensions")
        if "KHR_lights_punctual" in node_extensions:
            light_ref = _object(node_extensions["KHR_lights_punctual"], f"nodes[{number}].extensions.KHR_lights_punctual")
            _index(light_ref.get("light"), len(lights), f"nodes[{number}].extensions.KHR_lights_punctual.light")
        node_objects.append(node)
        node_children.append(children)

    for number, value in enumerate(skins):
        skin = _object(value, f"skins[{number}]")
        joints = _array(skin.get("joints"), f"skins[{number}].joints")
        if not joints:
            raise GlbError(f"skins[{number}] has no joints")
        for joint in joints:
            _index(joint, len(nodes), f"skins[{number}].joints")
        if "skeleton" in skin:
            _index(skin["skeleton"], len(nodes), f"skins[{number}].skeleton")
        if "inverseBindMatrices" in skin:
            validate_accessor_reference(skin["inverseBindMatrices"], f"skins[{number}].inverseBindMatrices")

    for number, value in enumerate(animations):
        animation = _object(value, f"animations[{number}]")
        animation_samplers = _array(animation.get("samplers"), f"animations[{number}].samplers")
        for sampler_number, sampler_value in enumerate(animation_samplers):
            sampler = _object(sampler_value, f"animations[{number}].samplers[{sampler_number}]")
            validate_accessor_reference(sampler.get("input"), f"animations[{number}].samplers[{sampler_number}].input")
            validate_accessor_reference(sampler.get("output"), f"animations[{number}].samplers[{sampler_number}].output")
        channels = _array(animation.get("channels"), f"animations[{number}].channels")
        for channel_number, channel_value in enumerate(channels):
            channel = _object(channel_value, f"animations[{number}].channels[{channel_number}]")
            _index(channel.get("sampler"), len(animation_samplers), f"animations[{number}].channels[{channel_number}].sampler")
            target = _object(channel.get("target"), f"animations[{number}].channels[{channel_number}].target")
            if "node" in target:
                _index(target["node"], len(nodes), f"animations[{number}].channels[{channel_number}].target.node")

    scenes = _root_array(document, "scenes")
    if not scenes:
        raise GlbError("GLB has no scenes")
    scene_index = _index(document.get("scene", 0), len(scenes), "scene")
    scene_roots: list[list[int]] = []
    for number, value in enumerate(scenes):
        scene = _object(value, f"scenes[{number}]")
        roots_raw = _array(scene.get("nodes", []), f"scenes[{number}].nodes")
        roots = [_index(root, len(nodes), f"scenes[{number}].nodes") for root in roots_raw]
        if len(set(roots)) != len(roots):
            raise GlbError(f"scenes[{number}].nodes contains duplicates")
        scene_roots.append(roots)

    def iter_scene_nodes() -> Iterator[tuple[int, tuple[float, ...]]]:
        active_nodes: set[int] = set()
        visited_nodes: set[int] = set()
        traversal: list[tuple[bool, int, tuple[float, ...]]] = [
            (True, root, _IDENTITY) for root in reversed(scene_roots[scene_index])
        ]
        while traversal:
            entering, node_index, parent = traversal.pop()
            if not entering:
                active_nodes.remove(node_index)
                visited_nodes.add(node_index)
                continue
            if node_index in active_nodes:
                raise GlbError("selected scene graph contains a cycle")
            if node_index in visited_nodes:
                raise GlbError("selected scene graph references a node more than once")
            active_nodes.add(node_index)
            node = node_objects[node_index]
            world = _matrix_multiply(parent, _node_matrix(node, f"nodes[{node_index}]"))
            if not all(math.isfinite(value) for value in world):
                raise GlbError("selected scene graph produces a non-finite transform")
            yield node_index, world
            traversal.append((False, node_index, world))
            traversal.extend(
                (True, child, world) for child in reversed(node_children[node_index])
            )

    world_minimum = [math.inf, math.inf, math.inf]
    world_maximum = [-math.inf, -math.inf, -math.inf]
    found_geometry = False
    for node_index, world in iter_scene_nodes():
        node = node_objects[node_index]
        mesh_value = node.get("mesh")
        if mesh_value is None:
            continue
        mesh_index = _index(mesh_value, len(meshes), f"nodes[{node_index}].mesh")
        primitives = _array(
            _object(meshes[mesh_index], f"meshes[{mesh_index}]").get("primitives"),
            f"meshes[{mesh_index}].primitives",
        )
        for primitive_number in range(len(primitives)):
            minimum, maximum = local_bounds[(mesh_index, primitive_number)]
            for x in (minimum[0], maximum[0]):
                for y in (minimum[1], maximum[1]):
                    for z in (minimum[2], maximum[2]):
                        point = _transform_point(world, (x, y, z))
                        for axis, coordinate in enumerate(point):
                            world_minimum[axis] = min(world_minimum[axis], coordinate)
                            world_maximum[axis] = max(world_maximum[axis], coordinate)
            found_geometry = True
    if not found_geometry:
        raise GlbError("selected scene contains no POSITION geometry")

    def world_position_iterator() -> Iterator[tuple[float, float, float]]:
        for node_index, world in iter_scene_nodes():
            node = node_objects[node_index]
            mesh_value = node.get("mesh")
            if mesh_value is None:
                continue
            mesh_index = _index(mesh_value, len(meshes), f"nodes[{node_index}].mesh")
            primitives = _array(
                _object(meshes[mesh_index], f"meshes[{mesh_index}]").get("primitives"),
                f"meshes[{mesh_index}].primitives",
            )
            for primitive_number in range(len(primitives)):
                label = f"meshes[{mesh_index}].primitives[{primitive_number}].POSITION"
                accessor_index = local_position_accessors[(mesh_index, primitive_number)]
                for point in position_values(accessor_index, label):
                    yield _transform_point(world, point)

    details = {
        "meshes": len(meshes),
        "primitives": primitive_count,
        "vertices": vertices,
        "triangles": triangles,
        "referenced_vertices": referenced_vertices,
        "unique_triangles": len(unique_faces),
        "degenerate_triangles": degenerate_triangles,
        "materials": len(materials),
        "material_primitives": material_primitives,
        "images": len(images),
        "embedded_images": embedded_images,
        "image_payload_sha256": image_payload_sha256,
        "material_texture_bindings": material_texture_bindings,
        "uv_primitives": uv_primitives,
        "animations": len(animations),
        "cameras": len(cameras),
        "lights": len(lights),
        "skins": len(skins),
        "morph_targets": morph_targets,
        "external_uris": sorted(external_uris),
        "extensions_used": extensions_used,
        "extensions_required": extensions_required,
        "world_bounds": {"min": world_minimum, "max": world_maximum},
    }
    return details, world_position_iterator


def inspect_glb(path: Path) -> dict[str, object]:
    """Inspect *path* and return authoritative geometry and resource metrics."""
    path = Path(path)
    document, data = _read_glb(path)
    details, _ = _inspect_document(document, data)
    metrics: dict[str, object] = {
        "path": str(path),
        "sha256": hashlib.sha256(data).hexdigest(),
        "bytes": len(data),
        **details,
    }
    if tuple(metrics) != METRIC_KEYS:
        raise AssertionError("internal metric key mismatch")
    return metrics


def iter_world_positions(path: Path) -> Iterator[tuple[float, float, float]]:
    """Yield checked POSITION values for every primitive in the selected scene."""
    document, data = _read_glb(Path(path))
    _, position_iterator = _inspect_document(document, data)
    return position_iterator()


def center_and_extents(bounds: object) -> tuple[list[float], list[float]]:
    bound_object = _object(bounds, "world_bounds")
    lo = _finite_vector(bound_object.get("min"), 3, "world_bounds.min")
    hi = _finite_vector(bound_object.get("max"), 3, "world_bounds.max")
    if any(a > b for a, b in zip(lo, hi)):
        raise GlbError("world_bounds is inverted")
    center = [(a + b) / 2.0 for a, b in zip(lo, hi)]
    extents = [b - a for a, b in zip(lo, hi)]
    return center, extents


def _metric_integer(metrics: Mapping[str, object], name: str, label: str) -> int:
    return _integer(metrics.get(name), f"{label}.{name}")


def compare_preservation(source: Mapping[str, object], output: Mapping[str, object]) -> list[str]:
    """Return preservation-policy diagnostics; an empty list means acceptance."""
    reasons: list[str] = []

    for label, metrics in (("source", source), ("output", output)):
        external = metrics.get("external_uris", [])
        if not isinstance(external, list):
            reasons.append(f"{label} external_uris is malformed")
        elif external:
            reasons.append(f"{label} contains external URIs: {', '.join(map(str, external))}")

        try:
            primitives = _metric_integer(metrics, "primitives", label)
            degenerate = _metric_integer(metrics, "degenerate_triangles", label)
            if degenerate:
                reasons.append(
                    f"{label} contains {degenerate} degenerate triangle"
                    f"{'' if degenerate == 1 else 's'}"
                )
            if _metric_integer(metrics, "uv_primitives", label) != primitives:
                reasons.append(f"{label} does not bind TEXCOORD_0 on every primitive")
            if _metric_integer(metrics, "material_primitives", label) != primitives:
                reasons.append(f"{label} does not bind a material on every primitive")
            for singular, metric_name in (
                ("animation", "animations"),
                ("camera", "cameras"),
                ("light", "lights"),
                ("skin", "skins"),
                ("morph target", "morph_targets"),
            ):
                count = _metric_integer(metrics, metric_name, label)
                if count:
                    reasons.append(f"{label} contains {count} {singular}{'' if count == 1 else 's'}")
        except GlbError as exc:
            reasons.append(str(exc))

    try:
        output_vertices = _metric_integer(output, "vertices", "output")
        output_referenced = _metric_integer(
            output, "referenced_vertices", "output"
        )
        if output_referenced != output_vertices:
            reasons.append(
                "output referenced vertex count "
                f"{output_referenced} differs from vertex count {output_vertices}"
            )
        output_triangles = _metric_integer(output, "triangles", "output")
        output_unique = _metric_integer(output, "unique_triangles", "output")
        if output_unique != output_triangles:
            reasons.append(
                "output unique triangle count "
                f"{output_unique} differs from raw triangle count "
                f"{output_triangles}"
            )
    except GlbError as exc:
        reasons.append(str(exc))

    output_extensions_value = output.get("extensions_used", [])
    output_required_value = output.get("extensions_required", [])
    if not isinstance(output_extensions_value, list) or not all(isinstance(item, str) for item in output_extensions_value):
        reasons.append("output extensions_used is malformed")
        output_extensions: set[str] = set()
    else:
        output_extensions = set(output_extensions_value)
    if not isinstance(output_required_value, list) or not all(isinstance(item, str) for item in output_required_value):
        reasons.append("output extensions_required is malformed")
        output_required: set[str] = set()
    else:
        output_required = set(output_required_value)
    for extension in sorted(output_extensions | output_required):
        if extension in DISALLOWED_EXTENSIONS:
            reasons.append(f"output compression extension {extension} is not allowed")
        elif extension not in ALLOWED_OUTPUT_EXTENSIONS:
            reasons.append(f"output extension {extension} is not approved")

    try:
        source_materials = _metric_integer(source, "materials", "source")
        output_materials = _metric_integer(output, "materials", "output")
        if source_materials != output_materials:
            reasons.append(
                "material count changed from "
                f"{source_materials} to {output_materials}"
            )
    except GlbError as exc:
        reasons.append(str(exc))

    source_payloads = source.get("image_payload_sha256")
    output_payloads = output.get("image_payload_sha256")
    if not isinstance(source_payloads, list) or not all(
        isinstance(item, (str, type(None))) for item in source_payloads
    ):
        reasons.append("source image payload hashes are malformed")
    elif not isinstance(output_payloads, list) or not all(
        isinstance(item, (str, type(None))) for item in output_payloads
    ):
        reasons.append("output image payload hashes are malformed")
    elif Counter(source_payloads) != Counter(output_payloads):
        reasons.append("image payload multiset changed")
        try:
            source_embedded = _metric_integer(
                source, "embedded_images", "source"
            )
            output_embedded = _metric_integer(
                output, "embedded_images", "output"
            )
            if source_embedded != output_embedded:
                reasons.append(
                    "embedded-image count changed from "
                    f"{source_embedded} to {output_embedded}"
                )
        except GlbError as exc:
            reasons.append(str(exc))

    source_bindings = source.get("material_texture_bindings")
    output_bindings = output.get("material_texture_bindings")
    if not isinstance(source_bindings, list):
        reasons.append("source material texture bindings are malformed")
    elif not isinstance(output_bindings, list):
        reasons.append("output material texture bindings are malformed")
    elif source_bindings != output_bindings:
        reasons.append("material texture bindings changed")

    try:
        source_center, source_extents = center_and_extents(source.get("world_bounds"))
        output_center, output_extents = center_and_extents(output.get("world_bounds"))
        source_longest = max(source_extents)
        output_longest = max(output_extents)
        if (
            source_longest <= 0.0
            or output_longest <= 0.0
            or not math.isfinite(source_longest)
            or not math.isfinite(output_longest)
        ):
            reasons.append("world bounds have zero or non-finite extent")
        else:
            center_drift = max(abs(a - b) for a, b in zip(source_center, output_center)) / source_longest
            if center_drift > CENTER_DRIFT_MAX:
                reasons.append(f"center drift {center_drift:.9g} exceeds {CENTER_DRIFT_MAX:.9g}")
            scale_drift = abs(output_longest / source_longest - 1.0)
            if scale_drift > SCALE_DRIFT_MAX:
                reasons.append(f"scale drift {scale_drift:.9g} exceeds {SCALE_DRIFT_MAX:.9g}")
            source_shape = [extent / source_longest for extent in source_extents]
            output_shape = [extent / output_longest for extent in output_extents]
            shape_drift = max(abs(a - b) for a, b in zip(source_shape, output_shape))
            if shape_drift > NORMALIZED_EXTENT_DRIFT_MAX:
                reasons.append(
                    f"normalized extent drift {shape_drift:.9g} exceeds "
                    f"{NORMALIZED_EXTENT_DRIFT_MAX:.9g}"
                )
    except GlbError as exc:
        reasons.append(str(exc))

    return reasons


def _diagnostic_payload(message: object) -> bytes:
    raw = str(message) or "input processing failed"
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

    prefix = b"glb-metrics: "
    body_budget = MAX_DIAGNOSTIC_BYTES - len(prefix) - 1
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
    if len(arguments) != 1:
        _emit_diagnostic("usage: glb_metrics.py FILE")
        return 2
    try:
        metrics = inspect_glb(Path(arguments[0]))
    except (
        GlbError, OSError, struct.error, UnicodeError, ValueError,
        OverflowError, MemoryError, RecursionError,
    ) as exc:
        _emit_diagnostic(exc)
        return 1
    print(json.dumps(metrics, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(_main(sys.argv[1:]))
