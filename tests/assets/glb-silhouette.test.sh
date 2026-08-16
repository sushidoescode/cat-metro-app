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
  "$tmp/two-regions.glb" <<'PY'
import re
import struct
import sys
import zlib
from pathlib import Path

png_path = Path(sys.argv[1])
stdout = Path(sys.argv[2]).read_text(encoding="utf-8")
source = Path(sys.argv[3])
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
# Break caught: the renderer reports only primitive zero or lies about evidence density.
assert int(vertices) == 16
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

echo "glb-silhouette test: pass"
