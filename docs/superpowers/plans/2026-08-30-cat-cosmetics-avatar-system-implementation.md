# Cat Cosmetics and Avatar System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a persistent profile-cat and named-cosmetics system whose reusable UGUI portrait is visible in Wardrobe and Home, whose paid access comes only from the existing RevenueCat-backed `PurchaseService`, and whose typed mount is ready for the HUD owner.

**Architecture:** Engine-free Services owns catalogue admission, profile/loadout state, access resolution, runtime projection, and portrait snapshots. Application owns the additive `profile.cosmetics` v3 save migration and the adapter that commits through the already-open `SaveStore` before any observable mutation. Presentation renders one portrait vocabulary in Wardrobe and Home; Bootstrap performs one surgical composition against current main and leaves the protected HUD/board files untouched.

**Tech Stack:** Unity 6000.3.16f1, C# 9/.NET Standard 2.1 production assemblies, NUnit 3, Newtonsoft.Json 13.0.2, UGUI, TextMeshPro, URP 17.5.0, RevenueCat purchases-unity 9.9.0, Google Play Billing through the existing RevenueCat integration.

**Spec:** `docs/superpowers/specs/2026-08-29-cat-cosmetics-avatar-system-design.md`

## Global Constraints

- Target `main` at `cfff675`; the feature worktree has merged that commit while preserving approved spec commit `15b2231`.
- The canonical save location is the nested member `payload["profile"]["cosmetics"]`. The spec's JSON is the member inserted inside `profile`, not a new top-level sibling.
- Advance `SaveDefaults.SAVE_VERSION` from 2 to 3. Register only additive `1 -> 2 -> 3` migration steps. The v2-to-v3 step must not read, normalize, or rewrite `entitlements.localLeases`.
- Every selected-cat/equip/unequip/earned mutation is disk-first: clone, mutate candidate, swap, `TryCommitAtomic`, restore the exact original payload identity on refusal or exception, then publish and notify only after success.
- Store, promotional, RevenueCat-ad, and local rewarded-ad access is authorized only by `PurchaseService.IsUnlocked(entitlementId)`. Never persist a store grant or add a paid-ownership field.
- Local `earnedItemIds` may contain only item rows whose declared acquisition is `earned`; entitlement-backed items must be rejected. Cats have no paid route in this model: `earnedCatIds` accepts only admitted non-starter cats, while the three launch cats are directly available through `Starter == true`.
- Prices come only from `PurchaseService.TryGetPrice`. No cosmetics JSON, string table, or view code may contain authored price text.
- Dashboard targets remain outfits $1.99, accessories $0.99, avatar frames $0.99, and Stationmaster Set $2.99, with no launch SKU above $2.99. Those numbers are human store configuration and never app data.
- The Stationmaster product stays in the pre-existing TASK 1 commerce catalogue but is not surfaced until a complete named bundle preview, all constituent assets/provenance, and a live localized store price exist. Its absence creates no card or gap.
- No paid randomness, paid intermediary currency, rarity/power tiers, countdown pressure, or gameplay-stat effects. Acquisition is deterministic and named.
- The conductor coat (`cm_outfit_conductor` -> `outfit_conductor`) is the only ship-blocking SKU. Missing optional assets or live products collapse out of the Wardrobe without a gap or player-facing error.
- The human licensing ruling is final: paid-tier Meshy/Tripo output is cleared for the store binary. A generated asset is still inadmissible without its complete provenance record.
- Current code-authored 2D portrait shapes use `sourceKind: "project_authored"` provenance. Any `sourceKind: "generated_paid"` record must contain provider, paid tier, task ID, prompt, generation timestamp, source hash, derivative hashes/transformation chain, custody location, and terms evidence.
- Delivery stays cheapest-first: profile/save and portrait code, three starter cats, two 2D frames, then the already-authored conductor gate. No new 3D outfit generation starts before that gate is proven.
- Generated provider source bytes and sidecars remain under `unity/Assets/Art/Generated/incoming/`; tracked derivatives retain hashes, and scale/axis/socket corrections live in Presentation rather than modifying the pinned source.
- Do not modify `BoardView.cs`, `BoardSceneLook.cs`, `BoardPropDecorator.cs`, `ToyTrainView.cs`, `ToyTrackMeshBuilder.cs`, or `WavePreviewStrip.cs`.
- Do not create a second save file, `PlayerPrefs` key, purchase service, entitlement ledger, rewarded grant API, or copied price cache.
- Keep `GameRoot.cs` changes surgical. The PR must include its literal zero-context hunk and an explicit list of device/store evidence that was not verified.
- The normal integration path is a PR to current `main`; TASK 16's Unity validation slot is the merge gate. Do not merge before that gate reports green.
- Do not read `.env`, upload to either store, force-push, or use `git commit -a`.
- Every commit uses `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.
- Unity `-runTests` commands never include `-quit`. The full EditMode run can emit no output for about 21 minutes.
- After binding a `RenderTexture`, yield one frame before screen-space layout. Painted-pixel arithmetic uses `Color32`/FLOAT32, not float64.
- Before any device command, run `adb devices -l` and confirm `48121FDAP006X4` reports the Pixel 9 Pro model. Never install on the Quest or Pico targets.

---

## File and Responsibility Map

### Create

- `unity/Assets/Scripts/Application/Save/SaveSchemaV3.cs` — additive v2-to-v3 migration and canonical default `profile.cosmetics` object.
- `unity/Assets/Scripts/Application/Save/SaveStoreCosmeticProfilePersistence.cs` — JSON adapter over the existing `SaveStore`; owns clone/swap/commit/rollback.
- `unity/Assets/Scripts/Services/Cosmetics/CosmeticCatalogDtos.cs` — immutable cat/item/asset/acquisition definitions.
- `unity/Assets/Scripts/Services/Cosmetics/CosmeticAssetInventory.cs` — engine-free renderer-token and provenance admission.
- `unity/Assets/Scripts/Services/Cosmetics/CosmeticCatalog.cs` — engine-free catalogue parser, compatibility checks, counts, and structured problems.
- `unity/Assets/Scripts/Services/Cosmetics/CosmeticProfileDtos.cs` — immutable saved loadouts, profile snapshots, and portrait snapshots.
- `unity/Assets/Scripts/Services/Cosmetics/ICosmeticProfilePersistence.cs` — load/replace durability boundary plus in-memory degraded implementation.
- `unity/Assets/Scripts/Services/Cosmetics/ICosmeticPortraitSource.cs` — typed Home/HUD portrait source.
- `unity/Assets/Scripts/Services/Cosmetics/CosmeticAccessResolver.cs` — starter/earned/`IsUnlocked` decision point.
- `unity/Assets/Scripts/Services/Cosmetics/CosmeticProfileService.cs` — selection, equip, preview, effective projection, disk-first publication, entitlement invalidation.
- `unity/Assets/Scripts/Services/Cosmetics/CosmeticRuntime.cs` — non-null runtime locator and `PurchaseRuntime.Installed` rebinding.
- `unity/Assets/Scripts/Services/Cosmetics/CosmeticWardrobeProjection.cs` — candidate-not-slot rows, routes, countdowns, and dynamic counts.
- `unity/Assets/Scripts/Services/Cosmetics/ICosmeticRewardedRoute.cs` — typed request seam with a disabled implementation; verified rewards still land through `PurchaseService`.
- `unity/Assets/Scripts/Services/Cosmetics/CosmeticDiagnostics.cs` — one-line static/dynamic read-back.
- `unity/Assets/Scripts/Presentation/Cosmetics/CosmeticPortraitPainter.cs` — the only renderer-token-to-UGUI vocabulary.
- `unity/Assets/Scripts/Presentation/Cosmetics/CosmeticPortraitView.cs` — reusable base/outfit/accessory/frame component and typed factory mount.
- `unity/Assets/Scripts/Presentation/Cosmetics/CosmeticItemCardView.cs` — one Wardrobe candidate card with real read-backs.
- `unity/Assets/Scripts/Bootstrap/CosmeticComposition.cs` — Resources loading, catalogue/inventory admission, persistent/degraded service construction, diagnostic logging.
- `unity/Assets/Resources/Cosmetics/cosmetic_catalog.json` — three starter cats plus only finished named item rows.
- `unity/Assets/Resources/Cosmetics/portrait_assets.json` — renderer tokens and per-asset provenance records.
- `unity/Assets/Tests/EditMode/Pure/Cosmetics/CosmeticCatalogTests.cs`
- `unity/Assets/Tests/EditMode/Pure/Cosmetics/CosmeticPersistenceTests.cs`
- `unity/Assets/Tests/EditMode/Pure/Cosmetics/CosmeticProfileServiceTests.cs`
- `unity/Assets/Tests/EditMode/Pure/Cosmetics/CosmeticWardrobeProjectionTests.cs`
- `unity/Assets/Tests/PlayMode/Cosmetics/CosmeticPortraitViewTests.cs`
- `unity/Assets/Tests/PlayMode/Cosmetics/CosmeticPortraitPixelTests.cs`
- `unity/Assets/Tests/PlayMode/Hud/CosmeticPortraitMountTests.cs`
- `unity/Assets/Tests/PlayMode/Screens/HomeCosmeticPortraitTests.cs`
- `unity/Assets/Tests/PlayMode/Bootstrap/CosmeticBootWiringTests.cs`

Unity import must create and commit the `.meta` sibling for every new C# directory/file and Resources asset. Do not hand-copy GUIDs from another asset.

### Modify

- `unity/Assets/Scripts/Application/Save/SaveDefaults.cs:13-99` — version 3 and fresh nested cosmetics defaults.
- `unity/Assets/Scripts/Application/Save/MigrationTable.cs:30-32` — register v2-to-v3 after v1-to-v2.
- `unity/Assets/Scripts/Presentation/Screens/WardrobeLayout.cs:6-79` — cat selector, tabs, compact list, portrait, status, action, restore geometry.
- `unity/Assets/Scripts/Presentation/Screens/WardrobeScreenView.cs:12-540` — replace the fixed coat-only screen with catalogue/profile projection while preserving the existing purchase and restore callbacks.
- `unity/Assets/Scripts/Presentation/Screens/HomeScreenView.cs:41-223` — mount the selected reusable portrait inside the existing `ParkedDistrictB` holder without removing the three holder nodes.
- `unity/Assets/Scripts/Bootstrap/GameRoot.cs:74,241-280,303-328,537-571,871-876,1562-1608` — one profile field, initialize/install before screen composition, pass dependencies, pause commit, teardown.
- `unity/Assets/Resources/Strings/ui.csv:35-59` — named cat/item/tab/state/action strings; no price literals.
- `unity/Assets/Tests/EditMode/Pure/Save/SaveMigrationTests.cs:13-251`
- `unity/Assets/Tests/EditMode/Pure/Save/SavePayloadTests.cs`
- `unity/Assets/Tests/EditMode/Presentation/UiCsvWardrobeTests.cs`
- `unity/Assets/Tests/PlayMode/Screens/WardrobePurchaseFlowTests.cs`
- `unity/Assets/Tests/PlayMode/Screens/HomeScreenStyleTests.cs`
- `unity/Assets/Tests/PlayMode/Bootstrap/WardrobeBootFlowTests.cs`

No assembly-definition or dotnet project edit is expected: existing source globs and assembly references already cover Services, Application, Presentation, Bootstrap, and pure tests.

---

### Task 1: Add the additive save-v3 schema

**Files:**
- Create: `unity/Assets/Scripts/Application/Save/SaveSchemaV3.cs`
- Modify: `unity/Assets/Scripts/Application/Save/SaveDefaults.cs:13-99`
- Modify: `unity/Assets/Scripts/Application/Save/MigrationTable.cs:30-32`
- Modify: `unity/Assets/Tests/EditMode/Pure/Save/SaveMigrationTests.cs`
- Modify: `unity/Assets/Tests/EditMode/Pure/Save/SavePayloadTests.cs`
- Modify: `unity/Assets/Tests/EditMode/Pure/Save/DailyReminderPreferencesTests.cs`

**Interfaces:**
- Consumes: `MigrationTable.Register(int, int, Func<JObject,JObject>)`, `SaveDefaults.FreshPayload()`.
- Produces: `SaveSchemaV3.DefaultCosmetics()`, `SaveSchemaV3.MigrateFromV2(JObject)`, `SaveDefaults.SAVE_VERSION == 3`, nested `profile.cosmetics`.

- [ ] **Step 1: Write fresh-payload and migration tests that pin the exact nested shape**

Add assertions equivalent to:

```csharp
[Test]
public void FreshPayload_IsV3_AndCosmeticsLivesOnlyInsideProfile()
{
    var payload = SaveDefaults.FreshPayload();
    Assert.That((int)payload["saveVersion"], Is.EqualTo(3));
    Assert.That(payload["cosmetics"], Is.Null, "cosmetics is not a top-level sibling");
    var cosmetics = (JObject)payload["profile"]["cosmetics"];
    Assert.That((string)cosmetics["selectedCatId"], Is.EqualTo("red_tabby"));
    CollectionAssert.IsEmpty((JArray)cosmetics["earnedCatIds"]);
    CollectionAssert.IsEmpty((JArray)cosmetics["earnedItemIds"]);
    Assert.That(((JArray)cosmetics["loadouts"]).Count, Is.EqualTo(1));
}

