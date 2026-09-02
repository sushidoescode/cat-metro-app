using CatMetro.Presentation.Board;
using NUnit.Framework;
using UnityEngine;

namespace CatMetro.Tests.EditMode.Presentation
{
    // LOOK step 6: the consist's edge-boundary law, pinned as pure math. The head rides the
    // current edge; a trailing vehicle sits `offset` arc-length units behind it, continuing
    // onto the ONE remembered previous edge when the offset crosses the edge start, and
    // clamping (flagged via Sample.Clamped) when it falls off known history entirely.
    public sealed class TrainConsistLayoutTests
    {
        [Test]
        public void WithinCurrentEdge_TrailsByExactlyTheOffset()
        {
            var sample = TrainConsistLayout.ResolveBehind(5f, 2f, 10f, -1f);

            Assert.That(sample.OnPreviousEdge, Is.False);
            Assert.That(sample.Distance, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(sample.Clamped, Is.False);
        }

        [Test]
        public void ExactlyAtTheEdgeStart_StaysOnTheCurrentEdgeUnclamped()
        {
            var sample = TrainConsistLayout.ResolveBehind(0.5f, 0.5f, 10f, 4f);

            Assert.That(sample.OnPreviousEdge, Is.False,
                "offset == headDistance is the boundary itself — still this edge's start");
            Assert.That(sample.Distance, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(sample.Clamped, Is.False);
        }

        [Test]
        public void CrossingTheBoundary_ContinuesOnThePreviousEdge()
        {
            var sample = TrainConsistLayout.ResolveBehind(0.3f, 0.5f, 10f, 4f);

            Assert.That(sample.OnPreviousEdge, Is.True,
                "the carriage must trail through the junction, not teleport to this edge's start");
            Assert.That(sample.Distance, Is.EqualTo(3.8f).Within(0.0001f));
            Assert.That(sample.Clamped, Is.False);
        }

        [Test]
        public void NoHistory_ClampsToTheCurrentEdgeStart()
        {
            var sample = TrainConsistLayout.ResolveBehind(0.3f, 0.5f, 10f, -1f);

            Assert.That(sample.OnPreviousEdge, Is.False,
                "a freshly spawned train has no previous edge to trail onto");
            Assert.That(sample.Distance, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(sample.Clamped, Is.True);
        }

        [Test]
        public void FallingOffTheRememberedHistory_ClampsToThePreviousEdgeStart()
        {
            var sample = TrainConsistLayout.ResolveBehind(0.1f, 5f, 10f, 3f);

            Assert.That(sample.OnPreviousEdge, Is.True);
            Assert.That(sample.Distance, Is.EqualTo(0f).Within(0.0001f),
                "history is one edge deep by design — beyond it the vehicle bunches at its start");
            Assert.That(sample.Clamped, Is.True);
        }

        [Test]
        public void HeadPastTheEdgeEnd_IsTreatedAsTheEdgeEnd()
        {
            var sample = TrainConsistLayout.ResolveBehind(12f, 2f, 10f, -1f);

            Assert.That(sample.OnPreviousEdge, Is.False);
            Assert.That(sample.Distance, Is.EqualTo(8f).Within(0.0001f));
            Assert.That(sample.Clamped, Is.False);
        }

        [Test]
        public void NegativeOffset_RidesAtTheHead()
        {
            var sample = TrainConsistLayout.ResolveBehind(5f, -1f, 10f, -1f);

            Assert.That(sample.OnPreviousEdge, Is.False);
            Assert.That(sample.Distance, Is.EqualTo(5f).Within(0.0001f),
                "an offset can never place a vehicle AHEAD of the head");
            Assert.That(sample.Clamped, Is.False);
        }

        [Test]
        public void TwoTrailingVehicles_KeepTheirArcSpacing()
        {
            var near = TrainConsistLayout.ResolveBehind(6f, 1f, 10f, -1f);
            var far = TrainConsistLayout.ResolveBehind(6f, 2.5f, 10f, -1f);

            Assert.That(near.Distance - far.Distance, Is.EqualTo(1.5f).Within(0.0001f),
                "spacing between vehicles is the difference of their offsets while on one edge");
        }

        [Test]
        public void OnARealStraightSpline_TheCarriageSitsTheOffsetBackAlongTheArc()
        {
            var graph = TrackSplineGraph.Build(
                new[] { Vector3.zero, new Vector3(6f, 0f, 0f) },
                new[] { 0 }, new[] { 1 });
            var path = graph.Path(0);

            var sample = TrainConsistLayout.ResolveBehind(4f, 1.5f, path.Length, -1f);
            Vector3 head = path.EvaluateDistanceFraction(4f / path.Length);
            Vector3 carriage = path.EvaluateDistanceFraction(sample.Distance / path.Length);

            Assert.That(sample.OnPreviousEdge, Is.False);
            Assert.That(Vector3.Distance(head, carriage), Is.EqualTo(1.5f).Within(0.001f),
                "resolver distances and spline sampling must agree about arc length");
        }
    }
}
