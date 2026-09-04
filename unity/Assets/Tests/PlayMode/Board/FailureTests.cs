using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Content;
using CatMetro.Presentation.Strings;

namespace CatMetro.Tests.PlayMode
{
    // CM-C3: fail/retry loop. Real Domain runs drive QueueOverflow and TimeOut. The focused
    // PlatformOverflow test constructs the published outcome solely to isolate framing + copy;
    // pure Domain coverage separately drives its live station-capacity producer.
    public sealed class FailureTests
    {
        private GameRoot _root;

        [SetUp]
        public void SetUp()
        {
            // CM-BOOT-HOME: this fixture drives L001 (and LaunchWith fixtures) to
            // FailureReview through the REAL Launch() seam in one test below — Home now
            // composes there by default (criterion 1), which would hold the sim at tick 0
            // (criterion 2) and gate board/tap input, defeating every RunToFail deadline loop.
            // Opt OUT via the dev skip-Home hatch (the inverted BootToHome successor) so this
            // file keeps booting straight to gameplay. LaunchWith-seam tests are unaffected
            // either way (LaunchWith never composes Home at all, CM-BOOT-HOME EDIT 2).
            GameRoot.DevSkipShippedHome = true;
        }

        [TearDown]
        public void TearDown()
        {
            GameRoot.DevSkipShippedHome = false; // hygiene: restore the true global default
            Time.timeScale = 1f;
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
        }

        private static ImportedLevel Overflow() =>
            Import(FixtureJson(queueCapacity: 4, floodWaves: true, timeLimit: 4000));
        private static ImportedLevel QuietTimeout() =>
            Import(FixtureJson(queueCapacity: 0, floodWaves: false, timeLimit: 20));

        private static ImportedLevel Import(string json)
        {
            var r = LevelImporter.Import(Encoding.UTF8.GetBytes(json));
            Assert.That(r.Ok, Is.True, $"fixture must import: {r.Error}");
            return r.Value;
        }

