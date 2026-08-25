using UnityEngine;
using UnityEngine.Rendering;

namespace CatMetro.Presentation.Board
{
    /// <summary>Owns the scene-wide visual treatment shared by every authored level.</summary>
    public static class BoardSceneLook
    {
        public static readonly Color WarmBackground = new Color(0.85f, 0.81f, 0.73f);

        private const string KeyLightName = "Diorama Warm Key";
        private const float TargetPortraitAspect = 9f / 19.5f;
        // These fractions serve the safe-frame law in RuntimeSceneRigTests.AssertInside:
        // viewport x in (0.055, 0.945), y in (0.12, 0.87), asserted at the pinned phone
        // aspect 917/2048 (~0.4478). The fit must assume an aspect before the camera knows
        // its real surface, and TargetPortraitAspect (~0.4615) is wider than the pinned
        // one, which squeezes content outward at assertion time. With the 1.05 pad:
        //   x extremes = 0.5 +/- TargetAspect*SafeWidth / (2*1.05*0.4478) -> [0.068, 0.932]
        //   y extremes = 0.495 +/- SafeHeight / (2*1.05)                  -> [0.133, 0.857]
        // ~0.013 inside the law on every edge. The old 0.93/0.78 put x extremes at 0.9565
        // (outside the law — the furnished-board signpost failure) and passed vertically by
        // only 0.0036. Do not widen these without re-deriving both bands.
        private const float SafeWidth = 0.88f;
        private const float SafeHeight = 0.76f;
        // Public because the diorama tilt is the ONLY thing that decides which way a board-local
        // feature faces the (identity-rotated, orthographic) camera. ToyTrainView derives the
        // cat's fixed facing from it rather than hardcoding a yaw that would silently rot if
        // this tilt were ever re-authored.
        public static readonly Quaternion BoardTilt = Quaternion.Euler(38f, -32f, -4f);

        // Declared below BoardTilt on purpose: feat/cats-on-trains inserts its comment block
        // at exactly that line, and an insertion of our own at the same point turns two
        // compatible edits into a merge conflict.
        //
        // The fit reaches this far in when a level's content is small. Lowered from 7 with
        // the content-driven width fit below: that fit asks for ~20-25% less size than the
        // slab-driven one did, so a floor of 7 would have become the binding constraint on
        // most levels and silently cancelled the change.
        private const float MinOrthoSize = 5f;
        // Widest portrait aspect the desk's near-plane clearance is solved for. 4:3 covers
        // every phone (the pinned one is 917/2048 ~ 0.448) and tablets too. Solving at 16:9
        // left a 4:3 frame's lower corners 0.1-0.3 units behind the near plane; solving here
        // costs ~1.2 units of camera pull-back, which the smaller frame now affords.
        private const float DeskCoverageAspect = 0.75f;
        private const float DeskNearClearance = 1f;

        public static void Apply(Transform owner, Camera camera, BoardView board)
        {
            var environment = owner.GetComponent<BoardSceneEnvironmentScope>();
            if (environment == null)
                environment = owner.gameObject.AddComponent<BoardSceneEnvironmentScope>();
            environment.Capture();
            ApplyBackground(camera);
            TiltAroundAuthoredCenter(board);
            FitCamera(camera, board);
            ApplyLighting(owner);
        }

        public static void ApplyBackground(Camera camera)
        {
            RenderSettings.skybox = null;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = WarmBackground;
        }

        private static void TiltAroundAuthoredCenter(BoardView board)
        {
            Vector3 center = board.PresentationCenterLocal;
            board.transform.localRotation = BoardTilt;
            board.transform.localPosition = center - BoardTilt * center;
        }

