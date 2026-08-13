namespace CatMetro.Content.Daily
{
    public static class DailyDifficulty
    {
        public static double Target(string dateKey)
        {
            switch (DateKeys.WeekdayIndex(dateKey))
            {
                case 0: return 0.30;
                case 1: return 0.35;
                case 2: return 0.38;
                case 3: return 0.42;
                case 4: return 0.45;
                case 5: return 0.50;
                default: return 0.55;
            }
        }
    }
}
