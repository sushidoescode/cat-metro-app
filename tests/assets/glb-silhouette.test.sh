#!/usr/bin/env bash
# Behavioral contract for the reproducible GLB silhouette renderer. Every
# assertion names the rendering break it catches and exercises real GLB bytes.
set -euo pipefail

silhouette_script=${SILHOUETTE_SCRIPT:-scripts/glb-silhouette.py}
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

write_fixture() {
  PYTHONDONTWRITEBYTECODE=1 python3 tests/assets/glb_fixture.py "$@"
}

run_metrics() {
  PYTHONDONTWRITEBYTECODE=1 python3 scripts/glb_metrics.py "$@"
}

run_silhouette() {
  PYTHONDONTWRITEBYTECODE=1 python3 "$silhouette_script" "$@"
}

# The two POSITION accessors partition X into opposite frame regions. Validate
# this setup independently so a malformed fixture cannot masquerade as RED.
write_fixture "$tmp/two-regions.glb" --triangles 2 --primitive-count 2
run_metrics "$tmp/two-regions.glb" >"$tmp/two-regions.json"
PYTHONDONTWRITEBYTECODE=1 python3 - "$tmp/two-regions.json" <<'PY'
import json
import sys

metrics = json.load(open(sys.argv[1], encoding="utf-8"))
# Break caught: fixture setup does not exercise two independent primitives.
assert metrics["meshes"] == 1
assert metrics["primitives"] == 2
assert metrics["vertices"] == 6
assert metrics["world_bounds"] == {
    "min": [-1.0, -1.0, -1.0],
    "max": [1.0, 1.0, 1.0],
}
PY

run_silhouette \
  "$tmp/two-regions.glb" "$tmp/two-regions.png" 0 \
  --size 128 --splat-radius 4 --min-coverage 0.01 \
  >"$tmp/two-regions.out"

PYTHONDONTWRITEBYTECODE=1 python3 - \
  "$tmp/two-regions.png" "$tmp/two-regions.out" \
  "$tmp/two-regions.glb" "$tmp/two-regions-count.err" <<'PY'
import re
import struct
import sys
import zlib
from pathlib import Path

png_path = Path(sys.argv[1])
stdout = Path(sys.argv[2]).read_text(encoding="utf-8")
source = Path(sys.argv[3])
count_error = Path(sys.argv[4])
data = png_path.read_bytes()

# Break caught: output is not a real PNG with the requested dimensions.
assert data.startswith(b"\x89PNG\r\n\x1a\n")
offset = 8
idat = bytearray()
width = height = None
while offset < len(data):
    length = struct.unpack_from(">I", data, offset)[0]
    kind = data[offset + 4:offset + 8]
    payload = data[offset + 8:offset + 8 + length]
    expected_crc = struct.unpack_from(">I", data, offset + 8 + length)[0]
    assert zlib.crc32(kind + payload) & 0xFFFFFFFF == expected_crc
    offset += 12 + length
    if kind == b"IHDR":
        width, height, depth, color, compression, filtering, interlace = struct.unpack(
            ">2I5B", payload
        )
        assert (depth, color, compression, filtering, interlace) == (8, 2, 0, 0, 0)
    elif kind == b"IDAT":
        idat.extend(payload)
    elif kind == b"IEND":
        break
assert (width, height) == (128, 128)

raw = zlib.decompress(bytes(idat))
row_size = width * 3
rows = []
cursor = 0
previous = bytearray(row_size)
for _ in range(height):
    filter_type = raw[cursor]
    encoded = raw[cursor + 1:cursor + 1 + row_size]
    cursor += row_size + 1
    row = bytearray(row_size)
    for index, value in enumerate(encoded):
        left = row[index - 3] if index >= 3 else 0
        above = previous[index]
        upper_left = previous[index - 3] if index >= 3 else 0
        if filter_type == 0:
            decoded = value
        elif filter_type == 1:
            decoded = value + left
        elif filter_type == 2:
            decoded = value + above
        elif filter_type == 3:
            decoded = value + ((left + above) // 2)
        elif filter_type == 4:
            p = left + above - upper_left
            distances = (abs(p - left), abs(p - above), abs(p - upper_left))
            predictor = (left, above, upper_left)[distances.index(min(distances))]
            decoded = value + predictor
        else:
            raise AssertionError(f"unsupported PNG filter {filter_type}")
        row[index] = decoded & 0xFF
    rows.append(bytes(row))
    previous = row
assert cursor == len(raw)

background = (250, 246, 236)
occupied = []
for y, row in enumerate(rows):
    for x in range(width):
        if tuple(row[x * 3:x * 3 + 3]) != background:
            occupied.append((x, y))

# Break caught: traversal or rasterization silently drops one primitive region.
assert any(x < width // 3 for x, _ in occupied)
assert any(x >= (2 * width) // 3 for x, _ in occupied)

match = re.fullmatch(
    rf"{re.escape(str(source))} -> {re.escape(str(png_path))} "
    r"\((\d+) vertices, (\d+) filled pixels, ([0-9.]+) coverage\)\n",
    stdout,
)
assert match, stdout
vertices, reported_filled, reported_coverage = match.groups()
# Break caught: the renderer reports raw, unreferenced accessor entries rather
# than the six triangle-corner surface positions across both primitives.  Keep
# this mismatch as an aggregate failure so the later independent hardening
# cases still execute against the RED implementation.
if int(vertices) != 6:
    count_error.write_text(
        f"two-region surface count: expected 6, got {vertices}",
        encoding="utf-8",
    )
assert int(reported_filled) == len(occupied)
coverage = len(occupied) / (width * height)
assert coverage >= 0.01
assert abs(float(reported_coverage) - coverage) <= 0.0000005
PY

write_fixture "$tmp/sparse.glb" --triangles 1
if run_silhouette \
  "$tmp/sparse.glb" "$tmp/sparse.png" 0 \
  --size 512 --splat-radius 1 --min-coverage 0.10 \
  >"$tmp/sparse.out" 2>"$tmp/sparse.err"; then
  echo "glb-silhouette test: sparse evidence unexpectedly passed coverage gate" >&2
  exit 1
fi
# Break caught: sparse point evidence is accepted or leaves an apparently valid render.
test ! -e "$tmp/sparse.png"
grep -nE 'glb-silhouette: coverage [0-9.]+ below 0\.100000' "$tmp/sparse.err"

alias_failures=0
check_source_alias_rejected() {
  local case_name=$1
  local source="$tmp/$case_name-source.glb"
  local output="$tmp/$case_name-output.png"
  local stdout="$tmp/$case_name.out"
  local stderr="$tmp/$case_name.err"
  local before after render_rc

  write_fixture "$source" --triangles 2 --primitive-count 2
  case "$case_name" in
    direct)
      output=$source
      ;;
    symlink)
      ln -s "$(basename "$source")" "$output"
      ;;
    hardlink)
      ln "$source" "$output"
      ;;
    *)
      echo "glb-silhouette test: unknown alias case $case_name" >&2
      exit 1
      ;;
  esac
  before=$(shasum -a 256 "$source" | awk '{print $1}')

  set +e
  run_silhouette \
    "$source" "$output" 0 \
    --size 128 --splat-radius 4 --min-coverage 0.01 \
    >"$stdout" 2>"$stderr"
  render_rc=$?
  set -e
  after=$(shasum -a 256 "$source" | awk '{print $1}')

  # Break caught: direct, symbolic, or hard-link output aliases overwrite the
  # immutable source instead of failing before rendering.
  if [ "$render_rc" -eq 0 ]; then
    echo "glb-silhouette test: $case_name source alias unexpectedly passed" >&2
    alias_failures=$((alias_failures + 1))
  fi
  if ! grep -qE '^glb-silhouette:' "$stderr"; then
    echo "glb-silhouette test: $case_name source alias lacked diagnostic" >&2
    alias_failures=$((alias_failures + 1))
  fi
  if [ "$before" != "$after" ]; then
    echo "glb-silhouette test: $case_name source alias changed GLB hash" >&2
    alias_failures=$((alias_failures + 1))
  fi
  if ! PYTHONDONTWRITEBYTECODE=1 python3 - "$source" <<'PY'
import sys
from pathlib import Path

assert Path(sys.argv[1]).read_bytes()[:4] == b"glTF"
PY
  then
    echo "glb-silhouette test: $case_name source alias changed GLB magic" >&2
    alias_failures=$((alias_failures + 1))
  fi
}

check_source_alias_rejected direct
check_source_alias_rejected symlink
check_source_alias_rejected hardlink
if [ "$alias_failures" -ne 0 ]; then
  echo "glb-silhouette test: $alias_failures source-alias protections failed" >&2
  exit 1
fi

# Output-custody rule frozen here: an existing ordinary, single-link PNG may
# be rerendered, but an output symlink or multiply-linked regular file is
# rejected before rendering.  Successful output publication is one atomic
# pathname replacement; any write or replacement failure preserves the prior
# ordinary output (if any) and leaves no staging residue.
#
# Evidence-validity rule frozen here: a silhouette consumes only positions
# referenced by non-degenerate surface topology.  Raw, unreferenced POSITION
# values are neither rendered nor reported, and a model with no valid surface
# cannot produce evidence.
write_fixture \
  "$tmp/hardening-source.glb" --triangles 2 \
  --omit-uv --omit-material --omit-image
write_fixture \
  "$tmp/hardening-target.glb" --triangles 3 \
  --omit-uv --omit-material --omit-image
write_fixture \
  "$tmp/shared-topology-base.glb" --triangles 1000 \
  --omit-uv --omit-material --omit-image

PYTHONDONTWRITEBYTECODE=1 python3 - \
  "$silhouette_script" "$tmp" <<'PY'
from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
import os
import re
import resource
import shutil
import signal
import struct
import subprocess
import sys
import time
import zlib
from pathlib import Path


script = Path(sys.argv[1]).resolve()
root = Path(sys.argv[2]).resolve()
source_template = root / "hardening-source.glb"
target_template = root / "hardening-target.glb"
shared_topology_template = root / "shared-topology-base.glb"
python = sys.executable
environment = {**os.environ, "PYTHONDONTWRITEBYTECODE": "1"}
failures: list[str] = []
maximum_source_bytes = 512 * 1024 * 1024
maximum_selected_scene_work = 8_000_000
oversized_diagnostic = (
    f"glb-silhouette: source GLB exceeds {maximum_source_bytes}-byte limit"
)

count_error = root / "two-regions-count.err"
if count_error.exists():
    failures.append(count_error.read_text(encoding="utf-8"))


def check(condition: bool, message: str) -> None:
    if not condition:
        failures.append(message)


def invoke(
    source: Path,
    output: Path,
    *arguments: str,
    timeout: float = 2.0,
    file_size_limit: int | None = None,
) -> subprocess.CompletedProcess[str] | None:
    def configure_child() -> None:
        if file_size_limit is None:
            return
        resource.setrlimit(
            resource.RLIMIT_FSIZE,
            (file_size_limit, file_size_limit),
        )
        signal.signal(signal.SIGXFSZ, signal.SIG_IGN)

    try:
        return subprocess.run(
            [python, str(script), str(source), str(output), *arguments],
            check=False,
            capture_output=True,
            text=True,
            env=environment,
            timeout=timeout,
            preexec_fn=configure_child if file_size_limit is not None else None,
        )
    except subprocess.TimeoutExpired:
        return None


