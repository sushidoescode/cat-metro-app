#!/usr/bin/env python3
"""Generate one original, actually open Cat Metro toy carriage in Blender 5.1.2.

blender --background --factory-startup --threads 6 --python-exit-code 97 \
  --python scripts/blender_original_open_carriage.py -- \
  --output-dir docs/design/assets/original-open-carriage

No imported model/image inputs. +X forward, +Z up, wheel bottoms at Z=0.
The two meshes share one original atlas material; preview staging is excluded.
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
ASSET_ID = "original-open-carriage"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rounded_ring(length, width, radius, z):
    """32 vertices around a rounded rectangle, counterclockwise from +X/+Y."""
    vertices = []
    for cx, cy, start in ((length / 2 - radius, width / 2 - radius, 0),
                          (-length / 2 + radius, width / 2 - radius, 90),
                          (-length / 2 + radius, -width / 2 + radius, 180),
                          (length / 2 - radius, -width / 2 + radius, 270)):
        for step in range(8):
            angle = math.radians(start + step * 90 / 7)
            vertices.append((cx + math.cos(angle) * radius,
                             cy + math.sin(angle) * radius, z))
    return vertices


def uv_swatch(recipe, obj, faces, name):
    """Retain generated planar UVs, moving only selected faces to another swatch."""
    source = next(i for i, pair in enumerate(recipe.PALETTE) if pair[0] == "cream")
    dest = next(i for i, pair in enumerate(recipe.PALETTE) if pair[0] == name)
    sr, sc = divmod(source, 4)
    dr, dc = divmod(dest, 4)
    uv = obj.data.uv_layers.active.data
    for face in faces:
        for loop in face.loop_indices:
            uv[loop].uv.x += (dc - sc) / 4
            uv[loop].uv.y += (dr - sr) / 2


def shell(recipe):
    # Cross-section runs from the underside up the outer wall, around the thick
    # rounded lip, down the inside wall, and onto the recessed floor. There is
    # deliberately NO polygon spanning the opening at the rim height.
    sections = [
        (.492, .452, .040, .065),
        (.502, .462, .043, .070),
        (.516, .476, .047, .137),
        (.520, .480, .049, .151),
        (.516, .476, .048, .161),
        (.507, .467, .044, .165),
        (.473, .433, .032, .165),
        (.462, .422, .029, .160),
        (.458, .418, .030, .153),
        (.440, .400, .039, .095),
        (.430, .390, .040, .085),
    ]
    vertices = [v for values in sections for v in rounded_ring(*values)]
    n = 32
    faces = [tuple(range(n - 1, -1, -1))]
    for ring in range(len(sections) - 1):
        for index in range(n):
            following = (index + 1) % n
            faces.append((ring * n + index, ring * n + following,
                          (ring + 1) * n + following, (ring + 1) * n + index))
    faces.append(tuple((len(sections) - 1) * n + i for i in range(n)))
    mesh = bpy.data.meshes.new("OpenShellGeometry")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("OpenShell", mesh)
    bpy.context.collection.objects.link(obj)
    recipe.finish(obj, "cream")
    # The lower exterior is painted teal; the interior stays warm and neutral.
    # Polygon ordering is unchanged by finish (no bevel topology modifier).
    uv_swatch(recipe, obj, list(mesh.polygons)[1:1 + n * 2], "teal")
    uv_swatch(recipe, obj, [mesh.polygons[-1]], "ivory")
    obj["opening"] = "Continuous recessed floor at .085, rounded rim at .165; no top cap."
    obj["coordinate_system"] = "+X forward; +Z up; bottom 0; board local (x,-y,.235-z)."
    recipe.PARTS.clear()
    return obj


def cylinder(recipe, name, location, radius, depth, swatch, vertices=24, bevel=.004):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,
                                      location=location, rotation=(math.pi / 2, 0, 0))
    obj = bpy.context.object
    obj.name = name
    return recipe.finish(obj, swatch, bevel, 2)


def undercarriage(recipe):
    recipe.box("Rounded navy chassis", (0, 0, .044), (.458, .414, .040), "navy", .013)
    for x in (-.160, .160):
        cylinder(recipe, "Wooden axle", (x, 0, .054), .012, .494, "navy", 16, .003)
        for y in (-.243, .243):
            cylinder(recipe, "Rounded navy wheel", (x, y, .054), .054, .048, "navy")
            cylinder(recipe, "Natural wood wheel hub", (x, math.copysign(.267, y), .054),
                     .027, .006, "wood", 20, .002)
    return recipe.join_asset("Undercarriage")


def ray_checks(obj):
    """Independent geometry queries distinguish a cavity from a solid top cap."""
    mesh = obj.data
    points = [obj.matrix_world @ vertex.co for vertex in mesh.vertices]
    tree = BVHTree.FromPolygons(points, [list(p.vertices) for p in mesh.polygons], all_triangles=False)
    samples = []
    # Nine interior rays must hit the .085 floor first; four rim rays must hit
    # .165 first. A filled top, missing wall, or missing floor fails these probes.
    for x in (-.15, 0, .15):
        for y in (-.13, 0, .13):
            samples.append(("floor", (x, y), .085))
    samples += [("rim", (x, y), .165) for x, y in
                ((.245, 0), (-.245, 0), (0, .225), (0, -.225))]
    report = []
    for name, (x, y), expected in samples:
        location, normal, face, distance = tree.ray_cast(Vector((x, y, .30)), Vector((0, 0, -1)))
        assert location is not None, (name, x, y, "no surface")
        assert abs(location.z - expected) < .00005, (name, x, y, location.z, expected)
        assert normal.z > .99, (name, normal)
        report.append({"kind": name, "xy": [x, y], "first_hit_z": location.z})
    # Four horizontal rays at cavity mid-height must meet the inside wall;
    # the same directions above the rim must remain unobstructed.
    for direction in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0)):
        hit, normal, face, distance = tree.ray_cast(Vector((0, 0, .120)), Vector(direction))
        assert hit is not None and .20 < distance < .24, (direction, hit, distance)
        assert normal.dot(Vector(direction)) < -.98, (direction, normal)
        above, unused_normal, unused_face, unused_distance = tree.ray_cast(
            Vector((0, 0, .170)), Vector(direction))
        assert above is None, (direction, "obstructed above opening")
        report.append({"kind": "inner_wall", "direction": direction,
                       "distance": distance, "above_rim_open": True})
    return {"depth": .080, "floor_z": .085, "rim_z": .165,
            "floor_outline_bounds": [.430, .390], "samples": report}


def reject_solid_control():
    """The cavity check must fail on an old-style solid body with the same rim."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, .115))
    obj = bpy.context.object
    obj.dimensions = (.520, .480, .100)
    bpy.context.view_layer.update()
    rejected = False
    try:
        ray_checks(obj)
    except AssertionError:
        rejected = True
    finally:
        bpy.data.objects.remove(obj, do_unlink=True)
    assert rejected, "A filled body must not pass the open-cavity checks"
    return True


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


