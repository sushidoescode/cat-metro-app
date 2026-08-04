using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Application.Save;

namespace CatMetro.Tests.Save
{
    // CM-C7 criteria 2 + 3: the payload v1 key set is EXACTLY ADR-0006 §2 — and the three OPEN
    // sub-shapes are absent, not guessed.
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
                Is.EquivalentTo(new[] { "lastDateKey", "streakDays", "playedKeys" }));
            Assert.That(((JObject)p["economy"]).Properties().Select(x => x.Name),
                Is.EquivalentTo(new[] { "tickets", "rewindBalance", "freeRewindDateKey" }));
            Assert.That(((JObject)p["entitlements"]).Properties().Select(x => x.Name),
                Is.EquivalentTo(new[] { "appUserId", "active", "fetchedAtUtc" }));
            Assert.That(((JObject)p["breadcrumbs"]).Properties().Select(x => x.Name),
                Is.EquivalentTo(new[] { "screenStack", "purchase" }));
            Assert.That(((JObject)p["settings"]).Properties().Select(x => x.Name),
                Is.EquivalentTo(new[] { "haptics", "motion", "audio", "equippedThemeId" }));
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
