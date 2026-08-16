#!/usr/bin/env bash
# Behavioral contract for the future strict GLB inspector.  Every assertion
# names the production break it catches and uses hand-derived fixture values.
set -euo pipefail

metrics_script=${GLB_METRICS_SCRIPT:-scripts/glb_metrics.py}
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

write_fixture() {
  PYTHONDONTWRITEBYTECODE=1 python3 tests/assets/glb_fixture.py "$@"
}

run_metrics() {
  PYTHONDONTWRITEBYTECODE=1 python3 "$metrics_script" "$@"
}

write_fixture "$tmp/valid.glb" --triangles 37 --translate 4 5 6
write_fixture "$tmp/two-primitives.glb" --triangles 12 --primitive-count 2
write_fixture "$tmp/scene-content.glb" --triangles 12 --primitive-count 2 --add-scene-content
write_fixture "$tmp/topology-1.glb" --triangles 1
write_fixture "$tmp/topology-2.glb" --triangles 2
write_fixture "$tmp/topology-30000.glb" --triangles 30000
PYTHONDONTWRITEBYTECODE=1 python3 - \
  "$tmp/topology-1.glb" "$tmp/topology-2.glb" "$tmp/topology-30000.glb" <<'PY'
import json
import math
import struct
import sys
from pathlib import Path


def read_glb(path):
    raw = Path(path).read_bytes()
    json_length, kind = struct.unpack_from("<I4s", raw, 12)
    assert kind == b"JSON"
    document = json.loads(raw[20:20 + json_length].rstrip(b" "))
    bin_start = 20 + json_length
    bin_length, bin_kind = struct.unpack_from("<I4s", raw, bin_start)
    assert bin_kind == b"BIN\0"
    return document, raw[bin_start + 8:bin_start + 8 + bin_length]


def accessor_values(document, binary, accessor_number):
    accessor = document["accessors"][accessor_number]
    view = document["bufferViews"][accessor["bufferView"]]
    formats = {
        (5123, "SCALAR"): ("H", 1),
        (5126, "VEC2"): ("f", 2),
        (5126, "VEC3"): ("f", 3),
    }
    code, width = formats[(accessor["componentType"], accessor["type"])]
    element_size = struct.calcsize("<" + code * width)
    stride = view.get("byteStride", element_size)
    start = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
    return [
        struct.unpack_from("<" + code * width, binary, start + index * stride)
        for index in range(accessor["count"])
    ]


for path, triangle_count, vertex_count in zip(
    sys.argv[1:], (1, 2, 30_000), (3, 4, 30_002), strict=True
):
    document, binary = read_glb(path)
    primitive = document["meshes"][0]["primitives"][0]
    positions = accessor_values(
        document, binary, primitive["attributes"]["POSITION"]
    )
    uvs = accessor_values(
        document, binary, primitive["attributes"]["TEXCOORD_0"]
    )
    indices = [
        entry[0]
        for entry in accessor_values(document, binary, primitive["indices"])
    ]
    faces = [tuple(indices[start:start + 3]) for start in range(0, len(indices), 3)]

    # Break caught: the shared fixture again uses decorative/unreferenced
    # vertices or repeated/zero-area faces to imitate a large mesh.
    assert len(positions) == len(uvs) == vertex_count
    assert len(faces) == triangle_count
    assert set(indices) == set(range(vertex_count))
    geometric_faces = set()
    for face in faces:
        points = [positions[index] for index in face]
        ab = tuple(points[1][axis] - points[0][axis] for axis in range(3))
        ac = tuple(points[2][axis] - points[0][axis] for axis in range(3))
        cross = (
            ab[1] * ac[2] - ab[2] * ac[1],
            ab[2] * ac[0] - ab[0] * ac[2],
            ab[0] * ac[1] - ab[1] * ac[0],
        )
        assert math.sqrt(sum(value * value for value in cross)) > 0.0
        geometric_faces.add(tuple(sorted(points)))
    assert len(geometric_faces) == triangle_count
    assert [min(point[axis] for point in positions) for axis in range(3)] == [
        -1.0, -1.0, -1.0
    ]
    assert [max(point[axis] for point in positions) for axis in range(3)] == [
        1.0, 1.0, 1.0
    ]
PY
PYTHONDONTWRITEBYTECODE=1 python3 - "$tmp/two-primitives.glb" "$tmp/scene-content.glb" <<'PY'
import json
import struct
import sys
from pathlib import Path

raw = Path(sys.argv[1]).read_bytes()
json_length, kind = struct.unpack_from("<I4s", raw, 12)
assert kind == b"JSON"
doc = json.loads(raw[20:20 + json_length].rstrip(b" "))
bin_start = 20 + json_length
bin_length, bin_kind = struct.unpack_from("<I4s", raw, bin_start)
assert bin_kind == b"BIN\0"
binary = raw[bin_start + 8:bin_start + 8 + bin_length]
positions = []
index_entries = 0
for primitive in doc["meshes"][0]["primitives"]:
    accessor = doc["accessors"][primitive["attributes"]["POSITION"]]
    view = doc["bufferViews"][accessor["bufferView"]]
    assert view["byteOffset"] % 4 == 0
    for index in range(accessor["count"]):
        positions.append(struct.unpack_from("<3f", binary, view["byteOffset"] + index * 12))
    index_entries += doc["accessors"][primitive["indices"]]["count"]
# Break caught: distributing primitive accessors expands requested global bounds.
assert [min(point[axis] for point in positions) for axis in range(3)] == [-1.0, -1.0, -1.0]
assert [max(point[axis] for point in positions) for axis in range(3)] == [1.0, 1.0, 1.0]
# Break caught: splitting triangles across primitives changes their requested total.
assert index_entries == 36

scene_raw = Path(sys.argv[2]).read_bytes()
scene_json_length, scene_kind = struct.unpack_from("<I4s", scene_raw, 12)
assert scene_kind == b"JSON"
scene = json.loads(scene_raw[20:20 + scene_json_length].rstrip(b" "))
scene_bin_start = 20 + scene_json_length
scene_bin_length, scene_bin_kind = struct.unpack_from("<I4s", scene_raw, scene_bin_start)
assert scene_bin_kind == b"BIN\0"
scene_binary = scene_raw[scene_bin_start + 8:scene_bin_start + 8 + scene_bin_length]
# Break caught: scene-content fixtures themselves are schema-invalid placeholders.
assert scene["animations"][0]["samplers"][0]["input"] >= 0
assert scene["animations"][0]["channels"][0]["target"] == {"node": 0, "path": "translation"}
assert scene["cameras"][0] == {"type": "perspective", "perspective": {"yfov": 0.7, "znear": 0.1}}
assert scene["skins"][0]["joints"] == [1]
assert len(scene["nodes"]) > 1
assert scene["extensions"]["KHR_lights_punctual"]["lights"][0]["type"] == "point"
target_counts = []
for scene_primitive in scene["meshes"][0]["primitives"]:
    attributes = scene_primitive["attributes"]
    position = scene["accessors"][attributes["POSITION"]]
    joints = scene["accessors"][attributes["JOINTS_0"]]
    weights = scene["accessors"][attributes["WEIGHTS_0"]]
    # Break caught: a skinned primitive has missing/mismatched JOINTS_0 or WEIGHTS_0.
    assert joints["count"] == weights["count"] == position["count"] == 8
    assert joints["componentType"] == 5123 and joints["type"] == "VEC4"
    assert weights["componentType"] == 5126 and weights["type"] == "VEC4"
    joints_view = scene["bufferViews"][joints["bufferView"]]
    weights_view = scene["bufferViews"][weights["bufferView"]]
    joint_values = list(struct.iter_unpack("<4H", scene_binary[joints_view["byteOffset"]:joints_view["byteOffset"] + joints_view["byteLength"]]))
    weight_values = list(struct.iter_unpack("<4f", scene_binary[weights_view["byteOffset"]:weights_view["byteOffset"] + weights_view["byteLength"]]))
    assert joint_values == [(0, 0, 0, 0)] * position["count"]
    assert weight_values == [(1.0, 0.0, 0.0, 0.0)] * position["count"]
    target_counts.append(len(scene_primitive["targets"]))
    morph = scene["accessors"][scene_primitive["targets"][0]["POSITION"]]
    # Break caught: morph POSITION accessors lack required shape/count/bounds facts.
    assert morph["count"] == position["count"] == 8
    assert morph["componentType"] == 5126 and morph["type"] == "VEC3"
    assert morph["min"] == morph["max"] == [0.0, 0.0, 0.0]
# Break caught: primitives in one mesh disagree on morph target count.
assert target_counts == [1, 1]
PY
run_metrics "$tmp/valid.glb" >"$tmp/valid.json"
PYTHONDONTWRITEBYTECODE=1 python3 - "$tmp/valid.json" <<'PY'
import json
import sys

