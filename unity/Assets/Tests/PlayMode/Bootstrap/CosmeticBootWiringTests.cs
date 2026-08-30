using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CatMetro.Application.Save;
using CatMetro.Bootstrap;
using CatMetro.Bootstrap.DevCapture;
using CatMetro.Presentation.Cosmetics;
using CatMetro.Presentation.Screens;
using CatMetro.Services;
using CatMetro.Services.Cosmetics;
using CatMetro.Services.Purchases;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    public sealed class CosmeticBootWiringTests
    {
        private const string ExpectedDiagnostic =
            "COSMETICS admittedRows=3 rejectedRows=0 admittedCats=3 " +
            "assetReadyRows=3 visibleRows=0 purchasableRows=0 conductorReady=false";
        private const string EntitledDiagnostic =
            "COSMETICS admittedRows=3 rejectedRows=0 admittedCats=3 " +
            "assetReadyRows=3 visibleRows=1 purchasableRows=0 conductorReady=true";
        private const string PurchasableDiagnostic =
            "COSMETICS admittedRows=3 rejectedRows=0 admittedCats=3 " +
            "assetReadyRows=3 visibleRows=3 purchasableRows=3 conductorReady=true";
        private const string PauseFailureDiagnostic =
            "save pause commit failed safely: DirectoryNotFoundException";

        private GameRoot _root;
        private TestStorageRoot _storage;
        private TestDirectory _devCaptureRoot;

        [SetUp]
        public void SetUp()
        {
            PurchaseRuntime.ResetForTests();
            CosmeticRuntime.ResetForTests();
            ResetGameRootSeams();
            _devCaptureRoot = new TestDirectory("cm-cosmetics-task8-devcap-");
            DevBootOverride.DirectoryOverride = _devCaptureRoot.RootPath;
            DevLevelOverride.DirectoryOverride = _devCaptureRoot.RootPath;
            GameRoot.MessagingFactoryOverride = () => new InertMessaging();
            GameRoot.AnalyticsRuntimeFactory = () =>
                new GameAnalyticsRuntime(new InertAnalytics());
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root.gameObject);
            _root = null;
            ResetGameRootSeams();
            PurchaseRuntime.ResetForTests();
            CosmeticRuntime.ResetForTests();
            _storage?.Dispose();
            _storage = null;
            _devCaptureRoot?.Dispose();
            _devCaptureRoot = null;
            Time.timeScale = 1f;
        }

        [Test]
        public void Composition_LoadsShippedArtifacts_AndEmitsOneExactDiagnostic()
        {
            var messages = new List<string>();
            UnityEngine.Application.LogCallback capture = (condition, _, __) =>
            {
                if (condition.StartsWith("COSMETICS ", StringComparison.Ordinal))
                    messages.Add(condition);
            };
            var purchases = ControlledPurchases(new EntitlementLedger());
            CosmeticProfileService service = null;
            UnityEngine.Application.logMessageReceived += capture;
            try
            {
                service = CosmeticComposition.Create(null, purchases);
            }
            finally
            {
                UnityEngine.Application.logMessageReceived -= capture;
            }

            Assert.That(service, Is.Not.Null);
            Assert.That(service.Catalog.AdmittedCatCount, Is.EqualTo(3));
            Assert.That(service.Catalog.AdmittedRowCount, Is.EqualTo(3));
            Assert.That(service.Catalog.RejectedRowCount, Is.Zero);
            Assert.That(service.SelectedCatId, Is.EqualTo("red_tabby"));
            Assert.That(service.CurrentPortrait.BaseAssetId, Is.EqualTo("cat.red_tabby"));
            CollectionAssert.AreEqual(new[] { ExpectedDiagnostic }, messages);
            service.Dispose();
        }

        [Test]
        public void Composition_WithConductorEntitlement_EmitsDynamicPositiveDiagnostic()
        {
            var ledger = new EntitlementLedger();
            ledger.ReplaceStoreGrants(new[]
            {
                new EntitlementGrant(EntitlementIds.OutfitConductor, GrantSource.Store),
            });

            var service = CreateWithDiagnostic(ControlledPurchases(ledger), out var messages);

            Assert.That(service, Is.Not.Null);
            CollectionAssert.AreEqual(new[] { EntitledDiagnostic }, messages);
            service.Dispose();
        }

        [Test]
        public void Composition_WithLocalizedConductorAndFrameProducts_EmitsPurchasableDiagnostic()
        {
            var backend = new ReadyPurchaseBackend(
                new StoreProductView(ProductIds.OutfitConductor, "Conductor's Coat",
                    new LocalizedPrice("$1.99")),
                new StoreProductView(ProductIds.FrameBrass, "Brass Ticket Frame",
                    new LocalizedPrice("$0.99")),
                new StoreProductView(ProductIds.FrameLantern, "Lantern Frame",
                    new LocalizedPrice("$0.99")));
            var purchases = ControlledPurchases(new EntitlementLedger(), backend);
            bool refreshed = false;
            purchases.Refresh(() => refreshed = true);
            Assert.That(refreshed, Is.True, "the ready backend must finish its refresh");

            var service = CreateWithDiagnostic(purchases, out var messages);

            Assert.That(service, Is.Not.Null);
            CollectionAssert.AreEqual(new[] { PurchasableDiagnostic }, messages);
            service.Dispose();
        }

        [Test]
        public void Composition_WithRealStore_PersistsSelectionAndEquipAcrossSeparateLoads()
        {
            using var storage = new TestStorageRoot();
            var ledger = new EntitlementLedger();
            ledger.ReplaceStoreGrants(new[]
            {
                new EntitlementGrant("outfit_conductor", GrantSource.Store),
            });
            var purchases = ControlledPurchases(ledger);

            var firstStore = LoadStore(storage);
            var first = CosmeticComposition.Create(firstStore, purchases);
            Assert.That(first.TrySelectCat("blue_siamese"), Is.True);
            Assert.That(first.TryEquip("blue_siamese", CosmeticSlot.Outfit,
                "outfit_conductor"), Is.True);
            first.Dispose();

            var secondStore = LoadStore(storage);
            Assert.That(secondStore, Is.Not.SameAs(firstStore),
                "relaunch must load through a separate SaveStore instance");
            Assert.That(secondStore.State.Payload["profile"], Is.TypeOf<JObject>());
            var rawProfile = (JObject)secondStore.State.Payload["profile"];
            Assert.That(rawProfile["cosmetics"], Is.TypeOf<JObject>());
            var rawCosmetics = (JObject)rawProfile["cosmetics"];
            Assert.That((string)rawCosmetics["selectedCatId"],
                Is.EqualTo("blue_siamese"));
            Assert.That(rawCosmetics["loadouts"], Is.TypeOf<JArray>());
            var rawBlueLoadout = ((JArray)rawCosmetics["loadouts"])
                .OfType<JObject>()
                .Single(row => (string)row["catId"] == "blue_siamese");
            Assert.That((string)rawBlueLoadout["outfitId"],
                Is.EqualTo("outfit_conductor"));

            var second = CosmeticComposition.Create(secondStore, purchases);
            Assert.That(second.Profile.SelectedCatId, Is.EqualTo("blue_siamese"));
            Assert.That(second.Profile.LoadoutFor("blue_siamese").OutfitId,
                Is.EqualTo("outfit_conductor"));
            Assert.That(second.SelectedCatId, Is.EqualTo("blue_siamese"));
            Assert.That(second.CurrentPortrait.BaseAssetId, Is.EqualTo("cat.blue_siamese"));
            Assert.That(second.CurrentPortrait.OutfitAssetId,
                Is.EqualTo("outfit.conductor"));
            second.Dispose();
        }

        [Test]
        public void NullResourceText_TotalParsersStillConstructANonNullEmptyService()
        {
            var inventory = CosmeticAssetInventory.Parse(null,
                CosmeticPortraitPainter.SupportedRendererTokens);
            var catalog = CosmeticCatalog.Parse(null, inventory.AssetIds,
                inventory.ProvenanceAssetIds);
            var service = new CosmeticProfileService(catalog, inventory,
                new InMemoryCosmeticProfilePersistence(CosmeticProfileSnapshot.Empty),
                ControlledPurchases(new EntitlementLedger()));

            Assert.That(service, Is.Not.Null);
            Assert.That(service.Catalog.AdmittedCatCount, Is.Zero);
            Assert.That(service.Catalog.AdmittedRowCount, Is.Zero);
            Assert.That(service.SelectedCatId, Is.Empty);
            Assert.That(service.CurrentPortrait.BaseAssetId, Is.Null.Or.Empty);
            service.Dispose();
        }

        [UnityTest]
        public IEnumerator StorageStartupFailure_KeepsShippedStarterInMemory_AndRelaunchResetsIt()
        {
            GameRoot.DailyStorageRootOverride = () =>
                throw new IOException("task8 storage unavailable");
            ExpectStorageFailure();
            _root = GameRoot.Launch();
            yield return null;

            var first = RootCosmetics(_root);
            Assert.That(first, Is.Not.Null);
            AssertCompositionIdentity(_root, first);
            Assert.That(first.Catalog.AdmittedCatCount, Is.EqualTo(3));
            Assert.That(first.Catalog.AdmittedRowCount, Is.EqualTo(3));
            Assert.That(first.SelectedCatId, Is.EqualTo("red_tabby"));
            Assert.That(_root.Home.ProfilePortrait.AppliedCatId, Is.EqualTo("red_tabby"));
            Assert.That(_root.Wardrobe.EntryPortrait.AppliedCatId, Is.EqualTo("red_tabby"));

            Assert.That(first.TrySelectCat("blue_siamese"), Is.True);
            yield return null;
            Assert.That(_root.Home.ProfilePortrait.AppliedCatId, Is.EqualTo("blue_siamese"));
            Assert.That(_root.Wardrobe.EntryPortrait.AppliedCatId, Is.EqualTo("blue_siamese"));
            _root.Wardrobe.Open();
            yield return null;
            Assert.That(_root.Wardrobe.LargePortrait.AppliedCatId,
                Is.EqualTo("blue_siamese"));

            UnityEngine.Object.DestroyImmediate(_root.gameObject);
            _root = null;
            yield return null;

            ExpectStorageFailure();
            _root = GameRoot.Launch();
            yield return null;
            var relaunched = RootCosmetics(_root);
            Assert.That(relaunched, Is.Not.SameAs(first));
            AssertCompositionIdentity(_root, relaunched);
            Assert.That(relaunched.SelectedCatId, Is.EqualTo("red_tabby"),
                "the degraded path is memory-only and must not invent a second persistence path");
            Assert.That(_root.Home.ProfilePortrait.AppliedCatId, Is.EqualTo("red_tabby"));
            Assert.That(_root.Wardrobe.EntryPortrait.AppliedCatId, Is.EqualTo("red_tabby"));
        }

        [UnityTest]
        public IEnumerator RealLaunch_InstallsOneProfileIntoRuntimeHomeAndBothWardrobePortraits()
        {
            _storage = new TestStorageRoot();
            GameRoot.DailyStorageRootOverride = () => _storage;
            var ledger = new EntitlementLedger();
            ledger.ReplaceStoreGrants(new[]
            {
                new EntitlementGrant("outfit_conductor", GrantSource.Store),
            });
            var purchases = ControlledPurchases(ledger);
            PurchaseRuntime.Install(purchases);

            _root = GameRoot.Launch();
            yield return null;

            var profile = RootCosmetics(_root);
            AssertCompositionIdentity(_root, profile);

            Assert.That(profile.TrySelectCat("blue_siamese"), Is.True);
            Assert.That(profile.TryEquip("blue_siamese", CosmeticSlot.Outfit,
                "outfit_conductor"), Is.True);
            yield return null;

            AssertPortrait(_root.Home.ProfilePortrait, "blue_siamese",
                "outfit.conductor");
            AssertPortrait(_root.Wardrobe.EntryPortrait, "blue_siamese",
                "outfit.conductor");
            _root.Wardrobe.Open();
            yield return null;
            AssertPortrait(_root.Wardrobe.LargePortrait, "blue_siamese",
                "outfit.conductor");
        }

        [Test]
        public void InitializeFromSeam_OrdersCosmeticsWithinEachRealBootBranch()
        {
            string method = SourceMethod("private void InitializeFromSeam");
            int devCondition = method.IndexOf("if (devLevel != null)",
                StringComparison.Ordinal);
            Assert.That(devCondition, Is.GreaterThanOrEqualTo(0));
            string devBranch = BraceBlockAt(method, devCondition);
            int shippedStart = method.IndexOf(
                "var source = new StreamingAssetsContentSource()",
                devCondition + 1, StringComparison.Ordinal);
            Assert.That(shippedStart, Is.GreaterThan(devCondition));
            string shippedBranch = method.Substring(shippedStart);

            Assert.That(Regex.Matches(method,
                @"InitializeDailyLiveServices\s*\(\s*\)").Count, Is.EqualTo(2));
            Assert.That(Regex.Matches(method,
                @"InitializeCosmetics\s*\(\s*\)").Count, Is.EqualTo(2));
            Assert.That(Regex.Matches(method,
                @"ComposeScreenFlow\s*\(\s*\)").Count, Is.EqualTo(2));

            const string ordered =
                @"InitializeDailyLiveServices\s*\(\s*\)\s*;\s*" +
                @"InitializeCosmetics\s*\(\s*\)\s*;\s*" +
                @"if\s*\(\s*!SkipHome\s*\(\s*\)\s*\)\s*" +
                @"ComposeScreenFlow\s*\(\s*\)\s*;";
            Assert.That(Regex.Matches(devBranch, ordered).Count, Is.EqualTo(1),
                "the dev-level early-return branch installs cosmetics before screens");
            Assert.That(Regex.Matches(shippedBranch, ordered).Count, Is.EqualTo(1),
                "the shipped branch installs cosmetics before screens");
        }

        [Test]
        public void ApplicationLifecycle_SourceCommitsOnlyPauseTrueBeforeAnalyticsBackground()
        {
            string pause = SourceMethod("private void OnApplicationPause");
            string focus = SourceMethod("private void OnApplicationFocus");
            const string saveCommitCall =
                @"_saveStore\s*(?:\?\s*)?\.\s*\w*Commit\w*\s*\(";
            var tryBoundary = Regex.Match(pause, @"try\s*\{");
            var commit = Regex.Match(pause,
                @"_saveStore\s*\?\.\s*TryCommitOnPause\s*\(\s*\)\s*;");
            var catchBoundary = Regex.Match(pause,
                @"catch\s*\(\s*System\.Exception\s+\w+\s*\)");
            var background = Regex.Match(pause,
                @"_analyticsRuntime\s*\?\.\s*OnBackground\s*\(\s*\)\s*;");

            Assert.That(Regex.Matches(pause, saveCommitCall).Count, Is.EqualTo(1),
                "pause owns exactly one SaveStore commit call");
            Assert.That(tryBoundary.Success, Is.True,
                "pause=true owns the lifecycle exception boundary");
            Assert.That(commit.Success, Is.True);
            Assert.That(catchBoundary.Success, Is.True,
                "save I/O faults are contained at the Unity lifecycle boundary");
            Assert.That(background.Success, Is.True);
            Assert.That(tryBoundary.Index, Is.LessThan(commit.Index));
            Assert.That(commit.Index, Is.LessThan(catchBoundary.Index));
            Assert.That(catchBoundary.Index, Is.LessThan(background.Index),
                "analytics backgrounds even after a save fault");
            Assert.That(Regex.Matches(focus, saveCommitCall).Count, Is.Zero,
                "focus loss cannot call any commit method on the SaveStore");
        }

        [UnityTest]
        public IEnumerator ApplicationPauseTrue_CommitsTheActualRootsExistingSaveStore()
        {
            _storage = new TestStorageRoot();
            GameRoot.DailyStorageRootOverride = () => _storage;
            _root = GameRoot.Launch();
            yield return null;

            var liveStore = PrivateField<SaveStore>(_root, "_saveStore");
            Assert.That(liveStore, Is.Not.Null);
            liveStore.State.Payload["task8PauseProbe"] = "persisted-through-root";

            string captureDirectory = RedirectFrameCapture(_root, "pause-success");
            _root.SendMessage("OnApplicationPause", true,
                SendMessageOptions.RequireReceiver);
            var reloaded = LoadStore(_storage);
            Assert.That((string)reloaded.State.Payload["task8PauseProbe"],
                Is.EqualTo("persisted-through-root"));
            Assert.That(File.Exists(Path.Combine(captureDirectory, "framelog.csv")), Is.True);
        }

        [UnityTest]
        public IEnumerator ApplicationPauseTrue_SaveIoFailureIsContained_AndAnalyticsStillBackgrounds()
        {
            _storage = new TestStorageRoot();
            GameRoot.DailyStorageRootOverride = () => _storage;
            _root = GameRoot.Launch();
            yield return null;

            var liveStore = PrivateField<SaveStore>(_root, "_saveStore");
            var analytics = PrivateField<GameAnalyticsRuntime>(_root, "_analyticsRuntime");
            Assert.That(liveStore, Is.Not.Null);
            Assert.That(analytics, Is.Not.Null);
            Assert.That(PrivateValue<bool>(analytics, "_backgrounded"), Is.False);

            Directory.Delete(_storage.SaveDirectory, true);
            File.WriteAllText(_storage.SaveDirectory,
                "fixture-owned file deliberately blocks the save directory");

            var observedErrors = new List<string>();
            UnityEngine.Application.LogCallback capture = (condition, _, type) =>
            {
                if (type == LogType.Error) observedErrors.Add(condition);
            };
            UnityEngine.Application.logMessageReceived += capture;
            try
            {
                LogAssert.Expect(LogType.Error, PauseFailureDiagnostic);
                string captureDirectory = RedirectFrameCapture(_root, "pause-fault");
                Assert.DoesNotThrow(() => _root.SendMessage("OnApplicationPause", true,
                    SendMessageOptions.RequireReceiver));
                Assert.That(File.Exists(Path.Combine(captureDirectory, "framelog.csv")), Is.True,
                    "the separately rooted dev capture still flushes after the save fault");
            }
            finally
            {
                UnityEngine.Application.logMessageReceived -= capture;
            }

            CollectionAssert.AreEqual(new[] { PauseFailureDiagnostic }, observedErrors,
                "the boundary emits exactly one sanitized type-only error");
            Assert.That(PrivateValue<bool>(analytics, "_backgrounded"), Is.True,
                "analytics must background even when persistence throws");
            var pauseEvents = liveStore.ReportedEvents.Where(item =>
                item.Name == "error_caught"
                && item.Detail == "domain=save_pause detail=DirectoryNotFoundException").ToArray();
            Assert.That(pauseEvents, Has.Length.EqualTo(1),
                "the live SaveStore records the contained I/O failure");
        }

        [UnityTest]
        public IEnumerator DestroyingRoot_UninstallsProfileAndDetachesPortraitAndLedgerCallbacks()
        {
            _storage = new TestStorageRoot();
            GameRoot.DailyStorageRootOverride = () => _storage;
            var ledger = new EntitlementLedger();
            var purchases = ControlledPurchases(ledger);
            PurchaseRuntime.Install(purchases);
            _root = GameRoot.Launch();
            yield return null;

            var oldProfile = RootCosmetics(_root);
            var oldPortraits = new[]
            {
                _root.Home.ProfilePortrait,
                _root.Wardrobe.EntryPortrait,
                _root.Wardrobe.LargePortrait,
            };
            Assert.That(HasEventTarget(oldProfile, "Changed", oldPortraits[0]), Is.True,
                "Home portrait is active before Wardrobe opens");
            Assert.That(HasEventTarget(oldProfile, "Changed", oldPortraits[1]), Is.True,
                "Wardrobe entry portrait is active before its panel opens");
            Assert.That(HasEventTarget(oldProfile, "Changed", oldPortraits[2]), Is.False,
                "large portrait stays detached while its panel is inactive");

            _root.Wardrobe.Open();
            yield return null;
            AssertCompositionIdentity(_root, oldProfile);
            foreach (var portrait in oldPortraits)
            {
                Assert.That(PortraitSource(portrait), Is.SameAs(oldProfile));
                Assert.That(portrait.AppliedCatId, Is.EqualTo(oldProfile.SelectedCatId));
            }
            Assert.That(HasEventTarget(ledger, "Changed", oldProfile), Is.True);
            Assert.That(HasEventTarget(oldProfile, "Changed", oldPortraits[0]), Is.True,
                "Home portrait remains active when Wardrobe.Open is invoked directly");
            Assert.That(HasEventTarget(oldProfile, "Changed", oldPortraits[1]), Is.False,
                "entry portrait detaches when the entry is hidden");
            Assert.That(HasEventTarget(oldProfile, "Changed", oldPortraits[2]), Is.True,
                "large portrait attaches when the panel becomes active");

            UnityEngine.Object.DestroyImmediate(_root.gameObject);
            _root = null;
            yield return null;

            Assert.That(CosmeticRuntime.Current, Is.Not.SameAs(oldProfile));
            Assert.That(HasEventTarget(ledger, "Changed", oldProfile), Is.False);
            foreach (var portrait in oldPortraits)
                Assert.That(HasEventTarget(oldProfile, "Changed", portrait), Is.False);

            ledger.ReplaceStoreGrants(new[]
            {
                new EntitlementGrant("outfit_conductor", GrantSource.Store),
            });
            Assert.That(HasEventTarget(ledger, "Changed", oldProfile), Is.False,
                "later purchase authority changes cannot target the disposed profile");
        }

        [Test]
        public void OnDestroy_SourceUninstallsDisposesAndClearsCosmeticsBeforeAnalytics()
        {
            string teardown = SourceMethod("private void OnDestroy");
            var uninstall = Regex.Match(teardown,
                @"CosmeticRuntime\s*\.\s*Uninstall\s*\(\s*_cosmetics\s*\)");
            var dispose = Regex.Match(teardown,
                @"_cosmetics\s*\.\s*Dispose\s*\(\s*\)");
            var clear = Regex.Match(teardown, @"_cosmetics\s*=\s*null\s*;");
            var analytics = Regex.Match(teardown,
                @"_analyticsRuntime\s*\?\.\s*Dispose\s*\(\s*\)");

            Assert.That(uninstall.Success, Is.True);
            Assert.That(dispose.Success, Is.True);
            Assert.That(clear.Success, Is.True);
            Assert.That(analytics.Success, Is.True);
            Assert.That(uninstall.Index, Is.LessThan(dispose.Index));
            Assert.That(dispose.Index, Is.LessThan(clear.Index));
            Assert.That(clear.Index, Is.LessThan(analytics.Index));
        }

        private static CosmeticProfileService CreateWithDiagnostic(PurchaseService purchases,
            out IReadOnlyList<string> messages)
        {
            var captured = new List<string>();
            UnityEngine.Application.LogCallback capture = (condition, _, __) =>
            {
                if (condition.StartsWith("COSMETICS ", StringComparison.Ordinal))
                    captured.Add(condition);
            };
            CosmeticProfileService service;
            UnityEngine.Application.logMessageReceived += capture;
            try
            {
                service = CosmeticComposition.Create(null, purchases);
            }
            finally
            {
                UnityEngine.Application.logMessageReceived -= capture;
            }

            messages = captured;
            return service;
        }

        private static PurchaseService ControlledPurchases(EntitlementLedger ledger,
            IPurchaseBackend backend = null)
        {
            var resource = Resources.Load<TextAsset>("Monetization/product_catalog");
            Assert.That(resource, Is.Not.Null);
            var catalog = PurchaseCatalog.Parse(resource.text);
            return new PurchaseService(catalog, backend ?? new NullPurchaseBackend(),
                () => 1_700_000_000L, ledger);
        }

        private static SaveStore LoadStore(IStorageRoot storage)
        {
            var bytes = File.ReadAllBytes(Path.Combine(UnityEngine.Application.streamingAssetsPath,
                "config", "runtime_bounds.json"));
            var bounds = RuntimeBounds.Parse(bytes);
            Assert.That(bounds.Ok, Is.True, bounds.Error?.ToString());
            var store = new SaveStore(storage, new RealSaveFileSystem(), bounds.Value,
                MigrationTable.CreateDefault());
            store.Load();
            return store;
        }

        private static CosmeticProfileService RootCosmetics(GameRoot root)
            => PrivateField<CosmeticProfileService>(root, "_cosmetics");

        private static ICosmeticPortraitSource PortraitSource(CosmeticPortraitView portrait)
            => PrivateField<ICosmeticPortraitSource>(portrait, "_source");

        private static void AssertCompositionIdentity(GameRoot root,
            CosmeticProfileService profile)
        {
            Assert.That(RootCosmetics(root), Is.SameAs(profile));
            Assert.That(CosmeticRuntime.Current, Is.SameAs(profile));
            Assert.That(PrivateField<CosmeticProfileService>(root.Wardrobe, "_profile"),
                Is.SameAs(profile));
            Assert.That(PortraitSource(root.Home.ProfilePortrait), Is.SameAs(profile));
            Assert.That(PortraitSource(root.Wardrobe.EntryPortrait), Is.SameAs(profile));
            Assert.That(PortraitSource(root.Wardrobe.LargePortrait), Is.SameAs(profile));
        }

        private static string SourceMethod(string signature)
        {
            string path = Path.Combine(UnityEngine.Application.dataPath,
                "Scripts", "Bootstrap", "GameRoot.cs");
            string source = File.ReadAllText(path);
            int signatureStart = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(signatureStart, Is.GreaterThanOrEqualTo(0),
                "missing source method " + signature);
            return StripComments(BraceBlockAt(source, signatureStart));
        }

        private static string BraceBlockAt(string source, int searchStart)
        {
            int open = source.IndexOf('{', searchStart);
            Assert.That(open, Is.GreaterThanOrEqualTo(0));
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0)
                    return source.Substring(open, i - open + 1);
            }

            Assert.Fail("unterminated source block");
            return string.Empty;
        }

        private static string StripComments(string source)
            => Regex.Replace(source, @"/\*[\s\S]*?\*/|//[^\r\n]*", string.Empty);

        private static T PrivateField<T>(object owner, string name) where T : class
        {
            Assert.That(owner, Is.Not.Null);
            var field = owner.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                owner.GetType().Name + " must own " + name);
            return field.GetValue(owner) as T;
        }

        private static T PrivateValue<T>(object owner, string name)
        {
            Assert.That(owner, Is.Not.Null);
            var field = owner.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                owner.GetType().Name + " must own " + name);
            return (T)field.GetValue(owner);
        }

        private string RedirectFrameCapture(GameRoot root, string leaf)
        {
            var capture = root.GetComponent<DevFrameCapture>();
            Assert.That(capture, Is.Not.Null);
            string directory = Path.Combine(_devCaptureRoot.RootPath, leaf);
            capture.OutputDirectory = directory;
            return directory;
        }

        private static bool HasEventTarget(object owner, string eventName, object target)
        {
            var field = owner.GetType().GetField(eventName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                owner.GetType().Name + " must expose the " + eventName + " event backing field");
            var callback = field.GetValue(owner) as Delegate;
            return callback != null && callback.GetInvocationList()
                .Any(item => ReferenceEquals(item.Target, target));
        }

        private static void AssertPortrait(CosmeticPortraitView portrait, string catId,
            string outfitAssetId)
        {
            Assert.That(portrait.AppliedCatId, Is.EqualTo(catId));
            Assert.That(portrait.AppliedOutfitAssetId, Is.EqualTo(outfitAssetId));
        }

        private static void ExpectStorageFailure()
        {
            LogAssert.Expect(LogType.Error, new Regex(
                "^daily progress unavailable; continuing without persistence: " +
                "task8 storage unavailable$"));
        }

        private static void ResetGameRootSeams()
        {
            GameRoot.DevSkipShippedHome = false;
            GameRoot.DailyEntryUnlocked = false;
            GameRoot.BootToHome = false;
            GameRoot.DailyStorageRootOverride = null;
            GameRoot.MessagingFactoryOverride = null;
            GameRoot.AnalyticsRuntimeFactory = null;
            DevBootOverride.DirectoryOverride = null;
            DevLevelOverride.DirectoryOverride = null;
        }

        private sealed class InertAnalytics : IAnalytics
        {
            public int QueuedEventCount => 0;
            public void Log(in AnalyticsEvent e) { }
            public void SetUserProperty(UserPropertyKey key, string value) { }
        }

        private sealed class InertMessaging : IMessaging
        {
            public bool IsAvailable => false;
            public string SubscriptionId => string.Empty;
            public MessagingPermission Permission => MessagingPermission.Unknown;
            public bool CanRequestPermission => false;
            public event Action<MessagingRoute> LinkOpened
            {
                add { }
                remove { }
            }

            public Task<MessagingPermission> PromptAsync(bool fallbackToSettings,
                CancellationToken cancellationToken)
                => Task.FromResult(MessagingPermission.Unknown);

            public void Schedule(DailyChallengeNotification notification) { }
            public void Cancel(string notificationId) { }
            public void Dispose() { }
        }

        private sealed class ReadyPurchaseBackend : IPurchaseBackend
        {
            private readonly IReadOnlyList<StoreProductView> _products;

            public ReadyPurchaseBackend(params StoreProductView[] products)
            {
                _products = products ?? Array.Empty<StoreProductView>();
            }

            public BackendAvailability Availability => BackendAvailability.Ready;

            public void FetchProducts(Action<IReadOnlyList<StoreProductView>> onDone)
                => onDone?.Invoke(_products);

            public void Purchase(string productId, Action<PurchaseResult> onDone)
                => onDone?.Invoke(PurchaseResult.Unavailable(productId, "test backend"));

            public void Restore(Action<RestoreResult> onDone)
                => onDone?.Invoke(new RestoreResult(RestoreOutcome.Completed));

            public void RefreshEntitlements(Action<EntitlementSnapshot> onDone)
                => onDone?.Invoke(new EntitlementSnapshot(true,
                    Array.Empty<EntitlementGrant>()));
        }

        private sealed class TestDirectory : IDisposable
        {
            public string RootPath { get; }

            public TestDirectory(string prefix)
            {
                RootPath = Path.Combine(Path.GetTempPath(),
                    prefix + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(RootPath);
            }

            public void Dispose()
            {
                try
                {
                    if (File.Exists(RootPath)) File.Delete(RootPath);
                    else if (Directory.Exists(RootPath)) Directory.Delete(RootPath, true);
                }
                catch
                {
                    // Best-effort test-artifact cleanup.
                }
            }
        }

        private sealed class TestStorageRoot : IStorageRoot, IDisposable
        {
            public string SaveDirectory { get; }
            public string CacheDirectory => SaveDirectory;

            public TestStorageRoot()
            {
                SaveDirectory = Path.Combine(Path.GetTempPath(),
                    "cm-cosmetics-task8-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(SaveDirectory);
            }

            public void Dispose()
            {
                try
                {
                    if (File.Exists(SaveDirectory)) File.Delete(SaveDirectory);
                    else if (Directory.Exists(SaveDirectory))
                        Directory.Delete(SaveDirectory, true);
                }
                catch
                {
                    // Best-effort test-artifact cleanup.
                }
            }
        }
    }
}
