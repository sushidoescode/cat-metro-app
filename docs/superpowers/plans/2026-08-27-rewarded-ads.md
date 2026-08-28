# Rewarded Ads and Durable Wardrobe Leases Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship four non-blocking wardrobe rewarded-video placements served by Unity LevelPlay, tracked through RevenueCat 9.9 `AdTracker` for the RevenueCat Ads product, and granted as durable 24-hour leases through the existing `PurchaseService` and `EntitlementLedger` path. This is intended for Catvertising; organizer confirmation of the exact LevelPlay + `AdTracker` combination remains a nonblocking human follow-up.

**Architecture:** TASK 19 first lands the active Shipaton branches on `main`. This branch then consumes that integrated `main`, adds one shared save-v2 migration and `SaveRuntime` publication point, makes rewarded grants durable before publishing them in memory, composes a provider-neutral rewarded coordinator around the existing purchase runtime, and keeps LevelPlay and RevenueCat vendor types in optional integration assemblies. The Wardrobe reads only source-blind entitlement state and rewarded availability.

**Tech Stack:** Unity 6000.3.16f1, C# 9/10, Newtonsoft JSON, NUnit/EditMode/PlayMode, .NET 8 linked-source tests, RevenueCat purchases-unity 9.9.0 `AdTracker`, Unity LevelPlay `com.unity.services.levelplay` 9.5.1, uGUI/TMP.

**Spec:** [`docs/superpowers/specs/2026-08-27-rewarded-ads-design.md`](../specs/2026-08-27-rewarded-ads-design.md)

## Global Constraints

- Do not merge `feat/revenuecat` into this branch. Wait for TASK 19, prove that branch is in integrated `main`, then merge only the resulting `origin/main`.
- TASK 19 owns the contested `GameRoot.cs` resolution. This lane's complete delta is the exact two-line change published below; do not add an ads field or another lifecycle method there.
- This lane owns save schema v2. There is one combined `1 -> 2` migration containing Daily Live fields, `entitlements.localLeases`, and the isolated rewarded-cap shape; TASK 13 may add cosmetics defaults to that same function before the first public v2 build.
- Only rewarded video exists. Do not add interstitial, banner, rewarded-interstitial, app-open, paid randomness, level-boundary, or ATT-gated paths.
- A missing config, unavailable reporter, initialization error, no fill, cap, or SDK exception hides the optional offer and leaves gameplay and purchase UI usable.
- Only a reward callback may grant. Close never grants. Close-before-reward and duplicate callbacks must be safe.
- Lease bytes must commit atomically before the entitlement ledger publishes `Changed`. Use the existing `SaveStore`; do not add another file, `PlayerPrefs`, or another entitlement authority.
- LevelPlay serves and locally signals the reward; RevenueCat `AdTracker` records ad-monetization events. This plan does not use or imply RevenueCat's separate AdMob-oriented verified-reward flow, and it does not claim confirmed Catvertising eligibility without organizer confirmation.
- Do not read environment-secret files, commit real app keys, build/upload a store artifact, or run any store upload. The human supplies dashboard configuration and performs release builds/uploads.
- Use `grep -E`, never `git commit -a`, never force-push, and include `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>` on every commit.
- Run Unity commands outside the filesystem sandbox. Unity test commands must never contain `-quit`. A full EditMode run can be silent for about 21 minutes.
- Run linked-source .NET restore/tests outside the sandbox with `-p:RestoreLockedMode=true`; never disable lock-file generation.
- Validation claims attach to artifacts: reload actual save bytes, render/capture the Wardrobe, compile both optional SDK states, and reserve network/dashboard claims for a configured device.

## Published Coordination Contracts

### TASK 19: exact `GameRoot.cs` delta

Against TASK 19's integrated `GameRoot.InitializeDailyLiveServices`, make only:

```diff
-_saveStore = new SaveStore(storage, new RealSaveFileSystem(),
-    parsedBounds.Value, new MigrationTable());
+_saveStore = new SaveStore(storage, new RealSaveFileSystem(),
+    parsedBounds.Value, MigrationTable.CreateDefault());
 _saveStore.Load();
+SaveRuntime.Install(_saveStore);
 _dailyProgress = new DailyProgressTracker(_saveStore);
```

Do not change `GameRoot` pause/focus/destroy methods for ads. `MonetizationPump` observes `SaveRuntime` and owns rewarded-save pause work.

### TASK 13: save-v2 migration contract

`SaveDefaults.SAVE_VERSION` stays `2`. `MigrationTable.CreateDefault()` registers exactly one `1 -> 2` step: `SaveSchemaV2.MigrateFromV1`. That function is additive, idempotent, and set-if-absent. It must preserve Daily Live's `daily.trustedDateKey`, `daily.completedKeys`, and `daily.lifetimeCompletions`, add `entitlements.localLeases: []`, add `caps.rewarded: { "dateKey": "", "counters": {} }`, and round-trip unrelated fields. The existing fixed five-key `caps.counters` object is untouched.

Before the first public v2 build, TASK 13 may extend both `SaveSchemaV2.MigrateFromV1` and `SaveDefaults.FreshPayload()` with a top-level `cosmetics` object for presentation selections. It must not register another `1 -> 2` step, bump the version independently, copy owned IDs out of the entitlement ledger, or create another file. After a public v2 build, a required schema change is v3; existing v2 files will not rerun the v1 migration.

---

## Task 0: Consume TASK 19's Integrated Main and Re-establish the Baseline

**Files:**

- Inspect: `unity/Assets/Scripts/Bootstrap/GameRoot.cs`
- Inspect: `unity/Assets/Scripts/Application/Save/MigrationTable.cs`
- Inspect: `unity/Assets/Scripts/Integrations/MonetizationBootstrap.cs`
- Inspect: `unity/Assets/Scripts/Integrations/RevenueCat/RevenueCatBackend.cs`
- Inspect: `unity/Assets/Scripts/Presentation/Screens/WardrobeScreenView.cs`
- No source edits in this task.

- [ ] **Step 1: Fetch TASK 19's result and prove both required ancestors landed**

Run from the ads worktree:

```bash
git fetch origin
git merge-base --is-ancestor feat/revenuecat origin/main
git merge-base --is-ancestor feat/daily-live origin/main
```

Expected: both commands exit `0`. If either exits non-zero, stop this task and coordinate with TASK 19; do not merge either feature branch directly.

- [ ] **Step 2: Consume integrated main only**

```bash
git status --short
git merge --no-edit origin/main
```

Expected: the documentation commit is the only pre-merge branch work, and the merge brings the RevenueCat catalogue/runtime, Wardrobe, Daily Live `SaveStore`, and all other TASK 19 resolutions into this branch.

- [ ] **Step 3: Inspect the contested composition sites**

```bash
grep -n -E "new SaveStore|MigrationTable|_saveStore.Load|DailyProgressTracker" unity/Assets/Scripts/Bootstrap/GameRoot.cs
grep -n -E "PurchaseRuntime.Install|PurchaseBackendFactory|MonetizationPump" unity/Assets/Scripts/Integrations/MonetizationBootstrap.cs
grep -n -E "class RevenueCatBehaviour|Purchases _purchases|Availability" unity/Assets/Scripts/Integrations/RevenueCat/RevenueCatBackend.cs
```

Expected: one production `SaveStore` exists, `PurchaseRuntime` is installed before screen composition, and `RevenueCatBehaviour` owns the one `Purchases` instance that the ad reporter will reuse.

- [ ] **Step 4: Run the post-integration baseline**

```bash
bash scripts/check.sh
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj -p:RestoreLockedMode=true
```

Expected: static checks pass and all linked-source tests pass. Record the exact pass count; do not assume the earlier 857-test baseline survived TASK 19 unchanged.

- [ ] **Step 5: Record the integrated base without making a synthetic source commit**

```bash
git status --short
git log -1 --oneline
```

Expected: clean worktree after the merge. The merge commit itself is the checkpoint for this task.

---

## Task 1: Make Save Schema v2 a Single Shared Migration and Publish `SaveRuntime`

**Files:**

