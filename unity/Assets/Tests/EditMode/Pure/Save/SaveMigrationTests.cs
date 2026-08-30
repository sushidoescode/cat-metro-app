using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Application.Save;
using CatMetro.Services;

namespace CatMetro.Tests.Save
{
    // Ordered migrations preserve unknown data; downgrade remains a read-only refusal. Daily
    // Live is the first real schema migration and must upgrade v1 saves additively.
    public sealed class SaveMigrationTests
    {
        [Test]
        public void MigrationTable_AppliesRegisteredStepsInOrder_V2ToV3()
        {
            var table = new MigrationTable()
                .Register(2, 3, payload =>
                {
                    payload["stubV3Marker"] = true;
                    return payload;
                });
            var migrated = table.Migrate(SaveDefaults.FreshPayload(), 2, 3);

            Assert.That(migrated, Is.Not.Null);
            Assert.That((bool)migrated["stubV3Marker"], Is.True);
            Assert.That((int)migrated["saveVersion"], Is.EqualTo(3),
                "the table stamps the target version after each step");
            Assert.That(migrated["ledger"], Is.Not.Null,
                "a migration step never deletes a key it does not understand (ADR-0006:72)");
        }

        [Test]
        public void DefaultV2ToV3_AddsCosmetics_WithoutReadingLeasesOrUnknownSiblings()
        {
            var v2 = SaveDefaults.FreshPayload();
            v2["saveVersion"] = 2;
            ((JObject)v2["profile"]).Remove("cosmetics");
            var leases = new JArray(
                new JObject { ["entitlementId"] = "outfit_conductor", ["expiresAtUnixSeconds"] = 99L },
                new JObject { ["futureLeaseShape"] = new JArray(1, "two") });
            v2["entitlements"]["localLeases"] = leases.DeepClone();
            v2["futureRoot"] = new JObject { ["kept"] = true };

            var migrated = MigrationTable.CreateDefault().Migrate(v2, 2, 3);

            Assert.That(migrated, Is.Not.Null, "the default table needs a v2-to-v3 step");
            Assert.That((int)migrated["saveVersion"], Is.EqualTo(3));
            Assert.That(migrated["profile"]["cosmetics"], Is.InstanceOf<JObject>());
            Assert.That(JToken.DeepEquals(migrated["entitlements"]["localLeases"], leases), Is.True);
            Assert.That((bool)migrated["futureRoot"]["kept"], Is.True);
        }

        [Test]
        public void DefaultV1ToV3_AppliesBothStepsInOrder()
        {
            var legacy = RepresentativeV1();

            var migrated = MigrationTable.CreateDefault().Migrate(legacy, 1, 3);

            Assert.That(migrated, Is.Not.Null, "the default table needs a complete v1-to-v3 chain");
            Assert.That((int)migrated["saveVersion"], Is.EqualTo(3));
            Assert.That(migrated["daily"]["completedKeys"], Is.InstanceOf<JArray>());
            Assert.That(migrated["profile"]["cosmetics"], Is.InstanceOf<JObject>());
        }

        [Test]
        public void DefaultV2ToV3_PreservesExistingCosmeticsAndUnknownProfileSiblings()
        {
            var v2 = SaveDefaults.FreshPayload();
            v2["saveVersion"] = 2;
            var profile = (JObject)v2["profile"];
            var cosmetics = new JObject
            {
                ["selectedCatId"] = "void_cat",
                ["earnedCatIds"] = new JArray("void_cat"),
                ["futureCosmetic"] = new JObject { ["kept"] = true },
            };
            profile["cosmetics"] = cosmetics.DeepClone();
            profile["futureProfileField"] = new JArray(3, 5, 8);

            var migrated = MigrationTable.CreateDefault().Migrate(v2, 2, 3);

            Assert.That(migrated, Is.Not.Null, "the default table needs a v2-to-v3 step");
            Assert.That(JToken.DeepEquals(migrated["profile"]["cosmetics"], cosmetics), Is.True);
            Assert.That(JToken.DeepEquals(migrated["profile"]["futureProfileField"],
                new JArray(3, 5, 8)), Is.True);
        }

