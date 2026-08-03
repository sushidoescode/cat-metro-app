# ADR-0005: dotnet-first dual test harness for the engine-free assemblies

- **Status:** Proposed — **NEW decision, not in `docs/plan/specs/architecture.md`**
- **Date:** 2026-08-02
- **Relates:** ADR-0002 (Domain purity is what makes this possible), ADR-0003 (which assemblies), ADR-0009 (CI topology).

## Context

Four facts collide:

1. **The first contract is a determinism test, and it is due before anything else exists.**
   "Domain skeleton: asmdef layout, 125 ms tick loop, PCG32, command log, **EditMode replay-hash
   stability test (TDD — write the failing test first)**" is Day 1-3 work
   (`docs/plan/EXECUTION_PLAN.md:444-445`); "TDD is mandatory for Domain code… the replay-hash test
   must stay green in CI at all times" (`docs/plan/EXECUTION_PLAN.md:474-475`).
2. **The Unity scaffold is a serial dependency on a human.** Unity install, licence activation,
   project creation, keystore, EDM4U import (`docs/plan/EXECUTION_PLAN.md:439-442`) — none of it is
   agent-reachable, and the Day-1 critical path is Play Console work, not Unity
   (`docs/plan/EXECUTION_PLAN.md:513-515`).
3. **Unity in CI needs a licence.** Every Unity CI action needs credentials in the runner, on a repo
   whose secret posture is the single "irreversible item" in the risk register (RK-33,
   `docs/prd/risks.md:124`), under a solo ruleset posture where any write-capable principal can merge
   a workflow change (ADR-0001 security notes).
4. **The house test interface is already fixed and stack-agnostic:** `scripts/test.sh` discovers
   `tests/**/*.test.sh` and a test passes iff it exits 0 (`scripts/test.sh:2,18`). It is designed to
   route to an engine runner later, and `tests/contract/` is immutable (AGENTS.md hard rule 1).

ADR-0002 and ADR-0003 make four assemblies — Domain, Content, Services, Application — contain **zero
`UnityEngine` references by construction**. Code with no engine references does not need an engine to
compile or a licence to test. That is a fact we can spend.

## Decision

We will build and test the four engine-free assemblies with the **plain .NET SDK first**, from the
**same source files** that Unity will later compile through its asmdefs. One source tree, two
compilers, two test runners, one set of tests.

### Layout

```
unity/                                  Unity project root (created later, by a human)
  Assets/Scripts/Domain/**/*.cs         ← CatMetro.Domain          (asmdef, noEngineReferences)
  Assets/Scripts/Content/**/*.cs        ← CatMetro.Content         (asmdef, noEngineReferences)
  Assets/Scripts/Services/**/*.cs       ← CatMetro.Services        (asmdef, noEngineReferences)
  Assets/Scripts/Application/**/*.cs    ← CatMetro.Application     (asmdef, noEngineReferences)
  Assets/Tests/EditMode/Pure/**/*.cs    ← CatMetro.Tests.EditMode  (asmdef) — engine-free, LINKED by dotnet
  Assets/Tests/EditMode/Engine/**/*.cs  ← CatMetro.Tests.EditMode  (asmdef) — may use UnityEngine, NEVER linked
dotnet/
  CatMetro.Domain/CatMetro.Domain.csproj              netstandard2.1, <Compile Include="../../unity/Assets/Scripts/Domain/**/*.cs"/>
  CatMetro.Content/…                                  netstandard2.1  (+ Newtonsoft.Json)
  CatMetro.Services/… CatMetro.Application/…          netstandard2.1
  CatMetro.Tests/CatMetro.Tests.csproj                net8.0, NUnit + NUnit3TestAdapter,
                                                      <Compile Include="../../unity/Assets/Tests/EditMode/Pure/**/*.cs"/>
  packages.lock.json                                  committed; exact NuGet versions (ADR-0004 pin hygiene)
  CatMetro.sln
tests/domain/determinism.test.sh                      wrapper: exits 0 iff `dotnet test` is green
tests/contract/replay-hash-golden.json                immutable, human-authored (see below)
config/runtime_bounds.json                            single source for the [ARCH] constants (ADR-0006)
```

Load-bearing details:

