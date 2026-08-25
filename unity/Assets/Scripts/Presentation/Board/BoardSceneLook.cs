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
            // A three-band ambient fill keeps the navy readable without introducing another
            // per-object light. Warm amber sky over a cool navy ground is the late-afternoon
            // split from the target renders; intensity sits below 1 so the key's raking is
            // what separates lit faces from shaded ones instead of the fill flattening both.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.47f, 0.36f);
            RenderSettings.ambientEquatorColor = new Color(0.40f, 0.34f, 0.31f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.26f, 0.33f);
            RenderSettings.ambientIntensity = 0.92f;

            var existing = owner.Find(KeyLightName);
            var key = existing == null
                ? new GameObject(KeyLightName).AddComponent<Light>()
                : existing.GetComponent<Light>();
            if (existing == null) key.transform.SetParent(owner, false);
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.78f, 0.56f);
            key.intensity = 1.18f;
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
