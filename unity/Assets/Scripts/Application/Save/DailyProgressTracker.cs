using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using CatMetro.Content.Daily;

namespace CatMetro.Application.Save
{
    public sealed class DailyDateSelection
    {
        public string RequestedDateKey { get; }
        public string EffectiveDateKey { get; }
        public bool IsClockRollback { get; }
        public bool CanCountCompletion { get; }
        public bool IsPractice => !CanCountCompletion;

        internal DailyDateSelection(string requestedDateKey, string effectiveDateKey,
            bool isClockRollback, bool canCountCompletion)
        {
            RequestedDateKey = requestedDateKey;
            EffectiveDateKey = effectiveDateKey;
            IsClockRollback = isClockRollback;
            CanCountCompletion = canCountCompletion;
        }
    }

    public sealed class DailyCompletionResult
    {
        public bool Counted { get; }
        public int LifetimeCompletions { get; }

        internal DailyCompletionResult(bool counted, int lifetimeCompletions)
        {
            Counted = counted;
            LifetimeCompletions = lifetimeCompletions;
        }
    }

    // Owns the two pieces of durable progress needed by Daily Live: unique campaign clears for
    // the configurable entry gate, and a cumulative lifetime Daily tally. It intentionally has
    // no streak calculation or expiry path.
    public sealed class DailyProgressTracker
    {
        private readonly SaveStore _store;

        public DailyProgressTracker(SaveStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public int CampaignCompletions
        {
            get
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                if (!(_store.State.Payload?["progress"]?["levels"] is JArray levels))
                    return 0;
                foreach (var token in levels)
                {
                    if (!(token is JObject row)) continue;
                    string id = (string)row["id"];
                    int clears = NonNegativeInt(row["clears"]);
                    if (!string.IsNullOrEmpty(id) && clears > 0) seen.Add(id);
                }
                return seen.Count;
            }
        }

        public int LifetimeCompletions => NonNegativeInt(
            _store.State.Payload?["daily"]?["lifetimeCompletions"]);

        public bool IsDailyUnlocked(int unlockAfterCampaignCompletions)
        {
            if (unlockAfterCampaignCompletions < 0)
                throw new ArgumentOutOfRangeException(nameof(unlockAfterCampaignCompletions));
            return CampaignCompletions >= unlockAfterCampaignCompletions;
        }

        public int RecordCampaignCompletion(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId))
                throw new ArgumentException("levelId is required", nameof(levelId));

            var original = _store.State.Payload;
            var mutated = (JObject)original.DeepClone();
            var progress = EnsureObject(mutated, "progress");
            var levels = EnsureArray(progress, "levels");
            JObject match = null;
            foreach (var token in levels)
            {
                if (token is JObject row && string.Equals((string)row["id"], levelId,
                    StringComparison.Ordinal))
                {
                    match = row;
                    break;
                }
            }

            if (match == null)
            {
                levels.Add(new JObject
                {
                    ["id"] = levelId,
                    ["stars"] = 0,
                    ["bestScore"] = 0,
                    ["clears"] = 1,
                });
            }
            else
            {
                int clears = NonNegativeInt(match["clears"]);
                match["clears"] = clears == int.MaxValue ? int.MaxValue : clears + 1;
            }

            TryPersist(original, mutated);
            return CampaignCompletions;
        }

        public DailyDateSelection ObserveUtcDate(string requestedDateKey)
        {
            if (!DateKeys.IsValid(requestedDateKey))
                throw new ArgumentException(
                    "dateKey must be a real calendar date in yyyy-MM-dd form",
                    nameof(requestedDateKey));

            string trusted = (string)_store.State.Payload?["daily"]?["trustedDateKey"];
            if (!DateKeys.IsValid(trusted)) trusted = "";

            if (trusted.Length > 0
                && string.CompareOrdinal(requestedDateKey, trusted) < 0)
            {
                // A rollback never moves the high-water mark backwards. The trusted puzzle is
                // still playable, but the resulting run is practice and cannot raise the tally.
                return new DailyDateSelection(requestedDateKey, trusted,
                    isClockRollback: true, canCountCompletion: false);
            }

            if (string.Equals(requestedDateKey, trusted, StringComparison.Ordinal))
                return new DailyDateSelection(requestedDateKey, requestedDateKey,
                    isClockRollback: false, canCountCompletion: true);

            // Forward dates are intentionally accepted. An offline app cannot distinguish a
            // manually advanced clock from a legitimate long absence, so rejecting forward
            // jumps would strand honest returning players.
            var original = _store.State.Payload;
            var mutated = (JObject)original.DeepClone();
            EnsureObject(mutated, "daily")["trustedDateKey"] = requestedDateKey;
            bool persisted = TryPersist(original, mutated);
            return new DailyDateSelection(requestedDateKey, requestedDateKey,
                isClockRollback: false, canCountCompletion: persisted);
        }

        public DailyCompletionResult RecordDailyCompletion(DailyDateSelection selection)
        {
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            int before = LifetimeCompletions;
            if (!selection.CanCountCompletion)
                return new DailyCompletionResult(false, before);

            var daily = _store.State.Payload?["daily"] as JObject;
            var completed = daily?["completedKeys"] as JArray;
            if (Contains(completed, selection.EffectiveDateKey))
                return new DailyCompletionResult(false, before);

            var original = _store.State.Payload;
            var mutated = (JObject)original.DeepClone();
            var changedDaily = EnsureObject(mutated, "daily");
            EnsureArray(changedDaily, "completedKeys").Add(selection.EffectiveDateKey);
            int after = before == int.MaxValue ? int.MaxValue : before + 1;
            changedDaily["lifetimeCompletions"] = after;

            bool persisted = TryPersist(original, mutated);
            return new DailyCompletionResult(persisted, persisted ? after : before);
        }

        private bool TryPersist(JObject original, JObject mutated)
        {
            _store.State.Payload = mutated;
            try
            {
                if (_store.TryCommitAtomic()) return true;
                _store.Report("error_caught", "domain=daily_save detail=commit refused");
            }
            catch (Exception ex)
            {
                _store.Report("error_caught", "domain=daily_save detail=" + ex.GetType().Name);
            }
            _store.State.Payload = original;
            return false;
        }

        private static bool Contains(JArray array, string value)
        {
            if (array == null) return false;
            foreach (var token in array)
                if (string.Equals((string)token, value, StringComparison.Ordinal)) return true;
            return false;
        }

        private static int NonNegativeInt(JToken token)
        {
            if (token == null || token.Type != JTokenType.Integer) return 0;
            try
            {
                long value = token.Value<long>();
                if (value <= 0) return 0;
                return value >= int.MaxValue ? int.MaxValue : (int)value;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static JObject EnsureObject(JObject parent, string key)
        {
            if (parent[key] is JObject existing) return existing;
            var created = new JObject();
            parent[key] = created;
            return created;
        }

        private static JArray EnsureArray(JObject parent, string key)
        {
            if (parent[key] is JArray existing) return existing;
            var created = new JArray();
            parent[key] = created;
            return created;
        }
    }
}
