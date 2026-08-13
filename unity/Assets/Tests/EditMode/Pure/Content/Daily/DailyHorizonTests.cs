using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using CatMetro.Content;
using CatMetro.Content.Daily;
using CatMetro.Content.Validation;

namespace CatMetro.Tests.Daily
{
#if CATMETRO_DAILY_LONG_TESTS
    public sealed class DailyLongHorizonTests
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
        public void ShippedFallbackNinetyDateHorizon_ExhaustsThenAdmitsFreshDeterministicBoards()
        {
            var config = DFixtures.ShippedPipelineConfig();
            Assert.That(config.PrevalidationDays, Is.EqualTo(ExpectedHorizon.Length));
            Assert.That(config.SaltMaxK, Is.EqualTo(10));
            Assert.That(DateKeys.Enumerate("2026-08-24", config.PrevalidationDays),
                Is.EqualTo(ExpectedHorizon), "the fallback proof uses the exact shipped horizon");

            var firstFactory = new RejectingCandidateShippedFallbackFactory();
            var secondFactory = new RejectingCandidateShippedFallbackFactory();
            var first = RunRuntime(ExpectedHorizon, config, firstFactory);
            var second = RunRuntime(ExpectedHorizon, config, secondFactory);

            AssertFallbackCalls(firstFactory, config);
            AssertFallbackCalls(secondFactory, config);
            Assert.That(first.Generator, Is.EqualTo("CM-DAILY-"));
            Assert.That(second.Generator, Is.EqualTo("CM-DAILY-"));
            Assert.That(first.ExitFailure, Is.False);
            Assert.That(second.ExitFailure, Is.False);
            Assert.That(first.Records.Count, Is.EqualTo(ExpectedHorizon.Length));
            Assert.That(second.Records.Count, Is.EqualTo(ExpectedHorizon.Length));

            for (int i = 0; i < ExpectedHorizon.Length; i++)
            {
                DailyDateRecord firstRecord = first.Records[i];
                DailyDateRecord secondRecord = second.Records[i];
                AssertDeterministicAdmission(firstRecord, secondRecord);
                Assert.That(firstRecord.UsedFallback, Is.True, firstRecord.DateKey);
                Assert.That(secondRecord.UsedFallback, Is.True, secondRecord.DateKey);
                Assert.That(firstRecord.K, Is.Zero, firstRecord.DateKey);
                Assert.That(firstRecord.Board, Is.SameAs(firstFactory.FallbackBoards[i]));
                Assert.That(secondRecord.Board, Is.SameAs(secondFactory.FallbackBoards[i]));
                AssertFreshBoardGraph(firstRecord.Board, secondRecord.Board, firstRecord.DateKey);
            }

            TestContext.WriteLine("DAILY_FALLBACK_HORIZON dates=" + first.Records.Count
                + " attemptsPerDate=" + (config.SaltMaxK + 1) + " fallbacks="
                + firstFactory.FallbackCalls.Count);
        }

