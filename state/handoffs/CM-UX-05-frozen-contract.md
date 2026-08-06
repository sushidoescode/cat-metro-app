# CONTRACT CM-UX-05 — Hint chip + attempt counter (NEEDS-WIRING · gate-untouched)

**Tranche:** UX tranche-1 slice 5 (`docs/ux/ux-layer-decompose.md` §2).
**Anchor:** origin/main ca13801 (CM-UX-02 merged, #35). Wrapper baseline at anchor: **N = 12**
(`find tests -name '*.test.sh' | wc -l`); this slice adds NO wrapper — counts stay N-relative.
**DEPENDS-ON:** CM-UX-01 (#28, merged) + CM-UX-02 (#35, merged) — both on the anchor. No
in-flight dependency: this slice needs no DEVFIX-style wait (chrome stack + TMP already merged).
**NEEDS-WIRING:** components + direct-construction tests only; attachment is CM-UX-07's
enumerated line (`attach HintChipController with () => root.ScreenState; reset on level load`).
No Bootstrap/** or GameRoot.cs edit of any kind (P-3).

### Goal

CM-R13.5's hint fallback exists at the component level: a per-level attempt-run counter counts
**FailureReview entries** (edge-triggered, Halted-blind), and after the **2nd** entry a
render-only hint chip — the only sanctioned tutorial text (S-01 flow node L) — renders
board-edge above the thumb band with one DRAFT csv line, on the merged TMP/UGUI chrome stack,
registering zero input targets.

### Standing honesty clause (decompose, binding)

L001 cannot reach FailureReview (F-DEV-3), so the chip is device-reachable only on L002/L003
until F-DEV-3 or Q-B resolves — **this slice makes no L001 claim and no PR/evidence line may
re-sell the chip against L001.** Halted edges are NOT counted: counting them would pre-decide
Q-B/NEW-Q4. `tutorial_step.retries` (CM-R13.5's analytics leg) is Application-layer/device-lane
work — named here as deferred, not absorbed.

### Live-wiring & anti-vacuity rule (inherited from CM-UX-01/02, binds every criterion)

PlayMode assertions drive REAL wiring (`GameRoot.LaunchWith(fixture)`), never a bare component.
Every negative or absence assertion carries a positive control in the same fixture (a decoy
registration, a decoy banned component, a pre-flip state check). A criterion whose check cannot
fail is a defect.

### Spec reference

`docs/prd/PRD.md` CM-R13.5 (hint after 2 fails on any tutorial level, from the string table),
CM-R13.1 (zero tutorial text — the chip is the argued exemption; CM-UX-03's exemption list
carries it from day one per decompose R1-F8) · `docs/prd/ux-flows.md` S-01 flow node L
(fail x2 → hint chip, 1 line → back to play), §1.1 (band law; interactive floor 48dp),
A11Y-S01-4 (the chip is interactive-grade dimensionally even though v1 registers no target),
A11Y-S01-5 (live-region hook only — TalkBack pass stays UX-OPEN-11) ·
`docs/ux/ux-layer-decompose.md` P-1..P-7, §4 (chip copy + placement to the TG-5/eyeball
sitting) · CM-UX-02 merged laws: state-source delegate shape (`Func<string>`), screen-state
literals carried in Presentation (Bootstrap unreferenced), UiChromeMaterial binding,
R1-L6 (`UiCsvDisciplineTests` row-count bound is slice-scoped and amendable by a later
append-slice as DECLARED evolution), R1-L7 (re-layout only on input change).

### Acceptance criteria (7)

1. **Attempt counting is pure, edge-triggered, and Halted-blind.** `HintAttemptCounter`
   (pure C#, no UnityEngine): `Observe(state)` increments only on a transition INTO
   `FailureReview`; consecutive FailureReview observations count once; `Halted` and `Won`
   entries never increment; `Reset()` re-arms at zero (the per-level attempt-run seam —
   CM-UX-07/level-advance calls it; a first observation that is already FailureReview is an
   entry). *Check:* red-first EditMode tests — entry, dedupe, two-entry round-trip,
   Halted/Won negatives with an in-fixture positive control, reset-and-re-arm.
2. **The chip renders after the 2nd FailureReview entry, within one pumped frame, and
   persists into the retry.** `HintChipController.Attach(Func<string> screenState)` (the
   merged delegate shape); visibility law: `Count >= 2 AND state ∈ {Playing, FailureReview}`
   (node L returns the player to play WITH the hint). Hidden by default: zero entries render
   nothing — the CM-R13.1 posture. *Check:* PlayMode transitions over real wiring: hidden at
   attach and after the 1st entry (pre-flip positive controls), visible within one pumped
   frame of the 2nd entry, still visible on the return to Playing, hidden + re-armed after
   `ResetForNewLevel()`; chip text resolves through `UiStrings.Get("hint.tutorial")`.
3. **Halted never renders the chip and never counts.** With the chip visible (count ≥ 2),
   flipping the source to `Halted` hides it within one pumped frame and leaves the count
   unchanged; returning to Playing restores it (information removed from the halt surface,
   never destroyed). The `Halted` literal is the merged vocabulary CM-UX-02's real-halt test
   already pinned against the wiring delegate shape. *Check:* PlayMode round-trip with count
   asserts on both edges.
4. **Placement pinned: board-edge above the thumb band, ≥48dp height — DRAFT for the
   eyeball.** Pure `HintChipView.ChipRect(safeArea, dpi)`: full safe-area width, bottom edge
   exactly `HudBands.ThumbBand(safeArea).yMax`, height exactly 48dp at the injected dpi —
   `MeetsMinTargetPx` holds by construction (A11Y-S01-4 honored dimensionally; v1 registers
   no target; placement flagged DRAFT for the §4 sitting). Live `Screen.safeArea`/`Screen.dpi`
   reads exist ONLY in the view layer, injected into the pure math (A-UX1-5); re-layout only
   when safeArea or dpi changes (R1-L7). *Check:* EditMode dpi-injected table (zero-inset ref,
   gesture-nav, gesture-nav+cutout, Pixel-9-Pro-class rows) + a dpi-0 fallback row + a
   floor-can-fail negative control; PlayMode join (the R1-H1 pattern): the painted rect the
   component actually applied equals `ChipRect(Screen.safeArea, Screen.dpi)`.
5. **Render-only + registers nothing (P-1).** The chip tree contains only render-side types
   (whitelist walk over the controller's chip root, plus explicit Selectable/raycaster scans,
   each with a decoy positive control); `ChromeRegions.Count` is unchanged when the chip
   appears (decoy registration moves it 0→1→0); zero banned input-surface tokens in this
   slice's sources AND prose — sweep before commit; TapInput stays the sole input consumer
   (harness criterion-2 statics green UNMODIFIED). TMP glyph-geometry renderability proxy
   (A-UX2-2 precedent). *Check:* PlayMode walks + the harness leg.
6. **Strings discipline.** `ui.csv` gains EXACTLY one appended row:
   `hint.tutorial,Tap the flashing switch` (DRAFT — TG-5 voice sitting before any device
   exposure; the only sanctioned tutorial text, CM-R13.5). *Check:* EditMode — exactly one
   `hint.tutorial` row, byte-exact, positioned after the 7 merged rows; round-trip through
   `UiStrings.Get`; P-4 literal guard (the value appears in no Presentation component
   source). **Declared test evolution (the merged R1-L6 comment's sanctioned path):**
   `UiCsvDisciplineTests.NewRows_ExactlyTheTwoPinned_Appended` raises its total-row bound by
   exactly one (7→8) with a comment naming this contract; rows 0–6 pins and both merged value
   asserts stay byte-identical. This is the slice's ONLY edit to an existing test, declared
   here at freeze — any further existing-test edit is a stop condition.
7. **A11y/motion posture (P-5) + zero drift.** Chip = shape + text (chip background + TMP
   label — never color-alone; the greyscale claim stays a structural proxy, the
   reviewer-signed checklist belongs to the batched TG sitting). Zero animation components
   under the chip root (v1 ships no motion, so the guarantee is structural — the honest
   R1-M5 posture; motion-off removes nothing because nothing moves), decoy control included.
   Live-region HOOK only: the view exposes `LiveRegionPoliteness == "polite"` and
   `AccessibilityLabel` equal to the resolved csv text (A11Y-S01-5; the Unity accessibility
   hierarchy + TalkBack pass stays UX-OPEN-11, never claimed). Zero edits to any other
   existing test; suites green at the anchor's re-derived counts + this slice's delta only;
   no wrapper/harness/Packages/scene edits. *Check:* headless EditMode+PlayMode runs +
   `bash scripts/check.sh` + full `bash scripts/test.sh` on a committed tree + diff surface.

### Visual verification (standing #33 rule)

After green: uncommitted capture probe renders real frames of the chip over the URP baseline
(hidden-by-default state and visible state), the session LOOKS at the PNGs, describes them in
the PR evidence, commits the frames under `evals/results/ux/cm-ux-05/`, and deletes the probe
before committing. Code-green alone is not evidence for a visual slice.

### Scope boundary

**In scope:** `unity/Assets/Scripts/Presentation/Hud/HintAttemptCounter.cs` ·
`unity/Assets/Scripts/Presentation/Hud/HintChipView.cs` ·
`unity/Assets/Scripts/Presentation/Hud/HintChipController.cs` (+ .meta files, batch-generated)
· **append-only** `ui.csv` row `hint.tutorial` · new tests under
`unity/Assets/Tests/EditMode/Presentation/**` and `unity/Assets/Tests/PlayMode/Hud/**`
(+ .meta) · the single declared R1-L6 amendment in
`unity/Assets/Tests/EditMode/Presentation/UiCsvDisciplineTests.cs` (criterion 6) ·
`state/handoffs/CM-UX-05-frozen-contract.md` + `state/handoffs/CM-UX-05.md` · one
SESSION-HANDOFF-ux.md status-log line · `evals/results/ux/cm-ux-05/` frames.

**Explicit non-goals:** no GameRoot/Bootstrap edit, no attachment, no reset wiring (CM-UX-07);
no region registration and no input target (v1 chip is render-only; a tappable hint is future
work behind its own contract); no TapInput/ChromeRegions/HudBands/ChromeGeometry/
ScreenChromeController edits; no analytics event (`tutorial_step.retries` deferred, named
above); no edits to existing csv rows; no new wrappers or wrapper edits; no
`unity/Packages/**` change of any kind; no scene edits; no Halted semantics (Q-B/NEW-Q4); no
L001 claim; no monetization-adjacent anything (`PRD.md:208`).

### Assumptions

- **A-UX5-1** The R1-L6 comment inside `UiCsvDisciplineTests` ("A later append-slice amends
  this bound as declared contract evolution — raise the count + pin its own rows; it may
  never touch rows 0-6") sanctions criterion 6's amendment; the amendment raises one integer
  bound and touches nothing else in that file.
- **A-UX5-2** Screen-state literals (`Playing`/`FailureReview`/`Halted`) carried in
  Presentation are the merged vocabulary — the state strings live in Bootstrap, which
  `CatMetro.Presentation` does not reference (the CM-UX-02 criterion-2 precedent).
- **A-UX5-3** The chip owns its own controller-created canvas at sortingOrder 90 — below the
  CM-UX-02 chrome canvas (100), so the halt veil overlays it if ever co-visible; the
  visibility law hides it on Halted anyway (belt and law).
- **A-UX5-4** Sibling slices CM-UX-04/06 also append csv rows in parallel; this slice's pin
  is index-tolerant (exactly-one row, byte-exact, after the merged 7) so a rebase re-derives
  the `UiCsvDisciplineTests` bound without weakening any pin.
- **A-UX5-5** v1 ships zero chip motion; any entrance pulse is a TG-sitting outcome behind
  its own change, not this slice's.

### Stop conditions

Defaults (AGENTS.md) plus:
1. Any criterion appears to require an event-system object, a `Selectable`, raycaster
   interactivity, or a second input-package consumer → stop; gate evolution is human-gated.
2. The csv row cannot land append-only, or criterion 6's amendment would need to touch
   anything beyond the one row-count bound → stop and report.
3. Harness criterion-2 statics fail for any reason other than this slice's own prose → stop;
   never edit the wrapper.
4. A sibling UX slice or the device session merges a Presentation or csv change mid-slice →
   stop, rebase, re-derive counts and the criterion-6 bound, re-verify pins before continuing.
5. Anything requires naming the chip against L001 to pass → stop (the honesty clause is
   load-bearing, not decorative).
