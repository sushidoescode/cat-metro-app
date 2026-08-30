using System.Collections.Concurrent;
using System.Threading;
using CatMetro.Application.Retry;
using CatMetro.Application.Save;
using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Content.Daily;
using CatMetro.Integrations.OneSignal;
using CatMetro.Services;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Cameras;
using CatMetro.Presentation.Diagnostics;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Hud.WavePreview;
using CatMetro.Services;
using UnityEngine;

namespace CatMetro.Bootstrap
{
    // The composition root (ADR-0003's 10th row): loads the level THROUGH the StreamingAssets
    // seam, builds the engine-free session, wires Presentation, drives the tick loop with real
    // frame time — this Update is the only place wall-clock time enters, and it enters as a dt
    // argument. Presentation never simulates. Boots two ways: placed in the Game scene (Awake
    // self-initializes — the device path, CM-C2b review F4c) or via Launch()/LaunchWith()
    // (tests; CM-C3 review N1: the factory paths SUPPRESS Awake's self-init so nothing is ever
    // double-wired and Launch(levelPath) honours its argument).
    // CM-C3: on failure the screen state is FailureReview — the cause camera frames the causal
    // node (state-derived, A-C3-1), the reason-keyed LOCKED string renders with substitution,
    // and one tap on the thumb band retries by RE-SIMULATION from tick 0 (no scene load,
    // no snapshot — ADR-0002 §9).
    public sealed class GameRoot : MonoBehaviour
    {
        private static bool _factoryConstructing; // review N1

        public GameSession Session { get; private set; }
        public BoardView View { get; private set; }
        public Presentation.Input.TapInput Input { get; private set; }
        public FrameLog Log { get; private set; }
        public BannerView Banner { get; private set; }
        public CauseCameraController CauseCam { get; private set; }
        public WavePreviewStrip Preview { get; private set; }
        public Camera Cam { get; private set; }
        public string ScreenState { get; private set; } = "Playing";
        private GameAnalyticsRuntime _analyticsRuntime;
        private NetworkReachability _lastNetworkReachability;
        private bool _networkReachabilityKnown;
        public IAnalytics Analytics => _analyticsRuntime?.Sink;
        // Read-only identity handoff for a future personless server bridge. The current official
        // commerce-to-analytics bridge creates a Person, so the no-person-profiles release must
        // not set that customer attribute or enable that bridge without a separately verified fix.
        public string AnalyticsAnonymousId => _analyticsRuntime?.AnonymousId;

        // CM-LOADNEXT: read-only so tests/UI can observe progression without a second source of
        // truth for "what level is this." Null only before the first Wire() (never observable
        // through Launch/LaunchWith, which always Wire synchronously before returning).
        public string CurrentLevelId => _level?.Dto.Id;

        // CM-DAILYWIRE: the session marker — true from a successful SelectDaily() until
        // ReturnHomeFromDaily() (or a fresh campaign LoadNext, defensively). Read-only so the
        // ONLY place that can flip it is this file's own funnel logic, never a test/UI hand-set.
        public bool IsDailySession => _dailySession;
        private bool _dailySession;
        // The level that was active immediately before Daily was selected — restored by
        // ReturnHomeFromDaily() so nothing behind Home is a stale Won Daily session (the same
        // collision class CM-LOADNEXT's Known-debt entry names for the dev BootToHome flow).
        private ImportedLevel _preDailyLevel;

        // CM-DAILYWIRE criterion 9 (A-DL-6): surfaced, not persisted — null except immediately
        // after a Daily session's real Won transition, cleared on the next LoadLevel(). No
        // level (campaign or Daily) renders a ticket amount in pixels yet; this is a tested
        // data seam, not UI (see the frozen contract's Known-debt list).
        public int? DailyTicketsEarned { get; private set; }

        private SaveStore _saveStore;
        private CatMetro.Services.Cosmetics.CosmeticProfileService _cosmetics;
        private DailyProgressTracker _dailyProgress;
        private DailyReminderPreferences _dailyReminderPreferences;
        private IMessaging _messaging;
        private CancellationTokenSource _messagingPermissionCancellation;
        private System.Threading.Tasks.Task _messagingPermissionTask;
        private bool _messagingPermissionRequestInFlight;
        private int _messagingPermissionRequestGeneration;
        private bool _messagingListenerAttached;
        private bool _messagingOperationalFailure;
        private bool _dailyReminderProviderActive;
        private bool _settingsEnableIntentPending;
        private bool _foregroundPermissionRecheckPending;
        private bool _destroying;
        private bool _reminderPromptPending;
        private bool _checkReminderAfterHomePresentation;
        private readonly ConcurrentQueue<MessagingRoute> _pendingMessagingRoutes =
            new ConcurrentQueue<MessagingRoute>();
        private DailyLiveConfig _dailyLiveConfig;
        private DailyBoardCatalog _dailyCatalog;
        private DailyDateSelection _activeDailySelection;
        private bool _activeDailyPractice;
        private bool _dailyEntryUnlocked;
        private System.Threading.Tasks.Task<DailyFallbackResolution> _dailyFallbackTask;
        private CancellationTokenSource _dailyFallbackCancellation;
        private DailyDateSelection _pendingDailySelection;
        private string _pendingDailyDateKey;
        private bool _returnHomeAfterCampaignUnlock;
        private readonly System.Collections.Generic.Dictionary<string, ImportedLevel>
            _generatedDailyCache =
                new System.Collections.Generic.Dictionary<string, ImportedLevel>(
                    System.StringComparer.Ordinal);

        public int LifetimeDailyCompletions => _dailyProgress?.LifetimeCompletions ?? 0;
        public int DailyUnlockAfterCampaignCompletions =>
            (_dailyLiveConfig ?? DailyLiveConfig.ProductionDefault())
                .UnlockAfterCampaignCompletions;
        public string ActiveDailyDateKey => _activeDailySelection?.EffectiveDateKey ?? "";
        public bool DailyRunIsPractice => _dailySession && _activeDailyPractice;
        public string LastDailyBoardSource { get; private set; } = "";

        // CM-DAILYWIRE: the ONE ambient-clock read this contract adds — injectable exactly like
        // MotionOffToggle/AnimatorDurationScale (the P-3 injection style) so tests pin an exact
        // date without depending on wall-clock nondeterminism. Everything downstream of this one
        // call (DailyLineSeed, DailyBoardFactory, CorpusValidator, the solver) stays the same
        // pure functions #73 already tests.
        public System.Func<long> DailyClockUnixSeconds =
            () => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Injectable exactly like DailyClockUnixSeconds — the production default is the ONE
        // shipped IBoardFactory (DailyBoardFactory, Content/Daily's Q3-authorized owner); tests
        // substitute a controlled factory to prove criterion 7's loud-failure path without
        // depending on the real generator ever actually failing (it does not, across #73's own
        // 90-date proof).
        public System.Func<IBoardFactory> DailyFactory = () => new DailyBoardFactory();

        // CM-C3 A-C3-3: motion state = toggle OR OS animation scale zero. No save field; the
        // device wiring of the OS scale arrives with the settings screen (reads-only here).
        public bool MotionOffToggle;
        public float AnimatorDurationScale = 1f;
        public bool MotionOff => MotionOffToggle || AnimatorDurationScale == 0f;

        private ImportedLevel _level;
        private bool _halted;

        // CM-BOOT-HOME criterion 1: unfenced (was dev-only behind the retired BootToHome gate)
        // — a shipped build composes Home over every real boot (InitializeFromSeam, below), so
        // these properties must exist there too, not just in a DEVELOPMENT_BUILD/editor test.
        public CatMetro.Presentation.Screens.HomeScreenView Home { get; private set; }
        public CatMetro.Presentation.Screens.LevelIntroSheet Intro { get; private set; }
        public CatMetro.Presentation.Screens.WardrobeScreenView Wardrobe { get; private set; }
        public CatMetro.Presentation.Screens.ScreenStack Stack { get; private set; }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        public static System.Func<GameAnalyticsRuntime> AnalyticsRuntimeFactory;

        // CM-BOOT-HOME criterion 3: RETIRED as the compose gate (ComposeScreenFlow no longer
        // reads this — it is unconditional on real boot now, gated only by SkipHome() below).
        // Kept, dev-only, ONLY because out-of-scope LaunchWith-seam fixtures still reference it
        // (DailyWireTests.cs, DevBootOverrideTests.Precedence_*, and this file's own history) —
        // setting it no longer composes anything through LaunchWith (EDIT 2 moved the compose
        // call to InitializeFromSeam, which LaunchWith bypasses by construction), so those
        // fixtures need their own follow-up migration (reported, not silently fixed here).
        public static bool BootToHome;

