using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CatMetro.Presentation.Hud.WavePreview;
using CatMetro.Presentation.Theme;

namespace CatMetro.Presentation.Hud
{
    // Outcome copy is screen UI. Its navy plaque paints above the results shade, while
    // the screen stack and transition veil still cover it during navigation.
    public sealed class BannerView : MonoBehaviour
    {
        private const float HorizontalInsetFraction = .08f;
        private Canvas _canvas;
        private RectTransform _rect;
        private Image _plaque;
        private TMP_Text _text;
        private float _shownAt;
        private Rect _paintedPx;
        private Rect _lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
        private float _lastDpi = -1f;

        public System.Func<bool> MotionOffSource;
        public string CurrentKey { get; private set; } = "";
        public string CurrentText => _text != null ? _text.text : "";
        public bool Visible => _text != null && gameObject.activeSelf && _text.text.Length > 0;
        public Rect PaintedRectPx => _paintedPx;
        public RectTransform TextTransform => _text != null ? _text.rectTransform : null;
        public bool IsTextOverflowing => _text != null && _text.isTextOverflowing;

        public static BannerView Create(Transform parent)
        {
            var go = new GameObject("Banner");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BannerView>();
            view._canvas = go.AddComponent<Canvas>();
            var cam = parent.GetComponentInChildren<Camera>();
            view._canvas.renderMode = cam != null ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
            view._canvas.worldCamera = cam;
            view._canvas.planeDistance = 1f;
            view._canvas.sortingOrder = 115;

            var paint = new GameObject("OutcomePlaque", typeof(RectTransform));
            paint.transform.SetParent(go.transform, false);
            view._rect = (RectTransform)paint.transform;
            var plaque = new GameObject("NavyFace", typeof(RectTransform), typeof(Image));
            plaque.transform.SetParent(paint.transform, false);
            view._plaque = plaque.GetComponent<Image>();
            view._plaque.sprite = HudShapeSprites.RoundedSquare;
            view._plaque.type = Image.Type.Sliced;
            view._plaque.material = UiChromeMaterial.Shared;
            view._plaque.raycastTarget = false;
            view._plaque.rectTransform.anchorMin = Vector2.zero;
            view._plaque.rectTransform.anchorMax = Vector2.one;

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(paint.transform, false);
            var labelRect = (RectTransform)textGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            view._text = textGo.AddComponent<TextMeshProUGUI>();
            view._text.text = "";
            view._text.alignment = TextAlignmentOptions.Center;
            view._text.enableWordWrapping = true;
            view._text.enableAutoSizing = true;
            view._text.raycastTarget = false;
            view.LayoutForViewport(Screen.safeArea, Screen.dpi);
            view.Hide();
            return view;
        }

        private void Update()
        {
            Layout(Screen.safeArea, Screen.dpi);
            if (Visible) SamplePresentation(Time.unscaledTime - _shownAt);
        }

        public void ShowKey(string key) => ShowResolved(key, Strings.UiStrings.Get(key));

        public void ShowKeySubstituted(string key, string token, string value) =>
            ShowResolved(key, Strings.UiStrings.Get(key).Replace(token, value ?? "?"));

        private void ShowResolved(string key, string text)
        {
            CurrentKey = key;
            _text.text = text;
            _shownAt = Time.unscaledTime;
            _rect.gameObject.SetActive(true);
            Layout(Screen.safeArea, Screen.dpi);
            SamplePresentation(0f);
        }

        public void Hide()
        {
            CurrentKey = "";
            _text.text = "";
            _rect.gameObject.SetActive(false);
        }

        public void SamplePresentation(float elapsedSeconds)
        {
            bool instant = CurrentKey != "win.banner" || (MotionOffSource != null && MotionOffSource());
            float eased = instant ? 1f : Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(elapsedSeconds / ResultsPanel.EaseSeconds));
            _text.color = Palette.WithAlpha(Palette.CreamCard, eased);
            _plaque.color = Palette.WithAlpha(Palette.DepotNavy, eased);
            _rect.localScale = Vector3.one * Mathf.Lerp(.92f, 1f, eased);
        }

        public void LayoutForViewport(Rect safeArea, float dpi)
        {
            float scale = HudBands.PxPerDp(dpi);
            float inset = Mathf.Max(safeArea.width * HorizontalInsetFraction, 16f * scale);
            float width = Mathf.Max(0f, safeArea.width - inset * 2f);
            var statusBand = HudBands.StatusBand(safeArea);
            _paintedPx = new Rect(safeArea.x + inset, statusBand.y, width, statusBand.height);
            _rect.anchorMin = _rect.anchorMax = Vector2.zero;
            _rect.pivot = new Vector2(.5f, .5f);
            _rect.anchoredPosition = _paintedPx.center;
            _rect.sizeDelta = _paintedPx.size;
            float verticalInset = Mathf.Max(0f, (_paintedPx.height - 72f * scale) * .5f);
            _plaque.rectTransform.offsetMin = new Vector2(0, verticalInset);
            _plaque.rectTransform.offsetMax = new Vector2(0, -verticalInset);
            _plaque.pixelsPerUnitMultiplier = .9f / scale;
            TypeScale.Apply(_text, TypeScale.Title, dpi, minimumDp: 14f);
            _text.fontSharedMaterial = TypeScale.BoardTitleMaterial;
            _text.margin = new Vector4(16f * scale, verticalInset + 4f * scale,
                16f * scale, verticalInset + 4f * scale);
            _lastSafeArea = safeArea;
            _lastDpi = dpi;
        }

        private void Layout(Rect safeArea, float dpi)
        {
            if (safeArea == _lastSafeArea && dpi == _lastDpi) return;
            LayoutForViewport(safeArea, dpi);
        }
    }
}
