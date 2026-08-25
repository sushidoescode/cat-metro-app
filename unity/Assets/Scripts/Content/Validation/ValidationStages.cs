using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CatMetro.Domain;
using CatMetro.Domain.Solver;

namespace CatMetro.Content.Validation
{
    // Stages 1-11, each a pure function over parsed inputs (bytes/DTO/graph/config) returning a
    // StageVerdict. All freezes are recorded in state/handoffs/CM-C5.md (A-C5-7..12); the tests
    // are the executable definitions. No file APIs anywhere in this assembly.
    public static class SchemaStage
    {
        // Stage 1: hand-rolled interpreter over the actual level_schema.json bytes (no new
        // dependency — hard rule 2; the Newtonsoft schema package is separate and
        // licence-encumbered). Fails closed on any schema keyword outside the implemented subset.
        public static StageVerdict Check(byte[] schemaBytes, byte[] levelBytes)
        {
            Newtonsoft.Json.Linq.JObject schema;
            Newtonsoft.Json.Linq.JToken data;
            try
            {
                schema = Newtonsoft.Json.Linq.JObject.Parse(
                    System.Text.Encoding.UTF8.GetString(schemaBytes));
            }
            catch (Exception ex)
            {
                return StageVerdict.Fail(Stage.Schema, "schema document unreadable: " + ex.Message);
            }
            try
            {
                data = ContentJson.LoadToken(System.Text.Encoding.UTF8.GetString(levelBytes));
            }
            catch (Exception ex)
            {
                return StageVerdict.Fail(Stage.Schema, "level unparseable: " + ex.Message);
            }

            var errors = new List<string>();
            try
            {
                AuditKeywords(schema); // review F5: audit is schema-driven, not data-driven
                ValidateNode(schema, data, "$", errors);
            }
            catch (UnsupportedKeywordException ex)
            {
                return StageVerdict.Fail(Stage.Schema, "unsupported schema keyword '" + ex.Keyword
                    + "' — the interpreter fails closed rather than skip a rule");
            }
            if (errors.Count > 0)
                return StageVerdict.Fail(Stage.Schema, string.Join("; ", errors.Take(8)),
                    errors.Count + " violation(s)");
            return StageVerdict.Pass(Stage.Schema, "schema v2 OK");
        }

        private sealed class UnsupportedKeywordException : Exception
        {
            public readonly string Keyword;
            public UnsupportedKeywordException(string kw) { Keyword = kw; }
        }

        // Annotations carry no constraint; everything else must be in the validated set or the
        // stage fails closed.
        private static readonly HashSet<string> Annotations = new HashSet<string>
        { "$schema", "title", "description", "default" };
        private static readonly HashSet<string> Supported = new HashSet<string>
        {
            "type", "const", "enum", "pattern", "required", "additionalProperties", "properties",
            "items", "prefixItems", "minimum", "maximum", "minItems", "maxItems", "minLength",
            "maxLength",
        };

        // Review F5: walk the SCHEMA (not the data) so a subschema for an absent optional
        // property still fails closed on an unsupported keyword — the verdict may not depend on
        // which optional keys a level happens to carry.
        private static void AuditKeywords(Newtonsoft.Json.Linq.JObject schema)
        {
            foreach (var prop in schema.Properties())
                if (!Annotations.Contains(prop.Name) && !Supported.Contains(prop.Name))
                    throw new UnsupportedKeywordException(prop.Name);

            var addl = schema["additionalProperties"];
            if (addl != null && addl.Type != Newtonsoft.Json.Linq.JTokenType.Boolean)
                throw new UnsupportedKeywordException("additionalProperties(schema-valued)");

            if (schema["properties"] is Newtonsoft.Json.Linq.JObject props)
                foreach (var p in props.Properties())
                {
                    if (!(p.Value is Newtonsoft.Json.Linq.JObject sub))
                        throw new UnsupportedKeywordException("properties(non-object schema)");
                    AuditKeywords(sub);
                }

            var items = schema["items"];
            if (items != null)
            {
                if (schema["prefixItems"] != null)
                    throw new UnsupportedKeywordException("items+prefixItems(tail semantics)");
                if (!(items is Newtonsoft.Json.Linq.JObject itemsObj))
                    throw new UnsupportedKeywordException("items(non-object schema)");
                AuditKeywords(itemsObj);
            }
            if (schema["prefixItems"] is Newtonsoft.Json.Linq.JArray prefix)
                foreach (var p in prefix)
                {
                    if (!(p is Newtonsoft.Json.Linq.JObject sub))
                        throw new UnsupportedKeywordException("prefixItems(non-object schema)");
                    AuditKeywords(sub);
                }
        }

