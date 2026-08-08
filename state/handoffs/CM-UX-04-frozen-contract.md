# CONTRACT CM-UX-04 — Results panel v1: Won-state panel, one LOCKED `Next` CTA, structurally-empty footer, `NextRequested` seam

**Tranche:** UX tranche-1 slice 4 (`docs/ux/ux-layer-decompose.md` §2, CM-UX-04 row; postures
P-1..P-7 bind every criterion).
**DEPENDS-ON:** CM-UX-01 (#28) and CM-UX-02 (#35) — both on main at this branch's anchor
(ca13801). NEEDS-WIRING class: components + tests by direct construction over
`GameRoot.LaunchWith(fixture)`; **the panel is NOT attached by CM-UX-07 until level-advance
exists or the human rules otherwise (§6 Q-3, answered 2026-08-05: "Hold until LoadNext
exists")** — a rendered LOCKED `Next` that does nothing is a worse dead-end than today's banner.
**Human decisions inherited:** Q-6 = TMP/UGUI chrome (P-2) · Q-3 = hold the attach (above) ·
the #33 standing visual-verification rule (rendered frames as evidence).

### Goal

The win dead-end (`GameRoot.cs:194-197` — a Won run shows the banner forever) gets its
component-level closure: a `Won`-state results panel with **exactly one primary CTA** `Next`
(`results.next`, LOCKED) and a **structurally-empty footer**, hit-routed through CM-UX-01's
chrome-region registry (render-only chrome, P-1), raising a `NextRequested` seam that is a seam
ONLY — level advance is Bootstrap-owned and does not exist yet. No score/star/ticket content:
Domain score is Q-C-pinned at 0 and rendering it would fabricate data.

### Live-wiring & anti-vacuity rule (binds every criterion — inherited CM-UX-01/02 law)

Any test asserting on live objects drives REAL wiring (`GameRoot.LaunchWith`), never a bare
component. Every negative or absence assertion carries a positive control in the same fixture
that demonstrates the assertion is able to fail. A criterion whose check cannot fail is a defect.

### Spec reference

`docs/ux/ux-layer-decompose.md` §2 CM-UX-04 + §1 P-1..P-7 + §5 (count==1 is a structural
monetization tripwire) + §6 Q-3 · `docs/prd/ux-flows.md` §1.1 (band law, 48dp) · `docs/prd/PRD.md`
CM-R19.3 (one primary CTA), CM-R07.1/.4 (one gesture handler; interactive chrome bottom 25%;
≥48dp), TG-4 (results weight — honored structurally; arrangement sitting deferred until a footer
exists to arrange) · CM-UX-01 laws: resolution order (band → regions → discs), explicit region
priorities (A-UX1-3), registrars `Unregister` in `OnDestroy` (R1-F3) · CM-UX-02 laws: bounded
supersession of the controller's `Won` row (R1-F19 — MAY, not must; see A-UX4-1), `HudBands`
safe-area math + `UiChromeMaterial` binding + the P-4 per-slice literal guard ·
A11Y-GLOBAL-14 is COMMERCE-scoped and deliberately NOT claimed here (decompose review R1-F6).

### Acceptance criteria (9)

1. **The panel renders on `Won`, proven on TRANSITIONS.** `ResultsPanel` (Presentation,
   MonoBehaviour): `Attach(Func<string> screenState, ChromeRegions regions)`; state map:
   `Won` → panel visible · `Playing`/`FailureReview`/`Halted` → hidden. Renders react to the
   state source within one pumped frame (P-6 language). *Check:* PlayMode transition tests over
   `GameRoot.LaunchWith` — attach while `Playing`, assert nothing renders (pre-flip positive
   control), flip a test-controlled source to `Won`, pump one frame, assert the panel; flip back,
   assert it hides.
2. **The REAL Won transition shows the panel, with the wiring delegate shape.** A winnable
   fixture (`win.deliveries` reachable on the initial route, no taps) over
   `GameRoot.LaunchWith`, attached as `() => root.ScreenState` — the SAME delegate shape a
   future attach would bind, so a vocabulary mismatch fails here. On `Won`: panel visible within
   one pumped frame; the merged win banner is untouched (this slice edits no Bootstrap code).
   *Check:* PlayMode, the GreyboxTests advance recipe (`AdvanceMs(n * TICK_MS)` + frame pump).
3. **Exactly one primary CTA + structurally-empty footer — the count==1 invariant.** While the
   panel is shown over live wiring, `Regions.Count == 1` (the panel's `results.next` region and
   NOTHING else) AND the panel's footer container exists with ZERO children — one test asserting
   CM-R19.3 and TG-4's empty-footer posture together; this is §5's structural monetization
   tripwire (any future footer button forces this red). *Check:* PlayMode; positive controls: a
   decoy registration moves the count 1→2→1 and a decoy footer child moves childCount 0→1→0.
4. **The CTA is the LOCKED string at the floor, hit-routed through the registry ONLY.**
   `results.next` resolves "Next" via `UiStrings` (key-only). The chip is full-width in
   `HudBands.ThumbBand(safeArea)` (live `Screen.safeArea`/`Screen.dpi` reads on the runtime path
   only — the CM-UX-02 pattern); the registered region's rect provider returns the chip's live
   painted rect (region == painted; during `Won` the retry band predicate is false —
   `GameRoot.cs:127` — so the registry rect IS the tappable rect on this surface: no divergence
   zone, stated and asserted). 48dp floor: PlayMode on the live painted rect; EditMode on the
   component's pure chip-rect law with dpi INJECTED PER ROW (zero-inset, gesture-nav,
   nav+cutout, Pixel-9-Pro-class 486 dpi — the CM-UX-02 table discipline; no `Screen.*` read in
   any EditMode leg). *Check:* EditMode table over `ResultsPanel.ChipRect(safeArea)`; PlayMode —
   a tap at the chip center returns **-3** (region consumption), fires `NextRequested` exactly
   once, appends NO session command, flips NO committed route; positive control: after the panel
   hides, the same tap resolves as merged behavior.
5. **`NextRequested` is a seam only.** Level advance is Bootstrap-owned (§3 tranche-2
   `LoadNext`): the panel never touches the session, the level, or any scene state when the seam
   fires; a tap with no subscriber is a silent no-op, never a throw. *Check:* PlayMode — tap with
   no subscriber (no exception, still -3); tap with a subscriber (fires once); session command
   count and `ScreenState` unchanged by both.
6. **Registry lifetime law (CM-UX-01 R1-F3).** Register on show, `Unregister` on hide AND in
   `OnDestroy`; idempotent across repeated `SetVisible`/state flips (never a duplicate-id throw,
   never a leaked region). *Check:* PlayMode — count returns to baseline after hide; after
   destroying the panel's host object mid-show, the count returns to baseline (the
   destroyed-view/live-provider hazard R1-F3 names).
