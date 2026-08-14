using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Theme;

namespace CatMetro.Presentation.Screens
{
    // CM-UX-06 criteria 3/4/5/6: the greybox session-1 Home. One pulsing L001 pin (72dp,
    // thumb-band-centered via pure HomeLayout math; live Screen reads only here, the
    // RetryCtaView precedent), parked-district silhouettes, csv-keyed title — and by DEFAULT
    // nothing else: no shop, no badges (S-01 layout intent; HomeScreenTests.cs's tree walk is
    // the monetization tripwire, still exercised via the same zero-argument Create() every
    // existing caller/test uses), and neither TG-3 variant of the bonus-district tile exists.
    // CM-DAILYWIRE correction (discovered mid-implementation, recorded in the frozen contract's
    // FA-4 revision): product_spec §18 gates Daily behind "after L007 win," and S-01's own
    // "no daily entry rendered in session 1" law is the SAME rule from the other direction —
    // this class honors both by never constructing the Daily pin unless the caller explicitly
    // opts in (dailyEntryUnlocked, default false — the CM-UX-07 "zero screen objects
    // constructed" precedent, not merely hidden). GameRoot passes true only when
    // DailyEntryUnlocked is set (currently a dev/test-only seam — see Known debt: the real
    // L007-win check needs the save layer, unwired for any level today).
    // Render-only by law (P-1): the pin's hit routes through the INJECTED ChromeRegions
    // registry — this view is the tranche's first registrar and honors the R1-F3 lifetime
    // law (unregister on Hide AND OnDestroy). Motion posture (P-5, A11Y-S01-2): the pulse is
    // easing and vanishes under the injected motion-off delegate; the raised-ring shape twin
    // renders in BOTH modes — motion never carries the information.
    public sealed class HomeScreenView : MonoBehaviour
    {
        private const string PinRegionId = "home.pin.l001";
        // CM-BOOT-HOME criterion 4 (the priority-debt fix): was ChromeRegions.ParentPriority(0)
        // — outranked ResultsPanel's ModalPriority(10) despite Home's ScreensCanvas painting
        // ABOVE ResultsPanel's canvas (120 vs 110, GameRoot.cs). See ChromeRegions.cs's own
        // comment for the full justification of the new value.
        private const int PinRegionPriority = ChromeRegions.HomeScreenPriority;
        // CM-DAILYWIRE: the Daily entry's own region, registered/unregistered by the exact same
        // RegisterPin/UnregisterPin/OnDisable/OnEnable lifetime law the L001 pin already obeys
        // (a second call site into the same helpers, never a parallel implementation).
        private const string DailyPinRegionId = "home.pin.daily";
        // CM-BOOT-HOME: deliberately left at the OLD ParentPriority(0), NOT raised alongside
        // PinRegionPriority above — Daily is explicitly out of scope for this contract
        // (criterion 5, commerce-free shipped path) and this pin is never constructed at all
        // when dailyUnlocked is false (GameRoot.ComposeScreenFlow), which is unconditionally the
        // case in a shipped build — so the priority-debt fix's live-tap-bug risk never reaches
        // it. Left as a named, flagged follow-up rather than silently widened past this
        // contract's declared scope.
        private const int DailyPinRegionPriority = 0; // explicit per A-UX1-3

        public System.Action LevelSelected;
        public System.Action DailySelected;

        private ChromeRegions _regions;
        private System.Func<bool> _motionOff;
        private bool _registered;
        private bool _dailyRegistered;
        private bool _shown; // #46 review F4: Show()-left-shown intent, survives OnDisable/OnEnable
        private Image _background; // BEAUTIFUL-MENU: the warm-paper tabletop ground
        private TMP_Text _title;
        private RectTransform _pin;
        private RectTransform _ring;
        private RectTransform _dailyPin;
        private TMP_Text _dailyLabel;
        private Rect _pinRectPx;
        private Rect _dailyPinRectPx;
        private float _phase;

