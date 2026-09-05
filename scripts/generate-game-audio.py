#!/usr/bin/env python3
"""Generate Cat Metro's small, project-original procedural SFX set.

The generator reads no audio input and uses only Python's standard library.  Every
noise source comes from the fixed XorShift32 implementation below, making each
named seed repeatable without depending on Python's random module.
"""

from __future__ import annotations

import argparse
import hashlib
import math
from dataclasses import dataclass
from pathlib import Path
import struct
import sys
import wave


SAMPLE_RATE = 44_100
CHANNELS = 1
SAMPLE_WIDTH_BYTES = 2
PAYLOAD_LIMIT_BYTES = 2_000_000
GENERATOR_VERSION = "cat-metro-audio-v1"
REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT_ROOT = REPO_ROOT / "unity/Assets/Resources/Audio/CatMetro"


class XorShift32:
    """Tiny explicitly specified PRNG used only as a procedural noise source."""

    def __init__(self, seed: int) -> None:
        self._state = seed & 0xFFFFFFFF
        if self._state == 0:
            self._state = 0x6D2B79F5

    def signed(self) -> float:
        value = self._state
        value ^= (value << 13) & 0xFFFFFFFF
        value ^= value >> 17
        value ^= (value << 5) & 0xFFFFFFFF
        self._state = value & 0xFFFFFFFF
        return ((self._state >> 8) / 8_388_607.5) - 1.0


@dataclass(frozen=True)
class SoundSpec:
    filename: str
    role: str
    duration_ms: int
    seed: int
    peak_dbfs: float
    recipe: str
    synth: str

    @property
    def sample_count(self) -> int:
        return self.duration_ms * SAMPLE_RATE // 1_000


SPECS = (
    SoundSpec(
        "wooden-tap.wav",
        "Buttons",
        100,
        0xC47A2301,
        -8.0,
        "Filtered-noise fingertip impulse exciting three short, damped wood modes.",
        "wooden_tap",
    ),
    SoundSpec(
        "switch-clunk.wav",
        "Track switch",
        220,
        0xC47A2302,
        -7.0,
        "Two offset low wooden impacts with a quiet, filtered lever scrape between them.",
        "switch_clunk",
    ),
    SoundSpec(
        "train-chuff-loop.wav",
        "Train moving loop",
        1_200,
        0xC47A2303,
        -11.0,
        "Four seamless low-passed noise puffs with soft 60 Hz body pulses.",
        "train_chuff_loop",
    ),
    SoundSpec(
        "delivery-chime.wav",
        "Correct delivery",
        550,
        0xC47A2304,
        -9.0,
        "G4 then D5, struck as warm inharmonic wooden-bar resonators with soft mallets.",
        "delivery_chime",
    ),
    SoundSpec(
        "wrong-station-thud.wav",
        "Wrong station / failure",
        280,
        0xC47A2305,
        -10.0,
        "Felted low-noise impulse exciting four quickly damped desk-and-wood modes.",
        "wrong_station_thud",
    ),
    SoundSpec(
        "celebrate-flourish.wav",
        "Level celebration",
        760,
        0xC47A2306,
        -9.0,
        "Three tiny wooden-bar notes (G4, C5, E5) with staggered soft-mallet attacks.",
        "celebrate_flourish",
    ),
    SoundSpec(
        "purchase-success.wav",
        "Purchase confirmed",
        680,
        0xC47A2307,
        -9.0,
        "A settled A4, E5, A5 wooden-bar cadence, distinct from delivery and celebration.",
        "purchase_success",
    ),
)


def _blank(spec: SoundSpec) -> list[float]:
    return [0.0] * spec.sample_count


def _seconds_to_index(seconds: float) -> int:
    return int(round(seconds * SAMPLE_RATE))


def _add_modes(
    samples: list[float],
    start_seconds: float,
    modes: tuple[tuple[float, float, float], ...],
    gain: float,
) -> None:
    start = _seconds_to_index(start_seconds)
    attack_seconds = 0.0011
    for index in range(start, len(samples)):
        elapsed = (index - start) / SAMPLE_RATE
        attack = 1.0 - math.exp(-elapsed / attack_seconds)
        value = 0.0
        for frequency, amplitude, decay_seconds in modes:
            value += amplitude * math.exp(-elapsed / decay_seconds) * math.sin(
                math.tau * frequency * elapsed
            )
        samples[index] += gain * attack * value


