#!/usr/bin/env python3
"""Measure raw screenshot parity without resizing or colour correction."""

from __future__ import annotations

import argparse
import binascii
import json
import math
import os
import struct
import sys
import tempfile
import zlib
from dataclasses import dataclass
from pathlib import Path


PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
MAX_FILE_BYTES = 512 * 1024 * 1024
MAX_PIXELS = 100_000_000


class ParityError(ValueError):
    """Raised when inputs cannot produce an honest parity measurement."""


@dataclass(frozen=True)
class PngImage:
    width: int
    height: int
    rgb: bytes
    source_color_type: str
    has_srgb_chunk: bool
    has_gamma_chunk: bool
    has_icc_profile: bool


def _read_png(path: Path) -> PngImage:
    try:
        data = path.read_bytes()
    except OSError as exc:
        raise ParityError(f"cannot read {path}: {exc}") from exc
    if len(data) > MAX_FILE_BYTES:
        raise ParityError(f"{path}: PNG exceeds the {MAX_FILE_BYTES}-byte limit")
    if not data.startswith(PNG_SIGNATURE):
        raise ParityError(f"{path}: invalid PNG signature")

    offset = len(PNG_SIGNATURE)
    header = None
    compressed = bytearray()
    ended = False
    has_srgb = False
    has_gamma = False
    has_icc = False
    chunk_number = 0

    while offset < len(data):
        if len(data) - offset < 12:
            raise ParityError(f"{path}: truncated PNG chunk")
        length = struct.unpack_from(">I", data, offset)[0]
        kind = data[offset + 4:offset + 8]
        payload_start = offset + 8
        payload_end = payload_start + length
        chunk_end = payload_end + 4
        if chunk_end > len(data):
            raise ParityError(f"{path}: truncated {kind!r} chunk")
        payload = data[payload_start:payload_end]
        expected_crc = struct.unpack_from(">I", data, payload_end)[0]
        actual_crc = binascii.crc32(kind + payload) & 0xFFFFFFFF
        if actual_crc != expected_crc:
            raise ParityError(f"{path}: bad CRC in {kind.decode('ascii', 'replace')} chunk")
        offset = chunk_end

        if kind == b"IHDR":
            if chunk_number != 0 or header is not None or length != 13:
                raise ParityError(f"{path}: invalid IHDR placement or length")
            header = struct.unpack(">2I5B", payload)
        elif kind == b"IDAT":
            if header is None:
                raise ParityError(f"{path}: IDAT appears before IHDR")
            compressed.extend(payload)
        elif kind == b"sRGB":
            has_srgb = True
        elif kind == b"gAMA":
            has_gamma = True
        elif kind == b"iCCP":
            has_icc = True
        elif kind == b"tRNS":
            raise ParityError(f"{path}: PNG transparency is not allowed")
        elif kind == b"IEND":
            if length != 0:
                raise ParityError(f"{path}: IEND must be empty")
            ended = True
            if offset != len(data):
                raise ParityError(f"{path}: data follows IEND")
            break
        elif kind not in (b"PLTE",) and not (kind[0] & 0x20):
            raise ParityError(
                f"{path}: unsupported critical PNG chunk {kind.decode('ascii', 'replace')}"
            )
        chunk_number += 1

    if header is None or not ended or not compressed:
        raise ParityError(f"{path}: PNG requires IHDR, IDAT, and IEND")

    width, height, depth, color_type, compression, filter_method, interlace = header
    if width <= 0 or height <= 0 or width * height > MAX_PIXELS:
        raise ParityError(f"{path}: invalid or excessive PNG dimensions {width}x{height}")
    if depth != 8 or color_type not in (2, 6):
        raise ParityError(f"{path}: only 8-bit RGB or RGBA PNGs are supported")
    if compression != 0 or filter_method != 0 or interlace != 0:
        raise ParityError(f"{path}: unsupported PNG compression, filter, or interlace mode")

    channels = 3 if color_type == 2 else 4
    stride = width * channels
    expected_size = height * (stride + 1)
    inflater = zlib.decompressobj()
    try:
        raw = inflater.decompress(bytes(compressed), expected_size + 1)
    except zlib.error as exc:
        raise ParityError(f"{path}: invalid IDAT stream: {exc}") from exc
    if len(raw) > expected_size or inflater.unconsumed_tail:
        raise ParityError(f"{path}: decompressed raster exceeds IHDR dimensions")
    if not inflater.eof or inflater.unused_data:
        raise ParityError(f"{path}: truncated or trailing IDAT stream")
    if len(raw) != expected_size:
        raise ParityError(
            f"{path}: raster has {len(raw)} bytes; expected {expected_size}"
        )

    rgb = bytearray(width * height * 3)
    previous = bytearray(stride)
    source_offset = 0
    output_offset = 0
    for _ in range(height):
        filter_type = raw[source_offset]
        source_offset += 1
        scanline = bytearray(raw[source_offset:source_offset + stride])
        source_offset += stride
        if filter_type > 4:
            raise ParityError(f"{path}: unsupported PNG row filter {filter_type}")
        for index in range(stride):
            left = scanline[index - channels] if index >= channels else 0
            above = previous[index]
            upper_left = previous[index - channels] if index >= channels else 0
            if filter_type == 1:
                predictor = left
            elif filter_type == 2:
                predictor = above
            elif filter_type == 3:
                predictor = (left + above) // 2
            elif filter_type == 4:
                predictor = _paeth(left, above, upper_left)
            else:
                predictor = 0
            scanline[index] = (scanline[index] + predictor) & 0xFF

        if channels == 3:
            rgb[output_offset:output_offset + width * 3] = scanline
            output_offset += width * 3
        else:
            for pixel in range(width):
                start = pixel * 4
                if scanline[start + 3] != 255:
                    raise ParityError(f"{path}: RGBA input contains non-opaque alpha")
                rgb[output_offset:output_offset + 3] = scanline[start:start + 3]
                output_offset += 3
        previous = scanline

    return PngImage(
        width,
        height,
        bytes(rgb),
        "RGB" if color_type == 2 else "RGBA-opaque",
        has_srgb,
        has_gamma,
        has_icc,
    )


