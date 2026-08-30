using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CatMetro.Integrations;
using CatMetro.Integrations.LevelPlay;
using CatMetro.Services.Ads;
using NUnit.Framework;
using UnityEngine;

namespace CatMetro.Tests.LevelPlay
{
    public sealed class LevelPlayRewardedAdProviderTests
    {
        private const string ConfigJson = @"{
          ""androidAppKey"": ""android-test-app-key"",
          ""androidRewardedAdUnitId"": ""one-rewarded-unit""
        }";

        private FakeSdk _sdk;
        private FakeRewardedAd _ad;
        private LevelPlayRewardedAdProvider _provider;
        private List<RewardedAdEvent> _events;
        private long _now;

        [SetUp]
        public void SetUp()
        {
            _ad = new FakeRewardedAd { Ready = true };
            _sdk = new FakeSdk(_ad);
            _events = new List<RewardedAdEvent>();
            _now = 10L;
            _provider = new LevelPlayRewardedAdProvider(
                RewardedAdsConfig.Parse(ConfigJson, RuntimePlatform.Android),
                _sdk, () => _now, contextLifetimeTicks: 100L);
            _provider.EventReceived += _events.Add;
        }

        [TearDown]
        public void TearDown() => _provider?.Dispose();

        [Test]
        public void PreInitLoadsCoalesce_InitSubscribesFirst_AndOneAdSubscribesBeforeLoad()
        {
            _provider.Load();
            _provider.Load();
            _provider.Initialize();
            _provider.Initialize();

            Assert.That(_sdk.Sequence.Take(3), Is.EqualTo(new[]
            {
                "add:InitializationSucceeded",
                "add:InitializationFailed",
                "Initialize",
            }));
            Assert.That(_sdk.InitializeCalls, Is.EqualTo(1));
            Assert.That(_sdk.CreateCalls, Is.Zero);

            _sdk.EmitInitializationSucceeded();
            _sdk.EmitInitializationSucceeded();

            Assert.That(_sdk.CreateCalls, Is.EqualTo(1));
            Assert.That(_sdk.CreatedAdUnitIds, Is.EqualTo(new[] { "one-rewarded-unit" }));
            Assert.That(_ad.LoadCalls, Is.EqualTo(1),
                "multiple pre-init loads are one pending demand");
            foreach (string eventName in FakeRewardedAd.EventNames)
            {
                Assert.That(_ad.AddCalls[eventName], Is.EqualTo(1), eventName);
                Assert.That(_ad.Sequence.IndexOf("add:" + eventName),
                    Is.LessThan(_ad.Sequence.IndexOf("Load")), eventName);
            }

            _provider.Load();
            Assert.That(_ad.LoadCalls, Is.EqualTo(2),
                "later loads remain explicit coordinator requests");
            Assert.That(_sdk.CreateCalls, Is.EqualTo(1));
        }

        [Test]
        public void InitializationFailureOrThrowFailsClosedWithoutCreatingAnAd()
        {
            _provider.Load();
            _provider.Initialize();
            _sdk.EmitInitializationFailed(508);

            Assert.That(_sdk.CreateCalls, Is.Zero);
            Assert.That(_provider.IsReady, Is.False);
            Assert.That(_events.Single().Kind, Is.EqualTo(RewardedAdEventKind.LoadFailed));
            Assert.That(_events.Single().ErrorCode, Is.EqualTo(508));

            _provider.Dispose();
            SetUp();
            _sdk.ThrowInitialize = true;
            Assert.DoesNotThrow(_provider.Initialize);
            Assert.That(_sdk.CreateCalls, Is.Zero);
            Assert.That(_provider.IsReady, Is.False);
            Assert.That(_events.Single().Kind, Is.EqualTo(RewardedAdEventKind.LoadFailed));
        }

        [Test]
        public void ReadinessChecksAdAndPlacementCapAndFailsClosedOnEitherException()
        {
            ReadyProvider();

            Assert.That(_provider.IsReady, Is.True);
            Assert.That(_provider.IsReadyForPlacement("wardrobe_try_scarf"), Is.True);
            Assert.That(_ad.ReadyChecks, Is.EqualTo(2));
            Assert.That(_ad.CapChecks, Is.EqualTo(new[] { "wardrobe_try_scarf" }));

            _ad.CappedPlacements.Add("wardrobe_try_scarf");
            Assert.That(_provider.IsReadyForPlacement("wardrobe_try_scarf"), Is.False);

            _ad.ThrowReady = true;
            Assert.DoesNotThrow(() => Assert.That(_provider.IsReady, Is.False));
            _ad.ThrowReady = false;
            _ad.ThrowCap = true;
            Assert.DoesNotThrow(() => Assert.That(
                _provider.IsReadyForPlacement("wardrobe_try_engineer"), Is.False));
        }

