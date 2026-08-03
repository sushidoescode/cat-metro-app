using System;

namespace CatMetro.Domain
{
    // Train slot layout inside the digest (A-C1-10): Id:2 Color:1 EdgeId:2 ProgressTicks:2
    // NodeId:2 State:1 = 10 bytes. Id is 1-based; a zeroed slot (Id 0, State None) is empty.
    // NodeId = the node most recently touched (source node at spawn). A delivered train's slot
    // is zeroed.
    public struct TrainSlot
    {
        public short Id;
        public byte Color;
        public short EdgeId;
        public short ProgressTicks;
        public short NodeId;
        public byte State; // TrainState
    }

    public static class TrainState
    {
        public const byte None = 0;
        public const byte OnEdge = 1;
        public const byte AtNode = 2;
    }

    // ADR-0002 §3: integer only. ADR-0002 §7 + overview.md §6: the digest layout below IS the
    // contract — reordering fields changes every golden.
    public sealed class SimulationState
    {
        public int Tick;
        public int Score;        // present, stays 0 in CM-C1 (scoring pinned, Q-C)
        public int Chain;        // present, stays 0 in CM-C1
        public int Deliveries;
        public int Rejections;   // stays 0 in CM-C1 (rejection pinned, NEW-Q4)
        public int Overloads;
        public int SwitchesUsed;
        public Pcg32 Rng;
        public byte[] SwitchRoutes;        // index = switchId
        public byte[] NodeQueueCounts;     // live count per node
        public short[][] NodeQueueSlots;   // per node: QCapBound slots, unused written 0
        public short[] OverloadTimers;     // per node; 16 ticks
        public TrainSlot[] Trains;         // fixed TrainsMax slots
        public SimOutcome Outcome;

        public LevelGraph Graph;           // not part of the digest; the immutable board

        // Rng stream selector for the level seed; constant so (levelId, seed, commandLog) stays
        // the whole determinism input set (ADR-0002 §8).
        public const ulong RngSequence = 54;

        public static SimulationState CreateInitial(LevelGraph graph, ulong seed)
        {
            var s = new SimulationState
            {
                Graph = graph,
                Rng = new Pcg32(seed, RngSequence),
                SwitchRoutes = new byte[graph.SwitchRoutes.Length],
                NodeQueueCounts = new byte[graph.NodeCount],
                NodeQueueSlots = new short[graph.NodeCount][],
                OverloadTimers = new short[graph.NodeCount],
                Trains = new TrainSlot[graph.TrainsMax],
                Outcome = SimOutcome.Running,
            };
            for (int i = 0; i < graph.NodeCount; i++) s.NodeQueueSlots[i] = new short[graph.QCapBound];
            for (int i = 0; i < s.SwitchRoutes.Length; i++) s.SwitchRoutes[i] = graph.SwitchInitialRoute[i];
            return s;
        }

        // Contract criterion 8: DigestLength is a pure function of the level shape.
        public static int DigestLength(int nSwitches, int nNodes, int nTrainsMax, int qCap)
            => 46 + nSwitches + nNodes * (3 + 2 * qCap) + 10 * nTrainsMax;

        public int DigestLength() => DigestLength(SwitchRoutes.Length, Graph.NodeCount, Graph.TrainsMax, Graph.QCapBound);

        // Canonical little-endian fixed-layout byte image (ADR-0002 §7; overview.md:312-320).
        public void WriteDigest(Span<byte> destination)
        {
            throw new NotImplementedException("CM-C1: WriteDigest not implemented yet (TDD red)");
        }
    }
}
