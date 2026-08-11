using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using CatMetro.Content.Daily;
using CatMetro.Content.Validation;

namespace CatMetro.Tests.Daily
{
    public sealed class DailyHorizonTests
    {
        private static readonly string[] ExpectedHorizon =
        {
            "2026-08-24",
            "2026-08-25",
            "2026-08-26",
            "2026-08-27",
            "2026-08-28",
            "2026-08-29",
            "2026-08-30",
            "2026-08-31",
            "2026-09-01",
            "2026-09-02",
            "2026-09-03",
            "2026-09-04",
            "2026-09-05",
            "2026-09-06",
            "2026-09-07",
            "2026-09-08",
            "2026-09-09",
            "2026-09-10",
            "2026-09-11",
            "2026-09-12",
            "2026-09-13",
            "2026-09-14",
            "2026-09-15",
            "2026-09-16",
            "2026-09-17",
            "2026-09-18",
            "2026-09-19",
            "2026-09-20",
            "2026-09-21",
            "2026-09-22",
            "2026-09-23",
            "2026-09-24",
            "2026-09-25",
            "2026-09-26",
            "2026-09-27",
            "2026-09-28",
            "2026-09-29",
            "2026-09-30",
            "2026-10-01",
            "2026-10-02",
            "2026-10-03",
            "2026-10-04",
            "2026-10-05",
            "2026-10-06",
            "2026-10-07",
            "2026-10-08",
            "2026-10-09",
            "2026-10-10",
            "2026-10-11",
            "2026-10-12",
            "2026-10-13",
            "2026-10-14",
            "2026-10-15",
            "2026-10-16",
            "2026-10-17",
            "2026-10-18",
            "2026-10-19",
            "2026-10-20",
            "2026-10-21",
            "2026-10-22",
            "2026-10-23",
            "2026-10-24",
            "2026-10-25",
            "2026-10-26",
            "2026-10-27",
            "2026-10-28",
            "2026-10-29",
            "2026-10-30",
            "2026-10-31",
            "2026-11-01",
            "2026-11-02",
            "2026-11-03",
            "2026-11-04",
            "2026-11-05",
            "2026-11-06",
            "2026-11-07",
            "2026-11-08",
            "2026-11-09",
            "2026-11-10",
            "2026-11-11",
            "2026-11-12",
            "2026-11-13",
            "2026-11-14",
            "2026-11-15",
            "2026-11-16",
            "2026-11-17",
            "2026-11-18",
            "2026-11-19",
            "2026-11-20",
            "2026-11-21",
        };

        [Test]
        public void ShippedNinetyDateHorizon_IsSolvedNonBlockingAndByteDeterministic()
        {
            var config = DFixtures.ShippedPipelineConfig();
            Assert.That(config.PrevalidationDays, Is.EqualTo(ExpectedHorizon.Length));
            Assert.That(DateKeys.Enumerate("2026-08-24", config.PrevalidationDays),
                Is.EqualTo(ExpectedHorizon), "the shipped civil-date order is a literal fixture");

            var first = RunRuntime(ExpectedHorizon, config);
            var second = RunRuntime(ExpectedHorizon, config);

            Assert.That(first.ExitFailure, Is.False);
            Assert.That(second.ExitFailure, Is.False);
            Assert.That(first.Records.Count, Is.EqualTo(90));
            Assert.That(second.Records.Count, Is.EqualTo(90));
            Assert.That(first.Records.Select(r => r.DateKey), Is.EqualTo(ExpectedHorizon));
            Assert.That(second.Records.Select(r => r.DateKey), Is.EqualTo(ExpectedHorizon));

            for (int i = 0; i < ExpectedHorizon.Length; i++)
                AssertDeterministicAdmission(first.Records[i], second.Records[i]);

            TestContext.WriteLine("DAILY_HORIZON distribution " + Distribution(first.Records));
        }

        [Test]
        public void YearAndLeapBoundaries_KeepLiteralOrderThroughTheRuntimePipeline()
        {
            AssertBoundaryRun("2026-12-31", new[]
            {
                "2026-12-31", "2027-01-01", "2027-01-02",
            });
            AssertBoundaryRun("2028-02-28", new[]
            {
                "2028-02-28", "2028-02-29", "2028-03-01",
            });
        }

