using System;
using CatMetro.Services.Ads;

namespace CatMetro.Integrations
{
    // Converts callback scalars without retaining any LevelPlay object or type. This stays in the
    // always-compiled assembly so package absence cannot remove its validation path.
    public static class LevelPlayPayloadMapper
    {
        private const double MicrosPerUsd = 1_000_000d;
        private const double LongOverflowBoundary = 9_223_372_036_854_775_808d;

        public static AdRevenuePrecision MapPrecision(string precision)
        {
            if (string.IsNullOrWhiteSpace(precision)) return AdRevenuePrecision.Unknown;
            switch (precision.Trim().ToUpperInvariant())
            {
                case "BID": return AdRevenuePrecision.Exact;
                case "RATE": return AdRevenuePrecision.PublisherDefined;
                case "CPM": return AdRevenuePrecision.Estimated;
                default: return AdRevenuePrecision.Unknown;
            }
        }

        public static bool TryUsdMicros(double value, out long micros)
        {
            micros = 0L;
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d) return false;

            double rounded = Math.Round(value * MicrosPerUsd,
                MidpointRounding.AwayFromZero);
            if (double.IsNaN(rounded) || double.IsInfinity(rounded) ||
                rounded < 0d || rounded >= LongOverflowBoundary)
                return false;

            try
            {
                micros = checked((long)rounded);
                return true;
            }
            catch (OverflowException)
            {
                micros = 0L;
                return false;
            }
        }

        public static RewardedAdEvent CreateLifecycle(RewardedAdEventKind kind,
            long attemptId = 0L, string placementId = null, string adUnitId = null,
            string adId = null, string auctionId = null, string networkName = null,
            int? errorCode = null)
            => new RewardedAdEvent(kind, attemptId, placementId, adUnitId, adId, auctionId,
                networkName, errorCode);

        public static bool TryCreateRevenue(long attemptId, string placementId,
            string adUnitId, string adId, string auctionId, string networkName,
            double revenueUsd, string precision, out RewardedAdEvent adEvent)
        {
            adEvent = default;
            if (string.IsNullOrWhiteSpace(auctionId) ||
                !TryUsdMicros(revenueUsd, out long revenueMicros))
                return false;

            adEvent = new RewardedAdEvent(RewardedAdEventKind.Revenue, attemptId,
                placementId, adUnitId, adId, auctionId, networkName,
                revenueMicros: revenueMicros, currency: "USD",
                revenuePrecision: MapPrecision(precision));
            return true;
        }
    }
}
