using System.Collections;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Application.Analytics;
using CatMetro.Bootstrap;
using CatMetro.Content;
using CatMetro.Services;
using Newtonsoft.Json.Linq;

namespace CatMetro.Tests.PlayMode
{
    public sealed class AnalyticsGameplayIntegrationTests
    {
        private GameRoot _root;
        private RecordingSink _sink;

        private sealed class RecordingSink : IAnalytics
        {
            public sealed class Record
            {
                public string Name;
                public JObject Params;
            }

            public readonly System.Collections.Generic.List<Record> Records =
                new System.Collections.Generic.List<Record>();
            public int QueuedEventCount => Records.Count;
            public void Log(in AnalyticsEvent e) => Records.Add(new Record
            {
                Name = e.Name,
                Params = e.Params == null ? new JObject() : (JObject)e.Params.DeepClone(),
            });
            public void SetUserProperty(UserPropertyKey key, string value) { }
        }

        private sealed class MemoryProfileStore : IAnalyticsProfileStore
        {
            public JObject Profile { get; } = new JObject
            {
                ["createdAtUtc"] = 0L,
                ["lastSeenAtUtc"] = 0L,
                ["sessionCount"] = 0,
            };
            public bool CommitDurable() => true;
            public void RequestCommit() { }
        }

        [SetUp]
        public void SetUp()
        {
            _sink = new RecordingSink();
            GameRoot.AnalyticsRuntimeFactory = () => new GameAnalyticsRuntime(_sink);
            GameRoot.DevSkipShippedHome = false;
            GameRoot.DailyEntryUnlocked = false;
        }

        [TearDown]
        public void TearDown()
        {
            GameRoot.AnalyticsRuntimeFactory = null;
            GameRoot.DevSkipShippedHome = false;
            GameRoot.DailyEntryUnlocked = false;
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
        }

        [UnityTest]
        public IEnumerator ShippedHome_DoesNotCountAStartUntilTheRealPlayTap()
        {
            _root = GameRoot.Launch();
            yield return null;
            Assert.That(_sink.Records, Is.Empty,
                "loading L001 behind Home is not a player start");

            _root.Input.HandleTapAtScreen(_root.Home.PinPaintedRectPx.center);
            Assert.That(_sink.Records, Is.Empty,
                "showing Intro is not a player start");

            _root.Input.HandleTapAtScreen(_root.Intro.PlayChipRectPx.center);

            Assert.That(_sink.Records.Select(x => x.Name),
                Is.EqualTo(new[] { "level_started" }));
            var e = _sink.Records.Single();
            Assert.That((string)e.Params["level_id"], Is.EqualTo("L001"));
            Assert.That((string)e.Params["mode"], Is.EqualTo("campaign"));
            Assert.That((int)e.Params["attempt"], Is.EqualTo(1));
            Assert.That((string)e.Params["from_screen"], Is.EqualTo("intro"));
        }

        [UnityTest]
        public IEnumerator FreshSession_OrdersInstallOpenAndTheRealFirstPlayTap()
        {
            var session = new AnalyticsAppSession(new MemoryProfileStore(), _sink,
                () => 1_800_000_000L, () => "00112233445566778899aabbccddeeff");
            session.Start("1.0", "mid", "35", "development");
            _root = GameRoot.Launch();
            yield return null;

            _root.Input.HandleTapAtScreen(_root.Home.PinPaintedRectPx.center);
            _root.Input.HandleTapAtScreen(_root.Intro.PlayChipRectPx.center);

            Assert.That(_sink.Records.Select(x => x.Name), Is.EqualTo(new[]
            {
                "first_open", "app_open", "level_started",
            }));
            Assert.That((string)_sink.Records[2].Params["level_id"], Is.EqualTo("L001"));
        }