[Test]
public void DefaultV2ToV3_AddsCosmetics_WithoutReadingLeasesOrUnknownSiblings()
{
    var v2 = SaveDefaults.FreshPayload();
    v2["saveVersion"] = 2;
    ((JObject)v2["profile"]).Remove("cosmetics");
    var leases = new JArray(
        new JObject { ["entitlementId"] = "outfit_conductor", ["expiresAtUnixSeconds"] = 99L },
        new JObject { ["futureLeaseShape"] = new JArray(1, "two") });
    v2["entitlements"]["localLeases"] = leases.DeepClone();
    v2["futureRoot"] = new JObject { ["kept"] = true };

    var migrated = MigrationTable.CreateDefault().Migrate(v2, 2, 3);

    Assert.That((int)migrated["saveVersion"], Is.EqualTo(3));
    Assert.That(migrated["profile"]["cosmetics"], Is.InstanceOf<JObject>());
    Assert.That(JToken.DeepEquals(migrated["entitlements"]["localLeases"], leases), Is.True);
    Assert.That((bool)migrated["futureRoot"]["kept"], Is.True);
}
```

Also add:

- v1 -> v2 -> v3 ordered-chain coverage;
- existing `profile.cosmetics` deep-equality preservation;
- present non-object `profile` returns null;
- present non-object `profile.cosmetics` returns null;
- unknown siblings inside `profile` survive;
- the exact top-level/profile-key sets in `SavePayloadTests` reflect nesting and nothing else.
- the real v1 `SaveStore.Load` cases in `SaveMigrationTests` and `DailyReminderPreferencesTests` now end at v3 while their explicitly targeted `MigrationTable.Migrate(..., 1, 2)` cases still end at v2.

- [ ] **Step 2: Run the focused pure tests and verify the new expectations fail**

Run:

```bash
dotnet restore dotnet/CatMetro.sln -p:RestoreLockedMode=true
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj --no-restore \
  --filter 'FullyQualifiedName~CatMetro.Tests.Save.SaveMigrationTests|FullyQualifiedName~CatMetro.Tests.Save.SavePayloadTests'
```

Expected: failures report `SAVE_VERSION` is 2, the default migration has no 2-to-3 step, and `profile.cosmetics` is absent.

- [ ] **Step 3: Implement the minimal migration**

Use this structure:

```csharp
public static class SaveSchemaV3
{
    public static JObject DefaultCosmetics() => new JObject
    {
        ["selectedCatId"] = "red_tabby",
        ["earnedCatIds"] = new JArray(),
        ["earnedItemIds"] = new JArray(),
        ["loadouts"] = new JArray(Loadout("red_tabby")),
    };

    public static JObject MigrateFromV2(JObject payload)
    {
        if (payload == null) return null;
        if (payload["profile"] != null && !(payload["profile"] is JObject)) return null;
        var profile = payload["profile"] as JObject ?? new JObject();
        if (payload["profile"] == null) payload["profile"] = profile;
        if (profile["cosmetics"] != null && !(profile["cosmetics"] is JObject)) return null;
        if (profile["cosmetics"] == null) profile["cosmetics"] = DefaultCosmetics();
        return payload;
    }

    private static JObject Loadout(string catId) => new JObject
    {
        ["catId"] = catId,
        ["outfitId"] = "",
        ["accessoryId"] = "",
        ["frameId"] = "",
    };
}
```

Set `SAVE_VERSION = 3`, assign `SaveSchemaV3.DefaultCosmetics()` inside `FreshPayload()["profile"]` so the default is built at one decision site, and change `CreateDefault()` to:

```csharp
public static MigrationTable CreateDefault() => new MigrationTable()
    .Register(1, 2, SaveSchemaV2.MigrateFromV1)
    .Register(2, 3, SaveSchemaV3.MigrateFromV2);
```

Do not reference `entitlements` or `localLeases` from `SaveSchemaV3.cs`.

- [ ] **Step 4: Run focused and full pure save tests**

Run:

```bash
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj --no-restore \
  --filter 'FullyQualifiedName~CatMetro.Tests.Save'
```

Expected: all save tests pass, including the byte-level migration/reload and downgrade tests.

- [ ] **Step 5: Import the new script metadata and commit Task 1**

Run a normal batch import (this is not a test run, so `-quit` is correct here):

```bash
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath "$PWD/unity" -quit \
  -logFile /tmp/cm-cosmetics-save-v3-import.log
```

```bash
git add unity/Assets/Scripts/Application/Save/SaveSchemaV3.cs \
  unity/Assets/Scripts/Application/Save/SaveSchemaV3.cs.meta \
  unity/Assets/Scripts/Application/Save/SaveDefaults.cs \
  unity/Assets/Scripts/Application/Save/MigrationTable.cs \
  unity/Assets/Tests/EditMode/Pure/Save/SaveMigrationTests.cs \
  unity/Assets/Tests/EditMode/Pure/Save/SavePayloadTests.cs \
  unity/Assets/Tests/EditMode/Pure/Save/DailyReminderPreferencesTests.cs
git commit -m "feat: add cosmetics save schema v3" \
  -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: Admit the cosmetics catalogue and provenance inventory

**Files:**
- Create: `unity/Assets/Scripts/Services/Cosmetics/CosmeticCatalogDtos.cs`
- Create: `unity/Assets/Scripts/Services/Cosmetics/CosmeticAssetInventory.cs`
- Create: `unity/Assets/Scripts/Services/Cosmetics/CosmeticCatalog.cs`
- Create: `unity/Assets/Resources/Cosmetics/cosmetic_catalog.json`
- Create: `unity/Assets/Resources/Cosmetics/portrait_assets.json`
- Create: `unity/Assets/Tests/EditMode/Pure/Cosmetics/CosmeticCatalogTests.cs`

**Interfaces:**
- Consumes: JSON text, injected renderer-token identifiers, injected asset/provenance admission sets.
- Produces: `CosmeticAssetInventory.Empty/Parse`, `CosmeticCatalog.Empty/Parse`, `TryGetCat`, `TryGetItem`, `AdmittedRowCount`, `RejectedRowCount`, `AdmittedCatCount`, `Problems`.

- [ ] **Step 1: Define immutable catalogue types in tests first**

Pin these public names:

```csharp
public enum CosmeticSlot { Outfit, Accessory, Frame }
public enum CosmeticAcquisition { Starter, Earned, Entitlement }

public sealed class CosmeticCatDefinition
{
    public string Id { get; }
    public string DisplayNameKey { get; }
    public string PortraitAssetId { get; }
    public bool Starter { get; }
}

public sealed class CosmeticItemDefinition
{
    public string Id { get; }
    public CosmeticSlot Slot { get; }
    public string DisplayNameKey { get; }
    public string PortraitAssetId { get; }
    public CosmeticAcquisition Acquisition { get; }
    public string EntitlementId { get; }
    public string ProductId { get; }
    public string EarnInstructionKey { get; }
    public string RewardedPlacementId { get; }
    public IReadOnlyList<string> CompatibleCatIds { get; }
    public int Order { get; }
}
```

