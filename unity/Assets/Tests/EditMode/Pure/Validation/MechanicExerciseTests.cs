using System.IO;
using System.Linq;
using NUnit.Framework;
using CatMetro.Content;
using CatMetro.Content.Validation;
using CatMetro.Domain.Solver;
using CatMetro.Tests.Domain;
using Newtonsoft.Json.Linq;

namespace CatMetro.Tests.Validation
{
    // CM-C5.1 criteria 1-5, 8: the dead-newMechanic gate. The observer replays the
    // solver-optimal log through the ONE replay seam; the liveness verdict rides
    // CorpusReport.CampaignVerdicts (no 12th stage, no 12th per-level row); posture BLOCKING
    // (ratified 2026-08-05/06). Tests may use file APIs; the shipped Content assembly may not.
    [TestFixture]
    public class MechanicExerciseTests
    {
        private static byte[] L004Bytes() =>
            File.ReadAllBytes(Path.Combine(Fixtures.RepoRoot(), "content", "levels", "L004.json"));

        private static byte[] DeadFixtureBytes() =>
            File.ReadAllBytes(Path.Combine(Fixtures.RepoRoot(),
                "tests", "validation", "fixtures", "dead-mechanic", "L004-dead-queue.json"));

        private static byte[] LiveFixtureBytes() =>
            File.ReadAllBytes(Path.Combine(Fixtures.RepoRoot(),
                "tests", "validation", "fixtures", "dead-mechanic", "L004-live-queue.json"));

        private static CorpusReport Run(params CorpusMember[] members) =>
            CorpusValidator.Validate(new ValidationRequest(
                VFixtures.SchemaBytes(), VFixtures.BareConfig(),
                "2026-08-01T00:00:00+00:00", members, maxNodesExpanded: 200000));

        private static StageVerdict Liveness(CorpusReport report, string levelId) =>
            report.CampaignVerdicts.Single(v =>
                v.Value.StartsWith("tag=CM-R06.2-liveness:" + levelId));

        // The real L001 + the real L004, as the campaign they ship in.
        private static readonly System.Lazy<CorpusReport> ShippedPair =
            new System.Lazy<CorpusReport>(() => Run(
                new CorpusMember("content/levels/L001.json", VFixtures.L001Bytes(), true),
                new CorpusMember("content/levels/L004.json", L004Bytes(), true)));

        // The real L001 + the dead-queue fixture (criterion 3's negative).
        private static readonly System.Lazy<CorpusReport> DeadPair =
            new System.Lazy<CorpusReport>(() => Run(
                new CorpusMember("content/levels/L001.json", VFixtures.L001Bytes(), true),
                new CorpusMember("content/levels/L004.json", DeadFixtureBytes(), true)));

        // --- criterion 1a: the observer measures the optimal trace through the replay seam ---
        [Test]
        public void Observer_AuthoredL004_QueueExercisedMaxOneAtTickEight()
        {
            var imported = VFixtures.Import(L004Bytes());
            var solve = LevelSolver.Solve(imported.Graph, (ulong)imported.Dto.Seed, 200000);
            Assert.That(solve.Verdict, Is.EqualTo(SolveVerdict.Solved), "the authored L004 solves");

            var rec = MechanicExercise.Observe(imported.Graph, (ulong)imported.Dto.Seed, solve.OptimalLog);
            // Hand-derived: the second same-tick emission at tick 8 enqueues (the mouth carries a
            // zero-progress train and the queued head cannot release that same tick).
            Assert.That(rec.MaxQueued, Is.EqualTo(1), "queue exercised");
            Assert.That(rec.MaxQueuedAtTick, Is.EqualTo(8), "sampled AFTER the step, tick = Tick-1");
        }

        // --- criterion 1b: determinism ---
        [Test]
        public void Observer_TwoRuns_ProduceAnEqualRecord()
        {
            var imported = VFixtures.Import(L004Bytes());
            var solve = LevelSolver.Solve(imported.Graph, (ulong)imported.Dto.Seed, 200000);
            var a = MechanicExercise.Observe(imported.Graph, (ulong)imported.Dto.Seed, solve.OptimalLog);
            var b = MechanicExercise.Observe(imported.Graph, (ulong)imported.Dto.Seed, solve.OptimalLog);
            Assert.That(a.MaxQueued, Is.EqualTo(b.MaxQueued));
            Assert.That(a.MaxQueuedAtTick, Is.EqualTo(b.MaxQueuedAtTick));
            Assert.That(a.RouteChanged, Is.EqualTo(b.RouteChanged));
            Assert.That(a.RouteChangedAtTick, Is.EqualTo(b.RouteChangedAtTick));
            Assert.That(a.SwitchesUsed, Is.EqualTo(b.SwitchesUsed));
        }

