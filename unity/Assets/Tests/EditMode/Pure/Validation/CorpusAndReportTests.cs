using System.Linq;
using NUnit.Framework;
using CatMetro.Content.Validation;
using Newtonsoft.Json.Linq;

namespace CatMetro.Tests.Validation
{
    // Criterion 11: stage 10 — absent key STALE, older STALE, newer FRESH, and a STALE verdict
    // contributes 0 to the exit code while Q-O is open.
    [TestFixture]
    public class StalenessTests
    {
        private const string Reference = "2026-08-01T00:00:00+00:00";

        [Test]
        public void AbsentKey_IsStale_AndDoesNotBlock()
        {
            var v = StalenessStage.Check(VFixtures.Import(VFixtures.L001Bytes()).Dto, Reference);
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Stale), "ADR-0008:119-123: absent key == stale");
            Assert.That(v.Blocks, Is.False, "Q-O open: stage 10 computes, prints, does not block");
            Assert.That(v.Detail, Does.Contain("Q-O"), "the report says so verbatim");
        }

        [Test]
        public void KeyOlderThanReference_IsStale()
        {
            var level = VFixtures.Level(o => o["meta"]["validatedAt"] = "2026-01-01T00:00:00+00:00");
            var v = StalenessStage.Check(VFixtures.Import(level).Dto, Reference);
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Stale));
            Assert.That(v.Blocks, Is.False);
        }

        [Test]
        public void KeyNewerThanReference_IsFresh()
        {
            var level = VFixtures.Level(o => o["meta"]["validatedAt"] = "2026-09-01T00:00:00+00:00");
            var v = StalenessStage.Check(VFixtures.Import(level).Dto, Reference);
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Fresh));
        }

        [Test]
        public void ReferenceUnavailable_IsStaleNotACrash()
        {
            var v = StalenessStage.Check(VFixtures.Import(VFixtures.L001Bytes()).Dto, null);
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Stale));
            Assert.That(v.Detail, Does.Contain("unavailable").IgnoreCase);
        }

        [Test] // review F9 — mixed offsets compare as instants, not as strings
        public void MixedOffsets_CompareAsInstants()
        {
            // Stamp 02:00Z vs reference 01:08:44-07:00 (= 08:08:44Z): ordinally "newer", actually
            // six hours STALE — the reviewer's exact scenario.
            var level = VFixtures.Level(o => o["meta"]["validatedAt"] = "2026-08-04T02:00:00Z");
            var v = StalenessStage.Check(VFixtures.Import(level).Dto, "2026-08-04T01:08:44-07:00");
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Stale),
                "an ordinal compare would call this FRESH");
        }
    }

    // Criterion 12: stage 11 — checklist row per level; capstones need 3 testers; never blocks.
    [TestFixture]
    public class PlaytestTests
    {
        [Test]
        public void L001_Row_OneTester_PendingNonBlocking()
        {
            var (row, v) = PlaytestStage.Row(VFixtures.Import(VFixtures.L001Bytes()).Dto);
            Assert.That(row.LevelId, Is.EqualTo("L001"));
            Assert.That(row.Band, Is.EqualTo("onboarding"));
            Assert.That(row.Capstone, Is.False);
            Assert.That(row.RequiredTesters, Is.EqualTo(1));
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Pending));
            Assert.That(v.Blocks, Is.False, "CI cannot run humans (ADR-0009:35); depends on D-6");
            Assert.That(v.Detail, Does.Contain("HUMAN-VERIFIED (pending)"));
        }

        [Test]
        public void CapstoneBand_NeedsThreeTesters()
        {
            var level = VFixtures.Level(o => o["meta"]["band"] = "capstone");
            var (row, _) = PlaytestStage.Row(VFixtures.Import(level).Dto);
            Assert.That(row.Capstone, Is.True);
            Assert.That(row.RequiredTesters, Is.EqualTo(3), "product_spec.md:647");
        }
    }

    // Criterion 14: the corpus is content/levels/** PLUS the stress boards; stress boards run
    // stages 1-8 + 10 with a stage-11 row, and stage 9 + campaign assertions skip them.
    // Criterion 16: two output forms, one truth — JSON shape, seconds = CompletionTicks / 8,
    // PINNED(NEW-Q1) on the range comparison.
    [TestFixture]
    public class CorpusAndReportTests
    {
        private static readonly System.Lazy<CorpusReport> Shared = new System.Lazy<CorpusReport>(() =>
        {
            var members = new[]
            {
                new CorpusMember("content/levels/L001.json", VFixtures.L001Bytes(), true),
                new CorpusMember("stress_boards.json#L701", VFixtures.StressLevelBytes(0), false),
                new CorpusMember("stress_boards.json#L702", VFixtures.StressLevelBytes(1), false),
            };
            // Test-speed budget: a stress-board search may hit it and report NotFound(Budget),
            // which is non-blocking by design — the shipped harness uses the authored default.
            var request = new ValidationRequest(VFixtures.SchemaBytes(), VFixtures.BareConfig(),
                "2026-08-01T00:00:00+00:00", members,
                maxNodesExpanded: CatMetro.Domain.Solver.SolverBounds.MAX_NODES_EXPANDED);
            return CorpusValidator.Validate(request);
        });

        private static CorpusReport FullRun() => Shared.Value;

        private static CorpusReport BudgetLimitedReport(bool campaign)
        {
            var member = new CorpusMember(
                campaign ? "content/levels/L001.json" : "stress_boards.json#L001",
                VFixtures.L001Bytes(), campaign);
            return CorpusValidator.Validate(new ValidationRequest(
                VFixtures.SchemaBytes(), VFixtures.BareConfig(), null, new[] { member },
                maxNodesExpanded: 1));
        }

        private static StageVerdict CampaignCountVerdict(int count)
        {
            var level = VFixtures.L001Bytes();
            var members = Enumerable.Range(0, count)
                .Select(index => new CorpusMember(
                    "content/levels/count-fixture-" + index + ".json", level, true))
                .ToArray();
            var report = CorpusValidator.Validate(new ValidationRequest(
                VFixtures.SchemaBytes(), VFixtures.BareConfig(), null, members,
                maxNodesExpanded: 1));
            return report.CampaignVerdicts.Single(v => v.Value == "tag=CM-R09.1");
        }

        [Test]
        public void StressBoards_AreValidated_WithTheQPStageSet()
        {
            var report = FullRun();
            Assert.That(report.Levels.Select(l => l.LevelId),
                Is.EquivalentTo(new[] { "L001", "L701", "L702" }), "criterion 14: both corpora");

            var l701 = report.Levels.Single(l => l.LevelId == "L701");
            var stage8 = l701.Verdicts.Single(v => v.Stage == Stage.DifficultyCheck);
            // Review F7: the POSITIVE form — stage 8 genuinely ran (raw axes computed, verdict is
            // the Q-R Unconfigured), so a budget regression that skips it fails loudly here.
            Assert.That(stage8.Code, Is.EqualTo(StageVerdictCode.Unconfigured),
                "stage 8 RUNS for stress boards (Q-P): " + stage8.Detail);
            Assert.That(stage8.Value, Does.Contain("B="), "raw axes were actually computed");
            var stage9 = l701.Verdicts.Single(v => v.Stage == Stage.NoveltyCheck);
            Assert.That(stage9.Detail, Is.EqualTo("SKIPPED(non-campaign)"));

            // Review F2: the stage-6 counts remain visible and the verdict is non-blocking.
            // Human re-pin ruling, 2026-08-09: the centered solver makes every L701 jitter
            // sample a win; pin the complete report so either robustness or window drift is loud.
            var stage6 = l701.Verdicts.Single(v => v.Stage == Stage.BrittlenessAccessibility);
            Assert.That(stage6.Blocks, Is.False, stage6.Detail);
            Assert.That(stage6.Value, Is.EqualTo(
                "retention=100% (wins=20 losses=0 pinned=0) windows=[20,20,24,25,20]"),
                "the centered L701 NEW-Q4 characteristic stays exact");

            // Criterion 12: the checklist row set EQUALS the corpus set — every member, no extras.
            Assert.That(report.Levels.Select(l => l.Checklist).All(c => c != null), Is.True);
            Assert.That(report.Levels.Select(l => l.Checklist.LevelId),
                Is.EquivalentTo(report.Levels.Select(l => l.LevelId)));
        }

        [Test]
        public void IncompleteCampaignCount_BlocksAndExcludesStressBoards()
        {
            var report = FullRun();
            var count = report.CampaignVerdicts.Single(v => v.Value == "tag=CM-R09.1");
            Assert.That(count.Detail, Does.Contain("1/60"),
                "the 60-level count sees content/levels/** only — never the stress boards");
            Assert.That(count.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(count.Blocks, Is.True, "an incomplete shipped campaign blocks");
            Assert.That(report.ExitFailure, Is.True);
            var proof = report.CampaignVerdicts.Single(v =>
                v.Value == "tag=CM-LADDER-solve-proof");
            Assert.That(proof.Code, Is.EqualTo(StageVerdictCode.Pass), proof.Detail);
        }

        [TestCase(0, StageVerdictCode.Fail, true)]
        [TestCase(59, StageVerdictCode.Fail, true)]
        [TestCase(60, StageVerdictCode.Pass, false)]
        [TestCase(61, StageVerdictCode.Fail, true)]
        public void CampaignCount_RequiresExactly60(
            int count, StageVerdictCode expectedCode, bool expectedBlocks)
        {
            var verdict = CampaignCountVerdict(count);
            Assert.That(verdict.Detail, Does.Contain(count + "/60"));
            Assert.That(verdict.Code, Is.EqualTo(expectedCode));
            Assert.That(verdict.Blocks, Is.EqualTo(expectedBlocks));
        }

        [Test]
        public void CampaignBudgetMiss_IsBlockedByExactSolveProof()
        {
            var report = BudgetLimitedReport(campaign: true);
            var level = report.Levels.Single();
            Assert.That(level.Solve.Verdict,
                Is.EqualTo(CatMetro.Domain.Solver.SolveVerdict.NotFound));
            Assert.That(level.Solve.NotFoundReason,
                Is.EqualTo(CatMetro.Domain.Solver.NotFoundReason.Budget));

            var solverWarning = level.Verdicts.Single(v => v.Stage == Stage.Solver);
            Assert.That(solverWarning.Code, Is.EqualTo(StageVerdictCode.Warn));
            Assert.That(solverWarning.Blocks, Is.False,
                "the existing per-level NotFound row remains a warning");

            var proof = report.CampaignVerdicts.Single(v =>
                v.Value == "tag=CM-LADDER-solve-proof");
            Assert.That(proof.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(proof.Blocks, Is.True);
            Assert.That(proof.Detail, Does.Contain("L001")
                .And.Contain("NotFound").And.Contain("Budget").And.Contain("beamWidthUsed=0"));
            Assert.That(report.ExitFailure, Is.True);
        }

        [Test]
        public void NonCampaignBudgetMiss_RemainsAWarningWhileEmptyCampaignCountBlocks()
        {
            var report = BudgetLimitedReport(campaign: false);
            var level = report.Levels.Single();
            Assert.That(level.Solve.Verdict,
                Is.EqualTo(CatMetro.Domain.Solver.SolveVerdict.NotFound));
            Assert.That(level.Solve.NotFoundReason,
                Is.EqualTo(CatMetro.Domain.Solver.NotFoundReason.Budget));

            var solverWarning = level.Verdicts.Single(v => v.Stage == Stage.Solver);
            Assert.That(solverWarning.Code, Is.EqualTo(StageVerdictCode.Warn));
            Assert.That(solverWarning.Blocks, Is.False);

            var proof = report.CampaignVerdicts.Single(v =>
                v.Value == "tag=CM-LADDER-solve-proof");
            Assert.That(proof.Code, Is.EqualTo(StageVerdictCode.Skipped));
            Assert.That(proof.Blocks, Is.False);
            var count = report.CampaignVerdicts.Single(v => v.Value == "tag=CM-R09.1");
            Assert.That(count.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(count.Blocks, Is.True);
            Assert.That(report.ExitFailure, Is.True,
                "the empty campaign count blocks even though the non-campaign solver row does not");
        }

        [Test]
        public void SchemaDeclaresTheCompleteMechanicLadderVocabulary()
        {
            var schema = JObject.Parse(System.Text.Encoding.UTF8.GetString(VFixtures.SchemaBytes()));
            var metaProperties = schema["properties"]["meta"]["properties"];
            var bands = metaProperties["band"]["enum"].Values<string>();
            Assert.That(bands, Is.SupersetOf(new[]
            {
                "onboarding", "shape", "budget", "two-source", "alternation", "tunnel",
                "combination", "timed-gates", "oneway", "multi-line", "combo", "stray",
                "pressure", "capstone", "queue-reading", "expert", "daily",
            }));

            var mechanics = metaProperties["mechanics"]["items"]["enum"].Values<string>();
            Assert.That(mechanics, Is.SupersetOf(new[]
            {
                "switch", "queue", "second-source", "wildcard", "cooldown", "gate",
                "express", "reversible", "shape", "budget", "tunnel", "second-train",
                "hold", "stray", "wildcard-express",
            }));
        }

        [Test]
        public void CampaignBandTableMatchesThe60LevelLadder()
        {
            var field = typeof(CorpusValidator).GetField("BandTable",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(field, Is.Not.Null);
            var actual = (System.ValueTuple<string, int, int, double, double>[])field.GetValue(null);
            var expected = new System.ValueTuple<string, int, int, double, double>[]
            {
                ("onboarding", 1, 8, 0.05, 0.16),
                ("shape", 9, 12, 0.17, 0.24),
                ("budget", 13, 16, 0.25, 0.32),
                ("two-source", 17, 20, 0.33, 0.40),
                ("alternation", 21, 24, 0.41, 0.48),
                ("tunnel", 25, 28, 0.49, 0.56),
                ("combination", 29, 32, 0.57, 0.64),
                ("timed-gates", 33, 36, 0.65, 0.72),
                ("oneway", 37, 40, 0.73, 0.78),
                ("multi-line", 41, 44, 0.79, 0.84),
                ("combo", 45, 48, 0.85, 0.88),
                ("stray", 49, 52, 0.89, 0.92),
                ("pressure", 53, 56, 0.93, 0.96),
                ("capstone", 57, 60, 0.97, 1.00),
            };
            Assert.That(actual, Is.EqualTo(expected));
            for (int i = 1; i < actual.Length; i++)
            {
                Assert.That(actual[i].Item2, Is.EqualTo(actual[i - 1].Item3 + 1));
                Assert.That(actual[i].Item4, Is.GreaterThan(actual[i - 1].Item5));
            }
        }

        [Test]
        public void MechanicOrder_ViolationBlocks()
        {
            // A second campaign level whose mechanics grow by TWO new mechanics at once.
            var l002 = VFixtures.Level(o =>
            {
                o["id"] = "L002";
                o["meta"]["band"] = "onboarding";
                o["meta"]["mechanics"] = new JArray("switch", "queue", "second-source");
                o["meta"]["newMechanic"] = "queue";
            });
            var members = new[]
            {
                new CorpusMember("L001.json", VFixtures.L001Bytes(), true),
                new CorpusMember("L002.json", l002, true),
            };
            var report = CorpusValidator.Validate(new ValidationRequest(
                VFixtures.SchemaBytes(), VFixtures.BareConfig(), null, members, maxNodesExpanded: 200000));
            var order = report.CampaignVerdicts.Single(v => v.Value == "tag=CM-R06.2");
            Assert.That(order.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(order.Blocks, Is.True, "CM-R06.2 violations are authoring defects now");
            Assert.That(report.ExitFailure, Is.True);
        }

        [Test]
        public void Report_JsonShape_SecondsAndTheNewQ1Pin()
        {
            var report = FullRun();
            var json = JObject.Parse(report.ToJson());
            var levels = (JArray)json["levels"];
            Assert.That(levels.Count, Is.EqualTo(3));

            var l001 = levels.Single(l => (string)l["id"] == "L001");
            Assert.That(((JArray)l001["stages"]).Count, Is.EqualTo(11), "a verdict row per stage");
            foreach (var s in (JArray)l001["stages"])
            {
                Assert.That(s["stage"], Is.Not.Null);
                Assert.That(s["code"], Is.Not.Null);
                Assert.That(s["detail"], Is.Not.Null);
                Assert.That(s["value"], Is.Not.Null);
                Assert.That(s["blocks"], Is.Not.Null);
            }
            var solve = l001["solve"];
            Assert.That((int)solve["completionTicks"], Is.EqualTo(50));
            Assert.That((double)solve["seconds"], Is.EqualTo(50 / 8.0).Within(1e-12),
                "CM-R19.1 consumes CompletionTicks / 8");
            Assert.That((string)solve["secondsVerdict"], Is.EqualTo("PINNED(NEW-Q1)"),
                "the 40-75 s range comparison is pinned, printed, non-blocking");
            Assert.That((bool)json["exitFailure"], Is.True,
                "the one-level campaign is incomplete and the count assertion blocks");
        }

        [Test]
        public void Table_CarriesTheStageGridAndThePartialMarker()
        {
            var report = FullRun();
            var table = report.ToTable();
            Assert.That(table, Does.Contain("L001").And.Contain("L701").And.Contain("L702"));
            Assert.That(table, Does.Contain("PARTIAL(Q-J)"), "axis H's caveat reaches the human");
            Assert.That(table, Does.Contain("UNCONFIGURED"), "Q-R rows are visible, not skipped");
        }
    }
}
