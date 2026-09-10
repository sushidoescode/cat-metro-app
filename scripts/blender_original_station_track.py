#!/usr/bin/env python3
"""Create Cat Metro's original station/track study; no imported art or network IO.

Blender 5.1.2:
  blender --background --factory-startup --python-exit-code 97 --python scripts/blender_original_station_track.py -- \
    --output-dir docs/design/assets/original-station-track

The three asset meshes share one original 1024x512 colour/wood atlas. All bevels
are applied before export. Preview staging is excluded from the asset exports.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

import bmesh
import bpy
from mathutils import Vector
import numpy as np


PALETTE = [
    ("cream", "F1DDB6"), ("wood", "CCA16C"),
    ("red", "CF533E"), ("navy", "293D50"),
    ("bench", "A87646"), ("teal", "497F7D"),
    ("ivory", "FFF0CF"), ("brass", "D3AC65"),
]
ASSET_IDS = ("original-station-circle", "original-track-straight", "original-track-curve")
PARTS: list[bpy.types.Object] = []
ATLAS_MATERIAL = None


def arguments():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--samples", type=int, default=48)
    parser.add_argument("--skip-renders", action="store_true")
    return parser.parse_args(sys.argv[sys.argv.index("--") + 1:])


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def atlas(output):
    """Eight quiet wood/paint swatches, analytically generated; no photo inputs."""
    global ATLAS_MATERIAL
    pixels = np.ones((512, 1024, 4), dtype=np.float32)
    y, x = np.mgrid[0:256, 0:256].astype(np.float32) / 255
    rng = np.random.default_rng(20260910)
    for index, (name, value) in enumerate(PALETTE):
        base = np.array([int(value[i:i + 2], 16) / 255 for i in (0, 2, 4)])
        # Grain bends slowly rather than making the evenly spaced ladder stripes
        # visible in the old board. Paint has much less colour variation than wood.
        wave = y * 37 + 1.5 * np.sin(x * 5.6) + .45 * np.sin(x * 17 + y * 3)
        grain = .40 * np.sin(wave) + .22 * np.sin(wave * 2.19 + x * 4)
        fibre = np.sin(y * 387 + np.sin(x * 8) * 7) * .08
        noise = rng.normal(0, .035, x.shape)
        strength = .055 if name in ("wood", "bench") else .019
        modulation = 1 + strength * (grain + fibre + noise)
        rgb = np.clip(base[None, None, :] * modulation[:, :, None], 0, 1)
        row, col = divmod(index, 4)
        pixels[row * 256:(row + 1) * 256, col * 256:(col + 1) * 256, :3] = rgb
    image = bpy.data.images.new("OriginalToyAtlas", width=1024, height=512, alpha=False)
    image.colorspace_settings.name = "sRGB"
    image.pixels.foreach_set(pixels.reshape(-1))
    image.filepath_raw = str(output / "Textures" / "original-toy-atlas.png")
    image.file_format = "PNG"
    image.save()
    image.pack()
    material = bpy.data.materials.new("OriginalToyAtlas_Matte")
    material.use_nodes = True
    material.diffuse_color = (.7, .5, .3, 1)
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = .72
    bsdf.inputs["Specular IOR Level"].default_value = .24
    texture = material.node_tree.nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = "Linear"
    material.node_tree.links.new(texture.outputs["Color"], bsdf.inputs["Base Color"])
    ATLAS_MATERIAL = material
    return image


def active(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def finish(obj, swatch, bevel=0.0, segments=3):
    active(obj)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        modifier = obj.modifiers.new("Soft toy edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = segments
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    # Recalculate closed solids independently of primitive/custom-mesh winding.
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(obj.data)
    bm.free()
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    modifier = obj.modifiers.new("Weighted toy normals", "WEIGHTED_NORMAL")
    modifier.keep_sharp = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    # Face projection has a 12-pixel swatch gutter. Grain follows each object's
    # long local dimension; all exported UVs use the single shared image.
    mesh = obj.data
    uv = mesh.uv_layers.new(name="UVMap") if not mesh.uv_layers else mesh.uv_layers.active
    coordinates = [v.co for v in mesh.vertices]
    low = [min(v[a] for v in coordinates) for a in range(3)]
    high = [max(v[a] for v in coordinates) for a in range(3)]
    index = [p[0] for p in PALETTE].index(swatch)
    row, col = divmod(index, 4)
    for face in mesh.polygons:
        normal_axis = max(range(3), key=lambda a: abs(face.normal[a]))
        axes = [a for a in range(3) if a != normal_axis]
        axes.sort(key=lambda a: high[a] - low[a], reverse=True)
        for loop_index in face.loop_indices:
            vertex = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            u, v = [(vertex[a] - low[a]) / max(high[a] - low[a], 1e-6) for a in axes]
            uv.data[loop_index].uv = ((col + .047 + u * .906) / 4,
                                      (row + .047 + v * .906) / 2)
    mesh.materials.clear()
    mesh.materials.append(ATLAS_MATERIAL)
    PARTS.append(obj)
    return obj


def box(name, location, size, swatch="cream", bevel=.035, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    return finish(obj, swatch, bevel)


def cylinder(name, location, radius, depth, swatch, rotation=(math.pi / 2, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=40, radius=radius, depth=depth,
                                      location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    return finish(obj, swatch, .014, 2)


def extrude(name, outline, depth, location, swatch, axis="Y", bevel=.025):
    # outline is X/Z for a front panel, Y/Z for a roof extrusion along X.
    verts = []
    for side in (-depth / 2, depth / 2):
        for a, z in outline:
            verts.append((a, side, z) if axis == "Y" else (side, a, z))
    count = len(outline)
    faces = [tuple(range(count - 1, -1, -1)), tuple(range(count, 2 * count))]
    faces += [(i, (i + 1) % count, (i + 1) % count + count, i + count)
              for i in range(count)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    return finish(obj, swatch, bevel)


def arch(name, location, width, height, depth, swatch):
    radius = width / 2
    outline = [(-radius, 0), (radius, 0), (radius, height - radius)]
    outline += [(math.cos(t * math.pi / 16) * radius,
                 height - radius + math.sin(t * math.pi / 16) * radius)
                for t in range(1, 17)]
    return extrude(name, outline, depth, location, swatch, bevel=.017)


def join_asset(asset_id):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in PARTS:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = PARTS[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = asset_id
    # Asset roots are identity. Its origin is the ground-plane centre for the
    # station, and the start centreline on the ground plane for either track.
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.data.materials.clear()
    obj.data.materials.append(ATLAS_MATERIAL)
    for face in obj.data.polygons:
        face.material_index = 0
    PARTS.clear()
    return obj


def station():
    box("Platform navy footing", (0, 0, .10), (2.96, 1.74, .20), "navy", .08)
    box("Solid wooden platform", (0, 0, .245), (3.02, 1.79, .24), "wood", .075)
    box("Cream platform cap", (0, -.01, .355), (3.05, 1.81, .08), "cream", .036)
    box("Doorstep", (-.03, -1.0, .105), (.86, .42, .21), "wood", .055)
    box("Doorstep cream tread", (-.03, -1.02, .21), (.88, .42, .065), "cream", .027)
    box("Rounded station walls", (-.31, .18, 1.035), (1.85, 1.05, 1.29), "cream", .065)
    box("Station skirting", (-.31, .16, .48), (1.88, 1.10, .17), "wood", .038)
    # The cream back wall and porch leave a readable building silhouette while
    # the little bench and open right bay identify a place to wait.
    for x in (-1.14, 1.16):
        box("Porch post", (x, -.56, 1.04), (.145, .17, 1.30), "wood", .037)
        box("Post foot", (x, -.56, .49), (.20, .22, .20), "bench", .037)
    box("Porch lintel", (0, -.55, 1.67), (2.52, .19, .15), "wood", .035)
    for x in (-1.02, 1.04):
        box("Rounded porch brace", (x, -.55, 1.51), (.105, .14, .42), "wood", .025,
            (0, -.64 if x < 0 else .64, 0))
    arch("Arched door surround", (.04, -.393, .405), .63, 1.095, .12, "wood")
    arch("Deep navy arched door", (.04, -.466, .424), .505, .995, .058, "navy")
    arch("Door upper teal inset", (.04, -.504, .99), .31, .30, .023, "teal")
    box("Door inset crossbar", (.04, -.522, 1.058), (.30, .025, .036), "wood", .01)
    cylinder("Round wooden door handle", (.205, -.516, .83), .038, .038, "brass")
    box("Front window wood surround", (-.70, -.401, 1.10), (.56, .13, .63), "wood", .065)
    box("Front window shadow", (-.70, -.475, 1.12), (.444, .04, .49), "navy", .041)
    box("Front window teal glass", (-.70, -.500, 1.12), (.394, .019, .44), "teal", .035)
    box("Window vertical muntin", (-.70, -.52, 1.12), (.034, .035, .46), "cream", .012)
    box("Window horizontal muntin", (-.70, -.52, 1.13), (.41, .035, .034), "cream", .012)
    box("Rounded window sill", (-.70, -.49, .824), (.63, .24, .095), "cream", .033)
    # A second window reads from the rear three-quarter asset inspection.
    box("Back window surround", (-.45, .744, 1.10), (.71, .10, .55), "wood", .055)
    box("Back window pane", (-.45, .801, 1.10), (.58, .025, .41), "teal", .035)
    box("Back window muntin", (-.45, .819, 1.10), (.04, .024, .42), "cream", .009)
    for x in (.76, 1.13):
        box("Bench leg", (x, .03, .53), (.085, .31, .30), "bench", .027)
    box("Bench seat", (.945, -.01, .69), (.63, .40, .115), "bench", .043)
    box("Bench back", (.945, .135, .86), (.64, .095, .28), "bench", .043)
    # Continuous roof profile, with a soft low ridge and chunky rounded eaves.
    roof = [(-.94, 1.69), (-.94, 1.82), (-.11, 2.165), (0, 2.183),
            (.11, 2.165), (.94, 1.82), (.94, 1.69), (.76, 1.69),
            (0, 2.012), (-.76, 1.69)]
    extrude("Tomato red toy roof", roof, 2.84, (0, 0, 0), "red", "X", .039)
    box("Soft red ridge cap", (0, 0, 2.167), (2.88, .17, .11), "red", .047)
    box("Badge navy post", (.96, -.805, .87), (.115, .115, .96), "navy", .03)
    cylinder("Ivory station badge", (.96, -.82, 1.32), .315, .12, "ivory")
    cylinder("Raised tomato circle", (.96, -.893, 1.32), .193, .031, "red")
    return join_asset(ASSET_IDS[0])


def sweep(name, path, section, swatch):
    vertices = []
    for x, y, tx, ty in path:
        for across, height in section:
            vertices.append((x + ty * across, y - tx * across, height))
    count = len(section)
    faces = []
    for ring in range(len(path) - 1):
        for corner in range(count):
            a = ring * count + corner
            b = ring * count + (corner + 1) % count
            faces.append((a, b, b + count, a + count))
    faces += [tuple(range(count - 1, -1, -1)),
              tuple((len(path) - 1) * count + i for i in range(count))]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish(obj, swatch)


def rounded_section(half_width, bottom, top, radius):
    section = []
    for cx, cz, begin in ((half_width - radius, top - radius, 0),
                          (-half_width + radius, top - radius, 90),
                          (-half_width + radius, bottom + radius, 180),
                          (half_width - radius, bottom + radius, 270)):
        for step in range(4):
            angle = math.radians(begin + step * 30)
            section.append((cx + radius * math.cos(angle), cz + radius * math.sin(angle)))
    return section


def track(curved):
    length = 1.9 * math.pi / 2 if curved else 3.2
    def point(t):
        if curved:
            angle = t * math.pi / 2
            return (1.9 * (1 - math.cos(angle)), 1.9 * math.sin(angle),
                    math.sin(angle), math.cos(angle))
        return (0, length * t, 0, 1)
    path = [point(i / 40) for i in range(41)] if curved else [point(0), point(1)]
    sweep("Rounded cream wooden track bed", path, rounded_section(.45, 0, .19, .044), "cream")
    # Navy running surfaces use the existing train's .5-unit centre gauge.
    for offset in (-.25, .25):
        rail_section = [(offset + lateral, height) for lateral, height
                        in rounded_section(.055, .205, .315, .023)]
        sweep("Soft navy rail", path, rail_section, "navy")
    sleeper_count = round(length / .35)
    for i in range(sleeper_count):
        x, y, tx, ty = point((i + .5) / sleeper_count)
        box("Cream wooden sleeper", (x, y, .206), (.78, .16, .055), "wood", .022,
            (0, 0, -math.atan2(tx, ty)))
    return join_asset(ASSET_IDS[2 if curved else 1])


def metrics(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    points = [obj.matrix_world @ vertex.co for vertex in mesh.vertices]
    lower = [min(point[a] for point in points) for a in range(3)]
    upper = [max(point[a] for point in points) for a in range(3)]
    bm = bmesh.new()
    bm.from_mesh(mesh)
    non_manifold = sum(not edge.is_manifold for edge in bm.edges)
    bm.free()
    return {"vertices": len(mesh.vertices), "triangles": len(mesh.loop_triangles),
            "materials": len(mesh.materials), "mesh_objects": 1,
            "non_manifold_edges": non_manifold,
            "bounds_blender_z_up": {"min": lower, "max": upper},
            "dimensions": [upper[i] - lower[i] for i in range(3)],
            "root_transform_identity": all(abs(v) < 1e-6 for v in obj.location)
                and all(abs(v) < 1e-6 for v in obj.rotation_euler)
                and all(abs(v - 1) < 1e-6 for v in obj.scale)}


def export_asset(obj, output):
    active(obj)
    fbx = output / "Models" / (obj.name + ".fbx")
    glb = output / "Models" / (obj.name + ".glb")
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True,
                             object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, apply_unit_scale=True,
                             use_mesh_modifiers=True, bake_anim=False,
                             path_mode="COPY", embed_textures=True)
    bpy.ops.export_scene.gltf(filepath=str(glb), use_selection=True,
                              export_format="GLB", export_materials="EXPORT",
                              export_animations=False, export_yup=True)
    return {path.name: sha256(path) for path in (fbx, glb)}


def aim(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def setup_stage(samples):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.cycles.seed = 19
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.world = bpy.data.worlds.new("PREVIEW_ONLY studio world")
    scene.world.color = (.22, .22, .22)
    scene.world.use_nodes = True
    world = scene.world.node_tree.nodes.get("Background")
    world.inputs["Color"].default_value = (.69, .75, .82, 1)
    world.inputs["Strength"].default_value = .35
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.035))
    floor = bpy.context.object
    floor.name = "PREVIEW_ONLY cream studio floor"
    material = bpy.data.materials.new("PREVIEW_ONLY warm paper")
    material.diffuse_color = (.65, .57, .44, 1)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (.65, .57, .44, 1)
    bsdf.inputs["Roughness"].default_value = .85
    floor.data.materials.append(material)
    for name, position, energy, size, color in (
        ("PREVIEW_ONLY warm key", (-3.5, -4.5, 7), 650, 4.0, (1, .87, .69)),
        ("PREVIEW_ONLY soft fill", (4, -1, 5), 360, 5.0, (.79, .87, 1)),
        ("PREVIEW_ONLY rim", (0, 4, 6), 520, 3.5, (1, .88, .73)),
    ):
        bpy.ops.object.light_add(type="AREA", location=position)
        light = bpy.context.object
        light.name = name
        light.data.energy = energy
        light.data.shape = "DISK"
        light.data.size = size
        light.data.color = color
        aim(light, (0, 0, .7))
    bpy.ops.object.camera_add(location=(5, -8, 6))
    camera = bpy.context.object
    camera.name = "PREVIEW_ONLY orthographic camera"
    camera.data.type = "ORTHO"
    scene.camera = camera
    return camera


def render(output, name, camera, location, target, scale, resolution):
    camera.location = location
    camera.data.ortho_scale = scale
    aim(camera, target)
    scene = bpy.context.scene
    scene.render.resolution_x, scene.render.resolution_y = resolution
    scene.render.resolution_percentage = 100
    scene.render.filepath = str(output / "Previews" / (name + ".png"))
    bpy.ops.render.render(write_still=True)


def main():
    args = arguments()
    if bpy.app.version[:3] != (5, 1, 2):
        raise RuntimeError("This recipe is pinned to Blender 5.1.2")
    output = args.output_dir.resolve()
    for folder in ("Models", "Textures", "Previews", "Source"):
        (output / folder).mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    image = atlas(output)
    objects = [station(), track(False), track(True)]
    provenance = {"generator": "scripts/blender_original_station_track.py",
        "generator_sha256": sha256(Path(__file__)), "blender_version": bpy.app.version_string,
        "blender_build_hash": bpy.app.build_hash.decode(), "seed": 20260910,
        "authorship": "Original procedural geometry and analytic texture authored for Cat Metro by Codex, 2026-09-10.",
        "source_assets": [], "external_services": [],
        "reference_direction": ["docs/LOOK.md", "docs/reference/gen-ref-NOTES.md",
                                "docs/reference/gen-ref-v2-props.png", "docs/reference/gen-ref-v2-board.png",
                                "docs/reference/gen-ref-v2-moments.png"],
        "texture": {"path": "Textures/original-toy-atlas.png", "width": 1024, "height": 512,
                    "sha256": sha256(output / "Textures" / "original-toy-atlas.png"),
                    "palette_srgb": dict(PALETTE)},
        "assets": []}
    for obj in objects:
        measured = metrics(obj)
        assert measured["materials"] == 1 and measured["non_manifold_edges"] == 0
        assert measured["root_transform_identity"]
        assert measured["triangles"] <= (16000 if obj == objects[0] else 8000)
        provenance["assets"].append({"id": obj.name,
            "source": "Original procedural Blender geometry; no imported mesh, image, font, or provider output.",
            "licence": "Project-original asset; no third-party asset licence dependencies.",
            "metrics": measured, "exports_sha256": export_asset(obj, output)})
    camera = setup_stage(args.samples)
    station_obj, straight, curve = objects
    straight.location = (-2.65, -.55, 0)
    straight.rotation_euler.z = -math.pi / 2
    curve.location = (.55, -.55, 0)
    curve.rotation_euler.z = -math.pi / 2
    station_obj.location = (-.7, 1.22, 0)
    if not args.skip_renders:
        render(output, "01-station-and-track", camera, (6, -9, 7), (0, -.1, .75), 7.1, (1600, 1200))
        for obj in (straight, curve):
            obj.hide_render = True
        station_obj.location = (0, 0, 0)
        render(output, "02-station-front", camera, (4.7, -7, 4.9), (0, -.08, 1.02), 4.1, (1400, 1200))
        render(output, "03-station-rear", camera, (-4.7, 7, 4.9), (0, 0, 1.02), 4.1, (1400, 1200))
        render(output, "04-station-small", camera, (4.7, -7, 4.9), (0, -.08, 1.02), 4.1, (192, 164))
        station_obj.hide_render = True
        straight.hide_render = curve.hide_render = False
        straight.location = (-2.4, -1.6, 0)
        straight.rotation_euler.z = 0
        curve.location = (-.5, -1.6, 0)
        curve.rotation_euler.z = 0
        render(output, "05-track-pieces", camera, (5, -6, 7.5), (-.35, -.1, .1), 6.3, (1600, 1100))
    # Save an editable stage with all three canonical assets arranged in context.
    station_obj.hide_render = False
    station_obj.location = (-.7, 1.22, 0)
    straight.location = (-2.65, -.55, 0)
    straight.rotation_euler.z = -math.pi / 2
    curve.location = (.55, -.55, 0)
    curve.rotation_euler.z = -math.pi / 2
    camera.location = (6, -9, 7)
    camera.data.ortho_scale = 7.1
    aim(camera, (0, -.1, .75))
    # Store canonical export placement explicitly so source edits do not confuse
    # stage placement with asset-root conventions.
    for obj in objects:
        obj["export_origin"] = "Reset object location/rotation to zero before re-export; dimensions are baked."
    image.filepath = "//../Textures/original-toy-atlas.png"
    blend = output / "Source" / "original-station-track.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), check_existing=False)
    provenance["blend_sha256"] = sha256(blend)
    provenance["previews_sha256"] = {p.name: sha256(p) for p in sorted((output / "Previews").glob("*.png"))}
    (output / "PROVENANCE.json").write_text(json.dumps(provenance, indent=2) + "\n")
    print("ORIGINAL_PROTOTYPE " + json.dumps(provenance["assets"]), flush=True)


if __name__ == "__main__":
    main()
