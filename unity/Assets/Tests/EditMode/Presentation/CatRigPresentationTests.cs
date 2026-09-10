using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Cats;
using CatMetro.Presentation.Cosmetics;
using CatMetro.Presentation.Props;
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

        [TestCase(TrainState.RejectedAtStation)]
        [TestCase(TrainState.OnEdgeReverse)]
        [TestCase(TrainState.AtNode)]
        public void LocalCarriageWaitKeepsSeatedBodyAndRidePhaseAcrossStopAndResume(byte simulationState)
        {
            ToyTrainView view = CreateSampledLocalTrain(out Animator animator);
            var track = new CatPresentationTrack();
            var slot = new TrainSlot { Id = 1, State = TrainState.OnEdge };
            track.Observe(slot, 1, false, 0f);
            track.Observe(slot, 1, false, .5f);
            Assert.That(track.State, Is.EqualTo(CatPresentationState.RideIdle));
            view.ApplyPresentation(track.State, track.PlatformBlend, false, 1f, false);
            animator.Update(.37f);
            AssertDiscriminatingRidePose(animator, .37f);
            float phase = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            Quaternion body = animator.transform.Find(BodyPath).localRotation;
            Vector3 root = view.transform.localPosition;
            Vector3 engine = view.transform.Find("Engine").localPosition;
            Vector3 carriage = view.transform.Find("Carriage").localPosition;
            Vector3 pin = view.transform.Find("Carriage/Pin").localPosition;

            slot.State = simulationState;
            track.Observe(slot, 1, false, .87f);
            Assert.That(track.State, Is.EqualTo(CatPresentationState.WaitingIdle));
            Assert.That(track.PlatformBlend, Is.Zero);
            view.ApplyPresentation(track.State, track.PlatformBlend, false, 1f, false);
            Assert.That(view.PresentationState, Is.EqualTo(CatPresentationState.WaitingIdle),
                "simulation-facing presentation state remains a wait");
            AssertRideStateAndPhase(animator, phase);
            Assert.That(Quaternion.Angle(animator.transform.Find(BodyPath).localRotation, body), Is.LessThan(.05f));
            Assert.That(view.transform.localPosition, Is.EqualTo(root));
            Assert.That(view.transform.Find("Engine").localPosition, Is.EqualTo(engine));
            Assert.That(view.transform.Find("Carriage").localPosition, Is.EqualTo(carriage));
            Assert.That(view.transform.Find("Carriage/Pin").localPosition, Is.EqualTo(pin));

            animator.Update(.21f);
            AssertDiscriminatingRidePose(animator, .58f);
            phase = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            view.ApplyPresentation(CatPresentationState.WaitingIdle, 0f, false, 1f, false);
            AssertRideStateAndPhase(animator, phase);
            slot.State = TrainState.OnEdge;
            track.Observe(slot, 1, false, 1.08f);
            view.ApplyPresentation(track.State, track.PlatformBlend, false, 1f, false);
            AssertRideStateAndPhase(animator, phase);
        }

        [Test]
        public void LocalWaitingLocationSelectsPlatformIdleOrCarriageRideEvenWhenPublicStateDoesNotChange()
        {
            ToyTrainView view = CreateSampledLocalTrain(out Animator animator);
            view.ApplyPresentation(CatPresentationState.WaitingIdle, 0f, false, 1f, false);
            animator.Update(.37f);
            AssertDiscriminatingRidePose(animator, .37f);
            Quaternion seated = animator.transform.Find(BodyPath).localRotation;
            view.ApplyPresentation(CatPresentationState.WaitingIdle, 1f, false, 1f, false);
            Assert.That(view.PresentationState, Is.EqualTo(CatPresentationState.WaitingIdle));
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Cat_IdleSit"), Is.True);
            Assert.That(Quaternion.Angle(animator.transform.Find(BodyPath).localRotation, seated), Is.GreaterThan(25f),
                "source/platform waiting uses the independently distinct neutral body");
            view.ApplyPresentation(CatPresentationState.WaitingIdle, 0f, false, 1f, false);
            AssertRideStateAndPhase(animator, 0f);
            AssertDiscriminatingRidePose(animator, 0f);

            view.PrepareDeliveredPassenger(Vector3.zero);
            var retained = new CatPresentationTrack(); retained.SampleWin(-1f, 0, false);
            view.ApplyDeliveredPose(retained, 1f, false);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Cat_IdleSit"), Is.True,
                "retained passengers remain on the platform");
        }

        [Test]
        public void LocalCarriageWaitRetainsSameOccupantPhaseButResetsForReusedSlot()
        {
            ToyTrainView view = CreateSampledLocalTrain(out Animator animator);
            view.ApplyPresentation(CatPresentationState.RideIdle, 0f, false, 1f, false);
            animator.Update(.37f);
            AssertDiscriminatingRidePose(animator, .37f);
            float phase = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            view.SyncSlot(0x0000000100000001L, CatColor.Red);
            view.ApplyPresentation(CatPresentationState.WaitingIdle, 0f, false, 1f, false);
            AssertRideStateAndPhase(animator, phase);
            view.ApplyPresentation(CatPresentationState.Hidden, 1f, false);
            view.SyncSlot(0x0000000100000002L, CatColor.Blue);
            view.ApplyPresentation(CatPresentationState.WaitingIdle, 0f, false, 1f, false);
            AssertRideStateAndPhase(animator, 0f);
            AssertDiscriminatingRidePose(animator, 0f);
        }

        [TestCase(0f, false)]
        [TestCase(1f, false)]
        [TestCase(0f, true)]
        [TestCase(1f, true)]
        public void LocalWaitingMotionOffKeepsOneStaticSampleThenResumesForItsLocation(float platformBlend, bool original)
        {
            RequireLocalSource();
            ToyTrainView view = CreateSeatTrain(original, out Animator animator, out Transform rig);
            view.ApplyPresentation(CatPresentationState.RideIdle, 0f, false, 1f, false);
            animator.Update(.37f);
            AssertDiscriminatingRidePose(animator, .37f);
            view.ApplyPresentation(CatPresentationState.WaitingIdle, platformBlend, false, 1f, true);
            bool seated = original && platformBlend == 0f;
            AssertStaticMotionOff(view, animator, rig, seated, SampleStaticReference(seated));
            Quaternion neutral = animator.transform.Find(BodyPath).localRotation;
            int samples = view.RigNeutralSampleCount;
            Assert.That(samples, Is.EqualTo(1));
            animator.Update(.3f);
            view.ApplyPresentation(CatPresentationState.WaitingIdle, platformBlend, false, 2f, true);
            Assert.That(animator.speed, Is.Zero);
            Assert.That(view.RigNeutralSampleCount, Is.EqualTo(samples));
            Assert.That(animator.transform.Find(BodyPath).localRotation, Is.EqualTo(neutral));
            view.ApplyPresentation(CatPresentationState.WaitingIdle, platformBlend, false, 2f, false);
            string resumed = platformBlend == 0f ? "Base Layer.Cat_Ride" : "Base Layer.Cat_IdleSit";
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(resumed), Is.True);
            Assert.That(animator.speed, Is.EqualTo(1f));
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(0f).Within(.00001f));
        }

        private ToyTrainView CreateSampledLocalTrain(out Animator animator)
        {
            RequireLocalSource();
            var view = ToyTrainView.Create(_host.transform, "carriage wait regression", new[] { 0 }, new[] { 1 },
                CatModelCatalog.LoadResources());
            view.SyncSlot(0x0000000100000001L, CatColor.Red);
            animator = view.GetComponentInChildren<Animator>(true);
            Assert.That(animator.GetComponent<CatRigPresentation>().AuthoredMotionInstalled, Is.True);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            return view;
        }

        [Test]
        public void OriginalCarriageSeatUsesCalibratedLimbsAndDepthWithoutChangingFallbackOrPlatformClips()
        {
            RequireLocalSource();
            ToyTrainView original = CreateSeatTrain(true, out Animator seated, out Transform seatRoot);
            ToyTrainView fallback = CreateSeatTrain(false, out Animator prior, out Transform priorRoot);
            foreach (var pair in new[] { (original, seated), (fallback, prior) })
            {
                pair.Item1.ApplyPresentation(CatPresentationState.RideIdle, 0f, false);
                pair.Item2.Update(.4f);
                SampleTrainLateUpdate(pair.Item1);
            }
            Assert.That(seatRoot.localPosition, Is.EqualTo(Vector3.forward * .0983f));
            Assert.That(priorRoot.localPosition, Is.EqualTo(Vector3.zero));
            foreach (var control in new[] {
                (BodyPath + "/bone_21/tripo::0_Right_Limb_0/bone_27", Vector3.right, 36f),
                (BodyPath + "/bone_21/tripo::0_Left_Limb_0/tripo::0_Left_Limb_1/tripo::0_Left_Limb_2", Vector3.up, 12f),
                (BodyPath + "/bone_9/bone_12/tripo::Tail_0", Vector3.right, 30f) })
            {
                Quaternion baseline = Quaternion.Inverse(prior.transform.rotation) * prior.transform.Find(control.Item1).rotation;
                Quaternion actual = Quaternion.Inverse(seated.transform.rotation) * seated.transform.Find(control.Item1).rotation;
                Assert.That(Quaternion.Angle(actual, Quaternion.AngleAxis(control.Item3, control.Item2) * baseline),
                    Is.LessThan(.05f), "calibration against independently evaluated unchanged fallback: " + control.Item1);
            }
            foreach (string path in new[] { BodyPath, CatRigPresentation.WeightedHeadPath })
                Assert.That(Quaternion.Angle(seated.transform.Find(path).localRotation,
                    prior.transform.Find(path).localRotation), Is.LessThan(.001f), path);
            foreach (string name in new[] { "Cat_IdleSit", "Cat_Walk", "Cat_Celebrate" })
                Assert.That(seated.runtimeAnimatorController.animationClips.Single(c => c.name == name),
                    Is.SameAs(prior.runtimeAnimatorController.animationClips.Single(c => c.name == name)), name);
            foreach (string path in new[] { "Engine", "Carriage" })
                Assert.That(original.transform.Find(path).localPosition, Is.EqualTo(fallback.transform.Find(path).localPosition), path);
            Assert.That(original.transform.Find("Carriage/Cat").localPosition, Is.EqualTo(Vector3.zero),
                "the calibrated seated body stays centred while its authored bones breathe");
            Assert.That(Vector3.Distance(original.transform.Find("Carriage/Pin").localPosition,
                fallback.transform.Find("Carriage/Pin").localPosition - fallback.transform.Find("Carriage/Cat").localPosition),
                Is.LessThan(.000001f), "removing rider bob preserves the label-to-rider offset");
            Assert.That(seated.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh,
                Is.SameAs(prior.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh));
            original.ApplyPresentation(CatPresentationState.WaitingIdle, 1f, false, 1f, false);
            Assert.That(seatRoot.localPosition, Is.EqualTo(Vector3.zero), "platform idle has no carriage correction");
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void OriginalCarriageDepthFollowsEvaluatedBoardAlightAndStaticRideOnMotionOffAndReuse(int framesPerSecond)
        {
            RequireLocalSource();
            ToyTrainView view = CreateSeatTrain(true, out Animator animator, out Transform rig);
            foreach (var state in new[] { CatPresentationState.Board, CatPresentationState.Alight })
            {
                view.ApplyPresentation(state, 0f, false);
                float previous = rig.localPosition.z;
                Assert.That(previous, Is.EqualTo(state == CatPresentationState.Board ? 0f : .0983f).Within(.00001f));
                float elapsed = 0f;
                while (elapsed < .18f)
                {
                    float delta = Mathf.Min(1f / framesPerSecond, .18f - elapsed);
                    animator.Update(delta);
                    SampleTrainLateUpdate(view);
                    float depth = rig.localPosition.z;
                    Assert.That(depth, Is.InRange(-.000001f, .098301f));
                    Assert.That(state == CatPresentationState.Board ? depth >= previous - .000001f : depth <= previous + .000001f,
                        Is.True, "evaluated transition must move monotonically between the actual seat and unchanged platform height");
                    for (int repeat = 0; repeat < 3; repeat++) SampleTrainLateUpdate(view);
                    Assert.That(rig.localPosition.z, Is.EqualTo(depth), "repeated rendering cannot accumulate depth");
                    previous = depth;
                    elapsed += delta;
                }
                Assert.That(rig.localPosition.z, Is.EqualTo(state == CatPresentationState.Board ? .0983f : 0f).Within(.00001f));
            }
            view.ApplyPresentation(CatPresentationState.RideIdle, 1f, false);
            Assert.That(rig.localPosition.z, Is.EqualTo(.0983f));
            view.ApplyPresentation(CatPresentationState.RideIdle, 1f, true);
            AssertStaticMotionOff(view, animator, rig, true, SampleStaticReference(true));
            int samples = view.RigNeutralSampleCount;
            view.ApplyPresentation(CatPresentationState.RideIdle, 2f, true);
            Assert.That(view.RigNeutralSampleCount, Is.EqualTo(samples));
            view.ApplyPresentation(CatPresentationState.Hidden, 2f, false);
            view.SyncSlot(99, CatColor.Blue);
            Assert.That(rig.localPosition, Is.EqualTo(Vector3.zero));
            view.ApplyPresentation(CatPresentationState.WaitingIdle, 0f, false, 3f, false);
            Assert.That(rig.localPosition.z, Is.EqualTo(.0983f));
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(0f).Within(.00001f));
        }

        [Test]
        public void OriginalCarriageMotionOffSeatsAtSourceReleaseAndHoldsAcrossWaitAndReuse()
        {
            RequireLocalSource();
            ToyTrainView view = CreateSeatTrain(true, out Animator animator, out Transform rig);
            Quaternion[] idle = SampleStaticReference(false), ride = SampleStaticReference(true);
            Vector3 rootPosition = view.transform.localPosition;
            Quaternion rootRotation = view.transform.localRotation;
            var track = new CatPresentationTrack();
            var slot = new TrainSlot { Id = 42, State = TrainState.OnEdge };
            // Exercise the actual track: the Motion Off view cuts its inbound walk/board
            // directly to the seat while the presentation track still reports those phases.
            float[] times = { 0f, .1f, .33f, .6f };
            var states = new[] { CatPresentationState.WaitingIdle, CatPresentationState.Walk,
                CatPresentationState.Board, CatPresentationState.RideIdle };
            for (int i = 0; i < times.Length; i++)
            {
                track.Observe(slot, 1, false, times[i], i == 0);
                Assert.That(track.State, Is.EqualTo(states[i]));
                view.ApplyPresentation(track.State, track.PlatformBlend, track.MovingToPlatform, times[i], true);
                AssertStaticMotionOff(view, animator, rig, i != 0, i == 0 ? idle : ride);
                Assert.That(view.RigNeutralSampleCount, Is.EqualTo(i == 0 ? 1 : 2));
            }
            foreach (byte state in new[] { TrainState.RejectedAtStation, TrainState.OnEdgeReverse, TrainState.AtNode })
            {
                slot.State = state;
                track.Observe(slot, 1, false, 1f);
                Assert.That(track.State, Is.EqualTo(CatPresentationState.WaitingIdle));
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    view.ApplyPresentation(track.State, track.PlatformBlend, track.MovingToPlatform, 1f + repeat, true);
                    animator.Update(.2f);
                    SampleTrainLateUpdate(view);
                    AssertStaticMotionOff(view, animator, rig, true, ride);
                    Assert.That(view.RigNeutralSampleCount, Is.EqualTo(2));
                }
            }
            view.SyncSlot(99, CatColor.Blue);
            view.ApplyPresentation(CatPresentationState.RideIdle, 4f, true);
            SampleTrainLateUpdate(view);
            AssertStaticMotionOff(view, animator, rig, true, ride);
            Assert.That(view.RigNeutralSampleCount, Is.EqualTo(2), "same static pose survives occupant reuse without resampling");
            Assert.That(view.GetComponentInChildren<Animator>(true), Is.SameAs(animator));
            Assert.That(view.transform.localPosition, Is.EqualTo(rootPosition));
            Assert.That(view.transform.localRotation, Is.EqualTo(rootRotation));
            view.ApplyPresentation(CatPresentationState.RideIdle, 5f, false);
            animator.Update(.2f);
            Assert.That(animator.speed, Is.EqualTo(1f));
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThan(0f));
        }

        [Test]
        public void OriginalCarriageMotionOffRestoresNeutralForHiddenAndRetainedPassengers()
        {
            RequireLocalSource();
            ToyTrainView view = CreateSeatTrain(true, out Animator animator, out Transform rig);
            Quaternion[] idle = SampleStaticReference(false), ride = SampleStaticReference(true);
            view.ApplyPresentation(CatPresentationState.RideIdle, 0f, true);
            AssertStaticMotionOff(view, animator, rig, true, ride);
            view.ApplyPresentation(CatPresentationState.Hidden, 1f, true);
            AssertStaticMotionOff(view, animator, rig, false, idle, hidden: true);
            Assert.That(view.transform.Find("Carriage/Cat").gameObject.activeSelf, Is.False);
            int hiddenSamples = view.RigNeutralSampleCount;
            view.ApplyPresentation(CatPresentationState.Hidden, 2f, true);
            Assert.That(view.RigNeutralSampleCount, Is.EqualTo(hiddenSamples));
            view.ApplyPresentation(CatPresentationState.RideIdle, 3f, true);
            AssertStaticMotionOff(view, animator, rig, true, ride);
            view.PrepareDeliveredPassenger(Vector3.zero);
            Assert.That(view.OriginalCarriageAdmitted, Is.True,
                "retaining a passenger does not clear original-carriage admission");
            Assert.That(view.transform.Find("Carriage/OriginalCarriage").gameObject.activeSelf, Is.False);
            var retained = new CatPresentationTrack();
            retained.SampleWin(.7f, 0, true);
            view.ApplyDeliveredPose(retained, .7f, true);
            SampleTrainLateUpdate(view);
            AssertStaticMotionOff(view, animator, rig, false, idle);
            int samples = view.RigNeutralSampleCount;
            retained.SampleWin(1.2f, 0, true);
            view.ApplyDeliveredPose(retained, 1.2f, true);
            AssertStaticMotionOff(view, animator, rig, false, idle);
            Assert.That(view.RigNeutralSampleCount, Is.EqualTo(samples));
        }

        [Test]
        public void OriginalCarriageMotionOffRequiresTheInstalledOpenCarriageController()
        {
            RequireLocalSource();
            ToyTrainView view = CreateSeatTrain(true, out Animator animator, out Transform rig);
            animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>(CatRigPresentation.ControllerResourcePath);
            animator.Rebind();
            Assert.That(animator.GetComponent<CatRigPresentation>().OpenCarriageMotionInstalled, Is.False);
            view.ApplyPresentation(CatPresentationState.RideIdle, 1f, true);
            SampleTrainLateUpdate(view);
            AssertStaticMotionOff(view, animator, rig, false, SampleStaticReference(false));
        }

        private Quaternion[] SampleStaticReference(bool seated)
        {
            GameObject reference = CloneThroughCatalog();
            try
            {
                Animator animator = reference.GetComponentInChildren<Animator>(true);
                AnimationClip clip = seated
                    ? Resources.Load<AnimationClip>("CatMotion/OpenCarriage/Cat_Ride")
                    : animator.runtimeAnimatorController.animationClips.Single(c => c.name == "Cat_IdleSit");
                Assert.That(clip, Is.Not.Null);
                clip.SampleAnimation(animator.gameObject, 0f);
                return BoneRotations(animator.transform);
            }
            finally { Object.DestroyImmediate(reference); }
        }

        private static void AssertStaticMotionOff(ToyTrainView view, Animator animator, Transform rig,
            bool seated, Quaternion[] expected, bool hidden = false)
        {
            Assert.That(animator.gameObject.activeInHierarchy, Is.EqualTo(!hidden));
            // Disabling this Animator clears its state info but preserves its sampled bones.
            // Visible callers must still prove both the selected state and frozen phase.
            if (!hidden)
            {
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(
                    seated ? "Base Layer.Cat_Ride" : "Base Layer.Cat_IdleSit"), Is.True);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(0f).Within(.00001f));
            }
            Assert.That(animator.speed, Is.Zero);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(rig.localPosition, Is.EqualTo(new Vector3(0f, 0f, seated ? .0983f : 0f)));
            AssertRotations(BoneRotations(animator.transform), expected);
            CatRigPresentation motion = animator.GetComponent<CatRigPresentation>();
            Assert.That(motion.HeadTransform.localScale, Is.EqualTo(motion.SourceHeadScale * 1.28f));
            Assert.That(view.GetComponentInChildren<Animator>(true), Is.SameAs(animator));
        }

        [Test]
        public void OriginalCarriageDepthBlendsActualAnimatorCrossfadeOnTheSameActor()
        {
            RequireLocalSource();
            ToyTrainView view = CreateSeatTrain(true, out Animator animator, out Transform rig);
            view.ApplyPresentation(CatPresentationState.RideIdle, 0f, false);
            Assert.That(rig.localPosition.z, Is.EqualTo(.0983f));
            animator.CrossFadeInFixedTime("Base Layer.Cat_IdleSit", .2f, 0, 0f);
            animator.Update(.05f);
            Assert.That(animator.IsInTransition(0), Is.True, "exercise a real blend, not a synthetic state label");
            SampleTrainLateUpdate(view);
            float middle = rig.localPosition.z;
            Assert.That(middle, Is.InRange(.001f, .097f));
            animator.Update(.05f);
            SampleTrainLateUpdate(view);
            Assert.That(rig.localPosition.z, Is.LessThan(middle));
            animator.Update(.2f);
            SampleTrainLateUpdate(view);
            Assert.That(rig.localPosition, Is.EqualTo(Vector3.zero));
            animator.CrossFadeInFixedTime("Base Layer.Cat_Ride", .2f, 0, 0f);
            animator.Update(.05f);
            SampleTrainLateUpdate(view);
            Assert.That(rig.localPosition.z, Is.InRange(.001f, .097f));
            animator.Update(.2f);
            SampleTrainLateUpdate(view);
            Assert.That(rig.localPosition.z, Is.EqualTo(.0983f));
            Assert.That(view.GetComponentInChildren<Animator>(true), Is.SameAs(animator));
        }

        [Test]
        public void OriginalCarriageRiderStaysInSeatWhileBonesAnimateAndFallbackAndPlatformStillBob()
        {
            RequireLocalSource();
            ToyTrainView view = CreateSeatTrain(true, out Animator animator, out Transform rig);
            ToyTrainView fallback = CreateSeatTrain(false, out Animator fallbackAnimator, out _);
            Transform cat = view.transform.Find("Carriage/Cat"), pin = view.transform.Find("Carriage/Pin");
            Transform fallbackCat = fallback.transform.Find("Carriage/Cat");
            Vector3 catSeat = cat.localPosition, pinSeat = pin.localPosition;
            Vector3 anchor = view.transform.localPosition;
            Quaternion firstBody = Quaternion.identity;
            float bodyMovement = 0f;
            foreach (float time in new[] { 0f, .25f, .8f })
            {
                view.ApplyPresentation(CatPresentationState.RideIdle, time, false);
                animator.Update(.2f); SampleTrainLateUpdate(view);
                fallback.ApplyPresentation(CatPresentationState.RideIdle, time, false);
                fallbackAnimator.Update(.2f); SampleTrainLateUpdate(fallback);
                Assert.That(Vector3.Distance(fallbackCat.localPosition, catSeat), Is.GreaterThan(.004f),
                    "the independent unchanged fallback must exhibit real rigid bob at this clock");
                Assert.That(cat.localPosition, Is.EqualTo(catSeat), "rigid bob would slide feet through the original cart walls");
                Assert.That(pin.localPosition, Is.EqualTo(pinSeat), "pin follows the same bob-free seat position");
                Assert.That(view.transform.localPosition, Is.EqualTo(anchor));
                Quaternion body = animator.transform.Find(BodyPath).localRotation;
                if (time == 0f) firstBody = body; else bodyMovement = Mathf.Max(bodyMovement, Quaternion.Angle(firstBody, body));
            }
            Assert.That(bodyMovement, Is.GreaterThan(.1f), "the actual authored Ride breathing must remain animated");
            float phase = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            view.ApplyPresentation(CatPresentationState.WaitingIdle, 0f, false, 2f, false);
            SampleTrainLateUpdate(view);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(phase));
            Assert.That(cat.localPosition, Is.EqualTo(catSeat));
            foreach (float time in new[] { .1f, .7f })
            {
                view.ApplyPresentation(CatPresentationState.WaitingIdle, 1f, false, time, false);
                fallback.ApplyPresentation(CatPresentationState.WaitingIdle, 1f, false, time, false);
                animator.Update(0f); fallbackAnimator.Update(0f);
                SampleTrainLateUpdate(view); SampleTrainLateUpdate(fallback);
                Assert.That(cat.localPosition, Is.EqualTo(fallbackCat.localPosition), "actual source platform movement is unchanged");
                Assert.That(pin.localPosition, Is.EqualTo(fallback.transform.Find("Carriage/Pin").localPosition));
            }
            view.ApplyPresentation(CatPresentationState.Hidden, 3f, false);
            view.SyncSlot(99, CatColor.Blue);
            view.ApplyPresentation(CatPresentationState.WaitingIdle, 0f, false, 3f, false);
            SampleTrainLateUpdate(view);
            Assert.That(cat.localPosition, Is.EqualTo(catSeat), "a reused slot cannot retain platform displacement or rigid bob");
            Assert.That(pin.localPosition, Is.EqualTo(pinSeat));
            Assert.That(rig.localPosition.z, Is.EqualTo(.0983f));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void OriginalCarriageRigidBobFadesWithEvaluatedBoardAndAlightWithoutAccumulation(int framesPerSecond)
        {
            RequireLocalSource();
            ToyTrainView view = CreateSeatTrain(true, out Animator animator, out _);
            ToyTrainView fallback = CreateSeatTrain(false, out _, out _);
            Transform cat = view.transform.Find("Carriage/Cat"), pin = view.transform.Find("Carriage/Pin");
            Vector3 seat = cat.localPosition, label = pin.localPosition;
            fallback.ApplyPresentation(CatPresentationState.RideIdle, .25f, false);
            Vector3 fullBob = fallback.transform.Find("Carriage/Cat").localPosition - seat;
            Assert.That(fullBob.magnitude, Is.GreaterThan(.004f));
            foreach (CatPresentationState state in new[] { CatPresentationState.Board, CatPresentationState.Alight })
            {
                view.ApplyPresentation(state, .25f, false);
                float previous = Vector3.Distance(cat.localPosition, seat);
                Assert.That(previous, Is.EqualTo(state == CatPresentationState.Board ? fullBob.magnitude : 0f).Within(.000001f));
                float elapsed = 0f;
                bool sawPartial = false;
                while (elapsed < .18f)
                {
                    float delta = Mathf.Min(1f / framesPerSecond, .18f - elapsed);
                    animator.Update(delta); SampleTrainLateUpdate(view);
                    float amount = Vector3.Distance(cat.localPosition, seat);
                    Assert.That(amount, Is.InRange(0f, fullBob.magnitude + .000001f));
                    Assert.That(state == CatPresentationState.Board ? amount <= previous + .000001f : amount >= previous - .000001f,
                        Is.True, "pose evaluation fades rigid translation toward or away from the physical seat");
                    sawPartial |= amount > .0001f && amount < fullBob.magnitude - .0001f;
                    Assert.That(Vector3.Distance(pin.localPosition - label, cat.localPosition - seat), Is.LessThan(.000001f));
                    Vector3 sampledCat = cat.localPosition, sampledPin = pin.localPosition;
                    for (int repeat = 0; repeat < 3; repeat++)
                    {
                        view.ApplyPresentation(state, .25f, false);
                        SampleTrainLateUpdate(view);
                        Assert.That(cat.localPosition, Is.EqualTo(sampledCat));
                        Assert.That(pin.localPosition, Is.EqualTo(sampledPin));
                    }
                    previous = amount; elapsed += delta;
                }
                Assert.That(sawPartial, Is.True, "an endpoint-only switch would visibly snap instead of fading");
                Assert.That(previous, Is.EqualTo(state == CatPresentationState.Board ? 0f : fullBob.magnitude).Within(.000001f));
            }
        }

        [Test]
        public void OriginalCarriageRigidBobFollowsActualAnimatorCrossfadeOnTheSameActor()
        {
            RequireLocalSource();
            ToyTrainView view = CreateSeatTrain(true, out Animator animator, out _);
            Transform cat = view.transform.Find("Carriage/Cat"), pin = view.transform.Find("Carriage/Pin");
            Vector3 seat = cat.localPosition, label = pin.localPosition;
            // This overload has no platform path; the independent neutral state exposes the full bob.
            view.ApplyPresentation(CatPresentationState.Celebrate, .25f, false);
            Vector3 fullBob = cat.localPosition - seat;
            Assert.That(fullBob.magnitude, Is.GreaterThan(.004f));
            view.ApplyPresentation(CatPresentationState.RideIdle, .25f, false);
            Assert.That(cat.localPosition, Is.EqualTo(seat));
            animator.CrossFadeInFixedTime("Base Layer.Cat_IdleSit", .2f, 0, 0f);
            animator.Update(.05f); Assert.That(animator.IsInTransition(0), Is.True);
            SampleTrainLateUpdate(view);
            float middle = Vector3.Distance(cat.localPosition, seat);
            Assert.That(middle, Is.InRange(.0001f, fullBob.magnitude - .0001f));
            animator.Update(.05f); SampleTrainLateUpdate(view);
            Assert.That(Vector3.Distance(cat.localPosition, seat), Is.GreaterThan(middle));
            animator.Update(.2f); SampleTrainLateUpdate(view);
            Assert.That(Vector3.Distance(cat.localPosition - seat, fullBob), Is.LessThan(.000001f));
            animator.CrossFadeInFixedTime("Base Layer.Cat_Ride", .2f, 0, 0f);
            animator.Update(.05f); SampleTrainLateUpdate(view);
            Assert.That(Vector3.Distance(cat.localPosition, seat), Is.InRange(.0001f, fullBob.magnitude - .0001f));
            animator.Update(.2f); SampleTrainLateUpdate(view);
            Assert.That(cat.localPosition, Is.EqualTo(seat)); Assert.That(pin.localPosition, Is.EqualTo(label));
            Assert.That(view.GetComponentInChildren<Animator>(true), Is.SameAs(animator));
        }

        [TestCase("absent")]
        [TestCase("missing-ride")]
        [TestCase("changed-idle")]
        [TestCase("foreign-base")]
        public void OpenCarriageOverrideRejectsPartialOrOutOfScopeChanges(string defect)
        {
            RequireLocalSource();
            var installed = Resources.Load<AnimatorOverrideController>(CatRigPresentation.OpenCarriageControllerResourcePath);
            Assert.That(installed, Is.Not.Null, "generate the owned carriage override before validating admission");
            var baseline = Resources.Load<RuntimeAnimatorController>(CatRigPresentation.ControllerResourcePath);
            var clone = Object.Instantiate(installed);
            try
            {
                var method = typeof(CatRigPresentation).GetMethod("HasOpenCarriageClips",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);
                Assert.That((bool)method.Invoke(null, new object[] { clone, baseline, null }), Is.True,
                    "an unchanged candidate is the positive control before corrupting only a disposable override");
                var pairs = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
                clone.GetOverrides(pairs);
                if (defect == "missing-ride") clone["Cat_Ride"] = null;
                if (defect == "changed-idle") clone["Cat_IdleSit"] = pairs.Single(p => p.Key.name == "Cat_Ride").Value;
                RuntimeAnimatorController expectedBase = defect == "foreign-base"
                    ? _source.GetComponentInChildren<Animator>().runtimeAnimatorController : baseline;
                Assert.That((bool)method.Invoke(null, new object[] { defect == "absent" ? null : clone, expectedBase, null }), Is.False);
                Assert.That(Resources.Load<AnimatorOverrideController>(CatRigPresentation.OpenCarriageControllerResourcePath), Is.SameAs(installed));
            }
            finally { Object.DestroyImmediate(clone); }
        }

        private static void SampleTrainLateUpdate(ToyTrainView view)
        {
            // EditMode does not dispatch player lifecycle messages. Invoke the same production
            // callback body directly after the real Animator evaluation; do not enable edit execution.
            var callback = typeof(ToyTrainView).GetMethod("LateUpdate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(callback, Is.Not.Null);
            callback.Invoke(view, null);
        }

        private ToyTrainView CreateSeatTrain(bool original, out Animator animator, out Transform rig)
        {
            GameObject prefab = Resources.Load<GameObject>(CarriageModelCatalog.ResourcePath);
            Assert.That(prefab, Is.Not.Null, "import the actual owned open carriage for this integration test");
            var view = ToyTrainView.Create(_host.transform, original ? "original-seat" : "prior-fallback", new[] { 0 }, new[] { 1 },
                CatModelCatalog.LoadResources(), original ? new CarriageModelCatalog(prefab) : CarriageModelCatalog.Empty);
            Assert.That(view.OriginalCarriageAdmitted, Is.EqualTo(original), view.CarriageFallbackReason);
            view.SyncSlot(42, CatColor.Red);
            animator = view.GetComponentInChildren<Animator>(true);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            rig = animator.transform;
            while (rig.parent != view.transform.Find("Carriage/Cat")) rig = rig.parent;
            return view;
        }

        private void AssertDiscriminatingRidePose(Animator actual, float seconds)
        {
            // Independently sample the committed clip on a separate admitted clone, rather than
            // accepting a state hash whose Animator has not yet evaluated its transforms.
            GameObject reference = CloneThroughCatalog();
            try
            {
                Animator expected = reference.GetComponentInChildren<Animator>(true);
                AnimationClip ride = expected.runtimeAnimatorController.animationClips.Single(c => c.name == "Cat_Ride");
                AnimationClip idle = expected.runtimeAnimatorController.animationClips.Single(c => c.name == "Cat_IdleSit");
                idle.SampleAnimation(expected.gameObject, 0f);
                Quaternion neutral = expected.transform.Find(BodyPath).localRotation;
                ride.SampleAnimation(expected.gameObject, seconds);
                Quaternion seated = expected.transform.Find(BodyPath).localRotation;
                Assert.That(Quaternion.Angle(seated, neutral), Is.GreaterThan(25f), "test clips must discriminate seated and neutral torso");
                Assert.That(Quaternion.Angle(actual.transform.Find(BodyPath).localRotation, seated), Is.LessThan(.05f),
                    "real Animator must evaluate the same Ride torso before testing continuity");
                Assert.That(actual.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Cat_Ride"), Is.True);
            }
            finally { Object.DestroyImmediate(reference); }
        }

        private static void AssertRideStateAndPhase(Animator animator, float phase)
        {
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Cat_Ride"), Is.True,
                "a cat waiting inside its carriage keeps the seated Ride clip");
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(phase).Within(.00001f),
                "a stop/reverse/state notification must not restart the same seated loop");
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
