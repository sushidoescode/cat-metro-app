using System.Threading;
using CatMetro.Application.Retry;
using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Cameras;
using CatMetro.Presentation.Diagnostics;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Hud.WavePreview;
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

        // CM-C3 A-C3-3: motion state = toggle OR OS animation scale zero. No save field; the
        // device wiring of the OS scale arrives with the settings screen (reads-only here).
        public bool MotionOffToggle;
        public float AnimatorDurationScale = 1f;
        public bool MotionOff => MotionOffToggle || AnimatorDurationScale == 0f;

        private ImportedLevel _level;
        private bool _halted;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        // CM-UX-07 criterion 6: the dev-only screen flow's launch seam — the DevLevelOverride
        // precedent (a dev build's boot path, never shipped boot: Q-5 honored, InitializeFromSeam
        // is unchanged when this is false). Default false.
        public static bool BootToHome;

        public CatMetro.Presentation.Screens.HomeScreenView Home { get; private set; }
        public CatMetro.Presentation.Screens.LevelIntroSheet Intro { get; private set; }
        public CatMetro.Presentation.Screens.ScreenStack Stack { get; private set; }
#endif

        // CM-UX-07 criterion 2: true iff the dev screen flow (criterion 6) currently shows a
        // screen (Home or LevelIntro) — derived from the stack so there is one source of truth.
        // Always false in shipped boot: the predicate degenerates to the decompose's exact line.
        public bool ScreensVisible
        {
            get
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                return Stack != null && Stack.Count > 0;
#else
                return false;
#endif
            }
        }

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
        public static GameRoot LaunchWith(ImportedLevel level)
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
            root.Wire(level);
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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            var devLevel = DevCapture.DevLevelOverride.TryImport();
            if (devLevel != null) { Wire(devLevel); return; }
