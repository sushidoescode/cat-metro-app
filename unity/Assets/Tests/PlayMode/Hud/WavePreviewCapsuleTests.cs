using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Content;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Hud.WavePreview;
using CatMetro.Presentation.Theme;

namespace CatMetro.Tests.PlayMode
{
    // HUD-WAVE: the wave preview capsule from docs/reference/target-01-tabletop.png — a cream
    // card in the status band holding the upcoming cats in order as faces, plus the run
    // counters. Geometry is asserted through the PURE laws at the pinned 917x2048 phone frame
    // (the editor's Game view aspect must never decide whether this passes); data, colour and
    // the render-only invariants are asserted on a live strip built by the real GameRoot seam.
    public sealed class WavePreviewCapsuleTests
    {
        // The reference phone frame, matching UiPhoneCaptureTests.
        private const float CaptureDpi = 408f;
        private static readonly Rect PhoneSafeArea = new Rect(0f, 64f, 917f, 1920f);

        private GameRoot _root;

        [SetUp]
        public void SetUp()
        {
            // Boot straight to the board: Home would hold the sim at tick 0 and cover the HUD.
            GameRoot.DevSkipShippedHome = true;
        }

        [TearDown]
        public void TearDown()
        {
            GameRoot.DevSkipShippedHome = false;
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
        }

        // --- geometry: the pure laws at the pinned phone aspect ---

        [Test]
        public void Capsule_SitsInsideTheSafeAreaStatusBand_AtThePinnedPhoneAspect()
        {
            var capsule = WavePreviewStrip.CapsuleRect(PhoneSafeArea, CaptureDpi);
            var band = HudBands.StatusBand(PhoneSafeArea);

            Assert.That(capsule.yMax, Is.LessThanOrEqualTo(band.yMax),
                "the capsule hangs below the safe-area top, never under a notch");
            Assert.That(capsule.yMin, Is.GreaterThanOrEqualTo(band.yMin),
                "the capsule stays inside the top 15% status band");
            Assert.That(capsule.xMin, Is.GreaterThan(PhoneSafeArea.xMin),
                "the capsule keeps a horizontal safe-area inset");
            Assert.That(capsule.xMax, Is.LessThan(PhoneSafeArea.xMax),
                "the capsule keeps a horizontal safe-area inset");
        }

        [Test]
        public void Capsule_IsHorizontallyCentredAndAboutFourFifthsOfTheFrame()
        {
            var capsule = WavePreviewStrip.CapsuleRect(PhoneSafeArea, CaptureDpi);

            // The target art's capsule spans ~80% of the frame, centred.
            Assert.That(capsule.width / PhoneSafeArea.width, Is.EqualTo(0.80f).Within(0.02f));
            Assert.That(capsule.center.x, Is.EqualTo(PhoneSafeArea.center.x).Within(0.5f),
                "equal insets left and right");
        }

        [Test]
        public void CounterRow_SitsDirectlyBelowTheCapsule_WithoutOverlapping()
        {
            var capsule = WavePreviewStrip.CapsuleRect(PhoneSafeArea, CaptureDpi);
            var counters = WavePreviewStrip.CounterRowRect(PhoneSafeArea, CaptureDpi);

            Assert.That(counters.yMax, Is.LessThan(capsule.yMin),
                "the counters clear the capsule — the target stacks them, never overlaps");
            Assert.That(counters.yMin, Is.GreaterThan(PhoneSafeArea.yMin));
            Assert.That(counters.x, Is.EqualTo(capsule.x).Within(0.01f),
                "the counter row shares the capsule's column");
        }

