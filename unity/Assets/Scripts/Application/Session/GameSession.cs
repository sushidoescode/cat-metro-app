using System.Collections.Generic;
using CatMetro.Content;
using CatMetro.Domain;

namespace CatMetro.Application.Session
{
    // A copied delivery identity for retained platform passengers. Simulation and replay
    // never consume these records; unlike a slot's latest node they survive render hitches.
    public readonly struct CatDelivery
    {
        public readonly int Slot, OccupantGeneration, Node;
        public readonly byte Colour;
        public CatDelivery(int slot, int occupantGeneration, int node, byte colour)
        {
            Slot = slot; OccupantGeneration = occupantGeneration; Node = node; Colour = colour;
        }
    }

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
        private readonly int[] _trainOccupantGenerations;
        private readonly int[] _trainOccupantSpawnNodes;
        private readonly int[] _trainOccupantSpawnEdges;
        private readonly int[] _trainDeliveryGenerations;
        private readonly int[] _trainDeliveryNodes;
        private readonly List<CatDelivery> _catDeliveries = new List<CatDelivery>();

        public GameSession(ImportedLevel level)
        {
            if (level == null) throw new System.ArgumentException("level is required");
            Level = level;
            State = SimulationState.CreateInitial(level.Graph, (ulong)level.Dto.Seed);
            PrevTrains = (TrainSlot[])State.Trains.Clone();
            _trainOccupantGenerations = new int[State.Trains.Length];
            _trainOccupantSpawnNodes = new int[State.Trains.Length];
            _trainOccupantSpawnEdges = new int[State.Trains.Length];
            _trainDeliveryGenerations = new int[State.Trains.Length];
            _trainDeliveryNodes = new int[State.Trains.Length];
            for (int t = 0; t < State.Trains.Length; t++)
            {
                _trainOccupantSpawnNodes[t] = -1;
                _trainOccupantSpawnEdges[t] = -1;
                _trainDeliveryNodes[t] = -1;
            }
            Log = new CommandLog();
        }

        public ImportedLevel Level { get; }
        public SimulationState State { get; }
        public CommandLog Log { get; }
        // Runner provenance only: rewind must not alter SimulationState or its canonical digest.
        public bool HasUsedRewind { get; private set; }
        public TrainSlot[] PrevTrains { get; private set; }
        public double Alpha => _clock.Alpha;
        // Read-only HUD snapshot. During play it counts every accepted committed tap immediately;
        // after the run ends it freezes at the number the simulation actually applied.
        public FlipBudgetStatus FlipStatus => FlipBudget.Evaluate(
            Level.Graph.PerfectMaxSwitches,
            State.Outcome.Kind == OutcomeKind.Running ? Log.Entries.Count : State.SwitchesUsed);

        /// <summary>
        /// Read-only presentation identity for a fixed simulation slot. It increments after an
        /// authoritative step changes that slot from empty to live, including refills hidden
        /// inside a multi-step render hitch. It is runner metadata only: Simulation never reads
        /// it and it is not part of the replay digest.
        /// </summary>
        public int TrainOccupantGeneration(int slotIndex) =>
            _trainOccupantGenerations[slotIndex];

        /// <summary>
        /// Exact source anchor observed when the current occupant entered this fixed slot.
        /// Presentation can reconstruct a source platform after a render hitch without
        /// inferring it from the train's later position.
        /// </summary>
        public int TrainOccupantSpawnNode(int slotIndex) =>
            _trainOccupantSpawnNodes[slotIndex];

        public int TrainOccupantSpawnEdge(int slotIndex) =>
            _trainOccupantSpawnEdges[slotIndex];

        /// <summary>
        /// Read-only runner metadata for presentation catch-up. Generation increments when the
        /// fixed slot delivers; node is the exact station endpoint observed at that step. Both
        /// stay outside SimulationState and are never read by Simulation.
        /// </summary>
        public int TrainDeliveryGeneration(int slotIndex) =>
            _trainDeliveryGenerations[slotIndex];

