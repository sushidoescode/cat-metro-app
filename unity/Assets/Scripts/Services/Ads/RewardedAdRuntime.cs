using System;

namespace CatMetro.Services.Ads
{
    // Runtime publication mirrors PurchaseRuntime: UI always receives a safe object and does not
    // need a second branch for optional monetization composition.
    public static class RewardedAdRuntime
    {
        private static IRewardedAds _current;

        public static IRewardedAds Current => _current ??= NoRewardedAds.Instance;
        public static bool IsInstalled { get; private set; }
        public static event Action Installed;

        public static void Install(IRewardedAds rewardedAds)
        {
            if (rewardedAds == null) return;
            _current = rewardedAds;
            IsInstalled = true;
            Installed?.Invoke();
        }

        public static void ResetForTests()
        {
            _current = NoRewardedAds.Instance;
            IsInstalled = false;
            Installed = null;
        }

        private sealed class NoRewardedAds : IRewardedAds
        {
            public static readonly NoRewardedAds Instance = new NoRewardedAds();
            public event Action AvailabilityChanged { add { } remove { } }
            public bool CanShow(string placementId) => false;
            public RewardedShowOutcome Show(string placementId) => RewardedShowOutcome.Unavailable;
        }
    }
}