raw = open(sys.argv[1], encoding="utf-8").read()
d = json.loads(raw)
# Break caught: a CLI emits non-canonical/multiple JSON instead of one sorted object.
assert raw == json.dumps(d, sort_keys=True) + "\n"
# Break caught: triangle counts are inferred from vertices or bytes, not indices.
assert d["triangles"] == 37
# Break caught: POSITION accessor vertices are omitted or double-counted.
assert d["vertices"] == 39
# Break caught: mesh, primitive, or material arrays are counted incorrectly.
assert d["meshes"] == d["primitives"] == d["materials"] == 1
# Break caught: an embedded image is classified as external or is missed.
assert d["images"] == d["embedded_images"] == 1
# Break caught: UV or material bindings on the primitive are missed.
assert d["uv_primitives"] == d["material_primitives"] == 1
# Break caught: absent scene content is fabricated by the inspector.
assert d["animations"] == d["cameras"] == d["lights"] == d["skins"] == 0
# Break caught: absent morph targets are fabricated by the inspector.
assert d["morph_targets"] == 0
# Break caught: a self-contained GLB reports a spurious external dependency.
assert d["external_uris"] == []
# Break caught: node translation is not included in actual world POSITION bounds.
assert d["world_bounds"] == {"min": [3.0, 4.0, 5.0], "max": [5.0, 6.0, 7.0]}
PY

cat >"$tmp/mutate_glb.py" <<'PY'
import base64
import json
import struct
import sys
from pathlib import Path


_PNG_1X1_BLUE = bytes.fromhex(
    "89504e470d0a1a0a0000000d49484452000000010000000108060000001f15c489"
    "0000000d4944415478da635048b8f01f000444025033a22d480000000049454e44"
    "ae426082"
)


def read(path):
    data = path.read_bytes()
    json_length, chunk_type = struct.unpack_from("<I4s", data, 12)
    assert chunk_type == b"JSON"
    start = 20
    doc = json.loads(data[start:start + json_length].rstrip(b" "))
    bin_start = start + json_length
    bin_length, bin_type = struct.unpack_from("<I4s", data, bin_start)
    assert bin_type == b"BIN\0"
    return doc, data[bin_start + 8:bin_start + 8 + bin_length]


def write(path, doc, binary):
    encoded = json.dumps(doc, separators=(",", ":"), sort_keys=True).encode()
    encoded += b" " * (-len(encoded) % 4)
    binary += b"\0" * (-len(binary) % 4)
    path.write_bytes(
        struct.pack("<4sII", b"glTF", 2, 12 + 8 + len(encoded) + 8 + len(binary))
        + struct.pack("<I4s", len(encoded), b"JSON") + encoded
        + struct.pack("<I4s", len(binary), b"BIN\0") + binary
    )


def write_raw_json(path, json_payload, binary):
    json_payload += b" " * (-len(json_payload) % 4)
    binary += b"\0" * (-len(binary) % 4)
    path.write_bytes(
        struct.pack("<4sII", b"glTF", 2, 12 + 8 + len(json_payload) + 8 + len(binary))
        + struct.pack("<I4s", len(json_payload), b"JSON") + json_payload
        + struct.pack("<I4s", len(binary), b"BIN\0") + binary
    )


def add_spot_light(doc):
    spot = {"innerConeAngle": 0.1, "outerConeAngle": 0.5}
    doc["extensionsUsed"] = ["KHR_lights_punctual"]
    doc["extensions"] = {"KHR_lights_punctual": {"lights": [{"type": "spot", "intensity": 1.0, "spot": spot}]}}
    doc["nodes"][0]["extensions"] = {"KHR_lights_punctual": {"light": 0}}
    return spot


def append_buffer_view(doc, binary, payload, *, target=None):
    declared_length = doc["buffers"][0]["byteLength"]
    changed = bytearray(binary[:declared_length])
    while len(changed) % 4:
        changed.append(0)
    view = {"buffer": 0, "byteOffset": len(changed), "byteLength": len(payload)}
    if target is not None:
        view["target"] = target
    changed.extend(payload)
    doc["bufferViews"].append(view)
    doc["buffers"][0]["byteLength"] = len(changed)
    return len(doc["bufferViews"]) - 1, bytes(changed)


def index_storage(doc, binary, primitive):
    accessor = doc["accessors"][primitive["indices"]]
    assert accessor["componentType"] == 5123 and accessor["type"] == "SCALAR"
    view = doc["bufferViews"][accessor["bufferView"]]
    start = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
    count = accessor["count"]
    values = list(struct.unpack_from(f"<{count}H", binary, start))
    return accessor, values


def sparse_uv(doc, binary, primitive, indices):
    uv_accessor = doc["accessors"][primitive["attributes"]["TEXCOORD_0"]]
    index_view, binary = append_buffer_view(doc, binary, bytes(indices))
    values_view, binary = append_buffer_view(
        doc, binary, struct.pack("<4f", 0.25, 0.25, 0.75, 0.75)
    )
    uv_accessor["sparse"] = {
        "count": 2,
        "indices": {"bufferView": index_view, "componentType": 5121},
        "values": {"bufferView": values_view},
    }
    return binary


def embedded_image_payload(doc, binary):
    image = doc["images"][0]
    view = doc["bufferViews"][image["bufferView"]]
    start = view.get("byteOffset", 0)
    return binary[start:start + view["byteLength"]]


def add_multi_role_material(doc, binary):
    image_view, binary = append_buffer_view(doc, binary, _PNG_1X1_BLUE)
    doc["images"].append({"bufferView": image_view, "mimeType": "image/png"})
    doc["textures"].append({"source": 1})
    doc["materials"][0]["pbrMetallicRoughness"][
        "metallicRoughnessTexture"
    ] = {"index": 1}
    return binary


path = Path(sys.argv[1])
action = sys.argv[2]
if action == "magic":
    data = path.read_bytes()
    path.write_bytes(b"BAD!" + data[4:])
    raise SystemExit()
if action == "truncated":
    path.write_bytes(path.read_bytes()[:-3])
    raise SystemExit()
if action in {"large-valid-file", "oversized-file"}:
    original = bytearray(path.read_bytes())
    minimum_total = (
        44 * 1024 * 1024
        if action == "large-valid-file"
        else 128 * 1024 * 1024 + 1
    )
    payload_length = minimum_total - len(original) - 8
    payload_length += -payload_length % 4
    total_length = len(original) + 8 + payload_length
    struct.pack_into("<I", original, 8, total_length)
    with path.open("wb") as handle:
        handle.write(original)
        handle.write(struct.pack("<I4s", payload_length, b"TEST"))
        handle.seek(payload_length - 1, 1)
        handle.write(b"\0")
    assert path.stat().st_size == total_length and total_length >= minimum_total
    raise SystemExit()
doc, binary = read(path)
if action == "deep-json":
    # The container and all required glTF fields stay valid; only unknown JSON
    # nesting is hostile to a parser without a depth guard.
    depth = 1_500
    base_json = json.dumps(doc, separators=(",", ":"), sort_keys=True).encode()
    write_raw_json(path, base_json[:-1] + b',"hostile":' + b"[" * depth + b"0" + b"]" * depth + b"}", binary)
    raise SystemExit()
if action == "duplicate-json-key":
    base_json = json.dumps(doc, separators=(",", ":"), sort_keys=True).encode()
    write_raw_json(
        path,
        base_json[:-1] + b',"asset":{"version":"2.0"}}',
        binary,
    )
    raise SystemExit()
if action == "hostile-json-integer":
    base_json = json.dumps(doc, separators=(",", ":"), sort_keys=True).encode()
    write_raw_json(
        path,
        base_json[:-1] + b',"hostile":' + b"9" * 5_000 + b"}",
        binary,
    )
    raise SystemExit()
if action == "oversized-json":
    doc["extras"] = {"padding": "x" * (16 * 1024 * 1024)}
    write(path, doc, binary)
    raise SystemExit()
primitive = doc["meshes"][0]["primitives"][0]
second_primitive = doc["meshes"][0]["primitives"][1] if len(doc["meshes"][0]["primitives"]) > 1 else None
if action == "missing-asset":
    doc.pop("asset", None)
elif action == "asset-version-1":
    doc["asset"]["version"] = "1.0"
elif action == "normalized-position":
    position = doc["accessors"][primitive["attributes"]["POSITION"]]
    position["normalized"] = True
elif action == "position-vec2":
    position = doc["accessors"][primitive["attributes"]["POSITION"]]
    position["type"] = "VEC2"
elif action == "position-unsigned-short":
    position = doc["accessors"][primitive["attributes"]["POSITION"]]
    position["componentType"] = 5123
elif action == "uv-alias-position":
    primitive["attributes"]["TEXCOORD_0"] = primitive["attributes"]["POSITION"]
elif action in {
    "uv-ushort-normalized",
    "uv-ushort-unnormalized",
    "uv-signed-normalized",
}:
    uv = doc["accessors"][primitive["attributes"]["TEXCOORD_0"]]
    view = doc["bufferViews"][uv["bufferView"]]
    changed_binary = bytearray(binary)
    start = view.get("byteOffset", 0) + uv.get("byteOffset", 0)
    count = uv["count"]
    values = tuple(
        value
        for index in range(count)
        for value in (
            round(65_535 * index / max(1, count - 1)),
            0 if index % 2 == 0 else 65_535,
        )
    )
    struct.pack_into(f"<{len(values)}H", changed_binary, start, *values)
    binary = bytes(changed_binary)
    uv["componentType"] = 5122 if action == "uv-signed-normalized" else 5123
    uv["normalized"] = action != "uv-ushort-unnormalized"
