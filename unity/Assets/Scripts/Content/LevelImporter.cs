using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CatMetro.Domain;
using CatMetro.Services;

namespace CatMetro.Content
{
    // The one import pipeline (CM-C2a): bytes -> [size cap] -> [UTF-8 decode, BOM-tolerant] ->
    // [depth pre-scan BEFORE deserialization, criterion 6] -> [JToken parse: syntax, duplicate
    // keys, trailing content] -> [schemaVersion] -> [typed DTO walk: integer strictness, missing
    // fields] -> [caps + duplicate ids + bounds + referential integrity] -> [pin pre-checks:
    // second source, wild color — typed failures naming the pin, criterion 10] -> [LevelGraph +
    // id maps]. Total and non-throwing: every failure is a ContentResult error (A-C2a-3).
    // Bare bound literals are banned outside ContentBounds (criterion 5): every number below
    // comes from the constants class.
    public sealed class ImportedLevel
    {
        public readonly LevelDto Dto;
        public readonly LevelGraph Graph;
        public readonly LevelIdMaps IdMaps;

        public ImportedLevel(LevelDto dto, LevelGraph graph, LevelIdMaps idMaps)
        {
            Dto = dto; Graph = graph; IdMaps = idMaps;
        }
    }

    public static class LevelImporter
    {
        private static ContentResult<ImportedLevel> Fail(ContentErrorKind kind, string detail) =>
            ContentResult<ImportedLevel>.Failure(kind, detail);

