using System;

namespace CatMetro.Services.Ads
{
    public enum RewardedShowOutcome
    {
        Started,
        Unavailable,
        Busy,
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
