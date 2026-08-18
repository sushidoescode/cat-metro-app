#!/usr/bin/env python3
"""Blender-only driver for the two frozen Cat Metro source-art curations."""

from __future__ import annotations

import argparse
import json
import math
import sys
from dataclasses import dataclass
from pathlib import Path

import bpy


sys.dont_write_bytecode = True

_SCRIPT_DIR = Path(__file__).resolve().parent
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from glb_curation_rules import (  # noqa: E402
    CurationRuleError,
    loaf_cutoff,
    select_non_largest_components,
    validate_loaf_footprints,
)


BLENDER_VERSION = (5, 1, 2)
BLENDER_BUILD_HASH = "ec6e62d40fa9"
EXIT_CODE = 97
LOAF_ID = "cat-blue-siamese-loaf"
WAVE_ID = "cat-yellow-longhair-wave"
EXPECTED_SOURCE_TRIANGLES = {
    LOAF_ID: 1_427_775,
}
EXPECTED_CURATED_TRIANGLES = {
    LOAF_ID: 773_061,
    WAVE_ID: 1_383_894,
}
EXPECTED_WAVE_INPUT_COMPONENT_TRIANGLES = {
    1_494_090: [1_383_894, 71_282, 38_914],
    1_422_808: [1_383_894, 38_914],
}
EXPECTED_WAVE_RETAINED_COMPONENT_TRIANGLES = [1_383_894]


class DriverArgumentParser(argparse.ArgumentParser):
    def error(self, message: str) -> None:
        raise RuntimeError(message)


@dataclass
class Component:
    vertices: set[int]
    triangles: int
    minimum: tuple[float, float, float]
    maximum: tuple[float, float, float]

    def record(self) -> dict[str, object]:
        return {
            "triangles": self.triangles,
            "minimum": list(self.minimum),
            "maximum": list(self.maximum),
        }


def _arguments(argv: list[str]) -> argparse.Namespace:
    try:
        separator = argv.index("--")
    except ValueError as exc:
        raise RuntimeError("missing -- argument separator") from exc
    parser = DriverArgumentParser(prog="blender_curate.py")
    parser.add_argument(
        "--operation", choices=("curate", "verify-curated"), required=True
    )
    parser.add_argument("--asset-id", choices=(LOAF_ID, WAVE_ID), required=True)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args(argv[separator + 1 :])
    if args.operation == "curate":
        if args.output is None or args.report is None:
            parser.error("curate requires --output and --report")
    elif args.output is not None or args.report is not None:
        parser.error("verify-curated does not accept --output or --report")
    return args


def _build_hash() -> str:
    value = bpy.app.build_hash
    if isinstance(value, bytes):
        return value.decode("ascii", errors="replace")
    return str(value)


def _require_blender_pin() -> None:
    if tuple(bpy.app.version) != BLENDER_VERSION:
        raise RuntimeError("requires Blender 5.1.2")
    if _build_hash() != BLENDER_BUILD_HASH:
        raise RuntimeError(f"requires Blender build {BLENDER_BUILD_HASH}")


def _clear_scene() -> None:
    result = bpy.ops.wm.read_factory_settings(use_empty=True)
    if result != {"FINISHED"}:
        raise RuntimeError(f"factory reset returned {result}")


