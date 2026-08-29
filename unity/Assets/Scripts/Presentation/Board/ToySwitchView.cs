using CatMetro.Presentation.Theme;
using UnityEngine;

namespace CatMetro.Presentation.Board
{
    // SWITCH-LEVERS: the LOOK.md switch — "a chunky orange lever on a teal base, tilted, with
    // an arrow showing where it will send the train" — replacing the greybox disc + clock-hand
    // arm. One toy assembly per switch:
    //
    //   switch:{id}     the ROOT — teal chamfered base block (this object's own renderer;
    //                   two-tone: MetroTeal walls, lighter BaseTopColor top face)
    //     LeverPivot    hinge at the base top, tilted LeverTiltDegrees toward local +Y
    //       Stem        wooden dowel (builtin cylinder)
    //       Knob        chunky orange head (builtin cube)
    //     Arrow         chunky orange arrow tile on the base top, pointing local +Y
    //
    // Route display is ONE rotation: BoardView calls SetDirection with the board-plane
    // direction toward the committed route's target node, and the whole assembly yaws so its
    // +Y aims there — the lever leans toward the chosen branch and the arrow swings with it.
    //
    // Contracts deliberately preserved from the greybox switch:
    //  - the root is the object named "switch:{id}"; BoardView still attaches the switch's ONE
    //    BoardElementId to it (this class adds none);
    //  - the root carries a Renderer (the teach-ring material comparison reads it) and its
    //    localScale stays Vector3.one for the teach pulse to modulate;
    //  - the TAP TARGET is untouched: TapInput resolves screen-space 48dp discs around
    //    BoardView.SwitchWorldPos (the authored node), never colliders — this class adds no
    //    collider and the root never moves off its build position;
    //  - materials are cached CreateTinted instances of the one greybox basis (the
    //    ToyTrackMeshBuilder idiom): same URP shader, no per-rebuild leak, no CreatePrimitive.
    public sealed class ToySwitchView : MonoBehaviour
    {
        public const float LeverTiltDegrees = 34f;

        // base block: chamfered prism in the board XY plane; camera-ward is negative Z
        private const float BaseHalfWidth = 0.26f;
        private const float BaseHalfLength = 0.39f; // long axis aims at the routed target
        private const float BaseCorner = 0.07f;
        private const float BaseTopZ = -0.16f;
        private const float BaseBottomZ = 0.10f;

        // teach ring (CM-UX-03's affordance, built here so its geometry lives with the toy it
        // rings). 2026-08-25 render review: a SOLID navy cylinder read as a heavy dark puck
        // with no equivalent in target-01. A true annulus teaches the same thing — the shape
        // the human ruling actually asked for — and lets the board's wood show through.
        // The hole is wider than the base's widest footprint, so a wood gap always separates
        // the ring from the toy no matter which way the switch is routed.
        public const float TeachRingInnerRadius = 0.47f;
        public const float TeachRingOuterRadius = 0.60f;
        private const int TeachRingSegments = 48;

        // lever
        private const float PivotY = -0.06f; // hinge slightly rear of the base centre
        private const float StemThickness = 0.10f;
        private const float StemLength = 0.40f;
        private const float KnobWidth = 0.24f;
        private const float KnobDepth = 0.26f;
        private const float KnobLength = 0.20f;
        private const float KnobSeat = 0.03f; // overlap into the stem so no gap shows

        // arrow, floating just above the base top — bold, phone-readable (2026-08-25 render
        // review finding 3: the first cut read mushy at board scale)
        private const float ArrowLift = 0.02f;
        public const float ArrowThickness = 0.03f;
        private const float ArrowShaftHalfWidth = 0.07f;
        private const float ArrowTailY = -0.30f;
        private const float ArrowHeadBaseY = 0.06f;
        private const float ArrowHeadHalfWidth = 0.19f;
        private const float ArrowTipY = 0.36f;

        private static Mesh _baseMesh;   // shared across instances and rebuilds
        private static Mesh _arrowMesh;
        private static Mesh _teachRingMesh;
        private static Mesh _cubeMesh;
        private static Mesh _dowelMesh;
        private static Material _baseMaterial;
        private static Material _baseTopMaterial;
        private static Material _leverMaterial;
        private static Material _stemMaterial;

        // 2026-08-25 render review finding 2: under the scene-mood raked key the flat teal
        // crushed dark, so the base is two-tone like a painted toy — MetroTeal walls, this
        // lighter token-derived teal on the top face the player actually reads. A Lerp of two
        // Palette tokens, never a literal; tests pin the exact mix.
        public static Color BaseTopColor =>
            Color.Lerp(Palette.MetroTeal, Palette.WarmPaper, 0.25f);

