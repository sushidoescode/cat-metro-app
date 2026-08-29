# Level schema / importer audit

**Audited:** 2026-08-26
**Schema:** `docs/plan/data/level_schema.json`
**Runtime adapter:** `unity/Assets/Scripts/Content/LevelImporter.cs`

Every schema leaf appears below. Array/object containers are named in their descendant rows (for
example, `board.nodes[]` covers both containers before listing every node leaf). “Read” means the
direct runtime importer consumes the JSON token; it does not imply the mechanic exists.

Cost key: **XS** = local DTO/consumer; **S** = one subsystem; **M** = coordinated Domain, solver,
validation, and presentation work; **L** = a new cross-cutting simulation mechanic.

## Field-by-field trace

| Schema field | Importer reads it? | Effect in the current artifact | Remaining wiring / cost |
|---|---|---|---|
| `schemaVersion` | Yes | Must equal 2 before mapping. | — |
| `id` | Yes | DTO, `LevelGraph.LevelId`, ID maps, campaign navigation. | — |
| `name` | Yes | DTO and level-intro title. | — |
| `seed` | Yes | Seeds `GameSession` and solver RNG. | — |
| `meta.band` | Yes | Validator band rules/difficulty lookup; onboarding presentation uses it. | — |
| `meta.difficultyTarget` | Yes | Validator difficulty target and daily-ramp admission; no simulation rule. | — for its metadata role |
| `meta.mechanics` | Yes | Validator ordering/liveness metadata only; does not enable mechanics. | Per declared mechanic below |
| `meta.newMechanic` | Yes | Validator checks the declared mechanic against the solver trace; null skips. | Per declared mechanic below |
| `meta.teachingGoal` | Yes | DTO and daily JSON round-trip only. | S — tutorial/copy consumer if desired |
| `meta.minActionWindowTicks` | Yes | Blocking brittleness/accessibility validation. | — for validation; importer/schema optionality mismatch below |
| `meta.authoredBy` | Yes | DTO and daily JSON round-trip only. | XS/S — analytics/provenance consumer if desired |
| `meta.validatedAt` | Yes, optional | Blocking staleness validation when present. | — |
| `board.nodes[].id` | Yes | Dense node mapping, duplicate/reference checks, presentation IDs. | — |
| `board.nodes[].x`, `.y` | Yes | DTO geometry, validator geometry, board layout. | —; numeric-type mismatch below |
| `board.nodes[].queueCapacity` | Yes, optional | Missing maps to 8; graph/simulation/solver use it for node overloads. | — |
| `board.edges[].id`, `.from`, `.to` | Yes | Dense directed graph, reference checks, routing and track rendering. | — |
| `board.edges[].travelTicks` | Yes | Simulation/solver travel time and validation. | — |
| `board.edges[].oneWay` | **No** | Silently dropped. Graph edges are always directed `from` → `to`, even when JSON says false. | M — reverse traversal identity, solver/validator, direction visuals |
| `board.edges[].reversible` | **No** | Silently dropped; declared `reversible` mechanics are unobservable. | L — direction state/commands, replay, solver, UI |
| `sources[].nodeId` | Yes | Reference checked and mapped to the single `LevelGraph.SourceNode`. The collection, not this leaf, is pinned when it contains more than one source. | M — per-wave source mapping, simulation/solver/presentation |
| `sources[].allowedColors` | Yes | DTO, compatibility validation, daily generation, unknown/wild pin checks; simulation does not enforce wave membership. | S — enforce source/wave compatibility at import or emission |
| `stations[].nodeId`, `.accepts` | Yes | Station mapping, colour acceptance, station appearance. Wrong-colour behavior remains pinned. | — for matching; see mis-delivery proposal |
| `stations[].capacity` | Yes | Copied into `LevelGraph.StationCapacity` but never consumed by simulation or solver. | M — platform occupancy/overflow semantics, solver, UI |
| `switches[].id`, `.nodeId`, `.routes`, `.initialRoute` | Yes | Mapping/validation, deterministic toggle routing, lever presentation. | — |
| `switches[].cooldownTicks` | **No** | Silently dropped; declared `cooldown` mechanics are unobservable. | M — command eligibility/timing state, solver, feedback |
| `gates[].edgeId` | **No** | Entire optional `gates` array is ignored. | L — graph schedules, edge-entry semantics, solver, UI |
| `gates[].openWindows[][0]`, `[][1]` | **No** | No DTO, validation, or runtime schedule. | Included in gate work (L) |
| `gates[].previewTicks` | **No** | No DTO or preview consumer. | Included in gate work (L) |
| `waves[].tick`, `.color`, `.count`, `.spacingTicks` | Yes | Graph emission schedule used by simulation and solver. | — |
| `waves[].sourceNode` | Yes | DTO and reference check only; runtime emits every wave from the one global source. | Included in multi-source work (M) |
| `waves[].express` | **No** | Silently dropped; declared `express` mechanics are unobservable. | M — no-wait rule/failure semantics, solver, UI |
| `win.deliveries`, `.timeLimitTicks` | Yes | Simulation win and timeout conditions. | — |
| `win.perfectMaxSwitches` | Yes, optional | Maps to `LevelGraph` and the pure flip-mastery evaluator; absence maps to `Unbudgeted`. All 17 authored pars are solver-proved at or under par by a blocking corpus test. | — |
| `win.stars.two`, `.three` | Yes | DTO plus validator ordering only. There is no score-threshold award consumer; `FlipBudgetStatus.RatingStars` is a separate, unpersisted mastery display value. | M — implement score model, award/persistence, UI |
| `economy.baseTickets` | Yes | Daily wins assign it to `DailyTicketsEarned`. | — for the daily path |
| `economy.perfectBonus` | Yes | DTO/daily round-trip only; never awarded. | S — define qualifying result and ledger/persistence consumer |
| `tags[]` | **No** | Silently dropped; no DTO or consumer. | XS/S — choose analytics/browser/generator owner, then wire it |

