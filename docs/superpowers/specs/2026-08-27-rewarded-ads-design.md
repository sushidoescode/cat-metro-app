# Rewarded Ads and Durable Wardrobe Leases Design

**Date:** 2026-08-27
**Branch:** `feat/rewarded-ads`
**Scope:** Shipaton Task 11, Option 1

## Goal

Ship four useful, rewarded-video-only wardrobe placements backed by Unity
LevelPlay and reported through RevenueCat Ads. Every successful reward lends an
existing catalog entitlement for 24 hours through the same
`PurchaseService`/`EntitlementLedger` path used by purchases, survives restart,
and remains optional when configuration, consent, network, or fill is absent.

## Decisions

- Use Unity LevelPlay `com.unity.services.levelplay` 9.5.1 to serve and mediate
  rewarded ads.
- Use RevenueCat `AdTracker` from the already-pinned
  `com.revenuecat.purchases-unity` 9.9.0 package to report the LevelPlay ad
  lifecycle and impression-level revenue data.
- Rewarded video is the only ad format. There are no interstitial, banner,
  rewarded-interstitial, or app-open ad code paths.
- Do not request ATT from an ad offer and do not wait for ATT before allowing
  gameplay. LevelPlay receives privacy signals already known by the app before
  initialization and can otherwise operate without an IDFA.
- Keep the four existing 24-hour, named-item leases. Do not add paid randomness,
  virtual currency, retry rewards, or level-boundary placements.
- Failure rewind is the next rewarded-ad slice after this work and the mechanic
  ladder. It is not implemented or advertised by this branch.

## RevenueCat Ads and Catvertising Eligibility

The official rules require an app that uses the RevenueCat SDK for a purchase
or "serves ads through RevenueCat Ads," and the Catvertising submission must
describe how RevenueCat Ads fits the placements and revenue stack. RevenueCat's
current Ads documentation defines the product as tracking layered onto an
existing serving SDK, documents manual `AdTracker` integration for
ironSource/LevelPlay, and shows the data flow as ad SDK callback to RevenueCat
AdTracker to RevenueCat charts. RevenueCat's Catvertising article points to that
manual integration as the route for ironSource and other ILRD providers.

Therefore LevelPlay serving plus complete RevenueCat AdTracker reporting is the
documented RevenueCat Ads integration and is the implementation for this entry.
The rules do not contain a sentence naming the exact LevelPlay + AdTracker
combination, so written confirmation from the Shipaton organizer is still worth
obtaining before the deadline. That administrative confirmation does not change
the technical design.

The shipped-artifact proof is not a source scan. A configured device test must
show a rewarded LevelPlay impression, record the raw ILR/currency/precision and
mapped micros in device logs, and show the corresponding lifecycle/revenue
event and available dimensions in RevenueCat's Ads sandbox view. Test inventory
may report zero revenue; nonzero live ILR is separate optional evidence, not an
engineering blocker. The human supplies dashboard access and records the
evidence. The dashboard is not claimed to expose raw micros or precision unless
the configured product actually displays those fields.

Primary sources:

- <https://revenuecat-shipaton-2026.devpost.com/rules>
- <https://www.revenuecat.com/docs/ad-monetization>
- <https://www.revenuecat.com/docs/ad-monetization/manual-integration>
- <https://www.revenuecat.com/blog/engineering/monetizing-without-a-paywall-using-ads>
- <https://docs.unity.com/en-us/grow/levelplay/sdk/unity/impression-level-revenue-integration>

## Branch Dependency

TASK 19 owns integration of `feat/revenuecat` and the other active Shipaton
branches onto `main`. This branch must wait until TASK 19 confirms that
`feat/revenuecat` is an ancestor of the integrated `main`, then consume that
post-integration `main`. It must not merge `feat/revenuecat` directly. The
RevenueCat work supplies the product catalog, four placement rows,
`PurchaseService`, `EntitlementLedger`, RevenueCat package, purchase backend,
and Wardrobe surface. This branch extends those seams rather than copying them.

Six active branches touch `GameRoot.cs`, so this branch declares its complete
`GameRoot` delta in advance. Against TASK 19's integrated version, change only
the production save construction and installation in the daily-save bootstrap:

```diff
-    parsedBounds.Value, new MigrationTable());
+    parsedBounds.Value, MigrationTable.CreateDefault());
 _saveStore.Load();
+SaveRuntime.Install(_saveStore);
```

There is no ads-specific `GameRoot` field, pause callback, destroy callback, or
screen-composition edit. Ads and lease persistence subscribe to `SaveRuntime`
outside `GameRoot`, so TASK 19 can resolve this file from the exact two-line
contract above.

