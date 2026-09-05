using CatMetro.Services;

namespace CatMetro.Application.EventTaxonomy
{
    // Metrics only: callers retain offer eligibility, callback identity, and gameplay grants.
    public static class FailureRewindAnalytics
    {
        public static void OfferViewed(IAnalytics analytics, string placement, string levelId) =>
            SafeLog(analytics, Events.AdOfferViewed(placement, levelId));

        public static void OfferDeclined(IAnalytics analytics, string placement) =>
            SafeLog(analytics, Events.AdOfferDeclined(placement));

        public static void AdDisplayed(IAnalytics analytics, string placement, string network,
            string adUnit) =>
            SafeLog(analytics, Events.RewardedAdStarted(placement,
                MetadataOrUnknown(network), MetadataOrUnknown(adUnit)));

        public static void AdCompleted(IAnalytics analytics, string placement, string network) =>
            SafeLog(analytics, Events.RewardedAdCompleted(placement,
                MetadataOrUnknown(network), "rewind", 1));

        public static void AdFailed(IAnalytics analytics, string placement, string network,
            string errorCode) =>
            SafeLog(analytics, Events.RewardedAdFailed(placement,
                MetadataOrUnknown(network), errorCode));

        public static void RewindApplied(IAnalytics analytics, string levelId, string balanceAfter) =>
            SafeLog(analytics, Events.RewindUsed(levelId, "rewarded", balanceAfter));

        private static string MetadataOrUnknown(string value) =>
            string.IsNullOrEmpty(value) ? "unknown" : value;

        private static void SafeLog(IAnalytics analytics, in AnalyticsEvent e)
        {
            // Analytics cannot interfere with cancellation, retry, or an earned gameplay grant.
            try { analytics?.Log(e); }
            catch { }
        }
    }
}
