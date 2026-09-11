#!/usr/bin/env python3
"""Regenerate one neutral station from original code; preserve the red baseline.

blender --background --factory-startup --python-exit-code 97 \
  --python scripts/blender_original_station_runtime.py -- \
  --output-dir docs/design/assets/original-station-runtime

Unity imports Body and RoofTint only. BadgePost/BadgeFace remain separate neutral
authoring parts: the game's existing dynamic badge replaces that assembly.
"""

import argparse
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import sys

import bpy

sys.dont_write_bytecode = True


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify_exports(output, recipe):
    from io_scene_fbx import parse_fbx
    record = json.loads((output / "PROVENANCE.json").read_text())
    assert record["generator_sha256"] == digest(Path(__file__))
    for relative, expected in record["outputs_sha256"].items():
        assert digest(output / relative) == expected, relative
    expected_parts = record["assets"][0]["parts"]
    results = []
    for extension in ("glb", "fbx"):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        path = output / "Models" / ("original-station-runtime." + extension)
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
        assert set(meshes) == set(expected_parts), set(meshes)
        assert len(bpy.data.objects) == 4
        for name, obj in meshes.items():
            measured = recipe.metrics(obj)
            expected = expected_parts[name]
            assert measured["triangles"] == expected["triangles"]
            assert measured["materials"] == 1
            for end in ("min", "max"):
                assert all(abs(a - b) < .00005 for a, b in zip(
                    measured["bounds_blender_z_up"][end], expected["bounds_blender_z_up"][end]))
            material = obj.data.materials[0]
            images = [node.image for node in material.node_tree.nodes
                      if node.type == "TEX_IMAGE" and node.image is not None]
            assert len(images) == 1 and tuple(images[0].size) == (1024, 512)
            if name == "RoofTint":
                assert all(.50 <= uv.uv.x <= .75 and 0 <= uv.uv.y <= .5
                           for uv in obj.data.uv_layers.active.data), "roof must sample only neutral swatch"
            results.append({"file": path.name, "part": name,
                            "triangles": measured["triangles"], "bounds_and_atlas_match": True})
    report = {"verified": results, "fbx_embedded_atlas_matches": True,
              "roof_samples_neutral_swatch_only": True,
              "generator_sha256": record["generator_sha256"],
              "not_verified": ["Unity import", "URP binding", "actual board rendering"]}
    (output / "EXPORT-VERIFICATION.json").write_text(json.dumps(report, indent=2) + "\n")
    print("ORIGINAL_STATION_EXPORT_VERIFIED " + json.dumps(report), flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--verify-existing", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    output = args.output_dir.resolve()
    for folder in ("Models", "Textures", "Source", "Previews"):
        (output / folder).mkdir(parents=True, exist_ok=True)
    source = Path(__file__).with_name("blender_original_station_track.py")
    spec = importlib.util.spec_from_file_location("original_station_recipe", source)
    recipe = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(recipe)
    if bpy.app.version[:3] != (5, 1, 2):
        raise RuntimeError("Requires Blender 5.1.2")
    if args.verify_existing:
        verify_exports(output, recipe)
        return
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    # Keep the baseline recipe and its exported bytes unchanged. This variant's
    # red swatch is white, so a URP per-renderer route tint multiplies neutral RGB.
    recipe.PALETTE[2] = ("red", "FFFFFF")
    atlas = recipe.atlas(output)
    recipe.ATLAS_MATERIAL.name = "OriginalStationRuntime_Atlas"
    join = recipe.join_asset
    recipe.join_asset = lambda unused: None
    recipe.station()
    recipe.join_asset = join
    parts = list(recipe.PARTS)
    roof_names = {"Tomato red toy roof", "Soft red ridge cap"}
    badge_names = {"Badge navy post", "Ivory station badge", "Raised tomato circle"}
    groups = {
        "Body": [p for p in parts if p.name not in roof_names | badge_names],
        "RoofTint": [p for p in parts if p.name in roof_names],
        "BadgePost": [p for p in parts if p.name == "Badge navy post"],
        "BadgeFace": [p for p in parts if p.name == "Ivory station badge"],
    }
    for part in parts:
        if part.name == "Raised tomato circle":
            bpy.data.objects.remove(part, do_unlink=True)
    objects = []
    for name, group in groups.items():
        assert group, name
        recipe.PARTS[:] = group
        objects.append(join(name))
    measured = {obj.name: recipe.metrics(obj) for obj in objects}
    for value in measured.values():
        assert value["root_transform_identity"] and value["non_manifold_edges"] == 0
        assert value["materials"] == 1
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    fbx = output / "Models" / "original-station-runtime.fbx"
    glb = output / "Models" / "original-station-runtime.glb"
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={"MESH"},
        axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        apply_unit_scale=True, use_mesh_modifiers=True, bake_anim=False,
        path_mode="COPY", embed_textures=True)
    bpy.ops.export_scene.gltf(filepath=str(glb), use_selection=True, export_format="GLB",
        export_materials="EXPORT", export_animations=False, export_yup=True)
    camera = recipe.setup_stage(48)
    roof = next(obj for obj in objects if obj.name == "RoofTint")
    for obj in objects:
        if obj.name in ("BadgePost", "BadgeFace"):
            obj.hide_render = True
    # Render the exact two meshes that the Unity importer will admit, with one
    # sample route tint. There is intentionally no second decorative badge.
    preview_material = recipe.ATLAS_MATERIAL.copy()
    preview_material.name = "PREVIEW_ONLY red route tint"
    nodes = preview_material.node_tree.nodes
    links = preview_material.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    texture = next(node for node in nodes if node.type == "TEX_IMAGE")
    multiply = nodes.new("ShaderNodeMixRGB")
    multiply.blend_type = "MULTIPLY"
    multiply.inputs[0].default_value = 1
    # Shader values are linear. This is a preview of tintable geometry; actual
    # runtime CatLine colors and the generated badges remain owned by Unity.
    colour = [int("CF533E"[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    linear = [v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in colour]
    multiply.inputs[2].default_value = (*linear, 1)
    links.new(texture.outputs["Color"], multiply.inputs[1])
    links.new(multiply.outputs["Color"], bsdf.inputs["Base Color"])
    roof.data.materials[0] = preview_material
    recipe.render(output, "01-runtime-building-red-preview", camera,
                  (4.7, -7, 4.9), (0, -.08, 1.02), 4.1, (1400, 1200))
    roof.data.materials[0] = recipe.ATLAS_MATERIAL
    for obj in objects:
        obj.hide_render = False
    recipe.render(output, "02-neutral-authoring-parts", camera,
                  (4.7, -7, 4.9), (0, -.08, 1.02), 4.1, (1400, 1200))
    # Source keeps canonical transforms, rather than preview placements.
    atlas.filepath = "//../Textures/original-toy-atlas.png"
    blend = output / "Source" / "original-station-runtime.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), check_existing=False)
    record = {
        "assets": [{"id": "original-station-runtime",
            "authorship": "Original procedural Blender geometry authored for Cat Metro by Codex, 2026-09-10.",
            "source": "Regenerated from the original in-repository station recipe; no imported asset bytes.",
            "licence": "Project-original asset; no third-party asset licence dependencies.",
            "source_assets": [], "external_services": [],
            "parts": measured, "unity_prefab_parts": ["Body", "RoofTint"],
            "runtime_triangles": sum(measured[name]["triangles"] for name in ("Body", "RoofTint")),
            "runtime_renderers": 2, "runtime_materials": 1,
            "badge_note": "No embedded colored circle. Neutral BadgePost/BadgeFace authoring parts are omitted from the Unity prefab; existing dynamic badges remain unchanged.",
            "roof_note": "RoofTint UVs use the neutral-white swatch; tint only this renderer with a property block."}],
        "generator": "scripts/blender_original_station_runtime.py",
        "generator_sha256": digest(Path(__file__)),
        "base_recipe": "scripts/blender_original_station_track.py", "base_recipe_sha256": digest(source),
        "blender": bpy.app.version_string, "build_hash": bpy.app.build_hash.decode(),
        "seed": 20260910,
        "outputs_sha256": {str(path.relative_to(output)): digest(path)
                            for path in (fbx, glb, blend, output / "Textures" / "original-toy-atlas.png")},
        "previews_sha256": {path.name: digest(path) for path in sorted((output / "Previews").glob("*.png"))},
    }
    (output / "PROVENANCE.json").write_text(json.dumps(record, indent=2) + "\n")
    print("ORIGINAL_STATION_RUNTIME " + json.dumps(record["assets"]), flush=True)


if __name__ == "__main__":
    main()
