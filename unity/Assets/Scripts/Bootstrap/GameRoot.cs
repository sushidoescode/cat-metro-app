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
    // frame time. Presentation never simulates. Boots two ways: placed in the Game scene
    // (Awake self-initializes — the device path) or via Launch()/LaunchWith() (tests).
    // CM-C3: on failure the screen state is FailureReview — the cause camera frames the causal
    // node (state-derived, A-C3-1), the reason-keyed LOCKED string renders with substitution,
    // and one tap on the thumb band retries by RE-SIMULATION from tick 0 (no scene load,
    // no snapshot — ADR-0002 §9).
    public sealed class GameRoot : MonoBehaviour
    {
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

        public static GameRoot Launch(string levelPath = "content/levels/L001.json")
        {
            var go = new GameObject("GameRoot");
            var root = go.AddComponent<GameRoot>();
            root.InitializeFromSeam(levelPath);
            return root;
        }

        public static GameRoot LaunchWith(ImportedLevel level)
        {
            var go = new GameObject("GameRoot");
            var root = go.AddComponent<GameRoot>();
            root.Wire(level);
            return root;
        }

        private void Awake()
        {
            if (Session == null) InitializeFromSeam("content/levels/L001.json");
        }

        private void InitializeFromSeam(string levelPath)
        {
            if (Session != null) return;
            var source = new StreamingAssetsContentSource();
            var bytes = source.ReadAsync(levelPath, CancellationToken.None).GetAwaiter().GetResult();
            var imported = LevelImporter.Import(bytes);
            if (!imported.Ok)
                throw new System.InvalidOperationException("level unusable: " + imported.Error);
            Debug.Log("SEAM_LOADED " + levelPath);
            Wire(imported.Value);
        }

        private void Wire(ImportedLevel level)
        {
            _level = level;
            Session = new GameSession(level);

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.orthographic = true;
            Cam.orthographicSize = 7f;
            camGo.transform.position = new Vector3(3f, 5.5f, -10f);
            CauseCam = camGo.AddComponent<CauseCameraController>();
            CauseCam.Wire(Cam);

            View = BoardView.Build(level, transform, Session);
            Input = gameObject.AddComponent<Presentation.Input.TapInput>();
            Input.Wire(Session, View, Cam);
            Input.RetryRegionActive = () => ScreenState == "FailureReview";
            Input.RetryTapped = Retry;
            Banner = BannerView.Create(transform);
            Preview = WavePreviewStrip.Create(transform, Session, Cam);
            Log = gameObject.AddComponent<FrameLog>();
            Log.SimTickSource = () => Session.State.Tick;
            Log.ScreenStateSource = () => ScreenState;
        }

        // CM-C3 criteria 8/9: retry = a fresh session over the SAME imported level; zero scene
        // loads; the board view rebuilds; every switch back at initialRoute by construction.
        public void Retry()
        {
            if (Session == null) return;
            Session = new GameSession(_level);
            if (View != null) Destroy(View.gameObject);
            View = BoardView.Build(_level, transform, Session);
            Input.Wire(Session, View, Cam);
            if (Preview != null) Destroy(Preview.gameObject);
            Preview = WavePreviewStrip.Create(transform, Session, Cam);
            Banner.Hide();
            CauseCam.Reset();
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
                    _halted = true;
                    ScreenState = "Halted";
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
                int causal = CauseAttribution.CausalNode(Session.State);
                if (causal >= 0)
                    CauseCam.FrameNode(View.NodeId(causal), View.NodeWorldPos(causal), MotionOff);
                if (outcome.Reason == CatMetro.Domain.FailReason.QueueOverflow)
                    Banner.ShowKeySubstituted("fail.queueoverflow", "{node}",
                        causal >= 0 ? View.NodeId(causal) : "?");
                else if (outcome.Reason == CatMetro.Domain.FailReason.TimeOut)
                    Banner.ShowKey("fail.banner.timeout");
                else
                    Banner.ShowKey("fail.banner"); // no shipped path constructs other reasons
            }
        }
    }
}