        // CM-BOOT-HOME criterion 3: the inverted successor to BootToHome. Home now composes by
        // DEFAULT on every real boot (Launch()/Awake() -> InitializeFromSeam) — this is the
        // dev/test-only opt-OUT hatch so the Launch()-gameplay fixtures (FailureTests,
        // ChromeStateTests, DeviceConfigTests, GreyboxTests, GameRootWiringTests' halt tests)
        // keep booting straight to L001 gameplay with no screen in the way. Default false: a
        // real device build never touches this — the shipped default composes Home.
        public static bool DevSkipShippedHome;

        // Dev/capture force-on seam retained for focused tests. Shipped builds ignore it and use
        // the save-backed threshold from config/daily_live.json.
        public static bool DailyEntryUnlocked;

        // Test/dev seam for an isolated save root. Null in normal Editor play and absent from
        // shipped code paths; production always uses EngineStorageRoot.
        public static System.Func<IStorageRoot> DailyStorageRootOverride;

        // Test/dev seam for the provider-neutral runtime boundary. Production constructs the
        // concrete OneSignal adapter below; PlayMode tests inject a complete IMessaging fake.
        public static System.Func<IMessaging> MessagingFactoryOverride;

#endif

        // ReturnHomeFromDaily defers Home for the one-frame input lockout in shipped builds too.
        // -1 means there is no pending show.
        private int _pendingHomeShowFrame = -1;

        // CM-UX-07 criterion 2 / CM-BOOT-HOME criterion 1: true iff a screen (Home or
        // LevelIntro) currently shows — derived from the stack so there is one source of truth.
        // Unconditional now: a shipped build composes Home on every real boot (Stack is
        // non-null there by construction), and the LaunchWith-only gameplay fixtures never
        // compose at all (EDIT 2), so Stack stays null and this degenerates to false for them —
        // no #if/#else needed either way.
        public bool ScreensVisible => Stack != null && Stack.Count > 0;

        public static GameRoot Launch(string levelPath = "content/levels/L001.json")
        {
            _factoryConstructing = true;
            GameRoot root;
            try
            {
                var go = new GameObject("GameRoot");
                root = go.AddComponent<GameRoot>();
            }
            finally
            {
                _factoryConstructing = false;
            }
            root.InitializeFromSeam(levelPath);
            return root;
        }

        // Test seam for fixture boards (CM-C2b criterion 5's scripted overflow and CM-C3's
        // failure fixtures) — same wiring, no file.
        public static GameRoot LaunchWith(ImportedLevel level,
            GameAnalyticsRuntime analyticsRuntime = null)
        {
            _factoryConstructing = true;
            GameRoot root;
            try
            {
                var go = new GameObject("GameRoot");
                root = go.AddComponent<GameRoot>();
            }
            finally
            {
                _factoryConstructing = false;
            }
            root._analyticsRuntime = analyticsRuntime;
            root.Wire(level);
            if (analyticsRuntime != null)
                analyticsRuntime.BeginCampaignLevel(level, retry: false, fromScreen: "direct");
            return root;
        }

        private void Awake()
        {
            // Scene-boot path: a GameRoot placed in Game.unity self-initializes on device
            // (CM-C2b review F4c). Factory paths initialize explicitly instead (review N1).
            if (!_factoryConstructing && Session == null)
                InitializeFromSeam("content/levels/L001.json");
        }

        private void InitializeFromSeam(string levelPath)
        {
            if (Session != null) return;
            InitializeAnalytics();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // CM-DEVCAP3: evaluated BEFORE the level-override early-return so the boot-to-home
            // file's own read/parse/log side effects fire on either sub-path (dev level override
            // OR the shipped level) — CM-BOOT-HOME criterion 3 retired its RETURN VALUE as a
            // compose gate (Home composes unconditionally on real boot now, see SkipHome()
            // below), but the file is still read here so DevBootOverrideTests.cs's malformed-
            // file coverage (loud/quiet fallback logging) keeps exercising the real thing.
            DevCapture.DevBootOverride.ShouldBootToHome();
            var devLevel = DevCapture.DevLevelOverride.TryImport();
            if (devLevel != null)
            {
                Wire(devLevel);
                InitializeDailyLiveServices();
                InitializeCosmetics();
                // CM-BOOT-HOME criterion 1: compose BEFORE the early return — this dev-level
                // sub-path is still a REAL boot (SceneBoot/Launch(), never LaunchWith), so it
                // gets Home exactly like the shipped branch below, unless the dev skip hatch
                // opts out.
                if (!SkipHome()) ComposeScreenFlow();
                return;
            }
#endif
            var source = new StreamingAssetsContentSource();
            var bytes = source.ReadAsync(levelPath, CancellationToken.None).GetAwaiter().GetResult();
            var imported = LevelImporter.Import(bytes);
            if (!imported.Ok)
                throw new System.InvalidOperationException("level unusable: " + imported.Error);
            // Criterion 8's artifact line: proves the played level came through the seam.
            Debug.Log("SEAM_LOADED " + levelPath);
            Wire(imported.Value);
            InitializeDailyLiveServices();
            InitializeCosmetics();
            // CM-BOOT-HOME criterion 1 (the new shipped default): every real boot composes Home
            // over the just-wired level, unless the dev skip hatch opts out (SkipHome(), always
            // false in a shipped build). LaunchWith (the ~12 gameplay fixtures' seam) bypasses
            // InitializeFromSeam entirely and never reaches this line — they get NO Home.
            if (!SkipHome()) ComposeScreenFlow();
        }

        private void InitializeDailyLiveServices()
        {
            if (_dailyLiveConfig != null) return;
            _dailyLiveConfig = DailyLiveConfig.ProductionDefault();
            var source = new StreamingAssetsContentSource();

            try
            {
                var configBytes = source.ReadAsync(
                    DailyLiveConfig.RelativePath, CancellationToken.None).GetAwaiter().GetResult();
                var parsed = DailyLiveConfig.Parse(configBytes);
                if (parsed.Ok) _dailyLiveConfig = parsed.Value;
                else Debug.LogWarning("daily live config rejected; using production default: "
                    + parsed.Error);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("daily live config unavailable; using production default: "
                    + ex.Message);
            }

            try
            {
                var boundsBytes = source.ReadAsync(
                    "config/runtime_bounds.json", CancellationToken.None).GetAwaiter().GetResult();
                var parsedBounds = RuntimeBounds.Parse(boundsBytes);
                if (!parsedBounds.Ok)
                    throw new System.InvalidOperationException(parsedBounds.Error.ToString());
                IStorageRoot storage = CreateRuntimeStorageRoot();
                _saveStore = new SaveStore(storage, new RealSaveFileSystem(),
                    parsedBounds.Value, MigrationTable.CreateDefault());
                _saveStore.Load();
                SaveRuntime.Install(_saveStore);
                _dailyProgress = new DailyProgressTracker(_saveStore);
                _dailyReminderPreferences = new DailyReminderPreferences(_saveStore);
                _reminderPromptPending = _dailyReminderPreferences.CanOfferPrompt(
                    _dailyProgress.LifetimeCompletions);
            }
            catch (System.Exception ex)
            {
                // Campaign remains playable. A threshold of zero still exposes Daily as
                // practice, but no completion is claimed without durable storage.
                Debug.LogError("daily progress unavailable; continuing without persistence: "
                    + ex.Message);
                _saveStore = null;
                _dailyProgress = null;
                _dailyReminderPreferences = null;
            }

            InitializeMessaging();

            try
            {
                var catalogBytes = source.ReadAsync(
                    DailyBoardCatalog.RelativePath, CancellationToken.None)
                    .GetAwaiter().GetResult();
                var loaded = DailyBoardCatalog.LoadShipped(
                    catalogBytes, DailyRuntimeInputs.ShippedPipelineConfig);
                if (loaded.Ok) _dailyCatalog = loaded.Value;
                else Debug.LogWarning(loaded.Detail + "; runtime fallback armed");
            }
            catch (System.Exception ex)
            {
                // The deterministic generator is the deliberate graceful fallback.
                Debug.LogWarning("daily precomputed catalog unavailable; runtime fallback armed: "
                    + ex.Message);
                _dailyCatalog = null;
            }
        }

        private void InitializeCosmetics()
        {
            if (_cosmetics != null) return;
            _cosmetics = CosmeticComposition.Create(
                _saveStore, CatMetro.Services.Purchases.PurchaseRuntime.Current);
            CatMetro.Services.Cosmetics.CosmeticRuntime.Install(_cosmetics);
        }

