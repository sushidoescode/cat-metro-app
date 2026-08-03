# ADR-0003: Assembly isolation — the 9-row asmdef layout, inward-only dependencies, one composition root

- **Status:** Proposed (ratifies `docs/plan/specs/architecture.md:29-44`; assigns the composition root, which that document left unassigned)
- **Date:** 2026-08-02
- **Relates:** ADR-0002 (Domain purity is enforced *here*), ADR-0005 (the dotnet leg is the second belt), ADR-0007 (Presentation), ADR-0008 (Content).

## Context

Three PRD gates are assembly-shaped, not review-shaped:

- CM-R01.2 `[CI]`: "the Domain assembly contains zero references to wall-clock/`DateTime`/`Time.*`"
  (`docs/prd/PRD.md:99`).
- CM-R02.1 `[CI]`: "a build-time test fails if the solver project references any duplicate step
  implementation" (`docs/prd/PRD.md:111`).
- CM-R25.1 `[CI]`: "zero occurrences of any `ofr_*` identifier outside `CatMetro.Integrations.RevenueCat`"
  (`docs/prd/PRD.md:430`), and CM-R43.2 `[CI]`: "a static check fails on any direct SDK call outside
  the wrapper" (`docs/prd/PRD.md:685`).

An agent fleet is writing this code. A dependency rule that lives only in a review comment will be
violated within a week; a dependency rule that lives in an asmdef fails the compile. The layout is
already settled at `docs/plan/specs/architecture.md:29-44`. One thing it does not say is **where the
composition root lives** — and since the rule is "nothing references Integrations except the
composition root", that omission is the difference between a compiler-enforced wall and a convention.

## Decision

We will ship the 9 rows of `docs/plan/specs/architecture.md:31-41` verbatim, expanding
`CatMetro.Integrations.*` into one asmdef per SDK, and we will **add one thin 10th row,
`CatMetro.Bootstrap`, to hold the composition root** so the Integrations rule is enforced by asmdef
references instead of by discipline.

| Assembly | Owns | May reference | Engine? |
|---|---|---|---|
| `CatMetro.Domain` | graph, tick, rules, score, PCG32, command log, solver step | *(nothing)* | **`noEngineReferences: true`** |
| `CatMetro.Content` | level DTOs, JSON parse, schema+bounds validation, catalog, content hash | Domain | **`noEngineReferences: true`** |
| `CatMetro.Services` | interfaces + POCO DTOs only. No behaviour. | Domain, Content | **`noEngineReferences: true`** |
| `CatMetro.Application` | level lifecycle, commands, replay, **the `ISave` implementation (`SaveStore`) and the `IContentSource`-fed content loader**, ledger, reward decisions, deep-link router | Domain, Content, Services | **`noEngineReferences: true`** |
| `CatMetro.Presentation` | input, camera, views, VFX, audio, haptics, UI screens, interpolation | Application, Services, Content, Domain (read-only types) | yes |
| `CatMetro.Integrations.RevenueCat` | purchases-unity adapter → `IPurchases` | Services | yes |
| `CatMetro.Integrations.OneSignal` | OneSignal adapter → `IMessaging` | Services | yes |
| `CatMetro.Integrations.Ads` | GMA (AppLovin fallback) adapter → `IAds` | Services | yes |
| `CatMetro.Integrations.Analytics` | analytics + Crashlytics adapters → `IAnalytics`, `IDiagnostics` | Services | yes |
| **`CatMetro.Bootstrap`** *(added)* | the composition root: SDK init behind flags, wiring, Boot scene entry, **the `IStorageRoot` implementation (the only place `UnityEngine.Application.persistentDataPath` is read)** | **everything** | yes |
| `CatMetro.Editor` | importers, solver runner, batch validator, capture tooling | all non-test | editor |
| `CatMetro.Tests.EditMode` | determinism, solver, economy, save migration, ledger | all non-test | yes |
| `CatMetro.Tests.PlayMode` | purchase mock flows, deep-link routing, tutorial journey | all non-test | yes |

**Rules that are mechanically enforced, not advisory:**

1. **Inward only.** `Presentation → Application → Content → Domain`; `Integrations → Services`;
   **only `CatMetro.Bootstrap` may reference an `Integrations.*` assembly.** Any other reference is a
   compile error inside Unity and a `dotnet` restore error outside it.
