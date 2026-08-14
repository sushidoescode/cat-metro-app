using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Content;
using CatMetro.Presentation.Hud;

namespace CatMetro.Tests.PlayMode
{
    // ART-EVIDENCE criteria 3/4: the #33 standing evidence-rig convention (ChromeStateTests,
    // ResultsPanelTests, TeachAffordanceTests, DioramaConstructionTests) — an env-var-armed
    // [UnityTest], Assert.Pass when disarmed so scripts/test.sh's total==passed gate never
    // breaks on an ordinary run. Set CM_ARTEV_CAPTURE_DIR to emit frames.
    //
    // Reads the actual composited screen back buffer (ScreenCapture, after WaitForEndOfFrame)
    // rather than a camera Render()-into-RenderTexture: the RT route only ever paints what one
    // camera draws, and a ScreenSpaceOverlay canvas composites straight onto the display,
    // outside any camera's render target — the PR #68 round-2 review's E-1 finding. This file
    // also writes one RT-route frame at the identical Playing/wave moment as a direct
    // side-by-side proof of that gap.
    public sealed class ArtEvidenceCaptureTests
    {
        // A fresh batch process's first uses of a given shader/material pair render one or a
        // few frames of a placeholder color while Unity compiles that variant asynchronously,
        // self-correcting once compilation catches up (unrelated to render mode or sortingOrder
        // — the same placeholder was isolated and reproduced on the very first captured frame
        // after a session boot, before the shared UI material's shader variants had ever been
        // used in this process, and never recurred on a second session booted later in the same
        // run). This many empty frames is a generous, cheap margin before the run's first
        // capture; only that first capture needs it.
        private const int WarmupFrames = 30;

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

        private static ImportedLevel Fixture(string json)
        {
            var r = LevelImporter.Import(Encoding.UTF8.GetBytes(json));
            Assert.That(r.Ok, Is.True, "fixture must import: " + r.Error);
            return r.Value;
        }

