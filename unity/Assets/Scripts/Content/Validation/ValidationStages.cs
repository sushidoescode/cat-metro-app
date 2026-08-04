using System.Collections.Generic;
using CatMetro.Domain;
using CatMetro.Domain.Solver;

namespace CatMetro.Content.Validation
{
    // Stages 1-11, each a pure function over parsed inputs (bytes/DTO/graph/config) returning a
    // StageVerdict. All freezes are recorded in state/handoffs/CM-C5.md (A-C5-7..12); the tests
    // are the executable definitions.
    public static class SchemaStage
    {
        // Stage 1: hand-rolled interpreter over the actual level_schema.json bytes (no new
        // dependency — hard rule 2; the Newtonsoft schema package is separate and
        // licence-encumbered). Fails closed on any schema keyword outside the implemented subset.
        public static StageVerdict Check(byte[] schemaBytes, byte[] levelBytes)
        {
            throw new System.NotImplementedException("CM-C5");
        }
    }

    public static class StaticAnalysisStage
    {
        // Stage 2 (A-C5-8): colour-compatible reachability, orphan switches, junction spacing
        // >= 1.2 grid units (fail), switch-in-top-15% (warn only).
        public static StageVerdict Check(LevelDto dto)
        {
            throw new System.NotImplementedException("CM-C5");
        }
    }

    public static class LowerBoundStage
    {
        // Stage 3 (A-C5-9): min colour-compatible source->station path (Dijkstra over
        // travelTicks) x win.deliveries, vs timeLimitTicks + lowerBoundSlack (Q-R row).
        public static StageVerdict Check(LevelDto dto, ValidatorConfig config)
        {
            throw new System.NotImplementedException("CM-C5");
        }
    }

    public static class SolverStage
    {
        // Stage 4: CM-C4's Solve. Blocks ONLY on Unsolvable; NotFound(Beam|Budget) and
        // Indeterminate print their counts, non-blocking (Q-N; ADR-0008:117).
        public static StageVerdict Verdict(SolveResult solve)
        {
            throw new System.NotImplementedException("CM-C5");
        }
    }

    public static class TrivialityStage
    {
        // Stage 5: EvaluateLog(empty). A Solved zero-input run FAILS the level (CM-R12.2);
        // NotFound/Indeterminate pass the stage.
        public static StageVerdict Check(LevelGraph graph, ulong seed)
        {
            throw new System.NotImplementedException("CM-C5");
        }
    }

    public static class BrittlenessStage
    {
        // Stage 6 (A-C5-2 + A-C5-7): deterministic Pcg32 jitter retention >= 70%; per-entry
        // action-window measurement on the solver-optimal log; onboarding band uses 12-16.
        public static StageVerdict Check(LevelDto dto, LevelGraph graph, CommandLog winningLog, ValidatorConfig config)
        {
            throw new System.NotImplementedException("CM-C5");
        }
    }

    public static class StarCheckStage
    {
        // Stage 7: two < three, both >= 1 (blocking today). Then: starBandSlack row absent =>
        // UNCONFIGURED(starBandSlack); row present => PINNED(NEW-Q5), because the reachability
        // comparison needs the pinned scoring model either way.
        public static StageVerdict Check(LevelDto dto, ValidatorConfig config)
        {
            throw new System.NotImplementedException("CM-C5");
        }
    }

    public sealed class DifficultyAxes
    {
        public readonly int B;                    // nodes + edges + switches
        public readonly int PeakTrains80;         // E component 1
        public readonly double InterleaveEntropy; // E component 2 (bits)
        public readonly int C;                    // proxy MaxSimultaneousPendingDecisions
        public readonly double T;                 // SolverOptimalTicks / TimeLimitTicks
        public readonly int HQueueSlack;          // proxy MinQueueSlackAtPeak — PARTIAL(Q-J)
        public readonly int RWinnable;
        public readonly int RTried;

        public DifficultyAxes(int b, int peak, double entropy, int c, double t, int h, int rw, int rt)
        {
            B = b; PeakTrains80 = peak; InterleaveEntropy = entropy; C = c; T = t;
            HQueueSlack = h; RWinnable = rw; RTried = rt;
        }
    }

    public static class DifficultyStage
    {
        // Stage 8 (A-C5-10): raw axes always computed and printed; normalisation + the +-0.05
        // comparison run only when the axisBBandCaps row exists (Q-R). H prints PARTIAL(Q-J).
        public static DifficultyAxes ComputeAxes(LevelDto dto, SolveResult solve)
        {
            throw new System.NotImplementedException("CM-C5");
        }

        public static double WeightedSum(DifficultyAxes axes, BandCapsRow caps)
        {
            throw new System.NotImplementedException("CM-C5");
        }

        public static StageVerdict Check(LevelDto dto, SolveResult solve, ValidatorConfig config)
        {
            throw new System.NotImplementedException("CM-C5");
        }
    }

    public static class NoveltyStage
    {
        // Stage 9 (A-C5-11): fixed 13-component feature vector, Euclidean distance vs prior
        // campaign levels in play order; threshold row noveltyMinDistance (Q-R).
        public static double[] Vector(LevelDto dto)
        {
            throw new System.NotImplementedException("CM-C5");
        }

        public static double Distance(double[] a, double[] b)
        {
            throw new System.NotImplementedException("CM-C5");
        }

        public static StageVerdict Check(LevelDto dto, IReadOnlyList<LevelDto> priorCampaign, ValidatorConfig config)
        {
            throw new System.NotImplementedException("CM-C5");
        }
    }

    public static class StalenessStage
    {
        // Stage 10 (Q-O analyst default): absent key => STALE; ISO-8601 ordinal compare against
        // the host-supplied reference; NEVER blocks while Q-O is open (the report says so).
        public static StageVerdict Check(LevelDto dto, string referenceTimestamp)
        {
            throw new System.NotImplementedException("CM-C5");
        }
    }

    public sealed class PlaytestRow
    {
        public readonly string LevelId;
        public readonly string Band;
        public readonly bool Capstone;
        public readonly int RequiredTesters;

        public PlaytestRow(string levelId, string band, bool capstone, int requiredTesters)
        {
            LevelId = levelId; Band = band; Capstone = capstone; RequiredTesters = requiredTesters;
        }
    }

    public static class PlaytestStage
    {
        // Stage 11: checklist artifact row; HUMAN-VERIFIED (pending); depends on D-6; never blocks.
        public static (PlaytestRow row, StageVerdict verdict) Row(LevelDto dto)
        {
            throw new System.NotImplementedException("CM-C5");
        }
    }
}
