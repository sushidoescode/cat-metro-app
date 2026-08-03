# Handoff — CM-C1: Deterministic Domain skeleton + replay-hash stability test

**Session:** 2026-08-02 (sprint pricing: in-session TDD, one review round) · **Branch:** `task/cm-c1-domain-skeleton` (off `forge/specify-prd`) · **Status:** IN PROGRESS

## Restatement (doer's words)

Build the pure-C# `CatMetro.Domain` under the ADR-0005 dotnet-first harness: sources under
`unity/Assets/Scripts/Domain/`, NUnit tests under `unity/Assets/Tests/EditMode/Pure/Domain/`, linked
into `dotnet/` csprojs (netstandard2.1 lib / net8.0 tests), discovered by `scripts/test.sh` via
`tests/domain/determinism.test.sh`. Deliver the 14 criteria: exact pins, purity static-check with
negative fixture, PCG32-in-state, the 143-byte L001-shape digest contract, versioned command-log
envelope excluded from the hash, 7-of-8 tick-order boundary tests, four NotSupportedException pin
guards, three-member FailReason (two raisable), and the load-bearing replay-hash stability test whose
golden ONLY the human commits (`tests/contract/replay-hash-golden.json`). TDD: the failing suite is
committed before implementation. The PR stays red until the human lands the golden — by design.

## Assumptions (contract A-C1-1..7 restated as adopted, plus doer extensions)

- A-C1-1..A-C1-7 adopted exactly as written in the contract (frozen copy below).
- **A-C1-8 (doer, new) — micro-semantics not pinned by any source, chosen deterministically,
  documented in code, and kept OUT of the golden fixture's execution path** (the golden run has zero
  edge/queue contention, so the golden hash is insensitive to all three):
  (i) an edge mouth is *occupied* iff any train on that edge has `ProgressTicks == 0`;
  (ii) node queues release their FIFO head at step 4 if the outgoing mouth is free — one release per
  node per tick, releases resolve before same-tick arrivals join;
  (iii) a wave emission joins the back of the source queue if the queue is non-empty or the mouth is
  occupied; otherwise it enters the edge directly.
  Flagged for ratification in review; changing any of them later is golden-invalidating only if a
  future golden fixture exercises them.
- **A-C1-9 (doer, new) — `Microsoft.NET.Test.Sdk` (pinned exact) is required for `dotnet test` to
  discover NUnit3TestAdapter tests at all.** A-C1-5 names only NUnit + NUnit3TestAdapter; the test
  platform package is a mechanical precondition of the contract's own named check command, not a
  discretionary dependency. Recorded here and in the PR per hard rule 2; if the human rejects it, the
  alternative is NUnitLite + `dotnet run`, which contradicts the contract's literal `dotnet test`.
- **A-C1-10 (doer, new) — digest slot conventions within the criterion-8 layout:** train slots are
  1-based `Id` (0 = empty slot), `State` ∈ {0 None, 1 OnEdge, 2 AtNode}; a delivered train's slot is
  zeroed; colors map red=1 blue=2 yellow=3 green=4 (wild is construction-guarded per criterion 14).
  Node order = graph construction order; L001 fixture order SRC,J1,RED,BLU; edges E1,E2,E3; switch S1.
  These are golden-defining, asserted in the offset-table test, and reviewable there.
