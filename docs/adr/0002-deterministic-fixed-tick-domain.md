# ADR-0002: Deterministic pure-C# fixed-tick Domain (8 tps, PCG32, command log, integer state)

- **Status:** Proposed (ratifies `docs/plan/specs/architecture.md:16,46-53`, settled thinking — not reopened here)
- **Date:** 2026-08-02
- **Supersedes / relates:** none. Depended on by ADR-0003 (assemblies), ADR-0005 (test harness), ADR-0008 (content).

## Context

The simulation *is* the product. Five separate deliverables collapse if it is not bit-identical
everywhere: the Daily Line ("the same board for everyone", no server — `docs/prd/PRD.md:96`), the
solver-validated level pipeline ("a level that solves in CI cannot be unsolvable on device" —
`docs/prd/PRD.md:108`), rewind (`docs/prd/PRD.md:211`), the replay-driven capture rig
(`docs/plan/specs/architecture.md:61`), and Cup anti-P2W enforcement
(`docs/plan/specs/liveops_spec.md:53,85` via `docs/prd/PRD.md:96`).

The choice is already made upstream and is LOCKED: "Fixed-tick deterministic sim, 8 ticks/s (125 ms),
pure C#, PCG32-seeded, command-logged; presentation interpolates; wall clock never read inside the
tick" (`docs/plan/specs/product_spec.md:204-206`); "TDD for all Domain code (the sim is the product —
replay-hash tests are non-negotiable)" (`docs/plan/EXECUTION_PLAN.md:52-53`). This ADR converts that
into a buildable contract and decides the three things the source documents left implicit: the
numeric representation, the replay-hash definition, and how rewind/attribution get their earlier
states.

## Decision

We will implement `CatMetro.Domain` as a pure-C# library with **no engine, no wall clock, no floating
point, and one RNG**, exposing a single step function that the runtime, the solver, the validator and
the capture rig all call.

1. **Tick.** `SimConstants.TicksPerSecond = 8`, `SimConstants.TickMilliseconds = 125`. Fixed. The
   Domain has no concept of a frame; Presentation interpolates between successive `SimulationState`
   snapshots at render rate (`docs/plan/specs/architecture.md:48`).
2. **Step function.** `public static void Step(ref SimulationState state, ReadOnlySpan<Command> commandsThisTick)`
   in `CatMetro.Domain.Simulation`. It implements the 8-step order at
   `docs/plan/specs/product_spec.md:216-227` verbatim. **There is exactly one implementation**; the
   solver, the batch validator and the runtime call this symbol (CM-R02.1, `docs/prd/PRD.md:111`).
3. **State representation: integer only.** Every field of `SimulationState` is `int`/`short`/`byte`/
   `ulong`. `float`, `double`, `decimal`, `Mathf`, `Math.*` transcendentals and `System.Numerics` are
   **banned in `CatMetro.Domain`**, enforced by a static check in `scripts/check.sh`. No fixed-point
   library is introduced (see alternatives).
4. **RNG.** A `Pcg32` struct (state + inc, both `ulong`) is the only RNG reachable from Domain code.
   It is seeded from the level `seed` (`docs/plan/data/level_schema.json:12`) and **its state is a
   field of `SimulationState`**, so it is covered by the replay hash. `System.Random`,
   `UnityEngine.Random`, `Guid.NewGuid` and `RandomNumberGenerator` are banned in Domain
   (CM-R01.5, `docs/prd/PRD.md:102`).
5. **No clock.** `IClock` (ADR-0003) is never referenced from `CatMetro.Domain`. `DateTime`,
   `DateTimeOffset`, `Environment.TickCount`, `Stopwatch` and `UnityEngine.Time.*` are banned there
   (CM-R01.2, `docs/prd/PRD.md:99`).
6. **Commands.** `readonly struct ToggleSwitchCommand(ushort SwitchId, int Tick)`. Taps append to an
   append-only `CommandLog` (a growable array of 8-byte records) and are applied at step 1 of the
   next tick boundary, in receipt order (CM-R07.3, `docs/prd/PRD.md:197`).
7. **Replay hash — the definition CI asserts against.** `SimulationState` exposes
   `void WriteDigest(Span<byte> destination)` writing a canonical **little-endian**, fixed-layout
   byte image of the full state. A run's replay hash is an *incremental* SHA-256 over the
   concatenation of the per-tick digests, rendered as 64 lowercase hex chars. The digest layout is
   part of the contract: reordering fields changes every golden.
   The hash is computed only in test/validator paths — never in `Playing` (CM-R01.6 zero-alloc,
   `docs/prd/PRD.md:103`).
8. **Determinism inputs are exactly three:** `(levelId, seed, commandLog)` → identical outcome
   (`docs/plan/specs/product_spec.md:229-230`).