        [Test]
        public void TryShowRepeatsReadyAndCapAndEstablishesAttemptBeforeSynchronousCallback()
        {
            ReadyProvider();
            _ad.OnShow = () => _ad.EmitDisplayed(AdInfo("ad-17", "auction-29",
                "wardrobe_try_scarf"));

            Assert.That(_provider.TryShow(41L, "wardrobe_try_scarf"), Is.True);

            Assert.That(_ad.Shows, Is.EqualTo(new[] { "wardrobe_try_scarf" }));
            Assert.That(_ad.ReadyChecks, Is.GreaterThanOrEqualTo(1));
            Assert.That(_ad.CapChecks.Last(), Is.EqualTo("wardrobe_try_scarf"));
            var displayed = _events.Single(e => e.Kind == RewardedAdEventKind.Displayed);
            Assert.That(displayed.AttemptId, Is.EqualTo(41L));
            Assert.That(displayed.AdId, Is.EqualTo("ad-17"));
            Assert.That(displayed.AuctionId, Is.EqualTo("auction-29"));
        }

        [Test]
        public void CappedOrThrowingShowNeverEscapesAndThrowMapsExactDisplayFailure()
        {
            ReadyProvider();
            _ad.CappedPlacements.Add("wardrobe_try_conductor");
            Assert.That(_provider.TryShow(1L, "wardrobe_try_conductor"), Is.False);
            Assert.That(_ad.Shows, Is.Empty);

            _ad.CappedPlacements.Clear();
            _ad.ThrowShow = true;
            bool accepted = true;
            Assert.DoesNotThrow(() => accepted =
                _provider.TryShow(2L, "wardrobe_try_conductor"));
            Assert.That(accepted, Is.False);
            var failure = _events.Single(e => e.Kind == RewardedAdEventKind.DisplayFailed);
            Assert.That(failure.AttemptId, Is.EqualTo(2L));
            Assert.That(failure.PlacementId, Is.EqualTo("wardrobe_try_conductor"));
        }

        [Test]
        public void SynchronousDisplayFailureRejectsShowWithoutProviderOwnedReload()
        {
            ReadyProvider();
            _ad.OnShow = () => _ad.EmitDisplayFailed(
                AdInfo("failed-ad", "failed-auction", "wardrobe_try_scarf"),
                new LevelPlayErrorSnapshot(509));

            Assert.That(_provider.TryShow(3L, "wardrobe_try_scarf"), Is.False);

            var failure = _events.Single(e => e.Kind == RewardedAdEventKind.DisplayFailed);
            Assert.That(failure.AttemptId, Is.EqualTo(3L));
            Assert.That(failure.ErrorCode, Is.EqualTo(509));
            Assert.That(_ad.LoadCalls, Is.EqualTo(1),
                "the coordinator, not the provider callback, owns the reload");
        }

        [Test]
        public void CallbackMappingPreservesNeutralIdsAndCoordinatorAloneReloadsAfterTerminalEvents()
        {
            ReadyProvider();
            _ad.EmitLoaded(AdInfo("load-ad", "load-auction", "loaded-placement"));
            _ad.EmitLoadFailed(new LevelPlayErrorSnapshot(204, "failed-ad", "one-rewarded-unit"));
            Assert.That(_provider.TryShow(73L, "wardrobe_try_goggles"), Is.True);
            var info = AdInfo("show-ad", "show-auction", "wardrobe_try_goggles");
            _ad.EmitDisplayed(info);
            _ad.EmitClicked(info);
            _ad.EmitClosed(info);

            Assert.That(_events.Select(e => e.Kind), Is.EqualTo(new[]
            {
                RewardedAdEventKind.Loaded,
                RewardedAdEventKind.LoadFailed,
                RewardedAdEventKind.Displayed,
                RewardedAdEventKind.Opened,
                RewardedAdEventKind.Closed,
            }));
            Assert.That(_events[0].AdId, Is.EqualTo("load-ad"));
            Assert.That(_events[0].AuctionId, Is.EqualTo("load-auction"));
            Assert.That(_events[0].AdUnitId, Is.EqualTo("one-rewarded-unit"));
            Assert.That(_events[1].ErrorCode, Is.EqualTo(204));
            Assert.That(_events.Skip(2).All(e => e.AttemptId == 73L), Is.True);
            Assert.That(_ad.LoadCalls, Is.EqualTo(1),
                "close must not trigger a provider-owned reload");

            _provider.Load();
            Assert.That(_provider.TryShow(74L, "wardrobe_try_engineer"), Is.True);
            _ad.EmitDisplayFailed(AdInfo("other-ad", "other-auction",
                "wardrobe_try_engineer"), new LevelPlayErrorSnapshot(509));
            Assert.That(_ad.LoadCalls, Is.EqualTo(2),
                "display failure must not trigger a provider-owned reload");
        }