        // 2026-08-25 render review: target-01's lever rides a wooden dowel, and the flat cream
        // bar read as a grey sliver. A warm mid-tone mixed from two Palette tokens — the repo
        // has no wood token and BoardSurface's wood is a private literal, so this stays
        // token-derived like BaseTopColor rather than inventing a fifth hardcoded colour.
        public static Color StemWoodColor =>
            Color.Lerp(Palette.TicketOrange, Palette.InkNavy, 0.32f);

        public Transform LeverPivot { get; private set; }

        // The lever's constant toy lean, applied at the pivot; direction comes from the yaw.
        public static Quaternion LeverLocalRotation => Quaternion.Euler(LeverTiltDegrees, 0f, 0f);

        // Pure yaw law: the rotation about board Z that takes local +Y onto the given
        // board-plane direction. Pinned in EditMode.
        public static float YawDegrees(Vector2 boardPlaneDir) =>
            Mathf.Atan2(boardPlaneDir.y, boardPlaneDir.x) * Mathf.Rad2Deg - 90f;

        public static ToySwitchView Build(string switchId, Transform parent, Vector3 localPosition)
        {
            var root = new GameObject("switch:" + switchId);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            var view = root.AddComponent<ToySwitchView>();

            root.AddComponent<MeshFilter>().sharedMesh = BaseMesh();
            // submesh 0 = walls + underside (MetroTeal), submesh 1 = the lighter top face;
            // sharedMaterial (singular) stays the MetroTeal instance the teach-ring
            // comparison reads.
            var baseRenderer = root.AddComponent<MeshRenderer>();
            baseRenderer.sharedMaterials = new[] { BaseMaterial(), BaseTopMaterial() };

            var pivot = new GameObject("LeverPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, PivotY, BaseTopZ);
            pivot.localRotation = LeverLocalRotation;
            view.LeverPivot = pivot;

            // the dowel: a builtin cylinder laid along the lever axis (its mesh runs +Y, height
            // 2, so the part is rotated a quarter turn and scaled to half-length)
            AddPart("Stem", pivot, DowelMesh(),
                new Vector3(0f, 0f, -StemLength * 0.5f),
                new Vector3(StemThickness, StemLength * 0.5f, StemThickness),
                Quaternion.Euler(90f, 0f, 0f), StemMaterial());
            AddPart("Knob", pivot, CubeMesh(),
                new Vector3(0f, 0f, -(StemLength + KnobLength * 0.5f - KnobSeat)),
                new Vector3(KnobWidth, KnobDepth, KnobLength),
                Quaternion.identity, LeverMaterial());

            var arrow = new GameObject("Arrow");
            arrow.transform.SetParent(root.transform, false);
            arrow.transform.localPosition = new Vector3(0f, 0f, BaseTopZ - ArrowLift);
            arrow.AddComponent<MeshFilter>().sharedMesh = ArrowMesh();
            var arrowRenderer = arrow.AddComponent<MeshRenderer>();
            arrowRenderer.sharedMaterial = LeverMaterial();
            return view;
        }

        // boardPlaneDir: normalized direction (board local XY) from the switch node toward the
        // committed route's target node. Called by BoardView.RefreshSwitches — synchronous with
        // the tap, mutates one cached transform, allocates nothing.
        public void SetDirection(Vector2 boardPlaneDir)
        {
            if (boardPlaneDir.sqrMagnitude < 1e-6f) return;
            transform.localRotation = Quaternion.Euler(0f, 0f, YawDegrees(boardPlaneDir));
        }

        // CM-UX-03's ring, built from this file so the affordance and the toy it rings share
        // one geometry idiom. Deliberately ONE GameObject with ONE renderer and no children:
        // the band-gate test counts the onboarding board's transforms and renderers as
        // exactly alternation + 2, and a multi-part ring would break that count.
        public static Transform BuildTeachRing(string switchId, Transform parent,
            Vector3 localPosition, Material material)
        {
            var ring = new GameObject("teachring:" + switchId);
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = localPosition;
            ring.AddComponent<MeshFilter>().sharedMesh = TeachRingMesh();
            var ringRenderer = ring.AddComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = material;
            return ring.transform;
        }

        private static void AddPart(string name, Transform parent, Mesh mesh,
            Vector3 localPosition, Vector3 scale, Quaternion localRotation, Material material)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
        }

        private static Mesh CubeMesh()
        {
            if (_cubeMesh == null)
                _cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            return _cubeMesh;
        }

        private static Mesh DowelMesh()
        {
            if (_dowelMesh == null)
                _dowelMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            return _dowelMesh;
        }