Tests must load the real Resources JSON from the repository and assert:

```csharp
Assert.That(catalog.AdmittedCatCount, Is.EqualTo(3));
Assert.That(catalog.AdmittedRowCount, Is.EqualTo(3));
Assert.That(catalog.RejectedRowCount, Is.Zero);
CollectionAssert.AreEqual(
    new[] { "red_tabby", "blue_siamese", "yellow_longhair" },
    catalog.Cats.Select(cat => cat.Id));
CollectionAssert.AreEqual(
    new[] { "outfit_conductor", "frame_brass", "frame_lantern" },
    catalog.Items.Select(item => item.Id));
```

Apply the duplicate-ID, unknown-slot, unknown-acquisition, missing-entitlement/product, missing-earned-instruction, unknown-compatible-cat, missing-asset, missing-provenance, unknown-renderer-token, and `random` mutations to submitted **item rows**. Each item mutation must reduce `AdmittedRowCount` and increase `RejectedRowCount`. Test malformed/duplicate cat rows separately: they reduce `AdmittedCatCount` and add a structured problem, but do not change the item-only `RejectedRowCount` promised by the design. A missing or future catalogue schema version must produce an empty catalogue plus a problem, never a partial interpretation.

- [ ] **Step 2: Verify catalogue tests fail before implementation**

Run:

```bash
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj --no-restore \
  --filter 'FullyQualifiedName~CatMetro.Tests.Cosmetics.CosmeticCatalogTests'
```

Expected: compile failure because the cosmetics catalogue types do not exist.

- [ ] **Step 3: Implement `CosmeticAssetInventory` with source-kind-specific provenance validation**

Expose:

```csharp
public sealed class CosmeticAssetInventory
{
    public static CosmeticAssetInventory Empty { get; }
    public IReadOnlyCollection<string> AssetIds { get; }
    public IReadOnlyCollection<string> ProvenanceAssetIds { get; }
    public IReadOnlyList<string> Problems { get; }

    public bool TryGet(string assetId, out CosmeticPortraitAssetDefinition definition);

    public static CosmeticAssetInventory Parse(
        string json,
        IReadOnlyCollection<string> supportedRendererTokens);
}
```

An asset is admitted only when its `assetId`, `rendererToken`, and `provenanceId` are non-empty, the renderer token is supported, and the referenced provenance record is valid. For `project_authored`, require `sourcePath` and `commercialDistribution: "cleared"`. For `generated_paid`, require every provenance field listed in Global Constraints and `commercialDistribution: "cleared"`. Never include a rejected asset in `AssetIds` or `ProvenanceAssetIds`.

- [ ] **Step 4: Implement `CosmeticCatalog` as a total parser**

Use a static signature that makes the headless admission proof explicit:

```csharp
public static CosmeticCatalog Empty { get; }

public static CosmeticCatalog Parse(
    string json,
    IReadOnlyCollection<string> portraitAssetIds,
    IReadOnlyCollection<string> provenanceAssetIds)
```

Reject a missing or unsupported catalogue `schemaVersion` as an empty catalogue with a structured problem. Otherwise catch JSON/field conversion errors per row, append a structured string to `Problems`, and continue. Increment `RejectedRowCount` once per submitted item row that fails. Do not reserve a visible slot for a rejected row. Reject any acquisition string other than `starter`, `earned`, or `entitlement`.

- [ ] **Step 5: Add the initial staged Resources data**

`cosmetic_catalog.json` contains exactly:

- starter cats `red_tabby`, `blue_siamese`, and `yellow_longhair`;
- `outfit_conductor`, mapped to `outfit_conductor` and `cm_outfit_conductor`;
- `frame_brass`, mapped to `frame_brass` and `cm_frame_brass`;
- `frame_lantern`, mapped to `frame_lantern` and `cm_frame_lantern`;
- all three items compatible with all three starter cats;
- no price, currency, random route, rarity, or stat field.

`portrait_assets.json` maps six base/item asset IDs to code-authored renderer tokens and six `project_authored` provenance records. The source paths point at `CosmeticPortraitPainter.cs`; this satisfies the provenance condition for the code-native 2D launch art. Do not list Engineer, Scarf, or Goggles until their portrait asset and provenance record actually exist.

- [ ] **Step 6: Add the no-price/no-random real-artifact assertions**

Walk the actual JSON token tree:

```csharp
var properties = root.DescendantsAndSelf().OfType<JProperty>().ToArray();
Assert.That(properties.Any(p =>
    p.Name.IndexOf("price", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
Assert.That(root.ToString().IndexOf("random", StringComparison.OrdinalIgnoreCase), Is.EqualTo(-1));
Assert.That(root.ToString().IndexOf("gacha", StringComparison.OrdinalIgnoreCase), Is.EqualTo(-1));
```

Also assert all declared entitlement/product IDs exist in the shipped Task 1 `PurchaseCatalog`.

- [ ] **Step 7: Run focused pure catalogue tests**

Run:

```bash
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj --no-restore \
  --filter 'FullyQualifiedName~CatMetro.Tests.Cosmetics.CosmeticCatalogTests'
```

Expected: all real-row and mutation tests pass with exact counts.

- [ ] **Step 8: Import Unity assets and commit Task 2**

Import once so every new C# and Resources asset receives a `.meta`:

```bash
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath "$PWD/unity" -quit \
  -logFile /tmp/cm-cosmetics-import.log
```

Then stage only this task's files:

```bash
git add unity/Assets/Scripts/Services/Cosmetics \
  unity/Assets/Scripts/Services/Cosmetics.meta \
  unity/Assets/Resources/Cosmetics \
  unity/Assets/Resources/Cosmetics.meta \
  unity/Assets/Tests/EditMode/Pure/Cosmetics.meta \
  unity/Assets/Tests/EditMode/Pure/Cosmetics/CosmeticCatalogTests.cs \
  unity/Assets/Tests/EditMode/Pure/Cosmetics/CosmeticCatalogTests.cs.meta
git commit -m "feat: add admitted cosmetics catalogue" \
  -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3: Add the atomic save adapter and immutable profile DTOs

**Files:**
- Create: `unity/Assets/Scripts/Services/Cosmetics/CosmeticProfileDtos.cs`
- Create: `unity/Assets/Scripts/Services/Cosmetics/ICosmeticProfilePersistence.cs`
- Create: `unity/Assets/Scripts/Services/Cosmetics/ICosmeticPortraitSource.cs`
- Create: `unity/Assets/Scripts/Application/Save/SaveStoreCosmeticProfilePersistence.cs`
- Create: `unity/Assets/Tests/EditMode/Pure/Cosmetics/CosmeticPersistenceTests.cs`

**Interfaces:**
- Consumes: existing `SaveStore.State.Payload`, `SaveStore.TryCommitAtomic`, v3 nested JSON.
- Produces: immutable `CosmeticLoadout`, `CosmeticProfileSnapshot`, `CosmeticPortraitSnapshot`, `ICosmeticProfilePersistence.TryLoad/TryReplace`, `InMemoryCosmeticProfilePersistence`.

- [ ] **Step 1: Write adapter tests against the real `SaveStore` and faulting filesystem**

Pin these cases:

1. load exact v3 profile;
2. preserve unknown IDs in desired loadouts;
3. successful replacement survives a new `SaveStore.Load`;
4. replacement preserves unknown root/profile/cosmetics siblings, unknown fields on a loadout matched by `catId`, and exact `localLeases`;
5. `TryCommitAtomic` refusal restores the original payload object identity;
6. write/replace exception restores the original identity and returns false;
7. malformed current-version cosmetics returns `TryLoad == false`, refuses later replacement, and never rewrites disk.

The fault assertion must measure identity:

```csharp
var original = store.State.Payload;
fs.FaultPoint = SFixtures.Fault.InReplace;

Assert.That(persistence.TryReplace(candidate), Is.False);
Assert.That(ReferenceEquals(store.State.Payload, original), Is.True);
Assert.That(JToken.DeepEquals(store.State.Payload["entitlements"]["localLeases"], leases), Is.True);
```

- [ ] **Step 2: Verify adapter tests fail**

Run:

```bash
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj --no-restore \
  --filter 'FullyQualifiedName~CatMetro.Tests.Cosmetics.CosmeticPersistenceTests'
```

Expected: compile failure for the missing DTOs/persistence boundary.

- [ ] **Step 3: Implement immutable snapshots and the persistence interface**

Use these signatures:

```csharp
public readonly struct CosmeticLoadout
{
    public string CatId { get; }
    public string OutfitId { get; }
    public string AccessoryId { get; }
    public string FrameId { get; }
    public string ItemFor(CosmeticSlot slot);
    public CosmeticLoadout With(CosmeticSlot slot, string itemId);
}

public sealed class CosmeticProfileSnapshot
{
    public static CosmeticProfileSnapshot Empty { get; }
    public string SelectedCatId { get; }
    public IReadOnlyList<string> EarnedCatIds { get; }
    public IReadOnlyList<string> EarnedItemIds { get; }
    public IReadOnlyList<CosmeticLoadout> Loadouts { get; }
    public CosmeticLoadout LoadoutFor(string catId);
    public CosmeticProfileSnapshot WithSelectedCat(string catId);
    public CosmeticProfileSnapshot WithLoadout(CosmeticLoadout loadout);
    public CosmeticProfileSnapshot WithEarnedCat(string catId);
    public CosmeticProfileSnapshot WithEarnedItem(string itemId);
}

public readonly struct CosmeticPortraitSnapshot : IEquatable<CosmeticPortraitSnapshot>
{
    public string CatId { get; }
    public string BaseAssetId { get; }
    public string OutfitAssetId { get; }
    public string AccessoryAssetId { get; }
    public string FrameAssetId { get; }
    public bool Equals(CosmeticPortraitSnapshot other);
    public override bool Equals(object obj);
    public override int GetHashCode();
}