### Mechanic declaration reality

The schema's eight `meta.mechanics` values are `switch`, `queue`, `second-source`, `wildcard`,
`cooldown`, `gate`, `express`, and `reversible`. Only `switch` and `queue` are observable in the
current Domain. The importer refuses a second source and wildcard colours; cooldown, gate,
express, and reversible fields are dropped. Metadata can therefore declare six mechanics that the
shipped simulation cannot exercise.

## `perfectMaxSwitches` fairness gate

`PerfectMaxSwitchesCorpusTests.AuthoredPar_HasASolverProvedWinningRun` enumerates the 17 authored
`L*.json` files, imports each through the real adapter, runs `LevelSolver` with a two-million-node
ceiling, and asserts both a solved verdict and `solve.SwitchesUsed <= perfectMaxSwitches`. All 17
pass. A future impossible par fails the suite; deleting the optional field leaves that level
explicitly ungated instead of assigning a permanently impossible top rating.

## Schema versus the direct runtime importer

`CorpusValidator` runs `SchemaStage`, but the game boot path calls `LevelImporter.Import` directly.
These differences therefore matter even when the offline corpus validator is healthy.

| Schema contract | Direct importer behavior | Consequence / repair |
|---|---|---|
| Defaults for `oneWay`, `cooldownTicks`, `previewTicks`, `spacingTicks`, and `express` | JSON Schema defaults are not materialized. `spacingTicks` is required by the importer; the other fields are dropped. | Align importer/defaulting with schema, or narrow the schema to implemented data. |
| Optional `meta.minActionWindowTicks` | Importer requires it. | Schema-valid omission fails runtime import; add the documented default or mark required. |
| Optional `stations[].capacity` | Importer requires it. | Schema-valid omission fails runtime import; define/import a default or mark required. |
| Optional `board.nodes[].queueCapacity` | Importer supplies 8 although the schema declares no default. | Document 8 in schema or make the runtime default explicit in shared policy. |
| Optional `win.perfectMaxSwitches` | Importer now maps absence to `FlipBudget.Unbudgeted` and enforces 0–200 when present. | Aligned. |
| Optional `economy`, `baseTickets`, `perfectBonus` | Importer requires the object and both leaves. | Schema-valid omission fails runtime import; add defaults or mark required. |
| `nodes[].x/y` type is `number` | Importer accepts integers only. | Decimal schema-valid coordinates fail runtime import; narrow schema or widen DTO/presentation types. |
| `sources` permits 1–6 | Importer returns `PinnedMechanic` above one. | Narrow schema until multi-source is implemented, or build the M-cost path above. |
| Full patterns/enums/minima/maxima and `additionalProperties: false` | Importer enforces selected types, caps, bounds, IDs, references, and pins, but does not execute the schema. It accepts unknown keys and misses several schema constraints. | Run a shared strict schema check at runtime, or deliberately duplicate every supported rule in the importer. |

## Task 14 dependency

Task 14 should not treat “the importer parsed the file” as evidence that a mechanic exists. Its
safe inputs today are the rows marked with a runtime effect. The first decisions before expanding
the ladder are the grouped cross-cutting mechanics: multi-source/per-wave source, cooldown,
gates, express trains, reversible traversal, and station capacity. Adding DTO fields alone would
leave the same silent gap this audit found.
