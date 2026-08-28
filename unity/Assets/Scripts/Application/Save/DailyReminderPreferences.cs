using System;
using Newtonsoft.Json.Linq;
using CatMetro.Services;

namespace CatMetro.Application.Save
{
    // Owns durable reminder choices only. Provider synchronization happens after a successful
    // local commit so an unavailable provider can be reconciled from this authoritative state.
    public sealed class DailyReminderPreferences
    {
        private readonly SaveStore _store;

        public DailyReminderPreferences(SaveStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public bool Enabled => BooleanValue(Settings?["dailyReminderEnabled"]);

        // A v3 payload missing or corrupting this one-shot state fails safe: it must never cause
        // an automatic prompt to reappear. A real v2 save receives false in MigrateV2ToV3.
        public bool PromptSeen => Settings?["dailyReminderPromptSeen"] is JValue value
            && value.Type == JTokenType.Boolean
            ? value.Value<bool>()
            : true;

        public DailyReminderSlot Slot => DailyReminderSlot.FromTagValue(
            Settings?["dailyReminderSlot"] is JValue value && value.Type == JTokenType.String
                ? value.Value<string>()
                : null);

        public bool CanOfferPrompt(int lifetimeCompletions) =>
            lifetimeCompletions > 0 && !PromptSeen;

        public bool TryMarkPromptSeen()
        {
            if (PromptSeen) return false;
            return TryUpdate(settings => settings["dailyReminderPromptSeen"] = true);
        }

        public bool TrySetEnabled(bool enabled) =>
            TryUpdate(settings => settings["dailyReminderEnabled"] = enabled);

        public bool TrySetSlot(DailyReminderSlot slot)
        {
            if (slot == null) throw new ArgumentNullException(nameof(slot));
            return TryUpdate(settings => settings["dailyReminderSlot"] = slot.TagValue);
        }

        private JObject Settings => _store.State.Payload?["settings"] as JObject;

        private bool TryUpdate(Action<JObject> update)
        {
            var original = _store.State.Payload;
            var mutated = (JObject)original.DeepClone();
            update(EnsureObject(mutated, "settings"));
            _store.State.Payload = mutated;
            try
            {
                if (_store.TryCommitAtomic()) return true;
                _store.Report("error_caught", "domain=daily_reminder_save detail=commit refused");
            }
            catch (Exception ex)
            {
                _store.Report("error_caught", "domain=daily_reminder_save detail="
                    + ex.GetType().Name);
            }
            _store.State.Payload = original;
            return false;
        }

        private static bool BooleanValue(JToken token) => token is JValue value
            && value.Type == JTokenType.Boolean
            && value.Value<bool>();

        private static JObject EnsureObject(JObject parent, string key)
        {
            if (parent[key] is JObject objectValue) return objectValue;
            var created = new JObject();
            parent[key] = created;
            return created;
        }
    }
}
