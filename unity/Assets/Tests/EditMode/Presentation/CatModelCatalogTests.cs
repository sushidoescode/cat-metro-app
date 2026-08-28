using System;
using System.Collections.Generic;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Cats;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.EditMode.Presentation
{
    public sealed class CatRigMonoBehaviourProbe : MonoBehaviour
    {
        public static int AwakeCount;

        private void Awake() => AwakeCount++;
    }

    public sealed class CatRigStateBehaviourProbe : StateMachineBehaviour
    {
        public static int StateEnterCount;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo,
            int layerIndex) => StateEnterCount++;
    }

    public sealed class CatModelCatalogTests
    {
        [Test]
        public void Task17HandoffContract_UsesThePinnedResourceAndStateLiterals()
        {
            Assert.That(CatModelCatalog.ResourcePath, Is.EqualTo("CatRigs/BoardCatRig"));
            Assert.That(CatModelCatalog.IdleSitClip, Is.EqualTo("Cat_IdleSit"));
            Assert.That(CatModelCatalog.WalkClip, Is.EqualTo("Cat_Walk"));
            Assert.That(CatModelCatalog.BoardClip, Is.EqualTo("Cat_Board"));
            Assert.That(CatModelCatalog.AlightClip, Is.EqualTo("Cat_Alight"));
            Assert.That(CatModelCatalog.CelebrateClip, Is.EqualTo("Cat_Celebrate"));
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

        [Test]
        public void EmptyWalkClip_IsRejectedEvenWhenEveryRequiredStateAndClipExists()
        {
            using (var fixture = new ConformingRigFixture(animateWalk: false))
            {
                var catalog = new CatModelCatalog(fixture.Prefab);

                Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(0));
                Assert.That(catalog.RejectionReason, Does.Contain("Cat_Walk"));
                Assert.That(catalog.RejectionReason, Does.Contain("positive-length"));
            }
        }

        [Test]
        public void EmptyMappedWalkClip_IsRejectedDespiteAnAnimatedSameNamedDecoy()
        {
            using (var fixture = new ConformingRigFixture(
                emptyMappedWalkWithAnimatedDuplicate: true))
            {
                var catalog = new CatModelCatalog(fixture.Prefab);

                Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(0));
                Assert.That(catalog.RejectionReason, Does.Contain("Cat_Walk"));
                Assert.That(catalog.RejectionReason, Does.Contain("positive-length"));
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
                        Is.EqualTo(Vector3.one * 0.34f));
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
                    Assert.That(rig.GetComponentInChildren<MeshRenderer>(true).enabled, Is.True);

                    Bounds standing = BoundsIn(cat,
                        rig.GetComponentInChildren<MeshFilter>(true));
                    Assert.That(standing.min.z, Is.EqualTo(-0.34f).Within(0.0001f));
                    Assert.That(standing.max.z, Is.EqualTo(0f).Within(0.0001f));
                    Assert.That(standing.size.z, Is.EqualTo(0.34f).Within(0.0001f));

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

        private sealed class ConformingRigFixture : IDisposable
        {
            private readonly AnimatorController _controller;
            private readonly List<AnimationClip> _clips = new List<AnimationClip>();
            private readonly List<StateMachineBehaviour> _stateBehaviours =
                new List<StateMachineBehaviour>();

            public ConformingRigFixture(bool animateWalk = true,
                bool swapWalkAndBoard = false, bool addStateBehaviour = false,
                bool emptyMappedWalkWithAnimatedDuplicate = false)
            {
                Prefab = new GameObject("ConformingBoardCatRig");
                var body = new GameObject("RigBody");
                body.transform.SetParent(Prefab.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                body.AddComponent<MeshFilter>().sharedMesh =
                    Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                body.AddComponent<MeshRenderer>();

                _controller = new AnimatorController();
                _controller.AddLayer("Base Layer");
                AddState("Cat_IdleSit");
                if (emptyMappedWalkWithAnimatedDuplicate)
                    AddState("walk_decoy", true, "Cat_Walk");
                AnimatorState walk = AddState("Cat_Walk",
                    emptyMappedWalkWithAnimatedDuplicate ? false : animateWalk);
                AnimatorState board = AddState("Cat_Board");
                AddState("Cat_Alight");
                AnimatorState celebrate = AddState("Cat_Celebrate");
                if (swapWalkAndBoard)
                {
                    Motion walkMotion = walk.motion;
                    walk.motion = board.motion;
                    board.motion = walkMotion;
                }
                _controller.layers[0].stateMachine.defaultState = celebrate;
                if (addStateBehaviour)
                    _stateBehaviours.Add(
                        celebrate.AddStateMachineBehaviour<CatRigStateBehaviourProbe>());

                var animator = Prefab.AddComponent<Animator>();
                animator.runtimeAnimatorController = _controller;
                animator.applyRootMotion = false;
            }

            public GameObject Prefab { get; }

            public void Dispose()
            {
                Object.DestroyImmediate(Prefab);
                for (int i = 0; i < _stateBehaviours.Count; i++)
                    Object.DestroyImmediate(_stateBehaviours[i]);
                Object.DestroyImmediate(_controller);
                for (int i = 0; i < _clips.Count; i++) Object.DestroyImmediate(_clips[i]);
            }

            private AnimatorState AddState(string literalName, bool animateChild = false,
                string clipName = null)
            {
                var clip = new AnimationClip { name = clipName ?? literalName };
                if (animateChild)
                    clip.SetCurve("RigBody", typeof(Transform), "localPosition.x",
                        AnimationCurve.Linear(0f, 0f, 0.4f, 0.02f));
                _clips.Add(clip);
                AnimatorState state = _controller.layers[0].stateMachine.AddState(literalName);
                state.motion = clip;
                return state;
            }
        }

        private static AnimationClip Clip(string name)
        {
            var clip = new AnimationClip { name = name };
            if (name == "Cat_Walk")
                clip.SetCurve("RigBody", typeof(Transform), "localPosition.x",
                    AnimationCurve.Linear(0f, 0f, 0.4f, 0.02f));
            return clip;
        }
    }
}
