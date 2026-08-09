using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CatMetro.Presentation.Hud
{
    // CM-UX-05 criterion 4: the hint chip view — the only sanctioned tutorial text
    // (CM-R13.5; hint.tutorial via UiStrings, key-only, DRAFT copy queued for TG-5).
    // Render-only by law (P-1): no registered region, no input target in v1 — A11Y-S01-4 is
    // honored DIMENSIONALLY (board-edge above the thumb band, exactly 48dp high at the live
    // dpi). Live Screen.safeArea/Screen.dpi reads exist only here, injected into the pure
    // rect law (A-UX1-5); background binds the Resources-loaded UI material (the
    // GreyboxMaterial F-DEV-2 lesson). Its marker and type use the shared chrome vocabulary.
    public sealed class HintChipView : MonoBehaviour
    {
        private RectTransform _rect;
        private TMP_Text _text;
        private Rect _paintedPx;

        public bool IsVisible => gameObject.activeSelf;
        public Rect PaintedRectPx => _paintedPx;
        public string RenderedText => _text != null ? _text.text : "";

        // A11Y-S01-5 HOOKS only: the Unity accessibility-hierarchy build + TalkBack pass is
        // deferred work (UX-OPEN-11) — these carry the metadata a wiring pass will consume.
        public const string LiveRegionPoliteness = "polite";
        public string AccessibilityLabel => RenderedText;

        // The pure placement law: full safe-area width, bottom edge exactly the safe-area
        // thumb band's top (board-edge above the band), height exactly 48dp at the injected
        // dpi. Pure math — no Screen reads in here (the CM-UX-01 injected-inputs law).
        public static Rect ChipRect(Rect safeArea, float dpi)
        {
            float height = HudBands.MinTargetDp * HudBands.PxPerDp(dpi);
            return new Rect(safeArea.x, HudBands.ThumbBand(safeArea).yMax,
                safeArea.width, height);
        }

        public static HintChipView Create(Transform canvasParent)
        {
            var go = new GameObject("HintChip");
            go.transform.SetParent(canvasParent, false);
            var view = go.AddComponent<HintChipView>();
            view._rect = go.AddComponent<RectTransform>();

            var bg = go.AddComponent<Image>();
            CatMetroUiTheme.StyleImage(bg, CatMetroUiTheme.MetroTeal);

            var marker = CatMetroUiTheme.MakeImage(go.transform, "CatEarMarker",
                new Vector2(0.035f, 0.20f), new Vector2(0.115f, 0.76f),
                CatMetroUiTheme.WarmPaper).rectTransform;
            var leftEar = CatMetroUiTheme.MakeSymbol(marker, "MarkerEarLeft",
                new Vector2(-0.02f, 0.76f), new Vector2(0.48f, 1.30f),
                CatMetroUiTheme.WarmPaper, "SymbolTriangle").rectTransform;
            leftEar.localRotation = Quaternion.Euler(0f, 0f, 18f);
            var rightEar = CatMetroUiTheme.MakeSymbol(marker, "MarkerEarRight",
                new Vector2(0.52f, 0.76f), new Vector2(1.02f, 1.30f),
                CatMetroUiTheme.WarmPaper, "SymbolTriangle").rectTransform;

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var trect = textGo.AddComponent<RectTransform>();
            trect.anchorMin = new Vector2(0.13f, 0f);
            trect.anchorMax = new Vector2(0.97f, 1f);
            trect.offsetMin = Vector2.zero;
            trect.offsetMax = Vector2.zero;
            view._text = textGo.AddComponent<TextMeshProUGUI>();
            view._text.text = Strings.UiStrings.Get("hint.tutorial"); // key-only, never a literal
            view._text.alignment = TextAlignmentOptions.Center;
            CatMetroUiTheme.StyleText(view._text, CatMetroTextRole.Body,
                CatMetroUiTheme.WarmPaper);

            go.SetActive(false);
            return view;
        }

        public void SetVisible(bool visible)
        {
            if (visible) Layout();
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        // The live binding site: Screen.safeArea/dpi are read HERE and handed to pure math.
        // R1-L7 (inherited): re-layout only when an INPUT of the rect law changes — the law
        // takes safeArea AND dpi, so the cache keys on both.
        private Rect _lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
        private float _lastDpi = -1f;

        private void Layout()
        {
            if (Screen.safeArea == _lastSafeArea && Screen.dpi == _lastDpi) return;
            _lastSafeArea = Screen.safeArea;
            _lastDpi = Screen.dpi;
            var rect = ChipRect(Screen.safeArea, Screen.dpi);
            _paintedPx = rect;
            _rect.anchorMin = Vector2.zero;
            _rect.anchorMax = Vector2.zero;
            _rect.pivot = Vector2.zero;
            _rect.anchoredPosition = new Vector2(rect.x, rect.y);
            _rect.sizeDelta = new Vector2(rect.width, rect.height);
        }
    }
}
