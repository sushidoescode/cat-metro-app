# Cat Metro — contract queue (backlog)

**Written:** 2026-08-02 · **Author:** product-analyst (agent), forge-decompose · **Status:** DRAFT for human ordering/approval · **Reviewed by:** human product owner (judgment) + evaluator (testability lint).

## What this file is

The human-ordered queue of **agent task contracts**. Each contract uses the field set of
`.github/ISSUE_TEMPLATE/agent-task.yml` (Goal · Spec reference · Acceptance criteria · Scope boundary ·
Assumptions · Stop conditions) and is sized for **one branch / one PR** (AGENTS.md hard rule 4).
A contract is done when every acceptance criterion maps to a passing check and
`docs/constitution.md:14-20` (Definition of Done) is satisfied.

This round decomposes **roadmap rows D2–D4 only** (`docs/plan/data/roadmap_56_days.csv:3,4,5` — the
Vertical Slice days). Nothing past D4 is in this file yet.

| Contract | Roadmap row | PRD requirements | ADRs |
|---|---|---|---|
| **CM-C1** | D2 engineering (`roadmap_56_days.csv:3`) | CM-R01, CM-R02 (pin-independent subset), CM-R03.1, CM-R07.3 | 0002, 0003, 0004, 0005 |
| **CM-C2** | D3 engineering + design (`roadmap_56_days.csv:4`) | CM-R07.1/.3, CM-R13.2, CM-R17 (partial), CM-R20.1, CM-R51.1, CM-R52 (perf) | 0003, 0004, 0005, 0007, 0008 |
| **CM-C3** | D4 engineering (`roadmap_56_days.csv:5`) | CM-R03.2, CM-R15 (D4 subset), CM-R16, CM-R17.1, CM-R22.3 (read-side) | 0002, 0007 |

---

## QUESTIONS A HUMAN MUST ANSWER (this queue is executable without them, but they bound it)

Nothing below is answered by an agent. These are the ones that touch **these three contracts**; the
full list is `docs/prd/PRD.md` §0 and §5 (57 open items).

| # | Question | Which contract it touches | Effect if unanswered |
|---|---|---|---|
| **Q-A** | **NEW-Q35** — wildcard resolution boundary. It changes **the command-log format**, not just rules (`docs/adr/0002-deterministic-fixed-tick-domain.md:160-162`). | CM-C1 | CM-C1 ships a *versioned* command-log envelope (`FormatVersion = 1`) and **no wildcard behaviour**. Resolution later extends the format at v2; it must not rewrite v1. |
| **Q-B** | **NEW-Q4** — reversing rejected cat meets an oncoming cat on a one-way edge (`docs/prd/PRD.md:114-118`). | CM-C1 | Station **rejection / reverse traversal is entirely out of CM-C1**; the code path is a loud `NotSupportedException` guard (criterion 12), never invented semantics. |
| **Q-C** | **NEW-Q5** — chain counter saturation + `PERFECT_BONUS_TICKETS` / `PERFECT_MAX_SWITCHES` values (`docs/prd/PRD.md:150`). | CM-C1 (excluded) | Scoring/chain is **out of CM-C1**; `Score`/`Chain` fields exist in the digest layout and stay 0 so the digest layout does not churn when scoring lands. |
| **Q-D** | **Does CM-C1 own all three `FailReason` members, or only `queue_overflow`?** The commissioning brief named "queue overflow fail"; CM-R03.1 requires the three-member enum and CM-C2's roadmap acceptance requires **platform overflow** to be raisable from the Domain (`roadmap_56_days.csv:4`). This decomposition assumes **all three enum members exist**; only `QueueOverflow` and `TimeOut` are *raisable* in this queue (see Q-J). | CM-C1, CM-C2 | Enum shape is settled either way by CM-R03.1. If the human says queue-only for the enum too, criterion 14's enum test is re-cut and the golden layout changes (`Outcome` byte). |
| **Q-E** | **Roadmap D3 says "Win by routing 10 cats"; the authored L001 has `win.deliveries: 2`** (`docs/plan/data/example_levels.json:15`) and CM-R13.2 pins L001's `initialRoute:1` / `minActionWindowTicks:16`. Which is L001? | CM-C2 | CM-C2 criterion C2-7 asserts **the level file's own `win.deliveries`**, not the literal 10. If the human wants 10, L001 is re-authored (a content change, not a code change). |
| **Q-F** | **A-19 / ADR-0008 §Open conflict** — no `district` field in schema v2. | CM-C2 (importer DTO shape) | CM-C2 imports schema v2 **exactly as it stands**; adding `meta.district` is a schema change → stop condition, not an agent edit. |
| **Q-G** | **Unity project scaffold** (6000.3.16f1, IL2CPP/ARM64/URP/Input System, minSdk 25 / targetSdk 36, package `com.catmetro.game`, keystore) is human-only and outside this queue (`docs/adr/0005-dotnet-first-dual-test-harness.md:16-19`). It must be created **in place at `unity/`** without deleting the sources CM-C1 lands under `unity/Assets/Scripts/Domain/`. | CM-C2 (BLOCKED-ON) | CM-C2 cannot start. CM-C1 is unaffected — that is the whole point of ADR-0005. |
| **Q-H** | **TG-5** — failure-screen copy voice (two LOCKED-conflicting string sets: `docs/prd/ux-flows.md:188` vs `docs/plan/specs/monetization_spec.md:173`). | CM-C3 | CM-C3 renders the `docs/plan/specs/product_spec.md:251-256` set that ux-flows marks **[LOCKED]**; if TG-5 picks the other voice it is a string-table edit, not a logic change. |
| **Q-I** | **Golden custody.** `tests/contract/replay-hash-golden.json` is an immutable path (AGENTS.md hard rule 1). Its content must be **committed by the human** inside CM-C1's PR (see criterion 11). | CM-C1 | CM-C1's PR cannot go green — by design (`docs/constitution.md:7`). |
| **Q-J** | **BLOCKING — what raises `platform_overflow` while NEW-Q4 pins rejection out?** The spec's *only* trigger for it is rejected cats: "Station platform: rejected cats exceeding station `capacity` → immediate **fail** (`fail_reason: platform_overflow`)" (`docs/plan/specs/product_spec.md:225`; CM-R02.3 at `docs/prd/PRD.md:113`). Q-B pins rejection entirely out of CM-C1, and with match-only acceptance an accepted cat is removed on arrival — so **no station can reach `capacity`** and `PlatformOverflow` is unreachable. Either the human ratifies an interim trigger in an ADR-0002 amendment before CM-C1 starts, or it stays unreachable. | CM-C1, CM-C2 | **Until answered:** `PlatformOverflow` stays a member of the enum (criterion 14 unaffected) but is **never raised** — any code path that would raise it throws `NotSupportedException` (criterion 14). CM-C1 13(f) and CM-C2 criterion 9 assert `Failed(QueueOverflow)`. The roadmap D3 acceptance "fail by platform overflow" (`roadmap_56_days.csv:4`) is **recorded as deferred, not met**, and re-opens when Q-J or NEW-Q4 lands. |
| **Q-K** | **`TimeOut` camera target — analyst-authored, needs ratification.** `TimeOut` has no causing node, so CM-C3 criterion 1 asserts a *presentation* rule the analyst chose: target = the node with the largest queue at the fail tick, ties broken by **lowest node id**. Nothing in the spec or ux-flows names this (A-C3-2). | CM-C3 | CM-C3 ships the authored rule as written. If the human overrules, criterion 1's `TimeOut` case is re-cut at **presentation cost only** — no Domain change, no golden change. |
| **Q-L** | **Template defect (human fix, hook/CODEOWNERS-gated path).** `.github/ISSUE_TEMPLATE/agent-task.yml:19-20` declares **Stop conditions** as `type: input` (single-line) with no `validations: required`, while every contract here supplies a 5–7 item list and the constitution treats stop conditions as load-bearing. No contract in this file can be filed through the template as written. | all three | The contracts carry their stop conditions in this file, so execution is unblocked. The template needs `type: textarea` + `validations: {required: true}`; an agent may not edit that path. |

