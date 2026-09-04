# Level schema / importer / runtime audit

**Task:** 6 — schema/importer gap-table evidence
**Audited:** 2026-09-01
**Schema:** `docs/plan/data/level_schema.json`
**Runtime adapter:** `unity/Assets/Scripts/Content/LevelImporter.cs`
**Authoritative campaign:** `content/levels/L001.json`–`L060.json`

This audit traces authored JSON through immutable DTOs, `LevelGraph`, the shared simulation/solver,
presentation, and corpus validation. It replaces the earlier snapshot that correctly found reserved
fields being parsed but not acted on. The ladder fields are now wired through the shared runtime;
the remaining schema/importer differences are listed separately below.

This is executable code-and-content evidence. It is **not** evidence of a human playtest, a device
run, a store upload, store review, or public availability.

## Current result

- The canonical validator was run read-only on 2026-09-01 against 60 campaign levels plus the two
  non-campaign stress boards. It exited 0 with no blocking row, no campaign solve outside exact
  BFS, and no declared-mechanic replay failure.
- The campaign is 60 contiguous levels. A focused parity test discovers the authoritative and
  `StreamingAssets` `L*.json` sets, rejects gaps/duplicate IDs or extra/missing filenames, and
  compares every mirrored file byte-for-byte.
- `CorpusValidator` blocks unless the campaign has exactly 60 members in the fourteen configured
  band ranges, introduces no unexplained mechanic, and keeps difficulty targets inside each band.
- Every campaign member must solve with `SolveVerdict.Solved` and `BeamWidthUsed == 0`. A
  `NotFound`, budget exhaustion, unsolved result, or beam fallback is blocking for a campaign
  member. Non-campaign stress inputs retain their warning behavior.
- The introduction row replays the solver-optimal log to prove `meta.newMechanic`; a second,
  blocking row replays that same artifact and proves **every** mechanic named in each level's
  `meta.mechanics` array. Metadata alone cannot satisfy either check.

The enforced campaign ranges are:

| Band | IDs | Band | IDs |
|---|---|---|---|
| onboarding | L001–L008 | shape | L009–L012 |
| budget | L013–L016 | two-source | L017–L020 |
| alternation | L021–L024 | tunnel | L025–L028 |
| combination | L029–L032 | timed-gates | L033–L036 |
| oneway | L037–L040 | multi-line | L041–L044 |
| combo | L045–L048 | stray | L049–L052 |
| pressure | L053–L056 | capstone | L057–L060 |

## Field-by-field trace

Every schema leaf is represented below. “Imported” means the direct adapter reads/maps the value
into a DTO and, where applicable, dense graph arrays. It does not mean the boot path runs or fully
duplicates the JSON Schema; that distinction remains important in the final section.