9. **Rewind and cause-attribution get earlier states by re-simulation, not by snapshots.** To reach
   tick *k*, replay from tick 0 with the command log truncated at *k*. A level is bounded at
   `win.timeLimitTicks ≤ 4000` (`docs/plan/data/level_schema.json:125`), so the worst case is one
   4000-tick re-run of an allocation-free integer loop. **No snapshot serialization format exists**,
   therefore none has to be versioned or migrated.
   The A-23 ambiguity predicate (`docs/prd/PRD.md:306`) is implemented as re-runs over the candidate
   set = distinct routing decisions in the trailing 24 ticks. The *theoretical* size of that set is
   `C_max = switches × 24 = 10 × 24 = 240` (`docs/plan/data/level_schema.json:81`), and at
   `timeLimitTicks ≤ 4000` that is ≈9.6 × 10⁵ tick-steps — **not** affordable inside the 3 s
   ghost-replay window (`docs/prd/PRD.md:311`) on the low tier. So the re-run count is **capped by a
   number, not by a judgement**:
   - **`ATTRIBUTION_MAX_RESIMS = 24`**, pinned in `config/runtime_bounds.json` (ADR-0006 §4) — the
     same file the tests read. Candidates are evaluated **newest-first** (nearest the failure tick),
     because the causal decision is by construction the *last* routing decision affecting the causal
     cat (`docs/prd/PRD.md:304`), so the cap truncates the least-likely candidates.
   - **Reaching the cap is a hard trigger, not a fallback anyone decides at runtime.** On the 24th
     re-run without a resolved single averting decision, attribution stops and renders the
     already-legal ambiguous branch — camera framed on the node, **zero blame chips, zero blame
     text** (`docs/prd/PRD.md:310`). This is fail-safe: the failure mode of running out of budget is
     identical to the failure mode of genuine ambiguity, which the PRD already enumerates and tests.
     There is no stall branch and no "the implementer decides whether it was fast enough" branch.
   The implementer still measures wall time at the vertical slice and reports it; a *lower* cap is an
   ordinary ADR amendment. The cap itself is no longer deferred.
10. **Outcome type.** `enum FailReason { QueueOverflow, PlatformOverflow, TimeOut }` — exactly three,
    contract-tested (CM-R03.1, `docs/prd/PRD.md:129`). `SimOutcome` is `Running | Won | Failed(FailReason)`.

## Alternatives seriously considered

- **Unity physics / NavMesh / DOTS-ECS as the sim.** Real advantage: free spatial queries, mature
  tooling, and ECS would give excellent cache behaviour for hundreds of commuters. Lost because every
  one of them is float-based and engine-coupled: cross-platform float determinism between an
  IL2CPP/ARM64 device and an x64 CI host is not guaranteed, which makes the CM-R01.1 golden-hash gate
  a coin flip, and engine coupling would make ADR-0005's licence-free CI impossible. `docs/plan/EXECUTION_PLAN.md:14-15`
  already lists "no physics/NavMesh" as the plan of record.
- **Float state plus a deterministic math library (e.g. software-emulated fp).** Real advantage: the
  natural way to express sub-tick positions and smooth speeds; keeps content authoring in familiar
  units. Lost because it buys precision no launch rule needs while adding a whole determinism
  attack surface — and it is the single most common source of "the golden hash only fails on one
  device" bugs, which we cannot afford to debug inside this window.
- **Q16.16 fixed-point state (the "integer/fixed-point" phrasing at `docs/plan/specs/architecture.md:16`).**
  Real advantage: headroom for a future mechanic (variable speed, acceleration, sub-tick interpolation
  authored in content). Lost on the constitution's sizing rule (`docs/constitution.md:8`): every
  authored quantity in schema v2 is already an integer — `travelTicks` 1-40, `spacingTicks` 1-40,
  `queueCapacity` 1-8, station `capacity` 1-12, `count` 1-8
  (`docs/plan/data/level_schema.json:51,116,40,76,115`) — so fixed-point would carry rounding rules,
  a mul/div helper and a second numeric mental model for zero named requirement. Adding Q16.16 later
  is additive and cheap; removing it later is not. **This is the one place we deviate in letter from
  architecture.md:16 while keeping its intent (no floats).**
- **Snapshot-based rewind (serialize `SimulationState` every N ticks).** Real advantage: O(1) rewind
  regardless of level length, and it is what most games do. Lost because it creates a *second*
  persisted binary format that must version alongside the save (ADR-0006) and stay in lockstep with
  the sim, for a saving of at most one 4000-tick integer loop. Re-simulation is the smaller system
  and it is exercised by the same tests that already prove determinism.
- **Higher tick rate (e.g. 30 tps) for smoother feel.** Lost: 8 tps is LOCKED
  (`docs/plan/specs/product_spec.md:204`) and every authored tick quantity in the corpus —
  `minActionWindowTicks` 6, the 16-tick overload ring, the 8-tick rejection dwell — is expressed in
  125 ms units. Presentation interpolation buys the smoothness instead.
