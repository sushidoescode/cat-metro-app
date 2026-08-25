using CatMetro.Presentation.Theme;
using UnityEngine;

namespace CatMetro.Presentation.Board
{
    // SWITCH-LEVERS: the LOOK.md switch — "a chunky orange lever on a teal base, tilted, with
    // an arrow showing where it will send the train" — replacing the greybox disc + clock-hand
    // arm. One toy assembly per switch:
    //
    //   switch:{id}     the ROOT — teal chamfered base block (this object's own renderer),
    //     LeverPivot    hinge at the base top, tilted LeverTiltDegrees toward local +Y
    //       Stem        cream shaft (builtin cube)
    //       Knob        chunky orange head (builtin cube)
    //     Arrow         flat orange arrow on the base top, pointing local +Y
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

        // lever
        private const float PivotY = -0.06f; // hinge slightly rear of the base centre
        private const float StemThickness = 0.09f;
        private const float StemLength = 0.40f;
        private const float KnobWidth = 0.24f;
        private const float KnobDepth = 0.26f;
        private const float KnobLength = 0.20f;
        private const float KnobSeat = 0.03f; // overlap into the stem so no gap shows

        // arrow, floating just above the base top
        private const float ArrowLift = 0.015f;
        private const float ArrowShaftHalfWidth = 0.055f;
        private const float ArrowTailY = -0.30f;
        private const float ArrowHeadBaseY = 0.10f;
        private const float ArrowHeadHalfWidth = 0.15f;
        private const float ArrowTipY = 0.34f;

        private static Mesh _baseMesh;   // shared across instances and rebuilds
        private static Mesh _arrowMesh;
        private static Mesh _cubeMesh;
        private static Material _baseMaterial;
        private static Material _leverMaterial;
        private static Material _stemMaterial;

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
            root.AddComponent<MeshRenderer>().sharedMaterial = BaseMaterial();

            var pivot = new GameObject("LeverPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, PivotY, BaseTopZ);
            pivot.localRotation = LeverLocalRotation;
            view.LeverPivot = pivot;

            AddCubePart("Stem", pivot,
                new Vector3(0f, 0f, -StemLength * 0.5f),
                new Vector3(StemThickness, StemThickness, StemLength), StemMaterial());
            AddCubePart("Knob", pivot,
                new Vector3(0f, 0f, -(StemLength + KnobLength * 0.5f - KnobSeat)),
                new Vector3(KnobWidth, KnobDepth, KnobLength), LeverMaterial());

            var arrow = new GameObject("Arrow");
            arrow.transform.SetParent(root.transform, false);
            arrow.transform.localPosition = new Vector3(0f, 0f, BaseTopZ - ArrowLift);
            arrow.AddComponent<MeshFilter>().sharedMesh = ArrowMesh();
            arrow.AddComponent<MeshRenderer>().sharedMaterial = LeverMaterial();
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

        private static void AddCubePart(string name, Transform parent, Vector3 localPosition,
            Vector3 scale, Material material)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = CubeMesh();
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Mesh CubeMesh()
        {
            if (_cubeMesh == null)
                _cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            return _cubeMesh;
        }

        private static Material BaseMaterial()
        {
            if (_baseMaterial == null)
                _baseMaterial = GreyboxMaterial.CreateTinted(
                    "Toy Switch — Teal Base", Palette.MetroTeal);
            return _baseMaterial;
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
                    "Toy Switch — Cream Stem", Palette.CreamCard);
            return _stemMaterial;
        }

        // Chamfered octagonal prism, the ToyTrackMeshBuilder sleeper pattern: front face on the
        // camera side (smaller Z), back face toward the table, walls between.
        private static Mesh BaseMesh()
        {
            if (_baseMesh != null) return _baseMesh;
            var footprint = new[]
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
            var vertices = new Vector3[footprint.Length * 2];
            for (int face = 0; face < 2; face++)
            {
                float z = face == 0 ? BaseTopZ : BaseBottomZ;
                for (int i = 0; i < footprint.Length; i++)
                    vertices[face * footprint.Length + i] =
                        new Vector3(footprint[i].x, footprint[i].y, z);
            }
            var triangles = new System.Collections.Generic.List<int>(96);
            for (int i = 1; i < footprint.Length - 1; i++)
            {
                AddTriangle(triangles, 0, i, i + 1);
                AddTriangle(triangles, 8, 8 + i + 1, 8 + i);
            }
            for (int i = 0; i < footprint.Length; i++)
            {
                int next = (i + 1) % footprint.Length;
                AddTriangle(triangles, i, 8 + i, 8 + next);
                AddTriangle(triangles, i, 8 + next, next);
            }
            _baseMesh = new Mesh { name = "Toy switch base" };
            _baseMesh.SetVertices(vertices);
            _baseMesh.SetTriangles(triangles, 0);
            _baseMesh.RecalculateNormals();
            _baseMesh.RecalculateBounds();
            return _baseMesh;
        }

        // Flat arrow in the board plane pointing +Y: shaft quad plus head triangle, camera-side
        // face only (the board is never seen from behind).
        private static Mesh ArrowMesh()
        {
            if (_arrowMesh != null) return _arrowMesh;
            var vertices = new[]
            {
                new Vector3(-ArrowShaftHalfWidth, ArrowTailY, 0f),     // 0
                new Vector3(ArrowShaftHalfWidth, ArrowTailY, 0f),      // 1
                new Vector3(ArrowShaftHalfWidth, ArrowHeadBaseY, 0f),  // 2
                new Vector3(ArrowHeadHalfWidth, ArrowHeadBaseY, 0f),   // 3
                new Vector3(0f, ArrowTipY, 0f),                        // 4
                new Vector3(-ArrowHeadHalfWidth, ArrowHeadBaseY, 0f),  // 5
                new Vector3(-ArrowShaftHalfWidth, ArrowHeadBaseY, 0f), // 6
            };
            var triangles = new System.Collections.Generic.List<int>(9);
            AddTriangle(triangles, 0, 1, 2);
            AddTriangle(triangles, 0, 2, 6);
            AddTriangle(triangles, 5, 3, 4);
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
