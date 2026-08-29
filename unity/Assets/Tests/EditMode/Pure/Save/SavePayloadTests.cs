using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Application.Save;

namespace CatMetro.Tests.Save
{
    // The v2 payload remains additive over ADR-0006's v1 shape. Daily Live, reminders, local
    // leases, and rewarded-video caps share this one pre-public upgrade.
    public sealed class SavePayloadTests
    {
        private static readonly string[] TopLevel =
        {
            "saveVersion", "contentHash", "profile", "progress", "daily", "economy",
            "caps", "ledger", "entitlements", "flags", "breadcrumbs", "settings",
        };

        [Test]
        public void TopLevelKeySet_ExactlyAdr0006()
        {
            var keys = SaveDefaults.FreshPayload().Properties().Select(p => p.Name).ToArray();
            Assert.That(keys, Is.EquivalentTo(TopLevel), "no more, no fewer (criterion 2)");
        }

        [Test]
        public void FreshPayload_IsV2_WithEveryReservedDefault()
        {
            var payload = SaveDefaults.FreshPayload();

            Assert.That(SaveDefaults.SAVE_VERSION, Is.EqualTo(2));
            Assert.That((int)payload["saveVersion"], Is.EqualTo(2));
            Assert.That((int)payload["daily"]["lifetimeCompletions"], Is.Zero);
            Assert.That((string)payload["daily"]["trustedDateKey"], Is.Empty);
            Assert.That(payload["daily"]["completedKeys"], Is.InstanceOf<JArray>());
            Assert.That(((JArray)payload["daily"]["completedKeys"]).Count, Is.Zero);
            Assert.That((bool)payload["settings"]["dailyReminderEnabled"], Is.False);
            Assert.That((bool)payload["settings"]["dailyReminderPromptSeen"], Is.False);
            Assert.That((string)payload["settings"]["dailyReminderSlot"], Is.EqualTo("morning"));
            Assert.That(payload["entitlements"]["localLeases"], Is.InstanceOf<JArray>());
            Assert.That(((JArray)payload["entitlements"]["localLeases"]).Count, Is.Zero);
            Assert.That((string)payload["caps"]["rewarded"]["dateKey"], Is.Empty);
            Assert.That(payload["caps"]["rewarded"]["counters"], Is.InstanceOf<JObject>());
            Assert.That(((JObject)payload["caps"]["rewarded"]["counters"]).Count, Is.Zero);
        }

        // Review F5: criterion 2 says "the SERIALISED payload's top-level keys" — assert the
        // bytes on disk, not the in-memory object, so a serializer-settings change (e.g. a
        // future NullValueHandling tweak at the shared settings site) that silently drops the
        // only null key (breadcrumbs.purchase) fails HERE before an irreversible v1 file ships.
        [Test]
        public void SerializedFile_CarriesExactlyTheKeySet_IncludingTheNullKey()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            store.CommitAtomic();

            var parsed = SaveHeader.TryParse(SFixtures.RawFile(store.SavePath),
                SaveDefaults.MAGIC, out var payloadBytes);
            Assert.That(parsed, Is.Not.Null);
            var file = JObject.Parse(System.Text.Encoding.UTF8.GetString(payloadBytes));
            Assert.That(file.Properties().Select(p => p.Name).ToArray(),
                Is.EquivalentTo(TopLevel), "the FILE's key set, no more, no fewer");
            Assert.That(((JObject)file["breadcrumbs"]).Property("purchase"), Is.Not.Null,
                "the null-valued key must survive serialisation");
            Assert.That(file["breadcrumbs"]["purchase"].Type, Is.EqualTo(JTokenType.Null));
        }

        [Test]
        public void CapsCounters_ExactlyTheFiveLockedAdSurfaces()
        {
            var counters = (JObject)SaveDefaults.FreshPayload()["caps"]["counters"];
            Assert.That(counters.Properties().Select(p => p.Name), Is.EquivalentTo(new[]
                { "rewind_failure", "double_tickets", "daily_gift_double", "streak_saver", "theme_rental" }));
        }

        [Test]
        public void Flags_ExactlyTheSixAdr0007Keys()
        {
            var flags = (JObject)SaveDefaults.FreshPayload()["flags"];
            Assert.That(flags.Properties().Select(p => p.Name), Is.EquivalentTo(new[]
                { "ads_enabled", "paywall_placements", "daily_enabled", "weekly_event", "share_card", "leaderboard" }));
        }

