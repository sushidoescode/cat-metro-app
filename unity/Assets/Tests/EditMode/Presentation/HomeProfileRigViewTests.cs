using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using CatMetro.Presentation.Cats;
using CatMetro.Presentation.Cosmetics;
using CatMetro.Presentation.Screens;
using CatMetro.Services.Cosmetics;
using CatMetro.Services.Purchases;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.EditMode.Presentation
{
    public sealed class HomeProfileRigViewTests
    {
        private GameObject _cameraHost;
        private GameObject _canvasHost;
        private RenderTexture _target;
        private Camera _camera;
        private RectTransform _holder;
        private CosmeticPortraitView _portrait;
        private PortraitSource _portraitSource;
        private ConformingSkinnedRigFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _cameraHost = new GameObject("HomeRigTestCamera");
            _camera = _cameraHost.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 1f;
            _camera.transform.position = new Vector3(0f, 0f, -10f);
            _camera.transform.rotation = Quaternion.identity;
            _target = new RenderTexture(600, 600, 24, RenderTextureFormat.ARGB32);
            _target.Create();
            _camera.targetTexture = _target;

            _canvasHost = new GameObject("HomeRigTestCanvas");
            var canvas = _canvasHost.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 1f;

            var holderGo = new GameObject("ParkedDistrictB", typeof(RectTransform),
                typeof(Image));
            holderGo.transform.SetParent(canvas.transform, false);
            _holder = (RectTransform)holderGo.transform;
            _holder.anchorMin = _holder.anchorMax = new Vector2(0.5f, 0.5f);
            _holder.pivot = new Vector2(0.5f, 0.5f);
            _holder.sizeDelta = new Vector2(300f, 300f);
            _holder.anchoredPosition = Vector2.zero;
            _portraitSource = new PortraitSource();
            _portrait = CosmeticPortraitView.Create(_holder,
                _portraitSource, "HomeProfilePortrait");
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasHost != null) Object.DestroyImmediate(_canvasHost);
            if (_cameraHost != null) Object.DestroyImmediate(_cameraHost);
            if (_target != null)
            {
                _target.Release();
                Object.DestroyImmediate(_target);
            }
            _fixture?.Dispose();
        }

        [Test]
        public void MissingCatalogAtHome_ReportsAdmissionFailureBeforeTheMountIsCreated()
        {
            LogAssert.Expect(LogType.Warning,
                new Regex("HOME_RIG fallback branch=5 admitted=0 reason=.*Missing cat rig"));
            HomeScreenView home = HomeScreenView.Create(_canvasHost.transform,
                portraitSource: new PortraitSource(), catCatalog: new CatModelCatalog(null));
            Assert.That(home.ProfilePortrait.BaseLayerTransform.gameObject.activeSelf, Is.True);
            LogAssert.NoUnexpectedReceived();
            Object.DestroyImmediate(home.gameObject);
        }

        [TestCase(1f, false)]
        [TestCase(5f, true)]
        public void CosmeticPlane_StaysReachableBeyondNearClip_WithoutChangingSafeLift(float cameraSize, bool needsClamp)
        {
            _camera.orthographicSize = cameraSize;
            _camera.nearClipPlane = .1f;
            _camera.transform.rotation = Quaternion.Euler(20f, 30f, 0f);
            var canvas = _canvasHost.GetComponent<Canvas>();
            canvas.worldCamera = null;
            canvas.worldCamera = _camera;
            Canvas.ForceUpdateCanvases();
            _fixture = new ConformingSkinnedRigFixture();
            var mount = HomeProfileRigView.Create(_holder, _portrait,
                CatModelCatalog.FromEntry(new CatModelCatalog.Entry(_fixture.Prefab, 180f)));

            Assert.That(mount.Layout(_camera), Is.True);

            RectTransform portrait = _portrait.RootTransform;
            float depth = _camera.WorldToScreenPoint(portrait.position).z;
            Assert.That(depth, Is.GreaterThan(_camera.nearClipPlane),
                "visible rig cosmetics must remain beyond the camera's near clip plane");
            Assert.That(RectTransformUtility.ScreenPointToWorldPointInRectangle(portrait,
                mount.RenderedHeadScreenRect.center, _camera, out _), Is.True,
                "the projected face must still reach the cosmetic plane for body fitting");
            float requestedLift = -Mathf.Min(_holder.rect.width, _holder.rect.height) * .2f;
            if (needsClamp)
            {
                Assert.That(portrait.anchoredPosition3D.z, Is.GreaterThan(requestedLift));
                Assert.That(depth, Is.LessThan(_camera.nearClipPlane + .02f),
                    "clipping correction must keep the cosmetics close to the requested plane");
            }
            else Assert.That(portrait.anchoredPosition3D.z, Is.EqualTo(requestedLift).Within(.0001f),
                "an already visible portrait must retain its existing lift");
        }

        [Test]
        public void LostCamera_ReportsFallbackAndRecovery_WithoutRepeatingOnEveryLayout()
        {
            _fixture = new ConformingSkinnedRigFixture();
            HomeProfileRigView view = HomeProfileRigView.Create(_holder, _portrait,
                CatModelCatalog.FromEntry(new CatModelCatalog.Entry(_fixture.Prefab, 180f)));
            LogAssert.Expect(LogType.Warning,
                new Regex("HOME_RIG fallback branch=1 admitted=1 reason=.*camera"));
            Assert.That(view.Layout(null), Is.False);
            Assert.That(view.Layout(null), Is.False);
            LogAssert.Expect(LogType.Log, new Regex("HOME_RIG mounted=true admitted=1"));
            Assert.That(view.Layout(_camera), Is.True);
            Assert.That(view.Layout(_camera), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Layout_SettlesPreferredHolderSizeBeforeComputingScale()
        {
            _fixture = new ConformingSkinnedRigFixture();
            var mount = HomeProfileRigView.Create(_holder, _portrait,
                CatModelCatalog.FromEntry(new CatModelCatalog.Entry(_fixture.Prefab, 180f)));
            _holder.sizeDelta = Vector2.one;
            var preferred = _holder.gameObject.AddComponent<LayoutElement>();
            preferred.preferredWidth = preferred.preferredHeight = 300f;
            var fitter = _holder.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Assert.That(_holder.rect.width, Is.LessThan(10f),
                "the pending UI layout must not already be settled in this reproduction");

            Assert.That(mount.Layout(_camera), Is.True);

            Assert.That(_holder.rect.size, Is.EqualTo(new Vector2(300f, 300f)));
            AssertFullSkinFits(mount, "fit must use the settled holder and the sampled skin");
            Assert.That(mount.RenderedHeadScreenRect.width, Is.GreaterThan(50f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CanvasRender_RefitsAChangedHolder_AndRecoversAfterEnable(bool homeWrapper)
        {
            _fixture = new ConformingSkinnedRigFixture();
            _holder.sizeDelta = new Vector2(120f, 120f);
            var catalog = CatModelCatalog.FromEntry(new CatModelCatalog.Entry(_fixture.Prefab, 180f));
            ProfileRigMount mount = homeWrapper
                ? HomeProfileRigView.Create(_holder, _portrait, catalog)
                : ProfileRigMount.Create(_holder, _portrait, catalog);
            Assert.That(mount.Layout(_camera), Is.True);
            float originalHeadWidth = mount.RenderedHeadScreenRect.width;
            float originalScale = mount.PrefabRoot.parent.parent.localScale.x;

            _holder.sizeDelta = new Vector2(300f, 300f);
            Canvas.ForceUpdateCanvases(); // No second screen-level Layout call.

            Assert.That(mount.PrefabRoot.parent.parent.localScale.x, Is.EqualTo(originalScale * 2.5f).Within(.01f));
            AssertFullSkinFits(mount, "resized holder");
            Assert.That(mount.RenderedHeadScreenRect.width, Is.GreaterThan(originalHeadWidth * 2f));
            AssertRectWithin(mount.RenderedHeadScreenRect,
                IndependentlyProjectedFixtureHead(mount.PrefabRoot), 3f,
                "head read-back must describe the resized frame");

            mount.gameObject.SetActive(false);
            _holder.sizeDelta = new Vector2(240f, 240f);
            Canvas.ForceUpdateCanvases();
            Assert.That(mount.PrefabRoot.parent.parent.localScale.x, Is.EqualTo(originalScale * 2.5f).Within(.01f),
                "a hidden mount must not refit until it is shown again");
            mount.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            Assert.That(mount.PrefabRoot.parent.parent.localScale.x, Is.EqualTo(originalScale * 2f).Within(.01f));
            AssertFullSkinFits(mount, "reshown holder");
        }

        [Test]
        public void CanvasRender_AfterMountDestruction_DoesNotAccessDestroyedGeometry()
        {
            _fixture = new ConformingSkinnedRigFixture();
            var mount = HomeProfileRigView.Create(_holder, _portrait,
                CatModelCatalog.FromEntry(new CatModelCatalog.Entry(_fixture.Prefab, 180f)));
            LogAssert.Expect(LogType.Log, new Regex("HOME_RIG mounted=true admitted=1"));
            Assert.That(mount.Layout(_camera), Is.True);
            Object.DestroyImmediate(mount.gameObject);
            _holder.sizeDelta = new Vector2(240f, 240f);

            Canvas.ForceUpdateCanvases();
            Canvas.ForceUpdateCanvases();

            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(0f)]
        [TestCase(.001f)]
        public void UnusableHolder_StaysFallback_AndRecoversWhenCanvasSizeSettles(float shortSide)
        {
            _fixture = new ConformingSkinnedRigFixture();
            _holder.sizeDelta = new Vector2(shortSide, shortSide);
            var mount = HomeProfileRigView.Create(_holder, _portrait,
                CatModelCatalog.FromEntry(new CatModelCatalog.Entry(_fixture.Prefab, 180f)));
            Assert.That(mount.Layout(_camera), Is.False,
                "an empty or subpixel head cannot establish a successful mount");
            Assert.That(mount.Mounted, Is.False);
            Assert.That(_portrait.BaseLayerTransform.gameObject.activeSelf, Is.True);

            _holder.sizeDelta = new Vector2(300f, 300f);
            Canvas.ForceUpdateCanvases();

            Assert.That(mount.Mounted, Is.True,
                "recovery must retain the requested camera even when the initial rect was empty");
            Assert.That(mount.RenderedHeadScreenRect.width, Is.GreaterThan(50f));
            Assert.That(_portrait.BaseLayerTransform.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void CanvasRender_RefreshesHeadReadbackWhenCameraViewportChanges()
        {
            _fixture = new ConformingSkinnedRigFixture();
            var mount = HomeProfileRigView.Create(_holder, _portrait,
                CatModelCatalog.FromEntry(new CatModelCatalog.Entry(_fixture.Prefab, 180f)));
            Assert.That(mount.Layout(_camera), Is.True);
            Rect original = mount.RenderedHeadScreenRect;

            _camera.rect = new Rect(.1f, .15f, .7f, .7f);
            Canvas.ForceUpdateCanvases();

            Assert.That(mount.RenderedHeadScreenRect, Is.Not.EqualTo(original));
            AssertRectWithin(mount.RenderedHeadScreenRect,
                IndependentlyProjectedFixtureHead(mount.PrefabRoot), 3f,
                "viewport changes must update the projected head before drawing");
        }

        [Test]
        public void NumericMountDiagnostic_UsesInvariantNumbers_AndReportsGeometryChangesOnce()
        {
            var messages = new List<string>();
            void Capture(string message, string trace, LogType type)
            {
                if (message.StartsWith("HOME_RIG mounted=true", StringComparison.Ordinal))
                    messages.Add(message);
            }
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            UnityEngine.Application.logMessageReceived += Capture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                _fixture = new ConformingSkinnedRigFixture();
                _holder.sizeDelta = new Vector2(123.5f, 234.75f);
                var mount = HomeProfileRigView.Create(_holder, _portrait,
                    CatModelCatalog.FromEntry(new CatModelCatalog.Entry(_fixture.Prefab, 180f)));
                Assert.That(mount.Layout(_camera), Is.True);
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0], Does.Match(@"scale=[0-9]+\.[0-9]{3} shortSide=123\.500"));
                Assert.That(messages[0], Does.Contain("holder=123.500x234.750 headPx="));

                mount.Layout(_camera);
                Canvas.ForceUpdateCanvases();
                Canvas.ForceUpdateCanvases();
                Assert.That(messages, Has.Count.EqualTo(1), "stable geometry must not spam logcat");

                _holder.sizeDelta = new Vector2(246.5f, 234.75f);
                Canvas.ForceUpdateCanvases();
                Assert.That(messages, Has.Count.EqualTo(2));
                Assert.That(messages[1], Does.Match(@"scale=[0-9]+\.[0-9]{3} shortSide=234\.750"));
                Canvas.ForceUpdateCanvases();
                Assert.That(messages, Has.Count.EqualTo(2));
            }
            finally
            {
                UnityEngine.Application.logMessageReceived -= Capture;
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        [Test]
        public void WardrobeMount_UsesTheMatchingRig_AndKeepsOtherBreedsAsPortraits()
        {
            _fixture = new ConformingSkinnedRigFixture();
            var catalog = CatModelCatalog.FromEntry(new CatModelCatalog.Entry(
                _fixture.Prefab, 180f, "red_tabby"));
            var inventory = CosmeticAssetInventory.Parse(
                Resources.Load<TextAsset>("Cosmetics/portrait_assets").text,
                CosmeticPortraitPainter.SupportedRendererTokens);
            var cosmetics = CosmeticCatalog.Parse(
                Resources.Load<TextAsset>("Cosmetics/cosmetic_catalog").text,
                inventory.AssetIds, inventory.ProvenanceAssetIds);
            using var profile = new CosmeticProfileService(cosmetics, inventory,
                new InMemoryCosmeticProfilePersistence(CosmeticProfileSnapshot.Empty),
                PurchaseRuntime.Current);
            var wardrobe = WardrobeScreenView.Create(_canvasHost.transform,
                PurchaseRuntime.Current, profile, new DisabledCosmeticRewardedRoute(), catalog);
            wardrobe.Open();
            wardrobe.LayoutForViewport(new Rect(0, 0, 600, 1100), 160f);
            var mount = wardrobe.GetComponentInChildren<ProfileRigMount>(true);
            Assert.That(mount, Is.Not.Null, "Wardrobe must mount the admitted rig under its hero");
            Assert.That(mount.Mounted, Is.True);
            Assert.That(mount.transform.parent.name, Is.EqualTo("LargePortraitMount"));
            Assert.That(mount.AppliedFacingYaw, Is.EqualTo(180f).Within(0.01f));
            Assert.That(wardrobe.LargePortrait.BaseLayerTransform.gameObject.activeSelf, Is.False);
            Assert.That(profile.TrySelectCat("blue_siamese"), Is.True);
            Assert.That(mount.Mounted, Is.False);
            Assert.That(wardrobe.LargePortrait.BaseLayerTransform.gameObject.activeSelf, Is.True);
            Assert.That(profile.TrySelectCat("red_tabby"), Is.True);
            Assert.That(mount.Mounted, Is.True);
            wardrobe.Hide();
            Assert.That(mount.gameObject.activeInHierarchy, Is.False);
            Object.DestroyImmediate(wardrobe.gameObject);
        }

        [Test]
        public void Turntable_ChangesTheWrapperYawAndKeepsCosmeticsAligned_WithNoPrefabMutation()
        {
            _fixture = new ConformingSkinnedRigFixture();
            var mount = ProfileRigMount.Create(_holder, _portrait,
                CatModelCatalog.FromEntry(new CatModelCatalog.Entry(_fixture.Prefab, 180f)),
                0f, "WARDROBE_RIG");
            mount.TurntableAmplitude = 15f;
            Assert.That(mount.Layout(_camera), Is.True);
            mount.AdvanceTurntable(3f);
            Assert.That(mount.AppliedFacingYaw, Is.EqualTo(195f).Within(0.01f));
            AssertRectWithin(mount.RenderedHeadScreenRect,
                IndependentlyProjectedFixtureHead(mount.PrefabRoot), 3f,
                "turntable cosmetics track the live head at the positive yaw limit");
            mount.AdvanceTurntable(6f);
            Assert.That(mount.AppliedFacingYaw, Is.EqualTo(165f).Within(0.01f));
            AssertRectWithin(mount.RenderedHeadScreenRect,
                IndependentlyProjectedFixtureHead(mount.PrefabRoot), 3f,
                "turntable cosmetics track the live head at the negative yaw limit");
            Assert.That(mount.PrefabRoot.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(mount.PrefabRoot.localScale, Is.EqualTo(Vector3.one));
            Assert.That(mount.RenderedHeadScreenRect.width, Is.GreaterThan(1f));
            Assert.That(_portrait.BaseLayerTransform.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void AdmittedSkinnedRig_FreezesIdleSitAndPreservesPrefabRootIdentity()
        {
            _fixture = new ConformingSkinnedRigFixture();
            var pinnedPosition = new Vector3(0.031f, -0.017f, 0.044f);
            _fixture.Prefab.transform.localPosition = pinnedPosition;
            var catalog = CatModelCatalog.FromEntry(
                new CatModelCatalog.Entry(_fixture.Prefab, 180f));

            HomeProfileRigView view = HomeProfileRigView.Create(
                _holder, _portrait, catalog);
            bool laidOut = view.Layout(_camera);

            Assert.That(laidOut, Is.True);
            Assert.That(view.Mounted, Is.True);
            Assert.That(view.CatalogAdmittedEntryCount, Is.EqualTo(1));
            Assert.That(view.SkinnedMeshRendererCount, Is.EqualTo(1));
            Assert.That(view.AnimatorCount, Is.Zero,
                "Home freezes and removes only its clone's Animator");
            Assert.That(_fixture.Prefab.GetComponentsInChildren<Animator>(true).Length,
                Is.EqualTo(1), "catalog source keeps the Animator used by the board");
            Assert.That(view.SampledPose, Is.EqualTo(CatModelCatalog.IdleSitClip));
            Assert.That(view.AppliedFacingYaw,
                Is.EqualTo(160f).Within(0.001f),
                "the entry's 180 degree correction receives Home's -20 degree turn");
            Assert.That(view.PrefabRoot.localPosition, Is.EqualTo(pinnedPosition));
            Assert.That(view.PrefabRoot.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(view.PrefabRoot.localScale, Is.EqualTo(Vector3.one));
            Transform sampledHead = view.PrefabRoot.Find(
                "ImportedModel/" + CatModelCatalog.HeadDeformerRootPath);
            Assert.That(sampledHead, Is.Not.Null);
            Assert.That(sampledHead.localPosition.y,
                Is.EqualTo(ConformingSkinnedRigFixture.IdleHeadY).Within(0.0001f),
                "the stripped clone retains the sampled sitting pose");
            Assert.That(_portrait.BaseLayerTransform.gameObject.activeSelf, Is.False);
            Assert.That(_portrait.OutfitLayerTransform.gameObject.activeSelf, Is.True);
            Assert.That(_portrait.FrameLayerTransform.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void MissingRig_LeavesTheCompleteTwoDimensionalPortraitUnchanged()
        {
            Vector2 anchorMin = _portrait.RootTransform.anchorMin;
            Vector2 anchorMax = _portrait.RootTransform.anchorMax;
            Vector2 offsetMin = _portrait.RootTransform.offsetMin;
            Vector2 offsetMax = _portrait.RootTransform.offsetMax;

            HomeProfileRigView view = HomeProfileRigView.Create(
                _holder, _portrait, new CatModelCatalog(null));

            Assert.That(view.Mounted, Is.False);
            Assert.That(view.CatalogAdmittedEntryCount, Is.Zero);
            Assert.That(view.SkinnedMeshRendererCount, Is.Zero);
            Assert.That(_portrait.BaseLayerTransform.gameObject.activeSelf, Is.True);
            Assert.That(_portrait.OutfitLayerTransform.gameObject.activeSelf, Is.True);
            Assert.That(_portrait.FrameLayerTransform.gameObject.activeSelf, Is.True);
            Assert.That(_portrait.RootTransform.anchorMin, Is.EqualTo(anchorMin));
            Assert.That(_portrait.RootTransform.anchorMax, Is.EqualTo(anchorMax));
            Assert.That(_portrait.RootTransform.offsetMin, Is.EqualTo(offsetMin));
            Assert.That(_portrait.RootTransform.offsetMax, Is.EqualTo(offsetMax));
        }

        [Test]
        public void HomeProfileHolder_WithNoAdmittedRig_KeepsTheOriginalTwoDimensionalTree()
        {
            HomeScreenView home = HomeScreenView.Create(_canvasHost.transform,
                portraitSource: new PortraitSource(),
                catCatalog: new CatModelCatalog(null));

            Assert.That(home.ProfileRig, Is.Null,
                "a missing optional rig must not change the shipped Home hierarchy");
            Assert.That(home.ProfilePortrait, Is.Not.Null);
            Assert.That(home.ProfilePortrait.BaseLayerTransform.gameObject.activeSelf,
                Is.True);
            Assert.That(home.ProfilePortrait.OutfitLayerTransform.gameObject.activeSelf,
                Is.True);
            Assert.That(home.ProfilePortrait.FrameLayerTransform.gameObject.activeSelf,
                Is.True);
            Assert.That(home.GetComponentsInChildren<HomeProfileRigView>(true), Is.Empty);
            Object.DestroyImmediate(home.gameObject);
        }

        [TestCase(1f, 1f, 1f)]
        [TestCase(1.7f, 0.8f, 1.2f)]
        public void RenderedHeadBounds_DriveTheCosmeticOverlayWithinThreePixels(
            float skinScaleX, float skinScaleY, float skinScaleZ)
        {
            _fixture = new ConformingSkinnedRigFixture();
            var catalog = CatModelCatalog.FromEntry(
                new CatModelCatalog.Entry(_fixture.Prefab, 180f));
            HomeProfileRigView view = HomeProfileRigView.Create(
                _holder, _portrait, catalog);

            view.PrefabRoot.GetComponentInChildren<SkinnedMeshRenderer>(true)
                .transform.localScale = new Vector3(skinScaleX, skinScaleY, skinScaleZ);
            Assert.That(view.Layout(_camera), Is.True);
            Canvas.ForceUpdateCanvases();

            Rect expectedHead = IndependentlyProjectedFixtureHead(view.PrefabRoot);
            Rect portraitRect = ProjectedRect(_portrait.RootTransform);
            Rect actualHeadGuide = Rect.MinMaxRect(
                Mathf.Lerp(portraitRect.xMin, portraitRect.xMax, 0.18f),
                Mathf.Lerp(portraitRect.yMin, portraitRect.yMax, 0.28f),
                Mathf.Lerp(portraitRect.xMin, portraitRect.xMax, 0.82f),
                Mathf.Lerp(portraitRect.yMin, portraitRect.yMax, 0.94f));

            AssertRectWithin(actualHeadGuide, expectedHead, 3f,
                "outfit/frame geometry may drift only a few pixels from the rendered head");
        }

        [Test]
        public void LostAlignmentInputs_RestoreTheCompleteTwoDimensionalFallback()
        {
            _fixture = new ConformingSkinnedRigFixture();
            HomeProfileRigView view = HomeProfileRigView.Create(_holder, _portrait,
                CatModelCatalog.FromEntry(
                    new CatModelCatalog.Entry(_fixture.Prefab, 180f)));
            Assert.That(view.Layout(_camera), Is.True);

            Assert.That(view.Layout(null), Is.False);

            Assert.That(view.Mounted, Is.False);
            Assert.That(_portrait.BaseLayerTransform.gameObject.activeSelf, Is.True);
            Assert.That(_portrait.OutfitLayerTransform.gameObject.activeSelf, Is.True);
            Assert.That(_portrait.FrameLayerTransform.gameObject.activeSelf, Is.True);
            Assert.That(view.PrefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                [0].enabled, Is.False);
        }

        [Test]
        public void SelectedCatWithoutMatchingRig_UsesFullPortrait_ThenRemountsItsMatchingRig()
        {
            _fixture = new ConformingSkinnedRigFixture();
            HomeProfileRigView view = HomeProfileRigView.Create(_holder, _portrait,
                CatModelCatalog.FromEntry(new CatModelCatalog.Entry(
                    _fixture.Prefab, 180f, "red_tabby")));
            Assert.That(view.Layout(_camera), Is.True);

            _portraitSource.Select("blue_siamese", "cat.blue_siamese");

            Assert.That(view.Mounted, Is.False,
                "one licensed orange tabby must not impersonate another selected breed");
            Assert.That(_portrait.BaseLayerTransform.gameObject.activeSelf, Is.True);
            Assert.That(_portrait.AppliedCatId, Is.EqualTo("blue_siamese"));
            Assert.That(view.PrefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                [0].enabled, Is.False);

            _portraitSource.Select("red_tabby", "cat.red_tabby");

            Assert.That(view.Mounted, Is.True);
            Assert.That(_portrait.BaseLayerTransform.gameObject.activeSelf, Is.False);
            Assert.That(_portrait.AppliedCatId, Is.EqualTo("red_tabby"));
            Assert.That(view.PrefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                [0].enabled, Is.True);
        }

        [Test]
        public void HomeProfileHolder_MountsRigBelowTheExistingPortraitAndKeepsItAcrossLayout()
        {
            _fixture = new ConformingSkinnedRigFixture();
            var catalog = CatModelCatalog.FromEntry(
                new CatModelCatalog.Entry(_fixture.Prefab, 180f));
            HomeScreenView home = HomeScreenView.Create(_canvasHost.transform,
                portraitSource: new PortraitSource(), catCatalog: catalog);
            home.Show();
            home.LayoutForViewport(new Rect(0f, 0f, 600f, 600f), 160f,
                new Rect(0f, 0f, 600f, 600f));
            Canvas.ForceUpdateCanvases();

            var holder = FindDirectChild(home.transform, "HeroCard", "ParkedDistrictB");
            HomeProfileRigView rig = home.ProfileRig;
            Assert.That(rig, Is.Not.Null);
            Assert.That(rig.transform.parent, Is.SameAs(holder));
            Assert.That(home.ProfilePortrait.transform.parent, Is.SameAs(holder));
            Assert.That(rig.transform.GetSiblingIndex(),
                Is.LessThan(home.ProfilePortrait.transform.GetSiblingIndex()),
                "the real cat is mounted beneath the existing cosmetic paint seam");
            Assert.That(rig.Mounted, Is.True);
            Assert.That(rig.CatalogAdmittedEntryCount, Is.EqualTo(1));
            Assert.That(rig.SkinnedMeshRendererCount, Is.GreaterThan(0));

            Transform root = rig.PrefabRoot;
            Vector3 localPosition = root.localPosition;
            Quaternion localRotation = root.localRotation;
            Vector3 localScale = root.localScale;
            home.Hide();
            home.Show();
            home.LayoutForViewport(new Rect(0f, 0f, 600f, 600f), 160f,
                new Rect(0f, 0f, 600f, 600f));

            Assert.That(home.ProfileRig.PrefabRoot, Is.SameAs(root));
            Assert.That(root.localPosition, Is.EqualTo(localPosition));
            Assert.That(root.localRotation, Is.EqualTo(localRotation));
            Assert.That(root.localScale, Is.EqualTo(localScale));
            Object.DestroyImmediate(home.gameObject);
        }

        [TestCase(300f, 240f, 1.28f)]
        [TestCase(150f, 260f, 2f)]
        public void Layout_FitsAndCentersTheWholeSampledSkin_AfterHeadGrowth(
            float width, float height, float headScale)
        {
            _fixture = new ConformingSkinnedRigFixture();
            _holder.sizeDelta = new Vector2(width, height);
            var mount = ProfileRigMount.Create(_holder, _portrait,
                CatModelCatalog.FromEntry(new CatModelCatalog.Entry(_fixture.Prefab, 180f)));
            var skin = mount.PrefabRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            skin.bones[1].localScale *= headScale;
            Vector3 enlargedScale = skin.bones[1].localScale;
            Assert.That(mount.Layout(_camera), Is.True);
            AssertFullSkinFits(mount, "enlarged head and unweighted body must both remain in the holder");
            Vector3 settledScale = mount.PrefabRoot.parent.parent.localScale;
            Assert.That(mount.Layout(_camera), Is.True);
            Assert.That(Vector3.Distance(mount.PrefabRoot.parent.parent.localScale, settledScale), Is.LessThan(.001f));
            Assert.That(skin.bones[1].localScale, Is.EqualTo(enlargedScale),
                "fitting must preserve head growth relative to the body");
            Assert.That(mount.PrefabRoot.localScale, Is.EqualTo(Vector3.one));
        }

        private void AssertFullSkinFits(ProfileRigMount mount, string message)
        {
            var skin = mount.PrefabRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            // Independent linear skinning of all fixture vertices, including the entirely
            // body-weighted feet. Do not repeat production's BakeMesh projection.
            Vector3[] vertices = skin.sharedMesh.vertices;
            Matrix4x4[] bindposes = skin.sharedMesh.bindposes;
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < vertices.Length; i++)
            {
                int bone = i < 4 ? 0 : 1;
                Vector3 point = _camera.WorldToScreenPoint((skin.bones[bone].localToWorldMatrix
                    * bindposes[bone]).MultiplyPoint3x4(vertices[i]));
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            Rect full = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            Rect holder = ProjectedRect(_holder);
            Assert.That(full.xMin, Is.GreaterThanOrEqualTo(holder.xMin + holder.width * .03f), message);
            Assert.That(full.xMax, Is.LessThanOrEqualTo(holder.xMax - holder.width * .03f), message);
            Assert.That(full.yMin, Is.GreaterThanOrEqualTo(holder.yMin + holder.height * .03f), message);
            Assert.That(full.yMax, Is.LessThanOrEqualTo(holder.yMax - holder.height * .03f), message);
            Assert.That(full.center.x, Is.EqualTo(holder.center.x).Within(.5f), message);
            Assert.That(full.center.y, Is.EqualTo(holder.center.y).Within(.5f), message);
            Assert.That(Mathf.Max(full.width / holder.width, full.height / holder.height),
                Is.GreaterThan(.89f), "the full cat should still fill its holder");
        }

        private Rect IndependentlyProjectedFixtureHead(Transform prefabRoot)
        {
            var skin = prefabRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            // Fixture vertices 4..7 belong entirely to head bone 1. Project from its
            // bind pose directly; repeating BakeMesh here would share production's error.
            Vector3[] vertices = skin.sharedMesh.vertices;
            Assert.That(vertices.Length, Is.EqualTo(8));
            Matrix4x4 headToWorld = skin.bones[1].localToWorldMatrix
                * skin.sharedMesh.bindposes[1];
            Vector3 first = _camera.WorldToScreenPoint(headToWorld.MultiplyPoint3x4(vertices[4]));
            float xMin = first.x, xMax = first.x;
            float yMin = first.y, yMax = first.y;
            for (int i = 5; i <= 7; i++)
            {
                Vector3 point = _camera.WorldToScreenPoint(headToWorld.MultiplyPoint3x4(vertices[i]));
                xMin = Mathf.Min(xMin, point.x);
                xMax = Mathf.Max(xMax, point.x);
                yMin = Mathf.Min(yMin, point.y);
                yMax = Mathf.Max(yMax, point.y);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private Rect ProjectedRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 first = _camera.WorldToScreenPoint(corners[0]);
            float xMin = first.x, xMax = first.x;
            float yMin = first.y, yMax = first.y;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector3 point = _camera.WorldToScreenPoint(corners[i]);
                xMin = Mathf.Min(xMin, point.x);
                xMax = Mathf.Max(xMax, point.x);
                yMin = Mathf.Min(yMin, point.y);
                yMax = Mathf.Max(yMax, point.y);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static RectTransform FindDirectChild(Transform root,
            string parentName, string childName)
        {
            RectTransform parent = null;
            foreach (RectTransform candidate in root.GetComponentsInChildren<RectTransform>(true))
                if (candidate.name == parentName) parent = candidate;
            Assert.That(parent, Is.Not.Null);
            RectTransform child = null;
            int count = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                var candidate = parent.GetChild(i) as RectTransform;
                if (candidate == null || candidate.name != childName) continue;
                child = candidate;
                count++;
            }
            Assert.That(count, Is.EqualTo(1));
            return child;
        }

        private static void AssertRectWithin(Rect actual, Rect expected,
            float tolerance, string message)
        {
            Assert.That(actual.xMin, Is.EqualTo(expected.xMin).Within(tolerance), message);
            Assert.That(actual.yMin, Is.EqualTo(expected.yMin).Within(tolerance), message);
            Assert.That(actual.xMax, Is.EqualTo(expected.xMax).Within(tolerance), message);
            Assert.That(actual.yMax, Is.EqualTo(expected.yMax).Within(tolerance), message);
        }

        private sealed class PortraitSource : ICosmeticPortraitSource
        {
            private readonly Dictionary<string, CosmeticPortraitAssetDefinition> _assets =
                new Dictionary<string, CosmeticPortraitAssetDefinition>(StringComparer.Ordinal)
                {
                    ["cat.red_tabby"] = new CosmeticPortraitAssetDefinition(
                        "cat.red_tabby", "cat.red_tabby", "test"),
                    ["cat.blue_siamese"] = new CosmeticPortraitAssetDefinition(
                        "cat.blue_siamese", "cat.blue_siamese", "test"),
                    ["outfit.conductor"] = new CosmeticPortraitAssetDefinition(
                        "outfit.conductor", "outfit.conductor", "test"),
                    ["frame.brass"] = new CosmeticPortraitAssetDefinition(
                        "frame.brass", "frame.brass", "test"),
                };

            public CosmeticPortraitSnapshot CurrentPortrait { get; private set; } =
                new CosmeticPortraitSnapshot("red_tabby", "cat.red_tabby",
                    "outfit.conductor", "", "frame.brass");

            public event Action Changed;

            public void Select(string catId, string baseAssetId)
            {
                CurrentPortrait = new CosmeticPortraitSnapshot(catId, baseAssetId,
                    "outfit.conductor", "", "frame.brass");
                Changed?.Invoke();
            }

            public bool TryGetPortraitAsset(string assetId,
                out CosmeticPortraitAssetDefinition asset)
                => _assets.TryGetValue(assetId ?? string.Empty, out asset);
        }

        private sealed class ConformingSkinnedRigFixture : IDisposable
        {
            private readonly AnimatorController _controller;
            private readonly List<AnimationClip> _clips = new List<AnimationClip>();
            private readonly Mesh _mesh;
            private readonly Material _material;

            public ConformingSkinnedRigFixture()
            {
                Prefab = new GameObject("ConformingHomeCatRig");
                // Match the admitted prefab topology: the catalog root owns a correction
                // wrapper, while the Animator and its relative bone paths live one level in.
                var importedModel = Child(Prefab.transform, "ImportedModel");
                var armature = Child(importedModel, "Armature");
                var root = Child(armature, "tripo::Root");
                var bodyBone = Child(root, "Body");
                var head0 = Child(root, "tripo::Head_0");
                var head1 = Child(head0, "tripo::Head_1");
                var head2 = Child(head1, "tripo::Head_2");

                var skinGo = new GameObject("RigSkin");
                skinGo.transform.SetParent(importedModel, false);
                var skin = skinGo.AddComponent<SkinnedMeshRenderer>();
                _mesh = MakeMesh(skin.transform, bodyBone, head2);
                skin.sharedMesh = _mesh;
                skin.bones = new[] { bodyBone, head2 };
                skin.rootBone = root;
                skin.localBounds = _mesh.bounds;
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Color");
                _material = new Material(shader);
                skin.sharedMaterial = _material;

                _controller = new AnimatorController();
                _controller.AddLayer("Base Layer");
                AnimatorState defaultState = null;
                foreach (string clipName in new[]
                         {
                             CatModelCatalog.IdleSitClip,
                             CatModelCatalog.WalkClip,
                             CatModelCatalog.BoardClip,
                             CatModelCatalog.AlightClip,
                             CatModelCatalog.CelebrateClip,
                         })
                {
                    float headY = clipName == CatModelCatalog.IdleSitClip ? IdleHeadY : 0f;
                    var clip = new AnimationClip { name = clipName };
                    clip.SetCurve(CatModelCatalog.HeadDeformerRootPath,
                        typeof(Transform), "localPosition.y",
                        AnimationCurve.Constant(0f, 1f / 24f, headY));
                    _clips.Add(clip);
                    AnimatorState state = _controller.layers[0].stateMachine.AddState(clipName);
                    state.motion = clip;
                    if (clipName == CatModelCatalog.CelebrateClip) defaultState = state;
                }
                _controller.layers[0].stateMachine.defaultState = defaultState;
                var animator = importedModel.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = _controller;
                animator.applyRootMotion = false;
            }

            public const float IdleHeadY = 0.025f;
            public GameObject Prefab { get; }

            public void Dispose()
            {
                Object.DestroyImmediate(Prefab);
                Object.DestroyImmediate(_controller);
                foreach (AnimationClip clip in _clips) Object.DestroyImmediate(clip);
                Object.DestroyImmediate(_mesh);
                Object.DestroyImmediate(_material);
            }

            private static Transform Child(Transform parent, string name)
            {
                var child = new GameObject(name).transform;
                child.SetParent(parent, false);
                return child;
            }

            private static Mesh MakeMesh(Transform rendererTransform,
                Transform bodyBone, Transform headBone)
            {
                var mesh = new Mesh { name = "ConformingHomeCatSkin" };
                mesh.vertices = new[]
                {
                    new Vector3(-0.25f, 0f, 0f),
                    new Vector3(0.25f, 0f, 0f),
                    new Vector3(-0.25f, 0.45f, 0f),
                    new Vector3(0.25f, 0.45f, 0f),
                    new Vector3(-0.30f, 0.45f, 0f),
                    new Vector3(0.30f, 0.45f, 0f),
                    new Vector3(-0.30f, 1f, 0f),
                    new Vector3(0.30f, 1f, 0f),
                };
                mesh.triangles = new[]
                {
                    0, 2, 1, 1, 2, 3,
                    4, 6, 5, 5, 6, 7,
                };
                var weights = new BoneWeight[8];
                for (int i = 0; i < weights.Length; i++)
                {
                    weights[i].boneIndex0 = i < 4 ? 0 : 1;
                    weights[i].weight0 = 1f;
                }
                mesh.boneWeights = weights;
                mesh.bindposes = new[]
                {
                    bodyBone.worldToLocalMatrix * rendererTransform.localToWorldMatrix,
                    headBone.worldToLocalMatrix * rendererTransform.localToWorldMatrix,
                };
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