- **Sources live under `unity/Assets/...` and the csproj *links* them** via a relative glob. Unity is
  the pickier host (it must own the files and their `.meta`); `dotnet` is happy to compile files from
  anywhere. Adding a `.cs` file needs no project edit in either host.
- **EditMode tests are split into `Pure/` and `Engine/`, and only `Pure/` is linked.** This is
  load-bearing, not tidiness. The EditMode leg's job includes "anything needing `UnityEngine` types"
  (see §What runs where), and the `dotnet` test project has no `UnityEngine` to reference at all — so
  a single unconditional `EditMode/**/*.cs` glob means **the first EditMode test that touches
  `UnityEngine` or `[UnityTest]` breaks the `dotnet` build, which breaks the credential-free required
  `ci` check** (`docs/adr/0009-ci-topology-and-secret-custody.md:33`). Rather than leave that as a
  tripwire for whoever writes that test:
  - `unity/Assets/Tests/EditMode/Pure/**` — **engine-free by contract.** Linked into
    `CatMetro.Tests.csproj`, runs in both hosts, and is covered by the same banned-symbol check as
    the Domain (below): `UnityEngine` is a **banned symbol under `Pure/`**, so the violation fails
    `scripts/check.sh` with a clear message instead of failing a compile with a confusing one.
  - `unity/Assets/Tests/EditMode/Engine/**` — **never linked.** May freely use `UnityEngine`,
    `[UnityTest]`, `UnityEngine.TestTools` and coroutine-based tests. Runs only in the
    `unity-editmode` leg.
  Both folders stay inside the single `CatMetro.Tests.EditMode` asmdef, so this costs **zero extra
  asmdefs** and the assembly count in ADR-0003 is unchanged; the split is a directory convention that
  one glob and one static check enforce.
- **`netstandard2.1` for the four libraries** — the exact scripting profile the engine pin declares
  (`docs/plan/specs/architecture.md:12`, ADR-0004). A file that compiles here compiles under IL2CPP's
  BCL surface; a file that reaches for `net8.0`-only APIs fails at the earliest possible moment.
- **`net8.0` for the test project only.** Tests never ship.
- **NUnit, not xUnit.** The Unity Test Framework is NUnit-based, so `[Test]`, `[TestCase]` and
  `Assert.That` compile unchanged under both `dotnet test` and Unity EditMode. Choosing xUnit would
  force two copies of every test — the exact duplication this ADR exists to prevent.
- **The Unity project root is `unity/`, not the repo root**, so Unity's generated `.csproj`/`.sln`
  files cannot collide with the hand-authored ones in `dotnet/`.
- **`scripts/test.sh` is untouched.** `tests/domain/determinism.test.sh` is an ordinary wrapper that
  shells out to `dotnet test`; when the Unity leg lands, `tests/unity/editmode.test.sh` joins it as a
  second wrapper. The harness interface stays permanent, exactly as `scripts/test.sh:3` anticipates.
- **The goldens split across the immutability line.** Test *code* lives in `tests/domain/` and
  `unity/Assets/Tests/EditMode/` (agent-writable). The **golden replay hashes live in
  `tests/contract/`** (`tests/contract/replay-hash-golden.json`), which is human-authored-only and
  pre-commit-hook enforced (AGENTS.md hard rule 1). An agent can therefore never make a failing
  determinism test pass by regenerating the expected value — which is the whole point of CM-R01.1
  ("byte-for-byte equal to the checked-in golden", `docs/prd/PRD.md:98`) and of
  `docs/constitution.md:7` ("the definition of passing is never owned by the party that must pass it").
- **`scripts/check.sh` gets the banned-symbol static analysis** (CM-R01.2, `docs/prd/PRD.md:99`):
  `UnityEngine`, `DateTime`, `Stopwatch`, `Environment.TickCount`, `System.Random`, `float`, `double`
  in `unity/Assets/Scripts/Domain/**`; plus **`UnityEngine` and `UnityEngine.TestTools` under
  `unity/Assets/Tests/EditMode/Pure/**`** (the linked test folder). This is a *belt* — the braces are
  that the `dotnet` build has no `UnityEngine` to reference at all.

### What runs where