- Create: `unity/Assets/Scripts/Application/Save/SaveSchemaV2.cs`
- Create: `unity/Assets/Scripts/Application/Save/SaveRuntime.cs`
- Modify: `unity/Assets/Scripts/Application/Save/MigrationTable.cs`
- Modify: `unity/Assets/Scripts/Application/Save/SaveDefaults.cs`
- Modify: `unity/Assets/Scripts/Application/Save/SaveStore.cs`
- Modify: `unity/Assets/Scripts/Bootstrap/GameRoot.cs` — only the published two-line delta
- Modify: `unity/Assets/Tests/EditMode/Pure/Save/SaveMigrationTests.cs`
- Modify: `unity/Assets/Tests/EditMode/Pure/Save/SavePayloadTests.cs`
- Create: `unity/Assets/Tests/EditMode/Pure/Save/SaveRuntimeTests.cs`
- Include Unity-generated `.meta` files for each new file under `unity/Assets/`.

- [ ] **Step 1: Write the failing v2 union-migration tests**

Add tests that start from a representative v1 payload with the three new Daily fields, `localLeases`, and `caps.rewarded` absent, plus unrelated future keys. Require:

```csharp
var migrated = MigrationTable.CreateDefault().Migrate(legacy, 1, 2);

Assert.That((int)migrated["saveVersion"], Is.EqualTo(2));
Assert.That(migrated["daily"]["completedKeys"], Is.InstanceOf<JArray>());
Assert.That(migrated["entitlements"]["localLeases"], Is.InstanceOf<JArray>());
Assert.That((string)migrated["caps"]["rewarded"]["dateKey"], Is.Empty);
Assert.That(migrated["caps"]["rewarded"]["counters"], Is.InstanceOf<JObject>());
Assert.That((bool)migrated["futureExperiment"]["kept"], Is.True);
Assert.That(JToken.DeepEquals(
    SaveSchemaV2.MigrateFromV1((JObject)migrated.DeepClone()), migrated), Is.True,
    "v1 migration is idempotent so TASK 13 can extend one shared step safely");
```

Also require `SaveDefaults.FreshPayload()` to contain empty `localLeases` and isolated `caps.rewarded` defaults while the legacy `caps.counters` remains exactly its existing five keys. Require duplicate `Register(1, 2, ...)` calls to throw rather than silently shadow one another.

Add an artifact migration test that writes a real v1 `SaveHeader` plus payload to `save.dat`, loads it through `new SaveStore(..., migrations: null)`, asserts `LoadResult.Ok` rather than a fresh fallback, commits, and reloads a second `SaveStore`. The final parsed v2 bytes must contain Daily, leases, rewarded caps, and the unknown sentinel. This proves the production default wiring, header version, migration, serializer, atomic commit, and reload together.

Add malformed-known-container cases: if existing `daily`, `entitlements`, `caps`, or `caps.rewarded` is a non-object, `SaveSchemaV2.MigrateFromV1` returns null instead of silently deleting that known malformed token. Unrelated unknown fields remain untouched.

- [ ] **Step 2: Write the failing `SaveRuntime` identity/event tests**

Require `SaveRuntime.Install(store)` to publish the exact instance synchronously, set `IsInstalled`, notify one `Installed` subscriber with that instance, ignore null, ignore a repeated install of the same reference, publish a genuinely new store reference once, and reset all static state through `ResetForTests()`.

- [ ] **Step 3: Run only the failing save tests**

```bash
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj -p:RestoreLockedMode=true --filter "FullyQualifiedName~SaveMigrationTests|FullyQualifiedName~SavePayloadTests|FullyQualifiedName~SaveRuntimeTests"
```

Expected: compile/test failure because `SaveSchemaV2`, `CreateDefault`, `SaveRuntime`, and the fresh `localLeases` field do not exist yet.

- [ ] **Step 4: Implement the one migration registration site**

Move the existing Daily v1 migration body into the new class and add the lease array there:

```csharp
public static class SaveSchemaV2
{
    public static JObject MigrateFromV1(JObject payload)
    {
        if (payload == null) return null;
        var daily = GetOrCreateObject(payload, "daily");
        if (daily == null) return null;
        if (daily["trustedDateKey"] == null) daily["trustedDateKey"] = "";
        if (daily["completedKeys"] == null) daily["completedKeys"] = new JArray();
        if (daily["lifetimeCompletions"] == null) daily["lifetimeCompletions"] = 0;

        var entitlements = GetOrCreateObject(payload, "entitlements");
        if (entitlements == null) return null;
        if (entitlements["localLeases"] == null)
            entitlements["localLeases"] = new JArray();

        var caps = GetOrCreateObject(payload, "caps");
        if (caps == null) return null;
        var rewarded = GetOrCreateObject(caps, "rewarded");
        if (rewarded == null) return null;
        if (rewarded["dateKey"] == null) rewarded["dateKey"] = "";
        if (rewarded["counters"] == null) rewarded["counters"] = new JObject();
        return payload;
    }
}
```

Make a bare `MigrationTable` useful for custom test steps, but make production defaults explicit and unique:

```csharp
public static MigrationTable CreateDefault() => new MigrationTable()
    .Register(1, 2, SaveSchemaV2.MigrateFromV1);
```

`Register` rejects a second step with the same `From`. `SaveStore` uses `MigrationTable.CreateDefault()` when its migration argument is null. Update tests/fixtures that genuinely require production migration to call the factory.

- [ ] **Step 5: Add `localLeases` to fresh saves and implement the runtime publication seam**

Add `localLeases = new JArray()` alongside existing entitlement fields and add `caps.rewarded` without changing the legacy five-key `caps.counters`. Update exact-shape tests to name the new owned subshape explicitly. Implement `SaveRuntime` in `CatMetro.Application.Save` with:

```csharp
public static SaveStore Current { get; private set; }
public static bool IsInstalled => Current != null;
public static event Action<SaveStore> Installed;
public static void Install(SaveStore store);
public static void ResetForTests();
```

`Install` ignores null and the already-current reference, and otherwise invokes subscribers synchronously after assigning `Current`; reset clears both the instance and event handlers for Unity domain-reload-safe tests.

- [ ] **Step 6: Apply exactly the declared `GameRoot` delta**

Change `new MigrationTable()` to `MigrationTable.CreateDefault()`, then add `SaveRuntime.Install(_saveStore)` immediately after `_saveStore.Load()` and before constructing `DailyProgressTracker`. Make no other `GameRoot.cs` edit.

- [ ] **Step 7: Run the focused and static checks**

```bash
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj -p:RestoreLockedMode=true --filter "FullyQualifiedName~SaveMigrationTests|FullyQualifiedName~SavePayloadTests|FullyQualifiedName~SaveRuntimeTests"
bash scripts/check.sh
git diff -- unity/Assets/Scripts/Bootstrap/GameRoot.cs
```

Expected: focused tests pass; the final diff command shows only the migration-factory replacement and one `SaveRuntime.Install` line.

- [ ] **Step 8: Commit the shared migration contract implementation**

