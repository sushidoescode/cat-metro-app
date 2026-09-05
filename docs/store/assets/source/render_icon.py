#!/usr/bin/env python3
"""Render the admitted Cat Metro rig as the store-icon conductor portrait.

Run with Blender, not the system Python:

    blender --background --factory-startup --python render_icon.py -- \
      --source /absolute/path/to/model.glb --output /tmp/cat-metro-icon-rig.png

The imported provider mesh is only assigned a temporary material in Blender's
in-memory scene. Its pinned bytes and geometry are never written or reshaped.
"""

from __future__ import annotations

import argparse
import hashlib
import math
import sys
from pathlib import Path

import bpy


EXPECTED_SOURCE_SHA256 = (
    "e9bcbb70f8fbc803b926b505c5ab4eb57fdad5bc3173498adf0b732080516a39"
)


def blender_args() -> argparse.Namespace:
    separator = sys.argv.index("--") if "--" in sys.argv else len(sys.argv)
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    return parser.parse_args(sys.argv[separator + 1 :])


def srgb_channel(value: int) -> float:
    encoded = value / 255.0
    if encoded <= 0.04045:
        return encoded / 12.92
    return ((encoded + 0.055) / 1.055) ** 2.4


def rgba(hex_value: str) -> tuple[float, float, float, float]:
    value = hex_value.removeprefix("#")
    return tuple(srgb_channel(int(value[i : i + 2], 16)) for i in (0, 2, 4)) + (1.0,)


def material(name: str, hex_value: str, roughness: float = 0.84) -> bpy.types.Material:
    result = bpy.data.materials.new(name)
    result.diffuse_color = rgba(hex_value)
    result.use_nodes = True
    principled = result.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = rgba(hex_value)
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Metallic"].default_value = 0.0
    return result


def add_uv_sphere(
    name: str,
    location: tuple[float, float, float],
    scale: tuple[float, float, float],
    surface: bpy.types.Material,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_uv_sphere_add(segments=64, ring_count=32, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(surface)
    bpy.ops.object.shade_smooth()
    return obj


def add_rounded_cube(
    name: str,
    location: tuple[float, float, float],
    scale: tuple[float, float, float],
    bevel: float,
    surface: bpy.types.Material,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(surface)
    modifier = obj.modifiers.new("modeled rounded edge", "BEVEL")
    modifier.width = bevel
    modifier.segments = 6
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.ops.object.shade_smooth()
    return obj


def add_curve(
    name: str,
    points: list[tuple[float, float, float]],
    bevel_depth: float,
    surface: bpy.types.Material,
) -> bpy.types.Object:
    curve_data = bpy.data.curves.new(name, type="CURVE")
    curve_data.dimensions = "3D"
    curve_data.bevel_depth = bevel_depth
    curve_data.bevel_resolution = 5
    spline = curve_data.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for point, coordinate in zip(spline.bezier_points, points, strict=True):
        point.co = coordinate
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve_data)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(surface)
    return obj


def main() -> None:
    args = blender_args()
    source = args.source.resolve(strict=True)
    output = args.output.resolve()
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    if digest != EXPECTED_SOURCE_SHA256:
        raise RuntimeError(f"licensed rig SHA-256 mismatch: {digest}")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(source))

    cream = material("Cream Card #F2EAD9", "F2EAD9")
    navy = material("Ink Navy #22304A", "22304A", roughness=0.78)
    teal = material("Metro Teal #3BAFA8", "3BAFA8", roughness=0.8)

    imported_meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    cat_meshes = [obj for obj in imported_meshes if obj.data.materials]
    if len(cat_meshes) != 1:
        names = ", ".join(obj.name for obj in imported_meshes)
        raise RuntimeError(f"expected one material-bearing cat mesh, found: {names}")
    cat = cat_meshes[0]
    for helper in imported_meshes:
        if helper is not cat:
            helper.hide_render = True
    cat.name = "Admitted licensed cat rig — temporary cream presentation"
    cat.data.materials.clear()
    cat.data.materials.append(cream)

    # A restrained, modeled conductor cap. It sits between the source ears; the
    # crown, brim, and badge all fit the brief's centered 512 px safe square.
    add_rounded_cube("Navy cap crown", (-0.414, 0.0, 0.864), (0.045, 0.205, 0.057), 0.042, navy)
    add_uv_sphere("Navy cap brim", (-0.472, 0.0, 0.806), (0.052, 0.245, 0.031), navy)
    add_uv_sphere("Teal cap badge", (-0.478, 0.0, 0.870), (0.014, 0.032, 0.032), teal)

    # The original texture is deliberately not reused: the smaller navy forms
    # make a calm, adult-premium expression and preserve the two-value read.
    add_uv_sphere("Left calm eye", (-0.402, -0.126, 0.687), (0.023, 0.050, 0.029), navy)
    add_uv_sphere("Right calm eye", (-0.402, 0.126, 0.687), (0.023, 0.050, 0.029), navy)
    add_uv_sphere("Nose", (-0.462, 0.0, 0.573), (0.019, 0.035, 0.022), navy)
    add_curve(
        "Quiet mouth",
        [(-0.474, -0.052, 0.545), (-0.483, 0.0, 0.528), (-0.474, 0.052, 0.545)],
        0.008,
        navy,
    )

    world = bpy.data.worlds.new("Transparent studio")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = rgba("FAF6EC")
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.45
    bpy.context.scene.world = world

    light_data = bpy.data.lights.new("Warm upper-left key", type="AREA")
    light_data.energy = 760.0
    light_data.color = (1.0, 0.79, 0.60)
    light_data.shape = "DISK"
    light_data.size = 2.8
    light = bpy.data.objects.new(light_data.name, light_data)
    light.location = (-3.4, -2.4, 3.5)
    bpy.context.collection.objects.link(light)

    fill_data = bpy.data.lights.new("Cool restrained fill", type="AREA")
    fill_data.energy = 300.0
    fill_data.color = (0.68, 0.79, 1.0)
    fill_data.size = 3.0
    fill = bpy.data.objects.new(fill_data.name, fill_data)
    fill.location = (-2.0, 2.8, 1.9)
    bpy.context.collection.objects.link(fill)

    camera_data = bpy.data.cameras.new("Square icon camera")
    camera = bpy.data.objects.new(camera_data.name, camera_data)
    camera.location = (-4.0, 0.0, 0.72)
    target = (0.0, 0.0, 0.72)
    direction = tuple(target[index] - camera.location[index] for index in range(3))
    camera.rotation_euler = mathutils.Vector(direction).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 0.88
    camera.data.lens = 52.0
    bpy.context.collection.objects.link(camera)
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.film_transparent = True
    scene.render.filepath = str(output)
    scene.render.image_settings.compression = 15
    scene.render.use_file_extension = True
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    scene.render.resolution_percentage = 100

    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.render.render(write_still=True)
    print(f"store icon rig source sha256={digest}")
    print(f"store icon transparent render={output}")


if __name__ == "__main__":
    import mathutils

    main()
