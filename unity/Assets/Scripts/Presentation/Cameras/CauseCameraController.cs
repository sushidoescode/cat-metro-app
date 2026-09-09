using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CatMetro.Presentation.Fx;
using CatMetro.Presentation.Theme;

namespace CatMetro.Presentation.Cameras
{
    // CM-C3 criteria 1/3/4/5: frames the causal node on failure. Motion OFF (toggle or OS
    // animation scale zero) → the camera reaches its final transform in ONE frame and a STATIC
    // ring renders on the node (alpha > 0, zero animation clips — nothing here ever uses
    // clips); motion ON → interpolated pan (>1 frame) to the same transform. The rendered
    // information set is IDENTICAL across the two states (criterion 5): target, framing, ring.
    public sealed class CauseCameraController : MonoBehaviour
    {
        // Review B3: DURATION-bounded (never speed-bounded) — a speed-based pan scales with
        // board size and provably busts the 1500 ms budget beyond 5.25 units. Any distance
        // completes in PAN_DURATION_MS.
        public const double PAN_DURATION_MS = 400.0;

        private UnityEngine.Camera _camera;
        private Vector3 _goal;
        private Vector3 _panFrom;
        private double _panElapsedMs;
        private bool _panning;
        private GameObject _ring;
        private Vector3 _restPose; // review B5: retry returns the camera HERE
        private Quaternion _restRotation;
        private float _restOrthographicSize;
        private Vector3 _boardFacingNormal = Vector3.back;
        private float _ringAlpha;
        private static Mesh _ringMesh, _rimMesh;
        private BoardFx _fx;
        private Volume _volume;
        private VolumeProfile _profile;
        private ColorAdjustments _colour;
        private Vignette _vignette;
        private bool _failing, _previousPostProcessing;
        public System.Func<bool> MotionOffSource;

        public string TargetNodeId { get; private set; } = "";
        public bool IsFramed => !_panning;
        public bool RingVisible => _ring != null && _ring.activeSelf;
        public float RingAlpha => _ring != null ? _ringAlpha : 0f;

        public void Wire(UnityEngine.Camera cam, Vector3 boardFacingNormal)
        {
            _camera = cam;
            CapturePlayPose(boardFacingNormal);
        }

        // LoadLevel re-fits the camera because every authored board has different bounds.
        // Capture that new play pose before Reset so Retry never returns to the prior level.
        public void CapturePlayPose(Vector3 boardFacingNormal)
        {
            if (_camera == null) return;
            _restPose = _camera.transform.position;
            _restRotation = _camera.transform.rotation;
            _restOrthographicSize = _camera.orthographicSize;
            _boardFacingNormal = boardFacingNormal.sqrMagnitude > 0.0001f
                ? boardFacingNormal.normalized : Vector3.back;
        }

        public Vector3 RingWorldPos => _ring != null ? _ring.transform.position : Vector3.zero;
        public Vector3 GoalPosition => _goal;

        public void FrameNode(string nodeId, Vector3 worldPos, bool motionOff)
        {
            TargetNodeId = nodeId ?? "";
            _goal = new Vector3(worldPos.x, worldPos.y, _camera.transform.position.z);
            ShowRing(worldPos);
            if (motionOff)
            {
                // criterion 3: a CUT — final transform this frame, static ring, no clips.
                _camera.transform.position = _goal;
                _panning = false;
            }
            else
            {
                _panFrom = _camera.transform.position;
                _panElapsedMs = 0.0;
                _panning = true; // criterion 4: interpolate across frames, duration-bounded
            }
        }

        public void Reset()
        {
            ClearFailureMood();
            TargetNodeId = "";
            _panning = false;
            if (_ring != null) _ring.SetActive(false);
            // Review B5: the retried run plays on the S-02 framing, never on the fail framing
            // or an interrupted pan position.
            if (_camera != null)
            {
                _camera.transform.position = _restPose;
                _camera.transform.rotation = _restRotation;
                _camera.orthographicSize = _restOrthographicSize;
            }
        }

        private void Update()
        {
            if (!_panning) return;
            _panElapsedMs += Time.deltaTime * 1000.0;
            if (MotionOffSource != null && MotionOffSource()) _panElapsedMs = PAN_DURATION_MS;
            float t = Mathf.Clamp01((float)(_panElapsedMs / PAN_DURATION_MS));
            // smoothstep ease; endpoint exact at t == 1
            float eased = t * t * (3f - 2f * t);
            _camera.transform.position = Vector3.Lerp(_panFrom, _goal, eased);
            if (t >= 1f) _panning = false;
        }

        private void OnDestroy()
        {
            ClearFailureMood();
            if (_volume != null) _volume.sharedProfile = null;
            if (_profile != null)
            {
                foreach (var component in _profile.components) DestroyOwned(component);
                DestroyOwned(_profile);
            }
            // The ring is world-rooted so it never rides the camera, but it is still owned by
            // this controller. Release it with GameRoot instead of leaking an inactive marker
            // and renderer material across level-test fixtures or scene teardown.
            DestroyOwned(_ring);
            _ring = null;
        }

