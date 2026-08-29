using System;
using System.Collections.Generic;
using CatMetro.Services.Purchases;
using Newtonsoft.Json.Linq;

namespace CatMetro.Application.Save
{
    // Stores only local rewarded-ad leases in the existing durable payload. It has no catalogue
    // or clock policy: PurchaseService validates restore rows against the live game data.
    public sealed class RewardedAdSaveStore : IEntitlementLeasePersistence
    {
        private readonly SaveStore _store;

        public RewardedAdSaveStore(SaveStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public bool TryReplaceRewardedAdLeases(IReadOnlyList<EntitlementGrant> leases)
        {
            var original = _store.State.Payload;
            try
            {
                var candidate = (JObject)original.DeepClone();
                var entitlements = candidate["entitlements"] as JObject;
                if (entitlements == null) return false;

                var rows = new List<EntitlementGrant>();
                if (leases != null)
                {
                    for (int i = 0; i < leases.Count; i++)
                    {
                        var lease = leases[i];
                        if (lease.Source != GrantSource.RewardedAd ||
                            string.IsNullOrEmpty(lease.EntitlementId) ||
                            lease.ExpiresAtUnixSeconds <= 0L)
                            continue;
                        rows.Add(lease);
                    }
                }
                rows.Sort((a, b) => string.CompareOrdinal(a.EntitlementId, b.EntitlementId));

                var serialized = new JArray();
                for (int i = 0; i < rows.Count; i++)
                {
                    serialized.Add(new JObject
                    {
                        ["entitlementId"] = rows[i].EntitlementId,
                        ["expiresAtUnixSeconds"] = rows[i].ExpiresAtUnixSeconds,
                    });
                }
                entitlements["localLeases"] = serialized;
                _store.State.Payload = candidate;
                if (_store.TryCommitAtomic()) return true;
            }
            catch
            {
                // SaveStore distinguishes a refusal (false) from an IO fault (throw); this seam
                // presents both as a failed precondition to the award while preserving memory.
            }

            _store.State.Payload = original;
            return false;
        }

        public IReadOnlyList<EntitlementGrant> ReadLocalLeases()
        {
            var result = new List<EntitlementGrant>();
            try
            {
                var rows = _store.State.Payload?["entitlements"]?["localLeases"] as JArray;
                if (rows == null) return result;
                for (int i = 0; i < rows.Count; i++)
                {
                    try
                    {
                        var row = rows[i] as JObject;
                        var id = row?["entitlementId"] as JValue;
                        var expiry = row?["expiresAtUnixSeconds"] as JValue;
                        if (id == null || id.Type != JTokenType.String ||
                            string.IsNullOrEmpty((string)id) ||
                            expiry == null || expiry.Type != JTokenType.Integer)
                            continue;
                        result.Add(new EntitlementGrant((string)id, GrantSource.RewardedAd,
                            (long)expiry));
                    }
                    catch
                    {
                        // A malformed row must not hide a later valid row.
                    }
                }
            }
            catch
            {
                // The load path is total: malformed local save data means no valid local lease.
            }
            return result;
        }
    }
}
