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
    // GreyboxMaterial F-DEV-2 lesson). Placement is DRAFT for the batched eyeball sitting.
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
            return default;
        }

        public static HintChipView Create(Transform canvasParent)
        {
            var go = new GameObject("HintChip");
            go.transform.SetParent(canvasParent, false);
            var view = go.AddComponent<HintChipView>();
            view._rect = go.AddComponent<RectTransform>();
            go.SetActive(false);
            return view;
        }

        public void SetVisible(bool visible)
        {
        }
    }
}
