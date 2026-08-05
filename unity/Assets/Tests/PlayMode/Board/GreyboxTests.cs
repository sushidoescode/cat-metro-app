using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Content;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Strings;

namespace CatMetro.Tests.PlayMode
{
    // CM-C2b criteria 1-5: the greybox playable loop, driven through the REAL seam (criterion 1
    // loads L001 via StreamingAssetsContentSource inside GameRoot.Launch — not a shim).
    public sealed class GreyboxTests
    {
        private GameRoot _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
        }

        // --- criterion 1 ---
        [UnityTest]
        public IEnumerator RenderFidelity_OneViewObjectPerAuthoredElement()
        {
            _root = GameRoot.Launch();
            yield return null;

            var elements = Object.FindObjectsByType<BoardElementId>(FindObjectsSortMode.None)
                .Where(e => e.Kind != "train").ToList();
            var ids = elements.Select(e => e.Id).OrderBy(x => x).ToArray();
            Assert.That(ids, Is.EqualTo(new[] { "BLU", "E1", "E2", "E3", "J1", "RED", "S1", "SRC" }),
                "exactly one view object per authored element");
            Assert.That(elements.Count(e => e.Kind == "source"), Is.EqualTo(1));
            Assert.That(elements.Count(e => e.Kind == "station"), Is.EqualTo(2));
            Assert.That(elements.Count(e => e.Kind == "node"), Is.EqualTo(1));
            Assert.That(elements.Count(e => e.Kind == "edge"), Is.EqualTo(3));
            Assert.That(elements.Count(e => e.Kind == "switch"), Is.EqualTo(1));

            var dto = _root.Session.Level.Dto;
            foreach (var n in dto.Nodes.ToArray())
            {
                var view = elements.Single(e => e.Id == n.Id && e.Kind != "edge" && e.Kind != "switch");
                Assert.That(view.transform.position.x, Is.EqualTo(n.X).Within(0.001));
                Assert.That(view.transform.position.y, Is.EqualTo(n.Y).Within(0.001));
            }
        }

        // --- criterion 2 ---
        [UnityTest]
        public IEnumerator HitRects48dp_AndExactlyOneGestureHandler()
        {
            _root = GameRoot.Launch();
            yield return null;

            Assert.That(_root.Input.EffectiveHitDiameterDp, Is.GreaterThanOrEqualTo(48f),
                "CM-R20.1: every interactive element's effective hit rect >= 48dp");
            Assert.That(Object.FindObjectsByType<CatMetro.Presentation.Input.TapInput>(
                FindObjectsSortMode.None).Length, Is.EqualTo(1),
                "exactly ONE gesture handler in the Game scene (CM-R07.1)");
        }

