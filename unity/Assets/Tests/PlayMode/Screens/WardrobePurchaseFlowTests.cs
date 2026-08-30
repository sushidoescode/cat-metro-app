using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CatMetro.Presentation.Cosmetics;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;
using CatMetro.Presentation.Strings;
using CatMetro.Services.Cosmetics;
using CatMetro.Services.Purchases;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CatMetro.Tests.PlayMode
{
    public sealed class WardrobePurchaseFlowTests
    {
        private const int Width = 917;
        private const int Height = 2048;
        private static readonly Rect PhoneSafeArea = new Rect(0f, 64f, Width, 1920f);

        private readonly List<CosmeticProfileService> _profiles =
            new List<CosmeticProfileService>();
        private GameObject _canvasHost;
        private GameObject _cameraHost;
        private RenderTexture _captureTarget;
        private WardrobeScreenView _view;
        private ChromeRegions _regions;
        private Canvas _canvas;

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _profiles.Count; i++) _profiles[i].Dispose();
            _profiles.Clear();
            if (_canvasHost != null) UnityEngine.Object.DestroyImmediate(_canvasHost);
            if (_cameraHost != null) UnityEngine.Object.DestroyImmediate(_cameraHost);
            if (_captureTarget != null)
            {
                _captureTarget.Release();
                UnityEngine.Object.DestroyImmediate(_captureTarget);
            }
            _canvasHost = null;
            _cameraHost = null;
            _captureTarget = null;
            _view = null;
        }

        [UnityTest]
        public IEnumerator StarterCats_ArePaintedRegisteredPersistFirst_AndRepaintBothPortraits()
        {
            var order = new List<string>();
            var persistence = new RecordingPersistence(DefaultProfile(), order);
            var setup = CreateSetup(persistence: persistence);
            setup.Profile.Changed += () => order.Add("publish");
            CreateView(setup);

            _view.Open();
            Layout();
            yield return null;

            foreach (var catId in new[] { "red_tabby", "blue_siamese", "yellow_longhair" })
            {
                var target = FindRect("CatSelector-" + catId);
                Assert.That(target.gameObject.activeInHierarchy, Is.True);
                Assert.That(_regions.IsRegistered("wardrobe.cat." + catId), Is.True);
                Assert.That(HudBands.MeetsMinTargetPx(ScreenRect(target), 408f), Is.True);
            }

            int before = persistence.ReplaceCalls;
            Tap(FindRect("CatSelector-blue_siamese"));
            yield return null;
            Assert.That(persistence.ReplaceCalls, Is.EqualTo(before + 1));
            Assert.That(order.TakeLast(2).ToArray(), Is.EqualTo(new[] { "persist", "publish" }),
                "selection is durable before observers see it");
            Assert.That(setup.Profile.SelectedCatId, Is.EqualTo("blue_siamese"));
            Assert.That(Portrait("LargePortrait").AppliedCatId, Is.EqualTo("blue_siamese"));

            _view.ShowEntry();
            Layout();
            yield return null;
            Assert.That(Portrait("EntryPortrait").AppliedCatId, Is.EqualTo("blue_siamese"));
        }

        [UnityTest]
        public IEnumerator Tabs_ProjectOnlyOneSlot_AndAccessoryIsOneUntappableEmptyState()
        {
            var setup = CreateSetup();
            CreateView(setup);
            _view.Open();
            Layout();
            yield return null;

            foreach (var slot in new[] { "outfit", "accessory", "frame" })
            {
                var target = FindRect("Tab-" + slot);
                Assert.That(target.gameObject.activeInHierarchy, Is.True);
                Assert.That(_regions.IsRegistered("wardrobe.tab." + slot), Is.True);
                Assert.That(HudBands.MeetsMinTargetPx(ScreenRect(target), 408f), Is.True);
            }
            Assert.That(_regions.IsRegistered("wardrobe.item.outfit_conductor"), Is.True);

            Tap(FindRect("Tab-accessory"));
            yield return null;
            Assert.That(ActiveCards().Count, Is.EqualTo(0));
            Assert.That(_regions.IsRegistered("wardrobe.item.outfit_conductor"), Is.False);
            Assert.That(_regions.IsRegistered("wardrobe.item.frame_brass"), Is.False);
            Assert.That(_regions.IsRegistered("wardrobe.item.frame_lantern"), Is.False);
            var empty = FindText("EmptyStateLabel");
            Assert.That(empty.gameObject.activeInHierarchy, Is.True);
            Assert.That(empty.text, Is.EqualTo(UiStrings.Get("wardrobe.empty")));
            Assert.That(_regions.IsRegistered("wardrobe.empty"), Is.False);
        }

        [UnityTest]
        public IEnumerator LockedCardTap_PreviewsCompleteCoat_WithoutSavingOrEquipping()
        {
            var persistence = new RecordingPersistence(DefaultProfile());
            var setup = CreateSetup(persistence: persistence);
            CreateView(setup);
            _view.Open();
            Layout();
            yield return null;

            int before = persistence.ReplaceCalls;
            Tap(CardRect("outfit_conductor"));
            yield return null;

            Assert.That(persistence.ReplaceCalls, Is.EqualTo(before));
            Assert.That(setup.Profile.Profile.LoadoutFor("red_tabby").OutfitId, Is.Empty);
            Assert.That(setup.Profile.CurrentPortrait.OutfitAssetId, Is.Empty);
            Assert.That(Portrait("LargePortrait").AppliedOutfitAssetId,
                Is.EqualTo("outfit.conductor"));
            Assert.That(Portrait("EntryPortrait").AppliedOutfitAssetId, Is.Empty);
            Assert.That(_regions.IsRegistered("wardrobe.primary"), Is.True);
        }

        [UnityTest]
        public IEnumerator Purchase_RechecksAuthorityThenAtomicallyEquips_AndStaysOpen()
        {
            var persistence = new RecordingPersistence(DefaultProfile());
            var setup = CreateSetup(persistence: persistence);
            setup.Backend.GrantOnPurchase = true;
            CreateView(setup);
            _view.Open();
            Layout();
            yield return null;

            Tap(CardRect("outfit_conductor"));
            Tap(FindRect("PrimaryActionChip"));
            yield return null;

            Assert.That(setup.Backend.PurchaseCalls, Is.EqualTo(1));
            Assert.That(setup.Backend.LastPurchasedProductId, Is.EqualTo("cm_outfit_conductor"));
            Assert.That(setup.Purchases.IsUnlocked("outfit_conductor"), Is.True);
            Assert.That(setup.Profile.Profile.LoadoutFor("red_tabby").OutfitId,
                Is.EqualTo("outfit_conductor"));
            Assert.That(Portrait("LargePortrait").AppliedOutfitAssetId,
                Is.EqualTo("outfit.conductor"));
            Assert.That(_view.PanelVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator EntitlementLandingWithSaveRefusal_KeepsAccessButRollsBackPreviewAndLoadout()
        {
            var persistence = new RecordingPersistence(DefaultProfile()) { Refuse = true };
            var setup = CreateSetup(persistence: persistence);
            setup.Backend.GrantOnPurchase = true;
            CreateView(setup);
            _view.Open();
            Layout();
            yield return null;

            Tap(CardRect("outfit_conductor"));
            Tap(FindRect("PrimaryActionChip"));
            yield return null;

            Assert.That(setup.Purchases.IsUnlocked("outfit_conductor"), Is.True,
                "the purchase ledger is not rolled back by a profile disk refusal");
            Assert.That(setup.Profile.Profile.LoadoutFor("red_tabby").OutfitId, Is.Empty);
            Assert.That(setup.Profile.CurrentPortrait.OutfitAssetId, Is.Empty);
            Assert.That(Portrait("LargePortrait").AppliedOutfitAssetId, Is.Empty);
            Assert.That(StatusText(), Is.EqualTo(UiStrings.Get("wardrobe.status.save.failed")));
        }

        [UnityTest]
        public IEnumerator NonAuthoritativePurchaseOutcomes_NeverGrantOrEquip_AndStayTruthful()
        {
            var setup = CreateSetup();
            CreateView(setup);
            _view.Open();
            Layout();
            yield return null;
            Tap(CardRect("outfit_conductor"));

            var cases = new[]
            {
                (PurchaseOutcome.UserCancelled, "wardrobe.status.cancelled"),
                (PurchaseOutcome.Pending, "wardrobe.status.pending"),
                (PurchaseOutcome.SuccessCandidate, "wardrobe.status.unconfirmed"),
                (PurchaseOutcome.Unavailable, "wardrobe.status.unavailable"),
                (PurchaseOutcome.Busy, "wardrobe.status.opening"),
                (PurchaseOutcome.Failure, "wardrobe.status.unavailable"),
            };
            foreach (var testCase in cases)
            {
                setup.Backend.NextPurchaseOutcome = testCase.Item1;
                setup.Backend.GrantOnPurchase = false;
                Tap(FindRect("PrimaryActionChip"));
                yield return null;
                Assert.That(setup.Purchases.IsUnlocked("outfit_conductor"), Is.False);
                Assert.That(setup.Profile.Profile.LoadoutFor("red_tabby").OutfitId, Is.Empty);
                Assert.That(StatusText(), Is.EqualTo(UiStrings.Get(testCase.Item2)),
                    testCase.Item1.ToString());
            }
        }

        [UnityTest]
        public IEnumerator SavedCoat_LapsesWithoutSaveOrDesiredErase_AndRestoresAutomatically()
        {
            var persistence = new RecordingPersistence(ProfileWith(outfit: "outfit_conductor"));
            var setup = CreateSetup(persistence: persistence);
            setup.Backend.WithStoreEntitlement("outfit_conductor");
            setup.Purchases.Refresh();
            CreateView(setup);
            _view.Open();
            Layout();
            yield return null;
            Assert.That(Portrait("LargePortrait").AppliedOutfitAssetId,
                Is.EqualTo("outfit.conductor"));

            int before = persistence.ReplaceCalls;
            setup.Backend.RevokeAllStoreEntitlements();
            setup.Purchases.RefreshEntitlements();
            yield return null;
            Assert.That(Portrait("LargePortrait").AppliedOutfitAssetId, Is.Empty);
            Assert.That(setup.Profile.Profile.LoadoutFor("red_tabby").OutfitId,
                Is.EqualTo("outfit_conductor"));
            Assert.That(persistence.ReplaceCalls, Is.EqualTo(before));

            setup.Backend.WithStoreEntitlement("outfit_conductor");
            setup.Purchases.RefreshEntitlements();
            yield return null;
            Assert.That(Portrait("LargePortrait").AppliedOutfitAssetId,
                Is.EqualTo("outfit.conductor"));
            Assert.That(persistence.ReplaceCalls, Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator BorrowedAccess_KeepsNamedPermanentPriceAndCountdown_ThenPurchaseWinsExpiry()
        {
            var setup = CreateSetup(persistence: new RecordingPersistence(
                ProfileWith(outfit: "outfit_conductor")));
            setup.Backend.GrantOnPurchase = true;
            CreateView(setup);
            _view.Open();
            Layout();
            yield return null;

            Assert.That(setup.Purchases.GrantRewardedAdEntitlement("outfit_conductor"),
                Is.EqualTo(AdGrantOutcome.Granted));
            yield return null;
            var card = Card("outfit_conductor");
            Assert.That(CardString(card, "DisplayedPriceText"), Is.EqualTo("CA$2.79"));
            Assert.That(CardString(card, "DisplayedStatusText"), Does.Contain("remaining"));
            Assert.That(CardString(card, "DisplayedStatusText"), Does.Not.Contain("0s"));

            Tap(CardRect("outfit_conductor"));
            Tap(FindRect("PrimaryActionChip"));
            yield return null;
            setup.Clock += 90_000L;
            setup.Purchases.PruneExpiredLeases();
            Assert.That(setup.Purchases.IsUnlocked("outfit_conductor"), Is.True);
            Assert.That(setup.Purchases.SecondsUntilExpiry("outfit_conductor"), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator RewardedCompletion_RechecksAuthority_AndEarnInstructionNeverGrants()
        {
            var catalogRoot = ShippedCosmeticCatalogRoot();
            Item(catalogRoot, "outfit_conductor")["rewardedPlacementId"] = "wardrobe.borrow.coat";
            var noPrice = new WardrobeBackend();
            var setup = CreateSetup(backend: noPrice, catalogRoot: catalogRoot);
            var rewarded = new RecordingRewardedRoute(true);
            CreateView(setup, rewarded);
            _view.Open();
            Layout();
            yield return null;

            Tap(CardRect("outfit_conductor"));
            Tap(FindRect("PrimaryActionChip"));
            yield return null;
            Assert.That(rewarded.RequestCalls, Is.EqualTo(1));
            Assert.That(setup.Profile.Profile.LoadoutFor("red_tabby").OutfitId, Is.Empty,
                "completion alone is not authority");

            rewarded.BeforeCompletion = () =>
                setup.Purchases.GrantRewardedAdEntitlement("outfit_conductor");
            Tap(FindRect("PrimaryActionChip"));
            yield return null;
            Assert.That(setup.Profile.Profile.LoadoutFor("red_tabby").OutfitId,
                Is.EqualTo("outfit_conductor"));

            var earnedRoot = ShippedCosmeticCatalogRoot();
            var conductor = Item(earnedRoot, "outfit_conductor");
            conductor["acquisition"] = "earned";
            conductor.Remove("entitlementId");
            conductor.Remove("productId");
            conductor["earnInstructionKey"] = "cosmetics.earn.conductor";
            ResetView();
            var earnedSetup = CreateSetup(backend: new WardrobeBackend(), catalogRoot: earnedRoot);
            CreateView(earnedSetup);
            _view.Open();
            Layout();
            yield return null;
            Tap(CardRect("outfit_conductor"));
            Tap(FindRect("PrimaryActionChip"));
            Assert.That(earnedSetup.Profile.IsAccessible("outfit_conductor"), Is.False);
            Assert.That(StatusText(), Is.EqualTo(UiStrings.Get("cosmetics.earn.conductor")));
        }

        [UnityTest]
        public IEnumerator RestoreRefreshesAuthorityOnly_AndReportsOtherOrEmptyTruthfully()
        {
            var persistence = new RecordingPersistence(DefaultProfile());
            var setup = CreateSetup(persistence: persistence);
            CreateView(setup);
            _view.Open();
            Layout();
            yield return null;

            setup.Backend.RestoreEntitlements = new[] { "frame_brass" };
            Tap(FindRect("RestoreChip"));
            yield return null;
            Assert.That(setup.Backend.RestoreCalls, Is.EqualTo(1));
            Assert.That(setup.Profile.Profile.LoadoutFor("red_tabby").FrameId, Is.Empty);
            Assert.That(StatusText(), Is.EqualTo(UiStrings.Get("wardrobe.status.restored")));

            setup.Backend.RevokeAllStoreEntitlements();
            setup.Backend.RestoreEntitlements = Array.Empty<string>();
            Tap(FindRect("RestoreChip"));
            yield return null;
            Assert.That(StatusText(), Is.EqualTo(UiStrings.Get("wardrobe.status.none")));
        }

        [UnityTest]
        public IEnumerator MissingProductAndAsset_CompactCardsWithoutBlankChildrenOrGhostRegions()
        {
            var backend = new WardrobeBackend().WithProduct("cm_frame_lantern", "€0.99");
            var inventory = ShippedInventory();
            var catalog = ShippedCatalog(inventory);
            var sparseInventoryRoot = ShippedInventoryRoot();
            Asset(sparseInventoryRoot, "frame.brass")["rendererToken"] = "unsupported.frame";
            var sparseInventory = CosmeticAssetInventory.Parse(sparseInventoryRoot.ToString(),
                CosmeticPortraitPainter.SupportedRendererTokens);
            var setup = CreateSetup(backend: backend, catalog: catalog, inventory: sparseInventory);
            CreateView(setup);
            _view.Open();
            Layout();
            yield return null;
            Tap(FindRect("Tab-frame"));
            yield return null;

            var cards = ActiveCards();
            Assert.That(cards.Count, Is.EqualTo(1));
            Assert.That(CardString(cards[0], "ItemId"), Is.EqualTo("frame_lantern"));
            Assert.That(_regions.IsRegistered("wardrobe.item.frame_brass"), Is.False);
            Assert.That(_regions.IsRegistered("wardrobe.item.frame_lantern"), Is.True);
            Assert.That(cards[0].transform.parent.childCount, Is.EqualTo(1),
                "omission leaves no inactive blank card child");

            setup.Backend.WithStoreEntitlement("frame_brass");
            setup.Purchases.RefreshEntitlements();
            ResetView();
            var accessibleSetup = CreateSetup(backend: setup.Backend);
            accessibleSetup.Backend.RemoveProduct("cm_frame_brass");
            accessibleSetup.Purchases.Refresh();
            CreateView(accessibleSetup);
            _view.Open();
            Layout();
            yield return null;
            Tap(FindRect("Tab-frame"));
            Assert.That(ActiveCards().Any(card => CardString(card, "ItemId") == "frame_brass"),
                Is.True, "accessible content remains visible without a live price");
        }

        [UnityTest]
        public IEnumerator RegionsAreExactAcrossRebuildDisableEnableHideAndDestroy()
        {
            var setup = CreateSetup();
            CreateView(setup);
            _view.Open();
            Layout();
            yield return null;
            AssertCoreRegions(shouldExist: true);

            Tap(FindRect("Tab-frame"));
            yield return null;
            Assert.That(_regions.IsRegistered("wardrobe.item.outfit_conductor"), Is.False);
            Assert.That(_regions.IsRegistered("wardrobe.item.frame_brass"), Is.True);
            Assert.That(_regions.IsRegistered("wardrobe.item.frame_lantern"), Is.True);

            _view.gameObject.SetActive(false);
            Assert.That(_regions.Count, Is.EqualTo(0));
            _view.gameObject.SetActive(true);
            yield return null;
            AssertCoreRegions(shouldExist: true);
            Assert.That(_regions.IsRegistered("wardrobe.item.frame_brass"), Is.True);

            _view.Hide();
            Assert.That(_regions.Count, Is.EqualTo(0));
            _view.Open();
            Layout();
            yield return null;
            AssertCoreRegions(shouldExist: true);
            UnityEngine.Object.Destroy(_view.gameObject);
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator CaptureEvidence_EmitsAllPhoneStatesAndPaintedPixelProof_WhenArmed()
        {
            string directory = Environment.GetEnvironmentVariable("CM_WARDROBE_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory))
            {
                Assert.Pass("capture rig disarmed — set CM_WARDROBE_CAPTURE_DIR");
                yield break;
            }

            Directory.CreateDirectory(directory);
            var persistence = new RecordingPersistence(DefaultProfile());
            var setup = CreateSetup(persistence: persistence);
            setup.Backend.GrantOnPurchase = true;
            setup.Backend.WithProduct("cm_frame_brass", "€0.99")
                .WithProduct("cm_frame_lantern", "¥120");
            CreateCamera();
            CreateView(setup, camera: _cameraHost.GetComponent<Camera>());
            yield return null;
            _view.Open();
            Layout();
            yield return null;

            var cardRootRect = ScreenRect(CardRect("outfit_conductor"));
            var exposedCardRect = CardScreenRect("outfit_conductor");
            Assert.That(exposedCardRect.center.x, Is.EqualTo(cardRootRect.center.x).Within(1f),
                "card read-back must expose camera-converted screen coordinates");
            Assert.That(exposedCardRect.center.y, Is.EqualTo(cardRootRect.center.y).Within(1f),
                "card read-back must expose camera-converted screen coordinates");
            yield return null;

            var captures = new List<string>();
            captures.Add(Capture(directory, "wardrobe-plain.png"));
            AssertPortraitHasInk(captures.Last(), FindRect("LargePortrait"), navy: false,
                brass: false, red: true);
            Tap(CardRect("outfit_conductor"));
            yield return null;
            captures.Add(Capture(directory, "wardrobe-locked-preview.png"));
            AssertRectCenterColor(captures.Last(), FindRect("ItemBadge"),
                new Color32(34, 48, 74, 255), "outfit card category swatch");
            AssertPortraitHasInk(captures.Last(), FindRect("LargePortrait"), navy: true,
                brass: true);
            Tap(FindRect("PrimaryActionChip"));
            yield return null;
            captures.Add(Capture(directory, "wardrobe-purchased-equipped.png"));

            Tap(FindRect("Tab-frame"));
            yield return null;
            Tap(CardRect("frame_brass"));
            Tap(FindRect("PrimaryActionChip"));
            yield return null;
            captures.Add(Capture(directory, "wardrobe-frame-brass.png"));
            AssertPortraitHasInk(captures.Last(), FindRect("LargePortrait"), navy: false,
                brass: true);

            Tap(CardRect("frame_lantern"));
            Tap(FindRect("PrimaryActionChip"));
            yield return null;
            captures.Add(Capture(directory, "wardrobe-frame-lantern.png"));
            AssertPortraitHasInk(captures.Last(), FindRect("LargePortrait"), navy: true,
                brass: false);

            setup.Backend.RevokeAllStoreEntitlements();
            setup.Purchases.RefreshEntitlements();
            yield return null;
            Assert.That(StatusText(), Is.Not.EqualTo(UiStrings.Get("wardrobe.state.equipped")),
                "an authority lapse must clear the stale equipped operation status");
            captures.Add(Capture(directory, "wardrobe-lapsed.png"));
            setup.Backend.WithStoreEntitlement("outfit_conductor")
                .WithStoreEntitlement("frame_lantern");
            setup.Purchases.RefreshEntitlements();
            yield return null;
            captures.Add(Capture(directory, "wardrobe-restored.png"));
            string contact = MakeContactSheet(directory, captures);

            foreach (var path in captures.Append(contact))
                Assert.That(new FileInfo(path).Length, Is.GreaterThan(10_000), path);
        }

        private Setup CreateSetup(RecordingPersistence persistence = null,
            WardrobeBackend backend = null, JObject catalogRoot = null,
            CosmeticCatalog catalog = null, CosmeticAssetInventory inventory = null)
        {
            inventory ??= ShippedInventory();
            catalog ??= catalogRoot == null
                ? ShippedCatalog(inventory)
                : CosmeticCatalog.Parse(catalogRoot.ToString(), inventory.AssetIds,
                    inventory.ProvenanceAssetIds);
            backend ??= new WardrobeBackend().WithProduct("cm_outfit_conductor", "CA$2.79")
                .WithProduct("cm_frame_brass", "€0.99")
                .WithProduct("cm_frame_lantern", "¥120");
            long clock = 1_700_000_000L;
            var purchases = new PurchaseService(ShippedPurchaseCatalog(), backend, () => clock);
            backend.Catalog = purchases.Catalog;
            persistence ??= new RecordingPersistence(DefaultProfile());
            var profile = new CosmeticProfileService(catalog, inventory, persistence, purchases);
            _profiles.Add(profile);
            purchases.Refresh();
            return new Setup(profile, purchases, backend, persistence,
                () => clock, value => clock = value);
        }

        private void CreateView(Setup setup, ICosmeticRewardedRoute rewarded = null,
            Camera camera = null)
        {
            _canvasHost = new GameObject("WardrobeTestCanvas");
            _canvas = _canvasHost.AddComponent<Canvas>();
            _canvas.renderMode = camera == null ? RenderMode.ScreenSpaceOverlay
                : RenderMode.ScreenSpaceCamera;
            if (camera != null)
            {
                _canvas.worldCamera = camera;
                _canvas.planeDistance = 1f;
            }
            _regions = new ChromeRegions();
            rewarded ??= new DisabledCosmeticRewardedRoute();
            var method = typeof(WardrobeScreenView).GetMethod("Create",
                BindingFlags.Public | BindingFlags.Static, null,
                new[]
                {
                    typeof(Transform), typeof(PurchaseService),
                    typeof(CosmeticProfileService), typeof(ICosmeticRewardedRoute),
                }, null);
            _view = method == null
                ? WardrobeScreenView.Create(_canvas.transform, setup.Purchases)
                : (WardrobeScreenView)method.Invoke(null,
                    new object[] { _canvas.transform, setup.Purchases, setup.Profile, rewarded });
            _view.Attach(_regions);
        }

        private void ResetView()
        {
            if (_canvasHost != null) UnityEngine.Object.DestroyImmediate(_canvasHost);
            _canvasHost = null;
            _view = null;
            _regions = null;
        }

        private void CreateCamera()
        {
            _cameraHost = new GameObject("WardrobeCaptureCamera");
            var camera = _cameraHost.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.08f, 0.05f, 1f);
            _captureTarget = new RenderTexture(Width, Height, 24);
            camera.targetTexture = _captureTarget;
        }

        private void Layout()
        {
            _view.LayoutForViewport(PhoneSafeArea, 408f);
            Canvas.ForceUpdateCanvases();
        }

        private RectTransform FindRect(string name)
        {
            var result = _view.GetComponentsInChildren<RectTransform>(true)
                .SingleOrDefault(rect => rect.name == name);
            Assert.That(result, Is.Not.Null, "missing painted RectTransform " + name);
            return result;
        }

        private TMP_Text FindText(string name)
        {
            var result = _view.GetComponentsInChildren<TMP_Text>(true)
                .SingleOrDefault(label => label.name == name);
            Assert.That(result, Is.Not.Null, "missing painted label " + name);
            return result;
        }

        private CosmeticPortraitView Portrait(string name)
        {
            var result = _view.GetComponentsInChildren<CosmeticPortraitView>(true)
                .SingleOrDefault(portrait => portrait.name == name);
            Assert.That(result, Is.Not.Null, "missing shared portrait " + name);
            return result;
        }

        private IReadOnlyList<MonoBehaviour> ActiveCards() => _view
            .GetComponentsInChildren<MonoBehaviour>(true)
            .Where(component => component.GetType().Name == "CosmeticItemCardView"
                && component.gameObject.activeInHierarchy).ToArray();

        private MonoBehaviour Card(string itemId)
        {
            var result = ActiveCards().SingleOrDefault(card =>
                CardString(card, "ItemId") == itemId);
            Assert.That(result, Is.Not.Null, "missing painted card " + itemId);
            return result;
        }

        private RectTransform CardRect(string itemId)
        {
            var card = Card(itemId);
            var root = card.GetType().GetProperty("RootTransform")?.GetValue(card)
                as RectTransform;
            Assert.That(root, Is.Not.Null, "card read-back must expose its actual root");
            return root;
        }

        private Rect CardScreenRect(string itemId)
        {
            var card = Card(itemId);
            var property = card.GetType().GetProperty("ScreenRect");
            Assert.That(property, Is.Not.Null, "card read-back must expose its actual screen rect");
            return (Rect)property.GetValue(card);
        }

        private static string CardString(MonoBehaviour card, string property)
        {
            var info = card.GetType().GetProperty(property);
            Assert.That(info, Is.Not.Null, "missing card read-back " + property);
            return info.GetValue(card)?.ToString() ?? string.Empty;
        }

        private void Tap(RectTransform target)
        {
            var rect = ScreenRect(target);
            Assert.That(rect.width, Is.GreaterThan(0f));
            Assert.That(_regions.TryResolve(rect.center, out var action), Is.True,
                target.name + " is painted but not routed");
            action();
        }

        private Rect ScreenRect(RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : _canvas.worldCamera;
            var bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
        }

        private string StatusText() => FindText("StatusLabel").text;

        private void AssertCoreRegions(bool shouldExist)
        {
            foreach (var id in new[]
                     {
                         "wardrobe.back", "wardrobe.restore", "wardrobe.cat.red_tabby",
                         "wardrobe.cat.blue_siamese", "wardrobe.cat.yellow_longhair",
                         "wardrobe.tab.outfit", "wardrobe.tab.accessory", "wardrobe.tab.frame",
                     })
                Assert.That(_regions.IsRegistered(id), Is.EqualTo(shouldExist), id);
        }

        private string Capture(string directory, string name)
        {
            Canvas.ForceUpdateCanvases();
            var camera = _cameraHost.GetComponent<Camera>();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = _captureTarget;
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            string path = Path.Combine(directory, name);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            return path;
        }

        private void AssertPortraitHasInk(string path, RectTransform portrait, bool navy,
            bool brass, bool red = false)
        {
            var bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(texture.LoadImage(bytes), Is.True);
            var pixels = texture.GetPixels32();
            var rect = ScreenRect(portrait);
            int xMin = Mathf.Clamp(Mathf.FloorToInt(rect.xMin), 0, Width - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(rect.xMax), 1, Width);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(rect.yMin), 0, Height - 1);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(rect.yMax), 1, Height);
            int navyCount = 0;
            int brassCount = 0;
            int redCount = 0;
            for (int y = yMin; y < yMax; y++)
            for (int x = xMin; x < xMax; x++)
            {
                var pixel = pixels[y * Width + x];
                if (Close(pixel, new Color32(24, 43, 67, 255), 35)) navyCount++;
                if (Close(pixel, new Color32(225, 170, 58, 255), 40)) brassCount++;
                if (Close(pixel, new Color32(185, 74, 58, 255), 40)) redCount++;
            }
            UnityEngine.Object.DestroyImmediate(texture);
            if (navy) Assert.That(navyCount, Is.GreaterThan(100), "navy coat/frame ink");
            if (brass) Assert.That(brassCount, Is.GreaterThan(20), "brass detail/frame ink");
            if (red) Assert.That(redCount, Is.GreaterThan(100), "selected red cat ink");
        }

        private void AssertRectCenterColor(string path, RectTransform target,
            Color32 expected, string message)
        {
            var bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(texture.LoadImage(bytes), Is.True);
            var rect = ScreenRect(target);
            int x = Mathf.Clamp(Mathf.RoundToInt(rect.center.x), 0, Width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(rect.center.y), 0, Height - 1);
            var actual = texture.GetPixel(x, y);
            UnityEngine.Object.DestroyImmediate(texture);
            Assert.That(Close((Color32)actual, expected, 20), Is.True,
                message + $" expected {expected} but painted {(Color32)actual}");
        }

        private static bool Close(Color32 actual, Color32 expected, int tolerance) =>
            Math.Abs(actual.r - expected.r) <= tolerance
            && Math.Abs(actual.g - expected.g) <= tolerance
            && Math.Abs(actual.b - expected.b) <= tolerance;

        private static string MakeContactSheet(string directory, IReadOnlyList<string> paths)
        {
            const int thumbWidth = Width / 4;
            const int thumbHeight = Height / 4;
            var sheet = new Texture2D(thumbWidth * 4, thumbHeight * 2,
                TextureFormat.RGBA32, false);
            for (int i = 0; i < paths.Count; i++)
            {
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                source.LoadImage(File.ReadAllBytes(paths[i]));
                var scaled = Scale(source, thumbWidth, thumbHeight);
                int x = (i % 4) * thumbWidth;
                int y = (1 - i / 4) * thumbHeight;
                sheet.SetPixels32(x, y, thumbWidth, thumbHeight, scaled);
                UnityEngine.Object.DestroyImmediate(source);
            }
            sheet.Apply();
            string path = Path.Combine(directory, "wardrobe-contact-sheet.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
            return path;
        }

        private static Color32[] Scale(Texture2D source, int width, int height)
        {
            var result = new Color32[width * height];
            var pixels = source.GetPixels32();
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int sourceX = x * source.width / width;
                int sourceY = y * source.height / height;
                result[y * width + x] = pixels[sourceY * source.width + sourceX];
            }
            return result;
        }

        private static CosmeticProfileSnapshot DefaultProfile() => new CosmeticProfileSnapshot(
            "red_tabby", null, null, new[]
            {
                new CosmeticLoadout("red_tabby", "", "", ""),
                new CosmeticLoadout("blue_siamese", "", "", ""),
                new CosmeticLoadout("yellow_longhair", "", "", ""),
            });

        private static CosmeticProfileSnapshot ProfileWith(string outfit = "", string frame = "")
            => new CosmeticProfileSnapshot("red_tabby", null, null, new[]
            {
                new CosmeticLoadout("red_tabby", outfit, "", frame),
            });

        private static CosmeticAssetInventory ShippedInventory() =>
            CosmeticAssetInventory.Parse(Resources.Load<TextAsset>("Cosmetics/portrait_assets").text,
                CosmeticPortraitPainter.SupportedRendererTokens);

        private static CosmeticCatalog ShippedCatalog(CosmeticAssetInventory inventory) =>
            CosmeticCatalog.Parse(Resources.Load<TextAsset>("Cosmetics/cosmetic_catalog").text,
                inventory.AssetIds, inventory.ProvenanceAssetIds);

        private static PurchaseCatalog ShippedPurchaseCatalog() => PurchaseCatalog.Parse(
            Resources.Load<TextAsset>("Monetization/product_catalog").text);

        private static JObject ShippedCosmeticCatalogRoot() => JObject.Parse(
            Resources.Load<TextAsset>("Cosmetics/cosmetic_catalog").text);

        private static JObject ShippedInventoryRoot() => JObject.Parse(
            Resources.Load<TextAsset>("Cosmetics/portrait_assets").text);

        private static JObject Item(JObject root, string id) => root["items"].Children<JObject>()
            .Single(row => (string)row["id"] == id);

        private static JObject Asset(JObject root, string id) => root["assets"].Children<JObject>()
            .Single(row => (string)row["assetId"] == id);

        private sealed class Setup
        {
            private readonly Func<long> _readClock;
            private readonly Action<long> _writeClock;
            public CosmeticProfileService Profile { get; }
            public PurchaseService Purchases { get; }
            public WardrobeBackend Backend { get; }
            public RecordingPersistence Persistence { get; }
            public long Clock { get => _readClock(); set => _writeClock(value); }

            public Setup(CosmeticProfileService profile, PurchaseService purchases,
                WardrobeBackend backend, RecordingPersistence persistence,
                Func<long> readClock, Action<long> writeClock)
            {
                Profile = profile;
                Purchases = purchases;
                Backend = backend;
                Persistence = persistence;
                _readClock = readClock;
                _writeClock = writeClock;
            }
        }

        private sealed class RecordingPersistence : ICosmeticProfilePersistence
        {
            private readonly List<string> _order;
            private CosmeticProfileSnapshot _snapshot;
            public bool Refuse { get; set; }
            public int ReplaceCalls { get; private set; }

            public RecordingPersistence(CosmeticProfileSnapshot snapshot, List<string> order = null)
            {
                _snapshot = snapshot;
                _order = order;
            }

            public bool TryLoad(out CosmeticProfileSnapshot snapshot)
            {
                snapshot = _snapshot;
                return true;
            }

            public bool TryReplace(CosmeticProfileSnapshot snapshot)
            {
                ReplaceCalls++;
                _order?.Add("persist");
                if (Refuse) return false;
                _snapshot = snapshot;
                return true;
            }
        }

        private sealed class RecordingRewardedRoute : ICosmeticRewardedRoute
        {
            private readonly bool _canOffer;
            public Action BeforeCompletion { get; set; }
            public int RequestCalls { get; private set; }

            public RecordingRewardedRoute(bool canOffer) => _canOffer = canOffer;
            public bool CanOffer(string placementId, string entitlementId) => _canOffer;
            public void Request(string placementId, Action completed)
            {
                RequestCalls++;
                BeforeCompletion?.Invoke();
                completed?.Invoke();
            }
        }

        private sealed class WardrobeBackend : IPurchaseBackend
        {
            private readonly Dictionary<string, StoreProductView> _products =
                new Dictionary<string, StoreProductView>(StringComparer.Ordinal);
            private readonly HashSet<string> _storeEntitlements =
                new HashSet<string>(StringComparer.Ordinal);
            public PurchaseCatalog Catalog { get; set; }
            public BackendAvailability Availability { get; set; } = BackendAvailability.Ready;
            public PurchaseOutcome NextPurchaseOutcome { get; set; } =
                PurchaseOutcome.SuccessCandidate;
            public bool GrantOnPurchase { get; set; }
            public IReadOnlyList<string> RestoreEntitlements { get; set; } = Array.Empty<string>();
            public int PurchaseCalls { get; private set; }
            public int RestoreCalls { get; private set; }
            public string LastPurchasedProductId { get; private set; }

            public WardrobeBackend WithProduct(string id, string price)
            {
                _products[id] = new StoreProductView(id, id, new LocalizedPrice(price));
                return this;
            }

            public void RemoveProduct(string id) => _products.Remove(id);
            public WardrobeBackend WithStoreEntitlement(string id)
            {
                _storeEntitlements.Add(id);
                return this;
            }
            public void RevokeAllStoreEntitlements() => _storeEntitlements.Clear();

            public void FetchProducts(Action<IReadOnlyList<StoreProductView>> onDone) =>
                onDone?.Invoke(_products.Values.ToArray());

            public void Purchase(string productId, Action<PurchaseResult> onDone)
            {
                PurchaseCalls++;
                LastPurchasedProductId = productId;
                if (GrantOnPurchase && NextPurchaseOutcome == PurchaseOutcome.SuccessCandidate)
                    foreach (var id in Catalog.EntitlementsFor(productId))
                        _storeEntitlements.Add(id);
                onDone?.Invoke(new PurchaseResult(NextPurchaseOutcome, productId));
            }

            public void Restore(Action<RestoreResult> onDone)
            {
                RestoreCalls++;
                foreach (var id in RestoreEntitlements) _storeEntitlements.Add(id);
                onDone?.Invoke(new RestoreResult(RestoreOutcome.Completed));
            }

            public void RefreshEntitlements(Action<EntitlementSnapshot> onDone) =>
                onDone?.Invoke(new EntitlementSnapshot(true, _storeEntitlements
                    .Select(id => new EntitlementGrant(id, GrantSource.Store)).ToArray()));
        }
    }
}
