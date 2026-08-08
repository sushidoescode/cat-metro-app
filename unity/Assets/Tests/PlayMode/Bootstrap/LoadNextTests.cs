using System.Collections;
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
    // CM-LOADNEXT: Won -> ResultsPanel's Next CTA -> the next band level loads and plays. Every
    // test drives REAL composition through GameRoot.Wire (the live-wiring + anti-vacuity rule
    // inherited from CM-UX-01/02/04/05/07); no test hand-wires a delegate GameRoot now binds
    // itself (GameRootWiringTests.cs's own established law, extended here to ResultsPanel).
    public sealed class LoadNextTests
    {
        private GameRoot _root;

        [TearDown]
        public void TearDown()
        {
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

        // --- criterion 3: the flagship end-to-end proof — tap Next, the next band level loads ---

        [UnityTest]
        public IEnumerator RealWin_TapNext_LoadsTheNextBandLevel_FreshAndPlaying()
        {
            _root = GameRoot.LaunchWith(Import(WinnableFixtureJson("L001")));
            yield return null;
            Assert.That(_root.CurrentLevelId, Is.EqualTo("L001"),
                "precondition: the fixture's id drives NextLevelId — otherwise progression "
                + "below proves nothing about the real band");

            _root.Session.AdvanceMs(200 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return null;
            Assert.That(_root.ScreenState, Is.EqualTo("Won"), "the real sim outcome");
            yield return null; // within one pumped frame, the panel is up

            var panel = _root.GetComponent<ResultsPanel>();
            Assert.That(panel, Is.Not.Null, "criterion 1 auto-attaches the panel");
            Assert.That(panel.IsVisible, Is.True,
                "precondition: the panel is showing — otherwise the tap below hits nothing");

            LogAssert.Expect(LogType.Log, new Regex(@"SEAM_LOADED content/levels/L002\.json"));
            int result = _root.Input.HandleTapAtScreen(panel.ChipPaintedRectPx.center);
            Assert.That(result, Is.EqualTo(-3), "the chrome region consumed the tap");

            Assert.That(_root.CurrentLevelId, Is.EqualTo("L002"),
                "Next progresses to the REAL next band level, read through the real seam — a "
                + "same-level bug would read L001 here");
            Assert.That(_root.ScreenState, Is.EqualTo("Playing"), "the new level starts Playing");
            Assert.That(_root.Session.State.Tick, Is.Zero, "a fresh session, tick 0");
            Assert.That(_root.Banner.Visible, Is.False, "the merged win banner clears on advance");

            yield return null; // the panel self-heals off ScreenState within one pumped frame
            Assert.That(panel.IsVisible, Is.False,
                "the panel hides once ScreenState leaves Won — LoadNext advances nothing else");
            Assert.That(_root.Input.Regions.TryResolve(panel.ChipPaintedRectPx.center, out _),
                Is.False, "results.next unregisters — no ghost region over the new level");
        }

        // --- PR #57 round-1 review F1: a same-frame double tap must not skip a level ---

        [UnityTest]
        public IEnumerator DoubleTap_SameFrame_NoYieldBetween_DoesNotSkipALevel()
        {
            // The flagship test above yields once (line ~81) BEFORE its own final region-gone
            // check — giving ResultsPanel's own Update()/Apply() poll a free frame to self-heal
            // and unregister naturally. That yield is exactly the gap this pin closes: NO yield
            // between the two taps below, so this exercises the SAME frame the first tap's
            // onTap() ran in, before any component's Update() runs again. Component Update
            // order across the GameObject is undefined, and LoadNext's synchronous, main-thread
            // ReadBlocking (StreamingAssetsContentSource) widens the real-world window further.
            _root = GameRoot.LaunchWith(Import(WinnableFixtureJson("L001")));
            yield return null;
            _root.Session.AdvanceMs(200 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return null;
            Assert.That(_root.ScreenState, Is.EqualTo("Won"));
            yield return null;

            var panel = _root.GetComponent<ResultsPanel>();
            Assert.That(panel.IsVisible, Is.True, "precondition: the panel is showing");
            var center = panel.ChipPaintedRectPx.center;

            int first = _root.Input.HandleTapAtScreen(center);
            Assert.That(first, Is.EqualTo(-3), "precondition: the first tap hit the region");
            Assert.That(_root.CurrentLevelId, Is.EqualTo("L002"),
                "precondition: the first tap advanced exactly once — otherwise the double-tap "
                + "assert below proves nothing about skipping");

            // NO yield here — the race window this pin exists to close.
            _root.Input.HandleTapAtScreen(center);

            Assert.That(_root.CurrentLevelId, Is.EqualTo("L002"),
                "a same-frame double tap must not skip a band level (must stay L002, never "
                + "L003) — the stale \"results.next\" region must not survive the level it just "
                + "advanced past (LoadLevel unregisters it proactively, not left to the panel's "
                + "own next Update() poll)");
        }

        // --- the WRAP assumption (A-LN-1), through the real seam ---

        [UnityTest]
        public IEnumerator RealWin_AtEndOfBand_WrapsToL001_ThroughTheRealSeam()
        {
            _root = GameRoot.LaunchWith(Import(WinnableFixtureJson("L005")));
            yield return null;
            _root.Session.AdvanceMs(200 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return null;
            Assert.That(_root.ScreenState, Is.EqualTo("Won"));
            yield return null;

            var panel = _root.GetComponent<ResultsPanel>();
            Assert.That(panel.IsVisible, Is.True, "precondition: the panel is showing");

            LogAssert.Expect(LogType.Log, new Regex(@"SEAM_LOADED content/levels/L001\.json"));
            _root.Input.HandleTapAtScreen(panel.ChipPaintedRectPx.center);

            Assert.That(_root.CurrentLevelId, Is.EqualTo("L001"),
                "ASSUMPTION under human review (state/handoffs/CM-LOADNEXT-frozen-contract.md, "
                + "A-LN-1): end-of-band WRAPS, a demo-friendly infinite loop, not a dead end");
        }

        // --- CM-UX-05 forward obligation: LoadNext resets the hint attempt count ---

        [UnityTest]
        public IEnumerator LoadNext_ResetsTheHintAttemptCount_UnlikeRetry()
        {
            _root = GameRoot.LaunchWith(Import(OverflowFixtureJson("L001")));
            _root.MotionOffToggle = true;
            yield return null;
            var hint = _root.GetComponent<HintChipController>();
            Assert.That(hint, Is.Not.Null, "criterion 1 (CM-UX-07) auto-attaches the hint controller");

            Time.timeScale = 12f;
            float deadline = Time.realtimeSinceStartup + 40f;
            while (_root.ScreenState == "Playing" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Time.timeScale = 1f;
            Assert.That(_root.ScreenState, Is.EqualTo("FailureReview"), "precondition: reached FailureReview once");
            _root.Retry();
            yield return null;

            Time.timeScale = 12f;
            deadline = Time.realtimeSinceStartup + 40f;
            while (_root.ScreenState == "Playing" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Time.timeScale = 1f;
            Assert.That(_root.ScreenState, Is.EqualTo("FailureReview"), "precondition: failed a second time");
            Assert.That(hint.AttemptCount, Is.EqualTo(2),
                "precondition: two FailureReview entries accumulated across the retry — "
                + "otherwise the reset below proves nothing (Retry itself does not reset the "
                + "count — the existing GameRootWiringTests.Retry_DoesNotResetTheHintAttemptCount pin)");

            // Direct call: the seam a real Next tap invokes only from Won, but LoadNext itself
            // does not gate on ScreenState (A-LN-3, Retry's own pattern) — this isolates the
            // reset law from the tap-routing law already proven in RealWin_TapNext_... above.
            _root.LoadNext();
            yield return null;

            Assert.That(hint.AttemptCount, Is.EqualTo(0),
                "a NEW level resets the per-level hint attempt-run (state/handoffs/CM-UX-05.md's "
                + "forward obligation) — Retry() of the SAME level must not (proven above)");
        }

        // --- criterion 5: reachable-state pins the panel's presence creates ---

        [UnityTest]
        public IEnumerator DuringWon_TheThumbBandTapIsNext_NeverRetry_EvenThoughRetryTappedIsWired()
        {
            _root = GameRoot.LaunchWith(Import(WinnableFixtureJson("L001")));
            _root.Input.RetryTapped = () => Assert.Fail("Retry must never fire during Won");
            yield return null;
            _root.Session.AdvanceMs(200 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return null;
            Assert.That(_root.ScreenState, Is.EqualTo("Won"));
            yield return null;

            var panel = _root.GetComponent<ResultsPanel>();
            Assert.That(panel.IsVisible, Is.True, "precondition: the panel is showing");
            var point = new Vector2(Screen.width * 0.5f, Screen.height * 0.10f); // the legacy retry band's own geometry
            Assert.That(panel.ChipPaintedRectPx.Contains(point), Is.True,
                "precondition: this point sits inside the chip's own painted rect — otherwise "
                + "this test cannot tell 'Next claimed it' apart from 'nothing claimed it'");

            int result = _root.Input.HandleTapAtScreen(point);
            Assert.That(result, Is.EqualTo(-3),
                "the chrome region (Next) claims the tap, never the legacy retry verb — "
                + "RetryRegionActive is false outside FailureReview, unaffected by this contract");
        }

        [UnityTest]
        public IEnumerator Retry_CalledDirectly_DuringWon_RestartsTheSameLevel_PanelHides()
        {
            _root = GameRoot.LaunchWith(Import(WinnableFixtureJson("L001")));
            yield return null;
            _root.Session.AdvanceMs(200 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return null;
            Assert.That(_root.ScreenState, Is.EqualTo("Won"));
            yield return null;
            var panel = _root.GetComponent<ResultsPanel>();
            Assert.That(panel.IsVisible, Is.True, "precondition: Won shows the panel");

            // Not reachable via any wired UI (no CTA calls Retry() during Won — Next is the
            // only registered chip, and the legacy retry band is gated off by
            // RetryRegionActive) — pinned defensively anyway, since Retry() is a public method
            // with no ScreenState guard of its own (the same shape as LoadNext, A-LN-3).
            _root.Retry();
            yield return null;

            Assert.That(_root.CurrentLevelId, Is.EqualTo("L001"), "Retry restarts the SAME level");
            Assert.That(_root.ScreenState, Is.EqualTo("Playing"));
            Assert.That(_root.Session.State.Tick, Is.Zero);
            Assert.That(panel.IsVisible, Is.False,
                "the panel self-heals off Won the moment ScreenState leaves it — the panel's "
                + "own state map, untouched by CM-LOADNEXT");
        }

        [UnityTest]
        public IEnumerator LoadNext_CalledDirectly_DuringFailureReview_StillProgresses_GatingIsUiOnly()
        {
            // Next during FailureReview: unreachable via any wired UI (results.next only
            // registers on Won), but LoadNext() itself carries no ScreenState guard (A-LN-3,
            // the same permissive shape as Retry()) — gating lives entirely at the registration
            // layer (ResultsPanel.Apply()), not inside the action method. Pinned so that shape
            // is a stated law, not an accident.
            yield return RunToFail(Import(OverflowFixtureJson("L001")));
            Assert.That(_root.ScreenState, Is.EqualTo("FailureReview"), "precondition");

            LogAssert.Expect(LogType.Log, new Regex(@"SEAM_LOADED content/levels/L002\.json"));
            _root.LoadNext();
            yield return null;

            Assert.That(_root.CurrentLevelId, Is.EqualTo("L002"));
            Assert.That(_root.ScreenState, Is.EqualTo("Playing"));
        }

        // --- fixtures ---

        // Parameterized by id so the SAME shape can drive band logic through the real seam:
        // ONE red cat, wave tick 3, initial route E2 aims at the matching RED station,
        // win.deliveries 1 — the real sim reaches Won with zero taps (the CM-UX-04
        // WinnableFixtureJson precedent, A-LN-4).
        private static string WinnableFixtureJson(string id)
        {
            return WinnableFixtureTemplate.Replace("__ID__", id);
        }

        private const string WinnableFixtureTemplate = @"{
  ""schemaVersion"": 2, ""id"": ""__ID__"", ""name"": ""LoadNext Winnable Fixture"", ""seed"": 950,
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

        // The CM-C2b/C3 scripted-overflow fixture shape (FailureTests.cs): queueCapacity 4 with
        // a flood then sustain holds the source queue inside the digest envelope, fails ~t27.
        private static string OverflowFixtureJson(string id)
        {
            return OverflowFixtureTemplate.Replace("__ID__", id);
        }

        private const string OverflowFixtureTemplate = @"{
  ""schemaVersion"": 2, ""id"": ""__ID__"", ""name"": ""LoadNext Overflow Fixture"", ""seed"": 951,
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
}
