using System;
using CatMetro.Application.Save;
using CatMetro.Integrations;
using CatMetro.Services.Ads;
using CatMetro.Services.Purchases;
using CatMetro.Services.Retry;
using CatMetro.Tests.Ads;
using CatMetro.Tests.Purchases;
using CatMetro.Tests.Save;
using NUnit.Framework;

namespace CatMetro.Tests
{
    public sealed class FailureRewindCompositionTests
    {
        private const string PlacementJson = @"{ ""placements"": [{
            ""id"": ""rewind_failure"", ""rewardKind"": ""failure_rewind"", ""enabled"": true,
            ""caps"": { ""session"": 2, ""localDate"": 5 } }] }";

        [SetUp]
        public void SetUp() { SaveRuntime.ResetForTests(); RewardedAdRuntime.ResetForTests(); }
        [TearDown]
        public void TearDown() { SaveRuntime.ResetForTests(); RewardedAdRuntime.ResetForTests(); }

        [Test]
        public void ConfiguredProviderWithWritableSaveStartsDurableSessionAndMakesRouteAvailable()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            SaveRuntime.Install(store);
            using var composition = Composition(() => new RewardedAdFixtures.Provider());
            using var route = new RewardedAdFailureRewindRoute();
            composition.Bind();
            Assert.That((int)store.State.Payload["profile"]["sessionCount"], Is.EqualTo(1));
            Assert.That(route.CanOffer("rewind_failure"), Is.True);
        }

        [Test]
        public void NoConfiguredProviderDoesNotTouchCapsOrPublishFailureCapability()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs);
            store.Load();
            string before = store.State.Payload.ToString();
            fs.Calls.Clear();
            SaveRuntime.Install(store);
            using var composition = Composition(() => null);
            composition.Bind();
            composition.OnApplicationPause(false);
            composition.Tick(100);
            Assert.That(RewardedAdRuntime.IsInstalled, Is.False);
            Assert.That(RewardedAdRuntime.Current, Is.Not.InstanceOf<IRewardedAdFailureRewindSource>());
            using var route = new RewardedAdFailureRewindRoute();
            Assert.That(route.CanOffer("rewind_failure"), Is.False);
            Assert.That(store.State.Payload.ToString(), Is.EqualTo(before));
            Assert.That(fs.Calls, Is.Empty);
            composition.OnApplicationPause(true);
            Assert.That(store.State.Payload.ToString(), Is.EqualTo(before),
                "the pre-existing pause flush must not introduce failure caps or session data");
        }

        [TestCase(1799, false)]
        [TestCase(1800, true)]
        public void PauseAndResumeReevaluateExactDurableSessionBoundary(int gap, bool available)
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            long now = Noon;
            var provider = new RewardedAdFixtures.Provider();
            using var composition = Composition(() => provider, () => now);
            composition.Bind();
            SaveRuntime.Install(store);
            Reward(provider);
            Reward(provider);
            now += 20;
            composition.OnApplicationPause(true);
            Assert.That((long)store.State.Payload["profile"]["lastSeenAtUtc"], Is.EqualTo(now));
            now += gap;
            composition.OnApplicationPause(false);
            using var route = new RewardedAdFailureRewindRoute();
            Assert.That(route.CanOffer("rewind_failure"), Is.EqualTo(available));
            Assert.That((int)store.State.Payload["caps"]["counters"]["rewind_failure"], Is.EqualTo(2));
        }

        [Test]
        public void ForegroundAtAdvancingUtcDayResetsSessionEvenWithSameLocalDate()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            SaveRuntime.Install(store);
            long now = Noon + 43200 - 1;
            var provider = new RewardedAdFixtures.Provider();
            using var composition = Composition(() => provider, () => now);
            composition.Bind();
            Reward(provider);
            Reward(provider);
            composition.OnApplicationPause(true);
            now++;
            composition.OnApplicationFocus(true);
            using var route = new RewardedAdFailureRewindRoute();
            Assert.That(route.CanOffer("rewind_failure"), Is.True);
            Assert.That((int)store.State.Payload["caps"]["counters"]["rewind_failure"], Is.EqualTo(2));
            Assert.That((int)store.State.Payload["profile"]["sessionCount"], Is.EqualTo(2));
        }

        [Test]
        public void QuickCompositionRelaunchRetainsCapFromDisk()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            SaveRuntime.Install(store);
            long now = Noon;
            var provider = new RewardedAdFixtures.Provider();
            using (var first = Composition(() => provider, () => now))
            {
                first.Bind();
                Reward(provider);
                Reward(provider);
            }
            var restartedStore = SFixtures.Store(root);
            restartedStore.Load();
            SaveRuntime.Install(restartedStore);
            now += 60;
            using var second = Composition(() => new RewardedAdFixtures.Provider(), () => now);
            second.Bind();
            using var route = new RewardedAdFailureRewindRoute();
            Assert.That(route.CanOffer("rewind_failure"), Is.False);
            Assert.That((int)restartedStore.State.Payload["profile"]["sessionCount"], Is.EqualTo(1));
        }

        [Test]
        public void ReadOnlySaveDoesNotCreateProviderOrPublishFailureCapability()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            SFixtures.WriteRaw(store.SavePath,
                SFixtures.FileWithVersion(checked((ushort)(SaveDefaults.SAVE_VERSION + 1))));
            store.Load();
            Assert.That(store.ReadOnlyMode, Is.True);
            SaveRuntime.Install(store);
            string before = store.State.Payload.ToString();
            using var composition = Composition(() =>
            {
                Assert.Fail("a read-only save cannot own an ad provider");
                return null;
            });
            composition.Bind();
            composition.OnApplicationPause(false);
            Assert.That(RewardedAdRuntime.IsInstalled, Is.False);
            Assert.That(store.State.Payload.ToString(), Is.EqualTo(before));
        }

        [Test]
        public void FailedStartupTouchDoesNotOfferButSuccessfulForegroundTouchCanRecover()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs);
            store.Load();
            SaveRuntime.Install(store);
            fs.FaultPoint = SFixtures.Fault.InReplace;
            using var composition = Composition(() => new RewardedAdFixtures.Provider());
            composition.Bind();
            using var route = new RewardedAdFailureRewindRoute();
            Assert.That(route.CanOffer("rewind_failure"), Is.False);
            Assert.That((int)store.State.Payload["profile"]["sessionCount"], Is.Zero);
            fs.FaultPoint = SFixtures.Fault.None;
            composition.OnApplicationPause(false);
            Assert.That(route.CanOffer("rewind_failure"), Is.True);
        }

        [Test]
        public void ForegroundHeartbeatRetainsConsumedSessionBeyondThirtyMinutesAndThrottlesWrites()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs);
            store.Load();
            SaveRuntime.Install(store);
            long now = Noon;
            var provider = new RewardedAdFixtures.Provider();
            using var composition = Composition(() => provider, () => now);
            composition.Bind();
            Reward(provider);
            Reward(provider);
            fs.Calls.Clear();
            using var route = new RewardedAdFailureRewindRoute();
            for (int second = 1; second <= 1860; second++)
            {
                now = Noon + second;
                composition.Tick(second);
                Assert.That(route.CanOffer("rewind_failure"), Is.False);
            }
            Assert.That((int)store.State.Payload["profile"]["sessionCount"], Is.EqualTo(1));
            Assert.That((int)store.State.Payload["caps"]["sessionCounters"]["rewind_failure"], Is.EqualTo(2));
            Assert.That((long)store.State.Payload["profile"]["lastSeenAtUtc"], Is.EqualTo(Noon + 1860));
            Assert.That(fs.Calls, Has.Count.EqualTo(62), "31 one-minute atomic touches; queries add no writes");
        }

        [Test]
        public void PausedTicksDoNotExtendSessionAndForegroundAfterThirtyMinutesResetsIt()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs);
            store.Load();
            SaveRuntime.Install(store);
            long now = Noon;
            var provider = new RewardedAdFixtures.Provider();
            using var composition = Composition(() => provider, () => now);
            composition.Bind();
            Reward(provider);
            Reward(provider);
            composition.OnApplicationPause(true);
            fs.Calls.Clear();
            for (int minute = 1; minute <= 30; minute++)
            {
                now = Noon + minute * 60;
                composition.Tick(minute * 60);
            }
            Assert.That(fs.Calls, Is.Empty);
            composition.OnApplicationPause(false);
            using var route = new RewardedAdFailureRewindRoute();
            Assert.That(route.CanOffer("rewind_failure"), Is.True);
            Assert.That((int)store.State.Payload["profile"]["sessionCount"], Is.EqualTo(2));
        }

        private static readonly long Noon = new DateTimeOffset(2026, 9, 5, 12, 0, 0,
            TimeSpan.Zero).ToUnixTimeSeconds();

        private static void Reward(RewardedAdFixtures.Provider provider)
        {
            var source = (IRewardedAdFailureRewindSource)RewardedAdRuntime.Current;
            RewardedAdCompletionKind? result = null;
            Assert.That(source.ShowFailureRewind("rewind_failure", null,
                completed => result = completed.Kind, out long attempt), Is.EqualTo(RewardedShowOutcome.Started));
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt, "rewind_failure"));
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Closed, attempt, "rewind_failure"));
            Assert.That(result, Is.EqualTo(RewardedAdCompletionKind.Granted));
        }

        private static RewardedAdsComposition Composition(Func<IRewardedAdProvider> provider,
            Func<long> clock = null)
        {
            var service = new PurchaseService(PFixtures.TinyCatalog());
            return new RewardedAdsComposition(service,
                RewardedPlacementCatalog.Parse(PlacementJson, service.Catalog), provider,
                new RewardedAdFixtures.Reporter(), () => "2026-09-05", clock ?? (() => Noon));
        }
    }
}
