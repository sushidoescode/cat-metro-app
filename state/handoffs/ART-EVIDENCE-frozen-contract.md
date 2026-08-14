# CONTRACT ART-EVIDENCE — chrome-canvas unification + the six taste-gate frames

**Branch:** `art/evidence-pass` off `art/diorama-pass` head `3c80b92`.
**Authority:** the human's 2026-08-13 reassignment of Lane 1B's remaining work to the
coordination session, plus the in-session drive-without-asks directive (both H-1-class,
recorded on the PR by the coordinator — not this session's own words to cite).

## Restatement (implementer's own words)

PR #68's round-2 review left a named defect (E-1, recorded on the PR and in
`state/PROJECT_STATE.md`'s art-chain-blockers bullet at `origin/main`): the #68 fix moved
`WavePreviewStrip` and `BannerView` to `RenderMode.ScreenSpaceOverlay`, but
`ScreenChromeController`, `HintChipController`, `ResultsPanel`, and `GameRoot`'s dev-only
`ScreensCanvas` stayed on `RenderMode.ScreenSpaceCamera`. Unity composites ALL
`ScreenSpaceOverlay` canvases directly onto the display after everything a camera renders
(including `ScreenSpaceCamera` canvases, which paint as part of that camera's own output) —
so an Overlay canvas always paints above a Camera-space one **regardless of `sortingOrder`**.
The authored ladder (wave 80 / hint 90 / chrome+veil 100 / results 110 / banner 115 / dev
screens 120) is therefore not authoritative today: the wave strip (Overlay, 80) can paint
*above* the halt veil (Camera-space, 100) even though 80 < 100, because the two canvases sit
on different, non-comparable layers. The same asymmetry makes a `Camera.Render()`-into-a-
`RenderTexture` capture (the repo's established evidence-capture pattern) blind to the four
Overlay surfaces: that route only ever paints what the one camera draws, and an Overlay
canvas never enters a camera's render target.

My job: (1) move the four remaining Camera-space canvases to Overlay so the whole chrome
stack shares one paint layer and the sortingOrder ladder is real end to end, preserving every
sortingOrder value byte-exact; (2) add a new PlayMode test that pins the resulting
cross-canvas paint order and is proven to fail against the pre-unification two-layer state;
(3) add a dev-fenced, committed capture rig that reads the composited screen back buffer
(not a camera render target) so it can actually see Overlay content, and empirically show the
RT-vs-back-buffer gap E-1 predicted; (4) use that rig to produce and commit the six taste-gate
frames UI-CHROME's criterion 9 named (Home, LevelIntro, Playing/wave, first warning, second
warning+hint, Won/Results) at a device-like portrait aspect, with an honest ARTIFACT.md.

## Binding references

- PR #68 round-2 review finding E-1 (recorded on the PR; restated in
  `origin/main:state/PROJECT_STATE.md`'s "Now" line, art-chain-blockers bullet).
- `state/handoffs/UI-CHROME-frozen-contract.md` criterion 9 (the six named frames) and its
  ResultsPanel/ScreenChromeController/HintChipController "renders into capture RTs" canvas
  comments — this contract's capture-path change (item 3) supersedes that rationale; the
  comments are corrected in place, not merely overridden silently.
- `GameRoot.cs:189-232` (chrome/hint/results attach + the sortingOrder regression pin) and
  `GameRoot.cs:234-275` (`ComposeDevScreenFlow`, the dev-only `ScreensCanvas`).
- `tests/unity/devcap.test.sh` (the DevCapture whole-file guard + unguarded-reference
  scanner) and `unity/Assets/Scripts/Bootstrap/DevCapture/*.cs` (the shape any code placed
  in that directory must match — this contract places no new file there; see Assumption
  A-AE-4).
- The `#33` standing evidence-rig convention already used by `ChromeStateTests.cs`,
  `ResultsPanelTests.cs`, `TeachAffordanceTests.cs`, and `DioramaConstructionTests.cs`
  (env-var-armed `[UnityTest]`, `Assert.Pass` when disarmed so `scripts/test.sh`'s
  total==passed gate never breaks) — the pattern item 3 follows.
- `unity/Assets/Art/Polyfork/Editor/CatMetroDioramaAuthoring.cs`'s
  `ConfigurePortraitEvidenceGameView()` (Lane 1A's exclusive file — read and CALLED from a
  new file, never edited) and `DioramaConstructionTests.cs`'s
  `CaptureEvidence_RealGameDiorama_WhenRequested` (the proven `Screen.SetResolution(900,
  2000, FullScreenMode.Windowed)` + pre-configured Game View combination for 900x2000).

## Scope

**Owned for this contract:**
- `unity/Assets/Scripts/Presentation/Hud/ScreenChromeController.cs`
- `unity/Assets/Scripts/Presentation/Hud/HintChipController.cs`
- `unity/Assets/Scripts/Presentation/Hud/ResultsPanel.cs`
- `unity/Assets/Scripts/Bootstrap/GameRoot.cs` (the `ComposeDevScreenFlow` canvas lines only)
- New PlayMode tests under `unity/Assets/Tests/PlayMode/Bootstrap/**`
- The three existing PlayMode assertions that currently pin `ScreenSpaceCamera` for these
  canvases (`GameRootWiringTests.cs` x2, `DevScreenFlowTests.cs` x1) — updated, not deleted,
  because the behavior they pin is the exact thing this contract changes on purpose.
- `evals/results/ux/ui-chrome-pass/**` (new evidence), this contract/handoff, and — at
  merge bookkeeping only — the lane's `state/PROJECT_STATE.md` row.

**Forbidden (per the launch instructions):** Board/Cameras/Input/Diagnostics product code,
`unity/Assets/Scenes/**`, `unity/ProjectSettings/**` (other than reverting incidental
editor-drift touches before committing), `Greybox.mat`, Domain, Content, `ui.csv` rows, the
criterion-5 `CreatePrimitive` gate, `docs/plan/**`. `WavePreviewStrip.cs` and `BannerView.cs`
are read-only reference points (already Overlay from #68) — not edited.

## Acceptance criteria → tests

1. **Canvas unification.** `ScreenChromeController`, `HintChipController`, `ResultsPanel`,
   and `GameRoot.ComposeDevScreenFlow`'s `ScreensCanvas` all construct
   `RenderMode.ScreenSpaceOverlay` canvases, unconditionally — every sortingOrder value
   (90/100/110/120) stays byte-identical. *Checks:* the three updated existing PlayMode
   assertions (renamed to say Overlay, not Camera) + the new z-order tests below (which
   would not compile/pass otherwise).
2. **Z-order pin, red-first.** A new PlayMode test file proves, with real Wire()-attached
   canvases: (a) at a real Halted state with a genuinely pending wave, the halt veil's canvas
   shares a render mode with the wave strip's canvas and paints above it (100 > 80, real);
   (b) ResultsPanel's canvas shares a render mode with and paints above chrome's (110 > 100);
   (c) the dev `ScreensCanvas` shares a render mode with and paints above both the banner
   (120 > 115) and ResultsPanel, with the banner itself between (115 > 110). Run RED against
   the pre-unification tree (proves the two-layer defect is real, not asserted-away), then
   GREEN after item 1, then mutation-proved (temporarily reverting one canvas to
   `ScreenSpaceCamera` turns it RED again).
3. **Capture path.** A committed, env-var-armed (`CM_ARTEV_CAPTURE_DIR`) PlayMode test reads
   the actual composited screen back buffer (`ScreenCapture.CaptureScreenshotAsTexture()`
   after `WaitForEndOfFrame`) rather than a camera `Render()`-into-`RenderTexture`, so it can
   see Overlay content. The same armed run also writes one `Cam.Render()`-into-RT frame at
   the identical Playing/wave moment, for a direct side-by-side proof of the RT-vs-back-buffer
   gap E-1 predicted.
4. **The six frames.** Home, LevelIntro, Playing/wave, first warning (no hint), second
   warning (with hint), Won/Results — captured through the item-3 rig at a 900x2000 portrait
   aspect (Lane 1A's proven Game-View + `Screen.SetResolution` recipe), committed under
   `evals/results/ux/ui-chrome-pass/**` with an `ARTIFACT.md` recording method, SHAs, the
   RT-vs-back-buffer result, and the Polyfork-models-absent limitation honestly (no art
   models locally — custody design; the diorama board may render as bare/greybox geometry in
   these frames, and the chrome/UI is what these frames are evidence for).

## Assumptions (listed, not hidden)

- **A-AE-1:** The z-order test's Halted case reuses the `ChromeStateTests.AttachControlled`
  precedent (rebinding the Wire-attached `ScreenChromeController` to a test-controlled state
  delegate) instead of driving the real exception-triggered halt boundary. This is
  behaviorally equivalent for what is being proved: canvas `renderMode`/`sortingOrder` are
  set once at `EnsureViews()` and never vary with `ScreenState`, so the assertion target does
  not depend on which halt route produced the state — while the positive-control asserts
  (`Veil.IsVisible`, `Preview.VisibleChipCount > 0`) still force the scenario to be real and
  concrete, not vacuous. Does not change any acceptance-test's pass/fail outcome versus the
  alternative (driving the real domain-boundary halt), only the harness's speed/flakiness.
- **A-AE-2:** "900x2000-equivalent aspect" is read as the literal 900x2000 pixel target Lane
  1A's own capture rig already proved reachable in this exact environment (not merely a
  same-ratio smaller frame), reusing that rig's public entry point.
- **A-AE-3:** The six-frame rig is committed (not written-then-deleted), matching the
  dominant "#33 evidence rig" pattern already used four times in this codebase, rather than
  the rarer disposable-probe variant used for a few earlier one-off captures.
- **A-AE-4:** No new file is added under `unity/Assets/Scripts/Bootstrap/DevCapture/` — that
  directory's whole-file guard (`#if DEVELOPMENT_BUILD || UNITY_EDITOR`) is shared by actual
  on-device dev-build code, and this contract's capture code needs Editor-only APIs
  (`UnityEditor`, Game View sizing) that cannot compile into a `DEVELOPMENT_BUILD` device
  player. Any such code stays under `unity/Assets/Editor/` (Unity's own editor-only folder
  convention, already used by `CatMetroCliBuild.cs`) or inside `unity/Assets/Tests/**` (never
  scanned by `devcap.test.sh`, which only walks `Bootstrap/DevCapture` and
  `unity/Assets/Scripts`). Nothing this contract adds references the four devcap-scanned
  symbols (`DevFrameCapture`/`FrameLogCsv`/`DevBootOverride`/`DevLevelOverride`) at all.

## Method

TDD where behavior changes: the z-order test is written and run RED against the current tree
first (proving the halt-veil-vs-wave-strip case fails for the right reason), then the
unification lands, then GREEN, then one mutation proof. The capture rig and the six frames
are an evidence deliverable, not a behavior pin — proportional (sprint-mode) testing applies:
the rig's own disarmed-path GREEN is the check that keeps it a safe, always-on suite citizen;
the frames themselves are inspected by eye per the standing visual-verification rule, not
"proved" by an assertion. The known-red Polyfork-models baseline (3 EM + 9 PM, recorded on
`origin/main:state/PROJECT_STATE.md`) is recorded before any edit and must not gain new
members. Full Unity EditMode+PlayMode run at the tip; `scripts/check.sh` green;
`scripts/test.sh` is expected to keep reporting the same pre-existing failures as the known-red
baseline (not a new regression) since `editmode.test.sh`'s editor half fails closed on ANY
Unity-suite red, including that baseline. `unity/Packages/packages-lock.json` and the five
editor-drift settings files are reverted before every commit if Unity touched them.

## Status log

- 2026-08-13 — contract frozen at `3c80b92` (this commit).
- 2026-08-14 — all four criteria certified. Z-order pin (`8818596`) proven RED against the
  pre-unification tree (both discriminating cases failed with the exact E-1 render-mode
  mismatch, not a setup error), then GREEN after the unification fix (`bcd4090`), then a
  single-canvas mutation proof (only `ScreenChromeController` reverted) killed exactly the
  two tests that reference it. Capture rig (`7575496`) needed one follow-up
  (`6fde975`): the run's first back-buffer reads showed a shader-compilation placeholder
  color (Unity's async compiler, first use of a shader/material pair in a fresh batch
  process) — isolated by comparing against the identical technique several frames later in
  the same session (correct) and a second session booted later in the process (correct, no
  warm-up needed); fixed with 30 warm-up frames before the run's first capture only. The
  RT-vs-back-buffer comparison pair (`03-playing-wave.png` vs
  `03-playing-wave-camera-rt-comparison.png`) empirically confirms E-1: the RT frame's top
  status band is empty where the back-buffer frame shows the wave-preview strip. Six frames
  + the comparison committed at `045055a`
  (`evals/results/ux/ui-chrome-pass/artev-2026-08-14/`), each inspected by eye; the
  Polyfork-models-absent limitation recorded honestly in that directory's `ARTIFACT.md`.
  Full-suite re-run at the tip matches the recorded baseline exactly (EM 833/830/3 failed,
  PM 180/171/9 failed, same test names both times) — zero new failures.
  `bash scripts/check.sh` green; `bash scripts/test.sh` 16/17 (the one failure is
  `tests/unity/editmode.test.sh`, fail-closed by design on the pre-existing baseline).
  Worktree clean at the final tip (one incidental `dotnet` lock-file drift from the test
  run's own `dotnet test` legs was caught and reverted).