elif action.startswith("sparse-uv-"):
    uv_count = doc["accessors"][primitive["attributes"]["TEXCOORD_0"]]["count"]
    sparse_indices = {
        "sparse-uv-valid": [0, uv_count - 1],
        "sparse-uv-duplicate": [0, 0],
        "sparse-uv-descending": [2, 1],
        "sparse-uv-out-of-range": [0, uv_count],
    }[action]
    binary = sparse_uv(doc, binary, primitive, sparse_indices)
elif action == "zero-indices":
    index_accessor, indices = index_storage(doc, binary, primitive)
    view = doc["bufferViews"][index_accessor["bufferView"]]
    start = view.get("byteOffset", 0) + index_accessor.get("byteOffset", 0)
    changed_binary = bytearray(binary)
    struct.pack_into(f"<{len(indices)}H", changed_binary, start, *([0] * len(indices)))
    binary = bytes(changed_binary)
elif action == "repeat-first-face":
    index_accessor, indices = index_storage(doc, binary, primitive)
    view = doc["bufferViews"][index_accessor["bufferView"]]
    start = view.get("byteOffset", 0) + index_accessor.get("byteOffset", 0)
    repeated = [value for _ in range(len(indices) // 3) for value in (0, 1, 2)]
    changed_binary = bytearray(binary)
    struct.pack_into(f"<{len(repeated)}H", changed_binary, start, *repeated)
    binary = bytes(changed_binary)
elif action == "unreferenced-vertex":
    index_accessor, indices = index_storage(doc, binary, primitive)
    position_count = doc["accessors"][primitive["attributes"]["POSITION"]]["count"]
    indices[-3:] = [0, 1, position_count - 2]
    view = doc["bufferViews"][index_accessor["bufferView"]]
    start = view.get("byteOffset", 0) + index_accessor.get("byteOffset", 0)
    changed_binary = bytearray(binary)
    struct.pack_into(f"<{len(indices)}H", changed_binary, start, *indices)
    binary = bytes(changed_binary)
elif action == "append-two-duplicate-faces":
    index_accessor, indices = index_storage(doc, binary, primitive)
    expanded = indices + indices[:6]
    new_view, binary = append_buffer_view(
        doc,
        binary,
        struct.pack(f"<{len(expanded)}H", *expanded),
        target=34963,
    )
    index_accessor["bufferView"] = new_view
    index_accessor["byteOffset"] = 0
    index_accessor["count"] = len(expanded)
elif action == "append-two-degenerate-faces":
    index_accessor, indices = index_storage(doc, binary, primitive)
    expanded = indices + [0, 0, 0, 1, 1, 1]
    new_view, binary = append_buffer_view(
        doc,
        binary,
        struct.pack(f"<{len(expanded)}H", *expanded),
        target=34963,
    )
    index_accessor["bufferView"] = new_view
    index_accessor["byteOffset"] = 0
    index_accessor["count"] = len(expanded)
elif action == "append-duplicate-position-face":
    position_accessor = doc["accessors"][primitive["attributes"]["POSITION"]]
    position_view = doc["bufferViews"][position_accessor["bufferView"]]
    position_start = position_view.get("byteOffset", 0) + position_accessor.get(
        "byteOffset", 0
    )
    position_count = position_accessor["count"]
    position_payload = binary[
        position_start:position_start + position_count * 12
    ]
    position_view_number, binary = append_buffer_view(
        doc, binary, position_payload + position_payload[:36], target=34962
    )
    position_accessor["bufferView"] = position_view_number
    position_accessor["byteOffset"] = 0
    position_accessor["count"] = position_count + 3

    uv_accessor = doc["accessors"][primitive["attributes"]["TEXCOORD_0"]]
    uv_view = doc["bufferViews"][uv_accessor["bufferView"]]
    uv_start = uv_view.get("byteOffset", 0) + uv_accessor.get("byteOffset", 0)
    uv_payload = binary[uv_start:uv_start + position_count * 8]
    uv_view_number, binary = append_buffer_view(
        doc, binary, uv_payload + uv_payload[:24], target=34962
    )
    uv_accessor["bufferView"] = uv_view_number
    uv_accessor["byteOffset"] = 0
    uv_accessor["count"] = position_count + 3

    index_accessor, indices = index_storage(doc, binary, primitive)
    expanded = indices + [position_count, position_count + 1, position_count + 2]
    index_view_number, binary = append_buffer_view(
        doc,
        binary,
        struct.pack(f"<{len(expanded)}H", *expanded),
        target=34963,
    )
    index_accessor["bufferView"] = index_view_number
    index_accessor["byteOffset"] = 0
    index_accessor["count"] = len(expanded)
elif action in {
    "multi-role-valid",
    "multi-role-detach-metallic",
    "multi-role-rebind-metallic",
}:
    binary = add_multi_role_material(doc, binary)
    metallic = doc["materials"][0]["pbrMetallicRoughness"][
        "metallicRoughnessTexture"
    ]
    if action == "multi-role-detach-metallic":
        doc["materials"][0]["pbrMetallicRoughness"].pop(
            "metallicRoughnessTexture"
        )
    elif action == "multi-role-rebind-metallic":
        metallic["index"] = 0
elif action == "detach-base-color":
    doc["materials"][0]["pbrMetallicRoughness"].pop("baseColorTexture")
elif action == "move-base-color-to-normal":
    material = doc["materials"][0]
    material["normalTexture"] = material["pbrMetallicRoughness"].pop(
        "baseColorTexture"
    )
elif action == "base-color-texcoord-1":
    doc["materials"][0]["pbrMetallicRoughness"]["baseColorTexture"][
        "texCoord"
    ] = 1
elif action in {"texcoord1-valid", "texcoord1-missing-second"}:
    assert second_primitive is not None
    doc["materials"][0]["pbrMetallicRoughness"]["baseColorTexture"][
        "texCoord"
    ] = 1
    for mesh_primitive in doc["meshes"][0]["primitives"]:
        attributes = mesh_primitive["attributes"]
        attributes["TEXCOORD_1"] = attributes["TEXCOORD_0"]
    if action == "texcoord1-missing-second":
        second_primitive["attributes"].pop("TEXCOORD_1")
elif action == "alter-image-payload":
    image = doc["images"][0]
    view = doc["bufferViews"][image["bufferView"]]
    changed_binary = bytearray(binary)
    changed_binary[view["byteOffset"] + view["byteLength"] - 1] ^= 1
    binary = bytes(changed_binary)
elif action.startswith("data-image-"):
    image = doc["images"][0]
    original_payload = embedded_image_payload(doc, binary)
    image.pop("bufferView")
    image.pop("mimeType", None)
    if action == "data-image-valid":
        uri = "data:image/png;base64," + base64.b64encode(original_payload).decode()
    elif action == "data-image-invalid-base64":
        uri = "data:image/png;base64,NOT-BASE64"
    elif action == "data-image-wrong-media":
        uri = "data:text/plain;base64," + base64.b64encode(original_payload).decode()
    elif action == "data-image-oversized":
        uri = "data:image/png;base64," + base64.b64encode(
            b"x" * (8 * 1024 * 1024 + 1)
        ).decode()
    else:
        raise AssertionError(action)
    image["uri"] = uri
elif action == "too-many-accessors":
    template = dict(doc["accessors"][0])
    doc["accessors"].extend(
        dict(template) for _ in range(65_537 - len(doc["accessors"]))
    )
elif action == "accessor-count-limit":
    doc["accessors"][primitive["attributes"]["POSITION"]]["count"] = 50_000_001
elif action == "mode":
    primitive["mode"] = 1
elif action == "declared-bounds":
    accessor = doc["accessors"][primitive["attributes"]["POSITION"]]
    accessor["min"] = [0.0, 0.0, 0.0]
    accessor["max"] = [0.0, 0.0, 0.0]
elif action == "translate":
    doc["nodes"][0]["translation"] = [1.0, 0.0, 0.0]
elif action == "remove-one-uv":
    assert second_primitive is not None
    second_primitive["attributes"].pop("TEXCOORD_0")
elif action == "remove-one-material":
    assert second_primitive is not None
    second_primitive.pop("material")
elif action == "extension":
    doc["extensionsUsed"] = ["VENDOR_unreviewed"]
elif action == "hostile-diagnostic-extension":
    hostile = (
        "\x1b[31m\x01\nAuthorization: Bearer "
        "CATMETRO_TEST_CREDENTIAL_SENTINEL\r" + "X" * 4_096
    )
    doc["extensionsUsed"] = [hostile, hostile]
elif action == "meshopt":
    doc["extensionsUsed"] = ["EXT_meshopt_compression"]
elif action == "extras-metadata":
    doc["extras"] = {"extensions": ["png", "jpg"], "fixture": "root metadata"}
    doc["nodes"][0]["extras"] = {"extensions": {"renderer": {"quality": "preview"}}}
    primitive["extras"] = {"extensions": ["thumbnail", "preview"]}
    doc["materials"][0]["extras"] = {"extensions": {"authoring": {"source": "fixture"}}}
elif action == "spot-vendor-extension":
    spot = add_spot_light(doc)
    spot["extensions"] = {"VENDOR_undeclared": {}}
elif action == "spot-extras-list":
    spot = add_spot_light(doc)
    spot["extras"] = {"extensions": ["png", "jpg"], "fixture": "opaque list metadata"}
elif action == "spot-extras-object":
    spot = add_spot_light(doc)
    spot["extras"] = {"extensions": {"asset_pipeline": {"format": "png", "quality": "preview"}}}
elif action == "undeclared-draco":
    doc.pop("extensionsUsed", None)
    primitive["extensions"] = {"KHR_draco_mesh_compression": {"bufferView": 0, "attributes": {"POSITION": 0}}}
elif action == "out-of-range-index":
    index_accessor = doc["accessors"][primitive["indices"]]
    index_view = doc["bufferViews"][index_accessor["bufferView"]]
    index_offset = index_view["byteOffset"] + index_accessor.get("byteOffset", 0)
    changed_binary = bytearray(binary)
    struct.pack_into("<H", changed_binary, index_offset, 255)
    binary = bytes(changed_binary)
elif action == "misaligned-accessor":
    for view in doc["bufferViews"]:
        view["byteOffset"] += 1
    binary = b"\0" + binary
    doc["buffers"][0]["byteLength"] = len(binary) + (-len(binary) % 4)
elif action == "deep-node-cycle":
    depth = 1_500
    doc["nodes"] = [
        {"mesh": 0, "translation": [0.0, 0.0, 0.0], "children": [1]},
        *({"children": [index + 1]} for index in range(1, depth)),
        {"children": [0]},
    ]
    doc["scenes"] = [{"nodes": [0]}]
    doc["scene"] = 0
elif action.startswith("only-"):
    content = action.removeprefix("only-")
    assert content in {"animation", "camera", "light", "skin", "morph"}
    node = doc["nodes"][0]
    if content != "animation":
        doc.pop("animations", None)
    if content != "camera":
        doc.pop("cameras", None)
        node.pop("camera", None)
    if content != "light":
        doc.pop("extensions", None)
        node.pop("extensions", None)
        doc.pop("extensionsUsed", None)
    if content != "skin":
        doc.pop("skins", None)
        node.pop("skin", None)
        node.pop("children", None)
        doc["nodes"] = doc["nodes"][:1]
    if content != "morph":
        primitive.pop("targets", None)
else:
    raise ValueError(action)
write(path, doc, binary)
PY

mutate() {
  PYTHONDONTWRITEBYTECODE=1 python3 "$tmp/mutate_glb.py" "$@"
}

cp "$tmp/valid.glb" "$tmp/extras-metadata.glb"
mutate "$tmp/extras-metadata.glb" extras-metadata
run_metrics "$tmp/extras-metadata.glb" >"$tmp/extras-metadata.json"
PYTHONDONTWRITEBYTECODE=1 python3 - "$tmp/extras-metadata.json" <<'PY'
import json
import sys

d = json.load(open(sys.argv[1], encoding="utf-8"))
# Break caught: opaque extras metadata is mistaken for real glTF extensions.
assert d["extensions_used"] == d["extensions_required"] == []
# Break caught: legal extras metadata changes unrelated ordinary inspection facts.
assert d["triangles"] == 37
assert d["vertices"] == 39
assert d["meshes"] == d["primitives"] == d["materials"] == 1
assert d["images"] == d["embedded_images"] == 1
assert d["uv_primitives"] == d["material_primitives"] == 1
assert d["world_bounds"] == {"min": [3.0, 4.0, 5.0], "max": [5.0, 6.0, 7.0]}
PY

expect_metrics_failure() {
  local name=$1
  local input=$2
  local expected_text=${3:-}
  local stdout="$tmp/${name// /-}.out"
  local stderr="$tmp/${name// /-}.err"
  set +e
  run_metrics "$input" >"$stdout" 2>"$stderr"
  local rc=$?
  set -e
  if [ "$rc" -eq 0 ]; then
    echo "expected failure: $name" >&2
    exit 1
  fi
  # Break caught: malformed input lacks the promised fail-closed diagnostic.
  grep -q '^glb-metrics:' "$stderr"
  if [ -n "$expected_text" ]; then
    grep -Fq "$expected_text" "$stderr"
  fi
}

for spot_case in spot-extras-list spot-extras-object; do
  cp "$tmp/valid.glb" "$tmp/$spot_case.glb"
  mutate "$tmp/$spot_case.glb" "$spot_case"
  run_metrics "$tmp/$spot_case.glb" >"$tmp/$spot_case.json"
done
PYTHONDONTWRITEBYTECODE=1 python3 - "$tmp/spot-extras-list.json" "$tmp/spot-extras-object.json" <<'PY'
import json
import sys

for path in sys.argv[1:]:
    d = json.load(open(path, encoding="utf-8"))
    # Break caught: opaque spot.extras metadata is counted as a glTF extension.
    assert d["extensions_used"] == ["KHR_lights_punctual"]
    assert d["extensions_required"] == []
    # Break caught: valid declared punctual spot lights are not counted.
    assert d["lights"] == 1
    # Break caught: opaque spot metadata alters ordinary inspection metrics.
    assert d["triangles"] == 37
    assert d["vertices"] == 39
    assert d["meshes"] == d["primitives"] == d["materials"] == 1
    assert d["images"] == d["embedded_images"] == 1
    assert d["uv_primitives"] == d["material_primitives"] == 1
    assert d["world_bounds"] == {"min": [3.0, 4.0, 5.0], "max": [5.0, 6.0, 7.0]}
PY

cp "$tmp/valid.glb" "$tmp/spot-vendor-extension.glb"
mutate "$tmp/spot-vendor-extension.glb" spot-vendor-extension
# Break caught: undeclared extensions nested in valid KHR spot objects are ignored.
expect_metrics_failure "undeclared spot vendor extension" "$tmp/spot-vendor-extension.glb" "VENDOR_undeclared"

cp "$tmp/valid.glb" "$tmp/bad-magic.glb"
mutate "$tmp/bad-magic.glb" magic
# Break caught: an inspector accepts a corrupt GLB magic value.
expect_metrics_failure "corrupt magic" "$tmp/bad-magic.glb"

cp "$tmp/valid.glb" "$tmp/truncated.glb"
mutate "$tmp/truncated.glb" truncated
# Break caught: chunk/header lengths can overrun a truncated container.
expect_metrics_failure "truncated chunk" "$tmp/truncated.glb"

write_fixture "$tmp/external.glb" --triangles 3 --external-image
run_metrics "$tmp/external.glb" >"$tmp/external.json"
PYTHONDONTWRITEBYTECODE=1 python3 - "$tmp/external.json" <<'PY'
import json
import sys

d = json.load(open(sys.argv[1], encoding="utf-8"))
# Break caught: external texture URIs are hidden from custody validation.
assert d["external_uris"] == ["fixture-external.png"]
PY

write_fixture "$tmp/omitted.glb" --triangles 3 --omit-uv --omit-material --omit-image
run_metrics "$tmp/omitted.glb" >"$tmp/omitted.json"
PYTHONDONTWRITEBYTECODE=1 python3 - "$tmp/omitted.json" <<'PY'
import json
import sys

d = json.load(open(sys.argv[1], encoding="utf-8"))
# Break caught: absent UV/material/image resources still contribute to metrics.
assert d["uv_primitives"] == d["materials"] == d["material_primitives"] == d["images"] == d["embedded_images"] == 0
PY

cp "$tmp/valid.glb" "$tmp/unsupported-mode.glb"
mutate "$tmp/unsupported-mode.glb" mode
# Break caught: line/point primitive modes are accepted as triangle geometry.
expect_metrics_failure "unsupported primitive mode" "$tmp/unsupported-mode.glb"

write_fixture "$tmp/translated.glb" --triangles 3 --translate -3 2 4
run_metrics "$tmp/translated.glb" >"$tmp/translated.json"
PYTHONDONTWRITEBYTECODE=1 python3 - "$tmp/translated.json" <<'PY'
import json
import sys

d = json.load(open(sys.argv[1], encoding="utf-8"))
# Break caught: node translation is ignored while accumulating world bounds.
assert d["world_bounds"] == {"min": [-4.0, 1.0, 3.0], "max": [-2.0, 3.0, 5.0]}
PY

cp "$tmp/valid.glb" "$tmp/falsified-bounds.glb"
mutate "$tmp/falsified-bounds.glb" declared-bounds
set +e
run_metrics "$tmp/falsified-bounds.glb" >"$tmp/falsified-bounds.json" 2>"$tmp/falsified-bounds.err"
falsified_bounds_rc=$?
set -e
if [ "$falsified_bounds_rc" -eq 0 ]; then
  PYTHONDONTWRITEBYTECODE=1 python3 - "$tmp/falsified-bounds.json" <<'PY'
import json
import sys

d = json.load(open(sys.argv[1], encoding="utf-8"))
# Break caught: declared accessor min/max can conceal the actual POSITION bytes.
assert d["world_bounds"] == {"min": [3.0, 4.0, 5.0], "max": [5.0, 6.0, 7.0]}
PY
else
  # Break caught: if declared bounds are cross-checked, disagreement must be
  # rejected rather than accepted with the falsified values.
  grep -q '^glb-metrics:' "$tmp/falsified-bounds.err"
fi

write_fixture "$tmp/source.glb" --triangles 12 --primitive-count 2
write_fixture "$tmp/output.glb" --triangles 12 --primitive-count 2
run_metrics "$tmp/source.glb" >"$tmp/source.json"
run_metrics "$tmp/output.glb" >"$tmp/output.json"
PYTHONDONTWRITEBYTECODE=1 python3 - "$tmp/source.json" "$tmp/output.json" <<'PY'
import json
import sys

source = json.load(open(sys.argv[1], encoding="utf-8"))
output = json.load(open(sys.argv[2], encoding="utf-8"))
for metrics in (source, output):
    # Break caught: a later primitive is skipped while collecting metrics/bounds.
    assert metrics["meshes"] == 1
    assert metrics["primitives"] == metrics["uv_primitives"] == metrics["material_primitives"] == 2
    assert metrics["world_bounds"] == {"min": [-1.0, -1.0, -1.0], "max": [1.0, 1.0, 1.0]}
PY

expect_preservation() {
  local expectation=$1
  local name=$2
  local source=$3
  local output=$4
  local required_diagnostic=${5:-}
  PYTHONDONTWRITEBYTECODE=1 python3 - "$metrics_script" "$expectation" "$source" "$output" "$name" "$required_diagnostic" <<'PY'
import importlib.util
import sys
from pathlib import Path

script, expectation, source_path, output_path, name, required_diagnostic = sys.argv[1:]
spec = importlib.util.spec_from_file_location("glb_metrics_under_test", Path(script))
assert spec and spec.loader
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
try:
    spec.loader.exec_module(module)
    reasons = module.compare_preservation(module.inspect_glb(Path(source_path)), module.inspect_glb(Path(output_path)))
finally:
    sys.modules.pop(spec.name, None)
assert isinstance(reasons, list), f"{name}: comparator must return a diagnostics list"
if expectation == "accept":
    assert reasons == [], f"{name}: unchanged valid pair was rejected: {reasons!r}"
elif expectation == "reject":
    assert reasons, f"{name}: preservation regression was accepted"
    if required_diagnostic:
        assert any(required_diagnostic.lower() in reason.lower() for reason in reasons), (
            f"{name}: expected explicit diagnostic containing {required_diagnostic!r}, got {reasons!r}"
        )
else:
    raise AssertionError(f"unknown expectation: {expectation}")
PY
}

expect_preservation_acceptance() {
  expect_preservation accept "$@"
}

expect_preservation_rejection() {
  expect_preservation reject "$@"
}

expect_inspection_or_preservation_rejection() {
  local name=$1
  local source=$2
  local output=$3
  local stdout="$tmp/${name// /-}.out"
  local stderr="$tmp/${name// /-}.err"
  set +e
  run_metrics "$output" >"$stdout" 2>"$stderr"
  local rc=$?
  set -e
  if [ "$rc" -ne 0 ]; then
    grep -q '^glb-metrics:' "$stderr"
  else
    expect_preservation_rejection "$name" "$source" "$output"
  fi
}

expect_single_diagnostic_failure() {
  local name=$1
  local input=$2
  local stdout="$tmp/${name// /-}.out"
  local stderr="$tmp/${name// /-}.err"
  set +e
  run_metrics "$input" >"$stdout" 2>"$stderr"
  local rc=$?
  set -e
  if [ "$rc" -eq 0 ]; then
    echo "expected hostile-input failure: $name" >&2
    exit 1
  fi
  PYTHONDONTWRITEBYTECODE=1 python3 - "$name" "$stdout" "$stderr" <<'PY'
import sys
from pathlib import Path

name, stdout_path, stderr_path = sys.argv[1:]
stdout = Path(stdout_path).read_text(encoding="utf-8")
stderr = Path(stderr_path).read_text(encoding="utf-8")
lines = stderr.splitlines()
assert stdout == "", f"{name}: hostile input wrote stdout: {stdout!r}"
assert len(lines) == 1 and lines[0].startswith("glb-metrics:"), f"{name}: expected one glb-metrics diagnostic, got {stderr!r}"
assert "Traceback" not in stderr and "RecursionError" not in stderr, f"{name}: Python exception escaped: {stderr!r}"
PY
}

# Break caught: a comparator rejects every output, including an unchanged valid pair.
expect_preservation_acceptance "unchanged baseline" "$tmp/source.glb" "$tmp/output.glb"

cp "$tmp/output.glb" "$tmp/undeclared-draco.glb"
mutate "$tmp/undeclared-draco.glb" undeclared-draco
# Break caught: an actual undeclared Draco payload bypasses the extension allowlist.
expect_inspection_or_preservation_rejection "undeclared Draco payload" "$tmp/source.glb" "$tmp/undeclared-draco.glb"

cp "$tmp/valid.glb" "$tmp/out-of-range-index.glb"
mutate "$tmp/out-of-range-index.glb" out-of-range-index
# Break caught: decoded indices outside the eight POSITION vertices are trusted.
expect_metrics_failure "out-of-range index" "$tmp/out-of-range-index.glb"

cp "$tmp/valid.glb" "$tmp/misaligned-accessor.glb"
mutate "$tmp/misaligned-accessor.glb" misaligned-accessor
# Break caught: accessor alignment ignores the bufferView byte offset.
expect_metrics_failure "misaligned effective accessor" "$tmp/misaligned-accessor.glb"

cp "$tmp/valid.glb" "$tmp/deep-node-cycle.glb"
mutate "$tmp/deep-node-cycle.glb" deep-node-cycle
# Break caught: hostile selected-scene depth/cycles leak a Python recursion traceback.
expect_single_diagnostic_failure "deep node cycle" "$tmp/deep-node-cycle.glb"

cp "$tmp/valid.glb" "$tmp/deep-json.glb"
mutate "$tmp/deep-json.glb" deep-json
# Break caught: hostile JSON nesting leaks a Python recursion traceback.
expect_single_diagnostic_failure "deep JSON" "$tmp/deep-json.glb"

cp "$tmp/output.glb" "$tmp/center-drift.glb"
mutate "$tmp/center-drift.glb" translate
# Break caught: output center moves more than the 0.5%-of-source-extent limit.
expect_preservation_rejection "center drift" "$tmp/source.glb" "$tmp/center-drift.glb"

PYTHONDONTWRITEBYTECODE=1 python3 - "$tmp/scale-drift.glb" "$tmp/shape-drift.glb" <<'PY'
import sys
from pathlib import Path

sys.path.insert(0, "tests/assets")
from glb_fixture import write_glb

# Hand-derived bounds: longest extent grows from 2 to 4, then the normalized
# Y/Z extent changes from 1.0 to 0.5 while X remains the longest axis.
write_glb(Path(sys.argv[1]), triangles=12, primitive_count=2,
          bounds=((-2.0, -2.0, -2.0), (2.0, 2.0, 2.0)))
write_glb(Path(sys.argv[2]), triangles=12, primitive_count=2,
          bounds=((-1.0, -0.5, -1.0), (1.0, 0.5, 1.0)))
PY
# Break caught: uniform scale changes beyond the 1% preservation limit.
expect_preservation_rejection "scale drift" "$tmp/source.glb" "$tmp/scale-drift.glb"
# Break caught: a non-uniform normalized extent change beyond 2% is accepted.
expect_preservation_rejection "normalized extent drift" "$tmp/source.glb" "$tmp/shape-drift.glb"

cp "$tmp/output.glb" "$tmp/missing-one-uv.glb"
mutate "$tmp/missing-one-uv.glb" remove-one-uv
# Break caught: a UV binding can disappear from one of several primitives.
expect_inspection_or_preservation_rejection \
  "missing UV binding" "$tmp/source.glb" "$tmp/missing-one-uv.glb"

cp "$tmp/output.glb" "$tmp/missing-one-material.glb"
mutate "$tmp/missing-one-material.glb" remove-one-material
# Break caught: a material binding can disappear from one of several primitives.
expect_preservation_rejection "missing material binding" "$tmp/source.glb" "$tmp/missing-one-material.glb"

write_fixture "$tmp/missing-image.glb" --triangles 12 --primitive-count 2 --omit-image
# Break caught: a derivative loses an embedded image while retaining geometry.
expect_preservation_rejection "missing embedded image" "$tmp/source.glb" "$tmp/missing-image.glb"

write_fixture "$tmp/external-output.glb" --triangles 12 --primitive-count 2 --external-image
# Break caught: an output becomes dependent on an external texture URI.
expect_preservation_rejection "external URI" "$tmp/source.glb" "$tmp/external-output.glb"

for extension_case in extension meshopt; do
  cp "$tmp/output.glb" "$tmp/$extension_case.glb"
  mutate "$tmp/$extension_case.glb" "$extension_case"
done
# Break caught: arbitrary output extensions bypass the empty extension allowlist.
expect_preservation_rejection "arbitrary extension" "$tmp/source.glb" "$tmp/extension.glb"
# Break caught: EXT_meshopt_compression is accepted despite Unity compatibility policy.
expect_preservation_rejection "meshopt compression" "$tmp/source.glb" "$tmp/meshopt.glb" "compression extension"

for scene_case in animation camera light skin morph; do
  cp "$tmp/scene-content.glb" "$tmp/$scene_case.glb"
  mutate "$tmp/$scene_case.glb" "only-$scene_case"
done
# Break caught: an animation is introduced into a static derivative.
expect_preservation_rejection "animation" "$tmp/source.glb" "$tmp/animation.glb"
# Break caught: a camera is introduced into a static derivative.
expect_preservation_rejection "camera" "$tmp/source.glb" "$tmp/camera.glb"
# Break caught: a punctual light is introduced into a static derivative.
expect_preservation_rejection "light" "$tmp/source.glb" "$tmp/light.glb"
# Break caught: a skin is introduced into a static derivative.
expect_preservation_rejection "skin" "$tmp/source.glb" "$tmp/skin.glb"
# Break caught: a morph target is introduced into a static derivative.
expect_preservation_rejection "morph target" "$tmp/source.glb" "$tmp/morph.glb"

# Hardening fixtures use independent byte/document mutations.  Positive
# controls sit beside each hostile case so a parser that rejects everything,
# returns constant topology/hash fields, or compares only resource counts
# cannot turn this section green.
for action in \
  missing-asset asset-version-1 normalized-position position-vec2 \
  position-unsigned-short uv-alias-position uv-ushort-normalized \
  uv-ushort-unnormalized uv-signed-normalized sparse-uv-valid \
  sparse-uv-duplicate sparse-uv-descending sparse-uv-out-of-range \
  zero-indices repeat-first-face unreferenced-vertex \
  append-two-duplicate-faces append-two-degenerate-faces \
  append-duplicate-position-face \
  multi-role-valid multi-role-detach-metallic multi-role-rebind-metallic \
  detach-base-color move-base-color-to-normal base-color-texcoord-1 \
  alter-image-payload \
  data-image-valid data-image-invalid-base64 data-image-wrong-media \
  data-image-oversized too-many-accessors accessor-count-limit \
  duplicate-json-key hostile-json-integer oversized-json large-valid-file \
  oversized-file hostile-diagnostic-extension
do
  cp "$tmp/valid.glb" "$tmp/$action.glb"
  mutate "$tmp/$action.glb" "$action"
done

for action in texcoord1-valid texcoord1-missing-second; do
  cp "$tmp/two-primitives.glb" "$tmp/$action.glb"
  mutate "$tmp/$action.glb" "$action"
done

# No writer is paired with this FIFO.  A reader that merely stats it and then
# reads to EOF will block; the bounded child below therefore distinguishes the
# required pre-open special-file rejection without risking the test runner.
mkfifo "$tmp/special-source.fifo"

set +e
PYTHONDONTWRITEBYTECODE=1 python3 - "$metrics_script" "$tmp" <<'PY'
import importlib.util
import json
import subprocess
import struct
import sys
from pathlib import Path


script = Path(sys.argv[1])
fixture_root = Path(sys.argv[2])
spec = importlib.util.spec_from_file_location("glb_metrics_hardening", script)
assert spec and spec.loader
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)

PNG_SHA256 = "6587ef8f161ecd2a57c16e50f2c264221a76f89acbd2bdb169d648225fc58400"
BLUE_PNG_SHA256 = "e5f51f6524bb1ea3b9fd6ecdeac8a3773f213c45c598aa49835d982d0f05ec55"
BASE_COLOR_BINDING = [{
    "material": 0,
    "role": "baseColor",
    "texcoord": 0,
    "payload_sha256": PNG_SHA256,
}]
NORMAL_BINDING = [{
    "material": 0,
    "role": "normal",
    "texcoord": 0,
    "payload_sha256": PNG_SHA256,
}]
MULTI_ROLE_BINDINGS = [
    {
        "material": 0,
        "role": "baseColor",
        "texcoord": 0,
        "payload_sha256": PNG_SHA256,
    },
    {
        "material": 0,
        "role": "metallicRoughness",
        "texcoord": 0,
        "payload_sha256": BLUE_PNG_SHA256,
    },
]
REBOUND_MULTI_ROLE_BINDINGS = [
    MULTI_ROLE_BINDINGS[0],
    {
        "material": 0,
        "role": "metallicRoughness",
        "texcoord": 0,
        "payload_sha256": PNG_SHA256,
    },
]
failures = []


def check(name, operation):
    try:
        operation()
    except Exception as exc:  # collect every independent RED in one invocation
        failures.append(f"{name}: {type(exc).__name__}: {exc}")


def inspect(name):
    return module.inspect_glb(fixture_root / f"{name}.glb")


def document(name):
    raw = (fixture_root / f"{name}.glb").read_bytes()
    json_length, kind = struct.unpack_from("<I4s", raw, 12)
    assert kind == b"JSON"
    return json.loads(raw[20:20 + json_length].rstrip(b" "))


def binary_payload(name):
    raw = (fixture_root / f"{name}.glb").read_bytes()
    json_length, json_kind = struct.unpack_from("<I4s", raw, 12)
    assert json_kind == b"JSON"
    binary_start = 20 + json_length
    binary_length, binary_kind = struct.unpack_from("<I4s", raw, binary_start)
    assert binary_kind == b"BIN\0"
    return raw[binary_start + 8:binary_start + 8 + binary_length]


def assert_surface(
    metrics, *, triangles, vertices, referenced, unique, degenerate=0
):
    # Break caught: raw index-entry count is replaced by the deduplicated count,
    # or surface facts are absent/constant and cannot expose fake topology.
    # ``unique_triangles`` is the number of distinct non-degenerate geometric
    # faces; the separate degenerate count keeps the two-face source duplicate
    # allowance from becoming a two-zero-area-face loophole.
    assert metrics["triangles"] == triangles
    assert metrics["vertices"] == vertices
    assert metrics["referenced_vertices"] == referenced
    assert metrics["unique_triangles"] == unique
    assert metrics["degenerate_triangles"] == degenerate


def assert_texture_facts(metrics):
    # Literals are derived from the fixed 70-byte PNG in the fixture, not from
    # the inspector.  Counts alone cannot satisfy either assertion.
    assert metrics["image_payload_sha256"] == [PNG_SHA256]
    assert metrics["material_texture_bindings"] == BASE_COLOR_BINDING


def valid_control():
    metrics = inspect("valid")
    assert_surface(
        metrics, triangles=37, vertices=39, referenced=39, unique=37
    )
    assert_texture_facts(metrics)
    assert module.compare_preservation(metrics, metrics) == []


check("valid strict surface/resource control", valid_control)


def real_envelope_file_control():
    source = inspect("valid")
    metrics = inspect("large-valid-file")
    # The largest reviewed source is about 71.7 MiB.  A valid 44 MiB control
    # keeps a guessed 20 MiB file cap from rejecting known-envelope inputs.
    assert metrics["bytes"] >= 44 * 1024 * 1024
    assert_surface(
        metrics, triangles=37, vertices=39, referenced=39, unique=37
    )
    assert_texture_facts(metrics)
    assert module.compare_preservation(source, metrics) == []


check("valid 44 MiB real-envelope file control", real_envelope_file_control)


def fixture_scale_controls():
    for name, triangles, vertices in (
        ("topology-1", 1, 3),
        ("topology-2", 2, 4),
        ("topology-30000", 30_000, 30_002),
    ):
        metrics = inspect(name)
        assert_surface(
            metrics,
            triangles=triangles,
            vertices=vertices,
            referenced=vertices,
            unique=triangles,
        )
        assert metrics["uv_primitives"] == 1
        assert metrics["world_bounds"] == {
            "min": [-1.0, -1.0, -1.0],
            "max": [1.0, 1.0, 1.0],
        }


check("1/2/30k topology metrics are measured, not constant", fixture_scale_controls)


def supported_uv_controls():
    default = inspect("valid")
    normalized_ushort = inspect("uv-ushort-normalized")
    assert default["uv_primitives"] == normalized_ushort["uv_primitives"] == 1
    assert_surface(
        normalized_ushort,
        triangles=37,
        vertices=39,
        referenced=39,
        unique=37,
    )


check("FLOAT and normalized-ushort VEC2 UV controls", supported_uv_controls)


def sparse_uv_control():
    metrics = inspect("sparse-uv-valid")
    assert metrics["uv_primitives"] == 1
    assert_surface(
        metrics, triangles=37, vertices=39, referenced=39, unique=37
    )


check("strictly increasing in-range sparse UV control", sparse_uv_control)


def data_image_control():
    metrics = inspect("data-image-valid")
    assert metrics["embedded_images"] == 1
    assert_texture_facts(metrics)
    assert module.compare_preservation(inspect("valid"), metrics) == []


check("valid bounded base64 image control", data_image_control)


def topology_output(name, referenced, unique, diagnostic, *, degenerate=0):
    source = inspect("valid")
    output = inspect(name)
    assert_surface(
        output,
        triangles=37,
        vertices=39,
        referenced=referenced,
        unique=unique,
        degenerate=degenerate,
    )
    reasons = module.compare_preservation(source, output)
    assert reasons, f"{name} fake surface was accepted"
    assert any(diagnostic in reason.lower() for reason in reasons), reasons


check(
    "zero-area output surface",
    lambda: topology_output(
        "zero-indices", 1, 0, "degenerate triangle", degenerate=37
    ),
)
check(
    "repeated-face output surface",
    lambda: topology_output("repeat-first-face", 3, 1, "unique triangle"),
)


def duplicate_positions_output():
    source = inspect("valid")
    output = inspect("append-duplicate-position-face")
    # The appended face has three fresh indexes and all 42 vertices are used,
    # but its three POSITION values duplicate face zero.  Index-tuple
    # deduplication therefore cannot satisfy the geometric surface metric.
    assert_surface(
        output, triangles=38, vertices=42, referenced=42, unique=37
    )
    reasons = module.compare_preservation(source, output)
    assert reasons and any("unique triangle" in reason.lower() for reason in reasons), reasons


check("duplicate geometric face through distinct indexes", duplicate_positions_output)
check(
    "unreferenced output vertex",
    lambda: topology_output("unreferenced-vertex", 38, 37, "referenced vert"),
)


def known_source_duplicate_delta():
    source = inspect("append-two-duplicate-faces")
    output = inspect("valid")
    # The two current source GLBs each have exactly this raw-vs-unique delta.
    # Preserve raw triangle semantics and permit the source-side delta only;
    # clean outputs remain strict.
    assert_surface(
        source, triangles=39, vertices=39, referenced=39, unique=37
    )
    assert_surface(
        output, triangles=37, vertices=39, referenced=39, unique=37
    )
    assert module.compare_preservation(source, output) == []


check("known source duplicate-face delta of two", known_source_duplicate_delta)


def duplicate_source_does_not_weaken_output_checks():
    source = inspect("append-two-duplicate-faces")
    assert_surface(
        source, triangles=39, vertices=39, referenced=39, unique=37
    )
    for output_name, diagnostic in (
        ("zero-indices", "degenerate triangle"),
        ("repeat-first-face", "unique triangle"),
        ("append-duplicate-position-face", "unique triangle"),
        ("unreferenced-vertex", "referenced vert"),
    ):
        reasons = module.compare_preservation(source, inspect(output_name))
        assert reasons, f"duplicate source bypassed {output_name} output check"
        assert any(diagnostic in reason.lower() for reason in reasons), reasons


check(
    "duplicate-face source still enforces every output topology check",
    duplicate_source_does_not_weaken_output_checks,
)


def source_degenerate_delta_is_not_the_duplicate_allowance():
    source = inspect("append-two-degenerate-faces")
    output = inspect("valid")
    assert_surface(
        source,
        triangles=39,
        vertices=39,
        referenced=39,
        unique=37,
        degenerate=2,
    )
    reasons = module.compare_preservation(source, output)
    assert reasons and any("degenerate triangle" in reason.lower() for reason in reasons), reasons


check(
    "source degenerate faces cannot borrow duplicate-face allowance",
    source_degenerate_delta_is_not_the_duplicate_allowance,
)


def multi_role_texture_control():
    metrics = inspect("multi-role-valid")
    assert metrics["materials"] == 1
    assert metrics["images"] == metrics["embedded_images"] == 2
    assert metrics["image_payload_sha256"] == [PNG_SHA256, BLUE_PNG_SHA256]
    assert metrics["material_texture_bindings"] == MULTI_ROLE_BINDINGS
    assert module.compare_preservation(metrics, metrics) == []


check("base-color plus metallic-roughness valid control", multi_role_texture_control)


def per_primitive_texcoord1_requirement():
    default_document = document("two-primitives")
    default_primitives = default_document["meshes"][0]["primitives"]
    default_texture = default_document["materials"][0][
        "pbrMetallicRoughness"
    ]["baseColorTexture"]
    # The absent texCoord property means TEXCOORD_0; every using primitive has
    # that set, so strict validation must retain the default-set control.
    assert default_texture == {"index": 0}
    assert all(
        "TEXCOORD_0" in primitive["attributes"]
        for primitive in default_primitives
    )
    default_metrics = inspect("two-primitives")
    assert default_metrics["uv_primitives"] == 2
    assert module.compare_preservation(default_metrics, default_metrics) == []

    valid_document = document("texcoord1-valid")
    missing_document = document("texcoord1-missing-second")
    valid_primitives = valid_document["meshes"][0]["primitives"]
    missing_primitives = missing_document["meshes"][0]["primitives"]
    # Both documents retain identical material/texture bindings and both
    # primitives retain TEXCOORD_0.  Only the second primitive's required
    # TEXCOORD_1 set is absent in the negative, defeating a material-wide
    # union of attribute semantics.
    assert valid_document["materials"] == missing_document["materials"]
    assert valid_document["materials"][0]["pbrMetallicRoughness"][
        "baseColorTexture"
    ] == {"index": 0, "texCoord": 1}
    assert [primitive["material"] for primitive in valid_primitives] == [0, 0]
    assert [primitive["material"] for primitive in missing_primitives] == [0, 0]
    assert sum(
        "TEXCOORD_0" in primitive["attributes"]
        for primitive in valid_primitives
    ) == sum(
        "TEXCOORD_0" in primitive["attributes"]
        for primitive in missing_primitives
    ) == 2
    assert sum(
        "TEXCOORD_1" in primitive["attributes"]
        for primitive in valid_primitives
    ) == 2
    assert sum(
        "TEXCOORD_1" in primitive["attributes"]
        for primitive in missing_primitives
    ) == 1

    valid_metrics = inspect("texcoord1-valid")
    assert valid_metrics["uv_primitives"] == 2
    assert valid_metrics["material_primitives"] == 2
    assert module.compare_preservation(valid_metrics, valid_metrics) == []
    try:
        missing_metrics = inspect("texcoord1-missing-second")
    except module.GlbError:
        return
    assert missing_metrics["uv_primitives"] == 2
    assert missing_metrics["material_primitives"] == 2
    reasons = module.compare_preservation(valid_metrics, missing_metrics)
    assert reasons, "second primitive omitted required TEXCOORD_1 and was accepted"


check(
    "TEXCOORD_1 is required on every primitive using its material",
    per_primitive_texcoord1_requirement,
)


def metallic_binding_regression(name, expected_bindings):
    source = inspect("multi-role-valid")
    output = inspect(name)
    assert output["materials"] == source["materials"] == 1
    assert output["images"] == source["images"] == 2
    assert output["embedded_images"] == source["embedded_images"] == 2
    assert output["image_payload_sha256"] == source["image_payload_sha256"]
    assert source["material_texture_bindings"] == MULTI_ROLE_BINDINGS
    assert output["material_texture_bindings"] == expected_bindings
    reasons = module.compare_preservation(source, output)
    assert reasons and any("texture binding" in reason.lower() for reason in reasons), reasons


check(
    "detached metallic-roughness role with intact counts/payloads",
    lambda: metallic_binding_regression(
        "multi-role-detach-metallic", BASE_COLOR_BINDING
    ),
)
check(
    "rebound metallic-roughness role with intact counts/payloads",
    lambda: metallic_binding_regression(
        "multi-role-rebind-metallic", REBOUND_MULTI_ROLE_BINDINGS
    ),
)


def detached_binding():
    source = inspect("valid")
    output = inspect("detach-base-color")
    assert output["materials"] == source["materials"] == 1
    assert output["images"] == source["images"] == 1
    assert output["embedded_images"] == source["embedded_images"] == 1
    assert output["image_payload_sha256"] == source["image_payload_sha256"]
    assert source["material_texture_bindings"] == BASE_COLOR_BINDING
    assert output["material_texture_bindings"] == []
    reasons = module.compare_preservation(source, output)
    assert reasons and any("texture binding" in reason.lower() for reason in reasons), reasons


check("detached base-color binding with intact counts", detached_binding)


def changed_texture_role():
    source = inspect("valid")
    output = inspect("move-base-color-to-normal")
    assert output["materials"] == source["materials"] == 1
    assert output["images"] == source["images"] == 1
    assert output["image_payload_sha256"] == source["image_payload_sha256"]
    assert source["material_texture_bindings"] == BASE_COLOR_BINDING
    assert output["material_texture_bindings"] == NORMAL_BINDING
    reasons = module.compare_preservation(source, output)
    assert reasons and any("texture binding" in reason.lower() for reason in reasons), reasons


check("texture role changed with intact counts/payload", changed_texture_role)


def changed_payload():
    source = inspect("valid")
    output = inspect("alter-image-payload")
    assert output["materials"] == source["materials"] == 1
    assert output["embedded_images"] == source["embedded_images"] == 1
    assert source["image_payload_sha256"] == [PNG_SHA256]
    assert output["image_payload_sha256"] != source["image_payload_sha256"]
    assert output["material_texture_bindings"] != BASE_COLOR_BINDING
    reasons = module.compare_preservation(source, output)
    assert reasons and any("image payload" in reason.lower() for reason in reasons), reasons


check("changed embedded payload with intact counts", changed_payload)


def cli_reject(name, *required_text):
    completed = subprocess.run(
        [sys.executable, str(script), str(fixture_root / f"{name}.glb")],
        check=False,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )
    assert completed.returncode != 0, "hostile fixture exited zero"
    assert completed.stdout == "", "hostile fixture wrote stdout"
    lines = completed.stderr.splitlines()
    has_traceback = "traceback" in completed.stderr.lower()
    assert len(lines) == 1 and lines[0].startswith("glb-metrics:"), (
        f"expected one prefixed diagnostic; lines={len(lines)}, "
        f"traceback={has_traceback}"
    )
    assert not has_traceback, "Python traceback escaped"
    for text in required_text:
        assert text.lower() in lines[0].lower(), lines[0]


def special_source_rejection():
    path = fixture_root / "special-source.fifo"
    try:
        completed = subprocess.run(
            [sys.executable, str(script), str(path)],
            check=False,
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            timeout=2.0,
        )
    except subprocess.TimeoutExpired as exc:
        raise AssertionError(
            "CLI opened/read the FIFO instead of rejecting it before open"
        ) from exc
    assert completed.returncode != 0, "special source exited zero"
    assert completed.stdout == "", "special source wrote stdout"
    lines = completed.stderr.splitlines()
    assert len(lines) == 1 and lines[0].startswith("glb-metrics:"), (
        f"expected one prefixed diagnostic, got {completed.stderr!r}"
    )
    assert "traceback" not in completed.stderr.lower(), completed.stderr
    folded = lines[0].lower()
    assert any(
        phrase in folded
        for phrase in (
            "regular file",
            "non-regular",
            "special file",
            "unsupported file type",
            "unsupported source",
            "not a file",
            "fifo",
        )
    ), f"diagnostic did not identify the special source: {lines[0]!r}"


check("FIFO source is rejected before opening", special_source_rejection)


def hostile_diagnostic_is_bounded_and_redacted():
    completed = subprocess.run(
        [
            sys.executable,
            str(script),
            str(fixture_root / "hostile-diagnostic-extension.glb"),
        ],
        check=False,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        timeout=2.0,
    )
    assert completed.returncode != 0, "hostile extension exited zero"
    assert completed.stdout == b"", "hostile extension wrote stdout"
    diagnostic = completed.stderr
    assert diagnostic.endswith(b"\n") and diagnostic.count(b"\n") == 1, (
        f"expected exactly one diagnostic line, got {diagnostic!r}"
    )
    assert len(diagnostic) <= 512, f"diagnostic is {len(diagnostic)} bytes"
    body = diagnostic[:-1]
    assert body.startswith(b"glb-metrics:"), body
    rendered = body.decode("utf-8")
    assert all(character.isprintable() for character in rendered), repr(rendered)
    folded = body.lower()
    for forbidden in (
        b"catmetro_test_credential_sentinel",
        b"authorization: bearer",
    ):
        assert forbidden not in folded, f"diagnostic echoed {forbidden!r}"
    assert b"traceback" not in folded, body


check(
    "hostile extension diagnostic is one bounded printable redacted line",
    hostile_diagnostic_is_bounded_and_redacted,
)


def implicit_texcoord0_is_required_during_inspection():
    control_document = document("two-primitives")
    negative_document = document("missing-one-uv")
    control_primitives = control_document["meshes"][0]["primitives"]
    negative_primitives = negative_document["meshes"][0]["primitives"]

    # baseColorTexture omits texCoord, so glTF's default set is TEXCOORD_0.
    # Both primitives use that material in the positive control.
    assert control_document["materials"][0]["pbrMetallicRoughness"][
        "baseColorTexture"
    ] == {"index": 0}
    assert [primitive["material"] for primitive in control_primitives] == [0, 0]
    assert all(
        "TEXCOORD_0" in primitive["attributes"]
        for primitive in control_primitives
    )
    control_metrics = inspect("two-primitives")
    assert control_metrics["uv_primitives"] == 2

    # The negative retains the same material, indices, POSITION, and first
    # primitive.  Restoring only the second TEXCOORD_0 reference makes its
    # JSON document exactly equal to the valid control.
    assert negative_document["materials"] == control_document["materials"]
    assert [primitive["material"] for primitive in negative_primitives] == [0, 0]
    assert "TEXCOORD_0" in negative_primitives[0]["attributes"]
    assert "TEXCOORD_0" not in negative_primitives[1]["attributes"]
    restored_document = json.loads(json.dumps(negative_document))
    restored_document["meshes"][0]["primitives"][1]["attributes"][
        "TEXCOORD_0"
    ] = control_primitives[1]["attributes"]["TEXCOORD_0"]
    assert restored_document == control_document
    assert binary_payload("missing-one-uv") == binary_payload("two-primitives")

    semantic_phrases = (
        "texcoord_0",
        "texcoord 0",
        "uv set 0",
        "texture coordinate set 0",
        "texture coordinate 0",
    )
    issues = []
    inspect_diagnostics = []
    for _ in range(2):
        try:
            inspect("missing-one-uv")
        except module.GlbError as exc:
            inspect_diagnostics.append(str(exc))
    if len(inspect_diagnostics) == 2:
        assert inspect_diagnostics[0] == inspect_diagnostics[1], (
            inspect_diagnostics
        )
        assert any(
            phrase in inspect_diagnostics[0].lower()
            for phrase in semantic_phrases
        ), inspect_diagnostics[0]
    else:
        issues.append("direct inspect accepted missing default TEXCOORD_0")

    cli_runs = [
        subprocess.run(
            [
                sys.executable,
                str(script),
                str(fixture_root / "missing-one-uv.glb"),
            ],
            check=False,
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            timeout=2.0,
        )
        for _ in range(2)
    ]
    for completed in cli_runs:
        if completed.returncode == 0:
            continue
        assert completed.stdout == "", completed.stdout
        lines = completed.stderr.splitlines()
        assert len(lines) == 1 and lines[0].startswith("glb-metrics:"), (
            f"expected one prefixed diagnostic, got {completed.stderr!r}"
        )
        assert "traceback" not in completed.stderr.lower(), completed.stderr
        assert any(
            phrase in lines[0].lower() for phrase in semantic_phrases
        ), lines[0]
    if all(completed.returncode != 0 for completed in cli_runs):
        assert cli_runs[0].stderr == cli_runs[1].stderr, (
            "CLI diagnostic changed between identical invocations"
        )
    else:
        issues.append("CLI accepted missing default TEXCOORD_0")
    assert not issues, "; ".join(issues)


check(
    "inspection rejects missing implicit TEXCOORD_0 on the second primitive",
    implicit_texcoord0_is_required_during_inspection,
)


for case, required in (
    ("missing-asset", ("asset",)),
    ("asset-version-1", ("version", "2.0")),
    ("duplicate-json-key", ("duplicate",)),
    ("hostile-json-integer", ()),
    ("oversized-json", ("json", "limit")),
    ("oversized-file", ("file", "limit")),
    ("too-many-accessors", ("accessor", "limit")),
    ("accessor-count-limit", ("accessor", "limit")),
    ("normalized-position", ("position", "normalized")),
    ("position-vec2", ("position", "float vec3")),
    ("position-unsigned-short", ("position", "float vec3")),
    ("uv-alias-position", ("texcoord_0",)),
    ("uv-ushort-unnormalized", ("texcoord_0", "normalized")),
    ("uv-signed-normalized", ("texcoord_0",)),
    ("base-color-texcoord-1", ("texcoord",)),
    ("sparse-uv-duplicate", ("sparse", "increasing")),
    ("sparse-uv-descending", ("sparse", "increasing")),
    ("sparse-uv-out-of-range", ("sparse", "range")),
    ("data-image-invalid-base64", ("image", "base64")),
    ("data-image-wrong-media", ("image",)),
    ("data-image-oversized", ("image", "limit")),
):
    check(f"CLI rejection: {case}", lambda case=case, required=required: cli_reject(case, *required))

if failures:
    for failure in failures:
        print(f"glb-metrics hardening RED: {failure}", file=sys.stderr)
    raise SystemExit(1)
PY
hardening_rc=$?
set -e
if [ "$hardening_rc" -ne 0 ]; then
  exit "$hardening_rc"
fi

echo "glb-metrics test: pass"
