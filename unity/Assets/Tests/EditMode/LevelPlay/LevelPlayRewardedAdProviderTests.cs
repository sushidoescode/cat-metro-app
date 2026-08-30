using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            Assert.That(_ad.LoadCalls, Is.EqualTo(1),
                "an explicit call cannot overlap the still in-flight vendor request");
            _ad.EmitLoaded(AdInfo(null, null, null));
            _provider.Load();
            Assert.That(_ad.LoadCalls, Is.EqualTo(1),
                "the loaded result remains the current consumable ad");
            Assert.That(_provider.TryShow(90L, "serialized-load"), Is.True);
            var shown = AdInfo("serialized-load-ad", "serialized-load-auction",
                "serialized-load");
            _ad.EmitDisplayed(shown);
            _ad.EmitClosed(shown);
            _provider.Load();
            Assert.That(_ad.LoadCalls, Is.EqualTo(2),
                "only a later explicit call after consumption starts the next request");
            Assert.That(_sdk.CreateCalls, Is.EqualTo(1));
        }

        [Test]
        public void ExplicitLoadsSerializeOneVendorRequestAndOneCallbackGeneration()
        {
            _provider.Load();
            _provider.Initialize();
            _sdk.EmitInitializationSucceeded();
            Assert.That(_ad.LoadCalls, Is.EqualTo(1));

            _provider.Load();
            Assert.That(_ad.LoadCalls, Is.EqualTo(1),
                "an in-flight request has no vendor token that could distinguish another load");
            Assert.That(PrivateLong("_loadGeneration"), Is.EqualTo(1L));

            var loaded = AdInfo("serialized-ad", "serialized-auction", "serialized");
            _ad.EmitLoaded(loaded);
            _provider.Load();
            Assert.That(_ad.LoadCalls, Is.EqualTo(1),
                "an already loaded ad is the one current consumable result");
            Assert.That(PrivateLong("_loadGeneration"), Is.EqualTo(1L));

            Assert.That(_provider.TryShow(901L, "serialized"), Is.True);
            _ad.EmitRewarded(loaded);
            Assert.That(_events.Single(e => e.Kind == RewardedAdEventKind.Rewarded).AttemptId,
                Is.EqualTo(901L));
        }

        [Test]
        public void LoadFailureCompletesTheFlightSoOneLaterExplicitLoadCanIssue()
        {
            _provider.Load();
            _provider.Initialize();
            _sdk.EmitInitializationSucceeded();
            Assert.That(_ad.LoadCalls, Is.EqualTo(1));

            _ad.EmitLoadFailed(new LevelPlayErrorSnapshot(204));
            _provider.Load();
            _provider.Load();

            Assert.That(_ad.LoadCalls, Is.EqualTo(2),
                "failure ends one flight, but the replacement remains serialized");
            Assert.That(PrivateLong("_loadGeneration"), Is.EqualTo(2L));
            _ad.EmitLoaded(AdInfo(null, "replacement-auction", "replacement"));
            Assert.That(_provider.IsReadyForPlacement("replacement"), Is.True,
                "the explicit replacement load must not deadlock serving");
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
            Assert.That(_provider.TryShow(73L, "wardrobe_try_goggles"), Is.True);
            var info = AdInfo("show-ad", "show-auction", "wardrobe_try_goggles");
            _ad.EmitDisplayed(info);
            _ad.EmitClicked(info);
            _ad.EmitClosed(info);

            Assert.That(_ad.LoadCalls, Is.EqualTo(1),
                "close must not trigger a provider-owned reload");

            _provider.Load();
            var other = AdInfo("load-ad", "load-auction", "wardrobe_try_engineer");
            _ad.EmitLoaded(other);
            Assert.That(_provider.TryShow(74L, "wardrobe_try_engineer"), Is.True);
            _ad.EmitDisplayFailed(other, new LevelPlayErrorSnapshot(509));
            Assert.That(_ad.LoadCalls, Is.EqualTo(2),
                "display failure must not trigger a provider-owned reload");

            _provider.Load();
            _ad.EmitLoadFailed(new LevelPlayErrorSnapshot(
                204, "failed-ad", "one-rewarded-unit"));
            Assert.That(_ad.LoadCalls, Is.EqualTo(3));
            Assert.That(_events.Select(e => e.Kind), Is.EqualTo(new[]
            {
                RewardedAdEventKind.Displayed,
                RewardedAdEventKind.Opened,
                RewardedAdEventKind.Closed,
                RewardedAdEventKind.Loaded,
                RewardedAdEventKind.DisplayFailed,
                RewardedAdEventKind.LoadFailed,
            }));

            var loaded = _events.Single(e => e.Kind == RewardedAdEventKind.Loaded);
            Assert.That(loaded.AdId, Is.EqualTo("load-ad"));
            Assert.That(loaded.AuctionId, Is.EqualTo("load-auction"));
            Assert.That(loaded.AdUnitId, Is.EqualTo("one-rewarded-unit"));
            Assert.That(_events.Single(e => e.Kind == RewardedAdEventKind.LoadFailed).ErrorCode,
                Is.EqualTo(204));
            Assert.That(_events.Where(e => e.Kind == RewardedAdEventKind.Displayed ||
                e.Kind == RewardedAdEventKind.Opened || e.Kind == RewardedAdEventKind.Closed)
                .All(e => e.AttemptId == 73L), Is.True);
            Assert.That(_events.Single(e => e.Kind == RewardedAdEventKind.DisplayFailed).AttemptId,
                Is.EqualTo(74L));
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
        public void ConflictingAuctionAndAdContextsDropWithoutMutatingEitherIndex()
        {
            ReadyProvider();
            Assert.That(_provider.TryShow(211L, "first"), Is.True);
            _ad.EmitDisplayed(AdInfo("ad-first", "auction-first", "first"));
            _ad.EmitClosed(AdInfo("ad-first", "auction-first", "first"));
            Assert.That(_provider.TryShow(212L, "second"), Is.True);
            _ad.EmitDisplayed(AdInfo("ad-second", "auction-second", "second"));

            _ad.EmitInfoChanged(AdInfo("ad-second", "auction-first", "malformed"));
            _ad.EmitRewarded(AdInfo("ad-second", null, "second"));
            _ad.EmitRewarded(AdInfo("ad-first", "auction-first", "first"));

            Assert.That(_events.Where(e => e.Kind == RewardedAdEventKind.Rewarded)
                .Select(e => e.AttemptId), Is.EqualTo(new[] { 212L, 211L }),
                "the conflicting pair must neither grant nor alias either stable index");
        }

        [Test]
        public void AdIdFirstThenMatchingAuctionProgressionBindsTheSameAttempt()
        {
            ReadyProvider();
            Assert.That(_provider.TryShow(221L, "placement"), Is.True);
            _ad.EmitDisplayed(AdInfo("ad-first", null, "placement"));

            _ad.EmitRewarded(AdInfo("ad-first", "auction-later", "placement"));

            var reward = _events.Single(e => e.Kind == RewardedAdEventKind.Rewarded);
            Assert.That(reward.AttemptId, Is.EqualTo(221L));
            Assert.That(reward.AdId, Is.EqualTo("ad-first"));
            Assert.That(reward.AuctionId, Is.EqualTo("auction-later"));
        }

        [Test]
        public void AContextBindsEachStableFieldOnceAndNeverRetainsAliases()
        {
            ReadyProvider();
            Assert.That(_provider.TryShow(231L, "placement"), Is.True);
            _ad.EmitDisplayed(AdInfo(null, "auction", "placement"));
            _ad.EmitInfoChanged(AdInfo("ad-primary", "auction", "placement"));
            _ad.EmitInfoChanged(AdInfo("ad-alias", "auction", "placement"));

            _ad.EmitRewarded(AdInfo("ad-alias", null, "placement"));
            Assert.That(_events.Any(e => e.Kind == RewardedAdEventKind.Rewarded), Is.False);

            _ad.EmitRewarded(AdInfo("ad-primary", "auction", "placement"));
            Assert.That(_events.Single(e => e.Kind == RewardedAdEventKind.Rewarded).AttemptId,
                Is.EqualTo(231L));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReusedAdIdCannotLetAnAdOnlyDuplicateGrantTheNewAttempt(bool expireFirst)
        {
            ReadyProvider();
            Assert.That(_provider.TryShow(241L, "first"), Is.True);
            var first = AdInfo("shared-ad", "auction-old", "first");
            _ad.EmitDisplayed(first);
            if (expireFirst)
            {
                _ad.EmitClosed(first);
                _now += 101L;
                _provider.IsReadyForPlacement("probe");
            }
            else
            {
                _ad.EmitRewarded(first);
                _ad.EmitClosed(first);
            }

            _provider.Load();
            var second = AdInfo("shared-ad", "auction-new", "second");
            _ad.EmitLoaded(second);
            Assert.That(_provider.TryShow(242L, "second"), Is.True);
            _ad.EmitDisplayed(second);

            _ad.EmitRewarded(AdInfo("shared-ad", null, "first"));
            Assert.That(_events.Count(e => e.Kind == RewardedAdEventKind.Rewarded),
                Is.EqualTo(expireFirst ? 0 : 1),
                "AdId-only history is ambiguous and cannot identify the newer attempt");

            _ad.EmitRewarded(second);
            Assert.That(_events.Last(e => e.Kind == RewardedAdEventKind.Rewarded).AttemptId,
                Is.EqualTo(242L), "the distinct current auction remains authoritative");
        }

        [Test]
        public void StableIndexesAndCompletedOrAmbiguousHistoriesStayBounded()
        {
            ReadyProvider();
            for (int i = 0; i < 40; i++)
            {
                string suffix = i.ToString();
                string adId = "reused-ad-" + suffix;
                var oldInfo = AdInfo(adId, "old-auction-" + suffix, "old-" + suffix);
                if (i > 0)
                {
                    _provider.Load();
                    _ad.EmitLoaded(oldInfo);
                }
                Assert.That(_provider.TryShow(1_000L + i * 2L, "old-" + suffix), Is.True);
                _ad.EmitDisplayed(oldInfo);
                _ad.EmitRewarded(oldInfo);
                _ad.EmitClosed(oldInfo);

                _provider.Load();
                var newInfo = AdInfo(adId, "new-auction-" + suffix, "new-" + suffix);
                _ad.EmitLoaded(newInfo);
                Assert.That(_provider.TryShow(1_001L + i * 2L, "new-" + suffix), Is.True);
                _ad.EmitDisplayed(newInfo);
                _ad.EmitRewarded(newInfo);
                _ad.EmitClosed(newInfo);
            }

            Assert.That(PrivateCollectionCount("_contexts"), Is.Zero);
            Assert.That(PrivateCollectionCount("_byAuction"), Is.Zero);
            Assert.That(PrivateCollectionCount("_byAd"), Is.Zero);
            Assert.That(PrivateCollectionCount("_completedAuctions"), Is.LessThanOrEqualTo(32));
            Assert.That(PrivateCollectionCount("_retiredAdIds"), Is.LessThanOrEqualTo(32));
            Assert.That(PrivateCollectionCount("_ambiguousAdIds"), Is.LessThanOrEqualTo(32));

            int rewardsBeforeReuse = _events.Count(e =>
                e.Kind == RewardedAdEventKind.Rewarded);
            _provider.Load();
            var afterEviction = AdInfo("reused-ad-0", "after-history-eviction", "latest");
            _ad.EmitLoaded(afterEviction);
            Assert.That(_provider.TryShow(2_000L, "latest"), Is.True);
            _ad.EmitRewarded(AdInfo("reused-ad-0", null, "latest"));
            Assert.That(_events.Count(e => e.Kind == RewardedAdEventKind.Rewarded),
                Is.EqualTo(rewardsBeforeReuse),
                "bounded history eviction must conservatively disable unsafe AdId-only grants");
            _ad.EmitRewarded(afterEviction);
            Assert.That(_events.Last(e => e.Kind == RewardedAdEventKind.Rewarded).AttemptId,
                Is.EqualTo(2_000L));
        }

        [Test]
        public void SaturatedAuctionHistoryRejectsEvictedAuctionOnlyBindingButAllowsSafeAnchors()
        {
            ReadyProvider();
            for (int i = 0; i < 34; i++)
            {
                string auctionId = "completed-auction-" + i;
                var terminal = AdInfo(null, auctionId, "completed-" + i);
                if (i > 0)
                {
                    _provider.Load();
                    _ad.EmitLoaded(terminal);
                }
                Assert.That(_provider.TryShow(3_000L + i, "completed-" + i), Is.True);
                _ad.EmitDisplayed(terminal);
                _ad.EmitRewarded(terminal);
                _ad.EmitClosed(terminal);
            }
            Assert.That(PrivateCollectionCount("_completedAuctions"), Is.EqualTo(32));

            _provider.Load();
            _ad.EmitLoaded(AdInfo(null, null, "current"));
            Assert.That(_provider.TryShow(4_000L, "current"), Is.True);
            int rewardsBeforeStale = _events.Count(e =>
                e.Kind == RewardedAdEventKind.Rewarded);

            var evicted = AdInfo(null, "completed-auction-0", "completed-0");
            _ad.EmitDisplayed(evicted);
            _ad.EmitRewarded(evicted);

            Assert.That(_events.Count(e => e.Kind == RewardedAdEventKind.Rewarded),
                Is.EqualTo(rewardsBeforeStale),
                "an evicted auction tombstone must never reopen unknown auction-only binding");

            var currentAdOnly = AdInfo("current-known-ad", null, "current");
            _ad.EmitDisplayed(currentAdOnly);
            var currentPair = AdInfo("current-known-ad", "current-auction", "current");
            _ad.EmitInfoChanged(currentPair);
            _ad.EmitRewarded(currentPair);
            Assert.That(_events.Last(e => e.Kind == RewardedAdEventKind.Rewarded).AttemptId,
                Is.EqualTo(4_000L),
                "a new auction paired with the current known AdId remains safe progression");
            _ad.EmitClosed(currentPair);

            _provider.Load();
            var loadedAnchor = AdInfo(null, "loaded-current-auction", "loaded-current");
            _ad.EmitLoaded(loadedAnchor);
            Assert.That(_provider.TryShow(4_001L, "loaded-current"), Is.True);
            _ad.EmitRewarded(loadedAnchor);
            Assert.That(_events.Last(e => e.Kind == RewardedAdEventKind.Rewarded).AttemptId,
                Is.EqualTo(4_001L),
                "the serialized current Loaded AuctionId remains authoritative after saturation");
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
            Assert.That(_provider.IsReadyForPlacement("second"), Is.False,
                "expiry alone cannot prove that the ready ad belongs to a new load generation");
            _provider.Load();
            _ad.EmitLoaded(AdInfo("new-ad", "new-auction", "second"));
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
        public void ExpiredUnboundContextRequiresANewerStableLoadedGeneration()
        {
            ReadyProvider();
            Assert.That(_provider.TryShow(411L, "old"), Is.True);
            _ad.EmitClosed(AdInfo(null, null, "old"));
            _now += 101L;

            _provider.Load();
            _ad.EmitLoaded(AdInfo(null, null, "new"));
            Assert.That(_provider.IsReadyForPlacement("new"), Is.False);
            Assert.That(_provider.TryShow(412L, "new"), Is.False);

            _provider.Load();
            _ad.EmitLoaded(AdInfo("reusable-ad-only", null, "new"));
            Assert.That(_provider.IsReadyForPlacement("new"), Is.False,
                "a reusable AdId alone cannot distinguish the expired unknown generation");
            Assert.That(_provider.TryShow(412L, "new"), Is.False);

            _provider.Load();
            _ad.EmitLoaded(AdInfo("new-ad", "new-auction", "new"));
            Assert.That(_provider.IsReadyForPlacement("new"), Is.True);
            Assert.That(_provider.TryShow(412L, "new"), Is.True);
        }

        [TestCase("Displayed")]
        [TestCase("Clicked")]
        [TestCase("InfoChanged")]
        [TestCase("Impression")]
        [TestCase("StableDisplayFailed")]
        [TestCase("AnonymousDisplayFailed")]
        public void StalePostExpiryCallbackCannotBindAcrossTheFreshLoadedGeneration(string kind)
        {
            ReadyProvider();
            Assert.That(_provider.TryShow(421L, "old"), Is.True);
            _ad.EmitClosed(AdInfo(null, null, "old"));
            _provider.Load();
            var current = AdInfo("new-ad", "new-auction", "new");
            _ad.EmitLoaded(current);
            _now += 101L;
            Assert.That(_provider.IsReadyForPlacement("new"), Is.True);
            Assert.That(_provider.TryShow(422L, "new"), Is.True);

            var stale = AdInfo("old-ad", "old-auction", "old");
            switch (kind)
            {
                case "Displayed": _ad.EmitDisplayed(stale); break;
                case "Clicked": _ad.EmitClicked(stale); break;
                case "InfoChanged": _ad.EmitInfoChanged(stale); break;
                case "Impression":
                    _ad.EmitImpression(new LevelPlayImpressionSnapshot("old-auction",
                        "one-rewarded-unit", "old", "old-network", 0.01d, "BID"));
                    _provider.DrainMainThreadEvents();
                    break;
                case "StableDisplayFailed":
                    _ad.EmitDisplayFailed(stale, new LevelPlayErrorSnapshot(509));
                    break;
                case "AnonymousDisplayFailed":
                    _ad.EmitDisplayFailed(AdInfo(null, null, null),
                        new LevelPlayErrorSnapshot(509));
                    break;
            }
            _ad.EmitRewarded(stale);

            Assert.That(_events.Any(e => e.Kind == RewardedAdEventKind.Rewarded), Is.False,
                kind + " must not transfer the expired generation's identity");
            Assert.That(_events.Any(e => e.Kind == RewardedAdEventKind.DisplayFailed &&
                e.AttemptId == 422L), Is.False);

            _ad.EmitDisplayed(current);
            _ad.EmitRewarded(current);
            Assert.That(_events.Single(e => e.Kind == RewardedAdEventKind.Rewarded).AttemptId,
                Is.EqualTo(422L));
        }

        [Test]
        public void AnonymousDisplayFailureOutsideTheCurrentShowCallIsDropped()
        {
            ReadyProvider();
            Assert.That(_provider.TryShow(431L, "placement"), Is.True);

            _ad.EmitDisplayFailed(AdInfo(null, null, null),
                new LevelPlayErrorSnapshot(509));

            Assert.That(_events.Any(e => e.Kind == RewardedAdEventKind.DisplayFailed), Is.False);
            var current = AdInfo("ad", "auction", "placement");
            _ad.EmitDisplayed(current);
            _ad.EmitRewarded(current);
            Assert.That(_events.Single(e => e.Kind == RewardedAdEventKind.Rewarded).AttemptId,
                Is.EqualTo(431L));
        }

        [Test]
        public void SynchronousAnonymousDisplayFailureHasPositiveCurrentShowEvidence()
        {
            ReadyProvider();
            _ad.OnShow = () => _ad.EmitDisplayFailed(AdInfo(null, null, null),
                new LevelPlayErrorSnapshot(509));

            Assert.That(_provider.TryShow(432L, "placement"), Is.False);

            var failure = _events.Single(e => e.Kind == RewardedAdEventKind.DisplayFailed);
            Assert.That(failure.AttemptId, Is.EqualTo(432L));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PostExpirySynchronousTerminalRequiresStableCurrentIdentity(
            bool stableTerminal)
        {
            ReadyProvider();
            Assert.That(_provider.TryShow(433L, "expired"), Is.True);
            _ad.EmitClosed(AdInfo(null, null, "expired"));
            _now += 101L;

            _provider.Load();
            var current = AdInfo("fresh-ad", "fresh-auction", "replacement");
            _ad.EmitLoaded(current);
            _ad.OnShow = () => _ad.EmitDisplayFailed(
                stableTerminal ? current : AdInfo(null, null, null),
                new LevelPlayErrorSnapshot(509));

            bool accepted = _provider.TryShow(434L, "replacement");

            Assert.That(accepted, Is.EqualTo(!stableTerminal));
            Assert.That(_events.Count(e => e.Kind == RewardedAdEventKind.DisplayFailed &&
                e.AttemptId == 434L), Is.EqualTo(stableTerminal ? 1 : 0));
            if (stableTerminal) return;

            _ad.OnShow = null;
            _ad.EmitDisplayed(current);
            _ad.EmitRewarded(current);
            _ad.EmitRewarded(current);
            _ad.EmitClosed(current);
            Assert.That(_events.Count(e => e.Kind == RewardedAdEventKind.Rewarded &&
                e.AttemptId == 434L), Is.EqualTo(1));
            Assert.That(_events.Count(e => e.Kind == RewardedAdEventKind.Closed &&
                e.AttemptId == 434L), Is.EqualTo(1));
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

        [TestCase(true)]
        [TestCase(false)]
        public void QueuedImpressionSurvivesBothRewardAndCloseTerminalOrders(bool rewardFirst)
        {
            ReadyProvider();
            Assert.That(_provider.TryShow(511L, "placement"), Is.True);
            var info = AdInfo("ad-terminal", "auction-terminal", "placement");
            _ad.EmitDisplayed(info);
            _ad.EmitImpression(new LevelPlayImpressionSnapshot("auction-terminal",
                "one-rewarded-unit", "placement", "network", 0.001234d, "BID"));

            if (rewardFirst)
            {
                _ad.EmitRewarded(info);
                _ad.EmitClosed(info);
            }
            else
            {
                _ad.EmitClosed(info);
                _ad.EmitRewarded(info);
            }
            Assert.That(_events.Count(e => e.Kind == RewardedAdEventKind.Revenue), Is.Zero);

            DrainThroughExistingMonetizationPump();

            var revenue = _events.Single(e => e.Kind == RewardedAdEventKind.Revenue);
            Assert.That(revenue.AttemptId, Is.EqualTo(511L));
            Assert.That(revenue.AuctionId, Is.EqualTo("auction-terminal"));
            Assert.That(revenue.RevenueMicros, Is.EqualTo(1_234L));
            Assert.That(_events.Count(e => e.Kind == RewardedAdEventKind.Rewarded), Is.EqualTo(1));
            _ad.EmitRewarded(info);
            DrainThroughExistingMonetizationPump();
            Assert.That(_events.Count(e => e.Kind == RewardedAdEventKind.Rewarded), Is.EqualTo(1));
            Assert.That(_events.Count(e => e.Kind == RewardedAdEventKind.Revenue), Is.EqualTo(1));
        }

        [Test]
        public void TerminalRevenueIsExactlyOnceAndExpiresWithoutDelivery()
        {
            ReadyProvider();
            Assert.That(_provider.TryShow(521L, "duplicate"), Is.True);
            var duplicate = AdInfo("ad-duplicate", "auction-duplicate", "duplicate");
            _ad.EmitDisplayed(duplicate);
            var duplicateImpression = new LevelPlayImpressionSnapshot("auction-duplicate",
                "one-rewarded-unit", "duplicate", "network", 0.01d, "CPM");
            _ad.EmitImpression(duplicateImpression);
            _ad.EmitImpression(duplicateImpression);
            _ad.EmitRewarded(duplicate);
            _ad.EmitClosed(duplicate);
            DrainThroughExistingMonetizationPump();
            Assert.That(_events.Count(e => e.Kind == RewardedAdEventKind.Revenue), Is.EqualTo(1));

            Assert.That(_provider.TryShow(522L, "expired"), Is.True);
            var expired = AdInfo("ad-expired", "auction-expired", "expired");
            _ad.EmitDisplayed(expired);
            _ad.EmitImpression(new LevelPlayImpressionSnapshot("auction-expired",
                "one-rewarded-unit", "expired", "network", 0.02d, "RATE"));
            _ad.EmitRewarded(expired);
            _ad.EmitClosed(expired);
            _now += 101L;
            DrainThroughExistingMonetizationPump();

            Assert.That(_events.Count(e => e.Kind == RewardedAdEventKind.Revenue), Is.EqualTo(1),
                "an expired terminal record drops analytics without affecting reward state");
        }

        [Test]
        public void TerminalRevenueCorrelationStateIsBounded()
        {
            ReadyProvider();
            for (int i = 0; i < 24; i++)
            {
                string suffix = i.ToString();
                Assert.That(_provider.TryShow(530L + i, "p-" + suffix), Is.True);
                var info = AdInfo("ad-" + suffix, "auction-" + suffix, "p-" + suffix);
                _ad.EmitDisplayed(info);
                _ad.EmitImpression(new LevelPlayImpressionSnapshot("auction-" + suffix,
                    "one-rewarded-unit", "p-" + suffix, "network", 0.01d, "BID"));
                _ad.EmitRewarded(info);
                _ad.EmitClosed(info);
            }

            Assert.That(PrivateCollectionCount("_terminalRevenueByAuction"),
                Is.LessThanOrEqualTo(16));
            DrainThroughExistingMonetizationPump();
            Assert.That(_events.Count(e => e.Kind == RewardedAdEventKind.Revenue),
                Is.LessThanOrEqualTo(16));
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
            Assert.That(_provider.TryShow(640L, "load-exception"), Is.True);
            _ad.EmitDisplayFailed(AdInfo("load-exception-ad", "load-exception-auction",
                "load-exception"), new LevelPlayErrorSnapshot(509));
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

            Assert.That(_provider.TryShow(641L, "placement"), Is.True);
            Assert.DoesNotThrow(() => _ad.EmitDisplayed(
                AdInfo("ad", "auction", "placement")));
            Assert.That(later, Is.EqualTo(1));
        }

        [Test]
        public void DisposingEventConsumerStopsRemainingProviderFanOut()
        {
            ReadyProvider();
            int later = 0;
            _provider.EventReceived += _ => _provider.Dispose();
            _provider.EventReceived += _ => later++;

            Assert.That(_provider.TryShow(642L, "placement"), Is.True);
            Assert.DoesNotThrow(() => _ad.EmitDisplayed(
                AdInfo("ad", "auction", "placement")));

            Assert.That(later, Is.Zero);
            Assert.That(_ad.DisposeCalls, Is.EqualTo(1));
        }

        [Test]
        public void ReentrantDisposeDuringTerminalRewardCannotRepopulateClearedState()
        {
            ReadyProvider();
            Assert.That(_provider.TryShow(651L, "placement"), Is.True);
            var info = AdInfo("ad", "auction", "placement");
            _ad.EmitDisplayed(info);
            _ad.EmitClosed(info);
            _provider.EventReceived += adEvent =>
            {
                if (adEvent.Kind == RewardedAdEventKind.Rewarded) _provider.Dispose();
            };

            _ad.EmitRewarded(info);

            Assert.That(_ad.DisposeCalls, Is.EqualTo(1));
            Assert.That(PrivateCollectionCount("_contexts"), Is.Zero);
            Assert.That(PrivateCollectionCount("_completedAuctions"), Is.Zero);
            Assert.That(PrivateCollectionCount("_retiredAdIds"), Is.Zero);
            Assert.That(PrivateCollectionCount("_terminalRevenueByAuction"), Is.Zero);
        }

        private static IEnumerable<string> RewardedEventNames => FakeRewardedAd.EventNames;

        private void ReadyProvider()
        {
            _provider.Load();
            _provider.Initialize();
            _sdk.EmitInitializationSucceeded();
            Assert.That(_ad.LoadCalls, Is.EqualTo(1));
            _ad.EmitLoaded(AdInfo(null, null, null));
            _events.Clear();
        }

        private static LevelPlayAdSnapshot AdInfo(string adId, string auctionId,
            string placement)
            => new LevelPlayAdSnapshot(adId, auctionId, "one-rewarded-unit", placement,
                "test-network");

        private int PrivateCollectionCount(string fieldName)
        {
            var field = typeof(LevelPlayRewardedAdProvider).GetField(fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName + " must be explicit bounded state");
            object value = field.GetValue(_provider);
            var count = value?.GetType().GetProperty("Count");
            Assert.That(count, Is.Not.Null, fieldName + " must expose a collection count");
            return (int)count.GetValue(value);
        }

        private long PrivateLong(string fieldName)
        {
            var field = typeof(LevelPlayRewardedAdProvider).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName + " must be explicit correlation state");
            return (long)field.GetValue(_provider);
        }

        private void DrainThroughExistingMonetizationPump()
        {
            Assembly integrations = typeof(RewardedAdsConfig).Assembly;
            Type compositionType = integrations.GetType(
                "CatMetro.Integrations.RewardedAdsComposition", throwOnError: true);
            Type pumpType = integrations.GetType(
                "CatMetro.Integrations.MonetizationPump", throwOnError: true);
            ConstructorInfo constructor = compositionType.GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic).Single();
            object composition = constructor.Invoke(new object[] { null, null, null, null, null });
            FieldInfo drain = compositionType.GetField("_mainThreadDrain",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(drain, Is.Not.Null);
            drain.SetValue(composition, _provider);
            var host = new GameObject("[LevelPlayPumpDrainTests]");
            try
            {
                var pump = host.AddComponent(pumpType);
                MethodInfo bind = pumpType.GetMethod("Bind",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo update = pumpType.GetMethod("Update",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(bind, Is.Not.Null);
                Assert.That(update, Is.Not.Null);
                bind.Invoke(pump, new[] { null, composition });
                update.Invoke(pump, null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                ((IDisposable)composition).Dispose();
            }
        }

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
