using System.Linq;
using NUnit.Framework;
using CatMetro.Application.Analytics;
using CatMetro.Tests.Save;

namespace CatMetro.Tests.Analytics
{
    // CM-C8 criteria 2, 4, 5: bounds come from the config file, overflow drops OLDEST-first on
    // both limbs with an exact visible count, oversize single events never enter.
    public sealed class QueueBoundsTests
    {
        // Criterion 2: construction reads the file's rows — the live values equal the shipped
        // config (the five literals appear in no source file; the wrapper greps that half).
        [Test]
        public void Bounds_AreReadFromTheConfigRows()
        {
            var b = SFixtures.RepoBounds();
            Assert.That(b.QueueMaxEvents, Is.EqualTo(2000));
            Assert.That(b.QueueMaxBytes, Is.EqualTo(1048576));
            Assert.That(b.QueueEventMaxBytes, Is.EqualTo(512));
            Assert.That(b.QueueFlushHighWater, Is.EqualTo(64));
            Assert.That(b.QueueFlushTrigger, Is.EqualTo(new[]
                { "network_reachable", "app_foreground", "app_pause", "high_water" }));
        }

        // Criterion 4, count limb: exceeding QUEUE_MAX_EVENTS drops oldest-first, survivors are
        // the newest N, the count is exact, one note per overflow event.
        [Test]
        public void Overflow_CountLimb_DropsOldestFirst_Visibly()
        {
            using var root = new SFixtures.TempRoot();
            var (q, _, _) = QFixtures.Queue(root, QFixtures.SmallQueueBounds(maxEvents: 3));
            for (int i = 1; i <= 5; i++) q.Log(QFixtures.Ev("e" + i));

            Assert.That(q.QueuedEventCount, Is.EqualTo(3));
            Assert.That(q.Snapshot().Select(e => e.Name), Is.EqualTo(new[] { "e3", "e4", "e5" }),
                "the survivors are the NEWEST N, in order");
            var drops = q.Notes.Where(n => n.Name == "queue_dropped").ToList();
            Assert.That(drops.Count, Is.EqualTo(2), "one note per overflow event");
            // Review S7: EXACT equality — "count=10".Contains("count=1") is true, so a
            // substring check could never prove exactness.
            Assert.That(drops.Select(d => d.Detail),
                Is.All.EqualTo("count=1 oldest-first overflow"));
        }

        // Criterion 4, byte limb (synthetic bounds — the shipped byte cap is a backstop that
        // the drift-(d) inequality makes unreachable at max event size).
        [Test]
        public void Overflow_ByteLimb_DropsOldestFirst_Visibly()
        {
            using var root = new SFixtures.TempRoot();
            var (q, _, _) = QFixtures.Queue(root,
                QFixtures.SmallQueueBounds(maxEvents: 100, maxBytes: 700, eventMaxBytes: 400));
            q.Log(QFixtures.Ev("big1", size: 200)); // ~260 B record
            q.Log(QFixtures.Ev("big2", size: 200));
            q.Log(QFixtures.Ev("big3", size: 200)); // pushes total past 700

            // Review S7: the contract's full byte-limb assertions — survivors are the newest N
            // in order, the dropped count is exact, one note per overflow event.
            Assert.That(q.Snapshot().Select(e => e.Name).ToArray(),
                Is.EqualTo(new[] { "big2", "big3" }), "the survivors are the NEWEST N, in order");
            var drops = q.Notes.Where(n => n.Name == "queue_dropped").ToList();
            Assert.That(drops.Count, Is.EqualTo(1), "one note per overflow event");
            Assert.That(drops[0].Detail, Is.EqualTo("count=1 oldest-first overflow"));
            Assert.That(q.Snapshot().Sum(e => e.Bytes), Is.LessThanOrEqualTo(700));
        }

        // Criterion 5: an event that cannot serialise under QUEUE_EVENT_MAX_BYTES is dropped,
        // not queued — length unchanged, counter incremented.
        [Test]
        public void OversizeSingleEvent_IsDroppedNotQueued()
        {
            using var root = new SFixtures.TempRoot();
            var (q, _, _) = QFixtures.Queue(root);
            q.Log(QFixtures.Ev("ok"));
            int before = q.QueuedEventCount;

            q.Log(QFixtures.Ev("oversize", size: 600)); // > 512 B record

            Assert.That(q.QueuedEventCount, Is.EqualTo(before), "never entered");
            Assert.That(q.Notes.Any(n =>
                n.Name == "queue_dropped" && n.Detail.Contains("oversize")), Is.True);
        }
    }
}
