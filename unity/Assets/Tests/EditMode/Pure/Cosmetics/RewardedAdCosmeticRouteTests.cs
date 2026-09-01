using System;
using CatMetro.Services.Ads;
using CatMetro.Services.Cosmetics;
using NUnit.Framework;

namespace CatMetro.Tests.Cosmetics
{
    public sealed class RewardedAdCosmeticRouteTests
    {
        [TearDown]
        public void TearDown() => RewardedAdRuntime.ResetForTests();

        [Test]
        public void ExactGrantedCompletion_ForwardsBothIdsAndCompletesOnce()
        {
            var ads = new ExactAds();
            RewardedAdRuntime.Install(ads);
            using var route = new RewardedAdCosmeticRoute();
            CosmeticRewardedCompletion result = CosmeticRewardedCompletion.NotGranted;
            int calls = 0;

            route.Request("wardrobe_try_conductor", "outfit_conductor", value =>
            {
                calls++;
                result = value;
            });
            ads.Complete(new RewardedAdCompletion(4L, "wardrobe_try_conductor",
                "outfit_conductor", RewardedAdCompletionKind.Granted));
            ads.Complete(new RewardedAdCompletion(4L, "wardrobe_try_conductor",
                "outfit_conductor", RewardedAdCompletionKind.Granted));

            Assert.That(ads.LastPlacement, Is.EqualTo("wardrobe_try_conductor"));
            Assert.That(ads.LastEntitlement, Is.EqualTo("outfit_conductor"));
            Assert.That(result, Is.EqualTo(CosmeticRewardedCompletion.Granted));
            Assert.That(calls, Is.EqualTo(1));
        }

        [TestCase(0L, "wardrobe_try_conductor", "outfit_conductor",
            RewardedAdCompletionKind.Granted)]
        [TestCase(4L, "wrong", "outfit_conductor", RewardedAdCompletionKind.Granted)]
        [TestCase(4L, "wardrobe_try_conductor", "wrong", RewardedAdCompletionKind.Granted)]
        [TestCase(4L, "wardrobe_try_conductor", "outfit_conductor",
            RewardedAdCompletionKind.ClosedWithoutReward)]
        [TestCase(4L, "wardrobe_try_conductor", "outfit_conductor",
            RewardedAdCompletionKind.DisplayFailed)]
        [TestCase(4L, "wardrobe_try_conductor", "outfit_conductor",
            RewardedAdCompletionKind.Unavailable)]
        [TestCase(4L, "wardrobe_try_conductor", "outfit_conductor",
            RewardedAdCompletionKind.GrantFailed)]
        [TestCase(4L, "wardrobe_try_conductor", "outfit_conductor",
            RewardedAdCompletionKind.Cancelled)]
        public void NonExactOrNonGrantedCompletion_FailsClosedOnce(long attemptId,
            string placementId, string entitlementId, RewardedAdCompletionKind kind)
        {
            var ads = new ExactAds();
            RewardedAdRuntime.Install(ads);
            using var route = new RewardedAdCosmeticRoute();
            int calls = 0;
            CosmeticRewardedCompletion result = CosmeticRewardedCompletion.Granted;

            route.Request("wardrobe_try_conductor", "outfit_conductor", value =>
            {
                calls++;
                result = value;
            });
            ads.Complete(new RewardedAdCompletion(attemptId, placementId, entitlementId, kind));
            ads.Complete(new RewardedAdCompletion(4L, "wardrobe_try_conductor",
                "outfit_conductor", RewardedAdCompletionKind.Granted));

            Assert.That(result, Is.EqualTo(CosmeticRewardedCompletion.NotGranted));
            Assert.That(calls, Is.EqualTo(1));
        }