        private static DailyRunReport RunRuntime(
            IReadOnlyList<string> dateKeys, DailyPipelineConfig config,
            IBoardFactory factory = null)
        {
            var request = DFixtures.RuntimeRequest(
                dateKeys, factory ?? new DailyBoardFactory(), config);
            Assert.That(request.SeedScheme, Is.SameAs(DailyLineSeedScheme.Instance));
            return DFixtures.Run(request);
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

        private static void AssertFallbackCalls(
            RejectingCandidateShippedFallbackFactory factory, DailyPipelineConfig config)
        {
            var expectedAttempts = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            Assert.That(config.SaltMaxK, Is.EqualTo(expectedAttempts.Last()));
            Assert.That(factory.CandidateCalls.Count,
                Is.EqualTo(ExpectedHorizon.Length * expectedAttempts.Length));
            Assert.That(factory.FallbackCalls.Count, Is.EqualTo(ExpectedHorizon.Length));
            Assert.That(factory.FallbackCalls.Select(call => call.dateKey),
                Is.EqualTo(ExpectedHorizon), "exactly one fallback follows each date's attempts");

            for (int i = 0; i < ExpectedHorizon.Length; i++)
            {
                var attempts = factory.CandidateCalls
                    .Skip(i * expectedAttempts.Length)
                    .Take(expectedAttempts.Length)
                    .ToArray();
                Assert.That(attempts.Select(call => call.dateKey),
                    Is.All.EqualTo(ExpectedHorizon[i]), ExpectedHorizon[i]);
                Assert.That(attempts.Select(call => call.k),
                    Is.EqualTo(expectedAttempts), ExpectedHorizon[i]);
                Assert.That(attempts.Select(call => call.seed),
                    Is.All.EqualTo(attempts[0].seed), ExpectedHorizon[i]);
                Assert.That(factory.FallbackCalls[i].seed,
                    Is.EqualTo(attempts[0].seed), ExpectedHorizon[i]);
            }
        }

        private static void AssertFreshBoardGraph(LevelDto first, LevelDto second, string dateKey)
        {
            Assert.That(second, Is.Not.SameAs(first), dateKey + " DTO");
            Assert.That(second.Meta, Is.Not.SameAs(first.Meta), dateKey + " meta");
            Assert.That(second.Meta.Mechanics.Equals(first.Meta.Mechanics), Is.False,
                dateKey + " mechanics array");
            Assert.That(second.Nodes.Equals(first.Nodes), Is.False, dateKey + " nodes array");
            Assert.That(second.Edges.Equals(first.Edges), Is.False, dateKey + " edges array");
            Assert.That(second.Sources.Equals(first.Sources), Is.False, dateKey + " sources array");
            Assert.That(second.Stations.Equals(first.Stations), Is.False, dateKey + " stations array");
            Assert.That(second.Switches.Equals(first.Switches), Is.False, dateKey + " switches array");
            Assert.That(second.Waves.Equals(first.Waves), Is.False, dateKey + " waves array");
            Assert.That(second.Sources.Span[0].AllowedColors.Equals(
                first.Sources.Span[0].AllowedColors), Is.False, dateKey + " source colors");
            Assert.That(second.Stations.Span[0].Accepts.Equals(
                first.Stations.Span[0].Accepts), Is.False, dateKey + " station colors");
            Assert.That(second.Switches.Span[0].Routes.Equals(
                first.Switches.Span[0].Routes), Is.False, dateKey + " routes");
            Assert.That(second.Win, Is.Not.SameAs(first.Win), dateKey + " win");
            Assert.That(second.Win.Stars, Is.Not.SameAs(first.Win.Stars), dateKey + " stars");
            Assert.That(second.Economy, Is.Not.SameAs(first.Economy), dateKey + " economy");
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

        // Candidate construction intentionally returns null so all configured attempts reject
        // without solver work. The only admitted DTO comes from the shipped fallback builder and
        // still travels through DailyPipeline's production CorpusValidator admission path.
        private sealed class RejectingCandidateShippedFallbackFactory
            : IBoardFactory, IDailyFallbackBoardFactory
        {
            private readonly DailyBoardFactory _shipped = new DailyBoardFactory();

            public readonly List<(uint seed, string dateKey, int k)> CandidateCalls =
                new List<(uint, string, int)>();
            public readonly List<(uint seed, string dateKey)> FallbackCalls =
                new List<(uint, string)>();
            public readonly List<LevelDto> FallbackBoards = new List<LevelDto>();

            public LevelDto Build(uint seed, string dateKey, int k)
            {
                CandidateCalls.Add((seed, dateKey, k));
                return null;
            }

            public LevelDto BuildFallback(uint seed, string dateKey)
            {
                FallbackCalls.Add((seed, dateKey));
                LevelDto board = _shipped.BuildFallback(seed, dateKey);
                FallbackBoards.Add(board);
                return board;
            }
        }
    }
#endif

    public sealed class DailyHorizonTests
    {
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

            var record = RunRuntime(new[] { actualDateKey }).Records.Single();
            AssertAdmission(record);
            Assert.That(Encoding.UTF8.GetBytes(record.BoardJson), Is.Not.Empty);
        }

        [Test]
        [Parallelizable(ParallelScope.None)]
        public void EqualOffsetInstants_HaveIdenticalUtcSeedAndBoardBytesAcrossCultures()
        {
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            CultureInfo originalUiCulture = Thread.CurrentThread.CurrentUICulture;
            try
            {
                var first = ResolveUnderCulture(
                    "2026-08-24T17:00:00-07:00", "fr-FR");
                var second = ResolveUnderCulture(
                    "2026-08-25T09:30:00+09:30", "ar-SA");

                Assert.That(first.UnixSeconds, Is.EqualTo(1787616000L));
                Assert.That(second.UnixSeconds, Is.EqualTo(1787616000L));
                Assert.That(first.DateKey, Is.EqualTo("2026-08-25"));
                Assert.That(second.DateKey, Is.EqualTo("2026-08-25"));
                Assert.That(first.Seed, Is.EqualTo(3466517912u));
                Assert.That(second.Seed, Is.EqualTo(3466517912u));
                Assert.That(second.BoardBytes, Is.EqualTo(first.BoardBytes));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
                Thread.CurrentThread.CurrentUICulture = originalUiCulture;
            }
        }

        private static InstantResolution ResolveUnderCulture(string label, string cultureName)
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            var instant = DateTimeOffset.ParseExact(label, "yyyy-MM-dd'T'HH:mm:sszzz",
                CultureInfo.InvariantCulture, DateTimeStyles.None);
            long unixSeconds = instant.ToUnixTimeSeconds();
            string dateKey = DailyLineSeed.DateKeyFromUnixSeconds(unixSeconds);
            uint seed = DailyLineSeed.DeriveFromUnixSeconds(unixSeconds);
            var record = RunRuntime(new[] { dateKey }).Records.Single();
            AssertAdmission(record);
            return new InstantResolution(unixSeconds, dateKey, seed,
                Encoding.UTF8.GetBytes(record.BoardJson));
        }

