using System.Collections;
using System.IO;
using System.Linq;
using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Cats;
using CatMetro.Presentation.Theme;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    // LOOK step 6: a train renders as a toy consist — engine + open carriage + seated cat —
    // riding the SAME spline the physical track was built from. These are the scene-level
    // laws; the pure edge-boundary math is pinned in TrainConsistLayoutTests (EditMode).
    public sealed class TrainConsistTests
    {
        private const float CarriageOffset = ToyTrainView.CarriageOffset;
        private static readonly Vector3 HeadLift = new Vector3(0f, 0f, -0.2f);

        private GameObject _host;
        private BoardView _view;
        private GameSession _session;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            _view = null;
            _host = null;
            _session = null;
        }

        [UnityTest]
        public IEnumerator Train_RendersEngineCarriageAndCat_AllDecorationInventoryFree()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 1, progressTicks: 6);
            _view.UpdateFrom(_session);

            var train = TrainRoot();
            var engine = train.transform.Find("Engine");
            var carriage = train.transform.Find("Carriage");
            Assert.That(engine, Is.Not.Null, "the consist leads with an engine");
            Assert.That(carriage, Is.Not.Null, "a carriage trails the engine");
            Assert.That(carriage.Find("Cat"), Is.Not.Null,
                "the occupied carriage seats a cat — the concept art's whole point");

            foreach (var filter in train.GetComponentsInChildren<MeshFilter>(true))
                Assert.That(filter.sharedMesh, Is.Not.Null,
                    filter.name + " must resolve its builtin mesh (a null here renders nothing)");
            Assert.That(train.GetComponentsInChildren<BoardElementId>(true).Length, Is.EqualTo(1),
                "only the train ROOT is authored inventory; every part is decoration");
            Assert.That(train.GetComponentsInChildren<Collider>(true), Is.Empty,
                "visual-only consist parts must never intercept switch taps");
        }

        [UnityTest]
        public IEnumerator CatHead_SeatedInTheCarriage_ChibiProportions()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 1, progressTicks: 6);
            _view.UpdateFrom(_session);

            // First-render verdict (2026-08-25): a head near the box's full width reads as
            // a ball ON the carriage. Target-02's cats are 60-70% of the box width with the
            // lower third hidden by the brim — pin the LAW, not the current magic numbers.
            // r3: these were computed from localScale, which is NOT a size — it is a size only
            // once multiplied by the mesh's own bounds. Against pSphere1 (~3.33 units across)
            // the head scored 0.679 here while RENDERING at 2.26x the box's width. Both laws
            // now measure the size the part actually occupies in the world.
            var head = TrainRoot().transform.Find("Carriage/Cat/Head");
            var body = TrainRoot().transform.Find("Carriage/Body");
            Vector3 headSize = RenderedWorldSize(head);
            Vector3 bodySize = RenderedWorldSize(body);

            Assert.That(headSize.x, Is.EqualTo(ToyTrainView.HeadDiameter).Within(0.002f),
                "the head must RENDER at the diameter its constant names — builtin meshes are " +
                "not unit-sized, and assuming they are is what buried the whole face");

            float widthRatio = headSize.x / bodySize.y; // lateral axis is y
            Assert.That(widthRatio, Is.InRange(0.55f, 0.75f),
                "the chibi head stays clearly narrower than the open box it sits in");

            float bodyTop = body.localPosition.z - bodySize.z * 0.5f; // -z is up
            float headBottom = head.localPosition.z + headSize.z * 0.5f;
            float submerged = (headBottom - bodyTop) / headSize.z;
            Assert.That(submerged, Is.InRange(0.25f, 0.5f),
                "the brim hides roughly the lower third of the head — seated IN, not ON");
        }

        // ── The invisible-ears regression, pinned ───────────────────────────────────────
        // 2026-08-25: two consecutive validated renders showed a smooth coloured ball in a
        // wagon. The ears WERE outside the head sphere — 0.023 above the crown — so every
        // world-space check passed. What failed was the only thing that matters: under the
        // diorama tilt they projected at most 2.3 px outside the head's on-screen silhouette,
        // and on the far side 3.4 px INSIDE it. So the laws below are stated in the projection
        // the camera actually performs, not in board-space clearance, and they are checked at
        // several headings because heading is what buried the far ear.

        // World units of on-screen clearance beyond the head's silhouette. At the fitted
        // gameplay zoom the board renders ~93 px per board unit (measured off the 917x2048
        // capture: a 0.90 sleeper spans 72 px, sleeper pitch 0.42 spans 32.7 px), so a
        // 17.7 px head needs an ear standing at least ~4.7 px clear to read as an ear.
        private const float MinEarSilhouetteClearance = 0.05f;

        // Board z of the switch-disc slab's mid-plane. Ears must stay UNDER it: a cat passes
        // beneath a signal, it does not grow through one. This is the ceiling that makes
        // "just make the ears taller" the wrong fix, and why the ears win on lateral spread.
        private const float SwitchDiscMidPlaneZ = -0.40f;

        // THE law the first 27 tests were all missing. Every one of them computed in authored
        // space — localScale, local offsets, my own arithmetic — and authored space did not
        // match the rendered hierarchy, so they passed unanimously while the render showed a
        // bare ball. This one measures the shipped GameObjects: each feature's real mesh
        // bounds, through its real world transform, against the head's real rendered radius.
        // It fails loudly on any mesh-scale surprise, in either direction, forever.
        [UnityTest]
        public IEnumerator CatFeatures_ProtrudeOutsideTheRenderedHead_NotJustTheAuthoredOne()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 1, progressTicks: 6);
            _view.UpdateFrom(_session);

            var cat = TrainRoot().transform.Find("Carriage/Cat");
            var head = cat.Find("Head");
            float headRadius = RenderedWorldSize(head).x * 0.5f;

            // Ears carry the silhouette, so they must stand well proud; the eyes and muzzle are
            // deliberately shallow domes and only have to break the surface at all.
            var required = new (string Name, float Margin)[]
            {
                ("EarLeft", 0.05f), ("EarRight", 0.05f),
                ("EyeLeft", 0.004f), ("EyeRight", 0.004f), ("Muzzle", 0.004f),
            };
            foreach (var (name, margin) in required)
            {
                var feature = cat.Find(name);
                float farthest = 0f;
                foreach (Vector3 corner in MeshBoundsCorners(feature))
                    farthest = Mathf.Max(farthest,
                        Vector3.Distance(feature.TransformPoint(corner), head.position));
                Assert.That(farthest, Is.GreaterThan(headRadius + margin),
                    $"{name} must protrude outside the head AS RENDERED (farthest {farthest:F4} " +
                    $"vs head radius {headRadius:F4}) — on 2026-08-25 every feature was correct " +
                    "and correctly placed, and all of them were sealed inside an oversized head");
            }
        }

        [UnityTest]
        public IEnumerator CatEars_StandClearOfTheHeadSilhouette_AtEveryHeading()
        {
            yield return BuildBoard();
            SeatFirstCat();
            foreach (int edge in new[] { 0, 1, 2 }) // three distinct authored headings
            {
                PlaceOnEdge(edge: edge, progressTicks: 4);
                _view.UpdateFrom(_session);
                var cat = TrainRoot().transform.Find("Carriage/Cat");
                var head = cat.Find("Head");
                foreach (string ear in new[] { "EarLeft", "EarRight" })
                {
                    float clearance = EarSilhouetteClearance(head, cat.Find(ear));
                    Assert.That(clearance, Is.GreaterThan(MinEarSilhouetteClearance),
                        $"on edge {edge}, {ear} must break the head's PROJECTED silhouette — " +
                        "a world-space bump that the camera sees end-on is an invisible ear");
                }
            }
        }

        [UnityTest]
        public IEnumerator CatEars_AreChunky_AndStayUnderTheSwitchDiscs()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 1, progressTicks: 6);
            _view.UpdateFrom(_session);

            var cat = TrainRoot().transform.Find("Carriage/Cat");
            var head = cat.Find("Head");
            var ear = cat.Find("EarLeft");
            // Rendered, not authored: ear and head carry DIFFERENT builtin meshes (cube vs
            // sphere), so a localScale ratio here compared two incommensurate numbers.
            float earToHead = RenderedWorldSize(ear).y / RenderedWorldSize(head).x;
            Assert.That(earToHead, Is.InRange(0.5f, 0.8f),
                "target-02's ears are a large fraction of the head — a token nub cannot read " +
                "at a 17.7 px head no matter where it is placed");

            float tipZ = EarExtremeBoardZ(ear);
            Assert.That(tipZ, Is.GreaterThan(SwitchDiscMidPlaneZ + 0.01f),
                "ears must stay under the switch-disc mid-plane — legibility comes from " +
                "lateral spread, NOT from growing up through the signals");
        }

        [UnityTest]
        public IEnumerator SeatedCat_HoldsAFixedCameraFacingYaw_WhateverTheTrackDoes()
        {
            yield return BuildBoard();
            SeatFirstCat();
            Vector3 toCamera = -(Quaternion.Inverse(BoardSceneLook.BoardTilt) * Vector3.forward);
            float bestPossible = new Vector2(toCamera.x, toCamera.y).magnitude;

            foreach (int edge in new[] { 0, 1, 2 })
            {
                PlaceOnEdge(edge: edge, progressTicks: 4);
                _view.UpdateFrom(_session);
                var cat = TrainRoot().transform.Find("Carriage/Cat");
                // Vehicles are modelled along +x, so the cat's own +x is the way it looks.
                Vector3 facing = _view.transform.InverseTransformDirection(cat.right);
                float yaw = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
                Assert.That(Mathf.DeltaAngle(yaw, ToyTrainView.CatBoardYaw), Is.EqualTo(0f)
                        .Within(0.01f),
                    $"on edge {edge} the cat must hold the SAME board yaw — a cat that turns " +
                    "with the carriage swings one ear behind the head at some headings");
                Assert.That(Vector3.Dot(facing.normalized, toCamera.normalized),
                    Is.GreaterThan(bestPossible * 0.99f),
                    "and that yaw must be the one that squares the face to the camera");
            }
        }

        [UnityTest]
        public IEnumerator SeatedCat_HasAFace_ThatNeverTakesTheLineTint()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 1, progressTicks: 6, color: CatColor.Red);
            _view.UpdateFrom(_session);

            var cat = TrainRoot().transform.Find("Carriage/Cat");
            foreach (string feature in new[] { "EyeLeft", "EyeRight", "Muzzle" })
            {
                var part = cat.Find(feature);
                Assert.That(part, Is.Not.Null,
                    $"the cat needs {feature} — at a 17.7 px head the face is what reads, " +
                    "and target-02's cats are eyes-and-muzzle first");
                Assert.That(part.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null,
                    feature + " must resolve its builtin mesh");
            }

            // The face is deliberately OUTSIDE the tinted renderer set: a red cat with red
            // eyes is a red ball again. Contrast is the whole point of adding it.
            var eye = cat.Find("EyeLeft").GetComponent<MeshRenderer>();
            AssertCatColor(eye.sharedMaterial.color, Palette.InkNavy,
                "eyes stay ink-navy whatever colour the cat is");
            AssertCatColor(cat.Find("Muzzle").GetComponent<MeshRenderer>().sharedMaterial.color,
                Palette.CreamCard, "the muzzle stays cream whatever colour the cat is");

            // The mutant this kills: adding the face to _catRenderers. Then SyncSlot's block
            // would stamp the line colour onto the eyes and the cat is a plain ball again.
            // An unset block reads back (0,0,0,0), which is nowhere near any line colour.
            var block = new MaterialPropertyBlock();
            eye.GetPropertyBlock(block);
            Assert.That(Vector4.Distance(block.GetColor("_BaseColor"), CatHeadColor()),
                Is.GreaterThan(0.1f),
                "no per-slot tint may reach the eyes — SyncSlot tints head and ears only");

            Assert.That(Luminance(CatHeadColor()) - Luminance(Palette.InkNavy),
                Is.GreaterThan(0.2f),
                "and the eyes must stay far darker than the head they sit on, or the face " +
                "stops reading at gameplay zoom");
        }

        // The projection the diorama camera performs: an orthographic, identity-rotated camera
        // looks along world +z, so in BOARD-local space it looks along the tilt's inverse of
        // that. A point's on-screen distance from the head's centre is the length of its
        // offset with the view component removed; the head's silhouette is a circle of the
        // head's own radius. Anything whose offset projects shorter than that radius is inside
        // the head's disc on screen, however far out of the sphere it sticks in world space.
        private float EarSilhouetteClearance(Transform head, Transform ear)
        {
            Vector3 view = (Quaternion.Inverse(BoardSceneLook.BoardTilt) * Vector3.forward)
                .normalized;
            Vector3 centre = _view.transform.InverseTransformPoint(head.position);
            float radius = RenderedWorldSize(head).x * 0.5f;
            float best = float.NegativeInfinity;
            foreach (Vector3 corner in MeshBoundsCorners(ear))
            {
                Vector3 offset =
                    _view.transform.InverseTransformPoint(ear.TransformPoint(corner)) - centre;
                Vector3 onScreen = offset - Vector3.Dot(offset, view) * view;
                best = Mathf.Max(best, onScreen.magnitude - radius);
            }
            return best;
        }

        // Board-local z of the ear's highest corner (-z is up out of the table).
        private float EarExtremeBoardZ(Transform ear)
        {
            float top = float.PositiveInfinity;
            foreach (Vector3 corner in MeshBoundsCorners(ear))
                top = Mathf.Min(top,
                    _view.transform.InverseTransformPoint(ear.TransformPoint(corner)).z);
            return top;
        }

        // r3: these helpers used to hardcode a +/-0.5 unit cube, making exactly the assumption
        // that the production code was being punished for. Corners now come from the mesh the
        // part actually carries, so the tests cannot repeat the bug they exist to catch.
        private static Vector3[] MeshBoundsCorners(Transform part)
        {
            Bounds b = part.GetComponent<MeshFilter>().sharedMesh.bounds;
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = b.center + Vector3.Scale(b.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f,
                    (i & 2) == 0 ? -1f : 1f,
                    (i & 4) == 0 ? -1f : 1f));
            return corners;
        }

        // The size a part actually occupies in the world: its mesh's intrinsic size scaled by
        // the transform chain. This, never localScale, is what a proportion law must measure.
        private static Vector3 RenderedWorldSize(Transform part)
        {
            Vector3 mesh = part.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            Vector3 scale = part.lossyScale;
            return new Vector3(mesh.x * Mathf.Abs(scale.x),
                mesh.y * Mathf.Abs(scale.y),
                mesh.z * Mathf.Abs(scale.z));
        }

        private static float Luminance(Color c) =>
            0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        // ── The destination pin ─────────────────────────────────────────────────────────────
        // target-01's chief readability device: a white card above each riding cat carrying
        // that cat's destination symbol. Colour says which LINE a cat belongs to; only the pin
        // says where it is GOING, and for a red/green viewer the pin says it alone. Every law
        // below is stated in the projection the camera performs or in the rendered hierarchy,
        // for the reason the ear tests carry in their own header: 27 authored-space tests once
        // passed unanimously while the render showed a bare ball.

        // Board units to screen pixels at the fitted gameplay zoom. BoardSceneLook.FitCamera is
        // orthographic and floors orthographicSize at 7, so px per board unit is (height/2) /
        // size: an L001-class level on a 917x2048 capture renders 1024/7 = 146, and a level big
        // enough to need size ~11 renders ~93. 93 is the honest WORST case and the figure the
        // ear work measured, so every readability law here is stated against it.
        private const float GameplayPixelsPerBoardUnit = 93f;

        // The lowest board z any switch furniture reaches. The disc is a cylinder centred at
        // -0.4, scaled 0.08 on its 2-unit axis, so the slab runs -0.48..-0.32; on onboarding
        // levels the teach ring hangs lower still, centred -0.35 by 0.04, so -0.31 is the real
        // floor of that airspace. The pin must stay UNDER all of it: a card that reaches into
        // the slab punches through a disc every time a train rolls past a switch.
        private const float SwitchFurnitureBottomZ = -0.31f;

        // Board z of the rail crowns — where the consist's own chassis bottoms out. The pin's
        // lower corner must stay above this or the track clips the card's bottom edge.
        private const float RailCrownZ = 0.035f;

        [UnityTest]
        public IEnumerator Pin_FloatsAboveTheCat_Untappable_AndInventoryFree()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 1, progressTicks: 6);
            _view.UpdateFrom(_session);

            var pin = Pin();
            Assert.That(pin, Is.Not.Null,
                "an occupied carriage carries a destination pin — without it a passenger's " +
                "destination is unreadable in play, which is the whole point of the device");
            Assert.That(pin.Find("Card"), Is.Not.Null, "the pin needs its white card");
            Assert.That(pin.Find("Symbol"), Is.Not.Null, "and the symbol that card carries");
            foreach (var filter in pin.GetComponentsInChildren<MeshFilter>(true))
                Assert.That(filter.sharedMesh, Is.Not.Null,
                    filter.name + " must resolve a mesh (a null here renders nothing at all)");
            Assert.That(pin.GetComponentsInChildren<Collider>(true), Is.Empty,
                "the pin is decoration: it floats over the board and must never swallow a " +
                "switch tap on its way past");
            Assert.That(pin.GetComponentsInChildren<BoardElementId>(true), Is.Empty,
                "and it is not authored inventory — the train ROOT carries the only id");
        }

        // THE law that keeps one vocabulary. If anyone ever writes a second colour-to-shape
        // decision — a switch in ToyTrainView, a lookup keyed off the tint, a "red is a circle
        // and everything else is a square" shortcut of the kind CatLine was written to delete —
        // it will disagree with CatLine.ShapeOf on at least one line and this fails. The
        // reference-equality check is deliberate: the pin must carry the SAME mesh instance the
        // shared realiser hands out, not an equivalent one built somewhere else.
        [UnityTest]
        public IEnumerator PinSymbol_IsTheShapeTheSharedVocabularyGivesThatLine()
        {
            yield return BuildBoard();
            var seen = new System.Collections.Generic.List<Mesh>();
            foreach (var (code, line) in new (byte, string)[]
            {
                (CatColor.Red, "red"), (CatColor.Blue, "blue"), (CatColor.Yellow, "yellow"),
                (CatColor.Green, "green"), (CatColor.Wild, "wild"),
            })
            {
                PlaceOnEdge(edge: 1, progressTicks: 6, color: code);
                _view.UpdateFrom(_session);

                DestinationShape shape = CatLine.ShapeOf(line);
                Mesh expected = shape == DestinationShape.Star
                    ? CatPinMeshBuilder.StarBadge()
                    : DestinationShapeMesh.ForShape(shape);
                Assert.That(PinSymbolMesh(), Is.SameAs(expected),
                    $"the {line} pin must carry the mesh CatLine.ShapeOf({line}) = {shape} " +
                    "resolves to — a second shape decision anywhere would show up here first");
                seen.Add(PinSymbolMesh());
            }
            Assert.That(seen, Is.Unique,
                "and every line needs its OWN shape: if two lines share a mesh, destination " +
                "identity has quietly fallen back onto colour alone");
        }

        // A wild cat rides (CatColor.Wild = 5, and LevelGraph guards construction, not travel),
        // so the pin has to draw a star. DestinationShapeMesh.ForShape(Star) throws by design —
        // its extruder fans from vertex 0 and a star is concave — and this pins that the pin
        // solved that by triangulating differently, NOT by weakening the guard next door.
        [UnityTest]
        public IEnumerator WildCat_RidesUnderAStar_AndTheConvexExtruderStillRefusesOne()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 1, progressTicks: 6, color: CatColor.Wild);
            _view.UpdateFrom(_session);

            Assert.That(CatLine.ShapeOf("wild"), Is.EqualTo(DestinationShape.Star));
            Assert.That(PinSymbolMesh(), Is.SameAs(CatPinMeshBuilder.StarBadge()),
                "a wild passenger's pin is the centre-fanned star, not a fallback circle");
            AssertCatColor(CatHeadColor(), Palette.CatnipViolet,
                "and the wild cat itself wears catnip violet — BoardView.ColorForCode had no " +
                "wild case and used to ride it out as loud magenta");

            Assert.That(() => DestinationShapeMesh.ForShape(DestinationShape.Star),
                Throws.ArgumentException,
                "the convex extruder must STILL refuse a star — the pin got its star from a " +
                "centre fan, and 'fix' that guard and station plates silently go to garbage");
        }

        [UnityTest]
        public IEnumerator Pin_HoldsItselfSquareToTheCamera_AtEveryHeading()
        {
            yield return BuildBoard();
            foreach (int edge in new[] { 0, 1, 2 }) // three distinct authored headings
            {
                PlaceOnEdge(edge: edge, progressTicks: 4);
                _view.UpdateFrom(_session);

                Quaternion asSeen = CameraSpaceRotation(Pin());
                Assert.That(Vector3.Dot(asSeen * Vector3.forward, Vector3.forward),
                    Is.GreaterThan(0.9999f),
                    $"on edge {edge} the pin's face must point straight down the view axis — " +
                    "a card left lying in the board plane renders at cos 48 = 67% height and " +
                    "its circle reads as an ellipse");
                Assert.That(Vector3.Dot(asSeen * Vector3.up, Vector3.up),
                    Is.GreaterThan(0.9999f),
                    $"on edge {edge} the pin must also be UPRIGHT on screen — a triangle that " +
                    "rolls with the track stops pointing at the sky and stops being a triangle");
            }
        }

        // The offset is counter-rotated as well as the rotation. Without that the card would
        // swing around its cat like a bucket on a rope as the train turned, and a pin whose
        // position depends on heading is a pin the player has to hunt for.
        [UnityTest]
        public IEnumerator Pin_SitsDIRECTLYAboveItsOwnCat_AtEveryHeading()
        {
            yield return BuildBoard();
            SeatFirstCat();
            foreach (int edge in new[] { 0, 1, 2 })
            {
                PlaceOnEdge(edge: edge, progressTicks: 4);
                _view.UpdateFrom(_session);

                Vector2 head = ScreenPoint(TrainRoot().transform.Find("Carriage/Cat/Head"));
                Vector2 pin = ScreenPoint(Pin());
                Assert.That(pin.x - head.x, Is.EqualTo(0f).Within(0.001f),
                    $"on edge {edge} the pin must sit dead above its cat, not off to one side " +
                    "— the board-plane offset is SOLVED for zero screen drift, so any wander " +
                    "here means the heading counter-rotation was dropped");
                Assert.That(pin.y - head.y, Is.EqualTo(0.26f).Within(0.001f),
                    $"on edge {edge} the pin must hold the same screen rise at every heading");
            }
        }

        // The clearance the whole placement exists to buy. A card held square to a camera 48
        // degrees off the board plane spans a LOT of board z on its own, and the airspace
        // straight above a cat's head belongs to the switch discs — the same ceiling that made
        // "just make the ears taller" the wrong fix. Measured on the shipped meshes' real
        // vertices through their real transforms, so rounded corners count as rounded.
        [UnityTest]
        public IEnumerator Pin_ClearsTheSwitchDiscSlab_AndTheRailCrowns()
        {
            yield return BuildBoard();
            foreach (int edge in new[] { 0, 1, 2 })
            {
                PlaceOnEdge(edge: edge, progressTicks: 4);
                _view.UpdateFrom(_session);

                float highest = float.PositiveInfinity, lowest = float.NegativeInfinity;
                foreach (float z in PinBoardZs())
                {
                    highest = Mathf.Min(highest, z); // -z is up out of the table
                    lowest = Mathf.Max(lowest, z);
                }
                Assert.That(highest, Is.GreaterThan(SwitchFurnitureBottomZ + 0.01f),
                    $"on edge {edge} the pin's top corner (board z {highest:F4}) must stay " +
                    $"clear UNDER the switch furniture at {SwitchFurnitureBottomZ} — a card " +
                    "that reaches into that slab punches through a disc at every switch");
                Assert.That(lowest, Is.LessThan(RailCrownZ - 0.01f),
                    $"on edge {edge} the pin's bottom corner (board z {lowest:F4}) must stay " +
                    $"above the rail crowns at {RailCrownZ}, or the track clips the card");
            }
        }

        [UnityTest]
        public IEnumerator Pin_ReadsAtGameplayZoom_OutSizingTheHeadItLabels()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 1, progressTicks: 6);
            _view.UpdateFrom(_session);

            float cardPx = RenderedWorldSize(Pin().Find("Card")).x * GameplayPixelsPerBoardUnit;
            float symbolPx =
                RenderedWorldSize(Pin().Find("Symbol")).x * GameplayPixelsPerBoardUnit;
            float headPx = RenderedWorldSize(TrainRoot().transform.Find("Carriage/Cat/Head")).x
                * GameplayPixelsPerBoardUnit;

            Assert.That(headPx, Is.EqualTo(17.7f).Within(0.5f),
                "sanity on the yardstick itself: the head renders 17.7 px at the worst zoom");
            Assert.That(cardPx, Is.GreaterThan(headPx),
                $"the card ({cardPx:F1} px) must out-read the head ({headPx:F1} px) it labels " +
                "— in target-01 the pin is about as large as the cat, and ours starts smaller");
            Assert.That(symbolPx, Is.GreaterThan(12f),
                $"the symbol ({symbolPx:F1} px) carries the entire destination channel");
            Assert.That(cardPx - symbolPx, Is.GreaterThan(4f),
                "and the card must still show white around the symbol, or it stops reading " +
                "as a card and the symbol loses the contrast it is mounted on for");

            // The finest detail any of the five symbols carries, and the constraint that fixed
            // the symbol at 0.17 rather than something daintier. Under ~4 px a star stops
            // reading as a star and the wild cat's badge becomes an anonymous blob.
            float starPointPx = 0.5f * (1f - CatPinMeshBuilder.StarInnerRadius)
                * ToyTrainView.PinSymbolSize * GameplayPixelsPerBoardUnit;
            Assert.That(starPointPx, Is.GreaterThan(4f),
                $"a star point renders {starPointPx:F1} px at the worst gameplay zoom");
        }

        // WINDING. This codebase has lost time to backface culling twice, most recently a whole
        // render review where every camera-facing triangle was culled. The generated cards are
        // the only new geometry here, so this checks the thing that actually goes wrong: that
        // their front caps face the CAMERA once the pin is posed, measured through the shipped
        // transform rather than asserted about the source array.
        [UnityTest]
        public IEnumerator PinCardMeshes_FrontCapsFaceTheCamera_NotAwayFromIt()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 1, progressTicks: 6, color: CatColor.Wild); // a star on the card
            _view.UpdateFrom(_session);

            foreach (string part in new[] { "Card", "Symbol" })
            {
                var piece = Pin().Find(part);
                var mesh = piece.GetComponent<MeshFilter>().sharedMesh;
                Quaternion asSeen = CameraSpaceRotation(piece);
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                int frontCapTriangles = 0;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]];
                    Vector3 b = vertices[triangles[i + 1]];
                    Vector3 c = vertices[triangles[i + 2]];
                    if (a.z > -0.4999f || b.z > -0.4999f || c.z > -0.4999f)
                        continue; // front-cap triangles only; side facets share rim positions
                    frontCapTriangles++;
                    Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                    // The camera looks along world +z, so a face pointing back AT it has a
                    // negative z. A mirrored winding flips exactly this sign and nothing else.
                    Assert.That((asSeen * normal).z, Is.LessThan(-0.99f),
                        $"{part} triangle {i / 3} is on the front cap but its normal points AWAY " +
                        "from the camera — the cap was wound forwards; camera-facing " +
                        "triangles enumerate the CCW outline in REVERSE");
                }
                Assert.That(frontCapTriangles, Is.GreaterThan(0),
                    part + " must actually have front-cap triangles to check");
            }
        }

        // BUFFER LAW. The winding test above derives face normals from triangle geometry so
        // duplicated side vertices cannot masquerade as cap vertices. Keep the independent mesh
        // contract that rewrite displaced: every rendered vertex still carries a normal for the
        // lighting pipeline. Removing RecalculateNormals must fail here, not as an index error in
        // an unrelated camera-facing assertion.
        [Test]
        public void PinCardMeshes_CarryOneNormalForEveryVertex()
        {
            foreach (Mesh mesh in new[] { CatPinMeshBuilder.Card(), CatPinMeshBuilder.StarBadge() })
            {
                Assert.That(mesh.vertexCount, Is.GreaterThan(0),
                    mesh.name + " must have rendered vertices to illuminate");
                Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount),
                    mesh.name + " must expose one vertex normal for every rendered vertex");
            }
        }

        [UnityTest]
        public IEnumerator Pin_IsPaletteBound_WhiteCardUnderTheLineColour()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 1, progressTicks: 6, color: CatColor.Blue);
            _view.UpdateFrom(_session);

            AssertCatColor(Pin().Find("Card").GetComponent<MeshRenderer>().sharedMaterial.color,
                Palette.WarmPaper,
                "the card is the palette's WarmPaper — brighter than the carriage's CreamCard " +
                "on purpose, because being the brightest thing on the board is what separates " +
                "a floating pin from the cream body underneath it");

            var block = new MaterialPropertyBlock();
            Pin().Find("Symbol").GetComponent<MeshRenderer>().GetPropertyBlock(block);
            AssertCatColor(block.GetColor("_BaseColor"), Palette.HarborBlue,
                "and the symbol wears the same line colour the cat does, from one vocabulary");
        }

        // Teardown law. The card and star prototypes are SHARED statics flagged HideAndDontSave,
        // the DestinationShapeMesh idiom — not per-consist meshes. Getting this wrong is not
        // merely wasteful: every train on the board carries these, so the first consist torn
        // down by a Retry would blank every other pin on its way out.
        [UnityTest]
        public IEnumerator PinCardMeshes_AreSharedPrototypes_ThatSurviveAConsistBeingTornDown()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 1, progressTicks: 6);
            _view.UpdateFrom(_session);
            var card = Pin().Find("Card").GetComponent<MeshFilter>().sharedMesh;
            Assert.That(card, Is.SameAs(CatPinMeshBuilder.Card()),
                "a consist mounts the shared card prototype, it does not build its own");
            Assert.That(card.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave),
                "the prototype must outlive a scene unload between PlayMode cases");

            Object.DestroyImmediate(_host);
            _host = null;
            _view = null;
            yield return null;
            Assert.That(CatPinMeshBuilder.Card(), Is.Not.Null,
                "and tearing a consist down must not destroy the mesh every other pin uses");
            Assert.That(CatPinMeshBuilder.Card(), Is.SameAs(card),
                "the surviving prototype is the SAME instance, not a quietly rebuilt one");
        }

        private Transform Pin() => TrainRoot().transform.Find("Carriage/Pin");

        private Mesh PinSymbolMesh() =>
            Pin().Find("Symbol").GetComponent<MeshFilter>().sharedMesh;

        // Where a part lands ON SCREEN. The diorama camera is orthographic and identity-rotated,
        // so once a board-local point is turned by the tilt, world x and y ARE screen x and y.
        // Applied explicitly rather than read off a live camera because these cases build a
        // BoardView without running BoardSceneLook.Apply.
        private Vector2 ScreenPoint(Transform part)
        {
            Vector3 tilted = BoardSceneLook.BoardTilt
                * _view.transform.InverseTransformPoint(part.position);
            return new Vector2(tilted.x, tilted.y);
        }

        // A part's rotation as the camera sees it: strip the host's own transform, then apply
        // the diorama tilt. Identity here means "square to the camera".
        private Quaternion CameraSpaceRotation(Transform part) =>
            BoardSceneLook.BoardTilt
            * Quaternion.Inverse(_view.transform.rotation) * part.rotation;

        // Board z of every vertex the pin actually renders. Real vertices, not bounds corners:
        // the card's corners are ROUNDED, so its box overstates the envelope by 0.013 and a
        // clearance figure computed off the box would be quietly pessimistic in a place where
        // every thousandth is spoken for.
        private System.Collections.Generic.IEnumerable<float> PinBoardZs()
        {
            foreach (var filter in Pin().GetComponentsInChildren<MeshFilter>(true))
            {
                Vector3[] vertices = filter.sharedMesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                    yield return _view.transform.InverseTransformPoint(
                        filter.transform.TransformPoint(vertices[i])).z;
            }
        }

        [UnityTest]
        public IEnumerator CatTint_MatchesTheTrainsLineColor()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 1, progressTicks: 6, color: CatColor.Red);
            _view.UpdateFrom(_session);

            AssertCatColor(CatHeadColor(), Palette.SignalRed,
                "the seated cat wears the train's line color, from Palette tokens");
        }

        [UnityTest]
        public IEnumerator Carriage_TrailsTheEngineAlongTheRenderedSpline()
        {
            yield return BuildBoard();
            const int progressTicks = 6;
            PlaceOnEdge(edge: 1, progressTicks: progressTicks);
            _view.UpdateFrom(_session);

            TrackSpline path = BuildTrackGraph().Path(1);
            float fraction = (float)progressTicks
                / _session.Level.Dto.Edges.ToArray()[1].TravelTicks;
            float headDistance = path.Length * fraction;
            Vector3 expected = path.EvaluateDistanceFraction(
                (headDistance - CarriageOffset) / path.Length) + HeadLift;
            Assert.That(Vector3.Distance(CarriageBoardLocal(), expected), Is.LessThan(0.001f),
                "the carriage samples the identical spline a fixed arc-length behind the head");

            Vector3 tangent = path.TangentDistanceFraction(fraction);
            Vector2 engineRight = TrainRoot().transform.Find("Engine").localRotation
                * Vector3.right;
            Assert.That(Vector2.Dot(engineRight.normalized, (Vector2)tangent.normalized),
                Is.GreaterThan(0.999f), "the engine faces along the direction of travel");
        }

        [UnityTest]
        public IEnumerator CrossingANode_TheCarriageKeepsTrailingOnTheArrivalEdge()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 0, progressTicks: 9);  // near E1's end — records history
            _view.UpdateFrom(_session);
            PlaceOnEdge(edge: 1, progressTicks: 0);  // the head just crossed J1 onto E2
            _view.UpdateFrom(_session);

            var graph = BuildTrackGraph();
            TrackSpline arrival = graph.Path(0);
            Vector3 expected = arrival.EvaluateDistanceFraction(
                (arrival.Length - CarriageOffset) / arrival.Length) + HeadLift;
            Vector3 actual = CarriageBoardLocal();
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.001f),
                "the carriage trails through the junction on the remembered arrival edge");
            Assert.That(Vector2.Distance(actual, graph.Path(1).Evaluate(0f)),
                Is.GreaterThan(0.3f),
                "no teleport-bunch: the carriage must NOT clamp to the new edge's start");

            // Review finding 2: at 60fps the head renders MANY frames on the new edge; a
            // same-edge frame must not clobber the previous-edge memory (a mutant that
            // records history unconditionally would put the carriage AHEAD of the engine
            // here, at the far end of the edge the head just entered).
            _view.UpdateFrom(_session);
            Assert.That(Vector3.Distance(CarriageBoardLocal(), expected), Is.LessThan(0.001f),
                "a repeated frame on the new edge still trails on the arrival edge");
        }

        [UnityTest]
        public IEnumerator CatchUpFrameSkippingAnEdge_ClampsInsteadOfTrailingForeignTrack()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 1, progressTicks: 11); // deep into E2, heading for RED
            _view.UpdateFrom(_session);
            // A pause/resume catch-up frame: the sim ran many ticks and the head reappears
            // on E3 — an edge E2 does not feed (E2 ends at RED, E3 starts at J1).
            PlaceOnEdge(edge: 2, progressTicks: 0);
            _view.UpdateFrom(_session);

            var graph = BuildTrackGraph();
            Vector3 actual = CarriageBoardLocal();
            Assert.That(Vector3.Distance(actual, graph.Path(2).Evaluate(0f) + HeadLift),
                Is.LessThan(0.001f),
                "non-adjacent history is discarded through the BoardView seam — the carriage " +
                "takes the spawn clamp at the new edge's start");
            TrackSpline foreign = graph.Path(1);
            Assert.That(Vector2.Distance(actual, foreign.EvaluateDistanceFraction(
                    (foreign.Length - CarriageOffset) / foreign.Length)),
                Is.GreaterThan(1f),
                "and it must be nowhere near the foreign edge's end the stale memory named");
        }

        [UnityTest]
        public IEnumerator QueuedAtANode_TheConsistTrailsBackAlongTheArrivalEdge()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 0, progressTicks: 9);
            _view.UpdateFrom(_session);
            _session.State.Trains[0].State = TrainState.AtNode; // arrived and queued at J1
            _session.State.Trains[0].EdgeId = -1;
            _session.State.Trains[0].ProgressTicks = 0;
            _session.State.Trains[0].NodeId = 1;
            _view.UpdateFrom(_session);

            var train = TrainRoot();
            Assert.That(Vector3.Distance(train.transform.localPosition,
                    new Vector3(3f, 6f, 0f) + HeadLift), Is.LessThan(0.001f),
                "a parked head anchors on its node, exactly like the old capsule");
            TrackSpline arrival = BuildTrackGraph().Path(0);
            Vector3 expected = arrival.EvaluateDistanceFraction(
                (arrival.Length - CarriageOffset) / arrival.Length) + HeadLift;
            Assert.That(Vector3.Distance(CarriageBoardLocal(), expected), Is.LessThan(0.001f),
                "the queued consist trails back along the edge it arrived on");
        }

        [UnityTest]
        public IEnumerator SlotReuse_ResetsEdgeHistoryAndRetintsTheCat()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 0, progressTicks: 9, id: 1, color: CatColor.Red);
            _view.UpdateFrom(_session);
            _session.State.Trains[0] = default; // delivered: the slot zeroes (A-C1-10)
            _view.UpdateFrom(_session);
            PlaceOnEdge(edge: 1, progressTicks: 0, id: 2, color: CatColor.Blue);
            _view.UpdateFrom(_session);

            AssertCatColor(CatHeadColor(), Palette.HarborBlue,
                "a reused slot re-tints for its NEW occupant");
            var graph = BuildTrackGraph();
            Assert.That(Vector3.Distance(CarriageBoardLocal(),
                    graph.Path(1).Evaluate(0f) + HeadLift), Is.LessThan(0.001f),
                "a reused slot must not inherit the dead train's edge history — with none, " +
                "the carriage bunches at the new edge's start (the documented spawn clamp)");
        }

        [UnityTest]
        public IEnumerator DeadSlot_HidesTheWholeConsist()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 1, progressTicks: 6);
            _view.UpdateFrom(_session);
            _session.State.Trains[0] = default;
            _view.UpdateFrom(_session);

            Assert.That(TrainRoot().gameObject.activeSelf, Is.False,
                "a delivered train's consist leaves the board, cat and all");
        }

        [UnityTest]
        public IEnumerator AdmittedRigEarTwitch_ComposesAfterAnimatorWithoutAccumulating()
        {
            yield return BuildBoard();
            PlaceOnEdge(edge: 0, progressTicks: 4);
            _view.UpdateFrom(_session, 0f);
            ToyTrainView train = TrainRoot().GetComponent<ToyTrainView>();
            if (!train.RigAdmitted)
                Assert.Ignore("The licensed local rig is absent; run in the combined workspace.");
            Assert.That(train.RigEarTwitchSupported, Is.True,
                "the shipped 30-bone rig must bind TASK 17's two measured ear branches");

            Animator animator = train.GetComponentInChildren<Animator>(true);
            Transform branchA = animator.transform.Find(CatModelCatalog.EarDeformerPathA);
            Transform branchB = animator.transform.Find(CatModelCatalog.EarDeformerPathB);
            const uint firstSeed = 41u;
            float firstTime = TimeWithLargeEarTwitch(firstSeed);
            CatMicroPose firstPose = new CatMicroMotion(firstSeed)
                .Evaluate(firstTime, false, false);
            Quaternion offsetA = Quaternion.Euler(0f, 0f,
                firstPose.EarTwitchDegrees * ToyTrainView.RigEarTwitchGain);
            Quaternion offsetB = Quaternion.Euler(0f, 0f,
                -firstPose.EarTwitchDegrees * ToyTrainView.RigEarTwitchGain);

            train.SyncSlot(firstSeed, CatColor.Red);
            train.ApplyPresentation(CatPresentationState.RideIdle, firstTime, false);
            yield return null;
            Quaternion firstFrameA = branchA.localRotation;
            Quaternion firstFrameB = branchB.localRotation;
            Quaternion sampledIdleA = firstFrameA * Quaternion.Inverse(offsetA);
            Quaternion sampledIdleB = firstFrameB * Quaternion.Inverse(offsetB);
            for (int frame = 0; frame < 4; frame++) yield return null;
            Assert.That(Quaternion.Angle(branchA.localRotation, firstFrameA), Is.LessThan(0.01f),
                "LateUpdate must replace, not accumulate, the additive on padded idle clips");
            Assert.That(Quaternion.Angle(branchB.localRotation, firstFrameB), Is.LessThan(0.01f));

            train.SyncSlot(42L, CatColor.Blue);
            Assert.That(Quaternion.Angle(branchA.localRotation, sampledIdleA), Is.LessThan(0.01f),
                "occupant reuse must strip the previous cat's procedural ear offset");
            Assert.That(Quaternion.Angle(branchB.localRotation, sampledIdleB), Is.LessThan(0.01f));

            float walkTime = TimeWithLargeEarTwitch(42u);
            CatMicroPose walkPose = new CatMicroMotion(42u).Evaluate(walkTime, false, false);
            train.ApplyPresentation(CatPresentationState.Walk, walkTime, false);
            yield return null;
            AnimatorStateInfo walkState = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(walkState.IsName("Base Layer." + CatModelCatalog.WalkClip), Is.True);

            GameObject control = Object.Instantiate(
                Resources.Load<GameObject>(CatModelCatalog.ResourcePath), _host.transform, false);
            Animator controlAnimator = control.GetComponentInChildren<Animator>(true);
            controlAnimator.applyRootMotion = false;
            controlAnimator.speed = 0f;
            controlAnimator.Rebind();
            float walkPhase = walkState.normalizedTime - Mathf.Floor(walkState.normalizedTime);
            controlAnimator.Play("Base Layer." + CatModelCatalog.WalkClip, 0, walkPhase);
            controlAnimator.Update(0f);
            Transform controlA = controlAnimator.transform.Find(CatModelCatalog.EarDeformerPathA);
            Transform controlB = controlAnimator.transform.Find(CatModelCatalog.EarDeformerPathB);
            Quaternion authoredA = branchA.localRotation * Quaternion.Inverse(
                Quaternion.Euler(0f, 0f, walkPose.EarTwitchDegrees
                    * ToyTrainView.RigEarTwitchGain));
            Quaternion authoredB = branchB.localRotation * Quaternion.Inverse(
                Quaternion.Euler(0f, 0f, -walkPose.EarTwitchDegrees
                    * ToyTrainView.RigEarTwitchGain));
            Assert.That(Quaternion.Angle(authoredA, controlA.localRotation), Is.LessThan(0.1f),
                "the additive must preserve Animator's authored Walk sample on branch A");
            Assert.That(Quaternion.Angle(authoredB, controlB.localRotation), Is.LessThan(0.1f),
                "the additive must preserve Animator's authored Walk sample on branch B");

            train.ApplyPresentation(CatPresentationState.RideIdle, walkTime, true);
            Assert.That(train.RigNeutralSampleCount, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator BuildBoard()
        {
            var level = ImportL001();
            _session = new GameSession(level);
            _host = new GameObject("train-consist-host");
            _view = BoardView.Build(level, _host.transform, _session);
            yield return null;
        }

        private void PlaceOnEdge(int edge, int progressTicks,
            byte color = CatColor.Red, short id = 1)
        {
            _session.State.Trains[0] = new TrainSlot
            {
                Id = id,
                Color = color,
                EdgeId = (short)edge,
                ProgressTicks = (short)progressTicks,
                NodeId = 1,
                State = TrainState.OnEdge,
            };
        }

        private void SeatFirstCat()
        {
            PlaceOnEdge(edge: 0, progressTicks: 4);
            _view.UpdateFrom(_session, 0f);
            _view.UpdateFrom(_session,
                CatPresentationTrack.SpawnWalkDuration
                + CatPresentationTrack.BoardDuration);
            Assert.That(TrainRoot().GetComponent<ToyTrainView>().PresentationState,
                Is.EqualTo(CatPresentationState.RideIdle),
                "seated geometry laws must measure the fixed-yaw RideIdle artifact, not the " +
                "path-facing Walk/Board transfer pose");
        }

        private static float TimeWithLargeEarTwitch(uint seed)
        {
            var motion = new CatMicroMotion(seed);
            for (int sample = 0; sample <= 1000; sample++)
            {
                float time = sample * 0.01f;
                if (Mathf.Abs(motion.Evaluate(time, false, false).EarTwitchDegrees) >= 12f)
                    return time;
            }
            Assert.Fail("the deterministic Tier-1 cadence must contain a >=12 degree ear pose");
            return 0f;
        }

        private BoardElementId TrainRoot() =>
            _view.GetComponentsInChildren<BoardElementId>(true).Single(x => x.Kind == "train");

        private Vector3 CarriageBoardLocal()
        {
            var carriage = TrainRoot().transform.Find("Carriage");
            return _view.transform.InverseTransformPoint(carriage.position);
        }

        private Color CatHeadColor()
        {
            var head = TrainRoot().transform.Find("Carriage").Find("Cat").Find("Head");
            var block = new MaterialPropertyBlock();
            head.GetComponent<MeshRenderer>().GetPropertyBlock(block);
            return block.GetColor("_BaseColor");
        }

        // The property-block round trip is not bit-exact in this Linear-color-space project
        // (ProjectSettings m_ActiveColorSpace: 1): SetColor stores the float32 sRGB->linear
        // conversion and GetColor approximates the inverse, an ulp-level wobble the first
        // device run caught with exact Color equality (identical 3-decimal prints, unequal
        // bits). No Color32/255-step exists anywhere in the tint path, so 2e-3 per channel
        // covers the conversion with room while staying ~15x under the closest palette pair
        // (SignalRed vs AlarmCoral, 8/255 apart in red).
        private static void AssertCatColor(Color actual, Color expected, string message)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.002f), message + " (r)");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.002f), message + " (g)");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.002f), message + " (b)");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.002f), message + " (a)");
        }

        private TrackSplineGraph BuildTrackGraph()
        {
            // Derive the comparison graph from the same imported artifact BoardView rendered;
            // a campaign retune must not leave this assertion measuring the legacy L001.
            var nodes = _session.Level.Dto.Nodes.ToArray();
            var edges = _session.Level.Dto.Edges.ToArray();
            var index = nodes.Select((node, i) => new { node.Id, Index = i })
                .ToDictionary(x => x.Id, x => x.Index);
            var positions = nodes.Select(node => new Vector3(node.X, node.Y, 0f)).ToArray();
            return TrackSplineGraph.Build(positions,
                edges.Select(edge => index[edge.From]).ToArray(),
                edges.Select(edge => index[edge.To]).ToArray());
        }

        private static ImportedLevel ImportL001()
        {
            string path = Path.Combine(UnityEngine.Application.streamingAssetsPath,
                "content", "levels", "L001.json");
            var imported = LevelImporter.Import(File.ReadAllBytes(path));
            Assert.That(imported.Ok, Is.True,
                imported.Ok ? string.Empty : imported.Error.ToString());
            return imported.Value;
        }
    }
}
