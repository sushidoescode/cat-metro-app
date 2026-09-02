using System.Linq;
using NUnit.Framework;
using CatMetro.Application.Analytics;
using CatMetro.Tests.Save;

namespace CatMetro.Tests.Analytics
{
    public sealed class QueueCaptureAndDisableTests
    {
        [Test]
        public void CaptureTimestamp_SurvivesThePersistedQueueArtifact()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var clock = new MutableUnixClock { Seconds = 1_800_000_000L };
            var q = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), null,
                clock.NowMilliseconds);

            q.Log(QFixtures.Ev("captured"));
            clock.Seconds += 999;
            var reloaded = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), null,
                clock.NowMilliseconds);

            Assert.That(reloaded.Snapshot().Single().CapturedAtUnixMs,
                Is.EqualTo(1_800_000_000_000L));
        }

        [Test]
        public void QueueOwnerId_SurvivesWithTheSameProfileIdentity()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            const string owner = "00112233445566778899aabbccddeeff";
            var queue = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), null,
                ownerId: owner);
            queue.Log(QFixtures.Ev("owned"));

            var reloaded = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), null,
                ownerId: owner);

            Assert.That(reloaded.Snapshot().Select(x => x.Name), Is.EqualTo(new[] { "owned" }));
        }

        [Test]
        public void RestoredQueueWithDifferentProfileIdentity_IsDiscardedBeforeDelivery()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var queue = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), null,
                ownerId: "00112233445566778899aabbccddeeff");
            queue.Log(QFixtures.Ev("old-owner"));

            var reloaded = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), null,
                ownerId: "ffeeddccbbaa99887766554433221100");

            Assert.That(reloaded.QueuedEventCount, Is.Zero,
                "historical activity must not be re-attributed to a new install identity");
            Assert.That(reloaded.Notes.Single().Detail, Does.Contain("owner_mismatch"));
            var rawReload = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), null,
                ownerId: "00112233445566778899aabbccddeeff");
            Assert.That(rawReload.QueuedEventCount, Is.Zero,
                "owner mismatch must overwrite the artifact, not only clear memory");
        }

        [Test]
        public void DisableAndDiscard_RemovesPersistedEventsSoTheyCannotUploadLater()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var q = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), null);
            q.Log(QFixtures.Ev("before_kill_switch"));

            q.DisableAndDiscard("remote_kill_switch");

            Assert.That(q.QueuedEventCount, Is.Zero);
            Assert.That(q.Notes.Last().Name, Is.EqualTo("queue_dropped"));
            Assert.That(q.Notes.Last().Detail, Does.Contain("remote_kill_switch"));
            var reloaded = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), null);
            Assert.That(reloaded.QueuedEventCount, Is.Zero,
                "discard must change the disk artifact, not only the in-memory list");
        }

        [Test]
        public void DisabledQueue_IgnoresNewEvents()
        {
            using var root = new SFixtures.TempRoot();
            var q = new AnalyticsQueue(root, new SFixtures.RecordingFs(),
                SFixtures.RepoBounds(), null);
            q.DisableAndDiscard("remote_kill_switch");

            q.Log(QFixtures.Ev("after_kill_switch"));

            Assert.That(q.QueuedEventCount, Is.Zero);
        }

        [Test]
        public void LateAcknowledgementAfterDisableAndReenable_CannotClearNewRecords()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var transport = new QFixtures.RecordingTransport { AutoComplete = false };
            var q = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), transport);
            q.Log(QFixtures.Ev("old"));
            q.OnTrigger("network_reachable");

            q.DisableAndDiscard("remote_kill_switch");
            q.Enable();
            q.Log(QFixtures.Ev("new"));
            transport.Complete(0, accepted: true);

            Assert.That(q.Snapshot().Select(x => x.Name), Is.EqualTo(new[] { "new" }));
            var reloaded = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), null);
            Assert.That(reloaded.Snapshot().Select(x => x.Name), Is.EqualTo(new[] { "new" }));
        }
    }
}
