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

## Fix round 1 — callback attribution hardening

Independent review of `a7c3e758041762560ff1315e6dd86ce3236af0ca` found four valid managed-code
state-machine defects: cross-ID inconsistency and unsafe AdId reuse, transfer of identity after an
unbound context expired, loss of queued ILR after reward plus close, and continued callback
fan-out after reentrant disposal. Each finding was reproduced with a mutation-sensitive test
before production code changed; no existing test was weakened or removed.

### Fix implementation

- Stable IDs are now resolved bidirectionally before either index is mutated. If AuctionId and
  AdId already identify different contexts, the callback is dropped with no index mutation.
  Each context binds each stable field once, while the legitimate AdId-first then
  AuctionId-plus-same-AdId sequence remains supported.
- The AdId index no longer retains alias lists. Retired and ambiguous AdIds have bounded
  32-entry histories, and once an eviction proves finite history cannot safely classify an
  arbitrary AdId-only callback, AdId-only attribution fails closed. Auction-qualified callbacks
  remain usable and completed-auction tombstones remain independently bounded to 32.
- Every explicit Load has a monotonic generation and captures the stable `LevelPlayAdInfo`
  identity delivered by the corresponding current Loaded callback. After an unbound context
  expires, readiness/show remain quarantined until a newer Loaded generation supplies a nonblank
  AuctionId. AdId-only Loaded evidence cannot end that quarantine. Stale displayed, clicked,
  info-changed, ILR, stable display-failure, and anonymous display-failure callbacks cannot bind
  the replacement attempt; anonymous display failure is accepted only synchronously inside the
  exact current Show invocation.
- A completed reward-plus-close context with queued, undelivered ILR leaves a bounded 16-entry
  revenue-only terminal record keyed by AuctionId. The existing `MonetizationPump.Update` can
  consume it once in either terminal order. It expires on the same injected monotonic lifetime,
  cannot resolve rewards, and cannot reopen eligibility.
- `MainThreadAdEventQueue.Drain` rechecks its disposal generation between snapshot actions while
  keeping the lock released during consumers. Reentrant enqueue remains next-frame work,
  consumer exceptions remain isolated, and disposal from action one stops and clears all later
  work. Provider `EventReceived` fan-out likewise stops between handlers after reentrant dispose.
  Terminal callbacks also recheck disposal before any post-handler state mutation.

Fix-round production files modified:

- `unity/Assets/Scripts/Integrations/LevelPlay/LevelPlayRewardedAdProvider.cs`
- `unity/Assets/Scripts/Integrations/MainThreadAdEventQueue.cs`

Fix-round test files modified:

- `unity/Assets/Tests/EditMode/Engine/LevelPlayPayloadMapperTests.cs`
- `unity/Assets/Tests/EditMode/LevelPlay/LevelPlayRewardedAdProviderTests.cs`

### Fix-round TDD evidence

Every Unity invocation below used the absolute ads-worktree project path, omitted `-quit`, and
retained XML only. The focused command form was:

`unity test /Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/unity --mode EditMode --filter '<filters below>' --output /Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/artifacts/<artifact below> --format json --non-interactive --timeout 600`

The exact filters, artifacts, and parsed XML outcomes were:

1. Reentrant disposal filter
   `CatMetro.Tests.LevelPlayPayloadMapperTests.Queue_ReentrantDisposeStopsTheRemainingSnapshotAndPendingWork;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.DisposingEventConsumerStopsRemainingProviderFanOut`:
   `task6-fix1-reentrant-red.xml` passed 0/2 and failed 2 (queue drain returned 2 instead of 1;
   the later provider handler ran once instead of zero), then
   `task6-fix1-reentrant-green.xml` passed 2/2.
