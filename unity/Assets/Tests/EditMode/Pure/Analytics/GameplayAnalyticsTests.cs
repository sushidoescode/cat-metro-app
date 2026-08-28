using System.Linq;
using NUnit.Framework;
using CatMetro.Application.Analytics;

namespace CatMetro.Tests.Analytics
{
    public sealed class GameplayAnalyticsTests
    {
        [Test]
        public void CampaignStartThenRetry_UsesOneBasedAttemptsAndFactualLevelFields()
        {
            var sink = new RecordingAnalytics();
            var funnel = new GameplayAnalytics(sink);

            funnel.BeginCampaignLevel("L007", 1.25, retry: false, fromScreen: "intro");
            funnel.BeginCampaignLevel("L007", 1.25, retry: true,
                fromScreen: "failure_review");

            Assert.That(sink.Records.Select(x => x.Name),
                Is.EqualTo(new[] { "level_started", "level_started" }));
            Assert.That((int)sink.Records[0].Params["attempt"], Is.EqualTo(1));
            Assert.That((int)sink.Records[1].Params["attempt"], Is.EqualTo(2));
            Assert.That((string)sink.Records[1].Params["level_id"], Is.EqualTo("L007"));
            Assert.That((string)sink.Records[1].Params["mode"], Is.EqualTo("campaign"));
            Assert.That((string)sink.Records[1].Params["difficulty_target"], Is.EqualTo("1.25"));
            Assert.That((string)sink.Records[1].Params["from_screen"],
                Is.EqualTo("failure_review"));
        }

        [Test]
        public void NewCampaignLevel_ResetsAttemptToOne()
        {
            var sink = new RecordingAnalytics();
            var funnel = new GameplayAnalytics(sink);
            funnel.BeginCampaignLevel("L001", 0.5, retry: false, fromScreen: "intro");
            funnel.BeginCampaignLevel("L001", 0.5, retry: true,
                fromScreen: "failure_review");

            funnel.BeginCampaignLevel("L002", 0.75, retry: false, fromScreen: "results");

            Assert.That((int)sink.Records.Last().Params["attempt"], Is.EqualTo(1));
            Assert.That((string)sink.Records.Last().Params["level_id"], Is.EqualTo("L002"));
        }

        [Test]
        public void Win_EmitsOneCompletionUsingSimulationFactsAndAuthoredThresholds()
        {
            var sink = new RecordingAnalytics();
            var funnel = new GameplayAnalytics(sink);
            funnel.BeginCampaignLevel("L003", 0.9, retry: false, fromScreen: "intro");
            sink.Records.Clear();

            funnel.CompleteLevel("L003", tick: 81, switchesUsed: 2, rejections: 0,
                overloads: 0, score: 305, perfectMaxSwitches: 2,
                twoStarScore: 200, threeStarScore: 300);
            funnel.CompleteLevel("L003", tick: 90, switchesUsed: 3, rejections: 1,
                overloads: 0, score: 200, perfectMaxSwitches: 2,
                twoStarScore: 200, threeStarScore: 300);

            Assert.That(sink.Records.Count, Is.EqualTo(1), "the won edge is idempotent");
            var e = sink.Records[0];
            Assert.That(e.Name, Is.EqualTo("level_completed"));
            Assert.That((int)e.Params["duration_s"], Is.EqualTo(10),
                "81 fixed 125 ms ticks floor to 10 whole seconds");
            Assert.That((int)e.Params["switches_used"], Is.EqualTo(2));
            Assert.That((bool)e.Params["perfect"], Is.True);
            Assert.That((int)e.Params["score"], Is.EqualTo(305));
            Assert.That((int)e.Params["stars"], Is.EqualTo(3));
        }

        [TestCase(0, 1)]
        [TestCase(200, 2)]
        [TestCase(299, 2)]
        [TestCase(300, 3)]
        public void Win_MapsActualScoreToAuthoredStarThresholds(int score, int expectedStars)
        {
            var sink = new RecordingAnalytics();
            var funnel = new GameplayAnalytics(sink);
            funnel.BeginCampaignLevel("L001", 0.5, retry: false, fromScreen: "intro");
            sink.Records.Clear();

            funnel.CompleteLevel("L001", tick: 8, switchesUsed: 0, rejections: 0,
                overloads: 0, score: score, perfectMaxSwitches: 1,
                twoStarScore: 200, threeStarScore: 300);

            Assert.That((int)sink.Records.Single().Params["stars"], Is.EqualTo(expectedStars));
        }

        [TestCase(1, 0, 0, 1, true)]
        [TestCase(2, 0, 0, 1, false)]
        [TestCase(1, 1, 0, 1, false)]
        [TestCase(1, 0, 1, 1, false)]
        public void Win_PerfectRequiresAllThreeDefinedConditions(int switches, int rejections,
            int overloads, int cap, bool expected)
        {
            var sink = new RecordingAnalytics();
            var funnel = new GameplayAnalytics(sink);
            funnel.BeginCampaignLevel("L001", 0.5, retry: false, fromScreen: "intro");
            sink.Records.Clear();

            funnel.CompleteLevel("L001", tick: 8, switchesUsed: switches,
                rejections: rejections, overloads: overloads, score: 0,
                perfectMaxSwitches: cap, twoStarScore: 200, threeStarScore: 300);

            Assert.That((bool)sink.Records.Single().Params["perfect"], Is.EqualTo(expected));
        }

        [Test]
        public void DailyAdmission_EmitsDailyStartedThenDailyModeLevelStarted()
        {
            var sink = new RecordingAnalytics();
            var funnel = new GameplayAnalytics(sink);

            funnel.BeginDailyLevel("D-2026-08-26", 2.5, 123456789L, "2026-08-26");

            Assert.That(sink.Records.Select(x => x.Name),
                Is.EqualTo(new[] { "daily_started", "level_started" }));
            Assert.That((long)sink.Records[0].Params["seed"], Is.EqualTo(123456789L));
            Assert.That((string)sink.Records[0].Params["local_date"], Is.EqualTo("2026-08-26"));
            Assert.That((string)sink.Records[1].Params["mode"], Is.EqualTo("daily"));
            Assert.That((int)sink.Records[1].Params["attempt"], Is.EqualTo(1));
        }

        [Test]
        public void CompletionWithoutARealStart_EmitsNothing()
        {
            var sink = new RecordingAnalytics();
            var funnel = new GameplayAnalytics(sink);

            funnel.CompleteLevel("L001", tick: 8, switchesUsed: 0, rejections: 0,
                overloads: 0, score: 0, perfectMaxSwitches: 1,
                twoStarScore: 200, threeStarScore: 300);

            Assert.That(sink.Records, Is.Empty);
        }
    }
}