**Unverified / not checked in this session:** no external market or platform claim is made in this file.
No user datapoints exist for Cat Metro as of 2026-08-02 (`docs/prd/PRD.md:64`); nothing here is derived
from user feedback.

---

## Ownership disjointness

Each contract owns a **disjoint set of file paths**. No two contracts write the same file, with one
enumerated exception class (registration files, below). Path ownership is the review test: a diff that
touches a path owned by another contract is out of scope (AGENTS.md hard rule 4).

**Resolution rule (implementable by a checker):** ownership of a changed path is decided by
**ordered longest-prefix match** over the globs below — the most specific matching glob wins, and the
`EXCEPT` clauses below are the explicit longer prefixes. A path matching no glob is unowned: a diff
touching it is out of scope for every contract in this queue.

| Contract | Owns (writes) |
|---|---|
| **CM-C1** | `unity/Assets/Scripts/Domain/**` · `unity/Assets/Tests/EditMode/Pure/Domain/**` · `dotnet/CatMetro.Domain/**` · `dotnet/CatMetro.Tests/**` · `dotnet/CatMetro.sln` · `dotnet/packages.lock.json` · `tests/domain/**` · `tests/fixtures/purity-bad/**` · `config/pins.json` · `scripts/check.sh` (banned-symbol block + the documented `--root` option only) |
| **CM-C2** | `unity/Assets/Scripts/Content/**` · `unity/Assets/Scripts/Application/**` · `unity/Assets/Scripts/Presentation/Board/**`, `/Input/**`, `/Diagnostics/**` · `unity/Assets/Scripts/Presentation/Hud/**` **EXCEPT `unity/Assets/Scripts/Presentation/Hud/WavePreview/**`** · `unity/Assets/Scripts/Bootstrap/**` · `unity/Assets/Scenes/Game*` · `unity/Assets/StreamingAssets/content/**` · `unity/Assets/Resources/Strings/ui.csv` (created here; see C2 criterion 8) · `unity/Assets/Tests/EditMode/Pure/Content/**` · `unity/Assets/Tests/EditMode/Engine/**` · `unity/Assets/Tests/PlayMode/Board/**` · `dotnet/CatMetro.Content/**` · `content/levels/**` · `tests/content/**` · `tests/unity/**` **EXCEPT `tests/unity/failure.test.sh`** |
| **CM-C3** | `unity/Assets/Scripts/Presentation/Failure/**` · `unity/Assets/Scripts/Presentation/Hud/WavePreview/**` · `unity/Assets/Scripts/Presentation/Camera/**` · `unity/Assets/Scripts/Application/Retry/**` · `unity/Assets/Tests/PlayMode/Failure/**` · `unity/Assets/Tests/EditMode/Pure/Retry/**` · `tests/unity/failure.test.sh` · **append-only rows** in `unity/Assets/Resources/Strings/ui.csv` (registration-exception class, below) |

