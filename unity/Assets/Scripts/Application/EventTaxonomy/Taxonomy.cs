using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using CatMetro.Services;

namespace CatMetro.Application.EventTaxonomy
{
    public static class Taxonomy
       {
        public static IReadOnlyList<TaxonomyRow> Rows { get; }

        static Taxonomy()
           {
            Rows = new List<TaxonomyRow>
               {
                new TaxonomyRow("app_open", "acquisition",
                    new[] { "session_id", "app_version", "install_age_days", "build_channel" },
                    new[] { "referrer_source", "notification_campaign_id" },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new[] { "last_seen_at", "session_count" },
                    new[] { "analytics", "onesignal_tag" },
                       "behavioral_no_pii", "cold launch + warm launch; verify session dedupe at 29m/31m"),
                new TaxonomyRow("first_open", "acquisition",
                    new[] { "app_version", "device_tier", "os_api_level" },
                    new[] { "install_referrer" },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new[] { "install_date" },
                    new[] { "analytics" },
                       "behavioral_no_pii", "fresh install; verify fires exactly once incl. after reinstall w/ backup off"),
                new TaxonomyRow("tutorial_started", "onboarding",
                    new[] { "variant" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new string[] { },
                    new[] { "analytics" },
                       "behavioral_no_pii", "fresh profile"),
                new TaxonomyRow("tutorial_step", "onboarding",
                    new[] { "step_id", "duration_s" },
                    new[] { "retries" },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new string[] { },
                    new[] { "analytics" },
                       "behavioral_no_pii", "walk all steps; abandon mid-step and relaunch"),
                new TaxonomyRow("tutorial_completed", "onboarding",
                    new[] { "duration_s", "total_retries" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new[] { "tutorial_done" },
                    new[] { "analytics", "onesignal_event" },
                       "behavioral_no_pii", "verify OneSignal receives event within 60s"),
                new TaxonomyRow("level_started", "gameplay",
                    new[] { "level_id", "mode", "attempt", "difficulty_target" },
                    new[] { "from_screen", "active_theme" },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new string[] { },
                    new[] { "analytics" },
                       "behavioral_no_pii", "campaign + daily + retry paths"),
                new TaxonomyRow("switch_toggled", "gameplay",
                    new[] { "level_id", "switch_id", "tick" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new string[] { },
                    new[] { "analytics_sampled_10pct" },
                       "behavioral_no_pii", "verify sampling; compare vs replay log"),
                new TaxonomyRow("level_completed", "gameplay",
                    new[] { "level_id", "mode", "attempt", "duration_s", "switches_used", "perfect", "score", "stars" },
                    new[] { "rewind_used" },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new[] { "highest_level", "last_level_at" },
                    new[] { "analytics", "onesignal_tag(highest_level)" },
                       "behavioral_no_pii", "verify stars/score match result screen"),
                new TaxonomyRow("level_failed", "gameplay",
                    new[] { "level_id", "mode", "attempt", "fail_reason", "progress_pct", "duration_s" },
                    new[] { "congested_node", "queue_state" },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new[] { "last_fail_level" },
                    new[] { "analytics", "onesignal_event" },
                       "behavioral_no_pii", "force each fail_reason enum value"),
                new TaxonomyRow("level_quit", "gameplay",
                    new[] { "level_id", "attempt", "progress_pct", "duration_s" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new string[] { },
                    new[] { "analytics" },
                       "behavioral_no_pii", "quit via back + home + kill"),
                new TaxonomyRow("rewind_used", "gameplay",
                    new[] { "level_id", "source", "balance_after" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>> { { "source", new[] { "free", "purchased", "rewarded" } } },
                    new string[] { },
                    new[] { "analytics" },
                       "behavioral_no_pii", "all 3 sources; verify ledger decrement once"),
                new TaxonomyRow("daily_unlocked", "retention",
                    new[] { "highest_level" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new[] { "daily_unlocked" },
                    new[] { "analytics", "onesignal_tag", "onesignal_event" },
                       "behavioral_no_pii", "verify single fire; OneSignal custom event is Journey 1 entry trigger"),
                new TaxonomyRow("daily_started", "retention",
                    new[] { "seed", "local_date" },
                    new[] { "streak_days" },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new string[] { },
                    new[] { "analytics" },
                       "behavioral_no_pii", "verify same seed as server-of-truth for date"),
                new TaxonomyRow("daily_completed", "retention",
                    new[] { "seed", "score", "rank_bucket", "streak_days" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new[] { "streak_days", "daily_last_done" },
                    new[] { "analytics", "onesignal_event" },
                       "behavioral_no_pii", "complete on 2 devices; verify same seed"),
                new TaxonomyRow("streak_changed", "retention",
                    new[] { "new_streak", "change" },
                    new[] { "saver_source" },
                    new Dictionary<string, IReadOnlyList<string>> { { "change", new[] { "inc", "reset", "saved" } } },
                    new[] { "streak_days" },
                    new[] { "analytics", "onesignal_tag" },
                       "behavioral_no_pii", "timezone-change test; DST edge"),
                new TaxonomyRow("event_joined", "liveops",
                    new[] { "event_id" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new[] { "active_event" },
                    new[] { "analytics", "onesignal_event" },
                       "behavioral_no_pii", "join + verify journey entry"),
                new TaxonomyRow("event_completed", "liveops",
                    new[] { "event_id", "score" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new string[] { },
                    new[] { "analytics", "onesignal_event" },
                       "behavioral_no_pii", "verify journey exit"),
                new TaxonomyRow("ticket_earned", "economy",
                    new[] { "amount", "source", "balance_after" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>> { { "source", new[] { "level", "daily", "gift", "event", "ad" } } },
                    new string[] { },
                    new[] { "analytics" },
                       "behavioral_no_pii", "each source; balance reconciles"),
                new TaxonomyRow("ticket_spent", "economy",
                    new[] { "amount", "sink", "item_id", "balance_after" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>> { { "sink", new[] { "cosmetic", "saver" } } },
                    new string[] { },
                    new[] { "analytics" },
                       "behavioral_no_pii", "insufficient-funds path"),
                new TaxonomyRow("cosmetic_unlocked", "collection",
                    new[] { "item_id", "method" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>> { { "method", new[] { "tickets", "iap", "event", "default" } } },
                    new[] { "owned_cosmetics_count" },
                    new[] { "analytics" },
                       "behavioral_no_pii", "each method"),
                new TaxonomyRow("cosmetic_equipped", "collection",
                    new[] { "item_id" },
                    new[] { "previous_item_id" },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new[] { "active_theme" },
                    new[] { "analytics", "onesignal_tag(preferred_theme)" },
                       "behavioral_no_pii", "persist across restart"),
                new TaxonomyRow("ad_offer_viewed", "ads",
                    new[] { "placement", "level_id" },
                    new[] { "eligibility_reason" },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new string[] { },
                    new[] { "analytics" },
                       "behavioral_no_pii", "every placement; verify caps suppress"),
                new TaxonomyRow("ad_offer_declined", "ads",
                    new[] { "placement" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new string[] { },
                    new[] { "analytics" },
                       "behavioral_no_pii", "decline path never penalizes"),
                new TaxonomyRow("rewarded_ad_started", "ads",
                    new[] { "placement", "network", "ad_unit" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new string[] { },
                    new[] { "analytics", "revenuecat_adtracker" },
                       "behavioral_ad", "airplane-mode failure path"),
                new TaxonomyRow("rewarded_ad_completed", "ads",
                    new[] { "placement", "network", "reward_type", "reward_amount" },
                    new[] { "revenue_micros" },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new[] { "ad_watches_today" },
                    new[] { "analytics", "revenuecat_adtracker" },
                       "behavioral_ad", "reward granted exactly once; kill app mid-ad"),
                new TaxonomyRow("rewarded_ad_failed", "ads",
                    new[] { "placement", "network", "error_code" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new string[] { },
                    new[] { "analytics", "revenuecat_adtracker" },
                       "behavioral_ad", "no-fill simulation; verify graceful UI"),
                new TaxonomyRow("paywall_viewed", "monetization",
                    new[] { "placement", "offering_id", "paywall_variant" },
                    new[] { "trigger" },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new[] { "paywall_views" },
                    new[] { "analytics", "(revenuecat auto if RC paywall)" },
                       "behavioral_no_pii", "verify RC impression parity for custom paywalls"),
                new TaxonomyRow("paywall_dismissed", "monetization",
                    new[] { "placement", "offering_id", "view_duration_s" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new string[] { },
                    new[] { "analytics" },
                       "behavioral_no_pii", "close button + back + outside tap"),
                new TaxonomyRow("purchase_started", "monetization",
                    new[] { "product_id", "placement", "offering_id" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new string[] { },
                    new[] { "analytics" },
                       "transactional", "cancel at store sheet"),
                new TaxonomyRow("purchase_completed", "monetization",
                    new[] { "product_id", "offering_id", "placement", "price_local_bucket", "txn_id_hash" },
                    new[] { "first_purchase" },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new[] { "payer_status", "ltv_bucket" },
                    new[] { "analytics", "onesignal_event", "revenuecat(source of truth)" },
                       "transactional", "sandbox + real; duplicate-callback dedupe"),
                new TaxonomyRow("purchase_failed", "monetization",
                    new[] { "product_id", "error_domain", "error_code", "user_cancelled" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new string[] { },
                    new[] { "analytics" },
                       "transactional", "each error class; offline purchase"),
                new TaxonomyRow("restore_started", "monetization",
                    new string[] { },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new string[] { },
                    new[] { "analytics" },
                       "transactional", ""),
                new TaxonomyRow("restore_completed", "monetization",
                    new[] { "entitlements_restored_count" },
                    new[] { "products_restored" },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new[] { "payer_status" },
                    new[] { "analytics" },
                       "transactional", "fresh reinstall restore"),
                new TaxonomyRow("entitlement_changed", "monetization",
                    new[] { "entitlement_id", "change" },
                    new[] { "source" },
                    new Dictionary<string, IReadOnlyList<string>> { { "change", new[] { "granted", "revoked", "expired" } } },
                    new[] { "payer_status" },
                    new[] { "analytics", "onesignal_tag(payer_status)" },
                       "transactional", "refund revocation test via RC dashboard"),
                new TaxonomyRow("push_soft_prompt_viewed", "messaging",
                    new[] { "trigger", "variant" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new[] { "soft_prompt_seen" },
                    new[] { "analytics", "onesignal" },
                       "behavioral_no_pii", "verify only after value moment; cap 1/build"),
                new TaxonomyRow("push_permission_result", "messaging",
                    new[] { "granted", "source_trigger" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new[] { "push_enabled" },
                    new[] { "analytics", "onesignal" },
                       "behavioral_no_pii", "grant + deny + settings-later paths"),
                new TaxonomyRow("notification_opened", "messaging",
                    new[] { "campaign_id", "journey_id", "deep_link" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new[] { "last_notif_open_at" },
                    new[] { "analytics", "onesignal(native)" },
                       "behavioral_no_pii", "cold + warm + killed states route correctly"),
                new TaxonomyRow("iam_viewed", "messaging",
                    new[] { "message_id", "trigger" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new string[] { },
                    new[] { "onesignal" },
                       "behavioral_no_pii", "frequency cap verified"),
                new TaxonomyRow("scorecard_shared", "virality",
                    new[] { "mode", "seed_or_level", "channel_if_known" },
                    new[] { "score" },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new[] { "shares_count" },
                    new[] { "analytics" },
                       "behavioral_no_pii", "share + cancel; no PII in image"),
                new TaxonomyRow("challenge_opened", "virality",
                    new[] { "seed", "source" },
                    new[] { "referrer_hash" },
                    new Dictionary<string, IReadOnlyList<string>> { { "source", new[] { "link", "code" } } },
                    new string[] { },
                    new[] { "analytics" },
                       "behavioral_no_pii", "invalid/expired code fallback to home"),
                new TaxonomyRow("review_prompt_shown", "reviews",
                    new[] { "trigger", "sessions_count" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new[] { "review_prompted" },
                    new[] { "analytics" },
                       "behavioral_no_pii", "verify Play quota behavior; never after failure"),
                new TaxonomyRow("feedback_submitted", "reviews",
                    new[] { "topic", "rating_bucket" },
                    new[] { "build" },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new string[] { },
                    new[] { "analytics" },
                       "behavioral_no_pii", "skip path"),
                new TaxonomyRow("error_caught", "quality",
                    new[] { "domain", "code", "context" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new string[] { },
                    new[] { "crash_reporter" },
                       "diagnostic", "forced test exception"),
                new TaxonomyRow("perf_sample", "quality",
                    new[] { "avg_ms", "p95_ms", "device_tier", "level_id" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new string[] { },
                    new[] { "analytics_sampled_1pct" },
                       "diagnostic", "verify sampling rate + no jank from sampler"),
                new TaxonomyRow("save_migrated", "quality",
                    new[] { "from_version", "to_version", "success" },
                    new string[] { },
                    new Dictionary<string, IReadOnlyList<string>>(),
                    new string[] { },
                    new[] { "analytics" },
                       "diagnostic", "downgrade-protection test")
               };
           }

        public static bool TryBuild(string name, JObject parameters,
            out AnalyticsEvent e, out string error)
           {
            e = default;
            error = null;

               // (a) parameters == null -> treat as empty JObject
            if (parameters == null)
                parameters = new JObject();

               // (b) unknown event name
            var row = Rows.FirstOrDefault(r => r.Name == name);
            if (row == null)
               {
                error = "unknown event: " + name;
                return false;
               }

               // (c) each required param present
            foreach (var req in row.RequiredParams)
               {
                var reqProp = parameters.Property(req);
                if (reqProp == null || reqProp.Value.Type == JTokenType.Null) // review B7: an explicit JSON null is MISSING
                   {
                    error = "missing required param: " + req;
                    return false;
                   }
               }

               // (d) CLOSED SET: any key not in required U optional
            var allowedKeys = new HashSet<string>(row.RequiredParams);
            foreach (var opt in row.OptionalParams)
                allowedKeys.Add(opt);

            foreach (var prop in parameters.Properties())
               {
                if (!allowedKeys.Contains(prop.Name))
                   {
                    error = "undeclared key: " + prop.Name;
                    return false;
                   }
               }

               // (e) declared value domains
            foreach (var kv in row.ValueDomains)
               {
                var prop = parameters.Property(kv.Key);
                if (prop != null)
                   {
                    var val = prop.Value.ToString();
                    if (!kv.Value.Contains(val))
                       {
                        error = "invalid value for " + kv.Key;
                        return false;
                       }
                   }
               }

               // (f) success
            // review B5: clone at construction — the validated shape is frozen; later caller
            // mutations of their own JObject can never smuggle undeclared keys past the wall
            e = new AnalyticsEvent(name, (JObject)parameters.DeepClone());
            return true;
           }
       }
}