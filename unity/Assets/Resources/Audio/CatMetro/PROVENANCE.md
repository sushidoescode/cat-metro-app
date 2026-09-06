# Cat Metro audio provenance

Generator: `cat-metro-audio-v2`.

Original sample-free Cat Metro compositions. No external recordings or paid asset accounts.
The seven original synthesized SFX remain available as fallbacks. All files are PCM16/44.1 kHz masters.
Music is stereo, four sample-locked 32-bar stems (80 s) plus a sparse 16-bar Home loop (40 s), at 96 BPM in C major.
Unity imports SFX as mono ADPCM and music as streaming stereo Vorbis at quality 0.5.

Regenerate with `python3 scripts/generate-game-audio.py`; `--check` renders into temporary storage and compares every master, recipe, and this manifest byte for byte.
The bundled ls-clad build-music/build-sfx 1.0.0 tools, full Apache-2.0 licence, and modification notice live under `scripts/vendor/ls-clad`.

| File | Role | Duration | Generator / licence | Seed | SHA-256 |
|---|---|---:|---|---|---|
| `wooden-tap.wav` | Buttons | 0.100 s | Cat Metro synth v2 / original synthesis | `0xC47A2301` | `104cbeee8d18fe5e181c06f8e22f40e8c9441f75a7b728c721791c171805f1bf` |
| `switch-clunk.wav` | Track switch | 0.220 s | Cat Metro synth v2 / original synthesis | `0xC47A2302` | `815023f67b00da1f8d2a63638724c3229f3879836a3c49adc4ec2d7954068b07` |
| `train-chuff-loop.wav` | Train moving loop | 1.200 s | Cat Metro synth v2 / original synthesis | `0xC47A2303` | `cb85db55905b0e8403c26529c8d01fe94bc356c968e037d7fdad506640d13af2` |
| `delivery-chime.wav` | Correct delivery | 0.550 s | Cat Metro synth v2 / original synthesis | `0xC47A2304` | `df27232b85e307b7ad712c795b951deb2ee4314df43a98b2fc425f9309f22e69` |
| `wrong-station-thud.wav` | Wrong station / failure | 0.280 s | Cat Metro synth v2 / original synthesis | `0xC47A2305` | `34b98740cb13560a2074bebf1664873cee1ba89d42cf8670640adeda35140952` |
| `celebrate-flourish.wav` | Level celebration | 0.760 s | Cat Metro synth v2 / original synthesis | `0xC47A2306` | `06a1fffef49fef5f6dc2114352ff3bc99dec7f94ad19ca50573592e720f59c6b` |
| `purchase-success.wav` | Purchase confirmed | 0.680 s | Cat Metro synth v2 / original synthesis | `0xC47A2307` | `c6a7dd7a5d5c4aec4807644846b98b73119d6b896d3f248106e6c8e33b1f1958` |
| `cat-mew-1.wav` | Cat voice | 0.280 s | Cat Metro synth v2 / original synthesis | `0xC47A2310` | `d44e2600bd528fd9116eefcdc666ace99f6f368404332f0fb9a26213255acb43` |
| `cat-mew-2.wav` | Cat voice | 0.310 s | Cat Metro synth v2 / original synthesis | `0xC47A2311` | `0b6dac72c93c1e48936b6a3a740617320bae5dec0f85053b3558a954a50d4674` |
| `cat-mew-3.wav` | Cat voice | 0.350 s | Cat Metro synth v2 / original synthesis | `0xC47A2312` | `ebcb5cd3f7118286ab6660ea64c1403663c5e414d81ec3b147e3f71823d3fbb4` |
| `cat-mew-4.wav` | Cat voice | 0.380 s | Cat Metro synth v2 / original synthesis | `0xC47A2313` | `efdc9ab5a3b63f4dc3ab2a1191b392111681bd69941ad838f30158ded38feb18` |
| `cat-mew-5.wav` | Cat voice | 0.260 s | Cat Metro synth v2 / original synthesis | `0xC47A2314` | `b71ff2b77285c5831b839a6fc1abbc63ea175610e3831d87baa563330cfdec24` |
| `cat-purr-1.wav` | Home contented purr | 0.900 s | Cat Metro synth v2 / original synthesis | `0xC47A2320` | `99c6f80f9fea445678b43cef1d7312ede25fa1fa2b80708848fa48b470b56d23` |
| `cat-purr-2.wav` | Home contented purr | 1.100 s | Cat Metro synth v2 / original synthesis | `0xC47A2321` | `adf0ff8598fee4c360adc27e8d91fc76a6571c149b9218af091960799bf1c230` |
| `cat-grumble.wav` | Wrong station cat voice | 0.400 s | Cat Metro synth v2 / original synthesis | `0xC47A2322` | `8258a493edff70442d2d4c06777fdb9d0fa7c4a262a31758b31eee563711adec` |
| `win-cadence.wav` | Won cadence | 2.500 s | Cat Metro synth v2 / original synthesis | `0xC47A2330` | `db60f4e2c0e1c100b3859cc803dcdf46a7dbf5b2923cfce17b0df58663ada374` |
| `fail-sting.wav` | Failed sting | 0.900 s | Cat Metro synth v2 / original synthesis | `0xC47A2331` | `f2008a55e29bde909b5d644e77b5f289a50ae73571dd333e0748ba0b32c00f12` |
| `train-chuff-96.wav` | Train moving loop at 96 BPM | 1.250 s | Cat Metro synth v2 / original synthesis | `0xC47A2332` | `3aeaaa80508de1052701c839f011fd8ec03be73eebc268ce4b39087aec498480` |
| `music/bed.wav` | Playing bed | 80.000 s | ls-clad 1.0.0 / Apache-2.0 engine; original composition | `0x019D5EB3` | `036a4d91fddf9764d1eaa842ebea624369c1bc5f806e2cf88e9448b369916f39` |
| `music/shaker.wav` | Train motion percussion | 80.000 s | ls-clad 1.0.0 / Apache-2.0 engine; original composition | `0x019D5EDF` | `8f5776068b194026c8f91409e44e8f11cec7723a23fd7419a0d1c78e765b3268` |
| `music/melody.wav` | First delivery melody | 80.000 s | ls-clad 1.0.0 / Apache-2.0 engine; original composition | `0x019D5EBE` | `db19fe6f32bd188265878c92b62f01efe9c3101a09b94178f777fab644e05e70` |
| `music/sparkle.wav` | Last-cat celebration stem | 80.000 s | ls-clad 1.0.0 / Apache-2.0 engine; original composition | `0x019D5ED4` | `45a3657460140ee7a095097b927ab11140d52c3a8b2b59484cbebe0fced631fe` |
| `music/home.wav` | Sparse Home bed | 40.000 s | ls-clad 1.0.0 / Apache-2.0 engine; original composition | `0x019D5EB3` | `6e716247b7fd6f4a721c88599eab9c933692e06d12df954672a5dd2a9c4c4d09` |

