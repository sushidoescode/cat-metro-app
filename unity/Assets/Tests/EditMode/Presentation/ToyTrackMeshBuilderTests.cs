using System.Collections.Generic;
using System.Linq;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Theme;
using NUnit.Framework;
using UnityEngine;

namespace CatMetro.Tests.EditMode.Presentation
{
    public sealed class ToyTrackMeshBuilderTests
    {
        private GameObject _host;
        private GameObject _track;

        [TearDown]
        public void TearDown()
        {
            if (_track != null)
            {
                var filter = _track.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                    Object.DestroyImmediate(filter.sharedMesh);
            }
            if (_host != null) Object.DestroyImmediate(_host);
            _track = null;
            _host = null;
        }

        [Test]
        public void Build_CreatesOneTaggedEdgeWithCreamSleepersAndTwinNavyRails()
        {
            var path = TrackSplineGraph.Build(
                new[] { new Vector3(0f, 3f, 0f), Vector3.zero },
                new[] { 0 }, new[] { 1 }).Path(0);
            _host = new GameObject("track-test-host");

            _track = ToyTrackMeshBuilder.Build("E-test", path, _host.transform);

            Assert.That(_track.transform.parent, Is.SameAs(_host.transform));
            var id = _track.GetComponent<BoardElementId>();
            Assert.That(id, Is.Not.Null);
            Assert.That(id.Id, Is.EqualTo("E-test"));
            Assert.That(id.Kind, Is.EqualTo("edge"));
            Assert.That(_track.GetComponentsInChildren<Collider>(true), Is.Empty,
                "track is presentation geometry and must not intercept switch taps");

            var mesh = _track.GetComponent<MeshFilter>().sharedMesh;
            var renderer = _track.GetComponent<MeshRenderer>();
            Assert.That(mesh.subMeshCount, Is.EqualTo(2),
                "sleepers and rails need independently coloured physical geometry");
            Assert.That(mesh.GetIndexCount(0), Is.GreaterThan(0), "cream sleepers are present");
            Assert.That(mesh.GetIndexCount(1), Is.GreaterThan(0), "both navy rails are present");
            Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(2));
            AssertColor(renderer.sharedMaterials[0].color, Palette.CreamCard);
            AssertColor(renderer.sharedMaterials[1].color, Palette.InkNavy);
            Assert.That(renderer.sharedMaterials[0].shader, Is.EqualTo(GreyboxMaterial.Shared.shader));
            Assert.That(renderer.sharedMaterials[1].shader, Is.EqualTo(GreyboxMaterial.Shared.shader));
        }

        [Test]
        public void Build_HasChunkyWidthAndVisibleThicknessInsteadOfAFlatQuad()
        {
            var path = TrackSplineGraph.Build(
                new[] { new Vector3(1f, 4f, 0f), new Vector3(1f, 0f, 0f) },
                new[] { 0 }, new[] { 1 }).Path(0);
            _host = new GameObject("track-thickness-host");

            _track = ToyTrackMeshBuilder.Build("E-thick", path, _host.transform);

            Bounds bounds = _track.GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.That(bounds.size.x, Is.GreaterThan(0.75f),
                "sleepers must read wider than the paired rails");
            Assert.That(bounds.size.z, Is.GreaterThan(0.10f),
                "front and side faces must make the track a physical object");
        }

