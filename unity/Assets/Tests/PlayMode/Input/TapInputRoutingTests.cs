using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Content;
using CatMetro.Presentation.Fx;
using UnityEngine.UI;

namespace CatMetro.Tests.PlayMode
{
    // CM-UX-01 criteria 1-3 under the contract's live-wiring rule: every routing assertion
    // drives a TapInput wired by GameRoot.LaunchWith to a real session/view/camera — never a
    // bare component (TapInput.cs:52's null-guard returns -1 for every input on one of those,
    // which made naive negative asserts vacuously green, review R1-F1). Every negative assert
    // here carries a positive control in the same fixture.
    public sealed class TapInputRoutingTests
    {
        private GameRoot _root;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
        }

        [UnityTest]
        public IEnumerator AcceptedChromeTap_PressesItsResolvedVisual_WithoutMovingTheBoard()
        {
            Time.timeScale = 0f;
            _root = GameRoot.LaunchWith(Fixture());
            yield return null;
            var button = new GameObject("Test chip", typeof(RectTransform), typeof(Image));
            button.transform.SetParent(_root.transform, false);
            var rect = (RectTransform)button.transform;
            var face = button.GetComponent<Image>();
            int actions = 0;
            _root.Input.Regions.Register("press-test", () => new Rect(0f, 0f, 100f, 100f), () => actions++, 99);
            _root.Input.Regions.BindVisual("press-test", rect, face);
            var fx = BoardFx.GetOrCreate(_root.transform, () => _root.MotionOff);
            int commands = _root.Session.Log.Entries.Count;
            Assert.That(_root.Input.HandleTapAtScreen(new Vector2(50f, 50f)), Is.EqualTo(-3));
            Assert.That(actions, Is.Zero, "the chip stays on screen for its visible press");
            Assert.That(face.color.r, Is.EqualTo(0.9f).Within(0.001f));
            fx.Advance(0.042f);
            Assert.That(rect.localScale.x, Is.EqualTo(0.95f).Within(0.001f));
            Assert.That(_root.Session.Log.Entries.Count, Is.EqualTo(commands));
            _root.Input.HandleTapAtScreen(new Vector2(50f, 50f));
            fx.Advance(0.10f);
            Assert.That(actions, Is.EqualTo(1), "a second tap during the press cannot navigate twice");
            _root.MotionOffToggle = true;
            fx.Advance(0.01f);
            Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
            Assert.That(face.color, Is.EqualTo(Color.white));
            _root.Input.HandleTapAtScreen(new Vector2(50f, 50f));
            Assert.That(actions, Is.EqualTo(2), "motion preference cannot suppress the action");
            Assert.That(fx.ActiveTweenCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PendingChromeAction_IsCancelledWhenItsRegionDisappears()
        {
            Time.timeScale = 0f;
            _root = GameRoot.LaunchWith(Fixture());
            yield return null;
            var button = new GameObject("Test chip", typeof(RectTransform), typeof(Image));
            button.transform.SetParent(_root.transform, false);
            int actions = 0;
            _root.Input.Regions.Register("press-cancel", () => new Rect(0f, 0f, 100f, 100f), () => actions++, 99);
            _root.Input.Regions.BindVisual("press-cancel", (RectTransform)button.transform);
            var fx = BoardFx.GetOrCreate(_root.transform, () => _root.MotionOff);
            _root.Input.HandleTapAtScreen(new Vector2(50f, 50f));
            _root.Input.Regions.Unregister("press-cancel");
            fx.Advance(0.2f);
            Assert.That(actions, Is.Zero, "a dismissed screen cannot execute its old navigation");
        }

        [UnityTest]
        public IEnumerator ReRegisteredChip_CannotExecuteThePreviousPress()
        {
            _root = GameRoot.LaunchWith(Fixture());
            yield return null;
            int actions = 0;
            System.Action action = () => actions++;
            var chip = new GameObject("refreshed chip", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            chip.transform.SetParent(_root.transform, false);
            _root.Input.Regions.Register("refresh", () => new Rect(0, 0, 100, 100), action, 100);
            _root.Input.Regions.BindVisual("refresh", (RectTransform)chip.transform);
            _root.Input.HandleTapAtScreen(new Vector2(50, 50));
            _root.Input.Regions.Unregister("refresh");
            _root.Input.Regions.Register("refresh", () => new Rect(0, 0, 100, 100), action, 100);
            _root.Input.Regions.BindVisual("refresh", (RectTransform)chip.transform);
            _root.GetComponent<CatMetro.Presentation.Fx.BoardFx>().Advance(0.2f);
            Assert.That(actions, Is.Zero, "a refreshed price/route needs a fresh tap, even with the same delegate");
        }

        private static ImportedLevel Fixture()
        {
            var r = LevelImporter.Import(Encoding.UTF8.GetBytes(FixtureJson()));
            Assert.That(r.Ok, Is.True, $"fixture must import: {r.Error}");
            return r.Value;
        }

        [UnityTest]
        public IEnumerator RefusedBudgetTap_HasNoAcceptedClunk_ShakesLeverAndPulsesFlips()
        {
            Time.timeScale = 0f;
            var json = FixtureJson().Replace("\"win\": {", "\"win\": { \"perfectMaxSwitches\": 1,");
            var imported = LevelImporter.Import(Encoding.UTF8.GetBytes(json));
            Assert.That(imported.Ok, Is.True, $"{imported.Error}");
            _root = GameRoot.LaunchWith(imported.Value);
            yield return null;
            int accepted = 0;
            _root.Input.SwitchTapAccepted += () => accepted++;
            _root.Input.HandleTapAtScreen(SwitchScreenPos());
            Assert.That(accepted, Is.EqualTo(1));
            _root.View.Fx.Advance(0.14f);
            _root.Preview.Refresh();
            var label = System.Array.Find(_root.Preview.GetComponentsInChildren<TMPro.TMP_Text>(),
                t => t.name == "flip-budget");
            Assert.That(label, Is.Not.Null);
            Assert.That(label.color, Is.EqualTo(CatMetro.Presentation.Theme.Palette.TabbyYellow),
                "a perfect run at its cap is yellow, never an error red");
            var lever = _root.View.GetComponentInChildren<CatMetro.Presentation.Board.ToySwitchView>();
            Quaternion neutral = lever.LeverPivot.localRotation;
            _root.Input.HandleTapAtScreen(SwitchScreenPos());
            Assert.That(accepted, Is.EqualTo(1), "a refused tap cannot play the accepted clunk");
            Assert.That(_root.Session.Log.Entries.Count, Is.EqualTo(1));
            _root.View.Fx.Advance(0.035f);
            _root.GetComponent<BoardFx>()?.Advance(0.035f);
            Assert.That(Quaternion.Angle(lever.LeverPivot.localRotation, neutral), Is.GreaterThan(0.5f));
            Assert.That(label.transform.localScale.x, Is.GreaterThan(1.01f));
            _root.View.Fx.Advance(0.2f);
            _root.GetComponent<BoardFx>()?.Advance(0.2f);
            Assert.That(Quaternion.Angle(lever.LeverPivot.localRotation, neutral), Is.LessThan(0.001f));
            Assert.That(label.transform.localScale.x, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator RefusedCooldownTap_OnlyFiresTheLockedFeedback()
        {
            Time.timeScale = 0f;
            var json = FixtureJson().Replace("\"initialRoute\": 0", "\"initialRoute\": 0, \"cooldownTicks\": 2");
            var imported = LevelImporter.Import(Encoding.UTF8.GetBytes(json));
            Assert.That(imported.Ok, Is.True, $"{imported.Error}");
            _root = GameRoot.LaunchWith(imported.Value);
            yield return null;
            int accepted = 0, refused = 0;
            _root.Input.SwitchTapAccepted += () => accepted++;
            _root.Input.SwitchTapRefused += () => refused++;
            _root.Input.HandleTapAtScreen(SwitchScreenPos());
            _root.Input.HandleTapAtScreen(SwitchScreenPos());
            Assert.That(accepted, Is.EqualTo(1));
            Assert.That(refused, Is.EqualTo(1));
            Assert.That(_root.Session.Log.Entries.Count, Is.EqualTo(1));
            _root.Session.AdvanceMs(250);
            _root.Input.HandleTapAtScreen(SwitchScreenPos());
            Assert.That(refused, Is.EqualTo(2));
            _root.Session.AdvanceMs(125);
            _root.Input.HandleTapAtScreen(SwitchScreenPos());
            Assert.That(accepted, Is.EqualTo(2), "the cooldown's last tick admits the next scheduled command");
        }

        [UnityTest]
        public IEnumerator LockedFeedback_HasItsOwnShortAudibleSynthClip()
        {
            _root = GameRoot.LaunchWith(Fixture());
            yield return null;
            var clip = _root.Audio.SwitchLockedClip;
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.length, Is.EqualTo(0.08f).Within(0.001f));
            var samples = new float[clip.samples];
            Assert.That(clip.GetData(samples, 0), Is.True);
            float peak = 0f;
            foreach (float value in samples) peak = Mathf.Max(peak, Mathf.Abs(value));
            Assert.That(peak, Is.InRange(0.1f, 0.5f));
        }

        private Vector2 SwitchScreenPos()
        {
            return _root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0));
        }

        private static Vector2 FarFromEverything()
        {
            return new Vector2(Screen.width - 2f, Screen.height - 2f);
        }

        private Vector2 BottomBandPoint()
        {
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.10f);
        }

        // --- criterion 3: the pin (labeled per P-7/A-UX1-4 — green on arrival, by design) ---

        [UnityTest]
        public IEnumerator PIN_MergedRouting_DiscToggleAndMissAndRetryBand_Unchanged()
        {
            _root = GameRoot.LaunchWith(Fixture());
            yield return null;

            // disc hit: index return, command appended, committed route flips this frame
            int before = _root.Session.Log.Entries.Count;
            int routeBefore = _root.View.CommittedRoute(0);
            Assert.That(_root.Input.HandleTapAtScreen(SwitchScreenPos()), Is.EqualTo(0));
            Assert.That(_root.Session.Log.Entries.Count, Is.EqualTo(before + 1));
            Assert.That(_root.View.CommittedRoute(0), Is.Not.EqualTo(routeBefore),
                "the lever shows the committed route on the tap frame");

            // miss: -1, nothing appended
            int afterHit = _root.Session.Log.Entries.Count;
            Assert.That(_root.Input.HandleTapAtScreen(FarFromEverything()), Is.EqualTo(-1));
            Assert.That(_root.Session.Log.Entries.Count, Is.EqualTo(afterHit));

            // retry band: -2 and the verb's own delegate fires (driven directly, the repo's
            // established seam — GameRoot wires the same delegates in Wire())
            bool retried = false;
            _root.Input.RetryRegionActive = () => true;
            _root.Input.RetryTapped = () => retried = true;
            Assert.That(_root.Input.HandleTapAtScreen(BottomBandPoint()), Is.EqualTo(-2));
            Assert.That(retried, Is.True);
            Assert.That(_root.Session.Log.Entries.Count, Is.EqualTo(afterHit),
                "the retry verb consumes the tap — no board command");
        }

        // --- criterion 1: regions resolve after the band, before the discs ---

        [UnityTest]
        public IEnumerator RegionOverDisc_ClaimsTheTap_NeverFallsThrough()
        {
            _root = GameRoot.LaunchWith(Fixture());
            yield return null;

            var discPos = SwitchScreenPos();
            int fired = 0;
            _root.Input.Regions.Register("over-disc",
                () => new Rect(discPos.x - 40, discPos.y - 40, 80, 80), () => fired++, 0);

            int before = _root.Session.Log.Entries.Count;
            int routeBefore = _root.View.CommittedRoute(0);
            Assert.That(_root.Input.HandleTapAtScreen(discPos), Is.EqualTo(-3),
                "a chrome region claims the tap (new consumption code)");
            Assert.That(fired, Is.EqualTo(1));
            Assert.That(_root.Session.Log.Entries.Count, Is.EqualTo(before),
                "no fall-through to the disc beneath");
            Assert.That(_root.View.CommittedRoute(0), Is.EqualTo(routeBefore));

            // positive control: unregister, same tap toggles the disc
            Assert.That(_root.Input.Regions.Unregister("over-disc"), Is.True);
            Assert.That(_root.Input.HandleTapAtScreen(discPos), Is.EqualTo(0));
            Assert.That(_root.Session.Log.Entries.Count, Is.EqualTo(before + 1));
        }

        [UnityTest]
        public IEnumerator RegionMissAndOverlapAndTie_ResolveDeterministically_LiveWired()
        {
            _root = GameRoot.LaunchWith(Fixture());
            yield return null;

            var p = new Vector2(Screen.width * 0.5f, Screen.height * 0.6f);
            string won = null;
            _root.Input.Regions.Register("low", () => new Rect(p.x - 50, p.y - 50, 100, 100),
                () => won = "low", 1);
            _root.Input.Regions.Register("tie-first", () => new Rect(p.x - 50, p.y - 50, 100, 100),
                () => won = "tie-first", 7);
            _root.Input.Regions.Register("tie-second", () => new Rect(p.x - 50, p.y - 50, 100, 100),
                () => won = "tie-second", 7);

            Assert.That(_root.Input.HandleTapAtScreen(p), Is.EqualTo(-3));
            Assert.That(won, Is.EqualTo("tie-first"),
                "highest priority wins; ties to the earliest registration");

            // miss: a tap outside every region falls through to normal routing (-1 here)
            Assert.That(_root.Input.HandleTapAtScreen(FarFromEverything()), Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator InBandRegionDuringFailureReview_IsDeadCode_TheBandWins()
        {
            _root = GameRoot.LaunchWith(Fixture());
            yield return null;

            bool retried = false;
            int regionFired = 0;
            _root.Input.RetryRegionActive = () => true; // FailureReview posture, driven directly
            _root.Input.RetryTapped = () => retried = true;
            var inBand = BottomBandPoint();
            _root.Input.Regions.Register("in-band",
                () => new Rect(inBand.x - 50, inBand.y - 50, 100, 100), () => regionFired++, 99);

            Assert.That(_root.Input.HandleTapAtScreen(inBand), Is.EqualTo(-2),
                "the retry band keeps FIRST claim — criterion 1's resolution-order law");
            Assert.That(retried, Is.True);
            Assert.That(regionFired, Is.EqualTo(0),
                "a region inside the band during FailureReview is dead code by law");

            // positive control: the SAME region fires once the band predicate is off
            _root.Input.RetryRegionActive = () => false;
            Assert.That(_root.Input.HandleTapAtScreen(inBand), Is.EqualTo(-3));
            Assert.That(regionFired, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DecoyRegionOutsideBand_LeavesTheBandFullWidth()
        {
            _root = GameRoot.LaunchWith(Fixture());
            yield return null;

            bool retried = false;
            int decoyFired = 0;
            _root.Input.RetryRegionActive = () => true;
            _root.Input.RetryTapped = () => retried = true;
            var above = new Vector2(Screen.width * 0.5f, Screen.height * 0.6f);
            _root.Input.Regions.Register("decoy",
                () => new Rect(above.x - 50, above.y - 50, 100, 100), () => decoyFired++, 0);

            Assert.That(_root.Input.HandleTapAtScreen(BottomBandPoint()), Is.EqualTo(-2),
                "regions elsewhere never shrink the band (criterion 3)");
            Assert.That(retried, Is.True);
            Assert.That(_root.Input.HandleTapAtScreen(above), Is.EqualTo(-3));
            Assert.That(decoyFired, Is.EqualTo(1));
        }

        // --- criterion 2: the board-input gate ---

        [UnityTest]
        public IEnumerator GateFalse_NoCommandNoVisual_PositiveControlsInSameFixture()
        {
            _root = GameRoot.LaunchWith(Fixture());
            yield return null;

            var discPos = SwitchScreenPos();

            // positive control FIRST: gate unbound (null) — merged behavior, tap toggles
            int count0 = _root.Session.Log.Entries.Count;
            Assert.That(_root.Input.HandleTapAtScreen(discPos), Is.EqualTo(0));
            Assert.That(_root.Session.Log.Entries.Count, Is.EqualTo(count0 + 1));

            // gate false: the disc scan is skipped — no command, committed route frozen
            _root.Input.BoardInputActive = () => false;
            int count1 = _root.Session.Log.Entries.Count;
            int route1 = _root.View.CommittedRoute(0);
            Assert.That(_root.Input.HandleTapAtScreen(discPos), Is.EqualTo(-1));
            Assert.That(_root.Session.Log.Entries.Count, Is.EqualTo(count1),
                "no command may reach the log through a closed gate");
            Assert.That(_root.View.CommittedRoute(0), Is.EqualTo(route1),
                "no lever flip against a stopped sim — the desync this gate exists to close");

            // positive control: gate re-opened — the SAME tap toggles again
            _root.Input.BoardInputActive = () => true;
            Assert.That(_root.Input.HandleTapAtScreen(discPos), Is.EqualTo(0));
            Assert.That(_root.Session.Log.Entries.Count, Is.EqualTo(count1 + 1));
        }

        [UnityTest]
        public IEnumerator GateFalse_BandAndRegionsStillResolve()
        {
            _root = GameRoot.LaunchWith(Fixture());
            yield return null;

            _root.Input.BoardInputActive = () => false;

            // regions still live behind a closed gate
            int fired = 0;
            var p = new Vector2(Screen.width * 0.5f, Screen.height * 0.6f);
            _root.Input.Regions.Register("chrome",
                () => new Rect(p.x - 50, p.y - 50, 100, 100), () => fired++, 0);
            Assert.That(_root.Input.HandleTapAtScreen(p), Is.EqualTo(-3));
            Assert.That(fired, Is.EqualTo(1));

            // the retry band still lives behind a closed gate
            bool retried = false;
            _root.Input.RetryRegionActive = () => true;
            _root.Input.RetryTapped = () => retried = true;
            Assert.That(_root.Input.HandleTapAtScreen(BottomBandPoint()), Is.EqualTo(-2));
            Assert.That(retried, Is.True);
        }

        // Quiet fixture: one switch over two routes, no waves in play range, generous limit —
        // Playing persists for the whole test (same JSON shape as FailureTests' fixture).
        private static string FixtureJson()
        {
            return @"{
  ""schemaVersion"": 2, ""id"": ""T905"", ""name"": ""Routing Fixture"", ""seed"": 905,
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
  ""win"": { ""deliveries"": 99, ""timeLimitTicks"": 4000,
    ""stars"": { ""two"": 200, ""three"": 300 } },
  ""economy"": { ""baseTickets"": 20, ""perfectBonus"": 10 }
}";
        }
    }
}
