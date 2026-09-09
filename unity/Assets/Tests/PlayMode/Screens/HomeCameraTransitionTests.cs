using System.Collections;
using System.IO;
using CatMetro.Bootstrap;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Fx;
using CatMetro.Presentation.Hud;
using CatMetro.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    public sealed class HomeCameraTransitionTests
    {
        private static readonly Rect PhoneSafe = new Rect(0, 64, 917, 1920);
        private static readonly Rect PhoneViewport = new Rect(0, 0, 917, 2048);
        private GameRoot _root;
        private RenderTexture _target;
        private string _storagePath;
        private float _timeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _timeScale = Time.timeScale;
            Time.timeScale = 0f;
            _storagePath = Path.Combine(Path.GetTempPath(), "cm-home-camera-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_storagePath);
            GameRoot.DailyStorageRootOverride = () => new Storage(_storagePath);
            GameRoot.DevSkipShippedHome = false;
            GameRoot.DailyEntryUnlocked = false;
            _root = GameRoot.Launch();
            _target = new RenderTexture(917, 2048, 24);
            _target.Create();
            _root.Cam.targetTexture = _target;
            _root.Cam.aspect = 917f / 2048f;
            _root.Cam.cullingMask = 0; // projection proof only; no worktree art render
            yield return null;
            _root.enabled = false;
            LayoutPhone();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            yield return null;
            if (_target != null) { _target.Release(); Object.Destroy(_target); }
            GameRoot.DevSkipShippedHome = false;
            GameRoot.DailyEntryUnlocked = false;
            GameRoot.DailyStorageRootOverride = null;
            CatMetro.Services.Purchases.PurchaseRuntime.ResetForTests();
            CatMetro.Services.Cosmetics.CosmeticRuntime.ResetForTests();
            Time.timeScale = _timeScale;
            if (Directory.Exists(_storagePath)) Directory.Delete(_storagePath, true);
        }

        [Test]
        public void ShownHome_ContainsTheCompleteFrameInsideTheLivePhoneWindow()
        {
            AssertFrameInsideWindow();
            Assert.That(_root.Session.State.Tick, Is.Zero);
        }

        [Test]
        public void IntroPlay_FadesChromeOverPointTwoFiveSeconds_AfterDroppingItsInput()
        {
            var frame = _root.Home.transform.Find("HeroCard/DioramaFrameTop")
                .GetComponent<UnityEngine.UI.Image>();
            var wardrobeFace = _root.Wardrobe.transform.Find("WardrobeCapsule/WardrobeButtonFace")
                .GetComponent<UnityEngine.UI.Image>();
            _root.Home.LevelSelected.Invoke();
            Assert.That(_root.Home.IsVisible, Is.False);
            Assert.That(_root.Wardrobe.EntryVisible, Is.False);
            Assert.That(_root.Input.Regions.IsRegistered("home.pin.l001"), Is.False);
            Assert.That(_root.Input.Regions.IsRegistered("wardrobe.entry"), Is.False);
            Assert.That(frame.gameObject.activeInHierarchy, Is.True, "paint fades after input is removed");
            var fx = _root.GetComponent<BoardFx>();
            Assert.That(fx, Is.Not.Null);
            fx.Advance(1f); // Reading the intro must not consume the Play dolly fade.
            _root.Intro.PlayRequested.Invoke();
            fx.Advance(.125f);
            Assert.That(frame.canvasRenderer.GetAlpha(), Is.EqualTo(.5f).Within(.01f));
            Assert.That(wardrobeFace.canvasRenderer.GetAlpha(), Is.EqualTo(.5f).Within(.01f));
            fx.Advance(.125f);
            Assert.That(frame.gameObject.activeInHierarchy, Is.False);
            Assert.That(wardrobeFace.gameObject.activeInHierarchy, Is.False);
            _root.Home.Show();
            _root.Wardrobe.ShowEntry();
            Assert.That(frame.canvasRenderer.GetAlpha(), Is.EqualTo(1f));
            Assert.That(wardrobeFace.canvasRenderer.GetAlpha(), Is.EqualTo(1f));
        }

        [Test]
        public void MotionOff_IntroPlayHidesChromeImmediately()
        {
            _root.MotionOffToggle = true;
            _root.Home.LevelSelected.Invoke();
            _root.Intro.PlayRequested.Invoke();
            Assert.That(_root.Home.gameObject.activeSelf, Is.False);
            Assert.That(_root.Wardrobe.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void RelayoutAndWardrobeReturn_RefitWithoutAccumulatingAnOffset()
        {
            var firstPosition = _root.Cam.transform.position;
            float firstSize = _root.Cam.orthographicSize;
            _root.Wardrobe.OpenRequested.Invoke();
            _root.Cam.transform.position += Vector3.up * 3f;
            _root.Wardrobe.BackRequested.Invoke();
            LayoutPhone();
            AssertFrameInsideWindow();
            AssertPose(firstPosition, firstSize);
            _root.Home.LayoutForViewport(new Rect(30, 90, 840, 1780), 408, PhoneViewport);
            AssertFrameInsideWindow();
            LayoutPhone();
            AssertPose(firstPosition, firstSize);
        }

        [Test]
        public void IntroPlay_DolliesOverPointFourSeconds_UsingTheUnscaledClock()
        {
            PlayPose(out var playPosition, out float playSize);
            var homePosition = _root.Cam.transform.position;
            float homeSize = _root.Cam.orthographicSize;
            Assert.That(Mathf.Abs(homeSize - playSize), Is.GreaterThan(.1f), "the two compositions differ");
            _root.Home.LevelSelected.Invoke();
            AssertPose(homePosition, homeSize);
            _root.Intro.PlayRequested.Invoke();
            Assert.That(_root.ScreensVisible, Is.False);
            AssertPose(homePosition, homeSize);
            var fx = _root.GetComponent<BoardFx>();
            Assert.That(fx, Is.Not.Null, "GameRoot uses the shared presentation clock");
            fx.Advance(.2f);
            AssertPose(Vector3.Lerp(homePosition, playPosition, .5f), Mathf.Lerp(homeSize, playSize, .5f));
            fx.Advance(.2f);
            AssertPose(playPosition, playSize);
            Assert.That(_root.Session.State.Tick, Is.Zero, "presentation does not advance the paused simulation");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MotionOff_SettlesThePlayPose_AtStartOrDuringTheDolly(bool fromStart)
        {
            PlayPose(out var playPosition, out float playSize);
            _root.MotionOffToggle = fromStart;
            _root.Home.LevelSelected.Invoke();
            _root.Intro.PlayRequested.Invoke();
            if (!fromStart)
            {
                var fx = _root.GetComponent<BoardFx>();
                Assert.That(fx, Is.Not.Null);
                fx.Advance(.1f);
                _root.MotionOffToggle = true;
                fx.Advance(.001f);
            }
            AssertPose(playPosition, playSize);
        }

        [Test]
        public void ShowingHomeDuringTheDolly_CancelsTheOldPoseAndRefits()
        {
            _root.Home.LevelSelected.Invoke();
            _root.Intro.PlayRequested.Invoke();
            var fx = _root.GetComponent<BoardFx>();
            Assert.That(fx, Is.Not.Null);
            fx.Advance(.1f);
            _root.Home.Show();
            LayoutPhone();
            var position = _root.Cam.transform.position;
            float size = _root.Cam.orthographicSize;
            fx.Advance(1f);
            AssertPose(position, size);
            AssertFrameInsideWindow();
        }

        [Test]
        public void RetryDuringTheDolly_SettlesAtTheCoveredLoad_WithoutReplayingHomePose()
        {
            _root.Home.LevelSelected.Invoke();
            _root.Intro.PlayRequested.Invoke();
            var fx = _root.GetComponent<BoardFx>();
            Assert.That(fx, Is.Not.Null);
            fx.Advance(.1f);
            var previousSession = _root.Session;
            _root.Retry();
            Assert.That(_root.IsTransitioning, Is.True);
            Assert.That(_root.Session, Is.SameAs(previousSession), "retry waits for D's opaque veil");
            var veil = _root.GetComponent<ScreenChromeController>().Transition;
            veil.Advance(TransitionVeil.CoverSeconds, false);
            Assert.That(veil.Alpha, Is.EqualTo(1f));
            Assert.That(_root.Session, Is.Not.SameAs(previousSession), "the covered load replaces the board");
            var position = _root.Cam.transform.position;
            float size = _root.Cam.orthographicSize;
            fx.Advance(.1f);
            AssertPose(position, size);
            veil.Advance(TransitionVeil.RevealSeconds, false);
            Assert.That(_root.IsTransitioning, Is.False);
            AssertPose(position, size);
        }

        [Test]
        public void FailureReviewDuringTheDolly_KeepsTheCauseCameraPose()
        {
            _root.Home.LevelSelected.Invoke();
            _root.Intro.PlayRequested.Invoke();
            var fx = _root.GetComponent<BoardFx>();
            fx.Advance(.1f);
            _root.Session.AdvanceMs(200 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            _root.MotionOffToggle = true;
            typeof(GameRoot).GetMethod("Update", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).Invoke(_root, null);
            Assert.That(_root.ScreenState, Is.EqualTo("FailureReview"));
            var position = _root.Cam.transform.position;
            float size = _root.Cam.orthographicSize;
            fx.Advance(1f);
            AssertPose(position, size);
        }

        private void LayoutPhone()
        {
            _root.Home.LayoutForViewport(PhoneSafe, 408, PhoneViewport);
            Canvas.ForceUpdateCanvases();
        }

        private void PlayPose(out Vector3 position, out float size)
        {
            BoardSceneLook.FitCamera(_root.Cam, _root.View);
            position = _root.Cam.transform.position;
            size = _root.Cam.orthographicSize;
            LayoutPhone();
            Assert.That(Mathf.Abs(_root.Cam.orthographicSize - size), Is.GreaterThan(.1f),
                "Home must restore its own framing after the gameplay baseline is measured");
        }

        private void AssertPose(Vector3 position, float size)
        {
            Assert.That(Vector3.Distance(_root.Cam.transform.position, position), Is.LessThan(.002f));
            Assert.That(_root.Cam.orthographicSize, Is.EqualTo(size).Within(.002f));
        }

        private void AssertFrameInsideWindow()
        {
            // Unity updates a ScreenSpaceCamera canvas's world scale on camera render.
            // Settle that native step before measuring the painted window after a zoom.
            _root.Cam.Render();
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            _root.Home.DioramaWindowTransform.GetWorldCorners(corners);
            var min = _root.Cam.WorldToViewportPoint(corners[0]);
            var max = _root.Cam.WorldToViewportPoint(corners[2]);
            Assert.That(max.x - min.x, Is.GreaterThan(.7f));
            Assert.That(max.y - min.y, Is.GreaterThan(.4f));
            var desk = _root.View.transform.Find("DeskSurface");
            int renderers = 0;
            foreach (var renderer in _root.View.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || renderer.transform.IsChildOf(desk)) continue;
                renderers++;
                var b = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = _root.Cam.WorldToViewportPoint(new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z));
                    Assert.That(p.x, Is.InRange(min.x, max.x), renderer.name + " horizontal edge");
                    Assert.That(p.y, Is.InRange(min.y, max.y), renderer.name + " vertical edge");
                    Assert.That(p.z, Is.InRange(_root.Cam.nearClipPlane, _root.Cam.farClipPlane), renderer.name);
                }
            }
            Assert.That(renderers, Is.GreaterThan(10), "the real runtime-built board supplies the bounds");
            TestContext.Progress.WriteLine($"HOME_FIT window=({min.x:R},{min.y:R})-({max.x:R},{max.y:R}) "
                + $"orthographicSize={_root.Cam.orthographicSize:R} renderers={renderers}");
        }

        private sealed class Storage : IStorageRoot
        {
            public string SaveDirectory { get; }
            public string CacheDirectory => SaveDirectory;
            public Storage(string path) { SaveDirectory = path; }
        }
    }
}