        [TestCase("profile")]
        [TestCase("profile.cosmetics")]
        public void DefaultV2ToV3_MalformedKnownContainer_ReturnsNull(string path)
        {
            var v2 = SaveDefaults.FreshPayload();
            v2["saveVersion"] = 2;
            if (path == "profile.cosmetics")
                ((JObject)v2["profile"])["cosmetics"] = "not-an-object";
            else
                v2[path] = "not-an-object";

            Assert.That(MigrationTable.CreateDefault().Migrate(v2, 2, 3), Is.Null,
                path + " must fail closed instead of silently discarding malformed data");
        }

        [Test]
        public void MigrationTable_DefaultV1ToV2_UnionsEveryReservedField_AndPreservesLegacyCaps()
        {
            var legacy = SaveDefaults.FreshPayload();
            legacy["saveVersion"] = 1;
            var daily = (JObject)legacy["daily"];
            daily.Remove("trustedDateKey");
            daily.Remove("completedKeys");
            daily.Remove("lifetimeCompletions");
            var settings = (JObject)legacy["settings"];
            settings.Remove("dailyReminderEnabled");
            settings.Remove("dailyReminderPromptSeen");
            settings.Remove("dailyReminderSlot");
            ((JObject)legacy["entitlements"]).Remove("localLeases");
            var caps = (JObject)legacy["caps"];
            caps.Remove("rewarded");
            var counters = (JObject)caps["counters"];
            counters["rewind_failure"] = 1;
            counters["double_tickets"] = 2;
            counters["daily_gift_double"] = 3;
            counters["streak_saver"] = 4;
            counters["theme_rental"] = 5;
            var legacyCounters = (JObject)caps["counters"].DeepClone();
            legacy["futureExperiment"] = new JObject { ["kept"] = true };

            var migrated = MigrationTable.CreateDefault().Migrate(legacy, 1, 2);

            Assert.That(migrated, Is.Not.Null);
            Assert.That((int)migrated["saveVersion"], Is.EqualTo(2));
            Assert.That((string)migrated["daily"]["trustedDateKey"], Is.Empty);
            Assert.That(migrated["daily"]["completedKeys"], Is.InstanceOf<JArray>());
            Assert.That((int)migrated["daily"]["lifetimeCompletions"], Is.Zero);
            Assert.That((bool)migrated["settings"]["dailyReminderEnabled"], Is.False,
                "migration never infers consent");
            Assert.That((bool)migrated["settings"]["dailyReminderPromptSeen"], Is.False);
            Assert.That((string)migrated["settings"]["dailyReminderSlot"], Is.EqualTo("morning"));
            Assert.That(migrated["entitlements"]["localLeases"], Is.InstanceOf<JArray>());
            Assert.That(((JArray)migrated["entitlements"]["localLeases"]).Count, Is.Zero);
            Assert.That((string)migrated["caps"]["rewarded"]["dateKey"], Is.Empty);
            Assert.That(migrated["caps"]["rewarded"]["counters"], Is.InstanceOf<JObject>());
            Assert.That(((JObject)migrated["caps"]["rewarded"]["counters"]).Count, Is.Zero);
            Assert.That(JToken.DeepEquals(migrated["caps"]["counters"], legacyCounters), Is.True,
                "the legacy five-key caps counters must survive value-for-value");
            Assert.That((bool)migrated["futureExperiment"]["kept"], Is.True,
                "migration must not delete unknown keys");
            var migratedTwice = SaveSchemaV2.MigrateFromV1((JObject)migrated.DeepClone());
            Assert.That(JToken.DeepEquals(migratedTwice, migrated), Is.True,
                "the shared v1 migration must be idempotent before another lane extends v2");
        }

