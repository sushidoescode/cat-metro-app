# CM-UX-03 — build-loop handoff note (session 2026-08-06, UX lane)

**Contract:** `state/handoffs/CM-UX-03-frozen-contract.md`, frozen as the branch's FIRST commit
(c8bc312) before any code — the declared freeze-in-history mechanism; reviewers verify the
commit order and that this file never changes across the PR. Copied VERBATIM below.

## TDD evidence

- **Red (post-c8bc312):** PlayMode 59 total / **5 red for the right reason** (skeleton
  affordance: probe false, rings absent, Retry restores nothing, motion sampling finds no
  carrier); 2 of the 7 new tests are law-pins green-at-red BY DESIGN with fail-capable decoys
  (zero-text inventory + no-object-churn — each carries an in-fixture decoy control proven able
  to fail; P-7 labeling).
- **Green:** PlayMode **59/59** on the first implementation pass — every merged inventory
  (render-fidelity BoardElementId set, halt tests, chrome suite) survived the rings because
  they carry no BoardElementId by design. EditMode **375/375** (no EditMode surface in-slice).
- **#33 visual evidence:** `evals/results/ux/cm-ux-03/cm-ux-03-teach.png` — Screen-matched
  capture (the CM-UX-02 M1 lesson applied from the start): both junction switches carry the
  visible ring halo, clearly distinct from plain nodes and stations; the still shows the
  SHAPE twin, which is exactly the motion-off information story. Session-eyeballed.

## Criterion → check map

