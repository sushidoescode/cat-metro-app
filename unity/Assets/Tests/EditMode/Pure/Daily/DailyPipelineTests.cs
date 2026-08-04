using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using CatMetro.Content.Daily;
using CatMetro.Content.Validation;

namespace CatMetro.Tests.Daily
{
    // CM-C6 criteria 2, 4, 5, 8: the date list is an input, the salt loop is bounded and
    // deterministic, every candidate runs CM-C5's real stages with CM-C5's blocking semantics,
    // and the whole pipeline is exercised through stub factories only (Q-S).
    public sealed class DailyPipelineTests
    {
        // Criterion 2: the pipeline signature takes the date list. Pinned by reflection so a
        // refactor to "the pipeline computes its own dates" cannot land silently.
        [Test]
        public void Request_DateKeys_IsAReadOnlyStringList()
        {
            var f = typeof(DailyRunRequest).GetField("DateKeys");
            Assert.That(f, Is.Not.Null);
            Assert.That(f.FieldType, Is.EqualTo(typeof(IReadOnlyList<string>)));
            Assert.That(f.IsInitOnly, Is.True);
        }

        // Criterion 4a: a board failing a blocking stage at k=0 and passing at k=1 resolves k=1.
        [Test]
        public void SaltLoop_FailAtK0_PassAtK1_ResolvesK1()
        {
            var factory = new DFixtures.KeyedFactory(passAtK: 1);
            var report = DFixtures.Run(DFixtures.Request(new[] { "2026-08-24" }, factory));
            var rec = report.Records.Single();

            Assert.That(rec.K, Is.EqualTo(1));
            Assert.That(rec.Verdict, Is.EqualTo("Pass"));
            Assert.That(rec.Blocks, Is.False);
            Assert.That(rec.Seed, Is.EqualTo(DailySeed.Derive("2026-08-24", 1)),
                "the reported seed must be the seed at the RESOLVED salt");
            Assert.That(factory.Builds, Is.EqualTo(2), "exactly k=0 then k=1");
            Assert.That(report.ExitFailure, Is.False);
        }

        // Criterion 4b: SALT_MAX_K exhaustion is a reported failure, not an infinite loop.
        [Test]
        public void SaltLoop_Exhaustion_IsReportedNotInfinite()
        {
            var factory = new DFixtures.KeyedFactory(passAtK: 99);
            var report = DFixtures.Run(DFixtures.Request(
                new[] { "2026-08-24" }, factory, DFixtures.TinyConfig(saltMaxK: 2)));
            var rec = report.Records.Single();

            Assert.That(factory.Builds, Is.EqualTo(3), "attempts k=0,1,2 then stop");
            Assert.That(rec.Verdict, Is.EqualTo("Fail"));
            Assert.That(rec.Blocks, Is.True);
            Assert.That(rec.Detail, Does.Contain("SALT_MAX_K"));
            Assert.That(report.ExitFailure, Is.True);
        }

        // Criterion 4c: two runs over the same date list produce the identical k for every date.
        [Test]
        public void SaltLoop_KIdenticalAcrossTwoRuns()
        {
            var dates = DateKeys.Enumerate("2026-08-24", 3);
            var r1 = DFixtures.Run(DFixtures.Request(dates, new DFixtures.KeyedFactory(passAtK: 1)));
            var r2 = DFixtures.Run(DFixtures.Request(dates, new DFixtures.KeyedFactory(passAtK: 1)));
            Assert.That(r1.Records.Select(r => r.K), Is.EqualTo(r2.Records.Select(r => r.K)));
            Assert.That(r1.Records.Select(r => r.Seed), Is.EqualTo(r2.Records.Select(r => r.Seed)));
        }

