using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CatMetro.Presentation.Hud
{
    // CM-UX-02 criterion 4: the F-DEV-4 fix — Halted renders a visible, semantics-neutral
    // veil (scrim + halt.notice) instead of nothing. Zero registered regions, zero
    // affordances: the veil is inert by construction; the input block is CM-UX-07's
    // BoardInputActive binding (forward obligation) and the restart escape is CM-UX-07's
    // wiring line (human answer Q-2). Nothing here may decide Q-B/NEW-Q4 semantics.
    // Scrim shape + text: legible in greyscale, never color-alone.
    public sealed class HaltVeilView : MonoBehaviour
    {
        private TMP_Text _text;

        public bool IsVisible => gameObject.activeSelf;
        public string RenderedText => _text != null ? _text.text : "";

        public static HaltVeilView Create(Transform canvasParent)
        {
            var go = new GameObject("HaltVeil");
            go.transform.SetParent(canvasParent, false);
            var view = go.AddComponent<HaltVeilView>();
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var scrim = go.AddComponent<Image>();
            var mat = UiChromeMaterial.Shared;
            if (mat != null) scrim.material = mat;
            scrim.color = new Color(0f, 0f, 0f, 0.65f);

            var textGo = new GameObject("Notice");
            textGo.transform.SetParent(go.transform, false);
            var trect = textGo.AddComponent<RectTransform>();
            trect.anchorMin = new Vector2(0.1f, 0.45f);
            trect.anchorMax = new Vector2(0.9f, 0.6f);
            trect.offsetMin = Vector2.zero;
            trect.offsetMax = Vector2.zero;
            view._text = textGo.AddComponent<TextMeshProUGUI>();
            view._text.text = Strings.UiStrings.Get("halt.notice"); // key-only, never a literal
            view._text.alignment = TextAlignmentOptions.Center;
            view._text.fontSize = 44f;
            view._text.color = Color.white;

            go.SetActive(false);
            return view;
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }
    }
}
