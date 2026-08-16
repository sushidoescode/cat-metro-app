#!/usr/bin/env python3
"""Strict, standard-library GLB inspection and preservation checks."""

from __future__ import annotations

import hashlib
import json
import math
import struct
import sys
from collections.abc import Mapping
from pathlib import Path


class GlbError(ValueError):
    """Raised when a GLB is malformed or outside the supported profile."""


METRIC_KEYS = (
    "path", "sha256", "bytes", "meshes", "primitives", "vertices",
    "triangles", "materials", "material_primitives", "images",
    "embedded_images", "uv_primitives", "animations", "cameras", "lights",
    "skins", "morph_targets", "external_uris", "extensions_used",
    "extensions_required", "world_bounds",
)

DISALLOWED_EXTENSIONS = {"EXT_meshopt_compression", "KHR_draco_mesh_compression"}
ALLOWED_OUTPUT_EXTENSIONS = frozenset()
CENTER_DRIFT_MAX = 0.005
SCALE_DRIFT_MAX = 0.01
NORMALIZED_EXTENT_DRIFT_MAX = 0.02

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


def _read_glb(path: Path) -> tuple[dict[str, object], bytes]:
    data = path.read_bytes()
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
            try:
                decoded = json.loads(
                    data[offset:end].rstrip(b" ").decode("utf-8"),
                    parse_constant=_reject_json_constant,
                )
            except (UnicodeDecodeError, json.JSONDecodeError) as exc:
                raise GlbError(f"invalid GLB JSON: {exc}") from exc
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
    result: list[str] = []
    for value in raw:
        if not isinstance(value, str) or not value:
            raise GlbError(f"{name} entries must be non-empty strings")
        if value in result:
            raise GlbError(f"{name} contains duplicate {value}")
        result.append(value)
    return sorted(result)


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
    element_size: int,
    label: str,
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


def _validate_texture_info(value: object, texture_count: int, label: str) -> None:
    info = _object(value, label)
    _index(info.get("index"), texture_count, f"{label}.index")


def _validate_material_references(material: Mapping[str, object], texture_count: int, label: str) -> None:
    pbr_value = material.get("pbrMetallicRoughness")
    if pbr_value is not None:
        pbr = _object(pbr_value, f"{label}.pbrMetallicRoughness")
        for name in ("baseColorTexture", "metallicRoughnessTexture"):
            if name in pbr:
                _validate_texture_info(pbr[name], texture_count, f"{label}.pbrMetallicRoughness.{name}")
    for name in ("normalTexture", "occlusionTexture", "emissiveTexture"):
        if name in material:
            _validate_texture_info(material[name], texture_count, f"{label}.{name}")


