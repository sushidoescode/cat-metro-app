# CONTRACT CM-LOADNEXT — close the gameplay loop: Won → ResultsPanel → Next → the next level

Frozen at branch anchor `03b6de2` (origin/main), FIRST commit on
`task/CM-LOADNEXT-won-flow`, before any code. Immutable across the branch except the Status log
section at the bottom (the CM-UX-07-frozen-contract.md freeze pattern, `#36`-ruled-sufficient
mechanism). Worktree: a fresh checkout at this anchor; Unity Library is COLD (first PlayMode run
pays the import).

## What this discharges (restated in my own words)

1. `state/handoffs/CM-UX-04-frozen-contract.md` + `state/handoffs/CM-UX-04.md`: `ResultsPanel`
   shipped DORMANT — built, tested, but never attached to `GameRoot` (Q-3: "Hold until LoadNext
   exists" — a rendered LOCKED `Next` with nothing behind it is a worse dead-end than the banner
   it replaces). `CM-UX-04.md:43`: "Panel activation is the LoadNext contract's single line."
   This contract IS that line, plus everything the line makes reachable for the first time.
2. `state/handoffs/CM-UX-07-delta-audit.md` §D-1: once `ResultsPanel` attaches, its
   `results.next` region (priority 10) and `LevelIntroSheet`'s `intro.play` region (also
   priority 10) can co-register in ONE `ChromeRegions` — CM-UX-07's own `Q-3` dormancy kept this
   UNREACHABLE at that contract's time; it is reachable now. I own defining and testing the
   ordering law so that co-registration is deterministic (never a registration-order accident)
   and a tap never double-fires. I also absorb `#46-F9`: the halt-escape's inline priority
   literal `5` becomes a named const in a documented priority ladder, applied consistently.
