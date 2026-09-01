using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatMetro.Application.Save;
using CatMetro.Presentation.Cosmetics;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Screens;
using CatMetro.Presentation.Strings;
using CatMetro.Services.Ads;
using CatMetro.Services.Cosmetics;
using CatMetro.Services.Purchases;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    public sealed class WardrobeRewardedPlacementTests
    {
        private const string ConductorItemId = "outfit_conductor";
        private const string ConductorPlacementId = "wardrobe_try_conductor";
        private const float PhoneDpi = 408f;

        private readonly List<CosmeticProfileService> _profiles =
            new List<CosmeticProfileService>();
        private RewardedAdsWiringTests _builders;
        private RewardedAdsWiringTests.Provider _provider;
        private RewardedAdsWiringTests.BackendReporter _backend;
        private PurchaseService _purchases;
        private SaveStore _store;

        [SetUp]
        public void SetUp()
        {
            _builders = new RewardedAdsWiringTests();
            _builders.SetUp();
            var root = _builders.NewTempRoot();
            _store = _builders.NewStore(root);
            _store.Load();
            var clock = new RewardedAdsWiringTests.MutableClock(
                RewardedAdsWiringTests.InitialNow);
            _provider = new RewardedAdsWiringTests.Provider();
            _backend = new RewardedAdsWiringTests.BackendReporter
            {
                OfferConductorProduct = false,
            };
            _purchases = RewardedAdsWiringTests.NewService(_backend, clock);
            var composition = _builders.NewComposition(_purchases, _provider, _backend);
            composition.Bind();
            SaveRuntime.Install(_store);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_builders != null)
            {
                var cleanup = _builders.TearDown();
                while (cleanup.MoveNext()) yield return cleanup.Current;
            }
            for (int i = _profiles.Count - 1; i >= 0; i--)
                _profiles[i]?.Dispose();
            _profiles.Clear();
            _builders = null;
            _provider = null;
            _backend = null;
            _purchases = null;
            _store = null;
        }

        [UnityTest]
        public IEnumerator NoExactOffer_OmitsTheCardTargetPrimaryAndPhantomBand()
        {
            var cases = new[]
            {
                new NoOfferCase("absent placement", root =>
                    WardrobePurchaseFlowTests.Item(root, ConductorItemId)
                        .Remove("rewardedPlacementId"), providerReady: true),
                new NoOfferCase("mismatched placement", root =>
                    WardrobePurchaseFlowTests.Item(root, ConductorItemId)
                        ["rewardedPlacementId"] = "wardrobe_try_engineer", providerReady: true),
                new NoOfferCase("unavailable provider", null, providerReady: false),
            };

            for (int i = 0; i < cases.Length; i++)
            {
                _provider.IsReady = cases[i].ProviderReady;
                var rig = NewRig(NewProfile(cases[i].MutateCatalog));
                OpenAndLayout(rig);
                yield return null;
                Canvas.ForceUpdateCanvases();

                AssertNoRewardCandidate(rig, cases[i].Name);
            }
        }

        [UnityTest]
        public IEnumerator ExactOffer_MountsOneRealCardAndOne48DpPrimaryTarget()
        {
            var rig = NewRig(NewProfile());
            OpenAndLayout(rig);
            yield return null;
            Canvas.ForceUpdateCanvases();

            var card = ConductorCard(rig);
            Assert.That(rig.View.VisibleCards, Has.Count.EqualTo(1));
            Assert.That(card.Route, Is.EqualTo(CosmeticWardrobeRoute.Rewarded));
            Assert.That(card.DisplayedPriceText, Is.Empty);
            Assert.That(rig.Input.Regions.IsRegistered(
                "wardrobe.item.outfit_conductor"), Is.True);
            Assert.That(rig.Input.Regions.IsRegistered("wardrobe.primary"), Is.False);

            Tap(rig, card.ScreenRect);
            yield return null;
            Canvas.ForceUpdateCanvases();

            var primary = FindRect(rig.View, "PrimaryActionChip");
            var primaryRect = ProjectedScreenRect(primary);
            Assert.That(primary.gameObject.activeInHierarchy, Is.True);
            Assert.That(rig.Input.Regions.IsRegistered("wardrobe.primary"), Is.True);
            Assert.That(HudBands.MeetsMinTargetPx(primaryRect, PhoneDpi), Is.True,
                "the registered, painted primary action must meet the 48dp floor");
            Assert.That(rig.View.PrimaryActionText,
                Is.EqualTo(UiStrings.Get("wardrobe.action.rewarded")));
        }

        [UnityTest]
        public IEnumerator StartedDoesNotGrant_ExactRewardGrantsAndEquipsTheInitiatingCatOnce()
        {
            var profile = NewProfile();
            var rig = NewRig(profile);
            OpenAndLayout(rig);
            yield return null;
            Tap(rig, ProjectedScreenRect(FindRect(rig.View,
                "CatSelector-blue_siamese")));
            yield return null;
            Assert.That(profile.SelectedCatId, Is.EqualTo("blue_siamese"));
            Tap(rig, ConductorCard(rig).ScreenRect);
            yield return null;
            byte[] beforeStarted = ReadSaveBytes();
            Tap(rig, ProjectedScreenRect(FindRect(rig.View, "PrimaryActionChip")));

            Assert.That(_provider.ShowCalls, Is.EqualTo(1));
            Assert.That(_provider.LastAttemptId, Is.GreaterThan(0L));
            Assert.That(_provider.Shows.Single().PlacementId,
                Is.EqualTo(ConductorPlacementId));
            Assert.That(_purchases.IsUnlocked(ConductorItemId), Is.False,
                "Started is not a reward or purchase grant");
            Assert.That(string.IsNullOrEmpty(
                profile.Profile.LoadoutFor("blue_siamese").OutfitId), Is.True);
            Assert.That(string.IsNullOrEmpty(
                profile.Profile.LoadoutFor("red_tabby").OutfitId), Is.True);
            CollectionAssert.AreEqual(beforeStarted, ReadSaveBytes(),
                "Started alone cannot persist ownership or a loadout mutation");

            _provider.Emit(RewardedAdEventKind.Rewarded, _provider.LastAttemptId,
                ConductorPlacementId);
            yield return null;
            Canvas.ForceUpdateCanvases();

            Assert.That(_purchases.IsUnlocked(ConductorItemId), Is.True);
            Assert.That(profile.Profile.LoadoutFor("blue_siamese").OutfitId,
                Is.EqualTo(ConductorItemId),
                $"status={rig.View.StatusText}; selected={profile.SelectedCatId}; " +
                $"red={profile.Profile.LoadoutFor("red_tabby").OutfitId ?? "<null>"}");
            Assert.That(string.IsNullOrEmpty(
                profile.Profile.LoadoutFor("red_tabby").OutfitId), Is.True,
                "the exact completion equips only the cat that initiated the request");
            Assert.That(_purchases.Ledger.ExportLeases(), Has.Count.EqualTo(1));
            byte[] committed = File.ReadAllBytes(_store.SavePath);

            _provider.Emit(RewardedAdEventKind.Rewarded, _provider.LastAttemptId,
                ConductorPlacementId);
            yield return null;

            CollectionAssert.AreEqual(committed, File.ReadAllBytes(_store.SavePath),
                "a duplicate reward cannot save or equip a second time");
            Assert.That(_purchases.Ledger.ExportLeases(), Has.Count.EqualTo(1));
            Assert.That(profile.Profile.LoadoutFor("blue_siamese").OutfitId,
                Is.EqualTo(ConductorItemId));
            Assert.That(string.IsNullOrEmpty(
                profile.Profile.LoadoutFor("red_tabby").OutfitId), Is.True);
        }

        [UnityTest]
        public IEnumerator CloseReleasesBusy_LateExactRewardOwnsButNeverAutoEquips()
        {
            var profile = NewProfile();
            var rig = NewRig(profile);
            OpenAndLayout(rig);
            yield return null;
            Tap(rig, ConductorCard(rig).ScreenRect);
            yield return null;
            Tap(rig, ProjectedScreenRect(FindRect(rig.View, "PrimaryActionChip")));
            long attempt = _provider.LastAttemptId;
            byte[] beforeForeign = ReadSaveBytes();

            EmitForeignTerminals(attempt);
            Assert.That(_purchases.IsUnlocked(ConductorItemId), Is.False);
            Assert.That(string.IsNullOrEmpty(
                profile.Profile.LoadoutFor("red_tabby").OutfitId), Is.True);
            Assert.That(_purchases.Ledger.ExportLeases(), Is.Empty);
            Assert.That(ConductorCard(rig).Route,
                Is.EqualTo(CosmeticWardrobeRoute.Rewarded));
            Assert.That(rig.Input.Regions.IsRegistered("wardrobe.primary"), Is.True);
            CollectionAssert.AreEqual(beforeForeign, ReadSaveBytes(),
                "foreign terminal identities cannot mutate the mounted row or save");
            int restoresWhilePending = _backend.RestoreCalls;
            Tap(rig, ProjectedScreenRect(FindRect(rig.View, "RestoreChip")));
            Assert.That(_backend.RestoreCalls, Is.EqualTo(restoresWhilePending),
                "foreign terminal identities cannot release the in-flight operation gate");

            _provider.Emit(RewardedAdEventKind.Closed, attempt, ConductorPlacementId);
            yield return null;
            Canvas.ForceUpdateCanvases();

            Assert.That(rig.View.VisibleCards, Is.Empty,
                "the retained exact attempt makes this locked candidate temporarily unavailable");
            Assert.That(rig.Input.Regions.IsRegistered("wardrobe.primary"), Is.False);
            int restoresBefore = _backend.RestoreCalls;
            Tap(rig, ProjectedScreenRect(FindRect(rig.View, "RestoreChip")));
            Assert.That(_backend.RestoreCalls, Is.EqualTo(restoresBefore + 1),
                "Close must release the Wardrobe operation gate");

            _provider.Emit(RewardedAdEventKind.Rewarded, attempt, ConductorPlacementId);
            yield return null;
            Canvas.ForceUpdateCanvases();

            Assert.That(_purchases.IsUnlocked(ConductorItemId), Is.True);
            Assert.That(string.IsNullOrEmpty(
                profile.Profile.LoadoutFor("red_tabby").OutfitId), Is.True,
                "a genuinely later durable reward must not resurrect the closed UI equip action");
            var owned = ConductorCard(rig);
            Assert.That(owned.Route, Is.EqualTo(CosmeticWardrobeRoute.Equip));
            Assert.That(owned.DisplayedStatusText,
                Does.StartWith(UiStrings.Get("wardrobe.state.owned")));
            Assert.That(rig.Input.Regions.IsRegistered(
                "wardrobe.item.outfit_conductor"), Is.True);
            byte[] ownedBytes = File.ReadAllBytes(_store.SavePath);

            _provider.Emit(RewardedAdEventKind.Rewarded, attempt, ConductorPlacementId);
            _provider.Emit(RewardedAdEventKind.DisplayFailed, attempt, ConductorPlacementId);
            yield return null;

            CollectionAssert.AreEqual(ownedBytes, File.ReadAllBytes(_store.SavePath));
            Assert.That(string.IsNullOrEmpty(
                profile.Profile.LoadoutFor("red_tabby").OutfitId), Is.True);
            Assert.That(_purchases.Ledger.ExportLeases(), Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisplayFailureReleasesBusyAndAllLaterOrForeignTerminalsStayInert()
        {
            var profile = NewProfile();
            var rig = NewRig(profile);
            OpenAndLayout(rig);
            yield return null;
            Tap(rig, ConductorCard(rig).ScreenRect);
            yield return null;
            Tap(rig, ProjectedScreenRect(FindRect(rig.View, "PrimaryActionChip")));
            long attempt = _provider.LastAttemptId;
            byte[] beforeForeign = ReadSaveBytes();

            EmitForeignTerminals(attempt);
            Assert.That(_purchases.IsUnlocked(ConductorItemId), Is.False);
            Assert.That(string.IsNullOrEmpty(
                profile.Profile.LoadoutFor("red_tabby").OutfitId), Is.True);
            Assert.That(_purchases.Ledger.ExportLeases(), Is.Empty);
            Assert.That(ConductorCard(rig).Route,
                Is.EqualTo(CosmeticWardrobeRoute.Rewarded));
            Assert.That(rig.Input.Regions.IsRegistered("wardrobe.primary"), Is.True);
            CollectionAssert.AreEqual(beforeForeign, ReadSaveBytes(),
                "foreign terminal identities cannot mutate the mounted row or save");
            int restoresWhilePending = _backend.RestoreCalls;
            Tap(rig, ProjectedScreenRect(FindRect(rig.View, "RestoreChip")));
            Assert.That(_backend.RestoreCalls, Is.EqualTo(restoresWhilePending),
                "foreign terminal identities cannot release the in-flight operation gate");
            _provider.Emit(RewardedAdEventKind.DisplayFailed, attempt, ConductorPlacementId);
            yield return null;
            Canvas.ForceUpdateCanvases();

            Assert.That(_purchases.IsUnlocked(ConductorItemId), Is.False);
            Assert.That(string.IsNullOrEmpty(
                profile.Profile.LoadoutFor("red_tabby").OutfitId), Is.True);
            Assert.That(_purchases.Ledger.ExportLeases(), Is.Empty);
            Assert.That(ConductorCard(rig).Route,
                Is.EqualTo(CosmeticWardrobeRoute.Rewarded));
            int restoresBefore = _backend.RestoreCalls;
            Tap(rig, ProjectedScreenRect(FindRect(rig.View, "RestoreChip")));
            Assert.That(_backend.RestoreCalls, Is.EqualTo(restoresBefore + 1),
                "DisplayFailed must release the Wardrobe operation gate");

            _provider.Emit(RewardedAdEventKind.Rewarded, attempt, ConductorPlacementId);
            _provider.Emit(RewardedAdEventKind.DisplayFailed, attempt, ConductorPlacementId);
            yield return null;

            Assert.That(_purchases.IsUnlocked(ConductorItemId), Is.False);
            Assert.That(string.IsNullOrEmpty(
                profile.Profile.LoadoutFor("red_tabby").OutfitId), Is.True);
            Assert.That(_purchases.Ledger.ExportLeases(), Is.Empty);
        }

        private CosmeticProfileService NewProfile(Action<JObject> mutateCatalog = null)
        {
            var inventory = WardrobePurchaseFlowTests.ShippedInventory();
            var root = WardrobePurchaseFlowTests.ShippedCosmeticCatalogRoot();
            mutateCatalog?.Invoke(root);
            var catalog = CosmeticCatalog.Parse(root.ToString(), inventory.AssetIds,
                inventory.ProvenanceAssetIds);
            Assert.That(catalog.RejectedRowCount, Is.Zero);
            var profile = new CosmeticProfileService(catalog, inventory,
                new SaveStoreCosmeticProfilePersistence(_store), _purchases);
            _profiles.Add(profile);
            return profile;
        }

        private RewardedAdsWiringTests.WardrobeRig NewRig(CosmeticProfileService profile)
            => _builders.NewWardrobeRig(_purchases, profile, new RewardedAdCosmeticRoute());

        private static void OpenAndLayout(RewardedAdsWiringTests.WardrobeRig rig)
        {
            rig.View.Open();
            rig.View.LayoutForViewport(RewardedAdsWiringTests.PhoneSafeArea, PhoneDpi);
            Canvas.ForceUpdateCanvases();
        }

        private static CosmeticItemCardView ConductorCard(
            RewardedAdsWiringTests.WardrobeRig rig)
        {
            var cards = rig.View.VisibleCards.Where(card => card != null && card.IsActive)
                .ToArray();
            Assert.That(cards.Count(card => card.ItemId == ConductorItemId), Is.EqualTo(1));
            return cards.Single(card => card.ItemId == ConductorItemId);
        }

        private static void AssertNoRewardCandidate(RewardedAdsWiringTests.WardrobeRig rig,
            string caseName)
        {
            Assert.That(rig.View.VisibleCards, Is.Empty, caseName);
            Assert.That(rig.View.GetComponentsInChildren<CosmeticItemCardView>(true)
                .Any(card => card.gameObject.activeInHierarchy), Is.False, caseName);
            Assert.That(rig.Input.Regions.IsRegistered(
                "wardrobe.item.outfit_conductor"), Is.False, caseName);
            Assert.That(rig.Input.Regions.IsRegistered("wardrobe.primary"), Is.False, caseName);
            Assert.That(rig.Input.Regions.Count, Is.EqualTo(8),
                caseName + " left a ghost action region");
            Assert.That(rig.View.ItemsRectPx.height,
                Is.EqualTo(48f * HudBands.PxPerDp(PhoneDpi)).Within(1f),
                caseName + " retained more than the real empty-state band");
            var empty = rig.View.GetComponentsInChildren<TMP_Text>(true)
                .Single(label => label.name == "EmptyStateLabel");
            Assert.That(empty.gameObject.activeInHierarchy, Is.True, caseName);
        }

        private void EmitForeignTerminals(long attempt)
        {
            foreach (var kind in new[]
                     {
                         RewardedAdEventKind.Rewarded,
                         RewardedAdEventKind.Closed,
                         RewardedAdEventKind.DisplayFailed,
                     })
            {
                _provider.Emit(kind, attempt + 7L, ConductorPlacementId);
                _provider.Emit(kind, attempt, "wardrobe_try_engineer");
            }
        }

        private byte[] ReadSaveBytes() => File.Exists(_store.SavePath)
            ? File.ReadAllBytes(_store.SavePath)
            : Array.Empty<byte>();

        private static void Tap(RewardedAdsWiringTests.WardrobeRig rig, Rect rect)
        {
            Assert.That(rect.width, Is.GreaterThan(0f));
            Assert.That(rect.height, Is.GreaterThan(0f));
            Assert.That(rig.Input.HandleTapAtScreen(rect.center), Is.EqualTo(-3));
        }

        private static RectTransform FindRect(WardrobeScreenView view, string name)
        {
            var rect = view.GetComponentsInChildren<RectTransform>(true)
                .SingleOrDefault(candidate => candidate.name == name);
            Assert.That(rect, Is.Not.Null, "missing painted RectTransform " + name);
            return rect;
        }

        private static Rect ProjectedScreenRect(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
        }

        private readonly struct NoOfferCase
        {
            public string Name { get; }
            public Action<JObject> MutateCatalog { get; }
            public bool ProviderReady { get; }

            public NoOfferCase(string name, Action<JObject> mutateCatalog, bool providerReady)
            {
                Name = name;
                MutateCatalog = mutateCatalog;
                ProviderReady = providerReady;
            }
        }
    }
}
