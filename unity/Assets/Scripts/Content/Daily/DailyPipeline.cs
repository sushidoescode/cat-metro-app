using System.Collections.Generic;
using CatMetro.Content.Validation;

namespace CatMetro.Content.Daily
{
    // CM-C6's orchestrator: given a date-key list (INPUTS — criterion 2), derive each date's
    // seed, run the bounded deterministic salt loop (criterion 4), validate every candidate
    // board through CM-C5's REAL stages (criterion 5: each candidate LevelDto is serialised
    // in-memory and fed to CorpusValidator as a single-member non-campaign corpus, so the two
    // jobs cannot disagree), apply the weekday ramp check (criterion 9), and emit the artifact
    // (criterion 6) — all with zero file or console access; the DailyTools host owns that.
    public sealed class DailyRunRequest
    {
        public readonly byte[] SchemaBytes;
        public readonly ValidatorConfig ValidatorConfig;
        public readonly DailyPipelineConfig PipelineConfig;
        public readonly byte[] WeekdayCurveBytes;       // null == file absent (NEW-Q21)
        public readonly IReadOnlyList<string> DateKeys; // criterion 2: the date list is an input
        public readonly IBoardFactory Factory;
        public readonly string ReferenceTimestamp;      // host-computed or null (CM-C5 A-C5-4)
        public readonly string BoardProvenance;         // artifact honesty: names the Q-S stub
        public readonly int MaxNodesExpanded;

        public DailyRunRequest(byte[] schemaBytes, ValidatorConfig validatorConfig,
            DailyPipelineConfig pipelineConfig, byte[] weekdayCurveBytes,
            IReadOnlyList<string> dateKeys, IBoardFactory factory,
            string referenceTimestamp, string boardProvenance,
            int maxNodesExpanded = CatMetro.Domain.Solver.SolverBounds.MAX_NODES_EXPANDED)
        {
            SchemaBytes = schemaBytes; ValidatorConfig = validatorConfig;
            PipelineConfig = pipelineConfig; WeekdayCurveBytes = weekdayCurveBytes;
            DateKeys = dateKeys; Factory = factory;
            ReferenceTimestamp = referenceTimestamp; BoardProvenance = boardProvenance;
            MaxNodesExpanded = maxNodesExpanded;
        }
    }

    public sealed class DailyDateRecord
    {
        public readonly string DateKey;
        public readonly int K;          // resolved salt, or the last attempted salt on failure
        public readonly uint Seed;      // seed at K
        public readonly string Verdict; // "Pass" | "Fail"
        public readonly string Detail;  // on Fail: names the stage and the reason (criterion 5)
        public readonly IReadOnlyList<StageVerdict> StageVerdicts; // last attempt, CM-C5 order
        public readonly RampVerdict Ramp;
        public readonly int SolverCompletionTicks; // -1 when stage 4 did not solve
        public readonly bool Blocks;

        public DailyDateRecord(string dateKey, int k, uint seed, string verdict, string detail,
            IReadOnlyList<StageVerdict> stageVerdicts, RampVerdict ramp,
            int solverCompletionTicks, bool blocks)
        {
            DateKey = dateKey; K = k; Seed = seed; Verdict = verdict; Detail = detail;
            StageVerdicts = stageVerdicts; Ramp = ramp;
            SolverCompletionTicks = solverCompletionTicks; Blocks = blocks;
        }
    }

    public sealed class DailyRunReport
    {
        public readonly IReadOnlyList<DailyDateRecord> Records;
        public readonly string BoardProvenance;
        public readonly bool ExitFailure;

        public DailyRunReport(IReadOnlyList<DailyDateRecord> records, string boardProvenance,
            bool exitFailure)
        {
            Records = records; BoardProvenance = boardProvenance; ExitFailure = exitFailure;
        }

        // The artifact, serialised in-memory and handed to the host (criterion 6): one record per
        // date {dateKey, k, seed, verdict, stageVerdicts, solverCompletionTicks}.
        public string ToJson()
        {
            throw new System.NotImplementedException();
        }

        // One line per date, "DAILY_SEED <dateKey> <k> <seed>" (ADR-0009:35; CM-R43.8's truth
        // source) — single-sourced here so the host and the tests agree on the format.
        public IReadOnlyList<string> SeedLines()
        {
            throw new System.NotImplementedException();
        }
    }

    public static class DailyPipeline
    {
        public static ContentResult<DailyRunReport> Run(DailyRunRequest request)
        {
            throw new System.NotImplementedException();
        }
    }
}
