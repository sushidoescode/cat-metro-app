using System.Collections.Generic;
using System.Linq;
using CatMetro.Domain.Solver;

namespace CatMetro.Content.Validation
{
    // One corpus member: raw level bytes plus its provenance. Campaign == lives under
    // content/levels/**; stress boards (docs/plan/data/stress_boards.json members) are
    // non-campaign (Q-P / A-C5-3): stages 1-8 + 10 + a stage-11 checklist row; stage 9 and the
    // campaign-order assertions print SKIPPED(non-campaign) for them.
    public sealed class CorpusMember
    {
        public readonly string SourceName;
        public readonly byte[] Bytes;
        public readonly bool IsCampaign;

        public CorpusMember(string sourceName, byte[] bytes, bool isCampaign)
        {
            SourceName = sourceName; Bytes = bytes; IsCampaign = isCampaign;
        }
    }

    public sealed class ValidationRequest
    {
        public readonly byte[] SchemaBytes;
        public readonly ValidatorConfig Config;
        public readonly string ReferenceTimestamp; // host-computed (A-C5-4); null == unavailable
        public readonly IReadOnlyList<CorpusMember> Members;
        public readonly int MaxNodesExpanded;      // CM-C4 criterion-11 injection pattern

        public ValidationRequest(byte[] schemaBytes, ValidatorConfig config,
            string referenceTimestamp, IReadOnlyList<CorpusMember> members,
            int maxNodesExpanded = SolverBounds.MAX_NODES_EXPANDED)
        {
            SchemaBytes = schemaBytes; Config = config;
            ReferenceTimestamp = referenceTimestamp; Members = members;
            MaxNodesExpanded = maxNodesExpanded;
        }
    }

    public sealed class LevelReport
    {
        public readonly string LevelId;          // schema id when parseable, else the source name
        public readonly string SourceName;
        public readonly bool IsCampaign;
        public readonly IReadOnlyList<StageVerdict> Verdicts;
        public readonly SolveResult Solve;       // null when stage 4 never ran
        public readonly DifficultyAxes Axes;     // null when stage 8 never ran
        public readonly PlaytestRow Checklist;   // null for import-failed members
        public readonly IReadOnlyList<double> NoveltyDistances;

        public LevelReport(string levelId, string sourceName, bool isCampaign,
            IReadOnlyList<StageVerdict> verdicts, SolveResult solve, DifficultyAxes axes,
            PlaytestRow checklist, IReadOnlyList<double> noveltyDistances)
        {
            LevelId = levelId; SourceName = sourceName; IsCampaign = isCampaign;
            Verdicts = verdicts; Solve = solve; Axes = axes; Checklist = checklist;
            NoveltyDistances = noveltyDistances;
        }
    }

    public sealed class CorpusReport
    {
        public readonly IReadOnlyList<LevelReport> Levels;
        public readonly IReadOnlyList<StageVerdict> CampaignVerdicts; // mechanic order, count row, band table
        public readonly bool ExitFailure;

        public CorpusReport(IReadOnlyList<LevelReport> levels,
            IReadOnlyList<StageVerdict> campaignVerdicts, bool exitFailure)
        {
            Levels = levels; CampaignVerdicts = campaignVerdicts; ExitFailure = exitFailure;
        }

