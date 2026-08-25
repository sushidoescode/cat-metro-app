using CatMetro.Presentation.Theme;
using UnityEngine;

namespace CatMetro.Presentation.Board
{
    // LOOK step 6 ("Put the cats on trains"): the toy consist a train slot renders as — a
    // little steam engine leading one cream open carriage with the slot's cat seated in it.
    // One carriage, not three: in the Domain a train IS one riding cat (a wave emission
    // carries a single Color and delivery zeroes the slot), so engine + one occupied carriage
    // is the honest consist — no seat on screen that state can't fill.
    //
    // Transform contract: the ROOT stays unrotated at the head anchor
    // (BoardTrackIntegrationTests pins root.localPosition == spline sample + the -0.2 z lift,
    // the same anchor the old capsule used), so every child pose is a plain board-local delta.
    // The engine sits on the anchor; the carriage trails CarriageOffset arc-length units back
    // along the rendered spline (TrainConsistLayout owns the edge-boundary law), each vehicle
    // carrying its own heading so the consist bends through curves.
    //
    // House rules: parts are built like BoardSurface.CreatePart — new GameObject + builtin
    // mesh + the shared greybox pipeline — never the primitive factory (no colliders on
    // visual-only objects; switch taps must pass through) and never a BoardElementId (the root
    // carries the one authored "train" id; decoration stays out of the inventory). Builtin
    // meshes and the statically cached tinted materials (ToyTrackMeshBuilder's shape) mean a
    // consist owns no generated assets, so teardown is just the GameObject's own destruction.
    public sealed class ToyTrainView : MonoBehaviour
    {
        // Arc-length from the engine anchor back to the carriage centre: half an engine
        // (0.23) + a toy-tight coupling gap (0.07) + half a carriage (0.18), sized against
        // the 0.5 track gauge (ToyTrackMeshBuilder.RailOffset * 2).
        public const float CarriageOffset = 0.48f;

        // The pinned head anchor lift off the board plane (the old capsule's -0.2). Part
        // z-offsets below are anchor-local: +z points down into the table, and the rail
        // crowns (board z +0.035) sit at +0.235, which is where the chassis parts bottom out.
        private const float HeadAnchorZ = -0.2f;

        // ── Cat geometry ────────────────────────────────────────────────────────────────
        // Sized from the 2026-08-25 render verdict, which showed a smooth coloured ball: the
        // 0.05 ears cleared the 0.19 crown by 0.023 (12% of head diameter) and, at the fitted
        // gameplay zoom (~93 px per board unit, head = 17.7 px), broke the head's projected
        // silhouette by at most 2.3 px — and that only on ONE side. The board tilt puts
        // table-up 48 degrees off the view axis, so as a train turns, the ears' lateral axis
        // swings through the view direction and the far ear projects INSIDE the head's disc
        // (measured worst case: 3.4 px inside, i.e. wholly buried). Two fixes, together:
        //
        //  1. The cat holds a FIXED board yaw facing the camera (CatBoardYaw) instead of
        //     turning with the carriage. That makes the ear axis exactly perpendicular to the
        //     view at every heading, so both ears project at full width, always — the
        //     heading-dependent burial is gone by construction, not by tuning.
        //  2. Ears grow to a flat 0.115 wedge set out at 0.080. Silhouette excess goes
        //     2.3 px (best case) -> 6.9 px (every case); projected area outside the head disc
        //     goes 8.7 px^2 -> 39.6 px^2 per ear.
        //
        // Height is the constrained axis, not width: the switch discs are a slab from board z
        // -0.48 to -0.32, so an ear tall enough to clear the crown on its own would have to
        // reach board z -0.408, past the disc mid-plane. So the ears lean on lateral spread
        // (unconstrained, and it projects at ~full strength once the yaw is fixed) and only
        // reach board z -0.371 — into the disc's lower half, where a tip is simply occluded
        // by the opaque disc as the cat passes under it.
        //
        // A face does the rest of the work: at a 17.7 px head, two near-black eyes and a pale
        // muzzle read by CONTRAST, which costs no silhouette and no headroom at all. Target-02
        // reads exactly this way at thumbnail size.
        //
        // r3 postscript — none of the above rendered, for a reason none of it was to blame for.
        // Every size below is a WORLD size and reaches the transform through
        // ScaleForWorldSize, because the builtin sphere mesh is ~3.33 units across, not 1.
        // The head was therefore rendering at 0.633 rather than 0.19 and enclosed the whole
        // face: the r3 slot measured the farthest feature 0.17 from the head centre against a
        // head extent of 0.31. The ear placement was right the whole time (that same 0.17 is
        // exactly where this file puts the ear's outer corner); only the head was wrong.

