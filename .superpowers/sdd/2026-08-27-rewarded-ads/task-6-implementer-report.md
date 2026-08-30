# Task 6 implementer report — guarded LevelPlay rewarded video

## Outcome

Implemented an optional, package-guarded LevelPlay 9.5.1 rewarded-video provider in the
`feat/rewarded-ads` linked worktree, starting from
`76ee9963c856d54ccf2b15dda9a72de86c673869`. The integration constructs only the single
configured rewarded ad unit, translates package callbacks into neutral immutable events, and is
owned and disposed through the existing rewarded composition/coordinator. It does not add a new
runtime host, ad format, privacy UI, entitlement grant path, or provider-owned post-terminal
reload.

The implementation commit uses:

- subject: `feat(ads): integrate guarded LevelPlay rewarded video`
- trailer: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`
- SHA: the report is part of that commit, so its resulting SHA is recorded in the final handoff.

Independent review follows this implementation and is not replaced by the self-review below.

## Implementation

### Neutral package-free layer

- Added `LevelPlayPayloadMapper` in the always-compiled Integrations assembly.
  - `BID`, `RATE`, and `CPM` map to Exact, PublisherDefined, and Estimated after trim/case
    normalization.
  - USD doubles convert to checked micros with AwayFromZero midpoint rounding.
  - invalid, negative, non-finite, and overflowing values fail without throwing.
  - `AdId`, `AuctionId`, and `AdUnitId` remain distinct; revenue requires a nonblank AuctionId.
- Added `MainThreadAdEventQueue`, which snapshots under its lock and invokes after releasing the
  lock. Reentrant work remains for the next drain, consumer exceptions are isolated, and dispose
  invalidates pending work.

### Optional LevelPlay assembly

- Added `CatMetro.Integrations.LevelPlay.asmdef` with references to `CatMetro.Services`,
  `CatMetro.Integrations`, and the package assembly `Unity.LevelPlay`.
- Added the exact `com.unity.services.levelplay` 9.5.1 version define and matching
  `CATMETRO_LEVELPLAY` define constraint; the provider file also has an outer
  `#if CATMETRO_LEVELPLAY` guard.
- Pinned exactly `"com.unity.services.levelplay": "9.5.1"` in the manifest and accepted Unity's
  generated lock resolution from `https://packages.unity.com`. The lock records LevelPlay 9.5.1
  at depth 0 and `com.unity.services.core` 1.16.0 as the resolved transitive package.
- The resolved cache directory is
  `com.unity.services.levelplay@16215dfb563e`; the registry artifact fingerprint established in
  the source-review ledger is `16215dfb563ea8dbba2e9607e11cdadfd28f9510`.

### Exact 9.5.1 API implementation

Resolved source inspection confirmed and the Editor compiler binds:

- runtime assembly `Unity.LevelPlay`, namespace `Unity.Services.LevelPlay`;
- `LevelPlay.Init(string, string = null)` and static success/failure initialization events;
- `LevelPlayRewardedAd(string, Config = null)`;
- `LoadAd`, `ShowAd(string = null)`, `IsAdReady`, static `IsPlacementCapped`, `Dispose`,
  `DestroyAd`, `GetAdId`, and `AdUnitId`;
- all nine per-rewarded-instance events: loaded, load failed, displayed, display failed, rewarded,
  clicked, closed, info changed, and impression data ready;
- distinct `LevelPlayAdInfo` AdId/AuctionId/AdUnitId/PlacementName/AdNetwork fields;
- ILR AuctionId/MediationAdUnitId/Placement/AdNetwork/Revenue/Precision fields. CreativeId is not
  substituted for AdId.

The provider subscribes before one initialization call, coalesces pre-init Load demand, creates
one rewarded instance only after successful initialization, subscribes all nine ad events before
the first LoadAd, repeats readiness and placement-cap checks in TryShow, and fails closed at every
fakeable vendor boundary. Actual initialization, construction, load, show, readiness, placement
cap, and vendor disposal calls are independently guarded by
`#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)`.

`AfterAssembliesLoaded` registers only the provider factory on supported players. Operational SDK
initialization remains in the normal `BeforeSceneLoad` monetization composition lifecycle, after
the package's synchronization-context setup. No `Resources/LevelPlayMediationSettings` asset was
added or enabled.

### Correlation, ownership, and thread handoff

- TryShow establishes the attempt context before callback-capable ShowAd.
- Contexts are indexed only by nonblank package AdId/AuctionId values. There is no ad-unit,
  placement, network, clock, or latest-attempt fallback.
