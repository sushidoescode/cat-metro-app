using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace CatMetro.Services.Purchases
{
    public enum RewardedRewardKind { EntitlementLease, FailureRewind }

    // A cap on how often one rewarded placement may pay out. Scope is a plain string
    // ("session", "localDate") rather than an enum so the ad lane can add a scope in data
    // without a code change here — the same reasoning that turned ProductIdentifier from an
    // enum into a string.
    public readonly struct RewardCap
    {
        public readonly string Scope;
        public readonly int Limit;

        public RewardCap(string scope, int limit)
        {
            Scope = scope;
            Limit = limit;
        }
    }

    // Where a rewarded ad may be offered and what it grants. Cosmetic rows retain the default
    // entitlement lease kind and catalogue-defined lease length. Failure rewind has no entitlement.
    public readonly struct RewardedPlacement
    {
        public readonly string Id;
        public readonly string EntitlementId;
        public readonly RewardedRewardKind RewardKind;
        public readonly IReadOnlyList<RewardCap> Caps;

        // The ad lane owns flipping this on. It stays false until an ad network is wired, so a
        // placement can be authored, parsed, capped and tested long before any ad SDK exists —
        // and so a half-wired ad integration cannot start showing ads by accident.
        public readonly bool Enabled;
        public readonly string DisabledReason;

        public RewardedPlacement(string id, string entitlementId, IReadOnlyList<RewardCap> caps,
            bool enabled, string disabledReason,
            RewardedRewardKind rewardKind = RewardedRewardKind.EntitlementLease)
        {
            Id = id;
            EntitlementId = entitlementId;
            RewardKind = rewardKind;
            Caps = caps;
            Enabled = enabled;
            DisabledReason = disabledReason;
        }
    }

    // Parses Resources/Monetization/rewarded_placements.json. Same posture as PurchaseCatalog:
    // never throws, reports problems, and a broken file yields no placements rather than a crash
    // — the worst case being that no rewarded ads are offered, which is a revenue loss and not a
    // player-facing failure.
    public sealed class RewardedPlacementCatalog
    {
        public static readonly RewardedPlacementCatalog Empty =
            new RewardedPlacementCatalog(Array.Empty<RewardedPlacement>(), Array.Empty<string>());

        public IReadOnlyList<RewardedPlacement> Placements { get; }
        public IReadOnlyList<string> Problems { get; }

        private RewardedPlacementCatalog(IReadOnlyList<RewardedPlacement> placements,
            IReadOnlyList<string> problems)
        {
            Placements = placements;
            Problems = problems;
        }

        public bool TryGet(string id, out RewardedPlacement placement)
        {
            for (int i = 0; i < Placements.Count; i++)
            {
                if (string.Equals(Placements[i].Id, id, StringComparison.Ordinal))
                {
                    placement = Placements[i];
                    return true;
                }
            }

            placement = default;
            return false;
        }

        // Lease rows require a declared, ad-grantable entitlement. Failure rewind instead has
        // its own exact placement/cap contract and can parse without an entitlement catalogue.
        public static RewardedPlacementCatalog Parse(string json, PurchaseCatalog productCatalog)
        {
            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(json))
            {
                problems.Add("rewarded placements source was null or empty");
                return new RewardedPlacementCatalog(Array.Empty<RewardedPlacement>(), problems);
            }

            JObject root;
            try { root = JObject.Parse(json); }
            catch (Exception e)
            {
                problems.Add("rewarded placements file is not valid JSON: " + e.Message);
                return new RewardedPlacementCatalog(Array.Empty<RewardedPlacement>(), problems);
            }

            var catalog = productCatalog ?? PurchaseCatalog.Empty;
            var result = new List<RewardedPlacement>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (!(root["placements"] is JArray array))
            {
                problems.Add("rewarded placements file has no \"placements\" array");
                return new RewardedPlacementCatalog(result, problems);
            }

            foreach (var token in array)
            {
                try
                {
                    if (!(token is JObject o)) { problems.Add("placement entry is not an object"); continue; }

                    var id = o["id"]?.Type == JTokenType.String ? (string)o["id"] : null;
                    if (string.IsNullOrWhiteSpace(id)) { problems.Add("placement entry has no id"); continue; }
                    if (!seen.Add(id)) { problems.Add("duplicate placement id: " + id); continue; }

                    var kind = RewardedRewardKind.EntitlementLease;
                    if (o["rewardKind"] != null)
                    {
                        if (o["rewardKind"].Type != JTokenType.String)
                        { problems.Add("placement " + id + " has invalid rewardKind"); continue; }
                        switch ((string)o["rewardKind"])
                        {
                            case "entitlement_lease": break;
                            case "failure_rewind": kind = RewardedRewardKind.FailureRewind; break;
                            default: problems.Add("placement " + id + " has unknown rewardKind"); continue;
                        }
                    }
                    var entitlementId = o["entitlement"]?.Type == JTokenType.String
                        ? (string)o["entitlement"] : null;
                    if (kind == RewardedRewardKind.FailureRewind)
                    {
                        if (id != "rewind_failure" || o["enabled"]?.Type != JTokenType.Boolean ||
                            !(bool)o["enabled"] || (o["entitlement"] != null &&
                            o["entitlement"].Type != JTokenType.Null) ||
                            !(o["caps"] is JObject failureCaps) || failureCaps.Count != 2 ||
                            failureCaps["session"]?.Type != JTokenType.Integer ||
                            failureCaps["localDate"]?.Type != JTokenType.Integer ||
                            (long)failureCaps["session"] != 2 || (long)failureCaps["localDate"] != 5)
                        { problems.Add("placement " + id + " has invalid failure-rewind reward/caps/enabled"); continue; }
                    }
                    else
                    {
                        if (id == "rewind_failure")
                        { problems.Add("placement rewind_failure requires failure_rewind rewardKind"); continue; }
                        if (string.IsNullOrWhiteSpace(entitlementId))
                        { problems.Add("placement " + id + " names no entitlement"); continue; }
                        if (!catalog.TryGetEntitlement(entitlementId, out var definition))
                        {
                            problems.Add("placement " + id + " grants undeclared entitlement: " + entitlementId);
                            continue;
                        }
                        if (!definition.IsAdGrantable)
                        {
                            problems.Add("placement " + id + " targets entitlement " + entitlementId +
                                         " which is not ad-grantable (adLeaseSeconds is 0)");
                            continue;
                        }
                    }

                    var caps = new List<RewardCap>();
                    if (o["caps"] is JObject capsObject)
                    {
                        foreach (var prop in capsObject.Properties())
                        {
                            int limit;
                            try { limit = (int)prop.Value; }
                            catch (Exception)
                            {
                                problems.Add("placement " + id + " cap " + prop.Name + " is not numeric");
                                continue;
                            }

                            if (limit < 0)
                            {
                                problems.Add("placement " + id + " cap " + prop.Name + " is negative");
                                continue;
                            }

                            caps.Add(new RewardCap(prop.Name, limit));
                        }
                    }

                    bool enabled = false;
                    var enabledToken = o["enabled"];
                    if (enabledToken != null && enabledToken.Type == JTokenType.Boolean)
                        enabled = (bool)enabledToken;

                    result.Add(new RewardedPlacement(id, entitlementId, caps, enabled,
                        (string)o["disabledReason"], kind));
                }
                catch (Exception)
                {
                    problems.Add("placement entry contains malformed fields");
                }
            }

            return new RewardedPlacementCatalog(result, problems);
        }
    }
}