        private static void FitCamera(Camera camera, BoardView board)
        {
            // Two unions, because the frame owes them different things.
            //   frameBounds   — everything except the desk. Nothing here may leave the frame
            //                   vertically, so the toy's rim still reads as a finite edge.
            //   contentBounds — the same minus the decorative slab (BoardBody). This is what
            //                   the RuntimeSceneRigTests safe-frame law actually governs:
            //                   node markers and prop renderers.
            // The slab is ~1.25x wider on screen than the content it carries, so fitting the
            // slab into the horizontal safe band shrank the whole diorama to satisfy a border
            // no law asks about. Target-01 lets the board run off both edges; so do we now.
            Bounds frameBounds = default, contentBounds = default;
            bool foundFrame = false, foundContent = false;
            var deskSurface = board.transform.Find("DeskSurface");
            var slab = board.transform.Find("BoardBody");
            foreach (var renderer in board.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                if (deskSurface != null && renderer.transform.IsChildOf(deskSurface)) continue;
                if (!foundFrame) { frameBounds = renderer.bounds; foundFrame = true; }
                else frameBounds.Encapsulate(renderer.bounds);
                if (slab != null && renderer.transform.IsChildOf(slab)) continue;
                if (!foundContent) { contentBounds = renderer.bounds; foundContent = true; }
                else contentBounds.Encapsulate(renderer.bounds);
            }

            if (!foundFrame)
            {
                Vector3 center = board.transform.TransformPoint(board.PresentationCenterLocal);
                frameBounds = new Bounds(center, Vector3.one);
            }
            // A board with no gameplay renderers at all still has to frame something.
            if (!foundContent) contentBounds = frameBounds;

            float requiredForHeight = frameBounds.size.y * 0.5f / SafeHeight;
            float requiredForWidth = contentBounds.size.x * 0.5f
                / (TargetPortraitAspect * SafeWidth);
            float size = Mathf.Max(MinOrthoSize,
                Mathf.Max(requiredForHeight, requiredForWidth) * 1.05f);
            float safeCenterY = (0.13f + 0.86f) * 0.5f;
            // Centre X on the content the law governs, not on the slab that may be lopsided
            // around it; centre Y on the full frame so the rim stays inside top and bottom.
            float cameraX = contentBounds.center.x;
            float cameraY = frameBounds.center.y - (safeCenterY - 0.5f) * 2f * size;

            float cameraZ = -10f;
            float farthestZ = frameBounds.max.z;
            foreach (var renderer in board.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                if (deskSurface != null && renderer.transform.IsChildOf(deskSurface)) continue;
                cameraZ = Mathf.Min(cameraZ, renderer.bounds.min.z - DeskNearClearance);
            }

            // The desk is a huge tilted slab, so its bounding box dips toward the camera far
            // outside the frame. Pulling the camera back to clear that off-screen corner cost
            // real shadow-distance budget (it put the board ~20 units deep) while clearing the
            // *visible* desk by only ~0.06 units at the pinned aspect — accidental, not safe.
            // Solve the near plane against the desk plane where it is actually on screen.
            var deskTop = deskSurface != null ? deskSurface.Find("DeskTop") : null;
            if (deskTop != null)
            {
                Vector3 normal = deskTop.forward;
                if (Mathf.Abs(normal.z) > 0.01f)
                {
                    Vector3 face = deskTop.TransformPoint(new Vector3(0f, 0f, -0.5f));
                    for (int corner = 0; corner < 4; corner++)
                    {
                        float x = cameraX + ((corner & 1) == 0 ? -1f : 1f)
                            * size * DeskCoverageAspect;
                        float y = cameraY + ((corner & 2) == 0 ? -1f : 1f) * size;
                        float z = face.z
                            - (normal.x * (x - face.x) + normal.y * (y - face.y)) / normal.z;
                        cameraZ = Mathf.Min(cameraZ, z - DeskNearClearance);
                    }
                }
            }
            if (deskSurface != null)
                foreach (var renderer in deskSurface.GetComponentsInChildren<Renderer>(true))
                    farthestZ = Mathf.Max(farthestZ, renderer.bounds.max.z);

            camera.orthographic = true;
            camera.orthographicSize = size;
            camera.transform.rotation = Quaternion.identity;
            camera.transform.position = new Vector3(cameraX, cameraY, cameraZ);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = Mathf.Max(50f, farthestZ - cameraZ + 1f);
            camera.allowHDR = false;
            camera.allowMSAA = true;
        }