        [Test]
        public void CloseBeforeRewardRetainsExactContextAndDuplicateRewardIsDropped()
        {
            ReadyProvider();
            Assert.That(_provider.TryShow(101L, "wardrobe_try_conductor"), Is.True);
            var info = AdInfo("ad-a", "auction-a", "wardrobe_try_conductor");

            _ad.EmitClosed(info);
            _ad.EmitRewarded(info);
            _ad.EmitRewarded(info);

            Assert.That(_events.Where(e => e.Kind == RewardedAdEventKind.Closed)
                .Select(e => e.AttemptId), Is.EqualTo(new[] { 101L }));
            Assert.That(_events.Where(e => e.Kind == RewardedAdEventKind.Rewarded)
                .Select(e => e.AttemptId), Is.EqualTo(new[] { 101L }));
        }

        [Test]
        public void StableAuctionIdsDistinguishNewerShowAndLateOlderReward()
        {
            ReadyProvider();
            Assert.That(_provider.TryShow(201L, "first"), Is.True);
            _ad.EmitClosed(AdInfo("shared-ad", "auction-old", "first"));

            Assert.That(_provider.TryShow(202L, "second"), Is.True);
            _ad.EmitDisplayed(AdInfo("shared-ad", "auction-new", "second"));
            _ad.EmitRewarded(AdInfo("shared-ad", "auction-old", "first"));
            _ad.EmitRewarded(AdInfo("shared-ad", "auction-new", "second"));

            Assert.That(_events.Where(e => e.Kind == RewardedAdEventKind.Rewarded)
                .Select(e => e.AttemptId), Is.EqualTo(new[] { 201L, 202L }));
        }

        [Test]
        public void AmbiguousUnknownAndMalformedRewardsNeverUseNewestAttemptFallback()
        {
            ReadyProvider();
            Assert.That(_provider.TryShow(301L, "first"), Is.True);
            _ad.EmitClosed(AdInfo("shared-ad", "auction-first", "first"));
            Assert.That(_provider.TryShow(302L, "second"), Is.True);
            _ad.EmitDisplayed(AdInfo("shared-ad", "auction-second", "second"));

            _ad.EmitRewarded(AdInfo("shared-ad", null, "second"));
            _ad.EmitRewarded(AdInfo("other-ad", "unknown-auction", "second"));
            _ad.EmitRewarded(AdInfo(null, null, "second"));

            Assert.That(_events.Any(e => e.Kind == RewardedAdEventKind.Rewarded), Is.False);
            _ad.EmitRewarded(AdInfo("shared-ad", "auction-second", "second"));
            Assert.That(_events.Single(e => e.Kind == RewardedAdEventKind.Rewarded).AttemptId,
                Is.EqualTo(302L));
        }

        [Test]
        public void RewardCannotEstablishAnUnknownIdentifierForAnUnboundShow()
        {
            ReadyProvider();
            Assert.That(_provider.TryShow(351L, "placement"), Is.True);

            _ad.EmitRewarded(AdInfo("unknown-ad", "unknown-auction", "placement"));

            Assert.That(_events.Any(e => e.Kind == RewardedAdEventKind.Rewarded), Is.False,
                "a reward is a grant boundary, not a safe first correlation signal");
            _ad.EmitDisplayed(AdInfo("current-ad", "current-auction", "placement"));
            _ad.EmitRewarded(AdInfo("current-ad", "current-auction", "placement"));
            Assert.That(_events.Single(e => e.Kind == RewardedAdEventKind.Rewarded).AttemptId,
                Is.EqualTo(351L));
        }

