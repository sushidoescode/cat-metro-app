using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatMetro.Bootstrap;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Cats;
using CatMetro.Presentation.Theme;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.EditMode.Presentation
{
    public sealed class CatRigMonoBehaviourProbe : MonoBehaviour
    {
        public static int AwakeCount;

        private void Awake() => AwakeCount++;
    }

    public sealed class CatModelCatalogTests
    {
        [Test]
        public void Task17HandoffContract_UsesThePinnedResourceAndStateLiterals()
        {
            Assert.That(CatModelCatalog.ResourcePath, Is.EqualTo("CatRigs/BoardCatRig"));
            Assert.That(CatModelCatalog.ResourceFacingYaw, Is.EqualTo(180f));
            Assert.That(CatModelCatalog.ResourceCosmeticCatId, Is.EqualTo("red_tabby"));
            Assert.That(CatModelCatalog.IdleSitClip, Is.EqualTo("Cat_IdleSit"));
            Assert.That(CatModelCatalog.WalkClip, Is.EqualTo("Cat_Walk"));
            Assert.That(CatModelCatalog.BoardClip, Is.EqualTo("Cat_Board"));
            Assert.That(CatModelCatalog.AlightClip, Is.EqualTo("Cat_Alight"));
            Assert.That(CatModelCatalog.CelebrateClip, Is.EqualTo("Cat_Celebrate"));
            Assert.That(CatModelCatalog.PresenterScale, Is.EqualTo(0.46725f));
            Assert.That(CatModelCatalog.NormalizedWalkTravelSpeedAtOneX,
                Is.EqualTo(0.238969f));
            Assert.That(CatModelCatalog.WalkTravelSpeedAtOneX,
                Is.EqualTo(CatModelCatalog.NormalizedWalkTravelSpeedAtOneX
                    * CatModelCatalog.PresenterScale));
            Assert.That(CatModelCatalog.EarDeformerPathA, Is.EqualTo(
                "Armature/tripo::Root/tripo::Head_0/tripo::Head_1/tripo::Head_2/bone_4"));
            Assert.That(CatModelCatalog.EarDeformerPathB, Is.EqualTo(
                "Armature/tripo::Root/tripo::Head_0/tripo::Head_1/tripo::Head_2/tripo::Head_3"));
        }

        [Test]
        public void MissingRig_StaysUnadmittedWithAReadBackReason()
        {
            var catalog = new CatModelCatalog(null);

            Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(0));
            Assert.That(catalog.RejectionReason, Is.Not.Empty);
            Assert.That(catalog.TryInstantiate(null, out var instance), Is.False);
            Assert.That(instance, Is.Null);
        }

        [Test]
        public void AdmittedEntry_ReturnsFacingMetadata_WithoutChangingThePrefabRootTransform()
        {
            using (var fixture = new ConformingRigFixture())
            {
                var parent = new GameObject("HomeRigFacing").transform;
                var pinnedPosition = new Vector3(0.031f, -0.017f, 0.044f);
                fixture.Prefab.transform.localPosition = pinnedPosition;
                var entry = new CatModelCatalog.Entry(
                    fixture.Prefab, 173f, "red_tabby");
                var catalog = CatModelCatalog.FromEntry(entry);
                GameObject instance = null;
                try
                {
                    Assert.That(catalog.TryInstantiate(parent, out instance,
                        out CatModelCatalog.Entry admitted), Is.True);
                    Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(1));
                    Assert.That(admitted, Is.SameAs(entry));
                    Assert.That(admitted.FacingYaw, Is.EqualTo(173f));
                    Assert.That(admitted.CosmeticCatId, Is.EqualTo("red_tabby"));
                    Assert.That(instance.transform.parent, Is.SameAs(parent));
                    Assert.That(instance.transform.localPosition, Is.EqualTo(pinnedPosition),
                        "catalog instantiation must preserve the prefab root position");
                    Assert.That(instance.transform.localRotation, Is.EqualTo(Quaternion.identity));
                    Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one));
                }
                finally
                {
                    if (instance != null) Object.DestroyImmediate(instance);
                    Object.DestroyImmediate(parent.gameObject);
                }
            }
        }

        [Test]
        public void ColliderOnRig_RejectsThePrefabRatherThanAdmittingInteractiveDecoration()
        {
            var prefab = new GameObject("invalid cat rig");
            prefab.AddComponent<BoxCollider>();
            try
            {
                var catalog = new CatModelCatalog(prefab);

                Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(0));
                Assert.That(catalog.RejectionReason, Does.Contain("Collider"));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [TestCase("BoxCollider2D")]
        [TestCase("Rigidbody2D")]
        public void TwoDimensionalPhysicsOnRig_IsRejectedAsInteractiveDecoration(string component)
        {
            var prefab = new GameObject("invalid 2D physics cat rig");
            if (component == "BoxCollider2D") prefab.AddComponent<BoxCollider2D>();
            else prefab.AddComponent<Rigidbody2D>();
            try
            {
                var catalog = new CatModelCatalog(prefab);

                Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(0));
                Assert.That(catalog.RejectionReason, Does.Contain(component));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void MonoBehaviourOnOtherwiseConformingRig_IsRejectedBeforeInstantiation()
        {
            using (var fixture = new ConformingRigFixture())
            {
                fixture.Prefab.AddComponent<CatRigMonoBehaviourProbe>();
                CatRigMonoBehaviourProbe.AwakeCount = 0;

                var catalog = new CatModelCatalog(fixture.Prefab);
                bool instantiated = catalog.TryInstantiate(null, out GameObject instance);
                if (instance != null) Object.DestroyImmediate(instance);

                Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(0));
                Assert.That(catalog.RejectionReason, Does.Contain("MonoBehaviour"));
                Assert.That(instantiated, Is.False);
                Assert.That(CatRigMonoBehaviourProbe.AwakeCount, Is.EqualTo(0),
                    "catalog admission must not clone and awaken an external rig script");
            }
        }

        [Test]
        public void StateMachineBehaviour_IsRejectedBeforeAnyAnimatorSamplingCallback()
        {
            using (var fixture = new ConformingRigFixture(addStateBehaviour: true))
            {
                Assert.That(fixture.StateBehaviour,
                    Is.TypeOf<CatRigStateBehaviourProbe>(),
                    "the negative fixture must attach a resolvable MonoScript");
                CatRigStateBehaviourProbe.StateEnterCount = 0;

                var catalog = new CatModelCatalog(fixture.Prefab);

                Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(0));
                Assert.That(catalog.RejectionReason, Does.Contain("StateMachineBehaviour"));
                Assert.That(CatRigStateBehaviourProbe.StateEnterCount, Is.EqualTo(0),
                    "catalog validation must reject controller callbacks before Rebind/Update");
            }
        }

        [Test]
        public void MatchingClipsWithWrongStateNames_RejectTheRigBeforeItCanSilentlyFailPlayback()
        {
            var prefab = new GameObject("rig with wrongly named states");
            var clips = new[]
            {
                Clip(CatModelCatalog.IdleSitClip),
                Clip(CatModelCatalog.WalkClip),
                Clip(CatModelCatalog.BoardClip),
                Clip(CatModelCatalog.AlightClip),
                Clip(CatModelCatalog.CelebrateClip),
            };
            var controller = new AnimatorController();
            controller.AddLayer("Base Layer");
            foreach (var clip in clips)
                controller.layers[0].stateMachine.AddState("wrong_" + clip.name).motion = clip;
            var animator = prefab.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            try
            {
                var catalog = new CatModelCatalog(prefab);

                Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(0));
                Assert.That(catalog.RejectionReason, Does.Contain("state"));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(controller);
                foreach (var clip in clips) Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void SwappedWalkAndBoardStateMotions_AreRejectedDespiteCompleteLiteralSets()
        {
            using (var fixture = new ConformingRigFixture(swapWalkAndBoard: true))
            {
                var catalog = new CatModelCatalog(fixture.Prefab);

                Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(0));
                Assert.That(catalog.RejectionReason, Does.Contain("Cat_Walk"));
                Assert.That(catalog.RejectionReason, Does.Contain("clip"));
            }
        }

        [TestCase("Cat_IdleSit")]
        [TestCase("Cat_Walk")]
        [TestCase("Cat_Board")]
        [TestCase("Cat_Alight")]
        [TestCase("Cat_Celebrate")]
        public void EmptyMappedRequiredClip_IsRejectedDespiteAnAnimatedSameNamedDecoy(
            string requiredState)
        {
            using (var fixture = new ConformingRigFixture(
                emptyMappedClipName: requiredState))
            {
                var catalog = new CatModelCatalog(fixture.Prefab);

                Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(0));
                Assert.That(catalog.RejectionReason, Does.Contain(requiredState));
                Assert.That(catalog.RejectionReason, Does.Contain("positive-length"));
            }
        }

        [Test]
        public void NonemptyZeroLengthMappedClip_IsRejectedByThePositiveLengthGate()
        {
            using (var fixture = new ConformingRigFixture(
                zeroLengthMappedClipName: CatModelCatalog.BoardClip))
            {
                AnimationClip clip = fixture.ClipNamed(CatModelCatalog.BoardClip);
                Assert.That(clip.empty, Is.False,
                    "this fixture must isolate length from the separate empty-clip check");
                Assert.That(clip.length, Is.Zero);
                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
                Assert.That(bindings.Length, Is.EqualTo(3),
                    "the zero-length negative case keeps TASK 17's XYZ packing");
                foreach (EditorCurveBinding binding in bindings)
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    Assert.That(curve.keys.Length, Is.EqualTo(1), binding.propertyName);
                    Assert.That(curve.keys[0].time, Is.Zero, binding.propertyName);
                }

                var catalog = new CatModelCatalog(fixture.Prefab);

                Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(0));
                Assert.That(catalog.RejectionReason, Does.Contain(CatModelCatalog.BoardClip));
                Assert.That(catalog.RejectionReason, Does.Contain("positive-length"));
            }
        }

        [Test]
        public void PositiveLengthBindPoseFallbackShape_IsAdmittedByTheStrictGate()
        {
            using (var fixture = new ConformingRigFixture())
            {
                Assert.That(fixture.Prefab.transform.Find("Armature").localPosition,
                    Is.EqualTo(new Vector3(0f, 0.4853515f, 0f)),
                    "the synthetic curves must preserve TASK 17's measured bind position");
                string[] fallbackNames =
                {
                    CatModelCatalog.IdleSitClip,
                    CatModelCatalog.BoardClip,
                    CatModelCatalog.AlightClip,
                    CatModelCatalog.CelebrateClip,
                };
                foreach (string fallbackName in fallbackNames)
                {
                    AnimationClip clip = fixture.ClipNamed(fallbackName);
                    Assert.That(clip.empty, Is.False, fallbackName);
                    Assert.That(clip.length,
                        Is.EqualTo(1f / 24f).Within(0.000001f), fallbackName);
                    EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
                    Assert.That(bindings.Length, Is.EqualTo(3), fallbackName);
                    CollectionAssert.AreEquivalent(new[]
                    {
                        "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
                    }, Array.ConvertAll(bindings, binding => binding.propertyName), fallbackName);
                    foreach (EditorCurveBinding binding in bindings)
                    {
                        Assert.That(binding.path, Is.EqualTo("Armature"), fallbackName);
                        Assert.That(binding.type, Is.EqualTo(typeof(Transform)), fallbackName);
                        AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                        Assert.That(curve.keys.Length, Is.EqualTo(1), fallbackName);
                        Assert.That(curve.keys[0].time,
                            Is.EqualTo(1f / 24f).Within(0.000001f), fallbackName);
                        float expectedValue = binding.propertyName == "m_LocalPosition.y"
                            ? 0.4853515f : 0f;
                        Assert.That(curve.keys[0].value,
                            Is.EqualTo(expectedValue).Within(0.000001f), fallbackName);
                    }
                }

                var catalog = new CatModelCatalog(fixture.Prefab);

                Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(1),
                    catalog.RejectionReason);
            }
        }

        [Test]
        public void FloatNoiseCenteredPivot_IsAdmittedWithinExplicitTolerance()
        {
            const float floatNoise = -2.98023224e-08f;
            using (var fixture = new ConformingRigFixture(
                       centerX: floatNoise, centerZ: -floatNoise))
            {
                MeshFilter body = fixture.Prefab.transform.Find("RigBody")
                    .GetComponent<MeshFilter>();
                Bounds authored = BoundsIn(fixture.Prefab.transform, body);
                Assert.That(authored.center.x, Is.Not.Zero,
                    "the regression fixture must retain the measured X residue");
                Assert.That(authored.center.z, Is.Not.Zero,
                    "the regression fixture must retain the measured Z residue");
                Assert.That(Mathf.Abs(authored.center.x), Is.LessThanOrEqualTo(0.0001f));
                Assert.That(Mathf.Abs(authored.center.z), Is.LessThanOrEqualTo(0.0001f));

                var catalog = new CatModelCatalog(fixture.Prefab);

                Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(1),
                    catalog.RejectionReason);
            }
        }

        [TestCase(0.01f, 0f)]
        [TestCase(0f, 0.01f)]
        public void GenuinelyOffCenterPivot_IsRejected(float centerX, float centerZ)
        {
            using (var fixture = new ConformingRigFixture(
                       centerX: centerX, centerZ: centerZ))
            {
                var catalog = new CatModelCatalog(fixture.Prefab);

                Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(0));
                Assert.That(catalog.RejectionReason, Does.Contain("ground-centred"));
            }
        }

        [Test]
        public void ConformingRig_IsAdmittedAndToyTrainMapsItsAxesScaleAndRequiredPlayback()
        {
            using (var fixture = new ConformingRigFixture())
            {
                var catalog = new CatModelCatalog(fixture.Prefab);
                Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(1), catalog.RejectionReason);

                var host = new GameObject("conforming-rig-train-host");
                try
                {
                    var view = ToyTrainView.Create(host.transform, "train:rig",
                        new[] { 0 }, new[] { 1 }, catalog);
                    view.SyncSlot(0x0000000100000001L, CatColor.Red);

                    Assert.That(view.RigAdmitted, Is.True, view.RigFallbackReason);
                    Transform cat = view.transform.Find("Carriage/Cat");
                    Animator[] animators = cat.GetComponentsInChildren<Animator>(true);
                    Assert.That(animators.Length, Is.EqualTo(1));
                    Assert.That(animators[0].applyRootMotion, Is.False);

                    Transform rig = animators[0].transform;
                    Assert.That(rig.localScale,
                        Is.EqualTo(Vector3.one * CatModelCatalog.PresenterScale));
                    AssertDirection(rig.localRotation * Vector3.up, Vector3.back,
                        "imported +Y must become cat/tabletop up (-Z)");
                    AssertDirection(rig.localRotation * Vector3.forward, Vector3.right,
                        "imported +Z must become the cat's +X facing axis");
                    AssertDirection(rig.TransformDirection(Vector3.up),
                        cat.TransformDirection(Vector3.back),
                        "world-space imported up must stand away from the tabletop");
                    AssertDirection(rig.TransformDirection(Vector3.forward),
                        cat.TransformDirection(Vector3.right),
                        "world-space imported forward must face with the cat");
                    Assert.That(cat.Find("Head").GetComponent<MeshRenderer>().enabled, Is.False);
                    Assert.That(cat.Find("EyeLeft").GetComponent<MeshRenderer>().enabled, Is.False);
                    Renderer[] rigRenderers = rig.GetComponentsInChildren<Renderer>(true);
                    Assert.That(rigRenderers, Has.Length.EqualTo(2),
                        "the fixture must exercise production's all-renderers tint loop");
                    AssertTints(rigRenderers, CatLine.ColorOf("red"),
                        "the admitted rig must inherit the authoritative cat-line tint");
                    view.SyncSlot(0x0000000100000002L, CatColor.Blue);
                    AssertTints(rigRenderers, CatLine.ColorOf("blue"),
                        "occupant reuse must retint the admitted rig, not retain its old line");

                    Bounds standing = BoundsIn(cat,
                        rig.GetComponentInChildren<MeshFilter>(true));
                    Assert.That(standing.min.z,
                        Is.EqualTo(-CatModelCatalog.PresenterScale).Within(0.0001f));
                    Assert.That(standing.max.z, Is.EqualTo(0f).Within(0.0001f));
                    Assert.That(standing.size.z,
                        Is.EqualTo(CatModelCatalog.PresenterScale).Within(0.0001f));

                    AssertPlays(view, animators[0], CatPresentationState.WaitingIdle,
                        "Base Layer.Cat_IdleSit");
                    AssertPlays(view, animators[0], CatPresentationState.Walk,
                        "Base Layer.Cat_Walk");
                    AssertPlays(view, animators[0], CatPresentationState.Board,
                        "Base Layer.Cat_Board");
                    AssertPlays(view, animators[0], CatPresentationState.Alight,
                        "Base Layer.Cat_Alight");
                    AssertPlays(view, animators[0], CatPresentationState.Celebrate,
                        "Base Layer.Cat_Celebrate");
                }
                finally
                {
                    Object.DestroyImmediate(host);
                }
            }
        }

        [Test]
        public void ResourcesRig_MeasuredEarBranchesCarryTierOneTwitchWithoutAPlaybackDependency()
        {
            GameObject prefab = Resources.Load<GameObject>(CatModelCatalog.ResourcePath);
            if (prefab == null)
                Assert.Ignore("The licensed local rig is absent; run this in the combined asset workspace.");

            var catalog = new CatModelCatalog(prefab);
            Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(1), catalog.RejectionReason);
            TestContext.Out.WriteLine("CAT_RIG_CATALOG_READBACK AdmittedEntryCount="
                + catalog.AdmittedEntryCount);
            var host = new GameObject("measured-ear-rig-host");
            Mesh neutralMesh = null;
            Mesh twitchMesh = null;
            try
            {
                var view = ToyTrainView.Create(host.transform, "train:measured-ears",
                    new[] { 0 }, new[] { 1 }, catalog);
                view.SyncSlot(41L, CatColor.Red);
                Animator animator = view.GetComponentInChildren<Animator>(true);
                Transform branchA = animator.transform.Find(CatModelCatalog.EarDeformerPathA);
                Transform branchB = animator.transform.Find(CatModelCatalog.EarDeformerPathB);
                Assert.That(branchA, Is.Not.Null, "TASK 17's first measured ear path must resolve");
                Assert.That(branchB, Is.Not.Null, "TASK 17's second measured ear path must resolve");

                SkinnedMeshRenderer[] skins =
                    animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.That(skins, Has.Length.EqualTo(1));
                Assert.That(skins[0].bones, Has.Length.EqualTo(30),
                    "the admitted artifact is TASK 17's measured 30-bone skin");
                Assert.That(Array.IndexOf(skins[0].bones, branchA), Is.GreaterThanOrEqualTo(0));
                Assert.That(Array.IndexOf(skins[0].bones, branchB), Is.GreaterThanOrEqualTo(0));
                Assert.That(view.RigEarTwitchSupported, Is.True,
                    "Tier 1 must bind only after both measured branches belong to the skin");

                view.ApplyPresentation(CatPresentationState.RideIdle, 0f, true);
                Quaternion neutralA = branchA.localRotation;
                Quaternion neutralB = branchB.localRotation;
                neutralMesh = new Mesh();
                // useScale=true compensates the renderer Transform scale. The baked vertices
                // remain renderer-local, so TransformPoint below then applies the admitted
                // 0.42 presentation hierarchy exactly once.
                skins[0].BakeMesh(neutralMesh, true);

                float sampleTime = TimeWithLargeEarTwitch(41u);
                CatMicroPose pose = new CatMicroMotion(41u).Evaluate(sampleTime, false, false);
                view.ApplyPresentation(CatPresentationState.RideIdle, sampleTime, false);
                Assert.That(Quaternion.Angle(branchA.localRotation,
                    neutralA * Quaternion.Euler(0f, 0f,
                        pose.EarTwitchDegrees * ToyTrainView.RigEarTwitchGain)),
                    Is.LessThan(0.01f));
                Assert.That(Quaternion.Angle(branchB.localRotation,
                    neutralB * Quaternion.Euler(0f, 0f,
                        -pose.EarTwitchDegrees * ToyTrainView.RigEarTwitchGain)),
                    Is.LessThan(0.01f));

                twitchMesh = new Mesh();
                skins[0].BakeMesh(twitchMesh, true);
                AssertLocalizedUpperHeadDeformation(neutralMesh, twitchMesh,
                    skins[0].transform, animator.transform);
            }
            finally
            {
                if (neutralMesh != null) Object.DestroyImmediate(neutralMesh);
                if (twitchMesh != null) Object.DestroyImmediate(twitchMesh);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        // The 60-level corpus took 251s in the slot with the licensed rig.
        [Timeout(600000)]
        public void ResourcesRig_ReleasedLaneEnvelopeAndCurrentEndpointCasesClearTheCarriage()
        {
            GameObject prefab = Resources.Load<GameObject>(CatModelCatalog.ResourcePath);
            if (prefab == null)
                Assert.Ignore("The licensed local rig is absent; run this in the combined asset workspace.");

            var catalog = new CatModelCatalog(prefab);
            Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(1), catalog.RejectionReason);
            var host = new GameObject("measured-platform-clearance-host");
            var baked = new Mesh { name = "measured-platform-clearance-snapshot" };
            try
            {
                var view = ToyTrainView.Create(host.transform, "train:rig-clearance",
                    new[] { 0 }, new[] { 1 }, catalog);
                view.SyncSlot(41L, CatColor.Red);
                Transform carriage = view.transform.Find("Carriage");
                Transform cat = carriage.Find("Cat");
                Animator animator = cat.GetComponentInChildren<Animator>(true);
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                SkinnedMeshRenderer[] skins =
                    animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.That(skins, Has.Length.EqualTo(1),
                    "the pinned licensed artifact must expose its one measured skin");
                AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
                AnimationClip walk = clips.Single(
                    clip => clip.name == CatModelCatalog.WalkClip);
                int walkHalfFrameSamples = Mathf.CeilToInt(
                    walk.length * walk.frameRate * 2f);
                Assert.That(walkHalfFrameSamples, Is.GreaterThanOrEqualTo(150),
                    "the probe must sample the complete licensed walk at half-frame spacing");
                float maximumBobTime;
                float minimumBobTime;
                float[] visualTimes = TimesAcrossEarRange(
                    41u, out maximumBobTime, out minimumBobTime);
                var motion = new CatMicroMotion(41u);
                for (int index = 0; index < visualTimes.Length; index++)
                {
                    float targetEar = Mathf.Lerp(-CatMicroMotion.EarTwitchMaximumDegrees,
                        CatMicroMotion.EarTwitchMaximumDegrees,
                        index / (float)(visualTimes.Length - 1));
                    CatMicroPose pose = motion.Evaluate(visualTimes[index], false, false);
                    Assert.That(pose.EarTwitchDegrees,
                        Is.EqualTo(targetEar).Within(0.03f),
                        "the clearance corpus must cover both signs and intermediate ear angles");
                }
                Assert.That(motion.Evaluate(maximumBobTime, false, false).Bob,
                    Is.GreaterThanOrEqualTo(0.99999f),
                    "positive bob extreme must come from the production micro-motion artifact");
                Assert.That(motion.Evaluate(minimumBobTime, false, false).Bob,
                    Is.LessThanOrEqualTo(-0.99999f),
                    "negative bob extreme must come from the production micro-motion artifact");

                string levelsDir = Path.Combine(UnityEngine.Application.streamingAssetsPath,
                    "content", "levels");
                string[] levelPaths = Directory.GetFiles(levelsDir, "L*.json")
                    .Where(path => Path.GetExtension(path) == ".json")
                    .ToArray();
                Assert.That(levelPaths.Length, Is.EqualTo(GameRoot.LevelBand.Length),
                    "the staged corpus must contain every shipped level in the level band");
                ImportedLevel[] authoredLevels = levelPaths
                    .Select(path => LevelImporter.Import(File.ReadAllBytes(path)))
                    .Select(import =>
                    {
                        Assert.That(import.Ok, Is.True,
                            import.Ok ? string.Empty : import.Error.ToString());
                        return import.Value;
                    })
                    .ToArray();
                int maximumAuthoredTrains = authoredLevels
                    .Max(level => level.Graph.TrainsMax);
                Assert.That(maximumAuthoredTrains, Is.GreaterThan(0),
                    "the staged authored corpus must expose a positive TrainsMax ceiling");

                float minimumGap = float.PositiveInfinity;
                string minimumLabel = string.Empty;
                Vector3 seatBoard = host.transform.InverseTransformPoint(cat.position);
                var states = new List<CatPresentationState>();
                var sourceSides = new List<Vector3>();
                var queuePositions = new List<int>();
                var movingToPlatforms = new List<bool>();
                var endpointLabels = new List<string>();
                const int maximumAuthoredWaitingQueuePosition = 3;
                var waitingSourceSides = new[]
                {
                    Vector3.left,
                    new Vector3(-1f, -1f, 0f).normalized,
                    new Vector3(-1f, 1f, 0f).normalized,
                };
                var waitingSourceLabels = new[]
                {
                    "straight-source",
                    "diagonal-source-a",
                    "diagonal-source-b",
                };
                for (int queuePosition = 0;
                    queuePosition < maximumAuthoredTrains;
                    queuePosition++)
                {
                    // Once a released source cat reaches its outgoing edge, the carriage and
                    // source tangent rotate together and BoardView's side is carriage-local -Y.
                    states.Add(CatPresentationState.Walk);
                    sourceSides.Add(Vector3.down);
                    queuePositions.Add(queuePosition);
                    movingToPlatforms.Add(false);
                    endpointLabels.Add("released/lane=" + queuePosition);
                }
                int releasedLaneCount = states.Count;
                Assert.That(releasedLaneCount,
                    Is.GreaterThanOrEqualTo(maximumAuthoredTrains),
                    "the released lane sweep must cover the staged corpus TrainsMax ceiling "
                    + maximumAuthoredTrains);
                // A source-blocked cat is still parked in the fresh heading-zero carriage.
                // L005/L011/L012/L013 reach lanes 0..3 at their straight sources; the two
                // diagonal source families in L018/L019 can expose lanes 0..1 on a catch-up
                // frame before the presentation-side allocator observes the first release.
                for (int queuePosition = 0;
                    queuePosition <= maximumAuthoredWaitingQueuePosition;
                    queuePosition++)
                {
                    states.Add(CatPresentationState.WaitingIdle);
                    sourceSides.Add(waitingSourceSides[0]);
                    queuePositions.Add(queuePosition);
                    movingToPlatforms.Add(false);
                    endpointLabels.Add("waiting/" + waitingSourceLabels[0]
                        + "/lane=" + queuePosition);
                }
                const int maximumDiagonalWaitingQueuePosition = 1;
                for (int side = 1; side < waitingSourceSides.Length; side++)
                {
                    for (int queuePosition = 0;
                        queuePosition <= maximumDiagonalWaitingQueuePosition;
                        queuePosition++)
                    {
                        states.Add(CatPresentationState.WaitingIdle);
                        sourceSides.Add(waitingSourceSides[side]);
                        queuePositions.Add(queuePosition);
                        movingToPlatforms.Add(false);
                        endpointLabels.Add("waiting/" + waitingSourceLabels[side]
                            + "/lane=" + queuePosition);
                    }
                }
                states.Add(CatPresentationState.Walk);
                sourceSides.Add(Vector3.down);
                queuePositions.Add(-1);
                movingToPlatforms.Add(true);
                endpointLabels.Add("departure-walk");

                Assert.That(states.Count,
                    Is.EqualTo(maximumAuthoredTrains
                        + maximumAuthoredWaitingQueuePosition + 6));
                for (int endpoint = 0; endpoint < states.Count; endpoint++)
                {
                    CatPresentationState state = states[endpoint];
                    Vector3 sourceSide = sourceSides[endpoint];
                    int queuePosition = queuePositions[endpoint];
                    bool movingToPlatform = movingToPlatforms[endpoint];
                    if (!movingToPlatform)
                        view.SetSourcePlatformAnchor(seatBoard, sourceSide, queuePosition);
                    MeasureRigClearanceCase(view, host.transform, baked, state,
                        movingToPlatform, sourceSide, maximumBobTime, minimumBobTime,
                        visualTimes, endpointLabels[endpoint],
                        ref minimumGap, ref minimumLabel);
                }
                Assert.That(minimumLabel, Is.Not.Empty,
                    "the released endpoint clearance sweep must measure at least one rig pose");

                int stationArrivalCases = 0;
                int expectedStationArrivalCases = authoredLevels.Sum(level =>
                    level.Graph.EdgeTo.Count(target =>
                        level.Graph.StationNode.Contains(target)));
                var uniqueStationHeadings = new List<float>();
                foreach (ImportedLevel level in authoredLevels)
                {
                    NodeDto[] levelNodes = level.Dto.Nodes.ToArray();
                    Vector3[] nodePositions = levelNodes
                        .Select(node => new Vector3(node.X, node.Y, 0f))
                        .ToArray();
                    TrackSplineGraph paths = TrackSplineGraph.Build(nodePositions,
                        level.Graph.EdgeFrom, level.Graph.EdgeTo);
                    ToyTrainView celebrateView = ToyTrainView.Create(host.transform,
                        "train:celebrate-clearance:" + level.Graph.LevelId,
                        level.Graph.EdgeFrom, level.Graph.EdgeTo, catalog);
                    try
                    {
                        for (int edge = 0; edge < level.Graph.EdgeFrom.Length; edge++)
                        {
                            int stationNode = level.Graph.EdgeTo[edge];
                            if (!level.Graph.StationNode.Contains(stationNode)) continue;
                            stationArrivalCases++;
                            celebrateView.SyncSlot(PresentationKeyForSeed(
                                41u, (uint)stationArrivalCases), CatColor.Red);
                            celebrateView.PlaceOnEdge(paths, edge, paths.Path(edge).Length);
                            celebrateView.PlaceAtNode(paths, stationNode,
                                nodePositions[stationNode]);
                            Transform celebrateCarriage = celebrateView.transform
                                .Find("Carriage");
                            float heading = Mathf.DeltaAngle(0f,
                                celebrateCarriage.localEulerAngles.z);
                            if (!uniqueStationHeadings.Any(existing =>
                                Mathf.Abs(Mathf.DeltaAngle(existing, heading)) < 0.001f))
                                uniqueStationHeadings.Add(heading);
                            foreach (CatPresentationState departureState in new[]
                                {
                                    CatPresentationState.Walk,
                                    CatPresentationState.Celebrate,
                                })
                                MeasureRigClearanceCase(celebrateView, host.transform, baked,
                                    departureState, true, Vector3.down,
                                    maximumBobTime, minimumBobTime, visualTimes,
                                    level.Graph.LevelId + "/departure-" + departureState
                                        + "/edge=" + edge
                                        + "/heading=" + heading.ToString("F3"),
                                    ref minimumGap, ref minimumLabel);
                        }
                    }
                    finally
                    {
                        Object.DestroyImmediate(celebrateView.gameObject);
                    }
                }
                Assert.That(stationArrivalCases, Is.EqualTo(expectedStationArrivalCases),
                    "every station-arrival edge in the staged corpus must be sampled");
                Assert.That(uniqueStationHeadings.Count, Is.GreaterThan(1),
                    "the staged corpus must exercise multiple station carriage headings");

                const int retainedHeadingStepDegrees = 5;
                int retainedHeadingSamples = 0;
                var retainedView = ToyTrainView.Create(host.transform,
                    "train:retained-heading-clearance", new[] { 0 }, new[] { 1 }, catalog);
                try
                {
                    for (int heading = 0; heading < 360;
                        heading += retainedHeadingStepDegrees)
                    {
                        retainedHeadingSamples++;
                        Vector3 direction = Quaternion.Euler(0f, 0f, heading)
                            * Vector3.right * 2f;
                        var headingPaths = TrackSplineGraph.Build(
                            new[] { Vector3.zero, direction }, new[] { 0 }, new[] { 1 });
                        retainedView.SyncSlot(PresentationKeyForSeed(
                            41u, (uint)(stationArrivalCases + retainedHeadingSamples)),
                            CatColor.Red);
                        retainedView.PlaceOnEdge(headingPaths, 0,
                            headingPaths.Path(0).Length);
                        // A collapsed catch-up can park at a foreign node while retaining the
                        // last rendered heading. This is the production clamp, not a direct
                        // transform mutation; five-degree intervals sample that retained state
                        // around the full circle without claiming a continuous proof.
                        retainedView.PlaceAtNode(headingPaths, 0, Vector3.zero);
                        foreach (CatPresentationState departureState in new[]
                            {
                                CatPresentationState.Walk,
                                CatPresentationState.Celebrate,
                            })
                            MeasureRigClearanceCase(retainedView, host.transform, baked,
                                departureState, true, Vector3.down,
                                maximumBobTime, minimumBobTime, visualTimes,
                                "retained-heading/" + heading + "/" + departureState,
                                ref minimumGap, ref minimumLabel);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(retainedView.gameObject);
                }
                Assert.That(retainedHeadingSamples, Is.EqualTo(72),
                    "72 five-degree samples must span the retained-heading circle");

                TestContext.Out.WriteLine("CAT_RIG_PLATFORM_CLEARANCE_READBACK offset="
                    + ToyTrainView.PlatformSideOffset.ToString("F6")
                    + " minimumGap=" + minimumGap.ToString("F6")
                    + " releasedMaximumQueuePosition=" + (maximumAuthoredTrains - 1)
                    + " straightWaitingMaximumQueuePosition="
                        + maximumAuthoredWaitingQueuePosition
                    + " diagonalWaitingMaximumQueuePosition="
                        + maximumDiagonalWaitingQueuePosition
                    + " endpointSamples=" + (states.Count + 2 * stationArrivalCases
                        + 2 * retainedHeadingSamples)
                    + " stationArrivalCases=" + stationArrivalCases
                    + " uniqueStationHeadings=" + uniqueStationHeadings.Count
                    + " retainedHeadingSamples=" + retainedHeadingSamples
                    + " departureStates=2"
                    + " earSamples=" + visualTimes.Length
                    + " sample=" + minimumLabel);
                const float requiredMinimumGap = 0.045f;
                Assert.That(ToyTrainView.PlatformEndpointClearance,
                    Is.GreaterThanOrEqualTo(requiredMinimumGap),
                    "the production separating-plane clearance must not drop below 0.045");
                Assert.That(minimumGap,
                    Is.GreaterThanOrEqualTo(ToyTrainView.PlatformEndpointClearance),
                    $"{minimumLabel} leaves only {minimumGap:F4} board units; the licensed "
                    + "skin must retain the declared separating-plane clearance across the "
                    + "released TrainsMax lane envelope, straight waiting lanes 0..3, both "
                    + "diagonal waiting normals at lanes 0..1, actual station arrivals, the "
                    + "five-degree retained-heading envelope, half-frame active clips and the "
                    + "17-angle ear corpus translated to maximum carriage-ward bob");
            }
            finally
            {
                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ResourcesRig_NoLocalizedEyeControlPinsBlinkToHudAndPlaceholderCats()
        {
            GameObject prefab = Resources.Load<GameObject>(CatModelCatalog.ResourcePath);
            if (prefab == null)
                Assert.Ignore("The licensed local rig is absent; run this in the combined asset workspace.");

            var catalog = new CatModelCatalog(prefab);
            Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(1), catalog.RejectionReason);
            var host = new GameObject("measured-blink-policy-host");
            try
            {
                var rigView = ToyTrainView.Create(host.transform, "train:rig-blink-policy",
                    new[] { 0 }, new[] { 1 }, catalog);
                rigView.SyncSlot(41L, CatColor.Red);
                Animator animator = rigView.GetComponentInChildren<Animator>(true);
                foreach (SkinnedMeshRenderer skin in
                    animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    Assert.That(skin.sharedMesh.blendShapeCount, Is.EqualTo(0),
                        "the admitted rig has no blendshape that could localize a blink");
                foreach (Transform candidate in animator.GetComponentsInChildren<Transform>(true))
                {
                    string lower = candidate.name.ToLowerInvariant();
                    Assert.That(lower.Contains("eye") || lower.Contains("lid"), Is.False,
                        "the admitted rig has no independently named eye/lid transform");
                }
                Assert.That(rigView.RigBlinkSupported, Is.False,
                    "without a localized control, broad face deformation is forbidden: " +
                    "board-rig blink is deferred while HUD and placeholder blink remain");
                Assert.That(rigView.transform.Find("Carriage/Cat/EyeLeft")
                    .GetComponent<MeshRenderer>().enabled, Is.False,
                    "admission hides the placeholder eyes rather than layering them over the rig");

                var placeholderView = ToyTrainView.Create(host.transform,
                    "train:placeholder-blink", new[] { 0 }, new[] { 1 },
                    new CatModelCatalog(null));
                placeholderView.SyncSlot(41L, CatColor.Red);
                Transform eye = placeholderView.transform.Find("Carriage/Cat/EyeLeft");
                float neutralEyeY = eye.localScale.y;
                placeholderView.ApplyPresentation(CatPresentationState.RideIdle,
                    TimeWithBlink(41u), false);
                Assert.That(eye.GetComponent<MeshRenderer>().enabled, Is.True);
                Assert.That(eye.localScale.y, Is.LessThan(neutralEyeY * 0.2f),
                    "the explicit deferral must not remove blink from placeholder board cats");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void WalkPlayback_UsesActualPlatformPathAndRetimesTheSameState()
        {
            using (var fixture = new ConformingRigFixture())
            {
                var host = new GameObject("walk-speed-rig-host");
                try
                {
                    host.transform.localScale = new Vector3(2f, 3f, 1f);
                    var view = ToyTrainView.Create(host.transform, "train:rig",
                        new[] { 0 }, new[] { 1 }, new CatModelCatalog(fixture.Prefab));
                    view.SyncSlot(0x0000000100000001L, CatColor.Red);
                    Animator animator = view.GetComponentInChildren<Animator>(true);
                    Transform cat = view.transform.Find("Carriage/Cat");
                    Vector3 seatBoard = host.transform.InverseTransformPoint(cat.position);
                    view.SetSourcePlatformAnchor(seatBoard, Vector3.down, 0);

                    const float laneZeroBlend = 0.8f;
                    view.ApplyPresentation(CatPresentationState.Walk, laneZeroBlend, false,
                        0f, false, 1f);
                    float laneZeroPath = ToyTrainView.PlatformSideOffset;
                    Assert.That(animator.speed,
                        Is.EqualTo(laneZeroPath / CatModelCatalog.WalkTravelSpeedAtOneX)
                            .Within(0.0001f),
                        "walk playback and the collision-safe lane-zero platform endpoint "
                        + "must remain one board-space distance law");

                    view.SetSourcePlatformAnchor(seatBoard, Vector3.down, 3);
                    const float laneThreeBlend = 0.75f;
                    view.ApplyPresentation(CatPresentationState.Walk, laneThreeBlend, false,
                        0.005f, false, 1f);
                    float laneThreePath = Mathf.Sqrt(
                        ToyTrainView.PlatformSideOffset * ToyTrainView.PlatformSideOffset
                        + Mathf.Pow(2f * ToyTrainView.PlatformQueueSpacing, 2f));
                    Assert.That(animator.speed,
                        Is.EqualTo(laneThreePath / CatModelCatalog.WalkTravelSpeedAtOneX)
                            .Within(0.0001f),
                        "queued playback must combine authored queue spacing with the same "
                        + "platform endpoint law");

                    view.ApplyPresentation(CatPresentationState.Walk, 0.7f, false,
                        0.01f, false, 0.5f);
                    Assert.That(animator.speed,
                        Is.EqualTo(laneThreePath * 0.5f
                            / CatModelCatalog.WalkTravelSpeedAtOneX).Within(0.0001f),
                        "speed changes within one Walk state must not be skipped");

                    view.ApplyPresentation(CatPresentationState.Board, 0.35f, false,
                        0.02f, false, 10f);
                    Assert.That(animator.speed, Is.EqualTo(1f),
                        "desired travel speed affects only the in-place Walk clip");
                }
                finally
                {
                    Object.DestroyImmediate(host);
                }
            }
        }

        [Test]
        public void MotionOff_FreezesIdleSitAndResamplesOnlyWhenSuppressionChanges()
        {
            using (var fixture = new ConformingRigFixture())
            {
                var host = new GameObject("motion-off-rig-host");
                try
                {
                    var view = ToyTrainView.Create(host.transform, "train:rig",
                        new[] { 0 }, new[] { 1 }, new CatModelCatalog(fixture.Prefab));
                    view.SyncSlot(0x0000000100000001L, CatColor.Red);
                    view.ApplyPresentation(CatPresentationState.RideIdle, 0f, false);
                    Animator animator = view.GetComponentInChildren<Animator>(true);

                    view.ApplyPresentation(CatPresentationState.RideIdle, 0.1f, true);
                    view.ApplyPresentation(CatPresentationState.RideIdle, 0.2f, true);
                    view.ApplyPresentation(CatPresentationState.RideIdle, 0.3f, true);
                    Assert.That(view.RigNeutralSampleCount, Is.EqualTo(1));
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0)
                        .IsName("Base Layer.Cat_IdleSit"), Is.True,
                        "motion-off must not freeze the controller's non-idle default state");
                    Assert.That(animator.speed, Is.EqualTo(0f));

                    view.ApplyPresentation(CatPresentationState.RideIdle, 0.4f, false);
                    view.ApplyPresentation(CatPresentationState.RideIdle, 0.5f, true);
                    Assert.That(view.RigNeutralSampleCount, Is.EqualTo(2));
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0)
                        .IsName("Base Layer.Cat_IdleSit"), Is.True);
                }
                finally
                {
                    Object.DestroyImmediate(host);
                }
            }
        }

        [Test]
        public void HiddenMotionOff_SamplesNeutralBeforeDeactivationAndAfterOccupantReuse()
        {
            using (var fixture = new ConformingRigFixture())
            {
                var host = new GameObject("hidden-motion-off-rig-host");
                try
                {
                    var view = ToyTrainView.Create(host.transform, "train:rig",
                        new[] { 0 }, new[] { 1 }, new CatModelCatalog(fixture.Prefab));
                    Transform cat = view.transform.Find("Carriage/Cat");
                    view.SyncSlot(0x0000000100000001L, CatColor.Red);
                    view.ApplyPresentation(CatPresentationState.RideIdle, 0f, false);

                    view.ApplyPresentation(CatPresentationState.Hidden, 0.1f, true);

                    Assert.That(cat.gameObject.activeSelf, Is.False);
                    Assert.That(view.RigNeutralSampleCount, Is.EqualTo(1),
                        "a hidden rig may claim a neutral sample only after Animator.Update ran");
                    LogAssert.NoUnexpectedReceived();

                    view.SyncSlot(0x0000000100000002L, CatColor.Blue);
                    view.ApplyPresentation(CatPresentationState.RideIdle, 0.2f, false);
                    view.ApplyPresentation(CatPresentationState.Hidden, 0.3f, true);

                    Assert.That(cat.gameObject.activeSelf, Is.False);
                    Assert.That(view.RigNeutralSampleCount, Is.EqualTo(2),
                        "a reused occupant must get its own active neutral resample");
                    LogAssert.NoUnexpectedReceived();
                }
                finally
                {
                    Object.DestroyImmediate(host);
                }
            }
        }

        private static void AssertPlays(ToyTrainView view, Animator animator,
            CatPresentationState state, string expectedState)
        {
            view.ApplyPresentation(state, 0f, false);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(expectedState), Is.True,
                "presentation state " + state + " must sample " + expectedState);
            Assert.That(animator.applyRootMotion, Is.False);
        }

        private static void AssertDirection(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual.normalized, expected), Is.LessThan(0.0001f), message);
        }

        private static void AssertTint(Color actual, Color expected, string message)
        {
            // MaterialPropertyBlock SetColor/GetColor is not bit-exact in this project's
            // Linear color space. The observed residue is about 1e-7 per channel.
            const float tolerance = 0.000001f;
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance), message + " (r)");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance), message + " (g)");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance), message + " (b)");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(tolerance), message + " (a)");
        }

        private static void AssertTints(Renderer[] renderers, Color expected, string message)
        {
            var properties = new MaterialPropertyBlock();
            for (int index = 0; index < renderers.Length; index++)
            {
                Assert.That(renderers[index].enabled, Is.True, message + " (enabled)");
                properties.Clear();
                renderers[index].GetPropertyBlock(properties);
                AssertTint(properties.GetColor("_BaseColor"), expected,
                    message + " (" + renderers[index].name + ")");
            }
        }

        private static Bounds BoundsIn(Transform frame, MeshFilter filter)
        {
            Bounds mesh = filter.sharedMesh.bounds;
            bool initialized = false;
            Bounds result = default;
            for (int x = 0; x <= 1; x++)
            for (int y = 0; y <= 1; y++)
            for (int z = 0; z <= 1; z++)
            {
                Vector3 point = new Vector3(x == 0 ? mesh.min.x : mesh.max.x,
                    y == 0 ? mesh.min.y : mesh.max.y,
                    z == 0 ? mesh.min.z : mesh.max.z);
                point = frame.InverseTransformPoint(filter.transform.TransformPoint(point));
                if (!initialized)
                {
                    result = new Bounds(point, Vector3.zero);
                    initialized = true;
                }
                else result.Encapsulate(point);
            }
            return result;
        }

        private static float TimeWithLargeEarTwitch(uint seed)
        {
            var motion = new CatMicroMotion(seed);
            for (int sample = 0; sample <= 1000; sample++)
            {
                float time = sample * 0.01f;
                if (Mathf.Abs(motion.Evaluate(time, false, false).EarTwitchDegrees) >= 12f)
                    return time;
            }
            Assert.Fail("the deterministic Tier-1 cadence must contain a >=12 degree ear pose");
            return 0f;
        }

        private static long PresentationKeyForSeed(uint seed, uint generation)
        {
            uint lowKey = seed ^ generation * 2654435761u;
            return ((long)generation << 32) | lowKey;
        }

        private static void MeasureRigClearanceCase(ToyTrainView view, Transform board,
            Mesh baked, CatPresentationState state, bool movingToPlatform,
            Vector3 sourceSide, float maximumBobTime, float minimumBobTime,
            float[] visualTimes, string caseLabel, ref float minimumGap,
            ref string minimumLabel)
        {
            Transform carriage = view.transform.Find("Carriage");
            Transform cat = carriage.Find("Cat");
            Animator animator = cat.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null, caseLabel + " must use the admitted rig");
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            SkinnedMeshRenderer[] skins =
                animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(skins, Has.Length.EqualTo(1),
                caseLabel + " must expose the pinned artifact's one skin");
            SkinnedMeshRenderer skin = skins[0];

            Vector3 towardCarriage = carriage.InverseTransformDirection(
                board.TransformDirection(-sourceSide)).normalized;
            Assert.That(towardCarriage.sqrMagnitude, Is.GreaterThan(0.999f),
                caseLabel + " must declare a non-zero platform side");
            float carriageMinimum = float.PositiveInfinity;
            foreach (string partName in new[] { "Body", "Chassis" })
            {
                MeshFilter part = carriage.Find(partName).GetComponent<MeshFilter>();
                Assert.That(part, Is.Not.Null, caseLabel + "/" + partName);
                carriageMinimum = Mathf.Min(carriageMinimum,
                    MinimumProjectionIn(carriage, part.transform,
                        part.sharedMesh.vertices, towardCarriage));
            }

            view.ApplyPresentation(state, 1f, movingToPlatform,
                maximumBobTime, false, 1f);
            float maximumRootProjection =
                Vector3.Dot(cat.localPosition, towardCarriage);
            view.ApplyPresentation(state, 1f, movingToPlatform,
                minimumBobTime, false, 1f);
            maximumRootProjection = Mathf.Max(maximumRootProjection,
                Vector3.Dot(cat.localPosition, towardCarriage));

            string clipName = state == CatPresentationState.WaitingIdle
                ? CatModelCatalog.IdleSitClip
                : state == CatPresentationState.Celebrate
                    ? CatModelCatalog.CelebrateClip
                    : CatModelCatalog.WalkClip;
            AnimationClip clip = animator.runtimeAnimatorController.animationClips
                .Single(candidate => candidate.name == clipName);
            int halfFrameSamples = Mathf.CeilToInt(
                clip.length * clip.frameRate * 2f);
            Assert.That(halfFrameSamples, Is.GreaterThan(0),
                caseLabel + " must sample a non-empty " + clipName + " artifact");
            var motion = new CatMicroMotion(41u);
            for (int clipSample = 0; clipSample <= halfFrameSamples; clipSample++)
            {
                float normalizedTime = clipSample / (float)halfFrameSamples;
                for (int earSample = 0; earSample < visualTimes.Length; earSample++)
                {
                    float visualTime = visualTimes[earSample];
                    animator.Play("Base Layer." + clipName, 0, normalizedTime);
                    animator.Update(0f);
                    view.ApplyPresentation(state, 1f, movingToPlatform,
                        visualTime, false, 1f);
                    skin.BakeMesh(baked, true);

                    float currentRootProjection =
                        Vector3.Dot(cat.localPosition, towardCarriage);
                    float bobCorrection = maximumRootProjection - currentRootProjection;
                    Assert.That(bobCorrection, Is.GreaterThanOrEqualTo(-0.00001f),
                        caseLabel + " selected bob extrema must never move the measured skin "
                        + "farther carriage-ward than the corrected envelope");
                    float conservativeBobCorrection = Mathf.Max(0f, bobCorrection);
                    float rigMaximum = MaximumProjectionIn(carriage, skin.transform,
                        baked.vertices, towardCarriage) + conservativeBobCorrection;
                    float gap = carriageMinimum - rigMaximum;
                    if (gap >= minimumGap) continue;

                    CatMicroPose pose = motion.Evaluate(visualTime, false,
                        state == CatPresentationState.Celebrate);
                    minimumGap = gap;
                    minimumLabel = caseLabel
                        + "/clip=" + clipName
                        + "/halfFrame=" + clipSample + "/" + halfFrameSamples
                        + "/earSample=" + earSample
                        + "/sourceEar=" + pose.EarTwitchDegrees.ToString("F3")
                        + "/appliedEar=" + (pose.EarTwitchDegrees
                            * ToyTrainView.RigEarTwitchGain).ToString("F3")
                        + "/bobCorrection=" + conservativeBobCorrection.ToString("F6")
                        + "/maxRoot=" + maximumRootProjection.ToString("F6")
                        + "/currentRoot=" + currentRootProjection.ToString("F6");
                }
            }
        }

        private static float[] TimesAcrossEarRange(uint seed, out float maximumBobTime,
            out float minimumBobTime)
        {
            var motion = new CatMicroMotion(seed);
            const int earSamples = 17;
            const float timeStep = 0.001f;
            var bestTimes = new float[earSamples];
            var bestErrors = Enumerable.Repeat(float.PositiveInfinity, earSamples).ToArray();
            maximumBobTime = 0f;
            minimumBobTime = 0f;
            float maximumBob = float.NegativeInfinity;
            float minimumBob = float.PositiveInfinity;
            // Bob at 4.1 rad/s and ears at 2.7 rad/s share a 20*PI-second period. Sweep that
            // exact production clock once, retaining the closest artifact time for each target
            // ear angle and both independent bob-extreme times.
            int samples = Mathf.CeilToInt(20f * Mathf.PI / timeStep);
            for (int sample = 0; sample <= samples; sample++)
            {
                float time = sample * timeStep;
                CatMicroPose pose = motion.Evaluate(time, false, false);
                if (pose.Bob > maximumBob)
                {
                    maximumBob = pose.Bob;
                    maximumBobTime = time;
                }
                if (pose.Bob < minimumBob)
                {
                    minimumBob = pose.Bob;
                    minimumBobTime = time;
                }
                for (int index = 0; index < earSamples; index++)
                {
                    float target = Mathf.Lerp(-CatMicroMotion.EarTwitchMaximumDegrees,
                        CatMicroMotion.EarTwitchMaximumDegrees,
                        index / (float)(earSamples - 1));
                    float error = Mathf.Abs(pose.EarTwitchDegrees - target);
                    if (error >= bestErrors[index]) continue;
                    bestErrors[index] = error;
                    bestTimes[index] = time;
                }
            }
            return bestTimes;
        }

        private static float MaximumProjectionIn(Transform frame, Transform source,
            Vector3[] vertices, Vector3 axis)
        {
            Assert.That(vertices, Is.Not.Empty);
            float maximum = float.NegativeInfinity;
            for (int index = 0; index < vertices.Length; index++)
                maximum = Mathf.Max(maximum, Vector3.Dot(
                    frame.InverseTransformPoint(source.TransformPoint(vertices[index])), axis));
            return maximum;
        }

        private static float MinimumProjectionIn(Transform frame, Transform source,
            Vector3[] vertices, Vector3 axis)
        {
            Assert.That(vertices, Is.Not.Empty);
            float minimum = float.PositiveInfinity;
            for (int index = 0; index < vertices.Length; index++)
                minimum = Mathf.Min(minimum, Vector3.Dot(
                    frame.InverseTransformPoint(source.TransformPoint(vertices[index])), axis));
            return minimum;
        }

        private static float TimeWithBlink(uint seed)
        {
            var motion = new CatMicroMotion(seed);
            for (int sample = 0; sample <= 1000; sample++)
            {
                float time = sample * 0.01f;
                if (motion.Evaluate(time, false, false).EyeYScale <= 0.1f)
                    return time;
            }
            Assert.Fail("the deterministic Tier-1 cadence must contain a closed-eye sample");
            return 0f;
        }

        private static void AssertLocalizedUpperHeadDeformation(Mesh neutral, Mesh twitch,
            Transform rendererTransform, Transform rigFrame)
        {
            Assert.That(twitch.vertexCount, Is.EqualTo(neutral.vertexCount));
            Vector3[] baseline = neutral.vertices;
            Vector3[] deformed = twitch.vertices;
            var rigBaseline = new Vector3[baseline.Length];
            Bounds bounds = default;
            for (int index = 0; index < baseline.Length; index++)
            {
                rigBaseline[index] = rigFrame.InverseTransformPoint(
                    rendererTransform.TransformPoint(baseline[index]));
                if (index == 0) bounds = new Bounds(rigBaseline[index], Vector3.zero);
                else bounds.Encapsulate(rigBaseline[index]);
            }
            float upperHeadFloor = bounds.min.y + bounds.size.y * 0.55f;
            int movedVertices = 0;
            float maximumDisplacement = 0f;
            float lowerBodyDisplacement = 0f;
            for (int index = 0; index < baseline.Length; index++)
            {
                Vector3 neutralWorld = rendererTransform.TransformPoint(baseline[index]);
                Vector3 twitchWorld = rendererTransform.TransformPoint(deformed[index]);
                float displacement = Vector3.Distance(neutralWorld, twitchWorld);
                maximumDisplacement = Mathf.Max(maximumDisplacement, displacement);
                if (displacement > 0.00001f) movedVertices++;
                if (rigBaseline[index].y < upperHeadFloor)
                    lowerBodyDisplacement = Mathf.Max(lowerBodyDisplacement, displacement);
            }

            Assert.That(movedVertices, Is.GreaterThan(1000),
                "both measured ear branches must deform a substantial visible region");
            TestContext.Out.WriteLine("CAT_RIG_EAR_TWITCH_READBACK movedVertices="
                + movedVertices + " maxBoardDisplacement=" + maximumDisplacement.ToString("F6")
                + " worstZoomUpperBoundPixels=" + (maximumDisplacement * 93f).ToString("F3")
                + " lowerBodyDisplacement=" + lowerBodyDisplacement.ToString("F6"));
            Assert.That(maximumDisplacement, Is.GreaterThan(0.008f),
                "the >=12 degree probe must move an ear by at least 0.008 board units; " +
                "screen projection remains a render-slot question");
            Assert.That(lowerBodyDisplacement, Is.LessThan(0.0001f),
                "the ear control must remain localized above the calibrated 55% height line");
        }

        private sealed class ConformingRigFixture : IDisposable
        {
            private readonly AnimatorController _controller;
            private readonly List<AnimationClip> _clips = new List<AnimationClip>();
            private readonly List<StateMachineBehaviour> _stateBehaviours =
                new List<StateMachineBehaviour>();

            private readonly Vector3 _animationBindPosition =
                new Vector3(0f, 0.4853515f, 0f);

            public ConformingRigFixture(bool swapWalkAndBoard = false,
                bool addStateBehaviour = false, string emptyMappedClipName = null,
                string zeroLengthMappedClipName = null, float centerX = 0f,
                float centerZ = 0f)
            {
                Prefab = new GameObject("ConformingBoardCatRig");
                var body = new GameObject("RigBody");
                body.transform.SetParent(Prefab.transform, false);
                body.transform.localPosition = new Vector3(centerX, 0.5f, centerZ);
                // Narrow horizontal extents keep the measured 2.98e-8 residue nonzero after
                // float32 bounds accumulation; a unit cube rounds that test input back to zero.
                body.transform.localScale = new Vector3(0.2f, 1f, 0.2f);
                body.AddComponent<MeshFilter>().sharedMesh =
                    Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                body.AddComponent<MeshRenderer>();
                var accent = new GameObject("RigAccent");
                accent.transform.SetParent(Prefab.transform, false);
                accent.AddComponent<MeshRenderer>();
                var armature = new GameObject("Armature");
                armature.transform.SetParent(Prefab.transform, false);
                armature.transform.localPosition = _animationBindPosition;

                _controller = new AnimatorController();
                _controller.AddLayer("Base Layer");
                AddRequiredState("Cat_IdleSit", emptyMappedClipName,
                    zeroLengthMappedClipName);
                AnimatorState walk = AddRequiredState("Cat_Walk", emptyMappedClipName,
                    zeroLengthMappedClipName);
                AnimatorState board = AddRequiredState("Cat_Board", emptyMappedClipName,
                    zeroLengthMappedClipName);
                AddRequiredState("Cat_Alight", emptyMappedClipName,
                    zeroLengthMappedClipName);
                AnimatorState celebrate = AddRequiredState("Cat_Celebrate",
                    emptyMappedClipName, zeroLengthMappedClipName);
                if (swapWalkAndBoard)
                {
                    Motion walkMotion = walk.motion;
                    walk.motion = board.motion;
                    board.motion = walkMotion;
                }
                _controller.layers[0].stateMachine.defaultState = celebrate;
                if (addStateBehaviour)
                {
                    StateBehaviour =
                        celebrate.AddStateMachineBehaviour<CatRigStateBehaviourProbe>();
                    if (StateBehaviour != null) _stateBehaviours.Add(StateBehaviour);
                }

                var animator = Prefab.AddComponent<Animator>();
                animator.runtimeAnimatorController = _controller;
                animator.applyRootMotion = false;
            }

            public GameObject Prefab { get; }

            public StateMachineBehaviour StateBehaviour { get; }

            public AnimationClip ClipNamed(string name)
            {
                for (int i = 0; i < _clips.Count; i++)
                    if (_clips[i].name == name) return _clips[i];
                return null;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Prefab);
                for (int i = 0; i < _stateBehaviours.Count; i++)
                    Object.DestroyImmediate(_stateBehaviours[i]);
                Object.DestroyImmediate(_controller);
                for (int i = 0; i < _clips.Count; i++) Object.DestroyImmediate(_clips[i]);
            }

            private AnimatorState AddState(string literalName, bool animateChild = false,
                string clipName = null, bool holdBindPose = false,
                bool zeroLength = false)
            {
                var clip = new AnimationClip { name = clipName ?? literalName };
                if (animateChild)
                {
                    float endTime = zeroLength ? 0f : holdBindPose ? 1f / 24f : 0.4f;
                    SetPositionCurve(clip, "localPosition.x", _animationBindPosition.x,
                        holdBindPose || zeroLength
                            ? _animationBindPosition.x : _animationBindPosition.x + 0.02f,
                        endTime, holdBindPose || zeroLength);
                    SetPositionCurve(clip, "localPosition.y", _animationBindPosition.y,
                        _animationBindPosition.y, endTime, holdBindPose || zeroLength);
                    SetPositionCurve(clip, "localPosition.z", _animationBindPosition.z,
                        _animationBindPosition.z, endTime, holdBindPose || zeroLength);
                }
                _clips.Add(clip);
                AnimatorState state = _controller.layers[0].stateMachine.AddState(literalName);
                state.motion = clip;
                return state;
            }

            private AnimatorState AddRequiredState(string literalName,
                string emptyMappedClipName, string zeroLengthMappedClipName)
            {
                bool emptyMappedClip = literalName == emptyMappedClipName;
                if (emptyMappedClip)
                    AddState("animated_decoy_" + literalName, true, literalName);
                bool fallback = literalName != CatModelCatalog.WalkClip;
                return AddState(literalName, !emptyMappedClip, null,
                    fallback, literalName == zeroLengthMappedClipName);
            }

            private static void SetPositionCurve(AnimationClip clip, string property,
                float startValue, float endValue, float endTime, bool singleKey)
            {
                AnimationCurve curve = singleKey
                    ? new AnimationCurve(new Keyframe(endTime, endValue))
                    : AnimationCurve.Linear(0f, startValue, endTime, endValue);
                clip.SetCurve("Armature", typeof(Transform), property, curve);
            }
        }

        private static AnimationClip Clip(string name)
        {
            var clip = new AnimationClip { name = name };
            clip.SetCurve("RigBody", typeof(Transform), "localPosition.x",
                AnimationCurve.Linear(0f, 0f, 0.4f, 0.02f));
            return clip;
        }
    }
}
