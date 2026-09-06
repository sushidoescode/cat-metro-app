#!/usr/bin/env python3
"""Generate Cat Metro's original procedural SFX and wooden-toy score.

The SFX generator reads no audio input and uses only Python's standard library. Every
noise source comes from the fixed XorShift32 implementation below, making each
named seed repeatable. Music uses Node and the bundled Apache-2.0 build-music engine.
"""

from __future__ import annotations

import argparse
import array
import hashlib
import math
from dataclasses import dataclass
from pathlib import Path
import struct
import subprocess
import sys
import tempfile
import wave


SAMPLE_RATE = 44_100
CHANNELS = 1
SAMPLE_WIDTH_BYTES = 2
PAYLOAD_LIMIT_BYTES = 2_000_000
GENERATOR_VERSION = "cat-metro-audio-v2"
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
    channels: int = CHANNELS

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

SPECS += tuple(
    SoundSpec(f"cat-mew-{index + 1}.wav", "Cat voice", duration, 0xC47A2310 + index,
              -13.0, "650→950→720 Hz vocal glide, 6 Hz vibrato, two formant band-passes; soft breath and tapered vowels.",
              "cat_mew")
    for index, duration in enumerate((280, 310, 350, 380, 260))
) + (
    SoundSpec("cat-purr-1.wav", "Home contented purr", 900, 0xC47A2320, -18,
              "Low 120 Hz chest resonance, 26 Hz throat pulses and filtered breath.", "cat_purr"),
    SoundSpec("cat-purr-2.wav", "Home contented purr", 1100, 0xC47A2321, -18,
              "Low 135 Hz chest resonance, 28 Hz throat pulses and filtered breath.", "cat_purr"),
    SoundSpec("cat-grumble.wav", "Wrong station cat voice", 400, 0xC47A2322, -14,
              "Short falling 230→140 Hz grumble with soft 28 Hz throat pulses.", "cat_grumble"),
    SoundSpec("win-cadence.wav", "Won cadence", 2500, 0xC47A2330, -10,
              "C-major felt-mallet cadence at 96 BPM: C5 E5 G5 then a settled C4 E4 G4 C5 chord.", "win_cadence"),
    SoundSpec("fail-sting.wav", "Failed sting", 900, 0xC47A2331, -12,
              "Gentle descending E4 D4 C4 felt-mallet answer; no alarm or harsh buzzer.", "fail_sting"),
    SoundSpec("train-chuff-96.wav", "Train moving loop at 96 BPM", 1250, 0xC47A2332, -11,
              "Four seamless low-passed noise puffs over two beats at 96 BPM with soft body pulses.", "train_chuff_loop"),
)