## Player Experience

The ad offer appears only beside a locked named wardrobe item, where the player
already wants access. The four placement IDs remain:

| Placement | Entitlement | Reward | Cap |
|---|---|---|---|
| `wardrobe_try_conductor` | `outfit_conductor` | 24-hour lease | 1/local day |
| `wardrobe_try_engineer` | `outfit_engineer` | 24-hour lease | 1/local day |
| `wardrobe_try_scarf` | `accessory_scarf` | 24-hour lease | 1/local day |
| `wardrobe_try_goggles` | `accessory_goggles` | 24-hour lease | 1/local day and 1/session |

Task 1 currently renders only the conductor coat. To make all four placements
real without taking over Task 13, the Wardrobe gains a small fixed try-on
preview strip. Each card has a simple, warm tabletop-style silhouette for the
named item, a locked/unlocked state, and an item-specific ad action. Its visual
state is derived only from `PurchaseService.IsUnlocked(entitlementId)`.

The preview strip does not add profile-cat selection, equipped-item state,
outfit compatibility, persisted appearance, 3D asset binding, or an owned-item
collection. Those remain Task 13. Multiple active leases simply illuminate
multiple previews; this branch invents no equip priority.

The action copy is equivalent to "Watch to borrow today." It uses the existing
cream paper, depot navy, ticket orange, teal, rounded chips, and dp/safe-area
layout conventions. It contains no network logo and no generic full-screen
monetization panel.

When an ad is not ready, capped, disabled, unconfigured, or unavailable, the
normal purchase path and all gameplay remain usable. The ad action is hidden or
shown unavailable without a spinner, modal wait, or retry loop. Ads are loaded
proactively after initialization; tapping never starts a blocking load.

## Architecture

### Engine-free ads seam

Add a focused `CatMetro.Services.Ads` area containing:

- `IRewardedAdProvider`: initialize, load, readiness, show, and provider event
  callbacks without Unity or vendor types.
- `IAdEventReporter`: loaded, displayed, opened, revenue, and load-failure
  reporting with a neutral immutable event DTO.
- `RewardedAdCoordinator`: owns one show attempt at a time, placement/cap
  preflight, reward deduplication, callback ordering, and calls the shared
  `PurchaseService` grant API.
- `RewardedAdRuntime`: the existing runtime-composition style used by
  `PurchaseRuntime`, with a no-op coordinator until installation.

There is one provider and one reusable rewarded ad unit. All four placement
names pass through `ShowAd(placement.Id)` and through the RevenueCat event DTO.
No SDK type crosses into Services, Application, Presentation, or the linked
.NET build.

### Optional Unity integrations

Create separate optional assemblies:

- `CatMetro.Integrations.LevelPlay` references
  `Unity.Services.LevelPlay` and compiles only when the LevelPlay package
  supplies `CATMETRO_LEVELPLAY` through an asmdef `versionDefines` entry.
- `CatMetro.Integrations.RevenueCatAds` references the RevenueCat package and
  compiles only with `CATMETRO_REVENUECAT`.

Concrete vendor calls are additionally guarded by:

```csharp
#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
```

The base project always compiles with no ad packages or mobile target and uses
no-op implementations. Direct typed SDK calls are used inside the optional
assemblies; reflection is not used because it is fragile under IL2CPP.

LevelPlay initialization and object creation follow the 9.x ad-unit API:

1. Set known privacy flags before `LevelPlay.Init`.
2. Subscribe to initialization callbacks before initializing.
3. Create `LevelPlayRewardedAd` only after successful initialization.
4. Subscribe to every rewarded callback before `LoadAd`.
5. Show only when `IsAdReady()` and the placement is not LevelPlay-capped.
6. Reload after a closed/failed display without blocking any screen.

The package source is the compile-time authority for exact 9.5.1 property and
event names. In particular, the implementation must confirm the per-ad
impression callback and stable auction/impression identifier instead of
inventing an identifier from placement or time.

### RevenueCat reporting

The neutral provider event is mapped to RevenueCat as follows:

- LevelPlay loaded -> `TrackAdLoaded`
- LevelPlay displayed -> `TrackAdDisplayed`
- LevelPlay clicked -> `TrackAdOpened`
- LevelPlay load failure -> `TrackAdFailedToLoad`
- LevelPlay impression-level revenue callback -> `TrackAdRevenue`

