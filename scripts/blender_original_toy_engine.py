#!/usr/bin/env python3
"""Author a project-original moving toy locomotive; Blender 5.1.2 only.

No imported meshes/images. +X forward, +Z up, wheels at Z=0. The complete
engine stays inside the existing .46 x .30 x .37 moving-engine envelope.
Exports only EngineBody/RunningGear with one original procedural atlas.
"""

import argparse
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.dont_write_bytecode = True
ASSET_ID = "original-toy-engine"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def cylinder(recipe, name, location, radius, depth, swatch,
             rotation=(math.pi / 2, 0, 0), vertices=24, bevel=.003):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius,
        depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    return recipe.finish(obj, swatch, bevel, 2)


def funnel_lip(recipe):
    # A closed cup section gives the funnel a real inset. Its rim stays rounded
    # without a flat cap spanning the opening.
    sections = ((.042, .338), (.047, .343), (.047, .364), (.043, .370),
                (.029, .370), (.025, .365), (.025, .352))
    count = 24
    vertices = [(.150 + radius * math.cos(i * 2 * math.pi / count),
                 radius * math.sin(i * 2 * math.pi / count), z)
                for radius, z in sections for i in range(count)]
    faces = [tuple(range(count - 1, -1, -1))]
    for ring in range(len(sections) - 1):
        for i in range(count):
            j = (i + 1) % count
            faces.append((ring * count + i, ring * count + j,
                          (ring + 1) * count + j, (ring + 1) * count + i))
    faces.append(tuple((len(sections) - 1) * count + i for i in range(count)))
    mesh = bpy.data.meshes.new("InsetFunnelGeometry")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("Rounded hollow funnel lip", mesh)
    bpy.context.collection.objects.link(obj)
    return recipe.finish(obj, "navy")


def body(recipe):
    recipe.box("Teal lower cab", (-.126, 0, .134), (.176, .230, .110), "teal", .012)
    recipe.box("Wood cab sill", (-.126, 0, .190), (.176, .238, .020), "wood", .006)
    # Real side-window openings, with a cream rear wall and four rounded posts.
    recipe.box("Cream cab back", (-.203, 0, .250), (.022, .220, .122), "cream", .008)
    for x in (-.192, -.058):
        for y in (-.102, .102):
            recipe.box("Cream window post", (x, y, .252), (.022, .024, .122), "cream", .006)
    recipe.box("Cream window header", (-.126, 0, .305), (.175, .238, .020), "cream", .006)
    # A gently arched navy roof with a cream underside; no separate display base.
    roof = [(-.148, .313), (-.148, .328), (-.118, .338), (-.065, .349),
            (0, .353), (.065, .349), (.118, .338), (.148, .328), (.148, .313)]
    recipe.extrude("Rounded navy cab roof", roof, .205, (-.124, 0, 0), "navy", axis="X", bevel=.007)
    cylinder(recipe, "Rounded teal boiler", (.062, 0, .194), .094, .268,
             "teal", (0, math.pi / 2, 0), vertices=32, bevel=.008)
    cylinder(recipe, "Cream boiler front", (.198, 0, .194), .083, .019,
             "cream", (0, math.pi / 2, 0), vertices=32, bevel=.005)
    cylinder(recipe, "Wood front medallion", (.210, 0, .194), .037, .008,
             "wood", (0, math.pi / 2, 0), vertices=24, bevel=.003)
    cylinder(recipe, "Navy front peg", (.216, 0, .194), .015, .009,
             "navy", (0, math.pi / 2, 0), vertices=16, bevel=.002)
    # Short funnel at the existing steam attachment's XY centre. The dark inset
    # is recessed below its lip, rather than a shiny metallic hole.
    cylinder(recipe, "Cream funnel collar", (.150, 0, .283), .048, .020,
             "cream", (0, 0, 0), bevel=.004)
    cylinder(recipe, "Teal funnel stem", (.150, 0, .316), .032, .065,
             "teal", (0, 0, 0), bevel=.004)
    funnel_lip(recipe)
    return recipe.join_asset("EngineBody")


def running_gear(recipe):
    recipe.box("Rounded navy chassis", (0, 0, .076), (.458, .258, .045), "navy", .010)
    recipe.box("Wood front buffer", (.214, 0, .087), (.027, .244, .043), "wood", .009)
    for x in (-.151, .151):
        cylinder(recipe, "Navy axle", (x, 0, .053), .013, .264, "navy", vertices=16)
        for y in (-.126, .126):
            cylinder(recipe, "Rounded navy wheel", (x, y, .053), .053, .037, "navy", bevel=.004)
            cylinder(recipe, "Wood wheel hub", (x, math.copysign(.145, y), .053),
                     .031, .008, "wood", vertices=20, bevel=.002)
            cylinder(recipe, "Navy axle button", (x, math.copysign(.148, y), .053),
                     .011, .003, "navy", vertices=16, bevel=.001)
    return recipe.join_asset("RunningGear")


