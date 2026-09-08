using UnityEngine;
using CatMetro.Presentation.Theme;

namespace CatMetro.Presentation.Hud
{
    // The input owner retains the full thumb band. This view paints the shared 60dp
    // retry pin and adds no input region of its own.
    public sealed class RetryCtaView : MonoBehaviour
    {
        private RectTransform _rect;
        private ChromeChip _chip;
        private Rect _paintedPx;
        private Rect _lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
        private float _lastDpi = -1f;

        public bool IsVisible => gameObject.activeSelf;
        public Rect PaintedRectPx => _paintedPx;
        public Rect FaceRectPx => ChromeChip.PrimaryFaceRect(_paintedPx, _lastDpi);
        public string RenderedText => _chip != null ? _chip.Label.text : "";

        public static RetryCtaView Create(Transform canvasParent)
        {
            var go = new GameObject("RetryCta", typeof(RectTransform));
            go.transform.SetParent(canvasParent, false);
            var view = go.AddComponent<RetryCtaView>();
            view._rect = (RectTransform)go.transform;
            view._chip = ChromeChip.PaintPrimary(go.transform, HudBands.ThumbBand(Screen.safeArea),
                Strings.UiStrings.Get("retry.cta"), Palette.MetroTeal);
            go.SetActive(false);
            return view;
        }

        public void SetVisible(bool visible)
        {
            if (visible && (Screen.safeArea != _lastSafeArea || Screen.dpi != _lastDpi))
                LayoutForViewport(Screen.safeArea, Screen.dpi);
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        public void LayoutForViewport(Rect safeArea, float dpi)
        {
            _lastSafeArea = safeArea;
            _lastDpi = dpi;
            _paintedPx = HudBands.ThumbBand(safeArea);
            _rect.anchorMin = _rect.anchorMax = Vector2.zero;
            _rect.pivot = Vector2.zero;
            _rect.anchoredPosition = _paintedPx.position;
            _rect.sizeDelta = _paintedPx.size;
            _chip.Layout(new Rect(Vector2.zero, _paintedPx.size), dpi);
        }
    }
}
