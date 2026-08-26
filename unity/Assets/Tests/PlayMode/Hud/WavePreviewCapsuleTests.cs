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
        public void Capsule_SitsAtTheTOPOfTheFrame_AsTheTargetArtHasIt()
        {
            // THE TARGET ART IS THE AUTHORITY ON THE EDGE. docs/reference/target-01-tabletop.png
            // puts the capsule unambiguously at the TOP — roughly 3-11% down from the top edge,
            // with the counters beneath it. "The safe-area status band" is silent on which end
            // of the safe rect it means, so this asserts the edge in the same terms the art is
            // measured in: distance DOWN from the top of the frame. A capsule anchored to the
            // bottom passes a "status band" phrasing and fails this.
            var capsule = WavePreviewStrip.CapsuleRect(PhoneSafeArea, CaptureDpi);
            const float frameHeight = 2048f;

            float topGapFraction = (frameHeight - capsule.yMax) / frameHeight;
            float bottomEdgeFraction = (frameHeight - capsule.yMin) / frameHeight;

            Assert.That(topGapFraction, Is.LessThan(0.13f),
                "the capsule's top edge is near the TOP of the frame, as in the target");
            Assert.That(bottomEdgeFraction, Is.LessThan(0.20f),
                "the whole capsule sits in the upper fifth — it is not a bottom bar");
            Assert.That(capsule.center.y, Is.GreaterThan(frameHeight * 0.5f),
                "unambiguously the top half");

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

        [UnityTest]
        public IEnumerator Capsule_LandsAtTheTop_EvenWhenNoRigInjectsAViewport()
        {
            // The regression behind slot 5's "wrong edge" reading. Screen.safeArea is in SCREEN
            // pixels while a Screen Space - Camera canvas is sized by its CAMERA's pixel rect,
            // so a capture into a RenderTexture fed the law a ~619x489 batchmode screen rect
            // against a 917x2048 canvas — putting the capsule 75% down the frame at 55% width.
            // The layout now derives its viewport from the CANVAS, so a rig that forgets to
            // inject still gets the capsule on the correct edge.
            _root = GameRoot.Launch();
            yield return null;

            var canvas = (RectTransform)_root.Preview.GetComponent<Canvas>().transform;
            var painted = _root.Preview.CapsuleRectPx;
            Assert.That(canvas.rect.height, Is.GreaterThan(0f), "precondition");

            Assert.That(painted.center.y, Is.GreaterThan(canvas.rect.height * 0.5f),
                "the capsule is in the TOP half of whatever it is rendering into");
            Assert.That((canvas.rect.height - painted.yMax) / canvas.rect.height,
                Is.LessThan(0.13f), "its top edge hugs the top of the canvas");
            Assert.That(painted.width / canvas.rect.width, Is.EqualTo(0.80f).Within(0.03f),
                "and it spans the canvas, not a stale smaller screen rect");
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

        // The drain window, and the ride each cat gets. TRAVEL_TICKS_MAX is the longest edge the
        // importer will accept (ContentBounds: travelTicks is bounded [1,40]) — an earlier
        // attempt at this fixture asked for 400 and simply failed to import.
        private const int DrainTravelTicks = ContentBounds.TRAVEL_TICKS_MAX;
        private const int DrainWindowTicks = 12;

        [UnityTest]
        public IEnumerator Queue_Shrinks_AsCatsAreEmitted()
        {
            // NOT L001. Its switch is authored initialRoute 1 (J1 -> E3 -> BLU) while its only
            // wave is RED — the mis-route IS the puzzle, so driving L001 forward without
            // flipping the switch lands a red cat in a blue berth at ~tick 30 and trips the
            // pinned NEW-Q4 limitation (rejection is out of CM-C1 scope). That pin is real and
            // must not be caught or suppressed; the fix is a fixture that cannot reach it.
            //
            // DrainFixture cannot reach it TWICE OVER, and both reasons are load-bearing:
            //   1. Distance. A train entering an edge at tick t arrives at t + travelTicks, so
            //      with the longest legal edge no cat can finish even the FIRST of the two
            //      edges inside this window, let alone stand in a berth. Asserted below.
            //   2. Routing. The fixture's switch sits at initialRoute 0 (J1 -> E2 -> RED) and
            //      the wave is RED, so a cat that DID arrive would be accepted — a legitimate
            //      delivery, never the pinned rejection path. Overrunning the window would
            //      break the Deliveries assertion loudly; it could not detonate the Domain.
            // What is left is exactly what the test claims to measure: EMISSION drains the
            // queue, with routing kept out of the question entirely.
            Assert.That(DrainWindowTicks, Is.LessThan(DrainTravelTicks),
                "precondition: the window is shorter than a single edge, so a cat emitted at "
                + "any tick >= 0 is still in flight when the measurement ends");

            _root = GameRoot.LaunchWith(Import(DrainFixture()));
            yield return null;
            Assert.That(_root.Preview.FaceCount, Is.EqualTo(2), "both cats still to come");

            // Emissions are at tick 2 and tick 5; 12 ticks clears both.
            _root.Session.AdvanceMs(
                DrainWindowTicks * CatMetro.Application.Session.TickInterpolator.TICK_MS);
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
            Assert.That(_root.Preview.OverflowText, Is.EqualTo(
                "+" + (_root.Preview.RemainingCats - WavePreviewStrip.MaxFaces)),
                "the hidden remainder is counted in the tail, not silently dropped");
        }

        // The validation capture showed ONE face and the read was "our cap is too low". It is
        // not: MaxFaces is 6 and has been. L001 authors a single red wave of 2 at tick 8 with
        // spacing 20, so its cats emit at tick 8 and tick 28 — and the capture was taken between
        // those, with one cat already riding (the riders counter read 1) and exactly one still
        // to come. The capsule was telling the truth about a level that genuinely had one cat
        // pending. These cases pin the whole range so that reading cannot be made again.
        [UnityTest]
        public IEnumerator FaceCount_IsTheDerivedQueue_AtOneTwoAndThree(
            [Values(1, 2, 3)] int cats)
        {
            _root = GameRoot.LaunchWith(Import(QueueFixture(cats)));
            yield return null;

            Assert.That(_root.Preview.FaceCount, Is.EqualTo(cats),
                "one face per pending cat — never a per-WAVE chip, never a cap of one");
            Assert.That(_root.Preview.RemainingCats, Is.EqualTo(cats));
            Assert.That(_root.Preview.FaceSummary,
                Is.EqualTo(string.Join("|", System.Linq.Enumerable.Repeat("red", cats))));
            Assert.That(_root.Preview.OverflowText, Is.Empty,
                "a queue that fits shows no tail");

            // The unused face slots are switched OFF, not merely drawn empty — a live but blank
            // face would still occupy its place in the row and push the group off centre.
            for (int i = cats; i < WavePreviewStrip.MaxFaces; i++)
                Assert.That(_root.Preview.Face(i).gameObject.activeSelf, Is.False,
                    "slot " + i + " is inactive");
        }

        [UnityTest]
        public IEnumerator TheFaceRow_StaysInsideTheCapsule_AtEveryQueueLength(
            [Values(1, 2, 3, 6)] int cats)
        {
            _root = GameRoot.LaunchWith(Import(QueueFixture(cats)));
            yield return null;

            _root.Preview.LayoutForViewport(PhoneSafeArea, CaptureDpi);
            var capsule = _root.Preview.CapsuleRectPx;
            float face = _root.Preview.FaceSizePx;

            // Row width measured on the INK: the leading head's left edge to the trailing
            // badge's right edge, which is wider than the nominal boxes.
            float width = (cats - 1) * WavePreviewStrip.FacePitch(face)
                + CatFaceView.InkLeftOfCentre(face) + CatFaceView.InkRightOfCentre(face);

            Assert.That(width, Is.LessThan(capsule.width),
                cats + " faces fit inside the capsule with room to spare");
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

        [UnityTest]
        public IEnumerator CounterGlyphs_AreATrophyAndAPeopleMark_NotColouredDots()
        {
            // They were two discs in two accent colours, which says "here are two counts of
            // something" and leaves the player to learn which is which. The target art names
            // them: a trophy for progress against the win condition, a crowd for cats aboard.
            _root = GameRoot.Launch();
            yield return null;

            Assert.That(_root.Preview.DeliveriesMarkSprite,
                Is.SameAs(HudShapeSprites.Trophy), "deliveries is marked by a trophy");
            Assert.That(_root.Preview.RidersMarkSprite,
                Is.SameAs(HudShapeSprites.People), "riders is marked by a group of people");

            Assert.That(_root.Preview.DeliveriesMarkSprite,
                Is.Not.SameAs(HudShapeSprites.Disc), "no longer a bare dot");
            Assert.That(_root.Preview.RidersMarkSprite,
                Is.Not.SameAs(_root.Preview.DeliveriesMarkSprite),
                "and the two counters no longer differ only by tint");
        }

        [UnityTest]
        public IEnumerator CounterGlyphs_BindPaletteTokens_AndMatchTheirNumerals()
        {
            _root = GameRoot.Launch();
            yield return null;

            // One cream for the whole row. The counters sit on the bare diorama with no card
            // behind them, so a tinted mark beside a cream numeral reads as two objects — which
            // is what the teal and orange dots did. Glyph and numeral share ONE token, and the
            // assertion is written as that equality rather than as two independent checks so it
            // keeps its meaning if the row is ever restyled to a different cream.
            Assert.That(_root.Preview.DeliveriesMarkColor, Is.EqualTo(Palette.WarmPaper));
            Assert.That(_root.Preview.RidersMarkColor, Is.EqualTo(Palette.WarmPaper));
            Assert.That(_root.Preview.DeliveriesMarkColor,
                Is.EqualTo(_root.Preview.DeliveriesTextColor),
                "the trophy and its numeral are one object");
            Assert.That(_root.Preview.RidersMarkColor,
                Is.EqualTo(_root.Preview.RidersTextColor),
                "the people mark and its numeral are one object");
        }

        [UnityTest]
        public IEnumerator CounterGlyphs_RasteriseWithRealInk_AtTheirOnScreenSize()
        {
            // A procedural glyph that silently rasterises to nothing would still pass every
            // structural assertion above and ship a blank counter row. At the pinned phone
            // frame the mark is ~41px, so read the sprites' own coverage back and require a
            // plausible amount of it — enough to be a shape, far from a filled square.
            yield return null;

            foreach (var sprite in new[] { HudShapeSprites.Trophy, HudShapeSprites.People })
            {
                var pixels = sprite.texture.GetPixels32();
                int inked = 0;
                foreach (var p in pixels) if (p.a > 128) inked++;
                float coverage = (float)inked / pixels.Length;

                // Rasterising these offline gives trophy 0.44 and people 0.66, so the bounds
                // are set to catch the two failures that matter — an empty tile and a solid
                // block — without pinning the artwork so tightly that a tweak turns them red.
                Assert.That(coverage, Is.GreaterThan(0.12f),
                    sprite.name + " draws real ink, not an empty tile");
                Assert.That(coverage, Is.LessThan(0.80f),
                    sprite.name + " is a glyph, not a filled block");
            }
        }

        // --- accessibility: colour is never the only carrier ---

        [UnityTest]
        public IEnumerator EachFace_CarriesAShape_NotJustAColour()
        {
            _root = GameRoot.LaunchWith(Import(InterleavedFixture()));
            yield return null;

            var red = _root.Preview.Face(0);
            var blue = _root.Preview.Face(1);

            Assert.That(red.ColorName, Is.EqualTo("red"));
            Assert.That(blue.ColorName, Is.EqualTo("blue"));

            // Shape — and the two shapes are the ones the BOARD already paints for these
            // lines, because both surfaces key off CatLine.ShapeOf.
            Assert.That(red.Shape, Is.EqualTo(DestinationShape.Circle));
            Assert.That(blue.Shape, Is.EqualTo(DestinationShape.Square));
            Assert.That(red.Shape, Is.Not.EqualTo(blue.Shape),
                "shape alone distinguishes the two destinations");

            // And the shape is REALISED, not merely reported. Shape is a field a bug could
            // set correctly while the badge still painted the default disc for everyone; the
            // sprites are the thing a colourblind player actually sees differ.
            Assert.That(red.BadgeSprite, Is.Not.Null);
            Assert.That(red.BadgeSprite, Is.Not.EqualTo(blue.BadgeSprite),
                "the two badges rasterise to DIFFERENT symbols, not one disc in two colours");

            // The letter is still the vocabulary's — the BOARD stamps it on station plates.
            // The HUD badge no longer paints it: at 31.7px a ~4px cream stroke inside a cream
            // ring reads as a trademark mark, not as a destination. See CatFaceView.Bind.
            Assert.That(red.Glyph, Is.EqualTo("R"));
            Assert.That(blue.Glyph, Is.EqualTo("B"));
            Assert.That(_root.Preview.GetComponentsInChildren<TMPro.TMP_Text>(true),
                Has.None.Matches<TMPro.TMP_Text>(t => t.text == "R" || t.text == "B"),
                "no letter is painted anywhere inside the capsule's faces");
        }

        // --- the badge is BESIDE the face, not printed on it ---

        [Test]
        public void Badge_ClearsTheHead_SoTheSymbolIsNeverPrintedOnTheFace()
        {
            // The defect this replaces: badge offset (0.30, -0.30) at 0.46 size against a 0.86
            // head put the badge CENTRE at 0.424 of the face box and the head EDGE at 0.43, so
            // the badge lay across the cat's chin and lost its own outline into the head fill.
            // Asserted as a pure law at the pinned phone frame, in real pixels.
            var capsule = WavePreviewStrip.CapsuleRect(PhoneSafeArea, CaptureDpi);
            float face = WavePreviewStrip.FaceSize(capsule.height);

            Assert.That(face, Is.EqualTo(85.7f).Within(0.5f),
                "precondition: the face box is ~86px on a 917x2048 phone");
            Assert.That(CatFaceView.BadgeClearance(face), Is.GreaterThan(0f),
                "the badge FILL and the head FILL do not intersect at all");

            // And the symbol is big enough to be worth separating. Below roughly 4px of
            // internal detail a rasterised triangle or star is a coloured blob.
            Assert.That(CatFaceView.BadgeDiameter(face), Is.GreaterThan(24f),
                "the badge is ~32px — every shape in the vocabulary resolves at that size");
            Assert.That(CatFaceView.HeadDiameter(face), Is.GreaterThan(
                CatFaceView.BadgeDiameter(face)),
                "the badge stays subordinate to the cat it belongs to");
        }

        [UnityTest]
        public IEnumerator Badge_IsASiblingOfTheHead_NotAChildOfIt()
        {
            // Structure, not just geometry: a badge parented UNDER the head inherits the head's
            // rect and can never be tucked outside it, however the fractions are tuned.
            _root = GameRoot.Launch();
            yield return null;

            var face = _root.Preview.Face(0);
            Assert.That(face, Is.Not.Null, "precondition: L001 has an upcoming cat");
            Assert.That(face.BadgeRect, Is.Not.Null);
            Assert.That(face.HeadRect, Is.Not.Null);

            Assert.That(face.BadgeRect.parent, Is.SameAs(face.HeadRect.parent),
                "badge and head are siblings under the face");
            Assert.That(face.BadgeRect.parent, Is.SameAs(face.FaceRect),
                "and that shared parent is the face itself");

            // The laid-out rects agree with the law: centres far enough apart to clear.
            float gap = Vector2.Distance(face.BadgeRect.anchoredPosition,
                            face.HeadRect.anchoredPosition)
                        - (face.BadgeRect.sizeDelta.x + face.HeadRect.sizeDelta.x) * 0.5f;
            Assert.That(gap, Is.GreaterThan(0f),
                "as laid out, not merely as computed, the badge clears the head");
        }

        [Test]
        public void AFacesBadge_NeverReachesItsNeighboursHead()
        {
            // The badge hangs outside the face BOX, so row pitch has to clear the ink, not the
            // box. Pitch is faceSize * 1.28; the badge reaches 0.625 of faceSize to the right
            // and the next head starts 1.28 - 0.38 = 0.90 along.
            var capsule = WavePreviewStrip.CapsuleRect(PhoneSafeArea, CaptureDpi);
            float face = WavePreviewStrip.FaceSize(capsule.height);

            float badgeEdge = CatFaceView.InkRightOfCentre(face);
            float neighbourHeadEdge =
                WavePreviewStrip.FacePitch(face) - CatFaceView.InkLeftOfCentre(face);

            Assert.That(badgeEdge, Is.LessThan(neighbourHeadEdge),
                "a cat's badge stops short of the next cat's head");
            Assert.That(neighbourHeadEdge - badgeEdge, Is.GreaterThan(8f),
                "and leaves visible daylight, not a hairline");
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
        public IEnumerator EverySurfaceCarryingLineIdentity_IsExactlyCatLineColorOf()
        {
            // Not "close to" and not "a tint of". The board lane sweeps for equality with
            // CatLine.ColorOf(name), so any darkened or alpha'd variant here — the
            // Palette.WithAlpha(...) idiom is one edit away — turns the MERGED branch red on
            // an otherwise correct implementation. The ears briefly were such a variant.
            // Every surface that says "this cat belongs to this line" is the token itself.
            _root = GameRoot.LaunchWith(Import(InterleavedFixture()));
            yield return null;

            foreach (var name in new[] { "red", "blue" })
            {
                var face = name == "red" ? _root.Preview.Face(0) : _root.Preview.Face(1);
                var token = CatLine.ColorOf(name);
                Assert.That(face.ColorName, Is.EqualTo(name), "precondition");
                Assert.That(face.HeadColor, Is.EqualTo(token), name + " head");
                Assert.That(face.EarColor, Is.EqualTo(token), name + " ears — no tinted variant");
                Assert.That(face.BadgeColor, Is.EqualTo(token), name + " badge");
            }
        }

        [UnityTest]
        public IEnumerator WildCatFaceBadge_RasterisesAStar_WithoutTouchingTheMeshExtruder()
        {
            // DestinationShapeMesh.ForShape(Star) THROWS by design — a star is concave and the
            // extruder fans from vertex 0. The HUD must therefore never reach for the 3D
            // realiser: its badges come from HudShapeSprites, which has no such constraint.
            // feat/level-variety ships wild cats in L019, so this is live content.
            yield return null;
            Assert.That(CatLine.ShapeOf("wild"), Is.EqualTo(DestinationShape.Star));
            var sprite = HudShapeSprites.ForShape(DestinationShape.Star);
            Assert.That(sprite, Is.Not.Null, "the wild badge rasterises rather than throwing");
            Assert.That(sprite, Is.Not.EqualTo(HudShapeSprites.Disc),
                "Star must not silently fall through to the default disc");
            Assert.That(sprite.texture.width, Is.GreaterThan(0));
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

        // Cats that emit almost immediately but ride for as long as the schema permits, so the
        // queue can be observed draining without any cat arriving anywhere. The ride is
        // ContentBounds.TRAVEL_TICKS_MAX, not an arbitrary large number: the importer rejects
        // travelTicks outside [1,40] with a BoundViolation and the fixture never loads.
        private static string DrainFixture() => FixtureJson(@"[
    { ""tick"": 2, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 2, ""spacingTicks"": 3 } ]",
            travelTicks: DrainTravelTicks);

        // Exactly `cats` red cats still to come at tick 0, for the 1/2/3 queue-length cases.
        // Count is bounded [1,8] by ContentBounds.WAVE_COUNT_MAX and spacing [1,40], so this
        // stays inside the importer for every value the tests ask for; the guard below fails
        // loudly rather than letting a widened [Values] list import as something else.
        private static string QueueFixture(int cats)
        {
            Assert.That(cats, Is.InRange(ContentBounds.WAVE_COUNT_MIN,
                ContentBounds.WAVE_COUNT_MAX), "fixture must stay inside the importer's bounds");
            return FixtureJson(@"[
    { ""tick"": 5, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": " + cats
                + @", ""spacingTicks"": 10 } ]", travelTicks: DrainTravelTicks);
        }

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