        // Flat annulus in the board plane, camera-facing, no thickness needed: nothing ever
        // sees its back. Same winding law as the rest of the toy — camera-facing triangles
        // enumerate the CCW rim in reverse.
        private static Mesh TeachRingMesh()
        {
            if (_teachRingMesh != null) return _teachRingMesh;
            var vertices = new Vector3[TeachRingSegments * 2];
            for (int i = 0; i < TeachRingSegments; i++)
            {
                float angle = i * 2f * Mathf.PI / TeachRingSegments;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                vertices[i * 2] = new Vector3(
                    cos * TeachRingInnerRadius, sin * TeachRingInnerRadius, 0f);
                vertices[i * 2 + 1] = new Vector3(
                    cos * TeachRingOuterRadius, sin * TeachRingOuterRadius, 0f);
            }
            var triangles = new System.Collections.Generic.List<int>(TeachRingSegments * 6);
            for (int i = 0; i < TeachRingSegments; i++)
            {
                int inner = i * 2;
                int outer = inner + 1;
                int nextInner = ((i + 1) % TeachRingSegments) * 2;
                int nextOuter = nextInner + 1;
                AddTriangle(triangles, inner, nextOuter, outer);
                AddTriangle(triangles, inner, nextInner, nextOuter);
            }
            _teachRingMesh = new Mesh { name = "Toy switch teach ring" };
            _teachRingMesh.SetVertices(vertices);
            _teachRingMesh.SetTriangles(triangles, 0);
            _teachRingMesh.RecalculateNormals();
            _teachRingMesh.RecalculateBounds();
            return _teachRingMesh;
        }

        private static Material BaseMaterial()
        {
            if (_baseMaterial == null)
                _baseMaterial = GreyboxMaterial.CreateTinted(
                    "Toy Switch — Teal Base", Palette.MetroTeal);
            return _baseMaterial;
        }

        private static Material BaseTopMaterial()
        {
            if (_baseTopMaterial == null)
                _baseTopMaterial = GreyboxMaterial.CreateTinted(
                    "Toy Switch — Teal Base Top", BaseTopColor);
            return _baseTopMaterial;
        }

        private static Material LeverMaterial()
        {
            if (_leverMaterial == null)
                _leverMaterial = GreyboxMaterial.CreateTinted(
                    "Toy Switch — Orange Lever", Palette.TicketOrange);
            return _leverMaterial;
        }

        private static Material StemMaterial()
        {
            if (_stemMaterial == null)
                _stemMaterial = GreyboxMaterial.CreateTinted(
                    "Toy Switch — Wood Stem", StemWoodColor);
            return _stemMaterial;
        }

        // Chamfered octagonal prism. WINDING LAW (2026-08-25 render review: the first cut
        // copied the sleeper fan verbatim and every camera-facing triangle was culled — the
        // sleeper builder maps footprint.x through the LATERAL axis, which mirrors its
        // polygon): the camera looks from -Z toward +Z, and Unity front faces wind CLOCKWISE
        // as seen by the camera, so camera-facing triangles here enumerate the CCW footprint
        // in REVERSE. Vertices are split per face so RecalculateNormals yields true flat
        // facets — the top face gets an honest camera-facing normal instead of a smeared
        // 45-degree average, which is what let the raked key light crush it.
        // Submesh 0 = walls + underside (MetroTeal), submesh 1 = top face (BaseTopColor).
        private static Mesh BaseMesh()
        {
            if (_baseMesh != null) return _baseMesh;
            var footprint = new[] // counter-clockwise in board XY
            {
                new Vector2(-BaseHalfWidth + BaseCorner, -BaseHalfLength),
                new Vector2(BaseHalfWidth - BaseCorner, -BaseHalfLength),
                new Vector2(BaseHalfWidth, -BaseHalfLength + BaseCorner),
                new Vector2(BaseHalfWidth, BaseHalfLength - BaseCorner),
                new Vector2(BaseHalfWidth - BaseCorner, BaseHalfLength),
                new Vector2(-BaseHalfWidth + BaseCorner, BaseHalfLength),
                new Vector2(-BaseHalfWidth, BaseHalfLength - BaseCorner),
                new Vector2(-BaseHalfWidth, -BaseHalfLength + BaseCorner),
            };
            int n = footprint.Length;
            var vertices = new System.Collections.Generic.List<Vector3>(2 * n + 4 * n);
            for (int i = 0; i < n; i++) // 0..7: top face only
                vertices.Add(new Vector3(footprint[i].x, footprint[i].y, BaseTopZ));
            for (int i = 0; i < n; i++) // 8..15: underside only
                vertices.Add(new Vector3(footprint[i].x, footprint[i].y, BaseBottomZ));

            var topTriangles = new System.Collections.Generic.List<int>(18);
            var wallTriangles = new System.Collections.Generic.List<int>(66);
            for (int i = 1; i < n - 1; i++)
            {
                // top face toward the camera: CCW footprint enumerated in reverse
                AddTriangle(topTriangles, 0, i + 1, i);
                // underside toward the table: forward enumeration faces +Z
                AddTriangle(wallTriangles, 8, 8 + i, 8 + i + 1);
            }
            for (int edge = 0; edge < n; edge++)
            {
                int next = (edge + 1) % n;
                int a = vertices.Count; // per-edge vertices: crisp facet normals
                vertices.Add(new Vector3(footprint[edge].x, footprint[edge].y, BaseTopZ));
                vertices.Add(new Vector3(footprint[next].x, footprint[next].y, BaseTopZ));
                vertices.Add(new Vector3(footprint[next].x, footprint[next].y, BaseBottomZ));
                vertices.Add(new Vector3(footprint[edge].x, footprint[edge].y, BaseBottomZ));
                AddTriangle(wallTriangles, a, a + 2, a + 3);
                AddTriangle(wallTriangles, a, a + 1, a + 2);
            }

            _baseMesh = new Mesh { name = "Toy switch base" };
            _baseMesh.SetVertices(vertices);
            _baseMesh.subMeshCount = 2;
            _baseMesh.SetTriangles(wallTriangles, 0);
            _baseMesh.SetTriangles(topTriangles, 1);
            _baseMesh.RecalculateNormals();
            _baseMesh.RecalculateBounds();
            return _baseMesh;
        }