def setup_preview(recipe, samples):
    camera = recipe.setup_stage(samples)
    camera.data.clip_start = .001
    # Tailor the inherited original studio to a half-unit toy rather than the
    # three-unit station. A wide soft key shows the curved inside wall clearly.
    floor = bpy.data.objects["PREVIEW_ONLY cream studio floor"]
    floor.location.z = -.002
    floor.data.materials[0].node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = (.55, .50, .42, 1)
    for light in [o for o in bpy.data.objects if o.type == "LIGHT"]:
        light.location *= .22
        light.data.energy *= .22 ** 2
        light.data.size *= .22
        recipe.aim(light, (0, 0, .08))
    return camera


def verify_exports(output, recipe):
    from io_scene_fbx import parse_fbx
    record = json.loads((output / "PROVENANCE.json").read_text())
    assert record["generator_sha256"] == digest(Path(__file__))
    assert record["base_recipe_sha256"] == digest(Path(__file__).with_name("blender_original_station_track.py"))
    for relative, expected in record["outputs_sha256"].items():
        assert digest(output / relative) == expected, relative
    expected = record["assets"][0]["parts"]
    reports = []
    for extension in ("glb", "fbx"):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        path = output / "Models" / (ASSET_ID + "." + extension)
        if extension == "fbx":
            tree, unused_version = parse_fbx.parse(str(path))
            embedded = []
            def walk(node):
                if node.id == b"Video":
                    for child in node.elems:
                        if child.id == b"Content" and child.props:
                            embedded.append(hashlib.sha256(child.props[0]).hexdigest())
                for child in node.elems:
                    walk(child)
            walk(tree)
            assert embedded == [record["outputs_sha256"]["Textures/original-toy-atlas.png"]]
            bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False, use_image_search=False)
        else:
            bpy.ops.import_scene.gltf(filepath=str(path), loglevel=1)
        meshes = {obj.name: obj for obj in bpy.data.objects if obj.type == "MESH"}
        assert set(meshes) == set(expected) and len(bpy.data.objects) == 2
        for name, obj in meshes.items():
            actual = recipe.metrics(obj)
            assert actual["triangles"] == expected[name]["triangles"], (extension, name)
            assert actual["materials"] == 1
            for end in ("min", "max"):
                assert all(abs(a - b) < .00005 for a, b in zip(
                    actual["bounds_blender_z_up"][end], expected[name]["bounds_blender_z_up"][end]))
            material = obj.data.materials[0]
            images = [node.image for node in material.node_tree.nodes
                      if node.type == "TEX_IMAGE" and node.image is not None]
            assert len(images) == 1 and tuple(images[0].size) == (1024, 512)
            reports.append({"file": path.name, "part": name,
                            "triangles": actual["triangles"], "bounds_and_atlas_match": True})
        ray_checks(meshes["OpenShell"])
        if extension == "fbx":
            camera = setup_preview(recipe, 48)
            recipe.render(output, "06-fbx-roundtrip", camera, (.72, -.96, .74),
                          (0, 0, .079), .78, (1400, 1100))
    report = {"verified": reports, "cavity_rays_pass_both_exports": True,
              "solid_body_negative_control_rejected": reject_solid_control(),
              "fbx_embedded_atlas_matches": True, "generator_sha256": record["generator_sha256"],
              "roundtrip_preview_sha256": digest(output / "Previews" / "06-fbx-roundtrip.png"),
              "not_verified": ["Unity import", "URP binding", "actual cat pose/occlusion", "Android"]}
    (output / "EXPORT-VERIFICATION.json").write_text(json.dumps(report, indent=2) + "\n")
    print("ORIGINAL_CARRIAGE_EXPORT_VERIFIED " + json.dumps(report), flush=True)


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
        verify_exports(output, recipe)
        return
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    atlas = recipe.atlas(output)
    recipe.ATLAS_MATERIAL.name = "OriginalOpenCarriage_Atlas"
    objects = [shell(recipe), undercarriage(recipe)]
    parts = {obj.name: recipe.metrics(obj) for obj in objects}
    for metrics in parts.values():
        assert metrics["root_transform_identity"] and metrics["non_manifold_edges"] == 0
        assert metrics["materials"] == 1
    triangles = sum(metrics["triangles"] for metrics in parts.values())
    assert triangles <= 4000, triangles
    cavity = ray_checks(objects[0])
    assert reject_solid_control()
    lower = [min(v["bounds_blender_z_up"]["min"][i] for v in parts.values()) for i in range(3)]
    upper = [max(v["bounds_blender_z_up"]["max"][i] for v in parts.values()) for i in range(3)]
    dimensions = [upper[i] - lower[i] for i in range(3)]
    assert all(abs(a - b) < .00001 for a, b in zip(dimensions, (.520, .540, .165))), dimensions
    assert abs(lower[2]) < .00001
    fbx, glb = export(objects, output)
    camera = setup_preview(recipe, args.samples)
    if not args.skip_renders:
        for name, location, target, scale, resolution in (
            ("01-open-front", (.72, -.96, .74), (0, 0, .079), .78, (1400, 1100)),
            ("02-open-rear", (-.72, .96, .74), (0, 0, .079), .78, (1400, 1100)),
            ("03-open-top", (0, 0, 1.5), (0, 0, .08), .66, (1200, 1200)),
            ("04-low-side", (.025, -1, .24), (0, 0, .08), .65, (1400, 850)),
            ("05-small", (.72, -.96, .74), (0, 0, .079), .78, (192, 151)),
        ):
            recipe.render(output, name, camera, location, target, scale, resolution)
    camera.location = (.72, -.96, .74)
    camera.data.ortho_scale = .78
    recipe.aim(camera, (0, 0, .079))
    atlas.filepath = "//../Textures/original-toy-atlas.png"
    blend = output / "Source" / (ASSET_ID + ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), check_existing=False)
    record = {
        "assets": [{"id": ASSET_ID,
            "authorship": "Original procedural geometry and analytic atlas authored for Cat Metro by Codex, 2026-09-10.",
            "source": "Code-authored rounded shell, chassis, axles and wheels; no imported meshes, photos or provider outputs.",
            "licence": "Project-original asset; no third-party asset licence dependencies.",
            "source_assets": [], "external_services": [], "parts": parts,
            "triangles": triangles, "mesh_objects": 2, "shared_materials": 1,
            "dimensions_blender_xyz": dimensions, "coordinate_system": "+X forward, +Z up, bottom Z=0",
            "suggested_board_adapter": "Blender (x,y,z) -> board-local (x,-y,.235-z). For an ordinary +Y-up Unity import, rotate -90 degrees about X then translate board-local Z to .235; validate actual imported axes before mounting.",
            "cavity": cavity}],
        "generator": "scripts/blender_original_open_carriage.py", "generator_sha256": digest(Path(__file__)),
        "base_recipe": "scripts/blender_original_station_track.py", "base_recipe_sha256": digest(source),
        "blender": bpy.app.version_string, "build_hash": bpy.app.build_hash.decode(), "seed": 20260910,
        "reference_direction": ["docs/LOOK.md", "docs/reference/gen-ref-v2-board.png", "docs/reference/gen-ref-v2-moments.png"],
        "texture": {"size": [1024, 512], "palette_srgb": dict(recipe.PALETTE)},
        "outputs_sha256": {str(path.relative_to(output)): digest(path)
                            for path in (fbx, glb, blend, output / "Textures" / "original-toy-atlas.png")},
        "previews_sha256": {path.name: digest(path) for path in sorted((output / "Previews").glob("0[1-5]-*.png"))},
    }
    (output / "PROVENANCE.json").write_text(json.dumps(record, indent=2) + "\n")
    print("ORIGINAL_OPEN_CARRIAGE " + json.dumps(record["assets"]), flush=True)


if __name__ == "__main__":
    main()
