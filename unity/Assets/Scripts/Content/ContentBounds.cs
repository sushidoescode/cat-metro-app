namespace CatMetro.Content
{
    // CM-C2a criterion 5: one constants class, every number cited. Runtime bounds validation is
    // independent of the CI schema gate because CI only validates what CI saw (ADR-0008 MUST 2).
    // config/runtime_bounds.json does NOT exist yet and is NOT authored here (Q-T -> CM-C7).
    public static class ContentBounds
    {
        public const int CONTENT_MAX_FILE_BYTES = 262144;      // docs/adr/0006-save-format-purchase-ledger-and-runtime-bounds.md:190
        public const int CONTENT_MAX_JSON_DEPTH = 16;          // docs/adr/0006-save-format-purchase-ledger-and-runtime-bounds.md:191
        public const int MAX_NODES = 40;                       // docs/plan/data/level_schema.json:34
        public const int MAX_EDGES = 70;                       // docs/plan/data/level_schema.json:45
        public const int MAX_WAVES = 30;                       // docs/plan/data/level_schema.json:108
        public const int MAX_SWITCHES = 10;                    // docs/plan/data/level_schema.json:81
        public const int MAX_SOURCES = 6;                      // docs/plan/data/level_schema.json:60
        public const int MAX_STATIONS = 6;                     // docs/plan/data/level_schema.json:70
        public const int TRAVEL_TICKS_MIN = 1;                 // docs/plan/data/level_schema.json:51
        public const int TRAVEL_TICKS_MAX = 40;                // docs/plan/data/level_schema.json:51
        public const int TIME_LIMIT_TICKS_MIN = 20;            // docs/plan/data/level_schema.json:125
        public const int TIME_LIMIT_TICKS_MAX = 4000;          // docs/plan/data/level_schema.json:125
        public const int PERFECT_MAX_SWITCHES_MIN = 0;         // docs/plan/data/level_schema.json:126
        public const int PERFECT_MAX_SWITCHES_MAX = 200;       // docs/plan/data/level_schema.json:126
        public const int QUEUE_CAPACITY_MIN = 1;               // docs/plan/data/level_schema.json:40
        public const int QUEUE_CAPACITY_MAX = 8;               // docs/plan/data/level_schema.json:40
        public const int STATION_CAPACITY_MIN = 1;             // docs/plan/data/level_schema.json:76
        public const int STATION_CAPACITY_MAX = 12;            // docs/plan/data/level_schema.json:76
        public const int INITIAL_ROUTE_MIN = 0;                // docs/plan/data/level_schema.json:88
        public const int INITIAL_ROUTE_MAX = 2;                // docs/plan/data/level_schema.json:88
        public const int ROUTES_MIN = 2;                       // docs/plan/data/level_schema.json:87
        public const int ROUTES_MAX = 3;                       // docs/plan/data/level_schema.json:87
        public const int WAVE_COUNT_MIN = 1;                   // docs/plan/data/level_schema.json:115
        public const int WAVE_COUNT_MAX = 8;                   // docs/plan/data/level_schema.json:115
        public const int SPACING_TICKS_MIN = 1;                // docs/plan/data/level_schema.json:116
        public const int SPACING_TICKS_MAX = 40;               // docs/plan/data/level_schema.json:116
        public const int MIN_ACTION_WINDOW_TICKS_FLOOR = 3;    // docs/plan/data/level_schema.json:23
    }
}
