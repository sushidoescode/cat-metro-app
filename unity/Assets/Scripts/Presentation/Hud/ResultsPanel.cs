using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Audio;

namespace CatMetro.Presentation.Hud
{
    // CM-UX-04: the Won-state results panel — the win dead-end's component-level closure.
    // Contract: state/handoffs/CM-UX-04-frozen-contract.md. ATTACHED: GameRoot.Wire composes
    // this panel unconditionally (CM-LOADNEXT, state/handoffs/CM-LOADNEXT-frozen-contract.md,
    // criterion 1) — the NEEDS-WIRING hold CM-UX-07 kept per Q-3 ("a rendered LOCKED Next that
    // does nothing is a worse dead-end than today's banner") is DISCHARGED; Next now has a
    // real advance target.
    // Standalone by design (A-UX4-1): riding ScreenChromeController's Won row would have
    // attached the panel the moment CM-UX-07 attached the controller, before LoadNext existed
    // — the standalone construction stays even now that the hold is discharged.
    // Render-only chrome (P-1/P-2): the CTA is painted here and HIT elsewhere — the one
    // registered chrome region routes the tap through TapInput; NextRequested is a seam ONLY
    // (level advance is Bootstrap-owned). Exactly one primary CTA (CM-R19.3) and a
    // structurally-empty footer (TG-4): together the §5 monetization tripwire — any second
    // target or footer child turns the count==1 test red.
    public sealed class ResultsPanel : MonoBehaviour
    {
        public const string RegionId = "results.next";
        // CM-LOADNEXT D-1: sourced from the shared priority ladder (ChromeRegions) — value
        // unchanged (10), still explicit per A-UX1-3, never order-reliant.
        public const int RegionPriority = ChromeRegions.ModalPriority;

        // The seam: invoked when the registry routes a tap to the CTA region. Null-safe no-op
        // with no subscriber; the panel itself never advances anything.
        public System.Action NextRequested;

        private System.Func<string> _screenState;
        private ChromeRegions _regions;
        private Canvas _canvas;
        private GameObject _panelRoot;
        private RectTransform _chipRect;
        private TMP_Text _ctaText;
        private Transform _footer;
        private UiAudioManager _audio;
        private bool _wasShown;
        private bool _registered;
        private Rect _chipPaintedPx;
        // R1-L7 lesson: re-layout only when the safe area actually changes (frame-rate policy).
        private Rect _lastSafeArea = new Rect(-1f, -1f, -1f, -1f);

        // #39 review F6: activeInHierarchy, not activeSelf — a deactivated host must never
        // report a visible panel to a future wiring test.
        public bool IsVisible => _panelRoot != null && _panelRoot.activeInHierarchy;
        public GameObject PanelRoot => _canvas != null ? _canvas.gameObject : null;
        public Transform FooterRoot => _footer;
        public Rect ChipPaintedRectPx => _chipPaintedPx;
        public string CtaText => _ctaText != null ? _ctaText.text : "";

        // The chip-rect LAW, pure and injectable (EditMode drives this; the runtime path
        // binds Screen.safeArea into the same function): the one primary CTA is full-width
        // in the safe-area thumb band. During Won the retry band predicate is false
        // (GameRoot.cs:127 — FailureReview-only), so the registered rect (== this painted
        // rect) IS the tappable rect: no divergence zone on this surface.
        public static Rect ChipRect(Rect safeArea) => HudBands.ThumbBand(safeArea);

        public void Attach(System.Func<string> screenState, ChromeRegions regions)
        {
            _screenState = screenState;
            _regions = regions;
            EnsureViews();
            Apply();
        }

        private void Update()
        {
            if (_screenState != null) Apply();
        }