        public static ContentResult<ImportedLevel> Import(byte[] bytes)
        {
            try
            {
                if (bytes == null) return Fail(ContentErrorKind.MalformedJson, "null payload");
                if (bytes.Length > ContentBounds.CONTENT_MAX_FILE_BYTES)
                    return Fail(ContentErrorKind.FileTooLarge,
                        $"payload {bytes.Length} bytes exceeds CONTENT_MAX_FILE_BYTES");

                string json;
                try
                {
                    int offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
                    json = new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
                }
                catch (DecoderFallbackException)
                {
                    return Fail(ContentErrorKind.EncodingError, "payload is not valid UTF-8");
                }

                int depth = ScanMaxDepth(json);
                if (depth > ContentBounds.CONTENT_MAX_JSON_DEPTH)
                    return Fail(ContentErrorKind.TooDeep,
                        $"nesting depth {depth} exceeds CONTENT_MAX_JSON_DEPTH");

                JToken root;
                try
                {
                    root = ContentJson.LoadToken(json);
                }
                catch (JsonReaderException ex)
                {
                    return ex.Message.IndexOf("same name", StringComparison.OrdinalIgnoreCase) >= 0
                        ? Fail(ContentErrorKind.DuplicateKey, ex.Message)
                        : Fail(ContentErrorKind.MalformedJson, ex.Message);
                }
                catch (JsonException ex)
                {
                    return Fail(ContentErrorKind.MalformedJson, ex.Message);
                }

                if (!(root is JObject o))
                    return Fail(ContentErrorKind.MalformedJson, "root must be a JSON object");

                return Build(o);
            }
            catch (Exception ex)
            {
                // Backstop keeping the function total (ADR-0008 MUST 3 "never a crash").
                return Fail(ContentErrorKind.MalformedJson, "unexpected " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        // Criterion 11: the read seam. Content never touches a filesystem; it receives bytes.
        public static async Task<ContentResult<ImportedLevel>> ImportFromSourceAsync(
            IContentSource source, string relativePath, CancellationToken ct)
        {
            var bytes = await source.ReadAsync(relativePath, ct).ConfigureAwait(false);
            return Import(bytes);
        }

        // Criterion 6: depth is rejected BEFORE the parser sees the document. Counts bracket
        // nesting outside string literals; exact for well-formed input, conservative otherwise.
        private static int ScanMaxDepth(string json)
        {
            int depth = 0, max = 0;
            bool inString = false, escaped = false;
            foreach (char c in json)
            {
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') inString = true;
                else if (c == '{' || c == '[') { depth++; if (depth > max) max = depth; }
                else if (c == '}' || c == ']') depth--;
            }
            return max;
        }

        private sealed class WalkException : Exception
        {
            public readonly ContentErrorKind Kind;
            public WalkException(ContentErrorKind kind, string detail) : base(detail) { Kind = kind; }
        }

        private static JToken Req(JObject o, string name)
        {
            var t = o[name];
            if (t == null) throw new WalkException(ContentErrorKind.MissingField, name);
            return t;
        }

        private static long ReqInt(JObject o, string name)
        {
            var t = Req(o, name);
            if (t.Type != JTokenType.Integer)
                throw new WalkException(t.Type == JTokenType.Float
                    ? ContentErrorKind.IntegerExpected
                    : ContentErrorKind.MalformedJson, name + " must be an integer");
            return (long)t;
        }

        private static int ReqIntIn(JObject o, string name, long min, long max)
        {
            long v = ReqInt(o, name);
            if (v < min || v > max)
                throw new WalkException(ContentErrorKind.BoundViolation, $"{name}={v} outside [{min},{max}]");
            return (int)v;
        }

        private static string ReqStr(JObject o, string name)
        {
            var t = Req(o, name);
            if (t.Type != JTokenType.String)
                throw new WalkException(ContentErrorKind.MalformedJson, name + " must be a string");
            return (string)t;
        }

        private static JArray ReqArr(JObject o, string name, int cap)
        {
            var t = Req(o, name);
            if (!(t is JArray a))
                throw new WalkException(ContentErrorKind.MalformedJson, name + " must be an array");
            if (a.Count > cap)
                throw new WalkException(ContentErrorKind.CollectionOverCap, $"{name} count {a.Count} exceeds cap {cap}");
            return a;
        }

        private static JObject AsObj(JToken t, string what)
        {
            if (!(t is JObject o)) throw new WalkException(ContentErrorKind.MalformedJson, what + " must be an object");
            return o;
        }

        private static readonly Dictionary<string, byte> ColorCodes = new Dictionary<string, byte>
        {
            ["red"] = CatColor.Red, ["blue"] = CatColor.Blue,
            ["yellow"] = CatColor.Yellow, ["green"] = CatColor.Green,
            ["wild"] = CatColor.Wild, // parsed here; pinned out at the pin pre-check (NEW-Q35)
        };

        private static byte ColorCode(string color, string where)
        {
            if (!ColorCodes.TryGetValue(color, out var code))
                throw new WalkException(ContentErrorKind.BoundViolation, $"{where} color '{color}' unknown");
            return code;
        }

        private static ContentResult<ImportedLevel> Build(JObject o)
        {
            try
            {
                int schemaVersion = (int)ReqInt(o, "schemaVersion");
                if (schemaVersion != 2)
                    throw new WalkException(ContentErrorKind.SchemaVersionMismatch, $"schemaVersion {schemaVersion} != 2");
                string id = ReqStr(o, "id");
                string name = ReqStr(o, "name");
                long seed = ReqInt(o, "seed");

                // meta
                var metaObj = AsObj(Req(o, "meta"), "meta");
                var mechArr = ReqArr(metaObj, "mechanics", ContentBounds.MAX_SWITCHES);
                var mechanics = new string[mechArr.Count];
                for (int i = 0; i < mechArr.Count; i++) mechanics[i] = (string)mechArr[i];
                string validatedAt = null;
                bool hasValidatedAt = false;
                var vaProp = metaObj.Property("validatedAt");
                if (vaProp != null)
                {
                    if (vaProp.Value.Type != JTokenType.String)
                        throw new WalkException(ContentErrorKind.MalformedJson,
                            "validatedAt must be a string when present (never null — AMD-09)");
                    validatedAt = (string)vaProp.Value;
                    hasValidatedAt = true;
                }
                var meta = new MetaDto(
                    ReqStr(metaObj, "band"),
                    (double)Req(metaObj, "difficultyTarget"),
                    mechanics,
                    ReqStr(metaObj, "newMechanic"),
                    ReqStr(metaObj, "teachingGoal"),
                    ReqIntIn(metaObj, "minActionWindowTicks", ContentBounds.MIN_ACTION_WINDOW_TICKS_FLOOR, int.MaxValue),
                    ReqStr(metaObj, "authoredBy"),
                    validatedAt, hasValidatedAt);

                // board
                var board = AsObj(Req(o, "board"), "board");
                var nodesArr = ReqArr(board, "nodes", ContentBounds.MAX_NODES);
                var nodes = new NodeDto[nodesArr.Count];
                var nodeIndex = new Dictionary<string, int>();
                for (int i = 0; i < nodesArr.Count; i++)
                {
                    var n = AsObj(nodesArr[i], "node");
                    string nid = ReqStr(n, "id");
                    if (nodeIndex.ContainsKey(nid))
                        throw new WalkException(ContentErrorKind.DuplicateId, $"node id '{nid}' duplicated");
                    int qc = 0; bool hasQc = n.Property("queueCapacity") != null;
                    if (hasQc)
                        qc = ReqIntIn(n, "queueCapacity", ContentBounds.QUEUE_CAPACITY_MIN, ContentBounds.QUEUE_CAPACITY_MAX);
                    nodes[i] = new NodeDto(nid, (int)ReqInt(n, "x"), (int)ReqInt(n, "y"), qc, hasQc);
                    nodeIndex[nid] = i;
                }

                var edgesArr = ReqArr(board, "edges", ContentBounds.MAX_EDGES);
                var edges = new EdgeDto[edgesArr.Count];
                var edgeIndex = new Dictionary<string, int>();
                for (int i = 0; i < edgesArr.Count; i++)
                {
                    var e = AsObj(edgesArr[i], "edge");
                    string eid = ReqStr(e, "id");
                    if (edgeIndex.ContainsKey(eid))
                        throw new WalkException(ContentErrorKind.DuplicateId, $"edge id '{eid}' duplicated");
                    edges[i] = new EdgeDto(eid, ReqStr(e, "from"), ReqStr(e, "to"),
                        ReqIntIn(e, "travelTicks", ContentBounds.TRAVEL_TICKS_MIN, ContentBounds.TRAVEL_TICKS_MAX));
                    edgeIndex[eid] = i;
                }

                var sourcesArr = ReqArr(o, "sources", ContentBounds.MAX_SOURCES);
                var sources = new SourceDto[sourcesArr.Count];
                for (int i = 0; i < sourcesArr.Count; i++)
                {
                    var s = AsObj(sourcesArr[i], "source");
                    var colorsArr = ReqArr(s, "allowedColors", ContentBounds.MAX_SOURCES);
                    var colors = new string[colorsArr.Count];
                    for (int c = 0; c < colorsArr.Count; c++) colors[c] = (string)colorsArr[c];
                    sources[i] = new SourceDto(ReqStr(s, "nodeId"), colors);
                }

                var stationsArr = ReqArr(o, "stations", ContentBounds.MAX_STATIONS);
                var stations = new StationDto[stationsArr.Count];
                for (int i = 0; i < stationsArr.Count; i++)
                {
                    var s = AsObj(stationsArr[i], "station");
                    var acceptsArr = ReqArr(s, "accepts", ContentBounds.MAX_STATIONS);
                    var accepts = new string[acceptsArr.Count];
                    for (int c = 0; c < acceptsArr.Count; c++) accepts[c] = (string)acceptsArr[c];
                    stations[i] = new StationDto(ReqStr(s, "nodeId"), accepts,
                        ReqIntIn(s, "capacity", ContentBounds.STATION_CAPACITY_MIN, ContentBounds.STATION_CAPACITY_MAX));
                }

                var switchesArr = ReqArr(o, "switches", ContentBounds.MAX_SWITCHES);
                var switches = new SwitchDto[switchesArr.Count];
                var switchIndex = new Dictionary<string, int>();
                for (int i = 0; i < switchesArr.Count; i++)
                {
                    var s = AsObj(switchesArr[i], "switch");
                    string sid = ReqStr(s, "id");
                    if (switchIndex.ContainsKey(sid))
                        throw new WalkException(ContentErrorKind.DuplicateId, $"switch id '{sid}' duplicated");
                    var routesArr = ReqArr(s, "routes", ContentBounds.ROUTES_MAX);
                    if (routesArr.Count < ContentBounds.ROUTES_MIN)
                        throw new WalkException(ContentErrorKind.BoundViolation, $"routes count {routesArr.Count} under min");
                    var routes = new string[routesArr.Count];
                    for (int r = 0; r < routesArr.Count; r++) routes[r] = (string)routesArr[r];
                    int initialRoute = ReqIntIn(s, "initialRoute", ContentBounds.INITIAL_ROUTE_MIN, ContentBounds.INITIAL_ROUTE_MAX);
                    if (initialRoute >= routes.Length)
                        throw new WalkException(ContentErrorKind.BoundViolation,
                            $"initialRoute={initialRoute} >= routes.length {routes.Length}");
                    switches[i] = new SwitchDto(sid, ReqStr(s, "nodeId"), routes, initialRoute);
                    switchIndex[sid] = i;
                }

                var wavesArr = ReqArr(o, "waves", ContentBounds.MAX_WAVES);
                var waves = new WaveDto[wavesArr.Count];
                for (int i = 0; i < wavesArr.Count; i++)
                {
                    var w = AsObj(wavesArr[i], "wave");
                    waves[i] = new WaveDto(
                        ReqIntIn(w, "tick", 0, ContentBounds.TIME_LIMIT_TICKS_MAX),
                        ReqStr(w, "sourceNode"),
                        ReqStr(w, "color"),
                        ReqIntIn(w, "count", ContentBounds.WAVE_COUNT_MIN, ContentBounds.WAVE_COUNT_MAX),
                        ReqIntIn(w, "spacingTicks", ContentBounds.SPACING_TICKS_MIN, ContentBounds.SPACING_TICKS_MAX));
                }

                var winObj = AsObj(Req(o, "win"), "win");
                var starsObj = AsObj(Req(winObj, "stars"), "win.stars");
                var win = new WinDto(
                    ReqIntIn(winObj, "deliveries", 1, int.MaxValue),
                    ReqIntIn(winObj, "timeLimitTicks", ContentBounds.TIME_LIMIT_TICKS_MIN, ContentBounds.TIME_LIMIT_TICKS_MAX),
                    (int)ReqInt(winObj, "perfectMaxSwitches"),
                    new StarsDto((int)ReqInt(starsObj, "two"), (int)ReqInt(starsObj, "three")));

                var econObj = AsObj(Req(o, "economy"), "economy");
                var economy = new EconomyDto((int)ReqInt(econObj, "baseTickets"), (int)ReqInt(econObj, "perfectBonus"));

                // referential integrity (ADR-0008 rule 2)
                foreach (var e in edges)
                {
                    if (!nodeIndex.ContainsKey(e.From))
                        throw new WalkException(ContentErrorKind.DanglingReference, $"edge '{e.Id}' from '{e.From}'");
                    if (!nodeIndex.ContainsKey(e.To))
                        throw new WalkException(ContentErrorKind.DanglingReference, $"edge '{e.Id}' to '{e.To}'");
                }
                var sourceNodeIds = new HashSet<string>();
                foreach (var s in sources)
                {
                    if (!nodeIndex.ContainsKey(s.NodeId))
                        throw new WalkException(ContentErrorKind.DanglingReference, $"source nodeId '{s.NodeId}'");
                    sourceNodeIds.Add(s.NodeId);
                }
                foreach (var s in stations)
                    if (!nodeIndex.ContainsKey(s.NodeId))
                        throw new WalkException(ContentErrorKind.DanglingReference, $"station nodeId '{s.NodeId}'");
                foreach (var s in switches)
                {
                    if (!nodeIndex.ContainsKey(s.NodeId))
                        throw new WalkException(ContentErrorKind.DanglingReference, $"switch '{s.Id}' nodeId '{s.NodeId}'");
                    foreach (var r in s.Routes.Span)
                        if (!edgeIndex.ContainsKey(r))
                            throw new WalkException(ContentErrorKind.DanglingReference, $"switch '{s.Id}' route '{r}'");
                }
                foreach (var w in waves)
                    if (!sourceNodeIds.Contains(w.SourceNode))
                        throw new WalkException(ContentErrorKind.DanglingReference, $"wave sourceNode '{w.SourceNode}'");

                // pin pre-checks (criterion 10): typed failures naming the pin; the shipped
                // LevelGraph guards then never fire for imported content.
                if (sources.Length > 1)
                    throw new WalkException(ContentErrorKind.PinnedMechanic,
                        "second source is pinned out of CM-C1 scope (state/backlog.md, criterion 14)");
                foreach (var w in waves)
                {
                    if (ColorCode(w.Color, "wave") == CatColor.Wild)
                        throw new WalkException(ContentErrorKind.PinnedMechanic,
                            "pinned NEW-Q35: the wild color's resolution boundary is undecided");
                }

                // dense mapping (criterion 9): authored order IS the index order (A-C1-10)
                var dto = new LevelDto(schemaVersion, id, name, seed, meta,
                    nodes, edges, sources, stations, switches, waves, win, economy);

                var nodeIds = new string[nodes.Length];
                var nodeQueueCapacity = new int[nodes.Length];
                for (int i = 0; i < nodes.Length; i++)
                {
                    nodeIds[i] = nodes[i].Id;
                    // A-C2a-6: absent queueCapacity maps to the schema max, matching CM-C1's fixture.
                    nodeQueueCapacity[i] = nodes[i].HasQueueCapacity ? nodes[i].QueueCapacity : ContentBounds.QUEUE_CAPACITY_MAX;
                }
                var edgeIds = new string[edges.Length];
                var edgeFrom = new int[edges.Length];
                var edgeTo = new int[edges.Length];
                var edgeTravel = new int[edges.Length];
                for (int i = 0; i < edges.Length; i++)
                {
                    edgeIds[i] = edges[i].Id;
                    edgeFrom[i] = nodeIndex[edges[i].From];
                    edgeTo[i] = nodeIndex[edges[i].To];
                    edgeTravel[i] = edges[i].TravelTicks;
                }
                var sourceIds = new string[sources.Length];
                var sourceNodes = new int[sources.Length];
                for (int i = 0; i < sources.Length; i++)
                {
                    sourceIds[i] = sources[i].NodeId;
                    sourceNodes[i] = nodeIndex[sources[i].NodeId];
                }
                var switchIds = new string[switches.Length];
                var switchRoutes = new int[switches.Length][];
                var switchNode = new int[switches.Length];
                var switchInitial = new byte[switches.Length];
                for (int i = 0; i < switches.Length; i++)
                {
                    switchIds[i] = switches[i].Id;
                    var span = switches[i].Routes.Span;
                    var routeIdx = new int[span.Length];
                    for (int r = 0; r < span.Length; r++) routeIdx[r] = edgeIndex[span[r]];
                    switchRoutes[i] = routeIdx;
                    switchNode[i] = nodeIndex[switches[i].NodeId];
                    switchInitial[i] = (byte)switches[i].InitialRoute;
                }
                var stationIds = new string[stations.Length];
                var stationNode = new int[stations.Length];
                var stationAccepts = new byte[stations.Length][];
                var stationCapacity = new int[stations.Length];
                for (int i = 0; i < stations.Length; i++)
                {
                    stationIds[i] = stations[i].NodeId;
                    stationNode[i] = nodeIndex[stations[i].NodeId];
                    var span = stations[i].Accepts.Span;
                    var codes = new byte[span.Length];
                    for (int c = 0; c < span.Length; c++) codes[c] = ColorCode(span[c], "station accepts");
                    stationAccepts[i] = codes;
                    stationCapacity[i] = stations[i].Capacity;
                }
                var waveTick = new int[waves.Length];
                var waveColor = new byte[waves.Length];
                var waveCount = new int[waves.Length];
                var waveSpacing = new int[waves.Length];
                int trainsMax = 0;
                for (int i = 0; i < waves.Length; i++)
                {
                    waveTick[i] = waves[i].Tick;
                    waveColor[i] = ColorCode(waves[i].Color, "wave");
                    waveCount[i] = waves[i].Count;
                    waveSpacing[i] = waves[i].SpacingTicks;
                    trainsMax += waves[i].Count;
                }

                LevelGraph graph;
                try
                {
                    graph = new LevelGraph(id, nodes.Length, nodeQueueCapacity,
                        edgeFrom, edgeTo, edgeTravel, sourceNodes,
                        switchRoutes, switchNode, switchInitial,
                        stationNode, stationAccepts, stationCapacity,
                        waveTick, waveColor, waveCount, waveSpacing,
                        win.Deliveries, win.TimeLimitTicks,
                        qCapBound: ContentBounds.QUEUE_CAPACITY_MAX, trainsMax: trainsMax);
                }
                catch (NotSupportedException ex)
                {
                    // Defensive: the pin pre-checks above make this unreachable for imported
                    // content, but the guard must never escape (criterion 10).
                    return Fail(ContentErrorKind.PinnedMechanic, ex.Message);
                }
                catch (ArgumentException ex)
                {
                    return Fail(ContentErrorKind.BoundViolation, ex.Message);
                }

                var idMaps = new LevelIdMaps(
                    new ContentIdMap(nodeIds), new ContentIdMap(edgeIds), new ContentIdMap(switchIds),
                    new ContentIdMap(stationIds), new ContentIdMap(sourceIds), waves.Length);

                return ContentResult<ImportedLevel>.Success(new ImportedLevel(dto, graph, idMaps));
            }
            catch (WalkException ex)
            {
                return Fail(ex.Kind, ex.Message);
            }
        }
    }
}
