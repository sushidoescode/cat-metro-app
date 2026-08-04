using System.Linq;
using NUnit.Framework;
using CatMetro.Application.Analytics;
using CatMetro.Tests.Save;

namespace CatMetro.Tests.Analytics
{
    // CM-C8 criteria 3 + 8: no-loss / in-order / no-duplicate at exactly QUEUE_MAX_EVENTS (the
    // criterion instance) and at 500 (the smoke instance); ids survive restart and dedupe
    // retried flushes.
    public sealed class QueueOrderTests
    {
        private static void FillFlushAssert(int count)
        {
            using var root = new SFixtures.TempRoot();
            var (q, transport, _) = QFixtures.Queue(root);
            transport.Available = false; // transport unavailable while filling

            for (int i = 0; i < count; i++) q.Log(QFixtures.Ev("ev_" + i));
            Assert.That(q.QueuedEventCount, Is.EqualTo(count), "no loss below the cap");

            // reconnect: everything flushes in enqueue order with zero duplicate ids
            transport.Available = true;
            transport.Batches.Clear();
            q.OnTrigger("network_reachable");

            Assert.That(q.QueuedEventCount, Is.Zero, "acked batch cleared");
            var delivered = transport.Batches.SelectMany(b => b).ToList();
            Assert.That(delivered.Count, Is.EqualTo(count));
            Assert.That(delivered.Select(e => e.Name),
                Is.EqualTo(Enumerable.Range(0, count).Select(i => "ev_" + i)),
                "delivery preserves enqueue order");
            Assert.That(delivered.Select(e => e.Id).Distinct().Count(), Is.EqualTo(count),
                "zero duplicate ids across the full batch");
        }

        // Criterion 3, the criterion instance: EXACTLY QUEUE_MAX_EVENTS (read from the config,
        // not a literal — ADR-0006:222-227's smoke-vs-criterion pattern).
        [Test]
        public void AtExactlyQueueMaxEvents_NoLoss_InOrder_NoDuplicate() =>
            FillFlushAssert(SFixtures.RepoBounds().QueueMaxEvents);

        // Criterion 3, the smoke instance.
        [Test]
        public void At500_SmokeInstance() => FillFlushAssert(500);

        // Criterion 8: ids are generated at enqueue, persisted, and IDENTICAL after a simulated
        // process death — a retried flush re-hands the same ids.
        [Test]
        public void Ids_SurviveProcessRestart()
        {
            using var root = new SFixtures.TempRoot();
            var (q, transport, _) = QFixtures.Queue(root);
            transport.Available = false;
            q.Log(QFixtures.Ev("a"));
            q.Log(QFixtures.Ev("b"));
            var before = q.Snapshot().Select(e => e.Id).ToArray();

            var (reborn, _, _) = QFixtures.Queue(root); // fresh instance, same disk
            Assert.That(reborn.Snapshot().Select(e => e.Id).ToArray(), Is.EqualTo(before));
        }

        // Criterion 8: an unacked flush re-hands the SAME batch with the SAME ids — the
        // consumer dedupes by id instead of inflating counts.
        [Test]
        public void RetriedFlush_HandsTheSameIds()
        {
            using var root = new SFixtures.TempRoot();
            var (q, transport, _) = QFixtures.Queue(root);
            transport.Available = false;
            q.Log(QFixtures.Ev("a"));
            q.Log(QFixtures.Ev("b"));

            q.OnTrigger("app_pause");  // handed, no ack
            q.OnTrigger("app_pause");  // retried

            Assert.That(transport.Batches.Count, Is.EqualTo(2));
            Assert.That(transport.Batches[0].Select(e => e.Id),
                Is.EqualTo(transport.Batches[1].Select(e => e.Id)),
                "one delivery per id after downstream dedupe");
            Assert.That(q.QueuedEventCount, Is.EqualTo(2), "nothing cleared without an ack");
        }
    }
}
