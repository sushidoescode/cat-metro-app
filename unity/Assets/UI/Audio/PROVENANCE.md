# Cat Metro UI stingers — provenance

Created for Cat Metro on 2026-08-09. These are original procedural sounds; they contain
no sample, recording, music, or other third-party audio. The committed WAVs are mono,
44.1 kHz, signed 16-bit PCM and are safe to reuse in game and submission footage.

The source commands used FFmpeg's built-in generators:

- `ui-tap.wav`: 880 Hz sine plus a short pink-noise transient, filtered, mixed, and
  faded to 95 ms.
- `ui-warning.wav`: two synthesized descending partials, low-pass filtered and faded
  to 340 ms.
- `ui-win.wav`: C5/E5/G5 sine tones entering 120 ms apart and fading as a 580 ms
  ascending flourish.

Reproduction uses only `sine`, `anoisesrc`, or `aevalsrc`, followed by FFmpeg audio
filters and `-ac 1 -ar 44100 -c:a pcm_s16le`. The exact commands are recorded in the
UI-CHROME PR evidence so they can be rerun without importing an external source file.