Use rewarded format, a stable custom mediator name of `LevelPlay`, the exact
placement ID, ad-unit ID, serving network, one provider-supplied impression ID
across an impression's events, USD revenue converted to integer micros, and an
explicit precision mapping: `BID` -> `Exact`, `RATE` -> `PublisherDefined`,
`CPM` -> `Estimated`, and every unknown/null value -> `Unknown`. This follows
LevelPlay's definitions: BID is auction-provided, RATE is manually assigned by
the publisher, and CPM is calculated from historical performance. Display
failures have no corresponding RevenueCat Ads event and remain internal
failure telemetry; they are not mislabeled as load failures.

Impression-revenue callbacks arrive off the main thread; data is copied there
and dispatched before touching Unity or RevenueCat objects.

Missing or malformed impression metadata is logged and omitted rather than
fabricated. A malformed analytics event never revokes a legitimately earned
reward. Conversely, shipped ads fail closed when the RevenueCat Ads reporter
is absent: the optional offer disappears and gameplay continues, avoiding a
release that serves untracked ads while claiming Catvertising integration.

### Reward ordering and exactly-once behavior

The LevelPlay reward and close callbacks are asynchronous and may arrive in
either order. Each `Show` creates one attempt containing its placement and an
exactly-once reward latch. Only the reward callback can grant the item;
`OnAdClosed` never grants it. A close-before-reward attempt remains eligible for
the late reward, and a second show cannot reuse that attempt. Duplicate reward
callbacks are ignored.

Display failure ends the attempt without a grant. Load/no-fill failure updates
availability and schedules a later background load; it never opens a blocking
error modal. A grant refusal reports a specific outcome such as already
unlocked, capped, invalid placement, or persistence failure.

## Durable Entitlement Leases

There remains one entitlement ledger and one save file.

Add a Services-level lease-persistence interface used by
`PurchaseService.GrantRewardedAdEntitlement`. The Application implementation
stores only local rewarded-ad leases in the existing `SaveStore` payload. It
does not persist CustomerInfo/store/promotional grants, because RevenueCat must
remain authoritative for refunds, expirations, and restores.

The save schema advances from version 1 to version 2 and adds isolated lease
and rewarded-cap subshapes:

```json
{
  "entitlements": {
    "appUserId": "",
    "active": [],
    "fetchedAtUtc": 0,
    "localLeases": [
      { "entitlementId": "outfit_conductor", "expiresAtUnixSeconds": 1780000000 }
    ]
  },
  "caps": {
    "rewarded": {
      "dateKey": "",
      "counters": {}
    }
  }
}
```

The v1-to-v2 migration adds an empty `localLeases` array and empty
`caps.rewarded` object only when absent and preserves every unknown field. It
does not add placement IDs to or reset the existing fixed five-key
`caps.counters` object. Loading accepts only known, ad-grantable entitlements
with a positive future expiry and imports them through
`EntitlementLedger.ImportLeases` before Wardrobe composition. A known v1
container with a non-object shape fails migration rather than being silently
replaced; valid unknown sibling fields are untouched.

### Save schema v2 migration contract for TASK 13

This branch owns save schema version 2. There is exactly one registered
`1 -> 2` migration, exposed by `MigrationTable.CreateDefault()` and implemented
by `SaveSchemaV2.MigrateFromV1(JObject payload)`. It is additive, idempotent,
uses set-if-absent semantics, adds `entitlements.localLeases` plus the isolated
`caps.rewarded` counter container, and never discards an unknown field.

TASK 13 must not independently bump version 1 to 2 or register a second
`1 -> 2` step. Before the first public v2 build, TASK 13 may add its presentation
defaults to `SaveSchemaV2.MigrateFromV1` and `SaveDefaults.FreshPayload` as part
of the same migration. Its persisted selection belongs in a top-level
`cosmetics` object; owned item IDs remain authoritative in the entitlement
ledger and must not be copied there. After any public v2 release, a new required
schema change uses v3 (or handles a missing optional key at read time), because
an already-v2 file will never rerun the v1 migration.

The shared v2 shape owned here is:

```json
{
  "saveVersion": 2,
  "entitlements": {
    "localLeases": []
  },
  "caps": {
    "rewarded": {
      "dateKey": "",
      "counters": {}
    }
  }
}
```

Existing `entitlements` members, legacy `caps.counters`, and all unrelated
top-level members are preserved. Neither TASK 13 nor this branch creates a
second save file, `PlayerPrefs` authority, a second entitlement store, or a
separate version counter.

Grant durability is ordered deliberately:

1. `PurchaseService` validates the catalog entitlement and computes the lease
   expiry from `adLeaseSeconds`.
2. It forms the candidate lease set from `EntitlementLedger.ExportLeases`.
3. The persistence adapter writes that candidate to the existing payload and
   succeeds through `SaveStore.TryCommitAtomic`.