        private IEnumerator RunToFail(ImportedLevel level, bool motionOff)
        {
            _root = GameRoot.LaunchWith(level);
            _root.MotionOffToggle = motionOff;
            yield return null;
            Time.timeScale = 12f;
            float deadline = Time.realtimeSinceStartup + 40f;
            while (_root.ScreenState == "Playing" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Time.timeScale = 1f;
            Assert.That(_root.ScreenState, Is.EqualTo("FailureReview"));
        }

        // --- criterion 1: cause camera targets the failing node (both REAL reasons) ---
        [UnityTest]
        public IEnumerator CauseCamera_QueueOverflow_TargetsTheOverloadedNode()
        {
            yield return RunToFail(Overflow(), motionOff: true);
            Assert.That(_root.Session.State.Outcome.Reason,
                Is.EqualTo(CatMetro.Domain.FailReason.QueueOverflow));
            Assert.That(_root.CauseCam.TargetNodeId, Is.EqualTo("SRC"));
        }

        [UnityTest]
        public IEnumerator CauseCamera_TimeOut_TargetsTheQKRuleNode()
        {
            yield return RunToFail(QuietTimeout(), motionOff: true);
            Assert.That(_root.Session.State.Outcome.Reason,
                Is.EqualTo(CatMetro.Domain.FailReason.TimeOut));
            Assert.That(_root.CauseCam.TargetNodeId, Is.EqualTo("SRC"),
                "A-C3-2 (Q-K): largest queue, ties to the lowest node id");
        }

        // Criterion 1's third limb + criterion 10's platform string: this test-only wrapper
        // isolates the shipped reason-to-key mapping and camera substitution. It is not a
        // parallel production outcome type.
        private sealed class ConstructedPresentationOutcome
        {
            public CatMetro.Domain.FailReason Reason;
            public string CausalNodeId;
        }

        [UnityTest]
        public IEnumerator PlatformOverflow_ConstructedOutcome_DrivesTheShippedMapping()
        {
            _root = GameRoot.LaunchWith(Overflow());
            yield return null;
            var constructed = new ConstructedPresentationOutcome
            {
                Reason = CatMetro.Domain.FailReason.PlatformOverflow,
                CausalNodeId = "BLU",
            };
            var (key, token) = GameRoot.FailKey(constructed.Reason);
            Assert.That(key, Is.EqualTo("fail.platformoverflow"),
                "the shipped mapping routes the live reason to the platform string");
            Assert.That(token, Is.EqualTo("{station}"));
            _root.CauseCam.FrameNode(constructed.CausalNodeId,
                _root.View.NodeWorldPos(3), motionOff: true);
            _root.Banner.ShowKeySubstituted(key, token, constructed.CausalNodeId);
            yield return null;

            Assert.That(_root.CauseCam.TargetNodeId, Is.EqualTo("BLU"));
            Assert.That(_root.Banner.CurrentText, Is.EqualTo("BLU platform overflowed"),
                "the LOCKED string with the station substituted");
        }

        // --- criteria 3/5: motion-off is a one-frame cut + static ring; no information lost ---
        [UnityTest]
        public IEnumerator MotionOff_CutInOneFrame_StaticRing([Values(true, false)] bool viaScale)
        {
            var level = Overflow();
            _root = GameRoot.LaunchWith(level);
            if (viaScale) _root.AnimatorDurationScale = 0f; else _root.MotionOffToggle = true;
            yield return null;
            Time.timeScale = 12f;
            float deadline = Time.realtimeSinceStartup + 40f;
            while (_root.ScreenState == "Playing" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Time.timeScale = 1f;

            // the cut completed on the FAIL frame itself: framed already, ring static & visible
            Assert.That(_root.CauseCam.IsFramed, Is.True, "final transform in one frame");
            Assert.That(_root.CauseCam.RingVisible, Is.True);
            Assert.That(_root.CauseCam.RingAlpha, Is.GreaterThan(0f));
            // review B1: the ring is ON the causal node — not riding the camera
            var nodePos = _root.View.NodeWorldPos(0); // SRC raised the failure
            Assert.That(Vector2.Distance(
                new Vector2(_root.CauseCam.RingWorldPos.x, _root.CauseCam.RingWorldPos.y),
                new Vector2(nodePos.x, nodePos.y)), Is.LessThan(0.01f),
                "the blame ring sits on the causal node");
            // review S8: the cut's framing geometry, kept for parity with the pan leg
            Assert.That(new Vector2(_root.Cam.transform.position.x, _root.Cam.transform.position.y),
                Is.EqualTo(new Vector2(nodePos.x, nodePos.y)).Within(0.01f),
                "the camera centres the causal node");
            Assert.That(Object.FindObjectsByType<Animation>(FindObjectsSortMode.None).Length,
                Is.Zero, "zero animation clips playing");
        }

        // --- criterion 4: motion-on pans over more than one frame to the SAME framing ---
        [UnityTest]
        public IEnumerator MotionOn_PansAcrossFrames_SameInformation()
        {
            yield return RunToFail(Overflow(), motionOff: false);
            bool framedImmediately = _root.CauseCam.IsFramed;
            Assert.That(framedImmediately, Is.False, "motion-on interpolates (>1 frame)");
            Assert.That(_root.CauseCam.RingVisible, Is.True, "criterion 5: ring in BOTH states");
            Assert.That(_root.CauseCam.TargetNodeId, Is.EqualTo("SRC"), "same target");
            Assert.That(_root.Banner.Visible, Is.True, "same banner");

            float deadline = Time.realtimeSinceStartup + 10f;
            while (!_root.CauseCam.IsFramed && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(_root.CauseCam.IsFramed, Is.True, "the pan converges");
            // review S8: parity on GEOMETRY, not labels — the pan ends exactly where the cut
            // ends, and the ring sat on the node the whole time (review B1).
            var node = _root.View.NodeWorldPos(0);
            Assert.That(new Vector2(_root.Cam.transform.position.x, _root.Cam.transform.position.y),
                Is.EqualTo(new Vector2(node.x, node.y)).Within(0.01f));
            Assert.That(Vector2.Distance(
                new Vector2(_root.CauseCam.RingWorldPos.x, _root.CauseCam.RingWorldPos.y),
                new Vector2(node.x, node.y)), Is.LessThan(0.01f));
        }

        // --- criteria 6/8/9: retry — one input from frame 1, no scene load, tick-0 state ---
        [UnityTest]
        public IEnumerator Retry_OneTapFromFrameOne_NoSceneLoad_RestoresTickZero()
        {
            _root = GameRoot.LaunchWith(Overflow());
            _root.MotionOffToggle = true;
            yield return null;
            // review B2: contaminate the switch state BEFORE failing, so "restored to
            // initialRoute" is distinguishable from "never changed".
            _root.Input.HandleTapAtScreen(_root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0)));
            var restPose = _root.Cam.transform.position;
            Time.timeScale = 12f;
            float dl = Time.realtimeSinceStartup + 40f;
            while (_root.ScreenState == "Playing" && Time.realtimeSinceStartup < dl)
                yield return null;
            Time.timeScale = 1f;
            Assert.That(_root.ScreenState, Is.EqualTo("FailureReview"));
            Assert.That(_root.Cam.transform.position, Is.Not.EqualTo(restPose),
                "the cause cut actually moved the camera (the restore below is meaningful)");
            int scenesBefore = SceneManager.sceneCount;
            var handleBefore = SceneManager.GetActiveScene().handle;
            int loadEvents = 0;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> counter = (s, m) => loadEvents++;
            SceneManager.sceneLoaded += counter;

            // frame 1 of FailureReview: one tap on the thumb band IS the retry (criterion 6)
            int result = _root.Input.HandleTapAtScreen(new Vector2(Screen.width * 0.5f, Screen.height * 0.1f));
            Assert.That(result, Is.EqualTo(-2), "the retry verb consumed the tap");
            Assert.That(_root.ScreenState, Is.EqualTo("Playing"), "back in Playing");

            // criterion 9 (PlayMode half): tick-0 state restored
            Assert.That(_root.Session.State.Tick, Is.Zero);
            Assert.That(_root.Session.Log.Entries.Count, Is.Zero);
            for (int s = 0; s < _root.Session.State.SwitchRoutes.Length; s++)
                Assert.That(_root.Session.State.SwitchRoutes[s],
                    Is.EqualTo(_root.Session.Level.Graph.SwitchInitialRoute[s]));

            yield return null;
            // criterion 8: zero scene loads, same handle
            SceneManager.sceneLoaded -= counter;
            Assert.That(loadEvents, Is.Zero, "no scene load on retry");
            Assert.That(SceneManager.sceneCount, Is.EqualTo(scenesBefore));
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(handleBefore));
            // review B5: the retried run plays on the S-02 rest framing, not the fail framing
            Assert.That(_root.Cam.transform.position,
                Is.EqualTo(restPose).Within(0.01f),
                "retry restores the play camera");
            // review N1: the factory paths never double-wire — exactly one of everything
            Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length,
                Is.EqualTo(1), "one camera (the Awake/factory double-wire stays dead)");
        }

