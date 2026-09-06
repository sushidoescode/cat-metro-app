using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CatMetro.Presentation.Cosmetics;
using CatMetro.Presentation.Cats;
using CatMetro.Presentation.Fx;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Hud.WavePreview;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Theme;
using CatMetro.Services;
using CatMetro.Services.Cosmetics;

namespace CatMetro.Presentation.Screens
{
    // LOOK Home: a carved navy/cream menu frame over the real, already-loaded tick-0 board,
    // with tactile routes and one shared cosmetics holder. The default remains
    // commerce-free; Daily is teased while locked and becomes interactive when the existing
    // progress/config gate opens, including a threshold crossed during the current run. Hit regions retain the existing unregister
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
        private const float WindowXMin = 0.05f;
        private const float WindowXMax = 0.95f;
        private const float WindowYMin = 0.05f;
        private const float WindowYMax = 0.95f;
        private const string AudioToggleRegionId = "home.audio.toggle";
        private const int AudioToggleRegionPriority = ChromeRegions.HomeScreenPriority;

        public System.Action LevelSelected;
        public System.Action DailySelected;
        public System.Action ReminderAccepted;
        public System.Action ReminderDismissed;
        public System.Action<bool> ReminderEnabledChanged;
        public System.Action<DailyReminderSlot> ReminderSlotChanged;
        public System.Action<bool> AudioEnabledChanged;
        // Pixel bounds of the same aperture used by the window and its vignette. Bootstrap
        // normalizes against the camera's screen or capture target before fitting the board.
        public System.Action<Rect> DioramaLaidOut;

        private ChromeRegions _regions;
        private System.Func<bool> _motionOff;
        private bool _registered;
        private bool _dailyRegistered;
        private bool _reminderGearRegistered;
        private bool _audioToggleRegistered;
        private bool _audioEnabled = true;
        private bool _shown; // #46 review F4: Show()-left-shown intent, survives OnDisable/OnEnable
        private BoardFx _hideFx;
        private Image _background;
        private Image _backdrop;
        private Sprite _vignetteSprite;
        private Rect _vignetteAperture;
        private float _vignetteUnderFrame;
        private RectTransform _lampGlow;
        private RectTransform _lampCore;
        private RectTransform _lampCord;
        private RectTransform _lampShade;
        private Image[] _heroShadowEdges;
        private RectTransform _titlePlaqueShadow;
        private RectTransform _titlePlaque;
        private RectTransform[] _titleNails;
        private RectTransform _titleCatMark;
        private TMP_Text _title;
        private TMP_Text _titleCarve;
        private RectTransform _heroShadow;
        private RectTransform _hero;
        private RectTransform _dioramaWindow;
        private ChromeChip _playChip;
        private ChromeChip _dailyChip;
        private RectTransform _pin;
        private RectTransform _ring;
        private TMP_Text _primaryLabel;
        private RectTransform _dailyPin;
        private Image _dailyLock;
        private Image[] _dailyPips;
        private bool _dailyUnlocked;
        private bool _dailyUnlockAttention;
        private int _dailyLifetimeCount;
        private string _dailyTransientKey;
        private TMP_Text _dailyLabel;
        private TMP_Text _dailyTally;
        private TMP_Text _dailyStatus;
        private RectTransform _reminderGear;
        private RectTransform _audioToggle;
        private TMP_Text _audioToggleLabel;
        private Image _audioTogglePaint;
        private Image _audioSpeaker;
        private Rect _audioToggleHitRectPx;
        private DailyReminderSheet _reminderSheet;
        private CosmeticPortraitView _profilePortrait;
        private HomeProfileRigView _profileRig;
        private bool _rigCovered;
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
        public bool IsVisible => _shown && gameObject.activeSelf;
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
        public HomeProfileRigView ProfileRig => _profileRig;
        public RectTransform ProfilePortraitTransform => _profilePortrait != null
            ? _profilePortrait.RootTransform
            : null;
        public int MarkerCount => 0;
        public Color[] MarkerColors => System.Array.Empty<Color>();

