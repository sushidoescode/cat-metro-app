using System.Collections.Generic;

namespace CatMetro.Services.Purchases
{
    // The persistence boundary for locally-earned rewarded-ad leases. Store and promotional
    // grants remain owned by the purchase backend and must never cross this boundary.
    public interface IEntitlementLeasePersistence
    {
        bool TryReplaceRewardedAdLeases(IReadOnlyList<EntitlementGrant> leases);
    }
}