        private void ShowRing(Vector3 worldPos)
        {
            if (_ring == null)
            {
                _ring = new GameObject("CauseRing");
                var filter = _ring.AddComponent<MeshFilter>();
                if (_ringMesh == null) _ringMesh = Annulus(.61f, .70f);
                if (_rimMesh == null) _rimMesh = Annulus(.70f, .74f);
                filter.sharedMesh = _ringMesh;
                var renderer = _ring.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Board.GreyboxMaterial.Shared;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                // Review B1: NEVER parented to the camera — the controller lives on the camera
                // object, so a camera-parented ring rides the cut/pan and ends 3.5 units off
                // the causal node. World-positioned, unparented: it stays ON the node.
                var color = Palette.TicketOrange;
                var properties = new MaterialPropertyBlock();
                properties.SetColor("_BaseColor", color);
                properties.SetColor("_Color", color);
                renderer.SetPropertyBlock(properties);
                _ringAlpha = color.a;
                var rim = new GameObject("Cream rim", typeof(MeshFilter), typeof(MeshRenderer));
                rim.transform.SetParent(_ring.transform, false);
                rim.GetComponent<MeshFilter>().sharedMesh = _rimMesh;
                var rimRenderer = rim.GetComponent<MeshRenderer>();
                rimRenderer.sharedMaterial = Board.GreyboxMaterial.Shared;
                rimRenderer.shadowCastingMode = ShadowCastingMode.Off;
                rimRenderer.receiveShadows = false;
                properties.SetColor("_BaseColor", Palette.CreamCard);
                properties.SetColor("_Color", Palette.CreamCard);
                rimRenderer.SetPropertyBlock(properties);
            }
            _ring.transform.rotation = Quaternion.FromToRotation(Vector3.up, _boardFacingNormal);
            // Keep the ring screen-centred on the node (the failure-review information law)
            // while pulling it toward this axis-aligned camera to avoid depth fighting. Its
            // normal still follows the board, so the marker reads as part of the diorama.
            _ring.transform.position = worldPos - _camera.transform.forward * 0.6f;
            _ring.SetActive(true);
        }

        // This profile belongs to the current camera and is enabled only during failure.
        // Normal Home/gameplay keeps its previous post-processing setting and colour grade.
        public void ShowFailureMood(bool motionOff)
        {
            if (_failing) return;
            _failing = true;
            var data = _camera.GetUniversalAdditionalCameraData();
            _previousPostProcessing = data.renderPostProcessing;
            if (_profile == null)
            {
                _profile = ScriptableObject.CreateInstance<VolumeProfile>();
                _profile.name = "Failure mood";
                _colour = _profile.Add<ColorAdjustments>();
                _colour.saturation.overrideState = true;
                _vignette = _profile.Add<Vignette>();
                _vignette.intensity.overrideState = true;
                _vignette.color.Override(Palette.DepotNavy);
                _vignette.smoothness.Override(.55f);
                _volume = gameObject.AddComponent<Volume>();
                _volume.isGlobal = true;
                _volume.priority = 100f;
                _volume.sharedProfile = _profile;
            }
            data.renderPostProcessing = true;
            _volume.weight = 1f;
            _fx = BoardFx.GetOrCreate(transform, () => MotionOffSource != null && MotionOffSource());
            _fx.Tween(this, motionOff ? 0f : .35f, p =>
            {
                if (!_failing) return;
                float eased = Mathf.SmoothStep(0f, 1f, p);
                _colour.saturation.value = Mathf.Lerp(0f, -35f, eased);
                _vignette.intensity.value = Mathf.Lerp(.25f, .45f, eased);
            });
        }

        private void ClearFailureMood()
        {
            if (!_failing) return;
            _failing = false;
            _fx?.Finish(this);
            if (_volume != null) _volume.weight = 0f;
            if (_colour != null) _colour.saturation.value = 0f;
            if (_vignette != null) _vignette.intensity.value = .25f;
            if (_camera != null)
                _camera.GetUniversalAdditionalCameraData().renderPostProcessing = _previousPostProcessing;
        }

        private static Mesh Annulus(float inner, float outer)
        {
            const int segments = 64;
            var vertices = new Vector3[segments * 2];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                int a = i * 2, b = (i + 1) % segments * 2, t = i * 6;
                vertices[a] = direction * inner; vertices[a + 1] = direction * outer;
                normals[a] = normals[a + 1] = Vector3.up;
                triangles[t] = a; triangles[t + 1] = b + 1; triangles[t + 2] = a + 1;
                triangles[t + 3] = a; triangles[t + 4] = b; triangles[t + 5] = b + 1;
            }
            var mesh = new Mesh { name = "Open cause ring", vertices = vertices,
                normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void DestroyOwned(Object value)
        {
            if (value == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