#endif
            var source = new StreamingAssetsContentSource();
            var bytes = source.ReadAsync(levelPath, CancellationToken.None).GetAwaiter().GetResult();
            var imported = LevelImporter.Import(bytes);
            if (!imported.Ok)
                throw new System.InvalidOperationException("level unusable: " + imported.Error);
            // Criterion 8's artifact line: proves the played level came through the seam.
            Debug.Log("SEAM_LOADED " + levelPath);
            Wire(imported.Value);
        }

        private void Wire(ImportedLevel level)
        {
            FramePolicy.Apply(); // CM-C2b-DEVFIX criterion 3: every boot path passes through here
            _level = level;
            Session = new GameSession(level);

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.orthographic = true;
            Cam.orthographicSize = 7f;
            camGo.transform.position = new Vector3(3f, 5.5f, -10f);
            CauseCam = camGo.AddComponent<CauseCameraController>();
            CauseCam.Wire(Cam); // captures the S-02 rest pose (review B5)

            View = BoardView.Build(level, transform, Session);
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
            // there (that is criterion 2's point). Only the "!ScreensVisible" term is
            // behavior-neutral in shipped boot, since ScreensVisible degenerates to false there
            // (criterion 6 never mounts).
            Input.BoardInputActive = () => ScreenState == "Playing" && !ScreensVisible;
            Banner = BannerView.Create(transform);
            Preview = WavePreviewStrip.Create(transform, Session, Cam);
            Log = gameObject.AddComponent<FrameLog>();
            Log.SimTickSource = () => Session.State.Tick;
            Log.ScreenStateSource = () => ScreenState;

            // CM-UX-07 criterion 1: chrome + hint attach to root.gameObject. F8 (round-1 review)
            // correction: the camera is NOT on root.gameObject itself — it lives on a child
            // GameObject named "Camera" (built above at GameRoot.cs:141-143); each controller
            // resolves it via GetComponentInChildren<Camera>() (the self-resolve pattern).
            // Regression pin: sortingOrder 100/90 unchanged.
            var chrome = gameObject.AddComponent<ScreenChromeController>();
            chrome.Attach(() => ScreenState);
            var hint = gameObject.AddComponent<HintChipController>();
            hint.Attach(() => ScreenState);
            // Criterion 7: ResetForNewLevel() has no call site yet — discharged as a documented
            // no-op until LoadNext exists; Retry() below never calls it (same-level law).

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (GetComponent<DevCapture.DevFrameCapture>() == null)
                gameObject.AddComponent<DevCapture.DevFrameCapture>().Wire(this);
            if (BootToHome) ComposeDevScreenFlow();
#endif
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        // CM-UX-07 criterion 6: ONE ScreensCanvas (ScreenSpaceCamera on Cam, sortingOrder 120 —
        // above the CM-UX-04 results canvas at 110) hosting Home + LevelIntro. Deliveries/name
        // come from the already-loaded level — no new I/O. ScreensVisible reads the stack, so
        // there is exactly one source of truth for "a screen is up".
        private void ComposeDevScreenFlow()
        {
            var canvasGo = new GameObject("ScreensCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Cam;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 120;

            Stack = new CatMetro.Presentation.Screens.ScreenStack();
            Home = CatMetro.Presentation.Screens.HomeScreenView.Create(canvasGo.transform);
            Home.Attach(Input.Regions, () => MotionOff);
            Intro = CatMetro.Presentation.Screens.LevelIntroSheet.Create(canvasGo.transform);
            Intro.Attach(Input.Regions);

            Home.LevelSelected = () =>
            {
                // A stack push navigates OFF Home (ScreenStack's own navigation law — only the
                // top of the stack is current): Home.Hide() also unregisters its pin, which
                // would otherwise tie-break ahead of Intro's Play chip (both center in the
                // thumb band at the identical point — the earliest registration wins ties).
                Home.Hide();
                Intro.Show(_level.Dto.Name, _level.Dto.Win.Deliveries);
                Stack.Push("intro");
            };
            Intro.PlayRequested = () =>
            {
                Intro.Hide();
                Home.Hide(); // idempotent — already hidden by the push above
                while (Stack.TryPop(out _)) { }
            };

            Home.Show();
            Stack.Push("home");
        }
#endif

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

        // CM-C3 criteria 8/9: retry = a fresh session over the SAME imported level; zero scene
        // loads; the board view rebuilds; every switch back at initialRoute by construction.
        public void Retry()
        {
            if (Session == null) return;
            // CM-UX-07 criterion 4 (Q-2): idempotent — a normal FailureReview retry never
            // registered "halt.escape", so this is a harmless no-op there (Unregister returns
            // false without throwing); a halt-escape retry clears the region it just consumed.
            Input.Regions.Unregister("halt.escape");
            Session = new GameSession(_level);
            if (View != null) Destroy(View.gameObject);
            View = BoardView.Build(_level, transform, Session);
            View.MotionOffSource = () => MotionOff; // criterion 3: rebind on the REBUILT view
            Input.Wire(Session, View, Cam);
            if (Preview != null) Destroy(Preview.gameObject);
            Preview = WavePreviewStrip.Create(transform, Session, Cam);
            Banner.Hide();
            CauseCam.Reset(); // clears the ring AND restores the rest pose (review B5)
            _halted = false;
            ScreenState = "Playing";
        }

        private void Update()
        {
            if (Session == null || _halted) return;
            if (Session.State.Outcome.Kind == CatMetro.Domain.OutcomeKind.Running
                && ScreenState == "Playing")
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
                    // halt" assert stays true untouched. Full-screen, priority 5; Retry (above)
                    // unregisters it, so a re-halt after escaping re-registers cleanly.
                    Input.Regions.Register("halt.escape",
                        () => new Rect(0f, 0f, Screen.width, Screen.height), Retry, 5);
                    Debug.LogError("run halted at a pinned/guarded Domain boundary: " + ex.Message);
                    return;
                }
            }
            View.UpdateFrom(Session);

            var outcome = Session.State.Outcome;
            if (outcome.Kind == CatMetro.Domain.OutcomeKind.Won && ScreenState != "Won")
            {
                ScreenState = "Won";
                Banner.ShowKey("win.banner");
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
    }
}
