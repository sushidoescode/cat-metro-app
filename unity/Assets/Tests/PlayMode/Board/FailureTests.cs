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
    // CM-C3: fail/retry loop. Real Domain runs drive QueueOverflow and TimeOut; the
    // PlatformOverflow limb uses a TEST-ONLY constructed presentation outcome (Q-J: no Domain
    // run can reach it), asserting framing + string only — the [CI] grep bans any shipped
    // construction of that reason in the render trees.
    public sealed class FailureTests
    {
        private GameRoot _root;

        [TearDown]
        public void TearDown()
        {
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

        // criterion 1's third limb + criterion 10's platform string: TEST-ONLY constructed
        // outcome — the presentation surfaces driven directly; no shipped type carries it.
        [UnityTest]
        public IEnumerator PlatformOverflow_ConstructedOutcome_FramesAndRendersTheLockedString()
        {
            _root = GameRoot.LaunchWith(Overflow());
            yield return null;
            _root.CauseCam.FrameNode("BLU", _root.View.NodeWorldPos(3), motionOff: true);
            _root.Banner.ShowKeySubstituted("fail.platformoverflow", "{station}", "BLU");
            yield return null;

            Assert.That(_root.CauseCam.TargetNodeId, Is.EqualTo("BLU"));
            Assert.That(_root.Banner.CurrentText, Is.EqualTo("BLU platform overflowed"),
                "the LOCKED string with the station substituted");
            Assert.That(UiStrings.Get("fail.platformoverflow"),
                Is.EqualTo("{station} platform overflowed"));
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
        }

        // --- criteria 6/8/9: retry — one input from frame 1, no scene load, tick-0 state ---
        [UnityTest]
        public IEnumerator Retry_OneTapFromFrameOne_NoSceneLoad_RestoresTickZero()
        {
            yield return RunToFail(Overflow(), motionOff: true);
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
        }

        // --- criteria 2 + 7: the p95 budgets over 20 scripted failures/retries, measured from
        // the FrameLog's single monotonic clock (editor CI leg; device legs are HUMAN) ---
        [UnityTest]
        public IEnumerator Budgets_CauseVisible1500_RetryUnder1000_P95Over20()
        {
            _root = GameRoot.LaunchWith(Overflow());
            _root.MotionOffToggle = true;
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
            _root = GameRoot.Launch(); // L001: one authored wave
            yield return null;

            // 1: shows the next wave's colour+count (L001 has one wave: red x2)
            Assert.That(_root.Preview.ChipSummary, Is.EqualTo("red x2"));
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
