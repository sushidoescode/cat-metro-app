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
    // CM-UX-05 criteria 2/3/4/5/7 under the live-wiring + anti-vacuity rule: the controller
    // attaches over REAL wiring (GameRoot.LaunchWith) with a test-controlled state source —
    // the same Func<string> delegate shape CM-UX-07 binds (state literals are the merged
    // vocabulary, pinned against real transitions by the merged CM-UX-02 real-halt test).
    // Every absence assert carries a positive control in the same fixture.
    public sealed class HintChipStateTests
    {
        private GameRoot _root;
        private string _state;

        [TearDown]
        public void TearDown()
        {
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

        private HintChipController AttachControlled(string initial)
        {
            _root = GameRoot.LaunchWith(Fixture());
            _state = initial;
            // CM-TESTFIX: resolve the Wire-attached instance instead of stacking a second
            // controller on the GameObject (the debt this contract fixes — #50's Wire-time
            // guard means AddComponent here would either throw duplicate-id on registration or
            // silently run two controllers side by side, doubling the HintAttemptCounter).
            var hint = _root.GetComponent<HintChipController>();
            Assert.That(hint, Is.Not.Null,
                "precondition: GameRoot.Wire must have attached a HintChipController — "
                + "otherwise the fixture has nothing to resolve and every assert below proves "
                + "nothing");
            Assert.That(_root.gameObject.GetComponents<HintChipController>().Length,
                Is.EqualTo(1),
                "exactly one controller on the GameObject — pins single-controller composition "
                + "forever (would have been RED before #50's Wire-time guard)");
            hint.Attach(() => _state);
            return hint;
        }

        private static bool ChipVisible(HintChipController hint)
        {
            return hint.Chip != null && hint.Chip.IsVisible;
        }

        // --- criterion 2: hidden by default, visible after the 2nd entry, persists ---

        [UnityTest]
        public IEnumerator Chip_HiddenByDefault_VisibleAfterSecondEntry_WithinOneFrame()
        {
            var hint = AttachControlled("Playing");
            yield return null;
            Assert.That(ChipVisible(hint), Is.False,
                "hidden by default — zero entries render nothing (the CM-R13.1 posture)");

            _state = "FailureReview";
            yield return null;
            Assert.That(hint.AttemptCount, Is.EqualTo(1), "first entry counted");
            Assert.That(ChipVisible(hint), Is.False,
                "one entry stays chip-less — the hint is a SECOND-fall-of-the-run affordance");

            _state = "Playing";
            yield return null;
            Assert.That(ChipVisible(hint), Is.False, "still nothing during the first retry");

            _state = "FailureReview";
            yield return null; // within one pumped frame of the 2nd entry
            Assert.That(hint.AttemptCount, Is.EqualTo(2), "second entry counted");
            Assert.That(ChipVisible(hint), Is.True,
                "the chip renders after the 2nd FailureReview entry — CM-R13.5 / node L");
            Assert.That(hint.Chip.RenderedText,
                Is.EqualTo(CatMetro.Presentation.Strings.UiStrings.Get("hint.tutorial")),
                "the chip copy resolves through the csv key, never a literal");
        }

        [UnityTest]
        public IEnumerator Chip_PersistsIntoTheRetry_AndResetForNewLevelHidesAndReArms()
        {
            var hint = AttachControlled("Playing");
            yield return null;
            _state = "FailureReview";
            yield return null;
            _state = "Playing";
            yield return null;
            _state = "FailureReview";
            yield return null;
            Assert.That(ChipVisible(hint), Is.True, "pre-condition: chip up after entry 2");

            _state = "Playing";
            yield return null;
            Assert.That(ChipVisible(hint), Is.True,
                "node L returns the player to play WITH the hint — the chip persists");

            hint.ResetForNewLevel();
            yield return null;
            Assert.That(ChipVisible(hint), Is.False, "a new level starts chip-less");
            Assert.That(hint.AttemptCount, Is.EqualTo(0), "and with a fresh attempt-run");

            _state = "FailureReview";
            yield return null;
            Assert.That(hint.AttemptCount, Is.EqualTo(1), "the edge re-armed after reset");
            Assert.That(ChipVisible(hint), Is.False,
                "one entry of the NEW run renders nothing — the law is per attempt-run");
        }

        // --- criterion 3: Halted hides the chip and never counts ---

        [UnityTest]
        public IEnumerator Halted_HidesTheChip_AndLeavesTheCountUntouched()
        {
            var hint = AttachControlled("Playing");
            yield return null;
            _state = "FailureReview";
            yield return null;
            _state = "Playing";
            yield return null;
            _state = "FailureReview";
            yield return null;
            Assert.That(ChipVisible(hint), Is.True, "positive control: chip demonstrably up");

            _state = "Halted";
            yield return null;
            Assert.That(ChipVisible(hint), Is.False,
                "nothing decorates the halt surface — the CM-UX-02 veil owns it");
            Assert.That(hint.AttemptCount, Is.EqualTo(2),
                "an unexpected halt is not a completed gameplay attempt");

            _state = "Playing";
            yield return null;
            Assert.That(ChipVisible(hint), Is.True,
                "leaving Halted restores the chip — information was removed, not destroyed");
            Assert.That(hint.AttemptCount, Is.EqualTo(2), "count unchanged across the round-trip");
        }

        // --- criterion 4: the painted rect IS the pure law (the R1-H1 join) ---

        [UnityTest]
        public IEnumerator Chip_PaintedRect_IsTheChipRectLaw_AndFloorHolds()
        {
            var hint = AttachControlled("Playing");
            yield return null;
            _state = "FailureReview";
            yield return null;
            _state = "Playing";
            yield return null;
            _state = "FailureReview";
            yield return null;
            Assert.That(ChipVisible(hint), Is.True, "pre-condition: chip rendered");

            var expected = HintChipView.ChipRect(Screen.safeArea, Screen.dpi);
            Assert.That(hint.Chip.PaintedRectPx, Is.EqualTo(expected),
                "the component's applied rect IS the pure placement law — no drift possible");
            Assert.That(hint.Chip.PaintedRectPx.yMin,
                Is.EqualTo(HudBands.ThumbBand(Screen.safeArea).yMax).Within(0.001f),
                "board-edge above the thumb band on the live host");
            float dpi = Screen.dpi > 0f ? Screen.dpi : 160f;
            Assert.That(HudBands.MeetsMinTargetPx(hint.Chip.PaintedRectPx, dpi), Is.True,
                "the 48dp floor holds on the live painted rect");
        }

        // --- criterion 5: render-only, registers nothing, tree whitelisted ---

        private static readonly System.Type[] Whitelist =
        {
            typeof(Transform), typeof(RectTransform), typeof(Canvas),
            typeof(CanvasRenderer), typeof(UnityEngine.UI.Image),
            typeof(TMPro.TextMeshProUGUI), typeof(HintChipView),
            // (the controller sits on GameRoot's object, never under ChipRoot)
        };

        private static Component FirstOffWhitelist(GameObject root)
        {
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                bool ok = false;
                foreach (var t in Whitelist)
                    if (t.IsInstanceOfType(c)) { ok = true; break; }
                if (!ok) return c;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator Chip_RegistersNoRegions_AndTreeIsRenderOnly()
        {
            var hint = AttachControlled("Playing");
            yield return null;
            int baseline = _root.Input.Regions.Count;

            _state = "FailureReview";
            yield return null;
            _state = "Playing";
            yield return null;
            _state = "FailureReview";
            yield return null;
            Assert.That(ChipVisible(hint), Is.True, "pre-condition: chip rendered");
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(baseline),
                "the chip is render-only in v1 — zero registered targets (A11Y-S01-4 is "
                + "honored dimensionally; a tappable hint is future work)");

            // positive control (anti-vacuity): the counter observes THIS registry
            _root.Input.Regions.Register("decoy", () => new Rect(0, 0, 1, 1), () => { }, 0);
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(baseline + 1));
            Assert.That(_root.Input.Regions.Unregister("decoy"), Is.True);
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(baseline));

            // whole-root whitelist walk (the CM-UX-02 R1-H2 pattern)
            Assert.That(hint.ChipRoot, Is.Not.Null);
            var off = FirstOffWhitelist(hint.ChipRoot);
            Assert.That(off, Is.Null, "chip tree carries render-side types only; found: "
                + (off ? off.GetType().FullName : ""));
            Assert.That(hint.ChipRoot
                .GetComponentsInChildren<UnityEngine.UI.Selectable>(true).Length, Is.EqualTo(0),
                "no Selectable anywhere under the chip");
            Assert.That(hint.ChipRoot
                .GetComponentsInChildren<UnityEngine.UI.GraphicRaycaster>(true).Length,
                Is.EqualTo(0), "no raycaster-driven interactivity under the chip");

            // positive control: the walk DOES flag an interactive component elsewhere
            var decoyGo = new GameObject("decoy-tree");
            try
            {
                decoyGo.AddComponent<UnityEngine.UI.Button>();
                Assert.That(FirstOffWhitelist(decoyGo), Is.Not.Null,
                    "the whitelist walk can detect a non-render component");
            }
            finally { Object.Destroy(decoyGo); }
        }

        [UnityTest]
        public IEnumerator Chip_TmpGlyphGeometryExists_RenderabilityProxy()
        {
            var hint = AttachControlled("Playing");
            yield return null;
            _state = "FailureReview";
            yield return null;
            _state = "Playing";
            yield return null;
            _state = "FailureReview";
            yield return null;
            yield return null; // TMP layout settles a frame after activation
            var tmp = hint.Chip.GetComponentInChildren<TMPro.TMP_Text>(true);
            Assert.That(tmp, Is.Not.Null, "the chip text is TMP (human answer Q-6)");
            tmp.ForceMeshUpdate();
            Assert.That(tmp.textInfo.characterCount, Is.GreaterThan(0),
                "generated glyph geometry exists — the A-UX2-2 renderability proxy");
        }

        // --- criterion 7: motion posture, stated honestly ---

        private static int MotionComponentCount(GameObject root)
        {
            return root.GetComponentsInChildren<Animator>(true).Length
                 + root.GetComponentsInChildren<Animation>(true).Length;
        }

        [UnityTest]
        public IEnumerator MotionOff_RendersTheChip_ZeroAnimationComponents()
        {
            // v1 ships no chip motion at all, so the guarantee is structural (zero animation
            // components — the honest CM-UX-02 R1-M5 posture): motion-off removes nothing
            // because nothing moves, and the full information set still renders.
            var hint = AttachControlled("Playing");
            _root.MotionOffToggle = true;
            Assert.That(_root.MotionOff, Is.True,
                "decoy control: the toggle assignment IS observable at the GameRoot seam this "
                + "pin relies on — a mutated GameRoot.MotionOff (e.g. ignoring the toggle) "
                + "would read False here");
            yield return null;
            _state = "FailureReview";
            yield return null;
            _state = "Playing";
            yield return null;
            _state = "FailureReview";
            yield return null;
            Assert.That(ChipVisible(hint), Is.True,
                "motion-off renders the full information set");
            Assert.That(MotionComponentCount(hint.ChipRoot), Is.EqualTo(0),
                "no animation components anywhere under the chip");

            // positive control: the counter counts when one exists
            var decoy = new GameObject("decoy-anim");
            try
            {
                decoy.AddComponent<Animator>();
                Assert.That(MotionComponentCount(decoy), Is.EqualTo(1));
            }
            finally { Object.Destroy(decoy); }
        }

        // --- criterion 7: the a11y label hook carries the resolved copy ---

        [UnityTest]
        public IEnumerator Chip_AccessibilityLabel_IsTheResolvedCsvCopy()
        {
            var hint = AttachControlled("Playing");
            yield return null;
            _state = "FailureReview";
            yield return null;
            _state = "Playing";
            yield return null;
            _state = "FailureReview";
            yield return null;
            Assert.That(hint.Chip.AccessibilityLabel,
                Is.EqualTo(CatMetro.Presentation.Strings.UiStrings.Get("hint.tutorial")),
                "the live-region hook exposes the resolved copy (A11Y-S01-5, hook only)");
        }

        private static string FixtureJson()
        {
            return @"{
  ""schemaVersion"": 2, ""id"": ""T907"", ""name"": ""Hint Fixture"", ""seed"": 907,
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