        // Public so a test can assert the head RENDERS at the diameter this constant names.
        // That assertion is the one that would have caught the r3 mesh-scale bug on frame one:
        // every law was computed in authored space, which did not match the rendered hierarchy.
        public const float HeadDiameter = 0.19f;
        private const float HeadCenterZ = -0.037f;
        private const float EarThickness = 0.038f; // along travel; ears are flat wedges
        private const float EarSize = 0.115f;      // the 45-degree diamond's box size
        private const float EarLateral = 0.080f;
        private const float EarCenterZ = -0.090f;
        private const float EyeSize = 0.048f;
        private static readonly Vector3 EyeOffset = new Vector3(0.0528f, 0.0369f, -0.0844f);
        private static readonly Vector3 MuzzleOffset = new Vector3(0.0768f, 0f, -0.0658f);
        private static readonly Vector3 MuzzleSize = new Vector3(0.050f, 0.068f, 0.044f);

        /// <summary>
        /// Board-local yaw that turns a cat's +x face toward the camera. Derived from the
        /// diorama tilt so it tracks any re-authoring of it (see BoardSceneLook.BoardTilt).
        /// </summary>
        public static float CameraFacingYawDegrees(Quaternion boardTilt)
        {
            // The camera is identity-rotated and orthographic, so it looks along world +z.
            Vector3 viewLocal = Quaternion.Inverse(boardTilt) * Vector3.forward;
            // Face back along it, flattened into the board plane.
            return Mathf.Atan2(-viewLocal.y, -viewLocal.x) * Mathf.Rad2Deg;
        }

        /// <summary>The fixed board-local yaw every seated cat holds (about -131.4 degrees).</summary>
        public static float CatBoardYaw => CameraFacingYawDegrees(BoardSceneLook.BoardTilt);

        private static Material _navyMaterial;
        private static Material _creamMaterial;
        private static Material _catBasisMaterial;
        private static Mesh _cubeMesh;
        private static Mesh _sphereMesh;
        private static Mesh _cylinderMesh;

        private Transform _engine;
        private Transform _carriage;
        private Transform _cat;
        private MeshRenderer[] _catRenderers; // head + ears — tinted per cat via property block

        // The authored graph's edge endpoints (BoardView's own arrays) — the authority that
        // decides whether remembered history is a path the train could actually have rolled.
        private int[] _edgeFrom;
        private int[] _edgeTo;

        // Presentation-side memory the sim doesn't carry: the edge the head is (or was last)
        // on, one edge of history behind it, and the last applied heading for parked frames.
        private short _seenTrainId;
        private int _currentEdge = -1;
        private int _previousEdge = -1;
        private float _headingDegrees;
        private Color _appliedCatColor;
        private bool _catColorApplied;

        public static ToyTrainView Create(Transform parent, string name,
            int[] edgeFrom, int[] edgeTo)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<ToyTrainView>();
            view._edgeFrom = edgeFrom;
            view._edgeTo = edgeTo;
            view.BuildConsist();
            return view;
        }

        // Slot slaved to sim state: a reused slot (new train Id) must not inherit the previous
        // occupant's edge history or tint. Colors come from the caller's Palette mapping.
        public void SyncSlot(short trainId, Color catColor)
        {
            if (trainId != _seenTrainId)
            {
                _seenTrainId = trainId;
                _currentEdge = -1;
                _previousEdge = -1;
                _headingDegrees = 0f;
            }
            if (!_catColorApplied || catColor != _appliedCatColor)
            {
                _appliedCatColor = catColor;
                _catColorApplied = true;
                var properties = new MaterialPropertyBlock();
                properties.SetColor("_BaseColor", catColor);
                properties.SetColor("_Color", catColor);
                for (int i = 0; i < _catRenderers.Length; i++)
                    _catRenderers[i].SetPropertyBlock(properties);
            }
        }