```bash
git add unity/Assets/Scripts/Application/Save unity/Assets/Scripts/Bootstrap/GameRoot.cs unity/Assets/Tests/EditMode/Pure/Save
git commit -m "feat(save): add durable entitlement lease schema" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

## Task 2: Make Rewarded Entitlement Grants Durable Before Ledger Publication

**Files:**

- Create: `unity/Assets/Scripts/Services/Purchases/IEntitlementLeasePersistence.cs`
- Modify: `unity/Assets/Scripts/Services/Purchases/PurchaseService.cs`
- Create: `unity/Assets/Scripts/Application/Save/RewardedAdSaveStore.cs`
- Modify: `unity/Assets/Tests/EditMode/Pure/Purchases/AdPurchaseConvergenceTests.cs`
- Modify: `unity/Assets/Tests/EditMode/Pure/Purchases/PurchaseFixtures.cs`
- Create: `unity/Assets/Tests/EditMode/Pure/Save/RewardedAdSaveStoreTests.cs`
- Include matching `.meta` files.

- [ ] **Step 1: Write failing service-level durability tests**

Add a recording fake for:

```csharp
public interface IEntitlementLeasePersistence
{
    bool TryReplaceRewardedAdLeases(IReadOnlyList<EntitlementGrant> leases);
}
```

Require all of these observable outcomes:

- without a persistence adapter, `CanOfferAdFor` still answers the catalogue/ledger value question, `CanPersistRewardedAdGrants` is false, and a direct grant returns `PersistenceFailed` without unlocking;
- a refusing or throwing adapter returns `PersistenceFailed` and leaves `Ledger.ExportLeases()` unchanged;
- the adapter has recorded the candidate lease before the ledger's `Changed` event fires;
- a successful grant persists only `GrantSource.RewardedAd` rows and then unlocks through `IsUnlocked`;
- an existing paid/promotional grant is never serialized as a local lease;
- restored rows keep their saved absolute expiry instead of restarting 24 hours;
- unknown, expired, permanent, and non-ad-grantable restored rows do not unlock.

Extend `AdGrantOutcome` with `PersistenceFailed`.

- [ ] **Step 2: Write failing save-adapter artifact tests**

Using the existing in-memory/temp `ISaveFileSystem` fixtures, require `RewardedAdSaveStore.TryReplaceRewardedAdLeases` to:

1. deep-clone the current payload;
2. write deterministic rows sorted by entitlement ID;
3. atomically commit through `SaveStore.TryCommitAtomic()`;
4. restore the original payload identity on refusal or exception;
5. reload the committed file into a fresh `SaveStore` and return the original expiry;
6. preserve unrelated top-level and `entitlements` fields.

- [ ] **Step 3: Run the tests and observe the red state**

```bash
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj -p:RestoreLockedMode=true --filter "FullyQualifiedName~AdPurchaseConvergenceTests|FullyQualifiedName~RewardedAdSaveStoreTests"
```

Expected: failure because rewarded grants still mutate the ledger directly and no adapter/outcome exists.

- [ ] **Step 4: Implement precommit ordering in `PurchaseService`**

Add `AttachLeasePersistence(IEntitlementLeasePersistence)`, read-only `CanPersistRewardedAdGrants`, and `RestoreRewardedAdLeases(IReadOnlyList<EntitlementGrant>)`. Keep `CanOfferAdFor` catalogue/ledger-only so its name continues to mean “would this lease add access?”; the coordinator combines it with `CanPersistRewardedAdGrants` before showing. Filter restored rows against the live product catalogue, `IsAdGrantable`, `GrantSource.RewardedAd`, positive future expiry, and the injected clock before importing them.

For a new grant, use this order:

```csharp
if (!_ledger.CanGrantLease(id, expiresAt, now)) return AdGrantOutcome.AlreadyUnlocked;
var candidate = ActiveLeaseCandidateWith(id, expiresAt, now);
try
{
    if (_leasePersistence == null ||
        !_leasePersistence.TryReplaceRewardedAdLeases(candidate))
        return AdGrantOutcome.PersistenceFailed;
}
catch
{
    return AdGrantOutcome.PersistenceFailed;
}
return _ledger.GrantLease(id, expiresAt, now)
    ? AdGrantOutcome.Granted
    : AdGrantOutcome.AlreadyUnlocked;
```

`ActiveLeaseCandidateWith` is a private deterministic helper, not an undefined abstraction: start from `ExportLeases()`, retain every other active future rewarded lease, remove the target row, add the new target expiry, and sort by entitlement ID. It must not include expired rows or any store/promotional source.

Do not let the persistence adapter mutate the ledger. Update older purchase/PlayMode test fixtures that intentionally grant ads to attach an in-memory accepting adapter; tests for missing persistence must remain explicit.

- [ ] **Step 5: Implement `RewardedAdSaveStore` over the existing payload**

Persist rows only as:

```json
{ "entitlementId": "outfit_conductor", "expiresAtUnixSeconds": 1780000000 }
```

`ReadLocalLeases()` parses totalistically: malformed arrays/rows return only valid rows and never throw. It creates `EntitlementGrant(..., GrantSource.RewardedAd, expiry)`; `PurchaseService` remains the catalogue/clock authority.

- [ ] **Step 6: Run focused tests and prove bytes reload**

```bash
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj -p:RestoreLockedMode=true --filter "FullyQualifiedName~AdPurchaseConvergenceTests|FullyQualifiedName~RewardedAdSaveStoreTests|FullyQualifiedName~EntitlementLedgerTests"
bash scripts/check.sh
```

Expected: tests reload a newly constructed store from committed bytes and pass; no test only inspects the adapter's authored in-memory object.

- [ ] **Step 7: Commit durable grant ordering**

```bash
git add unity/Assets/Scripts/Services/Purchases unity/Assets/Scripts/Application/Save/RewardedAdSaveStore.cs unity/Assets/Scripts/Application/Save/RewardedAdSaveStore.cs.meta unity/Assets/Tests/EditMode/Pure/Purchases unity/Assets/Tests/EditMode/Pure/Save/RewardedAdSaveStoreTests.cs unity/Assets/Tests/EditMode/Pure/Save/RewardedAdSaveStoreTests.cs.meta
git commit -m "feat(monetization): persist ad leases before grant" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

## Task 3: Build the Engine-Free Rewarded Coordinator and Persistent Caps

**Files:**

- Create: `unity/Assets/Scripts/Services/Ads/IRewardedAds.cs`
- Create: `unity/Assets/Scripts/Services/Ads/RewardedAdEvents.cs`
- Create: `unity/Assets/Scripts/Services/Ads/RewardedAdCoordinator.cs`
- Create: `unity/Assets/Scripts/Services/Ads/RewardedAdRuntime.cs`
- Modify: `unity/Assets/Scripts/Application/Save/RewardedAdSaveStore.cs`
- Create: `unity/Assets/Tests/EditMode/Pure/Ads/RewardedAdFixtures.cs`
- Create: `unity/Assets/Tests/EditMode/Pure/Ads/RewardedAdCoordinatorTests.cs`
- Create: `unity/Assets/Tests/EditMode/Pure/Ads/RewardedAdRuntimeTests.cs`
- Create: `unity/Assets/Tests/EditMode/Pure/Save/RewardedAdCapSaveTests.cs`
- Include new folder/file `.meta` assets.

- [ ] **Step 1: Write failing coordinator behavior tests with a programmable fake provider**

Define the neutral seam in tests first:

```csharp
public interface IRewardedAdProvider
{
    event Action<RewardedAdEvent> EventReceived;
    bool IsReady { get; }
    void Initialize();
    void Load();
    bool TryShow(long attemptId, string placementId);
}

public interface IAdEventReporter
{
    event Action ReadinessChanged;
    bool IsReady { get; }
    void Report(RewardedAdEvent adEvent);
}

public interface IRewardedAdCapStore
{
    int ReadLocalDateCount(string placementId, string localDateKey);
    bool TryIncrementLocalDateCount(string placementId, string localDateKey);
}
```

Require:

- disabled/unknown/owned/capped/no-fill/unconfigured/reporter-not-ready placements, plus `PurchaseService.CanPersistRewardedAdGrants == false`, refuse before `TryShow`;
- reporter readiness gates provider initialization, so an untracked loaded ad cannot predate RevenueCat readiness;
- one ready tap produces one attempt ID and one provider show;
- only a matching `Rewarded` callback invokes `GrantRewardedAdEntitlement`;
- `Closed` alone never grants;
- close-before-reward grants once when the late reward arrives;
- duplicate rewards and callbacks from unknown attempt IDs are ignored;
- display failure resolves without a grant and triggers a background reload;
- load failure changes availability without throwing or blocking;
- loaded/displayed/opened/load-failed/revenue events reach the reporter, while display failure remains internal;
- if reporter readiness is lost after show, the earned reward still grants but subsequent offers fail closed;
- local-date and session caps advance only after `AdGrantOutcome.Granted`;
- cap persistence failure does not revoke the already durable lease and still consumes the in-memory session opportunity.

- [ ] **Step 2: Write failing persistent-cap tests against reloaded save bytes**

Use only the v2-owned `caps.rewarded.dateKey` and `caps.rewarded.counters` object. Require a successful increment to survive a fresh `SaveStore`; a new date to reset only the rewarded counter object atomically; the legacy fixed `caps.counters` and unknown sibling fields to remain byte-equivalent; and a refused/throwing write to restore the payload containing the already-committed lease.

The lease commit and later cap commit are deliberately not one transaction: once the player has earned and durably received the item, a cap-write fault must not revoke it. Add an explicit failure artifact proving a fresh reload has the lease but not the failed counter, while the current session still blocks another show. Document this narrow failure-mode bypass instead of claiming anti-farming durability under disk failure/crash between commits.

- [ ] **Step 3: Run the red tests**

