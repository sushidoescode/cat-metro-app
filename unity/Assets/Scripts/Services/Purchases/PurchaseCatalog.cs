using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace CatMetro.Services.Purchases
{
    // The parsed contents of Resources/Monetization/product_catalog.json.
    //
    // The whole type is built around one rule: A BROKEN CATALOGUE MUST NOT BREAK THE GAME.
    // Nothing here throws. A file that is missing, empty, truncated, hand-edited into invalid
    // JSON, or shipped with a typo'd entitlement reference yields an EMPTY catalogue plus a list
    // of Problems — and an empty catalogue means the shop shows nothing and every cosmetic stays
    // locked, which is a bad day but a playable one. The alternative (an exception on a
    // background parse during boot) is a black screen.
    public sealed class PurchaseCatalog
    {
        public static readonly PurchaseCatalog Empty =
            new PurchaseCatalog(Array.Empty<PurchaseCatalogEntry>(),
                Array.Empty<EntitlementDefinition>(), Array.Empty<string>());

        private readonly Dictionary<string, PurchaseCatalogEntry> _byProductId;
        private readonly Dictionary<string, EntitlementDefinition> _byEntitlementId;

        public IReadOnlyList<PurchaseCatalogEntry> Products { get; }
        public IReadOnlyList<EntitlementDefinition> Entitlements { get; }

        // Non-fatal complaints from the parse. Empty means the file was clean. These are meant
        // to be logged loudly on device and asserted on in tests — a silently-degraded catalogue
        // is the exact failure mode AGENTS.md warns about with CatModelCatalog.
        public IReadOnlyList<string> Problems { get; }

        public bool IsEmpty => Products.Count == 0;

        private PurchaseCatalog(IReadOnlyList<PurchaseCatalogEntry> products,
            IReadOnlyList<EntitlementDefinition> entitlements, IReadOnlyList<string> problems)
        {
            Products = products;
            Entitlements = entitlements;
            Problems = problems;

            _byProductId = new Dictionary<string, PurchaseCatalogEntry>(products.Count, StringComparer.Ordinal);
            for (int i = 0; i < products.Count; i++) _byProductId[products[i].Id] = products[i];

            _byEntitlementId =
                new Dictionary<string, EntitlementDefinition>(entitlements.Count, StringComparer.Ordinal);
            for (int i = 0; i < entitlements.Count; i++)
                _byEntitlementId[entitlements[i].Id] = entitlements[i];
        }

        public bool TryGetProduct(string productId, out PurchaseCatalogEntry entry)
        {
            if (productId == null) { entry = default; return false; }
            return _byProductId.TryGetValue(productId, out entry);
        }

        public bool TryGetEntitlement(string entitlementId, out EntitlementDefinition definition)
        {
            if (entitlementId == null) { definition = default; return false; }
            return _byEntitlementId.TryGetValue(entitlementId, out definition);
        }

        // Every entitlement a successful purchase of this product should grant. Unknown product
        // -> empty, never null, never a throw: the store is allowed to know about products we do
        // not, and the correct response to that is to grant nothing rather than to crash.
        public IReadOnlyList<string> EntitlementsFor(string productId)
            => TryGetProduct(productId, out var entry)
                ? entry.Entitlements
                : Array.Empty<string>();

        // ---- parsing -------------------------------------------------------------------

        // Never throws. `json` may be null, empty, or garbage.
        public static PurchaseCatalog Parse(string json)
        {
            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(json))
            {
                problems.Add("catalogue source was null or empty");
                return new PurchaseCatalog(Array.Empty<PurchaseCatalogEntry>(),
                    Array.Empty<EntitlementDefinition>(), problems);
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception e)
            {
                // Newtonsoft throws JsonReaderException, but catching broadly is the point: this
                // method's contract to its callers is "returns, always".
                problems.Add("catalogue is not valid JSON: " + e.Message);
                return new PurchaseCatalog(Array.Empty<PurchaseCatalogEntry>(),
                    Array.Empty<EntitlementDefinition>(), problems);
            }

            var entitlements = ParseEntitlements(root["entitlements"] as JArray, problems);
            var declared = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entitlements.Count; i++) declared.Add(entitlements[i].Id);

            var products = ParseProducts(root["products"] as JArray, declared, problems);

            if (products.Count == 0 && problems.Count == 0)
                problems.Add("catalogue parsed cleanly but declares no products");

            return new PurchaseCatalog(products, entitlements, problems);
        }

        private static List<EntitlementDefinition> ParseEntitlements(JArray array, List<string> problems)
        {
            var result = new List<EntitlementDefinition>();
            if (array == null)
            {
                problems.Add("catalogue has no \"entitlements\" array");
                return result;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in array)
            {
                if (!(token is JObject o)) { problems.Add("entitlement entry is not an object"); continue; }

                var id = (string)o["id"];
                if (string.IsNullOrWhiteSpace(id)) { problems.Add("entitlement entry has no id"); continue; }
                if (!seen.Add(id)) { problems.Add("duplicate entitlement id: " + id); continue; }

                var kind = ParseKind((string)o["kind"], id, problems);
                var display = (string)o["display"] ?? id;

                // A negative lease is a data error, not a shorter lease. Clamp to "no ad grant"
                // rather than silently producing an entitlement that expires before it is given.
                int lease = 0;
                var leaseToken = o["adLeaseSeconds"];
                if (leaseToken != null && leaseToken.Type != JTokenType.Null)
                {
                    try { lease = (int)leaseToken; }
                    catch (Exception) { problems.Add("entitlement " + id + " has a non-numeric adLeaseSeconds"); }
                    if (lease < 0)
                    {
                        problems.Add("entitlement " + id + " has a negative adLeaseSeconds; treating as not ad-grantable");
                        lease = 0;
                    }
                }

                result.Add(new EntitlementDefinition(id, kind, display, lease));
            }

            return result;
        }

        private static EntitlementKind ParseKind(string raw, string entitlementId, List<string> problems)
        {
            switch (raw)
            {
                case "outfit": return EntitlementKind.Outfit;
                case "accessory": return EntitlementKind.Accessory;
                case "frame": return EntitlementKind.Frame;
                case "membership": return EntitlementKind.Membership;
                default:
                    problems.Add("entitlement " + entitlementId + " has unknown kind: " + (raw ?? "<missing>"));
                    return EntitlementKind.Unknown;
            }
        }

        private static List<PurchaseCatalogEntry> ParseProducts(JArray array,
            HashSet<string> declaredEntitlements, List<string> problems)
        {
            var result = new List<PurchaseCatalogEntry>();
            if (array == null)
            {
                problems.Add("catalogue has no \"products\" array");
                return result;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in array)
            {
                if (!(token is JObject o)) { problems.Add("product entry is not an object"); continue; }

                var id = (string)o["id"];
                if (string.IsNullOrWhiteSpace(id)) { problems.Add("product entry has no id"); continue; }
                if (!seen.Add(id)) { problems.Add("duplicate product id: " + id); continue; }

                if (!TryParseStoreType((string)o["storeType"], out var storeType))
                {
                    problems.Add("product " + id + " has unknown storeType: " +
                                 ((string)o["storeType"] ?? "<missing>"));
                    continue; // a product we cannot classify is a product we must not sell
                }

                var granted = new List<string>();
                if (o["entitlements"] is JArray ents)
                {
                    foreach (var e in ents)
                    {
                        var entId = (string)e;
                        if (string.IsNullOrWhiteSpace(entId)) { problems.Add("product " + id + " lists an empty entitlement"); continue; }

                        // The check that actually catches real mistakes: a product promising an
                        // entitlement nobody declared would take the player's money and unlock
                        // nothing. Drop the reference and complain rather than ship the promise.
                        if (!declaredEntitlements.Contains(entId))
                        {
                            problems.Add("product " + id + " grants undeclared entitlement: " + entId);
                            continue;
                        }

                        granted.Add(entId);
                    }
                }

                // A subscription that unlocks nothing is always a data error — you cannot sell
                // recurring access to nothing. A consumable legitimately may grant nothing
                // durable (it is spent immediately), so it is not checked here.
                if (storeType == PurchaseStoreType.Subscription && granted.Count == 0)
                    problems.Add("subscription " + id + " grants no entitlements");

                if (storeType == PurchaseStoreType.NonConsumable && granted.Count == 0)
                    problems.Add("non-consumable " + id + " grants no entitlements");

                result.Add(new PurchaseCatalogEntry(id, storeType, granted, (string)o["display"] ?? id));
            }

            return result;
        }

        private static bool TryParseStoreType(string raw, out PurchaseStoreType type)
        {
            switch (raw)
            {
                case "non_consumable": type = PurchaseStoreType.NonConsumable; return true;
                case "consumable": type = PurchaseStoreType.Consumable; return true;
                case "subscription": type = PurchaseStoreType.Subscription; return true;
                default: type = default; return false;
            }
        }
    }
}
