using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.EditMode.Presentation
{
    public sealed class CatRigMotionAuthoringTests
    {
        private const string Body = "Armature/tripo::Root/tripo::Head_0";
        private const string Head = Body + "/tripo::Head_1";
        private const string EarA = Head + "/tripo::Head_2/bone_4";
        private const string EarB = Head + "/tripo::Head_2/tripo::Head_3";
        private const string Tail = Body + "/bone_9/bone_12";
        private const string Paw = Body + "/bone_21/tripo::0_Left_Limb_0";
        private GameObject _root;
        private Animator _animator;
        private AnimationClip _neutral;
        private AnimationClip _walk;
        private AnimationClip[] _clips = Array.Empty<AnimationClip>();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("disposable authoring fixture");
            _root.transform.rotation = Quaternion.Euler(17f, 63f, -9f);
            _animator = _root.AddComponent<Animator>();
            _animator.enabled = false;
            foreach (string path in new[] { Body, Head, EarA, EarB, Tail, Paw }) AddPath(path);
            _root.transform.Find("Armature/tripo::Root").localRotation =
                Quaternion.Euler(71f, -13f, 38f);
            _root.transform.Find(Body).localRotation = Quaternion.Euler(4f, 19f, -12f);
            _root.transform.Find(Head).localRotation = Quaternion.Euler(-21f, 3f, 8f);
            _root.transform.Find(Paw).localPosition = new Vector3(0.3f, 0.12f, -0.2f);
            _neutral = new AnimationClip { name = "Cat_IdleSit" };
            _walk = new AnimationClip { name = "Cat_Walk" };
            // The sampled original idle deliberately differs from this fixture's bind pose.
            SetVector(_neutral, Paw, "m_LocalPosition", new Vector3(0.23f, 0.09f, -0.18f));
            SetRotation(_neutral, Body, Quaternion.Euler(8f, -7f, 23f));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (AnimationClip clip in _clips) Object.DestroyImmediate(clip);
            Object.DestroyImmediate(_neutral);
            Object.DestroyImmediate(_walk);
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void BoardSamplesOriginalIdleAndAppliesSeatAroundAnimatorAxis()
        {
            _neutral.SampleAnimation(_root, 0f);
            Quaternion bodyBefore = _root.transform.Find(Body).rotation;
            Quaternion headBefore = _root.transform.Find(Head).rotation;
            _clips = Generate();
            AnimationClip board = Clip("Cat_Board");
            board.SampleAnimation(_root, 0f);
            Assert.That(_root.transform.Find(Paw).localPosition,
                Is.EqualTo(new Vector3(0.23f, 0.09f, -0.18f)),
                "neutral must come from original IdleSit sampling, not the imported bind pose");
            AssertRotation(_root.transform.Find(Body).rotation, bodyBefore);
            board.SampleAnimation(_root, board.length);
            AssertRotation(_root.transform.Find(Body).rotation,
                Quaternion.AngleAxis(-35f, _root.transform.forward) * bodyBefore);
            AssertRotation(_root.transform.Find(Head).rotation,
                Quaternion.AngleAxis(-3f, _root.transform.forward) * headBefore);
        }

        [Test]
        public void AllClipsResetWalkRotationsAndPositionsWithoutRootOrScaleCurves()
        {
            _clips = Generate();
            string[] paths = _root.GetComponentsInChildren<Transform>()
                .Where(t => t != _root.transform)
                .Select(t => AnimationUtility.CalculateTransformPath(t, _root.transform)).ToArray();
            foreach (AnimationClip clip in _clips)
            {
                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
                Assert.That(bindings.Any(b => b.path.Length == 0), Is.False);
                Assert.That(bindings.Any(b => b.propertyName.Contains("Scale")), Is.False);
                Assert.That(clip.hasRootCurves, Is.False);
                Assert.That(AnimationUtility.GetAnimationEvents(clip), Is.Empty);
                foreach (string path in paths)
                {
                    Assert.That(bindings.Count(b => b.path == path), Is.EqualTo(7), path);
                    _root.transform.Find(path).localRotation = Quaternion.Euler(43f, 67f, 91f);
                    _root.transform.Find(path).localPosition = Vector3.one * 123f;
                }
                Vector3 enlargedHead = new Vector3(1.28f, 1.28f, 1.28f);
                _root.transform.Find(Head).localScale = enlargedHead;
                clip.SampleAnimation(_root, clip.length * 0.5f);
                Assert.That(_root.transform.Find(Paw).localPosition,
                    Is.EqualTo(new Vector3(0.23f, 0.09f, -0.18f)));
                Assert.That(_root.transform.Find(Paw).localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(_root.transform.Find(Head).localScale, Is.EqualTo(enlargedHead));
            }
        }

        [Test]
        public void TimingsAndEndpointsKeepTransitionsAndLoopsContinuous()
        {
            _clips = Generate();
            string[] names = { "Cat_IdleSit", "Cat_Ride", "Cat_Board", "Cat_Alight", "Cat_Celebrate" };
            float[] durations = { 3.2f, 1.6f, 0.18f, 0.18f, 0.48f };
            for (int index = 0; index < names.Length; index++)
            {
                AnimationClip clip = Clip(names[index]);
                Assert.That(clip.length, Is.EqualTo(durations[index]).Within(0.00001f));
                Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime,
                    Is.EqualTo(index < 2));
            }
            foreach (string name in new[] { "Cat_IdleSit", "Cat_Ride", "Cat_Celebrate" })
            {
                AnimationClip clip = Clip(name);
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                {
                    // SampleAnimation at a loop's exact length can wrap to t=0. Read authored
                    // endpoint keys directly so a discontinuous last key cannot pass by wrapping.
                    Keyframe[] keys = AnimationUtility.GetEditorCurve(clip, binding).keys;
                    Assert.That(keys.First().time, Is.EqualTo(0f));
                    Assert.That(keys.Last().time, Is.EqualTo(clip.length).Within(0.00001f));
                    Assert.That(keys.Last().value, Is.EqualTo(keys.First().value).Within(0.000001f),
                        name + " " + binding.path + " " + binding.propertyName);
                }
            }
            foreach (string path in new[] { Body, Head, EarA, EarB, Tail, Paw })
            {
                Quaternion seat = SampleRotation(Clip("Cat_Ride"), 0f, path);
                AssertRotation(SampleRotation(Clip("Cat_Ride"), Clip("Cat_Ride").length, path), seat);
                AssertRotation(SampleRotation(Clip("Cat_Board"), 0.18f, path), seat);
                AssertRotation(SampleRotation(Clip("Cat_Alight"), 0f, path), seat);
                AssertRotation(SampleRotation(Clip("Cat_Board"), 0f, path),
                    SampleRotation(Clip("Cat_Alight"), 0.18f, path));
            }
            Assert.That(Quaternion.Angle(SampleRotation(Clip("Cat_Celebrate"), 0f, Body),
                SampleRotation(Clip("Cat_Celebrate"), 0.22f, Body)), Is.GreaterThan(3f),
                "celebration must actually rear through the proven body control");
            foreach (string path in new[] { Head, Tail, EarA, EarB })
                Assert.That(Quaternion.Angle(SampleRotation(Clip("Cat_Celebrate"), 0f, path),
                    SampleRotation(Clip("Cat_Celebrate"), 0.24f, path)), Is.GreaterThan(0.5f),
                    "celebration retains its distinct head/tail/ear accents: " + path);
        }

        [Test]
        public void IdleKeepsSourceNeutralChestAtLoopEndpointsWhileBreathingAndAccentsRemainVisible()
        {
            _neutral.SampleAnimation(_root, 0f);
            Transform body = _root.transform.Find(Body), head = _root.transform.Find(Head);
            Transform tail = _root.transform.Find(Tail);
            Quaternion neutralBody = body.rotation, neutralHead = head.rotation, neutralTail = tail.rotation;
            _clips = Generate();
            AnimationClip idle = Clip("Cat_IdleSit");
            foreach (float time in new[] { 0f, idle.length })
            {
                idle.SampleAnimation(_root, time);
                AssertRotation(body.rotation, neutralBody);
                AssertRotation(head.rotation, neutralHead);
                AssertRotation(tail.rotation, Quaternion.AngleAxis(45f, _root.transform.forward) * neutralTail);
            }
            // These samples exercise the generated curves on a rotated hierarchy, rather
            // than reproducing the generator's sine/pulse formula or accepting distinct keys alone.
            foreach (float phase in new[] { .25f, .75f })
            {
                idle.SampleAnimation(_root, idle.length * phase);
                Assert.That(Quaternion.Angle(body.rotation, neutralBody), Is.InRange(.25f, 1.5f),
                    "breathing moves the torso gently around the original chest pose");
                Assert.That(Quaternion.Angle(head.rotation, neutralHead), Is.InRange(.1f, 2f),
                    "head sway remains visible without the seated counter-rotation");
            }
            foreach (var accent in new[] { (Tail, .25f), (EarA, .64f), (EarB, .715f) })
            {
                Quaternion start = SampleRotation(idle, 0f, accent.Item1);
                Quaternion moving = SampleRotation(idle, idle.length * accent.Item2, accent.Item1);
                Assert.That(Quaternion.Angle(start, moving), Is.InRange(.5f, 5f), accent.Item1);
            }
        }

        [TestCase(0f)]
        [TestCase(0.48f)]
        public void CelebrateEndpointsMatchTheSourceNeutralPlatformPose(float time)
        {
            // The expected pose is captured before invoking the generator, from the original
            // sampled idle on this rotated hierarchy; no generated clip defines the expectation.
            _neutral.SampleAnimation(_root, 0f);
            Transform body = _root.transform.Find(Body), head = _root.transform.Find(Head);
            Transform tail = _root.transform.Find(Tail);
            Quaternion neutralBody = body.rotation, neutralHead = head.rotation;
            Quaternion raisedTail = Quaternion.AngleAxis(45f, _root.transform.forward) * tail.rotation;
            _clips = Generate();
            Clip("Cat_Celebrate").SampleAnimation(_root, time);
            AssertRotation(body.rotation, neutralBody);
            AssertRotation(head.rotation, neutralHead);
            AssertRotation(tail.rotation, raisedTail);
            // These platform-facing body/head endpoints must agree as well. Ride keeps its
            // distinct carriage seat; the original provider walk is referenced unchanged.
            foreach (var endpoint in new[] { ("Cat_IdleSit", 0f), ("Cat_Board", 0f), ("Cat_Alight", 0.18f) })
            {
                Clip(endpoint.Item1).SampleAnimation(_root, endpoint.Item2);
                AssertRotation(body.rotation, neutralBody);
                AssertRotation(head.rotation, neutralHead);
            }
        }

        [Test]
        public void UnexpectedProviderScaleAnimationIsRejectedBeforeCreatingClips()
        {
            _walk.SetCurve(Head, typeof(Transform), "m_LocalScale.x",
                AnimationCurve.Linear(0f, 1f, 1f, 1.1f));
            var error = Assert.Throws<TargetInvocationException>(() => Generate());
            Assert.That(error.InnerException.Message, Does.Contain("scale"));
        }

        [Test]
        public void TinyImporterScaleDriftDoesNotBlockAuthoringOrCreateScaleCurves()
        {
            _walk.SetCurve(Head, typeof(Transform), "m_LocalScale.x",
                AnimationCurve.Linear(0f, 0.99999994f, 1f, 1.000001f));
            _clips = Generate();
            Assert.That(_clips, Has.Length.EqualTo(5));
            Assert.That(_clips.SelectMany(AnimationUtility.GetCurveBindings)
                .Any(b => b.propertyName.Contains("Scale")), Is.False);
        }

        [Test]
        public void RepeatedGenerationRestoresBaselineAndProducesIdenticalCurves()
        {
            _neutral.SampleAnimation(_root, 0f);
            Quaternion original = _root.transform.Find(Body).localRotation;
            _clips = Generate();
            AssertRotation(_root.transform.Find(Body).localRotation, original);
            AnimationClip[] repeated = Generate();
            try
            {
                AssertRotation(_root.transform.Find(Body).localRotation, original);
                for (int clip = 0; clip < _clips.Length; clip++)
                    foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(_clips[clip]))
                        Assert.That(AnimationUtility.GetEditorCurve(repeated[clip], binding).keys,
                            Is.EqualTo(AnimationUtility.GetEditorCurve(_clips[clip], binding).keys),
                            _clips[clip].name + " " + binding.path + " " + binding.propertyName);
            }
            finally { foreach (AnimationClip clip in repeated) Object.DestroyImmediate(clip); }
        }

        [Test]
        public void OpenCarriageVariantsMatchCalibratedAxesAndRejoinUnchangedPlatformEndpoints()
        {
            var controls = new[] {
                (Body + "/bone_21/tripo::0_Right_Limb_0/bone_27", Vector3.right, 36f),
                (Paw + "/tripo::0_Left_Limb_1/tripo::0_Left_Limb_2", Vector3.up, 12f),
                (Tail + "/tripo::Tail_0", Vector3.right, 30f) };
            foreach (var control in controls) AddPath(control.Item1);
            _clips = Generate();
            var before = _clips.Select(clip => AnimationUtility.GetCurveBindings(clip)
                .Select(binding => AnimationUtility.GetEditorCurve(clip, binding).keys).ToArray()).ToArray();
            var variants = (AnimationClip[])Invoke("GenerateOpenCarriageClips", _animator, _neutral, _walk);
            try
            {
                Assert.That(variants.Select(c => c.name), Is.EquivalentTo(new[] { "Cat_Ride", "Cat_Board", "Cat_Alight" }));
                for (int index = 0; index < _clips.Length; index++)
                    Assert.That(AnimationUtility.GetCurveBindings(_clips[index])
                        .Select(binding => AnimationUtility.GetEditorCurve(_clips[index], binding).keys).ToArray(),
                        Is.EqualTo(before[index]), "fallback/profile clips are not modified by variant generation");
                AnimationClip ride = variants.Single(c => c.name == "Cat_Ride");
                Clip("Cat_Ride").SampleAnimation(_root, .4f);
                Quaternion[] baseline = controls.Select(c => _root.transform.Find(c.Item1).rotation).ToArray();
                Quaternion body = _root.transform.Find(Body).localRotation, head = _root.transform.Find(Head).localRotation;
                ride.SampleAnimation(_root, .4f);
                for (int i = 0; i < controls.Length; i++)
                    AssertRotation(_root.transform.Find(controls[i].Item1).rotation,
                        Quaternion.AngleAxis(controls[i].Item3, _root.transform.TransformDirection(controls[i].Item2)) * baseline[i]);
                AssertRotation(_root.transform.Find(Body).localRotation, body);
                AssertRotation(_root.transform.Find(Head).localRotation, head);
                foreach (AnimationClip clip in variants)
                {
                    Assert.That(clip.hasRootCurves, Is.False);
                    Assert.That(AnimationUtility.GetCurveBindings(clip).Any(b => b.path.Length == 0 || b.propertyName.Contains("Scale")), Is.False);
                }
                string[] paths = _root.GetComponentsInChildren<Transform>().Where(t => t != _root.transform)
                    .Select(t => AnimationUtility.CalculateTransformPath(t, _root.transform)).ToArray();
                foreach (string path in paths)
                {
                    AssertRotation(SampleRotation(variants.Single(c => c.name == "Cat_Board"), 0f, path),
                        SampleRotation(Clip("Cat_Board"), 0f, path));
                    AssertRotation(SampleRotation(variants.Single(c => c.name == "Cat_Alight"), .18f, path),
                        SampleRotation(Clip("Cat_Alight"), .18f, path));
                    Quaternion seated = SampleRotation(ride, 0f, path);
                    AssertRotation(SampleRotation(variants.Single(c => c.name == "Cat_Board"), .18f, path), seated);
                    AssertRotation(SampleRotation(variants.Single(c => c.name == "Cat_Alight"), 0f, path), seated);
                }
                foreach (var binding in AnimationUtility.GetCurveBindings(ride))
                {
                    var keys = AnimationUtility.GetEditorCurve(ride, binding).keys;
                    Assert.That(keys.Last().value, Is.EqualTo(keys.First().value).Within(.000001f),
                        "inspect authored loop endpoints directly, since SampleAnimation can wrap");
                }
            }
            finally { foreach (AnimationClip clip in variants) Object.DestroyImmediate(clip); }
        }

        [Test]
        public void ControllerUsesOriginalWalkReferenceAndSixExactStates()
        {
            _clips = Generate();
            var controller = new AnimatorController();
            controller.AddLayer("Base Layer");
            try
            {
                Invoke("ConfigureController", controller, _clips, _walk);
                AnimatorStateMachine machine = controller.layers.Single().stateMachine;
                Assert.That(machine.states.Select(c => c.state.name), Is.EquivalentTo(
                    new[] { "Cat_IdleSit", "Cat_Walk", "Cat_Board", "Cat_Alight", "Cat_Celebrate", "Cat_Ride" }));
                Assert.That(machine.states.Single(c => c.state.name == "Cat_Walk").state.motion,
                    Is.SameAs(_walk));
                Assert.That(machine.defaultState.name, Is.EqualTo("Cat_IdleSit"));
                Assert.That(machine.anyStateTransitions, Is.Empty);
                foreach (var child in machine.states) Assert.That(child.state.transitions, Is.Empty);
            }
            finally
            {
                AnimatorStateMachine machine = controller.layers[0].stateMachine;
                foreach (var child in machine.states) Object.DestroyImmediate(child.state);
                Object.DestroyImmediate(machine);
                Object.DestroyImmediate(controller);
            }
        }

        [TestCase("Assets/Art/Generated/incoming/cat-rig/Resources/CatRigs/Cat_IdleSit.anim")]
        [TestCase("Assets/Art/Original/CatMotion/Resources/CatMotion/../Cat_IdleSit.anim")]
        [TestCase("Assets/Art/Original/CatMotion/Resources/CatMotion/unowned.anim")]
        public void OutputGuardRejectsPaidTraversalAndUnknownPaths(string path)
        {
            var error = Assert.Throws<TargetInvocationException>(() => Invoke("RequireOwnedPath", path));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
        }

        private AnimationClip[] Generate() =>
            (AnimationClip[])Invoke("GenerateClips", _animator, _neutral, _walk);
        private AnimationClip Clip(string name) => _clips.Single(c => c.name == name);
        private static object Invoke(string method, params object[] args)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("CatMetro.EditorTools.CatRigMotionAuthoring"))
                .FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null, "Editor generator must compile into the project");
            MethodInfo info = type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, method);
            return info.Invoke(null, args);
        }
        private void AddPath(string path)
        {
            Transform parent = _root.transform;
            foreach (string part in path.Split('/'))
            {
                Transform child = parent.Find(part);
                if (child == null)
                {
                    child = new GameObject(part).transform;
                    child.SetParent(parent, false);
                }
                parent = child;
            }
        }
        private Quaternion SampleRotation(AnimationClip clip, float time, string path)
        {
            clip.SampleAnimation(_root, time);
            return _root.transform.Find(path).localRotation;
        }
        private static void AssertRotation(Quaternion actual, Quaternion expected) =>
            Assert.That(Quaternion.Angle(actual, expected), Is.LessThan(0.05f));
        private static void SetVector(AnimationClip clip, string path, string property, Vector3 value)
        {
            foreach (int axis in new[] { 0, 1, 2 })
                clip.SetCurve(path, typeof(Transform), property + "." + "xyz"[axis],
                    AnimationCurve.Constant(0f, 0.1f, value[axis]));
        }
        private static void SetRotation(AnimationClip clip, string path, Quaternion value)
        {
            foreach (int axis in new[] { 0, 1, 2, 3 })
                clip.SetCurve(path, typeof(Transform), "m_LocalRotation." + "xyzw"[axis],
                    AnimationCurve.Constant(0f, 0.1f, value[axis]));
        }
    }
}
