using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CatMetro.Presentation.Cosmetics;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Theme;
using CatMetro.Services;
using CatMetro.Services.Cosmetics;

namespace CatMetro.Presentation.Screens
{
    // LOOK step 7 Home: a framed toy-route/depot stage around three non-interactive cat
    // holders, with one wide, csv-labelled L001 action. The default remains commerce-free;
    // Daily is constructed only when progress/config unlocks it, including a threshold crossed
    // during the current run. Hit regions retain the existing unregister lifecycle, and
    // motion-off keeps the action's static raised-ring cue.
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
        // Daily now ships on Home, so it shares Home's raised priority and cannot lose a tap to
        // a lower painted layer while the screen is visible.
        private const int DailyPinRegionPriority = ChromeRegions.HomeScreenPriority;
        private const string ReminderGearRegionId = "home.reminder.gear";
        private const int ReminderGearRegionPriority = ChromeRegions.HomeScreenPriority;

        public System.Action LevelSelected;
        public System.Action DailySelected;
        public System.Action ReminderAccepted;
        public System.Action ReminderDismissed;
        public System.Action<bool> ReminderEnabledChanged;
        public System.Action<DailyReminderSlot> ReminderSlotChanged;

        private ChromeRegions _regions;
        private System.Func<bool> _motionOff;
        private bool _registered;
        private bool _dailyRegistered;
        private bool _reminderGearRegistered;
        private bool _shown; // #46 review F4: Show()-left-shown intent, survives OnDisable/OnEnable
        private Image _background; // BEAUTIFUL-MENU: the warm-paper tabletop ground
        private TMP_Text _title;
        private RectTransform _heroShadow;
        private RectTransform _hero;
        private Image[] _markers;
        private RectTransform _pin;
        private RectTransform _ring;
        private TMP_Text _primaryLabel;
        private RectTransform _dailyPin;
        private TMP_Text _dailyLabel;
        private TMP_Text _dailyTally;
        private TMP_Text _dailyStatus;
        private RectTransform _reminderGear;
        private DailyReminderSheet _reminderSheet;
        private CosmeticPortraitView _profilePortrait;
        private Rect _pinRectPx;
        private Rect _dailyPinRectPx;
        private Rect _heroRectPx;
        private Rect _reminderGearRectPx;
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
        public string DailyTallyText => _dailyTally != null ? _dailyTally.text : "";
        public string DailyStatusText => _dailyStatus != null ? _dailyStatus.text : "";
        public bool DailyTallyVisible => _dailyTally != null && _dailyTally.gameObject.activeSelf;
        public Rect HeroRectPx => _heroRectPx;
        public string PrimaryLabelText => _primaryLabel != null ? _primaryLabel.text : "";
        public RectTransform ReminderGearTransform => _reminderGear;
        public Rect ReminderGearRectPx => _reminderGearRectPx;
        public DailyReminderSheet ReminderSheet => _reminderSheet;
        public CosmeticPortraitView ProfilePortrait => _profilePortrait;
        public RectTransform ProfilePortraitTransform => _profilePortrait != null
            ? _profilePortrait.RootTransform
            : null;
        public int MarkerCount => _markers != null ? _markers.Length : 0;
        public Color[] MarkerColors
        {
            get
            {
                if (_markers == null) return new Color[0];
                var colors = new Color[_markers.Length];
                for (int i = 0; i < _markers.Length; i++) colors[i] = _markers[i].color;
                return colors;
            }
        }

        // dailyEntryUnlocked (CM-DAILYWIRE, default false): the session-1 tree stays free of
        // Daily objects until the save/config gate opens. GameRoot may also call UnlockDaily
        // after a campaign win crosses that same threshold in the current run.
        public static HomeScreenView Create(Transform canvasParent,
            bool dailyEntryUnlocked = false, int lifetimeDailyCompletions = 0,
            ICosmeticPortraitSource portraitSource = null)
        {
            var go = new GameObject("HomeScreen");
            go.transform.SetParent(canvasParent, false);
            var view = go.AddComponent<HomeScreenView>();
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Warm paper is the desk; the inset cream board and its low navy lip make the
            // screen read as one physical card instead of a collection floating in a void.
            view._background = MakeSurface(go.transform, "Background",
                Vector2.zero, Vector2.one, Palette.WarmPaper, rounded: false);
            MakeSurface(go.transform, "BoardEdge",
                new Vector2(0.034f, 0.034f), new Vector2(0.966f, 0.944f),
                Palette.WithAlpha(Palette.DepotNavy, 0.55f), rounded: true);
            MakeSurface(go.transform, "BaseBoard",
                new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f), Palette.CreamCard, rounded: true);