1 band gate both ways (alternation fixture renders zero rings, component-identical; positive
  control in-file) · 2 per-switch clear via the session log incl. toggle-back persistence,
  others unaffected · 3 Retry re-teaches (gate lives in Build) · 4 motion-off removes the
  pulse only — 30-frame scale sampling proves oscillation exists (positive control) and stops;
  ring transform constant in both states, no Animator · 5 zero-instructional-text inventory
  (station glyphs + wave badges only) with the CM-R13.5 hint-chip key PRE-REGISTERED and a
  decoy-text control · 6 no object churn across 20 frames; pulse mutates cached transforms;
  log scan via indexer (no enumerator alloc) · 7 suites at the re-derived base (375 EM /
  52 PM at #35+#34) + harness + the committed frame.

## Forward obligations

- CM-UX-07 binds `BoardView.MotionOffSource = () => root.MotionOff` in the SAME PR that
  attaches the chrome controller (appended to the existing CM-UX-07 obligation list).
- CM-UX-05 amends the criterion-5 exemption list when the hint chip lands (pre-registered).

---

## Frozen contract (verbatim copy)

# CONTRACT CM-UX-03 — Teach affordance: onboarding switch pulse + static ring twin, band-gated inside BoardView

**Tranche:** UX tranche-1 slice 3 (`docs/ux/ux-layer-decompose.md` §2 — the tranche's only
pre-wiring device-visible change: live via the existing `GameRoot.Wire → BoardView.Build` call
the moment it merges, and it survives `Retry()`'s rebuild by construction because the gate
lives INSIDE `Build`).
**DEPENDS-ON:** CM-UX-02 merged (#35). **Freeze mechanism (proportionate sprint pricing):**
this contract is the FIRST commit on the implementation branch — frozen in history before any
code; the review round verifies the commit order and this file's immutability across the PR.
**Human gates:** none open for this slice (no strings, no copy, no TG-blocked surface; the
pulse FEEL is queued for the batched TG sitting — building it does not pass it).

### Goal

On onboarding-band levels the interactive verb surface teaches itself: every switch disc
carries a **static raised-ring** shape affordance plus a **scale pulse**; the affordance clears
per-switch on that switch's first toggle command and returns on Retry (tick-0 restores the
Read phase). Non-onboarding bands render exactly today's board. No text, no icons, no modals —
CM-R13.1's law asserted as a rendered-text inventory. Motion-off removes the pulse only; the
ring is ALWAYS present, so motion never carries the information (A11Y-S01-3 volume-0
visibility + A11Y-S02-9 spirit; A11Y-S01-2 proper belongs to CM-UX-06's Home pin).

### Spec reference

`docs/prd/PRD.md` CM-R13.1/.2 (no-text tutorial; L001 initialRoute wrong — the first tap is
the lesson), CM-R22 (motion posture) · `docs/prd/ux-flows.md` S-01 interaction behavior
("L001's switch … pulses; the first tap is the entire lesson"), §1.1 motion law ·
`docs/ux/ux-layer-decompose.md` CM-UX-03 slice text + P-5/P-6/P-7 · CM-UX-01/02 inherited
laws (live-wiring/anti-vacuity rule VERBATIM: every absence assert carries a positive control
in the same fixture; pins labeled) · the #33 standing rule (rendered-frame evidence).

### Acceptance criteria (7)

1. **Band gate, both directions.** An onboarding-band fixture renders ring+pulse carriers on
   every switch; an `alternation`-band fixture (schema-legal) renders NEITHER — the board tree
   is component-identical to today's. *Check:* two PlayMode tests over `GameRoot.LaunchWith`;
   the negative side's positive control is the onboarding side in the same file.
2. **Per-switch clear on first toggle, others unaffected.** On a 2-switch onboarding fixture:
   toggling switch 0 (via `HandleTapAtScreen` at its screen position) clears ring+pulse for
   switch 0 within one pumped frame while switch 1 keeps both; the cleared state persists even
   if the switch is toggled back (the clear keys on any command for that switch in the
   session's log, not on the current route). *Check:* one PlayMode test, pre-toggle presence
   as the positive control.
3. **Retry restores the teach state.** After a criterion-2 clear, `GameRoot.Retry()` renders
   ring+pulse on ALL switches again (fresh session/log; the gate lives in `Build`, so the
   rebuilt view re-teaches by construction). *Check:* one PlayMode test.
4. **Motion-off removes the pulse ONLY; the ring is always static.** With
   `BoardView.MotionOffSource = () => true`, the disc/pulse carrier's scale is constant
   across pumped frames while the ring remains; with motion on, the scale demonstrably
   oscillates (the positive control proving the pulse exists to remove); the ring's transform
   is constant in BOTH states and carries no animation component. *Check:* one parameterised
   PlayMode test sampling transforms across frames. `MotionOffSource` (`Func<bool>`, null =
   motion on) is the CM-UX-07 binding seam — the composition root binds it to
   `GameRoot.MotionOff` in the SAME PR that attaches the chrome controller (forward
   obligation, appended to the existing CM-UX-07 list).
5. **The zero-instructional-text law, inventoried.** During Playing on an onboarding fixture,
   the complete rendered-text set under the board+HUD is exactly: single-character station
   glyphs + wave-preview count badges (the merged inventory). The exemption list ships in the
   test WITH the CM-R13.5 hint-chip key (`hint.tutorial`) pre-registered as legal-when-it-
   arrives (the #27 R1-F8 obligation: CM-UX-05 must not turn this assertion red). *Check:*
   one PlayMode inventory test + a decoy text control (a stray TextMesh with prose makes the
   inventory fail — the anti-vacuity control).
6. **Zero per-frame allocation; determinism untouched.** The pulse mutates cached transforms
   (no allocation in the per-frame path — diff-reviewed + no new objects created after Build
   asserted across pumped frames); pulse phase reads render-side time only (Presentation
   never simulates; the sim path is untouched — the determinism leg of the harness stays
   green). *Check:* one PlayMode test counting child objects across frames + the harness.
7. **Zero drift + visual evidence.** Suites green at the re-derived base (375 EM + 52 PM at
   the #35+#34 merge base — re-derive if the device session merges first); zero existing-test
   edits; no wrapper/`unity/Packages/**`/GameRoot/Content/Domain edits; `scripts/test.sh` all
   wrappers green (13 exist today; count re-derived). **#33 rule:** one rendered frame of an
   onboarding board showing ring+pulse carriers, captured Screen-matched (the CM-UX-02 M1
   lesson), committed under `evals/results/ux/cm-ux-03/` and eyeballed before commit.
   *Check:* headless runs + harness + the committed frame.

### Scope boundary

**In scope:** `unity/Assets/Scripts/Presentation/Board/BoardView.cs` (the slice's ONLY
shipped-code edit) · new tests under `unity/Assets/Tests/PlayMode/Board/**` ·
`state/handoffs/CM-UX-03.md` · the single `state/PROJECT_STATE.md` merged-form row.

**Explicit non-goals:** no hint chip, no attempt counter, no strings, no ui.csv rows
(CM-UX-05); no Home pin (CM-UX-06); no TapInput/ChromeRegions/HudBands/chrome edits; no
GameRoot/Bootstrap edits (the `MotionOffSource` BINDING is CM-UX-07's); no solver-derived
"which route is wrong" logic — the affordance marks the interactive surface, never the
answer (deriving correctness is solver territory and out of the lane); no scene edits; no
wrapper edits; no palette/art (TG-1 bites at the art pass).

### Assumptions

- **A-UX3-1** "Wrong-route pulse" (decompose phrasing) is implemented as ALL-switches teach
  affordance on onboarding bands: on L001 (one switch, initialRoute wrong by authoring —
  CM-R13.2) this is exactly the spec'd behavior; on L002/L003 it marks the verb surface,
  which is the no-text teaching grammar. Deriving per-switch "wrongness" would need the
  solver (non-goal). If the human wants answer-marking instead, that is a taste call for the
  TG sitting — the affordance carrier is the same either way.
- **A-UX3-2** The clear keys on the session command log (any entry for that switch), read
  through the existing `GameSession.Log.Entries` seam — no new Application state.
- **A-UX3-3** The ring is a scaled primitive behind the disc using `GreyboxMaterial.Shared`
  (the merged provider; no new materials, no new Resources entries).
- **A-UX3-4** `Build()` signature unchanged (the NOW-live property); `MotionOffSource` is a
  settable field defaulting to motion-on, matching `GameRoot.MotionOff`'s default false.

### Stop conditions

Defaults (AGENTS.md) plus:
1. The affordance cannot be built without solver access or Domain/Content edits → stop.
2. Any criterion seems to require a string, icon, or modal on a tutorial level → stop
   (CM-R13.1 is the law this slice exists to respect).
3. The criterion-2 harness statics or the zero-text inventory conflict with a merged
   behavior → stop; never weaken either.
4. The device session merges a BoardView edit mid-slice → stop, rebase, re-verify.
5. Suites reveal the pulse perturbs any existing test (camera framing, element counts in
   merged tests) → stop and report before adapting anything.
