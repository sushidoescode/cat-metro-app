namespace CatMetro.Content.Daily
{
    // Q3 (human-ruled, PR #73): the runtime Daily board generator is IN scope — exactly ONE
    // shipped implementation of this interface exists under the Daily root (DailyBoardFactory,
    // deterministic template-parametric; grep-asserted as exactly-one by
    // tests/daily/daily-pipeline.test.sh, so a second implementation cannot land silently).
    // Historical note: under CM-C6 criterion 8 (Q-S) this interface shipped with ZERO
    // implementations and the same wrapper asserted that gap; the Q3 ruling re-authored the
    // assertion from zero to exactly-one. Tests supply stubs; the DailyTools host still supplies
    // its fixed-board harness stub importing an existing corpus level (A-C6-9) — zero shaping
    // rules there.
    public interface IBoardFactory
    {
        LevelDto Build(uint seed, string dateKey, int k);
    }
}
