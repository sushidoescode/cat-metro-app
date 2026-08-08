using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Content;
using CatMetro.Presentation.Hud;

namespace CatMetro.Tests.PlayMode
{
    // CM-UX-07 criteria 1, 2, 3, 4, 7: the thin-wiring PR that turns the UX tranche ON.
    // Every test drives REAL composition through GameRoot.Wire/Retry (the live-wiring +
    // anti-vacuity rule inherited from CM-UX-01/02/05); no test hand-sets a delegate GameRoot
    // now binds itself. Static-field hygiene: GameRoot.BootToHome is reset in SetUp AND
    // TearDown so a failed test never bleeds the dev flag into an unrelated fixture.
    public sealed class GameRootWiringTests
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

        private static ImportedLevel Import(string json)
        {
            var r = LevelImporter.Import(Encoding.UTF8.GetBytes(json));
            Assert.That(r.Ok, Is.True, "fixture must import: " + r.Error);
            return r.Value;
        }

        private static ImportedLevel QuietFixture() => Import(QuietFixtureJson());
        private static ImportedLevel OverflowFixture() => Import(OverflowFixtureJson());
        private static ImportedLevel WinnableFixture() => Import(WinnableFixtureJson());

        private IEnumerator RunToFail(ImportedLevel level)
        {
            _root = GameRoot.LaunchWith(level);
            _root.MotionOffToggle = true;
            yield return null;
            Time.timeScale = 12f;
            float deadline = Time.realtimeSinceStartup + 40f;
            while (_root.ScreenState == "Playing" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Time.timeScale = 1f;
            Assert.That(_root.ScreenState, Is.EqualTo("FailureReview"));
        }

        // --- criterion 1: chrome attach ---

        [UnityTest]
        public IEnumerator Wire_AttachesScreenChromeAndHintChip_ToRootGameObject_BothScreenSpaceCamera()
        {
            _root = GameRoot.Launch();
            yield return null;

            var chrome = _root.GetComponent<ScreenChromeController>();
            var hint = _root.GetComponent<HintChipController>();
            Assert.That(chrome, Is.Not.Null, "Wire attaches ScreenChromeController to root.gameObject");
            Assert.That(hint, Is.Not.Null, "Wire attaches HintChipController to root.gameObject");
            Assert.That(chrome.ChromeRoot, Is.Not.Null);
            Assert.That(hint.ChipRoot, Is.Not.Null);

            var chromeCanvas = chrome.ChromeRoot.GetComponent<Canvas>();
            var hintCanvas = hint.ChipRoot.GetComponent<Canvas>();
            Assert.That(chromeCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera),
                "the M-4 regression pin: the self-resolve pattern finds the camera child");
            Assert.That(hintCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            Assert.That(chromeCanvas.sortingOrder, Is.EqualTo(100), "unchanged");
            Assert.That(hintCanvas.sortingOrder, Is.EqualTo(90), "unchanged");
        }

        // --- criterion 2: the board-input gate ---

        [UnityTest]
        public IEnumerator BoardInputGate_RealBoot_DiscTapResolves_WhilePlaying()
        {
            _root = GameRoot.LaunchWith(QuietFixture());
            yield return null;
            Assert.That(_root.ScreenState, Is.EqualTo("Playing"));
            Assert.That(_root.ScreensVisible, Is.False, "shipped boot: no dev screens mounted");

            var discPos = _root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0));
            Assert.That(_root.Input.HandleTapAtScreen(discPos), Is.EqualTo(0),
                "Wire's own BoardInputActive binding keeps board input live in shipped boot");
        }

        [UnityTest]
        public IEnumerator BoardInputGate_OffPlaying_DiscMisses_RetryBandStillWorks()
        {
            yield return RunToFail(OverflowFixture());

            var discPos = _root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0));
            // #44 review F-1 (D-2): the retry band claims FIRST on any tap with y below 25% of
            // Screen.height — asserted explicitly so a future layout change that drops the disc
            // into the band can't silently turn this into a band-vs-gate test instead.
            Assert.That(discPos.y, Is.GreaterThanOrEqualTo(Screen.height * 0.25f),
                "precondition: the disc sits above the retry band — otherwise this test proves "
                + "nothing about the board gate (the band would claim the tap first)");
            Assert.That(_root.Input.HandleTapAtScreen(discPos), Is.EqualTo(-1),
                "criterion 2: the gate closes off Playing — Wire's own binding, not a test override");

            // positive control: the retry band still works exactly here (the resolution-order law)
            var inBand = new Vector2(Screen.width * 0.5f, Screen.height * 0.10f);
            Assert.That(_root.Input.HandleTapAtScreen(inBand), Is.EqualTo(-2));
            Assert.That(_root.ScreenState, Is.EqualTo("Playing"));
        }

        [UnityTest]
        public IEnumerator BoardInputGate_HomeShownUnderDevFlag_DiscScanMisses()
        {
            GameRoot.BootToHome = true;
            _root = GameRoot.LaunchWith(QuietFixture());
            yield return null;

            Assert.That(_root.ScreenState, Is.EqualTo("Playing"), "the sim itself keeps running");
            Assert.That(_root.ScreensVisible, Is.True, "Home mounted — the stack is non-empty");

            var discPos = _root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0));
            // #44 review F-1 (D-2): if the disc drifted UNDER Home's pin, the tap would resolve
            // as -3 (a chrome region claiming it) rather than -1 (the gate closing it) — asserted
            // explicitly so that specific confusion can never hide behind a passing/failing -1.
            Assert.That(_root.Home.PinPaintedRectPx.Contains(discPos), Is.False,
                "precondition: the disc sits outside the Home pin's rect — otherwise this test "
                + "cannot tell the board gate apart from the pin's chrome region claiming the tap");
            Assert.That(_root.Input.HandleTapAtScreen(discPos), Is.EqualTo(-1),
                "the S2 conflict pin: Home showing gates the board even while Playing");
        }

        // --- criterion 3: MotionOffSource bound in Wire AND Retry ---

        private static Transform FindByName(GameObject root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static int Distinct(List<Vector3> values)
        {
            var seen = new HashSet<Vector3>();
            foreach (var v in values) seen.Add(v);
            return seen.Count;
        }

        [UnityTest]
        public IEnumerator MotionOffSource_WireBinds_BoardViewReadsItLive_AndRetryRebinds()
        {
            _root = GameRoot.LaunchWith(Import(OnboardingSingleSwitchJson()));
            yield return null;

            var disc = FindByName(_root.View.gameObject, "switch:S1");
            Assert.That(disc, Is.Not.Null);

            // motion ON (default): the pulse demonstrably oscillates — positive control
            var scales = new List<Vector3>();
            for (int i = 0; i < 20; i++) { scales.Add(disc.localScale); yield return null; }
            Assert.That(Distinct(scales), Is.GreaterThan(1),
                "positive control: the pulse animates while MotionOff is false");

            // no test-side MotionOffSource assignment — Wire alone must have bound it
            _root.MotionOffToggle = true;
            yield return null; // settle to the base scale
            scales.Clear();
            for (int i = 0; i < 10; i++) { scales.Add(disc.localScale); yield return null; }
            Assert.That(Distinct(scales), Is.EqualTo(1),
                "Wire's own binding makes BoardView read MotionOff without test-side wiring");

            _root.Retry();
            yield return null;
            var discAfterRetry = FindByName(_root.View.gameObject, "switch:S1");
            Assert.That(discAfterRetry, Is.Not.Null);
            scales.Clear();
            for (int i = 0; i < 10; i++) { scales.Add(discAfterRetry.localScale); yield return null; }
            Assert.That(Distinct(scales), Is.EqualTo(1),
                "Retry rebinds MotionOffSource on the REBUILT view — a Wire-only binding dies "
                + "at first retry (#36 F1/F5, the regression this criterion closes)");
        }

        // --- criterion 4: halt escape (Q-2) ---

        [UnityTest]
        public IEnumerator HaltEscape_RealHalt_TapAnywhereRetries_RegionUnregisters()
        {
            _root = GameRoot.Launch(); // L001 through the real seam — the F-DEV-4 fixture shape
            var chrome = _root.GetComponent<ScreenChromeController>();
            Assert.That(chrome, Is.Not.Null, "criterion 1 auto-attaches chrome");
            LogAssert.Expect(LogType.Error, new Regex("run halted at a pinned/guarded Domain boundary"));
            Time.timeScale = 8f;

            int baseline = _root.Input.Regions.Count;
            float deadline = Time.realtimeSinceStartup + 60f;
            while (_root.ScreenState != "Halted" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Time.timeScale = 1f;
            Assert.That(_root.ScreenState, Is.EqualTo("Halted"),
                "the recipe must reach the real halt within the deadline");
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(baseline + 1),
                "the halt escape region registers on the halt transition");

            yield return null; // within one pumped frame of the real transition
            Assert.That(chrome.Veil != null && chrome.Veil.IsVisible, Is.True,
                "the F-DEV-4 veil still renders — untouched by the escape (no CTA/veil edit)");

            // F3 fix (round-1 review): the old precondition compared the SAME literal rect to
            // itself (new Rect(0,0,Screen.width,Screen.height).Contains(center)) — zero
            // red-power, and a dead-center tap never exercises whether the escape is truly
            // full-screen. A plausible device-hardening inset (e.g. narrowing GameRoot.cs:283
            // to Rect(0, Screen.height*0.1f, Screen.width, Screen.height*0.8f)) would keep this
            // test green while soft-locking a player who taps the top/bottom 10%. Replaced with
            // a corner-coverage proof: an extreme corner tap must itself resolve to the escape.
            var corner = new Vector2(1f, 1f); // bottom-left extreme — the top-right sibling
                                               // corner is pinned by HaltEscape_ReHaltAfterEscape_
                                               // ReRegisters_NoDuplicateIdThrow below
            int result = _root.Input.HandleTapAtScreen(corner);
            Assert.That(result, Is.EqualTo(-3),
                "the escape resolves a tap at the extreme bottom-left corner — proving the "
                + "region is genuinely full-screen, not just center-covering (an inset would "
                + "miss here and this would read -1 instead)");
            Assert.That(_root.ScreenState, Is.EqualTo("Playing"), "Retry restores Playing");
            Assert.That(_root.Session.State.Tick, Is.Zero, "tick 0 (re-simulation, ADR-0002 §9)");

            yield return null;
            Assert.That(chrome.Veil == null || !chrome.Veil.IsVisible, Is.True,
                "the veil hides again once Playing resumes");
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(baseline),
                "region count is back to baseline — Retry unregistered the escape");
        }

        [UnityTest]
        public IEnumerator HaltEscapeRegion_AbsentDuringPlayingWonAndFailureReview()
        {
            // Playing
            _root = GameRoot.LaunchWith(QuietFixture());
            yield return null;
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(0), "Playing: no halt.escape region");

            // Won (the real sim outcome). CM-LOADNEXT declared amendment (DE-1): ResultsPanel
            // now attaches (Q-3 discharged), so Won DOES register a region now —
            // "results.next" (ChromeRegions.ModalPriority) — this test's own point stays
            // exactly what it was: "halt.escape" specifically never registers outside Halted.
            // Sharpened rather than weakened: Register("halt.escape", ...) succeeding without a
            // duplicate-id throw is itself live proof the id is not already claimed.
            Object.Destroy(_root.gameObject);
            _root = GameRoot.LaunchWith(WinnableFixture());
            yield return null;
            _root.Session.AdvanceMs(200 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return null;
            Assert.That(_root.ScreenState, Is.EqualTo("Won"));
            yield return null;
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(1),
                "Won: exactly the results panel's own region, never the halt escape");
            Assert.DoesNotThrow(() => _root.Input.Regions.Register(
                    "halt.escape", () => new Rect(0, 0, 1, 1), () => { }, 0),
                "halt.escape is not already registered during Won — a live duplicate-id proof");
            Assert.That(_root.Input.Regions.Unregister("halt.escape"), Is.True); // tidy the probe

            // FailureReview
            Object.Destroy(_root.gameObject);
            yield return RunToFail(OverflowFixture());
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(0), "FailureReview: no halt.escape region");

            // positive control (anti-vacuity): the counter observes THIS registry
            _root.Input.Regions.Register("decoy", () => new Rect(0, 0, 1, 1), () => { }, 0);
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(1));
            Assert.That(_root.Input.Regions.Unregister("decoy"), Is.True);
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator HaltEscape_ReHaltAfterEscape_ReRegisters_NoDuplicateIdThrow()
        {
            _root = GameRoot.Launch();
            // #44 review F-1 (D-2): "region counts assumed at a baseline" — captured explicitly
            // (never assumed to be a bare 0) so the later +1/back-to-baseline asserts stay
            // meaningful even if some other future wiring adds a region at boot.
            int baseline = _root.Input.Regions.Count;
            LogAssert.Expect(LogType.Error, new Regex("run halted at a pinned/guarded Domain boundary"));
            Time.timeScale = 8f;
            float deadline = Time.realtimeSinceStartup + 60f;
            while (_root.ScreenState != "Halted" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Time.timeScale = 1f;
            Assert.That(_root.ScreenState, Is.EqualTo("Halted"), "first halt reached");
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(baseline + 1),
                "precondition: the halt escape registered — otherwise the re-halt check below "
                + "proves nothing about re-registration");

            // F3 fix (round-1 review): sibling corner to HaltEscape_RealHalt_TapAnywhereRetries_
            // RegionUnregisters's bottom-left tap — together the two tests pin BOTH extreme
            // corners, proving the escape region is genuinely full-screen (a device-hardening
            // inset at GameRoot.cs:283 would miss here and this read would go -1, not -3).
            var corner = new Vector2(Screen.width - 1f, Screen.height - 1f); // top-right extreme
            Assert.That(_root.Input.HandleTapAtScreen(corner), Is.EqualTo(-3), "escape consumed");
            Assert.That(_root.ScreenState, Is.EqualTo("Playing"));

            // re-halt: L001 re-simulates identically from tick 0 (deterministic replay) and
            // hits the SAME pinned boundary again — Register must not throw a duplicate-id
            // ArgumentException (an unhandled exception would fail this test on its own).
            LogAssert.Expect(LogType.Error, new Regex("run halted at a pinned/guarded Domain boundary"));
            Time.timeScale = 8f;
            deadline = Time.realtimeSinceStartup + 60f;
            while (_root.ScreenState != "Halted" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Time.timeScale = 1f;
            Assert.That(_root.ScreenState, Is.EqualTo("Halted"), "L001 re-halts identically after retry");
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(baseline + 1),
                "the escape re-registered — against the SAME baseline, not just any positive count");
        }

        // --- criterion 7: Retry does NOT reset the hint attempt count (the CM-UX-05 law) ---

        [UnityTest]
        public IEnumerator Retry_DoesNotResetTheHintAttemptCount()
        {
            _root = GameRoot.LaunchWith(OverflowFixture());
            _root.MotionOffToggle = true;
            yield return null;
            var hint = _root.GetComponent<HintChipController>();
            Assert.That(hint, Is.Not.Null, "criterion 1 auto-attaches the hint controller");

            for (int cycle = 0; cycle < 2; cycle++)
            {
                Time.timeScale = 12f;
                float deadline = Time.realtimeSinceStartup + 40f;
                while (_root.ScreenState == "Playing" && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Time.timeScale = 1f;
                Assert.That(_root.ScreenState, Is.EqualTo("FailureReview"), "cycle " + cycle);
                _root.Retry();
                yield return null;
            }
            Assert.That(hint.AttemptCount, Is.EqualTo(2),
                "ResetForNewLevel() has no call site yet — Retry() never resets the count "
                + "(retry is the SAME level, the CM-UX-05 law)");
        }

        // --- F5 (PR #46 review): AddComponent guard against duplicate controllers ---

        [UnityTest]
        public IEnumerator PreAttachedControllers_SurviveWireAsExactlyOne_NoDuplicate()
        {
            // The DevLevelOverrideTests scene-boot pattern: a bare AddComponent<GameRoot>()
            // outside the factory paths triggers Awake -> InitializeFromSeam -> Wire through the
            // real seam. Attaching the controllers FIRST, then letting Wire run over them, proves
            // the guard survives a pre-existing instance instead of stacking a second one on top
            // (F5: GameRoot.cs's AddComponent calls were unguarded, unlike DevFrameCapture's).
            var go = new GameObject("GameRoot-f5-preattached");
            var preChrome = go.AddComponent<ScreenChromeController>();
            var preHint = go.AddComponent<HintChipController>();
            _root = go.AddComponent<GameRoot>();
            yield return null;

            Assert.That(_root.Session, Is.Not.Null,
                "precondition: Wire actually ran (booted through the real seam) — otherwise the "
                + "component counts below prove nothing about the guard");

            var chromes = go.GetComponents<ScreenChromeController>();
            var hints = go.GetComponents<HintChipController>();
            Assert.That(chromes.Length, Is.EqualTo(1),
                "GameRoot.Wire's AddComponent<ScreenChromeController> must guard a pre-existing "
                + "instance (F5), mirroring the DevFrameCapture guard");
            Assert.That(hints.Length, Is.EqualTo(1),
                "GameRoot.Wire's AddComponent<HintChipController> must guard a pre-existing "
                + "instance (F5), mirroring the DevFrameCapture guard");
            Assert.That(chromes[0], Is.SameAs(preChrome),
                "the guard keeps the pre-existing instance — it never destroys and replaces it");
            Assert.That(hints[0], Is.SameAs(preHint));
        }

        // --- CM-LOADNEXT criterion 1: the ResultsPanel attach line ---

        [UnityTest]
        public IEnumerator Wire_AttachesResultsPanel_ToRootGameObject_ScreenSpaceCamera()
        {
            _root = GameRoot.Launch();
            yield return null;

            var results = _root.GetComponent<ResultsPanel>();
            Assert.That(results, Is.Not.Null, "Wire attaches ResultsPanel to root.gameObject");
            Assert.That(results.PanelRoot, Is.Not.Null);
            var resultsCanvas = results.PanelRoot.GetComponent<Canvas>();
            Assert.That(resultsCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera),
                "the M-4-style regression pin: the self-resolve pattern finds the camera child");
            Assert.That(resultsCanvas.sortingOrder, Is.EqualTo(110), "unchanged (above chrome/hint)");
        }

        [UnityTest]
        public IEnumerator PreAttachedResultsPanel_SurvivesWireAsExactlyOne_NoDuplicate()
        {
            // The #46 F5 guard style, extended to ResultsPanel: a pre-existing instance (e.g.
            // the scene-boot path) survives Wire as the single instance instead of stacking a
            // second one on top.
            var go = new GameObject("GameRoot-loadnext-preattached");
            var preResults = go.AddComponent<ResultsPanel>();
            _root = go.AddComponent<GameRoot>();
            yield return null;

            Assert.That(_root.Session, Is.Not.Null,
                "precondition: Wire actually ran (booted through the real seam) — otherwise the "
                + "component counts below prove nothing about the guard");
            var panels = go.GetComponents<ResultsPanel>();
            Assert.That(panels.Length, Is.EqualTo(1),
                "GameRoot.Wire's AddComponent<ResultsPanel> must guard a pre-existing instance, "
                + "mirroring the DevFrameCapture/#46-F5 guard");
            Assert.That(panels[0], Is.SameAs(preResults),
                "the guard keeps the pre-existing instance — it never destroys and replaces it");
        }

        // --- CM-LOADNEXT criterion 6 (Q-5): shipped boot stays L001 ---

        [UnityTest]
        public IEnumerator Launch_ShippedBoot_StartsAtL001_RegardlessOfLoadNext()
        {
            _root = GameRoot.Launch();
            yield return null;
            Assert.That(_root.CurrentLevelId, Is.EqualTo("L001"),
                "Q-5 law: shipped boot stays L001 — LoadNext only ever runs post-win, never at "
                + "boot, even though the band-progression seam now exists");
        }

        // --- fixtures ---

        private static string QuietFixtureJson()
        {
            return @"{
  ""schemaVersion"": 2, ""id"": ""T910"", ""name"": ""Wiring Quiet Fixture"", ""seed"": 910,
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

        private static string OnboardingSingleSwitchJson()
        {
            return @"{
  ""schemaVersion"": 2, ""id"": ""T911"", ""name"": ""Wiring Teach Fixture"", ""seed"": 911,
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

        // The CM-C2b/C3 scripted-overflow fixture shape (FailureTests.cs): queueCapacity 4 with
        // a flood then sustain holds the source queue inside the digest envelope, fails ~t27.
        private static string OverflowFixtureJson()
        {
            return @"{
  ""schemaVersion"": 2, ""id"": ""T912"", ""name"": ""Wiring Overflow Fixture"", ""seed"": 912,
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
  ""schemaVersion"": 2, ""id"": ""T913"", ""name"": ""Wiring Winnable Fixture"", ""seed"": 913,
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
