using System.Collections;
using System.IO;
using System.Linq;
using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
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
            PlaceOnEdge(edge: 1, progressTicks: 6); // fraction 0.5 of E2's arc
            _view.UpdateFrom(_session);

            TrackSpline path = BuildTrackGraph().Path(1);
            float headDistance = path.Length * 0.5f;
            Vector3 expected = path.EvaluateDistanceFraction(
                (headDistance - CarriageOffset) / path.Length) + HeadLift;
            Assert.That(Vector3.Distance(CarriageBoardLocal(), expected), Is.LessThan(0.001f),
                "the carriage samples the identical spline a fixed arc-length behind the head");

            Vector3 tangent = path.TangentDistanceFraction(0.5f);
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

        private static TrackSplineGraph BuildTrackGraph()
        {
            // L001's authored graph, matching BoardView's own construction inputs.
            var positions = new[]
            {
                new Vector3(3f, 9f, 0f), // SRC
                new Vector3(3f, 6f, 0f), // J1
                new Vector3(1f, 2f, 0f), // RED
                new Vector3(5f, 2f, 0f), // BLU
            };
            return TrackSplineGraph.Build(positions,
                new[] { 0, 1, 1 }, new[] { 1, 2, 3 });
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