def _paeth(left: int, above: int, upper_left: int) -> int:
    estimate = left + above - upper_left
    distance_left = abs(estimate - left)
    distance_above = abs(estimate - above)
    distance_upper_left = abs(estimate - upper_left)
    if distance_left <= distance_above and distance_left <= distance_upper_left:
        return left
    if distance_above <= distance_upper_left:
        return above
    return upper_left


def _safe_area(value: str) -> tuple[float, float, float, float]:
    parts = value.split(",")
    if len(parts) != 4:
        raise argparse.ArgumentTypeError("safe area must be X,Y,W,H")
    try:
        rectangle = tuple(float(part) for part in parts)
    except ValueError as exc:
        raise argparse.ArgumentTypeError("safe area must contain four numbers") from exc
    if not all(math.isfinite(part) for part in rectangle):
        raise argparse.ArgumentTypeError("safe area values must be finite")
    return rectangle  # type: ignore[return-value]


def _validated_insets(
    rectangle: tuple[float, float, float, float],
    width: int,
    height: int,
    label: str,
) -> dict[str, float]:
    x, y, safe_width, safe_height = rectangle
    tolerance = 0.000001
    if (
        x < 0
        or y < 0
        or safe_width <= 0
        or safe_height <= 0
        or x + safe_width > width + tolerance
        or y + safe_height > height + tolerance
    ):
        raise ParityError(f"{label} safe area lies outside its {width}x{height} frame")
    return {
        "top": height - (y + safe_height),
        "right": width - (x + safe_width),
        "bottom": y,
        "left": x,
    }


def _rounded(value: float) -> float:
    rounded = round(value, 6)
    return 0.0 if abs(rounded) < 0.0000005 else rounded


def _png_chunk(kind: bytes, payload: bytes) -> bytes:
    return (
        struct.pack(">I", len(payload))
        + kind
        + payload
        + struct.pack(">I", binascii.crc32(kind + payload) & 0xFFFFFFFF)
    )


