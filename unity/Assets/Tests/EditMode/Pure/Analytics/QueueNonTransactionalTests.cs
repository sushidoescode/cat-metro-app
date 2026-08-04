using System.IO;
using System.Linq;
using NUnit.Framework;
using CatMetro.Application.Analytics;
using CatMetro.Application.Save;
using CatMetro.Tests.Save;

namespace CatMetro.Tests.Analytics
{
    // CM-C8 criterion 11: the queue write and the save write are two separate atomic writes,
    // never combined, ordered save-commit -> enqueue -> flush — a crash in the gap loses the
    // EVENT, never the GRANT (ADR-0006:280; CM-R27.3).
    public sealed class QueueNonTransactionalTests
    {
        [Test]
        public void FaultBetweenGrantAndEnqueue_LosesTheEventNeverTheGrant()
        {
            using var root = new SFixtures.TempRoot();
            var (store, fs) = SFixtures.CommittedStore(root);
            var ledger = new ConsumableLedger(store, SFixtures.RepoBounds());
            var transport = new QFixtures.RecordingTransport();
            var q = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), transport);
            q.LoadFromDisk();

            // save.dat commit (the grant) succeeds...
            int granted = ledger.TryGrant("txn-nt", "rewind_pack_small", 3, 0L);
            Assert.That(granted, Is.EqualTo(3));

            // ...then the process dies before/while the event enqueue persists.
            fs.FaultPoint = SFixtures.Fault.InReplace;
            Assert.Throws<IOException>(() => q.Log(QFixtures.Ev("purchase_completed")));
            fs.FaultPoint = SFixtures.Fault.None;

            // Reboot: the grant is durable; the event is gone. Never the inverse.
            var reloadedStore = SFixtures.Store(root);
            reloadedStore.Load();
            Assert.That(reloadedStore.State.RewindBalance, Is.EqualTo(7 + 3),
                "the grant survived");
            var (rebornQueue, _, _) = QFixtures.Queue(root);
            Assert.That(rebornQueue.Snapshot().Any(e => e.Name == "purchase_completed"),
                Is.False, "the event was lost — by design");
        }

        [Test]
        public void TheTwoFiles_AreWrittenInSeparateOperations()
        {
            using var root = new SFixtures.TempRoot();
            var (store, fs) = SFixtures.CommittedStore(root);
            var ledger = new ConsumableLedger(store, SFixtures.RepoBounds());
            var q = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(),
                new QFixtures.RecordingTransport());
            q.LoadFromDisk();
            fs.Calls.Clear();

            ledger.TryGrant("txn-sep", "rewind_pack_small", 1, 0L);
            q.Log(QFixtures.Ev("purchase_completed"));

            Assert.That(fs.Calls, Is.EqualTo(new[]
            {
                "WriteTempDurable:save.dat.tmp",
                "Replace:save.dat",
                "WriteTempDurable:analytics_queue.dat.tmp",
                "Replace:analytics_queue.dat",
            }), "two separate atomic writes in the mandated order — never one operation");
        }
    }
}
