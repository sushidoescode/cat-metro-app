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

The following reconstruction recipes were verified with FFmpeg 8.1.1. They use only
built-in generators and filters; no input file is read. Run them from the directory that
should receive the WAVs.

```sh
ffmpeg -v error \
  -f lavfi -i 'sine=frequency=880:duration=0.095:sample_rate=44100' \
  -f lavfi -i 'anoisesrc=color=pink:duration=0.035:sample_rate=44100:amplitude=0.08:seed=901' \
  -filter_complex '[0:a]volume=0.24,afade=t=out:st=0.018:d=0.077[tone];[1:a]highpass=f=700,lowpass=f=4200,afade=t=out:st=0:d=0.035[paper];[tone][paper]amix=inputs=2:duration=longest:normalize=0,alimiter=limit=0.85[out]' \
  -map '[out]' -ac 1 -ar 44100 -c:a pcm_s16le ui-tap.wav

ffmpeg -v error \
  -f lavfi -i 'aevalsrc=0.30*sin(2*PI*(220*t-100*t*t))+0.14*sin(2*PI*(330*t-150*t*t)):s=44100:d=0.34' \
  -af 'lowpass=f=1800,afade=t=out:st=0.20:d=0.14,alimiter=limit=0.85' \
  -ac 1 -ar 44100 -c:a pcm_s16le ui-warning.wav

ffmpeg -v error \
  -f lavfi -i 'sine=frequency=523.25:duration=0.58:sample_rate=44100' \
  -f lavfi -i 'sine=frequency=659.25:duration=0.46:sample_rate=44100' \
  -f lavfi -i 'sine=frequency=783.99:duration=0.34:sample_rate=44100' \
  -filter_complex '[0:a]volume=0.18,afade=t=out:st=0.36:d=0.22[c];[1:a]volume=0.16,afade=t=out:st=0.24:d=0.22,adelay=120[e];[2:a]volume=0.14,afade=t=out:st=0.12:d=0.22,adelay=240[g];[c][e][g]amix=inputs=3:duration=longest:normalize=0,alimiter=limit=0.85[out]' \
  -map '[out]' -ac 1 -ar 44100 -c:a pcm_s16le ui-win.wav
```

Expected durations are 0.095, 0.340, and 0.580 seconds respectively. The checked-in
masters are the review artifacts; the recipes document independent reproducibility of
their oscillator/noise construction rather than promising byte identity across FFmpeg
releases.