        [Test]
        public void ExpiredUnboundContextRestoresAvailabilityButLateRewardCannotBindNewShow()
        {
            ReadyProvider();
            Assert.That(_provider.TryShow(401L, "first"), Is.True);
            _ad.EmitClosed(AdInfo(null, null, "first"));
            Assert.That(_provider.TryShow(402L, "second"), Is.False,
                "one unresolved closed context conservatively serializes shows");

            _now += 101L;
            Assert.That(_provider.IsReadyForPlacement("second"), Is.True);
            Assert.That(_provider.TryShow(402L, "second"), Is.True);
            _ad.EmitRewarded(AdInfo("late-old-ad", "late-old-auction", "first"));
            Assert.That(_events.Any(e => e.Kind == RewardedAdEventKind.Rewarded), Is.False,
                "expiry drops attribution and never transfers it to the current unbound show");

            _ad.EmitDisplayed(AdInfo("new-ad", "new-auction", "second"));
            _ad.EmitRewarded(AdInfo("new-ad", "new-auction", "second"));
            Assert.That(_events.Single(e => e.Kind == RewardedAdEventKind.Rewarded).AttemptId,
                Is.EqualTo(402L));
        }

        [Test]
        public void RetainedContextsAreBoundedAndExpiryOnlyRestoresAvailability()
        {
            ReadyProvider();
            for (int i = 0; i < 16; i++)
            {
                string suffix = i.ToString();
                Assert.That(_provider.TryShow(700L + i, "placement-" + suffix), Is.True);
                _ad.EmitClosed(AdInfo("ad-" + suffix, "auction-" + suffix,
                    "placement-" + suffix));
            }

            Assert.That(_provider.IsReadyForPlacement("overflow"), Is.False);
            Assert.That(_provider.TryShow(800L, "overflow"), Is.False);

            _now += 101L;
            Assert.That(_provider.IsReadyForPlacement("after-expiry"), Is.True);
            Assert.That(_provider.TryShow(801L, "after-expiry"), Is.True);
            _ad.EmitRewarded(AdInfo("ad-0", "auction-0", "placement-0"));
            Assert.That(_events.Any(e => e.Kind == RewardedAdEventKind.Rewarded), Is.False,
                "expiry can free bounded state but can never revive old attribution");
        }

        [Test]
        public void ImpressionWorkerCopiesScalarsAndPumpFacingDrainDeliversOnCallerThread()
        {
            ReadyProvider();
            Assert.That(_provider.TryShow(501L, "wardrobe_try_scarf"), Is.True);
            _ad.EmitDisplayed(AdInfo("ad-ilr", "auction-ilr", "wardrobe_try_scarf"));
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            int eventThread = 0;
            _provider.EventReceived += adEvent =>
            {
                if (adEvent.Kind == RewardedAdEventKind.Revenue)
                    eventThread = Thread.CurrentThread.ManagedThreadId;
            };
            var source = new MutableImpression
            {
                AuctionId = "auction-ilr",
                AdUnitId = "one-rewarded-unit",
                Placement = "wardrobe_try_scarf",
                Network = "network-original",
                Revenue = 0.001234d,
                Precision = "BID",
            };
            var worker = new Thread(() => _ad.EmitImpression(source.Snapshot()));

            worker.Start();
            Assert.That(worker.Join(2_000), Is.True);
            source.AuctionId = "mutated-auction";
            source.Network = "mutated-network";
            source.Revenue = 99d;
            Assert.That(_events.Any(e => e.Kind == RewardedAdEventKind.Revenue), Is.False);

            _provider.DrainMainThreadEvents();

            var revenue = _events.Single(e => e.Kind == RewardedAdEventKind.Revenue);
            Assert.That(eventThread, Is.EqualTo(mainThread));
            Assert.That(revenue.AttemptId, Is.EqualTo(501L));
            Assert.That(revenue.AuctionId, Is.EqualTo("auction-ilr"));
            Assert.That(revenue.AdId, Is.Null,
                "ILR CreativeId is not the package's distinct AdId and must not be substituted");
            Assert.That(revenue.NetworkName, Is.EqualTo("network-original"));
            Assert.That(revenue.RevenueMicros, Is.EqualTo(1_234L));
            Assert.That(revenue.RevenuePrecision, Is.EqualTo(AdRevenuePrecision.Exact));
        }

