namespace CatMetro.Content.Daily
{
    // CM-C6 criterion 8 (Q-S): the board generator is OUT OF SCOPE — the corpus specifies no
    // board-shaping rule anywhere, so no shipped implementation of this interface may exist under
    // the Daily root (grep-asserted by tests/daily/daily-pipeline.test.sh, so the gap cannot be
    // silently filled). Tests supply stubs; the DailyTools host supplies a fixed-board harness
    // stub that imports an existing corpus level (A-C6-9) — zero shaping rules there either.
    public interface IBoardFactory
    {
        LevelDto Build(uint seed, string dateKey, int k);
    }
}