| Leg | Runs | Needs Unity | Covers |
|---|---|---|---|
| `dotnet` (this ADR) | every PR, ~seconds | **no** | determinism/replay hash, tick-order suite, PCG32, solver, level schema + bounds validation, fuzz corpus, save round-trip/migration, ledger dedupe, analytics queue bounds |
| Unity EditMode | PRs touching `unity/**`, nightly | yes | the same `EditMode/Pure/**` tests recompiled through asmdefs, **plus `EditMode/Engine/**` — anything needing `UnityEngine` types, which by construction never enters the `dotnet` leg** |
| Unity PlayMode / device | nightly + gates | yes | purchase mock flows, deep-link routing, tutorial journey, perf, process-death |

The Unity EditMode leg is **not redundant**: it is the assertion that the shared sources really do
compile and pass under IL2CPP's BCL and Unity's asmdef graph. It is a slower, rarer confirmation of a
fast, frequent gate.

## Alternatives seriously considered

- **Unity EditMode only, via `Unity -batchmode -runTests` (the plan's implied default —
  `docs/plan/EXECUTION_PLAN.md:526`).** Real advantages: one toolchain, zero duplication risk, tests
  run in exactly the shipping environment, and it is what every Unity team does. **Lost on
  sequencing and on secrets:** Contract 1 (the failing replay-hash test) would be blocked behind a
  human Unity install and licence activation on Day 1 — the one day that is already fully spent on
  Play Console work — and every CI test run would need Unity credentials in a runner under the
  solo-posture residual (ADR-0001). It also makes the level-validation job (CM-R12.1, 11 stages on
  every content PR) a licensed Unity job, which is the slowest possible way to run a pure-C# solver.
  This alternative is genuinely strong; it loses to calendar and to blast radius, not to elegance.
- **Two separate test suites (fast dotnet smoke + authoritative Unity suite).** Real advantage: no
  linked-file gymnastics; each suite idiomatic to its host. Lost because two suites drift, and the
  drift is silent until the day they disagree — at which point you have two definitions of "the sim
  is correct", which is precisely the failure ADR-0002 exists to prevent.
- **Extract the Domain into a NuGet package / git submodule consumed by Unity.** Real advantage: the
  cleanest possible separation and a versioned artifact. Lost on cost/benefit for a solo hackathon
  build: publishing and version-bumping a package on every sim change adds a release step to the
  tightest inner loop in the project. Revisit post-window if the Domain is ever reused.
- **Sources under `dotnet/` (or `src/`) with Unity consuming them as a local package under
  `unity/Packages/com.catmetro.domain/`.** Real advantage: keeps game-logic sources out of `Assets/`
  and is a legitimate Unity pattern. Lost because it costs a `package.json` and an import path for no
  gain — and because Unity tooling, agents and every tutorial expect scripts under `Assets/`. Chosen
  direction is the boring one.
- **Mono/`msbuild` against Unity's own DLLs to compile the Domain outside the editor.** Real
  advantage: byte-identical compiler to the editor's. Lost because it requires a Unity installation
  anyway — it solves the licence-in-CI problem not at all.
- **Skip the fast leg; rely on the device smoke job.** Lost immediately: `docs/prd/PRD.md:98` makes a
  replay-hash mismatch a merge blocker, and a merge blocker that takes an AVD boot to evaluate will
  be routed around under deadline pressure.

## Consequences

**Easier.** Contract 1 can start today, with no Unity, no licence, no human in the loop. The `ci`
required check (ADR-0001, ADR-0009) is fast, free, credential-free and green from day one. The
solver, validator and daily-seed pre-validation jobs — all pure C# by ADR-0002 — become ordinary
`dotnet` jobs. Local-model execution of Domain contracts (`docs/plan/EXECUTION_PLAN.md:526-528`)
becomes *cheaper*, since `dotnet test` is a far friendlier check command than a batchmode editor run.
The no-`UnityEngine` rule stops being a review convention and becomes a compile error.

**Harder.** Two project graphs must agree: the asmdef references (ADR-0003) and the `.csproj`
references. A CI check asserts they match; without it, a reference added in one host and not the
other is a nightly-only failure. **That parity check has three assertions, not one** (it runs in
`ci`, `docs/adr/0009-ci-topology-and-secret-custody.md:33`):

