# Handoff — CM-C4: Solver (BFS ≤2-switch, beam 1k/2.5k/5k) — HYBRID LANE

**Session:** 2026-08-03 · **Branch:** `task/cm-c4-solver` (off main @ 54a3410; depends only on merged CM-C1) · **Status:** ANCHOR — planner phase
**Lane:** hybrid, opened by the human (session instruction 2026-08-03: "my local model … for execution tasks — is that still happening?" + standing keep-powering authorization). Frontier (this session) plans, authors ALL tests/wrapper/check-blocks, and reviews; `qwen3.6:35b-a3b-coding-nvfp4` executes solver sources against frozen sub-contracts; `auto_execute=false` — the human glances at each frozen JSON before `forge-hybrid exec`; completion = check_cmd, never the executor's claim. CM-C4 merges as ONE PR.

## Planner decisions (load-bearing; the executor implements these, never re-decides)

**Files (executor write_scope, all new):**
- `unity/Assets/Scripts/Domain/Solver/SolveResult.cs` — `enum SolveVerdict : byte { Solved=1, Unsolvable=2, NotFound=3, Indeterminate=4 }`; `enum NotFoundReason : byte { None=0, Beam=1, Budget=2 }`; `sealed class SolveResult` (readonly fields: Verdict, NotFoundReason, CommandLog OptimalLog, int CompletionTicks, int SwitchesUsed, int BeamWidthUsed /*0=BFS*/, int PinnedPruned, int NodesExpanded, string FirstPinMessage /*""*/, DifficultyProxy Proxy); `readonly struct DifficultyProxy` (int MaxSimultaneousPendingDecisions, SolverOptimalTicks, TimeLimitTicks, MinQueueSlackAtPeak, SinglePerturbationsWinnable, SinglePerturbationsTried). NO score/star/chain/ticket member (criterion 8).
- `unity/Assets/Scripts/Domain/Solver/SolverBounds.cs` — `public const int MAX_NODES_EXPANDED = 2000000;` (analyst-authored A-C4-5; derivation comment: ≥ full 4000-tick × 500-state frontier of the largest schema board while bounding a worst-case run to low minutes of integer stepping; not a corpus number).
- `unity/Assets/Scripts/Domain/Solver/Solver.cs` — `public static SolveResult Solve(LevelGraph graph, ulong seed, int maxNodesExpanded = SolverBounds.MAX_NODES_EXPANDED)`.

