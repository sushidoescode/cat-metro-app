using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CatMetro.Presentation.Hud
{
    // CM-UX-02 criterion 3: the LOCKED Try-again chip. Render-only by law (CM-UX-01: inside
    // the retry band during FailureReview the band's own RetryTapped IS the action; registering
    // a region here is dead code). Full-width in HudBands.ThumbBand(safeArea) — the live
    // Screen.safeArea binding lives HERE, injected into the pure math (A-UX1-5/A-UX2-4); the
    // 48dp floor holds on the TAPPABLE rect, painted overhang bounded by the pinned divergence
    // height. Text is TMP; the background binds the Resources-loaded UI material (the
    // GreyboxMaterial F-DEV-2 lesson: never depend on a strippable engine default).
    public sealed class RetryCtaView : MonoBehaviour
    {
        private RectTransform _rect;
        private TMP_Text _text;
        private Rect _paintedPx;

        public bool IsVisible => gameObject.activeSelf;
        public Rect PaintedRectPx => _paintedPx;
        public string RenderedText => _text != null ? _text.text : "";

        public static RetryCtaView Create(Transform canvasParent)
        {
            var go = new GameObject("RetryCta");
            go.transform.SetParent(canvasParent, false);
            var view = go.AddComponent<RetryCtaView>();
            view._rect = go.AddComponent<RectTransform>();

            var bg = go.AddComponent<Image>();
            var mat = UiChromeMaterial.Shared;
            if (mat != null) bg.material = mat;
            bg.color = new Color(0.13f, 0.19f, 0.29f, 0.92f); // ink-navy chip, text carries meaning

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var trect = textGo.AddComponent<RectTransform>();
            trect.anchorMin = Vector2.zero;
            trect.anchorMax = Vector2.one;
            trect.offsetMin = Vector2.zero;
            trect.offsetMax = Vector2.zero;
            view._text = textGo.AddComponent<TextMeshProUGUI>();
            view._text.text = Strings.UiStrings.Get("retry.cta"); // key-only, never a literal
            view._text.alignment = TextAlignmentOptions.Center;
            view._text.fontSize = 40f;
            view._text.color = Color.white;

            go.SetActive(false);
            return view;
        }

        public void SetVisible(bool visible)
        {
            if (visible) LayoutInThumbBand();
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        // The live binding site: Screen.safeArea/dpi are read HERE and handed to pure math.
        // R1-L7: re-layout only when the safe area actually changes — a static screen must not
        // dirty the RectTransform every frame (DEVFIX's frame-rate policy). The cache key is
        // the safe area ALONE: if the band formula ever gains another input, key on it too.
        private Rect _lastSafeArea = new Rect(-1f, -1f, -1f, -1f);

        private void LayoutInThumbBand()
        {
            if (Screen.safeArea == _lastSafeArea) return;
            _lastSafeArea = Screen.safeArea;
            var band = HudBands.ThumbBand(Screen.safeArea);
            _paintedPx = band;
            _rect.anchorMin = Vector2.zero;
            _rect.anchorMax = Vector2.zero;
            _rect.pivot = Vector2.zero;
            _rect.anchoredPosition = new Vector2(band.x, band.y);
            _rect.sizeDelta = new Vector2(band.width, band.height);
        }
    }
}