        [TestCase(1787615999L, "2026-08-24T16:59:59-07:00", "2026-08-24")]
        [TestCase(1787616000L, "2026-08-24T17:00:00-07:00", "2026-08-25")]
        [TestCase(1787616000L, "2026-08-25T09:30:00+09:30", "2026-08-25")]
        [TestCase(1787616001L, "2026-08-25T09:30:01+09:30", "2026-08-25")]
        public void OffsetLabelledUnixEdges_ResolveByUtcInstant(
            long unixSeconds, string offsetLabel, string expectedDateKey)
        {
            string actualDateKey = DailyLineSeed.DateKeyFromUnixSeconds(unixSeconds);
            Assert.That(actualDateKey, Is.EqualTo(expectedDateKey), offsetLabel);
            Assert.That(DailyLineSeed.DeriveFromUnixSeconds(unixSeconds),
                Is.EqualTo(DailyLineSeed.Derive(expectedDateKey)), offsetLabel);

            var record = RunRuntime(new[] { actualDateKey }, DFixtures.ShippedPipelineConfig())
                .Records.Single();
            AssertAdmission(record);
            Assert.That(Encoding.UTF8.GetBytes(record.BoardJson), Is.Not.Empty);
        }

        private static DailyRunReport RunRuntime(
            IReadOnlyList<string> dateKeys, DailyPipelineConfig config) =>
            DFixtures.Run(DFixtures.RuntimeRequest(dateKeys, new DailyBoardFactory(), config));

        private static void AssertBoundaryRun(string start, string[] expected)
        {
            Assert.That(DateKeys.Enumerate(start, expected.Length), Is.EqualTo(expected));
            var report = RunRuntime(expected, DFixtures.ShippedPipelineConfig());
            Assert.That(report.ExitFailure, Is.False);
            Assert.That(report.Records.Select(r => r.DateKey), Is.EqualTo(expected));
            foreach (var record in report.Records) AssertAdmission(record);
        }

        private static void AssertDeterministicAdmission(
            DailyDateRecord first, DailyDateRecord second)
        {
            AssertAdmission(first);
            AssertAdmission(second);
            Assert.That(second.K, Is.EqualTo(first.K), first.DateKey);
            Assert.That(second.Seed, Is.EqualTo(first.Seed), first.DateKey);
            Assert.That(second.UsedFallback, Is.EqualTo(first.UsedFallback), first.DateKey);
            Assert.That(second.StageVerdicts.Select(StageTuple),
                Is.EqualTo(first.StageVerdicts.Select(StageTuple)), first.DateKey);
            Assert.That(Encoding.UTF8.GetBytes(second.BoardJson),
                Is.EqualTo(Encoding.UTF8.GetBytes(first.BoardJson)), first.DateKey);
        }

        private static void AssertAdmission(DailyDateRecord record)
        {
            Assert.That(record.Blocks, Is.False, record.DateKey + ": " + record.Detail);
            Assert.That(record.Verdict, Is.EqualTo("Pass"), record.DateKey);
            Assert.That(record.Board, Is.Not.Null, record.DateKey);
            Assert.That(record.BoardJson, Is.Not.Null, record.DateKey);
            Assert.That(record.StageVerdicts.Count, Is.EqualTo(11), record.DateKey);
            Assert.That(record.StageVerdicts.Select(v => (int)v.Stage),
                Is.EqualTo(Enumerable.Range(1, 11)), record.DateKey);
            Assert.That(record.StageVerdicts.Any(v => v.Blocks), Is.False, record.DateKey);
            Assert.That(record.StageVerdicts.Single(v => v.Stage == Stage.Solver).Code,
                Is.EqualTo(StageVerdictCode.Pass), record.DateKey);
            Assert.That(record.SolverCompletionTicks, Is.GreaterThanOrEqualTo(0), record.DateKey);
            Assert.That(record.StageVerdicts.Single(
                v => v.Stage == Stage.BrittlenessAccessibility).Blocks, Is.False, record.DateKey);
            Assert.That(record.Seed, Is.EqualTo(DailyLineSeed.Derive(record.DateKey)), record.DateKey);
        }

        private static (Stage stage, StageVerdictCode code, string detail, string value, bool blocks)
            StageTuple(StageVerdict verdict) =>
            (verdict.Stage, verdict.Code, verdict.Detail, verdict.Value, verdict.Blocks);

        private static string Distribution(IReadOnlyList<DailyDateRecord> records)
        {
            var candidateCounts = records.Where(r => !r.UsedFallback)
                .GroupBy(CandidateFamily)
                .OrderBy(g => g.Key)
                .Select(g => g.Key + "=" + g.Count());
            var kCounts = records.GroupBy(r => r.K).OrderBy(g => g.Key)
                .Select(g => "k" + g.Key + "=" + g.Count());
            return string.Join(",", candidateCounts) + ";fallback="
                + records.Count(r => r.UsedFallback) + ";" + string.Join(",", kCounts);
        }

        private static string CandidateFamily(DailyDateRecord record)
        {
            int waveCount = record.Board.Waves.Length;
            if (waveCount == 4) return "alternating";
            if (waveCount == 2) return "queued";
            if (waveCount == 3) return "cascade";
            return "unexpected-" + waveCount;
        }
    }
}
