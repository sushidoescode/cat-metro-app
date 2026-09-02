using System.Threading;
using CatMetro.Application.Retry;
using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Content.Daily;
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
        public CatMetro.Presentation.Screens.ScreenStack Stack { get; private set; }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
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

        // CM-DAILYWIRE: mirrors BootToHome's exact shape — a static field so ComposeScreenFlow
        // can read it synchronously at compose-time (an instance field set AFTER LaunchWith
        // returns would be too late). Default false: product_spec §18 gates Daily behind "after
        // L007 win," and HomeScreenTests.cs's S-01 tree walk independently forbids a Daily node
        // on the session-1 Home this class composes today — the two rules agree, and neither is
        // this contract's to relax. The real L007-win check needs the save layer, which is
        // unwired for any level today (Known debt, both here and in the frozen contract);
        // until it lands, this is a dev/test-only seam for exercising the real wiring, never a
        // shipped default (ComposeScreenFlow reads this through a #if — see below — so a
        // shipped build never builds the Daily pin at all).
        public static bool DailyEntryUnlocked;

        // CM-DAILYWIRE F3 (review fix round): the pending-Home-show marker ReturnHomeFromDaily
        // sets instead of calling Home.Show()/Stack.Push synchronously — see the Update()
        // comment for the one-frame-lockout rationale. -1 == nothing pending.
        private int _pendingHomeShowFrame = -1;
#endif

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
            // CM-BOOT-HOME criterion 1 (the new shipped default): every real boot composes Home
            // over the just-wired level, unless the dev skip hatch opts out (SkipHome(), always
            // false in a shipped build). LaunchWith (the ~12 gameplay fixtures' seam) bypasses
            // InitializeFromSeam entirely and never reaches this line — they get NO Home.
            if (!SkipHome()) ComposeScreenFlow();
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
            // CM-DAILYWIRE / CM-BOOT-HOME criterion 5: DailyEntryUnlocked is a dev/test-only
            // static field (fenced) — a shipped build has no such flag, so dailyUnlocked is
            // unconditionally false there (S-01 / commerce-free law: the Daily pin must never
            // even be CONSTRUCTED on the shipped path, not merely hidden).
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            bool dailyUnlocked = DailyEntryUnlocked;
#else
            bool dailyUnlocked = false;