public interface ICosmeticProfilePersistence
{
    bool TryLoad(out CosmeticProfileSnapshot snapshot);
    bool TryReplace(CosmeticProfileSnapshot snapshot);
}

public sealed class InMemoryCosmeticProfilePersistence : ICosmeticProfilePersistence
{
    public InMemoryCosmeticProfilePersistence(CosmeticProfileSnapshot initial);
    public bool TryLoad(out CosmeticProfileSnapshot snapshot);
    public bool TryReplace(CosmeticProfileSnapshot snapshot);
}

public interface ICosmeticPortraitSource
{
    event Action Changed;
    CosmeticPortraitSnapshot CurrentPortrait { get; }
    bool TryGetPortraitAsset(string assetId, out CosmeticPortraitAssetDefinition asset);
}
```

Constructors defensively copy lists; no mutable collection escapes.

- [ ] **Step 4: Implement the SaveStore adapter with the exact disk-first order**

`TryReplace` must:

```csharp
var original = _store.State.Payload;
var candidate = (JObject)original.DeepClone();
var cosmetics = EnsureNestedCosmetics(candidate);
WriteKnownFields(cosmetics, snapshot); // preserve unknown siblings and per-cat loadout fields
_store.State.Payload = candidate;
try
{
    if (_store.TryCommitAtomic()) return true;
    _store.Report("error_caught", "domain=cosmetics_save detail=commit refused");
}
catch (Exception ex)
{
    _store.Report("error_caught", "domain=cosmetics_save detail=" + ex.GetType().Name);
}
_store.State.Payload = original;
return false;
```

`WriteKnownFields` replaces only the four owned cosmetics members. When serializing loadouts, start from the cloned existing object with the same `catId` and overwrite only `catId`, `outfitId`, `accessoryId`, and `frameId`, so forward-compatible fields survive a normal equip. Do not inspect `entitlements`; cloning the entire payload is the only interaction that subtree receives.
Before cloning, validate that the current `profile` and `profile.cosmetics` containers are objects. A malformed current-version owned container makes `TryReplace` return false; it is never overwritten with defaults.

- [ ] **Step 5: Implement degraded in-memory persistence**

`InMemoryCosmeticProfilePersistence` holds one immutable snapshot, accepts replacements, and never touches disk. It is used only when production save startup fails or by focused tests.

- [ ] **Step 6: Run focused persistence and save tests**

Run:

```bash
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj --no-restore \
  --filter 'FullyQualifiedName~CatMetro.Tests.Cosmetics.CosmeticPersistenceTests|FullyQualifiedName~CatMetro.Tests.Save'
```

Expected: all tests pass, including refusal/exception rollback identity.

- [ ] **Step 7: Import metadata and commit Task 3**

```bash
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath "$PWD/unity" -quit \
  -logFile /tmp/cm-cosmetics-persistence-import.log