4. Only after durable commit does `EntitlementLedger.GrantLease` publish the
   in-memory unlock and `Changed` event.

If persistence refuses or throws, the payload candidate is rolled back in
memory, the ledger is unchanged, and the outcome is `PersistenceFailed`. Thus
the UI never calls an unlock durable when it exists only in RAM. A crash after
disk commit but before the in-memory publish is safe: the lease appears on the
next boot.

The same `SaveStore` records local-date cap counters under `caps.rewarded`.
Session caps remain memory-only. Cap checks happen before `Show`; counters
advance only after a durable granted outcome. Lease and cap use two ordered
commits deliberately: a cap-write failure or crash between them can lose that
day's counter, but it never takes away the already durable earned item, and the
current session still consumes the opportunity. The lease is the user-value
transaction and has priority; the design does not claim anti-farming durability
under a disk fault.

TASK 19's integrated `GameRoot` owns and loads the production `SaveStore` using
`EngineStorageRoot` and `RealSaveFileSystem`. Immediately after load it installs
that same instance through `SaveRuntime.Install`. The lease/cap adapter observes
that shared runtime and owns its save-on-pause component; it does not add more
`GameRoot` lifecycle methods. No `PlayerPrefs`, second file, cached CustomerInfo
authority, or ad-specific unlock flag is introduced.

## Configuration and Human Setup

Commit an example config and ignore the real resource file. The human supplies:

- LevelPlay iOS app key
- LevelPlay Android app key
- rewarded ad-unit ID for each platform
- these four exact LevelPlay placement names
- selected mediation network credentials and matching adapters in LevelPlay
- RevenueCat public Apple/Google SDK keys through the integration landed by
  TASK 19
- RevenueCat Ads beta access enabled for the project
- privacy/CMP decisions and the App Store/Play data disclosures

No secret key, network credential, dashboard token, or real config file enters
Git. The repository never reads `.env`. Missing config installs the no-op path
and leaves the game fully playable.

## Validation

### Headless and EditMode

- Provider missing, initialization failure, load failure, no fill, and cap
  exhaustion leave the purchase and gameplay paths usable.
- A ready placement shows once and only the reward callback can grant it.
- Close-before-reward grants correctly; duplicate reward callbacks do not.
- Unknown, disabled, owned, expired, and non-ad-grantable placements fail
  closed.
- Paid grants and ad leases produce the same `IsUnlocked` result.
- Store refresh never deletes a local lease; permanent ownership wins over an
  expiring lease.
- Grant, atomic save, fresh service, migration, and import preserve the
  original expiry rather than restarting 24 hours.
- Corrupt, expired, unknown, and non-ad-grantable saved rows never unlock.
- Store/promotional grants never appear in `localLeases`.
- All four Wardrobe previews respond independently to paid and ad grants and
  disappear on expiry.
- The project compiles both with optional SDK defines and in the package-absent
  Editor/.NET configuration.

### PlayMode and visual evidence

- Repeated Wardrobe open/close does not duplicate input or SDK subscriptions.
- The preview strip and ad action remain inside safe areas at the project's
  phone reference resolutions.
- No-fill and unconfigured captures prove the normal purchase route remains
  usable.
- A restarted PlayMode/device flow proves an earned item remains visible.

### Configured device evidence

- Run LevelPlay test ads on the actual selected iOS test device first.
- Verify exactly one reward and a surviving restart.
- Verify placement, mediator, network, impression ID, and revenue in
  RevenueCat's Ads sandbox view.
- Capture the Wardrobe offer, completed reward, and borrowed item for the
  Shipaton video.
- Repeat integration checks on the Pixel 9 Pro before Android release work.

Store upload remains human-only. An APK or store binary is not built or
uploaded by this task unless the human separately runs the documented build
path.

## Acceptance Criteria

1. Four named Wardrobe placements are visible at genuine locked-item need
   points and no level-boundary ad exists.
2. Only rewarded video can be created or shown.
3. No ad load, consent state, SDK error, or no-fill can block gameplay or the
   purchase route.
4. Each successful reward goes through `PurchaseService` and the shared
   `EntitlementLedger`; Presentation has no source-specific unlock state.
5. A granted lease survives process restart with its original expiry.
6. RevenueCat Ads receives LevelPlay lifecycle and ILRD events in its sandbox
   view on a configured device.
7. Optional SDK/package absence does not break Editor or linked .NET
   compilation.
8. Relevant .NET, EditMode, and PlayMode suites pass with no introduced
   failures; configured-device and dashboard checks are reported separately.