        public void PlaceOnEdge(TrackSplineGraph paths, int edgeIndex, float headDistance)
        {
            if (edgeIndex != _currentEdge)
            {
                // Record the edge the head just left — but only when the graph agrees the
                // head could have rolled straight through (its end feeds this edge's start).
                // A multi-tick catch-up frame (pause/resume hitch) can skip a whole edge
                // between renders; trailing along non-adjacent history would put the
                // carriage somewhere the train never was, so it clamps instead.
                _previousEdge = _currentEdge >= 0
                    && _edgeTo[_currentEdge] == _edgeFrom[edgeIndex]
                    ? _currentEdge : -1;
                _currentEdge = edgeIndex;
            }
            var path = paths.Path(edgeIndex);
            headDistance = Mathf.Clamp(headDistance, 0f, path.Length);
            float fraction = path.Length > 0f ? headDistance / path.Length : 0f;
            Vector3 headPosition = path.EvaluateDistanceFraction(fraction);
            transform.localPosition = headPosition + new Vector3(0f, 0f, HeadAnchorZ);
            _headingDegrees = HeadingDegrees(path.TangentDistanceFraction(fraction));
            _engine.localRotation = Quaternion.Euler(0f, 0f, _headingDegrees);
            PlaceTrailing(paths, edgeIndex, headDistance, headPosition);
        }

        // Parked at a node: the head anchor is the node itself and the consist trails back
        // along the edge it arrived on — but only when that remembered edge actually ENDS at
        // this node (then the spline's end point IS the node position, so the two anchors
        // agree exactly). A catch-up frame can land the head at a node the remembered edge
        // never touches; foreign history is discarded and the whole consist parks on the node
        // point — the same documented clamp a source-queued train gets, pulling apart on its
        // first edge frame like a depot departure.
        public void PlaceAtNode(TrackSplineGraph paths, int nodeIndex, Vector3 nodeLocal)
        {
            transform.localPosition = nodeLocal + new Vector3(0f, 0f, HeadAnchorZ);
            if (_currentEdge >= 0 && _edgeTo[_currentEdge] == nodeIndex)
            {
                var arrival = paths.Path(_currentEdge);
                _headingDegrees = HeadingDegrees(arrival.TangentDistanceFraction(1f));
                _engine.localRotation = Quaternion.Euler(0f, 0f, _headingDegrees);
                PlaceTrailing(paths, _currentEdge, arrival.Length, nodeLocal);
                return;
            }
            _currentEdge = -1;  // the head is provably somewhere this history never led
            _previousEdge = -1;
            _engine.localRotation = Quaternion.Euler(0f, 0f, _headingDegrees);
            _carriage.localPosition = Vector3.zero;
            SetCarriageHeading(_headingDegrees);
        }

        private void PlaceTrailing(TrackSplineGraph paths, int headEdge, float headDistance,
            Vector3 headPosition)
        {
            float previousLength = _previousEdge >= 0 ? paths.Path(_previousEdge).Length : -1f;
            var sample = TrainConsistLayout.ResolveBehind(headDistance, CarriageOffset,
                paths.Path(headEdge).Length, previousLength);
            var path = paths.Path(sample.OnPreviousEdge ? _previousEdge : headEdge);
            float fraction = path.Length > 0f ? sample.Distance / path.Length : 0f;
            // The root is unrotated, so a board-local delta IS the child's local pose.
            _carriage.localPosition = path.EvaluateDistanceFraction(fraction) - headPosition;
            SetCarriageHeading(HeadingDegrees(path.TangentDistanceFraction(fraction)));
        }

        // The carriage turns with the track; the CAT does not. Counter-rotating the cat by the
        // carriage's own heading leaves it at a fixed board-local yaw — the one that squares
        // its face and ear axis to the diorama camera — so a passenger reads identically on a
        // straight, through a curve, and parked at a node. This is the structural half of the
        // invisible-ears fix: without it, no ear size survives every heading.
        private void SetCarriageHeading(float degrees)
        {
            _carriage.localRotation = Quaternion.Euler(0f, 0f, degrees);
            _cat.localRotation = Quaternion.Euler(0f, 0f, CatBoardYaw - degrees);
        }

