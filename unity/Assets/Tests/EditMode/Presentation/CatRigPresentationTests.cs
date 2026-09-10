using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Cats;
using CatMetro.Presentation.Cosmetics;
using CatMetro.Services.Cosmetics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.EditMode.Presentation
{
    public sealed class CatRigPresentationTests
    {
        private const string BodyPath = "Armature/tripo::Root/tripo::Head_0";
        private const string SourcePath = "Assets/Art/Generated/incoming/cat-rig/provider-tripo/candidate-b/"
            + "walk-fbx/tripo-out/cat-metro-walker-b-walk-fbx-6e32e93a/model.fbx";
        private GameObject _host;
        private GameObject _source;
        private string _sourceHash;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_sourceHash != null) Assert.That(HashSource(), Is.EqualTo(_sourceHash));
        }

        [Test]
        public void SyntheticNamedBranchDoesNotReceivePaidMotionOrHeadShape()
        {
            _host = new GameObject("synthetic rig");
            var model = new GameObject("model");
            model.transform.SetParent(_host.transform, false);
            model.AddComponent<Animator>();
            Transform parent = model.transform;
            foreach (string part in CatRigPresentation.WeightedHeadPath.Split('/'))
            {
                var child = new GameObject(part);
                child.transform.SetParent(parent, false);
                parent = child.transform;
            }
            parent.localScale = new Vector3(0.8f, 0.9f, 1.1f);
            Assert.That(CatRigPresentation.TryInstall(_host, _host), Is.Null);
            Assert.That(parent.localScale, Is.EqualTo(new Vector3(0.8f, 0.9f, 1.1f)));
            GameObject clone = Object.Instantiate(_host, _host.transform, false);
            Assert.That(CatRigPresentation.TryInstall(clone, _host), Is.Null,
                "a synthetic catalog entry must preserve its original controller and geometry");
            Assert.That(_host.GetComponentsInChildren<CatRigPresentation>(true), Is.Empty);
        }

        [Test]
        public void LocalPaidCloneInstallsSixStatesAndOriginalWalkWithoutMutatingSource()
        {
            RequireLocalSource();
            Animator original = _source.GetComponentInChildren<Animator>(true);
            RuntimeAnimatorController originalController = original.runtimeAnimatorController;
            Transform originalHead = original.transform.Find(CatRigPresentation.WeightedHeadPath);
            Vector3 originalScale = originalHead.localScale;
            GameObject clone = CloneThroughCatalog();
            Animator animator = clone.GetComponentInChildren<Animator>(true);
            CatRigPresentation motion = clone.GetComponentInChildren<CatRigPresentation>(true);
            Assert.That(motion, Is.Not.Null);
            Assert.That(motion.AuthoredMotionInstalled, Is.True);
            Assert.That(animator.runtimeAnimatorController.animationClips.Select(c => c.name),
                Is.EquivalentTo(new[] { "Cat_IdleSit", "Cat_Walk", "Cat_Board", "Cat_Alight", "Cat_Celebrate", "Cat_Ride" }));
            Assert.That(animator.runtimeAnimatorController.animationClips.Single(c => c.name == "Cat_Walk"),
                Is.SameAs(originalController.animationClips.Single(c => c.name == "Cat_Walk")));
            animator.Rebind();
            foreach (string state in new[] { "Cat_IdleSit", "Cat_Walk", "Cat_Board", "Cat_Alight", "Cat_Celebrate", "Cat_Ride" })
            {
                Assert.That(animator.HasState(0, Animator.StringToHash("Base Layer." + state)), Is.True);
                animator.Play("Base Layer." + state, 0, 0.5f);
                animator.Update(0f);
                motion.ApplyHeadShape();
                Assert.That(motion.HeadTransform.localScale,
                    Is.EqualTo(originalScale * 1.28f), state + " must retain the presentation shape");
                Assert.That(animator.applyRootMotion, Is.False);
            }
            Assert.That(CatRigPresentation.TryInstall(clone, _source), Is.SameAs(motion));
            Assert.That(motion.HeadTransform.localScale, Is.EqualTo(originalScale * 1.28f));
            Assert.That(original.runtimeAnimatorController, Is.SameAs(originalController));
            Assert.That(originalHead.localScale, Is.EqualTo(originalScale));
            Assert.That(_source.GetComponentsInChildren<CatRigPresentation>(true), Is.Empty);
            Assert.That(CatModelCatalog.TryValidate(_source, out string rejection), Is.True, rejection);
        }

        [Test]
        public void LocalPaidHeadShapeChangesWeightedHeadGeometryWhileUnweightedLowerBodyStaysFixed()
        {
            RequireLocalSource();
            GameObject neutral = Object.Instantiate(_source, _host.transform, false);
            GameObject shaped = CloneThroughCatalog();
            ResetToOriginalIdle(neutral);
            ResetToOriginalIdle(shaped);
            CatRigPresentation motion = shaped.GetComponentInChildren<CatRigPresentation>(true);
            motion.ApplyHeadShape();
            Vector3[] before = BakeInAnimator(neutral);
            Vector3[] after = BakeInAnimator(shaped);
            Animator sourceAnimator = neutral.GetComponentInChildren<Animator>(true);
            Transform sourceHead = sourceAnimator.transform.Find(CatRigPresentation.WeightedHeadPath);
            SkinnedMeshRenderer sourceSkin = neutral.GetComponentInChildren<SkinnedMeshRenderer>(true);
            bool[] headBranch = sourceSkin.bones.Select(b => b == sourceHead || b.IsChildOf(sourceHead)).ToArray();
            BoneWeight[] weights = sourceSkin.sharedMesh.boneWeights;
            Assert.That(after.Length, Is.EqualTo(before.Length));
            Assert.That(weights.Length, Is.EqualTo(before.Length));
            float[] headInfluence = weights.Select(w => HeadBranchInfluence(w, headBranch)).ToArray();
            Vector3 headPivot = sourceAnimator.transform.InverseTransformPoint(sourceHead.position);
            float sourceHeadRadius = before.Where((v, i) => headInfluence[i] > 0f)
                .Max(v => Vector3.Distance(v, headPivot));
            // Uniform 28% head expansion moves an entirely head-weighted point by 0.28 * radius.
            // The source head-influenced region supplies that radius; each lower-body vertex
            // may receive only its summed head/descendant fraction of that displacement.
            float sourceHeadDisplacementBound = (CatRigPresentation.HeadScale - 1f) * sourceHeadRadius;
            const float bakeTolerance = 0.00001f;
            int changed = 0, unweightedLower = 0, weakLower = 0, weakLowerMoved = 0;
            int maxUnweightedIndex = -1, maxWeakExcessIndex = -1;
            float maxUnweighted = 0f, maxWeak = 0f, maxWeakInfluence = 0f, maxHead = 0f;
            float maxWeakExcess = float.NegativeInfinity;
            for (int i = 0; i < before.Length; i++)
            {
                float distance = Vector3.Distance(before[i], after[i]);
                if (distance > bakeTolerance) changed++;
                if (before[i].y >= 0.3f)
                {
                    if (headInfluence[i] >= 0.25f) maxHead = Mathf.Max(maxHead, distance);
                    continue;
                }
                if (headInfluence[i] == 0f)
                {
                    unweightedLower++;
                    if (distance > maxUnweighted) { maxUnweighted = distance; maxUnweightedIndex = i; }
                    continue;
                }
                weakLower++;
                if (distance > bakeTolerance) weakLowerMoved++;
                maxWeak = Mathf.Max(maxWeak, distance);
                maxWeakInfluence = Mathf.Max(maxWeakInfluence, headInfluence[i]);
                float excess = distance - headInfluence[i] * sourceHeadDisplacementBound;
                if (excess > maxWeakExcess) { maxWeakExcess = excess; maxWeakExcessIndex = i; }
            }
            Debug.Log($"HEAD_SHAPE_GEOMETRY changed={changed} unweightedLower={unweightedLower} "
                + $"maxUnweighted={maxUnweighted:R} weakLower={weakLower} weakLowerMoved={weakLowerMoved} "
                + $"maxWeak={maxWeak:R} maxWeakInfluence={maxWeakInfluence:R} maxHead={maxHead:R} "
                + $"weakToHeadRatio={maxWeak / maxHead:R} sourceHeadRadius={sourceHeadRadius:R} "
                + $"sourceHeadDisplacementBound={sourceHeadDisplacementBound:R} maxWeakExcess={maxWeakExcess:R}");
            Assert.That(changed, Is.GreaterThan(4000), "the real weighted head must change, not only weak ear controls");
            Assert.That(unweightedLower, Is.GreaterThan(1000));
            Assert.That(maxUnweighted, Is.LessThan(bakeTolerance), "unweighted lower-body vertex " + maxUnweightedIndex);
            Assert.That(weakLowerMoved, Is.GreaterThan(0), "the paid mesh has measurable weak head influence on the body");
            Assert.That(maxWeakExcess, Is.LessThan(bakeTolerance),
                "lower-body vertex exceeds its weighted source-head expansion bound: " + maxWeakExcessIndex);
        }

        [Test]
        public void LocalTrainRideAndMotionOffResetAfterWalkAndOccupantReuse()
        {
            RequireLocalSource();
            var view = ToyTrainView.Create(_host.transform, "authored train", new[] { 0 }, new[] { 1 },
                CatModelCatalog.LoadResources());
            view.SyncSlot(0x0000000100000001L, CatColor.Red);
            Animator animator = view.GetComponentInChildren<Animator>(true);
            CatRigPresentation motion = view.GetComponentInChildren<CatRigPresentation>(true);
            Assert.That(motion.AuthoredMotionInstalled, Is.True);
            Vector3 rootPosition = animator.transform.localPosition;
            Quaternion rootRotation = animator.transform.localRotation;
            view.ApplyPresentation(CatPresentationState.RideIdle, 0f, false);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Cat_Ride"), Is.True);
            view.ApplyPresentation(CatPresentationState.RideIdle, 0f, true);
            Quaternion[] seated = BoneRotations(animator.transform);
            view.ApplyPresentation(CatPresentationState.Walk, 0.2f, false);
            animator.Update(0.31f);
            view.ApplyPresentation(CatPresentationState.RideIdle, 0.4f, true);
            AssertRotations(BoneRotations(animator.transform), seated);
            Assert.That(animator.speed, Is.Zero);
            int samples = view.RigNeutralSampleCount;
            view.ApplyPresentation(CatPresentationState.RideIdle, 4f, true);
            Assert.That(view.RigNeutralSampleCount, Is.EqualTo(samples));
            view.ApplyPresentation(CatPresentationState.Hidden, 5f, true);
            view.SyncSlot(0x0000000100000002L, CatColor.Blue);
            view.ApplyPresentation(CatPresentationState.RideIdle, 6f, true);
            AssertRotations(BoneRotations(animator.transform), seated);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(animator.transform.localPosition, Is.EqualTo(rootPosition));
            Assert.That(animator.transform.localRotation, Is.EqualTo(rootRotation));
            Assert.That(motion.HeadTransform.localScale, Is.EqualTo(motion.SourceHeadScale * 1.28f));
        }

        [Test]
        public void LocalProfileSamplesIdleWithoutAnimatorAndKeepsFitWhileMotionOffResetsAbsolutely()
        {
            RequireLocalSource();
            var cameraObject = new GameObject("profile camera");
            cameraObject.transform.SetParent(_host.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.pixelRect = new Rect(0f, 0f, 600f, 600f);
            var canvasObject = new GameObject("profile canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(_host.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            var holderObject = new GameObject("holder", typeof(RectTransform));
            holderObject.transform.SetParent(canvasObject.transform, false);
            var holder = (RectTransform)holderObject.transform;
            holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = new Vector2(300f, 300f);
            CosmeticPortraitView portrait = CosmeticPortraitView.Create(holder, new PortraitSource(), "portrait");
            ProfileRigMount mount = ProfileRigMount.Create(holder, portrait, CatModelCatalog.LoadResources(), logPrefix: null);
            bool off = false;
            mount.BindMotionOff(() => off);
            Assert.That(mount.Layout(camera), Is.True);
            Assert.That(mount.AnimatorCount, Is.Zero);
            CatRigPresentation motion = mount.PrefabRoot.GetComponentInChildren<CatRigPresentation>(true);
            Transform model = motion.transform;
            Quaternion[] seated = BoneRotations(model);
            Vector3 fit = mount.PrefabRoot.parent.parent.localScale;
            Vector2 cosmeticSize = portrait.RootTransform.sizeDelta;
            Quaternion body = model.Find(BodyPath).localRotation;
            mount.AdvanceTurntable(0.5f);
            Assert.That(Quaternion.Angle(model.Find(BodyPath).localRotation, body), Is.GreaterThan(0.1f));
            Assert.That(mount.PrefabRoot.parent.parent.localScale, Is.EqualTo(fit));
            Assert.That(portrait.RootTransform.sizeDelta, Is.EqualTo(cosmeticSize));
            mount.SetVisible(false);
            Quaternion[] hidden = BoneRotations(model);
            mount.AdvanceTurntable(0.5f);
            AssertRotations(BoneRotations(model), hidden);
            mount.SetVisible(true);
            off = true;
            mount.AdvanceTurntable(0.1f);
            AssertRotations(BoneRotations(model), seated);
            mount.AdvanceTurntable(2f);
            AssertRotations(BoneRotations(model), seated);
            mount.gameObject.SetActive(false);
            mount.gameObject.SetActive(true);
            AssertRotations(BoneRotations(model), seated);
            Assert.That(mount.AnimatorCount, Is.Zero);
        }

        private void RequireLocalSource()
        {
            _source = Resources.Load<GameObject>(CatModelCatalog.ResourcePath);
            if (_source == null) Assert.Ignore("The paid rig is license-local and unavailable in this checkout.");
            Assert.That(Resources.Load<RuntimeAnimatorController>(CatRigPresentation.ControllerResourcePath), Is.Not.Null,
                "run CatRigMotionAuthoring.Build before validating local motion integration");
            _sourceHash = HashSource();
            Assert.That(_sourceHash, Is.EqualTo("9d87464e3954954d5d64e8eb4aee6150a11f9efcdf320a9f82adb96449dca974"));
            _host = new GameObject("local cat presentation test");
        }
        private GameObject CloneThroughCatalog()
        {
            Assert.That(CatModelCatalog.LoadResources().TryInstantiate(_host.transform, out GameObject clone), Is.True);
            return clone;
        }
        private void ResetToOriginalIdle(GameObject instance)
        {
            Animator animator = instance.GetComponentInChildren<Animator>(true);
            animator.runtimeAnimatorController = _source.GetComponentInChildren<Animator>(true).runtimeAnimatorController;
            animator.Rebind();
            animator.Play("Base Layer.Cat_IdleSit", 0, 0f);
            animator.Update(0f);
        }
        private static Vector3[] BakeInAnimator(GameObject instance)
        {
            Transform animator = instance.GetComponentInChildren<Animator>(true).transform;
            SkinnedMeshRenderer skin = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var mesh = new Mesh();
            try
            {
                skin.BakeMesh(mesh, true);
                return mesh.vertices.Select(v => animator.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray();
            }
            finally { Object.DestroyImmediate(mesh); }
        }
        private static float HeadBranchInfluence(BoneWeight weight, bool[] headBranch)
        {
            return (headBranch[weight.boneIndex0] ? weight.weight0 : 0f)
                + (headBranch[weight.boneIndex1] ? weight.weight1 : 0f)
                + (headBranch[weight.boneIndex2] ? weight.weight2 : 0f)
                + (headBranch[weight.boneIndex3] ? weight.weight3 : 0f);
        }
        private static Quaternion[] BoneRotations(Transform root) => root.GetComponentsInChildren<Transform>(true)
            .Where(t => t != root).Select(t => t.localRotation).ToArray();
        private static void AssertRotations(Quaternion[] actual, Quaternion[] expected)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < actual.Length; i++)
                Assert.That(Quaternion.Angle(actual[i], expected[i]), Is.LessThan(0.05f), "bone " + i);
        }
        private static string HashSource()
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", SourcePath))))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        private sealed class PortraitSource : ICosmeticPortraitSource
        {
            public CosmeticPortraitSnapshot CurrentPortrait => new CosmeticPortraitSnapshot("red_tabby", "cat.red_tabby", "", "", "");
            public event Action Changed { add { } remove { } }
            public bool TryGetPortraitAsset(string id, out CosmeticPortraitAssetDefinition asset)
            {
                asset = new CosmeticPortraitAssetDefinition("cat.red_tabby", "cat.red_tabby", "test");
                return id == "cat.red_tabby";
            }
        }
    }
}
