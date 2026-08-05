using System.Threading;
using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Diagnostics;
using CatMetro.Presentation.Hud;
using UnityEngine;

namespace CatMetro.Bootstrap
{
    // The composition root (ADR-0003's 10th row): loads the level THROUGH the StreamingAssets
    // seam, builds the engine-free session, wires Presentation, drives the tick loop with real
    // frame time. Presentation never simulates; this Update is the only place wall-clock time
    // enters, and it enters as a dt argument. Boots two ways: placed in the Game scene (Awake
    // self-initializes — the device path, review F4c) or via Launch()/LaunchWith() (tests).
    public sealed class GameRoot : MonoBehaviour
    {
        public GameSession Session { get; private set; }
        public BoardView View { get; private set; }
        public Presentation.Input.TapInput Input { get; private set; }
        public FrameLog Log { get; private set; }
        public BannerView Banner { get; private set; }
        public Camera Cam { get; private set; }
        public string ScreenState { get; private set; } = "Playing";

        private bool _halted;

        public static GameRoot Launch(string levelPath = "content/levels/L001.json")
        {
            var go = new GameObject("GameRoot");
            var root = go.AddComponent<GameRoot>();
            root.InitializeFromSeam(levelPath);
            return root;
        }

        // Test seam for fixture boards (criterion 5's scripted overflow) — same wiring, no file.
        public static GameRoot LaunchWith(ImportedLevel level)
        {
            var go = new GameObject("GameRoot");
            var root = go.AddComponent<GameRoot>();
            root.Wire(level);
            return root;
        }

        private void Awake()
        {
            // Scene-boot path: a GameRoot placed in Game.unity self-initializes on device.
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
            // Criterion 8's artifact line: proves the played level came through the seam.
            Debug.Log("SEAM_LOADED " + levelPath);
            Wire(imported.Value);
        }

        private void Wire(ImportedLevel level)
        {
            Session = new GameSession(level);

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(transform, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.orthographic = true;
            Cam.orthographicSize = 7f;
            camGo.transform.position = new Vector3(3f, 5.5f, -10f);

            View = BoardView.Build(level, transform, Session);
            Input = gameObject.AddComponent<Presentation.Input.TapInput>();
            Input.Wire(Session, View, Cam);
            Banner = BannerView.Create(transform);
            Log = gameObject.AddComponent<FrameLog>();
            Log.SimTickSource = () => Session.State.Tick;
            Log.ScreenStateSource = () => ScreenState;
        }

        private void Update()
        {
            if (Session == null || _halted) return;
            if (Session.State.Outcome.Kind == CatMetro.Domain.OutcomeKind.Running)
            {
                try
                {
                    Session.AdvanceMs(Time.deltaTime * 1000.0);
                }
                catch (System.Exception ex)
                {
                    // Review F2: a pinned Domain boundary (NEW-Q4's NotSupportedException on a
                    // misroute — L001's zero-tap path hits it at ~t30) or an envelope guard must
                    // HALT the run loudly, never re-enter a partially-stepped tick every frame
                    // or masquerade as a game outcome. No misleading banner; the board stays.
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
            else if (outcome.Kind == CatMetro.Domain.OutcomeKind.Failed && ScreenState != "Failed")
            {
                ScreenState = "Failed";
                // Review F3: the banner keys by the REASON — a time-out must never read
                // "Overloaded!". The timeout row carries the LOCKED S-03 string (CM-C3 would
                // otherwise append the identical row; recorded in the disposition).
                Banner.ShowKey(outcome.Reason == CatMetro.Domain.FailReason.TimeOut
                    ? "fail.banner.timeout" : "fail.banner");
            }
        }
    }
}
