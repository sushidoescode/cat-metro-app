# CONTRACT CM-C5.1 — dead-`newMechanic` gate: the declared mechanic must be exercised in the solver-optimal trace

**Status:** **FREEZE-READY** (tranche 3, Wave 1). Supersedes `t3-dead-mechanic-gate-draft-contract.md`.
The id is no longer provisional — **`CM-C5.1`** was assigned by the human on 2026-08-05 (R4 below),
and the ownership inheritance from CM-C5 is acked in the delegated backlog batch (R3).
**Debt row it discharges:** `state/PROJECT_STATE.md:58` — "no gate detects a declared-but-dead
`newMechanic` — every blocking stage stays green whether L004's queue is alive or dead. Candidate: a
corpus assertion that the declared mechanic is exercised in the solver-optimal trace. Needs a
contract." (PR #15 review F5; the L005 dead-queue defect that motivated it is recorded at
`state/PROJECT_STATE.md:20` and the authoring law it rides on is `state/handoffs/L002-L005.md:27-29`.)
**Follow-up to:** CM-C5 (merged #10) — this contract inherits CM-C5's ownership rows
(`state/backlog.md:120`), acked in `t3-backlog-amendment.md` EDIT-5.
**DEPENDS-ON:** CM-C5 merged (the 11-stage validator + campaign assertions), CM-C4 merged (the
solver-optimal log). Both are on main.
**Branch:** `task/CM-C5.1-deadmech`, cut from latest `main`, in its own worktree.
**Wave:** **Wave 1**, in parallel with **CM-C9** (taxonomy) and **CM-C10** (stager) — ratified to land
**early**, so its first red-bar run answers F-K (do L001–L005 actually pass?) **before** the CM-C11
band is authored against it.
**Parallel-safety:** disjoint from the UX lane (`state/handoffs/SESSION-HANDOFF-ux.md:28-39` — the UX
lane is explicitly barred from `Content/**`, `Domain/**`, `scripts/`, `tests/` wrappers and
L006–L010) and from the Bootstrap-owned in-flight branches **CM-C3-DEVCAP** and **CM-C2b-DEVFIX**
(this contract touches no `Bootstrap/**`, no `Presentation/**`, no `ui.csv`, no
`tests/unity/editmode.test.sh`), and file-disjoint from CM-C9 and CM-C10 (cross-check §1 matrix). It
shares a *runtime* edge with the CM-C11 authoring lane: the gate will judge their levels — see the
joint note below, H1, and stop condition 1.

---

## Ratifications (human, in-session 2026-08-05) — binding; recorded before freeze

| # | Ratification as given | Effect on this contract |
|---|---|---|
| **R4 (ids + sequencing)** | **ids: TAX = CM-C9, stager = CM-C10, LVL = CM-C11, DM = CM-C5.1. Wave 1 = TAX + DM + stager in parallel worktrees; LVL is Wave 2, after the stager merges. DEVFIX's 7 Presentation lines precede the UX lane.** | Id, branch and handoff filenames fixed to `CM-C5.1`. A-DM-8's "id is provisional" is discharged. The Wave-1 placement is load-bearing for **F-K**: this contract's first red bar is what tells the CM-C11 authors whether the shipped corpus passes the gate, and it lands **before** they author. It also **bounds H1's blast radius**: the band is authored against a gate that already exists, rather than inheriting one mid-flight. |
| **R3 (HC-5)** | **The five-item backlog ownership amendment batch is delegated to the agent.** | **H4's ownership half is CLOSED**: `t3-backlog-amendment.md` EDIT-5 acks that CM-C5.1 inherits CM-C5's ownership rows (`state/backlog.md:120`) and adds `tests/validation/fixtures/dead-mechanic/**` under that inheritance. This contract never edits `state/backlog.md` itself. |
| **R1 (HC-1/HC-2)** *(recorded for context; no effect here)* | `scripts/stage-content.sh` (CM-C10) is THE single staging author, deterministic path-derived `.meta` guids; ContentSync **not** frozen now, re-cut later as assert-only. | This contract writes nothing under `unity/Assets/StreamingAssets/**` and stages nothing. Its two new fixtures live under `tests/validation/fixtures/dead-mechanic/**`, which is **not** a staging rule source (CM-C10 criterion 2 stages `content/levels/*.json` and `config/runtime_bounds.json` only) — so no fixture can ever reach the shipped tree. Its one new `.cs` + `.meta` pair under `unity/Assets/Scripts/Content/Validation/**` is **editor-generated** (A-DM-7), not stager output. |
| **Earlier same-session, tranche-3 relevant** | CM-C3's device legs use a **dev-only failable-level override**; **URP is being restored by CM-C2b-DEVFIX**. | Neither touches this contract: it renders nothing, writes no `Presentation/**`, `Bootstrap/**`, `ProjectSettings/**` or scene, and adds no device leg. Recorded so no implementer infers a dependency on either. |

**Cross-check dispositions applied to this file:** C5 (DM scope × CM-C11's L006 anchor) → the joint
note below, **defaults stand**; C6 (wrapper-count literals) → the rule below, applied to criteria 7
and 9; matrix rows `tests/validation/validator.test.sh` and `Pure/Validation/**` → covered by the R3
inheritance ack. No other cross-check finding names this contract.

### Rule (cross-check C6) — wrapper counts are relative, never literal

**No criterion in this contract may state a literal `scripts/test.sh` wrapper count.** Where a count is
asserted it reads: *"`bash scripts/test.sh` is green at **N**, where **N** is the wrapper count printed
by `bash scripts/test.sh` on this branch's **rebase baseline** (`git merge-base HEAD main`), captured
and pasted in the handoff note before any code is written."* **This contract adds no wrapper** — it
appends one labelled block to the existing `tests/validation/validator.test.sh` — so its target is
**N unchanged**, with `PASS tests/validation/validator.test.sh` present in the output. Rationale:
CM-C9, CM-C10, CM-C11, DEVCAP and DEVFIX are each adding a wrapper in parallel, so the draft's literal
"10/10" is false for whichever lands second; `state/PROJECT_STATE.md:8` already carries two different
literals (8/8, 10/10) from different days.

### JOINT NOTE — C5: DM scope × CM-C11's L006 anchor (defaults stand; **the pair is one decision**)

This note is **identical in `t3-dead-mechanic-frozen-ready.md` and `t3-levels-frozen-ready.md`** and
must stay identical if either is amended.

- **Defaults at freeze.** DM (**CM-C5.1**) ships **scope = `meta.newMechanic` only** (H2 default,
  criterion 2 + criterion 8's `SKIPPED(no declared newMechanic)` row). LVL (**CM-C11**) ships
  **CONFLICT-1 option A** — L006 byte-faithful to the authored anchor
  (`docs/plan/data/example_levels.json:19-36`, `product_spec.md:540`).
- **Under those two defaults there is no conflict, and no coverage either.** All five of L006–L010
  declare `newMechanic: null` (`docs/prd/PRD.md:187` — the next new mechanic is second-source at
  L018), so this gate reports `SKIPPED(no declared newMechanic)` for **every level in the alternation
  band** and never judges the L006 anchor's provably dead `queue` capacity. **The cost, recorded
  rather than hidden:** the band gets zero coverage from the very gate written for the defect class it
  exhibits. The only thing covering it is **CM-C11 criterion 6** (in-band liveness assertion, with the
  authored L006 anchor as its *negative control* — max queue depth 0).
- **Widening re-opens the anchor.** If **HC-14** later widens this gate to every entry of
  `meta.mechanics`, then L006 — which declares `mechanics ["switch","queue"]` with a queue that never
  holds a cat — **fails the gate**, and **HC-10 option A (anchor fidelity) and this gate's blocking
  posture cannot both hold**. Widening therefore forces one of: HC-10 **(B)** re-author (free only
  under NEW-Q1 branch Q1-A), HC-10 **(C)** minimum delta, a recorded per-level exemption, or this
  gate dropping to `Warn` for the carried-mechanic limb.
- **Neither contract may resolve this alone.** HC-10 and HC-14 are a **single human call**; each
  contract's stop conditions route to it (DM stop condition 1 / CM-C11 stop condition 9). An
  implementer who "fixes" one side unilaterally has failed review.

### Revision log vs `t3-dead-mechanic-gate-draft-contract.md`

1. Title/id/branch/handoff filenames: `CM-C5-DEADMECH` → **`CM-C5.1`** (R4); A-DM-8's provisional-id clause discharged.
2. Ratifications block added (above). H4's ownership half closed by R3.
3. Wave-1 placement + the F-K rationale ("land early, before the band is authored") recorded in the header.
4. Criteria 7 and 9: literal `10/10` → **N unchanged** (C6 rule); no other change to either criterion.
5. Joint C5 note added (identical text in CM-C11).
6. Open-questions table replaced by a **RIDES-WITH-PR** table (H1, H2/HC-14 joint, H3/HC-15, H6/HC-16, H7/HC-17, H8/HC-18, merge delegation).
7. **No criterion was weakened, renumbered or removed.** 9 criteria in, 9 criteria out.

---

## Goal

`bash scripts/validate-content.sh` fails, with a named blocking line, when a campaign level declares
`meta.newMechanic` and the **solver-optimal trace never exercises that mechanic** — proved by a
negative fixture that fires the gate and a positive twin that does not — while the shipped corpus
stays green, the per-level stage inventory stays exactly 11, and the daily pipeline is untouched.

## Spec reference (every line read at draft time)

- **The debt row:** `state/PROJECT_STATE.md:58` (F5 wording, verbatim above).
- **The rule it enforces:** CM-R06.2 one-new-mechanic ordering, already implemented as a *declaration*
  check at `unity/Assets/Scripts/Content/Validation/CorpusValidator.cs:279-299` — it asserts the
  declared `newMechanic` is in the level's own `mechanics` list and that no undeclared mechanic
  appears. **Nothing asserts the declared mechanic does anything.** `docs/plan/data/level_schema.json:21`
  types `newMechanic` as `["string","null"]` with the description "validator enforces".
- **The mechanic vocabulary (8 values, frozen):** `docs/plan/data/level_schema.json:20`.
- **The witness:** the solver-optimal log — `unity/Assets/Scripts/Domain/Solver/LevelSolver.cs:190-204`
  (the total order: fewest completion ticks → fewest commands → lexicographic `(Tick, SwitchId)`),
  surfaced as `SolveResult.OptimalLog` (`unity/Assets/Scripts/Domain/Solver/SolveResult.cs:52`).
- **The replay seam (no second scheduler):** `ReplayHasher.RunToEnd(graph, seed, log,
  Action<SimulationState> afterEachTick)` — `unity/Assets/Scripts/Domain/ReplayHasher.cs:30-35`; the
  per-tick callback fires after each `Simulation.Step` (`ReplayHasher.cs:42-43`).
- **Queue semantics (what "the queue was used" means):** enqueue on emission when the queue is
  non-empty or the mouth is occupied (`unity/Assets/Scripts/Domain/Simulation.cs:73-76`); enqueue on
  arrival when the route mouth is busy (`Simulation.cs:123-127`); head release at step 4a
  (`Simulation.cs:89-100`); observable state = `SimulationState.NodeQueueCounts` (read the same way
  the solver reads it, `LevelSolver.cs:335`). Zero-dwell pass-through — an arrival whose mouth is
  free never touches the queue — is golden-defining (`Simulation.cs:17-24`).
- **Switch semantics:** route index advances on command (`Simulation.cs:46-47`, `state.SwitchesUsed++`).
- **Where the verdict must live:** `CorpusReport.CampaignVerdicts`
  (`CorpusValidator.cs:65`, rendered at `:142-150` JSON / `:171-172` table / printed by the host at
  `dotnet/CatMetro.Validator/Program.cs:121-122` as `BLOCKING: campaign — <detail>`). It may **not**
  be a 12th per-level row: the stage enum is frozen at 11
  (`unity/Assets/Scripts/Content/Validation/Stage.cs:5-18`, pinned by
  `unity/Assets/Tests/EditMode/Pure/Validation/StageModelTests.cs:8-21`) and the per-level row count
  is asserted at `unity/Assets/Tests/EditMode/Pure/Validation/CorpusAndReportTests.cs:185` and
  `unity/Assets/Tests/EditMode/Pure/Daily/DailyPipelineTests.cs:123`.
- **Campaign classification is path-derived** (review F4): `Program.cs:147-152` —
  `StartsWith("content/levels/")` **or** `Contains("/content/levels/")`. This is what lets the
  negative fixture be exercised through the real CLI from a temp tree (criterion 7).
- **Unobservable mechanics:** the importer drops `cooldownTicks`, `express`, `reversible`, `oneWay`
  and the whole `gates` block — no DTO member exists for any of them
  (`unity/Assets/Scripts/Content/LevelDtos.cs:80-90` EdgeDto, `:118-124` SwitchDto, `:132-140`
  WaveDto, `:9-24` LevelDto). `second-source` / `wildcard` fail import first
  (`unity/Assets/Scripts/Content/ContentResult.cs:22` `PinnedMechanic`;
  `unity/Assets/Scripts/Domain/LevelGraph.cs:64,68-69`).
- **House verdict vocabulary:** `Stage.cs:23-63` (`Pass/Fail/Warn/Unconfigured/Skipped/Pinned/…`,
  `Blocks` is the only exit-code channel).
- **Fixture home:** `tests/validation/fixtures/**` (CM-C5-owned, inherited by this contract under R3;
  `tests/fixtures/content-bad/**` is CM-C2a's — `state/backlog.md:1161-1162`).
- **Wrapper shape and its anti-tautology lessons:** `tests/validation/validator.test.sh:36-45`
  (review F3: grep the exact `BLOCKING: …` line, never a token that appears in every run; review F4:
  assert *no* campaign noise when the fixture is non-campaign).

## Acceptance criteria (9)

Each is met only when the named check exits 0 (or fails exactly as specified). Every check can fail:
each one has a stated mutation that must turn it red.

1. **The observer measures the optimal trace through the one existing replay seam — no second
   scheduler, no second `Step`.** A new `CatMetro.Content.Validation.MechanicExercise` replays
   `solve.OptimalLog` via `ReplayHasher.RunToEnd(graph, seed, log, afterEachTick)`
   (`ReplayHasher.cs:30`) and records, per sampled tick: `max Σ NodeQueueCounts` and the tick it
   occurred at; whether any `SwitchRoutes[s]` differs from its authored initial index; and
   `SwitchesUsed`. *Checks:* (a) NUnit — for the authored L004 board (`content/levels/L004.json`, two
   waves at tick 8) the observer reports `queue exercised, maxQueued=1 @ tick 8` (hand-derived from
   `Simulation.cs:73-76` + `:89-100`: the second same-tick emission enqueues because the mouth
   carries a `ProgressTicks == 0` train, and step 4a cannot release it that tick);
   (b) NUnit — two runs of the observer on the same level produce an equal record (determinism);
   (c) a wrapper grep asserting zero `Simulation\.Step` matches under
   `unity/Assets/Scripts/Content/**` (true today — Content reaches the sim only through
   `LevelSolver`/`ReplayHasher`, `ValidationStages.cs:406,460,524`, `CorpusValidator.cs:223`).
   *Mutation that must fail it:* re-implement the due-command selection locally → (c) red; sample
   before the step instead of after → (a) red.
2. **Every one of the 8 schema mechanics has a declared, tested disposition — and an unobservable
   mechanic is named, never silently passed.** A frozen table maps
   `docs/plan/data/level_schema.json:20`'s enum to: `switch` OBSERVABLE, `queue` OBSERVABLE,
   `second-source`/`wildcard` UNREACHABLE (import fails first — `ContentResult.cs:22`,
   `LevelGraph.cs:64,68-69`), `cooldown`/`gate`/`express`/`reversible` UNOBSERVABLE (no DTO member
   exists — `LevelDtos.cs:80-90,118-124,132-140`). An UNOBSERVABLE or UNREACHABLE declared mechanic
   yields `StageVerdictCode.Pinned` with detail `PINNED(<mechanic> unobservable — no DTO field)`,
   `Blocks == false`, **never `Pass`**. *Checks:* one NUnit case per mechanic (8) asserting the
   disposition; one asserting a level with `newMechanic: "gate"` produces a Pinned, non-blocking,
   non-Pass verdict; one asserting the table's key set equals the schema enum read from
   `docs/plan/data/level_schema.json` **bytes** (so a schema edit fails the test rather than drifting).
   *Mutation:* add a 9th enum value to a local copy → the set-equality case goes red.
3. **The gate fires: a declared `newMechanic` unexercised by the optimal trace FAILS and blocks.**
   `tests/validation/fixtures/dead-mechanic/L004-dead-queue.json` — L004's board with the second
   tick-8 wave removed (so no two cats ever contend for the mouth) and `SRC.queueCapacity` still
   authored, still declaring `mechanics ["switch","queue"]`, `newMechanic "queue"` — produces a
   campaign verdict `Fail`, `Blocks == true`, `CorpusReport.ExitFailure == true`, with a detail naming
   **the level id and the mechanic**. *Checks:* one NUnit case on the failing fixture; one on the
   positive twin `…/L004-live-queue.json` (the same board with the second wave restored) asserting
   `Pass` and `ExitFailure == false`; one asserting the failing corpus reports **exactly one**
   blocking verdict (so the fixture is not failing for a band/order/count reason —
   `validator.test.sh:36-45` review-F3 discipline).
4. **The stage inventory stays exactly 11 and the verdict rides `CampaignVerdicts`.** *Checks:*
   `StageModelTests` (`:8-21`) and both 11-row assertions (`CorpusAndReportTests.cs:185`,
   `DailyPipelineTests.cs:123`) stay green **unmodified**; plus a new case asserting
   `report.Levels.All(l => l.Verdicts.Count == 11)` and that the dead-mechanic verdict is found in
   `report.CampaignVerdicts` and in the JSON at `root["campaign"]` (`CorpusValidator.cs:142-150`).
   *Mutation:* emit the verdict as a per-level row → three existing tests go red, which is the point.
5. **Non-campaign members and the daily pipeline are untouched.** Stress boards and daily candidates
   are non-campaign (`CorpusValidator.cs:232-244`; `DailyPipeline.cs:237-243` passes
   `isCampaign: false`), so the gate never judges them. With zero campaign members the verdict is
   `SKIPPED(no campaign members)`, `Blocks == false`. *Checks:* one NUnit case on a stress-board-only
   corpus asserting the Skipped, non-blocking verdict and `ExitFailure` unchanged; one asserting a
   single-member daily-shaped run's `ExitFailure` is identical with the gate present; the whole
   `Pure/Daily/**` suite stays green unmodified.
6. **The two existing campaign-verdict selectors are made *precise*, as a first-class deliverable —
   and they can still fail.** `CorpusAndReportTests.cs:146` selects with
   `.Single(v => v.Detail.Contains("corpus"))` and `:170` with `.Single(v => v.Detail.Contains(
   "mechanic"))`; a fourth campaign verdict that says "mechanic" makes `Single()` **throw**, so this
   contract cannot land without touching them. The amendment is *selector precision only*: every
   campaign verdict gains a stable machine tag (e.g. `Value` prefixed `tag=CM-R06.2` / `CM-R09.1` /
   `CM-R09.3` / `CM-R06.2-liveness`) and both selectors match the tag instead of prose. **No
   assertion is weakened or deleted** (AGENTS.md hard rule 5). *Checks:* (a) both amended tests still
   go **red** under the reviewer's mutation — flip the mechanic-order limb to non-blocking
   (`CorpusValidator.cs:296`) and `MechanicOrder_ViolationBlocks` must fail; drop the count row and
   `CampaignAssertions_ComputeOverCampaignLevelsOnly` must fail; (b) the before/after runs of both
   tests pasted in the PR; (c) a new case asserting each campaign verdict carries a unique tag.
7. **End-to-end through the real entry point, with a positive control (roadmap-D9 shape).** An
   appended, labelled block in `tests/validation/validator.test.sh` (the existing wrapper — **no new
   wrapper, so `bash scripts/test.sh` stays at N**, the rebase-baseline count captured in the handoff
   note; C6 rule above) that: copies the **real** `content/levels/L001.json` (the switch-teaching
   prior, so the mechanic-order limb is satisfied) plus one fixture into `"$tmp/content/levels/"` —
   campaign classification is path-derived, `Program.cs:147-152` — and runs
   `bash scripts/validate-content.sh --corpus "$tmp/content/levels"`. *Checks:* (a) with
   `L004-dead-queue.json`: exit non-zero **and** `grep -q "BLOCKING: campaign — .*L004.*queue"`
   **and** exactly one `^BLOCKING:` line in the output; (b) with `L004-live-queue.json`: exit 0 —
   the control that proves (a) is the liveness gate and not the band/order/count limbs;
   (c) the shipped-corpus run (existing 15a) still exits 0 and the byte/SHA and `git diff` belts
   (`validator.test.sh:14-26`) still pass; (d) no `--stamp` anywhere in the new block.
   *Mutation:* make the gate non-blocking → (a) red; make it fire unconditionally → (b) red.
8. **The measurement always prints, blocking or not.** Every campaign level's liveness row carries
   its evidence in the verdict `Value`: `newMechanic=<m|null>; exercised=<true|false>;
   evidence=<maxQueued=N@tick T | toggles=N,routeChangedAtTick=T | none>`, rendered in both output
   forms (`CorpusValidator.cs:142-150` JSON, `:171-172` table). A level with `newMechanic: null`
   (L002/L003/L005 — `content/levels/L005.json:13` — **and, under the joint note above, all five of
   L006–L010**) reports `SKIPPED(no declared newMechanic)`, non-blocking. A level whose stage 4 is not
   `Solved` reports `SKIPPED(no winning log)`, the same shape brittleness already uses
   (`state/handoffs/CM-C5.md:87-88`). *Checks:* NUnit asserting the `Value` string for L001
   (`switch`, hand-derived: `initialRoute 1` routes to BLU which accepts only blue, so a red cat
   cannot be delivered without a toggle) and for L004 (`queue`, `maxQueued=1@tick 8`); one asserting
   the null case is Skipped; one asserting a `NotFound` level is Skipped, not Fail.
9. **Gates green, nothing else moved.** `bash scripts/check.sh` OK; `bash scripts/test.sh` green at
   **N** (the rebase-baseline wrapper count — this contract adds no wrapper; C6 rule) with
   `PASS tests/validation/validator.test.sh` in the output; the full EditMode + `dotnet test` suite
   green with the new cases counted (paste before/after totals); `git diff --name-only
   origin/main...HEAD` lists **only** the paths in the §Scope-boundary table; the CM-C1 golden hash
   unchanged. *Check:* the four runs pasted in the PR, plus the `git diff --name-only` output verbatim.

## Scope boundary

**In scope — the complete file table (nothing else may appear in the diff):**

| Path | Mode | Why |
|---|---|---|
| `unity/Assets/Scripts/Content/Validation/MechanicExercise.cs` (+ `.meta`) | NEW | the observer + the 8-mechanic disposition table (criteria 1, 2, 8) |
| `unity/Assets/Scripts/Content/Validation/CorpusValidator.cs` | EDIT | pass campaign `(dto, graph, solve)` into `CampaignAssertions` (`:256`, `:275`) and add the 4th verdict + the tags of criterion 6 |
| `unity/Assets/Tests/EditMode/Pure/Validation/MechanicExerciseTests.cs` (+ `.meta`) | NEW | criteria 1–5, 8 |
| `unity/Assets/Tests/EditMode/Pure/Validation/CorpusAndReportTests.cs` | EDIT | **two selector lines only** (`:146`, `:170`) — criterion 6; no assertion removed |
| `tests/validation/fixtures/dead-mechanic/L004-dead-queue.json` | NEW | the negative fixture (criterion 3) |
| `tests/validation/fixtures/dead-mechanic/L004-live-queue.json` | NEW | the positive control (criteria 3, 7b) |
| `tests/validation/validator.test.sh` | EDIT | one appended labelled block (criterion 7); never restructure the file |
| `state/handoffs/CM-C5.1-deadmech.md`, `state/handoffs/CM-C5.1-frozen-contract.md` | NEW | the session handoff/evidence record (house style) |
| `state/PROJECT_STATE.md` | EDIT **on merge only** | one appended line + strike the `:58` debt row |

All of the above sit inside CM-C5's ownership rows (`state/backlog.md:120`) plus the new
`tests/validation/fixtures/dead-mechanic/**` leaf, acked in `t3-backlog-amendment.md` EDIT-5 (R3).

**Explicit non-goals / must-not-touch:**
- **No `content/levels/**`.** L001 is byte-frozen (`state/handoffs/L002-L005.md:37`) and L006–L010
  belong to the parallel CM-C11 authoring lane. If a shipped level fails the gate → stop condition 1.
- **No `Domain/**` (including `Domain/Solver/**`)** — golden-adjacent and CM-C1/CM-C4-owned
  (`state/backlog.md:115,119`). The observer needs no Domain change: `ReplayHasher.cs:30` is already
  public and already takes a per-tick callback.
- **No 12th `Stage` member, no 12th per-level verdict row** (criterion 4).
- **No new dependency.** The observer is `netstandard2.1` + the already-referenced Domain; the
  fixtures are plain JSON. *If any dependency seems needed → stop; it would need its own ADR named
  in the PR description (AGENTS.md hard rule 2), and none is proposed here.*
- **No `scripts/check.sh` append** — the one-`Step`-symbol belt of criterion 1c lives in the wrapper
  this contract owns, exactly as CM-C5's file-API belt does (`validator.test.sh:52-55`).
- **No `dotnet/CatMetro.Validator/Program.cs` change** — the temp-tree route works because campaign
  status is a path substring (`Program.cs:147-152`). If review demands an explicit
  `--corpus-campaign` flag instead, that is a scope change → stop and re-cut.
- **No `config/validator_thresholds.json` row.** The gate is a boolean, not a threshold; inventing a
  number would be the Q-R failure mode (`state/backlog.md:59`).
- **No `unity/Assets/StreamingAssets/**` write and no staging.** CM-C10 is the single staging author
  (R1); this contract's fixtures are deliberately outside the two staging rules.
- **No `unity/Assets/Scripts/Presentation/**`, no `Bootstrap/**`, no
  `unity/Assets/Resources/Strings/ui.csv`, no `tests/unity/**`** — the UX lane and the in-flight
  CM-C3-DEVCAP / CM-C2b-DEVFIX branches own those (`state/handoffs/SESSION-HANDOFF-ux.md:31-39`).
  This contract adds **zero** `ui.csv` rows (and would append only, never edit, if it ever did).
- **No `state/backlog.md` edit** — the ownership ack is the human's to apply from
  `t3-backlog-amendment.md`.
- **No `.github/**`, no `infra/**`, no `**/billing/**`, `**/iap/**`, `**/ads/**`.**
- **No immutable path:** `tests/contract/`, `docs/constitution.md`, `.claude/hooks/`,
  `scripts/git-hooks/`, `state/mode`, `evals/` except `evals/results/` (and never
  `evals/results/attested/`).
- **No edits inside `.claude/worktrees/ux-lane/`** — that is another session's worktree; it mirrors
  these files and will appear in repo-wide greps. Scope every grep to the real tree.

## Assumptions (all falsifiable; each is a human overrule point)

- **A-DM-1 — "exercised" means *observable in a rendered tick state*.** The observer samples **after**
  each `Step` (`ReplayHasher.cs:42-43`), so a queue that fills and drains *inside* one step is
  invisible. This is a real case, not a theoretical one: a cat enqueued at step 2
  (`Simulation.cs:73-76`) behind a train that entered on the **previous** tick is released at step 4a
  of the same step, because step 3 (`Simulation.cs:84`) advances that train past `ProgressTicks == 0`
  and frees the mouth (`Simulation.cs:173-179`, `:95-99`) → post-step count 0. The assumption is that
  such a queue is **dead for teaching purposes** — the player never sees a line form — which is the
  whole point of the F5 debt row. **The alternative (instrumenting enqueue events in `Simulation`) is
  a Domain change and is stop condition 2.** RIDES-WITH-PR row **H5/HC-15** asks the human to ratify
  this meaning.
- **A-DM-2 — the witness is the solver-optimal log only.** `LevelSolver.cs:190-204` defines one
  canonical winning log; the gate judges that trace. A level that exercises the mechanic on typical
  play but not on the optimal line reads as dead (false alarm), and a level that exercises it only on
  the optimal line reads as alive (weak pass). Checking *every* tied-optimal or every winning log is
  combinatorial and is not taken. Printed evidence (criterion 8) is what lets a human overrule.
- **A-DM-3 — `switch` liveness is observational, and stage 5 is its causal backstop.** Exercised iff
  some sampled tick shows a route index different from the authored `initialRoute` **and**
  `SwitchesUsed >= 1`. A board that wins with no toggle is already caught by stage 5's zero-input
  rule (`state/backlog.md:1041-1044`), so this limb is a cheap complement, not a duplicate.
- **A-DM-4 — the verdict rides `CampaignVerdicts` labelled `Stage.NoveltyCheck`,** because that is
  the existing house pattern for campaign-level rows (`CorpusValidator.cs:296,304,311,338`) and the
  `Stage` enum is frozen. The label is a known wart, inherited deliberately rather than fixed here
  (H6/HC-16 asks whether to fix it with tags — criterion 6 introduces them anyway).
- **A-DM-5 — the gate ships BLOCKING,** as the sibling of the CM-R06.2 declaration limb, which is
  blocking because "violations are authoring defects now" (`CorpusAndReportTests.cs:172`).
  **H1 can downgrade it to `Warn` for one tranche**; if so, criteria 3/7a assert `Warn` +
  `Blocks == false` and the wrapper asserts the *line prints* while exit stays 0 — same tests, one
  flag. R4's Wave-1-before-CM-C11 sequencing means the band is authored against whatever posture
  ships, rather than inheriting a gate mid-flight.
- **A-DM-6 — the shipped corpus passes.** Reasoned, **not measured** (no build was run while drafting):
  L001's `initialRoute: 1` points at a blue-only station while its cats are red
  (`content/levels/L001.json` via `docs/plan/data/example_levels.json:4-17`), so the optimal log must
  toggle; L004's two same-tick waves force an enqueue at tick 8. **L002/L003/L005 declare
  `newMechanic: null` and are vacuously skipped** (`content/levels/L005.json:13`). First red-bar run
  must confirm this; if it does not, stop condition 1 fires. **Under R4 this run happens in Wave 1,
  before the CM-C11 band is authored** — that is the point of the ordering.
- **A-DM-7 — new `unity/Assets/**` files ship a committed `.meta`** with a fresh editor-generated
  GUID, same shape as their siblings (e.g.
  `unity/Assets/Scripts/Content/Validation/Stage.cs.meta`), or the Unity leg breaks on next open.
  These are **not** CM-C10's derived staging guids (R1 scope is the staged tree only).
- **A-DM-8 — ownership inheritance rides the delegated batch.** Id fixed to `CM-C5.1` (R4);
  inheritance of CM-C5's ownership rows is acked in `t3-backlog-amendment.md` EDIT-5 (R3). No
  criterion's meaning depends on either.

## Stop conditions

Defaults apply (AGENTS.md hard rules 1–7). Plus:
1. **A shipped campaign level (L001–L005) fails the new gate → STOP and report.** The remedy would be
   content, and this contract owns no `content/levels/**` path; L001 is byte-frozen and L006–L010 are
   CM-C11's. Never soften the rule to make the corpus green (that is the CM-C5 F1 lesson).
2. **The gate seems to need a `Domain/**` or `Domain/Solver/**` change** (enqueue-event
   instrumentation, a new `SolveResult`/`DifficultyProxy` member) → STOP. `SolveResult.cs:3-5`
   declares the record's members frozen and `SolverResultTests.cs:151-161` pins the member surface;
   `Domain/**` edits are golden-adjacent (`state/backlog.md:157-159`).
3. **Any temptation to add a 12th `Stage` member or a 12th per-level verdict row** → STOP
   (`Stage.cs:5-18`, `StageModelTests.cs:8-21`, `CorpusAndReportTests.cs:185`,
   `DailyPipelineTests.cs:123`).
4. **Any test change beyond the two selector lines of criterion 6** — and in particular any change
   that makes an existing assertion weaker — → STOP (AGENTS.md hard rule 5). The mutation proof of
   criterion 6a is the evidence that the selectors still bite.
5. **A new dependency looks necessary** → STOP; name the ADR it would need, do not add it.
6. **Any need to touch `content/levels/**`, `StreamingAssets/**`, `Presentation/**`, `Bootstrap/**`,
   `ui.csv`, `tests/unity/**`, `scripts/check.sh`, `dotnet/CatMetro.Validator/**`, `state/backlog.md`,
   `.github/**` or an immutable path** → STOP; those belong to other lanes or other humans.
7. **A mechanic outside `level_schema.json:20`'s 8 values appears, or the schema needs a field** →
   STOP (schema frozen; Q-F, `state/backlog.md:47`).
8. **A threshold number is tempting** (e.g. "the queue must hold ≥2 cats for ≥3 ticks") → STOP and
   ask; ship the boolean plus the printed measurement (Q-R shape, `state/backlog.md:59`).
9. **The prose landmine fires:** `scripts/check.sh:69,88` scans **comments too** for
   `UnityEngine|System\.IO` under `unity/Assets/Scripts/Content/**`. Never name a storage-path or
   file API in any comment, HEREDOC or fixture under that tree — sweep before committing.
10. **Any urge to widen the gate to every entry of `meta.mechanics`** (H2/HC-14) → STOP: under the
    joint note above that decision **re-opens CM-C11's L006 anchor** and is a single human call
    spanning two contracts.

---

### RIDES-WITH-PR human calls (default recorded; ratify at review/merge)

| # | Call | Default this contract ships | Coupling |
|---|---|---|---|
| **H1** | **Does the gate land BLOCKING, or as a non-blocking `Warn` for one tranche?** It judges every future campaign level, including CM-C11's band. | **BLOCKING** (A-DM-5) — sibling of the CM-R06.2 limb. One flag flips criteria 3/7a to `Warn` + `Blocks == false` with the line still printing. R4's Wave-1 ordering means CM-C11 authors against a known posture. | Was bundled into HC-3; **only the sequencing half was ratified 2026-08-05**, so the posture itself is still open. |
| **H2 / HC-14** | **Scope: `meta.newMechanic` only, or every entry of `meta.mechanics`?** | **`newMechanic` only** (default stands at freeze); the full-`mechanics` audit may print non-blocking. | **JOINT with HC-10** — see the joint note; widening fails CM-C11's L006 anchor. Answer both or neither. |
| **H3 / HC-15** | **Is the solver-optimal trace the right witness, and is "exercised" = visible in a post-step tick state?** (A-DM-1 + A-DM-2; the within-step enqueue+release hole, notes F-D.) | Solver-optimal log only; post-step visibility; evidence printed per level (criterion 8) so a human can overrule per board. | Instrumenting `Simulation` is a Domain change → stop condition 2. |
| **H6 / HC-16** | **May campaign verdicts gain stable machine tags** (`tag=CM-R06.2` …) in the JSON report? That is a **report-shape change**, and the report feeds the daily artifact path. | **Tags added** — criterion 6 needs a non-prose selector. Flagged in the PR. | Report shape is external-ish; worth the human's eyes. |
| **H7 / HC-17** | **Should the liveness row appear in the daily pipeline's per-date artifact?** | **No** — dailies are non-campaign by construction (`DailyPipeline.cs:237-243`); criterion 5 asserts they are untouched. | — |
| **H8 / HC-18** | **If L001–L005 fail the gate on first run (F-K), who fixes the content and when?** This contract owns no `content/levels/**` path and L001 is byte-frozen. | **Stop and report** (stop condition 1); no content edit. | R4's Wave-1 ordering makes this surface early, before CM-C11 authors. |
| **HC-25** | **Merge-delegation re-confirmation for this lane this session** (`state/handoffs/SESSION-HANDOFF-device-testing.md:9-10`; Constitution Amendment 1). | Assume **not** delegated until the human re-confirms in-session. | Blocks **merge**, not work. |

**Closed since the draft:** H4 (id + ownership inheritance) by **R4** + **R3**.
