using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatMetro.Presentation.Hud
{
    // Outcome copy remains csv-keyed, while the runtime-built card supplies the visual
    // state: paper + text, with a separate colored keyline as a redundant accent.
    public sealed class BannerView : MonoBehaviour
    {
        private TMP_Text _text;
        private GameObject _card;
        private Image _keyline;

        public string CurrentKey { get; private set; } = "";
        public string CurrentText => _text != null ? _text.text : "";
        public bool Visible => _card != null && _card.activeInHierarchy
            && _text != null && _text.text.Length > 0;

        public static BannerView Create(Transform parent)
        {
            var go = new GameObject("Banner");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BannerView>();
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var canvas = go.AddComponent<Canvas>();
            // F-2: ScreenSpaceCamera placement disagrees with the diorama board camera's
            // off-centre custom projection/view matrices (CauseCameraController), the same
            // defect fixed on WavePreviewStrip's canvas. Overlay renders straight to the
            // device screen so the banner keeps its intended layout regardless of the board
            // camera's framing.
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 115;

            var card = CatMetroUiTheme.MakeImage(go.transform, "BannerCard",
                new Vector2(0.07f, 0.67f), new Vector2(0.93f, 0.82f),
                CatMetroUiTheme.WarmPaper);
            view._card = card.gameObject;
            view._keyline = CatMetroUiTheme.MakeImage(card.transform, "BannerKeyline",
                new Vector2(0.018f, 0.14f), new Vector2(0.045f, 0.86f),
                CatMetroUiTheme.AlarmCoral);
            view._text = CatMetroUiTheme.MakeText(card.transform, "BannerText",
                new Vector2(0.08f, 0.12f), new Vector2(0.96f, 0.88f), "",
                CatMetroTextRole.Body);
            view._text.fontStyle = FontStyles.Bold;
            view._card.SetActive(false);
            return view;
        }

        public void ShowKey(string key)
        {
            CurrentKey = key;
            _text.text = Strings.UiStrings.Get(key);
            ApplyKeyline(key);
            _card.SetActive(true);
        }

        public void ShowKeySubstituted(string key, string token, string value)
        {
            CurrentKey = key;
            _text.text = Strings.UiStrings.Get(key).Replace(token, value ?? "?");
            ApplyKeyline(key);
            _card.SetActive(true);
        }

        public void Hide()
        {
            CurrentKey = "";
            _text.text = "";
            _card.SetActive(false);
        }

        private void ApplyKeyline(string key)
        {
            _keyline.color = key != null && key.StartsWith("win.")
                ? CatMetroUiTheme.MetroTeal
                : CatMetroUiTheme.AlarmCoral;
        }
    }
}
