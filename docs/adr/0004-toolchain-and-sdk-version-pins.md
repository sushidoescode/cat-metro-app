# ADR-0004: Toolchain and SDK version pins, with named unpin triggers

- **Status:** Proposed (ratifies `docs/plan/EXECUTION_PLAN.md:18-22`; resolves the two `PIN:` holes left in `docs/plan/specs/architecture.md`)
- **Date:** 2026-08-02
- **Relates:** ADR-0003 (only `Integrations.*` sees these SDKs), ADR-0009 (CI enforces the pins).

## Context

`docs/plan/specs/architecture.md` shipped with two unresolved pins — the exact Unity patch
(`:10`) and crash reporting ("Unity Cloud Diagnostics or Firebase Crashlytics (PIN after conflict
check)", `:24`) — and one platform figure that the amendment pass has since overturned
(`minSdk 24`, `:15`). Meanwhile the toolchain is the plan's single largest landmine: AdMob is
confirmed broken on AGP 9, so the Unity patch version transitively pins the entire Android
toolchain (`docs/plan/EXECUTION_PLAN.md:18-20`). Four SDKs plus EDM4U must resolve to one
dependency set (`docs/plan/specs/architecture.md:106-108`), and `docs/prd/risks.md:136` records
that **no advisory, transitive-tree, licence or install-script check has been done on any of them**.

Nothing in this ADR is a fresh choice: the versions were verified by the 23-agent audit on
2026-07-31 and the perishable ones were re-checked on 2026-08-02 —
"the Unity pin (googleads-mobile-unity **#4212 still open** ⇒ pin holds); purchases-unity still at
9.7.0" (`docs/prd/risks.md:149`). This ADR's job is to make the pins *binding*, name what unpins
each one, and close the two holes.

## Decision

We will pin the following and treat every row as immutable until its named unpin trigger fires.

### Engine and build toolchain

| Pin | Value | Unpin trigger |
|---|---|---|
| Unity | **6000.3.16f1** — do **not** move to 6000.3.17f1+ | A googleads-mobile-unity release that closes **#4212** **AND** a green Android smoke build on the candidate (`docs/plan/EXECUTION_PLAN.md:345-347`) |
| Gradle / AGP | **8.13 / 8.10.0** (transitive from the Unity pin) | Same as Unity |
| Scripting backend | **IL2CPP**, **ARM64 only** | Play policy; no trigger in-window |
| Scripting API compatibility level | **`.NET Standard 2.1`** scripting profile | ADR-0005 depends on this exact profile |
| Android | **minSdk 25 / targetSdk 36** | Play policy deadline movement only |
| Render pipeline | **URP**, linear colour, **Vulkan first, GLES3 fallback** | A low-tier device failing the Vulkan path in the device matrix ⇒ GLES3-first for that tier, not a pipeline change |
| Native libs | every `.so` audited for **16 KB page size**, tested on the 16 KB emulator image | — |

**`minSdk 25` supersedes `docs/plan/specs/architecture.md:15` ("minSdk 24")**, per AMD-08
(`docs/plan/EXECUTION_PLAN.md:341-359`), which lists `specs/architecture.md:15` explicitly as a file
to correct and requires `grep -rin "api 24\|minsdk 24" → 0`. minSdk 25 = Android 7.1+.

### SDKs (exactly these versions; exactly one EDM4U)

