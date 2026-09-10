using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using CatMetro.Bootstrap;
using CatMetro.Presentation.Cats;
using CatMetro.Presentation.Cosmetics;
using CatMetro.Presentation.Hud.WavePreview;
using CatMetro.Presentation.Screens;
using CatMetro.Presentation.Theme;
using CatMetro.Services;
using CatMetro.Services.Cosmetics;
using CatMetro.Services.Purchases;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.PlayMode
{
    // Opt-in visual evidence for LOOK step 7. The armed rig insists on the reference phone
    // frame so a convenient landscape Game view cannot silently stand in for the shipped UI.
    public sealed class UiPhoneCaptureTests
    {
        private const int CaptureWidth = 917;
        private const int CaptureHeight = 2048;
        private const float CaptureDpi = 408f;
        private static readonly Rect CaptureSafeArea = new Rect(0f, 64f, 917f, 1920f);
        private const int HomeRigCaptureWidth = 1536;
        private const int HomeRigCaptureHeight = 2752;
        private const float HomeRigCaptureDpi = 683.4111f;
        private static readonly Rect HomeRigCaptureSafeArea =
            new Rect(0f, 86f, 1536f, 2580f);
        private GameRoot _root;
        private CaptureStorageRoot _captureStorage;

        [TearDown]
        public void TearDown()
        {
            GameRoot.DevSkipShippedHome = false;
            GameRoot.DailyEntryUnlocked = false;
            GameRoot.DailyStorageRootOverride = null;
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
            _root = null;
            PurchaseRuntime.ResetForTests();
            CosmeticRuntime.ResetForTests();
            _captureStorage?.Dispose();
            _captureStorage = null;
        }

        [UnityTest]
        public IEnumerator CaptureEvidence_ShippedHome_917x2048_WhenRequested()
        {
            var dir = System.Environment.GetEnvironmentVariable("CM_UI_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Pass("capture rig disarmed — set CM_UI_CAPTURE_DIR to emit phone frames");
                yield break;
            }

            _captureStorage = new CaptureStorageRoot();
            GameRoot.DailyStorageRootOverride = () => _captureStorage;
            GameRoot.DevSkipShippedHome = false;
            _root = GameRoot.Launch();
            _root.MotionOffToggle = true; // still captures settle the cold-boot reveal on the next frame
            yield return null;
            yield return null;
            Assert.That(_root.Home, Is.Not.Null);
            Assert.That(_root.Home.IsVisible, Is.True);
            yield return Capture(dir, "step-7-home.png", _root.Home, _root.Wardrobe);
        }

        [UnityTest]
        public IEnumerator CaptureEvidence_SettingsOverShippedHome_RigOcclusionAndClose_917x2048_WhenRequested()
        {
            string dir = Environment.GetEnvironmentVariable("CM_UI_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Pass("combined Home/settings capture disarmed");
                yield break;
            }

            // This evidence requires the real locally admitted art even when another capture
            // has explicitly allowed placeholders. Never substitute an isolated UI fixture.
            CaptureRig.RequireStoreCaptureArt(null);
            Assert.That(CatModelCatalog.LoadResources().AdmittedEntryCount, Is.EqualTo(1),
                "run the combined capture in the licensed main checkout");
            PurchaseRuntime.ResetForTests();
            CosmeticRuntime.ResetForTests();
            _captureStorage = new CaptureStorageRoot();
            GameRoot.DailyStorageRootOverride = () => _captureStorage;
            GameRoot.DevSkipShippedHome = false;
            GameRoot.DailyEntryUnlocked = false;
            _root = GameRoot.Launch();
            _root.MotionOffToggle = true;
            yield return null;
            yield return null; // Let GameRoot clear the real boot cover through Update.

            Camera camera = _root.Cam;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            float previousAspect = camera.aspect;
            var target = CaptureRig.CreateTarget(new CaptureRig.Size(CaptureWidth, CaptureHeight));
            try
            {
                camera.targetTexture = target;
                camera.aspect = CaptureWidth / (float)CaptureHeight;
                yield return null;
                LayoutSettingsHomeCapture();
                yield return null;
                LayoutSettingsHomeCapture();

                Assert.That(_root.enabled, Is.True, "the real GameRoot update loop remains active");
                Assert.That(_root.Stack.Current, Is.EqualTo("home"));
                Assert.That(_root.Home.IsVisible, Is.True);
                Assert.That(_root.Settings.IsVisible, Is.False);
                Assert.That(_root.Input.Regions.HasStackedModal, Is.False);
                Assert.That(_root.Session.State.Tick, Is.Zero);
                HomeProfileRigView rig = _root.Home.ProfileRig;
                Assert.That(rig, Is.Not.Null);
                Assert.That(rig.CatalogAdmittedEntryCount, Is.EqualTo(1));
                Assert.That(rig.Mounted, Is.True, rig.FallbackReason);
                Transform instance = rig.PrefabRoot;
                Assert.That(instance, Is.Not.Null);
                Assert.That(rig.RenderedHeadScreenRect.width, Is.GreaterThan(24f));
                Assert.That(rig.RenderedHeadScreenRect.height, Is.GreaterThan(24f));
                var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.That(skins, Is.Not.Empty);
                Assert.That(skins.Any(skin => skin.enabled), Is.True);
                Assert.That(CaptureSettingsFlowRigPixels(rig, camera, target, dir,
                    "settings-flow-home-before-917x2048.png"), Is.GreaterThan(100),
                    "positive control: the admitted rig must visibly paint on Home");

                // Follow the actual Home speaker region: Home.AudioEnabledChanged ->
                // GameRoot.ShowSettings -> Settings.Show/Register -> StackedModalChanged.
                bool soundBefore = _root.Audio.Enabled;
                Assert.That(_root.Input.Regions.IsRegistered("home.audio.toggle"), Is.True);
                _root.Input.HandleTapAtScreen(_root.Home.AudioToggleRectPx.center);
                Assert.That(_root.Settings.IsVisible, Is.True);
                Assert.That(_root.Stack.Current, Is.EqualTo("settings"));
                Assert.That(_root.Home.IsVisible, Is.True, "Settings overlays the retained Home");
                Assert.That(_root.Audio.Enabled, Is.EqualTo(soundBefore),
                    "opening settings must not retain the old speaker-toggle action");
                Assert.That(_root.Input.Regions.HasStackedModal, Is.True);
                Assert.That(skins.All(skin => !skin.enabled), Is.True,
                    "registering the real Settings modal must hide the lifted rig synchronously");
                LayoutSettingsHomeCapture();
                Assert.That(_root.Settings.CardRectPx.Overlaps(rig.RenderedHeadScreenRect), Is.True,
                    "the composed Settings card must overlap the actual Home head");
                Assert.That(CaptureSettingsFlowRigPixels(rig, camera, target, dir,
                    "settings-flow-open-first-frame-917x2048.png"), Is.Zero,
                    "no rig pixels may leak through the first Settings render");

                yield return null;
                yield return null;
                LayoutSettingsHomeCapture();
                Assert.That(rig.Mounted, Is.True, "covering the cat retains its fitted mount");
                Assert.That(rig.PrefabRoot, Is.SameAs(instance));
                Assert.That(CaptureSettingsFlowRigPixels(rig, camera, target, dir,
                    "settings-flow-open-settled-917x2048.png"), Is.Zero,
                    "the retained Home rig must also stay hidden after normal Updates");

                // Follow Settings' actual Close hit region and GameRoot.CloseSettings.
                Assert.That(_root.Input.Regions.IsRegistered("settings.close"), Is.True);
                _root.Input.HandleTapAtScreen(_root.Settings.CloseRectPx.center);
                Assert.That(_root.Settings.IsVisible, Is.False);
                Assert.That(_root.Input.Regions.HasStackedModal, Is.False);
                Assert.That(_root.Stack.Current, Is.EqualTo("home"));
                Assert.That(skins.Any(skin => skin.enabled), Is.True,
                    "unregistering the last modal must restore renderer visibility");
                yield return null;
                yield return null;
                LayoutSettingsHomeCapture();
                Assert.That(_root.Home.IsVisible, Is.True);
                Assert.That(_root.Session.State.Tick, Is.Zero);
                Assert.That(rig.Mounted, Is.True);
                Assert.That(rig.PrefabRoot, Is.SameAs(instance),
                    "Close restores the same licensed rig instance");
                Assert.That(CaptureSettingsFlowRigPixels(rig, camera, target, dir,
                    "settings-flow-home-after-close-917x2048.png"), Is.GreaterThan(100),
                    "positive control: the same cat must visibly return after Close");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                RenderTexture.active = previousActive;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private void LayoutSettingsHomeCapture()
        {
            // The target is bound before layout. Settings.Update reads the editor's Screen
            // dimensions, so reapply each real view's viewport seam after any yielded frames.
            _root.Home.LayoutForViewport(CaptureSafeArea, CaptureDpi,
                new Rect(0f, 0f, CaptureWidth, CaptureHeight));
            _root.Wardrobe.LayoutForViewport(CaptureSafeArea, CaptureDpi);
            if (_root.Settings.IsVisible)
                _root.Settings.LayoutForViewport(CaptureSafeArea, CaptureDpi);
            Canvas.ForceUpdateCanvases();
        }

        private int CaptureSettingsFlowRigPixels(HomeProfileRigView rig, Camera camera,
            RenderTexture target, string dir, string fileName)
        {
            Color32[] actual = ReadSettingsFlowFrame(camera, target, dir, fileName);
            bool wasActive = rig.PrefabRoot.gameObject.activeSelf;
            Color32[] withoutRig;
            rig.PrefabRoot.gameObject.SetActive(false);
            try { withoutRig = ReadSettingsFlowFrame(camera, target, null, null); }
            finally { rig.PrefabRoot.gameObject.SetActive(wasActive); }
            int changed = 0;
            for (int i = 0; i < actual.Length; i++)
                if (Math.Abs(actual[i].r - withoutRig[i].r) > 4
                    || Math.Abs(actual[i].g - withoutRig[i].g) > 4
                    || Math.Abs(actual[i].b - withoutRig[i].b) > 4) changed++;
            RectInt head = InsetAndClamp(rig.RenderedHeadScreenRect, .1f, .1f,
                target.width, target.height);
            float headFraction = ChangedFraction(actual, withoutRig, head, target.width, 4);
            if (_root.Stack.Current == "home")
                Assert.That(headFraction, Is.GreaterThan(.50f),
                    "the visible positive control must paint its head, not only a scene shadow");
            TestContext.Out.WriteLine("SETTINGS_HOME_CAPTURE file=" + fileName
                + " admitted=" + rig.CatalogAdmittedEntryCount + " mounted=" + rig.Mounted
                + " instance=" + rig.PrefabRoot.GetInstanceID()
                + " stack=" + _root.Stack.Current + " rigPixels=" + changed
                + " headFraction=" + headFraction.ToString("F6", CultureInfo.InvariantCulture)
                + " target=" + target.width + "x" + target.height);
            return changed;
        }

        private static Color32[] ReadSettingsFlowFrame(Camera camera, RenderTexture target,
            string dir, string fileName)
        {
            RenderTexture previous = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture = CaptureRig.ReadRgb24(target);
                Color32[] pixels = texture.GetPixels32();
                if (fileName != null)
                {
                    Directory.CreateDirectory(dir);
                    File.WriteAllBytes(Path.Combine(dir, fileName),
                        CaptureRig.EncodeOpaqueSrgbPng(texture));
                }
                return pixels;
            }
            finally
            {
                RenderTexture.active = previous;
                if (texture != null) Object.DestroyImmediate(texture);
            }
        }


        [UnityTest]
        public IEnumerator CaptureEvidence_WardrobeRig_917x2048_WhenRequested()
        {
            string dir = Environment.GetEnvironmentVariable("CM_UI_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Pass("capture rig disarmed — set CM_UI_CAPTURE_DIR to emit phone frames");
                yield break;
            }
            PurchaseRuntime.ResetForTests();
            CosmeticRuntime.ResetForTests();
            _captureStorage = new CaptureStorageRoot();
            GameRoot.DailyStorageRootOverride = () => _captureStorage;
            var ledger = new EntitlementLedger();
            ledger.ReplaceStoreGrants(new[]
            {
                new EntitlementGrant(EntitlementIds.OutfitConductor, GrantSource.Store),
                new EntitlementGrant(EntitlementIds.FrameBrass, GrantSource.Store),
                new EntitlementGrant(EntitlementIds.FrameLantern, GrantSource.Store),
            });
            PurchaseRuntime.Install(new PurchaseService(PurchaseCatalog.Parse(
                Resources.Load<TextAsset>("Monetization/product_catalog").text),
                new NullPurchaseBackend(), () => 1_700_000_000L, ledger));
            _root = GameRoot.Launch();
            yield return null;
            _root.Wardrobe.OpenRequested.Invoke();
            yield return null;
            Camera camera = _root.Cam;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            float previousAspect = camera.aspect;
            var target = new RenderTexture(CaptureWidth, CaptureHeight, 24,
                RenderTextureFormat.ARGB32);
            target.Create();
            try
            {
                camera.targetTexture = target;
                camera.aspect = CaptureWidth / (float)CaptureHeight;
                yield return null; // A camera-space canvas adopts its target on the next frame.
                ApplyLayout(_root.Wardrobe, CaptureSafeArea, CaptureDpi,
                    CaptureWidth, CaptureHeight);
                Canvas.ForceUpdateCanvases();
                ProfileRigMount rig = _root.Wardrobe.ProfileRig;
                Assert.That(rig.CatalogAdmittedEntryCount, Is.EqualTo(1),
                    "armed Wardrobe evidence requires the licensed resource in the main checkout");
                Assert.That(rig.Mounted, Is.True, rig.FallbackReason);
                Assert.That(_root.Wardrobe.PrimaryActionText, Is.EqualTo("Equip"),
                    "the first card's action is painted without tapping a card");
                var primary = _root.Wardrobe.GetComponentsInChildren<RectTransform>(true)
                    .Single(rect => rect.name == "PrimaryActionChip");
                Assert.That(primary.gameObject.activeInHierarchy, Is.True);
                Assert.That(_root.Wardrobe.GetComponentsInChildren<RectTransform>(true)
                    .Single(rect => rect.name == "Tab-accessory").gameObject.activeSelf, Is.False);
                Assert.That(_root.Wardrobe.LargePortrait.BaseLayerTransform.gameObject.activeSelf,
                    Is.False);
                rig.TurntableAmplitude = 0f;
                rig.Layout(camera);
                var skins = rig.PrefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var composed = ReadFrame(camera, target);
                Color32[] withoutRig;
                foreach (var skin in skins) skin.enabled = false;
                try { withoutRig = ReadFrame(camera, target); }
                finally { foreach (var skin in skins) skin.enabled = true; }
                var head = InsetAndClamp(rig.RenderedHeadScreenRect, 0.1f, 0.1f,
                    CaptureWidth, CaptureHeight);
                Assert.That(ChangedFraction(composed, withoutRig, head, CaptureWidth, 4),
                    Is.GreaterThan(0.1f), "the mounted rig must contribute visible head pixels");
                CaptureBound(camera, target, dir, "wardrobe-rig-917x2048.png");

                var frameTab = _root.Wardrobe.GetComponentsInChildren<RectTransform>(true)
                    .Single(rect => rect.name == "Tab-frame");
                _root.Input.HandleTapAtScreen(ProjectedScreenRect(frameTab, camera).center);
                yield return null;
                ApplyLayout(_root.Wardrobe, CaptureSafeArea, CaptureDpi,
                    CaptureWidth, CaptureHeight);
                Assert.That(_root.Wardrobe.VisibleCards.Count, Is.EqualTo(2));
                var cards = _root.Wardrobe.VisibleCards.OrderBy(card => card.ScreenRect.x).ToArray();
                float span = cards.Last().ScreenRect.xMax - cards.First().ScreenRect.xMin;
                Assert.That(span / _root.Wardrobe.ItemsRectPx.width, Is.GreaterThanOrEqualTo(0.9f));
                CaptureBound(camera, target, dir, "wardrobe-frames-917x2048.png");

                var blue = _root.Wardrobe.GetComponentsInChildren<RectTransform>(true)
                    .Single(rect => rect.name == "CatSelector-blue_siamese");
                _root.Input.HandleTapAtScreen(ProjectedScreenRect(blue, camera).center);
                yield return null;
                ApplyLayout(_root.Wardrobe, CaptureSafeArea, CaptureDpi,
                    CaptureWidth, CaptureHeight);
                Assert.That(rig.Mounted, Is.False);
                Assert.That(_root.Wardrobe.LargePortrait.BaseLayerTransform.gameObject.activeSelf,
                    Is.True);
                CaptureBound(camera, target, dir, "wardrobe-blue-fallback-917x2048.png");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                RenderTexture.active = previousActive;
                target.Release();
                Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator CaptureEvidence_ShippedHomeRig_1536x2752_WhenRequested()
        {
            var dir = Environment.GetEnvironmentVariable("CM_UI_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Pass("capture rig disarmed — set CM_UI_CAPTURE_DIR to emit phone frames");
                yield break;
            }

            CaptureRig.RequireStoreCaptureArt(
                Environment.GetEnvironmentVariable("CM_CAPTURE_ALLOW_PLACEHOLDER"));

            PurchaseRuntime.ResetForTests();
            CosmeticRuntime.ResetForTests();
            _captureStorage = new CaptureStorageRoot();
            GameRoot.DailyStorageRootOverride = () => _captureStorage;

            var productAsset = Resources.Load<TextAsset>("Monetization/product_catalog");
            Assert.That(productAsset, Is.Not.Null);
            var ledger = new EntitlementLedger();
            ledger.ReplaceStoreGrants(new[]
            {
                new EntitlementGrant(EntitlementIds.OutfitConductor, GrantSource.Store),
                new EntitlementGrant(EntitlementIds.FrameBrass, GrantSource.Store),
            });
            PurchaseRuntime.Install(new PurchaseService(
                PurchaseCatalog.Parse(productAsset.text), new NullPurchaseBackend(),
                () => 1_700_000_000L, ledger));

            GameRoot.DevSkipShippedHome = false;
            _root = GameRoot.Launch();
            _root.MotionOffToggle = true; // still captures settle the cold-boot reveal on the next frame
            yield return null;
            yield return null;

            CosmeticProfileService profile = CosmeticRuntime.Current;
            Assert.That(profile.SelectedCatId, Is.EqualTo("red_tabby"));
            Assert.That(profile.TryEquip("red_tabby", CosmeticSlot.Outfit,
                "outfit_conductor"), Is.True);
            Assert.That(profile.TryEquip("red_tabby", CosmeticSlot.Frame,
                "frame_brass"), Is.True);
            yield return null;

            Camera camera = _root.Cam;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            float previousAspect = camera.aspect;
            var target = new RenderTexture(HomeRigCaptureWidth, HomeRigCaptureHeight, 24,
                RenderTextureFormat.ARGB32);
            target.Create();

            try
            {
                // Head bounds are projected in screen pixels, so the exact output target must
                // already be bound before Home solves the 2D cosmetic layers around the rig.
                camera.targetTexture = target;
                camera.aspect = HomeRigCaptureWidth / (float)HomeRigCaptureHeight;
                ApplyLayout(_root.Home, HomeRigCaptureSafeArea, HomeRigCaptureDpi,
                    HomeRigCaptureWidth, HomeRigCaptureHeight);
                ApplyLayout(_root.Wardrobe, HomeRigCaptureSafeArea, HomeRigCaptureDpi,
                    HomeRigCaptureWidth, HomeRigCaptureHeight);
                Canvas.ForceUpdateCanvases();
                yield return null;
                ApplyLayout(_root.Home, HomeRigCaptureSafeArea, HomeRigCaptureDpi,
                    HomeRigCaptureWidth, HomeRigCaptureHeight);
                Canvas.ForceUpdateCanvases();

                Assert.That(_root.Session.State.Tick, Is.Zero,
                    "the evidence frame must be the already-loaded tick-0 board");
                Assert.That(_root.Home.IsVisible, Is.True);
                HomeProfileRigView rig = _root.Home.ProfileRig;
                Assert.That(rig, Is.Not.Null,
                    "the local licensed resource must pass admission for this armed capture");
                Assert.That(rig.CatalogAdmittedEntryCount, Is.EqualTo(1));
                Assert.That(rig.Mounted, Is.True);
                Assert.That(rig.PrefabRoot, Is.Not.Null);
                Assert.That(rig.AnimatorCount, Is.Zero);
                Assert.That(rig.PrefabRoot.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(rig.PrefabRoot.GetComponentsInChildren<Collider2D>(true), Is.Empty);
                Assert.That(rig.SkinnedMeshRendererCount, Is.GreaterThan(0));

                SkinnedMeshRenderer[] skins = rig.PrefabRoot
                    .GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.That(skins.Any(skin => skin.enabled && skin.sharedMesh != null
                    && skin.sharedMaterial != null), Is.True,
                    "at least one admitted skin must be enabled and materially renderable");
                Assert.That(rig.SampledPose, Is.EqualTo(CatModelCatalog.IdleSitClip));
                Assert.That(rig.AppliedFacingYaw,
                    Is.EqualTo(CatModelCatalog.ResourceFacingYaw
                        + HomeProfileRigView.HomeFacingYaw).Within(0.001f));
                Assert.That(rig.PrefabRoot.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(rig.PrefabRoot.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(rig.PrefabRoot.localScale, Is.EqualTo(Vector3.one));
                Assert.That(rig.transform.parent.name, Is.EqualTo("ParkedDistrictB"));

                CosmeticPortraitView portrait = _root.Home.ProfilePortrait;
                Assert.That(portrait.AppliedCatId, Is.EqualTo("red_tabby"));
                Assert.That(portrait.AppliedOutfitAssetId,
                    Is.EqualTo("outfit.conductor"));
                Assert.That(portrait.AppliedFrameAssetId, Is.EqualTo("frame.brass"));
                Assert.That(portrait.BaseLayerTransform.gameObject.activeSelf, Is.False);
                Assert.That(HasVisiblePaint(portrait.OutfitLayerTransform), Is.True,
                    "the equipped conductor coat must contribute active paint over the rig");
                Assert.That(HasVisiblePaint(portrait.FrameLayerTransform), Is.True,
                    "the equipped brass frame must contribute active paint around the rig");

                RectInt headPatch = InsetAndClamp(rig.RenderedHeadScreenRect,
                    0.10f, 0.10f, HomeRigCaptureWidth, HomeRigCaptureHeight);
                Assert.That(headPatch.width, Is.GreaterThanOrEqualTo(24));
                Assert.That(headPatch.height, Is.GreaterThanOrEqualTo(24));
                Color32[] composed = ReadFrame(camera, target);
                Color32[] withoutRig;
                bool[] enabledStates = skins.Select(skin => skin.enabled).ToArray();
                try
                {
                    foreach (SkinnedMeshRenderer skin in skins) skin.enabled = false;
                    withoutRig = ReadFrame(camera, target);
                }
                finally
                {
                    for (int i = 0; i < skins.Length; i++)
                        skins[i].enabled = enabledStates[i];
                }

                float rigPixelDelta = MeanRgbDelta(composed, withoutRig,
                    headPatch, HomeRigCaptureWidth);
                float rigChangedFraction = ChangedFraction(composed, withoutRig,
                    headPatch, HomeRigCaptureWidth, minimumPerChannelDelta: 4);
                Assert.That(rigPixelDelta, Is.GreaterThan(4f / 255f),
                    "the admitted skin must contribute visible pixels inside its head bounds");
                Assert.That(rigChangedFraction, Is.GreaterThan(0.50f),
                    "the rendered cat must fill most of its head guide; inflated bounds hide the face under cosmetics");

                RectInt cosmeticPatch = InsetAndClamp(
                    ProjectedScreenRect(portrait.RootTransform, camera),
                    0f, 0f, HomeRigCaptureWidth, HomeRigCaptureHeight);
                Color32[] withoutCosmetics;
                portrait.OutfitLayerTransform.gameObject.SetActive(false);
                portrait.FrameLayerTransform.gameObject.SetActive(false);
                try
                {
                    withoutCosmetics = ReadFrame(camera, target);
                }
                finally
                {
                    portrait.OutfitLayerTransform.gameObject.SetActive(true);
                    portrait.FrameLayerTransform.gameObject.SetActive(true);
                }
                float cosmeticChangedFraction = ChangedFraction(composed,
                    withoutCosmetics, cosmeticPatch, HomeRigCaptureWidth,
                    minimumPerChannelDelta: 4);
                Assert.That(cosmeticChangedFraction, Is.GreaterThan(0.04f),
                    "the equipped coat/frame must contribute visible holder pixels");

                Color32[] restored = ReadFrame(camera, target);
                Color32[] stable = ReadFrame(camera, target);
                float stablePixelDelta = MeanRgbDelta(restored, stable,
                    headPatch, HomeRigCaptureWidth);
                Assert.That(stablePixelDelta, Is.LessThanOrEqualTo(1f / 255f),
                    "the frozen Home skin must be stable across rendered frames, not z-fight");

                string outputPath = Path.Combine(dir,
                    "home-rig-holder-1536x2752.png");
                TestContext.Out.WriteLine("HOME_RIG_HOLDER_READBACK"
                    + " Tick=" + _root.Session.State.Tick
                    + " Size=1536x2752"
                    + " Parent=" + rig.transform.parent.name
                    + " AdmittedEntryCount=" + rig.CatalogAdmittedEntryCount
                    + " Mounted=" + rig.Mounted
                    + " Root=" + rig.PrefabRoot.name
                    + " Animator=" + rig.AnimatorCount
                    + " Collider=" + rig.PrefabRoot
                        .GetComponentsInChildren<Collider>(true).Length
                    + " Collider2D=" + rig.PrefabRoot
                        .GetComponentsInChildren<Collider2D>(true).Length
                    + " SkinnedMeshRenderer=" + rig.SkinnedMeshRendererCount
                    + " Pose=" + rig.SampledPose
                    + " Yaw=" + rig.AppliedFacingYaw.ToString(
                        "F3", CultureInfo.InvariantCulture)
                    + " EquippedCat=" + portrait.AppliedCatId
                    + " Outfit=" + portrait.AppliedOutfitAssetId
                    + " Frame=" + portrait.AppliedFrameAssetId
                    + " BaseLayerVisible="
                        + portrait.BaseLayerTransform.gameObject.activeSelf
                    + " OutfitLayerVisible="
                        + portrait.OutfitLayerTransform.gameObject.activeInHierarchy
                    + " FrameLayerVisible="
                        + portrait.FrameLayerTransform.gameObject.activeInHierarchy
                    + " RigPixelDelta=" + rigPixelDelta.ToString(
                        "F6", CultureInfo.InvariantCulture)
                    + " RigChangedFraction=" + rigChangedFraction.ToString(
                        "F6", CultureInfo.InvariantCulture)
                    + " CosmeticChangedFraction=" + cosmeticChangedFraction.ToString(
                        "F6", CultureInfo.InvariantCulture)
                    + " StablePixelDelta=" + stablePixelDelta.ToString(
                        "F6", CultureInfo.InvariantCulture)
                    + " Capture=" + outputPath);
                CaptureBound(camera, target, dir, Path.GetFileName(outputPath));
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                RenderTexture.active = previousActive;
                target.Release();
                Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator CaptureEvidence_UnlockedDailyHome_917x2048_WhenRequested()
        {
            var dir = System.Environment.GetEnvironmentVariable("CM_UI_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Pass("capture rig disarmed — set CM_UI_CAPTURE_DIR to emit phone frames");
                yield break;
            }

            _captureStorage = new CaptureStorageRoot();
            GameRoot.DailyStorageRootOverride = () => _captureStorage;
            GameRoot.DevSkipShippedHome = false;
            GameRoot.DailyEntryUnlocked = true;
            _root = GameRoot.Launch();
            _root.MotionOffToggle = true; // still captures settle the cold-boot reveal on the next frame
            yield return null;
            yield return null;
            Assert.That(_root.Home, Is.Not.Null);
            Assert.That(_root.Home.IsVisible, Is.True);
            Assert.That(_root.Home.DailyPinTransform, Is.Not.Null);
            yield return Capture(dir, "step-7-home-daily.png", _root.Home, _root.Wardrobe);
        }

        [UnityTest]
        public IEnumerator ShippedHome_WardrobeEntry_UsesSharedChromeWithItsLivePortraitInTheIconSlot()
        {
            GameRoot.DevSkipShippedHome = false;
            _root = GameRoot.Launch();
            _root.MotionOffToggle = true; // still captures settle the cold-boot reveal on the next frame
            yield return null;
            yield return null;

            Assert.That(_root.Wardrobe, Is.Not.Null);
            Assert.That(_root.Wardrobe.EntryVisible, Is.True);
            ApplyPhoneLayout(_root.Home);
            ApplyPhoneLayout(_root.Wardrobe);
            Canvas.ForceUpdateCanvases();

            var capsule = FindRequiredRect(_root.Wardrobe.transform, "WardrobeCapsule");
            Assert.That(capsule.parent, Is.SameAs(_root.Wardrobe.transform),
                "WardrobeCapsule remains the entry root under WardrobeSurface");
            Assert.That(capsule.GetComponent<Image>(), Is.Null, "ChromeChip's root owns geometry only");
            var shadow = capsule.Find("Shadow").GetComponent<Image>();
            Assert.That(shadow, Is.Not.Null);
            Assert.That(shadow.color, Is.EqualTo(Palette.WithAlpha(Palette.DepotNavy, .24f)));
            Assert.That(shadow.sprite, Is.SameAs(HudShapeSprites.SoftRoundedHalo));
            Assert.That(shadow.type, Is.EqualTo(Image.Type.Sliced));

            var face = FindRequiredRect(capsule, "WardrobeButtonFace");
            var faceImage = face.GetComponent<Image>();
            Assert.That(faceImage, Is.Not.Null);
            Assert.That(faceImage.color, Is.EqualTo(Palette.CreamCard));
            Assert.That(faceImage.sprite, Is.SameAs(HudShapeSprites.RoundedSquare));
            Assert.That(faceImage.type, Is.EqualTo(Image.Type.Sliced));

            var label = FindRequiredRect(capsule, "WardrobeLabel");
            var content = capsule.Find("Content");
            Assert.That(label.parent, Is.SameAs(content));
            Assert.That(label.GetComponent<TMPro.TMP_Text>().color,
                Is.EqualTo(Palette.InkNavy));

            var portraitMount = content.Find("Icon") as RectTransform;
            Assert.That(portraitMount, Is.Not.Null, "the shared icon slot hosts the selected cat");
            Assert.That(portraitMount.rect.width, Is.EqualTo(26f * 2.55f).Within(.01f));
            Assert.That(portraitMount.rect.height, Is.EqualTo(26f * 2.55f).Within(.01f));
            Assert.That(portraitMount.GetComponent<Image>().enabled, Is.False,
                "only the live portrait paints in the icon slot");
            Assert.That(face.GetSiblingIndex(), Is.LessThan(content.GetSiblingIndex()),
                "the cream face paints behind the measured portrait and label group");

            CosmeticPortraitView entryPortrait = _root.Wardrobe.EntryPortrait;
            Assert.That(entryPortrait, Is.Not.Null);
            Assert.That(entryPortrait.name, Is.EqualTo("EntryPortrait"));
            Assert.That(entryPortrait.transform.parent, Is.SameAs(portraitMount));
            var mountedPortraits = portraitMount.GetComponentsInChildren<CosmeticPortraitView>(
                true);
            Assert.That(mountedPortraits.Length, Is.EqualTo(1));
            Assert.That(mountedPortraits[0], Is.SameAs(entryPortrait),
                "the public Wardrobe portrait remains the view mounted beneath the seam");
        }

        [UnityTest]
        public IEnumerator CaptureEvidence_FailureBanner_917x2048_WhenRequested()
        {
            var dir = System.Environment.GetEnvironmentVariable("CM_UI_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Pass("capture rig disarmed — set CM_UI_CAPTURE_DIR to emit phone frames");
                yield break;
            }

            GameRoot.DevSkipShippedHome = false;
            _root = GameRoot.Launch();
            _root.MotionOffToggle = true; // still captures settle the cold-boot reveal on the next frame
            yield return null;
            _root.Home.Hide();
            _root.Banner.ShowKey("fail.banner.timeout");
            yield return null;
            yield return null;
            Assert.That(_root.Banner.CurrentText, Is.EqualTo("The last train left the depot"));
            Assert.That(_root.Banner.Visible, Is.True);
            yield return Capture(dir, "step-7-failure.png", _root.Banner);
        }

        // HUD-WAVE: the wave-preview capsule over the live board. Skips Home, because the
        // shipped Home covers the HUD and holds the sim at tick 0 — the capsule is only worth
        // photographing with a real upcoming queue behind it.
        [UnityTest]
        public IEnumerator CaptureEvidence_WavePreviewCapsule_917x2048_WhenRequested()
        {
            var dir = System.Environment.GetEnvironmentVariable("CM_UI_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir))
            {
                Assert.Pass("capture rig disarmed — set CM_UI_CAPTURE_DIR to emit phone frames");
                yield break;
            }

            GameRoot.DevSkipShippedHome = true;
            _root = GameRoot.Launch();
            _root.MotionOffToggle = true; // still captures settle the cold-boot reveal on the next frame
            yield return null;
            yield return null;
            Assert.That(_root.Preview.FaceCount, Is.GreaterThan(0),
                "precondition: L001 has upcoming cats to draw as faces");
            yield return Capture(dir, "step-7-wave-preview.png", _root.Preview);
        }

        [UnityTest]
        public IEnumerator FailureBanner_LayoutsInside917x2048SafeArea_WithoutTextOverflow()
        {
            GameRoot.DevSkipShippedHome = false;
            _root = GameRoot.Launch();
            _root.MotionOffToggle = true; // still captures settle the cold-boot reveal on the next frame
            yield return null;
            _root.Home.Hide();
            _root.Banner.ShowKey("fail.banner.timeout");
            _root.Banner.LayoutForViewport(CaptureSafeArea, CaptureDpi);
            Canvas.ForceUpdateCanvases();
            _root.Banner.TextTransform.GetComponent<TMPro.TMP_Text>().ForceMeshUpdate();

            Assert.That(_root.Banner.PaintedRectPx.xMin,
                Is.GreaterThan(CaptureSafeArea.xMin),
                "the banner keeps a horizontal safe-area inset on the reference phone");
            Assert.That(_root.Banner.PaintedRectPx.xMax,
                Is.LessThan(CaptureSafeArea.xMax),
                "the banner keeps a horizontal safe-area inset on the reference phone");
            Assert.That(_root.Banner.TextTransform, Is.Not.Null,
                "the responsive banner exposes its real TMP transform for layout inspection");
            Assert.That(_root.Banner.IsTextOverflowing, Is.False,
                "The last train left the depot must fit at 917x2048");
        }

        [UnityTest]
        public IEnumerator ShippedHome_DioramaWindow_PreservesTickZeroBoardPixels_WhileFramePaintsAboveIt()
        {
            GameRoot.DevSkipShippedHome = false;
            _root = GameRoot.Launch();
            _root.MotionOffToggle = true; // still captures settle the cold-boot reveal on the next frame
            yield return null;
            yield return null;

            Assert.That(_root.Home, Is.Not.Null);
            Assert.That(_root.Home.IsVisible, Is.True);
            Assert.That(_root.Session.State.Tick, Is.EqualTo(0),
                "Home must hold the already-loaded board at tick 0");

            Camera camera = _root.Cam;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var target = new RenderTexture(CaptureWidth, CaptureHeight, 24,
                RenderTextureFormat.ARGB32);
            target.Create();

            try
            {
                camera.targetTexture = target;
                // ScreenSpaceCamera must observe the phone target before projected UI corners
                // are sampled. Home remains visible, so both yields keep the session at tick 0.
                yield return null;
                ApplyPhoneLayout(_root.Home);
                Canvas.ForceUpdateCanvases();
                yield return null;
                ApplyPhoneLayout(_root.Home);
                Canvas.ForceUpdateCanvases();

                Assert.That(_root.Session.State.Tick, Is.EqualTo(0));
                RectTransform window = FindRequiredRect(
                    _root.Home.transform, "DioramaWindow");
                RectTransform frame = FindRequiredRect(
                    _root.Home.transform, "DioramaFrameTop");
                Assert.That(window.GetComponent<Graphic>(), Is.Null,
                    "the aperture is geometry-free, not a clear-looking Image");
                var frameImage = frame.GetComponent<Image>();
                Assert.That(frameImage, Is.Not.Null,
                    "the named frame is real rendered geometry");
                Assert.That(frameImage.color.a, Is.GreaterThanOrEqualTo(0.95f));

                RectInt aperturePatch = InsetAndClamp(
                    ProjectedScreenRect(window, camera), 0.40f, 0.25f,
                    CaptureWidth, CaptureHeight);
                RectInt framePatch = InsetAndClamp(
                    ProjectedScreenRect(frame, camera), 0.20f, 0.20f,
                    CaptureWidth, CaptureHeight);
                Assert.That(aperturePatch.width, Is.GreaterThanOrEqualTo(32));
                Assert.That(aperturePatch.height, Is.GreaterThanOrEqualTo(32));
                Assert.That(framePatch.width, Is.GreaterThanOrEqualTo(32));
                Assert.That(framePatch.height, Is.GreaterThanOrEqualTo(4));

                // The stack still owns Home while its renderer is hidden. No frame is yielded
                // between these reads, so board state and presentation are identical samples.
                _root.Home.Hide();
                Color32[] boardOnly = ReadFrame(camera, target);
                _root.Home.Show();
                ApplyPhoneLayout(_root.Home);
                Canvas.ForceUpdateCanvases();
                Color32[] withHome = ReadFrame(camera, target);

                yield return null;
                ApplyPhoneLayout(_root.Home);
                Canvas.ForceUpdateCanvases();
                Color32[] stableHome = ReadFrame(camera, target);

                Color32[] withoutFrame;
                frameImage.enabled = false;
                try
                {
                    withoutFrame = ReadFrame(camera, target);
                }
                finally
                {
                    frameImage.enabled = true;
                }

                Assert.That(_root.Session.State.Tick, Is.EqualTo(0),
                    "the comparison must use the same paused board tick");
                Assert.That(RgbSpatialStdDev(boardOnly, aperturePatch, CaptureWidth),
                    Is.GreaterThan(6f / 255f),
                    "the aperture contains varied board-world pixels, not camera clear");
                Assert.That(MeanRgbDelta(boardOnly, withHome,
                        aperturePatch, CaptureWidth), Is.LessThanOrEqualTo(2f / 255f),
                    "Home preserves board pixels through the transparent aperture");
                Assert.That(MeanRgbDelta(withoutFrame, withHome,
                        framePatch, CaptureWidth), Is.GreaterThan(8f / 255f),
                    "the cream frame itself composites above the shaded board");
                Assert.That(ChangedFraction(withoutFrame, withHome, framePatch,
                        CaptureWidth, minimumPerChannelDelta: 4), Is.GreaterThan(0.45f),
                    "most frame pixels come from the frame, not the backdrop beneath it");
                Assert.That(MeanRgbDelta(withHome, stableHome,
                        framePatch, CaptureWidth), Is.LessThanOrEqualTo(1f / 255f),
                    "the composited frame is stable across rendered frames, not z-fighting");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                Object.Destroy(target);
            }
        }

        private static void ApplyPhoneLayout(Component view)
        {
            ApplyPhoneLayout(view, new CaptureRig.Size(CaptureWidth, CaptureHeight));
        }

        private static void ApplyPhoneLayout(Component view, CaptureRig.Size size)
        {
            ApplyLayout(view,
                CaptureRig.ScaleSafeArea(CaptureSafeArea,
                    CaptureWidth, CaptureHeight, size),
                CaptureRig.ScaleDpi(CaptureDpi, CaptureHeight, size),
                size.Width, size.Height);
        }

        private static void ApplyLayout(Component view, Rect safeArea, float dpi,
            int width, int height)
        {
            // The old views have no injectable viewport seam; once a responsive view lands,
            // this invokes its real layout law against the phone-safe rect before rendering.
            var method = view.GetType().GetMethod("LayoutForViewport",
                BindingFlags.Instance | BindingFlags.Public);
            if (method != null)
            {
                var parameters = method.GetParameters();
                method.Invoke(view, parameters.Length == 3
                    ? new object[]
                    {
                        safeArea, dpi, new Rect(0f, 0f, width, height),
                    }
                    : new object[] { safeArea, dpi });
            }
        }

        private static bool HasVisiblePaint(RectTransform layer)
            => layer != null && layer.GetComponentsInChildren<Image>(true)
                .Any(image => image.gameObject.activeInHierarchy && image.color.a > 0.01f);

        private static RectTransform FindRequiredRect(Transform root, string name)
        {
            RectTransform found = null;
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect.name != name) continue;
                found = rect;
                break;
            }
            Assert.That(found, Is.Not.Null, name + " must exist in shipped Home");
            return found;
        }

        private static Rect ProjectedScreenRect(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 first = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            float xMin = first.x, xMax = first.x, yMin = first.y, yMax = first.y;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                xMin = Mathf.Min(xMin, point.x);
                xMax = Mathf.Max(xMax, point.x);
                yMin = Mathf.Min(yMin, point.y);
                yMax = Mathf.Max(yMax, point.y);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static RectInt InsetAndClamp(Rect rect, float horizontalInset,
            float verticalInset, int width, int height)
        {
            int xMin = Mathf.Clamp(Mathf.CeilToInt(Mathf.Lerp(
                rect.xMin, rect.xMax, horizontalInset)), 0, width - 1);
            int xMax = Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(
                rect.xMax, rect.xMin, horizontalInset)), xMin + 1, width);
            int yMin = Mathf.Clamp(Mathf.CeilToInt(Mathf.Lerp(
                rect.yMin, rect.yMax, verticalInset)), 0, height - 1);
            int yMax = Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(
                rect.yMax, rect.yMin, verticalInset)), yMin + 1, height);
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static Color32[] ReadFrame(Camera camera, RenderTexture target)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                RenderTexture.active = target;
                texture = new Texture2D(target.width, target.height,
                    TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                texture.Apply();
                return texture.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                if (texture != null) Object.Destroy(texture);
            }
        }

        private static float MeanRgbDelta(Color32[] first, Color32[] second,
            RectInt sample, int imageWidth)
        {
            double total = 0d;
            int count = 0;
            for (int y = sample.yMin; y < sample.yMax; y++)
            {
                int row = y * imageWidth;
                for (int x = sample.xMin; x < sample.xMax; x++)
                {
                    Color32 a = first[row + x];
                    Color32 b = second[row + x];
                    total += Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g)
                        + Mathf.Abs(a.b - b.b);
                    count++;
                }
            }
            return (float)(total / (count * 3d * 255d));
        }

        private static float ChangedFraction(Color32[] first, Color32[] second,
            RectInt sample, int imageWidth, int minimumPerChannelDelta)
        {
            int changed = 0;
            int count = 0;
            int minimumTotalDelta = minimumPerChannelDelta * 3;
            for (int y = sample.yMin; y < sample.yMax; y++)
            {
                int row = y * imageWidth;
                for (int x = sample.xMin; x < sample.xMax; x++)
                {
                    Color32 a = first[row + x];
                    Color32 b = second[row + x];
                    int delta = Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g)
                        + Mathf.Abs(a.b - b.b);
                    if (delta > minimumTotalDelta) changed++;
                    count++;
                }
            }
            return changed / (float)count;
        }

        private static float RgbSpatialStdDev(Color32[] pixels, RectInt sample,
            int imageWidth)
        {
            double r = 0d, g = 0d, b = 0d;
            double r2 = 0d, g2 = 0d, b2 = 0d;
            int count = 0;
            for (int y = sample.yMin; y < sample.yMax; y++)
            {
                int row = y * imageWidth;
                for (int x = sample.xMin; x < sample.xMax; x++)
                {
                    Color32 pixel = pixels[row + x];
                    r += pixel.r; g += pixel.g; b += pixel.b;
                    r2 += pixel.r * pixel.r;
                    g2 += pixel.g * pixel.g;
                    b2 += pixel.b * pixel.b;
                    count++;
                }
            }
            double inv = 1d / count;
            double meanR = r * inv, meanG = g * inv, meanB = b * inv;
            double variance = ((r2 * inv - meanR * meanR)
                + (g2 * inv - meanG * meanG)
                + (b2 * inv - meanB * meanB)) / 3d;
            return (float)(System.Math.Sqrt(System.Math.Max(0d, variance)) / 255d);
        }

        private IEnumerator Capture(string dir, string name, params Component[] layouts)
        {
            CaptureRig.RequireStoreCaptureArt(
                System.Environment.GetEnvironmentVariable("CM_CAPTURE_ALLOW_PLACEHOLDER"));
            CaptureRig.Size size = CaptureRig.ParseSize(
                Environment.GetEnvironmentVariable("CM_CAPTURE_SIZE"),
                CaptureWidth, CaptureHeight);
            Camera camera = _root.Cam;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            float previousAspect = camera.aspect;
            RenderTexture target = null;
            Texture2D texture = null;
            try
            {
                target = CaptureRig.CreateTarget(size);
                Assert.That(target.sRGB, Is.True, "capture target must store sRGB pixels");
                camera.targetTexture = target;
                camera.aspect = size.Width / (float)size.Height;
                // The target must be bound for a full frame before any screen-space layout.
                // Otherwise batchmode's Game view (often 619x489) becomes the UI ruler.
                yield return null;
                if (layouts != null)
                    foreach (Component view in layouts)
                        if (view != null) ApplyPhoneLayout(view, size);
                Canvas.ForceUpdateCanvases();
                if (layouts != null && layouts.Any(view => view is HomeScreenView))
                {
                    // Let the resized skin render once, as the dedicated holder capture does.
                    // Home holds tick zero while this presentation frame settles.
                    yield return null;
                    foreach (Component view in layouts)
                        if (view != null) ApplyPhoneLayout(view, size);
                    Canvas.ForceUpdateCanvases();
                }
                camera.Render();
                RenderTexture.active = target;
                texture = CaptureRig.ReadRgb24(target);
                Assert.That(texture.isDataSRGB, Is.True,
                    "readback must be encoded as sRGB");

                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, name),
                    CaptureRig.EncodeOpaqueSrgbPng(texture));
                if (layouts != null)
                    foreach (HomeScreenView home in layouts.OfType<HomeScreenView>())
                        AssertCapturedHomeRigPixels(home, camera, target, texture.GetPixels32());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                camera.aspect = previousAspect;
                if (texture != null) Object.Destroy(texture);
                if (target != null)
                {
                    target.Release();
                    Object.Destroy(target);
                }
            }
        }

        private static void AssertCapturedHomeRigPixels(HomeScreenView home, Camera camera,
            RenderTexture target, Color32[] composed)
        {
            HomeProfileRigView rig = home.ProfileRig;
            if ((rig == null || !rig.Mounted) && string.Equals(
                Environment.GetEnvironmentVariable("CM_CAPTURE_ALLOW_PLACEHOLDER"),
                "1", StringComparison.Ordinal)) return; // Explicit placeholder diagnostics stay available.
            Assert.That(rig, Is.Not.Null, "an armed Home capture requires the admitted rig");
            Assert.That(rig.Mounted, Is.True);
            RectInt headPatch = InsetAndClamp(rig.RenderedHeadScreenRect,
                .10f, .10f, target.width, target.height);
            var skins = rig.PrefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            bool[] enabledStates = skins.Select(skin => skin.enabled).ToArray();
            Color32[] withoutRig;
            try
            {
                foreach (var skin in skins) skin.enabled = false;
                withoutRig = ReadFrame(camera, target);
            }
            finally
            {
                for (int i = 0; i < skins.Length; i++) skins[i].enabled = enabledStates[i];
            }
            float changed = ChangedFraction(composed, withoutRig, headPatch, target.width, 4);
            Debug.Log("HOME_CAPTURE rigHeadChangedFraction=" + changed + " headPatch=" + headPatch);
            Assert.That(changed, Is.GreaterThan(.10f),
                "the captured rig must paint at its fitted head bounds after the viewport change");
        }

        private static void CaptureBound(Camera camera, RenderTexture target,
            string dir, string name)
        {
            RenderTexture previousActive = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture = new Texture2D(target.width, target.height,
                    TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                texture.Apply();

                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, name), texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (texture != null) Object.Destroy(texture);
            }
        }

        private sealed class CaptureStorageRoot : IStorageRoot, IDisposable
        {
            public string SaveDirectory { get; }
            public string CacheDirectory => SaveDirectory;

            public CaptureStorageRoot()
            {
                SaveDirectory = Path.Combine(Path.GetTempPath(),
                    "cm-home-rig-capture-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(SaveDirectory);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(SaveDirectory))
                        Directory.Delete(SaveDirectory, true);
                }
                catch
                {
                    // Best-effort cleanup of isolated test state.
                }
            }
        }
    }
}