- **A-C1-11 (doer) — step-8 tie:** deliveries and time limit reached in the same tick → `Won` (the
  contract's "reaching `win.timeLimitTicks` first" wording; win is checked before time).
- **A-C1-12 (doer) — overflow trigger reading:** Overload raises when an enqueue brings a node queue
  to `count == queueCapacity` (the contract's "a node queue at queueCapacity raises Overload");
  cancel when count drops below capacity before the 16-tick timer expires; expiry with
  `count >= capacity` → `Failed(QueueOverflow)`. Queue count therefore never exceeds the digest's
  `qCap` slot bound.

## Golden fixture (defined here; construction is part of the golden's meaning — A-C1-2)

L001 shape from `docs/plan/data/example_levels.json:4-17`: nodes SRC(3,9) J1(3,6) RED(1,2) BLU(5,2);
edges E1 SRC→J1 travel 10, E2 J1→RED travel 12, E3 J1→BLU travel 12; source SRC (red); stations RED
accepts red cap 6, BLU accepts blue cap 6; switch S1@J1 routes [E2,E3] initialRoute 1; wave tick 8 red
count 2 spacing 20; win deliveries 2, timeLimitTicks 160; seed 1001; qCap 8 (schema max), nTrainsMax 2.
Command log (FormatVersion 1): `[Toggle(S1, tick 12)]` → applies tick 13 → route E2 → both cats
delivered at RED (ticks 30, 50) → `Won`. Zero contention, zero rejection, zero overload on this path.

## Evidence (filled as criteria complete)

(pending)

---

## FROZEN CONTRACT (verbatim copy from state/backlog.md at commit 9c45300 — review verifies against THIS)
# CONTRACT CM-C1 — Deterministic Domain skeleton + replay-hash stability test (tests first)

**Roadmap:** D2 engineering, `docs/plan/data/roadmap_56_days.csv:3` ("125 ms fixed tick loop; PCG32
seeded RNG; command log; EditMode replay-hash stability test"; acceptance: "replay-hash test green in
CI").
**Depends on:** nothing. **Blocked on:** nothing (this is the point of ADR-0005).

### Goal

A pure-C# `CatMetro.Domain` library and a `dotnet`-run NUnit suite exist such that replaying the same
`(levelId, seed, commandLog)` produces a byte-identical SHA-256 replay hash on every run — and the
suite is discovered by `bash scripts/test.sh` with no Unity installed.

### Spec reference

`docs/prd/PRD.md` CM-R01 (all 6 criteria; 1, 2, 5 in scope, 3, 4, 6 are device/perf and out) ·
CM-R02.1–.2 and CM-R02.5 (pin-independent boundaries only) · CM-R03.1 · CM-R07.3 ·
`docs/adr/0002-deterministic-fixed-tick-domain.md` §§1–8, 10 and §Locked-in ·
`docs/adr/0003-assembly-isolation-and-dependency-rule.md` (Domain row: **may reference nothing**) ·
`docs/adr/0004-toolchain-and-sdk-version-pins.md:56-72,90-96` (dotnet pin rows, `config/pins.json`) ·
`docs/adr/0005-dotnet-first-dual-test-harness.md` §Layout, §What runs where, §Consequences ·
`docs/architecture/overview.md` §6 (`SimulationState` field list and digest contract), §7 (repo layout).

### Acceptance criteria (14)

Each is independently checkable by the named command; a criterion is met only when the named check
exits 0 (or fails exactly as specified).

1. **Toolchain precondition recorded.** An 8.x .NET SDK is installed. If it is not, the contract stops
   (ADR-0005 §Explicit assumption, `:191-195`). *Check:* `dotnet --list-sdks | grep -E "^8\."` **exits 0**;
   that command and its verbatim output are pasted in the PR report.
2. **dotnet leg builds.** `dotnet build dotnet/CatMetro.sln -c Release` exits 0 with
   `dotnet/CatMetro.Domain/CatMetro.Domain.csproj` targeting **`netstandard2.1`** and
   `dotnet/CatMetro.Tests/CatMetro.Tests.csproj` targeting **`net8.0`**, the Domain csproj compiling
   sources via a link glob over `unity/Assets/Scripts/Domain/**/*.cs` and the test csproj via
   `unity/Assets/Tests/EditMode/Pure/**/*.cs` **and only that path** (ADR-0005 test-split parity,
   `:169-172`). *Check:* build exit code + a test that asserts each csproj's `Compile Include` string
   equals the specified glob.
3. **Pins are exact and locked.** `config/pins.json` records the .NET SDK major, both target
   frameworks, and the exact NUnit + NUnit3TestAdapter versions; `dotnet restore --locked-mode`
   against the committed `dotnet/packages.lock.json` exits 0; no `PackageReference Version` in
   `dotnet/**/*.csproj` contains a floating range (`*`, `[..)`, or `Version` omitted)
   (ADR-0004:72,90-96). *Check:* restore exit code + grep assertion over the csproj files.
4. **Harness discovery.** `tests/domain/determinism.test.sh` exists and exits 0 iff `dotnet test` is
   green (`scripts/test.sh:10-18` discovers it as `tests/**/*.test.sh`; ADR-0005:92-94). *Check:*
   `bash scripts/test.sh` **exits 0** and its stdout contains **both** the line
   `PASS tests/domain/determinism.test.sh` (`scripts/test.sh:13`) and a summary line matching
   `^test: ([0-9]+)/\1 passed` (`scripts/test.sh:24`). (`scripts/test.sh` prints no `found` count —
   only the per-test `PASS`/`FAIL` line and that summary.)
5. **Tick constants and loop.** `SimConstants.TicksPerSecond == 8` and `SimConstants.TickMilliseconds == 125`;
   stepping N times from tick 0 leaves `state.Tick == N` for N ∈ {1, 8, 100} (ADR-0002 §1).
   *Check:* three NUnit cases.
6. **Domain purity, statically enforced.** `scripts/check.sh` gains a banned-symbol block that scans
   **`*.cs` files only** and fails on any **word-boundary** match of
   `UnityEngine`, `DateTime`, `DateTimeOffset`, `Stopwatch`, `Environment.TickCount`, `System.Random`,
   `Guid.NewGuid`, `RandomNumberGenerator`, `float`, `double`, `decimal`, `System.Numerics` under
   `unity/Assets/Scripts/Domain/**`, and of `UnityEngine`/`UnityEngine.TestTools` under
   `unity/Assets/Tests/EditMode/Pure/**` (CM-R01.2, `docs/prd/PRD.md:99`; ADR-0002 §3,5;
   ADR-0005:102-106). The match rule is exactly
   `grep -rEn --include='*.cs' '\b(UnityEngine|DateTime|DateTimeOffset|Stopwatch|Environment\.TickCount|System\.Random|Guid\.NewGuid|RandomNumberGenerator|float|double|decimal|System\.Numerics)\b'`
   — comments and string literals are **in scope by design** (a banned symbol named in a comment is
   still a review signal); the word boundary is what keeps `floating` and `doubled` from matching.
   `scripts/check.sh` also gains an optional **`--root <dir>`** flag, documented in its header comment,
   which **replaces** the default scan roots for this block only. *Check:* two runs, both evidenced in
   the PR — (a) `bash scripts/check.sh` exits **0** on the clean tree; (b)
   `bash scripts/check.sh --root tests/fixtures/purity-bad` exits **non-zero** and prints a message
   naming the offending symbol and file. The fixture lives at `tests/fixtures/purity-bad/Banned.cs`
   containing `double x;` and is **outside every default scan root**, so it can never fail run (a).
7. **PCG32 is the only RNG and is part of the state.** Two `Pcg32` values constructed from the same
   seed emit an identical sequence over **2000 draws** (CM-R01.5, `docs/prd/PRD.md:102`); the `Rng`
   struct is a field of `SimulationState`, proven by a test asserting `WriteDigest` output differs
   before vs. after a single draw with all other fields held equal (ADR-0002 §4). *Check:* two NUnit
   cases.
8. **Digest layout is the contract.** `SimulationState.WriteDigest(Span<byte>)` writes fields in the
   order and widths of `docs/architecture/overview.md:312-320`
   (`Tick, Score, Chain, Deliveries, Rejections, Overloads, SwitchesUsed, Rng{State,Inc}, SwitchRoutes,
   NodeQueues, OverloadTimers, Trains{Id,Color,EdgeId,ProgressTicks,NodeId,State}, Outcome`), canonical
   **little-endian** (ADR-0002 §7). The cited source is **fixed-layout, not fixed-length** — three of the
   fields are arrays — so the digest is **not** a single constant. The contract fixes the length as a
   pure function of the level shape, written in the test source as:

   ```
   DigestLength(nSwitches, nNodes, nTrainsMax, qCap)
     = 28                        // 7 × int32: Tick Score Chain Deliveries Rejections Overloads SwitchesUsed
     + 16                        // Rng: State:ulong + Inc:ulong
     + nSwitches * 1             // SwitchRoutes: byte per switch
     + nNodes * (1 + 2 * qCap)   // NodeQueues: 1-byte live count + qCap × short slots, unused slots written 0
     + nNodes * 2                // OverloadTimers: short per node
     + nTrainsMax * 10           // Trains: Id:2 Color:1 EdgeId:2 ProgressTicks:2 NodeId:2 State:1
     + 2                         // Outcome: 1-byte tag + 1-byte FailReason (0 when not Failed)
     = 46 + nSwitches + nNodes * (3 + 2 * qCap) + 10 * nTrainsMax
   ```

   For the A-C1-2 code fixture (L001 shape: `nSwitches=1`, `nNodes=4`, `nTrainsMax=2` = sum of wave
   `count`, `qCap=8` = the schema max, `docs/plan/data/level_schema.json:40`) this evaluates to
   **143 bytes**. `Score` and `Chain` are present and remain `0` in this contract (scoring is out of
   scope, Q-C). *Check:* (a) one test asserting `WriteDigest` length equals `DigestLength(...)` on
   **three** fixture shapes (1/4/2/8 → 143; 0-switch; 2-switch multi-train), and (b) one test asserting
   each field's byte offset on the golden fixture against an offset table written in the test.
   **The two padding constants — the per-node queue slot count (`qCap`) and the train-array bound
   (`nTrainsMax`) — are not derivable from ADR-0002 §7** (see A-C1-7). If the architect will not ratify
   them as written, that is **stop condition 6**, not an agent decision.
9. **Command log: versioned envelope, ordered application.** `CommandLog` carries
   `FormatVersion` with value **1** and an append-only array of
   `ToggleSwitchCommand { ushort SwitchId, int Tick }` (ADR-0002 §6). A command enqueued at any point
   during tick *t* applies at step 1 of tick *t+1*, and two commands enqueued in the same tick appear
   and apply in receipt order (CM-R07.3, `docs/prd/PRD.md:197`). *Check:* three NUnit cases (single
   command boundary; two-command ordering; `FormatVersion == 1`).
10. **The command-log envelope is outside the hash.** Two runs with byte-identical `Entries` but
    different `FormatVersion` values produce the **same** replay hash — i.e. the hash is defined over
    per-tick `SimulationState` digests only (ADR-0002 §7). This is what makes a NEW-Q35-driven format
    bump additive rather than golden-breaking. *Check:* one NUnit case.
11. **Replay-hash stability — the load-bearing test.** For a fixture triple `(levelId, seed, commandLog)`:
    (a) two in-process replays produce an identical **64-lowercase-hex** string; (b) two separate
    `dotnet test` process invocations produce the identical string; (c) a command log differing by one
    entry produces a **different** string; (d) the string is compared **byte-for-byte** against
    `tests/contract/replay-hash-golden.json` (CM-R01.1, `docs/prd/PRD.md:98`; ADR-0002 §7).
    **Emission contract for (b):** the NUnit test prints to stdout **exactly one line** of the form
    `REPLAY_HASH=<64 lowercase hex>` (regex `^REPLAY_HASH=[0-9a-f]{64}$`, no other line may start with
    `REPLAY_HASH=`). `tests/domain/determinism.test.sh` runs `dotnet test` **twice** in independent
    processes, `grep -E`s that line out of each run, and `diff`s the two values — the wrapper **fails**
    if either line is missing, if a run emits more than one such line, or if the two differ.
    *Check:* the four assertions in `tests/domain/determinism.test.sh`.
12. **Golden hand-off, agent never writes the golden.** When `tests/contract/replay-hash-golden.json`
    is absent or does not match, the test **fails** and prints (i) the computed hash as the criterion-11
    `REPLAY_HASH=` line and (ii) the exact JSON document to commit, delimited by the literal marker
    lines `GOLDEN_JSON_BEGIN` and `GOLDEN_JSON_END` on their own lines, so the human can extract it
    mechanically (`sed -n '/^GOLDEN_JSON_BEGIN$/,/^GOLDEN_JSON_END$/p'`) with no hand-editing.
    The agent's diff contains **zero** changes under `tests/contract/`
    (AGENTS.md hard rule 1; ADR-0005:95-101; ADR-0002 §Locked-in 2). The PR is merged only after a
    **human-authored commit** on the same branch adds the golden and CI turns green. *Check:* `git diff --name-only`
    on the agent's commits shows no `tests/contract/` path; the failing-then-green CI transition is the
    evidence.
13. **Pin-independent step subset — seven of the eight step boundaries.** The authoritative tick order
    has **eight** steps (`docs/plan/specs/product_spec.md:218-227` via CM-R02.2). This contract asserts
    **seven**; **step 7 (score/combo) is deferred with scoring** (Q-C, pin NEW-Q5) and is covered only
    as `Score == 0` / `Chain == 0` in criterion 8. Each test below asserts an observable state delta at
    exactly one boundary:
    (a) **step 1, commands** — `SwitchRoutes[i]` flips at the commands step and `SwitchesUsed` increments;
    (b) **step 2, waves** — a wave authored at tick *t* with `count` and `spacingTicks` emits its trains at
    ticks *t, t+spacing, …* and at no other tick;
    (c) **step 3, advance** — an in-transit train's `ProgressTicks` increments by exactly 1 per tick;
    (d) **step 4, node arrival** — at `travelTicks` the train leaves the edge and is enqueued at the node.
    **Sub-assertion of the same boundary (not a ninth step):** a train departing a junction takes the
    edge named by the current `SwitchRoutes` value, not the authored `initialRoute`, once toggled
    (`docs/plan/specs/product_spec.md:221` puts routing inside step 4);
    (e) **step 5, station acceptance (match only)** — a train whose colour is in `station.accepts` is
    removed and `Deliveries` increments by 1. Non-matching arrival is pinned out (Q-B, criterion 14);
    (f) **step 6, overflow** — a node queue at `queueCapacity` raises Overload with a **16-tick** timer;
    clearing space before tick 16 cancels it and clears the timer; not clearing ends the run with
    `Failed(QueueOverflow)` (CM-R02.5, `docs/prd/PRD.md:119`). **`PlatformOverflow` is not raisable in
    this contract and is not asserted here — see Q-J;** the enum member still exists (criterion 14);
    (g) **step 8, win/time** — reaching `win.deliveries` yields `Won`; reaching `win.timeLimitTicks` first
    yields `Failed(TimeOut)`.
    *Check:* **eight** NUnit cases named `Step_<Boundary>_<Assertion>` (seven boundaries; step 4 has two
    cases — arrival and routing), all run by `dotnet test`.
14. **Fail reasons and pin guards.** `FailReason` has exactly three members
    (`QueueOverflow, PlatformOverflow, TimeOut`) and a contract test fails if a fourth is added
    (CM-R03.1, ADR-0002 §10). **Four** guard tests assert that the **pinned** behaviours are absent and
    loud, not silently invented — each throws `NotSupportedException` whose message names the blocking
    pin: a non-matching cat arriving at a station (`NEW-Q4`), a level graph with more than one source
    node (second-source scope), a train with the `wild` colour (`NEW-Q35`), and **any code path that
    would set `Outcome = Failed(PlatformOverflow)`** (`NEW-Q4`/Q-J — the member exists so the digest
    layout and the enum test are stable, but nothing may raise it until Q-J is answered).
    *Check:* one enum test + four `Assert.Throws` cases.

### Scope boundary

**In scope (files this contract creates/edits — nothing else):**
- `unity/Assets/Scripts/Domain/**/*.cs` — `SimConstants`, `SimulationState`, `Pcg32`, `Command` /
  `ToggleSwitchCommand` / `CommandLog`, `LevelGraph` (the Domain's own integer board type — see
  assumption A-C1-1), `Simulation.Step`, `FailReason` / `SimOutcome`, `ReplayHasher`.
- `unity/Assets/Tests/EditMode/Pure/Domain/**/*.cs` — the NUnit suite.
- `dotnet/CatMetro.Domain/CatMetro.Domain.csproj`, `dotnet/CatMetro.Tests/CatMetro.Tests.csproj`,
  `dotnet/CatMetro.sln`, `dotnet/packages.lock.json`.
- `tests/domain/determinism.test.sh`.
- `tests/fixtures/purity-bad/Banned.cs` — the negative fixture for criterion 6 (never compiled by any
  csproj; outside every default scan root).
- `config/pins.json` (dotnet rows only).
- `scripts/check.sh` (append the banned-symbol block and the documented `--root <dir>` flag; do not
  restructure the file).

**Explicit non-goals (out of scope — a diff touching these is a failed review):**
- **No Unity anything.** No `.asmdef`, no `.meta`, no `ProjectSettings/`, no `Packages/manifest.json`,
  no scene, no `UnityEngine` reference. (The asmdef field name `noEngineReferences` is explicitly
  unverified — ADR-0003 rule 2 — and is the human scaffold's job.)
- **No wildcard, no second source, no reversible/express/gate/cooldown mechanics** (pin NEW-Q35 and
  post-launch bands; CM-R06.1).
- **No station rejection / reverse traversal / bounce-back** (pin NEW-Q4; CM-R02.3–.4).
- **No scoring, chain, chain bonus, time bonus, Perfect Flow, stars** (pin NEW-Q5; CM-R04). The
  `Score`/`Chain` fields exist in the digest and stay 0.
- **No solver, no validator, no beam search, no level JSON parsing, no content pipeline** (CM-R12,
  ADR-0008 — later contracts).
- **No rewind, no attribution/re-simulation, no `ATTRIBUTION_MAX_RESIMS` work** (ADR-0002 §9 —
  CM-C3 and later).
- **No save, no ledger, no analytics, no SDK, no config/economy_defaults.json.**
- **No writes to `tests/contract/`, `docs/constitution.md`, `.claude/hooks/`, `scripts/git-hooks/`,
  `state/mode`, `evals/`** (AGENTS.md hard rule 1).

### Assumptions (the executing agent restates and extends these before coding)

- **A-C1-1 — the Domain owns its own board type.** ADR-0003 says `CatMetro.Domain` may reference
  *nothing*, while ADR-0008 says level DTOs live in `CatMetro.Content`. Therefore the Domain defines an
  integer-only `LevelGraph` and `CatMetro.Content` (CM-C2) maps DTO → `LevelGraph`. This is forced by
  the two ADRs read together, but it is **not written down in either** — if the implementer reads it
  differently, stop and ask.
- **A-C1-2 — fixtures are constructed in code**, not loaded from JSON, because parsing is CM-C2's.
  The fixture triple used for the golden is therefore defined in test source and its construction is
  part of the golden's meaning: changing the fixture changes the golden.
- **A-C1-3 — all three `FailReason` members exist; only two are raisable** (see Q-D and **Q-J**). The
  brief named only queue overflow and CM-R03.1's enum test needs three members, so all three are
  declared. `PlatformOverflow` is **declared but guarded** (criterion 14): its only spec'd trigger is
  rejected cats (`docs/plan/specs/product_spec.md:225`), which Q-B pins out, so it cannot be raised
  without inventing a rule. CM-C2's roadmap acceptance ("fail by platform overflow") is **deferred**,
  not met. Flagged for the human rather than silently chosen.
- **A-C1-4 — `CommandLog.FormatVersion` is an addition to ADR-0002 §6**, which specifies the record but
  no envelope. It is additive and excluded from the hash (criterion 10); if the architect considers it
  an external-contract change it needs an ADR amendment.
- **A-C1-5 — NUnit + NUnit3TestAdapter are new dependencies** and are covered by ADR-0005/ADR-0004;
  the PR description must cite them (AGENTS.md hard rule 2). No other package may be added.
- **A-C1-6 — PCG32 is written from the published construction**, cited in the file header; no licensed
  code is copied (ADR-0002 §Spend).
- **A-C1-7 — the digest's two padding constants are analyst-authored** (criterion 8). ADR-0002 §7 fixes
  the *layout* and the endianness but not the *length*: `overview.md:315-318` lists `SwitchRoutes`,
  `NodeQueues`, `OverloadTimers` and `Trains` as arrays. Making the digest byte-comparable requires two
  choices no source makes — (i) each node's queue is written as a 1-byte live count plus `qCap` fixed
  `short` slots with unused slots zeroed, `qCap` = the level's per-node `queueCapacity` bound
  (`docs/plan/data/level_schema.json:40`, max 8); (ii) `Trains` is written as a fixed array of
  `nTrainsMax` = the sum of authored wave `count`s. Both are **golden-defining**: changing either
  changes every hash. If the architect prefers a different sizing rule, that is an ADR-0002 amendment
  and **stop condition 6** — do not pick one silently.

### Stop conditions

Defaults always apply (schema change · new dependency · criteria conflict → stop and ask). Plus:
1. Any need to decide **NEW-Q4, NEW-Q5 or NEW-Q35** to make a test pass → stop. The guards in
   criterion 14 exist so this is impossible to do by accident.
2. Any temptation to write, edit or "regenerate" `tests/contract/replay-hash-golden.json` → stop; hand
   the value to the human (criterion 12).
3. The determinism test cannot be made to fail first (TDD ordering broken) → stop; the failing test is
   the deliverable, not the implementation.
4. `dotnet --list-sdks` shows no 8.x → stop and report (ADR-0005 falsifier).
5. Any requirement to add a `UnityEngine` reference, a float, a clock read or a second RNG inside
   `CatMetro.Domain` → stop; that is an ADR-0002 amendment, not a task decision.
6. The digest layout in `docs/architecture/overview.md:312-320` proves insufficient for a criterion —
   **including the criterion-8 length formula and its two padding constants (A-C1-7)** — → stop; layout
   and length changes are golden-invalidating and human-gated. Do not invent a third sizing rule.
7. Any code path would need to raise `Failed(PlatformOverflow)` to make a criterion pass → stop and
   report (Q-J). The criterion-14 guard exists so this cannot happen by accident.

