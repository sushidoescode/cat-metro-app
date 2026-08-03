using System;
using System.Collections.Generic;
using System.Security.Cryptography;

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
            using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var state = SimulationState.CreateInitial(graph, seed);
                var digest = new byte[state.DigestLength()];
                RunLoop(ref state, log, s =>
                {
                    s.WriteDigest(digest);
                    sha.AppendData(digest);
                });
                return ToLowerHex(sha.GetHashAndReset());
            }
        }

        // Runs the simulation from tick 0 until a terminal outcome (bounded by the level's time
        // limit), applying each log entry at step 1 of tick (entry.Tick + 1). Returns the final state.
        public static SimulationState RunToEnd(LevelGraph graph, ulong seed, CommandLog log, Action<SimulationState> afterEachTick = null)
        {
            var state = SimulationState.CreateInitial(graph, seed);
            RunLoop(ref state, log, afterEachTick);
            return state;
        }

        private static void RunLoop(ref SimulationState state, CommandLog log, Action<SimulationState> afterEachTick)
        {
            int safety = state.Graph.TimeLimitTicks + 2;
            while (state.Outcome.Kind == OutcomeKind.Running && safety-- > 0)
            {
                Simulation.Step(ref state, Due(log, state.Tick));
                afterEachTick?.Invoke(state);
            }
        }

        private static ReadOnlySpan<ToggleSwitchCommand> Due(CommandLog log, int entryTick)
        {
            if (log == null || log.Entries.Count == 0) return ReadOnlySpan<ToggleSwitchCommand>.Empty;
            List<ToggleSwitchCommand> due = null;
            foreach (var e in log.Entries)
                if (e.Tick == entryTick - 1)
                    (due ?? (due = new List<ToggleSwitchCommand>())).Add(e);
            return due == null ? ReadOnlySpan<ToggleSwitchCommand>.Empty : due.ToArray();
        }

        private static string ToLowerHex(byte[] bytes)
        {
            var chars = new char[bytes.Length * 2];
            const string hex = "0123456789abcdef";
            for (int i = 0; i < bytes.Length; i++)
            {
                chars[2 * i] = hex[bytes[i] >> 4];
                chars[2 * i + 1] = hex[bytes[i] & 0xF];
            }
            return new string(chars);
        }
    }
}