7. **Render-only tree (P-1/P-2), whitelist-walked.** The panel's full tree carries ONLY
   render-side types (Canvas/CanvasRenderer/RectTransform/Image/TMP + the panel's own view
   types); no EventSystem-family object, no `Selectable` subclass, no raycaster-driven
   interactivity; zero banned input-surface tokens in sources AND prose (the harness criterion-2
   statics stay green UNMODIFIED); TMP glyph-geometry renderability proxy (A-UX2-2 precedent).
   *Check:* PlayMode whitelist walk over the panel root with a decoy-Button positive control +
   the harness leg.
8. **Strings + content discipline.** `ui.csv` gains exactly ONE row, appended:
   `results.next,Next` (LOCKED). Rows 0-7 stay byte-identical; round-trip through
   `UiStrings.Get` byte-exact; the P-4 literal guard covers the new key (the quoted literal
   `"Next"` appears in NO Presentation Hud component source). **Declared amendment:**
   `UiCsvDisciplineTests` row-count 7→8 + the row-7 pin, exactly the evolution its own R1-L6
   comment sanctions — recorded here, never silent; no other existing-test edit of any kind.
   **No fabricated content:** the panel's rendered text set is exactly the one LOCKED CTA string
   (no score/stars/tickets — Q-C pins score at 0). *Check:* EditMode csv pins + literal guard;
   PlayMode — exactly one TMP text under the panel root and it equals `UiStrings.Get("results.next")`.
