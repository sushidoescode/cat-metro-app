using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CatMetro.Services.Cosmetics
{
    public sealed class CosmeticAssetInventory
    {
        public static CosmeticAssetInventory Empty { get; } = new CosmeticAssetInventory(
            Array.Empty<CosmeticPortraitAssetDefinition>(), Array.Empty<string>());

        private readonly Dictionary<string, CosmeticPortraitAssetDefinition> _byAssetId;

        public IReadOnlyCollection<string> AssetIds { get; }
        public IReadOnlyCollection<string> ProvenanceAssetIds { get; }
        public IReadOnlyList<string> Problems { get; }

        private CosmeticAssetInventory(IReadOnlyList<CosmeticPortraitAssetDefinition> assets,
            IReadOnlyList<string> problems)
        {
            _byAssetId = new Dictionary<string, CosmeticPortraitAssetDefinition>(
                assets.Count, StringComparer.Ordinal);
            var assetIds = new List<string>(assets.Count);
            for (int i = 0; i < assets.Count; i++)
            {
                var asset = assets[i];
                _byAssetId.Add(asset.AssetId, asset);
                assetIds.Add(asset.AssetId);
            }

            AssetIds = assetIds.AsReadOnly();
            ProvenanceAssetIds = new List<string>(assetIds).AsReadOnly();
            Problems = new List<string>(problems).AsReadOnly();
        }

        public bool TryGet(string assetId, out CosmeticPortraitAssetDefinition definition)
        {
            if (assetId == null)
            {
                definition = null;
                return false;
            }
            return _byAssetId.TryGetValue(assetId, out definition);
        }

        public static CosmeticAssetInventory Parse(string json,
            IReadOnlyCollection<string> supportedRendererTokens)
        {
            var problems = new List<string>();
            var root = ParseRoot(json, "asset inventory", problems);
            if (root == null) return Failed(problems);

            if (!HasSupportedSchema(root, problems)) return Failed(problems);

            var supported = ToSet(supportedRendererTokens);
            var provenance = ParseProvenance(root["provenance"], problems);
            var assets = ParseAssets(root["assets"], supported, provenance, problems);
            return new CosmeticAssetInventory(assets, problems);
        }

        private static CosmeticAssetInventory Failed(IReadOnlyList<string> problems)
            => new CosmeticAssetInventory(Array.Empty<CosmeticPortraitAssetDefinition>(), problems);

        private static JObject ParseRoot(string json, string label, List<string> problems)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                problems.Add(label + " source was null or empty");
                return null;
            }

            try
            {
                using var text = new StringReader(json);
                using var reader = new JsonTextReader(text)
                {
                    DateParseHandling = DateParseHandling.None,
                };
                var token = JToken.ReadFrom(reader);
                if (token is JObject root) return root;
                problems.Add(label + " root is not an object");
            }
            catch (Exception e)
            {
                problems.Add(label + " is not valid JSON: " + e.Message);
            }
            return null;
        }

        private static bool HasSupportedSchema(JObject root, List<string> problems)
        {
            try
            {
                if (root["schemaVersion"] == null || (int)root["schemaVersion"] != 1)
                {
                    problems.Add("asset inventory has missing or unsupported schemaVersion");
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                problems.Add("asset inventory has missing or unsupported schemaVersion");
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

        private static Dictionary<string, bool> ParseProvenance(JToken token,
            List<string> problems)
        {
            var result = new Dictionary<string, bool>(StringComparer.Ordinal);
            if (!(token is JArray array))
            {
                problems.Add("asset inventory has no provenance array");
                return result;
            }

            var duplicates = FindDuplicateStringIds(array, "id");
            var reportedDuplicates = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in array)
            {
                if (!(row is JObject item))
                {
                    problems.Add("provenance row is not an object");
                    continue;
                }

                if (!TryGetText(item, "id", out var id))
                {
                    problems.Add("provenance row has invalid or missing id");
                    continue;
                }
                if (duplicates.Contains(id))
                {
                    if (reportedDuplicates.Add(id))
                    {
                        problems.Add("duplicate provenance id: " + id);
                        result[id] = false;
                    }
                    continue;
                }

                if (!TryValidateProvenance(item, id, out var problem))
                {
                    problems.Add(problem);
                    result.Add(id, false);
                    continue;
                }
                result.Add(id, true);
            }
            return result;
        }

        private static bool TryValidateProvenance(JObject item, string id, out string problem)
        {
            if (!TryGetText(item, "sourceKind", out var sourceKind))
                return Invalid(id, "sourceKind", out problem);

            if (sourceKind == "project_authored")
            {
                if (!HasText(item, "sourcePath"))
                    return Invalid(id, "sourcePath", out problem);
                if (!HasClearedDistribution(item))
                    return Invalid(id, "commercialDistribution", out problem);
                problem = null;
                return true;
            }

            if (sourceKind == "generated_paid")
            {
                foreach (var field in new[]
                {
                    "provider", "paidTier", "taskId", "prompt", "generationTimestamp",
                    "sourceHash", "custodyLocation", "termsEvidence",
                })
                    if (!HasText(item, field)) return Invalid(id, field, out problem);

                foreach (var field in new[] { "derivativeHashes", "transformationChain" })
                    if (!HasNonEmptyStringArray(item, field)) return Invalid(id, field, out problem);

                if (!HasClearedDistribution(item))
                    return Invalid(id, "commercialDistribution", out problem);
                problem = null;
                return true;
            }

            problem = "provenance " + id + " has unknown sourceKind: " +
                      (sourceKind ?? "<missing>");
            return false;
        }

        private static bool Invalid(string id, string field, out string problem)
        {
            problem = "provenance " + id + " has invalid or missing " + field;
            return false;
        }

        private static bool HasText(JObject item, string field)
        {
            return TryGetText(item, field, out _);
        }

        private static bool TryGetText(JObject item, string field, out string value)
        {
            var token = item[field];
            if (token == null || token.Type != JTokenType.String)
            {
                value = null;
                return false;
            }
            value = (string)token;
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool HasNonEmptyStringArray(JObject item, string field)
        {
            if (!(item[field] is JArray values) || values.Count == 0) return false;
            foreach (var value in values)
            {
                if (value.Type != JTokenType.String) return false;
                var text = (string)value;
                if (string.IsNullOrWhiteSpace(text)) return false;
            }
            return true;
        }

        private static bool HasClearedDistribution(JObject item)
        {
            return TryGetText(item, "commercialDistribution", out var value) && value == "cleared";
        }

        private static List<CosmeticPortraitAssetDefinition> ParseAssets(JToken token,
            HashSet<string> supportedRendererTokens, Dictionary<string, bool> provenance,
            List<string> problems)
        {
            var result = new List<CosmeticPortraitAssetDefinition>();
            if (!(token is JArray array))
            {
                problems.Add("asset inventory has no assets array");
                return result;
            }

            var duplicates = FindDuplicateStringIds(array, "assetId");
            var reportedDuplicates = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in array)
            {
                if (!(row is JObject item))
                {
                    problems.Add("asset row is not an object");
                    continue;
                }

                if (!TryGetText(item, "assetId", out var assetId))
                {
                    problems.Add("asset row has invalid or missing assetId");
                    continue;
                }
                if (duplicates.Contains(assetId))
                {
                    if (reportedDuplicates.Add(assetId))
                        problems.Add("duplicate asset id: " + assetId);
                    continue;
                }
                if (!TryGetText(item, "rendererToken", out var rendererToken))
                {
                    problems.Add("asset " + assetId + " has invalid or missing rendererToken");
                    continue;
                }
                if (!supportedRendererTokens.Contains(rendererToken))
                {
                    problems.Add("asset " + assetId + " has unsupported renderer token: " + rendererToken);
                    continue;
                }
                if (!TryGetText(item, "provenanceId", out var provenanceId))
                {
                    problems.Add("asset " + assetId + " has invalid or missing provenanceId");
                    continue;
                }
                if (!provenance.TryGetValue(provenanceId, out var valid) || !valid)
                {
                    problems.Add("asset " + assetId + " lacks admitted provenance: " + provenanceId);
                    continue;
                }

                result.Add(new CosmeticPortraitAssetDefinition(assetId, rendererToken, provenanceId));
            }
            return result;
        }

        private static HashSet<string> FindDuplicateStringIds(JArray rows, string field)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                if (!(row is JObject item) || !TryGetText(item, field, out var id)) continue;
                if (!seen.Add(id)) duplicates.Add(id);
            }
            return duplicates;
        }
    }
}