        public Rect PinPaintedRectPx => _pinRectPx;
        // #42 review F1: the world-corners read-back seam — tests measure the REAL transform
        // against the painted claim instead of comparing the claim to itself.
        public RectTransform PinTransform => _pin;
        public bool RingVisible => _ring != null && _ring.gameObject.activeInHierarchy;
        public float PinScale => _pin != null ? _pin.localScale.x : 1f;
        public bool IsVisible => gameObject.activeSelf;
        public string TitleText => _title != null ? _title.text : "";
        // BEAUTIFUL-MENU: style read-backs (the TitleText/RingVisible accessor precedent) —
        // tests measure the REAL painted colors against the Palette source of truth.
        public Color BackgroundColor => _background != null ? _background.color : default(Color);
        public Color TitleColor => _title != null ? _title.color : default(Color);
        public Color PinRingColor
        {
            get
            {
                if (_ring == null) return default(Color);
                var img = _ring.GetComponent<Image>();
                return img != null ? img.color : default(Color);
            }
        }
        public Rect DailyPinPaintedRectPx => _dailyPinRectPx;
        public RectTransform DailyPinTransform => _dailyPin;
        public string DailyLabelText => _dailyLabel != null ? _dailyLabel.text : "";

        // dailyEntryUnlocked (CM-DAILYWIRE, default false): every EXISTING caller/test uses the
        // zero-argument form, so HomeScreenTests.cs's S-01 tree walk and exactly-one-region
        // count keep exercising the untouched session-1 tree. Only GameRoot, when
        // DailyEntryUnlocked is explicitly set, passes true.
        public static HomeScreenView Create(Transform canvasParent, bool dailyEntryUnlocked = false)
        {
            var go = new GameObject("HomeScreen");
            go.transform.SetParent(canvasParent, false);
            var view = go.AddComponent<HomeScreenView>();
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // BEAUTIFUL-MENU: the warm-paper tabletop ground (full-bleed, drawn FIRST so it
            // sits behind everything) + an inset cream base board with the shared rounded
            // UiChrome material — the diorama "cardboard edge." Both are plain Images, so the
            // HomeScreenTests render-only whitelist still holds; names avoid every tripwire word.
            view._background = MakeSurface(go.transform, "Background",
                Vector2.zero, Vector2.one, Palette.WarmPaper, rounded: false);
            MakeSurface(go.transform, "BaseBoard",
                new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f), Palette.CreamCard, rounded: true);

            view._title = MakeText(go.transform, "Title",
                new Vector2(0.08f, 0.87f), new Vector2(0.92f, 0.97f),
                Strings.UiStrings.Get("home.title"), 48f, Palette.InkNavy); // key-only, never a literal

            // Parked-district silhouettes: scenery, not buttons (S-01 — curiosity, no locks).
            MakeSilhouette(go.transform, "ParkedDistrictA",
                new Vector2(0.08f, 0.55f), new Vector2(0.46f, 0.72f));
            MakeSilhouette(go.transform, "ParkedDistrictB",
                new Vector2(0.54f, 0.60f), new Vector2(0.92f, 0.78f));
            MakeSilhouette(go.transform, "ParkedDistrictC",
                new Vector2(0.16f, 0.32f), new Vector2(0.62f, 0.48f));

            // The raised-ring shape twin sits BEHIND the pin (sibling order = draw order).
            // BEAUTIFUL-MENU: the ring is the single warm CTA glow (ticket orange); the pin
            // stays ink navy — both from the Palette source, replacing inline literals.
            view._ring = MakeChip(go.transform, "PinRingL001", Palette.TicketOrange);
            view._pin = MakeChip(go.transform, "PinL001", Palette.InkNavy);

