using System;

namespace CatMetro.Domain
{
    // Colors are bytes in the digest. Wild is train-side state: it remains Wild in transit and
    // receives its universal acceptance semantics only at station step 5 (NEW-Q35 / CM-C14a).
    public static class CatColor
    {
        public const byte None = 0;
        public const byte Red = 1;
        public const byte Blue = 2;
        public const byte Yellow = 3;
        public const byte Green = 4;
        public const byte Wild = 5;
    }

    // A-C1-1: the Domain owns its own integer board type; CatMetro.Content maps DTO -> LevelGraph
    // in CM-C2. Fixtures construct this directly in test code (A-C1-2).
    // Arrays are indexed by dense integer ids; the construction order of nodes/edges/switches is
    // part of the digest contract (A-C1-10).
    public sealed class LevelGraph
    {
        public readonly string LevelId;
        public readonly int NodeCount;
        public readonly int[] NodeQueueCapacity;    // per node, 1..QCapBound (0 = no queue limit checks off)
        public readonly int[] EdgeFrom;             // per edge
        public readonly int[] EdgeTo;               // per edge
        public readonly int[] EdgeTravelTicks;      // per edge
        public readonly int SourceNode;             // legacy first-source view for one-source callers
        public readonly int[] SourceNodes;           // every authored source, in authored order
        public readonly int[][] SwitchRoutes;       // per switch: candidate outgoing edge ids (2-3)
        public readonly int[] SwitchNode;           // per switch: the junction node it sits on
        public readonly byte[] SwitchInitialRoute;  // per switch
        public readonly int[] StationNode;          // per station
        public readonly byte[][] StationAccepts;    // per station: accepted colors
        public readonly int[] StationCapacity;      // per station
        public readonly int[] WaveTick;             // per wave
        public readonly byte[] WaveColor;           // per wave
        public readonly int[] WaveCount;            // per wave
        public readonly int[] WaveSpacingTicks;     // per wave
        public readonly int[] WaveSourceNode;       // per wave: authored source node
        public readonly int WinDeliveries;
        public readonly int TimeLimitTicks;
        public readonly int QCapBound;              // digest padding: queue slots per node (A-C1-7 i)
        public readonly int TrainsMax;              // digest padding: fixed train array bound (A-C1-7 ii)

        // Authored `win.perfectMaxSwitches` — par for the flip budget (see Domain/FlipBudget.cs).
        // FlipBudget.Unbudgeted (-1) means the level authored none. LevelGraph is explicitly NOT
        // part of the digest, so this costs no golden hash.
        public readonly int PerfectMaxSwitches;

        // What a non-matching arrival does at step 5 (see Domain/Misdelivery.cs). Defaults to the
        // CM-C1 pin so existing fixtures — and the solver's PinnedPruned accounting — are unchanged.
        public readonly MisdeliveryPolicy Misdelivery;

        public LevelGraph(
            string levelId,
            int nodeCount, int[] nodeQueueCapacity,
            int[] edgeFrom, int[] edgeTo, int[] edgeTravelTicks,
            int[] sourceNodes,
            int[][] switchRoutes, int[] switchNode, byte[] switchInitialRoute,
            int[] stationNode, byte[][] stationAccepts, int[] stationCapacity,
            int[] waveTick, byte[] waveColor, int[] waveCount, int[] waveSpacingTicks,
            int winDeliveries, int timeLimitTicks,
            int qCapBound, int trainsMax,
            int[] waveSourceNode = null,
            // Trailing + defaulted on purpose: every existing fixture keeps compiling untouched,
            // and a level that says nothing behaves exactly as it did before this lane.
            int perfectMaxSwitches = FlipBudget.Unbudgeted,
            MisdeliveryPolicy misdelivery = MisdeliveryPolicy.Pinned)
        {
            LevelId = levelId;
            NodeCount = nodeCount;
            NodeQueueCapacity = nodeQueueCapacity;
            EdgeFrom = edgeFrom;
            EdgeTo = edgeTo;
            EdgeTravelTicks = edgeTravelTicks;
            if (waveTick == null || waveColor == null || waveCount == null
                || waveSpacingTicks == null
                || waveTick.Length != waveColor.Length
                || waveTick.Length != waveCount.Length
                || waveTick.Length != waveSpacingTicks.Length)
                throw new ArgumentException("all wave arrays must have the same length");
            int waveLength = waveTick.Length;
            if (sourceNodes == null || sourceNodes.Length == 0)
                throw new ArgumentException("at least one source node is required", nameof(sourceNodes));
            for (int i = 0; i < sourceNodes.Length; i++)
                if (sourceNodes[i] < 0 || sourceNodes[i] >= nodeCount)
                    throw new ArgumentException($"source node {sourceNodes[i]} is outside the graph", nameof(sourceNodes));
            SourceNodes = sourceNodes;
            SourceNode = sourceNodes[0];

            if (waveSourceNode == null)
            {
                WaveSourceNode = new int[waveLength];
                for (int w = 0; w < WaveSourceNode.Length; w++) WaveSourceNode[w] = SourceNode;
            }
            else
            {
                if (waveSourceNode.Length != waveLength)
                    throw new ArgumentException(
                        "waveSourceNode length must equal the wave arrays", nameof(waveSourceNode));
                WaveSourceNode = waveSourceNode;
            }
            for (int w = 0; w < WaveSourceNode.Length; w++)
            {
                bool declared = false;
                for (int s = 0; s < SourceNodes.Length; s++)
                    if (WaveSourceNode[w] == SourceNodes[s]) { declared = true; break; }
                if (!declared)
                    throw new ArgumentException(
                        $"wave {w}: source node {WaveSourceNode[w]} is not declared", nameof(waveSourceNode));
            }
            for (int w = 0; w < waveLength; w++)
            {
                if (waveCount[w] > 1 && waveSpacingTicks[w] <= 0)
                    throw new ArgumentException(
                        $"wave {w}: spacingTicks must be positive when count > 1 — a zero spacing would silently emit nothing (review F9)");
            }
            SwitchRoutes = switchRoutes;
            SwitchNode = switchNode;
            SwitchInitialRoute = switchInitialRoute;
            StationNode = stationNode;
            StationAccepts = stationAccepts;
            StationCapacity = stationCapacity;
            WaveTick = waveTick;
            WaveColor = waveColor;
            WaveCount = waveCount;
            WaveSpacingTicks = waveSpacingTicks;
            WinDeliveries = winDeliveries;
            TimeLimitTicks = timeLimitTicks;
            QCapBound = qCapBound;
            TrainsMax = trainsMax;
            // Any negative par normalises to the single Unbudgeted sentinel so downstream code
            // has one "no budget" value to test, not a range.
            PerfectMaxSwitches = perfectMaxSwitches < 0 ? FlipBudget.Unbudgeted : perfectMaxSwitches;
            Misdelivery = misdelivery;
        }
    }
}
