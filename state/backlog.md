# Cat Metro — contract queue (backlog)

**Written:** 2026-08-02 · **Refilled (tranche 2):** 2026-08-03 · **Author:** product-analyst (agent),
forge-decompose · **Status:** DRAFT for human ordering/approval · **Reviewed by:** human product owner
(judgment) + evaluator (testability lint).

## What this file is

The human-ordered queue of **agent task contracts**. Each contract uses the field set of
`.github/ISSUE_TEMPLATE/agent-task.yml` (Goal · Spec reference · Acceptance criteria · Scope boundary ·
Assumptions · Stop conditions) and is sized for **one branch / one PR** (AGENTS.md hard rule 4).
A contract is done when every acceptance criterion maps to a passing check and
`docs/constitution.md:14-20` (Definition of Done) is satisfied.

Tranche 1 decomposed roadmap rows D2–D4. **Tranche 2 (this refill)** adds roadmap rows **D6, D8, D9,
D12, D13** (`docs/plan/data/roadmap_56_days.csv:7,10,11,14,15`) and splits CM-C2. Citations are to the
amended `docs/plan/` corpus **as it stands on disk on 2026-08-03** — I read every cited file this
session; where a line is load-bearing the file:line is given. `docs/prd/PRD.md:943` (A-03) records the
pre-amendment caveat; it does not apply to citations in this file.