        private void EnsureViews()
        {
            if (_canvas != null) return;
            var go = new GameObject("ResultsCanvas");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            // ScreenSpaceCamera when the wired camera exists (renders into capture RTs — the
            // #33 visual-verification rule needs real frames); overlay only as a fallback.
            var cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceCamera;
                _canvas.worldCamera = cam;
                _canvas.planeDistance = 1f;
            }
            else
            {
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            _canvas.sortingOrder = 110; // above CM-UX-02's chrome canvas (100)

            var mat = UiChromeMaterial.Shared; // the F-DEV-2 lesson: never a strippable default

            _panelRoot = new GameObject("ResultsRoot");
            _panelRoot.transform.SetParent(go.transform, false);
            var rootRect = _panelRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            var scrim = _panelRoot.AddComponent<Image>();
            if (mat != null) scrim.material = mat;
            // Light staging scrim: dims the finished board — the world-space win banner is
            // dimmed right along with it, NOT legible above it (L-4 comment-truth fix, #39
            // post-merge audit): this canvas's plane sits at distance 1 from Cam while the
            // banner (and the rest of the board) sits much farther out, so the scrim draws in
            // front of/over it. Measured directly, not asserted: sampled sky/board/marker
            // pixels in evals/results/ux/cm-ux-04/cm-ux-04-results.png read ~25% darker than
            // the same coordinates in the undimmed CM-UX-02/03 baselines (e.g. srgb(91,107,119)
            // vs srgb(68,81,92)); the banner's own win-csv text glyphs read visibly dimmed too. Any
            // rearrangement to keep the banner legible above this scrim is TG-4's call, not
            // this slice's — the shape+text pair on the CTA still carries state, never color
            // alone.
            var scrimColor = CatMetroUiTheme.DepotNavy;
            scrimColor.a = 0.62f;
            CatMetroUiTheme.StyleImage(scrim, scrimColor, rounded: false);

            var card = CatMetroUiTheme.MakeImage(_panelRoot.transform, "CompletionCard",
                new Vector2(0.09f, 0.34f), new Vector2(0.91f, 0.72f),
                CatMetroUiTheme.WarmPaper);
            CatMetroUiTheme.MakeImage(card.transform, "RouteMotif",
                new Vector2(0.13f, 0.46f), new Vector2(0.87f, 0.54f),
                CatMetroUiTheme.MetroTeal);
            CatMetroUiTheme.MakeImage(card.transform, "RouteDotLeft",
                new Vector2(0.10f, 0.37f), new Vector2(0.24f, 0.63f),
                CatMetroUiTheme.TicketOrange);
            CatMetroUiTheme.MakeImage(card.transform, "RouteDotRight",
                new Vector2(0.76f, 0.37f), new Vector2(0.90f, 0.63f),
                CatMetroUiTheme.MetroTeal);
            var cat = CatMetroUiTheme.MakeImage(card.transform, "CompletionCat",
                new Vector2(0.42f, 0.29f), new Vector2(0.58f, 0.71f),
                CatMetroUiTheme.InkNavy).rectTransform;
            var earLeft = CatMetroUiTheme.MakeSymbol(cat, "CompletionEarLeft",
                new Vector2(-0.02f, 0.76f), new Vector2(0.48f, 1.34f),
                CatMetroUiTheme.InkNavy, "SymbolTriangle").rectTransform;
            earLeft.localRotation = Quaternion.Euler(0f, 0f, 18f);
            var earRight = CatMetroUiTheme.MakeSymbol(cat, "CompletionEarRight",
                new Vector2(0.52f, 0.76f), new Vector2(1.02f, 1.34f),
                CatMetroUiTheme.InkNavy, "SymbolTriangle").rectTransform;
            CatMetroUiTheme.MakeImage(_panelRoot.transform, "ConfettiTeal",
                new Vector2(0.035f, 0.51f), new Vector2(0.065f, 0.58f),
                CatMetroUiTheme.MetroTeal, rounded: false).rectTransform.localRotation =
                Quaternion.Euler(0f, 0f, 24f);
            CatMetroUiTheme.MakeImage(_panelRoot.transform, "ConfettiOrange",
                new Vector2(0.935f, 0.56f), new Vector2(0.965f, 0.63f),
                CatMetroUiTheme.TicketOrange, rounded: false).rectTransform.localRotation =
                Quaternion.Euler(0f, 0f, -20f);

            // Exactly ONE primary CTA (CM-R19.3) — painted here, hit through the region.
            var chipGo = new GameObject("PrimaryCta");
            chipGo.transform.SetParent(_panelRoot.transform, false);
            _chipRect = chipGo.AddComponent<RectTransform>();
            var chipBg = chipGo.AddComponent<Image>();
            CatMetroUiTheme.StyleImage(chipBg, CatMetroUiTheme.TicketOrange);

            var labelGo = new GameObject("CtaLabel");
            labelGo.transform.SetParent(chipGo.transform, false);
            var lrect = labelGo.AddComponent<RectTransform>();
            lrect.anchorMin = Vector2.zero;
            lrect.anchorMax = Vector2.one;
            lrect.offsetMin = Vector2.zero;
            lrect.offsetMax = Vector2.zero;
            _ctaText = labelGo.AddComponent<TextMeshProUGUI>();
            _ctaText.text = Strings.UiStrings.Get("results.next"); // key-only, never a literal
            _ctaText.alignment = TextAlignmentOptions.Center;
            CatMetroUiTheme.StyleText(_ctaText, CatMetroTextRole.Cta);

            // TG-4's structurally-empty footer: it EXISTS so its emptiness is assertable,
            // and it stays childless — the count==1 test is the §5 monetization tripwire.
            var footerGo = new GameObject("ResultsFooter");
            footerGo.transform.SetParent(_panelRoot.transform, false);
            var frect = footerGo.AddComponent<RectTransform>();
            frect.anchorMin = new Vector2(0f, 0.28f);
            frect.anchorMax = new Vector2(1f, 0.38f);
            frect.offsetMin = Vector2.zero;
            frect.offsetMax = Vector2.zero;
            _footer = footerGo.transform;
            _audio = UiAudioManager.Ensure(transform);

            _panelRoot.SetActive(false);
        }

