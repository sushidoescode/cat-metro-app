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

    public static class CatShape
    {
        public const byte Round = 1;
        public const byte Square = 2;
        public const byte Triangle = 3;

        public static bool IsKnown(byte shape) =>
            shape == Round || shape == Square || shape == Triangle;
    }

    // Immutable authored half-open gate interval. Edge entry is permitted when the processing
    // tick is >= StartTick and < EndTick.
    public readonly struct GateWindow
    {
        public readonly int StartTick;
        public readonly int EndTick;

        public GateWindow(int startTick, int endTick)
        {
            StartTick = startTick;
            EndTick = endTick;
        }
    }

    // A-C1-1: the Domain owns its own integer board type; CatMetro.Content maps DTO -> LevelGraph
    // in CM-C2. Fixtures construct this directly in test code (A-C1-2).
    // Arrays are indexed by dense integer ids; the construction order of nodes/edges/switches is
    // part of the digest contract (A-C1-10).
    public sealed class LevelGraph
    {
        // The authored schema/importer bounds boards to ten switches. Keep the transient bitset
        // representation pinned to that same gameplay envelope even though ushort has spare bits.
        private const int MaxFreshAutomaticCooldownSwitches = 10;

        public readonly string LevelId;
        public readonly int NodeCount;
        public readonly int[] NodeQueueCapacity;    // per node, 1..QCapBound (0 = no queue limit checks off)
        public readonly int[] EdgeFrom;             // per edge
        public readonly int[] EdgeTo;               // per edge
        public readonly int[] EdgeTravelTicks;      // per edge
        public readonly bool[] EdgeOneWay;          // per edge; false permits ordinary reverse traversal
        public readonly bool[] EdgeReversible;      // per edge; explicit ordinary reverse permission
        public readonly bool[] EdgeTunnel;          // per edge; endpoints are the paired portals
        public readonly bool[] EdgeHold;            // per edge; holding-loop designation only
        public readonly int SourceNode;             // legacy first-source view for one-source callers
        public readonly int[] SourceNodes;           // every authored source, in authored order
        public readonly int[][] SwitchRoutes;       // per switch: candidate outgoing edge ids (2-3)
        public readonly int[] SwitchNode;           // per switch: the junction node it sits on
        public readonly byte[] SwitchInitialRoute;  // per switch
        public readonly int[] SwitchCooldownTicks;  // per switch; accepted flip lock duration
        public readonly int[] GateEdge;              // per gate: dense edge index
        public readonly GateWindow[][] GateOpenWindows; // per gate, authored order
        public readonly int[] GatePreviewTicks;      // per gate
        public readonly int[] StationNode;          // per station
        public readonly byte[][] StationAccepts;    // per station: accepted colors
        public readonly int[] StationCapacity;      // per station
        public readonly byte[] StationShape;        // per station: concrete cats must also match
        public readonly int[] WaveTick;             // per wave
        public readonly byte[] WaveColor;           // per wave
        public readonly int[] WaveCount;            // per wave
        public readonly int[] WaveSpacingTicks;     // per wave
        public readonly int[] WaveSourceNode;       // per wave: authored source node
        public readonly bool[] WaveExpress;         // per wave; never enters a node queue
        public readonly byte[] WaveShape;           // per wave: CatShape code
        public readonly bool[] WaveStray;           // per wave; refuses stations and presses switches
        public readonly bool CollisionsEnabled;     // same-node/opposing-edge failure rule
        // A stray can establish cooldown after a player tap was accepted but before that tap's
        // next-boundary application. Only graphs capable of that interaction need the transient
        // origin mask in their canonical state digest.
        public readonly bool TracksFreshAutomaticCooldowns;
        public readonly int WinDeliveries;
        public readonly int TimeLimitTicks;
        public readonly int QCapBound;              // digest padding: queue slots per node (A-C1-7 i)
        public readonly int TrainsMax;              // digest padding: fixed train array bound (A-C1-7 ii)
        public readonly int PerfectMaxSwitches;

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
            int perfectMaxSwitches = FlipBudget.Unbudgeted,
            bool[] edgeOneWay = null,
            bool[] edgeReversible = null,
            int[] switchCooldownTicks = null,
            int[] gateEdge = null,
            GateWindow[][] gateOpenWindows = null,
            int[] gatePreviewTicks = null,
            bool[] waveExpress = null,
            bool[] edgeTunnel = null,
            byte[] stationShape = null,
            byte[] waveShape = null,
            bool[] edgeHold = null,
            bool[] waveStray = null,
            bool collisionsEnabled = false)
        {
            if (perfectMaxSwitches < FlipBudget.Unbudgeted)
                throw new ArgumentOutOfRangeException(nameof(perfectMaxSwitches));
            LevelId = levelId;
            NodeCount = nodeCount;
            NodeQueueCapacity = nodeQueueCapacity;
            EdgeFrom = edgeFrom;
            EdgeTo = edgeTo;
            EdgeTravelTicks = edgeTravelTicks;
            int edgeLength = edgeFrom == null ? 0 : edgeFrom.Length;
            EdgeOneWay = BoolDataOrDefault(edgeOneWay, edgeLength, true, nameof(edgeOneWay));
            EdgeReversible = BoolDataOrDefault(edgeReversible, edgeLength, false, nameof(edgeReversible));
            EdgeTunnel = BoolDataOrDefault(edgeTunnel, edgeLength, false, nameof(edgeTunnel));
            EdgeHold = BoolDataOrDefault(edgeHold, edgeLength, false, nameof(edgeHold));
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
            int switchLength = switchRoutes == null ? 0 : switchRoutes.Length;
            SwitchCooldownTicks = IntDataOrDefault(
                switchCooldownTicks, switchLength, nameof(switchCooldownTicks));
            for (int s = 0; s < SwitchCooldownTicks.Length; s++)
                if (SwitchCooldownTicks[s] < 0
                    || SwitchCooldownTicks[s] > SwitchState.MaxCooldown)
                    throw new ArgumentOutOfRangeException(
                        nameof(switchCooldownTicks),
                        $"switchCooldownTicks[{s}] must be 0..{SwitchState.MaxCooldown}");
            int[] resolvedGateEdge;
            GateWindow[][] resolvedGateWindows;
            int[] resolvedGatePreview;
            SetGateData(gateEdge, gateOpenWindows, gatePreviewTicks, edgeLength,
                out resolvedGateEdge, out resolvedGateWindows, out resolvedGatePreview);
            GateEdge = resolvedGateEdge;
            GateOpenWindows = resolvedGateWindows;
            GatePreviewTicks = resolvedGatePreview;
            StationNode = stationNode;
            StationAccepts = stationAccepts;
            StationCapacity = stationCapacity;
            int stationLength = stationNode == null ? 0 : stationNode.Length;
            StationShape = ShapeDataOrDefault(stationShape, stationLength, nameof(stationShape));
            WaveTick = waveTick;
            WaveColor = waveColor;
            WaveCount = waveCount;
            WaveSpacingTicks = waveSpacingTicks;
            WaveExpress = BoolDataOrDefault(waveExpress, waveLength, false, nameof(waveExpress));
            WaveShape = ShapeDataOrDefault(waveShape, waveLength, nameof(waveShape));
            WaveStray = BoolDataOrDefault(waveStray, waveLength, false, nameof(waveStray));
            CollisionsEnabled = collisionsEnabled;
            bool hasStray = false;
            for (int w = 0; w < WaveStray.Length; w++)
                if (WaveStray[w]) { hasStray = true; break; }
            bool hasCooldown = false;
            for (int s = 0; s < SwitchCooldownTicks.Length; s++)
                if (SwitchCooldownTicks[s] > 0) { hasCooldown = true; break; }
            TracksFreshAutomaticCooldowns = hasStray && hasCooldown;
            if (TracksFreshAutomaticCooldowns
                && switchLength > MaxFreshAutomaticCooldownSwitches)
                throw new ArgumentException(
                    $"stray/cooldown origin tracking supports at most {MaxFreshAutomaticCooldownSwitches} switches");
            WinDeliveries = winDeliveries;
            TimeLimitTicks = timeLimitTicks;
            QCapBound = qCapBound;
            TrainsMax = trainsMax;
            PerfectMaxSwitches = perfectMaxSwitches;
        }

        private static bool[] BoolDataOrDefault(
            bool[] values, int expectedLength, bool defaultValue, string paramName)
        {
            if (values != null)
            {
                if (values.Length != expectedLength)
                    throw new ArgumentException(
                        $"{paramName} length must be {expectedLength}", paramName);
                return values;
            }

            var materialized = new bool[expectedLength];
            if (defaultValue)
                for (int i = 0; i < materialized.Length; i++) materialized[i] = true;
            return materialized;
        }

        private static int[] IntDataOrDefault(int[] values, int expectedLength, string paramName)
        {
            if (values != null)
            {
                if (values.Length != expectedLength)
                    throw new ArgumentException(
                        $"{paramName} length must be {expectedLength}", paramName);
                return values;
            }
            return new int[expectedLength];
        }

        private static byte[] ShapeDataOrDefault(byte[] values, int expectedLength, string paramName)
        {
            if (values != null)
            {
                if (values.Length != expectedLength)
                    throw new ArgumentException(
                        $"{paramName} length must be {expectedLength}", paramName);
                for (int i = 0; i < values.Length; i++)
                    if (!CatShape.IsKnown(values[i]))
                        throw new ArgumentException(
                            $"{paramName}[{i}] is not a known CatShape code", paramName);
                return values;
            }

            var materialized = new byte[expectedLength];
            for (int i = 0; i < materialized.Length; i++) materialized[i] = CatShape.Round;
            return materialized;
        }

        private static void SetGateData(
            int[] gateEdge,
            GateWindow[][] gateOpenWindows,
            int[] gatePreviewTicks,
            int edgeLength,
            out int[] resolvedEdge,
            out GateWindow[][] resolvedWindows,
            out int[] resolvedPreview)
        {
            if (gateEdge == null && gateOpenWindows == null && gatePreviewTicks == null)
            {
                resolvedEdge = Array.Empty<int>();
                resolvedWindows = Array.Empty<GateWindow[]>();
                resolvedPreview = Array.Empty<int>();
                return;
            }
            if (gateEdge == null || gateOpenWindows == null || gatePreviewTicks == null
                || gateEdge.Length != gateOpenWindows.Length
                || gateEdge.Length != gatePreviewTicks.Length)
                throw new ArgumentException("all gate arrays must be present and have the same length");

            for (int g = 0; g < gateEdge.Length; g++)
            {
                if (gateEdge[g] < 0 || gateEdge[g] >= edgeLength)
                    throw new ArgumentException($"gate {g}: edge {gateEdge[g]} is outside the graph");
                for (int prior = 0; prior < g; prior++)
                    if (gateEdge[prior] == gateEdge[g])
                        throw new ArgumentException(
                            $"gate {g}: edge {gateEdge[g]} already has a gate", nameof(gateEdge));
                var windows = gateOpenWindows[g];
                if (windows == null || windows.Length == 0)
                    throw new ArgumentException($"gate {g}: at least one open window is required");
                int previousEnd = -1;
                for (int w = 0; w < windows.Length; w++)
                {
                    if (windows[w].StartTick < 0 || windows[w].EndTick <= windows[w].StartTick)
                        throw new ArgumentException(
                            $"gate {g} window {w}: expected 0 <= start < end");
                    if (w > 0 && windows[w].StartTick < previousEnd)
                        throw new ArgumentException(
                            $"gate {g} window {w}: windows must be ordered and non-overlapping");
                    previousEnd = windows[w].EndTick;
                }
            }

            resolvedEdge = gateEdge;
            resolvedWindows = gateOpenWindows;
            resolvedPreview = gatePreviewTicks;
        }
    }
}
