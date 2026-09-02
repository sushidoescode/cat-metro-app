using Newtonsoft.Json.Linq;

namespace CatMetro.Application.Save
{
    // The one pre-public v1->v2 schema union. Daily Live, reminder preferences, rewarded-ad
    // leases, and rewarded caps must all land here before the first public v2 build; an existing
    // v2 file will not rerun this step. Known containers fail closed when malformed; valid values
    // and unknown siblings are never overwritten.
    public static class SaveSchemaV2
    {
        public static JObject MigrateFromV1(JObject payload)
        {
            if (payload == null) return null;

            // Validate every known container before mutating any of them. A valid v1 save may
            // omit a container, but a present non-object must not be silently discarded.
            if (!MissingOrObject(payload, "daily")
                || !MissingOrObject(payload, "settings")
                || !MissingOrObject(payload, "entitlements")
                || !MissingOrObject(payload, "caps"))
                return null;
            if (payload["caps"] is JObject existingCaps
                && !MissingOrObject(existingCaps, "rewarded"))
                return null;

            var daily = GetOrCreateObject(payload, "daily");
            // Legacy playedKeys recorded attempts, not wins, so it cannot honestly seed the
            // lifetime completion tally.
            SetIfAbsent(daily, "trustedDateKey", "");
            SetIfAbsent(daily, "completedKeys", new JArray());
            SetIfAbsent(daily, "lifetimeCompletions", 0);

            var settings = GetOrCreateObject(payload, "settings");
            SetIfAbsent(settings, "dailyReminderEnabled", false);
            SetIfAbsent(settings, "dailyReminderPromptSeen", false);
            SetIfAbsent(settings, "dailyReminderSlot", "morning");

            var entitlements = GetOrCreateObject(payload, "entitlements");
            SetIfAbsent(entitlements, "localLeases", new JArray());

            var caps = GetOrCreateObject(payload, "caps");
            var rewarded = GetOrCreateObject(caps, "rewarded");
            SetIfAbsent(rewarded, "dateKey", "");
            SetIfAbsent(rewarded, "counters", new JObject());
            return payload;
        }

        private static bool MissingOrObject(JObject parent, string key) =>
            parent[key] == null || parent[key] is JObject;

        private static JObject GetOrCreateObject(JObject parent, string key)
        {
            if (parent[key] is JObject existing) return existing;
            var created = new JObject();
            parent[key] = created;
            return created;
        }

        private static void SetIfAbsent(JObject parent, string key, JToken value)
        {
            if (parent[key] == null) parent[key] = value;
        }
    }
}