2. Stable-ID filter
   `CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.AContextBindsEachStableFieldOnceAndNeverRetainsAliases;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.AdIdFirstThenMatchingAuctionProgressionBindsTheSameAttempt;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.ConflictingAuctionAndAdContextsDropWithoutMutatingEitherIndex;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.ReusedAdIdCannotLetAnAdOnlyDuplicateGrantTheNewAttempt;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.StableIndexesAndCompletedOrAmbiguousHistoriesStayBounded`:
   `task6-fix1-stable-ids-red.xml` passed 0/6 and failed 6, then
   `task6-fix1-stable-ids-green1.xml` passed 6/6.
3. Expired-generation filter
   `CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.AnonymousDisplayFailureOutsideTheCurrentShowCallIsDropped;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.ExpiredUnboundContextRequiresANewerStableLoadedGeneration;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.ExpiredUnboundContextRestoresAvailabilityButLateRewardCannotBindNewShow;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.StalePostExpiryCallbackCannotBindAcrossTheFreshLoadedGeneration;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.SynchronousAnonymousDisplayFailureHasPositiveCurrentShowEvidence`:
   `task6-fix1-generation-red.xml` passed 7/10 and failed the 3 missing quarantine/anonymous
   guards, then `task6-fix1-generation-green.xml` passed 10/10. A stricter mutation that supplied
   only AdId in the newer Loaded callback produced
   `task6-fix1-generation-auction-anchor-red.xml` at 0/1; requiring a fresh AuctionId anchor
   produced `task6-fix1-generation-auction-anchor-green.xml` at 1/1.
4. Terminal ILR filter
   `CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.QueuedImpressionSurvivesBothRewardAndCloseTerminalOrders;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.TerminalRevenueCorrelationStateIsBounded;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.TerminalRevenueIsExactlyOnceAndExpiresWithoutDelivery`:
   `task6-fix1-terminal-ilr-red.xml` passed 0/4 and failed 4, then
   `task6-fix1-terminal-ilr-green.xml` passed 4/4. The two order cases enqueue ILR, deliver
   reward/close or close/reward, and invoke the real existing `MonetizationPump.Update` through
   `RewardedAdsComposition`; they do not call the provider drain directly.
5. Bounded-history saturation filter
   `CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.StableIndexesAndCompletedOrAmbiguousHistoriesStayBounded`:
   `task6-fix1-history-saturation-red.xml` passed 0/1 and failed 1 when an evicted AdId-only late
   duplicate could grant, then `task6-fix1-history-saturation-green.xml` passed 1/1.
6. Reentrant terminal cleanup filter
   `CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.ReentrantDisposeDuringTerminalRewardCannotRepopulateClearedState`:
   `task6-fix1-terminal-dispose-red.xml` passed 0/1 and failed 1 when the reward callback
   repopulated cleared state, then `task6-fix1-terminal-dispose-green.xml` passed 1/1.

The package-present provider/mapper regression artifact
`task6-fix1-provider-mapper-green1.xml` passed 85/85 at that checkpoint. A subsequent full fresh
combined run supersedes it.

### Fix-round final verification and audits

Final combined command, rerun after the last AuctionId-generation and reentrant-terminal fixes:

`unity test /Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/unity --mode EditMode --filter 'CatMetro.Tests.LevelPlayPayloadMapperTests;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests;CatMetro.Tests.Ads.RewardedAdCoordinatorTests;CatMetro.Tests.RewardedAdsBootstrapTests;RevenueCat.Tests.RevenueCatAdReporterTests' --output /Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/artifacts/task6-fix1-final-editmode.xml --format json --non-interactive --timeout 600`

The XML is timestamped `2026-08-30 09:54:24Z` and passed 159/159, failed 0,
inconclusive 0, skipped 0:

- provider state machine: 60/60
- mapper/queue: 26/26
- coordinator: 32/32
- bootstrap/composition/pump: 17/17
- RevenueCat reporter: 24/24

Linked-source command:

`dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj --no-restore --filter 'FullyQualifiedName~RewardedAdCoordinatorTests' --logger 'console;verbosity=minimal'`