        [Test]
        public void Build_SleeperFacesAreWoundOutwardForBackfaceCulling()
        {
            var path = TrackSplineGraph.Build(
                new[] { new Vector3(0f, 0.8f, 0f), Vector3.zero },
                new[] { 0 }, new[] { 1 }).Path(0);
            _host = new GameObject("track-winding-host");

            _track = ToyTrackMeshBuilder.Build("E-winding", path, _host.transform);

            Mesh mesh = _track.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.GetTriangles(0);
            for (int triangle = 0; triangle < triangles.Length; triangle += 3)
            {
                Vector3 a = vertices[triangles[triangle]];
                Vector3 b = vertices[triangles[triangle + 1]];
                Vector3 c = vertices[triangles[triangle + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                Vector3 fromCentre = (a + b + c) / 3f - mesh.bounds.center;
                Assert.That(Vector3.Dot(normal, fromCentre), Is.GreaterThan(0.000001f),
                    "every cream sleeper face must point away from its solid volume");
            }
        }

        [Test]
        public void Build_TightTurnoutRailsNeverCollapseOrFoldBack()
        {
            var graph = TrackSplineGraph.Build(
                new[]
                {
                    new Vector3(3f, 9f, 0f),
                    new Vector3(3f, 6f, 0f),
                    new Vector3(1f, 1f, 0f),
                    new Vector3(5f, 1f, 0f),
                    new Vector3(5f, 6f, 0f),
                },
                new[] { 0, 1, 1, 1 }, new[] { 1, 2, 3, 4 });
            _host = new GameObject("track-tight-turnout-host");

            _track = ToyTrackMeshBuilder.Build("E4", graph.Path(3), _host.transform);

            Mesh mesh = _track.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            const int ringVertexCount = 6;
            var railComponents = ConnectedVertexComponents(mesh.GetTriangles(1));
            Assert.That(railComponents.Count, Is.EqualTo(2));
            foreach (int[] railVertices in railComponents)
            {
                Assert.That(railVertices.Length % ringVertexCount, Is.Zero);
                int ringsPerRail = railVertices.Length / ringVertexCount;
                float shortestStep = float.PositiveInfinity;
                Vector3 previous = RailRingCentre(vertices,
                    railVertices[0], ringVertexCount);
                for (int ring = 1; ring < ringsPerRail; ring++)
                {
                    Vector3 current = RailRingCentre(vertices,
                        railVertices[ring * ringVertexCount], ringVertexCount);
                    shortestStep = Mathf.Min(shortestStep, Vector3.Distance(previous, current));
                    previous = current;
                }
                Assert.That(shortestStep, Is.GreaterThan(0.04f),
                    "a rail offset must not pinch into a cusp on a tight switch route");
            }
        }

        [Test]
        public void DestroyingTrackOwner_ReleasesGeneratedMeshImmediatelyInEditMode()
        {
            var path = TrackSplineGraph.Build(
                new[] { new Vector3(0f, 2f, 0f), Vector3.zero },
                new[] { 0 }, new[] { 1 }).Path(0);
            _host = new GameObject("track-lifetime-host");
            _track = ToyTrackMeshBuilder.Build("E-lifetime", path, _host.transform);
            Mesh generated = _track.GetComponent<MeshFilter>().sharedMesh;

            Assert.That(UnityEngine.Application.isPlaying, Is.False,
                "this test must exercise the editor-time destruction branch");
            Object.DestroyImmediate(_track);
            _track = null;

            Assert.That(generated == null, Is.True,
                "edit-time board rebuilds must not retain generated mesh allocations");
        }

        private static List<int[]> ConnectedVertexComponents(int[] triangles)
        {
            var neighbours = new Dictionary<int, HashSet<int>>();
            for (int triangle = 0; triangle < triangles.Length; triangle += 3)
            {
                Connect(neighbours, triangles[triangle], triangles[triangle + 1]);
                Connect(neighbours, triangles[triangle + 1], triangles[triangle + 2]);
                Connect(neighbours, triangles[triangle + 2], triangles[triangle]);
            }

            var remaining = new HashSet<int>(neighbours.Keys);
            var components = new List<int[]>();
            while (remaining.Count > 0)
            {
                int seed = remaining.First();
                var pending = new Stack<int>();
                var component = new List<int>();
                pending.Push(seed);
                remaining.Remove(seed);
                while (pending.Count > 0)
                {
                    int current = pending.Pop();
                    component.Add(current);
                    foreach (int neighbour in neighbours[current])
                        if (remaining.Remove(neighbour)) pending.Push(neighbour);
                }
                components.Add(component.OrderBy(index => index).ToArray());
            }
            return components;
        }

        private static void Connect(Dictionary<int, HashSet<int>> neighbours, int a, int b)
        {
            if (!neighbours.TryGetValue(a, out HashSet<int> fromA))
                neighbours.Add(a, fromA = new HashSet<int>());
            if (!neighbours.TryGetValue(b, out HashSet<int> fromB))
                neighbours.Add(b, fromB = new HashSet<int>());
            fromA.Add(b);
            fromB.Add(a);
        }

        private static Vector3 RailRingCentre(Vector3[] vertices, int start, int count)
        {
            Vector3 centre = Vector3.zero;
            for (int i = 0; i < count; i++) centre += vertices[start + i];
            return centre / count;
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(Mathf.Abs(actual.r - expected.r), Is.LessThan(0.001f));
            Assert.That(Mathf.Abs(actual.g - expected.g), Is.LessThan(0.001f));
            Assert.That(Mathf.Abs(actual.b - expected.b), Is.LessThan(0.001f));
        }
    }
}
