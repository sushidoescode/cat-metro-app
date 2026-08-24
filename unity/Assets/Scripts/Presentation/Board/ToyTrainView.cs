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

        // The pinned head anchor lift off the board plane (the old capsule's -0.2), and the
        // rail-top plane measured from that anchor (rail crowns sit at board z +0.035, so
        // +0.235 in anchor-local z — +z points down into the table).
        private const float HeadAnchorZ = -0.2f;
        private const float RailTopZ = 0.235f;

        private static Material _navyMaterial;
        private static Material _creamMaterial;
        private static Material _catBasisMaterial;
        private static Mesh _cubeMesh;
        private static Mesh _sphereMesh;
        private static Mesh _cylinderMesh;

        private Transform _engine;
        private Transform _carriage;
        private MeshRenderer[] _catRenderers; // head + ears — tinted per cat via property block

        // Presentation-side memory the sim doesn't carry: the edge the head is (or was last)
        // on, one edge of history behind it, and the last applied heading for parked frames.
        private short _seenTrainId;
        private int _currentEdge = -1;
        private int _previousEdge = -1;
        private float _headingDegrees;
        private Color _appliedCatColor;
        private bool _catColorApplied;

        public static ToyTrainView Create(Transform parent, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<ToyTrainView>();
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
                _previousEdge = _currentEdge; // record the edge the head just left
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
        // along the edge it arrived on (still remembered as _currentEdge — the spline's end
        // point IS the node position, so the two anchors agree exactly). A source-queued train
        // that never travelled has no history: the whole consist parks on the node point and
        // pulls apart on its first edge frame, reading as a depot departure.
        public void PlaceAtNode(TrackSplineGraph paths, Vector3 nodeLocal)
        {
            transform.localPosition = nodeLocal + new Vector3(0f, 0f, HeadAnchorZ);
            if (_currentEdge >= 0)
            {
                var arrival = paths.Path(_currentEdge);
                _headingDegrees = HeadingDegrees(arrival.TangentDistanceFraction(1f));
                _engine.localRotation = Quaternion.Euler(0f, 0f, _headingDegrees);
                PlaceTrailing(paths, _currentEdge, arrival.Length, nodeLocal);
                return;
            }
            _engine.localRotation = Quaternion.Euler(0f, 0f, _headingDegrees);
            _carriage.localPosition = Vector3.zero;
            _carriage.localRotation = _engine.localRotation;
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
            _carriage.localRotation = Quaternion.Euler(0f, 0f,
                HeadingDegrees(path.TangentDistanceFraction(fraction)));
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
            CreatePart("Boiler", _engine, CylinderMesh(),
                new Vector3(0.08f, 0f, 0.065f), new Vector3(0.20f, 0.14f, 0.20f),
                Quaternion.Euler(0f, 0f, 90f), CreamMaterial()); // cylinder length onto +x
            CreatePart("Cab", _engine, CubeMesh(),
                new Vector3(-0.12f, 0f, 0.055f), new Vector3(0.18f, 0.26f, 0.22f),
                Quaternion.identity, CreamMaterial());
            CreatePart("CabRoof", _engine, CubeMesh(),
                new Vector3(-0.12f, 0f, -0.08f), new Vector3(0.22f, 0.30f, 0.05f),
                Quaternion.identity, NavyMaterial());
            CreatePart("Funnel", _engine, CylinderMesh(),
                new Vector3(0.15f, 0f, -0.085f), new Vector3(0.09f, 0.05f, 0.09f),
                Quaternion.Euler(90f, 0f, 0f), NavyMaterial()); // cylinder axis off the board

            _carriage = new GameObject("Carriage").transform;
            _carriage.SetParent(transform, false);
            CreatePart("Chassis", _carriage, CubeMesh(),
                new Vector3(0f, 0f, 0.205f), new Vector3(0.36f, 0.30f, 0.06f),
                Quaternion.identity, NavyMaterial());
            CreatePart("Body", _carriage, CubeMesh(),
                new Vector3(0f, 0f, 0.085f), new Vector3(0.34f, 0.28f, 0.18f),
                Quaternion.identity, CreamMaterial());

            // The passenger: a chibi head sunk a third into the open body so it reads as
            // seated IN the carriage (target-02's cats), ears as 45-degree diamonds. Tinted
            // per cat in SyncSlot over a white basis, BoardSurface's property-block way.
            var cat = new GameObject("Cat").transform;
            cat.SetParent(_carriage, false);
            _catRenderers = new[]
            {
                CreatePart("Head", cat, SphereMesh(),
                    new Vector3(0f, 0f, -0.075f), new Vector3(0.26f, 0.26f, 0.26f),
                    Quaternion.identity, CatBasisMaterial()),
                CreatePart("EarLeft", cat, CubeMesh(),
                    new Vector3(0f, 0.075f, -0.185f), new Vector3(0.07f, 0.07f, 0.07f),
                    Quaternion.Euler(45f, 0f, 0f), CatBasisMaterial()),
                CreatePart("EarRight", cat, CubeMesh(),
                    new Vector3(0f, -0.075f, -0.185f), new Vector3(0.07f, 0.07f, 0.07f),
                    Quaternion.Euler(45f, 0f, 0f), CatBasisMaterial()),
            };
        }

        // BoardSurface.CreatePart's shape: builtin mesh, no collider, project material only.
        private static MeshRenderer CreatePart(string name, Transform parent, Mesh mesh,
            Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            return renderer;
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
