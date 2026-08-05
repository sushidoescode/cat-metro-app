# UX layer — tranche-1 decompose (first-run experience)

**Status:** proposed (agent) · **Date:** 2026-08-05 · **Session:** parallel UX lane per
`state/handoffs/SESSION-HANDOFF-ux.md` · **Base:** origin/main 64cb0d8
**Method:** three independent lensed decompose proposals (player-pain / dependency-safety /
test-rigor), each adversarially verified against the hard boundaries, plus a cross-proposal
completeness critic; synthesized by the session. All panel verdicts: accept-with-fixes; every
verifier finding is either baked into a slice below or recorded in §6/§7.
**Mandate:** human approval 2026-08-05 — build the UX layer from `docs/prd/ux-flows.md`;
**rank the first-run experience first (home → play → teach → fail-visibility)**; TG-1..TG-8 stay
live human taste gates; monetization surfaces stay out (PRD attempt-1 invariant, `PRD.md:208`).

## 0. Why this ranking

The human's phrase names the **player's** journey; the build order below serves it in near-reverse,
and the evidence demands that:

- The device run (`evals/results/device/c2b-crit8/ARTIFACT.md`) proves a first-run player meets
  the fail/halt surfaces within seconds — and today the halt renders **nothing** (F-DEV-4,
  `GameRoot.cs:163` returns before any render), the retry verb has **no rendered affordance**
  (CM-C3 review N2), and a win **dead-ends forever** (`GameRoot.cs:185-189`). Those are the three
  measured full-stops; Home is orientation-grade pain nobody has yet reached in anger.
- Everything upstream in the journey (Home, boot-to-Home, LevelIntro hold) needs
  `Bootstrap/GameRoot` wiring that is frozen until the device session's CM-C3-DEVCAP +
  device-config-fix contracts merge. Ranking Home first would serialize the lane behind a merge
  we don't control; ranking foundation + fail/halt/win visibility first means every slice is
  review-complete and the thin wiring PR (CM-UX-07) turns them on together.
- A cross-verifier discovery hardens the order: the input desync is **not** halt-only — in every
  non-`Playing` state (`Won`, `Halted`, `FailureReview` above the retry band) taps still flip
  lever visuals against a stopped sim (`TapInput.cs:52-71` has no state gate). The input
  foundation slice must land before any chrome slice so the fix is structural, not per-view.

**One deliberate deviation from the panel:** the device-visible-now argument for ranking the retry
CTA first was **refuted** by its own verifier (the device build boots hardwired into L001, which
cannot reach FailureReview at all — F-DEV-3), so slice 1 is the input foundation, not a view.

**Honest aggregate (review R1-F12):** under this plan the only pre-DEVCAP device-visible first-run
change is CM-UX-03 (teach pulse). CM-UX-02/04/05/06 are review-complete but activate at CM-UX-07,
which is sequence-blocked on merges this lane doesn't control — and the win dead-end closes only
after §6 Q-3 / a `LoadNext` contract, i.e. **not in tranche 1 at all** under Q-3's recommended
default. That is the accepted price of the ownership boundary, stated here rather than implied.

## 1. Cross-cutting postures (bind every slice)

