using System;
using System.Collections;
using System.Collections.Generic;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;
using CatMetro.Presentation.Strings;
using CatMetro.Services.Ads;
using CatMetro.Services.Purchases;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    public sealed class WardrobeRewardedPlacementTests
    {
        private readonly struct CardSpec
        {
            public readonly string PlacementId;
            public readonly string EntitlementId;
            public readonly string NameKey;
            public readonly string[] SilhouetteParts;

            public CardSpec(string placementId, string entitlementId, string nameKey,
                params string[] silhouetteParts)
            {
                PlacementId = placementId;
                EntitlementId = entitlementId;
                NameKey = nameKey;
                SilhouetteParts = silhouetteParts;
            }
        }

        private static readonly CardSpec[] Cards =
        {
            new CardSpec("wardrobe_try_conductor", EntitlementIds.OutfitConductor,
                "wardrobe.tryon.conductor", "ConductorCoat", "ConductorHat"),
            new CardSpec("wardrobe_try_engineer", EntitlementIds.OutfitEngineer,
                "wardrobe.tryon.engineer", "EngineerBib", "EngineerBuckle"),
            new CardSpec("wardrobe_try_scarf", EntitlementIds.AccessoryScarf,
                "wardrobe.tryon.scarf", "ScarfLeft", "ScarfRight"),
            new CardSpec("wardrobe_try_goggles", EntitlementIds.AccessoryGoggles,
                "wardrobe.tryon.goggles", "GoggleLeft", "GoggleRight"),
        };

        private static readonly Rect CaptureSafeArea = new Rect(0f, 64f, 917f, 1920f);
        private static readonly Rect PixelSafeArea = new Rect(0f, 96f, 1344f, 2760f);
        private const float CaptureDpi = 408f;
        private const float PixelDpi = 495f;

        private GameObject _canvasHost;
        private GameObject _cameraHost;
        private GameObject _inputHost;
        private Camera _camera;
        private RenderTexture _captureTarget;
        private WardrobeScreenView _view;
        private TapInput _input;
        private TrackingRewardedAds _ads;

        [TearDown]
        public void TearDown()
        {
            RewardedAdRuntime.ResetForTests();
            if (_camera != null) _camera.targetTexture = null;
            if (_captureTarget != null)
            {
                _captureTarget.Release();
                UnityEngine.Object.Destroy(_captureTarget);
            }
            if (_canvasHost != null) UnityEngine.Object.Destroy(_canvasHost);
            if (_cameraHost != null) UnityEngine.Object.Destroy(_cameraHost);
            if (_inputHost != null) UnityEngine.Object.Destroy(_inputHost);
            _canvasHost = null;
            _cameraHost = null;
            _inputHost = null;
            _camera = null;
            _captureTarget = null;
            _view = null;
            _input = null;
            _ads = null;
        }

        [UnityTest]
        public IEnumerator CameraProjectedActionCentres_RouteThroughRealTapInput_ToExactShowsOnly()
        {
            var service = CreateService(new WardrobeBackend(), () => 1_000L);
            CreateRig(service, 917, 2048);
            SetAllAdsAvailable(true);

            yield return null; // ScreenSpaceCamera observes the bound RenderTexture first.
            yield return OpenAndLayout(CaptureSafeArea, CaptureDpi);

            for (int i = 0; i < Cards.Length; i++)
            {
                var action = FindCardChild(i, "ActionChip") as RectTransform;
                var actionScreenRect = ProjectedScreenRect(action);
                Assert.That(_input.HandleTapAtScreen(actionScreenRect.center), Is.EqualTo(-3),
                    Cards[i].PlacementId + " visible action must resolve in screen pixels");
                Assert.That(_ads.ShownPlacements.Count, Is.EqualTo(i + 1));
                Assert.That(_ads.ShownPlacements[i], Is.EqualTo(Cards[i].PlacementId),
                    "each painted action has one fixed placement mapping");

                for (int card = 0; card < Cards.Length; card++)
                {
                    AssertBorrowedState(card, false);
                    Assert.That(FindCardChild(card, "SuccessLabel"), Is.Not.Null,
                        "success feedback has a real runtime TMP consumer");
                    Assert.That(FindCardChild(card, "SuccessLabel").gameObject.activeSelf, Is.False,
                        "Show == Started is not entitlement authority");
                }
            }
        }

        [UnityTest]
        public IEnumerator ActualActionChips_Clear48DpFloor_OnBothPhoneTargets()
        {
            CreateRig(CreateService(new WardrobeBackend(), () => 1_000L), 917, 2048);
            SetAllAdsAvailable(true);
            yield return null;
            yield return OpenAndLayout(CaptureSafeArea, CaptureDpi);
            AssertActionTargets(CaptureSafeArea, CaptureDpi, "917x2048");

            RebindTarget(1344, 2992);
            yield return null;
            yield return Layout(PixelSafeArea, PixelDpi);
            AssertActionTargets(PixelSafeArea, PixelDpi, "1344x2992");
        }

        [UnityTest]
        public IEnumerator ActionOnlyRegions_FollowLiveRelayout_AndRejectCardGapOutsideAndStalePoints()
        {
            CreateRig(CreateService(new WardrobeBackend(), () => 1_000L), 917, 2048);
            SetAllAdsAvailable(true);
            yield return null;
            yield return OpenAndLayout(CaptureSafeArea, CaptureDpi);

            var oldActions = new Rect[Cards.Length];
            var oldCards = new Rect[Cards.Length];
            for (int i = 0; i < Cards.Length; i++)
            {
                oldActions[i] = ProjectedScreenRect(FindCardChild(i, "ActionChip") as RectTransform);
                oldCards[i] = ProjectedScreenRect(FindCard(i));
                Assert.That(_input.HandleTapAtScreen(oldActions[i].center), Is.EqualTo(-3));
                var silhouettePoint = new Vector2(oldActions[i].center.x,
                    (oldActions[i].yMax + oldCards[i].yMax) * 0.5f);
                Assert.That(_input.HandleTapAtScreen(silhouettePoint), Is.EqualTo(-1),
                    Cards[i].PlacementId + " silhouette/card body must not start an ad");
                if (i > 0)
                {
                    var gap = new Vector2((oldCards[i - 1].xMax + oldCards[i].xMin) * 0.5f,
                        oldActions[i].center.y);
                    Assert.That(_input.HandleTapAtScreen(gap), Is.EqualTo(-1),
                        "every painted inter-card gap is a miss");
                }
            }
            var strip = ProjectedScreenRect(FindRequired("WardrobePanel/TryOnStrip") as RectTransform);
            Assert.That(_input.HandleTapAtScreen(new Vector2(strip.center.x, strip.yMax + 2f)),
                Is.EqualTo(-1), "a point outside the strip must miss");

            Vector2 staleOnlyPoint = oldActions[0].center;
            RebindTarget(1344, 2992);
            yield return null;
            yield return Layout(PixelSafeArea, PixelDpi);

            Assert.That(_input.HandleTapAtScreen(staleOnlyPoint), Is.EqualTo(-1),
                "a pre-relayout-only point must not survive the live provider");
            for (int i = 0; i < Cards.Length; i++)
            {
                var moved = ProjectedScreenRect(FindCardChild(i, "ActionChip") as RectTransform);
                Assert.That(_input.HandleTapAtScreen(moved.center), Is.EqualTo(-3),
                    Cards[i].PlacementId + " live action must follow the repainted target");
            }
        }

        [UnityTest]
        public IEnumerator RuntimeTmp_UsesExactStrings_MeshesAndSourceNeutralLedgerSuccess()
        {
            long now = 1_000L;
            var service = CreateService(new WardrobeBackend(), () => now);
            CreateRig(service, 917, 2048);
            SetAllAdsAvailable(true);
            yield return null;
            yield return OpenAndLayout(CaptureSafeArea, CaptureDpi);

            AssertText("WardrobePanel/TryOnStrip/TryOnHeading/TryOnHeadingLabel",
                "wardrobe.tryon.heading");
            for (int i = 0; i < Cards.Length; i++)
            {
                AssertCardText(i, "ItemName", Cards[i].NameKey);
                AssertCardText(i, "LockedLabel", "wardrobe.tryon.locked");
                AssertCardText(i, "ActionChip/ActionLabel", "wardrobe.tryon.watch");
            }

            _ads.SetAvailable(Cards[1].PlacementId, false);
            _ads.RaiseAvailabilityChanged();
            yield return null;
            AssertCardText(1, "UnavailableLabel", "wardrobe.tryon.unavailable");

            Assert.That(service.GrantRewardedAdEntitlement(Cards[0].EntitlementId),
                Is.EqualTo(AdGrantOutcome.Granted));
            yield return null;
            AssertCardText(0, "BorrowedLabel", "wardrobe.tryon.borrowed");
            AssertCardText(0, "SuccessLabel", "wardrobe.tryon.success");
            Assert.That(UiStrings.Get("wardrobe.tryon.success"), Is.EqualTo("Ready to wear!"),
                "success copy stays truthful for store, restored, promo, or rewarded authority");

            var activeLabels = _view.GetComponentsInChildren<TMP_Text>(false);
            Assert.That(activeLabels.Length, Is.GreaterThan(0));
            foreach (var label in activeLabels)
            {
                label.ForceMeshUpdate();
                Assert.That(label.text, Is.Not.Empty, label.name + " has visible copy");
                Assert.That(label.text, Does.Not.StartWith("??"), label.name + " resolved a CSV key");
                Assert.That(label.textInfo.characterCount, Is.GreaterThan(0),
                    label.name + " generated a real TMP mesh");
                Assert.That(label.isTextOverflowing, Is.False, label.name + " must not overflow");
                Assert.That(label.text, Does.Not.Contain("LevelPlay").IgnoreCase);
                Assert.That(label.text, Does.Not.Contain("ironSource").IgnoreCase);
                Assert.That(label.text, Does.Not.Contain("RevenueCat").IgnoreCase);
            }
        }

        [UnityTest]
        public IEnumerator StoreAndRewardedAuthority_MapExactlyOneEntitlementToExactlyOneCard()
        {
            long now = 1_000L;
            var backend = new WardrobeBackend();
            var service = CreateService(backend, () => now);
            CreateRig(service, 917, 2048);
            yield return null;
            yield return OpenAndLayout(CaptureSafeArea, CaptureDpi);

            for (int expected = 0; expected < Cards.Length; expected++)
            {
                backend.SetOwned(Cards[expected].EntitlementId);
                service.RefreshEntitlements();
                yield return null;
                AssertExactlyCard(expected);
            }

            backend.SetOwned();
            service.RefreshEntitlements();
            yield return null;
            AssertExactlyCard(-1);
            for (int expected = 0; expected < Cards.Length; expected++)
            {
                Assert.That(service.GrantRewardedAdEntitlement(Cards[expected].EntitlementId),
                    Is.EqualTo(AdGrantOutcome.Granted));
                yield return null;
                AssertExactlyCard(expected);
                now += 3_601L;
                Assert.That(service.PruneExpiredLeases(), Is.True);
                yield return null;
                AssertExactlyCard(-1);
            }
        }

        [UnityTest]
        public IEnumerator FourStaggeredLeases_FirstExpiryRemovesOnlyFirstCard()
        {
            long now = 1_000L;
            var service = CreateService(new WardrobeBackend(), () => now);
            CreateRig(service, 917, 2048);
            yield return null;
            yield return OpenAndLayout(CaptureSafeArea, CaptureDpi);

            service.GrantRewardedAdEntitlement(Cards[0].EntitlementId);
            now = 2_000L;
            service.GrantRewardedAdEntitlement(Cards[1].EntitlementId);
            now = 2_100L;
            service.GrantRewardedAdEntitlement(Cards[2].EntitlementId);
            now = 2_200L;
            service.GrantRewardedAdEntitlement(Cards[3].EntitlementId);
            yield return null;
            for (int i = 0; i < Cards.Length; i++) AssertBorrowedState(i, true);

            now = 4_601L;
            Assert.That(service.PruneExpiredLeases(), Is.True);
            yield return null;
            AssertBorrowedState(0, false);
            for (int i = 1; i < Cards.Length; i++) AssertBorrowedState(i, true);
        }

        [UnityTest]
        public IEnumerator OneNoFill_RemovesOnlyThatAction_AndPreservesOtherAdsPurchaseRestoreAndBack()
        {
            var backend = new WardrobeBackend();
            CreateRig(CreateService(backend, () => 1_000L), 917, 2048);
            SetAllAdsAvailable(true);
            yield return null;
            yield return OpenAndLayout(CaptureSafeArea, CaptureDpi);

            var removedCentre = ProjectedScreenRect(
                FindCardChild(2, "ActionChip") as RectTransform).center;
            _ads.SetAvailable(Cards[2].PlacementId, false);
            _ads.RaiseAvailabilityChanged();
            yield return null;

            Assert.That(FindCard(2).gameObject.activeInHierarchy, Is.True,
                "no-fill leaves the named preview visible");
            Assert.That(FindCardChild(2, "Silhouette").gameObject.activeInHierarchy, Is.True);
            Assert.That(FindCardChild(2, "ActionChip").gameObject.activeSelf, Is.False);
            Assert.That(_input.Regions.IsRegistered(RegionId(Cards[2].PlacementId)), Is.False);
            Assert.That(_input.HandleTapAtScreen(removedCentre), Is.EqualTo(-1),
                "the removed painted action centre is no longer a target");

            for (int i = 0; i < Cards.Length; i++)
            {
                if (i == 2) continue;
                Assert.That(FindCardChild(i, "ActionChip").gameObject.activeSelf, Is.True);
                Assert.That(_input.HandleTapAtScreen(ProjectedScreenRect(
                    FindCardChild(i, "ActionChip") as RectTransform).center), Is.EqualTo(-3));
            }

            int backs = 0;
            _view.BackRequested = () => backs++;
            Assert.That(_input.HandleTapAtScreen(ProjectedScreenRect(
                FindRequired("WardrobePanel/BuyConductorCoatChip") as RectTransform).center),
                Is.EqualTo(-3));
            Assert.That(_input.HandleTapAtScreen(ProjectedScreenRect(
                FindRequired("WardrobePanel/RestorePurchasesChip") as RectTransform).center),
                Is.EqualTo(-3));
            Assert.That(_input.HandleTapAtScreen(ProjectedScreenRect(
                FindRequired("WardrobePanel/BackChip") as RectTransform).center), Is.EqualTo(-3));
            Assert.That(backend.PurchaseCalls, Is.EqualTo(1));
            Assert.That(backend.RestoreCalls, Is.EqualTo(1));
            Assert.That(backs, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AdAndLedgerCallbacksDuringSubscribeAndAfterHide_CannotLeaveGhostState()
        {
            var service = CreateService(new WardrobeBackend(), () => 1_000L);
            CreateRig(service, 917, 2048);
            SetAllAdsAvailable(true);
            _ads.RaiseDuringAdd = true;
            _ads.RaiseDuringRemove = true;
            yield return null;
            yield return OpenAndLayout(CaptureSafeArea, CaptureDpi);

            Assert.That(_ads.AddCount, Is.EqualTo(1));
            Assert.That(_input.Regions.Count, Is.EqualTo(7));
            _view.Hide();
            yield return null;
            Assert.That(_ads.RemoveCount, Is.EqualTo(1));
            Assert.That(_input.Regions.Count, Is.EqualTo(0));

            int canShowAfterHide = _ads.CanShowCalls;
            _ads.RaiseAvailabilityChanged();
            service.GrantRewardedAdEntitlement(Cards[0].EntitlementId);
            yield return null;
            Assert.That(_ads.CanShowCalls, Is.EqualTo(canShowAfterHide),
                "post-Hide callbacks cannot refresh presentation");
            Assert.That(_input.Regions.Count, Is.EqualTo(0),
                "post-Hide ads/ledger changes cannot resurrect regions");

            _view.Open();
            _view.LayoutForViewport(CaptureSafeArea, CaptureDpi);
            Canvas.ForceUpdateCanvases();
            yield return null;
            AssertBorrowedState(0, true);
            Assert.That(_input.Regions.Count, Is.EqualTo(6),
                "reopen reads the shared ledger and offers only the other three actions");

            _view.gameObject.SetActive(false);
            yield return null;
            Assert.That(_ads.RemoveCount, Is.EqualTo(2));
            Assert.That(_input.Regions.Count, Is.EqualTo(0));
            _ads.RaiseAvailabilityChanged();
            Assert.That(_input.Regions.Count, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ActualProjectedWardrobeGeometry_IsOrderedAndContained_OnBothPhones()
        {
            CreateRig(CreateService(new WardrobeBackend(), () => 1_000L), 917, 2048);
            SetAllAdsAvailable(true);
            yield return null;
            yield return OpenAndLayout(CaptureSafeArea, CaptureDpi);
            AssertGeometry(CaptureSafeArea, CaptureDpi, "917x2048");

            RebindTarget(1344, 2992);
            yield return null;
            yield return Layout(PixelSafeArea, PixelDpi);
            AssertGeometry(PixelSafeArea, PixelDpi, "1344x2992");
        }

        [UnityTest]
        public IEnumerator ProductionCreateOverload_UsesRewardedAdRuntimeCurrent()
        {
            var service = CreateService(new WardrobeBackend(), () => 1_000L);
            _ads = new TrackingRewardedAds();
            _ads.SetAvailable(Cards[0].PlacementId, true);
            RewardedAdRuntime.Install(_ads);
            CreateRig(service, 917, 2048, useProductionOverload: true);
            yield return null;
            yield return OpenAndLayout(CaptureSafeArea, CaptureDpi);

            Assert.That(_input.Regions.IsRegistered(RegionId(Cards[0].PlacementId)), Is.True,
                "production composition consumes the installed optional runtime seam");
        }

        private void CreateRig(PurchaseService service, int width, int height,
            bool useProductionOverload = false)
        {
            _cameraHost = new GameObject("WardrobeRewardedCamera");
            _camera = _cameraHost.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 100f;
            _captureTarget = new RenderTexture(width, height, 24);
            _camera.targetTexture = _captureTarget;

            _canvasHost = new GameObject("WardrobeRewardedCanvas");
            var canvas = _canvasHost.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 120;

            _inputHost = new GameObject("WardrobeRewardedTapInput");
            _input = _inputHost.AddComponent<TapInput>();
            if (_ads == null) _ads = new TrackingRewardedAds();
            _view = useProductionOverload
                ? WardrobeScreenView.Create(canvas.transform, service)
                : WardrobeScreenView.Create(canvas.transform, service, _ads);
            _view.Attach(_input.Regions);
        }

        private IEnumerator OpenAndLayout(Rect safeArea, float dpi)
        {
            _view.Open();
            yield return Layout(safeArea, dpi);
        }

        private IEnumerator Layout(Rect safeArea, float dpi)
        {
            _view.LayoutForViewport(safeArea, dpi);
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            _camera.Render();
        }

        private void RebindTarget(int width, int height)
        {
            _camera.targetTexture = null;
            _captureTarget.Release();
            UnityEngine.Object.Destroy(_captureTarget);
            _captureTarget = new RenderTexture(width, height, 24);
            _camera.targetTexture = _captureTarget;
        }

        private void SetAllAdsAvailable(bool available)
        {
            for (int i = 0; i < Cards.Length; i++)
                _ads.SetAvailable(Cards[i].PlacementId, available);
        }

        private Transform FindRequired(string relativePath)
        {
            var found = _view.transform.Find(relativePath);
            Assert.That(found, Is.Not.Null, relativePath + " must exist as rendered geometry");
            return found;
        }

        private RectTransform FindCard(int index)
            => FindRequired("WardrobePanel/TryOnStrip/TryOnCard_" + Cards[index].PlacementId)
                as RectTransform;

        private Transform FindCardChild(int index, string relativePath)
        {
            var found = FindCard(index).Find(relativePath);
            Assert.That(found, Is.Not.Null,
                Cards[index].PlacementId + "/" + relativePath + " must exist at runtime");
            return found;
        }

        private static Rect ProjectedScreenRect(RectTransform rect)
        {
            Assert.That(rect, Is.Not.Null);
            var canvas = rect.GetComponentInParent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Camera owningCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            Assert.That(owningCamera, Is.Not.Null,
                "the production-shaped canvas must own a projection camera");

            var worldCorners = new Vector3[4];
            rect.GetWorldCorners(worldCorners);
            Vector2 first = RectTransformUtility.WorldToScreenPoint(owningCamera, worldCorners[0]);
            float xMin = first.x;
            float xMax = first.x;
            float yMin = first.y;
            float yMax = first.y;
            for (int i = 1; i < worldCorners.Length; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(owningCamera, worldCorners[i]);
                xMin = Mathf.Min(xMin, screen.x);
                xMax = Mathf.Max(xMax, screen.x);
                yMin = Mathf.Min(yMin, screen.y);
                yMax = Mathf.Max(yMax, screen.y);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private void AssertActionTargets(Rect safeArea, float dpi, string phone)
        {
            for (int i = 0; i < Cards.Length; i++)
            {
                var card = FindCard(i);
                Assert.That(card.gameObject.activeInHierarchy, Is.True);
                var action = ProjectedScreenRect(FindCardChild(i, "ActionChip") as RectTransform);
                AssertContained(action, safeArea, phone + " " + Cards[i].PlacementId);
                Assert.That(HudBands.MeetsMinTargetPx(action, dpi), Is.True,
                    phone + " " + Cards[i].PlacementId + " visible ActionChip clears 48dp");
                Assert.That(_input.Regions.IsRegistered(RegionId(Cards[i].PlacementId)), Is.True);
                for (int part = 0; part < Cards[i].SilhouetteParts.Length; part++)
                    Assert.That(FindCardChild(i,
                        "Silhouette/" + Cards[i].SilhouetteParts[part]), Is.Not.Null);
            }
        }

        private void AssertGeometry(Rect safeArea, float dpi, string phone)
        {
            var status = ProjectedScreenRect(
                FindRequired("WardrobePanel/WardrobeStatus") as RectTransform);
            var strip = ProjectedScreenRect(
                FindRequired("WardrobePanel/TryOnStrip") as RectTransform);
            var heading = ProjectedScreenRect(
                FindRequired("WardrobePanel/TryOnStrip/TryOnHeading") as RectTransform);
            var portrait = ProjectedScreenRect(
                FindRequired("WardrobePanel/ProfileCatCard") as RectTransform);
            var title = ProjectedScreenRect(
                FindRequired("WardrobePanel/WardrobeTitle") as RectTransform);

            AssertContained(status, safeArea, phone + " status");
            AssertContained(strip, safeArea, phone + " strip");
            AssertContained(heading, strip, phone + " heading");
            AssertContained(portrait, safeArea, phone + " portrait");
            AssertContained(title, safeArea, phone + " title");
            Assert.That(status.yMax, Is.LessThan(strip.yMin), phone + " status before strip");
            Assert.That(strip.yMax, Is.LessThan(portrait.yMin), phone + " strip before portrait");
            Assert.That(portrait.yMax, Is.LessThan(title.yMin), phone + " portrait before title");
            Assert.That(portrait.height, Is.GreaterThanOrEqualTo(120f * HudBands.PxPerDp(dpi)),
                phone + " resized portrait remains the dominant readable card");

            Rect previousCard = Rect.zero;
            for (int i = 0; i < Cards.Length; i++)
            {
                var card = ProjectedScreenRect(FindCard(i));
                var action = ProjectedScreenRect(FindCardChild(i, "ActionChip") as RectTransform);
                AssertContained(card, strip, phone + " card " + i);
                AssertContained(action, card, phone + " action " + i);
                Assert.That(HudBands.MeetsMinTargetPx(action, dpi), Is.True);
                if (i > 0) Assert.That(previousCard.xMax, Is.LessThan(card.xMin));
                previousCard = card;
            }
        }

        private void AssertExactlyCard(int expected)
        {
            for (int i = 0; i < Cards.Length; i++)
                AssertBorrowedState(i, i == expected);
        }

        private void AssertBorrowedState(int index, bool expected)
        {
            Assert.That(FindCardChild(index, "BorrowedAccent").gameObject.activeSelf,
                Is.EqualTo(expected), Cards[index].EntitlementId + " exact accent mapping");
            Assert.That(FindCardChild(index, "LockedLabel").gameObject.activeSelf,
                Is.EqualTo(!expected));
            Assert.That(FindCardChild(index, "BorrowedLabel").gameObject.activeSelf,
                Is.EqualTo(expected));
            var success = FindCardChild(index, "SuccessLabel");
            Assert.That(success.gameObject.activeSelf, Is.EqualTo(expected),
                "success is source-neutral and driven only by PurchaseService.IsUnlocked");
        }

        private void AssertText(string relativePath, string key)
        {
            var label = FindRequired(relativePath).GetComponent<TMP_Text>();
            Assert.That(label, Is.Not.Null);
            Assert.That(label.gameObject.activeInHierarchy, Is.True);
            Assert.That(label.text, Is.EqualTo(UiStrings.Get(key)));
        }

        private void AssertCardText(int index, string relativePath, string key)
        {
            var label = FindCardChild(index, relativePath).GetComponent<TMP_Text>();
            Assert.That(label, Is.Not.Null);
            Assert.That(label.gameObject.activeInHierarchy, Is.True);
            Assert.That(label.text, Is.EqualTo(UiStrings.Get(key)));
        }

        private static void AssertContained(Rect inner, Rect outer, string name)
        {
            const float tolerance = 0.75f;
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - tolerance), name + " left");
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + tolerance), name + " right");
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - tolerance), name + " bottom");
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + tolerance), name + " top");
        }

        private static string RegionId(string placementId) => "wardrobe.rewarded." + placementId;

        private static PurchaseService CreateService(WardrobeBackend backend, Func<long> clock)
        {
            var service = new PurchaseService(Catalog(), backend, clock);
            service.AttachLeasePersistence(new AcceptingLeasePersistence());
            return service;
        }

        private static PurchaseCatalog Catalog() => PurchaseCatalog.Parse(@"{
          'entitlements': [
            { 'id': 'outfit_conductor', 'kind': 'outfit', 'display': 'Conductor Coat', 'adLeaseSeconds': 3600 },
            { 'id': 'outfit_engineer', 'kind': 'outfit', 'display': 'Engineer Overalls', 'adLeaseSeconds': 3600 },
            { 'id': 'accessory_scarf', 'kind': 'accessory', 'display': 'Signal Scarf', 'adLeaseSeconds': 3600 },
            { 'id': 'accessory_goggles', 'kind': 'accessory', 'display': 'Depot Goggles', 'adLeaseSeconds': 3600 }
          ],
          'products': [
            { 'id': 'cm_outfit_conductor', 'storeType': 'non_consumable', 'display': 'Conductor Coat', 'entitlements': ['outfit_conductor'] },
            { 'id': 'cm_outfit_engineer', 'storeType': 'non_consumable', 'display': 'Engineer Overalls', 'entitlements': ['outfit_engineer'] },
            { 'id': 'cm_accessory_scarf', 'storeType': 'non_consumable', 'display': 'Signal Scarf', 'entitlements': ['accessory_scarf'] },
            { 'id': 'cm_accessory_goggles', 'storeType': 'non_consumable', 'display': 'Depot Goggles', 'entitlements': ['accessory_goggles'] }
          ]
        }");

        private sealed class TrackingRewardedAds : IRewardedAds
        {
            private readonly Dictionary<string, bool> _available =
                new Dictionary<string, bool>(StringComparer.Ordinal);
            private event Action Changed;

            public readonly List<string> ShownPlacements = new List<string>();
            public int AddCount { get; private set; }
            public int RemoveCount { get; private set; }
            public int CanShowCalls { get; private set; }
            public bool RaiseDuringAdd { get; set; }
            public bool RaiseDuringRemove { get; set; }

            public event Action AvailabilityChanged
            {
                add
                {
                    AddCount++;
                    Changed += value;
                    if (RaiseDuringAdd) value?.Invoke();
                }
                remove
                {
                    RemoveCount++;
                    if (RaiseDuringRemove) value?.Invoke();
                    Changed -= value;
                }
            }

            public void SetAvailable(string placementId, bool value)
                => _available[placementId] = value;

            public void RaiseAvailabilityChanged() => Changed?.Invoke();

            public bool CanShow(string placementId)
            {
                CanShowCalls++;
                return _available.TryGetValue(placementId, out var available) && available;
            }

            public RewardedShowOutcome Show(string placementId)
            {
                ShownPlacements.Add(placementId);
                return RewardedShowOutcome.Started;
            }
        }

        private sealed class AcceptingLeasePersistence : IEntitlementLeasePersistence
        {
            public bool TryReplaceRewardedAdLeases(IReadOnlyList<EntitlementGrant> leases) => true;
        }

        private sealed class WardrobeBackend : IPurchaseBackend
        {
            private readonly List<EntitlementGrant> _owned = new List<EntitlementGrant>();
            public int PurchaseCalls { get; private set; }
            public int RestoreCalls { get; private set; }
            public BackendAvailability Availability => BackendAvailability.Ready;

            public void SetOwned(params string[] entitlementIds)
            {
                _owned.Clear();
                for (int i = 0; i < entitlementIds.Length; i++)
                    _owned.Add(new EntitlementGrant(entitlementIds[i], GrantSource.Store));
            }

            public void FetchProducts(Action<IReadOnlyList<StoreProductView>> onDone)
                => onDone?.Invoke(new[]
                {
                    new StoreProductView(ProductIds.Gate, "Conductor Coat", new LocalizedPrice("$1.99"))
                });

            public void Purchase(string productId, Action<PurchaseResult> onDone)
            {
                PurchaseCalls++;
                onDone?.Invoke(PurchaseResult.Unavailable(productId, "not used"));
            }

            public void Restore(Action<RestoreResult> onDone)
            {
                RestoreCalls++;
                onDone?.Invoke(new RestoreResult(RestoreOutcome.Completed));
            }

            public void RefreshEntitlements(Action<EntitlementSnapshot> onDone)
                => onDone?.Invoke(new EntitlementSnapshot(true, _owned.ToArray()));
        }
    }
}
