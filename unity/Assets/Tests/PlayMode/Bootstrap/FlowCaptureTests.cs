using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Bootstrap.DevCapture;
using CatMetro.Content;
using CatMetro.Presentation.Cats;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Screens;
using CatMetro.Presentation.Theme;
using CatMetro.Presentation.Input;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.PlayMode
{
    public sealed class FlowCaptureTests
    {
        private static readonly Rect PhoneSafe = new Rect(0, 64, 917, 1920);
        private GameRoot _root;
        private string _temporary;
        private GameObject _uiCanvas;
        private Camera _uiCamera;
        private LevelIntroSheet _uiSheet;
        private ResultsPanel _uiResults;
        private BannerView _uiBanner;
        private RetryCtaView _uiRetry;
        private float _winCaptureStart;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
            if (_uiCanvas != null) Object.DestroyImmediate(_uiCanvas);
            if (_uiCamera != null) Object.DestroyImmediate(_uiCamera.gameObject);
            _root = null;
            _uiCanvas = null;
            _uiCamera = null;
            _uiSheet = null;
            _uiResults = null;
            _uiBanner = null;
            _uiRetry = null;
            GameRoot.DevSkipShippedHome = false;
            DevLevelOverride.DirectoryOverride = null;
            GameRoot.DailyStorageRootOverride = null;
            Time.timeScale = 1f;
            if (_temporary != null && Directory.Exists(_temporary)) Directory.Delete(_temporary, true);
            _temporary = null;
        }

        [UnityTest]
        public IEnumerator CaptureEvidence_UiTicket_WhenRequested()
        {
            string directory = Environment.GetEnvironmentVariable("CM_FLOW_UI_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) { Assert.Pass("UI-only flow capture disarmed"); yield break; }
            _uiCamera = new GameObject("TicketOnlyCamera", typeof(Camera)).GetComponent<Camera>();
            _uiCamera.clearFlags = CameraClearFlags.SolidColor;
            _uiCamera.backgroundColor = Palette.WarmPaper;
            _uiCamera.orthographic = true;
            _uiCamera.nearClipPlane = .1f;
            _uiCanvas = new GameObject("TicketOnlyCanvas", typeof(Canvas));
            var canvas = _uiCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _uiCamera;
            canvas.planeDistance = 1f;
            _uiSheet = LevelIntroSheet.Create(canvas.transform);
            var imported = LevelImporter.Import(File.ReadAllBytes(Path.Combine(
                UnityEngine.Application.streamingAssetsPath, "content", "levels", "L001.json")));
            _uiSheet.Show(imported.Value.Dto.Name, imported.Value.Dto.Win.Deliveries,
                imported.Value.Dto.Meta.TeachingGoal);
            yield return null;
            Capture(directory, "intro-L001-ui-only.png");
        }

        [UnityTest]
        public IEnumerator CaptureEvidence_UiWin_WhenRequested()
        {
            string directory = Environment.GetEnvironmentVariable("CM_FLOW_UI_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) { Assert.Pass("UI-only win capture disarmed"); yield break; }
            _uiCamera = new GameObject("WinOnlyCamera", typeof(Camera)).GetComponent<Camera>();
            _uiCamera.clearFlags = CameraClearFlags.SolidColor;
            _uiCamera.backgroundColor = Palette.WarmPaper;
            _uiCamera.orthographic = true;
            _uiCamera.nearClipPlane = .1f;
            _uiResults = _uiCamera.gameObject.AddComponent<ResultsPanel>();
            _uiResults.Attach(() => "Won", new ChromeRegions());
            _uiBanner = BannerView.Create(_uiCamera.transform);
            _uiBanner.ShowKey("win.banner");
            yield return null;
            Capture(directory, "win-plus-0.4s-ui-only.png", .4f);
            Capture(directory, "win-plus-1.0s-ui-only.png", 1f);
            _uiResults.PanelRoot.SetActive(false);
            _uiBanner.ShowKey("fail.platformoverflow.generic");
            var chrome = _uiCamera.gameObject.AddComponent<ScreenChromeController>();
            chrome.Attach(() => "FailureReview");
            _uiRetry = chrome.Cta;
            Capture(directory, "fail-platform-ui-only.png");
            chrome.Transition.Begin(() => { }, false);
            chrome.Transition.Advance(.22f, false);
            Capture(directory, "transition-opaque-ui-only.png");
        }

        [UnityTest]
        public IEnumerator CaptureEvidence_IntroTickets_WhenRequested()
        {
            string directory = Environment.GetEnvironmentVariable("CM_FLOW_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) { Assert.Pass("flow capture disarmed"); yield break; }
            Assert.That(CatModelCatalog.LoadResources().AdmittedEntryCount, Is.EqualTo(1),
                "armed board captures require the main checkout's admitted licensed rig");
            _temporary = Path.Combine(Path.GetTempPath(), "cm-flow-capture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporary);
            DevLevelOverride.DirectoryOverride = _temporary;
            GameRoot.DailyStorageRootOverride = () => new CaptureStorage(_temporary);
            GameRoot.DevSkipShippedHome = false;
            _root = GameRoot.Launch();
            _root.MotionOffToggle = true;
            yield return null;
            _root.Input.HandleTapAtScreen(_root.Home.PinPaintedRectPx.center);
            yield return null;
            Assert.That(_root.Intro.IsVisible, Is.True);
            Capture(directory, "intro-L001-from-home.png");
            Assert.That(_root.Intro.GoalText, Is.EqualTo("Deliver 1 cat"));

            _root.Input.HandleTapAtScreen(_root.Intro.PlayChipRectPx.center);
            _root.Session.EnqueueToggle(0);
            _root.Session.AdvanceMs(200 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return null;
            yield return null;
            Assert.That(_root.ScreenState, Is.EqualTo("Won"));
            _root.Input.HandleTapAtScreen(_root.GetComponent<ResultsPanel>().ChipPaintedRectPx.center);
            yield return null;
            Assert.That(_root.CurrentLevelId, Is.EqualTo("L002"));
            Assert.That(_root.Intro.IsVisible, Is.True);
            Assert.That(_root.Session.State.Tick, Is.Zero);
            Capture(directory, "intro-L002-after-next.png");

            foreach (string id in new[] { "L005", "L009", "L013" })
            {
                Object.DestroyImmediate(_root.gameObject);
                File.WriteAllBytes(Path.Combine(_temporary, "level.json"), File.ReadAllBytes(Path.Combine(
                    UnityEngine.Application.streamingAssetsPath, "content", "levels", id + ".json")));
                _root = GameRoot.Launch();
                _root.MotionOffToggle = true;
                yield return null;
                _root.Input.HandleTapAtScreen(_root.Home.PinPaintedRectPx.center);
                Capture(directory, "intro-" + id + "-teaching.png");
            }
        }

        [UnityTest]
        public IEnumerator CaptureEvidence_WinAndFail_WhenRequested()
        {
            string directory = Environment.GetEnvironmentVariable("CM_FLOW_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) { Assert.Pass("flow capture disarmed"); yield break; }
            Assert.That(CatModelCatalog.LoadResources().AdmittedEntryCount, Is.EqualTo(1),
                "armed board captures require the main checkout's admitted licensed rig");
            _temporary = Path.Combine(Path.GetTempPath(), "cm-flow-capture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporary);
            DevLevelOverride.DirectoryOverride = _temporary;
            GameRoot.DailyStorageRootOverride = () => new CaptureStorage(_temporary);
            GameRoot.DevSkipShippedHome = false;
            _root = GameRoot.Launch();
            Time.timeScale = 0f;
            yield return null;
            _root.Input.HandleTapAtScreen(_root.Home.PinPaintedRectPx.center);
            _root.Input.HandleTapAtScreen(_root.Intro.PlayChipRectPx.center);
            _root.Session.EnqueueToggle(0);
            _root.Session.AdvanceMs(200 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            _winCaptureStart = Time.unscaledTime;
            _root.View.UpdateFrom(_root.Session, _winCaptureStart);
            yield return null;
            yield return null;
            Assert.That(_root.ScreenState, Is.EqualTo("Won"));
            Assert.That(_root.View.transform.Find("delivered-cat:0"), Is.Not.Null);
            yield return new WaitForSecondsRealtime(.4f);
            Capture(directory, "win-L001-plus-0.4s.png", .4f);
            var passenger = _root.View.transform.Find("delivered-cat:0");
            var cat = passenger.Find("Carriage/Cat");
            var restingScale = cat.localScale;
            Capture(directory, "win-L001-hop-peak.png", .84f);
            Assert.That(cat.localScale.magnitude, Is.GreaterThan(restingScale.magnitude * 1.1f),
                "the current rig's bind-pose Celebrate clip needs a visible presentation hop");
            yield return new WaitForSecondsRealtime(.6f);
            Capture(directory, "win-L001-plus-1.0s.png", 1f);
            Assert.That(passenger.gameObject.activeInHierarchy, Is.True);
            Assert.That(passenger.GetComponent<CatMetro.Presentation.Board.ToyTrainView>().RigAdmitted,
                Is.True, "the win frame must contain the real admitted cat");
            var animator = passenger.GetComponentInChildren<Animator>();
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(CatModelCatalog.CelebrateClip),
                Is.True, "the +1s frame is inside Cat_Celebrate");
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime,
                Is.GreaterThan(.75f), "the rig samples its pose even when timeScale is zero");

            Object.DestroyImmediate(_root.gameObject);
            File.WriteAllBytes(Path.Combine(_temporary, "level.json"), File.ReadAllBytes(Path.Combine(
                UnityEngine.Application.streamingAssetsPath, "content", "levels", "L005.json")));
            _root = GameRoot.Launch();
            _root.MotionOffToggle = true;
            yield return null;
            _root.Input.HandleTapAtScreen(_root.Home.PinPaintedRectPx.center);
            _root.Input.HandleTapAtScreen(_root.Intro.PlayChipRectPx.center);
            _root.Session.State.Outcome = CatMetro.Domain.SimOutcome.MakeFailed(
                CatMetro.Domain.FailReason.PlatformOverflow);
            yield return null;
            yield return null;
            string station = _root.Session.Level.Dto.Stations.ToArray()[0].NodeId;
            int cause = Array.FindIndex(_root.Session.Level.Dto.Nodes.ToArray(), n => n.Id == station);
            _root.CauseCam.FrameNode(station, _root.View.NodeWorldPos(cause), true);
            Assert.That(_root.Banner.CurrentText, Is.EqualTo("A platform overflowed"));
            Capture(directory, "fail-L005-forced-platform-overflow.png");
        }

        private sealed class CaptureStorage : CatMetro.Services.IStorageRoot
        {
            public string SaveDirectory { get; }
            public string CacheDirectory => SaveDirectory;
            public CaptureStorage(string root)
            {
                SaveDirectory = Path.Combine(root, "save");
                Directory.CreateDirectory(SaveDirectory);
            }
        }

        private void Capture(string directory, string name, float winElapsed = -1f)
        {
            var camera = _root != null ? _root.Cam : _uiCamera;
            var previous = camera.targetTexture;
            var previousActive = RenderTexture.active;
            float aspect = camera.aspect;
            var target = new RenderTexture(917, 2048, 24, RenderTextureFormat.ARGB32);
            Texture2D frame = null;
            try
            {
                camera.targetTexture = target;
                camera.aspect = 917f / 2048f;
                (_root != null ? _root.Intro : _uiSheet)?.LayoutForViewport(PhoneSafe, 408f);
                var banner = _root != null ? _root.Banner : _uiBanner;
                var results = _root != null ? _root.GetComponent<ResultsPanel>() : _uiResults;
                banner?.LayoutForViewport(PhoneSafe, 408f);
                results?.LayoutForViewport(PhoneSafe, 408f);
                (_root != null ? _root.GetComponent<ScreenChromeController>().Cta : _uiRetry)
                    ?.LayoutForViewport(PhoneSafe, 408f);
                if (winElapsed >= 0f)
                {
                    if (_root != null) _root.View.UpdateFrom(_root.Session, _winCaptureStart + winElapsed);
                    banner.SamplePresentation(winElapsed);
                    results.SamplePresentation(winElapsed);
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                frame = new Texture2D(917, 2048, TextureFormat.RGB24, false);
                frame.ReadPixels(new Rect(0, 0, 917, 2048), 0, 0);
                frame.Apply();
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, name), frame.EncodeToPNG());
                if (winElapsed >= 0f)
                {
                    Assert.That(results.ChipFaceRectPx.height / (408f / 160f),
                        Is.EqualTo(60f).Within(.01f));
                    float maximum = 0f;
                    int brightPixels = 0;
                    var bounds = banner.PaintedRectPx;
                    bounds.y = bounds.center.y - 32f * (408f / 160f);
                    bounds.height = 64f * (408f / 160f);
                    bounds.xMin += 32f * (408f / 160f);
                    bounds.xMax -= 32f * (408f / 160f);
                    for (int y = Mathf.CeilToInt(bounds.yMin); y < bounds.yMax; y++)
                        for (int x = Mathf.CeilToInt(bounds.xMin); x < bounds.xMax; x++)
                        {
                            Color c = frame.GetPixel(x, y);
                            float luminance = .2126f * c.r + .7152f * c.g + .0722f * c.b;
                            maximum = Mathf.Max(maximum, luminance);
                            if (luminance > .9f) brightPixels++;
                        }
                    File.WriteAllText(Path.Combine(directory, name + ".txt"),
                        "title max luminance=" + maximum + " bright pixels=" + brightPixels
                        + " CTA height=60dp; elapsed=" + winElapsed + "\n");
                    Assert.That(maximum, Is.GreaterThan(.9f));
                    Assert.That(brightPixels, Is.GreaterThan(100));
                }
            }
            finally
            {
                camera.targetTexture = previous;
                camera.aspect = aspect;
                RenderTexture.active = previousActive;
                if (frame != null) Object.DestroyImmediate(frame);
                Object.DestroyImmediate(target);
            }
        }
    }
}
