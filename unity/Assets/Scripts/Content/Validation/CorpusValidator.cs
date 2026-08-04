using System.Collections.Generic;
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
            throw new System.NotImplementedException("CM-C5");
        }

        public string ToTable()
        {
            throw new System.NotImplementedException("CM-C5");
        }
    }

    public static class CorpusValidator
    {
        public static CorpusReport Validate(ValidationRequest request)
        {
            throw new System.NotImplementedException("CM-C5");
        }
    }
}