9. **A11y floor + zero drift.** Text + shape, never color-alone (structural proxy; the
   reviewer-signed greyscale checklist stays with the TG sitting — P-5 discipline); zero
   animation components under the panel root (motion-off removes nothing because nothing moves —
   the CM-UX-02 criterion-5 structural posture) with a decoy-Animator control. Suites green at
   the anchor's re-derived counts (delta = this slice's tests only; the single declared
   amendment in criterion 8 aside, zero existing-test edits); `bash scripts/test.sh` all
   wrappers green (12 at anchor, N-relative — this slice adds no wrapper); no
   GameRoot/Bootstrap/Content/Domain/scripts/wrapper/`unity/Packages/**` edits. *Check:*
   headless runs + harness + diff surface.

### Scope boundary

**In scope:** `unity/Assets/Scripts/Presentation/Hud/ResultsPanel.cs` (+ .meta) · **append-only**
`ui.csv` row · new tests under `unity/Assets/Tests/EditMode/Presentation/**` and
`unity/Assets/Tests/PlayMode/Hud/**` (+ .meta) · the criterion-8 declared amendment to
`UiCsvDisciplineTests.cs` · `state/handoffs/CM-UX-04-frozen-contract.md` +
`state/handoffs/CM-UX-04.md` · the single `state/PROJECT_STATE.md` row.

**Explicit non-goals:** no `ScreenChromeController` edit (A-UX4-1); no attach/wiring anywhere
(CM-UX-07 + Q-3's ruling own it); no `LoadNext`/level-advance behavior; no score/star/ticket
rendering (Q-C); no second CTA, footer content, share/shop/commerce anything (`PRD.md:208`
embargo — criterion 3 is its tripwire); no TapInput/ChromeRegions/HudBands/ChromeGeometry
edits; no Bootstrap/GameRoot/Content/Domain/scripts/wrapper edits; no edits to existing ui.csv
rows; no `unity/Packages/**` change of any kind; no scene edits; no Halted/FailureReview
semantics.

### Assumptions

- **A-UX4-1** CM-UX-02's bounded supersession says CM-UX-04 MAY amend the controller's `Won`
  row — this slice deliberately does NOT: riding the controller would attach the panel the
  moment CM-UX-07 attaches the controller, violating Q-3's hold. `ResultsPanel` is standalone
  (own canvas, the controller's construction pattern); the controller's merged `Won`-renders-
  neither row stays true and untouched. The `Won`-row amendment happens at attach time, under
  Q-3's ruling.
- **A-UX4-2** The panel registers a region because `Next` is interactive chrome and P-1 routes
  every hit through the TapInput registry; during `Won` the retry band is inactive
  (`RetryRegionActive` = FailureReview-only, `GameRoot.cs:127`), so the region resolves without
  band interference. CM-UX-02's "first registrar is CM-UX-06" note is about the first WIRED
  registrar in a shipped build — this panel's registration runs only under direct construction
  until Q-3's attach ruling.
- **A-UX4-3** Region priority is explicit (A-UX1-3): the panel registers at a named constant
  priority; no reliance on registration order.
- **A-UX4-4** The winnable fixture reaches `Won` through the real sim on the initial route
  (deliveries=1, one red cat routed to the red station) — no constructed state, the GreyboxTests
  Win recipe.
- **A-UX4-5** Guard/pin tests that are green on arrival (the literal guard, the csv base-row
  pins) are labeled as such (P-7).

### Stop conditions

Defaults (AGENTS.md) plus:
1. Any criterion appears to require an `EventSystem`, `Selectable`, raycaster interactivity, or
   a second input-package consumer → stop; gate evolution needs the human.
2. The count==1 invariant cannot hold without touching TapInput/ChromeRegions → stop (this
   tranche's only TapInput edit was CM-UX-01's).
3. Harness criterion-2 statics fail for any reason other than this slice's own prose → stop;
   never edit the wrapper.
4. Anything requires a Bootstrap/GameRoot edit or a controller amendment → stop; it belongs to
   the attach-time contract under Q-3.
5. The device session merges a Presentation edit mid-slice → stop, rebase, re-verify the
   CM-UX-01/02 pins before continuing.
6. `ui.csv` needs anything beyond the single appended row → stop (copy set is the decompose's).