## Recipes

- `wooden-tap.wav`: Filtered-noise fingertip impulse exciting three short, damped wood modes.
- `switch-clunk.wav`: Two offset low wooden impacts with a quiet, filtered lever scrape between them.
- `train-chuff-loop.wav`: Four seamless low-passed noise puffs with soft 60 Hz body pulses.
- `delivery-chime.wav`: G4 then D5, struck as warm inharmonic wooden-bar resonators with soft mallets.
- `wrong-station-thud.wav`: Felted low-noise impulse exciting four quickly damped desk-and-wood modes.
- `celebrate-flourish.wav`: Three tiny wooden-bar notes (G4, C5, E5) with staggered soft-mallet attacks.
- `purchase-success.wav`: A settled A4, E5, A5 wooden-bar cadence, distinct from delivery and celebration.
- `cat-mew-1.wav`: 650→950→720 Hz vocal glide, 6 Hz vibrato, two formant band-passes; soft breath and tapered vowels.
- `cat-mew-2.wav`: 650→950→720 Hz vocal glide, 6 Hz vibrato, two formant band-passes; soft breath and tapered vowels.
- `cat-mew-3.wav`: 650→950→720 Hz vocal glide, 6 Hz vibrato, two formant band-passes; soft breath and tapered vowels.
- `cat-mew-4.wav`: 650→950→720 Hz vocal glide, 6 Hz vibrato, two formant band-passes; soft breath and tapered vowels.
- `cat-mew-5.wav`: 650→950→720 Hz vocal glide, 6 Hz vibrato, two formant band-passes; soft breath and tapered vowels.
- `cat-purr-1.wav`: Low 120 Hz chest resonance, 26 Hz throat pulses and filtered breath.
- `cat-purr-2.wav`: Low 135 Hz chest resonance, 28 Hz throat pulses and filtered breath.
- `cat-grumble.wav`: Short falling 230→140 Hz grumble with soft 28 Hz throat pulses.
- `win-cadence.wav`: C-major felt-mallet cadence at 96 BPM: C5 E5 G5 then a settled C4 E4 G4 C5 chord.
- `fail-sting.wav`: Gentle descending E4 D4 C4 felt-mallet answer; no alarm or harsh buzzer.
- `train-chuff-96.wav`: Four seamless low-passed noise puffs over two beats at 96 BPM with soft body pulses.
- `music/bed.wav`: 96 BPM C major; A8-B8-A-prime8-C8; felt piano comp and low nylon pluck; circular release tails.
- `music/shaker.wav`: 96 BPM sample-locked bossa shaker and muted woodblock; fixed accents, no fills.
- `music/melody.wav`: 96 BPM C-major chord-anchored music box and kalimba answers; breathing fourth bars.
- `music/sparkle.wav`: 96 BPM C-major music-box arpeggios in alternating bars; high register, small-room release.
- `music/home.wav`: 96 BPM C major; 16 bars of sparse felt piano and answering kalimba; circular release tails.
