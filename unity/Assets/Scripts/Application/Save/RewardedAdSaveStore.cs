using System;
using System.Collections.Generic;
using System.Globalization;
using CatMetro.Application.Analytics;
using CatMetro.Services.Ads;
using CatMetro.Services.Purchases;
using Newtonsoft.Json.Linq;

namespace CatMetro.Application.Save
{
    // Stores local rewarded-ad leases and caps in the durable payload. PurchaseService validates
    // lease rows. Failure rewind uses the ADR-0006 §2 dual cap for account health (SEC-24),
    // not anti-cheat: a local save/clock remains player-controlled.
    public sealed class RewardedAdSaveStore : IEntitlementLeasePersistence, IRewardedAdCapStore,
        IFailureRewindCapStore
    {
        private readonly SaveStore _store;

        private bool _failureSessionReady;
        private const string FailureKey = "rewind_failure";
        private static readonly string[] DailyKeys =
            { FailureKey, "double_tickets", "daily_gift_double", "streak_saver", "theme_rental" };

        public bool TryTouchFailureRewindSession(long nowUnixSeconds, string localDateKey)
        {
            _failureSessionReady = false;
            return TryWriteFailureRewind(nowUnixSeconds, localDateKey, consume: false);
        }

        public bool CanOfferFailureRewind(long nowUnixSeconds, string localDateKey)
        {
            if (!_failureSessionReady || _store.ReadOnlyMode) return false;
            try
            {
                if (!TryReadFailureState(_store.State.Payload, nowUnixSeconds, localDateKey,
                    out var state) || state.NewSession) return false;
                return state.SessionUsed < 2 && state.DailyUsed < 5;
            }
            catch { return false; }
        }

        public bool TryConsumeFailureRewind(long nowUnixSeconds, string localDateKey)
        {
            if (!_failureSessionReady) return false;
            return TryWriteFailureRewind(nowUnixSeconds, localDateKey, consume: true);
        }

        private bool TryWriteFailureRewind(long now, string dateKey, bool consume)
        {
            var original = _store.State.Payload;
            try
            {
                if (_store.ReadOnlyMode || !TryReadFailureState(original, now, dateKey, out var state))
                {
                    _failureSessionReady = false;
                    return false;
                }
                if (consume && (state.SessionUsed >= 2 || state.DailyUsed >= 5)) return false;
                var candidate = (JObject)original.DeepClone();
                var profile = (JObject)candidate["profile"];
                var caps = (JObject)candidate["caps"];
                if (state.NewSession)
                {
                    profile["sessionCount"] = state.SessionCount == int.MaxValue
                        ? int.MaxValue : state.SessionCount + 1;
                    caps["sessionCounters"][FailureKey] = 0;
                }
                profile["lastSeenAtUtc"] = now;
                if (consume)
                {
                    // Daily rollover and both increments share this one durable transaction.
                    // Unknown counters/siblings and the separate cosmetic rewarded caps survive.
                    if (state.NewDate)
                    {
                        caps["dateKey"] = dateKey;
                        foreach (var key in DailyKeys) caps["counters"][key] = 0;
                    }
                    caps["sessionCounters"][FailureKey] = state.SessionUsed + 1;
                    caps["counters"][FailureKey] = state.DailyUsed + 1;
                }
                _store.State.Payload = candidate;
                if (_store.TryCommitAtomic())
                {
                    _failureSessionReady = true;
                    return true;
                }
            }
            catch
            {
                // Initialization, touch, session reset and consumption all fail closed on IO.
            }
            _store.State.Payload = original;
            _failureSessionReady = false;
            return false;
        }

        private readonly struct FailureState
        {
            public readonly int SessionCount, SessionUsed, DailyUsed;
            public readonly bool NewSession, NewDate;
            public FailureState(int count, int sessionUsed, int dailyUsed, bool newSession, bool newDate)
            {
                SessionCount = count; SessionUsed = sessionUsed; DailyUsed = dailyUsed;
                NewSession = newSession; NewDate = newDate;
            }
        }

        private static bool TryReadFailureState(JObject payload, long now, string dateKey,
            out FailureState state)
        {
            state = default;
            if (now <= 0 || !IsLocalDate(dateKey) ||
                !(payload?["profile"] is JObject profile) || !(payload["caps"] is JObject caps) ||
                !(caps["counters"] is JObject daily) || !(caps["sessionCounters"] is JObject session) ||
                !TryReadCount(session[FailureKey], out int sessionUsed) ||
                !TryReadCount(profile["sessionCount"], out int sessionCount) ||
                !(profile["lastSeenAtUtc"] is JValue seen) || seen.Type != JTokenType.Integer ||
                !(caps["dateKey"] is JValue savedDate) || savedDate.Type != JTokenType.String)
                return false;
            long lastSeen = (long)seen;
            if (lastSeen < 0 || ((sessionCount == 0) != (lastSeen == 0))) return false;
            string previousDate = (string)savedDate;
            if (previousDate != "" && !IsLocalDate(previousDate)) return false;
            foreach (var key in DailyKeys)
                if (!TryReadCount(daily[key], out _)) return false;
            TryReadCount(daily[FailureKey], out int dailyUsed);
            bool newSession = sessionCount == 0 || now - lastSeen >= AnalyticsAppSession.SessionTimeoutSeconds
                || now / 86400L > lastSeen / 86400L;
            bool newDate = !string.Equals(previousDate, dateKey, StringComparison.Ordinal);
            state = new FailureState(sessionCount, newSession ? 0 : sessionUsed,
                newDate ? 0 : dailyUsed, newSession, newDate);
            return true;
        }

        private static bool IsLocalDate(string value) => DateTime.TryParseExact(value, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

        private static bool TryReadCount(JToken token, out int count)
        {
            count = 0;
            if (!(token is JValue value) || value.Type != JTokenType.Integer) return false;
            long number = (long)value;
            if (number < 0 || number > int.MaxValue) return false;
            count = (int)number;
            return true;
        }

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
