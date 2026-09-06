using System.Collections;
using System.Globalization;
using System.IO;
using CatMetro.Bootstrap;
using CatMetro.Presentation.Board;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    public sealed class BoardMotionTests
    {
        private GameRoot _root;
        private GameObject _captureCamera;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 0f;
            GameRoot.DevSkipShippedHome = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (_captureCamera != null) Object.DestroyImmediate(_captureCamera);
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
            Time.timeScale = 1f;
            GameRoot.DevSkipShippedHome = false;
        }

        [UnityTest]
        public IEnumerator CaptureSwitchThrow_SixFramesAt50ms_WhenRequested()
        {
            string dir = System.Environment.GetEnvironmentVariable("CM_SWITCH_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) yield break;
            _root = GameRoot.Launch();
            yield return null;
            var toy = _root.View.GetComponentInChildren<ToySwitchView>();
            foreach (var part in toy.GetComponentsInChildren<Transform>()) part.gameObject.layer = 30;
            var cam = MacroCamera(toy.transform.position + Vector3.up * 0.15f);
            _root.Input.HandleTapAtScreen(_root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0)));
            var particles = _root.View.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particles) ps.gameObject.layer = 30;
            Directory.CreateDirectory(dir);
            var poses = new string[7];
            poses[0] = "seconds,yaw,lever_x,scale";
            for (int frame = 0; frame < 6; frame++)
            {
                if (frame > 0)
                {
                    _root.View.Fx.Advance(0.05f);
                    foreach (var ps in particles) ps.Simulate(0.05f, false, false);
                }
                SaveFrame(cam, Path.Combine(dir, "switch-" + (frame * 50).ToString("D3") + "ms.png"));
                poses[frame + 1] = string.Format(CultureInfo.InvariantCulture, "{0:F2},{1:F3},{2:F3},{3:F3}",
                    frame * 0.05f, toy.transform.localEulerAngles.z, toy.LeverPivot.localEulerAngles.x,
                    toy.transform.localScale.x);
            }
            File.WriteAllLines(Path.Combine(dir, "switch-poses.csv"), poses);
        }

        private Camera MacroCamera(Vector3 center)
        {
            foreach (var canvas in _root.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
            _captureCamera = new GameObject("Board motion capture", typeof(Camera));
            var cam = _captureCamera.GetComponent<Camera>();
            cam.CopyFrom(_root.Cam);
            cam.transform.position = center + Vector3.back * 10f;
            cam.transform.rotation = Quaternion.identity;
            cam.orthographicSize = 1.6f;
            cam.cullingMask = 1 << 30;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = CatMetro.Presentation.Theme.Palette.InkNavy;
            return cam;
        }

        [UnityTest]
        public IEnumerator CaptureSteamAndEngineBob_WhenRequested()
        {
            string dir = System.Environment.GetEnvironmentVariable("CM_STEAM_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) yield break;
            _root = GameRoot.LaunchWith(BoardFeedbackTests.Level());
            yield return null;
            int slot = -1;
            for (int tick = 0; tick < 30 && slot < 0; tick++)
            {
                _root.Session.AdvanceMs(125);
                slot = System.Array.FindIndex(_root.Session.State.Trains,
                    t => t.State == CatMetro.Domain.TrainState.OnEdge);
            }
            _root.View.UpdateFrom(_root.Session, 0f);
            var train = _root.View.transform.Find("train:" + slot);
            foreach (var part in train.GetComponentsInChildren<Transform>()) part.gameObject.layer = 30;
            train.Find("Carriage/Cat").gameObject.SetActive(false);
            var engine = train.Find("Engine");
            var cam = MacroCamera(engine.position + Vector3.up * 0.20f);
            cam.orthographicSize = 0.8f;
            var steam = train.GetComponentInChildren<ParticleSystem>();
            Directory.CreateDirectory(dir);
            var times = new[] { 0f, 0.1875f, 0.3f, 0.4875f, 0.6f, 0.7875f };
            var samples = new string[7];
            samples[0] = "seconds,bob_z,particles";
            for (int frame = 0; frame < 6; frame++)
            {
                _root.View.UpdateFrom(_root.Session, times[frame]);
                train.Find("Carriage/Cat").gameObject.SetActive(false);
                if (frame > 0) steam.Simulate(times[frame] - times[frame - 1], false, false);
                SaveFrame(cam, Path.Combine(dir, "steam-" + Mathf.RoundToInt(times[frame] * 1000f).ToString("D3") + "ms.png"));
                samples[frame + 1] = string.Format(CultureInfo.InvariantCulture, "{0:F4},{1:F5},{2}",
                    times[frame], engine.localPosition.z, steam.particleCount);
            }
            File.WriteAllLines(Path.Combine(dir, "steam-poses.csv"), samples);
        }

        [UnityTest]
        public IEnumerator CaptureChromePress_WhenRequested()
        {
            string dir = System.Environment.GetEnvironmentVariable("CM_PRESS_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) yield break;
            _root = GameRoot.LaunchWith(BoardFeedbackTests.Level());
            var panel = _root.GetComponent<CatMetro.Presentation.Hud.ResultsPanel>();
            panel.Attach(() => "Won", _root.Input.Regions);
            int actions = 0;
            panel.NextRequested = () => actions++;
            foreach (var renderer in _root.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            foreach (var canvas in _root.GetComponentsInChildren<Canvas>())
                canvas.enabled = canvas.gameObject == panel.PanelRoot;
            var rt = new RenderTexture(917, 2048, 24);
            _root.Cam.targetTexture = rt;
            yield return null;
            var chip = panel.PanelRoot.transform.Find("Panel/PrimaryCta") as RectTransform;
            if (chip == null)
                foreach (var part in panel.PanelRoot.GetComponentsInChildren<RectTransform>())
                    if (part.name == "PrimaryCta") { chip = part; break; }
            Assert.That(chip, Is.Not.Null);
            // Centre the existing painted chip for the macro; phone layout is a separate gate.
            chip.anchorMin = new Vector2(0.12f, 0.45f); chip.anchorMax = new Vector2(0.88f, 0.55f);
            chip.offsetMin = chip.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            Directory.CreateDirectory(dir);
            try
            {
                SaveFrame(_root.Cam, Path.Combine(dir, "press-000ms.png"));
                _root.Input.HandleTapAtScreen(panel.ChipPaintedRectPx.center);
                var fx = _root.GetComponent<CatMetro.Presentation.Fx.BoardFx>();
                int frame = Time.frameCount;
                fx.Advance(0.042f, frame + 1);
                SaveFrame(_root.Cam, Path.Combine(dir, "press-042ms.png"));
                fx.Advance(0.056f, frame + 2);
                SaveFrame(_root.Cam, Path.Combine(dir, "press-098ms.png"));
                fx.Advance(0.043f, frame + 3);
                SaveFrame(_root.Cam, Path.Combine(dir, "press-140ms.png"));
                Assert.That(actions, Is.EqualTo(1));
            }
            finally { _root.Cam.targetTexture = null; Object.DestroyImmediate(rt); }
        }

        [UnityTest]
        public IEnumerator CaptureWrongStation_SixFramesAt50ms_WhenRequested()
        {
            string dir = System.Environment.GetEnvironmentVariable("CM_REJECTION_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) yield break;
            _root = GameRoot.LaunchWith(BoardFeedbackTests.Level());
            yield return null;
            int slot = BoardFeedbackTests.ReachFirstRejection(_root);
            var train = _root.View.transform.Find("train:" + slot);
            var station = _root.View.transform.Find("station:" + _root.View.NodeId(
                _root.Session.State.Trains[slot].NodeId));
            foreach (var part in train.GetComponentsInChildren<Transform>()) part.gameObject.layer = 30;
            foreach (var part in station.GetComponentsInChildren<Transform>()) part.gameObject.layer = 30;
            // This macro isolates owned vehicle and badge geometry. The licensed-cat board
            // capture remains a separate main-checkout gate.
            train.Find("Carriage/Cat").gameObject.SetActive(false);
            station.GetComponent<Renderer>().enabled = false;
            var pin = train.Find("Carriage/Pin");
            var plate = station.Find("station:plate-generated");
            var cam = MacroCamera((pin.position + plate.position) * 0.5f);
            cam.orthographicSize = 1.9f;
            var engine = train.Find("Engine");
            Quaternion neutral = plate.localRotation;
            Directory.CreateDirectory(dir);
            var poses = new string[7];
            poses[0] = "seconds,recoil_x,recoil_y,plate_degrees";
            int startFrame = Time.frameCount;
            for (int frame = 0; frame < 6; frame++)
            {
                if (frame > 0) _root.View.Fx.Advance(0.05f, startFrame + frame);
                SaveFrame(cam, Path.Combine(dir, "rejection-" + (frame * 50).ToString("D3") + "ms.png"));
                poses[frame + 1] = string.Format(CultureInfo.InvariantCulture, "{0:F2},{1:F5},{2:F5},{3:F3}",
                    frame * 0.05f, engine.localPosition.x, engine.localPosition.y,
                    Quaternion.Angle(plate.localRotation, neutral));
            }
            File.WriteAllLines(Path.Combine(dir, "rejection-poses.csv"), poses);
        }

        private static void SaveFrame(Camera cam, string path)
        {
            var rt = new RenderTexture(917, 2048, 24);
            var previous = RenderTexture.active;
            var previousTarget = cam.targetTexture;
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                cam.targetTexture = previousTarget;
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(rt);
            }
        }
    }
}