3. `unity/Assets/Scripts/Bootstrap/GameRoot.cs` — the Won transition (`ScreenState = "Won"` at
   the real sim outcome, `Banner.ShowKey("win.banner")`, otherwise nothing), `Retry()` (same
   level, re-simulation from tick 0, ADR-0002 §9), the `Wire` composition (chrome/hint attach,
   the `#46` review-F5 AddComponent guard style, the dev-only `BootToHome` screen flow), and how
   `Launch(levelPath)`/`LaunchWith(level)` load a level through the `StreamingAssetsContentSource`
   seam. `unity/Assets/Scripts/Presentation/Hud/ResultsPanel.cs` — its `Attach`/`Apply`
   self-healing registration and the `NextRequested` seam (currently a true no-op: "the panel
   never touches the session, the level, or any scene state when the seam fires").
   `content/levels/L001..L005.json` — the campaign band, ids exactly `"L001"`.."L005"`, staged
   byte-identically under `unity/Assets/StreamingAssets/content/levels/`.
4. `UiCsvDisciplineTests` + `ui.csv`: row 8 `results.next,Next` already exists (LOCKED). I need
   NO new UI copy — confirmed by inspection; if that turns out false I stop and report (it did
   not turn out false).

## Restated contract (my own words)

Today, winning a level shows the merged "All cats home!" banner over a permanently frozen
board — no way forward without quitting the app. This contract makes the loop close: `GameRoot`
composes `ResultsPanel` (the single attach line CM-UX-04 deferred); on the real `Won` transition
the panel shows its one LOCKED `Next` chip; tapping it loads the next level in the band
(`L001→L002→L003→L004→L005`, wrapping to `L001` at the end — an explicit, flagged ASSUMPTION,
see below) through the same real `StreamingAssetsContentSource`/`LevelImporter` seam `Launch`
uses, and play resumes at tick 0 on the new level. The two-priority-10-modals collision CM-UX-07
foresaw becomes reachable the moment the attach line lands; I define a durable priority ladder
so it resolves deterministically. Every state transition the panel's new presence creates
(Retry during Won, Next during FailureReview, the halt-escape/Next mutual exclusion, shipped
boot staying `L001`) gets an explicit, named test — not left implicit.

## ASSUMPTION flagged prominently — human to ratify or override

**End-of-band policy: WRAP to `L001`.** `docs/ux/ux-layer-decompose.md:169` explicitly defers
"level progression policy" (save-backed unlocks, campaign gating) to later work — this contract
only needs an INTERIM runtime policy so `Next` always does something sensible today. I chose
wrap-to-`L001` (a demo-friendly infinite loop) over "stop at L005 with no CTA" or "re-show the
banner" because those alternatives reintroduce the exact dead-end this contract exists to close.
**This is a guess about product intent, not a derived fact — the human may want a different
policy** (e.g. a "you beat the demo" screen, a stop, or a save-gated unlock). The seam is
DELIBERATELY narrow: `GameRoot.WrapAtEndOfBand` is a single private `const bool`; flipping it
(or replacing `NextLevelId`'s body) is the one-line/one-method edit that changes the policy
without touching anything else. `GameRoot.LevelBand` (`{"L001".."L005"}`) is the other half of
the seam — the band order itself.

## Seam audit (what I read, to confirm no other assumption is load-bearing-and-unconfirmed)

- Band ids: confirmed exactly `"L001".."L005"` in both `content/levels/*.json` and the staged
  `unity/Assets/StreamingAssets/content/levels/*.json` (byte-identical, CM-C10's gate).
- `GameRoot.ScreenState` vocabulary, `Retry()`, `Update()`'s halt/Won/FailureReview branches:
  read, NOT edited beyond the declared LoadLevel refactor below — no Session/Domain call changes.
- `ResultsPanel`'s state map, registration lifetime law, and render-only tree are UNCHANGED —
  this contract only supplies the attach line and the `NextRequested` subscriber; the panel's
  own `.cs` file gains a one-line const-sourcing change only (see Declared amendments).
- `LevelIntroSheet`/`HomeScreenView`: read for the D-1 law. `HomeScreenView.PinRegionPriority`
  stays untouched (already `0`, matches the ladder's `ParentPriority`) — I do not rewire it to
  the shared const class; see Known-but-not-fixed below for why this is a deliberate, narrower
  edit than "make everything consistent."
- `state/handoffs/CM-UX-05.md:73`: an explicit forward obligation on record — "call
  `hint.ResetForNewLevel()` wherever a NEW level loads (LoadNext, when it exists)." I wire this;
  it is IN SCOPE by this record, not scope creep.

## Acceptance criteria

1. **The single attach line.** `GameRoot.Wire` composes `ResultsPanel` onto `root.gameObject`,
   mirroring the chrome/hint attach pattern exactly: guarded (`#46` review-F5 style — a
   pre-attached instance survives Wire as the single instance, never a stacked duplicate),
   `Attach(() => ScreenState, Input.Regions)`, `NextRequested = LoadNext`. *Check:* real
   `GameRoot.Launch()` → `GetComponent<ResultsPanel>()` non-null, its canvas
   `renderMode == ScreenSpaceCamera` (M-4-style pin) at `sortingOrder 110` (unchanged); a
   pre-attached `ResultsPanel` survives as the single instance (F5-style guard test).
2. **Won shows the panel with Next unlocked; F-DEV-4 stays true untouched.** A real winnable
   fixture over `GameRoot.LaunchWith`, zero taps, reaches `Won` — the panel renders within one
   pumped frame, exactly as `ResultsPanelTests.cs` already proves for direct construction, now
   proven again for the REAL Wire-attached instance. No edit to `ScreenChromeController`/
   `HaltVeilView`/the F-DEV-4 closure test — halt still renders no CTA (verified by the full
   suite staying green, not a new test, since nothing here can plausibly touch that surface).
3. **`NextRequested` → progression, through the real seam, band order + the WRAP assumption.**
   Tapping the chip during `Won` loads `content/levels/<next-id>.json` through
   `StreamingAssetsContentSource`/`LevelImporter` (the `SEAM_LOADED` log line, `Launch`'s own
   proof pattern), rebuilds `Session`/`View`/`Input`/`Preview` fresh (tick 0, `Playing`), hides
   the banner, and resets the hint attempt counter (CM-UX-05's forward obligation) — Retry's own
   "same level never resets" law stays untouched (a named, separate test pins both directions).
   *Check:* real end-to-end PlayMode test, L001→L002; a second real end-to-end test, L005→L001
   (the wrap, through the real seam, not just the pure id function); an EditMode pure-logic
   table over the whole band + the wrap + an unknown-id fallback.
4. **The D-1 ordering law, named consts, red-power co-registration test.** A priority ladder
   (`ChromeRegions.ParentPriority=0`, `HaltEscapePriority=5`, `ModalPriority=10`,
   `StackedModalPriority=11`) replaces the halt-escape's inline literal `5` (`#46-F9`) and
   `LevelIntroSheet.PlayRegionPriority` (`ModalPriority→StackedModalPriority`, i.e. `10→11`) —
   `ResultsPanel.RegionPriority` stays the pinned value `10`, now sourced from `ModalPriority`.
   Law: the ScreenStack-hosted modal (`LevelIntroSheet`, whose `ScreensCanvas` paints at
   `sortingOrder 120`) always wins a tap over the standalone `ResultsPanel` (canvas
   `sortingOrder 110`) wherever both are registered — the tap law matches the paint law.
   *Check:* two PlayMode tests, direct construction (the `ScreenCoRegistrationTests.cs`
   precedent), proving the law holds under BOTH registration orders — the discriminating case
   (Results registers first) is the actual red-power proof; a tie-break-only implementation
   fails it.
5. **Every reachable state transition the panel's presence creates is pinned.** Retry() called
   directly during Won (not reachable via any wired UI — no CTA fires it there — but Retry
   itself carries no ScreenState guard; pinned defensively): restarts the SAME level, panel
   self-heals hidden. LoadNext() called directly during FailureReview (also not reachable via
   any wired UI — `results.next` only registers on Won — but LoadNext carries no ScreenState
   guard either, by deliberate symmetry with Retry: gating lives at the registration layer, not
   inside the action method): still progresses. The thumb-band tap during Won resolves to Next,
   never the legacy retry verb (the retry-band predicate is FailureReview-only, unaffected by
   this contract, re-pinned here since the panel's chip now lives at that exact geometry).
6. **Q-5 law: shipped boot stays L001.** A real `GameRoot.Launch()` (no `BootToHome`, no
   `LoadNext` call) starts at `CurrentLevelId == "L001"` — progression is post-win only, and
   this is a regression pin against a future mistake of routing boot through the band.
7. **Declared existing-test amendments (see below) — all sharpened, none weakened.**
8. **Visual verification (the standing #33 rule).** Uncommitted probe (never staged): capture
   the real Won-with-panel frame (Next chip visible, board + banner dimmed behind the scrim) and
   the post-Next next-level frame (a different level's board, no panel, no banner). PNGs to the
   scratchpad dir; eyeballed and described in my report; land via a follow-up evidence PR at
   review time (this contract's own PR carries no PNGs, per the established pattern — see
   CM-UX-05/CM-UX-07's status logs).
9. **Suites green, mutation-provable.** Filtered PlayMode + EditMode over every touched/new test
   class; then full `EditMode`/`PlayMode`; then `bash scripts/check.sh && bash scripts/test.sh`.
   Mutation proofs (each reverted byte-clean, `git diff` checked empty after): delete the attach
   line → the criterion-1/2 panel tests RED; revert `LevelIntroSheet.PlayRegionPriority` to
   `ModalPriority` (10) → the D-1 discriminating test RED; break `NextLevelId` to return its
   input unchanged → the flagship progression test RED (asserts `CurrentLevelId == "L002"`, a
   same-level bug reads `"L001"`).

## Declared existing-test amendments (sharpened, never weakened — named here per repo convention)

- **DE-1 (`GameRootWiringTests.cs`, `HaltEscapeRegion_AbsentDuringPlayingWonAndFailureReview`):**
  the Won leg's `Regions.Count == 0` assertion is now stale BY THIS CONTRACT'S OWN INTENDED
  EFFECT (Won now legitimately registers `results.next`). Replaced with a STRICTER, more precise
  check: `Regions.Count == 1` (exactly the panel's own region) PLUS a live duplicate-id proof
  that `"halt.escape"` specifically is not registered (`Assert.DoesNotThrow` on registering that
  exact id, then unregistering it) — the test's actual point ("no halt.escape region outside
  Halted") survives byte-for-byte in spirit, sharpened in mechanism.
- **DE-2 (`ResultsPanelTests.cs`, the shared `AttachControlled` helper):** `GameRoot.Wire` now
  attaches its own `ResultsPanel`; the helper's old `AddComponent<ResultsPanel>()` would stack a
  SECOND panel on the same GameObject. Fixed by resolving the Wire-attached instance and
  rebinding its state source — the exact `CM-TESTFIX`/`ChromeStateTests.AttachControlled`
  precedent, extended to `ResultsPanel` now that it is Wire-attached. The helper also clears the
  Wire-installed `NextRequested = LoadNext` back to `null` — without this, every chip-tap test in
  the file would silently trigger REAL progression (file I/O, a session swap) as a hidden side
  effect of tapping the chip while `_state` is test-controlled; a new, named test pins this
  clearing explicitly (`AttachControlled_IsIsolatedFromRealProgression_...`).
- **DE-3 (`ResultsPanelTests.cs`, `RealWin_ShowsPanel_WithTheWiringDelegateShape` and
  `CaptureEvidence_ResultsFrame_WhenRequested`):** both manually added a SECOND `ResultsPanel`
  bound to `() => _root.ScreenState` — the SAME delegate shape `Wire` now uses for real. Once
  attached, both the manual and the Wire-attached panel would independently try to register
  `"results.next"` the moment the real sim reaches Won, throwing a duplicate-id
  `ArgumentException`. Fixed by resolving the Wire-attached instance instead (the rehearsal these
  tests performed by hand is now the real composition — strictly more representative, not
  weaker).

## Scope boundary

**In scope:** `unity/Assets/Scripts/Bootstrap/GameRoot.cs` (Wire attach line, `LoadLevel`
extraction, `LoadNext`, `NextLevelId`/`LevelPath`/`LevelBand`/`WrapAtEndOfBand`,
`CurrentLevelId`, halt-escape priority const-sourcing) · `unity/Assets/Scripts/Presentation/Hud/ResultsPanel.cs`
(one-line const-sourcing of `RegionPriority`, value unchanged) ·
`unity/Assets/Scripts/Presentation/Screens/LevelIntroSheet.cs` (one-line const-sourcing of
`PlayRegionPriority`, value `10→11`) · `unity/Assets/Scripts/Presentation/Input/ChromeRegions.cs`
(new named priority consts, zero behavior change) · new tests under
`unity/Assets/Tests/EditMode/Engine/**` and `unity/Assets/Tests/PlayMode/{Bootstrap,Screens}/**`
(+ `.meta`) · the three declared amendments (DE-1/2/3) in `GameRootWiringTests.cs` and
`ResultsPanelTests.cs` · `state/handoffs/CM-LOADNEXT-frozen-contract.md` · the single
`state/PROJECT_STATE.md` row.

**Explicit non-goals:** no save-backed unlock/progression policy (deferred per the decompose);
no score/star/ticket rendering (Q-C stays pinned, untouched); no second CTA or footer content
(the count==1 tripwire stays); no `HomeScreenView.PinRegionPriority` rewiring to the shared const
class (value unchanged at `0`; touching that file is not required to close D-1 — see Known debt);
no fix for the separate, NOT-explicitly-scoped Results-vs-Home tap collision in the dev
`BootToHome` flow (see Known debt — flagged, not fixed); no ScreenChromeController/HaltVeilView
edits; no `ui.csv`/`UiCsvDisciplineTests` edits (no new copy needed); no scene edits; no
`unity/Packages/**` change.

## Known debt discovered but NOT fixed here (scope discipline — flagged for the record)

- **Results-vs-Home tap collision in the dev `BootToHome` flow.** `GameRoot.Update()` advances
  the simulation whenever `ScreenState == "Playing"`, REGARDLESS of `ScreensVisible` — so in the
  dev-only Home→Intro flow, the board can reach a real `Won` while `Home` is still showing (its
  pin still registered at `ParentPriority=0`). Since `ResultsPanel` registers at `ModalPriority`
  (10) — correctly ABOVE `Home`'s parent tier by the modal-over-parent law — a tap on the exact
  spot Home's pin occupies would resolve to `Next`, even though `Home`'s screen paints ABOVE
  `ResultsPanel` (`ScreensCanvas` sortingOrder 120 vs 110): the same "tap what you see resolves
  to something invisible" class of bug D-1 names, but for the Home pairing, not the Intro one.
  D-1's own text and its source audit scope the ordering-law obligation to the
  Results-vs-Intro (modal-vs-modal) collision specifically; Home-vs-Results is a DIFFERENT,
  not-named pairing, reachable only in the dev-only `BootToHome` flow via an unusual sequence
  (the sim winning entirely before the player clears the Home screen). I am not fixing it here —
  flagged for a follow-up contract or an explicit human ruling on whether `ResultsPanel` should
  ever coexist with a live `Home` pin at all.
- `HomeScreenView.PinRegionPriority` stays a local literal `0`, not sourced from the new shared
  `ChromeRegions.ParentPriority` const — value-identical, so no behavior risk, but the "ladder
  applied consistently" claim is narrower than "every priority literal in the codebase," by
  choice (touching `HomeScreenView.cs` is not required to close D-1 as scoped).

## Assumptions

- **A-LN-1** (the flagged ASSUMPTION above): end-of-band wraps to `L001`.
- **A-LN-2** An id outside the band (`_level.Dto.Id` from a `LaunchWith` test fixture, or a
  future `DevLevelOverride`d level) restarts the band at `L001` rather than throwing — pure
  logic, pinned by its own EditMode test, never silently guessed.
- **A-LN-3** `LoadNext()`/`Retry()` carry no `ScreenState` guard of their own (only `Session !=
  null`) — gating which UI can reach them lives entirely at the registration layer
  (`ResultsPanel`/`ChromeRegions`), matching `Retry()`'s own pre-existing shape. Criterion 5
  pins this as a stated law, not an implicit accident.
- **A-LN-4** The winnable fixtures used in new tests set `"id"` to a real band id (e.g. `"L001"`)
  so `NextLevelId` exercises the true band through the real seam, rather than the unknown-id
  fallback — mirrors the CM-UX-04 `WinnableFixtureJson` precedent, parameterized.

## Stop conditions

Defaults (AGENTS.md) plus:
1. Any criterion needs a save-backed/persisted progression policy → stop; that is explicitly
   deferred (decompose §5) and out of this contract's reach.
2. `ui.csv` needs any new row → stop and report (checked; not expected).
3. The D-1 law cannot hold without editing `ChromeRegions`' resolution algorithm itself (not
   just its named priority values) → stop; that is a TapInput/ChromeRegions architecture change.
4. Fixing the Results-vs-Home collision (Known debt above) turns out to be REQUIRED for any
   stated acceptance criterion to pass → stop and report; as scoped, it should not be.

## Status log

- 2026-08-08 — contract frozen at anchor `03b6de2`; red next.
- 2026-08-08 — SEQUENCING NOTE (deviation from a literal pre-green natural red phase, disclosed):
  because the attach line, `LoadLevel`/`LoadNext` refactor, the priority-ladder consts, and the
  `LevelIntroSheet` priority bump are tightly interdependent (tests for one reference symbols the
  others introduce), I implemented all production code in one pass rather than staging it behind
  a compiling-but-stubbed skeleton (the CM-UX-07 precedent). I then performed the three named
  MUTATION PROOFS as explicit, isolated red→green→revert cycles instead — which is what
  criterion 9 asks for as its own deliverable regardless of sequencing. Each is below.
- 2026-08-08 — GREEN (first full pass, before mutation proofs): filtered PlayMode
  (`GameRootWiringTests`, `ResultsPanelTests`, `ResultsPanelVsIntroOrderingTests`,
  `LoadNextTests`) 33/33; filtered EditMode (`LoadNextBandTests`) 9/9 — all passed on the first
  run, confirming the design was correct end-to-end before any mutation testing.
- 2026-08-08 — MUTATION PROOF 1 (criterion 1, the single attach line): removed the
  `ResultsPanel` attach block from `GameRoot.Wire` → RED, 16 tests failed across
  `GameRootWiringTests`/`ResultsPanelTests`/`LoadNextTests` (filtered run, 31 total), every
  failure an assertion (`panel != null` etc.), never a compile error. Reverted; re-ran filtered
  → 33/33 green again; `git diff` confirmed no stray artifact of the mutation remained
  (`grep -n "MUTATION PROOF" GameRoot.cs` → no matches).
- 2026-08-08 — MUTATION PROOF 2 (criterion 4, the D-1 ordering law): reverted
  `LevelIntroSheet.PlayRegionPriority` to `ChromeRegions.ModalPriority` (the tied value, 10) →
  RED on exactly the discriminating test, `IntroWins_OverResults_EvenWhenResultsRegisteredFirst`
  (Expected 1, was 0 — Results won the tie by registering first); the consistency-check test
  (`...WhenIntroRegisteredFirst`) stayed GREEN, confirming it alone could not have caught this
  and the discriminating test is the real red-power proof. Reverted; re-ran → 2/2 green.
- 2026-08-08 — MUTATION PROOF 3 (criterion 3, progression): changed `NextLevelId` to
  `return currentId;` (same-level bug) → RED: 3/6 `LoadNextTests` failed (the ones asserting a
  landing-level change — `RealWin_TapNext_...`, `RealWin_AtEndOfBand_...`,
  `LoadNext_CalledDirectly_DuringFailureReview_...`; the SEAM_LOADED log for the new file never
  appeared) while the 3 that don't check the landing id stayed green, as expected; ALL 9
  `LoadNextBandTests` (EditMode) also went RED (band/wrap/unknown-id logic is now vacuous).
  Reverted; re-ran → PlayMode 6/6, EditMode 9/9 green.
- 2026-08-08 — GREEN, full suite (post-mutation-proofs, final tree): EditMode 777/777, PlayMode
  131/131 (`bash scripts/test.sh` run twice at this point — once with the uncommitted visual
  probe present, 132/132, once after its deletion, 131/131, both `test: 14/14 passed`);
  `bash scripts/check.sh` OK. Unity run unsandboxed per the recorded licensing-daemon precedent
  (sandboxed batchmode fails with "attempt to write a readonly database").
- 2026-08-08 — visual verification (#33 rule) discharged: uncommitted probe
  (`CmLoadNextVisualProbeTests.cs`, never staged, deleted before commit along with its `.meta`)
  drove a controlled winnable fixture (id `"L001"`) through `GameRoot.LaunchWith` to a real Won,
  captured the panel frame, tapped `Next`, captured the post-progression frame. Both rendered
  (ScreenSpaceCamera, `-batchmode` without `-nographics`, the CM-UX-07 precedent) and eyeballed:
  (1) `01-won-with-panel.png` — the merged "All cats home!" banner (world-space, visibly dimmed
  by the panel's scrim per the documented ~25% darkening), the greybox board (source, switch
  ring, red/blue stations) behind it, and a full-width dark-green "Next" chip in the bottom
  thumb band — exactly CM-UX-04's documented shape, now with the CTA finally live. (2)
  `02-post-next-level.png` — no banner, no panel, no chip; a freshly-built board with TWO
  wave-preview chips (red and blue, both showing the "≈?" unknown-color glyph) at the top —
  visually confirms REAL L002 content loaded (a colour-split level, not a repeat of the same
  4-node fixture), i.e. progression genuinely advanced through the real seam, not a same-level
  no-op. Frames live at the session scratchpad
  (`/private/tmp/claude-501/.../scratchpad/loadnext-visual/`), not committed — land via a
  follow-up evidence PR at review time per the CM-UX-05/CM-UX-07 precedent. Methodology note:
  the Won frame uses a controlled fixture (not the real shipped L001 content) because L001's own
  zero-tap winnability was not verified within this contract's time budget; the post-Next frame
  IS the real shipped L002 content, read through the real StreamingAssets seam — the SAME
  progression path the flagship PlayMode test (`RealWin_TapNext_...`) proves with assertions.
- 2026-08-08 — scope hygiene: `dotnet/CatMetro.DailyTools/packages.lock.json` was touched by
  `dotnet test`/`restore` during `scripts/test.sh` runs (an unrelated dependency-graph
  regeneration, `catmetro.services`'s `Newtonsoft.Json` transitive dep annotation) — reverted via
  `git checkout --`, out of this contract's scope, never staged.
