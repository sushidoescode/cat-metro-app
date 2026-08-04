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
            throw new System.NotImplementedException();
        }
    }
}
