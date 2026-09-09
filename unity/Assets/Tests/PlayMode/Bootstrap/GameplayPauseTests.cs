using System;
using System.Collections;
using System.IO;
using System.Linq;
using CatMetro.Bootstrap;
using CatMetro.Bootstrap.DevCapture;
using CatMetro.Domain;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Fx;
using CatMetro.Presentation.Screens;
using CatMetro.Presentation.Theme;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.PlayMode
{
    public sealed class GameplayPauseTests
    {
        private GameRoot _root;
        private string _temporary;

        private sealed class Storage : CatMetro.Services.IStorageRoot
        {
            public string SaveDirectory { get; }
            public string CacheDirectory => SaveDirectory;
            public Storage(string path) { SaveDirectory = path; }
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 0f;
            _temporary = Path.Combine(Path.GetTempPath(), "cm-pause-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporary);
            DevLevelOverride.DirectoryOverride = _temporary;
            DevBootOverride.DirectoryOverride = _temporary;
            GameRoot.DailyStorageRootOverride = () => new Storage(_temporary);
            GameRoot.DevSkipShippedHome = false;
            _root = GameRoot.Launch();
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
            DevLevelOverride.DirectoryOverride = null;
            DevBootOverride.DirectoryOverride = null;
            GameRoot.DailyStorageRootOverride = null;
            GameRoot.DevSkipShippedHome = false;
            Time.timeScale = 1f;
            Directory.Delete(_temporary, true);
        }

        private IEnumerator Play()
        {
            _root.Input.HandleTapAtScreen(_root.Home.PinPaintedRectPx.center);
            Assert.That(_root.Input.Regions.IsRegistered("game.pause"), Is.False,
                "the gameplay pin cannot steal an intro tap");
            _root.Input.HandleTapAtScreen(_root.Intro.PlayChipRectPx.center);
            yield return null;
        }

        private RectTransform Pin => _root.GetComponentsInChildren<RectTransform>()
            .Single(t => t.name == "PausePin");

        private Vector2 ScreenPoint(RectTransform rect) => _root.Cam.WorldToScreenPoint(
            rect.TransformPoint(rect.rect.center));

        private void Open()
        {
            Assert.That(_root.Input.Regions.IsRegistered("game.pause"), Is.True,
                "gameplay must expose a reachable pause pin");
            Assert.That(_root.Input.HandleTapAtScreen(ScreenPoint(Pin)), Is.EqualTo(-3));
            Assert.That(_root.Stack.Current, Is.EqualTo("pause"));
        }

        private TMP_Text Label(string text) => _root.GetComponentsInChildren<TMP_Text>()
            .Single(t => t.text == text);

        private void TapLabel(string text) =>
            Assert.That(_root.Input.HandleTapAtScreen(ScreenPoint(Label(text).rectTransform)), Is.EqualTo(-3));

        [UnityTest]
        public IEnumerator PauseFreezesTheSameSession_ResumeRestoresSwitchTaps()
        {
            yield return Play();
            _root.MotionOffToggle = true;
            var point = (Vector2)_root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0));
            Assert.That(_root.Input.HandleTapAtScreen(point), Is.EqualTo(0), "positive control");
            var session = _root.Session;
            int tick = session.State.Tick, commands = session.Log.Entries.Count;
            Open();
            Time.timeScale = 1f;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(session.State.Tick, Is.EqualTo(tick), "pause closes the real simulation gate");
            Assert.That(_root.Input.HandleTapAtScreen(point), Is.EqualTo(-3), "the sheet consumes board taps");
            Assert.That(session.Log.Entries.Count, Is.EqualTo(commands));
            TapLabel("Resume");
            Assert.That(_root.ScreensVisible, Is.False, "MotionOff closes the sheet immediately");
            Assert.That(_root.Session, Is.SameAs(session), "Resume must not restart a run");
            Assert.That(_root.Input.HandleTapAtScreen(point), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator PauseUsesStackedPriority_AndBlocksLowerRegionsWithoutFeedback()
        {
            yield return Play();
            int stolen = 0, taps = 0;
            _root.Input.UiTapAccepted = () => taps++;
            _root.Input.Regions.Register("under-pause", () => new Rect(0, 0, Screen.width, Screen.height),
                () => stolen++, ChromeRegions.ModalPriority);
            // The entry itself is a parent control: remove the artificial full-screen modal
            // while opening, then re-register it beneath the real stacked sheet.
            _root.Input.Regions.Unregister("under-pause");
            Open();
            _root.Input.Regions.Register("under-pause", () => new Rect(0, 0, Screen.width, Screen.height),
                () => stolen++, ChromeRegions.ModalPriority);
            int before = taps;
            Assert.That(_root.Input.HandleTapAtScreen(new Vector2(1, Screen.height * .5f)), Is.EqualTo(-3));
            Assert.That(stolen, Is.Zero);
            Assert.That(taps, Is.EqualTo(before), "a shade is a blocker, not a button");
            Assert.That(Label("Resume").font, Is.SameAs(TypeScale.DisplayFont));
            Assert.That(Label("Your trains will wait.").font, Is.SameAs(TypeScale.BodyFont));
        }

        [UnityTest]
        public IEnumerator FailurePauseHome_RebuildsBehindTheVeil_AndHoldsTickZero()
        {
            yield return Play();
            _root.Session.State.Outcome = SimOutcome.MakeFailed(FailReason.TimeOut);
            yield return null;
            yield return null;
            var failed = _root.Session;
            Open();
            Assert.That(_root.Input.RetryRegionActive(), Is.False, "a sheet suspends Retry eligibility");
            Assert.That(_root.Input.HandleTapAtScreen(new Vector2(Screen.width * .5f, 1)), Is.EqualTo(-3));
            Assert.That(_root.Session, Is.SameAs(failed), "the old retry band must not dismiss the sheet");
            TapLabel("Home");
            var veil = _root.GetComponent<ScreenChromeController>().Transition;
            Assert.That(veil.IsInFlight, Is.True);
            Assert.That(_root.Session, Is.SameAs(failed));
            veil.Advance(.22f, false);
            Assert.That(_root.Session, Is.Not.SameAs(failed));
            Assert.That(_root.CauseCam.RingVisible, Is.False);
            Time.timeScale = 1f;
            for (int i = 0; i < 4; i++) yield return null;
            Assert.That(_root.Home.IsVisible, Is.True);
            Assert.That(_root.Stack.Current, Is.EqualTo("home"));
            Assert.That(_root.Session.State.Tick, Is.Zero, "no run starts during deferred Home presentation");
            Assert.That(_root.Input.Regions.IsRegistered("game.pause"), Is.False);
        }

        [UnityTest]
        public IEnumerator CompactPinClearsWaveCapsule_AndSheetMotionSettlesWithMotionOff()
        {
            yield return Play();
            Open();
            var sheet = Label("Paused").transform.parent;
            Vector3 initial = sheet.localScale;
            Assert.That(initial.x, Is.LessThan(1f), "motion starts at the composed small pose");
            _root.MotionOffToggle = true;
            yield return null;
            Assert.That(sheet.localScale, Is.EqualTo(Vector3.one));
            TapLabel("Resume");
            yield return null;
            var corners = new Vector3[4];
            Pin.GetWorldCorners(corners);
            var lower = (Vector2)_root.Cam.WorldToScreenPoint(corners[0]);
            var upper = (Vector2)_root.Cam.WorldToScreenPoint(corners[2]);
            var rect = new Rect(lower, upper - lower);
            Assert.That(rect.Overlaps(_root.Preview.CapsuleRectPx), Is.False,
                "the pause pin cannot obscure wave faces or the counters");
            Assert.That(HudBands.StatusBand(Screen.safeArea).Contains(rect.center), Is.True);
            Assert.That(rect.width, Is.GreaterThanOrEqualTo(48f * HudBands.PxPerDp(Screen.dpi) - .1f));
            Assert.That(rect.height, Is.GreaterThanOrEqualTo(48f * HudBands.PxPerDp(Screen.dpi) - .1f));
        }

        [UnityTest]
        public IEnumerator DisablingOwner_RemovesPauseActions_AndRejectsStaleHomeCallback()
        {
            yield return Play();
            Open();
            var session = _root.Session;
            _root.enabled = false;
            Assert.That(_root.Input.Regions.IsRegistered("pause.home"), Is.False);
            Assert.That(_root.Input.Regions.IsRegistered("pause.resume"), Is.False);
            Assert.That(_root.Input.Regions.IsRegistered("pause.blocker"), Is.False);
            _root.ReturnHome(); // A callback already copied by the input loop is also harmless.
            Assert.That(_root.GetComponent<ScreenChromeController>().Transition.IsInFlight, Is.False);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(_root.Session, Is.SameAs(session));
        }

        [UnityTest]
        public IEnumerator DisablingOwner_CancelsResumeCompletion_BeforeAReplacementSheet()
        {
            yield return Play();
            Open();
            TapLabel("Resume");
            _root.enabled = false;
            Assert.That(_root.GetComponentInChildren<GameplayPauseView>().IsVisible, Is.False);
            Assert.That(_root.Stack.Current, Is.Not.EqualTo("pause"));
            _root.enabled = true;
            yield return null;
            Open();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(_root.Stack.Current, Is.EqualTo("pause"),
                "the cancelled Resume cannot pop the replacement sheet");
            Assert.That(_root.GetComponentInChildren<GameplayPauseView>().IsVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator CapturePauseHudAndSheet_WhenRequested()
        {
            string directory = CaptureDirectory();
            if (string.IsNullOrEmpty(directory)) { Assert.Pass("pause capture disarmed"); yield break; }
            yield return Play();
            _root.MotionOffToggle = true;
            CapturePhone(directory, "hud-L001.png");
            var pause = _root.GetComponentInChildren<GameplayPauseView>();
            _root.Input.HandleTapAtScreen(pause.EntryRectPx.center);
            Assert.That(pause.IsVisible, Is.True);
            CapturePhone(directory, "pause-L001.png");
        }

        [UnityTest]
        public IEnumerator CaptureFailureMoodAndPause_WhenRequested()
        {
            string directory = CaptureDirectory();
            if (string.IsNullOrEmpty(directory)) { Assert.Pass("pause capture disarmed"); yield break; }
            Object.DestroyImmediate(_root.gameObject);
            File.Copy(Path.Combine(UnityEngine.Application.streamingAssetsPath, "content/levels/L005.json"),
                Path.Combine(_temporary, "level.json"));
            _root = GameRoot.Launch();
            yield return null;
            yield return Play();
            _root.Session.State.Trains[0] = new TrainSlot { Color = CatColor.Red, Id = 1, NodeId = 0, State = TrainState.AtNode };
            _root.Session.State.NodeQueueCounts[0] = 1;
            _root.Session.State.NodeQueueSlots[0][0] = 1;
            _root.View.UpdateFrom(_root.Session);
            CapturePhone(directory, "playing-L005.png");
            _root.CauseCam.CapturePlayPose(-_root.View.transform.forward);
            _root.Session.State.Outcome = SimOutcome.MakeFailed(FailReason.TimeOut);
            _root.SendMessage("Update");
            _root.Preview.BindScreenState(() => _root.ScreenState);
            // Isolate the mood samples at the final cause framing; the existing camera suite
            // separately verifies the pan. This is a constructed L005 timeout with one train.
            _root.CauseCam.FrameNode(_root.View.NodeId(0), _root.View.NodeWorldPos(0), true);
            CapturePhone(directory, "fail-L005-000ms.png", 0f);
            _root.CauseCam.GetComponent<BoardFx>().Advance(.175f);
            _root.Banner.SamplePresentation(.175f);
            foreach (var puff in _root.View.GetComponentsInChildren<ParticleSystem>())
                if (puff.name.StartsWith("Board burst")) puff.Simulate(.175f, true, false);
            CapturePhone(directory, "fail-L005-175ms.png", .175f);
            _root.CauseCam.GetComponent<BoardFx>().Advance(.175f);
            _root.Banner.SamplePresentation(.35f);
            CapturePhone(directory, "fail-L005-350ms.png", .35f);
            _root.MotionOffToggle = true;
            _root.Input.HandleTapAtScreen(_root.GetComponentInChildren<GameplayPauseView>().EntryRectPx.center);
            Assert.That(_root.Stack.Current, Is.EqualTo("pause"));
            CapturePhone(directory, "pause-failed-L005.png");
        }

        private static string CaptureDirectory()
        {
            string directory = Environment.GetEnvironmentVariable("CM_PAUSE_CAPTURE_DIR");
            var arguments = Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(arguments, "-cmPauseCaptureDir");
            return !string.IsNullOrEmpty(directory) ? directory
                : flag >= 0 && flag + 1 < arguments.Length ? arguments[flag + 1] : null;
        }

        private void CapturePhone(string directory, string filename, float failureElapsed = -1f)
        {
            var camera = _root.Cam;
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            float aspect = camera.aspect;
            var target = new RenderTexture(917, 2048, 24) { antiAliasing = 4 };
            var pixels = new Texture2D(917, 2048, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.aspect = 917f / 2048f;
                Canvas.ForceUpdateCanvases();
                if (_root.ScreenState == "Playing") BoardSceneLook.FitCamera(camera, _root.View);
                var safe = new Rect(0, 64, 917, 1920);
                _root.Preview.LayoutForViewport(safe, 408f);
                _root.GetComponentInChildren<GameplayPauseView>().LayoutForViewport(safe, 408f);
                _root.Banner.LayoutForViewport(safe, 408f);
                _root.GetComponent<ScreenChromeController>().Cta.LayoutForViewport(safe, 408f);
                if (failureElapsed >= 0f) _root.Banner.SamplePresentation(failureElapsed);
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 917, 2048), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, filename), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; camera.aspect = aspect;
                RenderTexture.active = active;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }
    }
}
