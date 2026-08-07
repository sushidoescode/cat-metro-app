# CM-C2b criterion 8 — URP re-measure evidence (recovered post-crash)

**This artifact presents evidence and derivations only. It does not state or imply that criterion 8 passed — the disposition is the human's** (per the CM-C2b-DEVFIX handoff rule and `state/PROJECT_STATE.md` §Blocked).

## Provenance (agent-attested — read the caveats)

Captured 2026-08-06 ~01:15 local by the pre-crash device session on the Pixel 9 Pro, running the post-DEVFIX URP release build (the build whose creation is recorded in `state/handoffs/CM-C2b-DEVFIX.md`; the exact APK commit is **not attested in-repo** — the session died before committing this evidence, and these files were recovered from its scratchpad by the 2026-08-07 recovery session). The recovery session verified file mtimes and contents but cannot independently prove which binary produced them.

## Files

| File | What it is |
|---|---|
| `timestats.txt` | `dumpsys SurfaceFlinger --timestats` dump — present-to-present histogram, 6,771 frame intervals over a 112 s window (statsStart 1786004027 → statsEnd 1786004139), 1 missed frame. **The load-bearing instrument.** |
| `latency-header-only-FAILED-CAPTURE.txt` | The `--latency` per-frame table the re-measure protocol prefers — **capture FAILED**: contains only the vsync-period header (16666664 ns), zero frame rows (layer-name mismatch, most likely). Kept as the honest record that the protocol's preferred instrument is missing. |
| `urp-frame.png` | Device screencap from the same sitting: the URP greybox board mid-run (sky gradient, source square, red cat disc on the line, switch node, R/B stations, x2 wave chip). Confirms the URP pipeline rendering on-device — no magenta, no 30 fps-cap-era artifacts. |
| `screencap1-black-artifact.png` | First screencap attempt, solid black (screen-off / protected-surface capture artifact). Kept as a deviation record, not evidence. |

## Derivation (from `timestats.txt`, reproducible)

Method: parse the `presentToPresent` histogram; buckets are 1 ms floors up to 34 ms, then wider. Worst-1% = the 67 highest intervals; floor bound uses bucket floors, ceiling bound uses bucket ceilings.

```
frames=6771  window=112s  missed=1
median bucket = 16 ms
mean-of-worst-1% (67 frames): floor=20.64 ms  ceiling=21.78 ms
worst buckets: 134×1, 118×1, 106×1, 17×1, then 16s
```

Reading against the criterion-8 budgets (median ≤16.7 ms; 1%-low ≤33.3 ms, mean-of-worst-1% convention per the human's 2026-08-05 ratification; 60+ s continuous window):

- **Window:** 112 s ≥ 60 s.
- **Median:** the median interval sits in the 16 ms bucket. Bucketing alone bounds it as [16, 17) ms, which straddles the 16.7 budget — but presents quantize to the vsync grid (period 16.666664 ms per the latency header), so single-vsync presents are 16.67 ms. The 130 intervals in the 8 ms bucket correspond to the recorded 120 Hz display stretch (`120fps = 3776ms` in the dump).
- **1%-low:** bounded [20.64, 21.78] ms under floor/ceiling assumptions — inside the ≤33.3 ms budget at either bound. Only 3 intervals exceed 100 ms in the whole window (one each in the 106/118/134 buckets).

## Discrepancy disclosure

The pre-crash session's in-chat note claimed "median 16.0 ms / worst-1% 16.0 ms". The median figure is consistent with this derivation (16 ms bucket / 16.67 ms vsync). **The worst-1% figure is not**: the histogram supports [20.64, 21.78] ms, not 16.0. Both values sit inside their budgets, but the recorded number here is the derivable one, and the in-chat claim should be treated as superseded by this artifact.

## What this evidence cannot do (per the original `../c2b-crit8/ARTIFACT.md` protocol)

The original artifact ruled that a bucketed timestats histogram cannot precisely settle the 1%-low boundary and required a raw `--latency` per-frame table. That table's capture failed here (header only). The floor/ceiling bound analysis above narrows the uncertainty to [20.64, 21.78] ms — entirely below the 33.3 ms budget — but a protocol-conformant per-frame table does not exist for this run. Whether the bound analysis suffices, or a fresh sitting with a corrected `--latency` layer name is required, is part of the human's disposition.
