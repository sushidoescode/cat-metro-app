using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CatMetro.Application.Save;
using CatMetro.Application.Session;
using CatMetro.Bootstrap;
using CatMetro.Bootstrap.DevCapture;
using CatMetro.Content;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Theme;
using CatMetro.Services;
using CatMetro.Services.Ads;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.PlayMode
{
    public sealed class FailureRewindTests
    {
        private const string RegionId = "failure.rewind";
        private const string Placement = "rewind_failure";
        private GameRoot _root;
        private FakeAds _ads;
        private Sink _sink;
        private int _sceneLoads;
        private string _temporary;
        private Vector3 _playCameraPosition;
        private Quaternion _playCameraRotation;
        private float _playCameraSize;

        private sealed class Sink : IAnalytics
        {
            public readonly List<AnalyticsEvent> Events = new List<AnalyticsEvent>();
            public Action<AnalyticsEvent> OnLog;
            public int QueuedEventCount => Events.Count;
            public void Log(in AnalyticsEvent e)
            {
                Events.Add(e);
                OnLog?.Invoke(e);
            }
            public void SetUserProperty(UserPropertyKey key, string value) { }
        }

        private sealed class FakeAds : IRewardedAds, IRewardedAdFailureRewindSource
        {
            public event Action AvailabilityChanged;
            public bool Available = true;
            private bool _displayed;
            public int Requests;
            public long Abandoned;
            public string RequestedPlacement;
            public Action<RewardedAdEvent> Lifecycle;
            public Action<FailureRewindAdCompletion> Completed;
            public bool CanShow(string placementId) => false;
            public RewardedShowOutcome Show(string placementId) => RewardedShowOutcome.Unavailable;
            public bool CanShowFailureRewind(string placementId) => Available && placementId == Placement;
            public bool CanContinueFailureRewind(long attemptId, string placementId) =>
                attemptId == Requests && placementId == RequestedPlacement && (Available || _displayed);
            public RewardedShowOutcome ShowFailureRewind(string placementId,
                Action<RewardedAdEvent> lifecycle, Action<FailureRewindAdCompletion> completed,
                out long attemptId)
            {
                attemptId = ++Requests;
                _displayed = false;
                RequestedPlacement = placementId;
                Lifecycle = lifecycle;
                Completed = completed;
                return RewardedShowOutcome.Started;
            }
            public void AbandonFailureRewind(long attemptId) => Abandoned = attemptId;
            public void SetAvailable(bool available)
            {
                Available = available;
                AvailabilityChanged?.Invoke();
            }
            public void Display()
            {
                _displayed = true;
                Lifecycle(new RewardedAdEvent(RewardedAdEventKind.Displayed,
                    Requests, Placement, "actual-unit", networkName: "actual-network"));
            }
            public void Finish(RewardedAdCompletionKind kind, bool metadata = true) =>
                Completed(new FailureRewindAdCompletion(Requests, Placement, kind,
                    metadata ? "actual-unit" : null, metadata ? "actual-network" : null,
                    metadata ? 731 : (int?)null));
        }

        [SetUp]
        public void SetUp()
        {
            RewardedAdRuntime.ResetForTests();
            SaveRuntime.ResetForTests();
            _sink = new Sink();
            _sceneLoads = 0;
            Time.timeScale = 0f;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [TearDown]
        public void TearDown()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
            RewardedAdRuntime.ResetForTests();
            SaveRuntime.ResetForTests();
            Time.timeScale = 1f;
            if (_temporary != null)
            {
                GameRoot.AnalyticsRuntimeFactory = null;
                GameRoot.DailyStorageRootOverride = null;
                GameRoot.MessagingFactoryOverride = null;
                GameRoot.DevSkipShippedHome = false;
                DevLevelOverride.DirectoryOverride = null;
                DevBootOverride.DirectoryOverride = null;
                Directory.Delete(_temporary, true);
                _temporary = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => _sceneLoads++;
        private int Count(string name) => _sink.Events.Count(e => e.Name == name);
        private JObject Event(string name) => _sink.Events.Single(e => e.Name == name).Params;
        private Transform Offer => _root.transform.Find("FailureRewindCanvas/FailureRewindOffer");
        private Vector2 OfferPoint => new Vector2(Screen.safeArea.center.x,
            HudBands.ThumbBand(Screen.safeArea).yMax + 24f * HudBands.PxPerDp(Screen.dpi));

        private IEnumerator Fail(bool configured = true, bool decision = true, bool shippedFlow = false)
        {
            if (configured)
            {
                _ads = new FakeAds();
                RewardedAdRuntime.Install(_ads);
            }
            var imported = LevelImporter.Import(Encoding.UTF8.GetBytes(Fixture));
            Assert.That(imported.Ok, Is.True, imported.Error?.ToString());
            if (shippedFlow)
            {
                _temporary = Path.Combine(Path.GetTempPath(), "cm-rewind-flow-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_temporary);
                File.WriteAllText(Path.Combine(_temporary, "level.json"), Fixture);
                DevLevelOverride.DirectoryOverride = _temporary;
                DevBootOverride.DirectoryOverride = _temporary;
                GameRoot.DailyStorageRootOverride = () => new FlowStorage(_temporary);
                GameRoot.AnalyticsRuntimeFactory = () => new GameAnalyticsRuntime(_sink);
                GameRoot.MessagingFactoryOverride = () => new InertMessaging();
                GameRoot.DevSkipShippedHome = false;
                _root = GameRoot.Launch();
                _root.MotionOffToggle = false;
                yield return null;
                Assert.That(_root.Input.HandleTapAtScreen(_root.Home.PinPaintedRectPx.center), Is.EqualTo(-3));
                var fx = _root.GetComponent<CatMetro.Presentation.Fx.BoardFx>();
                fx.Advance(.14f);
                Assert.That(_root.Intro.IsVisible, Is.True, "finish the Home press before tapping Play");
                Assert.That(_root.Input.HandleTapAtScreen(_root.Intro.PlayChipRectPx.center), Is.EqualTo(-3));
                fx.Advance(.14f);
                Assert.That(_root.ScreensVisible, Is.False);
                // Play begins Home's dolly; rewind must restore the settled gameplay pose.
                fx.Advance(.4f);
            }
            else
            {
                _root = GameRoot.LaunchWith(imported.Value, new GameAnalyticsRuntime(_sink));
                _root.MotionOffToggle = true;
            }
            _playCameraPosition = _root.Cam.transform.position;
            _playCameraRotation = _root.Cam.transform.rotation;
            _playCameraSize = _root.Cam.orthographicSize;
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.False);
            Assert.That(Offer, Is.Null, "no offer while Playing");
            _root.Session.AdvanceMs(5 * TickInterpolator.TICK_MS);
            if (decision) Assert.That(_root.Session.EnqueueToggle(0), Is.True);
            _root.Session.AdvanceMs(100 * TickInterpolator.TICK_MS);
            yield return null;
            yield return null;
            Assert.That(_root.ScreenState, Is.EqualTo("FailureReview"));
        }

        private void TapOffer()
        {
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.True,
                "eligible failure must expose a rewind region");
            Assert.That(_root.Input.HandleTapAtScreen(OfferPoint), Is.EqualTo(-3));
            Assert.That(_ads.RequestedPlacement, Is.EqualTo(Placement));
        }

        private void OpenGameplayPause()
        {
            Assert.That(_root.Input.Regions.IsRegistered("game.pause"), Is.True);
            var pin = _root.GetComponentsInChildren<RectTransform>().Single(t => t.name == "PausePin");
            _root.Input.HandleTapAtScreen(_root.Cam.WorldToScreenPoint(pin.TransformPoint(pin.rect.center)));
            Assert.That(_root.Stack.Current, Is.EqualTo("pause"));
        }

        private void TapPauseLabel(string text)
        {
            var label = _root.GetComponentsInChildren<TMP_Text>().Single(t => t.text == text);
            _root.Input.HandleTapAtScreen(_root.Cam.WorldToScreenPoint(
                label.rectTransform.TransformPoint(label.rectTransform.rect.center)));
        }

        [UnityTest]
        public IEnumerator GameplayHome_CancelsDisplayedRewindBeforeTheVeil_AndRejectsLateGrant()
        {
            yield return Fail(shippedFlow: true);
            var failed = _root.Session;
            TapOffer();
            _ads.Display();
            OpenGameplayPause();
            Assert.That(_ads.Abandoned, Is.EqualTo(1), "the stacked sheet invalidates a pending reward route");
            TapPauseLabel("Home");
            var veil = _root.GetComponent<ScreenChromeController>().Transition;
            Assert.That(veil.IsInFlight, Is.True);
            _ads.Finish(RewardedAdCompletionKind.Granted);
            Assert.That(_root.Session, Is.SameAs(failed), "a stale grant cannot replace the covered board");
            veil.Advance(.22f, false);
            var homeSession = _root.Session;
            Assert.That(homeSession, Is.Not.SameAs(failed));
            _ads.Finish(RewardedAdCompletionKind.Granted);
            Assert.That(_root.Session, Is.SameAs(homeSession));
            for (int i = 0; i < 4; i++) yield return null;
            Assert.That(_root.Home.IsVisible, Is.True);
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.False);
            Assert.That(Count("rewind_used"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator GameplayPauseResume_ReoffersForTheSameFailure_AndRetryKeepsFirstClaim()
        {
            yield return Fail(shippedFlow: true);
            var failed = _root.Session;
            OpenGameplayPause();
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.False);
            _root.MotionOffToggle = true;
            TapPauseLabel("Resume");
            Assert.That(_root.Session, Is.SameAs(failed));
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.True);
            int before = _ads.Requests;
            Assert.That(_root.Input.HandleTapAtScreen(new Vector2(Screen.width * .5f, 1)), Is.EqualTo(-2));
            Assert.That(_root.Session, Is.Not.SameAs(failed));
            Assert.That(_ads.Requests, Is.EqualTo(before), "Retry never falls through to the offer");
        }

        [UnityTest]
        public IEnumerator ShippedRetry_DeclinesAndRemovesOfferBeforeTheDelayedRebuild()
        {
            yield return Fail(shippedFlow: true);
            var failed = _root.Session;
            var offerCanvas = Offer.GetComponentInParent<Canvas>();
            _root.Retry();
            var veil = _root.GetComponent<ScreenChromeController>().Transition;
            Assert.That(veil.IsInFlight, Is.True);
            Assert.That(_root.Session, Is.SameAs(failed), "the old board remains until cover is opaque");
            Assert.That(Count("ad_offer_declined"), Is.EqualTo(1), "decline belongs to the tap, not the rebuild");
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.False);
            Assert.That(offerCanvas.gameObject.activeSelf, Is.False);
            _ads.SetAvailable(true);
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.False,
                "availability must not reopen an offer during the cover");
            _root.Retry();
            Assert.That(Count("ad_offer_declined"), Is.EqualTo(1));
            veil.Advance(.11f, false);
            Assert.That(_root.Session, Is.SameAs(failed));
            veil.Advance(.11f, false);
            Assert.That(veil.Alpha, Is.EqualTo(1f));
            Assert.That(_root.Session, Is.Not.SameAs(failed));
            Assert.That(_root.Session.State.Tick, Is.Zero);
            Assert.That(Count("level_started"), Is.EqualTo(2), "initial play plus one committed retry");
        }

        [UnityTest]
        public IEnumerator ShippedRetry_CancelsDisplayedRequestBeforeVeilMidpoint()
        {
            yield return Fail(shippedFlow: true);
            var failed = _root.Session;
            TapOffer();
            _ads.Display();
            _root.Retry();
            var veil = _root.GetComponent<ScreenChromeController>().Transition;
            Assert.That(veil.IsInFlight, Is.True);
            Assert.That(_root.Session, Is.SameAs(failed));
            Assert.That(_ads.Abandoned, Is.EqualTo(1), "cancel on the tap while the old board still exists");
            Assert.That(Count("ad_offer_declined"), Is.Zero);
            _ads.Finish(RewardedAdCompletionKind.Granted);
            _ads.Display();
            Assert.That(_root.Session, Is.SameAs(failed), "late reward cannot replace the pending Retry");
            Assert.That(Count("rewind_used"), Is.Zero);
            Assert.That(Count("rewarded_ad_completed"), Is.Zero);
            veil.Advance(.22f, false);
            var retried = _root.Session;
            Assert.That(retried, Is.Not.SameAs(failed));
            Assert.That(retried.State.Tick, Is.Zero);
            Assert.That(retried.HasUsedRewind, Is.False);
            veil.Advance(.28f, false);
            _ads.Finish(RewardedAdCompletionKind.Granted);
            Assert.That(_root.Session, Is.SameAs(retried));
        }

        [UnityTest]
        public IEnumerator DisableDuringRetryCover_CancelsPendingBoardRebuild()
        {
            yield return Fail(shippedFlow: true);
            var failed = _root.Session;
            _root.Retry();
            var veil = _root.GetComponent<ScreenChromeController>().Transition;
            Assert.That(veil.IsInFlight, Is.True);
            _root.enabled = false;
            Assert.That(veil.IsInFlight, Is.False, "a disabled flow owner cannot retain a rebuild callback");
            veil.Advance(1f, false);
            Assert.That(_root.Session, Is.SameAs(failed));
            Assert.That(_root.Input.Regions.IsRegistered("transition.cover"), Is.False);
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.False);
            _root.enabled = true;
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.True);
        }

        [UnityTest]
        public IEnumerator ShippedGrant_InstallsExactPreparedSessionWithMotionEnabled()
        {
            yield return Fail(shippedFlow: true);
            var candidate = (GameSession)typeof(GameRoot)
                .GetField("_failureRewindCandidate", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_root);
            Assert.That(candidate, Is.Not.Null);
            var before = Digest(candidate);
            TapOffer();
            _ads.Display();
            _ads.Finish(RewardedAdCompletionKind.Granted);
            Assert.That(_root.Session, Is.SameAs(candidate),
                "an earned rewind installs the prepared object immediately, without a cancellable deferred grant");
            Assert.That(Digest(_root.Session), Is.EqualTo(before));
            Assert.That(_root.Session.State.Tick, Is.EqualTo(5));
            Assert.That(_root.Session.HasUsedRewind, Is.True);
            var veil = _root.GetComponent<ScreenChromeController>().Transition;
            Assert.That(veil.IsInFlight, Is.False);
            veil.Advance(.5f, false);
            _ads.Finish(RewardedAdCompletionKind.Granted);
            Assert.That(_root.Session, Is.SameAs(candidate));
            Assert.That(Count("rewind_used"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SuccessfulRewind_ClearsExistingFailureMoodAndRestoresPlayCamera()
        {
            yield return Fail(shippedFlow: true);
            var mood = _root.CauseCam.GetComponent<UnityEngine.Rendering.Volume>();
            Assert.That(mood, Is.Not.Null);
            _root.CauseCam.GetComponent<CatMetro.Presentation.Fx.BoardFx>().Advance(.35f);
            Assert.That(mood.sharedProfile.TryGet<UnityEngine.Rendering.Universal.ColorAdjustments>(out var colour), Is.True);
            Assert.That(colour.saturation.value, Is.EqualTo(-35f));
            // TimeOut has no causal node; stage the existing fail framing so the reset
            // assertions exercise a visible ring and displaced camera rather than a no-op.
            _root.CauseCam.FrameNode("SRC", _root.View.NodeWorldPos(0), true);
            Assert.That(_root.CauseCam.RingVisible, Is.True);
            Assert.That(_root.Banner.Visible, Is.True);
            Assert.That(_root.GetComponent<ScreenChromeController>().Cta.IsVisible, Is.True);
            Assert.That(Vector3.Distance(_root.Cam.transform.position, _playCameraPosition),
                Is.GreaterThan(.1f));
            TapOffer();
            _ads.Finish(RewardedAdCompletionKind.Granted);
            Assert.That(_root.ScreenState, Is.EqualTo("Playing"));
            Assert.That(mood.weight, Is.Zero, "the prepared rewind clears the real failure volume");
            Assert.That(colour.saturation.value, Is.Zero);
            Assert.That(_root.Banner.Visible, Is.False);
            Assert.That(_root.CauseCam.RingVisible, Is.False);
            Assert.That(_root.CauseCam.TargetNodeId, Is.Empty);
            Assert.That(Vector3.Distance(_root.Cam.transform.position, _playCameraPosition), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(_root.Cam.transform.rotation, _playCameraRotation), Is.LessThan(.001f));
            Assert.That(_root.Cam.orthographicSize, Is.EqualTo(_playCameraSize).Within(.001f));
            yield return null;
            Assert.That(_root.GetComponent<ScreenChromeController>().Cta.IsVisible, Is.False);
            Assert.That(_root.GetComponent<ResultsPanel>().IsVisible, Is.False);
            Assert.That(_root.Audio.CelebratePending, Is.False);
        }

        [UnityTest]
        public IEnumerator RetryAndBannerStayClearOfTheFullWidthRewindStrip()
        {
            yield return Fail();
            var view = Offer.GetComponent<FailureRewindOfferView>();
            var band = HudBands.ThumbBand(Screen.safeArea);
            Assert.That(view.PaintedRectPx, Is.EqualTo(FailureRewindOfferView.ChipRect(Screen.safeArea, Screen.dpi)));
            Assert.That(view.PaintedRectPx.x, Is.EqualTo(Screen.safeArea.x));
            Assert.That(view.PaintedRectPx.width, Is.EqualTo(Screen.safeArea.width));
            Assert.That(view.PaintedRectPx.yMin, Is.EqualTo(band.yMax));
            Assert.That(view.PaintedRectPx.height, Is.EqualTo(48f * HudBands.PxPerDp(Screen.dpi)));
            var retry = _root.GetComponent<ScreenChromeController>().Cta;
            Assert.That(retry.PaintedRectPx, Is.EqualTo(band));
            Assert.That(view.PaintedRectPx.Overlaps(retry.FaceRectPx), Is.False);
            Assert.That(view.PaintedRectPx.Overlaps(_root.Banner.PaintedRectPx), Is.False);
            foreach (var phone in new[] { new Vector3(917, 2048, 408), new Vector3(1179, 2556, 460),
                new Vector3(480, 1080, 240) })
            {
                var safe = new Rect(0, 64, phone.x, phone.y - 128);
                var strip = FailureRewindOfferView.ChipRect(safe, phone.z);
                retry.LayoutForViewport(safe, phone.z);
                _root.Banner.LayoutForViewport(safe, phone.z);
                Assert.That(retry.FaceRectPx.yMax + 6f * HudBands.PxPerDp(phone.z),
                    Is.LessThan(strip.yMin), "the halo stays below the offer too");
                Assert.That(strip.Overlaps(_root.Banner.PaintedRectPx), Is.False);
                Assert.That(retry.PaintedRectPx.yMax, Is.EqualTo(strip.yMin));
            }
            CaptureOfferUiOnlyIfRequested(view);
        }

        private sealed class FlowStorage : IStorageRoot
        {
            public string SaveDirectory { get; }
            public string CacheDirectory => SaveDirectory;
            public FlowStorage(string directory)
            {
                SaveDirectory = Path.Combine(directory, "save");
                Directory.CreateDirectory(SaveDirectory);
            }
        }

        private sealed class InertMessaging : IMessaging
        {
            public bool IsAvailable => false;
            public string SubscriptionId => string.Empty;
            public MessagingPermission Permission => MessagingPermission.Unknown;
            public bool CanRequestPermission => false;
            public event Action<MessagingRoute> LinkOpened { add { } remove { } }
            public Task<MessagingPermission> PromptAsync(bool fallbackToSettings, CancellationToken cancellationToken)
                => Task.FromResult(MessagingPermission.Unknown);
            public void Schedule(DailyChallengeNotification notification) { }
            public void Cancel(string notificationId) { }
            public void Dispose() { }
        }

        private void CaptureOfferUiOnlyIfRequested(FailureRewindOfferView offer)
        {
            string path = Environment.GetEnvironmentVariable("CM_REWIND_FLOW_UI_CAPTURE");
            if (string.IsNullOrEmpty(path)) return;
            var camera = _root.Cam;
            var priorTarget = camera.targetTexture;
            var priorActive = RenderTexture.active;
            int priorMask = camera.cullingMask;
            var target = new RenderTexture(917, 2048, 24);
            var pixels = new Texture2D(917, 2048, TextureFormat.RGB24, false);
            try
            {
                // Only these three UI canvases enter this frame. No board or licensed art
                // is rendered, so this geometry review can run in the isolated worktree.
                foreach (var canvas in new[] { offer.GetComponentInParent<Canvas>(),
                    _root.Banner.GetComponent<Canvas>(),
                    _root.GetComponent<ScreenChromeController>().Cta.GetComponentInParent<Canvas>() })
                    foreach (var child in canvas.GetComponentsInChildren<Transform>(true))
                        child.gameObject.layer = 5;
                camera.cullingMask = 1 << 5;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Palette.WarmPaper;
                camera.targetTexture = target;
                camera.aspect = 917f / 2048f;
                var safe = new Rect(0, 64, 917, 1920);
                _root.Banner.LayoutForViewport(safe, 408f);
                _root.GetComponent<ScreenChromeController>().Cta.LayoutForViewport(safe, 408f);
                var rect = offer.GetComponent<RectTransform>();
                var strip = FailureRewindOfferView.ChipRect(safe, 408f);
                rect.anchoredPosition = strip.position;
                rect.sizeDelta = strip.size;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 917, 2048), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = priorTarget;
                camera.cullingMask = priorMask;
                RenderTexture.active = priorActive;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
        }

        [UnityTest]
        public IEnumerator NoProviderHasNoOfferTreeRegionGapOrDeadTargetAndStillRetries()
        {
            yield return Fail(configured: false);
            Assert.That(_root.GetComponentsInChildren<Transform>(true)
                .Any(t => t.name.Contains("FailureRewind")), Is.False);
            Assert.That(_root.GetComponentsInChildren<Canvas>(true)
                .Any(c => c.name.Contains("FailureRewind")), Is.False);
            Assert.That(_root.GetComponentsInChildren<FailureRewindOfferView>(true), Is.Empty);
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.False);
            var retry = _root.GetComponent<ScreenChromeController>().Cta;
            Assert.That(retry.PaintedRectPx, Is.EqualTo(HudBands.ThumbBand(Screen.safeArea)));
            Assert.That(_root.Input.HandleTapAtScreen(OfferPoint), Is.EqualTo(-1),
                "the optional area has no consuming dead target");
            Assert.That(_root.Input.HandleTapAtScreen(new Vector2(Screen.width / 2f,
                Screen.height * .1f)), Is.EqualTo(-2));
            Assert.That(_root.ScreenState, Is.EqualTo("Playing"));
            Assert.That(_root.Session.State.Tick, Is.Zero);
            Assert.That(Count("ad_offer_viewed"), Is.Zero);
            Assert.That(_sceneLoads, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AvailableOfferIsLocalized48DpOutsideRetryWithOneRegionAndViewEvent()
        {
            yield return Fail();
            Assert.That(Offer, Is.Not.Null, "eligible failure needs a visible offer");
            Assert.That(Offer.GetComponentInParent<RetryCtaView>(), Is.Null);
            Assert.That(Offer.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Watch to rewind"));
            var rect = Offer.GetComponent<RectTransform>();
            Assert.That(rect.anchoredPosition.y, Is.EqualTo(HudBands.ThumbBand(Screen.safeArea).yMax));
            Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(48f * HudBands.PxPerDp(Screen.dpi)));
            Assert.That(_root.Input.Regions.Count, Is.EqualTo(1));
            yield return null;
            _ads.SetAvailable(true);
            Assert.That(Count("ad_offer_viewed"), Is.EqualTo(1));
            Assert.That((string)Event("ad_offer_viewed")["placement"], Is.EqualTo(Placement));
            CaptureOfferIfRequested();
        }

        [UnityTest]
        public IEnumerator FailureWithoutDecisionCannotOfferEvenWithProvider()
        {
            yield return Fail(decision: false);
            Assert.That(Offer, Is.Null);
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.False);
            Assert.That(Count("ad_offer_viewed"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator GrantRebuildsPreparedSessionOnceWithNoLaterReceiptOrSceneLoad()
        {
            yield return Fail();
            var failed = _root.Session;
            var before = Digest(failed);
            var oldView = _root.View;
            var oldPreview = _root.Preview;
            TapOffer();
            Assert.That(_root.Session, Is.SameAs(failed));
            Assert.That(Count("rewarded_ad_started"), Is.Zero, "request is not Displayed");
            _root.Input.HandleTapAtScreen(OfferPoint);
            Assert.That(_ads.Requests, Is.EqualTo(1));
            _ads.Display();
            _ads.Display();
            Assert.That(Count("rewarded_ad_started"), Is.EqualTo(1));
            Assert.That((string)Event("rewarded_ad_started")["network"], Is.EqualTo("actual-network"));
            Assert.That((string)Event("rewarded_ad_started")["ad_unit"], Is.EqualTo("actual-unit"));
            var observedPlaying = new List<string>();
            _sink.OnLog = e =>
            {
                if (e.Name == "rewarded_ad_completed" || e.Name == "rewind_used")
                    observedPlaying.Add(_root.ScreenState + ":" + _root.Session.State.Tick);
            };
            _ads.Finish(RewardedAdCompletionKind.Granted);
            var rewound = _root.Session;
            Assert.That(rewound, Is.Not.SameAs(failed));
            Assert.That(rewound.Level, Is.SameAs(failed.Level));
            Assert.That(Digest(failed), Is.EqualTo(before));
            Assert.That(rewound.State.Tick, Is.EqualTo(5));
            Assert.That(rewound.Log.Entries, Is.Empty);
            Assert.That(rewound.HasUsedRewind, Is.True);
            Assert.That(_root.ScreenState, Is.EqualTo("Playing"));
            Assert.That(_root.View, Is.Not.SameAs(oldView));
            Assert.That(_root.Preview, Is.Not.SameAs(oldPreview));
            Assert.That(_root.Banner.Visible, Is.False);
            Assert.That(_root.CauseCam.RingVisible, Is.False);
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.False);
            Assert.That(Count("level_started"), Is.EqualTo(1), "rewind is not a new attempt");
            Assert.That(Count("level_retried"), Is.Zero);
            Assert.That((string)Event("rewarded_ad_completed")["reward_type"], Is.EqualTo("rewind"));
            Assert.That((string)Event("rewarded_ad_completed")["network"], Is.EqualTo("actual-network"));
            Assert.That((int)Event("rewarded_ad_completed")["reward_amount"], Is.EqualTo(1));
            Assert.That((string)Event("rewind_used")["source"], Is.EqualTo("rewarded"));
            Assert.That((string)Event("rewind_used")["balance_after"], Is.EqualTo("0"));
            Assert.That(observedPlaying, Is.EqualTo(new[] { "Playing:5", "Playing:5" }),
                "both success events observe the installed candidate");
            Assert.That(_sink.Events.TakeLast(2).Select(e => e.Name),
                Is.EqualTo(new[] { "rewarded_ad_completed", "rewind_used" }));
            _ads.Finish(RewardedAdCompletionKind.Granted);
            Assert.That(_root.Session, Is.SameAs(rewound));
            Assert.That(Count("rewind_used"), Is.EqualTo(1));
            rewound.AdvanceMs(5 * TickInterpolator.TICK_MS);
            Assert.That(rewound.Log.Entries, Is.Empty, "removed decisions never replay later");
            Assert.That(_root.Input.HandleTapAtScreen(_root.Cam.WorldToScreenPoint(
                _root.View.SwitchWorldPos(0))), Is.EqualTo(0));
            Assert.That(rewound.Log.Entries.Count, Is.EqualTo(1),
                "the rebuilt input records the player's next decision in the rewound session");
            Assert.That(rewound.HasUsedRewind, Is.True);
            Assert.That(_sceneLoads, Is.Zero);
        }

        [UnityTest]
        public IEnumerator NonGrantPreservesExactFailedSessionAndReoffers(
            [Values(RewardedAdCompletionKind.ClosedWithoutReward, RewardedAdCompletionKind.DisplayFailed,
                RewardedAdCompletionKind.GrantFailed, RewardedAdCompletionKind.Cancelled,
                RewardedAdCompletionKind.Unavailable)] RewardedAdCompletionKind kind)
        {
            yield return Fail();
            var failed = _root.Session;
            var state = failed.State;
            var log = failed.Log;
            var receipts = log.Entries.ToArray();
            var before = Digest(failed);
            int tick = state.Tick;
            TapOffer();
            _ads.Finish(kind);
            Assert.That(_root.Session, Is.SameAs(failed));
            Assert.That(failed.State, Is.SameAs(state));
            Assert.That(failed.Log, Is.SameAs(log));
            Assert.That(log.Entries, Is.EqualTo(receipts));
            Assert.That(state.Tick, Is.EqualTo(tick));
            Assert.That(Digest(failed), Is.EqualTo(before));
            Assert.That(_root.ScreenState, Is.EqualTo("FailureReview"));
            Assert.That(Count("rewarded_ad_failed"), Is.EqualTo(1));
            Assert.That((string)Event("rewarded_ad_failed")["network"], Is.EqualTo("actual-network"));
            Assert.That((string)Event("rewarded_ad_failed")["error_code"], Is.EqualTo("731"));
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.True);
            Assert.That(Count("ad_offer_viewed"), Is.EqualTo(2));
            _ads.Finish(kind);
            Assert.That(Count("rewarded_ad_failed"), Is.EqualTo(1));
            Assert.That(Count("rewind_used"), Is.Zero);
            Assert.That(_sceneLoads, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PreDisplayInvalidationUsesStableFallbackAndDoesNotReoffer()
        {
            yield return Fail();
            var failed = _root.Session;
            TapOffer();
            _ads.SetAvailable(false);
            _ads.Finish(RewardedAdCompletionKind.ClosedWithoutReward, metadata: false);
            Assert.That(_root.Session, Is.SameAs(failed));
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.False);
            Assert.That((string)Event("rewarded_ad_failed")["network"], Is.EqualTo("unknown"));
            Assert.That((string)Event("rewarded_ad_failed")["error_code"], Is.EqualTo("Cancelled"));
            yield return null;
            Assert.That(Offer, Is.Null);
        }

        [UnityTest]
        public IEnumerator PreDisplayAvailabilityLossCancelsAndLateGrantPreservesExactFailure()
        {
            yield return Fail();
            var failed = _root.Session;
            var state = failed.State;
            var log = failed.Log;
            var receipts = log.Entries.ToArray();
            var before = Digest(failed);
            TapOffer();
            _ads.SetAvailable(false);
            Assert.That(_ads.Abandoned, Is.EqualTo(1), "availability loss abandons the exact attempt");
            _ads.SetAvailable(false);
            _ads.Finish(RewardedAdCompletionKind.Granted);
            _ads.Finish(RewardedAdCompletionKind.Granted);
            Assert.That(_root.Session, Is.SameAs(failed));
            Assert.That(failed.State, Is.SameAs(state));
            Assert.That(failed.Log, Is.SameAs(log));
            Assert.That(log.Entries, Is.EqualTo(receipts));
            Assert.That(Digest(failed), Is.EqualTo(before));
            Assert.That(_root.ScreenState, Is.EqualTo("FailureReview"));
            Assert.That(Count("rewarded_ad_failed"), Is.EqualTo(1));
            Assert.That((string)Event("rewarded_ad_failed")["error_code"], Is.EqualTo("Cancelled"));
            Assert.That(Count("rewarded_ad_completed"), Is.Zero);
            Assert.That(Count("rewind_used"), Is.Zero);
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.False);
            Assert.That(_sceneLoads, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DisplayedAdStillGrantsWhenTheNextOfferBecomesUnavailable()
        {
            yield return Fail();
            TapOffer();
            _ads.Display();
            _ads.SetAvailable(false);
            Assert.That(_ads.Abandoned, Is.Zero, "next-fill readiness cannot revoke a displayed ad");
            _ads.Finish(RewardedAdCompletionKind.Granted);
            Assert.That(_root.ScreenState, Is.EqualTo("Playing"));
            Assert.That(_root.Session.HasUsedRewind, Is.True);
            Assert.That(Count("rewarded_ad_completed"), Is.EqualTo(1));
            Assert.That(Count("rewarded_ad_failed"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator AvailabilityLossRemovesRegionAndTreeThenRestoresNewPresentation()
        {
            yield return Fail();
            Assert.That(Count("ad_offer_viewed"), Is.EqualTo(1));
            _ads.SetAvailable(false);
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.False);
            yield return null;
            Assert.That(Offer, Is.Null);
            Assert.That(_root.transform.Find("FailureRewindCanvas"), Is.Null);
            _ads.SetAvailable(true);
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.True);
            Assert.That(Count("ad_offer_viewed"), Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator RetryDeclinesVisibleOfferOnceAndRawRetryBandWinsOverlap()
        {
            yield return Fail();
            // Deliberately overlap this feature's real action with the raw retry band.
            Assert.That(_root.Input.Regions.TryResolve(OfferPoint, out var offerAction), Is.True);
            _root.Input.Regions.Unregister(RegionId);
            _root.Input.Regions.Register(RegionId, () => new Rect(0, 0, Screen.width, Screen.height),
                offerAction, ChromeRegions.ParentPriority);
            Assert.That(_root.Input.HandleTapAtScreen(new Vector2(Screen.width / 2f,
                Screen.height * .1f)), Is.EqualTo(-2));
            Assert.That(_ads.Requests, Is.Zero);
            Assert.That(Count("ad_offer_declined"), Is.EqualTo(1));
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.False);
            _root.Retry();
            Assert.That(Count("ad_offer_declined"), Is.EqualTo(1));
            Assert.That(_root.Session.State.Tick, Is.Zero);
            Assert.That(Count("level_started"), Is.EqualTo(3), "both ordinary retries keep analytics");
        }

        [UnityTest]
        public IEnumerator RetryDuringRequestCancelsAndStaleGrantCannotReplaceNewRun()
        {
            yield return Fail();
            TapOffer();
            _ads.Display();
            _root.Retry();
            var retried = _root.Session;
            Assert.That(_ads.Abandoned, Is.EqualTo(1));
            Assert.That(Count("ad_offer_declined"), Is.Zero);
            _ads.Finish(RewardedAdCompletionKind.Granted);
            _ads.Display();
            Assert.That(_root.Session, Is.SameAs(retried));
            Assert.That(Count("rewind_used"), Is.Zero);
            Assert.That(Count("rewarded_ad_started"), Is.EqualTo(1));
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.False);
        }

        [UnityTest]
        public IEnumerator DisableCancelsPendingRequestAndDestroysOffer()
        {
            yield return Fail();
            TapOffer();
            var failed = _root.Session;
            _root.enabled = false;
            Assert.That(_ads.Abandoned, Is.EqualTo(1));
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.False);
            _ads.Finish(RewardedAdCompletionKind.Granted);
            Assert.That(_root.Session, Is.SameAs(failed));
            yield return null;
            Assert.That(_root.transform.Find("FailureRewindCanvas"), Is.Null);
        }

        [UnityTest]
        public IEnumerator GrantReportsUnchangedCurrentSaveBalance()
        {
            using var storage = new RewardedAdsWiringTests.TempStorageRoot();
            var store = new RewardedAdsWiringTests().NewStore(storage);
            store.State.Payload["economy"]["rewindBalance"] = 7;
            SaveRuntime.Install(store);
            yield return Fail();
            TapOffer();
            _ads.Finish(RewardedAdCompletionKind.Granted);
            Assert.That((string)Event("rewind_used")["balance_after"], Is.EqualTo("7"));
            Assert.That((int)store.State.Payload["economy"]["rewindBalance"], Is.EqualTo(7));
        }

        [UnityTest]
        public IEnumerator GrantWithMalformedBalanceStillRewindsAndReportsZero(
            [Values("\"not-a-number\"", "null", "-1", "2147483648", "missing", "{}", "1.5")]
            string savedValue)
        {
            using var storage = new RewardedAdsWiringTests.TempStorageRoot();
            var store = new RewardedAdsWiringTests().NewStore(storage);
            SaveRuntime.Install(store);
            yield return Fail();
            var economy = (JObject)store.State.Payload["economy"];
            if (savedValue == "missing") economy.Remove("rewindBalance");
            else economy["rewindBalance"] = JToken.Parse(savedValue);
            var failed = _root.Session;
            TapOffer();

            Assert.DoesNotThrow(() => _ads.Finish(RewardedAdCompletionKind.Granted));

            Assert.That(_root.Session, Is.Not.SameAs(failed));
            Assert.That(_root.Session.HasUsedRewind, Is.True);
            Assert.That(_root.Session.State.Tick, Is.EqualTo(5));
            Assert.That(_root.ScreenState, Is.EqualTo("Playing"));
            Assert.That(Count("rewarded_ad_completed"), Is.EqualTo(1));
            Assert.That(Count("rewind_used"), Is.EqualTo(1));
            Assert.That(_sink.Events.TakeLast(2).Select(e => e.Name),
                Is.EqualTo(new[] { "rewarded_ad_completed", "rewind_used" }));
            Assert.That((string)Event("rewind_used")["balance_after"], Is.EqualTo("0"));
            Assert.That((string)Event("rewind_used")["source"], Is.EqualTo("rewarded"));
            _ads.Finish(RewardedAdCompletionKind.Granted);
            Assert.That(Count("rewarded_ad_completed"), Is.EqualTo(1));
            Assert.That(Count("rewind_used"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LoadNextCancelsAndDestroyUnregistersWithoutStaleCallbacks()
        {
            yield return Fail();
            TapOffer();
            _root.LoadNext();
            var next = _root.Session;
            Assert.That(_root.CurrentLevelId, Is.EqualTo("L002"));
            Assert.That(_ads.Abandoned, Is.EqualTo(1));
            _ads.Finish(RewardedAdCompletionKind.Granted);
            Assert.That(_root.Session, Is.SameAs(next));
            Assert.That(Count("ad_offer_declined"), Is.Zero);
            Assert.That(Count("rewind_used"), Is.Zero);
            var regions = _root.Input.Regions;
            Object.DestroyImmediate(_root.gameObject);
            Assert.That(regions.IsRegistered(RegionId), Is.False);
            Assert.That(_sceneLoads, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RuntimeUninstallCancelsWithDisplayedMetadataAndNoReoffer()
        {
            yield return Fail();
            TapOffer();
            _ads.Display();
            var failed = _root.Session;
            RewardedAdRuntime.Uninstall(_ads);
            Assert.That(_ads.Abandoned, Is.EqualTo(1));
            Assert.That(_root.Session, Is.SameAs(failed));
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.False);
            Assert.That((string)Event("rewarded_ad_failed")["network"], Is.EqualTo("actual-network"));
            Assert.That((string)Event("rewarded_ad_failed")["error_code"], Is.EqualTo("Cancelled"));
            _ads.Finish(RewardedAdCompletionKind.Granted);
            Assert.That(_root.Session, Is.SameAs(failed));
            Assert.That(Count("rewind_used"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator DisableVisibleOfferRemovesItImmediatelyAndReenableCanOfferSameFailure()
        {
            yield return Fail();
            var failed = _root.Session;
            _root.enabled = false;
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.False);
            yield return null;
            Assert.That(Offer, Is.Null);
            _root.enabled = true;
            yield return null;
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.True,
                "reenabling an existing failure review can present its still-available offer");
            Assert.That(_root.Session, Is.SameAs(failed));
            Assert.That(Count("ad_offer_viewed"), Is.EqualTo(2));
            var regions = _root.Input.Regions;
            Object.DestroyImmediate(_root.gameObject);
            Assert.That(regions.IsRegistered(RegionId), Is.False);
            _ads.SetAvailable(true);
            Assert.That(regions.IsRegistered(RegionId), Is.False);
        }

        [UnityTest]
        public IEnumerator DisablingOfferViewAlsoHidesItsCanvasAndUnregistersTarget()
        {
            yield return Fail();
            Assert.That(_root.Input.Regions.TryResolve(OfferPoint, out var staleTap), Is.True);
            var view = Offer.GetComponent<FailureRewindOfferView>();
            var canvas = view.GetComponentInParent<Canvas>();
            view.enabled = false;
            Assert.That(_root.Input.Regions.IsRegistered(RegionId), Is.False);
            Assert.That(canvas.gameObject.activeSelf, Is.False,
                "a disabled offer must not leave its painted canvas behind");
            staleTap();
            Assert.That(_ads.Requests, Is.Zero, "a stale tap cannot start a hidden offer");
            _root.Retry();
            Assert.That(Count("ad_offer_declined"), Is.Zero, "hidden offers are not declined");
        }

        private static byte[] Digest(GameSession session)
        {
            var bytes = new byte[session.State.DigestLength()];
            session.State.WriteDigest(bytes);
            return bytes;
        }

        private void CaptureOfferIfRequested()
        {
            var path = Environment.GetEnvironmentVariable("CAT_METRO_REWIND_CAPTURE");
            if (string.IsNullOrEmpty(path)) return;
            var target = new RenderTexture(Screen.width, Screen.height, 24);
            var priorTarget = _root.Cam.targetTexture;
            var priorActive = RenderTexture.active;
            var pixels = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            try
            {
                Canvas.ForceUpdateCanvases();
                _root.Cam.targetTexture = target;
                _root.Cam.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                pixels.Apply();
                System.IO.File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                _root.Cam.targetTexture = priorTarget;
                RenderTexture.active = priorActive;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
        }

        // Real TimeOut outcome with a committed decision at tick 5; this fixture mirrors the
        // existing FailureTests quiet-timeout board, so presentation never fabricates failure.
        private const string Fixture = @"{
          ""schemaVersion"":2,""id"":""L001"",""name"":""Rewind fixture"",""seed"":904,
          ""meta"":{""band"":""onboarding"",""difficultyTarget"":0.1,""mechanics"":[""switch""],
            ""newMechanic"":null,""teachingGoal"":""test fixture"",""minActionWindowTicks"":12,""authoredBy"":""llm+validator""},
          ""board"":{""nodes"":[{""id"":""SRC"",""x"":3,""y"":9},{""id"":""J1"",""x"":3,""y"":6},
            {""id"":""RED"",""x"":1,""y"":2},{""id"":""BLU"",""x"":5,""y"":2}],
            ""edges"":[{""id"":""E1"",""from"":""SRC"",""to"":""J1"",""travelTicks"":10},
            {""id"":""E2"",""from"":""J1"",""to"":""RED"",""travelTicks"":12},
            {""id"":""E3"",""from"":""J1"",""to"":""BLU"",""travelTicks"":12}]},
          ""sources"":[{""nodeId"":""SRC"",""allowedColors"":[""red""]}],
          ""stations"":[{""nodeId"":""RED"",""accepts"":[""red""],""capacity"":6},
            {""nodeId"":""BLU"",""accepts"":[""blue""],""capacity"":6}],
          ""switches"":[{""id"":""S1"",""nodeId"":""J1"",""routes"":[""E2"",""E3""],""initialRoute"":0}],
          ""waves"":[{""tick"":100,""sourceNode"":""SRC"",""color"":""red"",""count"":1,""spacingTicks"":1}],
          ""win"":{""deliveries"":99,""timeLimitTicks"":20,""perfectMaxSwitches"":4,""stars"":{""two"":200,""three"":300}},
          ""economy"":{""baseTickets"":20,""perfectBonus"":10}}
        ";
    }
}