- **Variable timestep with an accumulator.** Lost: 125 ms is the game's read rhythm, not an
  implementation detail; a variable step re-introduces wall-clock dependence at the exact boundary we
  need clean.

## Consequences

**Easier.** The solver, the 11-stage validator (CM-R12), the daily-seed pre-validation job (CM-R46),
the capture rig and the CI determinism gate all run the production sim with no engine and no device.
Bug repro becomes "attach the command log". `dotnet`-only CI (ADR-0005) becomes possible at all.

**Harder.** Anything with genuine sub-tick physicality (springs, easing authored in content, variable
train speeds) is off the table inside the Domain and must live in Presentation as pure visual
interpolation. Contributors must internalise "no float, no clock, no `Random`" — hence the static
check rather than a code-review convention.

**Locked in — declare irreversible, human ADR gate:**

These are the sharpest irreversibles in the whole ADR set — sharper than the save schema, because
they invalidate *every authored level and every checked-in golden simultaneously* — so they carry the
same gate heading as ADR-0003 §Locked in, ADR-0006 §Locked in and ADR-0008 §Locked in, not a softer
one.

1. The **per-tick order of operations** is the semantics of all 40 shipped levels + 30 pre-validated
   dailies. Changing it invalidates every authored level; the `meta.validatedAt` staleness gate
   (CM-R12.5, `docs/prd/PRD.md:272`) exists precisely to make that failure loud rather than silent.
2. The **replay-hash digest layout** is a de-facto external contract: the goldens are checked in and
   the device-tier cross-check compares against them. Changing the layout is a **golden
   regeneration**, and goldens under `tests/contract/` are **human-authored commits only** — see
   `docs/adr/0009-ci-topology-and-secret-custody.md:66-69` ("regenerating a golden is a deliberate
   human act with a diff, which is the only thing standing between 'the sim changed' and 'the test
   was moved'"). An agent may never close a determinism failure by regenerating the expectation.
3. `FailReason`'s three members are published in player-facing copy and in the analytics taxonomy.

**Spend / licence / lock-in:** none. No dependency is added by this ADR. PCG32 is ~40 lines we write
ourselves (public-domain algorithm, implemented from the published construction — the implementer
cites the source in the file header and does not copy licensed code).

**Blocking gaps — the step function cannot be finalised until these are answered by the human
(they are already open PRD pins, not new questions):**
- **NEW-Q4** — step 5 behaviour when a reversing rejected cat meets an oncoming cat on the same
  one-way edge (`docs/prd/PRD.md:114-118`). Three authored branches, all mapping onto the existing
  three fail reasons.
- **NEW-Q35** — wildcard resolution boundary: commands-boundary (appears in the command log) vs
  station-acceptance boundary (does not) (`docs/prd/PRD.md:121`, `docs/prd/PRD.md:910`). This changes
  the command-log schema, so it is on the critical path for the *format*, not just the rules.
- **NEW-Q5** — whether the chain counter saturates at 5 (`docs/prd/PRD.md:150`).
Everything else in this ADR — tick rate, integer state, PCG32, command log, digest, re-simulation
rewind, banned-symbol list — is buildable today. Steps 1-4 and 6-8 of the tick order are unambiguous.

## Security notes

- **No new trust boundary.** The Domain consumes only in-build level DTOs and the local command log.
  Untrusted input (deep-link seeds, share codes) reaches it only after the validation layer in
  ADR-0008; the Domain itself performs no parsing.
- **The replay hash is an integrity check against our own goldens, not an anti-cheat.** The save is
  plaintext and editable (RK-21, `docs/prd/risks.md:84`). No published claim may describe determinism
  or the ledger as tamper resistance (CM-R56.4). Cup fairness stays command-log-derived and
  cosmetic-only (`docs/prd/risks.md:145`).
- **Determinism leaks nothing.** The daily seed is derived from a public date key; a shared seed
  reveals only the board every player already sees.
- **Denial of service via re-simulation:** rewind bounds its work by `win.timeLimitTicks`
  (schema-capped at 4000). Attribution is bounded on **both** axes, which matters because the window
  alone does not bound the *count*: the trailing 24-tick window caps how far back candidates are
  drawn from, but the candidate set inside it is as large as
  `C_max = switches × 24 = 10 × 24 = 240` (`docs/plan/data/level_schema.json:81`) — so the window
  alone would still admit ≈9.6 × 10⁵ tick-steps of work. The re-run count is therefore capped
  independently at `ATTRIBUTION_MAX_RESIMS = 24` (§9, `config/runtime_bounds.json`, ADR-0006 §4),
  and hitting the cap renders the ambiguous branch rather than continuing. A hostile or pathological
  level cannot make either axis unbounded: the runtime content bounds in ADR-0008 reject
  out-of-range `switches`/`timeLimitTicks` before the sim ever sees the board.
