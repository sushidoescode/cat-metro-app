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
        // Sunk blocks, not free-standing sleepers: only the top 0.03 clears the bed.
        // The skirt runs well below the bed's top surface so no crack can open at the
        // join, and the whole block stays closed so the mesh encloses a real volume.
        private const float SleeperLength = 0.88f;  // inside the bed's 0.94 flat top
        private const float SleeperWidth = 0.16f;
        private const float SleeperSpacing = 0.34f;
        private const float SleeperEndInset = 0.45f;
        private const float SleeperTopZ = 0.075f;
        private const float SleeperSkirtZ = 0.25f;
        private const float SleeperCorner = 0.035f;

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