        // --- criterion 2: every schema mechanic has a declared, tested disposition ---
        [TestCase("switch", MechanicDisposition.Observable)]
        [TestCase("queue", MechanicDisposition.Observable)]
        [TestCase("second-source", MechanicDisposition.Unreachable)]
        [TestCase("wildcard", MechanicDisposition.Unreachable)]
        [TestCase("cooldown", MechanicDisposition.Unobservable)]
        [TestCase("gate", MechanicDisposition.Unobservable)]
        [TestCase("express", MechanicDisposition.Unobservable)]
        [TestCase("reversible", MechanicDisposition.Unobservable)]
        public void DispositionTable_MapsEachMechanic(string mechanic, MechanicDisposition expected)
        {
            Assert.That(MechanicExercise.Dispositions[mechanic], Is.EqualTo(expected));
        }

        [Test]
        public void DispositionTable_KeySetEqualsTheSchemaEnumBytes()
        {
            var schema = JObject.Parse(System.Text.Encoding.UTF8.GetString(VFixtures.SchemaBytes()));
            var enumValues = ((JArray)schema["properties"]["meta"]["properties"]["mechanics"]["items"]["enum"])
                .Select(t => (string)t).ToArray();
            Assert.That(MechanicExercise.Dispositions.Keys, Is.EquivalentTo(enumValues),
                "a schema-enum edit must fail this test rather than drift");
        }

        [Test]
        public void UnobservableNewMechanic_Gate_IsPinnedNonBlockingNeverPass()
        {
            var level = VFixtures.Level(o =>
            {
                o["meta"]["mechanics"] = new JArray("gate");
                o["meta"]["newMechanic"] = "gate";
            });
            var report = Run(new CorpusMember("content/levels/L001.json", level, true));
            var row = Liveness(report, "L001");
            Assert.That(row.Code, Is.EqualTo(StageVerdictCode.Pinned), "named, never silently passed");
            Assert.That(row.Code, Is.Not.EqualTo(StageVerdictCode.Pass));
            Assert.That(row.Blocks, Is.False);
            Assert.That(row.Detail, Is.EqualTo("PINNED(gate unobservable — no DTO field)"));
        }

