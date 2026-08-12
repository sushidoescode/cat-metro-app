using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Domain.Solver;

namespace CatMetro.Tests.Corpus
{
    // CM-C12: L011-L017, the queue-reading band (product_spec.md band table row 523: L011-L017,
    // difficultyTarget 0.28-0.36, mechanics switch+queue, no new mechanic; per-level ladder row
    // 571-577). This is a NEW, independent fixture/test file — it never touches BandFixtures.cs
    // or AlternationBandTests.cs (both stay frozen per this contract's declared scope). Unlike
    // CM-C11's L006, none of these seven levels are byte-faithful anchors, so there is no
    // CONFLICT-1-style anchor-fidelity class here.
    internal static class QueueBandFixtures
    {
        public static readonly string[] Ids =
            { "L011", "L012", "L013", "L014", "L015", "L016", "L017" };

        public static string RepoRoot() => CatMetro.Tests.Domain.Fixtures.RepoRoot();

        public static byte[] Bytes(string id) =>
            File.ReadAllBytes(Path.Combine(RepoRoot(), "content", "levels", id + ".json"));

        public static ImportedLevel Import(string id)
        {
            var r = LevelImporter.Import(Bytes(id));
            Assert.That(r.Ok, Is.True, $"{id} must import: {r.Error}");
            return r.Value;
        }

        // Per-node max queue depth over a full replay of `log` — same sampling seam as
        // BandFixtures.MaxNodeQueueDepth (post-Step, A-DM-1), reimplemented locally so this file
        // has no compile-time dependency on the alternation-band fixture file.
        public static int[] MaxNodeQueueDepth(LevelGraph graph, ulong seed, CommandLog log)
        {
            var max = new int[graph.NodeCount];
            ReplayHasher.RunToEnd(graph, seed, log, s =>
            {
                for (int n = 0; n < s.NodeQueueCounts.Length; n++)
                    if (s.NodeQueueCounts[n] > max[n]) max[n] = s.NodeQueueCounts[n];
            });
            return max;
        }

        // Every one of the seven levels shares the same safety shape (proven by L007-L010):
        // switch index 0 (S1, always the first-declared switch, always the outer "GATE") routes
        // [real-continuation, HOLD] with initialRoute 0. One toggle of switch 0 at tick 0 —
        // applies at step 1 of tick 1, before any wave emits — permanently diverts every cat onto
        // the trap route. HOLD is never a station in any of the seven boards, so this witness can
        // never mismatch-deliver a cat (the ReplayHasher.RunToEnd throw guard CM-C11 documented).
        public static CommandLog GateToHoldWitness()
        {
            var log = new CommandLog();
            log.Append(new ToggleSwitchCommand(0, 0));
            return log;
        }

        // Local re-implementation of the stage-6 value string parse (ValidationStages.cs:507-508
        // format: "retention=<x> (wins=W losses=L pinned=P) windows=[...]") — kept independent of
        // AlternationBandGateTests's own internal helpers so this file has zero compile-time
        // coupling to the alternation-band fixture/test file.
        public static (int wins, int losses, int pinned) ParseRetention(string value)
        {
            int wi = value.IndexOf("wins=", System.StringComparison.Ordinal) + 5;
            int li = value.IndexOf("losses=", System.StringComparison.Ordinal) + 7;
            int pi = value.IndexOf("pinned=", System.StringComparison.Ordinal) + 7;
            int w = ReadInt(value, wi);
            int l = ReadInt(value, li);
            int p = ReadInt(value, pi);
            return (w, l, p);
        }

        public static int[] ParseWindows(string value)
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

    // Criterion-shaped like AlternationBandFieldTests: the authored progression row, asserted
    // twice (once off the parsed DTO, once off the raw JSON key walk so a parser bug cannot mask
    // a content bug).
    [TestFixture]
    public class QueueReadingBandFieldTests
    {
        private static JObject Raw(string id) =>
            JObject.Parse(System.Text.Encoding.UTF8.GetString(QueueBandFixtures.Bytes(id)));

        private static readonly (string Id, string Name, int Seed, double Diff)[] Locked =
        {
            ("L011", "Market Morning", 1011, 0.28),
            ("L012", "Stall Rows", 1012, 0.30),
            ("L013", "Fish Rush", 1013, 0.31),
            ("L014", "Cross Traffic", 1014, 0.32),
            ("L015", "Market Capstone", 1015, 0.34),
            ("L016", "Mirror Tracks", 1016, 0.35),
            ("L017", "Tight Headways", 1017, 0.36),
        };

