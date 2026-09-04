using System.Linq;
using System.Text.RegularExpressions;
using CatMetro.Content;
using CatMetro.Content.Daily;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CatMetro.Tests.Daily
{
    public sealed class DailyArtifactTests
    {
        private static DailyRunReport ThreeDateReport()
        {
            var dates = DateKeys.Enumerate("2026-08-24", 3);
            return DFixtures.Run(DFixtures.Request(
                dates, new DFixtures.FixedFactory(DFixtures.L001Dto())));
        }

        [Test]
        public void ToJson_CarriesTheRecordShapePerDate()
        {
            var root = JObject.Parse(ThreeDateReport().ToJson());
            var dates = (JArray)root["dates"];
            Assert.That(dates, Has.Count.EqualTo(3));
            foreach (var token in dates)
            {
                var record = (JObject)token;
                foreach (var key in new[]
                         { "dateKey", "k", "seed", "verdict", "stageVerdicts", "solverCompletionTicks" })
                    Assert.That(record[key], Is.Not.Null, $"record key '{key}' missing");
                var stages = (JArray)record["stageVerdicts"];
                Assert.That(stages.Count, Is.GreaterThanOrEqualTo(11));
                foreach (var stage in stages.Cast<JObject>())
                    foreach (var key in new[] { "stage", "code", "detail", "value", "blocks" })
                        Assert.That(stage[key], Is.Not.Null, $"stage key '{key}' missing");
            }
        }

        [Test]
        public void IdenticalRunsRenderByteIdenticalJson()
        {
            string first = ThreeDateReport().ToJson();
            string second = ThreeDateReport().ToJson();
            Assert.That(second, Is.EqualTo(first));
            Assert.That(JObject.Parse(first)["dates"], Has.Count.EqualTo(3));
        }

        [Test]
        public void ToJson_UsesTheSelectedGeneratorLabel()
        {
            var historical = DFixtures.Request(new[] { "2026-08-24" },
                new DFixtures.FixedFactory(DFixtures.L001Dto()));
            var runtime = new DailyRunRequest(
                historical.SchemaBytes,
                historical.ValidatorConfig,
                historical.PipelineConfig,
                historical.WeekdayCurveBytes,
                historical.DateKeys,
                historical.Factory,
                historical.ReferenceTimestamp,
                historical.BoardProvenance,
                DailyLineSeedScheme.Instance,
                historical.MaxNodesExpanded);

            Assert.That(DFixtures.Run(runtime).ToJson(),
                Does.StartWith("{\n  \"generator\": \"CM-DAILY-"));
            Assert.That(ThreeDateReport().ToJson(),
                Does.StartWith("{\n  \"generator\": \"CM-DAILY-1\""));
        }

        [Test]
        public void SeedLines_OnePerDate_AnchoredFormat()
        {
            var report = ThreeDateReport();
            var lines = report.SeedLines();
            Assert.That(lines, Has.Count.EqualTo(report.Records.Count));
            var format = new Regex(@"^DAILY_SEED \d{4}-\d{2}-\d{2} \d+ \d+$");
            Assert.That(lines, Is.Unique);
            for (int i = 0; i < lines.Count; i++)
            {
                Assert.That(format.IsMatch(lines[i]), Is.True, lines[i]);
                var record = report.Records[i];
                Assert.That(lines[i], Is.EqualTo(
                    $"DAILY_SEED {record.DateKey} {record.K} {record.Seed}"));
            }
        }

        [Test]
        public void RoundTrip_L001_ReimportsTheSameContentShape()
        {
            var dto = DFixtures.L001Dto();
            string text = DailyBoardJson.Serialize(dto);
            var back = LevelImporter.Import(System.Text.Encoding.UTF8.GetBytes(text));
            Assert.That(back.Ok, Is.True, $"round-trip must import: {back.Error}");
            var roundTripped = back.Value.Dto;

            Assert.That(roundTripped.Id, Is.EqualTo(dto.Id));
            Assert.That(roundTripped.Seed, Is.EqualTo(dto.Seed));
            Assert.That(roundTripped.SchemaVersion, Is.EqualTo(dto.SchemaVersion));
            Assert.That(roundTripped.Meta.Band, Is.EqualTo(dto.Meta.Band));
            Assert.That(roundTripped.Meta.NewMechanic, Is.EqualTo(dto.Meta.NewMechanic));
            Assert.That(roundTripped.Meta.MinActionWindowTicks, Is.EqualTo(dto.Meta.MinActionWindowTicks));
            Assert.That(roundTripped.Nodes.Length, Is.EqualTo(dto.Nodes.Length));
            Assert.That(roundTripped.Edges.Length, Is.EqualTo(dto.Edges.Length));
            Assert.That(roundTripped.Waves.Length, Is.EqualTo(dto.Waves.Length));
            Assert.That(roundTripped.Win.TimeLimitTicks, Is.EqualTo(dto.Win.TimeLimitTicks));
            Assert.That(roundTripped.Win.Deliveries, Is.EqualTo(dto.Win.Deliveries));
            Assert.That(roundTripped.Win.PerfectMaxSwitches, Is.EqualTo(dto.Win.PerfectMaxSwitches));
        }

        [Test]
        public void RoundTrip_NonDefaultMechanicFieldsPreserveTheCompleteDto()
        {
            var dto = DFixtures.AllFieldsDto();
            string text = DailyBoardJson.Serialize(dto);
            var json = JObject.Parse(text);

            Assert.That((bool)json["board"]["edges"][0]["oneWay"], Is.False);
            Assert.That((bool)json["board"]["edges"][0]["reversible"], Is.True);
            Assert.That((bool)json["board"]["edges"][0]["tunnel"], Is.True);
            Assert.That((bool)json["board"]["edges"][1]["hold"], Is.True);
            Assert.That((string)json["stations"][0]["shape"], Is.EqualTo("triangle"));
            Assert.That((int)json["switches"][0]["cooldownTicks"], Is.EqualTo(5));
            Assert.That(json["gates"][0]["openWindows"].ToObject<int[][]>(),
                Is.EqualTo(new[] { new[] { 2, 6 }, new[] { 10, 14 } }));
            Assert.That((int)json["gates"][0]["previewTicks"], Is.EqualTo(9));
            Assert.That((bool)json["waves"][0]["express"], Is.True);
            Assert.That((string)json["waves"][0]["shape"], Is.EqualTo("square"));
            Assert.That((bool)json["waves"][0]["stray"], Is.True);
            Assert.That(json["tags"].Values<string>(),
                Is.EqualTo(new[] { "daily", "field-probe" }));

            var back = LevelImporter.Import(System.Text.Encoding.UTF8.GetBytes(text));
            Assert.That(back.Ok, Is.True, $"round-trip must import: {back.Error}");
            AssertCompleteDto(back.Value.Dto, dto);
        }

        [Test]
        public void RoundTrip_PreservesAbsentKeys()
        {
            string text = DailyBoardJson.Serialize(DFixtures.L001Dto());
            Assert.That(text, Does.Not.Contain("queueCapacity"));
            Assert.That(text, Does.Not.Contain("validatedAt"));
        }

        [Test]
        public void RoundTrip_UnbudgetedWinOmitsTheOptionalBudgetKey()
        {
            var dto = DFixtures.L001Dto();
            var unbudgeted = new LevelDto(dto.SchemaVersion, dto.Id, dto.Name, dto.Seed, dto.Meta,
                dto.Nodes.ToArray(), dto.Edges.ToArray(), dto.Sources.ToArray(),
                dto.Stations.ToArray(), dto.Switches.ToArray(), dto.Waves.ToArray(),
                new WinDto(dto.Win.Deliveries, dto.Win.TimeLimitTicks,
                    CatMetro.Domain.FlipBudget.Unbudgeted, dto.Win.Stars),
                dto.Economy, dto.Gates.ToArray(), dto.Tags.ToArray());

            string text = DailyBoardJson.Serialize(unbudgeted);
            Assert.That(text, Does.Not.Contain("perfectMaxSwitches"));
            var back = LevelImporter.Import(System.Text.Encoding.UTF8.GetBytes(text));
            Assert.That(back.Ok, Is.True, $"round-trip must import: {back.Error}");
            Assert.That(back.Value.Dto.Win.PerfectMaxSwitches,
                Is.EqualTo(CatMetro.Domain.FlipBudget.Unbudgeted));
        }

        [Test]
        public void SolverCompletionTicks_PositiveForASolvedBoard()
        {
            Assert.That(ThreeDateReport().Records[0].SolverCompletionTicks, Is.GreaterThan(0));
        }

        private static void AssertCompleteDto(LevelDto actual, LevelDto expected)
        {
            Assert.That(actual.SchemaVersion, Is.EqualTo(expected.SchemaVersion));
            Assert.That(actual.Id, Is.EqualTo(expected.Id));
            Assert.That(actual.Name, Is.EqualTo(expected.Name));
            Assert.That(actual.Seed, Is.EqualTo(expected.Seed));
            Assert.That(actual.Meta.Band, Is.EqualTo(expected.Meta.Band));
            Assert.That(actual.Meta.DifficultyTarget, Is.EqualTo(expected.Meta.DifficultyTarget));
            Assert.That(actual.Meta.Mechanics.ToArray(), Is.EqualTo(expected.Meta.Mechanics.ToArray()));
            Assert.That(actual.Meta.NewMechanic, Is.EqualTo(expected.Meta.NewMechanic));
            Assert.That(actual.Meta.TeachingGoal, Is.EqualTo(expected.Meta.TeachingGoal));
            Assert.That(actual.Meta.MinActionWindowTicks,
                Is.EqualTo(expected.Meta.MinActionWindowTicks));
            Assert.That(actual.Meta.AuthoredBy, Is.EqualTo(expected.Meta.AuthoredBy));
            Assert.That(actual.Meta.ValidatedAt, Is.EqualTo(expected.Meta.ValidatedAt));
            Assert.That(actual.Meta.HasValidatedAt, Is.EqualTo(expected.Meta.HasValidatedAt));
            Assert.That(actual.Nodes.ToArray().Select(n =>
                    (n.Id, n.X, n.Y, n.QueueCapacity, n.HasQueueCapacity)),
                Is.EqualTo(expected.Nodes.ToArray().Select(n =>
                    (n.Id, n.X, n.Y, n.QueueCapacity, n.HasQueueCapacity))));
            Assert.That(actual.Edges.ToArray().Select(e =>
                    (e.Id, e.From, e.To, e.TravelTicks, e.OneWay,
                        e.Reversible, e.Tunnel, e.Hold)),
                Is.EqualTo(expected.Edges.ToArray().Select(e =>
                    (e.Id, e.From, e.To, e.TravelTicks, e.OneWay,
                        e.Reversible, e.Tunnel, e.Hold))));
            Assert.That(actual.Sources.ToArray().Select(s =>
                    (s.NodeId, string.Join(",", s.AllowedColors.ToArray()))),
                Is.EqualTo(expected.Sources.ToArray().Select(s =>
                    (s.NodeId, string.Join(",", s.AllowedColors.ToArray())))));
            Assert.That(actual.Stations.ToArray().Select(s =>
                    (s.NodeId, string.Join(",", s.Accepts.ToArray()), s.Capacity, s.Shape)),
                Is.EqualTo(expected.Stations.ToArray().Select(s =>
                    (s.NodeId, string.Join(",", s.Accepts.ToArray()), s.Capacity, s.Shape))));
            Assert.That(actual.Switches.ToArray().Select(s =>
                    (s.Id, s.NodeId, string.Join(",", s.Routes.ToArray()),
                        s.InitialRoute, s.CooldownTicks)),
                Is.EqualTo(expected.Switches.ToArray().Select(s =>
                    (s.Id, s.NodeId, string.Join(",", s.Routes.ToArray()),
                        s.InitialRoute, s.CooldownTicks))));
            Assert.That(actual.Gates.ToArray().Select(g =>
                    (g.EdgeId, string.Join(";", g.OpenWindows.ToArray()
                        .Select(w => w.StartTick + "," + w.EndTick)), g.PreviewTicks)),
                Is.EqualTo(expected.Gates.ToArray().Select(g =>
                    (g.EdgeId, string.Join(";", g.OpenWindows.ToArray()
                        .Select(w => w.StartTick + "," + w.EndTick)), g.PreviewTicks))));
            Assert.That(actual.Waves.ToArray().Select(w =>
                    (w.Tick, w.SourceNode, w.Color, w.Count, w.SpacingTicks,
                        w.Express, w.Shape, w.Stray)),
                Is.EqualTo(expected.Waves.ToArray().Select(w =>
                    (w.Tick, w.SourceNode, w.Color, w.Count, w.SpacingTicks,
                        w.Express, w.Shape, w.Stray))));
            Assert.That(actual.Tags.ToArray(), Is.EqualTo(expected.Tags.ToArray()));
            Assert.That((actual.Win.Deliveries, actual.Win.TimeLimitTicks,
                    actual.Win.PerfectMaxSwitches, actual.Win.Stars.Two, actual.Win.Stars.Three),
                Is.EqualTo((expected.Win.Deliveries, expected.Win.TimeLimitTicks,
                    expected.Win.PerfectMaxSwitches, expected.Win.Stars.Two,
                    expected.Win.Stars.Three)));
            Assert.That((actual.Economy.BaseTickets, actual.Economy.PerfectBonus),
                Is.EqualTo((expected.Economy.BaseTickets, expected.Economy.PerfectBonus)));
        }
    }
}
