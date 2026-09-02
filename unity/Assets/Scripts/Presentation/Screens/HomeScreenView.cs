using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CatMetro.Presentation.Cosmetics;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Hud.WavePreview;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Theme;
using CatMetro.Services;
using CatMetro.Services.Cosmetics;

namespace CatMetro.Presentation.Screens
{
    // LOOK Home: a carved navy/cream menu frame over the real, already-loaded tick-0 board,
    // with tactile full-width routes and three preserved cosmetics holders. The default remains
    // commerce-free; Daily is constructed only when progress/config unlocks it, including a
    // threshold crossed during the current run. Hit regions retain the existing unregister
    // lifecycle, and motion-off keeps the action's static raised-ring cue.
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
        private const float WindowXMin = 0.075f;
        private const float WindowXMax = 0.925f;
        private const float WindowYMin = 0.075f;
        private const float WindowYMax = 0.93f;
        private const string AudioToggleRegionId = "home.audio.toggle";
        private const int AudioToggleRegionPriority = ChromeRegions.HomeScreenPriority;

        public System.Action LevelSelected;
        public System.Action DailySelected;
        public System.Action ReminderAccepted;
        public System.Action ReminderDismissed;
        public System.Action<bool> ReminderEnabledChanged;
        public System.Action<DailyReminderSlot> ReminderSlotChanged;
        public System.Action<bool> AudioEnabledChanged;

        private ChromeRegions _regions;
        private System.Func<bool> _motionOff;
        private bool _registered;
        private bool _dailyRegistered;
        private bool _reminderGearRegistered;
        private bool _audioToggleRegistered;
        private bool _audioEnabled = true;
        private bool _shown; // #46 review F4: Show()-left-shown intent, survives OnDisable/OnEnable
        private Image _background;
        private RectTransform _backdropTop;
        private RectTransform _backdropBottom;
        private RectTransform _backdropLeft;
        private RectTransform _backdropRight;
        private RectTransform _titlePlaqueShadow;
        private RectTransform _titlePlaque;
        private TMP_Text _title;
        private RectTransform _heroShadow;
        private RectTransform _hero;
        private RectTransform _dioramaWindow;
        private Image[] _markers;
        private RectTransform _pin;
        private RectTransform _ring;
        private TMP_Text _primaryLabel;
        private RectTransform _dailyPin;
        private TMP_Text _dailyLabel;
        private TMP_Text _dailyTally;
        private TMP_Text _dailyStatus;
        private RectTransform _reminderGear;
        private RectTransform _audioToggle;
        private TMP_Text _audioToggleLabel;
        private Image _audioTogglePaint;
        private DailyReminderSheet _reminderSheet;
        private CosmeticPortraitView _profilePortrait;
        private Rect _pinRectPx;
        private Rect _dailyPinRectPx;
        private Rect _heroRectPx;
        private Rect _reminderGearRectPx;
        private Rect _audioToggleRectPx;
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
        public RectTransform DioramaWindowTransform => _dioramaWindow;
        public string PrimaryLabelText => _primaryLabel != null ? _primaryLabel.text : "";
        public RectTransform ReminderGearTransform => _reminderGear;
        public Rect ReminderGearRectPx => _reminderGearRectPx;
        public DailyReminderSheet ReminderSheet => _reminderSheet;
        public RectTransform AudioToggleTransform => _audioToggle;
        public Rect AudioToggleRectPx => _audioToggleRectPx;
        public string AudioToggleText => _audioToggleLabel != null ? _audioToggleLabel.text : "";
        public bool AudioEnabled => _audioEnabled;
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

            // The board has already been built and framed before Home is composed. A transparent
            // paint token leaves that real, paused tick-0 diorama visible instead of replacing it
            // with a flat card. The named window below is deliberately a graphic-free RectTransform.
            view._background = MakeSurface(go.transform, "Background",
                Vector2.zero, Vector2.one,
                Palette.WithAlpha(Palette.WarmPaper, 0f), rounded: false);
            var surroundShade = Palette.WithAlpha(Palette.DepotNavy, 0.48f);
            view._backdropTop = MakeSurface(go.transform, "BackdropTop",
                Vector2.zero, Vector2.one, surroundShade, false).rectTransform;
            view._backdropBottom = MakeSurface(go.transform, "BackdropBottom",
                Vector2.zero, Vector2.one, surroundShade, false).rectTransform;
            view._backdropLeft = MakeSurface(go.transform, "BackdropLeft",
                Vector2.zero, Vector2.one, surroundShade, false).rectTransform;
            view._backdropRight = MakeSurface(go.transform, "BackdropRight",
                Vector2.zero, Vector2.one, surroundShade, false).rectTransform;