            view._title = MakeText(go.transform, "Title",
                new Vector2(0.08f, 0.87f), new Vector2(0.92f, 0.97f),
                Strings.UiStrings.Get("home.title"), 48f, Palette.InkNavy); // key-only, never a literal

            // A single depot route-card gives the three existing cat holders somewhere to
            // belong. Every child is render-only geometry; the central rails and sleepers are
            // a miniature promise of the board beneath Home, not extra controls.
            view._heroShadow = MakeChip(go.transform, "HeroShadow",
                Palette.WithAlpha(Palette.DepotNavy, 0.30f));
            view._hero = MakeChip(go.transform, "HeroCard", Palette.DepotNavy);
            MakeSurface(view._hero, "HeroDeck", new Vector2(0.025f, 0.018f),
                new Vector2(0.975f, 0.982f), Palette.CreamCard, rounded: false);
            MakeSurface(view._hero, "RouteAccent", new Vector2(0.025f, 0.91f),
                new Vector2(0.975f, 0.982f), Palette.MetroTeal, rounded: false);

            MakeSurface(view._hero, "RouteBed", new Vector2(0.43f, 0.13f),
                new Vector2(0.57f, 0.89f), Palette.WithAlpha(Palette.DepotNavy, 0.16f), false);
            MakeSurface(view._hero, "RailNorth", new Vector2(0.465f, 0.13f),
                new Vector2(0.482f, 0.89f), Palette.InkNavy, false);
            MakeSurface(view._hero, "RailSouth", new Vector2(0.518f, 0.13f),
                new Vector2(0.535f, 0.89f), Palette.InkNavy, false);
            for (int i = 0; i < 9; i++)
            {
                float y = 0.17f + i * 0.082f;
                MakeSurface(view._hero, "Sleeper" + i.ToString("00"),
                    new Vector2(0.405f, y), new Vector2(0.595f, y + 0.014f),
                    Palette.WarmPaper, false);
            }

            MakeSurface(view._hero, "BranchA", new Vector2(0.34f, 0.725f),
                new Vector2(0.50f, 0.745f), Palette.InkNavy, false);
            MakeSurface(view._hero, "BranchB", new Vector2(0.50f, 0.515f),
                new Vector2(0.66f, 0.535f), Palette.InkNavy, false);
            MakeSurface(view._hero, "BranchC", new Vector2(0.34f, 0.305f),
                new Vector2(0.50f, 0.325f), Palette.InkNavy, false);

            MakeSurface(view._hero, "CatBayA", new Vector2(0.07f, 0.64f),
                new Vector2(0.37f, 0.84f), Palette.WithAlpha(Palette.SignalRed, 0.18f), false);
            MakeSurface(view._hero, "CatBayB", new Vector2(0.63f, 0.43f),
                new Vector2(0.93f, 0.63f), Palette.WithAlpha(Palette.HarborBlue, 0.18f), false);
            MakeSurface(view._hero, "CatBayC", new Vector2(0.07f, 0.22f),
                new Vector2(0.37f, 0.42f), Palette.WithAlpha(Palette.TabbyYellow, 0.22f), false);

