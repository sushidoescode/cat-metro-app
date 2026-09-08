using UnityEngine;
using UnityEngine.UI;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Theme;

namespace CatMetro.Presentation.Hud
{
    // Paint and input have separate bounds: the cream pin is 60dp, while the existing
    // full thumb band remains the single registered CTA. The title lives on Banner's canvas.
    public sealed class ResultsPanel : MonoBehaviour
    {
        public const string RegionId = "results.next";
        public const int RegionPriority = ChromeRegions.ModalPriority;
        public const float PaintDelaySeconds = .6f;
        public const float EaseSeconds = .35f;
        public System.Action NextRequested;

        private System.Func<string> _screenState;
        private System.Func<bool> _motionOff;
        private ChromeRegions _regions;
        private Canvas _canvas;
        private GameObject _panelRoot;
        private Image _scrim;
        private ChromeChip _chip;
        private Transform _footer;
        private bool _registered;
        private float _shownAt;
        private Rect _chipPaintedPx;
        private Rect _lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
        private float _lastDpi = -1f;

        public bool IsVisible => _panelRoot != null && _panelRoot.activeInHierarchy;
        public GameObject PanelRoot => _canvas != null ? _canvas.gameObject : null;
        public Transform FooterRoot => _footer;
        // Historical read-back retained: this is the full registered band.
        public Rect ChipPaintedRectPx => _chipPaintedPx;
        public Rect ChipFaceRectPx => _chip != null ? _chip.FaceRectPx : default;
        public string CtaText => _chip != null ? _chip.Label.text : "";
        public float PaintedAlpha => _chip != null ? _chip.Label.color.a : 0f;
        public static Rect ChipRect(Rect safeArea) => HudBands.ThumbBand(safeArea);

        public void Attach(System.Func<string> screenState, ChromeRegions regions,
            System.Func<bool> motionOff = null)
        {
            _screenState = screenState;
            _regions = regions;
            if (motionOff != null) _motionOff = motionOff;
            EnsureViews();
            Apply();
        }

        public void SetCtaTextKey(string key)
        {
            EnsureViews();
            _chip.SetLabel(Strings.UiStrings.Get(key));
            _chip.SetRingColour(key == "results.daily.done" ? Palette.InkNavy : Palette.TicketOrange);
        }

        private void Update()
        {
            if (_screenState != null) Apply();
        }

        private void EnsureViews()
        {
            if (_canvas != null) return;
            var go = new GameObject("ResultsCanvas");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            var cam = GetComponentInChildren<Camera>();
            _canvas.renderMode = cam != null ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
            _canvas.worldCamera = cam;
            _canvas.planeDistance = 1f;
            _canvas.sortingOrder = 110;

            _panelRoot = new GameObject("ResultsRoot", typeof(RectTransform));
            _panelRoot.transform.SetParent(go.transform, false);
            var rootRect = (RectTransform)_panelRoot.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            _scrim = _panelRoot.AddComponent<Image>();
            _scrim.material = UiChromeMaterial.Shared;
            _scrim.raycastTarget = false;
            _chip = ChromeChip.PaintPrimary(_panelRoot.transform, ChipRect(Screen.safeArea),
                Strings.UiStrings.Get("results.next"), Palette.TicketOrange);

            var footerGo = new GameObject("ResultsFooter", typeof(RectTransform));
            footerGo.transform.SetParent(_panelRoot.transform, false);
            _footer = footerGo.transform;
            _panelRoot.SetActive(false);
        }

        private void Apply()
        {
            bool show = _screenState() == "Won";
            if (show && !_panelRoot.activeSelf) _shownAt = Time.unscaledTime;
            if (_panelRoot.activeSelf != show) _panelRoot.SetActive(show);
            if (show)
            {
                if (Screen.safeArea != _lastSafeArea || Screen.dpi != _lastDpi)
                    LayoutForViewport(Screen.safeArea, Screen.dpi);
                SamplePresentation(Time.unscaledTime - _shownAt);
            }
            if (show && !_registered && _regions != null)
            {
                _regions.Register(RegionId, () => _chipPaintedPx, InvokeNext, RegionPriority);
                _registered = true;
            }
            else if (!show) Unregister();
        }

        // The runtime and phone capture rig sample the same Image/TMP animation; no
        // CanvasGroup or animation component is added beneath the one-TMP panel tree.
        public void SamplePresentation(float elapsedSeconds)
        {
            float alpha = _motionOff != null && _motionOff() ? 1f
                : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsedSeconds - PaintDelaySeconds) / EaseSeconds));
            _scrim.color = Palette.WithAlpha(Palette.DepotNavy, .48f * alpha);
            _chip.SetOpacity(alpha);
            _chip.Root.localScale = Vector3.one * Mathf.Lerp(.92f, 1f, alpha);
        }

        public void LayoutForViewport(Rect safeArea, float dpi)
        {
            _lastSafeArea = safeArea;
            _lastDpi = dpi;
            _chipPaintedPx = ChipRect(safeArea);
            _chip.Layout(_chipPaintedPx, dpi);
        }

        private void InvokeNext() => NextRequested?.Invoke();

        private void Unregister()
        {
            if (!_registered || _regions == null) return;
            _regions.Unregister(RegionId);
            _registered = false;
        }

        private void OnDisable() => Unregister();
        private void OnDestroy() => Unregister();
    }
}
