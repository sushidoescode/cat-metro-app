# Unity Technical Architecture — Cat Metro (working title)

Status: draft v1 (31 Jul 2026). Version pins marked `PIN:` are finalized in the
compatibility matrix after SDK verification.

## Baseline decisions

| Area | Decision | Why |
|---|---|---|
| Engine | Unity 6 LTS (PIN: exact patch from verification) | Current LTS; Android 16/API 36 + 16 KB support; support window covers post-launch |
| Rendering | URP, linear color, Vulkan first + GLES3 fallback | Mid-tier Android perf; toon/flat look needs no HDRP features |
| Scripting | IL2CPP, ARM64 only, .NET Standard 2.1 | Play requirement + performance |
| Input | Unity Input System (tap/pointer only) | Single tap interaction; new-project default; EnhancedTouch for multi-safe hit tests |
| UI | UGUI + TextMeshPro (not UI Toolkit) | Mature world-space + screen-space mixing, mature localization/accessibility patterns; UI Toolkit runtime still weaker for game HUD juice |
| Android | minSdk 25, targetSdk 36, 16 KB page-size audit of all native libs | Play policy (verify exact deadline); min API 25 (Android 7.1+) is the Unity 6.3 documented Android minimum |
| Determinism | Pure C# fixed-tick domain (8 ticks/s), integer/fixed-point state, seeded PCG RNG, command log | Replay, solver, level validation, capture system, and bug repro all depend on it |
| Save | Versioned JSON + binary header, atomic write (temp+rename), migration table, durable purchase ledger | Process-death safety; consumable double-grant protection |
| Content | Levels as validated JSON in StreamingAssets, imported to immutable DTOs at load; content hash in save | No remote-content failure path during the event |
| Addressables | NOT used at launch | One content pack, local; Addressables adds build complexity with zero benefit at this size |
| Remote config | NOT used at launch except RevenueCat Offerings (which is remote by design) | Offerings give paywall/product remote control without a config service |
| Audio | Unity AudioSource pools + central mixer; layered combo stems | No FMOD/Wwise — licensing + integration cost unjustified |
| Haptics | Android VibrationEffect via thin JNI helper; 3 tiers; master toggle | Unity built-in Handheld.Vibrate is single-amplitude only |
| Localization | String table (custom, CSV-driven) — EN only at launch, structure ready | Unity Localization package is heavy for one language; CSV keeps agents safe |
| Crash reporting | Unity Cloud Diagnostics or Firebase Crashlytics (PIN after conflict check) | Crash-free-rate evidence for submission |
| Analytics | Thin typed wrapper → one product analytics backend + OneSignal tags + RC events | Single choke point; unknown event names fail in dev builds |
| DI | Composition root + constructor injection into plain C# services; no framework | Zenject/VContainer unnecessary at this scale; keeps agent-generated code simple |
| CI | GitHub Actions: compile+EditMode per PR; level batch validation per content PR; nightly Android dev build; RC builds 2×/week from Week 3 | Deterministic merge gates for the agent fleet |

## Assembly layout (asmdefs)

```
CatMetro.Domain          — graph, sim, rules, score. NO UnityEngine reference.
CatMetro.Application     — level lifecycle, commands, replay, save orchestration, reward decisions.
CatMetro.Content         — DTOs, JSON import, schema validation, catalog.
CatMetro.Presentation    — input, camera, views, VFX, audio, haptics, UI screens.
CatMetro.Services        — interfaces only: IPurchases, IMessaging, IAds, IAnalytics, ISave, IClock.
CatMetro.Integrations.*  — RevenueCat / OneSignal / Ads / Analytics adapters (one asmdef each; only these reference SDK code).
CatMetro.Editor          — importers, solver runner, batch validator, capture tooling.
CatMetro.Tests.EditMode  — domain determinism, solver, economy, save migration.
CatMetro.Tests.PlayMode  — purchase mock flows, deep-link routing, tutorial journey.
```

Dependency rule: arrows point inward only (Presentation→Application→Domain;
Integrations→Services; nothing references Integrations except the composition root).

## Simulation core

