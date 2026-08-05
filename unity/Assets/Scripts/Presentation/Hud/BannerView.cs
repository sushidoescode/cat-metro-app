using UnityEngine;

namespace CatMetro.Presentation.Hud
{
    // Greybox banner: a world-space TextMesh carrying ONLY csv-resolved strings (criterion 4's
    // zero-literals rule — callers pass the KEY). ADR-0007's UGUI+TMP chrome arrives with
    // CM-C3's screens; greybox renders the text primitively and exposes it for assertion.
    public sealed class BannerView : MonoBehaviour
    {
        private TextMesh _mesh;

        public string CurrentKey { get; private set; } = "";
        public string CurrentText => _mesh != null ? _mesh.text : "";
        public bool Visible => _mesh != null && gameObject.activeSelf && _mesh.text.Length > 0;

        public static BannerView Create(Transform parent)
        {
            var go = new GameObject("Banner");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(3f, 11f, -1f);
            var view = go.AddComponent<BannerView>();
            view._mesh = go.AddComponent<TextMesh>();
            view._mesh.characterSize = 0.5f;
            view._mesh.anchor = TextAnchor.MiddleCenter;
            view._mesh.text = "";
            return view;
        }

        public void ShowKey(string key)
        {
            CurrentKey = key;
            _mesh.text = Strings.UiStrings.Get(key);
        }

        // CM-C3 criterion 10: the LOCKED fail strings carry a {node}/{station} token — the
        // component still receives only the KEY plus the substitution pair, never a literal.
        public void ShowKeySubstituted(string key, string token, string value)
        {
            CurrentKey = key;
            _mesh.text = Strings.UiStrings.Get(key).Replace(token, value ?? "?");
        }

        public void Hide()
        {
            CurrentKey = "";
            _mesh.text = "";
        }
    }
}
