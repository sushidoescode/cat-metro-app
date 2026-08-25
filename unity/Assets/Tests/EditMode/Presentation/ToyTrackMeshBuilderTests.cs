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
        public void Build_BallastStaysLighterThanTheBoardItSitsOn()
        {
            // The scene-mood lane repainted the board interior to albedo (0.78, 0.63, 0.57)
            // to land near target-01's rendered (194, 122, 84). A wide ballast bed only
            // buys contrast while it stays clearly lighter than that; if a later retint
            // drags the cream down toward the wood, we are back to a track that vanishes
            // into the board. Not a reference to BoardSurface — that file is another
            // lane's, so the ceiling is copied here deliberately.
            const float boardInterior = 0.6576f;

            var path = TrackSplineGraph.Build(
                new[] { new Vector3(0f, 3f, 0f), Vector3.zero },
                new[] { 0 }, new[] { 1 }).Path(0);
            _host = new GameObject("track-contrast-host");

            _track = ToyTrackMeshBuilder.Build("E-contrast", path, _host.transform);

            Material[] materials = _track.GetComponent<MeshRenderer>().sharedMaterials;
            float ballast = RelativeLuminance(materials[0].color);
            float rails = RelativeLuminance(materials[1].color);

            Assert.That(ballast - boardInterior, Is.GreaterThan(0.20f),
                "the ballast bed must read clearly lighter than the board interior");
            Assert.That(ballast - rails, Is.GreaterThan(0.5f),
                "the rails must stay dark against the bed they are set into");
        }

        private static float RelativeLuminance(Color c) =>
            0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        [Test]
        public void Build_BallastBedIsOneUnbrokenRibbonWiderThanTheGauge()
        {
            var path = TrackSplineGraph.Build(
                new[] { new Vector3(0f, 5f, 0f), Vector3.zero },
                new[] { 0 }, new[] { 1 }).Path(0);
            _host = new GameObject("track-ribbon-host");

            _track = ToyTrackMeshBuilder.Build("E-ribbon", path, _host.transform);

            Mesh mesh = _track.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int[] bed = LargestComponent(mesh.GetTriangles(0));

            // The edge runs down -y, so the sweep's lateral axis is +x.
            for (float distance = 0.05f; distance <= path.Length - 0.05f; distance += 0.1f)
            {
                Vector3 centre = path.EvaluateDistanceFraction(distance / path.Length);
                bool covered = false;
                float left = 0f;
                float right = 0f;
                foreach (int index in bed)
                {
                    Vector3 vertex = vertices[index];
                    if (Mathf.Abs(vertex.y - centre.y) > 0.25f) continue;
                    covered = true;
                    left = Mathf.Min(left, vertex.x);
                    right = Mathf.Max(right, vertex.x);
                }
                Assert.That(covered, Is.True,
                    "the ballast bed must be unbroken along the whole edge — a row of "
                    + "separate sleepers is what read as thin lines on bare board");
                Assert.That(right, Is.GreaterThanOrEqualTo(0.5f), "at distance " + distance);
                Assert.That(left, Is.LessThanOrEqualTo(-0.5f), "at distance " + distance);
            }
        }

        [Test]
        public void Build_AdjacentEdgesButtTheirBedsTogetherAtTheSharedNode()
        {
            var shared = new Vector3(0f, 2f, 0f);
            var graph = TrackSplineGraph.Build(
                new[] { new Vector3(0f, 4f, 0f), shared, new Vector3(2f, 0f, 0f) },
                new[] { 0, 1 }, new[] { 1, 2 });
            _host = new GameObject("track-joint-host");

            for (int edge = 0; edge < 2; edge++)
            {
                GameObject built = ToyTrackMeshBuilder.Build(
                    "E" + edge, graph.Path(edge), _host.transform);
                Mesh mesh = built.GetComponent<MeshFilter>().sharedMesh;
                Vector3[] vertices = mesh.vertices;

                float nearest = float.PositiveInfinity;
                foreach (int index in LargestComponent(mesh.GetTriangles(0)))
                    nearest = Mathf.Min(nearest, Vector2.Distance(
                        new Vector2(vertices[index].x, vertices[index].y),
                        new Vector2(shared.x, shared.y)));

                Assert.That(nearest, Is.LessThan(0.01f),
                    "E" + edge + " must carry its bed all the way to the shared node, or "
                    + "the ribbon breaks at every junction");
            }
        }

        [Test]
        public void Build_NavyRailsAreInsetIntoTheCreamBedNotLaidOnTopOfIt()
        {
            var path = TrackSplineGraph.Build(
                new[] { new Vector3(0f, 4f, 0f), Vector3.zero },
                new[] { 0 }, new[] { 1 }).Path(0);
            _host = new GameObject("track-inset-host");

            _track = ToyTrackMeshBuilder.Build("E-inset", path, _host.transform);

            Mesh mesh = _track.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int[] bed = LargestComponent(mesh.GetTriangles(0));
            int[] rails = mesh.GetTriangles(1);

            // Board-local -z is up, so a top surface is a band of SMALL z.
            float railCrown = rails.Min(index => vertices[index].z);
            float railUnderside = rails.Max(index => vertices[index].z);
            float bedHighest = bed.Min(index => vertices[index].z);
            float bedTop = bed.Where(index => vertices[index].z <= bedHighest + 0.05f)
                .Max(index => vertices[index].z);

            Assert.That(railCrown, Is.LessThan(bedTop),
                "the rails must stand proud of the bed or there is no navy to read");
            Assert.That(bedTop - railCrown, Is.InRange(0.04f, 0.10f),
                "a shallow proud lip reads as inset; a tall one is lines on stilts again");
            Assert.That(railUnderside - bedTop, Is.GreaterThan(0.04f),
                "the ballast must close over the rail's underside");
            Assert.That((railUnderside - bedTop) / (railUnderside - railCrown),
                Is.GreaterThan(0.4f),
                "at least 40% of each rail must sit down inside the ballast");
            Assert.That(rails.Max(index => Mathf.Abs(vertices[index].x)),
                Is.LessThan(bed.Max(index => Mathf.Abs(vertices[index].x)) - 0.1f),
                "the ribbon must still show cream outboard of both rails");
        }

        [Test]
        public void Build_SleeperTicksReadAsTieMarksNotProudBars()
        {
            var path = TrackSplineGraph.Build(
                new[] { new Vector3(0f, 5f, 0f), Vector3.zero },
                new[] { 0 }, new[] { 1 }).Path(0);
            _host = new GameObject("track-ticks-host");

            _track = ToyTrackMeshBuilder.Build("E-ticks", path, _host.transform);

            Mesh mesh = _track.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int[] cream = mesh.GetTriangles(0);
            List<int[]> islands = ConnectedVertexComponents(cream);
            int[] bed = islands.OrderByDescending(island => island.Length).First();

            Assert.That(islands.Count, Is.GreaterThan(4),
                "the bed must still carry a run of separate sleeper ticks");

            // -z is up: the ticks are the highest cream thing, the bed's crown is next.
            float tickTop = cream.Min(index => vertices[index].z);
            float bedCrown = bed.Min(index => vertices[index].z);
            float bedEdge = bed.Where(index => vertices[index].z <= bedCrown + 0.05f)
                .Max(index => vertices[index].z);

            Assert.That(bedCrown - tickTop, Is.InRange(0.004f, 0.016f),
                "a tie mark is a shading tick over the bed's crown, not a step — the "
                + "first render had 0.022 here and it read as chunky proud bars");
            Assert.That(bedEdge - tickTop, Is.LessThan(0.025f),
                "and it has to stay shallow at the bed's flat-top edge too");
        }

        [Test]
        public void Build_HighestSurfaceIsTheRailCrownTheTrainRidesOn()
        {
            var path = TrackSplineGraph.Build(
                new[] { new Vector3(0f, 4f, 0f), Vector3.zero },
                new[] { 0 }, new[] { 1 }).Path(0);
            _host = new GameObject("track-contract-host");

            _track = ToyTrackMeshBuilder.Build("E-contract", path, _host.transform);

            Mesh mesh = _track.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;

            // ToyTrainView pins its head anchor at board z -0.2 and bottoms every chassis
            // part out at anchor-local +0.235 — exactly RailCrownZ. Widening the bed must
            // never lift cream geometry through the plane the consist rides on.
            Assert.That(ToyTrackMeshBuilder.RailOffset * 2f, Is.EqualTo(0.5f).Within(0.0001f),
                "the consist is built to a 0.5 gauge");
            Assert.That(mesh.bounds.min.z,
                Is.EqualTo(ToyTrackMeshBuilder.RailCrownZ).Within(0.0001f),
                "the rail crown is the highest thing on the track, and it is a contract");
            Assert.That(mesh.GetTriangles(0).Min(index => vertices[index].z),
                Is.GreaterThan(ToyTrackMeshBuilder.RailCrownZ),
                "no cream surface may rise through the running plane or the train sinks");
        }

        [Test]
        public void Build_EverySubmeshEnclosesPositiveVolumeThroughATightTurnout()
        {
            _host = new GameObject("track-volume-host");

            _track = ToyTrackMeshBuilder.Build("E4", TightTurnout(), _host.transform);

            Mesh mesh = _track.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                int[] triangles = mesh.GetTriangles(submesh);
                double volume = 0.0;
                for (int triangle = 0; triangle < triangles.Length; triangle += 3)
                    volume += Vector3.Dot(vertices[triangles[triangle]],
                        Vector3.Cross(vertices[triangles[triangle + 1]],
                            vertices[triangles[triangle + 2]]));
                Assert.That(volume, Is.GreaterThan(0.0),
                    "submesh " + submesh + " encloses negative volume, so its faces are "
                    + "mirrored inward and backface culling erases them");
            }
        }

        [Test]
        public void Build_TopSurfacesFaceTheCameraEvenThroughATightTurnout()
        {
            _host = new GameObject("track-facing-host");

            _track = ToyTrackMeshBuilder.Build("E4", TightTurnout(), _host.transform);

            Mesh mesh = _track.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                int[] triangles = mesh.GetTriangles(submesh);
                float highest = triangles.Min(index => vertices[index].z);
                int checkedLids = 0;
                for (int triangle = 0; triangle < triangles.Length; triangle += 3)
                {
                    Vector3 a = vertices[triangles[triangle]];
                    Vector3 b = vertices[triangles[triangle + 1]];
                    Vector3 c = vertices[triangles[triangle + 2]];
                    if (Mathf.Max(a.z, Mathf.Max(b.z, c.z)) > highest + 0.04f) continue;
                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    if (Mathf.Abs(normal.z) < 0.5f * normal.magnitude) continue;
                    checkedLids++;
                    // The board's camera sits on the -z side: a lid you can see points at it.
                    Assert.That(normal.z, Is.LessThan(0f),
                        "a top face of submesh " + submesh + " points into the table — a "
                        + "swept ribbon that folds on a tight curve inverts exactly here");
                }
                Assert.That(checkedLids, Is.GreaterThan(8),
                    "submesh " + submesh + " must expose a real top surface to check");
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

        // The switch route from E4 of the existing cusp pin: node 1 leaves heading -y and
        // has to whip round to a node level with it, so this is the tightest curvature the
        // authored levels produce (radius ~0.17, well inside the bed's half width).
        private static TrackSpline TightTurnout() =>
            TrackSplineGraph.Build(
                new[]
                {
                    new Vector3(3f, 9f, 0f),
                    new Vector3(3f, 6f, 0f),
                    new Vector3(1f, 1f, 0f),
                    new Vector3(5f, 1f, 0f),
                    new Vector3(5f, 6f, 0f),
                },
                new[] { 0, 1, 1, 1 }, new[] { 1, 2, 3, 4 }).Path(3);

        // Submesh 0 holds the swept bed plus one island per sleeper tick; the bed is the
        // big one.
        private static int[] LargestComponent(int[] triangles) =>
            ConnectedVertexComponents(triangles)
                .OrderByDescending(component => component.Length)
                .First();

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
