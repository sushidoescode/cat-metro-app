#!/usr/bin/env python3
"""Headless proof render: FBX + baked palette atlas -> one PNG frame.

URP materials fail silently (docs/lessons: a prop can pass every test and
still render as a flat grey ghost), so the furnish pipeline proves its
colour bake with an actual render before anything reaches Unity. Uses the
Workbench engine in TEXTURE mode: no GPU features, deterministic headless.
"""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


EXIT_CODE = 95


class DriverArgumentParser(argparse.ArgumentParser):
    def error(self, message: str) -> None:
        raise RuntimeError(message)


def _arguments(argv: list[str]) -> argparse.Namespace:
    try:
        separator = argv.index("--")
    except ValueError as exc:
        raise RuntimeError("missing -- argument separator") from exc
    parser = DriverArgumentParser(prog="blender_polyfork_proof.py")
    parser.add_argument("--fbx", type=Path, required=True)
    parser.add_argument("--texture", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    return parser.parse_args(argv[separator + 1 :])


def _remove_factory_objects() -> None:
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)


def main(argv: list[str]) -> int:
    try:
        args = _arguments(argv)
        for path in (args.fbx, args.texture):
            if not path.is_file():
                raise RuntimeError(f"missing input: {path}")

        _remove_factory_objects()
        result = bpy.ops.import_scene.fbx(filepath=str(args.fbx))
        if result != {"FINISHED"}:
            raise RuntimeError(f"FBX import returned {result}")
        meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
        if not meshes:
            raise RuntimeError("FBX contains no mesh")

        image = bpy.data.images.load(str(args.texture))
        material = bpy.data.materials.new("proof-baked")
        material.use_nodes = True
        nodes = material.node_tree.nodes
        principled = nodes["Principled BSDF"]
        texture = nodes.new("ShaderNodeTexImage")
        texture.image = image
        texture.interpolation = "Closest"
        material.node_tree.links.new(
            texture.outputs["Color"], principled.inputs["Base Color"]
        )
        for obj in meshes:
            obj.data.materials.clear()
            obj.data.materials.append(material)

        minimum = Vector((math.inf,) * 3)
        maximum = Vector((-math.inf,) * 3)
        for obj in meshes:
            for corner in obj.bound_box:
                world = obj.matrix_world @ Vector(corner)
                minimum = Vector(map(min, minimum, world))
                maximum = Vector(map(max, maximum, world))
        center = (minimum + maximum) / 2.0
        radius = max((maximum - minimum).length / 2.0, 0.1)

        camera_data = bpy.data.cameras.new("proof-camera")
        camera = bpy.data.objects.new("proof-camera", camera_data)
        bpy.context.scene.collection.objects.link(camera)
        direction = Vector((1.0, -1.2, 0.7)).normalized()
        camera.location = center + direction * radius * 2.6
        camera.rotation_euler = (
            (center - camera.location).to_track_quat("-Z", "Y").to_euler()
        )
        bpy.context.scene.camera = camera

        scene = bpy.context.scene
        scene.render.engine = "BLENDER_WORKBENCH"
        shading = scene.display.shading
        shading.light = "STUDIO"
        shading.color_type = "TEXTURE"
        scene.render.resolution_x = 512
        scene.render.resolution_y = 512
        scene.render.filepath = str(args.output)
        scene.render.image_settings.file_format = "PNG"
        result = bpy.ops.render.render(write_still=True)
        if result != {"FINISHED"}:
            raise RuntimeError(f"render returned {result}")
        if not args.output.is_file() or args.output.stat().st_size == 0:
            raise RuntimeError("render did not write an output")
        print(f"blender-polyfork-proof: rendered {args.output.name}")
        return 0
    except (OSError, RuntimeError, ValueError) as exc:
        print(f"blender-polyfork-proof: {exc}", file=sys.stderr)
        return EXIT_CODE


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
