using System.Collections.Generic;
using CatMetro.Content;
using CatMetro.Domain;

namespace CatMetro.Application.Session
{
    // CM-C2b: the engine-free run driver (overview.md §3). Presentation NEVER simulates — it
    // hands real elapsed milliseconds in and reads snapshots + Alpha out. Command scheduling is
    // the CM-C1 law: an enqueue stamps the CURRENT tick; Step at stepTick T applies entries
    // stamped T-1, so every command lands on the NEXT tick boundary and the first step is
    // uncommandable. PrevTrains is a value-copy of the slot array taken before each Step — the
    // interpolation base (TrainSlot is a struct; Clone() copies values).
    public sealed class GameSession
    {
        private readonly TickInterpolator _clock = new TickInterpolator();
        private readonly List<ToggleSwitchCommand> _due = new List<ToggleSwitchCommand>();

        public GameSession(ImportedLevel level)
        {
            if (level == null) throw new System.ArgumentException("level is required");
            Level = level;
            State = SimulationState.CreateInitial(level.Graph, (ulong)level.Dto.Seed);
            PrevTrains = (TrainSlot[])State.Trains.Clone();
            Log = new CommandLog();
        }

        public ImportedLevel Level { get; }
        public SimulationState State { get; }
        public CommandLog Log { get; }
        public TrainSlot[] PrevTrains { get; private set; }
        public double Alpha => _clock.Alpha;
        // Read-only HUD snapshot. During play it counts every accepted committed tap immediately;
        // after the run ends it freezes at the number the simulation actually applied.
        public FlipBudgetStatus FlipStatus => FlipBudget.Evaluate(
            Level.Graph.PerfectMaxSwitches,
            State.Outcome.Kind == OutcomeKind.Running ? Log.Entries.Count : State.SwitchesUsed);

        // Returns true only when this player flip is accepted into the authoritative replay.
        // Rejected budget/cooldown taps never enter the log, so pending lever presentation and
        // FlipStatus cannot visually commit a command the simulation will ignore.
        public bool EnqueueToggle(int switchId)
        {
            if (State.Outcome.Kind != OutcomeKind.Running) return false;
            if (switchId < 0 || switchId >= State.SwitchRoutes.Length) return false;
            if (!FlipBudget.CanAccept(Level.Graph.PerfectMaxSwitches, Log.Entries.Count))
                return false;

            if (Level.Graph.SwitchCooldownTicks[switchId] > 0)
            {
                // A tap stamped now applies one processing tick later. Remaining==1 decays on
                // the intervening tick and is eligible at application; remaining>=2 is not.
                if (SwitchState.Cooldown(State.SwitchRoutes[switchId]) > 1) return false;
                if (PendingToggleCount(switchId) > 0) return false;
            }

            Log.Append(new ToggleSwitchCommand((ushort)switchId, State.Tick));
            return true;
        }

        // Toggles enqueued but not yet applied (the lever shows the COMMITTED route immediately;
        // the sim applies it at the next boundary — ux-flows S-02). Review round F1: a command
        // stamped X is applied by the Step that BEGINS at State.Tick == X+1, so "not yet
        // applied" is Tick >= State.Tick - 1 — the >= State.Tick form made every lever REVERT
        // for the full tick between stamp+1 and application ("the visual must not lie").
        public int PendingToggleCount(int switchId)
        {
            int n = 0;
            foreach (var e in Log.Entries)
                if (e.SwitchId == switchId && e.Tick >= State.Tick - 1) n++;
            return n;
        }

        public void AdvanceMs(double dtMs)
        {
            int steps = _clock.AdvanceMs(dtMs);
            for (int i = 0; i < steps && State.Outcome.Kind == OutcomeKind.Running; i++)
            {
                PrevTrains = (TrainSlot[])State.Trains.Clone();
                _due.Clear();
                foreach (var e in Log.Entries)
                    if (e.Tick == State.Tick - 1) _due.Add(e); // order-independent Due scan
                var state = State;
                Simulation.Step(ref state, _due.ToArray());
            }
        }
    }
}
