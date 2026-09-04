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

            GameRoot.DevSkipShippedHome = false;
            _root = GameRoot.Launch();
            yield return null;
            yield return null;
            Assert.That(_root.Home, Is.Not.Null);
            Assert.That(_root.Home.IsVisible, Is.True);
            yield return Capture(dir, "step-7-home.png", _root.Home, _root.Wardrobe);
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
                Assert.That(rigChangedFraction, Is.GreaterThan(0.10f),
                    "an enabled but offscreen or fully occluded rig must fail this capture");

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

            GameRoot.DevSkipShippedHome = false;
            GameRoot.DailyEntryUnlocked = true;
            _root = GameRoot.Launch();
            yield return null;
            yield return null;
            Assert.That(_root.Home, Is.Not.Null);
            Assert.That(_root.Home.IsVisible, Is.True);
            Assert.That(_root.Home.DailyPinTransform, Is.Not.Null);
            yield return Capture(dir, "step-7-home-daily.png", _root.Home, _root.Wardrobe);
        }

        [UnityTest]
        public IEnumerator ShippedHome_WardrobeEntry_IsRaisedCreamToyButton_WithoutReplacingPortraitMount()
        {
            GameRoot.DevSkipShippedHome = false;
            _root = GameRoot.Launch();
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
            var shadow = capsule.GetComponent<Image>();
            Assert.That(shadow, Is.Not.Null);
            Assert.That(shadow.color, Is.EqualTo(Palette.DepotNavy),
                "WardrobeCapsule stays the navy raised-button shadow/root");
            Assert.That(shadow.sprite, Is.SameAs(HudShapeSprites.RoundedSquare));
            Assert.That(shadow.type, Is.EqualTo(Image.Type.Sliced));

            var face = FindRequiredRect(capsule, "WardrobeButtonFace");
            var faceImage = face.GetComponent<Image>();
            Assert.That(faceImage, Is.Not.Null);
            Assert.That(faceImage.color, Is.EqualTo(Palette.CreamCard));
            Assert.That(faceImage.sprite, Is.SameAs(HudShapeSprites.RoundedSquare));
            Assert.That(faceImage.type, Is.EqualTo(Image.Type.Sliced));

            var label = FindRequiredRect(capsule, "WardrobeLabel");
            Assert.That(label.parent, Is.SameAs(capsule));
            Assert.That(label.GetComponent<TMPro.TMP_Text>().color,
                Is.EqualTo(Palette.InkNavy));

            RectTransform portraitMount = null;
            int mountCount = 0;
            for (int i = 0; i < capsule.childCount; i++)
            {
                var child = capsule.GetChild(i) as RectTransform;
                if (child == null || child.name != "EntryPortraitMount") continue;
                portraitMount = child;
                mountCount++;
            }
            Assert.That(mountCount, Is.EqualTo(1),
                "the cosmetics seam remains one direct WardrobeCapsule child");
            Assert.That(portraitMount.anchorMin,
                Is.EqualTo(new Vector2(0.035f, 0.08f)));
            Assert.That(portraitMount.anchorMax,
                Is.EqualTo(new Vector2(0.30f, 0.92f)));
            Assert.That(face.GetSiblingIndex(), Is.LessThan(portraitMount.GetSiblingIndex()),
                "the cream face paints behind the existing portrait mount");
            Assert.That(face.GetSiblingIndex(), Is.LessThan(label.GetSiblingIndex()),
                "the cream face paints behind the existing label");

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
                camera.Render();
                RenderTexture.active = target;
                texture = CaptureRig.ReadRgb24(target);
                Assert.That(texture.isDataSRGB, Is.True,
                    "readback must be encoded as sRGB");

                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, name),
                    CaptureRig.EncodeOpaqueSrgbPng(texture));
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
