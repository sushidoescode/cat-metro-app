using System.Linq;
using NUnit.Framework;
using CatMetro.Content.Daily;
using CatMetro.Content.Validation;
using CatMetro.Domain.Solver;

namespace CatMetro.Tests.Daily
{
    public sealed class DailyRuntimePipelineTests
    {
        [Test]
        public void DailyDateRecord_LegacyConstructorRemainsAvailable()
        {
            var constructor = typeof(DailyDateRecord).GetConstructor(new[]
            {
                typeof(string), typeof(int), typeof(uint), typeof(string), typeof(string),
                typeof(System.Collections.Generic.IReadOnlyList<StageVerdict>),
                typeof(RampVerdict), typeof(int), typeof(bool),
            });

            Assert.That(constructor, Is.Not.Null,
                "the additive runtime record fields must not remove CM-C6's public constructor");
        }

        [Test]
        public void RuntimeCandidate_IsRetainedOnlyAfterRealElevenStageAdmission()
        {
            var record = DFixtures.Run(DFixtures.RuntimeRequest(
                new[] { "2026-08-24" }, new DailyBoardFactory())).Records.Single();

            Assert.That(record.StageVerdicts.Select(v => (int)v.Stage),
                Is.EqualTo(Enumerable.Range(1, 11)));
            Assert.That(record.StageVerdicts.Any(v => v.Blocks), Is.False);
            Assert.That(record.StageVerdicts.Single(v => v.Stage == Stage.Solver).Code,
                Is.EqualTo(StageVerdictCode.Pass));
            Assert.That(record.SolverCompletionTicks, Is.GreaterThanOrEqualTo(0));
            Assert.That(record.Blocks, Is.False);
            Assert.That(record.Board, Is.Not.Null);
            Assert.That(record.BoardJson, Is.EqualTo(DailyBoardJson.Serialize(record.Board)));
            Assert.That(record.UsedFallback, Is.False);
        }