        private void Apply()
        {
            bool show = _screenState() == "Won";
            // F-6 (round-1 review): explicit null check, not `?.` — see ScreenChromeController
            // for the Unity fake-null rationale.
            if (show && !_wasShown && _audio != null) _audio.PlayWin();
            if (show) LayoutChip();
            if (_panelRoot.activeSelf != show) _panelRoot.SetActive(show);
            if (show && !_registered && _regions != null)
            {
                // The rect provider tracks the live layout; the registration lives only
                // while the panel is shown (idempotent across state flips).
                _regions.Register(RegionId, () => _chipPaintedPx, InvokeNext, RegionPriority);
                _registered = true;
            }
            else if (!show && _registered)
            {
                _regions.Unregister(RegionId);
                _registered = false;
            }
            _wasShown = show;
        }

        private void InvokeNext()
        {
            // Seam ONLY (criterion 5): Bootstrap owns level advance. Nothing else happens.
            // F-6 (round-1 review): explicit null check, not `?.` — see ScreenChromeController
            // for the Unity fake-null rationale.
            if (_audio != null) _audio.PlayTap();
            NextRequested?.Invoke();
        }

        private void LayoutChip()
        {
            if (Screen.safeArea == _lastSafeArea) return;
            _lastSafeArea = Screen.safeArea;
            var band = ChipRect(Screen.safeArea); // the live binding site (A-UX2-4 pattern)
            _chipPaintedPx = band;
            _chipRect.anchorMin = Vector2.zero;
            _chipRect.anchorMax = Vector2.zero;
            _chipRect.pivot = Vector2.zero;
            _chipRect.anchoredPosition = new Vector2(band.x, band.y);
            _chipRect.sizeDelta = new Vector2(band.width, band.height);
        }

        private void OnDestroy()
        {
            // CM-UX-01 R1-F3 law: registrations must not outlive their owner — a live rect
            // provider over a destroyed view throws on the next tap otherwise.
            if (_registered && _regions != null)
            {
                _regions.Unregister(RegionId);
                _registered = false;
            }
        }

        // CM-UX-07 W-3 (R2-3, audit M-3; written while the panel was still UNATTACHED per
        // Q-3 — CM-LOADNEXT has since attached it, so this law now applies to the real Wire
        // path too, not only direct-construction tests): mirrors OnDestroy — a
        // deactivated-but-not-destroyed host must drop the registration too, or a live rect
        // provider survives over a host that stopped posting frames.
        private void OnDisable()
        {
            if (_registered && _regions != null)
            {
                _regions.Unregister(RegionId);
                _registered = false;
            }
        }
    }
}
