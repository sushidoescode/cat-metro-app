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
        // The 2026-08-31 curated references make two scale requirements explicit: the head is
        // roughly 70-85% of its carriage width, and most of it sits above the carriage wall.
        // At the production L001 phone projection, the old 0.19 head rendered at only 3.2% of
        // frame width after the frontal-camera change; 0.31 renders just over 5% with the
        // explicit 5-6% delivery target. The carriage grows with it below, keeping the head at
        // ~82% of the open box rather than turning it into a ball balanced on a tiny wagon.
        //
        // Height remains constrained by the switch-disc slab. The bigger head grows mostly
        // downward into a lower wall: its crown moves by less than a hundredth of a board unit.
        // Ears keep their tips below the pinned switch midpoint and gain readability through
        // lateral spread. All values are WORLD sizes passed through ScaleForWorldSize; builtin
        // Sphere.fbx is ~3.33 units across and a raw localScale would repeat the r3 regression.
        public const float HeadDiameter = 0.31f;
        private const float HeadCenterZ = 0.010f;
        private const float EarThickness = 0.050f; // along travel; ears are flat wedges
        private const float EarSize = 0.140f;      // the 45-degree diamond's box size
        private const float EarLateral = 0.080f;
        private const float EarCenterZ = -0.080f;
        private const float EyeSize = 0.079f;
        private static readonly Vector3 EyeOffset = new Vector3(0.0862f, 0.0602f, -0.0673f);
        private static readonly Vector3 MuzzleOffset = new Vector3(0.1253f, 0f, -0.0370f);
        private static readonly Vector3 MuzzleSize = new Vector3(0.082f, 0.111f, 0.071f);

        // ── The destination pin ─────────────────────────────────────────────────────────
        // target-01's single most important readability device: above every riding cat floats
        // a small white card carrying that cat's destination symbol. It is the reason you can
        // tell at a glance where a passenger is going; without it the cat's LINE is legible
        // (it is tinted) but its DESTINATION is not, and a colour-blind player has nothing at
        // all. The shape comes from CatLine.ShapeOf and nowhere else.
        //
        // SIZING, in the projection the camera actually performs. The diorama camera is
        // orthographic and identity-rotated, so px-per-board-unit is (screenHeight / 2) /
        // orthographicSize. A level big enough to need size ~11 renders ~93 px per board unit,
        // the fixed conservative sizing yardstick used below. It is not claimed as the current
        // corpus minimum; BoardLookTests measures the delivery target from a rendered artifact.
        //
        //     cat head    0.31  x 93 = 28.8 px
        //     pin card    0.24  x 93 = 22.3 px   (77% of the enlarged head; card height is
        //                                         constrained by rail/switch clearance)
        //     symbol      0.17  x 93 = 15.8 px
        //     white margin      each side 3.3 px  (the card still reads as a card behind it)
        //     star point  (0.5-0.45) x 0.17 x 93 = 4.3 px
        //
        // The star's point length is the binding constraint on the whole design: it is the
        // finest detail any of the five symbols carries, and under ~4 px it stops reading as a
        // star at all. That is what fixes the symbol at 0.17 rather than something daintier,
        // and the card at 0.24 rather than something daintier.
        public const float PinCardSize = 0.24f;
        public const float PinSymbolSize = 0.17f;
        private const float PinCardDepth = 0.02f;
        private const float PinSymbolDepth = 0.02f;
        // The symbol rides proud of the card toward the camera, its back face buried 0.002
        // INSIDE the card. Interpenetration, deliberately: coplanar faces z-fight, a gap
        // floats. It reads as an embossed badge from the only angle anyone sees it from.
        private const float PinSymbolLocalZ = -0.018f;

        // Screen-space board-units from the cat's head CENTRE up to the pin's centre. 0.30
        // leaves 0.30 - 0.12 - 0.155 = 0.025 of clear air between the larger head and card
        // (2.3 px even at the 93 px/unit yardstick) without spending more switch clearance.
        private const float PinScreenRise = 0.30f;

        // Carriage-local board z for the pin's centre, i.e. board z -0.155. This is the ONLY
        // number the switch discs constrain, and it is centred in the gap they leave. A card
        // this size, held square to the camera above the tilted board, spans 0.2353 of
        // board z all by itself, against a window running from the rail crowns (+0.035) up to
        // the lowest switch furniture (the onboarding teach ring's underside, -0.31). Centring
        // in that window leaves 0.0373 of clearance under the switch furniture and 0.0723 over
        // the rails. See TrainConsistTests.Pin_ClearsTheSwitchDiscSlab_AndTheRailCrowns, which
        // measures the shipped mesh's real vertices rather than trusting this comment.
        private const float PinBoardZ = 0.045f;

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

        /// <summary>The fixed board-local yaw every seated cat holds (-90 degrees frontally).</summary>
        public static float CatBoardYaw => CameraFacingYawDegrees(BoardSceneLook.BoardTilt);

        /// <summary>
        /// The board-local rotation that turns a flat card's authored front face (local -z,
        /// the DestinationShapeMesh convention) square-on to the camera, with the card's own
        /// +y running straight up the screen.
        /// </summary>
        /// <remarks>
        /// The same mechanism as CameraFacingYawDegrees, carried from a yaw to the full
        /// rotation a flat card needs — derived from the diorama tilt, never hardcoded, so it
        /// tracks any re-authoring of BoardSceneLook.BoardTilt. A yaw alone is enough for the
        /// CAT, whose features are domes on a sphere: it only has to bring them round to the
        /// camera's side. It is NOT enough for a card. Board-plane -z sits 38 degrees off the
        /// view axis, so a pin left lying in the board plane would render at cos 38 = 79% of
        /// its height and its circle would read as an ellipse. Undoing the whole tilt is the
        /// answer because the camera is identity-rotated: tilt * inverse(tilt) is identity, so
        /// the card ends up axis-aligned with the camera itself.
        /// </remarks>
        public static Quaternion CameraFacingRotation(Quaternion boardTilt) =>
            Quaternion.Inverse(boardTilt);

        /// <summary>
        /// The board-local offset that lands a feature exactly <paramref name="screenRise"/>
        /// ABOVE the origin ON SCREEN — no horizontal drift at all — while spending exactly
        /// <paramref name="boardZ"/> of table height getting there.
        /// </summary>
        /// <remarks>
        /// Why this solves instead of simply lifting. The switch discs own the airspace
        /// directly over a riding cat's head (board z -0.48 to -0.32, and the onboarding teach
        /// ring hangs lower still at -0.31), which is the same ceiling that made "just make the
        /// ears taller" the wrong fix. So the pin does not climb in board z; it climbs the
        /// SCREEN, by travelling in the board PLANE, which costs no table height whatsoever.
        ///
        /// The rows of the tilt are the board-local gradients of screen x and screen y, so
        /// "directly above the cat, by this much" is a 2x2 solve for the in-plane part, with
        /// the z spend's own screen drift subtracted out on the right-hand side. The
        /// determinant is the tilt's zz term (about 0.788 at the authored tilt) and only
        /// collapses if the board were ever tilted edge-on to the camera, at which point
        /// nothing on it renders anyway.
        /// </remarks>
        public static Vector3 ScreenUpOffset(Quaternion boardTilt, float screenRise, float boardZ)
        {
            Quaternion inverse = Quaternion.Inverse(boardTilt);
            Vector3 gradientX = inverse * Vector3.right; // board-local gradient of screen x
            Vector3 gradientY = inverse * Vector3.up;    // board-local gradient of screen y
            float wantX = -gradientX.z * boardZ;               // cancel the z spend's drift
            float wantY = screenRise - gradientY.z * boardZ;   // plane carries the rest of the rise
            float det = gradientX.x * gradientY.y - gradientX.y * gradientY.x;
            return new Vector3(
                (wantX * gradientY.y - gradientX.y * wantY) / det,
                (gradientX.x * wantY - wantX * gradientY.x) / det,
                boardZ);
        }

        /// <summary>The pin's fixed board-local rotation — square to the camera at any heading.</summary>
        public static Quaternion PinBoardRotation =>
            CameraFacingRotation(BoardSceneLook.BoardTilt);

        /// <summary>
        /// The pin's fixed board-local offset from the CARRIAGE origin: up from the carriage to
        /// the head centre, then out through the board plane until the pin sits PinScreenRise
        /// directly above that head on screen.
        /// </summary>
        public static Vector3 PinBoardOffset =>
            new Vector3(0f, 0f, HeadCenterZ)
            + ScreenUpOffset(BoardSceneLook.BoardTilt, PinScreenRise, PinBoardZ - HeadCenterZ);

        private static Material _navyMaterial;
        private static Material _creamMaterial;
        private static Material _catBasisMaterial;
        private static Material _pinCardMaterial;
        private static Mesh _cubeMesh;
        private static Mesh _sphereMesh;
        private static Mesh _cylinderMesh;

        private Transform _engine;
        private Transform _carriage;
        private Transform _cat;
        private Transform _pin;
        private MeshFilter _pinSymbolFilter;
        private MeshRenderer _pinSymbol;
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
        private byte _appliedColorCode;
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
        // occupant's edge history, tint or pin shape.
        //
        // This takes the Domain colour CODE rather than a resolved Color, and that is the
        // whole reason the pin can exist. A Color cannot be turned back into a line without
        // inventing a second colour->shape table, which is exactly the duplication CatLine was
        // written to delete. With the code in hand, both channels come off the one vocabulary:
        // CatLine.ColorOf paints the cat, CatLine.ShapeOf cuts the pin, and they cannot drift
        // apart. It also fixes a quiet bug in passing — BoardView.ColorForCode has no wild
        // case, so a wild passenger used to ride out MAGENTA; CatLine.ColorOf gives it the
        // catnip violet the manifest pinned.
        public void SyncSlot(short trainId, byte colorCode)
        {
            if (trainId != _seenTrainId)
            {
                _seenTrainId = trainId;
                _currentEdge = -1;
                _previousEdge = -1;
                _headingDegrees = 0f;
            }
            if (!_catColorApplied || colorCode != _appliedColorCode)
            {
                _appliedColorCode = colorCode;
                _catColorApplied = true;
                Color catColor = CatLine.ColorOf(colorCode);
                var properties = new MaterialPropertyBlock();
                properties.SetColor("_BaseColor", catColor);
                properties.SetColor("_Color", catColor);
                for (int i = 0; i < _catRenderers.Length; i++)
                    _catRenderers[i].SetPropertyBlock(properties);
                ApplyPinShape(CatLine.ShapeOf(CatLine.NameOfCode(colorCode)), properties);
            }
        }

        // The pin's symbol, in the shape the shared vocabulary gives this cat's line and the
        // same tint the cat itself wears.
        //
        // STAR IS THE INTERESTING CASE. A riding cat can be wild (CatColor.Wild = 5), and wild's
        // badge is a star — but DestinationShapeMesh.ForShape(Star) THROWS on purpose, because
        // its extruder fans from vertex 0 and that is only a valid triangulation for a convex
        // outline. Nothing here weakens that guard, and no station plate changes: the pin simply
        // takes its star from CatPinMeshBuilder, whose fan runs from the polygon's CENTRE and is
        // therefore valid for any star-shaped outline, concave ones included. Every other shape
        // still comes from the shared realiser, unchanged, so there remains exactly one place a
        // line becomes a shape and one place that shape becomes board geometry.
        //
        // PlateRotation and PlateScale's job is done here by the shape's own mesh bounds through
        // ScaleForWorldSize, which reaches the same answer for the builtin cylinder (bounds 2
        // units on its axis, so depth halves) without a second constant that could rot.
        private void ApplyPinShape(DestinationShape shape, MaterialPropertyBlock tint)
        {
            Mesh mesh = shape == DestinationShape.Star
                ? CatPinMeshBuilder.StarBadge()
                : DestinationShapeMesh.ForShape(shape);
            _pinSymbolFilter.sharedMesh = mesh;
            _pinSymbol.transform.localRotation = DestinationShapeMesh.PlateRotation(shape);
            _pinSymbol.transform.localScale = ScaleForWorldSize(mesh, SymbolWorldSize(shape));
            _pinSymbol.SetPropertyBlock(tint);
        }

        // The symbol's world size, in the axis order its own mesh uses: the circle is the
        // builtin cylinder standing on its Y axis until PlateRotation lays it face-on, so for
        // that one shape the DEPTH is the y entry.
        private static Vector3 SymbolWorldSize(DestinationShape shape) =>
            shape == DestinationShape.Circle
                ? new Vector3(PinSymbolSize, PinSymbolDepth, PinSymbolSize)
                : new Vector3(PinSymbolSize, PinSymbolSize, PinSymbolDepth);

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
            // The pin gets the same treatment one dimension up. Undoing the carriage's turn
            // leaves it at a FIXED board-local pose — rotation and offset both — so the card
            // holds still, square to the camera and directly above its cat, on a straight,
            // through a curve and parked at a node. Counter-rotating the OFFSET as well as the
            // rotation is what keeps the pin from swinging around its cat like a bucket on a
            // rope as the train turns.
            Quaternion unturn = Quaternion.Inverse(Quaternion.Euler(0f, 0f, degrees));
            _pin.localRotation = unturn * PinBoardRotation;
            _pin.localPosition = unturn * PinBoardOffset;
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
                new Vector3(0f, 0f, 0.205f), new Vector3(0.36f, 0.40f, 0.06f),
                Quaternion.identity, NavyMaterial());
            CreatePart("Body", _carriage, CubeMesh(),
                new Vector3(0f, 0f, 0.145f), new Vector3(0.34f, 0.38f, 0.10f),
                Quaternion.identity, CreamMaterial());

            // The passenger: a chibi head at 82% of the body's width. Its lower fifth intersects
            // the low wall in board-local geometry, while the frontal artifact keeps nearly all
            // of its face visible, so it remains seated IN the open box and reads clearly.
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

            // The destination pin: a white card floating above the passenger with that cat's
            // destination symbol on it. A sibling of the Cat rather than a child of it, because
            // the cat holds a camera-facing YAW while the pin holds a camera-facing ROTATION —
            // hanging one off the other would mean undoing the yaw before applying the rotation,
            // for no gain. Both are counter-rotated out of the carriage's heading in one place,
            // SetCarriageHeading, which is the mechanism this branch already established.
            //
            // No mesh is created per consist: the card and star prototypes are shared statics
            // and the other four symbols are the board's own plate meshes, so a consist still
            // owns no generated assets and teardown is still just the GameObject's destruction.
            _pin = new GameObject("Pin").transform;
            _pin.SetParent(_carriage, false);
            CreatePart("Card", _pin, CatPinMeshBuilder.Card(),
                Vector3.zero, new Vector3(PinCardSize, PinCardSize, PinCardDepth),
                Quaternion.identity, PinCardMaterial());
            // Mesh, scale and rotation are all set per-line by ApplyPinShape; this only has to
            // exist with a renderer on it before the first SyncSlot lands.
            _pinSymbol = CreatePart("Symbol", _pin, CatPinMeshBuilder.StarBadge(),
                new Vector3(0f, 0f, PinSymbolLocalZ),
                new Vector3(PinSymbolSize, PinSymbolSize, PinSymbolDepth),
                Quaternion.identity, CatBasisMaterial());
            _pinSymbolFilter = _pinSymbol.GetComponent<MeshFilter>();

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

        // WarmPaper, not the carriage's CreamCard: the card has to read as a separate object
        // floating in front of the diorama, and at 22 px the only thing separating it from the
        // cream body below is that it is the brightest thing on the board.
        private static Material PinCardMaterial()
        {
            if (_pinCardMaterial == null)
                _pinCardMaterial = GreyboxMaterial.CreateTinted(
                    "Toy Train — Pin Card", Palette.WarmPaper);
            return _pinCardMaterial;
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
