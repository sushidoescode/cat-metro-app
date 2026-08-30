using System;
using System.Collections.Generic;
using CatMetro.Services.Purchases;

namespace CatMetro.Services.Cosmetics
{
    public enum CosmeticWardrobeRoute
    {
        None,
        Equip,
        Purchase,
        Rewarded,
        EarnInstruction,
    }

    public readonly struct CosmeticWardrobeRow
    {
        public CosmeticItemDefinition Item { get; }
        public bool IsAccessible { get; }
        public bool IsEquipped { get; }
        public long SecondsRemaining { get; }
        public CosmeticWardrobeRoute Route { get; }
        public LocalizedPrice Price { get; }

        public CosmeticWardrobeRow(CosmeticItemDefinition item, bool isAccessible,
            bool isEquipped, long secondsRemaining, CosmeticWardrobeRoute route,
            LocalizedPrice price)
        {
            Item = item;
            IsAccessible = isAccessible;
            IsEquipped = isEquipped;
            SecondsRemaining = secondsRemaining;
            Route = route;
            Price = price;
        }
    }

    public static class CosmeticWardrobeProjection
    {
        public static IReadOnlyList<CosmeticWardrobeRow> Build(
            CosmeticCatalog catalog,
            CosmeticProfileService profile,
            PurchaseService purchases,
            ICosmeticRewardedRoute rewarded,
            string catId,
            CosmeticSlot slot)
        {
            if (catalog == null || profile == null || string.IsNullOrEmpty(catId))
                return Array.Empty<CosmeticWardrobeRow>();

            var rows = new List<CosmeticWardrobeRow>();
            var desiredItemId = profile.Profile.LoadoutFor(catId).ItemFor(slot);

            for (int i = 0; i < catalog.Items.Count; i++)
            {
                var item = catalog.Items[i];
                if (item.Slot != slot || !IsCompatible(item, catId)) continue;
                if (!profile.TryGetPortraitAsset(item.PortraitAssetId, out _)) continue;

                bool isAccessible = profile.IsAccessible(item.Id);
                bool isEquipped = isAccessible
                    && string.Equals(desiredItemId, item.Id, StringComparison.Ordinal);
                LocalizedPrice price = default;
                bool hasPrice = purchases != null
                    && !string.IsNullOrEmpty(item.ProductId)
                    && purchases.TryGetPrice(item.ProductId, out price);

                if (isAccessible)
                {
                    long secondsRemaining = purchases != null
                        && item.Acquisition == CosmeticAcquisition.Entitlement
                        ? purchases.SecondsUntilExpiry(item.EntitlementId)
                        : 0L;
                    var route = hasPrice && secondsRemaining > 0L
                        ? CosmeticWardrobeRoute.Purchase
                        : isEquipped
                            ? CosmeticWardrobeRoute.None
                            : CosmeticWardrobeRoute.Equip;
                    rows.Add(new CosmeticWardrobeRow(item, true, isEquipped,
                        secondsRemaining, route,
                        route == CosmeticWardrobeRoute.Purchase ? price : default));
                    continue;
                }

                if (item.Acquisition == CosmeticAcquisition.Earned)
                {
                    rows.Add(new CosmeticWardrobeRow(item, false, isEquipped, 0L,
                        CosmeticWardrobeRoute.EarnInstruction, default));
                    continue;
                }

                if (item.Acquisition != CosmeticAcquisition.Entitlement) continue;

                if (hasPrice)
                {
                    rows.Add(new CosmeticWardrobeRow(item, false, isEquipped, 0L,
                        CosmeticWardrobeRoute.Purchase, price));
                    continue;
                }

                if (!string.IsNullOrEmpty(item.RewardedPlacementId)
                    && rewarded != null
                    && rewarded.CanOffer(item.RewardedPlacementId, item.EntitlementId))
                {
                    rows.Add(new CosmeticWardrobeRow(item, false, isEquipped, 0L,
                        CosmeticWardrobeRoute.Rewarded, default));
                }
            }

            return rows.AsReadOnly();
        }

        private static bool IsCompatible(CosmeticItemDefinition item, string catId)
        {
            for (int i = 0; i < item.CompatibleCatIds.Count; i++)
                if (string.Equals(item.CompatibleCatIds[i], catId,
                    StringComparison.Ordinal))
                    return true;
            return false;
        }
    }
}
