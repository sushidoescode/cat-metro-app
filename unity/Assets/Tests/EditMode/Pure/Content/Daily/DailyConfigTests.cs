using System.Linq;
using NUnit.Framework;
using CatMetro.Content.Daily;

namespace CatMetro.Tests.Daily
{
    // CM-C6 criterion 3: the horizon is a configured row, not a literal — the shipped file's row
    // equals 90 (a corpus number, PRD:727 / ADR-0009:35, A-C6-1), the pipeline processes exactly
    // that many dates, and the 30-date run is the smoke instance (the ADR-0006:224-227
    // QUEUE_MAX_EVENTS/500 shape). The value 90 appears in THIS test and the config file only —
    // the wrapper greps the Daily sources for a hard-coded copy.
    public sealed class DailyConfigTests
    {
        [Test]
        public void ShippedFile_HorizonRowEquals90() =>
            Assert.That(DFixtures.ShippedPipelineConfig().PrevalidationDays, Is.EqualTo(90));

        [Test]
        public void ShippedFile_SaltCeilingAndAnchorAreUsable()
        {
            var cfg = DFixtures.ShippedPipelineConfig();
            Assert.That(cfg.SaltMaxK, Is.GreaterThan(0));
            Assert.That(DateKeys.IsValid(cfg.AnchorDateKey), Is.True,
                "PIPELINE_ANCHOR_DATE must be a valid yyyy-MM-dd key (A-C6-7)");
        }

        // Criterion 3a: the criterion instance — the pipeline processes exactly the CONFIGURED
        // horizon, read from the shipped file, not a number this test invents.
        [Test]
        public void Pipeline_ProcessesExactlyTheConfiguredHorizon()
        {
            var cfg = DFixtures.ShippedPipelineConfig();
            var dates = DateKeys.Enumerate(cfg.AnchorDateKey, cfg.PrevalidationDays);
            var factory = new DFixtures.FixedFactory(DFixtures.L001Dto());
            var report = DFixtures.Run(DFixtures.Request(dates, factory, cfg));

            Assert.That(report.Records.Count, Is.EqualTo(cfg.PrevalidationDays));
            Assert.That(report.ExitFailure, Is.False);
            Assert.That(report.Records.All(r => r.K == 0), Is.True,
                "a corpus-green board must resolve k=0 on every date");
            Assert.That(report.Records.Select(r => r.Seed).Distinct().Count(),
                Is.EqualTo(cfg.PrevalidationDays), "every date must derive its own seed");
        }

        // Criterion 3b: the smoke instance — 30 dates, same pipeline, same semantics.
        [Test]
        public void Pipeline_30DateSmokeInstance()
        {
            var dates = DateKeys.Enumerate("2026-08-24", 30);
            var factory = new DFixtures.FixedFactory(DFixtures.L001Dto());
            var report = DFixtures.Run(DFixtures.Request(dates, factory));
            Assert.That(report.Records.Count, Is.EqualTo(30));
            Assert.That(report.ExitFailure, Is.False);
        }

        [Test]
        public void Parse_MissingHorizonRow_IsTypedFailure()
        {
            var r = DailyPipelineConfig.Parse(System.Text.Encoding.UTF8.GetBytes(
                "{\"SALT_MAX_K\": 5, \"PIPELINE_ANCHOR_DATE\": \"2026-08-24\"}"));
            Assert.That(r.Ok, Is.False);
        }

        [Test]
        public void Parse_MissingSaltCeiling_IsTypedFailure()
        {
            var r = DailyPipelineConfig.Parse(System.Text.Encoding.UTF8.GetBytes(
                "{\"DAILY_PREVALIDATION_DAYS\": 90, \"PIPELINE_ANCHOR_DATE\": \"2026-08-24\"}"));
            Assert.That(r.Ok, Is.False);
        }

        [Test]
        public void Parse_InvalidAnchor_IsTypedFailure()
        {
            var r = DailyPipelineConfig.Parse(System.Text.Encoding.UTF8.GetBytes(
                "{\"DAILY_PREVALIDATION_DAYS\": 90, \"SALT_MAX_K\": 5, \"PIPELINE_ANCHOR_DATE\": \"24/08/2026\"}"));
            Assert.That(r.Ok, Is.False);
        }

        [Test]
        public void Parse_GarbageBytes_IsTypedFailure()
        {
            var r = DailyPipelineConfig.Parse(System.Text.Encoding.UTF8.GetBytes("not json"));
            Assert.That(r.Ok, Is.False);
            Assert.That(DailyPipelineConfig.Parse(null).Ok, Is.False);
        }
    }
}
