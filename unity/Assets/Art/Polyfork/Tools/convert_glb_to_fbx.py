"""Offline Polyfork GLB -> Unity FBX conversion; no runtime importer dependency."""

import pathlib
import sys

import bpy


DECOR_PALETTE = (
    (0xF2 / 255.0, 0xEA / 255.0, 0xD9 / 255.0),  # Cream Card
    (0xFA / 255.0, 0xF6 / 255.0, 0xEC / 255.0),  # Warm Paper
    (0x22 / 255.0, 0x30 / 255.0, 0x4A / 255.0),  # Ink Navy
    (0x13 / 255.0, 0x1C / 255.0, 0x30 / 255.0),  # Depot Navy
    (0x3B / 255.0, 0xAF / 255.0, 0xA8 / 255.0),  # Metro Teal
    (0xF0 / 255.0, 0x8A / 255.0, 0x3C / 255.0),  # Ticket Orange
)


def nearest_palette(rgb):
    return min(
        DECOR_PALETTE,
        key=lambda candidate: sum((rgb[i] - candidate[i]) ** 2 for i in range(3)),
    )


def remap_vertex_colors(mesh):
    if not mesh.color_attributes:
        attribute = mesh.color_attributes.new(
            name="Color", type="BYTE_COLOR", domain="CORNER"
        )
        for item in attribute.data:
            item.color_srgb = (*DECOR_PALETTE[1], 1.0)
        return

    for attribute in mesh.color_attributes:
        for item in attribute.data:
            source = item.color_srgb
            item.color_srgb = (*nearest_palette(source[:3]), source[3])


def arguments():
    separator = sys.argv.index("--")
    source, destination = sys.argv[separator + 1 : separator + 3]
    return pathlib.Path(source).resolve(), pathlib.Path(destination).resolve()


source_path, destination_path = arguments()
if not source_path.is_file():
    raise SystemExit(f"source GLB is missing: {source_path}")
destination_path.parent.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source_path), import_pack_images=True)

for obj in bpy.context.scene.objects:
    if obj.type == "MESH":
        remap_vertex_colors(obj.data)
    obj.select_set(obj.type in {"MESH", "EMPTY"})

bpy.ops.export_scene.fbx(
    filepath=str(destination_path),
    use_selection=True,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS",
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="STRIP",
    mesh_smooth_type="FACE",
)

print(f"CAT_METRO_CONVERTED {source_path.name} -> {destination_path.name}")
