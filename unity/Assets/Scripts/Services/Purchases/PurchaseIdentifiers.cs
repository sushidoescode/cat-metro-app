namespace CatMetro.Services.Purchases
{
    // Well-known identifiers. These are CONSTANTS, not an enum: the rescued foundation used
    // `enum ProductIdentifier`, which fights the data-driven catalogue it shipped alongside —
    // adding a SKU would have meant a code change plus a store change plus a JSON change, and
    // any id the store returned that the enum did not know about had nowhere to go. Product ids
    // are now plain strings validated at parse time against product_catalog.json, and the only
    // ids that appear in code are the handful below that presentation code must name directly.
    //
    // Naming: `cm_` prefixes a store product id (what Google Play / App Store sell); an
    // entitlement id has no prefix (what the game asks about). One product may grant several
    // entitlements — that is what the Stationmaster bundle is for.
    public static class ProductIds
    {
        // The launch cosmetic set: outfits and accessories for the player's profile cat, and
        // frames that sit around its portrait. All non-consumable — bought once, owned forever,
        // and therefore restorable, which is why RestorePurchases is not optional (see
        // PurchaseService.Restore).
        public const string OutfitConductor = "cm_outfit_conductor";
        public const string OutfitEngineer = "cm_outfit_engineer";
        public const string AccessoryScarf = "cm_accessory_scarf";
        public const string AccessoryGoggles = "cm_accessory_goggles";
        public const string FrameBrass = "cm_frame_brass";
        public const string FrameLantern = "cm_frame_lantern";
        public const string BundleStationmaster = "cm_bundle_stationmaster";

        // The single SKU wired end to end first, and the one that satisfies the Shipaton
        // "RevenueCat SDK powers at least one purchase" rule. Chosen because a single
        // non-consumable is the least store configuration that can possibly work: one managed
        // product in Play Console, one RevenueCat product, one entitlement, one package.
        public const string Gate = OutfitConductor;
    }

    public static class EntitlementIds
    {
        public const string OutfitConductor = "outfit_conductor";
        public const string OutfitEngineer = "outfit_engineer";
        public const string AccessoryScarf = "accessory_scarf";
        public const string AccessoryGoggles = "accessory_goggles";
        public const string FrameBrass = "frame_brass";
        public const string FrameLantern = "frame_lantern";
        public const string Supporter = "supporter";
    }

    // The RevenueCat-side names. Kept beside the product ids because they have to be typed
    // identically into the RevenueCat dashboard by hand (see docs/runbooks/revenuecat-setup.md)
    // and a typo there is invisible until a device test fails — the offering simply comes back
    // empty, with no error.
    public static class RevenueCatNames
    {
        // Offering identifier. The catalogue is fetched by this name rather than by "current"
        // so that flipping the dashboard's current offering cannot silently empty our shop.
        public const string CosmeticsOffering = "cosmetics";
    }
}
