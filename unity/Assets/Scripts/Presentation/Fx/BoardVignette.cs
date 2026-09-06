using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CatMetro.Presentation.Fx
{
    // The board normally renders without post-processing. Blend the brief's .25 -> .42
    // pulse into that idle look, then restore the camera's original rendering path.
    public sealed class BoardVignette : MonoBehaviour
    {
        private BoardFx _fx;
        private Camera _camera;
        private UniversalAdditionalCameraData _cameraData;
        private Volume _volume;
        private VolumeProfile _profile;
        private Vignette _vignette;
        private bool _pulsing, _restingPost;

        public void Bind(BoardFx fx, Camera camera)
        {
            _fx?.Finish(this);
            Restore();
            _fx = fx;
            _camera = camera;
        }

        public void Pulse()
        {
            if (_camera == null || _fx == null || _fx.MotionOff || !_fx.isActiveAndEnabled) return;
            _fx.Finish(this);
            if (_volume == null)
            {
                var host = new GameObject("Rejected arrival vignette");
                host.transform.SetParent(transform, false);
                _volume = host.AddComponent<Volume>();
                _volume.isGlobal = true;
                _volume.priority = 1000f;
                _volume.weight = 0f;
                _profile = ScriptableObject.CreateInstance<VolumeProfile>();
                _profile.name = "Board rejection (runtime)";
                _vignette = _profile.Add<Vignette>(true);
                _vignette.color.value = Color.black;
                _vignette.smoothness.value = 0.3f;
                _volume.sharedProfile = _profile;
            }
            _cameraData = _camera.GetUniversalAdditionalCameraData();
            _restingPost = _cameraData.renderPostProcessing;
            _cameraData.renderPostProcessing = true;
            _pulsing = true;
            _volume.enabled = true;
            _fx.Tween(this, 0.4f, p =>
            {
                if (p >= 1f) { Restore(); return; }
                _vignette.intensity.value = Mathf.Lerp(0.25f, 0.42f, Mathf.Sin(p * Mathf.PI));
                _volume.weight = Mathf.Min(Mathf.SmoothStep(0f, 1f, p / 0.2f),
                    Mathf.SmoothStep(0f, 1f, (1f - p) / 0.2f));
            });
        }

        private void Restore()
        {
            if (_volume != null) { _volume.weight = 0f; _volume.enabled = false; }
            if (_pulsing && _cameraData != null) _cameraData.renderPostProcessing = _restingPost;
            _pulsing = false;
        }

        private void OnDisable() { _fx?.Finish(this); Restore(); }
        private void OnDestroy()
        {
            Restore();
            if (_vignette != null) Destroy(_vignette);
            if (_profile != null) Destroy(_profile);
        }
    }
}