2. **Four assemblies are engine-free** (Domain, Content, Services, Application). In Unity this is the
   asmdef `noEngineReferences: true` flag (verify the exact field name at scaffold — it is not
   invented here, but it is not verified by this document either); outside Unity it is enforced far
   more strongly by ADR-0005, which compiles those four with the plain .NET SDK where `UnityEngine`
   does not exist at all.
3. **`CatMetro.Services` contains interfaces and DTOs only** — zero SDK types in any signature. An
   `IPurchases` method returning a RevenueCat type would defeat the entire boundary, so the CI check
   is "no `Integrations` assembly's types appear in a `Services` signature".
4. **Cross-cutting `[CI]` greps get a home:** `ofr_*` ids may appear only under
   `CatMetro.Integrations.RevenueCat` (CM-R25.1); raw analytics event strings only under
   `CatMetro.Integrations.Analytics` (CM-R43.2); SDK namespaces (`RevenueCat`, `OneSignalSDK`,
   `GoogleMobileAds`, `Firebase`) only under `CatMetro.Integrations.*` and `CatMetro.Bootstrap`.

**The `CatMetro.Services` interface set is 6 ratified + 3 architect additions = 9.** The ratified six
are `IPurchases, IMessaging, IAds, IAnalytics, ISave, IClock` (`docs/plan/specs/architecture.md:36`).
Added, each with a named requirement:

- **`IContentSource`** — because Android stores `StreamingAssets` inside the compressed APK, where
  `System.IO` cannot read it. Without this interface, `CatMetro.Content` would need `UnityWebRequest`
  and would stop being engine-free, which would break ADR-0005's licence-free level validation.
  (Implementer verifies the `UnityWebRequest`-for-StreamingAssets-on-Android behaviour at scaffold;
  the *shape* of the interface is correct either way.)
- **`IDiagnostics`** — because RK-31 (`docs/prd/risks.md:115`) requires "one logging wrapper with a
  scrubber applied to crash custom keys and breadcrumbs **as well as** events", and Crashlytics is an
  SDK, which by rule 4 above may not be called outside `Integrations`.
- **`IStorageRoot`** *(added — this is the write-side twin of `IContentSource`)* — because the
  Android save location comes from `UnityEngine.Application.persistentDataPath`, which is an **engine
  API**, while `ISave`'s implementation lives in `CatMetro.Application`, which is declared engine-free
  by rule 2. Reads were solved by `IContentSource` and writes were left unsolved; without this
  interface `ISave` has no legal home — either `CatMetro.Application` takes an engine reference
  (breaking rule 2, breaking ADR-0005's `dotnet` leg, and breaking ADR-0006's claim that the
  kill-during-write and migration tests run engine-free), or `ISave` migrates to an engine-coupled
  assembly and the save orchestration leaves the layer that owns it. `IStorageRoot` is two
  directory-path properties and nothing else:

  ```csharp
  public interface IStorageRoot {
      string SaveDirectory  { get; }   // Bootstrap supplies Application.persistentDataPath
      string CacheDirectory { get; }   // Bootstrap supplies Application.temporaryCachePath
  }
  ```

  **Named requirement:** CM-R05's kill-during-write, migration and low-storage-soak tests
  (`docs/prd/PRD.md:157-174`) are assigned to the fast `dotnet` leg by ADR-0006 §Consequences. A test
  process supplies a temp directory; the app supplies `persistentDataPath`; `SaveStore` cannot tell
  the difference and needs no `#if UNITY_ANDROID`. `CatMetro.Application` uses plain `System.IO`
  (available under `netstandard2.1` and functional on Android for `persistentDataPath` — unlike
  `StreamingAssets`, which is *inside* the APK and is why the read side needed a different answer).

**So the ownership is explicit, and no implementer has to ask:** `ISave` is **declared** in
`CatMetro.Services`, **implemented** in `CatMetro.Application`, and **rooted** by an `IStorageRoot`
implemented in `CatMetro.Bootstrap`. `CatMetro.Bootstrap` is the only assembly in the project that
names `Application.persistentDataPath`, and a `[CI]` grep asserts that (same mechanism as rule 4).

Full signatures are in `docs/architecture/overview.md` §Interface contracts.

## Alternatives seriously considered

