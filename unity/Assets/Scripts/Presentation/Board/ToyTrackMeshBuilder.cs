using System.Collections.Generic;
using CatMetro.Presentation.Theme;
using UnityEngine;

namespace CatMetro.Presentation.Board
{
    // Board-local +z points DOWN into the table, so every height here is a z where the
    // SMALLER number is the higher surface. The board deck is at z +0.35; the camera and
    // the whole toy sit on the -z side of it.
    //
    // Shape, from target-01/target-02: the track is ONE continuous pale ballast ribbon
    // swept along the edge spline, wider than the gauge, with the twin navy rails sunk
    // into it so roughly half of each rail is buried and only a shallow crown stands
    // proud. Sleeper ticks survive as low cream blocks embossed into the bed — the
    // rhythm reads, but the bed is still a single object. The old shape (isolated
    // sleeper blocks with a 0.125 gap under free-floating rails) is what made the track
    // read as thin dark lines on bare board.
    public static class ToyTrackMeshBuilder
    {
        // ---- Rails ----------------------------------------------------------------
        // CROSS-BRANCH CONTRACT. RailCrownZ is the plane the consist rides on:
        // ToyTrainView pins its head anchor at board z -0.2 and bottoms every chassis
        // part out at anchor-local +0.235, i.e. exactly here. BoardView's greybox
        // capsule (z -0.2, uniform scale 0.35) clears it by the same construction.
        // Moving either of these two numbers moves the train off the rails.
        public const float RailOffset = 0.25f;   // half gauge; the consist is sized to 0.5
        public const float RailCrownZ = 0.035f;  // PINNED — the running surface

        private const float RailWidth = 0.13f;   // was 0.11: the navy has to read at zoom
        private const float RailShoulderZ = 0.065f;
        private const float RailBackZ = 0.165f;  // underside; the bed closes over it

        // ---- Ballast bed ----------------------------------------------------------
        // The ribbon. 1.08 wide against a 2.0 minimum node spacing across all 17 levels,
        // so parallel runs still leave most of a cell of board showing between them.
        private const float BedHalfWidth = 0.54f;
        private const float BedChamfer = 0.07f;
        private const float BedTopZ = 0.105f;    // top surface at the flat-top edges
        private const float BedCrownRise = 0.008f; // gentle camber: reads as moulded wood,
                                                   // and keeps two beds crossing at a node
                                                   // from sharing one coplanar top face
        private const float BedBackZ = 0.34f;    // 0.01 clear of the deck: a contact shadow,
                                                 // not a hover

        // ---- Sleeper ticks --------------------------------------------------------
        // Sunk blocks, not free-standing sleepers. The first render had them 0.030 proud
        // and they read as chunky bars casting hard shadows, where target-01's ties are
        // barely-there impressions; the board also renders ~1.4x larger in area than the
        // captures these were first sized against, so proudness carries further than the
        // raw number suggests. Now 0.010 clear of the bed's crown and 0.018 clear of its
        // flat-top edge — a shading tick, not a step.
        //
        // The end inset also keeps ticks out of the zone where a NEIGHBOURING edge's bed
        // overlaps this one. A tick stands above its own bed, so it stands above the
        // neighbour's too and pokes through its top face as a stair-stepped sliver. A
        // neighbouring bed reaches a median 0.68 (max 1.01) in from an edge end, so 0.72
        // clears it on 56% of edges outright and the shallower proudness shrinks what is
        // left; fully clearing it would need 1.0+, which strips short edges back to a
        // single tick. Properly fixing the rest needs the builder to know its neighbours,
        // which the one-edge-at-a-time API does not carry.
        private const float SleeperLength = 0.88f;  // inside the bed's 0.94 flat top
        private const float SleeperWidth = 0.16f;
        private const float SleeperSpacing = 0.34f;
        private const float SleeperEndInset = 0.72f;
        private const float SleeperTopZ = 0.087f;
        private const float SleeperSkirtZ = 0.25f;
        private const float SleeperCorner = 0.035f;