        private static void ValidateNode(Newtonsoft.Json.Linq.JObject schema,
            Newtonsoft.Json.Linq.JToken data, string path, List<string> errors)
        {
            foreach (var prop in schema.Properties())
                if (!Annotations.Contains(prop.Name) && !Supported.Contains(prop.Name))
                    throw new UnsupportedKeywordException(prop.Name);

            if (schema["type"] != null && !TypeMatches(schema["type"], data))
            {
                errors.Add(path + ": type mismatch (expected " + schema["type"].ToString(Newtonsoft.Json.Formatting.None) + ")");
                return; // nothing below can apply meaningfully to the wrong shape
            }

            if (schema["const"] != null && !Newtonsoft.Json.Linq.JToken.DeepEquals(schema["const"], data))
                errors.Add(path + ": const " + schema["const"].ToString(Newtonsoft.Json.Formatting.None) + " violated");

            if (schema["enum"] is Newtonsoft.Json.Linq.JArray allowed
                && !allowed.Any(a => Newtonsoft.Json.Linq.JToken.DeepEquals(a, data)))
                errors.Add(path + ": value not in enum");

            if (data is Newtonsoft.Json.Linq.JValue v && v.Type == Newtonsoft.Json.Linq.JTokenType.String)
            {
                string s = (string)v;
                if (schema["pattern"] != null
                    && !System.Text.RegularExpressions.Regex.IsMatch(s, (string)schema["pattern"]))
                    errors.Add(path + ": does not match pattern '" + (string)schema["pattern"] + "'");
                if (schema["minLength"] != null && s.Length < (int)schema["minLength"])
                    errors.Add(path + ": shorter than minLength " + (int)schema["minLength"]);
                if (schema["maxLength"] != null && s.Length > (int)schema["maxLength"])
                    errors.Add(path + ": longer than maxLength " + (int)schema["maxLength"]);
            }

            if (data is Newtonsoft.Json.Linq.JValue n
                && (n.Type == Newtonsoft.Json.Linq.JTokenType.Integer || n.Type == Newtonsoft.Json.Linq.JTokenType.Float))
            {
                double d = (double)n;
                if (schema["minimum"] != null && d < (double)schema["minimum"])
                    errors.Add(path + ": below minimum "
                        + schema["minimum"].ToString(Newtonsoft.Json.Formatting.None));
                if (schema["maximum"] != null && d > (double)schema["maximum"])
                    errors.Add(path + ": above maximum "
                        + schema["maximum"].ToString(Newtonsoft.Json.Formatting.None));
            }

            if (data is Newtonsoft.Json.Linq.JObject obj)
            {
                if (schema["required"] is Newtonsoft.Json.Linq.JArray req)
                    foreach (var r in req)
                        if (obj[(string)r] == null)
                            errors.Add(path + ": required property '" + (string)r + "' missing");

                var props = schema["properties"] as Newtonsoft.Json.Linq.JObject;
                var addl = schema["additionalProperties"];
                if (addl != null && addl.Type != Newtonsoft.Json.Linq.JTokenType.Boolean)
                    throw new UnsupportedKeywordException("additionalProperties(schema-valued)");
                bool allowExtra = addl == null || (bool)addl;

                foreach (var p in obj.Properties())
                {
                    var sub = props?[p.Name] as Newtonsoft.Json.Linq.JObject;
                    if (sub != null) ValidateNode(sub, p.Value, path + "." + p.Name, errors);
                    else if (!allowExtra)
                        errors.Add(path + ": additionalProperties false forbids '" + p.Name + "'");
                }
            }

            if (data is Newtonsoft.Json.Linq.JArray arr)
            {
                if (schema["minItems"] != null && arr.Count < (int)schema["minItems"])
                    errors.Add(path + ": fewer than minItems " + (int)schema["minItems"]);
                if (schema["maxItems"] != null && arr.Count > (int)schema["maxItems"])
                    errors.Add(path + ": more than maxItems " + (int)schema["maxItems"]);
                if (schema["prefixItems"] is Newtonsoft.Json.Linq.JArray prefix)
                    for (int i = 0; i < arr.Count && i < prefix.Count; i++)
                        ValidateNode((Newtonsoft.Json.Linq.JObject)prefix[i], arr[i], path + "[" + i + "]", errors);
                else if (schema["items"] is Newtonsoft.Json.Linq.JObject itemSchema)
                    for (int i = 0; i < arr.Count; i++)
                        ValidateNode(itemSchema, arr[i], path + "[" + i + "]", errors);
            }
        }

