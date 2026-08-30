using System;

namespace CatMetro.Services.Cosmetics
{
    public sealed class CosmeticAccessResolver
    {
        private Purchases.PurchaseService _purchases;

        public CosmeticAccessResolver(Purchases.PurchaseService purchases)
        {
            _purchases = purchases;
        }

        public void BindPurchases(Purchases.PurchaseService purchases)
        {
            _purchases = purchases;
        }

        public bool IsAccessible(CosmeticItemDefinition item, CosmeticProfileSnapshot profile)
        {
            if (item == null || profile == null) return false;

            switch (item.Acquisition)
            {
                case CosmeticAcquisition.Starter:
                    return true;
                case CosmeticAcquisition.Earned:
                    return Contains(profile.EarnedItemIds, item.Id);
                case CosmeticAcquisition.Entitlement:
                    return _purchases != null && _purchases.IsUnlocked(item.EntitlementId);
                default:
                    return false;
            }
        }

        private static bool Contains(System.Collections.Generic.IReadOnlyList<string> ids,
            string id)
        {
            for (int i = 0; i < ids.Count; i++)
                if (string.Equals(ids[i], id, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