        // ---- Puzzle-piece joins ---------------------------------------------------
        // target-01 builds its track from wooden toy segments joined by interlocking
        // tab-and-socket connectors: a hairline seam cut clean across the cream bed
        // with a mushroom lobe in the middle. It is the detail that says "toy" rather
        // than "model railway", so it earns its triangles.
        //
        // ONE outline draws both halves. The lobe is a TAB on the piece behind the
        // seam and the matching SOCKET on the piece in front of it — that is what a
        // tab-and-socket join is when you look down on it, so there is nothing to
        // model twice. The lobe points along +tangent, which puts every piece's tab
        // at its downstream end exactly like a real peg-and-hole wooden rail.
        //
        // PLACEMENT is anchored to the edge's END, not its start, and this matters.
        // Adjacent edges butt their beds with no inset, so a seam sitting AT a shared
        // node would double up, and at a turnout the outgoing branches have not yet
        // separated — measured on the corpus, three start-anchored seams land 0.338
        // apart at L001's switch, which is one third of a bed width and reads as a
        // smear. End-anchoring puts exactly one seam at each node that has an edge
        // arriving, so all 17 levels produce zero seam collisions. (The one merge
        // node L018/L019 introduce is the single exception — see the note on
        // JoinInset.)
        //
        // JoinInset is sized off the neighbouring-bed overlap, not off taste: a seam
        // stands 0.010 proud of ITS OWN bed, so wherever a neighbour's bed lies over
        // it the lobe pokes through as a stair-stepped sliver — the same failure the
        // sleeper ticks document. Sweeping the corpus, 0.72 leaves 31 of 76 seams
        // buried under a neighbour; 0.90 clears every one of them with 0.070 to
        // spare. That same clearance is why end-anchoring cannot smear: if no
        // neighbour's bed reaches the seam, no neighbour's seam can either.
        // SIZED OFF THE RENDER, not off the model. Measured on the r6 capture at
        // 917x2048: the bed's 1.08 width covers 99 px and the 0.50 rail gauge covers
        // 47.5 px, so roughly 93 px per world unit ACROSS the track. Along the track
        // the camera's tilt compresses that: the 0.34 sleeper pitch covers ~13.5 px
        // against the 31.6 it would get unforeshortened, a factor of 0.43.
        //
        // That asymmetry is the whole reason these numbers are what they are. A seam
        // runs ACROSS the bed, so its WIDTH is measured along the track and takes the
        // full 0.43 squash. The first pass used 0.06 and would have rendered 2.4 px
        // wide — under the ~3 px where a line still survives resolve. 0.09 lands at
        // 3.6 px: finer than a sleeper tick's 6.4 px, which is right for a seam, and
        // still there. The lobe is sized the same way and comes out 37 px across by
        // 16 px deep, comfortably legible against a board that fills ~26% of frame.
        private const float JoinSeamHalfWidth = 0.045f;  // 0.09 wide -> ~3.6 px
        private const float JoinNeckHalfWidth = 0.09f;   // 0.09 gap  -> ~8.4 px
        private const float JoinHeadRadius = 0.155f;     // 0.40 wide -> ~37 px
        private const float JoinHeadCentre = 0.20f;      // 0.40 deep -> ~16 px
        private const int JoinArcSteps = 14;
        private const float JoinSkirtZ = 0.20f;

        // Relief, not depth: the seam is ADDED as a proud hairline rather than cut
        // into the bed. At this scale the choice is invisible — 0.010 of relief
        // projects to about one pixel, so a groove and a rib produce the same
        // one-pixel light/shadow pair — and additive geometry cannot fold the swept
        // ribbon, cannot break it, and leaves the sweep's fold verification standing
        // untouched. Carving would mean subdividing the sweep's top face, which is
        // the one thing on this branch that is expensively known to be correct.
        // 0.010 above the bed is exactly the sleeper ticks' relief over the crown,
        // which is the empirical proof it reads: those ticks are legible in the
        // r6 capture.
        private const float JoinProud = 0.010f;

        // PUBLIC so the tests can pin placement without copying the numbers.
        public const float JoinInset = 0.90f;
        public const float JoinMinimumLength = 3f * JoinInset;

        private const float JoinHalfSpan = BedHalfWidth - BedChamfer; // reach the chamfer

        private const float SampleSpacing = 0.16f;

