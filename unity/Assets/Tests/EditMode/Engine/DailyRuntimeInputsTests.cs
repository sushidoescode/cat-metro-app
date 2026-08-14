using System.IO;
using NUnit.Framework;
using CatMetro.Bootstrap;
using CatMetro.Content.Daily;
using CatMetro.Content.Validation;

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

        // F1 (review fix round): the ORIGINAL version of this test never read
        // config/validator_thresholds.json at all — it compared the embedded constants to a
        // second set of hand-typed literals, so a real config edit could drift silently
        // against DailyRuntimeInputs without ever failing here. Rewritten to the schema
        // test's own pattern: read the REAL file, parse it through the REAL
        // ValidatorConfig.Parse, and assert the embedded copy against the PARSED value —
        // including LowerBoundSlack/StarBandSlack/NoveltyMinDistance/AxisBBandCaps compared
        // to what parsing actually produces, not pinned to a literal null.
        [Test]
        public void ValidatorConfig_MatchesTheRealSourceFile_Values()
        {
            string repoRoot = Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, "..", ".."));
            string path = Path.Combine(repoRoot, "config", "validator_thresholds.json");
            Assert.That(File.Exists(path), Is.True,
                "precondition: the real validator-config source must exist at " + path
                + " — otherwise this drift guard proves nothing");
            byte[] real = File.ReadAllBytes(path);
            var parsed = ValidatorConfig.Parse(real);
            Assert.That(parsed.Ok, Is.True,
                "precondition: the real file must parse through the REAL ValidatorConfig.Parse: "
                + parsed.Error);

            var cfg = DailyRuntimeInputs.ValidatorConfig;
            Assert.That(cfg.JitterSampleCount, Is.EqualTo(parsed.Value.JitterSampleCount),
                "a future config/validator_thresholds.json edit must fail HERE, not drift "
                + "silently against the embedded copy DailyPipeline actually runs against");
            Assert.That(cfg.LowerBoundSlack, Is.EqualTo(parsed.Value.LowerBoundSlack));
            Assert.That(cfg.StarBandSlack, Is.EqualTo(parsed.Value.StarBandSlack));
            Assert.That(cfg.NoveltyMinDistance, Is.EqualTo(parsed.Value.NoveltyMinDistance));
            Assert.That(cfg.AxisBBandCaps, Is.EqualTo(parsed.Value.AxisBBandCaps));
        }

        // F1 (review fix round): same rewrite as above. PrevalidationDays/AnchorDateKey are
        // DELIBERATELY not drift-guarded here — DailyRuntimeInputs documents them as
        // CI-horizon placeholders DailyPipeline.Run never consults for a single-date request
        // (see DailyRuntimeInputs.cs), so comparing them to the parsed file would assert a
        // claim this class never makes. SaltMaxK is the one field this class actually claims
        // mirrors the file, so it is the one field drift-guarded against the REAL parse.
        [Test]
        public void PipelineConfig_CarriesTheRealSaltMaxK()
        {
            string repoRoot = Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, "..", ".."));
            string path = Path.Combine(repoRoot, "config", "daily_pipeline.json");
            Assert.That(File.Exists(path), Is.True,
                "precondition: the real pipeline-config source must exist at " + path
                + " — otherwise this drift guard proves nothing");
            byte[] real = File.ReadAllBytes(path);
            var parsed = DailyPipelineConfig.Parse(real);
            Assert.That(parsed.Ok, Is.True,
                "precondition: the real file must parse through the REAL "
                + "DailyPipelineConfig.Parse: " + parsed.Error);

            var cfg = DailyRuntimeInputs.PipelineConfig("2026-08-24");
            Assert.That(cfg.SaltMaxK, Is.EqualTo(parsed.Value.SaltMaxK),
                "a future config/daily_pipeline.json SALT_MAX_K edit must fail HERE, not "
                + "drift silently against the embedded copy DailyPipeline actually runs "
                + "against");
        }

        // F5 (review fix round): DailyRuntimeInputs supplies exactly THREE embedded inputs
        // (schema, validator config, pipeline config) — GameRoot.ResolveDailyBoard passes a
        // FOURTH DailyRunRequest input, weekdayCurveBytes, as a literal null (#73's own
        // documented NEW-Q21 "absent file" behavior). config/daily_weekday_curve.json is
        // gated OUT of existence today by the daily wrapper's own criterion-9 check
        // (tests/daily/daily-pipeline.test.sh: "no agent may commit a curve"), so this test
        // passes TRIVIALLY while that gate holds — it is a forcing function, not a proof of
        // absence. The day NEW-Q21 answers and the file lands, this test goes RED and forces
        // a real decision: DailyRuntimeInputs needs a fourth embedded input
        // (WeekdayCurveBytes, mirroring SchemaBytes/ValidatorConfig/PipelineConfig) and
        // GameRoot must stop passing null, rather than silently ignoring a now-real curve
        // file. Recorded as Known debt in the frozen contract's amendment section — do not
        // silently delete this test if it ever fails.
        [Test]
        public void WeekdayCurveBytes_IfTheFileExists_MustBeEmbedded()
        {
            string repoRoot = Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, "..", ".."));
            string path = Path.Combine(repoRoot, "config", "daily_weekday_curve.json");
            if (!File.Exists(path))
            {
                Assert.Pass("config/daily_weekday_curve.json does not exist (NEW-Q21 still "
                    + "open) — nothing to embed; this test exists to force the decision the "
                    + "day it lands");
                return;
            }
            Assert.Fail("config/daily_weekday_curve.json now EXISTS — DailyRuntimeInputs "
                + "must gain a fourth embedded input (WeekdayCurveBytes) mirroring the "
                + "schema/validator/pipeline pattern, and GameRoot.ResolveDailyBoard must "
                + "stop passing weekdayCurveBytes: null. This failure is deliberate (F5) — "
                + "it means the truth-fork this test exists for has arrived.");
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
