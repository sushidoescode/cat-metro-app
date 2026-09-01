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

            route.Request("wardrobe.borrow.coat", "outfit_conductor", value =>
            {
                calls++;
                result = value;
            });
            ads.Complete(new RewardedAdCompletion(4L, "wardrobe.borrow.coat",
                "outfit_conductor", RewardedAdCompletionKind.Granted));
            ads.Complete(new RewardedAdCompletion(4L, "wardrobe.borrow.coat",
                "outfit_conductor", RewardedAdCompletionKind.Granted));

            Assert.That(ads.LastPlacement, Is.EqualTo("wardrobe.borrow.coat"));
            Assert.That(ads.LastEntitlement, Is.EqualTo("outfit_conductor"));
            Assert.That(result, Is.EqualTo(CosmeticRewardedCompletion.Granted));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void RuntimeReplacementBeforeCompletion_FailsClosed()
        {
            var old = new ExactAds();
            RewardedAdRuntime.Install(old);
            using var route = new RewardedAdCosmeticRoute();
            CosmeticRewardedCompletion result = CosmeticRewardedCompletion.Granted;
            route.Request("wardrobe.borrow.coat", "outfit_conductor", value => result = value);
            RewardedAdRuntime.Install(new ExactAds());

            old.Complete(new RewardedAdCompletion(4L, "wardrobe.borrow.coat",
                "outfit_conductor", RewardedAdCompletionKind.Granted));

            Assert.That(result, Is.EqualTo(CosmeticRewardedCompletion.NotGranted));
        }

        private sealed class ExactAds : IRewardedAds, IRewardedAdExactCompletionSource
        {
            private Action<RewardedAdCompletion> _completion;
            public event Action AvailabilityChanged;
            public string LastPlacement { get; private set; }
            public string LastEntitlement { get; private set; }
            public bool CanShow(string placementId) => true;
            public bool CanShow(string placementId, string entitlementId) => true;
            public RewardedShowOutcome Show(string placementId) => RewardedShowOutcome.Started;
            public RewardedShowOutcome Show(string placementId, string entitlementId,
                Action<RewardedAdCompletion> completed)
            {
                LastPlacement = placementId;
                LastEntitlement = entitlementId;
                _completion = completed;
                return RewardedShowOutcome.Started;
            }
            public void Complete(RewardedAdCompletion completion) => _completion?.Invoke(completion);
        }
    }
}