        private static DailyRunReport RunRuntime(IReadOnlyList<string> dateKeys) =>
            DFixtures.Run(DFixtures.RuntimeRequest(
                dateKeys, new DailyBoardFactory(), DFixtures.ShippedPipelineConfig()));

        private static void AssertBoundaryRun(string start, string[] expected)
        {
            Assert.That(DateKeys.Enumerate(start, expected.Length), Is.EqualTo(expected));
            var report = RunRuntime(expected);
            Assert.That(report.ExitFailure, Is.False);
            Assert.That(report.Records.Select(record => record.DateKey), Is.EqualTo(expected));
            foreach (var record in report.Records) AssertAdmission(record);
        }

        private static void AssertAdmission(DailyDateRecord record)
        {
            Assert.That(record.Blocks, Is.False, record.DateKey + ": " + record.Detail);
            Assert.That(record.Board, Is.Not.Null, record.DateKey);
            Assert.That(record.StageVerdicts.Count, Is.EqualTo(11), record.DateKey);
            Assert.That(record.StageVerdicts.Select(verdict => (int)verdict.Stage),
                Is.EqualTo(Enumerable.Range(1, 11)), record.DateKey);
            Assert.That(record.StageVerdicts.Single(verdict => verdict.Stage == Stage.Solver).Code,
                Is.EqualTo(StageVerdictCode.Pass), record.DateKey);
            Assert.That(record.StageVerdicts.Single(
                verdict => verdict.Stage == Stage.BrittlenessAccessibility).Blocks,
                Is.False, record.DateKey);
        }

        private sealed class InstantResolution
        {
            public readonly long UnixSeconds;
            public readonly string DateKey;
            public readonly uint Seed;
            public readonly byte[] BoardBytes;

            public InstantResolution(long unixSeconds, string dateKey, uint seed, byte[] boardBytes)
            {
                UnixSeconds = unixSeconds;
                DateKey = dateKey;
                Seed = seed;
                BoardBytes = boardBytes;
            }
        }
    }
}