        [Test]
        public void Exhaustion_AttemptsInclusiveBoundThenAdmitsExactlyOneFallback()
        {
            var bad = DFixtures.UnsolvableDto();
            var fallback = DFixtures.L001Dto();
            var firstFactory = new DFixtures.ExhaustingFallbackFactory(bad, fallback);
            var secondFactory = new DFixtures.ExhaustingFallbackFactory(bad, fallback);

            var first = DFixtures.Run(DFixtures.RuntimeRequest(
                new[] { "2026-08-24" }, firstFactory,
                DFixtures.TinyConfig(saltMaxK: 2))).Records.Single();
            var second = DFixtures.Run(DFixtures.RuntimeRequest(
                new[] { "2026-08-24" }, secondFactory,
                DFixtures.TinyConfig(saltMaxK: 2))).Records.Single();

            Assert.That(firstFactory.CandidateCalls.Select(c => c.k),
                Is.EqualTo(new[] { 0, 1, 2 }), "k=SaltMaxK remains an inclusive candidate attempt");
            Assert.That(firstFactory.CandidateCalls.Select(c => c.seed),
                Is.All.EqualTo(1449106418u));
            Assert.That(firstFactory.CandidateCalls.Select(c => c.dateKey),
                Is.All.EqualTo("2026-08-24"));
            Assert.That(firstFactory.FallbackCalls,
                Is.EqualTo(new[] { (1449106418u, "2026-08-24") }));
            Assert.That(secondFactory.CandidateCalls.Select(c => c.k),
                Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(secondFactory.FallbackCalls.Count, Is.EqualTo(1));

            Assert.That(first.UsedFallback, Is.True);
            Assert.That(first.Blocks, Is.False);
            Assert.That(first.Verdict, Is.EqualTo("Pass"));
            Assert.That(first.StageVerdicts.Select(v => (int)v.Stage),
                Is.EqualTo(Enumerable.Range(1, 11)));
            Assert.That(first.StageVerdicts.Single(v => v.Stage == Stage.Solver).Code,
                Is.EqualTo(StageVerdictCode.Pass));
            Assert.That(first.Board, Is.SameAs(fallback));
            Assert.That(first.BoardJson, Is.EqualTo(DailyBoardJson.Serialize(fallback)));
            Assert.That(first.BoardJson, Is.Not.EqualTo(DailyBoardJson.Serialize(bad)),
                "no rejected candidate serialization may escape in the resolved record");

            Assert.That(second.K, Is.EqualTo(first.K));
            Assert.That(second.Seed, Is.EqualTo(first.Seed));
            Assert.That(second.BoardJson, Is.EqualTo(first.BoardJson));
            Assert.That(second.StageVerdicts.Select(v => (v.Stage, v.Code, v.Detail, v.Value, v.Blocks)),
                Is.EqualTo(first.StageVerdicts.Select(v =>
                    (v.Stage, v.Code, v.Detail, v.Value, v.Blocks))));
        }

        [Test]
        public void FallbackNotFound_BlocksLoudlyAndRetainsNoBoard()
        {
            var factory = new DFixtures.ExhaustingFallbackFactory(
                DFixtures.TrivialWinDto(), DFixtures.L001Dto());

            var report = DFixtures.Run(DFixtures.RuntimeRequest(
                new[] { "2026-08-24" }, factory,
                DFixtures.TinyConfig(saltMaxK: 0),
                maxNodesExpanded: 0));
            var record = report.Records.Single();

            Assert.That(factory.CandidateCalls.Select(c => c.k), Is.EqualTo(new[] { 0 }));
            Assert.That(factory.FallbackCalls.Count, Is.EqualTo(1));
            Assert.That(record.StageVerdicts.Single(v => v.Stage == Stage.Solver).Code,
                Is.EqualTo(StageVerdictCode.Warn),
                "the real validator reports budget NotFound as non-blocking");
            Assert.That(record.Detail, Does.Contain("fallback").IgnoreCase);
            Assert.That(record.Detail, Does.Contain("NotFound"));
            Assert.That(record.Verdict, Is.EqualTo("Fail"));
            Assert.That(record.Blocks, Is.True);
            Assert.That(report.ExitFailure, Is.True);
            Assert.That(record.Board, Is.Null);
            Assert.That(record.BoardJson, Is.Null);
            Assert.That(record.UsedFallback, Is.False);
            Assert.That(report.SeedLines(), Is.Empty);
        }

        [Test]
        public void FallbackSerializationFailure_BecomesBlockingRecord()
        {
            var factory = new DFixtures.ExhaustingFallbackFactory(
                DFixtures.UnsolvableDto(), DFixtures.NullMetaDto());

            DailyRunReport report = null;
            Assert.That(() => report = DFixtures.Run(DFixtures.RuntimeRequest(
                new[] { "2026-08-24" }, factory,
                DFixtures.TinyConfig(saltMaxK: 0))), Throws.Nothing);

            var record = report.Records.Single();
            Assert.That(factory.FallbackCalls.Count, Is.EqualTo(1));
            Assert.That(record.Detail, Does.Contain("fallback").IgnoreCase);
            Assert.That(record.Detail, Does.Contain("admission threw"));
            Assert.That(record.Blocks, Is.True);
            Assert.That(record.Board, Is.Null);
            Assert.That(record.BoardJson, Is.Null);
        }

        [Test]
        public void CandidateSerializationFailure_BecomesBlockingRecord()
        {
            DailyRunReport report = null;
            Assert.That(() => report = DFixtures.Run(DFixtures.Request(
                new[] { "2026-08-24" },
                new DFixtures.FixedFactory(DFixtures.NullMetaDto()),
                DFixtures.TinyConfig(saltMaxK: 0))), Throws.Nothing);

            var record = report.Records.Single();
            Assert.That(record.Detail, Does.Contain("admission threw"));
            Assert.That(record.Blocks, Is.True);
            Assert.That(record.Board, Is.Null);
            Assert.That(record.BoardJson, Is.Null);
        }

        [Test]
        public void HistoricalFallback_ReportsTheBaseSeedAtKZero()
        {
            var factory = new DFixtures.ExhaustingFallbackFactory(
                DFixtures.UnsolvableDto(), DFixtures.L001Dto());

            var record = DFixtures.Run(DFixtures.Request(
                new[] { "2026-08-24" }, factory,
                DFixtures.TinyConfig(saltMaxK: 2))).Records.Single();

            Assert.That(factory.CandidateCalls.Select(c => c.k),
                Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(factory.FallbackCalls.Single().seed,
                Is.EqualTo(DailySeed.Derive("2026-08-24", 0)));
            Assert.That(record.UsedFallback, Is.True);
            Assert.That(record.K, Is.EqualTo(0), "fallback uses the base variation, not a salt");
            Assert.That(record.Seed, Is.EqualTo(DailySeed.Derive("2026-08-24", record.K)));
        }
    }
}
