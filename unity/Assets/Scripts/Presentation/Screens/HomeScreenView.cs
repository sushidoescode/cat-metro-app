using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Audio;

namespace CatMetro.Presentation.Screens
{
    // CM-UX-06 criteria 3/4/5/6: the branded paper session-1 Home. One pulsing L001 pin (72dp,
    // thumb-band-centered via pure HomeLayout math; live Screen reads only here, the
    // RetryCtaView precedent), parked-district silhouettes, csv-keyed title — and NOTHING
    // else: no shop, no daily, no badges (S-01 layout intent; the tree test is the
    // monetization tripwire), and neither TG-3 variant of the bonus-district tile exists.
    // Render-only by law (P-1): the pin's hit routes through the INJECTED ChromeRegions
    // registry — this view is the tranche's first registrar and honors the R1-F3 lifetime
    // law (unregister on Hide AND OnDestroy). Motion posture (P-5, A11Y-S01-2): the pulse is
    // easing and vanishes under the injected motion-off delegate; the raised-ring shape twin
    // renders in BOTH modes — motion never carries the information.
    public sealed class HomeScreenView : MonoBehaviour
    {
        private const string PinRegionId = "home.pin.l001";
        private const int PinRegionPriority = 0; // explicit per A-UX1-3

        public System.Action LevelSelected;

        private ChromeRegions _regions;
        private System.Func<bool> _motionOff;
        private bool _registered;
        private bool _shown; // #46 review F4: Show()-left-shown intent, survives OnDisable/OnEnable
        private TMP_Text _title;
        private RectTransform _pin;
        private RectTransform _ring;
        private Rect _pinRectPx;
        private float _phase;

        public Rect PinPaintedRectPx => _pinRectPx;
        // #42 review F1: the world-corners read-back seam — tests measure the REAL transform
        // against the painted claim instead of comparing the claim to itself.
        public RectTransform PinTransform => _pin;
        public bool RingVisible => _ring != null && _ring.gameObject.activeInHierarchy;
        public float PinScale => _pin != null ? _pin.localScale.x : 1f;
        public bool IsVisible => gameObject.activeSelf;
        public string TitleText => _title != null ? _title.text : "";

        public static HomeScreenView Create(Transform canvasParent)
        {
            var go = new GameObject("HomeScreen");
            go.transform.SetParent(canvasParent, false);
            var view = go.AddComponent<HomeScreenView>();
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            CatMetroUiTheme.MakeImage(go.transform, "HomePaper",
                Vector2.zero, Vector2.one, CatMetroUiTheme.WarmPaper);

            var brand = new GameObject("BrandMark");
            CatMetroUiTheme.Stretch(brand, go.transform,
                new Vector2(0.07f, 0.83f), new Vector2(0.24f, 0.97f));
            CatMetroUiTheme.MakeImage(brand.transform, "CatHead",
                new Vector2(0.21f, 0.20f), new Vector2(0.79f, 0.76f),
                CatMetroUiTheme.InkNavy);
            var leftEar = CatMetroUiTheme.MakeSymbol(brand.transform, "LeftEar",
                new Vector2(0.20f, 0.62f), new Vector2(0.46f, 0.91f),
                CatMetroUiTheme.InkNavy, "SymbolTriangle").rectTransform;
            leftEar.localRotation = Quaternion.Euler(0f, 0f, 18f);
            var rightEar = CatMetroUiTheme.MakeSymbol(brand.transform, "RightEar",
                new Vector2(0.54f, 0.62f), new Vector2(0.80f, 0.91f),
                CatMetroUiTheme.InkNavy, "SymbolTriangle").rectTransform;
            rightEar.localRotation = Quaternion.Euler(0f, 0f, -18f);
            CatMetroUiTheme.MakeImage(brand.transform, "RailLineTeal",
                new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.15f),
                CatMetroUiTheme.MetroTeal);
            CatMetroUiTheme.MakeImage(brand.transform, "RailDotOrange",
                new Vector2(0.69f, 0.00f), new Vector2(0.85f, 0.20f),
                CatMetroUiTheme.TicketOrange);

            view._title = CatMetroUiTheme.MakeText(go.transform, "Title",
                new Vector2(0.23f, 0.83f), new Vector2(0.93f, 0.97f),
                Strings.UiStrings.Get("home.title"), CatMetroTextRole.Display);

            // Parked-district silhouettes: scenery, not buttons (S-01 — curiosity, no locks).
            MakeSilhouette(go.transform, "ParkedDistrictA",
                new Vector2(0.07f, 0.56f), new Vector2(0.45f, 0.74f), 0);
            MakeSilhouette(go.transform, "ParkedDistrictB",
                new Vector2(0.55f, 0.58f), new Vector2(0.93f, 0.78f), 1);
            MakeSilhouette(go.transform, "ParkedDistrictC",
                new Vector2(0.13f, 0.31f), new Vector2(0.67f, 0.51f), 2);

            // The raised-ring shape twin sits BEHIND the pin (sibling order = draw order).
            view._ring = MakeChip(go.transform, "PinRingL001",
                CatMetroUiTheme.WarmPaper);
            view._pin = MakeChip(go.transform, "PinL001",
                CatMetroUiTheme.TicketOrange);
            CatMetroUiTheme.MakeImage(view._pin, "PinCore",
                new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.78f),
                CatMetroUiTheme.InkNavy);