        private void InitializeMessaging()
        {
            if (_messaging != null || _destroying) return;

            OneSignalMessaging productionMessaging = null;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (MessagingFactoryOverride != null)
            {
                try
                {
                    _messaging = MessagingFactoryOverride();
                }
                catch (System.Exception ex)
                {
                    _messagingOperationalFailure = true;
                    Debug.LogWarning("daily reminder provider factory failed: " + ex.Message);
                }
            }
#endif
            if (_messaging == null)
            {
                productionMessaging = new OneSignalMessaging();
                _messaging = productionMessaging;
            }

            _messagingPermissionCancellation = new CancellationTokenSource();
            try
            {
                // Subscribe before OneSignal initialization so a cold-launch click delivered
                // synchronously by the SDK is retained for the first main-thread Update.
                _messaging.LinkOpened += QueueMessagingRoute;
                _messagingListenerAttached = true;
                productionMessaging?.Initialize(OneSignalRuntimeConfig.LoadAppId());
            }
            catch (System.Exception ex)
            {
                _messagingOperationalFailure = true;
                Debug.LogWarning("daily reminder provider initialization failed: " + ex.Message);
            }

            ReconcileReminderProvider();
        }

        private IStorageRoot CreateRuntimeStorageRoot()
        {
#if UNITY_EDITOR
            if (DailyStorageRootOverride != null) return DailyStorageRootOverride();
            // Batch tests must never mutate a developer's real profile. Interactive Editor and
            // every shipped player use persistentDataPath through EngineStorageRoot.
            if (UnityEngine.Application.isBatchMode) return new BatchStorageRoot();
#endif
            return new EngineStorageRoot();
        }

#if UNITY_EDITOR
        private sealed class BatchStorageRoot : IStorageRoot
        {
            public string SaveDirectory { get; }
            public string CacheDirectory => SaveDirectory;

            public BatchStorageRoot()
            {
                SaveDirectory = System.IO.Path.Combine(UnityEngine.Application.temporaryCachePath,
                    "catmetro-daily-tests-" + System.Guid.NewGuid().ToString("N"));
                System.IO.Directory.CreateDirectory(SaveDirectory);
            }
        }
#endif

        private void InitializeAnalytics()
        {
            if (_analyticsRuntime != null) return;
#if UNITY_EDITOR
            if (AnalyticsRuntimeFactory != null)
                _analyticsRuntime = AnalyticsRuntimeFactory();
#endif
            if (_analyticsRuntime == null)
                _analyticsRuntime = GameAnalyticsRuntime.CreateProduction();
            _lastNetworkReachability = UnityEngine.Application.internetReachability;
            _networkReachabilityKnown = true;
        }

        // CM-BOOT-HOME criterion 3: true only in a dev/test build that explicitly opts OUT of
        // the shipped Home screen (DevSkipShippedHome, the inverted BootToHome successor) —
        // always false in a shipped (non-dev, non-editor) build, so a real device ALWAYS
        // composes Home on boot.
        private static bool SkipHome()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            return DevSkipShippedHome;
#else
            return false;
#endif
        }

        private void Wire(ImportedLevel level)
        {
            FramePolicy.Apply(); // CM-C2b-DEVFIX criterion 3: every boot path passes through here
            _level = level;
            Session = new GameSession(level);

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            View = BoardView.Build(level, transform, Session);
            // LOOK steps 4-5: the camera stays axis-aligned so the existing screen-space
            // input/failure geometry remains exact; the complete board diorama is tilted as
            // one presentation space, then framed from its actual renderer bounds.
            BoardSceneLook.Apply(transform, Cam, View);
            CauseCam = camGo.AddComponent<CauseCameraController>();
            CauseCam.Wire(Cam, -View.transform.forward); // captures the fitted play pose
            // CM-UX-07 criterion 3 (#36 F1/F5): a Wire-only binding dies at first Retry, since
            // Retry rebuilds View — bound again there too.
            View.MotionOffSource = () => MotionOff;
            Input = gameObject.AddComponent<Presentation.Input.TapInput>();
            Input.Wire(Session, View, Cam);
            Input.RetryRegionActive = () => ScreenState == "FailureReview";
            Input.RetryTapped = Retry;
            // CM-UX-07 criterion 2: the board-input gate. F7 (round-1 review) correction: this
            // is NOT behavior-unchanged in shipped boot as a whole — previously BoardInputActive
            // was null (TapInput treats null as always-active), so discs resolved even during
            // Won/FailureReview/Halted; the "ScreenState == Playing" term newly closes them
            // there (that is criterion 2's point). CM-BOOT-HOME update: the "!ScreensVisible"
            // term is LIVE in shipped boot now too (Home composes by default there, criterion 1)
            // — it degenerates to false only on the LaunchWith-only gameplay-fixture seam, which
            // never composes a screen flow at all (EDIT 2).
            Input.BoardInputActive = () => ScreenState == "Playing" && !ScreensVisible;
            Banner = BannerView.Create(transform);
            Preview = WavePreviewStrip.Create(transform, Session, Cam);
            BindPreviewCatMotion();
            // HUD-WAVE: the preview hides itself on Won/FailureReview. Explicit state binding,
            // NOT z-order — the banner happens to sort above the HUD, but two views owned by
            // different lanes must not depend on that to stay correct.
            Preview.BindScreenState(() => ScreenState);
            Log = gameObject.AddComponent<FrameLog>();
            Log.SimTickSource = () => Session.State.Tick;
            Log.ScreenStateSource = () => ScreenState;

            // CM-UX-07 criterion 1: chrome + hint attach to root.gameObject. F8 (round-1 review)
            // correction: the camera is NOT on root.gameObject itself — it lives on a child
            // GameObject named "Camera" (built above at GameRoot.cs:141-143); each controller
            // resolves it via GetComponentInChildren<Camera>() (the self-resolve pattern).
            // Regression pin: sortingOrder 100/90 unchanged.
            // #46 review F5: guarded the same way the dev-only capture attach below is guarded
            // (existence-checked before AddComponent) — a pre-existing controller (e.g. attached
            // before Wire runs, the scene-boot path) survives as the single instance instead of
            // stacking a duplicate under it.
            if (GetComponent<ScreenChromeController>() == null)
                gameObject.AddComponent<ScreenChromeController>();
            var chrome = GetComponent<ScreenChromeController>();
            chrome.Attach(() => ScreenState);
            if (GetComponent<HintChipController>() == null)
                gameObject.AddComponent<HintChipController>();
            var hint = GetComponent<HintChipController>();
            hint.Attach(() => ScreenState);
            // Criterion 7 (CM-UX-07): Retry() never calls hint.ResetForNewLevel() (the
            // same-level law) — LoadNext() (CM-LOADNEXT, below) is the call site the CM-UX-05
            // handoff named ("wherever a NEW level loads", state/handoffs/CM-UX-05.md).

            // CM-LOADNEXT criterion 1 (Q-3 discharged): the single attach line — ResultsPanel
            // was NEEDS-WIRING until progression existed to give its Next CTA somewhere to go
            // (CM-UX-07 held this per the human's Q-3 ruling, state/handoffs/CM-UX-04.md:43).
            // Guarded like chrome/hint (#46 review F5 guard style) so a pre-attached instance
            // (the scene-boot precedent) survives Wire as the single instance, never a stacked
            // duplicate.
            if (GetComponent<ResultsPanel>() == null)
                gameObject.AddComponent<ResultsPanel>();
            var results = GetComponent<ResultsPanel>();
            results.Attach(() => ScreenState, Input.Regions);
            // CM-DAILYWIRE: the seam now routes through a small router instead of binding
            // LoadNext directly — a Daily win must never reach campaign progression.
            results.NextRequested = OnResultsCtaRequested;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (GetComponent<DevCapture.DevFrameCapture>() == null)
                gameObject.AddComponent<DevCapture.DevFrameCapture>().Wire(this);
#endif
        }