        [UnityTest]
        public IEnumerator CaptureEvidence_SixChromeFrames_WhenRequested()
        {
            var dir = System.Environment.GetEnvironmentVariable("CM_ARTEV_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir))
            {
                // Disarmed path PASSES explicitly (an Ignore would break the wrapper's
                // total==passed gate) — the CM-UX-02/03/04 rig precedent.
                Assert.Pass("capture rig disarmed — set CM_ARTEV_CAPTURE_DIR to emit frames");
                yield break;
            }
            Directory.CreateDirectory(dir);

            // The DioramaConstructionTests recipe, empirically proven in this environment: a
            // pre-selected 900x2000 Game View plus an in-test Screen.SetResolution together
            // reach a genuine 900x2000 portrait back buffer under -runTests.
            Screen.SetResolution(900, 2000, FullScreenMode.Windowed);
            yield return null;
            yield return null;

            GameRoot.BootToHome = true;
            _root = GameRoot.LaunchWith(Fixture(OverflowFixtureJson()));
            // WarmupFrames: this session's first frames can read a placeholder color for the
            // shared UI material's shader before this fresh batch process finishes compiling
            // that variant (see the WarmupFrames comment above) — a capture-timing artifact,
            // not a canvas/render-mode defect. A too-early read shows this placeholder; a
            // second GameRoot booted later in the same run (item 5 below) needs no warmup at
            // all because the variant is already compiled by then.
            for (int i = 0; i < WarmupFrames; i++) yield return null;

            // 1. Home
            Assert.That(_root.ScreensVisible, Is.True, "capture precondition: Home is up");
            Assert.That(_root.Home.IsVisible, Is.True);
            yield return CaptureBackBuffer(dir, "01-home.png");

            // 2. LevelIntro
            Assert.That(_root.Input.HandleTapAtScreen(_root.Home.PinPaintedRectPx.center),
                Is.EqualTo(-3), "capture precondition: the pin tap actually routed to Home");
            for (int i = 0; i < WarmupFrames; i++) yield return null;
            Assert.That(_root.Intro.IsVisible, Is.True, "capture precondition: Intro is up");
            yield return CaptureBackBuffer(dir, "02-levelintro.png");

            // 3. Playing/wave
            Assert.That(_root.Input.HandleTapAtScreen(_root.Intro.PlayChipRectPx.center),
                Is.EqualTo(-3), "capture precondition: the Play tap actually routed to Intro");
            yield return null;
            Assert.That(_root.ScreensVisible, Is.False, "capture precondition: screens cleared");
            Assert.That(_root.ScreenState, Is.EqualTo("Playing"));
            Assert.That(_root.Preview.VisibleChipCount, Is.GreaterThan(0),
                "capture precondition: a wave is genuinely pending for this frame");
            yield return CaptureBackBuffer(dir, "03-playing-wave.png");
            // The E-1 comparison pair: the SAME live moment through the camera RT route.
            CaptureCameraRenderTexture(_root.Cam, dir, "03-playing-wave-camera-rt-comparison.png");

            var hint = _root.GetComponent<HintChipController>();
            Assert.That(hint, Is.Not.Null);

            // 4a. First warning outcome — no hint yet (ChipVisibleAfterEntries = 2)
            Time.timeScale = 12f;
            float deadline1 = Time.realtimeSinceStartup + 40f;
            while (_root.ScreenState == "Playing" && Time.realtimeSinceStartup < deadline1)
                yield return null;
            Time.timeScale = 1f;
            Assert.That(_root.ScreenState, Is.EqualTo("FailureReview"),
                "capture precondition: the first failure was actually reached");
            yield return null;
            Assert.That(hint.AttemptCount, Is.EqualTo(1));
            Assert.That(hint.Chip == null || !hint.Chip.IsVisible, Is.True,
                "capture precondition: the hint has not appeared yet (1 entry < 2)");
            yield return CaptureBackBuffer(dir, "04a-fail-1st-no-hint.png");

            // 4b. Second warning outcome — hint now visible
            _root.Retry();
            yield return null;
            Assert.That(_root.ScreenState, Is.EqualTo("Playing"));

            Time.timeScale = 12f;
            float deadline2 = Time.realtimeSinceStartup + 40f;
            while (_root.ScreenState == "Playing" && Time.realtimeSinceStartup < deadline2)
                yield return null;
            Time.timeScale = 1f;
            Assert.That(_root.ScreenState, Is.EqualTo("FailureReview"),
                "capture precondition: the second failure was actually reached");
            yield return null;
            Assert.That(hint.AttemptCount, Is.EqualTo(2));
            Assert.That(hint.Chip != null && hint.Chip.IsVisible, Is.True,
                "capture precondition: the hint chip is now visible (2nd entry)");
            yield return CaptureBackBuffer(dir, "04b-fail-2nd-with-hint.png");

            // 5. Won/Results — a fresh, deliberately winnable session
            Object.Destroy(_root.gameObject);
            GameRoot.BootToHome = false;
            _root = GameRoot.LaunchWith(Fixture(WinnableFixtureJson()));
            var panel = _root.GetComponent<ResultsPanel>();
            Assert.That(panel, Is.Not.Null);
            _root.Session.AdvanceMs(200 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return null;
            yield return null;
            Assert.That(_root.ScreenState, Is.EqualTo("Won"),
                "capture precondition: the sim actually reached Won");
            Assert.That(panel.IsVisible, Is.True,
                "capture precondition: the results panel is actually showing");
            yield return CaptureBackBuffer(dir, "05-won-results.png");
        }

        private static IEnumerator CaptureBackBuffer(string dir, string fileName)
        {
            yield return new WaitForEndOfFrame();
            Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
            byte[] png = frame.EncodeToPNG();
            Object.Destroy(frame);
            File.WriteAllBytes(Path.Combine(dir, fileName), png);
            Debug.Log("ARTEV_FRAME_WRITTEN " + fileName + " bytes=" + png.Length);
        }

        private static void CaptureCameraRenderTexture(Camera cam, string dir, string fileName)
        {
            var rt = new RenderTexture(Screen.width, Screen.height, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            byte[] png = tex.EncodeToPNG();
            File.WriteAllBytes(Path.Combine(dir, fileName), png);
            Object.Destroy(tex);
            Object.Destroy(rt);
            Debug.Log("ARTEV_RT_COMPARISON_WRITTEN " + fileName + " bytes=" + png.Length);
        }

        private static string OverflowFixtureJson()
        {
            return @"{
  ""schemaVersion"": 2, ""id"": ""T960"", ""name"": ""ArtEvidence Overflow Fixture"", ""seed"": 960,
  ""meta"": { ""band"": ""onboarding"", ""difficultyTarget"": 0.1, ""mechanics"": [""switch"", ""queue""],
    ""newMechanic"": null, ""teachingGoal"": ""test fixture"", ""minActionWindowTicks"": 12,
    ""authoredBy"": ""llm+validator"" },
  ""board"": { ""nodes"": [
      { ""id"": ""SRC"", ""x"": 3, ""y"": 9, ""queueCapacity"": 4 },
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
  ""waves"": [
    { ""tick"": 8, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 6, ""spacingTicks"": 1 },
    { ""tick"": 8, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 6, ""spacingTicks"": 1 },
    { ""tick"": 14, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 8, ""spacingTicks"": 1 },
    { ""tick"": 22, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 4, ""spacingTicks"": 1 } ],
  ""win"": { ""deliveries"": 99, ""timeLimitTicks"": 4000, ""perfectMaxSwitches"": 1,
    ""stars"": { ""two"": 200, ""three"": 300 } },
  ""economy"": { ""baseTickets"": 20, ""perfectBonus"": 10 }
}";
        }

        private static string WinnableFixtureJson()
        {
            return @"{
  ""schemaVersion"": 2, ""id"": ""T961"", ""name"": ""ArtEvidence Winnable Fixture"", ""seed"": 961,
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
}
