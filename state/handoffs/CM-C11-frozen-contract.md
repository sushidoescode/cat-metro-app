# CONTRACT CM-C11 — L006–L010: Alternation band authored + validated (tranche 3, content lane)

**Status:** **FREEZE-READY** (tranche 3, **Wave 2**). Supersedes `t3-levels-l006-l010-draft-contract.md`.
The id is **`CM-C11`** (assigned by the human 2026-08-05, R4 below).
**Roadmap/PRD:** CM-R09.1/.2/.3 (`docs/prd/PRD.md:220-232`) · CM-R06.2/.4 (`:184-187`) ·
CM-R12.1–.6 (validator stages) · CM-R13 no-text teaching · CM-R16 (fail/retry loop — consumer).
**DEPENDS-ON:** CM-C2a (#8), CM-C4 (#9), CM-C5 (#10), L002-L005 (#15) — all merged — **plus
`CM-C10` (the content stager) MERGED**, which is what makes criterion 9 a tool run instead of a hand
copy. Ratified as **Wave 2, after the stager merges** (R4).
**Precedent contract (authoring pattern, review lessons):** `state/handoffs/L002-L005.md` in full.
**Runtime edge (no file collision):** **CM-C5.1**, the dead-`newMechanic` gate, lands in Wave 1 and
will judge this band — see the JOINT NOTE below.
**Parallel lanes it must not collide with:** UX lane (`state/handoffs/SESSION-HANDOFF-ux.md:27-39`
— L006–L010 content is explicitly NOT theirs; `tests/unity/editmode.test.sh` is theirs after
DEVCAP merges, so this contract must **satisfy** that wrapper, never edit it) and the in-flight
Bootstrap-owned branches **CM-C3-DEVCAP** and **CM-C2b-DEVFIX** (this contract touches no
`Bootstrap/**`, no `ProjectSettings`, no `Presentation/**`).
**Branch:** `task/CM-C11-alternation-band`, own worktree, cut from `main` **after CM-C10 merges**.

---

## Ratifications (human, in-session 2026-08-05) — binding; recorded before freeze

| # | Ratification as given | Effect on this contract |
|---|---|---|
| **R1 (HC-1/HC-2)** | **`scripts/stage-content.sh` (CM-C10) is THE single staging author, with deterministic path-derived guids for staged `.meta` files.** ContentSync is **not** frozen now and will be re-cut later as assert-only. | **Criterion 9 is rewritten**: the five staged copies and their five `.json.meta` siblings are **produced by running `bash scripts/stage-content.sh --apply`**, not hand-copied and not hand-authored. The draft's "fresh guid, shape copied from `L005.json.meta`" is **deleted** — guids are the stager's path-derived values (cross-check C3 closed; three competing `.meta` policies collapse to one). **OPEN-1 shrinks** from "may this contract hand-copy into CM-C2b's glob?" to "may the stager's mechanical output for five **new** files land in this PR?" — see the RIDES-WITH-PR row. This contract still **never edits `tests/unity/editmode.test.sh`** and never touches L001–L005 or their staged copies. |
| **R3 (HC-5)** | **The five-item backlog ownership amendment batch is delegated to the agent.** | **OPEN-4 is CLOSED**: `unity/Assets/Tests/EditMode/Pure/Corpus/**` and `tests/corpus/**` are drafted as this lane's owned rows in `t3-backlog-amendment.md` (EDIT-4). This contract never edits `state/backlog.md`; the queue owner applies the batch. |
| **R4 (ids + sequencing)** | **ids: TAX = CM-C9, stager = CM-C10, LVL = CM-C11, DM = CM-C5.1. Wave 1 = TAX + DM + stager in parallel worktrees; LVL is Wave 2, after the stager merges. DEVFIX's 7 Presentation lines precede the UX lane.** | Id, branch and Wave-2 placement fixed. The band is authored **after** CM-C5.1's first red-bar run, so F-K (does the shipped corpus pass the liveness gate?) is answered before authoring starts. Cross-check C4 (hand-copy default vs the staging drafts' ordering advice) is closed in the stager's favour. |
| **Earlier same-session — dev-only failable-level override** | **CM-C3's device legs use a dev-only failable-level override.** | **OPEN-5 is CLOSED, negatively: the failable-campaign-level alternative is dead.** This band does **not** replace the device hook and is **not** re-sequenced ahead of the CM-C3 device cycles. **Criterion 7 is retained, un-weakened**, but its justification changes: it is now authoring quality (avoid repeating the F-DEV-3 Win-or-Halt pathology, `evals/results/device/c2b-crit8/ARTIFACT.md:97-100`) and campaign content for the CM-C3 fail/retry loop — **not** a dependency of the device session. |
| **Earlier same-session — URP** | **URP is being restored by CM-C2b-DEVFIX.** | Recorded because this contract's levels are the content the greybox renders. This contract writes **no** `Presentation/**`, no `ProjectSettings/**`, no shader, no material and no scene; the magenta/shader-stripping defect and its URP fix are entirely DEVFIX's. **No criterion here depends on rendering**, and "greybox-validated is the bar" means validator-green plus the tests below — never a visual claim. If a level looks wrong on device after DEVFIX lands, that is a taste/TG finding, not a criterion of this contract. |

**Cross-check dispositions applied to this file:** C3 (`.meta` provenance) closed by R1; C4 (start
path) closed by R4; **C5** → the JOINT NOTE below, defaults stand; C6 (wrapper-count literals) →
criterion 11; matrix rows `StreamingAssets/content/levels/**` and `Pure/Corpus/**` + `tests/corpus/**`
→ R1 + R3.

### Rule (cross-check C6) — wrapper counts are relative, never literal

**No criterion in this contract may state a literal `scripts/test.sh` wrapper count.** Where a count is
asserted it reads: *"`bash scripts/test.sh` is green at **N+1**, where **N** is the wrapper count
printed by `bash scripts/test.sh` on this branch's **rebase baseline** (`git merge-base HEAD main`),
captured and pasted in the handoff note **before** the new wrapper is added."* Rationale: CM-C9,
CM-C10, CM-C11, DEVCAP and DEVFIX each add a wrapper in parallel, so the draft's literal "11/11" is
false for whichever lands second — and this contract is **Wave 2**, so at least one of them will
already have landed. This contract adds **exactly one** wrapper
(`tests/corpus/alternation-band.test.sh`) → target **N+1**.

### JOINT NOTE — C5: CM-C5.1's scope × this contract's L006 anchor (defaults stand; **the pair is one decision**)

This note is **identical in `t3-dead-mechanic-frozen-ready.md` and `t3-levels-frozen-ready.md`** and
must stay identical if either is amended.

- **Defaults at freeze.** DM (**CM-C5.1**) ships **scope = `meta.newMechanic` only** (its H2 default).
  LVL (**CM-C11**) ships **CONFLICT-1 option A** — L006 byte-faithful to the authored anchor
  (`docs/plan/data/example_levels.json:19-36`, `product_spec.md:540`).
- **Under those two defaults there is no conflict, and no coverage either.** All five of L006–L010
  declare `newMechanic: null` (`docs/prd/PRD.md:187` — the next new mechanic is second-source at
  L018), so the dead-mechanic gate reports `SKIPPED(no declared newMechanic)` for **every level in the
  alternation band** and never judges the L006 anchor's provably dead `queue` capacity. **The cost,
  recorded rather than hidden:** the band gets zero coverage from the very gate written for the defect
  class it exhibits. The only thing covering it is **criterion 6 below** (in-band liveness assertion,
  with the authored L006 anchor as its *negative control* — max queue depth 0).
- **Widening re-opens the anchor.** If **HC-14** later widens that gate to every entry of
  `meta.mechanics`, then L006 — which declares `mechanics ["switch","queue"]` with a queue that never
  holds a cat — **fails the gate**, and **HC-10 option A (anchor fidelity) and the gate's blocking
  posture cannot both hold**. Widening therefore forces one of: HC-10 **(B)** re-author (free only
  under NEW-Q1 branch Q1-A), HC-10 **(C)** minimum delta, a recorded per-level exemption, or the gate
  dropping to `Warn` for the carried-mechanic limb.
- **Neither contract may resolve this alone.** HC-10 and HC-14 are a **single human call**; each
  contract's stop conditions route to it (this contract's stop condition 9 / CM-C5.1's stop
  condition 10). An implementer who "fixes" one side unilaterally has failed review.

### Revision log vs `t3-levels-l006-l010-draft-contract.md`

1. Title/id/branch: `L006–L010` → **`CM-C11`** (R4); Wave-2 placement and the `CM-C10`-merged dependency added to the header.
2. Ratifications block added (above). OPEN-1 narrowed (R1), OPEN-4 closed (R3), **OPEN-5 closed negatively** (dev-only override ratified; the failable-campaign-level alternative is dead).
3. **Criterion 9 rewritten**: staging is `bash scripts/stage-content.sh --apply` output; `.meta` guids are the stager's deterministic path-derived values; the hand-copy / fresh-guid language is deleted. Its check gains the stager's own check-mode run alongside the untouched `editmode.test.sh` run.
4. Criterion 11: literal `11/11` → **N+1** (C6 rule).
5. Criterion 7's justification updated for the dev-only override ratification — **the criterion itself is unchanged and un-weakened**.
6. Goal bullet (b) reworded for the same reason; a rendering/URP note added (no criterion depends on rendering).
7. Joint C5 note added (identical text in CM-C5.1); stop condition 9 extended to name HC-14.
8. Stop condition 8 rewritten around the stager (staging failure ≠ hand-copy fallback).
9. Open-questions table replaced by a **RIDES-WITH-PR** table (OPEN-1 residual, OPEN-2/HC-10 joint, OPEN-3/HC-11, OPEN-6/HC-12, OPEN-7/HC-13, merge delegation).
10. **No criterion was weakened, renumbered or removed.** 12 criteria in, 12 criteria out.

---

## Goal

The campaign corpus grows 5/30 → 10/30: the **alternation** band (L006–L010, Harbor Line) is
authored to the LOCKED band table (`docs/plan/specs/product_spec.md:522,566-570`), passes every
blocking validator stage, and — unlike the shipped onboarding band — is authored so that
(a) the declared `queue` mechanic is **provably alive** on the winning line, (b) each level has a
**reachable non-pinned failure** (`QueueOverflow` or `TimeOut`), so the CM-C3 fail/retry loop has real
campaign content to run on *(note: since the 2026-08-05 ratification, the **device** fail/retry cycles
are served by a dev-only failable-level override, so this is authoring quality, not a device-session
dependency)*, and (c) jitter retention holds at ≥70% **under both readings of NEW-Q4**, so the
PROJECT_STATE risk trigger cannot force a redesign of this band later. Greybox-**validated** is the
bar — validator-green plus the tests below; **no visual or rendering claim is made** (URP restoration
rides CM-C2b-DEVFIX). Taste stays human (stage 11 PENDING is expected).

---

## Spec reference (every claim anchored to a line read on 2026-08-05)

**Band law.** `docs/plan/specs/product_spec.md:522` (alternation = L006–L010, difficultyTarget
0.18–0.28, first-attempt 80–88%, mechanics *switch, queue*) · `:566-570` (per-level LOCKED row:
L006 Alternating Line 0.20 · L007 Ferry Timing 0.22 · L008 Double Berth 0.24 · L009 Tide Tables
0.26 · L010 Harbor Capstone 0.28; district = Harbor Line) · encoded and enforced at
`unity/Assets/Scripts/Content/Validation/CorpusValidator.cs:266` (`("alternation", 6, 10, 0.18, 0.28)`)
and asserted at `:314-341`.
**Mechanic order.** `docs/prd/PRD.md:185,187` (one new mechanic; next new is second-source at
L018) · enforced at `CorpusValidator.cs:279-299`. No level in this band introduces a mechanic.
**Duration.** `product_spec.md:551` (alternation anchor L006 = 260 ticks → 32.5 s; duration target
~25–40 s) and `:542-546` (AMD-02: anchors ship as authored, the `~` endpoints are **provisional,
human sign-off pending**) — so seconds are **printed, never compared** (`CorpusValidator.cs:110-111`
stamps `secondsVerdict: PINNED(NEW-Q1)`; PRD `:228-231` holds both Q1-A/Q1-B branches open).
**Accessibility.** `product_spec.md:642` (jitter ≥70%; window floor 6 ticks; *onboarding* uses
12–16 — **no alternation range exists in the corpus**, see the RIDES-WITH-PR row OPEN-3) · schema
floor 3 (`docs/plan/data/level_schema.json:23`).
**Schema v2 (FROZEN).** `level_schema.json:16` (`teachingGoal` is REQUIRED meta), `:18` (band
enum), `:22` (≤160 chars), `:24` (`authoredBy` enum), `:25` + ADR-0008:119-123 (`validatedAt`
never authored — AMD-09), `:40` (queueCapacity 1–8), `:87` (routes 2–3), `:108` (waves ≤30),
`:112-116` (tick ≤2000, count ≤8, spacing 1–40).
**Shipped simulation semantics the boards must be authored against** (all verified in
`unity/Assets/Scripts/Domain/Simulation.cs`): emission enqueues only when the source queue is
non-empty or the mouth is occupied (`:73-76`); **one queued head releases per node per tick**
(`:89-100`); a mouth is occupied only for the tick a train enters (`:173-179`) — so a queue forms
only where **>1 arrival per tick** occurs; an arrival at a junction with a free mouth passes
through without touching the queue (`:19-24`, A-C1-8 iv); a **non-matching station arrival throws**
(`:114-117`, pinned NEW-Q4); a junction arrival with a blocked/absent route enqueues (`:123-127`);
`QueueOverflow` fires 16 ticks after a capacity node first reaches its cap (`:131-151`);
`TimeOut` fires at `Tick >= TimeLimitTicks` after the win check (`:157-162`); `Enqueue` **throws**
past the digest bound `QCapBound` (=8) (`:192-193`).
**Solver.** `unity/Assets/Scripts/Domain/Solver/LevelSolver.cs:23-24` — exact BFS **iff
`SwitchRoutes.Length <= 2`**, reporting `beamWidthUsed = 0`; 3+ switches fall to beam and can
report `NotFound(Beam)` (`:41-43`), which is a **warn**, not a proof (`ValidationStages.cs:389-392`).
Budget `SolverBounds.cs:13` = 2,000,000 expansions (committed fixtures peak at 23,390).
**Validator stages consumed.** static `ValidationStages.cs:219-313` · lower-bound `:316-349`
(UNCONFIGURED, Q-R) · solver `:373-398` · triviality `:401-415` · brittleness `:418-514` (band window
law is **onboarding-only** at `:426-427`; retention is measured over **unpinned** samples at
`:439-448,476-480`; windows at `:486-505`; the printed value string at `:507-508`) · stars `:528-543`
(UNCONFIGURED) · difficulty `:564-619` (UNCONFIGURED) · novelty `:661-711` (UNCONFIGURED) · staleness
`:714-736` · playtest `:764-776`. Thresholds file `config/validator_thresholds.json:2-3`
(jitterSampleCount 20; the four Q-R rows deliberately absent).
**Report shape the checks parse.** `CorpusValidator.cs:87-96` (per-stage `stage/code/detail/value/
blocks`), `:98-113` (`solve` block incl. `beamWidthUsed`, `pinnedPruned`, `seconds`).
**Gates this contract must keep green.** `tests/unity/editmode.test.sh:17-27` — **set equality both
directions** between `content/levels/*.json` and `unity/Assets/StreamingAssets/content/levels/*.json`
plus per-file byte identity · `tests/validation/validator.test.sh:12,15-22` (corpus glob non-empty;
inputs byte-identical after a gate run; exit 0 over the full corpus) ·
`tests/content/importer.test.sh:22-26` (**exactly one** `new JsonSerializerSettings` under
`unity/Assets`, tests included) · `scripts/check.sh:62` (no `UnityEngine` token — **comments
included** — under `unity/Assets/Tests/EditMode/Pure`) · **and, from Wave 1,
`scripts/stage-content.sh` check mode** (CM-C10 criterion 1) plus **CM-C5.1's campaign liveness
verdict** (which reports `SKIPPED` for this band under the joint note).
**Review lessons carried forward.** `state/PROJECT_STATE.md:58` (F5: no gate detects a
declared-but-dead mechanic — now CM-C5.1's contract) · `:60` (F4: retention measured over unpinned
samples; L002/L003/L005 would read 65%/75%/60% if NEW-Q4 resolves misroute-as-loss) ·
`evals/results/device/c2b-crit8/ARTIFACT.md:97-100` (**F-DEV-3**: L001 can only Win or Halt —
both junction exits terminate at stations, so a mismatched cat throws and FailureReview/TimeOut are
unreachable; **the device cycles are now served by the ratified dev-only failable-level override**,
so criterion 7 is authoring quality rather than a device unblock).

---

## CONFLICT-1 — the L006 anchor cannot satisfy this contract's teaching obligations (human call, default A)

`product_spec.md:540` says "Anchors L001/L006/L018 ship **exactly as in example_levels.json**", and
PRD `:230` (branch Q1-B) repeats it. The authored anchor is `docs/plan/data/example_levels.json:19-36`.
Two defects in it, both verified against shipped code:

1. **The declared `queue` mechanic is dead.** `queueCapacity: 3` sits on J1 (`:22`), but the four
   waves emit one cat per tick at ticks 8/24/48/64/88/104/128/144 (`:29-32`, spacing 16) and E1 is 8
   ticks (`:23`), so arrivals at J1 are ≥16 ticks apart. A node releases one train per tick and the
   mouth is free after one tick (`Simulation.cs:89-100,173-179`), so **J1's queue never holds a
   cat** — the capacity is decorative. This is exactly the F5/dead-mechanic class
   (`state/PROJECT_STATE.md:58`) that the L005 review caught pre-merge, and the class **CM-C5.1**
   gates — though not for this band under the joint note's defaults.
2. **It is unfailable except by the pinned halt.** Both S1 routes terminate at stations
   (`:23,26-27`), so any misroute throws at `Simulation.cs:116` and every correct line wins at
   ~tick 164 < the 260 limit. It is the **F-DEV-3 pathology verbatim** — Win or Halt, no
   `FailureReview`, no `TimeOut`.

**Options (the human picks; the contract does not).**
- **(A) Anchor fidelity wins — DEFAULT for execution, and the default that stands at freeze.** L006
  ships byte-faithful to `example_levels.json:19-36`. Criteria 6 and 7 below are then scoped to
  **L007–L010** and L006's dead queue + unfailability are recorded as known, accepted deviations in
  the PR. Cheapest, keeps Q1-B open, but ships a second Win-or-Halt level and a second dead `queue`
  declaration.
- **(B) Re-author L006.** Free **if** NEW-Q1 resolves to Q1-A (which already re-authors the three
  anchors, PRD `:229`); otherwise it contradicts `product_spec.md:540`. Criteria 6/7 then cover all
  five levels.
- **(C) Minimum delta.** Keep every authored field except the two that make the mechanic real
  (e.g. an overlapping wave so cats genuinely queue, and one non-station exit), recorded as a named
  amendment to the anchor. Cheaper than (B), still breaks the byte-faithful reading.

Execution proceeds under **(A)** unless the human says otherwise; the diff is a two-file change if
the answer arrives later. **See the JOINT NOTE: this choice is coupled to CM-C5.1's scope (HC-14) and
must be answered with it.**

---

## Acceptance criteria (12)

Each is met only when the named check exits 0 (or fails exactly as specified). "The report" means
`bash scripts/validate-content.sh --out <report.json>`. New tests are TDD-first: red before green.

1. **Five files exist and their authored metadata equals the LOCKED progression row.**
   `content/levels/L006.json` … `L010.json` are schema-v2 valid and carry exactly:
   `schemaVersion 2`; ids `L006`…`L010`; seeds `1006`…`1010` (continuing L001–L005's `1000+N`);
   `name` = `Alternating Line` / `Ferry Timing` / `Double Berth` / `Tide Tables` /
   `Harbor Capstone` (`product_spec.md:566-570`); `meta.band = "alternation"` for all five;
   `meta.difficultyTarget` = `0.20 / 0.22 / 0.24 / 0.26 / 0.28` (`:566-570`);
   `meta.mechanics = ["switch","queue"]` for all five (`:522`); `meta.newMechanic = null` for all
   five (next new mechanic is second-source at L018, `docs/prd/PRD.md:187`);
   `meta.authoredBy = "llm+validator"` (`level_schema.json:24`); `meta.teachingGoal` present,
   non-empty, ≤160 chars (`:22`) and **distinct across all ten campaign levels**;
   `meta.validatedAt` **absent** (AMD-09 / ADR-0008:119-123).
   *Check:* one NUnit case per field family per level in
   `unity/Assets/Tests/EditMode/Pure/Corpus/AlternationBandTests.cs`, asserted from a **raw-JSON
   key walk** (so a parser bug cannot mask a content bug — L002-L005/CM-C2a criterion 1 pattern),
   plus one case asserting `meta` has no `validatedAt` key, plus one asserting the ten
   `teachingGoal` strings are pairwise distinct.

2. **The L006 anchor is honoured (CONFLICT-1 option A).** `content/levels/L006.json` parses to the
   same authored values as `docs/plan/data/example_levels.json:19-36` — field-for-field, including
   `win.timeLimitTicks == 260`, `win.deliveries == 8`, `win.perfectMaxSwitches == 4`,
   `stars {two:700, three:950}`, `economy {baseTickets:25, perfectBonus:15}`,
   `minActionWindowTicks == 12`, and the board/wave arrays.
   *Check:* one NUnit case diffing the parsed L006 DTO against the anchor object extracted from
   `example_levels.json` (both read as bytes; JSON formatting may differ, values may not). Under
   options B/C this criterion is replaced by the human-approved delta and the case is rewritten to
   the approved values — never deleted.

3. **Every blocking stage is green for all five, and the whole corpus gate still exits 0.**
   Per level: stage 1 Schema PASS · stage 2 StaticAnalysis PASS or WARN (decoy-station warns are
   allowed, `ValidationStages.cs:231-245`) · stage 4 Solver **verdict `Solved`** with
   `beamWidthUsed == 0` (i.e. BFS-exact — ≤2 switches, `LevelSolver.cs:23-24`) · stage 5
   TrivialityReject PASS (the zero-input run must not win; a pinned or timed-out zero-input run
   both satisfy it, `ValidationStages.cs:404-415`) · stage 6 Brittleness PASS.
   *Check:* `bash scripts/validate-content.sh --out report.json` exits 0 over the full 10-level
   corpus + the stress boards, and `tests/corpus/alternation-band.test.sh` parses the report and
   fails if any of the five rows above is not the stated code, or if `solve.verdict != "Solved"`,
   or if `solve.beamWidthUsed != 0`, for any of L006–L010. **The run must also show the CM-C5.1
   campaign liveness verdict as `SKIPPED(no declared newMechanic)` for all five** (joint note) — if
   it shows anything else, the joint decision moved and stop condition 9 fires.

4. **Retention holds under BOTH readings of NEW-Q4 (discharges the F4 risk trigger).** For each of
   L006–L010, with `jitterSampleCount = 20` (`config/validator_thresholds.json:3`):
   (a) the shipped optimistic rule passes — stage 6 PASS, retention ≥70% over unpinned samples
   (`ValidationStages.cs:476-480`); **and** (b) the pessimistic rule passes —
   `wins*100 / (wins+losses+pinned) >= 70`, i.e. every pinned jitter sample counted as a loss.
   *Check:* `tests/corpus/alternation-band.test.sh` parses the stage-6 `value` string
   (`retention=<x> (wins=W losses=L pinned=P) windows=[...]`, format fixed at
   `ValidationStages.cs:507-508`) for each of the five and fails when `W*100/(W+L+P) < 70`, printing
   both readings per level. **This is the criterion that keeps `state/PROJECT_STATE.md:60` from ever
   reopening this band**; L002–L005 are not retro-fitted here (out of scope, see Scope boundary).

5. **Action windows are floored and printed.** Every level declares
   `meta.minActionWindowTicks = 12` (the only corpus-sourced number for this band —
   `example_levels.json:20`; see the RIDES-WITH-PR row OPEN-3), which is ≥ the spec floor 6
   (`product_spec.md:642`) and ≥ the schema floor 3 (`level_schema.json:23`); and every entry window
   measured on the solver-optimal log is ≥ 12 (`ValidationStages.cs:502-504` already fails the stage
   otherwise).
   *Check:* the wrapper asserts `minActionWindowTicks == 12` in all five files **and** parses
   `windows=[...]` from the stage-6 value, failing if any element `< 12` or if the array is empty.
   (The empty-array guard matters: a zero-command winning log would make the window law vacuous.)

6. **The declared `queue` mechanic is provably alive** *(L007–L010 under CONFLICT-1 option A; all
   five under B/C)*. Re-running the solver-optimal winning log tick-by-tick through
   `Simulation.Step`, at least one node carrying an authored `queueCapacity` reaches
   `NodeQueueCounts[n] >= 2` at some tick — a real line, not a one-tick mouth block
   (`Simulation.cs:173-179`; the shipped L005 pattern reaches depth 3, `content/levels/L005.json:99-113`).
   *Check:* one NUnit case per level in `Pure/Corpus/` that solves the level, replays the optimal
   log through `Simulation.Step`, records max depth per node, asserts the bound, and prints the
   table. Negative control in the same file: the same helper run against the **authored L006
   anchor** must report max depth 0 (proving the assertion can fail — this is the CONFLICT-1
   evidence, and it is a real red test, not a tautology). **Under the joint note this criterion is
   the band's only liveness coverage**, because CM-C5.1 skips every `newMechanic: null` level.

7. **Each level has a reachable failure that is NOT the pinned halt** *(same scoping as 6)*. For
   each level a committed **witness command log** (a plausible mis-play, ≤6 entries) driven through
   `ReplayHasher.RunToEnd` terminates in `Failed(QueueOverflow)` **or** `Failed(TimeOut)` and
   **throws nothing**. Band coverage: **≥2 levels reach `QueueOverflow`** and **≥1 reaches
   `TimeOut`**, so both CM-C3 camera rules (overloaded node; Q-K largest-queue tie-break) have real
   campaign content. Sanctioned authoring levers, all verified against shipped code — no Domain
   change is needed or permitted:
   - **holding node:** a switch route ending at a **non-station** node with an authored
     `queueCapacity` — arrivals enqueue (`Simulation.cs:123-127`) and overflow 16 ticks after the
     cap is reached (`:131-151`). Authoring rule: `cap + (arrivals within those 16 ticks) <= 8`, or
     `Enqueue` throws at the digest bound (`:192-193`) and the failure is a crash, not a loss.
   - **rejoining asymmetric paths:** both routes reach the correctly-coloured station, one slower
     (L007's authored teaching element, `product_spec.md:567`) — a wrong choice costs ticks and
     ends in `TimeOut` (`:159-162`) with **no pin reachable at all**, which also drives criterion
     4's pinned count toward 0.
   *Check:* one NUnit case per level asserting `Outcome.Kind == Failed`, the named `FailReason`,
   and `Assert.DoesNotThrow`; plus one wrapper/test assertion that the band's witness set contains
   ≥2 `QueueOverflow` and ≥1 `TimeOut`. Under option A the PR states in one line that L006 is
   exempt and why (F-DEV-3 class).
   *Justification note (2026-08-05 ratification):* the **device** fail/retry cycles are served by a
   dev-only failable-level override, so this criterion no longer unblocks the device session. It is
   retained **un-weakened** as authoring quality and as real campaign content for the shipped
   CM-C3 loop.

8. **Campaign assertions and non-regression.** The report's campaign block shows: mechanic order
   **PASS** (`CorpusValidator.cs:279-299`), band table **PASS** (all ten ids inside their band's
   L-range and difficultyTarget inside its band's range, `:314-341`), corpus count **10/30 PENDING**
   (non-blocking, expected, `:301-306`); and `git diff --stat -- content/levels/L001.json
   content/levels/L002.json content/levels/L003.json content/levels/L004.json
   content/levels/L005.json` is **empty**.
   *Check:* wrapper greps the campaign verdicts in the report for `PASS` on the two blocking rows
   and `10/30` on the count row, and runs the `git diff` emptiness assertion (fails loudly if any
   shipped level moved a byte). **If CM-C5.1 has merged, its liveness rows are also present and must
   not block** (criterion 3).

9. **The five levels are staged BY THE STAGER and byte-identical, so the shipping gate stays green.**
   Each new level is also present at `unity/Assets/StreamingAssets/content/levels/L00N.json`,
   byte-identical to its `content/levels/` source, each with a committed `.json.meta`. **Both the
   staged payloads and their `.meta` files are produced by running `bash scripts/stage-content.sh
   --apply` (CM-C10) and committed as its mechanical output** — R1: the stager is the single staging
   author, and the `.meta` guids are its deterministic path-derived values. **No file under
   `unity/Assets/StreamingAssets/**` may be hand-copied, hand-edited or hand-authored in this
   contract, and no guid may be invented or copied from `L005.json.meta`.** L001–L005, their staged
   copies, their `.meta` files and the folder `.meta` files are byte-unchanged.
   *Check:* (a) `bash scripts/stage-content.sh` (check mode, no args) exits 0 with an empty
   `git status --porcelain` afterwards — i.e. the committed staged tree already equals the stager's
   output; (b) `bash tests/unity/editmode.test.sh` exits 0 — its set-equality and per-file `cmp` at
   `:17-24` fail closed on any drift; (c) `git diff --name-only` shows exactly ten new
   StreamingAssets paths (5 `.json` + 5 `.json.meta`) and no modification to any pre-existing one.
   **This wrapper is NOT edited by this contract** (it belongs to the UX lane's gate-evolution
   carve-out, `SESSION-HANDOFF-ux.md:36-39`), and **`scripts/stage-content.sh` is NOT edited either**
   (CM-C10's). See the RIDES-WITH-PR row OPEN-1: landing the stager's output for five **new** files
   still touches CM-C2b's glob.

10. **Evidence table in the PR, machine-derived.** For each of L006–L010 the PR pastes, straight
    from `report.json`: `solve.completionTicks` · `solve.seconds` (printed with its
    `secondsVerdict: PINNED(NEW-Q1)` label — **never compared** to 40–75 s or to the provisional
    ~25–40 s of `product_spec.md:551`, whose endpoints are unsigned-off at `:546`) ·
    `solve.switchesUsed` · `solve.beamWidthUsed` · `solve.pinnedPruned` · `solve.nodesExpanded` ·
    stage-6 retention (both readings) and `windows=[...]` · max queue depth per capacity node
    (criterion 6) · witness fail reason (criterion 7) · wall-clock of the whole gate run.
    *Check:* the wrapper fails if any of the five level objects in the report is missing a `solve`
    block or a stage-6 `value`, so the table can never be hand-waved.

11. **Gate hygiene — nothing else moved.** `bash scripts/check.sh` OK (note `:62` bans the
    `UnityEngine` token **in comments too** under `Tests/EditMode/Pure` — the new Corpus tests must
    not name it); `bash scripts/test.sh` green at **N+1**, where **N** is the wrapper count on this
    branch's rebase baseline (`git merge-base HEAD main`), captured **before** the new wrapper was
    added and pasted with the after-count in the PR — **never a literal** (C6 rule above; this
    contract is Wave 2, so at least one other lane's wrapper is already in **N**);
    `grep -ro --include='*.cs' 'new JsonSerializerSettings' unity/Assets | wc -l` still **1**
    (`tests/content/importer.test.sh:22-26`); `tests/validation/validator.test.sh` still green,
    including its "the gate run modified an input file" belt at `:14-22` now covering ten levels;
    `bash tests/staging/stage-content.test.sh` (CM-C10's) still green.
    *Check:* the commands above, exit codes and both wrapper counts pasted in the PR.

12. **No new dependency, no frozen-surface edit.** Zero additions to any `.csproj`/`packages.lock.json`
    (the new tests compile through the existing link-glob
    `dotnet/CatMetro.Tests/CatMetro.Tests.csproj:17` over `unity/Assets/Tests/EditMode/Pure/**/*.cs`
    — `state/backlog.md:148-152`), zero edits under `unity/Assets/Scripts/**`, zero edits to
    `docs/plan/data/level_schema.json`, `config/validator_thresholds.json`,
    `config/runtime_bounds.json`, `scripts/stage-content.sh`, or any immutable path.
    *Check:* `git diff --name-only main...HEAD` reviewed against the file table below; the PR
    asserts the list is a subset. Any `Compile Include` append is a stop condition, not a fix.

---

## Scope boundary — complete file table

| Path | Action | Ownership basis |
|---|---|---|
| `content/levels/L006.json` … `L010.json` | **create** (5 files) | `state/backlog.md:116` glob is CM-C2a's; L002-L005 (#15) is the standing precedent for a content contract writing new level files there |
| `unity/Assets/StreamingAssets/content/levels/L006.json` … `L010.json` | **create** (5 files) — **as `scripts/stage-content.sh --apply` output only** | CM-C2b's glob (`state/backlog.md:117`); R1 makes CM-C10 the staging author — see RIDES-WITH-PR OPEN-1 (new files only) |
| `unity/Assets/StreamingAssets/content/levels/L006.json.meta` … `L010.json.meta` | **create** (5 files) — **stager-generated, deterministic path-derived guids** | same; **not** hand-authored, **not** copied from `L005.json.meta` (R1 / cross-check C3) |
| `unity/Assets/Tests/EditMode/Pure/Corpus/AlternationBandTests.cs` | **create** | ownership row drafted in the delegated batch (`t3-backlog-amendment.md` EDIT-4); path unowned today |
| `unity/Assets/Tests/EditMode/Pure/Corpus/BandFixtures.cs` | **create** | same row (shared loaders; tests may use file APIs — `ValidationFixtures.cs:13`) |
| `unity/Assets/Tests/EditMode/Pure/Corpus/*.cs.meta` + `unity/Assets/Tests/EditMode/Pure/Corpus.meta` | **create** | same row; editor-generated guids (these are **not** staged files, so the stager does not touch them) |
| `tests/corpus/alternation-band.test.sh` | **create** | same delegated row — `tests/corpus/**`; discovered by `scripts/test.sh:18` |
| `state/handoffs/CM-C11.md`, `state/handoffs/CM-C11-frozen-contract.md` | **create** | session log / frozen-contract copy, this lane's own files |
| `state/PROJECT_STATE.md` | **append ONE line at merge** | four lanes append in parallel (`SESSION-HANDOFF-ux.md:45-49`) |

**Explicitly NOT touched** (a diff here is out of scope, AGENTS.md hard rule 4):
`content/levels/L001–L005.json` and their staged copies · `scripts/stage-content.sh` and
`tests/staging/**` (CM-C10's) · `unity/Assets/Scripts/Content/Validation/**` and
`tests/validation/**` (CM-C5/CM-C5.1's) · `docs/plan/**` (including `example_levels.json`,
`level_schema.json`, `product_spec.md`) · `unity/Assets/Scripts/**` (Domain, Content,
Content/Validation, Domain/Solver, Presentation, Application, **Bootstrap** — DEVCAP/DEVFIX in
flight) · `unity/Assets/Resources/Strings/ui.csv` (UX lane; append-only when it is touched at all —
this contract adds no UI string) · `unity/ProjectSettings/**` (DEVFIX in flight, URP restoration) ·
`tests/unity/editmode.test.sh`, `tests/unity/failure.test.sh`, `tests/content/**`, `tests/solver/**`,
`tests/taxonomy/**` (CM-C9's) · `scripts/*.sh` · `config/**` · `.github/**` · `docs/adr/**` ·
`state/backlog.md` (the ownership rows are applied by the human from `t3-backlog-amendment.md`) ·
every immutable path (`tests/contract/`, `docs/constitution.md`, `.claude/hooks/`,
`scripts/git-hooks/`, `state/mode`, `state/trust.json`, `evals/` except `evals/results/`, and never
`evals/results/attested/`).

**Explicit non-goals:** no schema change; no validator-code change (including the
alternation-band window rule that `ValidationStages.cs:426-427` does not have — see OPEN-3); no
dead-mechanic **gate** (that is **CM-C5.1**, Wave 1 — this contract discharges the defect **for this
band by authoring**, not by gating); no staging-tool change (CM-C10 owns it); no
catalog.json/content.sha256; no ContentSync; no level-select or district UI; no scoring, stars or
tickets (pins NEW-Q5 / NEW-Q7); no thresholds authored into `config/validator_thresholds.json` (Q-R);
no `meta.validatedAt` stamping (Q-O); no retro-fit of L001–L005 to criteria 4/6/7; no rendering,
material, shader or URP work (CM-C2b-DEVFIX); no monetization surface of any kind.

---

## Assumptions

- **A-L6-1** Campaign play order is id order and this band is L006–L010, Harbor Line
  (`product_spec.md:566-570`); the schema has **no `district` field** (Q-F, `state/backlog.md:47`),
  so the district exists only in the level `name`s and in the spec table. CM-R09.1's "5 per district
  × 6" stays unimplementable until Q-F resolves. No agent invents a `tags` convention for it.
- **A-L6-2** All five declare `mechanics ["switch","queue"]` with `newMechanic null`. The order
  stage passes trivially because `queue` was introduced at L004
  (`CorpusValidator.cs:279-299`) — which is exactly why criterion 6 exists, and why **CM-C5.1 skips
  this band** under the joint note.
- **A-L6-3** ≤2 switches per level, so every level is BFS-exact and its `Solved` verdict is a
  proof rather than a beam miss (`LevelSolver.cs:23-24,41-43`). L008's "two switches, one line"
  (`product_spec.md:568`) sits exactly at the boundary; L009/L010 stay at 2 and grow difficulty
  through wave density and colour interleave (axes E/C, `product_spec.md:507-508`), not switch count.
- **A-L6-4** Colours stay within the shipped set red/blue/yellow/green (`LevelGraph.cs:9-14`);
  `wild` is construction-guarded and throws (`LevelGraph.cs:67-69`, NEW-Q35). L009's authored
  "3-color waves" (`product_spec.md:569`) is therefore red/blue/yellow with a third station —
  colours are not mechanics, so CM-R06.1's four-mechanic set is untouched.
- **A-L6-5** Single source only (second source is pinned out until L018 —
  `LevelGraph.cs:64` throws on a second source). Every multi-exit junction carries a switch
  (`Simulation.cs:214-223` picks the first outgoing edge otherwise, which is a silent trap).
- **A-L6-6** `minActionWindowTicks = 12` for all five, from the only corpus source for the band
  (`example_levels.json:20`). A descending ramp is **not** authored because no alternation range
  exists anywhere in the corpus (OPEN-3).
- **A-L6-7** `win.stars`, `economy.*` and `win.perfectMaxSwitches` are authored only to keep stage 7
  schema-legal (`1 <= two < three`, `ValidationStages.cs:537-539`); they carry no scoring meaning
  while NEW-Q5/NEW-Q7 are pinned, and stage 7 reports `UNCONFIGURED(starBandSlack)` either way.
- **A-L6-8** Stages 3, 7, 8, 9 report `UNCONFIGURED` and stage 10 reports `STALE`, both
  non-blocking, exactly as they do for L001–L005 (Q-R at `state/backlog.md:59`; Q-O at `:56`).
  A level is **not** judged on those stages by this contract.
- **A-L6-9** The witness fail logs of criterion 7 are test fixtures under `Pure/Corpus/`, not
  content: no new file format, no golden, and `tests/contract/` is never touched.
- **A-L6-10 (staging is a tool run, not an authoring act).** Criterion 9's staged files are produced
  by `bash scripts/stage-content.sh --apply` on this branch and committed as-is. If the stager's
  output differs from what the author expected, that is a **finding**, not something to hand-fix
  (stop condition 8). The stager is CM-C10's; this contract neither edits it nor works around it.
- **A-L6-11 (no rendering claim).** "Greybox-validated" means validator-green plus this contract's
  tests. URP/shader restoration rides **CM-C2b-DEVFIX**; nothing here asserts a pixel.

---

## Stop conditions

1. **A schema change is needed** (a district field, a per-level flag, anything) → **stop**; schema v2
   is frozen (ADR-0008 §Level schema v2 is frozen) and Q-F is a human question.
2. **A Domain edit looks necessary** — including "make misroute a loss instead of a throw", a
   non-throwing legality probe, or raising `SolverBounds` — → **stop**. Those are NEW-Q4/Q-N and
   CM-C1/CM-C4 surfaces, and a Domain edit invalidates `tests/contract/replay-hash-golden.json`.
3. **A validator-code edit looks necessary** — e.g. adding an `alternation` row to the band window
   law at `ValidationStages.cs:426-427`, or a threshold row in `config/validator_thresholds.json` —
   → **stop and ask** (OPEN-3, Q-R). No agent picks a product number.
4. **A level cannot pass brittleness without dropping `minActionWindowTicks` below 12** → redesign
   the board; **never** lower the band row (L002-L005 stop condition, `state/handoffs/L002-L005.md:51-52`).
5. **A level cannot pass criterion 4's pessimistic reading without a redesign** → redesign the
   board. Do **not** weaken criterion 4 to the shipped optimistic rule; that is the whole point of
   the criterion.
6. **A level reports `NotFound(Beam)`, `NotFound(Budget)`, `Indeterminate` or `Unsolvable`** →
   redesign (fewer switches, shorter horizon, fewer trains). Never raise the solver budget, never
   inject beam widths, never mark a warn as a pass.
7. **The gate run's wall clock becomes hostile** (a single level dominating the run, or the corpus
   run pushing `scripts/test.sh` past a few minutes) → stop, record `nodesExpanded` and the wall
   clock, and hand the sizing question to the human; `SolverBounds.cs:10-12` already anticipates a
   budget amendment and it is CM-C4's, not this contract's.
8. **Staging cannot be done by the stager** — CM-C10 is not merged, `--apply` errors, its output
   drifts from expectation, or the StreamingAssets grant (OPEN-1) is refused → **stop before
   committing any staged file**. **Hand-copying is not a fallback** (R1: one author only), and
   without staging `tests/unity/editmode.test.sh:17-24` fails set-equality the moment the first level
   lands in `content/levels/` — a wrapper this contract may not edit. In that case the band waits.
9. **CONFLICT-1 is answered as (B) or (C), or CM-C5.1's scope (HC-14) is widened** → stop and re-cut
   criteria 2/6/7 before authoring L006; do not improvise a partial re-author. Per the JOINT NOTE the
   two questions are one decision.
10. **Any `Compile Include` append or new csproj appears necessary** → stop
    (`state/backlog.md:146-155`).
11. **A taste/fun judgment is required** (is the band fun? does the ramp feel right?) → that is
    stage 11 / TG gates / human; record it, never assert it.
12. **Any need to edit `scripts/stage-content.sh`, `tests/unity/editmode.test.sh`,
    `tests/validation/**`, `state/backlog.md`, `docs/plan/**` or an immutable path** → stop; each
    belongs to another contract or another human.

---

### RIDES-WITH-PR human calls (default recorded; ratify at review/merge)

| # | Call | Default this contract ships | Coupling / gate |
|---|---|---|---|
| **OPEN-1 (residual)** | **May the stager's mechanical output for five NEW files land in this PR**, inside CM-C2b's `unity/Assets/StreamingAssets/**` glob (`state/backlog.md:117`) — new files only, never touching L001–L005 or the folder `.meta`? | Yes, as **CM-C10 `--apply` output** only (criterion 9). R1 settled *who authors*; this is the narrower per-PR landing question, and it is drafted as the **non-delegated appendix** of `t3-backlog-amendment.md`. | Blocks **start** if refused (stop condition 8). |
| **OPEN-2 / HC-10** | **CONFLICT-1: does L006 ship byte-faithful to the anchor** (`product_spec.md:540`, PRD Q1-B) despite its dead `queue` and Win-or-Halt shape — or (B) re-author / (C) minimum delta? | **(A) anchor fidelity**; criteria 6/7 scoped to L007–L010; L006's two defects recorded as accepted deviations in the PR. | **JOINT with HC-14** — see the JOINT NOTE. Answer both or neither. |
| **OPEN-3 / HC-11** | **What is the alternation band's `minActionWindowTicks` range?** The corpus has a number for onboarding only (12–16, `product_spec.md:642`, hardcoded at `ValidationStages.cs:426-427`). | **12 flat**, the anchor's value (`example_levels.json:20`), asserted by criterion 5's own wrapper check. | Adding an `alternation` row to the validator is a CM-C5-owned code edit **and** a product number → stop condition 3. |
| **OPEN-6 / HC-12** | **Is the provisional ~25–40 s alternation duration target signed off** (`product_spec.md:551`, endpoints unsigned-off at `:546`) — and if so, does it become a blocking comparison? | **Printed, never compared** (NEW-Q1 pin, `CorpusValidator.cs:110-111`); criterion 10 pastes the value with its `PINNED(NEW-Q1)` label. | AMD-02 defers the endpoints to human sign-off; making it blocking would pre-empt NEW-Q1. |
| **OPEN-7 / HC-13** | **District identity (Q-F)** — L006–L010 are "Harbor Line" but schema v2 has no `district` field, so CM-R09.1's "5 per district × 6" stays unassertable. Add the field, use `tags`, or keep district in prose only? | **Prose only** (A-L6-1); nothing is authored either way. | A schema field is a frozen-schema change → **ADR gate**. |
| **HC-25** | **Merge-delegation re-confirmation for this lane this session** (`state/handoffs/SESSION-HANDOFF-device-testing.md:9-10`; Constitution Amendment 1). | Assume **not** delegated until the human re-confirms in-session. | Blocks **merge**, not work. |

**Closed since the draft:** OPEN-1's *writer-model* half by **R1** (stager, derived guids);
OPEN-4 (two new ownership rows) by **R3** (drafted in the delegated batch);
OPEN-5 (does a failable campaign level replace the dev-only hook?) by the **dev-only failable-level
override ratification — negatively: the alternative is dead, the band is not re-sequenced, and
criterion 7 stays as authoring quality**; the id/ordering questions by **R4**.

---
**Freeze-time ratification addendum (human, in-session 2026-08-05/06):** N1 writer-grant LANDED (#32 — staged-derived-tree exception class; this contract commits stager output under clause (i)); HC-10×HC-14 defaults CONFIRMED (gate scopes newMechanic-only; the L006 anchor stands as authored — widening the gate later re-opens this anchor, recorded in both contracts); CM-C5.1 posture ratified BLOCKING (this band must pass the gate to land); the CM-C3 device legs use the dev-only failable-level override (the failable-campaign-level alternative is closed).