        // Criterion 5a+5c: a date whose board fails a blocking stage fails the run, and the
        // printed reason names the stage (here stage 4 Solver — the unsolvable stub board).
        [Test]
        public void BlockingFail_NamesTheStageAndReason()
        {
            var factory = new DFixtures.KeyedFactory(passAtK: 99);
            var report = DFixtures.Run(DFixtures.Request(
                new[] { "2026-08-24" }, factory, DFixtures.TinyConfig(saltMaxK: 0)));
            var rec = report.Records.Single();

            Assert.That(report.ExitFailure, Is.True);
            Assert.That(rec.Detail, Does.Contain("Solver"),
                "the failure detail must name the blocking stage");
            Assert.That(rec.StageVerdicts.Any(v => v.Blocks && v.Stage == Stage.Solver), Is.True);
        }

        // Criterion 5b: non-blocking verdicts (UNCONFIGURED rows under the bare shipped config,
        // PENDING playtest, SKIPPED novelty for non-campaign) print and do NOT fail — the same
        // semantics CM-C5 criterion 13 establishes, so the two jobs cannot disagree.
        [Test]
        public void NonBlockingVerdicts_DoNotFail()
        {
            var factory = new DFixtures.FixedFactory(DFixtures.L001Dto());
            var report = DFixtures.Run(DFixtures.Request(new[] { "2026-08-24" }, factory));
            var rec = report.Records.Single();

            Assert.That(report.ExitFailure, Is.False);
            Assert.That(rec.StageVerdicts.Any(v =>
                v.Code == StageVerdictCode.Unconfigured && !v.Blocks), Is.True,
                "the bare config must surface at least one UNCONFIGURED row, non-blocking");
            Assert.That(rec.StageVerdicts.Any(v =>
                v.Code == StageVerdictCode.Skipped && v.Stage == Stage.NoveltyCheck), Is.True,
                "a daily board is non-campaign: novelty prints SKIPPED");
        }

        // Criterion 8a: the pipeline is FULLY exercised through a stub — every date runs all 11
        // CM-C5 stages, and the factory receives exactly the derived seed for (dateKey, k).
        [Test]
        public void Pipeline_FullyExercisedThroughStub()
        {
            var dates = DateKeys.Enumerate("2026-08-24", 3);
            var factory = new DFixtures.FixedFactory(DFixtures.L001Dto());
            var report = DFixtures.Run(DFixtures.Request(dates, factory));

            Assert.That(factory.Calls.Count, Is.EqualTo(3));
            foreach (var (seed, dateKey, k) in factory.Calls)
            {
                Assert.That(k, Is.EqualTo(0));
                Assert.That(seed, Is.EqualTo(DailySeed.Derive(dateKey, 0)),
                    "the factory must receive the derived seed, not a copy of its own");
            }
            foreach (var rec in report.Records)
                Assert.That(rec.StageVerdicts.Count, Is.EqualTo(11),
                    "all 11 CM-C5 stages must appear per date");
        }

        // A-C6-4: a malformed date key rejects the RUN with a typed failure naming the key —
        // deriving a seed from a malformed key would silently fork the shared board.
        [Test]
        public void MalformedDateKey_IsATypedRunFailure()
        {
            var r = DailyPipeline.Run(DFixtures.Request(
                new[] { "2026-8-4" }, new DFixtures.FixedFactory(DFixtures.L001Dto())));
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Error.ToString(), Does.Contain("2026-8-4"));
        }

        // Totality (the ADR-0008 MUST-3 posture): a detonating factory becomes a blocking record,
        // never an escaping exception — the host must always get its artifact.
        [Test]
        public void ThrowingFactory_BecomesABlockingRecord()
        {
            var report = DFixtures.Run(DFixtures.Request(
                new[] { "2026-08-24" }, new DFixtures.ThrowingFactory(),
                DFixtures.TinyConfig(saltMaxK: 1)));
            var rec = report.Records.Single();
            Assert.That(rec.Blocks, Is.True);
            Assert.That(rec.Detail, Does.Contain("factory"));
            Assert.That(report.ExitFailure, Is.True);
        }
    }
}
