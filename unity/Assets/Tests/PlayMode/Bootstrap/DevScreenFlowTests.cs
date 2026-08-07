using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Content;

namespace CatMetro.Tests.PlayMode
{
    // CM-UX-07 criterion 6: the dev-only screen flow behind GameRoot.BootToHome (S1/S9
    // resolution). Static-field hygiene: BootToHome is reset in SetUp AND TearDown so a failed
    // test never bleeds the dev flag into an unrelated fixture (the flag defaults false —
    // shipped boot, Q-5 honored).
    public sealed class DevScreenFlowTests
    {
        private GameRoot _root;

        [SetUp]
        public void SetUp()
        {
            GameRoot.BootToHome = false;
        }

        [TearDown]
        public void TearDown()
        {
            GameRoot.BootToHome = false;
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
        }

        private static ImportedLevel Fixture()
        {
            var r = LevelImporter.Import(Encoding.UTF8.GetBytes(FixtureJson()));
            Assert.That(r.Ok, Is.True, "fixture must import: " + r.Error);
            return r.Value;
        }

        private static GameObject FindByName(GameObject root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }

        // --- the flag OFF pin: zero screen objects constructed (shipped boot) ---

        [UnityTest]
        public IEnumerator FlagFalse_ZeroScreenObjectsConstructed_ShippedBootPin()
        {
            _root = GameRoot.LaunchWith(Fixture());
            yield return null;

            Assert.That(_root.Home, Is.Null);
            Assert.That(_root.Intro, Is.Null);
            Assert.That(_root.Stack, Is.Null);
            Assert.That(_root.ScreensVisible, Is.False);
            Assert.That(FindByName(_root.gameObject, "ScreensCanvas"), Is.Null,
                "no ScreensCanvas exists in shipped boot");
            Assert.That(
                _root.gameObject.GetComponentInChildren<CatMetro.Presentation.Screens.HomeScreenView>(true),
                Is.Null, "zero Home objects constructed");
            Assert.That(
                _root.gameObject.GetComponentInChildren<CatMetro.Presentation.Screens.LevelIntroSheet>(true),
                Is.Null, "zero Intro objects constructed");

            // board input is unaffected (positive control: the merged behavior still holds)
            var discPos = _root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0));
            Assert.That(_root.Input.HandleTapAtScreen(discPos), Is.EqualTo(0));
        }

        // --- the flag ON flow: Home -> pin tap -> Intro -> Play tap -> Playing, full round-trip ---

        [UnityTest]
        public IEnumerator
            BootToHome_ComposesHomeIntroFlow_PinTapToIntro_PlayTapToPlaying_StackRoundTrips()
        {
            GameRoot.BootToHome = true;
            _root = GameRoot.LaunchWith(Fixture());
            yield return null;

            Assert.That(_root.ScreensVisible, Is.True);
            Assert.That(_root.Home, Is.Not.Null);
            Assert.That(_root.Home.IsVisible, Is.True, "Home shown on boot");
            Assert.That(_root.Intro, Is.Not.Null);
            Assert.That(_root.Intro.IsVisible, Is.False, "Intro not shown yet");
            CollectionAssert.AreEqual(new[] { "home" }, _root.Stack.ToBreadcrumb());

            var canvasGo = FindByName(_root.gameObject, "ScreensCanvas");
            Assert.That(canvasGo, Is.Not.Null);
            var canvas = canvasGo.GetComponent<Canvas>();
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            Assert.That(canvas.sortingOrder, Is.EqualTo(120), "above the CM-UX-04 results canvas (110)");

            // board input is gated while Home is shown (criterion 2c re-confirmed through the
            // composed flow, not a test-side override)
            var discPos = _root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0));
            Assert.That(_root.Input.HandleTapAtScreen(discPos), Is.EqualTo(-1));

            // tap the pin: Intro shows with the substituted goal count from the loaded level
            int tapResult = _root.Input.HandleTapAtScreen(_root.Home.PinPaintedRectPx.center);
            Assert.That(tapResult, Is.EqualTo(-3), "the pin is a chrome region");
            Assert.That(_root.Intro.IsVisible, Is.True);
            Assert.That(_root.Intro.NameText, Is.EqualTo(_root.Session.Level.Dto.Name));
            Assert.That(_root.Intro.GoalText, Is.EqualTo(
                CatMetro.Presentation.Strings.UiStrings.Get("intro.goal")
                    .Replace("{count}", _root.Session.Level.Dto.Win.Deliveries.ToString())),
                "no new I/O — the count is substituted from the already-loaded level");
            CollectionAssert.AreEqual(new[] { "home", "intro" }, _root.Stack.ToBreadcrumb());
            Assert.That(_root.ScreensVisible, Is.True, "still up — the stack is non-empty");

            // tap Play: both hide, stack empties, board input returns
            int playResult = _root.Input.HandleTapAtScreen(_root.Intro.PlayChipRectPx.center);
            Assert.That(playResult, Is.EqualTo(-3), "the Play chip is a chrome region");
            Assert.That(_root.Intro.IsVisible, Is.False);
            Assert.That(_root.Home.IsVisible, Is.False);
            Assert.That(_root.ScreensVisible, Is.False, "the stack popped to empty");
            CollectionAssert.AreEqual(new string[0], _root.Stack.ToBreadcrumb());

            var discAgain = _root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0));
            Assert.That(_root.Input.HandleTapAtScreen(discAgain), Is.EqualTo(0),
                "input is live once the screens clear");
        }

        private static string FixtureJson()
        {
            return @"{
  ""schemaVersion"": 2, ""id"": ""T914"", ""name"": ""Dev Screen Flow Fixture"", ""seed"": 914,
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
  ""waves"": [ { ""tick"": 3999, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 1, ""spacingTicks"": 1 } ],
  ""win"": { ""deliveries"": 5, ""timeLimitTicks"": 4000, ""perfectMaxSwitches"": 1,
    ""stars"": { ""two"": 200, ""three"": 300 } },
  ""economy"": { ""baseTickets"": 20, ""perfectBonus"": 10 }
}";
        }
    }
}