def _inspect_document(document: dict[str, object], data: bytes) -> dict[str, object]:
    chunks = _chunks(data)
    binary_chunks = [payload for kind, payload in chunks if kind == b"BIN\0"]
    if len(binary_chunks) > 1:
        raise GlbError("duplicate BIN chunk")
    binary = binary_chunks[0] if binary_chunks else None

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
        accessor_offset = _integer(accessor.get("byteOffset", 0), f"accessors[{number}].byteOffset")
        view_value = accessor.get("bufferView")
        view_index = None if view_value is None else _index(view_value, len(buffer_views), f"accessors[{number}].bufferView")
        element_size = _COMPONENT_SIZES[component_type] * _TYPE_COMPONENTS[kind]
        if accessor_offset % _COMPONENT_SIZES[component_type]:
            raise GlbError(f"accessors[{number}].byteOffset is misaligned")
        if view_index is not None:
            _, _, view_length, view_stride = validated_views[view_index]
            stride = view_stride or element_size
            if stride < element_size:
                raise GlbError(f"accessors[{number}] byteStride is smaller than its element")
            required = accessor_offset if count == 0 else accessor_offset + (count - 1) * stride + element_size
            if required > view_length:
                raise GlbError(f"accessors[{number}] overruns its bufferView")
        elif "sparse" not in accessor:
            raise GlbError(f"accessors[{number}] has neither bufferView nor sparse storage")
        if "sparse" in accessor:
            _validate_sparse(
                accessor["sparse"], count, validated_views, element_size,
                f"accessors[{number}].sparse",
            )
        normalized = accessor.get("normalized", False)
        if not isinstance(normalized, bool):
            raise GlbError(f"accessors[{number}].normalized must be boolean")
        validated_accessors.append((view_index, accessor_offset, count, kind, component_type, element_size))

    images = _root_array(document, "images")
    embedded_images = 0
    for number, value in enumerate(images):
        image = _object(value, f"images[{number}]")
        has_uri = "uri" in image
        has_view = "bufferView" in image
        if has_uri == has_view:
            raise GlbError(f"images[{number}] must contain exactly one of uri or bufferView")
        if has_view:
            _index(image["bufferView"], len(buffer_views), f"images[{number}].bufferView")
            mime = image.get("mimeType")
            if not isinstance(mime, str) or not mime:
                raise GlbError(f"images[{number}].mimeType is required for bufferView images")
            embedded_images += 1
        else:
            uri = image["uri"]
            if not isinstance(uri, str) or not uri:
                raise GlbError(f"images[{number}].uri must be a non-empty string")
            if uri.startswith("data:"):
                embedded_images += 1
            else:
                external_uris.add(uri)

    samplers = _root_array(document, "samplers")
    for number, value in enumerate(samplers):
        _object(value, f"samplers[{number}]")
    textures = _root_array(document, "textures")
    for number, value in enumerate(textures):
        texture = _object(value, f"textures[{number}]")
        if "source" in texture:
            _index(texture["source"], len(images), f"textures[{number}].source")
        if "sampler" in texture:
            _index(texture["sampler"], len(samplers), f"textures[{number}].sampler")

    materials = _root_array(document, "materials")
    for number, value in enumerate(materials):
        material = _object(value, f"materials[{number}]")
        _validate_material_references(material, len(textures), f"materials[{number}]")

    local_bounds: dict[tuple[int, int], tuple[list[float], list[float]]] = {}

    def validate_accessor_reference(value: object, label: str) -> int:
        return _index(value, len(accessors), label)

    def decode_position(accessor_index: int, label: str) -> tuple[list[float], list[float]]:
        accessor = _object(accessors[accessor_index], f"accessors[{accessor_index}]")
        view_index, accessor_offset, count, kind, component_type, _ = validated_accessors[accessor_index]
        if kind != "VEC3" or component_type != 5126:
            raise GlbError(f"{label} must use a FLOAT VEC3 accessor")
        if "sparse" in accessor:
            raise GlbError(f"{label} cannot use a sparse accessor")
        if count == 0 or view_index is None:
            raise GlbError(f"{label} must contain POSITION values")
        buffer_index, view_offset, _, view_stride = validated_views[view_index]
        if not buffer_embedded[buffer_index] or buffer_index != 0 or binary is None:
            raise GlbError(f"{label} must be stored in the embedded BIN chunk")
        stride = view_stride or 12
        start = view_offset + accessor_offset
        minimum = [math.inf, math.inf, math.inf]
        maximum = [-math.inf, -math.inf, -math.inf]
        for item in range(count):
            point = struct.unpack_from("<3f", binary, start + item * stride)
            if not all(math.isfinite(value) for value in point):
                raise GlbError(f"{label} contains a non-finite POSITION")
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

    meshes = _root_array(document, "meshes")
    primitive_count = 0
    vertices = 0
    triangles = 0
    material_primitives = 0
    uv_primitives = 0
    morph_targets = 0
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
            position_count = validated_accessors[position_index][2]
            for semantic, accessor_index in attribute_indices.items():
                if validated_accessors[accessor_index][2] != position_count:
                    raise GlbError(f"{label}.attributes.{semantic} count disagrees with POSITION")
            mode = _integer(primitive.get("mode", 4), f"{label}.mode")
            if mode not in (4, 5, 6):
                raise GlbError(f"{label} uses unsupported primitive mode {mode}")
            element_count = position_count
            if "indices" in primitive:
                index_accessor = validate_accessor_reference(primitive["indices"], f"{label}.indices")
                _, _, element_count, index_kind, index_component, _ = validated_accessors[index_accessor]
                if index_kind != "SCALAR" or index_component not in (5121, 5123, 5125):
                    raise GlbError(f"{label}.indices must use an unsigned SCALAR accessor")
            if mode == 4:
                if element_count % 3:
                    raise GlbError(f"{label} triangle count is not divisible by three")
                triangles += element_count // 3
            else:
                triangles += max(0, element_count - 2)
            if "material" in primitive:
                _index(primitive["material"], len(materials), f"{label}.material")
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

    extensions_used = _extension_names(document, "extensionsUsed")
    extensions_required = _extension_names(document, "extensionsRequired")
    if not set(extensions_required).issubset(extensions_used):
        raise GlbError("extensionsRequired must be a subset of extensionsUsed")

    world_minimum = [math.inf, math.inf, math.inf]
    world_maximum = [-math.inf, -math.inf, -math.inf]
    found_geometry = False

    def walk(node_index: int, parent: tuple[float, ...], ancestors: frozenset[int]) -> None:
        nonlocal found_geometry
        if node_index in ancestors:
            raise GlbError("selected scene graph contains a cycle")
        node = node_objects[node_index]
        world = _matrix_multiply(parent, _node_matrix(node, f"nodes[{node_index}]"))
        if not all(math.isfinite(value) for value in world):
            raise GlbError("selected scene graph produces a non-finite transform")
        mesh_value = node.get("mesh")
        if mesh_value is not None:
            mesh_index = _index(mesh_value, len(meshes), f"nodes[{node_index}].mesh")
            primitives = _array(_object(meshes[mesh_index], f"meshes[{mesh_index}]").get("primitives"), f"meshes[{mesh_index}].primitives")
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
        next_ancestors = ancestors | {node_index}
        for child in node_children[node_index]:
            walk(child, world, next_ancestors)

    for root in scene_roots[scene_index]:
        walk(root, _IDENTITY, frozenset())
    if not found_geometry:
        raise GlbError("selected scene contains no POSITION geometry")

    return {
        "meshes": len(meshes),
        "primitives": primitive_count,
        "vertices": vertices,
        "triangles": triangles,
        "materials": len(materials),
        "material_primitives": material_primitives,
        "images": len(images),
        "embedded_images": embedded_images,
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


def inspect_glb(path: Path) -> dict[str, object]:
    """Inspect *path* and return authoritative geometry and resource metrics."""
    path = Path(path)
    document, data = _read_glb(path)
    details = _inspect_document(document, data)
    metrics: dict[str, object] = {
        "path": str(path),
        "sha256": hashlib.sha256(data).hexdigest(),
        "bytes": len(data),
        **details,
    }
    if tuple(metrics) != METRIC_KEYS:
        raise AssertionError("internal metric key mismatch")
    return metrics


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

    for name, description in (
        ("materials", "material count"),
        ("embedded_images", "embedded-image count"),
    ):
        try:
            source_value = _metric_integer(source, name, "source")
            output_value = _metric_integer(output, name, "output")
            if source_value != output_value:
                reasons.append(f"{description} changed from {source_value} to {output_value}")
        except GlbError as exc:
            reasons.append(str(exc))

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


def _main(arguments: list[str]) -> int:
    if len(arguments) != 1:
        print("glb-metrics: usage: glb_metrics.py FILE", file=sys.stderr)
        return 2
    try:
        metrics = inspect_glb(Path(arguments[0]))
    except (GlbError, OSError, struct.error) as exc:
        print(f"glb-metrics: {exc}", file=sys.stderr)
        return 1
    print(json.dumps(metrics, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(_main(sys.argv[1:]))
