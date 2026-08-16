#!/usr/bin/env python3
"""Deterministic, tiny GLB fixtures for asset-pipeline regression tests."""

from __future__ import annotations

import argparse
import json
import struct
from pathlib import Path


_PNG_1X1 = bytes.fromhex(
    "89504e470d0a1a0a0000000d49484452000000010000000108060000001f15c489"
    "0000000d49444154789c63f8cfc0f01f00050002ff49c2fd650000000049454e44ae426082"
)


def write_glb(
    path: Path,
    *,
    triangles: int,
    primitive_count: int = 1,
    include_uv: bool = True,
    include_material: bool = True,
    include_image: bool = True,
    external_image: bool = False,
    extensions: tuple[str, ...] = (),
    add_scene_content: bool = False,
    bounds: tuple[tuple[float, float, float], tuple[float, float, float]] = (
        (-1.0, -1.0, -1.0),
        (1.0, 1.0, 1.0),
    ),
    declared_bounds: tuple[tuple[float, float, float], tuple[float, float, float]] | None = None,
    translation: tuple[float, float, float] = (0.0, 0.0, 0.0),
) -> None:
    """Write a self-contained GLB whose repeated indices encode *triangles*."""
    if triangles < 0:
        raise ValueError("triangles must be non-negative")
    if primitive_count < 1:
        raise ValueError("primitive_count must be positive")

    minimum, maximum = bounds
    declared_minimum, declared_maximum = declared_bounds or bounds
    for point in (minimum, maximum, declared_minimum, declared_maximum, translation):
        if len(point) != 3:
            raise ValueError("bounds and translation must be VEC3 values")

    data = bytearray()
    buffer_views: list[dict[str, int]] = []
    accessors: list[dict[str, object]] = []

    def append_view(payload: bytes, *, target: int | None = None) -> int:
        while len(data) % 4:
            data.append(0)
        view: dict[str, int] = {"buffer": 0, "byteOffset": len(data), "byteLength": len(payload)}
        if target is not None:
            view["target"] = target
        data.extend(payload)
        buffer_views.append(view)
        return len(buffer_views) - 1

    def append_accessor(
        view: int,
        count: int,
        component_type: int,
        kind: str,
        *,
        minimum_value: list[float] | None = None,
        maximum_value: list[float] | None = None,
    ) -> int:
        accessor: dict[str, object] = {
            "bufferView": view,
            "componentType": component_type,
            "count": count,
            "type": kind,
        }
        if minimum_value is not None:
            accessor["min"] = minimum_value
        if maximum_value is not None:
            accessor["max"] = maximum_value
        accessors.append(accessor)
        return len(accessors) - 1

    base_positions = [
        (x, y, z)
        for x in (minimum[0], maximum[0])
        for y in (minimum[1], maximum[1])
        for z in (minimum[2], maximum[2])
    ]
    uvs = ((0.0, 0.0), (1.0, 0.0), (0.0, 1.0), (1.0, 1.0)) * 2
    triangle_base, triangle_remainder = divmod(triangles, primitive_count)
    primitives: list[dict[str, object]] = []

    for primitive_index in range(primitive_count):
        # Separate accessors make each primitive independently inspectable.  The
        # offsets distribute their centers evenly along the X axis.
        center_offset = (primitive_index - (primitive_count - 1) / 2.0) * (
            (maximum[0] - minimum[0]) / max(primitive_count, 1)
        )
        positions = [(x + center_offset, y, z) for x, y, z in base_positions]
        accessor_minimum = list(declared_minimum) if declared_bounds else [
            minimum[0] + center_offset, minimum[1], minimum[2]
        ]
        accessor_maximum = list(declared_maximum) if declared_bounds else [
            maximum[0] + center_offset, maximum[1], maximum[2]
        ]
        position_bytes = struct.pack("<24f", *(coordinate for point in positions for coordinate in point))
        position_view = append_view(position_bytes, target=34962)
        position_accessor = append_accessor(
            position_view,
            8,
            5126,
            "VEC3",
            minimum_value=accessor_minimum,
            maximum_value=accessor_maximum,
        )

        primitive_triangles = triangle_base + (1 if primitive_index < triangle_remainder else 0)
        indices = (0, 1, 2) * primitive_triangles
        index_view = append_view(struct.pack(f"<{len(indices)}H", *indices), target=34963)
        index_accessor = append_accessor(index_view, len(indices), 5123, "SCALAR")
        attributes: dict[str, int] = {"POSITION": position_accessor}
        if include_uv:
            uv_view = append_view(struct.pack("<16f", *(coordinate for uv in uvs for coordinate in uv)), target=34962)
            attributes["TEXCOORD_0"] = append_accessor(uv_view, 8, 5126, "VEC2")
        primitive: dict[str, object] = {"attributes": attributes, "indices": index_accessor, "mode": 4}
        if include_material:
            primitive["material"] = 0
        primitives.append(primitive)

    document: dict[str, object] = {
        "asset": {"version": "2.0", "generator": "cat-metro-glb-fixture"},
        "buffers": [{"byteLength": len(data)}],
        "bufferViews": buffer_views,
        "accessors": accessors,
        "meshes": [{"primitives": primitives}],
        "nodes": [{"mesh": 0, "translation": list(translation)}],
        "scenes": [{"nodes": [0]}],
        "scene": 0,
    }

    if include_image:
        if external_image:
            images: list[dict[str, object]] = [{"uri": "fixture-external.png"}]
        else:
            image_view = append_view(_PNG_1X1)
            images = [{"bufferView": image_view, "mimeType": "image/png"}]
        document["images"] = images
        document["textures"] = [{"source": 0}]
    if include_material:
        material: dict[str, object] = {}
        if include_image:
            material = {"pbrMetallicRoughness": {"baseColorTexture": {"index": 0}}}
        document["materials"] = [material]
    if extensions:
        document["extensionsUsed"] = list(extensions)
    if add_scene_content:
        document["animations"] = [{}]
        document["cameras"] = [{}]
        document["skins"] = [{}]
        document["extensionsUsed"] = list(dict.fromkeys([*extensions, "KHR_lights_punctual"]))
        document["extensions"] = {"KHR_lights_punctual": {"lights": [{}]}}
        node = document["nodes"][0]
        assert isinstance(node, dict)
        node["camera"] = 0
        node["extensions"] = {"KHR_lights_punctual": {"light": 0}}
        primitives[0]["targets"] = [{}]

    # Views are appended while assembling the image, so buffer byteLength is
    # finalized only after every payload is present.
    document["buffers"] = [{"byteLength": len(data)}]
    json_payload = json.dumps(document, separators=(",", ":"), sort_keys=True).encode("utf-8")
    json_payload += b" " * (-len(json_payload) % 4)
    binary_payload = bytes(data) + b"\0" * (-len(data) % 4)
    total_length = 12 + 8 + len(json_payload) + 8 + len(binary_payload)
    payload = (
        struct.pack("<4sII", b"glTF", 2, total_length)
        + struct.pack("<I4s", len(json_payload), b"JSON")
        + json_payload
        + struct.pack("<I4s", len(binary_payload), b"BIN\0")
        + binary_payload
    )
    path.write_bytes(payload)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("path", type=Path)
    parser.add_argument("--triangles", type=int, required=True)
    parser.add_argument("--omit-uv", action="store_true")
    parser.add_argument("--omit-material", action="store_true")
    parser.add_argument("--omit-image", action="store_true")
    parser.add_argument("--external-image", action="store_true")
    parser.add_argument("--primitive-count", type=int, default=1)
    parser.add_argument("--extension", action="append", default=[])
    parser.add_argument("--add-scene-content", action="store_true")
    parser.add_argument("--translate", nargs=3, type=float, default=(0.0, 0.0, 0.0))
    args = parser.parse_args()
    write_glb(args.path, triangles=args.triangles,
              include_uv=not args.omit_uv,
              include_material=not args.omit_material,
              include_image=not args.omit_image,
              external_image=args.external_image,
              primitive_count=args.primitive_count,
              extensions=tuple(args.extension),
              add_scene_content=args.add_scene_content,
              translation=tuple(args.translate))
