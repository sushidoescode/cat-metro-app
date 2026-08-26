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
        public void Build_LongEdgeCarriesOnePuzzleJoinInItsFinalThird()
        {
            var path = TrackSplineGraph.Build(
                new[] { new Vector3(0f, 4f, 0f), Vector3.zero },
                new[] { 0 }, new[] { 1 }).Path(0);
            _host = new GameObject("track-join-host");

            _track = ToyTrackMeshBuilder.Build("E-join", path, _host.transform);

            Mesh mesh = _track.GetComponent<MeshFilter>().sharedMesh;
            int[] seam = SeamIsland(mesh);

            Assert.That(seam, Is.Not.Empty, "a long edge must carry a puzzle join");

            // Where the seam LINE crosses, not where the island's centroid sits — the
            // lobe is most of the outline's vertices and drags a plain centroid ~0.13
            // downstream of the seam itself.
            float distance = NearestDistanceAlong(path, SeamLineCentre(mesh, path, seam));
            Assert.That(distance / path.Length, Is.GreaterThan(2f / 3f),
                "the seam marks where this piece hands over to the next one, so it "
                + "belongs hard against the node — a seam in the middle of a run "
                + "reads as a scratch, not a join");
            Assert.That(path.Length - distance,
                Is.EqualTo(ToyTrackMeshBuilder.JoinInset).Within(0.05f),
                "and it sits exactly JoinInset back from the edge's end");
        }

        [Test]
        public void Build_ShortEdgeGetsNoJoinRatherThanOneStrandedMidRun()
        {
            // The seam has to clear the neighbouring bed that overlaps this one at a
            // shared node (JoinInset) AND still land in the final third. Below
            // 3 x JoinInset those cannot both hold, and a seam floating at the middle
            // of a short run is worse than no seam at all.
            var path = TrackSplineGraph.Build(
                new[] { new Vector3(0f, 2f, 0f), Vector3.zero },
                new[] { 0 }, new[] { 1 }).Path(0);
            Assert.That(path.Length, Is.LessThan(ToyTrackMeshBuilder.JoinMinimumLength));
            _host = new GameObject("track-shortjoin-host");

            _track = ToyTrackMeshBuilder.Build("E-short", path, _host.transform);

            Mesh mesh = _track.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(SeamIsland(mesh), Is.Empty,
                "a short edge must go without rather than carry a stranded seam");
            foreach (int[] island in ConnectedVertexComponents(mesh.GetTriangles(0)))
                Assert.That(island.Length == 16 || island.Length > 16 * 3, Is.True,
                    "submesh 0 should hold only the bed and 16-vertex sleeper ticks here");
        }

        [Test]
        public void Build_JoinSeamCrossesTheBedAndItsLobeActuallyInterlocks()
        {
            var path = TrackSplineGraph.Build(
                new[] { new Vector3(0f, 4f, 0f), Vector3.zero },
                new[] { 0 }, new[] { 1 }).Path(0);
            _host = new GameObject("track-joinshape-host");

            _track = ToyTrackMeshBuilder.Build("E-joinshape", path, _host.transform);

            Mesh mesh = _track.GetComponent<MeshFilter>().sharedMesh;
            int[] seam = SeamIsland(mesh);

            // Measure in the seam's OWN frame rather than in board x/y. Which way the
            // spline happens to run is not the point being tested, and reading it
            // backwards silently inverts "head" and "neck".
            List<Vector2> outline = SeamFootprint(mesh, path, seam);
            float lateral = outline.Max(p => p.x) - outline.Min(p => p.x);
            float forward = outline.Max(p => p.y);

            Assert.That(lateral, Is.GreaterThan(0.9f),
                "the seam must read right across the bed, not just between the rails");
            Assert.That(forward, Is.GreaterThan(0.25f),
                "a straight butt joint is not a puzzle piece — a lobe has to stand "
                + "off the seam line, and it points along +tangent so every piece "
                + "carries its tab at the downstream end");

            // The interlock, stated as geometry: the head has to be wider than the
            // mouth of the neck it hangs off, or the two pieces would pull straight
            // apart and it would be a wiggly butt joint, not a tab and socket.
            float headWidth = SpanBetween(outline, forward - 0.15f, float.PositiveInfinity);
            float neckOpening = NeckOpening(outline, 0.005f, 0.045f);
            Assert.That(neckOpening, Is.GreaterThan(0f), "the neck must be a real gap");
            Assert.That(headWidth, Is.GreaterThan(neckOpening * 1.4f),
                "the lobe must be a tab-and-socket head, wider than its own neck");
        }

        [Test]
        public void Build_JoinSeamEnclosesPositiveVolumeAndItsLidFacesTheCamera()
        {
            // NOT the centroid check Build_SleeperFacesAreWoundOutwardForBackfaceCulling
            // uses: that proxy only holds for a convex island, and the seam is a long
            // non-convex ribbon whose mushroom neck has inner walls that legitimately
            // face inward. Signed volume is the statement that actually holds for any
            // closed solid, and the lid normals are what backface culling reads.
            _host = new GameObject("track-joinwinding-host");

            _track = ToyTrackMeshBuilder.Build("E4", SeamedTightTurnout(), _host.transform);

            Mesh mesh = _track.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int[] seam = SeamIsland(mesh);
            Assert.That(seam, Is.Not.Empty);

            var members = new HashSet<int>(seam);
            int[] triangles = mesh.GetTriangles(0);
            double volume = 0.0;
            float highest = seam.Min(index => vertices[index].z);
            int checkedLids = 0;
            for (int triangle = 0; triangle < triangles.Length; triangle += 3)
            {
                if (!members.Contains(triangles[triangle])) continue;
                Vector3 a = vertices[triangles[triangle]];
                Vector3 b = vertices[triangles[triangle + 1]];
                Vector3 c = vertices[triangles[triangle + 2]];
                volume += Vector3.Dot(a, Vector3.Cross(b, c));

                if (Mathf.Max(a.z, Mathf.Max(b.z, c.z)) > highest + 0.004f) continue;
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (Mathf.Abs(normal.z) < 0.5f * normal.magnitude) continue;
                checkedLids++;
                Assert.That(normal.z, Is.LessThan(0f),
                    "a seam lid points into the table — the footprint basis is "
                    + "mirrored, so the strip must be enumerated in reverse");
            }

            Assert.That(volume, Is.GreaterThan(0.0),
                "the seam island encloses negative volume, so its faces are mirrored "
                + "inward and backface culling erases the whole join");
            Assert.That(checkedLids, Is.GreaterThan(8),
                "the seam must expose a real top surface to check");
        }

        [Test]
        public void Build_JoinSeamSitsAtTheTickReliefAndNeverBreaksTheRunningPlane()
        {
            var path = TrackSplineGraph.Build(
                new[] { new Vector3(0f, 4f, 0f), Vector3.zero },
                new[] { 0 }, new[] { 1 }).Path(0);
            _host = new GameObject("track-joinrelief-host");

            _track = ToyTrackMeshBuilder.Build("E-joinrelief", path, _host.transform);

            Mesh mesh = _track.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int[] seam = SeamIsland(mesh);
            int[] bed = LargestComponent(mesh.GetTriangles(0));

            float seamTop = seam.Min(index => vertices[index].z);
            float bedCrown = bed.Min(index => vertices[index].z);

            Assert.That(seamTop, Is.GreaterThan(ToyTrackMeshBuilder.RailCrownZ),
                "the seam must stay below the plane the consist rides on");
            Assert.That(mesh.bounds.min.z,
                Is.EqualTo(ToyTrackMeshBuilder.RailCrownZ).Within(0.0001f),
                "and adding it must not disturb the rail-crown contract");
            Assert.That(bedCrown - seamTop, Is.InRange(0.004f, 0.016f),
                "the seam is a hairline at the same relief as a tie mark — at this "
                + "scale that is about one pixel, which is the whole reason it can be "
                + "embossed instead of cut");
            Assert.That(seamTop, Is.GreaterThanOrEqualTo(bedCrown - 0.016f),
                "and it must never become the highest cream thing on the track");
        }

        [Test]
        public void Build_SeamedHairpinKeepsPositiveVolumeAndCameraFacingLids()
        {
            // TightTurnout() is only 2.17 long, so it carries no seam and the existing
            // fold pins never see one. This is the corpus's tightest curving edge that
            // IS long enough (L008 E4, radius ~0.19 at its hardest, 4.12 long).
            _host = new GameObject("track-seamedhairpin-host");
            TrackSpline path = SeamedTightTurnout();
            Assert.That(path.Length,
                Is.GreaterThan(ToyTrackMeshBuilder.JoinMinimumLength));

            _track = ToyTrackMeshBuilder.Build("E4", path, _host.transform);

            Mesh mesh = _track.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            Assert.That(SeamIsland(mesh), Is.Not.Empty, "this hairpin must carry a seam");

            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                int[] triangles = mesh.GetTriangles(submesh);
                double volume = 0.0;
                for (int triangle = 0; triangle < triangles.Length; triangle += 3)
                    volume += Vector3.Dot(vertices[triangles[triangle]],
                        Vector3.Cross(vertices[triangles[triangle + 1]],
                            vertices[triangles[triangle + 2]]));
                Assert.That(volume, Is.GreaterThan(0.0),
                    "submesh " + submesh + " inverted once the seam was added");

                float highest = triangles.Min(index => vertices[index].z);
                for (int triangle = 0; triangle < triangles.Length; triangle += 3)
                {
                    Vector3 a = vertices[triangles[triangle]];
                    Vector3 b = vertices[triangles[triangle + 1]];
                    Vector3 c = vertices[triangles[triangle + 2]];
                    if (Mathf.Max(a.z, Mathf.Max(b.z, c.z)) > highest + 0.04f) continue;
                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    if (Mathf.Abs(normal.z) < 0.5f * normal.magnitude) continue;
                    Assert.That(normal.z, Is.LessThan(0f),
                        "a top face of submesh " + submesh + " points into the table");
                }
            }
        }

        [Test]
        public void Build_ThreeEdgesAtATurnoutDoNotPileTheirSeamsUpOnTheNode()
        {
            // L001's switch. Anchoring the seam to each edge's END puts exactly one at
            // the node an edge arrives at; anchoring it to the START would put three
            // seams 0.338 apart here, well inside one 1.08 bed width, and they would
            // smear into each other while the branches are still overlapping.
            var junction = new Vector3(3f, 6f, 0f);
            var graph = TrackSplineGraph.Build(
                new[]
                {
                    new Vector3(3f, 9f, 0f), junction,
                    new Vector3(1f, 2f, 0f), new Vector3(5f, 2f, 0f),
                },
                new[] { 0, 1, 1 }, new[] { 1, 2, 3 });
            _host = new GameObject("track-turnoutseam-host");

            var centres = new List<Vector3>();
            for (int edge = 0; edge < 3; edge++)
            {
                GameObject built = ToyTrackMeshBuilder.Build(
                    "E" + edge, graph.Path(edge), _host.transform);
                Mesh mesh = built.GetComponent<MeshFilter>().sharedMesh;
                Vector3[] vertices = mesh.vertices;
                int[] seam = SeamIsland(mesh);
                Assert.That(seam, Is.Not.Empty, "E" + edge + " should carry a seam");
                Vector3 centroid = Vector3.zero;
                foreach (int index in seam) centroid += vertices[index];
                centres.Add(centroid / seam.Length);
            }

            for (int a = 0; a < centres.Count; a++)
                for (int b = a + 1; b < centres.Count; b++)
                    Assert.That(Vector3.Distance(centres[a], centres[b]),
                        Is.GreaterThan(1.08f),
                        "two seams closer than a bed width read as one smeared mark");

            int atJunction = centres.Count(
                centre => Vector3.Distance(centre, junction)
                    < ToyTrackMeshBuilder.JoinInset + 0.2f);
            Assert.That(atJunction, Is.EqualTo(1),
                "exactly one piece boundary belongs at a turnout, on the edge that "
                + "arrives — not one per branch leaving it");
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

        // The tightest curving edge in the authored corpus that is also long enough to
        // carry a seam: L008 E4, J2 -> BLU, radius ~0.19 at its hardest and 4.12 long.
        private static TrackSpline SeamedTightTurnout() =>
            TrackSplineGraph.Build(
                new[]
                {
                    new Vector3(3f, 9f, 0f),
                    new Vector3(3f, 7f, 0f),
                    new Vector3(1f, 3f, 0f),
                    new Vector3(5f, 7f, 0f),
                    new Vector3(5f, 3f, 0f),
                    new Vector3(7f, 7f, 0f),
                },
                new[] { 0, 1, 1, 3, 3 }, new[] { 1, 2, 3, 4, 5 }).Path(3);

        // Submesh 0 holds, in descending size: the swept bed, then the join seam if the
        // edge earned one, then a 16-vertex island per sleeper tick. The bed is always
        // the biggest — the shortest seamed edge still sweeps 18 rings of 7 — so the
        // seam is the second island, and its absence is what a short edge looks like.
        private static int[] SeamIsland(Mesh mesh)
        {
            List<int[]> islands = ConnectedVertexComponents(mesh.GetTriangles(0))
                .OrderByDescending(island => island.Length)
                .ToList();
            if (islands.Count < 2 || islands[1].Length <= 16) return new int[0];
            return islands[1];
        }

        private static float NearestDistanceAlong(TrackSpline path, Vector3 point)
        {
            float best = 0f;
            float nearest = float.PositiveInfinity;
            for (int step = 0; step <= 400; step++)
            {
                float fraction = step / 400f;
                float away = Vector3.Distance(path.EvaluateDistanceFraction(fraction), point);
                if (away >= nearest) continue;
                nearest = away;
                best = fraction * path.Length;
            }
            return best;
        }

        // The seam's vertices projected into its own (lateral, tangent) frame at the
        // station the builder placed it, with the origin on the seam line.
        private static List<Vector2> SeamFootprint(Mesh mesh, TrackSpline path, int[] seam)
        {
            float fraction = ToyTrackMeshBuilder.JoinDistance(path) / path.Length;
            Vector3 centre = path.EvaluateDistanceFraction(fraction);
            Vector3 tangent = path.TangentDistanceFraction(fraction);
            var lateral = new Vector3(-tangent.y, tangent.x, 0f).normalized;

            Vector3[] vertices = mesh.vertices;
            var footprint = new List<Vector2>(seam.Length);
            foreach (int index in seam)
            {
                Vector3 offset = vertices[index] - centre;
                footprint.Add(new Vector2(
                    Vector3.Dot(offset, lateral), Vector3.Dot(offset, tangent)));
            }
            return footprint;
        }

        // The midpoint of the seam's two outboard arms. Those sit at the bed rim with
        // the seam line running through them, so they locate the join itself rather
        // than the lobe hanging off it.
        private static Vector3 SeamLineCentre(Mesh mesh, TrackSpline path, int[] seam)
        {
            float fraction = ToyTrackMeshBuilder.JoinDistance(path) / path.Length;
            Vector3 centre = path.EvaluateDistanceFraction(fraction);
            Vector3 tangent = path.TangentDistanceFraction(fraction);
            var lateral = new Vector3(-tangent.y, tangent.x, 0f).normalized;

            Vector3[] vertices = mesh.vertices;
            Vector3 total = Vector3.zero;
            int counted = 0;
            foreach (int index in seam)
            {
                if (Mathf.Abs(Vector3.Dot(vertices[index] - centre, lateral)) < 0.4f) continue;
                total += vertices[index];
                counted++;
            }
            Assert.That(counted, Is.GreaterThan(0), "the seam must reach the bed's rim");
            return total / counted;
        }

        private static float SpanBetween(List<Vector2> footprint, float from, float to)
        {
            List<float> band = footprint
                .Where(p => p.y >= from && p.y <= to)
                .Select(p => p.x).ToList();
            return band.Count == 0 ? 0f : band.Max() - band.Min();
        }

        // The gap the neck leaves between its two walls — the mouth the tab has to be
        // too fat to slip back out of.
        private static float NeckOpening(List<Vector2> footprint, float from, float to)
        {
            List<Vector2> band = footprint.Where(p => p.y >= from && p.y <= to).ToList();
            List<float> left = band.Where(p => p.x < 0f).Select(p => p.x).ToList();
            List<float> right = band.Where(p => p.x > 0f).Select(p => p.x).ToList();
            if (left.Count == 0 || right.Count == 0) return 0f;
            return right.Min() - left.Max();
        }

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