- Only one unbound context is allowed. Closed-but-reward-eligible contexts are retained, bounded
  to 16, and expired with an injected monotonic clock. Expiry restores availability but never
  grants or reattributes.
- Rewards cannot establish a previously unknown binding. Unknown, malformed, ambiguous, stale,
  duplicate, and expired rewards are dropped. Close-before-reward and distinct old/new stable
  auctions are supported.
- A synchronous display-failure callback makes TryShow reject after the callback removes its exact
  context; the coordinator recognizes that the terminal callback already owns the one reload.
- Provider callbacks never call LoadAd after close or display failure. `RewardedAdCoordinator` is
  the only post-terminal reload owner.
- The ILR callback copies package fields immediately into a readonly neutral snapshot and queues
  it. The existing `MonetizationPump.Update` drains through its currently owned composition before
  reporter delivery. Provider replacement and pump teardown sever the old drain owner.
- Provider and coordinator disposal are explicit and idempotent. Static initialization and all
  nine per-ad subscriptions are removed independently, queued ILR is invalidated, and the one ad
  bridge is disposed once even when removal/disposal accessors throw.

## Files

Created:

- `unity/Assets/Scripts/Integrations/LevelPlay.meta`
- `unity/Assets/Scripts/Integrations/LevelPlay/CatMetro.Integrations.LevelPlay.asmdef` and meta
- `unity/Assets/Scripts/Integrations/LevelPlay/LevelPlayRewardedAdProvider.cs` and meta
- `unity/Assets/Scripts/Integrations/LevelPlayPayloadMapper.cs` and meta
- `unity/Assets/Scripts/Integrations/MainThreadAdEventQueue.cs` and meta
- `unity/Assets/Tests/EditMode/Engine/LevelPlayPayloadMapperTests.cs` and meta
- `unity/Assets/Tests/EditMode/LevelPlay.meta`
- `unity/Assets/Tests/EditMode/LevelPlay/CatMetro.Tests.LevelPlay.EditMode.asmdef` and meta
- `unity/Assets/Tests/EditMode/LevelPlay/LevelPlayRewardedAdProviderTests.cs` and meta
- this report

Modified:

- `unity/Assets/Scripts/Services/Ads/RewardedAdEvents.cs`
- `unity/Assets/Scripts/Services/Ads/RewardedAdCoordinator.cs`
- `unity/Assets/Scripts/Integrations/RewardedAdsComposition.cs`
- `unity/Assets/Scripts/Integrations/MonetizationBootstrap.cs`
- `unity/Assets/Tests/EditMode/Pure/Ads/RewardedAdFixtures.cs`
- `unity/Assets/Tests/EditMode/Pure/Ads/RewardedAdCoordinatorTests.cs`
- `unity/Assets/Tests/EditMode/Engine/RewardedAdsBootstrapTests.cs`
- `unity/Packages/manifest.json`
- `unity/Packages/packages-lock.json`

Unity's package editor generated an untracked `unity/Assets/LevelPlay` dependency/version snapshot
during the cold import. It was outside the approved source list and was removed after the final
Unity run; the package can regenerate it. The pre-existing untracked
`unity/mono_crash.143e1228df.0.json` remains untouched as required. No raw `.log` artifact is
retained.

## TDD evidence

All accepted Unity commands used the absolute project path
`/Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/unity`, never `-quit`, and retained XML
only.

### Phase A — package absent

1. Tests were written first for the mapper/queue before their production types existed.
2. RED command shape:

   `unity test /Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/unity --mode EditMode --filter CatMetro.Tests.LevelPlayPayloadMapperTests --output .../artifacts/levelplay-absent-editmode.xml --format json --non-interactive`

   Expected compilation RED was observed: `CS0103`/`CS0246` for the absent
   `LevelPlayPayloadMapper` and `MainThreadAdEventQueue`. Compilation failure produced no valid XML.
3. After implementing the neutral layer and optional skipped assembly, with the manifest still
   lacking LevelPlay, `artifacts/levelplay-absent-editmode.xml` passed 25/25, failed 0, skipped 0.

### Phase B — package present

1. After pinning 9.5.1, Unity resolved the lock and
   `artifacts/levelplay-package-resolve.xml` passed the package-free suite 25/25.
2. Package-guarded provider tests were written against the thin seam before provider API/types
   existed. The expected compilation RED named the missing provider namespace, snapshots, bridge,
   and placement/drain capabilities; compilation failure produced no valid XML.