def _add_noise_burst(
    samples: list[float],
    start_seconds: float,
    duration_seconds: float,
    seed: int,
    gain: float,
    smoothing: float,
    attack_seconds: float,
    decay_seconds: float,
) -> None:
    start = _seconds_to_index(start_seconds)
    count = min(_seconds_to_index(duration_seconds), len(samples) - start)
    source = XorShift32(seed)
    filtered = 0.0
    for offset in range(max(0, count)):
        elapsed = offset / SAMPLE_RATE
        filtered += smoothing * (source.signed() - filtered)
        attack = 1.0 - math.exp(-elapsed / attack_seconds)
        envelope = attack * math.exp(-elapsed / decay_seconds)
        samples[start + offset] += gain * envelope * filtered


def _add_scrape(
    samples: list[float],
    start_seconds: float,
    duration_seconds: float,
    seed: int,
    gain: float,
) -> None:
    start = _seconds_to_index(start_seconds)
    count = min(_seconds_to_index(duration_seconds), len(samples) - start)
    source = XorShift32(seed)
    filtered = 0.0
    for offset in range(max(0, count)):
        phase = offset / max(1, count - 1)
        window = math.sin(math.pi * phase) ** 2
        filtered += 0.075 * (source.signed() - filtered)
        samples[start + offset] += gain * window * filtered


def _add_wooden_bar(
    samples: list[float],
    start_seconds: float,
    fundamental: float,
    seed: int,
    gain: float,
) -> None:
    ratios = (1.0, 2.73, 5.12, 8.41)
    amplitudes = (1.0, 0.19, 0.065, 0.022)
    decays = (0.34, 0.14, 0.070, 0.036)
    modes = tuple(
        (fundamental * ratio, amplitude, decay)
        for ratio, amplitude, decay in zip(ratios, amplitudes, decays)
    )
    _add_modes(samples, start_seconds, modes, gain)
    _add_noise_burst(
        samples,
        start_seconds,
        0.030,
        seed,
        gain * 0.22,
        0.12,
        0.00045,
        0.010,
    )


def _circular_box_filter(values: list[float], radius: int) -> list[float]:
    if radius <= 0:
        return list(values)
    extended = values[-radius:] + values + values[:radius]
    width = radius * 2 + 1
    running = sum(extended[:width])
    result = [running / width]
    for start in range(1, len(values)):
        running += extended[start + width - 1] - extended[start - 1]
        result.append(running / width)
    return result


def _wooden_tap(spec: SoundSpec) -> list[float]:
    samples = _blank(spec)
    _add_modes(
        samples,
        0.0,
        ((305.0, 1.0, 0.035), (548.0, 0.42, 0.026), (913.0, 0.16, 0.017)),
        0.82,
    )
    _add_noise_burst(samples, 0.0, 0.045, spec.seed, 0.44, 0.12, 0.00035, 0.012)
    return samples


def _switch_clunk(spec: SoundSpec) -> list[float]:
    samples = _blank(spec)
    _add_modes(
        samples,
        0.0,
        ((91.0, 1.0, 0.085), (146.0, 0.55, 0.065), (284.0, 0.22, 0.042)),
        0.65,
    )
    _add_noise_burst(samples, 0.0, 0.070, spec.seed, 0.25, 0.065, 0.0005, 0.020)
    _add_scrape(samples, 0.035, 0.090, spec.seed ^ 0x51A9E201, 0.12)
    _add_modes(
        samples,
        0.104,
        ((78.0, 1.0, 0.070), (123.0, 0.49, 0.050), (235.0, 0.19, 0.032)),
        0.88,
    )
    _add_noise_burst(
        samples, 0.104, 0.075, spec.seed ^ 0xA12F038C, 0.31, 0.055, 0.0004, 0.018
    )
    return samples


def _train_chuff_loop(spec: SoundSpec) -> list[float]:
    count = spec.sample_count
    source = XorShift32(spec.seed)
    periodic_noise = [source.signed() for _ in range(count)]
    periodic_noise = _circular_box_filter(periodic_noise, 5)
    periodic_noise = _circular_box_filter(periodic_noise, 7)
    samples = [0.0] * count
    pulse_samples = count // 4
    for index in range(count):
        pulse_phase = (index % pulse_samples) / pulse_samples
        attack = min(1.0, pulse_phase / 0.075)
        decay = (1.0 - pulse_phase) ** 3.2
        puff = attack * decay
        body = math.sin(math.tau * 18.0 * pulse_phase) * puff
        # The entire noise buffer and all four envelopes repeat exactly at the loop seam.
        samples[index] = 0.76 * periodic_noise[index] * puff + 0.18 * body
    return samples