        // The teaser is always visible; only the saved/configured unlock enables its input.
        public static HomeScreenView Create(Transform canvasParent,
            bool dailyEntryUnlocked = false, int lifetimeDailyCompletions = 0,
            ICosmeticPortraitSource portraitSource = null,
            CatModelCatalog catCatalog = null)
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
            view._backdrop = MakeSurface(go.transform, "HomeVignette",
                Vector2.zero, Vector2.one, new Color(42f / 255f, 26f / 255f, 16f / 255f), false);
            var lampGlow = MakeSurface(go.transform, "TitleLampGlow", Vector2.zero, Vector2.one,
                Palette.WithAlpha(Palette.TicketOrange, 0.18f), false);
            lampGlow.sprite = HudShapeSprites.RadialGlow;
            view._lampGlow = lampGlow.rectTransform;
            var lampCore = MakeSurface(go.transform, "TitleLampCore", Vector2.zero, Vector2.one,
                Palette.WithAlpha(Palette.WarmPaper, 0.85f), false);
            lampCore.sprite = HudShapeSprites.RadialGlow;
            view._lampCore = lampCore.rectTransform;
            view._lampCord = MakeSurface(go.transform, "LampCord", Vector2.zero, Vector2.one,
                Palette.DepotNavy, false).rectTransform;
            var lampShade = MakeSurface(go.transform, "LampShade", Vector2.zero, Vector2.one,
                Palette.InkNavy, false);
            lampShade.sprite = HudShapeSprites.Disc;
            view._lampShade = lampShade.rectTransform;