| Package | Pin | Notes |
|---|---|---|
| purchases-unity (RevenueCat) + RevenueCatUI | **9.7.0** | Re-check at import; cadence is weekly (`docs/plan/EXECUTION_PLAN.md:20-21`). **No Unity IAP** — verify a single `BillingClient` in the merged manifest. |
| OneSignal Unity | **5.3.2** | Custom events require SDK ≥5.2.0 **and a paid plan** (A-09, `docs/prd/PRD.md:949`) |
| Google Mobile Ads Unity | **11.3.0** | The AGP9 constraint owner (#4212) |
| EDM4U | **1.2.188**, exactly one copy | `Force Resolve` in CI; diff the resolved-dependencies file (`docs/plan/specs/architecture.md:106-108`) |
| AppLovin MAX | **8.6.4** — *fallback only, not shipped by default* | `docs/plan/EXECUTION_PLAN.md:188` |
| Crash reporting | **Firebase Crashlytics** | **Resolves the `PIN` at `docs/plan/specs/architecture.md:24`** |
| JSON | **Newtonsoft.Json** via `com.unity.nuget.newtonsoft-json` (Unity) and the matching NuGet package (dotnet leg) | See ADR-0008 for the hardening rules; this row satisfies AGENTS.md hard rule 2 |

### The `dotnet` leg is part of the pinned set (ADR-0005)

An earlier draft of this ADR defined "the pinned set" as Unity-only while ADR-0005 independently
required "versions pinned exactly (no floating ranges), and the lock file committed"
(`docs/adr/0005-dotnet-first-dual-test-harness.md:172-174`). Two documents defining the same term
differently is how a pin silently stops being one. These rows close it — the `dotnet` leg compiles
the **shipping** Domain/Content/Services/Application sources, so its toolchain is not a test detail:

| Component | Pin | Notes |
|---|---|---|
| .NET SDK | **8.x** (any SDK that can target `net8.0` and reference `netstandard2.1`) | ADR-0005's explicit assumption + falsifier (`dotnet --list-sdks`); CI uses `actions/setup-dotnet` with the same major |
| Target frameworks | **`netstandard2.1`** for the four libraries, **`net8.0`** for `CatMetro.Tests` only | Mirrors the scripting profile row above; a library reaching for `net8.0`-only APIs must fail in the fast leg, not at IL2CPP |
| NUnit | exact version, no floating range | Must stay NUnit-major-compatible with the Unity Test Framework so `[Test]`/`[TestCase]`/`Assert.That` compile unchanged in both hosts (ADR-0005) |
| NUnit3TestAdapter | exact version, no floating range | `dotnet test` discovery only; never ships |
| Newtonsoft.Json (NuGet) | exact version, **matched to the version inside `com.unity.nuget.newtonsoft-json`** | The two hosts parsing the same level JSON with different Newtonsoft majors is a silent content-behaviour skew; this is the row most worth a mismatch check |

**Exact version strings are recorded at scaffold, in `config/pins.json` and `dotnet/packages.lock.json`
— not invented here.** No NuGet package version in this project has been verified by this document,
and RK-36 records that no advisory/licence/transitive pass has been run on anything
(`docs/prd/risks.md:136`); writing plausible-looking version numbers into an ADR is exactly the
failure mode A-07 forbids (`docs/plan/EXECUTION_PLAN.md:480-481`). The scaffold PR fills the cells
and the pin check enforces them from that commit on.

**Crash reporting = Firebase Crashlytics, not Unity Cloud Diagnostics.** `docs/plan/EXECUTION_PLAN.md:22`
already lists Crashlytics in the pinned stack, and a Firebase project is on the Day-1 human checklist
anyway for FCM v1 → OneSignal (`docs/plan/EXECUTION_PLAN.md:422-423`). Choosing Cloud Diagnostics would
mean standing up a second vendor for one signal while the Firebase project exists regardless.
Crashlytics also gives the custom keys and breadcrumbs that RK-31's scrubber needs a home in.

### Pin hygiene (how a pin stops being a sentence in a doc)

1. Versions live in exactly **three** machine-readable places:
   (a) `unity/Packages/manifest.json` (+ the EDM4U-resolved dependencies file);
   (b) `unity/ProjectSettings/ProjectVersion.txt`;
   (c) **`dotnet/**/*.csproj` (`TargetFramework` + every `PackageReference Version`) and the
   committed `dotnet/packages.lock.json`.**
   A `[CI]` check asserts all three match the table checked in at `config/pins.json`, and that table
   is the one this ADR describes — **including the `dotnet` rows above; `config/pins.json` carries the
   .NET SDK major, both target frameworks, NUnit, NUnit3TestAdapter and Newtonsoft.Json.** The check
   additionally asserts `packages.lock.json` is present, in sync (`dotnet restore --locked-mode`
   succeeds) and contains no floating range, and that the Newtonsoft version in (c) matches the one
   the Unity package in (a) carries. A pin change in any of the three is therefore a diff,
   reviewable, and blocked by the human merge gate.
2. **Any SDK or engine version change re-runs the Android smoke build before merge**
   (`docs/plan/specs/architecture.md:109-110`) and re-runs the audit's version checks
   (`docs/plan/EXECUTION_PLAN.md:480-481`).
3. `mainTemplate.gradle`, `launcherTemplate.gradle`, `gradleTemplate.properties` and the ProGuard/R8
   keep rules (Billing, ad SDK, OneSignal push receivers) are **committed and reviewed**
   (`docs/plan/specs/architecture.md:109-114`). Template drift is a code change, not an IDE side effect.
4. **SCA is not optional and it has not happened** (RK-36, `docs/prd/risks.md:136`): the first
   scaffold PR runs a dependency/advisory/licence pass and re-runs it on every version change.
5. The standing Monday verification duty already covers "pinned SDK release pages,
   googleads-mobile-unity #4212" (`docs/plan/EXECUTION_PLAN.md:482-485`). This ADR is the register it
   verifies against.

## Alternatives seriously considered

- **Take the newest Unity 6.3 patch and the newest SDKs (the reflex).** Real advantage: newest bug
  fixes, longest support window, and avoids an awkward "why are we behind?" conversation with judges.
  Lost on one verified fact: AdMob is confirmed broken on AGP 9 (googleads-mobile-unity #4212, still
  open as of 2026-08-02), and 6000.3.17f1+ carries AGP 9. Newer here means no ads, which means no
  Catvertising entry and no rewarded surfaces — a scope loss, not an inconvenience.
- **Drop AdMob and take the newest engine** (i.e. resolve the conflict in the engine's favour). Real
  advantage: removes the sharpest pin in the stack and simplifies EDM4U resolution to two SDKs. Lost
  because rewarded ads are five shipped surfaces and one of the P0 award targets
  (`docs/plan/EXECUTION_PLAN.md:12,188`), and because the contingency for "no ads" already exists as a
  *fallback* (Model A, `docs/plan/EXECUTION_PLAN.md:560`) — spending it voluntarily to gain patch
  versions is a bad trade.
- **AppLovin MAX as the primary ad SDK instead of GMA.** Real advantage: it genuinely might not have
  the AGP 9 constraint, which would unpin the engine. Lost because it is unverified for that claim,
  the AdMob account and test units are already on the Day-1 human checklist
  (`docs/plan/EXECUTION_PLAN.md:424`), and swapping the primary ad SDK mid-window trades a known,
  worked-around problem for an unknown one. It stays pinned as the **fallback** at 8.6.4 exactly so
  the swap is a decision we can make later with evidence.
- **Unity Cloud Diagnostics for crash reporting.** Real advantage: zero extra SDK, zero extra EDM4U
  dependency (the sharpest integration risk in the project), no second vendor in the Data Safety
  form. Genuinely close. Lost because the Firebase project must exist anyway for FCM v1 → OneSignal,
  because Crashlytics' custom keys/breadcrumbs are what RK-31's scrubber attaches to, and because
  crash-free-rate evidence is a submission artifact where the mainstream tool has better-known
  behaviour under review scrutiny. **If the EDM4U resolution proves hostile at the Day-1/D3 spike,
  this is the first row to revisit** — it is the most reversible pin in the table.
- **Vendoring / committing resolved AARs instead of EDM4U resolution.** Real advantage: reproducible
  builds with no resolver in the loop. Lost: it fights every SDK's supported install path and turns
  each version bump into manual dependency archaeology, for a solo maintainer, in an 8-week window.
- **`minSdk 24` (the architecture.md figure).** Lost to AMD-08's verified correction; API 25 is the
  documented floor for the Unity 6.3 line and the amendment pass names `architecture.md:15` as a fix
  target (`docs/plan/EXECUTION_PLAN.md:352`). The device-coverage delta between API 24 and 25 is not
  worth contradicting a verified platform requirement.

## Consequences

**Easier.** "Can I upgrade X?" has a written answer with a named trigger. EDM4U conflicts —
the top integration risk (`docs/plan/specs/architecture.md:106-108`) — are diffable because exactly
one resolver runs and its output is committed. The dependency set is small enough to actually audit.

**Harder.** We ship on an engine patch that will be visibly behind by September, and any security
advisory against a pinned SDK forces an explicit exception decision rather than a routine bump. An
accepted CVE trade-off is already on the record for libcurl CVE-2026-27135
(`docs/plan/EXECUTION_PLAN.md:346-347`) — that is the shape of the cost.

**Locked in — flag for the human gate:** the **vendor set** (RevenueCat, OneSignal, Google/AdMob,
Firebase) is effectively frozen for the window; each is a declared third-party data flow in the Play
Data Safety form (RK-30, `docs/prd/risks.md:107`), so adding or removing one is a store-listing change
and a policy re-declaration, not a code change.

**Ratification has a repo-instruction consequence — name it here so it is not forgotten.** `AGENTS.md`
currently records *"Stack: TBD — deferred until the product specs land"* with a stack-agnostic gate
harness. That line was true before this ADR and is false after it; leaving it would make the repo's
own instruction file — the one every agent reads first — contradict the ratified ADR set, which is a
defect of exactly the kind AGENTS.md hard rule 3 exists to prevent. **On ratification of ADR-0004 and
ADR-0005, the same change-set updates `AGENTS.md`:**

1. **Stack line** → Unity **6000.3.16f1** / C# / **IL2CPP**, ARM64 only, **.NET Standard 2.1**
   scripting profile, URP; Android **minSdk 25 / targetSdk 36**. Track (mobile game / Android /
   Google Play) is unchanged.
2. **Commands** → record that `scripts/check.sh` and `scripts/test.sh` now route to the `dotnet` leg
   (ADR-0005): `check.sh` gains the banned-symbol and pin/parity static analysis, `test.sh` still
   discovers `tests/**/*.test.sh` — **its interface is deliberately unchanged** (ADR-0005) — but the
   wrappers underneath it now shell out to `dotnet test`, and `tests/unity/editmode.test.sh` joins
   them when the Unity project is scaffolded.
3. **Layout** → `unity/`, `dotnet/`, `content/` and `config/` are named (see
   `docs/architecture/overview.md` §7), replacing "app/game code: not yet scaffolded".

`AGENTS.md` is not an architect-writable file under this role's boundaries (docs-only), so this is
recorded as a **ratification action for the human**, not as work already done. The ADR gate is not
complete until it is applied.

**Spend:** OneSignal Growth is $19/mo, free for 3 months via the Ship Kit perk — claim before
subscribing (`docs/plan/EXECUTION_PLAN.md:39-40`); a silent downgrade breaks J1/J3 custom events
(A-09). AdMob/Firebase/RevenueCat at our scale are free tiers. No licence cost is introduced by this
ADR.

**Verification obligations the implementer inherits (do not treat as done):**
- RK-36 SCA pass at first scaffold (`docs/prd/risks.md:136`).
- RK-39 — read the purchases-unity 9.7.0 package source **before** any ledger or entitlement code is
  written, and report back against RK-16/RK-19/RK-20/RK-22 (`docs/prd/risks.md:139`). A-07/A-08
  (`docs/prd/PRD.md:947-948`) mean no RC API signature in the corpus is verified. **Never invent an
  SDK API; verify against the pinned source** (`docs/plan/EXECUTION_PLAN.md:480-481`).
- 16 KB page-size audit of every `.so`, on the 16 KB emulator image
  (`docs/plan/specs/architecture.md:111-112`).
- R8/ProGuard release-minified build tested early, not in launch week
  (`docs/plan/specs/architecture.md:113-114`).

## Security notes

- **Unpatched-by-choice is a security posture, not an oversight.** The engine pin means we knowingly
  forgo upstream fixes for the window. The mitigation is scope: one process, no server, no user
  accounts, no user-supplied URL fetching (`docs/prd/risks.md:145`). Any advisory affecting a pinned
  SDK must be triaged explicitly against that scope, and the disposition written down — severity
  acceptance is never delegated (`docs/constitution.md:26`).
- **EDM4U resolves and downloads dependencies at build time.** That is a supply-chain edge: the
  resolved-dependencies file is committed and diffed on every change precisely so a silent
  transitive shift is visible in review.
- **Each pinned SDK is a trust boundary and a declared data flow.** OneSignal receives tags
  including `payer_status` (RK-30); AdMob receives ad-request context on a 13+ title carrying
  `AD_ID`; Crashlytics receives whatever the RK-31 scrubber lets through. The Data Safety form must
  match this exact vendor list.
- **The AppLovin fallback is dormant code with a live trust boundary** if it is ever imported. It
  stays out of the build until the fallback is actually taken, and taking it re-opens the Data Safety
  declaration.
