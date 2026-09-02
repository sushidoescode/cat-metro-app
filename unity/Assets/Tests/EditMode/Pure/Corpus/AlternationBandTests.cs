using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Domain.Solver;

namespace CatMetro.Tests.Corpus
{
    // CM-C11: L006-L010, the alternation band. Criterion 1: one case per field family per level,
    // asserted from a raw-JSON key walk (parser bugs can't mask content bugs — CM-C2a criterion
    // 1 / L002-L005 pattern). Tests may use file APIs; the shipped Content assembly may not.
    //
    // CM-C13 (lane 9, design-phase re-author): L007-L010's boards were near-duplicates of one
    // shared GATE/HOLD/J1/RED/BLU shape (the #62 review's flagged anti-pattern) with a single
    // real switch decision each — the fixed tie-break solver (SOLVER-TIEBREAK) removed the reason
    // that shape existed (it dodged the old earliest-tick brittleness trap). This file's L007-L010
    // sections are re-recorded two-sided against four distinct new topologies (per-level
    // rationale and novelty evidence recorded on PR #75). L006's sections (locked anchor)
    // are untouched.
    [TestFixture]
    public class AlternationBandFieldTests
    {
        private static JObject Raw(string id) =>
            JObject.Parse(System.Text.Encoding.UTF8.GetString(BandFixtures.Bytes(id)));

        private static readonly (string Id, string Name, int Seed, double Diff)[] Locked =
        {
            ("L006", "Alternating Line", 1006, 0.20),
            ("L007", "Ferry Timing", 1007, 0.22),
            ("L008", "Double Berth", 1008, 0.24),
            ("L009", "Tide Tables", 1009, 0.26),
            ("L010", "Harbor Capstone", 1010, 0.28),
        };

        [TestCaseSource(nameof(Locked))]
        public void Dto_CarriesTheLockedProgressionRow((string Id, string Name, int Seed, double Diff) row)
        {
            var dto = BandFixtures.Import(row.Id).Dto;
            Assert.That(dto.SchemaVersion, Is.EqualTo(2));
            Assert.That(dto.Id, Is.EqualTo(row.Id));
            Assert.That(dto.Seed, Is.EqualTo(row.Seed));
            Assert.That(dto.Name, Is.EqualTo(row.Name));
            Assert.That(dto.Meta.Band, Is.EqualTo("alternation"));
            Assert.That(dto.Meta.DifficultyTarget, Is.EqualTo(row.Diff).Within(1e-12));
            Assert.That(dto.Meta.Mechanics.ToArray(), Is.EqualTo(new[] { "switch", "queue" }));
            Assert.That(dto.Meta.NewMechanic, Is.Null);
            Assert.That(dto.Meta.AuthoredBy, Is.EqualTo("llm+validator"));
            Assert.That(dto.Meta.TeachingGoal, Is.Not.Null.And.Not.Empty);
            Assert.That(dto.Meta.TeachingGoal.Length, Is.LessThanOrEqualTo(160));
            Assert.That(dto.Meta.HasValidatedAt, Is.False);
            Assert.That(dto.Meta.ValidatedAt, Is.Null);
        }

