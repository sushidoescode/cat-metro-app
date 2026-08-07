using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;

namespace CatMetro.Presentation.Screens
{
    // CM-UX-06 criteria 6/7: the minimal LevelIntro sheet — the piece that makes the game
    // explain itself before play. Level name + goal line (intro.goal template with {count}
    // substituted — the component receives the KEY plus injected data, never a literal:
    // the BannerView substitution precedent) + an explicit full-width Play CTA in the
    // safe-area thumb band (S-05's spec'd interaction; the §1.1 primary-CTA law).
    // Tap-anywhere dismissal is NOT built — the Play chip is the sheet's ONLY registered
    // region, so a tap outside it does nothing by construction. Render-only (P-1): the hit
    // routes through the injected ChromeRegions; R1-F3 lifetime law honored (unregister on
    // Hide AND OnDestroy). Star thresholds / best score stay deferred (decompose §5).
    public sealed class LevelIntroSheet : MonoBehaviour
    {
        private const string PlayRegionId = "intro.play";
        // #42 review F3, the modal-over-parent law: a sheet shown OVER a screen registers
        // STRICTLY above it — at equal priority the registry's earliest-registration tie-break
        // hands the modal's dead-center tap to the parent's pin — the game could not have been
        // started from the sheet once CM-UX-07 attached these views (latent, caught pre-wiring). Explicit per A-UX1-3; parents register at 0, modals at 10.
        private const int PlayRegionPriority = 10;

        public System.Action PlayRequested;

        private ChromeRegions _regions;
        private bool _registered;
        private TMP_Text _name;
        private TMP_Text _goal;
        private TMP_Text _playLabel;
        private RectTransform _chip;
        private Rect _chipRectPx;

        public string NameText => _name != null ? _name.text : "";
        public string GoalText => _goal != null ? _goal.text : "";
        public string PlayText => _playLabel != null ? _playLabel.text : "";
        public Rect PlayChipRectPx => _chipRectPx;
        public RectTransform ChipTransform => _chip; // #42 F1 read-back seam
        public bool IsVisible => gameObject.activeSelf;

        public static LevelIntroSheet Create(Transform canvasParent)
        {
            var go = new GameObject("LevelIntroSheet");
            go.transform.SetParent(canvasParent, false);
            var view = go.AddComponent<LevelIntroSheet>();
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            MakePanel(go.transform, "SheetPanel",
                new Vector2(0.06f, 0.40f), new Vector2(0.94f, 0.76f),
                new Color(0.13f, 0.19f, 0.29f, 0.92f));
            view._name = MakeText(go.transform, "LevelName",
                new Vector2(0.10f, 0.62f), new Vector2(0.90f, 0.74f), 52f);
            view._goal = MakeText(go.transform, "GoalLine",
                new Vector2(0.10f, 0.44f), new Vector2(0.90f, 0.58f), 36f);

            // The Play chip: px-laid to the thumb band at Show (live Screen reads there only).
            var chipGo = new GameObject("PlayChip");
            chipGo.transform.SetParent(go.transform, false);
            view._chip = chipGo.AddComponent<RectTransform>();
            var bg = chipGo.AddComponent<Image>();
            var mat = UiChromeMaterial.Shared;
            if (mat != null) bg.material = mat;
            bg.color = new Color(0.13f, 0.19f, 0.29f, 0.95f);
            view._playLabel = MakeText(chipGo.transform, "PlayLabel",
                Vector2.zero, Vector2.one, 40f);
            view._playLabel.text = Strings.UiStrings.Get("intro.play"); // key-only

            go.SetActive(false);
            return view;
        }

        private static void MakePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            var mat = UiChromeMaterial.Shared;
            if (mat != null) img.material = mat;
            img.color = color;
        }

        private static TMP_Text MakeText(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = "";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = size;
            tmp.color = Color.white;
            return tmp;
        }

        public void Attach(ChromeRegions regions)
        {
            _regions = regions;
        }

        public void Show(string levelName, int deliveries)
        {
            _name.text = levelName; // injected level data, not UI copy
            _goal.text = Strings.UiStrings.Get("intro.goal")
                .Replace("{count}", deliveries.ToString());
            gameObject.SetActive(true);
            LayoutChip();
            if (_regions != null && !_registered)
            {
                _regions.Register(PlayRegionId, () => _chipRectPx,
                    () => PlayRequested?.Invoke(), PlayRegionPriority);
                _registered = true;
            }
        }

        public void Hide()
        {
            UnregisterChip();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            UnregisterChip(); // R1-F3 lifetime law
        }

        private void UnregisterChip()
        {
            if (_regions != null && _registered)
            {
                _regions.Unregister(PlayRegionId);
                _registered = false;
            }
        }

        // The live binding site (A-UX1-5): Screen.safeArea read HERE, handed to pure math;
        // the chip IS the safe-area thumb band — full-width, bottom-anchored (§1.1).
        private void LayoutChip()
        {
            _chipRectPx = HudBands.ThumbBand(Screen.safeArea);
            _chip.anchorMin = Vector2.zero;
            _chip.anchorMax = Vector2.zero;
            _chip.pivot = Vector2.zero;
            _chip.anchoredPosition = new Vector2(_chipRectPx.x, _chipRectPx.y);
            _chip.sizeDelta = new Vector2(_chipRectPx.width, _chipRectPx.height);
        }
    }
}
