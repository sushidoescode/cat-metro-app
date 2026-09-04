using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatMetro.Content;
using CatMetro.Presentation.Board;
using NUnit.Framework;
using UnityEngine;

namespace CatMetro.Tests.EditMode.Presentation
{
    public sealed class TrackSplineGraphTests
    {
        [Test]
        public void BranchPaths_ArriveAndDepartWithOneSmoothTangent()
        {
            var nodes = new[]
            {
                new Vector3(0f, 3f, 0f),
                Vector3.zero,
                new Vector3(-2f, -4f, 0f),
                new Vector3(2f, -4f, 0f),
            };
            var graph = TrackSplineGraph.Build(nodes,
                new[] { 0, 1, 1 }, new[] { 1, 2, 3 });

            var incoming = graph.Path(0);
            var left = graph.Path(1);
            var right = graph.Path(2);

            Assert.That(Vector3.Distance(incoming.Evaluate(1f), nodes[1]), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(left.Evaluate(0f), nodes[1]), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(right.Evaluate(0f), nodes[1]), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(incoming.Derivative(1f), left.Derivative(0f)),
                Is.LessThan(0.0001f), "left turnout must be C1-smooth at the junction");
            Assert.That(Vector3.Distance(incoming.Derivative(1f), right.Derivative(0f)),
                Is.LessThan(0.0001f), "right turnout must be C1-smooth at the junction");

            Assert.That(Vector3.Distance(left.EvaluateDistanceFraction(0.5f),
                    Vector3.Lerp(nodes[1], nodes[2], 0.5f)),
                Is.GreaterThan(0.12f), "the left route must be an arc, not its old straight quad");
            Assert.That(Vector3.Distance(right.EvaluateDistanceFraction(0.5f),
                    Vector3.Lerp(nodes[1], nodes[3], 0.5f)),
                Is.GreaterThan(0.12f), "the right route must be an arc, not its old straight quad");
        }

        [Test]
        public void StraightChain_PreservesAuthoredEndpointsAndCentreline()
        {
            var nodes = new[]
            {
                new Vector3(1f, 4f, 0f),
                new Vector3(1f, 2f, 0f),
                new Vector3(1f, 0f, 0f),
            };
            var graph = TrackSplineGraph.Build(nodes,
                new[] { 0, 1 }, new[] { 1, 2 });

            Assert.That(Vector3.Distance(graph.Path(0).EvaluateDistanceFraction(0.5f),
                new Vector3(1f, 3f, 0f)), Is.LessThan(0.005f));
            Assert.That(Vector3.Distance(graph.Path(1).Evaluate(1f), nodes[2]),
                Is.LessThan(0.0001f));
        }

        [Test]
        public void DistanceFractions_ProduceEvenTravelAroundACorner()
        {
            var graph = TrackSplineGraph.Build(
                new[]
                {
                    new Vector3(0f, 2f, 0f),
                    Vector3.zero,
                    new Vector3(2f, 0f, 0f),
                },
                new[] { 0, 1 }, new[] { 1, 2 });
            var corner = graph.Path(1);
            var steps = Enumerable.Range(0, 4)
                .Select(i => Vector3.Distance(
                    corner.EvaluateDistanceFraction(i * 0.25f),
                    corner.EvaluateDistanceFraction((i + 1) * 0.25f)))
                .ToArray();

            Assert.That(steps.Max() / steps.Min(), Is.LessThan(1.15f),
                "equal simulation progress must not visibly speed up or slow down on a curve");
        }

        [Test]
        public void EveryAuthoredLevel_BuildsContinuousForwardMovingTrackPaths()
        {
            // Discovery keeps future additions in the sweep automatically; the floor prevents
            // a missing subset of the shipped 60-level campaign from making "every" vacuous.
            string levelsRoot = Path.Combine(UnityEngine.Application.streamingAssetsPath,
                "content", "levels");
            var levelPaths = Directory.GetFiles(levelsRoot, "L*.json")
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .ToArray();
            Assert.That(levelPaths.Length, Is.GreaterThanOrEqualTo(60),
                "the spline corpus assertion must inspect all 60 shipped level artifacts");
            foreach (string path in levelPaths)
            {
                string levelId = Path.GetFileNameWithoutExtension(path);
                var imported = LevelImporter.Import(File.ReadAllBytes(path));
                Assert.That(imported.Ok, Is.True,
                    imported.Ok ? string.Empty : levelId + ": " + imported.Error);

                var nodes = imported.Value.Dto.Nodes.ToArray();
                var edges = imported.Value.Dto.Edges.ToArray();
                var nodeIndex = new Dictionary<string, int>(nodes.Length);
                var positions = new Vector3[nodes.Length];
                for (int node = 0; node < nodes.Length; node++)
                {
                    nodeIndex.Add(nodes[node].Id, node);
                    positions[node] = new Vector3(nodes[node].X, nodes[node].Y, 0f);
                }

                int[] from = edges.Select(edge => nodeIndex[edge.From]).ToArray();
                int[] to = edges.Select(edge => nodeIndex[edge.To]).ToArray();
                var graph = TrackSplineGraph.Build(positions, from, to);

                Assert.That(graph.EdgeCount, Is.EqualTo(edges.Length), levelId);
                for (int edge = 0; edge < edges.Length; edge++)
                {
                    TrackSpline track = graph.Path(edge);
                    Vector3 chord = positions[to[edge]] - positions[from[edge]];
                    Assert.That(Vector3.Distance(track.Evaluate(0f), positions[from[edge]]),
                        Is.LessThan(0.0001f), levelId + ":" + edges[edge].Id + " start");
                    Assert.That(Vector3.Distance(track.Evaluate(1f), positions[to[edge]]),
                        Is.LessThan(0.0001f), levelId + ":" + edges[edge].Id + " end");
                    Assert.That(track.Derivative(0f).sqrMagnitude, Is.GreaterThan(0.0001f),
                        levelId + ":" + edges[edge].Id + " start tangent");
                    Assert.That(track.Derivative(1f).sqrMagnitude, Is.GreaterThan(0.0001f),
                        levelId + ":" + edges[edge].Id + " end tangent");
                    for (int sample = 0; sample <= 20; sample++)
                        Assert.That(Vector3.Dot(track.Derivative(sample / 20f), chord),
                            Is.GreaterThanOrEqualTo(-0.0001f),
                            levelId + ":" + edges[edge].Id + " must not double back");
                }
            }
        }
    }
}