- `SimTick = 125 ms` fixed; presentation interpolates between snapshots at render rate.
- State: `SimulationState` (immutable snapshot exposed; internal mutable buffer double-buffered).
- Input: `ToggleSwitchCommand(switchId, tick)` appended to command log; applied at next tick boundary.
- RNG: PCG32 seeded from level seed; the ONLY RNG in Domain. Wall clock (`IClock`) never read inside the tick.
- Replay: (levelId, seed, commandLog) → identical outcome; CI asserts replay hash stability across platforms (EditMode + device smoke).
- Solver: BFS for ≤2-switch boards, beam search (widths 1k/2.5k/5k) beyond; shares the exact Domain step function — no parallel implementation.

## Scene map

```
Boot        — composition root, save load, SDK init behind feature flags, then →
Home        — district map, daily entry, shop, settings (single scene, screen stack)
Game        — board view + HUD; loads level DTO additively, owns retry loop
Capture     — editor/dev-only portrait capture rig (1080×1920), replay-driven
```

Screen navigation is a stack of UI screens within Home/Game, not scene loads —
scene loads on mobile cost more than they organize.

## Game-state machine

```
Boot → Home ⇄ LevelIntro → Playing ⇄ Paused
                Playing → Won  → Results → (Next | Home | Share)
                Playing → Failed → FailureReview → (Retry | RewindOffer | Home)
Deep link (daily/challenge) → Home(resolve) → LevelIntro
Purchase/restore/ad flows are modal overlays over any state; sim pauses; state
survives process death via persisted screen-stack breadcrumb.
```

## Android lifecycle rules

- `OnApplicationPause(true)`: flush save + analytics queue synchronously (bounded 50 ms budget), pause sim, timestamp for session gap.
- Purchase or ad in flight during pause: never grant from memory on resume — reconcile from RC CustomerInfo / ad callback + ledger.
- Back gesture: pause menu in Game, screen-stack pop in Home, never exits mid-purchase.
- Process-death test is part of the smoke suite (adb kill during save, purchase, ad).

## Performance budgets (mid-tier device, e.g. 2022 $200-class Android)

| Budget | Target |
|---|---|
| Frame time | ≤16.6 ms p50, ≤22 ms p95 during max wave |
| GC | 0 allocations/frame in Playing after warm-up (pooled commuters/VFX/text) |
| Draw calls | ≤120 (atlased sprites/meshes, static+SRP batching) |
| App size | ≤60 MB AAB download |
| Memory | ≤350 MB PSS on 3 GB device |
| Cold start | ≤3.5 s to Home on mid-tier |
| Battery | no sustained >40% single-core in Playing; thermal test: 20-min session no downclock jank |

## Feature flags

Compile-time + save-stored runtime flags: `ads_enabled`, `paywall_placements`,
`daily_enabled`, `weekly_event`, `share_card`, `leaderboard(OFF at launch)`.
Flags let the commercial beta ship with systems dark, and give instant kill-switches
without a remote-config service (next update flips them).

## Known integration risk zones (verify in Week-1 spike)

1. EDM4U (External Dependency Manager) version conflicts between Google Mobile Ads
   Unity plugin, RevenueCat, OneSignal — single EDM4U instance, pinned; run
   `Force Resolve` in CI and diff the resolved dependencies file.
2. Gradle/AGP template drift: keep custom `mainTemplate.gradle` + `gradleTemplate.properties`
   under version control; any SDK update re-runs the Android smoke build before merge.
3. 16 KB page-size: audit every `.so` (RevenueCat/Billing is Java-only — fine;
   ad SDK + Unity engine libs are the risk); test on 16 KB emulator image.
4. ProGuard/R8: keep rules for Billing, ad SDK, OneSignal push receivers; test
   release-minified build early, not at launch week.

## Device test matrix (minimum)

| Tier | Device class | API | Screen | Purpose |
|---|---|---|---|---|
| Low | 2-3 GB RAM, Mali GPU (e.g. Galaxy A1x/Redmi 9) | 24-29 | 720p | perf floor, GLES3 path |
| Mid | 4-6 GB (e.g. Pixel 6a / Galaxy A5x) | 33-36 | 1080p | primary target, Vulkan |
| High | flagship (Pixel 9 / S24) | 36 | 1440p+120 Hz | polish/refresh handling |
| Special | 16 KB-page emulator image; tablet/foldable aspect | 36 | var | policy + layout |
