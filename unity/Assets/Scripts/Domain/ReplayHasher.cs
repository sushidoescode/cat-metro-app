using System;

namespace CatMetro.Domain
{
    // ADR-0002 §7: a run's replay hash is an incremental SHA-256 over the concatenation of the
    // per-tick digests (one digest appended after every Step call, from the first step to the
    // terminal tick), rendered as 64 lowercase hex chars. Computed only in test/validator paths.
    // The CommandLog ENVELOPE is outside the hash (criterion 10): only Entries influence the run.
    public static class ReplayHasher
    {
        public static string ComputeReplayHash(LevelGraph graph, ulong seed, CommandLog log)
        {
            throw new NotImplementedException("CM-C1: ComputeReplayHash not implemented yet (TDD red)");
        }

        // Runs the simulation from tick 0 until a terminal outcome (bounded by the level's time
        // limit), applying each log entry at step 1 of tick (entry.Tick + 1). Returns the final state.
        public static SimulationState RunToEnd(LevelGraph graph, ulong seed, CommandLog log, Action<SimulationState> afterEachTick = null)
        {
            throw new NotImplementedException("CM-C1: RunToEnd not implemented yet (TDD red)");
        }
    }
}
