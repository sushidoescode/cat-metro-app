# ADR-0008: Content pipeline — solver-validated JSON levels, immutable DTOs, schema v2, content hash

- **Status:** Proposed (ratifies `docs/plan/specs/architecture.md:18`; contains one **open schema conflict** the human must resolve)
- **Date:** 2026-08-02
- **Relates:** ADR-0002 (the solver is the sim), ADR-0003 (`CatMetro.Content` is engine-free), ADR-0005 (validation runs licence-free), ADR-0006 (`contentHash` in the save).

## Context

Content is the product's other half: **30 campaign levels + 10 Night Harbor levels (L901-L910) shipped
in the build, 90 daily dates pre-validated in CI, and a 30-board dated backup pool shipped in the
build** — every one of them **solver-proven solvable free**, the mechanically-enforced half of the
fairness posture (`docs/prd/PRD.md:54`). Those are three distinct quantities and conflating them
mis-scopes the pipeline: the 30-board pool is the *offline fallback* the device serves when boot
validation fails, while the daily pipeline pre-validates the **next 90 dates** on every content/sim PR
and nightly (CM-R46, `docs/prd/PRD.md:728`; ADR-0009 `validate-dailies`). The validation pipeline is an 11-stage merge gate
(CM-R12, `docs/prd/PRD.md:264-274`), the schema already exists at v2
(`docs/plan/data/level_schema.json`), and the source format is settled: "Levels as validated JSON in
StreamingAssets, imported to immutable DTOs at load; content hash in save… No remote-content failure
path during the event" (`docs/plan/specs/architecture.md:18`).

Two things force decisions this document must actually make. First, **content is on the boot path and
some of it is generated**, so RK-34 (`docs/prd/risks.md:125`) assigns the architect explicit parser
rules. Second, the schema has a known hole: **there is no `district` field** despite district-based
unlock being locked, and `L901`-`L910` breaks any implicit id-range mapping (A-19,
`docs/prd/PRD.md:959`).

## Decision

### Source of truth and flow

```
docs/plan/data/*.json  (authoring)  →  content/levels/*.json  (shipped source of truth, schema v2)
        │                                        │
        │                          CI: 11-stage validation (dotnet, no Unity — ADR-0005)
        │                                        │
        └── generator (dailies, 90 dates) ───────┴──► unity/Assets/StreamingAssets/content/
                                                          levels/*.json
                                                          daily_overrides.json
                                                          daily_backup_pool.json   (30 hand-validated)
                                                          catalog.json             (index + per-file sha256)
                                                          content.sha256           (hash of catalog.json)

config/runtime_bounds.json  (authored, ADR-0006 §4) ──► unity/Assets/StreamingAssets/config/
                                                          runtime_bounds.json      (verbatim byte copy)
```

- **The JSON file is the single source of truth.** ScriptableObjects, if ever generated, are a
  build-time cache only (`docs/plan/data/level_schema.json:4`) — never authored, never diverging.
- **`catalog.json`** lists every content file with its SHA-256. **`content.sha256`** is the hash of
  `catalog.json` and is what lands in the save's `contentHash` (ADR-0006 §2), so a save can always
  say which corpus produced it. On mismatch the app does not refuse to run: it logs
  `content_hash_changed` and re-validates progress against the new catalog.
- **Loading goes through `IContentSource`** (ADR-0003), because Android keeps `StreamingAssets`
  inside the compressed APK where `System.IO` cannot reach it. `CatMetro.Content` receives bytes and
  stays engine-free — which is what lets the entire validator and fuzz corpus run in the fast
  `dotnet` leg.
- **`config/runtime_bounds.json` ships to `StreamingAssets/config/`, and it is *not* content.**
  ADR-0009's required `ci` check asserts "`config/runtime_bounds.json` ↔ StreamingAssets
  byte-identity" (`docs/adr/0009-ci-topology-and-secret-custody.md:33`); that assertion previously had
  no source path in any document. It has one now:
  - **Destination:** `unity/Assets/StreamingAssets/config/runtime_bounds.json` — a sibling of
    `content/`, deliberately **outside** it.
  - **Copy step:** `CatMetro.Editor`'s `ContentSync` — the same editor/CLI step that stages
    `content/` into `StreamingAssets/content/` copies this one file verbatim. It is a byte copy: no
    re-serialization, no key reordering, no formatting pass, because the `ci` assertion is
    byte-identity and any prettifier would break it.
  - **It is *not* indexed in `catalog.json` and therefore *not* folded into `content.sha256`.** This
    is deliberate. `content.sha256` is persisted in the save as `contentHash` and answers "which level
    corpus produced this save?"; folding a bounds file into it would make an `[ARCH]` constant tweak
    look like a corpus change, firing `content_hash_changed` and a progress re-validation on every
    installed device for a change that touched no level. The two artifacts are verified by two
    different mechanisms and that separation is the point: **content integrity → `catalog.json` +
    `content.sha256`; bounds integrity → the `ci` byte-identity assertion.** The authored file under
    `config/` remains the single source of truth (ADR-0006 §4) and the shipped copy is a build
    artifact that must never be hand-edited.
  - Runtime bounds are read through `IContentSource` like everything else in `StreamingAssets`, so
    `CatMetro.Content` stays engine-free while reading them.

