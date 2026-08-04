using System.Linq;
using NUnit.Framework;
using CatMetro.Application.Analytics;
using CatMetro.Tests.Save;

namespace CatMetro.Tests.Analytics
{
    // CM-C8 criterion 6: flush fires on exactly the four triggers plus the high-water
    // threshold — and on NO timer (the negative is decidable because no time source exists).
    public sealed class QueueFlushTests
    {
        [TestCase("network_reachable")]
        [TestCase("app_foreground")]
        [TestCase("app_pause")]
        [TestCase("high_water")]
        public void EachConfiguredTrigger_Flushes(string trigger)
        {
            using var root = new SFixtures.TempRoot();
            var (q, transport, _) = QFixtures.Queue(root);
            q.Log(QFixtures.Ev("a"));
            transport.Batches.Clear();

            q.OnTrigger(trigger);

            Assert.That(transport.Batches.Count, Is.EqualTo(1), trigger + " must flush");
            Assert.That(q.QueuedEventCount, Is.Zero);
        }

        [Test]
        public void UnknownTrigger_DoesNotFlush()
        {
            using var root = new SFixtures.TempRoot();
            var (q, transport, _) = QFixtures.Queue(root);
            q.Log(QFixtures.Ev("a"));
            transport.Batches.Clear();

            q.OnTrigger("timer_elapsed"); // not in QUEUE_FLUSH_TRIGGER — and never will be

            Assert.That(transport.Batches, Is.Empty);
            Assert.That(q.QueuedEventCount, Is.EqualTo(1));
        }

        [Test]
        public void HighWaterThreshold_FlushesAutomatically()
        {
            using var root = new SFixtures.TempRoot();
            var (q, transport, _) = QFixtures.Queue(root,
                QFixtures.SmallQueueBounds(maxEvents: 100, highWater: 3));
            q.Log(QFixtures.Ev("a"));
            q.Log(QFixtures.Ev("b"));
            Assert.That(transport.Batches, Is.Empty, "below the threshold nothing fires");

            q.Log(QFixtures.Ev("c")); // count reaches QUEUE_FLUSH_HIGH_WATER

            Assert.That(transport.Batches.Count, Is.EqualTo(1));
            Assert.That(transport.Batches[0].Count, Is.EqualTo(3));
        }

        // The negative test CM-R43.4(c) demands: events sit below high-water with no trigger —
        // zero flushes ever, because no timer path exists in the type at all.
        [Test]
        public void NoTriggerNoTimer_NeverFlushes()
        {
            using var root = new SFixtures.TempRoot();
            var (q, transport, _) = QFixtures.Queue(root);
            for (int i = 0; i < 10; i++) q.Log(QFixtures.Ev("idle_" + i));

            Assert.That(transport.Batches, Is.Empty,
                "no elapsed-time path can flush — the trigger list is the whole surface");
            Assert.That(q.QueuedEventCount, Is.EqualTo(10));
            // and the type exposes no time-shaped member for a future timer to hide behind
            Assert.That(typeof(AnalyticsQueue).GetMethods()
                .Count(m => m.Name.Contains("Tick") || m.Name.Contains("Update")
                    || m.Name.Contains("Elapsed")), Is.Zero);
        }
    }
}
