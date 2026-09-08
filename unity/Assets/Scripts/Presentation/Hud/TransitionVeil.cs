using System;
using UnityEngine;
using UnityEngine.UI;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Theme;

namespace CatMetro.Presentation.Hud
{
    // ScreenChromeController owns the clock and lifetime. Rebuilds run only after the
    // cream image becomes opaque; an input cover survives until the outgoing fade ends.
    public sealed class TransitionVeil
    {
        public const float CoverSeconds = .22f;
        public const float RevealSeconds = .28f;
        private const string RegionId = "transition.cover";
        private readonly GameObject _root;
        private readonly Image _image;
        private ChromeRegions _regions;
        private Action _pending;
        private bool _revealing;
        private float _elapsed;
        public bool IsInFlight { get; private set; }
        public float Alpha => _image != null ? _image.color.a : 0f;
        public GameObject Root => _root;

        private TransitionVeil(GameObject root, Image image, ChromeRegions regions)
        {
            _root = root;
            _image = image;
            _regions = regions;
            Cancel();
        }

        public static TransitionVeil Create(Transform owner, Camera camera, ChromeRegions regions)
        {
            var root = new GameObject("TransitionCanvas", typeof(Canvas));
            root.transform.SetParent(owner, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = camera != null ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 130;
            var paint = new GameObject("CreamVeil", typeof(RectTransform), typeof(Image));
            paint.transform.SetParent(root.transform, false);
            var rect = (RectTransform)paint.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = paint.GetComponent<Image>();
            image.material = UiChromeMaterial.Shared;
            image.raycastTarget = false;
            return new TransitionVeil(root, image, regions);
        }

        public void Bind(ChromeRegions regions)
        {
            if (_regions == regions) return;
            Cancel();
            _regions = regions;
        }

        public bool Begin(Action load, bool motionOff)
        {
            if (IsInFlight || load == null) return false;
            _pending = load;
            _elapsed = 0f;
            _revealing = false;
            IsInFlight = true;
            _root.SetActive(true);
            Paint(0f);
            _regions?.Register(RegionId, () => new Rect(0, 0, Screen.width, Screen.height),
                () => { }, ChromeRegions.StackedModalPriority + 1, ChromeFeedback.None);
            if (motionOff) FinishImmediately();
            return true;
        }

        public void Advance(float seconds, bool motionOff)
        {
            if (!IsInFlight) return;
            if (motionOff) { FinishImmediately(); return; }
            if (!float.IsNaN(seconds) && !float.IsInfinity(seconds)) _elapsed += Mathf.Max(0f, seconds);
            if (!_revealing)
            {
                Paint(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_elapsed / CoverSeconds)));
                if (_elapsed < CoverSeconds) return;
                Paint(1f);
                InvokePending();
                _revealing = true;
                _elapsed = 0f;
                // Preserve an opaque rendered frame even after a long frame/hitch.
                return;
            }
            Paint(1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_elapsed / RevealSeconds)));
            if (_elapsed >= RevealSeconds) Cancel();
        }

        private void InvokePending()
        {
            var load = _pending;
            _pending = null;
            try { load?.Invoke(); }
            catch { Cancel(); throw; }
        }

        private void FinishImmediately()
        {
            Paint(1f);
            try { InvokePending(); }
            finally { Cancel(); }
        }

        public void Cancel()
        {
            _pending = null;
            IsInFlight = false;
            _regions?.Unregister(RegionId);
            Paint(0f);
            if (_root != null) _root.SetActive(false);
        }

        private void Paint(float alpha)
        {
            if (_image != null) _image.color = Palette.WithAlpha(Palette.CreamCard, alpha);
        }
    }
}