        private static bool TypeMatches(Newtonsoft.Json.Linq.JToken typeSpec, Newtonsoft.Json.Linq.JToken data)
        {
            if (typeSpec is Newtonsoft.Json.Linq.JArray options)
                return options.Any(o => SingleTypeMatches((string)o, data));
            return SingleTypeMatches((string)typeSpec, data);
        }

        private static bool SingleTypeMatches(string type, Newtonsoft.Json.Linq.JToken data)
        {
            switch (type)
            {
                case "object": return data.Type == Newtonsoft.Json.Linq.JTokenType.Object;
                case "array": return data.Type == Newtonsoft.Json.Linq.JTokenType.Array;
                case "string": return data.Type == Newtonsoft.Json.Linq.JTokenType.String;
                case "integer": return data.Type == Newtonsoft.Json.Linq.JTokenType.Integer;
                case "number":
                    return data.Type == Newtonsoft.Json.Linq.JTokenType.Integer
                        || data.Type == Newtonsoft.Json.Linq.JTokenType.Float;
                case "boolean": return data.Type == Newtonsoft.Json.Linq.JTokenType.Boolean;
                case "null": return data.Type == Newtonsoft.Json.Linq.JTokenType.Null;
                default: throw new UnsupportedKeywordException("type:" + type);
            }
        }
    }

    internal static class SourceStationColorCompatibility
    {
        public static bool Matches(SourceDto source, StationDto station)
        {
            var sourceColors = source.AllowedColors.Span;
            for (int i = 0; i < sourceColors.Length; i++)
                if (sourceColors[i] == "wild")
                    return true;

            var stationColors = station.Accepts.Span;
            for (int i = 0; i < sourceColors.Length; i++)
                for (int j = 0; j < stationColors.Length; j++)
                    if (sourceColors[i] == stationColors[j])
                        return true;

            return false;
        }
    }

    public static class StaticAnalysisStage
    {
        // Stage 2 (A-C5-8): colour-compatible reachability, orphan switches, junction spacing
        // >= 1.2 grid units (fail), switch-in-top-15% (warn only).
        public static StageVerdict Check(LevelDto dto)
        {
            var fails = new List<string>();
            var warns = new List<string>();
            var nodes = dto.Nodes.ToArray();
            var edges = dto.Edges.ToArray();
            var nodeById = nodes.ToDictionary(n => n.Id);

            // Reachability: BFS from each colour-compatible source. A station compatible with NO
            // source is a deliberate decoy (L001's BLU teaches the wrong
            // route) — no cat can ever need it, so it cannot FAIL — but review F6: silence hides
            // the accepts-typo class, so it WARNS instead of passing vacuously.
            foreach (var station in dto.Stations.ToArray())
            {
                bool anyCompatibleSource = dto.Sources.ToArray()
                    .Any(s => SourceStationColorCompatibility.Matches(s, station));
                if (!anyCompatibleSource)
                {
                    warns.Add("station " + station.NodeId
                        + " is a decoy: no source emits any colour it accepts");
                    continue;
                }
                bool reachable = false;
                foreach (var source in dto.Sources.ToArray())
                {
                    if (!SourceStationColorCompatibility.Matches(source, station)) continue;
                    var seen = new HashSet<string> { source.NodeId };
                    var queue = new Queue<string>();
                    queue.Enqueue(source.NodeId);
                    while (queue.Count > 0)
                    {
                        var at = queue.Dequeue();
                        if (at == station.NodeId) { reachable = true; break; }
                        foreach (var e in edges)
                            if (e.From == at && seen.Add(e.To))
                                queue.Enqueue(e.To);
                    }
                    if (reachable) break;
                }
                if (!reachable)
                    fails.Add("station " + station.NodeId
                        + " is not reachable from any source able to emit its colours");
            }

            // Orphan switches.
            foreach (var sw in dto.Switches.ToArray())
            {
                if (!edges.Any(e => e.To == sw.NodeId))
                    fails.Add("orphan switch " + sw.Id + ": node " + sw.NodeId + " has no inbound edge");
                foreach (var route in sw.Routes.ToArray())
                    if (!edges.Any(e => e.Id == route && e.From == sw.NodeId))
                        fails.Add("orphan switch " + sw.Id + ": route " + route
                            + " is not an outbound edge of " + sw.NodeId);
            }

            // Junction spacing >= 1.2 (CM-R07.6): Euclidean over authored coordinates.
            var switches = dto.Switches.ToArray();
            for (int i = 0; i < switches.Length; i++)
                for (int j = i + 1; j < switches.Length; j++)
                {
                    var a = nodeById[switches[i].NodeId];
                    var b = nodeById[switches[j].NodeId];
                    double dx = a.X - b.X, dy = a.Y - b.Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist < 1.2)
                        fails.Add("junction spacing " + switches[i].Id + "-" + switches[j].Id + " = "
                            + dist.ToString("0.##", CultureInfo.InvariantCulture)
                            + " < 1.2 grid units");
                }