        [TestCaseSource(nameof(Locked))]
        public void Dto_CarriesTheLockedProgressionRow((string Id, string Name, int Seed, double Diff) row)
        {
            var dto = QueueBandFixtures.Import(row.Id).Dto;
            Assert.That(dto.SchemaVersion, Is.EqualTo(2));
            Assert.That(dto.Id, Is.EqualTo(row.Id));
            Assert.That(dto.Seed, Is.EqualTo(row.Seed));
            Assert.That(dto.Name, Is.EqualTo(row.Name));
            Assert.That(dto.Meta.Band, Is.EqualTo("queue-reading"));
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
            Assert.That((string)j["meta"]["band"], Is.EqualTo("queue-reading"));
            Assert.That((double)j["meta"]["difficultyTarget"], Is.EqualTo(row.Diff).Within(1e-12));
            Assert.That(j["meta"]["mechanics"].Select(t => (string)t), Is.EqualTo(new[] { "switch", "queue" }));
            Assert.That(j["meta"]["newMechanic"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That((string)j["meta"]["authoredBy"], Is.EqualTo("llm+validator"));
            Assert.That((int)j["meta"]["minActionWindowTicks"], Is.EqualTo(12));
            Assert.That(((JObject)j["meta"]).Property("validatedAt"), Is.Null,
                "AMD-09 / ADR-0008:119-123: the key is deleted when unvalidated, never null");
        }

        [Test]
        public void MinActionWindowTicks_Is12_ForAllSeven()
        {
            foreach (var id in QueueBandFixtures.Ids)
            {
                var dto = QueueBandFixtures.Import(id).Dto;
                Assert.That(dto.Meta.MinActionWindowTicks, Is.EqualTo(12), id);
            }
        }

        // Wider than CM-C11's own L001-L010 check (criterion 1's spirit extended): this band
        // lands after both the onboarding and alternation bands, so the distinctness net widens
        // to all seventeen shipped campaign levels, not just this band's seven.
        [Test]
        public void TeachingGoals_AreDistinctAcrossAllSeventeenCampaignLevels()
        {
            var ids = new[]
            {
                "L001", "L002", "L003", "L004", "L005", "L006", "L007", "L008", "L009", "L010",
                "L011", "L012", "L013", "L014", "L015", "L016", "L017",
            };
            var goals = ids.Select(id =>
                LevelImporter.Import(File.ReadAllBytes(
                    Path.Combine(QueueBandFixtures.RepoRoot(), "content", "levels", id + ".json")))
                    .Value.Dto.Meta.TeachingGoal).ToList();
            Assert.That(goals, Is.Unique, "criterion 1: teachingGoal must be pairwise distinct across L001-L017");
        }
    }

    // Criteria 3/4/5-shaped: the corpus gate over L001-L017 (the whole shipped campaign to date).
    // Runs the real console validator in-process against the shipped bytes — same shape as
    // AlternationBandGateTests / CorpusAndReportTests.cs.
    // [Timeout] finding (this session's own Unity EditMode batch run, not present in the dotnet
    // leg): the class-shared `Shared` Lazy<CorpusReport> pays its one-time cost (17 BFS-exact
    // solves) on whichever test happens to run first — Unity's NUnit runs fixture methods
    // alphabetically, so that landed on Campaign_CorpusCount_Is17Of30Pending, which measured
    // 422s and exceeded NUnit's 180000ms default per-test timeout while every other test in this
    // class (reading the already-memoized value) passed in microseconds. AlternationBandGateTests
    // (L001-L010, 10 levels) stays under the default; this band's 17 levels do not. A class-level
    // Timeout raises the wall-clock budget only — it asserts nothing new and weakens no existing
    // assertion.
    [TestFixture]
    [Timeout(600000)]
    public class QueueReadingBandGateTests
    {
        private static readonly System.Lazy<CatMetro.Content.Validation.CorpusReport> Shared =
            new System.Lazy<CatMetro.Content.Validation.CorpusReport>(() =>
        {
            var schemaBytes = File.ReadAllBytes(
                Path.Combine(QueueBandFixtures.RepoRoot(), "docs", "plan", "data", "level_schema.json"));
            var configResult = CatMetro.Content.Validation.ValidatorConfig.Parse(File.ReadAllBytes(
                Path.Combine(QueueBandFixtures.RepoRoot(), "config", "validator_thresholds.json")));
            Assert.That(configResult.Ok, Is.True, $"{configResult.Error}");
            var ids = new[]
            {
                "L001", "L002", "L003", "L004", "L005", "L006", "L007", "L008", "L009", "L010",
                "L011", "L012", "L013", "L014", "L015", "L016", "L017",
            };
            var members = ids
                .Select(id => new CatMetro.Content.Validation.CorpusMember(
                    "content/levels/" + id + ".json", QueueBandFixtures.Bytes(id), true))
                .ToArray();
            var request = new CatMetro.Content.Validation.ValidationRequest(
                schemaBytes, configResult.Value, "2026-08-01T00:00:00+00:00", members);
            return CatMetro.Content.Validation.CorpusValidator.Validate(request);
        });

        private static CatMetro.Content.Validation.LevelReport Level(string id) =>
            Shared.Value.Levels.Single(l => l.LevelId == id);

        [Test]
        public void FullSeventeenLevelCorpus_ExitsClean()
        {
            Assert.That(Shared.Value.ExitFailure, Is.False,
                string.Join("\n", Shared.Value.Levels.SelectMany(l => l.Verdicts).Where(v => v.Blocks)
                    .Select(v => v.Stage + ": " + v.Detail)));
        }

        [TestCaseSource(typeof(QueueBandFixtures), nameof(QueueBandFixtures.Ids))]
        public void Level_PassesEveryBlockingStage(string id)
        {
            var l = Level(id);
            var schema = l.Verdicts.Single(v => v.Stage == CatMetro.Content.Validation.Stage.Schema);
            Assert.That(schema.Code, Is.EqualTo(CatMetro.Content.Validation.StageVerdictCode.Pass), id + " " + schema.Detail);

            var stat = l.Verdicts.Single(v => v.Stage == CatMetro.Content.Validation.Stage.StaticAnalysis);
            // Is.EqualTo(..).Or.EqualTo(..) rather than Is.AnyOf: Unity's bundled NUnit predates
            // AnyOf (CM-C11's compile-error finding, 2026-08-08 — the dotnet leg's newer NUnit
            // masks it, so this must be pinned here too even though it never fired locally).
            Assert.That(stat.Code, Is.EqualTo(CatMetro.Content.Validation.StageVerdictCode.Pass)
                .Or.EqualTo(CatMetro.Content.Validation.StageVerdictCode.Warn), id + " " + stat.Detail);

            Assert.That(l.Solve, Is.Not.Null, id + ": stage 4 never ran");
            Assert.That(l.Solve.Verdict, Is.EqualTo(CatMetro.Domain.Solver.SolveVerdict.Solved), id);
            Assert.That(l.Solve.BeamWidthUsed, Is.EqualTo(0), id + ": must be BFS-exact");

            var triv = l.Verdicts.Single(v => v.Stage == CatMetro.Content.Validation.Stage.TrivialityReject);
            Assert.That(triv.Code, Is.EqualTo(CatMetro.Content.Validation.StageVerdictCode.Pass), id + " " + triv.Detail);

            var britt = l.Verdicts.Single(v => v.Stage == CatMetro.Content.Validation.Stage.BrittlenessAccessibility);
            Assert.That(britt.Code, Is.EqualTo(CatMetro.Content.Validation.StageVerdictCode.Pass), id + " " + britt.Detail);
        }

        [Test]
        public void Campaign_LivenessSkipsEveryQueueReadingLevel_NoDeclaredNewMechanic()
        {
            foreach (var id in QueueBandFixtures.Ids)
            {
                var row = Shared.Value.CampaignVerdicts.Single(v =>
                    v.Value.StartsWith("tag=CM-R06.2-liveness:" + id));
                Assert.That(row.Detail, Is.EqualTo("SKIPPED(no declared newMechanic)"), id);
                Assert.That(row.Blocks, Is.False, id);
            }
        }

        // Criterion 4-shaped: both NEW-Q4 readings clear 70% for all seven levels, with no
        // anchor exemption — unlike L006, none of these seven is byte-locked to a pre-fix
        // authored source, so there is nothing here for the fixed centering tie-break to leave
        // below the bar. Parsed from the stage-6 value string
        // `retention=<x> (wins=W losses=L pinned=P) windows=[...]` (ValidationStages.cs:507-508).
        [TestCaseSource(typeof(QueueBandFixtures), nameof(QueueBandFixtures.Ids))]
        public void Level_RetentionHolds_UnderBothNEWQ4Readings(string id)
        {
            var britt = Level(id).Verdicts.Single(v =>
                v.Stage == CatMetro.Content.Validation.Stage.BrittlenessAccessibility);
            var (w, l, p) = QueueBandFixtures.ParseRetention(britt.Value);
            int denom = w + l;
            Assert.That(denom, Is.GreaterThan(0), id + ": all-pinned sample set, cannot read retention — " + britt.Value);
            int optimistic = w * 100 / denom;
            Assert.That(optimistic, Is.GreaterThanOrEqualTo(70),
                id + " optimistic reading " + optimistic + "% — " + britt.Value);
            int pessimistic = w * 100 / (w + l + p);
            Assert.That(pessimistic, Is.GreaterThanOrEqualTo(70),
                id + " pessimistic reading " + pessimistic + "% — " + britt.Value);
        }

        // Criterion 5-shaped: minActionWindowTicks == 12 asserted (field tests above) AND every
        // window in the stage-6 value is >= 12, with a non-empty array (the vacuous-law guard).
        [TestCaseSource(typeof(QueueBandFixtures), nameof(QueueBandFixtures.Ids))]
        public void Level_ActionWindows_AreFlooredAndNonEmpty(string id)
        {
            var britt = Level(id).Verdicts.Single(v =>
                v.Stage == CatMetro.Content.Validation.Stage.BrittlenessAccessibility);
            var windows = QueueBandFixtures.ParseWindows(britt.Value);
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
        public void Campaign_CorpusCount_Is17Of30Pending()
        {
            var count = Shared.Value.CampaignVerdicts.Single(v => v.Value == "tag=CM-R09.1");
            Assert.That(count.Detail, Does.Contain("17/30"));
            Assert.That(count.Blocks, Is.False);
        }

        [Test]
        public void ShippedLevels_L001ThroughL010_AreUnchangedByThisBand()
        {
            foreach (var id in new[]
                     { "L001", "L002", "L003", "L004", "L005", "L006", "L007", "L008", "L009", "L010" })
            {
                var path = Path.Combine(QueueBandFixtures.RepoRoot(), "content", "levels", id + ".json");
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
    }

    // Criterion 6-shaped positive evidence: "queue as buffer" (L011), "chained queues" (L012)
    // and "burst wave" (L013) are provably exercised by the solver-optimal winning log — a real
    // multi-cat backlog forms at the buffering node(s), not just a decorative queueCapacity
    // declaration. This band has no CONFLICT-1-style byte-locked anchor, so there is no negative
    // control analogous to L006's "queue never holds a cat" case.
    [TestFixture]
    public class QueueReadingLivenessTests
    {
        [TestCase("L011", "SRC")]
        [TestCase("L012", "SRC")]
        [TestCase("L013", "SRC")]
        public void QueueMechanic_IsProvablyAlive_OnTheOptimalWinningLog(string id, string bufferNodeId)
        {
            var imported = QueueBandFixtures.Import(id);
            var solve = LevelSolver.Solve(imported.Graph, (ulong)imported.Dto.Seed, 2000000);
            Assert.That(solve.Verdict, Is.EqualTo(SolveVerdict.Solved), id);

            var depths = QueueBandFixtures.MaxNodeQueueDepth(imported.Graph, (ulong)imported.Dto.Seed, solve.OptimalLog);
            int idx = imported.IdMaps.Nodes.IndexOf(bufferNodeId);
            Assert.That(depths[idx], Is.GreaterThanOrEqualTo(2),
                id + ": " + bufferNodeId + " (queueCapacity-declared) must carry >=2 queued cats on the "
                + "optimal log — " + string.Join(",", depths));
        }
    }

    // Criterion 7-shaped: each of the seven levels has a reachable failure that is NOT the
    // solver's own Won path, driven by a committed <=1-entry witness command log through
    // ReplayHasher.RunToEnd, throwing nothing. All seven share the same GATE/HOLD trap shape
    // (S1 = switch index 0 in every one of these seven files), so one shared witness covers the
    // whole band — unlike CM-C11's L006, none of these seven is exempt.
    [TestFixture]
    public class QueueReadingReachableFailureTests
    {
        [TestCase("L011")]
        [TestCase("L012")]
        [TestCase("L013")]
        [TestCase("L014")]
        [TestCase("L015")]
        [TestCase("L016")]
        [TestCase("L017")]
        public void GateDiversion_OverflowsTheHoldingNode(string id)
        {
            var imported = QueueBandFixtures.Import(id);
            SimulationState end = null;
            Assert.DoesNotThrow(() =>
                end = ReplayHasher.RunToEnd(imported.Graph, (ulong)imported.Dto.Seed,
                    QueueBandFixtures.GateToHoldWitness()),
                id + ": the witness must never mismatch a cat at a real station");
            Assert.That(end.Outcome.Kind, Is.EqualTo(OutcomeKind.Failed), id);
            Assert.That(end.Outcome.Reason, Is.EqualTo(FailReason.QueueOverflow), id);
        }
    }
}
