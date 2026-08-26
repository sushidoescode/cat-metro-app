using System.Collections.Generic;

namespace CatMetro.Services.Purchases
{
    public enum PurchaseStoreType
    {
        NonConsumable,
        Consumable
    }

    public enum PurchasePlacement
    {
        PostLevel5,
        ThemePreview,
        BonusDistrict,
        Shop,
        RewindFailure
    }

    public readonly struct PurchaseCatalogEntry
    {
        public readonly ProductIdentifier Product;
        public readonly PurchaseStoreType StoreType;
        public readonly IReadOnlyList<string> Entitlements;

        public PurchaseCatalogEntry(ProductIdentifier product, PurchaseStoreType storeType,
            IReadOnlyList<string> entitlements)
        {
            Product = product;
            StoreType = storeType;
            Entitlements = entitlements;
        }
    }
}