| Schema field | Import and current consumer |
|---|---|
| `schemaVersion` | Imported and required to equal 2 before mapping. |
| `id` | Imported into the DTO/graph, ID maps, reports, and campaign navigation. The campaign gate additionally requires a contiguous `L001`–`L060` set. |
| `name` | Imported and displayed by the level-intro presentation. |
| `seed` | Imported and used by `GameSession`, replay, and solver RNG. |
| `meta.band` | Imported; drives campaign ID/difficulty ranges, validator band rules, and onboarding presentation. |
| `meta.difficultyTarget` | Imported; checked by campaign/band and difficulty validation and used by the daily ramp. It is metadata, not a simulation rule. |
| `meta.mechanics[]` | Imported; enables the `second-train` collision rule when named and otherwise feeds order and declared-mechanic replay checks. It does not implicitly enable unrelated mechanics. |
| `meta.newMechanic` | Imported as string or null. When non-null, it must belong to the level's own mechanics and be exercised by the exact winning replay. |
| `meta.teachingGoal` | Imported and retained by daily JSON/factory paths; there is no shipped tutorial-copy consumer. |
| `meta.minActionWindowTicks` | Imported and used by blocking brittleness/accessibility validation. |
| `meta.authoredBy` | Imported and retained by daily JSON/factory paths; no simulation effect. |
| `meta.validatedAt` | Imported when present and compared with the latest schema/simulation timestamp. Staleness is currently printed but explicitly non-blocking. |
| `board.nodes[].id` | Imported into dense node maps and reference validation. |
| `board.nodes[].x`, `.y` | Imported as integer geometry and consumed by static validation and board layout. |
| `board.nodes[].queueCapacity` | Optional; absent maps to 8. Simulation/solver use it for queue-overload state. |
| `board.edges[].id`, `.from`, `.to` | Imported into dense edge maps, reference checks, routing, validation, and track presentation. |
| `board.edges[].travelTicks` | Imported and used by the shared simulation/solver and track animation. |
| `board.edges[].oneWay` | Optional, default true. Forward traversal (`from` to `to`) is always eligible; ordinary reverse traversal is eligible when `!oneWay || reversible`. Static reachability and lower-bound search use the same rule. |
| `board.edges[].reversible` | Optional, default false. Makes the authored edge eligible for ordinary reverse routing, including a reverse-incidental switch route, and is observable as `TrainState.OnEdgeReverse`. |
| `board.edges[].tunnel` | Optional, default false. Motion uses the ordinary edge; presentation treats its endpoints as portals/hides the train during traversal, and replay liveness requires an actual winning traversal. |
| `board.edges[].hold` | Optional, default false. Designates an ordinary routed holding-loop edge. Static analysis blocks unless it participates in a directed cycle; presentation/liveness expose and verify traversal. |
| `sources[].nodeId` | Imported, reference checked, and mapped per wave into its actual runtime emission origin. |
| `sources[].allowedColors[]` | Imported, enum/schema checked by the corpus gate, and used for source/wave compatibility validation. |
| `stations[].nodeId`, `.accepts[]` | Imported into station/color maps and presentation. Concrete delivery requires an accepted color and matching shape; train-side Wild accepts universally. |
| `stations[].capacity` | Imported into `LevelGraph.StationCapacity`. More simultaneously refused trains than capacity fails with `PlatformOverflow`; equality is allowed. |
| `stations[].shape` | Optional `round`/`square`/`triangle`, default round. Imported into `StationShape`, displayed, and matched against concrete train tokens. |
| `switches[].id`, `.nodeId`, `.routes[]`, `.initialRoute` | Imported/reference checked and used for deterministic routing, replay, solver, and switch presentation. A route may be forward-incident or reverse-incident when reverse traversal is allowed. |
| `switches[].cooldownTicks` | Optional, default 0. An accepted press packs the cooldown into switch state; a cooling switch rejects presses for the authored number of following processing ticks. The solver and on-board countdown use the same state. |
| `gates[].edgeId` | Optional collection; imported/reference checked and mapped to a dense edge index. Gate state controls entry onto that edge in both directions. |
| `gates[].openWindows[][0]`, `[][1]` | Imported as ordered, non-overlapping half-open intervals `[start,end)`. Ordinary trains wait for the selected edge; the solver executes the same entry rule. |
| `gates[].previewTicks` | Optional, default 16 and schema minimum 8. The board shows the next open/close countdown only inside this authored preview horizon. |
| `waves[].tick`, `.count`, `.spacingTicks` | Imported into the deterministic emission schedule used by simulation and solver. |
| `waves[].sourceNode` | Imported/reference checked and mapped to `WaveSourceNode`; multi-source levels therefore emit from their authored sources. |
| `waves[].color` | Imported as a base color code and packed into the runtime train-token byte at emission. |
| `waves[].express` | Optional, default false. Packed into the train token. Express trains never occupy node queues: blocked source emission waits in `ExpressHeldAtSource`, while a blocked junction or station mismatch bounces immediately. |
| `waves[].shape` | Optional `round`/`square`/`triangle`, default round. Packed into the train token and used for station matching/presentation. |
| `waves[].stray` | Optional, default false. Packed into the train token. A stray never delivers and automatically presses a visited switch without spending player flip budget, while still obeying/starting cooldown. |
| `win.deliveries`, `.timeLimitTicks` | Imported and used by the shared win/timeout outcome. Static analysis requires `sum(wave.count where !wave.stray) == deliveries`. |
| `win.perfectMaxSwitches` | Optional; absence maps to `FlipBudget.Unbudgeted`. When authored, it is a hard accepted-player-press cap in simulation and solver, not merely a rating target. Budget liveness requires the winning replay to bind the cap. |
| `win.stars.two`, `.three` | Imported and ordering-validated. The score-threshold award/persistence model remains deferred; `FlipBudgetStatus.RatingStars` is separate. |
| `economy.baseTickets` | Imported; daily wins currently assign this value to `DailyTicketsEarned`. |
| `economy.perfectBonus` | Imported and retained by daily JSON/factory paths, but is not currently awarded. |
| `tags[]` | Optional imported DTO metadata; no Domain or presentation behavior. |

