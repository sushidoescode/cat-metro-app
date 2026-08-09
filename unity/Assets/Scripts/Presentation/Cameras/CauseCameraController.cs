using UnityEngine;

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

        public string TargetNodeId { get; private set; } = "";
        public bool IsFramed => !_panning;
        public bool RingVisible => _ring != null && _ring.activeSelf;
        public float RingAlpha => _ring != null
            ? _ring.GetComponent<Renderer>().material.color.a : 0f;

        public void Wire(UnityEngine.Camera cam)
        {
            _camera = cam;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Board.DioramaPalette.WarmPaper;
            _restPose = cam.transform.position; // the S-02 play framing (review B5)
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
            TargetNodeId = "";
            _panning = false;
            if (_ring != null) _ring.SetActive(false);
            // Review B5: the retried run plays on the S-02 framing, never on the fail framing
            // or an interrupted pan position.
            if (_camera != null) _camera.transform.position = _restPose;
        }

        private void Update()
        {
            if (!_panning) return;
            _panElapsedMs += Time.deltaTime * 1000.0;
            float t = Mathf.Clamp01((float)(_panElapsedMs / PAN_DURATION_MS));
            // smoothstep ease; endpoint exact at t == 1
            float eased = t * t * (3f - 2f * t);
            _camera.transform.position = Vector3.Lerp(_panFrom, _goal, eased);
            if (t >= 1f) _panning = false;
        }

        private void ShowRing(Vector3 worldPos)
        {
            if (_ring == null)
            {
                _ring = new GameObject("CauseRing");
                Board.DioramaMeshFactory.Attach(_ring, Board.DioramaMeshKind.Cylinder,
                    Board.DioramaPalette.Material("cause-ring", Board.DioramaPalette.TicketOrange));
                _ring.name = "CauseRing";
                // Review B1: NEVER parented to the camera — the controller lives on the camera
                // object, so a camera-parented ring rides the cut/pan and ends 3.5 units off
                // the causal node. World-positioned, unparented: it stays ON the node.
                _ring.transform.localScale = new Vector3(1.4f, 0.02f, 1.4f);
                _ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var color = Board.DioramaPalette.TicketOrange;
                color.a = 0.85f;
                _ring.GetComponent<Renderer>().material.color = color;
            }
            _ring.transform.position = worldPos + new Vector3(0f, 0f, -0.6f);
            _ring.SetActive(true);
        }
    }
}