MUSIC_SPECS = tuple(
    SoundSpec("music/" + name + ".wav", role, duration, seed, peak,
              recipe, "build_music", channels=2)
    for name, role, duration, seed, peak, recipe in (
        ("bed", "Playing bed", 80000, 27090611, -9,
         "96 BPM C major; A8-B8-A-prime8-C8; felt piano comp and low nylon pluck; circular release tails."),
        ("shaker", "Train motion percussion", 80000, 27090655, -12,
         "96 BPM sample-locked bossa shaker and muted woodblock; fixed accents, no fills."),
        ("melody", "First delivery melody", 80000, 27090622, -12,
         "96 BPM C-major chord-anchored music box and kalimba answers; breathing fourth bars."),
        ("sparkle", "Last-cat celebration stem", 80000, 27090644, -16,
         "96 BPM C-major music-box arpeggios in alternating bars; high register, small-room release."),
        ("home", "Sparse Home bed", 40000, 27090611, -6,
         "96 BPM C major; 16 bars of sparse felt piano and answering kalimba; circular release tails."),
    )
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


def _band_pass(samples: list[float], frequency: float, q: float) -> list[float]:
    omega = math.tau * frequency / SAMPLE_RATE
    alpha = math.sin(omega) / (2 * q)
    a0 = 1 + alpha
    b0, b2 = alpha / a0, -alpha / a0
    a1, a2 = -2 * math.cos(omega) / a0, (1 - alpha) / a0
    x1 = x2 = y1 = y2 = 0.0
    result = []
    for x in samples:
        y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2
        result.append(y)
        x2, x1, y2, y1 = x1, x, y1, y
    return result


def _cat_mew(spec: SoundSpec) -> list[float]:
    source = XorShift32(spec.seed)
    raw, phase = [], 0.0
    variation = (spec.seed & 7) * 0.006
    for index in range(spec.sample_count):
        t = index / SAMPLE_RATE
        u = index / (spec.sample_count - 1)
        frequency = (650 + 300 * min(1, u / .43) if u < .43
                     else 950 - 230 * (u - .43) / .57)
        frequency *= 1 + variation + .017 * math.sin(math.tau * 6 * t)
        phase += math.tau * frequency / SAMPLE_RATE
        throat = sum(math.sin(phase * k) / k for k in range(1, 8))
        raw.append(throat + .025 * source.signed())
    first, second = _band_pass(raw, 1000, 2.2), _band_pass(raw, 2400, 3.1)
    return [(first[i] * .72 + second[i] * .28) * max(0, math.sin(math.pi * i / (len(raw) - 1))) ** .8
            for i in range(len(raw))]


def _cat_purr(spec: SoundSpec) -> list[float]:
    source, filtered = XorShift32(spec.seed), 0.0
    result = []
    for i in range(spec.sample_count):
        t, u = i / SAMPLE_RATE, i / (spec.sample_count - 1)
        filtered += .035 * (source.signed() - filtered)
        pulse = (.5 + .5 * math.sin(math.tau * (26 + (spec.seed & 1) * 2) * t)) ** 2
        chest = math.sin(math.tau * (120 + (spec.seed & 1) * 15) * t)
        result.append((.7 * chest + filtered) * (.15 + .85 * pulse) * math.sin(math.pi * u))
    return result


def _cat_grumble(spec: SoundSpec) -> list[float]:
    phase, result = 0.0, []
    for i in range(spec.sample_count):
        t, u = i / SAMPLE_RATE, i / (spec.sample_count - 1)
        phase += math.tau * (230 - 90 * u) / SAMPLE_RATE
        result.append((math.sin(phase) + .23 * math.sin(phase * 3))
                      * (.65 + .35 * math.sin(math.tau * 28 * t)) * math.sin(math.pi * u))
    return result


def _win_cadence(spec: SoundSpec) -> list[float]:
    samples = _blank(spec)
    for i, (start, frequency, gain) in enumerate(((0, 523.25, .45), (.3125, 659.25, .42),
            (.625, 783.99, .40), (1.25, 261.63, .38), (1.27, 329.63, .27),
            (1.29, 392, .24), (1.31, 523.25, .30))):
        _add_wooden_bar(samples, start, frequency, spec.seed + i, gain)
    return samples


def _fail_sting(spec: SoundSpec) -> list[float]:
    samples = _blank(spec)
    for i, frequency in enumerate((329.63, 293.66, 261.63)):
        _add_wooden_bar(samples, i * .22, frequency, spec.seed + i, .5 - i * .06)
    return samples


SYNTHS = {
    "wooden_tap": _wooden_tap,
    "switch_clunk": _switch_clunk,
    "train_chuff_loop": _train_chuff_loop,
    "delivery_chime": _delivery_chime,
    "wrong_station_thud": _wrong_station_thud,
    "celebrate_flourish": _celebrate_flourish,
    "purchase_success": _purchase_success,
    "cat_mew": _cat_mew,
    "cat_purr": _cat_purr,
    "cat_grumble": _cat_grumble,
    "win_cadence": _win_cadence,
    "fail_sting": _fail_sting,
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
    rendered = {spec: _wav_bytes(spec) for spec in SPECS}
    with tempfile.TemporaryDirectory(prefix="catmetro-music-") as directory:
        subprocess.run(["node", str(REPO_ROOT / "scripts/generate-game-music.js"), directory],
                       check=True, stdout=subprocess.DEVNULL)
        for spec in MUSIC_SPECS:
            rendered[spec] = (Path(directory) / Path(spec.filename).name).read_bytes()
    return rendered


def _recipe(spec: SoundSpec) -> str:
    generator = ("ls-clad build-music 1.0.0; scripts/generate-game-music.js"
                 if spec.synth == "build_music" else "scripts/generate-game-audio.py")
    return (f"Cat Metro original audio — {spec.filename}\nGenerator: {generator}\n"
            f"Version: {GENERATOR_VERSION}\nSeed: 0x{spec.seed:08X}\n"
            f"Generation brief: {spec.recipe}\n"
            "No sampled audio, model weights, or third-party recordings.\n"
            "Original composition/synthesis for Cat Metro; commercial use in a paid app permitted.\n"
            "The sample-free ls-clad engine is Apache-2.0; its licence and copyright notice\n"
            "are saved in music/ENGINE-LICENSE.txt and music/ENGINE-NOTICE.txt.\n")


def _provenance(rendered: dict[SoundSpec, bytes]) -> str:
    rows = ["# Cat Metro audio provenance", "", f"Generator: `{GENERATOR_VERSION}`.", "",
            "Original sample-free Cat Metro compositions. No external recordings or paid asset accounts.",
            "The seven original synthesized SFX remain available as fallbacks. All files are PCM16/44.1 kHz masters.",
            "Music is stereo, four sample-locked 32-bar stems (80 s) plus a sparse 16-bar Home loop (40 s), at 96 BPM in C major.",
            "Unity imports SFX as mono ADPCM and music as streaming stereo Vorbis at quality 0.5.", "",
            "Regenerate with `python3 scripts/generate-game-audio.py`; `--check` renders into temporary storage and compares every master, recipe, and this manifest byte for byte.",
            "The bundled ls-clad build-music/build-sfx 1.0.0 tools, full Apache-2.0 licence, and modification notice live under `scripts/vendor/ls-clad`.", "",
            "| File | Role | Duration | Generator / licence | Seed | SHA-256 |", "|---|---|---:|---|---|---|"]
    for spec, data in rendered.items():
        origin = ("ls-clad 1.0.0 / Apache-2.0 engine; original composition"
                  if spec.synth == "build_music" else "Cat Metro synth v2 / original synthesis")
        rows.append(f"| `{spec.filename}` | {spec.role} | {spec.duration_ms/1000:.3f} s | {origin} | `0x{spec.seed:08X}` | `{_sha256(data)}` |")
    rows.extend(["", "## Recipes", ""])
    rows.extend(f"- `{spec.filename}`: {spec.recipe}" for spec in rendered)
    return "\n".join(rows) + "\n"


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
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(data)
        destination.with_suffix(".recipe.txt").write_text(_recipe(spec), encoding="utf-8")
        print(f"generated {spec.filename}  {_sha256(data)}")
    (output_root / "PROVENANCE.md").write_text(_provenance(rendered), encoding="utf-8")
    for name in ("LICENSE", "NOTICE"):
        (output_root / "music" / ("ENGINE-" + name + ".txt")).write_bytes(
            (REPO_ROOT / "scripts/vendor/ls-clad" / name).read_bytes())
    total = sum(len(data) for data in rendered.values())
    print(f"{len(rendered)} files, {total} bytes ({total / 1024:.1f} KiB)")
    return 0


def _check(output_root: Path, rendered: dict[SoundSpec, bytes]) -> int:
    failures: list[str] = []
    expected_names = {spec.filename for spec in rendered}
    actual_names = {str(path.relative_to(output_root)) for path in output_root.rglob("*.wav")}
    if actual_names != expected_names:
        failures.append(
            "WAV inventory differs: missing="
            + repr(sorted(expected_names - actual_names))
            + " extra="
            + repr(sorted(actual_names - expected_names))
        )

    for name in ("LICENSE", "NOTICE"):
        copied = output_root / "music" / ("ENGINE-" + name + ".txt")
        if not copied.exists() or copied.read_bytes() != (REPO_ROOT / "scripts/vendor/ls-clad" / name).read_bytes():
            failures.append("missing or altered engine " + name)

    provenance_path = output_root / "PROVENANCE.md"
    provenance = provenance_path.read_text(encoding="utf-8") if provenance_path.exists() else ""
    if not provenance:
        failures.append(f"missing provenance record: {provenance_path}")
    elif provenance != _provenance(rendered):
        failures.append("PROVENANCE.md differs from the regenerated manifest")

    total = 0
    for spec, expected in rendered.items():
        source = output_root / spec.filename
        if not source.exists():
            continue
        actual = source.read_bytes()
        if spec.synth != "build_music":
            total += len(actual)
        recipe = source.with_suffix(".recipe.txt")
        if not recipe.exists() or recipe.read_text(encoding="utf-8") != _recipe(spec):
            failures.append(f"missing or altered generation brief: {recipe.name}")
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
                if spec.synth == "build_music":
                    pcm = array.array("h", reader.readframes(reader.getnframes()))
                    if sys.byteorder != "little":
                        pcm.byteswap()
                    power = sum(value * value for value in pcm) / max(1, len(pcm)) / 32768 ** 2
                    gain = .5 if spec.filename.endswith("home.wav") else .7 if spec.filename.endswith("bed.wav") else 1
                    rms_db = 10 * math.log10(max(1e-15, power * gain * gain))
                    floor = {"music/home.wav": -35, "music/bed.wav": -33,
                             "music/shaker.wav": -41, "music/melody.wav": -32, "music/sparkle.wav": -42}[spec.filename]
                    if rms_db < floor:
                        failures.append(f"{spec.filename} runtime level {rms_db:.2f} dBFS is below {floor} dBFS")
        except (EOFError, wave.Error) as error:
            failures.append(f"{spec.filename} is not a readable PCM WAV: {error}")
            continue
        expected_format = (
            spec.channels,
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
        f"audio check: PASS — {len(SPECS)} mono SFX and {len(MUSIC_SPECS)} stereo music masters; "
        f"SFX {total} bytes (< {PAYLOAD_LIMIT_BYTES}); all SHA-256 rows and recipes reproduce"
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
