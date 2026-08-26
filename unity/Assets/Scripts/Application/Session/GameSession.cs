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

        // ---- FLIP BUDGET: the read-only surface the HUD binds to -----------------------------
        // The HUD lane owns WavePreviewStrip.cs and is not touched by this lane. These five
        // members are the whole contract. Nothing here mutates the run.
        //
        // Bind the COMMITTED variants, not the applied ones. A tap stamps a command that Step
        // does not apply until the next tick boundary, so `State.SwitchesUsed` lags the player's
        // finger by up to one tick. The lever art already solves this the same way via
        // PendingToggleCount — "the visual must not lie" (see the note above that method).
        // Painting FlipsApplied would make the counter visibly stutter on every tap.

        /// <summary>Authored par for this level, or FlipBudget.Unbudgeted (-1) if none.</summary>
        public int FlipPar => Level.Graph.PerfectMaxSwitches;

        /// <summary>Flips the simulation has actually applied. Lags a tap by up to one tick.</summary>
        public int FlipsApplied => State.SwitchesUsed;

        /// <summary>Flips applied PLUS taps already committed but not yet stepped. Paint this one.</summary>
        public int FlipsCommitted
        {
            get
            {
                int pending = 0;
                foreach (var e in Log.Entries)
                    if (e.Tick >= State.Tick - 1) pending++;
                return State.SwitchesUsed + pending;
            }
        }

        /// <summary>Budget snapshot over <see cref="FlipsCommitted"/> — par, remaining, tier, stars.</summary>
        public FlipBudgetStatus FlipStatus => FlipBudget.Evaluate(FlipPar, FlipsCommitted);

        /// <summary>Stars earned as things stand: 3/2/1 on a win by flip tier, 0 otherwise.</summary>
        public int FlipStars => FlipBudget.StarsFor(FlipStatus.Tier, State.Outcome.Kind == OutcomeKind.Won);

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
