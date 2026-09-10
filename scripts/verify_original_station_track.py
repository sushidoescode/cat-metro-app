#!/usr/bin/env python3
"""Reimport the original prototype's actual FBX/GLB exports and render the FBXs.

Run in a fresh background Blender 5.1.2 process with --python-exit-code 97.
This checks interchange files, not Unity material/prefab admission.
"""

import argparse
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import sys

import bpy


def digest(data):
    return hashlib.sha256(data).hexdigest()


def embedded_fbx_images(path):
    from io_scene_fbx import parse_fbx
    root, version = parse_fbx.parse(str(path))
    images = []
    def walk(node):
        if node.id == b"Video":
            for child in node.elems:
                if child.id == b"Content" and child.props:
                    images.append(digest(child.props[0]))
        for child in node.elems:
            walk(child)
    walk(root)
    return version, images


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--asset-dir", type=Path, required=True)
    parser.add_argument("--samples", type=int, default=48)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    output = args.asset_dir.resolve()
    source = Path(__file__).with_name("blender_original_station_track.py")
    spec = importlib.util.spec_from_file_location("original_recipe", source)
    recipe = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(recipe)
    evidence = json.loads((output / "PROVENANCE.json").read_text())
    assert evidence["generator_sha256"] == digest(source.read_bytes())
    atlas_digest = digest((output / evidence["texture"]["path"]).read_bytes())
    assert atlas_digest == evidence["texture"]["sha256"]
    imported_records = []
    # GLB first; keep only the FBX pass in the final render scene.
    for extension in ("glb", "fbx"):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        objects = []
        for asset in evidence["assets"]:
            path = output / "Models" / (asset["id"] + "." + extension)
            assert digest(path.read_bytes()) == asset["exports_sha256"][path.name]
            before = set(bpy.data.objects)
            embedded = []
            if extension == "fbx":
                version, embedded = embedded_fbx_images(path)
                assert embedded == [atlas_digest], (path.name, "embedded image bytes differ", embedded)
                bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False, use_image_search=False)
            else:
                bpy.ops.import_scene.gltf(filepath=str(path), loglevel=1)
            imported = set(bpy.data.objects) - before
            assert len(imported) == 1, (path.name, "unexpected extra object", len(imported))
            obj = imported.pop()
            assert obj.type == "MESH", (path.name, obj.type)
            measured = recipe.metrics(obj)
            expected = asset["metrics"]
            assert measured["triangles"] == expected["triangles"], (path.name, measured)
            assert measured["materials"] == 1, (path.name, measured)
            # glTF/FBX split UV and normal seams on export; manifold edge counts
            # and vertex counts therefore belong to authoring meshes, not imports.
            actual_bounds = measured["bounds_blender_z_up"]
            for end in ("min", "max"):
                for a, b in zip(actual_bounds[end], expected["bounds_blender_z_up"][end]):
                    assert abs(a - b) < .00005, (path.name, end, a, b)
            material = obj.data.materials[0]
            textures = [node.image for node in material.node_tree.nodes
                        if node.type == "TEX_IMAGE" and node.image is not None]
            assert len(textures) == 1 and tuple(textures[0].size) == (1024, 512), path.name
            bsdf = next(node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
            assert bsdf.inputs["Base Color"].is_linked, (path.name, "missing base colour texture binding")
            imported_records.append({"file": path.name, "triangles": measured["triangles"],
                "materials": 1, "texture_bound": True, "texture_dimensions": list(textures[0].size),
                "bounds_match_tolerance": .00005, "fbx_embedded_images_sha256": embedded,
                "blender_import_rotation": list(obj.rotation_euler)})
            objects.append(obj)
    camera = recipe.setup_stage(args.samples)
    station, straight, curve = objects
    station.location = (-.7, 1.22, 0)
    straight.location = (-2.65, -.55, 0)
    straight.rotation_euler.z = -math.pi / 2
    curve.location = (.55, -.55, 0)
    curve.rotation_euler.z = -math.pi / 2
    recipe.render(output, "06-fbx-roundtrip", camera, (6, -9, 7), (0, -.1, .75), 7.1, (1600, 1200))
    report = {"blender": bpy.app.version_string, "generator_sha256": evidence["generator_sha256"],
        "verified": imported_records,
        "preview": "Previews/06-fbx-roundtrip.png",
        "preview_sha256": digest((output / "Previews" / "06-fbx-roundtrip.png").read_bytes()),
        "not_verified": ["Unity import and URP material binding", "runtime catalog admission",
                         "camera composition in authored levels", "Android rendering or frame time"]}
    (output / "EXPORT-VERIFICATION.json").write_text(json.dumps(report, indent=2) + "\n")
    print("ORIGINAL_EXPORT_VERIFIED " + json.dumps(report), flush=True)


if __name__ == "__main__":
    main()