#endif
            Home = CatMetro.Presentation.Screens.HomeScreenView.Create(
                canvasGo.transform, dailyUnlocked);
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
            // CM-DAILYWIRE: the Daily entry — no Intro sheet (the pin's own label is
            // self-explanatory; unlike campaign levels this is not a fresh unnamed board), so
            // SelectDaily() itself hides Home and clears the stack straight into gameplay on a
            // successful resolve; on failure it returns without touching Home at all
            // (criterion 7 — loud, never silent, never a half-shown screen). SelectDaily is a
            // public, unfenced method, but the Daily pin above is only ever CONSTRUCTED when
            // dailyUnlocked is true (never in a shipped build), so this delegate can never fire
            // there — wiring it unconditionally costs nothing and keeps this method identical
            // across dev and shipped builds.
            Home.DailySelected = SelectDaily;
            Intro.PlayRequested = () =>
            {
                Intro.Hide();
                Home.Hide(); // idempotent — already hidden by the push above
                while (Stack.TryPop(out _)) { }
            };

            Home.Show();
            Stack.Push("home");
        }

        // CM-C3 criterion 10's reason→key mapping, PURE and test-drivable (review S1): the
        // PlatformOverflow is the ELSE: the correct station-specific string renders with the
        // causal station substitution supplied by CauseAttribution.
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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // CM-DAILYWIRE F3 (review fix round): any new level load cancels a stale pending
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
#endif
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
            Banner.Hide();
            CauseCam.Reset(); // clears the ring AND restores this level's fitted play pose
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
            // CM-DAILYWIRE: defensive — campaign progression must never run inside a Daily
            // session. Unreachable via any wired UI (OnResultsCtaRequested routes Daily to
            // ReturnHomeFromDaily instead), but LoadNext is a public method any caller could
            // invoke directly (the same permissive shape Retry/LoadNext already had before this
            // contract, A-LN-3) — a second, independent guard layer.
            _dailySession = false;
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

        // CM-DAILYWIRE: ResultsPanel's ONE seam now routes here instead of binding LoadNext
        // directly — the single decision point that keeps a Daily win structurally unable to
        // reach LevelBand/NextLevelId/WrapAtEndOfBand (criterion 5). Deleting this branch (i.e.
        // routing straight to LoadNext again) is the named mutation proof.
        private void OnResultsCtaRequested()
        {
            if (_dailySession) { ReturnHomeFromDaily(); return; }
            LoadNext();
        }

        // CM-DAILYWIRE criterion 6: a Daily win returns Home, never the next level. Restores
        // the campaign level that was active before Daily was selected (never a stale Won
        // Daily session bleeding behind a freshly re-shown Home — the same collision class
        // CM-LOADNEXT's Known-debt entry names for this dev-only flow) and resets the CTA text
        // back to the campaign default. The Home/Stack re-show is internally fenced because
        // this whole path is reachable only through a real Daily session (dev/test-only,
        // DailyEntryUnlocked-gated — CM-BOOT-HOME leaves Daily out of scope, criterion 5), and
        // _dailySession can only be true when Home exists in the first place, since SelectDaily's
        // only caller is Home.DailySelected.
        private void ReturnHomeFromDaily()
        {
            _dailySession = false;
            var results = GetComponent<ResultsPanel>();
            if (results != null) results.SetCtaTextKey("results.next");
            if (_preDailyLevel != null)
            {
                var restore = _preDailyLevel;
                _preDailyLevel = null;
                LoadLevel(restore);
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // CM-DAILYWIRE F3 (review fix round): Home.Show()/Stack.Push deferred — see the
            // Update() comment below for the one-frame-input-lockout rationale (the L001/
            // Daily pins the Show() below would register sit at the SAME screen coordinates
            // the results CTA the player just tapped occupied, both centered in the same
            // thumb band).
            if (Home != null) _pendingHomeShowFrame = Time.frameCount;
#endif
        }

        // CM-DAILYWIRE criteria 2/3/8: the funnel-position-6 entry point. Reads the ONE clock
        // value, runs the REAL DailyPipeline (schema/validator/pipeline inputs from
        // DailyRuntimeInputs — embedded compiled constants, never a shipped file, FA-2), and on
        // a real admission loads the board through LevelImporter.Import — the identical
        // function InitializeFromSeam/LoadNext already call. On any failure nothing loads and
        // Home stays exactly as it was (criterion 7) — a loud, recorded error, never a silent
        // blank board.
        public void SelectDaily()
        {
            if (Session == null) return;
            // CM-DAILYWIRE F4 (review fix round): defensive — a second SelectDaily() call
            // while already in a Daily session must be a no-op, mirroring LoadNext's own
            // defensive-clear shape/comment style (A-LN-3-adjacent). Without this guard,
            // _preDailyLevel below would be overwritten with _level, which by then IS the
            // Daily board itself (set by the FIRST call's LoadLevel) — corrupting the
            // return-home restore target to the Daily board instead of the true pre-Daily
            // campaign level.
            if (_dailySession) return;
            var resolved = ResolveDailyBoard();
            if (resolved == null) return;
            _preDailyLevel = _level;
            LoadLevel(resolved);
            _dailySession = true;
            var results = GetComponent<ResultsPanel>();
            if (results != null) results.SetCtaTextKey("results.daily.done");
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Home != null)
            {
                Home.Hide();
                while (Stack != null && Stack.TryPop(out _)) { }
            }
#endif
        }

        // Pure orchestration over the real engine API — no parallel validator, no shortcut
        // admission. Returns null on ANY failure (request-level or a blocked date record); the
        // caller (SelectDaily) treats null uniformly as "nothing to load."
        private ImportedLevel ResolveDailyBoard()
        {
            long unixSeconds = DailyClockUnixSeconds();
            string dateKey = DailyLineSeed.DateKeyFromUnixSeconds(unixSeconds);
            var request = new DailyRunRequest(
                DailyRuntimeInputs.SchemaBytes,
                DailyRuntimeInputs.ValidatorConfig,
                DailyRuntimeInputs.PipelineConfig(dateKey),
                weekdayCurveBytes: null,
                dateKeys: new[] { dateKey },
                factory: DailyFactory(),
                referenceTimestamp: null,
                boardProvenance: "runtime:GameRoot.SelectDaily (CM-DAILYWIRE)",
                seedScheme: DailyLineSeedScheme.Instance);

            var run = DailyPipeline.Run(request);
            if (!run.Ok)
            {
                Debug.LogError("daily pipeline request failed for " + dateKey + ": " + run.Error);
                return null;
            }
            var record = run.Value.Records[0];
            if (record.Blocks || record.Board == null || record.BoardJson == null)
            {
                Debug.LogError("daily pipeline could not admit a board for " + dateKey + ": "
                    + record.Detail);
                return null;
            }
            var imported = LevelImporter.Import(
                System.Text.Encoding.UTF8.GetBytes(record.BoardJson));
            if (!imported.Ok)
            {
                Debug.LogError("daily board unusable for " + dateKey + ": " + imported.Error);
                return null;
            }
            // The SEAM_LOADED artifact line's Daily sibling — proves the played board came
            // through the real admission pipeline, not a shortcut.
            Debug.Log("SEAM_LOADED daily:" + dateKey);
            return imported.Value;
        }

        private void Update()
        {
            if (Session == null || _halted) return;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // CM-DAILYWIRE F3 (review fix round): the one-frame input lockout. Request frame
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
                {
                    Home.Show();
                    Stack.Push("home");
                }
            }
#endif
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
                    // CM-C2b review F2: an unexpected Domain or envelope guard must HALT the run
                    // loudly, never re-enter a partially-stepped tick every frame or masquerade
                    // as a game outcome.
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
                    Debug.LogError("run halted at an unexpected Domain boundary: " + ex.Message);
                    return;
                }
            }
            View.UpdateFrom(Session);

            var outcome = Session.State.Outcome;
            if (outcome.Kind == CatMetro.Domain.OutcomeKind.Won && ScreenState != "Won")
            {
                ScreenState = "Won";
                Banner.ShowKey("win.banner");
                // CM-DAILYWIRE criterion 9 (A-DL-6): surfaced the moment a REAL Daily win
                // happens — the admitted board's own DTO reward, never a guessed/pinned amount.
                if (_dailySession) DailyTicketsEarned = _level.Dto.Economy.BaseTickets;
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