### Immutable DTOs

Parsed levels become `sealed` types with `readonly` fields and `ReadOnlyMemory<T>`/`ReadOnlySpan<T>`
views over the arrays, constructed once at load and never mutated. `SimulationState` (ADR-0002) holds
*indices into* the level DTO, not copies. A level DTO is safe to share across the runtime, the
solver and the capture rig with no defensive copying, which is what keeps `Playing` allocation-free.

### Parsing rules — **MUST**, per RK-34

1. **No polymorphic or type-name deserialization, ever.** Explicit typed models only.
   `TypeNameHandling` stays `None`, asserted by a static check in `scripts/check.sh`, and this is a
   permanent rule, not a default someone may relax "for schema flexibility"
   (`docs/prd/risks.md:125`).
2. **Runtime bounds validation independent of the CI schema gate**, because CI only validates what CI
   saw. Before parsing: `CONTENT_MAX_FILE_BYTES` (256 KiB) and `MaxDepth = CONTENT_MAX_JSON_DEPTH`
   (16) (ADR-0006 §4). After parsing: every schema v2 bound re-checked at runtime — nodes ≤40,
   edges ≤70, waves ≤30, switches ≤10, sources ≤6, stations ≤6, `travelTicks` 1-40,
   `timeLimitTicks` 20-4000 (`docs/plan/data/level_schema.json:34,45,108,81,60,70,51,125`) — plus
   referential integrity: every `from`/`to`/`nodeId`/`sourceNode`/`routes[]` id resolves, and
   `initialRoute` is in range.
3. **Every content load is wrapped**: a parse or bounds failure falls back to the dated backup pool
   (dailies) or to Home with a logged `error_caught(domain=content_parse)` — never a crash. Content
   parsing is the likeliest boot-path crash source and crash-free ≥99.5% is never-cut.
4. **A fuzz corpus of malformed and adversarial level JSON runs in CI** (truncation, depth bombs,
   huge counts, duplicate ids, dangling references, NaN/exponent numerics, duplicate keys, BOM/encoding
   oddities). It is a `dotnet` test, so it runs on every PR.
5. **Untrusted seeds are not content.** Deep-link and share-code seeds are typed and range-checked
   (fixed-width unsigned) and **never string-concatenated into a path or resource name** (RK-27,
   `docs/prd/risks.md:104`).

### Validation as a merge gate

All 11 stages of CM-R12 run in CI on every content PR and block merge
(`docs/prd/PRD.md:268`): schema → static analysis → lower-bound feasibility → solver → triviality
reject → brittleness/accessibility → star check → difficulty ±0.05 → novelty → staleness → human
playtest (the last being the one stage CI cannot run; it is a checklist artifact, and it depends on
D-6). The solver is BFS for ≤2-switch boards and beam search at widths 1k/2.5k/5k beyond, **sharing
the exact Domain step function** (`docs/plan/specs/architecture.md:53`), and a human witness replay
is admissible proof where beam search fails (`docs/plan/specs/product_spec.md:632`).

**`meta.validatedAt` handling (AMD-09 / NEW-Q9):** the field is typed `"string"` with no null allowed
(`docs/plan/data/level_schema.json:25`), so tooling **deletes the key** when a level is not yet
validated — it never writes `null` (`docs/prd/PRD.md:274`). The staleness stage compares
`validatedAt` against the last sim/schema change; an absent key is treated as stale, which is the
safe reading.

### Level schema v2 is frozen for the window

Schema v2 is the shipped contract (`schemaVersion: const 2`). No field is added or removed in-window
without an ADR amendment plus a re-validation of every level — because `meta.validatedAt` staleness
(CM-R12.5) makes a schema change invalidate the entire corpus by design.

## Open conflict — requires a human decision (A-19)

**District-based unlock is locked** ("district N+1 is unreachable until district N has 5 completions",
CM-R09.4, `docs/prd/PRD.md:227`) but **the schema has no `district` field**, and `L901`-`L910` (Night
Harbor) breaks any implicit id-range mapping (`docs/prd/PRD.md:959`). Options:

- **(A) Add `meta.district` (integer 1-7) to schema v2.** Explicit, greppable, survives Night Harbor
  and any future bonus district. Cost: a schema change that by rule invalidates `validatedAt` on every
  authored level — cheapest **now**, while almost nothing is authored, and near-impossible later.
- **(B) Derive district from id ranges** (`L001`-`L005` = 1, …, `L026`-`L030` = 6, `L9xx` = Night
  Harbor). Zero schema change. Cost: an implicit convention encoded in code that breaks the first time
  a level is renumbered or a district changes size, with no validator able to catch it.
- **(C) A separate `content/districts.json` mapping district → ordered level ids.** No schema change,
  explicit, and it also gives the map screen its ordering. Cost: a second file that can disagree with
  the levels, needing its own validation stage.

