# Editor visual pass — merged main @ 6f5fed5 (2026-08-08)

**This artifact presents rendered frames and observations only; it dispositions nothing.** Captured by the recovery session's verification agent per the standing visual rule; every frame was eyeballed (agent + orchestrator).

## Method

Uncommitted PlayMode probe (`CmSeamVisualProbeTests.cs`, deleted after the run — worktree verified clean) using the committed capture pattern (`RenderTexture(Screen.width, Screen.height, 24)` → `Cam.Render()` → `ReadPixels` → `EncodeToPNG`), Unity 6000.3.16f1 batch WITHOUT `-nographics`. Captures 1–3 booted via the **#52 file seam** (`DevBootOverride.DirectoryOverride` + a real `boot.json`, real `GameRoot.Launch()` → `InitializeFromSeam` → StreamingAssets → `LevelImporter`, `DEVCAP_BOOT_OVERRIDE` + `SEAM_LOADED` observed in the log) — the same path a device boot takes. Captures 4–5 used `LaunchWith` on the solver-witnessed demo level per the DevLevelOverrideTests recipes. Taps were driven through `TapInput.HandleTapAtScreen` at the views' own painted-rect centers.

**Host-resolution caveat (load-bearing):** `-screen-width/-height 1080x2400` does not reach the PlayMode test player; frames are **640×480** (the CM-UX-02-documented batch-host behavior, matching all prior editor evidence). Size/DPI observations below are therefore host-scale observations, not device findings.

## Frames

| File | What is visible (as eyeballed) |
|---|---|
| `01-home.png` | "Cat Metro" title (small red chip overlapping the leading glyph — see `zoom-title.png`); three grey district silhouettes (third low-contrast against the ground — `zoom-districtC.png`); L001 board behind (no backdrop panel, by design); **the known CM-UX-06 pin/ring oversize+clip reproduces** (`zoom-pinregion.png`: ring ~half frame width, obscures stations, clipped at bottom) |
| `02-levelintro.png` | "First Switch" + "Deliver 2 cats" over translucent sheet, board through it, full-width Play chip; Home title/pin gone (`Home.Hide()` holds) |
| `03-playing.png` | Screens gone; source disc, switch + hollow-ring teach affordance, edges, R/B stations, wave-preview chip; no overlay chrome |
| `04a-fail-1st-no-hint.png` | 1st FailureReview: cause banner "The last train left the depot", cause-camera framing, full-width Try-again CTA; **hint chip correctly absent** (rule requires ≥2 entries) |
| `04b-fail-2nd-with-hint.png` | 2nd failure same attempt-run: banner + CTA + dark-spruce hint chip "Tap the flashing switch" in its own band; no overlap among the three stacked elements — the edge-triggered hint rule holds on real pixels |
| `05-won.png` | "All cats home!" banner over the frozen board — **nothing else**: no ResultsPanel, no score, no Next chip. The documented Q-3 gap, confirmed as a player sees it; the LoadNext contract owns filling it |

## Observations (for follow-up, not dispositions)

1. Pin/ring oversize — pre-disclosed CM-UX-06 host-scale defect, reproduces unchanged.
2. **New, low:** wave-preview chip text illegible at 640×480 on every frame showing it (`zoom-wavechip.png` — smeared pixels, no readable glyphs). Likely the same host-resolution class as (1); requires a device-resolution capture to confirm before treating as a real defect.
3. All other text crisp and legible (title, level name, goal, Play, banners, CTA, hint).

Related artifact from the same day: polish dev APK v2 built from this main, SHA-256 `0c65b91279df0be6ba9e587d6c380891a8fe5ee501cd35908b631b508257e915` (dev-fenced seam tokens verified present in `global-metadata.dat`; APK itself lives outside the repo).