Result: 32/32 passed, 0 failed, 0 skipped. `bash scripts/check.sh` returned `check: OK`.
`git diff --check` was clean.

The complete `76ee9963c856d54ccf2b15dda9a72de86c673869..working` Task 6 range and the
`a7c3e758041762560ff1315e6dd86ce3236af0ca..working` fix range were reviewed. Dependency
direction remains neutral-to-optional; only `Unity.LevelPlay` is referenced as the package
assembly and `Unity.Services.LevelPlay` remains the namespace. The manifest and lock retain the
single exact 9.5.1 package. All real native operations retain
`#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)`. Production constructs exactly one
`LevelPlayRewardedAd` for the configured rewarded unit and exposes no other format. The provider
never reloads after terminal callbacks; the coordinator remains the sole reload owner. The ILR
drain remains on the existing composition/pump lifecycle, and dispose severs it.

Scope/secret/static audits found no raw `.log`, mediation-settings Resources asset, second hidden
manager, scene, GameRoot, ProjectSettings, unrelated package, credentials, or non-dummy secret
value. The required untracked `unity/mono_crash.143e1228df.0.json` remains untouched and will not
be staged. No `.env`, process command line, device, player/store build, install, upload, push,
merge, or rebase operation was used.

The configured-device-only proof boundary is unchanged: Editor and linked managed tests cannot
prove native Android/iOS adapter resolution and initialization, actual fill/show/reward/close or
worker-thread ILR ordering, dashboard caps/no-fill, privacy/ATT/CMP behavior, native teardown, or
RevenueCat sandbox ingestion/dimensions/revenue. No native or device claim is made.

## Fix round 2 — saturated auction and serialized load evidence

Review of fix-round-1 commit `75e833d9be8859c3f7adb36e961cc4232e780dc4` confirmed the
terminal-ILR and reentrant-disposal findings were closed, but identified two remaining attribution
paths: an evicted completed AuctionId could bind a new unbound attempt, and overlapping Load calls
could relabel an older callback as a newer generation. It also identified a synchronous anonymous
display-failure permutation that could terminate a correctly loaded post-expiry replacement.

### Fix-round-2 implementation

- Completed AuctionId tombstones remain bounded to 32. The first eviction permanently raises an
  auction-history saturation fence. Thereafter, an unknown AuctionId cannot establish a context
  from an auction-only callback (or from a pair with no independently known current AdId).
- Safe post-saturation progression remains explicit: a serialized current Loaded callback is the
  only trusted source allowed to anchor an otherwise-new AuctionId, and a later AuctionId may be
  paired with an already indexed live AdId only after the existing bidirectional conflict checks.
  Known auctions continue resolving normally. The earlier bounded AdId saturation fence remains
  independent and unchanged.
- LevelPlay 9.5.1 has no Load request token, so the provider now permits at most one vendor Load in
  flight. Redundant pre-init, in-flight, and already-loaded Load calls collapse without queuing a
  hidden replacement. Loaded or LoadFailed ends the exact flight; a later explicit coordinator
  call may issue one replacement. A synchronous Load exception also ends the flight. No callback,
  close, display failure, or provider-owned timer auto-issues another load.
- Readiness is false while a Load callback is outstanding, preventing a delayed callback from
  crossing a Show. Loaded identity is copied only when that serialized flight is active, and the
  monotonic load generation increments only when a real vendor Load call is issued. An unusable
  post-expiry Loaded result (blank IDs or AdId only) does not deadlock replacement: the next
  explicit Load can replace it, while a consumable loaded result remains the one current ad.
- Accepting a fresh Loaded AuctionId after an expired unresolved context now leaves a terminal
  evidence barrier on the replacement Show. An anonymous DisplayFailed flushed synchronously by
  Show cannot cross that barrier. A stable callback that resolves the current context clears it;
  therefore a stable matching synchronous display failure still terminates normally, while a
  rejected anonymous stale failure leaves the replacement available for stable displayed,
  rewarded, and closed callbacks exactly once.