**Architect's recommendation: (A), decided before level authoring starts.** It is the only option
where the validator can enforce the unlock rule, and the schema-change cost is strictly increasing
with time. **This is a schema decision — it goes through the human ADR gate.**

## Alternatives seriously considered

- **ScriptableObjects as the authored format.** Real advantages: the Unity-native path, inspector
  authoring, no parsing at runtime, no boot-path parse risk at all. Lost because SOs are binary-ish
  YAML that diffs badly, cannot be generated or validated outside the editor, and would drag the
  entire validation pipeline back inside a licensed Unity job — destroying the licence-free
  content-PR gate (ADR-0005) that makes CM-R12 affordable on every PR.
- **Levels as compiled C# / code-generated tables.** Real advantages: zero parse cost, compile-time
  referential integrity, no runtime bounds needed for in-build levels. Lost because *dailies are
  generated at runtime-adjacent times* and the backup pool + `daily_overrides.json` are data by
  nature — so a parser exists regardless, and having two content paths is worse than having one
  hardened one.
- **Addressables/remote content packs.** Covered and rejected in ADR-0007; the decisive point here is
  the same one: "no remote-content failure path during the event"
  (`docs/plan/specs/architecture.md:18`).
- **`System.Text.Json` instead of Newtonsoft.** Real advantages: no third-party dependency, faster,
  and no `TypeNameHandling` footgun to police. Genuinely tempting. Lost on IL2CPP risk: its
  reflection-based path needs source-generation or careful stripping configuration to be safe under
  AOT, and `com.unity.nuget.newtonsoft-json` is the path with the most Unity/IL2CPP mileage —
  including in the SDKs we already ship. We take the dependency (ADR-0004) and pay for it with an
  enforced `TypeNameHandling = None` check, which is a smaller risk than an AOT surprise in launch
  week. **Revisit post-window.**
- **JSON Schema validation at runtime** (running the same ajv-equivalent on device). Real advantage:
  one validation definition for CI and runtime. Lost: it is a heavyweight dependency on the boot path
  to re-check what CI already proved for in-build content; the runtime needs *bounds* checking
  (rule 2), which is 60 lines and covers the actual risk — a file CI never saw.
- **Validating dailies on a server.** Lost: there is no server, by design
  (`docs/prd/risks.md:145`), and adding one re-opens the whole threat-model class.

## Consequences

**Easier.** A level is a diffable text file that an agent can author, a validator can prove, and a
reviewer can read. The whole 11-stage gate is a fast `dotnet` job. Content and code ship in one
artifact, so there is no version-skew failure mode between them. Because the solver *is* the sim, a
CI-green level cannot be unsolvable on device — the property CM-R02 exists to guarantee.

**Harder.** Any sim change invalidates content (`validatedAt` staleness) and requires a re-validation
pass — that is intended friction, but it is friction. All content ships in the AAB, so the ≤60 MB
budget is a content budget too. Levels 31-60 post-launch mean app updates, not content pushes.

**Locked in — declare irreversible, human ADR gate:**
1. **Level schema v2 shape** — authored content is written against it, and every change invalidates
   the corpus. The `district` question above must therefore be settled *before* authoring, not after.
2. **`content.sha256` / `catalog.json` format** — it is persisted in the save (`contentHash`).
3. **The `content/` StreamingAssets path layout** — it is baked into the shipped app and the loader.

**Blocking gap:** the per-level ticket schedule has three conflicting sources and is a human decision
(**NEW-Q7**, `docs/prd/PRD.md:232`) — the schema's `economy.baseTickets`/`perfectBonus` fields exist,
but the authoritative table `data/level_ticket_schedule.csv` does not. Content authoring can start;
economy values cannot be finalised. Similarly **NEW-Q1** (flat 45-90 s invariant vs a per-band range
table) decides whether `data/difficulty_bands.csv` exists as a validated input.

## Security notes

- **Trust classification, explicitly:** in-build levels are *trusted* (they passed CI and shipped in a
  signed artifact); `daily_overrides.json` and generated boards are *semi-trusted* (we generated them,
  but not necessarily in this build); deep-link seeds and share codes are **untrusted**. The parser
  applies the same bounds to all three, because the difference between them is provenance, not
  format — and a truncated asset read produces the same bytes as a hostile one.
- **RK-34 closed by rules 1-4** above, with one honest residual: the runtime bounds and the CI schema
  are two definitions of "valid", and they can drift. The fuzz corpus is what keeps them honest, and
  the bounds live in `config/runtime_bounds.json` (ADR-0006) so they are reviewable in one place.
- **The 200 ms boot validation budget** for today's and tomorrow's boards (CM-R46.4,
  `docs/prd/PRD.md:733`) is enforced against *bounded* inputs only — pathological boards are rejected
  by the bounds check before the solver-lite ever runs, which is what stops RK-34's OOM/ANR scenario
  on the low tier.
- **No content path ever produces executable behaviour.** No type names, no expressions, no scripting
  in level data. A level can only describe nodes, edges, waves and win conditions — this is a property
  worth defending in every future review.