        [TestCase(false, RewardedShowOutcome.Started)]
        [TestCase(true, RewardedShowOutcome.Unavailable)]
        [TestCase(true, RewardedShowOutcome.Busy)]
        public void MissingUnavailableOrRejectedExactSource_FailsClosedOnce(bool canShow,
            RewardedShowOutcome shown)
        {
            var ads = new ExactAds { CanShowExact = canShow, NextShow = shown };
            RewardedAdRuntime.Install(ads);
            using var route = new RewardedAdCosmeticRoute();
            int calls = 0;
            CosmeticRewardedCompletion result = CosmeticRewardedCompletion.Granted;

            route.Request("wardrobe_try_conductor", "outfit_conductor", value =>
            {
                calls++;
                result = value;
            });

            Assert.That(result, Is.EqualTo(CosmeticRewardedCompletion.NotGranted));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(ads.ExactShowCalls, Is.EqualTo(canShow ? 1 : 0));
        }

        [Test]
        public void RuntimeReplacement_CompletesOldRequestImmediatelyWithoutWaitingForOldSource()
        {
            var old = new ExactAds();
            RewardedAdRuntime.Install(old);
            using var route = new RewardedAdCosmeticRoute();
            int calls = 0;
            CosmeticRewardedCompletion result = CosmeticRewardedCompletion.Granted;
            route.Request("wardrobe_try_conductor", "outfit_conductor", value =>
            {
                calls++;
                result = value;
            });

            RewardedAdRuntime.Install(new ExactAds());

            Assert.That(result, Is.EqualTo(CosmeticRewardedCompletion.NotGranted));
            Assert.That(calls, Is.EqualTo(1));
            old.Complete(new RewardedAdCompletion(4L, "wardrobe_try_conductor",
                "outfit_conductor", RewardedAdCompletionKind.Granted));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void CanShowRuntimeReplacement_FailsClosedWithoutCallingStaleSource()
        {
            var old = new ExactAds();
            RewardedAdRuntime.Install(old);
            using var route = new RewardedAdCosmeticRoute();
            old.OnExactCanShow = () => RewardedAdRuntime.Install(new ExactAds());
            int replacementCalls = 0;
            CosmeticRewardedCompletion replacementResult = CosmeticRewardedCompletion.Granted;
            route.Request("wardrobe_try_conductor", "outfit_conductor", result =>
            {
                replacementCalls++;
                replacementResult = result;
            });
            Assert.That(replacementCalls, Is.EqualTo(1));
            Assert.That(replacementResult, Is.EqualTo(CosmeticRewardedCompletion.NotGranted));
            Assert.That(old.ExactShowCalls, Is.Zero);
        }

        [Test]
        public void RuntimeUninstallDuringPendingRequest_FailsClosedOnceAndOldSourceStaysInert()
        {
            var old = new ExactAds();
            RewardedAdRuntime.Install(old);
            using var route = new RewardedAdCosmeticRoute();
            int calls = 0;
            CosmeticRewardedCompletion result = CosmeticRewardedCompletion.Granted;
            route.Request("wardrobe_try_conductor", "outfit_conductor", value =>
            {
                calls++;
                result = value;
            });

            Assert.That(old.ExactShowCalls, Is.EqualTo(1));
            Assert.That(RewardedAdRuntime.Uninstall(old), Is.True);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(result, Is.EqualTo(CosmeticRewardedCompletion.NotGranted));

            old.Complete(new RewardedAdCompletion(4L, "wardrobe_try_conductor",
                "outfit_conductor", RewardedAdCompletionKind.Granted));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(route.CanOffer("wardrobe_try_conductor", "outfit_conductor"), Is.False);
        }

        [Test]
        public void SecondRequestWhileFirstIsPending_FailsNewRequestWithoutReplacingOldCompletion()
        {
            var ads = new ExactAds();
            RewardedAdRuntime.Install(ads);
            using var route = new RewardedAdCosmeticRoute();
            int first = 0;
            int second = 0;
            CosmeticRewardedCompletion firstResult = CosmeticRewardedCompletion.NotGranted;
            CosmeticRewardedCompletion secondResult = CosmeticRewardedCompletion.Granted;
            route.Request("wardrobe_try_conductor", "outfit_conductor", result =>
            {
                first++;
                firstResult = result;
            });
            route.Request("wardrobe_try_conductor", "outfit_conductor", result =>
            {
                second++;
                secondResult = result;
            });
            ads.Complete(new RewardedAdCompletion(4L, "wardrobe_try_conductor",
                "outfit_conductor", RewardedAdCompletionKind.Granted));

            Assert.That(first, Is.EqualTo(1));
            Assert.That(second, Is.EqualTo(1));
            Assert.That(firstResult, Is.EqualTo(CosmeticRewardedCompletion.Granted));
            Assert.That(secondResult, Is.EqualTo(CosmeticRewardedCompletion.NotGranted));
        }

        [Test]
        public void NonExactRuntimeSource_FailsClosedWithoutCallingLegacyShow()
        {
            var ads = new LegacyAds();
            RewardedAdRuntime.Install(ads);
            using var route = new RewardedAdCosmeticRoute();
            int calls = 0;
            route.Request("wardrobe_try_conductor", "outfit_conductor", _ => calls++);

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(ads.ShowCalls, Is.Zero);
        }

        [Test]
        public void RuntimeAvailabilityAndReplacement_SubscribeExactlyOnceAndDisposeFailsClosed()
        {
            var first = new ExactAds();
            RewardedAdRuntime.Install(first);
            var route = new RewardedAdCosmeticRoute();
            int changes = 0;
            route.AvailabilityChanged += () => changes++;
            var second = new ExactAds();

            first.RaiseAvailability();
            RewardedAdRuntime.Install(second);
            second.RaiseAvailability();
            route.Dispose();
            second.RaiseAvailability();
            int calls = 0;
            CosmeticRewardedCompletion result = CosmeticRewardedCompletion.Granted;
            route.Request("wardrobe_try_conductor", "outfit_conductor", value =>
            {
                calls++;
                result = value;
            });

            Assert.That(first.AvailabilityAddCalls, Is.EqualTo(1));
            Assert.That(first.AvailabilityRemoveCalls, Is.EqualTo(1));
            Assert.That(second.AvailabilityAddCalls, Is.EqualTo(1));
            Assert.That(second.AvailabilityRemoveCalls, Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(3),
                "the live source, replacement, and replacement availability each reproject once");
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(result, Is.EqualTo(CosmeticRewardedCompletion.NotGranted));
            Assert.That(route.CanOffer("wardrobe_try_conductor", "outfit_conductor"), Is.False);
        }

        private sealed class ExactAds : IRewardedAds, IRewardedAdExactCompletionSource
        {
            private Action _availabilityChanged;
            private Action<RewardedAdCompletion> _completion;
            public int AvailabilityAddCalls { get; private set; }
            public int AvailabilityRemoveCalls { get; private set; }
            public bool CanShowExact { get; set; } = true;
            public Action OnExactCanShow { get; set; }
            public RewardedShowOutcome NextShow { get; set; } = RewardedShowOutcome.Started;
            public int ExactShowCalls { get; private set; }
            public string LastPlacement { get; private set; }
            public string LastEntitlement { get; private set; }
            public event Action AvailabilityChanged
            {
                add { AvailabilityAddCalls++; _availabilityChanged += value; }
                remove { AvailabilityRemoveCalls++; _availabilityChanged -= value; }
            }
            public bool CanShow(string placementId) => CanShowExact;
            public bool CanShow(string placementId, string entitlementId)
            {
                OnExactCanShow?.Invoke();
                return CanShowExact;
            }
            public RewardedShowOutcome Show(string placementId) => NextShow;
            public RewardedShowOutcome Show(string placementId, string entitlementId,
                Action<RewardedAdCompletion> completed)
            {
                ExactShowCalls++;
                LastPlacement = placementId;
                LastEntitlement = entitlementId;
                _completion = completed;
                return NextShow;
            }
            public void Complete(RewardedAdCompletion completion) => _completion?.Invoke(completion);
            public void RaiseAvailability() => _availabilityChanged?.Invoke();
        }

        private sealed class LegacyAds : IRewardedAds
        {
            public int ShowCalls { get; private set; }
            public event Action AvailabilityChanged { add { } remove { } }
            public bool CanShow(string placementId) => true;
            public RewardedShowOutcome Show(string placementId)
            {
                ShowCalls++;
                return RewardedShowOutcome.Started;
            }
        }
    }
}
