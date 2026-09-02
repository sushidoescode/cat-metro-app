# Cat Metro game SFX — provenance

Created for Cat Metro on 2026-09-01. These seven WAV masters are project-original
procedural synthesis. No sample, recording, music, copyrighted source, generative-audio
service, or other third-party audio was used. The generator reads no audio input and uses
only Python's standard library; all noise comes from its documented, seeded XorShift32
implementation.

The committed files are the review masters. They are mono, 44.1 kHz, signed 16-bit PCM.
Unity imports them as ADPCM, Decompress On Load, without normalization; runtime playback
uses 2D sources. This keeps the short tactile transients responsive on mobile while
preserving the intentionally quiet relative levels. Distribution rights follow the Cat
Metro repository/project rather than any third-party asset licence.

| File | Role | Duration | Seed | SHA-256 |
|---|---|---:|---:|---|
| `wooden-tap.wav` | Buttons | 0.100 s | `0xC47A2301` | `104cbeee8d18fe5e181c06f8e22f40e8c9441f75a7b728c721791c171805f1bf` |
| `switch-clunk.wav` | Track switch | 0.220 s | `0xC47A2302` | `815023f67b00da1f8d2a63638724c3229f3879836a3c49adc4ec2d7954068b07` |
| `train-chuff-loop.wav` | Train moving loop | 1.200 s | `0xC47A2303` | `cb85db55905b0e8403c26529c8d01fe94bc356c968e037d7fdad506640d13af2` |
| `delivery-chime.wav` | Correct delivery | 0.550 s | `0xC47A2304` | `df27232b85e307b7ad712c795b951deb2ee4314df43a98b2fc425f9309f22e69` |
| `wrong-station-thud.wav` | Wrong station / failure | 0.280 s | `0xC47A2305` | `34b98740cb13560a2074bebf1664873cee1ba89d42cf8670640adeda35140952` |
| `celebrate-flourish.wav` | Level celebration | 0.760 s | `0xC47A2306` | `06a1fffef49fef5f6dc2114352ff3bc99dec7f94ad19ca50573592e720f59c6b` |
| `purchase-success.wav` | Purchase confirmed | 0.680 s | `0xC47A2307` | `c6a7dd7a5d5c4aec4807644846b98b73119d6b896d3f248106e6c8e33b1f1958` |

## Per-file synthesis recipes

- `wooden-tap.wav` — Filtered-noise fingertip impulse exciting three short, damped wood modes.
- `switch-clunk.wav` — Two offset low wooden impacts with a quiet, filtered lever scrape between them.
- `train-chuff-loop.wav` — Four seamless low-passed noise puffs with soft 60 Hz body pulses.
- `delivery-chime.wav` — G4 then D5, struck as warm inharmonic wooden-bar resonators with soft mallets.
- `wrong-station-thud.wav` — Felted low-noise impulse exciting four quickly damped desk-and-wood modes.
- `celebrate-flourish.wav` — Three tiny wooden-bar notes (G4, C5, E5) with staggered soft-mallet attacks.
- `purchase-success.wav` — A settled A4, E5, A5 wooden-bar cadence, distinct from delivery and celebration.

## Reproduction

From the repository root, run:

```sh
python3 scripts/generate-game-audio.py
python3 scripts/generate-game-audio.py --check
```

Generator version: `cat-metro-audio-v1` in
`scripts/generate-game-audio.py`. It was verified here with CPython 3.14.6. The source WAV
payload is 334,586 bytes (326.7 KiB), below the task's 2,000,000-byte cap before Unity's
ADPCM import compression.
