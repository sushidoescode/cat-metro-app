using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    public sealed class BoardTrackIntegrationTests
    {
        private GameObject _host;
        private BoardView _view;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            _view = null;
            _host = null;
        }

        [UnityTest]
        public IEnumerator BoardBuild_ReplacesEveryEdgeCubeWithPhysicalToyTrack()
        {
            var level = ImportL001();
            _host = new GameObject("board-track-host");
            _view = BoardView.Build(level, _host.transform, new GameSession(level));
            yield return null;

            var edges = _view.GetComponentsInChildren<BoardElementId>(true)
                .Where(x => x.Kind == "edge").ToArray();
            Assert.That(edges.Length, Is.EqualTo(level.Dto.Edges.Length));
            foreach (var edge in edges)
            {
                var filter = edge.GetComponent<MeshFilter>();
                Assert.That(filter, Is.Not.Null, edge.Id);
                Assert.That(filter.sharedMesh.subMeshCount, Is.EqualTo(2),
                    edge.Id + " must contain cream sleepers and navy rails");
                Assert.That(edge.GetComponentsInChildren<Collider>(true), Is.Empty,
                    edge.Id + " must not keep the old primitive collider");
            }
        }

        [UnityTest]
        public IEnumerator TrainOnBranch_FollowsExactlyTheRenderedArc()
        {
            var level = ImportL001();
            var session = new GameSession(level);
            _host = new GameObject("board-train-curve-host");
            _view = BoardView.Build(level, _host.transform, session);
            const int edge = 1;
            const int progressTicks = 6;
            session.State.Trains[0] = new TrainSlot
            {
                Id = 1,
                Color = CatColor.Red,
                EdgeId = edge,
                ProgressTicks = progressTicks,
                NodeId = 1,
                State = TrainState.OnEdge,
            };

            _view.UpdateFrom(session);
            yield return null;

            var train = _view.GetComponentsInChildren<BoardElementId>(true)
                .Single(x => x.Kind == "train");
            TrackSpline expectedPath = BuildTrackGraph(level).Path(edge);
            float expectedFraction = Mathf.Min(1f,
                (progressTicks + (float)session.Alpha) / level.Graph.EdgeTravelTicks[edge]);
            Vector3 expectedPosition = expectedPath.EvaluateDistanceFraction(expectedFraction)
                + new Vector3(0f, 0f, -0.2f);
            Assert.That(Vector3.Distance(train.transform.localPosition, expectedPosition),
                Is.LessThan(0.001f),
                "train progress and rendered rail must evaluate the identical spline");

            Vector3 oldStraightMidpoint =
                Vector3.Lerp(new Vector3(3f, 6f, 0f), new Vector3(1f, 2f, 0f), 0.5f)
                + new Vector3(0f, 0f, -0.2f);
            Assert.That(Vector2.Distance(train.transform.localPosition, oldStraightMidpoint),
                Is.GreaterThan(0.10f),
                "train and physical rail must use the same curved path");
        }

        [UnityTest]
        public IEnumerator ReversingTrain_UsesTheSameSplineInTheOppositeDirection()
        {
            var level = ImportL001();
            var session = new GameSession(level);
            _host = new GameObject("board-reverse-curve-host");
            _view = BoardView.Build(level, _host.transform, session);
            const int edge = 1;
            const int progressTicks = 3;
            session.State.Trains[0] = new TrainSlot
            {
                Id = 1,
                Color = CatColor.Red,
                EdgeId = edge,
                ProgressTicks = progressTicks,
                NodeId = 2,
                State = TrainState.OnEdgeReverse,
            };

            _view.UpdateFrom(session);
            yield return null;

            var train = _view.GetComponentsInChildren<BoardElementId>(true)
                .Single(x => x.Kind == "train");
            TrackSpline expectedPath = BuildTrackGraph(level).Path(edge);
            float expectedFraction = 1f - Mathf.Min(1f,
                (progressTicks + (float)session.Alpha) / level.Graph.EdgeTravelTicks[edge]);
            Vector3 expectedPosition = expectedPath.EvaluateDistanceFraction(expectedFraction)
                + new Vector3(0f, 0f, -0.2f);
            Assert.That(Vector3.Distance(train.transform.localPosition, expectedPosition),
                Is.LessThan(0.001f),
                "reverse progress must mirror the rendered spline fraction, not jump endpoints");
        }

        [UnityTest]
        public IEnumerator DestroyingRuntimeBoard_ReleasesEveryGeneratedTrackMesh()
        {
            var level = ImportL001();
            _host = new GameObject("board-track-lifetime-host");
            _view = BoardView.Build(level, _host.transform, new GameSession(level));
            Mesh[] generated = _view.GetComponentsInChildren<BoardElementId>(true)
                .Where(x => x.Kind == "edge")
                .Select(x => x.GetComponent<MeshFilter>().sharedMesh)
                .ToArray();

            Object.Destroy(_host);
            _host = null;
            _view = null;
            yield return null;
            yield return null;

            Assert.That(generated.All(mesh => mesh == null), Is.True,
                "runtime board teardown must release every generated edge mesh");
        }

        private static TrackSplineGraph BuildTrackGraph(ImportedLevel level)
        {
            var nodes = level.Dto.Nodes.ToArray();
            var edges = level.Dto.Edges.ToArray();
            var nodeIndex = new Dictionary<string, int>(nodes.Length);
            var positions = new Vector3[nodes.Length];
            for (int node = 0; node < nodes.Length; node++)
            {
                nodeIndex.Add(nodes[node].Id, node);
                positions[node] = new Vector3(nodes[node].X, nodes[node].Y, 0f);
            }
            return TrackSplineGraph.Build(positions,
                edges.Select(edge => nodeIndex[edge.From]).ToArray(),
                edges.Select(edge => nodeIndex[edge.To]).ToArray());
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
