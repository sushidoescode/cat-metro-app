#!/usr/bin/env python3
"""Blender-only one-asset GLB collapse-decimation driver."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import bpy


BLENDER_VERSION = (5, 1, 2)
BLENDER_BUILD_HASH = "ec6e62d40fa9"
EXIT_CODE = 97


class DriverArgumentParser(argparse.ArgumentParser):
    def error(self, message: str) -> None:
        raise RuntimeError(message)


def _arguments(argv: list[str]) -> argparse.Namespace:
    try:
        separator = argv.index("--")
    except ValueError as exc:
        raise RuntimeError("missing -- argument separator") from exc

    parser = DriverArgumentParser(prog="blender_decimate.py")
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--source-triangles", type=int, required=True)
    parser.add_argument("--target-triangles", type=int, required=True)
    parser.add_argument("--minimum-triangles", type=int, required=True)
    parser.add_argument("--maximum-triangles", type=int, required=True)
    args = parser.parse_args(argv[separator + 1 :])

    if args.source_triangles <= args.target_triangles:
        parser.error("source triangles must exceed target triangles")
    if not (
        0 < args.minimum_triangles
        <= args.target_triangles
        <= args.maximum_triangles
    ):
        parser.error("triangle policy is inconsistent")
    if args.output.suffix != ".glb":
        parser.error("output path must already end in .glb")
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
    actual_hash = _build_hash()
    if actual_hash != BLENDER_BUILD_HASH:
        raise RuntimeError(
            f"requires Blender build {BLENDER_BUILD_HASH}; found {actual_hash}"
        )


def _remove_factory_objects() -> None:
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)


def _import_source(source: Path) -> None:
    result = bpy.ops.import_scene.gltf(
        filepath=str(source),
        loglevel=1,
        import_pack_images=True,
        merge_vertices=True,
        import_shading="SMOOTH",
        import_webp_texture=False,
        import_unused_materials=False,
        import_select_created_objects=True,
        import_scene_extras=False,
        import_scene_as_collection=True,
        import_merge_material_slots=True,
    )
    if result != {"FINISHED"}:
        raise RuntimeError(f"GLB import returned {result}")


def _static_mesh_objects() -> list[object]:
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
    if bpy.data.actions or any(obj.animation_data is not None for obj in objects):
        raise RuntimeError("source contains animation data or actions")

    mesh_objects = [obj for obj in objects if obj.type == "MESH"]
    if not mesh_objects:
        raise RuntimeError("source contains no mesh")
    if len(mesh_objects) != 1:
        raise RuntimeError("source must contain exactly one mesh")

    mesh_object = mesh_objects[0]
    mesh = mesh_object.data
    if mesh.shape_keys is not None:
        raise RuntimeError("source contains shape keys or morph targets")
    if any(modifier.type == "ARMATURE" for modifier in mesh_object.modifiers):
        raise RuntimeError("source contains an armature modifier or skin")
    if len(mesh_object.material_slots) != 1:
        raise RuntimeError("source must contain exactly one material primitive")
    material_indexes = {polygon.material_index for polygon in mesh.polygons}
    if material_indexes != {0}:
        raise RuntimeError("source must contain exactly one mesh primitive")
    return mesh_objects


def apply_modifier(obj: object, modifier: object) -> None:
    selection_result = bpy.ops.object.select_all(action="DESELECT")
    if selection_result != {"FINISHED"}:
        raise RuntimeError(f"object deselection returned {selection_result}")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    result = bpy.ops.object.modifier_apply(
        modifier=modifier.name,
        report=True,
        merge_customdata=True,
        single_user=True,
        all_keyframes=False,
        use_selected_objects=False,
    )
    if result != {"FINISHED"}:
        raise RuntimeError(f"modifier apply returned {result}")


def _triangle_count(mesh_objects: list[object]) -> int:
    total = 0
    for obj in mesh_objects:
        mesh = obj.data
        mesh.calc_loop_triangles()
        total += len(mesh.loop_triangles)
    return total


def _decimate(args: argparse.Namespace, mesh_objects: list[object]) -> None:
    for obj in mesh_objects:
        triangulate = obj.modifiers.new("CatMetroTriangulate", "TRIANGULATE")
        apply_modifier(obj, triangulate)

    measured_source = _triangle_count(mesh_objects)
    if measured_source != args.source_triangles:
        raise RuntimeError(
            "in-memory source triangle count "
            f"{measured_source} disagrees with inspector count "
            f"{args.source_triangles}"
        )

    ratio = args.target_triangles / args.source_triangles
    for obj in mesh_objects:
        decimate = obj.modifiers.new("CatMetroCollapseDecimate", "DECIMATE")
        decimate.decimate_type = "COLLAPSE"
        decimate.ratio = ratio
        decimate.use_collapse_triangulate = True
        apply_modifier(obj, decimate)

    output_triangles = _triangle_count(mesh_objects)
    if not args.minimum_triangles <= output_triangles <= args.maximum_triangles:
        raise RuntimeError(
            "decimated in-memory triangle count "
            f"{output_triangles} is outside "
            f"{args.minimum_triangles}..{args.maximum_triangles}"
        )


def _export_output(output: Path) -> None:
    result = bpy.ops.export_scene.gltf(
        filepath=str(output),
        check_existing=False,
        export_format="GLB",
        export_image_format="AUTO",
        export_image_add_webp=False,
        export_image_webp_fallback=False,
        export_keep_originals=False,
        export_texcoords=True,
        export_normals=True,
        export_tangents=False,
        export_materials="EXPORT",
        export_unused_images=False,
        export_unused_textures=False,
        export_attributes=False,
        export_gn_mesh=False,
        use_mesh_edges=False,
        use_mesh_vertices=False,
        use_selection=False,
        use_visible=False,
        use_renderable=False,
        use_active_collection=False,
        use_active_scene=True,
        export_extras=False,
        export_yup=True,
        export_apply=False,
        export_animations=False,
        export_skins=False,
        export_morph=False,
        export_cameras=False,
        export_lights=False,
        export_draco_mesh_compression_enable=False,
        export_use_gltfpack=False,
        export_gpu_instances=False,
        export_hierarchy_full_collections=False,
        export_extra_animations=False,
        will_save_settings=False,
    )
    if result != {"FINISHED"}:
        raise RuntimeError(f"GLB export returned {result}")
    if not output.is_file() or output.stat().st_size == 0:
        raise RuntimeError("GLB export did not create a non-empty output")


def main(argv: list[str]) -> int:
    try:
        args = _arguments(argv)
        _require_blender_pin()
        if not args.source.is_file():
            raise RuntimeError("source GLB is missing")
        if not args.output.parent.is_dir():
            raise RuntimeError("output directory is missing")
        if args.output.exists():
            raise RuntimeError("staged output already exists")
        _remove_factory_objects()
        _import_source(args.source)
        mesh_objects = _static_mesh_objects()
        _decimate(args, mesh_objects)
        _export_output(args.output)
        return 0
    except (OSError, RuntimeError, ValueError) as exc:
        print(f"blender-decimate: {exc}", file=sys.stderr)
        return EXIT_CODE


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