        // --- criterion 3: the gate fires on the dead fixture, and only there ---
        [Test]
        public void DeadQueueFixture_FailsBlocking_NamingLevelAndMechanic()
        {
            var report = DeadPair.Value;
            var row = Liveness(report, "L004");
            Assert.That(row.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(row.Blocks, Is.True, "ratified posture: BLOCKING");
            Assert.That(row.Detail, Does.Contain("L004").And.Contain("queue"));
            Assert.That(report.ExitFailure, Is.True);
        }

        [Test]
        public void LiveQueueTwin_Passes_AndExitIsClean()
        {
            var report = Run(
                new CorpusMember("content/levels/L001.json", VFixtures.L001Bytes(), true),
                new CorpusMember("content/levels/L004.json", LiveFixtureBytes(), true));
            var row = Liveness(report, "L004");
            Assert.That(row.Code, Is.EqualTo(StageVerdictCode.Pass));
            Assert.That(report.ExitFailure, Is.False);
        }

        [Test]
        public void DeadQueueCorpus_ReportsExactlyOneBlockingVerdict()
        {
            var report = DeadPair.Value;
            int blocking = report.Levels.SelectMany(l => l.Verdicts).Count(v => v.Blocks)
                + report.CampaignVerdicts.Count(v => v.Blocks);
            Assert.That(blocking, Is.EqualTo(1),
                "the fixture must fail for liveness, not for a band/order/count/solver reason");
        }

        // --- criterion 4: no 12th row — the verdict rides CampaignVerdicts in both forms ---
        [Test]
        public void Verdict_RidesCampaignVerdicts_AndEveryLevelKeepsElevenRows()
        {
            var report = DeadPair.Value;
            Assert.That(report.Levels.All(l => l.Verdicts.Count == 11), Is.True,
                "the stage inventory stays exactly 11 per level");
            Assert.That(report.CampaignVerdicts.Any(v => v.Blocks && v.Detail.Contains("L004")),
                Is.True, "the dead-mechanic verdict lives in CampaignVerdicts");
            var json = JObject.Parse(report.ToJson());
            Assert.That(((JArray)json["campaign"]).Any(c =>
                (bool)c["blocks"] && ((string)c["detail"]).Contains("L004")), Is.True,
                "and it renders at root[campaign] in the JSON form");
        }

        // --- criterion 5: non-campaign members and the daily shape are untouched ---
        [Test]
        public void StressOnlyCorpus_LivenessSkipsNonBlocking()
        {
            var report = Run(
                new CorpusMember("stress_boards.json#L701", VFixtures.StressLevelBytes(0), false),
                new CorpusMember("stress_boards.json#L702", VFixtures.StressLevelBytes(1), false));
            var row = report.CampaignVerdicts.Single(v => v.Value.StartsWith("tag=CM-R06.2-liveness"));
            Assert.That(row.Code, Is.EqualTo(StageVerdictCode.Skipped));
            Assert.That(row.Detail, Is.EqualTo("SKIPPED(no campaign members)"));
            Assert.That(row.Blocks, Is.False);
            Assert.That(report.ExitFailure, Is.False, "ExitFailure unchanged by the gate");
        }

        [Test]
        public void DailyShapedSingleMember_ExitFailureIdenticalWithTheGatePresent()
        {
            // DailyPipeline.Run validates each candidate as a single-member NON-campaign corpus;
            // the gate must never judge it (H7 default: NO liveness row in the daily artifact).
            var report = Run(new CorpusMember("2026-08-06#k0", VFixtures.L001Bytes(), false));
            Assert.That(report.ExitFailure, Is.False,
                "identical to the pre-gate verdict for this member");
            Assert.That(report.CampaignVerdicts.Where(v => v.Value.StartsWith("tag=CM-R06.2-liveness"))
                .All(v => v.Code == StageVerdictCode.Skipped && !v.Blocks), Is.True);
        }

        // --- criterion 6c: each campaign verdict carries a unique machine tag ---
        [Test]
        public void CampaignVerdicts_EachCarriesAUniqueTag()
        {
            var report = ShippedPair.Value;
            var tags = report.CampaignVerdicts
                .Select(v => v.Value.Split(';')[0]).ToList();
            Assert.That(tags, Is.All.Matches<string>(t => t.StartsWith("tag=")),
                "every campaign verdict is machine-selectable");
            Assert.That(tags, Is.Unique, "criterion 6c: Single() selectors must never throw");
        }

        // --- criterion 8: the measurement always prints, blocking or not ---
        [Test]
        public void L001_LivenessValue_CarriesTheSwitchEvidence()
        {
            // Hand-derived: initialRoute 1 routes to BLU which accepts only blue, so a red cat
            // cannot be delivered without a toggle — the optimal log must exercise the switch.
            var row = Liveness(ShippedPair.Value, "L001");
            Assert.That(row.Code, Is.EqualTo(StageVerdictCode.Pass));
            Assert.That(row.Value, Does.Contain("newMechanic=switch; exercised=true; evidence=toggles=1,routeChangedAtTick="));
        }

        [Test]
        public void L004_LivenessValue_CarriesTheQueueEvidence()
        {
            var row = Liveness(ShippedPair.Value, "L004");
            Assert.That(row.Code, Is.EqualTo(StageVerdictCode.Pass));
            Assert.That(row.Value,
                Does.Contain("newMechanic=queue; exercised=true; evidence=maxQueued=1@tick 8"));
        }

        [Test]
        public void NullNewMechanic_IsSkipped_NonBlocking()
        {
            var l002 = VFixtures.Level(o =>
            {
                o["id"] = "L002";
                o["meta"]["mechanics"] = new JArray("switch");
                o["meta"]["newMechanic"] = null;
            });
            var report = Run(
                new CorpusMember("content/levels/L001.json", VFixtures.L001Bytes(), true),
                new CorpusMember("content/levels/L002.json", l002, true));
            var row = Liveness(report, "L002");
            Assert.That(row.Code, Is.EqualTo(StageVerdictCode.Skipped));
            Assert.That(row.Detail, Is.EqualTo("SKIPPED(no declared newMechanic)"));
            Assert.That(row.Blocks, Is.False);
            Assert.That(report.ExitFailure, Is.False);
        }

        [Test]
        public void NoWinningLog_IsSkipped_NotFail()
        {
            // BeamMissLevel: solver reports NotFound (beam miss, no pins) — the same SKIPPED
            // shape brittleness already uses, never a liveness Fail.
            var report = Run(new CorpusMember(
                "content/levels/L001.json", VFixtures.BeamMissLevel(), true));
            var row = Liveness(report, "L001");
            Assert.That(row.Code, Is.EqualTo(StageVerdictCode.Skipped));
            Assert.That(row.Detail, Is.EqualTo("SKIPPED(no winning log)"));
            Assert.That(row.Blocks, Is.False);
        }
    }
}