        [Test]
        public void Capsule_SurvivesAShortViewport_ViaItsDpFloors()
        {
            // A squat editor Game view must not collapse the capsule to a hairline.
            var squat = new Rect(0f, 0f, 800f, 300f);
            var capsule = WavePreviewStrip.CapsuleRect(squat, 160f);

            Assert.That(capsule.height, Is.GreaterThanOrEqualTo(
                HudBands.MinTargetDp * HudBands.PxPerDp(160f)),
                "the dp floor keeps the capsule at least one touch-target tall");
            Assert.That(capsule.yMax, Is.LessThanOrEqualTo(squat.yMax));
            Assert.That(capsule.width, Is.GreaterThan(0f));
        }

        // --- data: faces come from the session's upcoming wave, in order ---

        [UnityTest]
        public IEnumerator Faces_MatchL001sUpcomingCats_OneFacePerCat()
        {
            _root = GameRoot.Launch(); // L001: one wave, red x2
            yield return null;

            // The OLD strip could only say "red x2" — one chip for the whole wave. The capsule
            // shows the cats themselves, which is the whole point of the redesign.
            Assert.That(_root.Preview.FaceCount, Is.EqualTo(2),
                "L001's single red wave of 2 is TWO faces, not one chip");
            Assert.That(_root.Preview.FaceSummary, Is.EqualTo("red|red"));
            Assert.That(_root.Preview.RemainingCats, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator Faces_AreOrderedByEmissionTick_AcrossWaves()
        {
            // red@5, blue@10, red@20, blue@30 — authored out of order on purpose, so a strip
            // that just concatenated waves would read "blue|blue|red|red" and fail here.
            _root = GameRoot.LaunchWith(Import(InterleavedFixture()));
            yield return null;

            Assert.That(_root.Preview.FaceSummary, Is.EqualTo("red|blue|red|blue"),
                "faces follow EMISSION order, not authoring order");
            Assert.That(_root.Preview.FaceCount, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator Queue_Shrinks_AsCatsAreEmitted()
        {
            // NOT L001. Its switch is authored initialRoute 1 (J1 -> E3 -> BLU) while its only
            // wave is RED — the mis-route IS the puzzle, so driving L001 forward without
            // flipping the switch lands a red cat in a blue berth at ~tick 30 and trips the
            // pinned NEW-Q4 limitation (rejection is out of CM-C1 scope). That pin is real and
            // must not be caught or suppressed; the fix is a fixture that cannot reach it.
            //
            // DrainFixture gives the cats a 400-tick ride, so within this window they are still
            // far out on the first edge. The test then measures exactly what it claims to —
            // that EMISSION drains the queue — with routing kept out of the question entirely.
            _root = GameRoot.LaunchWith(Import(DrainFixture()));
            yield return null;
            Assert.That(_root.Preview.FaceCount, Is.EqualTo(2), "both cats still to come");

            // Emissions are at tick 2 and tick 5; 12 ticks clears both.
            _root.Session.AdvanceMs(12 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return null;

            Assert.That(_root.Preview.FaceCount, Is.Zero, "emitted cats leave the capsule");
            Assert.That(_root.Preview.FaceSummary, Is.Empty);
            Assert.That(_root.Preview.RemainingCats, Is.Zero);

            // Proves the drain was emission and not delivery — if a cat had reached a berth
            // this test would be quietly measuring something else.
            Assert.That(_root.Session.State.Deliveries, Is.Zero,
                "no cat reached a station in the window");
            Assert.That(_root.ScreenState, Is.EqualTo("Playing"));
        }

        [UnityTest]
        public IEnumerator Overflow_CollapsesTheTail_WhenMoreCatsThanFacesRemain()
        {
            _root = GameRoot.LaunchWith(Import(FloodFixture()));
            yield return null;

            Assert.That(_root.Preview.FaceCount, Is.EqualTo(WavePreviewStrip.MaxFaces),
                "the capsule fills to its cap");
            Assert.That(_root.Preview.RemainingCats,
                Is.GreaterThan(WavePreviewStrip.MaxFaces),
                "precondition: the fixture really does overflow the capsule");
        }

        // --- counters ---

        [UnityTest]
        public IEnumerator Counters_ReadDeliveriesAgainstTheLevelsWinTarget()
        {
            _root = GameRoot.Launch(); // L001 wins at 2 deliveries
            yield return null;

            Assert.That(_root.Preview.DeliveriesText, Is.EqualTo("0/2"),
                "the trophy counter is State.Deliveries over Win.Deliveries");
            Assert.That(_root.Preview.RidersText, Is.EqualTo("0"),
                "no cats are riding before the first wave emits");
        }

        // --- accessibility: colour is never the only carrier ---

        [UnityTest]
        public IEnumerator EachFace_CarriesAShapeAndALetter_NotJustAColour()
        {
            _root = GameRoot.LaunchWith(Import(InterleavedFixture()));
            yield return null;

            var red = _root.Preview.Face(0);
            var blue = _root.Preview.Face(1);

            Assert.That(red.ColorName, Is.EqualTo("red"));
            Assert.That(blue.ColorName, Is.EqualTo("blue"));

            // Shape — and the two shapes are the ones the BOARD already paints for these
            // lines (BoardPropDecorator: "R" gets a cylinder plate, anything else a cube).
            Assert.That(red.Shape, Is.EqualTo(DestinationShape.Circle));
            Assert.That(blue.Shape, Is.EqualTo(DestinationShape.Square));
            Assert.That(red.Shape, Is.Not.EqualTo(blue.Shape),
                "shape alone distinguishes the two destinations");

            // Letter — the same glyph BoardView stamps on the station.
            Assert.That(red.Glyph, Is.EqualTo("R"));
            Assert.That(blue.Glyph, Is.EqualTo("B"));
        }

        [UnityTest]
        public IEnumerator FaceColours_BindPaletteTokens_NotInlineLiterals()
        {
            _root = GameRoot.LaunchWith(Import(InterleavedFixture()));
            yield return null;

            Assert.That(_root.Preview.Face(0).HeadColor, Is.EqualTo(Palette.SignalRed));
            Assert.That(_root.Preview.Face(1).HeadColor, Is.EqualTo(Palette.HarborBlue));
            Assert.That(_root.Preview.Face(0).BadgeColor, Is.EqualTo(Palette.SignalRed));
        }

        [UnityTest]
        public IEnumerator Capsule_IsPaintedInTheLockedCreamChrome()
        {
            _root = GameRoot.Launch();
            yield return null;

            Assert.That(_root.Preview.CapsuleColor, Is.EqualTo(Palette.WarmPaper),
                "the capsule is the warm-paper chrome token, not an inline cream");
            Assert.That(_root.Preview.WaveColor, Is.EqualTo(Palette.CreamCard),
                "the decorative wave is a shade of the same locked cream");
        }

        // --- house rules: render-only, no geometry, no shadows, no colliders ---

        [UnityTest]
        public IEnumerator Preview_ContributesNoRenderersAtAll_SoNothingCanCastIntoTheDiorama()
        {
            _root = GameRoot.Launch();
            yield return null;

            // BoardLookTests.WorldSpacePreview_DoesNotCastIntoTheDiorama walks the Renderers
            // under Preview and requires shadows off on each. Now that the capsule is pure
            // UGUI (CanvasRenderer, which is NOT a Renderer) that loop has nothing to walk —
            // so pin the stronger property here explicitly, rather than letting the older
            // test quietly pass vacuously and call it proof.
            Assert.That(_root.Preview.GetComponentsInChildren<Renderer>(true), Is.Empty,
                "the HUD is screen-space UGUI — it owns no scene geometry to cast or receive");
        }

        [UnityTest]
        public IEnumerator Preview_HasZeroInteractiveElements()
        {
            _root = GameRoot.Launch();
            yield return null;

            Assert.That(_root.Preview.GetComponentsInChildren<Collider>(true), Is.Empty,
                "information only — no collider");
            Assert.That(_root.Preview.GetComponentsInChildren<GraphicRaycaster>(true), Is.Empty,
                "information only — the canvas must not raycast");
            foreach (var graphic in _root.Preview.GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False,
                    graphic.gameObject.name + " must not be a raycast target");
        }

        [UnityTest]
        public IEnumerator Preview_RendersBelowTheBannerAndTheModalChrome()
        {
            _root = GameRoot.Launch();
            yield return null;

            var canvas = _root.Preview.GetComponent<Canvas>();
            Assert.That(canvas, Is.Not.Null, "the capsule is a screen-space canvas");
            Assert.That(canvas.sortingOrder, Is.LessThan(90),
                "the HUD sits under the banner (90), chrome (100), results (110), screens (120)");
        }

        // --- the outcome states hide the HUD OUTRIGHT, not by z-order ---

        [Test]
        public void VisibleInState_HidesTheHudOnceTheRunIsOver()
        {
            // "What is coming next" is a meaningless question after the run ends.
            Assert.That(WavePreviewStrip.VisibleInState("Won"), Is.False);
            Assert.That(WavePreviewStrip.VisibleInState("FailureReview"), Is.False);
            // Halted is a PAUSE, not an ending — the queue still matters.
            Assert.That(WavePreviewStrip.VisibleInState("Playing"), Is.True);
            Assert.That(WavePreviewStrip.VisibleInState("Halted"), Is.True);
        }

        [UnityTest]
        public IEnumerator Capsule_HidesItself_WhenTheRunIsWonOrFailed()
        {
            _root = GameRoot.Launch();
            yield return null;
            Assert.That(_root.Preview.IsVisible, Is.True, "visible while playing");

            var state = "Playing";
            _root.Preview.BindScreenState(() => state);

            state = "FailureReview";
            yield return null;
            Assert.That(_root.Preview.IsVisible, Is.False,
                "the HUD leaves the screen on FailureReview — it does not merely sort under "
                + "the banner, which would break the moment either view's order changed");

            state = "Won";
            yield return null;
            Assert.That(_root.Preview.IsVisible, Is.False);

            state = "Playing";
            yield return null;
            Assert.That(_root.Preview.IsVisible, Is.True, "and comes back for the next run");
        }

        [UnityTest]
        public IEnumerator Capsule_HidingDoesNotDependOnSortingOrder()
        {
            _root = GameRoot.Launch();
            yield return null;

            var state = "Won";
            _root.Preview.BindScreenState(() => state);
            yield return null;

            // Force the HUD to sort ABOVE the banner. If hiding were a z-order trick this
            // would put the dead preview back on top of the outcome copy.
            _root.Preview.GetComponent<Canvas>().sortingOrder = 999;
            yield return null;

            Assert.That(_root.Preview.IsVisible, Is.False,
                "hiding is a state decision, independent of who sorts above whom");
        }

        // --- the legacy read-backs other suites pin ---

        [UnityTest]
        public IEnumerator LegacyWaveReadbacks_KeepTheirOriginalMeanings()
        {
            _root = GameRoot.Launch();
            yield return null;

            Assert.That(_root.Preview.ChipSummary, Is.EqualTo("red x2"),
                "pending WAVE summary grammar is unchanged (FailureTests pins it)");
            Assert.That(_root.Preview.VisibleChipCount, Is.EqualTo(1));
            Assert.That(_root.Preview.InTopBand(0), Is.True,
                "the preview still reports itself in the top 0-15% band");
        }

        [UnityTest]
        public IEnumerator Teardown_LeavesNoStrayHudObjects()
        {
            _root = GameRoot.Launch();
            yield return null;
            Assert.That(GameObject.Find("WavePreview"), Is.Not.Null, "precondition");

            Object.DestroyImmediate(_root.gameObject);
            _root = null;
            yield return null;

            Assert.That(GameObject.Find("WavePreview"), Is.Null,
                "the capsule is parented under GameRoot and dies with it");
        }

        // --- fixtures ---

        private static ImportedLevel Import(string json)
        {
            var r = LevelImporter.Import(Encoding.UTF8.GetBytes(json));
            Assert.That(r.Ok, Is.True, "fixture must import: " + r.Error);
            return r.Value;
        }

        // Waves authored deliberately OUT of emission order so an order assertion has teeth.
        private static string InterleavedFixture() => FixtureJson(@"[
    { ""tick"": 10, ""sourceNode"": ""SRC"", ""color"": ""blue"", ""count"": 2, ""spacingTicks"": 20 },
    { ""tick"": 5,  ""sourceNode"": ""SRC"", ""color"": ""red"",  ""count"": 1, ""spacingTicks"": 10 },
    { ""tick"": 20, ""sourceNode"": ""SRC"", ""color"": ""red"",  ""count"": 1, ""spacingTicks"": 10 } ]");

        // Cats that emit almost immediately but ride for a very long time, so the queue can
        // be observed draining without any cat arriving anywhere.
        private static string DrainFixture() => FixtureJson(@"[
    { ""tick"": 2, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 2, ""spacingTicks"": 3 } ]",
            travelTicks: 400);

        // More cats than the capsule can show, to exercise the "+N" tail.
        private static string FloodFixture() => FixtureJson(@"[
    { ""tick"": 8, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 6, ""spacingTicks"": 4 },
    { ""tick"": 40, ""sourceNode"": ""SRC"", ""color"": ""blue"", ""count"": 6, ""spacingTicks"": 4 } ]");

        // The FailureTests board shape (SRC -> J1 -> RED|BLU), with the waves swapped in.
        private static string FixtureJson(string waves, int travelTicks = 12)
        {
            return @"{
  ""schemaVersion"": 2, ""id"": ""T950"", ""name"": ""Hud Wave Fixture"", ""seed"": 950,
  ""meta"": { ""band"": ""onboarding"", ""difficultyTarget"": 0.1, ""mechanics"": [""switch""],
    ""newMechanic"": null, ""teachingGoal"": ""test fixture"", ""minActionWindowTicks"": 12,
    ""authoredBy"": ""llm+validator"" },
  ""board"": { ""nodes"": [
      { ""id"": ""SRC"", ""x"": 3, ""y"": 9 },
      { ""id"": ""J1"", ""x"": 3, ""y"": 6 },
      { ""id"": ""RED"", ""x"": 1, ""y"": 2 }, { ""id"": ""BLU"", ""x"": 5, ""y"": 2 } ],
    ""edges"": [
      { ""id"": ""E1"", ""from"": ""SRC"", ""to"": ""J1"", ""travelTicks"": " + travelTicks + @" },
      { ""id"": ""E2"", ""from"": ""J1"", ""to"": ""RED"", ""travelTicks"": " + travelTicks + @" },
      { ""id"": ""E3"", ""from"": ""J1"", ""to"": ""BLU"", ""travelTicks"": " + travelTicks + @" } ] },
  ""sources"": [ { ""nodeId"": ""SRC"", ""allowedColors"": [""red"", ""blue""] } ],
  ""stations"": [
    { ""nodeId"": ""RED"", ""accepts"": [""red""], ""capacity"": 6 },
    { ""nodeId"": ""BLU"", ""accepts"": [""blue""], ""capacity"": 6 } ],
  ""switches"": [ { ""id"": ""S1"", ""nodeId"": ""J1"", ""routes"": [""E2"", ""E3""], ""initialRoute"": 0 } ],
  ""waves"": " + waves + @",
  ""win"": { ""deliveries"": 4, ""timeLimitTicks"": 4000, ""perfectMaxSwitches"": 1,
    ""stars"": { ""two"": 200, ""three"": 300 } },
  ""economy"": { ""baseTickets"": 20, ""perfectBonus"": 10 }
}";
        }
    }
}