**Search semantics:**
- A search node = the SimulationState AFTER a Step call; dedupe key = the exact `WriteDigest` byte image (visited set on the digest — deterministic, no hashing of our own beyond byte equality).
- Action space at each tick boundary: per switch, 0..(routeCount−1) toggles applied that tick (k identical `ToggleSwitchCommand(switch, tick-1)` entries, matching the shipped runner schedule `entry.Tick == stepTick − 1` so criterion 9's `ReplayHasher` replay agrees). Combos = cartesian across switches. ≤2-switch BFS: ≤9 combos/tick.
- BFS: frontier strictly by tick depth → first Won layer is provably tick-minimal; within the layer apply the criterion-7 tie-break (fewer commands, then lexicographic (Tick, SwitchId) pairs) before returning.
- Beam (S>2): per-tick beam ordered by (Deliveries desc, then digest lexicographic for determinism), widths 1000→2500→5000, report the succeeding width; miss at 5000 ⇒ NotFound(Beam,5000).
- Pinned successors: catch `NotSupportedException` around `Step` only (A-C4-2/Q-N), count, record first message, prune. LevelGraph construction throws are the CALLER's problem (criterion 5's carve-out).
- Budget: count each expanded node; exceed ⇒ NotFound(Budget) with NodesExpanded.
- `CompletionTicks == RunToEnd(graph, seed, log).Tick − 1` (criterion 6's equation — Won is set after the increment).
- DifficultyProxy exact counting rules: FROZEN AT TEST-AUTHORING against `product_spec.md:504-511` (the axes' own text) — recorded in the test file as the executable definition; the executor codes to the tests.

**Sub-cut plan (executor cuts; ≤2 criteria each per trust level 0; sequential; same stop conditions as the parent contract):**
G1 types+skeleton compile (feeds crit 1,6-shape) · G2 BFS exact (crit 3) · G3 beam (crit 4) · G4 pin-pruning (crit 5) · G5 tie-break+determinism+result population (crit 6,7) · G6 budget param + empty-log baseline (crit 10,11). Frontier-side (not executor work): all tests, `tests/solver/solver.test.sh`, check.sh grep blocks + negative fixtures (crit 2,8,12,13 check-halves), review, PR.

## Planner rulings round 2 (frozen at test-authoring; executor codes to these via the tests)

- **API extension (mirrors criterion 11's own pattern):** `Solve(LevelGraph graph, ulong seed, int maxNodesExpanded = SolverBounds.MAX_NODES_EXPANDED, int[] beamWidths = null)` — null ⇒ the authored `{1000, 2500, 5000}` (a test asserts the default via `SolverBounds.BEAM_WIDTHS` identity, so the injection point cannot drift); tests use `{1, 2500}` to force observable escalation.
- **Escalation law (deterministic by construction):** beam ordering is (Deliveries desc, then digest lexicographic asc); `SwitchesUsed` lives at digest byte 24, before `SwitchRoutes` at 44, so among delivery-tied states the LEAST-toggLED always sorts first — a width-1 beam therefore keeps the zero-toggle line and dies on any toggle-requiring board ⇒ escalation guaranteed.
- **Difficulty proxy, executable definitions (product_spec.md:504-511):**
  - C `MaxSimultaneousPendingDecisions` = max over winning-trace ticks of |{switches s : a train is OnEdge with `EdgeTo == SwitchNode[s]` or queued at `SwitchNode[s]`}|.
  - H `MinQueueSlackAtPeak` (queue term only, PARTIAL(Q-J)): peak tick = argmax total queued (earliest on ties); value = min over capacity-bearing nodes of `capacity − queued` at that tick; zero-queue traces report min capacity.
  - R: perturbation set = per optimal-log entry {entry removed} ∪ {entry.Tick + 1}; `Tried = 2×entries`, `Winnable` = count where `RunToEnd` still `Won` (pinned throws count as not-won).
  - Proxy fully populated ONLY when `Verdict == Solved`; otherwise all-zero except `TimeLimitTicks`.
- **Cross-process emission (criterion 7c):** exactly one stdout line `SOLVER_LOG=<hex|empty>` — lowercase hex of concat(ushort SwitchId LE, int Tick LE) per entry in log order; wrapper diffs two `dotnet test` processes.
- **Criterion-3 comparator bound:** the in-test brute force enumerates all command logs with ≤2 entries (fixtures designed so the true optimum uses ≤2 commands); comparator wraps pinned throws as not-won, mirroring Q-N.
- **Baseline nuance:** L001's empty-log run hits the NEW-Q4 pin (reds ride to BLU) ⇒ baseline verdict is `Indeterminate`, which satisfies criterion 10's "does not win" as `Verdict != Solved`.

## Hybrid lane outcome (2026-08-03, recorded honestly per the lane rules)

Cut g2 went to `qwen3.6:35b-a3b-coding-nvfp4` after the human glance. Three runs: (1) sandbox blocked
localhost:11434 (kit note: forge-hybrid exited 0 on the unreachable diagnostic); (2) cold-load timed
out the chat call with a raw traceback (kit note: no retry around load-time); (3) warm model ran the
full 8-turn loop — ONE whole-file edit applied (435 lines, structurally plausible: digest dedupe,
tie-break comparator, pin counters), 7 turns failed on search/replace anchor drift, check never
passed, **two-strike escalation fired** (`state/hybrid-escalations/cm-c4-g2-bfs-exact.md`). Root
cause of the draft's failure: it used `ReplayHasher.RunToEnd` (terminal-state semantics) where
step-by-layer simulation was required — its frontier could never contain a Running state — plus an
off-by-one in the command-tick schedule and a duplicated method. The planner-coder gap, measured
live, exactly as the kit README predicts. **Frontier implemented per the escalation rule**; cuts
g3–g6 retired to frontier with it (§9.0.3's proceed-frontier-only lever). Retry the lane later on a
write_file-shaped contract.

## Evidence (2026-08-03, per criterion)

1. Placement/purity: solver under `Domain/Solver/**`, zero csproj/dotnet-dir changes, `check.sh` exit 0.
2. One-Step + no tick-writes/score-reads: check.sh blocks green on tree, all three fire on `tests/fixtures/solver-bad` (exit 1).
3. BFS exactness: both brute-force comparators green (1-switch L001, 2-switch board), Unsolvable proof green.
4. Beam: solved-at-first-width reports 1000; forced `{1,2500}` escalation observed (the escalation law held); miss → `NotFound(Beam, 5000)`, asserted ≠ Unsolvable.
5. Q-N: L701-shape run completes with `PinnedPruned > 0` and a recorded pin message; win-despite-prunes green; zero-pin Unsolvable green.
6. Result record fully populated for L001; `CompletionTicks == 50` hand-computed; the `RunToEnd().Tick − 1` identity asserted.
7. Tie-break returns `[(0,0)]` on the two-solution board (required the within-layer comparator dedupe — see the bug note below); in-process double-run byte-identical incl. `NodesExpanded`; cross-process `SOLVER_LOG=000000000000010008000000` stable (wrapper exit 0) — decoding to `(S0,T0)+(S1,T8)`, the predicted optimum.
8. No score-shaped member (reflection over both result types) + the check.sh grep.
9. Hash equality ×2 + Won + Tick−1 equation green. `tests/contract/` untouched.
10. Baseline both limbs: L001 empty-log does-not-win (Indeterminate via the NEW-Q4 pin, per the planner ruling); already-correct board wins with the empty log.
11. Budget: `NotFound(Budget)` at cap 5 with `NodesExpanded ∈ (0,6]`; default-arg == constant asserted for budget AND widths.
12. `bash scripts/test.sh` → `test: 4/4 passed`, `PASS tests/solver/solver.test.sh`.
13. Runtime-reference guard armed (roots conditional-scanned; fixture-proven).

**Suite: 105/105.** Implementation bug found by the tests mid-build: insertion-order dedupe let the
untoggled-prefix branch claim every converged state, making the canonical log carry the LATEST
toggle and inverting the tie-break — fixed with criterion-7-comparator collision resolution
(the tie-break tests exist precisely for this).

---

## FROZEN CONTRACT (verbatim copy from state/backlog.md @ main 54a3410 — review verifies against THIS)
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