3. Initial provider GREEN: `artifacts/levelplay-provider-green-candidate.xml` passed 34/34.
4. Two mutation-sensitive correlation/reentrancy tests then produced
   `artifacts/levelplay-correlation-reentrancy-red.xml`: 0/2 passed, 2 failed. One exposed duplicate
   coordinator reload after a synchronous terminal callback; one exposed reward establishing an
   unknown unbound identifier. After fixes,
   `artifacts/levelplay-correlation-reentrancy-green.xml` passed 2/2.
5. A final synchronous provider display-failure mutation produced
   `artifacts/levelplay-sync-displayfailure-red.xml`: 0/1 passed, 1 failed (`Expected: False`,
   `But was: True`). After propagating the removed context as a rejection,
   `artifacts/levelplay-sync-displayfailure-green.xml` passed 1/1.

## Final verification

Final combined EditMode command:

`unity test /Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/unity --mode EditMode --filter 'CatMetro.Tests.LevelPlayPayloadMapperTests;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests;CatMetro.Tests.Ads.RewardedAdCoordinatorTests;CatMetro.Tests.RewardedAdsBootstrapTests;RevenueCat.Tests.RevenueCatAdReporterTests' --output /Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/artifacts/task6-final-editmode.xml --format json --non-interactive --timeout 600`

XML result: 137/137 passed, 0 failed, 0 inconclusive, 0 skipped:

- mapper/queue: 25/25
- LevelPlay provider: 39/39
- coordinator: 32/32
- bootstrap/drain owner: 17/17
- RevenueCat reporter: 24/24

Linked-source command:

`dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj --no-restore --filter 'FullyQualifiedName~RewardedAdCoordinatorTests' --logger 'console;verbosity=minimal'`

Result: 32/32 passed, 0 failed, 0 skipped.

Static gates:

- `bash scripts/check.sh`: `check: OK`
- `git diff --check`: clean
- final staged-scope/status and no-raw-log checks: clean except the required untracked crash JSON

One earlier command used the ambiguous project argument `unity`, which the CLI resolved to the
main checkout and returned a zero-test XML. That result was rejected and overwritten; no claim in
this report relies on it. Every accepted XML above records the absolute ads-worktree project path
and a nonzero expected fixture/count.

## Self-review audits

- Dependency direction: neutral Services/Integrations code has no package types; only the optional
  assembly references `Unity.LevelPlay`; the package test assembly is also version constrained.
- Package/source: manifest and lock pin LevelPlay 9.5.1 from Unity's default registry; exact
  resolved source members were compiled through real bridge bindings.
- Native guards: actual SDK initialization, construction, load, show, readiness, cap, and disposal
  operations are player-platform guarded in addition to package guards.
- Lifecycle: registration only registers a factory; existing bootstrap/coordinator initializes,
  loads, reloads, drains, replaces, and disposes. No second manager/GameObject was added.
- Format/unit scope: production source mentions/constructs only `LevelPlayRewardedAd`, with one
  runtime constructor call for the one configured rewarded unit. There is no banner,
  interstitial, app-open, or native-ad API.
- Correlation: stable AdId/AuctionId indexes only, bounded monotonic state, conservative unbound
  serialization, close-before-reward, no newest/fallback attribution, and reward-only exact
  coordinator grants.
- ILR: immutable scalar copy on the documented worker callback, explicit queue, tested concrete
  pump drain owner, replacement/teardown invalidation, and consumer exception isolation.
- Exceptions/cleanup: fake seam covers init/create/load/show/ready/cap, every event add/remove,
  disposal, consumer throws, idempotency, and one rewarded instance.
- Privacy/auto-init: no ATT/CMP/consent UI/call and no LevelPlay mediation settings resource.
- Secrets/scope: only explicit dummy test values occur; no `.env` was read, no credentials were
  added, no GameRoot, scene, Resources, `ProjectSettings`, render settings, or unrelated package
  registry was changed.
- Operations: no player/store binary, build, device command, install, upload, or push was run.

## Concerns and configured-device-only proof boundary

No local managed-code failure remains in the accepted evidence. Independent review is still
required. The package's cold-import editor tooling can regenerate compatible native SDK/adapter
dependency snapshots; actual dependency resolution must therefore be inspected in the eventual
human-run configured-player validation rather than inferred from Editor tests.

The following are explicitly not claimed by this implementation: native initialization and
adapter resolution; real fill/show/reward/close ordering; actual worker-thread ILR delivery;
dashboard placement caps and no-fill; ATT/CMP/privacy behavior; native teardown; and RevenueCat
Ads sandbox ingestion, dashboard dimensions, or revenue. Those require configured Android/iOS
device credentials and dashboard state. No device or player build was attempted.