        // Bold arrow pointing +Y — shaft quad plus a wide head triangle, EXTRUDED into a thin
        // solid tile (2026-08-25 render review: the single-sided flat decal was backface-culled
        // and invisible; a closed solid cannot be hidden by any winding or mirrored transform).
        // Same winding law as BaseMesh: camera-facing triangles enumerate the CCW outline in
        // reverse; the front face sits ArrowThickness toward the camera.
        private static Mesh ArrowMesh()
        {
            if (_arrowMesh != null) return _arrowMesh;
            var outline = new[] // counter-clockwise in board XY
            {
                new Vector2(-ArrowShaftHalfWidth, ArrowTailY),     // 0
                new Vector2(ArrowShaftHalfWidth, ArrowTailY),      // 1
                new Vector2(ArrowShaftHalfWidth, ArrowHeadBaseY),  // 2
                new Vector2(ArrowHeadHalfWidth, ArrowHeadBaseY),   // 3
                new Vector2(0f, ArrowTipY),                        // 4
                new Vector2(-ArrowHeadHalfWidth, ArrowHeadBaseY),  // 5
                new Vector2(-ArrowShaftHalfWidth, ArrowHeadBaseY), // 6
            };
            int n = outline.Length;
            var vertices = new System.Collections.Generic.List<Vector3>(2 * n + 4 * n);
            for (int i = 0; i < n; i++) // 0..6: front face (camera side)
                vertices.Add(new Vector3(outline[i].x, outline[i].y, -ArrowThickness));
            for (int i = 0; i < n; i++) // 7..13: back face (rests on the base top)
                vertices.Add(new Vector3(outline[i].x, outline[i].y, 0f));

            var triangles = new System.Collections.Generic.List<int>(60);
            // front face toward the camera: reversed enumeration
            AddTriangle(triangles, 0, 2, 1);
            AddTriangle(triangles, 0, 6, 2);
            AddTriangle(triangles, 5, 4, 3);
            // back face toward the table: forward enumeration
            AddTriangle(triangles, 7, 8, 9);
            AddTriangle(triangles, 7, 9, 13);
            AddTriangle(triangles, 12, 10, 11);
            // walls, split vertices per edge for crisp facets
            for (int edge = 0; edge < n; edge++)
            {
                int next = (edge + 1) % n;
                int a = vertices.Count;
                vertices.Add(new Vector3(outline[edge].x, outline[edge].y, -ArrowThickness));
                vertices.Add(new Vector3(outline[next].x, outline[next].y, -ArrowThickness));
                vertices.Add(new Vector3(outline[next].x, outline[next].y, 0f));
                vertices.Add(new Vector3(outline[edge].x, outline[edge].y, 0f));
                AddTriangle(triangles, a, a + 2, a + 3);
                AddTriangle(triangles, a, a + 1, a + 2);
            }

            _arrowMesh = new Mesh { name = "Toy switch arrow" };
            _arrowMesh.SetVertices(vertices);
            _arrowMesh.SetTriangles(triangles, 0);
            _arrowMesh.RecalculateNormals();
            _arrowMesh.RecalculateBounds();
            return _arrowMesh;
        }

        private static void AddTriangle(System.Collections.Generic.List<int> triangles,
            int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }
    }
}