        [Test]
        public void DisposeInvalidatesQueuedImpressionAndIsExactlyOnceAcrossRepeatedCalls()
        {
            ReadyProvider();
            Assert.That(_provider.TryShow(601L, "wardrobe_try_goggles"), Is.True);
            _ad.EmitDisplayed(AdInfo("ad", "auction", "wardrobe_try_goggles"));
            _ad.EmitImpression(new LevelPlayImpressionSnapshot("auction",
                "one-rewarded-unit", "wardrobe_try_goggles", "network", 0.1d, "CPM"));

            Assert.DoesNotThrow(_provider.Dispose);
            Assert.DoesNotThrow(_provider.Dispose);
            Assert.DoesNotThrow(_provider.DrainMainThreadEvents);

            Assert.That(_events.Any(e => e.Kind == RewardedAdEventKind.Revenue), Is.False);
            Assert.That(_ad.DisposeCalls, Is.EqualTo(1));
            Assert.That(_sdk.RemoveCalls["InitializationSucceeded"], Is.EqualTo(1));
            Assert.That(_sdk.RemoveCalls["InitializationFailed"], Is.EqualTo(1));
            foreach (string eventName in FakeRewardedAd.EventNames)
                Assert.That(_ad.RemoveCalls[eventName], Is.EqualTo(1), eventName);
            Assert.That(_ad.TotalSubscriberCount, Is.Zero);
        }

        [TestCase("InitializationSucceeded")]
        [TestCase("InitializationFailed")]
        public void InitEventAddThatAttachesThenThrowsIsCompensated(string eventName)
        {
            _sdk.ThrowAfterAddEvent = eventName;

            Assert.DoesNotThrow(_provider.Initialize);
            Assert.That(_sdk.InitializeCalls, Is.Zero);
            Assert.That(_sdk.TotalSubscriberCount, Is.Zero);
            Assert.That(_provider.IsReady, Is.False);
        }

        [TestCase("InitializationSucceeded")]
        [TestCase("InitializationFailed")]
        public void InitEventRemoveExceptionCannotSkipOtherRemovalOrSuccessfulSetup(
            string eventName)
        {
            _sdk.ThrowAfterRemoveEvent = eventName;
            _provider.Load();
            _provider.Initialize();

            Assert.DoesNotThrow(_sdk.EmitInitializationSucceeded);

            Assert.That(_sdk.RemoveCalls["InitializationSucceeded"], Is.EqualTo(1));
            Assert.That(_sdk.RemoveCalls["InitializationFailed"], Is.EqualTo(1));
            Assert.That(_sdk.TotalSubscriberCount, Is.Zero);
            Assert.That(_sdk.CreateCalls, Is.EqualTo(1));
            Assert.That(_ad.LoadCalls, Is.EqualTo(1));
        }

        [TestCaseSource(nameof(RewardedEventNames))]
        public void RewardedEventAddThatAttachesThenThrowsDisposesWithoutLoading(string eventName)
        {
            _ad.ThrowAfterAddEvent = eventName;
            _provider.Load();
            _provider.Initialize();

            Assert.DoesNotThrow(_sdk.EmitInitializationSucceeded);
            Assert.That(_sdk.CreateCalls, Is.EqualTo(1));
            Assert.That(_ad.LoadCalls, Is.Zero);
            Assert.That(_ad.DisposeCalls, Is.EqualTo(1));
            Assert.That(_ad.TotalSubscriberCount, Is.Zero);
            Assert.That(_provider.IsReady, Is.False);
        }

        [TestCaseSource(nameof(RewardedEventNames))]
        public void RewardedEventRemoveExceptionCannotSkipOtherCleanupOrDoubleDispose(
            string eventName)
        {
            ReadyProvider();
            _ad.ThrowAfterRemoveEvent = eventName;
            _ad.ThrowDispose = true;

            Assert.DoesNotThrow(_provider.Dispose);
            Assert.DoesNotThrow(_provider.Dispose);

            Assert.That(_ad.DisposeCalls, Is.EqualTo(1));
            foreach (string name in FakeRewardedAd.EventNames)
                Assert.That(_ad.RemoveCalls[name], Is.EqualTo(1), name);
            Assert.That(_ad.TotalSubscriberCount, Is.Zero);
        }