| # | Posture |
|---|---|
| P-1 | **One-input-surface gate: untouched, everywhere.** Resolution (a) from the handoff: all chrome is render-only; every hit routes through `TapInput` via a chrome-region registry (pure rect math). No slice introduces `EventSystems`/pointer-handler/`Touchscreen`/`EnhancedTouch`/`OnMouse` tokens (source **or prose** — the greps scan comments), and `TapInput` stays the sole `UnityEngine.InputSystem` consumer. **No gate-evolution PR exists in this tranche**; if one ever becomes necessary its merge waits for explicit human OK. |
| P-2 | **Chrome rendering technology: TMP + UGUI, now — DECIDED by the human (§6 Q-6, answered 2026-08-05, recommendation overridden).** All chrome from CM-UX-02 onward renders on ADR-0007's UGUI+TMP stack (render-only: no EventSystem-family objects, hits through the TapInput registry — P-1 unchanged). CM-UX-02 carries the essentials import. The ONLY remaining TextMesh is the merged greybox (`BannerView`, board glyphs, preview badges); its back-migration to TMP is the §3 tranche-2 item. No slice may ship NEW TextMesh chrome. (History: an earlier draft deferred TMP on a wrong F-DEV-2 reading — the device shader strips because the built scene references NO material, `ARTIFACT.md:64-69`; TMP ships referenced assets, the opposite case.) |
| P-3 | **Components now, wiring later.** No slice edits `Bootstrap/**` or `GameRoot.cs`. Slices marked NEEDS-WIRING land components + tests that prove behavior by direct construction (the repo's `LaunchWith`/drive-the-seam pattern); CM-UX-07 is the single enumerated composition-only wiring PR, sequence-blocked on the DEVCAP + device-config-fix merges (mechanical `git log` check). |
| P-4 | **Strings:** ui.csv appends only, never edit an existing row; zero literals in components (`UiStrings` keys). The harness grep leg only covers the three locked fail phrases and the wrappers are not this lane's to extend — so **each slice ships its own EditMode literal guard** under `unity/Assets/Tests/**` covering its new keys. LOCKED copy lands verbatim; DRAFT copy is flagged in the slice contract and queued for the TG-5 voice sitting before any device exposure. |
| P-5 | **A11y floor per slice:** every interactive target ≥48dp on the 360×640dp reference (band math from CM-UX-01); state never conveyed by color alone (greyscale criterion per chrome view); motion-off (`GameRoot.MotionOff`) removes easing only, never information; label/live-region **hooks** land per slice — the Unity accessibility-hierarchy build + TalkBack pass is deferred work (UX-OPEN-11), never claimed early. |
| P-6 | **Determinism:** Presentation never simulates; chrome reads state via injected delegates; no wall-clock enters the sim; no assertion spans two `Update` orders with a "same frame" claim (execution-order-safe language: "within one pumped frame"). |
| P-7 | **Test labeling discipline:** red-first tests are labeled red-first; characterization pins of merged behavior are labeled pins (green on arrival, by design). |

## 2. Tranche 1 — ranked slices

One PR per slice, in this order. "NOW" = lands fully live pre-DEVCAP; "NEEDS-WIRING" = components
+ tests now, composition lines enumerated for CM-UX-07.

### CM-UX-01 — Input foundation (NOW · gate-untouched)
Frozen contract: `state/handoffs/CM-UX-01-frozen-contract.md` (this PR).
TapInput chrome-region registry (consulted before board discs, deterministic resolution, nothing
registered yet), `BoardInputActive` gate delegate (null until wired — fixes the non-Playing lever
desync structurally when CM-UX-07 binds it), `HudBands` band/48dp math with injected metrics, and
the **test-assembly fix** both verifiers proved blocking: `CatMetro.Tests.EditMode.asmdef` gains
the `CatMetro.Presentation` reference (EditMode tests over Presentation components are
uncompilable today). Behavior-neutral until consumed; the tranche's only TapInput edit.
*Journey:* enables all · *csv:* none · *TG:* none.

### CM-UX-02 — Fail/halt visibility (NEEDS-WIRING · gate-untouched)
`ScreenChromeController` (bound to a `Func<string>` screen-state source), rendered **Try again**
CTA (`retry.cta`, LOCKED "Try again", full-width thumb-band chip ≥48dp, **render-only per
CM-UX-01's resolution law** — inside the retry band the band's own `RetryTapped` IS the action;
registering a competing region there is dead code by law — visible from the first FailureReview
frame; the full band stays hit-testable, the chip narrows perception, not the target). This slice
also binds `Screen.safeArea`/`Screen.dpi` into `HudBands` (A-UX1-5) and **documents the
band-divergence zone** (pinned raw-screen retry band vs safe-area chip rect on inset devices;
reconciliation at CM-UX-07). And the **halt veil** (F-DEV-4): a visible
overlay + neutral DRAFT copy with **zero registered targets — absence tested** so the surface
cannot be read as deciding Q-B/NEW-Q4 halt semantics. Greyscale + motion-off criteria per view.
*Journey:* fail-visibility · *csv:* `retry.cta` (LOCKED), `halt.notice` (DRAFT, semantics-neutral,
no loss/fail vocabulary) · *TG:* TG-5-adjacent DRAFT copy queued for the voice sitting.

### CM-UX-03 — Teach pulse (NOW · gate-untouched)
The tutorial affordance inside `BoardView`, gated on `Meta.Band == "onboarding"` — internal
gating survives the Retry rebuild by construction and goes live via the existing
`GameRoot.Wire → BoardView.Build` call the moment it merges: **the tranche's only pre-DEVCAP
device-visible change**. Wrong-route switch pulses; motion-off renders the raised-ring shape twin
(mute/volume-0 + motion-off legibility per A11Y-S01-3 and A11Y-S02-9's spirit; A11Y-S01-2 proper
is the HOME pin's shape-state criterion and belongs to CM-UX-06 — review R1-F7); cleared on first
accepted toggle. Zero-instructional-text law asserted with an argued exemption list (station
symbol glyphs, preview count badges, level name/score are legal; tutorial prose is not —
CM-R13.1), and **the exemption list carries CM-R13.5's hint chip from day one** so CM-UX-05's
chip cannot turn this assertion red and tempt a weakening edit (review R1-F8, hard rule 5).
*Journey:* teach · *csv:* none · *TG:* pulse feel queued for the batched eyeball.

### CM-UX-04 — Results panel v1 (NEEDS-WIRING · gate-untouched)
`Won` → results panel with **exactly one primary CTA** `Next` (`results.next`, LOCKED) and a
**structurally-empty footer** — the registry-count==1 invariant asserts CM-R19.3 + TG-4's
empty-footer posture in one test. (A11Y-GLOBAL-14 is scoped to COMMERCE surfaces' rendered trees
— timers, preselection, close affordance, decline copy — and stays untested until a commerce
surface exists, which the embargo forbids; claiming it here would hand the first real commerce
surface a checked-off criterion nobody tested — review R1-F6.) `NextRequested` is a seam only (level advance
is Bootstrap-owned); **the panel is not attached by CM-UX-07 until level-advance exists or the
human rules otherwise** — a rendered LOCKED `Next` that does nothing is a worse dead-end than
today's banner (§6 Q-3). No score/star/ticket content: Domain score is Q-C-pinned at 0 and
rendering it would fabricate data.
*Journey:* play (closes the win dead-end) · *csv:* `results.next` (LOCKED) · *TG:* TG-4 honored
structurally; arrangement sitting deferred until a footer exists to arrange.

### CM-UX-05 — Hint chip + attempt counter (NEEDS-WIRING · gate-untouched)
Counts **FailureReview entries** per level attempt-run; chip renders after the 2nd fail
(CM-R13.5, S-01 flow node L) with one csv line — the only sanctioned tutorial text. **Standing
honesty clause:** L001 cannot reach FailureReview (F-DEV-3), so the chip is device-reachable only
on L002/L003 until F-DEV-3 or Q-B resolves — no slice may re-sell the chip against L001. Halted
edges are **not** counted (counting them would pre-decide Q-B). Placement pinned board-edge above
the thumb band at ≥48dp height (A11Y-S01-4 lists the chip as an interactive-grade element even
though v1 registers no target — honored dimensionally, flagged DRAFT for the eyeball).
*Journey:* teach · *csv:* `hint.tutorial` (DRAFT: "Tap the flashing switch") · *TG:* copy + placement
to the voice/eyeball sitting.

### CM-UX-06 — ScreenStack + Home greybox + LevelIntro sheet (NEEDS-WIRING · gate-untouched)
Pure-C# `ScreenStack` (push/pop per ADR-0007 navigation; serialization **shape** matches ADR-0006
`breadcrumbs.screenStack` — save I/O stays Application-layer, deferred), greybox Home (one pulsing
L001 pin ≥48dp with motion-off shape twin; parked-district silhouettes; **session-1 structural
law** — a tree test asserting no shop/daily/badge node exists), and the minimal LevelIntro sheet:
level name + explicit thumb-band `Play` CTA (S-05's spec'd interaction; tap-anywhere dismissal
violates the thumb-band law and is not built). Tick-0 hold and boot-to-Home are GameRoot state
machine work → excluded to the boot-flow follow-up contract, never the thin wiring PR.
*Journey:* home · *csv:* `home.title` etc. + `intro.play` (DRAFT set, minimal) · *TG:* TG-3
honored by building **neither** Night-Harbor variant (tile absent in greybox).

### CM-UX-07 — Thin wiring PR (NEEDS-WIRING · sequence-blocked on DEVCAP + device-config-fix merges)
Enumerated composition-only GameRoot lines: attach `ScreenChromeController` with
`() => ScreenState`; bind `BoardInputActive = () => ScreenState == "Playing"` (turns on the
CM-UX-01 desync fix); attach hint counter; Home/stack mount behind an explicit launch argument
(boot stays L001 for the device capture path until the boot-flow contract). **Reviewable thinness
is an acceptance criterion.** Plus the red-first integration tests only real wiring can honestly
run (the F-DEV-4 closure test among them) and **exactly one human-gated line**: whether the halt
veil's escape routes to tick-0 restart (recommended: yes — semantics-neutral; both readings of
Q-B want a restart affordance — but this is the human's call, §6 Q-2). Results-panel attach
follows §6 Q-3's ruling.
*Journey:* all — turns the tranche on · *csv:* none · *TG:* **the batched TG sitting happens
here** (§4).

## 3. Tranche 2 head (named, not started)
**Settings floor** (motion/haptics toggles, CM-R22.3 — dep-safety's sink-driven-row design):
ranked immediately after CM-UX-07 **unless** the human wants motion-off device-reachable for the
eyeball sitting, in which case it swaps ahead of CM-UX-05 (see §7 G-1 — motion-off is currently
unreachable on any real device). Then: pause surface (UX-OPEN-02, deferred entirely — its
contents ARE the open question), boot-to-Home + LevelIntro tick-0 hold contract, level-advance
(`LoadNext`) contract, save-backed stack restore, TMP/UGUI migration (P-2), status-band
name/score readout (score blocked on Q-C unpin).

## 4. TG eyeball schedule (human, batched)
One sitting at CM-UX-07 (first wired build): TG-1 posture check (greybox ships no palette — the
gate bites at the art pass, per CM-C3 precedent) · TG-3 Home first-read (neither variant built —
confirm) · TG-4 results weight (footer empty by construction — confirm) · TG-5 voice batch over
every DRAFT string (`halt.notice`, `hint.tutorial`, LevelIntro/Home set) **before any device
exposure** · CM-UX-03 pulse feel + CM-UX-05 chip placement. TG-2/TG-6/TG-7/TG-8 surfaces are not
built (blocked-human by construction).

## 5. Deferred register (consolidated from all three lenses)
Pause menu (UX-OPEN-02 — contents are the open question; back gesture stays dead in greybox and a
device tester WILL hit it, flagged in §6 Q-4) · planning pause (TG-2/UX-OPEN-01) · consent (TG-7/
UX-OPEN-10) · audio toggles (TG-6/UX-OPEN-09) · blame chip (TG-5 + UX-OPEN-18 EXPERIMENTAL) ·
ghost replay (Application-layer seam we don't own) · results score/stars/tickets (Q-C pin, NEW-Q5/
NEW-Q7) · chain/purr meter (dead UI over a pinned 0) · LevelIntro best-score/thresholds (save +
content reads) · level progression policy (Bootstrap-owned; seam only) · boot-to-Home (device-lane
coordinated) · save-backed restore + settings persistence (Application-layer) · Unity a11y
hierarchy + TalkBack pass (UX-OPEN-11 — hooks only in-tranche) · haptics JNI (no sink yet) ·
palette/TG-1 repaint (art pass) · L002-L005 teach choreography (rides device-session content) ·
**all** monetization/store/daily/share/streak/notification surfaces (mandate embargo; CM-UX-04's
count==1 and CM-UX-06's tree law are the structural tripwires) · gate-evolution path (b) — not
needed anywhere in this tranche.

## 6. Open questions for the human (with recommended defaults)

> **Answered in-session 2026-08-05 — the human's answers, verbatim in substance (record:
> `SESSION-HANDOFF-ux.md`):** Q-1 = "Land DRAFT now" · Q-2 = "Yes, restart escape" · Q-3 =
> "Hold until LoadNext exists" · Q-6 = "Import TMP + UGUI now" (recommendation overridden).
>
> **Consequences derived by the LANE (not the human's words):** Q-2's escape becomes CM-UX-07's
> wiring line; Q-6 supersedes P-2's TextMesh posture, CM-UX-02 carries the essentials import,
> and the lane sequences that import AFTER CM-C2b-DEVFIX's URP restore per #29's ratified
> ordering. Q-4/Q-5 remain open ratifications, blocking nothing.
- **Q-1 (blocks CM-UX-02 copy only):** approve DRAFT `halt.notice` — must stay semantics-neutral
  pending Q-B/NEW-Q4. Recommended: land DRAFT-flagged now (csv-swappable without code change),
  voice-pass at the TG-5 sitting.
- **Q-2 (blocks one CM-UX-07 line):** may the halt veil's escape route to tick-0 restart?
  Recommended: yes — semantics-neutral either way Q-B lands; explicitly the human's call.
- **Q-3 (blocks CM-UX-04 attach in CM-UX-07):** attach the results panel while `Next` has no
  advance target, or hold until `LoadNext` exists? Recommended: hold — a dead LOCKED `Next` reads
  as a bug; the seam still merges and tests.
- **Q-4 (ratification, blocks nothing):** confirm full pause-surface deferral (UX-OPEN-02) over a
  Resume/Restart floor — the floor's contents are exactly the open question. Recommended: defer.
- **Q-5 (ratification, blocks nothing):** confirm boot stays L001 post-wiring (device capture
  path unchanged) with Home behind a launch argument until the boot-flow contract. Recommended:
  yes.
- **Q-6 (blocks CM-UX-02+ rendering tech; CM-UX-01 unaffected):** ADR-0007 ratifies UGUI+TMP
  chrome; merged greybox code defers it (`BannerView.cs:5-7`) and TMP essentials are not
  imported. Continue the TextMesh-greybox precedent through tranche 1 with a named TMP/UGUI
  migration contract at the art/chrome pass, or import TMP now? Deferring a ratified ADR is your
  call, not the lane's (constitution: ADR approval is human). Recommended: continue TextMesh
  greybox — the migration rides the art pass where TG-1 already forces a board-rendering sitting;
  the known cost is that band-math/48dp tests written now get re-verified against RectTransforms
  at migration.

## 7. Known gaps with owners (recorded, not silently absorbed)
- **G-1 Motion-off is unreachable on any real device today:** `GameRoot.MotionOff` ORs a toggle
  stub (no UI) with `AnimatorDurationScale` (never read from the OS). Every "motion-off parity"
  criterion in this tranche is therefore editor-verified only until the settings floor + the OS
  animator-scale read land. Owner: settings-floor contract (tranche 2, §3); the wiring-PR TG
  sitting must not claim device motion-off coverage.
- **G-2 Zero-literal harness leg is phrase-scoped:** covered per-slice by P-4's Unity-side guards;
  a consolidated harness leg belongs to whichever session owns the wrappers when the lanes rejoin.
- **G-3 Input desync in non-Playing states:** structural fix lands in CM-UX-01 but only activates
  at CM-UX-07 (`BoardInputActive` binding). Between those merges the desync remains on device —
  accepted; it is invisible until chrome makes the states legible anyway.
- **G-4 Band-divergence zone (review R1-F15):** CM-UX-01's pin freezes the retry band's RAW
  bottom-25% consumption (`TapInput.cs:47`) while `HudBands` defines bands on the SAFE AREA
  (`ux-flows.md:32`). On inset devices a tap can retry without hitting the rendered chip — AND
  the inverse (#31 review R1-F1): rendered chip pixels can be inert, so CM-UX-02 asserts the
  48dp floor on the INTERSECTION of chip and raw band. Owner: CM-UX-02 documents + pins the
  zone numerically; CM-UX-07 reconciles deliberately against the pin.
- **G-5 Queued for the next human ratification batch (#31 review R1-F8):** the lane merged
  CM-UX-01 (184 Presentation lines, zero rendering code) before DEVFIX, reading #29's "DEVFIX's
  7 Presentation lines precede UX-lane code" as scoped to RENDERING code. That narrowing is the
  lane's interpretation, not the human's words — disclosed on #31; the human ratifies or
  corrects the reading; it is not precedent either way.
