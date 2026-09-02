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
        // A refused cat keeps its incoming EdgeId while ProgressTicks counts its eight-tick
        // platform dwell. Both new states fit the existing one-byte digest field; TrainSlot
        // remains exactly 10 bytes.
        public const byte RejectedAtStation = 3;
        public const byte OnEdgeReverse = 4;
    }

    // ADR-0002 §3: integer only. ADR-0002 §7 + overview.md §6: the digest layout below IS the
    // contract — reordering fields changes every golden.
    public sealed class SimulationState
    {
        public int Tick;
        public int Score;        // present, stays 0 in CM-C1 (scoring pinned, Q-C)
        public int Chain;        // present, stays 0 in CM-C1
        public int Deliveries;
        public int Rejections;   // wrong-colour station arrivals; repeated refusals each count
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

        public FlipBudgetStatus FlipStatus => FlipBudget.Evaluate(Graph.PerfectMaxSwitches, SwitchesUsed);

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
        // Field order and widths are the contract; the offset table lives in DigestTests.
        public void WriteDigest(Span<byte> destination)
        {
            int need = DigestLength();
            if (destination.Length < need)
                throw new ArgumentException($"digest needs {need} bytes, span has {destination.Length}");
            int o = 0;
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(o, 4), Tick); o += 4;
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(o, 4), Score); o += 4;
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(o, 4), Chain); o += 4;
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(o, 4), Deliveries); o += 4;
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(o, 4), Rejections); o += 4;
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(o, 4), Overloads); o += 4;
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(o, 4), SwitchesUsed); o += 4;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(o, 8), Rng.State); o += 8;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(o, 8), Rng.Inc); o += 8;
            for (int i = 0; i < SwitchRoutes.Length; i++) { destination[o] = SwitchRoutes[i]; o += 1; }
            for (int n = 0; n < Graph.NodeCount; n++)
            {
                destination[o] = NodeQueueCounts[n]; o += 1;
                var slots = NodeQueueSlots[n];
                for (int q = 0; q < Graph.QCapBound; q++)
                {
                    short v = q < slots.Length ? slots[q] : (short)0;
                    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(o, 2), v); o += 2;
                }
            }
            for (int n = 0; n < Graph.NodeCount; n++)
            {
                System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(o, 2), OverloadTimers[n]); o += 2;
            }
            for (int t = 0; t < Graph.TrainsMax; t++)
            {
                var tr = Trains[t];
                System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(o, 2), tr.Id); o += 2;
                destination[o] = tr.Color; o += 1;
                System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(o, 2), tr.EdgeId); o += 2;
                System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(o, 2), tr.ProgressTicks); o += 2;
                System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(o, 2), tr.NodeId); o += 2;
                destination[o] = tr.State; o += 1;
            }
            destination[o] = (byte)Outcome.Kind; o += 1;
            destination[o] = (byte)Outcome.Reason;
        }
    }
}