            // Top-15% warning (product_spec.md:638): warns, never fails.
            if (nodes.Length > 0 && switches.Length > 0)
            {
                double maxY = nodes.Max(n => (double)n.Y), minY = nodes.Min(n => (double)n.Y);
                if (maxY > minY)
                {
                    double threshold = maxY - 0.15 * (maxY - minY);
                    foreach (var sw in switches)
                        if (nodeById[sw.NodeId].Y >= threshold)
                            warns.Add("switch " + sw.Id + " sits in the top 15% of the board");
                }
            }

            if (fails.Count > 0)
                return StageVerdict.Fail(Stage.StaticAnalysis,
                    string.Join("; ", fails.Concat(warns.Select(w => "WARN: " + w))));
            if (warns.Count > 0)
                return StageVerdict.Warn(Stage.StaticAnalysis, string.Join("; ", warns));
            return StageVerdict.Pass(Stage.StaticAnalysis, "static analysis OK");
        }
    }

    public static class LowerBoundStage
    {
        // Stage 3 (A-C5-9): min colour-compatible source->station path (Dijkstra over
        // travelTicks) x win.deliveries, vs timeLimitTicks + lowerBoundSlack (Q-R row).
        public static StageVerdict Check(LevelDto dto, ValidatorConfig config)
        {
            var edges = dto.Edges.ToArray();
            int best = int.MaxValue;
            foreach (var source in dto.Sources.ToArray())
            {
                var dist = Dijkstra(source.NodeId, edges);
                foreach (var station in dto.Stations.ToArray())
                {
                    if (!SourceStationColorCompatibility.Matches(source, station)) continue;
                    if (dist.TryGetValue(station.NodeId, out int d) && d < best) best = d;
                }
            }
            if (best == int.MaxValue)
                return StageVerdict.Fail(Stage.LowerBoundFeasibility,
                    "no colour-compatible source-station path exists");

            long bound = (long)best * dto.Win.Deliveries;
            string value = "lowerBound=" + bound + " (minTravelTicks=" + best
                + " x deliveries=" + dto.Win.Deliveries + ")";
            if (config.LowerBoundSlack == null)
                return StageVerdict.Unconfigured(Stage.LowerBoundFeasibility, "lowerBoundSlack", value);
            long limit = (long)dto.Win.TimeLimitTicks + config.LowerBoundSlack.Value;
            if (bound > limit)
                return StageVerdict.Fail(Stage.LowerBoundFeasibility,
                    "lowerBound " + bound + " > timeLimitTicks " + dto.Win.TimeLimitTicks
                    + " + slack " + config.LowerBoundSlack.Value, value);
            return StageVerdict.Pass(Stage.LowerBoundFeasibility, value);
        }

        private static Dictionary<string, int> Dijkstra(string from, EdgeDto[] edges)
        {
            var dist = new Dictionary<string, int> { [from] = 0 };
            var done = new HashSet<string>();
            while (true)
            {
                string at = null;
                int atDist = int.MaxValue;
                foreach (var kv in dist)
                    if (!done.Contains(kv.Key) && kv.Value < atDist) { at = kv.Key; atDist = kv.Value; }
                if (at == null) return dist;
                done.Add(at);
                foreach (var e in edges)
                    if (e.From == at)
                    {
                        int nd = atDist + e.TravelTicks;
                        if (!dist.TryGetValue(e.To, out int old) || nd < old) dist[e.To] = nd;
                    }
            }
        }
    }

    public static class SolverStage
    {
        // Stage 4: CM-C4's Solve. Blocks ONLY on Unsolvable; NotFound(Beam|Budget) and
        // Indeterminate print their counts, non-blocking (Q-N; ADR-0008:117).
        public static StageVerdict Verdict(SolveResult solve)
        {
            string value = "nodes=" + solve.NodesExpanded + " pinned=" + solve.PinnedPruned;
            switch (solve.Verdict)
            {
                case SolveVerdict.Solved:
                    return StageVerdict.Pass(Stage.Solver,
                        "ticks=" + solve.CompletionTicks + " switches=" + solve.SwitchesUsed
                        + " width=" + solve.BeamWidthUsed + " " + value);
                case SolveVerdict.Unsolvable:
                    return StageVerdict.Fail(Stage.Solver,
                        "Unsolvable — the reachable space holds no winning log", value);
                case SolveVerdict.NotFound:
                    return StageVerdict.Warn(Stage.Solver,
                        "NotFound(" + solve.NotFoundReason + ", width=" + solve.BeamWidthUsed
                        + ") — not a proof; a human witness replay is admissible (ADR-0008:117)", value);
                default:
                    return StageVerdict.Warn(Stage.Solver,
                        "Indeterminate(pinned, " + solve.PinnedPruned + ", "
                        + solve.FirstPinMessage + ") — Q-N: pruned pins are not unsolvability", value);
            }
        }
    }

    public static class TrivialityStage
    {
        // Stage 5: EvaluateLog(empty). A Solved zero-input run FAILS the level (CM-R12.2).
        public static StageVerdict Check(LevelGraph graph, ulong seed)
        {
            var baseline = LevelSolver.EvaluateLog(graph, seed, new CommandLog());
            if (baseline.Verdict == SolveVerdict.Solved)
                return StageVerdict.Fail(Stage.TrivialityReject,
                    "zero-input run wins in " + baseline.CompletionTicks
                    + " ticks — even L001 requires its one tap (CM-R12.2)");
            if (baseline.Verdict == SolveVerdict.Indeterminate)
                return StageVerdict.Pass(Stage.TrivialityReject,
                    "zero-input run pinned (" + baseline.FirstPinMessage + ") — not a win");
            return StageVerdict.Pass(Stage.TrivialityReject, "zero-input run does not win");
        }
    }

    public static class BrittlenessStage
    {
        // Stage 6 (A-C5-2 + A-C5-7): deterministic Pcg32 jitter retention >= 70%; per-entry
        // action-window measurement on the solver-optimal log; onboarding band uses 12-16.
        public static StageVerdict Check(LevelDto dto, LevelGraph graph, CommandLog winningLog, ValidatorConfig config)
        {
            var fails = new List<string>();
            int maw = dto.Meta.MinActionWindowTicks;
            if (dto.Meta.Band == "onboarding" && (maw < 12 || maw > 16))
                fails.Add("onboarding band requires minActionWindowTicks in 12-16, got " + maw);

            if (winningLog == null)
            {
                if (fails.Count > 0)
                    return StageVerdict.Fail(Stage.BrittlenessAccessibility, string.Join("; ", fails));
                return StageVerdict.Skipped(Stage.BrittlenessAccessibility, "no winning log");
            }

            ulong seed = (ulong)dto.Seed;
            var entries = winningLog.Entries;

            // Jitter retention: one Pcg32 stream over (samples x entries) draws — deterministic.
            // A jittered sample that hits the NEW-Q4 guard is neither a win nor a loss: rejection
            // semantics are undecided, so the sample is INDETERMINATE (stop condition 7).
            // Review F2 narrowed the rule: retention is measured over the UNPINNED samples
            // whenever any exist — wins and losses there measure real brittleness and the >= 70%
            // guard stays live (measured on L701: 18/20 samples pin, and the 2 unpinned both win,
            // so the shipped corpus passes at 100% over its unpinned set with the pin counts
            // printed). Only a sample set with NO unpinned member reports PINNED(NEW-Q4) — a
            // near-unreachable backstop, since a one-entry-changed jitter sample is also a window
            // shift, so all-pinned jitters imply width-1 windows and the window limb fires first.
            var rng = new Pcg32(seed, 3);
            int wins = 0, losses = 0, pinnedSamples = 0;
            int samples = config.JitterSampleCount;
            for (int s = 0; s < samples; s++)
            {
                var jittered = new CommandLog();
                foreach (var e in entries)
                {
                    int offset = (int)(rng.Next() % 3) - 1;
                    jittered.Append(new ToggleSwitchCommand(e.SwitchId, Math.Max(0, e.Tick + offset)));
                }
                switch (LevelSolver.EvaluateLog(graph, seed, jittered).Verdict)
                {
                    case SolveVerdict.Solved: wins++; break;
                    case SolveVerdict.Indeterminate: pinnedSamples++; break;
                    default: losses++; break;
                }
            }
            int denom = wins + losses;
            bool pinDominated = denom == 0 && pinnedSamples > 0;
            string retentionStr;
            if (pinDominated)
            {
                retentionStr = "PINNED(NEW-Q4)";
            }
            else
            {
                int retention = denom == 0 ? 100 : wins * 100 / denom;
                retentionStr = retention + "%";
                if (retention < 70)
                    fails.Add("jitter retention " + retention + "% < 70% over "
                        + denom + " unpinned samples (CM-R12.3)");
            }

            // Action windows, measured on the solver-optimal log (recorded limitation: another
            // solution might have wider windows; the numbers print so a human can overrule).
            var windows = new List<int>();
            int bound = maw + 2;
            for (int i = 0; i < entries.Count; i++)
            {
                int t = entries[i].Tick;
                int width = 1;
                for (int d = 1; d <= bound; d++)
                {
                    if (!ShiftWins(graph, seed, entries, i, t + d)) break;
                    width++;
                }
                for (int d = 1; d <= bound && t - d >= 0; d++)
                {
                    if (!ShiftWins(graph, seed, entries, i, t - d)) break;
                    width++;
                }
                windows.Add(width);
                if (width < maw)
                    fails.Add("entry " + i + " action window " + width
                        + " ticks < minActionWindowTicks " + maw);
            }

            string value = "retention=" + retentionStr + " (wins=" + wins + " losses=" + losses
                + " pinned=" + pinnedSamples + ") windows=[" + string.Join(",", windows) + "]";
            if (fails.Count > 0)
                return StageVerdict.Fail(Stage.BrittlenessAccessibility, string.Join("; ", fails), value);
            if (pinDominated)
                return StageVerdict.Pinned(Stage.BrittlenessAccessibility, "NEW-Q4", value);
            return StageVerdict.Pass(Stage.BrittlenessAccessibility, value);
        }

        private static bool ShiftWins(LevelGraph graph, ulong seed,
            IReadOnlyList<ToggleSwitchCommand> entries, int index, int newTick)
        {
            var shifted = new CommandLog();
            for (int j = 0; j < entries.Count; j++)
                shifted.Append(j == index
                    ? new ToggleSwitchCommand(entries[j].SwitchId, newTick)
                    : entries[j]);
            return LevelSolver.EvaluateLog(graph, seed, shifted).Verdict == SolveVerdict.Solved;
        }
    }

    public static class StarCheckStage
    {
        // Stage 7: two < three, both >= 1 (blocking today). Then: starBandSlack row absent =>
        // UNCONFIGURED(starBandSlack); row present => PINNED(NEW-Q5), because the reachability
        // comparison needs the pinned scoring model either way.
        public static StageVerdict Check(LevelDto dto, ValidatorConfig config)
        {
            int two = dto.Win.Stars.Two, three = dto.Win.Stars.Three;
            string value = "two=" + two + " three=" + three;
            if (two < 1 || three < 1 || two >= three)
                return StageVerdict.Fail(Stage.StarCheck,
                    "stars must satisfy 1 <= two < three, got two=" + two + " three=" + three, value);
            if (config.StarBandSlack == null)
                return StageVerdict.Unconfigured(Stage.StarCheck, "starBandSlack", value);
            return StageVerdict.Pinned(Stage.StarCheck, "NEW-Q5", value);
        }
    }

    public sealed class DifficultyAxes
    {
        public readonly int B;                    // nodes + edges + switches
        public readonly int PeakTrains80;         // E component 1
        public readonly double InterleaveEntropy; // E component 2 (bits)
        public readonly int C;                    // proxy MaxSimultaneousPendingDecisions
        public readonly double T;                 // SolverOptimalTicks / TimeLimitTicks
        public readonly int HQueueSlack;          // proxy MinQueueSlackAtPeak — PARTIAL(Q-J)
        public readonly int RWinnable;
        public readonly int RTried;

        public DifficultyAxes(int b, int peak, double entropy, int c, double t, int h, int rw, int rt)
        {
            B = b; PeakTrains80 = peak; InterleaveEntropy = entropy; C = c; T = t;
            HQueueSlack = h; RWinnable = rw; RTried = rt;
        }
    }

    public static class DifficultyStage
    {
        // Stage 8 (A-C5-10): raw axes always computed and printed; normalisation + the +-0.05
        // comparison run only when the axisBBandCaps row exists (Q-R). H prints PARTIAL(Q-J).
        public static DifficultyAxes ComputeAxes(LevelDto dto, SolveResult solve)
        {
            int b = dto.Nodes.Length + dto.Edges.Length + dto.Switches.Length;
            var spawns = SpawnSequence(dto);
            int peak = PeakInWindow(spawns, 80);
            double entropy = TransitionEntropy(spawns);
            double t = solve.Proxy.SolverOptimalTicks / (double)dto.Win.TimeLimitTicks;
            return new DifficultyAxes(b, peak, entropy,
                solve.Proxy.MaxSimultaneousPendingDecisions, t,
                solve.Proxy.MinQueueSlackAtPeak,
                solve.Proxy.SinglePerturbationsWinnable, solve.Proxy.SinglePerturbationsTried);
        }

        public static double WeightedSum(DifficultyAxes axes, BandCapsRow caps)
        {
            double nB = Math.Min(1.0, axes.B / (double)caps.MaxComplexity);
            double nE = Math.Min(1.0, axes.PeakTrains80 / (double)caps.PeakTrainsCap) * 0.5
                + Math.Min(1.0, axes.InterleaveEntropy / 2.0) * 0.5;
            double nC = Math.Min(1.0, axes.C / (double)caps.MaxConcurrency);
            double nT = Math.Min(1.0, axes.T);
            double nH = 1.0 - Math.Min(1.0, axes.HQueueSlack / (double)caps.MaxHeadroom);
            double nR = 1.0 - (axes.RTried == 0 ? 1.0 : axes.RWinnable / (double)axes.RTried);
            return 0.20 * nB + 0.25 * nE + 0.20 * nC + 0.15 * nT + 0.15 * nH + 0.05 * nR;
        }

        public static StageVerdict Check(LevelDto dto, SolveResult solve, ValidatorConfig config)
        {
            if (solve == null || solve.Verdict != SolveVerdict.Solved)
                return StageVerdict.Skipped(Stage.DifficultyCheck, "no solver trace");
            var axes = ComputeAxes(dto, solve);
            string value = "B=" + axes.B + " peak80=" + axes.PeakTrains80
                + " entropy=" + axes.InterleaveEntropy.ToString("0.###", CultureInfo.InvariantCulture)
                + " C=" + axes.C
                + " T=" + axes.T.ToString("0.####", CultureInfo.InvariantCulture)
                + " H=" + axes.HQueueSlack + " PARTIAL(Q-J)"
                + " R=" + axes.RWinnable + "/" + axes.RTried;

            if (config.AxisBBandCaps == null || !config.AxisBBandCaps.ContainsKey(dto.Meta.Band))
                return StageVerdict.Unconfigured(Stage.DifficultyCheck, "axisBBandCaps", value);

            double sum = WeightedSum(axes, config.AxisBBandCaps[dto.Meta.Band]);
            double diff = Math.Abs(sum - dto.Meta.DifficultyTarget);
            string cmp = value + " computed="
                + sum.ToString("0.####", CultureInfo.InvariantCulture);
            if (diff > 0.05)
                return StageVerdict.Fail(Stage.DifficultyCheck,
                    "computed " + sum.ToString("0.####", CultureInfo.InvariantCulture)
                    + " deviates from authored "
                    + dto.Meta.DifficultyTarget.ToString("0.####", CultureInfo.InvariantCulture)
                    + " by more than the 0.05 tolerance (CM-R09.2)", cmp);
            return StageVerdict.Pass(Stage.DifficultyCheck, cmp);
        }

        internal static List<(int tick, string color)> SpawnSequence(LevelDto dto)
        {
            var spawns = new List<(int tick, string color)>();
            foreach (var w in dto.Waves.ToArray())
                for (int i = 0; i < w.Count; i++)
                    spawns.Add((w.Tick + i * w.SpacingTicks, w.Color));
            return spawns.OrderBy(s => s.tick).ToList(); // OrderBy is stable: wave order breaks ties
        }

        internal static int PeakInWindow(List<(int tick, string color)> spawns, int window)
        {
            int peak = 0;
            foreach (var (start, _) in spawns)
            {
                int count = spawns.Count(s => s.tick >= start && s.tick < start + window);
                if (count > peak) peak = count;
            }
            return peak;
        }

        internal static double TransitionEntropy(List<(int tick, string color)> spawns)
        {
            if (spawns.Count < 2) return 0.0;
            var counts = new Dictionary<string, int>();
            for (int i = 1; i < spawns.Count; i++)
            {
                string key = spawns[i - 1].color + ">" + spawns[i].color;
                counts[key] = counts.TryGetValue(key, out int c) ? c + 1 : 1;
            }
            double total = spawns.Count - 1;
            double h = 0;
            foreach (var c in counts.Values)
            {
                double p = c / total;
                h -= p * Math.Log(p, 2.0);
            }
            return h;
        }
    }

    public static class NoveltyStage
    {
        // Stage 9 (A-C5-11): fixed 13-component feature vector, Euclidean distance vs prior
        // campaign levels in play order; threshold row noveltyMinDistance (Q-R).
        public static double[] Vector(LevelDto dto)
        {
            var spawns = DifficultyStage.SpawnSequence(dto);
            var edges = dto.Edges.ToArray();
            return new double[]
            {
                dto.Nodes.Length, dto.Edges.Length, dto.Switches.Length, dto.Stations.Length,
                dto.Sources.Length, dto.Waves.Length, spawns.Count,
                DifficultyStage.PeakInWindow(spawns, 80),
                spawns.Select(s => s.color).Distinct().Count(),
                dto.Win.TimeLimitTicks / 100.0, dto.Win.Deliveries,
                edges.Length == 0 ? 0 : edges.Average(e => (double)e.TravelTicks),
                DifficultyStage.TransitionEntropy(spawns),
            };
        }

        public static double Distance(double[] a, double[] b)
        {
            double sum = 0;
            for (int i = 0; i < a.Length; i++) { double d = a[i] - b[i]; sum += d * d; }
            return Math.Sqrt(sum);
        }

        public static StageVerdict Check(LevelDto dto, IReadOnlyList<LevelDto> priorCampaign, ValidatorConfig config)
        {
            var mine = Vector(dto);
            var distances = priorCampaign
                .Select(p => (p.Id, d: Distance(mine, Vector(p))))
                .ToList();
            string value = distances.Count == 0
                ? "no priors"
                : string.Join(" ", distances.Select(x =>
                    x.Id + ":" + x.d.ToString("0.###", CultureInfo.InvariantCulture)));

            if (config.NoveltyMinDistance == null)
                return StageVerdict.Unconfigured(Stage.NoveltyCheck, "noveltyMinDistance", value);
            if (distances.Count == 0)
                return StageVerdict.Pass(Stage.NoveltyCheck, value);
            var worst = distances.OrderBy(x => x.d).First();
            if (worst.d < config.NoveltyMinDistance.Value)
                return StageVerdict.Fail(Stage.NoveltyCheck,
                    "feature distance to " + worst.Id + " = "
                    + worst.d.ToString("0.###", CultureInfo.InvariantCulture)
                    + " < threshold "
                    + config.NoveltyMinDistance.Value.ToString("0.###", CultureInfo.InvariantCulture), value);
            return StageVerdict.Pass(Stage.NoveltyCheck, value);
        }
    }

    public static class StalenessStage
    {
        // Stage 10 (Q-O analyst default): absent key => STALE; ISO-8601 ordinal compare against
        // the host-supplied reference; NEVER blocks while Q-O is open (the report says so).
        public static StageVerdict Check(LevelDto dto, string referenceTimestamp)
        {
            const string qo = " — computed, printed, non-blocking while Q-O is open";
            if (referenceTimestamp == null)
                return new StageVerdict(Stage.Staleness, StageVerdictCode.Stale,
                    "reference timestamp unavailable — treated as stale" + qo,
                    dto.Meta.HasValidatedAt ? dto.Meta.ValidatedAt : "", false);
            if (!dto.Meta.HasValidatedAt)
                return new StageVerdict(Stage.Staleness, StageVerdictCode.Stale,
                    "meta.validatedAt absent — treated as stale (ADR-0008:119-123)" + qo, "", false);
            if (CompareIso(dto.Meta.ValidatedAt, referenceTimestamp) < 0)
                return new StageVerdict(Stage.Staleness, StageVerdictCode.Stale,
                    "validatedAt " + dto.Meta.ValidatedAt + " predates the last sim/schema change "
                    + referenceTimestamp + qo, dto.Meta.ValidatedAt, false);
            return new StageVerdict(Stage.Staleness, StageVerdictCode.Fresh,
                "validatedAt " + dto.Meta.ValidatedAt + " is newer than " + referenceTimestamp,
                dto.Meta.ValidatedAt, false);
        }

        // Review F9: authors and this repo's own host write ISO-8601 with DIFFERENT offsets
        // ("Z" stamps vs git's "-07:00"), so an ordinal compare inverts across offsets. Parse
        // both instants when possible; ordinal only as the unparseable fallback.
        private static int CompareIso(string a, string b)
        {
            if (DateTimeOffset.TryParse(a, CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var da)
                && DateTimeOffset.TryParse(b, CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var db))
                return da.CompareTo(db);
            return string.CompareOrdinal(a, b);
        }
    }

    public sealed class PlaytestRow
    {
        public readonly string LevelId;
        public readonly string Band;
        public readonly bool Capstone;
        public readonly int RequiredTesters;

        public PlaytestRow(string levelId, string band, bool capstone, int requiredTesters)
        {
            LevelId = levelId; Band = band; Capstone = capstone; RequiredTesters = requiredTesters;
        }
    }

    public static class PlaytestStage
    {
        // Stage 11: checklist artifact row; HUMAN-VERIFIED (pending); depends on D-6; never blocks.
        public static (PlaytestRow row, StageVerdict verdict) Row(LevelDto dto)
        {
            bool capstone = dto.Meta.Band == "capstone";
            var row = new PlaytestRow(dto.Id, dto.Meta.Band, capstone, capstone ? 3 : 1);
            var verdict = new StageVerdict(Stage.HumanPlaytest, StageVerdictCode.Pending,
                "HUMAN-VERIFIED (pending) — CI cannot run this stage (ADR-0009:35); depends on D-6",
                "testers=" + row.RequiredTesters, false);
            return (row, verdict);
        }
    }
}