def _import_source(source: Path) -> object:
    if not source.is_file() or source.is_symlink():
        raise RuntimeError("source must be a regular non-symlink file")
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
    objects = list(bpy.data.objects)
    if any(obj.type == "CAMERA" for obj in objects):
        raise RuntimeError("source contains a camera")
    if any(obj.type == "LIGHT" for obj in objects):
        raise RuntimeError("source contains a light")
    if any(obj.type == "ARMATURE" for obj in objects):
        raise RuntimeError("source contains an armature")
    unsupported = sorted(
        {obj.type for obj in objects if obj.type not in {"MESH", "EMPTY"}}
    )
    if unsupported:
        raise RuntimeError("source contains unsupported object types")
    if bpy.data.actions or any(obj.animation_data is not None for obj in objects):
        raise RuntimeError("source contains animation data")
    meshes = [obj for obj in objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError("source must contain exactly one mesh")
    mesh_object = meshes[0]
    mesh = mesh_object.data
    if mesh.shape_keys is not None:
        raise RuntimeError("source contains shape keys")
    if len(mesh_object.material_slots) != 1:
        raise RuntimeError("source must contain exactly one material slot")
    if {polygon.material_index for polygon in mesh.polygons} != {0}:
        raise RuntimeError("source must contain exactly one material primitive")
    return mesh_object


def _gltf_point(point: object) -> tuple[float, float, float]:
    # Blender imports glTF Y-up as (x, -z, y). Convert world coordinates back.
    return (float(point.x), float(point.z), float(-point.y))


def _world_positions(mesh_object: object) -> list[tuple[float, float, float]]:
    matrix = mesh_object.matrix_world
    positions = [_gltf_point(matrix @ vertex.co) for vertex in mesh_object.data.vertices]
    if not positions or any(
        not math.isfinite(component)
        for position in positions
        for component in position
    ):
        raise RuntimeError("mesh positions must be finite and non-empty")
    return positions


def _bounds(
    positions: list[tuple[float, float, float]],
) -> dict[str, tuple[float, float, float]]:
    return {
        "minimum": tuple(min(point[axis] for point in positions) for axis in range(3)),
        "maximum": tuple(max(point[axis] for point in positions) for axis in range(3)),
    }


def _triangle_count(mesh_object: object) -> int:
    mesh_object.data.calc_loop_triangles()
    return len(mesh_object.data.loop_triangles)


def _components(mesh_object: object) -> list[Component]:
    mesh = mesh_object.data
    mesh.calc_loop_triangles()
    positions = _world_positions(mesh_object)
    parent = list(range(len(positions)))

    def find(value: int) -> int:
        while parent[value] != value:
            parent[value] = parent[parent[value]]
            value = parent[value]
        return value

    def union(left: int, right: int) -> None:
        left_root = find(left)
        right_root = find(right)
        if left_root != right_root:
            parent[right_root] = left_root

    for triangle in mesh.loop_triangles:
        first, second, third = triangle.vertices
        union(first, second)
        union(first, third)

    vertices_by_root: dict[int, set[int]] = {}
    triangles_by_root: dict[int, int] = {}
    for triangle in mesh.loop_triangles:
        root = find(triangle.vertices[0])
        vertices_by_root.setdefault(root, set()).update(triangle.vertices)
        triangles_by_root[root] = triangles_by_root.get(root, 0) + 1

    components: list[Component] = []
    for root, vertex_indexes in vertices_by_root.items():
        component_positions = [positions[index] for index in vertex_indexes]
        component_bounds = _bounds(component_positions)
        components.append(
            Component(
                vertices=vertex_indexes,
                triangles=triangles_by_root[root],
                minimum=component_bounds["minimum"],
                maximum=component_bounds["maximum"],
            )
        )
    components.sort(key=lambda component: component.triangles, reverse=True)
    return components


def _delete_vertices(mesh_object: object, selected: set[int]) -> None:
    if not selected or len(selected) >= len(mesh_object.data.vertices):
        raise RuntimeError("curation vertex selection is empty or exhaustive")
    result = bpy.ops.object.select_all(action="DESELECT")
    if result != {"FINISHED"}:
        raise RuntimeError(f"object deselection returned {result}")
    mesh_object.select_set(True)
    bpy.context.view_layer.objects.active = mesh_object
    result = bpy.ops.object.mode_set(mode="EDIT")
    if result != {"FINISHED"}:
        raise RuntimeError(f"edit-mode entry returned {result}")
    result = bpy.ops.mesh.select_all(action="DESELECT")
    if result != {"FINISHED"}:
        raise RuntimeError(f"vertex deselection returned {result}")
    result = bpy.ops.object.mode_set(mode="OBJECT")
    if result != {"FINISHED"}:
        raise RuntimeError(f"object-mode entry returned {result}")
    for vertex in mesh_object.data.vertices:
        vertex.select = vertex.index in selected
    result = bpy.ops.object.mode_set(mode="EDIT")
    if result != {"FINISHED"}:
        raise RuntimeError(f"edit-mode re-entry returned {result}")
    result = bpy.ops.mesh.delete(type="VERT")
    if result != {"FINISHED"}:
        raise RuntimeError(f"vertex deletion returned {result}")
    result = bpy.ops.object.mode_set(mode="OBJECT")
    if result != {"FINISHED"}:
        raise RuntimeError(f"object-mode restore returned {result}")
    mesh_object.data.update()


def _span(bounds: dict[str, tuple[float, float, float]], axis: int) -> float:
    return bounds["maximum"][axis] - bounds["minimum"][axis]


def _curate_loaf(mesh_object: object) -> dict[str, object]:
    before_triangles = _triangle_count(mesh_object)
    if before_triangles != EXPECTED_SOURCE_TRIANGLES[LOAF_ID]:
        raise RuntimeError("loaf source triangle anchor mismatch")
    positions = _world_positions(mesh_object)
    full_bounds = _bounds(positions)
    cutoff = loaf_cutoff(full_bounds["minimum"][1], full_bounds["maximum"][1])
    selected = {
        index for index, position in enumerate(positions) if position[1] < cutoff
    }
    retained = [
        position for index, position in enumerate(positions) if index not in selected
    ]
    if not selected or not retained:
        raise RuntimeError("loaf cut produced an empty partition")
    selected_bounds = _bounds([positions[index] for index in selected])
    retained_bounds = _bounds(retained)
    full_width = _span(full_bounds, 0)
    full_depth = _span(full_bounds, 2)
    if full_width <= 0.0 or full_depth <= 0.0:
        raise RuntimeError("loaf horizontal footprint must be positive")
    ratios = {
        "selected_width_ratio": _span(selected_bounds, 0) / full_width,
        "selected_depth_ratio": _span(selected_bounds, 2) / full_depth,
        "retained_width_ratio": _span(retained_bounds, 0) / full_width,
        "retained_depth_ratio": _span(retained_bounds, 2) / full_depth,
    }
    validate_loaf_footprints(**ratios)
    _delete_vertices(mesh_object, selected)
    after_triangles = _triangle_count(mesh_object)
    if after_triangles != EXPECTED_CURATED_TRIANGLES[LOAF_ID]:
        raise RuntimeError("loaf curated triangle anchor mismatch")
    return {
        "asset_id": LOAF_ID,
        "operation": "min-y-plinth-strip",
        "triangles_before": before_triangles,
        "triangles_after": after_triangles,
        "triangles_removed": before_triangles - after_triangles,
        "selected_vertices": len(selected),
        "cutoff_y": cutoff,
        "full_bounds": full_bounds,
        "retained_bounds": _bounds(_world_positions(mesh_object)),
        "footprint_ratios": ratios,
    }


def _curate_wave(mesh_object: object) -> dict[str, object]:
    before_triangles = _triangle_count(mesh_object)
    expected_components = EXPECTED_WAVE_INPUT_COMPONENT_TRIANGLES.get(before_triangles)
    if expected_components is None:
        raise RuntimeError("wave source triangle anchor mismatch")
    positions = _world_positions(mesh_object)
    full_bounds = _bounds(positions)
    components = _components(mesh_object)
    component_records = [component.record() for component in components]
    if [component.triangles for component in components] != expected_components:
        raise RuntimeError("wave component triangle anchors mismatch")
    selected_indexes = select_non_largest_components(component_records, kind="cat")
    if selected_indexes != list(range(1, len(components))):
        raise RuntimeError("wave selector did not isolate every non-largest component")
    selected_vertices = set().union(
        *(components[index].vertices for index in selected_indexes)
    )
    _delete_vertices(mesh_object, selected_vertices)
    after_triangles = _triangle_count(mesh_object)
    if after_triangles != EXPECTED_CURATED_TRIANGLES[WAVE_ID]:
        raise RuntimeError("wave curated triangle anchor mismatch")
    retained_components = _components(mesh_object)
    if [component.triangles for component in retained_components] != (
        EXPECTED_WAVE_RETAINED_COMPONENT_TRIANGLES
    ):
        raise RuntimeError("wave retained components mismatch")
    return {
        "asset_id": WAVE_ID,
        "operation": "non-largest-component-strip",
        "triangles_before": before_triangles,
        "triangles_after": after_triangles,
        "triangles_removed": before_triangles - after_triangles,
        "selected_vertices": len(selected_vertices),
        "full_bounds": full_bounds,
        "components_before": component_records,
        "components_after": [component.record() for component in retained_components],
        "selected_component_indexes": selected_indexes,
    }


def _verify_curated(asset_id: str, mesh_object: object) -> dict[str, object]:
    positions = _world_positions(mesh_object)
    full_bounds = _bounds(positions)
    if asset_id == LOAF_ID:
        cutoff = loaf_cutoff(
            full_bounds["minimum"][1], full_bounds["maximum"][1]
        )
        selected = [position for position in positions if position[1] < cutoff]
        retained = [position for position in positions if position[1] >= cutoff]
        if not selected or not retained:
            raise RuntimeError("loaf verification partition is empty")
        selected_bounds = _bounds(selected)
        retained_bounds = _bounds(retained)
        full_width = _span(full_bounds, 0)
        full_depth = _span(full_bounds, 2)
        try:
            validate_loaf_footprints(
                selected_width_ratio=_span(selected_bounds, 0) / full_width,
                selected_depth_ratio=_span(selected_bounds, 2) / full_depth,
                retained_width_ratio=_span(retained_bounds, 0) / full_width,
                retained_depth_ratio=_span(retained_bounds, 2) / full_depth,
            )
        except CurationRuleError:
            pass
        else:
            raise RuntimeError("loaf display plinth remains")
        component_count = len(_components(mesh_object))
    else:
        components = _components(mesh_object)
        records = [component.record() for component in components]
        selected_indexes = select_non_largest_components(records, kind="cat")
        if selected_indexes:
            raise RuntimeError("wave non-largest component remains")
        if len(components) != 1:
            raise RuntimeError("wave curated component count must be one")
        component_count = len(components)
    return {
        "asset_id": asset_id,
        "operation": "verify-curated",
        "triangles": _triangle_count(mesh_object),
        "components": component_count,
        "full_bounds": full_bounds,
        "curated": True,
    }


def _export_output(output: Path) -> None:
    if output.suffix != ".glb":
        raise RuntimeError("output must end in .glb")
    if not output.parent.is_dir() or output.exists() or output.is_symlink():
        raise RuntimeError("output must be a new file in an existing directory")
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


def _write_report(path: Path, report: dict[str, object]) -> None:
    if not path.parent.is_dir() or path.exists() or path.is_symlink():
        raise RuntimeError("report must be a new file in an existing directory")
    with path.open("x", encoding="utf-8", newline="\n") as handle:
        json.dump(report, handle, allow_nan=False, indent=2, sort_keys=True)
        handle.write("\n")


def main(argv: list[str]) -> int:
    try:
        args = _arguments(argv)
        _require_blender_pin()
        _clear_scene()
        mesh_object = _import_source(args.source)
        if args.operation == "verify-curated":
            report = _verify_curated(args.asset_id, mesh_object)
            print("blender-curate: " + json.dumps(report, allow_nan=False, sort_keys=True))
            return 0
        if args.asset_id == LOAF_ID:
            report = _curate_loaf(mesh_object)
        else:
            report = _curate_wave(mesh_object)
        _export_output(args.output)
        _write_report(args.report, report)
        print("blender-curate: curated " + args.asset_id)
        return 0
    except (CurationRuleError, OSError, RuntimeError, ValueError) as exc:
        message = str(exc) or "curation failed"
        if len(message) > 300:
            message = message[:297] + "..."
        print(f"blender-curate: {message}", file=sys.stderr)
        return EXIT_CODE


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
