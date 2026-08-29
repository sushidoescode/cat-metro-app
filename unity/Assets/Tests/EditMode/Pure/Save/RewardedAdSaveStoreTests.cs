using System.Collections.Generic;
using System.Text;
using CatMetro.Application.Save;
using CatMetro.Services.Purchases;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CatMetro.Tests.Save
{
    public sealed class RewardedAdSaveStoreTests
    {
        [Test]
        public void ReplaceRewardedAdLeases_CommitsSortedRowsAndReloadsTheSavedAbsoluteExpiry()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs);
            store.Load();
            store.State.Payload["futureTopLevel"] = new JObject { ["kept"] = "yes" };
            store.State.Payload["entitlements"]["futureEntitlementField"] = 17;
            store.CommitAtomic();
            var originalPayload = store.State.Payload;
            var leases = new RewardedAdSaveStore(store);
            fs.Calls.Clear();

            Assert.That(leases.TryReplaceRewardedAdLeases(new[]
            {
                new EntitlementGrant("zebra", GrantSource.RewardedAd, 9_001L),
                new EntitlementGrant("alpha", GrantSource.RewardedAd, 8_123L),
            }), Is.True);

            Assert.That(store.State.Payload, Is.Not.SameAs(originalPayload),
                "the committed payload is a clone, never an in-place edit");
            Assert.That(((JArray)originalPayload["entitlements"]["localLeases"]), Is.Empty);
            var rows = (JArray)store.State.Payload["entitlements"]["localLeases"];
            Assert.That((string)rows[0]["entitlementId"], Is.EqualTo("alpha"));
            Assert.That((string)rows[1]["entitlementId"], Is.EqualTo("zebra"));
            Assert.That(fs.Calls, Is.EqualTo(new[]
            {
                "WriteTempDurable:save.dat.tmp",
                "Replace:save.dat",
            }), "the replacement uses SaveStore's atomic durable write path");

            var reloaded = SFixtures.Store(root, new SFixtures.RecordingFs());
            Assert.That(reloaded.Load(), Is.EqualTo(CatMetro.Services.LoadResult.Ok));
            var loaded = new RewardedAdSaveStore(reloaded).ReadLocalLeases();
            Assert.That(loaded, Has.Count.EqualTo(2));
            Assert.That(loaded[0].EntitlementId, Is.EqualTo("alpha"));
            Assert.That(loaded[0].ExpiresAtUnixSeconds, Is.EqualTo(8_123L),
                "this is read from newly loaded committed bytes, not the authored JObject");
            Assert.That((string)reloaded.State.Payload["futureTopLevel"]["kept"], Is.EqualTo("yes"));
            Assert.That((int)reloaded.State.Payload["entitlements"]["futureEntitlementField"], Is.EqualTo(17));
        }

        [Test]
        public void ReplaceRewardedAdLeases_RefusalRestoresTheOriginalPayloadIdentity()
        {
            using var root = new SFixtures.TempRoot();
            var boundsJson = JObject.Parse(Encoding.UTF8.GetString(SFixtures.RepoBoundsBytes()));
            boundsJson["SAVE_MAX_BYTES"] = 1;
            var bounds = RuntimeBounds.Parse(Encoding.UTF8.GetBytes(boundsJson.ToString())).Value;
            var store = SFixtures.Store(root, bounds: bounds);
            store.Load();
            var originalPayload = store.State.Payload;

            Assert.That(new RewardedAdSaveStore(store).TryReplaceRewardedAdLeases(new[]
            {
                new EntitlementGrant("alpha", GrantSource.RewardedAd, 8_123L)
            }), Is.False);
            Assert.That(store.State.Payload, Is.SameAs(originalPayload));
        }

        [Test]
        public void ReplaceRewardedAdLeases_ExceptionRestoresTheOriginalPayloadIdentity()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs { FaultPoint = SFixtures.Fault.InWriteTemp };
            var store = SFixtures.Store(root, fs);
            store.Load();
            var originalPayload = store.State.Payload;

            Assert.That(new RewardedAdSaveStore(store).TryReplaceRewardedAdLeases(new[]
            {
                new EntitlementGrant("alpha", GrantSource.RewardedAd, 8_123L)
            }), Is.False);
            Assert.That(store.State.Payload, Is.SameAs(originalPayload));
        }

        [Test]
        public void ReadLocalLeases_SkipsMalformedRowsWithoutThrowing()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            store.State.Payload["entitlements"]["localLeases"] = new JArray
            {
                new JObject { ["entitlementId"] = "alpha", ["expiresAtUnixSeconds"] = 8_123L },
                new JObject { ["entitlementId"] = "missing_expiry" },
                new JObject { ["entitlementId"] = 3, ["expiresAtUnixSeconds"] = 1L },
                new JValue("not an object"),
            };

            IReadOnlyList<EntitlementGrant> read = null;
            Assert.DoesNotThrow(() => read = new RewardedAdSaveStore(store).ReadLocalLeases());
            Assert.That(read, Has.Count.EqualTo(1));
            Assert.That(read[0].EntitlementId, Is.EqualTo("alpha"));
            Assert.That(read[0].ExpiresAtUnixSeconds, Is.EqualTo(8_123L));
        }
    }
}
