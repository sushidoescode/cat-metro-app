namespace CatMetro.Content.Daily
{
    internal static class DailyBoardTemplates
    {
        internal static LevelDto Candidate(int family, uint seed, double difficulty)
        {
            switch (family)
            {
                case 0: return AlternatingFork(seed, difficulty);
                case 1: return QueuedFork(seed, difficulty);
                default: return Cascade(seed, difficulty);
            }
        }

        internal static LevelDto Fallback(uint seed, double difficulty)
        {
            return Board(seed, difficulty, new[] { "switch" },
                new[]
                {
                    Node("SRC", 3, 8), Node("J1", 3, 5),
                    Node("RED", 1, 1), Node("BLU", 5, 1),
                },
                new[]
                {
                    Edge("E1", "SRC", "J1", 8), Edge("E2", "J1", "RED", 12),
                    Edge("E3", "J1", "BLU", 12),
                },
                new[] { new SourceDto("SRC", new[] { "red" }) },
                new[]
                {
                    new StationDto("RED", new[] { "red" }, 8),
                    new StationDto("BLU", new[] { "blue" }, 8),
                },
                new[] { new SwitchDto("S1", "J1", new[] { "E2", "E3" }, 1) },
                new[] { new WaveDto(8, "SRC", "red", 2, 20) },
                new WinDto(2, 160, 1, new StarsDto(200, 300)));
        }

        private static LevelDto AlternatingFork(uint seed, double difficulty)
        {
            return Board(seed, difficulty, new[] { "switch", "queue" },
                new[]
                {
                    Node("SRC", 3, 9, 3), Node("J1", 3, 6),
                    Node("RED", 1, 2), Node("BLU", 5, 2),
                },
                new[]
                {
                    Edge("E1", "SRC", "J1", 8), Edge("E2", "J1", "RED", 12),
                    Edge("E3", "J1", "BLU", 12),
                },
                new[] { new SourceDto("SRC", new[] { "red", "blue" }) },
                new[]
                {
                    new StationDto("RED", new[] { "red" }, 6),
                    new StationDto("BLU", new[] { "blue" }, 6),
                },
                new[] { new SwitchDto("S1", "J1", new[] { "E2", "E3" }, 0) },
                new[]
                {
                    new WaveDto(8, "SRC", "red", 2, 16),
                    new WaveDto(48, "SRC", "blue", 2, 16),
                    new WaveDto(88, "SRC", "red", 2, 16),
                    new WaveDto(128, "SRC", "blue", 2, 16),
                },
                new WinDto(8, 260, 4, new StarsDto(700, 950)));
        }

        private static LevelDto QueuedFork(uint seed, double difficulty)
        {
            return Board(seed, difficulty, new[] { "switch", "queue" },
                new[]
                {
                    Node("SRC", 3, 9, 5), Node("GATE", 3, 7),
                    Node("HOLD", 5, 7, 1), Node("J1", 3, 5),
                    Node("RED", 1, 1), Node("BLU", 5, 1),
                },
                new[]
                {
                    Edge("E1", "SRC", "GATE", 6), Edge("E2", "GATE", "J1", 6),
                    Edge("E3", "GATE", "HOLD", 20), Edge("E4", "J1", "RED", 8),
                    Edge("E5", "J1", "BLU", 8),
                },
                new[] { new SourceDto("SRC", new[] { "red", "blue" }) },
                new[]
                {
                    new StationDto("RED", new[] { "red" }, 8),
                    new StationDto("BLU", new[] { "blue" }, 8),
                },
                new[]
                {
                    new SwitchDto("S1", "GATE", new[] { "E2", "E3" }, 0),
                    new SwitchDto("S2", "J1", new[] { "E4", "E5" }, 1),
                },
                new[]
                {
                    new WaveDto(8, "SRC", "red", 3, 8),
                    new WaveDto(64, "SRC", "blue", 2, 16),
                },
                new WinDto(5, 130, 2, new StarsDto(400, 600)));
        }

        private static LevelDto Cascade(uint seed, double difficulty)
        {
            return Board(seed, difficulty, new[] { "switch", "queue" },
                new[]
                {
                    Node("SRC", 3, 10, 4), Node("J1", 3, 7, 3),
                    Node("RED", 0, 1), Node("J2", 5, 5, 3),
                    Node("BLU", 4, 1), Node("YEL", 6, 1),
                },
                new[]
                {
                    Edge("E1", "SRC", "J1", 8), Edge("E2", "J1", "RED", 12),
                    Edge("E3", "J1", "J2", 8), Edge("E4", "J2", "BLU", 10),
                    Edge("E5", "J2", "YEL", 10),
                },
                new[] { new SourceDto("SRC", new[] { "red", "blue", "yellow" }) },
                new[]
                {
                    new StationDto("RED", new[] { "red" }, 6),
                    new StationDto("BLU", new[] { "blue" }, 6),
                    new StationDto("YEL", new[] { "yellow" }, 6),
                },
                new[]
                {
                    new SwitchDto("S1", "J1", new[] { "E2", "E3" }, 1),
                    new SwitchDto("S2", "J2", new[] { "E4", "E5" }, 0),
                },
                new[]
                {
                    new WaveDto(8, "SRC", "red", 1, 16),
                    new WaveDto(40, "SRC", "blue", 1, 16),
                    new WaveDto(72, "SRC", "yellow", 1, 16),
                },
                new WinDto(3, 180, 3, new StarsDto(500, 750)));
        }

        private static LevelDto Board(uint seed, double difficulty, string[] mechanics,
            NodeDto[] nodes, EdgeDto[] edges, SourceDto[] sources, StationDto[] stations,
            SwitchDto[] switches, WaveDto[] waves, WinDto win)
        {
            return new LevelDto(2, "L800", "Daily Line", seed,
                new MetaDto("daily", difficulty, mechanics, null,
                    "Route each cat to the matching station", 12, "generator+validator", null, false),
                nodes, edges, sources, stations, switches, waves, win, new EconomyDto(100, 0));
        }

        private static NodeDto Node(string id, int x, int y)
        {
            return new NodeDto(id, x, y, 0, false);
        }

        private static NodeDto Node(string id, int x, int y, int queueCapacity)
        {
            return new NodeDto(id, x, y, queueCapacity, true);
        }

        private static EdgeDto Edge(string id, string from, string to, int travelTicks)
        {
            return new EdgeDto(id, from, to, travelTicks);
        }
    }
}