        // --- criteria 2 + 7: the p95 budgets over 20 scripted failures/retries, measured from
        // the FrameLog's single monotonic clock (editor CI leg; device legs are HUMAN) ---
        [UnityTest]
        public IEnumerator Budgets_CauseVisible1500_RetryUnder1000_P95Over20(
            [Values(true, false)] bool motionOff)
        {
            // review B3: BOTH legs (criterion 4 requires the budget to hold under motion-on;
            // the pan is duration-bounded at PAN_DURATION_MS so distance cannot scale it).
            _root = GameRoot.LaunchWith(Overflow());
            _root.MotionOffToggle = motionOff;
            yield return null;

            var causeMs = new List<long>();
            var retryMs = new List<long>();
            Time.timeScale = 12f;
            for (int i = 0; i < 20; i++)
            {
                float deadline = Time.realtimeSinceStartup + 40f;
                while (_root.ScreenState == "Playing" && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(_root.ScreenState, Is.EqualTo("FailureReview"), "iteration " + i);

                // cause-visible: fail-tick frame -> first frame framed AND banner rendered.
                // The cut framed+bannered within the SAME Update, so the next record bounds it.
                var recs = _root.Log.Records;
                long failFrameMs = recs[recs.Count - 1].MonotonicMs;
                float frameDl = Time.realtimeSinceStartup + 5f;
                while ((!_root.CauseCam.IsFramed || !_root.Banner.Visible)
                    && Time.realtimeSinceStartup < frameDl)
                    yield return null;
                long framedMs = _root.Log.Records[_root.Log.Records.Count - 1].MonotonicMs;
                Assert.That(_root.CauseCam.IsFramed && _root.Banner.Visible, Is.True);
                causeMs.Add(framedMs - failFrameMs);

                // retry: tap-down -> first frame in Playing
                long tapMs = _root.Log.Records[_root.Log.Records.Count - 1].MonotonicMs;
                _root.Input.HandleTapAtScreen(new Vector2(Screen.width * 0.5f, Screen.height * 0.1f));
                yield return null;
                var post = _root.Log.Records[_root.Log.Records.Count - 1];
                Assert.That(post.ScreenState, Is.EqualTo("Playing"));
                retryMs.Add(post.MonotonicMs - tapMs);
            }
            Time.timeScale = 1f;

            causeMs.Sort(); retryMs.Sort();
            long causeP95 = causeMs[(int)System.Math.Ceiling(0.95 * causeMs.Count) - 1];
            long retryP95 = retryMs[(int)System.Math.Ceiling(0.95 * retryMs.Count) - 1];
            TestContext.Out.WriteLine("CAUSE_MS_TABLE=" + string.Join(",", causeMs));
            TestContext.Out.WriteLine("RETRY_MS_TABLE=" + string.Join(",", retryMs));
            TestContext.Out.WriteLine("CAUSE_P95=" + causeP95 + " RETRY_P95=" + retryP95);
            Assert.That(causeP95, Is.LessThanOrEqualTo(1500), "criterion 2 editor leg");
            Assert.That(retryP95, Is.LessThan(1000), "criterion 7 editor leg");
        }

        // --- criterion 10: the remaining real-reason strings render with substitution ---
        [UnityTest]
        public IEnumerator FailStrings_RenderWithSubstitution()
        {
            yield return RunToFail(Overflow(), motionOff: true);
            Assert.That(_root.Banner.CurrentText, Is.EqualTo("Platform overflowed at SRC"));
            Assert.That(UiStrings.CameFromCsv("fail.queueoverflow",
                _root.Banner.CurrentText.Replace("SRC", "{node}")), Is.True);
            _root.Input.HandleTapAtScreen(new Vector2(Screen.width * 0.5f, Screen.height * 0.1f));

            Object.Destroy(_root.gameObject);
            yield return RunToFail(QuietTimeout(), motionOff: true);
            Assert.That(_root.Banner.CurrentText, Is.EqualTo("The last train left the depot"));
        }

        // --- criterion 11: the wave preview strip ---
        [UnityTest]
        public IEnumerator WavePreview_NextTwoWaves_TopBand_NonInteractive_Updates()
        {
            _root = GameRoot.Launch();
            yield return null;

            // 1: shows the next authored wave's colour+count
            var firstWave = _root.Session.Level.Dto.Waves.ToArray()[0];
            Assert.That(_root.Preview.ChipSummary,
                Is.EqualTo(firstWave.Color + " x" + firstWave.Count));
            Assert.That(_root.Preview.VisibleChipCount, Is.EqualTo(1));
            // 2: sits in the top 0-15% band
            Assert.That(_root.Preview.InTopBand(0), Is.True);
            // 3: zero interactive elements (no collider anywhere under the strip)
            Assert.That(_root.Preview.GetComponentsInChildren<Collider>(true).Length, Is.Zero);
            // 4: updates as waves are consumed — run past the wave's last emission
            _root.Input.HandleTapAtScreen(_root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0)));
            _root.Session.AdvanceMs(40 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return null;
            Assert.That(_root.Preview.VisibleChipCount, Is.Zero, "consumed waves leave the strip");

            // review S3: the TWO-chip headline proven on a multi-wave board
            Object.Destroy(_root.gameObject);
            _root = GameRoot.LaunchWith(Overflow());
            yield return null;
            Assert.That(_root.Preview.VisibleChipCount, Is.EqualTo(2),
                "the NEXT TWO waves render as chips");
            Assert.That(_root.Preview.ChipSummary, Is.EqualTo("red x6|red x6"));
            Assert.That(_root.Preview.InTopBand(0), Is.True);
            Assert.That(_root.Preview.InTopBand(1), Is.True);
        }