            // A navy, raised sign carries the same carved-toy identity as the board labels.
            view._titlePlaqueShadow = MakeChip(go.transform, "TitlePlaqueShadow",
                Palette.WithAlpha(Palette.DepotNavy, 0.45f));
            view._titlePlaqueShadow.GetComponent<Image>().sprite = HudShapeSprites.SoftShadow;
            view._titlePlaque = MakeChip(go.transform, "TitlePlaque", Palette.DepotNavy);
            MakeSurface(view._titlePlaque, "TitlePlaqueFace",
                new Vector2(0.018f, 0.045f), new Vector2(0.982f, 0.975f),
                Palette.InkNavy, rounded: true);
            MakeSurface(view._titlePlaque, "TitleBevelTop",
                new Vector2(0.022f, 0.93f), new Vector2(0.978f, 0.976f),
                Color.Lerp(Palette.InkNavy, Palette.CreamCard, 0.12f), true);
            view._titleNails = new RectTransform[4];
            for (int i = 0; i < view._titleNails.Length; i++)
            {
                var nail = MakeSurface(view._titlePlaque, "TitleNail" + i,
                    Vector2.zero, Vector2.zero,
                    Color.Lerp(Palette.CreamCard, Palette.DepotNavy, 0.35f), false);
                nail.sprite = HudShapeSprites.Disc;
                view._titleNails[i] = nail.rectTransform;
            }
            string wordmark = Strings.UiStrings.Get("home.title").Replace(" ", "\n");
            var titleCarve = view._titleCarve = MakeText(view._titlePlaque, "TitleCarveShadow",
                new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.94f),
                wordmark, 48f, Palette.DepotNavy);
            titleCarve.rectTransform.anchoredPosition = new Vector2(0f, -2f);
            titleCarve.fontStyle = FontStyles.Bold;
            titleCarve.enableAutoSizing = true;
            titleCarve.fontSizeMin = 28f;
            titleCarve.fontSizeMax = 48f;
            titleCarve.enableWordWrapping = false;
            view._title = MakeText(view._titlePlaque, "Title",
                new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.96f),
                wordmark, 48f, Palette.CreamCard);
            view._title.fontStyle = FontStyles.Bold;
            view._title.enableAutoSizing = true;
            view._title.fontSizeMin = 28f;
            view._title.fontSizeMax = 48f;
            view._title.enableWordWrapping = false;
            var catMark = MakeSurface(view._titlePlaque, "TitleCatMark",
                Vector2.zero, Vector2.zero, Color.white, false);
            catMark.sprite = Resources.Load<Sprite>("Theme/HomeCatMark");
            catMark.useSpriteMesh = true;
            catMark.preserveAspect = true;
            view._titleCatMark = catMark.rectTransform;

            // The frame is opaque, but its center is not an Image: pixels there come straight
            // from the real board camera. Split edge geometry avoids the classic full-card fill
            // that silently paints over the diorama even when a child looks transparent.
            view._heroShadow = MakeRect(go.transform, "HeroShadow");
            MakeSurface(view._heroShadow, "DioramaShadowTop",
                new Vector2(0.018f, 0.918f), new Vector2(0.982f, 0.992f),
                Palette.WithAlpha(Palette.DepotNavy, 0.45f), false);
            MakeSurface(view._heroShadow, "DioramaShadowBottom",
                new Vector2(0.018f, 0.008f), new Vector2(0.982f, 0.082f),
                Palette.WithAlpha(Palette.DepotNavy, 0.45f), false);
            MakeSurface(view._heroShadow, "DioramaShadowLeft",
                new Vector2(0.018f, 0.07f), new Vector2(0.082f, 0.93f),
                Palette.WithAlpha(Palette.DepotNavy, 0.45f), false);
            MakeSurface(view._heroShadow, "DioramaShadowRight",
                new Vector2(0.918f, 0.07f), new Vector2(0.982f, 0.93f),
                Palette.WithAlpha(Palette.DepotNavy, 0.45f), false);

            view._heroShadowEdges = view._heroShadow.GetComponentsInChildren<Image>();
            foreach (var edge in view._heroShadowEdges)
            {
                edge.sprite = HudShapeSprites.SoftShadow;
                edge.type = Image.Type.Sliced;
            }

            view._hero = MakeRect(go.transform, "HeroCard");
            view._dioramaWindow = MakeRect(view._hero, "DioramaWindow",
                new Vector2(WindowXMin, WindowYMin),
                new Vector2(WindowXMax, WindowYMax));
            MakeSurface(view._hero, "DioramaFrameTop",
                new Vector2(0.02f, 0.95f), new Vector2(0.98f, 0.99f), Palette.CreamCard, false);
            MakeSurface(view._hero, "DioramaFrameBottom",
                new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.05f), Palette.CreamCard, false);
            MakeSurface(view._hero, "DioramaFrameLeft",
                new Vector2(0.02f, 0.05f), new Vector2(0.05f, 0.95f), Palette.CreamCard, false);
            MakeSurface(view._hero, "DioramaFrameRight",
                new Vector2(0.95f, 0.05f), new Vector2(0.98f, 0.95f), Palette.CreamCard, false);
            // B remains the shared portrait/rig mount; empty furniture has no Home role.
            var parkedDistrictB = MakeSilhouette(view._hero, "ParkedDistrictB",
                new Vector2(0.67f, 0.455f), new Vector2(0.89f, 0.605f));
            if (portraitSource != null)
            {
                parkedDistrictB.color = Color.clear;
                view._profilePortrait = CosmeticPortraitView.Create(
                    parkedDistrictB.transform, portraitSource, "HomeProfilePortrait");
                if (catCatalog != null && catCatalog.AdmittedEntryCount == 1)
                    view._profileRig = HomeProfileRigView.Create(
                        parkedDistrictB.rectTransform, view._profilePortrait, catCatalog);
                else HomeProfileRigView.ReportUnavailable(catCatalog);
            }

            view._playChip = ChromeChip.PaintPrimary(go.transform, default,
                Strings.UiStrings.Get("intro.play"), Palette.TicketOrange, HudShapeSprites.Train);
            view._pin = view._playChip.Root;
            view._pin.name = "PinL001";
            view._ring = (RectTransform)view._pin.Find("Ring");
            view._playChip.Face.name = "PlayButtonFace";
            view._primaryLabel = view._playChip.Label;
            view._primaryLabel.name = "PlayLabel";

            view.BuildDailyPin();
            if (dailyEntryUnlocked) view.UnlockDaily(lifetimeDailyCompletions);
            else view.RefreshDailyPaint();

            go.SetActive(false);
            return view;
        }

        private void BuildDailyPin()
        {
            _dailyChip = ChromeChip.PaintPrimary(transform, default,
                Strings.UiStrings.Get("home.daily.label"), Palette.CreamCard, HudShapeSprites.Padlock);
            _dailyPin = _dailyChip.Root;
            _dailyPin.name = "PinDaily";
            var face = _dailyChip.Face;
            face.name = "DailyButtonFace";
            _dailyLock = _dailyPin.Find("Content/Icon").GetComponent<Image>();
            _dailyLock.name = "DailyLock";
            _dailyLabel = _dailyChip.Label;
            _dailyLabel.name = "PinDailyLabel";
            _dailyPips = new Image[7];
            for (int i = 0; i < _dailyPips.Length; i++)
            {
                _dailyPips[i] = MakeSurface(face.transform, "DailyWinPip" + i,
                    Vector2.zero, Vector2.zero, Palette.DepotNavy, false);
                _dailyPips[i].sprite = HudShapeSprites.Disc;
            }
            // Caption sits in the reserved bottom breathing room, so a half-width pill never
            // squeezes the 12dp type floor to fit a two-line explanation.
            _dailyTally = MakeText(_dailyPin, "LifetimeTally", Vector2.zero, Vector2.zero,
                "", TypeScale.Caption, Palette.CreamCard);
            _dailyStatus = MakeText(_dailyPin, "DailyStatus", Vector2.zero, Vector2.zero,
                "", TypeScale.Caption, Palette.CreamCard);
            _dailyTally.enableWordWrapping = _dailyStatus.enableWordWrapping = true;
            SetCampaignWinCount(0);
        }

        public void UnlockDaily(int lifetimeDailyCompletions, bool highlight = false)
        {
            if (!_dailyUnlocked && highlight) _dailyUnlockAttention = true;
            _dailyUnlocked = true;
            SetDailyLifetimeCompletions(lifetimeDailyCompletions);
            SetCampaignWinCount(7);
            RefreshDailyPaint();
            LayoutForViewport(Screen.safeArea, Screen.dpi);
            if (_shown && isActiveAndEnabled) RegisterDailyPin();
        }

        public void SetCampaignWinCount(int count)
        {
            if (_dailyPips == null) return;
            int filled = _dailyUnlocked ? 7 : Mathf.Clamp(count, 0, 7);
            for (int i = 0; i < _dailyPips.Length; i++)
                _dailyPips[i].color = i < filled ? Palette.MetroTeal
                    : Palette.WithAlpha(Palette.DepotNavy, 0.25f);
        }

        private void RefreshDailyPaint()
        {
            _dailyChip.SetRingColour(_dailyUnlocked && _dailyUnlockAttention
                ? Palette.TicketOrange : Palette.CreamCard);
            _dailyChip.SetOpacity(_dailyUnlocked ? 1f : 0.6f);
            _dailyLock.sprite = _dailyUnlocked ? HudShapeSprites.Star : HudShapeSprites.Padlock;
            SetDailyStatusKey(_dailyTransientKey);
        }

        public void SetDailyLifetimeCompletions(int count)
        {
            _dailyLifetimeCount = Mathf.Max(0, count);
            _dailyTally.text = _dailyLifetimeCount > 0
                ? Strings.UiStrings.Get("home.daily.tally").Replace("{count}", _dailyLifetimeCount.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)) : "";
            SetDailyStatusKey(_dailyTransientKey);
        }

        public void SetDailyStatusKey(string key)
        {
            if (_dailyStatus == null || _dailyTally == null) return;
            _dailyTransientKey = key;
            string statusKey = !_dailyUnlocked ? "home.daily.locked"
                : !string.IsNullOrEmpty(key) ? key
                : _dailyLifetimeCount == 0 ? "home.daily.ready" : null;
            _dailyStatus.text = statusKey != null ? Strings.UiStrings.Get(statusKey) : "";
            _dailyStatus.gameObject.SetActive(statusKey != null);
            _dailyTally.gameObject.SetActive(_dailyUnlocked && _dailyLifetimeCount > 0 && statusKey == null);
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
            _audioTogglePaint.color = Palette.CreamCard;
            _audioSpeaker.sprite = enabled ? HudShapeSprites.SpeakerOn : HudShapeSprites.SpeakerOff;
            LayoutForViewport(Screen.safeArea, Screen.dpi);
            if (_shown && isActiveAndEnabled) RegisterAudioToggle();
        }

        private void EnsureAudioToggle()
        {
            if (_audioToggle != null) return;
            _audioToggle = MakeChip(transform, "SoundToggle", Palette.CreamCard);
            _audioTogglePaint = _audioToggle.GetComponent<Image>();
            _audioTogglePaint.sprite = HudShapeSprites.Disc;
            _audioTogglePaint.type = Image.Type.Simple;
            _audioTogglePaint.preserveAspect = true;
            _audioSpeaker = MakeSurface(_audioToggle, "SpeakerGlyph",
                new Vector2(0.20f, 0.20f), new Vector2(0.80f, 0.80f), Palette.InkNavy, false);
            _audioSpeaker.sprite = HudShapeSprites.SpeakerOn;
            _audioSpeaker.preserveAspect = true;
            _audioToggleLabel = MakeText(_audioToggle, "SoundToggleLabel",
                Vector2.zero, Vector2.one, "", 17f, Palette.InkNavy);
            _audioToggleLabel.enableAutoSizing = true;
            _audioToggleLabel.fontSizeMin = TypeScale.Minimum;
            _audioToggleLabel.fontSizeMax = 17f;
            _audioToggleLabel.fontStyle = FontStyles.Bold;
            // Keep the CSV-backed semantic label available to accessibility/read-back callers.
            _audioToggleLabel.gameObject.SetActive(false);
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
            if (_regions != null) _regions.StackedModalChanged -= RefreshRigVisibility;
            _regions = regions;
            if (_regions != null) _regions.StackedModalChanged += RefreshRigVisibility;
            RefreshRigVisibility();
            _motionOff = motionOff; // GameRoot.MotionOff binding is CM-UX-07's (P-3)
            if (_reminderSheet != null) _reminderSheet.Attach(regions);
        }

        public void Show()
        {
            _hideFx?.Finish(this);
            _shown = true;
            gameObject.SetActive(true);
            LayoutForViewport(Screen.safeArea, Screen.dpi);
            RegisterPin();
            RegisterDailyPin();
            RegisterReminderGear();
            RegisterAudioToggle();
        }

        public void SetRigCovered(bool covered)
        {
            _rigCovered = covered;
            RefreshRigVisibility();
        }

        private void RefreshRigVisibility() => _profileRig?.SetVisible(
            _shown && isActiveAndEnabled && !_rigCovered && !(_regions?.HasStackedModal ?? false));

        // Keep the frame and route paint for the Play dolly while the intro owns input.
        public void SuspendForIntro()
        {
            _hideFx?.Finish(this);
            _shown = false;
            if (_reminderSheet != null) _reminderSheet.Hide();
            UnregisterPin();
            UnregisterDailyPin();
            UnregisterReminderGear();
            UnregisterAudioToggle();
            RefreshRigVisibility();
        }

        public void Hide() => Hide(null);

        public void HideWithFade(BoardFx fx) => Hide(fx);

        private void Hide(BoardFx fx)
        {
            _hideFx?.Finish(this);
            _shown = false;
            RefreshRigVisibility();
            _dailyUnlockAttention = false;
            if (_dailyChip != null) _dailyChip.SetRingColour(Palette.CreamCard);
            if (_reminderSheet != null) _reminderSheet.Hide();
            UnregisterPin();
            UnregisterDailyPin();
            UnregisterReminderGear();
            UnregisterAudioToggle();
            if (fx == null || !gameObject.activeSelf) { gameObject.SetActive(false); return; }
            _hideFx = fx;
            var paint = GetComponentsInChildren<Graphic>(true);
            fx.Tween(this, .25f, progress =>
            {
                foreach (var graphic in paint)
                    if (graphic != null) graphic.canvasRenderer.SetAlpha(1f - progress);
                if (progress < 1f) return;
                gameObject.SetActive(false);
                foreach (var graphic in paint)
                    if (graphic != null) graphic.canvasRenderer.SetAlpha(1f);
                _hideFx = null;
            });
        }

        private void OnDestroy()
        {
            if (_regions != null) _regions.StackedModalChanged -= RefreshRigVisibility;
            ReleaseVignette();
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
            RefreshRigVisibility();
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
            RefreshRigVisibility();
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
                _regions.BindVisual(PinRegionId, _pin);
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

        // Teaser paint does not bypass the existing campaign unlock gate.
        private void RegisterDailyPin()
        {
            if (_dailyUnlocked && _regions != null && !_dailyRegistered)
            {
                _regions.Register(DailyPinRegionId, () => _dailyPinRectPx,
                    () => DailySelected?.Invoke(), DailyPinRegionPriority);
                _regions.BindVisual(DailyPinRegionId, _dailyPin);
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
                _regions.BindVisual(ReminderGearRegionId, _reminderGear);
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
                _regions.Register(AudioToggleRegionId, () => _audioToggleHitRectPx,
                    () => AudioEnabledChanged?.Invoke(!_audioEnabled),
                    AudioToggleRegionPriority);
                _regions.BindVisual(AudioToggleRegionId, _audioToggle);
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
            TypeScale.Apply(_title, TypeScale.Display, dpi);
            TypeScale.Apply(_titleCarve, TypeScale.Display, dpi);
            TypeScale.Apply(_dailyTally, TypeScale.Caption, dpi, body: true);
            TypeScale.Apply(_dailyStatus, TypeScale.Caption, dpi, body: true);
            TypeScale.Apply(_audioToggleLabel, TypeScale.Caption, dpi, body: true);
            bool hasDaily = _dailyPin != null;
            _pinRectPx = HomeLayout.PrimaryPinRect(safeArea, dpi, hasDaily);
            _playChip.LayoutFace(_pinRectPx, dpi);

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
            var aperture = Rect.MinMaxRect(
                (windowXMin - viewport.xMin) / viewport.width,
                (windowYMin - viewport.yMin) / viewport.height,
                (windowXMax - viewport.xMin) / viewport.width,
                (windowYMax - viewport.yMin) / viewport.height);
            float px = HudBands.PxPerDp(dpi);
            float underFrame = (windowYMin - _heroRectPx.yMin + 40f * px) / viewport.height;
            if (_vignetteSprite == null || _vignetteAperture != aperture
                || !Mathf.Approximately(_vignetteUnderFrame, underFrame))
            {
                ReleaseVignette();
                _vignetteAperture = aperture;
                _vignetteUnderFrame = underFrame;
                _vignetteSprite = HudShapeSprites.CreateHomeVignette(aperture, underFrame);
                _backdrop.sprite = _vignetteSprite;
            }
            ApplyPx(_backdrop.rectTransform, viewport);
            ApplyPx(_hero, _heroRectPx);
            float margin = HomeLayout.ShadowMarginDp * px;
            var outer = new Rect(_heroRectPx.x - margin, _heroRectPx.y - margin,
                _heroRectPx.width + margin * 2f, _heroRectPx.height + margin * 2f);
            ApplyPx(_heroShadow, outer);
            LayoutShadowEdge(_heroShadowEdges[0], Rect.MinMaxRect(outer.xMin, windowYMax,
                outer.xMax, outer.yMax), outer);
            LayoutShadowEdge(_heroShadowEdges[1], Rect.MinMaxRect(outer.xMin, outer.yMin,
                outer.xMax, windowYMin), outer);
            LayoutShadowEdge(_heroShadowEdges[2], Rect.MinMaxRect(outer.xMin, windowYMin,
                windowXMin, windowYMax), outer);
            LayoutShadowEdge(_heroShadowEdges[3], Rect.MinMaxRect(windowXMax, windowYMin,
                outer.xMax, windowYMax), outer);
            foreach (var edge in _heroShadowEdges) edge.pixelsPerUnitMultiplier = 1f / px;
            _titlePlaqueShadow.GetComponent<Image>().pixelsPerUnitMultiplier = 1f / px;
            float lampY = viewport.yMax - 11f * px;
            float lampX = safeArea.center.x;
            ApplyPx(_lampGlow, new Rect(lampX - 80f * px, lampY - 48f * px, 160f * px, 80f * px));
            ApplyPx(_lampCore, new Rect(lampX - 13f * px, lampY - 13f * px, 26f * px, 10f * px));
            ApplyPx(_lampCord, new Rect(lampX - px, lampY, 2f * px, viewport.yMax - lampY));
            ApplyPx(_lampShade, new Rect(lampX - 14f * px, lampY - 6f * px, 28f * px, 12f * px));
            var titlePlaque = HomeLayout.TitleRect(safeArea, dpi,
                _audioToggle != null, _reminderGear != null);
            ApplyPx(_titlePlaque, titlePlaque);
            ApplyPx(_titlePlaqueShadow, HomeLayout.TitleShadowRect(titlePlaque, dpi));
            _title.lineSpacing = _titleCarve.lineSpacing = -4f * px;
            _titleCarve.rectTransform.anchoredPosition = new Vector2(0f, -2f * px);
            for (int i = 0; i < _titleNails.Length; i++)
            {
                float x = i % 2 == 0 ? 10f * px : titlePlaque.width - 10f * px;
                float y = i < 2 ? 10f * px : titlePlaque.height - 10f * px;
                ApplyPx(_titleNails[i], new Rect(x - 3f * px, y - 3f * px, 6f * px, 6f * px));
            }
            ApplyPx(_titleCatMark, new Rect(-46f * px, 4f * px, 48f * px, 48f * px));

            // The two secondary routes retain their geometry across Daily unlock.
            if (_dailyPin != null)
            {
                _dailyPinRectPx = HomeLayout.DailyPinRect(safeArea, dpi);
                _dailyChip.LayoutFace(_dailyPinRectPx, dpi);
                var daily = _dailyPinRectPx;
                // Keep the measured icon/label group together above the seven progress pips.
                ((RectTransform)_dailyLabel.transform.parent).anchoredPosition = new Vector2(0, 4f * px);
                float pipStart = daily.width * 0.5f - 30f * px;
                for (int i = 0; i < 7; i++)
                    ApplyPx(_dailyPips[i].rectTransform,
                        new Rect(pipStart + i * 9f * px, 12f * px, 4f * px, 4f * px));
                var caption = new Rect(0f, -35f * px, daily.width, 34f * px);
                ApplyPx(_dailyTally.rectTransform, caption);
                ApplyPx(_dailyStatus.rectTransform, caption);
            }
            if (_reminderGear != null)
            {
                _reminderGearRectPx = HomeLayout.ReminderGearRect(safeArea, dpi);
                ApplyPx(_reminderGear, _reminderGearRectPx);
            }
            if (_audioToggle != null)
            {
                _audioToggleRectPx = HomeLayout.AudioToggleRect(safeArea, dpi);
                _audioToggleHitRectPx = HomeLayout.AudioToggleHitRect(safeArea, dpi);
                ApplyPx(_audioToggle, _audioToggleRectPx);
            }
            if (_reminderSheet != null && _reminderSheet.IsVisible)
                _reminderSheet.LayoutForViewport(safeArea, dpi);
            if (IsVisible)
                DioramaLaidOut?.Invoke(Rect.MinMaxRect(windowXMin, windowYMin, windowXMax, windowYMax));
            if (_profileRig != null && gameObject.activeInHierarchy)
            {
                // Show first sizes the hero; settle its anchored holder before the first mount.
                _hero.ForceUpdateRectTransforms();
                ((RectTransform)_profileRig.transform.parent).ForceUpdateRectTransforms();
                Canvas canvas = GetComponentInParent<Canvas>();
                RefreshRigVisibility();
                _profileRig.Layout(canvas != null ? canvas.worldCamera : null);
            }
        }

        private static void LayoutShadowEdge(Image edge, Rect screenRect, Rect outer)
        {
            screenRect.position -= outer.position;
            ApplyPx(edge.rectTransform, screenRect);
        }

        private void ReleaseVignette()
        {
            if (_vignetteSprite == null) return;
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                DestroyImmediate(_vignetteSprite.texture);
                DestroyImmediate(_vignetteSprite);
            }
            else
#endif
            {
                Destroy(_vignetteSprite.texture);
                Destroy(_vignetteSprite);
            }
            _vignetteSprite = null;
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
            if (_pin == null || !_shown) return;
            var fx = GetComponentInParent<CatMetro.Presentation.Fx.BoardFx>();
            if (fx != null && fx.IsAnimating(_pin)) return;
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
