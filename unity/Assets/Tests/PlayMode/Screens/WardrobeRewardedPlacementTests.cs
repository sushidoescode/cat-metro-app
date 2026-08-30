using System;
using System.Collections;
using System.Collections.Generic;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;
using CatMetro.Services.Ads;
using CatMetro.Services.Purchases;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    public sealed class WardrobeRewardedPlacementTests
    {
        private static readonly string[] PlacementIds =
        {
            "wardrobe_try_conductor",
            "wardrobe_try_engineer",
            "wardrobe_try_scarf",
            "wardrobe_try_goggles",
        };

        private static readonly string[] EntitlementIdsByCard =
        {
            EntitlementIds.OutfitConductor,
            EntitlementIds.OutfitEngineer,
            EntitlementIds.AccessoryScarf,
            EntitlementIds.AccessoryGoggles,
        };

        private static readonly Rect CaptureSafeArea = new Rect(0f, 64f, 917f, 1920f);
        private static readonly Rect PixelSafeArea = new Rect(0f, 96f, 1344f, 2760f);
        private const float CaptureDpi = 408f;
        private const float PixelDpi = 495f;

        private GameObject _canvasHost;
        private WardrobeScreenView _view;
        private ChromeRegions _regions;
        private TrackingRewardedAds _ads;

        [TearDown]
        public void TearDown()
        {
            RewardedAdRuntime.ResetForTests();
            if (_canvasHost != null) UnityEngine.Object.Destroy(_canvasHost);
            _canvasHost = null;
            _view = null;
            _regions = null;
            _ads = null;
        }

        [UnityTest]
        public IEnumerator FourLockedCards_PaintDistinctSilhouettes_AndResolveExactAdTaps()
        {
            var service = CreateService(new WardrobeBackend(), () => 1_000L);
            CreateView(service);
            for (int i = 0; i < PlacementIds.Length; i++)
                _ads.SetAvailable(PlacementIds[i], true);

            OpenAtCapturePhone();
            yield return null;

            var previous = Rect.zero;
            for (int i = 0; i < PlacementIds.Length; i++)
            {
                var card = FindCard(PlacementIds[i]);
                Assert.That(card.gameObject.activeInHierarchy, Is.True,
                    PlacementIds[i] + " preview remains visible while locked");
                Assert.That(card.Find("BorrowedAccent").gameObject.activeSelf, Is.False,
                    PlacementIds[i] + " cannot invent an unlock outside PurchaseService");
                Assert.That(card.Find("ActionChip").gameObject.activeInHierarchy, Is.True,
                    PlacementIds[i] + " paints the available action");
                Assert.That(card.Find("Silhouette").childCount, Is.GreaterThanOrEqualTo(2),
                    PlacementIds[i] + " uses a chunky multi-part silhouette, not a text placeholder");

                var painted = ScreenRect(card);
                AssertContained(painted, CaptureSafeArea, PlacementIds[i]);
                Assert.That(HudBands.MeetsMinTargetPx(painted, CaptureDpi), Is.True,
                    PlacementIds[i] + " actual painted bounds clear the 48dp target floor");
                Assert.That(_regions.IsRegistered(RegionId(PlacementIds[i])), Is.True,
                    PlacementIds[i] + " actual action is registered");
                Assert.That(_regions.TryResolve(painted.center, out var action), Is.True,
                    PlacementIds[i] + " center resolves through the real ChromeRegions registry");
                action();

                Assert.That(_ads.ShownPlacements.Count, Is.EqualTo(i + 1));
                Assert.That(_ads.ShownPlacements[i], Is.EqualTo(PlacementIds[i]),
                    "the painted card routes only its fixed placement id");

                if (i > 0)
                {
                    Assert.That(painted.xMin, Is.GreaterThan(previous.xMax),
                        "the compact cards remain distinct targets rather than overlapping");
                    var gapPoint = new Vector2((previous.xMax + painted.xMin) * 0.5f, painted.center.y);
                    Assert.That(_regions.TryResolve(gapPoint, out _), Is.False,
                        "a point painted between cards must not start an ad");
                }
                previous = painted;
            }

            Assert.That(FindCard(PlacementIds[0]).Find("Silhouette/ConductorCoat"), Is.Not.Null);
            Assert.That(FindCard(PlacementIds[0]).Find("Silhouette/ConductorHat"), Is.Not.Null);
            Assert.That(FindCard(PlacementIds[1]).Find("Silhouette/EngineerBib"), Is.Not.Null);
            Assert.That(FindCard(PlacementIds[1]).Find("Silhouette/EngineerBuckle"), Is.Not.Null);
            Assert.That(FindCard(PlacementIds[2]).Find("Silhouette/ScarfLeft"), Is.Not.Null);
            Assert.That(FindCard(PlacementIds[2]).Find("Silhouette/ScarfRight"), Is.Not.Null);
            Assert.That(FindCard(PlacementIds[3]).Find("Silhouette/GoggleLeft"), Is.Not.Null);
            Assert.That(FindCard(PlacementIds[3]).Find("Silhouette/GoggleRight"), Is.Not.Null);

            _view.LayoutForViewport(PixelSafeArea, PixelDpi);
            Canvas.ForceUpdateCanvases();
            yield return null;
            for (int i = 0; i < PlacementIds.Length; i++)
            {
                var painted = ScreenRect(FindCard(PlacementIds[i]));
                AssertContained(painted, PixelSafeArea, PlacementIds[i] + " Pixel-class");
                Assert.That(HudBands.MeetsMinTargetPx(painted, PixelDpi), Is.True,
                    PlacementIds[i] + " scales through HudBands at Pixel-class density");
                Assert.That(_regions.TryResolve(painted.center, out _), Is.True,
                    PlacementIds[i] + " live region follows its repainted high-DPI card");
            }
        }

        [UnityTest]
        public IEnumerator NoFill_HidesOnlyAdActions_AndRemovesTheirRegions()
        {
            CreateView(CreateService(new WardrobeBackend(), () => 1_000L));
            OpenAtCapturePhone();
            yield return null;

            Assert.That(_regions.Count, Is.EqualTo(3),
                "Buy, Restore, and Back remain usable when no rewarded ad has fill");
            Assert.That(_regions.IsRegistered("wardrobe.buy"), Is.True);
            Assert.That(_regions.IsRegistered("wardrobe.restore"), Is.True);
            Assert.That(_regions.IsRegistered("wardrobe.back"), Is.True);
            for (int i = 0; i < PlacementIds.Length; i++)
            {
                var card = FindCard(PlacementIds[i]);
                Assert.That(card.gameObject.activeInHierarchy, Is.True,
                    "no-fill keeps the named preview visible");
                Assert.That(card.Find("ActionChip").gameObject.activeSelf, Is.False,
                    "no-fill hides the action instead of painting a ghost button");
                Assert.That(card.Find("UnavailableLabel").gameObject.activeInHierarchy, Is.True);
                Assert.That(_regions.IsRegistered(RegionId(PlacementIds[i])), Is.False);
            }

            int backs = 0;
            _view.BackRequested = () => backs++;
            Assert.That(_regions.TryResolve(_view.BackRectPx.center, out var back), Is.True);
            back();
            Assert.That(backs, Is.EqualTo(1), "normal navigation survives no-fill");

            _ads.SetAvailable(PlacementIds[2], true);
            _ads.RaiseAvailabilityChanged();
            yield return null;
            Assert.That(FindCard(PlacementIds[2]).Find("ActionChip").gameObject.activeSelf, Is.True);
            Assert.That(_regions.IsRegistered(RegionId(PlacementIds[2])), Is.True);
            Assert.That(_regions.Count, Is.EqualTo(4));

            _ads.SetAvailable(PlacementIds[2], false);
            _ads.RaiseAvailabilityChanged();
            yield return null;
            Assert.That(FindCard(PlacementIds[2]).Find("ActionChip").gameObject.activeSelf, Is.False);
            Assert.That(_regions.IsRegistered(RegionId(PlacementIds[2])), Is.False,
                "a later no-fill callback removes the live hit region immediately");
            Assert.That(_regions.Count, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator PaidAndRewardedAuthority_IlluminateTheSameIndependentPreviewState()
        {
            long now = 1_000L;
            var backend = new WardrobeBackend();
            var service = CreateService(backend, () => now);
            CreateView(service);
            OpenAtCapturePhone();
            yield return null;

            Assert.That(service.GrantRewardedAdEntitlement(EntitlementIds.OutfitEngineer),
                Is.EqualTo(AdGrantOutcome.Granted));
            backend.SetOwned(EntitlementIds.AccessoryGoggles);
            service.RefreshEntitlements();
            yield return null;

            AssertBorrowedState(PlacementIds[0], false);
            AssertBorrowedState(PlacementIds[1], true);
            AssertBorrowedState(PlacementIds[2], false);
            AssertBorrowedState(PlacementIds[3], true);

            Assert.That(service.IsUnlocked(EntitlementIds.OutfitEngineer), Is.True,
                "rewarded authority enters the shared ledger");
            Assert.That(service.IsUnlocked(EntitlementIds.AccessoryGoggles), Is.True,
                "paid authority enters the same IsUnlocked query");
        }

        [UnityTest]
        public IEnumerator Expiry_RemovesOnlyTheExpiredCard_WhileAnotherLeaseStaysPainted()
        {
            long now = 1_000L;
            var service = CreateService(new WardrobeBackend(), () => now);
            CreateView(service);
            OpenAtCapturePhone();
            yield return null;

            Assert.That(service.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor),
                Is.EqualTo(AdGrantOutcome.Granted));
            now = 1_100L;
            Assert.That(service.GrantRewardedAdEntitlement(EntitlementIds.AccessoryScarf),
                Is.EqualTo(AdGrantOutcome.Granted));
            yield return null;
            AssertBorrowedState(PlacementIds[0], true);
            AssertBorrowedState(PlacementIds[2], true);

            now = 4_601L;
            Assert.That(service.PruneExpiredLeases(), Is.True);
            yield return null;

            AssertBorrowedState(PlacementIds[0], false);
            AssertBorrowedState(PlacementIds[2], true);
        }

        [UnityTest]
        public IEnumerator OpenHideDisableAndDestroy_BalanceSubscriptionsAndLeaveNoGhostRegions()
        {
            var service = CreateService(new WardrobeBackend(), () => 1_000L);
            CreateView(service);
            for (int i = 0; i < PlacementIds.Length; i++)
                _ads.SetAvailable(PlacementIds[i], true);

            OpenAtCapturePhone();
            yield return null;
            Assert.That(_ads.AddCount, Is.EqualTo(1));
            Assert.That(_ads.RemoveCount, Is.EqualTo(0));
            Assert.That(_regions.Count, Is.EqualTo(7));

            _view.Open();
            yield return null;
            Assert.That(_ads.AddCount, Is.EqualTo(1), "repeated Open cannot duplicate accessors");
            Assert.That(_regions.Count, Is.EqualTo(7), "repeated Open cannot duplicate regions");

            int beforeLedgerEvent = _ads.CanShowCalls;
            service.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor);
            yield return null;
            Assert.That(_ads.CanShowCalls - beforeLedgerEvent, Is.EqualTo(3),
                "one ledger event refreshes the three still-locked cards exactly once");
            Assert.That(_regions.Count, Is.EqualTo(6),
                "the newly unlocked card stops offering an unnecessary ad");

            _view.Hide();
            yield return null;
            Assert.That(_ads.RemoveCount, Is.EqualTo(1));
            Assert.That(_regions.Count, Is.EqualTo(0));
            int afterHide = _ads.CanShowCalls;
            _ads.RaiseAvailabilityChanged();
            Assert.That(_ads.CanShowCalls, Is.EqualTo(afterHide),
                "callbacks after Hide cannot refresh or resurrect targets");

            _view.Open();
            yield return null;
            Assert.That(_ads.AddCount, Is.EqualTo(2));
            Assert.That(_regions.Count, Is.EqualTo(6));

            _view.gameObject.SetActive(false);
            yield return null;
            Assert.That(_ads.RemoveCount, Is.EqualTo(2));
            Assert.That(_regions.Count, Is.EqualTo(0));

            _view.gameObject.SetActive(true);
            yield return null;
            Assert.That(_ads.AddCount, Is.EqualTo(3));
            Assert.That(_regions.Count, Is.EqualTo(6));

            UnityEngine.Object.Destroy(_view.gameObject);
            yield return null;
            Assert.That(_ads.RemoveCount, Is.EqualTo(3));
            Assert.That(_regions.Count, Is.EqualTo(0));
            _view = null;
        }

        [UnityTest]
        public IEnumerator ProductionCreateOverload_UsesRewardedAdRuntimeCurrent()
        {
            var service = CreateService(new WardrobeBackend(), () => 1_000L);
            CreateCanvas();
            _ads = new TrackingRewardedAds();
            _ads.SetAvailable(PlacementIds[0], true);
            RewardedAdRuntime.Install(_ads);
            _view = WardrobeScreenView.Create(_canvasHost.transform, service);
            _view.Attach(_regions);

            OpenAtCapturePhone();
            yield return null;

            Assert.That(_regions.IsRegistered(RegionId(PlacementIds[0])), Is.True,
                "production composition consumes the installed optional runtime seam");
        }

        private void CreateView(PurchaseService service)
        {
            CreateCanvas();
            _ads = new TrackingRewardedAds();
            _view = WardrobeScreenView.Create(_canvasHost.transform, service, _ads);
            _view.Attach(_regions);
        }

        private void CreateCanvas()
        {
            _canvasHost = new GameObject("WardrobeRewardedTestCanvas");
            var canvas = _canvasHost.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _regions = new ChromeRegions();
        }

        private void OpenAtCapturePhone()
        {
            _view.Open();
            _view.LayoutForViewport(CaptureSafeArea, CaptureDpi);
            Canvas.ForceUpdateCanvases();
        }

        private RectTransform FindCard(string placementId)
        {
            var card = _view.transform.Find("WardrobePanel/TryOnStrip/TryOnCard_" + placementId)
                as RectTransform;
            Assert.That(card, Is.Not.Null,
                placementId + " must exist as painted Wardrobe geometry");
            return card;
        }

        private void AssertBorrowedState(string placementId, bool expected)
        {
            var card = FindCard(placementId);
            Assert.That(card.Find("BorrowedAccent").gameObject.activeSelf, Is.EqualTo(expected),
                placementId + " accent must mirror PurchaseService.IsUnlocked only");
            Assert.That(card.Find("LockedLabel").gameObject.activeSelf, Is.EqualTo(!expected));
            Assert.That(card.Find("BorrowedLabel").gameObject.activeSelf, Is.EqualTo(expected));
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static void AssertContained(Rect inner, Rect outer, string name)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin), name + " left");
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax), name + " right");
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin), name + " bottom");
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax), name + " top");
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

            public event Action AvailabilityChanged
            {
                add { AddCount++; Changed += value; }
                remove { RemoveCount++; Changed -= value; }
            }

            public void SetAvailable(string placementId, bool value) => _available[placementId] = value;
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
            public BackendAvailability Availability => BackendAvailability.Ready;

            public void SetOwned(params string[] entitlementIds)
            {
                _owned.Clear();
                for (int i = 0; i < entitlementIds.Length; i++)
                    _owned.Add(new EntitlementGrant(entitlementIds[i], GrantSource.Store));
            }

            public void FetchProducts(Action<IReadOnlyList<StoreProductView>> onDone)
                => onDone?.Invoke(Array.Empty<StoreProductView>());

            public void Purchase(string productId, Action<PurchaseResult> onDone)
                => onDone?.Invoke(PurchaseResult.Unavailable(productId, "not used"));

            public void Restore(Action<RestoreResult> onDone)
                => onDone?.Invoke(new RestoreResult(RestoreOutcome.Completed));

            public void RefreshEntitlements(Action<EntitlementSnapshot> onDone)
                => onDone?.Invoke(new EntitlementSnapshot(true, _owned.ToArray()));
        }
    }
}