            // A navy, raised sign carries the same carved-toy identity as the board labels.
            view._titlePlaqueShadow = MakeChip(go.transform, "TitlePlaqueShadow",
                Palette.WithAlpha(Palette.DepotNavy, 0.72f));
            view._titlePlaque = MakeChip(go.transform, "TitlePlaque", Palette.DepotNavy);
            MakeSurface(view._titlePlaque, "TitlePlaqueFace",
                new Vector2(0.018f, 0.09f), new Vector2(0.982f, 0.98f),
                Palette.InkNavy, rounded: true);
            MakeSurface(view._titlePlaque, "TitleNailLeft",
                new Vector2(0.035f, 0.38f), new Vector2(0.075f, 0.62f),
                Palette.TicketOrange, rounded: true);
            MakeSurface(view._titlePlaque, "TitleNailRight",
                new Vector2(0.925f, 0.38f), new Vector2(0.965f, 0.62f),
                Palette.TicketOrange, rounded: true);
            var titleCarve = MakeText(view._titlePlaque, "TitleCarveShadow",
                new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.94f),
                Strings.UiStrings.Get("home.title"), 48f, Palette.DepotNavy);
            titleCarve.rectTransform.anchoredPosition = new Vector2(0f, -2f);
            titleCarve.fontStyle = FontStyles.Bold;
            titleCarve.enableAutoSizing = true;
            titleCarve.fontSizeMin = 28f;
            titleCarve.fontSizeMax = 48f;
            titleCarve.enableWordWrapping = false;
            view._title = MakeText(view._titlePlaque, "Title",
                new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.96f),
                Strings.UiStrings.Get("home.title"), 48f, Palette.CreamCard);
            view._title.fontStyle = FontStyles.Bold;
            view._title.enableAutoSizing = true;
            view._title.fontSizeMin = 28f;
            view._title.fontSizeMax = 48f;
            view._title.enableWordWrapping = false;

            // The frame is opaque, but its center is not an Image: pixels there come straight
            // from the real board camera. Split edge geometry avoids the classic full-card fill
            // that silently paints over the diorama even when a child looks transparent.
            view._heroShadow = MakeRect(go.transform, "HeroShadow");
            MakeSurface(view._heroShadow, "DioramaShadowTop",
                new Vector2(0.018f, 0.918f), new Vector2(0.982f, 0.992f),
                Palette.WithAlpha(Palette.DepotNavy, 0.72f), false);
            MakeSurface(view._heroShadow, "DioramaShadowBottom",
                new Vector2(0.018f, 0.008f), new Vector2(0.982f, 0.082f),
                Palette.WithAlpha(Palette.DepotNavy, 0.72f), false);
            MakeSurface(view._heroShadow, "DioramaShadowLeft",
                new Vector2(0.018f, 0.07f), new Vector2(0.082f, 0.93f),
                Palette.WithAlpha(Palette.DepotNavy, 0.72f), false);
            MakeSurface(view._heroShadow, "DioramaShadowRight",
                new Vector2(0.918f, 0.07f), new Vector2(0.982f, 0.93f),
                Palette.WithAlpha(Palette.DepotNavy, 0.72f), false);

            view._hero = MakeRect(go.transform, "HeroCard");
            view._dioramaWindow = MakeRect(view._hero, "DioramaWindow",
                new Vector2(WindowXMin, WindowYMin),
                new Vector2(WindowXMax, WindowYMax));
            MakeSurface(view._hero, "DioramaFrameTop",
                new Vector2(0.025f, 0.925f), new Vector2(0.975f, 0.99f),
                Palette.CreamCard, false);
            MakeSurface(view._hero, "DioramaFrameBottom",
                new Vector2(0.025f, 0.01f), new Vector2(0.975f, 0.08f),
                Palette.CreamCard, false);
            MakeSurface(view._hero, "DioramaFrameLeft",
                new Vector2(0.025f, 0.07f), new Vector2(0.08f, 0.935f),
                Palette.CreamCard, false);
            MakeSurface(view._hero, "DioramaFrameRight",
                new Vector2(0.92f, 0.07f), new Vector2(0.975f, 0.935f),
                Palette.CreamCard, false);
            MakeSurface(view._hero, "DioramaInnerTop",
                new Vector2(0.075f, 0.918f), new Vector2(0.925f, 0.93f),
                Palette.InkNavy, false);
            MakeSurface(view._hero, "DioramaInnerBottom",
                new Vector2(0.075f, 0.075f), new Vector2(0.925f, 0.087f),
                Palette.InkNavy, false);
            MakeSurface(view._hero, "DioramaInnerLeft",
                new Vector2(0.068f, 0.075f), new Vector2(0.08f, 0.93f),
                Palette.InkNavy, false);
            MakeSurface(view._hero, "DioramaInnerRight",
                new Vector2(0.92f, 0.075f), new Vector2(0.932f, 0.93f),
                Palette.InkNavy, false);
            MakeSurface(view._hero, "WindowLampLeft",
                new Vector2(0.085f, 0.938f), new Vector2(0.125f, 0.975f),
                Palette.TicketOrange, true);
            MakeSurface(view._hero, "WindowLampRight",
                new Vector2(0.875f, 0.938f), new Vector2(0.915f, 0.975f),
                Palette.TicketOrange, true);

            // These exact direct-child Image holders are an integration seam. Cosmetics may
            // mount portrait/model roots beneath them; Home never traverses or normalizes those
            // roots, whose provider-authored localPosition and ~300x holder scale stay intact.
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

            view._markers = new[]
            {
                MakeSurface(view._hero, "RouteMarkerA", new Vector2(0.135f, 0.845f),
                    new Vector2(0.17f, 0.885f), Palette.SignalRed, true),
                MakeSurface(view._hero, "RouteMarkerB", new Vector2(0.83f, 0.845f),
                    new Vector2(0.865f, 0.885f), Palette.HarborBlue, true),
                MakeSurface(view._hero, "RouteMarkerC", new Vector2(0.135f, 0.095f),
                    new Vector2(0.17f, 0.135f), Palette.TabbyYellow, true),
            };

            // The raised-ring shape twin sits BEHIND the pin (sibling order = draw order).
            // The ring is the single warm CTA glow; the navy lip and cream face make the route
            // read as a raised wooden button. Every paint comes from the Palette source.
            view._ring = MakeChip(go.transform, "PinRingL001", Palette.TicketOrange);
            view._pin = MakeChip(go.transform, "PinL001", Palette.DepotNavy);
            var playFace = MakeSurface(view._pin, "PlayButtonFace",
                new Vector2(0.008f, 0.10f), new Vector2(0.992f, 0.99f),
                Palette.CreamCard, true);
            var playIcon = MakeSurface(playFace.transform, "PlayIconTile",
                new Vector2(0.035f, 0.14f), new Vector2(0.185f, 0.88f),
                Palette.InkNavy, true);
            MakeSurface(playIcon.transform, "PlayIconWindowA",
                new Vector2(0.20f, 0.55f), new Vector2(0.43f, 0.78f),
                Palette.CreamCard, true);
            MakeSurface(playIcon.transform, "PlayIconWindowB",
                new Vector2(0.57f, 0.55f), new Vector2(0.80f, 0.78f),
                Palette.CreamCard, true);
            MakeSurface(playIcon.transform, "PlayIconRail",
                new Vector2(0.18f, 0.25f), new Vector2(0.82f, 0.38f),
                Palette.TicketOrange, true);
            MakeSurface(playIcon.transform, "PlayIconWheelA",
                new Vector2(0.20f, 0.08f), new Vector2(0.40f, 0.28f),
                Palette.CreamCard, true);
            MakeSurface(playIcon.transform, "PlayIconWheelB",
                new Vector2(0.60f, 0.08f), new Vector2(0.80f, 0.28f),
                Palette.CreamCard, true);
            view._primaryLabel = MakeText(playFace.transform, "PlayLabel",
                new Vector2(0.22f, 0f), new Vector2(0.94f, 1f),
                Strings.UiStrings.Get("intro.play"), 42f, Palette.InkNavy);
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
                _dailyPin = MakeChip(transform, "PinDaily", Palette.DepotNavy);
                var dailyFace = MakeSurface(_dailyPin, "DailyButtonFace",
                    new Vector2(0.008f, 0.10f), new Vector2(0.992f, 0.99f),
                    Palette.CreamCard, true);
                var dailyIcon = MakeSurface(dailyFace.transform, "DailyIconTile",
                    new Vector2(0.035f, 0.14f), new Vector2(0.185f, 0.88f),
                    Palette.MetroTeal, true);
                MakeSurface(dailyIcon.transform, "DailyIconPage",
                    new Vector2(0.20f, 0.18f), new Vector2(0.80f, 0.72f),
                    Palette.CreamCard, true);
                MakeSurface(dailyIcon.transform, "DailyIconBinding",
                    new Vector2(0.20f, 0.67f), new Vector2(0.80f, 0.81f),
                    Palette.TicketOrange, true);
                _dailyLabel = MakeText(dailyFace.transform, "PinDailyLabel",
                    new Vector2(0.22f, 0.50f), new Vector2(0.94f, 0.94f),
                    Strings.UiStrings.Get("home.daily.label"), 28f, Palette.InkNavy); // key-only, never a literal
                _dailyLabel.enableAutoSizing = true;
                _dailyLabel.fontSizeMin = 18f;
                _dailyLabel.fontSizeMax = 28f;
                _dailyLabel.fontStyle = FontStyles.Bold;
                _dailyTally = MakeText(dailyFace.transform, "LifetimeTally",
                    new Vector2(0.22f, 0.08f), new Vector2(0.94f, 0.53f),
                    "", 14f, Palette.InkNavy);
                _dailyTally.enableAutoSizing = true;
                _dailyTally.fontSizeMin = 9f;
                _dailyTally.fontSizeMax = 14f;
                _dailyStatus = MakeText(dailyFace.transform, "DailyStatus",
                    new Vector2(0.22f, 0.08f), new Vector2(0.94f, 0.53f),
                    "", 14f, Palette.InkNavy);
                _dailyStatus.enableAutoSizing = true;
                _dailyStatus.fontSizeMin = 9f;
                _dailyStatus.fontSizeMax = 14f;
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

        // The sound setting is independent of Daily progression. GameRoot configures it on
        // every real boot from the existing save-v3 settings.audio field, so mute is reachable
        // from Home even when the reminder gear has not unlocked.
        public void ConfigureAudio(bool enabled)
        {
            EnsureAudioToggle();
            _audioEnabled = enabled;
            _audioToggleLabel.text = Strings.UiStrings.Get(
                enabled ? "settings.audio.on" : "settings.audio.off");
            _audioTogglePaint.color = enabled ? Palette.MetroTeal : Palette.CreamCard;
            LayoutForViewport(Screen.safeArea, Screen.dpi);
            if (_shown && isActiveAndEnabled) RegisterAudioToggle();
        }

        private void EnsureAudioToggle()
        {
            if (_audioToggle != null) return;
            _audioToggle = MakeChip(transform, "SoundToggle", Palette.MetroTeal);
            _audioTogglePaint = _audioToggle.GetComponent<Image>();
            _audioToggleLabel = MakeText(_audioToggle, "SoundToggleLabel",
                Vector2.zero, Vector2.one, "", 17f, Palette.InkNavy);
            _audioToggleLabel.enableAutoSizing = true;
            _audioToggleLabel.fontSizeMin = 10f;
            _audioToggleLabel.fontSizeMax = 17f;
            _audioToggleLabel.fontStyle = FontStyles.Bold;
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
            if (rounded) ApplyRoundedPaint(img);
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
            ApplyRoundedPaint(img);
            img.color = Palette.WithAlpha(Palette.DepotNavy, 0.18f);
            img.raycastTarget = false;
            return img;
        }

        private static RectTransform MakeRect(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        private static RectTransform MakeRect(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = MakeRect(parent, name);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform MakeChip(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            ApplyRoundedPaint(img);
            img.color = color;
            img.raycastTarget = false;
            return rect;
        }

        private static void ApplyRoundedPaint(Image image)
        {
            var mat = UiChromeMaterial.Shared;
            if (mat != null) image.material = mat;
            image.sprite = HudShapeSprites.RoundedSquare;
            image.type = Image.Type.Sliced;
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
            RegisterAudioToggle();
        }

        public void Hide()
        {
            _shown = false;
            if (_reminderSheet != null) _reminderSheet.Hide();
            UnregisterPin();
            UnregisterDailyPin();
            UnregisterReminderGear();
            UnregisterAudioToggle();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            UnregisterPin(); // R1-F3 lifetime law
            UnregisterDailyPin();
            UnregisterReminderGear();
            UnregisterAudioToggle();
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
            UnregisterAudioToggle();
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
                RegisterAudioToggle();
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

        private void RegisterAudioToggle()
        {
            if (_audioToggle != null && _regions != null && !_audioToggleRegistered)
            {
                _regions.Register(AudioToggleRegionId, () => _audioToggleRectPx,
                    () => AudioEnabledChanged?.Invoke(!_audioEnabled),
                    AudioToggleRegionPriority);
                _audioToggleRegistered = true;
            }
        }

        private void UnregisterAudioToggle()
        {
            if (_regions != null && _audioToggleRegistered)
            {
                _regions.Unregister(AudioToggleRegionId);
                _audioToggleRegistered = false;
            }
        }

        // Injected safe-area/viewport geometry keeps capture and runtime on the same law. The
        // optional viewport falls back to the live Screen only for ordinary Show() calls; the
        // offscreen rig supplies its exact RenderTexture bounds.
        public void LayoutForViewport(Rect safeArea, float dpi, Rect viewport = default)
        {
            bool hasDaily = _dailyPin != null;
            _pinRectPx = HomeLayout.PrimaryPinRect(safeArea, dpi, hasDaily);
            ApplyPx(_pin, _pinRectPx);
            ApplyPx(_ring, HomeLayout.RingRect(safeArea, dpi, hasDaily));

            _heroRectPx = HomeLayout.HeroRect(safeArea, dpi, hasDaily);
            if (viewport.width <= 0f || viewport.height <= 0f)
            {
                viewport = new Rect(0f, 0f,
                    Mathf.Max(Screen.width, safeArea.xMax),
                    Mathf.Max(Screen.height, safeArea.yMax));
            }
            float windowXMin = _heroRectPx.x + _heroRectPx.width * WindowXMin;
            float windowXMax = _heroRectPx.x + _heroRectPx.width * WindowXMax;
            float windowYMin = _heroRectPx.y + _heroRectPx.height * WindowYMin;
            float windowYMax = _heroRectPx.y + _heroRectPx.height * WindowYMax;
            ApplyPx(_backdropBottom, new Rect(viewport.xMin, viewport.yMin,
                viewport.width, Mathf.Max(0f, windowYMin - viewport.yMin)));
            ApplyPx(_backdropTop, new Rect(viewport.xMin, windowYMax, viewport.width,
                Mathf.Max(0f, viewport.yMax - windowYMax)));
            ApplyPx(_backdropLeft, new Rect(viewport.xMin, windowYMin,
                Mathf.Max(0f, windowXMin - viewport.xMin),
                Mathf.Max(0f, windowYMax - windowYMin)));
            ApplyPx(_backdropRight, new Rect(windowXMax, windowYMin,
                Mathf.Max(0f, viewport.xMax - windowXMax),
                Mathf.Max(0f, windowYMax - windowYMin)));
            ApplyPx(_hero, _heroRectPx);
            float shadowDx = 4f * HudBands.PxPerDp(dpi);
            float shadowDy = 6f * HudBands.PxPerDp(dpi);
            ApplyPx(_heroShadow, new Rect(_heroRectPx.x + shadowDx,
                _heroRectPx.y - shadowDy, _heroRectPx.width, _heroRectPx.height));
            var titlePlaque = HomeLayout.TitleRect(safeArea, dpi,
                _audioToggle != null, _reminderGear != null);
            ApplyPx(_titlePlaque, titlePlaque);
            ApplyPx(_titlePlaqueShadow, HomeLayout.TitleShadowRect(titlePlaque, dpi));

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
            if (_audioToggle != null)
            {
                _audioToggleRectPx = HomeLayout.AudioToggleRect(safeArea, dpi);
                ApplyPx(_audioToggle, _audioToggleRectPx);
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