        // Criterion 16: two output forms, one truth — both render THIS object.
        public string ToJson()
        {
            var root = new Newtonsoft.Json.Linq.JObject();
            var levels = new Newtonsoft.Json.Linq.JArray();
            foreach (var l in Levels)
            {
                var lo = new Newtonsoft.Json.Linq.JObject
                {
                    ["id"] = l.LevelId,
                    ["source"] = l.SourceName,
                    ["campaign"] = l.IsCampaign,
                };
                var stages = new Newtonsoft.Json.Linq.JArray();
                foreach (var v in l.Verdicts)
                    stages.Add(new Newtonsoft.Json.Linq.JObject
                    {
                        ["stage"] = v.Stage.ToString(),
                        ["code"] = v.Code.ToString(),
                        ["detail"] = v.Detail,
                        ["value"] = v.Value,
                        ["blocks"] = v.Blocks,
                    });
                lo["stages"] = stages;
                if (l.Solve != null)
                {
                    // CM-R19.1: the seconds figure is CompletionTicks / 8 (the tick rate); the
                    // 40-75 s range comparison is PINNED(NEW-Q1) — printed, never compared.
                    lo["solve"] = new Newtonsoft.Json.Linq.JObject
                    {
                        ["verdict"] = l.Solve.Verdict.ToString(),
                        ["completionTicks"] = l.Solve.CompletionTicks,
                        ["switchesUsed"] = l.Solve.SwitchesUsed,
                        ["beamWidthUsed"] = l.Solve.BeamWidthUsed,
                        ["pinnedPruned"] = l.Solve.PinnedPruned,
                        ["nodesExpanded"] = l.Solve.NodesExpanded,
                        ["seconds"] = l.Solve.CompletionTicks / 8.0,
                        ["secondsVerdict"] = "PINNED(NEW-Q1)",
                    };
                }
                if (l.Axes != null)
                {
                    lo["axes"] = new Newtonsoft.Json.Linq.JObject
                    {
                        ["B"] = l.Axes.B,
                        ["peakTrains80"] = l.Axes.PeakTrains80,
                        ["interleaveEntropy"] = l.Axes.InterleaveEntropy,
                        ["C"] = l.Axes.C,
                        ["T"] = l.Axes.T,
                        ["HQueueSlack"] = l.Axes.HQueueSlack,
                        ["HVerdict"] = "PARTIAL(Q-J)",
                        ["RWinnable"] = l.Axes.RWinnable,
                        ["RTried"] = l.Axes.RTried,
                    };
                }
                if (l.NoveltyDistances != null)
                    lo["noveltyDistances"] = new Newtonsoft.Json.Linq.JArray(l.NoveltyDistances);
                if (l.Checklist != null)
                    lo["checklist"] = new Newtonsoft.Json.Linq.JObject
                    {
                        ["levelId"] = l.Checklist.LevelId,
                        ["band"] = l.Checklist.Band,
                        ["capstone"] = l.Checklist.Capstone,
                        ["requiredTesters"] = l.Checklist.RequiredTesters,
                    };
                levels.Add(lo);
            }
            root["levels"] = levels;
            var campaign = new Newtonsoft.Json.Linq.JArray();
            foreach (var v in CampaignVerdicts)
                campaign.Add(new Newtonsoft.Json.Linq.JObject
                {
                    ["code"] = v.Code.ToString(),
                    ["detail"] = v.Detail,
                    ["blocks"] = v.Blocks,
                });
            root["campaign"] = campaign;
            root["exitFailure"] = ExitFailure;
            return root.ToString(Newtonsoft.Json.Formatting.Indented);
        }