```bash
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj -p:RestoreLockedMode=true --filter "FullyQualifiedName~RewardedAdCoordinatorTests|FullyQualifiedName~RewardedAdRuntimeTests|FullyQualifiedName~RewardedAdCapSaveTests"
```

Expected: compile failure because `CatMetro.Services.Ads` does not exist.

- [ ] **Step 4: Implement neutral event and public UI contracts**

`RewardedAdEvent` carries only CLR values: kind, attempt ID, placement ID, ad-unit ID, impression/auction ID, network name, optional integer error code, revenue micros, currency, and `AdRevenuePrecision` (`Exact`, `PublisherDefined`, `Estimated`, `Unknown`). It contains no Unity, LevelPlay, or RevenueCat type.

Expose UI behavior through:

```csharp
public interface IRewardedAds
{
    event Action AvailabilityChanged;
    bool CanShow(string placementId);
    RewardedShowOutcome Show(string placementId);
}
```

Use explicit `Started`, `Unavailable`, and `Busy` outcomes. `RewardedAdRuntime.Current` is always a no-op implementation until `Install`, mirrors `PurchaseRuntime` reset behavior, and never returns null.

- [ ] **Step 5: Implement coordinator attempt ownership and callback ordering**

Keep one open provider show at a time and a bounded set of closed-but-reward-eligible attempts. On close, release the provider slot and reload, but keep that placement pending until reward or display failure so it cannot be offered twice before a late reward. Mark the attempt's reward latch before calling `PurchaseService` so re-entrant duplicate callbacks cannot double-grant.

Report only valid lifecycle kinds to `IAdEventReporter`; a reporter exception changes future availability to fail closed but never bubbles into gameplay and never revokes an earned reward.

- [ ] **Step 6: Extend `RewardedAdSaveStore` for local-date caps**

Use deep-clone/commit/rollback exactly as for leases. `ReadLocalDateCount` returns zero for a different or malformed date. `TryIncrementLocalDateCount` resets only `caps.rewarded.counters` when its own date changes, increments only the requested placement with overflow saturation, commits, and restores the previous payload on failure. It never edits the legacy five-key `caps.counters`. Session counts live only in `RewardedAdCoordinator`.

- [ ] **Step 7: Run focused tests and full linked-source compilation**

```bash
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj -p:RestoreLockedMode=true --filter "FullyQualifiedName~RewardedAdCoordinatorTests|FullyQualifiedName~RewardedAdRuntimeTests|FullyQualifiedName~RewardedAdCapSaveTests"
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj -p:RestoreLockedMode=true
bash scripts/check.sh
```

Expected: coordinator permutations pass and all linked Domain/Services/Application tests remain green.

- [ ] **Step 8: Commit the reusable ad core**

```bash
git add unity/Assets/Scripts/Services/Ads unity/Assets/Scripts/Application/Save/RewardedAdSaveStore.cs unity/Assets/Tests/EditMode/Pure/Ads unity/Assets/Tests/EditMode/Pure/Save/RewardedAdCapSaveTests.cs unity/Assets/Tests/EditMode/Pure/Save/RewardedAdCapSaveTests.cs.meta
git commit -m "feat(ads): add reusable rewarded coordinator" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

## Task 4: Compose Ads Around the Existing Purchase and Save Runtimes

**Files:**

- Create: `unity/Assets/Scripts/Integrations/RewardedAdProviderFactory.cs`
- Create: `unity/Assets/Scripts/Integrations/RewardedAdsConfig.cs`
- Create: `unity/Assets/Scripts/Integrations/RewardedAdsComposition.cs`
- Create: `unity/Assets/Scripts/Integrations/AssemblyInfo.cs`
- Modify: `unity/Assets/Scripts/Integrations/MonetizationBootstrap.cs`
- Modify: `unity/Assets/Scripts/Integrations/CatMetro.Integrations.asmdef`
- Modify: `unity/Assets/Tests/EditMode/CatMetro.Tests.EditMode.asmdef`
- Create: `unity/Assets/Tests/EditMode/Engine/RewardedAdsBootstrapTests.cs`
- Create: `config/rewarded-ads.example.json`
- Modify: `.gitignore`
- Include matching `.meta` files for new Unity assets.

- [ ] **Step 1: Write failing composition and configuration tests**

Make the EditMode test assembly reference `CatMetro.Integrations`. Require:

- missing resource, blank current-platform app key, or blank ad-unit ID returns a not-configured result without throwing;
- the committed example parses but remains deliberately unconfigured;
- `RewardedAdProviderFactory.Create` catches factory exceptions and returns null;
- `PurchaseBackendFactory.ResetForTests` and `RewardedAdProviderFactory.ResetForTests` clear their static factories between tests;
- `RewardedAdsComposition.Bind` subscribes to `SaveRuntime.Installed` and immediately consumes `SaveRuntime.Current` when a store was installed before binding;
- a new store reads/imports leases, attaches one `RewardedAdSaveStore` to `PurchaseRuntime.Current`, constructs one coordinator, and installs `RewardedAdRuntime`;
- installing/binding the same save reference twice is idempotent and does not restore, initialize, or subscribe twice;
- `OnApplicationPause(true)` uses that same `SaveStore.TryCommitOnPause` path, while resume still refreshes RevenueCat entitlements;
- destroy/reset removes static runtime subscriptions so repeated PlayMode boots do not duplicate callbacks.

- [ ] **Step 2: Run the red EditMode filter**

```bash
mkdir -p artifacts
UNITY_EDITOR=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY_EDITOR" -batchmode -projectPath "$PWD/unity" -runTests -testPlatform EditMode -testFilter CatMetro.Tests.RewardedAdsBootstrapTests -testResults "$PWD/artifacts/rewarded-bootstrap-editmode.xml" -logFile "$PWD/artifacts/rewarded-bootstrap-editmode.log"
```

Expected: failing tests/compilation for the missing provider factory/config/bootstrap behavior. Do not add `-quit`.

- [ ] **Step 3: Add a non-secret config contract and provider factory**

The ignored runtime resource is:

`unity/Assets/Resources/Monetization/rewarded_ads_config.json`

Ignore that file and its `.meta`, and ignore the root `artifacts/` test-output directory. Commit only `config/rewarded-ads.example.json`:

```json
{
  "iosAppKey": "",
  "androidAppKey": "",
  "iosRewardedAdUnitId": "",
  "androidRewardedAdUnitId": ""
}
```

`RewardedAdsConfig.Parse(string json, RuntimePlatform platform)` is the deterministic test seam; `Load()` only reads the Resource and delegates to it. Parsing selects only the current platform pair, reports a precise `Problem`, and never logs key contents. It must not read process environment or request ATT.

The base factory API is concrete:

```csharp
public static void Register(Func<RewardedAdsConfig, IRewardedAdProvider> factory);
internal static IRewardedAdProvider Create(RewardedAdsConfig config);
```

Registration replaces only the optional provider constructor; `Create` returns null for unconfigured input or a thrown constructor and logs no key value.

- [ ] **Step 4: Extend existing bootstrap ownership instead of adding a second manager**

Add `CatMetro.Application` to the Integrations asmdef. Add `InternalsVisibleTo` for `CatMetro.Tests.EditMode` and `CatMetro.Tests.PlayMode` so tests can inject dependencies into the internal composition object without widening the shipped API. Add internal `ResetForTests` methods to both static factories and call them from test teardown. Keep `MonetizationBootstrap` as the only boot entry and extend its existing `[Monetization]` host/pump. After `PurchaseRuntime` and the backend exist, give `RewardedAdsComposition` the purchase service, placement catalogue, optional provider, and `backend as IAdEventReporter`; `MonetizationPump` delegates Unity pause/destroy lifecycle to it.

`RewardedAdsComposition.Bind` subscribes first, then calls the same guarded handler with `SaveRuntime.Current` if non-null. The handler ignores the store reference already bound, disposes/replaces composition only for a genuinely new store, and performs:

```csharp
var saveData = new RewardedAdSaveStore(store);
service.RestoreRewardedAdLeases(saveData.ReadLocalLeases());
service.AttachLeasePersistence(saveData);
var coordinator = new RewardedAdCoordinator(
    placements, service, provider, reporter, saveData, LocalDateKey);
