# CONTRACT CM-UX-02 — Fail/halt visibility: chrome controller, rendered Try-again CTA, halt veil, TMP/UGUI foundation

**Tranche:** UX tranche-1 slice 2 (`docs/ux/ux-layer-decompose.md` §2).
**DEPENDS-ON (both hard):** (1) CM-UX-01 merged (#28 — registry, gate delegate, `HudBands`,
EditMode-Presentation test access); (2) **CM-C2b-DEVFIX merged** — the 2026-08-05 ratification
batch (#29) sequences DEVFIX's 7 Presentation lines BEFORE UX-lane code, and this slice's TMP
shader assets belong on the restored-URP baseline, not before it. **Red phase does not start
until `git log` shows DEVFIX on main.** Contract freeze itself is docs-only and gate-free.
**Human decisions incorporated (in-session 2026-08-05, recorded in `SESSION-HANDOFF-ux.md`):**
Q-6 = **import TMP + UGUI now** (overriding the TextMesh recommendation; ADR-0007 honored
directly); Q-1 = land the DRAFT halt copy now, voice-pass at the TG-5 sitting; Q-2 = the halt
restart escape is **CM-UX-07's wiring line**, not this slice's; Q-3 does not touch this slice.

### Goal

The two measured fail-visibility gaps close at the component level: FailureReview renders a
LOCKED **Try again** CTA in the thumb band (CM-C3 review-N2 debt), and Halted renders a visible,
semantics-neutral veil instead of nothing (F-DEV-4) — driven by a screen-state-bound chrome
controller, on ADR-0007's UGUI+TMP stack, with zero new input surfaces and zero registered
regions. Composition-root attachment is CM-UX-07's; every behavior is proven here by direct
construction over `GameRoot.LaunchWith`.

### Spec reference

`docs/prd/ux-flows.md` S-03 (layout intent, `Try again` **[LOCKED]**, hit-testable from frame 1),
§1.1 (band law, 48dp) · `docs/prd/PRD.md` CM-R16.2, CM-R07.4, CM-R03.2 (zero literal strings) ·
`docs/adr/0007-*` (UGUI+TMP — honored per Q-6) · `docs/ux/ux-layer-decompose.md` P-1..P-7,
G-3/G-4, §6 Q-1/Q-2/Q-6 · CM-UX-01 laws: resolution order (band → regions → discs; an in-band
region during FailureReview is dead code), `HudBands` safe-area math, R1-F3 (registering owners
must `Unregister` in `OnDestroy`), R2-N2 (first `MeetsMinTargetPx` call site gets review
attention) · Q-B/NEW-Q4 OPEN — nothing in this slice may decide halt semantics.

### Acceptance criteria (9)

1. **TMP/UGUI foundation, render-only by construction.** TMP essentials (settings asset +
   default font) are imported; chrome renders on a screen-space `Canvas` whose full component
   tree contains ONLY render-side types — no `EventSystem`-family object, no `Selectable`
   subclass, no raycaster-driven interactivity anywhere under the chrome root; the criterion-2
   harness statics stay green UNMODIFIED (one input-package consumer; zero banned tokens,
   prose included). *Check:* one EditMode tree-walk test over the instantiated chrome root
   asserting a component whitelist; the harness leg on the committed tree.
2. **`ScreenChromeController` renders by state, exhaustively.** A Presentation MonoBehaviour
   bound via `Attach(Func<string> screenState, ...)` (composition root binds in CM-UX-07; tests
   bind directly): `FailureReview` → CTA visible, veil hidden · `Halted` → veil visible, CTA
   hidden · `Playing` → neither · `Won` → neither (**declared supersession:** CM-UX-04 extends
   `Won` to the results panel and MUST amend this state-map test — recorded here so that edit is
   contract evolution, not weakening). State transitions render **within one pumped frame** of
   the state source changing (execution-order-safe language — P-6). *Check:* one parameterised
   PlayMode test over the four states via `LaunchWith` + direct `Attach`.
3. **The CTA is the LOCKED string, in the band, at the floor, render-only.** `retry.cta`
   resolves "Try again" via `UiStrings` (key-only — zero literals in components); the chip is
   full-width inside `HudBands.ThumbBand(safeArea)` with the **live** `Screen.safeArea` +
   `Screen.dpi` binding (A-UX1-5 lands here); its rect passes `MeetsMinTargetPx(rect,
   Screen.dpi)` — **R2-N2: this is the first real call site; the reviewer verifies the dpi
   argument is a screen density, never a dp constant**; and it registers **NOTHING** with
   `ChromeRegions` — the CM-UX-01 law makes an in-band FailureReview region dead code; the
   band's own `RetryTapped` IS the CTA's action. *Check:* PlayMode — CTA visible on the first
   FailureReview frame; `Regions.Count` unchanged by CTA show/hide; a band tap during
   FailureReview retries exactly as before (existing pin untouched); band-rect assertions
   EditMode over injected safe areas including one inset case.
4. **The veil is visible, neutral, and inert.** On `Halted`: a full-board scrim + `halt.notice`
   DRAFT text ("Signal fault — the line stopped" — Q-1: lands DRAFT-flagged, voice-passed at
   the TG-5 sitting; **no loss/fail vocabulary may enter this string or this slice** — Q-B/NEW-Q4
   stay undecided) render above the frozen board; the veil registers **zero** regions
   (`Regions.Count` unchanged — absence-tested) and offers **no** affordance (Q-2's restart
   escape is CM-UX-07's single human-gated wiring line). *Check:* PlayMode via a halted
   `LaunchWith` fixture (drive the state source to `Halted` directly); one EditMode string test
   asserting the csv value contains none of: fail/fault-of-player/lost/lose/game over (list in
   the test, extensible).
5. **Greyscale + motion-off legibility.** CTA and veil each carry text + shape (never
   color-only state — A11Y-GLOBAL-2); with `MotionOff` true (toggle OR animator scale 0), both
   appear with zero animation clips playing and no information loss (motion removes easing
   only). *Check:* two PlayMode tests (motion on/off) asserting visibility + zero playing
   animation state; a greyscale-invariant structural assert (text non-empty + scrim shape
   present, no color-conditional branch in the component — code-level).
6. **Strings discipline.** `ui.csv` gains exactly `retry.cta,Try again` (LOCKED,
   `monetization_spec.md:173` via ux-flows S-03) and `halt.notice,<DRAFT>` — appends only;
   every pre-existing row byte-identical. *Check:* EditMode test comparing the 5 base rows
   against their frozen values + the per-slice literal guard (P-4): neither new value appears
   as a string literal in any Presentation component source.
7. **Registry lifetime law honored trivially.** This slice registers no regions, so no
   `Unregister` obligations exist — asserted (`Regions.Count == 0` throughout the slice's
   tests) and recorded: **the first registrar is CM-UX-06 (LevelIntro Play CTA / Home pin),
   which inherits R1-F3's Unregister-in-OnDestroy law.** *Check:* the Count asserts in
   criteria 3/4.
8. **Band-divergence zone documented (G-4 duty).** The slice's handoff records the measured
   divergence between the pinned raw-screen retry band and the safe-area chip rect for one
   inset example (numbers, not prose) — reconciliation stays CM-UX-07/own-contract per R2-N1
   of #27's review. *Check:* handoff section exists with the worked example.
9. **Zero behavior drift + combined-tree green.** Full suites green at the rebase base's
   re-derived counts (353 EditMode + 33 PlayMode at #28's merge into main — re-derive if the
   device session merges first; delta = this slice's tests only); zero existing-test edits
   except criterion 2's declared supersession target (none this slice); `bash scripts/test.sh`
   10/10 on the committed tree; no GameRoot/Bootstrap/Content/Domain/scripts/wrapper edits.
   *Check:* headless runs + harness; diff surface.

### Scope boundary

**In scope:** `unity/Assets/Scripts/Presentation/Hud/ScreenChromeController.cs` ·
`unity/Assets/Scripts/Presentation/Hud/RetryCtaView.cs` ·
`unity/Assets/Scripts/Presentation/Hud/HaltVeilView.cs` (names indicative) · TMP essentials
assets (`unity/Assets/TextMesh Pro/**` or package-prescribed location) + any required
`CatMetro.Presentation` asmdef reference additions for UGUI/TMP · **append-only** `ui.csv`
rows · new tests under `unity/Assets/Tests/EditMode/Presentation/**` and
`unity/Assets/Tests/PlayMode/**` · `state/handoffs/CM-UX-02.md`.

**Explicit non-goals:** no GameRoot/Bootstrap edit, no controller attachment (CM-UX-07); no
restart escape or any Halted affordance (Q-2 → CM-UX-07's human-gated line); no results panel
(CM-UX-04); no region registrations; no TapInput/ChromeRegions/HudBands edits (CM-UX-01 is the
tranche's only TapInput edit; `HudBands` is consumed, not modified); no blame chip, ghost
replay, rewind chip, or any commerce surface (attempt-1 invariant, `PRD.md:208`); no edits to
existing ui.csv rows; no Halted semantics of any kind (Q-B/NEW-Q4); no gate-wrapper edits.

### Assumptions

- **A-UX2-1** TMP ships inside the pinned `com.unity.ugui` for Unity 6 — the essentials import
  adds ASSETS, not a package: no new dependency, no ADR (verify at red; if a package add turns
  out to be required, that is stop condition 3).
- **A-UX2-2** TMP's SDF UI shaders render on the DEVFIX-restored URP baseline for screen-space
  canvas text (standard behavior). A PlayMode renderability proxy (generated mesh vertex count
  > 0) stands in for device proof; device magenta-checks remain the device lane's.
- **A-UX2-3** The chrome canvas is created by the controller itself (no scene edit — Game.unity
  stays untouched; the controller is test-attached now, root-attached in CM-UX-07).
- **A-UX2-4** `Screen.safeArea`/`Screen.dpi` reads live in the VIEW layer (binding into the pure
  `HudBands` at call sites), per A-UX1-5. In batchmode tests the safe area equals the raw screen
  — the inset behavior is covered by EditMode injection, not by faking `Screen`.
- **A-UX2-5** The Halted fixture drives the controller's state source directly (the real
  GameRoot Halted transition needs a pinned-boundary throw, which no importable fixture can
  produce deterministically — same constructed-state precedent as CM-C3's PlatformOverflow leg).

### Stop conditions

Defaults (AGENTS.md) plus:
1. DEVFIX has not merged when red would start → **stop and wait** (ratified sequencing; do not
   reorder).
2. Any criterion appears to require an `EventSystem`, `Selectable`, raycaster interactivity, or
   a second input-package consumer → stop; the gate-evolution path needs the human.
3. TMP essentials turn out to require a NEW package/manifest pin → stop (dependency = ADR = human).
4. The veil cannot be made legible without loss/fail vocabulary or a Halted affordance → stop
   (Q-B/TG-5 territory).
5. The criterion-2 harness statics fail for any reason other than this slice's own prose → stop;
   never edit the wrapper.
6. The device session merges a Presentation edit mid-slice → stop, rebase, re-verify the
   CM-UX-01 pins against the new base before continuing.
7. Build-size or import weight of TMP essentials visibly breaches the ≤60 MB AAB posture
   (gross, not marginal) → stop and report with numbers.
