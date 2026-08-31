using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CatMetro.Application.Save;
using CatMetro.Bootstrap;
using CatMetro.Integrations;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;
using CatMetro.Services;
using CatMetro.Services.Ads;
using CatMetro.Services.Purchases;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    public sealed class RewardedAdsWiringTests
    {
        private const long InitialNow = 2_000_000_000L;
        private const long ExpectedConductorExpiry = InitialNow + 86_400L;
        private const string LocalDateKey = "2033-05-18";
        private static readonly Rect PhoneSafeArea = new Rect(0f, 64f, 917f, 1920f);

        private readonly List<GameObject> _ownedObjects = new List<GameObject>();
        private readonly List<RewardedAdsComposition> _directCompositions =
            new List<RewardedAdsComposition>();
        private readonly List<TempStorageRoot> _tempRoots = new List<TempStorageRoot>();

        [SetUp]
        public void SetUp()
        {
            MonetizationBootstrap.ResetForTests();
            PurchaseBackendFactory.ResetForTests();
            RewardedAdProviderFactory.ResetForTests();
            SaveRuntime.ResetForTests();
            RewardedAdRuntime.ResetForTests();
            PurchaseRuntime.ResetForTests();
            GameRoot.DailyStorageRootOverride = null;
            GameRoot.DevSkipShippedHome = false;
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = _ownedObjects.Count - 1; i >= 0; i--)
            {
                if (_ownedObjects[i] != null) UnityEngine.Object.Destroy(_ownedObjects[i]);
            }
            yield return null;

            for (int i = _directCompositions.Count - 1; i >= 0; i--)
                _directCompositions[i]?.Dispose();
            MonetizationBootstrap.ResetForTests();
            PurchaseBackendFactory.ResetForTests();
            RewardedAdProviderFactory.ResetForTests();
            SaveRuntime.ResetForTests();
            RewardedAdRuntime.ResetForTests();
            PurchaseRuntime.ResetForTests();
            GameRoot.DailyStorageRootOverride = null;
            GameRoot.DevSkipShippedHome = false;
            Time.timeScale = 1f;

            for (int i = _tempRoots.Count - 1; i >= 0; i--)
                _tempRoots[i]?.Dispose();
            _ownedObjects.Clear();
            _directCompositions.Clear();
            _tempRoots.Clear();
        }

        [UnityTest]
        public IEnumerator SaveInstall_RewardPrecommitsBeforeLedger_AndFreshRestartRestoresOriginalExpiryToWardrobe()
        {
            var root = NewTempRoot();
            var store = NewStore(root);
            store.Load();
            var clock = new MutableClock(InitialNow);
            var firstProvider = new Provider();
            var firstReporter = new BackendReporter();
            var firstService = NewService(firstReporter, clock);
            PurchaseRuntime.Install(firstService);
            var firstComposition = NewComposition(firstService, firstProvider, firstReporter);
            firstComposition.Bind();

            Assert.That(SaveRuntime.IsInstalled, Is.False);
            Assert.That(RewardedAdRuntime.IsInstalled, Is.False);
            Assert.That(File.Exists(store.SavePath), Is.False,
                "loading a fresh profile must not create bytes before the earned reward");

            SaveRuntime.Install(store);

            Assert.That(firstService.CanPersistRewardedAdGrants, Is.True,
                "SaveRuntime.Install synchronously attaches the production save adapter");
            Assert.That(firstProvider.InitializeCalls, Is.EqualTo(1));
            Assert.That(firstProvider.LoadCalls, Is.EqualTo(1));
            Assert.That(RewardedAdRuntime.IsInstalled, Is.True);

            var firstRig = NewWardrobeRig(firstService);
            firstRig.View.Open();
            firstRig.View.LayoutForViewport(PhoneSafeArea, 408f);
            Canvas.ForceUpdateCanvases();
            yield return null;

            bool observerRan = false;
            bool durableBeforePublication = false;
            firstService.Ledger.Changed += () =>
            {
                observerRan = true;
                var verifier = NewStore(root);
                verifier.Load();
                durableBeforePublication = ExactExpiry(
                    new RewardedAdSaveStore(verifier).ReadLocalLeases(),
                    EntitlementIds.OutfitConductor) == ExpectedConductorExpiry;
            };

            Assert.That(RewardedAdRuntime.Current.Show("wardrobe_try_conductor"),
                Is.EqualTo(RewardedShowOutcome.Started));
            Assert.That(firstProvider.LastAttemptId, Is.GreaterThan(0L));
            firstProvider.Emit(RewardedAdEventKind.Rewarded, firstProvider.LastAttemptId,
                "wardrobe_try_conductor");
            yield return null;

            Assert.That(observerRan, Is.True);
            Assert.That(durableBeforePublication, Is.True,
                "Ledger.Changed must observe the exact earned lease already committed to disk");
            Assert.That(firstService.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);
            AssertBorrowed(firstRig.View, 0, true);
            Assert.That(ExactExpiry(firstService.Ledger.ExportLeases(),
                EntitlementIds.OutfitConductor), Is.EqualTo(ExpectedConductorExpiry));
            Assert.That(ExactExpiry(new RewardedAdSaveStore(store).ReadLocalLeases(),
                EntitlementIds.OutfitConductor), Is.EqualTo(ExpectedConductorExpiry));

            byte[] committedBytes = File.ReadAllBytes(store.SavePath);
            firstRig.View.Hide();
            UnityEngine.Object.Destroy(firstRig.CanvasHost);
            UnityEngine.Object.Destroy(firstRig.InputHost);
            UnityEngine.Object.Destroy(firstRig.PumpHost);
            yield return null;

            Assert.That(firstProvider.EventRemoveCalls, Is.EqualTo(1));
            Assert.That(firstReporter.EventRemoveCalls, Is.EqualTo(1));
            Assert.That(RewardedAdRuntime.IsInstalled, Is.False);
            SaveRuntime.ResetForTests();
            PurchaseRuntime.ResetForTests();

            var freshStore = NewStore(root);
            freshStore.Load();
            CollectionAssert.AreEqual(committedBytes, File.ReadAllBytes(freshStore.SavePath),
                "loading the committed profile must not rewrite its bytes");
            var secondProvider = new Provider();
            var secondReporter = new BackendReporter();
            var secondService = NewService(secondReporter, clock);
            PurchaseRuntime.Install(secondService);
            var secondComposition = NewComposition(secondService, secondProvider, secondReporter);
            secondComposition.Bind();
            SaveRuntime.Install(freshStore);
            var secondRig = NewWardrobeRig(secondService);
            secondRig.View.Open();
            secondRig.View.LayoutForViewport(PhoneSafeArea, 408f);
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.That(secondProvider.ShowCalls, Is.Zero,
                "fresh presentation restores without another rewarded callback");
            Assert.That(secondService.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);
            Assert.That(ExactExpiry(secondService.Ledger.ExportLeases(),
                EntitlementIds.OutfitConductor), Is.EqualTo(ExpectedConductorExpiry));
            AssertBorrowed(secondRig.View, 0, true);
            secondRig.View.Hide();
            UnityEngine.Object.Destroy(secondRig.CanvasHost);
            UnityEngine.Object.Destroy(secondRig.InputHost);
            UnityEngine.Object.Destroy(secondRig.PumpHost);
            yield return null;
            Assert.That(secondProvider.EventRemoveCalls, Is.EqualTo(1));
            Assert.That(secondReporter.EventRemoveCalls, Is.EqualTo(1));
            Assert.That(RewardedAdRuntime.IsInstalled, Is.False);
        }

        [UnityTest]
        public IEnumerator ComposedCallbacks_DuplicateRewardIsLatched_AndCloseBeforeRewardGrantsTheOriginalAttempt()
        {
            var root = NewTempRoot();
            var store = NewStore(root);
            store.Load();
            var clock = new MutableClock(InitialNow);
            var provider = new Provider();
            var reporter = new BackendReporter();
            var service = NewService(reporter, clock);
            var composition = NewComposition(service, provider, reporter);
            composition.Bind();
            SaveRuntime.Install(store);
            var rig = NewWardrobeRig(service);
            rig.View.Open();
            rig.View.LayoutForViewport(PhoneSafeArea, 408f);
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.That(RewardedAdRuntime.Current.Show(PlacementIds[0]),
                Is.EqualTo(RewardedShowOutcome.Started));
            long conductorAttempt = provider.LastAttemptId;
            provider.Emit(RewardedAdEventKind.Rewarded, conductorAttempt, PlacementIds[0]);
            long firstExpiry = ExactExpiry(service.Ledger.ExportLeases(),
                EntitlementIds.OutfitConductor);
            Assert.That(firstExpiry, Is.EqualTo(ExpectedConductorExpiry));
            Assert.That(new RewardedAdSaveStore(store).ReadLocalDateCount(
                PlacementIds[0], LocalDateKey), Is.EqualTo(1));

            clock.Now = firstExpiry + 1L;
            Assert.That(service.PruneExpiredLeases(), Is.True);
            Assert.That(service.IsUnlocked(EntitlementIds.OutfitConductor), Is.False);
            AssertBorrowed(rig.View, 0, false);
            byte[] beforeExpiredDuplicate = File.ReadAllBytes(store.SavePath);

            provider.Emit(RewardedAdEventKind.Rewarded, conductorAttempt, PlacementIds[0]);

            Assert.That(service.IsUnlocked(EntitlementIds.OutfitConductor), Is.False,
                "a duplicate after expiry/prune must not renew the original attempt");
            Assert.That(ExactExpiry(service.Ledger.ExportLeases(),
                EntitlementIds.OutfitConductor), Is.EqualTo(-1L));
            Assert.That(new RewardedAdSaveStore(store).ReadLocalDateCount(
                PlacementIds[0], LocalDateKey), Is.EqualTo(1));
            CollectionAssert.AreEqual(beforeExpiredDuplicate, File.ReadAllBytes(store.SavePath),
                "a latched duplicate cannot durably mutate the lease or cap");
            provider.Emit(RewardedAdEventKind.Closed, conductorAttempt, PlacementIds[0]);

            Assert.That(RewardedAdRuntime.Current.Show(PlacementIds[1]),
                Is.EqualTo(RewardedShowOutcome.Started));
            long engineerAttempt = provider.LastAttemptId;
            provider.Emit(RewardedAdEventKind.Closed, engineerAttempt, PlacementIds[1]);
            Assert.That(RewardedAdRuntime.Current.Show(PlacementIds[2]),
                Is.EqualTo(RewardedShowOutcome.Started));
            long scarfAttempt = provider.LastAttemptId;

            provider.Emit(RewardedAdEventKind.Rewarded, engineerAttempt, PlacementIds[1]);

            Assert.That(service.IsUnlocked(EntitlementIds.OutfitEngineer), Is.True,
                "close-before-reward keeps the exact original attempt eligible");
            Assert.That(service.IsUnlocked(EntitlementIds.AccessoryScarf), Is.False,
                "the late Engineer callback cannot grant the newer Scarf attempt");
            AssertBorrowed(rig.View, 1, true);
            AssertBorrowed(rig.View, 2, false);
            Assert.That(RewardedAdRuntime.Current.Show(PlacementIds[3]),
                Is.EqualTo(RewardedShowOutcome.Busy),
                "the newer Scarf attempt remains the one open attempt");
            byte[] afterLateReward = File.ReadAllBytes(store.SavePath);
            provider.Emit(RewardedAdEventKind.Rewarded, engineerAttempt, PlacementIds[1]);
            CollectionAssert.AreEqual(afterLateReward, File.ReadAllBytes(store.SavePath));
            Assert.That(new RewardedAdSaveStore(store).ReadLocalDateCount(
                PlacementIds[1], LocalDateKey), Is.EqualTo(1));
            AssertBorrowed(rig.View, 1, true);
            AssertBorrowed(rig.View, 2, false);
            provider.Emit(RewardedAdEventKind.Closed, scarfAttempt, PlacementIds[2]);
            rig.View.Hide();
            UnityEngine.Object.Destroy(rig.CanvasHost);
            UnityEngine.Object.Destroy(rig.InputHost);
            UnityEngine.Object.Destroy(rig.PumpHost);
            yield return null;
            Assert.That(provider.EventRemoveCalls, Is.EqualTo(1));
            Assert.That(reporter.EventRemoveCalls, Is.EqualTo(1));
            Assert.That(RewardedAdRuntime.IsInstalled, Is.False);
        }

        [UnityTest]
        public IEnumerator ProductionBootstrap_FourPlacementsShareOneProviderAndConfiguredAdUnit()
        {
            var root = NewTempRoot();
            var store = NewStore(root);
            store.Load();
            var backend = new BackendReporter();
            var provider = new Provider();
            int backendFactoryCalls = 0;
            int providerFactoryCalls = 0;
            RewardedAdsConfig factoryConfig = null;
            PurchaseBackendFactory.Register(_ =>
            {
                backendFactoryCalls++;
                return backend;
            });
            RewardedAdProviderFactory.Register(config =>
            {
                providerFactoryCalls++;
                factoryConfig = config;
                provider.AdUnitId = config.RewardedAdUnitId;
                return provider;
            });
            var configured = ConfiguredAds();
            MonetizationBootstrap.SetRewardedAdsConfigForTests(configured);

            MonetizationBootstrap.Boot();
            var monetizationHost = Track(GameObject.Find("[Monetization]"));

            Assert.That(providerFactoryCalls, Is.Zero,
                "boot waits for the production save runtime before constructing ads");
            SaveRuntime.Install(store);
            Assert.That(backendFactoryCalls, Is.EqualTo(1));
            Assert.That(providerFactoryCalls, Is.EqualTo(1));
            Assert.That(factoryConfig, Is.SameAs(configured));
            Assert.That(provider.InitializeCalls, Is.EqualTo(1));
            Assert.That(provider.LoadCalls, Is.EqualTo(1));

            for (int i = 0; i < PlacementIds.Length; i++)
            {
                Assert.That(RewardedAdRuntime.Current.Show(PlacementIds[i]),
                    Is.EqualTo(RewardedShowOutcome.Started), PlacementIds[i]);
                Assert.That(provider.Shows[i].PlacementId, Is.EqualTo(PlacementIds[i]));
                Assert.That(provider.Shows[i].AdUnitId,
                    Is.EqualTo(configured.RewardedAdUnitId));
                provider.Emit(RewardedAdEventKind.Closed, provider.Shows[i].AttemptId,
                    PlacementIds[i]);
            }

            Assert.That(provider.Shows, Has.Count.EqualTo(4));
            Assert.That(providerFactoryCalls, Is.EqualTo(1));
            Assert.That(provider.DisposeCalls, Is.Zero);
            UnityEngine.Object.Destroy(monetizationHost);
            yield return null;
            Assert.That(provider.EventRemoveCalls, Is.EqualTo(1));
            Assert.That(backend.EventRemoveCalls, Is.EqualTo(1));
            Assert.That(provider.DisposeCalls, Is.EqualTo(1));
            Assert.That(RewardedAdRuntime.IsInstalled, Is.False);
        }

        [UnityTest]
        public IEnumerator ReporterFailureAndNoFill_LeaveHomeShopAndBoardUsableWithZeroAdTargets()
        {
            var root = NewTempRoot();
            GameRoot.DailyStorageRootOverride = () => root;
            var backend = new BackendReporter { ThrowOnReport = true };
            var provider = new Provider { IsReady = false, EmitLoadedDuringLoad = true };
            PurchaseBackendFactory.Register(_ => backend);
            RewardedAdProviderFactory.Register(config =>
            {
                provider.AdUnitId = config.RewardedAdUnitId;
                return provider;
            });
            MonetizationBootstrap.SetRewardedAdsConfigForTests(ConfiguredAds());
            MonetizationBootstrap.Boot();
            var monetizationHost = Track(GameObject.Find("[Monetization]"));

            var game = GameRoot.Launch();
            Track(game.gameObject);
            yield return null;

            Assert.That(provider.LoadCalls, Is.EqualTo(1));
            Assert.That(backend.ReportCalls, Is.EqualTo(1),
                "the loaded callback exercises the reporter failure boundary");
            Assert.That(game.Home.IsVisible, Is.True, "real shipped Home is visible");
            Assert.That(game.Wardrobe.EntryVisible, Is.True);
            Assert.That(game.Input.HandleTapAtScreen(game.Wardrobe.EntryRectPx.center),
                Is.EqualTo(-3));
            yield return null;

            AssertWardrobeNoAdTargets(game.Wardrobe, game.Input);
            Assert.That(game.Input.HandleTapAtScreen(game.Wardrobe.BuyRectPx.center),
                Is.EqualTo(-3));
            Assert.That(game.Input.HandleTapAtScreen(game.Wardrobe.RestoreRectPx.center),
                Is.EqualTo(-3));
            Assert.That(backend.PurchaseCalls, Is.EqualTo(1));
            Assert.That(backend.RestoreCalls, Is.EqualTo(1));
            Assert.That(game.Input.HandleTapAtScreen(game.Wardrobe.BackRectPx.center),
                Is.EqualTo(-3));

            Assert.That(game.Home.IsVisible, Is.True);
            Assert.That(game.Input.HandleTapAtScreen(game.Home.PinPaintedRectPx.center),
                Is.EqualTo(-3));
            Assert.That(game.Intro.IsVisible, Is.True);
            Assert.That(game.Input.HandleTapAtScreen(game.Intro.PlayChipRectPx.center),
                Is.EqualTo(-3));
            Assert.That(game.ScreensVisible, Is.False);
            var boardTarget = game.Cam.WorldToScreenPoint(game.View.SwitchWorldPos(0));
            Assert.That(game.Input.HandleTapAtScreen(boardTarget), Is.EqualTo(0),
                "a real board target remains usable after reporter failure and no-fill");
            UnityEngine.Object.Destroy(game.gameObject);
            UnityEngine.Object.Destroy(monetizationHost);
            yield return null;
            Assert.That(provider.EventRemoveCalls, Is.EqualTo(1));
            Assert.That(backend.EventRemoveCalls, Is.EqualTo(1));
            Assert.That(RewardedAdRuntime.IsInstalled, Is.False);
        }

        [UnityTest]
        public IEnumerator MissingReporter_DoesNotConstructProviderOrInstallRewardedRuntime()
        {
            var root = NewTempRoot();
            GameRoot.DailyStorageRootOverride = () => root;
            var backend = new BackendOnly();
            int providerFactoryCalls = 0;
            PurchaseBackendFactory.Register(_ => backend);
            RewardedAdProviderFactory.Register(_ =>
            {
                providerFactoryCalls++;
                return new Provider();
            });
            MonetizationBootstrap.SetRewardedAdsConfigForTests(ConfiguredAds());
            MonetizationBootstrap.Boot();
            var monetizationHost = Track(GameObject.Find("[Monetization]"));

            var game = GameRoot.Launch();
            Track(game.gameObject);
            yield return null;

            Assert.That(providerFactoryCalls, Is.Zero);
            Assert.That(RewardedAdRuntime.IsInstalled, Is.False);
            Assert.That(game.Home.IsVisible, Is.True);
            Assert.That(game.Input.HandleTapAtScreen(game.Wardrobe.EntryRectPx.center),
                Is.EqualTo(-3));
            yield return null;
            Assert.That(game.Wardrobe.transform.Find("WardrobePanel/TryOnStrip"), Is.Null);
            Assert.That(game.Input.Regions.Count, Is.EqualTo(3));
            for (int i = 0; i < PlacementIds.Length; i++)
                Assert.That(game.Input.Regions.IsRegistered(
                    "wardrobe.rewarded." + PlacementIds[i]), Is.False);
            UnityEngine.Object.Destroy(game.gameObject);
            UnityEngine.Object.Destroy(monetizationHost);
            yield return null;
            Assert.That(RewardedAdRuntime.IsInstalled, Is.False);
        }

        [UnityTest]
        public IEnumerator UnconfiguredAds_RealBootOmitsTryOnArtifactAndReclaimsPreviewGap()
        {
            var root = NewTempRoot();
            GameRoot.DailyStorageRootOverride = () => root;
            var backend = new BackendReporter();
            int providerFactoryCalls = 0;
            PurchaseBackendFactory.Register(_ => backend);
            RewardedAdProviderFactory.Register(_ =>
            {
                providerFactoryCalls++;
                return new Provider();
            });
            MonetizationBootstrap.SetRewardedAdsConfigForTests(
                RewardedAdsConfig.Parse("{}", RuntimePlatform.Android));
            MonetizationBootstrap.Boot();
            Track(GameObject.Find("[Monetization]"));

            var game = GameRoot.Launch();
            Track(game.gameObject);
            yield return null;

            Assert.That(providerFactoryCalls, Is.Zero,
                "an unconfigured build must not construct the mediation provider");
            Assert.That(RewardedAdRuntime.IsInstalled, Is.False);
            Assert.That(game.Input.HandleTapAtScreen(game.Wardrobe.EntryRectPx.center),
                Is.EqualTo(-3));
            yield return null;

            game.Wardrobe.LayoutForViewport(PhoneSafeArea, 408f);
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();

            var panel = game.Wardrobe.transform.Find("WardrobePanel");
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.Find("TryOnStrip"), Is.Null,
                "an absent provider must leave no ad-shaped hierarchy to render");

            string[] forbiddenTryOnText =
            {
                "Today's try-ons",
                "Conductor",
                "Engineer",
                "Scarf",
                "Goggles",
                "Locked",
                "Borrowed today",
                "Watch to borrow today",
                "Try-on unavailable",
                "Ready to wear!",
            };
            var allLabels = game.Wardrobe.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < forbiddenTryOnText.Length; i++)
            {
                string forbidden = forbiddenTryOnText[i];
                Assert.That(Array.Exists(allLabels, label => label.text == forbidden), Is.False,
                    "unconfigured Wardrobe retained try-on text: " + forbidden);
            }

            Assert.That(game.Input.Regions.Count, Is.EqualTo(3),
                "only Back, Buy, and Restore may remain on the open Wardrobe");
            for (int i = 0; i < PlacementIds.Length; i++)
            {
                Assert.That(game.Input.Regions.IsRegistered(
                    "wardrobe.rewarded." + PlacementIds[i]), Is.False,
                    PlacementIds[i] + " left a ghost rewarded target");
            }

            var status = ProjectedScreenRect(
                panel.Find("WardrobeStatus") as RectTransform);
            var portrait = ProjectedScreenRect(
                panel.Find("ProfileCatCard") as RectTransform);
            const float expectedGapPx = 12f * (408f / 160f);
            Assert.That(portrait.yMin - status.yMax,
                Is.EqualTo(expectedGapPx).Within(1f),
                "the portrait must reclaim the omitted 172dp preview band without a blank hole");

            Assert.That(panel.Find("BackChip").gameObject.activeInHierarchy, Is.True);
            Assert.That(panel.Find("BuyConductorCoatChip").gameObject.activeInHierarchy, Is.True);
            Assert.That(panel.Find("RestorePurchasesChip").gameObject.activeInHierarchy, Is.True);
            Assert.That(game.Input.HandleTapAtScreen(game.Wardrobe.BuyRectPx.center),
                Is.EqualTo(-3));
            Assert.That(game.Input.HandleTapAtScreen(game.Wardrobe.RestoreRectPx.center),
                Is.EqualTo(-3));
            Assert.That(backend.PurchaseCalls, Is.EqualTo(1));
            Assert.That(backend.RestoreCalls, Is.EqualTo(1));
            Assert.That(game.Input.HandleTapAtScreen(game.Wardrobe.BackRectPx.center),
                Is.EqualTo(-3));
            yield return null;
            Assert.That(game.Wardrobe.PanelVisible, Is.False);
            Assert.That(game.Home.IsVisible, Is.True);
        }

        private RewardedAdsComposition NewComposition(PurchaseService service, Provider provider,
            IAdEventReporter reporter)
        {
            var composition = new RewardedAdsComposition(service, ShippedPlacements(service.Catalog),
                () => provider, reporter, () => LocalDateKey);
            _directCompositions.Add(composition);
            return composition;
        }

        private static PurchaseService NewService(IPurchaseBackend backend, MutableClock clock)
            => new PurchaseService(ShippedCatalog(), backend, clock.Read);

        private WardrobeRig NewWardrobeRig(PurchaseService service)
        {
            var canvasHost = Track(new GameObject("RewardedAdsWiringCanvas"));
            var canvas = canvasHost.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var inputHost = Track(new GameObject("RewardedAdsWiringInput"));
            var input = inputHost.AddComponent<TapInput>();
            var view = WardrobeScreenView.Create(canvas.transform, service);
            view.Attach(input.Regions);
            var pumpHost = Track(new GameObject("RewardedAdsWiringPump"));
            pumpHost.AddComponent<MonetizationPump>().Bind(service,
                _directCompositions[_directCompositions.Count - 1]);
            return new WardrobeRig(canvasHost, inputHost, pumpHost, view, input);
        }

        private TempStorageRoot NewTempRoot()
        {
            var root = new TempStorageRoot();
            _tempRoots.Add(root);
            return root;
        }

        private SaveStore NewStore(IStorageRoot root)
        {
            var bytes = File.ReadAllBytes(Path.Combine(UnityEngine.Application.streamingAssetsPath,
                "config", "runtime_bounds.json"));
            var parsed = RuntimeBounds.Parse(bytes);
            Assert.That(parsed.Ok, Is.True, "shipped runtime bounds must parse: " + parsed.Error);
            return new SaveStore(root, new RealSaveFileSystem(), parsed.Value,
                MigrationTable.CreateDefault());
        }

        private GameObject Track(GameObject gameObject)
        {
            _ownedObjects.Add(gameObject);
            return gameObject;
        }

        private static PurchaseCatalog ShippedCatalog()
        {
            var source = Resources.Load<TextAsset>(MonetizationBootstrap.CatalogResourcePath);
            Assert.That(source, Is.Not.Null);
            var catalog = PurchaseCatalog.Parse(source.text);
            Assert.That(catalog.Problems, Is.Empty);
            return catalog;
        }

        private static RewardedPlacementCatalog ShippedPlacements(PurchaseCatalog catalog)
        {
            var source = Resources.Load<TextAsset>(MonetizationBootstrap.PlacementsResourcePath);
            Assert.That(source, Is.Not.Null);
            var placements = RewardedPlacementCatalog.Parse(source.text, catalog);
            Assert.That(placements.Problems, Is.Empty);
            return placements;
        }

        private static RewardedAdsConfig ConfiguredAds()
            => RewardedAdsConfig.Parse(@"{
              ""androidAppKey"": ""task-8-test-app"",
              ""androidRewardedAdUnitId"": ""task-8-shared-rewarded-unit""
            }", RuntimePlatform.Android);

        private static void AssertWardrobeNoAdTargets(WardrobeScreenView view, TapInput input)
        {
            Assert.That(view.PanelVisible, Is.True);
            Assert.That(input.Regions.Count, Is.EqualTo(3),
                "only Back, Buy, and Restore remain registered");
            Assert.That(input.Regions.IsRegistered("wardrobe.back"), Is.True);
            Assert.That(input.Regions.IsRegistered("wardrobe.buy"), Is.True);
            Assert.That(input.Regions.IsRegistered("wardrobe.restore"), Is.True);
            for (int i = 0; i < PlacementIds.Length; i++)
            {
                var card = view.transform.Find("WardrobePanel/TryOnStrip/TryOnCard_" +
                    PlacementIds[i]);
                Assert.That(card, Is.Not.Null);
                Assert.That(card.gameObject.activeInHierarchy, Is.True,
                    PlacementIds[i] + " card must remain visible");
                Assert.That(card.Find("Silhouette").gameObject.activeInHierarchy, Is.True);
                Assert.That(card.Find("ActionChip").gameObject.activeSelf, Is.False);
                Assert.That(input.Regions.IsRegistered(
                    "wardrobe.rewarded." + PlacementIds[i]), Is.False,
                    PlacementIds[i] + " must leave no ghost input target");
            }
        }

        private static Rect ProjectedScreenRect(RectTransform rect)
        {
            Assert.That(rect, Is.Not.Null);
            var canvas = rect.GetComponentInParent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                Assert.That(camera, Is.Not.Null);

            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 first = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            float xMin = first.x;
            float xMax = first.x;
            float yMin = first.y;
            float yMax = first.y;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                xMin = Mathf.Min(xMin, screen.x);
                xMax = Mathf.Max(xMax, screen.x);
                yMin = Mathf.Min(yMin, screen.y);
                yMax = Mathf.Max(yMax, screen.y);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static long ExactExpiry(IReadOnlyList<EntitlementGrant> leases, string id)
        {
            for (int i = 0; i < leases.Count; i++)
                if (string.Equals(leases[i].EntitlementId, id, StringComparison.Ordinal))
                    return leases[i].ExpiresAtUnixSeconds;
            return -1L;
        }

        private static void AssertBorrowed(WardrobeScreenView view, int cardIndex, bool expected)
        {
            var card = view.transform.Find("WardrobePanel/TryOnStrip/TryOnCard_" +
                PlacementIds[cardIndex]);
            Assert.That(card, Is.Not.Null);
            Assert.That(card.Find("BorrowedAccent").gameObject.activeSelf, Is.EqualTo(expected));
            Assert.That(card.Find("LockedLabel").gameObject.activeSelf, Is.EqualTo(!expected));
            Assert.That(card.Find("BorrowedLabel").gameObject.activeSelf, Is.EqualTo(expected));
            Assert.That(card.Find("SuccessLabel").gameObject.activeSelf, Is.EqualTo(expected));
        }

        private static readonly string[] PlacementIds =
        {
            "wardrobe_try_conductor",
            "wardrobe_try_engineer",
            "wardrobe_try_scarf",
            "wardrobe_try_goggles",
        };

        private sealed class MutableClock
        {
            public long Now;
            public MutableClock(long now) { Now = now; }
            public long Read() => Now;
        }

        private sealed class WardrobeRig
        {
            public readonly GameObject CanvasHost;
            public readonly GameObject InputHost;
            public readonly GameObject PumpHost;
            public readonly WardrobeScreenView View;
            public readonly TapInput Input;

            public WardrobeRig(GameObject canvasHost, GameObject inputHost, GameObject pumpHost,
                WardrobeScreenView view, TapInput input)
            {
                CanvasHost = canvasHost;
                InputHost = inputHost;
                PumpHost = pumpHost;
                View = view;
                Input = input;
            }
        }

        private sealed class TempStorageRoot : IStorageRoot, IDisposable
        {
            public string SaveDirectory { get; }
            public string CacheDirectory => SaveDirectory;

            public TempStorageRoot()
            {
                SaveDirectory = Path.Combine(Path.GetTempPath(),
                    "catmetro-rewarded-wiring-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(SaveDirectory);
            }

            public void Dispose()
            {
                try { Directory.Delete(SaveDirectory, true); }
                catch { }
            }
        }

        private sealed class Provider : IRewardedAdProvider
        {
            private Action<RewardedAdEvent> _events;
            public bool IsReady { get; set; } = true;
            public bool EmitLoadedDuringLoad { get; set; }
            public bool ShowSucceeds { get; set; } = true;
            public string AdUnitId { get; set; }
            public int InitializeCalls { get; private set; }
            public int LoadCalls { get; private set; }
            public int ShowCalls { get; private set; }
            public int DisposeCalls { get; private set; }
            public int EventAddCalls { get; private set; }
            public int EventRemoveCalls { get; private set; }
            public long LastAttemptId { get; private set; }
            public readonly List<ShowRecord> Shows = new List<ShowRecord>();

            public event Action<RewardedAdEvent> EventReceived
            {
                add { EventAddCalls++; _events += value; }
                remove { EventRemoveCalls++; _events -= value; }
            }

            public void Initialize() => InitializeCalls++;
            public void Load()
            {
                LoadCalls++;
                if (EmitLoadedDuringLoad)
                    _events?.Invoke(new RewardedAdEvent(RewardedAdEventKind.Loaded,
                        adUnitId: AdUnitId, adId: "task-8-ad", auctionId: "task-8-auction"));
            }
            public bool TryShow(long attemptId, string placementId)
            {
                ShowCalls++;
                LastAttemptId = attemptId;
                Shows.Add(new ShowRecord(attemptId, placementId, AdUnitId));
                return ShowSucceeds;
            }
            public void Emit(RewardedAdEventKind kind, long attemptId, string placementId)
                => _events?.Invoke(new RewardedAdEvent(kind, attemptId, placementId,
                    AdUnitId, "task-8-ad", "task-8-auction"));
            public void Dispose() => DisposeCalls++;
        }

        private readonly struct ShowRecord
        {
            public readonly long AttemptId;
            public readonly string PlacementId;
            public readonly string AdUnitId;

            public ShowRecord(long attemptId, string placementId, string adUnitId)
            {
                AttemptId = attemptId;
                PlacementId = placementId;
                AdUnitId = adUnitId;
            }
        }

        private sealed class BackendReporter : IPurchaseBackend, IAdEventReporter
        {
            private Action _readinessChanged;
            public BackendAvailability Availability => BackendAvailability.Ready;
            public bool IsReady => true;
            public bool ThrowOnReport { get; set; }
            public int EventRemoveCalls { get; private set; }
            public int PurchaseCalls { get; private set; }
            public int RestoreCalls { get; private set; }
            public int ReportCalls { get; private set; }

            public event Action ReadinessChanged
            {
                add { _readinessChanged += value; }
                remove { EventRemoveCalls++; _readinessChanged -= value; }
            }

            public void FetchProducts(Action<IReadOnlyList<StoreProductView>> onDone)
                => onDone?.Invoke(new[]
                {
                    new StoreProductView(ProductIds.Gate, "Conductor Coat",
                        new LocalizedPrice("$1.99")),
                });
            public void Purchase(string productId, Action<PurchaseResult> onDone)
            {
                PurchaseCalls++;
                onDone?.Invoke(PurchaseResult.Unavailable(productId, "test"));
            }
            public void Restore(Action<RestoreResult> onDone)
            {
                RestoreCalls++;
                onDone?.Invoke(new RestoreResult(RestoreOutcome.Unavailable));
            }
            public void RefreshEntitlements(Action<EntitlementSnapshot> onDone)
                => onDone?.Invoke(EntitlementSnapshot.Unreachable());
            public void Report(RewardedAdEvent adEvent)
            {
                ReportCalls++;
                if (ThrowOnReport) throw new InvalidOperationException("task-8 reporter fault");
            }
        }

        private sealed class BackendOnly : IPurchaseBackend
        {
            public BackendAvailability Availability => BackendAvailability.Ready;

            public void FetchProducts(Action<IReadOnlyList<StoreProductView>> onDone)
                => onDone?.Invoke(new[]
                {
                    new StoreProductView(ProductIds.Gate, "Conductor Coat",
                        new LocalizedPrice("$1.99")),
                });
            public void Purchase(string productId, Action<PurchaseResult> onDone)
                => onDone?.Invoke(PurchaseResult.Unavailable(productId, "test"));
            public void Restore(Action<RestoreResult> onDone)
                => onDone?.Invoke(new RestoreResult(RestoreOutcome.Unavailable));
            public void RefreshEntitlements(Action<EntitlementSnapshot> onDone)
                => onDone?.Invoke(EntitlementSnapshot.Unreachable());
        }
    }
}
