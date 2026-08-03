using System;
using System.Collections.Generic;
using System.IO;
using CatMetro.Domain;

namespace CatMetro.Tests.Domain
{
    // A-C1-2: fixtures are constructed in code, not loaded from JSON (parsing is CM-C2's).
    // The L001 shape mirrors docs/plan/data/example_levels.json:4-17 field-for-field; its
    // construction order (nodes SRC,J1,RED,BLU; edges E1,E2,E3; switch S1) is part of the
    // golden's meaning (A-C1-10).
    public static class Fixtures
    {
        public const ulong L001Seed = 1001;

        public static LevelGraph L001Shape() => new LevelGraph(
            "L001",
            nodeCount: 4,
            nodeQueueCapacity: new[] { 8, 8, 8, 8 },
            edgeFrom: new[] { 0, 1, 1 },          // E1 SRC->J1, E2 J1->RED, E3 J1->BLU
            edgeTo: new[] { 1, 2, 3 },
            edgeTravelTicks: new[] { 10, 12, 12 },
            sourceNodes: new[] { 0 },
            switchRoutes: new[] { new[] { 1, 2 } },   // S1 routes [E2, E3]
            switchNode: new[] { 1 },
            switchInitialRoute: new byte[] { 1 },     // -> E3 (BLU) as authored
            stationNode: new[] { 2, 3 },
            stationAccepts: new[] { new[] { CatColor.Red }, new[] { CatColor.Blue } },
            stationCapacity: new[] { 6, 6 },
            waveTick: new[] { 8 },
            waveColor: new[] { CatColor.Red },
            waveCount: new[] { 2 },
            waveSpacingTicks: new[] { 20 },
            winDeliveries: 2,
            timeLimitTicks: 160,
            qCapBound: 8,
            trainsMax: 2);

        // The golden command log: toggle S1 during tick 12 -> applies at step 1 of tick 13 ->
        // route becomes E2 (RED) before the first cat reaches J1 at tick 18. Zero contention,
        // zero rejection, zero overload on this path (handoff §Golden fixture).
        public static CommandLog GoldenLog()
        {
            var log = new CommandLog();
            log.Append(new ToggleSwitchCommand(0, 12));
            return log;
        }