        [Test]
        public void MigrationTable_DefaultV1ToV2_IsAdditiveInsideOwnedSubObjects()
        {
            var legacy = SaveDefaults.FreshPayload();
            legacy["saveVersion"] = 1;
            var daily = (JObject)legacy["daily"];
            daily["trustedDateKey"] = "2026-08-27";
            daily.Remove("completedKeys");
            daily.Remove("lifetimeCompletions");
            daily["futureDailyField"] = 17;
            var settings = (JObject)legacy["settings"];
            settings["dailyReminderEnabled"] = true;
            settings.Remove("dailyReminderPromptSeen");
            settings.Remove("dailyReminderSlot");
            settings["futureSetting"] = "kept";
            var leases = new JArray(new JObject { ["productId"] = "named_theme" });
            ((JObject)legacy["entitlements"])["localLeases"] = leases.DeepClone();
            ((JObject)legacy["caps"])["rewarded"] = new JObject
            {
                ["dateKey"] = "2026-08-27",
                ["futureRewardedField"] = 9,
            };

            var migrated = MigrationTable.CreateDefault().Migrate(legacy, 1, 2);

            Assert.That(migrated, Is.Not.Null);
            Assert.That((int)migrated["saveVersion"], Is.EqualTo(2));
            Assert.That((string)migrated["daily"]["trustedDateKey"], Is.EqualTo("2026-08-27"));
            Assert.That(migrated["daily"]["completedKeys"], Is.InstanceOf<JArray>());
            Assert.That((int)migrated["daily"]["lifetimeCompletions"], Is.Zero);
            Assert.That((int)migrated["daily"]["futureDailyField"], Is.EqualTo(17));
            Assert.That((bool)migrated["settings"]["dailyReminderEnabled"], Is.True,
                "an existing preference must not be reset by migration");
            Assert.That((bool)migrated["settings"]["dailyReminderPromptSeen"], Is.False);
            Assert.That((string)migrated["settings"]["dailyReminderSlot"], Is.EqualTo("morning"));
            Assert.That((string)migrated["settings"]["futureSetting"], Is.EqualTo("kept"));
            Assert.That(JToken.DeepEquals(migrated["entitlements"]["localLeases"], leases), Is.True);
            Assert.That((string)migrated["caps"]["rewarded"]["dateKey"],
                Is.EqualTo("2026-08-27"));
            Assert.That(migrated["caps"]["rewarded"]["counters"], Is.InstanceOf<JObject>());
            Assert.That((int)migrated["caps"]["rewarded"]["futureRewardedField"], Is.EqualTo(9));
        }

        [TestCase("daily")]
        [TestCase("settings")]
        [TestCase("entitlements")]
        [TestCase("caps")]
        [TestCase("caps.rewarded")]
        public void SaveSchemaV2_MalformedKnownContainer_ReturnsNull(string path)
        {
            var legacy = RepresentativeV1();
            if (path == "caps.rewarded")
                ((JObject)legacy["caps"])["rewarded"] = "not-an-object";
            else
                legacy[path] = "not-an-object";

            Assert.That(SaveSchemaV2.MigrateFromV1(legacy), Is.Null,
                path + " must fail closed instead of silently discarding malformed data");
        }

        [Test]
        public void MigrationTable_RejectsDuplicateSourceVersion()
        {
            var table = new MigrationTable().Register(1, 2, payload => payload);

            Assert.Throws<System.ArgumentException>(() =>
                table.Register(1, 2, payload => payload));
        }

        [Test]
        public void SaveStore_DefaultMigration_RoundTripsRealV1FileAsUnionedV3Bytes()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            var legacy = RepresentativeV1();
            legacy["futureExperiment"] = new JObject
            {
                ["nested"] = new JArray("kept", 27),
            };
            var expectedUnknown = legacy["futureExperiment"].DeepClone();
            var legacyCounters = (JObject)legacy["caps"]["counters"];
            legacyCounters["rewind_failure"] = 11;
            legacyCounters["double_tickets"] = 12;
            legacyCounters["daily_gift_double"] = 13;
            legacyCounters["streak_saver"] = 14;
            legacyCounters["theme_rental"] = 15;
            var expectedLegacyCounters = legacyCounters.DeepClone();
            var expectedDefaultCosmetics = new JObject
            {
                ["selectedCatId"] = "red_tabby",
                ["earnedCatIds"] = new JArray(),
                ["earnedItemIds"] = new JArray(),
                ["loadouts"] = new JArray(new JObject
                {
                    ["catId"] = "red_tabby",
                    ["outfitId"] = "",
                    ["accessoryId"] = "",
                    ["frameId"] = "",
                }),
            };
            SFixtures.WriteRaw(store.SavePath, SFixtures.FileWithVersion(1, legacy));

            Assert.That(store.Load(), Is.EqualTo(LoadResult.Ok));
            Assert.That((int)store.State.Payload["saveVersion"], Is.EqualTo(3));
            Assert.That(store.TryCommitAtomic(), Is.True);