```

```bash
git add unity/Assets/Scripts/Services/Cosmetics/CosmeticProfileDtos.cs \
  unity/Assets/Scripts/Services/Cosmetics/ICosmeticProfilePersistence.cs \
  unity/Assets/Scripts/Services/Cosmetics/ICosmeticPortraitSource.cs \
  unity/Assets/Scripts/Application/Save/SaveStoreCosmeticProfilePersistence.cs \
  unity/Assets/Tests/EditMode/Pure/Cosmetics/CosmeticPersistenceTests.cs \
  unity/Assets/Scripts/Services/Cosmetics/*.meta \
  unity/Assets/Scripts/Application/Save/SaveStoreCosmeticProfilePersistence.cs.meta \
  unity/Assets/Tests/EditMode/Pure/Cosmetics/CosmeticPersistenceTests.cs.meta
git commit -m "feat: persist cosmetic profiles atomically" \
  -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 4: Implement profile selection, equipping, effective portraits, and runtime rebinding

**Files:**
- Create: `unity/Assets/Scripts/Services/Cosmetics/CosmeticAccessResolver.cs`
- Create: `unity/Assets/Scripts/Services/Cosmetics/CosmeticProfileService.cs`
- Create: `unity/Assets/Scripts/Services/Cosmetics/CosmeticRuntime.cs`
- Create: `unity/Assets/Tests/EditMode/Pure/Cosmetics/CosmeticProfileServiceTests.cs`

**Interfaces:**
- Consumes: `CosmeticCatalog`, `CosmeticAssetInventory`, `ICosmeticProfilePersistence`, `PurchaseService.IsUnlocked`, `PurchaseService.Ledger.Changed`, `PurchaseRuntime.Installed`.
- Produces: the exact constructor/properties/methods below, `CurrentPortrait`, `Changed`, `BindPurchases`, `CosmeticRuntime.Current`, and conditional runtime uninstall.

```csharp
public sealed class CosmeticProfileService : ICosmeticPortraitSource, IDisposable
{
    public CosmeticProfileService(
        CosmeticCatalog catalog,
        CosmeticAssetInventory assets,
        ICosmeticProfilePersistence persistence,
        PurchaseService purchases);

    public CosmeticCatalog Catalog { get; }
    public CosmeticProfileSnapshot Profile { get; }
    public string SelectedCatId { get; }
    public CosmeticPortraitSnapshot CurrentPortrait { get; }
    public event Action Changed;

    public bool IsAccessible(string itemId);
    public bool TrySelectCat(string catId);
    public bool TryEquip(string catId, CosmeticSlot slot, string itemId);
    public bool TryUnequip(string catId, CosmeticSlot slot);
    public bool TryGrantEarnedCat(string catId);
    public bool TryGrantEarnedItem(string itemId);
    public CosmeticPortraitSnapshot EffectivePortraitFor(string catId);
    public CosmeticPortraitSnapshot PreviewPortrait(
        string catId, CosmeticSlot slot, string itemId);
    public void BindPurchases(PurchaseService purchases);
    public void Dispose();
}
```

- [ ] **Step 1: Write profile-service tests for durable publication and access lifecycle**

Cover:

- three starter cats are selectable;
- selecting/equipping calls persistence before `Changed`;
- failed persistence leaves the selected cat/loadout and event count unchanged;
- an entitlement-backed item cannot be inserted into `earnedItemIds`;
- equip calls through a real `PurchaseService` over an injected `EntitlementLedger`, so the only authorization query exercised is `PurchaseService.IsUnlocked`;
- locked complete preview succeeds but equip fails;
- a saved conductor ID paints when unlocked;
- replacing store grants with none (refund simulation) omits only the effective outfit and leaves the saved ID;
- advancing the injected clock past a rewarded lease and calling the existing `PurchaseService.PruneExpiredLeases()` produces the same layer-only omission without a persistence call;
- restoring the store grant makes the same saved outfit return without a persistence call;
- unknown/incompatible desired IDs remain in the snapshot but do not paint;
- an unknown saved selected-cat ID remains in `Profile.SelectedCatId` while the effective `SelectedCatId`/portrait safely uses the first admitted accessible starter;
- `PurchaseRuntime.Install` rebinding detaches the old ledger and reacts to the new ledger.

Use an ordered fake:

```csharp
var order = new List<string>();
var persistence = new RecordingPersistence(order);
var service = CreateService(persistence);
service.Changed += () => order.Add("changed");

Assert.That(service.TrySelectCat("blue_siamese"), Is.True);
CollectionAssert.AreEqual(new[] { "persist", "changed" }, order);
```

- [ ] **Step 2: Run tests to confirm the service is absent**

Run:

```bash
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj --no-restore \
  --filter 'FullyQualifiedName~CatMetro.Tests.Cosmetics.CosmeticProfileServiceTests'
```

Expected: compile failure for `CosmeticProfileService`, resolver, and runtime.

- [ ] **Step 3: Implement the one access resolver**

Give it `CosmeticAccessResolver(PurchaseService purchases)`, `BindPurchases(PurchaseService purchases)`, and the access method below:

```csharp
public bool IsAccessible(CosmeticItemDefinition item, CosmeticProfileSnapshot profile)
{
    switch (item.Acquisition)
    {
        case CosmeticAcquisition.Starter:
            return true;
        case CosmeticAcquisition.Earned:
            return profile.EarnedItemIds.Contains(item.Id, StringComparer.Ordinal);
        case CosmeticAcquisition.Entitlement:
            return _purchases.IsUnlocked(item.EntitlementId);
        default:
            return false;
    }
}
```

Cat access is starter or an ID in `EarnedCatIds`. Do not call `EntitlementLedger.IsActive` or examine grant sources.

- [ ] **Step 4: Implement disk-first profile mutations**

Every mutator follows:

```csharp
var candidate = _profile.With...;
if (!_persistence.TryReplace(candidate)) return false;
_profile = candidate;
RecomputeCurrentPortrait();
Changed?.Invoke();
return true;
```

Validate cat/item existence, current cat access, slot equality, compatibility, and current item access before building the candidate. `TryGrantEarnedItem` rejects any row whose acquisition is not `Earned`; `TryGrantEarnedCat` rejects an unknown or starter cat, because admitted non-starter cats are the only deterministic earned-cat route.

On construction, load the persisted snapshot. When loading fails because storage is unavailable, build an in-memory effective default from the first admitted starter cat without writing. Keep raw desired IDs in `Profile`; expose the validated fallback through `SelectedCatId` and `CurrentPortrait`.

- [ ] **Step 5: Implement preview and lapsed-layer behavior**

`PreviewPortrait(catId, slot, itemId)` validates the row and compatibility but deliberately does not require access. `EffectivePortraitFor(catId)` looks up each desired ID and includes its asset only when the resolver currently returns true. Neither method rewrites the saved snapshot.

On `Ledger.Changed`, recompute the current effective portrait and raise `Changed` only if the immutable portrait value changed. Never invoke persistence from this callback.

- [ ] **Step 6: Implement runtime install/rebind/reset**

`CosmeticRuntime.Current` is never null. Its degraded value is a `CosmeticProfileService` built from `CosmeticCatalog.Empty`, `CosmeticAssetInventory.Empty`, `new InMemoryCosmeticProfilePersistence(CosmeticProfileSnapshot.Empty)`, and `PurchaseRuntime.Current`. `Install(CosmeticProfileService)` binds the installed service to `PurchaseRuntime.Current`, subscribes once to `PurchaseRuntime.Installed`, and rebinds to `PurchaseRuntime.Current` when that event fires. `Uninstall(CosmeticProfileService expected)` replaces the current value with that degraded service only when `ReferenceEquals(Current, expected)`, so one root cannot tear down another root's service. `ResetForTests()` unsubscribes static handlers and installs the same degraded in-memory service.

- [ ] **Step 7: Run profile, purchase-convergence, and save tests**

Run:

```bash
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj --no-restore \
  --filter 'FullyQualifiedName~CatMetro.Tests.Cosmetics|FullyQualifiedName~CatMetro.Tests.Purchases.AdPurchaseConvergenceTests|FullyQualifiedName~CatMetro.Tests.Save'
```

Expected: profile tests pass and the existing paid/ad convergence tests remain green.

- [ ] **Step 8: Import metadata and commit Task 4**

```bash
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath "$PWD/unity" -quit \
  -logFile /tmp/cm-cosmetics-profile-import.log
```

```bash
git add unity/Assets/Scripts/Services/Cosmetics/CosmeticAccessResolver.cs \
  unity/Assets/Scripts/Services/Cosmetics/CosmeticProfileService.cs \
  unity/Assets/Scripts/Services/Cosmetics/CosmeticRuntime.cs \
  unity/Assets/Tests/EditMode/Pure/Cosmetics/CosmeticProfileServiceTests.cs \
  unity/Assets/Scripts/Services/Cosmetics/*.meta \
  unity/Assets/Tests/EditMode/Pure/Cosmetics/CosmeticProfileServiceTests.cs.meta
git commit -m "feat: add persistent cosmetic profile service" \
  -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 5: Project compact Wardrobe rows and expose one-line diagnostics

**Files:**
- Create: `unity/Assets/Scripts/Services/Cosmetics/ICosmeticRewardedRoute.cs`
- Create: `unity/Assets/Scripts/Services/Cosmetics/CosmeticWardrobeProjection.cs`
- Create: `unity/Assets/Scripts/Services/Cosmetics/CosmeticDiagnostics.cs`
- Create: `unity/Assets/Tests/EditMode/Pure/Cosmetics/CosmeticWardrobeProjectionTests.cs`

**Interfaces:**
- Consumes: catalogue order, selected cat, desired loadout, `CosmeticAccessResolver`, `PurchaseService.TryGetPrice`, `PurchaseService.SecondsUntilExpiry`, rewarded availability seam.
- Produces: compact `CosmeticWardrobeRow` list, `CosmeticWardrobeRoute`, caller-computed visible/purchasable counts, and the exact projection/diagnostic signatures below.

```csharp
public static IReadOnlyList<CosmeticWardrobeRow> Build(
    CosmeticCatalog catalog,
    CosmeticProfileService profile,
    PurchaseService purchases,
    ICosmeticRewardedRoute rewarded,
    string catId,
    CosmeticSlot slot);

public static string OneLine(
    CosmeticCatalog catalog,
    CosmeticAssetInventory assets,
    int visibleRowCount,
    int purchasableRowCount,
    bool conductorReady);
```

The first signature belongs to `CosmeticWardrobeProjection`; the second belongs to `CosmeticDiagnostics`. `Build` reads `profile.Profile` and `profile.IsAccessible(item.Id)` rather than constructing another resolver.

- [ ] **Step 1: Write candidate-not-slot and diagnostics tests**

Cover exact projections:

- accessible row stays visible when its product price disappears;
- locked entitlement row with neither price nor rewarded route is absent;
- locked entitlement row with a localized price is `Purchase`;
- locked entitlement row with no price but an offerable rewarded placement is `Rewarded`;
- deterministic earned row is `EarnInstruction`;
- incompatible and statically rejected rows are absent;
- output list indices are contiguous after any omission;
- temporary access reports `SecondsRemaining > 0`;
- exact diagnostic string contains admitted/rejected cats/rows, asset-ready, visible, purchasable, and conductor readiness.

Expected format:

```text
COSMETICS admittedRows=3 rejectedRows=0 admittedCats=3 assetReadyRows=3 visibleRows=1 purchasableRows=1 conductorReady=true
```

- [ ] **Step 2: Verify projection tests fail**

Run:

```bash
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj --no-restore \
  --filter 'FullyQualifiedName~CatMetro.Tests.Cosmetics.CosmeticWardrobeProjectionTests'
```

Expected: compile failure for projection and diagnostics types.

- [ ] **Step 3: Define the typed disabled rewarded route**

Use:

```csharp
public interface ICosmeticRewardedRoute
{
    bool CanOffer(string placementId, string entitlementId);
    void Request(string placementId, Action completed);
}

public sealed class DisabledCosmeticRewardedRoute : ICosmeticRewardedRoute
{
    public bool CanOffer(string placementId, string entitlementId) => false;
    public void Request(string placementId, Action completed) => completed?.Invoke();
}
```

This seam never grants. The ad-owning implementation handles the network callback and calls the existing verified grant path; Wardrobe rechecks `IsUnlocked` after `completed`.

- [ ] **Step 4: Implement row projection without slot reservation**

Model access state and action route separately so a borrowed item can show time remaining while retaining a permanent named purchase:

```csharp
public enum CosmeticWardrobeRoute { None, Equip, Purchase, Rewarded, EarnInstruction }

public readonly struct CosmeticWardrobeRow
{
    public CosmeticItemDefinition Item { get; }
    public bool IsAccessible { get; }
    public bool IsEquipped { get; }
    public long SecondsRemaining { get; }
    public CosmeticWardrobeRoute Route { get; }
    public LocalizedPrice Price { get; }
}
```

For a locked entitlement row, choose the purchase route when `TryGetPrice` succeeds; otherwise choose rewarded only when the typed route is offerable. Add nothing when neither route exists. For an accessible temporary item, keep the row, populate its countdown, and retain `Purchase` when the direct product has a known price.

- [ ] **Step 5: Implement the numeric read-back and conductor gate**

`CosmeticDiagnostics.OneLine` receives static catalogue/inventory counts and the current projection. `conductorReady` is true only when the conductor row is admitted, its portrait asset resolves, and either it is unlocked or its real product price is known.

- [ ] **Step 6: Run focused projection tests**

Run:

```bash
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj --no-restore \
  --filter 'FullyQualifiedName~CatMetro.Tests.Cosmetics'
```

Expected: all cosmetics pure tests pass.

- [ ] **Step 7: Import metadata and commit Task 5**

```bash
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath "$PWD/unity" -quit \
  -logFile /tmp/cm-cosmetics-projection-import.log
```

```bash
git add unity/Assets/Scripts/Services/Cosmetics/ICosmeticRewardedRoute.cs \
  unity/Assets/Scripts/Services/Cosmetics/CosmeticWardrobeProjection.cs \
  unity/Assets/Scripts/Services/Cosmetics/CosmeticDiagnostics.cs \
  unity/Assets/Tests/EditMode/Pure/Cosmetics/CosmeticWardrobeProjectionTests.cs \
  unity/Assets/Scripts/Services/Cosmetics/*.meta \
  unity/Assets/Tests/EditMode/Pure/Cosmetics/CosmeticWardrobeProjectionTests.cs.meta
git commit -m "feat: project compact cosmetic catalogue rows" \
  -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 6: Build the reusable portrait and typed HUD mounting seam

**Files:**
- Create: `unity/Assets/Scripts/Presentation/Cosmetics/CosmeticPortraitPainter.cs`
- Create: `unity/Assets/Scripts/Presentation/Cosmetics/CosmeticPortraitView.cs`
- Create: `unity/Assets/Tests/PlayMode/Cosmetics/CosmeticPortraitViewTests.cs`
- Create: `unity/Assets/Tests/PlayMode/Cosmetics/CosmeticPortraitPixelTests.cs`
- Create: `unity/Assets/Tests/PlayMode/Hud/CosmeticPortraitMountTests.cs`

**Interfaces:**
- Consumes: `ICosmeticPortraitSource`, `CosmeticPortraitSnapshot`, admitted renderer token, `Palette`, `HudShapeSprites`, `UiChromeMaterial.Shared`.
- Produces: `CosmeticPortraitView.Create(Transform, ICosmeticPortraitSource, string)`, `Bind`, `ApplySnapshot`, real layer transforms/images, zero-raycast render tree.

- [ ] **Step 1: Write mount/lifecycle/read-back tests before the view**

Pin:

- the factory parents exactly one `CosmeticPortraitView` under the supplied host;
- base/outfit/accessory/frame layer roots exist in draw order;
- every `Image.raycastTarget` is false and no `Selectable`, `Button`, or collider exists;
- source `Changed` repaints the actual layer tree;
- disable/destroy detaches the subscription;
- empty/lapsed layer IDs deactivate their layer root;
- all three cat renderer tokens produce distinct `Color32` pixels;
- Brass and Lantern renderer tokens paint distinct frame pixels;
- Conductor paints readable navy/brass pixels at phone size.
- the renderer tokens admitted by the real `portrait_assets.json` set-equal `CosmeticPortraitPainter.SupportedRendererTokens`, and every `project_authored.sourcePath` resolves to an existing repository file rather than merely being a non-empty string.

Expose real read-backs:

```csharp
public RectTransform RootTransform { get; }
public RectTransform BaseLayerTransform { get; }
public RectTransform OutfitLayerTransform { get; }
public RectTransform AccessoryLayerTransform { get; }
public RectTransform FrameLayerTransform { get; }
public string AppliedCatId { get; }
public string AppliedOutfitAssetId { get; }
public string AppliedAccessoryAssetId { get; }
public string AppliedFrameAssetId { get; }
```

- [ ] **Step 2: Run PlayMode tests and verify the component is absent**

Run the pinned Unity binary without `-quit`:

```bash
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD/unity" -runTests -testPlatform PlayMode \
  -testFilter 'CatMetro.Tests.PlayMode.CosmeticPortraitViewTests|CatMetro.Tests.PlayMode.CosmeticPortraitPixelTests|CatMetro.Tests.PlayMode.CosmeticPortraitMountTests' \
  -testResults /tmp/cm-cosmetics-portrait-red.xml \
  -logFile /tmp/cm-cosmetics-portrait-red.log
```

Expected: compile/test failure for the missing view/painter.

- [ ] **Step 3: Implement one code-native portrait vocabulary**

`CosmeticPortraitPainter.SupportedRendererTokens` contains exactly the six tokens declared in `portrait_assets.json`. Paint:

- Red Tabby: Signal Red round head, same-hue ears, ink eyes/muzzle, tabby stripe marks.
- Blue Siamese: Harbor Blue head, ink ears/muzzle, cream face centre.
- Yellow Longhair: Tabby Yellow head with wider cheek tufts and ink face.
- Conductor: Ink Navy coat/hat, Cream collar, Tabby Yellow buttons/badge.
- Brass frame: double Cream/Tabby Yellow rail with corner ticket notches.
- Lantern frame: Ink Navy/Metro Teal rail with four warm lantern corner marks.

All geometry is UGUI `Image`; use `HudShapeSprites` and `UiChromeMaterial.Shared`. Do not add sprites or material instances.

- [ ] **Step 4: Implement the typed factory and safe subscription lifecycle**

Use:

```csharp
public static CosmeticPortraitView Create(
    Transform parent,
    ICosmeticPortraitSource source,
    string name = "CosmeticPortrait")
```

`Bind` unsubscribes any prior source, subscribes the new source, and immediately applies `CurrentPortrait`. `OnDisable` unsubscribes; `OnEnable` resubscribes only when a source has been bound; `OnDestroy` unsubscribes idempotently. `ApplySnapshot` resolves renderer tokens through `source.TryGetPortraitAsset` and clears a layer when resolution fails.

The typed HUD seam is this exact factory plus `ICosmeticPortraitSource`; no HUD-owner file changes in this branch.

- [ ] **Step 5: Implement FLOAT32 painted-pixel assertions**

Bind the capture camera to a `RenderTexture`, `yield return null`, lay out the portrait, `Canvas.ForceUpdateCanvases`, render, and inspect `Texture2D.GetPixels32()`. Compare a plain-cat negative control with coat/frame states and require non-zero increases in the expected palette-family pixel counts inside the actual `RootTransform` world-corner rectangle. Resolve the repository root from `Application.dataPath` in the test and call `File.Exists` for every admitted `project_authored.sourcePath`; do not let provenance pass on string presence alone.

Do not assert only GameObject active flags.

- [ ] **Step 6: Run portrait PlayMode tests**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD/unity" -runTests -testPlatform PlayMode \
  -testFilter 'CatMetro.Tests.PlayMode.CosmeticPortraitViewTests|CatMetro.Tests.PlayMode.CosmeticPortraitPixelTests|CatMetro.Tests.PlayMode.CosmeticPortraitMountTests' \
  -testResults /tmp/cm-cosmetics-portrait-green.xml \
  -logFile /tmp/cm-cosmetics-portrait-green.log
```

Expected: all portrait, lifecycle, mount, and pixel tests pass.

- [ ] **Step 7: Capture and inspect the portrait sheet**

Arm the test's capture directory and run the pixel fixture to emit the individual plain, complete-preview, purchased/equipped, lapsed, restored, Brass-frame, and Lantern-frame states plus a composed `portrait-states.png` contact sheet:

```bash
CM_COSMETIC_CAPTURE_DIR=/tmp/cm-cosmetic-portraits \
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD/unity" -runTests -testPlatform PlayMode \
  -testFilter 'CatMetro.Tests.PlayMode.CosmeticPortraitPixelTests' \
  -testResults /tmp/cm-cosmetics-portrait-capture.xml \
  -logFile /tmp/cm-cosmetics-portrait-capture.log
```

Assert `/tmp/cm-cosmetic-portraits/portrait-states.png` exists, then open every PNG in the directory and compare it to the rounded, high-contrast target language in `docs/reference/target-01-tabletop.png` and `target-02-diorama.png`.

- [ ] **Step 8: Commit Task 6**

```bash
git add unity/Assets/Scripts/Presentation/Cosmetics \
  unity/Assets/Scripts/Presentation/Cosmetics.meta \
  unity/Assets/Tests/PlayMode/Cosmetics \
  unity/Assets/Tests/PlayMode/Cosmetics.meta \
  unity/Assets/Tests/PlayMode/Hud/CosmeticPortraitMountTests.cs \
  unity/Assets/Tests/PlayMode/Hud/CosmeticPortraitMountTests.cs.meta
git commit -m "feat: add reusable cosmetic portrait mount" \
  -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 7: Replace the fixed coat screen with the profile Wardrobe

**Files:**
- Create: `unity/Assets/Scripts/Presentation/Cosmetics/CosmeticItemCardView.cs`
- Modify: `unity/Assets/Scripts/Presentation/Screens/WardrobeLayout.cs`
- Modify: `unity/Assets/Scripts/Presentation/Screens/WardrobeScreenView.cs`
- Modify: `unity/Assets/Resources/Strings/ui.csv`
- Modify: `unity/Assets/Tests/EditMode/Presentation/UiCsvWardrobeTests.cs`
- Modify: `unity/Assets/Tests/PlayMode/Screens/WardrobePurchaseFlowTests.cs`

**Interfaces:**
- Consumes: `CosmeticProfileService`, `CosmeticWardrobeProjection`, `CosmeticPortraitView`, `PurchaseService`, `ICosmeticRewardedRoute`, `ChromeRegions`.
- Produces: cat selector, Outfit/Accessory/Frame tabs, compact cards, complete preview, equip/purchase/restore actions, readable card/portrait diagnostics, and:

```csharp
public static WardrobeScreenView Create(
    Transform canvasParent,
    PurchaseService purchases,
    CosmeticProfileService profile,
    ICosmeticRewardedRoute rewarded);
```

- [ ] **Step 1: Rewrite tests around the approved product surface**

Keep the existing purchase/restore/rewarded convergence cases and add:

- all three starter cat buttons select and persist;
- tabs expose only the selected slot;
- locked conductor tap paints a complete preview without equipping/saving;
- successful purchase rechecks `IsUnlocked`, atomically equips, and remains on screen;
- failed save after purchase leaves ownership intact but does not claim/paint equipped state;
- lapsed saved conductor hides the effective layer without erasing the desired ID;
- restored access repaints the same saved conductor;
- missing frame product compacts the card list with no blank child or registered region;
- missing asset compacts the list even when a fake store price exists;
- the launch-empty Accessory tab contains zero cards/regions and one non-interactive localized empty-state label rather than a reserved product slot;
- localized price comes from the fake backend;
- borrowed access keeps the permanent purchase route and countdown;
- Restore remains visible and source-correct;
- painted-pixel checks prove coat/frame ink rather than trusting labels.

Update the view factory in tests to supply a real in-memory cosmetics service and disabled rewarded route.

- [ ] **Step 2: Run focused Wardrobe tests and verify they fail against the fixed screen**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD/unity" -runTests -testPlatform PlayMode \
  -testFilter 'CatMetro.Tests.PlayMode.WardrobePurchaseFlowTests' \
  -testResults /tmp/cm-cosmetics-wardrobe-red.xml \
  -logFile /tmp/cm-cosmetics-wardrobe-red.log
```

Expected: failures for missing cat selector/tabs/cards and old auto-painted coat behavior.

- [ ] **Step 3: Add complete CSV vocabulary**

Add explicit keys for:

- Red Tabby, Blue Siamese, Yellow Longhair;
- Outfit, Accessory, Frame tabs;
- Conductor's Coat, Brass Ticket Frame, Lantern Frame;
- Equipped, Owned, Equip, Unequip, Watch to borrow, time remaining, deterministic earning instruction;
- the neutral empty-list message used when a tab has no admitted/available candidates;
- purchase/restore/save/cancel/pending/unavailable states.

The buy row remains `Unlock · {price}`. Do not add `$`, currency codes, or numeric price text.

- [ ] **Step 4: Implement pure layout rectangles**

Extend `WardrobeLayout` with deterministic methods for:

```csharp
public static Rect CatSelectorRect(Rect safeArea, float dpi);
public static Rect PortraitRect(Rect safeArea, float dpi);
public static Rect TabsRect(Rect safeArea, float dpi);
public static Rect ItemsRect(Rect safeArea, float dpi);
public static Rect ItemCardRect(Rect itemsRect, int visibleIndex, int visibleCount, float dpi);
public static Rect PrimaryActionRect(Rect safeArea, float dpi);
```

Use only compact `visibleIndex` values from the projection. Every action rectangle must meet `HudBands.MinTargetDp`.

- [ ] **Step 5: Implement card pooling and typed regions**

`RebuildCards()` destroys or parks prior cards, creates exactly one card per projected row, lays them out at contiguous indices, and registers only painted cards. Region IDs include item IDs (`wardrobe.item.outfit_conductor`) so omission cannot leave a ghost region.

`CosmeticItemCardView` exposes its actual label text, route, root transform, and active state for tests.

- [ ] **Step 6: Replace `BuildProfileCat` with the shared portrait**

Mount one large `CosmeticPortraitView` in the portrait card and one small bound portrait in the Home-visible Wardrobe entry capsule. Delete the duplicate ears/face/coat construction from `WardrobeScreenView`; keep no private cosmetic geometry.

Card tap sets Presentation-only preview state:

```csharp
_previewItemId = row.Item.Id;
_portrait.ApplySnapshot(_profile.PreviewPortrait(
    _profile.SelectedCatId, row.Item.Slot, row.Item.Id));
```

It must not call persistence.

- [ ] **Step 7: Route primary action through existing commerce and atomic equip**

For `Purchase`:

1. call `_purchases.Purchase(row.Item.ProductId, callback)`;
2. do not infer ownership from `PurchaseResult`;
3. in the callback call `_purchases.IsUnlocked(row.Item.EntitlementId)`;
4. only when true call `_profile.TryEquip(...)`;
5. if persistence fails, clear the Presentation-only preview, reapply `_profile.CurrentPortrait`, show the save-failure key, and keep desired/effective saved state unchanged.

For `Rewarded`, call the typed route and, on completion, recheck `IsUnlocked` before equip. For `Equip`, call `TryEquip`. For `EarnInstruction`, show its deterministic CSV instruction and issue no grant.

Restore only refreshes authority. A previously saved lapsed layer returns through `Changed`; a newly restored but never-equipped item becomes `Owned`/`Equip` and is not silently added to the loadout.

- [ ] **Step 8: Refresh products by callback, entitlements by event**

On `Open`, subscribe to profile changes and invoke `_purchases.Refresh(() => RebuildProjectionAndCards())`. `Ledger.Changed` reaches the profile service, which recomputes effective portraits. Do not treat `Ledger.Changed` as a product-price event.

Mirror existing `Hide`, `OnDisable`, `OnEnable`, and `OnDestroy` unregistration laws for cards/profile subscriptions.

- [ ] **Step 9: Run Wardrobe, CSV, and pure cosmetics tests**

Run:

```bash
dotnet test dotnet/CatMetro.Tests/CatMetro.Tests.csproj --no-restore \
  --filter 'FullyQualifiedName~CatMetro.Tests.Cosmetics'

/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD/unity" -runTests -testPlatform EditMode \
  -testFilter 'CatMetro.Tests.Presentation.UiCsvWardrobeTests' \
  -testResults /tmp/cm-cosmetics-csv.xml -logFile /tmp/cm-cosmetics-csv.log

/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD/unity" -runTests -testPlatform PlayMode \
  -testFilter 'CatMetro.Tests.PlayMode.WardrobePurchaseFlowTests' \
  -testResults /tmp/cm-cosmetics-wardrobe-green.xml \
  -logFile /tmp/cm-cosmetics-wardrobe-green.log
```

Expected: all focused suites pass.

- [ ] **Step 10: Capture and inspect phone-sized Wardrobe states**

Emit 917x2048 frames for plain, locked preview, purchased/equipped, Brass frame, Lantern frame, lapsed, and restored states. Open all frames; reject clipped tabs/actions, blank cards, unreadable outfit pixels, and any row gap.

- [ ] **Step 11: Commit Task 7**

```bash
git add unity/Assets/Scripts/Presentation/Cosmetics/CosmeticItemCardView.cs \
  unity/Assets/Scripts/Presentation/Screens/WardrobeLayout.cs \
  unity/Assets/Scripts/Presentation/Screens/WardrobeScreenView.cs \
  unity/Assets/Resources/Strings/ui.csv \
  unity/Assets/Tests/EditMode/Presentation/UiCsvWardrobeTests.cs \
  unity/Assets/Tests/PlayMode/Screens/WardrobePurchaseFlowTests.cs \
  unity/Assets/Scripts/Presentation/Cosmetics/CosmeticItemCardView.cs.meta
git commit -m "feat: ship the profile cat wardrobe" \
  -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 8: Mount the Home portrait and compose the persistent runtime surgically

**Files:**
- Create: `unity/Assets/Scripts/Bootstrap/CosmeticComposition.cs`
- Modify: `unity/Assets/Scripts/Presentation/Screens/HomeScreenView.cs`
- Modify: `unity/Assets/Scripts/Bootstrap/GameRoot.cs`
- Create: `unity/Assets/Tests/PlayMode/Screens/HomeCosmeticPortraitTests.cs`
- Create: `unity/Assets/Tests/PlayMode/Bootstrap/CosmeticBootWiringTests.cs`
- Modify: `unity/Assets/Tests/PlayMode/Screens/HomeScreenStyleTests.cs`
- Modify: `unity/Assets/Tests/PlayMode/Bootstrap/WardrobeBootFlowTests.cs`

**Interfaces:**
- Consumes: shipped Resources JSON, existing loaded `SaveStore`, `PurchaseRuntime.Current`, `CosmeticRuntime`, Home/Wardrobe factories.
- Produces: `CosmeticComposition.Create(SaveStore, PurchaseService)`, production persistent service before screen composition, save-failure starter-only fallback, selected Home portrait, pause durability backstop, teardown.

- [ ] **Step 1: Write boot/Home tests before wiring**

Pin:

- `CosmeticComposition.Create` reports exact real catalogue counts;
- with a real test storage root, selecting/equipping survives destroy/relaunch;
- when storage construction is unavailable, Home/Wardrobe still receive a starter in-memory service;
- `GameRoot` installs `CosmeticRuntime` before `ComposeScreenFlow`;
- Home and Wardrobe show the same selected cat/effective layers;
- Home keeps exact `ParkedDistrictA/B/C` holder nodes and mounts the shared component inside `ParkedDistrictB`;
- Home's actual portrait transform is readable at 917x2048;
- `OnApplicationPause(true)` reaches the existing save pause-budget path;
- teardown removes static/runtime subscriptions.

- [ ] **Step 2: Run boot/Home tests and verify missing wiring**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD/unity" -runTests -testPlatform PlayMode \
  -testFilter 'CatMetro.Tests.PlayMode.HomeCosmeticPortraitTests|CatMetro.Tests.PlayMode.CosmeticBootWiringTests|CatMetro.Tests.PlayMode.WardrobeBootFlowTests' \
  -testResults /tmp/cm-cosmetics-boot-red.xml \
  -logFile /tmp/cm-cosmetics-boot-red.log
```

Expected: compile/test failures for the missing composition and Home portrait.

- [ ] **Step 3: Implement `CosmeticComposition` outside `GameRoot`**

`public static CosmeticProfileService Create(SaveStore saveStore, PurchaseService purchases)` must:

1. load `Cosmetics/portrait_assets` and `Cosmetics/cosmetic_catalog` as `TextAsset`;
2. parse inventory with `CosmeticPortraitPainter.SupportedRendererTokens`;
3. parse catalogue with the inventory's admitted asset/provenance IDs;
4. choose `SaveStoreCosmeticProfilePersistence` when `saveStore != null`, otherwise in-memory persistence;
5. construct `CosmeticProfileService`;
6. emit exactly one `CosmeticDiagnostics.OneLine` boot log;
7. when save startup is unavailable, retain the admitted Resources catalogue/inventory and return a starter-capable in-memory service;
8. when resource parsing fails, return a non-null empty/degraded service and report exact zero/rejection diagnostics rather than inventing an asset.

No second `SaveStore` is created here.

- [ ] **Step 4: Mount Home through the reusable component**

Extend the existing factory by adding the optional portrait source last, preserving all current call sites:

```csharp
public static HomeScreenView Create(
    Transform canvasParent,
    bool dailyEntryUnlocked = false,
    int lifetimeDailyCompletions = 0,
    ICosmeticPortraitSource portraitSource = null)
```

Keep `ParkedDistrictB` as the existing `Image` holder. Only when `portraitSource != null`, make the holder's own paint transparent and attach:

```csharp
view._profilePortrait = CosmeticPortraitView.Create(
    parkedDistrictB.transform, portraitSource, "HomeProfilePortrait");
```

Stretch the portrait root inside the holder and expose the actual `CosmeticPortraitView`/transform for tests. Direct Home tests that pass no source retain the existing fallback holder behavior.

- [ ] **Step 5: Apply the minimal `GameRoot` delta**

The only behavioral additions are:

```csharp
private CatMetro.Services.Cosmetics.CosmeticProfileService _cosmetics;
```

After each `InitializeDailyLiveServices()` call and before `ComposeScreenFlow()`:

```csharp
InitializeCosmetics();
```

Where:

```csharp
private void InitializeCosmetics()
{
    if (_cosmetics != null) return;
    _cosmetics = CosmeticComposition.Create(
        _saveStore, CatMetro.Services.Purchases.PurchaseRuntime.Current);
    CatMetro.Services.Cosmetics.CosmeticRuntime.Install(_cosmetics);
}
```

Replace the two composition calls exactly with:

```csharp
Home = CatMetro.Presentation.Screens.HomeScreenView.Create(
    canvasGo.transform, dailyUnlocked, LifetimeDailyCompletions, _cosmetics);
Wardrobe = CatMetro.Presentation.Screens.WardrobeScreenView.Create(
    canvasGo.transform,
    CatMetro.Services.Purchases.PurchaseRuntime.Current,
    _cosmetics,
    new CatMetro.Services.Cosmetics.DisabledCosmeticRewardedRoute());
```

The disabled rewarded route is only the typed no-offer default; it never grants access. In pause:

```csharp
if (pauseStatus)
{
    _saveStore?.TryCommitOnPause();
    _analyticsRuntime?.OnBackground();
}
```

In teardown, call `CatMetro.Services.Cosmetics.CosmeticRuntime.Uninstall(_cosmetics)`, then dispose `_cosmetics`. Do not edit any protected board/HUD owner.

- [ ] **Step 6: Run boot/Home/Wardrobe tests**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD/unity" -runTests -testPlatform PlayMode \
  -testFilter 'CatMetro.Tests.PlayMode.HomeCosmeticPortraitTests|CatMetro.Tests.PlayMode.CosmeticBootWiringTests|CatMetro.Tests.PlayMode.WardrobeBootFlowTests|CatMetro.Tests.PlayMode.HomeScreenStyleTests|CatMetro.Tests.PlayMode.WardrobePurchaseFlowTests' \
  -testResults /tmp/cm-cosmetics-boot-green.xml \
  -logFile /tmp/cm-cosmetics-boot-green.log
```

Expected: all focused composition/presentation tests pass.

- [ ] **Step 7: Record the literal zero-context `GameRoot.cs` hunk immediately**

Run:

```bash
git diff --unified=0 cfff675...HEAD -- unity/Assets/Scripts/Bootstrap/GameRoot.cs
```

Copy the complete output into the eventual PR body under `GameRoot.cs literal hunk`. Do not summarize it into line numbers.

- [ ] **Step 8: Commit Task 8**

```bash
git add unity/Assets/Scripts/Bootstrap/CosmeticComposition.cs \
  unity/Assets/Scripts/Bootstrap/CosmeticComposition.cs.meta \
  unity/Assets/Scripts/Presentation/Screens/HomeScreenView.cs \
  unity/Assets/Scripts/Bootstrap/GameRoot.cs \
  unity/Assets/Tests/PlayMode/Screens/HomeCosmeticPortraitTests.cs \
  unity/Assets/Tests/PlayMode/Screens/HomeCosmeticPortraitTests.cs.meta \
  unity/Assets/Tests/PlayMode/Bootstrap/CosmeticBootWiringTests.cs \
  unity/Assets/Tests/PlayMode/Bootstrap/CosmeticBootWiringTests.cs.meta \
  unity/Assets/Tests/PlayMode/Screens/HomeScreenStyleTests.cs \
  unity/Assets/Tests/PlayMode/Bootstrap/WardrobeBootFlowTests.cs
git commit -m "feat: compose persistent cosmetic portraits" \
  -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 9: Validate artifacts, prove offline cold boot, and open the gated PR

**Files:**
- Create from test capture: `docs/evidence/cosmetics/portrait-states.png`
- Create from target device: `docs/evidence/cosmetics/pixel-offline-cold-boot.png`
- Create from target-device filtered logs: `docs/evidence/cosmetics/pixel-offline-cold-boot.log`
- No source edits unless a validation failure identifies a real defect; return to that task's red/green cycle if so.

**Interfaces:**
- Consumes: completed feature, current `main`, existing conductor purchase baseline, target Pixel, RevenueCat cache, repository/Unity test harness.
- Produces: verified branch, exact PR handoff artifact, explicit unverified list, TASK 16 merge gate.

- [ ] **Step 1: Verify changed-file scope and protected-file boundary**

Run:

```bash
git status --short
git diff --name-only cfff675...HEAD
git diff --name-only cfff675...HEAD | grep -E \
  'BoardView\.cs|BoardSceneLook\.cs|BoardPropDecorator\.cs|ToyTrainView\.cs|ToyTrackMeshBuilder\.cs|WavePreviewStrip\.cs'
```

Expected: the final command prints nothing. Inspect every changed file; no unrelated main-checkout drift is staged.

- [ ] **Step 2: Run repository checks and the complete headless .NET suite**

Run:

```bash
bash scripts/check.sh
bash scripts/test.sh
dotnet restore dotnet/CatMetro.sln -p:RestoreLockedMode=true
dotnet test dotnet/CatMetro.sln --no-restore
```

Expected: every command exits 0. Record exact test totals from the dotnet output.

- [ ] **Step 3: Run full Unity EditMode without `-quit`**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD/unity" -runTests -testPlatform EditMode \
  -testResults /tmp/cm-cosmetics-full-edit.xml \
  -logFile /tmp/cm-cosmetics-full-edit.log
```

Expected: XML exists and reports zero failures. No console output for roughly 21 minutes is normal.

- [ ] **Step 4: Run full Unity PlayMode without `-quit`**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD/unity" -runTests -testPlatform PlayMode \
  -testResults /tmp/cm-cosmetics-full-play.xml \
  -logFile /tmp/cm-cosmetics-full-play.log
```

Expected: XML exists and reports zero failures.

- [ ] **Step 5: Build the dev APK without any store upload**

Run:

```bash
bash scripts/build-apk.sh
```

Expected: the local development APK is produced. Inspect Git status afterwards and leave Unity-generated settings drift unstaged.

- [ ] **Step 6: Resolve the real device before touching it**

Run:

```bash
adb devices -l
```

Proceed only if serial `48121FDAP006X4` reports the Pixel 9 Pro model. Stop if it is absent or the model does not match.

- [ ] **Step 7: Prove purchased profile/loadout persistence online**

First record the installed package path/version, then attempt an in-place update only on the verified Pixel:

```bash
adb -s 48121FDAP006X4 shell pm path com.catmetro.game
adb -s 48121FDAP006X4 shell dumpsys package com.catmetro.game \
  | grep -E 'versionCode=|versionName='
adb -s 48121FDAP006X4 install -r build/CatMetro-dev.apk
adb -s 48121FDAP006X4 shell monkey -p com.catmetro.game -c android.intent.category.LAUNCHER 1
```

Never uninstall the baseline app and never clear its data: either would destroy the exact local save/RevenueCat-cache precondition this proof is meant to exercise. If `install -r` reports a signature/version incompatibility, stop; the human must supply the feature build through the existing Play testing path. This lane does not upload it, and the device criterion stays explicitly unverified until that compatible build is installed.

With the existing real conductor entitlement baseline, open Wardrobe, select a non-default starter cat, equip Conductor's Coat, then:

```bash
adb -s 48121FDAP006X4 shell am force-stop com.catmetro.game
adb -s 48121FDAP006X4 shell monkey -p com.catmetro.game -c android.intent.category.LAUNCHER 1
```

Capture:

- the selected cat still selected;
- the desired conductor loadout present;
- the coat visibly painted;
- the one-line `COSMETICS` diagnostic counts.

This verifies Task 13 persistence; it does not repeat or take credit for the separate main-branch purchase baseline.

- [ ] **Step 8: Prove offline cold boot from RevenueCat's cache**

With the coat still equipped:

```bash
adb -s 48121FDAP006X4 logcat -c
adb -s 48121FDAP006X4 shell am force-stop com.catmetro.game
adb -s 48121FDAP006X4 shell cmd connectivity airplane-mode enable
adb -s 48121FDAP006X4 shell monkey -p com.catmetro.game -c android.intent.category.LAUNCHER 1
```

Wait for the first stable Wardrobe/Home portrait, capture the screen, and save only log lines matching `RevenueCat|Purchases|customerInfo|COSMETICS`. Confirm the coat is painted before any network response, then restore connectivity:

```bash
adb -s 48121FDAP006X4 exec-out screencap -p \
  > /tmp/pixel-offline-cold-boot.png
adb -s 48121FDAP006X4 logcat -d \
  | grep -E 'RevenueCat|Purchases|customerInfo|COSMETICS' \
  > /tmp/pixel-offline-cold-boot.log
adb -s 48121FDAP006X4 shell cmd connectivity airplane-mode disable
```

The log must contain `Vending customerInfo from cache` or the exact purchases-unity 9.9.0 equivalent. The app save supplies only the desired loadout; RevenueCat's cached `CustomerInfo` supplies paid authority. If that cache line or painted coat is absent, this criterion is unverified and the PR must not claim it. Restore airplane mode before any further diagnosis, including on a failed proof.

- [ ] **Step 9: Assemble committed evidence**

Create the contact sheet from plain/preview/purchased/lapsed/restored captures and add the Pixel offline screenshot/log. The portrait capture test emits `/tmp/cm-cosmetic-portraits/portrait-states.png`. Inspect the filtered log for API keys/tokens before copying:

```bash
mkdir -p docs/evidence/cosmetics
cp /tmp/cm-cosmetic-portraits/portrait-states.png \
  docs/evidence/cosmetics/portrait-states.png
cp /tmp/pixel-offline-cold-boot.png \
  docs/evidence/cosmetics/pixel-offline-cold-boot.png
cp /tmp/pixel-offline-cold-boot.log \
  docs/evidence/cosmetics/pixel-offline-cold-boot.log
```

The committed log contains only the filtered cache/diagnostic lines—no API keys, tokens, or unrelated device data.

```bash
git add docs/evidence/cosmetics/portrait-states.png \
  docs/evidence/cosmetics/pixel-offline-cold-boot.png \
  docs/evidence/cosmetics/pixel-offline-cold-boot.log
git commit -m "test: record cosmetic device evidence" \
  -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

If device evidence cannot be obtained, do not create invented files or this commit; list the exact missing proof in the PR.

- [ ] **Step 10: Re-run final verification after the evidence commit**

Run:

```bash
git diff --check cfff675...HEAD
bash scripts/check.sh
git status --short
git log --format='%h %s%n%b' cfff675..HEAD
```

Expected: diff check and repository check pass; worktree is clean; every task commit contains the required trailer.

- [ ] **Step 11: Create the literal PR handoff artifact**

Prepare `/tmp/catmetro-task13-pr.md` with:

1. feature summary;
2. exact test commands and totals;
3. device model/serial and offline-cache evidence links;
4. the complete output of:

```bash
git diff --unified=0 cfff675...HEAD -- unity/Assets/Scripts/Bootstrap/GameRoot.cs
```

5. an `Unverified evidence` list that explicitly names any missing iOS TestFlight cold-boot proof, missing device cache line, absent optional SKU/assets, and the fact that no store upload was performed;
6. `Merge gate: TASK 16 full Unity validation`.

- [ ] **Step 12: Push normally and open—do not merge—the PR**

Run:

```bash
git push
gh pr create --base main --head feat/cat-cosmetics \
  --title "feat: ship profile cat cosmetics" \
  --body-file /tmp/catmetro-task13-pr.md
```

Never force-push. Leave the PR open until TASK 16 validates the exact PR head with full Unity EditMode/PlayMode and posts a green merge gate.

---

## Final Acceptance Cross-Check

- Criteria 1-2: Tasks 1, 3, 4, and 8 prove three-cat selection plus restart persistence through the existing save file.
- Criteria 3-4: Tasks 4 and 7 prove complete locked preview and access-plus-atomic-commit equip.
- Criterion 5: Tasks 4 and 7 prove lapsed/refunded omission, unchanged desired ID, and automatic restoration.
- Criterion 6: Tasks 2, 5, and 7 prove missing asset/product candidate compaction.
- Criterion 7: Tasks 2, 5, and 8 prove exact admitted/rejected counts and the one-line runtime diagnostic.
- Criterion 8: Tasks 6 and 8 prove one portrait component in Wardrobe/Home plus the typed HUD mounting seam without touching its owner.
- Criterion 9: Task 9 requires a real Pixel offline-cold-boot painted coat backed by cached RevenueCat `CustomerInfo`.
- Criterion 10: Tasks 1 and 3 deep-compare complete `localLeases` and unknown data across migration/mutation.
- Criterion 11: Tasks 6, 8, and 9 prove the protected-file boundary from the actual branch diff.
- Criterion 12: Task 9 puts the literal zero-context `GameRoot.cs` hunk and unverified-evidence list in the normal PR path, with TASK 16 as merge gate.