Round-2 changed files:

- `unity/Assets/Scripts/Integrations/LevelPlay/LevelPlayRewardedAdProvider.cs`
- `unity/Assets/Tests/EditMode/LevelPlay/LevelPlayRewardedAdProviderTests.cs`
- this report

No mapper, queue, composition, package, scene, settings, or asset file changed in this round.

### Fix-round-2 TDD evidence

All Unity commands used the absolute project path
`/Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/unity`, omitted `-quit`, and retained
NUnit XML only.

Focused RED command:

`unity test /Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/unity --mode EditMode --filter 'CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.ExplicitLoadsSerializeOneVendorRequestAndOneCallbackGeneration;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.LoadFailureCompletesTheFlightSoOneLaterExplicitLoadCanIssue;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.SaturatedAuctionHistoryRejectsEvictedAuctionOnlyBindingButAllowsSafeAnchors;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.PostExpirySynchronousTerminalRequiresStableCurrentIdentity' --output /Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/artifacts/task6-fix2-core-red.xml --format json --non-interactive --timeout 600`

Parsed RED result: 1/5 passed and 4/5 failed. The stable synchronous terminal control passed.
The four expected failures were:

- an overlapping explicit Load produced 2 vendor calls instead of 1;
- the load-failure replacement sequence produced 3 calls instead of 2;
- an anonymous synchronous failure rejected the post-quarantine replacement (`False` instead of
  `True`); and
- an evicted auction-only callback increased the reward count from 34 to 35.

The same filter and command with output `task6-fix2-core-green1.xml` passed 4/5. The remaining
failure showed the saturation fixture itself needed current Loaded anchors for terminal histories
created after the fence had already closed unknown binding. After making those histories
serialized and trusted rather than bypassing the production fence, the identical command with
output `task6-fix2-core-green2.xml` passed 5/5, failed 0, skipped 0.

Provider/mapper/queue command:

`unity test /Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/unity --mode EditMode --filter 'CatMetro.Tests.LevelPlayPayloadMapperTests;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests' --output /Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/artifacts/task6-fix2-provider-mapper-candidate2.xml --format json --non-interactive --timeout 600`

The first candidate exposed six existing fixtures whose fake callback flow was impossible under a
serialized request (85/91 passed). Their intended assertions were preserved while setup was
changed to complete the initial load, issue later Loads only after consumption, and exercise
event fan-out through a valid displayed callback. The run also exposed that blank/AdId-only
quarantine evidence must permit a later explicit replacement instead of deadlocking. After that
fix, `task6-fix2-provider-mapper-candidate2.xml` passed 91/91, failed 0, skipped 0. This includes
the earlier terminal ILR pump-drain and reentrant queue/provider disposal regressions.

### Fix-round-2 final verification and audits

Final combined command, rerun after the last production comment and strengthened exact event-order
assertion:

`unity test /Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/unity --mode EditMode --filter 'CatMetro.Tests.LevelPlayPayloadMapperTests;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests;CatMetro.Tests.Ads.RewardedAdCoordinatorTests;CatMetro.Tests.RewardedAdsBootstrapTests;RevenueCat.Tests.RevenueCatAdReporterTests' --output /Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/artifacts/task6-fix2-final-editmode.xml --format json --non-interactive --timeout 600`

The current-head XML is timestamped `2026-08-30 10:22:40Z` and passed 164/164, failed 0,
inconclusive 0, skipped 0:

- LevelPlay provider: 65/65
- mapper/queue: 26/26
- coordinator: 32/32
- bootstrap/composition/pump: 17/17
- RevenueCat reporter: 24/24

Linked-source command:

`dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj --no-restore --filter 'FullyQualifiedName~RewardedAdCoordinatorTests' --logger 'console;verbosity=minimal'`