RewardedAdRuntime.Install(coordinator);
coordinator.Start();
```

Use `DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)` for the authored `localDate` cap, injected in tests. If provider, reporter, config, or save is absent, leave the no-op runtime installed and continue.

- [ ] **Step 5: Run focused EditMode plus linked-source tests**

```bash
UNITY_EDITOR=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY_EDITOR" -batchmode -projectPath "$PWD/unity" -runTests -testPlatform EditMode -testFilter CatMetro.Tests.RewardedAdsBootstrapTests -testResults "$PWD/artifacts/rewarded-bootstrap-editmode.xml" -logFile "$PWD/artifacts/rewarded-bootstrap-editmode.log"
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj -p:RestoreLockedMode=true
bash scripts/check.sh
```

Expected: bootstrap tests pass, test XML exists, and missing real config leaves the test game usable.

- [ ] **Step 6: Commit runtime composition and the human config template**

```bash
git add .gitignore config/rewarded-ads.example.json unity/Assets/Scripts/Integrations unity/Assets/Tests/EditMode/CatMetro.Tests.EditMode.asmdef unity/Assets/Tests/EditMode/Engine/RewardedAdsBootstrapTests.cs unity/Assets/Tests/EditMode/Engine/RewardedAdsBootstrapTests.cs.meta
git commit -m "feat(ads): compose rewarded runtime safely" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

## Task 5: Reuse RevenueCat's Existing `Purchases` Instance as the Ads Reporter

**Files:**

- Modify: `unity/Assets/Scripts/Integrations/RevenueCat/RevenueCatBackend.cs`
- Modify: `unity/Assets/Tests/EditMode/Engine/RewardedAdsBootstrapTests.cs`
- Inspect package source: `unity/Library/PackageCache/com.revenuecat.purchases-unity@*/Scripts/AdTracker.cs`
- Inspect package source: `unity/Library/PackageCache/com.revenuecat.purchases-unity@*/Scripts/Ad*Data.cs`

- [ ] **Step 1: Add failing reporter-readiness and event-routing tests at the neutral seam**

Extend the bootstrap tests with a backend fake that also implements `IAdEventReporter`. Require the exact backend object returned by `PurchaseBackendFactory` to be passed to the coordinator; a second reporter/SDK object must never be constructed. Require provider initialization to wait until reporter readiness changes to true.

Add pure event-validation cases requiring displayed/opened/loaded/revenue to have nonblank ad-unit and impression IDs, load failure to require only an ad-unit ID, and malformed metadata to be dropped without affecting reward delivery.

- [ ] **Step 2: Reconfirm the pinned 9.9.0 API from the integrated manifest and resolved source before editing**

```bash
grep -n -E 'com\.revenuecat\.purchases-unity.*9\.9\.0' unity/Packages/manifest.json
grep -R -n -E "TrackAdDisplayed|TrackAdOpened|TrackAdRevenue|TrackAdLoaded|TrackAdFailedToLoad|class MediatorName|class Precision" unity/Library/PackageCache/com.revenuecat.purchases-unity@*/Scripts/AdTracker.cs unity/Library/PackageCache/com.revenuecat.purchases-unity@*/Scripts/Ad*Data.cs
```

Expected: `Purchases.AdTracker` exposes the five methods above; `AdTracker.MediatorName` accepts a custom string; rewarded format and four precision values match the design.

- [ ] **Step 3: Implement `IAdEventReporter` on `RevenueCatBehaviour`**

Use the existing private `_purchases`; never add another `Purchases` component. Add a private `_adTrackingReady` flag: `IsReady` is true only when `_purchases != null && _adTrackingReady`. Set it after `_purchases.Configure(...)` succeeds; clear it on fatal configuration/reporting failure and destroy. Raise `ReadinessChanged` only when that flag changes. Do not couple ad reporting readiness to transient product/CustomerInfo `Availability` changes.

Map neutral events exactly:

| Neutral kind | RevenueCat 9.9 call |
|---|---|
| `Loaded` | `TrackAdLoaded(new AdLoadedData(...))` |
| `Displayed` | `TrackAdDisplayed(new AdDisplayedData(...))` |
| `Opened` | `TrackAdOpened(new AdOpenedData(...))` |
| `LoadFailed` | `TrackAdFailedToLoad(new AdFailedToLoadData(...))` |
| `RevenuePaid` | `TrackAdRevenue(new AdRevenueData(...))` |
| `DisplayFailed`, `Rewarded`, `Closed` | internal only; no RevenueCat call |

Use `new AdTracker.MediatorName("LevelPlay")`, `AdTracker.Format.Rewarded`, USD micros supplied by the provider, and precision mapping `BID -> Exact`, `RATE -> PublisherDefined`, `CPM -> Estimated`, anything else -> `Unknown`. Catch/log SDK exceptions and change future readiness to false; never throw into the coordinator.

- [ ] **Step 4: Compile and run the focused EditMode tests**

```bash
UNITY_EDITOR=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY_EDITOR" -batchmode -projectPath "$PWD/unity" -runTests -testPlatform EditMode -testFilter CatMetro.Tests.RewardedAdsBootstrapTests -testResults "$PWD/artifacts/revenuecat-ads-editmode.xml" -logFile "$PWD/artifacts/revenuecat-ads-editmode.log"
grep -E "error CS|Exception|FAILED" "$PWD/artifacts/revenuecat-ads-editmode.log" | head -80 || true
```

Expected: tests pass and the grep produces no new compile/exception/failure line. An empty grep result is acceptable only after the test XML reports pass.

- [ ] **Step 5: Commit the RevenueCat Ads bridge**

```bash
git add unity/Assets/Scripts/Integrations/RevenueCat/RevenueCatBackend.cs unity/Assets/Tests/EditMode/Engine/RewardedAdsBootstrapTests.cs
git commit -m "feat(ads): report ad lifecycle to RevenueCat" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

## Task 6: Add the Optional LevelPlay 9.5.1 Serving Adapter

**Files:**

- Create: `unity/Assets/Scripts/Integrations/LevelPlay/CatMetro.Integrations.LevelPlay.asmdef`
- Create: `unity/Assets/Scripts/Integrations/LevelPlay/LevelPlayRewardedAdProvider.cs`
- Create: `unity/Assets/Scripts/Integrations/LevelPlayPayloadMapper.cs` — neutral base assembly, no vendor types
- Create: `unity/Assets/Scripts/Integrations/MainThreadAdEventQueue.cs` — neutral thread-handoff queue
- Modify: `unity/Packages/manifest.json`
- Modify: `unity/Packages/packages-lock.json`
- Create: `unity/Assets/Tests/EditMode/Engine/LevelPlayPayloadMapperTests.cs`
- Include matching `.meta` files.

- [ ] **Step 1: Write failing pure mapping tests before vendor code**

Require:

- `BID`, `RATE`, and `CPM` map to the approved precision values and unknown/null maps to `Unknown`;
- `0.001234 USD` becomes exactly `1234` micros using checked decimal/double bounds, while NaN, infinity, negative, and overflow values are rejected;
- an impression with a blank auction/impression ID is not fabricated from placement, network, or ad-unit data;
- copied callback payloads contain only immutable neutral CLR values.
- a worker thread can enqueue an ILR event, no consumer runs on that worker, and one explicit main-thread `Drain()` delivers it exactly once.

- [ ] **Step 2: Create the optional assembly while the package is absent and prove Editor safety**

Keep `LevelPlayPayloadMapper` in the always-compiled base Integrations assembly so its raw-string/double mapping tests work with the package absent. The optional asmdef references `CatMetro.Services`, `CatMetro.Integrations`, and `Unity.Services.LevelPlay`; its `versionDefines` entry is `{ "name": "com.unity.services.levelplay", "expression": "9.5.1", "define": "CATMETRO_LEVELPLAY" }`, and `defineConstraints` contains `CATMETRO_LEVELPLAY`. Wrap only the concrete provider source with `#if CATMETRO_LEVELPLAY` as a second visible guard.

Implement `MapPrecision(string)`, `TryUsdMicros(double, out long)`, a neutral payload constructor, and a lock/queue-based `MainThreadAdEventQueue` in the base assembly now. `TryUsdMicros` accepts `double`, rejects NaN/infinity/negative/overflow, and computes `checked((long)Math.Round(value * 1_000_000d, MidpointRounding.AwayFromZero))`; the tests pin boundary and half-micro behavior. It trims/case-normalizes only the documented precision label and returns false rather than inventing an impression ID. `Drain()` copies/dequeues under the lock and invokes consumers after releasing it.

