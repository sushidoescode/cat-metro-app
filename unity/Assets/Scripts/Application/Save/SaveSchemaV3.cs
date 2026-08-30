using Newtonsoft.Json.Linq;

namespace CatMetro.Application.Save
{
    // Cosmetics are profile-owned. This migration deliberately does not inspect entitlement
    // leases: their schema and values remain opaque to the cosmetics lane.
    public static class SaveSchemaV3
    {
        public static JObject DefaultCosmetics() => new JObject
        {
            ["selectedCatId"] = "red_tabby",
            ["earnedCatIds"] = new JArray(),
            ["earnedItemIds"] = new JArray(),
            ["loadouts"] = new JArray(Loadout("red_tabby")),
        };

        public static JObject MigrateFromV2(JObject payload)
        {
            if (payload == null) return null;
            if (payload["profile"] != null && !(payload["profile"] is JObject)) return null;
            var profile = payload["profile"] as JObject ?? new JObject();
            if (payload["profile"] == null) payload["profile"] = profile;
            if (profile["cosmetics"] != null && !(profile["cosmetics"] is JObject)) return null;
            if (profile["cosmetics"] == null) profile["cosmetics"] = DefaultCosmetics();
            return payload;
        }

        private static JObject Loadout(string catId) => new JObject
        {
            ["catId"] = catId,
            ["outfitId"] = "",
            ["accessoryId"] = "",
            ["frameId"] = "",
        };
    }
}