def geometry_checks(objects):
    points = [obj.matrix_world @ v.co for obj in objects for v in obj.data.vertices]
    lower = [min(v[i] for v in points) for i in range(3)]
    upper = [max(v[i] for v in points) for i in range(3)]
    for actual, allowed in zip(lower, (-.230, -.150, 0)):
        assert actual >= allowed - .000005, ("below envelope", lower)
    for actual, allowed in zip(upper, (.230, .150, .370)):
        assert actual <= allowed + .000005, ("above envelope", upper)
    assert abs(lower[2]) < .000005, "Wheel bottoms must remain on the existing plane"
    obj = next(obj for obj in objects if obj.name == "EngineBody")
    tree = BVHTree.FromPolygons([obj.matrix_world @ v.co for v in obj.data.vertices],
                              [list(p.vertices) for p in obj.data.polygons], all_triangles=False)
    for z in (.232, .270):
        hit, unused_normal, unused_face, unused_distance = tree.ray_cast(
            Vector((-.126, -.20, z)), Vector((0, 1, 0)), .40)
        assert hit is None, ("Cab side window is obstructed", z, hit)
    return {"bounds_blender_z_up": {"min": lower, "max": upper},
            "existing_envelope": {"min": [-.23, -.15, 0], "max": [.23, .15, .37]},
            "two_side_window_rays_unobstructed": True, "wheel_bottom_z": lower[2]}


def stage(recipe, samples):
    camera = recipe.setup_stage(samples)
    camera.data.clip_start = .001
    floor = bpy.data.objects["PREVIEW_ONLY cream studio floor"]
    floor.location.z = -.002
    floor.data.materials[0].node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = (.55, .50, .42, 1)
    for light in [o for o in bpy.data.objects if o.type == "LIGHT"]:
        light.location *= .22
        light.data.energy *= .22 ** 2
        light.data.size *= .22
        recipe.aim(light, (0, 0, .18))
    return camera


def export(objects, output):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    fbx = output / "Models" / (ASSET_ID + ".fbx")
    glb = output / "Models" / (ASSET_ID + ".glb")
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={"MESH"},
        axis_forward="-Z", axis_up="Y", bake_space_transform=True, apply_unit_scale=True,
        use_mesh_modifiers=True, bake_anim=False, path_mode="COPY", embed_textures=True)
    bpy.ops.export_scene.gltf(filepath=str(glb), use_selection=True, export_format="GLB",
        export_materials="EXPORT", export_animations=False, export_yup=True)
    return fbx, glb