## Packed runtime identity

`TrainSlot.Color` remains the canonical one-byte token, so adding shape/stray/express did not widen
the ten-byte train slot or replay digest. `CatToken.Pack` lays out the byte as follows:

| Bits | Meaning |
|---|---|
| 0–2 | Base color (`red`, `blue`, `yellow`, `green`, `wild`) |
| 3–4 | Shape offset (`round`, `square`, `triangle`) |
| 5 | Stray flag |
| 6 | Express flag |
| 7 | Reserved and rejected if set |

A round, concrete, non-express, non-stray token remains byte-identical to the legacy color value.
Concrete cats must match both station color and station shape. Train-side Wild ignores both;
station-side `wild` remains an exact accepted color and does not make a concrete cat universal.

The switch byte is also packed: `SwitchState.Pack(route, cooldown)` preserves the legacy route-only
value when cooldown is zero. Commands logged at tick `T` are processed at `T + 1`; accepting a
cooldown `N` press blocks the next `N` processing ticks and permits the following one. Player
presses consult the hard `perfectMaxSwitches` cap. Stray automatic presses are cap-free and do not
increment `SwitchesUsed`, but use the same cooldown state.

## Direction, gates, cycles, and recovery

Authored graph traversal is no longer limited to a tree. Every edge is traversable from `from` to
`to`; it is also traversable from `to` to `from` when `oneWay` is false or `reversible` is true.
Switch-route incidence, static reachability, lower-bound Dijkstra, simulation, solver, and replay
observation use that rule. The campaign contains directed cycles, including validated holding
loops; a `hold:true` edge that cannot return to its start blocks static analysis.

Gates apply at edge-entry time in both directions using the half-open authored windows. A closed
gate does not cause alternate-route search: an ordinary train waits on its selected route. The
winning-artifact gate witness is deliberately stronger than “a gate field exists”: a train must be
observed waiting for a selected closed gated edge and later traversing that same edge while open.

Wrong-color and wrong-shape station arrivals are recoverable. A non-express train occupies the
station for exactly eight simulation ticks, increments `Rejections`, and then reverses its incoming
edge before rejoining ordinary routing. That refusal escape deliberately ignores direction and gate
flags; it is not accepted as evidence for the reversible mechanic. An express mismatch bounces
immediately. Simultaneous refused occupancy above `stations[].capacity` fails with
`PlatformOverflow`. Strays always refuse and never count as deliveries.

## Campaign artifact gates

The schema vocabulary contains fifteen labels: `switch`, `queue`, `second-source`, `wildcard`,
`cooldown`, `gate`, `express`, `reversible`, `shape`, `budget`, `tunnel`, `second-train`, `hold`,
`stray`, and the compound `wildcard-express`. The campaign uses `wildcard-express` as the compound
pressure rung; the validator still has distinct observations for standalone `wildcard` and
`express` should a level declare them.