        [TestCaseSource(nameof(Locked))]
        public void RawJson_CarriesTheLockedProgressionRow((string Id, string Name, int Seed, double Diff) row)
        {
            var j = Raw(row.Id);
            Assert.That((int)j["schemaVersion"], Is.EqualTo(2));
            Assert.That((string)j["id"], Is.EqualTo(row.Id));
            Assert.That((long)j["seed"], Is.EqualTo(row.Seed));
            Assert.That((string)j["name"], Is.EqualTo(row.Name));
            Assert.That((string)j["meta"]["band"], Is.EqualTo("alternation"));
            Assert.That((double)j["meta"]["difficultyTarget"], Is.EqualTo(row.Diff).Within(1e-12));
            Assert.That(j["meta"]["mechanics"].Select(t => (string)t), Is.EqualTo(new[] { "switch", "queue" }));
            Assert.That(j["meta"]["newMechanic"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That((string)j["meta"]["authoredBy"], Is.EqualTo("llm+validator"));
            Assert.That((int)j["meta"]["minActionWindowTicks"], Is.EqualTo(12));
            Assert.That(((JObject)j["meta"]).Property("validatedAt"), Is.Null,
                "AMD-09 / ADR-0008:119-123: the key is deleted when unvalidated, never null");
        }

        [Test]
        public void MinActionWindowTicks_Is12_ForAllFive()
        {
            foreach (var id in BandFixtures.Ids)
            {
                var dto = BandFixtures.Import(id).Dto;
                Assert.That(dto.Meta.MinActionWindowTicks, Is.EqualTo(12), id);
            }
        }

        [Test]
        public void TeachingGoals_AreDistinctAcrossAllTenCampaignLevels()
        {
            var ids = new[] { "L001", "L002", "L003", "L004", "L005", "L006", "L007", "L008", "L009", "L010" };
            var goals = ids.Select(id =>
                LevelImporter.Import(File.ReadAllBytes(
                    Path.Combine(BandFixtures.RepoRoot(), "content", "levels", id + ".json")))
                    .Value.Dto.Meta.TeachingGoal).ToList();
            Assert.That(goals, Is.Unique, "criterion 1: teachingGoal must be pairwise distinct across L001-L010");
        }
    }

    // Criterion 2: the L006 anchor is honoured byte-for-byte against the authored source
    // (CONFLICT-1 option A, human-ratified default at freeze).
    [TestFixture]
    public class L006AnchorFidelityTests
    {
        private static JObject Anchor()
        {
            var wrapper = JObject.Parse(System.Text.Encoding.UTF8.GetString(
                File.ReadAllBytes(Path.Combine(BandFixtures.RepoRoot(), "docs", "plan", "data", "example_levels.json"))));
            return (JObject)((JArray)wrapper["levels"]).Single(l => (string)l["id"] == "L006");
        }

        [Test]
        public void L006_MatchesTheAnchorFieldForField()
        {
            var shipped = Raw();
            var anchor = Anchor();
            Assert.That(JToken.DeepEquals(shipped, anchor), Is.True,
                $"content/levels/L006.json must be field-for-field identical to the anchor.\nshipped={shipped}\nanchor={anchor}");
        }

        private static JObject Raw() =>
            JObject.Parse(System.Text.Encoding.UTF8.GetString(BandFixtures.Bytes("L006")));

        [Test]
        public void L006_Dto_CarriesTheAnchorWinAndAccessibilityValues()
        {
            var dto = BandFixtures.Import("L006").Dto;
            Assert.That(dto.Win.TimeLimitTicks, Is.EqualTo(260));
            Assert.That(dto.Win.Deliveries, Is.EqualTo(8));
            Assert.That(dto.Win.PerfectMaxSwitches, Is.EqualTo(4));
            Assert.That(dto.Win.Stars.Two, Is.EqualTo(700));
            Assert.That(dto.Win.Stars.Three, Is.EqualTo(950));
            Assert.That(dto.Economy.BaseTickets, Is.EqualTo(25));
            Assert.That(dto.Economy.PerfectBonus, Is.EqualTo(15));
            Assert.That(dto.Meta.MinActionWindowTicks, Is.EqualTo(12));
            var board = dto.Nodes.ToArray();
            Assert.That(board.Select(n => n.Id), Is.EqualTo(new[] { "SRC", "J1", "RED", "BLU" }));
            Assert.That(board.Single(n => n.Id == "J1").QueueCapacity, Is.EqualTo(3));
        }
    }

    // Criteria 3/4/5: the corpus gate over the full band. Runs the real console validator
    // in-process against the shipped bytes — same shape as CorpusAndReportTests.cs.
    [TestFixture]
    public class AlternationBandGateTests
    {
        private static readonly System.Lazy<CatMetro.Content.Validation.CorpusReport> Shared =
            new System.Lazy<CatMetro.Content.Validation.CorpusReport>(() =>
        {
            var schemaBytes = File.ReadAllBytes(
                Path.Combine(BandFixtures.RepoRoot(), "docs", "plan", "data", "level_schema.json"));
            var configResult = CatMetro.Content.Validation.ValidatorConfig.Parse(File.ReadAllBytes(
                Path.Combine(BandFixtures.RepoRoot(), "config", "validator_thresholds.json")));
            Assert.That(configResult.Ok, Is.True, $"{configResult.Error}");
            var members = new[] { "L001", "L002", "L003", "L004", "L005", "L006", "L007", "L008", "L009", "L010" }
                .Select(id => new CatMetro.Content.Validation.CorpusMember(
                    "content/levels/" + id + ".json", BandFixtures.Bytes(id), true))
                .ToArray();
            var request = new CatMetro.Content.Validation.ValidationRequest(
                schemaBytes, configResult.Value, "2026-08-01T00:00:00+00:00", members);
            return CatMetro.Content.Validation.CorpusValidator.Validate(request);
        });

        private static CatMetro.Content.Validation.LevelReport Level(string id) =>
            Shared.Value.Levels.Single(l => l.LevelId == id);

        [Test]
        public void FullTenLevelCorpus_ExitsClean()
        {
            Assert.That(Shared.Value.ExitFailure, Is.False,
                string.Join("\n", Shared.Value.Levels.SelectMany(l => l.Verdicts).Where(v => v.Blocks)
                    .Select(v => v.Stage + ": " + v.Detail)));
        }

        [TestCaseSource(typeof(BandFixtures), nameof(BandFixtures.Ids))]
        public void Level_PassesEveryBlockingStage(string id)
        {
            var l = Level(id);
            var schema = l.Verdicts.Single(v => v.Stage == CatMetro.Content.Validation.Stage.Schema);
            Assert.That(schema.Code, Is.EqualTo(CatMetro.Content.Validation.StageVerdictCode.Pass), id + " " + schema.Detail);

            var stat = l.Verdicts.Single(v => v.Stage == CatMetro.Content.Validation.Stage.StaticAnalysis);
            // Tightened from Pass-or-Warn to Pass-only at the PR #75 review's Important-3:
            // the shipped gate accepted the #62 decoy anti-pattern (old L007's "BLU is a decoy"
            // Warn still yielded RESULT: OK), so Warn-tolerance here made this band's headline
            // claim a one-time run instead of a pin. Band levels L006-L010 are all Warn-free
            // post-CM-C13; a reintroduced decoy now goes RED here (proven both directions:
            // 20/20 green at this tip, old L007 fails this assertion).
            Assert.That(stat.Code, Is.EqualTo(CatMetro.Content.Validation.StageVerdictCode.Pass),
                id + " " + stat.Detail);

            Assert.That(l.Solve, Is.Not.Null, id + ": stage 4 never ran");
            Assert.That(l.Solve.Verdict, Is.EqualTo(CatMetro.Domain.Solver.SolveVerdict.Solved), id);
            Assert.That(l.Solve.BeamWidthUsed, Is.EqualTo(0), id + ": must be BFS-exact");

            var triv = l.Verdicts.Single(v => v.Stage == CatMetro.Content.Validation.Stage.TrivialityReject);
            Assert.That(triv.Code, Is.EqualTo(CatMetro.Content.Validation.StageVerdictCode.Pass), id + " " + triv.Detail);

            var britt = l.Verdicts.Single(v => v.Stage == CatMetro.Content.Validation.Stage.BrittlenessAccessibility);
            Assert.That(britt.Code, Is.EqualTo(CatMetro.Content.Validation.StageVerdictCode.Pass), id + " " + britt.Detail);
        }

        [Test]
        public void Campaign_LivenessSkipsEveryAlternationLevel_NoDeclaredNewMechanic()
        {
            foreach (var id in BandFixtures.Ids)
            {
                var row = Shared.Value.CampaignVerdicts.Single(v =>
                    v.Value.StartsWith("tag=CM-R06.2-liveness:" + id));
                Assert.That(row.Detail, Is.EqualTo("SKIPPED(no declared newMechanic)"), id);
                Assert.That(row.Blocks, Is.False, id);
            }
        }

        // Criterion 4: BOTH readings of NEW-Q4 must clear 70% — as amended by the human's
        // criterion-4(b) ruling (2026-08-08, in-session, agent-relayed; recorded verbatim in
        // CM-C11.md §Ruling): the pessimistic bar applies to L007-L010; L006's pessimistic
        // reading is pinned below as a recorded characteristic of the authored anchor
        // (ruling option 1 — re-scope; anchor bytes stay as authored). Parsed from the
        // stage-6 value string `retention=<x> (wins=W losses=L pinned=P) windows=[...]`
        // (ValidationStages.cs:507-508 format).
        [TestCaseSource(typeof(BandFixtures), nameof(BandFixtures.Ids))]
        public void Level_RetentionHolds_UnderBothNEWQ4Readings(string id)
        {
            var britt = Level(id).Verdicts.Single(v =>
                v.Stage == CatMetro.Content.Validation.Stage.BrittlenessAccessibility);
            var (w, l, p) = ParseRetention(britt.Value);
            int denom = w + l;
            Assert.That(denom, Is.GreaterThan(0), id + ": all-pinned sample set, cannot read retention — " + britt.Value);
            int optimistic = w * 100 / denom;
            Assert.That(optimistic, Is.GreaterThanOrEqualTo(70),
                id + " optimistic reading " + optimistic + "% — " + britt.Value);
            int pessimistic = w * 100 / (w + l + p);
            if (id == "L006")
            {
                // The 2026-08-09 re-pin ruling records the fixed solver's live characteristic:
                // centered ticks 43/83/123 keep a margin on both sides of all three decisions.
                // Two-sided on purpose: any future improvement OR regression turns this red and
                // sends the changed measurement back through the ruling gate.
                Assert.That((w, l, p), Is.EqualTo((20, 0, 0)),
                    id + " anchor characteristic drifted — " + britt.Value);
                Assert.That(pessimistic, Is.EqualTo(100),
                    id + " pessimistic characteristic drifted — " + britt.Value);
                return;
            }
            Assert.That(pessimistic, Is.GreaterThanOrEqualTo(70),
                id + " pessimistic reading " + pessimistic + "% — " + britt.Value);
        }

        // Criterion 5: minActionWindowTicks == 12 asserted (field tests above) AND every window
        // in the stage-6 value is >= 12, with a non-empty array (the vacuous-law guard).
        [TestCaseSource(typeof(BandFixtures), nameof(BandFixtures.Ids))]
        public void Level_ActionWindows_AreFlooredAndNonEmpty(string id)
        {
            var britt = Level(id).Verdicts.Single(v =>
                v.Stage == CatMetro.Content.Validation.Stage.BrittlenessAccessibility);
            var windows = ParseWindows(britt.Value);
            Assert.That(windows, Is.Not.Empty, id + ": a zero-command winning log would make the window law vacuous");
            Assert.That(windows, Is.All.GreaterThanOrEqualTo(12), id + " — " + britt.Value);
        }

        [Test]
        public void Campaign_MechanicOrderAndBandTable_Pass()
        {
            var order = Shared.Value.CampaignVerdicts.Single(v => v.Value == "tag=CM-R06.2");
            Assert.That(order.Code, Is.EqualTo(CatMetro.Content.Validation.StageVerdictCode.Pass), order.Detail);
            var band = Shared.Value.CampaignVerdicts.Single(v => v.Value == "tag=CM-R09.3");
            Assert.That(band.Code, Is.EqualTo(CatMetro.Content.Validation.StageVerdictCode.Pass), band.Detail);
        }

        [Test]
        public void Campaign_CorpusCount_Is10Of60Pending()
        {
            var count = Shared.Value.CampaignVerdicts.Single(v => v.Value == "tag=CM-R09.1");
            Assert.That(count.Detail, Does.Contain("10/60"));
            Assert.That(count.Blocks, Is.False);
        }

        [Test]
        public void ShippedLevels_L001ThroughL005_AreUnchangedByThisBand()
        {
            foreach (var id in new[] { "L001", "L002", "L003", "L004", "L005" })
            {
                var path = Path.Combine(BandFixtures.RepoRoot(), "content", "levels", id + ".json");
                var bytes = File.ReadAllBytes(path);
                Assert.That(bytes.Length, Is.GreaterThan(0), id);
                // The set-equality + byte-identity belt against the staged copy is
                // tests/unity/editmode.test.sh (criterion 9); this asserts the source itself
                // parses to a stable shape (no accidental edit) via re-import round-trip.
                var r = LevelImporter.Import(bytes);
                Assert.That(r.Ok, Is.True, $"{id}: {r.Error}");
                Assert.That(r.Value.Dto.Id, Is.EqualTo(id));
            }
        }

        internal static (int wins, int losses, int pinned) ParseRetention(string value)
        {
            // "retention=<x> (wins=W losses=L pinned=P) windows=[...]"
            int wi = value.IndexOf("wins=", System.StringComparison.Ordinal) + 5;
            int li = value.IndexOf("losses=", System.StringComparison.Ordinal) + 7;
            int pi = value.IndexOf("pinned=", System.StringComparison.Ordinal) + 7;
            int w = ReadInt(value, wi);
            int l = ReadInt(value, li);
            int p = ReadInt(value, pi);
            return (w, l, p);
        }

        internal static int[] ParseWindows(string value)
        {
            int start = value.IndexOf("windows=[", System.StringComparison.Ordinal) + "windows=[".Length;
            int end = value.IndexOf(']', start);
            var inner = value.Substring(start, end - start);
            if (inner.Length == 0) return System.Array.Empty<int>();
            return inner.Split(',').Select(int.Parse).ToArray();
        }

        private static int ReadInt(string s, int start)
        {
            int end = start;
            while (end < s.Length && char.IsDigit(s[end])) end++;
            return int.Parse(s.Substring(start, end - start));
        }
    }

    // Criterion 6: the declared queue mechanic is provably alive on L007-L010 (SRC's simultaneous
    // 3-cat burst — every re-authored level's opening wave), with the authored L006 anchor as the
    // negative control (max depth 0 at J1 — the CONFLICT-1 evidence).
    [TestFixture]
    public class QueueLivenessTests
    {
        [TestCase("L007")]
        [TestCase("L008")]
        [TestCase("L009")]
        [TestCase("L010")]
        public void QueueMechanic_IsProvablyAlive_OnTheOptimalWinningLog(string id)
        {
            var imported = BandFixtures.Import(id);
            var solve = LevelSolver.Solve(imported.Graph, (ulong)imported.Dto.Seed, 2000000);
            Assert.That(solve.Verdict, Is.EqualTo(SolveVerdict.Solved), id);

            var depths = BandFixtures.MaxNodeQueueDepth(imported.Graph, (ulong)imported.Dto.Seed, solve.OptimalLog);
            int srcIdx = imported.IdMaps.Nodes.IndexOf("SRC");
            Assert.That(depths[srcIdx], Is.GreaterThanOrEqualTo(2),
                id + ": SRC (queueCapacity-declared) must carry >=2 queued cats on the optimal log — "
                + string.Join(",", depths));
        }

        [Test]
        public void NegativeControl_L006Anchor_QueueNeverHoldsACat()
        {
            var imported = BandFixtures.Import("L006");
            var solve = LevelSolver.Solve(imported.Graph, (ulong)imported.Dto.Seed, 2000000);
            Assert.That(solve.Verdict, Is.EqualTo(SolveVerdict.Solved));

            var depths = BandFixtures.MaxNodeQueueDepth(imported.Graph, (ulong)imported.Dto.Seed, solve.OptimalLog);
            int j1Idx = imported.IdMaps.Nodes.IndexOf("J1");
            Assert.That(depths[j1Idx], Is.EqualTo(0),
                "CONFLICT-1 evidence: the authored L006 anchor's J1 queueCapacity is decorative — "
                + "this must be a real red test (able to fail), proving the assertion is not a tautology");
        }
    }

    // Criterion 7: each of L007-L010 has a reachable failure that is NOT the pinned halt, driven
    // by a committed witness command log (<=6 entries) through ReplayHasher.RunToEnd, throwing
    // nothing. L006 is exempt (CONFLICT-1 option A, F-DEV-3 class — Win or the pinned Halt only).
    //
    // CM-C13 re-author: L007-L010 no longer share one topology, so each witness names its own
    // switch index and toggle count (BandFixtures.TrapWitness) rather than one hardcoded
    // GateToHoldWitness(). L007/L009/L010 each have a single 3-route switch (index 0) whose last
    // route (index 2) is the dead-end TRAP node — two toggles from the authored initialRoute (0)
    // reach it. L008 has two switches: S1 (index 0, GATE) toggled once sends every cat toward the
    // J2 wing; S2 (index 1, the berth switch) is left at its own authored initialRoute (1 — the
    // TRAP route), so the single S1 toggle alone is enough to divert everything into the trap.
    [TestFixture]
    public class ReachableFailureTests
    {
        [TestCase("L007", 0, 2)]
        [TestCase("L009", 0, 2)]
        [TestCase("L010", 0, 2)]
        public void PermanentTrapDiversion_OverflowsTheDeadEndNode(string id, int switchIndex, int toggles)
        {
            var imported = BandFixtures.Import(id);
            SimulationState end = null;
            Assert.DoesNotThrow(() =>
                end = ReplayHasher.RunToEnd(imported.Graph, (ulong)imported.Dto.Seed,
                    BandFixtures.TrapWitness(switchIndex, toggles)),
                id + ": the witness must never mismatch a cat at a real station");
            Assert.That(end.Outcome.Kind, Is.EqualTo(OutcomeKind.Failed), id);
            Assert.That(end.Outcome.Reason, Is.EqualTo(FailReason.QueueOverflow), id);
        }

        [Test]
        public void L008_GateDiversion_OverflowsTheBerthTrap_WithoutTouchingTheBerthSwitch()
        {
            var imported = BandFixtures.Import("L008");
            // S1 (GATE, index 0) toggled once at tick 0 sends every cat — regardless of colour —
            // down the J2 wing instead of the direct RED route. S2 (the berth switch, index 1) is
            // never touched, so it stays at its own authored initialRoute (1 == the TRAP route):
            // every cat that reaches J2 funnels straight into the dead end. This exercises BOTH
            // switches' un-set state at once — the level's own "the berth switch must be set
            // before the rush arrives" teaching point, inverted into a failure witness.
            SimulationState end = null;
            Assert.DoesNotThrow(() =>
                end = ReplayHasher.RunToEnd(imported.Graph, (ulong)imported.Dto.Seed,
                    BandFixtures.TrapWitness(0, 1)),
                "L008: the witness must never mismatch a cat at a real station");
            Assert.That(end.Outcome.Kind, Is.EqualTo(OutcomeKind.Failed));
            Assert.That(end.Outcome.Reason, Is.EqualTo(FailReason.QueueOverflow));
        }

        [Test]
        public void BandCoverage_AllFourReAuthoredLevels_ReachAGenuineQueueOverflow()
        {
            var reasons = new System.Collections.Generic.List<FailReason>();

            foreach (var (id, switchIndex, toggles) in new[] { ("L007", 0, 2), ("L009", 0, 2), ("L010", 0, 2) })
            {
                var imported = BandFixtures.Import(id);
                var end = ReplayHasher.RunToEnd(imported.Graph, (ulong)imported.Dto.Seed,
                    BandFixtures.TrapWitness(switchIndex, toggles));
                reasons.Add(end.Outcome.Reason);
            }
            {
                var imported = BandFixtures.Import("L008");
                var end = ReplayHasher.RunToEnd(imported.Graph, (ulong)imported.Dto.Seed,
                    BandFixtures.TrapWitness(0, 1));
                reasons.Add(end.Outcome.Reason);
            }

            Assert.That(reasons, Is.All.EqualTo(FailReason.QueueOverflow), string.Join(",", reasons));
        }
    }
}
