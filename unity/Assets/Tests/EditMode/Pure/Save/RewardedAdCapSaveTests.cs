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
        private static readonly long Noon = new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        private const string Today = "2026-09-05";

        [TestCase(1)]
        [TestCase(1800)]
        public void FailureRewind_ObservationsAcrossUtcMidnightDoNotRollSession(int elapsed)
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs);
            var caps = new RewardedAdSaveStore(store);
            long beforeMidnight = Noon + 43200 - 1;
            Assert.That(caps.TryTouchFailureRewindSession(beforeMidnight, Today, allowSessionRollover: true), Is.True);
            Assert.That(caps.TryConsumeFailureRewind(beforeMidnight, Today), Is.True);
            fs.Calls.Clear();
            Assert.That(caps.CanOfferFailureRewind(beforeMidnight + elapsed, "2026-09-06"), Is.True);
            Assert.That(fs.Calls, Is.Empty);
            Assert.That(caps.TryConsumeFailureRewind(beforeMidnight + elapsed, "2026-09-06"), Is.True);
            Assert.That((int)store.State.Payload["profile"]["sessionCount"], Is.EqualTo(1));
            Assert.That((int)store.State.Payload["caps"]["sessionCounters"]["rewind_failure"], Is.EqualTo(2));
            Assert.That((int)store.State.Payload["caps"]["counters"]["rewind_failure"], Is.EqualTo(1));
            Assert.That(caps.CanOfferFailureRewind(beforeMidnight + elapsed, "2026-09-06"), Is.False);
            Assert.That(caps.TryConsumeFailureRewind(beforeMidnight + elapsed, "2026-09-06"), Is.False);
        }

        [Test]
        public void FailureRewind_RequiresDurableInitializationAndRetainsCapAcrossQuickRelaunch()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            IFailureRewindCapStore caps = new RewardedAdSaveStore(store);
            Assert.That(caps.CanOfferFailureRewind(Noon, Today), Is.False);
            Assert.That(caps.TryConsumeFailureRewind(Noon, Today), Is.False);
            Assert.That(caps.TryTouchFailureRewindSession(Noon, Today, allowSessionRollover: true), Is.True);
            Assert.That(caps.TryConsumeFailureRewind(Noon, Today), Is.True);
            Assert.That(caps.TryConsumeFailureRewind(Noon + 1, Today), Is.True);

            var reloaded = SFixtures.Store(root);
            Assert.That(reloaded.Load(), Is.EqualTo(CatMetro.Services.LoadResult.Ok));
            var restarted = new RewardedAdSaveStore(reloaded);
            Assert.That(restarted.TryTouchFailureRewindSession(Noon + 60, Today, allowSessionRollover: true), Is.True);
            Assert.That(restarted.CanOfferFailureRewind(Noon + 60, Today), Is.False);
            Assert.That(restarted.TryConsumeFailureRewind(Noon + 60, Today), Is.False);
            Assert.That((int)reloaded.State.Payload["profile"]["sessionCount"], Is.EqualTo(1));
            Assert.That((int)reloaded.State.Payload["caps"]["counters"]["rewind_failure"], Is.EqualTo(2));
        }

        [TestCase(1799, false)]
        [TestCase(1800, true)]
        public void FailureRewind_InactivityBoundaryResetsOnlySessionCounter(int seconds, bool resets)
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            var caps = new RewardedAdSaveStore(store);
            Assert.That(caps.TryTouchFailureRewindSession(Noon, Today, allowSessionRollover: true), Is.True);
            Assert.That(caps.TryConsumeFailureRewind(Noon, Today), Is.True);
            Assert.That(caps.TryConsumeFailureRewind(Noon, Today), Is.True);
            Assert.That(caps.TryTouchFailureRewindSession(Noon + seconds, Today, allowSessionRollover: true), Is.True);
            Assert.That(caps.CanOfferFailureRewind(Noon + seconds, Today), Is.EqualTo(resets));
            Assert.That((int)store.State.Payload["profile"]["sessionCount"], Is.EqualTo(resets ? 2 : 1));
            Assert.That((int)store.State.Payload["caps"]["counters"]["rewind_failure"], Is.EqualTo(2));
            Assert.That((long)store.State.Payload["profile"]["lastSeenAtUtc"], Is.EqualTo(Noon + seconds));
        }

        [Test]
        public void FailureRewind_ActivityExtendsSessionAndForegroundEntryAtUtcRolloverStartsNewSession()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            var caps = new RewardedAdSaveStore(store);
            long midnight = Noon + 43200;
            Assert.That(caps.TryTouchFailureRewindSession(midnight - 1700, Today, allowSessionRollover: true), Is.True);
            Assert.That(caps.TryConsumeFailureRewind(midnight - 1700, Today), Is.True);
            Assert.That(caps.TryTouchFailureRewindSession(midnight - 100, Today, allowSessionRollover: false), Is.True);
            Assert.That(caps.TryTouchFailureRewindSession(midnight - 1, Today, allowSessionRollover: false), Is.True);
            Assert.That((int)store.State.Payload["profile"]["sessionCount"], Is.EqualTo(1));
            Assert.That(caps.TryTouchFailureRewindSession(midnight, Today, allowSessionRollover: true), Is.True);
            Assert.That((int)store.State.Payload["profile"]["sessionCount"], Is.EqualTo(2));
            Assert.That((int)store.State.Payload["caps"]["sessionCounters"]["rewind_failure"], Is.Zero);
            Assert.That((int)store.State.Payload["caps"]["counters"]["rewind_failure"], Is.EqualTo(1));
        }

        [TestCase(1)]
        [TestCase(1800)]
        public void FailureRewind_ActivityTouchAcrossMidnightPreservesDurableSessionCap(int elapsed)
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            var caps = new RewardedAdSaveStore(store);
            long beforeMidnight = Noon + 43200 - 1;
            Assert.That(caps.TryTouchFailureRewindSession(beforeMidnight, Today,
                allowSessionRollover: true), Is.True);
            Assert.That(caps.TryConsumeFailureRewind(beforeMidnight, Today), Is.True);
            Assert.That(caps.TryConsumeFailureRewind(beforeMidnight, Today), Is.True);
            Assert.That(caps.TryTouchFailureRewindSession(beforeMidnight + elapsed, "2026-09-06",
                allowSessionRollover: false), Is.True);
            Assert.That(caps.CanOfferFailureRewind(beforeMidnight + elapsed, "2026-09-06"), Is.False);
            Assert.That(caps.TryConsumeFailureRewind(beforeMidnight + elapsed, "2026-09-06"), Is.False);
            var reloaded = SFixtures.Store(root);
            reloaded.Load();
            Assert.That((int)reloaded.State.Payload["profile"]["sessionCount"], Is.EqualTo(1));
            Assert.That((long)reloaded.State.Payload["profile"]["lastSeenAtUtc"], Is.EqualTo(beforeMidnight + elapsed));
            Assert.That((int)reloaded.State.Payload["caps"]["sessionCounters"]["rewind_failure"], Is.EqualTo(2));
            Assert.That((int)reloaded.State.Payload["caps"]["counters"]["rewind_failure"], Is.EqualTo(2));
        }

        [Test]
        public void FailureRewind_LocalRolloverIsReadOnlyUntilAtomicConsumptionAndPreservesSessionAndUnknownData()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs);
            var caps = new RewardedAdSaveStore(store);
            Assert.That(caps.TryTouchFailureRewindSession(Noon, Today, allowSessionRollover: true), Is.True);
            Assert.That(caps.TryConsumeFailureRewind(Noon, Today), Is.True);
            store.State.Payload["caps"]["counters"]["double_tickets"] = 3;
            store.State.Payload["caps"]["counters"]["future"] = new JArray(7);
            store.State.Payload["caps"]["sessionCounters"]["future"] = 9;
            var before = store.State.Payload.ToString();
            fs.Calls.Clear();
            Assert.That(caps.CanOfferFailureRewind(Noon + 1, "2026-09-06"), Is.True);
            Assert.That(store.State.Payload.ToString(), Is.EqualTo(before));
            Assert.That(fs.Calls, Is.Empty);
            Assert.That(caps.TryConsumeFailureRewind(Noon + 1, "2026-09-06"), Is.True);
            Assert.That(fs.Calls, Is.EqualTo(new[] { "WriteTempDurable:save.dat.tmp", "Replace:save.dat" }));
            Assert.That((int)store.State.Payload["caps"]["counters"]["rewind_failure"], Is.EqualTo(1));
            Assert.That((int)store.State.Payload["caps"]["counters"]["double_tickets"], Is.Zero);
            Assert.That((int)store.State.Payload["caps"]["sessionCounters"]["rewind_failure"], Is.EqualTo(2));
            Assert.That((int)store.State.Payload["caps"]["sessionCounters"]["future"], Is.EqualTo(9));
            Assert.That(JToken.DeepEquals(store.State.Payload["caps"]["counters"]["future"], new JArray(7)), Is.True);
            Assert.That(caps.CanOfferFailureRewind(Noon + 2, "2026-09-07"), Is.False);
        }

        [Test]
        public void FailureRewind_FifthDailyGrantAllowedAndSixthConsumesNeitherCounter()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            var caps = new RewardedAdSaveStore(store);
            for (int i = 0; i < 5; i++)
            {
                long now = Noon + (i / 2) * 1800;
                Assert.That(caps.TryTouchFailureRewindSession(now, Today, allowSessionRollover: true), Is.True);
                Assert.That(caps.TryConsumeFailureRewind(now, Today), Is.True);
            }
            var original = store.State.Payload;
            string before = original.ToString();
            Assert.That(caps.CanOfferFailureRewind(Noon + 3600, Today), Is.False);
            Assert.That(caps.TryConsumeFailureRewind(Noon + 3600, Today), Is.False);
            Assert.That(store.State.Payload, Is.SameAs(original));
            Assert.That(store.State.Payload.ToString(), Is.EqualTo(before));
            Assert.That((int)original["caps"]["sessionCounters"]["rewind_failure"], Is.EqualTo(1));
            Assert.That((int)original["caps"]["counters"]["rewind_failure"], Is.EqualTo(5));
        }

        [TestCase("caps")]
        [TestCase("caps.counters")]
        [TestCase("caps.sessionCounters")]
        [TestCase("caps.counters.rewind_failure")]
        [TestCase("caps.sessionCounters.rewind_failure")]
        [TestCase("caps.dateKey")]
        [TestCase("profile.lastSeenAtUtc")]
        [TestCase("profile.sessionCount")]
        public void FailureRewind_MalformedSaveFailsClosedEvenAtRollover(string path)
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            var caps = new RewardedAdSaveStore(store);
            Assert.That(caps.TryTouchFailureRewindSession(Noon, Today, allowSessionRollover: true), Is.True);
            store.State.Payload.SelectToken(path).Replace(new JArray("invalid"));
            var original = store.State.Payload;
            var before = original.ToString();
            Assert.That(caps.CanOfferFailureRewind(Noon + 1800, "2026-09-06"), Is.False);
            Assert.That(caps.TryConsumeFailureRewind(Noon + 1800, "2026-09-06"), Is.False);
            Assert.That(caps.TryTouchFailureRewindSession(Noon + 1800, "2026-09-06", allowSessionRollover: true), Is.False);
            Assert.That(store.State.Payload, Is.SameAs(original));
            Assert.That(original.ToString(), Is.EqualTo(before));
        }

        [TestCase(SFixtures.Fault.InWriteTemp, false)]
        [TestCase(SFixtures.Fault.InReplace, false)]
        [TestCase(SFixtures.Fault.InWriteTemp, true)]
        [TestCase(SFixtures.Fault.InReplace, true)]
        public void FailureRewind_FailedInitializationOrTouchBlocksAvailability(SFixtures.Fault fault, bool initialized)
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs);
            var caps = new RewardedAdSaveStore(store);
            if (initialized) Assert.That(caps.TryTouchFailureRewindSession(Noon, Today, allowSessionRollover: true), Is.True);
            var original = store.State.Payload;
            fs.FaultPoint = fault;
            Assert.That(caps.TryTouchFailureRewindSession(Noon + 1800, Today, allowSessionRollover: true), Is.False);
            Assert.That(store.State.Payload, Is.SameAs(original));
            Assert.That(caps.CanOfferFailureRewind(Noon + 1800, Today), Is.False);
            Assert.That(caps.TryConsumeFailureRewind(Noon + 1800, Today), Is.False);
        }

        [TestCase(SFixtures.Fault.InWriteTemp)]
        [TestCase(SFixtures.Fault.InReplace)]
        public void FailureRewind_FailedDualCommitRollsBackRolloverCountersAndTimestamp(SFixtures.Fault fault)
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs);
            var caps = new RewardedAdSaveStore(store);
            Assert.That(caps.TryTouchFailureRewindSession(Noon, Today, allowSessionRollover: true), Is.True);
            Assert.That(caps.TryConsumeFailureRewind(Noon, Today), Is.True);
            var original = store.State.Payload;
            var before = original.ToString();
            fs.FaultPoint = fault;
            Assert.That(caps.TryConsumeFailureRewind(Noon + 1800, "2026-09-06"), Is.False);
            Assert.That(store.State.Payload, Is.SameAs(original));
            Assert.That(original.ToString(), Is.EqualTo(before));
            Assert.That(caps.CanOfferFailureRewind(Noon + 1800, "2026-09-06"), Is.False);
            var reload = SFixtures.Store(root);
            Assert.That(reload.Load(), Is.EqualTo(CatMetro.Services.LoadResult.Ok));
            Assert.That(reload.State.Payload.ToString(), Is.EqualTo(before));
        }

        [TestCase("caps.sessionCounters.rewind_failure", "-1")]
        [TestCase("caps.counters.rewind_failure", "2147483648")]
        [TestCase("caps.counters.rewind_failure", "\"1\"")]
        [TestCase("profile.lastSeenAtUtc", "-1")]
        [TestCase("profile.sessionCount", "0")]
        [TestCase("caps.dateKey", "\"2026-99-99\"")]
        public void FailureRewind_InvalidKnownValueCannotMintCapacity(string path, string json)
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            var caps = new RewardedAdSaveStore(store);
            Assert.That(caps.TryTouchFailureRewindSession(Noon, Today, allowSessionRollover: true), Is.True);
            store.State.Payload.SelectToken(path).Replace(JToken.Parse(json));
            var before = store.State.Payload.ToString();
            Assert.That(caps.CanOfferFailureRewind(Noon, Today), Is.False);
            Assert.That(caps.TryConsumeFailureRewind(Noon + 1800, "2026-09-06"), Is.False);
            Assert.That(caps.TryTouchFailureRewindSession(Noon + 1800, "2026-09-06", allowSessionRollover: true), Is.False);
            Assert.That(store.State.Payload.ToString(), Is.EqualTo(before));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailureRewind_SaveRefusalBlocksInitializationOrConsumption(bool initialized)
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            var caps = new RewardedAdSaveStore(store);
            if (initialized) Assert.That(caps.TryTouchFailureRewindSession(Noon, Today, allowSessionRollover: true), Is.True);
            // Real SaveStore refusal before IO: exceed its configured byte ceiling.
            store.State.Payload["futurePadding"] = new string('x', SFixtures.RepoBounds().SaveMaxBytes);
            var original = store.State.Payload;
            Assert.That(initialized ? caps.TryConsumeFailureRewind(Noon, Today)
                : caps.TryTouchFailureRewindSession(Noon, Today, allowSessionRollover: true), Is.False);
            Assert.That(store.State.Payload, Is.SameAs(original));
            Assert.That(caps.CanOfferFailureRewind(Noon, Today), Is.False);
        }

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