        // CM-UX-07 criterion 6 / CM-BOOT-HOME criterion 1: ONE ScreensCanvas (ScreenSpaceCamera
        // on Cam, sortingOrder 120 — above the CM-UX-04 results canvas at 110) hosting Home +
        // LevelIntro. Deliveries/name come from the already-loaded level — no new I/O.
        // ScreensVisible reads the stack, so there is exactly one source of truth for "a screen
        // is up". CM-BOOT-HOME: unfenced (renamed from ComposeDevScreenFlow) — this now runs on
        // EVERY real boot, shipped included (criterion 1), so it may reference only already-
        // shipped Presentation.Screens types + Canvas/Camera — no Newtonsoft, no DevCapture
        // (verified: this method touches none).
        private void ComposeScreenFlow()
        {
            var canvasGo = new GameObject("ScreensCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Cam;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 120;

            Stack = new CatMetro.Presentation.Screens.ScreenStack();
            bool savedProgressUnlock = DailyUnlockAfterCampaignCompletions == 0
                || (_dailyProgress != null && _dailyProgress.IsDailyUnlocked(
                    DailyUnlockAfterCampaignCompletions));
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // Existing capture/tests may force the surface. Production progress still flows
            // through the same data threshold in Development builds.
            bool dailyUnlocked = DailyEntryUnlocked || savedProgressUnlock;
#else
            bool dailyUnlocked = savedProgressUnlock;
#endif
            _dailyEntryUnlocked = dailyUnlocked;
            Home = CatMetro.Presentation.Screens.HomeScreenView.Create(
                canvasGo.transform, dailyUnlocked, LifetimeDailyCompletions, _cosmetics);
            Home.Attach(Input.Regions, () => MotionOff);
            Home.ReminderAccepted = BeginEnableDailyReminder;
            Home.ReminderDismissed = ConfigureReminderHome;
            Home.ReminderEnabledChanged = OnReminderEnabledChanged;
            Home.ReminderSlotChanged = OnReminderSlotChanged;
            ConfigureReminderHome();
            Intro = CatMetro.Presentation.Screens.LevelIntroSheet.Create(canvasGo.transform);
            Intro.Attach(Input.Regions);
            Wardrobe = CatMetro.Presentation.Screens.WardrobeScreenView.Create(
                canvasGo.transform,
                CatMetro.Services.Purchases.PurchaseRuntime.Current,
                _cosmetics,
                new CatMetro.Services.Cosmetics.DisabledCosmeticRewardedRoute());
            Wardrobe.Attach(Input.Regions);

            Home.LevelSelected = () =>
            {
                CancelPendingDailyFallback();
                // A stack push navigates OFF Home (ScreenStack's own navigation law — only the
                // top of the stack is current): Home.Hide() also unregisters its pin, which
                // would otherwise tie-break ahead of Intro's Play chip (both center in the
                // thumb band at the identical point — the earliest registration wins ties).
                Wardrobe.Hide();
                Home.Hide();
                Intro.Show(_level.Dto.Name, _level.Dto.Win.Deliveries);
                Stack.Push("intro");
            };
            // CM-DAILYWIRE: the Daily entry — no Intro sheet (the pin's own label is
            // self-explanatory; unlike campaign levels this is not a fresh unnamed board), so
            // SelectDaily() itself hides Home and clears the stack straight into gameplay on a
            // successful resolve; on failure it returns without touching Home at all
            // (criterion 7 — loud, never silent, never a half-shown screen). SelectDaily is a
            // public, unfenced method. The pin is constructed only when the saved/configured
            // gate opens, at boot or after a threshold-crossing campaign win.
            Home.DailySelected = SelectDaily;
            Intro.PlayRequested = () =>
            {
                Intro.Hide();
                Wardrobe.Hide();
                Home.Hide(); // idempotent — already hidden by the push above
                while (Stack.TryPop(out _)) { }
                _analyticsRuntime?.BeginCampaignLevel(_level, retry: false,
                    fromScreen: "intro");
            };
            Wardrobe.OpenRequested = () =>
            {
                Home.Hide();
                Wardrobe.Open();
                Stack.Push("wardrobe");
            };
            Wardrobe.BackRequested = () =>
            {
                Wardrobe.ShowEntry();
                Home.Show();
                if (Stack.Current == "wardrobe") Stack.TryPop(out _);
            };

            ShowHomeForPresentation();
        }

        private void ShowHomeForPresentation()
        {
            if (Home == null || Stack == null) return;
            Home.Show();
            Wardrobe.ShowEntry();
            Stack.Push("home");
            _checkReminderAfterHomePresentation = true;
        }

        private void ConfigureReminderHome()
        {
            if (Home == null || _dailyReminderPreferences == null
                || LifetimeDailyCompletions <= 0)
                return;

            ReadMessagingState(out bool available, out MessagingPermission permission,
                out bool canRequestPermission);
            bool effectiveEnabled = _dailyReminderPreferences.Enabled
                && available && permission == MessagingPermission.Authorized;
            Home.ConfigureReminder(configurationUnlocked: true, effectiveEnabled,
                _dailyReminderPreferences.Slot, permission, canRequestPermission, available);
        }

        private void TryPresentEarnedReminderPrompt()
        {
            if (!_reminderPromptPending || Home == null || !Home.IsVisible
                || Stack == null || !string.Equals(Stack.Current, "home",
                    System.StringComparison.Ordinal)
                || _dailyReminderPreferences == null)
                return;

            ReadMessagingState(out bool available, out _, out _);
            if (!available) return;
            if (!_dailyReminderPreferences.CanOfferPrompt(LifetimeDailyCompletions))
            {
                _reminderPromptPending = false;
                return;
            }

            // One attempt per earned Home presentation. A failed save must not paint a prompt
            // that can repeat, and must not hammer persistence from Update every frame. A later
            // process derives eligibility again from the still-unseen durable state.
            _reminderPromptPending = false;
            if (!_dailyReminderPreferences.TryMarkPromptSeen())
            {
                ConfigureReminderHome();
                return;
            }

            ConfigureReminderHome();
            Home.ShowReminderPrompt();
        }

        private void OnReminderEnabledChanged(bool enabled)
        {
            if (enabled) BeginEnableDailyReminder();
            else
            {
                SupersedePermissionRequest();
                ClearSettingsEnableIntent();
                ApplyPlayerReminderEnabled(false);
            }
        }

        private void BeginEnableDailyReminder()
        {
            if (_destroying || _dailyReminderPreferences == null || _messaging == null)
                return;

            ReadMessagingState(out bool available, out MessagingPermission permission,
                out bool canRequestPermission);
            if (!available)
            {
                ConfigureReminderHome();
                return;
            }
            if (permission == MessagingPermission.Authorized)
            {
                SupersedePermissionRequest();
                ClearSettingsEnableIntent();
                ApplyPlayerReminderEnabled(true);
                return;
            }
            if (_messagingPermissionRequestInFlight) return;

            var cancellation = _messagingPermissionCancellation;
            if (cancellation == null || cancellation.IsCancellationRequested) return;
            bool fallbackToSettings = !canRequestPermission
                && permission != MessagingPermission.Authorized;
            if (fallbackToSettings)
            {
                _settingsEnableIntentPending = true;
                _foregroundPermissionRecheckPending = false;
            }
            else
            {
                ClearSettingsEnableIntent();
            }
            int requestGeneration = ++_messagingPermissionRequestGeneration;
            _messagingPermissionRequestInFlight = true;
            _messagingPermissionTask = RequestReminderPermissionAsync(
                fallbackToSettings, cancellation.Token, requestGeneration);
        }

        private async System.Threading.Tasks.Task RequestReminderPermissionAsync(
            bool fallbackToSettings, CancellationToken cancellationToken,
            int requestGeneration)
        {
            try
            {
                MessagingPermission result = await _messaging.PromptAsync(
                    fallbackToSettings, cancellationToken);
                if (_destroying || cancellationToken.IsCancellationRequested
                    || requestGeneration != _messagingPermissionRequestGeneration)
                    return;
                if (fallbackToSettings)
                {
                    // Opening OS settings may complete before the player changes permission.
                    // Retain this explicit enable intent for the next foreground callback unless
                    // focus reconciliation (or explicit Off) already consumed it.
                    if (!_settingsEnableIntentPending) return;
                    if (result == MessagingPermission.Authorized)
                    {
                        if (ApplyPlayerReminderEnabled(true))
                            ClearSettingsEnableIntent();
                    }
                    else
                    {
                        ApplyPlayerReminderEnabled(false);
                    }
                }
                else
                {
                    ClearSettingsEnableIntent();
                    ApplyPlayerReminderEnabled(result == MessagingPermission.Authorized);
                }
            }
            catch (System.OperationCanceledException)
            {
                // Destruction owns cancellation; no UI/save/provider mutation follows it.
                if (!_destroying
                    && requestGeneration == _messagingPermissionRequestGeneration)
                    ClearSettingsEnableIntent();
            }
            catch (System.Exception ex)
            {
                if (!_destroying
                    && requestGeneration == _messagingPermissionRequestGeneration)
                {
                    ClearSettingsEnableIntent();
                    _messagingOperationalFailure = true;
                    Debug.LogWarning("daily reminder permission request failed: " + ex.Message);
                }
            }
            finally
            {
                // Generation supersession makes this completion logically stale, but it does
                // not end the provider's physical PromptAsync. Keep the duplicate-request guard
                // until whichever request actually owns it has settled.
                _messagingPermissionRequestInFlight = false;
                if (requestGeneration == _messagingPermissionRequestGeneration)
                {
                    if (!_destroying) ConfigureReminderHome();
                }
            }
        }

        private bool ApplyPlayerReminderEnabled(bool enabled)
        {
            if (_destroying || _dailyReminderPreferences == null) return false;
            if (!_dailyReminderPreferences.TrySetEnabled(enabled))
            {
                ConfigureReminderHome();
                return false;
            }

            if (enabled)
                ScheduleDailyReminder(_dailyReminderPreferences.Slot);
            else
                CancelDailyReminder();
            ConfigureReminderHome();
            return true;
        }

        private void ClearSettingsEnableIntent()
        {
            _settingsEnableIntentPending = false;
        }

        private void SupersedePermissionRequest()
        {
            _messagingPermissionRequestGeneration++;
        }

        private void QueueForegroundPermissionRecheck()
        {
            if (_destroying || (!_settingsEnableIntentPending
                && !(_dailyReminderPreferences?.Enabled ?? false)))
                return;
            _foregroundPermissionRecheckPending = true;
        }

        private void PumpForegroundPermissionRecheck()
        {
            if (!_foregroundPermissionRecheckPending) return;
            _foregroundPermissionRecheckPending = false;
            if (_destroying) return;

            bool recoveringFromProviderFailure = _messagingOperationalFailure;
            ReadMessagingState(out bool available, out MessagingPermission permission, out _,
                allowOperationalRecovery: true);
            if (_settingsEnableIntentPending)
            {
                if (available && permission == MessagingPermission.Authorized)
                {
                    if (ApplyPlayerReminderEnabled(true))
                    {
                        SupersedePermissionRequest();
                        ClearSettingsEnableIntent();
                    }
                    return;
                }

                // A focus callback is a one-shot observation, not a persistence/provider poll.
                // Keep the explicit settings intent for a later real foreground transition.
                ConfigureReminderHome();
                return;
            }

            if (_dailyReminderPreferences?.Enabled == true && available)
            {
                if (permission == MessagingPermission.Authorized)
                {
                    if (!_dailyReminderProviderActive || recoveringFromProviderFailure)
                        ScheduleDailyReminder(_dailyReminderPreferences.Slot);
                }
                else if (_dailyReminderProviderActive || recoveringFromProviderFailure)
                {
                    // OS permission is authoritative for delivery. Keep the player's durable
                    // choice, but exit the Journey until permission becomes authorized again.
                    CancelDailyReminder();
                }
            }
            ConfigureReminderHome();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) QueueForegroundPermissionRecheck();
            if (hasFocus) _analyticsRuntime?.OnForeground();
            else _analyticsRuntime?.OnBackground();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus) QueueForegroundPermissionRecheck();
            if (pauseStatus)
            {
                try
                {
                    _saveStore?.TryCommitOnPause();
                }
                catch (System.Exception ex)
                {
                    string errorType = ex.GetType().Name;
                    _saveStore?.Report("error_caught",
                        "domain=save_pause detail=" + errorType);
                    Debug.LogError("save pause commit failed safely: " + errorType);
                }
                finally
                {
                    _analyticsRuntime?.OnBackground();
                }
            }
            else _analyticsRuntime?.OnForeground();
        }

        private void OnReminderSlotChanged(DailyReminderSlot slot)
        {
            if (_destroying || _dailyReminderPreferences == null || slot == null) return;
            if (!_dailyReminderPreferences.TrySetSlot(slot))
            {
                ConfigureReminderHome();
                return;
            }

            ReadMessagingState(out bool available, out MessagingPermission permission, out _);
            if (_dailyReminderPreferences.Enabled && available
                && permission == MessagingPermission.Authorized)
                ScheduleDailyReminder(_dailyReminderPreferences.Slot);
            ConfigureReminderHome();
        }

        private void ReconcileReminderProvider()
        {
            if (_messaging == null) return;
            ReadMessagingState(out bool available, out MessagingPermission permission, out _);
            if (!available) return;

            if (_dailyReminderPreferences != null && _dailyReminderPreferences.Enabled
                && permission == MessagingPermission.Authorized)
                ScheduleDailyReminder(_dailyReminderPreferences.Slot);
            else
                CancelDailyReminder();
        }

        private void ScheduleDailyReminder(DailyReminderSlot slot)
        {
            try
            {
                _messaging?.Schedule(DailyChallengeNotification.Create(
                    slot ?? DailyReminderSlot.Morning));
                _dailyReminderProviderActive = _messaging != null;
                _messagingOperationalFailure = false;
            }
            catch (System.Exception ex)
            {
                _messagingOperationalFailure = true;
                Debug.LogWarning("daily reminder schedule failed: " + ex.Message);
            }
        }

        private void CancelDailyReminder()
        {
            try
            {
                _messaging?.Cancel("daily-ready");
                _dailyReminderProviderActive = false;
                _messagingOperationalFailure = false;
            }
            catch (System.Exception ex)
            {
                _messagingOperationalFailure = true;
                Debug.LogWarning("daily reminder cancel failed: " + ex.Message);
            }
        }

        private void ReadMessagingState(out bool available,
            out MessagingPermission permission, out bool canRequestPermission,
            bool allowOperationalRecovery = false)
        {
            available = false;
            permission = MessagingPermission.Unknown;
            canRequestPermission = false;
            if (_messaging == null
                || (_messagingOperationalFailure && !allowOperationalRecovery))
                return;

            try
            {
                available = _messaging.IsAvailable;
                if (!available) return;
                permission = _messaging.Permission;
                canRequestPermission = _messaging.CanRequestPermission;
            }
            catch (System.Exception ex)
            {
                _messagingOperationalFailure = true;
                available = false;
                permission = MessagingPermission.Unknown;
                canRequestPermission = false;
                Debug.LogWarning("daily reminder provider state unavailable: " + ex.Message);
            }
        }

        private void QueueMessagingRoute(MessagingRoute route)
        {
            // SDK callbacks may arrive off-thread. This handler deliberately touches no Unity
            // object; SelectDaily remains on the main loop and owns date/practice policy.
            if (route == MessagingRoute.Daily) _pendingMessagingRoutes.Enqueue(route);
        }

        private void PumpMessagingRoutes()
        {
            while (_pendingMessagingRoutes.TryDequeue(out MessagingRoute route))
                if (route == MessagingRoute.Daily) SelectDaily();
        }

        // CM-C3 criterion 10's reason→key mapping, PURE and test-drivable (review S1): the
        // PlatformOverflow branch is the ELSE — no shipped code names the pinned enum member
        // (the [CI] grep enforces that), yet the day Q-J unpins it, the correct LOCKED string
        // renders with the {station} substitution instead of a wrong banner.
        public static (string key, string token) FailKey(CatMetro.Domain.FailReason reason)
        {
            if (reason == CatMetro.Domain.FailReason.QueueOverflow)
                return ("fail.queueoverflow", "{node}");
            if (reason == CatMetro.Domain.FailReason.TimeOut)
                return ("fail.banner.timeout", null);
            return ("fail.platformoverflow", "{station}");
        }

        // CM-LOADNEXT: the campaign band, in play order — the level-progression POLICY (save-
        // backed unlocks, campaign gating) is explicitly deferred beyond this contract
        // (docs/ux/ux-layer-decompose.md §5), so this stays a plain ordered list, not a graph.
        // ASSUMPTION under human review (state/handoffs/CM-LOADNEXT-frozen-contract.md, A-LN-1):
        // the band WRAPS at the end back to L001 (a demo-friendly infinite loop) rather than
        // stopping — WrapAtEndOfBand is the one seam to flip if that assumption is overridden.
        public static readonly string[] LevelBand = {
            "L001", "L002", "L003", "L004", "L005",
            "L006", "L007", "L008", "L009", "L010",
            "L011", "L012", "L013", "L014", "L015", "L016", "L017",
            "L018", "L019",
        };
        private const bool WrapAtEndOfBand = true;

        // Pure and test-drivable (the FailKey precedent, above) — no Unity object needed. An id
        // outside the band (a dev-override or test-fixture id, A-LN-2) restarts the band at its
        // first level rather than crashing progression.
        public static string NextLevelId(string currentId)
        {
            int idx = System.Array.IndexOf(LevelBand, currentId);
            if (idx < 0) return LevelBand[0];
            int next = idx + 1;
            if (next >= LevelBand.Length)
                return WrapAtEndOfBand ? LevelBand[0] : LevelBand[LevelBand.Length - 1];
            return LevelBand[next];
        }

        public static string LevelPath(string levelId) => "content/levels/" + levelId + ".json";

        // CM-C3 criteria 8/9 (Retry) + CM-LOADNEXT (LoadNext): the shared rebuild both callers
        // use — fresh session over the GIVEN level (SAME level for Retry, a NEWLY IMPORTED one
        // for LoadNext); zero scene loads; the board view rebuilds; every switch back at
        // initialRoute by construction (ADR-0002 §9's "no scene load, no snapshot" holds either
        // way — only the DATA behind Session/View changes, never the Unity scene).
        private void LoadLevel(ImportedLevel level)
        {
            // A navigation or retry supersedes any off-thread Daily fallback. The pure worker
            // may already be inside the solver, but its cancellation token prevents its result
            // from being installed over the newer navigation state.
            CancelPendingDailyFallback();
            // CM-UX-07 criterion 4 (Q-2): idempotent — a normal FailureReview retry (or a fresh
            // LoadNext from Won) never registered "halt.escape", so this is a harmless no-op
            // there (Unregister returns false without throwing); a halt-escape retry clears the
            // region it just consumed.
            Input.Regions.Unregister("halt.escape");
            // PR #57 round-1 review F1: the SAME idempotent treatment for "results.next" —
            // proactively unregistered HERE rather than left to ResultsPanel's own next
            // Update()/Apply() poll. Component Update order is undefined, and LoadNext's
            // synchronous, main-thread ReadBlocking widens the window further: without this
            // line, a same-frame second tap (no yield between the two — see
            // LoadNextTests.DoubleTap_SameFrame_NoYieldBetween_DoesNotSkipALevel) can re-resolve
            // the STALE region and invoke NextRequested again, skipping a band level.
            Input.Regions.Unregister(ResultsPanel.RegionId);
            // CM-DAILYWIRE criterion 9: cleared on every level load (Retry/LoadNext/Daily
            // select/return-home alike) — the ONLY place this becomes non-null again is a
            // real Daily Won transition in Update(), below.
            DailyTicketsEarned = null;
            // Any new level load cancels a stale pending
            // Home-show. Without this, a rapid return-home -> re-select-Daily sequence (no
            // yield in between — SelectDaily_IsDeterministic_... exercises exactly this
            // shape) would leave the FIRST return-home's deferred show armed while a SECOND
            // Daily session is now in progress; Update() would later show Home uninvited
            // mid-session the first time enough frames pass. SelectDaily's own LoadLevel call
            // clears it here BEFORE SelectDaily arms nothing (SelectDaily never shows Home at
            // all); ReturnHomeFromDaily's LoadLevel call clears it here too, before
            // ReturnHomeFromDaily arms its OWN fresh value immediately afterward — so the
            // ordering never fights itself.
            _pendingHomeShowFrame = -1;
            _level = level;
            Session = new GameSession(level);
            if (View != null) Destroy(View.gameObject);
            View = BoardView.Build(level, transform, Session);
            BoardSceneLook.Apply(transform, Cam, View);
            CauseCam.CapturePlayPose(-View.transform.forward);
            View.MotionOffSource = () => MotionOff; // criterion 3: rebind on the REBUILT view
            Input.Wire(Session, View, Cam);
            if (Preview != null) Destroy(Preview.gameObject);
            Preview = WavePreviewStrip.Create(transform, Session, Cam);
            BindPreviewCatMotion();
            Preview.BindScreenState(() => ScreenState); // rebind on the REBUILT preview
            Banner.Hide();
            CauseCam.Reset(); // clears the ring AND restores this level's fitted play pose
            _halted = false;
            ScreenState = "Playing";
        }

        // Preview faces are a fixed pool. Discover them only when a new preview is composed,
        // never in the frame loop, so every face follows the same accessibility source as the
        // board view without adding a WavePreviewStrip dependency or changing its refresh path.
        private void BindPreviewCatMotion()
        {
            if (Preview == null) return;
            foreach (var face in Preview.GetComponentsInChildren<CatFaceView>(true))
                face.BindMotionOff(() => MotionOff);
        }

        public void Retry()
        {
            if (Session == null) return;
            LoadLevel(_level);
            _analyticsRuntime?.RetryLevel(_level, _dailySession);
        }

        // CM-LOADNEXT: the NextRequested seam's Bootstrap-owned half (CM-UX-04 criterion 5 —
        // "level advance is Bootstrap-owned... does not exist yet"; it exists now). Reads the
        // NEXT level through the real StreamingAssets seam (the InitializeFromSeam pattern,
        // without the dev-only override branches — progression is a runtime gameplay action,
        // never a boot decision, Q-5) and rebuilds through the SAME LoadLevel() Retry() uses, so
        // a new level plays exactly like a freshly booted one. A-LN-3: no ScreenState guard of
        // its own (only Session != null, Retry's own shape) — gating which UI can reach this
        // lives at the registration layer (ResultsPanel), not inside the action method.
        public void LoadNext()
        {
            if (Session == null) return;
            // CM-DAILYWIRE: defensive — campaign progression must never run inside a Daily
            // session. Unreachable via any wired UI (OnResultsCtaRequested routes Daily to
            // ReturnHomeFromDaily instead), but LoadNext is a public method any caller could
            // invoke directly (the same permissive shape Retry/LoadNext already had before this
            // contract, A-LN-3) — a second, independent guard layer.
            _dailySession = false;
            _activeDailySelection = null;
            _activeDailyPractice = false;
            string nextPath = LevelPath(NextLevelId(_level.Dto.Id));
            var source = new StreamingAssetsContentSource();
            var bytes = source.ReadAsync(nextPath, CancellationToken.None).GetAwaiter().GetResult();
            var imported = LevelImporter.Import(bytes);
            if (!imported.Ok)
                throw new System.InvalidOperationException("level unusable: " + imported.Error);
            // The SEAM_LOADED artifact line: proves the played level came through the seam
            // (InitializeFromSeam's own precedent, GameRoot.cs above).
            Debug.Log("SEAM_LOADED " + nextPath);
            LoadLevel(imported.Value);
            _analyticsRuntime?.BeginCampaignLevel(_level, retry: false,
                fromScreen: "results");
            // CM-UX-05 forward obligation (state/handoffs/CM-UX-05.md): a NEW level resets the
            // per-level hint attempt-run; Retry() of the SAME level must not (that accumulation
            // is the mechanic) — LoadLevel() stays silent on this by design so Retry() keeps its
            // pinned behavior; LoadNext is the one caller that speaks.
            var hint = GetComponent<HintChipController>();
            if (hint != null) hint.ResetForNewLevel();
        }

        // CM-DAILYWIRE: ResultsPanel's ONE seam now routes here instead of binding LoadNext
        // directly — the single decision point that keeps a Daily win structurally unable to
        // reach LevelBand/NextLevelId/WrapAtEndOfBand (criterion 5). Deleting this branch (i.e.
        // routing straight to LoadNext again) is the named mutation proof.
        private void OnResultsCtaRequested()
        {
            if (_dailySession) { ReturnHomeFromDaily(); return; }
            if (_returnHomeAfterCampaignUnlock)
            {
                ReturnHomeAfterCampaignUnlock();
                return;
            }
            LoadNext();
        }

        // The threshold-crossing campaign result uses the existing single CTA to reveal the
        // newly unlocked Home surface in the same run. L008 is loaded first, so Home never sits
        // over a stale Won board and continuing campaign play remains one tap away.
        private void ReturnHomeAfterCampaignUnlock()
        {
            _returnHomeAfterCampaignUnlock = false;
            var results = GetComponent<ResultsPanel>();
            if (results != null) results.SetCtaTextKey("results.next");
            LoadNext();
            if (Home != null)
            {
                Home.SetDailyLifetimeCompletions(LifetimeDailyCompletions);
                Home.SetDailyStatusKey(null);
                _pendingHomeShowFrame = Time.frameCount;
            }
        }

        // CM-DAILYWIRE criterion 6: a Daily win returns Home, never the next level. Restores
        // the campaign level that was active before Daily was selected (never a stale Won
        // Daily session bleeding behind a freshly re-shown Home) and resets the CTA text back
        // to the campaign default. This is a shipped path whenever save/config unlocks Daily;
        // _dailySession can only be true when Home exists because SelectDaily is bound there.
        private void ReturnHomeFromDaily()
        {
            _dailySession = false;
            _activeDailySelection = null;
            _activeDailyPractice = false;
            var results = GetComponent<ResultsPanel>();
            if (results != null) results.SetCtaTextKey("results.next");
            if (_preDailyLevel != null)
            {
                var restore = _preDailyLevel;
                _preDailyLevel = null;
                LoadLevel(restore);
            }
            // Home.Show()/Stack.Push is deferred — see the
            // Update() comment below for the one-frame-input-lockout rationale (the L001/
            // Daily pins the Show() below would register sit at the SAME screen coordinates
            // the results CTA the player just tapped occupied, both centered in the same
            // thumb band).
            if (Home != null)
            {
                Home.SetDailyLifetimeCompletions(LifetimeDailyCompletions);
                Home.SetDailyStatusKey(null);
                _pendingHomeShowFrame = Time.frameCount;
            }
        }

        // CM-DAILYWIRE criteria 2/3/8: the funnel-position-6 entry point. Reads the ONE clock
        // value and tries the prevalidated catalog first. A catalog hit loads immediately; a
        // miss runs the pure generator/validator/solver off the Unity thread and returns this
        // frame with visible loading copy. On any failure nothing loads and Home stays visible
        // with a recorded, user-facing error.
        public void SelectDaily()
        {
            if (Session == null) return;
            if (Home != null && !_dailyEntryUnlocked) return;
            if (_dailyFallbackTask != null) return;
            // CM-DAILYWIRE F4 (review fix round): defensive — a second SelectDaily() call
            // while already in a Daily session must be a no-op, mirroring LoadNext's own
            // defensive-clear shape/comment style (A-LN-3-adjacent). Without this guard,
            // _preDailyLevel below would be overwritten with _level, which by then IS the
            // Daily board itself (set by the FIRST call's LoadLevel) — corrupting the
            // return-home restore target to the Daily board instead of the true pre-Daily
            // campaign level.
            if (_dailySession) return;
            Home?.SetDailyStatusKey(null);
            long unixSeconds = DailyClockUnixSeconds();
            string requestedDateKey = DailyLineSeed.DateKeyFromUnixSeconds(unixSeconds);
            DailyDateSelection selection = _dailyProgress?.ObserveUtcDate(requestedDateKey);
            string effectiveDateKey = selection?.EffectiveDateKey ?? requestedDateKey;
            if (TryResolvePrecomputedDailyBoard(effectiveDateKey, out var resolved))
            {
                EnterDaily(resolved, selection, effectiveDateKey);
                return;
            }

            BeginDailyFallback(effectiveDateKey, selection);
        }

        private void EnterDaily(ImportedLevel resolved, DailyDateSelection selection, string dateKey)
        {
            Wardrobe?.Hide();
            _preDailyLevel = _level;
            LoadLevel(resolved);
            _activeDailySelection = selection;
            _activeDailyPractice = selection == null || selection.IsPractice;
            _dailySession = true;
            _analyticsRuntime?.BeginDailyLevel(_level, dateKey);
            var results = GetComponent<ResultsPanel>();
            if (results != null) results.SetCtaTextKey("results.daily.done");
            // A notification route can arrive while either composed screen is current. Hide
            // both owners before clearing their breadcrumbs so no visible sheet or registered
            // chrome region survives over the newly loaded Daily board.
            Home?.Hide();
            Intro?.Hide();
            while (Stack != null && Stack.TryPop(out _)) { }
            if (_activeDailyPractice) Banner.ShowKey("daily.practice");
        }

        private bool TryResolvePrecomputedDailyBoard(string dateKey, out ImportedLevel level)
        {
            LastDailyBoardSource = "";
            if (_dailyCatalog != null)
            {
                var lookup = _dailyCatalog.Lookup(dateKey);
                if (lookup.Found)
                {
                    LastDailyBoardSource = "precomputed";
                    Debug.Log("SEAM_LOADED daily:" + dateKey);
                    Debug.Log("DAILY_SOURCE precomputed:" + dateKey);
                    level = lookup.Entry.Level;
                    return true;
                }
                Debug.Log("daily precomputed miss for " + dateKey + ": " + lookup.Detail);
            }
            if (_generatedDailyCache.TryGetValue(dateKey, out level))
            {
                LastDailyBoardSource = "generated";
                Debug.Log("SEAM_LOADED daily:" + dateKey);
                Debug.Log("DAILY_SOURCE generated-memory:" + dateKey);
                return true;
            }
            level = null;
            return false;
        }

        private void BeginDailyFallback(string dateKey, DailyDateSelection selection)
        {
            DailyRunRequest request;
            try
            {
                request = new DailyRunRequest(
                    DailyRuntimeInputs.SchemaBytes,
                    DailyRuntimeInputs.ValidatorConfig,
                    DailyRuntimeInputs.PipelineConfig(dateKey),
                    weekdayCurveBytes: null,
                    dateKeys: new[] { dateKey },
                    factory: DailyFactory(),
                    referenceTimestamp: null,
                    boardProvenance: "runtime:GameRoot.SelectDaily (CM-DAILYWIRE)",
                    seedScheme: DailyLineSeedScheme.Instance);
            }
            catch (System.Exception ex)
            {
                FailDailyFallback(dateKey, "request setup failed: " + ex.Message);
                return;
            }

            _pendingDailyDateKey = dateKey;
            _pendingDailySelection = selection;
            _dailyFallbackCancellation = new CancellationTokenSource();
            CancellationToken token = _dailyFallbackCancellation.Token;
            Home?.SetDailyStatusKey("home.daily.loading");
            _dailyFallbackTask = System.Threading.Tasks.Task.Run(
                () => GenerateDailyFallback(request, dateKey, token), token);
        }

        private sealed class DailyFallbackResolution
        {
            public ImportedLevel Level { get; }
            public string Error { get; }

            private DailyFallbackResolution(ImportedLevel level, string error)
            {
                Level = level;
                Error = error;
            }

            public static DailyFallbackResolution Success(ImportedLevel level) =>
                new DailyFallbackResolution(level, null);

            public static DailyFallbackResolution Failure(string error) =>
                new DailyFallbackResolution(null, error);
        }

        // Engine-free work only. Unity logging, UI mutation, and board installation stay in
        // PumpDailyFallback on the main thread.
        private static DailyFallbackResolution GenerateDailyFallback(
            DailyRunRequest request, string dateKey, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return DailyFallbackResolution.Failure("cancelled");

            var run = DailyPipeline.Run(request);
            if (!run.Ok)
                return DailyFallbackResolution.Failure(
                    "pipeline request failed: " + run.Error);
            if (token.IsCancellationRequested)
                return DailyFallbackResolution.Failure("cancelled");
            if (run.Value.Records == null || run.Value.Records.Count == 0)
                return DailyFallbackResolution.Failure("pipeline returned no date record");

            var record = run.Value.Records[0];
            if (record.Blocks || record.Board == null || record.BoardJson == null)
                return DailyFallbackResolution.Failure(
                    "pipeline could not admit a board: " + record.Detail);
            if (token.IsCancellationRequested)
                return DailyFallbackResolution.Failure("cancelled");

            var imported = LevelImporter.Import(
                System.Text.Encoding.UTF8.GetBytes(record.BoardJson));
            if (!imported.Ok)
                return DailyFallbackResolution.Failure(
                    "generated board unusable: " + imported.Error);
            return DailyFallbackResolution.Success(imported.Value);
        }

        private void PumpDailyFallback()
        {
            var task = _dailyFallbackTask;
            if (task == null || !task.IsCompleted) return;

            string dateKey = _pendingDailyDateKey;
            DailyDateSelection selection = _pendingDailySelection;
            var cancellation = _dailyFallbackCancellation;
            _dailyFallbackTask = null;
            _dailyFallbackCancellation = null;
            _pendingDailyDateKey = null;
            _pendingDailySelection = null;

            if (task.IsCanceled || cancellation == null || cancellation.IsCancellationRequested)
            {
                cancellation?.Dispose();
                return;
            }

            DailyFallbackResolution resolution;
            try
            {
                resolution = task.Result;
            }
            catch (System.Exception ex)
            {
                cancellation.Dispose();
                FailDailyFallback(dateKey, "background task failed: " + ex.GetBaseException().Message);
                return;
            }
            cancellation.Dispose();

            if (resolution == null || resolution.Level == null)
            {
                FailDailyFallback(dateKey, resolution?.Error ?? "background task returned no result");
                return;
            }

            Debug.Log("SEAM_LOADED daily:" + dateKey);
            Debug.Log("DAILY_SOURCE generated:" + dateKey);
            LastDailyBoardSource = "generated";
            _generatedDailyCache[dateKey] = resolution.Level;
            Home?.SetDailyStatusKey(null);
            EnterDaily(resolution.Level, selection, dateKey);
        }

        private void FailDailyFallback(string dateKey, string detail)
        {
            Debug.LogError("daily fallback failed for " + dateKey + ": " + detail);
            Home?.SetDailyStatusKey("home.daily.unavailable");
        }

        private void CancelPendingDailyFallback()
        {
            if (_dailyFallbackTask == null) return;
            var task = _dailyFallbackTask;
            var cancellation = _dailyFallbackCancellation;
            cancellation?.Cancel();
            _dailyFallbackTask = null;
            _dailyFallbackCancellation = null;
            _pendingDailyDateKey = null;
            _pendingDailySelection = null;
            Home?.SetDailyStatusKey(null);
            ObserveDetachedDailyFallback(task, cancellation);
        }

        // DailyPipeline does not accept a cancellation token, so a worker already inside it may
        // finish after navigation. Checks bracket the full pipeline, its result is detached and
        // can never install, and this continuation still observes any fault and disposes the CTS.
        private static void ObserveDetachedDailyFallback(
            System.Threading.Tasks.Task<DailyFallbackResolution> task,
            CancellationTokenSource cancellation)
        {
            task.ContinueWith(completed =>
            {
                if (completed.IsFaulted)
                {
                    System.AggregateException ignored = completed.Exception;
                    System.GC.KeepAlive(ignored);
                }
                cancellation?.Dispose();
            }, CancellationToken.None,
                System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously,
                System.Threading.Tasks.TaskScheduler.Default);
        }

        private void Update()
        {
            PumpDailyFallback();
            PumpMessagingRoutes();
            PumpForegroundPermissionRecheck();
            _analyticsRuntime?.Tick();
            PollAnalyticsConnectivity();
            if (Session == null || _halted) return;
            // The one-frame input lockout. Request frame
            // F (ReturnHomeFromDaily, above) sets _pendingHomeShowFrame = F WITHOUT showing
            // Home yet, so a same-frame repeat tap at the CTA's coordinates finds nothing
            // registered there. This check only fires once Time.frameCount > F + 1 — i.e. it
            // skips the very next Update() (frame F+1) too, so a SECOND tap arriving after
            // exactly one yield (still "at" frame F+1) ALSO finds nothing registered yet and
            // falls through harmlessly, instead of landing on the freshly-shown pin and
            // pushing Intro. Home actually appears on the Update() at frame F+2. Mirrors
            // LoadLevel's own unregister-before-show sequencing principle, extended across a
            // frame boundary instead of within one synchronous call.
            if (_pendingHomeShowFrame >= 0 && Time.frameCount > _pendingHomeShowFrame + 1)
            {
                _pendingHomeShowFrame = -1;
                if (Home != null)
                ShowHomeForPresentation();
            }
            if (_checkReminderAfterHomePresentation)
            {
                _checkReminderAfterHomePresentation = false;
                TryPresentEarnedReminderPrompt();
            }
            // CM-BOOT-HOME criterion 2 (the tick-0 hold — the one genuinely new behavior): the
            // trailing "&& !ScreensVisible" mirrors the board-input gate above
            // (Input.BoardInputActive) so the sim never advances behind Home/Intro before the
            // first Play tap — L001 cannot auto-run/fail while the player hasn't started yet.
            // Once the Play tap drains the stack (ScreensVisible -> false), the sim resumes from
            // tick 0 exactly as if this frame were the very first one.
            if (Session.State.Outcome.Kind == CatMetro.Domain.OutcomeKind.Running
                && ScreenState == "Playing" && !ScreensVisible)
            {
                try
                {
                    Session.AdvanceMs(Time.deltaTime * 1000.0);
                }
                catch (System.Exception ex)
                {
                    // CM-C2b review F2: a pinned Domain boundary (NEW-Q4's exception on a
                    // misroute) or an envelope guard must HALT the run loudly, never re-enter a
                    // partially-stepped tick every frame or masquerade as a game outcome.
                    _halted = true;
                    ScreenState = "Halted";
                    // CM-UX-07 criterion 4 (Q-2, human-approved): the halt escape is a chrome
                    // REGION, never a CTA/veil component edit — the F-DEV-4 "no Try-again on
                    // halt" assert stays true untouched. Full-screen, HaltEscapePriority (5,
                    // CM-LOADNEXT D-1's named ladder — was an inline literal, #46-F9); Retry
                    // (above) unregisters it, so a re-halt after escaping re-registers cleanly.
                    Input.Regions.Register("halt.escape",
                        () => new Rect(0f, 0f, Screen.width, Screen.height), Retry,
                        Presentation.Input.ChromeRegions.HaltEscapePriority);
                    Debug.LogError("run halted at a pinned/guarded Domain boundary: " + ex.Message);
                    return;
                }
            }
            View.UpdateFrom(Session);

            var outcome = Session.State.Outcome;
            if (outcome.Kind == CatMetro.Domain.OutcomeKind.Won && ScreenState != "Won")
            {
                ScreenState = "Won";
                _analyticsRuntime?.CompleteLevel(_level, Session.State);
                Banner.ShowKey("win.banner");
                // CM-DAILYWIRE criterion 9 (A-DL-6): surfaced the moment a REAL Daily win
                // happens — the admitted board's own DTO reward, never a guessed/pinned amount.
                if (_dailySession)
                {
                    DailyTicketsEarned = _level.Dto.Economy.BaseTickets;
                    if (_dailyProgress != null && _activeDailySelection != null)
                    {
                        var completion = _dailyProgress.RecordDailyCompletion(
                            _activeDailySelection);
                        Home?.SetDailyLifetimeCompletions(completion.LifetimeCompletions);
                        if (completion.Counted && _dailyReminderPreferences != null)
                        {
                            _reminderPromptPending = _dailyReminderPreferences.CanOfferPrompt(
                                completion.LifetimeCompletions);
                            ConfigureReminderHome();
                        }
                    }
                }
                else if (_dailyProgress != null
                    && System.Array.IndexOf(LevelBand, _level.Dto.Id) >= 0)
                {
                    int campaignCompletions =
                        _dailyProgress.RecordCampaignCompletion(_level.Dto.Id);
                    if (!_dailyEntryUnlocked
                        && campaignCompletions >= DailyUnlockAfterCampaignCompletions)
                    {
                        _dailyEntryUnlocked = true;
                        Home?.UnlockDaily(LifetimeDailyCompletions);
                        _returnHomeAfterCampaignUnlock = true;
                        var results = GetComponent<ResultsPanel>();
                        if (results != null) results.SetCtaTextKey("results.daily.done");
                    }
                }
            }
            else if (outcome.Kind == CatMetro.Domain.OutcomeKind.Failed
                && ScreenState != "FailureReview")
            {
                ScreenState = "FailureReview";
                // ux-flows S-03 ST-ERR (review N9): attribution may NEVER block the fail sheet
                // or the retry — a throw falls back to the ambiguous variant (no framing).
                int causal = -1;
                try
                {
                    causal = CauseAttribution.CausalNode(Session.State);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("error_caught domain=cause_attribution: " + ex.Message);
                }
                if (causal >= 0)
                    CauseCam.FrameNode(View.NodeId(causal), View.NodeWorldPos(causal), MotionOff);
                // CM-C2b review F3 lineage: the banner keys by the REASON — never a wrong
                // string for the fail the player actually hit.
                var (key, token) = FailKey(outcome.Reason);
                if (token != null)
                    Banner.ShowKeySubstituted(key, token, causal >= 0 ? View.NodeId(causal) : "?");
                else
                    Banner.ShowKey(key);
            }
        }

        private void OnDestroy()
        {
            _destroying = true;
            SupersedePermissionRequest();
            ClearSettingsEnableIntent();
            _foregroundPermissionRecheckPending = false;
            var permissionCancellation = _messagingPermissionCancellation;
            _messagingPermissionCancellation = null;
            if (permissionCancellation != null)
            {
                permissionCancellation.Cancel();
                permissionCancellation.Dispose();
            }

            var permissionTask = _messagingPermissionTask;
            _messagingPermissionTask = null;
            if (permissionTask != null) ObserveDetachedPermissionTask(permissionTask);

            var messaging = _messaging;
            _messaging = null;
            if (messaging != null)
            {
                if (_messagingListenerAttached)
                {
                    try
                    {
                        messaging.LinkOpened -= QueueMessagingRoute;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning("daily reminder listener cleanup failed: " + ex.Message);
                    }
                    _messagingListenerAttached = false;
                }
                try
                {
                    messaging.Dispose();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("daily reminder provider disposal failed: " + ex.Message);
                }
            }
            CancelPendingDailyFallback();
            if (_cosmetics != null)
            {
                CatMetro.Services.Cosmetics.CosmeticRuntime.Uninstall(_cosmetics);
                _cosmetics.Dispose();
                _cosmetics = null;
            }
            _analyticsRuntime?.Dispose();
            _analyticsRuntime = null;
        }

        private static void ObserveDetachedPermissionTask(System.Threading.Tasks.Task task)
        {
            task.ContinueWith(completed =>
            {
                if (completed.IsFaulted)
                {
                    System.AggregateException ignored = completed.Exception;
                    System.GC.KeepAlive(ignored);
                }
            }, CancellationToken.None,
                System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously,
                System.Threading.Tasks.TaskScheduler.Default);
        }

        private void PollAnalyticsConnectivity()
        {
            if (_analyticsRuntime == null) return;
            var current = UnityEngine.Application.internetReachability;
            if (_networkReachabilityKnown
                && _lastNetworkReachability == NetworkReachability.NotReachable
                && current != NetworkReachability.NotReachable)
                _analyticsRuntime.OnNetworkReachable();
            _lastNetworkReachability = current;
            _networkReachabilityKnown = true;
        }

    }
}
