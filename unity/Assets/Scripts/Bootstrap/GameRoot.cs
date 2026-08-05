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
    // enters, and it enters as a dt argument.
    public sealed class GameRoot : MonoBehaviour
    {
        public GameSession Session { get; private set; }
        public BoardView View { get; private set; }
        public Presentation.Input.TapInput Input { get; private set; }
        public FrameLog Log { get; private set; }
        public BannerView Banner { get; private set; }
        public Camera Cam { get; private set; }
        public string ScreenState { get; private set; } = "Playing";

        public static GameRoot Launch(string levelPath = "content/levels/L001.json")
        {
            var source = new StreamingAssetsContentSource();
            var bytes = source.ReadAsync(levelPath, CancellationToken.None).GetAwaiter().GetResult();
            var imported = LevelImporter.Import(bytes);
            if (!imported.Ok)
                throw new System.InvalidOperationException("level unusable: " + imported.Error);
            return LaunchWith(imported.Value);
        }

        // Test seam for fixture boards (criterion 5's scripted overflow) — same wiring, no file.
        public static GameRoot LaunchWith(ImportedLevel level)
        {
            var go = new GameObject("GameRoot");
            var root = go.AddComponent<GameRoot>();
            root.Session = new GameSession(level);

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(go.transform, false);
            root.Cam = camGo.AddComponent<Camera>();
            root.Cam.orthographic = true;
            root.Cam.orthographicSize = 7f;
            camGo.transform.position = new Vector3(3f, 5.5f, -10f);

            root.View = BoardView.Build(level, go.transform, root.Session);
            root.Input = go.AddComponent<Presentation.Input.TapInput>();
            root.Input.Wire(root.Session, root.View, root.Cam);
            root.Banner = BannerView.Create(go.transform);
            root.Log = go.AddComponent<FrameLog>();
            root.Log.SimTickSource = () => root.Session.State.Tick;
            root.Log.ScreenStateSource = () => root.ScreenState;
            return root;
        }

        private void Update()
        {
            if (Session == null) return;
            if (Session.State.Outcome.Kind == CatMetro.Domain.OutcomeKind.Running)
                Session.AdvanceMs(Time.deltaTime * 1000.0);
            View.UpdateFrom(Session);

            var kind = Session.State.Outcome.Kind;
            if (kind == CatMetro.Domain.OutcomeKind.Won && ScreenState != "Won")
            {
                ScreenState = "Won";
                Banner.ShowKey("win.banner");
            }
            else if (kind == CatMetro.Domain.OutcomeKind.Failed && ScreenState != "Failed")
            {
                ScreenState = "Failed";
                Banner.ShowKey("fail.banner");
            }
        }
    }
}
