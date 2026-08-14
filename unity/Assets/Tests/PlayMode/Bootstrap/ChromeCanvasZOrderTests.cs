using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Content;
using CatMetro.Presentation.Hud;

namespace CatMetro.Tests.PlayMode
{
    // ART-EVIDENCE criterion 2 (PR #68 round-2 review finding E-1). Before this pin, the wave
    // strip and the banner painted through Unity's ScreenSpaceOverlay compositing layer while
    // chrome/hint/results/the dev screens painted through a ScreenSpaceCamera layer instead.
    // Those two layers never compare by sortingOrder: an overlay canvas always composites above
    // a camera-attached one, whatever the numbers on either side say. So the authored ladder
    // (wave 80 / hint 90 / chrome-and-veil 100 / results 110 / banner 115 / dev screens 120)
    // was not the real paint order — a numerically LOWER wave chip could sit visually on top of
    // a numerically HIGHER halt veil. Canvas unification (GameRoot, HintChipController,
    // ScreenChromeController, ResultsPanel) puts every chrome surface on the same layer, so
    // sortingOrder alone decides paint order end to end, matching what the ladder always read.
    public sealed class ChromeCanvasZOrderTests
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
            Time.timeScale = 1f;
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

        // --- the halt-veil-vs-wave-strip case named in the contract ---

        [UnityTest]
        public IEnumerator Halted_WithPendingWave_VeilCanvas_SharesRenderMode_AndPaintsAbove_WaveCanvas()
        {
            _root = GameRoot.LaunchWith(Fixture());
            yield return null;

            // positive control: a wave is genuinely pending — otherwise the comparison below
            // proves nothing about the wave strip specifically.
            Assert.That(_root.Preview, Is.Not.Null);
            Assert.That(_root.Preview.VisibleChipCount, Is.GreaterThan(0),
                "precondition: the fixture's wave must still be pending");
            var waveCanvas = _root.Preview.GetComponentInChildren<Canvas>();
            Assert.That(waveCanvas, Is.Not.Null);

            // The ChromeStateTests.AttachControlled shape: rebind the Wire-attached instance's
            // own state source rather than construct a second controller (#50's Wire-time
            // guard already forbids stacking a duplicate). Canvas renderMode/sortingOrder are
            // fixed once at EnsureViews() and never vary with ScreenState, so this reaches the
            // exact same canvas the real exception-triggered halt would — only the state-entry
            // route differs, and only the route this fixture's wave (a tick-3999 wave that
            // never all-emits) is chosen to keep pending regardless of that route.
            var chrome = _root.GetComponent<ScreenChromeController>();
            Assert.That(chrome, Is.Not.Null);
            chrome.Attach(() => "Halted");
            yield return null;

            // positive control: the veil is genuinely showing — otherwise the comparison below
            // proves nothing about the halt veil specifically.
            Assert.That(chrome.Veil != null && chrome.Veil.IsVisible, Is.True,
                "precondition: the veil must actually be visible");
            var veilCanvas = chrome.ChromeRoot.GetComponent<Canvas>();
            Assert.That(veilCanvas, Is.Not.Null);

            Assert.That(veilCanvas.renderMode, Is.EqualTo(waveCanvas.renderMode),
                "the E-1 defect: two different render modes never compare by sortingOrder — a "
                + "fixed overlay layer always paints above a camera-attached one regardless of "
                + "the number on either side");
            Assert.That((int)veilCanvas.sortingOrder, Is.GreaterThan((int)waveCanvas.sortingOrder),
                "with a shared render mode, 100 > 80 is now the real, authoritative paint order "
                + "(veil=" + veilCanvas.sortingOrder + " wave=" + waveCanvas.sortingOrder + ")");
        }

        // --- ResultsPanel above chrome ---

        [UnityTest]
        public IEnumerator ResultsCanvas_SharesRenderMode_AndPaintsAbove_ChromeAndHint()
        {
            _root = GameRoot.Launch();
            yield return null;

            var chrome = _root.GetComponent<ScreenChromeController>();
            var hint = _root.GetComponent<HintChipController>();
            var results = _root.GetComponent<ResultsPanel>();
            Assert.That(chrome, Is.Not.Null);
            Assert.That(hint, Is.Not.Null);
            Assert.That(results, Is.Not.Null);

            var chromeCanvas = chrome.ChromeRoot.GetComponent<Canvas>();
            var hintCanvas = hint.ChipRoot.GetComponent<Canvas>();
            var resultsCanvas = results.PanelRoot.GetComponent<Canvas>();
            Assert.That(chromeCanvas, Is.Not.Null);
            Assert.That(hintCanvas, Is.Not.Null);
            Assert.That(resultsCanvas, Is.Not.Null);

            Assert.That(resultsCanvas.renderMode, Is.EqualTo(chromeCanvas.renderMode));
            Assert.That(resultsCanvas.renderMode, Is.EqualTo(hintCanvas.renderMode));
            Assert.That((int)resultsCanvas.sortingOrder, Is.GreaterThan((int)chromeCanvas.sortingOrder),
                "110 > 100 — Results paints above chrome");
            Assert.That((int)chromeCanvas.sortingOrder, Is.GreaterThan((int)hintCanvas.sortingOrder),
                "100 > 90 — chrome paints above the hint chip (A-UX5-3, unchanged)");
        }

        // --- the dev ScreensCanvas above the banner above ResultsPanel ---

        [UnityTest]
        public IEnumerator ScreensCanvas_SharesRenderMode_AndPaintsAbove_Banner_ThenResults()
        {
            GameRoot.BootToHome = true;
            _root = GameRoot.LaunchWith(Fixture());
            yield return null;

            var results = _root.GetComponent<ResultsPanel>();
            Assert.That(results, Is.Not.Null);
            var resultsCanvas = results.PanelRoot.GetComponent<Canvas>();
            Assert.That(resultsCanvas, Is.Not.Null);

            var bannerCanvas = _root.Banner.GetComponent<Canvas>();
            Assert.That(bannerCanvas, Is.Not.Null);

            var screensGo = FindByName(_root.gameObject, "ScreensCanvas");
            Assert.That(screensGo, Is.Not.Null, "precondition: the dev screen flow composed");
            var screensCanvas = screensGo.GetComponent<Canvas>();
            Assert.That(screensCanvas, Is.Not.Null);

            Assert.That(screensCanvas.renderMode, Is.EqualTo(resultsCanvas.renderMode));
            Assert.That(bannerCanvas.renderMode, Is.EqualTo(resultsCanvas.renderMode));
            Assert.That((int)screensCanvas.sortingOrder, Is.GreaterThan((int)bannerCanvas.sortingOrder),
                "120 > 115 — the dev screens paint above the banner");
            Assert.That((int)bannerCanvas.sortingOrder, Is.GreaterThan((int)resultsCanvas.sortingOrder),
                "115 > 110 — the banner sits between Results and the dev screens");
        }

        // A tick-3999 wave never fully emits inside any real test run — the wave strip stays
        // pending regardless of which state-entry route the halt veil case above takes.
        private static string FixtureJson()
        {
            return @"{
  ""schemaVersion"": 2, ""id"": ""T950"", ""name"": ""ZOrder Fixture"", ""seed"": 950,
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
  ""win"": { ""deliveries"": 99, ""timeLimitTicks"": 4000, ""perfectMaxSwitches"": 1,
    ""stars"": { ""two"": 200, ""three"": 300 } },
  ""economy"": { ""baseTickets"": 20, ""perfectBonus"": 10 }
}";
        }
    }
}
