using System.Collections;
using System.IO;
using System.Linq;
using CatMetro.Bootstrap;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Presentation.Fx;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Theme;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.PlayMode
{
    public sealed class FailureMoodTests
    {
        private GameRoot _root;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 0f;
            var level = LevelImporter.Import(File.ReadAllBytes(Path.Combine(
                UnityEngine.Application.streamingAssetsPath, "content/levels/L005.json")));
            Assert.That(level.Ok, Is.True);
            _root = GameRoot.LaunchWith(level.Value);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
            Time.timeScale = 1f;
        }

        private void Fail(bool train = false)
        {
            if (train)
            {
                _root.Session.State.Trains[0] = new TrainSlot
                { Color = CatColor.Red, Id = 1, NodeId = 0, State = TrainState.AtNode };
                _root.Session.State.NodeQueueCounts[0] = 1;
                _root.Session.State.NodeQueueSlots[0][0] = 1;
                _root.View.UpdateFrom(_root.Session);
            }
            _root.Session.State.Outcome = SimOutcome.MakeFailed(FailReason.TimeOut);
            // Observe the edge without waiting a variable-duration editor frame: the
            // shared unscaled tween clock can then sample the exact 0/.175/.35s poses.
            _root.SendMessage("Update");
            Assert.That(_root.ScreenState, Is.EqualTo("FailureReview"));
        }

        private Volume Mood()
        {
            var volume = _root.CauseCam.GetComponent<Volume>();
            Assert.That(volume, Is.Not.Null, "the failure edge must compose a real URP volume");
            return volume;
        }

        private static void AssertMood(Volume volume, float saturation, float vignette)
        {
            Assert.That(volume.sharedProfile.TryGet<ColorAdjustments>(out var color), Is.True);
            Assert.That(volume.sharedProfile.TryGet<Vignette>(out var shade), Is.True);
            Assert.That(color.saturation.overrideState, Is.True);
            Assert.That(shade.intensity.overrideState, Is.True);
            Assert.That(color.saturation.value, Is.EqualTo(saturation).Within(.001f));
            Assert.That(shade.intensity.value, Is.EqualTo(vignette).Within(.001f));
        }

        [Test]
        public void FailureMood_EasesOver350ms_AndRetryRestoresCameraSettings()
        {
            bool previous = _root.Cam.GetUniversalAdditionalCameraData().renderPostProcessing;
            Fail();
            var volume = Mood();
            Assert.That(volume.weight, Is.EqualTo(1f));
            Assert.That(_root.Cam.GetUniversalAdditionalCameraData().renderPostProcessing, Is.True);
            AssertMood(volume, 0f, .25f);
            var fx = _root.CauseCam.GetComponent<BoardFx>();
            Assert.That(fx, Is.Not.Null);
            fx.Advance(.175f);
            AssertMood(volume, -17.5f, .35f);
            fx.Advance(.175f);
            AssertMood(volume, -35f, .45f);
            _root.Retry();
            _root.GetComponent<ScreenChromeController>().Transition.Advance(.22f, false);
            Assert.That(volume.weight, Is.Zero);
            AssertMood(volume, 0f, .25f);
            Assert.That(_root.Cam.GetUniversalAdditionalCameraData().renderPostProcessing, Is.EqualTo(previous));
        }

        [Test]
        public void MotionOffDuringFailure_SettlesTheMoodWithoutWaiting()
        {
            Fail();
            var volume = Mood();
            _root.MotionOffToggle = true;
            _root.CauseCam.GetComponent<BoardFx>().Advance(0f);
            AssertMood(volume, -35f, .45f);
        }

        [Test]
        public void CauseRing_LeavesItsCentreOpen_AndUsesTheTicketPalette()
        {
            _root.CauseCam.FrameNode("fixture", _root.View.NodeWorldPos(0), true);
            var ring = GameObject.Find("CauseRing");
            var mesh = ring.GetComponent<MeshFilter>().sharedMesh;
            foreach (var point in mesh.vertices)
                Assert.That(new Vector2(point.x, point.z).magnitude, Is.GreaterThan(.45f),
                    "no cap or centre vertex may paint over the causal toy");
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]]; var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                float ab = a.x * b.z - a.z * b.x;
                float bc = b.x * c.z - b.z * c.x;
                float ca = c.x * a.z - c.z * a.x;
                Assert.That((ab >= 0 && bc >= 0 && ca >= 0) || (ab <= 0 && bc <= 0 && ca <= 0),
                    Is.False, "no triangle spans the open centre");
            }
            var paint = new MaterialPropertyBlock();
            ring.GetComponent<Renderer>().GetPropertyBlock(paint);
            var color = paint.GetColor("_BaseColor");
            Assert.That(Vector4.Distance(color, Palette.TicketOrange), Is.LessThan(.00001f),
                "the material block round-trip preserves the palette within float precision");
        }

        [Test]
        public void FailurePuff_ComesFromTheCausalTrainsFunnel_Once()
        {
            Fail(train: true);
            var funnel = _root.View.GetComponentsInChildren<MeshFilter>().Single(m => m.name == "Funnel");
            var mouth = funnel.transform.TransformPoint(new Vector3(0, funnel.sharedMesh.bounds.min.y, 0));
            var bursts = _root.View.GetComponentsInChildren<ParticleSystem>()
                .Where(p => p.name.StartsWith("Board burst") && p.particleCount > 0).ToArray();
            Assert.That(bursts.Length, Is.EqualTo(1), "one causal puff, not a shower over every train");
            Assert.That(bursts[0].particleCount, Is.EqualTo(1));
            Assert.That(Vector3.Distance(bursts[0].transform.position, mouth), Is.LessThan(.001f));
            var tint = bursts[0].main.startColor.color;
            Assert.That(tint.r, Is.EqualTo(tint.g).Within(.001f));
            Assert.That(tint.b, Is.EqualTo(tint.g).Within(.001f));
            Assert.That(bursts[0].main.startSize.constant, Is.GreaterThan(.24f),
                "a single puff must remain visible at the fitted phone framing");
            var particles = new ParticleSystem.Particle[1];
            bursts[0].GetParticles(particles);
            Assert.That(Vector3.Dot(particles[0].velocity, _root.Cam.transform.up), Is.GreaterThan(.35f),
                "the puff must rise clear of the engine instead of wandering behind it");
            Assert.That(Vector3.Dot(particles[0].velocity, -_root.Cam.transform.forward), Is.GreaterThan(.1f));
            _root.SendMessage("Update");
            Assert.That(bursts[0].particleCount, Is.EqualTo(1), "no repeat while reviewing the same failure");
        }

        [Test]
        public void MotionOff_KeepsFailureInformation_WithoutTransientPuff()
        {
            _root.MotionOffToggle = true;
            Fail(train: true);
            AssertMood(Mood(), -35f, .45f);
            Assert.That(_root.CauseCam.RingVisible, Is.True);
            Assert.That(_root.Banner.CurrentText, Is.Not.Empty);
            Assert.That(_root.View.GetComponentsInChildren<ParticleSystem>()
                .Any(p => p.name.StartsWith("Board burst") && p.particleCount > 0), Is.False);
        }

        [TestCase(FailReason.QueueOverflow)]
        [TestCase(FailReason.PlatformOverflow)]
        public void CausalFunnel_UsesQueueOrRefusalEvidenceBeforeAnEarlierInboundTrain(FailReason reason)
        {
            int edge = reason == FailReason.PlatformOverflow ? 1 : 0;
            short node = (short)_root.Session.Level.Graph.EdgeTo[edge];
            _root.Session.State.Trains[0] = new TrainSlot { Color = CatColor.Red, Id = 1, EdgeId = (short)edge,
                NodeId = (short)_root.Session.Level.Graph.EdgeFrom[edge], State = TrainState.OnEdge };
            _root.Session.State.Trains[1] = new TrainSlot { Color = CatColor.Red, Id = 2, EdgeId = (short)edge, NodeId = node,
                State = reason == FailReason.PlatformOverflow ? TrainState.RejectedAtStation : TrainState.AtNode };
            _root.View.UpdateFrom(_root.Session);
            _root.Session.State.Outcome = SimOutcome.MakeFailed(reason);
            AssertFunnel(1, node);
        }

        [Test]
        public void CausalFunnel_DoesNotInventAQueueCauseOnAnApproachingTrain()
        {
            _root.Session.State.Trains[0] = new TrainSlot { Color = CatColor.Red, Id = 1, EdgeId = 0,
                NodeId = 0, State = TrainState.OnEdge };
            _root.View.UpdateFrom(_root.Session);
            _root.Session.State.Outcome = SimOutcome.MakeFailed(FailReason.QueueOverflow);
            Assert.That(_root.View.TryGetCausalFunnel(_root.Session.Level.Graph.EdgeTo[0], out _), Is.False);
        }

        [Test]
        public void CollisionFunnel_UsesCompletedArrivalsBeforeAnEarlierApproachingTrain()
        {
            var graph = _root.Session.Level.Graph;
            for (int i = 0; i < 3; i++)
                _root.Session.State.Trains[i] = new TrainSlot { Color = CatColor.Red, Id = (short)(i + 1), EdgeId = 0,
                    NodeId = 0, State = TrainState.OnEdge,
                    ProgressTicks = (short)(i == 0 ? 0 : graph.EdgeTravelTicks[0]) };
            _root.View.UpdateFrom(_root.Session);
            _root.Session.State.Outcome = SimOutcome.MakeFailed(FailReason.Collision);
            AssertFunnel(1, graph.EdgeTo[0]);
        }

        [Test]
        public void CollisionFunnel_UsesTheOpposingPairBeforeAWaitingTrainAtItsDestination()
        {
            var graph = _root.Session.Level.Graph;
            short node = (short)graph.EdgeTo[1];
            _root.Session.State.Trains[0] = new TrainSlot { Color = CatColor.Red, Id = 1, EdgeId = 1,
                NodeId = node, State = TrainState.AtNode };
            _root.Session.State.Trains[1] = new TrainSlot { Color = CatColor.Red, Id = 2, EdgeId = 1,
                NodeId = (short)graph.EdgeFrom[1], State = TrainState.OnEdge, ProgressTicks = 6 };
            _root.Session.State.Trains[2] = new TrainSlot { Color = CatColor.Red, Id = 3, EdgeId = 1,
                NodeId = node, State = TrainState.OnEdgeReverse, ProgressTicks = 6 };
            _root.View.UpdateFrom(_root.Session);
            _root.Session.State.Outcome = SimOutcome.MakeFailed(FailReason.Collision);
            AssertFunnel(1, node);
        }

        private void AssertFunnel(int slot, int node)
        {
            var filter = _root.View.transform.Find("train:" + slot + "/Engine/Funnel").GetComponent<MeshFilter>();
            var expected = filter.transform.TransformPoint(new Vector3(0, filter.sharedMesh.bounds.min.y, 0));
            Assert.That(_root.View.TryGetCausalFunnel(node, out var actual), Is.True);
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(.001f));
        }

        [Test]
        public void FailureBanner_DropsWithASmallBounce_AndMotionOffUsesTheFinalPose()
        {
            var banner = _root.Banner;
            banner.ShowKey("fail.platformoverflow.generic");
            banner.LayoutForViewport(new Rect(0, 64, 917, 1920), 408f);
            var plaque = (RectTransform)banner.TextTransform.parent;
            float rest = banner.PaintedRectPx.center.y;
            banner.SamplePresentation(0f);
            Assert.That(plaque.anchoredPosition.y, Is.GreaterThan(rest + 50f));
            banner.SamplePresentation(.28f);
            Assert.That(plaque.anchoredPosition.y, Is.LessThan(rest), "a small overshoot softens the landing");
            Assert.That(plaque.anchoredPosition.y, Is.GreaterThan(rest - 20f));
            banner.SamplePresentation(.35f);
            Assert.That(plaque.anchoredPosition.y, Is.EqualTo(rest).Within(.01f));
            _root.MotionOffToggle = true;
            banner.SamplePresentation(0f);
            Assert.That(plaque.anchoredPosition.y, Is.EqualTo(rest).Within(.01f));
        }