        // Vehicles are modelled along +x; travel tangents live in the board's XY plane.
        private static float HeadingDegrees(Vector3 tangent) =>
            Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;

        private void BuildConsist()
        {
            _engine = new GameObject("Engine").transform;
            _engine.SetParent(transform, false);
            // Navy chassis under a cream boiler and cab, navy roof and funnel — the little
            // steam engine from target-01, in palette tokens.
            CreatePart("Chassis", _engine, CubeMesh(),
                new Vector3(0f, 0f, 0.200f), new Vector3(0.46f, 0.30f, 0.07f),
                Quaternion.identity, NavyMaterial());
            // 0.28 long, not 0.14: the old localScale of 0.14 was written against the builtin
            // cylinder being 2 units tall on y. That convention is now stated as the world
            // size it always meant, so the part renders identically while no longer depending
            // on a reader knowing the mesh's intrinsic length.
            CreatePart("Boiler", _engine, CylinderMesh(),
                new Vector3(0.08f, 0f, 0.065f), new Vector3(0.20f, 0.28f, 0.20f),
                Quaternion.Euler(0f, 0f, 90f), CreamMaterial()); // cylinder length onto +x
            CreatePart("Cab", _engine, CubeMesh(),
                new Vector3(-0.12f, 0f, 0.055f), new Vector3(0.18f, 0.26f, 0.22f),
                Quaternion.identity, CreamMaterial());
            CreatePart("CabRoof", _engine, CubeMesh(),
                new Vector3(-0.12f, 0f, -0.08f), new Vector3(0.22f, 0.30f, 0.05f),
                Quaternion.identity, NavyMaterial());
            CreatePart("Funnel", _engine, CylinderMesh(),
                new Vector3(0.15f, 0f, -0.085f), new Vector3(0.09f, 0.10f, 0.09f),
                Quaternion.Euler(90f, 0f, 0f), NavyMaterial()); // cylinder axis off the board

            _carriage = new GameObject("Carriage").transform;
            _carriage.SetParent(transform, false);
            CreatePart("Chassis", _carriage, CubeMesh(),
                new Vector3(0f, 0f, 0.205f), new Vector3(0.36f, 0.30f, 0.06f),
                Quaternion.identity, NavyMaterial());
            CreatePart("Body", _carriage, CubeMesh(),
                new Vector3(0f, 0f, 0.085f), new Vector3(0.34f, 0.28f, 0.18f),
                Quaternion.identity, CreamMaterial());

            // The passenger: a chibi head at 68% of the body's width (target-02 reads
            // 60-70%) with its lower THIRD sunk below the brim, so it sits IN the open box
            // rather than ON it — the seating the 2026-08-25 render confirmed, left alone.
            // Head and ears carry the line tint; the face is deliberately OUTSIDE the tinted
            // set, so the eyes stay near-black and the muzzle cream whatever colour the cat
            // is. Ears are 45-degree diamonds anchored in the head, splayed up and out.
            _cat = new GameObject("Cat").transform;
            _cat.SetParent(_carriage, false);
            _catRenderers = new[]
            {
                CreatePart("Head", _cat, SphereMesh(),
                    new Vector3(0f, 0f, HeadCenterZ),
                    new Vector3(HeadDiameter, HeadDiameter, HeadDiameter),
                    Quaternion.identity, CatBasisMaterial()),
                CreatePart("EarLeft", _cat, CubeMesh(),
                    new Vector3(0f, EarLateral, EarCenterZ),
                    new Vector3(EarThickness, EarSize, EarSize),
                    Quaternion.Euler(45f, 0f, 0f), CatBasisMaterial()),
                CreatePart("EarRight", _cat, CubeMesh(),
                    new Vector3(0f, -EarLateral, EarCenterZ),
                    new Vector3(EarThickness, EarSize, EarSize),
                    Quaternion.Euler(45f, 0f, 0f), CatBasisMaterial()),
            };

            // The face. Because the cat holds a fixed camera-facing yaw, these sit at a known
            // screen position for every train on every heading — so they can be placed once,
            // square to the camera, instead of hedged against rotation. Each is a builtin
            // sphere sunk into the head so it reads as a dome on the surface, never a decal
            // that could z-fight. Reuses the engine's two cached materials: no new material,
            // no property block, nothing to tear down.
            CreatePart("EyeLeft", _cat, SphereMesh(),
                new Vector3(EyeOffset.x, EyeOffset.y, EyeOffset.z),
                new Vector3(EyeSize, EyeSize, EyeSize),
                Quaternion.identity, NavyMaterial());
            CreatePart("EyeRight", _cat, SphereMesh(),
                new Vector3(EyeOffset.x, -EyeOffset.y, EyeOffset.z),
                new Vector3(EyeSize, EyeSize, EyeSize),
                Quaternion.identity, NavyMaterial());
            CreatePart("Muzzle", _cat, SphereMesh(),
                MuzzleOffset, MuzzleSize,
                Quaternion.identity, CreamMaterial());

            SetCarriageHeading(0f); // a consist faces the camera before its first placement
        }

