using System.Collections.Generic;

namespace CatMetro.Services.Purchases
{
    public enum RewardedPlacementIdentifier
    {
        RewindFailure,
        DoubleTickets,
        DailyGiftDouble,
        StreakRepair,
        ThemeRental,
        CatSkinTrial,
        LiveryTrial,
        DistrictGuestRoute
    }

    public readonly struct RewardCap
    {
        public readonly string Scope;
        public readonly int Limit;

        public RewardCap(string scope, int limit)
        {
            Scope = scope;
            Limit = limit;
        }
    }

    public readonly struct RewardedPlacement
    {
        public readonly RewardedPlacementIdentifier Identifier;
        public readonly string Reward;
        public readonly IReadOnlyList<RewardCap> Caps;
        public readonly bool SdkCallEnabled;
        public readonly string DisabledReason;

        public RewardedPlacement(RewardedPlacementIdentifier identifier, string reward,
            IReadOnlyList<RewardCap> caps, bool sdkCallEnabled, string disabledReason)
        {
            Identifier = identifier;
            Reward = reward;
            Caps = caps;
            SdkCallEnabled = sdkCallEnabled;
            DisabledReason = disabledReason;
        }
    }
}
