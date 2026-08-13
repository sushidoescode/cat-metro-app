namespace CatMetro.Content.Daily
{
    public sealed class HistoricalDailySeedScheme : IDailySeedScheme
    {
        public static readonly HistoricalDailySeedScheme Instance = new HistoricalDailySeedScheme();

        public string ArtifactLabel => DailySeed.GENERATOR_CONSTANT;

        private HistoricalDailySeedScheme() { }

        public uint Derive(string dateKey, int k) => DailySeed.Derive(dateKey, k);
    }

    public sealed class DailyLineSeedScheme : IDailySeedScheme
    {
        public static readonly DailyLineSeedScheme Instance = new DailyLineSeedScheme();

        public string ArtifactLabel => DailyLineSeed.GeneratorLabel;

        private DailyLineSeedScheme() { }

        public uint Derive(string dateKey, int k)
        {
            if (k < 0)
                throw new System.ArgumentException("salt k must be >= 0, got " + k);
            return DailyLineSeed.Derive(dateKey);
        }
    }
}