def open_targets_path(
    path: object,
    keywords: dict[str, object],
    expected: Path,
) -> bool:
    """Recognize absolute or parent-dirfd opens of one controlled source."""
    try:
        raw_path = os.fsdecode(os.fspath(path))
    except TypeError:
        return False
    directory_descriptor = keywords.get("dir_fd")
    if directory_descriptor is None or os.path.isabs(raw_path):
        return os.path.abspath(raw_path) == str(expected)
    if (
        isinstance(directory_descriptor, bool)
        or not isinstance(directory_descriptor, int)
        or raw_path != expected.name
    ):
        return False
    try:
        opened_parent = os.fstat(directory_descriptor)
        expected_parent = expected.parent.stat()
    except OSError:
        return False
    return (
        opened_parent.st_dev == expected_parent.st_dev
        and opened_parent.st_ino == expected_parent.st_ino
    )


def invoke_with_rss_limit(
    program: Path,
    source: Path,
    output: Path,
    *arguments: str,
    timeout: float = 2.0,
    maximum_rss: int = 64 * 1024 * 1024,
) -> tuple[subprocess.CompletedProcess[str], bool, bool, int]:
    command = [python, str(program), str(source), str(output), *arguments]

    def lower_priority() -> None:
        try:
            os.nice(10)
        except OSError:
            pass

    process = subprocess.Popen(
        command,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        env=environment,
        preexec_fn=lower_priority,
    )
    ps = shutil.which("ps")
    assert ps is not None
    deadline = time.monotonic() + timeout
    peak_rss = 0
    exceeded_memory = False
    exceeded_time = False
    try:
        while process.poll() is None:
            measurement = subprocess.run(
                [ps, "-o", "rss=", "-p", str(process.pid)],
                check=False,
                capture_output=True,
                text=True,
            )
            value = measurement.stdout.strip()
            if value.isdigit():
                peak_rss = max(peak_rss, int(value) * 1024)
            if peak_rss > maximum_rss:
                exceeded_memory = True
                process.kill()
                break
            if time.monotonic() >= deadline:
                exceeded_time = True
                process.kill()
                break
            time.sleep(0.005)
        stdout, stderr = process.communicate(timeout=1.0)
    finally:
        if process.poll() is None:
            process.kill()
            process.wait()
    return (
        subprocess.CompletedProcess(command, process.returncode, stdout, stderr),
        exceeded_memory,
        exceeded_time,
        peak_rss,
    )


def check_prefixed_failure(
    result: subprocess.CompletedProcess[str] | None,
    label: str,
) -> None:
    if result is None:
        failures.append(f"{label}: renderer exceeded bounded runtime")
        return
    lines = result.stderr.splitlines()
    check(result.returncode != 0, f"{label}: unexpectedly returned success")
    check(result.stdout == "", f"{label}: failure wrote stdout")
    check(
        len(lines) == 1 and lines[0].startswith("glb-silhouette:"),
        f"{label}: expected one prefixed diagnostic, got {result.stderr!r}",
    )
    check("Traceback" not in result.stderr, f"{label}: leaked a traceback")


def check_oversized_failure(
    result: subprocess.CompletedProcess[str],
    label: str,
) -> None:
    check(result.returncode == 1, f"{label}: expected exit 1")
    check(result.stdout == "", f"{label}: failure wrote stdout")
    check(
        result.stderr == oversized_diagnostic + "\n",
        f"{label}: expected {oversized_diagnostic!r}, got {result.stderr!r}",
    )


def parse_glb(path: Path) -> tuple[dict[str, object], bytearray]:
    payload = path.read_bytes()
    magic, version, declared = struct.unpack_from("<4sII", payload, 0)
    assert (magic, version, declared) == (b"glTF", 2, len(payload))
    json_length, json_kind = struct.unpack_from("<I4s", payload, 12)
    assert json_kind == b"JSON"
    json_start = 20
    json_end = json_start + json_length
    document = json.loads(payload[json_start:json_end].rstrip(b" "))
    binary_length, binary_kind = struct.unpack_from("<I4s", payload, json_end)
    assert binary_kind == b"BIN\0"
    binary_start = json_end + 8
    binary_end = binary_start + binary_length
    assert binary_end == len(payload)
    return document, bytearray(payload[binary_start:binary_end])


