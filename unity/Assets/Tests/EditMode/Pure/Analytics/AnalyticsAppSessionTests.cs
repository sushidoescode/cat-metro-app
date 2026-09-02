using System.Linq;
using NUnit.Framework;
using CatMetro.Application.Analytics;
using CatMetro.Application.Save;
using CatMetro.Tests.Save;

namespace CatMetro.Tests.Analytics
{
    public sealed class AnalyticsAppSessionTests
    {
        [Test]
        public void FreshInstall_EmitsFirstOpenThenAppOpen_AndPersistsSessionProfile()
        {
            using var root = new SFixtures.TempRoot();
            var save = SFixtures.Store(root);
            save.Load();
            var sink = new RecordingAnalytics();
            var clock = new MutableUnixClock { Seconds = 1_800_000_000L };
            var ids = new System.Collections.Generic.Queue<string>(new[] { "session-a" });
            var session = new AnalyticsAppSession(new SaveBackedAnalyticsProfileStore(save),
                sink, clock.NowSeconds, ids.Dequeue);

            session.Start("1.0.0", "mid", "35", "production");

            Assert.That(sink.Records.Select(x => x.Name),
                Is.EqualTo(new[] { "first_open", "app_open" }));
            Assert.That((string)sink.Records[1].Params["session_id"], Is.EqualTo("session-a"));
            Assert.That((int)sink.Records[1].Params["install_age_days"], Is.Zero);
            Assert.That((long)save.State.Payload["profile"]["createdAtUtc"],
                Is.EqualTo(1_800_000_000L));
            Assert.That((long)save.State.Payload["profile"]["lastSeenAtUtc"],
                Is.EqualTo(1_800_000_000L));
            Assert.That((int)save.State.Payload["profile"]["sessionCount"], Is.EqualTo(1));

            var reloaded = SFixtures.Store(root);
            reloaded.Load();
            Assert.That((int)reloaded.State.Payload["profile"]["sessionCount"], Is.EqualTo(1),
                "the profile mutation must reach the save artifact, not just memory");
        }

        [Test]
        public void ExistingInstall_DoesNotRepeatFirstOpen_AndReportsInstallAge()
        {
            using var root = new SFixtures.TempRoot();
            var save = SFixtures.Store(root);
            save.Load();
            save.State.Payload["profile"]["createdAtUtc"] = 1_700_000_000L;
            save.State.Payload["profile"]["lastSeenAtUtc"] = 1_700_000_100L;
            save.State.Payload["profile"]["sessionCount"] = 4;
            save.CommitAtomic();
            var sink = new RecordingAnalytics();
            var clock = new MutableUnixClock { Seconds = 1_700_172_800L };
            var session = new AnalyticsAppSession(new SaveBackedAnalyticsProfileStore(save),
                sink, clock.NowSeconds, () => "session-b");

            session.Start("1.0.0", "mid", "35", "production");

            Assert.That(sink.Records.Select(x => x.Name), Is.EqualTo(new[] { "app_open" }));
            Assert.That((int)sink.Records[0].Params["install_age_days"], Is.EqualTo(2));
            Assert.That((int)save.State.Payload["profile"]["sessionCount"], Is.EqualTo(5));
        }

        [Test]
        public void DurablyPreparedFirstLaunch_StillEmitsFirstOpenBeforeAppOpen()
        {
            using var root = new SFixtures.TempRoot();
            var save = SFixtures.Store(root);
            save.Load();
            save.State.Payload["profile"]["createdAtUtc"] = 1_800_000_000L;
            var sink = new RecordingAnalytics();
            var session = new AnalyticsAppSession(new SaveBackedAnalyticsProfileStore(save),
                sink, () => 1_800_000_000L, () => "session-prepared");

            session.Start("1.0.0", "mid", "35", "production",
                emitFirstOpen: true);

            Assert.That(sink.Records.Select(x => x.Name),
                Is.EqualTo(new[] { "first_open", "app_open" }));
        }

        [Test]
        public void ForegroundAtTwentyNineMinutes_StaysInTheSameSession()
        {
            using var root = new SFixtures.TempRoot();
            var save = SFixtures.Store(root);
            save.Load();
            var sink = new RecordingAnalytics();
            var clock = new MutableUnixClock { Seconds = 2_000_000_000L };
            var ids = new System.Collections.Generic.Queue<string>(new[] { "initial", "later" });
            var session = new AnalyticsAppSession(new SaveBackedAnalyticsProfileStore(save),
                sink, clock.NowSeconds, ids.Dequeue);
            session.Start("1.0.0", "mid", "35", "production");
            sink.Records.Clear();

            session.OnBackground();
            session.OnBackground();
            clock.Seconds += 29 * 60;
            session.OnForeground();
            session.OnForeground();

            Assert.That(sink.Records, Is.Empty);
            Assert.That((int)save.State.Payload["profile"]["sessionCount"], Is.EqualTo(1));
        }

        [Test]
        public void ForegroundAtThirtyMinutes_StartsExactlyOneNewSession()
        {
            using var root = new SFixtures.TempRoot();
            var save = SFixtures.Store(root);
            save.Load();
            var sink = new RecordingAnalytics();
            var clock = new MutableUnixClock { Seconds = 2_000_000_000L };
            var ids = new System.Collections.Generic.Queue<string>(new[] { "initial", "return" });
            var session = new AnalyticsAppSession(new SaveBackedAnalyticsProfileStore(save),
                sink, clock.NowSeconds, ids.Dequeue);
            session.Start("1.0.0", "mid", "35", "production");
            sink.Records.Clear();

            session.OnBackground();
            clock.Seconds += AnalyticsAppSession.SessionTimeoutSeconds;
            session.OnForeground();
            session.OnForeground();

            Assert.That(sink.Records.Select(x => x.Name), Is.EqualTo(new[] { "app_open" }));
            Assert.That((string)sink.Records[0].Params["session_id"], Is.EqualTo("return"));
            Assert.That((int)save.State.Payload["profile"]["sessionCount"], Is.EqualTo(2));
        }

        [Test]
        public void ForegroundOnANewUtcDate_RecordsADailyReturnEvenBeforeThirtyMinutes()
        {
            using var root = new SFixtures.TempRoot();
            var save = SFixtures.Store(root);
            save.Load();
            var sink = new RecordingAnalytics();
            var clock = new MutableUnixClock
            {
                Seconds = new System.DateTimeOffset(2026, 8, 26, 23, 50, 0,
                    System.TimeSpan.Zero).ToUnixTimeSeconds(),
            };
            var ids = new System.Collections.Generic.Queue<string>(new[] { "initial", "d1" });
            var session = new AnalyticsAppSession(new SaveBackedAnalyticsProfileStore(save),
                sink, clock.NowSeconds, ids.Dequeue);
            session.Start("1.0.0", "mid", "35", "production");
            sink.Records.Clear();

            session.OnBackground();
            clock.Seconds += 20 * 60;
            session.OnForeground();

            Assert.That(sink.Records.Select(x => x.Name), Is.EqualTo(new[] { "app_open" }));
            Assert.That((string)sink.Records[0].Params["session_id"], Is.EqualTo("d1"));
            Assert.That((int)save.State.Payload["profile"]["sessionCount"], Is.EqualTo(2));
        }
    }
}