        public string ToTable()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("level   stage                      verdict       detail");
            foreach (var l in Levels)
            {
                foreach (var v in l.Verdicts)
                {
                    sb.Append(l.LevelId.PadRight(8));
                    sb.Append(((int)v.Stage + "-" + v.Stage).PadRight(27));
                    sb.Append(v.Code.ToString().PadRight(14));
                    sb.Append(v.Detail);
                    if (v.Value.Length > 0) sb.Append("  [").Append(v.Value).Append(']');
                    sb.AppendLine();
                }
            }
            foreach (var v in CampaignVerdicts)
                sb.AppendLine("campaign  " + v.Code + "  " + v.Detail);
            sb.AppendLine(ExitFailure
                ? "RESULT: FAIL — a blocking stage failed"
                : "RESULT: OK — no blocking stage failed");
            return sb.ToString();
        }
    }

    public static class CorpusValidator
    {
        public static CorpusReport Validate(ValidationRequest request)
        {
            // Pass 1: schema + import for every member.
            var parsed = new List<(CorpusMember member, StageVerdict schema, ImportedLevel imported, string error)>();
            foreach (var m in request.Members)
            {
                var schemaVerdict = SchemaStage.Check(request.SchemaBytes, m.Bytes);
                var import = LevelImporter.Import(m.Bytes);
                parsed.Add(import.Ok
                    ? (m, schemaVerdict, import.Value, null)
                    : (m, schemaVerdict, null, import.Error.ToString()));
            }

            // Campaign play order = id order (handoff §Campaign-order assertions).
            var campaignDtos = parsed
                .Where(p => p.member.IsCampaign && p.imported != null)
                .Select(p => p.imported.Dto)
                .OrderBy(d => d.Id, System.StringComparer.Ordinal)
                .ToList();

            var reports = new List<LevelReport>();
            foreach (var (member, schemaVerdict, imported, error) in parsed)
            {
                var verdicts = new List<StageVerdict> { schemaVerdict };
                if (imported == null)
                {
                    verdicts.Add(StageVerdict.Fail(Stage.StaticAnalysis, "import failed: " + error));
                    for (var s = Stage.LowerBoundFeasibility; s <= Stage.HumanPlaytest; s++)
                        verdicts.Add(StageVerdict.Skipped(s, "import failed"));
                    reports.Add(new LevelReport(member.SourceName, member.SourceName,
                        member.IsCampaign, verdicts, null, null, null, null));
                    continue;
                }

                var dto = imported.Dto;
                var graph = imported.Graph;
                ulong seed = (ulong)dto.Seed;

                verdicts.Add(StaticAnalysisStage.Check(dto));
                verdicts.Add(LowerBoundStage.Check(dto, request.Config));

                var solve = LevelSolver.Solve(graph, seed, request.MaxNodesExpanded);
                verdicts.Add(SolverStage.Verdict(solve));
                verdicts.Add(TrivialityStage.Check(graph, seed));
                verdicts.Add(BrittlenessStage.Check(dto, graph,
                    solve.Verdict == SolveVerdict.Solved ? solve.OptimalLog : null, request.Config));
                verdicts.Add(StarCheckStage.Check(dto, request.Config));
                verdicts.Add(DifficultyStage.Check(dto, solve, request.Config));

                IReadOnlyList<double> distances = null;
                if (member.IsCampaign)
                {
                    var priors = campaignDtos
                        .Where(d => System.StringComparer.Ordinal.Compare(d.Id, dto.Id) < 0)
                        .ToList();
                    verdicts.Add(NoveltyStage.Check(dto, priors, request.Config));
                    var mine = NoveltyStage.Vector(dto);
                    distances = priors.Select(p => NoveltyStage.Distance(mine, NoveltyStage.Vector(p))).ToList();
                }
                else
                {
                    verdicts.Add(StageVerdict.Skipped(Stage.NoveltyCheck, "non-campaign"));
                }

                verdicts.Add(StalenessStage.Check(dto, request.ReferenceTimestamp));
                var (row, playtest) = PlaytestStage.Row(dto);
                verdicts.Add(playtest);

                var axes = solve.Verdict == SolveVerdict.Solved
                    ? DifficultyStage.ComputeAxes(dto, solve) : null;
                reports.Add(new LevelReport(dto.Id, member.SourceName, member.IsCampaign,
                    verdicts, solve, axes, row, distances));
            }

            var campaignVerdicts = CampaignAssertions(campaignDtos);
            bool exitFailure = reports.SelectMany(r => r.Verdicts).Any(v => v.Blocks)
                || campaignVerdicts.Any(v => v.Blocks);
            return new CorpusReport(reports, campaignVerdicts, exitFailure);
        }

        // The launch band table (product_spec.md §21, LOCKED): band -> (level range, target range).
        private static readonly (string band, int lo, int hi, double dLo, double dHi)[] BandTable =
        {
            ("onboarding", 1, 5, 0.05, 0.16),
            ("alternation", 6, 10, 0.18, 0.28),
            ("queue-reading", 11, 17, 0.28, 0.36),
            ("two-source", 18, 20, 0.38, 0.42),
            ("combo", 21, 25, 0.42, 0.48),
            ("multi-line", 26, 28, 0.48, 0.51),
            ("pressure", 29, 29, 0.53, 0.53),
            ("capstone", 30, 30, 0.55, 0.55),
        };

        private static List<StageVerdict> CampaignAssertions(List<LevelDto> campaign)
        {
            var result = new List<StageVerdict>();

            // CM-R06.2: one new mechanic at a time, in play order. Blocking.
            var seen = new HashSet<string>();
            var orderFails = new List<string>();
            foreach (var lvl in campaign)
            {
                var mechanics = new HashSet<string>(lvl.Meta.Mechanics.ToArray());
                var fresh = mechanics.Where(m => !seen.Contains(m)).ToList();
                string newM = lvl.Meta.NewMechanic;
                if (newM != null && !mechanics.Contains(newM))
                    orderFails.Add(lvl.Id + ": newMechanic '" + newM + "' not in its own mechanics list");
                var unexplained = fresh.Where(m => m != newM).ToList();
                if (unexplained.Count > 0)
                    orderFails.Add(lvl.Id + ": mechanics [" + string.Join(",", unexplained)
                        + "] appear without being the declared newMechanic");
                foreach (var m in mechanics) seen.Add(m);
            }
            result.Add(orderFails.Count > 0
                ? StageVerdict.Fail(Stage.NoveltyCheck,
                    "mechanic-order violation (CM-R06.2): " + string.Join("; ", orderFails))
                : new StageVerdict(Stage.NoveltyCheck, StageVerdictCode.Pass,
                    "mechanic order OK (CM-R06.2)", "", false));

            // CM-R09.1: 30 campaign levels. PENDING while the corpus grows; a hard count only at 30+.
            int n = campaign.Count;
            if (n < 30)
                result.Add(new StageVerdict(Stage.NoveltyCheck, StageVerdictCode.Pending,
                    "campaign corpus " + n + "/30 — PENDING until the corpus reaches 30 (CM-R09.1)",
                    "", false));
            else if (n == 30)
                result.Add(new StageVerdict(Stage.NoveltyCheck, StageVerdictCode.Pass,
                    "campaign corpus 30/30 (CM-R09.1)", "", false));
            else
                result.Add(StageVerdict.Fail(Stage.NoveltyCheck,
                    "campaign corpus " + n + "/30 exceeds the locked 30 (CM-R09.1)"));

            // CM-R09.3: band table conformance (id range + difficultyTarget range). Blocking.
            var bandFails = new List<string>();
            foreach (var lvl in campaign)
            {
                var row = BandTable.FirstOrDefault(b => b.band == lvl.Meta.Band);
                if (row.band == null)
                {
                    bandFails.Add(lvl.Id + ": band '" + lvl.Meta.Band + "' is not a campaign band");
                    continue;
                }
                if (int.TryParse(lvl.Id.Substring(1), out int num))
                {
                    if (num < row.lo || num > row.hi)
                        bandFails.Add(lvl.Id + ": outside band '" + row.band + "' range L"
                            + row.lo.ToString("000") + "-L" + row.hi.ToString("000"));
                }
                if (lvl.Meta.DifficultyTarget < row.dLo - 1e-9 || lvl.Meta.DifficultyTarget > row.dHi + 1e-9)
                    bandFails.Add(lvl.Id + ": difficultyTarget " + lvl.Meta.DifficultyTarget
                        + " outside band range [" + row.dLo + "," + row.dHi + "]");
            }
            result.Add(bandFails.Count > 0
                ? StageVerdict.Fail(Stage.NoveltyCheck,
                    "band-table violation (CM-R09.3): " + string.Join("; ", bandFails))
                : new StageVerdict(Stage.NoveltyCheck, StageVerdictCode.Pass,
                    "band table OK (CM-R09.3)", "", false));

            return result;
        }
    }
}