**Enumerated exception (registration-only edits).** Because the contracts are **strictly sequential**,
CM-C2 and CM-C3 may append — never modify — registration lines in files another contract owns:
`dotnet/CatMetro.sln` (project entries), `dotnet/CatMetro.Tests/CatMetro.Tests.csproj`
(`ProjectReference` **only**), `dotnet/packages.lock.json` (regenerated), `config/pins.json` (new pin
rows), `scripts/check.sh` (new banned-symbol roots), and — for CM-C3 — **new rows** in
`unity/Assets/Resources/Strings/ui.csv` (never an edit to a row CM-C2 authored).

**No `Compile Include` append is permitted.** CM-C1's test-csproj glob
`unity/Assets/Tests/EditMode/Pure/**/*.cs` is **deliberately open-ended**: later contracts add test
folders *under* `EditMode/Pure/` (e.g. `Pure/Content/**`, `Pure/Retry/**`) and are picked up with no
csproj edit. Appending an `Include` would break CM-C1 criterion 2's equality assertion and the
test-split parity check that requires exactly that glob (`docs/adr/0005-dotnet-first-dual-test-harness.md:169-172`).

Any edit to CM-C1's Domain sources or to `tests/domain/determinism.test.sh` from a later contract is a
**stop condition**, not a merge conflict to resolve.

## Dependency order (human-ordered; do not parallelise)

```
CM-C1  (no dependency — runs today, no Unity, no licence, no human in the loop)
   └─> CM-C2  BLOCKED-ON: human Unity 6000.3.16f1 scaffold (Q-G) — outside this queue
          └─> CM-C3  DEPENDS-ON: CM-C2 merged
```

`state/mode` is **sprint** (`docs/prd/PRD.md:940`, A-00): ceremony is priced, the enforcement floor is
not. TDD for Domain code, immutable paths, `[CI]` criteria and the human-merge gate stand at full
strength for every contract below.

---

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

---

# CONTRACT CM-C2 — Greybox board + L001 load/render/play (Unity side)