def _write_diff(path: Path, width: int, height: int, rgb: bytes, inputs: list[Path]) -> None:
    output_absolute = os.path.abspath(path)
    for source in inputs:
        if output_absolute == os.path.abspath(source):
            raise ParityError("diff output must not overwrite an input PNG")
        if path.exists():
            try:
                if os.path.samefile(path, source):
                    raise ParityError("diff output must not alias an input PNG")
            except FileNotFoundError:
                pass
    if not path.parent.is_dir():
        raise ParityError(f"diff output directory does not exist: {path.parent}")
    if path.is_symlink():
        raise ParityError("diff output must not be a symbolic link")

    stride = width * 3
    raster = b"".join(
        b"\0" + rgb[row * stride:(row + 1) * stride] for row in range(height)
    )
    header = struct.pack(">2I5B", width, height, 8, 2, 0, 0, 0)
    payload = (
        PNG_SIGNATURE
        + _png_chunk(b"IHDR", header)
        + _png_chunk(b"IDAT", zlib.compress(raster, 9))
        + _png_chunk(b"IEND", b"")
    )
    descriptor = -1
    staged = None
    try:
        descriptor, staged_name = tempfile.mkstemp(
            prefix=".capture-parity-", suffix=".tmp", dir=path.parent
        )
        staged = Path(staged_name)
        with os.fdopen(descriptor, "wb", closefd=True) as handle:
            descriptor = -1
            handle.write(payload)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(staged, path)
        staged = None
    finally:
        if descriptor >= 0:
            os.close(descriptor)
        if staged is not None:
            staged.unlink(missing_ok=True)


def _measure(
    rig: PngImage,
    device: PngImage,
    rig_safe_area: tuple[float, float, float, float],
    device_safe_area: tuple[float, float, float, float],
) -> tuple[dict[str, object], bytes]:
    if (rig.width, rig.height) != (device.width, device.height):
        raise ParityError(
            "rig and device PNG dimensions differ; render the rig at the device size"
        )

    sums = [0, 0, 0]
    maxima = [0, 0, 0]
    difference = bytearray(len(rig.rgb))
    for index, (rig_value, device_value) in enumerate(zip(rig.rgb, device.rgb)):
        delta = abs(rig_value - device_value)
        channel = index % 3
        sums[channel] += delta
        maxima[channel] = max(maxima[channel], delta)
        difference[index] = delta

    pixels = rig.width * rig.height
    rig_insets = _validated_insets(rig_safe_area, rig.width, rig.height, "rig")
    device_insets = _validated_insets(
        device_safe_area, device.width, device.height, "device"
    )
    drift = {
        edge: _rounded(device_insets[edge] - rig_insets[edge])
        for edge in ("top", "right", "bottom", "left")
    }
    max_drift = _rounded(max(abs(value) for value in drift.values()))

    report: dict[str, object] = {
        "sample_space": "encoded-rgb8",
        "size": {"width": rig.width, "height": rig.height, "pixels": pixels},
        "delta": {
            "mean": {
                "r": _rounded(sums[0] / pixels),
                "g": _rounded(sums[1] / pixels),
                "b": _rounded(sums[2] / pixels),
                "all": _rounded(sum(sums) / (pixels * 3)),
            },
            "max": {
                "r": maxima[0],
                "g": maxima[1],
                "b": maxima[2],
                "all": max(maxima),
            },
        },
        "safe_area": {
            "device_minus_rig_px": drift,
            "max_abs_drift_px": max_drift,
            "drift_detected": max_drift > 0,
        },
        "png": {
            "rig_source_color_type": rig.source_color_type,
            "device_source_color_type": device.source_color_type,
            "color_metadata": {
                "rig": {
                    "srgb": rig.has_srgb_chunk,
                    "gamma": rig.has_gamma_chunk,
                    "icc": rig.has_icc_profile,
                },
                "device": {
                    "srgb": device.has_srgb_chunk,
                    "gamma": device.has_gamma_chunk,
                    "icc": device.has_icc_profile,
                },
            },
        },
    }
    return report, bytes(difference)


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Compare same-size rig/device PNGs in encoded RGB8 and report safe-area drift. "
            "Safe areas use Unity's bottom-left-origin X,Y,W,H pixel convention."
        )
    )
    parser.add_argument("rig_png", type=Path)
    parser.add_argument("device_png", type=Path)
    parser.add_argument("--rig-safe-area", required=True, type=_safe_area, metavar="X,Y,W,H")
    parser.add_argument(
        "--device-safe-area", required=True, type=_safe_area, metavar="X,Y,W,H"
    )
    parser.add_argument("--diff-output", type=Path, metavar="PNG")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        rig = _read_png(args.rig_png)
        device = _read_png(args.device_png)
        report, difference = _measure(
            rig, device, args.rig_safe_area, args.device_safe_area
        )
        if args.diff_output is not None:
            _write_diff(
                args.diff_output,
                rig.width,
                rig.height,
                difference,
                [args.rig_png, args.device_png],
            )
    except (ParityError, OSError) as exc:
        print(f"capture-parity: {exc}", file=sys.stderr)
        return 2

    json.dump(report, sys.stdout, indent=2, sort_keys=True)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
