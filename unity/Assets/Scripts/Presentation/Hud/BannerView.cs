using UnityEngine;
using TMPro;

namespace CatMetro.Presentation.Hud
{
    // Outcome copy is screen UI, rather than board geometry: a world-space TextMesh scales its
    // available width with camera aspect and clips on a portrait phone. The transparent canvas
    // stays below the CTA/results canvases (100/110) while its TMP label fits the safe viewport.
    public sealed class BannerView : MonoBehaviour
    {
        private const float HorizontalInsetFraction = 0.08f;

        private Canvas _canvas;
        private RectTransform _rect;
        private TMP_Text _text;
        private Rect _paintedPx;
        private Rect _lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
        private float _lastDpi = -1f;

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
            if (cam != null)
            {
                view._canvas.renderMode = RenderMode.ScreenSpaceCamera;
                view._canvas.worldCamera = cam;
                view._canvas.planeDistance = 1f;
            }
            else
            {
                view._canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            view._canvas.sortingOrder = 90;

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            view._rect = textGo.AddComponent<RectTransform>();
            view._text = textGo.AddComponent<TextMeshProUGUI>();
            view._text.text = "";
            view._text.alignment = TextAlignmentOptions.Center;
            view._text.enableWordWrapping = true;
            view._text.enableAutoSizing = true;
            view._text.fontSizeMin = 22f;
            view._text.fontSizeMax = 50f;
            view._text.color = Color.white;
            view.Layout(Screen.safeArea, Screen.dpi);
            return view;
        }

        private void Update()
        {
            Layout(Screen.safeArea, Screen.dpi);
        }

        public void ShowKey(string key)
        {
            CurrentKey = key;
            _text.text = Strings.UiStrings.Get(key);
            Layout(Screen.safeArea, Screen.dpi);
        }

        // CM-C3 criterion 10: the LOCKED fail strings carry a {node}/{station} token — the
        // component still receives only the KEY plus the substitution pair, never a literal.
        public void ShowKeySubstituted(string key, string token, string value)
        {
            CurrentKey = key;
            _text.text = Strings.UiStrings.Get(key).Replace(token, value ?? "?");
            Layout(Screen.safeArea, Screen.dpi);
        }

        public void Hide()
        {
            CurrentKey = "";
            _text.text = "";
        }

        // Public for the phone-capture rig: the test injects its 917x2048 safe area before it
        // renders a capture, rather than trusting the editor's convenient landscape Game view.
        public void LayoutForViewport(Rect safeArea, float dpi)
        {
            float inset = Mathf.Max(safeArea.width * HorizontalInsetFraction,
                16f * HudBands.PxPerDp(dpi));
            float width = Mathf.Max(0f, safeArea.width - inset * 2f);
            var statusBand = HudBands.StatusBand(safeArea);
            _paintedPx = new Rect(safeArea.x + inset, statusBand.y, width, statusBand.height);

            _rect.anchorMin = Vector2.zero;
            _rect.anchorMax = Vector2.zero;
            _rect.pivot = Vector2.zero;
            _rect.anchoredPosition = new Vector2(_paintedPx.x, _paintedPx.y);
            _rect.sizeDelta = new Vector2(_paintedPx.width, _paintedPx.height);
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
