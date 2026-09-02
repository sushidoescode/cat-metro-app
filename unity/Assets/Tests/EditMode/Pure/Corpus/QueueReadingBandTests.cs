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

        public static string[] CampaignFiles() =>
            Directory.GetFiles(Path.Combine(RepoRoot(), "content", "levels"), "L*.json")
                .OrderBy(path => Path.GetFileName(path), System.StringComparer.Ordinal)
                .ToArray();

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

        // Wider than CM-C11's own L001-L010 check (criterion 1's spirit extended): discover the
        // authoritative campaign so this assertion grows with the ladder rather than silently
        // stopping at the formerly shipped seventeen levels.
        [Test]
        public void TeachingGoals_AreDistinctAcrossDiscoveredCampaign()
        {
            var goals = QueueBandFixtures.CampaignFiles().Select(path =>
            {
                var imported = LevelImporter.Import(File.ReadAllBytes(path));
                Assert.That(imported.Ok, Is.True, Path.GetFileName(path) + ": " + imported.Error);
                return imported.Value.Dto.Meta.TeachingGoal;
            }).ToList();
            Assert.That(goals, Is.Unique,
                "criterion 1: teachingGoal must be pairwise distinct across the discovered campaign");
        }

        [Test]
        public void CampaignFiles_AreContiguousUniqueAndByteIdenticalToStreamingAssets()
        {
            var authoritative = QueueBandFixtures.CampaignFiles();
            Assert.That(authoritative, Is.Not.Empty, "the authoritative campaign must not be empty");

            var ids = authoritative.Select(path =>
            {
                var filenameId = Path.GetFileNameWithoutExtension(path);
                var imported = LevelImporter.Import(File.ReadAllBytes(path));
                Assert.That(imported.Ok, Is.True, Path.GetFileName(path) + ": " + imported.Error);
                Assert.That(imported.Value.Dto.Id, Is.EqualTo(filenameId),
                    "the authored id must match its filename");
                return imported.Value.Dto.Id;
            }).ToArray();

            Assert.That(ids, Is.Unique, "campaign ids must not be duplicated");
            var expectedIds = Enumerable.Range(1, authoritative.Length)
                .Select(number => "L" + number.ToString("000"))
                .ToArray();
            Assert.That(ids, Is.EqualTo(expectedIds),
                "campaign ids must be contiguous from L001 with no gaps");

            var stagedDirectory = Path.Combine(QueueBandFixtures.RepoRoot(), "unity", "Assets",
                "StreamingAssets", "content", "levels");
            var staged = Directory.GetFiles(stagedDirectory, "L*.json")
                .OrderBy(path => Path.GetFileName(path), System.StringComparer.Ordinal)
                .ToArray();
            Assert.That(staged.Select(Path.GetFileName),
                Is.EqualTo(authoritative.Select(Path.GetFileName)),
                "StreamingAssets must contain exactly the authoritative campaign filenames");
            for (int i = 0; i < authoritative.Length; i++)
                CollectionAssert.AreEqual(File.ReadAllBytes(authoritative[i]), File.ReadAllBytes(staged[i]),
                    Path.GetFileName(authoritative[i]) + " differs from its staged copy");
        }
    }

    // Criteria 3/4/5-shaped: the corpus gate over the discovered shipped campaign.
    // Runs the real console validator in-process against the shipped bytes — same shape as
    // AlternationBandGateTests / CorpusAndReportTests.cs.
    // [Timeout] finding: the class-shared `Shared` Lazy<CorpusReport> pays its one-time campaign
    // solve cost on whichever test runs first. The former campaign measured 422s-519.8s in Unity
    // EditMode, so the 900000ms class limit preserves headroom as discovery adds ladder levels.
    // A class-level Timeout raises the wall-clock budget only — it asserts nothing new and
    // weakens no existing assertion.
    [TestFixture]
    [Timeout(900000)]
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
            var members = QueueBandFixtures.CampaignFiles()
                .Select(path => new CatMetro.Content.Validation.CorpusMember(
                    "content/levels/" + Path.GetFileName(path), File.ReadAllBytes(path), true))
                .ToArray();
            var request = new CatMetro.Content.Validation.ValidationRequest(
                schemaBytes, configResult.Value, "2026-08-01T00:00:00+00:00", members);
            return CatMetro.Content.Validation.CorpusValidator.Validate(request);
        });

        private static CatMetro.Content.Validation.LevelReport Level(string id) =>
            Shared.Value.Levels.Single(l => l.LevelId == id);

        [Test]
        public void DiscoveredCampaignCorpus_ExitsClean()
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
            // Tightened from Pass-or-Warn to Pass-only at the round-1 review's Minor-5 (propagating
            // the PR #75/CM-C13 review's Important-3 tightening, which this wrapper had missed):
            // all seven L011-L017 levels are Warn-free, so a reintroduced Warn (e.g. a decoy
            // station) must go red here, not slide through on the Warn-tolerant reading.
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
            var proof = Shared.Value.CampaignVerdicts.Single(v => v.Value == "tag=CM-LADDER-solve-proof");
            Assert.That(proof.Code, Is.EqualTo(CatMetro.Content.Validation.StageVerdictCode.Pass), proof.Detail);
        }

        [Test]
        public void Campaign_CorpusCount_MatchesDiscoveredCampaign()
        {
            var count = Shared.Value.CampaignVerdicts.Single(v => v.Value == "tag=CM-R09.1");
            Assert.That(count.Detail, Does.Contain(QueueBandFixtures.CampaignFiles().Length + "/60"));
            Assert.That(count.Blocks, Is.False);
        }

        [Test]
        public void ShippedLevels_L001ThroughL010_StillParseToTheirDeclaredIds()
        {
            // Renamed at the round-1 review's Minor-6: the ORIGINAL name claimed byte-unchanged,
            // which this test cannot detect and cannot fail for (live proof: PR #75/CM-C13
            // rewrote L007-L010 wholesale inside this very tip and this assertion still passes,
            // since re-import round-tripping to the same declared id says nothing about content
            // drift). The real byte-unchanged guard is tests/corpus/queue-reading-band.test.sh's
            // git-diff check against L001-L010; this test only proves the sibling band's ten
            // files still parse (schema-valid, non-empty) and still carry the id their filename
            // promises — a much narrower, honestly-named claim.
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

    // Criterion 6-shaped positive evidence, narrowed at the round-1 review's Important-1/-2 (the
    // CONTRACT-AUTHOR RULING: "covered" means EXERCISED, not just structurally declared): SRC's
    // own "queue as buffer" behavior is provably exercised by the solver-optimal winning log for
    // L011, L012 and L013 — a real multi-cat backlog forms AT THE SOURCE, not just a decorative
    // queueCapacity declaration there. This does NOT prove the downstream chain nodes (L012's
    // Q1/Q2, L013's PLAT) ever buffer — the round-1 reviewer's own probe showed they don't, and a
    // from-first-principles trace of Simulation.cs confirms why: step 4a releases at most one
    // queued head per node per tick, so any single incoming edge feeds a downstream node at most
    // 1 arrival/tick, which that node's own 1-per-tick release always keeps pace with. Multi-edge
    // convergence CAN produce depth >= 2 at a non-source node — the round-2 reviewer's
    // two-converging-edge counter-example reaches it (per-tick trace in the #76 review record),
    // refuting the ">= 3 converging edges" lemma this comment previously claimed (corrected here
    // per #76 Minor-10) — but never on the line these observables sample: on a single-source
    // board, inflow is mouth-capped at 1 cat/tick upstream, so a parallel converging route can
    // only add delay, never throughput (the counter-example's converging line wins at tick 38 vs
    // the solver optimum 35, necessarily). Since liveness here samples the SOLVER-OPTIMAL log,
    // downstream buffering can never appear on it on any single-source board — do not re-attempt
    // convergence experiments against these observables; that exact experiment already blew the
    // work meter below. A single `sources` entry is enforced (LevelImporter.cs,
    // "second source is pinned out of CM-C1 scope"), so multi-edge convergence into one node is
    // achievable only via an extra switch — attempted for L012 this round (a 3-route SPLIT switch
    // feeding Q1 via three travelTicks-staggered edges, engineered to converge three solo cats on
    // one tick): schema-valid and statically clean, but the solver returned
    // NotFound(Budget, width=1000) at nodes=77344 even at a non-exact beam width, empirically
    // confirming the design report's own pre-existing budget-risk finding for added decision
    // surface in this band. Reverted rather than shipped broken. See the PR body's mechanics
    // coverage map for the corrected, honest per-level claims (L012/L013/L014/L015).
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
