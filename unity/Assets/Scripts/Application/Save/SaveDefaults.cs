using Newtonsoft.Json.Linq;

namespace CatMetro.Application.Save
{
    // The fresh v1 payload — EXACTLY the ADR-0006 §2 block (IRREVERSIBLE; human ADR gate rides
    // the CM-C7 PR). The three OPEN sub-shapes are ABSENT, not guessed (criterion 3):
    // no caps.sessionCounters, flags.paywall_placements stays bool, breadcrumbs.purchase is null
    // (when present: exactly {productId, placement, startedAtUtc, state}, state an OPAQUE string
    // round-tripped untouched — enumerating it would invent an RC API, RK-39).
    // Flag defaults mirror the ADR block, which marks them illustrative of TYPE — launch values
    // are NEW-Q45 (human); a missing/unparseable flag reads as the compile-time default and
    // never throws (ADR-0007 "flags must fail closed").
    public static class SaveDefaults
    {
        public const ushort FORMAT_VERSION = 1; // A-C7-4
        public const ushort SAVE_VERSION = 1;   // A-C7-4
        public const string MAGIC = "CMSV";

        public static JObject FreshPayload()
        {
            return new JObject
            {
                ["saveVersion"] = (int)SAVE_VERSION,
                ["contentHash"] = "", // ADR-0008 catalog pipeline is a later contract; key round-trips
                ["profile"] = new JObject
                {
                    ["createdAtUtc"] = 0L, ["lastSeenAtUtc"] = 0L, ["sessionCount"] = 0,
                },
                ["progress"] = new JObject
                {
                    ["levels"] = new JArray(),
                    ["districtsUnlocked"] = 1,
                    ["tutorialDone"] = false,
                },
                ["daily"] = new JObject
                {
                    ["lastDateKey"] = "", ["streakDays"] = 0, ["playedKeys"] = new JArray(),
                },
                ["economy"] = new JObject
                {
                    ["tickets"] = 0, ["rewindBalance"] = 0, ["freeRewindDateKey"] = "",
                },
                ["caps"] = new JObject
                {
                    ["dateKey"] = "",
                    ["counters"] = new JObject
                    {
                        // the five locked ad surfaces, verbatim (ADR-0006:106-110)
                        ["rewind_failure"] = 0,
                        ["double_tickets"] = 0,
                        ["daily_gift_double"] = 0,
                        ["streak_saver"] = 0,
                        ["theme_rental"] = 0,
                    },
                },
                ["ledger"] = new JObject
                {
                    ["keyScheme"] = "cm-ledger-v1",
                    ["dedupe"] = new JArray(),
                    ["audit"] = new JArray(),
                },
                ["entitlements"] = new JObject
                {
                    ["appUserId"] = "", ["active"] = new JArray(), ["fetchedAtUtc"] = 0L,
                },
                ["flags"] = new JObject
                {
                    // the six ADR-0007 keys (ADR-0006:118-122)
                    ["ads_enabled"] = true,
                    ["paywall_placements"] = true,
                    ["daily_enabled"] = true,
                    ["weekly_event"] = true,
                    ["share_card"] = true,
                    ["leaderboard"] = false,
                },
                ["breadcrumbs"] = new JObject
                {
                    ["screenStack"] = new JArray(),
                    ["purchase"] = JValue.CreateNull(),
                },
                ["settings"] = new JObject
                {
                    ["haptics"] = true, ["motion"] = true, ["audio"] = true,
                    ["equippedThemeId"] = "",
                },
            };
        }
    }
}