        private static void ApplyLighting(Transform owner)
        {
            // Cool fill under a warm key. This is the inverse of what stood here — amber sky
            // over cool ground — and the inversion is the fix for a class-level bug, not a
            // taste change.
            //
            // What was wrong. Measured off the 2026-08-25 r3 render by fitting two COPLANAR
            // surfaces of known albedo (the WoodTop and the CreamRim share the board's normal,
            // so albedo is the only variable between them), the whole rig reduces to
            //
            //     rendered_linear = S * (albedo_linear + 0.0254)
            //
            // with S = (1.17, 0.678, 0.354) — blue attenuated to 0.303 of red. The additive
            // 0.0254 is itself proportional to S in all three channels, so it is the light's
            // own colour, not a neutral floor. Consequences, all measured on that render:
            // CreamCard rendered (255, 200, 138) with red CLIPPED and b-r -117; InkNavy rails
            // rendered (57, 52, 51); the MetroTeal switch wedge rendered (121, 130, 94), an
            // olive. target-01 does none of this: its cream reads (243, 226, 197) unclipped
            // with b-r -46, its rails (69, 75, 95) and its navy roofs (52, 58, 80) at b-r +28,
            // its teal (51, 120, 115) at b-r +64. Dividing the target's cream by a CreamCard
            // albedo puts its illuminant near (1.00, 0.91, 0.78): warm, but nowhere near the
            // amber stamp ours was applying. The target's warmth lives in its ALBEDOS — its
            // board divides out to roughly (207, 144, 101) — while its light stays close to
            // neutral. Ours had it backwards, and a light that starved of blue cannot render
            // any cool albedo cool. docs/LOOK.md names navy and teal in the palette, so this
            // was defeating the art direction at the class level, not just on the rails.
            //
            // Why the ambient was the wrong half of it. The board's visible normal is
            // (0.418, 0.616, -0.668) — n.y = 0.616, so every surface the player looks at is
            // upward-facing and draws almost entirely on the SKY band. The cool ground colour
            // was only ever reaching downward faces, which is to say nothing. Cooling the fill
            // means cooling the sky.
            //
            // What this is. Sky and equator go to a cool blue-grey; the ground stays warm,
            // which is physically what it is — bounce off a wooden desk. Intensity rises to
            // 1.15 to hold the fill's share of the total. With the key below, the illuminant
            // becomes S = (1.063, 0.890, 0.738), i.e. blue at 0.694 of red against 0.303.
            // Predicted from the measured transfer: InkNavy renders (59, 62, 74), b-r +15;
            // MetroTeal (77, 170, 151), b-r +74; CreamCard (252, 225, 193), b-r -59 and no
            // longer clipping. The warm mood is not spent — it moves into the wood albedos
            // (re-solved in BoardSurface so the board still renders (204, 130, 87), which is
            // target-01's board to within three units) and into the warm/cool split that a
            // cool fill under a warm key finally makes possible. Before this, lit and shaded
            // faces were both amber, which is the flatness the raking key was fighting alone.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.59f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.44f, 0.47f, 0.53f);
            RenderSettings.ambientGroundColor = new Color(0.41f, 0.37f, 0.33f);
            RenderSettings.ambientIntensity = 1.15f;

            var existing = owner.Find(KeyLightName);
            var key = existing == null
                ? new GameObject(KeyLightName).AddComponent<Light>()
                : existing.GetComponent<Light>();
            if (existing == null) key.transform.SetParent(owner, false);
            key.type = LightType.Directional;
            // Warm, but no longer amber enough to stamp its own hue on every albedo in the
            // game. The old (1, 0.78, 0.56) is (1, 0.571, 0.274) once Unity linearises it —
            // a key with barely a quarter of its red in blue — and since the key carries
            // ~85% of the illumination, that single number is what made navy render brown and
            // teal render olive. (1, 0.90, 0.78) linearises to (1, 0.787, 0.565): still
            // unmistakably late-afternoon against a cool fill, but it lets a cool albedo stay
            // cool. Intensity drops 1.18 -> 1.02 because the fill above now carries more of
            // the total; together they land the illuminant at (1.063, 0.890, 0.738).
            key.color = new Color(1f, 0.90f, 0.78f);
            key.intensity = 1.02f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.55f;
            key.shadowBias = 0.08f;
            key.shadowNormalBias = 0.25f;
            key.shadowNearPlane = 0.2f;
            // Geometry note: the board is tilted Euler(38,-32,-4), so its visible surface
            // normal is ~(0.42, 0.62, -0.67). The old key at Euler(35,-30,2) sat within
            // ~2.5 degrees of that normal — square-on light, which is why the diorama read
            // flat-lit with near-zero shadow length. This pose keeps the light high-right in
            // frame but ~28 degrees off the board normal, so it rakes: shadows stretch to
            // roughly half an object's height toward frame-left and vertical faces split
            // into lit and shaded sides. Renderer positions are untouched, so the 24-unit
            // shadow-distance law in RuntimeSceneRigTests still holds.
            key.transform.localRotation = Quaternion.Euler(19f, -56f, 0f);
        }
    }

    /// <summary>Returns scene-global render settings when the owning game root goes away.</summary>
    internal sealed class BoardSceneEnvironmentScope : MonoBehaviour
    {
        private static int _activeOwners;
        private static bool _hasSnapshot;
        private static Material _skybox;
        private static AmbientMode _ambientMode;
        private static Color _ambientSky;
        private static Color _ambientEquator;
        private static Color _ambientGround;
        private static float _ambientIntensity;
        private bool _ownsLease;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedState()
        {
            _activeOwners = 0;
            _hasSnapshot = false;
            _skybox = null;
        }

        public void Capture()
        {
            if (_ownsLease) return;
            if (_activeOwners == 0)
            {
                _skybox = RenderSettings.skybox;
                _ambientMode = RenderSettings.ambientMode;
                _ambientSky = RenderSettings.ambientSkyColor;
                _ambientEquator = RenderSettings.ambientEquatorColor;
                _ambientGround = RenderSettings.ambientGroundColor;
                _ambientIntensity = RenderSettings.ambientIntensity;
                _hasSnapshot = true;
            }
            _activeOwners++;
            _ownsLease = true;
        }

        private void OnDestroy()
        {
            if (!_ownsLease) return;
            _ownsLease = false;
            _activeOwners = Mathf.Max(0, _activeOwners - 1);
            if (_activeOwners != 0 || !_hasSnapshot) return;
            RenderSettings.skybox = _skybox;
            RenderSettings.ambientMode = _ambientMode;
            RenderSettings.ambientSkyColor = _ambientSky;
            RenderSettings.ambientEquatorColor = _ambientEquator;
            RenderSettings.ambientGroundColor = _ambientGround;
            RenderSettings.ambientIntensity = _ambientIntensity;
            _hasSnapshot = false;
            _skybox = null;
        }
    }
}