            // Exact names and Image types are retained for the cat-art lane: admitted models
            // replace only these fallback paints and continue to use their rects as holders.
            MakeSilhouette(view._hero, "ParkedDistrictA",
                new Vector2(0.11f, 0.665f), new Vector2(0.33f, 0.815f));
            var parkedDistrictB = MakeSilhouette(view._hero, "ParkedDistrictB",
                new Vector2(0.67f, 0.455f), new Vector2(0.89f, 0.605f));
            MakeSilhouette(view._hero, "ParkedDistrictC",
                new Vector2(0.11f, 0.245f), new Vector2(0.33f, 0.395f));
            if (portraitSource != null)
            {
                parkedDistrictB.color = Color.clear;
                view._profilePortrait = CosmeticPortraitView.Create(
                    parkedDistrictB.transform, portraitSource, "HomeProfilePortrait");
            }

            MakeSurface(view._hero, "DepotSpur", new Vector2(0.50f, 0.125f),
                new Vector2(0.68f, 0.145f), Palette.InkNavy, false);
            MakeSurface(view._hero, "DepotPlatform", new Vector2(0.63f, 0.07f),
                new Vector2(0.91f, 0.18f), Palette.DepotNavy, false);
            MakeSurface(view._hero, "DepotDeck", new Vector2(0.66f, 0.095f),
                new Vector2(0.88f, 0.145f), Palette.WarmPaper, false);
            MakeSurface(view._hero, "DepotCanopy", new Vector2(0.66f, 0.145f),
                new Vector2(0.88f, 0.18f), Palette.TicketOrange, false);

            view._markers = new[]
            {
                MakeSurface(view._hero, "RouteMarkerA", new Vector2(0.465f, 0.715f),
                    new Vector2(0.535f, 0.755f), Palette.SignalRed, false),
                MakeSurface(view._hero, "RouteMarkerB", new Vector2(0.465f, 0.505f),
                    new Vector2(0.535f, 0.545f), Palette.HarborBlue, false),
                MakeSurface(view._hero, "RouteMarkerC", new Vector2(0.465f, 0.295f),
                    new Vector2(0.535f, 0.335f), Palette.TabbyYellow, false),
            };

            // The raised-ring shape twin sits BEHIND the pin (sibling order = draw order).
            // BEAUTIFUL-MENU: the ring is the single warm CTA glow (ticket orange); the pin
            // stays ink navy — both from the Palette source, replacing inline literals.
            view._ring = MakeChip(go.transform, "PinRingL001", Palette.TicketOrange);
            view._pin = MakeChip(go.transform, "PinL001", Palette.InkNavy);
            view._primaryLabel = MakeText(view._pin, "PlayLabel", Vector2.zero, Vector2.one,
                Strings.UiStrings.Get("intro.play"), 42f, Palette.WarmPaper);
            view._primaryLabel.enableAutoSizing = true;
            view._primaryLabel.fontSizeMin = 24f;
            view._primaryLabel.fontSizeMax = 42f;
            view._primaryLabel.fontStyle = FontStyles.Bold;

            if (dailyEntryUnlocked) view.UnlockDaily(lifetimeDailyCompletions);

