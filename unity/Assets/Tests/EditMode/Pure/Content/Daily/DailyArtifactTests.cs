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
        public void RoundTrip_PreservesAbsentKeys()
        {
            string text = DailyBoardJson.Serialize(DFixtures.L001Dto());
            Assert.That(text, Does.Not.Contain("queueCapacity"));
            Assert.That(text, Does.Not.Contain("validatedAt"));
        }

        [Test]
        public void SolverCompletionTicks_PositiveForASolvedBoard()
        {
            Assert.That(ThreeDateReport().Records[0].SolverCompletionTicks, Is.GreaterThan(0));
        }
    }
}
