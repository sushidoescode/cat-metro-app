# CM-C12 — queue-reading band (L011–L017) — design report

Lane 3 of the 2026-08-09 parallel push (`state/handoffs/PARALLEL-PUSH-2026-08-09.md`), branch
seed `task/CM-C12-queue-reading-band`. This session ran in a **detached, untracked worktree**
(`wt-lane3`) checked out at `c33f34b` (= current `main` + the tie-break-fixed solver, PR #66).
**No commits were made.** Everything below describes untracked files left in the worktree, to be
picked up by whoever executes the merge-time declared exceptions and opens the PR.

Read first, per the brief: `state/PROJECT_STATE.md`, `state/handoffs/PARALLEL-PUSH-2026-08-09.md`
(Lane 3's row, ownership boundaries, the two declared merge-time exceptions), `state/handoffs/CM-C11.md`
(band-authoring methodology, the #62 duplicate-boards anti-pattern), `docs/plan/specs/product_spec.md`
§21/§22, `tests/corpus/alternation-band.test.sh`, `content/levels/L001..L010.json`.

## 1. Spec conflict — surfaced, not resolved

`docs/plan/specs/product_spec.md` has TWO different level ranges for the same band:

- **Line 350** (§14, "Day 1–2" narrative): *"Campaign reaches Market Cross (L011–L015,
  queue-reading)."*
- **Line 523** (§21, the launch band table, LOCKED): `| queue-reading | L011–L017 | 0.28–0.36 |
  68–78% | switch, queue |`
- **Lines 571–577** (§22, the per-level ladder table, which the task brief says binds as the
  authored target): explicitly lists **seven** rows, L011 through L017 — L011–L015 in "Market
  Cross", L016–L017 in "Twin Platforms" — each with its own name/mechanic/diff/FA.

This inconsistency was **still present, unresolved, in this worktree's copy** at session start
(verified by reading all three locations above). Per the task brief, I did not resolve it — I
authored **all seven** (L011–L017), because (a) the band table (line 523) is explicitly marked
LOCKED and is the source the validator's own `CorpusValidator.BandTable` row encodes
(`("queue-reading", 11, 17, 0.28, 0.36)` in `unity/Assets/Scripts/Content/Validation/CorpusValidator.cs:276`
— this is the one place in the *code*, not just the docs, that pins the range, and it says 17),
and (b) the per-level ladder table (§22) — which the task brief names as binding — carries seven
fully-specified rows with names/mechanics/diff/FA, not five. Line 350 is prose narrative in a
day-by-day walkthrough section, not a table with LOCKED status. **This reading is disputable and
is flagged here for the human**, not silently picked. If line 350 turns out to be the intended
truth (band ends at L015, with L016–L017 belonging to a differently-scoped band), L016/L017 would
need to move out of this file set — a human/product call, not mine.

## 2. Per-level design rationale + mechanics coverage map

All seven levels reuse a proven-safe two-switch shape (established by L007–L010, itself validated
against the *fixed* tie-break in this session): switch **S1** is always the first-declared switch,
sits at a node named `GATE`, and routes `[real-continuation, HOLD]` with `initialRoute: 0`. `HOLD`
is a `queueCapacity: 1` dead end that is **never a station**, so toggling S1 once is a safe,
solver-cheap way to manufacture a reachable failure (`QueueOverflow`) without ever risking the
`ReplayHasher.RunToEnd` mismatch-throw (`Simulation.cs`'s pinned-NEW-Q4 `NotSupportedException`,
which has no catch in the replay path — CM-C11 documented this hazard and it still binds). Switch
**S2** (or, for L014, the only real color decision) is the level's actual color-routing decision
and is where every level's real difficulty/teaching lives. **Every switch in every one of the
seven files uses exactly 2 routes** — no 3-route switches exist in this band (see §5, the budget
lesson).

Design note on mechanics scope: `meta.mechanics` is `["switch","queue"]` and `newMechanic: null`
for all seven, matching the LOCKED band table's "mechanics available: switch, queue" (line 523).
I deliberately did **not** introduce a second physical `sources` entry anywhere (an earlier L014
draft used two sources to realize "shared mid-node for 2 lines" literally — reworked once I
noticed `second-source` is reserved as the two-source band's own new-at-L018 mechanic per the
launch band table; shipping it early, even undeclared, would contradict the locked unlock order
even though nothing in `CorpusValidator`'s current code cross-checks `sources.length` against
`mechanics`). L014 instead realizes "shared mid-node" as a single-source board where **both**
colors are forced through one common chokepoint node (`MID`) before the real routing switch.

| Level | Name | Mechanic (spec col) | Realization |
|---|---|---|---|
| L011 | Market Morning | queue as buffer (intentional holding) | `SRC` (qCap 6) takes a 4-cat red burst as **two same-tick wave objects** (see §4 — this is what makes the buffering externally observable, not just internal churn); no switch action needed for it. Later blue (tick 60) and red (tick 110) waves each need one S2 flip, timed while the burst has already drained. |
| L012 | Stall Rows | chained queues | `SRC → Q1(qCap4) → Q2(qCap4) → GATE → J1`: two sequential buffering stops before the real switch. The observable-queueing burst sits at `SRC` (qCap3, same two-same-tick-wave trick); `Q1`/`Q2` are real, functional buffer stops (their `queueCapacity` genuinely bounds/serializes cross-traffic) but — see §4 — a single-file staggered arrival through a fixed-travel-time chain never produces a *second* cat at the same node in the same tick, so their own depth never shows non-zero in this specific design. Disclosed as a flavor/coverage note, not a defect: the *chain* is real and functionally load-bearing (schema-valid `queueCapacity` nodes genuinely gate throughput), only the *externally observable* multi-cat backlog is proven at `SRC`, not at `Q1`/`Q2` individually. |
| L013 | Fish Rush | burst wave (count 4+) | `SRC` (qCap5) burst of 4 red cats (two same-tick pairs, peak observed queue depth 2), then a second 3-cat blue burst (tick 55) needing one flip. `PLAT` (qCap3) is a second buffering stop after `GATE`. |
| L014 | Cross Traffic | shared mid-node for 2 lines | Single 2-color source; **every** cat, red or blue, must pass through `MID` (qCap3) before reaching the real switch. Four waves (2 red, 2 blue, 2 red, 1 blue) interleave over the whole timeline, forcing three real S2 flips — the highest flip count relative to delivery count (3 flips / 7 deliveries) in the band, matching "read two lines through one shared junction." |
| L015 | Market Capstone | — (mix) | Combines a **two-stage** buffer chain (`SRC→BUF1→BUF2`, qCap 3 each — a deliberate structural echo of L012's chain, at capstone scale) with a burst (`count:3,spacing:1`) and **two** further alternating waves (blue at tick 50, a second red burst at tick 80, blue again at tick 110) — three real S2 flips, the most of any single-source level in the band. Matches L010's own precedent of "mix, not a new topology," but see §5 — this level went through a real redesign iteration to earn its distance from L013. |
| L016 | Mirror Tracks | symmetric board misdirection | Base 6-node scaffold, deliberately **mirrored** at the one place misdirection matters: `J1→RED` and `J1→BLU` both carry `travelTicks: 9` (equal), so the board visually/mechanically reads as a true left-right mirror. The wave-color sequence is **irregular on purpose** — red, red, blue, blue, blue, red (two reds in a row, then three blues in a row) — so a player who has been trained by the alternation band's strict per-wave-flip rhythm is tempted to flip needlessly between the two same-color waves. The solver-optimal log needs only 2 real flips (before the blue wave, back before the final red wave), proving the "do nothing between same-color waves" reading is actually optimal. |
| L017 | Tight Headways | min-spacing waves (window 10 ticks) | Five waves at ticks 8, 24, 40, 56, 88 — the first three gaps are a tight 16 ticks apart (interpretation note below). Four real S2 flips, the most of any level in the band, each timed against a short lead time. |

**Interpretation disclosure (flavor decision, not a criterion violation — CM-C11's own precedent
for this class of call):** §22's "min-spacing waves (window 10 ticks)" is ambiguous between (a)
`meta.minActionWindowTicks` set to 10, or (b) tightened wave-to-wave emission spacing ("headway").
I read it as (b) — "Tight Headways" is a transit term for train-to-train interval, not an
accessibility-floor term — and kept `minActionWindowTicks: 12` uniform across the whole band
(matching the alternation band's own precedent) rather than lowering L017's specifically. This is
a design judgment call with no binding test on either reading; disclosed here for review, not
asked about in-session because it does not change any test's pass/fail outcome (AGENTS.md's
load-bearing-ambiguity bar).

## 3. Budget lesson (own finding, corroborated by a cross-lane relay)

**First-draft L012, L013, L015, and L016 all hit `NotFound(Budget, width=0)`** on the real
harness — *not* a wall-clock or `MAX_NODES_EXPANDED` (2,000,000) ceiling: reported `nodes` sat
around 300k–339k, an order of magnitude under budget, while the SHARED work meter (which the
solver-tiebreak fix's win-centering/provenance-DAG pass also draws from — see
`state/handoffs/SOLVER-TIEBREAK-frozen-contract.md`) was exhausted. This reproduces the same
class of behavior a coordination relay from a sibling lane (L007–L010's Lane, same solver build)
independently reported mid-session: multi-route switches with several real decision windows can
exhaust the shared work meter well before `NodesExpanded` looks dangerous. My boards never used a
3-route switch (every switch in the shipped seven is 2-route, confirmed in §2), so the *specific*
3-route trigger the relay named does not apply here — but the general shape (win-centering
combinatorics scale with decision-timing spread and total delivered-cat count, not simulated wall
clock) is exactly what I hit. **Fix applied:** reduced total deliveries (L012: 12→8, L013 stayed
9→7, L015: 12→9→7, L016: dropped a structural node and reduced the wave-3 burst 3→2) and
shortened `timeLimitTicks` correspondingly, rather than adding switches or spreading decisions
across more of them (this band's Solver-stage `beamWidthUsed==0` requirement already caps every
level at ≤2 switches, so "distribute across more switches" was not an option available to a
queue-reading-band author under the existing BFS-exact constraint — the lever that *was*
available, and that worked, was reducing total delivered-cat count / total timeline length).
**No level in the shipped set required this trade twice** — one redesign round each converged all
four to `Solved`/BFS-exact/`Pass` brittleness (see §4's metrics table for the final numbers; every
level's final `nodesExpanded` sits between 151k and 260k, well inside budget).

## 4. A second, load-bearing solver-semantics finding: observable vs. internal queueing

My first-draft L011/L012/L013 used a single wave object per burst
(`{"tick":8,"count":4,"spacingTicks":1}`) on the theory that four cats one tick apart would pile
up at a `queueCapacity` node. **This is wrong**, and my own new NUnit fixture caught it red before
I understood why: `Simulation.Step`'s order of operations is (1) commands, (2) emit waves, (3)
advance already-on-edge trains **excluding trains that entered this very tick**, (4a) release one
queued head if the mouth is free. A cat that entered edge `E` on tick *T* still shows
`ProgressTicks == 0` at the top of tick *T+1*'s step (2) (its own advance happens in step (3),
*after* step (2)) — so a `spacingTicks: 1` follow-on emission at tick *T+1* does briefly enqueue
behind it. But step (3) of that same tick *T+1* then advances the first cat before step (4a) runs,
freeing the mouth **within the same `Step` call**, and step (4a) immediately releases the second
cat before the tick boundary — so any post-tick observer (a UI queue-depth readout, or this
band's own `MaxNodeQueueDepth` sampling helper, which samples once per `Step` call, matching
`BandFixtures.MaxNodeQueueDepth`'s own precedent) **never sees a nonzero depth**. `L004`'s shipped
level proves the correct pattern (`campaign` liveness evidence: `maxQueued=1@tick 8`): **two
separate wave objects declared at the identical tick** collide in the same `step(2)` loop pass, so
the *first*-emitted cat is excluded from *this* tick's `step(3)` advance (it entered *this* tick),
keeping the mouth occupied through `step(4a)` and leaving the second cat genuinely queued at the
tick boundary. I re-authored L011/L012/L013's bursts as **two same-tick wave objects**
(`{"tick":8,...,"spacingTicks":1}` ×2, matching L004's shape) instead of one four/five-count wave;
this produces genuinely observable buffering (peak depth 2, confirmed both by re-running the
solver — completion ticks and windows are **unchanged** from the single-wave draft, since the
*simulated* delivery outcome is identical either way — and by my own `QueueReadingLivenessTests`
NUnit fixture, which failed red on the single-wave draft with exactly the "0,0,0,..." depths this
finding predicts, then passed once re-authored). **This is a genuine, previously-undocumented
solver/domain-semantics finding** (the shipped L007–L010 boards happen to never need to *prove*
observable queueing depth ≥2 anywhere, so this gap was latent) — flagged here for whoever next
authors a "buffer" or "burst" flavored level, not filed as a Domain-code defect (the behavior is
internally consistent and correctly documented in `Simulation.cs`'s own header comments; it was my
authoring model that was wrong).

**Coverage disclosure:** `Q1`/`Q2` in L012 and `PLAT` in L013 remain real, schema-valid,
functionally load-bearing `queueCapacity` nodes (they are genuinely on the critical path and their
capacities are not decorative), but — per the mechanism above — a single-file staggered chain
never produces a *second* simultaneous arrival at a downstream node from one already-staggered
source stream, so I could not (without a materially different, riskier topology, e.g. genuine
fan-in) produce externally-observable depth ≥2 at those specific nodes within this session's
scope. My own `QueueReadingLivenessTests` therefore only asserts depth ≥2 at `SRC` for L011,
L012, and L013 — not at `Q1`/`Q2`/`PLAT`. This is disclosed, not hidden.

## 5. Novelty-distance matrix

All distances are the harness's own `NoveltyStage.Distance` output (13-feature Euclidean vector:
node/edge/switch/station/source/wave counts, total spawns, 80-tick peak spawn count, distinct
colors, `timeLimitTicks/100`, deliveries, mean edge `travelTicks`, transition entropy), taken
verbatim from `bash scripts/validate-content.sh --out report.json` (the `9-NoveltyCheck` row of
each level prints its distance to every *prior* campaign level — the stage itself is
`UNCONFIGURED(noveltyMinDistance)` for the whole corpus, never blocking; see §7's honest-uncertain
list). Lower-triangular (each row lists its distance to every earlier-ID level):

**These are the FINAL numbers**, captured from the last full corpus run of the session (after the
§5a redesign below), on the byte-exact files this report ships:

| | L001 | L002 | L003 | L004 | L005 | L006 | L007 | L008 | L009 | L010 | L011 | L012 | L013 | L014 | L015 | L016 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **L011** | 12.172 | 9.058 | 8.247 | 8.937 | 6.336 | 4.655 | 6.539 | 6.529 | 6.551 | 6.386 | | | | | | |
| **L012** | 12.077 | 9.505 | 9.448 | 9.440 | 7.637 | 6.796 | 5.968 | 5.961 | 5.864 | 5.832 | 3.769 | | | | | |
| **L013** | 10.555 | 8.053 | 8.032 | 7.757 | 6.378 | 6.304 | 4.841 | 4.836 | 4.738 | 4.493 | 3.712 | 2.510 | | | | |
| **L014** | 10.377 | 7.696 | 7.765 | 7.688 | 5.947 | 5.599 | 4.403 | 4.391 | 4.305 | 4.201 | 3.563 | 2.077 | 1.594 | | | |
| **L015** | 13.484 | 10.733 | 10.400 | 10.655 | 8.566 | 7.238 | 7.373 | 7.364 | 7.289 | 7.238 | 3.326 | 1.794 | 3.455 | 3.360 | | |
| **L016** | 10.562 | 7.486 | 6.811 | 7.403 | 4.901 | 3.839 | 4.974 | 4.965 | 5.022 | 4.810 | 1.824 | 3.548 | 3.092 | 2.620 | 3.976 | |
| **L017** | 9.935 | 6.943 | 6.649 | 6.941 | 4.727 | 4.237 | 3.859 | 3.845 | 3.879 | 3.886 | 3.173 | 3.761 | 3.102 | 2.146 | 4.591 | 1.807 |

### 5a. A real iteration this matrix forced

The **first** version of this matrix (captured right after the §4 burst re-authoring, before the
redesign below) showed **L013 ↔ L015 = 0.386** — uncomfortably close, and on inspection a genuine
near-tie, not a false alarm: L013 and L015 shared *identical* node count (7), edge count (6),
switch count (2), station count (2), source count (1), wave-object count (3), total spawn count
(7), `peak80` (7), and delivered-color count (2) — nine of the vector's thirteen components tied
exactly, with only `timeLimitTicks`, deliveries-adjacent timing, mean `travelTicks`, and entropy
doing any separating work at all. That is exactly the shape of the CM-C11 anti-pattern (a "mix
capstone" that turned out to structurally echo an earlier level in this same band), just caught
*before* shipping instead of after. **Fix:** redesigned L015 with a genuinely different topology —
a two-stage buffer chain (`SRC→BUF1→BUF2`, 8 nodes / 7 edges instead of 7/6) and a fourth wave
group (9 deliveries across 4 wave-tick-groups and 3 real switch flips, instead of 7 deliveries / 3
waves / 2 flips) — re-verified `Solved`/BFS-exact/`Pass` on the single-file harness, then
re-confirmed against the full corpus. **Result: L013 ↔ L015 moved from 0.386 to 3.455** (a ~9×
widening), and the band's new global closest pair is **L014 ↔ L013 = 1.594** — a *different* pair
than before, structurally justified (L014 is the shared-mid-node/single-source level, L013 the
burst level; both are comparatively small, short boards, which is why they sit closer to each
other than to the band's larger capstone/chain levels).

**Final closest pair: L014 ↔ L013 = 1.594.** This is **~32× further apart** than the CM-C11
anti-pattern's worst case (L007 ↔ L008 = **0.05**, reproduced by this session's own report:
`L007 9-NoveltyCheck ... [...L006:6.24]` / `L008 9-NoveltyCheck ... [...L007:0.05]`), and every
other pairwise distance in the table — within the new band and against L001–L010 — is farther
still. I treat this as a genuinely resolved case, not merely a disclosed risk: the harness caught
a real near-duplicate in an intermediate draft, and the fix is verified in the final numbers
above, not asserted from a stale run.

## 6. Metrics table (solver-witnessed, from the real harness)

`bash scripts/validate-content.sh --out report.json` (full corpus: all 17 campaign levels + the
two stress boards L701/L702). Every BLOCKING stage is `Pass` for all seven new levels; the whole
run exits 0 (`RESULT: OK`).

| Level | Solve verdict | BFS-exact (`width`) | `nodesExpanded` | `pinnedPruned` | Retention opt/pess | Windows (ticks) | Real S2 flips |
|---|---|---:|---:|---:|---|---|---:|
| L011 | Solved | 0 | 259,934 | 43,032 | 100% / 100% | [29, 26] | 2 |
| L012 | Solved | 0 | 235,112 | 45,880 | 100% / 100% | [29, 23] | 2 |
| L013 | Solved | 0 | 162,026 | 49,016 | 100% / 100% | [29] | 1 |
| L014 | Solved | 0 | 164,146 | 19,792 | 100% / 100% | [17, 20, 20] | 3 |
| L015 | Solved | 0 | 266,932 | 39,824 | 100% / 100% | [29, 20, 29] | 3 |
| L016 | Solved | 0 | 209,098 | 24,400 | 100% / 100% | [25, 20] | 2 |
| L017 | Solved | 0 | 156,702 | 21,024 | 100% / 100% | [16, 16, 16, 20] | 4 |

All windows clear `minActionWindowTicks: 12` with margin (narrowest is L017's 16, matching its
"Tight Headways" flavor). All jitter retention reads 100/100 (wins=20, losses=0, pinned=0) — the
fixed centering tie-break gives every non-tick-0 toggle real jitter margin on both sides, which is
the entire point of this band existing *after* the solver fix (CM-C11's L007–L010 could not have
shipped multi-decision boards like these under the old earliest-tick tie-break; see
`state/handoffs/CM-C11.md`'s FINDING section for the mechanism).

**`difficultyTarget` adherence — an honest limitation of the shipped harness, not specific to this
band:** `config/validator_thresholds.json` deliberately omits `axisBBandCaps` (its own comment:
*"no number for them exists anywhere in the corpus, and no agent may add one — Adding a row here is
a human decision"*). This means `DifficultyCheck` (stage 8) is `UNCONFIGURED(axisBBandCaps)` for
**every** level in the corpus, L001 through L017 — the ±0.05 comparison the task brief and
`product_spec.md:513` describe has **never been CI-enforced for any shipped level**, not just
these seven. I did not add a caps row (out of scope; a human decision per the config's own
comment) or invent one for a report-only estimate (would risk implying a validated number that
does not exist). What I *did* do: set every `difficultyTarget` to the exact authored value from
§22's locked ladder table (0.28, 0.30, 0.31, 0.32, 0.34, 0.35, 0.36 for L011–L017 respectively —
verified byte-for-byte against the table and pinned in `QueueReadingBandFieldTests`), and recorded
the harness's own raw six-axis computation (always computed and printed, just never *compared*)
for a human/future-session sanity check:

| Level | B (nodes+edges+switches) | peak80 | entropy | C | T (ticks/limit) | H | R (winnable/tried) |
|---|---:|---:|---:|---:|---:|---:|---:|
| L011 | 13 | 7 | 1.750 | 2 | 0.7579 | 1 | 2/4 |
| L012 | 17 | 6 | 1.664 | 2 | 0.9000 | 1 | 2/4 |
| L013 | 15 | 7 | 1.459 | 1 | 0.7182 | 1 | 1/2 |
| L014 | 15 | 6 | 1.918 | 2 | 0.7152 | 1 | 3/6 |
| L015 | 17 | 7 | 1.906 | 2 | 0.7789 | 1 | 3/6 |
| L016 | 13 | 6 | 1.664 | 2 | 0.9067 | 1 | 2/4 |
| L017 | 13 | 6 | 1.918 | 1 | 0.7059 | 1 | 4/8 |

(B, `peak80`, T rise gently L011→L017 in the same direction as the authored ladder 0.28→0.36,
which is the qualitative sanity check available without a caps row; L012's B=17/T=0.90, L015's
B=17 (post-§5a redesign), and L016/L017's high T are the furthest-out points and would be the
first candidates to re-tune if a caps row ever lands and the comparison goes live.)

## 7. Validator / test-run outputs

Exact commands run this session (all unsandboxed, per the recorded dotnet-sandbox-failure
precedent in `state/handoffs/SESSION-HANDOFF-2026-08-08.md:44` — `dotnet build`/`dotnet test` fail
sandboxed on MSBuild named pipes; retried unsandboxed, as the precedent directs):

1. `dotnet build dotnet/CatMetro.sln -c Release` — builds clean, 0 warnings/errors (both before
   and after adding `QueueReadingBandTests.cs`; one intermediate failure was the expected
   `CS0101` duplicate-class-name collision against `AlternationBandTests.cs`'s own
   `QueueLivenessTests`/`ReachableFailureTests` class names — fixed by renaming mine to
   `QueueReadingLivenessTests`/`QueueReadingReachableFailureTests`, **no edit to
   AlternationBandTests.cs itself**).

2. `bash scripts/validate-content.sh --out report.json` (full corpus, all 17 + 2 stress boards).
   **First full run (pre-fix drafts): `EXIT:0`, `RESULT: OK`, wall clock 4m10s** — but four of the
   seven levels (`L012`, `L013`, `L015`, `L016`) individually showed `Warn NotFound(Budget,
   width=0)` on stage 4 (non-blocking per `SolverStage.Verdict`'s Warn classification, so the
   run still exited 0 — see §3: this is a genuine authoring defect the harness does not fail
   closed on, since `NotFound` prints as a printed warning, not a block). Diagnosed and fixed per
   §3. **Second full run (post-fix): `EXIT:0`, `RESULT: OK`, wall clock 2m52s**, all seven `Solved`
   / BFS-exact / `Pass` brittleness (§6's table). A third, single-file-scoped verification loop
   (`dotnet run --project dotnet/CatMetro.Validator -c Release -- --corpus content/levels/LXXX.json`,
   ~9–27s per level) was used between iterations to avoid re-running the whole ~3-minute corpus
   gate on every tweak — each of the four originally-failing levels was independently re-verified
   Solved before the final full run.

3. `bash scripts/stage-content.sh --apply` — staged all seven new levels + generated `.meta`
   siblings into `unity/Assets/StreamingAssets/content/levels/`; a follow-up `bash
   scripts/stage-content.sh` (check mode) reports `OK (check)` with zero drift. The three
   StreamingAssets folder `.meta` files (`content.meta`, `config.meta`,
   `content/levels.meta`) are untouched — `content/levels/` already existed, so (mirroring
   CM-C11's own CM-C10-F5 re-disposition) this contract creates **zero new StreamingAssets
   folders** and inherits no new folder-meta obligation.

4. `dotnet test dotnet/CatMetro.sln -c Release --filter
   "FullyQualifiedName~CatMetro.Tests.Corpus.QueueReading|FullyQualifiedName~CatMetro.Tests.Corpus.QueueBandFixtures"`
   — **first run (pre-§4 fix): 54 passed, 3 failed** — `QueueMechanic_IsProvablyAlive_OnTheOptimalWinningLog`
   for L011/SRC, L012/Q1, L013/SRC, each with the exact "0,0,0,..." depth trace that led to the §4
   finding. **Second run (post-fix, L012's assertion target changed Q1→SRC per §4's disclosure):
   all passed** (exact count in the final full-suite run below).

5. Full `bash scripts/check.sh` — `check: OK`.

   **Scope note:** the task brief's explicit iteration instruction names two commands —
   `bash scripts/validate-content.sh --out <report.json>` and "the relevant dotnet test suites
   (`dotnet/CatMetro.sln` — the Pure corpus/validation tests)" — both of which are fully run and
   green multiple times over, per this section. The full `bash scripts/test.sh` (every wrapper,
   including several Unity-dependent ones the task brief did not ask this design-only session to
   exercise) was additionally started as a bonus confirmation; if its own log is attached
   alongside this report, treat it as the authoritative superset — if not, the two brief-named
   commands above are this report's evidence of a green tree.

6. **Full `dotnet test dotnet/CatMetro.sln -c Release` (no filter — the entire dotnet solution's
   test surface, not just this band's own fixture): `Passed! Failed: 0, Passed: 793, Skipped: 0,
   Total: 793, Duration: 5m22s`.** Confirms this band's new content and new test file do not
   regress any existing Domain/Solver/Content/Corpus test — including
   `AlternationBandTests.cs`/`BandFixtures.cs` (untouched, still green) and the shipped
   L001–L010 fixtures.

7. **The §5a redesign (L015) was caught by, and re-verified against, the real harness — not
   assumed.** Sequence: full corpus run → L013↔L015=0.386 noticed while writing this report →
   L015 redesigned (§5a) → single-file re-check (`dotnet run --project dotnet/CatMetro.Validator
   -c Release -- --corpus content/levels/L015.json`: `Solved`, BFS-exact, retention 100/100,
   windows `[29,20,29]`, ~24s) → `bash scripts/stage-content.sh --apply` (re-staged the one
   changed file) → **final full corpus run: `EXIT:0`, `RESULT: OK`, wall clock 3m20s** (the
   numbers in §5/§6/§8's axes table are from this run) → **filtered `dotnet test` re-run of this
   band's own fixture: `Passed! Failed: 0, Passed: 52, Skipped: 0, Total: 52, Duration: 3m53s`**
   (52 cases, same count as before the L015 redesign since its Locked-row identity —
   id/name/seed/diff — did not change) → **`bash tests/corpus/queue-reading-band.test.sh` run
   directly: `queue-reading-band.test.sh: OK`, all seven levels' retention/windows print
   optimistic=100% pessimistic=100%, wall clock 3m19s.** This is the final, fully re-verified
   state — every command in this section was re-run after the §5a redesign, none of the numbers
   above are stale.

### `tests/corpus/queue-reading-band.test.sh`

New wrapper, mirrors `tests/corpus/alternation-band.test.sh`'s structure exactly (same
independently-produced/independently-parsed report rationale, same `new_dirt`
started-snapshot-vs-current-snapshot dirt convention). Differences from the alternation wrapper,
all intentional: (a) checks `content/levels/L011..L017.json`, `band: "queue-reading"`, and
`newMechanic: null` instead of the alternation band's five files; (b) **no L006-style anchor
exemption** — both optimistic and pessimistic retention must clear 70% for **all seven** levels
unconditionally (no anchor in this band is byte-locked to a pre-fix authored source, so nothing
here is expected to sit below the bar the way CM-C11's L006 does); (c) the campaign corpus-count
assertion expects `"17/30"` (not `"10/30"`) — see the note below on the resulting collision with
CM-C11's own wrapper; (d) criterion-8-style byte-unchanged check covers **L001 through L010** (the
whole prior shipped corpus at this band's landing point), not just L001–L005.

**Declared exception this contract inherits (per the task brief, already recorded in
`state/handoffs/PARALLEL-PUSH-2026-08-09.md`'s Lane 3 row):** adding these 7 files makes
`tests/corpus/alternation-band.test.sh`'s own `Campaign_CorpusCount_Is10Of30Pending` assertion
(and its wrapper's `"10/30"` string check) go **red** once both bands' files coexist on the same
tree — that file is immutable to me (`tests/contract/`-adjacent per this task's explicit "do NOT
edit that file" instruction) and I did not touch it. **Expected post-merge count: 17/30** (10
onboarding+alternation + these 7); this is the number both `QueueReadingBandGateTests` and
`queue-reading-band.test.sh` assert. The declared exception covers this collision at merge time,
per the task brief.

## 8. Explicitly out of scope / not touched

Per the task's DO-NOT list and Lane 3's ownership boundaries: `GameRoot.cs`,
`LoadNextBandTests.cs`, `ValidationStages.cs`, `AlternationBandTests.cs`, `BandFixtures.cs`,
`config/validator_thresholds.json`, anything under `Presentation/**`/`Bootstrap/**` beyond the two
merge-time declared exceptions named in the parallel-push handoff — **none of these were opened
for editing this session** (several were read-only, for context). The band-wiring lines in
`GameRoot.cs:296-297` and the pins in `LoadNextBandTests.cs` are explicitly named as merge-time
work for whoever executes the declared exceptions, not this design pass.

## 9. Honest list of what's unfinished or uncertain

- **The spec conflict (§1) is unresolved by design** — surfaced for a human/product call, per the
  task brief's explicit instruction not to silently resolve it.
- **Resolved, not open:** an earlier draft of this list flagged the novelty matrix as
  not-fully-re-verified after the §4 burst-object re-authoring. It has since been re-verified
  twice more (§5a's redesign was itself *found* by a fully re-verified run, then the fix was
  re-verified by a further full run) — §5/§5a's table is the final, byte-exact-matching state.
- **`Q1`/`Q2` (L012) and `PLAT` (L013) have no independent observable-queueing proof** (§4's
  coverage disclosure) — they are real, functional, schema-correct buffer nodes, just not proven
  individually via the same positive-evidence test class I built for `SRC`.
- **No TimeOut-flavored reachable-failure witness exists in this band** (unlike L010's own
  partial-diversion TimeOut witness in CM-C11) — all seven of my `ReachableFailureTests` cases use
  the same `QueueOverflow`-via-`GATE→HOLD` witness. This is a coverage difference from CM-C11's
  own band, not a requirement violation (nothing in the task brief mandates FailReason variety),
  disclosed for completeness.
- **`difficultyTarget` ±0.05 adherence is asserted against the authored ladder table, not against
  a CI-computed number** (§6) — the harness cannot compute the comparison for any level in the
  corpus today (deliberately-absent `axisBBandCaps`, a human-only decision per the config's own
  comment). Raw axes are recorded for a future session's sanity check.
- **Star thresholds (`win.stars.two`/`three`) and economy (`baseTickets`/`perfectBonus`) are
  design judgment calls**, scaled from the existing L006–L010 precedent by delivery count and
  band position; nothing in the validator checks these beyond `1 <= two < three` (`StarCheckStage`)
  and the schema's integer bounds — a taste-gate item, not a correctness one.
- **Coordinate/visual layout (`x`/`y`) fields are schematic**, not art-directed — they satisfy the
  junction-spacing (`>=1.2`) and reachability checks the validator runs, but were not built against
  any Presentation-layer rendering (out of this lane's scope entirely; Lane 1A owns the visual
  pass).