        // Criterion 8 shape 2: zero switches. DigestLength = 46 + 0 + 2*(3+2*4) + 10*1 = 78.
        public static LevelGraph ZeroSwitchShape() => new LevelGraph(
            "FX-Z", 2,
            new[] { 4, 4 },
            new[] { 0 }, new[] { 1 }, new[] { 6 },
            new[] { 0 },
            new int[0][], new int[0], new byte[0],
            new[] { 1 }, new[] { new[] { CatColor.Red } }, new[] { 6 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            1, 100, qCapBound: 4, trainsMax: 1);

        // Criterion 8 shape 3: two switches, multi-train. 46 + 2 + 6*(3+16) + 10*5 = 212.
        public static LevelGraph TwoSwitchShape() => new LevelGraph(
            "FX-2S", 6,
            new[] { 8, 8, 8, 8, 8, 8 },
            new[] { 0, 1, 1, 2, 2 }, new[] { 1, 2, 3, 4, 5 }, new[] { 5, 5, 5, 5, 5 },
            new[] { 0 },
            new[] { new[] { 1, 2 }, new[] { 3, 4 } },
            new[] { 1, 2 },
            new byte[] { 0, 0 },
            new[] { 3, 4, 5 },
            new[] { new[] { CatColor.Red }, new[] { CatColor.Blue }, new[] { CatColor.Green } },
            new[] { 6, 6, 6 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 5 }, new[] { 8 },
            5, 400, qCapBound: 8, trainsMax: 5);

        // No waves: nothing ever spawns, nothing can be delivered -> runs to the time limit.
        public static LevelGraph NoWaveShape(int timeLimitTicks = 200) => new LevelGraph(
            "FX-NW", 2,
            new[] { 8, 8 },
            new[] { 0 }, new[] { 1 }, new[] { 10 },
            new[] { 0 },
            new int[0][], new int[0], new byte[0],
            new[] { 1 }, new[] { new[] { CatColor.Red } }, new[] { 6 },
            new int[0], new byte[0], new int[0], new int[0],
            1, timeLimitTicks, qCapBound: 8, trainsMax: 1);

        // Overflow FAIL: source queue capacity 1. A double emission at tick 0 raises Overload
        // (count 1 == capacity); a steady one-per-tick wave then keeps the queue oscillating
        // between 1 and 2 (one in, one out per tick, entry blocked by the prog-0 mouth) so the
        // count never drops below capacity and never exceeds 2 — the 16-tick timer expires ->
        // Failed(QueueOverflow) (contract 13(f), A-C1-12).
        public static LevelGraph OverflowFailShape() => new LevelGraph(
            "FX-OF", 2,
            new[] { 1, 12 },
            new[] { 0 }, new[] { 1 }, new[] { 40 },
            new[] { 0 },
            new int[0][], new int[0], new byte[0],
            new[] { 1 }, new[] { new[] { CatColor.Red } }, new[] { 12 },
            new[] { 0, 0 }, new[] { CatColor.Red, CatColor.Red }, new[] { 17, 1 }, new[] { 1, 1 },
            999, 400, qCapBound: 8, trainsMax: 18);

        // Overflow CANCEL: same shape but only one queued train ever exists; it releases on the
        // next tick, the count drops below capacity, the timer clears, and the run completes Won.
        public static LevelGraph OverflowCancelShape() => new LevelGraph(
            "FX-OC", 2,
            new[] { 1, 12 },
            new[] { 0 }, new[] { 1 }, new[] { 40 },
            new[] { 0 },
            new int[0][], new int[0], new byte[0],
            new[] { 1 }, new[] { new[] { CatColor.Red } }, new[] { 12 },
            new[] { 0, 0 }, new[] { CatColor.Red, CatColor.Red }, new[] { 1, 1 }, new[] { 1, 1 },
            2, 400, qCapBound: 8, trainsMax: 2);

        // Guard fixture: the only station rejects the only color -> first arrival must hit the
        // NEW-Q4 NotSupportedException guard (contract criterion 14), never invented semantics.
        public static LevelGraph MismatchShape() => new LevelGraph(
            "FX-MM", 2,
            new[] { 8, 8 },
            new[] { 0 }, new[] { 1 }, new[] { 5 },
            new[] { 0 },
            new int[0][], new int[0], new byte[0],
            new[] { 1 }, new[] { new[] { CatColor.Blue } }, new[] { 6 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            1, 100, qCapBound: 8, trainsMax: 1);

        // Steps the sim to the END of processing tick `lastTick` (i.e. `lastTick + 1` Step calls),
        // feeding each call the log entries due at its boundary: an entry enqueued during tick t
        // applies at step 1 of the call whose entry Tick == t + 1 (CM-R07.3; handoff timing note).
        public static SimulationState RunThroughTick(LevelGraph graph, ulong seed, CommandLog log, int lastTick,
            Action<SimulationState> afterEachTick = null)
        {
            var state = SimulationState.CreateInitial(graph, seed);
            while (state.Tick <= lastTick && state.Outcome.Kind == OutcomeKind.Running)
            {
                Simulation.Step(ref state, DueCommands(log, state.Tick));
                afterEachTick?.Invoke(state);
            }
            return state;
        }

        public static ReadOnlySpan<ToggleSwitchCommand> DueCommands(CommandLog log, int entryTick)
        {
            if (log == null) return ReadOnlySpan<ToggleSwitchCommand>.Empty;
            var due = new List<ToggleSwitchCommand>();
            foreach (var e in log.Entries)
                if (e.Tick == entryTick - 1)
                    due.Add(e);
            return due.ToArray();
        }

        // Walks up from the test assembly to the repo root (the directory holding dotnet/ and scripts/).
        public static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "dotnet"))
                    && Directory.Exists(Path.Combine(dir.FullName, "scripts"))
                    && File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new InvalidOperationException("repo root not found above " + AppContext.BaseDirectory);
        }
    }
}
