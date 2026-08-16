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
assert d["vertices"] == 8
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
import json
import struct
import sys
from pathlib import Path


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


path = Path(sys.argv[1])
action = sys.argv[2]
if action == "magic":
    data = path.read_bytes()
    path.write_bytes(b"BAD!" + data[4:])
    raise SystemExit()
if action == "truncated":
    path.write_bytes(path.read_bytes()[:-3])
    raise SystemExit()
doc, binary = read(path)
if action == "deep-json":
    # The container and all required glTF fields stay valid; only unknown JSON
    # nesting is hostile to a parser without a depth guard.
    depth = 1_500
    base_json = json.dumps(doc, separators=(",", ":"), sort_keys=True).encode()
    write_raw_json(path, base_json[:-1] + b',"hostile":' + b"[" * depth + b"0" + b"]" * depth + b"}", binary)
    raise SystemExit()
primitive = doc["meshes"][0]["primitives"][0]
second_primitive = doc["meshes"][0]["primitives"][1] if len(doc["meshes"][0]["primitives"]) > 1 else None
if action == "mode":
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
elif action == "meshopt":
    doc["extensionsUsed"] = ["EXT_meshopt_compression"]
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

expect_metrics_failure() {
  local name=$1
  local input=$2
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
}

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
expect_preservation_rejection "missing UV binding" "$tmp/source.glb" "$tmp/missing-one-uv.glb"

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