        [UnityTest]
        public IEnumerator RealWinEdge_EmitsOneCompletion_ThenRealNextStartsL002()
        {
            var imported = LevelImporter.Import(Encoding.UTF8.GetBytes(WinnableFixture));
            Assert.That(imported.Ok, Is.True, imported.Error?.ToString());
            var runtime = new GameAnalyticsRuntime(_sink);
            _root = GameRoot.LaunchWith(imported.Value, runtime);
            yield return null;

            _root.Session.AdvanceMs(200 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return null;
            Assert.That(_root.ScreenState, Is.EqualTo("Won"));
            yield return null;

            Assert.That(_sink.Records.Count(x => x.Name == "level_completed"), Is.EqualTo(1));
            var completed = _sink.Records.Single(x => x.Name == "level_completed");
            Assert.That((string)completed.Params["level_id"], Is.EqualTo("L001"));
            Assert.That((int)completed.Params["score"],
                Is.EqualTo(_root.Session.State.Score));
            Assert.That((int)completed.Params["stars"], Is.EqualTo(1),
                "the current Domain score is factually zero; a win is one star");

            _root.LoadNext();

            var starts = _sink.Records.Where(x => x.Name == "level_started").ToArray();
            Assert.That(starts.Length, Is.EqualTo(2));
            Assert.That((string)starts[1].Params["level_id"], Is.EqualTo("L002"));
            Assert.That((int)starts[1].Params["attempt"], Is.EqualTo(1));
            Assert.That((string)starts[1].Params["from_screen"], Is.EqualTo("results"));

            _root.LoadNext();
            _root.LoadNext();
            _root.LoadNext();
            Assert.That(_sink.Records.Where(x => x.Name == "level_started")
                .Select(x => (string)x.Params["level_id"]),
                Is.EqualTo(new[] { "L001", "L002", "L003", "L004", "L005" }),
                "later level N is measured from the real loaded level id");
        }

        [UnityTest]
        public IEnumerator RealDailyAdmission_EmitsCanonicalDateAndDailyModeStartOnce()
        {
            GameRoot.DailyEntryUnlocked = true;
            _root = GameRoot.Launch();
            yield return null;
            _root.DailyClockUnixSeconds = () => 1_787_572_800L;

            _root.SelectDaily();

            Assert.That(_root.IsDailySession, Is.True,
                "the real daily pipeline must admit and load before analytics fires");
            Assert.That(_sink.Records.Select(x => x.Name),
                Is.EqualTo(new[] { "daily_started", "level_started" }));
            Assert.That((long)_sink.Records[0].Params["seed"], Is.EqualTo(1_449_106_418L));
            Assert.That((string)_sink.Records[0].Params["local_date"],
                Is.EqualTo("2026-08-24"));
            Assert.That((string)_sink.Records[1].Params["mode"], Is.EqualTo("daily"));

            _root.SelectDaily();

            Assert.That(_sink.Records.Count, Is.EqualTo(2),
                "the real same-session daily guard must not manufacture a second start");
        }

        private const string WinnableFixture = @"{
  ""schemaVersion"": 2, ""id"": ""L001"", ""name"": ""Analytics Winnable Fixture"", ""seed"": 950,
  ""meta"": { ""band"": ""onboarding"", ""difficultyTarget"": 0.1, ""mechanics"": [""switch""],
    ""newMechanic"": null, ""teachingGoal"": ""test fixture"", ""minActionWindowTicks"": 12,
    ""authoredBy"": ""llm+validator"" },
  ""board"": { ""nodes"": [
      { ""id"": ""SRC"", ""x"": 3, ""y"": 9 },
      { ""id"": ""J1"", ""x"": 3, ""y"": 6 },
      { ""id"": ""RED"", ""x"": 1, ""y"": 2 }, { ""id"": ""BLU"", ""x"": 5, ""y"": 2 } ],
    ""edges"": [
      { ""id"": ""E1"", ""from"": ""SRC"", ""to"": ""J1"", ""travelTicks"": 10 },
      { ""id"": ""E2"", ""from"": ""J1"", ""to"": ""RED"", ""travelTicks"": 12 },
      { ""id"": ""E3"", ""from"": ""J1"", ""to"": ""BLU"", ""travelTicks"": 12 } ] },
  ""sources"": [ { ""nodeId"": ""SRC"", ""allowedColors"": [""red""] } ],
  ""stations"": [
    { ""nodeId"": ""RED"", ""accepts"": [""red""], ""capacity"": 6 },
    { ""nodeId"": ""BLU"", ""accepts"": [""blue""], ""capacity"": 6 } ],
  ""switches"": [ { ""id"": ""S1"", ""nodeId"": ""J1"", ""routes"": [""E2"", ""E3""], ""initialRoute"": 0 } ],
  ""waves"": [ { ""tick"": 3, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 1, ""spacingTicks"": 1 } ],
  ""win"": { ""deliveries"": 1, ""timeLimitTicks"": 4000, ""perfectMaxSwitches"": 1,
    ""stars"": { ""two"": 200, ""three"": 300 } },
  ""economy"": { ""baseTickets"": 20, ""perfectBonus"": 10 }
}";
    }
}
