using System;

namespace CatMetro.Services.Cosmetics
{
    public interface ICosmeticRewardedRoute
    {
        bool CanOffer(string placementId, string entitlementId);
        void Request(string placementId, Action completed);
    }

    public sealed class DisabledCosmeticRewardedRoute : ICosmeticRewardedRoute
    {
        public bool CanOffer(string placementId, string entitlementId) => false;

        public void Request(string placementId, Action completed) => completed?.Invoke();
    }
}
