using System;
using CatMetro.Services.Ads;

namespace CatMetro.Services.Retry
{
    public interface IFailureRewindRewardedRoute
    {
        event Action AvailabilityChanged;
        bool CanOffer(string placementId);
        void Request(string placementId, Action<RewardedAdEvent> providerLifecycle,
            Action<FailureRewindAdCompletion> completed);
        void Cancel();
    }
}
