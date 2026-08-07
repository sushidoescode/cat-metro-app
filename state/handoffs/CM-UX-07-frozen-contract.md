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
