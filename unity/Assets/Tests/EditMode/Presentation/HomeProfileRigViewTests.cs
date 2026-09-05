using System;
using System.Collections.Generic;
using CatMetro.Presentation.Cats;
using CatMetro.Presentation.Cosmetics;
using CatMetro.Presentation.Screens;
using CatMetro.Services.Cosmetics;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
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

        [Test]
        public void RenderedHeadBounds_DriveTheCosmeticOverlayWithinThreePixels()
        {
            _fixture = new ConformingSkinnedRigFixture();
            var catalog = CatModelCatalog.FromEntry(
                new CatModelCatalog.Entry(_fixture.Prefab, 180f));
            HomeProfileRigView view = HomeProfileRigView.Create(
                _holder, _portrait, catalog);

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

        private Rect IndependentlyProjectedFixtureHead(Transform prefabRoot)
        {
            var skin = prefabRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var baked = new Mesh();
            try
            {
                skin.BakeMesh(baked, false);
                Vector3[] vertices = baked.vertices;
                Assert.That(vertices.Length, Is.EqualTo(8));
                Vector3 first = _camera.WorldToScreenPoint(
                    skin.transform.TransformPoint(vertices[4]));
                float xMin = first.x, xMax = first.x;
                float yMin = first.y, yMax = first.y;
                for (int i = 5; i <= 7; i++)
                {
                    Vector3 point = _camera.WorldToScreenPoint(
                        skin.transform.TransformPoint(vertices[i]));
                    xMin = Mathf.Min(xMin, point.x);
                    xMax = Mathf.Max(xMax, point.x);
                    yMin = Mathf.Min(yMin, point.y);
                    yMax = Mathf.Max(yMax, point.y);
                }
                return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
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
