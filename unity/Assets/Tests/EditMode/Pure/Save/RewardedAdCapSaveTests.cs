using System;
using System.IO;
using System.Linq;
using System.Text;
using CatMetro.Application.Save;
using CatMetro.Services.Ads;
using CatMetro.Services.Purchases;
using CatMetro.Tests.Ads;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CatMetro.Tests.Save
{
    public sealed class RewardedAdCapSaveTests
    {
        [Test]
        public void IncrementCommitsAndSurvivesFreshSaveStoreReload()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();

            Assert.That(new RewardedAdSaveStore(store)
                .TryIncrementLocalDateCount("wardrobe", "2026-08-29"), Is.True);

            var reloaded = SFixtures.Store(root);
            Assert.That(reloaded.Load(), Is.EqualTo(CatMetro.Services.LoadResult.Ok));
            Assert.That(new RewardedAdSaveStore(reloaded)
                .ReadLocalDateCount("wardrobe", "2026-08-29"), Is.EqualTo(1));
        }

        [Test]
        public void ChangedDateResetsOnlyRewardedCountersAndPreservesUnknownAndLegacySiblings()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            var legacy = (JObject)store.State.Payload["caps"]["counters"];
            legacy["rewind_failure"] = 4;
            var caps = (JObject)store.State.Payload["caps"];
            caps["futureSibling"] = new JObject { ["opaque"] = new JArray(1, 2, 3) };
            caps["rewarded"]["dateKey"] = "2026-08-28";
            caps["rewarded"]["counters"] = new JObject { ["old"] = 7, ["other"] = 8 };
            store.CommitAtomic();
            string legacyBytes = legacy.ToString(Formatting.None);
            string siblingBytes = caps["futureSibling"].ToString(Formatting.None);

            Assert.That(new RewardedAdSaveStore(store)
                .TryIncrementLocalDateCount("new", "2026-08-29"), Is.True);

            var reloaded = SFixtures.Store(root);
            reloaded.Load();
            Assert.That(reloaded.State.Payload["caps"]["counters"].ToString(Formatting.None),
                Is.EqualTo(legacyBytes));
            Assert.That(reloaded.State.Payload["caps"]["futureSibling"].ToString(Formatting.None),
                Is.EqualTo(siblingBytes));
            Assert.That((string)reloaded.State.Payload["caps"]["rewarded"]["dateKey"],
                Is.EqualTo("2026-08-29"));
            var rewarded = (JObject)reloaded.State.Payload["caps"]["rewarded"]["counters"];
            Assert.That(rewarded.Properties().Select(p => p.Name), Is.EqualTo(new[] { "new" }));
            Assert.That((int)rewarded["new"], Is.EqualTo(1));
        }

        [Test]
        public void ReadRejectsDifferentOrMalformedDateAndCountWithoutThrowing()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            var rewarded = (JObject)store.State.Payload["caps"]["rewarded"];
            rewarded["dateKey"] = "2026-08-29";
            rewarded["counters"] = new JObject
            {
                ["negative"] = -1,
                ["text"] = "4",
                ["valid"] = 3,
            };
            var capStore = new RewardedAdSaveStore(store);

            Assert.DoesNotThrow(() => capStore.ReadLocalDateCount("valid", "2026-08-29"));
            Assert.That(capStore.ReadLocalDateCount("valid", "2026-08-28"), Is.Zero);
            Assert.That(capStore.ReadLocalDateCount("negative", "2026-08-29"), Is.Zero);
            Assert.That(capStore.ReadLocalDateCount("text", "2026-08-29"), Is.Zero);
            Assert.That(capStore.ReadLocalDateCount(null, "2026-08-29"), Is.Zero);
        }

        [Test]
        public void IncrementSaturatesAtIntMaxValue()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            store.State.Payload["caps"]["rewarded"]["dateKey"] = "2026-08-29";
            store.State.Payload["caps"]["rewarded"]["counters"] =
                new JObject { ["p0"] = int.MaxValue };

            Assert.That(new RewardedAdSaveStore(store)
                .TryIncrementLocalDateCount("p0", "2026-08-29"), Is.True);

            var reloaded = SFixtures.Store(root);
            reloaded.Load();
            Assert.That(new RewardedAdSaveStore(reloaded)
                .ReadLocalDateCount("p0", "2026-08-29"), Is.EqualTo(int.MaxValue));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RefusedOrThrowingCapWriteRestoresOriginalPayloadIdentity(bool throws)
        {
            using var root = new SFixtures.TempRoot();
            SaveStore store;
            if (throws)
            {
                var fs = new SFixtures.RecordingFs { FaultPoint = SFixtures.Fault.InWriteTemp };
                store = SFixtures.Store(root, fs);
            }
            else
            {
                var boundsJson = JObject.Parse(Encoding.UTF8.GetString(SFixtures.RepoBoundsBytes()));
                boundsJson["SAVE_MAX_BYTES"] = 1;
                var bounds = RuntimeBounds.Parse(Encoding.UTF8.GetBytes(boundsJson.ToString())).Value;
                store = SFixtures.Store(root, bounds: bounds);
            }
            store.Load();
            var original = store.State.Payload;

            Assert.That(new RewardedAdSaveStore(store)
                .TryIncrementLocalDateCount("p0", "2026-08-29"), Is.False);
            Assert.That(store.State.Payload, Is.SameAs(original));
        }

        [Test]
        public void LeaseCommitSurvivesFailedCounterCommitAndCurrentCoordinatorStillBlocksReplay()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs);
            store.Load();
            var saveData = new RewardedAdSaveStore(store);
            var capFault = new FaultingCapStore(saveData, fs);
            var clock = new RewardedAdFixtures.Clock();
            var service = new PurchaseService(Purchases.PFixtures.TinyCatalog(), clock: clock.Read);
            service.AttachLeasePersistence(saveData);
            var provider = new RewardedAdFixtures.Provider();
            var reporter = new RewardedAdFixtures.Reporter();
            var placements = RewardedAdFixtures.Placements(
                ", \"caps\": { \"localDate\": 1 }");
            using var coordinator = new RewardedAdCoordinator(placements, service, provider,
                reporter, capFault, () => "2026-08-29");
            coordinator.Start();
            coordinator.Show("p0");
            long attempt = provider.Shows.Single().AttemptId;

            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Rewarded, attempt, "p0"));
            provider.Emit(new RewardedAdEvent(RewardedAdEventKind.Closed, attempt, "p0"));

            Assert.That(service.IsUnlocked("outfit_conductor"), Is.True);
            clock.Advance(3_601L);
            Assert.That(service.CanOfferAdFor("outfit_conductor"), Is.True,
                "the lease must expire before the coordinator cap assertion");
            Assert.That(coordinator.CanShow("p0"), Is.False,
                "the failed cap commit still consumes the current session opportunity");
            var reloaded = SFixtures.Store(root);
            Assert.That(reloaded.Load(), Is.EqualTo(CatMetro.Services.LoadResult.Ok));
            var reloadedData = new RewardedAdSaveStore(reloaded);
            Assert.That(reloadedData.ReadLocalLeases(), Has.Count.EqualTo(1),
                "the earlier durable lease is not rolled back with the later cap fault");
            Assert.That(reloadedData.ReadLocalDateCount("p0", "2026-08-29"), Is.Zero,
                "the failed counter candidate never reached committed bytes");
        }

        private sealed class FaultingCapStore : IRewardedAdCapStore
        {
            private readonly RewardedAdSaveStore _inner;
            private readonly SFixtures.RecordingFs _fs;

            public FaultingCapStore(RewardedAdSaveStore inner, SFixtures.RecordingFs fs)
            {
                _inner = inner;
                _fs = fs;
            }

            public int ReadLocalDateCount(string placementId, string localDateKey)
                => _inner.ReadLocalDateCount(placementId, localDateKey);

            public bool TryIncrementLocalDateCount(string placementId, string localDateKey)
            {
                _fs.FaultPoint = SFixtures.Fault.InWriteTemp;
                return _inner.TryIncrementLocalDateCount(placementId, localDateKey);
            }
        }
    }
}
