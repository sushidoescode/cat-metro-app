#!/usr/bin/env bash
set -euo pipefail

repo=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)
tool=$repo/scripts/capture-parity.py
tmp=$(mktemp -d "${TMPDIR:-/tmp}/capture-parity.XXXXXXXX")
trap 'rm -rf -- "$tmp"' EXIT

python3 - "$tmp" <<'PY'
import binascii
import struct
import sys
import zlib
from pathlib import Path

out = Path(sys.argv[1])

def chunk(kind, payload):
    return (struct.pack(">I", len(payload)) + kind + payload
            + struct.pack(">I", binascii.crc32(kind + payload) & 0xffffffff))

def png(name, width, height, color_type, pixels, before_idat=b""):
    channels = 3 if color_type == 2 else 4
    assert len(pixels) == width * height * channels
    rows = b"".join(
        b"\0" + pixels[y * width * channels:(y + 1) * width * channels]
        for y in range(height)
    )
    payload = (b"\x89PNG\r\n\x1a\n"
               + chunk(b"IHDR", struct.pack(">2I5B", width, height, 8,
                                              color_type, 0, 0, 0))
               + before_idat
               + chunk(b"IDAT", zlib.compress(rows, 9))
               + chunk(b"IEND", b""))
    (out / name).write_bytes(payload)

png("rig.png", 2, 1, 2, bytes((10, 20, 30, 100, 110, 120)))
png("device.png", 2, 1, 2, bytes((13, 18, 30, 90, 130, 125)))
png("opaque-rgba.png", 2, 1, 6,
    bytes((10, 20, 30, 255, 100, 110, 120, 255)))
png("transparent.png", 2, 1, 6,
    bytes((10, 20, 30, 254, 100, 110, 120, 255)))
png("transparent-rgb.png", 2, 1, 2,
    bytes((10, 20, 30, 100, 110, 120)),
    chunk(b"tRNS", struct.pack(">3H", 10, 20, 30)))
png("wide.png", 3, 1, 2, bytes((0, 0, 0) * 3))
PY

python3 "$tool" "$tmp/rig.png" "$tmp/device.png" \
  --rig-safe-area 0,0,2,1 --device-safe-area 0.25,0,1.5,1 \
  --diff-output "$tmp/diff.png" >"$tmp/report.json"

python3 - "$tmp/report.json" <<'PY'
import json
import sys

report = json.load(open(sys.argv[1], encoding="utf-8"))
assert report["sample_space"] == "encoded-rgb8"
assert report["size"] == {"height": 1, "pixels": 2, "width": 2}
assert report["delta"]["mean"] == {
    "all": 6.666667, "b": 2.5, "g": 11.0, "r": 6.5,
}
assert report["delta"]["max"] == {"all": 20, "b": 5, "g": 20, "r": 10}
assert report["safe_area"]["device_minus_rig_px"] == {
    "bottom": 0.0, "left": 0.25, "right": 0.25, "top": 0.0,
}
assert report["safe_area"]["max_abs_drift_px"] == 0.25
assert report["safe_area"]["drift_detected"] is True
PY

# Opaque RGBA is a valid screenshot input and compares as RGB.
python3 "$tool" "$tmp/rig.png" "$tmp/opaque-rgba.png" \
  --rig-safe-area 0,0,2,1 --device-safe-area 0,0,2,1 >"$tmp/rgba.json"
python3 - "$tmp/rgba.json" <<'PY'
import json
import sys

report = json.load(open(sys.argv[1], encoding="utf-8"))
assert report["delta"]["max"]["all"] == 0
assert report["safe_area"]["drift_detected"] is False
PY

# Re-running the same measurement and diff must be byte deterministic.
python3 "$tool" "$tmp/rig.png" "$tmp/device.png" \
  --rig-safe-area 0,0,2,1 --device-safe-area 0.25,0,1.5,1 \
  --diff-output "$tmp/diff-2.png" >"$tmp/report-2.json"
cmp "$tmp/report.json" "$tmp/report-2.json"
cmp "$tmp/diff.png" "$tmp/diff-2.png"

if python3 "$tool" "$tmp/rig.png" "$tmp/transparent.png" \
  --rig-safe-area 0,0,2,1 --device-safe-area 0,0,2,1 >/dev/null 2>&1; then
  echo "capture-parity test: transparent input was accepted" >&2
  exit 1
fi
if python3 "$tool" "$tmp/rig.png" "$tmp/transparent-rgb.png" \
  --rig-safe-area 0,0,2,1 --device-safe-area 0,0,2,1 >/dev/null 2>&1; then
  echo "capture-parity test: RGB tRNS transparency was accepted" >&2
  exit 1
fi
if python3 "$tool" "$tmp/rig.png" "$tmp/wide.png" \
  --rig-safe-area 0,0,2,1 --device-safe-area 0,0,3,1 >/dev/null 2>&1; then
  echo "capture-parity test: mismatched sizes were accepted" >&2
  exit 1
fi
if python3 "$tool" "$tmp/rig.png" "$tmp/device.png" \
  --rig-safe-area 0,0,2,1 >/dev/null 2>&1; then
  echo "capture-parity test: missing device safe area was accepted" >&2
  exit 1
fi
if python3 "$tool" "$tmp/rig.png" "$tmp/device.png" \
  --rig-safe-area 0,0,2,1 --device-safe-area 0,0,2,1 \
  --diff-output "$tmp/rig.png" >/dev/null 2>&1; then
  echo "capture-parity test: input/output alias was accepted" >&2
  exit 1
fi

mkdir "$tmp/not-a-png"
set +e
python3 "$tool" "$tmp/rig.png" "$tmp/device.png" \
  --rig-safe-area 0,0,2,1 --device-safe-area 0,0,2,1 \
  --diff-output "$tmp/not-a-png" >"$tmp/output-error.stdout" \
  2>"$tmp/output-error.stderr"
output_status=$?
set -e
if [ "$output_status" -ne 2 ] || grep -q 'Traceback' "$tmp/output-error.stderr"; then
  echo "capture-parity test: output filesystem error did not use stable exit 2" >&2
  exit 1
fi

echo "capture-parity test: OK"