1. **Reference parity** — every asmdef reference among the four engine-free assemblies has a matching
   `ProjectReference`, and vice versa.
2. **Link-glob parity** — each `.csproj`'s `<Compile Include>` glob resolves to exactly the source
   set its asmdef claims, so a new folder cannot be picked up by one host and missed by the other.
3. **Test-split parity** — `CatMetro.Tests.csproj` links `Assets/Tests/EditMode/Pure/**` and
   **only** that; an `Include` that reaches `EditMode/Engine/**` (or drops the `Pure/` segment and
   globs all of `EditMode/**`) fails the check. This is the assertion that keeps the credential-free
   required check from being broken by an ordinary, correct EditMode test.

`netstandard2.1` occasionally bites, but **not** in the way a reader might assume: it **does**
include `Span<T>`, `ReadOnlySpan<T>`, `Memory<T>` and `ReadOnlyMemory<T>`, which are exactly the
vocabulary this architecture mandates in public signatures —
`Step(ref SimulationState, ReadOnlySpan<Command>)` and `WriteDigest(Span<byte>)`
(`docs/adr/0002-deterministic-fixed-tick-domain.md:33,52-53`) and the `ReadOnlyMemory<T>`/
`ReadOnlySpan<T>` views over level DTO arrays
(`docs/adr/0008-content-pipeline-and-level-schema.md:53`). **Use them; they are intended.** The real
gap is the .NET Core 3.0+ *additions* that never landed in the standard: `System.Text.Json` (which is
why ADR-0008 takes Newtonsoft anyway), and some of the newer `MemoryMarshal`/`BitOperations` surface.
A file reaching for one of those fails at the earliest possible moment, which is the constraint doing
its job, not a defect. Contributors need a .NET SDK installed as well as Unity.

**Locked in:** the **test harness interface** (`tests/**/*.test.sh`, exit 0 = pass) is permanent and
already immutable-adjacent; the **`tests/contract/` golden location** is where the human gate lives.
Neither is expensive to live with. The source-layout choice (`unity/` as project root) is cheap to
change before the Unity project exists and expensive after — decide it at scaffold.

**Explicit assumption, with its falsifier:** this ADR assumes a **.NET SDK 8** is installed on the
dev machine and available to CI runners (`actions/setup-dotnet` covers CI). Falsifier:
`dotnet --list-sdks` shows no 8.x. This document does **not** claim to have verified the local
install; the scaffold contract's first step is that check, and if 8.x is absent the fix is one
setup step, not a redesign (any SDK that can target `net8.0` and reference `netstandard2.1` works).

**Not covered by this leg, and no one should think otherwise:** anything touching `UnityEngine`,
rendering, input, prefabs, scenes, SDK adapters, IL2CPP-specific behaviour (stripping, AOT generics),
Android lifecycle, or performance. Those are Unity and device legs. A green `dotnet` run says the
*rules* are right; it says nothing about the *game*.

## Security notes

- **This is a secret-surface reduction.** The required-on-every-PR check runs with zero credentials:
  no Unity licence, no keystore, no Play service account, no SDK keys. Under ADR-0001's solo posture,
  where an agent-authored PR can be self-merged, the check that runs on every PR is the one that must
  hold no secrets — RK-33's "agent-reachable contexts never hold the credential"
  (`docs/prd/risks.md:124`) becomes structurally true for the common path.
- **It makes the RK-34 parser fuzz corpus affordable** (`docs/prd/risks.md:125`): malformed and
  adversarial level JSON is exercised by a fast, licence-free job that can run thousands of cases per
  PR, rather than by a device job that would never be built.
- **New supply-chain surface:** the `dotnet` leg introduces NuGet restore (NUnit, NUnit3TestAdapter,
  Newtonsoft.Json). Package sources must be pinned to nuget.org, versions pinned exactly (no floating
  ranges), and the lock file committed; the RK-36 SCA pass (ADR-0004) covers this tree too.
- **Immutable-path dependency:** the integrity of this whole scheme rests on `tests/contract/` and
  `.claude/hooks/` staying human-authored. ADR-0001 records the residual honestly — under solo
  posture a write-capable principal can merge changes to hook-protected paths. That residual, not the
  harness, is the weak link; do not let the harness's tidiness read as a stronger guarantee than the
  merge floor provides.
