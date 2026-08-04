using Newtonsoft.Json.Linq;

namespace CatMetro.Content.Daily
{
    // CM-C6 criterion 5's bridge: the factory hands the pipeline a LevelDto; CM-C5's stages
    // consume schema-shaped bytes. This serialiser is the inverse of LevelImporter's DTO walk —
    // same key order as the shipped corpus, absent-key conventions preserved (queueCapacity only
    // when authored, validatedAt only when present and always a string per AMD-09, newMechanic
    // null-able per E-C2a-1) — so a round-trip re-imports to the identical DTO and the daily leg
    // runs the REAL validator, not a parallel one. Serialisation is in-memory only (criterion 6:
    // the Daily root opens and writes nothing).
    public static class DailyBoardJson
    {
        public static string Serialize(LevelDto dto)
        {
            if (dto == null) throw new System.ArgumentException("dto must not be null");

            var meta = new JObject
            {
                ["band"] = dto.Meta.Band,
                ["difficultyTarget"] = dto.Meta.DifficultyTarget,
                ["mechanics"] = Strings(dto.Meta.Mechanics),
                ["newMechanic"] = dto.Meta.NewMechanic == null
                    ? JValue.CreateNull() : (JToken)dto.Meta.NewMechanic,
                ["teachingGoal"] = dto.Meta.TeachingGoal,
                ["minActionWindowTicks"] = dto.Meta.MinActionWindowTicks,
                ["authoredBy"] = dto.Meta.AuthoredBy,
            };
            if (dto.Meta.HasValidatedAt) meta["validatedAt"] = dto.Meta.ValidatedAt;

            var nodes = new JArray();
            foreach (var n in dto.Nodes.Span)
            {
                var o = new JObject { ["id"] = n.Id, ["x"] = n.X, ["y"] = n.Y };
                if (n.HasQueueCapacity) o["queueCapacity"] = n.QueueCapacity;
                nodes.Add(o);
            }

            var edges = new JArray();
            foreach (var e in dto.Edges.Span)
                edges.Add(new JObject
                {
                    ["id"] = e.Id, ["from"] = e.From, ["to"] = e.To,
                    ["travelTicks"] = e.TravelTicks,
                });

            var sources = new JArray();
            foreach (var s in dto.Sources.Span)
                sources.Add(new JObject
                {
                    ["nodeId"] = s.NodeId,
                    ["allowedColors"] = Strings(s.AllowedColors),
                });

            var stations = new JArray();
            foreach (var s in dto.Stations.Span)
                stations.Add(new JObject
                {
                    ["nodeId"] = s.NodeId,
                    ["accepts"] = Strings(s.Accepts),
                    ["capacity"] = s.Capacity,
                });

            var switches = new JArray();
            foreach (var s in dto.Switches.Span)
                switches.Add(new JObject
                {
                    ["id"] = s.Id, ["nodeId"] = s.NodeId,
                    ["routes"] = Strings(s.Routes),
                    ["initialRoute"] = s.InitialRoute,
                });

            var waves = new JArray();
            foreach (var w in dto.Waves.Span)
                waves.Add(new JObject
                {
                    ["tick"] = w.Tick, ["sourceNode"] = w.SourceNode, ["color"] = w.Color,
                    ["count"] = w.Count, ["spacingTicks"] = w.SpacingTicks,
                });

            var root = new JObject
            {
                ["schemaVersion"] = dto.SchemaVersion,
                ["id"] = dto.Id,
                ["name"] = dto.Name,
                ["seed"] = dto.Seed,
                ["meta"] = meta,
                ["board"] = new JObject { ["nodes"] = nodes, ["edges"] = edges },
                ["sources"] = sources,
                ["stations"] = stations,
                ["switches"] = switches,
                ["waves"] = waves,
                ["win"] = new JObject
                {
                    ["deliveries"] = dto.Win.Deliveries,
                    ["timeLimitTicks"] = dto.Win.TimeLimitTicks,
                    ["perfectMaxSwitches"] = dto.Win.PerfectMaxSwitches,
                    ["stars"] = new JObject
                    {
                        ["two"] = dto.Win.Stars.Two,
                        ["three"] = dto.Win.Stars.Three,
                    },
                },
                ["economy"] = new JObject
                {
                    ["baseTickets"] = dto.Economy.BaseTickets,
                    ["perfectBonus"] = dto.Economy.PerfectBonus,
                },
            };
            return root.ToString(Newtonsoft.Json.Formatting.Indented);
        }

        private static JArray Strings(System.ReadOnlyMemory<string> values)
        {
            var a = new JArray();
            foreach (var v in values.Span) a.Add(v);
            return a;
        }
    }
}
