using Newtonsoft.Json.Linq;

namespace CatMetro.Application.Save
{
    // ADR-0006 §2 / SEC-24: the failure rewind session cap survives process death.
    // Migration is additive: daily counters, cosmetic caps and unknown siblings stay opaque.
    public static class SaveSchemaV4
    {
        public static JObject DefaultSessionCounters() => new JObject { ["rewind_failure"] = 0 };

        public static JObject MigrateFromV3(JObject payload)
        {
            if (payload == null) return null;
            // Preserve malformed cap state for RewardedAdSaveStore's fail-closed validation.
            // Returning null would invoke SaveStore's fresh-save fallback, losing progress and
            // replacing exhausted/invalid counters with new capacity (SEC-24).
            if (payload["caps"] != null && !(payload["caps"] is JObject)) return payload;
            var caps = payload["caps"] as JObject ?? new JObject();
            if (payload["caps"] == null) payload["caps"] = caps;
            if (caps["sessionCounters"] != null && !(caps["sessionCounters"] is JObject)) return payload;
            var counters = caps["sessionCounters"] as JObject;
            if (counters == null) caps["sessionCounters"] = DefaultSessionCounters();
            else if (counters["rewind_failure"] == null) counters["rewind_failure"] = 0;
            return payload;
        }
    }
}
