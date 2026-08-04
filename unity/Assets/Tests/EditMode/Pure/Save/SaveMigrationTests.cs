using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Application.Save;
using CatMetro.Services;

namespace CatMetro.Tests.Save
{
    // CM-C7 criterion 7: ordered migration table; downgrade refused (file untouched, read-only
    // in-memory default profile, save_migrated(...success=false) recorded). A-C7-4: the v1->v2
    // step is a STUB — no real v2 schema is invented.
    public sealed class SaveMigrationTests
    {
        [Test]
        public void MigrationTable_AppliesStepsInOrder_V1ToV2Stub()
        {
            var table = new MigrationTable()
                .Register(1, 2, payload =>
                {
                    payload["stubV2Marker"] = true; // stub step: additive only
                    return payload;
                });
            var migrated = table.Migrate(SaveDefaults.FreshPayload(), 1, 2);

            Assert.That(migrated, Is.Not.Null);
            Assert.That((bool)migrated["stubV2Marker"], Is.True);
            Assert.That((int)migrated["saveVersion"], Is.EqualTo(2),
                "the table stamps the target version after each step");
            Assert.That(migrated["ledger"], Is.Not.Null,
                "a migration step never deletes a key it does not understand (ADR-0006:72)");
        }

        [Test]
        public void MigrationTable_GapReturnsNull_NeverGuesses()
        {
            var table = new MigrationTable(); // no steps registered
            Assert.That(table.Migrate(SaveDefaults.FreshPayload(), 1, 2), Is.Null);
        }

        [Test]
        public void Downgrade_IsRefused_FileUntouched_ReadOnly_EventRecorded()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            // a file from "the future": saveVersion 2 > build's 1
            var future = SFixtures.FileWithVersion(2);
            SFixtures.WriteRaw(store.SavePath, future);

            var result = store.Load();

            Assert.That(result, Is.EqualTo(LoadResult.RefusedDowngrade));
            Assert.That(SFixtures.RawFile(store.SavePath), Is.EqualTo(future),
                "the newer file's bytes are left untouched");
            Assert.That(store.ReadOnlyMode, Is.True);
            Assert.That(store.ReportedEvents.Any(e => e.Name == "save_migrated"
                && e.Detail.Contains("from=2") && e.Detail.Contains("to=1")
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
