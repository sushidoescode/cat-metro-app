# CM-UX-07 — Frozen contract: the thin wiring PR (turns the UX tranche ON)

Frozen at branch anchor 32dbecb (origin/main after #36/#37/#38/#39/#41/#42), FIRST commit on
task/CM-UX-07-wiring, before any code. Immutable across the branch except the Status log
section at the bottom. Decompose §CM-UX-07 (docs/ux/ux-layer-decompose.md:133-145) is the
parent; the CM-UX-06 handoff's consolidated ledger (state/handoffs/CM-UX-06.md:36-43) is the
obligation source of record. Sequence gates DEVCAP (#26) + device-config-fix (#30): both
merged — DISCHARGED (stated here so the reviewer need not re-derive it).

Human answers of record (docs/ux/ux-layer-decompose.md:179-182): Q-2 = YES restart escape ·
Q-3 = HOLD results panel until LoadNext · Q-5 = boot stays L001. No open human calls in this
contract.

## Scope walls

- Composition lands in `GameRoot.Wire`/`Retry` + one dev-fenced flow composer (criterion 6).
- DECLARED component edits — exactly these, nothing else (the "composition-only" posture is
  amended here, openly, because the ledger's own R2-3 law demands component code):
  W-1 `HomeScreenView`: add `OnDisable()` mirroring `OnDestroy()`.
  W-2 `LevelIntroSheet`: add `OnDisable()` mirroring `OnDestroy()`.
  W-3 `ResultsPanel`: add `OnDisable()` mirroring `OnDestroy()` (audit M-3; the panel stays
  UNATTACHED per Q-3 — this is the component-local half only).
- FORBIDDEN: Domain/**, Application/** (except none), Content/**, scripts/**, tests/contract/**,
  docs/**, evals/** (except results), ui.csv (no new rows — the halt escape ships without an
  instructional string; voice is the TG sitting's), UiCsv*Tests, any existing test EDIT except
  the enumerated E-1 below, wrappers, Packages, ProjectSettings.
- EXCLUDED (declared, with owners): band reconciliation + F4 world-corners read-backs
  (A-UX1-5/G-4/R2-NEW-4 — its own separately-reviewed contract, never a composition edit);
  F5 TMP proxy upgrade (tranche-2 migration); F11 vocabulary-guard enrolment (test-hardening
  follow-up task); ResultsPanel attach (Q-3); shipped boot-flow change (Q-5 — tranche-2
  boot-to-Home contract); settings floor / motion-off OS read (G-1, tranche-2).
- E-1 (declared existing-test edit): NONE expected. The F-DEV-4 closure test
  (ChromeStateTests.cs:112-145) stays byte-untouched — its "no Try-again affordance on halt"
  assert remains TRUE because the escape is a chrome REGION, not a CTA view (criterion 4).
  If green requires editing ANY existing test, STOP (stop condition 1).

## Criteria

1. **Chrome attach.** `Wire` attaches `ScreenChromeController` + `HintChipController` to
   `root.gameObject` (the camera's host — the self-resolve pattern finds it; audit M-4) with
   `Attach(() => ScreenState)`. Test: real `Launch()` boot → both canvases exist AND each
   `Canvas.renderMode == ScreenSpaceCamera` (the M-4 regression pin), chrome at sortingOrder
   100 / hint 90 unchanged.
2. **Board-input gate.** `Wire` binds `Input.BoardInputActive = () => ScreenState == "Playing"
   && !ScreensVisible` where `ScreensVisible` is a GameRoot-owned bool that is true iff the
   dev screen flow (criterion 6) currently shows Home or LevelIntro (false in shipped boot —
   the predicate degenerates to the decompose's exact line). Tests: (a) real boot, disc tap
   resolves while Playing; (b) state forced off Playing (drive to FailureReview via the
   existing RunToFail shape) → disc scan returns -1 while the retry band still works;
   (c) with Home shown under the dev flag, disc scan returns -1 (the S2 conflict pin).
3. **MotionOffSource.** `Wire` AND `Retry` both bind `View.MotionOffSource = () => MotionOff`
   (#36 F1/F5: a Wire-only binding dies at first retry). Tests: real boot, toggle
   `MotionOffToggle` → BoardView motion path reads off; then `Retry()` → binding still live
   on the REBUILT view (the regression that motivated the ledger line).
4. **Halt escape (Q-2).** When `Update` enters Halted, GameRoot registers chrome region
   `"halt.escape"` (full-screen rect, priority 5) firing `Retry`; `Retry` unregisters it.
   No veil/CTA component edit; no new string. Tests: (a) drive a real halt (the F-DEV-4
   fixture shape at ChromeStateTests:112 — a NEW test, existing one untouched), tap center
   screen → `ScreenState == "Playing"`, tick 0, veil hidden, region count back to baseline;
   (b) the escape region does NOT exist while Playing/Won/FailureReview; (c) re-halt after
   escape → region re-registers (no duplicate-id throw — the Register/Unregister pairing law).
5. **OnDisable unregister law (R2-3, audit M-3).** W-1/W-2/W-3 as declared. Tests per
   component: Show → host `SetActive(false)` → region unregistered (count drops, resolve
   misses); reactivate + re-Show → no duplicate-id throw, region live again.
6. **Dev screen flow behind an explicit launch seam (S1/S9 resolution).** New
   `#if DEVELOPMENT_BUILD || UNITY_EDITOR` static `public static bool BootToHome` on GameRoot
   (default false; the DevLevelOverride precedent — a dev build's boot path, never shipped
   boot: Q-5 honored, `InitializeFromSeam` unchanged when false). When true at Wire-end:
   compose ONE `ScreensCanvas` (ScreenSpaceCamera on `Cam`, sortingOrder 120 — above results
   110), `HomeScreenView.Create + Attach(Input.Regions, () => MotionOff) + Show`,
   `LevelIntroSheet.Create + Attach(Input.Regions)`, a `ScreenStack` pushed "home", and the
   flow: `LevelSelected` → `intro.Show(levelName, deliveries)` + push "intro" ·
   `PlayRequested` → hide both, pop to empty, `ScreensVisible` false. `ScreensVisible` true
   iff stack non-empty. Deliveries/levelName come from the loaded level (no new I/O). Tests:
   flag on + `LaunchWith` → Home visible, board input gated (criterion 2c), pin tap →
   intro visible with substituted goal count, play tap → screens gone, input live, stack
   breadcrumb round-trips ["home","intro"] → []. Flag stays false → zero screen objects
   constructed (the shipped-boot pin).
7. **Hint counter.** `Wire` attaches hint with the state source (criterion 1 covers the
   attach); `ResetForNewLevel()` has NO call site until LoadNext exists — discharged as
   documented no-op here; test pins that `Retry()` does NOT reset the attempt count (the
   CM-UX-05 law that retry is the SAME level).
8. **Thinness (reviewable).** The GameRoot diff is additive composition only: no edit to the
   tick loop, halt branch semantics, FailKey, Won/FailureReview transitions, or any Session/
   Domain call. The reviewer verifies by reading the GameRoot diff hunk-by-hunk against this
   list; any hunk not traceable to criteria 1-7 is a finding.
9. **Suite + evidence.** Full committed-tree `scripts/check.sh` + `scripts/test.sh` green
   (wrapper N unchanged — this slice adds no wrapper); EditMode/PlayMode counts recorded
   red→green in the status log; per-criterion evidence table in the PR; visual verification
   (#33): with `BootToHome=true` in-editor capture — Home frame, LevelIntro frame, playing
   frame after Play — eyeballed AND committed under evals/results/ux/cm-ux-07/.

## Stop conditions

1. Any criterion seems to need an existing-test edit → stop (E-1 is empty by design).
2. Any criterion seems to need a ScreenState vocabulary change or Session/Domain edit → stop
   (that is the tranche-2 boot-flow contract, CM-UX-06-frozen-contract.md:110-113).
3. The screens' canvas/sort choice (120) collides with anything at the TG sitting → note for
   the human, do not redesign.
4. A component needs an edit beyond W-1/W-2/W-3 → stop and report.

## Status log

- 2026-08-07 — contract frozen at anchor 32dbecb; red next.
- 2026-08-07 — restatement (implementing session): turn the UX tranche ON by composing
  GameRoot.Wire/Retry over already-shipped components (ScreenChromeController, HintChipController,
  BoardView.MotionOffSource, ChromeRegions, HomeScreenView, LevelIntroSheet, ScreenStack) — no
  component behavior changes beyond the declared W-1/W-2/W-3 OnDisable mirrors. Assumptions (none
  load-bearing enough to stop on, all resolvable from the seam facts/contract text): (a) the halt
  escape rect is a literal full-screen `Rect(0,0,Screen.width,Screen.height)` — no pure-math helper
  needed since it is not safe-area-scoped; (b) `ScreensVisible` is DERIVED from `Stack.Count > 0`
  (single source of truth) rather than a separately-toggled bool, satisfying "true iff stack
  non-empty" literally; (c) no `.meta` files are needed for the evals/results PNGs since that path
  is outside `unity/Assets/` (confirmed: zero `.meta` siblings for any prior cm-ux-0X evidence
  PNG) — the task brief's "+.meta via a -quit import pass" is boilerplate that doesn't apply here,
  followed precedent instead.
- 2026-08-07 — RED: 14 new PlayMode tests written across
  `Tests/PlayMode/Bootstrap/{GameRootWiringTests,DevScreenFlowTests}.cs` (criteria 1,2,3,4,6,7) and
  three new `*OnDisableTests.cs` files (criterion 5, W-1/W-2/W-3). Captured against a temporary
  GameRoot "skeleton" (new properties/fields declared — `BootToHome`, `ScreensVisible` hardcoded
  false, `Home`/`Intro`/`Stack` — but Wire/Retry left unwired) plus the 3 component files reverted
  to their pre-contract HEAD state, so every new test compiled and failed on an ASSERTION, never a
  compile error: 11/14 failed for the right reason (chrome/hint absent, gate absent, MotionOffSource
  never bound, halt.escape never registered, OnDisable absent — see per-test messages in the PR);
  3/14 passed at red as documented P-7-style pins (unchanged merged behavior / vacuous absence
  controls with their own decoy positive-control asserts): `BoardInputGate_RealBoot_...`,
  `HaltEscapeRegion_AbsentDuringPlayingWonAndFailureReview`, `FlagFalse_ZeroScreenObjects...`.
- 2026-08-07 — GREEN: full composition landed in GameRoot.Wire/Retry/Update plus the dev-fenced
  `ComposeDevScreenFlow` (criterion 6) and the 3 component OnDisable mirrors (W-1/W-2/W-3). All 14
  new tests green (`GameRootWiringTests` 9/9, `DevScreenFlowTests` 2/2, the 3 OnDisable tests 1/1
  each). One real defect caught by the criterion-6 round-trip test and FIXED within the same
  composer (GameRoot-only, no component edit): `HomeScreenView`'s pin rect and `LevelIntroSheet`'s
  Play chip rect are both centered in the safe-area thumb band — the IDENTICAL point — so with Home
  still registered while Intro showed, `ChromeRegions`' earliest-registration tie-break routed the
  "Play" tap back to Home's pin (re-firing `LevelSelected`) instead of `PlayRequested`. Fixed by
  calling `Home.Hide()` at the `LevelSelected` step (a stack push navigates OFF Home — ScreenStack's
  own top-of-stack law), unregistering the pin before Intro's chip is the only thing in that spot;
  `PlayRequested`'s own `Home.Hide()` is now idempotent. Full committed-tree suite:
  `check.sh` OK; `test.sh` 13/13; `editmode.test.sh` EditMode 745/745 (unchanged), PlayMode 102/102
  (88 baseline + 14 new).
- 2026-08-07 — visual verification (#33 rule) discharged: uncommitted probe
  (`CmUx07VisualProbeTests.cs`, never staged) booted `GameRoot.Launch()` with `BootToHome=true`
  (real L001 seam) and rendered three ScreenSpaceCamera frames to Screen-matched RTs (640x480 batch
  host). Environment note: capturing under `-batchmode -nographics` produces a blank/uniform-gray
  frame — reproduced identically on the PRE-EXISTING CM-UX-02 capture rig run in isolation, so this
  is a host/flag artifact, not a defect; dropping `-nographics` (keeping `-batchmode`) restored real
  rendering, matching how the committed cm-ux-02..06 evidence must have been produced. Eyeballed:
  (1) Home — "Cat Metro" title top (a small red wave-preview chip overlaps the leading "C"), three
  grey parked-district silhouette rectangles, the L001 board visible behind/through Home (no
  backdrop panel — HomeScreenView ships none; a pre-existing CM-UX-06 component characteristic, not
  something this thin-wiring PR adds), and the navy pin + cream ring at the bottom, oversized/
  clipped on this tiny 640x480 host exactly as CM-UX-06's own evidence noted for the same reason.
  (2) LevelIntro — "First Switch" (L001's real name) + "Deliver 2 cats" (L001's real win.deliveries
  substituted) over a translucent navy sheet with the board visible through it, and a full-width
  "Play" chip in the thumb band; Home's title/pin are gone (the Home.Hide() fix confirmed visually).
  (3) Playing (post-Play) — both screens gone entirely; the raw L001 board (source, switch with its
  onboarding teach ring, red/blue stations, wave-preview chip) renders with no overlay chrome.
  Frames committed to `evals/results/ux/cm-ux-07/`; probe deleted before commit (never staged).
- 2026-08-07 — coordinator follow-up: merged main @ `5c87c19` (#44 "CM-UX-06 follow-up: modal-
  over-parent priority law + world-corners read-backs") into this branch (merge commit `0b6ab63`,
  clean, no conflicts). #44 raises `LevelIntroSheet.PlayRegionPriority` 0→10 (parents register at
  0, modals at 10) and adds `ScreenCoRegistrationTests` — an independent second layer over the
  same Home-pin/Intro-chip tie-break defect this PR's `Home.Hide()`-on-`LevelSelected` fix already
  closes; the two fixes are now belt-and-suspenders, confirmed non-conflicting (both green).
  D-2 (the delta audit's #45 precondition-assert law, #44 review F-1): audited all 14 new tests for
  implicit spatial/setup preconditions and added explicit `"precondition: ... — otherwise this test
  proves nothing"` asserts where a later assertion depended on an unstated geometric/count fact.
  Gains, by file (no existing test or non-test file touched):
  - `GameRootWiringTests.cs`: `BoardInputGate_OffPlaying_DiscMisses_RetryBandStillWorks` — disc
    position asserted above the retry band before relying on the gate (not the band) closing it;
    `BoardInputGate_HomeShownUnderDevFlag_DiscScanMisses` — disc position asserted outside Home's
    pin rect before relying on the gate (not the pin's chrome region) closing it;
    `HaltEscape_RealHalt_TapAnywhereRetries_RegionUnregisters` and
    `HaltEscape_ReHaltAfterEscape_ReRegisters_NoDuplicateIdThrow` — the tap point asserted inside
    the full-screen escape rect (geometrically invariant here, asserted anyway for consistency
    with the law); the re-halt test's region-count check was also strengthened from a loose
    `Is.GreaterThan(0)` to an explicit captured `baseline` + `Is.EqualTo(baseline + 1)` in three
    places (the "region counts assumed at a baseline" category).
  - `DevScreenFlowTests.cs`: the round-trip test — disc position asserted outside Home's pin rect
    (same shape as above); and, before the Play tap, an explicit assert that Intro's Play chip
    rect CONTAINS Home's pin center — the precondition that proves this test actually exercises
    the #44 modal-priority/`Home.Hide()` fix rather than tapping two things that never overlapped.
  - `HomeScreenViewOnDisableTests.cs`, `LevelIntroSheetOnDisableTests.cs`,
    `ResultsPanelOnDisableTests.cs` — each already had a "pre-condition:" style assert (registered
    count == 1 before disabling); reworded to the exact F-1 message format. No new assert needed
    beyond that (each fixture has exactly one registrant — no cross-region overlap risk to pin).
  Filtered re-run on the merged tree (PlayMode): `ChromeStateTests` 9/9, `DevScreenFlowTests` 2/2,
  `GameRootWiringTests` 9/9, `HomeScreenViewOnDisableTests` 1/1, `LevelIntroSheetOnDisableTests` 1/1,
  `ResultsPanelOnDisableTests` 1/1, `ScreenCoRegistrationTests` 2/2 — 25/25 total.
