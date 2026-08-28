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

        [Test]
        public void StartedRequest_RetainsTheExactBatchUntilServerAcknowledgement()
        {
            using var root = new SFixtures.TempRoot();
            var (q, transport, _) = QFixtures.Queue(root);
            transport.AutoComplete = false;
            q.Log(QFixtures.Ev("a"));
            q.Log(QFixtures.Ev("b"));

            q.OnTrigger("network_reachable");

            Assert.That(q.QueuedEventCount, Is.EqualTo(2),
                "starting an async request is not an ingestion acknowledgement");
            var reloadedBeforeAck = new AnalyticsQueue(root, new SFixtures.RecordingFs(),
                SFixtures.RepoBounds(), null);
            Assert.That(reloadedBeforeAck.QueuedEventCount, Is.EqualTo(2),
                "process death during the request must retain the disk queue");

            transport.Complete(0, accepted: false);
            Assert.That(q.QueuedEventCount, Is.EqualTo(2), "offline failure retains both records");

            q.OnTrigger("network_reachable");
            transport.Complete(1, accepted: true);
            Assert.That(q.QueuedEventCount, Is.Zero);
            var reloadedAfterAck = new AnalyticsQueue(root, new SFixtures.RecordingFs(),
                SFixtures.RepoBounds(), null);
            Assert.That(reloadedAfterAck.QueuedEventCount, Is.Zero,
                "only a server acknowledgement clears the durable artifact");
        }

        [Test]
        public void Acknowledgement_RemovesOnlyTheAttemptedPrefix_ThenPumpsTheNextBatch()
        {
            using var root = new SFixtures.TempRoot();
            var (q, transport, _) = QFixtures.Queue(root,
                QFixtures.SmallQueueBounds(maxEvents: 100, highWater: 64));
            transport.AutoComplete = false;
            transport.MaxBatchSize = 2;
            q.Log(QFixtures.Ev("a"));
            q.Log(QFixtures.Ev("b"));
            q.Log(QFixtures.Ev("c"));

            q.OnTrigger("network_reachable");
            Assert.That(transport.Batches.Single().Select(x => x.Name),
                Is.EqualTo(new[] { "a", "b" }));

            q.Log(QFixtures.Ev("d"));
            transport.Complete(0, accepted: true);

            Assert.That(transport.Batches.Count, Is.EqualTo(2));
            Assert.That(transport.Batches[1].Select(x => x.Name),
                Is.EqualTo(new[] { "c", "d" }));
            Assert.That(q.QueuedEventCount, Is.EqualTo(2));
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

            // Review S6: the structural half of the decidable negative — no member of ANY
            // visibility, of any kind, is timer- or clock-typed or timer-named. A future timer
            // cannot hide in a private field.
            const System.Reflection.BindingFlags all =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
            foreach (var member in typeof(AnalyticsQueue).GetMembers(all))
            {
                Assert.That(member.Name, Does.Not.Contain("Tick").And.Not.Contain("Timer")
                    .And.Not.Contain("Elapsed").And.Not.Contain("Poll").And.Not.Contain("Heartbeat"),
                    "timer-shaped member name: " + member.Name);
                var type = member is System.Reflection.FieldInfo fi ? fi.FieldType
                    : member is System.Reflection.PropertyInfo pi ? pi.PropertyType : null;
                if (type != null)
                    Assert.That(type.FullName, Does.Not.Contain("Timer").And.Not.Contain("Stopwatch"),
                        "time-typed member: " + member.Name + " : " + type.FullName);
            }
        }
    }
}
