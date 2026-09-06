using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Hud.WavePreview;
using CatMetro.Presentation.Theme;

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
        // started from the sheet once CM-UX-07 attached these views (latent, caught pre-wiring).
        // Explicit per A-UX1-3; parents register at ChromeRegions.ParentPriority.
        // CM-LOADNEXT D-1: sourced from ChromeRegions.StackedModalPriority (12), ONE tier above
        // ResultsPanel's ChromeRegions.ModalPriority (10) — this sheet is hosted on the
        // ScreenStack's ScreensCanvas, which paints ABOVE ResultsPanel's canvas (sortingOrder
        // 120 vs 110, GameRoot.cs); the tap law now matches the paint law, so co-registration
        // with ResultsPanel resolves to whichever is visually on top, by priority, never by
        // which one happened to register first (state/handoffs/CM-UX-07-delta-audit.md D-1).
        private const int PlayRegionPriority = ChromeRegions.StackedModalPriority;

        public System.Action PlayRequested;

        private ChromeRegions _regions;
        private bool _registered;
        private bool _shown; // #46 review F4: Show()-left-shown intent, survives OnDisable/OnEnable
        private TMP_Text _name;
        private TMP_Text _goal;
        private TMP_Text _teaching;
        private TMP_Text _playLabel;
        private RectTransform _chip;
        private ChromeChip _playPaint;
        private Image _ticket;
        private Image _ticketFace;
        private Image _shadow;
        private Rect _chipRectPx;
        private Rect _lastSafeArea;
        private float _lastDpi = -1f;

        public string NameText => _name != null ? _name.text : "";
        public string GoalText => _goal != null ? _goal.text : "";
        public string TeachingText => _teaching != null ? _teaching.text : "";
        public string PlayText => _playLabel != null ? _playLabel.text : "";
        public Rect PlayChipRectPx => _chipRectPx;
        public Rect PlayFaceRectPx => ChromeChip.PrimaryFaceRect(_chipRectPx, _lastDpi);
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

            MakePanel(go.transform, "FullScreenShade", Vector2.zero, Vector2.one,
                Palette.WithAlpha(Palette.DepotNavy, .48f));
            view._shadow = MakePanel(go.transform, "TicketShadow", Vector2.zero, Vector2.zero,
                Palette.WithAlpha(Palette.DepotNavy, .24f), HudShapeSprites.SoftRoundedHalo);
            view._ticket = MakePanel(go.transform, "CreamTicket", Vector2.zero, Vector2.zero,
                Palette.TicketOrange, HudShapeSprites.RoundedSquare);
            view._ticketFace = MakePanel(view._ticket.transform, "Paper", Vector2.zero, Vector2.one,
                Palette.CreamCard, HudShapeSprites.RoundedSquare);
            view._name = MakeText(view._ticket.transform, "LevelName",
                new Vector2(.08f, .69f), new Vector2(.92f, .94f), 32f);
            view._goal = MakeText(view._ticket.transform, "GoalLine",
                new Vector2(.08f, .48f), new Vector2(.92f, .66f), 20f);
            view._teaching = MakeText(view._ticket.transform, "TeachingLine",
                new Vector2(.10f, .10f), new Vector2(.90f, .42f), 16f);

            // The Play chip: px-laid to the thumb band at Show (live Screen reads there only).
            var chipGo = new GameObject("PlayChip");
            chipGo.transform.SetParent(go.transform, false);
            view._chip = chipGo.AddComponent<RectTransform>();
            view._playPaint = ChromeChip.PaintPrimary(chipGo.transform,
                HudBands.ThumbBand(Screen.safeArea), Strings.UiStrings.Get("intro.play"),
                Palette.TicketOrange);
            view._playLabel = view._playPaint.Label;

            view.LayoutForViewport(Screen.safeArea, Screen.dpi);
            go.SetActive(false);
            return view;
        }

        private static Image MakePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color, Sprite sprite = null)
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
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            return img;
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
            tmp.color = Palette.InkNavy;
            tmp.raycastTarget = false;
            tmp.enableAutoSizing = true;
            return tmp;
        }

        public void Attach(ChromeRegions regions)
        {
            _regions = regions;
        }

        public void Show(string levelName, int deliveries) => Show(levelName, deliveries, null);

        public void Show(string levelName, int deliveries, string teachingGoal)
        {
            _name.text = levelName; // injected level data, not UI copy
            _goal.text = Strings.UiStrings.Get(deliveries == 1 ? "intro.goal.one" : "intro.goal")
                .Replace("{count}", deliveries.ToString());
            _teaching.text = teachingGoal ?? "";
            _teaching.gameObject.SetActive(!string.IsNullOrWhiteSpace(teachingGoal));
            _shown = true;
            gameObject.SetActive(true);
            LayoutChip();
            RegisterChip();
        }

        public void Hide()
        {
            _shown = false;
            UnregisterChip();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            UnregisterChip(); // R1-F3 lifetime law
        }

        // CM-UX-07 W-2 (R2-3, audit M-3): mirrors OnDestroy — a deactivated-but-not-destroyed
        // sheet (SetActive(false) directly on the host, not through Hide()) must drop its
        // registration too, or a live rect provider survives over a host that stopped posting
        // frames.
        private void OnDisable()
        {
            UnregisterChip();
        }

        // #46 review F4: mirrors OnDisable — a host reactivated directly (SetActive(true), not
        // through Show()) must re-register, or a visible Play chip sits inert over an
        // unstartable game (the ghost-affordance asymmetry). Gated on _shown, not on _regions
        // alone: Create()'s AddComponent fires OnEnable transiently (the GameObject starts
        // active by default before Create() parks it inactive) BEFORE Attach()/Show() ever run,
        // and a composed-but-never-shown component must register nothing — boot semantics stay
        // unchanged. Hide() clears _shown, so a bare re-activation after Hide() also registers
        // nothing (Hide()'s "not shown" intent survives OnEnable too).
        private void OnEnable()
        {
            if (_shown) RegisterChip();
        }

        private void RegisterChip()
        {
            if (_regions != null && !_registered)
            {
                _regions.Register(PlayRegionId, () => _chipRectPx,
                    () => PlayRequested?.Invoke(), PlayRegionPriority);
                _regions.BindVisual(PlayRegionId, _chip);
                _registered = true;
            }
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
            LayoutForViewport(Screen.safeArea, Screen.dpi);
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea || Screen.dpi != _lastDpi) LayoutChip();
        }

        public void LayoutForViewport(Rect safeArea, float dpi)
        {
            _lastSafeArea = safeArea;
            _lastDpi = dpi;
            float scale = HudBands.PxPerDp(dpi);
            float width = Mathf.Min(320f * scale, Mathf.Max(0f, safeArea.width - 40f * scale));
            float height = Mathf.Min(230f * scale, safeArea.height * .42f);
            var ticket = new Rect(safeArea.center.x - width * .5f,
                safeArea.center.y - height * .5f + 24f * scale, width, height);
            Place(_ticket.rectTransform, ticket);
            Place(_shadow.rectTransform, new Rect(ticket.x - 6f * scale,
                ticket.y - 10f * scale, ticket.width + 12f * scale, ticket.height + 12f * scale));
            _ticket.pixelsPerUnitMultiplier = _ticketFace.pixelsPerUnitMultiplier = .9f / scale;
            _shadow.pixelsPerUnitMultiplier = 1.4f / scale;
            _ticketFace.rectTransform.offsetMin = Vector2.one * (2f * scale);
            _ticketFace.rectTransform.offsetMax = -Vector2.one * (2f * scale);
            SizeText(_name, 32f, 24f, scale);
            SizeText(_goal, 20f, 16f, scale, body: true);
            SizeText(_teaching, 16f, 12f, scale, body: true);
            _chipRectPx = HudBands.ThumbBand(safeArea);
            Place(_chip, _chipRectPx);
            _playPaint.Layout(new Rect(Vector2.zero, _chipRectPx.size), dpi);
        }

        private static void SizeText(TMP_Text text, float sizeDp, float minimumDp, float scale,
            bool body = false)
        {
            TypeScale.Apply(text, sizeDp, scale * HudBands.FallbackDpi, body, minimumDp);
        }

        private static void Place(RectTransform rect, Rect pixels)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = pixels.position;
            rect.sizeDelta = pixels.size;
        }
    }
}
