using System;

namespace CatMetro.Services.Cosmetics
{
    public enum CosmeticRewardedCompletion
    {
        Granted,
        NotGranted,
    }

    public interface ICosmeticRewardedRoute
    {
        event Action AvailabilityChanged;
        bool CanOffer(string placementId, string entitlementId);
        void Request(string placementId, string entitlementId,
            Action<CosmeticRewardedCompletion> completed);
    }

    public sealed class DisabledCosmeticRewardedRoute : ICosmeticRewardedRoute
    {
        public event Action AvailabilityChanged { add { } remove { } }
        public bool CanOffer(string placementId, string entitlementId) => false;

        public void Request(string placementId, string entitlementId,
            Action<CosmeticRewardedCompletion> completed)
        {
            try { completed?.Invoke(CosmeticRewardedCompletion.NotGranted); }
            catch { }
        }
    }
}
