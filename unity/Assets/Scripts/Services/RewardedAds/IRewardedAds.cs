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

    // Presentation's complete rewarded-ad surface. Availability is optional; gameplay and the
    // purchase path remain usable when every answer here fails closed.
    public interface IRewardedAds
    {
        event Action AvailabilityChanged;
        bool CanShow(string placementId);
        RewardedShowOutcome Show(string placementId);
    }
}
