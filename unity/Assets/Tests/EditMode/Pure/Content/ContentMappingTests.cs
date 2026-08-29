using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Content;
using CatMetro.Services;
using CatMetro.Tests.Domain;

namespace CatMetro.Tests.Content
{
    [TestFixture]
    public class ContentMappingTests
    {
        private static byte[] L001Bytes() =>
            File.ReadAllBytes(Path.Combine(Fixtures.RepoRoot(), "content", "levels", "L001.json"));

        private static byte[] Mutated(System.Action<JObject> mutate)
        {
            var j = JObject.Parse(System.Text.Encoding.UTF8.GetString(L001Bytes()));
            mutate(j);
            return System.Text.Encoding.UTF8.GetBytes(j.ToString());
        }

        [Test] // criterion 9 — bijection per collection + authored-order stability
        public void L001_IdMaps_AreBijectiveAndOrderStable()
        {
            var r = LevelImporter.Import(L001Bytes());
            Assert.That(r.Ok, Is.True, r.Ok ? "" : r.Error.ToString());
            var maps = r.Value.IdMaps;
            var graph = r.Value.Graph;

            Assert.That(maps.Nodes.Ids.ToArray(), Is.EqualTo(new[] { "SRC", "J1", "RED", "BLU" }), "authored node order");
            Assert.That(maps.Edges.Ids.ToArray(), Is.EqualTo(new[] { "E1", "E2", "E3" }));
            Assert.That(maps.Switches.Ids.ToArray(), Is.EqualTo(new[] { "S1" }));
            Assert.That(maps.Stations.Ids.ToArray(), Is.EqualTo(new[] { "RED", "BLU" }));
            Assert.That(maps.Sources.Ids.ToArray(), Is.EqualTo(new[] { "SRC" }));
            Assert.That(maps.WaveCount, Is.EqualTo(1));

            foreach (var map in new[] { maps.Nodes, maps.Edges, maps.Switches, maps.Stations, maps.Sources })
                for (int i = 0; i < map.Count; i++)
                    Assert.That(map.IndexOf(map.IdOf(i)), Is.EqualTo(i), "id<->index round trip");

            Assert.That(graph.NodeCount, Is.EqualTo(4));
            Assert.That(graph.EdgeTravelTicks, Is.EqualTo(new[] { 10, 12, 12 }));
            Assert.That(graph.EdgeFrom, Is.EqualTo(new[] { 0, 1, 1 }));
            Assert.That(graph.EdgeTo, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(graph.SwitchInitialRoute, Is.EqualTo(new byte[] { 1 }));
            Assert.That(graph.SwitchRoutes[0], Is.EqualTo(new[] { 1, 2 }), "routes as dense edge indices");
            Assert.That(graph.WinDeliveries, Is.EqualTo(2));
            Assert.That(graph.TimeLimitTicks, Is.EqualTo(160));
            Assert.That(graph.QCapBound, Is.EqualTo(8), "A-C2a-4: schema max");
            Assert.That(graph.TrainsMax, Is.EqualTo(2), "A-C2a-4: sum of wave counts");
            Assert.That(graph.NodeQueueCapacity, Is.EqualTo(new[] { 8, 8, 8, 8 }), "A-C2a-6: absent -> schema max");
        }

        [Test] // criterion 9 — the graph runs identically to CM-C1's in-code fixture path
        public void L001_ImportedGraph_WinsUnderTheGoldenCommandLog()
        {
            var r = LevelImporter.Import(L001Bytes());
            Assert.That(r.Ok, Is.True);
            var end = Fixtures.RunThroughTick(r.Value.Graph, 1001, Fixtures.GoldenLog(), 159);
            Assert.That(end.Outcome.Kind, Is.EqualTo(CatMetro.Domain.OutcomeKind.Won),
                "the imported graph must play out exactly like the authored level");
            Assert.That(end.Deliveries, Is.EqualTo(2));
        }

        [Test] // CM-C14a — the former pin sites now map both ratified mechanics
        public void EnabledMechanics_ImportAndMapWithoutEscapingExceptions()
        {
            ContentResult<ImportedLevel> r = default;

            var second = Mutated(j =>
            {
                ((JArray)j["sources"]).Add(new JObject(
                    new JProperty("nodeId", "J1"),
                    new JProperty("allowedColors", new JArray("red"))));
                j["waves"][0]["sourceNode"] = "J1";
            });
            Assert.DoesNotThrow(() => r = LevelImporter.Import(second));
            Assert.That(r.Ok, Is.True, r.Ok ? "" : r.Error.ToString());
            Assert.That(r.Value.Graph.SourceNodes, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(r.Value.Graph.WaveSourceNode, Is.EqualTo(new[] { 1 }));

            var wild = Mutated(j =>
            {
                j["sources"][0]["allowedColors"] = new JArray("wild");
                j["waves"][0]["color"] = "wild";
            });
            Assert.DoesNotThrow(() => r = LevelImporter.Import(wild));
            Assert.That(r.Ok, Is.True, r.Ok ? "" : r.Error.ToString());
            Assert.That(r.Value.Graph.WaveColor, Is.EqualTo(new[] { CatMetro.Domain.CatColor.Wild }));

            Assert.DoesNotThrow(() => r = LevelImporter.Import(L001Bytes()));
            Assert.That(r.Ok, Is.True, "L001 raises nothing");
        }

        [Test] // criterion 11 — the read seam; Content never touches a filesystem
        public async Task Importer_DrivesThroughInMemoryContentSource()
        {
            var source = new InMemorySource(new Dictionary<string, byte[]>
            {
                ["levels/L001.json"] = L001Bytes(),
            });
            var r = await LevelImporter.ImportFromSourceAsync(source, "levels/L001.json", CancellationToken.None);
            Assert.That(r.Ok, Is.True);
            Assert.That(r.Value.Dto.Id, Is.EqualTo("L001"));
        }

        [Test] // review F2 — the seam is TOTAL: throw/cancel/null-source/null-task all typed
        public async Task Importer_SourceSeamNeverLetsAnExceptionEscape()
        {
            var r = await LevelImporter.ImportFromSourceAsync(new ThrowingSource(), "x", CancellationToken.None);
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Error.Kind, Is.EqualTo(ContentErrorKind.SourceReadFailed), "throwing source");

            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                r = await LevelImporter.ImportFromSourceAsync(new CancellingSource(), "x", cts.Token);
                Assert.That(r.Ok, Is.False);
                Assert.That(r.Error.Kind, Is.EqualTo(ContentErrorKind.SourceReadFailed), "pre-cancelled token");
            }

            r = await LevelImporter.ImportFromSourceAsync(null, "x", CancellationToken.None);
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Error.Kind, Is.EqualTo(ContentErrorKind.SourceReadFailed), "null source");

