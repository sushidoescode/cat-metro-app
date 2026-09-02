using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Domain.Solver;
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
            var raw = JObject.Parse(System.Text.Encoding.UTF8.GetString(L001Bytes()));
            var nodes = ((JArray)raw["board"]["nodes"]).Cast<JObject>().ToArray();
            var edges = ((JArray)raw["board"]["edges"]).Cast<JObject>().ToArray();
            var switches = ((JArray)raw["switches"]).Cast<JObject>().ToArray();

            Assert.That(maps.Nodes.Ids.ToArray(),
                Is.EqualTo(nodes.Select(node => (string)node["id"])), "authored node order");
            Assert.That(maps.Edges.Ids.ToArray(),
                Is.EqualTo(edges.Select(edge => (string)edge["id"])), "authored edge order");
            Assert.That(maps.Switches.Ids.ToArray(),
                Is.EqualTo(switches.Select(item => (string)item["id"])), "authored switch order");
            Assert.That(maps.Stations.Ids.ToArray(),
                Is.EqualTo(raw["stations"].Select(item => (string)item["nodeId"])), "authored station order");
            Assert.That(maps.Sources.Ids.ToArray(),
                Is.EqualTo(raw["sources"].Select(item => (string)item["nodeId"])), "authored source order");
            Assert.That(maps.WaveCount, Is.EqualTo(((JArray)raw["waves"]).Count));

            foreach (var map in new[] { maps.Nodes, maps.Edges, maps.Switches, maps.Stations, maps.Sources })
                for (int i = 0; i < map.Count; i++)
                    Assert.That(map.IndexOf(map.IdOf(i)), Is.EqualTo(i), "id<->index round trip");

            Assert.That(graph.NodeCount, Is.EqualTo(nodes.Length));
            Assert.That(graph.EdgeTravelTicks, Is.EqualTo(edges.Select(edge => (int)edge["travelTicks"])));
            Assert.That(graph.EdgeFrom,
                Is.EqualTo(edges.Select(edge => maps.Nodes.IndexOf((string)edge["from"]))));
            Assert.That(graph.EdgeTo,
                Is.EqualTo(edges.Select(edge => maps.Nodes.IndexOf((string)edge["to"]))));
            Assert.That(graph.SwitchInitialRoute,
                Is.EqualTo(switches.Select(item => (byte)(int)item["initialRoute"])));
            for (int i = 0; i < switches.Length; i++)
                Assert.That(graph.SwitchRoutes[i],
                    Is.EqualTo(switches[i]["routes"].Select(route => maps.Edges.IndexOf((string)route))),
                    "routes as dense edge indices");
            Assert.That(graph.WinDeliveries, Is.EqualTo((int)raw["win"]["deliveries"]));
            Assert.That(graph.TimeLimitTicks, Is.EqualTo((int)raw["win"]["timeLimitTicks"]));
            Assert.That(graph.QCapBound, Is.EqualTo(8), "A-C2a-4: schema max");
            Assert.That(graph.TrainsMax, Is.EqualTo(raw["waves"].Sum(wave => (int)wave["count"])),
                "A-C2a-4: sum of wave counts");
            Assert.That(graph.NodeQueueCapacity,
                Is.EqualTo(nodes.Select(node => (int?)(node["queueCapacity"]) ?? graph.QCapBound)),
                "authored capacities map, absent capacities materialize the schema maximum");
        }

        [Test] // the imported artifact replays its solver-proved command log
        public void L001_ImportedGraph_WinsUnderItsExactSolverReplay()
        {
            var r = LevelImporter.Import(L001Bytes());
            Assert.That(r.Ok, Is.True);
            var solve = LevelSolver.Solve(r.Value.Graph, (ulong)r.Value.Dto.Seed, 2_000_000);
            Assert.That(solve.Verdict, Is.EqualTo(SolveVerdict.Solved));
            Assert.That(solve.BeamWidthUsed, Is.Zero, "campaign proofs must remain exact BFS");
            var end = ReplayHasher.RunToEnd(r.Value.Graph, (ulong)r.Value.Dto.Seed, solve.OptimalLog);
            Assert.That(end.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            Assert.That(end.Deliveries, Is.EqualTo(r.Value.Dto.Win.Deliveries));
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
