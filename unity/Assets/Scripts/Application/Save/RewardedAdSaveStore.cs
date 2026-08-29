using System;
using System.Collections.Generic;
using CatMetro.Services.Ads;
using CatMetro.Services.Purchases;
using Newtonsoft.Json.Linq;

namespace CatMetro.Application.Save
{
    // Stores only local rewarded-ad leases in the existing durable payload. It has no catalogue
    // or clock policy: PurchaseService validates restore rows against the live game data.
    public sealed class RewardedAdSaveStore : IEntitlementLeasePersistence, IRewardedAdCapStore
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

        public int ReadLocalDateCount(string placementId, string localDateKey)
        {
            if (string.IsNullOrEmpty(placementId) || string.IsNullOrEmpty(localDateKey)) return 0;
            try
            {
                var rewarded = _store.State.Payload?["caps"]?["rewarded"] as JObject;
                if (!(rewarded?["dateKey"] is JValue date) ||
                    date.Type != JTokenType.String ||
                    !string.Equals((string)date, localDateKey, StringComparison.Ordinal) ||
                    !(rewarded["counters"] is JObject counters) ||
                    !(counters[placementId] is JValue value) ||
                    value.Type != JTokenType.Integer)
                    return 0;
                long count = (long)value;
                return count < 0L || count > int.MaxValue ? 0 : (int)count;
            }
            catch
            {
                return 0;
            }
        }

        public bool TryIncrementLocalDateCount(string placementId, string localDateKey)
        {
            if (string.IsNullOrEmpty(placementId) || string.IsNullOrEmpty(localDateKey)) return false;
            var original = _store.State.Payload;
            try
            {
                var candidate = (JObject)original.DeepClone();
                var caps = candidate["caps"] as JObject;
                var rewarded = caps?["rewarded"] as JObject;
                if (rewarded == null) return false;

                bool sameDate = rewarded["dateKey"] is JValue date &&
                    date.Type == JTokenType.String &&
                    string.Equals((string)date, localDateKey, StringComparison.Ordinal);
                JObject counters;
                if (!sameDate)
                {
                    counters = new JObject();
                    rewarded["dateKey"] = localDateKey;
                    rewarded["counters"] = counters;
                }
                else
                {
                    counters = rewarded["counters"] as JObject;
                    if (counters == null) return false;
                }

                int current = ReadNonnegativeInt(counters[placementId]);
                counters[placementId] = current == int.MaxValue ? int.MaxValue : current + 1;
                _store.State.Payload = candidate;
                if (_store.TryCommitAtomic()) return true;
            }
            catch
            {
                // The already-durable lease may be the original identity here. Restore it exactly
                // even when this later, deliberately separate cap commit faults.
            }

            _store.State.Payload = original;
            return false;
        }

        private static int ReadNonnegativeInt(JToken token)
        {
            try
            {
                if (!(token is JValue value) || value.Type != JTokenType.Integer) return 0;
                long count = (long)value;
                return count < 0L || count > int.MaxValue ? 0 : (int)count;
            }
            catch
            {
                return 0;
            }
        }
    }
}
