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

        // Id generation is injected so the persisted artifact, rather than a derivation guess,
        // proves that the enqueue-time value is retained.
        [Test]
        public void IdGeneration_PersistsTheExactEnqueueValue()
        {
            using var root = new SFixtures.TempRoot();
            var q = new AnalyticsQueue(root, new SFixtures.RecordingFs(),
                SFixtures.RepoBounds(), null, null, () => "7f36cdbd8178cbf3");
            var p = new Newtonsoft.Json.Linq.JObject { ["lvl"] = 7, ["mode"] = "classic" };
            q.Log(new CatMetro.Services.AnalyticsEvent("level_started", p));
            Assert.That(q.Snapshot()[0].Id, Is.EqualTo("7f36cdbd8178cbf3"));
        }

        [Test]
        public void AckedEmptyRestart_DoesNotReuseAnEarlierEventId()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var transport = new QFixtures.RecordingTransport();
            var first = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), transport,
                null, () => "1111111111111111");
            first.Log(QFixtures.Ev("same"));
            string oldId = first.Snapshot().Single().Id;
            first.OnTrigger("app_pause");

            var restarted = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), null,
                null, () => "2222222222222222");
            restarted.Log(QFixtures.Ev("same"));

            Assert.That(restarted.Snapshot().Single().Id, Is.Not.EqualTo(oldId));
        }

        // Review B3(b): an acked flush must persist the EMPTY queue — otherwise every launch
        // re-uploads the same delivered batch forever.
        [Test]
        public void AckedFlush_PersistsTheEmptyQueue()
        {
            using var root = new SFixtures.TempRoot();
            var (q, transport, _) = QFixtures.Queue(root);
            q.Log(QFixtures.Ev("a"));
            q.Log(QFixtures.Ev("b"));
            q.OnTrigger("app_pause"); // acked (transport.Available = true)
            Assert.That(q.QueuedEventCount, Is.Zero);

            var (reborn, rebornTransport, _) = QFixtures.Queue(root);
            Assert.That(reborn.QueuedEventCount, Is.Zero,
                "the empty state is DURABLE — nothing re-delivers after a restart");
            reborn.OnTrigger("network_reachable");
            Assert.That(rebornTransport.Batches, Is.Empty);
        }

        // Review B3(c): ordinals continue past the reloaded maximum — a reset would let a
        // repeated payload reproduce an old id and the consumer would DROP the newer event.
        [Test]
        public void Ordinals_ContinuePastTheReloadedMax()
        {
            using var root = new SFixtures.TempRoot();
            var (q, transport, _) = QFixtures.Queue(root);
            transport.Available = false;
            q.Log(QFixtures.Ev("a"));
            q.Log(QFixtures.Ev("b"));

            var (reborn, _, _) = QFixtures.Queue(root);
            reborn.Log(QFixtures.Ev("c"));

            var ords = reborn.Snapshot().Select(e => e.Ord).ToArray();
            Assert.That(ords, Is.EqualTo(new long[] { 0, 1, 2 }),
                "the third event takes ordinal 2, never a reused 0");
            Assert.That(reborn.Snapshot().Select(e => e.Id).Distinct().Count(), Is.EqualTo(3));
        }

        // Review B2: a caller reusing its params buffer must not falsify already-queued
        // records — the queue clones on the way in, so record 0 keeps the bytes its id was
        // derived from.
        [Test]
        public void ReusedCallerBuffer_CannotFalsifyQueuedRecords()
        {
            using var root = new SFixtures.TempRoot();
            var (q, _, _) = QFixtures.Queue(root);
            var buffer = new Newtonsoft.Json.Linq.JObject { ["lvl"] = 1 };
            q.Log(new CatMetro.Services.AnalyticsEvent("level_started", buffer));
            buffer["lvl"] = 2; // the caller reuses its object
            q.Log(new CatMetro.Services.AnalyticsEvent("level_completed", buffer));

            var (reborn, _, _) = QFixtures.Queue(root); // read back from DISK
            var records = reborn.Snapshot();
            Assert.That((int)records[0].Params["lvl"], Is.EqualTo(1),
                "record 0 keeps the payload its id was derived from");
            Assert.That((int)records[1].Params["lvl"], Is.EqualTo(2));
        }

        // Criterion 8: an unacked flush re-hands the SAME batch with the SAME ids — the
        // consumer dedupes by id instead of inflating counts.
        [Test]
        public void RetriedFlush_HandsTheSameIds()
        {
            using var root = new SFixtures.TempRoot();
            var (q, transport, _) = QFixtures.Queue(root);
            transport.ServerAccepted = false;
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