def write_glb(path: Path, document: dict[str, object], binary: bytearray) -> None:
    buffers = document["buffers"]
    assert isinstance(buffers, list) and len(buffers) == 1
    buffer = buffers[0]
    assert isinstance(buffer, dict)
    buffer["byteLength"] = len(binary)
    json_payload = json.dumps(
        document,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")
    json_payload += b" " * (-len(json_payload) % 4)
    binary_payload = bytes(binary) + b"\0" * (-len(binary) % 4)
    total = 12 + 8 + len(json_payload) + 8 + len(binary_payload)
    path.write_bytes(
        struct.pack("<4sII", b"glTF", 2, total)
        + struct.pack("<I4s", len(json_payload), b"JSON")
        + json_payload
        + struct.pack("<I4s", len(binary_payload), b"BIN\0")
        + binary_payload
    )


def write_unknown_integer_glb(
    path: Path,
    source: Path,
    digits: int,
) -> None:
    payload = source.read_bytes()
    json_length, json_kind = struct.unpack_from("<I4s", payload, 12)
    assert json_kind == b"JSON"
    encoded = payload[20:20 + json_length].rstrip(b" ")
    assert encoded.endswith(b"}")
    changed = encoded[:-1] + b',"numericBoundary":' + b"7" * digits + b"}"
    changed += b" " * (-len(changed) % 4)
    suffix = payload[20 + json_length:]
    total = 12 + 8 + len(changed) + len(suffix)
    path.write_bytes(
        struct.pack("<4sII", b"glTF", 2, total)
        + struct.pack("<I4s", len(changed), b"JSON")
        + changed
        + suffix
    )


def primitive(document: dict[str, object]) -> dict[str, object]:
    meshes = document["meshes"]
    assert isinstance(meshes, list) and len(meshes) == 1
    mesh = meshes[0]
    assert isinstance(mesh, dict)
    primitives = mesh["primitives"]
    assert isinstance(primitives, list) and len(primitives) == 1
    value = primitives[0]
    assert isinstance(value, dict)
    return value


def accessor_and_view(
    document: dict[str, object],
    accessor_index: int,
) -> tuple[dict[str, object], dict[str, object]]:
    accessors = document["accessors"]
    views = document["bufferViews"]
    assert isinstance(accessors, list) and isinstance(views, list)
    accessor = accessors[accessor_index]
    assert isinstance(accessor, dict)
    view = views[accessor["bufferView"]]
    assert isinstance(view, dict)
    return accessor, view


def surface_shape(path: Path) -> tuple[int | None, tuple[int, ...] | None]:
    """Return the fixture's POSITION count and unsigned-short indices safely."""
    try:
        document, binary = parse_glb(path)
        item = primitive(document)
        attributes = item["attributes"]
        if not isinstance(attributes, dict):
            return None, None
        position_number = attributes["POSITION"]
        index_number = item["indices"]
        if not isinstance(position_number, int) or not isinstance(index_number, int):
            return None, None
        position_accessor, _ = accessor_and_view(document, position_number)
        index_accessor, index_view = accessor_and_view(document, index_number)
        position_count = position_accessor.get("count")
        index_count = index_accessor.get("count")
        if (
            not isinstance(position_count, int)
            or position_count < 0
            or index_accessor.get("componentType") != 5123
            or index_accessor.get("type") != "SCALAR"
            or not isinstance(index_count, int)
            or index_count < 0
        ):
            return None, None
        start = int(index_view.get("byteOffset", 0)) + int(
            index_accessor.get("byteOffset", 0)
        )
        end = start + index_count * 2
        if start < 0 or end > len(binary):
            return None, None
        indices = struct.unpack_from(f"<{index_count}H", binary, start)
        return position_count, indices
    except (
        AssertionError,
        IndexError,
        KeyError,
        OSError,
        TypeError,
        ValueError,
        struct.error,
    ):
        return None, None


def append_view(
    document: dict[str, object],
    binary: bytearray,
    payload: bytes,
    target: int,
) -> int:
    while len(binary) % 4:
        binary.append(0)
    views = document["bufferViews"]
    assert isinstance(views, list)
    views.append(
        {
            "buffer": 0,
            "byteOffset": len(binary),
            "byteLength": len(payload),
            "target": target,
        }
    )
    binary.extend(payload)
    return len(views) - 1


def append_accessor(
    document: dict[str, object],
    value: dict[str, object],
) -> int:
    accessors = document["accessors"]
    assert isinstance(accessors, list)
    accessors.append(value)
    return len(accessors) - 1


def build_zeroed_indices(source: Path, output: Path) -> None:
    document, binary = parse_glb(source)
    item = primitive(document)
    index_number = item["indices"]
    assert isinstance(index_number, int)
    accessor, view = accessor_and_view(document, index_number)
    assert accessor["componentType"] == 5123
    count = accessor["count"]
    assert isinstance(count, int) and count >= 3 and count % 3 == 0
    start = int(view.get("byteOffset", 0)) + int(accessor.get("byteOffset", 0))
    end = start + count * 2
    before = bytes(binary[start:end])
    assert any(before)
    binary[start:end] = b"\0" * (count * 2)
    write_glb(output, document, binary)


def build_orphan_positions(source: Path, output: Path) -> None:
    document, binary = parse_glb(source)
    item = primitive(document)
    attributes = item["attributes"]
    assert isinstance(attributes, dict)
    position_number = attributes["POSITION"]
    assert isinstance(position_number, int)
    accessor, view = accessor_and_view(document, position_number)
    assert accessor["componentType"] == 5126
    assert accessor["type"] == "VEC3"
    count = accessor["count"]
    assert isinstance(count, int) and count >= 3
    start = int(view.get("byteOffset", 0)) + int(accessor.get("byteOffset", 0))
    original = bytes(binary[start:start + count * 12])
    decorative = []
    for number in range(10_000):
        decorative.extend(
            (
                -1.0 + 2.0 * (number % 100) / 99.0,
                -1.0 + 2.0 * ((number // 100) % 100) / 99.0,
                -1.0 + 2.0 * ((number * 37) % 101) / 100.0,
            )
        )
    payload = original + struct.pack(f"<{len(decorative)}f", *decorative)
    view_number = append_view(document, binary, payload, 34962)
    new_accessor = {
        "bufferView": view_number,
        "componentType": 5126,
        "count": count + 10_000,
        "type": "VEC3",
        "min": accessor["min"],
        "max": accessor["max"],
    }
    attributes["POSITION"] = append_accessor(document, new_accessor)
    write_glb(output, document, binary)


def build_high_work_surface(source: Path, output: Path) -> None:
    document, binary = parse_glb(source)
    item = primitive(document)
    attributes = item["attributes"]
    assert isinstance(attributes, dict)
    position_number = attributes["POSITION"]
    assert isinstance(position_number, int)
    position_accessor, _ = accessor_and_view(document, position_number)
    position_count = position_accessor["count"]
    assert isinstance(position_count, int) and position_count >= 9_999
    indices = tuple(range(9_999))
    payload = struct.pack(f"<{len(indices)}H", *indices)
    view_number = append_view(document, binary, payload, 34963)
    item["indices"] = append_accessor(
        document,
        {
            "bufferView": view_number,
            "componentType": 5123,
            "count": len(indices),
            "type": "SCALAR",
        },
    )
    write_glb(output, document, binary)


def build_shared_primitive_scene(
    source: Path,
    output: Path,
    primitive_count: int,
    *,
    expected_index_count: int,
    expected_position_count: int,
) -> None:
    """Repeat one real primitive without repeating its binary accessors."""
    document, binary = parse_glb(source)
    meshes = document["meshes"]
    assert isinstance(meshes, list) and len(meshes) == 1
    mesh = meshes[0]
    assert isinstance(mesh, dict)
    primitives = mesh["primitives"]
    assert isinstance(primitives, list) and len(primitives) == 1
    item = primitives[0]
    assert isinstance(item, dict)
    index_number = item["indices"]
    assert isinstance(index_number, int)
    index_accessor, _ = accessor_and_view(document, index_number)
    attributes = item["attributes"]
    assert isinstance(attributes, dict)
    position_number = attributes["POSITION"]
    assert isinstance(position_number, int)
    position_accessor, _ = accessor_and_view(document, position_number)
    assert index_accessor["count"] == expected_index_count
    assert position_accessor["count"] == expected_position_count
    # JSON serialization materializes the primitive records, but every record
    # intentionally shares the same compact POSITION and index accessors.
    mesh["primitives"] = [item] * primitive_count
    write_glb(output, document, binary)


def build_shared_node_scene(
    source: Path,
    output: Path,
    node_count: int,
) -> None:
    """Instance one real mesh primitive from many selected scene nodes."""
    document, binary = parse_glb(source)
    meshes = document["meshes"]
    assert isinstance(meshes, list) and len(meshes) == 1
    mesh = meshes[0]
    assert isinstance(mesh, dict)
    primitives = mesh["primitives"]
    assert isinstance(primitives, list) and len(primitives) == 1
    nodes = [{"mesh": 0} for _ in range(node_count)]
    document["nodes"] = nodes
    document["scenes"] = [{"nodes": list(range(node_count))}]
    document["scene"] = 0
    write_glb(output, document, binary)


def shared_scene_work(path: Path) -> tuple[int, int, int, int, int, int]:
    """Independently measure the single-node shared-accessor test shape."""
    document, _ = parse_glb(path)
    meshes = document["meshes"]
    nodes = document["nodes"]
    scenes = document["scenes"]
    assert isinstance(meshes, list) and len(meshes) == 1
    assert isinstance(nodes, list) and len(nodes) == 1
    assert isinstance(scenes, list) and len(scenes) == 1
    node = nodes[0]
    scene = scenes[0]
    assert isinstance(node, dict) and node.get("mesh") == 0
    assert isinstance(scene, dict) and scene.get("nodes") == [0]
    mesh = meshes[0]
    assert isinstance(mesh, dict)
    primitives = mesh["primitives"]
    assert isinstance(primitives, list)
    selected_index_references = 0
    selected_position_values = 0
    index_accessors: set[int] = set()
    position_accessors: set[int] = set()
    for item in primitives:
        assert isinstance(item, dict) and item.get("mode") == 4
        attributes = item.get("attributes")
        assert isinstance(attributes, dict)
        position_number = attributes.get("POSITION")
        assert isinstance(position_number, int)
        position_accessors.add(position_number)
        position_accessor, _ = accessor_and_view(document, position_number)
        position_count = position_accessor.get("count")
        assert isinstance(position_count, int)
        selected_position_values += position_count
        index_number = item.get("indices")
        assert isinstance(index_number, int)
        index_accessors.add(index_number)
        accessor, _ = accessor_and_view(document, index_number)
        count = accessor.get("count")
        assert isinstance(count, int)
        selected_index_references += count
    return (
        len(primitives),
        len(index_accessors),
        len(position_accessors),
        selected_index_references,
        selected_position_values,
        selected_index_references + selected_position_values,
    )


def instanced_scene_work(
    path: Path,
) -> tuple[int, int, int, int, int, int, int, int]:
    """Measure mesh-definition and selected-instance work independently."""
    document, _ = parse_glb(path)
    meshes = document["meshes"]
    nodes = document["nodes"]
    scenes = document["scenes"]
    assert isinstance(meshes, list) and len(meshes) == 1
    assert isinstance(nodes, list)
    assert isinstance(scenes, list) and len(scenes) == 1
    mesh = meshes[0]
    scene = scenes[0]
    assert isinstance(mesh, dict) and isinstance(scene, dict)
    primitives = mesh["primitives"]
    roots = scene["nodes"]
    assert isinstance(primitives, list) and len(primitives) == 1
    assert isinstance(roots, list) and roots == list(range(len(nodes)))
    item = primitives[0]
    assert isinstance(item, dict) and item.get("mode") == 4
    attributes = item.get("attributes")
    assert isinstance(attributes, dict)
    position_number = attributes.get("POSITION")
    assert isinstance(position_number, int)
    position_accessor, _ = accessor_and_view(document, position_number)
    position_count = position_accessor.get("count")
    assert isinstance(position_count, int)
    index_number = item.get("indices")
    assert isinstance(index_number, int)
    index_accessor, _ = accessor_and_view(document, index_number)
    index_count = index_accessor.get("count")
    assert isinstance(index_count, int)
    for node in nodes:
        assert isinstance(node, dict) and node.get("mesh") == 0
    mesh_definition_references = index_count * len(primitives)
    mesh_definition_positions = position_count * len(primitives)
    mesh_definition_work = (
        mesh_definition_references + mesh_definition_positions
    )
    selected_references = mesh_definition_references * len(nodes)
    selected_positions = mesh_definition_positions * len(nodes)
    selected_work = selected_references + selected_positions
    return (
        len(nodes),
        len(primitives),
        mesh_definition_references,
        mesh_definition_positions,
        mesh_definition_work,
        selected_references,
        selected_positions,
        selected_work,
    )


def selected_work_document(
    *,
    position_count: int,
    index_count: int | None,
    node_count: int = 1,
) -> dict[str, object]:
    """Build the smallest selected-scene document needed by the cheap guard."""
    accessors: list[object] = [{"count": position_count}]
    primitive: dict[str, object] = {
        "attributes": {"POSITION": 0},
        "mode": 4,
    }
    if index_count is not None:
        accessors.append({"count": index_count})
        primitive["indices"] = 1
    return {
        "accessors": accessors,
        "meshes": [{"primitives": [primitive]}],
        "nodes": [{"mesh": 0} for _ in range(node_count)],
        "scenes": [{"nodes": list(range(node_count))}],
        "scene": 0,
    }


def selected_work_error(document: dict[str, object]) -> BaseException | None:
    """Return the guard's rejection without invoking the expensive inspector."""
    try:
        module._validate_selected_scene_work(document)
    except BaseException as exc:
        return exc
    return None


def reported_vertices(stdout: str) -> int | None:
    match = re.search(r"\((\d+) vertices,", stdout)
    return None if match is None else int(match.group(1))


def is_complete_png(path: Path, expected_size: int) -> bool:
    try:
        data = path.read_bytes()
        if not data.startswith(b"\x89PNG\r\n\x1a\n"):
            return False
        offset = 8
        dimensions: tuple[int, int] | None = None
        compressed = bytearray()
        saw_end = False
        while offset < len(data):
            if len(data) - offset < 12:
                return False
            length = struct.unpack_from(">I", data, offset)[0]
            end = offset + 12 + length
            if end > len(data):
                return False
            kind = data[offset + 4:offset + 8]
            payload = data[offset + 8:offset + 8 + length]
            checksum = struct.unpack_from(">I", data, offset + 8 + length)[0]
            if zlib.crc32(kind + payload) & 0xFFFFFFFF != checksum:
                return False
            if kind == b"IHDR":
                if len(payload) != 13:
                    return False
                width, height = struct.unpack_from(">2I", payload)
                dimensions = (width, height)
            elif kind == b"IDAT":
                compressed.extend(payload)
            elif kind == b"IEND":
                if payload:
                    return False
                saw_end = True
                offset = end
                break
            offset = end
        if (
            not saw_end
            or offset != len(data)
            or dimensions != (expected_size, expected_size)
            or not compressed
        ):
            return False
        raw = zlib.decompress(bytes(compressed))
        return len(raw) == expected_size * (1 + expected_size * 3)
    except (OSError, struct.error, zlib.error):
        return False


# Control: a normal single-link PNG can be atomically rerendered in place.
ordinary_output = root / "ordinary-rerender.png"
ordinary_first = invoke(
    source_template,
    ordinary_output,
    "0",
    "--size", "64",
    "--splat-radius", "4",
    "--min-coverage", "0.001",
)
check(
    ordinary_first is not None and ordinary_first.returncode == 0,
    "ordinary output: initial render failed",
)
ordinary_second = invoke(
    source_template,
    ordinary_output,
    "25",
    "--size", "64",
    "--splat-radius", "4",
    "--min-coverage", "0.001",
)
check(
    ordinary_second is not None and ordinary_second.returncode == 0,
    "ordinary output: rerender was refused",
)
check(
    is_complete_png(ordinary_output, 64),
    "ordinary output: rerender did not leave a PNG",
)
if ordinary_first is not None:
    ordinary_record = re.fullmatch(
        rf"{re.escape(str(source_template))} -> "
        rf"{re.escape(str(ordinary_output))} "
        r"\(\d+ vertices, \d+ filled pixels, [0-9.]+ coverage\)\n",
        ordinary_first.stdout,
    )
    check(
        ordinary_record is not None and ordinary_first.stderr == "",
        "ordinary output: short-path success record changed",
    )


# A success record is public even when both rendered files remain private test
# fixtures. Risk-shaped or nonprintable path values are represented without
# changing the ordinary short-path record above.
record_case = root / "success-record-boundary"
record_case.mkdir()
record_marker = "NEUTRAL_PRIVATE_SENTINEL"
record_source = record_case / f"source\ncredential-{record_marker}.glb"
record_output = record_case / f"output\n{record_marker}.png"
shutil.copyfile(source_template, record_source)
record_result = invoke(
    record_source,
    record_output,
    "0",
    "--size", "64",
    "--splat-radius", "4",
    "--min-coverage", "0.001",
)
check(
    record_result is not None and record_result.returncode == 0,
    "success record boundary: render failed",
)
if record_result is not None:
    record_lines = record_result.stdout.splitlines()
    check(record_result.stderr == "", "success record boundary wrote stderr")
    check(
        record_result.stdout.endswith("\n")
        and len(record_lines) == 1
        and all(character.isprintable() for character in record_lines[0])
        and len(record_result.stdout.encode("utf-8")) <= 512,
        "success record boundary is not one bounded printable line",
    )
    check(
        record_marker not in record_result.stdout
        and str(record_source) not in record_result.stdout
        and str(record_output) not in record_result.stdout,
        "success record boundary echoed a private path value",
    )
    check(
        re.fullmatch(
            r"\[redacted\] -> \[redacted\] "
            r"\(\d+ vertices, \d+ filled pixels, [0-9.]+ coverage\)\n",
            record_result.stdout,
        )
        is not None,
        "success record boundary lost its stable redacted shape",
    )
check(
    is_complete_png(record_output, 64),
    "success record boundary did not leave the requested PNG",
)


# Mutations caught: following either kind of link and writing the final path
# directly.  A renderer may reject the linked output or atomically replace its
# directory entry with a complete PNG; it must never write through to the
# distinct immutable GLB.
for link_kind in ("symlink", "hardlink"):
    case = root / f"distinct-{link_kind}"
    case.mkdir()
    source = case / "a.glb"
    target = case / "b.glb"
    output = case / "out.png"
    shutil.copyfile(source_template, source)
    shutil.copyfile(target_template, target)
    source_before = source.read_bytes()
    target_before = target.read_bytes()
    check(source_before != target_before, f"{link_kind}: setup GLBs are not distinct")
    if link_kind == "symlink":
        output.symlink_to(target.name)
    else:
        os.link(target, output)
    result = invoke(
        source,
        output,
        "0",
        "--size", "64",
        "--splat-radius", "4",
        "--min-coverage", "0.001",
    )
    if result is not None and result.returncode == 0:
        check(result.stderr == "", f"{link_kind}: successful render wrote stderr")
        check(
            is_complete_png(output, 64),
            f"{link_kind}: successful replacement is not a complete PNG",
        )
        check(
            not output.is_symlink() and not os.path.samefile(output, target),
            f"{link_kind}: successful replacement still aliases its target",
        )
    else:
        check_prefixed_failure(result, f"distinct {link_kind} output")
        check(
            output.exists() and output.read_bytes() == target_before,
            f"{link_kind}: rejected output was partially changed",
        )
    check(source.read_bytes() == source_before, f"{link_kind}: source GLB changed")
    check(target.read_bytes() == target_before, f"{link_kind}: target GLB changed")
    check(
        {path.name for path in case.iterdir()} == {"a.glb", "b.glb", "out.png"},
        f"{link_kind}: output transaction left staging residue",
    )


# A real short-write fault must affect only a staging file.  Direct final
# Path.write_bytes leaves the 64-byte residue this test rejects.
write_fault = root / "write-fault"
write_fault.mkdir()
write_fault_source = write_fault / "source.glb"
shutil.copyfile(source_template, write_fault_source)
write_fault_output = write_fault / "output.png"
write_result = invoke(
    write_fault_source,
    write_fault_output,
    "25",
    "--size", "64",
    "--splat-radius", "4",
    "--min-coverage", "0.001",
    file_size_limit=64,
)
check_prefixed_failure(write_result, "injected PNG short write")
check(not write_fault_output.exists(), "short write left a partial final PNG")
check(
    {path.name for path in write_fault.iterdir()} == {"source.glb"},
    "short write left staging residue",
)


# Inject failure at the atomic publication boundary.  A prior ordinary PNG is
# immutable until replacement succeeds, and failed staging is cleaned.
replace_fault = root / "replace-fault"
replace_fault.mkdir()
replace_source = replace_fault / "source.glb"
replace_output = replace_fault / "output.png"
shutil.copyfile(source_template, replace_source)
prior_png = ordinary_output.read_bytes()
replace_output.write_bytes(prior_png)
replace_calls = 0
replace_error: BaseException | None = None
original_replace = os.replace
original_rename = os.rename


def injected_replace(
    _source: object,
    _target: object,
    *_arguments: object,
    **_keywords: object,
) -> None:
    global replace_calls
    replace_calls += 1
    raise OSError("injected silhouette replace failure")


module_name = "catmetro_glb_silhouette_atomic_probe"
spec = importlib.util.spec_from_file_location(module_name, script)
assert spec is not None and spec.loader is not None
module = importlib.util.module_from_spec(spec)
scripts_directory = str(script.parent)
sys.path.insert(0, scripts_directory)
try:
    os.replace = injected_replace
    os.rename = injected_replace
    spec.loader.exec_module(module)
    try:
        module.render(
            replace_source,
            replace_output,
            25.0,
            size=64,
            splat_radius=4,
            minimum_coverage=0.001,
        )
    except BaseException as exc:  # The injected OSError must surface safely.
        replace_error = exc
finally:
    os.replace = original_replace
    os.rename = original_rename
    sys.path.remove(scripts_directory)
    sys.modules.pop(module_name, None)

check(replace_calls >= 1, "replace fault: atomic publication was never reached")
check(replace_error is not None, "replace fault: renderer reported success")
check(replace_output.read_bytes() == prior_png, "replace fault changed prior PNG")
check(
    {path.name for path in replace_fault.iterdir()} == {"source.glb", "output.png"},
    "replace fault left staging residue",
)


# JSON integer-token behavior must not depend on the interpreter's optional
# global digit ceiling. The exact boundary remains accepted; +1 fails in the
# captured-byte loader before strict inspection or rasterization.
numeric_case = root / "numeric-token-boundary"
numeric_case.mkdir()
numeric_exact = numeric_case / "exact.glb"
numeric_oversized = numeric_case / "plus-one.glb"
write_unknown_integer_glb(numeric_exact, source_template, 4_300)
write_unknown_integer_glb(numeric_oversized, source_template, 4_301)
integer_setter = getattr(sys, "set_int_max_str_digits", None)
integer_getter = getattr(sys, "get_int_max_str_digits", None)
previous_integer_limit = integer_getter() if integer_getter is not None else None
if integer_setter is not None:
    integer_setter(0)
try:
    try:
        exact_numeric_document = module._source_document(numeric_exact.read_bytes())
    except BaseException as exc:
        failures.append(f"numeric token exact boundary was rejected: {exc}")
    else:
        check(
            set(exact_numeric_document).issuperset({"asset", "numericBoundary"}),
            "numeric token exact boundary lost the parsed document",
        )
    try:
        module._source_document(numeric_oversized.read_bytes())
    except module.GlbError as exc:
        numeric_diagnostic = str(exc).lower()
        check(
            "integer" in numeric_diagnostic and "limit" in numeric_diagnostic,
            "numeric token +1 diagnostic did not identify the integer limit",
        )
    else:
        failures.append("numeric token +1 was accepted by the silhouette loader")
finally:
    if integer_setter is not None and previous_integer_limit is not None:
        integer_setter(previous_integer_limit)


# The selected-scene 8M ceiling remains separate from the strict inspector's
# all-mesh budget. Lower only the latter for a compact exact/+1 oracle: the
# selected mesh is exactly ten units, while an unselected mesh sharing the same
# accessors takes the all-mesh total to twenty units.
aggregate_case = root / "unselected-aggregate-work"
aggregate_case.mkdir()
aggregate_source = aggregate_case / "source.glb"
aggregate_output = aggregate_case / "output.png"
aggregate_document, aggregate_binary = parse_glb(source_template)
aggregate_mesh = aggregate_document["meshes"][0]
aggregate_item = aggregate_mesh["primitives"][0]
aggregate_position = aggregate_item["attributes"]["POSITION"]
aggregate_indices = aggregate_item["indices"]
aggregate_exact_work = (
    aggregate_document["accessors"][aggregate_position]["count"]
    + aggregate_document["accessors"][aggregate_indices]["count"]
)
check(aggregate_exact_work == 10, "unselected aggregate fixture exact work changed")
aggregate_document["meshes"].append(
    {"primitives": [copy.deepcopy(aggregate_item)]}
)
check(
    len(aggregate_document["nodes"]) == 1
    and aggregate_document["nodes"][0].get("mesh") == 0,
    "unselected aggregate fixture selected the added mesh",
)
write_glb(aggregate_source, aggregate_document, aggregate_binary)

geometry_missing = object()
previous_geometry_limit = getattr(
    module.glb_metrics,
    "MAX_GEOMETRY_WORK",
    geometry_missing,
)
setattr(module.glb_metrics, "MAX_GEOMETRY_WORK", aggregate_exact_work)
exact_aggregate_output = aggregate_case / "exact.png"
try:
    try:
        module.render(
            source_template,
            exact_aggregate_output,
            25.0,
            size=64,
            splat_radius=1,
            minimum_coverage=0.0,
        )
    except BaseException as exc:
        failures.append(f"all-mesh exact work boundary was rejected: {exc}")
    else:
        check(
            is_complete_png(exact_aggregate_output, 64),
            "all-mesh exact work boundary did not render a complete PNG",
        )

    real_metrics_unpack = module.glb_metrics.struct.unpack_from
    aggregate_position_decodes = 0

    def observe_metrics_unpack(format_string, *args, **kwargs):
        global aggregate_position_decodes
        if format_string == "<3f":
            aggregate_position_decodes += 1
        return real_metrics_unpack(format_string, *args, **kwargs)

    module.glb_metrics.struct.unpack_from = observe_metrics_unpack
    try:
        try:
            module.render(
                aggregate_source,
                aggregate_output,
                25.0,
                size=64,
                splat_radius=1,
                minimum_coverage=0.0,
            )
        except module.GlbError as exc:
            aggregate_diagnostic = str(exc).lower()
            check(
                "geometry" in aggregate_diagnostic
                and "work" in aggregate_diagnostic,
                "unselected aggregate diagnostic did not identify geometry work",
            )
        else:
            failures.append("unselected mesh aggregate work was accepted")
    finally:
        module.glb_metrics.struct.unpack_from = real_metrics_unpack
    check(
        aggregate_position_decodes == 0,
        "unselected aggregate work was rejected after POSITION decoding",
    )
    check(
        not aggregate_output.exists(),
        "unselected aggregate rejection left an evidence PNG",
    )
finally:
    if previous_geometry_limit is geometry_missing:
        delattr(module.glb_metrics, "MAX_GEOMETRY_WORK")
    else:
        module.glb_metrics.MAX_GEOMETRY_WORK = previous_geometry_limit


# Mutation caught: every index is valid but zero, so raw POSITION splatting
# produces the same acceptable PNG even though all triangle topology is gone.
zeroed_source = root / "zeroed-indices.glb"
build_zeroed_indices(source_template, zeroed_source)
source_position_count, source_indices = surface_shape(source_template)
zeroed_position_count, zeroed_indices = surface_shape(zeroed_source)
check(
    hashlib.sha256(zeroed_source.read_bytes()).digest()
    != hashlib.sha256(source_template.read_bytes()).digest(),
    "zero-index mutation did not change the GLB",
)
check(
    source_indices is not None and len(source_indices) >= 3 and any(source_indices),
    "zero-index control lacks nonzero triangle indices",
)
check(
    zeroed_position_count == source_position_count
    and source_indices is not None
    and zeroed_indices is not None
    and len(zeroed_indices) == len(source_indices)
    and len(zeroed_indices) >= 3
    and all(index == 0 for index in zeroed_indices),
    "zero-index mutation did not preserve and zero every triangle index",
)
surface_output = root / "referenced-surface.png"
surface_result = invoke(
    source_template,
    surface_output,
    "25",
    "--size", "128",
    "--splat-radius", "4",
    "--min-coverage", "0.001",
)
check(
    surface_result is not None and surface_result.returncode == 0,
    "referenced surface control did not render",
)
zeroed_output = root / "zeroed-indices.png"
zeroed_result = invoke(
    zeroed_source,
    zeroed_output,
    "25",
    "--size", "128",
    "--splat-radius", "4",
    "--min-coverage", "0.001",
)
check_prefixed_failure(zeroed_result, "zeroed triangle topology")
check(not zeroed_output.exists(), "zeroed topology left an evidence PNG")
if zeroed_output.exists() and surface_output.exists():
    check(
        zeroed_output.read_bytes() != surface_output.read_bytes(),
        "zeroed topology produced byte-identical evidence",
    )


# Mutation caught: ten thousand decorative POSITION values are not referenced
# by any index.  They must neither change pixels nor inflate reported evidence.
orphan_source = root / "orphan-positions.glb"
build_orphan_positions(source_template, orphan_source)
orphan_position_count, orphan_indices = surface_shape(orphan_source)
check(
    source_position_count is not None
    and orphan_position_count == source_position_count + 10_000,
    "orphan mutation did not append exactly 10000 POSITION values",
)
check(
    source_position_count is not None
    and source_indices is not None
    and orphan_indices == source_indices
    and bool(source_indices)
    and max(source_indices) < source_position_count,
    "orphan mutation did not leave appended POSITION values unreferenced",
)
orphan_output = root / "orphan-positions.png"
orphan_result = invoke(
    orphan_source,
    orphan_output,
    "25",
    "--size", "128",
    "--splat-radius", "4",
    "--min-coverage", "0.001",
)
check(
    orphan_result is not None and orphan_result.returncode == 0,
    "unreferenced POSITION mutation did not render",
)
if (
    surface_result is not None
    and surface_result.returncode == 0
    and orphan_result is not None
    and orphan_result.returncode == 0
):
    check(
        orphan_output.read_bytes() == surface_output.read_bytes(),
        "unreferenced POSITION values changed evidence pixels",
    )
    check(
        reported_vertices(orphan_result.stdout)
        == reported_vertices(surface_result.stdout),
        "unreferenced POSITION values inflated the evidence count",
    )


# Hostile numeric inputs have bounded, diagnostic-only failure.  The final
# case keeps each number representable but makes their combined raster work
# unsafe over a genuinely referenced 9,999-corner surface.
high_work_source = root / "high-work-surface.glb"
build_high_work_surface(orphan_source, high_work_source)
numeric_cases = (
    (
        "hostile size",
        source_template,
        ("0", "--size", "4000000000", "--splat-radius", "1", "--min-coverage", "0"),
    ),
    (
        "hostile splat radius",
        source_template,
        ("0", "--size", "32", "--splat-radius", "1000000000", "--min-coverage", "0"),
    ),
    (
        "hostile raster work factor",
        high_work_source,
        ("0", "--size", "128", "--splat-radius", "64", "--min-coverage", "0"),
    ),
)
for label, numeric_source, arguments in numeric_cases:
    output = root / f"{label.replace(' ', '-')}.png"
    started = time.monotonic()
    result = invoke(numeric_source, output, *arguments, timeout=1.5)
    elapsed = time.monotonic() - started
    check_prefixed_failure(result, label)
    check(elapsed < 2.0, f"{label}: failure was not bounded")
    check(not output.exists(), f"{label}: left an output PNG")


# A special GLB input is rejected by metadata before an open can block.
fifo_source = root / "source.fifo"
os.mkfifo(fifo_source)
fifo_source_output = root / "fifo-source.png"
fifo_source_result = invoke(
    fifo_source,
    fifo_source_output,
    "0",
    "--size", "64",
    "--splat-radius", "1",
    "--min-coverage", "0",
    timeout=1.5,
)
check_prefixed_failure(fifo_source_result, "FIFO source")
check(not fifo_source_output.exists(), "FIFO source left an output PNG")

# A FIFO output is not a GLB input.  As with linked output paths, either clean
# rejection or bounded atomic replacement with a complete PNG is compliant.
fifo_output_case = root / "fifo-output"
fifo_output_case.mkdir()
fifo_output_source = fifo_output_case / "source.glb"
shutil.copyfile(source_template, fifo_output_source)
fifo_output_source_before = fifo_output_source.read_bytes()
fifo_output = fifo_output_case / "output.fifo"
os.mkfifo(fifo_output)
fifo_output_result = invoke(
    fifo_output_source,
    fifo_output,
    "0",
    "--size", "64",
    "--splat-radius", "1",
    "--min-coverage", "0",
    timeout=1.5,
)
if fifo_output_result is not None and fifo_output_result.returncode == 0:
    check(fifo_output_result.stderr == "", "FIFO output success wrote stderr")
    check(is_complete_png(fifo_output, 64), "FIFO output is not a complete PNG")
    check(
        fifo_output.is_file() and not fifo_output.is_symlink(),
        "FIFO output success did not atomically install a regular PNG",
    )
else:
    check_prefixed_failure(fifo_output_result, "FIFO output")
    check(not fifo_output.is_file(), "FIFO output rejection left a partial file")
check(
    fifo_output_source.read_bytes() == fifo_output_source_before,
    "FIFO output changed its GLB source",
)
fifo_output_names = {path.name for path in fifo_output_case.iterdir()}
check(
    fifo_output_names in ({"source.glb"}, {"source.glb", "output.fifo"}),
    "FIFO output transaction left staging residue",
)


# A sparse 1 TiB input runs in a lower-priority subprocess whose elapsed time
# and resident set are monitored externally.  It cannot be fully consumed by a
# bounded-memory streaming reader before the hard timeout.  Production may
# read a small bounded header, but must select the size-limit diagnostic before
# walking the file.
oversized_case = root / "oversized-input"
oversized_case.mkdir()
oversized_source = oversized_case / "oversized.glb"
with oversized_source.open("wb") as handle:
    handle.write(
        struct.pack("<4sII", b"glTF", 2, 0xFFFFFFFF)
        + struct.pack("<I4s", 4, b"JSON")
        + b"{}  "
    )
    handle.truncate(1 << 40)
oversized_output = oversized_case / "oversized.png"
oversized_result, oversized_memory, oversized_time, oversized_peak = (
    invoke_with_rss_limit(
        script,
        oversized_source,
        oversized_output,
        "0",
        "--size", "64",
        "--splat-radius", "1",
        "--min-coverage", "0",
    )
)
if oversized_memory:
    failures.append(
        f"oversized input exceeded 64 MiB RSS budget (peak {oversized_peak} bytes)"
    )
elif oversized_time:
    failures.append("oversized input exceeded bounded runtime")
else:
    check_oversized_failure(oversized_result, "oversized input")
check(not oversized_output.exists(), "oversized input left an output PNG")
check(
    {path.name for path in oversized_case.iterdir()} == {"oversized.glb"},
    "oversized input left staging residue",
)

# Control: one bounded 1 MiB inspection read stays under both limits and may
# still produce the exact size diagnostic.
bounded_reader = root / "bounded-header-reader.py"
bounded_reader.write_text(
    "from pathlib import Path\n"
    "import sys\n"
    "with Path(sys.argv[1]).open('rb') as handle:\n"
    "    handle.read(1024 * 1024)\n"
    f"print({oversized_diagnostic!r}, file=sys.stderr)\n"
    "raise SystemExit(1)\n",
    encoding="utf-8",
)
bounded_output = root / "bounded-header-reader.png"
bounded_result, bounded_memory, bounded_time, _bounded_peak = (
    invoke_with_rss_limit(
        bounded_reader,
        oversized_source,
        bounded_output,
        timeout=2.0,
    )
)
check(
    not bounded_memory and not bounded_time,
    "bounded oversize oracle rejected a small inspection read",
)
check_oversized_failure(bounded_result, "bounded inspection control")
check(not bounded_output.exists(), "bounded inspection control left output")

# Mutation control: a Path.open(...).read(1 MiB) loop keeps resident memory
# small, but full streaming consumption of 1 TiB must hit the time bound before
# it can print the otherwise-exact diagnostic.
streaming_reader = root / "streaming-chunk-reader.py"
streaming_reader.write_text(
    "from pathlib import Path\n"
    "import sys\n"
    "with Path(sys.argv[1]).open('rb') as handle:\n"
    "    while handle.read(1024 * 1024):\n"
    "        pass\n"
    f"print({oversized_diagnostic!r}, file=sys.stderr)\n"
    "raise SystemExit(1)\n",
    encoding="utf-8",
)
streaming_output = root / "streaming-chunk-reader.png"
streaming_result, streaming_memory, streaming_time, _streaming_peak = (
    invoke_with_rss_limit(
        streaming_reader,
        oversized_source,
        streaming_output,
        timeout=1.0,
    )
)
check(
    streaming_time
    and not streaming_memory
    and streaming_result.returncode != 0,
    "bounded oversize oracle did not stop the streaming-chunk mutation",
)
check(not streaming_output.exists(), "streaming reader mutation left output")


# Reviewer arithmetic oracles exercise the cheap selected-scene guard directly.
# They intentionally omit large backing buffers: reaching strict GLB inspection
# would itself violate the guard-before-decode property pinned by the CLI cases.
def check_combined_work_rejection(
    error: BaseException | None,
    label: str,
) -> None:
    check(
        isinstance(error, module.RenderError),
        f"{label}: combined work was not rejected by the cheap guard",
    )
    if error is None:
        return
    diagnostic = str(error).lower()
    check(
        "8000000" in diagnostic
        and "position" in diagnostic
        and "reference" in diagnostic
        and "work" in diagnostic,
        f"{label}: rejection did not identify the combined work cap",
    )


# Mutation caught: treating an unindexed primitive as having zero implicit
# references.  One POSITION accessor of 4,000,001 values is individually below
# the ceiling, but its implicit references plus decode values total 8,000,002.
unindexed_position_count = 4_000_001
unindexed_document = selected_work_document(
    position_count=unindexed_position_count,
    index_count=None,
)
unindexed_meshes = unindexed_document["meshes"]
assert isinstance(unindexed_meshes, list) and len(unindexed_meshes) == 1
unindexed_mesh = unindexed_meshes[0]
assert isinstance(unindexed_mesh, dict)
unindexed_primitives = unindexed_mesh["primitives"]
assert isinstance(unindexed_primitives, list) and len(unindexed_primitives) == 1
unindexed_primitive = unindexed_primitives[0]
assert isinstance(unindexed_primitive, dict)
check(
    "indices" not in unindexed_primitive
    and unindexed_position_count <= maximum_selected_scene_work
    and unindexed_position_count * 2 == 8_000_002
    and unindexed_position_count * 2 > maximum_selected_scene_work,
    "unindexed work: fixture does not isolate implicit-reference accounting",
)
check_combined_work_rejection(
    selected_work_error(unindexed_document),
    "unindexed implicit-reference work",
)


# Mutation caught: adding POSITION work only for the first selected instance.
# Index work across all 800 nodes and first-instance-only POSITION work are both
# safe; only multiplying both categories per node crosses the fixed ceiling.
instanced_position_count = 10_000
instanced_index_count = 3
instanced_node_count = 800
instanced_mesh_work = instanced_position_count + instanced_index_count
instanced_combined_work = instanced_mesh_work * instanced_node_count
instanced_first_position_work = (
    instanced_position_count + instanced_index_count * instanced_node_count
)
check(
    instanced_index_count * instanced_node_count
    < maximum_selected_scene_work
    and instanced_first_position_work < maximum_selected_scene_work
    and instanced_mesh_work * (instanced_node_count - 1)
    <= maximum_selected_scene_work
    and instanced_combined_work == 8_002_400
    and instanced_combined_work > maximum_selected_scene_work,
    "instanced POSITION work: fixture does not isolate per-node multiplication",
)
check_combined_work_rejection(
    selected_work_error(
        selected_work_document(
            position_count=instanced_position_count,
            index_count=instanced_index_count,
            node_count=instanced_node_count,
        )
    ),
    "instanced POSITION work",
)


# Boundary mutation caught: changing the guard from > to >= rejects an exact
# 8,000,000-unit selected scene.  The adjacent +1 document must still reject.
boundary_position_count = 4_000_000
boundary_reference_count = 4_000_000
boundary_document = selected_work_document(
    position_count=boundary_position_count,
    index_count=boundary_reference_count,
)
check(
    boundary_position_count + boundary_reference_count
    == maximum_selected_scene_work,
    "combined-work boundary: fixture is not exactly 8000000 units",
)
check(
    selected_work_error(boundary_document) is None,
    "combined-work boundary: exact 8000000 units were rejected",
)
boundary_plus_one_document = selected_work_document(
    position_count=boundary_position_count,
    index_count=boundary_reference_count + 1,
)
check_combined_work_rejection(
    selected_work_error(boundary_plus_one_document),
    "combined-work boundary +1",
)


# Independent-review follow-up: selected-scene decode work is bounded before
# the strict metrics inspector or surface iterator can perform the full walk.
# The fixed ceiling is 8,000,000 combined index-reference and POSITION-value
# units.  It is deliberately above the frozen real-source envelope: all 15
# recorded sources have one mesh and one primitive, and the worst source has
# 5,956,374 selected references plus 1,023,844 POSITION values, exactly
# 6,980,218 combined units.  This tracked evidence is the positive current-real
# envelope control; the 1,000-triangle fixture below is the ordinary executable
# control.
recorded_metrics = json.loads(
    Path("docs/design/assets/GLB-DECIMATION-METRICS.json").read_text(
        encoding="utf-8"
    )
)
recorded_assets = recorded_metrics.get("assets")
check(
    isinstance(recorded_assets, list) and len(recorded_assets) == 15,
    "topology envelope: tracked metrics do not contain 15 assets",
)
recorded_work: list[tuple[int, int, int]] = []
if isinstance(recorded_assets, list):
    for asset in recorded_assets:
        source_metrics = asset.get("source") if isinstance(asset, dict) else None
        if not isinstance(source_metrics, dict):
            failures.append("topology envelope: malformed source metrics")
            continue
        triangles = source_metrics.get("triangles")
        vertices = source_metrics.get("vertices")
        meshes = source_metrics.get("meshes")
        primitives = source_metrics.get("primitives")
        if (
            not isinstance(triangles, int)
            or not isinstance(vertices, int)
            or meshes != 1
            or primitives != 1
        ):
            failures.append(
                "topology envelope: frozen source is not one measured "
                "mode-4 mesh primitive"
            )
            continue
        references = triangles * 3
        recorded_work.append((references, vertices, references + vertices))
check(
    len(recorded_work) == 15
    and max(recorded_work, key=lambda item: item[2], default=(0, 0, 0))
    == (5_956_374, 1_023_844, 6_980_218)
    and all(
        combined <= maximum_selected_scene_work
        for _references, _positions, combined in recorded_work
    ),
    "decode-work envelope: 8000000-unit ceiling rejects a recorded source",
)

topology_control_output = root / "shared-topology-control.png"
topology_control = invoke(
    shared_topology_template,
    topology_control_output,
    "25",
    "--size", "64",
    "--splat-radius", "1",
    "--min-coverage", "0",
    timeout=2.0,
)
check(
    topology_control is not None and topology_control.returncode == 0,
    "decode-work budget: ordinary 4002-unit surface was rejected",
)
check(
    is_complete_png(topology_control_output, 64),
    "topology budget: ordinary control did not leave a complete PNG",
)

# Reviewer regression: repeated primitives share one large POSITION accessor
# while retaining only six index references apiece.  Index-only accounting sees
# just 4,800 references and admits the file; the strict inspector then performs
# more than eight million POSITION decodes.  Combined accounting must reject
# this compact real-GLB shape before that walk.
shared_position_case = root / "shared-position-budget"
shared_position_case.mkdir()
shared_position_source = shared_position_case / "shared-position.glb"
shared_position_output = shared_position_case / "shared-position.png"
build_shared_primitive_scene(
    orphan_source,
    shared_position_source,
    primitive_count=800,
    expected_index_count=6,
    expected_position_count=10_004,
)
(
    shared_position_primitive_total,
    shared_position_distinct_indices,
    shared_position_distinct_positions,
    shared_position_references,
    shared_position_values,
    shared_position_work,
) = shared_scene_work(shared_position_source)
check(
    shared_position_primitive_total == 800
    and shared_position_distinct_indices == 1
    and shared_position_distinct_positions == 1
    and shared_position_references == 4_800
    and shared_position_references < maximum_selected_scene_work
    and shared_position_values == 8_003_200
    and shared_position_work == 8_008_000
    and shared_position_work > maximum_selected_scene_work,
    "shared POSITION work: fixture does not discriminate index-only accounting",
)
shared_position_started = time.monotonic()
shared_position_result = invoke(
    shared_position_source,
    shared_position_output,
    "25",
    "--size", "64",
    "--splat-radius", "1",
    "--min-coverage", "0",
    timeout=1.5,
)
shared_position_elapsed = time.monotonic() - shared_position_started
check_prefixed_failure(shared_position_result, "shared POSITION decode work")
if shared_position_result is not None:
    shared_position_diagnostic = shared_position_result.stderr.lower()
    check(
        "8000000" in shared_position_diagnostic
        and "position" in shared_position_diagnostic
        and "reference" in shared_position_diagnostic
        and "work" in shared_position_diagnostic,
        "shared POSITION work: diagnostic does not identify the combined cap",
    )
check(
    shared_position_elapsed < 2.0,
    "shared POSITION work: rejection occurred after expensive accessor decoding",
)
check(
    not shared_position_output.exists(),
    "shared POSITION work left an evidence PNG",
)
check(
    {path.name for path in shared_position_case.iterdir()}
    == {"shared-position.glb"},
    "shared POSITION work rejection left staging residue",
)

topology_case = root / "aggregate-topology-budget"
topology_case.mkdir()
topology_source = topology_case / "shared-primitives.glb"
topology_output = topology_case / "shared-primitives.png"
build_shared_primitive_scene(
    shared_topology_template,
    topology_source,
    primitive_count=3_000,
    expected_index_count=3_000,
    expected_position_count=1_002,
)
(
    primitive_total,
    distinct_indices,
    distinct_positions,
    selected_references,
    selected_positions,
    selected_work,
) = shared_scene_work(topology_source)
check(
    primitive_total == 3_000
    and distinct_indices == 1
    and distinct_positions == 1
    and selected_references == 9_000_000
    and selected_positions == 3_006_000
    and selected_work == 12_006_000
    and selected_work > maximum_selected_scene_work,
    "topology budget: compact hostile fixture does not cross the fixed cap",
)
topology_started = time.monotonic()
topology_result = invoke(
    topology_source,
    topology_output,
    "25",
    "--size", "64",
    "--splat-radius", "1",
    "--min-coverage", "0",
    timeout=1.5,
)
topology_elapsed = time.monotonic() - topology_started
check_prefixed_failure(topology_result, "aggregate selected-scene topology")
if topology_result is not None:
    topology_diagnostic = topology_result.stderr.lower()
    check(
        "8000000" in topology_diagnostic
        and "position" in topology_diagnostic
        and "reference" in topology_diagnostic,
        "topology budget: diagnostic does not identify the combined work cap",
    )
check(
    topology_elapsed < 2.0,
    "topology budget: rejection occurred after an expensive surface walk",
)
check(not topology_output.exists(), "topology budget left an evidence PNG")
check(
    {path.name for path in topology_case.iterdir()} == {"shared-primitives.glb"},
    "topology budget rejection left staging residue",
)

# Mutation caught: counting each mesh definition only once misses scene-node
# instancing.  This fixture has one mesh/primitive with 3,000 indices and 1,002
# POSITION values, which is safely below the cap at the mesh-definition layer,
# but 3,000 selected nodes make the actual selected-scene work 12,006,000 units.
instanced_topology_case = root / "instanced-topology-budget"
instanced_topology_case.mkdir()
instanced_topology_source = instanced_topology_case / "shared-mesh-nodes.glb"
instanced_topology_output = instanced_topology_case / "shared-mesh-nodes.png"
build_shared_node_scene(
    shared_topology_template,
    instanced_topology_source,
    node_count=3_000,
)
(
    selected_node_total,
    mesh_primitive_total,
    mesh_definition_references,
    mesh_definition_positions,
    mesh_definition_work,
    instanced_selected_references,
    instanced_selected_positions,
    instanced_selected_work,
) = instanced_scene_work(instanced_topology_source)
check(
    selected_node_total == 3_000
    and mesh_primitive_total == 1
    and mesh_definition_references == 3_000
    and mesh_definition_positions == 1_002
    and mesh_definition_work == 4_002
    and mesh_definition_work < maximum_selected_scene_work
    and instanced_selected_references == 9_000_000
    and instanced_selected_positions == 3_006_000
    and instanced_selected_work == 12_006_000
    and instanced_selected_work > maximum_selected_scene_work,
    "instanced topology: fixture does not discriminate mesh-only counting",
)
instanced_topology_started = time.monotonic()
instanced_topology_result = invoke(
    instanced_topology_source,
    instanced_topology_output,
    "25",
    "--size", "64",
    "--splat-radius", "1",
    "--min-coverage", "0",
    timeout=1.5,
)
instanced_topology_elapsed = time.monotonic() - instanced_topology_started
check_prefixed_failure(
    instanced_topology_result,
    "instanced selected-scene topology",
)
if instanced_topology_result is not None:
    instanced_topology_diagnostic = instanced_topology_result.stderr.lower()
    check(
        "8000000" in instanced_topology_diagnostic
        and "position" in instanced_topology_diagnostic
        and "reference" in instanced_topology_diagnostic,
        "instanced topology: diagnostic does not identify the combined work cap",
    )
check(
    instanced_topology_elapsed < 2.0,
    "instanced topology: rejection occurred after expanded scene traversal",
)
check(
    not instanced_topology_output.exists(),
    "instanced topology left an evidence PNG",
)
check(
    {path.name for path in instanced_topology_case.iterdir()}
    == {"shared-mesh-nodes.glb"},
    "instanced topology rejection left staging residue",
)


# Independent-review follow-up: the source itself is immutable evidence input.
# It must be a regular, single-link, non-symlink file, and the bytes opened
# after metadata preflight must be the same inode that was preflighted.
source_custody = root / "source-custody"
source_custody.mkdir()
for link_kind in ("symlink", "hardlink"):
    case = source_custody / link_kind
    case.mkdir()
    target = case / "target.glb"
    source = case / "source.glb"
    output = case / "output.png"
    shutil.copyfile(source_template, target)
    target_before = target.read_bytes()
    if link_kind == "symlink":
        source.symlink_to(target.name)
    else:
        os.link(target, source)
    result = invoke(
        source,
        output,
        "25",
        "--size", "64",
        "--splat-radius", "1",
        "--min-coverage", "0",
    )
    check_prefixed_failure(result, f"{link_kind} GLB source")
    check(not output.exists(), f"{link_kind} GLB source left an evidence PNG")
    check(
        target.read_bytes() == target_before
        and source.read_bytes() == target_before,
        f"{link_kind} GLB source changed source bytes",
    )
    if link_kind == "symlink":
        check(
            source.is_symlink() and os.readlink(source) == target.name,
            "symlink GLB source changed link custody",
        )
    else:
        check(
            os.path.samefile(source, target)
            and source.stat().st_nlink == 2
            and target.stat().st_nlink == 2,
            "hardlink GLB source changed link custody",
        )
    check(
        {path.name for path in case.iterdir()} == {"source.glb", "target.glb"},
        f"{link_kind} GLB source left staging residue",
    )

swap_case = source_custody / "preflight-open-swap"
swap_case.mkdir()
swap_source = swap_case / "source.glb"
swap_replacement = swap_case / "replacement.glb"
swap_held_original = swap_case / "held-original.glb"
swap_output = swap_case / "output.png"
shutil.copyfile(source_template, swap_source)
shutil.copyfile(target_template, swap_replacement)
swap_original_bytes = swap_source.read_bytes()
swap_replacement_bytes = swap_replacement.read_bytes()
check(
    swap_original_bytes != swap_replacement_bytes,
    "source swap: original and replacement fixtures are not distinct",
)

swap_module_name = "catmetro_glb_silhouette_source_swap_probe"
swap_spec = importlib.util.spec_from_file_location(swap_module_name, script)
assert swap_spec is not None and swap_spec.loader is not None
swap_module = importlib.util.module_from_spec(swap_spec)
swap_original_open = os.open
swap_original_replace = os.replace
swap_performed = False
swap_error: BaseException | None = None


def swap_before_open(
    path: object,
    flags: int,
    *arguments: object,
    **keywords: object,
) -> int:
    global swap_performed
    if (
        not swap_performed
        and open_targets_path(path, keywords, swap_source)
    ):
        swap_original_replace(swap_source, swap_held_original)
        swap_original_replace(swap_replacement, swap_source)
        swap_performed = True
    return swap_original_open(path, flags, *arguments, **keywords)


sys.path.insert(0, scripts_directory)
try:
    swap_spec.loader.exec_module(swap_module)
    os.open = swap_before_open
    try:
        swap_module.render(
            swap_source,
            swap_output,
            25.0,
            size=64,
            splat_radius=1,
            minimum_coverage=0.0,
        )
    except BaseException as exc:
        swap_error = exc
finally:
    os.open = swap_original_open
    sys.path.remove(scripts_directory)
    sys.modules.pop(swap_module_name, None)

check(swap_performed, "source swap: preflight/open mutation was not injected")
check(swap_error is not None, "source swap: replacement bytes were rendered")
check(not swap_output.exists(), "source swap left misattributed evidence")
check(
    swap_held_original.read_bytes() == swap_original_bytes
    and swap_source.read_bytes() == swap_replacement_bytes,
    "source swap changed either captured source payload",
)
check(
    {path.name for path in swap_case.iterdir()}
    == {"held-original.glb", "source.glb"},
    "source swap rejection left staging residue",
)

# Mutation caught: a superficially correct lstat/O_NOFOLLOW/fstat preflight
# that closes its descriptor and later reopens the pathname.  The hook swaps
# only on the second source-path os.open.  A renderer may reject that mutation
# or render from the first validated snapshot; it may never render the
# replacement bytes.  Distinct control renders and a forced post-render second
# open prove both the visual oracle and the mutation injector.
reopen_original_control = root / "reopen-original-control.png"
reopen_replacement_control = root / "reopen-replacement-control.png"
reopen_original_control_result = invoke(
    source_template,
    reopen_original_control,
    "25",
    "--size", "64",
    "--splat-radius", "1",
    "--min-coverage", "0",
)
reopen_replacement_control_result = invoke(
    target_template,
    reopen_replacement_control,
    "25",
    "--size", "64",
    "--splat-radius", "1",
    "--min-coverage", "0",
)
check(
    reopen_original_control_result is not None
    and reopen_original_control_result.returncode == 0
    and reopen_replacement_control_result is not None
    and reopen_replacement_control_result.returncode == 0,
    "second-open controls did not both render",
)
reopen_original_control_bytes = (
    reopen_original_control.read_bytes()
    if reopen_original_control.exists()
    else b""
)
reopen_replacement_control_bytes = (
    reopen_replacement_control.read_bytes()
    if reopen_replacement_control.exists()
    else b""
)
check(
    bool(reopen_original_control_bytes)
    and bool(reopen_replacement_control_bytes)
    and reopen_original_control_bytes != reopen_replacement_control_bytes
    and reopen_original_control_result is not None
    and reopen_replacement_control_result is not None
    and reported_vertices(reopen_original_control_result.stdout)
    != reported_vertices(reopen_replacement_control_result.stdout),
    "second-open controls do not distinguish original and replacement",
)

reopen_case = source_custody / "second-open-swap"
reopen_case.mkdir()
reopen_source = reopen_case / "source.glb"
reopen_replacement = reopen_case / "replacement.glb"
reopen_held_original = reopen_case / "held-original.glb"
reopen_output = reopen_case / "output.png"
shutil.copyfile(source_template, reopen_source)
shutil.copyfile(target_template, reopen_replacement)
reopen_original_bytes = reopen_source.read_bytes()
reopen_replacement_bytes = reopen_replacement.read_bytes()
check(
    reopen_original_bytes != reopen_replacement_bytes,
    "second-open swap fixtures are not byte-distinct",
)

reopen_module_name = "catmetro_glb_silhouette_second_open_probe"
reopen_spec = importlib.util.spec_from_file_location(reopen_module_name, script)
assert reopen_spec is not None and reopen_spec.loader is not None
reopen_module = importlib.util.module_from_spec(reopen_spec)
reopen_original_open = os.open
reopen_original_replace = os.replace
reopen_open_count = 0
reopen_swap_performed = False
reopen_error: BaseException | None = None
reopen_render_result: tuple[int, int, float] | None = None


def swap_on_second_open(
    path: object,
    flags: int,
    *arguments: object,
    **keywords: object,
) -> int:
    global reopen_open_count, reopen_swap_performed
    if open_targets_path(path, keywords, reopen_source):
        reopen_open_count += 1
        if reopen_open_count == 2:
            reopen_original_replace(reopen_source, reopen_held_original)
            reopen_original_replace(reopen_replacement, reopen_source)
            reopen_swap_performed = True
    return reopen_original_open(path, flags, *arguments, **keywords)


sys.path.insert(0, scripts_directory)
try:
    reopen_spec.loader.exec_module(reopen_module)
    os.open = swap_on_second_open
    try:
        reopen_render_result = reopen_module.render(
            reopen_source,
            reopen_output,
            25.0,
            size=64,
            splat_radius=1,
            minimum_coverage=0.0,
        )
    except BaseException as exc:
        reopen_error = exc
    reopen_render_open_count = reopen_open_count

    # A correct one-open snapshot does not naturally reach the injected swap.
    # Complete the second-open mutation after rendering so the setup oracle
    # proves that exactly the next pathname open selects replacement bytes.
    mutation_probe_payload = b""
    while reopen_open_count < 2:
        probe_descriptor = os.open(reopen_source, os.O_RDONLY)
        try:
            chunks: list[bytes] = []
            while True:
                chunk = os.read(probe_descriptor, 64 * 1024)
                if not chunk:
                    break
                chunks.append(chunk)
            mutation_probe_payload = b"".join(chunks)
        finally:
            os.close(probe_descriptor)
finally:
    os.open = reopen_original_open
    sys.path.remove(scripts_directory)
    sys.modules.pop(reopen_module_name, None)

check(
    reopen_render_open_count >= 1,
    "second-open policy did not consume a no-follow source descriptor",
)
check(reopen_swap_performed, "second-open pathname swap was not injected")
check(
    reopen_held_original.read_bytes() == reopen_original_bytes
    and reopen_source.read_bytes() == reopen_replacement_bytes,
    "second-open swap changed either captured source payload",
)
if mutation_probe_payload:
    check(
        mutation_probe_payload == reopen_replacement_bytes,
        "second-open mutation probe did not read replacement bytes",
    )
if reopen_error is not None:
    check(not reopen_output.exists(), "rejected second-open swap left a PNG")
else:
    check(
        reopen_render_result is not None
        and reopen_render_result[0]
        == reported_vertices(reopen_original_control_result.stdout)
        and reopen_output.exists()
        and reopen_output.read_bytes() == reopen_original_control_bytes
        and reopen_output.read_bytes() != reopen_replacement_control_bytes,
        "second-open swap rendered replacement bytes instead of first snapshot",
    )
check(
    {path.name for path in reopen_case.iterdir()}
    in (
        {"held-original.glb", "source.glb"},
        {"held-original.glb", "source.glb", "output.png"},
    ),
    "second-open swap left staging residue",
)


# Independent-review follow-up: face non-degeneracy is a world-space property.
# A singular scene transform that collapses a valid local triangle cannot
# produce silhouette evidence.
singular_case = root / "singular-world-transform"
singular_case.mkdir()
singular_source = singular_case / "singular.glb"
singular_output = singular_case / "singular.png"
singular_document, singular_binary = parse_glb(source_template)
singular_nodes = singular_document["nodes"]
assert isinstance(singular_nodes, list) and len(singular_nodes) == 1
singular_node = singular_nodes[0]
assert isinstance(singular_node, dict)
singular_node["scale"] = [1.0, 0.0, 1.0]
write_glb(singular_source, singular_document, singular_binary)
singular_item = primitive(singular_document)
singular_attributes = singular_item["attributes"]
assert isinstance(singular_attributes, dict)
singular_position_number = singular_attributes["POSITION"]
singular_index_number = singular_item["indices"]
assert isinstance(singular_position_number, int)
assert isinstance(singular_index_number, int)
singular_position_accessor, singular_position_view = accessor_and_view(
    singular_document, singular_position_number
)
singular_index_accessor, singular_index_view = accessor_and_view(
    singular_document, singular_index_number
)
assert singular_position_accessor["componentType"] == 5126
assert singular_position_accessor["type"] == "VEC3"
assert singular_index_accessor["componentType"] == 5123
position_start = int(singular_position_view.get("byteOffset", 0)) + int(
    singular_position_accessor.get("byteOffset", 0)
)
index_start = int(singular_index_view.get("byteOffset", 0)) + int(
    singular_index_accessor.get("byteOffset", 0)
)
first_indices = struct.unpack_from("<3H", singular_binary, index_start)
local_triangle = tuple(
    struct.unpack_from("<3f", singular_binary, position_start + index * 12)
    for index in first_indices
)


def triangle_cross(
    points: tuple[tuple[float, float, float], ...],
) -> tuple[float, float, float]:
    a, b, c = points
    ab = tuple(b[axis] - a[axis] for axis in range(3))
    ac = tuple(c[axis] - a[axis] for axis in range(3))
    return (
        ab[1] * ac[2] - ab[2] * ac[1],
        ab[2] * ac[0] - ab[0] * ac[2],
        ab[0] * ac[1] - ab[1] * ac[0],
    )


world_triangle = tuple((x, 0.0, z) for x, _y, z in local_triangle)
check(
    triangle_cross(local_triangle) != (0.0, 0.0, 0.0)
    and triangle_cross(world_triangle) == (0.0, 0.0, 0.0),
    "world-collapse fixture is not locally valid and world-degenerate",
)
singular_result = invoke(
    singular_source,
    singular_output,
    "25",
    "--size", "64",
    "--splat-radius", "4",
    "--min-coverage", "0",
)
check_prefixed_failure(singular_result, "world-collapsed triangle")
check(not singular_output.exists(), "world-collapsed triangle left a PNG")
check(
    {path.name for path in singular_case.iterdir()} == {"singular.glb"},
    "world-collapsed triangle left staging residue",
)


# Independent-review follow-up: argparse failures use the same bounded,
# printable, redacted one-line diagnostic boundary as rendering failures.
argument_output = root / "hostile-argument.png"
hostile_argument = (
    "not-an-integer\n"
    "token=SAFE_GENERIC_SENTINEL\t"
    + "x" * 2_048
)
argument_result = invoke(
    source_template,
    argument_output,
    "25",
    "--size", hostile_argument,
    "--splat-radius", "1",
    "--min-coverage", "0",
)
check_prefixed_failure(argument_result, "hostile argparse value")
if argument_result is not None:
    argument_lines = argument_result.stderr.splitlines()
    check(
        len(argument_result.stderr.encode("utf-8")) <= 512,
        "hostile argparse value exceeded 512 diagnostic bytes",
    )
    check(
        len(argument_lines) == 1
        and all(character.isprintable() for character in argument_lines[0]),
        "hostile argparse value did not emit exactly one printable line",
    )
    lowered_argument_diagnostic = argument_result.stderr.lower()
    check(
        "safe_generic_sentinel" not in lowered_argument_diagnostic
        and "token=" not in lowered_argument_diagnostic
        and "not-an-integer" not in lowered_argument_diagnostic,
        "hostile argparse value was echoed instead of redacted",
    )
check(not argument_output.exists(), "hostile argparse value left a PNG")


# Independent-review follow-up: when publication and exact temporary-file
# cleanup both fail, the cleanup failure is part of the bounded diagnostic.
# The prior ordinary PNG remains untouched.  Earlier link cases independently
# pin preservation of symlink/hardlink referents.
cleanup_case = root / "persistent-cleanup-failure"
cleanup_case.mkdir()
cleanup_source = cleanup_case / "source.glb"
cleanup_output = cleanup_case / "output.png"
shutil.copyfile(source_template, cleanup_source)
cleanup_prior = ordinary_output.read_bytes()
cleanup_output.write_bytes(cleanup_prior)
cleanup_wrapper = root / "persistent-cleanup-probe.py"
cleanup_report = root / "persistent-cleanup-report.json"
cleanup_wrapper.write_text(
    "from __future__ import annotations\n"
    "import json\n"
    "import os\n"
    "import runpy\n"
    "import stat\n"
    "import sys\n"
    "from pathlib import Path\n"
    "script = Path(sys.argv[1]).resolve()\n"
    "source = Path(sys.argv[2]).resolve()\n"
    "output = Path(sys.argv[3]).resolve()\n"
    "report = Path(sys.argv[4]).resolve()\n"
    "original_replace = os.replace\n"
    "original_unlink = os.unlink\n"
    "original_remove = os.remove\n"
    "original_path_unlink = Path.unlink\n"
    "captured_stage: Path | None = None\n"
    "captured_identity: tuple[int, int] | None = None\n"
    "capture_calls = 0\n"
    "publication_faults = 0\n"
    "cleanup_faults = 0\n"
    "def is_captured_stage(value: object) -> bool:\n"
    "    try:\n"
    "        candidate = Path(os.path.abspath(os.fspath(value)))\n"
    "    except TypeError:\n"
    "        return False\n"
    "    if captured_stage is None or captured_identity is None:\n"
    "        return False\n"
    "    try:\n"
    "        status = candidate.lstat()\n"
    "    except OSError:\n"
    "        return False\n"
    "    return (candidate == captured_stage and "
    "(status.st_dev, status.st_ino) == captured_identity)\n"
    "def fail_replace(stage: object, final: object, *_args: object, **_kwargs: object) -> None:\n"
    "    global captured_stage, captured_identity, capture_calls, publication_faults\n"
    "    if Path(os.path.abspath(os.fspath(final))) != output:\n"
    "        raise AssertionError('publication target was not the requested output')\n"
    "    candidate = Path(os.path.abspath(os.fspath(stage)))\n"
    "    status = candidate.lstat()\n"
    "    captured_stage = candidate\n"
    "    captured_identity = (status.st_dev, status.st_ino)\n"
    "    capture_calls += 1\n"
    "    publication_faults += 1\n"
    "    raise OSError('SAFE_GENERIC_SENTINEL publication failure')\n"
    "def fail_unlink(path: object, *args: object, **kwargs: object) -> None:\n"
    "    global cleanup_faults\n"
    "    if is_captured_stage(path):\n"
    "        cleanup_faults += 1\n"
    "        raise OSError('SAFE_GENERIC_SENTINEL temporary cleanup failure')\n"
    "    original_unlink(path, *args, **kwargs)\n"
    "def fail_remove(path: object, *args: object, **kwargs: object) -> None:\n"
    "    global cleanup_faults\n"
    "    if is_captured_stage(path):\n"
    "        cleanup_faults += 1\n"
    "        raise OSError('SAFE_GENERIC_SENTINEL temporary cleanup failure')\n"
    "    original_remove(path, *args, **kwargs)\n"
    "def fail_path_unlink(self: Path, *args: object, **kwargs: object) -> None:\n"
    "    global cleanup_faults\n"
    "    if is_captured_stage(self):\n"
    "        cleanup_faults += 1\n"
    "        raise OSError('SAFE_GENERIC_SENTINEL temporary cleanup failure')\n"
    "    original_path_unlink(self, *args, **kwargs)\n"
    "sys.path.insert(0, str(script.parent))\n"
    "os.replace = fail_replace\n"
    "os.unlink = fail_unlink\n"
    "os.remove = fail_remove\n"
    "Path.unlink = fail_path_unlink\n"
    "try:\n"
    "    sys.argv = [str(script), str(source), str(output), '25', "
    "'--size', '64', '--splat-radius', '1', '--min-coverage', '0']\n"
    "    try:\n"
    "        runpy.run_path(str(script), run_name='__main__')\n"
    "    except SystemExit as exc:\n"
    "        result = exc.code if isinstance(exc.code, int) else 1\n"
    "    else:\n"
    "        result = 0\n"
    "finally:\n"
    "    os.replace = original_replace\n"
    "    os.unlink = original_unlink\n"
    "    os.remove = original_remove\n"
    "    Path.unlink = original_path_unlink\n"
    "    sys.path.pop(0)\n"
    "    stage_status = None\n"
    "    if captured_stage is not None:\n"
    "        try:\n"
    "            observed = captured_stage.lstat()\n"
    "        except OSError:\n"
    "            pass\n"
    "        else:\n"
    "            stage_status = {\n"
    "                'device': observed.st_dev,\n"
    "                'inode': observed.st_ino,\n"
    "                'regular': stat.S_ISREG(observed.st_mode),\n"
    "                'links': observed.st_nlink,\n"
    "            }\n"
    "    report.write_text(json.dumps({\n"
    "        'stage_path': str(captured_stage) if captured_stage is not None else None,\n"
    "        'stage_identity': list(captured_identity) if captured_identity is not None else None,\n"
    "        'stage_status': stage_status,\n"
    "        'capture_calls': capture_calls,\n"
    "        'publication_faults': publication_faults,\n"
    "        'cleanup_faults': cleanup_faults,\n"
    "    }, sort_keys=True), encoding='utf-8')\n"
    "raise SystemExit(result)\n",
    encoding="utf-8",
)
try:
    cleanup_result = subprocess.run(
        [
            python,
            str(cleanup_wrapper),
            str(script),
            str(cleanup_source),
            str(cleanup_output),
            str(cleanup_report),
        ],
        check=False,
        capture_output=True,
        text=True,
        env=environment,
        timeout=2.0,
    )
except subprocess.TimeoutExpired:
    cleanup_result = None
check_prefixed_failure(cleanup_result, "persistent temporary cleanup failure")
if cleanup_result is not None:
    check(
        len(cleanup_result.stderr.encode("utf-8")) <= 512,
        "persistent cleanup diagnostic exceeded 512 bytes",
    )
    check(
        "cleanup" in cleanup_result.stderr.lower(),
        "persistent cleanup failure was omitted from the diagnostic",
    )
    cleanup_lines = cleanup_result.stderr.splitlines()
    check(
        len(cleanup_lines) == 1
        and all(character.isprintable() for character in cleanup_lines[0]),
        "persistent cleanup failure did not emit one printable diagnostic",
    )
check(
    cleanup_output.read_bytes() == cleanup_prior,
    "persistent cleanup failure changed the prior PNG",
)
if cleanup_report.exists():
    cleanup_probe = json.loads(cleanup_report.read_text(encoding="utf-8"))
    stage_value = cleanup_probe.get("stage_path")
    captured_stage = Path(stage_value) if isinstance(stage_value, str) else None
    stage_identity = cleanup_probe.get("stage_identity")
    stage_status = cleanup_probe.get("stage_status")
    check(
        cleanup_probe.get("capture_calls") == 1,
        "persistent cleanup probe did not capture exactly one staging role",
    )
    check(
        cleanup_probe.get("publication_faults") == 1,
        "persistent cleanup probe did not fault the captured publication boundary",
    )
    check(
        cleanup_probe.get("cleanup_faults", 0) >= 1,
        "persistent cleanup probe did not fault cleanup of the captured staging identity",
    )
    check(
        captured_stage is not None and captured_stage.parent == cleanup_case,
        "persistent cleanup probe captured a stage outside the output directory",
    )
    check(
        isinstance(stage_identity, list)
        and len(stage_identity) == 2
        and isinstance(stage_status, dict)
        and [stage_status.get("device"), stage_status.get("inode")] == stage_identity
        and stage_status.get("regular") is True
        and stage_status.get("links") == 1,
        "persistent cleanup residue did not preserve the captured regular identity",
    )
    cleanup_residue = [
        path
        for path in cleanup_case.iterdir()
        if path not in {cleanup_source, cleanup_output}
    ]
    check(
        captured_stage is not None and cleanup_residue == [captured_stage],
        "persistent cleanup residue was not exactly the captured staging path",
    )
else:
    failures.append("persistent cleanup probe omitted its runtime dataflow report")


def python_39_public_cli() -> None:
    candidate = Path("/usr/bin/python3")
    if not candidate.is_file():
        print(
            "glb-silhouette Python 3.9 public CLI: skipped "
            "(/usr/bin/python3 is not Python 3.9)",
            file=sys.stderr,
        )
        return
    probe = subprocess.run(
        [
            str(candidate),
            "-B",
            "-c",
            "import sys; print(int(sys.version_info[:2] == (3, 9)))",
        ],
        check=False,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        timeout=3.0,
    )
    if probe.returncode != 0 or probe.stdout != b"1\n":
        print(
            "glb-silhouette Python 3.9 public CLI: skipped "
            "(/usr/bin/python3 is not Python 3.9)",
            file=sys.stderr,
        )
        return

    output = root / "python39-public.png"
    result = subprocess.run(
        [
            str(candidate),
            "-B",
            str(script),
            str(source_template),
            str(output),
            "25",
            "--size",
            "64",
            "--splat-radius",
            "4",
            "--min-coverage",
            "0",
        ],
        check=False,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        timeout=3.0,
    )
    check(result.returncode == 0, "Python 3.9 public CLI returned failure")
    check(result.stderr == b"", "Python 3.9 public CLI wrote a diagnostic")
    check(
        output.is_file() and output.read_bytes().startswith(b"\x89PNG\r\n\x1a\n"),
        "Python 3.9 public CLI did not publish a PNG",
    )
    check(
        result.stdout.endswith(b"\n")
        and result.stdout.count(b"\n") == 1
        and len(result.stdout) <= 512,
        "Python 3.9 public CLI success record was not one bounded line",
    )


python_39_public_cli()


if failures:
    for failure in failures:
        print(f"glb-silhouette hardening: {failure}", file=sys.stderr)
    raise SystemExit(1)
PY

echo "glb-silhouette test: pass"