            go.SetActive(false);
            return view;
        }

        private static void MakeSilhouette(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, int variant)
        {
            var go = new GameObject(name);
            CatMetroUiTheme.Stretch(go, parent, anchorMin, anchorMax);
            var baseColor = CatMetroUiTheme.DepotNavy;
            baseColor.a = 0.14f;
            CatMetroUiTheme.StyleImage(go.AddComponent<Image>(), baseColor);

            Color ink = CatMetroUiTheme.InkNavy;
            ink.a = 0.42f;
            Color teal = CatMetroUiTheme.MetroTeal;
            teal.a = 0.34f;
            CatMetroUiTheme.MakeImage(go.transform, "DepotBody",
                new Vector2(0.12f, 0.16f), new Vector2(0.74f, 0.60f), ink);
            var roof = CatMetroUiTheme.MakeImage(go.transform, "DepotRoof",
                new Vector2(0.18f, 0.56f), new Vector2(0.70f, 0.84f), ink).rectTransform;
            roof.localRotation = Quaternion.Euler(0f, 0f, variant == 1 ? -8f : 7f);
            CatMetroUiTheme.MakeImage(go.transform, "RailBed",
                new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.15f), teal);
            CatMetroUiTheme.MakeImage(go.transform, "SignalPost",
                new Vector2(0.79f, 0.22f), new Vector2(0.86f, 0.78f), ink);
            if (variant >= 1)
            {
                CatMetroUiTheme.MakeImage(go.transform, "WaterTower",
                    new Vector2(0.72f, 0.48f), new Vector2(0.94f, 0.82f), teal);
            }
            if (variant >= 2)
            {
                CatMetroUiTheme.MakeImage(go.transform, "ShedAnnex",
                    new Vector2(0.58f, 0.20f), new Vector2(0.88f, 0.48f), ink);
            }
        }

        private static RectTransform MakeChip(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            CatMetroUiTheme.StyleImage(go.AddComponent<Image>(), color);
            return rect;
        }

        public void Attach(ChromeRegions regions, System.Func<bool> motionOff)
        {
            _regions = regions;
            _motionOff = motionOff; // GameRoot.MotionOff binding is CM-UX-07's (P-3)
        }

        public void Show()
        {
            _shown = true;
            gameObject.SetActive(true);
            LayoutPin();
            RegisterPin();
        }

        public void Hide()
        {
            _shown = false;
            UnregisterPin();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            UnregisterPin(); // R1-F3 lifetime law
        }

        // CM-UX-07 W-1 (R2-3, audit M-3): mirrors OnDestroy — a deactivated-but-not-destroyed
        // Home (SetActive(false) directly on the host, not through Hide()) must drop its
        // registration too, or a live rect provider survives over a host that stopped posting
        // frames.
        private void OnDisable()
        {
            UnregisterPin();
        }

        // #46 review F4: mirrors OnDisable — a host reactivated directly (SetActive(true), not
        // through Show()) must re-register, or a visible-but-pulsing pin sits inert over an
        // unstartable game (the ghost-affordance asymmetry). Gated on _shown, not on _regions
        // alone: Create()'s AddComponent fires OnEnable transiently (the GameObject starts
        // active by default before Create() parks it inactive) BEFORE Attach()/Show() ever run,
        // and a composed-but-never-shown component must register nothing — boot semantics stay
        // unchanged. Hide() clears _shown, so a bare re-activation after Hide() also registers
        // nothing (Hide()'s "not shown" intent survives OnEnable too).
        private void OnEnable()
        {
            if (_shown) RegisterPin();
        }

        private void RegisterPin()
        {
            if (_regions != null && !_registered)
            {
                _regions.Register(PinRegionId, () => _pinRectPx,
                    InvokeLevelSelected, PinRegionPriority);
                _registered = true;
            }
        }

        private void InvokeLevelSelected()
        {
            UiAudioManager.Ensure(transform)?.PlayTap();
            LevelSelected?.Invoke();
        }

        private void UnregisterPin()
        {
            if (_regions != null && _registered)
            {
                _regions.Unregister(PinRegionId);
                _registered = false;
            }
        }

        // The live binding site (A-UX1-5 law): Screen.safeArea/dpi are read HERE and handed
        // to pure math; px placement via bottom-left anchoring, the RetryCtaView pattern.
        private void LayoutPin()
        {
            var safeArea = Screen.safeArea;
            float dpi = Screen.dpi;
            _pinRectPx = HomeLayout.PinRect(safeArea, dpi);
            ApplyPx(_pin, _pinRectPx);
            ApplyPx(_ring, HomeLayout.RingRect(safeArea, dpi));
        }

        private static void ApplyPx(RectTransform rect, Rect px)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(px.center.x, px.center.y);
            rect.sizeDelta = new Vector2(px.width, px.height);
        }

        // The pulse: code-driven easing only (zero Animator components — the whitelist walk
        // proves it). Motion-off locks the rest pose exactly; the ring twin carries the
        // "available" information in both modes. Time is presentation-only (A-UX6-3) and
        // never enters the sim (P-6).
        private void Update()
        {
            if (_pin == null) return;
            float scale = 1f;
            bool off = _motionOff != null && _motionOff();
            if (!off)
            {
                _phase += Time.unscaledDeltaTime * 5f;
                scale = 1f + 0.08f * Mathf.Sin(_phase);
            }
            _pin.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
