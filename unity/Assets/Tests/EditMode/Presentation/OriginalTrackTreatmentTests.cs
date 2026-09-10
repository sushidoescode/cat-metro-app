using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatMetro.Content;
using CatMetro.Presentation.Board;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.EditMode.Presentation
{
    public sealed class OriginalTrackTreatmentTests
    {
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
        }

        [Test]
        public void Build_BindsOriginalAtlasAndKeepsEachSurfaceInsideItsColourSwatch()
        {
            _host = new GameObject("original-track-atlas");
            var path = TrackSplineGraph.Build(new[] { Vector3.zero, new Vector3(0, 4, 0) },
                new[] { 0 }, new[] { 1 }).Path(0);
            GameObject track = ToyTrackMeshBuilder.Build("atlas", path, _host.transform);
            Mesh mesh = track.GetComponent<MeshFilter>().sharedMesh;
            var atlas = Resources.Load<Texture2D>("Track/original-toy-atlas");
            Assert.That(atlas, Is.Not.Null, "Run CatMetroOriginalTrackImportPipeline.Build first.");
            Assert.That(atlas.width, Is.EqualTo(1024));
            Assert.That(atlas.height, Is.EqualTo(512));
            foreach (Material material in track.GetComponent<MeshRenderer>().sharedMaterials)
            {
                Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(atlas));
                Assert.That(material.GetColor("_BaseColor"), Is.EqualTo(Color.white),
                    "tinting the coloured atlas twice muddies the cream bed");
            }
            AssertAtlasUvs(mesh, "straight");
            var readable = new Texture2D(2, 2);
            try
            {
                Assert.That(ImageConversion.LoadImage(readable,
                    File.ReadAllBytes(AssetDatabase.GetAssetPath(atlas))), Is.True);
                Color bed = readable.GetPixelBilinear(.125f, .25f);
                Color wood = readable.GetPixelBilinear(.375f, .25f);
                Color navy = readable.GetPixelBilinear(.875f, .25f);
                Assert.That(Luminance(bed) - Luminance(wood), Is.GreaterThan(.18f));
                Assert.That(Luminance(bed) - Luminance(navy), Is.GreaterThan(.5f));
            }
            finally { Object.DestroyImmediate(readable); }
        }

        [Test]
        public void Build_SoftensRailShouldersWithoutMovingTheRunningPlaneOrGauge()
        {
            _host = new GameObject("original-track-shoulders");
            var path = TrackSplineGraph.Build(new[] { Vector3.zero, new Vector3(0, 4, 0) },
                new[] { 0 }, new[] { 1 }).Path(0);
            Mesh mesh = ToyTrackMeshBuilder.Build("round", path, _host.transform)
                .GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            Vector3[] rail = mesh.GetTriangles(1).Distinct().Select(i => vertices[i]).ToArray();
            Assert.That(rail.Any(p => Mathf.Abs(p.x) > .290f && p.z > .036f && p.z < .064f),
                Is.True, "the shoulder needs a rounded intermediate surface, not one flat chamfer");
            Assert.That(rail.Min(p => p.z), Is.EqualTo(.035f).Within(.00001f));
            Assert.That(rail.Max(p => Mathf.Abs(p.x)), Is.EqualTo(.315f).Within(.00001f));
            Assert.That(mesh.bounds.size.x, Is.EqualTo(.88f).Within(.00001f));
            Assert.That(mesh.bounds.max.z, Is.EqualTo(.34f).Within(.00001f));
        }

        [Test]
        public void EveryShippedLevel_PresentationGridBuildsClosedTrackBelowTheTrainWithBoundAtlasUvs()
        {
            string root = Path.Combine(UnityEngine.Application.streamingAssetsPath, "content", "levels");
            string[] files = Directory.GetFiles(root, "L*.json").OrderBy(p => p, StringComparer.Ordinal).ToArray();
            Assert.That(files.Length, Is.GreaterThanOrEqualTo(60));
            int checkedEdges = 0;
            foreach (string file in files)
            {
                var imported = LevelImporter.Import(File.ReadAllBytes(file));
                Assert.That(imported.Ok, Is.True, file);
                var nodes = imported.Value.Dto.Nodes.ToArray();
                var edges = imported.Value.Dto.Edges.ToArray();
                var indices = nodes.Select((n, i) => new { n.Id, Index = i }).ToDictionary(n => n.Id, n => n.Index);
                // Actual BoardView presentation grid, deliberately independent of the graph's raw-grid test.
                var positions = nodes.Select(n => new Vector3(n.X * .8f, n.Y * 1.47f, 0)).ToArray();
                var graph = TrackSplineGraph.Build(positions,
                    edges.Select(e => indices[e.From]).ToArray(), edges.Select(e => indices[e.To]).ToArray());
                _host = new GameObject("track-corpus-" + Path.GetFileNameWithoutExtension(file));
                try
                {
                    for (int e = 0; e < edges.Length; e++)
                    {
                        string label = Path.GetFileNameWithoutExtension(file) + ":" + edges[e].Id;
                        GameObject track = ToyTrackMeshBuilder.Build(edges[e].Id, graph.Path(e), _host.transform);
                        Mesh mesh = track.GetComponent<MeshFilter>().sharedMesh;
                        Vector3[] v = mesh.vertices;
                        Assert.That(v.All(p => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z)),
                            Is.True, label);
                        Assert.That(mesh.bounds.min.z, Is.EqualTo(.035f).Within(.00002f), label);
                        Assert.That(mesh.bounds.max.z, Is.EqualTo(.34f).Within(.00002f), label);
                        Assert.That(track.GetComponentsInChildren<Collider>(true), Is.Empty, label);
                        AssertAtlasUvs(mesh, label);
                        for (int s = 0; s < 3; s++) AssertClosedOutwardSurface(mesh, s, label);
                        Object.DestroyImmediate(track);
                        checkedEdges++;
                    }
                }
                finally { Object.DestroyImmediate(_host); _host = null; }
            }
            TestContext.WriteLine("Original track corpus: " + files.Length + " levels, " + checkedEdges + " generated edges.");
        }

        private static void AssertAtlasUvs(Mesh mesh, string label)
        {
            Vector2[] uv = mesh.uv;
            Assert.That(uv.Length, Is.EqualTo(mesh.vertexCount), label + " missing explicit atlas UVs");
            // Atlas bottom row: cream column 0, wood column 1, navy column 3.
            int[] columns = { 1, 3, 0 };
            for (int s = 0; s < 3; s++)
                foreach (int i in mesh.GetTriangles(s).Distinct())
                {
                    Assert.That(uv[i].x, Is.InRange(columns[s] * .25f + .01f, (columns[s] + 1) * .25f - .01f), label);
                    Assert.That(uv[i].y, Is.InRange(.02f, .48f), label);
                }
        }

        private static void AssertClosedOutwardSurface(Mesh mesh, int submesh, string label)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.GetTriangles(submesh);
            // Short edges can intentionally carry no sleeper or connector geometry.
            if (triangles.Length == 0) return;
            double volume = 0;
            var uses = new Dictionary<(int, int), int>();
            float highest = triangles.Min(i => vertices[i].z);
            int lids = 0;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                volume += Vector3.Dot(a, Vector3.Cross(b, c));
                for (int j = 0; j < 3; j++)
                {
                    int x = triangles[i + j], y = triangles[i + (j + 1) % 3];
                    var edge = (Math.Min(x, y), Math.Max(x, y));
                    uses.TryGetValue(edge, out int count); uses[edge] = count + 1;
                }
                if (Mathf.Max(a.z, Mathf.Max(b.z, c.z)) > highest + .04f
                    || Mathf.Abs(normal.z) < .5f * normal.magnitude || normal.sqrMagnitude < 1e-12f) continue;
                Assert.That(normal.z, Is.LessThan(0), label + " inward top, submesh " + submesh);
                lids++;
            }
            Assert.That(volume, Is.GreaterThan(0), label + " signed volume, submesh " + submesh);
            Assert.That(uses.Values.All(count => count == 2), Is.True, label + " open/nonmanifold edge, submesh " + submesh);
            Assert.That(lids, Is.GreaterThan(0), label + " no visible top, submesh " + submesh);
        }

        private static float Luminance(Color color) => .2126f * color.r + .7152f * color.g + .0722f * color.b;
    }
}