            go.SetActive(false);
            return view;
        }

        // Opens the same save/config-gated surface both at boot and when the threshold is
        // crossed during play. Idempotent so a duplicate outcome observation cannot stack
        // render objects or input regions.
        public void UnlockDaily(int lifetimeDailyCompletions)
        {
            if (_dailyPin == null)
            {
                _dailyPin = MakeChip(transform, "PinDaily",
                    new Color(0.10f, 0.32f, 0.24f, 0.95f));
                _dailyLabel = MakeText(_dailyPin.transform, "PinDailyLabel",
                    new Vector2(0.05f, 0.57f), new Vector2(0.95f, 0.96f),
                    Strings.UiStrings.Get("home.daily.label"), 18f, Palette.WarmPaper); // key-only, never a literal
                _dailyLabel.enableAutoSizing = true;
                _dailyLabel.fontSizeMin = 10f;
                _dailyLabel.fontSizeMax = 18f;
                _dailyLabel.fontStyle = FontStyles.Bold;
                _dailyTally = MakeText(_dailyPin.transform, "LifetimeTally",
                    new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.57f),
                    "", 12f, Palette.WarmPaper);
                _dailyTally.enableAutoSizing = true;
                _dailyTally.fontSizeMin = 7f;
                _dailyTally.fontSizeMax = 12f;
                _dailyStatus = MakeText(_dailyPin.transform, "DailyStatus",
                    new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.57f),
                    "", 11f, Palette.WarmPaper);
                _dailyStatus.enableAutoSizing = true;
                _dailyStatus.fontSizeMin = 7f;
                _dailyStatus.fontSizeMax = 11f;
                _dailyStatus.gameObject.SetActive(false);
            }

            SetDailyLifetimeCompletions(lifetimeDailyCompletions);
            LayoutForViewport(Screen.safeArea, Screen.dpi);
            if (_shown && isActiveAndEnabled) RegisterDailyPin();
        }

        public void SetDailyLifetimeCompletions(int count)
        {
            if (_dailyTally == null) return;
            int safeCount = Mathf.Max(0, count);
            _dailyTally.text = Strings.UiStrings.Get("home.daily.tally")
                .Replace("{count}", safeCount.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
        }

        // Cache fallback work and failures stay on Home. Surface that state in the existing
        // Daily chip so the tap never appears dead; null/empty clears it and restores the tally.
        public void SetDailyStatusKey(string key)
        {
            if (_dailyStatus == null || _dailyTally == null) return;
            bool hasStatus = !string.IsNullOrEmpty(key);
            _dailyStatus.text = hasStatus ? Strings.UiStrings.Get(key) : "";
            _dailyStatus.gameObject.SetActive(hasStatus);
            _dailyTally.gameObject.SetActive(!hasStatus);
        }

        public void ConfigureReminder(bool configurationUnlocked, bool enabled,
            DailyReminderSlot slot, MessagingPermission permission,
            bool canRequestPermission, bool providerAvailable)
        {
            if (!configurationUnlocked) return;
            EnsureReminderViews();
            _reminderSheet.Configure(enabled, slot, permission,
                canRequestPermission, providerAvailable);
            LayoutForViewport(Screen.safeArea, Screen.dpi);
            if (_shown && isActiveAndEnabled) RegisterReminderGear();
        }

        public void ShowReminderPrompt()
        {
            if (_reminderSheet == null || !_shown || !isActiveAndEnabled) return;
            _reminderSheet.ShowPrompt();
        }

        public void ShowReminderSettings()
        {
            if (_reminderSheet == null || !_shown || !isActiveAndEnabled) return;
            _reminderSheet.ShowSettings();
        }

        private void EnsureReminderViews()
        {
            if (_reminderSheet != null) return;

            _reminderGear = MakeChip(transform, "ReminderGear", Palette.CreamCard);
            MakeSurface(_reminderGear, "GearHub", new Vector2(0.24f, 0.24f),
                new Vector2(0.76f, 0.76f), Palette.InkNavy, true);
            MakeSurface(_reminderGear, "GearHole", new Vector2(0.42f, 0.42f),
                new Vector2(0.58f, 0.58f), Palette.WarmPaper, true);
            for (int i = 0; i < 4; i++)
            {
                var tooth = MakeSurface(_reminderGear, "GearTooth" + i,
                    new Vector2(0.14f, 0.44f), new Vector2(0.86f, 0.56f),
                    Palette.InkNavy, false);
                tooth.rectTransform.localEulerAngles = new Vector3(0f, 0f, i * 45f);
            }

            _reminderSheet = DailyReminderSheet.Create(transform);
            _reminderSheet.Attach(_regions);
            _reminderSheet.Accepted = () => ReminderAccepted?.Invoke();
            _reminderSheet.Dismissed = () => ReminderDismissed?.Invoke();
            _reminderSheet.EnabledChanged = value => ReminderEnabledChanged?.Invoke(value);
            _reminderSheet.SlotChanged = value => ReminderSlotChanged?.Invoke(value);
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
            tmp.raycastTarget = false;
            return tmp;
        }

        // Plain Images keep the Home tree render-only. The shared material is currently the
        // project's explicit UI/Default material; the flag means "use shared chrome paint."
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
            img.raycastTarget = false;
            return img;
        }

        private static Image MakeSilhouette(Transform parent, string name,
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
            img.color = Palette.WithAlpha(Palette.DepotNavy, 0.18f);
            img.raycastTarget = false;
            return img;
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
            img.raycastTarget = false;
            return rect;
        }

        public void Attach(ChromeRegions regions, System.Func<bool> motionOff)
        {
            _regions = regions;
            _motionOff = motionOff; // GameRoot.MotionOff binding is CM-UX-07's (P-3)
            if (_reminderSheet != null) _reminderSheet.Attach(regions);
        }

        public void Show()
        {
            _shown = true;
            gameObject.SetActive(true);
            LayoutForViewport(Screen.safeArea, Screen.dpi);
            RegisterPin();
            RegisterDailyPin();
            RegisterReminderGear();
        }

        public void Hide()
        {
            _shown = false;
            if (_reminderSheet != null) _reminderSheet.Hide();
            UnregisterPin();
            UnregisterDailyPin();
            UnregisterReminderGear();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            UnregisterPin(); // R1-F3 lifetime law
            UnregisterDailyPin();
            UnregisterReminderGear();
        }

        // CM-UX-07 W-1 (R2-3, audit M-3): mirrors OnDestroy — a deactivated-but-not-destroyed
        // Home (SetActive(false) directly on the host, not through Hide()) must drop its
        // registration too, or a live rect provider survives over a host that stopped posting
        // frames.
        private void OnDisable()
        {
            UnregisterPin();
            UnregisterDailyPin();
            UnregisterReminderGear();
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
                RegisterReminderGear();
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

        private void RegisterReminderGear()
        {
            if (_reminderGear != null && _regions != null && !_reminderGearRegistered)
            {
                _regions.Register(ReminderGearRegionId, () => _reminderGearRectPx,
                    ShowReminderSettings, ReminderGearRegionPriority);
                _reminderGearRegistered = true;
            }
        }

        private void UnregisterReminderGear()
        {
            if (_regions != null && _reminderGearRegistered)
            {
                _regions.Unregister(ReminderGearRegionId);
                _reminderGearRegistered = false;
            }
        }

        // Pure layout injection keeps the capture and the runtime on the same law. Show() is
        // the only live Screen binding because the shipped orientation is portrait-locked.
        public void LayoutForViewport(Rect safeArea, float dpi)
        {
            bool hasDaily = _dailyPin != null;
            _pinRectPx = HomeLayout.PrimaryPinRect(safeArea, dpi, hasDaily);
            ApplyPx(_pin, _pinRectPx);
            ApplyPx(_ring, HomeLayout.RingRect(safeArea, dpi, hasDaily));

            _heroRectPx = HomeLayout.HeroRect(safeArea, dpi);
            ApplyPx(_hero, _heroRectPx);
            float shadowDx = 4f * HudBands.PxPerDp(dpi);
            float shadowDy = 6f * HudBands.PxPerDp(dpi);
            ApplyPx(_heroShadow, new Rect(_heroRectPx.x + shadowDx,
                _heroRectPx.y - shadowDy, _heroRectPx.width, _heroRectPx.height));
            ApplyPx(_title.rectTransform, HomeLayout.HeaderRect(safeArea, dpi));

            // Guarded: null when Create() ran with dailyEntryUnlocked false (S-01).
            if (_dailyPin != null)
            {
                _dailyPinRectPx = HomeLayout.DailyPinRect(safeArea, dpi);
                ApplyPx(_dailyPin, _dailyPinRectPx);
            }
            if (_reminderGear != null)
            {
                _reminderGearRectPx = DailyReminderLayout.GearRect(safeArea, dpi);
                ApplyPx(_reminderGear, _reminderGearRectPx);
            }
            if (_reminderSheet != null && _reminderSheet.IsVisible)
                _reminderSheet.LayoutForViewport(safeArea, dpi);
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
                // A wide CTA needs only a breathing cue; the old 8% square-pin pulse made the
                // new full-width action lunge outside its safe-area margins.
                scale = 1f + 0.025f * Mathf.Sin(_phase);
            }
            _pin.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
