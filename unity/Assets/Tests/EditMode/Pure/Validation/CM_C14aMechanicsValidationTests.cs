using System.IO;
using System.Linq;
using System.Text;
using CatMetro.Content;
using CatMetro.Content.Validation;
using CatMetro.Tests.Domain;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CatMetro.Tests.Validation
{
    [TestFixture]
    public class CM_C14aMechanicsValidationTests
    {
        [Test]
        public void WildEmitter_IsCompatibleWithAConcreteStation_InStagesTwoAndThree()
        {
            var bytes = VFixtures.Level(o =>
            {
                o["sources"][0]["allowedColors"] = new JArray("wild");
                o["waves"][0]["color"] = "wild";
            });
            var imported = VFixtures.Import(bytes);
            var dto = imported.Dto;

            var stage2 = StaticAnalysisStage.Check(dto);
            var stage3 = LowerBoundStage.Check(dto, VFixtures.BareConfig());

            Assert.That(stage2.Code, Is.Not.EqualTo(StageVerdictCode.Fail), stage2.Detail);
            Assert.That(stage2.Detail, Does.Not.Contain("station RED is a decoy"));
            Assert.That(stage3.Code, Is.Not.EqualTo(StageVerdictCode.Fail), stage3.Detail);
            int expectedMinTravel = ShortestAuthoredTravel(dto,
                dto.Sources.Span[0].NodeId,
                dto.Stations.ToArray().Select(station => station.NodeId).ToArray());
            Assert.That(stage3.Value, Does.Contain("minTravelTicks=" + expectedMinTravel));
        }

        [Test]
        public void StationSideWild_DoesNotMakeAConcreteEmitterCompatible()
        {
            var bytes = VFixtures.Level(o =>
            {
                o["stations"][0]["accepts"] = new JArray("wild");
                o["stations"][1]["accepts"] = new JArray("wild");
            });
            var dto = VFixtures.Import(bytes).Dto;

            var stage2 = StaticAnalysisStage.Check(dto);
            var stage3 = LowerBoundStage.Check(dto, VFixtures.BareConfig());

            Assert.That(stage2.Code, Is.EqualTo(StageVerdictCode.Warn),
                "stage 2's ratified decoy policy warns rather than blocks");
            Assert.That(stage2.Detail, Does.Contain("no source emits any colour it accepts"));
            Assert.That(stage3.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(stage3.Detail, Does.Contain("no colour-compatible"));
        }

        [Test]
        public void SecondSource_ReachabilityIsCheckedIndependently()
        {
            var anchor = LockedL018();
            var original = VFixtures.Import(Encoding.UTF8.GetBytes(anchor.ToString()));
            Assert.That(StaticAnalysisStage.Check(original.Dto).Code,
                Is.Not.EqualTo(StageVerdictCode.Fail));

            ((JArray)anchor["board"]["edges"])
                .Single(e => (string)e["id"] == "EB")
                .Remove();
            var disconnected = VFixtures.Import(Encoding.UTF8.GetBytes(anchor.ToString()));
            var verdict = StaticAnalysisStage.Check(disconnected.Dto);

            Assert.That(verdict.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(verdict.Detail, Does.Contain("BLU").And.Contain("not reachable"));
        }

        [Test]
        public void WaveColorMustBeAllowedByItsAuthoredSource_AndDanglingSourceStaysTyped()
        {
            var mismatch = VFixtures.Level(o => o["waves"][0]["color"] = "wild");
            var r = LevelImporter.Import(mismatch);
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Error.Kind, Is.EqualTo(ContentErrorKind.BoundViolation));
            Assert.That(r.Error.Detail, Does.Contain("allowedColors"));

            var dangling = VFixtures.Level(o => o["waves"][0]["sourceNode"] = "MISSING");
            r = LevelImporter.Import(dangling);
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Error.Kind, Is.EqualTo(ContentErrorKind.DanglingReference));
        }

        [Test]
        public void Importer_MapsWaveOriginToNoncontiguousNodeIndex_NotSourceOrdinal()
        {
            var bytes = VFixtures.Level(o =>
            {
                ((JArray)o["board"]["nodes"]).Add(VFixtures.Node("SRC2", 6, 9));
                ((JArray)o["board"]["edges"]).Add(
                    VFixtures.Edge("E4", "SRC2", "RED", 5));
                ((JArray)o["sources"]).Add(new JObject
                {
                    ["nodeId"] = "SRC2",
                    ["allowedColors"] = new JArray("red"),
                });
                o["waves"][0]["sourceNode"] = "SRC2";
            });

            var imported = VFixtures.Import(bytes);

            Assert.That(imported.Graph.SourceNodes, Is.EqualTo(new[] { 0, 4 }));
            Assert.That(imported.Graph.WaveSourceNode, Is.EqualTo(new[] { 4 }),
                "node index 4 must not be confused with source-list ordinal 1");
            var end = CatMetro.Domain.ReplayHasher.RunToEnd(
                imported.Graph, (ulong)imported.Dto.Seed, new CatMetro.Domain.CommandLog());
            Assert.That(end.Outcome.Kind, Is.EqualTo(CatMetro.Domain.OutcomeKind.Won),
                "the mapped wave actually travels SRC2 -> RED");
        }

        private static JObject LockedL018()
        {
            var path = Path.Combine(Fixtures.RepoRoot(),
                "docs", "plan", "data", "example_levels.json");
            var wrapper = JObject.Parse(File.ReadAllText(path));
            return (JObject)((JArray)wrapper["levels"])
                .Single(o => (string)o["id"] == "L018")
                .DeepClone();
        }

        private static int ShortestAuthoredTravel(
            LevelDto dto, string sourceNode, string[] stationNodes)
        {
            var distances = dto.Nodes.ToArray().ToDictionary(node => node.Id, _ => int.MaxValue);
            distances[sourceNode] = 0;
            for (int pass = 0; pass < distances.Count - 1; pass++)
            {
                bool changed = false;
                foreach (var edge in dto.Edges.ToArray())
                {
                    if (distances[edge.From] == int.MaxValue) continue;
                    int candidate = distances[edge.From] + edge.TravelTicks;
                    if (candidate >= distances[edge.To]) continue;
                    distances[edge.To] = candidate;
                    changed = true;
                }
                if (!changed) break;
            }
            return stationNodes.Min(node => distances[node]);
        }
    }
}
