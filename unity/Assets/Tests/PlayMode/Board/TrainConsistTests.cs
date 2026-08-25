using System.Collections;
using System.IO;
using System.Linq;
using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Theme;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    // LOOK step 6: a train renders as a toy consist — engine + open carriage + seated cat —
    // riding the SAME spline the physical track was built from. These are the scene-level
    // laws; the pure edge-boundary math is pinned in TrainConsistLayoutTests (EditMode).
    public sealed class TrainConsistTests
    {
        private const float CarriageOffset = ToyTrainView.CarriageOffset;
        private static readonly Vector3 HeadLift = new Vector3(0f, 0f, -0.2f);

        private GameObject _host;
        private BoardView _view;
        private GameSession _session;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            _view = null;
            _host = null;
            _session = null;
        }

        [UnityTest]
        public IEnumerator Train_RendersEngineCarriageAndCat_AllDecorationInventoryFree()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 1, progressTicks: 6);
            _view.UpdateFrom(_session);

            var train = TrainRoot();
            var engine = train.transform.Find("Engine");
            var carriage = train.transform.Find("Carriage");
            Assert.That(engine, Is.Not.Null, "the consist leads with an engine");
            Assert.That(carriage, Is.Not.Null, "a carriage trails the engine");
            Assert.That(carriage.Find("Cat"), Is.Not.Null,
                "the occupied carriage seats a cat — the concept art's whole point");

            foreach (var filter in train.GetComponentsInChildren<MeshFilter>(true))
                Assert.That(filter.sharedMesh, Is.Not.Null,
                    filter.name + " must resolve its builtin mesh (a null here renders nothing)");
            Assert.That(train.GetComponentsInChildren<BoardElementId>(true).Length, Is.EqualTo(1),
                "only the train ROOT is authored inventory; every part is decoration");
            Assert.That(train.GetComponentsInChildren<Collider>(true), Is.Empty,
                "visual-only consist parts must never intercept switch taps");
        }

        [UnityTest]
        public IEnumerator CatTint_MatchesTheTrainsLineColor()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 1, progressTicks: 6, color: CatColor.Red);
            _view.UpdateFrom(_session);

            AssertCatColor(CatHeadColor(), Palette.SignalRed,
                "the seated cat wears the train's line color, from Palette tokens");
        }

        [UnityTest]
        public IEnumerator Carriage_TrailsTheEngineAlongTheRenderedSpline()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 1, progressTicks: 6); // fraction 0.5 of E2's arc
            _view.UpdateFrom(_session);

            TrackSpline path = BuildTrackGraph().Path(1);
            float headDistance = path.Length * 0.5f;
            Vector3 expected = path.EvaluateDistanceFraction(
                (headDistance - CarriageOffset) / path.Length) + HeadLift;
            Assert.That(Vector3.Distance(CarriageBoardLocal(), expected), Is.LessThan(0.001f),
                "the carriage samples the identical spline a fixed arc-length behind the head");

            Vector3 tangent = path.TangentDistanceFraction(0.5f);
            Vector2 engineRight = TrainRoot().transform.Find("Engine").localRotation
                * Vector3.right;
            Assert.That(Vector2.Dot(engineRight.normalized, (Vector2)tangent.normalized),
                Is.GreaterThan(0.999f), "the engine faces along the direction of travel");
        }

        [UnityTest]
        public IEnumerator CrossingANode_TheCarriageKeepsTrailingOnTheArrivalEdge()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 0, progressTicks: 9);  // near E1's end — records history
            _view.UpdateFrom(_session);
            PlaceOnEdge(edge: 1, progressTicks: 0);  // the head just crossed J1 onto E2
            _view.UpdateFrom(_session);

            var graph = BuildTrackGraph();
            TrackSpline arrival = graph.Path(0);
            Vector3 expected = arrival.EvaluateDistanceFraction(
                (arrival.Length - CarriageOffset) / arrival.Length) + HeadLift;
            Vector3 actual = CarriageBoardLocal();
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.001f),
                "the carriage trails through the junction on the remembered arrival edge");
            Assert.That(Vector2.Distance(actual, graph.Path(1).Evaluate(0f)),
                Is.GreaterThan(0.3f),
                "no teleport-bunch: the carriage must NOT clamp to the new edge's start");

            // Review finding 2: at 60fps the head renders MANY frames on the new edge; a
            // same-edge frame must not clobber the previous-edge memory (a mutant that
            // records history unconditionally would put the carriage AHEAD of the engine
            // here, at the far end of the edge the head just entered).
            _view.UpdateFrom(_session);
            Assert.That(Vector3.Distance(CarriageBoardLocal(), expected), Is.LessThan(0.001f),
                "a repeated frame on the new edge still trails on the arrival edge");
        }

        [UnityTest]
        public IEnumerator CatchUpFrameSkippingAnEdge_ClampsInsteadOfTrailingForeignTrack()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 1, progressTicks: 11); // deep into E2, heading for RED
            _view.UpdateFrom(_session);
            // A pause/resume catch-up frame: the sim ran many ticks and the head reappears
            // on E3 — an edge E2 does not feed (E2 ends at RED, E3 starts at J1).
            PlaceOnEdge(edge: 2, progressTicks: 0);
            _view.UpdateFrom(_session);

            var graph = BuildTrackGraph();
            Vector3 actual = CarriageBoardLocal();
            Assert.That(Vector3.Distance(actual, graph.Path(2).Evaluate(0f) + HeadLift),
                Is.LessThan(0.001f),
                "non-adjacent history is discarded through the BoardView seam — the carriage " +
                "takes the spawn clamp at the new edge's start");
            TrackSpline foreign = graph.Path(1);
            Assert.That(Vector2.Distance(actual, foreign.EvaluateDistanceFraction(
                    (foreign.Length - CarriageOffset) / foreign.Length)),
                Is.GreaterThan(1f),
                "and it must be nowhere near the foreign edge's end the stale memory named");
        }

        [UnityTest]
        public IEnumerator QueuedAtANode_TheConsistTrailsBackAlongTheArrivalEdge()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 0, progressTicks: 9);
            _view.UpdateFrom(_session);
            _session.State.Trains[0].State = TrainState.AtNode; // arrived and queued at J1
            _session.State.Trains[0].EdgeId = -1;
            _session.State.Trains[0].ProgressTicks = 0;
            _session.State.Trains[0].NodeId = 1;
            _view.UpdateFrom(_session);

            var train = TrainRoot();
            Assert.That(Vector3.Distance(train.transform.localPosition,
                    new Vector3(3f, 6f, 0f) + HeadLift), Is.LessThan(0.001f),
                "a parked head anchors on its node, exactly like the old capsule");
            TrackSpline arrival = BuildTrackGraph().Path(0);
            Vector3 expected = arrival.EvaluateDistanceFraction(
                (arrival.Length - CarriageOffset) / arrival.Length) + HeadLift;
            Assert.That(Vector3.Distance(CarriageBoardLocal(), expected), Is.LessThan(0.001f),
                "the queued consist trails back along the edge it arrived on");
        }

        [UnityTest]
        public IEnumerator SlotReuse_ResetsEdgeHistoryAndRetintsTheCat()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 0, progressTicks: 9, id: 1, color: CatColor.Red);
            _view.UpdateFrom(_session);
            _session.State.Trains[0] = default; // delivered: the slot zeroes (A-C1-10)
            _view.UpdateFrom(_session);
            PlaceOnEdge(edge: 1, progressTicks: 0, id: 2, color: CatColor.Blue);
            _view.UpdateFrom(_session);

            AssertCatColor(CatHeadColor(), Palette.HarborBlue,
                "a reused slot re-tints for its NEW occupant");
            var graph = BuildTrackGraph();
            Assert.That(Vector3.Distance(CarriageBoardLocal(),
                    graph.Path(1).Evaluate(0f) + HeadLift), Is.LessThan(0.001f),
                "a reused slot must not inherit the dead train's edge history — with none, " +
                "the carriage bunches at the new edge's start (the documented spawn clamp)");
        }

        [UnityTest]
        public IEnumerator DeadSlot_HidesTheWholeConsist()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 1, progressTicks: 6);
            _view.UpdateFrom(_session);
            _session.State.Trains[0] = default;
            _view.UpdateFrom(_session);

            Assert.That(TrainRoot().gameObject.activeSelf, Is.False,
                "a delivered train's consist leaves the board, cat and all");
        }

        private IEnumerator BuildBoard()
        {
            var level = ImportL001();
            _session = new GameSession(level);
            _host = new GameObject("train-consist-host");
            _view = BoardView.Build(level, _host.transform, _session);
            yield return null;
        }

        private void PlaceOnEdge(int edge, int progressTicks,
            byte color = CatColor.Red, short id = 1)
        {
            _session.State.Trains[0] = new TrainSlot
            {
                Id = id,
                Color = color,
                EdgeId = (short)edge,
                ProgressTicks = (short)progressTicks,
                NodeId = 1,
                State = TrainState.OnEdge,
            };
        }

        private BoardElementId TrainRoot() =>
            _view.GetComponentsInChildren<BoardElementId>(true).Single(x => x.Kind == "train");

        private Vector3 CarriageBoardLocal()
        {
            var carriage = TrainRoot().transform.Find("Carriage");
            return _view.transform.InverseTransformPoint(carriage.position);
        }

        private Color CatHeadColor()
        {
            var head = TrainRoot().transform.Find("Carriage").Find("Cat").Find("Head");
            var block = new MaterialPropertyBlock();
            head.GetComponent<MeshRenderer>().GetPropertyBlock(block);
            return block.GetColor("_BaseColor");
        }

        // The property-block round trip is not bit-exact in this Linear-color-space project
        // (ProjectSettings m_ActiveColorSpace: 1): SetColor stores the float32 sRGB->linear
        // conversion and GetColor approximates the inverse, an ulp-level wobble the first
        // device run caught with exact Color equality (identical 3-decimal prints, unequal
        // bits). No Color32/255-step exists anywhere in the tint path, so 2e-3 per channel
        // covers the conversion with room while staying ~15x under the closest palette pair
        // (SignalRed vs AlarmCoral, 8/255 apart in red).
        private static void AssertCatColor(Color actual, Color expected, string message)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.002f), message + " (r)");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.002f), message + " (g)");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.002f), message + " (b)");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.002f), message + " (a)");
        }

        private static TrackSplineGraph BuildTrackGraph()
        {
            // L001's authored graph, matching BoardView's own construction inputs.
            var positions = new[]
            {
                new Vector3(3f, 9f, 0f), // SRC
                new Vector3(3f, 6f, 0f), // J1
                new Vector3(1f, 2f, 0f), // RED
                new Vector3(5f, 2f, 0f), // BLU
            };
            return TrackSplineGraph.Build(positions,
                new[] { 0, 1, 1 }, new[] { 1, 2, 3 });
        }

        private static ImportedLevel ImportL001()
        {
            string path = Path.Combine(UnityEngine.Application.streamingAssetsPath,
                "content", "levels", "L001.json");
            var imported = LevelImporter.Import(File.ReadAllBytes(path));
            Assert.That(imported.Ok, Is.True,
                imported.Ok ? string.Empty : imported.Error.ToString());
            return imported.Value;
        }
    }
}