        // How close a swept ring may reach toward its own centre of curvature. Below 1.0
        // the ribbon can never cross itself; 0.9 leaves margin for splines tighter than
        // anything currently authored.
        private const float CurveTuckSafety = 0.9f;

        // Cross-sections live in (lateral, board z) and are wound so that
        // Cross(v1 - v0, v2 - v0) points OUT of the solid: walk the outline with
        // lateral to the right and z downwards, starting along the top surface.
        // Reversing this list mirrors every face inward and the whole sweep is culled.
        private static readonly Vector2[] BedSection =
        {
            new Vector2(-(BedHalfWidth - BedChamfer), BedTopZ),
            new Vector2(0f, BedTopZ - BedCrownRise),
            new Vector2(BedHalfWidth - BedChamfer, BedTopZ),
            new Vector2(BedHalfWidth, BedTopZ + BedChamfer),
            new Vector2(BedHalfWidth, BedBackZ),
            new Vector2(-BedHalfWidth, BedBackZ),
            new Vector2(-BedHalfWidth, BedTopZ + BedChamfer),
        };

        private static readonly Vector2[] RailSection =
        {
            new Vector2(-RailWidth * 0.5f + 0.025f, RailCrownZ),
            new Vector2(RailWidth * 0.5f - 0.025f, RailCrownZ),
            new Vector2(RailWidth * 0.5f, RailShoulderZ),
            new Vector2(RailWidth * 0.5f, RailBackZ),
            new Vector2(-RailWidth * 0.5f, RailBackZ),
            new Vector2(-RailWidth * 0.5f, RailShoulderZ),
        };

        private static Material _sleeperMaterial;
        private static Material _railMaterial;