| Contract | Roadmap row | PRD requirements | ADRs | Status |
|---|---|---|---|---|
| **CM-C1** | D2 (`roadmap_56_days.csv:3`) | CM-R01, CM-R02 (pin-independent subset), CM-R03.1, CM-R07.3 | 0002, 0003, 0004, 0005 | **MERGED** |
| **CM-C2a** | D3 content+importer (`:4`) | CM-R13.2, CM-R12 (schema/bounds subset), CM-R02.1 (mapping side) | 0003, 0005, 0006 (§4 bounds), 0008 | queued, **UNBLOCKED** |
| **CM-C2b** | D3 engineering (`:4`) | CM-R07.1/.3, CM-R17 (partial), CM-R20.1, CM-R51.1, CM-R52 | 0003, 0005, 0007, 0008 | **READY — Q-G resolved (#19, recut 2026-08-04)** |
| **CM-C3** | D4 (`:5`) | CM-R03.2, CM-R15 (D4 subset), CM-R16, CM-R17.1, CM-R22.3 | 0002, 0007 | queued |
| **CM-C4** | D8 + D9 solver (`:10,11`) | CM-R02.1, CM-R12.2 (input), CM-R19.1 (input), CM-R04.2 (input) | 0002, 0003, 0005, 0008 | queued, **UNBLOCKED**, HYBRID-ELIGIBLE |
| **CM-C5** | D9 (`:11`) | CM-R12.1–.6, CM-R06.1/.2, CM-R07.6, CM-R09.2, CM-R04.2 | 0002, 0005, 0008, 0009 | queued |
| **CM-C6** | D12 (`:14`) | CM-R46.1–.3/.5 (pure-C# subset), CM-R11.1/.7 | 0002, 0005, 0008, 0009 | queued |
| **CM-C7** | D6 (`:7`) | CM-R05.1/.3/.5, CM-R27.3–.5 (ledger data subset) | 0003, 0005, 0006 | queued, **DEPENDS-ON CM-C2a** |
| **CM-C8** | D13 (`:15`) | CM-R43.4(a)–(d) | 0003, 0005, 0006 §5 | queued |

---

## QUESTIONS A HUMAN MUST ANSWER (this queue is executable without them, but they bound it)

Nothing below is answered by an agent. These are the ones that touch **these contracts**; the full list
is `docs/prd/PRD.md` §0 and §5 (57 open items). **D-decisions and §4.1 pins are never resolved here.**

| # | Question | Which contract it touches | Effect if unanswered |
|---|---|---|---|
| **Q-A** | **NEW-Q35** — wildcard resolution boundary. It changes **the command-log format**, not just rules (`docs/adr/0002-deterministic-fixed-tick-domain.md:160-162`). | CM-C1 (shipped), CM-C4 | CM-C1 shipped a versioned envelope (`CommandLog.CurrentFormatVersion = 1`, `unity/Assets/Scripts/Domain/Commands.cs:25`) and **no wildcard behaviour** — `LevelGraph` throws on the `wild` colour (`LevelGraph.cs:68`). CM-C4 inherits the throw (see Q-N). |
| **Q-B** | **NEW-Q4** — reversing rejected cat meets an oncoming cat on a one-way edge (`docs/prd/PRD.md:114-118`). | CM-C1 (shipped), CM-C4, CM-C5 | Station rejection is out of the shipped Domain: a non-matching arrival throws `NotSupportedException` (`Simulation.cs:116-117`). Every search and every validator stage inherits that throw — see **Q-N**. |
| **Q-C** | **NEW-Q5** — chain saturation + `PERFECT_BONUS_TICKETS` / `PERFECT_MAX_SWITCHES` (`docs/prd/PRD.md:150`). | CM-C4 (excluded) | Scoring stays out. `Score`/`Chain` exist in the digest and stay 0 (`SimulationState.cs:31-32`). **CM-C4's search optimises deliveries/time only** (criterion 8). |
| **Q-D** | Does CM-C1 own all three `FailReason` members, or only `queue_overflow`? | settled by shipped code | `Outcomes.cs:7-12` ships all three; only `QueueOverflow`/`TimeOut` are raisable (`Outcomes.cs:40-42`). Unchanged. |
| **Q-E** | Roadmap D3 says "Win by routing 10 cats"; authored L001 has `win.deliveries: 2` (`docs/plan/data/example_levels.json:15`). | CM-C2a, CM-C2b | CM-C2a asserts **the level file's own** `win.deliveries`. If the human wants 10, L001 is re-authored — content change, not code. |
| **Q-F** | **A-19 / ADR-0008 §Open conflict** — no `district` field in schema v2 (`docs/adr/0008-content-pipeline-and-level-schema.md:131-149`). | CM-C2a (DTO shape), CM-C5 | CM-C2a imports schema v2 **exactly as it stands**; adding `meta.district` is a schema change → stop condition, not an agent edit. |
| **Q-G** | **Unity project scaffold** (6000.3.16f1, IL2CPP/ARM64/URP/Input System, minSdk 25 / targetSdk 36, package `com.catmetro.game`, keystore) is human-only (`docs/adr/0005-dotnet-first-dual-test-harness.md:16-19`). Must be created **in place at `unity/`** without deleting `unity/Assets/Scripts/Domain/**`. | CM-C2b (was BLOCKED-ON), CM-C8 (M-21 backup limb) | **RESOLVED 2026-08-04: scaffold merged as #19 (`e586712`), every pin verified; keystore/Play App Signing remain open human items (release-era).** CM-C2b is READY. |
| **Q-H** | **TG-5** — failure-screen copy voice (`docs/prd/ux-flows.md:188` vs `docs/plan/specs/monetization_spec.md:173`). | CM-C3 | CM-C3 renders the LOCKED set; a TG-5 flip is a string-table edit. |
| **Q-I** | **Golden custody** — `tests/contract/replay-hash-golden.json` human-committed. | CM-C1 (**discharged**) | Discharged: golden human-committed, CI green, merged (`state/PROJECT_STATE.md:15`). The rule stands for every future golden. |
| **Q-J** | **BLOCKING — what raises `platform_overflow` while NEW-Q4 pins rejection out?** (`docs/plan/specs/product_spec.md:225`; CM-R02.3 at `docs/prd/PRD.md:113`). | CM-C2b, CM-C3, CM-C4, CM-C5 | `PlatformOverflow` stays an enum member and is never raised (`Outcomes.cs:40-42`). Roadmap D3's "fail by platform overflow" (`roadmap_56_days.csv:4`) is **recorded as deferred, not met**. |
| **Q-K** | **`TimeOut` camera target** — analyst-authored rule (largest queue at fail tick, ties → lowest node id), unratified. | CM-C3 | CM-C3 ships the authored rule; an overrule costs presentation only. |
| **Q-L** | **Template defect** — `.github/ISSUE_TEMPLATE/agent-task.yml:19-20` declares **Stop conditions** as single-line `type: input` with no `validations: required`. Hook/CODEOWNERS-gated path; an agent may not edit it. | all contracts | Contracts carry their stop conditions in this file, so execution is unblocked. The template needs `type: textarea` + `validations: {required: true}`. |
| **Q-M** | **SOLVER PLACEMENT — ADR/architecture conflict, analyst decided under protest.** `docs/architecture/overview.md:56` and `docs/adr/0003-assembly-isolation-and-dependency-rule.md:44` put "solver runner · batch validator" in **`CatMetro.Editor`** (an *editor* assembly), and roadmap D8 says "Editor solver runner" (`roadmap_56_days.csv:10`). But `docs/adr/0005-dotnet-first-dual-test-harness.md:112` puts **solver + level/bounds validation in the licence-free `dotnet` leg**, and `docs/adr/0009-ci-topology-and-secret-custody.md:35` gives `validate-content` **zero credentials**. An Editor-assembly solver cannot run in either. **Decision taken (per the commissioning instruction to place per ADR-0005's engine-free requirement): the search lives at `unity/Assets/Scripts/Domain/Solver/**`**, inside `CatMetro.Domain` — see the rationale block below the ownership table. **The architect must ratify one of:** (a) Domain/Solver as placed; (b) an ADR-0003 amendment adding a 14th engine-free `CatMetro.Solver` assembly (**assembly names are declared irreversible**, ADR-0003 §Locked in); (c) `CatMetro.Application`. Also affected: CM-R02.1's wording *"the **solver project** references any duplicate step implementation"* (`docs/prd/PRD.md:111`) presumes a separate project. | CM-C4, CM-C5, CM-C6 | CM-C4 ships at `Domain/Solver/**`; CM-R02.1's check is re-expressed as "exactly one `Simulation.Step` definition in the tree" (criterion 2), which is stronger, not weaker. A later move to (b)/(c) is a file move plus one csproj, **no behaviour change and no golden impact**. |
| **Q-N** | **BLOCKING for search — the shipped `Step` throws on pinned behaviour.** `Simulation.cs:116-117` throws `NotSupportedException` on a non-matching station arrival; `Outcomes.cs:40-42` throws on `Failed(PlatformOverflow)`; `LevelGraph.cs:64,68` throws on a second source and on `wild`. **Any search that explores a wrong route on a multi-colour board hits the rejection guard** — e.g. `docs/plan/data/stress_boards.json` L701 has 3 colours into 3 stations, so most branches throw. Options: **(i)** the solver catches the pin guard, prunes that branch as `PinnedUnreachable`, counts them, and returns `Indeterminate(pinned)` rather than `Unsolvable`; **(ii)** an ADR-0002 amendment adding a non-throwing legality probe (a Domain API change → re-opens the golden). | CM-C4, CM-C5, CM-C6 | **Analyst assumption A-C4-2: (i).** CM-C4 ships catch-prune-count; a board whose only losing branches are pinned reports `Indeterminate(pinned)` with the count, **never `Unsolvable`**, and CM-C5 stage 4 propagates `Indeterminate` as a non-blocking verdict with a printed count. If the human wants a blocking verdict, that decision needs NEW-Q4 first. |
| **Q-O** | **Who stamps `meta.validatedAt`, and does stage 10 block today?** ADR-0008:119-123 says tooling **deletes** the key when unvalidated and "an absent key is treated as stale". CM-R12.5 says a stale level **fails CI** (`docs/prd/PRD.md:272`). **Composed, those two make every level fail stage 10 on the day CM-C5 lands**, because nothing in the corpus says who writes the key, into which file, or whether the validator may rewrite its own inputs (a validator that stamps its inputs inside the gate run is self-certifying). Options: **(a)** read-only gate run + a separate opt-in `--stamp` invocation whose diff a human commits; **(b)** a sidecar `content/validation_report.json` holds timestamps and level files never carry the key (schema-compatible: `validatedAt` is optional, `level_schema.json:16,25`); **(c)** human hand-stamps. | CM-C5, CM-C6 | **Analyst default: (a) + stage 10 computes and prints its verdict but does not block** until a stamped corpus exists (CM-C5 criteria 11, 13, 17). The gate run **never writes** under `content/levels/**`. |
| **Q-P** | **Is `docs/plan/data/stress_boards.json` a validated corpus member?** Its own header says the boards "must pass it (static, lower-bound, solver, triviality, brittleness, novelty) plus human playtest" but are "NOT campaign content: never enter the L001-L030 progression" (`stress_boards.json:2`). Campaign-order stages (novelty vs prior levels, one-new-mechanic ordering, band table, the 30-level count of CM-R09.1) have no meaning for them. | CM-C5 | **Analyst assumption A-C5-3:** stress boards run **stages 1–8 and 10** — stage 8 (difficulty) is **included** because the boards carry an authored `difficultyTarget` (0.30 / 0.35, `stress_boards.json:6`) that is worth checking, though it reports `UNCONFIGURED` while axis B's band caps are a Q-R row. **Stage 9** (novelty-vs-prior-order) and the CM-R06.2/CM-R09.1/CM-R09.3 campaign-order assertions **skip** them, printing `SKIPPED(non-campaign)` per skipped stage rather than `PASS`. **Stage 11 (human playtest) emits a checklist row for them**, because `stress_boards.json:2` says the boards must pass the validator "**plus human playtest**"; the row is non-blocking like every other stage-11 row. This wording is the authoritative one — criterion 14 states the same set. |
| **Q-Q** | **Daily pre-validation horizon: 90 dates or 30?** CM-R46 and ADR-0009's `validate-dailies` say **the next 90 dates** (`docs/prd/PRD.md:730`; `docs/adr/0009-...:35`); ADR-0008:9-14 warns that the **30-board dated backup pool** is a *different quantity* and conflating them mis-scopes the pipeline. The commissioning brief for this tranche asked for a **30-day** run artifact. | CM-C6 | **The horizon is not unpinned — it is 90.** CM-R46's heading says "90 dates pre-validated in CI" (`docs/prd/PRD.md:727`) and ADR-0009:35 says `validate-dailies` runs "over the next 90 dates"; both are corpus numbers, not agent choices. CM-C6 declares `DAILY_PREVALIDATION_DAYS = 90` in `config/daily_pipeline.json` per the PRD constant convention (`docs/prd/PRD.md:88`), with the citation on the row; the **30-date run is the smoke instance** and the 90-date run is the criterion instance — the same shape ADR-0006:224-227 uses for `QUEUE_MAX_EVENTS`. What is still open is only the ADR-0008:9-14 warning that the **30-board dated backup pool is a different quantity**; CM-C6 never conflates them. `SALT_MAX_K` is the genuinely unpinned number and stays on A-C6-2's declare-with-derivation route. |
| **Q-R** | **Four validator thresholds exist as words with no number anywhere in the corpus.** Stage 3 "lower-bound feasibility … **with slack**"; stage 7 "3★ achievable **within band slack**"; stage 9 novelty "feature-vector distance … **above threshold**" (`docs/plan/specs/product_spec.md:639,643,645`); and stage 8's axis **B** "normalized to **band caps**" (`:506`) names no caps. | CM-C5 | Those four stages compute and **print** their value, then read the comparison threshold from `config/validator_thresholds.json`; **an absent row yields the verdict `UNCONFIGURED`, which prints and does not block** (criterion 13). No agent picks a number (stop condition 3). Every other stage blocks normally. |
| **Q-S** | **The daily board *generator algorithm* is specified nowhere.** ADR-0008:36 draws a "generator (dailies, 90 dates)" box; `docs/plan/specs/liveops_spec.md` gives the seed, the salt loop and the weekday ramp, but no board-shaping rule. NEW-Q21 additionally pins the weekday curve and `config/daily_weekday_curve.json` does not exist. | CM-C6 | CM-C6 ships the **pipeline** (seed derivation → salt loop → validate → artifact) against a caller-supplied `IBoardFactory`; the shipped factory is **out of scope and stop-condition-gated** (stop condition 2). Every pipeline criterion is testable today with a stub factory. |
| **Q-T** | **`config/runtime_bounds.json` authorship — assigned here, needs ratification.** Tranche 1's CM-C2 stop condition 6 asked who authors it. **This decomposition assigns it to CM-C7** (the save contract, which is the ADR-0006 owner). Sub-question the human must also ack: **is a purchase *ledger data structure* inside `Application/Save/**` a monetization tripwire?** The AGENTS.md globs are `**/billing/**`, `**/iap/**`, `**/ads/**` — `Application/Save/Ledger*` matches none, and CM-C7 makes zero store/SDK calls; but the adjacency deserves an explicit human yes before merge. | CM-C2a, CM-C7 | CM-C2a keeps its bounds in `CatMetro.Content.ContentBounds` (hard-coded from the cited sources) and does **not** author the file. CM-C7 authors it verbatim from ADR-0006 §4 and adds a **drift test** asserting `ContentBounds` equals the three `CONTENT_*` rows. If the human says the ledger is a tripwire, CM-C7 stops until `state/mode` is `production`. |
| **Q-U** | **M-21's backup limb contradicts ADR-0006 §5.** The threat model requires "a per-event idempotency id **that survives a backup restore**, not merely a retry" (`docs/security/threat-model.md:211`), while ADR-0006:282,285-289 excludes `analytics_queue.dat` from Play auto-backup **unconditionally**, precisely so a restored queue can never re-emit. Under exclusion the id has nothing to survive. | CM-C8 | CM-C8 implements the four limbs it can (bounds, drop-oldest, `queue_dropped`, metrics-only) plus an id stable across **process restart and flush retry**; the backup limb is recorded as **satisfied by file exclusion, not by the id**, and the manifest/backup-rules artifact that makes exclusion true is **not in this contract** (Android manifest → Q-G; the exclusion set → RK-17, ADR-0006 §Open conflict). Recorded as a deviation, not met. |
| **Q-V** | **The `validate-content` / `validate-dailies` workflow files live under `.github/**`** — a declared risky path requiring independent security review, hook- and CODEOWNERS-protected (AGENTS.md §Risky paths; RK-37 at `docs/adr/0009-...:18-20`). | CM-C5, CM-C6 | CM-C5 and CM-C6 deliver **entry-point scripts** (`scripts/validate-content.sh`, `scripts/validate-dailies.sh`) and fast-leg wrappers only. Wiring them into a workflow is a separate human-gated `.github/**` PR. Until then the jobs exist but nothing triggers them in CI. |
| **Q-W** | **"Solver-optimal" is undefined beyond completion time.** CM-R19.1 and `docs/plan/specs/product_spec.md:389` say "solver-optimal completion time"; CM-R10.6 and CM-R04.2 also consume a solver-optimal *run*. Nothing defines the tie-break when two winning logs share a completion tick. | CM-C4, CM-C5 | **Analyst assumption A-C4-3 (unratified):** optimal = minimal completion ticks; ties broken by **fewer commands**, then by **lexicographic order of `(Tick, SwitchId)` pairs**. Deterministic and asserted (criterion 7). An overrule changes the reported log, not solvability. |
| **Q-X** | **Two new `dotnet`-leg host executables, analyst-assigned.** CM-C5 and CM-C6 must do file I/O and **may not do it where their logic lives** (CM-C2a criterion 2 bans `System.IO` under `unity/Assets/Scripts/Content/**`; ADR-0008:53-56). So each owns a console host — `dotnet/CatMetro.Validator/**` and `dotnet/CatMetro.DailyTools/**`. These are **leg-only tool exes with no Unity asmdef counterpart**, so they are not rows in ADR-0003's 13-assembly list (§Locked in — assembly names are irreversible) — but a human should confirm that a `dotnet`-leg tool project sits **outside** that list rather than being a 14th and 15th row. | CM-C5, CM-C6 | Both contracts ship the host as specified and name it in the PR. If the architect rules it an ADR-0003 row, the remedy is an ADR amendment plus a rename — **no behaviour change, no criterion changes meaning.** Same shape as Q-M. |
| **Q-Y** | **`config/runtime_bounds.json` ↔ `unity/Assets/StreamingAssets/config/runtime_bounds.json` byte-identity has no owner.** The required `ci` job asserts it (`docs/adr/0009-ci-topology-and-secret-custody.md:33`; ADR-0008 names the copy step). **CM-C7 authors the source file and owns no `StreamingAssets` path**; `.../StreamingAssets/content/**` is CM-C2b's and `.../StreamingAssets/config/**` is unowned by every contract in this queue. Making CM-C7 write it would mean creating a Unity asset folder (and its `.meta`) before the Q-G scaffold exists — no `.meta` file exists anywhere in `unity/` today. | CM-C7, and whoever takes the content-pipeline contract | **CM-C7 defers the copy step and says so** (non-goal + PR note): the `ci` byte-identity assertion is **unsatisfiable until a content-pipeline contract owns `unity/Assets/StreamingAssets/config/**`**, and CM-C7's PR names that follow-up. A human must either accept the deferral (and keep that `ci` clause off until the follow-up lands) or assign the path now. **RESOLVED at the 2026-08-04 recut: CM-C2b criterion 10 owns the path (ownership row updated) and delivers the byte-identity gate.** |

**Unverified / not checked in this session:** no external market or platform claim is made in this file.
No user datapoints exist for Cat Metro as of 2026-08-03 (`docs/prd/PRD.md:64`); nothing here is derived
from user feedback. Nothing in this file resolves a D-decision or a §4.1 pin.

---

## Decomposition revision — CM-C2 is split (recorded honestly)

**What changed and why.** Tranche 1's CM-C2 bundled two kinds of work behind one blocker: content
authoring + JSON→DTO→`LevelGraph` import (**pure C#, no engine**) and greybox render/input/manifest/
device-perf (**Unity, licensed, human-scaffold-dependent**). ADR-0005 exists to keep exactly those two
apart — "Code with no engine references does not need an engine to compile or a licence to test"
(`docs/adr/0005-dotnet-first-dual-test-harness.md:29-31`) — and the whole import half was sitting idle
behind Q-G for no reason. It also made the contract 12 criteria long across two hosts, which violates
one-branch sizing (AGENTS.md hard rule 4).

**The split:**

- **CM-C2a — Content importer (dotnet).** Old CM-C2 criteria 1, 2, 3, 4 → re-cut and extended to 12.
  **UNBLOCKED**: no Unity, no licence, no human in the loop.
- **CM-C2b — Greybox board render (Unity).** Old CM-C2 criteria 5, 6, 7, 8, 9, 10, 11, 12 → renumbered
  1–8, unchanged in substance. **Still BLOCKED-ON Q-G**, and now additionally **DEPENDS-ON CM-C2a**.

**What the split costs, stated plainly:** two PRs instead of one, and CM-C2b's PR can no longer show
"file on disk → cat on screen" in a single diff. **What it buys:** the entire content pipeline
(importer → solver → validator → dailies) becomes executable today, and the D9 CI content gate stops
being transitively blocked on a Unity licence. Nothing in old CM-C2's criteria was weakened or dropped;
the criteria are traceable one-for-one above.

**CM-C3 is unchanged** except that its **DEPENDS-ON is now CM-C2b** (it consumed old CM-C2's render,
input and `FrameLog.cs` deliverables, all of which landed in the CM-C2b half).

---

## Ownership disjointness

Each contract owns a **disjoint set of file paths**. No two contracts write the same file, with one
enumerated exception class (registration files, below). Path ownership is the review test: a diff that
touches a path owned by another contract is out of scope (AGENTS.md hard rule 4).

**Resolution rule (unchanged, implementable by a checker):** ownership of a changed path is decided by
**ordered longest-prefix match** over the globs below — the most specific matching glob wins, and the
`EXCEPT` clauses are the explicit longer prefixes. A path matching no glob is unowned: a diff touching
it is out of scope for every contract in this queue.

| Contract | Owns (writes) |
|---|---|
| **CM-C1** *(merged; frozen — edits here are a stop condition for everyone else)* | `unity/Assets/Scripts/Domain/**` **EXCEPT `unity/Assets/Scripts/Domain/Solver/**`** · `unity/Assets/Tests/EditMode/Pure/Domain/**` · `dotnet/CatMetro.Domain/**` · `dotnet/CatMetro.Tests/**` · `dotnet/CatMetro.sln` · `dotnet/packages.lock.json` · `tests/domain/**` · `tests/fixtures/purity-bad/**` · `config/pins.json` · `scripts/check.sh` |
| **CM-C2a** | `unity/Assets/Scripts/Content/**` **EXCEPT `unity/Assets/Scripts/Content/Validation/**` and `unity/Assets/Scripts/Content/Daily/**`** · `unity/Assets/Scripts/Services/Content/**` · `unity/Assets/Tests/EditMode/Pure/Content/**` · `dotnet/CatMetro.Content/**` · `dotnet/CatMetro.Services/**` (project file created here; see the glob note) · `content/levels/**` · `tests/content/**` · `tests/fixtures/content-bad/**` |
| **CM-C2b** | `unity/Assets/Scripts/Presentation/Board/**`, `/Input/**`, `/Diagnostics/**` · `unity/Assets/Scripts/Presentation/Hud/**` **EXCEPT `.../Hud/WavePreview/**`** · `unity/Assets/Scripts/Application/**` **EXCEPT `Application/Retry/**`, `Application/Save/**`, `Application/Analytics/**`** · `unity/Assets/Scripts/Bootstrap/**` · `unity/Assets/Scenes/Game*` · `unity/Assets/StreamingAssets/content/**` · `unity/Assets/StreamingAssets/config/**` *(granted by the 2026-08-04 recut for criterion 10 — closes Q-Y)* · `unity/Assets/Resources/Strings/ui.csv` (created here) · `unity/Assets/Tests/EditMode/Engine/**` · `unity/Assets/Tests/PlayMode/Board/**` · `tests/unity/**` **EXCEPT `tests/unity/failure.test.sh`** |
| **CM-C3** | `unity/Assets/Scripts/Presentation/Failure/**` · `.../Presentation/Hud/WavePreview/**` · `.../Presentation/Camera/**` · `unity/Assets/Scripts/Application/Retry/**` · `unity/Assets/Tests/PlayMode/Failure/**` · `unity/Assets/Tests/EditMode/Pure/Retry/**` · `tests/unity/failure.test.sh` · **append-only rows** in `unity/Assets/Resources/Strings/ui.csv` |
| **CM-C4** | `unity/Assets/Scripts/Domain/Solver/**` · `unity/Assets/Tests/EditMode/Pure/Solver/**` · `tests/solver/**` |
| **CM-C5** | `unity/Assets/Scripts/Content/Validation/**` · **`dotnet/CatMetro.Validator/**`** (the console host: file I/O + the on-disk `IContentSource`) · `unity/Assets/Tests/EditMode/Pure/Validation/**` · `tests/validation/**` · `scripts/validate-content.sh` · `config/validator_thresholds.json` |
| **CM-C6** | `unity/Assets/Scripts/Content/Daily/**` · **`dotnet/CatMetro.DailyTools/**`** (the console host: file I/O + artifact writing) · `unity/Assets/Tests/EditMode/Pure/Daily/**` · `tests/daily/**` · `scripts/validate-dailies.sh` · `config/daily_pipeline.json` |
| **CM-C7** | `unity/Assets/Scripts/Application/Save/**` · `unity/Assets/Scripts/Services/**` **EXCEPT `Services/Content/**` and `Services/Analytics/**`** · `unity/Assets/Tests/EditMode/Pure/Save/**` · `dotnet/CatMetro.Application/**` · `tests/save/**` · `config/runtime_bounds.json` |
| **CM-C8** | `unity/Assets/Scripts/Application/Analytics/**` · `unity/Assets/Scripts/Services/Analytics/**` · `unity/Assets/Tests/EditMode/Pure/Analytics/**` · `tests/analytics/**` |

**Enumerated exception (registration-only edits).** Because the contracts are **ordered**, a later
contract may append — never modify — registration lines in files another contract owns:
`dotnet/CatMetro.sln` (project entries), `dotnet/CatMetro.Tests/CatMetro.Tests.csproj`
(`ProjectReference` **only**), `dotnet/packages.lock.json` (regenerated), `config/pins.json` (new pin
rows), `scripts/check.sh` (**append a new static-check block or a new banned-symbol root; never
restructure the file or alter the existing blocks at `scripts/check.sh:37-63`**), and — for CM-C3 —
**new rows** in `unity/Assets/Resources/Strings/ui.csv`.

**Why CM-C5 and CM-C6 each own a `dotnet/` console host.** Both contracts must open files — CM-C5's
batch validator reads `content/levels/**` and `docs/plan/data/stress_boards.json`, CM-C6's job writes
`--out <path>` — and **neither may do it in the library where its logic lives**: CM-C2a criterion 2
appends a `check.sh` block failing on any `System\.IO` match under `unity/Assets/Scripts/Content/**`,
which is exactly where `Content/Validation/**` and `Content/Daily/**` sit (ADR-0008:53-56 — Content
receives *bytes*). `dotnet/CatMetro.Validator/**` and `dotnet/CatMetro.DailyTools/**` are therefore
**console executables in the licence-free `dotnet` leg** that host the file I/O and the only on-disk
`IContentSource` implementation, and that each entry-point script invokes with `dotnet run`. They are
registered in `dotnet/CatMetro.sln` under the registration-only exception above and, being executables
rather than libraries, they carry **no** `Compile Include` link-glob over a `unity/` tree — they
reference `dotnet/CatMetro.Content/` and `dotnet/CatMetro.Services/` as ordinary `ProjectReference`s.
**Their existence is analyst-assigned and needs architect ack — see Q-X.**

**No `Compile Include` append is permitted, in any csproj.** The existing globs are deliberately
open-ended and pick new folders up for free:
`dotnet/CatMetro.Domain/CatMetro.Domain.csproj:17` links `../../unity/Assets/Scripts/Domain/**/*.cs`
(so **CM-C4's `Domain/Solver/**` compiles with zero csproj edit**), and
`dotnet/CatMetro.Tests/CatMetro.Tests.csproj:17` links `../../unity/Assets/Tests/EditMode/Pure/**/*.cs`
(so `Pure/Content/**`, `Pure/Solver/**`, `Pure/Validation/**`, `Pure/Daily/**`, `Pure/Save/**`,
`Pure/Analytics/**`, `Pure/Retry/**` are all already covered). Every new library project created by
this queue **must use the same one-line link-glob shape**, because ADR-0005's link-glob and test-split
parity checks assert exactly that (`docs/adr/0005-...:167-172`), and because appending an `Include`
would break CM-C1 criterion 2's equality assertion.

Any edit to CM-C1's Domain sources or to `tests/domain/determinism.test.sh` from a later contract is a
**stop condition**, not a merge conflict to resolve. The golden at
`tests/contract/replay-hash-golden.json` is immutable for every contract in this file.

### Solver placement — the decision and its rationale (Q-M)

**Placed at `unity/Assets/Scripts/Domain/Solver/**`, inside `CatMetro.Domain`.** Reasoning, in the
order the constraints bind:

1. **Engine-free is non-negotiable.** ADR-0005:112 assigns "solver, level schema + bounds validation,
   fuzz corpus" to the `dotnet` leg, and ADR-0009:35 gives `validate-content` **zero credentials**.
   `CatMetro.Editor` is an editor assembly (`docs/adr/0003-...:44`) — it cannot run in either. So
   `architecture/overview.md:56`'s "solver runner · batch validator" node and roadmap D8's "Editor
   solver runner" (`roadmap_56_days.csv:10`) are **flagged as the conflict**, not followed.
   *(A reading that reconciles them: ADR-0003 gives the Domain "solver **step**" and the Editor a
   "solver **runner**" — an editor menu entry. Neither row names a home for the **search**, and that is
   precisely the gap.)*
2. **Among the four engine-free assemblies, `Domain` is the only one whose declared ownership already
   names the solver** ("graph, tick, rules, score, PCG32, command log, **solver step**",
   `docs/adr/0003-...:34`), and it is the only one the search actually needs: the search consumes
   `LevelGraph` and calls `Simulation.Step`, both Domain types. `Content`/`Application` placement would
   work but adds a dependency the search does not need and mixes offline tooling into runtime layers.
3. **It costs zero new assemblies.** ADR-0003 declares assembly names **irreversible** (§Locked in);
   adding a 14th row is a human ADR gate, not an agent decision. Placement inside Domain keeps the
   13-assembly count and the asmdef↔csproj parity check untouched.
4. **It buys a free correctness constraint.** `scripts/check.sh:61` bans `float`/`double`/`decimal`/
   clocks under `unity/Assets/Scripts/Domain/**`, so **the search is integer-only and clock-free by
   construction** — exactly the property that makes a CI-green level un-unsolvable on device
   (`docs/adr/0008-...:185`). The float-shaped work (difficulty ±0.05, band slack) lives in CM-C5 under
   `CatMetro.Content`, which is **not** under a banned-symbol root.

**The honest costs, recorded rather than hidden:** (a) solver code compiles into the shipping player
assembly (IL2CPP code size; managed stripping may or may not remove it — unverified, and it is a
release-gate measurement, not a claim made here); (b) `CatMetro.Domain` grows beyond "the tick loop",
which is a readability tax; (c) CM-R01.6's zero-alloc-in-`Playing` rule now needs a *call-site* guard
rather than an assembly-boundary one — CM-C4 criterion 13 supplies it. If the architect prefers (b) or
(c) from Q-M, the remedy is a folder move plus one csproj: **no behaviour change, no golden impact.**

## Dependency order (human-ordered; do not parallelise without a human saying so)

```
CM-C1  ── MERGED (golden human-committed, CI green; state/PROJECT_STATE.md:15)
   │
   ├─> CM-C2a  Content importer (dotnet)            UNBLOCKED — no Unity, no licence
   │      ├─> CM-C2b  Greybox board (Unity)         READY — Q-G resolved (#19; recut 2026-08-04)
   │      │      └─> CM-C3  Fail/retry loop         DEPENDS-ON CM-C2b merged
   │      ├─> CM-C7  Save v1 (dotnet subset)        DEPENDS-ON CM-C2a merged
   │      │      │                                  (dotnet/CatMetro.Services/ skeleton)
   │      │      └─> CM-C8  Analytics offline queue  DEPENDS-ON CM-C7 (the Services/
   │      │                                          Application projects and the
   │      │                                          runtime_bounds.json rows)
   │      └─────────────┐
   │                    ├─> CM-C5  11-stage validator + validate-content leg
   │                    │         (DEPENDS-ON CM-C2a + CM-C4)
   └─> CM-C4  Solver ───┘         └─> CM-C6  Daily-seed pre-validation
          HYBRID-ELIGIBLE                   (DEPENDS-ON CM-C4 + CM-C5)
          UNBLOCKED
```

**Startable today with no human and no licence:** CM-C2a, CM-C4 (**two** independent roots).
**Startable the moment CM-C2a merges:** CM-C7 (it needs CM-C2a's `dotnet/CatMetro.Services/` project
skeleton — CM-C7's own header, and CM-C2a criterion 2, are the binding statement) → then CM-C8.
**Startable after Q-G only:** CM-C2b → CM-C3.

`state/mode` is **sprint** (`docs/prd/PRD.md:940`, A-00): ceremony is priced, the enforcement floor is
not. TDD for Domain code, immutable paths, `[CI]` criteria and the human-merge gate stand at full
strength for every contract below.

### Hybrid local-model lane (applies to CM-C4; opt-in per contract)

`docs/plan/EXECUTION_PLAN.md:523-529` scopes the lane: **frontier plans and reviews, the local model
implements**, limited to **pure-C# Domain work**, with `auto_execute` **false** until ≥3 contracts merge
clean. CM-C4 is the first contract in this queue that fits: it is pure C#, engine-free, has a friendly
check command (`bash scripts/test.sh`, not a batchmode editor run — ADR-0005:156-157), and touches no
SDK, no device and no immutable path.

**If the human opens the lane for CM-C4**, its execution may be sub-cut into **task-contract JSONs of
≤2 acceptance criteria each**, in the criterion order below (1–2 · 3 · 4 · 5 · 6–7 · 8–9 · 10–11 ·
12–13). Binding conditions: every sub-cut carries the *same* stop conditions; the frontier session
authors the sub-cuts and reviews every diff in fresh context; `auto_execute` stays **false**; and a
sub-cut may never be the unit of merge — **CM-C4 merges as one PR** (AGENTS.md hard rule 4).
CM-C5/C6/C7/C8 are **not** hybrid-eligible as written: C5 and C6 leave `Domain`, and C7/C8 touch the
save/ledger surface where Q-T's tripwire ack is pending.

---

# CONTRACT CM-C1 — Deterministic Domain skeleton + replay-hash stability test — **MERGED**

**Status (2026-08-03): MERGED to `main`.** 14 acceptance criteria met; the golden
`tests/contract/replay-hash-golden.json` was **human-committed** on the branch, CI turned green, merged
via PR #3 (`state/PROJECT_STATE.md:15`). Shipped surface — cited by every contract below, and **frozen**
for all of them: `CatMetro.Domain.{SimConstants, Pcg32, LevelGraph, CatColor, SimulationState,
TrainSlot, TrainState, ToggleSwitchCommand, CommandLog, Simulation.Step, FailReason, OutcomeKind,
SimOutcome, ReplayHasher}` under `unity/Assets/Scripts/Domain/`. The full contract text (goal, spec
references, 14 criteria, scope boundary, assumptions A-C1-1…A-C1-7, 7 stop conditions) is preserved in
git history at the tranche-1 revision of this file; it is not reproduced here because a merged contract
is a record, not a queue item. **Three shipped facts every later contract depends on and must not
change:**

- `Simulation.Step(ref SimulationState, ReadOnlySpan<ToggleSwitchCommand>)` is the **one** step symbol
  (`unity/Assets/Scripts/Domain/Simulation.cs:32`; ADR-0002 §2).
- The digest layout and `SimulationState.DigestLength(nSwitches, nNodes, nTrainsMax, qCap) =
  46 + nSwitches + nNodes*(3 + 2*qCap) + 10*nTrainsMax` (`SimulationState.cs:70-71`) are
  **golden-defining**; touching either invalidates `tests/contract/replay-hash-golden.json`.
- Four pin guards throw `NotSupportedException` rather than inventing semantics: non-matching station
  arrival (`Simulation.cs:116`), `Failed(PlatformOverflow)` (`Outcomes.cs:40`), second source
  (`LevelGraph.cs:64,92`), `wild` colour (`LevelGraph.cs:68`). **See Q-N** — these are load-bearing for
  CM-C4/C5/C6.

---

# CONTRACT CM-C2a — Content importer: L001 authored + schema-v2 parse to immutable DTOs → `LevelGraph`

**Roadmap:** D3 content (`docs/plan/data/roadmap_56_days.csv:4` — "L1 authored in schema v2 JSON
(deliverables/data/level_schema.json) and loaded through the Content importer").
**DEPENDS-ON:** CM-C1 (merged). **Blocked on:** *nothing* — no Unity, no licence, no human in the loop
(ADR-0005 §Consequences, `:153-155`).

### Goal

`content/levels/L001.json` exists as shipped-source content, and a `CatMetro.Content` library parses it
— under a hardened, bounds-checked, non-polymorphic parser — into `sealed` immutable DTOs and maps
those totally onto the shipped `CatMetro.Domain.LevelGraph`, all compiled and tested by `dotnet` with
no engine present.

### Spec reference

`docs/prd/PRD.md` CM-R13.2 (L001 `initialRoute:1`, `minActionWindowTicks:16`) · CM-R12.1 (schema stage)
· CM-R02.1 (the mapping feeds the one step function) ·
`docs/adr/0008-content-pipeline-and-level-schema.md` §Immutable DTOs (`:79-84`), §Parsing rules MUST
1–5 (`:86-107`), §Level schema v2 is frozen (`:125-129`) ·
`docs/adr/0006-save-format-purchase-ledger-and-runtime-bounds.md:190-192` (the three `CONTENT_*` rows) ·
`docs/adr/0003-assembly-isolation-and-dependency-rule.md:35,71-74` (Content row; `IContentSource`) ·
`docs/adr/0005-dotnet-first-dual-test-harness.md:50-53,167-172` (Content joins the dotnet leg; parity) ·
`docs/plan/data/level_schema.json` (the frozen contract) · `docs/plan/data/example_levels.json:4-17`
(authored L001) · `unity/Assets/Scripts/Domain/LevelGraph.cs` (the target type — **as shipped**).

### Acceptance criteria (13)

Each is independently checkable by the named command; a criterion is met only when the named check
exits 0 (or fails exactly as specified).

1. **L001 exists as shipped-source content, byte-true to the authored example.**
   `content/levels/L001.json` is schema-v2 valid and its authored field values are identical to
   `docs/plan/data/example_levels.json:4-17`: `schemaVersion 2`, `id "L001"`, `seed 1001`,
   `meta.band "onboarding"`, `meta.difficultyTarget 0.08`, `meta.mechanics ["switch"]`,
   `meta.newMechanic "switch"`, `meta.minActionWindowTicks 16`, `meta.authoredBy "human"`,
   **`meta.validatedAt` absent** (AMD-09 / ADR-0008:119-123 — the key is deleted, never `null`),
   4 nodes / 3 edges / 1 source / 2 stations / 1 switch with `initialRoute: 1` / 1 wave
   (`tick 8, color red, count 2, spacingTicks 20`), `win.deliveries 2`, `win.timeLimitTicks 160`,
   `win.perfectMaxSwitches 1`, `win.stars {two:200, three:300}`, `economy {baseTickets:20,
   perfectBonus:10}`. *Check:* one NUnit case per named field (asserted from the parsed DTO **and**
   from a raw-JSON key walk, so a parser bug cannot mask a content bug) + one case asserting
   `meta` has no `validatedAt` key.
2. **`CatMetro.Content` joins the dotnet leg and is engine-free.**
   `dotnet/CatMetro.Content/CatMetro.Content.csproj` targets **`netstandard2.1`**, links exactly
   `../../unity/Assets/Scripts/Content/**/*.cs` in one `Compile Include`, is registered in
   `dotnet/CatMetro.sln`, and `dotnet build dotnet/CatMetro.sln -c Release` exits 0.
   `dotnet/CatMetro.Services/CatMetro.Services.csproj` is created the same way over
   `../../unity/Assets/Scripts/Services/**/*.cs`. Zero `UnityEngine` and zero `System.IO` types appear
   under `unity/Assets/Scripts/Content/**` (ADR-0008:53-56 — Content receives *bytes*).
   *Check:* build exit code + a test asserting each new csproj's `Compile Include` string equals the
   specified glob + a `scripts/check.sh` appended grep block for `UnityEngine|System\.IO` under
   `unity/Assets/Scripts/Content/**`.
3. **DTOs are immutable.** Every level DTO type is `sealed` with `readonly` fields; every array-bearing
   property is exposed as `ReadOnlyMemory<T>` / `ReadOnlySpan<T>` or an immutable view; no public
   setter and no mutating method exists on any DTO (ADR-0008:79-84; ADR-0005:174-181 confirms these
   types exist under `netstandard2.1`). *Check:* one reflection-driven NUnit case enumerating every
   type in the DTO namespace and asserting `IsSealed`, all fields `IsInitOnly`, zero settable
   properties, and zero array-typed public members.
4. **One serializer-settings site in the whole tree, and its `TypeNameHandling` is `None`.**
   **Exactly one `JsonSerializerSettings` construction site exists**, in `CatMetro.Content` (e.g.
   `CatMetro.Content.ContentJson.Settings`), and it sets `TypeNameHandling = None` (ADR-0008:88-90,
   permanent rule). `scripts/check.sh` gains an appended block failing on any `*.cs` match of
   `TypeNameHandling` under `unity/Assets/Scripts/**` **outside that one file path** — the exception is
   a **path**, not "the first site someone writes", so a later contract can satisfy the block without
   editing it. **Every later contract that serialises reuses this factory and constructs none of its
   own** (ADR-0003 permits `Application` → `Content`): see CM-C7 A-C7-3 and CM-C8 A-C8-5.
   *Check:* two runs pasted in the PR — (a) `bash scripts/check.sh` exits 0 on the clean tree;
   (b) `bash scripts/check.sh --root tests/fixtures/content-bad` (or the block's own negative fixture)
   exits non-zero naming the file. Plus one NUnit case asserting the live serializer settings object
   reports `TypeNameHandling.None`, and one `[CI]` grep asserting the tree contains exactly one
   `new JsonSerializerSettings` occurrence.
5. **`ContentBounds` is one constants class and every number is cited.**
   `CatMetro.Content.ContentBounds` declares exactly, with the citation in a comment on each row:
   `CONTENT_MAX_FILE_BYTES = 262144` (`docs/adr/0006-...:190`),
   `CONTENT_MAX_JSON_DEPTH = 16` (`docs/adr/0006-...:191`),
   `MAX_NODES = 40` (`level_schema.json:34`), `MAX_EDGES = 70` (`:45`), `MAX_WAVES = 30` (`:108`),
   `MAX_SWITCHES = 10` (`:81`), `MAX_SOURCES = 6` (`:60`), `MAX_STATIONS = 6` (`:70`),
   `TRAVEL_TICKS_MIN = 1` / `TRAVEL_TICKS_MAX = 40` (`:51`),
   `TIME_LIMIT_TICKS_MIN = 20` / `TIME_LIMIT_TICKS_MAX = 4000` (`:125`),
   `QUEUE_CAPACITY_MIN = 1` / `QUEUE_CAPACITY_MAX = 8` (`:40`),
   `STATION_CAPACITY_MIN = 1` / `STATION_CAPACITY_MAX = 12` (`:76`),
   `INITIAL_ROUTE_MIN = 0` / `INITIAL_ROUTE_MAX = 2` (`:88`),
   `ROUTES_MIN = 2` / `ROUTES_MAX = 3` (`:87`),
   `WAVE_COUNT_MIN = 1` / `WAVE_COUNT_MAX = 8` (`:115`),
   `SPACING_TICKS_MIN = 1` / `SPACING_TICKS_MAX = 40` (`:116`),
   `MIN_ACTION_WINDOW_TICKS_FLOOR = 3` (`:23`).
   **`config/runtime_bounds.json` does not exist yet and CM-C2a does not author it** — see Q-T; CM-C7
   authors it and adds the drift test. *Check:* one NUnit case asserting each constant's value, and one
   `[CI]` grep asserting no other source file contains a bare integer literal from the **distinctive
   multi-digit subset** `262144 · 4000 · 70 · 40 · 30 · 12`, rooted at
   `unity/Assets/Scripts/Content/**` **EXCEPT `Content/Validation/**` and `Content/Daily/**`**.
   Two deliberate narrowings, both load-bearing: **(a)** the small values (`1, 2, 3, 6, 8, 10, 16`) are
   excluded because they appear in ordinary code — array indices, `schemaVersion == 2` — so a grep over
   the full list is unimplementable, not merely noisy; **(b)** the `EXCEPT` roots mirror the ownership
   table's own `EXCEPT` clauses, so a correct CM-C5 or CM-C6 diff can never break a CM-C2a criterion.
6. **Pre-parse bounds run before the parser sees the bytes.** A payload over
   `CONTENT_MAX_FILE_BYTES` and a document deeper than `CONTENT_MAX_JSON_DEPTH` are both rejected
   **before** deserialization, with a typed failure and **no exception escaping to the caller**
   (ADR-0008:92-93). *Check:* two NUnit cases asserting the typed failure value, and — for the depth
   case — asserting the reader's configured `MaxDepth` equals the constant.
7. **Post-parse bounds + referential integrity, one fixture per rule.** Parsing returns a typed error
   (never a thrown exception) for each of: (a) `travelTicks` outside 1–40; (b) `win.timeLimitTicks`
   outside 20–4000; (c) `queueCapacity` outside 1–8; (d) `initialRoute` outside 0–2 **or**
   ≥ `routes.length`; (e) a dangling `from`/`to`/`nodeId`/`sourceNode`/`routes[]` id;
   (f) any collection over its cap (nodes/edges/waves/switches/sources/stations);
   (g) `schemaVersion != 2` (ADR-0008 rule 2 at `:92-98`; §Level schema v2 is frozen).
   *Check:* 7 NUnit cases over 7 fixtures under `tests/fixtures/content-bad/`, each asserting the typed
   failure discriminant **and** `Assert.DoesNotThrow`.
8. **Fuzz corpus, as an ordinary fast test.** A corpus of malformed and adversarial level JSON —
   truncation, depth bomb, huge counts, duplicate ids, dangling references, NaN/exponent numerics,
   duplicate keys, BOM/encoding oddities (the eight classes named at ADR-0008:102-104) — runs under
   `dotnet test`; **every case returns a typed failure and none throws, hangs or allocates unbounded**
   (RK-34, `docs/prd/risks.md:125`). *Check:* one `[TestCaseSource]` case per corpus file, ≥3 files per
   named class (≥24 cases), each asserting typed-failure + `DoesNotThrow`.
9. **DTO → `Domain.LevelGraph` mapping is total and index-stable.** Every node, edge, switch, station,
   source and wave in L001 appears **exactly once** in the produced `LevelGraph`; string ids resolve to
   dense integer indices in both directions; the mapping order matches the authored file order (which
   `LevelGraph.cs:19-20` records as part of the digest contract); `QCapBound` and `TrainsMax` are
   populated from the authored file (`QCapBound` = the schema max 8, `level_schema.json:40`;
   `TrainsMax` = the sum of wave `count`s) exactly as CM-C1's fixtures did (A-C1-7).
   *Check:* one NUnit case per collection asserting a bijection id↔index, plus one asserting a
   round-trip through **the importer's id map** returns the original authored ids in order. **The map,
   not `LevelGraph`, is the round-trip surface:** the importer returns a `CatMetro.Content` id map
   (`id → dense index` and `index → id`, per collection) **alongside** the `LevelGraph`, and the
   assertion runs against that map. The shipped `LevelGraph` carries only `LevelId` plus dense integer
   arrays and **no** node/edge/switch/station id table (`unity/Assets/Scripts/Domain/LevelGraph.cs:21-44`)
   — by design; adding a lookup member to it would be a frozen-Domain edit and is stop condition 3.
10. **The mapping honours the shipped pin guards without swallowing them.** Mapping a level with a
    second source, or with a `wild` wave colour, returns a **typed failure naming the blocking pin**
    (`NEW-Q35` / second-source scope) and never lets `LevelGraph`'s `NotSupportedException`
    (`LevelGraph.cs:64,68`) escape to the caller; mapping L001 raises nothing.
    *Check:* three NUnit cases (second-source fixture → typed failure naming the pin; `wild` fixture →
    typed failure naming NEW-Q35; L001 → success), each asserting no exception escapes.
11. **`IContentSource` is the read seam and it is declared, not implemented, here.**
    `CatMetro.Services.IContentSource` is declared with the signature at
    `docs/architecture/overview.md:240-243` (`Task<byte[]> ReadAsync(string, CancellationToken)`,
    `bool Exists(string)`). `CatMetro.Content` consumes it and never touches the filesystem; tests
    supply an in-memory implementation. **No `CatMetro.Bootstrap` implementation is written** — that is
    engine-side and belongs to CM-C2b (ADR-0003:71-74, overview.md:211).
    *Check:* one NUnit case driving the importer through an in-memory `IContentSource`, plus the
    criterion-2 `System.IO` grep.
12. **Newtonsoft is pinned and locked.** `Newtonsoft.Json` is added at the exact version inside
    `com.unity.nuget.newtonsoft-json` (ADR-0008:166-172; ADR-0004), recorded as a new row in
    `config/pins.json`, present in the regenerated `dotnet/packages.lock.json`, and `dotnet restore
    --locked-mode` exits 0 with no floating range in any csproj (ADR-0004 pin hygiene;
    `docs/adr/0009-...:33`). The ADR reference is cited in the PR description (AGENTS.md hard rule 2).
    *Check:* restore exit code + the grep assertion CM-C1 criterion 3 already established.
13. **Harness discovery.** `tests/content/importer.test.sh` exits 0 iff `dotnet test` is green, and
    `bash scripts/test.sh` prints `PASS tests/content/importer.test.sh` and a summary line matching
    `^test: [0-9]+/[0-9]+ passed` **whose two numbers the wrapper extracts and compares equal**
    (`scripts/test.sh:13,24`). **The equality is asserted in the wrapper, not in the regex** —
    `^test: ([0-9]+)/\1 passed` uses a backreference, which POSIX ERE (`grep -E`) does not support, so
    it is not runnable on the default toolchain; `grep -P` is the only alternative and is not portable.
    *Check:* `bash scripts/test.sh` exits 0 with both lines in stdout and the wrapper's numeric
    comparison passing.

### Scope boundary

**In scope:** the paths in the ownership table for CM-C2a, plus registration-only appends
(sln project entries · test-csproj `ProjectReference` only · `config/pins.json` rows ·
`scripts/check.sh` appended blocks).

**Explicit non-goals (a diff touching these is a failed review):**
- **No Unity anything** — no scene, no prefab, no `.asmdef`, no `.meta`, no `UnityEngine` reference, no
  `StreamingAssets` staging. CM-C2b's half.
- **No render, no input, no HUD, no `FrameLog.cs`, no manifest, no device measurement.**
- **No `catalog.json` / `content.sha256` / ContentSync / `contentHash`** — the full pipeline
  (ADR-0008 §Source of truth) is later; CM-C2a loads one file through `IContentSource`.
- **No solver, no validator stages, no daily generator** (CM-C4/C5/C6).
- **No `config/runtime_bounds.json`** (Q-T → CM-C7). **No schema change** (schema v2 frozen).
- **No levels beyond L001.** No daily, no Night Harbor, no stress boards.
- **No save, no ledger, no analytics, no SDK, no commerce.**
- **No path matching `**/billing/**`, `**/iap/**` or `**/ads/**`**; any such need is a **stop
  condition** requiring `state/mode=production` first (AGENTS.md §Risky paths;
  `state/PROJECT_STATE.md:10`).
- **No edits to CM-C1's Domain sources or `tests/domain/determinism.test.sh`**; **no `Compile Include`
  append** to `CatMetro.Tests.csproj`.
- **No writes to `tests/contract/`, `docs/constitution.md`, `.claude/hooks/`, `scripts/git-hooks/`,
  `state/mode`, `evals/`** (AGENTS.md hard rule 1).

### Assumptions

- **A-C2a-1** L001's authored `win.deliveries` is **2** (Q-E). The roadmap's "10 cats" is prose.
- **A-C2a-2** `Newtonsoft.Json` is the parser (ADR-0008:166-172); no second parser is introduced.
- **A-C2a-3** The importer's failure channel is a **typed result**, not exceptions — ADR-0008:99-101
  requires "never a crash", and CM-R05/CM-R12 tests need a discriminant to assert on. The exact result
  type is the implementer's; that it is total and non-throwing is the criterion.
- **A-C2a-4** `TrainsMax` = sum of authored wave `count`s and `QCapBound` = the schema max 8, matching
  the shipped digest padding (A-C1-7, `LevelGraph.cs:42-43`). Changing either is golden-invalidating.
- **A-C2a-5** `CatMetro.Services` is created here because CM-C2a is the first contract needing it; C7
  and C8 add files under it without touching the csproj (link-glob mechanism).

### Stop conditions

Defaults apply (schema change · new dependency · criteria conflict → stop and ask). Plus:
1. Any level-schema field is needed that schema v2 does not have (notably `meta.district`, Q-F/A-19) → stop.
2. A bound needed by criterion 5 is absent from both ADR-0006 §4 and `docs/plan/data/level_schema.json`
   → stop; **do not choose a number.**
3. Any Domain behaviour change is needed to make the mapping total → stop; that re-opens the golden.
4. The mapping cannot be made total without inventing semantics for a pinned mechanic → stop (Q-A/Q-B).
5. `config/runtime_bounds.json` appears to be needed → stop and cite Q-T; CM-C2a never authors it.
6. Any temptation to make the importer write to `content/levels/**` (normalising, re-serialising,
   stamping) → stop; the authored file is the source of truth (ADR-0008:47-48).

---

# CONTRACT CM-C2b — Greybox board: render, input, win/fail, manifest, 60 fps (Unity side)

**Roadmap:** D3 engineering, `docs/plan/data/roadmap_56_days.csv:4`.
**DEPENDS-ON:** CM-C2a merged ✓ (#8). **Q-G RESOLVED (recut 2026-08-04):** the scaffold merged as
#19 (`e586712`) with every pin verified — 6000.3.16f1, IL2CPP, ARM64, URP, Input System, minSdk 25 /
targetSdk 36, `com.catmetro.game`, created in place; asmdefs per ADR-0003/0005; the EditMode suite
runs 324/324 in-engine with the replay hash byte-identical to the dotnet leg (the prep note's
"replay-hash parity EditMode leg" is therefore DONE at scaffold; the `unity-editmode` CI job stays
Q-V human). **Keystore + Play App Signing remain open human items** (`docs/plan/EXECUTION_PLAN.md:439`;
ADR-0004:36) — release-era, not a greybox blocker. **minSdk anywhere is 25** — the roadmap's 24 is
superseded by AMD-08 (`docs/plan/EXECUTION_PLAN.md:349-350`).

### Goal

L001, loaded through CM-C2a's importer, renders as a greybox board and is playable to a win and to an
overflow fail — with taps driving the shipped Domain through the command log and Presentation only
interpolating.

### Spec reference

`docs/prd/PRD.md` CM-R07.1/.3 · CM-R20.1 (≥48dp) · CM-R51.1 (`docs/prd/PRD.md:810`) · CM-R52 (perf) ·
`docs/prd/ux-flows.md` S-02 (`:148-198`) · `docs/adr/0003-*` (Presentation/Application/Bootstrap rows) ·
`docs/adr/0007-*` (UGUI+TMP, screen stack, Input System, no Addressables) ·
`docs/architecture/overview.md` §3 (`:119-149` tap → command → Step → snapshot → interpolate), §7.

### Acceptance criteria (11) — 1–8 from tranche 1 unchanged in substance; 9–11 added by the
### 2026-08-04 recut (state/handoffs/CM-C2b-C3-prep.md) to absorb what landed since tranche 2

1. **Greybox render fidelity.** Loading L001 instantiates exactly one view object per authored board
   element (4 nodes incl. 1 source and 2 stations, 3 edges, 1 switch), each carrying the authored id
   and positioned at the authored grid coordinate. *Check:* an EditMode/PlayMode test enumerating the
   scene and comparing the id set and coordinates to the DTO.
2. **Tap targets ≥48dp and one gesture handler.** On the 360×640dp reference frame
   (`docs/prd/ux-flows.md:32`), every interactive element's effective hit rect is ≥48dp (CM-R20.1); the
   Game scene registers **exactly one** gesture handler and zero drag/pinch/long-press-to-aim handlers
   (CM-R07.1). *Check:* two automated UI tests (enumerate-and-measure; enumerate-and-assert-count).
3. **Tap → command → tick, and the frame log exists.** A tap on the junction (a) changes the lever's
   visual state on the first rendered frame after tap-down, and (b) appends exactly one
   `ToggleSwitchCommand` to `CommandLog` (`unity/Assets/Scripts/Domain/Commands.cs:8-18,38`), applied at
   the next tick boundary (CM-R07.3; `docs/architecture/overview.md:129-137`). Two taps in one tick
   produce two entries in receipt order. **This criterion also delivers the instrumented frame log**
   CM-C3 criteria 2, 4 and 7 measure against:
   `unity/Assets/Scripts/Presentation/Diagnostics/FrameLog.cs`, one record per rendered frame with
   exactly `frameIndex:int`, `monotonicMs:long`, `simTick:int`, `screenState:string`; `monotonicMs`
   comes from **one** clock source, named in the file header and in every artifact citing the log.
   *Check:* a PlayMode test asserting log contents and applied tick; a frame-log assertion for the
   lever state; one test asserting a record per frame with all four fields populated and `monotonicMs`
   non-decreasing.
4. **Win by routing the authored cat count.** Playing L001 with the correct route delivers
   `win.deliveries` cats and the run ends `Won`, with the LOCKED banner string `"All cats home!"`
   (`docs/prd/ux-flows.md:188`) read from `unity/Assets/Resources/Strings/ui.csv`, **which this
   contract creates** (CM-C3 appends rows). Zero literal UI strings in components. **The asserted
   number is the level file's own `win.deliveries` (2 as authored), not the roadmap's "10" — Q-E.**
   *Check:* one PlayMode test asserting outcome, banner text, and that the text resolved through
   `ui.csv`.
5. **Fail by overflow — queue overflow only (Q-J).** A scripted fixture board whose **node queue**
   reaches `queueCapacity` and is not cleared within the 16-tick Overload window ends the run with
   `Failed(QueueOverflow)` (`Simulation.cs:131-151`), and the fail state renders (banner present, board
   still visible). *Check:* one PlayMode test asserting outcome + banner presence.
   **Deferred, not met:** roadmap D3's "fail by platform overflow" (`roadmap_56_days.csv:4`) is
   unmeetable while NEW-Q4/Q-J are open (`Outcomes.cs:40-42`). The PR records it as deferred and cites
   Q-J; it re-opens as a follow-up contract when Q-J lands.
6. **Presentation never simulates.** A static check asserts zero calls to `Simulation.Step` outside
   `CatMetro.Application` and the test assemblies; a unit test on the interpolator asserts that at a
   60 Hz render rate against an 8 tps sim the interpolation factor stays in `[0,1)`, increases
   monotonically between snapshots, and resets exactly once per tick (ADR-0002 §1;
   `docs/architecture/overview.md:147`). *Check:* grep assertion + one NUnit case in `EditMode/Pure/`.
7. **Manifest compliance.** The generated Android manifest declares `minSdkVersion=25` and
   `targetSdkVersion=36`; a check fails the build on any lower value (CM-R51.1, `docs/prd/PRD.md:810`;
   AMD-08). *Check:* a build-step assertion over the merged manifest, output pasted in the PR.
8. **60 fps on a Pixel-6a-class device — HUMAN-VERIFIED.** *An agent cannot run this.* On a
   Pixel-6a-class device, an IL2CPP/ARM64 release build playing L001 for **60 continuous seconds**
   records **median frame time ≤16.7 ms and 1%-low ≤33.3 ms** via `adb shell dumpsys gfxinfo <pkg>
   framestats` or the Unity profiler. The run artifact (device model, build id, raw frametime table,
   both figures) is attached to the PR. **The criterion fails if the artifact is absent**, not merely
   if the numbers miss (`roadmap_56_days.csv:4`; CM-R52).
9. **Bootstrap seams land (recut).** `CatMetro.Bootstrap` (asmdef per ADR-0003's added 10th row —
   the ONLY assembly that may name the engine's persistent data path) implements **`IStorageRoot`**
   (persistent + cache paths; CM-C7's `SaveStore` becomes constructible on device) and
   **`IContentSource`** (StreamingAssets reads via an Android-capable engine API — ADR-0008:53-56:
   the APK keeps StreamingAssets where plain file reads cannot reach on device; editor tests prove
   the seam, the device leg rides criterion 8's session). *Check:* an EditMode/PlayMode test loads
   L001 THROUGH the Bootstrap `IContentSource` into the importer and renders it (criterion 1 runs on
   this path, not a test shim); one test constructs `SaveStore` AND `AnalyticsQueue` against the
   Bootstrap `IStorageRoot` and commits+reloads each; `scripts/check.sh`'s runtime-tree guard stays
   green (Bootstrap must not reference solver types); a STATIC assertion that the Bootstrap content
   reader routes StreamingAssets through `UnityWebRequest` (zero plain-file reads of the
   streamingAssets path outside an explicitly editor-only branch — evaluator D4: an editor-passing
   `File.ReadAll` impl fails on device); and a grep over `unity/Assets/Scripts/**` EXCLUDING
   `Bootstrap/**` asserting zero references to the engine's persistent/cache path APIs (evaluator
   D5: Presentation is the first engine-referencing assembly where the ADR-0003 invariant becomes
   violable — the grep is appended to a discovered wrapper or `scripts/check.sh`). Criterion 8's
   device artifact additionally records that the played L001 was loaded through the Bootstrap seam
   (one log line in the run artifact).
10. **StreamingAssets ships the corpus + bounds, byte-identical (recut — closes Q-Y).**
    the staged set is **exactly the merged corpus — ALL of `content/levels/*.json` (L001–L005
    today)** — byte-identical file-for-file, and `unity/Assets/StreamingAssets/config/runtime_bounds.json`
    is a byte-identical copy of `config/runtime_bounds.json` (ADR-0009:33's `ci` clause, deferred by
    CM-C7's Q-Y note, becomes satisfiable HERE). The "no levels beyond L001" non-goal means no NEW
    authored levels — staging the already-merged corpus is this criterion's copy, not authoring.
    *Check:* a SET-EQUALITY assertion (filename sets equal in both directions — evaluator D8: pairwise
    identity alone is omission-blind) plus per-file byte-identity, in a discovered wrapper that FAILS
    on drift in either direction; the CI wiring of ADR-0009:33 itself stays Q-V (human `.github/**`).
    **Deviation recorded (evaluator D9):** ADR-0008:57-62's `ContentSync` editor tooling (the
    prevention half) needs a `CatMetro.Editor` assembly that does not exist; this criterion ships the
    copies + the drift GATE, and `ContentSync` is the named follow-up riding the first CatMetro.Editor
    contract.
11. **RK-17 backup posture — implement the human's decision, or stop loudly (recut).** ADR-0006
    §Open conflict (`:291-333`) leaves the posture open and `docs/prd/risks.md:80` (quoted at
    ADR-0006:394-396) requires it to land WITH the save format; the save format merged (#16), so
    **RK-17 is now PAST DUE — a human decision is required during this contract's window.** If decided:
    the manifest/backup-rules artifact implements it, and `analytics_queue.dat` + its transient `.tmp`
    are excluded UNCONDITIONALLY (ADR-0006 §5; Q-U/M-21's satisfied-by-exclusion deviation in
    `state/handoffs/CM-C8.md`). If still undecided at build time: criterion 7 ships the SDK-version
    assertions only, and the PR names RK-17 as the open release-gate blocker — never a silent default.
    *Check (conditional — evaluator D6):* **decided branch** — the merged-manifest assertion covers
    the chosen posture AND a grep asserts the queue file (+ its transient `.tmp`) is named in the
    committed backup-rules XML. **Undecided branch** — a grep asserts NO `android:allowBackup`
    attribute and NO backup-rules XML is committed anywhere, and the PR carries the named-blocker
    note **stating plainly that criterion 8's device session then runs under Android's platform
    default (backup-ON) — RK-17's exploit posture — as a knowingly-accepted, PR-named exposure
    (evaluator D7). The human decides RK-17 either before the device session or by accepting that
    named exposure; an agent never picks the posture.**

### Scope boundary

**In scope:** the paths in the ownership table for CM-C2b, plus the PlayMode/EditMode harness
wrapper(s) under `tests/unity/` (e.g. `tests/unity/editmode.test.sh`, ADR-0005:93) — **except**
`tests/unity/failure.test.sh`, which is CM-C3's — plus registration-only appends.

**Explicit non-goals:**
- **No polish, no art pass, no audio, no haptics, no VFX** — greybox primitives only.
- **No fail/retry loop, no cause camera, no next-wave preview HUD, no results chrome** — CM-C3.
- **No scoring/chain/star UI** (pin NEW-Q5). **No solver, no validator** (CM-C4/C5).
- **No levels beyond L001**, no daily, no Night Harbor.
- **No SDK, no commerce, no ads, no analytics-taxonomy work** — `**/billing/**`, `**/iap/**`, `**/ads/**`
  are monetization tripwires requiring `state/mode=production` first (AGENTS.md; `state/PROJECT_STATE.md:10`).
  (Recut note: constructing CM-C7's `SaveStore`/CM-C8's queue against Bootstrap seams is criterion 9's
  wiring, not new save/analytics behaviour — neither type gains a line of code here.)
- **No edits to CM-C1's Domain sources or CM-C2a's importer**; **no `Compile Include` append**.
- **No daily DEVICE limbs (recut):** the 250 ms on-device salt loop, ≤200 ms boot validation, the
  30-board backup pool (CM-R46.3/.4) **and CM-C6 criterion 7's handed-off two-device same-seed check
  (roadmap D12; evaluator D10)** stay OUT — all gated on **Q-S** (no board generator exists; the
  shipped `IBoardFactory` stub is fixed-board by design) plus a backup-pool content contract and a
  two-device session. Recut them when Q-S lands; do not fake them against the stub.
- **No writes to immutable paths** (AGENTS.md hard rule 1). **No schema change.**

### Assumptions

- **A-C2b-1** SATISFIED at recut: #19's scaffold matches Q-G exactly (verified in the PR evidence);
  the stop now guards regressions only.
- **A-C2b-2** `IContentSource` (declared by CM-C2a) is implemented in `CatMetro.Bootstrap`
  (ADR-0003:71-74; overview.md:211). CM-C2b implements only what L001 loading needs.
- **A-C2b-3** The greybox uses colour **plus symbol** placeholders from the start, because colour-alone
  encoding is a merge-gate failure later (CM-R21.1); full triple-coding art is out of scope.

### Stop conditions

Defaults apply. Plus:
1. The Unity scaffold is missing, differs from Q-G, or removed/moved `unity/Assets/Scripts/Domain/**`
   or `.../Content/**` → stop.
2. Any Domain or importer behaviour change is needed to make a render or win/fail criterion pass →
   stop; that is a CM-C1/CM-C2a amendment and re-opens the golden.
3. A criterion cannot be met without touching a monetization path or an SDK → stop.
4. The 60 fps criterion cannot be evidenced because no device is available → stop and hand criterion 8
   to the human as explicitly open; do **not** mark it met from an editor measurement.

---

# CONTRACT CM-C3 — Fail/retry loop: cause-first camera, sub-1s retry, next-wave preview

**Roadmap:** D4, `docs/plan/data/roadmap_56_days.csv:5`.
**DEPENDS-ON: CM-C2b merged** *(changed by the tranche-2 split — it was "CM-C2 merged"; the render,
input and `FrameLog.cs` deliverables CM-C3 measures against all live in the CM-C2b half).*
**Recut 2026-08-04: Q-G resolved (#19); reviewed against everything landed through CM-C8 — the
contract stands as written. Notes: A-C3-3's motion source is unchanged (no settings screen exists;
read the toggle stub + `ANIMATOR_DURATION_SCALE`); `ui.csv` ownership split (C2b creates, C3 appends)
unchanged; criteria 2/4/7's device legs remain HUMAN-VERIFIED with artifacts. TG disposition
(evaluator D11): greybox ships no palette, so TG-1 (board readability gates) does not bite until the
art pass; TG-3/6/7/8 are post-greybox; TG-2/4/5 already appear as CM-C3 non-goals/stop conditions.**

### Goal

A failed run reframes the board on the node that caused the failure, a single tap returns the player to
`Playing` in under a second without a scene load, and the HUD shows the next two waves before they
arrive.

### Spec reference

`docs/prd/PRD.md` CM-R03.2 · CM-R15 (**D4 subset only**) · CM-R16.1–.3 · CM-R17.1 · CM-R22.3 ·
`docs/prd/ux-flows.md:43,188,254,258-270,287,290` · `docs/adr/0002-*` §9 (retry re-simulates from
tick 0; no snapshot format) · `docs/adr/0007-*` (screen stack, **not** scene loads; motion/haptics).

### Acceptance criteria (11) — unchanged from tranche 1

1. **Cause camera targets the failing node.** On `Failed(reason)` the camera's target equals the node
   id that raised the failure, asserted from the camera controller state. *Check:* one PlayMode test
   per reason — `QueueOverflow` and `TimeOut` driven by a **real Domain run** to the fail tick;
   `PlatformOverflow` **not raisable while Q-J/NEW-Q4 are open** (`Outcomes.cs:40-42`), so driven by a
   **constructed presentation-level outcome**, asserting framing only, with the PR recording that no
   Domain run reaches this state. **That constructed outcome type is test-only and lives under
   `unity/Assets/Tests/**`** — it may never become a shipped parallel outcome type carrying
   `PlatformOverflow` through Presentation, which is exactly the semantics the pin exists to keep out
   of the tree. *Also checked:* a `[CI]` grep asserting **no** type under
   `unity/Assets/Scripts/Presentation/**` or `unity/Assets/Scripts/Application/**` constructs a
   `FailReason.PlatformOverflow` value, with a negative fixture proving the grep fires. Criterion 10's
   `PlatformOverflow` string case uses **the same** test-only type and is bound by the same grep.
   For `TimeOut` the target is **the node with the largest queue at the
   fail tick, ties broken by the lowest node id** — **analyst-authored (A-C3-2), unratified, Q-K**.
2. **Cause visible within 1.5 s.** Time from the fail tick to the frame in which the causal node is
   framed **and** the fail banner is rendered is **≤1500 ms**, p95 over 20 scripted failures
   (`roadmap_56_days.csv:5`), measured from CM-C2b criterion 3's frame log (single named clock source).
   Two legs: **CI gate** = the editor PlayMode measurement with the raw per-failure table attached;
   **HUMAN-VERIFIED** = the same protocol on a **low-tier and a mid-tier device**
   (`docs/prd/ux-flows.md:287`), same artifact requirement. The criterion **fails if the artifact is
   absent**. An editor-only measurement never satisfies it (stop condition 7).
3. **Motion-off is a cut plus a static ring.** With the motion toggle OFF **or**
   `Settings.Global.ANIMATOR_DURATION_SCALE == 0` (two independent cases), the camera reaches its final
   transform in **one frame** and a **static** ring renders on the causal node with alpha > 0 and zero
   animation clips playing (`docs/prd/ux-flows.md:43,254,290`). *Check:* two PlayMode tests.
4. **Motion-on pans and still meets the budget.** With motion on the camera interpolates (>1 frame) and
   criterion 2's budget holds under **both** legs. *Check:* one PlayMode test + the device artifact.
5. **No information is lost at motion-off.** Banner text, causal-node framing and ring are present in
   both motion states; the rendered information set is identical across them (ring vs pulse the only
   difference) (`docs/prd/ux-flows.md:290`). *Check:* one parameterised PlayMode test.
6. **Retry is one input, live from frame 1.** `Try again` is hit-testable on the **first** frame of
   FailureReview (`docs/prd/ux-flows.md:265`; CM-R16.2). *Check:* one PlayMode hit-test on frame 1.
7. **Retry under 1 s, measured.** Tap-down → first frame in `Playing` is **<1000 ms**, p95 over 20
   retries, from CM-C2b's frame log on the editor target, raw table attached (CM-R16.1). The
   **low/mid-tier device** repetition is **HUMAN-VERIFIED** with the same artifact requirement; the
   criterion fails if the artifact is absent.
8. **No scene reload on retry.** Scene load/unload count across a retry is **0** and the scene handle
   is unchanged (ADR-0007 §Navigation). *Check:* one PlayMode test on the load-counter delta.
9. **Retry restores tick-0 state and stays deterministic.** After retry every switch equals its level
   `initialRoute` (`LevelGraph.cs:32`; `SimulationState.cs:65`), the command log is empty,
   `state.Tick == 0`, and replaying the identical post-retry command sequence produces the identical
   replay hash as the same sequence from a fresh level entry (CM-R16.3; ADR-0002 §9 + CM-R01).
   *Check:* one PlayMode test + one `EditMode/Pure` hash-equality test. **If the two hashes differ,
   that is stop condition 7 — stop and report; never touch `tests/contract/`.**
10. **Fail strings render with substitution.** Each fail reason renders its LOCKED string with the
    node/station name substituted — `"Platform overflowed at {node}"` / `"{station} platform
    overflowed"` / `"The last train left the depot"` — read from
    `unity/Assets/Resources/Strings/ui.csv` (created by CM-C2b; CM-C3 **appends rows only**), with
    **zero literal strings in UI components** (CM-R03.2; `docs/prd/ux-flows.md:188`).
    *Check:* one test per reason (3) + a grep assertion. The `PlatformOverflow` case is driven by a
    constructed presentation-level outcome (Q-J).
11. **Next-wave preview HUD.** At tick 0 the strip displays the **next two waves'** colour and count,
    contains **zero** interactive elements, sits in the top 0–15% band, and updates as waves are
    consumed (CM-R17.1; `docs/prd/ux-flows.md:184`; CM-R07.4). *Check:* four assertions in one PlayMode
    test.

### Scope boundary

**In scope:** the paths in the ownership table for CM-C3, including its **own** wrapper
`tests/unity/failure.test.sh` and **append-only rows** in `ui.csv`.

**Explicit non-goals:** no rewind sheet/chip/eligibility (CM-R08) — and the invariant CM-C3 must not
break: on attempt 1 **no** paywall/ad surface may even be constructed (`docs/prd/PRD.md:208`); no
monetization of any kind; no ghost replay, blame chip, A-23 ambiguity predicate or
`ATTRIBUTION_MAX_RESIMS` re-simulation; no results-screen rollup (UX-OPEN-03 / TG-4); no scoring, stars
or tickets (pins NEW-Q5, NEW-Q7); no settings screen (reads motion state only); no planning pause
(TG-2); no edits to CM-C1 Domain sources, CM-C2a's importer or CM-C2b's board code; no `Compile
Include` append; no edit to an existing `ui.csv` row; no writes to immutable paths.

### Assumptions

- **A-C3-1** The Domain's failure outcome carries the failing node id. The shipped `SimOutcome`
  (`Outcomes.cs:22-45`) carries **`Kind` + `Reason` only — no node id.** *This is now a confirmed
  finding, not an open assumption:* criterion 1 must derive the causal node from the **state at the
  fail tick** (the node whose `OverloadTimers[n]` reached 0, `Simulation.cs:142-145`), not from the
  outcome. If that derivation is judged insufficient, it is a CM-C1 amendment → stop condition 1.
- **A-C3-2** `TimeOut` has no single causing node; criterion 1's rule is **analyst-authored and
  unratified — Q-K**. Overruling costs `Presentation/Camera/**` only.
- **A-C3-3** The motion state source is `(Settings motion toggle) OR (ANIMATOR_DURATION_SCALE == 0)`
  (`docs/prd/ux-flows.md:43`, PC-14). CM-C3 introduces no save field.
- **A-C3-5** "Instant retry" is re-entry to `Playing` by re-simulation from tick 0 (ADR-0002 §9), not a
  snapshot restore — no snapshot format exists and none may be created.
- **A-C3-6** The frame log is **CM-C2b's deliverable** (criterion 3). If absent or lacking
  `monotonicMs`/`simTick`, CM-C3 stops (stop condition 8) rather than writing a second clock source.

### Stop conditions

Defaults apply. Plus:
1. Criterion 1 requires the Domain to report a node id it does not report (**it does not — A-C3-1**) →
   stop before changing the Domain; the derivation-from-state route is the sanctioned path.
2. Any criterion appears to require ghost replay, blame chip or the ambiguity predicate → stop.
3. Any commerce/ad surface, placement fetch or entitlement check appears in the fail path → stop
   immediately (CM-R08.1 invariant + monetization tripwire).
4. The <1 s retry cannot be met without a scene load or a snapshot format → stop and report the
   measurement; do not weaken criteria 7 or 8.
5. TG-5 or TG-4 must be resolved to render a required string or CTA → stop and ask.
6. Motion-off behaviour would remove information (not just easing) to hit a budget → stop.
7. The post-retry replay hash differs from the fresh-entry hash → **stop and report**; never touch
   `tests/contract/` — a mismatch is evidence of a retry-path defect, not a stale golden.
8. **No device available to evidence criteria 2, 4 or 7** → hand those to the human as explicitly open;
   never mark a device-dependent budget met from an editor measurement. Likewise if CM-C2b's frame log
   is missing or single-clock-source cannot be shown.

---

# CONTRACT CM-C4 — Solver: BFS for ≤2-switch boards, beam search beyond, sharing the one `Step`

**Roadmap:** D8 (`docs/plan/data/roadmap_56_days.csv:10` — "BFS solver for <=2-switch boards sharing the
exact Domain step function (no parallel sim); Editor solver runner"; acceptance "Solver proves L1-L8
solvable and reports min-switch counts") + D9 beam legs (`:11` — "Beam search widths 1k/2.5k/5k").
**DEPENDS-ON:** CM-C1 (merged) **only**. **Blocked on:** nothing. **HYBRID-ELIGIBLE** (see the lane
section above).

### Goal

A pure-C#, engine-free, clock-free, float-free search over `LevelGraph` that calls the **one** shipped
`Simulation.Step` symbol and returns, per board: solvable yes/no, the optimal command log, the
solver-optimal completion tick count, and the raw integer inputs the difficulty model consumes — with
every result reproducible byte-for-byte across processes.

### Spec reference

`docs/prd/PRD.md` CM-R02.1 (`:111` — one step symbol, build-time duplicate check) · CM-R12.2 (the
zero-input baseline stage 5 consumes) · CM-R19.1 (`:355-358` — solver-optimal completion time, **`[PIN
NEW-Q1]`**) · CM-R04.2 (star reachability, consumed by CM-C5) ·
`docs/adr/0002-deterministic-fixed-tick-domain.md` §2 (`:33-36` "There is exactly one implementation;
the solver, the batch validator and the runtime call this symbol"), §3 (integer only), §5 (no clock) ·
`docs/adr/0008-content-pipeline-and-level-schema.md:114-117` ("BFS for ≤2-switch boards and beam search
at widths 1k/2.5k/5k beyond, **sharing the exact Domain step function**"; human witness replay
admissible where beam search fails) · `docs/adr/0005-...:112` (solver runs in the dotnet leg) ·
`docs/plan/specs/product_spec.md:640` (stage 4 wording) · `docs/architecture/overview.md:323-326` ·
`unity/Assets/Scripts/Domain/{Simulation,LevelGraph,SimulationState,Commands,Outcomes}.cs` (the shipped
surface — **read before coding; do not invent a member**).

### Acceptance criteria (13)

1. **Placement and purity.** Solver sources live under `unity/Assets/Scripts/Domain/Solver/**` and are
   compiled by the **existing** `dotnet/CatMetro.Domain/CatMetro.Domain.csproj:17` glob with **zero
   csproj edits**; `bash scripts/check.sh` exits 0, i.e. the solver contains no `float`/`double`/
   `decimal`/`DateTime`/`Stopwatch`/`System.Random`/`UnityEngine`/`System.Numerics`
   (`scripts/check.sh:41,61`). *Check:* (a) `git diff --name-only` shows no change under `dotnet/`;
   (b) `dotnet build dotnet/CatMetro.sln -c Release` exits 0; (c) `bash scripts/check.sh` exits 0.
   **Placement is Q-M and is unratified** — see assumption A-C4-1.
2. **One step symbol, enforced.** A `[CI]` check asserts (a) the tree contains **exactly one**
   definition matching `static void Step(ref SimulationState`, and (b) **zero** occurrences under
   `unity/Assets/Scripts/Domain/Solver/**` of any tick-advancing write (`\.Tick\s*(=|\+\+)`,
   `Deliveries\s*(=|\+\+)`, `OverloadTimers\[`) — the solver may only reach state through `Step`
   (CM-R02.1, `docs/prd/PRD.md:111`; ADR-0002 §2). *Check:* two grep assertions appended to
   `scripts/check.sh` (registration-exception class), each with a negative fixture proving it fires.
3. **BFS is exact for ≤2-switch boards.** For a board with `SwitchRoutes.Length ≤ 2`, the search is a
   breadth-first enumeration over command sequences that returns a **provably minimal-completion-tick**
   winning log, or `Unsolvable` after exhausting the reachable space within `TimeLimitTicks`.
   *Check:* three NUnit cases — a 1-switch board (L001-shaped: 4 nodes / 3 edges / 1 switch / 2 waves,
   `example_levels.json:4-17`) where BFS's answer equals a brute-force enumeration written separately
   in the test; a 2-switch board; an unsolvable 1-switch board asserting `Unsolvable`.
4. **Beam search beyond 2 switches, at the three authored widths.** For `SwitchRoutes.Length > 2` the
   search runs beam widths **1000 → 2500 → 5000** in ascending order, stopping at the first width that
   finds a win, and reports the width that succeeded (ADR-0008:116; `product_spec.md:640`). A board
   unsolved at 5000 returns `NotFound(beam, 5000)` — **explicitly not `Unsolvable`**, because
   ADR-0008:117 admits a human witness replay as proof where beam search fails.
   *Check:* three NUnit cases — a 3-switch board solved at width 1000 (asserting the reported width);
   a synthetic board contrived to need a wider beam (asserting escalation occurred); a board that
   returns `NotFound(beam, 5000)` and asserts the discriminant is **not** `Unsolvable`.
5. **Pinned branches are pruned and counted, never mistaken for unsolvability (Q-N).** When
   **`Simulation.Step` or `SimOutcome.MakeFailed`** throws `NotSupportedException`
   (`Simulation.cs:116`, `Outcomes.cs:40`), the search prunes that successor as `PinnedUnreachable`,
   increments a counter, and — if **no** win was found **and** the counter is > 0 — returns
   `Indeterminate(pinned, count, firstPinMessage)` rather than `Unsolvable`.
   **`LevelGraph` construction is deliberately *not* in that list:** its pin guards fire in the
   constructor (`LevelGraph.cs:64,68`) and the graph is a single input built **once, before** the
   search, so a wild-colour or second-source board never reaches the solver and there is nothing to
   prune. **A `LevelGraph` that cannot be constructed is CM-C2a criterion 10 / CM-C5 stage 1's
   failure, not CM-C4's.**
   *Check:* three NUnit cases — (a) a 3-colour/3-station board (the `stress_boards.json:5-42` L701
   shape) where a wrong route triggers the rejection pin: asserts the run completes, `count > 0`, and
   the verdict is a win or `Indeterminate`, **never** an escaped exception; (b) a board with a solution
   asserts the win is still found despite pruned branches; (c) an unsolvable board with `count == 0`
   asserts `Unsolvable`.
6. **The result record, fully populated.** A `SolveResult` carries exactly: `Verdict`
   (`Solved | Unsolvable | NotFound | Indeterminate`), `CommandLog OptimalLog`,
   `int CompletionTicks`, `int SwitchesUsed`,
   `int BeamWidthUsed` (0 for BFS), `int PinnedPruned`, `int NodesExpanded`, and a
   `DifficultyProxy { int MaxSimultaneousPendingDecisions, int SolverOptimalTicks, int TimeLimitTicks,
   int MinQueueSlackAtPeak, int SinglePerturbationsWinnable, int SinglePerturbationsTried }` — the
   **integer** inputs for axes **C, T, H, R** of `product_spec.md:508-511`. The weighted float score is
   **not** computed here (it is CM-C5's, outside the float ban).
   **`CompletionTicks` is defined by an equation, not by prose**, because `Simulation.cs:155-162`
   sets `Won` **after** incrementing `state.Tick`:
   **`CompletionTicks == ReplayHasher.RunToEnd(graph, seed, log).Tick - 1`** — the tick *during* which
   the winning delivery landed. Criterion 9 asserts that identity directly, which is what removes the
   one-tick ambiguity from the hand-computed L001 value in the check below.
   **Axis H is recorded as partial, not silently narrowed:** `product_spec.md:510` defines H as
   `min(queue+platform slack)` during the solver trace's peak load, but the
   **platform-slack term is unreachable while Q-J/NEW-Q4 are open**
   (`Outcomes.cs:40-42` — nothing ever raises `PlatformOverflow`, so no platform slack is observable).
   `MinQueueSlackAtPeak` is therefore **the queue term only**, named accordingly, and **CM-C5 stage 8
   prints H as `PARTIAL(Q-J)`**. Dropping the term is defensible; dropping it *unrecorded* was the
   defect. *Check:* one NUnit case asserting each field is populated for L001; one asserting
   `CompletionTicks` equals a hand-computed value for the L001 optimal log; one asserting the
   `RunToEnd(...).Tick - 1` identity.
7. **Optimality is defined and deterministic (Q-W).** Optimal = **minimal `CompletionTicks`**; ties
   broken by **fewer commands**, then by **lexicographic order over the `(Tick, SwitchId)` pairs** of
   the log. Two runs over the same `(LevelGraph, seed, width)` produce **byte-identical** command logs.
   *Check:* one NUnit case constructing a board with two equal-tick solutions and asserting the
   tie-break picks the specified one; one case asserting log equality across two in-process runs; one
   asserting equality across **two separate `dotnet test` process invocations** (the CM-C1 criterion 11
   emission pattern: exactly one stdout line `SOLVER_LOG=<hex>` per run, diffed by the wrapper).
8. **Scoring stays pinned out (Q-C).** The search optimises **deliveries and time only**.
   `SolveResult` carries **no score, no star, no chain and no ticket field**, and a `[CI]` grep asserts
   zero reads of `\.Score\b` or `\.Chain\b` under `unity/Assets/Scripts/Domain/Solver/**` (pins NEW-Q5,
   NEW-Q7; `SimulationState.cs:31-32` keeps both at 0). *Check:* one reflection case over
   `SolveResult`'s members + one grep assertion with a negative fixture.
9. **Determinism against the shipped hasher.** Replaying `SolveResult.OptimalLog` through
   `ReplayHasher.ComputeReplayHash(graph, seed, log)` (`ReplayHasher.cs:13`) twice yields the identical
   64-lowercase-hex string, and running the log through `ReplayHasher.RunToEnd` (`:30`) yields a state
   with `Outcome.Kind == Won` and **`state.Tick - 1 == CompletionTicks`** — the criterion-6 identity,
   asserted as an equation. It is **not** `state.Tick == CompletionTicks`: `Simulation.cs:155-162`
   increments `state.Tick` and *then* sets `Won`, so `RunToEnd` returns a state one tick past the
   completing tick. *Check:* two NUnit cases (hash equality; the `Won` + `Tick - 1` equation).
   **The solver never writes or compares against `tests/contract/replay-hash-golden.json`.**
10. **Zero-input baseline (CM-R12.2's input).** `SolveResult` for the **empty** command log is
    computable independently and reports whether the board wins with no input.
    *Check:* two NUnit cases — L001 with an empty log does **not** win
    (`example_levels.json:13` `initialRoute: 1` is deliberately wrong); a contrived
    already-correct board does win, proving the check has both limbs.
11. **Work is bounded by a number, not a clock.** `SolverBounds.MAX_NODES_EXPANDED` caps total
    expansions; exceeding it returns `NotFound(budget)` with `NodesExpanded` reported. **No wall-clock
    read exists** (`Stopwatch`/`DateTime` are banned under the Domain root, `scripts/check.sh:41`), so
    the bound is expansion count, not milliseconds. The constant is declared in one place with its
    derivation in a comment. **The search takes an optional expansion-budget parameter defaulting to
    `SolverBounds.MAX_NODES_EXPANDED`; the test passes a low value** — a `const` cannot be set from a
    test, so without that parameter the check below is unrunnable. *Check:* one NUnit case passing a
    low budget and asserting `NotFound(budget)` and a non-zero `NodesExpanded`; one asserting the
    default-argument value **is** `SolverBounds.MAX_NODES_EXPANDED` (so the injection point cannot
    drift from the declared constant); one grep asserting no clock symbol under the solver root.
12. **Harness discovery.** `tests/solver/solver.test.sh` exits 0 iff `dotnet test` is green and
    performs the criterion-7(c) two-process diff; `bash scripts/test.sh` prints
    `PASS tests/solver/solver.test.sh` and a summary line matching `^test: [0-9]+/[0-9]+ passed`
    **whose two numbers the wrapper compares equal** (`scripts/test.sh:13,24`; the backreference form
    `\1` is not POSIX ERE — see CM-C2a criterion 13).
    *Check:* `bash scripts/test.sh` exits 0 with both lines and the numeric comparison passing.
13. **The solver is not reachable from the runtime (CM-R01.6 guard).** A `[CI]` grep asserts zero
    references to any `CatMetro.Domain.Solver` type from `unity/Assets/Scripts/Application/**`,
    `.../Presentation/**` or `.../Bootstrap/**`. Those trees are empty today, so this passes trivially
    now and is a **standing guard** for CM-C2b and later — which is exactly its purpose, since
    placement inside `CatMetro.Domain` removes the assembly boundary that would otherwise enforce it
    (Q-M cost (c)). *Check:* one grep assertion with a negative fixture.

### Scope boundary

**In scope:** `unity/Assets/Scripts/Domain/Solver/**`, `unity/Assets/Tests/EditMode/Pure/Solver/**`,
`tests/solver/**`, plus registration-only appends to `scripts/check.sh` (new grep blocks only).

**Explicit non-goals:**
- **No edit to any file under `unity/Assets/Scripts/Domain/` outside `Solver/`** — the shipped `Step`,
  `LevelGraph`, `SimulationState`, `Pcg32`, `ReplayHasher` are frozen. Needing one is stop condition 1.
- **No csproj edit, no new dotnet project, no new assembly, no asmdef** (Q-M is a ratification, not a
  build change).
- **No JSON, no level parsing, no `content/` read** — the solver consumes `LevelGraph` only; test
  fixtures are constructed in code (the A-C1-2 pattern).
- **No validator stages, no difficulty score, no star check, no novelty, no staleness** — CM-C5.
- **No daily generation, no seeds beyond the `ulong` a caller passes** — CM-C6.
- **No scoring, chain, stars, tickets** (pins NEW-Q5, NEW-Q7). **No wildcard, no second source, no
  rejection semantics** (pins NEW-Q35, NEW-Q4 — the guards stay guards).
- **No Unity, no Editor menu item, no `CatMetro.Editor`** (Q-M; a runner UI is a later contract).
- **No path matching `**/billing/**`, `**/iap/**` or `**/ads/**`**; any such need is a **stop
  condition** requiring `state/mode=production` first (AGENTS.md §Risky paths;
  `state/PROJECT_STATE.md:10`).
- **No writes to `tests/contract/`, `docs/constitution.md`, `.claude/hooks/`, `scripts/git-hooks/`,
  `state/mode`, `evals/`** (AGENTS.md hard rule 1).

### Assumptions

- **A-C4-1 — placement is analyst-decided and unratified (Q-M).** `unity/Assets/Scripts/Domain/Solver/**`
  is chosen because ADR-0005:112 and ADR-0009:35 make the `CatMetro.Editor` placement at
  `overview.md:56` / ADR-0003:44 impossible. If the architect rules otherwise, the fix is a folder move
  plus one csproj — **no criterion above changes meaning.**
- **A-C4-2 — pinned throws are pruned, not propagated (Q-N).** Catching `NotSupportedException` per
  successor is the only route that does not duplicate step logic (forbidden by ADR-0002 §2) or change
  the Domain (golden-invalidating). Its cost is exception-handling overhead in the hot loop; criterion
  11's expansion cap bounds the damage. If measured cost is unacceptable, that is an ADR-0002
  amendment, not a task decision → stop condition 4.
- **A-C4-3 — the optimality tie-break is analyst-authored (Q-W).** Nothing in the corpus defines it.
- **A-C4-4 — the difficulty proxy fields are the *integer inputs*, not the axes.**
  `product_spec.md:508-511` defines C/T/H/R in terms the solver can count; the normalisation and the
  weighted sum need real arithmetic and live in CM-C5, outside the Domain float ban.
  **Axis H is partial and it is recorded, not assumed away:** `product_spec.md:510` asks for
  `min(queue+platform slack)`, `MinQueueSlackAtPeak` supplies **only the queue term**, and the platform
  term stays unreachable while `PlatformOverflow` is never raised (`Outcomes.cs:40-42`; **Q-J**,
  NEW-Q4). CM-C5 stage 8 prints H as `PARTIAL(Q-J)` so no one reads it as the spec's H.
- **A-C4-5 — `SolverBounds.MAX_NODES_EXPANDED` is analyst-authored.** No source names a work bound for
  the solver (ADR-0006 §4 bounds attribution, not search). It is declared with its derivation and
  raised/lowered by ordinary amendment; unlike the beam widths, **it is not a corpus number** and is
  flagged as such in the PR.

### Stop conditions

Defaults always apply. Plus:
1. Any criterion appears to need an edit to a shipped Domain file outside `Solver/` → **stop**; that
   re-opens `tests/contract/replay-hash-golden.json`, which is human-only.
2. Any temptation to write a second step/tick implementation, "just for the search" → **stop**; that is
   the single thing ADR-0002 §2 and CM-R02.1 exist to prevent.
3. A board cannot be searched without deciding **NEW-Q4, NEW-Q5 or NEW-Q35** → stop (Q-N is the
   sanctioned handling; inventing rejection or wildcard semantics is not).
4. Exception-driven pruning proves unaffordable and a non-throwing Domain probe looks necessary → stop
   and report with the measurement (Q-N option (ii) is an ADR-0002 amendment).
5. `float`/`double` appears necessary for a beam score or a difficulty proxy → **stop**; the Domain ban
   is ADR-0002 §3 and the float-shaped work belongs to CM-C5.
6. Any need to read a level JSON file, a clock, `config/`, or `content/` → stop; those are CM-C2a's,
   CM-C5's and CM-C6's inputs, passed in as arguments.
7. Any need to create a new csproj/assembly/asmdef → stop and escalate Q-M; assembly names are
   irreversible (ADR-0003 §Locked in).

---

# CONTRACT CM-C5 — 11-stage level validator + the licence-free `validate-content` leg

**Roadmap:** D9 (`docs/plan/data/roadmap_56_days.csv:11` — "batch validator CLI; GitHub Action runs
level validation on every content PR"; acceptance "CI fails a deliberately broken level and passes
L1-L12").
**DEPENDS-ON:** **CM-C2a merged** (parsed DTOs + `LevelGraph` mapping) **and CM-C4 merged** (stages 3,
4, 5, 6, 7, 8 all consume solver output).

### Goal

A pure-C# batch validator implementing all 11 CM-R12 stages over `content/levels/**` and
`docs/plan/data/stress_boards.json`, runnable as a **credential-free, licence-free** job
(`scripts/validate-content.sh`) plus a fast-leg wrapper, which prints a per-level per-stage verdict
table and exits non-zero iff a **blocking** stage fails.

### Spec reference

`docs/prd/PRD.md` CM-R12.1–.6 (`:264-274`, including the AMD-09 note at `:274`) · CM-R06.1/.2
(mechanic set + one-new-mechanic ordering, `:184-185`) · CM-R07.6 (junction spacing ≥1.2, `:200`) ·
CM-R09.2 (difficulty ±0.05, `:225`) · CM-R04.2 (3★ solver-reachable, `:151`) · CM-R19.1
(solver-optimal time, **`[PIN NEW-Q1]`**, `:355-358`) ·
`docs/plan/specs/product_spec.md:637-647` (**the 11 stages verbatim**) and `:504-515` (the B/E/C/T/H/R
axes and weights) · `docs/adr/0008-content-pipeline-and-level-schema.md:109-123` (validation as a merge
gate; `meta.validatedAt` handling) · `docs/adr/0009-ci-topology-and-secret-custody.md:35`
(`validate-content` job: **no credentials**, 10 automated stages) · `docs/adr/0005-...:112,128-129`
(why the validator must not be a licensed Unity job).

### Acceptance criteria (17)

1. **The stage inventory is exactly 11 and matches the source.** A `Stage` enumeration declares
   exactly the 11 stages of `product_spec.md:637-647`, in order and with those names:
   `Schema, StaticAnalysis, LowerBoundFeasibility, Solver, TrivialityReject, BrittlenessAccessibility,
   StarCheck, DifficultyCheck, NoveltyCheck, Staleness, HumanPlaytest`. A contract test fails if a
   member is added, removed or reordered (the same shape as CM-C1's `FailReason` enum test).
   *Check:* one NUnit case asserting the member list and ordinals.
2. **Stage 1 — Schema.** Every level validates against `docs/plan/data/level_schema.json` including the
   `^L[0-9]{3}$` id pattern (`:10`), `schemaVersion const 2` (`:9`), the `band` enum (`:18`), the
   `mechanics` enum (`:20`), `additionalProperties: false` at every level, and every `required` list.
   *Check:* one NUnit case per rule over a matching malformed fixture (≥6 cases) + one asserting L001
   passes.
3. **Stage 2 — Static analysis.** Fails a level when: a station is unreachable from a source able to
   emit its colours; an orphan switch exists (a switch whose node has no inbound edge, or whose routes
   are not all outbound edges of its node); two junction centres are <1.2 grid units apart
   (`product_spec.md:638`; CM-R07.6). **Warns** (does not fail) when a switch sits in the top 15% of
   the board (`product_spec.md:638`). *Check:* three failing fixtures + one warning fixture asserting
   verdict `WARN` and a zero exit contribution.
4. **Stage 3 — Lower-bound feasibility.** Computes `minTravelTicks × requiredDeliveries` and compares
   against `win.timeLimitTicks` **with slack read from `config/validator_thresholds.json`**. The
   computed value is always printed. **The slack number is absent from the corpus (Q-R)** → with no
   threshold row the verdict is `UNCONFIGURED` (criterion 13). *Check:* one case asserting the computed
   lower bound for L001 equals a hand-derived value; one asserting `UNCONFIGURED` with no row; one
   asserting a fail with a row present and a violating fixture.
5. **Stage 4 — Solver.** Calls CM-C4 and fails a level that is `Unsolvable`. `NotFound(beam, 5000)` and
   `Indeterminate(pinned, …)` are **non-blocking** verdicts printed with their counts — the first
   because ADR-0008:117 admits a human witness replay, the second because of **Q-N**.
   *Check:* three cases, one per verdict, asserting blocking/non-blocking behaviour.
6. **Stage 5 — Triviality reject.** A zero-input run must **not** win, on any level including L001
   (CM-R12.2, `docs/prd/PRD.md:269`; `product_spec.md:641`). *Check:* one case asserting L001 fails a
   zero-input run (so the stage passes) + one asserting a contrived always-winning board **fails the
   stage**.
7. **Stage 6 — Brittleness / accessibility.** Applies **±1-tick jitter** to a winning command log over
   a fixed, seeded perturbation set and requires **≥70%** win retention; fails any level whose only
   solutions require action windows below `meta.minActionWindowTicks`; asserts onboarding-band levels
   use **12–16** ticks (CM-R12.3, `docs/prd/PRD.md:270`; `product_spec.md:642`; `level_schema.json:23`).
   The jitter set is derived from `Pcg32` seeded by the level `seed`, so the stage is deterministic.
   *Check:* three cases — a robust fixture ≥70%; a brittle fixture <70% failing; an onboarding level
   outside 12–16 failing.
8. **Stage 7 — Star check.** Fails any level whose `win.stars.three` is not reachable by the solver
   **within band slack**; also asserts the schema rule `stars.two < stars.three`, both ≥1
   (CM-R04.2/.3, `docs/prd/PRD.md:151-152`; `level_schema.json:127-131`).
   **Band slack is absent from the corpus (Q-R)** → `UNCONFIGURED` with no threshold row. Note that
   star *scores* depend on the pinned scoring model (Q-C), so the reachability limb reports
   `PINNED(NEW-Q5)` until scoring lands; the `two < three` limb blocks today. *Check:* one case per
   limb (3).
9. **Stage 8 — Difficulty check.** Computes all six axes **B, E, C, T, H, R** with the weights
   `0.20, 0.25, 0.20, 0.15, 0.15, 0.05` (`product_spec.md:504-511`), consuming CM-C4's integer
   `DifficultyProxy` for C/T/H/R, and fails a level whose computed `difficultyTarget` deviates from the
   authored value by **>0.05** (CM-R09.2). **Axis B's "normalized to band caps" names no caps (Q-R)** →
   axis B is computed and printed but its normalisation basis is `UNCONFIGURED`, which makes the whole
   stage `UNCONFIGURED` until the row exists. **Axis H prints as `PARTIAL(Q-J)`**: `product_spec.md:510`
   defines H as `min(queue+platform slack)`, but the platform term is unobservable while
   `PlatformOverflow` is never raised (`Outcomes.cs:40-42`), so CM-C4's `MinQueueSlackAtPeak` is the
   **queue term only** (CM-C4 criterion 6). The stage therefore never claims to have computed
   `product_spec.md`'s H. *Check:* one case per axis asserting the computed raw value on L001 against a
   hand-derived number (6), one asserting the weighted sum, one asserting the `UNCONFIGURED`
   propagation, one asserting axis H's printed verdict is `PARTIAL(Q-J)`.
10. **Stage 9 — Novelty check.** Computes a feature vector (board topology + wave signature) per level
    and the pairwise distance against all prior levels in play order; fails a level below the
    **threshold**. **The threshold is absent from the corpus (Q-R)** → `UNCONFIGURED`. The distance
    values are always printed. *Check:* one case asserting the distance between two deliberately
    near-identical fixtures is smaller than between two dissimilar ones; one asserting `UNCONFIGURED`.
11. **Stage 10 — Staleness.** Compares `meta.validatedAt` against the last sim/schema change; **an
    absent key is treated as stale** (ADR-0008:119-123). Because nothing stamps the key today, this
    stage would fail every level — so it **computes and prints its verdict and does not block, pending
    Q-O**, and the report says so verbatim. *Check:* three cases — absent key → `STALE`; a key older
    than the reference → `STALE`; a key newer → `FRESH`; plus one asserting a `STALE` verdict
    contributes **0** to the exit code while Q-O is open.
12. **Stage 11 — Human playtest.** Not runnable by CI (`docs/adr/0009-...:35` says 10 of 11 are
    automated). The stage emits a **checklist artifact row per level** (level id, band, capstone
    yes/no, required tester count — 3 for capstones per `product_spec.md:647`) and reports
    `HUMAN-VERIFIED (pending)`. It never blocks and never claims to have run. **Depends on D-6**
    (tester roster) — cited, not resolved. *Check:* one case asserting the artifact row set equals the
    corpus level set and that the stage's exit contribution is 0.
13. **`UNCONFIGURED` semantics are themselves tested.** A stage whose threshold row is absent from
    `config/validator_thresholds.json` prints `UNCONFIGURED(<row name>)` and contributes **0** to the
    exit code; the same stage with the row present blocks normally. *Check:* two NUnit cases per
    affected stage (3, 7, 8, 9) = 8 cases, run against a fixture config with and without each row.
    **No agent may add a value for the four Q-R rows** (stop condition 3).
14. **Corpus selection and the non-campaign carve-out (Q-P).** The validator's inputs are
    `content/levels/**` **and** `docs/plan/data/stress_boards.json` (`:3-75`, boards L701/L702).
    **Stress boards run stages 1–8 and 10, and stage 11 emits a checklist row for them.** Stage 8 is
    **included** because the boards carry an authored `difficultyTarget` (0.30 / 0.35,
    `stress_boards.json:6`) worth checking — it will report `UNCONFIGURED` while axis B's band caps are
    a Q-R row, which is a printed verdict, not a skip. Stage 11 emits a row because
    `stress_boards.json:2` requires the boards to pass the validator "**plus human playtest**".
    **Stage 9** (novelty-vs-prior-order) and the campaign-order assertions (CM-R06.2 one-new-mechanic;
    CM-R09.1's 30-level count; CM-R09.3's band table) print `SKIPPED(non-campaign)` for them, per
    `stress_boards.json:2` ("NOT campaign content: never enter the L001-L030 progression"). **The Q-P
    row states this same set; the two must not diverge** — the difference between "1–7" and "1–8" is
    load-bearing for both the exit code and the printed report. *Check:* one case asserting L701/L702
    are validated; one asserting stage 8 runs for them while stage 9 reports `SKIPPED(non-campaign)`;
    one asserting stage 11's checklist contains a row for each of L701/L702; one asserting the campaign
    count assertion is computed over `content/levels/**` only.
15. **CI entry point, credential-free, hosted by the contract's own exe.**
    `scripts/validate-content.sh` runs the batch validator by invoking
    **`dotnet run --project dotnet/CatMetro.Validator`** — the console host this contract owns, which is
    where **all** file I/O and the only on-disk `IContentSource` implementation live. It needs **no**
    Unity, no licence, no network and no secret (ADR-0009:35), and exits 0 iff every **blocking** stage
    passes on every level. **The validation logic under `unity/Assets/Scripts/Content/Validation/**`
    opens nothing**: `System.IO` is banned there by CM-C2a criterion 2's appended `check.sh` block, so
    every read arrives as bytes through `IContentSource` (ADR-0008:53-56). *Check:*
    (a) `bash scripts/validate-content.sh` exits 0 on the current corpus;
    (b) the same command against **`tests/validation/fixtures/broken-level.json`** — a CM-C5-owned
    path, because `tests/fixtures/content-bad/**` belongs to CM-C2a and writing there would be an
    out-of-scope diff under the longest-prefix resolution rule — exits non-zero naming the level and
    the failing stage; this is roadmap D9's acceptance verbatim ("CI fails a deliberately broken
    level");
    (c) `grep -rn 'secrets\.\|UnityEngine\|Unity ' scripts/validate-content.sh` returns nothing;
    (d) a `[CI]` grep asserting zero `System\.IO` matches under
    `unity/Assets/Scripts/Content/Validation/**`.
16. **Two output forms, one truth.** The validator emits (a) a human-readable per-level × per-stage
    table to stdout and (b) a machine-readable JSON report to a caller-supplied `--out <path>`
    containing, per level: id, per-stage verdict, per-stage computed value, and the CM-C4 `SolveResult`
    summary including `CompletionTicks`. **`CM-R19.1`'s 40–75 s check consumes `CompletionTicks ÷ 8`
    and is `[PIN NEW-Q1]`** — the seconds figure is computed and printed; the range comparison reports
    `PINNED(NEW-Q1)` and does not block. *Check:* one case asserting the JSON shape; one asserting the
    seconds figure for L001 equals `CompletionTicks / 8`; one asserting `PINNED(NEW-Q1)` is emitted.
17. **The gate run never writes to its own inputs (Q-O).** In gate mode the validator opens
    `content/levels/**` and `docs/plan/data/**` **read-only**; a separate, explicitly opt-in
    `--stamp` invocation is the only path that may write `meta.validatedAt`, and it writes only that
    key, preserving byte-for-byte everything else. *Check:* (a) a test asserting the gate run leaves
    every input file's SHA-256 unchanged; (b) a test asserting `--stamp` changes exactly one key and no
    other byte; (c) `git diff --name-only` on a CI-mode run shows zero content paths.
    **Both the gate read and the `--stamp` write happen in `dotnet/CatMetro.Validator`**, never in the
    `Content/Validation/**` library (criterion 15).
    Plus the fast-leg wrapper `tests/validation/validator.test.sh` discovered by `scripts/test.sh`:
    `bash scripts/test.sh` prints `PASS tests/validation/validator.test.sh` and a summary line matching
    `^test: [0-9]+/[0-9]+ passed` **whose two numbers the wrapper compares equal** (the backreference
    form `\1` is not POSIX ERE — see CM-C2a criterion 13).

### Scope boundary

**In scope:** the paths in the ownership table for CM-C5, plus registration-only appends.

**Explicit non-goals:**
- **No `.github/**` workflow file** — that is a risky path needing independent security review
  (**Q-V**; AGENTS.md; RK-37). CM-C5 delivers the script and the wrapper; wiring is a human PR.
- **No solver implementation** — CM-C4 owns it; CM-C5 calls it.
- **No level authoring** beyond the malformed fixtures it owns. **No schema change.**
- **No daily generation, no seed derivation, no salt loop** — CM-C6.
- **No value for any Q-R threshold**, no invented band caps, no invented novelty distance.
- **No `meta.validatedAt` write in gate mode**; no write anywhere under `docs/plan/**`.
- **No Unity, no `CatMetro.Editor`, no editor menu item.**
- **No `System.IO` under `unity/Assets/Scripts/Content/**`** — CM-C2a criterion 2's appended `check.sh`
  block bans it and CM-C5 may not edit that block. **All reads go through `IContentSource`**; the only
  filesystem code this contract writes lives in `dotnet/CatMetro.Validator/**` (criterion 15, Q-X).
- **No writes under `tests/fixtures/content-bad/**`** — that tree is CM-C2a's; CM-C5's own malformed
  fixtures live at `tests/validation/fixtures/**`.
- **No path matching `**/billing/**`, `**/iap/**` or `**/ads/**`**; any such need is a **stop
  condition** requiring `state/mode=production` first (AGENTS.md §Risky paths;
  `state/PROJECT_STATE.md:10`).
- **No writes to immutable paths** (AGENTS.md hard rule 1).

### Assumptions

- **A-C5-1** The validator lives in `CatMetro.Content` under `Validation/**` because ADR-0003:35 gives
  Content "schema+bounds validation" and Content may reference Domain (so it reaches CM-C4's solver);
  Content is **not** under a banned-symbol root, so the difficulty model's real arithmetic is legal
  there. If the architect places it elsewhere, Q-M's ruling applies to this contract too.
- **A-C5-2** Stage 6's jitter set is **seeded from the level `seed` via `Pcg32`**, so "≥70% win rate"
  is a deterministic figure and not a flaky one. The set size is declared in
  `config/validator_thresholds.json` as an ordinary configured row (it is a test-design number, not a
  product number).
- **A-C5-3** Stress boards run **stages 1–8 and 10** plus a stage-11 checklist row, and are excluded
  from stage 9 and the campaign-order assertions (**Q-P**, criterion 14 — the two statements are
  identical by construction).
- **A-C5-6** The console host `dotnet/CatMetro.Validator/**` is **analyst-assigned** (**Q-X**): the
  validation library may not do file I/O, so an executable must. It is a `dotnet`-leg tool exe with no
  Unity asmdef counterpart and therefore no row in ADR-0003's 13-assembly list; if the architect rules
  otherwise the remedy is a rename, and **no criterion changes meaning**.
- **A-C5-4** "the last sim/schema change" (stage 10's reference point) is taken as the most recent
  commit timestamp touching `unity/Assets/Scripts/Domain/**` or `docs/plan/data/level_schema.json`.
  **Analyst-authored** — no source defines it. Recorded in the report so a human can overrule.
- **A-C5-5** `[CI]` criteria CM-R12.1's "all 11 stages run in CI on every content PR" is satisfied by
  the script existing and being green; the *trigger* is Q-V's human PR.

### Stop conditions

Defaults apply. Plus:
1. A stage cannot be implemented without a Domain change → stop (golden-invalidating).
2. A stage needs a level-schema field that does not exist → stop (Q-F, schema frozen).
3. **Any temptation to pick a number for a Q-R row** (lower-bound slack, band slack, novelty threshold,
   axis-B band caps) → **stop and ask**; ship `UNCONFIGURED`.
4. Stage 10 appears to require stamping inside the gate run → stop and cite Q-O.
5. The corpus contains a level that cannot be classified as campaign or non-campaign → stop (Q-P).
6. A stage requires a Unity licence, a network call or a secret → stop; that breaks ADR-0009:35 and the
   entire economics of ADR-0008.
7. A stage requires resolving NEW-Q1, NEW-Q4, NEW-Q5, NEW-Q9, NEW-Q21 or D-6 → stop; report `PINNED`.

---

# CONTRACT CM-C6 — Daily-seed pre-validation pipeline (pure-C# subset of CM-R46)

**Roadmap:** D12 (`docs/plan/data/roadmap_56_days.csv:14` — "Daily Line seeded mode behind a feature
flag"; acceptance "the same daily seed produces an identical level on 2 devices").
**DEPENDS-ON:** CM-C4 merged **and** CM-C5 merged.

### Goal

A deterministic, clock-free, engine-free pipeline that, given a list of date keys, derives each date's
seed, runs the bounded salt loop, validates each resulting board through CM-C5's blocking stages, and
emits a run artifact that prints the resolved seed per date key — the truth source CM-R43.8 compares a
device against.

### Spec reference

`docs/prd/PRD.md` CM-R46.1–.3, .5 (`:727-735`) · CM-R11.1 (fixed seed vectors, `:254`) · CM-R11.7
(the `"CM-DAILY-1"` constant asserted unchanged through Sep 30, `:260`) ·
`docs/plan/specs/liveops_spec.md:22-27` (seed = lower 32 bits of `SHA-256("CM-DAILY-1|" + local ISO
dateKey + "|" + k)`), `:29-31` (local-midnight rollover; UTC explicitly rejected), `:51-56`
(the `validate-dailies` job and the salt loop), `:57` (generator version frozen) ·
`docs/adr/0009-ci-topology-and-secret-custody.md:35` (`validate-dailies` over the next 90 dates,
printing the resolved seed per dateKey) · `docs/adr/0008-...:9-15` (the three distinct quantities:
90 pre-validated dates ≠ 30-board backup pool ≠ 40 shipped levels).
> **[CONFLICT carried, not resolved]** `docs/prd/PRD.md:252` records **NEW-Q8** — `product_spec.md:447`
> gives UTC + `"CM-DAILY-"`, `liveops_spec.md:22-31` gives local dateKey + `"CM-DAILY-1|"`. CM-R11
> **adopts liveops** pending human confirmation; CM-C6 implements the adopted reading and pins the
> constant (criterion 1).

### Acceptance criteria (11)

1. **Seed derivation with fixed vectors, and the constant is pinned.** `DailySeed.Derive(dateKey, k)`
   returns the **lower 32 bits of `SHA-256("CM-DAILY-1|" + dateKey + "|" + k)`**
   (`liveops_spec.md:22-27`). Three known dateKeys produce three seed values recorded in the test
   source (CM-R11.1). A contract test asserts the literal generator constant is exactly `"CM-DAILY-1"`
   and fails if it changes (CM-R11.7, `liveops_spec.md:57`).
   *Check:* three NUnit vector cases + one constant test.
2. **No clock, anywhere.** Date keys are **inputs** (a `IReadOnlyList<string>` of `yyyy-MM-dd`), never
   read from a clock. `IClock` is not referenced; `DateTime`/`DateTimeOffset` do not appear under
   `unity/Assets/Scripts/Content/Daily/**`. This is what keeps the local-midnight/DST question
   (`liveops_spec.md:29-31`) out of a pure-C# contract entirely.
   *Check:* one appended `scripts/check.sh` grep block over the Daily root with a negative fixture +
   one NUnit case asserting the pipeline signature takes the date list.
3. **The horizon is a constant, not a literal, and its value is 90 (Q-Q).**
   `DAILY_PREVALIDATION_DAYS = 90` is declared in `config/daily_pipeline.json`, **copied from the
   corpus with the citation on the row** — CM-R46's heading says "90 dates pre-validated in CI"
   (`docs/prd/PRD.md:727`) and ADR-0009:35 says `validate-dailies` runs "over the next 90 dates" — and
   read by both the job and the tests (PRD constant convention, `docs/prd/PRD.md:88`).
   **This is a corpus number with exactly the status of the beam widths, not an agent choice**, so the
   criterion does not need Q-Q resolved to pass; what Q-Q still guards is only ADR-0008:9-14's warning
   that the **30-board dated backup pool is a different quantity**, which this pipeline never touches.
   **The criterion instance runs the configured 90; the 30-date run is the smoke instance** — the same
   shape ADR-0006:224-227 uses for `QUEUE_MAX_EVENTS`/500.
   *Check:* one case asserting the pipeline processes exactly `DAILY_PREVALIDATION_DAYS` (= 90) dates;
   one 30-date smoke case; one asserting the value is read from the file and not hard-coded (grep);
   one asserting the file's row equals 90.
4. **Bounded, deterministic salt loop.** If `k = 0` produces a board failing any blocking CM-C5 stage,
   `k` increments deterministically until a board passes or `SALT_MAX_K` (declared in
   `config/daily_pipeline.json`) is reached; the resolved `k` is reported per date
   (`liveops_spec.md:55-56`; CM-R46.3). Two runs over the same date list produce the identical `k` for
   every date. *Check:* one case with a stub factory failing at `k=0` and passing at `k=1` asserting
   `k == 1`; one asserting `SALT_MAX_K` exhaustion yields a reported failure, not an infinite loop; one
   asserting `k` equality across two runs.
5. **Each date's board runs CM-C5's blocking stages.** A date whose board fails a blocking stage fails
   the job with the date key, the stage and the reason printed (CM-R46.1: "a failing date blocks
   merge"). Non-blocking verdicts (`UNCONFIGURED`, `PINNED`, `Indeterminate`, `STALE`) print and do not
   fail — the same semantics CM-C5 criterion 13 establishes, so the two jobs cannot disagree.
   *Check:* two cases (blocking fail → non-zero; non-blocking verdict → zero) + one asserting the
   printed reason names the stage.
6. **The artifact is the truth source, and it prints — written by the contract's own exe.**
   `scripts/validate-dailies.sh --out <path>` invokes
   **`dotnet run --project dotnet/CatMetro.DailyTools -- --out <path>`** — the console host this
   contract owns, which is where **all** file I/O lives — and that host writes JSON with one record per
   date (`{dateKey, k, seed, verdict, stageVerdicts, solverCompletionTicks}`) **and** prints one stdout
   line per date matching `^DAILY_SEED <dateKey> <k> <seed>$` (ADR-0009:35 "printing the resolved seed
   per dateKey"; the source CM-R43.8 compares a device against, `docs/prd/PRD.md:695`).
   **The pipeline logic under `unity/Assets/Scripts/Content/Daily/**` opens and writes nothing**:
   `System.IO` is banned there by CM-C2a criterion 2's appended `check.sh` block, so config arrives as
   bytes through `IContentSource` and the artifact is serialised in-memory and handed to the host.
   *Check:* one case asserting the JSON record shape for every date; one asserting exactly one
   `DAILY_SEED` line per date and no other line starting with `DAILY_SEED`; one `[CI]` grep asserting
   zero `System\.IO` matches under `unity/Assets/Scripts/Content/Daily/**`.
7. **Byte-identical across runs.** Two invocations over the same date list and the same config produce
   **byte-identical** artifacts (this is the pure-C# half of roadmap D12's "the same daily seed
   produces an identical level on 2 devices"; the two-device half is CM-C2b/device work and is **not**
   claimed here). *Check:* one wrapper-level `diff` of two runs.
8. **The board generator is out of scope and stop-gated (Q-S).** The pipeline consumes an
   `IBoardFactory { LevelDto Build(uint seed, string dateKey, int k); }`. **No shipped implementation
   is written** — the corpus specifies no board-shaping rule anywhere, and NEW-Q21's weekday curve file
   does not exist. Tests supply a stub factory. *Check:* one case asserting the pipeline is fully
   exercised through a stub; one asserting no type under `Content/Daily/**` implements `IBoardFactory`
   (grep), so the gap cannot be silently filled.
9. **Weekday ramp reads a file that does not exist yet.** The ramp check (CM-R46.5, **`[PIN NEW-Q21]`**)
   reads `config/daily_weekday_curve.json`; **absent → the check prints `UNCONFIGURED(NEW-Q21)` and
   does not block**, exactly as CM-C5 criterion 13. Neither candidate curve
   (`liveops_spec.md` 0.35…0.75 vs `product_spec.md:452` 0.30…0.55) may be committed by an agent.
   *Check:* one case asserting `UNCONFIGURED(NEW-Q21)` with the file absent; one asserting the ±0.05
   comparison runs when a fixture curve is supplied.
10. **Harness discovery.** `tests/daily/daily-pipeline.test.sh` exits 0 iff `dotnet test` is green and
    performs criterion 7's two-run diff; `bash scripts/test.sh` prints
    `PASS tests/daily/daily-pipeline.test.sh` and a summary line matching `^test: [0-9]+/[0-9]+ passed`
    **whose two numbers the wrapper compares equal** (the backreference form `\1` is not POSIX ERE —
    see CM-C2a criterion 13).
    *Check:* `bash scripts/test.sh` exits 0 with both lines and the numeric comparison passing.
11. **Scope guard, asserted.** No backend, no store, no push, no analytics, no clock, no `UnityEngine`,
    no network: a `[CI]` grep over `unity/Assets/Scripts/Content/Daily/**` and
    `scripts/validate-dailies.sh` finds zero occurrences of `Http|WebRequest|UnityEngine|DateTime|
    IClock|OneSignal|RevenueCat|Firebase`. The **device-side** limbs of CM-R46 — the 250 ms bounded
    salt loop (CM-R46.3) and the ≤200 ms boot validation with backup-pool fallback (CM-R46.4) — are
    **explicitly not claimed here**; they are device work and are recorded as deferred in the PR.
    *Check:* one grep assertion with a negative fixture + the PR's deferral note.

### Scope boundary

**In scope:** the paths in the ownership table for CM-C6, plus registration-only appends.

**Explicit non-goals:** no `.github/**` workflow (**Q-V**); no board generator (**Q-S**); no weekday
curve values (**NEW-Q21**); no seed-scheme choice (**NEW-Q8** — implement the adopted reading, pin the
constant); no clock, no timezone, no DST logic; no `daily_overrides.json`, no `daily_backup_pool.json`,
no `catalog.json`/`content.sha256` (the shipping pipeline is a later contract); no device budget
claims; no Unity; **no `System.IO` under `unity/Assets/Scripts/Content/Daily/**`** — CM-C2a criterion
2's `check.sh` block bans it and CM-C6 may not edit that block, so **all reads go through
`IContentSource`** and the only filesystem code this contract writes lives in
`dotnet/CatMetro.DailyTools/**` (criterion 6, Q-X); no writes to immutable paths.

### Assumptions

- **A-C6-1** The horizon is **90, copied from `docs/prd/PRD.md:727` and ADR-0009:35** — a corpus
  number, not an agent choice, with the same status as the beam widths (**Q-Q**). It is declared as a
  configured row so the tests and the job read one source, not so an agent may pick it.
- **A-C6-2** `SALT_MAX_K` is the **one** unpinned number here and is analyst-authored —
  `liveops_spec.md:55-56` describes the loop but names no ceiling. Declared with its derivation in the
  config file and flagged in the PR. Stop condition 3 deliberately does **not** cover it: forbidding
  the commit that criterion 4 requires would make the criterion unpassable.
- **A-C6-5** The console host `dotnet/CatMetro.DailyTools/**` is **analyst-assigned** (**Q-X**): the
  pipeline library may not do file I/O, so an executable must. Same status and same remedy as A-C5-6.
- **A-C6-3** The seed's `k` component is serialised as its invariant decimal representation; nothing in
  the corpus states the encoding, so it is fixed here and recorded — **changing it changes every daily
  seed**, so it is treated as golden-adjacent and named in the PR.
- **A-C6-4** Date keys are supplied by the caller in `yyyy-MM-dd` form (`overview.md:221`
  `IClock.LocalDateKey`). CM-C6 validates the form and rejects anything else.

### Stop conditions

Defaults apply. Plus:
1. Any need to read a clock, a timezone database or a network → stop.
2. **Any temptation to write a board generator** because the pipeline "needs one to be useful" → stop
   and cite Q-S; a stub factory is the deliverable.
3. **Any temptation to commit a weekday curve value** that is not human-ratified → stop (NEW-Q21;
   criterion 9 ships `UNCONFIGURED`). **Narrowed deliberately:** this condition no longer covers the
   horizon or the salt ceiling, because it previously forbade the very commits criteria 3 and 4
   *require*. The horizon is **90, copied from `docs/prd/PRD.md:727` / ADR-0009:35** (a corpus number,
   A-C6-1); `SALT_MAX_K` rides A-C6-2's declare-with-derivation route and is flagged in the PR.
   A number that is neither in the corpus nor derivable **is** still a stop.
4. NEW-Q8 appears to need answering to pick a seed scheme → stop; the adopted reading plus the pinned
   constant is the sanctioned path.
5. A daily board cannot be validated without changing a CM-C5 stage → stop; that is a CM-C5 amendment.
6. Anything requires `state/mode=production` or touches a monetization path → stop.

---

# CONTRACT CM-C7 — Save v1: header + payload, atomic write, migration, ledger dedupe, `[ARCH]` bounds

**Roadmap:** D6 (`docs/plan/data/roadmap_56_days.csv:7` — "Save v1 (versioned JSON + atomic
temp-then-rename); save and analytics flush in OnApplicationPause within the 50 ms budget").
**DEPENDS-ON:** CM-C1 (merged) and **CM-C2a merged** (`dotnet/CatMetro.Services/` project skeleton).
**Blocked on:** **CM-C2a's merge only** — no human, no licence, no Unity. ADR-0006 §Consequences
(`:369-371`) puts the kill-during-write and migration tests in the fast `dotnet` leg precisely because
"none of this needs an engine". **CM-C7 is therefore not a root of the dependency graph**; it is the
first contract that unblocks when CM-C2a lands.

### Goal

The engine-free half of the save system: the 16-byte header, the v1 JSON payload, the atomic
temp+`File.Replace` write behind `IStorageRoot`, the migration table, the domain-separated purchase
ledger **as a data structure**, and the authored `config/runtime_bounds.json` that every other contract
reads.

### Spec reference

`docs/prd/PRD.md` CM-R05.1 (kill-during-write, SI-1…SI-7 at `:161-168`), CM-R05.3 (migration +
downgrade refusal), CM-R05.5 (`SAVE_MAX_BYTES`) · CM-R27.3–.5 (dedupe insert + audit + balance in one
write; FIFO audit cap) ·
`docs/adr/0006-save-format-purchase-ledger-and-runtime-bounds.md` §1 (`:28-72` header, atomic write,
`.bak` fallback, migration), §2 (`:74-140` payload v1 — **IRREVERSIBLE**, with three OPEN sub-shapes),
§3 (`:142-164` ledger + the RK-19 dedupe key), §4 (`:166-261` the `[ARCH]` constants, verbatim) ·
`docs/adr/0003-...:78-105` (`IStorageRoot`: the seam that keeps `CatMetro.Application` engine-free;
`ISave` declared in Services, implemented in Application) · `docs/architecture/overview.md:224-238`
(the `ISave` / `IStorageRoot` signatures) · `docs/adr/0005-...:112` (save round-trip/migration/ledger
dedupe run in the dotnet leg).

### Acceptance criteria (15)

1. **Header layout, byte-exact.** `save.dat` begins with the 16-byte header of ADR-0006:32-40 —
   `magic "CMSV"` (4), `formatVersion` uint16 LE (2), `saveVersion` uint16 LE (2), `payloadLength`
   uint32 LE (4), `payloadCrc32` uint32 LE (4, CRC-32/IEEE over the payload) — followed by UTF-8 JSON
   with **no BOM**. *Check:* one NUnit case asserting each field's offset, width and endianness on a
   written file, and one asserting byte 16 onward parses as BOM-free UTF-8 JSON.
2. **Payload v1 key set, exactly ADR-0006 §2.** The serialised payload's top-level keys are exactly
   `saveVersion, contentHash, profile, progress, daily, economy, caps, ledger, entitlements, flags,
   breadcrumbs, settings`, with the enumerated sub-shapes: `caps.counters` = the **five** locked ad
   surfaces (`rewind_failure, double_tickets, daily_gift_double, streak_saver, theme_rental`,
   ADR-0006:106-110), `flags` = the **six** ADR-0007 keys (`:118-122`), `ledger` =
   `{keyScheme, dedupe[], audit[]}`. *Check:* one case asserting the exact top-level key set (no more,
   no fewer) and one per enumerated sub-object.
3. **The three OPEN sub-shapes are absent, not guessed.** `caps.sessionCounters`,
   a typed `flags.paywall_placements` beyond `bool`, and any `breadcrumbs.purchase.state` **enum** are
   **not** introduced (ADR-0006:112-137; `overview.md:462`; RK-39 forbids inventing an RC API).
   `breadcrumbs.purchase` is `null` or exactly `{productId, placement, startedAtUtc, state}` with
   `state` carried as an **opaque string round-tripped untouched**. *Check:* three cases asserting each
   open shape is absent/opaque; one case asserting an **unknown key** in a loaded payload round-trips
   unchanged (ADR-0006:72 "A migration step never deletes a key it does not understand").
4. **Atomic write, exactly the ADR's three calls.** Commit = serialise → write `save.dat.tmp` →
   `FileStream.Flush(flushToDisk: true)` on the temp file → close →
   `File.Replace(save.dat.tmp, save.dat, save.dat.bak)` (ADR-0006:48-51). Never write in place.
   *Check:* one case asserting `save.dat.bak` exists with the previous contents after a second commit;
   one asserting no `.tmp` remains after a successful commit; one asserting the sequence via an
   injected filesystem seam. **No directory-fsync and no JNI helper is added** (ADR-0006:54-60).
5. **Kill-during-write leaves one complete version — SI-1…SI-7.** After an interrupted write
   (temp written, `File.Replace` not reached), the loaded save is the **complete previous** version;
   after a completed replace it is the **complete new** version; never a partial file. The loaded
   result satisfies **SI-1…SI-7** (`docs/prd/PRD.md:161-168`) against whichever version loaded.
   *Check:* one NUnit case per SI invariant (7) driven through the injected seam, ×2 interruption
   points.
6. **Load fallback chain never throws.** `save.dat` failing magic/length/CRC falls back to
   `save.dat.bak`; both failing starts a fresh save and reports `error_caught(domain=save_corrupt)`; a
   stale `.tmp` on boot is deleted (**SI-6**); nothing on the boot path throws (ADR-0006:62-67;
   `overview.md:300-301`). *Check:* four cases (bad magic, bad length, bad CRC, both files bad), each
   asserting `LoadResult` ∈ `{Ok, RecoveredFromBackup, Fresh, RefusedDowngrade}` and
   `Assert.DoesNotThrow`; one asserting stale-`.tmp` deletion.
7. **Migration table, ordered, with downgrade refused.** `MigrationTable` is an ordered list of
   `(from, to, Func<JObject, JObject>)` applied in sequence from the file's `saveVersion` to the
   build's; a file whose `saveVersion` **exceeds** the build's is left untouched, the app starts in a
   read-only in-memory default profile, and `save_migrated(from,to,success=false)` is logged
   (CM-R05.3; ADR-0006:68-72). *Check:* one v1→v2 migration case with a stub step; one downgrade case
   asserting the file's bytes are unchanged, the profile is read-only, and the event was recorded.
8. **Dedupe key, domain-separated, 32 lowercase hex.**
   `key = lowercase-hex(first 16 bytes of SHA-256("cm-ledger-v1|" + productId + "|" + transactionId))`
   (ADR-0006:146-153; RK-19). `ledger.keyScheme` persists as `"cm-ledger-v1"`.
   *Check:* one case asserting a pinned key for a fixed `(productId, transactionId)` pair; one
   asserting two different `productId`s with the same `transactionId` produce **different** keys (the
   RK-19 collision that the prefix closes); one asserting the output is exactly 32 lowercase hex chars.
9. **`TryGrant` is the only balance-raising path, and the order is non-negotiable.**
   `ConsumableLedger.TryGrant(transactionId, productId)` computes the key → checks dedupe → mutates
   in-memory state → performs **one** atomic write containing dedupe insert + audit entry + balance
   → **then** returns the value the caller may emit an event from. A fault before the write produces
   neither the balance change nor a grantable event (CM-R27.3/27.4; ADR-0006:155-157).
   *Check:* one case asserting a duplicate `transactionId` grants **zero** the second time; one
   asserting the three mutations land in a single `File.Replace`; one fault-injection case asserting
   neither balance nor dedupe changed and no event value was returned.
10. **RK-20 cap: refuse, never trim.** At `LEDGER_DEDUPE_MAX_ENTRIES` `TryGrant` **refuses**, returns 0
    and reports `error_caught(domain=ledger_capacity)`; the dedupe set is **never** trimmed. The audit
    list is FIFO-capped at `LEDGER_AUDIT_MAX_ENTRIES` (ADR-0006:158-162; CM-R27.5).
    *Check:* one case at the cap asserting refusal + the error + an unchanged set; one asserting the
    audit list drops oldest at its cap while the dedupe set does not.
11. **Size ceiling and the pause budget.** `LastCommittedBytes` is exposed and asserted against
    `SAVE_MAX_BYTES`; `TryCommitWithin(budgetMs)` returns false without writing when it cannot finish
    inside the budget, and the budget default is `SAVE_PAUSE_BUDGET_MS`
    (`overview.md:231-232`; ADR-0006:175-176; CM-R05.5). *Check:* one case asserting a
    synthetic over-cap payload is refused before writing; one asserting `TryCommitWithin(0)` writes
    nothing and returns false.
12. **`config/runtime_bounds.json` is authored here, verbatim, and cannot drift (Q-T).** The file
    contains **exactly the 15 keys of ADR-0006 §4 (`:171-193`) — `schemaVersion` plus 14 constants** —
    and the enumeration below is the authoritative list (an "exactly N" assertion whose N contradicts
    its own enumeration is not testable; N is 15 and the enumeration is what the test asserts):
    `schemaVersion 1 · SAVE_MAX_BYTES 524288 · SAVE_PAUSE_BUDGET_MS 50 · LEDGER_DEDUPE_MAX_ENTRIES 5000
    · LEDGER_AUDIT_MAX_ENTRIES 200 · LEDGER_KEY_SCHEME "cm-ledger-v1" · QUEUE_MAX_EVENTS 2000 ·
    QUEUE_MAX_BYTES 1048576 · QUEUE_EVENT_MAX_BYTES 512 · QUEUE_FLUSH_HIGH_WATER 64 ·
    QUEUE_FLUSH_TRIGGER ["network_reachable","app_foreground","app_pause","high_water"] ·
    ATTRIBUTION_MAX_RESIMS 24 · CONTENT_MAX_FILE_BYTES 262144 · CONTENT_MAX_JSON_DEPTH 16 ·
    CONTENT_BOUNDS_PROFILE "level-schema-v2"` — **15 keys, counted.**
    **Four drift tests:** (a) the file's key set has exactly those 15 members, no more and no fewer;
    (b) every constant this contract uses equals the file's row (no duplicated literal);
    (c) `CatMetro.Content.ContentBounds`'s `CONTENT_MAX_FILE_BYTES` and `CONTENT_MAX_JSON_DEPTH`
    (CM-C2a criterion 5) equal the file's rows;
    (d) `QUEUE_MAX_BYTES ≥ QUEUE_MAX_EVENTS × QUEUE_EVENT_MAX_BYTES`, the inequality ADR-0006:228-238
    says must hold or CM-R43.4(a) fails against its own bounds. **The `[ARCH]` values are copied, never
    chosen** — a value not in ADR-0006 §4 is stop condition 2.
    **Not delivered here, and the PR must say so (Q-Y):** ADR-0009:33 makes the required `ci` job assert
    `config/runtime_bounds.json` ↔ `unity/Assets/StreamingAssets/config/runtime_bounds.json`
    **byte-identity** (ADR-0008 names the copy step). **CM-C7 owns no `StreamingAssets` path** —
    `.../StreamingAssets/content/**` is CM-C2b's, `.../StreamingAssets/config/**` is unowned by every
    contract in this queue, and no `.meta` file exists anywhere under `unity/` until the Q-G scaffold
    lands. See the non-goal below.
13. **Engine-free, through `IStorageRoot`.** `CatMetro.Application` builds under `netstandard2.1` in
    `dotnet/CatMetro.sln`; `IStorageRoot` is declared in Services with exactly the two properties of
    `overview.md:235-238`; **zero** occurrences of `UnityEngine` or `persistentDataPath` appear outside
    `unity/Assets/Scripts/Bootstrap/**` (ADR-0003:102-105). Tests supply a temp directory; no
    `#if UNITY_ANDROID` exists. *Check:* build exit code + two grep assertions + one case running the
    whole suite against a temp `IStorageRoot`.
14. **Monetization tripwire, stated and checked (Q-T).** The ledger here is a **data structure**: a
    `[CI]` grep asserts zero files under `unity/Assets/Scripts/Application/Save/**` match
    `/billing/|/iap/|/ads/|RevenueCat|Purchases\.|BillingClient|GoogleMobileAds`, and no path this
    contract creates matches the AGENTS.md risky-path globs `**/billing/**`, `**/iap/**`, `**/ads/**`.
    **`state/mode` is not touched and no monetization surface is constructed.** The PR states this
    explicitly and asks for the human ack Q-T names. *Check:* one grep assertion + one
    `git diff --name-only` review showing no risky-path match.
15. **Harness discovery.** `tests/save/save.test.sh` exits 0 iff `dotnet test` is green;
    `bash scripts/test.sh` prints `PASS tests/save/save.test.sh` and a summary line matching
    `^test: [0-9]+/[0-9]+ passed` **whose two numbers the wrapper compares equal** (the backreference
    form `\1` is not POSIX ERE — see CM-C2a criterion 13).
    *Check:* `bash scripts/test.sh` exits 0 with both lines and the numeric comparison passing.

### Scope boundary

**In scope:** the paths in the ownership table for CM-C7, plus registration-only appends (sln entries ·
test-csproj `ProjectReference` · lock file · `config/pins.json` · `scripts/check.sh` blocks).

**Explicit non-goals:**
- **No Unity, no `IStorageRoot` implementation, no `persistentDataPath`** — that is Bootstrap's
  (ADR-0003:43; overview.md:210) and needs Q-G.
- **No SDK, no RevenueCat, no purchase flow, no entitlement fetch, no ad, no paywall.** The ledger
  gains balance only through `TryGrant`, whose caller does not exist yet.
- **No `analytics_queue.dat`, no queue behaviour** — CM-C8 (the two files are deliberately
  non-transactional, ADR-0006:280).
- **No Android manifest, no `allowBackup`, no backup-rules XML** — the RK-17 open conflict
  (ADR-0006:291-333) is a **human decision that must land with the save format**; CM-C7 records it as
  unresolved and ships neither posture.
- **No `contentHash` computation** (ADR-0008's catalog pipeline is a later contract); the key exists and
  round-trips.
- **No `unity/Assets/StreamingAssets/config/**`, and therefore no copy step (Q-Y).** The
  `config/runtime_bounds.json` ↔ `unity/Assets/StreamingAssets/config/runtime_bounds.json`
  byte-identity assertion of ADR-0009:33 (ADR-0008's copy step) is **deferred to the content-pipeline
  contract that owns `StreamingAssets`**. **The PR records that this `ci` clause is unsatisfiable until
  then and names the follow-up by name** — it is not silently left to fail. CM-C7 authors only
  `config/runtime_bounds.json`.
- **No economy values** — `config/economy_defaults.json` is a human decision (CM-R04.1) and is not
  authored here.
- **No writes to immutable paths** (AGENTS.md hard rule 1).

### Assumptions

- **A-C7-1** Authorship of `config/runtime_bounds.json` is assigned to this contract by the tranche-2
  decomposition (**Q-T**), resolving tranche 1's CM-C2 stop condition 6. The values are ADR-0006's; the
  assignment is the analyst's and needs human ratification.
- **A-C7-2** The ledger is a data structure, not a monetization surface (**Q-T**). If the human rules
  otherwise, CM-C7 stops until `state/mode` is `production` (`state/PROJECT_STATE.md:10`).
- **A-C7-3** The save's payload is serialised with the CM-C2a-pinned Newtonsoft version and
  `TypeNameHandling = None` (ADR-0006:337-342 rejects `BinaryFormatter`-class deserialization outright).
  **CM-C7 reuses `CatMetro.Content`'s settings factory (CM-C2a criterion 4, e.g. `ContentJson.Settings`)
  and constructs none of its own** — ADR-0003 permits `Application` → `Content`. This is not a style
  preference: CM-C2a's appended `check.sh` block fails on any `TypeNameHandling` match outside that one
  file path, and CM-C7 may not edit an existing block, so a second settings site would be unmergeable.
- **A-C7-4** `saveVersion` starts at 1 and `formatVersion` starts at 1 (ADR-0006:35-36). The v1→v2
  migration test uses a **stub** v2 step; no real v2 schema is invented.
- **A-C7-5** The RK-17 backup decision and the three open payload sub-shapes stay open; shipping v1
  without them is legal because none of the three has a *present* consumer — but **the moment a tester
  device holds a v1 file the payload shape is irreversible** (ADR-0006:100-104,377-379), which is why
  the PR must carry the human ADR gate explicitly.

### Stop conditions

Defaults apply. Plus:
1. Any criterion requires an SDK call, a store type, a purchase flow or an ad → **stop**; that is the
   monetization tripwire and needs `state/mode=production` first.
2. **Any temptation to choose an `[ARCH]` value not present in ADR-0006 §4** → stop; copy or stop.
3. The RK-17 auto-backup decision appears to be needed to make a criterion pass → stop and escalate
   (ADR-0006 §Open conflict; "the backup rules must land **with** the save format").
4. Any of the three OPEN payload sub-shapes appears to need a concrete type → **stop**; enumerating
   `breadcrumbs.purchase.state` would be inventing an RC API, which RK-39/A-07 forbid.
5. Directory-fsync, JNI or a native helper looks necessary for durability → stop (ADR-0006:54-60).
6. Trimming the dedupe set looks like the fix for the cap → **stop**; refuse-at-cap is the decision
   (ADR-0006:356-359) and trimming re-opens double-granting.
7. `config/runtime_bounds.json` authorship is contested by the human (Q-T) → stop and re-cut.

---

# CONTRACT CM-C8 — Analytics offline queue: bounded, ordered, lossy-but-visible, metrics-only

**Roadmap:** D13 (`docs/plan/data/roadmap_56_days.csv:15` — "Typed analytics wrapper (single choke
point; unknown event names assert in dev builds); **offline event queue**").
**DEPENDS-ON:** **CM-C7 merged** — it supplies `dotnet/CatMetro.Application/`, the header/atomic-write
helper, and the `QUEUE_*` rows in `config/runtime_bounds.json` that this contract's criteria read.

### Goal

The Domain/Application half of the offline analytics queue: a bounded, ordered, crash-safe,
metrics-only queue behind `IAnalytics`, with per-event idempotency, drop-oldest overflow accounting and
flush-trigger semantics — all as pure logic, with **no SDK and no engine**.

### Spec reference

`docs/prd/PRD.md` CM-R43.4(a)–(d) (`:687-691`) — read (a) precisely: the MUST test enqueues **exactly
`QUEUE_MAX_EVENTS`** and the 500-event/24 h instance is the *smoke* variant (ADR-0006:222-227) ·
`docs/adr/0006-...` §4 (`QUEUE_*` rows and their rationales, `:182-186,222-245`), §5 (`:269-289` —
`analytics_queue.dat`: path, `"CMQU"` header, same write helper, **non-transactional with respect to
the ledger**, lossy-by-design, **excluded from auto-backup unconditionally**) ·
`docs/adr/0003-...:42,75-77` (`IAnalytics`/`IDiagnostics` declared in Services; SDK types live only in
`Integrations.*`) · `docs/architecture/overview.md:245-249` (the `IAnalytics` signature) ·
`docs/security/threat-model.md:211` (**M-21** — see **Q-U**) · `docs/prd/risks.md` RK-31/RK-32.

### Acceptance criteria (12)

1. **File shape reuses CM-C7's helper.** `analytics_queue.dat` sits beside `save.dat` under
   `IStorageRoot.SaveDirectory` and uses the **same 16-byte header** with magic `"CMQU"`, the same CRC
   check, the same temp+`File.Replace` write path, and the same reject-and-restart-empty behaviour on
   header/CRC failure (ADR-0006:277-279). *Check:* one case asserting the magic and header offsets; one
   asserting a corrupted queue file restarts empty and reports `queue_dropped` with the lost count; one
   asserting the write path is CM-C7's helper (no second implementation — a grep assertion).
2. **Every bound is read from `config/runtime_bounds.json`, never hard-coded.**
   `QUEUE_MAX_EVENTS`, `QUEUE_MAX_BYTES`, `QUEUE_EVENT_MAX_BYTES`, `QUEUE_FLUSH_HIGH_WATER`,
   `QUEUE_FLUSH_TRIGGER` are read at construction. *Check:* one case asserting each constant's live
   value equals the file's row; one grep asserting the five literals appear in no source file.
3. **No-loss / in-order / no-duplicate at the cap.** With **exactly `QUEUE_MAX_EVENTS`** events
   enqueued and the transport unavailable, all of them flush **in enqueue order** with **zero
   duplicates** on reconnect, verified by the per-event idempotency id (CM-R43.4(a),
   `docs/prd/PRD.md:688`). *Check:* one criterion-instance case at `QUEUE_MAX_EVENTS`; one **smoke**
   case at 500 (the ADR-0006:224-227 reading); both asserting order equality and a duplicate count of 0.
4. **Overflow drops oldest-first and says so, on both limbs.** Exceeding `QUEUE_MAX_EVENTS` **or**
   `QUEUE_MAX_BYTES` drops **oldest-first** and emits the named counter `queue_dropped` carrying the
   dropped count (CM-R43.4(b); ADR-0006:263-266; RK-32). *Check:* two cases — the count limb (normal
   events beyond the cap) and the byte limb (`QUEUE_EVENT_MAX_BYTES`-sized events beyond the byte cap)
   — each asserting the surviving set is the newest N, the dropped count is exact, and
   `queue_dropped` fired once per overflow event.
5. **An oversize single event is dropped, not queued.** An event that cannot serialise under
   `QUEUE_EVENT_MAX_BYTES` is dropped with `queue_dropped` and never enters the queue
   (ADR-0006:239-241). *Check:* one case asserting the queue length is unchanged and the counter
   incremented.
6. **Flush fires on exactly the four triggers and on no timer.** `QUEUE_FLUSH_TRIGGER` is exactly
   `["network_reachable","app_foreground","app_pause","high_water"]`, plus the `QUEUE_FLUSH_HIGH_WATER`
   threshold; a **negative test** asserts no flush occurs from elapsed time alone
   (CM-R43.4(c); ADR-0006:242-245 — "exactly these four and **no timer**, so the negative test is
   decidable"). *Check:* four positive cases (one per trigger) + one negative case advancing a
   simulated tick source with no trigger and asserting zero flushes.
7. **Metrics-only, statically.** A `[CI]` check asserts no entitlement, ledger or cap type can be
   written through the queue: zero references to the CM-C7 ledger/entitlement/caps types from
   `unity/Assets/Scripts/Application/Analytics/**`, and the enqueue signature accepts only the
   analytics event type (CM-R43.4(d); ADR-0006:266-267). *Check:* one grep assertion with a negative
   fixture + one reflection case over the public enqueue surface.
8. **Idempotency id survives process restart and flush retry.** Each event carries an id generated at
   **enqueue** time and persisted with the event; reloading the queue file from disk and re-flushing
   after a simulated process death produces the **same ids**, so a retried flush dedupes instead of
   inflating counts. The derivation is **deterministic and reproducible in a test** (not
   `Guid.NewGuid`). *Check:* one case asserting id stability across a save/load cycle; one asserting a
   double flush of the same batch dedupes to one delivery per id; one asserting id uniqueness across
   `QUEUE_MAX_EVENTS` enqueues.
9. **M-21's backup limb — deviation recorded, not met (Q-U).** `docs/security/threat-model.md:211`
   requires an idempotency id "that survives a **backup restore**"; ADR-0006:282,285-289 excludes
   `analytics_queue.dat` from auto-backup **unconditionally**, which makes the restore path
   unreachable rather than idempotent. **This contract implements the four reachable limbs (criteria
   2–8) and records the backup limb as satisfied-by-exclusion, not by the id.** The artifact that makes
   exclusion true is the Android manifest / backup-rules XML — **not in this contract** (Q-G) — and the
   exclusion *set* depends on the unresolved RK-17 decision (ADR-0006 §Open conflict).
   *Check:* the PR carries a written deviation note naming M-21, ADR-0006 §5, Q-G and RK-17; one test
   asserts the queue's persisted record contains the idempotency id (so a future backup-aware design
   has the field it needs). **The criterion fails if the deviation note is absent**, not if the limb is
   unmet.
10. **`IAnalytics` is a Services interface and no SDK is touched.** `IAnalytics` is declared in
    `CatMetro.Services` with the signature at `overview.md:245-249`
    (`void Log(in AnalyticsEvent e)`, `void SetUserProperty(UserPropertyKey, string)`,
    `int QueuedEventCount`); zero SDK namespaces (`Firebase`, `OneSignalSDK`, `GoogleMobileAds`,
    `RevenueCat`) and zero `UnityEngine` appear anywhere this contract writes (ADR-0003:61-64).
    *Check:* one interface-shape case + one grep assertion with a negative fixture.
11. **Non-transactional with the save, by design and by test.** The queue write and the `save.dat`
    write are **two separate atomic writes, never combined**, and the ordering is always
    `save.dat` commit → enqueue → (later) flush, so a crash in the gap loses the **event**, never the
    **grant** (ADR-0006:280; CM-R27.3). *Check:* one case asserting a fault between the two writes
    leaves the grant durable and the event absent; one asserting no code path writes both files in one
    operation (grep + a seam assertion).
12. **Harness discovery.** `tests/analytics/queue.test.sh` exits 0 iff `dotnet test` is green;
    `bash scripts/test.sh` prints `PASS tests/analytics/queue.test.sh` and a summary line matching
    `^test: [0-9]+/[0-9]+ passed` **whose two numbers the wrapper compares equal** (the backreference
    form `\1` is not POSIX ERE — see CM-C2a criterion 13).
    *Check:* `bash scripts/test.sh` exits 0 with both lines and the numeric comparison passing.

### Scope boundary

**In scope:** the paths in the ownership table for CM-C8, plus registration-only appends.

**Explicit non-goals:**
- **No SDK adapter, no `CatMetro.Integrations.Analytics`, no Firebase, no Crashlytics, no OneSignal.**
- **No 45-event taxonomy, no typed event constructors, no required-param tests** (CM-R43.1/.2/.3 are a
  separate contract — this one is the **queue**, not the taxonomy).
- **No sampling, no session logic, no `first_open`/`app_open` semantics** (CM-R43.5/.6/.7).
- **No Android manifest, no backup-rules XML** (Q-G, RK-17 — criterion 9's deviation).
- **No `IDiagnostics` scrubber** (RK-31 — a separate contract).
- **No edits to CM-C7's save code**; the queue reuses its helper, it does not modify it. Needing a
  change there is a stop condition.
- **No writes to immutable paths** (AGENTS.md hard rule 1).

### Assumptions

- **A-C8-1** `CatMetro.Application` and `CatMetro.Services` exist from CM-C7/CM-C2a; CM-C8 adds files
  under paths it owns and edits no csproj (link-glob mechanism).
- **A-C8-2** The idempotency id derivation is analyst-shaped: deterministic per `(enqueue ordinal,
  event payload hash)` so it is reproducible in a test and stable across a reload. **No source
  specifies it**; the *properties* (stable, unique, persisted) are the criteria, the derivation is the
  implementer's and is recorded in the file header.
- **A-C8-3** The queue's "transport" is an injected seam in tests; **no real transport exists in this
  contract**, so "flush" means "hand the batch to the seam and mark it delivered on ack".
- **A-C8-4** M-21's backup limb is out of reach here (**Q-U**) and is recorded, not silently dropped.
- **A-C8-5** The queue's persisted records are serialised through **`CatMetro.Content`'s settings
  factory** (CM-C2a criterion 4, e.g. `ContentJson.Settings`, `TypeNameHandling = None`); CM-C8
  **constructs no `JsonSerializerSettings` of its own** — ADR-0003 permits `Application` → `Content`,
  and CM-C2a's `check.sh` block fails on any `TypeNameHandling` match outside that one file path, which
  CM-C8 may not edit.

### Stop conditions

Defaults apply. Plus:
1. Any criterion appears to need an SDK, a network client or a real transport → stop.
2. Any entitlement, ledger or cap value looks like it belongs in an event → **stop**; that breaks
   CM-R43.4(d) and ADR-0006:266-267 outright.
3. Any need to combine the queue write with the `save.dat` write "for atomicity" → **stop**; that
   inverts CM-R27.3 and would let a crash lose a grant.
4. Any need to change CM-C7's header/write helper → stop; that is a CM-C7 amendment.
5. Making M-21's backup limb true appears to require a manifest or backup-rules change → stop and cite
   Q-U/Q-G/RK-17; do **not** claim the limb met.
6. A `QUEUE_*` value looks wrong for a test to pass → stop; the values are ADR-0006 §4's and changing
   one is an ADR amendment (ADR-0006:374-375).
7. Anything requires `state/mode=production` or touches a monetization path → stop.