Before adding the package dependency, run:

```bash
UNITY_EDITOR=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY_EDITOR" -batchmode -projectPath "$PWD/unity" -runTests -testPlatform EditMode -testFilter CatMetro.Tests.LevelPlayPayloadMapperTests -testResults "$PWD/artifacts/levelplay-absent-editmode.xml" -logFile "$PWD/artifacts/levelplay-absent-editmode.log"
```

Expected: the project compiles with the optional assembly skipped; base mapping tests pass. This is the artifact for package-absent Editor compilation.

- [ ] **Step 3: Pin and resolve LevelPlay 9.5.1**

Add only the dependency below; it resolves from Unity's default package registry, not OpenUPM:

```json
"com.unity.services.levelplay": "9.5.1"
```

to dependencies. Let Unity resolve the package and update `packages-lock.json`; do not hand-invent transitive hashes.

- [ ] **Step 4: Inspect the resolved 9.5.1 source for exact callback/property names**

```bash
grep -R -n -E "class LevelPlayRewardedAd|OnAdImpressionDataReady|OnAdRewarded|OnAdClosed|AuctionId|MediationAdUnitId|IsPlacementCapped" unity/Library/PackageCache/com.unity.services.levelplay@* | head -240
```

Expected: the new rewarded Ad Unit API and per-ad impression event exist. Use the package source's exact auction/impression property; do not synthesize an ID if it is absent.

- [ ] **Step 5: Implement serving and callback translation**

Registration runs `AfterAssembliesLoaded`, registers only on iOS/Android players, and returns without installing a live provider in the Editor. The provider:

1. applies only privacy signals already supplied by the app; it never displays ATT/CMP UI;
2. subscribes `LevelPlay.OnInitSuccess/OnInitFailed` before `LevelPlay.Init`;
3. creates one `LevelPlayRewardedAd` only after init succeeds;
4. subscribes loaded/load-failed/displayed/display-failed/rewarded/closed/clicked/impression callbacks before `LoadAd`;
5. reports readiness from `IsAdReady()` and additionally refuses dashboard-capped placements;
6. calls `ShowAd(placementId)` only from `TryShow`, emits close/display-failure events, and leaves every subsequent `Load()` decision to the coordinator;
7. tags every show callback with the coordinator's attempt ID;
8. copies impression data inside `OnAdImpressionDataReady`, queues the neutral event, and drains it on Unity's main thread before any RevenueCat call;
9. unsubscribes every SDK event in `OnDestroy` and tolerates repeated teardown.

Correlate callbacks to shows by the package-confirmed auction/impression ID whenever it is nonblank. Keep a bounded show-context map across `Closed`, because `Rewarded` may arrive later, and resolve it only on reward or display failure. Before an auction ID is known, permit only one unbound show context. Never attach a late callback to “the newest attempt”; ambiguous/malformed analytics is dropped, while close still never grants.

Loaded/load-failed/displayed/display-failed/rewarded/closed/clicked callbacks remain on their documented Unity main-thread path. Only `OnAdImpressionDataReady` enters `MainThreadAdEventQueue`; the thread-handoff EditMode test is required evidence, while actual vendor-thread delivery remains a configured-device check.

Do not include any API capable of creating another ad format.

- [ ] **Step 6: Compile the package-present adapter and run its mapping tests**

```bash
UNITY_EDITOR=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY_EDITOR" -batchmode -projectPath "$PWD/unity" -runTests -testPlatform EditMode -testFilter CatMetro.Tests.LevelPlayPayloadMapperTests -testResults "$PWD/artifacts/levelplay-present-editmode.xml" -logFile "$PWD/artifacts/levelplay-present-editmode.log"
grep -E "error CS|Exception|FAILED" "$PWD/artifacts/levelplay-present-editmode.log" | head -80 || true
```

Expected: package-present compilation succeeds, tests pass, and no vendor API name was assumed without compiler confirmation.

- [ ] **Step 7: Commit the guarded network integration**

```bash
git add unity/Packages/manifest.json unity/Packages/packages-lock.json unity/Assets/Scripts/Integrations/LevelPlay unity/Assets/Scripts/Integrations/LevelPlayPayloadMapper.cs unity/Assets/Scripts/Integrations/LevelPlayPayloadMapper.cs.meta unity/Assets/Scripts/Integrations/MainThreadAdEventQueue.cs unity/Assets/Scripts/Integrations/MainThreadAdEventQueue.cs.meta unity/Assets/Tests/EditMode/Engine/LevelPlayPayloadMapperTests.cs unity/Assets/Tests/EditMode/Engine/LevelPlayPayloadMapperTests.cs.meta
git commit -m "feat(ads): integrate guarded LevelPlay rewarded video" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

## Task 7: Enable Four Real Placements and Add the Wardrobe Need Surface

**Files:**

- Modify: `unity/Assets/Resources/Monetization/rewarded_placements.json`
- Modify: `unity/Assets/Resources/Strings/ui.csv`
- Modify: `unity/Assets/Scripts/Presentation/Screens/WardrobeLayout.cs`
- Modify: `unity/Assets/Scripts/Presentation/Screens/WardrobeScreenView.cs`
- Modify: `unity/Assets/Tests/EditMode/Pure/Purchases/ShippedCatalogTests.cs`
- Modify: `unity/Assets/Tests/EditMode/Presentation/UiCsvWardrobeTests.cs`
- Create: `unity/Assets/Tests/PlayMode/Screens/WardrobeRewardedPlacementTests.cs`
- Modify: `unity/Assets/Tests/PlayMode/Screens/WardrobePurchaseFlowTests.cs`
- Include the new test `.meta`.

- [ ] **Step 0: Reopen the visual contract before changing the Wardrobe**

Read `docs/LOOK.md` completely and open `docs/reference/actual-01-home.jpeg`, `docs/reference/target-01-tabletop.png`, and `docs/reference/target-02-diorama.png` with the image-viewing tool. Record the cream paper, depot navy, ticket orange, teal, chunky rounded geometry, and phone-scale legibility cues the four cards must reuse. Do this again even if the files were viewed during design; implementation starts from the visual artifact, not memory.

- [ ] **Step 1: Write failing shipped-data tests for exactly four enabled wardrobe rows**

Require these exact mappings and no enabled non-wardrobe placement:

```text
wardrobe_try_conductor -> outfit_conductor
wardrobe_try_engineer  -> outfit_engineer
wardrobe_try_scarf     -> accessory_scarf
wardrobe_try_goggles   -> accessory_goggles
```

Require one `localDate: 1` cap on all four and the additional `session: 1` cap only on goggles. Require user-facing string keys for the four names, borrowed/locked states, “Watch to borrow today”, unavailable state, and successful borrowing.

- [ ] **Step 2: Write failing PlayMode tests against rendered/registered UI behavior**

Inject a fake `IRewardedAds` into an overload of `WardrobeScreenView.Create`. For each of the four pairs, require:

- locked preview geometry is present but its borrowed accent is off;
- when `CanShow` is true, the corresponding painted card is a registered minimum-size target;
- tapping calls `Show` with that exact placement once;
- an ad grant and a paid authoritative grant both illuminate the same preview state through `PurchaseService.IsUnlocked`;
- expiry hides only that item, and multiple simultaneous grants illuminate multiple cards;
- unavailable/no-fill hides or disables the ad action while Buy, Restore, Back, and gameplay navigation remain registered;
- repeated `Open`, `Hide`, disable, and destroy do not duplicate ledger, ads-availability, or Chrome-region subscriptions.

Update existing rewarded-grant PlayMode fixtures to attach an accepting lease persistence fake.

- [ ] **Step 3: Run the tests and observe failure before UI changes**

```bash
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj -p:RestoreLockedMode=true --filter "FullyQualifiedName~ShippedCatalogTests"
UNITY_EDITOR=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY_EDITOR" -batchmode -projectPath "$PWD/unity" -runTests -testPlatform PlayMode -testFilter CatMetro.Tests.PlayMode.WardrobeRewardedPlacementTests -testResults "$PWD/artifacts/wardrobe-rewarded-playmode.xml" -logFile "$PWD/artifacts/wardrobe-rewarded-playmode.log"
```

Expected: data test fails because rows are disabled; PlayMode test fails because the four preview targets do not exist.

- [ ] **Step 4: Enable only the four approved placements**

Set their existing `enabled` fields to true and remove the obsolete “no ad network wired” reasons. Do not add retry/rewind, daily attempt, cosmetic unlock, or level transition rows. Failure rewind remains the next slice after the mechanic ladder.

- [ ] **Step 5: Add a fixed four-card preview strip without taking TASK 13 ownership**

Add `PreviewStripRect` between the status area and the existing portrait and reduce `PortraitRect` from the rendered available space. Divide the strip into four equal safe-area-aware card rects with at least the project's minimum touch target at reference phone DPIs.

Build four small code-native silhouettes in the existing visual idiom:

- conductor: navy coat/hat mark;
- engineer: teal/orange bib mark;
- scarf: orange crossed scarf mark;
- goggles: navy double-lens mark.

Each card is permanently associated with one entitlement/placement pair. Its accent derives only from `_service.IsUnlocked(entitlementId)`, and its action derives only from `_rewardedAds.CanShow(placementId)`. Do not add equipped selection, avatar model binding, compatibility rules, or persisted presentation state.

- [ ] **Step 6: Wire lifecycle-safe availability and taps**

Subscribe to `Ledger.Changed` and `IRewardedAds.AvailabilityChanged` only while the panel is active; remove both subscriptions and all four Chrome regions on hide/disable/destroy. A tap calls only `_rewardedAds.Show(placementId)`. The view never calls `GrantRewardedAdEntitlement` directly and never treats close as success.

- [ ] **Step 7: Run data, layout, and PlayMode tests**

```bash
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj -p:RestoreLockedMode=true --filter "FullyQualifiedName~ShippedCatalogTests|FullyQualifiedName~AdPurchaseConvergenceTests"
UNITY_EDITOR=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY_EDITOR" -batchmode -projectPath "$PWD/unity" -runTests -testPlatform EditMode -testFilter CatMetro.Tests.UiCsvWardrobeTests -testResults "$PWD/artifacts/wardrobe-strings-editmode.xml" -logFile "$PWD/artifacts/wardrobe-strings-editmode.log"
"$UNITY_EDITOR" -batchmode -projectPath "$PWD/unity" -runTests -testPlatform PlayMode -testFilter CatMetro.Tests.PlayMode.WardrobeRewardedPlacementTests -testResults "$PWD/artifacts/wardrobe-rewarded-playmode.xml" -logFile "$PWD/artifacts/wardrobe-rewarded-playmode.log"
```

Expected: exact data mappings, strings, touch regions, convergence, expiry, and subscription cleanup pass.

- [ ] **Step 8: Commit the four need-based placements**

```bash
git add unity/Assets/Resources/Monetization/rewarded_placements.json unity/Assets/Resources/Strings/ui.csv unity/Assets/Scripts/Presentation/Screens/WardrobeLayout.cs unity/Assets/Scripts/Presentation/Screens/WardrobeScreenView.cs unity/Assets/Tests/EditMode/Pure/Purchases/ShippedCatalogTests.cs unity/Assets/Tests/EditMode/Presentation/UiCsvWardrobeTests.cs unity/Assets/Tests/PlayMode/Screens/WardrobePurchaseFlowTests.cs unity/Assets/Tests/PlayMode/Screens/WardrobeRewardedPlacementTests.cs unity/Assets/Tests/PlayMode/Screens/WardrobeRewardedPlacementTests.cs.meta
git commit -m "feat(wardrobe): add four rewarded try-on placements" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