        public static GameObject Build(string edgeId, TrackSpline path, Transform parent)
        {
            var vertices = new List<Vector3>(1024);
            var bedTriangles = new List<int>(1536);
            var railTriangles = new List<int>(1536);

            // The bed runs the WHOLE spline — no end inset. Edges that share a node
            // butt their beds together there, which is what makes a route read as one
            // ribbon instead of a row of separate track pieces.
            AppendSweep(path, BedSection, vertices, bedTriangles);
            AppendSleeperTicks(path, vertices, bedTriangles);
            AppendJoinSeam(path, vertices, bedTriangles);
            AppendSweep(path.CreateLateralRail(-RailOffset), RailSection,
                vertices, railTriangles);
            AppendSweep(path.CreateLateralRail(RailOffset), RailSection,
                vertices, railTriangles);

            var mesh = new Mesh { name = "Toy track " + edgeId };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(bedTriangles, 0);
            mesh.SetTriangles(railTriangles, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var root = new GameObject("edge:" + edgeId);
            root.transform.SetParent(parent, false);
            var id = root.AddComponent<BoardElementId>();
            id.Id = edgeId;
            id.Kind = "edge";
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { SleeperMaterial(), RailMaterial() };
            root.AddComponent<GeneratedTrackMeshOwner>().Mesh = mesh;
            return root;
        }

        private static Material SleeperMaterial()
        {
            if (_sleeperMaterial == null)
                _sleeperMaterial = GreyboxMaterial.CreateTinted(
                    "Toy Track — Cream Ballast", Palette.CreamCard);
            return _sleeperMaterial;
        }

        private static Material RailMaterial()
        {
            if (_railMaterial == null)
                _railMaterial = GreyboxMaterial.CreateTinted(
                    "Toy Track — Navy Rails", Palette.InkNavy);
            return _railMaterial;
        }

        // Sweeps a closed cross-section along a spline. Side quads first, then a fan cap
        // at each end — the caps assume a convex section, which both sections above are.
        //
        // A ribbon this wide folds inside-out on the tight side of a turn whose radius is
        // smaller than its half-width, and the authored levels turn as tight as R=0.167
        // (L008 E4) — well inside the bed's 0.54. A fold inverts the top face, backface
        // culling drops it, and you get a hole through the track at exactly the switch the
        // player is looking at. So each ring caps its INNER lateral offset at the local
        // centre of curvature: the ribbon tucks in through a hairpin instead of crossing
        // itself. Sized off the ring-to-ring turn rather than an instantaneous curvature,
        // because it is the quad between two rings that folds. Verified to leave zero
        // inverted faces across all 76 edges of all 17 levels; the tuck bites on 1% of
        // rings and the median edge keeps the full width.
        private static void AppendSweep(TrackSpline centreline, Vector2[] crossSection,
            List<Vector3> vertices, List<int> triangles)
        {
            int segments = Mathf.Clamp(
                Mathf.CeilToInt(centreline.Length / SampleSpacing), 8, 64);
            int ring = crossSection.Length;

            var centres = new Vector3[segments + 1];
            var tangents = new Vector3[segments + 1];
            var laterals = new Vector3[segments + 1];
            for (int segment = 0; segment <= segments; segment++)
            {
                float fraction = (float)segment / segments;
                centres[segment] = centreline.EvaluateDistanceFraction(fraction);
                tangents[segment] = centreline.TangentDistanceFraction(fraction);
                laterals[segment] =
                    new Vector3(-tangents[segment].y, tangents[segment].x, 0f).normalized;
            }

            // Signed turn rate per interval: positive turns toward +lateral, so the centre
            // of curvature sits at centre + lateral / turnRate.
            var turnRate = new float[segments];
            for (int segment = 0; segment < segments; segment++)
            {
                Vector3 from = tangents[segment];
                Vector3 to = tangents[segment + 1];
                float step = Vector3.Distance(centres[segment], centres[segment + 1]);
                turnRate[segment] = step > 0.000001f
                    ? Mathf.Atan2(from.x * to.y - from.y * to.x, from.x * to.x + from.y * to.y)
                        / step
                    : 0f;
            }

            int start = vertices.Count;
            for (int segment = 0; segment <= segments; segment++)
            {
                float rate = 0f;
                if (segment > 0) rate = turnRate[segment - 1];
                if (segment < segments && Mathf.Abs(turnRate[segment]) > Mathf.Abs(rate))
                    rate = turnRate[segment];

                for (int i = 0; i < ring; i++)
                {
                    float offset = crossSection[i].x;
                    if (rate != 0f && offset * rate > 0f)
                    {
                        float limit = CurveTuckSafety / Mathf.Abs(rate);
                        if (Mathf.Abs(offset) > limit)
                            offset = Mathf.Sign(offset) * limit;
                    }
                    vertices.Add(centres[segment] + laterals[segment] * offset
                        + Vector3.forward * crossSection[i].y);
                }
            }

            for (int segment = 0; segment < segments; segment++)
            {
                int a = start + segment * ring;
                int b = a + ring;
                for (int i = 0; i < ring; i++)
                {
                    int next = (i + 1) % ring;
                    AddTriangle(triangles, a + i, b + next, b + i);
                    AddTriangle(triangles, a + i, a + next, b + next);
                }
            }
            for (int i = 1; i < ring - 1; i++)
            {
                AddTriangle(triangles, start, start + i + 1, start + i);
                int end = start + segments * ring;
                AddTriangle(triangles, end, end + i, end + i + 1);
            }
        }

        // A real wooden piece has no tie printed across its connector, and geometry
        // agrees: a tick's flat top sits at SleeperTopZ and the seam's cambered top
        // touches exactly that height at the crown, so a tick crossing the seam would
        // put two cream surfaces on one plane and z-fight. Clearing the window costs
        // one tick per seam across the whole corpus and leaves at least four standing
        // on every seamed edge.
        private static bool JoinBlocksTick(TrackSpline path, float distance)
        {
            float seam = JoinDistance(path);
            if (seam < 0f) return false;
            // The lobe points UPSTREAM, so the window it clears runs back from the
            // seam line, not forward from it.
            float reach = JoinHeadCentre + JoinHeadRadius + JoinSeamHalfWidth;
            return distance >= seam - reach - SleeperWidth * 0.5f
                && distance <= seam + JoinSeamHalfWidth + SleeperWidth * 0.5f;
        }

        private static void AppendSleeperTicks(TrackSpline path,
            List<Vector3> vertices, List<int> triangles)
        {
            float usableLength = Mathf.Max(0f, path.Length - 2f * SleeperEndInset);
            int count = Mathf.Max(1, Mathf.FloorToInt(usableLength / SleeperSpacing) + 1);
            for (int sleeper = 0; sleeper < count; sleeper++)
            {
                float distance = count == 1
                    ? path.Length * 0.5f
                    : SleeperEndInset + usableLength * sleeper / (count - 1);
                if (JoinBlocksTick(path, distance)) continue;
                float fraction = path.Length > 0f ? distance / path.Length : 0f;
                Vector3 centre = path.EvaluateDistanceFraction(fraction);
                Vector3 tangent = path.TangentDistanceFraction(fraction);
                Vector3 lateral = new Vector3(-tangent.y, tangent.x, 0f).normalized;
                AppendChamferedSleeper(centre, tangent, lateral, vertices, triangles);
            }
        }

        // The footprint's x maps through LATERAL and its y through TANGENT, and
        // lateral x tangent = -forward — the basis is mirrored against the board plane.
        // So a footprint listed counter-clockwise in (x, y) produces a camera-facing top
        // cap only when the top fan keeps the list order and the bottom fan reverses it.
        // Getting this backwards culls every sleeper; it cost this repo a day once.
        private static void AppendChamferedSleeper(Vector3 centre,
            Vector3 tangent, Vector3 lateral,
            List<Vector3> vertices, List<int> triangles)
        {
            float halfLength = SleeperLength * 0.5f;
            float halfWidth = SleeperWidth * 0.5f;
            var footprint = new[]
            {
                new Vector2(-halfLength + SleeperCorner, -halfWidth),
                new Vector2(halfLength - SleeperCorner, -halfWidth),
                new Vector2(halfLength, -halfWidth + SleeperCorner),
                new Vector2(halfLength, halfWidth - SleeperCorner),
                new Vector2(halfLength - SleeperCorner, halfWidth),
                new Vector2(-halfLength + SleeperCorner, halfWidth),
                new Vector2(-halfLength, halfWidth - SleeperCorner),
                new Vector2(-halfLength, -halfWidth + SleeperCorner),
            };

            int start = vertices.Count;
            for (int face = 0; face < 2; face++)
            {
                float z = face == 0 ? SleeperTopZ : SleeperSkirtZ;
                for (int i = 0; i < footprint.Length; i++)
                    vertices.Add(centre + lateral * footprint[i].x
                        + tangent * footprint[i].y + Vector3.forward * z);
            }

            for (int i = 1; i < footprint.Length - 1; i++)
            {
                AddTriangle(triangles, start, start + i, start + i + 1);
                AddTriangle(triangles, start + 8, start + 8 + i + 1, start + 8 + i);
            }
            for (int i = 0; i < footprint.Length; i++)
            {
                int next = (i + 1) % footprint.Length;
                AddTriangle(triangles, start + i, start + 8 + i, start + 8 + next);
                AddTriangle(triangles, start + i, start + 8 + next, start + next);
            }
        }

        // Where this edge's seam sits, as a distance along the spline, or -1 when the
        // edge is too short to carry one. Short edges going without is deliberate:
        // the seam has to clear the neighbouring bed (0.90) AND still read as a piece
        // boundary rather than a mark stranded mid-run, so it must land in the final
        // third. Below 3 x JoinInset those two demands cannot both be met, and a
        // wrong seam is worse than no seam. 43 of the corpus's 76 edges qualify and
        // every one of the 17 levels keeps at least two.
        public static float JoinDistance(TrackSpline path) =>
            path.Length < JoinMinimumLength ? -1f : path.Length - JoinInset;

        // The bed's cambered top at a lateral offset. Smaller z is higher, so the
        // crown at lateral 0 is the highest point and it falls away to the flat-top
        // edge. The seam has to ride this or it would sink into the bed at the centre
        // and float off it at the rim.
        private static float BedTopAt(float lateralOffset)
        {
            float flat = BedHalfWidth - BedChamfer;
            float a = Mathf.Min(Mathf.Abs(lateralOffset), flat);
            return (BedTopZ - BedCrownRise) + BedCrownRise * (a / flat);
        }

        // The seam centreline in footprint space (x = lateral, y = tangent), walked
        // from one rim of the bed to the other: straight in, up the neck, right round
        // the head, back down the far side of the neck, straight out.
        private static Vector2[] JoinSeamOutline()
        {
            var outline = new List<Vector2>(JoinArcSteps + 6);
            float half = Mathf.Sqrt(Mathf.Max(0f,
                JoinHeadRadius * JoinHeadRadius - JoinNeckHalfWidth * JoinNeckHalfWidth));
            float neckTop = JoinHeadCentre - half;

            outline.Add(new Vector2(-JoinHalfSpan, 0f));
            outline.Add(new Vector2(-JoinNeckHalfWidth, 0f));
            outline.Add(new Vector2(-JoinNeckHalfWidth, neckTop));

            float a0 = Mathf.Atan2(-half, -JoinNeckHalfWidth);
            float a1 = Mathf.Atan2(-half, JoinNeckHalfWidth);
            float sweep = (a1 - 2f * Mathf.PI) - a0;   // over the TOP of the head
            for (int step = 1; step < JoinArcSteps; step++)
            {
                float angle = a0 + sweep * step / JoinArcSteps;
                outline.Add(new Vector2(
                    JoinHeadRadius * Mathf.Cos(angle),
                    JoinHeadCentre + JoinHeadRadius * Mathf.Sin(angle)));
            }

            outline.Add(new Vector2(JoinNeckHalfWidth, neckTop));
            outline.Add(new Vector2(JoinNeckHalfWidth, 0f));
            outline.Add(new Vector2(JoinHalfSpan, 0f));

            // Turn the whole thing through 180 degrees so the lobe points AWAY from
            // the node, upstream. Pointing it downstream put its tip 0.90 - 0.40 =
            // 0.50 from the node, back inside the reach of the neighbouring bed that
            // JoinInset exists to clear; upstream it sits 1.30 clear and the corpus's
            // worst clearance goes from 0.070 to 0.334. Every piece then carries its
            // tab at its upstream end and its socket at the downstream one, which is
            // the same peg-and-hole alternation, just handed the other way.
            //
            // It has to be a ROTATION, not a mirror: (x, y) -> (-x, -y) has
            // determinant +1 and preserves the footprint's handedness. Negating y
            // alone would flip it, and a flipped footprint on an already-mirrored
            // basis inverts every lid.
            var rotated = new Vector2[outline.Count];
            for (int i = 0; i < outline.Count; i++)
                rotated[i] = new Vector2(-outline[i].x, -outline[i].y);
            return rotated;
        }

        // Ring-to-ring turn rate around a station, measured the same way AppendSweep
        // measures it, so the seam can be clamped by the same rule the ribbon uses.
        private static float LocalTurnRate(TrackSpline path, float fraction)
        {
            if (path.Length <= 0f) return 0f;
            float step = SampleSpacing / path.Length;
            float from = Mathf.Max(0f, fraction - step);
            float to = Mathf.Min(1f, fraction + step);
            Vector3 t0 = path.TangentDistanceFraction(from);
            Vector3 t1 = path.TangentDistanceFraction(to);
            float span = Vector3.Distance(
                path.EvaluateDistanceFraction(from), path.EvaluateDistanceFraction(to));
            if (span < 0.000001f) return 0f;
            return Mathf.Atan2(t0.x * t1.y - t0.y * t1.x, t0.x * t1.x + t0.y * t1.y) / span;
        }

        // A closed prism over the seam ribbon: top lid, underside, two side walls and
        // two end caps.
        //
        // WINDING. Same trap as AppendChamferedSleeper, and it has cost this repo a
        // day already: the footprint's x maps through LATERAL and its y through
        // TANGENT, and lateral x tangent = -forward, so the basis is MIRRORED against
        // the board plane. A strip listed left-then-right in footprint order
        // therefore has to be enumerated in REVERSE to face the camera. The lids
        // below look backwards on the page for exactly that reason — do not "fix"
        // them. The pins are the seam island's own signed volume and its lid normals.
        private static void AppendJoinSeam(TrackSpline path,
            List<Vector3> vertices, List<int> triangles)
        {
            float distance = JoinDistance(path);
            if (distance < 0f) return;

            float fraction = distance / path.Length;
            Vector3 centre = path.EvaluateDistanceFraction(fraction);
            Vector3 tangent = path.TangentDistanceFraction(fraction);
            Vector3 lateral = new Vector3(-tangent.y, tangent.x, 0f).normalized;
            float rate = LocalTurnRate(path, fraction);

            Vector2[] outline = JoinSeamOutline();
            int count = outline.Length;
            var left = new Vector2[count];
            var right = new Vector2[count];
            OffsetChains(outline, JoinSeamHalfWidth, left, right);

            int start = vertices.Count;
            for (int face = 0; face < 2; face++)
            {
                for (int side = 0; side < 2; side++)
                {
                    Vector2[] chain = side == 0 ? left : right;
                    for (int i = 0; i < count; i++)
                    {
                        // The ribbon never reaches past where the bed itself tucks on
                        // a hairpin. It does not bite on anything authored today, but
                        // a seam hanging off the side of a tucked bed is exactly the
                        // artefact this branch already paid to remove once.
                        float offset = chain[i].x;
                        if (rate != 0f && offset * rate > 0f)
                        {
                            float limit = CurveTuckSafety / Mathf.Abs(rate);
                            if (Mathf.Abs(offset) > limit)
                                offset = Mathf.Sign(offset) * limit;
                        }
                        float z = face == 0 ? BedTopAt(offset) - JoinProud : JoinSkirtZ;
                        vertices.Add(centre + lateral * offset
                            + tangent * chain[i].y + Vector3.forward * z);
                    }
                }
            }

            int topLeft = start;
            int topRight = start + count;
            int lowLeft = start + 2 * count;
            int lowRight = start + 3 * count;

            for (int i = 0; i < count - 1; i++)
            {
                AddTriangle(triangles, topLeft + i, topLeft + i + 1, topRight + i);
                AddTriangle(triangles, topRight + i, topLeft + i + 1, topRight + i + 1);

                AddTriangle(triangles, lowLeft + i, lowRight + i, lowLeft + i + 1);
                AddTriangle(triangles, lowRight + i, lowRight + i + 1, lowLeft + i + 1);

                AddTriangle(triangles, topLeft + i, lowLeft + i, topLeft + i + 1);
                AddTriangle(triangles, lowLeft + i, lowLeft + i + 1, topLeft + i + 1);

                AddTriangle(triangles, topRight + i, topRight + i + 1, lowRight + i);
                AddTriangle(triangles, lowRight + i, topRight + i + 1, lowRight + i + 1);
            }

            AddTriangle(triangles, topLeft, topRight, lowLeft);
            AddTriangle(triangles, topRight, lowRight, lowLeft);
            AddTriangle(triangles, topLeft + count - 1, lowLeft + count - 1, topRight + count - 1);
            AddTriangle(triangles, topRight + count - 1, lowLeft + count - 1, lowRight + count - 1);
        }

        // Offsets a polyline both ways using averaged joint normals. The sharpest
        // corner on the seam is the neck-to-head bend at ~59 degrees, which widens the
        // ribbon there by 1/cos(29.5) = 1.15 — a rounded-looking corner, not a spike.
        // The head's inner offset stays at radius 0.085, so the strip cannot cross
        // itself and the prism stays a valid closed solid.
        private static void OffsetChains(Vector2[] points, float halfWidth,
            Vector2[] left, Vector2[] right)
        {
            int count = points.Length;
            for (int i = 0; i < count; i++)
            {
                Vector2 direction;
                if (i == 0) direction = points[1] - points[0];
                else if (i == count - 1) direction = points[count - 1] - points[count - 2];
                else
                {
                    Vector2 before = (points[i] - points[i - 1]).normalized;
                    Vector2 after = (points[i + 1] - points[i]).normalized;
                    direction = before + after;
                }
                if (direction.sqrMagnitude < 0.000001f) direction = Vector2.right;
                direction.Normalize();
                var normal = new Vector2(-direction.y, direction.x);
                left[i] = points[i] - normal * halfWidth;
                right[i] = points[i] + normal * halfWidth;
            }
        }

        private static void AddTriangle(List<int> triangles, int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }
    }

    [ExecuteAlways]
    internal sealed class GeneratedTrackMeshOwner : MonoBehaviour
    {
        public Mesh Mesh;

        private void OnDestroy()
        {
            if (Mesh == null) return;
            if (UnityEngine.Application.IsPlaying(gameObject)) Destroy(Mesh);
            else DestroyImmediate(Mesh);
            Mesh = null;
        }
    }
}
