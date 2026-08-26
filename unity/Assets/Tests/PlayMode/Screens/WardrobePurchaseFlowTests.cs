using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;
using CatMetro.Services.Purchases;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    public sealed class WardrobePurchaseFlowTests
    {
        private GameObject _canvasHost;
        private GameObject _cameraHost;
        private RenderTexture _captureTarget;
        private WardrobeScreenView _view;
        private ChromeRegions _regions;

        [TearDown]
        public void TearDown()
        {
            if (_canvasHost != null) UnityEngine.Object.Destroy(_canvasHost);
            if (_cameraHost != null) UnityEngine.Object.Destroy(_cameraHost);
            if (_captureTarget != null)
            {
                _captureTarget.Release();
                UnityEngine.Object.Destroy(_captureTarget);
            }
            _canvasHost = null;
            _cameraHost = null;
            _captureTarget = null;
            _view = null;
        }

        [UnityTest]
        public IEnumerator PurchaseTap_PaintsConductorCoat_AndLeavesTheResultOnScreen()
        {
            var backend = new FilmablePurchaseBackend { GrantOnPurchase = true };
            var service = CreateService(backend);
            CreateView(service);

            _view.Open();
            LayoutForFilmablePhone();
            yield return null;

            Assert.That(_view.PanelVisible, Is.True, "precondition: the filmable wardrobe is open");
            Assert.That(_view.ConductorCoatVisible, Is.False,
                "precondition: the plain profile cat is visibly coat-free before purchase");
            Assert.That(_view.BuyLabelText, Does.Contain("$1.99"),
                "the CTA uses the store-formatted price, not an authored currency string");

            Tap(_view.BuyRectPx);
            yield return null;

            Assert.That(backend.PurchaseCalls, Is.EqualTo(1),
                "the painted CTA reaches the purchase backend exactly once");
            Assert.That(service.IsUnlocked(EntitlementIds.OutfitConductor), Is.True,
                "the authoritative CustomerInfo-shaped snapshot owns the entitlement");
            Assert.That(_view.ConductorCoatVisible, Is.True,
                "a recording must show the purchase land: the coat geometry becomes visible");
            Assert.That(_view.PanelVisible, Is.True,
                "the result stays on screen long enough to film; purchase does not dismiss it");
            AssertPaintedCoatIsLargeEnoughToRead(_view.ConductorCoatTransform);
        }

        [UnityTest]
        public IEnumerator RestoreTap_PaintsTheSameConductorCoat_AndStaysVisible()
        {
            var backend = new FilmablePurchaseBackend { GrantOnRestore = true };
            var service = CreateService(backend);
            CreateView(service);

            _view.Open();
            LayoutForFilmablePhone();
            yield return null;
            Assert.That(_view.ConductorCoatVisible, Is.False,
                "precondition: restore begins from the visibly locked cat");

            Tap(_view.RestoreRectPx);
            yield return null;

            Assert.That(backend.RestoreCalls, Is.EqualTo(1));
            Assert.That(service.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);
            Assert.That(_view.ConductorCoatVisible, Is.True,
                "restore and purchase paint the same coat, not parallel cosmetic state");
            Assert.That(_view.PanelVisible, Is.True,
                "the restored result remains filmable on the wardrobe card");
            AssertPaintedCoatIsLargeEnoughToRead(_view.ConductorCoatTransform);
        }

        [UnityTest]
        public IEnumerator RewardedAdGrant_PaintsThroughTheSameEntitlementConsumer()
        {
            var backend = new FilmablePurchaseBackend();
            long now = 1_000L;
            var service = CreateService(backend, () => now);
            CreateView(service);

            _view.Open();
            LayoutForFilmablePhone();
            yield return null;
            Assert.That(_view.ConductorCoatVisible, Is.False);

            Assert.That(service.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor),
                Is.EqualTo(AdGrantOutcome.Granted),
                "TASK 11 binds here; it does not get a second wardrobe API");
            yield return null;

            Assert.That(_view.ConductorCoatVisible, Is.True,
                "the UI consumes IsUnlocked, so paid and ad-granted rewards paint identically");
            AssertPaintedCoatIsLargeEnoughToRead(_view.ConductorCoatTransform);

            now += 3_601L;
            Assert.That(service.PruneExpiredLeases(), Is.True);
            yield return null;

            Assert.That(_view.ConductorCoatVisible, Is.False,
                "an expired ad lease removes the same geometry without a second cosmetic state");
            Assert.That(_view.StatusText, Is.EqualTo(
                CatMetro.Presentation.Strings.UiStrings.Get("wardrobe.status.locked")),
                "the status must not keep claiming the now-hidden coat is equipped");
        }

        [UnityTest]
        public IEnumerator AdLease_LeavesPermanentPurchaseAvailable_AndBuyingUpgradesIt()
        {
            var backend = new FilmablePurchaseBackend { GrantOnPurchase = true };
            long now = 1_000L;
            var service = CreateService(backend, () => now);
            CreateView(service);
            _view.Open();
            LayoutForFilmablePhone();
            yield return null;

            service.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor);
            yield return null;

            Assert.That(_view.ConductorCoatVisible, Is.True,
                "the borrowed coat paints through the shared access path");
            Assert.That(_view.BuyLabelText, Does.Contain("$1.99"),
                "temporary access must not suppress the named permanent purchase");

            Tap(_view.BuyRectPx);
            yield return null;
            Assert.That(backend.PurchaseCalls, Is.EqualTo(1));

            now += 3_601L;
            service.PruneExpiredLeases();
            yield return null;

            Assert.That(_view.ConductorCoatVisible, Is.True,
                "the permanent store grant keeps the coat after the borrowed lease expires");
            Assert.That(service.SecondsUntilExpiry(EntitlementIds.OutfitConductor), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator AdLease_EmptyRestoreSaysNoPurchases_WithoutRemovingTheCoat()
        {
            var backend = new FilmablePurchaseBackend();
            var service = CreateService(backend);
            CreateView(service);
            _view.Open();
            LayoutForFilmablePhone();
            yield return null;
            service.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor);
            yield return null;

            Tap(_view.RestoreRectPx);
            yield return null;

            Assert.That(_view.StatusText, Is.EqualTo(
                CatMetro.Presentation.Strings.UiStrings.Get("wardrobe.status.none")),
                "an ad lease is access, but it is not a restored store purchase");
            Assert.That(_view.ConductorCoatVisible, Is.True,
                "the source-blind wardrobe keeps wearing the independently earned lease");
        }

        [UnityTest]
        public IEnumerator RestoringOnlyAnotherCosmetic_DoesNotClaimTheCoatWasRestored()
        {
            var backend = new FilmablePurchaseBackend { GrantFrameOnRestore = true };
            var service = CreateService(backend);
            CreateView(service);
            _view.Open();
            LayoutForFilmablePhone();
            yield return null;

            Tap(_view.RestoreRectPx);
            yield return null;

            Assert.That(_view.ConductorCoatVisible, Is.False);
            Assert.That(_view.StatusText, Is.EqualTo(
                CatMetro.Presentation.Strings.UiStrings.Get("wardrobe.status.none")),
                "this fixed coat screen must not claim a different restored cosmetic equipped it");
        }

        [UnityTest]
        public IEnumerator NoStore_ShowsAnUnavailableWardrobeWithoutThrowingOrUnlocking()
        {
            var service = new PurchaseService(Catalog(),
                new NullPurchaseBackend(BackendAvailability.NotCompiled, "no store on this build"),
                () => 1_000L);
            CreateView(service);

            Assert.DoesNotThrow(() => _view.Open());
            yield return null;

            Assert.That(_view.PanelVisible, Is.True,
                "monetization failure never takes down or dismisses the game UI");
            Assert.That(_view.ConductorCoatVisible, Is.False);
            Assert.That(_view.BuyLabelText, Is.EqualTo(
                CatMetro.Presentation.Strings.UiStrings.Get("wardrobe.store.unavailable")));

            Assert.DoesNotThrow(() => Tap(_view.BuyRectPx));
            Assert.DoesNotThrow(() => Tap(_view.RestoreRectPx));
            yield return null;
            Assert.That(_view.ConductorCoatVisible, Is.False,
                "an unavailable store never grants a cosmetic locally");
        }

        [UnityTest]
        public IEnumerator CachedPrice_RemainsPurchasableDuringATransientEntitlementFailure()
        {
            var backend = new FilmablePurchaseBackend { GrantOnPurchase = true };
            var service = CreateService(backend);
            service.Refresh();
            backend.Availability = BackendAvailability.Unreachable;
            CreateView(service);

            _view.Open();
            LayoutForFilmablePhone();
            yield return null;
            Assert.That(_view.BuyLabelText, Does.Contain("$1.99"),
                "the cached offering still paints a real purchasable package");

            Tap(_view.BuyRectPx);
            yield return null;

            Assert.That(backend.PurchaseCalls, Is.EqualTo(1),
                "a transient CustomerInfo failure must not turn a priced Buy button into a no-op");
            Assert.That(_view.ConductorCoatVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator EntryAndPanel_RegisterOnlyTheirPaintedTargets_AndCleanUpOnHide()
        {
            CreateView(CreateService(new FilmablePurchaseBackend()));

            _view.ShowEntry();
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(1), "the collapsed wardrobe has one real target");
            Assert.That(HudBands.MeetsMinTargetPx(_view.EntryRectPx, EffectiveDpi()), Is.True);

            int opened = 0;
            _view.OpenRequested = () => opened++;
            Tap(_view.EntryRectPx);
            Assert.That(opened, Is.EqualTo(1));

            _view.Open();
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(3), "back, buy, and restore are the only modal targets");
            Assert.That(HudBands.MeetsMinTargetPx(_view.BuyRectPx, EffectiveDpi()), Is.True);
            Assert.That(HudBands.MeetsMinTargetPx(_view.RestoreRectPx, EffectiveDpi()), Is.True);
            Assert.That(HudBands.MeetsMinTargetPx(_view.BackRectPx, EffectiveDpi()), Is.True);

            _view.Hide();
            yield return null;
            Assert.That(_regions.Count, Is.EqualTo(0), "hidden wardrobe leaves no ghost targets");
        }

        [UnityTest]
        public IEnumerator CaptureEvidence_PurchaseAndRestorePaintPhoneFrames_WhenRequested()
        {
            string dir = Environment.GetEnvironmentVariable("CM_WARDROBE_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Pass("capture rig disarmed — set CM_WARDROBE_CAPTURE_DIR to emit phone frames");
                yield break;
            }

            const int width = 917;
            const int height = 2048;
            var safeArea = new Rect(0f, 64f, width, 1920f);

            _cameraHost = new GameObject("WardrobeCaptureCamera");
            var camera = _cameraHost.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.08f, 0.05f, 1f);
            _captureTarget = new RenderTexture(width, height, 24);
            camera.targetTexture = _captureTarget;

            var purchaseBackend = new FilmablePurchaseBackend { GrantOnPurchase = true };
            CreateView(CreateService(purchaseBackend), camera);
            // A ScreenSpaceCamera canvas must observe the bound RenderTexture for a frame before
            // its phone-space layout is trustworthy.
            yield return null;
            _view.Open();
            _view.LayoutForViewport(safeArea, 408f);
            yield return null;
            Capture(camera, dir, "wardrobe-before.png", width, height);

            Tap(_view.BuyRectPx);
            yield return null;
            Assert.That(_view.ConductorCoatVisible, Is.True);
            Capture(camera, dir, "wardrobe-purchased.png", width, height);

            UnityEngine.Object.Destroy(_canvasHost);
            _canvasHost = null;
            _view = null;
            yield return null;

            var restoreBackend = new FilmablePurchaseBackend { GrantOnRestore = true };
            CreateView(CreateService(restoreBackend), camera);
            _view.Open();
            _view.LayoutForViewport(safeArea, 408f);
            yield return null;
            Tap(_view.RestoreRectPx);
            yield return null;
            Assert.That(_view.ConductorCoatVisible, Is.True);
            Capture(camera, dir, "wardrobe-restored.png", width, height);

            Assert.That(new FileInfo(Path.Combine(dir, "wardrobe-purchased.png")).Length,
                Is.GreaterThan(10_000), "the evidence is a rendered phone frame, not an empty file");
            Assert.That(new FileInfo(Path.Combine(dir, "wardrobe-restored.png")).Length,
                Is.GreaterThan(10_000));
        }

        private void CreateView(PurchaseService service, Camera camera = null)
        {
            _canvasHost = new GameObject("WardrobeTestCanvas");
            var canvas = _canvasHost.AddComponent<Canvas>();
            canvas.renderMode = camera == null
                ? RenderMode.ScreenSpaceOverlay
                : RenderMode.ScreenSpaceCamera;
            if (camera != null)
            {
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }
            _regions = new ChromeRegions();
            _view = WardrobeScreenView.Create(canvas.transform, service);
            _view.Attach(_regions);
        }

        private void Capture(Camera camera, string dir, string name, int width, int height)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = _captureTarget;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;

            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name), texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
        }

        private void Tap(Rect rect)
        {
            Assert.That(rect.width, Is.GreaterThan(0f), "the target has been laid out, not defaulted");
            Assert.That(_regions.TryResolve(rect.center, out var action), Is.True,
                "the center of the painted target resolves through ChromeRegions");
            action();
        }

        private void LayoutForFilmablePhone()
            => _view.LayoutForViewport(new Rect(0f, 0f, 1179f, 2556f), 160f);

        private static void AssertPaintedCoatIsLargeEnoughToRead(RectTransform coat)
        {
            Assert.That(coat, Is.Not.Null, "the read-back points at real coat geometry");
            Assert.That(coat.gameObject.activeInHierarchy, Is.True);
            var corners = new Vector3[4];
            coat.GetWorldCorners(corners);
            float width = corners[2].x - corners[0].x;
            float height = corners[2].y - corners[0].y;
            Assert.That(width, Is.GreaterThanOrEqualTo(96f),
                "the coat must read in a phone recording, not be a tiny status badge");
            Assert.That(height, Is.GreaterThanOrEqualTo(72f));
        }

        private static float EffectiveDpi() => Screen.dpi > 0f ? Screen.dpi : 160f;

        private static PurchaseService CreateService(FilmablePurchaseBackend backend,
            Func<long> clock = null)
            => new PurchaseService(Catalog(), backend, clock ?? (() => 1_000L));

        private static PurchaseCatalog Catalog() => PurchaseCatalog.Parse(@"{
          'entitlements': [
            { 'id': 'outfit_conductor', 'kind': 'outfit', 'display': 'Conductor Coat',
              'adLeaseSeconds': 3600 },
            { 'id': 'frame_brass', 'kind': 'frame', 'display': 'Brass Frame',
              'adLeaseSeconds': 0 }
          ],
          'products': [
            { 'id': 'cm_outfit_conductor', 'storeType': 'non_consumable',
              'display': 'Conductor Coat', 'entitlements': ['outfit_conductor'] }
          ]
        }");

        private sealed class FilmablePurchaseBackend : IPurchaseBackend
        {
            public bool GrantOnPurchase;
            public bool GrantOnRestore;
            public bool GrantFrameOnRestore;
            public int PurchaseCalls;
            public int RestoreCalls;

            private bool _entitled;
            private bool _frameEntitled;

            public BackendAvailability Availability { get; set; } = BackendAvailability.Ready;

            public void FetchProducts(Action<IReadOnlyList<StoreProductView>> onDone)
                => onDone?.Invoke(new[]
                {
                    new StoreProductView(ProductIds.Gate, "Conductor Coat", new LocalizedPrice("$1.99"))
                });

            public void Purchase(string productId, Action<PurchaseResult> onDone)
            {
                PurchaseCalls++;
                if (GrantOnPurchase) _entitled = true;
                onDone?.Invoke(new PurchaseResult(PurchaseOutcome.SuccessCandidate, productId));
            }

            public void Restore(Action<RestoreResult> onDone)
            {
                RestoreCalls++;
                if (GrantOnRestore) _entitled = true;
                if (GrantFrameOnRestore) _frameEntitled = true;
                onDone?.Invoke(new RestoreResult(RestoreOutcome.Completed));
            }

            public void RefreshEntitlements(Action<EntitlementSnapshot> onDone)
            {
                var grants = new List<EntitlementGrant>();
                if (_entitled)
                    grants.Add(new EntitlementGrant(EntitlementIds.OutfitConductor, GrantSource.Store));
                if (_frameEntitled)
                    grants.Add(new EntitlementGrant(EntitlementIds.FrameBrass, GrantSource.Store));
                onDone?.Invoke(new EntitlementSnapshot(true, grants));
            }
        }
    }
}
