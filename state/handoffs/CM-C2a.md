# Handoff — CM-C2a: Content importer (L001 + schema-v2 parse → immutable DTOs → LevelGraph)

**Session:** 2026-08-03 (sprint pricing: in-session TDD, one review round) · **Branch:** `task/cm-c2a-content-importer` (stacked on `forge/decompose-t2`) · **Status:** IN PROGRESS

## Restatement (doer's words)

Author `content/levels/L001.json` byte-true to the amended example; build `CatMetro.Content`
(netstandard2.1, engine-free, byte-fed — zero `System.IO`) parsing under a hardened non-polymorphic
Newtonsoft pipeline (single `JsonSerializerSettings` site, `TypeNameHandling.None`, `MaxDepth 16`,
256 KiB cap, duplicate-key rejection) into sealed immutable DTOs, bounds-checked against the cited
`ContentBounds` constants, referential-integrity-checked, then mapped totally onto the shipped
`CatMetro.Domain.LevelGraph` with a `ContentIdMap` as the round-trip surface — pin guards surfaced as
typed failures, never escaping exceptions. `CatMetro.Services.IContentSource` declared (not
implemented). All dotnet-tested; wrapper `tests/content/importer.test.sh`.

## Assumptions (contract A-C2a-1..5 adopted verbatim, plus doer extensions)

- **A-C2a-6 (doer):** a node's optional `queueCapacity` absent in the authored file maps to the
  schema max 8 — consistent with A-C2a-4's `QCapBound = 8` and CM-C1's L001-shape fixture (all-8s).
  Golden-neutral (the golden uses the in-code fixture, not the importer).
- **A-C2a-7 (doer):** Newtonsoft.Json pin = **13.0.2** — the version inside
  `com.unity.nuget.newtonsoft-json` 3.2.x (web-verified 2026-08-03; 13.0.2 fixes an ARM
  deserialization race relevant to Android). Recorded in config/pins.json.
- **A-C2a-8 (doer):** duplicate-key rejection uses Newtonsoft's
  `JsonLoadSettings.DuplicatePropertyNameHandling = Error` (present since 12.0.1) during the JToken
  load phase; DTO materialization then runs `ToObject` through the single settings site. Two-phase =
  full control over depth/dup/integer checks with one serializer site.
- **A-C2a-9 (doer):** integer-typed schema fields (ticks, counts, capacities, seed) are rejected
  with a typed failure when authored as float/exponent forms (fuzz class NaN/exponent) — asserted at
  the JToken walk before materialization.
- **A-C2a-10 (doer):** criterion 13's "wrapper extracts and compares" is implemented as the evidence
  procedure on `scripts/test.sh` output (the importer wrapper cannot invoke scripts/test.sh without
  recursion); `tests/content/importer.test.sh` itself gates `dotnet test` + the `[CI]` greps
  (single `new JsonSerializerSettings` occurrence; distinctive-literal ban under Content except
  `Content/Validation/**` and `Content/Daily/**`).
- **A-C2a-11 (doer):** `scripts/check.sh --root <dir>` now scans the union of the Domain ban list and
  the new Content ban patterns, so one negative fixture directory serves both blocks; the Content
  negative fixture is `tests/fixtures/content-bad/Banned.cs` (never compiled).

## Evidence (filled as criteria complete)

(pending)

---

## FROZEN CONTRACT (verbatim copy from state/backlog.md @ PR #6 head — review verifies against THIS)
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