#if UNITY_EDITOR
        [Test]
        public void ShippedRenderer_HasPostProcessShadersForTheFailureVolume()
        {
            var renderer = UnityEditor.AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                "Assets/Settings/CatMetro_Renderer.asset");
            Assert.That(renderer.postProcessData, Is.Not.Null,
                "URP silently skips the effects without this renderer resource");
        }
#endif

        [Test]
        public void FailurePostProcessing_ChangesRenderedSaturationAndCornerShade()
        {
            var camera = _root.Cam;
            camera.cullingMask = 0; // A uniform colour isolates the real URP pass from art/lighting.
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.8f, .2f, .1f);
            var before = RenderColourField(camera);
            _root.MotionOffToggle = true;
            Fail();
            var after = RenderColourField(camera);
            Assert.That(Chroma(after[0]), Is.LessThan(Chroma(before[0]) * .85f),
                "setting a Volume value is insufficient: the actual centre pixel must desaturate");
            Assert.That(after[1].grayscale / after[0].grayscale,
                Is.LessThan(before[1].grayscale / before[0].grayscale * .9f),
                "the actual corner must darken relative to the centre");
        }

        private static float Chroma(Color c) => Mathf.Max(c.r, c.g, c.b) - Mathf.Min(c.r, c.g, c.b);

        private static Color[] RenderColourField(Camera camera)
        {
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            var target = new RenderTexture(96, 192, 24);
            var pixels = new Texture2D(96, 192, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 96, 192), 0, 0); pixels.Apply();
                return new[] { pixels.GetPixel(48, 96), pixels.GetPixel(1, 1) };
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }
    }
}