        // --- criterion 3 ---
        [UnityTest]
        public IEnumerator Tap_CommitsLeverThisFrame_AppliesNextTick_TwoTapsInOrder()
        {
            _root = GameRoot.Launch();
            yield return null;

            int before = _root.View.CommittedRoute(0);
            int tapTick = _root.Session.State.Tick;
            Vector2 screen = _root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0));
            int hit = _root.Input.HandleTapAtScreen(screen);

            Assert.That(hit, Is.EqualTo(0), "the tap resolves to the switch");
            Assert.That(_root.View.CommittedRoute(0), Is.Not.EqualTo(before),
                "the lever shows the COMMITTED route before this frame renders (3a)");
            Assert.That(_root.Session.Log.Entries.Count, Is.EqualTo(1), "exactly one command (3b)");
            Assert.That(_root.Session.Log.Entries[0].Tick, Is.EqualTo(tapTick),
                "stamped with the enqueue tick — applied at the NEXT boundary by the CM-C1 law");
            byte routeAtTap = _root.Session.State.SwitchRoutes[0];

            // cross the next tick boundary: the sim applies it exactly there
            _root.Session.AdvanceMs(TickIfNeeded(tapTick));
            Assert.That(_root.Session.State.SwitchRoutes[0], Is.Not.EqualTo(routeAtTap),
                "applied at the tick boundary");

            // two taps inside one tick → two entries in receipt order
            int t2 = _root.Session.State.Tick;
            _root.Input.HandleTapAtScreen(screen);
            _root.Input.HandleTapAtScreen(screen);
            var lastTwo = _root.Session.Log.Entries.Skip(1).ToArray();
            Assert.That(lastTwo.Length, Is.EqualTo(2));
            Assert.That(lastTwo.All(e => e.Tick == t2), Is.True, "same tick, receipt order");
        }

        // Review round F1: the committed lever must NEVER revert — observe CommittedRoute across
        // EACH single-tick advance from stamp to application (the off-by-one made it flip back
        // for the full tick between stamp+1 and application).
        [UnityTest]
        public IEnumerator CommittedLever_NeverRevertsAcrossTickBoundaries()
        {
            _root = GameRoot.Launch();
            yield return null;

            int before = _root.View.CommittedRoute(0);
            _root.Input.HandleTapAtScreen(_root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0)));
            int committed = _root.View.CommittedRoute(0);
            Assert.That(committed, Is.Not.EqualTo(before));

            for (int i = 0; i < 4; i++)
            {
                _root.Session.AdvanceMs(CatMetro.Application.Session.TickInterpolator.TICK_MS);
                Assert.That(_root.View.CommittedRoute(0), Is.EqualTo(committed),
                    "the committed route holds at every single-tick boundary (advance " + i + ")");
            }
            Assert.That(_root.Session.State.SwitchRoutes[0], Is.EqualTo((byte)committed),
                "the sim converged to the committed route");
        }

        // Review round F6: the 48dp claim bound BEHAVIOURALLY — a tap 23dp off-center hits the
        // expanded rect, 25dp off-center misses (radius 24dp; headless dpi fallback 1px = 1dp).
        [UnityTest]
        public IEnumerator ExpandedHitRect_HitAt23dp_MissAt25dp()
        {
            _root = GameRoot.Launch();
            yield return null;

            float pxPerDp = Screen.dpi > 0f ? Screen.dpi / 160f : 1f;
            Vector2 center = _root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0));
            int entries = _root.Session.Log.Entries.Count;

            Assert.That(_root.Input.HandleTapAtScreen(center + new Vector2(23f * pxPerDp, 0f)),
                Is.EqualTo(0), "23dp off-center is inside the expanded rect");
            Assert.That(_root.Session.Log.Entries.Count, Is.EqualTo(entries + 1));
            Assert.That(_root.Input.HandleTapAtScreen(center + new Vector2(25f * pxPerDp, 0f)),
                Is.EqualTo(-1), "25dp off-center is outside");
            Assert.That(_root.Session.Log.Entries.Count, Is.EqualTo(entries + 1), "no phantom command");
        }

        // Review round F2: the zero-tap L001 path hits the pinned NEW-Q4 boundary at ~t30 —
        // the run must HALT loudly (no unhandled exception out of Update, no misleading
        // banner, board intact), never limp to a wrong TimeOut fail.
        [UnityTest]
        public IEnumerator NoTapL001_HaltsAtThePinnedBoundary_NoUnhandledThrow()
        {
            _root = GameRoot.Launch();
            yield return null;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "run halted at a pinned/guarded Domain boundary"));

            // REAL frames only — GameRoot.Update must be the code that meets the throw
            // (a direct AdvanceMs here would detonate in the test instead of the halt path).
            Time.timeScale = 8f;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (_root.ScreenState == "Playing" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Time.timeScale = 1f;

            Assert.That(_root.ScreenState, Is.EqualTo("Halted"));
            Assert.That(_root.Banner.Visible, Is.False, "no misleading outcome banner");
            Assert.That(Object.FindObjectsByType<BoardElementId>(FindObjectsSortMode.None).Length,
                Is.GreaterThan(0), "the board is still visible");
        }

        private static double TickIfNeeded(int tapTick)
        {
            // enough ms to cross at least two boundaries from wherever alpha sits
            return 2.5 * CatMetro.Application.Session.TickInterpolator.TICK_MS;
        }

        // --- criterion 3's frame log ---
        [UnityTest]
        public IEnumerator FrameLog_OneRecordPerFrame_FourFields_MonotonicClock()
        {
            _root = GameRoot.Launch();
            yield return null;
            int start = _root.Log.Records.Count;
            for (int i = 0; i < 5; i++) yield return null;

            var records = _root.Log.Records.Skip(start).ToList();
            Assert.That(records.Count, Is.EqualTo(5), "one record per rendered frame");
            long lastMs = long.MinValue;
            foreach (var r in records)
            {
                Assert.That(r.FrameIndex, Is.GreaterThan(0));
                Assert.That(r.MonotonicMs, Is.GreaterThanOrEqualTo(lastMs), "non-decreasing clock");
                Assert.That(r.SimTick, Is.GreaterThanOrEqualTo(0));
                Assert.That(r.ScreenState, Is.Not.Empty);
                lastMs = r.MonotonicMs;
            }
        }

        // --- criterion 4 ---
        [UnityTest]
        public IEnumerator Win_ByRoutingTheAuthoredCatCount_BannerFromCsv()
        {
            _root = GameRoot.Launch();
            yield return null;

            // the correct route: L001's one tap (initial route aims at the decoy)
            _root.Input.HandleTapAtScreen(_root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0)));
            _root.Session.AdvanceMs(200 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return null; // GameRoot.Update picks up the outcome and shows the banner

            Assert.That(_root.Session.State.Outcome.Kind, Is.EqualTo(CatMetro.Domain.OutcomeKind.Won));
            Assert.That(_root.Session.State.Deliveries,
                Is.EqualTo(_root.Session.Level.Dto.Win.Deliveries),
                "the asserted number is the level file's own win.deliveries (Q-E)");
            Assert.That(_root.Banner.Visible, Is.True);
            Assert.That(_root.Banner.CurrentText, Is.EqualTo("All cats home!"), "the LOCKED string");
            Assert.That(UiStrings.CameFromCsv("win.banner", _root.Banner.CurrentText), Is.True,
                "resolved through ui.csv, not a literal");
        }

        // --- criterion 5 ---
        [UnityTest]
        public IEnumerator Fail_ByQueueOverflow_BannerPresent_BoardVisible()
        {
            var imported = LevelImporter.Import(Encoding.UTF8.GetBytes(OverflowFixtureJson()));
            Assert.That(imported.Ok, Is.True, $"fixture must import: {imported.Error}");
            _root = GameRoot.LaunchWith(imported.Value);
            yield return null;

            // Envelope-safe overflow: capacity 4 with a 2/tick flood then a 1/tick sustain
            // holds the source queue at 4-7 across the whole 16-tick Overload window (intra-tick
            // peak 7 < the 8-slot digest envelope), failing ~t27. Cats route to the MATCHING
            // station (initialRoute 0 = RED, red cats) — zero NEW-Q4 pins on this path.
            _root.Session.AdvanceMs(40 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return null;

            Assert.That(_root.Session.State.Outcome.Kind,
                Is.EqualTo(CatMetro.Domain.OutcomeKind.Failed));
            Assert.That(_root.Session.State.Outcome.Reason,
                Is.EqualTo(CatMetro.Domain.FailReason.QueueOverflow));
            Assert.That(_root.Banner.Visible, Is.True, "the fail state renders a banner");
            Assert.That(Object.FindObjectsByType<BoardElementId>(FindObjectsSortMode.None).Length,
                Is.GreaterThan(0), "the board is still visible");
        }

        private static string OverflowFixtureJson()
        {
            // Envelope-safe overflow (the first cut tripped the CM-C1 qCap digest guard):
            // queueCapacity 4 with a 2/tick flood then a 1/tick sustain holds the source queue
            // at 4-7 for the whole 16-tick Overload window (peak 7 < the 8-slot envelope), so
            // Failed(QueueOverflow) lands ~t27. Cats route to the matching station (initialRoute
            // 0 = RED, red cats) — zero NEW-Q4 pins anywhere on this path.
            return @"{
  ""schemaVersion"": 2, ""id"": ""T901"", ""name"": ""Overflow Fixture"", ""seed"": 901,
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
}
