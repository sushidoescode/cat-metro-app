using System;
using System.Collections;
using System.IO;
using CatMetro.Bootstrap;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Theme;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    public sealed class BoardFeedbackTests
    {
        private GameRoot _root;

        [SetUp] public void SetUp() { Time.timeScale = 0f; GameRoot.DevSkipShippedHome = true; }
        [TearDown] public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root.gameObject);
            Time.timeScale = 1f;
            GameRoot.DevSkipShippedHome = false;
        }

        [UnityTest]
        public IEnumerator WrongStation_RecoilsAndShakesOnce_ShowsTheCatsShape_WithoutChangingState()
        {
            _root = GameRoot.LaunchWith(Level());
            yield return null;
            int slot = ReachFirstRejection(_root);
            var train = _root.View.transform.Find("train:" + slot).GetComponent<ToyTrainView>();
            var engine = train.transform.Find("Engine");
            var rejected = train.transform.Find("Carriage/Pin/Rejected");
            Assert.That(rejected, Is.Not.Null, "the mismatch needs its own visible shape and slash");
            Assert.That(rejected.gameObject.activeSelf, Is.True);
            Assert.That(rejected.Find("Symbol").GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(DestinationShapeMesh.ForShape(DestinationShape.Square)));
            Assert.That(rejected.Find("Cross-bar"), Is.Not.Null);
            Assert.That(rejected.localScale.x, Is.EqualTo(1.6f).Within(0.001f));
            Assert.That(engine.localPosition.magnitude, Is.EqualTo(0.08f).Within(0.001f));
            var station = _root.View.transform.Find("station:" +
                _root.Session.Level.Dto.Nodes.Span[_root.Session.State.Trains[slot].NodeId].Id);
            var plate = station.Find("station:plate-generated");
            var paint = new MaterialPropertyBlock();
            plate.GetComponent<Renderer>().GetPropertyBlock(paint);
            Assert.That(Vector4.Distance(paint.GetColor("_BaseColor"), Palette.SignalRed), Is.LessThan(0.0001f));
            var neutral = plate.localRotation;
            byte[] before = Digest(_root);
            int frame = Time.frameCount;
            _root.View.Fx.Advance(0.05f, frame + 1);
            Assert.That(Quaternion.Angle(plate.localRotation, neutral), Is.GreaterThan(0.5f));
            Assert.That(engine.localPosition.magnitude, Is.LessThan(0.08f));
            _root.View.Fx.Advance(0.26f, frame + 2);
            Assert.That(engine.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(Quaternion.Angle(plate.localRotation, neutral), Is.LessThan(0.001f));
            Assert.That(rejected.gameObject.activeSelf, Is.True, "the symbol outlasts the recoil");
            _root.View.UpdateFrom(_root.Session, _root.Session.State.Tick * 0.1f + 0.61f);
            Assert.That(rejected.gameObject.activeSelf, Is.False, "repainting a dwell must not restart it");
            CollectionAssert.AreEqual(before, Digest(_root), "juice never mutates the simulation");
        }

        [UnityTest]
        public IEnumerator MotionOff_LeavesAStaticMismatchGlyph_WithoutRecoilOrFlash()
        {
            _root = GameRoot.LaunchWith(Level());
            _root.MotionOffToggle = true;
            yield return null;
            int slot = ReachFirstRejection(_root);
            var train = _root.View.transform.Find("train:" + slot);
            Assert.That(train.Find("Carriage/Pin/Rejected"), Is.Not.Null);
            Assert.That(train.Find("Carriage/Pin/Rejected").gameObject.activeSelf, Is.True);
            Assert.That(train.Find("Engine").localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(_root.View.Fx.ActiveTweenCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator MovingTrain_HasWorldSpaceSteamAndBob_ThatStopWithMotionOff()
        {
            _root = GameRoot.LaunchWith(Level());
            yield return null;
            for (int tick = 0; tick < 30 && Array.FindIndex(_root.Session.State.Trains,
                t => t.State == TrainState.OnEdge) < 0; tick++) _root.Session.AdvanceMs(125);
            _root.View.UpdateFrom(_root.Session, 0f);
            int slot = Array.FindIndex(_root.Session.State.Trains, t => t.State == TrainState.OnEdge);
            Assert.That(slot, Is.GreaterThanOrEqualTo(0));
            var train = _root.View.transform.Find("train:" + slot);
            var steam = train.Find("Engine/Funnel/Steam");
            Assert.That(steam, Is.Not.Null, "a moving funnel must smoke");
            var particles = steam.GetComponent<ParticleSystem>();
            Assert.That(particles.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
            Assert.That(particles.emission.rateOverTime.constant, Is.EqualTo(5f));
            particles.Simulate(0.4f, false, false);
            Assert.That(particles.particleCount, Is.GreaterThan(0));
            Vector3 anchor = train.localPosition;
            _root.View.UpdateFrom(_root.Session, 0.0375f);
            Assert.That(Mathf.Abs(train.Find("Engine").localPosition.z), Is.InRange(0.0039f, 0.0041f));
            Assert.That(train.localPosition, Is.EqualTo(anchor), "only the rendered engine bobs");
            _root.MotionOffToggle = true;
            _root.View.UpdateFrom(_root.Session, 0.05f);
            _root.View.Fx.Advance(0.01f);
            Assert.That(particles.particleCount, Is.Zero);
            Assert.That(particles.emission.enabled, Is.False);
            Assert.That(train.Find("Engine").localPosition, Is.EqualTo(Vector3.zero));
        }

        [UnityTest]
        public IEnumerator HomeCamera_BreathesWithoutAdvancingThePuzzle_AndRestsOnPlay()
        {
            GameRoot.DevSkipShippedHome = false;
            _root = GameRoot.Launch();
            yield return null;
            float low = float.MaxValue, high = 0f;
            for (int i = 0; i <= 24; i++)
            {
                _root.View.UpdateFrom(_root.Session, i * 0.25f);
                low = Mathf.Min(low, _root.Cam.orthographicSize);
                high = Mathf.Max(high, _root.Cam.orthographicSize);
            }
            Assert.That(high / low, Is.InRange(1.011f, 1.013f));
            Assert.That(_root.Session.State.Tick, Is.Zero);
            _root.MotionOffToggle = true;
            _root.View.UpdateFrom(_root.Session, 8f);
            float rest = _root.Cam.orthographicSize;
            _root.Input.HandleTapAtScreen(_root.Home.PinPaintedRectPx.center);
            _root.Input.HandleTapAtScreen(_root.Intro.PlayChipRectPx.center);
            Assert.That(_root.ScreensVisible, Is.False);
            _root.MotionOffToggle = false;
            _root.View.UpdateFrom(_root.Session, 9f);
            Assert.That(_root.Cam.orthographicSize, Is.EqualTo(rest).Within(0.0001f));
            Assert.That(_root.Session.State.Tick, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ParkedEngine_PuffsEveryFourSeconds_OnlyBehindScreens()
        {
            GameRoot.DevSkipShippedHome = false;
            _root = GameRoot.Launch();
            var engine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            engine.name = "parked engine fixture";
            engine.transform.SetParent(_root.View.transform, false);
            var identity = engine.AddComponent<CatMetro.Presentation.Props.BoardPropInstance>();
            typeof(CatMetro.Presentation.Props.BoardPropInstance).GetProperty("Role")
                .SetValue(identity, "parked-engine");
            yield return null;
            _root.View.UpdateFrom(_root.Session, 0f);
            _root.View.UpdateFrom(_root.Session, 4.01f);
            var smoke = engine.GetComponentInChildren<ParticleSystem>();
            Assert.That(smoke, Is.Not.Null);
            Assert.That(smoke.particleCount, Is.EqualTo(1));
            smoke.Clear();
            _root.View.UpdateFrom(_root.Session, 7.9f);
            Assert.That(smoke.particleCount, Is.Zero);
            _root.View.UpdateFrom(_root.Session, 8.02f);
            Assert.That(smoke.particleCount, Is.EqualTo(1));
            _root.MotionOffToggle = true;
            _root.Input.HandleTapAtScreen(_root.Home.PinPaintedRectPx.center);
            _root.Input.HandleTapAtScreen(_root.Intro.PlayChipRectPx.center);
            _root.MotionOffToggle = false;
            _root.View.UpdateFrom(_root.Session, 12.1f);
            Assert.That(smoke.particleCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ReverseTravel_ChangesSteamDriftAndTheRecoilDirection()
        {
            _root = GameRoot.LaunchWith(Level());
            yield return null;
            int slot = ReachFirstRejection(_root);
            var train = _root.View.transform.Find("train:" + slot).GetComponent<ToyTrainView>();
            var engine = train.transform.Find("Engine");
            Vector3 forwardRecoil = engine.localPosition;
            train.ShowRejection(20f, true);
            Assert.That(Vector3.Distance(engine.localPosition, -forwardRecoil), Is.LessThan(0.0001f),
                "arriving at EdgeFrom reverses the recoil, without changing the engine's facing");
            _root.View.Fx.Advance(0.3f);
            for (int tick = 0; tick < 12 && _root.Session.State.Trains[slot].State != TrainState.OnEdgeReverse; tick++)
            {
                _root.Session.AdvanceMs(125);
                _root.View.UpdateFrom(_root.Session, 21f);
            }
            Assert.That(_root.Session.State.Trains[slot].State, Is.EqualTo(TrainState.OnEdgeReverse));
            Vector3 start = train.transform.position;
            _root.Session.AdvanceMs(125);
            _root.View.UpdateFrom(_root.Session, 21.125f);
            Vector3 travel = (train.transform.position - start).normalized;
            var drift = train.transform.Find("Engine/Funnel/Steam").GetComponent<ParticleSystem>().velocityOverLifetime;
            Vector3 velocity = new Vector3(drift.x.constant, drift.y.constant, drift.z.constant) - Vector3.back * 0.15f;
            Assert.That(Vector3.Dot(velocity, travel), Is.LessThan(-0.09f), "steam trails actual reverse travel");
        }

        [UnityTest]
        public IEnumerator FailedSnapshot_StopsSteamAndEngineBob()
        {
            _root = GameRoot.LaunchWith(Level());
            yield return null;
            for (int tick = 0; tick < 30 && Array.FindIndex(_root.Session.State.Trains,
                t => t.State == TrainState.OnEdge) < 0; tick++) _root.Session.AdvanceMs(125);
            _root.View.UpdateFrom(_root.Session, 0.0375f);
            var train = _root.View.GetComponentInChildren<ToyTrainView>();
            var steam = train.GetComponentInChildren<ParticleSystem>();
            Assert.That(steam.emission.enabled, Is.True);
            _root.Session.State.Outcome = SimOutcome.MakeFailed(FailReason.Collision);
            _root.View.UpdateFrom(_root.Session, 0.0375f);
            Assert.That(steam.emission.enabled, Is.False);
            Assert.That(train.transform.Find("Engine").localPosition, Is.EqualTo(Vector3.zero));
        }

        [UnityTest]
        public IEnumerator UnexpectedHalt_StopsAlreadyLoopingSteam()
        {
            _root = GameRoot.LaunchWith(Level());
            yield return null;
            for (int tick = 0; tick < 30 && Array.FindIndex(_root.Session.State.Trains,
                t => t.State == TrainState.OnEdge) < 0; tick++) _root.Session.AdvanceMs(125);
            _root.View.UpdateFrom(_root.Session, 0f);
            var steam = _root.View.GetComponentInChildren<ToyTrainView>().GetComponentInChildren<ParticleSystem>();
            Assert.That(steam.emission.enabled, Is.True);
            var graph = _root.Session.State.Graph;
            graph.WaveTick[1] = _root.Session.State.Tick;
            for (int edge = 0; edge < graph.EdgeFrom.Length; edge++)
                if (graph.EdgeFrom[edge] == graph.SourceNode)
                    graph.EdgeFrom[edge] = (graph.SourceNode + 1) % graph.NodeCount;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("run halted at an unexpected Domain boundary"));
            Time.timeScale = 4f;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (_root.ScreenState != "Halted" && Time.realtimeSinceStartup < deadline) yield return null;
            Time.timeScale = 0f;
            Assert.That(_root.ScreenState, Is.EqualTo("Halted"));
            Assert.That(steam.emission.enabled, Is.False, "halt bypasses UpdateFrom, so it must stop existing effects explicitly");
        }

        [UnityTest]
        public IEnumerator StrayExpressLeavingDwell_DoesNotBorrowAnotherCatsRejection()
        {
            _root = GameRoot.LaunchWith(Level());
            yield return null;
            int slot = ReachFirstRejection(_root);
            var state = _root.Session.State;
            var cat = state.Trains[slot];
            cat.Color = CatToken.Pack(CatToken.Color(cat.Color), CatToken.Shape(cat.Color), true, true);
            state.Trains[slot] = cat;
            _root.View.UpdateFrom(_root.Session, 20f);
            var train = _root.View.transform.Find("train:" + slot);
            Assert.That(train.Find("Carriage/Pin/Rejected").gameObject.activeSelf, Is.False);
            cat.State = TrainState.OnEdgeReverse;
            cat.ProgressTicks = 0;
            state.Trains[slot] = cat;
            state.Rejections++; // a different cat's rejection arrived in this copied frame
            _root.View.UpdateFrom(_root.Session, 20.1f);
            Assert.That(train.Find("Carriage/Pin/Rejected").gameObject.activeSelf, Is.False);
        }

        internal static int ReachFirstRejection(GameRoot root)
        {
            root.View.UpdateFrom(root.Session, 0f);
            for (int i = 0; i < 150 && root.Session.State.Rejections == 0; i++)
            {
                root.Session.AdvanceMs(125);
                root.View.UpdateFrom(root.Session, root.Session.State.Tick * 0.1f);
            }
            Assert.That(root.Session.State.Rejections, Is.EqualTo(1));
            int slot = Array.FindIndex(root.Session.State.Trains,
                t => t.State == TrainState.RejectedAtStation);
            Assert.That(slot, Is.GreaterThanOrEqualTo(0));
            return slot;
        }

        internal static ImportedLevel Level()
        {
            var bytes = new StreamingAssetsContentSource().ReadAsync(
                "content/levels/L009.json", System.Threading.CancellationToken.None)
                .GetAwaiter().GetResult();
            var level = LevelImporter.Import(bytes);
            Assert.That(level.Ok, Is.True, $"{level.Error}");
            return level.Value;
        }

        private static byte[] Digest(GameRoot root)
        {
            var state = root.Session.State;
            var bytes = new byte[state.DigestLength()];
            state.WriteDigest(bytes);
            return bytes;
        }
    }
}
