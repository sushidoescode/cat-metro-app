using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatMetro.Application.Save;
using CatMetro.Services;
using CatMetro.Services.Cosmetics;
using CatMetro.Tests.Save;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CatMetro.Tests.Cosmetics
{
    public sealed class CosmeticPersistenceTests
    {
        [Test]
        public void TryLoad_ReadsTheExactV3Profile()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            store.State.Payload["profile"]["cosmetics"] = Cosmetics(
                "blue_siamese",
                new JArray("yellow_longhair", "blue_siamese"),
                new JArray("earned_hat", "earned_frame"),
                new JArray(
                    Loadout("blue_siamese", "outfit_conductor", "accessory_bell", "frame_brass"),
                    Loadout("yellow_longhair", "", "accessory_future", "frame_lantern")));

            var persistence = new SaveStoreCosmeticProfilePersistence(store);

            Assert.That(persistence.TryLoad(out var snapshot), Is.True);
            Assert.That(snapshot.SelectedCatId, Is.EqualTo("blue_siamese"));
            CollectionAssert.AreEqual(new[] { "yellow_longhair", "blue_siamese" },
                snapshot.EarnedCatIds);
            CollectionAssert.AreEqual(new[] { "earned_hat", "earned_frame" },
                snapshot.EarnedItemIds);
            Assert.That(snapshot.Loadouts.Count, Is.EqualTo(2));
            AssertLoadout(snapshot.Loadouts[0], "blue_siamese", "outfit_conductor",
                "accessory_bell", "frame_brass");
            AssertLoadout(snapshot.Loadouts[1], "yellow_longhair", "", "accessory_future",
                "frame_lantern");
        }

        [Test]
        public void TryLoad_PreservesUnknownDesiredIdsWithoutCatalogueFiltering()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            store.State.Payload["profile"]["cosmetics"] = Cosmetics(
                "cat_from_future",
                new JArray("earned_cat_from_future"),
                new JArray("earned_item_from_future"),
                new JArray(Loadout("cat_from_future", "outfit_from_future",
                    "accessory_from_future", "frame_from_future")));

            var persistence = new SaveStoreCosmeticProfilePersistence(store);

            Assert.That(persistence.TryLoad(out var snapshot), Is.True);
            Assert.That(snapshot.SelectedCatId, Is.EqualTo("cat_from_future"));
            Assert.That(snapshot.EarnedCatIds, Is.EqualTo(new[] { "earned_cat_from_future" }));
            Assert.That(snapshot.EarnedItemIds, Is.EqualTo(new[] { "earned_item_from_future" }));
            AssertLoadout(snapshot.LoadoutFor("cat_from_future"), "cat_from_future",
                "outfit_from_future", "accessory_from_future", "frame_from_future");
        }

        [Test]
        public void TryReplace_CommitsCandidateThatSurvivesANewSaveStoreLoad()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            store.CommitAtomic();
            var candidate = Snapshot("yellow_longhair",
                new[] { "earned_cat" }, new[] { "earned_item" },
                new CosmeticLoadout("yellow_longhair", "outfit_conductor", "accessory_bell",
                    "frame_lantern"));

            var persistence = new SaveStoreCosmeticProfilePersistence(store);

            Assert.That(persistence.TryReplace(candidate), Is.True);
            var reloadedStore = SFixtures.Store(root);
            Assert.That(reloadedStore.Load(), Is.EqualTo(LoadResult.Ok));
            var reloadedPersistence = new SaveStoreCosmeticProfilePersistence(reloadedStore);
            Assert.That(reloadedPersistence.TryLoad(out var reloaded), Is.True);
            Assert.That(reloaded.SelectedCatId, Is.EqualTo("yellow_longhair"));
            Assert.That(reloaded.EarnedCatIds, Is.EqualTo(new[] { "earned_cat" }));
            Assert.That(reloaded.EarnedItemIds, Is.EqualTo(new[] { "earned_item" }));
            AssertLoadout(reloaded.LoadoutFor("yellow_longhair"), "yellow_longhair",
                "outfit_conductor", "accessory_bell", "frame_lantern");
        }

        [Test]
        public void TryReplace_PreservesUnknownSiblingsMatchedLoadoutFieldsAndOpaqueLeases()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            var payload = store.State.Payload;
            payload["futureRoot"] = new JObject { ["kept"] = new JArray(1, "two") };
            payload["profile"]["futureProfile"] = new JObject { ["kept"] = true };
            var cosmetics = (JObject)payload["profile"]["cosmetics"];
            cosmetics["futureCosmetics"] = new JObject { ["schema"] = 17 };
            cosmetics["loadouts"] = new JArray(
                new JObject
                {
                    ["catId"] = "red_tabby",
                    ["outfitId"] = "old_outfit",
                    ["accessoryId"] = "old_accessory",
                    ["frameId"] = "old_frame",
                    ["futureLoadout"] = new JObject { ["paint"] = "striped" },
                },
                Loadout("cat_removed_by_replace", "old", "old", "old"));
            var leases = new JObject
            {
                ["futureLeaseShape"] = new JArray(
                    new JObject { ["opaque"] = 99L },
                    JValue.CreateNull(),
                    "uninterpreted"),
            };
            payload["entitlements"]["localLeases"] = leases.DeepClone();
            store.CommitAtomic();

            var replacement = Snapshot("red_tabby", Array.Empty<string>(),
                Array.Empty<string>(),
                new CosmeticLoadout("red_tabby", "new_outfit", "new_accessory", "new_frame"),
                new CosmeticLoadout("new_cat", "new_cat_outfit", "", ""));
            var persistence = new SaveStoreCosmeticProfilePersistence(store);

            Assert.That(persistence.TryReplace(replacement), Is.True);

            var reloaded = SFixtures.Store(root);
            Assert.That(reloaded.Load(), Is.EqualTo(LoadResult.Ok));
            var saved = reloaded.State.Payload;
            Assert.That(JToken.DeepEquals(saved["futureRoot"], payload["futureRoot"]), Is.True);
            Assert.That(JToken.DeepEquals(saved["profile"]["futureProfile"],
                payload["profile"]["futureProfile"]), Is.True);
            Assert.That(JToken.DeepEquals(saved["profile"]["cosmetics"]["futureCosmetics"],
                cosmetics["futureCosmetics"]), Is.True);
            Assert.That(JToken.DeepEquals(saved["entitlements"]["localLeases"], leases), Is.True,
                "the lease subtree is opaque and must survive exactly");

            var savedLoadouts = (JArray)saved["profile"]["cosmetics"]["loadouts"];
            Assert.That(savedLoadouts.Count, Is.EqualTo(2));
            var matched = (JObject)savedLoadouts[0];
            Assert.That((string)matched["catId"], Is.EqualTo("red_tabby"));
            Assert.That((string)matched["outfitId"], Is.EqualTo("new_outfit"));
            Assert.That((string)matched["accessoryId"], Is.EqualTo("new_accessory"));
            Assert.That((string)matched["frameId"], Is.EqualTo("new_frame"));
            Assert.That(JToken.DeepEquals(matched["futureLoadout"],
                cosmetics["loadouts"][0]["futureLoadout"]), Is.True);
            Assert.That(savedLoadouts[1]["futureLoadout"], Is.Null,
                "a newly introduced cat gets only the four owned members");
        }

        [Test]
        public void TryReplace_CommitRefusalRestoresOriginalPayloadIdentityAndDiskBytes()
        {
            using var root = new SFixtures.TempRoot();
            var future = SaveDefaults.FreshPayload();
            File.WriteAllBytes(Path.Combine(root.SaveDirectory, "save.dat"),
                SFixtures.FileWithVersion((ushort)(SaveDefaults.SAVE_VERSION + 1), future));
            var diskBefore = File.ReadAllBytes(Path.Combine(root.SaveDirectory, "save.dat"));
            var store = SFixtures.Store(root);
            Assert.That(store.Load(), Is.EqualTo(LoadResult.RefusedDowngrade));
            var original = store.State.Payload;
            var leases = original["entitlements"]["localLeases"].DeepClone();
            var persistence = new SaveStoreCosmeticProfilePersistence(store);

            Assert.That(persistence.TryReplace(Snapshot("blue_siamese")), Is.False);

            Assert.That(ReferenceEquals(store.State.Payload, original), Is.True);
            Assert.That(JToken.DeepEquals(store.State.Payload["entitlements"]["localLeases"],
                leases), Is.True);
            CollectionAssert.AreEqual(diskBefore,
                File.ReadAllBytes(Path.Combine(root.SaveDirectory, "save.dat")));
        }

        [TestCase(SFixtures.Fault.InWriteTemp)]
        [TestCase(SFixtures.Fault.InReplace)]
        public void TryReplace_WriteOrReplaceExceptionRestoresOriginalIdentityAndReturnsFalse(
            SFixtures.Fault fault)
        {
            using var root = new SFixtures.TempRoot();
            var (store, fs) = SFixtures.CommittedStore(root);
            var original = store.State.Payload;
            var leases = new JObject
            {
                ["doNotInterpret"] = new JArray(7, "lease", new JObject { ["x"] = false }),
            };
            original["entitlements"]["localLeases"] = leases.DeepClone();
            store.CommitAtomic();
            var diskBefore = SFixtures.RawFile(store.SavePath);
            fs.FaultPoint = fault;
            var persistence = new SaveStoreCosmeticProfilePersistence(store);

            Assert.That(persistence.TryReplace(Snapshot("blue_siamese")), Is.False);

            Assert.That(ReferenceEquals(store.State.Payload, original), Is.True);
            Assert.That(JToken.DeepEquals(store.State.Payload["entitlements"]["localLeases"],
                leases), Is.True);
            CollectionAssert.AreEqual(diskBefore, SFixtures.RawFile(store.SavePath));
            Assert.That(store.ReportedEvents.Any(e => e.Name == "error_caught"
                && e.Detail.Contains("domain=cosmetics_save")), Is.True);
        }

        private static IEnumerable<TestCaseData> MalformedOwnedShapes()
        {
            yield return Malformed("profile scalar", payload => payload["profile"] = 7);
            yield return Malformed("cosmetics array", payload =>
                payload["profile"]["cosmetics"] = new JArray());
            yield return Malformed("selected cat type", payload =>
                payload["profile"]["cosmetics"]["selectedCatId"] = 3);
            yield return Malformed("missing selected cat", payload =>
                ((JObject)payload["profile"]["cosmetics"]).Remove("selectedCatId"));
            yield return Malformed("earned cats container", payload =>
                payload["profile"]["cosmetics"]["earnedCatIds"] = new JObject());
            yield return Malformed("earned cat member", payload =>
                payload["profile"]["cosmetics"]["earnedCatIds"] = new JArray("cat", 2));
            yield return Malformed("earned items container", payload =>
                payload["profile"]["cosmetics"]["earnedItemIds"] = "item");
            yield return Malformed("earned item member", payload =>
                payload["profile"]["cosmetics"]["earnedItemIds"] = new JArray(false));
            yield return Malformed("loadouts container", payload =>
                payload["profile"]["cosmetics"]["loadouts"] = new JObject());
            yield return Malformed("loadout row", payload =>
                payload["profile"]["cosmetics"]["loadouts"] = new JArray("row"));
            yield return Malformed("missing loadout cat", payload =>
                ((JObject)payload["profile"]["cosmetics"]["loadouts"][0]).Remove("catId"));
            yield return Malformed("loadout outfit type", payload =>
                payload["profile"]["cosmetics"]["loadouts"][0]["outfitId"] = 1);
            yield return Malformed("missing loadout accessory", payload =>
                ((JObject)payload["profile"]["cosmetics"]["loadouts"][0]).Remove("accessoryId"));
            yield return Malformed("loadout frame type", payload =>
                payload["profile"]["cosmetics"]["loadouts"][0]["frameId"] = new JArray());
        }

        [TestCaseSource(nameof(MalformedOwnedShapes))]
        public void MalformedCurrentV3OwnedShape_FailsLoadAndReplaceWithoutRewritingDisk(
            string _, Action<JObject> mutate)
        {
            using var root = new SFixtures.TempRoot();
            var malformed = SaveDefaults.FreshPayload();
            mutate(malformed);
            var path = Path.Combine(root.SaveDirectory, "save.dat");
            File.WriteAllBytes(path, SFixtures.FileWithVersion(SaveDefaults.SAVE_VERSION, malformed));
            var diskBefore = File.ReadAllBytes(path);
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs);
            Assert.That(store.Load(), Is.EqualTo(LoadResult.Ok));
            var original = store.State.Payload;
            int callsBefore = fs.Calls.Count;
            var persistence = new SaveStoreCosmeticProfilePersistence(store);

            Assert.That(persistence.TryLoad(out var loaded), Is.False);
            Assert.That(ReferenceEquals(loaded, CosmeticProfileSnapshot.Empty), Is.True);
            Assert.That(persistence.TryReplace(Snapshot("blue_siamese")), Is.False);

            Assert.That(ReferenceEquals(store.State.Payload, original), Is.True);
            Assert.That(fs.Calls.Count, Is.EqualTo(callsBefore),
                "malformed owned data must be refused before any filesystem write");
            CollectionAssert.AreEqual(diskBefore, File.ReadAllBytes(path));
        }

        [Test]
        public void ProfileDtos_DefensivelyCopyAndResolveDuplicatesDeterministically()
        {
            var earnedCats = new List<string> { "cat_a", "cat_a", "cat_b" };
            var earnedItems = new List<string> { "item_a", "item_a", "item_b" };
            var loadouts = new List<CosmeticLoadout>
            {
                new CosmeticLoadout("cat_a", "first", "", ""),
                new CosmeticLoadout("cat_b", "only", "", ""),
                new CosmeticLoadout("cat_a", "last", "", ""),
            };
            var snapshot = new CosmeticProfileSnapshot("cat_a", earnedCats, earnedItems, loadouts);

            earnedCats[0] = "mutated";
            earnedItems.Clear();
            loadouts.Clear();

            CollectionAssert.AreEqual(new[] { "cat_a", "cat_b" }, snapshot.EarnedCatIds,
                "earned IDs keep first occurrence order");
            CollectionAssert.AreEqual(new[] { "item_a", "item_b" }, snapshot.EarnedItemIds,
                "earned IDs keep first occurrence order");
            Assert.That(snapshot.Loadouts.Select(l => l.CatId),
                Is.EqualTo(new[] { "cat_a", "cat_b" }),
                "duplicate cat loadouts keep their first position");
            Assert.That(snapshot.LoadoutFor("cat_a").OutfitId, Is.EqualTo("last"),
                "the last submitted loadout wins deterministically");
            Assert.That(snapshot.EarnedCatIds, Is.Not.InstanceOf<List<string>>());
            Assert.That(snapshot.EarnedItemIds, Is.Not.InstanceOf<List<string>>());
            Assert.That(snapshot.Loadouts, Is.Not.InstanceOf<List<CosmeticLoadout>>());
        }

        [Test]
        public void ImmutableDtoOperations_NormalizeNullsAndFailClosedForInvalidArguments()
        {
            Assert.DoesNotThrow(() => _ = new CosmeticProfileSnapshot(null, null, null, null));
            var empty = new CosmeticProfileSnapshot(null, null, null, null);
            Assert.That(empty.SelectedCatId, Is.Empty);
            Assert.That(empty.EarnedCatIds, Is.Empty);
            Assert.That(empty.EarnedItemIds, Is.Empty);
            Assert.That(empty.Loadouts, Is.Empty);
            Assert.That(ReferenceEquals(empty.WithSelectedCat(null), empty), Is.True);
            Assert.That(ReferenceEquals(empty.WithEarnedCat(null), empty), Is.True);
            Assert.That(ReferenceEquals(empty.WithEarnedItem(null), empty), Is.True);
            Assert.That(ReferenceEquals(empty.WithLoadout(default), empty), Is.True);

            var loadout = new CosmeticLoadout(null, null, null, null);
            AssertLoadout(loadout, "", "", "", "");
            Assert.That(loadout.ItemFor((CosmeticSlot)99), Is.Empty);
            Assert.That(loadout.With((CosmeticSlot)99, "ignored").CatId, Is.EqualTo(loadout.CatId));
            Assert.That(loadout.With(CosmeticSlot.Outfit, null).OutfitId, Is.Empty);

            var portrait = new CosmeticPortraitSnapshot(null, null, null, null, null);
            Assert.That(portrait, Is.EqualTo(new CosmeticPortraitSnapshot("", "", "", "", "")));
            Assert.That(portrait.GetHashCode(),
                Is.EqualTo(new CosmeticPortraitSnapshot("", "", "", "", "").GetHashCode()));
        }

        [Test]
        public void InMemoryPersistence_HoldsOnlyImmutableSnapshotsAndRejectsNullReplacement()
        {
            var persistence = new InMemoryCosmeticProfilePersistence(null);
            Assert.That(persistence.TryLoad(out var initial), Is.True);
            Assert.That(ReferenceEquals(initial, CosmeticProfileSnapshot.Empty), Is.True);
            Assert.That(persistence.TryReplace(null), Is.False);
            Assert.That(persistence.TryReplace(Snapshot("blue_siamese")), Is.True);
            Assert.That(persistence.TryLoad(out var replaced), Is.True);
            Assert.That(replaced.SelectedCatId, Is.EqualTo("blue_siamese"));
        }

        [Test]
        public void SaveStorePersistence_NullStoreOrSnapshotFailsClosed()
        {
            var missingStore = new SaveStoreCosmeticProfilePersistence(null);
            Assert.That(missingStore.TryLoad(out var missing), Is.False);
            Assert.That(ReferenceEquals(missing, CosmeticProfileSnapshot.Empty), Is.True);
            Assert.That(missingStore.TryReplace(Snapshot("red_tabby")), Is.False);

            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            var original = store.State.Payload;
            var persistence = new SaveStoreCosmeticProfilePersistence(store);
            Assert.That(persistence.TryReplace(null), Is.False);
            Assert.That(ReferenceEquals(store.State.Payload, original), Is.True);
            Assert.That(File.Exists(store.SavePath), Is.False);
        }

        private static TestCaseData Malformed(string name, Action<JObject> mutate) =>
            new TestCaseData(name, mutate).SetName("MalformedCurrentV3OwnedShape_" +
                name.Replace(" ", "_"));

        private static JObject Cosmetics(string selectedCatId, JArray earnedCats,
            JArray earnedItems, JArray loadouts) => new JObject
        {
            ["selectedCatId"] = selectedCatId,
            ["earnedCatIds"] = earnedCats,
            ["earnedItemIds"] = earnedItems,
            ["loadouts"] = loadouts,
        };

        private static JObject Loadout(string catId, string outfitId, string accessoryId,
            string frameId) => new JObject
        {
            ["catId"] = catId,
            ["outfitId"] = outfitId,
            ["accessoryId"] = accessoryId,
            ["frameId"] = frameId,
        };

        private static CosmeticProfileSnapshot Snapshot(string selectedCatId,
            IReadOnlyList<string> earnedCats = null, IReadOnlyList<string> earnedItems = null,
            params CosmeticLoadout[] loadouts) => new CosmeticProfileSnapshot(selectedCatId,
                earnedCats ?? Array.Empty<string>(), earnedItems ?? Array.Empty<string>(),
                loadouts ?? Array.Empty<CosmeticLoadout>());

        private static void AssertLoadout(CosmeticLoadout actual, string catId, string outfitId,
            string accessoryId, string frameId)
        {
            Assert.That(actual.CatId, Is.EqualTo(catId));
            Assert.That(actual.OutfitId, Is.EqualTo(outfitId));
            Assert.That(actual.AccessoryId, Is.EqualTo(accessoryId));
            Assert.That(actual.FrameId, Is.EqualTo(frameId));
        }
    }
}
