using System;

namespace CatMetro.Services.Ads
{
    public enum RewardedAdEventKind
    {
        Loaded,
        LoadFailed,
        Displayed,
        DisplayFailed,
        Rewarded,
        Closed,
        Opened,
        Revenue,
    }

    public enum AdRevenuePrecision
    {
        Exact,
        PublisherDefined,
        Estimated,
        Unknown,
    }

    // Immutable CLR-only callback data. AdId and AuctionId intentionally remain distinct:
    // neither is an ad-unit identifier, and downstream impression reporting must not invent one
    // from another.
    public readonly struct RewardedAdEvent
    {
        public RewardedAdEventKind Kind { get; }
        public long AttemptId { get; }
        public string PlacementId { get; }
        public string AdUnitId { get; }
        public string AdId { get; }
        public string AuctionId { get; }
        public string NetworkName { get; }
        public int? ErrorCode { get; }
        public long RevenueMicros { get; }
        public string Currency { get; }
        public AdRevenuePrecision RevenuePrecision { get; }

        public RewardedAdEvent(RewardedAdEventKind kind, long attemptId = 0L,
            string placementId = null, string adUnitId = null, string adId = null,
            string auctionId = null, string networkName = null, int? errorCode = null,
            long revenueMicros = 0L, string currency = null,
            AdRevenuePrecision revenuePrecision = AdRevenuePrecision.Unknown)
        {
            Kind = kind;
            AttemptId = attemptId;
            PlacementId = placementId;
            AdUnitId = adUnitId;
            AdId = adId;
            AuctionId = auctionId;
            NetworkName = networkName;
            ErrorCode = errorCode;
            RevenueMicros = revenueMicros;
            Currency = currency;
            RevenuePrecision = revenuePrecision;
        }
    }

    // The serving adapter is owned by exactly one coordinator. Disposal is explicit so a future
    // native adapter can unsubscribe deterministically; implementations must tolerate repeats.
    public interface IRewardedAdProvider : IDisposable
    {
        event Action<RewardedAdEvent> EventReceived;
        bool IsReady { get; }
        void Initialize();
        void Load();
        bool TryShow(long attemptId, string placementId);
    }

    // Optional provider capability for SDK-owned per-placement caps. The base property remains
    // available to package-free providers; a placement-aware provider is preferred by policy.
    public interface IRewardedAdPlacementReadiness
    {
        bool IsReadyForPlacement(string placementId);
    }

    // Worker callbacks enqueue copied neutral values; the existing Unity frame owner calls this
    // capability before any reporter or other Unity-facing consumer observes them.
    public interface IMainThreadRewardedAdEventDrain
    {
        void DrainMainThreadEvents();
    }

    public interface IAdEventReporter
    {
        event Action ReadinessChanged;
        bool IsReady { get; }
        void Report(RewardedAdEvent adEvent);
    }

    public interface IRewardedAdCapStore
    {
        int ReadLocalDateCount(string placementId, string localDateKey);
        bool TryIncrementLocalDateCount(string placementId, string localDateKey);
    }
}
