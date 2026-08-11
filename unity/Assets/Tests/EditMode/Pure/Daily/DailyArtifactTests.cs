using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Content;
using CatMetro.Content.Daily;

namespace CatMetro.Tests.Daily
{
    // CM-C6 criteria 6 + 7 (library half): the artifact is serialised in-memory with one record
    // per date {dateKey, k, seed, verdict, stageVerdicts, solverCompletionTicks}, the seed lines
    // are single-sourced and anchored, and two identical runs render byte-identical text. The
    // wrapper (tests/daily/daily-pipeline.test.sh) proves the file-level half through the host.
    public sealed class DailyArtifactTests
    {
        private static DailyRunReport ThreeDateReport()
        {
            var dates = DateKeys.Enumerate("2026-08-24", 3);
            return DFixtures.Run(DFixtures.Request(
                dates, new DFixtures.FixedFactory(DFixtures.L001Dto())));
        }

        // Criterion 6a: the JSON record shape, for every date.
        [Test]
        public void ToJson_CarriesTheRecordShapePerDate()
        {
            var root = JObject.Parse(ThreeDateReport().ToJson());
            var dates = (JArray)root["dates"];
            Assert.That(dates, Is.Not.Null);
            Assert.That(dates.Count, Is.EqualTo(3));
            foreach (var t in dates)
            {
                var o = (JObject)t;
                foreach (var key in new[]
                    { "dateKey", "k", "seed", "verdict", "stageVerdicts", "solverCompletionTicks" })
                    Assert.That(o[key], Is.Not.Null, $"record key '{key}' missing");
                var stages = (JArray)o["stageVerdicts"];
                Assert.That(stages.Count, Is.GreaterThanOrEqualTo(11));
                foreach (var s in stages)
                    foreach (var key in new[] { "stage", "code", "detail", "value", "blocks" })
                        Assert.That(((JObject)s)[key], Is.Not.Null, $"stage key '{key}' missing");
            }
        }

        // Criterion 7 (library half): identical inputs render byte-identical artifacts.
        [Test]
        public void ToJson_ByteIdenticalAcrossTwoRuns()
        {
            Assert.That(ThreeDateReport().ToJson(), Is.EqualTo(ThreeDateReport().ToJson()));
        }

        [Test]
        public void ToJson_RuntimeSchemeStartsWithItsGeneratorLabel()
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
                Does.StartWith("{\n  \"generator\": \"CM-DAILY-\""));
            Assert.That(ThreeDateReport().ToJson(),
                Does.StartWith("{\n  \"generator\": \"CM-DAILY-1\""),
                "the historical artifact prefix must remain byte-identical");
        }

        // Criterion 6b (format half): one anchored line per date, nothing else DAILY_SEED-shaped.
        [Test]
        public void SeedLines_OnePerDate_AnchoredFormat()
        {
            var report = ThreeDateReport();
            var lines = report.SeedLines();
            Assert.That(lines.Count, Is.EqualTo(3));
            var rx = new Regex(@"^DAILY_SEED \d{4}-\d{2}-\d{2} \d+ \d+$");
            foreach (var line in lines)
                Assert.That(rx.IsMatch(line), Is.True, $"malformed seed line: {line}");
            Assert.That(lines.Distinct().Count(), Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                var rec = report.Records[i];
                Assert.That(lines[i],
                    Is.EqualTo($"DAILY_SEED {rec.DateKey} {rec.K} {rec.Seed}"));
            }
        }

        // The serialiser bridge (criterion 5's mechanism): a corpus DTO round-trips through
        // DailyBoardJson -> LevelImporter to the identical content.
        [Test]
        public void RoundTrip_L001_ReimportsToTheSameContent()
        {
            var dto = DFixtures.L001Dto();
            var text = DailyBoardJson.Serialize(dto);
            var back = LevelImporter.Import(System.Text.Encoding.UTF8.GetBytes(text));
            Assert.That(back.Ok, Is.True, $"round-trip must import: {back.Error}");
            var b = back.Value.Dto;

            Assert.That(b.Id, Is.EqualTo(dto.Id));
            Assert.That(b.Seed, Is.EqualTo(dto.Seed));
            Assert.That(b.SchemaVersion, Is.EqualTo(dto.SchemaVersion));
            Assert.That(b.Meta.Band, Is.EqualTo(dto.Meta.Band));
            Assert.That(b.Meta.NewMechanic, Is.EqualTo(dto.Meta.NewMechanic));
            Assert.That(b.Meta.MinActionWindowTicks, Is.EqualTo(dto.Meta.MinActionWindowTicks));
            Assert.That(b.Meta.HasValidatedAt, Is.EqualTo(dto.Meta.HasValidatedAt));
            Assert.That(b.Nodes.Length, Is.EqualTo(dto.Nodes.Length));
            Assert.That(b.Edges.Length, Is.EqualTo(dto.Edges.Length));
            Assert.That(b.Waves.Length, Is.EqualTo(dto.Waves.Length));
            Assert.That(b.Win.TimeLimitTicks, Is.EqualTo(dto.Win.TimeLimitTicks));
            Assert.That(b.Win.Deliveries, Is.EqualTo(dto.Win.Deliveries));
            Assert.That(b.Economy.BaseTickets, Is.EqualTo(dto.Economy.BaseTickets));
        }

        // Absent-key conventions survive: L001 authors no queueCapacity and no validatedAt, so
        // the serialised text must not invent either key (AMD-09: tooling deletes the key rather
        // than write null).
        [Test]
        public void RoundTrip_PreservesAbsentKeys()
        {
            var text = DailyBoardJson.Serialize(DFixtures.L001Dto());
            Assert.That(text, Does.Not.Contain("queueCapacity"));
            Assert.That(text, Does.Not.Contain("validatedAt"));
        }

        // solverCompletionTicks is the CM-C5 stage-4 figure (CompletionTicks — the sim-tick the
        // final delivery lands on, RunToEnd().Tick - 1 by the CM-C1 convention).
        [Test]
        public void SolverCompletionTicks_PositiveForASolvedBoard()
        {
            var rec = ThreeDateReport().Records[0];
            Assert.That(rec.SolverCompletionTicks, Is.GreaterThan(0));
        }
    }
}