        [Test]
        public void LoadCreateAndReadinessBoundaryExceptionsNeverEscapeGameplay()
        {
            _sdk.ThrowCreate = true;
            _provider.Load();
            _provider.Initialize();
            Assert.DoesNotThrow(_sdk.EmitInitializationSucceeded);
            Assert.That(_provider.IsReady, Is.False);
            Assert.That(_events.Single().Kind, Is.EqualTo(RewardedAdEventKind.LoadFailed));

            _provider.Dispose();
            SetUp();
            ReadyProvider();
            _ad.ThrowLoad = true;
            Assert.DoesNotThrow(_provider.Load);
            Assert.That(_events.Last().Kind, Is.EqualTo(RewardedAdEventKind.LoadFailed));

            _ad.ThrowLoad = false;
            _ad.ThrowReady = true;
            Assert.DoesNotThrow(() => Assert.That(_provider.IsReady, Is.False));
            _ad.ThrowReady = false;
            _ad.ThrowCap = true;
            Assert.DoesNotThrow(() => Assert.That(
                _provider.IsReadyForPlacement("placement"), Is.False));
        }

        [Test]
        public void ThrowingEventConsumerCannotEscapeVendorCallbackOrBlockLaterConsumer()
        {
            ReadyProvider();
            int later = 0;
            _provider.EventReceived += _ => throw new InvalidOperationException("consumer fault");
            _provider.EventReceived += _ => later++;

            Assert.DoesNotThrow(() => _ad.EmitLoaded(
                AdInfo("ad", "auction", "placement")));
            Assert.That(later, Is.EqualTo(1));
        }

        private static IEnumerable<string> RewardedEventNames => FakeRewardedAd.EventNames;

        private void ReadyProvider()
        {
            _provider.Load();
            _provider.Initialize();
            _sdk.EmitInitializationSucceeded();
            Assert.That(_ad.LoadCalls, Is.EqualTo(1));
        }

        private static LevelPlayAdSnapshot AdInfo(string adId, string auctionId,
            string placement)
            => new LevelPlayAdSnapshot(adId, auctionId, "one-rewarded-unit", placement,
                "test-network");

        private sealed class MutableImpression
        {
            public string AuctionId;
            public string AdUnitId;
            public string Placement;
            public string Network;
            public double? Revenue;
            public string Precision;

            public LevelPlayImpressionSnapshot Snapshot()
                => new LevelPlayImpressionSnapshot(AuctionId, AdUnitId, Placement, Network,
                    Revenue, Precision);
        }

        private sealed class FakeSdk : ILevelPlaySdkBridge
        {
            private Action _initializationSucceeded;
            private Action<int> _initializationFailed;
            private readonly FakeRewardedAd _ad;

            public FakeSdk(FakeRewardedAd ad)
            {
                _ad = ad;
                AddCalls["InitializationSucceeded"] = 0;
                AddCalls["InitializationFailed"] = 0;
                RemoveCalls["InitializationSucceeded"] = 0;
                RemoveCalls["InitializationFailed"] = 0;
            }

            public readonly List<string> Sequence = new List<string>();
            public readonly List<string> CreatedAdUnitIds = new List<string>();
            public readonly Dictionary<string, int> AddCalls = new Dictionary<string, int>();
            public readonly Dictionary<string, int> RemoveCalls = new Dictionary<string, int>();
            public string ThrowAfterAddEvent;
            public string ThrowAfterRemoveEvent;
            public bool ThrowInitialize;
            public bool ThrowCreate;
            public int InitializeCalls;
            public int CreateCalls;

            public event Action InitializationSucceeded
            {
                add => Add(ref _initializationSucceeded, value, "InitializationSucceeded");
                remove => Remove(ref _initializationSucceeded, value, "InitializationSucceeded");
            }

            public event Action<int> InitializationFailed
            {
                add => Add(ref _initializationFailed, value, "InitializationFailed");
                remove => Remove(ref _initializationFailed, value, "InitializationFailed");
            }

            public int TotalSubscriberCount =>
                (_initializationSucceeded?.GetInvocationList().Length ?? 0) +
                (_initializationFailed?.GetInvocationList().Length ?? 0);

            public void Initialize(string appKey)
            {
                Sequence.Add("Initialize");
                InitializeCalls++;
                if (ThrowInitialize) throw new InvalidOperationException("init fault");
            }

            public ILevelPlayRewardedAdBridge CreateRewardedAd(string adUnitId)
            {
                Sequence.Add("CreateRewardedAd");
                CreateCalls++;
                CreatedAdUnitIds.Add(adUnitId);
                if (ThrowCreate) throw new InvalidOperationException("create fault");
                return _ad;
            }

            public void EmitInitializationSucceeded() => _initializationSucceeded?.Invoke();
            public void EmitInitializationFailed(int code) => _initializationFailed?.Invoke(code);

