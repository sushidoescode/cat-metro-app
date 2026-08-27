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
            Assert.That(table.Migrate(SaveDefaults.FreshPayload(), 2, 3), Is.Null);
        }

        [Test]
        public void Downgrade_IsRefused_FileUntouched_ReadOnly_EventRecorded()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            // A file from the future remains byte-identical and forces read-only mode.
            var future = SFixtures.FileWithVersion(3);
            SFixtures.WriteRaw(store.SavePath, future);

            var result = store.Load();

            Assert.That(result, Is.EqualTo(LoadResult.RefusedDowngrade));
            Assert.That(SFixtures.RawFile(store.SavePath), Is.EqualTo(future),
                "the newer file's bytes are left untouched");
            Assert.That(store.ReadOnlyMode, Is.True);
            Assert.That(store.ReportedEvents.Any(e => e.Name == "save_migrated"
                && e.Detail.Contains("from=3") && e.Detail.Contains("to=2")
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
