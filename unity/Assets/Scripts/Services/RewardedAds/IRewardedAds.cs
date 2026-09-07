using System;

namespace CatMetro.Services.Ads
{
    public enum RewardedShowOutcome
    {
        Started,
        Unavailable,
        Busy,
    }

    public enum RewardedAdCompletionKind
    {
        Granted,
        ClosedWithoutReward,
        DisplayFailed,
        Unavailable,
        GrantFailed,
        Cancelled,
    }

    public readonly struct RewardedAdCompletion
    {
        public long AttemptId { get; }
        public string PlacementId { get; }
        public string EntitlementId { get; }
        public RewardedAdCompletionKind Kind { get; }

        public RewardedAdCompletion(long attemptId, string placementId, string entitlementId,
            RewardedAdCompletionKind kind)
        {
            AttemptId = attemptId;
            PlacementId = placementId;
            EntitlementId = entitlementId;
            Kind = kind;
        }
    }

    // Optional capability: existing IRewardedAds consumers remain source-compatible.
    public interface IRewardedAdExactCompletionSource
    {
        bool CanShow(string placementId, string entitlementId);
        RewardedShowOutcome Show(string placementId, string entitlementId,
            Action<RewardedAdCompletion> completed);
    }

    // Failure rewind consumes durable gameplay caps and never represents an entitlement lease.
    // Metadata comes from the actual provider callback; absent metadata stays absent.
    public readonly struct FailureRewindAdCompletion
    {
        public long AttemptId { get; }
        public string PlacementId { get; }
        public RewardedAdCompletionKind Kind { get; }
        public string AdUnitId { get; }
        public string NetworkName { get; }
        public int? ErrorCode { get; }

        public FailureRewindAdCompletion(long attemptId, string placementId,
            RewardedAdCompletionKind kind, string adUnitId = null, string networkName = null,
            int? errorCode = null)
        {
            AttemptId = attemptId;
            PlacementId = placementId;
            Kind = kind;
            AdUnitId = adUnitId;
            NetworkName = networkName;
            ErrorCode = errorCode;
        }
    }

    public interface IRewardedAdFailureRewindSource
    {
        bool CanShowFailureRewind(string placementId);
        // Exact request validity is separate from offer/next-fill readiness: opening an ad
        // normally makes CanShow false before Displayed. Genuine pre-display invalidation
        // returns false; a displayed/earned ad keeps its existing completion route.
        bool CanContinueFailureRewind(long attemptId, string placementId);
        // Assign attemptId before invoking any provider/lifecycle callback, so reentrant route
        // cancellation can abandon that exact attempt even before Show has returned.
        RewardedShowOutcome ShowFailureRewind(string placementId,
            Action<RewardedAdEvent> providerLifecycle, Action<FailureRewindAdCompletion> completed,
            out long attemptId);
        void AbandonFailureRewind(long attemptId);
    }

    // Presentation's complete rewarded-ad surface. Availability is optional; gameplay and the
    // purchase path remain usable when every answer here fails closed.
    public interface IRewardedAds
    {
        event Action AvailabilityChanged;
        bool CanShow(string placementId);
        RewardedShowOutcome Show(string placementId);
    }
}
