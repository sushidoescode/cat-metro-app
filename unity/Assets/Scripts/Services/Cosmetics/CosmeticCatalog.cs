using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace CatMetro.Services.Cosmetics
{
    public sealed class CosmeticCatalog
    {
        public static CosmeticCatalog Empty { get; } = new CosmeticCatalog(
            Array.Empty<CosmeticCatDefinition>(), Array.Empty<CosmeticItemDefinition>(), 0,
            Array.Empty<string>());

        private readonly Dictionary<string, CosmeticCatDefinition> _byCatId;
        private readonly Dictionary<string, CosmeticItemDefinition> _byItemId;

        public IReadOnlyList<CosmeticCatDefinition> Cats { get; }
        public IReadOnlyList<CosmeticItemDefinition> Items { get; }
        public int AdmittedRowCount => Items.Count;
        public int RejectedRowCount { get; }
        public int AdmittedCatCount => Cats.Count;
        public IReadOnlyList<string> Problems { get; }

        private CosmeticCatalog(IReadOnlyList<CosmeticCatDefinition> cats,
            IReadOnlyList<CosmeticItemDefinition> items, int rejectedRowCount,
            IReadOnlyList<string> problems)
        {
            Cats = new List<CosmeticCatDefinition>(cats).AsReadOnly();
            Items = new List<CosmeticItemDefinition>(items).AsReadOnly();
            RejectedRowCount = rejectedRowCount;
            Problems = new List<string>(problems).AsReadOnly();

            _byCatId = new Dictionary<string, CosmeticCatDefinition>(cats.Count, StringComparer.Ordinal);
            for (int i = 0; i < cats.Count; i++) _byCatId.Add(cats[i].Id, cats[i]);
            _byItemId = new Dictionary<string, CosmeticItemDefinition>(items.Count, StringComparer.Ordinal);
            for (int i = 0; i < items.Count; i++) _byItemId.Add(items[i].Id, items[i]);
        }

        public bool TryGetCat(string catId, out CosmeticCatDefinition definition)
        {
            if (catId == null)
            {
                definition = null;
                return false;
            }
            return _byCatId.TryGetValue(catId, out definition);
        }

        public bool TryGetItem(string itemId, out CosmeticItemDefinition definition)
        {
            if (itemId == null)
            {
                definition = null;
                return false;
            }
            return _byItemId.TryGetValue(itemId, out definition);
        }

        public static CosmeticCatalog Parse(string json,
            IReadOnlyCollection<string> portraitAssetIds,
            IReadOnlyCollection<string> provenanceAssetIds)
        {
            var problems = new List<string>();
            var root = ParseRoot(json, problems);
            if (root == null || !HasSupportedSchema(root, problems))
                return Failed(problems);

            var assets = ToSet(portraitAssetIds);
            var provenance = ToSet(provenanceAssetIds);
            var cats = ParseCats(root["cats"], assets, provenance, problems);
            var admittedCatIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < cats.Count; i++) admittedCatIds.Add(cats[i].Id);
            var items = ParseItems(root["items"], admittedCatIds, assets, provenance,
                problems, out var rejectedRows);
            StableSortByOrder(items);
            return new CosmeticCatalog(cats, items, rejectedRows, problems);
        }

        private static CosmeticCatalog Failed(IReadOnlyList<string> problems)
            => new CosmeticCatalog(Array.Empty<CosmeticCatDefinition>(),
                Array.Empty<CosmeticItemDefinition>(), 0, problems);

        private static JObject ParseRoot(string json, List<string> problems)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                problems.Add("cosmetics catalogue source was null or empty");
                return null;
            }
            try
            {
                var token = JToken.Parse(json);
                if (token is JObject root) return root;
                problems.Add("cosmetics catalogue root is not an object");
            }
            catch (Exception e)
            {
                problems.Add("cosmetics catalogue is not valid JSON: " + e.Message);
            }
            return null;
        }

        private static bool HasSupportedSchema(JObject root, List<string> problems)
        {
            try
            {
                if (root["schemaVersion"] == null || (int)root["schemaVersion"] != 1)
                {
                    problems.Add("cosmetics catalogue has missing or unsupported schemaVersion");
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                problems.Add("cosmetics catalogue has missing or unsupported schemaVersion");
                return false;
            }
        }

        private static HashSet<string> ToSet(IReadOnlyCollection<string> values)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (values == null) return result;
            foreach (var value in values)
                if (!string.IsNullOrWhiteSpace(value)) result.Add(value);
            return result;
        }

        private static List<CosmeticCatDefinition> ParseCats(JToken token,
            HashSet<string> assets, HashSet<string> provenance, List<string> problems)
        {
            var result = new List<CosmeticCatDefinition>();
            if (!(token is JArray array))
            {
                problems.Add("cosmetics catalogue has no cats array");
                return result;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in array)
            {
                if (!(row is JObject item))
                {
                    problems.Add("cat row is not an object");
                    continue;
                }

                try
                {
                    var id = (string)item["id"];
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        problems.Add("cat row has no id");
                        continue;
                    }
                    if (!seen.Add(id))
                    {
                        problems.Add("duplicate cat id: " + id);
                        continue;
                    }

                    var displayNameKey = (string)item["displayNameKey"];
                    var portraitAssetId = (string)item["portraitAssetId"];
                    if (string.IsNullOrWhiteSpace(displayNameKey))
                    {
                        problems.Add("cat " + id + " has no displayNameKey");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(portraitAssetId))
                    {
                        problems.Add("cat " + id + " has no portraitAssetId");
                        continue;
                    }
                    if (!assets.Contains(portraitAssetId))
                    {
                        problems.Add("cat " + id + " has missing portrait asset: " + portraitAssetId);
                        continue;
                    }
                    if (!provenance.Contains(portraitAssetId))
                    {
                        problems.Add("cat " + id + " has missing provenance: " + portraitAssetId);
                        continue;
                    }
                    if (item["starter"] == null || item["starter"].Type != JTokenType.Boolean)
                    {
                        problems.Add("cat " + id + " has invalid starter flag");
                        continue;
                    }

                    result.Add(new CosmeticCatDefinition(id, displayNameKey, portraitAssetId,
                        (bool)item["starter"]));
                }
                catch (Exception e)
                {
                    problems.Add("cat row has invalid fields: " + e.GetType().Name);
                }
            }
            return result;
        }

        private static List<CosmeticItemDefinition> ParseItems(JToken token,
            HashSet<string> admittedCatIds, HashSet<string> assets, HashSet<string> provenance,
            List<string> problems, out int rejectedRows)
        {
            var result = new List<CosmeticItemDefinition>();
            rejectedRows = 0;
            if (!(token is JArray array))
            {
                problems.Add("cosmetics catalogue has no items array");
                return result;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in array)
            {
                if (!(row is JObject item))
                {
                    rejectedRows++;
                    problems.Add("item row is not an object");
                    continue;
                }

                try
                {
                    if (!TryParseItem(item, seen, admittedCatIds, assets, provenance,
                        out var definition, out var problem))
                    {
                        rejectedRows++;
                        problems.Add(problem);
                        continue;
                    }
                    result.Add(definition);
                }
                catch (Exception e)
                {
                    rejectedRows++;
                    problems.Add("item row has invalid fields: " + e.GetType().Name);
                }
            }
            return result;
        }

        private static bool TryParseItem(JObject item, HashSet<string> seen,
            HashSet<string> admittedCatIds, HashSet<string> assets, HashSet<string> provenance,
            out CosmeticItemDefinition definition, out string problem)
        {
            definition = null;
            var id = (string)item["id"];
            if (string.IsNullOrWhiteSpace(id))
                return Reject("item row has no id", out problem);
            if (!seen.Add(id))
                return Reject("duplicate item id: " + id, out problem);

            var displayNameKey = (string)item["displayNameKey"];
            if (string.IsNullOrWhiteSpace(displayNameKey))
                return Reject("item " + id + " has no displayNameKey", out problem);

            if (!TryParseSlot((string)item["slot"], out var slot))
                return Reject("item " + id + " has unknown slot", out problem);
            if (!TryParseAcquisition((string)item["acquisition"], out var acquisition))
                return Reject("item " + id + " has unknown acquisition", out problem);

            var portraitAssetId = (string)item["portraitAssetId"];
            if (string.IsNullOrWhiteSpace(portraitAssetId) || !assets.Contains(portraitAssetId))
                return Reject("item " + id + " has missing portrait asset: " +
                              (portraitAssetId ?? "<missing>"), out problem);
            if (!provenance.Contains(portraitAssetId))
                return Reject("item " + id + " has missing provenance: " + portraitAssetId,
                    out problem);

            var entitlementId = (string)item["entitlementId"] ?? string.Empty;
            var productId = (string)item["productId"] ?? string.Empty;
            var earnInstructionKey = (string)item["earnInstructionKey"] ?? string.Empty;
            var rewardedPlacementId = (string)item["rewardedPlacementId"] ?? string.Empty;
            if (acquisition == CosmeticAcquisition.Entitlement &&
                (string.IsNullOrWhiteSpace(entitlementId) || string.IsNullOrWhiteSpace(productId)))
                return Reject("item " + id + " entitlement acquisition requires entitlementId and productId",
                    out problem);
            if (acquisition == CosmeticAcquisition.Earned && string.IsNullOrWhiteSpace(earnInstructionKey))
                return Reject("item " + id + " earned acquisition requires earnInstructionKey", out problem);

            if (!(item["compatibleCatIds"] is JArray compatibleTokens) || compatibleTokens.Count == 0)
                return Reject("item " + id + " has no compatibleCatIds", out problem);
            var compatibleCatIds = new List<string>(compatibleTokens.Count);
            var compatibleSeen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in compatibleTokens)
            {
                var catId = (string)token;
                if (string.IsNullOrWhiteSpace(catId) || !admittedCatIds.Contains(catId))
                    return Reject("item " + id + " names unknown compatible cat: " +
                                  (catId ?? "<missing>"), out problem);
                if (!compatibleSeen.Add(catId))
                    return Reject("item " + id + " repeats compatible cat: " + catId, out problem);
                compatibleCatIds.Add(catId);
            }

            if (item["order"] == null || item["order"].Type != JTokenType.Integer)
                return Reject("item " + id + " has invalid order", out problem);
            var order = (int)item["order"];

            definition = new CosmeticItemDefinition(id, slot, displayNameKey, portraitAssetId,
                acquisition, entitlementId, productId, earnInstructionKey, rewardedPlacementId,
                compatibleCatIds, order);
            problem = null;
            return true;
        }

        private static bool Reject(string message, out string problem)
        {
            problem = message;
            return false;
        }

        private static void StableSortByOrder(List<CosmeticItemDefinition> items)
        {
            // Insertion sort moves only strictly greater orders. Equal-order rows therefore
            // retain their admitted submission order on every runtime, unlike List.Sort.
            for (int i = 1; i < items.Count; i++)
            {
                var current = items[i];
                int destination = i;
                while (destination > 0 && items[destination - 1].Order > current.Order)
                {
                    items[destination] = items[destination - 1];
                    destination--;
                }
                items[destination] = current;
            }
        }

        private static bool TryParseSlot(string raw, out CosmeticSlot slot)
        {
            switch (raw)
            {
                case "outfit": slot = CosmeticSlot.Outfit; return true;
                case "accessory": slot = CosmeticSlot.Accessory; return true;
                case "frame": slot = CosmeticSlot.Frame; return true;
                default: slot = default; return false;
            }
        }

        private static bool TryParseAcquisition(string raw, out CosmeticAcquisition acquisition)
        {
            switch (raw)
            {
                case "starter": acquisition = CosmeticAcquisition.Starter; return true;
                case "earned": acquisition = CosmeticAcquisition.Earned; return true;
                case "entitlement": acquisition = CosmeticAcquisition.Entitlement; return true;
                default: acquisition = default; return false;
            }
        }
    }
}
