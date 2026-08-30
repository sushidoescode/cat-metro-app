using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatMetro.Services.Cosmetics;
using CatMetro.Services.Purchases;
using CatMetro.Tests.Purchases;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CatMetro.Tests.Cosmetics
{
    public sealed class CosmeticWardrobeProjectionTests
    {
        private static readonly string[] SupportedRendererTokens =
        {
            "cat.red_tabby", "cat.blue_siamese", "cat.yellow_longhair",
            "outfit.conductor", "frame.brass", "frame.lantern",
        };

        private readonly List<CosmeticProfileService> _profiles =
            new List<CosmeticProfileService>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _profiles.Count; i++) _profiles[i].Dispose();
            _profiles.Clear();
        }

        [Test]
        public void AccessibleEquippedRowStaysVisibleWhenItsProductPriceDisappears()
        {
            var inventory = ShippedInventory();
            var catalog = ShippedCatalog(inventory);
            var purchases = CreatePurchases();
            purchases.Ledger.ReplaceStoreGrants(new[]
            {
                new EntitlementGrant("outfit_conductor", GrantSource.Store),
            });
            var profile = CreateProfile(catalog, inventory, purchases,
                ProfileWith("outfit_conductor"));

            var rows = CosmeticWardrobeProjection.Build(catalog, profile, purchases,
                new DisabledCosmeticRewardedRoute(), "red_tabby", CosmeticSlot.Outfit);

            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].Item.Id, Is.EqualTo("outfit_conductor"));
            Assert.That(rows[0].IsAccessible, Is.True);
            Assert.That(rows[0].IsEquipped, Is.True);
            Assert.That(rows[0].Route, Is.EqualTo(CosmeticWardrobeRoute.None));
            Assert.That(rows[0].Price.IsKnown, Is.False);
        }

        [Test]
        public void AccessibleUnequippedPermanentRowUsesEquipRoute()
        {
            var inventory = ShippedInventory();
            var catalog = ShippedCatalog(inventory);
            var purchases = CreatePurchases();
            purchases.Ledger.ReplaceStoreGrants(new[]
            {
                new EntitlementGrant("outfit_conductor", GrantSource.Store),
            });
            var profile = CreateProfile(catalog, inventory, purchases, DefaultProfile());

            var rows = CosmeticWardrobeProjection.Build(catalog, profile, purchases,
                new DisabledCosmeticRewardedRoute(), "red_tabby", CosmeticSlot.Outfit);

            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].Route, Is.EqualTo(CosmeticWardrobeRoute.Equip));
            Assert.That(rows[0].SecondsRemaining, Is.Zero);
        }

        [Test]
        public void LockedEntitlementWithNeitherPriceNorDeclaredRewardedRouteIsAbsent()
        {
            var inventory = ShippedInventory();
            var catalog = ShippedCatalog(inventory);
            var purchases = CreatePurchases();
            var profile = CreateProfile(catalog, inventory, purchases, DefaultProfile());

            var rows = CosmeticWardrobeProjection.Build(catalog, profile, purchases,
                new OfferAllRewardedRoute(), "red_tabby", CosmeticSlot.Outfit);

            Assert.That(rows, Is.Empty,
                "an offer provider cannot invent a placement absent from the admitted row");
        }

        [Test]
        public void LockedEntitlementWithLocalizedPriceUsesPurchaseBeforeRewarded()
        {
            var inventory = ShippedInventory();
            var root = ShippedCatalogRoot();
            Item(root, "outfit_conductor")["rewardedPlacementId"] = "wardrobe.conductor";
            var catalog = ParseCatalog(root, inventory);
            var purchases = CreatePurchases("cm_outfit_conductor", "€1.99");
            var profile = CreateProfile(catalog, inventory, purchases, DefaultProfile());

            var rows = CosmeticWardrobeProjection.Build(catalog, profile, purchases,
                new OfferAllRewardedRoute(), "red_tabby", CosmeticSlot.Outfit);

            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].IsAccessible, Is.False);
            Assert.That(rows[0].Route, Is.EqualTo(CosmeticWardrobeRoute.Purchase));
            Assert.That(rows[0].Price.DisplayText, Is.EqualTo("€1.99"));
        }

        [Test]
        public void LockedEntitlementWithNoPriceAndOfferableDeclaredPlacementUsesRewarded()
        {
            var inventory = ShippedInventory();
            var root = ShippedCatalogRoot();
            Item(root, "outfit_conductor")["rewardedPlacementId"] = "wardrobe.conductor";
            var catalog = ParseCatalog(root, inventory);
            var purchases = CreatePurchases();
            var profile = CreateProfile(catalog, inventory, purchases, DefaultProfile());
            var rewarded = new SelectiveRewardedRoute("wardrobe.conductor",
                "outfit_conductor");

            var rows = CosmeticWardrobeProjection.Build(catalog, profile, purchases,
                rewarded, "red_tabby", CosmeticSlot.Outfit);

            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].Route, Is.EqualTo(CosmeticWardrobeRoute.Rewarded));
            Assert.That(rows[0].Price.IsKnown, Is.False);
        }

        [Test]
        public void LockedEarnedRowUsesItsDeterministicEarnInstructionRoute()
        {
            var inventory = ShippedInventory();
            var root = ShippedCatalogRoot();
            MakeEarned(Item(root, "frame_lantern"), "cosmetics.earn.finish_line_12");
            var catalog = ParseCatalog(root, inventory);
            var purchases = CreatePurchases();
            var profile = CreateProfile(catalog, inventory, purchases, DefaultProfile());

            var rows = CosmeticWardrobeProjection.Build(catalog, profile, purchases,
                new DisabledCosmeticRewardedRoute(), "red_tabby", CosmeticSlot.Frame);

            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].Item.Id, Is.EqualTo("frame_lantern"));
            Assert.That(rows[0].Item.EarnInstructionKey,
                Is.EqualTo("cosmetics.earn.finish_line_12"));
            Assert.That(rows[0].Route, Is.EqualTo(CosmeticWardrobeRoute.EarnInstruction));
        }

        [Test]
        public void StaticAndCompatibilityOmissionsLeaveCatalogueOrderContiguous()
        {
            var inventory = ShippedInventory();
            var root = ShippedCatalogRoot();
            var conductor = Item(root, "outfit_conductor");
            conductor["slot"] = "frame";
            MakeEarned(conductor, "cosmetics.earn.conductor");

            Item(root, "frame_brass")["portraitAssetId"] = "frame.missing";
            var lantern = Item(root, "frame_lantern");
            MakeEarned(lantern, "cosmetics.earn.lantern");
            lantern["compatibleCatIds"] = new JArray("red_tabby");

            var tail = (JObject)lantern.DeepClone();
            tail["id"] = "frame_tail";
            tail["compatibleCatIds"] = new JArray("blue_siamese");
            tail["order"] = 40;
            ((JArray)root["items"]).Add(tail);

            var catalog = ParseCatalog(root, inventory);
            var purchases = CreatePurchases();
            var profile = CreateProfile(catalog, inventory, purchases, DefaultProfile());

            var rows = CosmeticWardrobeProjection.Build(catalog, profile, purchases,
                new DisabledCosmeticRewardedRoute(), "blue_siamese", CosmeticSlot.Frame);

            Assert.That(catalog.RejectedRowCount, Is.EqualTo(1));
            Assert.That(catalog.TryGetItem("frame_brass", out _), Is.False);
            Assert.That(catalog.TryGetItem("frame_lantern", out _), Is.True);
            CollectionAssert.AreEqual(new[] { "outfit_conductor", "frame_tail" },
                rows.Select(row => row.Item.Id).ToArray());
            Assert.That(rows[0].Item.Order, Is.EqualTo(10));
            Assert.That(rows[1].Item.Order, Is.EqualTo(40));
        }

        [Test]
        public void AccessibleTemporaryEntitlementReportsCountdownAndRetainsDirectPurchase()
        {
            var inventory = ShippedInventory();
            var catalog = ShippedCatalog(inventory);
            var purchases = CreatePurchases("cm_outfit_conductor", "¥1.99");
            Assert.That(purchases.GrantRewardedAdEntitlement("outfit_conductor"),
                Is.EqualTo(AdGrantOutcome.Granted));
            var profile = CreateProfile(catalog, inventory, purchases, DefaultProfile());

            var rows = CosmeticWardrobeProjection.Build(catalog, profile, purchases,
                new DisabledCosmeticRewardedRoute(), "red_tabby", CosmeticSlot.Outfit);

            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].IsAccessible, Is.True);
            Assert.That(rows[0].SecondsRemaining, Is.GreaterThan(0));
            Assert.That(rows[0].Route, Is.EqualTo(CosmeticWardrobeRoute.Purchase));
            Assert.That(rows[0].Price.DisplayText, Is.EqualTo("¥1.99"));
        }

        [Test]
        public void DisabledRewardedRouteCompletesWithoutGrantingSharedAccess()
        {
            var purchases = CreatePurchases();
            var route = new DisabledCosmeticRewardedRoute();
            int completions = 0;

            Assert.That(route.CanOffer("wardrobe.conductor", "outfit_conductor"), Is.False);
            route.Request("wardrobe.conductor", () => completions++);

            Assert.That(completions, Is.EqualTo(1));
            Assert.That(purchases.IsUnlocked("outfit_conductor"), Is.False);
        }

        [Test]
        public void DiagnosticsFormatsExactStaticDynamicAndConductorReadbacks()
        {
            var inventory = ShippedInventory();
            var catalog = ShippedCatalog(inventory);
            var purchases = CreatePurchases("cm_outfit_conductor", "$1.99");
            var profile = CreateProfile(catalog, inventory, purchases, DefaultProfile());
            var rows = CosmeticWardrobeProjection.Build(catalog, profile, purchases,
                new DisabledCosmeticRewardedRoute(), "red_tabby", CosmeticSlot.Outfit);
            int purchasable = rows.Count(row => row.Route == CosmeticWardrobeRoute.Purchase);
            bool conductorReady = catalog.TryGetItem("outfit_conductor", out var conductor)
                && inventory.TryGet(conductor.PortraitAssetId, out _)
                && (profile.IsAccessible(conductor.Id)
                    || purchases.TryGetPrice(conductor.ProductId, out _));

            var diagnostic = CosmeticDiagnostics.OneLine(catalog, inventory, rows.Count,
                purchasable, conductorReady);

            Assert.That(diagnostic, Is.EqualTo(
                "COSMETICS admittedRows=3 rejectedRows=0 admittedCats=3 " +
                "assetReadyRows=3 visibleRows=1 purchasableRows=1 conductorReady=true"));
        }

        [Test]
        public void DiagnosticsMeasuresAssetReadyAgainstSuppliedInventoryNotAuthoredRows()
        {
            var fullInventory = ShippedInventory();
            var catalog = ShippedCatalog(fullInventory);
            var inventoryRoot = ShippedInventoryRoot();
            Asset(inventoryRoot, "frame.brass")["rendererToken"] = "frame.unsupported";
            var sparseInventory = CosmeticAssetInventory.Parse(inventoryRoot.ToString(),
                SupportedRendererTokens);

            var diagnostic = CosmeticDiagnostics.OneLine(catalog, sparseInventory, 0, 0,
                false);

            Assert.That(catalog.AdmittedRowCount, Is.EqualTo(3));
            Assert.That(sparseInventory.TryGet("frame.brass", out _), Is.False);
            Assert.That(diagnostic, Is.EqualTo(
                "COSMETICS admittedRows=3 rejectedRows=0 admittedCats=3 " +
                "assetReadyRows=2 visibleRows=0 purchasableRows=0 conductorReady=false"));
        }

        private CosmeticProfileService CreateProfile(CosmeticCatalog catalog,
            CosmeticAssetInventory inventory, PurchaseService purchases,
            CosmeticProfileSnapshot snapshot)
        {
            var profile = new CosmeticProfileService(catalog, inventory,
                new InMemoryCosmeticProfilePersistence(snapshot), purchases);
            _profiles.Add(profile);
            return profile;
        }

        private static PurchaseService CreatePurchases(string productId = null,
            string localizedPrice = null)
        {
            var catalog = PurchaseCatalog.Parse(File.ReadAllText(PurchaseCatalogPath()));
            var backend = new FakePurchaseBackend { GrantOnPurchase = catalog.EntitlementsFor };
            if (productId != null) backend.WithProduct(productId, productId, localizedPrice);
            var purchases = new PurchaseService(catalog, backend, () => 1_700_000_000L,
                new EntitlementLedger());
            if (productId != null) purchases.Refresh();
            return purchases;
        }

        private static CosmeticProfileSnapshot DefaultProfile() =>
            new CosmeticProfileSnapshot("red_tabby", null, null, new[]
            {
                new CosmeticLoadout("red_tabby", "", "", ""),
                new CosmeticLoadout("blue_siamese", "", "", ""),
            });

        private static CosmeticProfileSnapshot ProfileWith(string outfitId) =>
            new CosmeticProfileSnapshot("red_tabby", null, null, new[]
            {
                new CosmeticLoadout("red_tabby", outfitId, "", ""),
            });

        private static CosmeticAssetInventory ShippedInventory() =>
            CosmeticAssetInventory.Parse(File.ReadAllText(InventoryPath()),
                SupportedRendererTokens);

        private static CosmeticCatalog ShippedCatalog(CosmeticAssetInventory inventory) =>
            CosmeticCatalog.Parse(File.ReadAllText(CatalogPath()), inventory.AssetIds,
                inventory.ProvenanceAssetIds);

        private static CosmeticCatalog ParseCatalog(JObject root,
            CosmeticAssetInventory inventory) => CosmeticCatalog.Parse(root.ToString(),
                inventory.AssetIds, inventory.ProvenanceAssetIds);

        private static JObject ShippedCatalogRoot() => JObject.Parse(
            File.ReadAllText(CatalogPath()));

        private static JObject ShippedInventoryRoot() => JObject.Parse(
            File.ReadAllText(InventoryPath()));

        private static JObject Item(JObject root, string id) =>
            ((JArray)root["items"]).OfType<JObject>()
                .Single(row => (string)row["id"] == id);

        private static JObject Asset(JObject root, string id) =>
            ((JArray)root["assets"]).OfType<JObject>()
                .Single(row => (string)row["assetId"] == id);

        private static void MakeEarned(JObject item, string instructionKey)
        {
            item["acquisition"] = "earned";
            item.Remove("entitlementId");
            item.Remove("productId");
            item.Remove("rewardedPlacementId");
            item["earnInstructionKey"] = instructionKey;
        }

        private static string CatalogPath() => Path.Combine(RepoRoot(), "unity", "Assets",
            "Resources", "Cosmetics", "cosmetic_catalog.json");

        private static string InventoryPath() => Path.Combine(RepoRoot(), "unity", "Assets",
            "Resources", "Cosmetics", "portrait_assets.json");

        private static string PurchaseCatalogPath() => Path.Combine(RepoRoot(), "unity", "Assets",
            "Resources", "Monetization", "product_catalog.json");

        private static string RepoRoot() => CatMetro.Tests.Domain.Fixtures.RepoRoot();

        private sealed class OfferAllRewardedRoute : ICosmeticRewardedRoute
        {
            public bool CanOffer(string placementId, string entitlementId) => true;
            public void Request(string placementId, Action completed) => completed?.Invoke();
        }

        private sealed class SelectiveRewardedRoute : ICosmeticRewardedRoute
        {
            private readonly string _placementId;
            private readonly string _entitlementId;

            public SelectiveRewardedRoute(string placementId, string entitlementId)
            {
                _placementId = placementId;
                _entitlementId = entitlementId;
            }

            public bool CanOffer(string placementId, string entitlementId) =>
                string.Equals(placementId, _placementId, StringComparison.Ordinal)
                && string.Equals(entitlementId, _entitlementId, StringComparison.Ordinal);

            public void Request(string placementId, Action completed) => completed?.Invoke();
        }
    }
}