def verify(output, recipe):
    from io_scene_fbx import parse_fbx
    record = json.loads((output / "PROVENANCE.json").read_text())
    assert record["generator_sha256"] == digest(Path(__file__))
    assert record["base_recipe_sha256"] == digest(Path(__file__).with_name("blender_original_station_track.py"))
    for relative, expected in record["outputs_sha256"].items():
        assert digest(output / relative) == expected, relative
    reports = []
    for extension in ("glb", "fbx"):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        path = output / "Models" / (ASSET_ID + "." + extension)
        if extension == "fbx":
            tree, unused_version = parse_fbx.parse(str(path))
            embedded = []
            def walk(node):
                if node.id == b"Video":
                    embedded.extend(hashlib.sha256(child.props[0]).hexdigest()
                        for child in node.elems if child.id == b"Content" and child.props)
                for child in node.elems:
                    walk(child)
            walk(tree)
            assert embedded == [record["outputs_sha256"]["Textures/original-toy-atlas.png"]]
            bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False, use_image_search=False)
        else:
            bpy.ops.import_scene.gltf(filepath=str(path), loglevel=1)
        objects = [obj for obj in bpy.data.objects if obj.type == "MESH"]
        assert {o.name for o in objects} == {"EngineBody", "RunningGear"}
        assert len(bpy.data.objects) == 2
        geometry = geometry_checks(objects)
        for obj in objects:
            actual = recipe.metrics(obj)
            expected = record["assets"][0]["parts"][obj.name]
            assert actual["triangles"] == expected["triangles"]
            assert actual["materials"] == 1
            for end in ("min", "max"):
                assert all(abs(a - b) < .00005 for a, b in zip(
                    actual["bounds_blender_z_up"][end], expected["bounds_blender_z_up"][end]))
            images = [node.image for node in obj.data.materials[0].node_tree.nodes
                      if node.type == "TEX_IMAGE" and node.image is not None]
            assert len(images) == 1 and tuple(images[0].size) == (1024, 512)
            reports.append({"file": path.name, "part": obj.name,
                "triangles": actual["triangles"], "bounds_and_atlas_match": True})
        if extension == "fbx":
            camera = stage(recipe, 48)
            recipe.render(output, "06-fbx-roundtrip", camera, (.72, -.96, .72),
                          (0, 0, .185), .66, (1200, 1000))
    report = {"verified": reports, "geometry": geometry, "fbx_embedded_atlas_matches": True,
        "roundtrip_preview_sha256": digest(output / "Previews" / "06-fbx-roundtrip.png"),
        "not_verified": ["Unity import and axes", "URP material binding", "actual board readability", "steam placement", "Android"]}
    (output / "EXPORT-VERIFICATION.json").write_text(json.dumps(report, indent=2) + "\n")
    print("ORIGINAL_ENGINE_EXPORT_VERIFIED " + json.dumps(report), flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--samples", type=int, default=48)
    parser.add_argument("--skip-renders", action="store_true")
    parser.add_argument("--verify-existing", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    if bpy.app.version[:3] != (5, 1, 2):
        raise RuntimeError("Requires Blender 5.1.2")
    output = args.output_dir.resolve()
    for folder in ("Models", "Textures", "Source", "Previews"):
        (output / folder).mkdir(parents=True, exist_ok=True)
    source = Path(__file__).with_name("blender_original_station_track.py")
    spec = importlib.util.spec_from_file_location("original_toy_recipe", source)
    recipe = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(recipe)
    if args.verify_existing:
        verify(output, recipe)
        return
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    atlas = recipe.atlas(output)
    recipe.ATLAS_MATERIAL.name = "OriginalToyEngine_Atlas"
    objects = [body(recipe), running_gear(recipe)]
    parts = {obj.name: recipe.metrics(obj) for obj in objects}
    for metrics in parts.values():
        assert metrics["root_transform_identity"] and metrics["non_manifold_edges"] == 0
        assert metrics["materials"] == 1
    triangles = sum(metrics["triangles"] for metrics in parts.values())
    assert triangles <= 10000, triangles
    geometry = geometry_checks(objects)
    fbx, glb = export(objects, output)
    camera = stage(recipe, args.samples)
    if not args.skip_renders:
        for name, location, target, scale, resolution in (
            ("01-front", (.72, -.96, .72), (0, 0, .185), .66, (1200, 1000)),
            ("02-rear", (-.72, .96, .72), (0, 0, .185), .66, (1200, 1000)),
            ("03-side", (0, -1, .185), (0, 0, .185), .59, (1200, 950)),
            ("04-board-38deg", (.60, -.80, .9663), (0, 0, .185), .66, (1200, 1000)),
            ("05-small", (.72, -.96, .72), (0, 0, .185), .66, (192, 160)),
        ):
            recipe.render(output, name, camera, location, target, scale, resolution)
    atlas.filepath = "//../Textures/original-toy-atlas.png"
    blend = output / "Source" / (ASSET_ID + ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), check_existing=False)
    record = {
        "assets": [{"id": ASSET_ID, "authorship": "Original code-authored geometry and analytic atlas for Cat Metro, Codex, 2026-09-10.",
            "source": "Rounded boiler, open cab, arched roof, funnel, chassis and wood/navy wheels; no imported geometry or imagery.",
            "licence": "Project-original asset; no third-party asset licence dependencies.",
            "source_assets": [], "external_services": [], "parts": parts,
            "triangles": triangles, "mesh_objects": 2, "shared_materials": 1,
            "coordinate_system": "+X forward, +Z up, wheel bottoms Z=0", "geometry": geometry,
            "suggested_board_adapter": "Blender (x,y,z) -> board (x,-y,.235-z); ordinary Unity +Y-up import rotated -90 degrees about X and translated Z+.235, scale1. Validate native import before admission.",
            "retained_runtime_attachment": "Existing Engine/Funnel transform at board local (.15,0,-.085), including its original rotation/scale, remains the steam parent."}],
        "generator": "scripts/blender_original_toy_engine.py", "generator_sha256": digest(Path(__file__)),
        "base_recipe": "scripts/blender_original_station_track.py", "base_recipe_sha256": digest(source),
        "blender": bpy.app.version_string, "build_hash": bpy.app.build_hash.decode(), "seed": 20260910,
        "reference_direction": ["docs/LOOK.md", "docs/reference/gen-ref-v2-board.png", "docs/reference/gen-ref-v2-props.png"],
        "texture": {"size": [1024, 512], "palette_srgb": dict(recipe.PALETTE)},
        "outputs_sha256": {str(path.relative_to(output)): digest(path)
            for path in (fbx, glb, blend, output / "Textures" / "original-toy-atlas.png")},
        "previews_sha256": {path.name: digest(path) for path in sorted((output / "Previews").glob("0[1-5]-*.png"))},
    }
    (output / "PROVENANCE.json").write_text(json.dumps(record, indent=2) + "\n")
    print("ORIGINAL_TOY_ENGINE " + json.dumps(record["assets"]), flush=True)


if __name__ == "__main__":
    main()