`MechanicExercise.DeclaredMechanicsLiveness` replays each solver-optimal command log and blocks on
any declared mechanic that lacks the corresponding artifact evidence:

| Declaration | Required replay evidence |
|---|---|
| `switch` | A player press changes a route. |
| `queue` | At least one train occupies a node queue. |
| `second-source` | At least two authored source nodes emit. |
| `wildcard` | A Wild train delivers. |
| `shape` | A non-round concrete train delivers. |
| `budget` | Accepted player presses equal the authored hard cap. |
| `cooldown` | A player press occurs and packed cooldown becomes nonzero. |
| `tunnel` | A winning train actually traverses a tunnel edge. |
| `gate` | A train waits for a chosen closed gate, then traverses that edge open. |
| `reversible` | An ordinary, non-refusal/non-express traversal uses `OnEdgeReverse` on a reversible edge. |
| `second-train` | At least two trains are active and the zero-input replay fails by collision. |
| `hold` | A winning train actually traverses a hold edge. |
| `stray` | A stray emits and produces a cap-free automatic route toggle. |
| `express` | An express train delivers and never enters a node queue. |
| `wildcard-express` | A Wild express train delivers and no express train enters a node queue. |

The campaign assertions also block on mechanic-order, exact count, band/range violations, and an
unexercised `newMechanic`. Per-level rows still matter: the exact-solve campaign row was added
because the generic solver stage intentionally reports `NotFound` as a non-blocking warning for
stress inputs. The campaign proof closes that narrower-than-named gap by requiring both `Solved`
and beam width zero for every campaign row.

## Remaining schema/direct-import differences

`CorpusValidator` runs `SchemaStage` before `LevelImporter`, but the game boot path calls the direct
importer. These are remaining contract differences, not dropped ladder-mechanic fields:

| Schema contract | Direct importer behavior | Remaining risk |
|---|---|---|
| Optional `waves[].spacingTicks` with default 12 | Direct import currently requires the field. | A schema-valid omission fails runtime import; align the default or make it required. |
| Optional `meta.minActionWindowTicks` | Direct import currently requires the field. | A schema-valid omission fails runtime import. |
| Optional `stations[].capacity` | Direct import currently requires the field. | A schema-valid omission fails runtime import. |
| Optional `board.nodes[].queueCapacity` with no declared default | Direct import maps absence to 8. | Record the default in the schema or shared policy. |
| Optional `economy` and optional economy leaves | Direct import requires the object and both leaves. | A schema-valid omission fails runtime import. |
| `board.nodes[].x/y` type is `number` | Direct import accepts integers only. | Decimal schema-valid geometry fails runtime import. |
| Gate-window entries are only typed pairs in the schema | Direct import additionally requires nonnegative, ordered, non-overlapping `start < end` intervals. | A schema-valid but ill-ordered schedule fails runtime import; encode those invariants in schema when feasible. |
| Strict enums/patterns/lengths and `additionalProperties:false` | The direct importer duplicates selected bounds, IDs, references, colors, shapes, and defaults but does not execute the full schema or reject every unknown key. | A file can pass direct import yet fail the canonical schema stage; campaign admission must run both. |

## Task 6 conclusion and verification boundary

The original Task 6 blocker is discharged for the ladder: `oneWay`, `reversible`, `tunnel`,
`hold`, station/wave shape, switch cooldown, gate windows/preview, express, stray, multi-source,
station capacity, and the flip cap all survive JSON → DTO → graph and have authoritative runtime
or presentation consumers. Cycles and recoverable mis-delivery are implemented in the shared state
machine searched by the solver, rather than being validator-only declarations.

The canonical read-only proof command is:

```sh
dotnet run --no-restore --project dotnet/CatMetro.Validator -c Release -- --out <report.json>
```

That report and the focused parity/spline/prop tests are engineering evidence only. The validator's
human-playtest row remains `Pending` and non-blocking; this document does not claim device, store,
purchase, ads, submission, review, or public-live verification.
