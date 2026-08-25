#!/usr/bin/env python3
"""Blender-only Polyfork GLB -> Unity FBX + baked palette atlas driver.

Polyfork assets are flat-shaded vertex-colour meshes (COLOR_0) with no
textures. Unity's URP/Lit does not read vertex colours, and the project's
PropModelCatalog only admits materials with a bound _BaseMap texture, so a
plain format conversion would arrive white/grey. This driver bakes the
vertex palette into a small PNG atlas (4x4-texel blocks so block compression
cannot bleed across colours) and rewrites every face's UVs to point at its
colour's block centre. Geometry is untouched: these assets are already
low-poly and are never decimated here.

Export uses the repo's known-good Unity settings (.claude/rules/unity.md):
bake_space_transform=True, axis_forward='-Z', axis_up='Y'.
"""

from __future__ import annotations

import argparse
import json
import struct
import sys
import zlib
from pathlib import Path

import bpy
from mathutils import Vector


BLENDER_VERSION = (5, 1, 2)
BLENDER_BUILD_HASH = "ec6e62d40fa9"
EXIT_CODE = 96

ATLAS_SIZE = 64
BLOCK = 4
BLOCKS_PER_ROW = ATLAS_SIZE // BLOCK
MAX_COLORS = BLOCKS_PER_ROW * BLOCKS_PER_ROW


class DriverArgumentParser(argparse.ArgumentParser):
    def error(self, message: str) -> None:
        raise RuntimeError(message)


def _arguments(argv: list[str]) -> argparse.Namespace:
    try:
        separator = argv.index("--")
    except ValueError as exc:
        raise RuntimeError("missing -- argument separator") from exc

    parser = DriverArgumentParser(prog="blender_polyfork_bake.py")
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--fbx-output", type=Path, required=True)
    parser.add_argument("--texture-output", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args(argv[separator + 1 :])

    if args.fbx_output.suffix != ".fbx":
        parser.error("fbx output path must end in .fbx")
    if args.texture_output.suffix != ".png":
        parser.error("texture output path must end in .png")
    return args


def _build_hash() -> str:
    value = bpy.app.build_hash
    if isinstance(value, bytes):
        return value.decode("ascii", errors="replace")
    return str(value)


def _require_blender_pin() -> None:
    if tuple(bpy.app.version) != BLENDER_VERSION:
        raise RuntimeError(
            "requires Blender 5.1.2; "
            f"found {'.'.join(str(value) for value in bpy.app.version)}"
        )
    if _build_hash() != BLENDER_BUILD_HASH:
        raise RuntimeError(
            f"requires Blender build {BLENDER_BUILD_HASH}; found {_build_hash()}"
        )


def _remove_factory_objects() -> None:
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)


def _import_glb(source: Path) -> None:
    result = bpy.ops.import_scene.gltf(
        filepath=str(source),
        loglevel=1,
        import_pack_images=True,
        merge_vertices=False,
        import_shading="FLAT",
        import_webp_texture=False,
        import_unused_materials=False,
        import_select_created_objects=True,
        import_scene_extras=False,
        import_scene_as_collection=True,
        import_merge_material_slots=True,
    )
    if result != {"FINISHED"}:
        raise RuntimeError(f"GLB import returned {result}")


def _mesh_objects() -> list[object]:
    objects = list(bpy.data.objects)
    if any(obj.type == "CAMERA" for obj in objects):
        raise RuntimeError("source contains a camera")
    if any(obj.type == "LIGHT" for obj in objects):
        raise RuntimeError("source contains a light")
    if any(obj.type == "ARMATURE" for obj in objects):
        raise RuntimeError("source contains an armature or skin")
    unsupported = sorted(
        {obj.type for obj in objects if obj.type not in {"MESH", "EMPTY"}}
    )
    if unsupported:
        raise RuntimeError(
            "unsupported imported object types: " + ", ".join(unsupported)
        )
    mesh_objects = [obj for obj in objects if obj.type == "MESH"]
    if not mesh_objects:
        raise RuntimeError("source contains no mesh")
    for obj in mesh_objects:
        if obj.data.shape_keys is not None:
            raise RuntimeError("source contains shape keys or morph targets")
    return mesh_objects


