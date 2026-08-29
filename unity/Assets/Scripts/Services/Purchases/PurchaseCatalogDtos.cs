using System.Collections.Generic;

namespace CatMetro.Services.Purchases
{
    // How the store treats a product. This drives real behaviour, not documentation:
    // NonConsumable and Subscription MUST be restorable (both stores require a restore path for
    // them); Consumable must NOT be, because a consumed purchase is gone and asking the store to
    // restore it either returns nothing or double-grants.
    public enum PurchaseStoreType
    {
        NonConsumable,
        Consumable,
        Subscription
    }

    // What KIND of cosmetic an entitlement unlocks. Presentation uses this to decide which
    // wardrobe slot a thing occupies — a cat can wear one outfit and one accessory at a time,
    // and a frame is not worn at all, it surrounds the portrait.
    public enum EntitlementKind
    {
        Unknown,
        Outfit,
        Accessory,
        Frame,
        Membership
    }

    // An entitlement is the thing the GAME asks about ("may this cat wear the conductor coat?").
    // Products grant entitlements; rewarded ads grant the same entitlements on a lease. Keeping
    // the ad lease length on the entitlement rather than on a product is deliberate: an ad does
    // not sell a product, it lends an entitlement, and the lease is a property of the thing lent.
    public readonly struct EntitlementDefinition
    {
        public readonly string Id;
        public readonly EntitlementKind Kind;
        public readonly string DisplayName;

        // 0 means "a rewarded ad can never grant this". Anything > 0 is how long an ad-granted
        // lease lasts, in seconds. The ad lane owns showing the ad; it does not get to invent a
        // lease length, because that is a monetization balance decision that belongs in data.
        public readonly int AdLeaseSeconds;

        public bool IsAdGrantable => AdLeaseSeconds > 0;

        public EntitlementDefinition(string id, EntitlementKind kind, string displayName,
            int adLeaseSeconds)
        {
            Id = id;
            Kind = kind;
            DisplayName = displayName;
            AdLeaseSeconds = adLeaseSeconds;
        }
    }

    // A product is the thing the STORE sells. Its Id is the Google Play / App Store product id
    // AND the RevenueCat product identifier — they are kept identical on purpose, because the
    // one place this pipeline reliably breaks is a mismatch between three consoles.
    public readonly struct PurchaseCatalogEntry
    {
        public readonly string Id;
        public readonly PurchaseStoreType StoreType;
        public readonly IReadOnlyList<string> Entitlements;
        public readonly string DisplayName;

        // Restore is a store capability, not a preference. Consumables are excluded because
        // restoring one is either a no-op or a duplicate grant, never the right thing.
        public bool IsRestorable => StoreType != PurchaseStoreType.Consumable;

        public PurchaseCatalogEntry(string id, PurchaseStoreType storeType,
            IReadOnlyList<string> entitlements, string displayName)
        {
            Id = id;
            StoreType = storeType;
            Entitlements = entitlements;
            DisplayName = displayName;
        }
    }
}