            // CM-DAILYWIRE: the Daily entry — a second, static (no pulse) chip beside the L001
            // pin, following the same MakeChip + csv-keyed label pattern as the rest of Home.
            // NEVER constructed when dailyEntryUnlocked is false (S-01 — the CM-UX-07
            // "zero objects constructed" law, not merely inactive/hidden).
            if (dailyEntryUnlocked)
            {
                view._dailyPin = MakeChip(go.transform, "PinDaily",
                    new Color(0.10f, 0.32f, 0.24f, 0.95f));
                view._dailyLabel = MakeText(view._dailyPin.transform, "PinDailyLabel",
                    Vector2.zero, Vector2.one,
                    Strings.UiStrings.Get("home.daily.label"), 22f, Palette.WarmPaper); // key-only, never a literal
            }

            go.SetActive(false);
            return view;
        }

        private static TMP_Text MakeText(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, string text, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = size;
            tmp.color = color;
            return tmp;
        }

        // BEAUTIFUL-MENU: a full-rect surface (background / base board). Plain Image so the
        // whitelist holds; 'rounded' opts into the shared UiChrome material for a soft edge.
        private static Image MakeSurface(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color, bool rounded)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            if (rounded)
            {
                var mat = UiChromeMaterial.Shared;
                if (mat != null) img.material = mat;
            }
            img.color = color;
            return img;
        }

        private static void MakeSilhouette(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
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
            // BEAUTIFUL-MENU: parked scenery in low-alpha depot navy over the cream board
            // (S-01: curiosity, not locks) — was a flat neutral grey.
            img.color = Palette.WithAlpha(Palette.DepotNavy, 0.30f);
        }

        private static RectTransform MakeChip(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            var mat = UiChromeMaterial.Shared;
            if (mat != null) img.material = mat;
            img.color = color;
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
            RegisterDailyPin();
        }

        public void Hide()
        {
            _shown = false;
            UnregisterPin();
            UnregisterDailyPin();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            UnregisterPin(); // R1-F3 lifetime law
            UnregisterDailyPin();
        }

        // CM-UX-07 W-1 (R2-3, audit M-3): mirrors OnDestroy — a deactivated-but-not-destroyed
        // Home (SetActive(false) directly on the host, not through Hide()) must drop its
        // registration too, or a live rect provider survives over a host that stopped posting
        // frames.
        private void OnDisable()
        {
            UnregisterPin();
            UnregisterDailyPin();
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
            if (_shown)
            {
                RegisterPin();
                RegisterDailyPin();
            }
        }

        private void RegisterPin()
        {
            if (_regions != null && !_registered)
            {
                _regions.Register(PinRegionId, () => _pinRectPx,
                    () => LevelSelected?.Invoke(), PinRegionPriority);
                _registered = true;
            }
        }

        private void UnregisterPin()
        {
            if (_regions != null && _registered)
            {
                _regions.Unregister(PinRegionId);
                _registered = false;
            }
        }

        // CM-DAILYWIRE: the Daily pin's own register/unregister pair — same shape as
        // RegisterPin/UnregisterPin above, a second call site into the identical lifetime law
        // (R1-F3), never a parallel implementation of it. Guarded on _dailyPin != null: when
        // Create() ran with dailyEntryUnlocked false, no pin exists to register a hit region
        // for — S-01 forbids even an invisible one.
        private void RegisterDailyPin()
        {
            if (_dailyPin != null && _regions != null && !_dailyRegistered)
            {
                _regions.Register(DailyPinRegionId, () => _dailyPinRectPx,
                    () => DailySelected?.Invoke(), DailyPinRegionPriority);
                _dailyRegistered = true;
            }
        }

        private void UnregisterDailyPin()
        {
            if (_regions != null && _dailyRegistered)
            {
                _regions.Unregister(DailyPinRegionId);
                _dailyRegistered = false;
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
            // Guarded: null when Create() ran with dailyEntryUnlocked false (S-01).
            if (_dailyPin != null)
            {
                _dailyPinRectPx = HomeLayout.DailyPinRect(safeArea, dpi);
                ApplyPx(_dailyPin, _dailyPinRectPx);
            }
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