            var header = SaveHeader.TryParse(SFixtures.RawFile(store.SavePath),
                SaveDefaults.MAGIC, out var payloadBytes);
            Assert.That(header, Is.Not.Null);
            Assert.That(header.SaveVersion, Is.EqualTo(3));
            var filePayload = JObject.Parse(System.Text.Encoding.UTF8.GetString(payloadBytes));
            Assert.That((int)filePayload["saveVersion"], Is.EqualTo(3));
            Assert.That(filePayload["daily"]["completedKeys"], Is.InstanceOf<JArray>());
            Assert.That((bool)filePayload["settings"]["dailyReminderEnabled"], Is.False);
            Assert.That(filePayload["entitlements"]["localLeases"], Is.InstanceOf<JArray>());
            Assert.That((string)filePayload["caps"]["rewarded"]["dateKey"], Is.Empty);
            Assert.That(filePayload["caps"]["rewarded"]["counters"], Is.InstanceOf<JObject>());
            Assert.That(JToken.DeepEquals(filePayload["profile"]["cosmetics"],
                expectedDefaultCosmetics), Is.True,
                "the serialized artifact must carry the canonical cosmetics default inserted for v1");
            Assert.That(JToken.DeepEquals(filePayload["futureExperiment"], expectedUnknown), Is.True);
            Assert.That(JToken.DeepEquals(filePayload["caps"]["counters"],
                expectedLegacyCounters), Is.True,
                "the committed v2 artifact must retain every legacy cap value");

            var reloaded = SFixtures.Store(root);
            Assert.That(reloaded.Load(), Is.EqualTo(LoadResult.Ok));
            Assert.That(JToken.DeepEquals(reloaded.State.Payload, filePayload), Is.True,
                "the serialized v3 artifact must reload without another migration or data loss");
            Assert.That(JToken.DeepEquals(reloaded.State.Payload["profile"]["cosmetics"],
                expectedDefaultCosmetics), Is.True,
                "the reloaded artifact must retain the canonical cosmetics default inserted for v1");
        }

        [Test]
        public void MigrationTable_GapAfterCurrentVersion_ReturnsNull_NeverGuesses()
        {
            var table = MigrationTable.CreateDefault();
            Assert.That(table.Migrate(SaveDefaults.FreshPayload(), SaveDefaults.SAVE_VERSION,
                SaveDefaults.SAVE_VERSION + 1), Is.Null,
                "the default table must contain no unpublished v3->v4 production step");
        }

        [Test]
        public void Downgrade_IsRefused_FileUntouched_ReadOnly_EventRecorded()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            // A file from the future remains byte-identical and forces read-only mode.
            var futureVersion = checked((ushort)(SaveDefaults.SAVE_VERSION + 1));
            var future = SFixtures.FileWithVersion(futureVersion);
            SFixtures.WriteRaw(store.SavePath, future);

            var result = store.Load();

            Assert.That(result, Is.EqualTo(LoadResult.RefusedDowngrade));
            Assert.That(SFixtures.RawFile(store.SavePath), Is.EqualTo(future),
                "the newer file's bytes are left untouched");
            Assert.That(store.ReadOnlyMode, Is.True);
            Assert.That(store.ReportedEvents.Any(e => e.Name == "save_migrated"
                && e.Detail.Contains("from=" + futureVersion)
                && e.Detail.Contains("to=" + SaveDefaults.SAVE_VERSION)
                && e.Detail.Contains("success=false")), Is.True);

            // read-only means commits refuse: the file's bytes stay byte-identical after both
            // commit paths, and the refusal is recorded, not silent.
            store.State.Tickets = 5;
            store.CommitAtomic();
            Assert.That(store.TryCommitWithin(50), Is.False);
            Assert.That(SFixtures.RawFile(store.SavePath), Is.EqualTo(future));
            Assert.That(store.ReportedEvents.Any(e => e.Detail.Contains("save_readonly")), Is.True);
        }

        private static JObject RepresentativeV1()
        {
            var legacy = SaveDefaults.FreshPayload();
            legacy["saveVersion"] = 1;
            var daily = (JObject)legacy["daily"];
            daily.Remove("trustedDateKey");
            daily.Remove("completedKeys");
            daily.Remove("lifetimeCompletions");
            var settings = (JObject)legacy["settings"];
            settings.Remove("dailyReminderEnabled");
            settings.Remove("dailyReminderPromptSeen");
            settings.Remove("dailyReminderSlot");
            ((JObject)legacy["entitlements"]).Remove("localLeases");
            ((JObject)legacy["caps"]).Remove("rewarded");
            ((JObject)legacy["profile"]).Remove("cosmetics");
            return legacy;
        }
    }
}