            r = await LevelImporter.ImportFromSourceAsync(new NullTaskSource(), "x", CancellationToken.None);
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Error.Kind, Is.EqualTo(ContentErrorKind.SourceReadFailed), "null task");
        }

        [Test] // CM-C14a — Wild is legal in all schema color positions; unknown stays typed
        public void WildColors_ImportInAcceptsAndAllowedColors_WhileUnknownIsRejected()
        {
            ContentResult<ImportedLevel> r = default;

            var wildAccepts = Mutated(j => j["stations"][0]["accepts"] = new JArray("wild"));
            Assert.DoesNotThrow(() => r = LevelImporter.Import(wildAccepts));
            Assert.That(r.Ok, Is.True, r.Ok ? "" : r.Error.ToString());
            Assert.That(r.Value.Graph.StationAccepts[0],
                Is.EqualTo(new[] { CatMetro.Domain.CatColor.Wild }));

            var wildAllowed = Mutated(j =>
                j["sources"][0]["allowedColors"] = new JArray("red", "wild"));
            Assert.DoesNotThrow(() => r = LevelImporter.Import(wildAllowed));
            Assert.That(r.Ok, Is.True, r.Ok ? "" : r.Error.ToString());
            Assert.That(r.Value.Dto.Sources.Span[0].AllowedColors.ToArray(),
                Is.EqualTo(new[] { "red", "wild" }));

            var unknown = Mutated(j => j["sources"][0]["allowedColors"] = new JArray("chartreuse"));
            Assert.DoesNotThrow(() => r = LevelImporter.Import(unknown));
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Error.Kind, Is.EqualTo(ContentErrorKind.BoundViolation), "unknown color");
        }

        [Test] // review F5 — well-formed-input failures carry their true discriminants
        public void DuplicateStationNode_And_HugeSeed_TypedCorrectly()
        {
            ContentResult<ImportedLevel> r = default;

            var dupStation = Mutated(j => ((JArray)j["stations"]).Add(new JObject(
                new JProperty("nodeId", "RED"), new JProperty("accepts", new JArray("red")), new JProperty("capacity", 6))));
            Assert.DoesNotThrow(() => r = LevelImporter.Import(dupStation));
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Error.Kind, Is.EqualTo(ContentErrorKind.DuplicateId), "duplicate station nodeId");

            var hugeSeed = Mutated(j => j["seed"] = Newtonsoft.Json.Linq.JToken.Parse("99999999999999999999999999"));
            Assert.DoesNotThrow(() => r = LevelImporter.Import(hugeSeed));
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Error.Kind, Is.EqualTo(ContentErrorKind.BoundViolation), "BigInteger seed");
        }

        private sealed class InMemorySource : IContentSource
        {
            private readonly Dictionary<string, byte[]> _files;
            public InMemorySource(Dictionary<string, byte[]> files) { _files = files; }
            public Task<byte[]> ReadAsync(string relativePath, CancellationToken ct) => Task.FromResult(_files[relativePath]);
            public bool Exists(string relativePath) => _files.ContainsKey(relativePath);
        }

        private sealed class ThrowingSource : IContentSource
        {
            public Task<byte[]> ReadAsync(string relativePath, CancellationToken ct) =>
                throw new System.InvalidOperationException("device read failed");
            public bool Exists(string relativePath) => true;
        }

        private sealed class CancellingSource : IContentSource
        {
            public Task<byte[]> ReadAsync(string relativePath, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(new byte[0]);
            }
            public bool Exists(string relativePath) => true;
        }

        private sealed class NullTaskSource : IContentSource
        {
            public Task<byte[]> ReadAsync(string relativePath, CancellationToken ct) => null;
            public bool Exists(string relativePath) => true;
        }
    }
}
