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

        [UnityTest]
        public IEnumerator Typography_OverflowUsesPhoneDpi_AndFacesUseShapeBadges()
        {
            _root = GameRoot.Launch();
            yield return null;
            var labels = _root.Preview.GetComponentsInChildren<TMPro.TMP_Text>(true);
            foreach (var label in labels)
                if (label.name == "Overflow") label.text = "+3";
            _root.Preview.LayoutForViewport(PhoneSafeArea, CaptureDpi);
            Canvas.ForceUpdateCanvases();
            float minimum = TypeScale.Minimum * HudBands.PxPerDp(CaptureDpi);
            int overflowLabels = 0;
            foreach (var label in labels)
            {
                Assert.That(label.name, Is.Not.EqualTo("cat-token"),
                    "destination shapes replace the temporary token letters");
                if (label.name == "Overflow")
                {
                    overflowLabels++;
                    Assert.That(label.fontSizeMin, Is.GreaterThanOrEqualTo(minimum), label.name);
                    Assert.That(label.fontSizeMax, Is.GreaterThanOrEqualTo(minimum), label.name);
                }
            }
            Assert.That(overflowLabels, Is.EqualTo(1), "exercise the real overflow label");
            Assert.That(_root.Preview.FaceCount, Is.GreaterThan(0), "L001 must show a queued cat");
            for (int i = 0; i < _root.Preview.FaceCount; i++)
            {
                var face = _root.Preview.Face(i);
                Assert.That(face.gameObject.activeInHierarchy, Is.True);
                Assert.That(face.Shape, Is.EqualTo(DestinationShape.Circle), "L001's red destination");
                Assert.That(face.BadgeSprite, Is.SameAs(HudShapeSprites.Disc));
                Assert.That(face.BadgeRect.rect.width, Is.GreaterThan(0f));
                Assert.That(face.GetComponentsInChildren<TMPro.TMP_Text>(true), Is.Empty);
            }
            string dir = System.Environment.GetEnvironmentVariable("CM_TYPE_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) yield break;
            // A HUD-only proof uses no licensed models or board. Full shipped-scene captures
            // remain in the main checkout, where the local art is installed.
            var target = new RenderTexture(917, 2048, 24);
            var camera = _root.Cam;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                foreach (var child in _root.Preview.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = 31;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Palette.InkNavy;
                camera.targetTexture = target;
                camera.aspect = 917f / 2048f;
                _root.Preview.LayoutForViewport(PhoneSafeArea, CaptureDpi);
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(917, 448, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 1600, 917, 448), 0, 0);
                pixels.Apply();
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "type-hud-only.png"),
                    pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                Object.Destroy(target);
                if (pixels != null) Object.Destroy(pixels);
            }
        }

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
        public void CounterRow_FitsInsideTheCapsulesRightThird_OnTheSameRow()
        {
            var capsule = WavePreviewStrip.CapsuleRect(PhoneSafeArea, CaptureDpi);
            var counters = WavePreviewStrip.CounterRowRect(PhoneSafeArea, CaptureDpi);

            Assert.That(counters.yMin, Is.GreaterThanOrEqualTo(capsule.yMin));
            Assert.That(counters.yMax, Is.LessThanOrEqualTo(capsule.yMax));
            Assert.That(counters.center.y, Is.EqualTo(capsule.center.y).Within(0.01f));
            Assert.That(counters.xMin, Is.GreaterThanOrEqualTo(capsule.x + capsule.width * 2f / 3f),
                "rank 30 reserves the right third without a second row below the capsule");
            Assert.That(counters.xMax, Is.LessThan(capsule.xMax));
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
        public IEnumerator Faces_MatchTwoRedPreviewFixture_OneFacePerCat()
        {
            _root = GameRoot.LaunchWith(Import(TwoRedPreviewFixture()));
            yield return null;

            // The OLD strip could only say "red x2" — one chip for the whole wave. The capsule
            // shows the cats themselves, which is the whole point of the redesign.
            Assert.That(_root.Preview.FaceCount, Is.EqualTo(2),
                "the fixture's single red wave of 2 is TWO faces, not one chip");
            Assert.That(_root.Preview.FaceSummary, Is.EqualTo("red|red"));
            Assert.That(_root.Preview.RemainingCats, Is.EqualTo(2));
            yield return CaptureHud("capsule-two-upcoming");
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
        public IEnumerator EmittedQueue_BecomesASlimProgressChip_AndChecksOnlyDeliveredCats()
        {
            _root = GameRoot.LaunchWith(Import(DrainFixture()));
            yield return null;
            _root.enabled = false;
            _root.Preview.LayoutForViewport(PhoneSafeArea, CaptureDpi);
            float fullHeight = _root.Preview.CapsuleRectPx.height;
            _root.Session.AdvanceMs(DrainWindowTicks
                * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            _root.Preview.Refresh();
            Assert.That(_root.Preview.FaceCount, Is.Zero);
            Assert.That(_root.Preview.CapsuleRectPx.height, Is.LessThan(fullHeight * 0.8f),
                "after the final emission the capsule collapses into a slim progress chip");
            var status = _root.Preview.transform.Find("Hud/Faces/TailStatusMark");
            Assert.That(status, Is.Not.Null, "a travelling group mark prevents a blank pill before delivery");
            Assert.That(status.gameObject.activeSelf, Is.True);
            Assert.That(status.GetComponent<Image>().sprite, Is.SameAs(HudShapeSprites.People),
                "emission alone never claims that a cat was delivered");
            yield return CaptureHud("capsule-in-flight");

            while (_root.Session.State.Deliveries == 0 && _root.Session.State.Tick < 100)
                _root.Session.AdvanceMs(CatMetro.Application.Session.TickInterpolator.TICK_MS);
            _root.Preview.Refresh();
            Assert.That(_root.Session.State.Deliveries, Is.GreaterThan(0), "one cat has arrived");
            Assert.That(_root.Preview.RidersText, Is.Not.EqualTo("0"), "another cat is still travelling");
            Assert.That(_root.Preview.Face(0).gameObject.activeSelf, Is.True,
                "the slim chip retains delivered faces while the final train travels");
            Assert.That(_root.Preview.Face(0).HeadColor,
                Is.EqualTo(Palette.WithAlpha(Palette.InkNavy, 0.35f)));
            Assert.That(status.GetComponent<Image>().sprite.name, Is.EqualTo("HudCheck"));
            yield return CaptureHud("capsule-delivered-tail");
        }

        [UnityTest]
        public IEnumerator LeverGlyph_ReplacesTheFlipsWord_AndKeepsAcceptedTapCounts()
        {
            _root = GameRoot.LaunchWith(Import(TwoRedPreviewFixture()));
            yield return null;
            Assert.That(_root.Preview.FlipSummary, Is.EqualTo("0/1"));
            var lever = _root.Preview.transform.Find("Hud/Counters/FlipMark");
            Assert.That(lever, Is.Not.Null);
            Assert.That(lever.GetComponent<Image>().sprite.name, Is.EqualTo("HudLever"));
            Assert.That(_root.Session.EnqueueToggle(0), Is.True);
            _root.Preview.Refresh();
            Assert.That(_root.Preview.FlipSummary, Is.EqualTo("1/1"));
            Assert.That(lever.GetComponent<Image>().color, Is.EqualTo(Palette.TabbyYellow),
                "using the perfect budget is yellow; only exceeding it is red");
        }

        [UnityTest]
        public IEnumerator PositiveFlipBudget_HasVisibleInkAgainstItsCapsule()
        {
            _root = GameRoot.LaunchWith(Import(TwoRedPreviewFixture()));
            _root.enabled = false;
            yield return null;
            _root.Preview.enabled = false;
            foreach (var renderer in _root.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            foreach (var canvas in _root.GetComponentsInChildren<Canvas>(true))
                if (canvas != _root.Preview.GetComponent<Canvas>()) canvas.enabled = false;
            var camera = _root.Cam;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = new RenderTexture(917, 2048, 24) { antiAliasing = 4 };
            var pixels = new Texture2D(917, 2048, TextureFormat.RGB24, false);
            var mark = _root.Preview.transform.Find("Hud/Counters/FlipMark").GetComponent<Image>();
            var label = _root.Preview.transform.Find("Hud/Counters/flip-budget").GetComponent<TMPro.TMP_Text>();
            try
            {
                camera.targetTexture = target;
                yield return null;
                _root.Preview.LayoutForViewport(PhoneSafeArea, CaptureDpi);
                Canvas.ForceUpdateCanvases();
                Assert.That(mark.color, Is.EqualTo(Palette.WarmPaper));
                Assert.That(label.color, Is.EqualTo(Palette.WarmPaper));
                Color[] Read()
                {
                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 917, 2048), 0, 0);
                    pixels.Apply();
                    return pixels.GetPixels();
                }
                Color[] painted = Read();
                string dir = System.Environment.GetEnvironmentVariable("CM_UI_CAPTURE_DIR");
                if (!string.IsNullOrEmpty(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "capsule-positive-flips.png"),
                        pixels.EncodeToPNG());
                }
                mark.enabled = false;
                label.enabled = false;
                Canvas.ForceUpdateCanvases();
                Color[] backing = Read();
                foreach (var graphic in new Graphic[] { mark, label })
                {
                    var corners = new Vector3[4];
                    graphic.rectTransform.GetWorldCorners(corners);
                    Vector3 min = camera.WorldToScreenPoint(corners[0]);
                    Vector3 max = camera.WorldToScreenPoint(corners[2]);
                    int visibleInk = 0;
                    for (int y = Mathf.Max(0, Mathf.CeilToInt(min.y)); y < Mathf.Min(2048, max.y); y++)
                        for (int x = Mathf.Max(0, Mathf.CeilToInt(min.x)); x < Mathf.Min(917, max.x); x++)
                        {
                            int index = y * 917 + x;
                            if (Mathf.Abs(painted[index].grayscale - backing[index].grayscale) > 0.25f)
                                visibleInk++;
                        }
                    Assert.That(visibleInk, Is.GreaterThan(30),
                        graphic.name + " must paint contrasting strokes, not cream on cream");
                }
            }
            finally
            {
                mark.enabled = true;
                label.enabled = true;
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(pixels);
                target.Release();
                Object.DestroyImmediate(target);
            }
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
            _root.Preview.LayoutForViewport(PhoneSafeArea, CaptureDpi);
            Assert.That(CatFaceView.BadgeDiameter(_root.Preview.FaceSizePx), Is.GreaterThan(24f),
                "the actual overflow layout must preserve the existing readable badge floor");
            yield return CaptureHud("capsule-overflow");
        }

        // The capsule still shows each of one, two and three pending cats individually;
        // rank 30's readable four-face limit never turns those small queues into wave chips.
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
            [Values(1, 2, 3, WavePreviewStrip.MaxFaces)] int cats)
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
            var faceRow = (RectTransform)_root.Preview.Face(0).transform.parent;
            float origin = faceRow.anchoredPosition.x + faceRow.rect.width * 0.5f;
            float right = origin + _root.Preview.Face(cats - 1).FaceRect.anchoredPosition.x
                + CatFaceView.InkRightOfCentre(face);
            float left = origin + _root.Preview.Face(0).FaceRect.anchoredPosition.x
                - CatFaceView.InkLeftOfCentre(face);
            Assert.That(left, Is.GreaterThan(capsule.xMin));
            Assert.That(right, Is.LessThan(_root.Preview.CounterRowRectPx.xMin - 8f),
                "the last badge leaves clear space before the first counter");
            Assert.That(WavePreviewStrip.FacePitch(face) - CatFaceView.InkRightOfCentre(face)
                - CatFaceView.InkLeftOfCentre(face), Is.GreaterThan(8f),
                "the compressed queue still separates each destination badge from the next head");
            Assert.That(CatFaceView.BadgeDiameter(face), Is.GreaterThan(24f),
                "the actual displayed badge keeps its readability floor at every queue length");
        }

        [UnityTest]
        public IEnumerator MixedShapeOverflow_PreservesFourReadableDestinationSymbols() =>
            MixedShapeOverflow(false);

        [UnityTest]
        public IEnumerator NavigationPin_PreservesFourReadableDestinationSymbolsAndOverflow() =>
            MixedShapeOverflow(true);

        private IEnumerator MixedShapeOverflow(bool navigation)
        {
            string fixture = FixtureJson(@"[
    { ""tick"": 5, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 2, ""spacingTicks"": 20 },
    { ""tick"": 6, ""sourceNode"": ""SRC"", ""color"": ""blue"", ""count"": 2, ""spacingTicks"": 20 },
    { ""tick"": 7, ""sourceNode"": ""SRC"", ""color"": ""yellow"", ""count"": 2, ""spacingTicks"": 20 },
    { ""tick"": 8, ""sourceNode"": ""SRC"", ""color"": ""green"", ""count"": 2, ""spacingTicks"": 20 } ]")
                .Replace("\"allowedColors\": [\"red\", \"blue\"]",
                    "\"allowedColors\": [\"red\", \"blue\", \"yellow\", \"green\"]")
                .Replace("\"accepts\": [\"red\"]", "\"accepts\": [\"red\", \"yellow\"]")
                .Replace("\"accepts\": [\"blue\"]", "\"accepts\": [\"blue\", \"green\"]");
            _root = GameRoot.LaunchWith(Import(fixture));
            yield return null;
            if (navigation) _root.Preview.ReserveNavigationSpace();
            _root.Preview.LayoutForViewport(PhoneSafeArea, CaptureDpi);
            Assert.That(_root.Preview.FaceSummary, Is.EqualTo("red|blue|yellow|green"));
            Assert.That(_root.Preview.OverflowText, Is.EqualTo("+4"));
            var symbols = new[] { HudShapeSprites.Disc, HudShapeSprites.RoundedSquare,
                HudShapeSprites.Triangle, HudShapeSprites.Diamond };
            for (int i = 0; i < symbols.Length; i++)
            {
                Assert.That(_root.Preview.Face(i).BadgeSprite, Is.SameAs(symbols[i]));
                Assert.That(_root.Preview.Face(i).BadgeRect.sizeDelta.x, Is.GreaterThan(24f));
            }
            yield return CaptureHud(navigation ? "capsule-navigation-mixed-overflow" : "capsule-mixed-shape-overflow");
        }

        // --- counters ---

        [UnityTest]
        public IEnumerator Counters_ReadDeliveriesAgainstTheLevelsWinTarget()
        {
            _root = GameRoot.LaunchWith(Import(TwoRedPreviewFixture()));
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

            // Rank 30 puts the whole row on cream, so both marks and numbers use navy.
            Assert.That(_root.Preview.DeliveriesMarkColor, Is.EqualTo(Palette.InkNavy));
            Assert.That(_root.Preview.RidersMarkColor, Is.EqualTo(Palette.InkNavy));
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
            // Sample at the real compact counter size, so a full-resolution sprite cannot
            // pass while its on-phone strokes disappear after shrinking into the capsule.
            _root = GameRoot.Launch();
            yield return null;
            _root.Preview.LayoutForViewport(PhoneSafeArea, CaptureDpi);

            foreach (string name in new[] { "DeliveredMark", "RidersMark", "FlipMark" })
            {
                var mark = _root.Preview.transform.Find("Hud/Counters/" + name).GetComponent<Image>();
                var sprite = mark.sprite;
                int size = Mathf.FloorToInt(mark.rectTransform.sizeDelta.x);
                Assert.That(size, Is.GreaterThanOrEqualTo(24), name + " keeps a readable pixel footprint");
                int inked = 0;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        if (sprite.texture.GetPixelBilinear((x + 0.5f) / size, (y + 0.5f) / size).a > 0.5f)
                            inked++;
                float coverage = (float)inked / (size * size);

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
            _root = GameRoot.LaunchWith(Import(TwoRedPreviewFixture()));
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

        private IEnumerator CaptureHud(string name)
        {
            string dir = System.Environment.GetEnvironmentVariable("CM_UI_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) yield break;
            // This isolates UGUI on a plain field. It needs no licensed board art and does
            // not substitute for the main-checkout furnished composition captures.
            _root.enabled = false;
            _root.Preview.enabled = false;
            foreach (var renderer in _root.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            foreach (var canvas in _root.GetComponentsInChildren<Canvas>(true))
                if (canvas != _root.Preview.GetComponent<Canvas>()) canvas.enabled = false;
            var camera = _root.Cam;
            var previous = camera.targetTexture;
            var target = new RenderTexture(917, 2048, 24) { antiAliasing = 4 };
            target.Create();
            camera.targetTexture = target;
            Texture2D pixels = null;
            var active = RenderTexture.active;
            try
            {
                yield return null;
                _root.Preview.LayoutForViewport(PhoneSafeArea, CaptureDpi);
                Canvas.ForceUpdateCanvases();
                yield return null;
                RenderTexture.active = target;
                pixels = new Texture2D(917, 2048, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 917, 2048), 0, 0);
                pixels.Apply();
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = active;
                camera.targetTexture = previous;
                if (pixels != null) Object.Destroy(pixels);
                target.Release();
                Object.Destroy(target);
            }
        }

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

        private static string TwoRedPreviewFixture() => FixtureJson(@"[
    { ""tick"": 8, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 2,
      ""spacingTicks"": 20 } ]", winDeliveries: 2);

        // More cats than the capsule can show, to exercise the "+N" tail.
        private static string FloodFixture() => FixtureJson(@"[
    { ""tick"": 8, ""sourceNode"": ""SRC"", ""color"": ""red"", ""count"": 6, ""spacingTicks"": 4 },
    { ""tick"": 40, ""sourceNode"": ""SRC"", ""color"": ""blue"", ""count"": 6, ""spacingTicks"": 4 } ]");

        // The FailureTests board shape (SRC -> J1 -> RED|BLU), with the waves swapped in.
        private static string FixtureJson(
            string waves, int travelTicks = 12, int winDeliveries = 4)
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
  ""win"": { ""deliveries"": " + winDeliveries + @", ""timeLimitTicks"": 4000,
    ""perfectMaxSwitches"": 1,
    ""stars"": { ""two"": 200, ""three"": 300 } },
  ""economy"": { ""baseTickets"": 20, ""perfectBonus"": 10 }
}";
        }
    }
}
