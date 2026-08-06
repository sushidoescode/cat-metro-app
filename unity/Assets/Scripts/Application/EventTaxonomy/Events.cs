using Newtonsoft.Json.Linq;
using CatMetro.Services;

namespace CatMetro.Application.EventTaxonomy
{
    public static class Events
    {
        public static AnalyticsEvent AppOpen(string sessionId, string appVersion, int installAgeDays, string buildChannel, string referrerSource = null, string notificationCampaignId = null)
        {
            var p = new JObject();
            p["session_id"] = sessionId;
            p["app_version"] = appVersion;
            p["install_age_days"] = installAgeDays;
            p["build_channel"] = buildChannel;
            if (referrerSource != null) p["referrer_source"] = referrerSource;
            if (notificationCampaignId != null) p["notification_campaign_id"] = notificationCampaignId;
            if (Taxonomy.TryBuild("app_open", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent FirstOpen(string appVersion, string deviceTier, string osApiLevel, string installReferrer = null)
        {
            var p = new JObject();
            p["app_version"] = appVersion;
            p["device_tier"] = deviceTier;
            p["os_api_level"] = osApiLevel;
            if (installReferrer != null) p["install_referrer"] = installReferrer;
            if (Taxonomy.TryBuild("first_open", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent TutorialStarted(string variant = null)
        {
            var p = new JObject();
            if (variant != null) p["variant"] = variant;
            if (Taxonomy.TryBuild("tutorial_started", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent TutorialStep(string stepId, int durationS, int retries = 0)
        {
            var p = new JObject();
            p["step_id"] = stepId;
            p["duration_s"] = durationS;
            if (retries > 0) p["retries"] = retries;
            if (Taxonomy.TryBuild("tutorial_step", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent TutorialCompleted(int durationS, int totalRetries)
        {
            var p = new JObject();
            p["duration_s"] = durationS;
            p["total_retries"] = totalRetries;
            if (Taxonomy.TryBuild("tutorial_completed", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent LevelStarted(string levelId, string mode, int attempt, string difficultyTarget, string fromScreen = null, string activeTheme = null)
        {
            var p = new JObject();
            p["level_id"] = levelId;
            p["mode"] = mode;
            p["attempt"] = attempt;
            p["difficulty_target"] = difficultyTarget;
            if (fromScreen != null) p["from_screen"] = fromScreen;
            if (activeTheme != null) p["active_theme"] = activeTheme;
            if (Taxonomy.TryBuild("level_started", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent SwitchToggled(string levelId, string switchId, int tick)
        {
            var p = new JObject();
            p["level_id"] = levelId;
            p["switch_id"] = switchId;
            p["tick"] = tick;
            if (Taxonomy.TryBuild("switch_toggled", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent LevelCompleted(string levelId, string mode, int attempt, int durationS, int switchesUsed, bool perfect, int score, int stars, string rewindUsed = null)
        {
            var p = new JObject();
            p["level_id"] = levelId;
            p["mode"] = mode;
            p["attempt"] = attempt;
            p["duration_s"] = durationS;
            p["switches_used"] = switchesUsed;
            p["perfect"] = perfect;
            p["score"] = score;
            p["stars"] = stars;
            if (rewindUsed != null) p["rewind_used"] = rewindUsed;
            if (Taxonomy.TryBuild("level_completed", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent LevelFailed(string levelId, string mode, int attempt, string failReason, int progressPct, int durationS, string congestedNode = null, string queueState = null)
        {
            var p = new JObject();
            p["level_id"] = levelId;
            p["mode"] = mode;
            p["attempt"] = attempt;
            p["fail_reason"] = failReason;
            p["progress_pct"] = progressPct;
            p["duration_s"] = durationS;
            if (congestedNode != null) p["congested_node"] = congestedNode;
            if (queueState != null) p["queue_state"] = queueState;
            if (Taxonomy.TryBuild("level_failed", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent LevelQuit(string levelId, int attempt, int progressPct, int durationS)
        {
            var p = new JObject();
            p["level_id"] = levelId;
            p["attempt"] = attempt;
            p["progress_pct"] = progressPct;
            p["duration_s"] = durationS;
            if (Taxonomy.TryBuild("level_quit", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent RewindUsed(string levelId, string source, string balanceAfter)
        {
            var p = new JObject();
            p["level_id"] = levelId;
            p["source"] = source;
            p["balance_after"] = balanceAfter;
            if (Taxonomy.TryBuild("rewind_used", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent DailyUnlocked(string highestLevel = null)
        {
            var p = new JObject();
            if (highestLevel != null) p["highest_level"] = highestLevel;
            if (Taxonomy.TryBuild("daily_unlocked", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent DailyStarted(long seed, string localDate, int streakDays = 0)
        {
            var p = new JObject();
            p["seed"] = seed;
            p["local_date"] = localDate;
            if (streakDays > 0) p["streak_days"] = streakDays;
            if (Taxonomy.TryBuild("daily_started", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent DailyCompleted(long seed, int score, string rankBucket, int streakDays)
        {
            var p = new JObject();
            p["seed"] = seed;
            p["score"] = score;
            p["rank_bucket"] = rankBucket;
            p["streak_days"] = streakDays;
            if (Taxonomy.TryBuild("daily_completed", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent StreakChanged(int newStreak, string change, string saverSource = null, int streakDays = 0)
        {
            var p = new JObject();
            p["new_streak"] = newStreak;
            p["change"] = change;
            if (saverSource != null) p["saver_source"] = saverSource;
            if (streakDays > 0) p["streak_days"] = streakDays;
            if (Taxonomy.TryBuild("streak_changed", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent EventJoined(string eventId)
        {
            var p = new JObject();
            p["event_id"] = eventId;
            if (Taxonomy.TryBuild("event_joined", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent EventCompleted(string eventId, int score)
        {
            var p = new JObject();
            p["event_id"] = eventId;
            p["score"] = score;
            if (Taxonomy.TryBuild("event_completed", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent TicketEarned(int amount, string source, string balanceAfter)
        {
            var p = new JObject();
            p["amount"] = amount;
            p["source"] = source;
            p["balance_after"] = balanceAfter;
            if (Taxonomy.TryBuild("ticket_earned", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent TicketSpent(int amount, string sink, string itemId, string balanceAfter)
        {
            var p = new JObject();
            p["amount"] = amount;
            p["sink"] = sink;
            p["item_id"] = itemId;
            p["balance_after"] = balanceAfter;
            if (Taxonomy.TryBuild("ticket_spent", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent CosmeticUnlocked(string itemId, string method)
        {
            var p = new JObject();
            p["item_id"] = itemId;
            p["method"] = method;
            if (Taxonomy.TryBuild("cosmetic_unlocked", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent CosmeticEquipped(string itemId, string previousItemId = null)
        {
            var p = new JObject();
            p["item_id"] = itemId;
            if (previousItemId != null) p["previous_item_id"] = previousItemId;
            if (Taxonomy.TryBuild("cosmetic_equipped", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent AdOfferViewed(string placement, string levelId, string eligibilityReason = null)
        {
            var p = new JObject();
            p["placement"] = placement;
            p["level_id"] = levelId;
            if (eligibilityReason != null) p["eligibility_reason"] = eligibilityReason;
            if (Taxonomy.TryBuild("ad_offer_viewed", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent AdOfferDeclined(string placement)
        {
            var p = new JObject();
            p["placement"] = placement;
            if (Taxonomy.TryBuild("ad_offer_declined", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent RewardedAdStarted(string placement, string network, string adUnit)
        {
            var p = new JObject();
            p["placement"] = placement;
            p["network"] = network;
            p["ad_unit"] = adUnit;
            if (Taxonomy.TryBuild("rewarded_ad_started", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent RewardedAdCompleted(string placement, string network, string rewardType, int rewardAmount, long revenueMicros = 0)
        {
            var p = new JObject();
            p["placement"] = placement;
            p["network"] = network;
            p["reward_type"] = rewardType;
            p["reward_amount"] = rewardAmount;
            if (revenueMicros > 0) p["revenue_micros"] = revenueMicros;
            if (Taxonomy.TryBuild("rewarded_ad_completed", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent RewardedAdFailed(string placement, string network, string errorCode)
        {
            var p = new JObject();
            p["placement"] = placement;
            p["network"] = network;
            p["error_code"] = errorCode;
            if (Taxonomy.TryBuild("rewarded_ad_failed", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent PaywallViewed(string placement, string offeringId, string paywallVariant, string trigger = null)
        {
            var p = new JObject();
            p["placement"] = placement;
            p["offering_id"] = offeringId;
            p["paywall_variant"] = paywallVariant;
            if (trigger != null) p["trigger"] = trigger;
            if (Taxonomy.TryBuild("paywall_viewed", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent PaywallDismissed(string placement, string offeringId, int viewDurationS)
        {
            var p = new JObject();
            p["placement"] = placement;
            p["offering_id"] = offeringId;
            p["view_duration_s"] = viewDurationS;
            if (Taxonomy.TryBuild("paywall_dismissed", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent PurchaseStarted(string productId, string placement, string offeringId)
        {
            var p = new JObject();
            p["product_id"] = productId;
            p["placement"] = placement;
            p["offering_id"] = offeringId;
            if (Taxonomy.TryBuild("purchase_started", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent PurchaseCompleted(string productId, string offeringId, string placement, string priceLocalBucket, string txnIdHash, string firstPurchase = null)
        {
            var p = new JObject();
            p["product_id"] = productId;
            p["offering_id"] = offeringId;
            p["placement"] = placement;
            p["price_local_bucket"] = priceLocalBucket;
            p["txn_id_hash"] = txnIdHash;
            if (firstPurchase != null) p["first_purchase"] = firstPurchase;
            if (Taxonomy.TryBuild("purchase_completed", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent PurchaseFailed(string productId, string errorDomain, string errorCode, bool userCancelled)
        {
            var p = new JObject();
            p["product_id"] = productId;
            p["error_domain"] = errorDomain;
            p["error_code"] = errorCode;
            p["user_cancelled"] = userCancelled;
            if (Taxonomy.TryBuild("purchase_failed", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent RestoreStarted()
        {
            var p = new JObject();
            if (Taxonomy.TryBuild("restore_started", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent RestoreCompleted(int entitlementsRestoredCount, string productsRestored = null)
        {
            var p = new JObject();
            p["entitlements_restored_count"] = entitlementsRestoredCount;
            if (productsRestored != null) p["products_restored"] = productsRestored;
            if (Taxonomy.TryBuild("restore_completed", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent EntitlementChanged(string entitlementId, string change, string source = null, string payerStatus = null)
        {
            var p = new JObject();
            p["entitlement_id"] = entitlementId;
            p["change"] = change;
            if (source != null) p["source"] = source;
            if (payerStatus != null) p["payer_status"] = payerStatus;
            if (Taxonomy.TryBuild("entitlement_changed", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent PushSoftPromptViewed(string trigger, string variant)
        {
            var p = new JObject();
            p["trigger"] = trigger;
            p["variant"] = variant;
            if (Taxonomy.TryBuild("push_soft_prompt_viewed", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent PushPermissionResult(bool granted, string sourceTrigger)
        {
            var p = new JObject();
            p["granted"] = granted;
            p["source_trigger"] = sourceTrigger;
            if (Taxonomy.TryBuild("push_permission_result", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent NotificationOpened(string campaignId, string journeyId, string deepLink)
        {
            var p = new JObject();
            p["campaign_id"] = campaignId;
            p["journey_id"] = journeyId;
            p["deep_link"] = deepLink;
            if (Taxonomy.TryBuild("notification_opened", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent IamViewed(string messageId, string trigger)
        {
            var p = new JObject();
            p["message_id"] = messageId;
            p["trigger"] = trigger;
            if (Taxonomy.TryBuild("iam_viewed", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent ScorecardShared(string mode, string seedOrLevel, string channelIfKnown, int score = 0, int sharesCount = 0)
        {
            var p = new JObject();
            p["mode"] = mode;
            p["seed_or_level"] = seedOrLevel;
            p["channel_if_known"] = channelIfKnown;
            if (score > 0) p["score"] = score;
            if (sharesCount > 0) p["shares_count"] = sharesCount;
            if (Taxonomy.TryBuild("scorecard_shared", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent ChallengeOpened(string seed, string source, string referrerHash = null)
        {
            var p = new JObject();
            p["seed"] = seed;
            p["source"] = source;
            if (referrerHash != null) p["referrer_hash"] = referrerHash;
            if (Taxonomy.TryBuild("challenge_opened", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent ReviewPromptShown(string trigger, int sessionsCount)
        {
            var p = new JObject();
            p["trigger"] = trigger;
            p["sessions_count"] = sessionsCount;
            if (Taxonomy.TryBuild("review_prompt_shown", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent FeedbackSubmitted(string topic, string ratingBucket, string build = null)
        {
            var p = new JObject();
            p["topic"] = topic;
            p["rating_bucket"] = ratingBucket;
            if (build != null) p["build"] = build;
            if (Taxonomy.TryBuild("feedback_submitted", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent ErrorCaught(string domain, string code, string context)
        {
            var p = new JObject();
            p["domain"] = domain;
            p["code"] = code;
            p["context"] = context;
            if (Taxonomy.TryBuild("error_caught", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent PerfSample(double avgMs, double p95Ms, string deviceTier, string levelId)
        {
            var p = new JObject();
            p["avg_ms"] = avgMs;
            p["p95_ms"] = p95Ms;
            p["device_tier"] = deviceTier;
            p["level_id"] = levelId;
            if (Taxonomy.TryBuild("perf_sample", p, out var e, out var err)) return e;
            return default;
        }

        public static AnalyticsEvent SaveMigrated(string fromVersion, string toVersion, bool success)
        {
            var p = new JObject();
            p["from_version"] = fromVersion;
            p["to_version"] = toVersion;
            p["success"] = success;
            if (Taxonomy.TryBuild("save_migrated", p, out var e, out var err)) return e;
            return default;
        }
    }
}