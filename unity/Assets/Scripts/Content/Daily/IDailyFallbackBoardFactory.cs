namespace CatMetro.Content.Daily
{
    public interface IDailyFallbackBoardFactory
    {
        LevelDto BuildFallback(uint seed, string dateKey);
    }
}
