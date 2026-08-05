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
        private const float PAN_SPEED = 3.5f; // world units/s toward the target frame

        private UnityEngine.Camera _camera;
        private Vector3 _goal;
        private bool _panning;
        private GameObject _ring;

        public string TargetNodeId { get; private set; } = "";
        public bool IsFramed => !_panning;
        public bool RingVisible => _ring != null && _ring.activeSelf;
        public float RingAlpha => _ring != null
            ? _ring.GetComponent<Renderer>().material.color.a : 0f;

        public void Wire(UnityEngine.Camera cam) => _camera = cam;

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
                _panning = true; // criterion 4: interpolate across frames
            }
        }

        public void Reset()
        {
            TargetNodeId = "";
            _panning = false;
            if (_ring != null) _ring.SetActive(false);
        }

        private void Update()
        {
            if (!_panning) return;
            var p = _camera.transform.position;
            var next = Vector3.MoveTowards(p, _goal, PAN_SPEED * Time.deltaTime);
            _camera.transform.position = next;
            if ((next - _goal).sqrMagnitude < 0.0001f) _panning = false;
        }

        private void ShowRing(Vector3 worldPos)
        {
            if (_ring == null)
            {
                _ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                _ring.name = "CauseRing";
                _ring.transform.SetParent(transform, false);
                _ring.transform.localScale = new Vector3(1.4f, 0.02f, 1.4f);
                _ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                _ring.GetComponent<Renderer>().material.color = new Color(1f, 0.35f, 0.1f, 0.85f);
            }
            _ring.transform.position = worldPos + new Vector3(0f, 0f, -0.6f);
            _ring.SetActive(true);
        }
    }
}
