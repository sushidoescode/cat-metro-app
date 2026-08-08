# CM-C2b criterion 8 — URP re-measure evidence (recovered post-crash; rev 2)

**This artifact presents evidence and derivations only. It does not state or imply that criterion 8 passed — the disposition is the human's** (per the CM-C2b-DEVFIX handoff rule and `state/PROJECT_STATE.md` §Blocked). Rev 2 supersedes rev 1 of this file, whose figures were derived from the wrong population (review F-1) — see §Corrections.

## Provenance (agent-attested — read the caveats)

Captured 2026-08-06 ~01:15 local by the pre-crash device session on the Pixel 9 Pro, running the post-DEVFIX URP release build (whose creation is recorded in `state/handoffs/CM-C2b-DEVFIX.md`; the exact APK commit is **not attested in-band** — no APK hash appears in the dump, unlike the original artifact's protocol; the session died before committing this evidence, and these files were recovered from its scratchpad by the 2026-08-07 recovery session, which cannot independently prove which binary produced them).

## Files

| File | What it is |
|---|---|
| `timestats.txt` | `dumpsys SurfaceFlinger --timestats` dump. Contains BOTH the display-global "Legacy stats" block AND per-layer blocks — including the game's surface layer (`SurfaceView[com.catmetro.game/...UnityPlayerGameActivity]`, three display-config sections) with per-layer `present2present`, `present2presentDelta`, and companion histograms. **The game-layer sections are the protocol's instrument** (original artifact: "presented-frame intervals for the game's surface layer"). |
| `latency-header-only-FAILED-CAPTURE.txt` | The `--latency` per-frame table — **capture FAILED**: vsync-period header only (16666664 ns), zero frame rows (layer-name mismatch, most likely). Kept as the honest record. Note: its absence does NOT leave the per-layer question unanswerable — see the `present2presentDelta` row below. |
| `urp-frame.png` | Device screencap from the same sitting: the URP greybox board mid-run — sky gradient, source square, red cat capsule on the line, switch node, R/B station squares, x2 wave chip. Zero magenta pixels (checked programmatically). The "x2"/"R"/"B" labels are legible in real pixels — **this satisfies DEVFIX §4 step-2's device-screenshot gate for this frame**; the R-1 register row (TextMesh-under-URP, the register's worst risk) also names a human-carried editor Play-Mode screenshot as its second proof leg, which is NOT in this PR — closing R-1 is the human's call with both legs in hand. A single still cannot evidence frame rate; no rate claim is made from it. |
| `screencap1-black-artifact.png` | First screencap attempt, uniformly black (screen-off / protected-surface artifact). Deviation record, not evidence. |

## Derivation (reproducible from `timestats.txt`)