**Roadmap:** D3, `docs/plan/data/roadmap_56_days.csv:4` (engineering: "Sources spawn trains; two-state
junction toggle; color+symbol station sinks; queue capacity + overflow fail; snapshot interpolation at
render rate; tap raycast with ≥48dp targets"; content: "L1 authored in schema v2 JSON … loaded through
the Content importer"; acceptance: "Win by routing 10 cats; fail by platform overflow; 60 fps on a
Pixel-6a-class device").

**DEPENDS-ON:** CM-C1 merged (the Domain and its determinism test must be green before anything renders it).
**BLOCKED-ON:** *human* Unity 6000.3.16f1 scaffold — outside this queue (Q-G). Required before start:
project created **in place at `unity/`** without removing `unity/Assets/Scripts/Domain/**`; IL2CPP,
ARM64, URP, Input System; **minSdk 25 / targetSdk 36**; package `com.catmetro.game`; keystore + Play App
Signing (`docs/plan/EXECUTION_PLAN.md:439`; ADR-0004:36). **minSdk anywhere is 25** — the roadmap's 24
is superseded by AMD-08 (`docs/plan/EXECUTION_PLAN.md:349-350`).

### Goal

L001 loads from its schema-v2 JSON file through the `CatMetro.Content` importer into immutable DTOs,
renders as a greybox board, and is playable to a win (routing its authored cat count) and to an
overflow fail — with taps driving the CM-C1 Domain through the command log and Presentation only
interpolating.

### Spec reference

`docs/prd/PRD.md` CM-R07.1/.3 (single gesture handler; tap → `ToggleSwitchCommand` at the next tick
boundary) · CM-R13.2 (L001 `initialRoute:1`, `minActionWindowTicks:16`) · CM-R20.1 (≥48dp) ·
CM-R51.1 (`targetSdk=36`, `minSdk=25`, `docs/prd/PRD.md:810`) · CM-R52 (perf) ·
`docs/prd/ux-flows.md` S-02 (`:148-198`, layout bands, hit rect, one gesture handler) ·
`docs/adr/0003-*` (Content/Application/Presentation rows) · `docs/adr/0005-*` (Content joins the dotnet
leg) · `docs/adr/0007-*` (UGUI+TMP, screen stack, Input System, no Addressables) ·
`docs/adr/0008-*` (JSON source of truth, immutable DTOs, `TypeNameHandling = None`, runtime bounds) ·
`docs/architecture/overview.md` §3 (tap → command → Step → snapshot → interpolate), §7 (layout).

### Acceptance criteria (12)

1. **L001 exists as shipped-source content.** `content/levels/L001.json` is schema-v2 valid and
   byte-equal in its authored fields to `docs/plan/data/example_levels.json:4-17`
   (`initialRoute: 1`, `minActionWindowTicks: 16`, `win.deliveries: 2`, `win.timeLimitTicks: 160`,
   4 nodes / 3 edges / 1 switch / 2 stations / 1 wave). *Check:* a dotnet test asserting each named
   field value.
2. **Importer produces immutable DTOs.** `CatMetro.Content` parses the file into `sealed` types with
   `readonly` fields; a test asserts (a) every array-bearing property is exposed as
   `ReadOnlyMemory<T>`/`ReadOnlySpan<T>` or an immutable view, (b) `TypeNameHandling` is `None`
   (grep assertion in `scripts/check.sh`), (c) `CatMetro.Content` builds under `netstandard2.1` in
   `dotnet/CatMetro.sln` with zero `UnityEngine` references (ADR-0008 parsing rules 1-2; ADR-0005).
3. **Runtime bounds and referential integrity.** Parsing rejects, with a typed error and **no
   exception escaping to the caller**, each of the following. Every bound is a **number stated here**,
   held in one constants class (`CatMetro.Content.ContentBounds`) — `config/runtime_bounds.json` does
   **not** exist yet and CM-C2 does not author it (stop condition 6):
   (a) a file over **`CONTENT_MAX_FILE_BYTES = 262144`** (256 KiB, `docs/adr/0006-save-format-purchase-ledger-and-runtime-bounds.md:190`);
   (b) JSON deeper than **`CONTENT_MAX_JSON_DEPTH = 16`** (`docs/adr/0006-...:191`; ADR-0008:93 repeats both);
   (c) `travelTicks` outside **1–40** (`docs/plan/data/level_schema.json:51`);
   (d) `win.timeLimitTicks` outside **20–4000** (`level_schema.json:125`);
   (e) `queueCapacity` outside **1–8** (`level_schema.json:40`);
   (f) `initialRoute` outside **0–2** **or** ≥ the switch's `routes.length` (`level_schema.json:87-88`);
   (g) a dangling `from`/`to`/`nodeId`/`sourceNode`/`routes[]` id (ADR-0008 rule 2).
   *Check:* one NUnit case per malformed fixture (7 cases, one per letter), each asserting the typed
   failure value and that no exception escapes.
4. **DTO → `Domain.LevelGraph` mapping is total and checked.** A test asserts every node, edge, switch,
   station and wave in L001 appears exactly once in the produced `LevelGraph`, ids resolve to the same
   indices in both directions, and the mapping allocates nothing after construction.
5. **Greybox render fidelity.** Loading L001 instantiates exactly one view object per authored board
   element (4 nodes incl. 1 source and 2 stations, 3 edges, 1 switch), each carrying the authored id and
   positioned at the authored grid coordinate. *Check:* an EditMode/PlayMode test enumerating the scene
   and comparing the id set and coordinates to the DTO.
6. **Tap targets ≥48dp and one gesture handler.** On the 360×640dp reference frame
   (`docs/prd/ux-flows.md:32`), every interactive element's effective hit rect is ≥48dp (CM-R20.1); the
   Game scene registers **exactly one** gesture handler and zero drag/pinch/long-press-to-aim handlers
   (CM-R07.1). *Check:* two automated UI tests (enumerate-and-measure; enumerate-and-assert-count).
7. **Tap → command → tick, and the frame log exists.** A tap on the junction (a) changes the lever's
   visual state on the first rendered frame after tap-down, and (b) appends exactly one
   `ToggleSwitchCommand` to the command log, applied at the next tick boundary (CM-R07.3;
   `docs/architecture/overview.md:129-137`). Two taps in one tick produce two entries in receipt order.
   **This criterion also delivers the instrumented frame log** that CM-C3 criteria 2, 4 and 7 measure
   against: `unity/Assets/Scripts/Presentation/Diagnostics/FrameLog.cs`, appending **one record per
   rendered frame** with exactly the fields `frameIndex:int`, `monotonicMs:long`, `simTick:int`,
   `screenState:string`. `monotonicMs` comes from **one** clock source, named in the file header and
   recorded in every artifact that cites the log; mixing clock sources is a defect.
   *Check:* a PlayMode test asserting log contents and applied tick; a frame-log assertion for the lever
   state; one test asserting the frame log emits a record per frame with all four fields populated and
   `monotonicMs` non-decreasing.
8. **Win by routing the authored cat count.** Playing L001 with the correct route delivers
   `win.deliveries` cats and the run ends `Won`, with the LOCKED banner string `"All cats home!"`
   (`docs/prd/ux-flows.md:188`) read from the string table
   **`unity/Assets/Resources/Strings/ui.csv`, which this contract creates** (CM-C3 appends rows to it;
   see the registration-exception class). Zero literal UI strings in components.
   **The asserted number is the level file's own `win.deliveries` (2 as authored), not the roadmap's
   "10" — see Q-E.** *Check:* one PlayMode test asserting outcome, banner text and that the text was
   resolved through `ui.csv`.
9. **Fail by overflow — queue overflow only (Q-J).** A scripted fixture board whose **node queue**
   reaches `queueCapacity` and is not cleared within the 16-tick Overload window ends the run with
   `Failed(QueueOverflow)`, and the fail state is rendered (banner present, board still visible).
   *Check:* one PlayMode test asserting outcome + banner presence.
   **Deferred, not met:** the roadmap D3 acceptance "fail by platform overflow"
   (`docs/plan/data/roadmap_56_days.csv:4`) is **unmeetable while NEW-Q4/Q-J are open** —
   `PlatformOverflow`'s only spec'd trigger is rejected cats (`docs/plan/specs/product_spec.md:225`),
   which is pinned out. The PR must record it as deferred and cite Q-J; it re-opens as a follow-up
   contract when Q-J lands.
10. **Presentation never simulates.** A static check asserts zero calls to `Simulation.Step` outside
    `CatMetro.Application` and the test assemblies; a unit test on the interpolator asserts that at a
    60 Hz render rate against an 8 tps sim the interpolation factor stays in `[0,1)` and monotonically
    increases between snapshots, resetting exactly once per tick (ADR-0002 §1;
    `docs/architecture/overview.md:147`). *Check:* grep assertion + one NUnit case in `EditMode/Pure/`.
11. **Manifest compliance.** The generated Android manifest declares `minSdkVersion=25` and
    `targetSdkVersion=36`; a check fails the build on any lower value (CM-R51.1, `docs/prd/PRD.md:810`;
    AMD-08). *Check:* a build-step assertion over the merged manifest, output pasted in the PR.
12. **60 fps on a Pixel-6a-class device — HUMAN-VERIFIED.** *An agent cannot run this; it is recorded as
    a human criterion with a fixed protocol.* On a Pixel-6a-class device, an IL2CPP/ARM64 release build
    playing L001 for **60 continuous seconds** records **median frame time ≤16.7 ms and 1%-low frame
    time ≤33.3 ms** via `adb shell dumpsys gfxinfo <pkg> framestats` or the Unity profiler. The run
    artifact (device model, build id, raw frametime table, both figures) is attached to the PR. The
    criterion fails if the artifact is absent, not merely if the numbers miss
    (`docs/plan/data/roadmap_56_days.csv:4`; CM-R52).

### Scope boundary

**In scope:** the paths in the ownership table for CM-C2, plus the registration-only appends listed in
the exception class (sln project entries · test-csproj `ProjectReference` **only** · pins · check.sh
banned-symbol roots). CM-C2 also creates the PlayMode/EditMode harness wrapper(s) under `tests/unity/`
(e.g. `tests/unity/editmode.test.sh`, ADR-0005:93) — **except** `tests/unity/failure.test.sh`, which is
CM-C3's.

**Explicit non-goals:**
- **No polish, no art pass, no audio, no haptics, no VFX** — greybox primitives only (D3 art is a
  separate lane; audio starts D6, `roadmap_56_days.csv:7`).
- **No fail/retry loop, no cause camera, no next-wave preview HUD, no win/results screen chrome** —
  that is **CM-C3**. CM-C2 renders the terminal state as a banner and stops.
- **No scoring/chain/star UI** (pin NEW-Q5).
- **No `catalog.json` / `content.sha256` / ContentSync / `contentHash` in save** — the full content
  pipeline (ADR-0008 §Source of truth) is a later contract; CM-C2 loads one file through
  `IContentSource`.
- **No solver, no 11-stage validator** (CM-R12).
- **No levels beyond L001**, no daily, no Night Harbor.
- **No SDK, no commerce, no ads, no analytics, no save** — `**/billing/**`, `**/iap/**`, `**/ads/**`
  are monetization tripwires requiring `state/mode=production` first (AGENTS.md Risky paths;
  `state/PROJECT_STATE.md:10`).
- **No edits to CM-C1's Domain sources or determinism test**, and **no `Compile Include` append** to
  `dotnet/CatMetro.Tests/CatMetro.Tests.csproj` — `Pure/Content/**` is already covered by CM-C1's glob
  (ownership section; ADR-0005:169-172).
- **No writes to `tests/contract/`, `docs/constitution.md`, `.claude/hooks/`, `scripts/git-hooks/`,
  `state/mode`, `evals/`** (AGENTS.md hard rule 1).
- **No schema change** — schema v2 is frozen for the window (ADR-0008 §Level schema v2 is frozen).

### Assumptions

- **A-C2-1** The human scaffold landed exactly as Q-G specifies; if any of IL2CPP/ARM64/URP/Input
  System/minSdk 25 differs, stop.
- **A-C2-2** `Newtonsoft.Json` is the parser, pinned to the version inside
  `com.unity.nuget.newtonsoft-json` (ADR-0004:54,70; ADR-0008). Adding it is covered by those ADRs and
  must be cited in the PR description.
- **A-C2-3** `IContentSource` and `IStorageRoot` are declared in `CatMetro.Services` and implemented in
  `CatMetro.Bootstrap` (ADR-0003:102-105). CM-C2 implements only what L001 loading needs; unused
  interface members are not stubbed speculatively.
- **A-C2-4** L001's authored `win.deliveries` is 2 (Q-E). The "10 cats" figure is treated as roadmap
  prose, not a criterion.
- **A-C2-5** The greybox uses colour **plus symbol** placeholders from the start, because colour-alone
  encoding is a merge-gate failure later (CM-R21.1); full triple-coding art is out of scope.

### Stop conditions

Defaults apply. Plus:
1. The Unity scaffold is missing, differs from Q-G, or has removed/moved `unity/Assets/Scripts/Domain/**` → stop.
2. Any level-schema field is needed that schema v2 does not have (notably `meta.district`, Q-F/A-19) → stop.
3. Any Domain behaviour change is needed to make a render or win/fail criterion pass → stop; that is a
   CM-C1 amendment and re-opens the golden.
4. A criterion cannot be met without touching a monetization path or an SDK → stop.
5. The 60 fps criterion cannot be evidenced because no device is available → stop and hand criterion 12
   to the human as an explicitly open item; do **not** mark it met from an editor measurement.
6. `config/runtime_bounds.json` does not exist (it is ADR-0006's artifact, `docs/adr/0006-...:166-169`).
   **This does not block criterion 3:** CM-C2 hard-codes the ADR-0006:190-191 and `level_schema.json`
   values behind the single `ContentBounds` constants class and does **not** author the file. Stop only
   to ask the human **who authors `config/runtime_bounds.json`** — CM-C2 or the save-format contract —
   and never invent a value that is not cited in criterion 3.
7. A bound needed by criterion 3 is absent from both ADR-0006 and `docs/plan/data/level_schema.json` →
   stop; do not choose a number.

---

# CONTRACT CM-C3 — Fail/retry loop: cause-first camera, sub-1s retry, next-wave preview

**Roadmap:** D4, `docs/plan/data/roadmap_56_days.csv:5` ("Win and fail states; cause-first failure
camera pans to the overflowed platform; instant retry <1 s without scene reload; next-wave preview
HUD"; acceptance: "Retry tap-to-playing under 1 s measured; failure cause visible within 1.5 s of fail").

**DEPENDS-ON:** CM-C2 merged (board render, input, level load).

### Goal

A failed run reframes the board on the node that caused the failure, a single tap returns the player to
`Playing` in under a second without a scene load, and the HUD shows the next two waves before they
arrive.

### Spec reference

`docs/prd/PRD.md` CM-R03.2 (fail strings with node/station substituted) · CM-R15 (**D4 subset only** —
camera reframe on the causing node; the A-23 ambiguity predicate, blame chip and ghost replay are
deferred) · CM-R16.1–.3 (retry <1 s; hit-testable from frame 1; switches restored to `initialRoute`) ·
CM-R17.1 (preview strip, display-only) · CM-R22.3 (motion toggle read side) ·
`docs/prd/ux-flows.md:43` (motion-off = **cut + static highlight ring**, never information loss),
`:254`, `:290` (`A11Y-S03-4`), `:258-270` (FailureReview layout + interaction), `:287` (`A11Y-S03-1`),
`:188` (LOCKED fail/win strings) · `docs/adr/0002-*` §9 (retry re-simulates from tick 0; no snapshot
format) · `docs/adr/0007-*` (screen stack, **not** scene loads; motion/haptics toggles).

### Acceptance criteria (11)

1. **Cause camera targets the failing node.** On `Failed(reason)`, the camera's target equals the node
   id that raised the failure (the overflowing node/station reported by the Domain outcome), asserted
   directly from the camera controller state. *Check:* one PlayMode test per reason:
   - `QueueOverflow` and `TimeOut` — driven by a **real Domain run** to the fail tick.
   - `PlatformOverflow` — **not raisable by the Domain while Q-J/NEW-Q4 are open** (CM-C1 criterion 14
     guards it). Driven instead by feeding the camera controller a **constructed presentation-level
     outcome**; the test asserts framing only, and the PR records that no Domain run reaches this state.
   - For `TimeOut` the target is **the node with the largest queue at the fail tick; ties broken by the
     lowest node id** — a deterministic rule, asserted rather than inferred. This rule is
     **analyst-authored (A-C3-2) and unratified — see Q-K.**
2. **Cause visible within 1.5 s.** Time from the fail tick to the frame in which the causal node is
   framed **and** the fail banner is rendered is **≤1500 ms**, p95 over 20 scripted failures
   (`docs/plan/data/roadmap_56_days.csv:5`), measured from the instrumented frame log CM-C2 criterion 7
   delivers (`unity/Assets/Scripts/Presentation/Diagnostics/FrameLog.cs`; records
   `frameIndex, monotonicMs, simTick, screenState`; the single clock source is named in the artifact).
   This is a **device-dependent perceptual budget**, so it has two legs, exactly as criterion 7:
   - **CI gate:** the editor PlayMode measurement, raw per-failure table attached to the PR.
   - **HUMAN-VERIFIED:** the same protocol repeated on a **low-tier and a mid-tier device**
     (`docs/prd/ux-flows.md:287`, A11Y-S03-1), with the same artifact requirement — device model, build
     id, raw per-failure table, p95. The criterion **fails if the artifact is absent**, not merely if
     the numbers miss. An editor-only measurement never satisfies this criterion (stop condition 7).
   *Check:* automated PlayMode measurement + the human device artifact.
3. **Motion-off is a cut plus a static ring.** With the Settings motion toggle OFF **or**
   `Settings.Global.ANIMATOR_DURATION_SCALE == 0` (each asserted independently — two cases), the camera
   reaches its final transform in **one frame** (zero interpolated pan frames) and a **static** highlight
   ring is rendered on the causal node with alpha > 0 and zero animation clips playing
   (`docs/prd/ux-flows.md:43,254,290`). *Check:* two PlayMode tests asserting frame count == 1, clip
   count == 0, ring present.
4. **Motion-on pans and still meets the budget.** With motion on, the camera interpolates (>1 frame of
   movement) and criterion 2's 1500 ms budget still holds — **under both of criterion 2's legs**: the
   editor PlayMode measurement is the CI gate, and the low/mid-tier device repetition is
   **HUMAN-VERIFIED** with the same artifact requirement. *Check:* one PlayMode test + the device
   artifact from criterion 2's run (motion-on is the default state measured there).
5. **No information is lost at motion-off.** The fail banner text, the causal node framing and the ring
   are all present in both motion states; a test asserts the rendered information set is identical
   across the two states (same banner string, same framed node id, ring vs. pulse being the only
   difference) (`docs/prd/ux-flows.md:290`). *Check:* one parameterised PlayMode test over both states.
6. **Retry is one input, live from frame 1.** `Try again` is hit-testable on the **first** frame of
   FailureReview (`docs/prd/ux-flows.md:265`; CM-R16.2). *Check:* one PlayMode test performing a
   hit-test on frame 1.
7. **Retry under 1 s, measured.** Tap-down on `Try again` → first frame in `Playing` is **<1000 ms**,
   p95 over 20 retries, measured from the instrumented frame log CM-C2 criterion 7 delivers
   (`Presentation/Diagnostics/FrameLog.cs`, same single clock source) on the editor target; the raw
   per-retry table is attached (CM-R16.1; `docs/prd/ux-flows.md:287`). The **low/mid-tier device**
   repetition of the same measurement is marked **HUMAN-VERIFIED** with the same protocol and artifact
   requirement (device model, build id, raw table, p95); the criterion fails if the artifact is absent.
   *Check:* automated table + human artifact.
8. **No scene reload on retry.** Across a retry, the count of scene loads/unloads is **0** and the scene
   handle is unchanged; navigation is stack-based (ADR-0007 §Navigation). *Check:* one PlayMode test
   asserting the load counter delta is 0.
9. **Retry restores tick-0 state and stays deterministic.** After retry, every switch equals its level
   `initialRoute`, the command log is empty, `state.Tick == 0`, and replaying the identical post-retry
   command sequence produces the identical replay hash as the same sequence from a fresh level entry
   (CM-R16.3; ADR-0002 §9 + CM-R01). *Check:* one PlayMode test for the state assertions plus one
   `EditMode/Pure` test for the hash equality. **If the two hashes differ, that is stop condition 7 —
    stop and report; never touch `tests/contract/replay-hash-golden.json` or any other immutable path.**
10. **Fail strings render with substitution.** Each fail reason renders its LOCKED string with
    the node/station name substituted — `"Platform overflowed at {node}"` / `"{station} platform
    overflowed"` / `"The last train left the depot"` — read from
    **`unity/Assets/Resources/Strings/ui.csv`** (created by CM-C2 criterion 8; CM-C3 **appends rows
    only**, never edits a CM-C2 row — registration-exception class), with **zero literal strings in UI
    components** (CM-R03.2; `docs/prd/ux-flows.md:188`; ADR-0007 §Localization).
    *Check:* one test per reason (3) + a grep assertion for literals. As in criterion 1, the
    `PlatformOverflow` case is driven by a **constructed presentation-level outcome** because the Domain
    cannot raise it while Q-J is open.
11. **Next-wave preview HUD.** At tick 0 the strip displays the **next two waves'** colour and count,
    contains **zero** interactive elements, sits in the top 0–15% band (outside the bottom-25% thumb
    zone), and updates as waves are consumed so it always shows the next two (or fewer, at the end)
    (CM-R17.1; `docs/prd/ux-flows.md:184`; CM-R07.4). *Check:* four assertions in one PlayMode test
    (content at tick 0; interactive-element count == 0; RectTransform band; content after wave 1 is
    consumed).

### Scope boundary

**In scope:** the paths in the ownership table for CM-C3, including its **own** harness wrapper
`tests/unity/failure.test.sh` (CM-C3 adds a new wrapper file; it never edits CM-C2's
`tests/unity/editmode.test.sh`) and **append-only rows** in `unity/Assets/Resources/Strings/ui.csv`.

**Explicit non-goals:**
- **No rewind sheet, no rewind chip, no eligibility rule, no `rewind_failure` placement** (CM-R08) —
  and note the invariant CM-C3 must not break: on attempt 1 **no** paywall/ad surface may even be
  constructed (`docs/prd/PRD.md:208`). CM-C3 ships **no** commerce surface at all.
- **No monetization of any kind** — `**/billing/**`, `**/iap/**`, `**/ads/**` require
  `state/mode=production` first (AGENTS.md Risky paths).
- **No ghost replay (3 s at 60% speed), no blame chip, no A-23 ambiguity predicate, no
  `ATTRIBUTION_MAX_RESIMS` re-simulation** — the full CM-R15 lands in a later contract; CM-C3 frames
  the node the Domain already names.
- **No results screen rollup** (score ticker, star pops, ticket count-up, footer row) — CM-R19.3 /
  UX-OPEN-03 / TG-4 are unresolved; CM-C3 renders the LOCKED win banner and nothing else.
- **No scoring, stars, or tickets** (pin NEW-Q5, NEW-Q7).
- **No settings screen** — CM-C3 **reads** the motion state; persistence across restart and the
  Settings UI (CM-R22.3, S-11) are out of scope.
- **No planning pause** (CM-R22.1, TG-2 unresolved).
- **No edits to CM-C1 Domain sources, CM-C2 importer, or the determinism test**, no `Compile Include`
  append to `dotnet/CatMetro.Tests/CatMetro.Tests.csproj` (`Pure/Retry/**` is already covered by
  CM-C1's glob), and **no edit to an existing row** of `unity/Assets/Resources/Strings/ui.csv` — new
  rows only.
- **No writes to `tests/contract/`, `docs/constitution.md`, `.claude/hooks/`, `scripts/git-hooks/`,
  `state/mode`, `evals/`** (AGENTS.md hard rule 1). Criterion 9 compares replay hashes; a mismatch is a
  finding to report, never a golden to edit.

### Assumptions

- **A-C3-1** The Domain's failure outcome carries the failing node id. If CM-C1 shipped `FailReason`
  without a node id, criterion 1 cannot be met without a Domain change → stop condition 3.
- **A-C3-2** `TimeOut` has no single causing node; the rule asserted in criterion 1 (**largest queue at
  the fail tick, ties broken by lowest node id**) is **analyst-authored and unratified — raised as Q-K**
  in the questions table. The human may overrule it: it is a presentation choice, not a sim rule, so the
  re-cut costs nothing outside `Presentation/Camera/**`.
- **A-C3-3** The motion state source is `(Settings motion toggle) OR (ANIMATOR_DURATION_SCALE == 0)`
  (`docs/prd/ux-flows.md:43`, PC-14). The toggle's storage is whatever CM-C2/Bootstrap already provides;
  CM-C3 does not introduce a save field.
- **A-C3-4** Fail-string voice follows the `docs/plan/specs/product_spec.md:251-256` set that ux-flows
  marks **[LOCKED]**; TG-5 may replace the strings later at string-table cost only (Q-H).
- **A-C3-6** The instrumented frame log criteria 2, 4 and 7 measure against is **CM-C2's deliverable**
  (`unity/Assets/Scripts/Presentation/Diagnostics/FrameLog.cs`, CM-C2 criterion 7), not CM-C3's. If it
  is absent or lacks `monotonicMs`/`simTick`, CM-C3 cannot measure and stops (stop condition 8) rather
  than writing a second clock source.
- **A-C3-5** "Instant retry" is implemented as re-entry to `Playing` by re-simulation from tick 0
  (ADR-0002 §9) — not by restoring a snapshot, because no snapshot format exists and none may be
  created.

### Stop conditions

Defaults apply. Plus:
1. Criterion 1 requires the Domain to report a node id it does not report → stop (CM-C1 amendment,
   golden-invalidating).
2. Any criterion appears to require the ghost replay, blame chip or ambiguity predicate → stop; those
   are out of scope and the criterion has been misread.
3. Any commerce/ad surface, placement fetch or entitlement check appears anywhere in the fail path →
   stop immediately (CM-R08.1 invariant + monetization tripwire).
4. The <1 s retry cannot be met without a scene load or a snapshot format → stop and report the
   measurement; do not weaken criterion 7 or 8.
5. TG-5 or TG-4 must be resolved to render a required string or CTA → stop and ask.
6. Motion-off behaviour would remove information (not just easing) to hit a budget → stop; that is an
   accessibility regression (`docs/prd/ux-flows.md:43`).
7. The post-retry replay hash differs from the fresh-entry hash (criterion 9) → **stop and report**;
   never touch `tests/contract/` or any other immutable path (AGENTS.md hard rule 1) — a hash mismatch
   is evidence of a retry-path defect, not a stale golden.
8. **No device available to evidence criteria 2, 4 or 7** → hand those criteria to the human as
   explicitly open; do **not** mark a device-dependent budget met from an editor measurement. Likewise
   if CM-C2's frame log is missing or single-clock-source cannot be shown (A-C3-6) → stop.