def _flatten(mesh_objects: list[object]) -> list[object]:
    """Join every mesh into one root-level identity object.

    Polyfork rigged previews carry child pivot groups (e.g. a lantern that
    can swing in three.js). bake_space_transform double-transforms FBX
    children, and the Unity prop lane is render-only anyway (admission
    forbids Animators), so the pivots buy nothing there. Joining first makes
    every asset the same single-mesh shape the existing five props have.
    """
    bpy.ops.object.select_all(action="DESELECT")
    for obj in mesh_objects:
        obj.select_set(True)
    active = mesh_objects[0]
    bpy.context.view_layer.objects.active = active
    if len(mesh_objects) > 1:
        result = bpy.ops.object.join()
        if result != {"FINISHED"}:
            raise RuntimeError(f"mesh join returned {result}")
    if active.parent is not None:
        result = bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
        if result != {"FINISHED"}:
            raise RuntimeError(f"parent clear returned {result}")
    bpy.ops.object.select_all(action="DESELECT")
    active.select_set(True)
    bpy.context.view_layer.objects.active = active
    result = bpy.ops.object.transform_apply(
        location=True, rotation=True, scale=True
    )
    if result != {"FINISHED"}:
        raise RuntimeError(f"transform apply returned {result}")
    for obj in list(bpy.data.objects):
        if obj.type == "EMPTY":
            bpy.data.objects.remove(obj, do_unlink=True)
    return [active]


def _triangle_count(mesh_objects: list[object]) -> int:
    total = 0
    for obj in mesh_objects:
        obj.data.calc_loop_triangles()
        total += len(obj.data.loop_triangles)
    return total


def _world_dimensions(mesh_objects: list[object]) -> dict[str, float]:
    minimum = [float("inf")] * 3
    maximum = [float("-inf")] * 3
    for obj in mesh_objects:
        for corner in obj.bound_box:
            world = obj.matrix_world @ Vector(corner)
            for axis in range(3):
                minimum[axis] = min(minimum[axis], world[axis])
                maximum[axis] = max(maximum[axis], world[axis])
    return {
        "x": round(maximum[0] - minimum[0], 4),
        "y": round(maximum[1] - minimum[1], 4),
        "z": round(maximum[2] - minimum[2], 4),
        "min_y": round(minimum[1], 4),
    }


def _face_srgb_colors(mesh: object) -> list[tuple[int, int, int]]:
    attributes = mesh.color_attributes
    attribute = attributes.active_color
    if attribute is None and len(attributes) > 0:
        attribute = attributes[0]
    if attribute is None:
        raise RuntimeError(f"mesh {mesh.name} has no colour attribute")
    if attribute.domain not in {"CORNER", "POINT"}:
        raise RuntimeError(
            f"mesh {mesh.name} colour domain {attribute.domain} is unsupported"
        )

    colors = []
    for polygon in mesh.polygons:
        accumulated = [0.0, 0.0, 0.0]
        count = 0
        for loop_index in polygon.loop_indices:
            if attribute.domain == "CORNER":
                value = attribute.data[loop_index].color_srgb
            else:
                value = attribute.data[
                    mesh.loops[loop_index].vertex_index
                ].color_srgb
            for axis in range(3):
                accumulated[axis] += value[axis]
            count += 1
        colors.append(
            tuple(
                max(0, min(255, round(255.0 * channel / count)))
                for channel in accumulated
            )
        )
    return colors


def _block_center_uv(block_index: int) -> tuple[float, float]:
    column = block_index % BLOCKS_PER_ROW
    row = block_index // BLOCKS_PER_ROW
    u = (column * BLOCK + BLOCK / 2.0) / ATLAS_SIZE
    # PNG rows are written top-down; UV v=0 is the bottom of the image.
    v = 1.0 - (row * BLOCK + BLOCK / 2.0) / ATLAS_SIZE
    return u, v


def _bake_palette(mesh_objects: list[object]) -> list[tuple[int, int, int]]:
    palette: list[tuple[int, int, int]] = []
    index_of: dict[tuple[int, int, int], int] = {}

    per_mesh_faces = []
    for obj in mesh_objects:
        face_colors = _face_srgb_colors(obj.data)
        per_mesh_faces.append((obj, face_colors))
        for color in face_colors:
            if color not in index_of:
                index_of[color] = len(palette)
                palette.append(color)

    if len(palette) > MAX_COLORS:
        raise RuntimeError(
            f"asset uses {len(palette)} colours; atlas holds {MAX_COLORS}"
        )

    for obj, face_colors in per_mesh_faces:
        mesh = obj.data
        while mesh.uv_layers:
            mesh.uv_layers.remove(mesh.uv_layers[0])
        uv_layer = mesh.uv_layers.new(name="UVMap")
        for polygon, color in zip(mesh.polygons, face_colors):
            u, v = _block_center_uv(index_of[color])
            for loop_index in polygon.loop_indices:
                uv_layer.data[loop_index].uv = (u, v)
        for attribute in list(mesh.color_attributes):
            mesh.color_attributes.remove(attribute)
    return palette


