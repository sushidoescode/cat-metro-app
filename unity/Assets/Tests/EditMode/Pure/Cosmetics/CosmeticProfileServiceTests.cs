using System;
using System.Collections.Generic;
using CatMetro.Services.Cosmetics;
using CatMetro.Services.Purchases;
using CatMetro.Tests.Purchases;
using NUnit.Framework;

namespace CatMetro.Tests.Cosmetics
{
    public sealed class CosmeticProfileServiceTests
    {
        private const string CatalogJson = @"{
          ""schemaVersion"": 1,
          ""cats"": [
            { ""id"": ""red_tabby"", ""displayNameKey"": ""cat.red"", ""portraitAssetId"": ""cat.red"", ""starter"": true },
            { ""id"": ""blue_siamese"", ""displayNameKey"": ""cat.blue"", ""portraitAssetId"": ""cat.blue"", ""starter"": true },
            { ""id"": ""yellow_longhair"", ""displayNameKey"": ""cat.yellow"", ""portraitAssetId"": ""cat.yellow"", ""starter"": true },
            { ""id"": ""green_shorthair"", ""displayNameKey"": ""cat.green"", ""portraitAssetId"": ""cat.green"", ""starter"": false }
          ],
          ""items"": [
            {
              ""id"": ""outfit_conductor"", ""slot"": ""outfit"", ""displayNameKey"": ""item.conductor"",
              ""portraitAssetId"": ""outfit.conductor"", ""acquisition"": ""entitlement"",
              ""entitlementId"": ""outfit_conductor"", ""productId"": ""cm_outfit_conductor"",
              ""compatibleCatIds"": [""red_tabby"", ""blue_siamese"", ""yellow_longhair"", ""green_shorthair""], ""order"": 10
            },
            {
              ""id"": ""accessory_scarf"", ""slot"": ""accessory"", ""displayNameKey"": ""item.scarf"",
              ""portraitAssetId"": ""accessory.scarf"", ""acquisition"": ""earned"",
              ""earnInstructionKey"": ""earn.scarf"",
              ""compatibleCatIds"": [""red_tabby"", ""blue_siamese"", ""yellow_longhair"", ""green_shorthair""], ""order"": 20
            },
            {
              ""id"": ""accessory_bow"", ""slot"": ""accessory"", ""displayNameKey"": ""item.bow"",
              ""portraitAssetId"": ""accessory.bow"", ""acquisition"": ""starter"",
              ""compatibleCatIds"": [""red_tabby"", ""blue_siamese"", ""yellow_longhair"", ""green_shorthair""], ""order"": 30
            },
            {
              ""id"": ""frame_plain"", ""slot"": ""frame"", ""displayNameKey"": ""item.frame_plain"",
              ""portraitAssetId"": ""frame.plain"", ""acquisition"": ""starter"",
              ""compatibleCatIds"": [""red_tabby"", ""blue_siamese"", ""yellow_longhair"", ""green_shorthair""], ""order"": 40
            },
            {
              ""id"": ""frame_red_only"", ""slot"": ""frame"", ""displayNameKey"": ""item.frame_red"",
              ""portraitAssetId"": ""frame.red"", ""acquisition"": ""entitlement"",
              ""entitlementId"": ""frame_red"", ""productId"": ""cm_frame_red"",
              ""compatibleCatIds"": [""red_tabby""], ""order"": 50
            }
          ]
        }";