Bucket model: histogram labels are truncated integer milliseconds (proven by the dump's own identities: `totalP2PTime = 107607` = Σ label×count exactly; `averageFrameDuration = 2.760` = 18691/6773 exactly). In the 1 ms-grid region a bucket labeled `k` therefore holds values in [k, k+1) ms; and regardless of bucket-edge model in the wide region, the zero residual in the totalP2PTime identity pins the three >100 ms global intervals at 106/118/134 ms exactly.

### Game surface layer — the protocol's population (three `SurfaceView[com.catmetro.game...]` sections summed)

```
N = 6,673 present-to-present intervals · 106.7 s presented
median bucket = 16 ms
mean-of-worst-1%: k=66 (⌊1%⌋) → [19.17, 20.17) ms ; k=67 → [19.12, 20.12) ms
intervals >100 ms: 2 (one 106 ms, one 134 ms)
sub-16 ms intervals: 32 — 29 in the 30-frame 120 Hz-display/120 Hz-render section
  (presents on that config's 8.33 ms grid), 3 in the 130-frame 120 Hz-display/
  60 Hz-render section, and ZERO in the 6,513-interval 60 Hz section
present2presentDelta (the dump's own pacing-jitter measure):
  main 60 Hz section: 6,511 of 6,513 intervals at 0 ms delta, 2 at 1 ms
  all sections combined: 6,665 at 0 ms + 2 at 1 ms of 6,672
```

The delta histogram is **measured** evidence of a stable present grid — it replaces rev 1's vsync-quantization inference for the pacing question, and it directly answers Q-DEVFIX-3's alternating-8.3/25 ms concern for this run: the 60 Hz section shows **zero** sub-16 ms intervals of 6,513 (6,512×16 ms + 1×17 ms), and the game layer has no entries in the 24, 25, or 33 ms buckets anywhere — the alternating pattern is absent, not merely rare.

### Display-global "Legacy stats" — kept for reconciliation only (includes launcher, splash, taskbar, status bar)

```
6,771 intervals · 112 s · median bucket 16 ms
mean-of-worst-1% (k=67): [20.64, 21.64) ms
intervals >100 ms: 3 — the extra 118 ms appears in NO layer's present2present
  histogram; it is an unattributed display-level gap (the only per-layer 118 ms
  trace is in the game's own present2presentDelta, 120/60 section)
8 ms bucket: 130 (display-level accounting) — per-layer sub-16 ms presents total
  140 across all layers (launcher 53, splash 41, taskbar 13, game 32, window
  layer 1) under a DIFFERENT accounting; the two are not required to sum.
  Predominantly launch-time system chrome, NOT game pacing (rev 1 wrongly
  attributed all 130 to "the 120 Hz stretch")
```

Frame-count reconciliation: global `totalFrames = 6773` vs 6,771 histogram intervals (the dump does not explain the 2-frame gap); game SurfaceView sections' `totalFrames` sum to 6,673; the combined game delta histogram holds 6,672 entries (the 120/120 section records 29 deltas for 30 presents).

## Reading against the criterion-8 budgets — with the reserved calls named

- **1%-low** (the one convention the human HAS pinned: mean-of-worst-1%, 2026-08-05 ratification): game layer [19.17, 20.17) ms; display-global [20.64, 21.64) ms — inside the ≤33.3 ms budget at either bound of either population.
- **Median — the convention is UNPINNED.** Q-DEVFIX-4 (`CM-C2b-DEVFIX.md`): "Pin the median definition before the re-measure runs" — a 60 fps cap yields ~16.67 ms against a ≤16.7 ms budget, ~0.03 ms of margin, so any aggregation that bins upward flips the result. The median bucket is 16 ms in both populations and the delta histogram measures a stable grid, but **which aggregation the budget uses is a pin the human has not made; this artifact does not make it.**
- **Window — presents ≠ play; the composition call is the human's and open.** Game-layer presented time is 106.7 s ≥ 60 s of *presents*, but the dump cannot distinguish active simulation from a static banner hold, and L001 as shipped offers **6.25 s of active sim** and cannot loop (F-DEV-3); the original artifact called its 43.8 s window length a **CONTRACT MISS**, and the active-sim composition question is the separate call DEVFIX re-measure item 4 explicitly reserves for the human. Nothing in this artifact settles it.

## Corrections (rev 2 supersedes)

1. **Rev 1 of this file** derived all figures from the display-global block and called it "the load-bearing instrument"; the protocol's instrument is the game layer, whose sections rev 1's own committed file contains. Rev 1 also claimed "a protocol-conformant per-frame table does not exist for this run" — false as stated: the per-layer histograms (including `present2presentDelta`) exist in the dump; only the raw `--latency` table is missing. Rev 1's 8 ms/120 Hz sentence was wrong (130 × 8.33 ms = 1.08 s ≠ 3.776 s; the real cause is launch chrome). The first commit's message repeats rev 1's framing — this file supersedes it.
2. **The pre-crash session's in-chat claim** "median 16.0 ms / worst-1% 16.0 ms": the median figure is consistent with the 16 ms bucket; the worst-1% figure is not supported by any population in the dump — game layer [19.17, 20.17), global [20.64, 21.64). Superseded.

## What this evidence cannot do

- **Pin the median convention** — that is Q-DEVFIX-4's open human pin; the median reading above is bucket + measured-grid evidence, not a settled limb (and rev 1's vsync inference now sits behind measured delta data, but the pin is still the human's).
- **Prove active simulation** for the window clause — presents are blind to scene content; the composition call (DEVFIX item 4) remains open.
- **Attest binary provenance** — no APK hash in-band; the in-repo bar (the original artifact records an APK SHA-256) is not met by this run.
- **Supply the raw `--latency` per-frame table** the original protocol named for exact 1%-low — the per-layer histograms bound it ([19.17, 20.17) game-layer, entirely below budget) and `present2presentDelta` measures pacing directly; whether those suffice, or a fresh sitting with a corrected layer name is required, is part of the human's disposition.
