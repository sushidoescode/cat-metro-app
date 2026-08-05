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

        public void EnqueueToggle(int switchId) =>
            Log.Append(new ToggleSwitchCommand((ushort)switchId, State.Tick));

        // Toggles enqueued but not yet applied (the lever shows the COMMITTED route immediately;
        // the sim applies it at the next boundary — ux-flows S-02).
        public int PendingToggleCount(int switchId)
        {
            int n = 0;
            foreach (var e in Log.Entries)
                if (e.SwitchId == switchId && e.Tick >= State.Tick) n++;
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