            private void Add(ref Action field, Action value, string name)
            {
                Sequence.Add("add:" + name);
                AddCalls[name]++;
                field += value;
                if (ThrowAfterAddEvent == name)
                    throw new InvalidOperationException("post-add fault: " + name);
            }

            private void Add<T>(ref Action<T> field, Action<T> value, string name)
            {
                Sequence.Add("add:" + name);
                AddCalls[name]++;
                field += value;
                if (ThrowAfterAddEvent == name)
                    throw new InvalidOperationException("post-add fault: " + name);
            }

            private void Remove(ref Action field, Action value, string name)
            {
                Sequence.Add("remove:" + name);
                RemoveCalls[name]++;
                field -= value;
                if (ThrowAfterRemoveEvent == name)
                    throw new InvalidOperationException("post-remove fault: " + name);
            }

            private void Remove<T>(ref Action<T> field, Action<T> value, string name)
            {
                Sequence.Add("remove:" + name);
                RemoveCalls[name]++;
                field -= value;
                if (ThrowAfterRemoveEvent == name)
                    throw new InvalidOperationException("post-remove fault: " + name);
            }
        }

        private sealed class FakeRewardedAd : ILevelPlayRewardedAdBridge
        {
            internal static readonly string[] EventNames =
            {
                "AdLoaded", "AdLoadFailed", "AdDisplayed", "AdDisplayFailed",
                "AdRewarded", "AdClicked", "AdClosed", "AdInfoChanged", "AdImpression",
            };

            private Action<LevelPlayAdSnapshot> _adLoaded;
            private Action<LevelPlayErrorSnapshot> _adLoadFailed;
            private Action<LevelPlayAdSnapshot> _adDisplayed;
            private Action<LevelPlayAdSnapshot, LevelPlayErrorSnapshot> _adDisplayFailed;
            private Action<LevelPlayAdSnapshot> _adRewarded;
            private Action<LevelPlayAdSnapshot> _adClicked;
            private Action<LevelPlayAdSnapshot> _adClosed;
            private Action<LevelPlayAdSnapshot> _adInfoChanged;
            private Action<LevelPlayImpressionSnapshot> _adImpression;

            public FakeRewardedAd()
            {
                foreach (string name in EventNames)
                {
                    AddCalls[name] = 0;
                    RemoveCalls[name] = 0;
                }
            }

            public readonly List<string> Sequence = new List<string>();
            public readonly List<string> Shows = new List<string>();
            public readonly List<string> CapChecks = new List<string>();
            public readonly HashSet<string> CappedPlacements = new HashSet<string>();
            public readonly Dictionary<string, int> AddCalls = new Dictionary<string, int>();
            public readonly Dictionary<string, int> RemoveCalls = new Dictionary<string, int>();
            public string ThrowAfterAddEvent;
            public string ThrowAfterRemoveEvent;
            public bool Ready;
            public bool ThrowLoad;
            public bool ThrowShow;
            public bool ThrowReady;
            public bool ThrowCap;
            public bool ThrowDispose;
            public Action OnShow;
            public int LoadCalls;
            public int ReadyChecks;
            public int DisposeCalls;

            public event Action<LevelPlayAdSnapshot> AdLoaded
            {
                add => Add(ref _adLoaded, value, "AdLoaded");
                remove => Remove(ref _adLoaded, value, "AdLoaded");
            }
            public event Action<LevelPlayErrorSnapshot> AdLoadFailed
            {
                add => Add(ref _adLoadFailed, value, "AdLoadFailed");
                remove => Remove(ref _adLoadFailed, value, "AdLoadFailed");
            }
            public event Action<LevelPlayAdSnapshot> AdDisplayed
            {
                add => Add(ref _adDisplayed, value, "AdDisplayed");
                remove => Remove(ref _adDisplayed, value, "AdDisplayed");
            }
            public event Action<LevelPlayAdSnapshot, LevelPlayErrorSnapshot> AdDisplayFailed
            {
                add => Add(ref _adDisplayFailed, value, "AdDisplayFailed");
                remove => Remove(ref _adDisplayFailed, value, "AdDisplayFailed");
            }
            public event Action<LevelPlayAdSnapshot> AdRewarded
            {
                add => Add(ref _adRewarded, value, "AdRewarded");
                remove => Remove(ref _adRewarded, value, "AdRewarded");
            }
            public event Action<LevelPlayAdSnapshot> AdClicked
            {
                add => Add(ref _adClicked, value, "AdClicked");
                remove => Remove(ref _adClicked, value, "AdClicked");
            }
            public event Action<LevelPlayAdSnapshot> AdClosed
            {
                add => Add(ref _adClosed, value, "AdClosed");
                remove => Remove(ref _adClosed, value, "AdClosed");
            }
            public event Action<LevelPlayAdSnapshot> AdInfoChanged
            {
                add => Add(ref _adInfoChanged, value, "AdInfoChanged");
                remove => Remove(ref _adInfoChanged, value, "AdInfoChanged");
            }
            public event Action<LevelPlayImpressionSnapshot> AdImpression
            {
                add => Add(ref _adImpression, value, "AdImpression");
                remove => Remove(ref _adImpression, value, "AdImpression");
            }

