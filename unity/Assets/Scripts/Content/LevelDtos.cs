using System;

namespace CatMetro.Content
{
    // ADR-0008 §Immutable DTOs: sealed types, readonly fields, ReadOnlyMemory<T> views over
    // arrays, constructed once at load and never mutated. Safe to share across runtime, solver
    // and capture rig with no defensive copying. Criterion 3's reflection test enforces the shape
    // of every type in this file (namespace CatMetro.Content, name ending in "Dto").
    public sealed class LevelDto
    {
        public readonly int SchemaVersion;
        public readonly string Id;
        public readonly string Name;
        public readonly long Seed;
        public readonly MetaDto Meta;
        private readonly NodeDto[] _nodes;
        private readonly EdgeDto[] _edges;
        private readonly SourceDto[] _sources;
        private readonly StationDto[] _stations;
        private readonly SwitchDto[] _switches;
        private readonly GateDto[] _gates;
        private readonly WaveDto[] _waves;
        private readonly string[] _tags;
        public readonly WinDto Win;
        public readonly EconomyDto Economy;

        public ReadOnlyMemory<NodeDto> Nodes => _nodes;
        public ReadOnlyMemory<EdgeDto> Edges => _edges;
        public ReadOnlyMemory<SourceDto> Sources => _sources;
        public ReadOnlyMemory<StationDto> Stations => _stations;
        public ReadOnlyMemory<SwitchDto> Switches => _switches;
        public ReadOnlyMemory<GateDto> Gates => _gates;
        public ReadOnlyMemory<WaveDto> Waves => _waves;
        public ReadOnlyMemory<string> Tags => _tags;

        public LevelDto(int schemaVersion, string id, string name, long seed, MetaDto meta,
            NodeDto[] nodes, EdgeDto[] edges, SourceDto[] sources, StationDto[] stations,
            SwitchDto[] switches, WaveDto[] waves, WinDto win, EconomyDto economy,
            GateDto[] gates = null, string[] tags = null)
        {
            SchemaVersion = schemaVersion; Id = id; Name = name; Seed = seed; Meta = meta;
            _nodes = nodes; _edges = edges; _sources = sources; _stations = stations;
            _switches = switches; _gates = gates ?? Array.Empty<GateDto>(); _waves = waves;
            _tags = tags ?? Array.Empty<string>(); Win = win; Economy = economy;
        }
    }

    public sealed class MetaDto
    {
        public readonly string Band;
        public readonly double DifficultyTarget;
        private readonly string[] _mechanics;
        public readonly string NewMechanic;
        public readonly string TeachingGoal;
        public readonly int MinActionWindowTicks;
        public readonly string AuthoredBy;
        public readonly string ValidatedAt;      // null == key absent (never authored as null; AMD-09)
        public readonly bool HasValidatedAt;

        public ReadOnlyMemory<string> Mechanics => _mechanics;

        public MetaDto(string band, double difficultyTarget, string[] mechanics, string newMechanic,
            string teachingGoal, int minActionWindowTicks, string authoredBy, string validatedAt, bool hasValidatedAt)
        {
            Band = band; DifficultyTarget = difficultyTarget; _mechanics = mechanics;
            NewMechanic = newMechanic; TeachingGoal = teachingGoal;
            MinActionWindowTicks = minActionWindowTicks; AuthoredBy = authoredBy;
            ValidatedAt = validatedAt; HasValidatedAt = hasValidatedAt;
        }
    }

    public sealed class NodeDto
    {
        public readonly string Id;
        public readonly int X;
        public readonly int Y;
        public readonly int QueueCapacity;       // authored value, or 0 when absent
        public readonly bool HasQueueCapacity;

        public NodeDto(string id, int x, int y, int queueCapacity, bool hasQueueCapacity)
        {
            Id = id; X = x; Y = y; QueueCapacity = queueCapacity; HasQueueCapacity = hasQueueCapacity;
        }
    }

    public sealed class EdgeDto
    {
        public readonly string Id;
        public readonly string From;
        public readonly string To;
        public readonly int TravelTicks;
        public readonly bool OneWay;
        public readonly bool Reversible;

        public EdgeDto(string id, string from, string to, int travelTicks,
            bool oneWay = true, bool reversible = false)
        {
            Id = id; From = from; To = to; TravelTicks = travelTicks;
            OneWay = oneWay; Reversible = reversible;
        }
    }

    public sealed class SourceDto
    {
        public readonly string NodeId;
        private readonly string[] _allowedColors;
        public ReadOnlyMemory<string> AllowedColors => _allowedColors;

        public SourceDto(string nodeId, string[] allowedColors)
        {
            NodeId = nodeId; _allowedColors = allowedColors;
        }
    }

    public sealed class StationDto
    {
        public readonly string NodeId;
        private readonly string[] _accepts;
        public readonly int Capacity;
        public ReadOnlyMemory<string> Accepts => _accepts;

        public StationDto(string nodeId, string[] accepts, int capacity)
        {
            NodeId = nodeId; _accepts = accepts; Capacity = capacity;
        }
    }

    public sealed class SwitchDto
    {
        public readonly string Id;
        public readonly string NodeId;
        private readonly string[] _routes;
        public readonly int InitialRoute;
        public readonly int CooldownTicks;
        public ReadOnlyMemory<string> Routes => _routes;

        public SwitchDto(string id, string nodeId, string[] routes, int initialRoute,
            int cooldownTicks = 0)
        {
            Id = id; NodeId = nodeId; _routes = routes; InitialRoute = initialRoute;
            CooldownTicks = cooldownTicks;
        }
    }

    public sealed class GateWindowDto
    {
        public readonly int StartTick;
        public readonly int EndTick;

        public GateWindowDto(int startTick, int endTick)
        {
            StartTick = startTick; EndTick = endTick;
        }
    }

    public sealed class GateDto
    {
        public readonly string EdgeId;
        private readonly GateWindowDto[] _openWindows;
        public readonly int PreviewTicks;
        public ReadOnlyMemory<GateWindowDto> OpenWindows => _openWindows;

        public GateDto(string edgeId, GateWindowDto[] openWindows, int previewTicks)
        {
            EdgeId = edgeId; _openWindows = openWindows; PreviewTicks = previewTicks;
        }
    }

    public sealed class WaveDto
    {
        public readonly int Tick;
        public readonly string SourceNode;
        public readonly string Color;
        public readonly int Count;
        public readonly int SpacingTicks;
        public readonly bool Express;

        public WaveDto(int tick, string sourceNode, string color, int count, int spacingTicks,
            bool express = false)
        {
            Tick = tick; SourceNode = sourceNode; Color = color; Count = count;
            SpacingTicks = spacingTicks; Express = express;
        }
    }

    public sealed class WinDto
    {
        public readonly int Deliveries;
        public readonly int TimeLimitTicks;
        public readonly int PerfectMaxSwitches;
        public readonly StarsDto Stars;

        public WinDto(int deliveries, int timeLimitTicks, int perfectMaxSwitches, StarsDto stars)
        {
            Deliveries = deliveries; TimeLimitTicks = timeLimitTicks;
            PerfectMaxSwitches = perfectMaxSwitches; Stars = stars;
        }
    }

    public sealed class StarsDto
    {
        public readonly int Two;
        public readonly int Three;

        public StarsDto(int two, int three)
        {
            Two = two; Three = three;
        }
    }

    public sealed class EconomyDto
    {
        public readonly int BaseTickets;
        public readonly int PerfectBonus;

        public EconomyDto(int baseTickets, int perfectBonus)
        {
            BaseTickets = baseTickets; PerfectBonus = perfectBonus;
        }
    }
}
