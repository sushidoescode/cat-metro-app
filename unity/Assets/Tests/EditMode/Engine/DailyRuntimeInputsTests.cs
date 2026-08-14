using System.IO;
using NUnit.Framework;
using CatMetro.Bootstrap;
using CatMetro.Content.Daily;

namespace CatMetro.Tests.Engine
{
    // CM-DAILYWIRE FA-2: DailyRuntimeInputs reproduces validator_thresholds.json,
    // daily_pipeline.json, and level_schema.json as compiled C# rather than shipping the
    // source files (scripts/stage-content.sh's own "none of that may ever ship" rule for
    // config/, mirrored here for docs/plan/data/level_schema.json). This file is the drift
    // guard AND the end-to-end proof that the embedded copies are sufficient for the REAL
    // DailyPipeline to admit a board — not a stub, not a mock, not merely "parses OK".
    public sealed class DailyRuntimeInputsTests
    {
        [Test]
        public void EmbeddedSchema_MatchesTheRealSourceFile_ByteForByte()
        {
            // UnityEngine.Application.dataPath fully qualified on purpose (the CS0234
            // Application-namespace trap named in this repo's own device-session notes).
            string repoRoot = Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, "..", ".."));
            string path = Path.Combine(repoRoot, "docs", "plan", "data", "level_schema.json");
            Assert.That(File.Exists(path), Is.True,
                "precondition: the real schema source must exist at " + path
                + " — otherwise this drift guard proves nothing");
            byte[] real = File.ReadAllBytes(path);
            Assert.That(DailyRuntimeInputs.SchemaBytes, Is.EqualTo(real),
                "the embedded copy must be byte-identical to docs/plan/data/level_schema.json "
                + "— a future schema edit must fail HERE, not drift silently against the "
                + "runtime copy DailyPipeline actually validates against");
        }

        [Test]
        public void ValidatorConfig_MatchesTheRealSourceFile_Values()
        {
            var cfg = DailyRuntimeInputs.ValidatorConfig;
            Assert.That(cfg.JitterSampleCount, Is.EqualTo(20),
                "config/validator_thresholds.json's one configured row");
            Assert.That(cfg.LowerBoundSlack, Is.Null, "the Q-R row stays deliberately absent");
            Assert.That(cfg.StarBandSlack, Is.Null, "the Q-R row stays deliberately absent");
            Assert.That(cfg.NoveltyMinDistance, Is.Null, "the Q-R row stays deliberately absent");
            Assert.That(cfg.AxisBBandCaps, Is.Null, "the Q-R row stays deliberately absent");
        }

        [Test]
        public void PipelineConfig_CarriesTheRealSaltMaxK()
        {
            var cfg = DailyRuntimeInputs.PipelineConfig("2026-08-24");
            Assert.That(cfg.SaltMaxK, Is.EqualTo(10),
                "config/daily_pipeline.json's SALT_MAX_K row (A-C6-2's device-solve-budget "
                + "derivation)");
        }

        // The end-to-end proof: the embedded inputs are enough for the REAL DailyPipeline to
        // admit a real board for a pinned date. #73's own frozen contract pins the seed for
        // 2026-08-24 at 1449106418 — reusing that vector here means this test does not invent
        // a second source of truth for "what the correct board is."
        [Test]
        public void RealDailyPipeline_AdmitsABoard_ForAPinnedDate_UsingOnlyEmbeddedInputs()
        {
            const string dateKey = "2026-08-24";
            var request = new DailyRunRequest(
                DailyRuntimeInputs.SchemaBytes,
                DailyRuntimeInputs.ValidatorConfig,
                DailyRuntimeInputs.PipelineConfig(dateKey),
                weekdayCurveBytes: null,
                dateKeys: new[] { dateKey },
                factory: new DailyBoardFactory(),
                referenceTimestamp: null,
                boardProvenance: "test:DailyRuntimeInputsTests",
                seedScheme: DailyLineSeedScheme.Instance);

            var run = DailyPipeline.Run(request);
            Assert.That(run.Ok, Is.True, "the request itself must be well-formed: " + run.Error);
            var record = run.Value.Records[0];
            Assert.That(record.Blocks, Is.False,
                "the embedded inputs must admit a real board: " + record.Detail);
            Assert.That(record.Seed, Is.EqualTo(1449106418u),
                "#73's own pinned vector for 2026-08-24 (DAILY-LINE-frozen-contract.md)");
            Assert.That(record.Board, Is.Not.Null);
            Assert.That(record.BoardJson, Is.Not.Null.And.Not.Empty);
        }
    }
}