def _delivery_chime(spec: SoundSpec) -> list[float]:
    samples = _blank(spec)
    _add_wooden_bar(samples, 0.0, 392.00, spec.seed, 0.64)
    _add_wooden_bar(samples, 0.180, 587.33, spec.seed ^ 0x24E6C101, 0.56)
    return samples


def _wrong_station_thud(spec: SoundSpec) -> list[float]:
    samples = _blank(spec)
    _add_modes(
        samples,
        0.0,
        ((74.0, 1.0, 0.115), (119.0, 0.62, 0.090), (187.0, 0.31, 0.060),
         (286.0, 0.13, 0.038)),
        0.78,
    )
    _add_noise_burst(samples, 0.0, 0.100, spec.seed, 0.22, 0.045, 0.0010, 0.025)
    return samples


def _celebrate_flourish(spec: SoundSpec) -> list[float]:
    samples = _blank(spec)
    notes = ((0.0, 392.00, 0.54), (0.115, 523.25, 0.49), (0.240, 659.25, 0.45))
    for index, (start, frequency, gain) in enumerate(notes):
        _add_wooden_bar(samples, start, frequency, spec.seed ^ (index * 0x13579BDF), gain)
    return samples


def _purchase_success(spec: SoundSpec) -> list[float]:
    samples = _blank(spec)
    notes = ((0.0, 440.00, 0.52), (0.145, 659.25, 0.47), (0.300, 880.00, 0.38))
    for index, (start, frequency, gain) in enumerate(notes):
        _add_wooden_bar(samples, start, frequency, spec.seed ^ (index * 0x2468ACE1), gain)
    return samples


SYNTHS = {
    "wooden_tap": _wooden_tap,
    "switch_clunk": _switch_clunk,
    "train_chuff_loop": _train_chuff_loop,
    "delivery_chime": _delivery_chime,
    "wrong_station_thud": _wrong_station_thud,
    "celebrate_flourish": _celebrate_flourish,
    "purchase_success": _purchase_success,
}


def _finish(samples: list[float], peak_dbfs: float, loop: bool) -> bytes:
    mean = sum(samples) / len(samples)
    shaped = []
    for value in samples:
        centered = value - mean
        shaped.append(centered / (1.0 + 0.20 * abs(centered)))

    if not loop:
        fade_in = _seconds_to_index(0.001)
        fade_out = _seconds_to_index(0.014)
        for index in range(min(fade_in, len(shaped))):
            shaped[index] *= index / max(1, fade_in - 1)
        for offset in range(min(fade_out, len(shaped))):
            shaped[-1 - offset] *= offset / max(1, fade_out - 1)

    peak = max(abs(value) for value in shaped)
    if peak <= 0.0:
        raise ValueError("synthesis unexpectedly produced silence")
    target = 10.0 ** (peak_dbfs / 20.0)
    scale = target / peak
    pcm = []
    for value in shaped:
        quantized = int(round(value * scale * 32_767.0))
        pcm.append(max(-32_768, min(32_767, quantized)))
    return struct.pack("<%dh" % len(pcm), *pcm)


def _wav_bytes(spec: SoundSpec) -> bytes:
    samples = SYNTHS[spec.synth](spec)
    pcm = _finish(samples, spec.peak_dbfs, spec.synth == "train_chuff_loop")
    byte_rate = SAMPLE_RATE * CHANNELS * SAMPLE_WIDTH_BYTES
    block_align = CHANNELS * SAMPLE_WIDTH_BYTES
    header = (
        b"RIFF"
        + struct.pack("<I", 36 + len(pcm))
        + b"WAVE"
        + b"fmt "
        + struct.pack(
            "<IHHIIHH",
            16,
            1,
            CHANNELS,
            SAMPLE_RATE,
            byte_rate,
            block_align,
            SAMPLE_WIDTH_BYTES * 8,
        )
        + b"data"
        + struct.pack("<I", len(pcm))
    )
    return header + pcm


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _render_all() -> dict[SoundSpec, bytes]:
    return {spec: _wav_bytes(spec) for spec in SPECS}