        private static string FixtureJson(int queueCapacity, bool floodWaves, int timeLimit)
        {
            string qc = queueCapacity > 0 ? @", ""queueCapacity"": " + queueCapacity : "";
            string waves = floodWaves
                ? @"[
    { ""tick"": 8, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 6, ""spacingTicks"": 1 },
    { ""tick"": 8, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 6, ""spacingTicks"": 1 },
    { ""tick"": 14, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 8, ""spacingTicks"": 1 },
    { ""tick"": 22, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 4, ""spacingTicks"": 1 } ]"
                : @"[ { ""tick"": 100, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 1, ""spacingTicks"": 1 } ]";
            return @"{
  ""schemaVersion"": 2, ""id"": ""T904"", ""name"": ""Fail Fixture"", ""seed"": 904,
  ""meta"": { ""band"": ""onboarding"", ""difficultyTarget"": 0.1, ""mechanics"": [""switch"", ""queue""],
    ""newMechanic"": null, ""teachingGoal"": ""test fixture"", ""minActionWindowTicks"": 12,
    ""authoredBy"": ""llm+validator"" },
  ""board"": { ""nodes"": [
      { ""id"": ""SRC"", ""x"": 3, ""y"": 9" + qc + @" },
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
  ""waves"": " + waves + @",
  ""win"": { ""deliveries"": 99, ""timeLimitTicks"": " + timeLimit + @", ""perfectMaxSwitches"": 1,
    ""stars"": { ""two"": 200, ""three"": 300 } },
  ""economy"": { ""baseTickets"": 20, ""perfectBonus"": 10 }
}";
        }
    }
}