Result: 32/32 passed, failed 0, skipped 0. `bash scripts/check.sh` returned `check: OK` and
`git diff --check` was clean.

The complete `75e833d9be8859c3f7adb36e961cc4232e780dc4..working` round-2 diff and cumulative
`76ee9963c856d54ccf2b15dda9a72de86c673869..working` Task 6 diff were reviewed. Round 2 changes
only the guarded provider state machine, its real-seam tests, and this report. Dependency
direction, exact 9.5.1 manifest/lock pin, `Unity.LevelPlay` assembly reference,
`Unity.Services.LevelPlay` namespace, and every native player-platform guard remain intact.
Production still constructs only the single configured rewarded unit, exposes no other ad
format, adds no auto-init Resources asset or hidden manager, and never reloads from provider
callbacks. The existing composition/pump drain, terminal-revenue-only records, reentrant disposal,
and exactly-once cleanup remain covered.

Secret/scope/static checks found no credential-bearing value, raw `.log`, GameRoot, scene,
Resources, ProjectSettings, unrelated package, or other worktree change. The sole unstaged item
remains the required `unity/mono_crash.143e1228df.0.json`, preserved without staging. No `.env`,
process command line, device, build, install, upload, push, merge, or rebase operation was used.

The configured-device-only proof boundary remains unchanged. Managed Editor evidence cannot prove
native Android/iOS adapter resolution/init, real fill/show/reward/close or worker-thread ILR
ordering, dashboard caps/no-fill, privacy/ATT/CMP behavior, native teardown, or RevenueCat sandbox
ingestion/dimensions/revenue. No native/device claim is made.

## Fix round 3 — retained AdId saturation bypass

Review of `50c312ef1501025cc1fede7161c47e3bb69665c6` found one remaining bypass in the
auction-history saturation exception. An unknown/evicted AuctionId was rejected when no AdId index
existed, but any indexed AdId was accepted as permission to continue. If that AdId belonged to an
older closed-but-reward-eligible context with a different known AuctionId, the resolver redirected
the pair to the newer unbound context, mutated its indexes, and let the matching stale reward grant
the newer attempt.

The correction remains inside the resolver's pre-mutation saturation check. When auction history
is saturated and the callback AuctionId is unknown, non-Loaded evidence now fails closed if its
indexed AdId context already owns a different AuctionId. The check runs after both indexes are
resolved and before maps, histories, context IDs, unbound ownership, or confirmation flags change.
Trusted serialized current Loaded identity remains an explicit exception. An AdId already bound to
the same context while its AuctionId is blank can still acquire that AuctionId, including after
close while the context remains reward-eligible. Known current auctions and ordinary
cross-consistent callbacks are unchanged; histories remain bounded.

Round-3 changed files:

- `unity/Assets/Scripts/Integrations/LevelPlay/LevelPlayRewardedAdProvider.cs`
- `unity/Assets/Tests/EditMode/LevelPlay/LevelPlayRewardedAdProviderTests.cs`
- this report

### Fix-round-3 TDD evidence

Focused RED command:

`unity test /Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/unity --mode EditMode --filter 'CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.SaturatedAuctionHistoryRejectsOldAdRedirectToEvictedAuctionWithoutMutation' --output /Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/artifacts/task6-fix3-retained-old-ad-red.xml --format json --non-interactive --timeout 600`

The real production provider test passed 0/1 and failed 1/1 at `2026-08-30 10:34:46Z`. After 34
terminal auctions, it retained AdId `shared` on a closed eligible context at
`retained-auction`, started a newer unbound attempt, then sent `shared` paired with evicted
`evicted-auction-0` through Displayed and Rewarded. The expected reward count stayed 34, but the
current resolver produced 35, directly proving the bypass.

Focused GREEN command:

