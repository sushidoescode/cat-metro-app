using System.Linq;
using System.Threading;
using NUnit.Framework;
using CatMetro.Application.Analytics;
using CatMetro.Application.Save;
using CatMetro.Bootstrap;
using CatMetro.Content;

namespace CatMetro.Tests.Engine
{
    // CM-C2b criterion 9: the REAL seams, in the engine host (this file lives under
    // EditMode/Engine/** — never linked into the dotnet leg, per ADR-0005's split).
    public sealed class BootstrapSeamTests
    {
        [Test]
        public void L001_LoadsThroughTheStreamingAssetsSeam()
        {
            var source = new StreamingAssetsContentSource();
            Assert.That(source.Exists("content/levels/L001.json"), Is.True);
            var bytes = source.ReadAsync("content/levels/L001.json", CancellationToken.None)
                .GetAwaiter().GetResult();
            var imported = LevelImporter.Import(bytes);
            Assert.That(imported.Ok, Is.True, $"L001 must import through the seam: {imported.Error}");
            Assert.That(imported.Value.Dto.Id, Is.EqualTo("L001"));
        }

        [Test]
        public void RuntimeBounds_ReadableThroughTheSeam_MatchesTheRepoRow()
        {
            var source = new StreamingAssetsContentSource();
            var bytes = source.ReadAsync("config/runtime_bounds.json", CancellationToken.None)
                .GetAwaiter().GetResult();
            var bounds = RuntimeBounds.Parse(bytes);
            Assert.That(bounds.Ok, Is.True);
            Assert.That(bounds.Value.SaveMaxBytes, Is.EqualTo(524288));
        }

        private static readonly string[] SaveFiles =
        {
            "save.dat", "save.dat.bak", "save.dat.tmp",
            "analytics_queue.dat", "analytics_queue.dat.bak", "analytics_queue.dat.tmp",
            "analytics_profile.dat", "analytics_profile.dat.bak", "analytics_profile.dat.tmp",
        };

        [Test]
        public void SaveStore_AndQueue_CommitAndReload_AgainstTheEngineStorageRoot()
        {
            var root = new EngineStorageRoot();
            Assert.That(root.SaveDirectory, Is.Not.Empty);
            Assert.That(root.CacheDirectory, Is.Not.Empty);
            var source = new StreamingAssetsContentSource();
            var boundsBytes = source.ReadAsync("config/runtime_bounds.json", CancellationToken.None)
                .GetAwaiter().GetResult();
            var bounds = RuntimeBounds.Parse(boundsBytes).Value;

            // Review F8: the contract wants the REAL root, so pre-existing developer files are
            // snapshotted and restored — never destroyed, never allowed to skew the test.
            var snapshot = new System.Collections.Generic.Dictionary<string, byte[]>();
            foreach (var f in SaveFiles)
            {
                var p = System.IO.Path.Combine(root.SaveDirectory, f);
                if (System.IO.File.Exists(p))
                {
                    snapshot[f] = System.IO.File.ReadAllBytes(p);
                    System.IO.File.Delete(p);
                }
            }
            try
            {
                var store = new SaveStore(root, new RealSaveFileSystem(), bounds,
                    MigrationTable.CreateDefault());
                store.Load();
                store.State.Tickets = 21;
                store.CommitAtomic();

                var reload = new SaveStore(root, new RealSaveFileSystem(), bounds,
                    MigrationTable.CreateDefault());
                reload.Load();
                Assert.That(reload.State.Tickets, Is.EqualTo(21));

                var queue = new AnalyticsQueue(root, new RealSaveFileSystem(), bounds, null);
                queue.Log(new CatMetro.Services.AnalyticsEvent("seam_probe",
                    new Newtonsoft.Json.Linq.JObject { ["ok"] = true }));
                Assert.That(queue.QueuedEventCount, Is.EqualTo(1));

                // Review F5: the RELOAD is the assertion — TryPersist swallows IO faults by
                // design, so only a second instance reading the DISK proves durability here.
                var rebornQueue = new AnalyticsQueue(root, new RealSaveFileSystem(), bounds, null);
                Assert.That(rebornQueue.QueuedEventCount, Is.EqualTo(1),
                    "the queued event survived through the engine storage root");
                Assert.That(rebornQueue.Snapshot()[0].Name, Is.EqualTo("seam_probe"));
            }
            finally
            {
                foreach (var f in SaveFiles)
                {
                    var p = System.IO.Path.Combine(root.SaveDirectory, f);
                    if (System.IO.File.Exists(p)) System.IO.File.Delete(p);
                    if (snapshot.TryGetValue(f, out var bytes))
                        System.IO.File.WriteAllBytes(p, bytes);
                }
            }
        }
    }
}