        // BoardSurface.CreatePart's shape: builtin mesh, no collider, project material only —
        // but taking the size the part should OCCUPY IN THE WORLD, never a raw localScale.
        // See ScaleForWorldSize: a localScale only means what you think it means when the mesh
        // happens to be unit-sized, and one of the three we use is not.
        private static MeshRenderer CreatePart(string name, Transform parent, Mesh mesh,
            Vector3 position, Vector3 worldSize, Quaternion rotation, Material material)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = ScaleForWorldSize(mesh, worldSize);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            return renderer;
        }

        // Builtin meshes are NOT unit-sized, and assuming they are is what made the cat a bare
        // ball for three rounds. Resources.GetBuiltinResource<Mesh>("Sphere.fbx") returns
        // pSphere1, whose bounds are ~3.33 units across; the 2026-08-25 r3 slot measured a
        // head authored at 0.19 rendering 0.633 across, which swallowed every ear, eye and
        // muzzle whole (the features were correct all along — a head-off capture showed them
        // present, coloured and correctly arranged). Cube.fbx is unit and Cylinder.fbx is
        // 2 long on y, so dividing by the mesh's own bounds is a no-op for the parts that were
        // already right and a correction for the ones that were not. Deriving at runtime means
        // this holds for whatever mesh Unity actually hands back, in any future version.
        private static Vector3 ScaleForWorldSize(Mesh mesh, Vector3 worldSize)
        {
            Vector3 intrinsic = mesh.bounds.size;
            return new Vector3(
                intrinsic.x > 1e-6f ? worldSize.x / intrinsic.x : worldSize.x,
                intrinsic.y > 1e-6f ? worldSize.y / intrinsic.y : worldSize.y,
                intrinsic.z > 1e-6f ? worldSize.z / intrinsic.z : worldSize.z);
        }

        private static Material NavyMaterial()
        {
            if (_navyMaterial == null)
                _navyMaterial = GreyboxMaterial.CreateTinted(
                    "Toy Train — Navy Trim", Palette.InkNavy);
            return _navyMaterial;
        }

        private static Material CreamMaterial()
        {
            if (_creamMaterial == null)
                _creamMaterial = GreyboxMaterial.CreateTinted(
                    "Toy Train — Cream Body", Palette.CreamCard);
            return _creamMaterial;
        }

        private static Material CatBasisMaterial()
        {
            if (_catBasisMaterial == null)
                _catBasisMaterial = GreyboxMaterial.CreateTinted(
                    "Toy Train — Cat", Color.white); // neutral basis; the line color is a
            return _catBasisMaterial;                 // per-renderer property block
        }

        private static Mesh CubeMesh()
        {
            if (_cubeMesh == null)
                _cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            return _cubeMesh;
        }

        private static Mesh SphereMesh()
        {
            if (_sphereMesh == null)
                _sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            return _sphereMesh;
        }

        private static Mesh CylinderMesh()
        {
            if (_cylinderMesh == null)
                _cylinderMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            return _cylinderMesh;
        }
    }
}