## Task 8: Prove Restart, No-Fill Degradation, and the Rendered Wardrobe Artifact

**Files:**

- Create: `unity/Assets/Tests/PlayMode/Bootstrap/RewardedAdsWiringTests.cs`
- Modify: `unity/Assets/Tests/PlayMode/Screens/WardrobeRewardedPlacementTests.cs`
- Modify: `unity/Assets/Tests/PlayMode/CatMetro.Tests.PlayMode.asmdef` — add `CatMetro.Integrations`
- Create runtime artifacts only under ignored `artifacts/rewarded-ads/`.
- Include the new test `.meta`.

- [ ] **Step 1: Write failing end-to-end PlayMode tests across real composition seams**

Use fake provider/reporter plus a real temp-backed `SaveStore`. Require:

1. `SaveRuntime.Install` synchronously wires the production purchase service to the save adapter;
2. ready offer -> provider reward -> `PurchaseService` precommit -> ledger change -> Wardrobe preview visible;
3. destroying the runtime/service, loading a new `SaveStore` from bytes, reinstalling, and recreating Wardrobe leaves the same preview visible with the original expiry;
4. a provider that never becomes ready/no-fills leaves the board/home/purchase route usable and has no ad Chrome targets;
5. four placements reuse one provider instance and one rewarded ad unit;
6. close-before-reward and duplicate reward behavior is visible at the composed boundary, not only the coordinator unit test.

- [ ] **Step 2: Add an armed RenderTexture capture test**

Use `CM_REWARDED_CAPTURE_DIR` only as an output switch, never for keys. Bind the `RenderTexture`, yield one frame, then lay out the screen-space UI. Capture at minimum:

- `wardrobe-rewarded-locked.png` — all four named need cards and available actions;
- `wardrobe-rewarded-granted.png` — one borrowed item visibly changed while purchase remains available;
- `wardrobe-rewarded-no-fill.png` — no ad action/ghost target, normal shop still usable.

Assert each file is nontrivial in size and assert the rendered UI objects are inside the supplied safe area. Do not substitute authored-coordinate calculations for this PlayMode render.

- [ ] **Step 3: Run the failing test before final wiring fixes**

```bash
UNITY_EDITOR=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY_EDITOR" -batchmode -projectPath "$PWD/unity" -runTests -testPlatform PlayMode -testFilter CatMetro.Tests.PlayMode.RewardedAdsWiringTests -testResults "$PWD/artifacts/rewarded-wiring-playmode.xml" -logFile "$PWD/artifacts/rewarded-wiring-playmode.log"
```

Expected: red until all runtime cleanup/restart seams are correctly composed.

- [ ] **Step 4: Make the smallest wiring/lifecycle fixes justified by the failed artifact test**

Keep fixes within `MonetizationPump`, `RewardedAdCoordinator`, runtime reset methods, or Wardrobe subscription cleanup. Do not broaden `GameRoot.cs` beyond the published delta.

- [ ] **Step 5: Run the composed tests and emit captures**

```bash
mkdir -p artifacts/rewarded-ads
UNITY_EDITOR=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY_EDITOR" -batchmode -projectPath "$PWD/unity" -runTests -testPlatform PlayMode -testFilter CatMetro.Tests.PlayMode.RewardedAdsWiringTests -testResults "$PWD/artifacts/rewarded-wiring-playmode.xml" -logFile "$PWD/artifacts/rewarded-wiring-playmode.log"
CM_REWARDED_CAPTURE_DIR="$PWD/artifacts/rewarded-ads" "$UNITY_EDITOR" -batchmode -projectPath "$PWD/unity" -runTests -testPlatform PlayMode -testFilter CatMetro.Tests.PlayMode.WardrobeRewardedPlacementTests -testResults "$PWD/artifacts/rewarded-wardrobe-playmode.xml" -logFile "$PWD/artifacts/rewarded-wardrobe-playmode.log"
find artifacts/rewarded-ads -maxdepth 1 -type f -name '*.png' -print -exec file {} \;
```

Expected: PlayMode XML passes and three phone PNGs exist. Open all three images and visually verify readable names, distinct silhouettes, no overlap/clipping, a visible granted-state difference, and a usable no-fill purchase route. Record anything not visually verified.

- [ ] **Step 6: Commit end-to-end proof tests, not generated captures**

