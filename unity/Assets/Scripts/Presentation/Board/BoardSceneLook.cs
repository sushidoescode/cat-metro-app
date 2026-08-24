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
        private const float SafeWidth = 0.93f;
        private const float SafeHeight = 0.78f;
        private static readonly Quaternion BoardTilt = Quaternion.Euler(38f, -32f, -4f);

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
            Bounds visibleBounds = default;
            bool found = false;
            var deskSurface = board.transform.Find("DeskSurface");
            foreach (var renderer in board.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                if (deskSurface != null && renderer.transform.IsChildOf(deskSurface)) continue;
                if (!found)
                {
                    visibleBounds = renderer.bounds;
                    found = true;
                }
                else visibleBounds.Encapsulate(renderer.bounds);
            }

            if (!found)
            {
                Vector3 center = board.transform.TransformPoint(board.PresentationCenterLocal);
                visibleBounds = new Bounds(center, Vector3.one);
            }

            float requiredForHeight = visibleBounds.size.y * 0.5f / SafeHeight;
            float requiredForWidth = visibleBounds.size.x * 0.5f
                / (TargetPortraitAspect * SafeWidth);
            float size = Mathf.Max(7f, Mathf.Max(requiredForHeight, requiredForWidth) * 1.05f);
            float safeCenterY = (0.13f + 0.86f) * 0.5f;
            float cameraZ = -10f;
            float farthestZ = visibleBounds.max.z;
            if (deskSurface != null)
            {
                foreach (var renderer in deskSurface.GetComponentsInChildren<Renderer>(true))
                {
                    cameraZ = Mathf.Min(cameraZ, renderer.bounds.min.z - 1f);
                    farthestZ = Mathf.Max(farthestZ, renderer.bounds.max.z);
                }
            }

            camera.orthographic = true;
            camera.orthographicSize = size;
            camera.transform.rotation = Quaternion.identity;
            camera.transform.position = new Vector3(
                visibleBounds.center.x,
                visibleBounds.center.y - (safeCenterY - 0.5f) * 2f * size,
                cameraZ);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = Mathf.Max(50f, farthestZ - cameraZ + 1f);
            camera.allowHDR = false;
            camera.allowMSAA = true;
        }

        private static void ApplyLighting(Transform owner)
        {
            // A three-band ambient fill keeps the navy readable without introducing another
            // per-object light. The ground is cooler/darker than the warm sky so the slab keeps
            // its toy-like volume even in shadow.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.54f, 0.46f);
            RenderSettings.ambientEquatorColor = new Color(0.44f, 0.38f, 0.34f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.31f, 0.34f);
            RenderSettings.ambientIntensity = 1f;

            var existing = owner.Find(KeyLightName);
            var key = existing == null
                ? new GameObject(KeyLightName).AddComponent<Light>()
                : existing.GetComponent<Light>();
            if (existing == null) key.transform.SetParent(owner, false);
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.82f, 0.67f);
            key.intensity = 1.05f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.38f;
            key.shadowBias = 0.08f;
            key.shadowNormalBias = 0.25f;
            key.shadowNearPlane = 0.2f;
            key.transform.localRotation = Quaternion.Euler(35f, -30f, 2f);
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