def _write_palette_png(path: Path, palette: list[tuple[int, int, int]]) -> None:
    pixels = bytearray()
    for y in range(ATLAS_SIZE):
        pixels.append(0)  # PNG filter: none
        for x in range(ATLAS_SIZE):
            block_index = (y // BLOCK) * BLOCKS_PER_ROW + (x // BLOCK)
            color = (
                palette[block_index]
                if block_index < len(palette)
                else (255, 0, 255)
            )
            pixels.extend(color)

    def chunk(tag: bytes, payload: bytes) -> bytes:
        return (
            struct.pack(">I", len(payload))
            + tag
            + payload
            + struct.pack(">I", zlib.crc32(tag + payload) & 0xFFFFFFFF)
        )

    header = struct.pack(">IIBBBBB", ATLAS_SIZE, ATLAS_SIZE, 8, 2, 0, 0, 0)
    data = (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", header)
        + chunk(b"IDAT", zlib.compress(bytes(pixels), 9))
        + chunk(b"IEND", b"")
    )
    path.write_bytes(data)
    if not path.is_file() or path.stat().st_size == 0:
        raise RuntimeError("palette PNG was not written")


def _replace_materials(mesh_objects: list[object], name: str) -> None:
    material = bpy.data.materials.new(name)
    for obj in mesh_objects:
        obj.data.materials.clear()
        obj.data.materials.append(material)


def _export_fbx(output: Path) -> None:
    result = bpy.ops.export_scene.fbx(
        filepath=str(output),
        check_existing=False,
        use_selection=False,
        use_visible=False,
        use_active_collection=False,
        object_types={"MESH", "EMPTY"},
        use_mesh_modifiers=True,
        mesh_smooth_type="OFF",
        use_triangles=True,
        add_leaf_bones=False,
        bake_anim=False,
        embed_textures=False,
        path_mode="STRIP",
        use_custom_props=False,
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True,
        axis_forward="-Z",
        axis_up="Y",
    )
    if result != {"FINISHED"}:
        raise RuntimeError(f"FBX export returned {result}")
    if not output.is_file() or output.stat().st_size == 0:
        raise RuntimeError("FBX export did not create a non-empty output")


def _verify_fbx(
    output: Path, expected_triangles: int, expected_dims: dict[str, float]
) -> None:
    _remove_factory_objects()
    result = bpy.ops.import_scene.fbx(filepath=str(output))
    if result != {"FINISHED"}:
        raise RuntimeError(f"FBX verification import returned {result}")
    mesh_objects = [obj for obj in bpy.data.objects if obj.type == "MESH"]
    if not mesh_objects:
        raise RuntimeError("verification: FBX contains no mesh")
    triangles = _triangle_count(mesh_objects)
    if triangles != expected_triangles:
        raise RuntimeError(
            "verification: FBX triangle count "
            f"{triangles} != source {expected_triangles}"
        )
    dims = _world_dimensions(mesh_objects)
    for axis in ("x", "y", "z"):
        if abs(dims[axis] - expected_dims[axis]) > 0.01:
            raise RuntimeError(
                f"verification: FBX {axis} dimension {dims[axis]} "
                f"!= source {expected_dims[axis]}"
            )
    for obj in mesh_objects:
        if not obj.data.uv_layers:
            raise RuntimeError(
                f"verification: FBX mesh {obj.name} lost its UV layer"
            )


def main(argv: list[str]) -> int:
    try:
        args = _arguments(argv)
        _require_blender_pin()
        if not args.source.is_file():
            raise RuntimeError("source GLB is missing")
        for target in (args.fbx_output, args.texture_output, args.report):
            if not target.parent.is_dir():
                raise RuntimeError(f"output directory missing for {target}")

        _remove_factory_objects()
        _import_glb(args.source)
        mesh_objects = _flatten(_mesh_objects())
        source_triangles = _triangle_count(mesh_objects)
        source_dims = _world_dimensions(mesh_objects)

        palette = _bake_palette(mesh_objects)
        _write_palette_png(args.texture_output, palette)
        _replace_materials(mesh_objects, args.fbx_output.stem + "-baked")
        _export_fbx(args.fbx_output)
        _verify_fbx(args.fbx_output, source_triangles, source_dims)

        report = {
            "source": args.source.name,
            "fbx": args.fbx_output.name,
            "texture": args.texture_output.name,
            "triangles": source_triangles,
            "dimensions_m": source_dims,
            "mesh_objects": len(mesh_objects),
            "palette_colors": len(palette),
        }
        args.report.write_text(
            json.dumps(report, indent=2, sort_keys=True) + "\n"
        )
        print("blender-polyfork-bake: " + json.dumps(report, sort_keys=True))
        return 0
    except (OSError, RuntimeError, ValueError) as exc:
        print(f"blender-polyfork-bake: {exc}", file=sys.stderr)
        return EXIT_CODE


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