            public int TotalSubscriberCount =>
                Count(_adLoaded) + Count(_adLoadFailed) + Count(_adDisplayed) +
                Count(_adDisplayFailed) + Count(_adRewarded) + Count(_adClicked) +
                Count(_adClosed) + Count(_adInfoChanged) + Count(_adImpression);

            public void Load()
            {
                Sequence.Add("Load");
                LoadCalls++;
                if (ThrowLoad) throw new InvalidOperationException("load fault");
            }

            public void Show(string placementId)
            {
                Sequence.Add("Show");
                Shows.Add(placementId);
                if (ThrowShow) throw new InvalidOperationException("show fault");
                OnShow?.Invoke();
            }

            public bool IsReady()
            {
                ReadyChecks++;
                if (ThrowReady) throw new InvalidOperationException("ready fault");
                return Ready;
            }

            public bool IsPlacementCapped(string placementId)
            {
                CapChecks.Add(placementId);
                if (ThrowCap) throw new InvalidOperationException("cap fault");
                return CappedPlacements.Contains(placementId);
            }

            public void Dispose()
            {
                DisposeCalls++;
                if (ThrowDispose) throw new InvalidOperationException("dispose fault");
            }

            public void EmitLoaded(LevelPlayAdSnapshot info) => _adLoaded?.Invoke(info);
            public void EmitLoadFailed(LevelPlayErrorSnapshot error) => _adLoadFailed?.Invoke(error);
            public void EmitDisplayed(LevelPlayAdSnapshot info) => _adDisplayed?.Invoke(info);
            public void EmitDisplayFailed(LevelPlayAdSnapshot info, LevelPlayErrorSnapshot error)
                => _adDisplayFailed?.Invoke(info, error);
            public void EmitRewarded(LevelPlayAdSnapshot info) => _adRewarded?.Invoke(info);
            public void EmitClicked(LevelPlayAdSnapshot info) => _adClicked?.Invoke(info);
            public void EmitClosed(LevelPlayAdSnapshot info) => _adClosed?.Invoke(info);
            public void EmitInfoChanged(LevelPlayAdSnapshot info) => _adInfoChanged?.Invoke(info);
            public void EmitImpression(LevelPlayImpressionSnapshot impression)
                => _adImpression?.Invoke(impression);

            private void Add<T>(ref Action<T> field, Action<T> value, string name)
            {
                Sequence.Add("add:" + name);
                AddCalls[name]++;
                field += value;
                if (ThrowAfterAddEvent == name)
                    throw new InvalidOperationException("post-add fault: " + name);
            }

            private void Add<T1, T2>(ref Action<T1, T2> field, Action<T1, T2> value,
                string name)
            {
                Sequence.Add("add:" + name);
                AddCalls[name]++;
                field += value;
                if (ThrowAfterAddEvent == name)
                    throw new InvalidOperationException("post-add fault: " + name);
            }

            private void Remove<T>(ref Action<T> field, Action<T> value, string name)
            {
                Sequence.Add("remove:" + name);
                RemoveCalls[name]++;
                field -= value;
                if (ThrowAfterRemoveEvent == name)
                    throw new InvalidOperationException("post-remove fault: " + name);
            }

            private void Remove<T1, T2>(ref Action<T1, T2> field, Action<T1, T2> value,
                string name)
            {
                Sequence.Add("remove:" + name);
                RemoveCalls[name]++;
                field -= value;
                if (ThrowAfterRemoveEvent == name)
                    throw new InvalidOperationException("post-remove fault: " + name);
            }

            private static int Count(Delegate handler)
                => handler?.GetInvocationList().Length ?? 0;
        }
    }
}
