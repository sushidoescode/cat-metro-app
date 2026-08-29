using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Application.Save;

namespace CatMetro.Tests.Save
{
    public sealed class DailyProgressTrackerTests
    {
        [Test]
        public void UnlockThreshold_IsConfigurable_AndCountsUniqueCampaignClears()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            var tracker = new DailyProgressTracker(store);

            Assert.That(tracker.IsDailyUnlocked(0), Is.True,
                "zero is the demo/video setting and must unlock without a code change");
            Assert.That(tracker.IsDailyUnlocked(7), Is.False);

            for (int i = 1; i <= 6; i++)
                tracker.RecordCampaignCompletion("L" + i.ToString("000"));
            tracker.RecordCampaignCompletion("L001");

            Assert.That(tracker.CampaignCompletions, Is.EqualTo(6),
                "replaying a cleared level does not advance the unlock gate");
            Assert.That(tracker.IsDailyUnlocked(7), Is.False);

            tracker.RecordCampaignCompletion("L007");
            Assert.That(tracker.CampaignCompletions, Is.EqualTo(7));
            Assert.That(tracker.IsDailyUnlocked(7), Is.True);
        }

        [Test]
        public void CampaignCompletions_PersistAcrossReload()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            var tracker = new DailyProgressTracker(store);
            tracker.RecordCampaignCompletion("L001");
            tracker.RecordCampaignCompletion("L002");

            var reloaded = SFixtures.Store(root);
            Assert.That(reloaded.Load(), Is.EqualTo(CatMetro.Services.LoadResult.Ok));
            Assert.That(new DailyProgressTracker(reloaded).CampaignCompletions, Is.EqualTo(2));
        }

        [Test]
        public void TrustedUtcHighWater_AcceptsForwardJump_ButRollbackBecomesPractice()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            var tracker = new DailyProgressTracker(store);

            var first = tracker.ObserveUtcDate("2026-08-26");
            var forward = tracker.ObserveUtcDate("2027-02-11");
            var rollback = tracker.ObserveUtcDate("2026-08-27");

            Assert.That(first.EffectiveDateKey, Is.EqualTo("2026-08-26"));
            Assert.That(first.CanCountCompletion, Is.True);
            Assert.That(forward.EffectiveDateKey, Is.EqualTo("2027-02-11"));
            Assert.That(forward.CanCountCompletion, Is.True,
                "offline clients intentionally accept forward dates: a clock jump is indistinguishable from a long absence");
            Assert.That((string)store.State.Payload["daily"]["trustedDateKey"],
                Is.EqualTo("2027-02-11"));

            Assert.That(rollback.RequestedDateKey, Is.EqualTo("2026-08-27"));
            Assert.That(rollback.EffectiveDateKey, Is.EqualTo("2027-02-11"),
                "rollback keeps the trusted puzzle playable instead of refusing entry");
            Assert.That(rollback.IsClockRollback, Is.True);
            Assert.That(rollback.IsPractice, Is.True);
            Assert.That(rollback.CanCountCompletion, Is.False);
        }

        [Test]
        public void LifetimeTally_CountsEachEligibleDateOnce_NeverUsesOrChangesStreak()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            store.State.Payload["daily"]["streakDays"] = 99;
            var tracker = new DailyProgressTracker(store);

            var dayOne = tracker.ObserveUtcDate("2026-08-26");
            var first = tracker.RecordDailyCompletion(dayOne);
            var replay = tracker.RecordDailyCompletion(dayOne);
            var rollback = tracker.ObserveUtcDate("2026-08-25");
            var rollbackWin = tracker.RecordDailyCompletion(rollback);

            Assert.That(first.Counted, Is.True);
            Assert.That(first.LifetimeCompletions, Is.EqualTo(1));
            Assert.That(replay.Counted, Is.False);
            Assert.That(rollbackWin.Counted, Is.False,
                "clock rollback is playable practice and can never increment the tally");
            Assert.That(tracker.LifetimeCompletions, Is.EqualTo(1));
            Assert.That((int)store.State.Payload["daily"]["streakDays"], Is.EqualTo(99),
                "the cumulative tally is explicitly not a consecutive-day streak");

            var completedKeys = ((JArray)store.State.Payload["daily"]["completedKeys"])
                .Select(t => (string)t).ToArray();
            Assert.That(completedKeys, Is.EqualTo(new[] { "2026-08-26" }));

            var reloaded = SFixtures.Store(root);
            Assert.That(reloaded.Load(), Is.EqualTo(CatMetro.Services.LoadResult.Ok));
            Assert.That(new DailyProgressTracker(reloaded).LifetimeCompletions, Is.EqualTo(1));
        }

        [Test]
        public void FailedCompletionCommit_RollsBackAndDegradesToUncountedWin()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs);
            store.Load();
            store.CommitAtomic();
            var tracker = new DailyProgressTracker(store);
            var selection = tracker.ObserveUtcDate("2026-08-26");
            int before = tracker.LifetimeCompletions;
            fs.FaultPoint = SFixtures.Fault.InReplace;

            DailyCompletionResult result = null;
            Assert.DoesNotThrow(() => result = tracker.RecordDailyCompletion(selection));

            Assert.That(result.Counted, Is.False);
            Assert.That(result.LifetimeCompletions, Is.EqualTo(before));
            Assert.That(tracker.LifetimeCompletions, Is.EqualTo(before),
                "no in-memory phantom tally without a durable write");
            Assert.That(store.ReportedEvents.Any(e => e.Name == "error_caught"
                && e.Detail.Contains("daily_save")), Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("2026-02-30")]
        [TestCase("26-08-2026")]
        public void ObserveUtcDate_RejectsAnythingExceptARealCanonicalDate(string dateKey)
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            var tracker = new DailyProgressTracker(store);

            Assert.Throws<System.ArgumentException>(() => tracker.ObserveUtcDate(dateKey));
        }

        [Test]
        public void UnlockThreshold_RejectsNegativeValues()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();

            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new DailyProgressTracker(store).IsDailyUnlocked(-1));
        }
    }
}