        [Test]
        public void Ledger_KeySchemeDedupeAudit()
        {
            var ledger = (JObject)SaveDefaults.FreshPayload()["ledger"];
            Assert.That(ledger.Properties().Select(p => p.Name),
                Is.EquivalentTo(new[] { "keyScheme", "dedupe", "audit" }));
            Assert.That((string)ledger["keyScheme"], Is.EqualTo("cm-ledger-v1"));
            Assert.That(ledger["dedupe"], Is.InstanceOf<JArray>());
            Assert.That(ledger["audit"], Is.InstanceOf<JArray>());
        }

        [Test]
        public void EnumeratedSubObjects_MatchTheAdrShapes()
        {
            var p = SaveDefaults.FreshPayload();
            Assert.That(((JObject)p["profile"]).Properties().Select(x => x.Name),
                Is.EquivalentTo(new[] { "createdAtUtc", "lastSeenAtUtc", "sessionCount" }));
            Assert.That(((JObject)p["progress"]).Properties().Select(x => x.Name),
                Is.EquivalentTo(new[] { "levels", "districtsUnlocked", "tutorialDone" }));
            Assert.That(((JObject)p["daily"]).Properties().Select(x => x.Name),
                Is.EquivalentTo(new[]
                {
                    "lastDateKey", "streakDays", "playedKeys", "trustedDateKey",
                    "completedKeys", "lifetimeCompletions",
                }));
            Assert.That(((JObject)p["economy"]).Properties().Select(x => x.Name),
                Is.EquivalentTo(new[] { "tickets", "rewindBalance", "freeRewindDateKey" }));
            Assert.That(((JObject)p["caps"]).Properties().Select(x => x.Name),
                Is.EquivalentTo(new[] { "dateKey", "counters", "rewarded" }));
            Assert.That(((JObject)p["caps"]["rewarded"]).Properties().Select(x => x.Name),
                Is.EquivalentTo(new[] { "dateKey", "counters" }));
            Assert.That(((JObject)p["entitlements"]).Properties().Select(x => x.Name),
                Is.EquivalentTo(new[] { "appUserId", "active", "fetchedAtUtc", "localLeases" }));
            Assert.That(((JObject)p["breadcrumbs"]).Properties().Select(x => x.Name),
                Is.EquivalentTo(new[] { "screenStack", "purchase" }));
            Assert.That(((JObject)p["settings"]).Properties().Select(x => x.Name),
                Is.EquivalentTo(new[]
                {
                    "haptics", "motion", "audio", "equippedThemeId", "dailyReminderEnabled",
                    "dailyReminderPromptSeen", "dailyReminderSlot",
                }));
        }

        // Criterion 3: the three OPEN sub-shapes are ABSENT, not guessed.
        [Test]
        public void OpenShape_CapsSessionCounters_IsAbsent() =>
            Assert.That(SaveDefaults.FreshPayload()["caps"]["sessionCounters"], Is.Null);

        [Test]
        public void OpenShape_PaywallPlacements_StaysBool() =>
            Assert.That(SaveDefaults.FreshPayload()["flags"]["paywall_placements"].Type,
                Is.EqualTo(JTokenType.Boolean));

        [Test]
        public void OpenShape_PurchaseBreadcrumb_NullOrOpaqueState()
        {
            Assert.That(SaveDefaults.FreshPayload()["breadcrumbs"]["purchase"].Type,
                Is.EqualTo(JTokenType.Null));

            // When present, state is an OPAQUE string round-tripped untouched (RK-39: no RC API
            // is invented). Load a payload carrying a value no enum would contain.
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            store.State.Payload["breadcrumbs"]["purchase"] = new JObject
            {
                ["productId"] = "rewind_pack_small",
                ["placement"] = "post_fail",
                ["startedAtUtc"] = 1754300000000L,
                ["state"] = "someFutureSdkStateNoEnumKnows",
            };
            store.CommitAtomic();

            var reload = SFixtures.Store(root);
            Assert.That(reload.Load(), Is.EqualTo(CatMetro.Services.LoadResult.Ok));
            Assert.That((string)reload.State.Payload["breadcrumbs"]["purchase"]["state"],
                Is.EqualTo("someFutureSdkStateNoEnumKnows"));
        }

        // ADR-0006:72: a migration step (and the store itself) never deletes a key it does not
        // understand — an unknown key survives commit + reload byte-for-byte.
        [Test]
        public void UnknownKey_RoundTripsUntouched()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            store.State.Payload["futureExperiment"] = new JObject { ["nested"] = new JArray(1, 2, 3) };
            store.CommitAtomic();

            var reload = SFixtures.Store(root);
            reload.Load();
            Assert.That(JToken.DeepEquals(reload.State.Payload["futureExperiment"],
                new JObject { ["nested"] = new JArray(1, 2, 3) }), Is.True);
        }
    }
}
