using System;

namespace CatMetro.Domain
{
    // Colors are bytes in the digest. wild is a pinned mechanic (NEW-Q35): constructing a wave
    // with it throws, per contract criterion 14.
    public static class CatColor
    {
        public const byte None = 0;
        public const byte Red = 1;
        public const byte Blue = 2;
        public const byte Yellow = 3;
        public const byte Green = 4;
        public const byte Wild = 5; // reserved; construction-guarded
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
        public readonly int SourceNode;             // exactly one source (second source is pinned out)
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
        public readonly int WinDeliveries;
        public readonly int TimeLimitTicks;
        public readonly int QCapBound;              // digest padding: queue slots per node (A-C1-7 i)
        public readonly int TrainsMax;              // digest padding: fixed train array bound (A-C1-7 ii)

        public LevelGraph(
            string levelId,
            int nodeCount, int[] nodeQueueCapacity,
            int[] edgeFrom, int[] edgeTo, int[] edgeTravelTicks,
            int[] sourceNodes,
            int[][] switchRoutes, int[] switchNode, byte[] switchInitialRoute,
            int[] stationNode, byte[][] stationAccepts, int[] stationCapacity,
            int[] waveTick, byte[] waveColor, int[] waveCount, int[] waveSpacingTicks,
            int winDeliveries, int timeLimitTicks,
            int qCapBound, int trainsMax)
        {
            LevelId = levelId;
            NodeCount = nodeCount;
            NodeQueueCapacity = nodeQueueCapacity;
            EdgeFrom = edgeFrom;
            EdgeTo = edgeTo;
            EdgeTravelTicks = edgeTravelTicks;
            // Pin guards live at construction so pinned behaviour is impossible to reach by
            // accident (contract criterion 14, stop condition 1).
            SourceNode = sourceNodes.Length == 1 ? sourceNodes[0] : ThrowSecondSource();
            for (int w = 0; w < waveColor.Length; w++)
                if (waveColor[w] == CatColor.Wild)
                    throw new NotSupportedException(
                        "pinned NEW-Q35: the wild color is out of CM-C1 scope — the resolution boundary changes the command-log format (state/backlog.md Q-A, criterion 14)");
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
        }

        private static int ThrowSecondSource()
        {
            throw new NotSupportedException(
                "pinned: a second source node is out of CM-C1 scope (state/backlog.md, CM-C1 criterion 14 + non-goals)");
        }
    }
}
