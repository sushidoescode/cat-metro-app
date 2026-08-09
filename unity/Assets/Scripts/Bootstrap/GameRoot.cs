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

        // CM-LOADNEXT: read-only so tests/UI can observe progression without a second source of
        // truth for "what level is this." Null only before the first Wire() (never observable
        // through Launch/LaunchWith, which always Wire synchronously before returning).
        public string CurrentLevelId => _level?.Dto.Id;

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

        // CM-DEVCAP3: the device-side companion to BootToHome — set from DevBootOverride's file
        // read inside InitializeFromSeam (the real device/Launch()/Awake() path only; LaunchWith
        // never touches it, exactly like DevLevelOverride never applies there). OR'd with the
        // static flag at Wire-end (criterion 5: the two seams are independent — neither can
        // suppress the other; see DevBootOverrideTests.Precedence_*).
        private bool _bootToHomeFileOverride;

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
            // CM-DEVCAP3: evaluated BEFORE the level-override early-return so the boot-to-home
            // file takes effect on either sub-path (dev level override OR the shipped level) —
            // the two file seams are independent (one picks WHICH level, the other picks
            // WHETHER the dev screen flow composes over it, Q-5 law).
            _bootToHomeFileOverride = DevCapture.DevBootOverride.ShouldBootToHome();
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
            results.NextRequested = LoadNext;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (GetComponent<DevCapture.DevFrameCapture>() == null)
                gameObject.AddComponent<DevCapture.DevFrameCapture>().Wire(this);
            // CM-DEVCAP3 criterion 5 (precedence): OR'd, never AND'd — the static test flag's
            // own power to compose is never gated by the file (it composes even when the file
            // is absent/malformed), and the file's power to compose is never gated by the flag
            // (it composes even when BootToHome is left at its default false, the only case
            // that matters on a real device where the static flag is never touched).
            if (BootToHome || _bootToHomeFileOverride) ComposeDevScreenFlow();
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

        // CM-LOADNEXT: the campaign band, in play order — the level-progression POLICY (save-
        // backed unlocks, campaign gating) is explicitly deferred beyond this contract
        // (docs/ux/ux-layer-decompose.md §5), so this stays a plain ordered list, not a graph.
        // ASSUMPTION under human review (state/handoffs/CM-LOADNEXT-frozen-contract.md, A-LN-1):
        // the band WRAPS at the end back to L001 (a demo-friendly infinite loop) rather than
        // stopping — WrapAtEndOfBand is the one seam to flip if that assumption is overridden.
        public static readonly string[] LevelBand = { "L001", "L002", "L003", "L004", "L005" };
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
            _level = level;
            Session = new GameSession(level);
            if (View != null) Destroy(View.gameObject);
            View = BoardView.Build(level, transform, Session);
            View.MotionOffSource = () => MotionOff; // criterion 3: rebind on the REBUILT view
            Input.Wire(Session, View, Cam);
            if (Preview != null) Destroy(Preview.gameObject);
            Preview = WavePreviewStrip.Create(transform, Session, Cam);
            Banner.Hide();
            CauseCam.Reset(); // clears the ring AND restores the rest pose (review B5)
            _halted = false;
            ScreenState = "Playing";
        }

        public void Retry()
        {
            if (Session == null) return;
            LoadLevel(_level);
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
            // CM-UX-05 forward obligation (state/handoffs/CM-UX-05.md): a NEW level resets the
            // per-level hint attempt-run; Retry() of the SAME level must not (that accumulation
            // is the mechanic) — LoadLevel() stays silent on this by design so Retry() keeps its
            // pinned behavior; LoadNext is the one caller that speaks.
            var hint = GetComponent<HintChipController>();
            if (hint != null) hint.ResetForNewLevel();
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
