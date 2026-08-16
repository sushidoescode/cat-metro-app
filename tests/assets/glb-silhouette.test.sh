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
assert metrics["vertices"] == 16
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
rg -n 'glb-silhouette: coverage [0-9.]+ below 0\.100000' "$tmp/sparse.err"

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
  if ! rg -q '^glb-silhouette:' "$stderr"; then
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

PYTHONDONTWRITEBYTECODE=1 python3 - \
  "$silhouette_script" "$tmp" <<'PY'
from __future__ import annotations

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
python = sys.executable
environment = {**os.environ, "PYTHONDONTWRITEBYTECODE": "1"}
failures: list[str] = []
maximum_source_bytes = 512 * 1024 * 1024
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
    assert count == 8
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
        "min": [-1.0, -1.0, -1.0],
        "max": [1.0, 1.0, 1.0],
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
    assert position_count == 10_008
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


# Mutation caught: every index is valid but zero, so raw POSITION splatting
# produces the same acceptable PNG even though all triangle topology is gone.
zeroed_source = root / "zeroed-indices.glb"
build_zeroed_indices(source_template, zeroed_source)
check(
    hashlib.sha256(zeroed_source.read_bytes()).digest()
    != hashlib.sha256(source_template.read_bytes()).digest(),
    "zero-index mutation did not change the GLB",
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


if failures:
    for failure in failures:
        print(f"glb-silhouette hardening: {failure}", file=sys.stderr)
    raise SystemExit(1)
PY

echo "glb-silhouette test: pass"