- **Composition root inside `CatMetro.Presentation`** (the reading `architecture.md` most invites,
  since Boot is a scene). Real advantage: 9 rows instead of 10; the Boot MonoBehaviour sits next to
  the other MonoBehaviours; no extra asmdef to explain. **Lost because it makes the central rule
  unenforceable:** if Presentation references all four Integrations assemblies, then every screen,
  VFX script and audio pool in the project can `using RevenueCat;` and the compiler will not care —
  the rule degrades to a grep. A ~50-line assembly is a genuinely small price for turning the
  project's most important boundary into a compile error. This is the deviation from the literal
  9 rows and it is the only one.
- **One `CatMetro.Integrations` assembly for all four SDKs.** Real advantage: fewer asmdefs, one place
  for EDM4U-resolved dependencies, simpler `link.xml`. Lost because the four SDKs have independent
  kill switches and independent failure modes — `ads_enabled=false` (ADR-0007), the AppLovin fallback
  (ADR-0004), the "RC Ads beta not granted" contingency (`docs/plan/EXECUTION_PLAN.md:560`) — and one
  assembly means one blast radius: an OneSignal compile break blocks the purchase build.
- **No `CatMetro.Services` layer; adapters implement interfaces declared next to their consumers.**
  Real advantage: fewer files, no "interface-only assembly" ceremony. Lost because `Application` would
  then have to know which adapter it is talking to, and the `Integrations → Services` arrow — the
  thing that lets the Domain/Application half compile without a single SDK present — disappears.
  ADR-0005 depends on this arrow existing.
- **Folder conventions + a lint rule instead of asmdefs.** Real advantage: no assembly-boundary
  friction (`internal` stops working across the layers, which is a genuine cost). Lost because the
  enforcement is exactly as strong as the lint rule's coverage, and because asmdefs also buy
  incremental compile time, which matters at agent-fleet iteration rates.
- **Merging `Content` into `Domain`.** Real advantage: one fewer assembly; the solver already needs
  both. Lost because parsing is the project's likeliest boot-path crash source (RK-34,
  `docs/prd/risks.md:125`) and keeping the parser out of the assembly that must stay allocation-free
  and side-effect-free is worth one asmdef.

## Consequences

**Easier.** "Where does this go?" has one answer per concern. A whole class of PRD `[CI]` checks
becomes a one-line assembly-reference assertion rather than a source scanner. SDK swaps
(AppLovin fallback, analytics backend) touch exactly one assembly plus Bootstrap.

**Harder.** `internal` no longer spans layers, so a few types that would naturally be internal become
public with an `Api`/`Internal` namespace convention instead. Adding a service means touching
Services + an Integrations adapter + Bootstrap — three files for one capability; that friction is the
feature. Unity's asmdef graph must be kept in sync with the `dotnet` project references (ADR-0005);
a CI check asserts the two agree.

**Locked in (declare irreversible — human ADR gate):** **assembly names are effectively permanent.**
They appear in `link.xml`, ProGuard/R8 keep rules, IL2CPP stripping config, every `[CI]` grep in the
PRD, and CM-R25.1's published contract. Renaming one after the first signed build is a cross-cutting
change to gates the PRD names by string. Names are settled here and should not drift.

**Reversible:** `CatMetro.Bootstrap` can be folded into Presentation later at the cost of the
enforcement; the reverse (splitting it out) is what we are doing now, and it is cheap only while
Presentation is empty. Do it at scaffold or not at all.

## Security notes

- **This ADR *is* a security control.** It creates the boundary that guarantees SDK code — the only
  code in the project that talks to a network, a store, or a push service — is reachable from exactly
  one assembly. Every third-party trust boundary in the threat model (`docs/prd/risks.md` §E-G) sits
  on an `Integrations.*` edge.
- **Secret custody follows the boundary:** the OneSignal REST key must never exist in the client
  (RK-29, `docs/prd/risks.md:106`) — the client holds the App ID only, in `Bootstrap`. No key,
  id or endpoint literal may appear outside `Integrations.*`/`Bootstrap`, which makes the secret-scan
  surface a four-directory review rather than a whole-repo one.
- **`IDiagnostics` exists so the RK-31 scrubber has exactly one choke point.** If any assembly can
  call Crashlytics directly, the scrubber is decorative and transaction-id-shaped tokens reach crash
  reports as an undeclared data flow.
- **`IContentSource` keeps the parser engine-free**, which is what lets the RK-34 fuzz corpus
  (`docs/prd/risks.md:125`) run as an ordinary fast `dotnet` test instead of a device job — the
  difference between a fuzz gate that runs on every PR and one that never gets written.
