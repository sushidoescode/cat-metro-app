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
        public void MigrationTable_AppliesRegisteredStepsInOrder_V3ToV4()
        {
            var table = new MigrationTable()
                .Register(3, 4, payload =>
                {
                    payload["stubV4Marker"] = true;
                    return payload;
                });
            var migrated = table.Migrate(SaveDefaults.FreshPayload(), 3, 4);

            Assert.That(migrated, Is.Not.Null);
            Assert.That((bool)migrated["stubV4Marker"], Is.True);
            Assert.That((int)migrated["saveVersion"], Is.EqualTo(4),
                "the table stamps the target version after each step");
            Assert.That(migrated["ledger"], Is.Not.Null,
                "a migration step never deletes a key it does not understand (ADR-0006:72)");
        }

        [Test]
        public void MigrationTable_DefaultV2ToV3_AddsReminderDefaults_AndPreservesUnknownData()
        {
            var v2 = SaveDefaults.FreshPayload();
            v2["saveVersion"] = 2;
            var settings = (JObject)v2["settings"];
            settings.Remove("dailyReminderEnabled");
            settings.Remove("dailyReminderPromptSeen");
            settings.Remove("dailyReminderSlot");
            v2["futureExperiment"] = new JObject { ["kept"] = true };

            var migrated = new MigrationTable().Migrate(v2, 2, 3);

            Assert.That(migrated, Is.Not.Null);
            Assert.That((int)migrated["saveVersion"], Is.EqualTo(3));
            Assert.That((bool)migrated["settings"]["dailyReminderEnabled"], Is.False,
                "migration never infers consent");
            Assert.That((bool)migrated["settings"]["dailyReminderPromptSeen"], Is.False);
            Assert.That((string)migrated["settings"]["dailyReminderSlot"], Is.EqualTo("morning"));
            Assert.That((bool)migrated["futureExperiment"]["kept"], Is.True,
                "migration must not delete unknown keys");
        }

        [Test]
        public void MigrationTable_DefaultV1ToV2_AddsDailyFields_AndPreservesUnknownData()
        {
            var legacy = SaveDefaults.FreshPayload();
            legacy["saveVersion"] = 1;
            var daily = (JObject)legacy["daily"];
            daily.Remove("trustedDateKey");
            daily.Remove("completedKeys");
            daily.Remove("lifetimeCompletions");
            legacy["futureExperiment"] = new JObject { ["kept"] = true };

            var migrated = new MigrationTable().Migrate(legacy, 1, 2);

            Assert.That(migrated, Is.Not.Null);
            Assert.That((int)migrated["saveVersion"], Is.EqualTo(2));
            Assert.That((string)migrated["daily"]["trustedDateKey"], Is.Empty);
            Assert.That(migrated["daily"]["completedKeys"], Is.InstanceOf<JArray>());
            Assert.That((int)migrated["daily"]["lifetimeCompletions"], Is.Zero);
            Assert.That((bool)migrated["futureExperiment"]["kept"], Is.True);
        }

        [Test]
        public void MigrationTable_GapAfterCurrentVersion_ReturnsNull_NeverGuesses()
        {
            var table = new MigrationTable();
            Assert.That(table.Migrate(SaveDefaults.FreshPayload(), 3, 4), Is.Null);
        }

        [Test]
        public void Downgrade_IsRefused_FileUntouched_ReadOnly_EventRecorded()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            // A file from the future remains byte-identical and forces read-only mode.
            var future = SFixtures.FileWithVersion(4);
            SFixtures.WriteRaw(store.SavePath, future);

            var result = store.Load();

            Assert.That(result, Is.EqualTo(LoadResult.RefusedDowngrade));
            Assert.That(SFixtures.RawFile(store.SavePath), Is.EqualTo(future),
                "the newer file's bytes are left untouched");
            Assert.That(store.ReadOnlyMode, Is.True);
            Assert.That(store.ReportedEvents.Any(e => e.Name == "save_migrated"
                && e.Detail.Contains("from=4") && e.Detail.Contains("to=3")
                && e.Detail.Contains("success=false")), Is.True);

            // read-only means commits refuse: the file's bytes stay byte-identical after both
            // commit paths, and the refusal is recorded, not silent.
            store.State.Tickets = 5;
            store.CommitAtomic();
            Assert.That(store.TryCommitWithin(50), Is.False);
            Assert.That(SFixtures.RawFile(store.SavePath), Is.EqualTo(future));
            Assert.That(store.ReportedEvents.Any(e => e.Detail.Contains("save_readonly")), Is.True);
        }
    }
}
