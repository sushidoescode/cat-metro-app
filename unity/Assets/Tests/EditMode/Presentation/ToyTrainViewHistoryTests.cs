using CatMetro.Presentation.Board;
using NUnit.Framework;
using UnityEngine;

namespace CatMetro.Tests.EditMode.Presentation
{
    // The consist's history-validity law (review finding on frame-keyed memory): remembered
    // edge history is honored only when the authored graph agrees the head could have rolled
    // straight through it — a multi-tick catch-up frame can skip a whole edge between
    // renders, and trailing along non-adjacent history would place the carriage somewhere
    // the train never was. Driven against ToyTrainView directly on a synthetic straight
    // chain (nodes at x = 0/3/6/9; edges 0->1, 1->2, 2->3), where every expected pose is
    // arithmetic: the root is unrotated, so carriage.localPosition IS the board-local delta.
    public sealed class ToyTrainViewHistoryTests
    {
        // Tolerance for assertions whose EXPECTED side is analytic (-CarriageOffset) while
        // the actual side rides TrackSpline's arc-length lookup: a straight spline passes
        // the flatness test immediately, so subdivision stops at the minimum depth (2^4 = 16
        // arc samples) and ParameterAtDistanceFraction interpolates linearly between them.
        // With handle 0.9 (0.3 x the 3-long edges) the parametric speed varies 2.7..3.15,
        // bounding each lookup's position error near 1.3e-3 (linear-interp bound
        // ds^2/8 * max|t''| * max speed); a carriage delta stacks two lookups (~2.6e-3;
        // 1.13e-3 observed in the first Unity run). 5e-3 covers that bound with headroom
        // while staying ~76x below the 0.38 signature a broken history guard would produce
        // (clamp at the current edge's start vs trail on the previous edge). Assertions
        // whose both sides are exact t=0 evaluations keep the tight 1e-3.
        private const float SplineLookupTolerance = 0.005f;

        private GameObject _host;
        private ToyTrainView _view;
        private TrackSplineGraph _paths;

        [SetUp]
        public void SetUp()
        {
            var nodes = new[]
            {
                Vector3.zero,
                new Vector3(3f, 0f, 0f),
                new Vector3(6f, 0f, 0f),
                new Vector3(9f, 0f, 0f),
            };
            var edgeFrom = new[] { 0, 1, 2 };
            var edgeTo = new[] { 1, 2, 3 };
            _paths = TrackSplineGraph.Build(nodes, edgeFrom, edgeTo);
            _host = new GameObject("consist-history-host");
            _view = ToyTrainView.Create(_host.transform, "train:test", edgeFrom, edgeTo);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            _host = null;
            _view = null;
            _paths = null;
        }

        [Test]
        public void AdjacentEdgeChange_KeepsTrailingOnTheEdgeBehind()
        {
            _view.PlaceOnEdge(_paths, 0, 2.9f);
            _view.PlaceOnEdge(_paths, 1, 0f); // edge 0 ends where edge 1 starts

            Assert.That(Vector3.Distance(Carriage().localPosition,
                    new Vector3(-ToyTrainView.CarriageOffset, 0f, 0f)),
                Is.LessThan(SplineLookupTolerance),
                "with sim-adjacent history the carriage trails through the junction");
        }

        [Test]
        public void RepeatedFramesOnTheSameEdge_DoNotClobberTheHistory()
        {
            _view.PlaceOnEdge(_paths, 0, 2.9f);
            _view.PlaceOnEdge(_paths, 1, 0f);
            _view.PlaceOnEdge(_paths, 1, 0.1f); // a normal next frame on the same edge

            Assert.That(Vector3.Distance(Carriage().localPosition,
                    new Vector3(-ToyTrainView.CarriageOffset, 0f, 0f)),
                Is.LessThan(SplineLookupTolerance),
                "a same-edge frame must not overwrite the previous-edge memory (a clobbering " +
                "guard clamps at the current edge's start, 0.38 from this expectation — far " +
                "outside the arc-lookup tolerance)");
        }

        [Test]
        public void CatchUpFrameSkippingAnEdge_ClampsInsteadOfTrailingForeignTrack()
        {
            _view.PlaceOnEdge(_paths, 0, 2.9f);
            _view.PlaceOnEdge(_paths, 2, 0f); // edge 0 ends at node 1; edge 2 starts at node 2

            Assert.That(Vector3.Distance(Carriage().localPosition, Vector3.zero),
                Is.LessThan(0.001f),
                "non-adjacent history is discarded — the carriage bunches at the new edge's " +
                "start (the documented spawn clamp) instead of riding track the train never saw");
        }

        [Test]
        public void ParkedAtItsArrivalNode_TrailsBackAlongTheArrivalEdge()
        {
            _view.PlaceOnEdge(_paths, 0, 2.9f);
            _view.PlaceAtNode(_paths, 1, new Vector3(3f, 0f, 0f)); // node 1 IS edge 0's end

            Assert.That(Vector3.Distance(Carriage().localPosition,
                    new Vector3(-ToyTrainView.CarriageOffset, 0f, 0f)),
                Is.LessThan(SplineLookupTolerance),
                "parking at the edge's own end node keeps the consist trailing back along it");
        }

        [Test]
        public void ParkedAtAForeignNode_DiscardsHistoryAndParksOnThePoint()
        {
            _view.PlaceOnEdge(_paths, 0, 2.9f);
            _view.PlaceAtNode(_paths, 3, new Vector3(9f, 0f, 0f)); // edge 0 never touches node 3

            Assert.That(Vector3.Distance(Carriage().localPosition, Vector3.zero),
                Is.LessThan(0.001f),
                "a node the remembered edge never touches gets the park clamp, not a trail " +
                "along foreign track");

            // The discard must persist: the next edge frame has no history to misuse either.
            _view.PlaceOnEdge(_paths, 2, 0f);
            Assert.That(Vector3.Distance(Carriage().localPosition, Vector3.zero),
                Is.LessThan(0.001f),
                "after a foreign park the consist departs with the spawn clamp, never a " +
                "resurrected stale edge");
        }

        private Transform Carriage() => _view.transform.Find("Carriage");
    }
}