def _print_manifest(rendered: dict[SoundSpec, bytes]) -> None:
    print("| File | Role | Duration | Seed | SHA-256 |")
    print("|---|---|---:|---:|---|")
    for spec, data in rendered.items():
        print(
            f"| `{spec.filename}` | {spec.role} | {spec.duration_ms / 1000:.3f} s "
            f"| `0x{spec.seed:08X}` | `{_sha256(data)}` |"
        )


def _write(output_root: Path, rendered: dict[SoundSpec, bytes]) -> int:
    output_root.mkdir(parents=True, exist_ok=True)
    for spec, data in rendered.items():
        destination = output_root / spec.filename
        destination.write_bytes(data)
        print(f"generated {destination.relative_to(REPO_ROOT)}  {_sha256(data)}")
    total = sum(len(data) for data in rendered.values())
    print(f"{len(rendered)} files, {total} bytes ({total / 1024:.1f} KiB)")
    return 0


def _check(output_root: Path, rendered: dict[SoundSpec, bytes]) -> int:
    failures: list[str] = []
    expected_names = {spec.filename for spec in SPECS}
    actual_names = {path.name for path in output_root.glob("*.wav")}
    if actual_names != expected_names:
        failures.append(
            "WAV inventory differs: missing="
            + repr(sorted(expected_names - actual_names))
            + " extra="
            + repr(sorted(actual_names - expected_names))
        )

    provenance_path = output_root / "PROVENANCE.md"
    provenance = provenance_path.read_text(encoding="utf-8") if provenance_path.exists() else ""
    if not provenance:
        failures.append(f"missing provenance record: {provenance_path}")

    total = 0
    for spec, expected in rendered.items():
        source = output_root / spec.filename
        if not source.exists():
            continue
        actual = source.read_bytes()
        total += len(actual)
        if actual != expected:
            failures.append(
                f"{spec.filename} is not generator-exact: expected {_sha256(expected)}, "
                f"found {_sha256(actual)}"
            )
        try:
            with wave.open(str(source), "rb") as reader:
                observed = (
                    reader.getnchannels(),
                    reader.getsampwidth(),
                    reader.getframerate(),
                    reader.getnframes(),
                    reader.getcomptype(),
                )
        except (EOFError, wave.Error) as error:
            failures.append(f"{spec.filename} is not a readable PCM WAV: {error}")
            continue
        expected_format = (
            CHANNELS,
            SAMPLE_WIDTH_BYTES,
            SAMPLE_RATE,
            spec.sample_count,
            "NONE",
        )
        if observed != expected_format:
            failures.append(
                f"{spec.filename} format {observed!r}, expected {expected_format!r}"
            )
        digest = _sha256(actual)
        for token, label in (
            (spec.filename, "filename"),
            (f"0x{spec.seed:08X}", "seed"),
            (f"{spec.duration_ms / 1000:.3f} s", "duration"),
            (digest, "SHA-256"),
            (spec.recipe, "recipe"),
        ):
            if token not in provenance:
                failures.append(
                    f"PROVENANCE.md lacks {label} for {spec.filename}: {token!r}"
                )

    if total >= PAYLOAD_LIMIT_BYTES:
        failures.append(
            f"source WAV payload is {total} bytes; must stay below {PAYLOAD_LIMIT_BYTES}"
        )

    if failures:
        for failure in failures:
            print("audio check: FAIL — " + failure, file=sys.stderr)
        return 1
    print(
        f"audio check: PASS — {len(SPECS)} deterministic mono PCM16/44.1kHz WAVs, "
        f"{total} bytes (< {PAYLOAD_LIMIT_BYTES})"
    )
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output-root",
        type=Path,
        default=DEFAULT_OUTPUT_ROOT,
        help="destination folder (defaults to the Unity Resources audio folder)",
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="compare committed WAVs and provenance with a fresh in-memory render",
    )
    parser.add_argument(
        "--print-manifest",
        action="store_true",
        help="print Markdown inventory rows after rendering",
    )
    args = parser.parse_args()

    rendered = _render_all()
    if args.print_manifest:
        _print_manifest(rendered)
        if args.check:
            return _check(args.output_root, rendered)
        return 0
    if args.check:
        return _check(args.output_root, rendered)
    return _write(args.output_root, rendered)


if __name__ == "__main__":
    raise SystemExit(main())
