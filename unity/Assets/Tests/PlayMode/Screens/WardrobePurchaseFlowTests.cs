using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatMetro.Presentation.Cosmetics;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;
using CatMetro.Presentation.Strings;
using CatMetro.Presentation.Theme;
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
        private static readonly Rect TallPhoneSafeArea = new Rect(0f, 96f, 1344f, 2760f);

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
            CosmeticRuntime.ResetForTests();
            PurchaseRuntime.ResetForTests();
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
            CreateCamera();
            CreateView(setup, camera: _cameraHost.GetComponent<Camera>());

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
            var portraitRect = ScreenRect(FindRect("LargePortrait"));
            var bluePixels = RenderPixels();
            Assert.That(CountColor(bluePixels, portraitRect,
                new Color32(62, 124, 201, 255), 28), Is.GreaterThan(1_000),
                "Blue Siamese must paint real Harbor Blue pixels");

            _view.ShowEntry();
            Layout();
            yield return null;
            Assert.That(Portrait("EntryPortrait").AppliedCatId, Is.EqualTo("blue_siamese"));

            _view.Open();
            Layout();
            yield return null;
            before = persistence.ReplaceCalls;
            Tap(FindRect("CatSelector-yellow_longhair"));
            yield return null;
            Assert.That(persistence.ReplaceCalls, Is.EqualTo(before + 1));
            Assert.That(order.TakeLast(2).ToArray(), Is.EqualTo(new[] { "persist", "publish" }));
            Assert.That(setup.Profile.SelectedCatId, Is.EqualTo("yellow_longhair"));
            Assert.That(Portrait("LargePortrait").AppliedCatId, Is.EqualTo("yellow_longhair"));
            var yellowPixels = RenderPixels();
            Assert.That(CountColor(yellowPixels, portraitRect,
                new Color32(239, 193, 61, 255), 28), Is.GreaterThan(1_000),
                "Yellow Longhair must paint real Tabby Yellow pixels");
            Assert.That(PixelDelta(bluePixels, yellowPixels, portraitRect),
                Is.GreaterThan(10_000), "Blue and Yellow portraits must be visibly distinct");
            _view.ShowEntry();
            Layout();
            yield return null;
            Assert.That(Portrait("EntryPortrait").AppliedCatId, Is.EqualTo("yellow_longhair"));
        }

        [UnityTest]
        public IEnumerator CatSelectors_PaintCanonicalLineColoursThroughSelectionTransitions()
        {
            var setup = CreateSetup();
            CreateView(setup);

            Assert.That(FindRect("CatSelector-red_tabby").GetComponent<Image>().color,
                Is.EqualTo(CatLine.ColorOf("red")),
                "the real red catalogue id must ask the canonical line vocabulary");
            Assert.That(FindRect("CatSelector-blue_siamese").GetComponent<Image>().color,
                Is.EqualTo(CatLine.ColorOf("blue")),
                "the real blue catalogue id must ask the canonical line vocabulary");
            Assert.That(FindRect("CatSelector-yellow_longhair").GetComponent<Image>().color,
                Is.EqualTo(CatLine.ColorOf("yellow")),
                "the real yellow catalogue id must ask the canonical line vocabulary");

            _view.Open();
            Layout();
            yield return null;

            Assert.That(FindRect("CatSelector-red_tabby").GetComponent<Image>().color,
                Is.EqualTo(Palette.TicketOrange),
                "the selected cat uses the selection colour");
            Assert.That(FindRect("CatSelector-blue_siamese").GetComponent<Image>().color,
                Is.EqualTo(CatLine.ColorOf("blue")));
            Assert.That(FindRect("CatSelector-yellow_longhair").GetComponent<Image>().color,
                Is.EqualTo(CatLine.ColorOf("yellow")));

            Tap(FindRect("CatSelector-blue_siamese"));
            yield return null;

            Assert.That(FindRect("CatSelector-red_tabby").GetComponent<Image>().color,
                Is.EqualTo(CatLine.ColorOf("red")),
                "the deselected cat returns to its canonical line colour");
            Assert.That(FindRect("CatSelector-blue_siamese").GetComponent<Image>().color,
                Is.EqualTo(Palette.TicketOrange),
                "the newly selected cat uses the selection colour");
            Assert.That(FindRect("CatSelector-yellow_longhair").GetComponent<Image>().color,
                Is.EqualTo(CatLine.ColorOf("yellow")));
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
        public IEnumerator HorizontalItemCards_ShareOneCenteredBandWithoutOverlap()
        {
            var root = ShippedCosmeticCatalogRoot();
            var third = (JObject)Item(root, "frame_brass").DeepClone();
            third["id"] = "frame_third";
            third["displayNameKey"] = "cosmetics.item.frame_brass";
            third["acquisition"] = "earned";
            third["earnInstructionKey"] = "cosmetics.earn.conductor";
            third["order"] = 40;
            third.Remove("entitlementId");
            third.Remove("productId");
            ((JArray)root["items"]).Add(third);
            var setup = CreateSetup(catalogRoot: root);
            CreateView(setup);
            _view.Open();
            Layout();
            yield return null;

            foreach (var fixture in new[]
                     {
                         (Safe: PhoneSafeArea, Dpi: 408f, ThreeWidthDp: 106.54f),
                         (Safe: TallPhoneSafeArea, Dpi: 495f, ThreeWidthDp: 112f),
                     })
            {
                Layout(fixture.Safe, fixture.Dpi);
                Tap(FindRect("Tab-frame"));
                Tap(CardRect("frame_brass"));
                Canvas.ForceUpdateCanvases();
                AssertHorizontalCardBand(3, fixture.Safe, fixture.Dpi,
                    fixture.ThreeWidthDp);
                Assert.That(ScreenRect(FindRect("LargePortraitCard")).height /
                    HudBands.PxPerDp(fixture.Dpi), Is.GreaterThanOrEqualTo(320f),
                    "selected three-card state must retain the 320dp hero floor");

                Tap(FindRect("Tab-outfit"));
                Canvas.ForceUpdateCanvases();
                AssertHorizontalCardBand(1, fixture.Safe, fixture.Dpi, 112f);
            }

            Layout(PhoneSafeArea, 408f);
            var oneItemBand = ScreenRect(FindRect("ItemsBand"));
            var onePortrait = ScreenRect(FindRect("LargePortraitCard"));
            Tap(FindRect("Tab-accessory"));
            Canvas.ForceUpdateCanvases();
            AssertEmptyBand();
            Assert.That(ScreenRect(FindRect("ItemsBand")), Is.EqualTo(oneItemBand),
                "zero rows retain the fixed rail instead of vertically jumping the layout");
            Assert.That(ScreenRect(FindRect("LargePortraitCard")), Is.EqualTo(onePortrait),
                "zero rows retain the same hero band");
        }

        [UnityTest]
        public IEnumerator SelectedRoute_UsesOnePrimaryActionAndSharedBottomBand()
        {
            var purchase = CreateSetup();
            CreateView(purchase);
            _view.Open();
            Layout();
            yield return null;
            AssertSelectedRoute("outfit_conductor", CosmeticWardrobeRoute.Purchase,
                "Unlock · CA$2.79");

            ResetView();
            var ownedBackend = new WardrobeBackend().WithProduct(
                "cm_outfit_conductor", "CA$2.79").WithStoreEntitlement("outfit_conductor");
            var owned = CreateSetup(backend: ownedBackend);
            owned.Purchases.RefreshEntitlements();
            CreateView(owned);
            _view.Open();
            Layout();
            yield return null;
            AssertSelectedRoute("outfit_conductor", CosmeticWardrobeRoute.Equip, "Equip");

            ResetView();
            var equippedBackend = new WardrobeBackend().WithProduct(
                "cm_outfit_conductor", "CA$2.79").WithStoreEntitlement("outfit_conductor");
            var equipped = CreateSetup(new RecordingPersistence(
                ProfileWith(outfit: "outfit_conductor")), equippedBackend);
            equipped.Purchases.RefreshEntitlements();
            CreateView(equipped);
            _view.Open();
            Layout();
            yield return null;
            AssertSelectedRoute("outfit_conductor", CosmeticWardrobeRoute.None, "Unequip");

            ResetView();
            var rewarded = CreateSetup(backend: new WardrobeBackend());
            CreateView(rewarded, new RecordingRewardedRoute(canOffer: true));
            _view.Open();
            Layout();
            yield return null;
            AssertSelectedRoute("outfit_conductor", CosmeticWardrobeRoute.Rewarded,
                "Watch to borrow");

            ResetView();
            var earnedRoot = ShippedCosmeticCatalogRoot();
            var earnedItem = Item(earnedRoot, "outfit_conductor");
            earnedItem["acquisition"] = "earned";
            earnedItem["earnInstructionKey"] = "cosmetics.earn.conductor";
            earnedItem.Remove("entitlementId");
            earnedItem.Remove("productId");
            earnedItem.Remove("rewardedPlacementId");
            var earned = CreateSetup(backend: new WardrobeBackend(), catalogRoot: earnedRoot);
            CreateView(earned);
            _view.Open();
            Layout();
            yield return null;
            AssertSelectedRoute("outfit_conductor", CosmeticWardrobeRoute.EarnInstruction,
                "Complete the stationmaster route to earn this outfit.");
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
        public IEnumerator PortraitStandAndCardPortraits_AreRealPaintedArtifacts()
        {
            var backend = new WardrobeBackend()
                .WithStoreEntitlement("outfit_conductor")
                .WithStoreEntitlement("frame_brass")
                .WithStoreEntitlement("frame_lantern");
            var persistence = new RecordingPersistence(ProfileWith(
                outfit: "outfit_conductor", frame: "frame_brass"));
            var setup = CreateSetup(persistence, backend);
            setup.Purchases.RefreshEntitlements();
            CreateCamera();
            CreateView(setup, camera: _cameraHost.GetComponent<Camera>());
            yield return null; // RenderTexture/camera binding must settle before screen layout.
            _view.Open();
            Layout();
            yield return null;

            var stand = FindRect("PortraitStand");
            var shadow = FindChildRect(stand, "StandShadow");
            var baseLayer = FindChildRect(stand, "StandBase");
            var plaque = FindChildRect(stand, "StandPlaque");
            Assert.That(shadow.parent, Is.SameAs(stand));
            Assert.That(baseLayer.parent, Is.SameAs(stand));
            Assert.That(plaque.parent, Is.SameAs(stand));
            AssertPaintedAgainstDisabledControl(shadow, 150, "stand shadow");
            AssertPaintedAgainstDisabledControl(baseLayer, 500, "stand base");
            AssertPaintedAgainstDisabledControl(plaque, 100, "stand plaque");

            var portraitCard = FindRect("LargePortraitCard");
            var heroMount = FindRect("LargePortraitMount");
            AssertContained(heroMount, portraitCard, "expanded hero mount");
            Assert.That(ScreenRect(heroMount).Overlaps(ScreenRect(stand)), Is.True,
                "the complete cat overlaps its toy stand inside the portrait card");
            Assert.That(_view.LargePortrait.AppliedCatId, Is.EqualTo("red_tabby"));
            Assert.That(_view.LargePortrait.AppliedOutfitAssetId,
                Is.EqualTo("outfit.conductor"));
            Assert.That(_view.LargePortrait.AppliedFrameAssetId, Is.EqualTo("frame.brass"));
            var heroPixels = RenderPixels();
            var heroRect = ScreenRect(heroMount);
            Assert.That(CountColor(heroPixels, heroRect,
                new Color32(225, 90, 71, 255), 30), Is.GreaterThan(500), "hero base cat");
            Assert.That(CountColor(heroPixels, heroRect,
                new Color32(34, 48, 74, 255), 30), Is.GreaterThan(500), "hero coat");
            Assert.That(CountColor(heroPixels, heroRect,
                new Color32(239, 193, 61, 255), 30), Is.GreaterThan(100), "hero frame");
            AssertPaintedAgainstDisabledControl(_view.LargePortrait.RootTransform, 2_000,
                "large portrait");

            var conductor = Card("outfit_conductor");
            var conductorPortrait = CardPortrait(conductor);
            Assert.That(conductorPortrait.name, Is.EqualTo("ItemPortrait"));
            Assert.That(conductorPortrait.AppliedOutfitAssetId,
                Is.EqualTo("outfit.conductor"));
            var conductorPixels = RenderPixels();
            var conductorRect = ScreenRect(conductorPortrait.RootTransform);
            Assert.That(CountColor(conductorPixels, conductorRect,
                new Color32(34, 48, 74, 255), 30), Is.GreaterThan(80),
                "Conductor card contains admitted navy coat pixels");
            Assert.That(CountColor(conductorPixels, conductorRect,
                new Color32(225, 90, 71, 255), 30), Is.GreaterThan(80),
                "Conductor card contains its complete Red Tabby base");
            AssertPaintedAgainstDisabledControl(conductorPortrait.RootTransform, 250,
                "Conductor item portrait");

            Tap(FindRect("Tab-frame"));
            yield return null;
            var brass = CardPortrait(Card("frame_brass"));
            var lantern = CardPortrait(Card("frame_lantern"));
            var framePixels = RenderPixels();
            Assert.That(CountColor(framePixels, ScreenRect(brass.RootTransform),
                new Color32(239, 193, 61, 255), 30), Is.GreaterThan(80),
                "Brass card contains admitted yellow frame pixels");
            Assert.That(CountColor(framePixels, ScreenRect(lantern.RootTransform),
                new Color32(59, 175, 168, 255), 30), Is.GreaterThan(80),
                "Lantern card contains admitted teal frame pixels");
            AssertPaintedAgainstDisabledControl(brass.RootTransform, 250,
                "Brass item portrait");
            AssertPaintedAgainstDisabledControl(lantern.RootTransform, 250,
                "Lantern item portrait");

            var oldCards = ActiveCards().ToArray();
            var oldPortraits = oldCards.Select(CardPortrait).ToArray();
            Assert.That(setup.Profile.TrySelectCat("blue_siamese"), Is.True);
            Assert.That(oldCards.All(card => !card.gameObject.activeInHierarchy), Is.True,
                "rebuilt rows are made inert before deferred destruction");
            yield return null;
            Assert.That(oldCards.All(card => card == null), Is.True,
                "old card objects are destroyed after the rebuild frame");
            Assert.That(oldPortraits.All(portrait => portrait == null), Is.True,
                "old static previews do not survive a legitimate Wardrobe rebuild");
            Assert.That(ActiveCards().Count, Is.EqualTo(2));
            foreach (var card in ActiveCards())
            {
                Assert.That(card.GetComponentsInChildren<CosmeticPortraitView>(true).Length,
                    Is.EqualTo(1), card.ItemId + " has exactly one replacement preview");
                Assert.That(CardPortrait(card).AppliedCatId, Is.EqualTo("blue_siamese"));
            }
            AssertNoAcquisitionModeTabsOrCardActions();
        }

        [UnityTest]
        public IEnumerator PriceChip_RendersOnlyLiveLocalizedPrice()
        {
            var setup = CreateSetup();
            CreateCamera();
            CreateView(setup, camera: _cameraHost.GetComponent<Camera>());
            yield return null; // RenderTexture/camera binding must settle before screen layout.
            _view.Open();
            Layout();
            yield return null;

            var card = Card("outfit_conductor");
            Assert.That(CardBool(card, "PriceChipVisible"), Is.True);
            Assert.That(card.DisplayedPriceText, Is.EqualTo("CA$2.79"));
            var chip = FindChildRect(card.transform, "PriceChip");
            Assert.That(chip.gameObject.activeInHierarchy, Is.True);
            var price = FindChildText(chip, "ItemPriceLabel");
            price.ForceMeshUpdate();
            Assert.That(price.text, Is.EqualTo("CA$2.79"));
            Assert.That(price.textInfo.characterCount, Is.EqualTo(7));
            Assert.That(price.textInfo.meshInfo.Sum(mesh => mesh.vertexCount),
                Is.GreaterThan(0), "localized price must generate real TMP geometry");
            Assert.That(price.isTextOverflowing, Is.False);
            AssertContained((RectTransform)price.transform, chip, "localized price mesh");

            var painted = RenderPixels();
            var chipRect = ScreenRect(chip);
            Assert.That(CountColor(painted, chipRect,
                new Color32(59, 175, 168, 255), 28), Is.GreaterThan(500),
                "the real price chip contributes teal pixels inside its projected crop");
            AssertPaintedAgainstDisabledControl(chip, 300, "localized price chip");
            AssertNoAcquisitionModeTabsOrCardActions();

            ResetView();
            var ownedBackend = new WardrobeBackend().WithProduct(
                "cm_outfit_conductor", "CA$2.79").WithStoreEntitlement("outfit_conductor");
            var owned = CreateSetup(backend: ownedBackend);
            owned.Purchases.RefreshEntitlements();
            CreateView(owned, camera: _cameraHost.GetComponent<Camera>());
            yield return null;
            _view.Open();
            Layout();
            yield return null;

            var ownedCard = Card("outfit_conductor");
            Assert.That(ownedCard.DisplayedPriceText, Is.Empty);
            Assert.That(CardBool(ownedCard, "PriceChipVisible"), Is.False);
            Assert.That(FindChildRect(ownedCard.transform, "PriceChip").gameObject.activeSelf,
                Is.False, "an owned non-purchase row has no price chip");
            AssertHorizontalCardBand(1, PhoneSafeArea, 408f, 112f);
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
        public IEnumerator DeferredPurchase_EquipsTheInitiatingCatAfterExternalSelectionChange()
        {
            var setup = CreateSetup();
            setup.Backend.DeferPurchase = true;
            setup.Backend.GrantOnPurchase = true;
            CreateView(setup);
            _view.Open();
            Layout();
            yield return null;

            Tap(CardRect("outfit_conductor"));
            Tap(FindRect("PrimaryActionChip"));
            Assert.That(setup.Profile.TrySelectCat("blue_siamese"), Is.True);
            yield return null;

            setup.Backend.CompletePurchase();
            yield return null;

            Assert.That(setup.Profile.Profile.LoadoutFor("red_tabby").OutfitId,
                Is.EqualTo("outfit_conductor"));
            Assert.That(setup.Profile.Profile.LoadoutFor("blue_siamese").OutfitId, Is.Empty);
            Assert.That(setup.Profile.SelectedCatId, Is.EqualTo("blue_siamese"));
            Assert.That(Portrait("LargePortrait").AppliedCatId, Is.EqualTo("blue_siamese"));
            Assert.That(Portrait("LargePortrait").AppliedOutfitAssetId, Is.Empty,
                "equipping Red must not change the selected Blue portrait");
        }

        [UnityTest]
        public IEnumerator DeferredReward_EquipsInitiatingCat_AndDoubleTapIssuesOneRequest()
        {
            var root = ShippedCosmeticCatalogRoot();
            Item(root, "outfit_conductor")["rewardedPlacementId"] = "wardrobe.borrow.coat";
            var setup = CreateSetup(backend: new WardrobeBackend(), catalogRoot: root);
            var rewarded = new RecordingRewardedRoute(true) { DeferCompletion = true };
            rewarded.BeforeCompletion = () =>
                setup.Purchases.GrantRewardedAdEntitlement("outfit_conductor");
            CreateView(setup, rewarded);
            _view.Open();
            Layout();
            yield return null;

            Tap(CardRect("outfit_conductor"));
            var primaryAction = Resolve(FindRect("PrimaryActionChip"));
            var catAction = Resolve(FindRect("CatSelector-yellow_longhair"));
            var tabAction = Resolve(FindRect("Tab-frame"));
            var cardAction = Resolve(CardRect("outfit_conductor"));
            var restoreAction = Resolve(FindRect("RestoreChip"));
            primaryAction();
            primaryAction();
            catAction();
            tabAction();
            cardAction();
            restoreAction();
            Assert.That(rewarded.RequestCalls, Is.EqualTo(1),
                "the UI owns the rewarded in-flight guard");
            Assert.That(rewarded.LastPlacementId, Is.EqualTo("wardrobe.borrow.coat"));
            Assert.That(setup.Profile.SelectedCatId, Is.EqualTo("red_tabby"));
            Assert.That(_view.SelectedSlot, Is.EqualTo(CosmeticSlot.Outfit));
            Assert.That(setup.Backend.RestoreCalls, Is.EqualTo(0));
            Assert.That(setup.Profile.TrySelectCat("blue_siamese"), Is.True);
            rewarded.CompleteNext();
            yield return null;

            Assert.That(setup.Profile.Profile.LoadoutFor("red_tabby").OutfitId,
                Is.EqualTo("outfit_conductor"));
            Assert.That(setup.Profile.Profile.LoadoutFor("blue_siamese").OutfitId, Is.Empty);
            Assert.That(Portrait("LargePortrait").AppliedCatId, Is.EqualTo("blue_siamese"));
            Assert.That(Portrait("LargePortrait").AppliedOutfitAssetId, Is.Empty);
        }

        [UnityTest]
        public IEnumerator ClosedReward_ReprojectsLateOwnershipWithoutAutoEquipping()
        {
            var root = ShippedCosmeticCatalogRoot();
            Item(root, "outfit_conductor")["rewardedPlacementId"] = "wardrobe.borrow.coat";
            var setup = CreateSetup(backend: new WardrobeBackend(), catalogRoot: root);
            var rewarded = new RecordingRewardedRoute(true)
            {
                NextCompletion = CosmeticRewardedCompletion.NotGranted,
                DeferCompletion = true,
            };
            CreateView(setup, rewarded);
            _view.Open();
            Layout();
            yield return null;

            Tap(CardRect("outfit_conductor"));
            Tap(FindRect("PrimaryActionChip"));
            rewarded.CompleteNext();
            yield return null;

            Assert.That(rewarded.LastPlacementId, Is.EqualTo("wardrobe.borrow.coat"));
            Assert.That(setup.Purchases.IsUnlocked("outfit_conductor"), Is.False,
                "the close completion releases Wardrobe before a late coordinator reward");
            Assert.That(setup.Profile.Profile.LoadoutFor("red_tabby").OutfitId, Is.Empty,
                "a non-granted visible completion must never auto-equip");
            Assert.That(_view.VisibleCards.Count, Is.GreaterThan(0));
            Assert.That(_regions.IsRegistered("wardrobe.primary"), Is.True);

            Assert.That(setup.Purchases.GrantRewardedAdEntitlement("outfit_conductor"),
                Is.EqualTo(AdGrantOutcome.Granted));
            yield return null;
            Assert.That(setup.Purchases.IsUnlocked("outfit_conductor"), Is.True,
                "the shared durable authority observes the late grant");
            Assert.That(setup.Profile.Profile.LoadoutFor("red_tabby").OutfitId, Is.Empty,
                "late durable ownership reprojects the row but never auto-equips it");
        }

        [UnityTest]
        public IEnumerator BusyOperations_LockSelectorsTabsCardsPrimaryAndRestore()
        {
            var setup = CreateSetup();
            setup.Backend.DeferPurchase = true;
            CreateView(setup);
            _view.Open();
            Layout();
            yield return null;

            Tap(CardRect("outfit_conductor"));
            string primary = _view.PrimaryActionText;
            var purchaseAction = Resolve(FindRect("PrimaryActionChip"));
            var catAction = Resolve(FindRect("CatSelector-blue_siamese"));
            var tabAction = Resolve(FindRect("Tab-frame"));
            var cardAction = Resolve(CardRect("outfit_conductor"));
            var restoreAction = Resolve(FindRect("RestoreChip"));
            purchaseAction();
            catAction();
            tabAction();
            cardAction();
            purchaseAction();
            restoreAction();

            Assert.That(setup.Profile.SelectedCatId, Is.EqualTo("red_tabby"));
            Assert.That(_view.SelectedSlot, Is.EqualTo(CosmeticSlot.Outfit));
            Assert.That(_view.PrimaryActionText, Is.Not.EqualTo(primary),
                "the purchase may paint its running label but no competing selection");
            Assert.That(setup.Backend.PurchaseCalls, Is.EqualTo(1));
            Assert.That(setup.Backend.RestoreCalls, Is.EqualTo(0));

            setup.Backend.NextPurchaseOutcome = PurchaseOutcome.UserCancelled;
            setup.Backend.CompletePurchase();
            yield return null;

            setup.Backend.DeferRestore = true;
            restoreAction = Resolve(FindRect("RestoreChip"));
            catAction = Resolve(FindRect("CatSelector-yellow_longhair"));
            tabAction = Resolve(FindRect("Tab-frame"));
            cardAction = Resolve(CardRect("outfit_conductor"));
            purchaseAction = Resolve(FindRect("PrimaryActionChip"));
            restoreAction();
            catAction();
            tabAction();
            cardAction();
            purchaseAction();
            restoreAction();
            Assert.That(setup.Profile.SelectedCatId, Is.EqualTo("red_tabby"));
            Assert.That(_view.SelectedSlot, Is.EqualTo(CosmeticSlot.Outfit));
            Assert.That(setup.Backend.PurchaseCalls, Is.EqualTo(1));
            Assert.That(setup.Backend.RestoreCalls, Is.EqualTo(1));
            setup.Backend.NextRestoreOutcome = RestoreOutcome.Completed;
            setup.Backend.CompleteRestore();
        }

        [UnityTest]
        public IEnumerator OldPurchaseAndRewardCallbacks_LandAuthorityWithoutNewSessionEquipOrStatus()
        {
            var purchaseSetup = CreateSetup();
            purchaseSetup.Backend.DeferPurchase = true;
            purchaseSetup.Backend.GrantOnPurchase = true;
            CreateView(purchaseSetup);
            _view.Open();
            Layout();
            yield return null;
            Tap(CardRect("outfit_conductor"));
            Tap(FindRect("PrimaryActionChip"));
            _view.ShowEntry();
            _view.Open();
            Layout();
            yield return null;
            purchaseSetup.Backend.CompletePurchase();
            yield return null;
            Assert.That(purchaseSetup.Purchases.IsUnlocked("outfit_conductor"), Is.True);
            Assert.That(purchaseSetup.Profile.Profile.LoadoutFor("red_tabby").OutfitId, Is.Empty);
            Assert.That(StatusText(), Is.Empty);
            Assert.That(Card("outfit_conductor").Route, Is.EqualTo(CosmeticWardrobeRoute.Equip));

            ResetView();
            var root = ShippedCosmeticCatalogRoot();
            Item(root, "outfit_conductor")["rewardedPlacementId"] = "wardrobe.borrow.coat";
            var rewardSetup = CreateSetup(backend: new WardrobeBackend(), catalogRoot: root);
            var rewarded = new RecordingRewardedRoute(true) { DeferCompletion = true };
            rewarded.BeforeCompletion = () =>
                rewardSetup.Purchases.GrantRewardedAdEntitlement("outfit_conductor");
            CreateView(rewardSetup, rewarded);
            _view.Open();
            Layout();
            yield return null;
            Tap(CardRect("outfit_conductor"));
            Tap(FindRect("PrimaryActionChip"));
            _view.Hide();
            _view.Open();
            Layout();
            yield return null;
            rewardSetup.Backend.WithProduct("cm_outfit_conductor", "CA$2.79");
            rewardSetup.Purchases.Refresh();
            rewarded.CompleteNext();
            yield return null;
            Assert.That(rewardSetup.Purchases.IsUnlocked("outfit_conductor"), Is.True);
            Assert.That(rewardSetup.Profile.Profile.LoadoutFor("red_tabby").OutfitId, Is.Empty);
            Assert.That(StatusText(), Is.Empty);
            Assert.That(Card("outfit_conductor").Route, Is.EqualTo(CosmeticWardrobeRoute.Purchase),
                "borrowed access retains the permanent store route when a live price exists");
        }

        [UnityTest]
        public IEnumerator OldRestoreAndRefreshCallbacks_CannotOverwriteNewSessionPresentation()
        {
            var restoreSetup = CreateSetup();
            restoreSetup.Backend.DeferRestore = true;
            restoreSetup.Backend.RestoreEntitlements = new[] { "frame_brass" };
            CreateView(restoreSetup);
            _view.Open();
            Layout();
            yield return null;
            Tap(FindRect("RestoreChip"));
            Assert.That(_view.RestoreLabelText,
                Is.EqualTo(UiStrings.Get("wardrobe.restore.running")));
            Assert.That(StatusText(), Is.EqualTo(UiStrings.Get("wardrobe.status.restoring")));
            _view.Hide();
            _view.Open();
            Layout();
            yield return null;
            restoreSetup.Backend.CompleteRestore();
            yield return null;
            Assert.That(restoreSetup.Purchases.IsUnlocked("frame_brass"), Is.True);
            Assert.That(restoreSetup.Profile.Profile.LoadoutFor("red_tabby").FrameId, Is.Empty);
            Assert.That(_view.RestoreLabelText, Is.EqualTo(UiStrings.Get("wardrobe.restore")));
            Assert.That(StatusText(), Is.Empty);

            restoreSetup.Backend.RestoreEntitlements = Array.Empty<string>();
            Tap(FindRect("RestoreChip"));
            _view.gameObject.SetActive(false);
            _view.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            Assert.That(StatusText(), Is.Empty,
                "disable/enable invalidates the running presentation session");
            restoreSetup.Backend.CompleteRestore();
            yield return null;
            Assert.That(StatusText(), Is.Empty);

            ResetView();
            var refreshSetup = CreateSetup();
            refreshSetup.Backend.DeferProductFetch = true;
            CreateView(refreshSetup);
            _view.Open();
            Layout();
            yield return null;
            _view.Hide();
            _view.Open();
            Layout();
            yield return null;
            Assert.That(StatusText(), Is.EqualTo(UiStrings.Get("wardrobe.status.checking")));
            refreshSetup.Backend.CompleteNextProductFetch();
            yield return null;
            Assert.That(StatusText(), Is.EqualTo(UiStrings.Get("wardrobe.status.checking")),
                "the first Open callback is stale in the second session");
            refreshSetup.Backend.CompleteNextProductFetch();
            yield return null;
            Assert.That(StatusText(), Is.Empty, "the current Open callback may finish checking");
        }

        [UnityTest]
        public IEnumerator CandidateDisappearingDuringPurchase_InvalidatesOnlyLocalEquipEffect()
        {
            var setup = CreateSetup();
            setup.Backend.DeferPurchase = true;
            setup.Backend.GrantOnPurchase = true;
            CreateView(setup);
            _view.Open();
            Layout();
            yield return null;
            Tap(CardRect("outfit_conductor"));
            Tap(FindRect("PrimaryActionChip"));

            setup.Backend.RemoveProduct("cm_outfit_conductor");
            setup.Purchases.Refresh();
            Assert.That(setup.Profile.TrySelectCat("blue_siamese"), Is.True);
            yield return null;
            Assert.That(ActiveCards().Count, Is.EqualTo(0));
            Assert.That(_regions.IsRegistered("wardrobe.primary"), Is.False);

            setup.Backend.CompletePurchase();
            yield return null;
            Assert.That(setup.Purchases.IsUnlocked("outfit_conductor"), Is.True);
            Assert.That(setup.Profile.Profile.LoadoutFor("red_tabby").OutfitId, Is.Empty);
            Assert.That(setup.Profile.Profile.LoadoutFor("blue_siamese").OutfitId, Is.Empty);
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
        public IEnumerator ExistingRewardedLease_NonSuccessPurchasesNeverEquipAndPaintOutcome()
        {
            var cases = new[]
            {
                (PurchaseOutcome.UserCancelled, "wardrobe.status.cancelled"),
                (PurchaseOutcome.Pending, "wardrobe.status.pending"),
                (PurchaseOutcome.Busy, "wardrobe.status.opening"),
                (PurchaseOutcome.Failure, "wardrobe.status.unavailable"),
                (PurchaseOutcome.Unavailable, "wardrobe.status.unavailable"),
                (PurchaseOutcome.UnknownUnsettled, "wardrobe.status.unavailable"),
            };

            foreach (var testCase in cases)
            {
                var setup = CreateSetup();
                Assert.That(setup.Purchases.GrantRewardedAdEntitlement("outfit_conductor"),
                    Is.EqualTo(AdGrantOutcome.Granted));
                setup.Backend.NextPurchaseOutcome = testCase.Item1;
                CreateView(setup);
                _view.Open();
                Layout();
                yield return null;

                Tap(CardRect("outfit_conductor"));
                Tap(FindRect("PrimaryActionChip"));
                yield return null;

                Assert.That(setup.Purchases.IsUnlocked("outfit_conductor"), Is.True,
                    "the older lease remains source-blind wearable access");
                Assert.That(setup.Purchases.SecondsUntilExpiry("outfit_conductor"),
                    Is.GreaterThan(0));
                Assert.That(setup.Profile.Profile.LoadoutFor("red_tabby").OutfitId, Is.Empty,
                    testCase.Item1 + " cannot convert an older lease into a desired equip");
                Assert.That(StatusText(), Is.EqualTo(UiStrings.Get(testCase.Item2)),
                    testCase.Item1.ToString());
                ResetView();
            }
        }

        [UnityTest]
        public IEnumerator ExistingRewardedLease_UnconfirmedSuccessNeverEquips()
        {
            var cases = new[]
            {
                (authoritative: true, availability: BackendAvailability.Ready,
                    label: "authoritative snapshot missing the product promise"),
                (authoritative: false, availability: BackendAvailability.Unreachable,
                    label: "unreachable CustomerInfo"),
            };

            foreach (var testCase in cases)
            {
                var setup = CreateSetup();
                Assert.That(setup.Purchases.GrantRewardedAdEntitlement("outfit_conductor"),
                    Is.EqualTo(AdGrantOutcome.Granted));
                setup.Backend.GrantOnPurchase = false;
                CreateView(setup);
                _view.Open();
                Layout();
                yield return null;

                Tap(CardRect("outfit_conductor"));
                setup.Backend.EntitlementsAreAuthoritative = testCase.authoritative;
                setup.Backend.Availability = testCase.availability;
                Tap(FindRect("PrimaryActionChip"));
                yield return null;

                Assert.That(setup.Purchases.IsUnlocked("outfit_conductor"), Is.True,
                    testCase.label);
                Assert.That(setup.Purchases.SecondsUntilExpiry("outfit_conductor"),
                    Is.GreaterThan(0), testCase.label);
                Assert.That(setup.Profile.Profile.LoadoutFor("red_tabby").OutfitId, Is.Empty,
                    testCase.label);
                Assert.That(StatusText(),
                    Is.EqualTo(UiStrings.Get("wardrobe.status.unconfirmed")), testCase.label);
                ResetView();
            }
        }

        [UnityTest]
        public IEnumerator ExistingRewardedLease_ConfirmedFallbackBecomesPermanentAndEquipsInitiator()
        {
            var setup = CreateSetup();
            Assert.That(setup.Purchases.GrantRewardedAdEntitlement("outfit_conductor"),
                Is.EqualTo(AdGrantOutcome.Granted));
            setup.Backend.GrantOnPurchase = true;
            CreateView(setup);
            _view.Open();
            Layout();
            yield return null;

            Tap(CardRect("outfit_conductor"));
            Tap(FindRect("PrimaryActionChip"));
            yield return null;

            Assert.That(setup.Purchases.SecondsUntilExpiry("outfit_conductor"), Is.EqualTo(0),
                "accepted fallback CustomerInfo replaces the countdown with permanent access");
            Assert.That(setup.Profile.Profile.LoadoutFor("red_tabby").OutfitId,
                Is.EqualTo("outfit_conductor"));
            Assert.That(StatusText(), Is.EqualTo(UiStrings.Get("wardrobe.state.equipped")));
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
        public IEnumerator DeferredRestore_PaintsRunningCompletedEmptyBusyUnavailableAndFailure()
        {
            var setup = CreateSetup();
            setup.Backend.DeferRestore = true;
            CreateView(setup);
            _view.Open();
            Layout();
            yield return null;

            setup.Backend.RestoreEntitlements = new[] { "frame_brass" };
            Tap(FindRect("RestoreChip"));
            Assert.That(_view.RestoreLabelText,
                Is.EqualTo(UiStrings.Get("wardrobe.restore.running")));
            Assert.That(StatusText(), Is.EqualTo(UiStrings.Get("wardrobe.status.restoring")));
            setup.Backend.CompleteRestore();
            yield return null;
            Assert.That(StatusText(), Is.EqualTo(UiStrings.Get("wardrobe.status.restored")));

            var cases = new[]
            {
                (RestoreOutcome.Completed, "wardrobe.status.none"),
                (RestoreOutcome.Busy, "wardrobe.status.restoring"),
                (RestoreOutcome.Unavailable, "wardrobe.status.unavailable"),
                (RestoreOutcome.Failure, "wardrobe.status.restore.failed"),
            };
            setup.Backend.RestoreEntitlements = Array.Empty<string>();
            setup.Backend.RevokeAllStoreEntitlements();
            foreach (var testCase in cases)
            {
                setup.Backend.NextRestoreOutcome = testCase.Item1;
                Tap(FindRect("RestoreChip"));
                Assert.That(_view.RestoreLabelText,
                    Is.EqualTo(UiStrings.Get("wardrobe.restore.running")));
                setup.Backend.CompleteRestore();
                yield return null;
                Assert.That(_view.RestoreLabelText,
                    Is.EqualTo(UiStrings.Get("wardrobe.restore")), testCase.Item1.ToString());
                Assert.That(StatusText(), Is.EqualTo(UiStrings.Get(testCase.Item2)),
                    testCase.Item1.ToString());
            }
        }

        [UnityTest]
        public IEnumerator CachedLocalizedPrice_RemainsPurchasableDuringTransientUnreachableBackend()
        {
            var setup = CreateSetup();
            CreateView(setup);
            _view.Open();
            Layout();
            yield return null;

            int readyProductCount = setup.Purchases.StoreProductCount;
            Assert.That(readyProductCount, Is.EqualTo(3));
            setup.Backend.ClearProducts();
            setup.Backend.Availability = BackendAvailability.Unreachable;
            setup.Purchases.Refresh();
            _view.Hide();
            _view.Open();
            Layout();
            yield return null;

            Assert.That(setup.Purchases.StoreProductCount, Is.EqualTo(readyProductCount),
                "an unreachable empty offerings response preserves the shared cache");
            var card = Card("outfit_conductor");
            Assert.That(card.Route, Is.EqualTo(CosmeticWardrobeRoute.Purchase));
            Assert.That(card.DisplayedNameText, Is.EqualTo("Conductor's Coat"));
            Assert.That(card.DisplayedPriceText, Is.EqualTo("CA$2.79"));

            setup.Backend.Availability = BackendAvailability.Ready;
            setup.Purchases.Refresh();
            _view.Hide();
            _view.Open();
            Layout();
            yield return null;

            Assert.That(setup.Purchases.StoreProductCount, Is.EqualTo(0),
                "Ready empty offerings are authoritative removal");
            Assert.That(ActiveCards(), Is.Empty);
            Assert.That(_regions.IsRegistered("wardrobe.item.outfit_conductor"), Is.False);
            AssertEmptyBand();
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
            AssertHorizontalCardBand(1, PhoneSafeArea, 408f, 112f);
            AssertActionGeometry(primaryVisible: false);

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
            AssertHorizontalCardBand(2, PhoneSafeArea, 408f, 112f);
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
            Assert.That(_regions.Count, Is.EqualTo(9));
            Assert.That(_regions.IsRegistered("wardrobe.item.outfit_conductor"), Is.True);
            Assert.That(_regions.IsRegistered("wardrobe.primary"), Is.False);
            AssertCardArtifacts(expectedCards: 1, expectedVisiblePrices: 1);

            Tap(CardRect("outfit_conductor"));
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(10));
            Assert.That(_regions.IsRegistered("wardrobe.primary"), Is.True);
            AssertCardArtifacts(expectedCards: 1, expectedVisiblePrices: 1);

            _view.gameObject.SetActive(false);
            Assert.That(_regions.Count, Is.EqualTo(0));
            _view.gameObject.SetActive(true);
            yield return null;
            AssertCoreRegions(shouldExist: true);
            Assert.That(_regions.Count, Is.EqualTo(10));
            Assert.That(_regions.IsRegistered("wardrobe.primary"), Is.True);
            AssertCardArtifacts(expectedCards: 1, expectedVisiblePrices: 1);

            Tap(FindRect("CatSelector-blue_siamese"));
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(9));
            Assert.That(_regions.IsRegistered("wardrobe.primary"), Is.False);
            Assert.That(_regions.IsRegistered("wardrobe.item.outfit_conductor"), Is.True);
            AssertCardArtifacts(expectedCards: 1, expectedVisiblePrices: 1);

            Tap(FindRect("Tab-frame"));
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(10));
            Assert.That(_regions.IsRegistered("wardrobe.item.outfit_conductor"), Is.False);
            Assert.That(_regions.IsRegistered("wardrobe.item.frame_brass"), Is.True);
            Assert.That(_regions.IsRegistered("wardrobe.item.frame_lantern"), Is.True);
            AssertCardArtifacts(expectedCards: 2, expectedVisiblePrices: 2);

            _view.Hide();
            Assert.That(_regions.Count, Is.EqualTo(0));
            Assert.That(ActiveCards(), Is.Empty);
            _view.Open();
            Layout();
            yield return null;
            AssertCoreRegions(shouldExist: true);
            Assert.That(_regions.Count, Is.EqualTo(10));
            Assert.That(_regions.IsRegistered("wardrobe.item.frame_brass"), Is.True);
            Assert.That(_regions.IsRegistered("wardrobe.item.frame_lantern"), Is.True);
            AssertCardArtifacts(expectedCards: 2, expectedVisiblePrices: 2);
            UnityEngine.Object.Destroy(_view.gameObject);
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator TwoArgumentFactory_DelegatesToCurrentProfileAndDisabledRewardedRoute()
        {
            var root = ShippedCosmeticCatalogRoot();
            Item(root, "outfit_conductor")["rewardedPlacementId"] = "wardrobe.borrow.coat";
            var setup = CreateSetup(backend: new WardrobeBackend(), catalogRoot: root);
            PurchaseRuntime.Install(setup.Purchases);
            CosmeticRuntime.Install(setup.Profile);

            _canvasHost = new GameObject("WardrobeBridgeCanvas");
            _canvas = _canvasHost.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _regions = new ChromeRegions();
            _view = WardrobeScreenView.Create(_canvas.transform, setup.Purchases);
            _view.Attach(_regions);
            _view.Open();
            Layout();
            yield return null;

            Assert.That(_view.LargePortrait.AppliedCatId,
                Is.EqualTo(CosmeticRuntime.Current.SelectedCatId));
            Assert.That(_view.LargePortrait.AppliedCatId, Is.EqualTo("red_tabby"));
            Assert.That(ActiveCards().Count, Is.EqualTo(0),
                "the bridge supplies DisabledCosmeticRewardedRoute when price is absent");
            Assert.That(_regions.Count, Is.EqualTo(8));
        }

        [UnityTest]
        public IEnumerator DestroyWithPendingCallbacks_IsHarmlessForPurchaseRewardRestoreAndRefresh()
        {
            var purchase = CreateSetup();
            purchase.Backend.DeferPurchase = true;
            purchase.Backend.GrantOnPurchase = true;
            CreateView(purchase);
            _view.Open();
            Layout();
            yield return null;
            Tap(CardRect("outfit_conductor"));
            Tap(FindRect("PrimaryActionChip"));
            UnityEngine.Object.DestroyImmediate(_view.gameObject);
            Assert.That(_regions.Count, Is.EqualTo(0));
            purchase.Backend.CompletePurchase();
            Assert.That(purchase.Purchases.IsUnlocked("outfit_conductor"), Is.True);
            Assert.That(purchase.Profile.Profile.LoadoutFor("red_tabby").OutfitId, Is.Empty);

            ResetView();
            var rewardRoot = ShippedCosmeticCatalogRoot();
            Item(rewardRoot, "outfit_conductor")["rewardedPlacementId"] =
                "wardrobe.borrow.coat";
            var reward = CreateSetup(backend: new WardrobeBackend(), catalogRoot: rewardRoot);
            var rewarded = new RecordingRewardedRoute(true) { DeferCompletion = true };
            rewarded.BeforeCompletion = () =>
                reward.Purchases.GrantRewardedAdEntitlement("outfit_conductor");
            CreateView(reward, rewarded);
            _view.Open();
            Layout();
            yield return null;
            Tap(CardRect("outfit_conductor"));
            Tap(FindRect("PrimaryActionChip"));
            UnityEngine.Object.DestroyImmediate(_view.gameObject);
            Assert.That(_regions.Count, Is.EqualTo(0));
            rewarded.CompleteNext();
            Assert.That(reward.Purchases.IsUnlocked("outfit_conductor"), Is.True);
            Assert.That(reward.Profile.Profile.LoadoutFor("red_tabby").OutfitId, Is.Empty);

            ResetView();
            var restore = CreateSetup();
            restore.Backend.DeferRestore = true;
            restore.Backend.RestoreEntitlements = new[] { "frame_brass" };
            CreateView(restore);
            _view.Open();
            Layout();
            yield return null;
            Tap(FindRect("RestoreChip"));
            UnityEngine.Object.DestroyImmediate(_view.gameObject);
            Assert.That(_regions.Count, Is.EqualTo(0));
            restore.Backend.CompleteRestore();
            Assert.That(restore.Purchases.IsUnlocked("frame_brass"), Is.True);
            Assert.That(restore.Profile.Profile.LoadoutFor("red_tabby").FrameId, Is.Empty);

            ResetView();
            var refresh = CreateSetup();
            refresh.Backend.DeferProductFetch = true;
            CreateView(refresh);
            _view.Open();
            Layout();
            yield return null;
            UnityEngine.Object.DestroyImmediate(_view.gameObject);
            Assert.That(_regions.Count, Is.EqualTo(0));
            refresh.Backend.CompleteNextProductFetch();
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
            AssertCaptureColor(captures.Last(), FindRect("StandBase"),
                new Color32(240, 138, 60, 255), 100, "painted toy stand base");
            var plainCard = Card("outfit_conductor");
            AssertCaptureColor(captures.Last(), CardPortrait(plainCard).RootTransform,
                new Color32(34, 48, 74, 255), 50, "Conductor card coat preview");
            AssertCaptureColor(captures.Last(), FindChildRect(plainCard.transform, "PriceChip"),
                new Color32(59, 175, 168, 255), 100, "localized real-price chip");
            Tap(CardRect("outfit_conductor"));
            yield return null;
            captures.Add(Capture(directory, "wardrobe-locked-preview.png"));
            AssertPortraitHasInk(captures.Last(), CardPortrait(Card("outfit_conductor"))
                .RootTransform, navy: true, brass: true, red: true);
            AssertPortraitHasInk(captures.Last(), FindRect("LargePortrait"), navy: true,
                brass: true);
            Tap(FindRect("PrimaryActionChip"));
            yield return null;
            captures.Add(Capture(directory, "wardrobe-purchased-equipped.png"));

            Tap(FindRect("PrimaryActionChip"));
            yield return null;
            Assert.That(Portrait("LargePortrait").AppliedOutfitAssetId, Is.Empty,
                "frame proof uses no conductor colors");
            var portraitRect = ScreenRect(FindRect("LargePortrait"));
            var noFramePixels = RenderPixels();

            Tap(FindRect("Tab-frame"));
            yield return null;
            Tap(CardRect("frame_brass"));
            Tap(FindRect("PrimaryActionChip"));
            yield return null;
            Assert.That(Portrait("LargePortrait").AppliedOutfitAssetId, Is.Empty);
            Assert.That(Portrait("LargePortrait").AppliedFrameAssetId,
                Is.EqualTo("frame.brass"));
            captures.Add(Capture(directory, "wardrobe-frame-brass.png"));
            var brassPixels = LoadPixels(captures.Last());
            AssertFrameBorder(noFramePixels, brassPixels, portraitRect,
                new Color32(239, 193, 61, 255), "Brass yellow rails/corners");
            AssertCenterCat(brassPixels, portraitRect);

            Tap(CardRect("frame_lantern"));
            Tap(FindRect("PrimaryActionChip"));
            yield return null;
            Assert.That(Portrait("LargePortrait").AppliedOutfitAssetId, Is.Empty);
            Assert.That(Portrait("LargePortrait").AppliedFrameAssetId,
                Is.EqualTo("frame.lantern"));
            captures.Add(Capture(directory, "wardrobe-frame-lantern.png"));
            var lanternPixels = LoadPixels(captures.Last());
            AssertFrameBorder(noFramePixels, lanternPixels, portraitRect,
                new Color32(59, 175, 168, 255), "Lantern teal rails");
            AssertCenterCat(lanternPixels, portraitRect);
            Assert.That(BorderPixelDelta(brassPixels, lanternPixels, portraitRect),
                Is.GreaterThan(5_000), "Brass and Lantern borders must be visibly distinct");

            Tap(FindRect("Tab-outfit"));
            yield return null;
            Tap(CardRect("outfit_conductor"));
            Tap(FindRect("PrimaryActionChip"));
            yield return null;
            Assert.That(Portrait("LargePortrait").AppliedOutfitAssetId,
                Is.EqualTo("outfit.conductor"));
            Assert.That(Portrait("LargePortrait").AppliedFrameAssetId,
                Is.EqualTo("frame.lantern"));

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

            var expected = new[]
            {
                "wardrobe-plain.png", "wardrobe-locked-preview.png",
                "wardrobe-purchased-equipped.png", "wardrobe-frame-brass.png",
                "wardrobe-frame-lantern.png", "wardrobe-lapsed.png",
                "wardrobe-restored.png", "wardrobe-contact-sheet.png",
            };
            Assert.That(captures.Append(contact).Select(Path.GetFileName).ToArray(),
                Is.EqualTo(expected));
            foreach (var path in expected.Select(name => Path.Combine(directory, name)))
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
            purchases.AttachLeasePersistence(new AcceptingLeasePersistence());
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
            _view = WardrobeScreenView.Create(_canvas.transform, setup.Purchases,
                setup.Profile, rewarded);
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
            Layout(PhoneSafeArea, 408f);
        }

        private void Layout(Rect safeArea, float dpi)
        {
            _view.LayoutForViewport(safeArea, dpi);
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

        private static RectTransform FindChildRect(Transform parent, string name)
        {
            var result = parent.GetComponentsInChildren<RectTransform>(true)
                .SingleOrDefault(rect => rect.name == name);
            Assert.That(result, Is.Not.Null, "missing child RectTransform " + name);
            return result;
        }

        private static TMP_Text FindChildText(Transform parent, string name)
        {
            var result = parent.GetComponentsInChildren<TMP_Text>(true)
                .SingleOrDefault(label => label.name == name);
            Assert.That(result, Is.Not.Null, "missing child label " + name);
            return result;
        }

        private CosmeticPortraitView Portrait(string name)
        {
            var result = _view.GetComponentsInChildren<CosmeticPortraitView>(true)
                .SingleOrDefault(portrait => portrait.name == name);
            Assert.That(result, Is.Not.Null, "missing shared portrait " + name);
            return result;
        }

        private IReadOnlyList<CosmeticItemCardView> ActiveCards() => _view
            .GetComponentsInChildren<CosmeticItemCardView>(true)
            .Where(component => component.gameObject.activeInHierarchy).ToArray();

        private CosmeticItemCardView Card(string itemId)
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

        private static bool CardBool(MonoBehaviour card, string property)
        {
            var info = card.GetType().GetProperty(property);
            Assert.That(info, Is.Not.Null, "missing card read-back " + property);
            return info != null && (bool)info.GetValue(card);
        }

        private static CosmeticPortraitView CardPortrait(CosmeticItemCardView card)
        {
            var info = card.GetType().GetProperty("ItemPortrait");
            Assert.That(info, Is.Not.Null,
                "cards must expose the real reusable ItemPortrait component");
            var result = info?.GetValue(card) as CosmeticPortraitView;
            Assert.That(result, Is.Not.Null, "ItemPortrait read-back is unbound");
            return result;
        }

        private void Tap(RectTransform target)
        {
            Resolve(target)();
        }

        private Action Resolve(RectTransform target)
        {
            var rect = ScreenRect(target);
            Assert.That(rect.width, Is.GreaterThan(0f));
            Assert.That(_regions.TryResolve(rect.center, out var action), Is.True,
                target.name + " is painted but not routed");
            return action;
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

        private void AssertActionGeometry(bool primaryVisible)
        {
            var targets = new List<RectTransform>
            {
                FindRect("BackChip"), FindRect("RestoreChip"),
                FindRect("CatSelector-red_tabby"), FindRect("CatSelector-blue_siamese"),
                FindRect("CatSelector-yellow_longhair"), FindRect("Tab-outfit"),
                FindRect("Tab-accessory"), FindRect("Tab-frame"),
            };
            targets.AddRange(ActiveCards().Select(card => card.RootTransform));
            var primary = FindRect("PrimaryActionChip");
            Assert.That(primary.gameObject.activeInHierarchy, Is.EqualTo(primaryVisible));
            if (primaryVisible) targets.Add(primary);

            float px = HudBands.PxPerDp(408f);
            float inset = 12f * px;
            float gap = 8f * px;
            float contentWidth = PhoneSafeArea.width - inset * 2f;
            var restoreRect = ScreenRect(FindRect("RestoreChip"));
            Assert.That(restoreRect.yMin,
                Is.EqualTo(PhoneSafeArea.yMin + inset).Within(1f));
            Assert.That(restoreRect.height, Is.EqualTo(56f * px).Within(1f));
            if (primaryVisible)
            {
                var primaryRect = ScreenRect(primary);
                float half = (contentWidth - gap) / 2f;
                Assert.That(primaryRect.xMin,
                    Is.EqualTo(PhoneSafeArea.xMin + inset).Within(1f));
                Assert.That(primaryRect.width, Is.EqualTo(half).Within(1f));
                Assert.That(primaryRect.y, Is.EqualTo(restoreRect.y).Within(1f));
                Assert.That(primaryRect.height, Is.EqualTo(restoreRect.height).Within(1f));
                Assert.That(restoreRect.xMin - primaryRect.xMax, Is.EqualTo(gap).Within(1f));
                Assert.That(restoreRect.width, Is.EqualTo(half).Within(1f));
                Assert.That(restoreRect.xMax,
                    Is.EqualTo(PhoneSafeArea.xMax - inset).Within(1f));
            }
            else
            {
                Assert.That(restoreRect.xMin,
                    Is.EqualTo(PhoneSafeArea.xMin + inset).Within(1f));
                Assert.That(restoreRect.width, Is.EqualTo(contentWidth).Within(1f),
                    "Restore fills the action band when no row is selected");
            }
            Assert.That(ScreenRect(FindRect("WardrobeStatus")).yMin,
                Is.EqualTo(restoreRect.yMax + gap).Within(1f),
                "status sits directly above the one shared action band");

            var rects = targets.Select(ScreenRect).ToArray();
            for (int i = 0; i < rects.Length; i++)
            {
                var rect = rects[i];
                Assert.That(HudBands.MeetsMinTargetPx(rect, 408f), Is.True,
                    targets[i].name + " violates the 48dp action floor");
                Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(PhoneSafeArea.xMin - 1f));
                Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(PhoneSafeArea.yMin - 1f));
                Assert.That(rect.xMax, Is.LessThanOrEqualTo(PhoneSafeArea.xMax + 1f));
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(PhoneSafeArea.yMax + 1f));
                for (int j = i + 1; j < rects.Length; j++)
                    Assert.That(rect.Overlaps(rects[j]), Is.False,
                        targets[i].name + " overlaps " + targets[j].name);
            }
        }

        private void AssertHorizontalCardBand(int expectedCount, Rect safeArea, float dpi,
            float expectedWidthDp)
        {
            var cards = ActiveCards().OrderBy(card => card.ScreenRect.xMin).ToArray();
            Assert.That(cards.Length, Is.EqualTo(expectedCount));
            var band = ScreenRect(FindRect("ItemsBand"));
            float px = HudBands.PxPerDp(dpi);
            Assert.That(band.height, Is.EqualTo(112f * px).Within(1f),
                "the item rail remains one fixed 112dp band");
            float expectedGap = 8f * px;
            float firstY = cards[0].ScreenRect.y;
            float firstHeight = cards[0].ScreenRect.height;
            for (int i = 0; i < cards.Length - 1; i++)
            {
                Assert.That(cards[i + 1].ScreenRect.xMin - cards[i].ScreenRect.xMax,
                    Is.EqualTo(expectedGap).Within(1f), "exactly 8dp separates horizontal cards");
            }
            foreach (var card in cards)
            {
                Assert.That(card.ScreenRect.y, Is.EqualTo(firstY).Within(1f));
                Assert.That(card.ScreenRect.height, Is.EqualTo(firstHeight).Within(1f));
                Assert.That(card.ScreenRect.width / px,
                    Is.EqualTo(expectedWidthDp).Within(0.05f));
                Assert.That(HudBands.MeetsMinTargetPx(card.ScreenRect, dpi), Is.True,
                    card.ItemId + " is below 48dp");
                Assert.That(card.ScreenRect.xMin, Is.GreaterThanOrEqualTo(band.xMin - 1f));
                Assert.That(card.ScreenRect.xMax, Is.LessThanOrEqualTo(band.xMax + 1f));
                Assert.That(card.ScreenRect.yMin, Is.GreaterThanOrEqualTo(band.yMin - 1f));
                Assert.That(card.ScreenRect.yMax, Is.LessThanOrEqualTo(band.yMax + 1f));
            }
            Assert.That(cards[0].ScreenRect.y, Is.EqualTo(band.y).Within(1f));
            Assert.That(cards[0].ScreenRect.height, Is.EqualTo(band.height).Within(1f));
            float railMin = cards[0].ScreenRect.xMin;
            float railMax = cards[cards.Length - 1].ScreenRect.xMax;
            Assert.That((railMin + railMax) * 0.5f, Is.EqualTo(band.center.x).Within(1f),
                "the capped card rail is centered inside the content band");
            Assert.That(band.xMin, Is.EqualTo(safeArea.xMin + 12f * px).Within(1f));
        }

        private void AssertEmptyBand()
        {
            var band = ScreenRect(FindRect("ItemsBand"));
            var empty = FindText("EmptyStateLabel");
            Assert.That(empty.gameObject.activeInHierarchy, Is.True);
            Assert.That(band.height, Is.EqualTo(112f * HudBands.PxPerDp(408f)).Within(1f));
            var emptyRect = ScreenRect((RectTransform)empty.transform);
            Assert.That(emptyRect.xMin, Is.GreaterThanOrEqualTo(band.xMin - 1f));
            Assert.That(emptyRect.yMin, Is.GreaterThanOrEqualTo(band.yMin - 1f));
            Assert.That(emptyRect.xMax, Is.LessThanOrEqualTo(band.xMax + 1f));
            Assert.That(emptyRect.yMax, Is.LessThanOrEqualTo(band.yMax + 1f));
            Assert.That(_regions.Count, Is.EqualTo(8),
                "empty slot has only back, restore, three cats, and three tabs");
        }

        private void AssertSelectedRoute(string itemId, CosmeticWardrobeRoute route,
            string expectedLabel)
        {
            AssertActionGeometry(primaryVisible: false);
            var card = Card(itemId);
            Assert.That(card.Route, Is.EqualTo(route));
            Tap(card.RootTransform);
            Canvas.ForceUpdateCanvases();
            Assert.That(_view.PrimaryActionText, Is.EqualTo(expectedLabel));
            Assert.That(_regions.IsRegistered("wardrobe.primary"), Is.True);
            Assert.That(_regions.Count, Is.EqualTo(10),
                "eight static targets, one preview-only card, and one selected-row action");
            AssertActionGeometry(primaryVisible: true);
            AssertNoAcquisitionModeTabsOrCardActions();
        }

        private void AssertNoAcquisitionModeTabsOrCardActions()
        {
            var transforms = _view.GetComponentsInChildren<Transform>(true);
            foreach (var transform in transforms)
            {
                string value = transform.name ?? string.Empty;
                Assert.That(string.Equals(value, "Tab-equip",
                    StringComparison.OrdinalIgnoreCase), Is.False);
                Assert.That(string.Equals(value, "Tab-shop",
                    StringComparison.OrdinalIgnoreCase), Is.False);
                Assert.That(value.IndexOf("card-action", StringComparison.OrdinalIgnoreCase),
                    Is.LessThan(0));
                Assert.That(value.IndexOf("coin", StringComparison.OrdinalIgnoreCase),
                    Is.LessThan(0));
                Assert.That(value.IndexOf("balance", StringComparison.OrdinalIgnoreCase),
                    Is.LessThan(0));
            }
            foreach (var label in _view.GetComponentsInChildren<TMP_Text>(true))
            {
                string value = label.text ?? string.Empty;
                Assert.That(value.IndexOf("coin", StringComparison.OrdinalIgnoreCase),
                    Is.LessThan(0));
                Assert.That(value.IndexOf("balance", StringComparison.OrdinalIgnoreCase),
                    Is.LessThan(0));
            }
        }

        private void AssertCardArtifacts(int expectedCards, int expectedVisiblePrices)
        {
            var cards = ActiveCards();
            Assert.That(cards.Count, Is.EqualTo(expectedCards));
            Assert.That(cards.Count(card => CardBool(card, "PriceChipVisible")),
                Is.EqualTo(expectedVisiblePrices));
            foreach (var card in cards)
            {
                Assert.That(card.GetComponentsInChildren<CosmeticPortraitView>(true).Length,
                    Is.EqualTo(1), card.ItemId + " has one static portrait mount");
                Assert.That(CardPortrait(card).name, Is.EqualTo("ItemPortrait"));
                Assert.That(card.GetComponentsInChildren<RectTransform>(true)
                    .Count(rect => rect.name == "PriceChip"), Is.EqualTo(1),
                    card.ItemId + " has one conditional price chip");
            }
            Assert.That(_view.GetComponentsInChildren<RectTransform>(true)
                .Count(rect => rect.name == "Tab-outfit"), Is.EqualTo(1));
            Assert.That(_view.GetComponentsInChildren<RectTransform>(true)
                .Count(rect => rect.name == "Tab-accessory"), Is.EqualTo(1));
            Assert.That(_view.GetComponentsInChildren<RectTransform>(true)
                .Count(rect => rect.name == "Tab-frame"), Is.EqualTo(1));
        }

        private void AssertContained(RectTransform inner, RectTransform outer, string message)
        {
            var innerRect = ScreenRect(inner);
            var outerRect = ScreenRect(outer);
            Assert.That(innerRect.xMin, Is.GreaterThanOrEqualTo(outerRect.xMin - 1f), message);
            Assert.That(innerRect.yMin, Is.GreaterThanOrEqualTo(outerRect.yMin - 1f), message);
            Assert.That(innerRect.xMax, Is.LessThanOrEqualTo(outerRect.xMax + 1f), message);
            Assert.That(innerRect.yMax, Is.LessThanOrEqualTo(outerRect.yMax + 1f), message);
        }

        private void AssertPaintedAgainstDisabledControl(RectTransform target, int minimumDelta,
            string message)
        {
            Assert.That(target.gameObject.activeInHierarchy, Is.True, message + " is active");
            var rect = ScreenRect(target);
            var painted = RenderPixels();
            target.gameObject.SetActive(false);
            Canvas.ForceUpdateCanvases();
            var paper = RenderPixels();
            target.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            Assert.That(PixelDelta(painted, paper, rect), Is.GreaterThan(minimumDelta),
                message + " must differ from its paper/background negative control");
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

        private Color32[] RenderPixels()
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
            var pixels = texture.GetPixels32();
            UnityEngine.Object.DestroyImmediate(texture);
            return pixels;
        }

        private static Color32[] LoadPixels(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
                throw new InvalidOperationException("could not decode capture " + path);
            var pixels = texture.GetPixels32();
            UnityEngine.Object.DestroyImmediate(texture);
            return pixels;
        }

        private static void AssertFrameBorder(IReadOnlyList<Color32> noFrame,
            IReadOnlyList<Color32> framed, Rect rect, Color32 expected, string message)
        {
            int baseline = CountBorderColor(noFrame, rect, expected, 28);
            int painted = CountBorderColor(framed, rect, expected, 28);
            Assert.That(painted - baseline, Is.GreaterThan(1_000), message);
        }

        private static void AssertCenterCat(IReadOnlyList<Color32> pixels, Rect portrait)
        {
            var centre = new Rect(portrait.xMin + portrait.width * 0.25f,
                portrait.yMin + portrait.height * 0.18f,
                portrait.width * 0.50f, portrait.height * 0.66f);
            Assert.That(CountColor(pixels, centre, new Color32(225, 90, 71, 255), 28),
                Is.GreaterThan(1_000), "the centre Red Tabby remains painted");
        }

        private static int CountBorderColor(IReadOnlyList<Color32> pixels, Rect rect,
            Color32 expected, int tolerance)
        {
            int count = 0;
            int xMin = Mathf.Clamp(Mathf.FloorToInt(rect.xMin), 0, Width - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(rect.xMax), 1, Width);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(rect.yMin), 0, Height - 1);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(rect.yMax), 1, Height);
            float insetX = rect.width * 0.19f;
            float insetY = rect.height * 0.19f;
            for (int y = yMin; y < yMax; y++)
            for (int x = xMin; x < xMax; x++)
            {
                bool border = x < rect.xMin + insetX || x >= rect.xMax - insetX
                    || y < rect.yMin + insetY || y >= rect.yMax - insetY;
                if (border && Close(pixels[y * Width + x], expected, tolerance)) count++;
            }
            return count;
        }

        private static int BorderPixelDelta(IReadOnlyList<Color32> left,
            IReadOnlyList<Color32> right, Rect rect)
        {
            int count = 0;
            int xMin = Mathf.Clamp(Mathf.FloorToInt(rect.xMin), 0, Width - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(rect.xMax), 1, Width);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(rect.yMin), 0, Height - 1);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(rect.yMax), 1, Height);
            float insetX = rect.width * 0.19f;
            float insetY = rect.height * 0.19f;
            for (int y = yMin; y < yMax; y++)
            for (int x = xMin; x < xMax; x++)
            {
                bool border = x < rect.xMin + insetX || x >= rect.xMax - insetX
                    || y < rect.yMin + insetY || y >= rect.yMax - insetY;
                if (!border) continue;
                int index = y * Width + x;
                var a = left[index];
                var b = right[index];
                if (Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b) > 60)
                    count++;
            }
            return count;
        }

        private static int CountColor(IReadOnlyList<Color32> pixels, Rect rect,
            Color32 expected, int tolerance)
        {
            int count = 0;
            int xMin = Mathf.Clamp(Mathf.FloorToInt(rect.xMin), 0, Width - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(rect.xMax), 1, Width);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(rect.yMin), 0, Height - 1);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(rect.yMax), 1, Height);
            for (int y = yMin; y < yMax; y++)
            for (int x = xMin; x < xMax; x++)
                if (Close(pixels[y * Width + x], expected, tolerance)) count++;
            return count;
        }

        private static int PixelDelta(IReadOnlyList<Color32> left,
            IReadOnlyList<Color32> right, Rect rect)
        {
            int count = 0;
            int xMin = Mathf.Clamp(Mathf.FloorToInt(rect.xMin), 0, Width - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(rect.xMax), 1, Width);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(rect.yMin), 0, Height - 1);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(rect.yMax), 1, Height);
            for (int y = yMin; y < yMax; y++)
            for (int x = xMin; x < xMax; x++)
            {
                int index = y * Width + x;
                var a = left[index];
                var b = right[index];
                if (Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b) > 60)
                    count++;
            }
            return count;
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

        private void AssertCaptureColor(string path, RectTransform target, Color32 expected,
            int minimum, string message)
        {
            var pixels = LoadPixels(path);
            Assert.That(CountColor(pixels, ScreenRect(target), expected, 30),
                Is.GreaterThan(minimum), message);
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

        internal static CosmeticAssetInventory ShippedInventory() =>
            CosmeticAssetInventory.Parse(Resources.Load<TextAsset>("Cosmetics/portrait_assets").text,
                CosmeticPortraitPainter.SupportedRendererTokens);

        internal static CosmeticCatalog ShippedCatalog(CosmeticAssetInventory inventory) =>
            CosmeticCatalog.Parse(Resources.Load<TextAsset>("Cosmetics/cosmetic_catalog").text,
                inventory.AssetIds, inventory.ProvenanceAssetIds);

        private static PurchaseCatalog ShippedPurchaseCatalog() => PurchaseCatalog.Parse(
            Resources.Load<TextAsset>("Monetization/product_catalog").text);

        internal static JObject ShippedCosmeticCatalogRoot() => JObject.Parse(
            Resources.Load<TextAsset>("Cosmetics/cosmetic_catalog").text);

        private static JObject ShippedInventoryRoot() => JObject.Parse(
            Resources.Load<TextAsset>("Cosmetics/portrait_assets").text);

        internal static JObject Item(JObject root, string id) => root["items"].Children<JObject>()
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
            private readonly Queue<Action<CosmeticRewardedCompletion>> _pending =
                new Queue<Action<CosmeticRewardedCompletion>>();
            public Action BeforeCompletion { get; set; }
            public bool DeferCompletion { get; set; }
            public CosmeticRewardedCompletion NextCompletion { get; set; } =
                CosmeticRewardedCompletion.Granted;
            public int RequestCalls { get; private set; }
            public string LastPlacementId { get; private set; }
            public int PendingCount => _pending.Count;

            public RecordingRewardedRoute(bool canOffer) => _canOffer = canOffer;
            public event Action AvailabilityChanged { add { } remove { } }
            public bool CanOffer(string placementId, string entitlementId) => _canOffer;
            public void Request(string placementId, string entitlementId,
                Action<CosmeticRewardedCompletion> completed)
            {
                RequestCalls++;
                LastPlacementId = placementId;
                if (DeferCompletion)
                {
                    _pending.Enqueue(completed);
                    return;
                }
                Complete(completed);
            }

            public void CompleteNext()
            {
                if (_pending.Count == 0)
                    throw new InvalidOperationException("no rewarded callback is pending");
                Complete(_pending.Dequeue());
            }

            private void Complete(Action<CosmeticRewardedCompletion> completed)
            {
                BeforeCompletion?.Invoke();
                completed?.Invoke(NextCompletion);
            }
        }

        private sealed class AcceptingLeasePersistence : IEntitlementLeasePersistence
        {
            public bool TryReplaceRewardedAdLeases(IReadOnlyList<EntitlementGrant> leases) => true;
        }

        private sealed class WardrobeBackend : IPurchaseBackend
        {
            private readonly Dictionary<string, StoreProductView> _products =
                new Dictionary<string, StoreProductView>(StringComparer.Ordinal);
            private readonly HashSet<string> _storeEntitlements =
                new HashSet<string>(StringComparer.Ordinal);
            private readonly Queue<Action<IReadOnlyList<StoreProductView>>> _productCallbacks =
                new Queue<Action<IReadOnlyList<StoreProductView>>>();
            private readonly Queue<Action<EntitlementSnapshot>> _entitlementCallbacks =
                new Queue<Action<EntitlementSnapshot>>();
            private Action<PurchaseResult> _purchaseCallback;
            private Action<RestoreResult> _restoreCallback;
            public PurchaseCatalog Catalog { get; set; }
            public BackendAvailability Availability { get; set; } = BackendAvailability.Ready;
            public PurchaseOutcome NextPurchaseOutcome { get; set; } =
                PurchaseOutcome.SuccessCandidate;
            public RestoreOutcome NextRestoreOutcome { get; set; } = RestoreOutcome.Completed;
            public bool GrantOnPurchase { get; set; }
            public bool EntitlementsAreAuthoritative { get; set; } = true;
            public bool ReturnConfirmedPurchaseSnapshot { get; set; }
            public bool DeferPurchase { get; set; }
            public bool DeferRestore { get; set; }
            public bool DeferProductFetch { get; set; }
            public bool DeferEntitlementRefresh { get; set; }
            public IReadOnlyList<string> RestoreEntitlements { get; set; } = Array.Empty<string>();
            public int PurchaseCalls { get; private set; }
            public int RestoreCalls { get; private set; }
            public int ProductFetchCalls { get; private set; }
            public string LastPurchasedProductId { get; private set; }
            public int PendingProductFetches => _productCallbacks.Count;

            public WardrobeBackend WithProduct(string id, string price)
            {
                _products[id] = new StoreProductView(id, id, new LocalizedPrice(price));
                return this;
            }

            public void RemoveProduct(string id) => _products.Remove(id);
            public void ClearProducts() => _products.Clear();
            public WardrobeBackend WithStoreEntitlement(string id)
            {
                _storeEntitlements.Add(id);
                return this;
            }
            public void RevokeAllStoreEntitlements() => _storeEntitlements.Clear();

            public void FetchProducts(Action<IReadOnlyList<StoreProductView>> onDone)
            {
                ProductFetchCalls++;
                if (DeferProductFetch)
                {
                    _productCallbacks.Enqueue(onDone);
                    return;
                }
                onDone?.Invoke(ProductsForFetch());
            }

            public void Purchase(string productId, Action<PurchaseResult> onDone)
            {
                PurchaseCalls++;
                LastPurchasedProductId = productId;
                if (DeferPurchase)
                {
                    if (_purchaseCallback != null)
                        throw new InvalidOperationException("only one backend purchase may pend");
                    _purchaseCallback = onDone;
                    return;
                }
                CompletePurchase(onDone);
            }

            public void Restore(Action<RestoreResult> onDone)
            {
                RestoreCalls++;
                if (DeferRestore)
                {
                    if (_restoreCallback != null)
                        throw new InvalidOperationException("only one backend restore may pend");
                    _restoreCallback = onDone;
                    return;
                }
                CompleteRestore(onDone);
            }

            public void RefreshEntitlements(Action<EntitlementSnapshot> onDone)
            {
                if (DeferEntitlementRefresh)
                {
                    _entitlementCallbacks.Enqueue(onDone);
                    return;
                }
                onDone?.Invoke(Snapshot());
            }

            public void CompletePurchase()
            {
                if (_purchaseCallback == null)
                    throw new InvalidOperationException("no backend purchase is pending");
                var callback = _purchaseCallback;
                _purchaseCallback = null;
                CompletePurchase(callback);
            }

            public void CompleteRestore()
            {
                if (_restoreCallback == null)
                    throw new InvalidOperationException("no backend restore is pending");
                var callback = _restoreCallback;
                _restoreCallback = null;
                CompleteRestore(callback);
            }

            public void CompleteNextProductFetch()
            {
                if (_productCallbacks.Count == 0)
                    throw new InvalidOperationException("no product refresh is pending");
                _productCallbacks.Dequeue()?.Invoke(ProductsForFetch());
            }

            public void CompleteNextEntitlementRefresh()
            {
                if (_entitlementCallbacks.Count == 0)
                    throw new InvalidOperationException("no entitlement refresh is pending");
                _entitlementCallbacks.Dequeue()?.Invoke(Snapshot());
            }

            private void CompletePurchase(Action<PurchaseResult> callback)
            {
                if (GrantOnPurchase && NextPurchaseOutcome == PurchaseOutcome.SuccessCandidate)
                    foreach (var id in Catalog.EntitlementsFor(LastPurchasedProductId))
                        _storeEntitlements.Add(id);
                callback?.Invoke(new PurchaseResult(NextPurchaseOutcome, LastPurchasedProductId,
                    confirmedEntitlements: ReturnConfirmedPurchaseSnapshot ? Snapshot() : null));
            }

            private void CompleteRestore(Action<RestoreResult> callback)
            {
                if (NextRestoreOutcome == RestoreOutcome.Completed)
                    foreach (var id in RestoreEntitlements) _storeEntitlements.Add(id);
                callback?.Invoke(new RestoreResult(NextRestoreOutcome));
            }

            private IReadOnlyList<StoreProductView> ProductsForFetch()
                => Availability == BackendAvailability.Ready
                    ? _products.Values.ToArray()
                    : Array.Empty<StoreProductView>();

            private EntitlementSnapshot Snapshot() => EntitlementsAreAuthoritative
                ? new EntitlementSnapshot(true, _storeEntitlements.Select(id =>
                    new EntitlementGrant(id, GrantSource.Store)).ToArray())
                : EntitlementSnapshot.Unreachable();
        }
    }
}