`unity test /Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/unity --mode EditMode --filter 'CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.SaturatedAuctionHistoryRejectsOldAdRedirectToEvictedAuctionWithoutMutation;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.SaturatedAuctionHistoryRejectsEvictedAuctionOnlyBindingButAllowsSafeAnchors;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests.AdIdFirstThenMatchingAuctionProgressionBindsTheSameAttempt' --output /Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/artifacts/task6-fix3-saturation-green.xml --format json --non-interactive --timeout 600`

Result: 3/3 passed, failed 0, skipped 0. The new test additionally proves the rejected stale pair
does not mutate the newer attempt by safely binding that attempt through a legitimate current
AdId-to-AuctionId progression. It then closes an AdId-only context and proves the same retained
context can acquire its previously blank AuctionId and receive exactly one reward. The focused
set also preserves the prior direct auction-only saturation rejection and trusted Loaded anchor.

Provider/mapper/queue command:

`unity test /Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/unity --mode EditMode --filter 'CatMetro.Tests.LevelPlayPayloadMapperTests;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests' --output /Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/artifacts/task6-fix3-provider-mapper.xml --format json --non-interactive --timeout 600`

Result: 92/92 passed, failed 0, inconclusive 0, skipped 0. This includes all stable-ID, bounded
history, generation quarantine, serialized load, synchronous terminal, terminal-ILR concrete pump
drain, queue exception/reentrancy, and provider reentrant-disposal tests.

Final combined command:

`unity test /Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/unity --mode EditMode --filter 'CatMetro.Tests.LevelPlayPayloadMapperTests;CatMetro.Tests.LevelPlay.LevelPlayRewardedAdProviderTests;CatMetro.Tests.Ads.RewardedAdCoordinatorTests;CatMetro.Tests.RewardedAdsBootstrapTests;RevenueCat.Tests.RevenueCatAdReporterTests' --output /Users/sushantsrikrish/cat-metro-app/.claude/worktrees/ads/artifacts/task6-fix3-final-editmode.xml --format json --non-interactive --timeout 600`

The fresh post-edit XML is timestamped `2026-08-30 10:35:52Z` and passed 165/165, failed 0,
inconclusive 0, skipped 0:

- LevelPlay provider: 66/66
- mapper/queue: 26/26
- coordinator: 32/32
- bootstrap/composition/pump: 17/17
- RevenueCat reporter: 24/24

Linked-source command:

`dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj --no-restore --filter 'FullyQualifiedName~RewardedAdCoordinatorTests' --logger 'console;verbosity=minimal'`

Result: 32/32 passed, failed 0, skipped 0. `bash scripts/check.sh` returned `check: OK` and
`git diff --check` was clean.

The `50c312ef1501025cc1fede7161c47e3bb69665c6..working` round-3 diff and cumulative
`76ee9963c856d54ccf2b15dda9a72de86c673869..working` Task 6 diff were reviewed. Round 3 changes
one resolver guard, one real-provider mutation test, and this report. Exact package 9.5.1,
`Unity.LevelPlay` assembly direction, `Unity.Services.LevelPlay` namespace, native player guards,
single configured rewarded instance/format, coordinator-only reload, serialized load evidence,
existing pump drain, terminal revenue retention, reentrant disposal, and subscription cleanup are
unchanged and covered.

Narrow secret/scope/static audits found no credential-bearing value, raw `.log`, other format,
mediation-settings Resources asset, scene, GameRoot, ProjectSettings, package, or unrelated file
change. The required untracked `unity/mono_crash.143e1228df.0.json` remains untouched and will not
be staged. No `.env`, process command line, device, build, install, upload, push, merge, or rebase
operation was used.

The configured-device-only proof boundary remains unchanged: no claim is made for native adapter
resolution/init, real fill/show/reward/close or worker-thread ILR ordering, dashboard caps/no-fill,
privacy/ATT/CMP behavior, native teardown, or RevenueCat sandbox ingestion/dimensions/revenue.