        private const string InventoryJson = @"{
          ""schemaVersion"": 1,
          ""assets"": [
            { ""assetId"": ""cat.red"", ""rendererToken"": ""cat.red"", ""provenanceId"": ""p.red"" },
            { ""assetId"": ""cat.blue"", ""rendererToken"": ""cat.blue"", ""provenanceId"": ""p.blue"" },
            { ""assetId"": ""cat.yellow"", ""rendererToken"": ""cat.yellow"", ""provenanceId"": ""p.yellow"" },
            { ""assetId"": ""cat.green"", ""rendererToken"": ""cat.green"", ""provenanceId"": ""p.green"" },
            { ""assetId"": ""outfit.conductor"", ""rendererToken"": ""outfit.conductor"", ""provenanceId"": ""p.conductor"" },
            { ""assetId"": ""accessory.scarf"", ""rendererToken"": ""accessory.scarf"", ""provenanceId"": ""p.scarf"" },
            { ""assetId"": ""accessory.bow"", ""rendererToken"": ""accessory.bow"", ""provenanceId"": ""p.bow"" },
            { ""assetId"": ""frame.plain"", ""rendererToken"": ""frame.plain"", ""provenanceId"": ""p.frame_plain"" },
            { ""assetId"": ""frame.red"", ""rendererToken"": ""frame.red"", ""provenanceId"": ""p.frame_red"" }
          ],
          ""provenance"": [
            { ""id"": ""p.red"", ""sourceKind"": ""project_authored"", ""sourcePath"": ""red"", ""commercialDistribution"": ""cleared"" },
            { ""id"": ""p.blue"", ""sourceKind"": ""project_authored"", ""sourcePath"": ""blue"", ""commercialDistribution"": ""cleared"" },
            { ""id"": ""p.yellow"", ""sourceKind"": ""project_authored"", ""sourcePath"": ""yellow"", ""commercialDistribution"": ""cleared"" },
            { ""id"": ""p.green"", ""sourceKind"": ""project_authored"", ""sourcePath"": ""green"", ""commercialDistribution"": ""cleared"" },
            { ""id"": ""p.conductor"", ""sourceKind"": ""project_authored"", ""sourcePath"": ""conductor"", ""commercialDistribution"": ""cleared"" },
            { ""id"": ""p.scarf"", ""sourceKind"": ""project_authored"", ""sourcePath"": ""scarf"", ""commercialDistribution"": ""cleared"" },
            { ""id"": ""p.bow"", ""sourceKind"": ""project_authored"", ""sourcePath"": ""bow"", ""commercialDistribution"": ""cleared"" },
            { ""id"": ""p.frame_plain"", ""sourceKind"": ""project_authored"", ""sourcePath"": ""frame_plain"", ""commercialDistribution"": ""cleared"" },
            { ""id"": ""p.frame_red"", ""sourceKind"": ""project_authored"", ""sourcePath"": ""frame_red"", ""commercialDistribution"": ""cleared"" }
          ]
        }";

        private const string PurchaseCatalogJson = @"{
          ""schemaVersion"": 2,
          ""entitlements"": [
            { ""id"": ""outfit_conductor"", ""kind"": ""outfit"", ""display"": ""Conductor"", ""adLeaseSeconds"": 60 },
            { ""id"": ""frame_red"", ""kind"": ""frame"", ""display"": ""Red Frame"", ""adLeaseSeconds"": 0 },
            { ""id"": ""supporter"", ""kind"": ""membership"", ""display"": ""Supporter"", ""adLeaseSeconds"": 0 }
          ],
          ""products"": [
            { ""id"": ""cm_outfit_conductor"", ""storeType"": ""non_consumable"", ""display"": ""Conductor"", ""entitlements"": [""outfit_conductor""] },
            { ""id"": ""cm_frame_red"", ""storeType"": ""non_consumable"", ""display"": ""Red Frame"", ""entitlements"": [""frame_red""] }
          ]
        }";

        private static readonly string[] RendererTokens =
        {
            "cat.red", "cat.blue", "cat.yellow", "cat.green", "outfit.conductor",
            "accessory.scarf", "accessory.bow", "frame.plain", "frame.red",
        };

        private readonly List<CosmeticProfileService> _services =
            new List<CosmeticProfileService>();

        [SetUp]
        public void SetUp()
        {
            PurchaseRuntime.ResetForTests();
            CosmeticRuntime.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _services.Count; i++) _services[i].Dispose();
            _services.Clear();
            CosmeticRuntime.ResetForTests();
            PurchaseRuntime.ResetForTests();
        }

        [TestCase("red_tabby")]
        [TestCase("blue_siamese")]
        [TestCase("yellow_longhair")]
        public void EachAdmittedStarterCat_IsSelectable(string catId)
        {
            var persistence = new RecordingPersistence(DefaultProfile());
            var service = CreateService(persistence);

            Assert.That(service.TrySelectCat(catId), Is.True);
            Assert.That(service.SelectedCatId, Is.EqualTo(catId));
            Assert.That(service.Profile.SelectedCatId, Is.EqualTo(catId));
            Assert.That(service.CurrentPortrait.CatId, Is.EqualTo(catId));
        }

        [Test]
        public void SelectionAndEquip_PersistBeforePublishingChanged()
        {
            var order = new List<string>();
            var purchase = CreatePurchases();
            var persistence = new RecordingPersistence(DefaultProfile(), order);
            var service = CreateService(persistence, purchase.Service);
            service.Changed += () => order.Add("changed");

            Assert.That(service.TrySelectCat("blue_siamese"), Is.True);
            CollectionAssert.AreEqual(new[] { "persist", "changed" }, order);

            order.Clear();
            purchase.Ledger.ReplaceStoreGrants(new[]
            {
                new EntitlementGrant("outfit_conductor", GrantSource.Store),
            });
            order.Clear();
            Assert.That(service.TryEquip("blue_siamese", CosmeticSlot.Outfit,
                "outfit_conductor"), Is.True);
            CollectionAssert.AreEqual(new[] { "persist", "changed" }, order);
            Assert.That(service.CurrentPortrait.OutfitAssetId, Is.EqualTo("outfit.conductor"));
        }

        [TestCase(PersistenceFailure.Refuse)]
        [TestCase(PersistenceFailure.Throw)]
        public void FailedPersistence_LeavesAllObservableProfileStateUnpublished(
            PersistenceFailure failure)
        {
            var purchase = CreatePurchases();
            purchase.Ledger.ReplaceStoreGrants(new[]
            {
                new EntitlementGrant("outfit_conductor", GrantSource.Store),
            });
            var persistence = new RecordingPersistence(DefaultProfile()) { Failure = failure };
            var service = CreateService(persistence, purchase.Service);
            var originalProfile = service.Profile;
            var originalPortrait = service.CurrentPortrait;
            var originalLoadout = service.Profile.LoadoutFor("red_tabby");
            int changed = 0;
            service.Changed += () => changed++;

            Assert.That(service.TrySelectCat("blue_siamese"), Is.False);
            Assert.That(ReferenceEquals(service.Profile, originalProfile), Is.True);
            Assert.That(service.SelectedCatId, Is.EqualTo("red_tabby"));
            Assert.That(service.CurrentPortrait, Is.EqualTo(originalPortrait));
            Assert.That(changed, Is.Zero);

            Assert.That(service.TryEquip("red_tabby", CosmeticSlot.Outfit,
                "outfit_conductor"), Is.False);
            Assert.That(ReferenceEquals(service.Profile, originalProfile), Is.True);
            Assert.That(service.Profile.LoadoutFor("red_tabby").OutfitId,
                Is.EqualTo(originalLoadout.OutfitId));
            Assert.That(service.CurrentPortrait, Is.EqualTo(originalPortrait));
            Assert.That(changed, Is.Zero);
        }

        [Test]
        public void EarnedGrantRoutes_AcceptOnlyDeclaredEarnedRowsAndNonStarterCats()
        {
            var persistence = new RecordingPersistence(DefaultProfile());
            var service = CreateService(persistence);

            Assert.That(service.TryGrantEarnedItem("outfit_conductor"), Is.False,
                "entitlement intent cannot become local ownership");
            Assert.That(service.TryGrantEarnedItem("accessory_bow"), Is.False,
                "starter rows are not earned grants");
            Assert.That(service.TryGrantEarnedItem("missing"), Is.False);
            Assert.That(service.TryGrantEarnedItem("accessory_scarf"), Is.True);
            CollectionAssert.AreEqual(new[] { "accessory_scarf" }, service.Profile.EarnedItemIds);

            Assert.That(service.TryGrantEarnedCat("missing"), Is.False);
            Assert.That(service.TryGrantEarnedCat("red_tabby"), Is.False,
                "starter cats are directly available, never locally earned");
            Assert.That(service.TryGrantEarnedCat("green_shorthair"), Is.True);
            CollectionAssert.AreEqual(new[] { "green_shorthair" }, service.Profile.EarnedCatIds);
        }

        [Test]
        public void Equip_UsesTheRealPurchaseServiceForStoreAndRewardedConvergence()
        {
            var purchase = CreatePurchases();
            var persistence = new RecordingPersistence(DefaultProfile());
            var service = CreateService(persistence, purchase.Service);

            Assert.That(service.IsAccessible("outfit_conductor"), Is.False);
            Assert.That(service.TryEquip("red_tabby", CosmeticSlot.Outfit,
                "outfit_conductor"), Is.False);
            Assert.That(persistence.ReplaceCalls, Is.Zero);

            purchase.Ledger.ReplaceStoreGrants(new[]
            {
                new EntitlementGrant("outfit_conductor", GrantSource.Store),
            });
            Assert.That(service.IsAccessible("outfit_conductor"), Is.True);
            Assert.That(service.TryEquip("red_tabby", CosmeticSlot.Outfit,
                "outfit_conductor"), Is.True);

            Assert.That(service.TryUnequip("red_tabby", CosmeticSlot.Outfit), Is.True);
            purchase.Ledger.ReplaceStoreGrants(Array.Empty<EntitlementGrant>());
            Assert.That(purchase.Service.GrantRewardedAdEntitlement("outfit_conductor"),
                Is.EqualTo(AdGrantOutcome.Granted));
            Assert.That(service.IsAccessible("outfit_conductor"), Is.True);
            Assert.That(service.TryEquip("red_tabby", CosmeticSlot.Outfit,
                "outfit_conductor"), Is.True);
        }

        [Test]
        public void LockedKnownCompatibleItem_HasACompletePreviewButCannotEquip()
        {
            var initial = new CosmeticProfileSnapshot("red_tabby", null, null, new[]
            {
                new CosmeticLoadout("red_tabby", "", "accessory_bow", "frame_plain"),
            });
            var persistence = new RecordingPersistence(initial);
            var service = CreateService(persistence);

            var preview = service.PreviewPortrait("red_tabby", CosmeticSlot.Outfit,
                "outfit_conductor");

            Assert.That(preview.CatId, Is.EqualTo("red_tabby"));
            Assert.That(preview.BaseAssetId, Is.EqualTo("cat.red"));
            Assert.That(preview.OutfitAssetId, Is.EqualTo("outfit.conductor"));
            Assert.That(preview.AccessoryAssetId, Is.EqualTo("accessory.bow"));
            Assert.That(preview.FrameAssetId, Is.EqualTo("frame.plain"));
            Assert.That(service.TryEquip("red_tabby", CosmeticSlot.Outfit,
                "outfit_conductor"), Is.False);
            Assert.That(service.Profile.LoadoutFor("red_tabby").OutfitId, Is.Empty);
            Assert.That(persistence.ReplaceCalls, Is.Zero);
        }

        [Test]
        public void StoreRefundAndRestore_OmitAndReturnOnlyTheEffectiveLayerWithoutPersistence()
        {
            var purchase = CreatePurchases();
            purchase.Backend.WithEntitlement("outfit_conductor");
            purchase.Service.RefreshEntitlements();
            var initial = ProfileWithOutfit("outfit_conductor");
            var persistence = new RecordingPersistence(initial);
            var service = CreateService(persistence, purchase.Service);
            int changed = 0;
            service.Changed += () => changed++;

            Assert.That(service.CurrentPortrait.OutfitAssetId, Is.EqualTo("outfit.conductor"));
            purchase.Backend.RevokeAll();
            purchase.Service.RefreshEntitlements();

            Assert.That(service.CurrentPortrait.OutfitAssetId, Is.Empty);
            Assert.That(service.Profile.LoadoutFor("red_tabby").OutfitId,
                Is.EqualTo("outfit_conductor"));
            Assert.That(persistence.ReplaceCalls, Is.Zero);
            Assert.That(changed, Is.EqualTo(1));

            purchase.Backend.RestoreGrants = new[] { "outfit_conductor" };
            purchase.Service.Restore(_ => { });

            Assert.That(service.CurrentPortrait.OutfitAssetId, Is.EqualTo("outfit.conductor"));
            Assert.That(service.Profile.LoadoutFor("red_tabby").OutfitId,
                Is.EqualTo("outfit_conductor"));
            Assert.That(persistence.ReplaceCalls, Is.Zero);
            Assert.That(changed, Is.EqualTo(2));
        }

        [Test]
        public void RewardedLeaseExpiry_OmitsOnlyTheLayerAndNeverPersists()
        {
            var purchase = CreatePurchases();
            var persistence = new RecordingPersistence(ProfileWithOutfit("outfit_conductor"));
            var service = CreateService(persistence, purchase.Service);
            int changed = 0;
            service.Changed += () => changed++;

            Assert.That(purchase.Service.GrantRewardedAdEntitlement("outfit_conductor"),
                Is.EqualTo(AdGrantOutcome.Granted));
            Assert.That(service.CurrentPortrait.OutfitAssetId, Is.EqualTo("outfit.conductor"));
            Assert.That(persistence.ReplaceCalls, Is.Zero);

            purchase.Clock.Advance(60);
            Assert.That(purchase.Service.PruneExpiredLeases(), Is.True);

            Assert.That(service.CurrentPortrait.OutfitAssetId, Is.Empty);
            Assert.That(service.Profile.LoadoutFor("red_tabby").OutfitId,
                Is.EqualTo("outfit_conductor"));
            Assert.That(persistence.ReplaceCalls, Is.Zero);
            Assert.That(changed, Is.EqualTo(2));
        }

        [Test]
        public void UnknownAndIncompatibleSavedIds_RemainRawButOmitFromEffectivePortrait()
        {
            var purchase = CreatePurchases();
            purchase.Ledger.ReplaceStoreGrants(new[]
            {
                new EntitlementGrant("frame_red", GrantSource.Store),
            });
            var initial = new CosmeticProfileSnapshot("blue_siamese", null, null, new[]
            {
                new CosmeticLoadout("blue_siamese", "outfit_future", "", "frame_red_only"),
            });
            var persistence = new RecordingPersistence(initial);
            var service = CreateService(persistence, purchase.Service);

            Assert.That(service.Profile.LoadoutFor("blue_siamese").OutfitId,
                Is.EqualTo("outfit_future"));
            Assert.That(service.Profile.LoadoutFor("blue_siamese").FrameId,
                Is.EqualTo("frame_red_only"));
            Assert.That(service.CurrentPortrait.BaseAssetId, Is.EqualTo("cat.blue"));
            Assert.That(service.CurrentPortrait.OutfitAssetId, Is.Empty);
            Assert.That(service.CurrentPortrait.FrameAssetId, Is.Empty);
            Assert.That(persistence.ReplaceCalls, Is.Zero);
        }

        [Test]
        public void UnknownSavedSelectedCat_RemainsRawWhileEffectiveSelectionUsesFirstStarter()
        {
            var initial = new CosmeticProfileSnapshot("future_cat", null, null, new[]
            {
                new CosmeticLoadout("future_cat", "future_outfit", "", ""),
            });
            var persistence = new RecordingPersistence(initial);
            var service = CreateService(persistence);

            Assert.That(service.Profile.SelectedCatId, Is.EqualTo("future_cat"));
            Assert.That(service.SelectedCatId, Is.EqualTo("red_tabby"));
            Assert.That(service.CurrentPortrait.CatId, Is.EqualTo("red_tabby"));
            Assert.That(service.CurrentPortrait.BaseAssetId, Is.EqualTo("cat.red"));
            Assert.That(persistence.ReplaceCalls, Is.Zero);
        }

        [Test]
        public void FailedInitialLoad_BuildsAStarterDefaultInMemoryWithoutWriting()
        {
            var persistence = new RecordingPersistence(DefaultProfile()) { LoadSucceeds = false };
            var service = CreateService(persistence);

            Assert.That(service.Profile.SelectedCatId, Is.EqualTo("red_tabby"));
            Assert.That(service.SelectedCatId, Is.EqualTo("red_tabby"));
            Assert.That(service.CurrentPortrait.BaseAssetId, Is.EqualTo("cat.red"));
            Assert.That(persistence.ReplaceCalls, Is.Zero);
        }

        [Test]
        public void LedgerChange_PublishesOnlyWhenImmutableEffectivePortraitChanges()
        {
            var purchase = CreatePurchases();
            var persistence = new RecordingPersistence(ProfileWithOutfit("outfit_conductor"));
            var service = CreateService(persistence, purchase.Service);
            int changed = 0;
            service.Changed += () => changed++;

            purchase.Ledger.ReplaceStoreGrants(new[]
            {
                new EntitlementGrant("supporter", GrantSource.Store),
            });
            Assert.That(changed, Is.Zero, "an unrelated entitlement does not repaint");

            purchase.Ledger.ReplaceStoreGrants(new[]
            {
                new EntitlementGrant("supporter", GrantSource.Store),
                new EntitlementGrant("outfit_conductor", GrantSource.Store),
            });
            Assert.That(changed, Is.EqualTo(1));

            purchase.Ledger.ReplaceStoreGrants(new[]
            {
                new EntitlementGrant("supporter", GrantSource.Store),
                new EntitlementGrant("outfit_conductor", GrantSource.Store),
            });
            Assert.That(changed, Is.EqualTo(1), "an identical ledger snapshot is silent");
            Assert.That(persistence.ReplaceCalls, Is.Zero);
        }

        [Test]
        public void PurchaseRuntimeInstall_RebindsOnceAndDetachesTheOldLedger()
        {
            var oldPurchase = CreatePurchases();
            oldPurchase.Ledger.ReplaceStoreGrants(new[]
            {
                new EntitlementGrant("outfit_conductor", GrantSource.Store),
            });
            PurchaseRuntime.Install(oldPurchase.Service);
            var persistence = new RecordingPersistence(ProfileWithOutfit("outfit_conductor"));
            var service = CreateService(persistence, oldPurchase.Service);
            CosmeticRuntime.Install(service);
            CosmeticRuntime.Install(service);
            int changed = 0;
            service.Changed += () => changed++;

            var newPurchase = CreatePurchases();
            PurchaseRuntime.Install(newPurchase.Service);
            Assert.That(service.CurrentPortrait.OutfitAssetId, Is.Empty);
            Assert.That(changed, Is.EqualTo(1));

            oldPurchase.Ledger.ReplaceStoreGrants(Array.Empty<EntitlementGrant>());
            oldPurchase.Ledger.ReplaceStoreGrants(new[]
            {
                new EntitlementGrant("outfit_conductor", GrantSource.Store),
            });
            Assert.That(changed, Is.EqualTo(1), "old ledger callbacks are detached");

            newPurchase.Ledger.ReplaceStoreGrants(new[]
            {
                new EntitlementGrant("outfit_conductor", GrantSource.Store),
            });
            Assert.That(service.CurrentPortrait.OutfitAssetId, Is.EqualTo("outfit.conductor"));
            Assert.That(changed, Is.EqualTo(2), "new ledger callback is subscribed once");
            Assert.That(persistence.ReplaceCalls, Is.Zero);
        }

        [Test]
        public void RuntimeCurrentIsNonNullAndUninstallIsConditionalByReference()
        {
            Assert.That(CosmeticRuntime.Current, Is.Not.Null);
            var first = CreateService(new RecordingPersistence(DefaultProfile()));
            var second = CreateService(new RecordingPersistence(DefaultProfile()));

            CosmeticRuntime.Install(first);
            CosmeticRuntime.Install(second);
            CosmeticRuntime.Uninstall(first);
            Assert.That(CosmeticRuntime.Current, Is.SameAs(second));

            CosmeticRuntime.Uninstall(second);
            Assert.That(CosmeticRuntime.Current, Is.Not.Null);
            Assert.That(CosmeticRuntime.Current, Is.Not.SameAs(second));
        }

        [Test]
        public void Dispose_DetachesLedgerCallbacks()
        {
            var purchase = CreatePurchases();
            purchase.Ledger.ReplaceStoreGrants(new[]
            {
                new EntitlementGrant("outfit_conductor", GrantSource.Store),
            });
            var service = CreateService(new RecordingPersistence(
                ProfileWithOutfit("outfit_conductor")), purchase.Service);
            var portrait = service.CurrentPortrait;
            int changed = 0;
            service.Changed += () => changed++;

            service.Dispose();
            purchase.Ledger.ReplaceStoreGrants(Array.Empty<EntitlementGrant>());

            Assert.That(service.CurrentPortrait, Is.EqualTo(portrait));
            Assert.That(changed, Is.Zero);
        }

        [Test]
        public void PortraitAssetLookup_UsesTheAdmittedInventory()
        {
            var service = CreateService(new RecordingPersistence(DefaultProfile()));

            Assert.That(service.TryGetPortraitAsset("cat.red", out var asset), Is.True);
            Assert.That(asset.RendererToken, Is.EqualTo("cat.red"));
            Assert.That(service.TryGetPortraitAsset("missing", out _), Is.False);
        }

        private CosmeticProfileService CreateService(RecordingPersistence persistence,
            PurchaseService purchases = null)
        {
            var inventory = CosmeticAssetInventory.Parse(InventoryJson, RendererTokens);
            var catalog = CosmeticCatalog.Parse(CatalogJson, inventory.AssetIds,
                inventory.ProvenanceAssetIds);
            var service = new CosmeticProfileService(catalog, inventory, persistence,
                purchases ?? CreatePurchases().Service);
            _services.Add(service);
            return service;
        }

        private static PurchaseHarness CreatePurchases()
        {
            var catalog = PurchaseCatalog.Parse(PurchaseCatalogJson);
            var clock = new PFixtures.Clock();
            var ledger = new EntitlementLedger();
            var backend = new FakePurchaseBackend { GrantOnPurchase = catalog.EntitlementsFor };
            return new PurchaseHarness(new PurchaseService(catalog, backend, clock.Fn, ledger),
                backend, ledger, clock);
        }

        private static CosmeticProfileSnapshot DefaultProfile() =>
            new CosmeticProfileSnapshot("red_tabby", null, null, new[]
            {
                new CosmeticLoadout("red_tabby", "", "", ""),
            });

        private static CosmeticProfileSnapshot ProfileWithOutfit(string outfitId) =>
            new CosmeticProfileSnapshot("red_tabby", null, null, new[]
            {
                new CosmeticLoadout("red_tabby", outfitId, "", ""),
            });

        private readonly struct PurchaseHarness
        {
            public PurchaseService Service { get; }
            public FakePurchaseBackend Backend { get; }
            public EntitlementLedger Ledger { get; }
            public PFixtures.Clock Clock { get; }

            public PurchaseHarness(PurchaseService service, FakePurchaseBackend backend,
                EntitlementLedger ledger, PFixtures.Clock clock)
            {
                Service = service;
                Backend = backend;
                Ledger = ledger;
                Clock = clock;
            }
        }

        public enum PersistenceFailure
        {
            None,
            Refuse,
            Throw,
        }

        private sealed class RecordingPersistence : ICosmeticProfilePersistence
        {
            private readonly List<string> _order;
            private CosmeticProfileSnapshot _snapshot;

            public PersistenceFailure Failure { get; set; }
            public bool LoadSucceeds { get; set; } = true;
            public int ReplaceCalls { get; private set; }

            public RecordingPersistence(CosmeticProfileSnapshot initial,
                List<string> order = null)
            {
                _snapshot = initial;
                _order = order;
            }

            public bool TryLoad(out CosmeticProfileSnapshot snapshot)
            {
                snapshot = _snapshot;
                return LoadSucceeds;
            }

            public bool TryReplace(CosmeticProfileSnapshot snapshot)
            {
                ReplaceCalls++;
                _order?.Add("persist");
                if (Failure == PersistenceFailure.Throw)
                    throw new InvalidOperationException("disk unavailable");
                if (Failure == PersistenceFailure.Refuse) return false;
                _snapshot = snapshot;
                return true;
            }
        }
    }
}
