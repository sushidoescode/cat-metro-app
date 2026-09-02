using CatMetro.Domain;

namespace CatMetro.Content.Daily
{
    public sealed class DailyBoardFactory : IBoardFactory, IDailyFallbackBoardFactory
    {
        public LevelDto Build(uint seed, string dateKey, int k)
        {
            var rng = new Pcg32(seed, (ulong)k);
            int family = (int)(rng.Next() % 3u);
            bool mirror = (rng.Next() % 2u) != 0;
            var template = DailyBoardTemplates.Candidate(
                family, seed, DailyDifficulty.Target(dateKey));

            var reverseRoutes = new bool[template.Switches.Length];
            for (int i = 0; i < reverseRoutes.Length; i++)
                reverseRoutes[i] = (rng.Next() % 2u) != 0;

            string[] colorMap = ShuffleColors(ref rng);
            return Transform(template, mirror, reverseRoutes, colorMap);
        }

        public LevelDto BuildFallback(uint seed, string dateKey)
        {
            var rng = new Pcg32(seed, 0UL);
            string[] colorMap = ShuffleColors(ref rng);
            var template = DailyBoardTemplates.Fallback(
                seed, DailyDifficulty.Target(dateKey));
            return Transform(template, false, new bool[template.Switches.Length], colorMap);
        }

        private static string[] ShuffleColors(ref Pcg32 rng)
        {
            var colors = new[] { "red", "blue", "yellow", "green" };
            for (int i = colors.Length - 1; i > 0; i--)
            {
                int j = (int)(rng.Next() % (uint)(i + 1));
                string swap = colors[i];
                colors[i] = colors[j];
                colors[j] = swap;
            }
            return colors;
        }

        private static LevelDto Transform(LevelDto template, bool mirror,
            bool[] reverseRoutes, string[] colorMap)
        {
            var nodes = new NodeDto[template.Nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                NodeDto n = template.Nodes.Span[i];
                nodes[i] = new NodeDto(n.Id, mirror ? 6 - n.X : n.X, n.Y,
                    n.QueueCapacity, n.HasQueueCapacity);
            }

            var edges = new EdgeDto[template.Edges.Length];
            for (int i = 0; i < edges.Length; i++)
            {
                EdgeDto e = template.Edges.Span[i];
                edges[i] = new EdgeDto(e.Id, e.From, e.To, e.TravelTicks,
                    e.OneWay, e.Reversible, e.Tunnel, e.Hold);
            }

            var sources = new SourceDto[template.Sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                SourceDto source = template.Sources.Span[i];
                sources[i] = new SourceDto(source.NodeId,
                    MapColors(source.AllowedColors, colorMap));
            }

            var stations = new StationDto[template.Stations.Length];
            for (int i = 0; i < stations.Length; i++)
            {
                StationDto station = template.Stations.Span[i];
                stations[i] = new StationDto(station.NodeId,
                    MapColors(station.Accepts, colorMap), station.Capacity, station.Shape);
            }

            var switches = new SwitchDto[template.Switches.Length];
            for (int i = 0; i < switches.Length; i++)
            {
                SwitchDto value = template.Switches.Span[i];
                var routes = new string[value.Routes.Length];
                for (int j = 0; j < routes.Length; j++)
                    routes[j] = value.Routes.Span[j];

                int initialRoute = value.InitialRoute;
                if (reverseRoutes[i] && routes.Length == 2)
                {
                    string swap = routes[0];
                    routes[0] = routes[1];
                    routes[1] = swap;
                    initialRoute = 1 - initialRoute;
                }
                switches[i] = new SwitchDto(value.Id, value.NodeId, routes, initialRoute,
                    value.CooldownTicks);
            }

            var gates = new GateDto[template.Gates.Length];
            for (int i = 0; i < gates.Length; i++)
            {
                GateDto gate = template.Gates.Span[i];
                var windows = new GateWindowDto[gate.OpenWindows.Length];
                for (int j = 0; j < windows.Length; j++)
                {
                    GateWindowDto window = gate.OpenWindows.Span[j];
                    windows[j] = new GateWindowDto(window.StartTick, window.EndTick);
                }
                gates[i] = new GateDto(gate.EdgeId, windows, gate.PreviewTicks);
            }

            var waves = new WaveDto[template.Waves.Length];
            for (int i = 0; i < waves.Length; i++)
            {
                WaveDto wave = template.Waves.Span[i];
                waves[i] = new WaveDto(wave.Tick, wave.SourceNode,
                    MapColor(wave.Color, colorMap), wave.Count, wave.SpacingTicks,
                    wave.Express, wave.Shape, wave.Stray);
            }

            var mechanics = new string[template.Meta.Mechanics.Length];
            for (int i = 0; i < mechanics.Length; i++)
                mechanics[i] = template.Meta.Mechanics.Span[i];
            var tags = new string[template.Tags.Length];
            for (int i = 0; i < tags.Length; i++) tags[i] = template.Tags.Span[i];

            var meta = new MetaDto(template.Meta.Band, template.Meta.DifficultyTarget,
                mechanics, template.Meta.NewMechanic, template.Meta.TeachingGoal,
                template.Meta.MinActionWindowTicks, template.Meta.AuthoredBy,
                template.Meta.ValidatedAt, template.Meta.HasValidatedAt);
            var win = new WinDto(template.Win.Deliveries, template.Win.TimeLimitTicks,
                template.Win.PerfectMaxSwitches,
                new StarsDto(template.Win.Stars.Two, template.Win.Stars.Three));
            var economy = new EconomyDto(
                template.Economy.BaseTickets, template.Economy.PerfectBonus);

            return new LevelDto(template.SchemaVersion, template.Id, template.Name,
                template.Seed, meta, nodes, edges, sources, stations, switches, waves, win, economy,
                gates, tags);
        }

        private static string[] MapColors(System.ReadOnlyMemory<string> values, string[] colorMap)
        {
            var mapped = new string[values.Length];
            for (int i = 0; i < mapped.Length; i++)
                mapped[i] = MapColor(values.Span[i], colorMap);
            return mapped;
        }

        private static string MapColor(string color, string[] colorMap)
        {
            switch (color)
            {
                case "red": return colorMap[0];
                case "blue": return colorMap[1];
                case "yellow": return colorMap[2];
                case "green": return colorMap[3];
                default: return color;
            }
        }
    }
}
