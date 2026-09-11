using CatMetro.Presentation.Cats;
using CatMetro.Presentation.Props;
using CatMetro.Presentation.Theme;
using CatMetro.Presentation.Fx;
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
        // (0.23) + a toy-tight coupling gap (0.04) + half a carriage chassis (0.21), sized against
        // the 0.5 track gauge (ToyTrackMeshBuilder.RailOffset * 2).
        public const float CarriageOffset = 0.48f;

        // The pinned head anchor lift off the board plane (the old capsule's -0.2). Part
        // z-offsets below are anchor-local: +z points down into the table, and the rail
        // crowns (board z +0.035) sit at +0.235, which is where the chassis parts bottom out.
        public const float HeadAnchorZ = -0.2f;

        // ── Cat geometry ────────────────────────────────────────────────────────────────
        // Rank 15 on the portrait grid: a 0.36-unit head in a 0.44-unit carriage keeps the
        // reference's ~82% proportion. BoardLookTests measures the rendered head and ears at
        // >=5% of L001's frame width and >=4% at L002/L009; the licensed rig has its own scale.
        // +z points into the board. Lower the enlarged head's centre by 0.03 so its crown
        // stays below switch furniture, and lower the carriage wall to leave the face exposed.
        // Scale facial features about the head centre so they remain on the sphere's surface.
        public const float HeadDiameter = 0.36f;
        private const float HeadCenterZ = 0.040f;
        private const float CatFeatureScale = HeadDiameter / 0.31f;
        private const float EarThickness = 0.050f * CatFeatureScale;
        private const float EarSize = 0.140f * CatFeatureScale;
        private const float EarLateral = 0.080f * CatFeatureScale;
        private const float EarCenterZ = HeadCenterZ - 0.090f * CatFeatureScale;
        private const float EyeSize = 0.079f * CatFeatureScale;
        // A platform-centre offset measured in board units, not authored mesh space. At the
        // admitted rig's current presentation scale, the baked artifact test crosses the
        // released-lane envelope representable by the authored TrainsMax values; the current
        // straight waiting cases at lanes 0..3; both current diagonal normals at lanes 0..1;
        // departure Walk plus Celebrate at every current station-arrival heading and a sampled
        // five-degree retained-heading envelope. Each case crosses the complete active clip at
        // half-frame spacing, a 17-angle applied-ear corpus and independently measured maximum
        // carriage-ward bob. The wider original carriage left .020663 clearance at offset
        // .704 in the worst diagonal waiting lane. Native trials at .734 leave .051693;
        // the shared platform anchor also reserves the new position in camera framing.
        public const float PlatformSideOffset = 0.734f;
        // Board-X clearance that keeps an ARRIVING cat out from behind the station badge.
        // station:keyline-generated is KeylineSize (1.5525) x the 0.6 anchor scale = 0.9315
        // board units across, standing vertically at the node and .67 units nearer the camera
        // than the platform anchor, so it covers that anchor outright: a measured probe of the
        // delivered passenger found only 3.0% of its own pixels visible, rising to 93.1% with
        // the badge suppressed, while suppressing the roof changed nothing. Half the keyline
        // (.46575) plus half the admitted rig's measured head-and-ears width (.2696) plus a
        // .044 margin = .78. Departures only; sources carry no badge.
        public const float PlatformBadgeClearance = 0.78f;
        public const float PlatformEndpointClearance = 0.045f;
        // Horizontal half-extent reserved by the camera around a platform cat's root. The
        // fallback head, ears and 0.28 card all fit inside one HeadDiameter; the admitted-rig
        // artifact sweep remains the authority if that model's animated silhouette grows.
        public const float PlatformFramingHalfWidth = HeadDiameter;
        // At 93 px/unit, the foreshortened queue pitch is 0.48*cos(38)*93 = 35.2 px,
        // leaving a visible gap around each enlarged 26 px card and its waiting cat.
        public const float PlatformQueueSpacing = 0.48f;
        // The delivered queue needs more pitch than the source queue. 0.48 was sized against
        // the 0.28 pin card; a rendered probe measured the admitted rig's settled silhouette at
        // 0.6383 board units wide and its Alight pose at 0.8044, so two 0.48 lanes overlap by
        // about a quarter of a cat. .5392 is the measured head-and-ears width (2 x the .2696
        // half at :70); .044 is the same margin the badge clearance uses.
        public const float PlatformDeliveredQueueSpacing = 0.5832f;

        internal static float PlatformLaneOffset(int lane)
        {
            if (lane <= 0) return 0f;
            int step = (lane + 1) / 2;
            return (lane & 1) == 1
                ? step * PlatformQueueSpacing
                : -step * PlatformQueueSpacing;
        }

        internal static Vector3 SourcePlatformAnchorBoard(Vector3 boardLocalNode,
            Vector3 boardLocalSide, float boardLocalZ, int queuePosition)
        {
            Vector3 side = new Vector3(boardLocalSide.x, boardLocalSide.y, 0f);
            if (side.sqrMagnitude <= 1e-8f) side = Vector3.down;
            else side.Normalize();
            Vector3 behind = new Vector3(side.y, -side.x, 0f);
            return new Vector3(boardLocalNode.x, boardLocalNode.y, boardLocalZ)
                + side * PlatformSideOffset
                + behind * PlatformLaneOffset(queuePosition);
        }
        private const float WalkLegSwingDegrees = 22f;
        private const float TransitionLegSwingDegrees = 12f;
        // The admitted skin's partial ear weights turn the doubled >=12-degree Tier-1 probe
        // into 0.020037 board units of localized 3D deformation. At the conservative 93 px/unit
        // yardstick that is a 1.863 px upper bound against the enlarged 28.8 px head. Only a
        // render can establish how much of that deformation is visible.
        public const float RigEarTwitchGain = 2f;
        public static readonly Vector3 PlaceholderBodyWorldSize =
            new Vector3(0.200f, 0.175f, 0.165f) * CatFeatureScale;
        private static readonly Vector3 PlaceholderLegWorldSize =
            new Vector3(0.060f, 0.058f, 0.130f) * CatFeatureScale;
        private static readonly Vector3 EyeOffset =
            new Vector3(0.0862f, 0.0602f, -0.0773f) * CatFeatureScale
            + Vector3.forward * HeadCenterZ;
        private static readonly Vector3 MuzzleOffset =
            new Vector3(0.1253f, 0f, -0.0470f) * CatFeatureScale
            + Vector3.forward * HeadCenterZ;
        private static readonly Vector3 MuzzleSize =
            new Vector3(0.082f, 0.111f, 0.071f) * CatFeatureScale;

        // ── The destination pin ─────────────────────────────────────────────────────────
        // target-01's single most important readability device: above every riding cat floats
        // a small white card carrying that cat's destination symbol. It is the reason you can
        // tell at a glance where a passenger is going; without it the cat's LINE is legible
        // (it is tinted) but its DESTINATION is not, and a colour-blind player has nothing at
        // all. Authored token shapes take priority; legacy lines retain CatLine.ShapeOf.
        //
        // SIZING, in the projection the camera actually performs. The diorama camera is
        // orthographic and identity-rotated, so px-per-board-unit is (screenHeight / 2) /
        // orthographicSize. A level big enough to need size ~11 renders ~93 px per board unit,
        // the fixed conservative sizing yardstick used below. It is not claimed as the current
        // corpus minimum; BoardLookTests measures the delivery target from a rendered artifact.
        //
        //     cat head    0.36  x 93 = 33.5 px
        //     pin card    0.28  x 93 = 26.0 px   (78% of the enlarged head; card height is
        //                                         constrained by rail/switch clearance)
        //     symbol      0.20  x 93 = 18.6 px
        //     white margin      each side 3.7 px  (the card still reads as a card behind it)
        //     star point  0.5 * (1-0.45) * 0.20 * 93 = 5.1 px
        //
        // The star's point length is the binding constraint on the whole design: it is the
        // finest detail any of the five symbols carries, and under ~4 px it stops reading as a
        // star at all. Keep that detail and the cream keyline substantial beside the head.
        public const float PinCardSize = 0.28f;
        public const float PinSymbolSize = 0.20f;
        private const float PinCardDepth = 0.02f;
        private const float PinSymbolDepth = 0.02f;
        // The symbol rides proud of the card toward the camera, its back face buried 0.002
        // INSIDE the card. Interpenetration, deliberately: coplanar faces z-fight, a gap
        // floats. It reads as an embossed badge from the only angle anyone sees it from.
        private const float PinSymbolLocalZ = -0.018f;

        // Preserve a 0.03-unit screen gap above the enlarged head: 0.35 - 0.14 - 0.18.
        // PinBoardZ separately keeps the card under the switch furniture.
        private const float PinScreenRise = 0.35f;

        // Carriage-local board z for the pin's centre, i.e. board z -0.155. This is the ONLY
        // number the switch discs constrain. The enlarged card stays inside the window from
        // the rail crowns (+0.035) to the lowest switch furniture (-0.31); TrainConsistTests
        // checks its actual rendered corners at every heading, with at least 0.01 clearance over
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
        private BoardFx _fx;
        private Transform _rejected;
        private Vector3 _carriagePosition, _recoil;
        private float _rejectedUntil;
        private ParticleSystem _steam;
        private bool _moving;
        private float _engineBob;
        private Transform _cat;
        private Transform _pin;
        private Transform _head;
        private Transform _earLeft;
        private Transform _earRight;
        private Transform _eyeLeft;
        private Transform _eyeRight;
        private Transform _muzzle;
        private Transform _legLeft;
        private Transform _legRight;
        private Transform[] _bodyLegs;
        private MeshFilter _pinSymbolFilter;
        private MeshRenderer _pinSymbol;
        private MeshRenderer[] _catRenderers; // placeholder renderers — tinted per cat via property block

        private Vector3 _catBaseLocalPosition;
        private Vector3 _catPathLocalPosition, _pinPathLocalPosition, _riderBobLocalOffset;
        private bool _hasRiderBobPose;
        private Quaternion _catBaseLocalRotation;
        private Quaternion _headBaseLocalRotation;
        private Quaternion _earLeftBaseLocalRotation;
        private Quaternion _earRightBaseLocalRotation;
        private Quaternion _eyeLeftBaseLocalRotation;
        private Quaternion _eyeRightBaseLocalRotation;
        private Quaternion _muzzleBaseLocalRotation;
        private Quaternion _legLeftBaseLocalRotation;
        private Quaternion _legRightBaseLocalRotation;
        private Vector3 _earLeftBaseLocalPosition;
        private Vector3 _earRightBaseLocalPosition;
        private Vector3 _eyeLeftBaseLocalPosition;
        private Vector3 _eyeRightBaseLocalPosition;
        private Vector3 _muzzleBaseLocalPosition;
        private Vector3 _eyeLeftBaseLocalScale;
        private Vector3 _eyeRightBaseLocalScale;
        private Vector3 _pinBaseLocalPosition;
        private Vector3 _platformAnchorWorldPosition;
        private CatMicroMotion _microMotion = new CatMicroMotion(0u);
        private CatPresentationState _presentationState = CatPresentationState.Hidden;
        private CatPresentationState _lastRigState = CatPresentationState.Hidden;
        private CatModelCatalog _catCatalog;
        private CarriageModelCatalog _carriageCatalog;
        private EngineModelCatalog _engineCatalog;
        public bool OriginalEngineAdmitted { get; private set; }
        public string EngineFallbackReason { get; private set; }
        private GameObject _rigInstance;
        private const float OpenCarriageSeatDepth = .0983f;
        private Animator _rigAnimator;
        private CatRigPresentation _rigPresentation;
        private BoardFurTint _rigFurTint;
        private Transform _rigEarDeformerA;
        private Transform _rigEarDeformerB;
        private Quaternion _rigEarAPreviousOffset = Quaternion.identity;
        private Quaternion _rigEarBPreviousOffset = Quaternion.identity;
        private Quaternion _rigEarALastApplied;
        private Quaternion _rigEarBLastApplied;
        private bool _rigAdmitted;
        private bool _rigMotionSuppressed;
        private bool _rigStaticSeated;
        private bool _rigEarTwitchSupported;
        private bool _rigEarPoseApplied;
        private bool _rigEarTwitchActive;
        private float _rigEarTwitchDegrees;
        private bool _hasPlatformAnchor;
        private bool _platformAnchorMovesToPlatform;
        private int _rigNeutralSampleCount;
        private string _rigFallbackReason = "Rig has not been evaluated.";

        // The authored graph's edge endpoints (BoardView's own arrays) — the authority that
        // decides whether remembered history is a path the train could actually have rolled.
        private int[] _edgeFrom;
        private int[] _edgeTo;

        // Presentation-side memory the sim doesn't carry: the edge the head is (or was last)
        // on, one edge of history behind it, and the last applied heading for parked frames.
        private long _seenOccupantKey;
        private bool _hasSeenOccupant;
        private int _currentEdge = -1;
        private int _previousEdge = -1;
        private float _headingDegrees;
        private byte _appliedColorCode;
        private bool _catColorApplied;
        public Color CatTint => CatLine.ColorOf(_appliedColorCode);
        public DestinationShape PinShape { get; private set; }
        private PassengerStatusMarks _statusMarks;
        public void SetTokenFlags(bool stray, bool express) => _statusMarks.Bind(stray, express);

        public bool RigAdmitted => _rigAdmitted;
        public bool OriginalCarriageAdmitted { get; private set; }
        public string CarriageFallbackReason { get; private set; } = "Carriage has not been evaluated.";
        private Vector3 _deliveredBaseScale;
        private int _deliveredIdleState, _deliveredCelebrateState;
        // The board-X the arriving cat steps away from, so it clears the station badge. Derived
        // from the SEAT at anchor time, never from the train's current node: the anchor resolves
        // on the frame movingToPlatform flips, and on a reused slot the train can still be at its
        // approach node then, which would pick the opposite side and teleport the hand-off.
        // BoardView supplies the centre because only it knows the board's extent.
        private float _boardCentreX;
        private bool _hasBoardCentre;
        public void SetBoardCentreX(float centreX)
        {
            _boardCentreX = centreX;
            _hasBoardCentre = true;
        }

        public static float BadgeStepX(float boardCentreX, float seatBoardX) =>
            (boardCentreX - seatBoardX < 0f ? -1f : 1f) * PlatformBadgeClearance;

        private Vector3 BadgeStep(float seatBoardX) => _hasBoardCentre
            ? Vector3.right * BadgeStepX(_boardCentreX, seatBoardX) : Vector3.zero;

        public Vector3 PlatformEndpointWorld
        {
            get
            {
                if (_hasPlatformAnchor) return _platformAnchorWorldPosition;
                Vector3 seat = transform.parent.InverseTransformPoint(
                    _carriage.TransformPoint(_catBaseLocalPosition));
                return transform.parent.TransformPoint(
                    seat + Vector3.down * PlatformSideOffset + BadgeStep(seat.x));
            }
        }

        // The existing rig/placeholder renderer also paints passengers after delivery.
        // Vehicle geometry is hidden; no second model implementation or licensed asset.
        public void PrepareDeliveredPassenger(Vector3 boardAnchor)
        {
            SetDeliveredAnchor(boardAnchor);
            for (int i = 0; i < transform.childCount; i++)
                if (transform.GetChild(i) != _carriage)
                    transform.GetChild(i).gameObject.SetActive(false);
            for (int i = 0; i < _carriage.childCount; i++)
            {
                var child = _carriage.GetChild(i);
                if (child != _cat && child != _pin) child.gameObject.SetActive(false);
            }
            _carriage.localPosition = Vector3.zero;
            _carriage.localRotation = Quaternion.identity;
            _hasPlatformAnchor = true;
            _platformAnchorMovesToPlatform = true;
            _platformAnchorWorldPosition = transform.position;
            _deliveredBaseScale = _cat.localScale;
            if (_rigAnimator != null)
            {
                string layer = _rigAnimator.GetLayerName(0) + ".";
                _deliveredIdleState = Animator.StringToHash(layer + CatModelCatalog.IdleSitClip);
                _deliveredCelebrateState = Animator.StringToHash(layer + CatModelCatalog.CelebrateClip);
            }
        }

        public void SetDeliveredAnchor(Vector3 boardAnchor)
        {
            transform.localPosition = boardAnchor;
            _platformAnchorWorldPosition = transform.position;
        }

        public void ApplyDeliveredPose(CatPresentationTrack track, float visualTime, bool motionOff)
        {
            ApplyPresentation(track.State, 1f, true, visualTime, motionOff, 0f);
            // Retained passengers add a presentation hop while the named celebration plays.
            // This scale accent stays outside the authored bone curves and also serves placeholders.
            float hop = !motionOff && track.State == CatPresentationState.Celebrate
                ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(track.StateElapsed
                    / CatPresentationTrack.CelebrateDuration)) : 0f;
            _cat.localScale = _deliveredBaseScale * (1f + .13f * hop);
            if (!motionOff && _rigAnimator != null)
            {
                bool celebrate = track.State == CatPresentationState.Celebrate;
                float duration = Mathf.Max(.01f, _rigAnimator.GetCurrentAnimatorStateInfo(0).length);
                float phase = celebrate ? track.StateElapsed / CatPresentationTrack.CelebrateDuration
                    : Mathf.Repeat(visualTime / duration, 1f);
                _rigAnimator.Play(celebrate ? _deliveredCelebrateState : _deliveredIdleState, 0, phase);
                _rigAnimator.Update(0f);
                _rigPresentation?.ApplyHeadShape();
                _rigAnimator.speed = 0f; // the board's unscaled presentation clock owns this pose
            }
        }
        public string RigFallbackReason => _rigFallbackReason;
        public CatPresentationState PresentationState => _presentationState;
        public long PresentationOccupantKey => _seenOccupantKey;
        public int RigNeutralSampleCount => _rigNeutralSampleCount;
        public bool RigEarTwitchSupported => _rigEarTwitchSupported;
        // TASK 17's admitted mesh has neither blendshapes nor a localized eye/lid bone. A
        // broad head squash would be worse than no blink, so rig blink is deliberately deferred;
        // Tier 1 still blinks the HUD faces and the discrete-part placeholder cats.
        public bool RigBlinkSupported => false;

        public static ToyTrainView Create(Transform parent, string name,
            int[] edgeFrom, int[] edgeTo, CatModelCatalog catCatalog = null,
            CarriageModelCatalog carriageCatalog = null, EngineModelCatalog engineCatalog = null)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<ToyTrainView>();
            view._fx = BoardFx.GetOrCreate(parent != null ? parent : root.transform);
            view._edgeFrom = edgeFrom;
            view._edgeTo = edgeTo;
            view._catCatalog = catCatalog;
            view._carriageCatalog = carriageCatalog;
            view._engineCatalog = engineCatalog;
            view.BuildConsist();
            return view;
        }

        // The key is presentation-owned because Domain TrainSlot.Id identifies the fixed slot,
        // not successive occupants of it. A new key must not inherit the previous occupant's
        // edge history, micro-motion phase, pose, tint or pin shape.
        //
        // This takes the Domain colour CODE rather than a resolved Color, and that is the
        // whole reason the pin can exist. A Color cannot be turned back into a line without
        // inventing a second colour->shape table, which is exactly the duplication CatLine was
        // written to delete. With the code in hand, both channels come off the one vocabulary:
        // CatLine.ColorOf paints the cat, CatLine.ShapeOf cuts the pin, and they cannot drift
        // apart. It also fixes a quiet bug in passing — BoardView.ColorForCode has no wild
        // case, so a wild passenger used to ride out MAGENTA; CatLine.ColorOf gives it the
        // catnip violet the manifest pinned.
        public void SyncSlot(long presentationOccupantKey, byte colorCode,
            DestinationShape? shape = null)
        {
            if (!_hasSeenOccupant || presentationOccupantKey != _seenOccupantKey)
            {
                ResetFeedback();
                _hasSeenOccupant = true;
                _seenOccupantKey = presentationOccupantKey;
                _currentEdge = -1;
                _previousEdge = -1;
                _headingDegrees = 0f;
                uint lowKey = (uint)presentationOccupantKey;
                uint highKey = (uint)(presentationOccupantKey >> 32);
                _microMotion = new CatMicroMotion(lowKey ^ highKey * 2654435761u);
                _presentationState = CatPresentationState.Hidden;
                _lastRigState = CatPresentationState.Hidden;
                _hasPlatformAnchor = false;
                ClearRigEarTwitch();
                ResetVisualPose();
            }
            DestinationShape resolved = shape ?? CatLine.ShapeOf(CatLine.NameOfCode(colorCode));
            if (!_catColorApplied || colorCode != _appliedColorCode || PinShape != resolved)
            {
                _appliedColorCode = colorCode;
                _catColorApplied = true;
                Color catColor = CatLine.ColorOf(colorCode);
                ApplyCatTint(catColor);
                var pinProperties = new MaterialPropertyBlock();
                pinProperties.SetColor("_BaseColor", catColor);
                pinProperties.SetColor("_Color", catColor);
                PinShape = resolved;
                ApplyPinShape(resolved, pinProperties);
            }
        }

        /// <summary>
        /// Pins the spawn endpoint to an exact board node rather than the carriage position of
        /// whichever frame first observes the occupant. Board-local side is derived from the
        /// emitted edge tangent by BoardView; no gameplay transform reads this value back.
        /// </summary>
        public void SetSourcePlatformAnchor(Vector3 boardLocalNode, Vector3 boardLocalSide,
            int queuePosition = 0)
        {
            Transform board = transform.parent;
            Vector3 seatBoard = board.InverseTransformPoint(
                _carriage.TransformPoint(_catBaseLocalPosition));
            // BoardView supplies right-normal(tangent). SourcePlatformAnchorBoard derives
            // -tangent from it and alternates stable lanes around the boarding point.
            Vector3 anchorBoard = SourcePlatformAnchorBoard(boardLocalNode, boardLocalSide,
                seatBoard.z, queuePosition);
            _platformAnchorWorldPosition = board.TransformPoint(anchorBoard);
            _platformAnchorMovesToPlatform = false;
            _hasPlatformAnchor = true;
        }

        /// <summary>
        /// Applies presentation-only decoration after the caller has placed the train root on
        /// its authoritative spline/node position. This method deliberately never changes the
        /// train root, engine, carriage, or destination-pin placement contracts.
        /// </summary>
        public void ApplyPresentation(CatPresentationState state, float visualTime, bool motionOff) =>
            ApplyPresentationInternal(state, 0f, false, visualTime, motionOff,
                false, 0f, false);

        /// <summary>
        /// Applies the cat's presentation-owned seat-to-platform path. Platform blend is copied
        /// from CatPresentationTrack and can move only the Cat child plus its destination-card
        /// sibling; it never moves the train root, carriage, spline anchor, or simulation data.
        /// </summary>
        public void ApplyPresentation(CatPresentationState state, float platformBlend,
            float visualTime, bool motionOff) =>
            ApplyPresentationInternal(state, platformBlend,
                state == CatPresentationState.Alight || state == CatPresentationState.Celebrate,
                visualTime, motionOff, true, 0f, false);

        public void ApplyPresentation(CatPresentationState state, float platformBlend,
            bool movingToPlatform, float visualTime, bool motionOff) =>
            ApplyPresentationInternal(state, platformBlend, movingToPlatform,
                visualTime, motionOff, true, 0f, false);

        /// <summary>
        /// Applies a presentation-owned path rate as well as its current blend. The rate remains
        /// geometry-free in CatPresentationTrack; this view measures the current seat-to-anchor
        /// distance in board units, including a source queue lane, before driving the in-place
        /// Walk clip. Existing callers without the rate retain nominal one-times playback.
        /// </summary>
        public void ApplyPresentation(CatPresentationState state, float platformBlend,
            bool movingToPlatform, float visualTime, bool motionOff,
            float platformBlendSpeed) =>
            ApplyPresentationInternal(state, platformBlend, movingToPlatform,
                visualTime, motionOff, true, platformBlendSpeed, true);

        private void ApplyPresentationInternal(CatPresentationState state, float platformBlend,
            bool movingToPlatform, float visualTime, bool motionOff, bool usePlatformPath,
            float platformBlendSpeed, bool scaleWalkPlayback)
        {
            _presentationState = state;
            _engineBob = _moving && !motionOff
                ? Mathf.Sin(visualTime * Mathf.PI * 2f * (8f / 1.2f)) * 0.004f : 0f;
            ApplyVehicleOffsets();
            if (_rejected != null && visualTime >= _rejectedUntil)
                _rejected.gameObject.SetActive(false);
            bool hidden = state == CatPresentationState.Hidden;
            if (hidden)
            {
                // Animator.Update cannot sample an inactive hierarchy. A first motion-off
                // transition therefore takes its one neutral sample before Cat is hidden.
                // A previously seated static pose also returns to neutral before hiding.
                // Repeated hidden frames do not reactivate or resample it.
                if (motionOff && (!_rigMotionSuppressed || _rigStaticSeated))
                    _cat.gameObject.SetActive(true);
                ClearRigEarTwitch();
                ResetVisualPose();
                _hasPlatformAnchor = false;
                SetBodyLegVisibility(false);
                if (motionOff) PlayRig(CatPresentationState.Hidden, true, 0f);
                _cat.gameObject.SetActive(false);
                _pin.gameObject.SetActive(false);
                return;
            }

            _cat.gameObject.SetActive(true);
            _pin.gameObject.SetActive(true);

            float safePlatformBlend = float.IsNaN(platformBlend) || float.IsInfinity(platformBlend)
                ? 0f : Mathf.Clamp01(platformBlend);
            bool platformWaiting = state == CatPresentationState.WaitingIdle
                && safePlatformBlend > 0f;
            SetBodyLegVisibility(state == CatPresentationState.Walk
                || state == CatPresentationState.Board
                || state == CatPresentationState.Alight
                || state == CatPresentationState.Celebrate
                || platformWaiting);
            bool followsPlatform = usePlatformPath
                && (state == CatPresentationState.Walk
                    || state == CatPresentationState.Board
                    || state == CatPresentationState.Alight
                    || state == CatPresentationState.Celebrate
                    || platformWaiting);
            // A queued wait is information, so reduced motion keeps its static platform
            // endpoint. Other live transitions cut to the authoritative carriage seat.
            bool resolvePlatformPath = followsPlatform && (!motionOff || platformWaiting);
            if (resolvePlatformPath && (!_hasPlatformAnchor
                || _platformAnchorMovesToPlatform != movingToPlatform))
            {
                if (movingToPlatform)
                {
                    // A catch-up frame can retain any previously rendered carriage heading at
                    // a delivered station. Keep departures on board-local down so that stale
                    // heading cannot turn the calibrated travel distance into a horizontal
                    // safe-frame escape. Source anchors remain explicit and tangent-relative.
                    Transform board = transform.parent;
                    Vector3 seatBoard = board.InverseTransformPoint(
                        _carriage.TransformPoint(_catBaseLocalPosition));
                    _platformAnchorWorldPosition = board.TransformPoint(
                        seatBoard + Vector3.down * PlatformSideOffset + BadgeStep(seatBoard.x));
                }
                else
                {
                    _platformAnchorWorldPosition = _carriage.TransformPoint(
                        _catBaseLocalPosition + Vector3.down * PlatformSideOffset);
                }
                _hasPlatformAnchor = true;
                _platformAnchorMovesToPlatform = movingToPlatform;
            }
            else if (!followsPlatform)
            {
                _hasPlatformAnchor = false;
            }

            // Relative seat/platform transition speed only: carriage advection is intentionally
            // excluded because it carries the cat rather than representing walking intent. The
            // actual current path length still includes stable queue-lane displacement and grows
            // as a moving carriage separates from its fixed source anchor.
            float desiredTravelSpeed = CatModelCatalog.WalkTravelSpeedAtOneX;
            if (scaleWalkPlayback && state == CatPresentationState.Walk)
            {
                desiredTravelSpeed = 0f;
                float safeBlendSpeed = float.IsNaN(platformBlendSpeed)
                    || float.IsInfinity(platformBlendSpeed)
                    ? 0f : Mathf.Max(0f, platformBlendSpeed);
                if (resolvePlatformPath && _hasPlatformAnchor)
                {
                    Transform board = transform.parent;
                    Vector3 seatBoard = board.InverseTransformPoint(
                        _carriage.TransformPoint(_catBaseLocalPosition));
                    Vector3 anchorBoard = board.InverseTransformPoint(
                        _platformAnchorWorldPosition);
                    desiredTravelSpeed = Vector3.Distance(seatBoard, anchorBoard)
                        * safeBlendSpeed;
                    if (float.IsNaN(desiredTravelSpeed)
                        || float.IsInfinity(desiredTravelSpeed))
                        desiredTravelSpeed = 0f;
                }
            }

            Vector3 pathLocalPosition = _catBaseLocalPosition;
            if (resolvePlatformPath && _hasPlatformAnchor)
            {
                Vector3 platformLocalPosition = _carriage.InverseTransformPoint(
                    _platformAnchorWorldPosition);
                pathLocalPosition = Vector3.Lerp(_catBaseLocalPosition,
                    platformLocalPosition, safePlatformBlend);
            }
            Vector3 pathOffset = pathLocalPosition - _catBaseLocalPosition;
            ResetVisualPose();
            if (motionOff)
            {
                ClearRigEarTwitch();
                if (platformWaiting)
                {
                    _cat.localPosition = pathLocalPosition;
                    _pin.localPosition = _pinBaseLocalPosition + pathOffset;
                }
                // Reduced motion cuts inbound walking/boarding to the actual seat. Source
                // waits and retained platform passengers keep their neutral standing pose.
                bool staticSeated = OriginalCarriageAdmitted && _rigPresentation != null
                    && _rigPresentation.OpenCarriageMotionInstalled
                    && (state == CatPresentationState.RideIdle
                        || (state == CatPresentationState.WaitingIdle && safePlatformBlend == 0f)
                        || (!movingToPlatform && (state == CatPresentationState.Walk
                            || state == CatPresentationState.Board)));
                PlayRig(state, true, desiredTravelSpeed, staticSeated);
                ApplyRigSeatDepth();
                return;
            }

            float safeTime = float.IsNaN(visualTime) || float.IsInfinity(visualTime)
                ? 0f : visualTime;
            if (resolvePlatformPath && _hasPlatformAnchor
                && state != CatPresentationState.Celebrate)
                FaceAlongPlatformPath(movingToPlatform,
                    _carriage.TransformPoint(_catBaseLocalPosition));
            bool arrival = state == CatPresentationState.Alight || state == CatPresentationState.Celebrate;
            CatMicroPose pose = _microMotion.Evaluate(safeTime, false, arrival);
            // ScreenUpOffset carries exactly 0.021 board units of screen-space vertical travel;
            // it is applied to the cat and its label pin, never to the train/root spline anchor.
            Vector3 boardBob = ScreenUpOffset(BoardSceneLook.BoardTilt, pose.Bob * 0.021f, 0f);
            Vector3 carriageLocalBob = Quaternion.Inverse(_carriage.localRotation) * boardBob;
            _catPathLocalPosition = pathLocalPosition;
            _pinPathLocalPosition = _pinBaseLocalPosition + pathOffset;
            _riderBobLocalOffset = carriageLocalBob;
            _hasRiderBobPose = true;
            _cat.localPosition = pathLocalPosition
                + carriageLocalBob;
            // The destination card labels the cat, not its empty seat. Carry the exact same
            // presentation-only path and bob deltas so the solved screen rise remains
            // invariant while neither transform can affect the authoritative train root.
            _pin.localPosition = _pinBaseLocalPosition + pathOffset + carriageLocalBob;
            ApplyPlaceholderGait(state, safeTime);
            Quaternion headTurn = Quaternion.Euler(0f, 0f, pose.ArrivalHeadTurnDegrees);
            _head.localRotation = headTurn * _headBaseLocalRotation;
            SetFeaturePose(_earLeft, _earLeftBaseLocalPosition, _earLeftBaseLocalRotation,
                headTurn, Quaternion.Euler(0f, 0f, pose.EarTwitchDegrees));
            SetFeaturePose(_earRight, _earRightBaseLocalPosition, _earRightBaseLocalRotation,
                headTurn, Quaternion.Euler(0f, 0f, -pose.EarTwitchDegrees));
            SetFeaturePose(_eyeLeft, _eyeLeftBaseLocalPosition, _eyeLeftBaseLocalRotation,
                headTurn, Quaternion.identity);
            SetFeaturePose(_eyeRight, _eyeRightBaseLocalPosition, _eyeRightBaseLocalRotation,
                headTurn, Quaternion.identity);
            SetFeaturePose(_muzzle, _muzzleBaseLocalPosition, _muzzleBaseLocalRotation,
                headTurn, Quaternion.identity);
            _eyeLeft.localScale = new Vector3(_eyeLeftBaseLocalScale.x,
                _eyeLeftBaseLocalScale.y * pose.EyeYScale, _eyeLeftBaseLocalScale.z);
            _eyeRight.localScale = new Vector3(_eyeRightBaseLocalScale.x,
                _eyeRightBaseLocalScale.y * pose.EyeYScale, _eyeRightBaseLocalScale.z);
            SetRigEarTwitch(pose.EarTwitchDegrees);
            // WaitingIdle also describes source/platform passengers. At the carriage seat,
            // keep the authored Ride loop and cache that effective playback state so stopping,
            // reversing and resuming do not restart it or replace the seated body with Idle.
            CatPresentationState rigState = state == CatPresentationState.WaitingIdle
                && safePlatformBlend == 0f && _rigPresentation != null
                && _rigPresentation.AuthoredMotionInstalled
                ? CatPresentationState.RideIdle : state;
            PlayRig(rigState, false, desiredTravelSpeed);
            ApplyRigSeatDepth();
            ApplyRigEarTwitch();
        }

        // GameRoot supplies presentation state in Update. Unity samples Animator after Update,
        // so the same additive pose is re-applied in LateUpdate to remain visible without ever
        // becoming an input to the deterministic simulation.
        private void LateUpdate()
        {
            ApplyRigSeatDepth();
            ApplyRigEarTwitch();
        }

        private void ApplyRigSeatDepth()
        {
            if (_rigInstance == null) return;
            float weight = 0f;
            bool openCarriageMotion = OriginalCarriageAdmitted && _rigPresentation != null
                && _rigPresentation.OpenCarriageMotionInstalled;
            if (openCarriageMotion && _presentationState != CatPresentationState.Hidden && _rigAnimator != null)
            {
                if (_rigMotionSuppressed) weight = _rigStaticSeated ? 1f : 0f;
                else
                {
                    weight = SeatWeight(_rigAnimator.GetCurrentAnimatorStateInfo(0));
                    if (_rigAnimator.IsInTransition(0))
                        weight = Mathf.Lerp(weight, SeatWeight(_rigAnimator.GetNextAnimatorStateInfo(0)),
                            Mathf.Clamp01(_rigAnimator.GetAnimatorTransitionInfo(0).normalizedTime));
                }
            }
            // Animated depth follows the evaluated clock rather than PlatformBlend:
            // boarding starts at blend .35 with a neutral pose.
            _rigInstance.transform.localPosition = Vector3.forward * (OpenCarriageSeatDepth * weight);
            if (openCarriageMotion && _hasRiderBobPose && !_rigMotionSuppressed)
            {
                // Rigid screen-up bob slides seated feet through the carriage walls. Fade
                // only that translation with the same evaluated seat contribution; the owned
                // breathing and ear motion remain. Absolute path samples cannot accumulate.
                Vector3 bob = _riderBobLocalOffset * (1f - weight);
                _cat.localPosition = _catPathLocalPosition + bob;
                _pin.localPosition = _pinPathLocalPosition + bob;
            }
        }

        private static float SeatWeight(AnimatorStateInfo state)
        {
            if (state.IsName("Base Layer.Cat_Ride")) return 1f;
            float phase = float.IsFinite(state.normalizedTime) ? Mathf.Clamp01(state.normalizedTime) : 0f;
            if (state.IsName("Base Layer.Cat_Board")) return Mathf.SmoothStep(0f, 1f, phase);
            if (state.IsName("Base Layer.Cat_Alight")) return 1f - Mathf.SmoothStep(0f, 1f, phase);
            return 0f;
        }

        private void FaceAlongPlatformPath(bool movingToPlatform, Vector3 seatWorldPosition)
        {
            Vector3 travelWorld = movingToPlatform
                ? _platformAnchorWorldPosition - seatWorldPosition
                : seatWorldPosition - _platformAnchorWorldPosition;
            Vector3 travelLocal = _carriage.InverseTransformDirection(travelWorld);
            travelLocal.z = 0f;
            if (travelLocal.sqrMagnitude <= 1e-8f) return;
            float angle = Mathf.Atan2(travelLocal.y, travelLocal.x) * Mathf.Rad2Deg;
            // TASK 17's +Z source-forward is adapted to Cat-local +X. Point that exact axis
            // down the path while retaining Cat-local -Z as tabletop up.
            _cat.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void ApplyPlaceholderGait(CatPresentationState state, float visualTime)
        {
            float maximum = state == CatPresentationState.Walk
                ? WalkLegSwingDegrees
                : state == CatPresentationState.Board || state == CatPresentationState.Alight
                    ? TransitionLegSwingDegrees : 0f;
            float swing = Mathf.Sin(visualTime * 18f) * maximum;
            _legLeft.localRotation = _legLeftBaseLocalRotation * Quaternion.Euler(0f, swing, 0f);
            _legRight.localRotation = _legRightBaseLocalRotation * Quaternion.Euler(0f, -swing, 0f);
        }

        private void SetFeaturePose(Transform feature, Vector3 baselinePosition,
            Quaternion baselineRotation, Quaternion headTurn, Quaternion localTwitch)
        {
            feature.localPosition = _head.localPosition
                + headTurn * (baselinePosition - _head.localPosition);
            feature.localRotation = headTurn * baselineRotation * localTwitch;
        }

        private void ApplyCatTint(Color color)
        {
            var properties = new MaterialPropertyBlock();
            for (int i = 0; i < _catRenderers.Length; i++)
            {
                _catRenderers[i].GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", color);
                properties.SetColor("_Color", color);
                _catRenderers[i].SetPropertyBlock(properties);
                properties.Clear();
            }
            if (_rigInstance == null) return;
            if (_rigFurTint != null)
            {
                _rigFurTint.Apply(color);
                return;
            }
            var rigRenderers = _rigInstance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rigRenderers.Length; i++)
            {
                rigRenderers[i].GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", color);
                properties.SetColor("_Color", color);
                rigRenderers[i].SetPropertyBlock(properties);
                properties.Clear();
            }
        }

        public void ShowRejection(float visualTime, bool arrivedInReverse = false)
        {
            _fx.Finish(this, 10);
            if (_rejected == null)
            {
                _rejected = new GameObject("Rejected").transform;
                _rejected.SetParent(_pin, false);
                const float rejectionScale = 1.6f;
                float rise = PinCardSize * (1f + rejectionScale) * 0.5f + 0.03f;
                _rejected.localPosition = new Vector3(0f, rise, -0.04f);
                _rejected.localScale = Vector3.one * rejectionScale;
                CreatePart("Card", _rejected, CatPinMeshBuilder.Card(), Vector3.zero,
                    new Vector3(PinCardSize, PinCardSize, PinCardDepth), Quaternion.identity, PinCardMaterial());
                CreatePart("Symbol", _rejected, CatPinMeshBuilder.StarBadge(),
                    new Vector3(0f, 0f, PinSymbolLocalZ), Vector3.one, Quaternion.identity, CatBasisMaterial());
                CreatePart("Cross-bar", _rejected, CubeMesh(), new Vector3(0f, 0f, -0.045f),
                    new Vector3(0.225f, 0.024f, 0.014f), Quaternion.Euler(0f, 0f, 42f), NavyMaterial());
            }
            var symbol = _rejected.Find("Symbol");
            var mesh = PinShape == DestinationShape.Star ? CatPinMeshBuilder.StarBadge()
                : DestinationShapeMesh.ForShape(PinShape);
            symbol.GetComponent<MeshFilter>().sharedMesh = mesh;
            symbol.localRotation = DestinationShapeMesh.PlateRotation(PinShape);
            symbol.localScale = ScaleForWorldSize(mesh, SymbolWorldSize(PinShape));
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", CatTint);
            tint.SetColor("_Color", CatTint);
            symbol.GetComponent<Renderer>().SetPropertyBlock(tint);
            _rejectedUntil = visualTime + 0.6f;
            _rejected.gameObject.SetActive(true);
            Vector3 back = -(Quaternion.Euler(0f, 0f, _headingDegrees) * Vector3.right) * 0.08f;
            if (arrivedInReverse) back = -back;
            _fx.Tween(this, 0.3f, p =>
            {
                _recoil = p >= 1f ? Vector3.zero : back * Mathf.Cos(p * Mathf.PI * 3f) * (1f - p) * (1f - p);
                ApplyVehicleOffsets();
            }, channel: 10);
        }

        private void ApplyVehicleOffsets()
        {
            if (_engine != null) _engine.localPosition = _recoil + Vector3.back * _engineBob;
            if (_carriage != null) _carriage.localPosition = _carriagePosition + _recoil;
        }

        private void ResetFeedback()
        {
            SetMoving(false);
            if (_fx != null) _fx.Finish(this, 10);
            _recoil = Vector3.zero;
            if (_rejected != null) _rejected.gameObject.SetActive(false);
            ApplyVehicleOffsets();
        }

        private void OnDisable()
        {
            ResetFeedback();
            if (_steam != null) _steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void SetMoving(bool moving, bool reverse = false)
        {
            moving = moving && _fx != null && !_fx.MotionOff;
            if (moving && _steam == null)
            {
                _steam = _fx.CreateParticles(_engine.Find("Funnel"), "Steam", BoardFxSprite.Puff);
                _steam.transform.localPosition = Vector3.down;
                var main = _steam.main;
                main.loop = true;
                main.startLifetime = 0.9f;
                main.startSize = 0.2f; // the shared 0.35 -> 1 curve gives 0.07 -> 0.20
                main.startSpeed = 0f;
                main.startColor = Palette.WarmPaper;
                main.scalingMode = ParticleSystemScalingMode.Shape;
                var emission = _steam.emission;
                emission.rateOverTime = 5f;
            }
            if (_steam != null)
            {
                var emission = _steam.emission;
                emission.enabled = moving;
                if (moving)
                {
                    Vector3 heading = transform.parent.TransformDirection(
                        Quaternion.Euler(0f, 0f, _headingDegrees) * Vector3.right).normalized;
                    if (reverse) heading = -heading;
                    var drift = _steam.velocityOverLifetime;
                    drift.enabled = true;
                    drift.space = ParticleSystemSimulationSpace.World;
                    Vector3 velocity = Vector3.back * 0.15f - heading * 0.1f;
                    drift.x = velocity.x; drift.y = velocity.y; drift.z = velocity.z;
                    if (!_steam.isPlaying) _steam.Play();
                }
                else if (_moving || _fx.MotionOff)
                    _steam.Stop(true, _fx.MotionOff ? ParticleSystemStopBehavior.StopEmittingAndClear
                        : ParticleSystemStopBehavior.StopEmitting);
            }
            _moving = moving;
            if (!moving) { _engineBob = 0f; ApplyVehicleOffsets(); }
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
            _carriagePosition = Vector3.zero;
            ApplyVehicleOffsets();
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
            _carriagePosition = path.EvaluateDistanceFraction(fraction) - headPosition;
            ApplyVehicleOffsets();
            SetCarriageHeading(HeadingDegrees(path.TangentDistanceFraction(fraction)));
        }

        // The carriage turns with the track; the CAT does not. Counter-rotating the cat by the
        // carriage's own heading leaves it at a fixed board-local yaw — the one that squares
        // its face and ear axis to the diorama camera — so a passenger reads identically on a
        // straight, through a curve, and parked at a node. This is the structural half of the
        // invisible-ears fix: without it, no ear size survives every heading.
        private void SetCarriageHeading(float degrees)
        {
            _hasRiderBobPose = false;
            _carriage.localRotation = Quaternion.Euler(0f, 0f, degrees);
            _catBaseLocalRotation = Quaternion.Euler(0f, 0f, CatBoardYaw - degrees);
            _cat.localRotation = _catBaseLocalRotation;
            // The pin gets the same treatment one dimension up. Undoing the carriage's turn
            // leaves it at a FIXED board-local pose — rotation and offset both — so the card
            // holds still, square to the camera and directly above its cat, on a straight,
            // through a curve and parked at a node. Counter-rotating the OFFSET as well as the
            // rotation is what keeps the pin from swinging around its cat like a bucket on a
            // rope as the train turns.
            Quaternion unturn = Quaternion.Inverse(Quaternion.Euler(0f, 0f, degrees));
            _pin.localRotation = unturn * PinBoardRotation;
            _pin.localPosition = unturn * PinBoardOffset;
            _pinBaseLocalPosition = _pin.localPosition;
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
            BuildEngineVisual();

            _carriage = new GameObject("Carriage").transform;
            _carriage.SetParent(transform, false);
            BuildCarriageVisual();

            // Keep the existing passenger anchors with either carriage. The primitive
            // fallback's chibi head spans 82% of its box width and its lower fifth intersects
            // the low wall; the authored open shell has separate rendered exposure checks.
            // Head and ears carry the line tint; the face is deliberately OUTSIDE the tinted
            // set, so the eyes stay near-black and the muzzle cream whatever colour the cat
            // is. Ears are 45-degree diamonds anchored in the head, splayed up and out.
            _cat = new GameObject("Cat").transform;
            _cat.SetParent(_carriage, false);
            var head = CreatePart("Head", _cat, SphereMesh(),
                new Vector3(0f, 0f, HeadCenterZ),
                new Vector3(HeadDiameter, HeadDiameter, HeadDiameter),
                Quaternion.identity, CatBasisMaterial());
            var earLeft = CreatePart("EarLeft", _cat, CubeMesh(),
                new Vector3(0f, EarLateral, EarCenterZ),
                new Vector3(EarThickness, EarSize, EarSize),
                Quaternion.Euler(45f, 0f, 0f), CatBasisMaterial());
            var earRight = CreatePart("EarRight", _cat, CubeMesh(),
                new Vector3(0f, -EarLateral, EarCenterZ),
                new Vector3(EarThickness, EarSize, EarSize),
                Quaternion.Euler(45f, 0f, 0f), CatBasisMaterial());
            _head = head.transform;
            _earLeft = earLeft.transform;
            _earRight = earRight.transform;

            // The face. Because the cat holds a fixed camera-facing yaw, these sit at a known
            // screen position for every train on every heading — so they can be placed once,
            // square to the camera, instead of hedged against rotation. Each is a builtin
            // sphere sunk into the head so it reads as a dome on the surface, never a decal
            // that could z-fight. Reuses the engine's two cached materials: no new material,
            // no property block, nothing to tear down.
            var eyeLeft = CreatePart("EyeLeft", _cat, SphereMesh(),
                new Vector3(EyeOffset.x, EyeOffset.y, EyeOffset.z),
                new Vector3(EyeSize, EyeSize, EyeSize),
                Quaternion.identity, NavyMaterial());
            var eyeRight = CreatePart("EyeRight", _cat, SphereMesh(),
                new Vector3(EyeOffset.x, -EyeOffset.y, EyeOffset.z),
                new Vector3(EyeSize, EyeSize, EyeSize),
                Quaternion.identity, NavyMaterial());
            var muzzle = CreatePart("Muzzle", _cat, SphereMesh(),
                MuzzleOffset, MuzzleSize,
                Quaternion.identity, CreamMaterial());
            _eyeLeft = eyeLeft.transform;
            _eyeRight = eyeRight.transform;
            _muzzle = muzzle.transform;

            // A sphere's neutral render is rotationally symmetric, so rotate its local basis
            // without changing its neutral appearance: local Y is now the projected screen-up
            // direction at the fixed Cat board yaw, making blink collapse read vertically.
            Quaternion fixedCatYaw = Quaternion.Euler(0f, 0f, CatBoardYaw);
            Vector3 screenUp = ScreenUpOffset(BoardSceneLook.BoardTilt, 1f, 0f).normalized;
            Quaternion eyeBasis = Quaternion.FromToRotation(Vector3.up,
                Quaternion.Inverse(fixedCatYaw) * screenUp);
            _eyeLeft.localRotation = eyeBasis;
            _eyeRight.localRotation = eyeBasis;

            // A Tier-1 body and legs scaled with the enlarged head give walking/alighting cats
            // a readable silhouette; their neutral lower edges meet the -0.2 root's tabletop.
            // These use the same builtin meshes and bounds-derived scale as every train part:
            // no primitive factory, colliders, or owned generated asset.
            var body = CreatePart("Body", _cat, SphereMesh(),
                new Vector3(-0.050f * CatFeatureScale, 0f,
                    -HeadAnchorZ - PlaceholderBodyWorldSize.z * 0.5f), PlaceholderBodyWorldSize,
                Quaternion.identity, CatBasisMaterial());
            var legLeft = CreatePart("LegLeft", _cat, CubeMesh(),
                new Vector3(-0.055f * CatFeatureScale, 0.078f * CatFeatureScale,
                    -HeadAnchorZ - PlaceholderLegWorldSize.z * 0.5f), PlaceholderLegWorldSize,
                Quaternion.identity, CatBasisMaterial());
            var legRight = CreatePart("LegRight", _cat, CubeMesh(),
                new Vector3(-0.055f * CatFeatureScale, -0.078f * CatFeatureScale,
                    -HeadAnchorZ - PlaceholderLegWorldSize.z * 0.5f), PlaceholderLegWorldSize,
                Quaternion.identity, CatBasisMaterial());
            _legLeft = legLeft.transform;
            _legRight = legRight.transform;
            _bodyLegs = new[] { body.transform, legLeft.transform, legRight.transform };
            _catRenderers = new[] { head, earLeft, earRight, body, legLeft, legRight };

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
            _statusMarks = PassengerStatusMarks.Create(_pin, false);
            _statusMarks.transform.localPosition = new Vector3(-0.077f, -0.076f, -0.046f);
            _statusMarks.transform.localScale = Vector3.one * 0.055f;

            SetCarriageHeading(0f); // a consist faces the camera before its first placement
            _catBaseLocalPosition = _cat.localPosition;
            _headBaseLocalRotation = _head.localRotation;
            _earLeftBaseLocalRotation = _earLeft.localRotation;
            _earRightBaseLocalRotation = _earRight.localRotation;
            _eyeLeftBaseLocalRotation = _eyeLeft.localRotation;
            _eyeRightBaseLocalRotation = _eyeRight.localRotation;
            _muzzleBaseLocalRotation = _muzzle.localRotation;
            _legLeftBaseLocalRotation = _legLeft.localRotation;
            _legRightBaseLocalRotation = _legRight.localRotation;
            _earLeftBaseLocalPosition = _earLeft.localPosition;
            _earRightBaseLocalPosition = _earRight.localPosition;
            _eyeLeftBaseLocalPosition = _eyeLeft.localPosition;
            _eyeRightBaseLocalPosition = _eyeRight.localPosition;
            _muzzleBaseLocalPosition = _muzzle.localPosition;
            _eyeLeftBaseLocalScale = _eyeLeft.localScale;
            _eyeRightBaseLocalScale = _eyeRight.localScale;
            SetBodyLegVisibility(false);
            TryInstallRig();
        }

        private void ResetVisualPose()
        {
            _hasRiderBobPose = false;
            if (_rigInstance != null) _rigInstance.transform.localPosition = Vector3.zero;
            _cat.localPosition = _catBaseLocalPosition;
            _cat.localRotation = _catBaseLocalRotation;
            _head.localRotation = _headBaseLocalRotation;
            _earLeft.localRotation = _earLeftBaseLocalRotation;
            _earRight.localRotation = _earRightBaseLocalRotation;
            _eyeLeft.localRotation = _eyeLeftBaseLocalRotation;
            _eyeRight.localRotation = _eyeRightBaseLocalRotation;
            _muzzle.localRotation = _muzzleBaseLocalRotation;
            _legLeft.localRotation = _legLeftBaseLocalRotation;
            _legRight.localRotation = _legRightBaseLocalRotation;
            _pin.localPosition = _pinBaseLocalPosition;
            _earLeft.localPosition = _earLeftBaseLocalPosition;
            _earRight.localPosition = _earRightBaseLocalPosition;
            _eyeLeft.localPosition = _eyeLeftBaseLocalPosition;
            _eyeRight.localPosition = _eyeRightBaseLocalPosition;
            _muzzle.localPosition = _muzzleBaseLocalPosition;
            _eyeLeft.localScale = _eyeLeftBaseLocalScale;
            _eyeRight.localScale = _eyeRightBaseLocalScale;
        }

        private void SetBodyLegVisibility(bool visible)
        {
            for (int i = 0; i < _bodyLegs.Length; i++)
                _bodyLegs[i].gameObject.SetActive(visible);
        }

        private void TryInstallRig()
        {
            var catalog = _catCatalog ?? CatModelCatalog.LoadResources();
            _rigFallbackReason = catalog.RejectionReason;
            if (!catalog.TryInstantiate(_cat, out _rigInstance)) return;

            var animators = _rigInstance.GetComponentsInChildren<Animator>(true);
            if (animators.Length != 1)
            {
                _rigFallbackReason = "Instantiated cat rig must contain exactly one Animator.";
                DestroyOwned(_rigInstance);
                _rigInstance = null;
                return;
            }

            _rigAnimator = animators[0];
            _rigPresentation = _rigAnimator.GetComponent<CatRigPresentation>();
            if (OriginalCarriageAdmitted) _rigPresentation?.TryUseOpenCarriageMotion();
            // Resource/weighted-head admission already identifies the calibrated paid rig.
            // Only its board clone gets a coat material; profile mounts keep the source atlas.
            if (_rigPresentation != null) _rigFurTint = BoardFurTint.TryInstall(_rigInstance);
            _rigAnimator.applyRootMotion = false;
            _rigInstance.transform.localPosition = Vector3.zero;
            // TASK 17 imports conventional +Y-up, +Z-forward content. This presentation-only
            // adapter stands it on Cat's -Z tabletop-up axis and points it along Cat's +X face.
            _rigInstance.transform.localRotation = Quaternion.LookRotation(
                Vector3.right, Vector3.back);
            _rigInstance.transform.localScale = Vector3.one * CatModelCatalog.PresenterScale;
            ResolveRigTierOneControls();
            _rigAdmitted = true;
            _rigFallbackReason = string.Empty;
            SetPlaceholderRenderersVisible(false);
            if (_catColorApplied)
                ApplyCatTint(CatLine.ColorOf(_appliedColorCode));
        }

        private void ResolveRigTierOneControls()
        {
            Transform branchA = _rigAnimator.transform.Find(CatModelCatalog.EarDeformerPathA);
            Transform branchB = _rigAnimator.transform.Find(CatModelCatalog.EarDeformerPathB);
            if (branchA == null || branchB == null) return;

            bool branchAInSkin = false;
            bool branchBInSkin = false;
            var skins = _rigAnimator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int skinIndex = 0; skinIndex < skins.Length; skinIndex++)
            {
                Transform[] bones = skins[skinIndex].bones;
                for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
                {
                    branchAInSkin |= bones[boneIndex] == branchA;
                    branchBInSkin |= bones[boneIndex] == branchB;
                }
            }
            if (!branchAInSkin || !branchBInSkin) return;

            _rigEarDeformerA = branchA;
            _rigEarDeformerB = branchB;
            _rigEarTwitchSupported = true;
        }

        private void SetRigEarTwitch(float degrees)
        {
            if (!_rigEarTwitchSupported) return;
            _rigEarTwitchDegrees = Mathf.Clamp(degrees,
                -CatMicroMotion.EarTwitchMaximumDegrees,
                CatMicroMotion.EarTwitchMaximumDegrees) * RigEarTwitchGain;
            _rigEarTwitchActive = true;
        }

        private void ApplyRigEarTwitch()
        {
            if (!_rigEarTwitchSupported || !_rigEarTwitchActive) return;

            Quaternion offsetA = Quaternion.Euler(0f, 0f, _rigEarTwitchDegrees);
            Quaternion offsetB = Quaternion.Euler(0f, 0f, -_rigEarTwitchDegrees);
            Quaternion sampledA = AnimationSampleWithoutPreviousOffset(
                _rigEarDeformerA.localRotation, _rigEarALastApplied,
                _rigEarAPreviousOffset, _rigEarPoseApplied);
            Quaternion sampledB = AnimationSampleWithoutPreviousOffset(
                _rigEarDeformerB.localRotation, _rigEarBLastApplied,
                _rigEarBPreviousOffset, _rigEarPoseApplied);
            _rigEarDeformerA.localRotation = sampledA * offsetA;
            _rigEarDeformerB.localRotation = sampledB * offsetB;
            _rigEarAPreviousOffset = offsetA;
            _rigEarBPreviousOffset = offsetB;
            _rigEarALastApplied = _rigEarDeformerA.localRotation;
            _rigEarBLastApplied = _rigEarDeformerB.localRotation;
            _rigEarPoseApplied = true;
        }

        private void ClearRigEarTwitch()
        {
            _rigEarTwitchActive = false;
            _rigEarTwitchDegrees = 0f;
            if (!_rigEarTwitchSupported || !_rigEarPoseApplied) return;

            _rigEarDeformerA.localRotation = AnimationSampleWithoutPreviousOffset(
                _rigEarDeformerA.localRotation, _rigEarALastApplied,
                _rigEarAPreviousOffset, true);
            _rigEarDeformerB.localRotation = AnimationSampleWithoutPreviousOffset(
                _rigEarDeformerB.localRotation, _rigEarBLastApplied,
                _rigEarBPreviousOffset, true);
            _rigEarAPreviousOffset = Quaternion.identity;
            _rigEarBPreviousOffset = Quaternion.identity;
            _rigEarPoseApplied = false;
        }

        private static Quaternion AnimationSampleWithoutPreviousOffset(Quaternion current,
            Quaternion lastApplied, Quaternion previousOffset, bool hadPreviousOffset)
        {
            // If Animator did not rewrite this branch, current is our prior final pose and the
            // old additive must be removed before the new one is applied. If Animator sampled
            // a walk pose in between, preserve that fresh sample instead. This avoids both
            // accumulation on the padded idle clips and clobbering the authored walk motion.
            return hadPreviousOffset && Quaternion.Angle(current, lastApplied) < 0.001f
                ? current * Quaternion.Inverse(previousOffset)
                : current;
        }

        private void PlayRig(CatPresentationState state, bool motionOff,
            float desiredTravelSpeed, bool staticSeated = false)
        {
            if (!_rigAdmitted || _rigAnimator == null) return;
            _rigAnimator.applyRootMotion = false;
            if (motionOff)
            {
                if (_rigMotionSuppressed && _rigStaticSeated == staticSeated) return;
                _rigMotionSuppressed = true;
                _rigStaticSeated = staticSeated;
                _rigAnimator.Rebind();
                _rigAnimator.Play(_rigAnimator.GetLayerName(0) + "."
                    + (staticSeated ? CatRigPresentation.RideClip : CatModelCatalog.IdleSitClip), 0, 0f);
                _rigAnimator.Update(0f);
                _rigPresentation?.ApplyHeadShape();
                _rigAnimator.speed = 0f;
                _rigNeutralSampleCount++;
                _lastRigState = CatPresentationState.Hidden;
                return;
            }

            _rigMotionSuppressed = false;
            _rigStaticSeated = false;
            float playbackSpeed = 1f;
            if (state == CatPresentationState.Walk)
            {
                float safeTravelSpeed = float.IsNaN(desiredTravelSpeed)
                    || float.IsInfinity(desiredTravelSpeed)
                    ? 0f : Mathf.Max(0f, desiredTravelSpeed);
                playbackSpeed = safeTravelSpeed / CatModelCatalog.WalkTravelSpeedAtOneX;
            }
            // A stored source anchor can separate farther from a moving carriage without a
            // presentation-state transition, so retime before the same-state early return.
            _rigAnimator.speed = playbackSpeed;
            if (_lastRigState == state) return;
            string clip = state == CatPresentationState.RideIdle
                && _rigPresentation != null && _rigPresentation.AuthoredMotionInstalled
                ? CatRigPresentation.RideClip : CatModelCatalog.ClipFor(state);
            _rigAnimator.Play(_rigAnimator.GetLayerName(0) + "." + clip, 0, 0f);
            _rigAnimator.Update(0f); // presentation sampling only; root motion stays disabled.
            _rigPresentation?.ApplyHeadShape();
            _lastRigState = state;
        }

        private void SetPlaceholderRenderersVisible(bool visible)
        {
            for (int i = 0; i < _catRenderers.Length; i++)
                _catRenderers[i].enabled = visible;
            _eyeLeft.GetComponent<MeshRenderer>().enabled = visible;
            _eyeRight.GetComponent<MeshRenderer>().enabled = visible;
            _cat.Find("Muzzle").GetComponent<MeshRenderer>().enabled = visible;
        }

        private void BuildEngineVisual()
        {
            EngineModelCatalog catalog = _engineCatalog ?? EngineModelCatalog.LoadResources();
            OriginalEngineAdmitted = catalog.TryGetPrefab(out GameObject prefab);
            EngineFallbackReason = catalog.RejectionReason;
            if (!OriginalEngineAdmitted) return;
            GameObject model = Instantiate(prefab, _engine, false);
            model.name = "OriginalEngine";
            model.transform.localPosition = new Vector3(0f, 0f, .235f);
            model.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            model.transform.localScale = Vector3.one;
            // Retain every legacy attachment transform, especially Funnel's original
            // scale/rotation used by Steam. Only its primitive renderer is replaced.
            foreach (string name in new[] { "Chassis", "Boiler", "Cab", "CabRoof", "Funnel" })
                _engine.Find(name).GetComponent<MeshRenderer>().enabled = false;
        }

        private void BuildCarriageVisual()
        {
            CarriageModelCatalog catalog = _carriageCatalog ?? CarriageModelCatalog.LoadResources();
            OriginalCarriageAdmitted = catalog.TryGetPrefab(out GameObject prefab);
            CarriageFallbackReason = catalog.RejectionReason;
            if (OriginalCarriageAdmitted)
            {
                // The original model is +X forward / +Y up at immutable scale. Only
                // this visual wrapper adapts it to board-local -Z up and the rail crown.
                // Cat, pin, vehicle anchors and simulation never inherit this correction.
                GameObject model = Instantiate(prefab, _carriage, false);
                model.name = "OriginalCarriage";
                model.transform.localPosition = new Vector3(0f, 0f, .235f);
                model.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                model.transform.localScale = Vector3.one;
                return;
            }
            CreatePart("Chassis", _carriage, CubeMesh(),
                new Vector3(0f, 0f, 0.205f), new Vector3(0.42f, 0.46f, 0.06f),
                Quaternion.identity, NavyMaterial());
            CreatePart("Body", _carriage, CubeMesh(),
                new Vector3(0f, 0f, 0.185f), new Vector3(0.40f, 0.44f, 0.10f),
                Quaternion.identity, CreamMaterial());
        }

        private static void DestroyOwned(GameObject instance)
        {
            if (UnityEngine.Application.isPlaying) Destroy(instance);
            else DestroyImmediate(instance);
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