```bash
git add unity/Assets/Tests/PlayMode/CatMetro.Tests.PlayMode.asmdef unity/Assets/Tests/PlayMode/Bootstrap/RewardedAdsWiringTests.cs unity/Assets/Tests/PlayMode/Bootstrap/RewardedAdsWiringTests.cs.meta unity/Assets/Tests/PlayMode/Screens/WardrobeRewardedPlacementTests.cs
git commit -m "test(ads): prove restart and no-fill behavior" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

## Task 9: Add the Human Configuration and Configured-Device Proof Runbook

**Files:**

- Create: `docs/runbooks/rewarded-ads-device-proof.md`
- Create: `docs/runbooks/shipaton-submission-checklist.md`
- Modify: `docs/superpowers/specs/2026-08-27-rewarded-ads-design.md` only if implementation changed a named API, never to weaken acceptance criteria.

- [ ] **Step 1: Document every human-supplied value without including a value**

The runbook lists:

- LevelPlay iOS App Key and Android App Key;
- one rewarded Ad Unit ID per platform;
- the four exact LevelPlay placement names;
- selected network dashboard credentials and matching LevelPlay Network Manager adapters;
- RevenueCat Apple/Google public SDK setup already landed by TASK 19, the Task 5 `AdTracker` bridge, and RevenueCat Ads beta access;
- privacy/CMP/COPPA/ATT decisions and store disclosure updates;
- iOS CocoaPods/resolver, privacy manifest, SKAdNetwork, Xcode 26+, and minimum-iOS checks;
- Android AD_ID, resolver, and selected-network manifest checks for the later Android release.

State plainly that no value belongs in Git and that the ignored runtime config is created by the human. State separately that LevelPlay serves the video and issues its reward callback, while RevenueCat `AdTracker` records monetization events; the LevelPlay reward is not RevenueCat's separate server-verified reward product.

- [ ] **Step 2: Document the exact configured-device evidence sequence**

Each evidence bundle records app version and Git SHA, device model/OS, Unity/LevelPlay/RevenueCat SDK versions, timestamp/time zone, masked app/ad-unit identifiers, exact placement, device-log path, and RevenueCat dashboard screenshot path. The human/device checklist must prove, in order:

1. LevelPlay integration helper/test suite passes for each selected adapter;
2. LevelPlay test inventory produces load/display/open-when-clicked/reward/close callbacks, and a forced no-fill leaves gameplay/shop usable;
3. one reward unlocks the exact selected item, and killing/relaunching preserves its original expiry;
4. a duplicate/second callback does not grant again;
5. device logs show the LevelPlay ILR input, currency, raw precision label, and mapped micros without exposing credentials;
6. RevenueCat Ads sandbox shows the corresponding received lifecycle/revenue event and the dimensions its current UI actually exposes (mediator, format, placement, ad unit, network, impression, and revenue when present);
7. if valid live inventory is available and appropriate, record a nonzero ILR example separately; test inventory may report zero revenue, so nonzero live revenue is not an engineering completion blocker;
8. the Shipaton capture contains the locked need, ad completion, and visibly borrowed item within the two-minute submission video;
9. release debug/test-suite switches are off.

Do not claim the RevenueCat dashboard proves the raw micros integer or precision enum unless that configured dashboard visibly exposes those exact fields.

For the later Android check, run `adb devices -l` first and inspect `model:`. Target only Pixel 9 Pro serial `48121FDAP006X4`; never install to the Quest or Pico devices listed in `AGENTS.md`.

Organizer written confirmation is explicitly a human follow-up and not an engineering blocker.

- [ ] **Step 3: Add a strict “device only” evidence boundary**

The runbook must say that unit/EditMode/PlayMode tests cannot prove live fill, vendor callback delivery, native dependency resolution, RevenueCat ingestion/dashboard display, actual revenue/precision, OS privacy UI behavior, or process persistence in the platform container. Those claims remain unverified until the configured device checklist passes.

- [ ] **Step 4: Add the nonblocking human Shipaton submission checklist**

The checklist reuses the existing submission assets/plans and covers: Devpost registration/category selection; an honest description that LevelPlay serves and RevenueCat `AdTracker` tracks the ads, with organizer confirmation pending; a fully public first release on an accepted store during the eligibility window (iOS first per project sequencing, and “in review” is not live) before 2026-09-30 11:45pm PDT; public YouTube/Vimeo video no longer than two minutes; 1024×1024 icon; one 1179×2556 screenshot without a device frame; judges-only trial/promo access; and category-specific Catvertising placement/revenue-stack copy. Store release, promo generation, organizer contact, Devpost submission, and uploads remain human-only.

- [ ] **Step 5: Commit the runbooks**

```bash
git add docs/runbooks/rewarded-ads-device-proof.md docs/runbooks/shipaton-submission-checklist.md docs/superpowers/specs/2026-08-27-rewarded-ads-design.md
git commit -m "docs(ads): add configured-device proof runbook" -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

## Task 10: Full Verification, Diff Audit, Review, and Push

**Files:**

- Review every file changed since the TASK 19 merge base.
- Do not create a release/store artifact in this task.

- [ ] **Step 1: Run static checks and the complete linked-source suite**

```bash
bash scripts/check.sh
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj -p:RestoreLockedMode=true
```

Expected: all checks and all linked-source tests pass. Report the exact count and duration.

- [ ] **Step 2: Run the complete Unity EditMode suite**

```bash
UNITY_EDITOR=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY_EDITOR" -batchmode -projectPath "$PWD/unity" -runTests -testPlatform EditMode -testResults "$PWD/artifacts/rewarded-full-editmode.xml" -logFile "$PWD/artifacts/rewarded-full-editmode.log"
```

Expected: roughly 1,000 tests pass, exact count taken from XML. Allow the known silent ~21-minute interval; do not terminate it as stalled and do not add `-quit`.

- [ ] **Step 3: Run the complete Unity PlayMode suite**

```bash
UNITY_EDITOR=/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity
"$UNITY_EDITOR" -batchmode -projectPath "$PWD/unity" -runTests -testPlatform PlayMode -testResults "$PWD/artifacts/rewarded-full-playmode.xml" -logFile "$PWD/artifacts/rewarded-full-playmode.log"
```

Expected: roughly 250 tests pass, exact count taken from XML, with no newly introduced Console error.

- [ ] **Step 4: Audit the actual diff and configuration surface**

```bash
git status --short
git diff --check origin/main...HEAD
git diff --stat origin/main...HEAD
git diff origin/main...HEAD -- unity/Assets/Scripts/Bootstrap/GameRoot.cs
git grep -n -E "Interstitial|RewardedInterstitial|BannerAd|AppOpen|RequestTrackingAuthorization" -- unity/Assets/Scripts/Services/Ads unity/Assets/Scripts/Integrations unity/Assets/Scripts/Presentation/Screens/WardrobeScreenView.cs || true
git grep -n -E "iosAppKey|androidAppKey|iosRewardedAdUnitId|androidRewardedAdUnitId" -- config/rewarded-ads.example.json
if git ls-files --error-unmatch unity/Assets/Resources/Monetization/rewarded_ads_config.json >/dev/null 2>&1; then echo "real rewarded ad config is tracked"; exit 1; fi
```

Expected: `GameRoot` still has only the declared delta; forbidden ad formats/ATT calls are absent; only blank example key fields are tracked. Inspect every non-empty grep hit rather than treating grep output as a verdict.

- [ ] **Step 5: Request code review before declaring completion**

Use `superpowers:requesting-code-review` against the complete branch diff. Review specifically for grant ordering, migration collision with Daily/TASK 13, attempt callback ordering, optional assembly correctness, event unsubscription, no-fill behavior, and whether tests exercise reloaded bytes/rendered output rather than authored state.

- [ ] **Step 6: Re-run affected verification after any review fix**

Run the smallest focused test first, then repeat `bash scripts/check.sh`, the full .NET suite, and whichever full Unity suite the fix could affect. Do not reuse pre-fix evidence.

- [ ] **Step 7: Push normally and hand off without uploading**

```bash
git status --short
git push -u origin feat/rewarded-ads
```

Expected: clean worktree and a normal non-force push. On TLS/x509 failure, retry the same plain push outside the sandbox. Do not build or upload a store binary.

## Completion Report Requirements

The final implementation report must separate:

- code/tests/rendered artifacts actually verified locally;
- human-supplied keys/adapters/disclosures still required;
- configured-device checks completed versus outstanding;
- exact tests/counts/durations and any pre-existing failures;
- the exact `GameRoot.cs` delta delivered to TASK 19;
- the save-v2 migration contract delivered to TASK 13;
- the failure-rewind placement explicitly deferred until after the mechanic ladder.

It must end with a plainly labeled list of what only a configured device can prove: live LevelPlay initialization/fill/show/reward/ILR callbacks, selected native adapter resolution, RevenueCat Ads ingestion/dashboard fields, real platform-container restart persistence, and iOS/Android privacy/resume behavior. Until those artifacts exist, do not claim them.