        public int TrainDeliveryNode(int slotIndex) => _trainDeliveryNodes[slotIndex];
        public int CatDeliveryCount => _catDeliveries.Count;
        public CatDelivery CatDeliveryAt(int index) => _catDeliveries[index];

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

        // Rebuilds the pre-decision state from the append-order command-log prefix. The receiver
        // remains untouched so callers can retain the failed run for local review.
        public bool TryCreateRewindBeforeLastDecision(out GameSession rewound)
        {
            rewound = null;
            int selectedIndex = Log.Entries.Count - 1;
            if (selectedIndex < 0) return false;

            var selected = Log.Entries[selectedIndex];
            var replay = new GameSession(Level);
            for (int i = 0; i < selectedIndex; i++)
                replay.Log.Append(Log.Entries[i]);

            // ADR-0002 section 9: rewind reaches prior state by fresh fixed-tick re-simulation,
            // never by snapshots or serialized SimulationState.
            while (replay.State.Outcome.Kind == OutcomeKind.Running
                && replay.State.Tick < selected.Tick)
                replay.AdvanceMs(TickInterpolator.TICK_MS);

            // A hostile/impossible log prefix cannot manufacture a rewind after terminal state.
            if (replay.State.Tick != selected.Tick) return false;

            replay.HasUsedRewind = true;
            rewound = replay;
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
                int deliveriesBeforeStep = State.Deliveries;
                var state = State;
                Simulation.Step(ref state, _due.ToArray());
                bool deliveryOccurred = State.Deliveries > deliveriesBeforeStep;
                for (int t = 0; t < State.Trains.Length; t++)
                {
                    if (!IsLive(PrevTrains[t]) && IsLive(State.Trains[t]))
                    {
                        _trainOccupantGenerations[t] = NextGeneration(
                            _trainOccupantGenerations[t]);
                        byte spawnedState = State.Trains[t].State;
                        bool spawnedOnEdge = spawnedState == TrainState.OnEdge
                            || spawnedState == TrainState.OnEdgeReverse;
                        int spawnEdge = spawnedOnEdge
                            ? State.Trains[t].EdgeId
                            : Simulation.SelectedOutgoingEdge(State, State.Trains[t].NodeId);
                        int spawnNode = spawnedState == TrainState.OnEdge
                            ? State.Graph.EdgeFrom[spawnEdge]
                            : spawnedState == TrainState.OnEdgeReverse
                                ? State.Graph.EdgeTo[spawnEdge]
                                : State.Trains[t].NodeId;
                        bool validSpawnEdge = spawnEdge >= 0
                            && spawnEdge < State.Graph.EdgeFrom.Length;
                        _trainOccupantSpawnEdges[t] = validSpawnEdge ? spawnEdge : -1;
                        _trainOccupantSpawnNodes[t] = spawnNode;
                    }
                    if (deliveryOccurred && IsLive(PrevTrains[t]) && !IsLive(State.Trains[t]))
                    {
                        _trainDeliveryGenerations[t] = NextGeneration(
                            _trainDeliveryGenerations[t]);
                        int edge = PrevTrains[t].EdgeId;
                        _trainDeliveryNodes[t] = edge >= 0 && edge < State.Graph.EdgeTo.Length
                            ? PrevTrains[t].State == TrainState.OnEdgeReverse
                                ? State.Graph.EdgeFrom[edge]
                                : State.Graph.EdgeTo[edge]
                            : PrevTrains[t].NodeId;
                        _catDeliveries.Add(new CatDelivery(t, _trainOccupantGenerations[t],
                            _trainDeliveryNodes[t], PrevTrains[t].Color));
                    }
                }
            }
        }

        private static bool IsLive(TrainSlot slot) =>
            slot.Id != 0 && slot.State != TrainState.None;

        private static int NextGeneration(int current)
        {
            int next = unchecked(current + 1);
            return next > 0 ? next : 1;
        }
    }
}
