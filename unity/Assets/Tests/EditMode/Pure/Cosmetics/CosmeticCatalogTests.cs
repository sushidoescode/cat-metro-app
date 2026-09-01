using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatMetro.Services.Cosmetics;
using CatMetro.Services.Purchases;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CatMetro.Tests.Cosmetics
{
    public sealed class CosmeticCatalogTests
    {
        private static readonly string[] SupportedRendererTokens =
        {
            "cat.red_tabby",
            "cat.blue_siamese",
            "cat.yellow_longhair",
            "outfit.conductor",
            "frame.brass",
            "frame.lantern",
        };

        [Test]
        public void RealResources_AdmitExactlyTheStagedCatsAndNamedItems()
        {
            var inventory = ShippedInventory();
            var catalog = ShippedCatalog(inventory);

            Assert.That(inventory.Problems, Is.Empty);
            Assert.That(inventory.AssetIds.Count, Is.EqualTo(6));
            Assert.That(inventory.ProvenanceAssetIds.Count, Is.EqualTo(6));
            Assert.That(catalog.Problems, Is.Empty);
            Assert.That(catalog.AdmittedCatCount, Is.EqualTo(3));
            Assert.That(catalog.AdmittedRowCount, Is.EqualTo(3));
            Assert.That(catalog.RejectedRowCount, Is.Zero);
            CollectionAssert.AreEqual(
                new[] { "red_tabby", "blue_siamese", "yellow_longhair" },
                catalog.Cats.Select(cat => cat.Id));
            CollectionAssert.AreEqual(
                new[] { "outfit_conductor", "frame_brass", "frame_lantern" },
                catalog.Items.Select(item => item.Id));
            Assert.That(catalog.Cats.All(cat => cat.Starter), Is.True,
                "all three launch cats must be directly available starters");

            Assert.That(catalog.TryGetCat("red_tabby", out var cat), Is.True);
            Assert.That(cat.Starter, Is.True);
            Assert.That(cat.PortraitAssetId, Is.EqualTo("cat.red_tabby"));
            Assert.That(catalog.TryGetItem("outfit_conductor", out var conductor), Is.True);
            Assert.That(conductor.Slot, Is.EqualTo(CosmeticSlot.Outfit));
            Assert.That(conductor.Acquisition, Is.EqualTo(CosmeticAcquisition.Entitlement));
            Assert.That(conductor.EntitlementId, Is.EqualTo("outfit_conductor"));
            Assert.That(conductor.ProductId, Is.EqualTo("cm_outfit_conductor"));
            Assert.That(conductor.RewardedPlacementId, Is.EqualTo("wardrobe_try_conductor"),
                "the shipped Wardrobe row may offer only the exact conductor placement");
            Assert.That(conductor.CompatibleCatIds,
                Is.EqualTo(new[] { "red_tabby", "blue_siamese", "yellow_longhair" }));
            Assert.That(catalog.TryGetCat(null, out _), Is.False);
            Assert.That(catalog.TryGetItem(null, out _), Is.False);
        }

        [TestCase("red_tabby", "cosmetics.cat.red_tabby", "cat.red_tabby")]
        [TestCase("blue_siamese", "cosmetics.cat.blue_siamese", "cat.blue_siamese")]
        [TestCase("yellow_longhair", "cosmetics.cat.yellow_longhair", "cat.yellow_longhair")]
        public void RealCatRow_HasExactLaunchMapping(string id, string displayNameKey,
            string portraitAssetId)
        {
            var catalog = ShippedCatalog(ShippedInventory());

            Assert.That(catalog.TryGetCat(id, out var cat), Is.True);
            Assert.That(cat.DisplayNameKey, Is.EqualTo(displayNameKey));
            Assert.That(cat.PortraitAssetId, Is.EqualTo(portraitAssetId));
            Assert.That(cat.Starter, Is.True);
        }

        [TestCase("outfit_conductor", CosmeticSlot.Outfit, "cosmetics.item.outfit_conductor",
            "outfit.conductor", "outfit_conductor", "cm_outfit_conductor")]
        [TestCase("frame_brass", CosmeticSlot.Frame, "cosmetics.item.frame_brass",
            "frame.brass", "frame_brass", "cm_frame_brass")]
        [TestCase("frame_lantern", CosmeticSlot.Frame, "cosmetics.item.frame_lantern",
            "frame.lantern", "frame_lantern", "cm_frame_lantern")]
        public void RealItemRow_HasExactLaunchMapping(string id, CosmeticSlot slot,
            string displayNameKey, string portraitAssetId, string entitlementId, string productId)
        {
            var catalog = ShippedCatalog(ShippedInventory());

            Assert.That(catalog.TryGetItem(id, out var item), Is.True);
            Assert.That(item.Slot, Is.EqualTo(slot));
            Assert.That(item.DisplayNameKey, Is.EqualTo(displayNameKey));
            Assert.That(item.PortraitAssetId, Is.EqualTo(portraitAssetId));
            Assert.That(item.Acquisition, Is.EqualTo(CosmeticAcquisition.Entitlement));
            Assert.That(item.EntitlementId, Is.EqualTo(entitlementId));
            Assert.That(item.ProductId, Is.EqualTo(productId));
            CollectionAssert.AreEqual(
                new[] { "red_tabby", "blue_siamese", "yellow_longhair" },
                item.CompatibleCatIds);
        }

        [Test]
        public void RealResources_ContainNoPriceCurrencyRandomGachaRarityOrStats()
        {
            foreach (var path in new[] { CatalogPath(), InventoryPath() })
            {
                var root = JObject.Parse(File.ReadAllText(path));
                var properties = root.DescendantsAndSelf().OfType<JProperty>().ToArray();

                foreach (var forbiddenName in new[] { "price", "currency", "rarity", "stat", "power" })
                    Assert.That(properties.Any(p => p.Name.IndexOf(
                        forbiddenName, StringComparison.OrdinalIgnoreCase) >= 0), Is.False,
                        Path.GetFileName(path) + " declares forbidden field fragment " + forbiddenName);

                var text = root.ToString();
                Assert.That(text.IndexOf("random", StringComparison.OrdinalIgnoreCase), Is.EqualTo(-1));
                Assert.That(text.IndexOf("gacha", StringComparison.OrdinalIgnoreCase), Is.EqualTo(-1));
            }
        }

        [Test]
        public void EveryRealEntitlementItem_IsProvenAgainstTheShippedPurchaseCatalog()
        {
            var inventory = ShippedInventory();
            var cosmetics = ShippedCatalog(inventory);
            var purchases = PurchaseCatalog.Parse(File.ReadAllText(Path.Combine(
                RepoRoot(), "unity", "Assets", "Resources", "Monetization", "product_catalog.json")));

            foreach (var item in cosmetics.Items.Where(i => i.Acquisition == CosmeticAcquisition.Entitlement))
            {
                Assert.That(purchases.TryGetEntitlement(item.EntitlementId, out _), Is.True,
                    item.Id + " declares an entitlement absent from PurchaseCatalog");
                Assert.That(purchases.TryGetProduct(item.ProductId, out var product), Is.True,
                    item.Id + " declares a product absent from PurchaseCatalog");
                Assert.That(product.Entitlements, Does.Contain(item.EntitlementId),
                    item.ProductId + " does not grant " + item.EntitlementId);
            }
        }

        private static IEnumerable<TestCaseData> RejectedItemMutations()
        {
            yield return ItemMutation("duplicate id", (root, item) =>
                item["id"] = (string)((JArray)root["items"])[0]["id"]);
            yield return ItemMutation("unknown slot", (_, item) => item["slot"] = "cape");
            yield return ItemMutation("unknown acquisition", (_, item) => item["acquisition"] = "rental");
            yield return ItemMutation("random acquisition", (_, item) => item["acquisition"] = "random");
            yield return ItemMutation("missing entitlement", (_, item) => item.Remove("entitlementId"));
            yield return ItemMutation("missing product", (_, item) => item.Remove("productId"));
            yield return ItemMutation("missing earned instruction", (_, item) =>
            {
                item["acquisition"] = "earned";
                item.Remove("entitlementId");
                item.Remove("productId");
                item.Remove("earnInstructionKey");
            });
            yield return ItemMutation("unknown compatible cat", (_, item) =>
                ((JArray)item["compatibleCatIds"])[0] = "cat_not_submitted");
            yield return ItemMutation("missing asset", (_, item) => item["portraitAssetId"] = "asset.missing");
            yield return ItemMutation("malformed order", (_, item) => item["order"] = new JObject());
        }

        [TestCaseSource(nameof(RejectedItemMutations))]
        public void InvalidSubmittedItemRow_IsRejectedOnce_WithoutReservingAGap(
            string _, Action<JObject, JObject> mutate)
        {
            var inventory = ShippedInventory();
            var root = JObject.Parse(File.ReadAllText(CatalogPath()));
            mutate(root, (JObject)((JArray)root["items"])[1]);

            var catalog = CosmeticCatalog.Parse(root.ToString(), inventory.AssetIds,
                inventory.ProvenanceAssetIds);

            Assert.That(catalog.AdmittedRowCount, Is.EqualTo(2));
            Assert.That(catalog.RejectedRowCount, Is.EqualTo(1));
            Assert.That(catalog.Items.Count, Is.EqualTo(2), "a rejected row must create no visible gap");
            Assert.That(catalog.Problems, Is.Not.Empty);
        }

        [Test]
        public void ItemWhoseAssetLacksProvenance_IsRejectedOnce()
        {
            var inventory = ShippedInventory();
            var provenance = inventory.ProvenanceAssetIds
                .Where(id => id != "frame.brass").ToArray();

            var catalog = CosmeticCatalog.Parse(File.ReadAllText(CatalogPath()),
                inventory.AssetIds, provenance);

            Assert.That(catalog.AdmittedRowCount, Is.EqualTo(2));
            Assert.That(catalog.RejectedRowCount, Is.EqualTo(1));
            Assert.That(catalog.TryGetItem("frame_brass", out _), Is.False);
            Assert.That(catalog.Problems.Any(p => p.Contains("provenance")), Is.True);
        }

        [Test]
        public void ItemWhoseRendererTokenIsUnsupported_IsRejectedOnceThroughInventoryAdmission()
        {
            var inventoryRoot = JObject.Parse(File.ReadAllText(InventoryPath()));
            var brass = ((JArray)inventoryRoot["assets"]).OfType<JObject>()
                .Single(a => (string)a["assetId"] == "frame.brass");
            brass["rendererToken"] = "frame.not_supported";
            var inventory = CosmeticAssetInventory.Parse(inventoryRoot.ToString(), SupportedRendererTokens);

            var catalog = CosmeticCatalog.Parse(File.ReadAllText(CatalogPath()),
                inventory.AssetIds, inventory.ProvenanceAssetIds);

            Assert.That(inventory.AssetIds, Does.Not.Contain("frame.brass"));
            Assert.That(inventory.ProvenanceAssetIds, Does.Not.Contain("frame.brass"));
            Assert.That(inventory.Problems.Any(p => p.Contains("renderer")), Is.True);
            Assert.That(catalog.AdmittedRowCount, Is.EqualTo(2));
            Assert.That(catalog.RejectedRowCount, Is.EqualTo(1));
        }

        [Test]
        public void RejectedCatRow_AloneDoesNotRejectItems_ButAReferenceToItRejectsOneItem()
        {
            var inventory = ShippedInventory();
            var root = JObject.Parse(File.ReadAllText(CatalogPath()));
            var cats = (JArray)root["cats"];
            cats.Add(new JObject
            {
                ["id"] = "rejected_cat",
                ["portraitAssetId"] = "cat.red_tabby",
                ["starter"] = true,
            });
            cats.Add(((JObject)cats[0]).DeepClone());

            var catFailuresOnly = CosmeticCatalog.Parse(root.ToString(), inventory.AssetIds,
                inventory.ProvenanceAssetIds);

            Assert.That(catFailuresOnly.AdmittedCatCount, Is.EqualTo(3));
            Assert.That(catFailuresOnly.AdmittedRowCount, Is.EqualTo(3));
            Assert.That(catFailuresOnly.RejectedRowCount, Is.Zero,
                "RejectedRowCount is defined only over submitted item rows");
            Assert.That(catFailuresOnly.Problems.Count, Is.GreaterThanOrEqualTo(2));

            var brass = (JObject)((JArray)root["items"])[1];
            ((JArray)brass["compatibleCatIds"]).Add("rejected_cat");
            var referencedRejectedCat = CosmeticCatalog.Parse(root.ToString(), inventory.AssetIds,
                inventory.ProvenanceAssetIds);

            Assert.That(referencedRejectedCat.AdmittedCatCount, Is.EqualTo(3));
            Assert.That(referencedRejectedCat.AdmittedRowCount, Is.EqualTo(2));
            Assert.That(referencedRejectedCat.RejectedRowCount, Is.EqualTo(1));
            Assert.That(referencedRejectedCat.Items.Select(item => item.Id),
                Is.EqualTo(new[] { "outfit_conductor", "frame_lantern" }),
                "the rejected frame must reserve no gap");
            Assert.That(referencedRejectedCat.Problems.Any(p => p.Contains("rejected_cat")), Is.True);
        }

        [Test]
        public void EqualItemOrder_PreservesAdmittedSubmissionOrder()
        {
            var inventory = ShippedInventory();
            var root = JObject.Parse(File.ReadAllText(CatalogPath()));
            var template = (JObject)((JArray)root["items"])[0];
            var submittedIds = new List<string>();
            var tiedItems = new JArray();
            for (int i = 0; i < 20; i++)
            {
                var item = (JObject)template.DeepClone();
                var id = "tied_item_" + i.ToString("D2");
                item["id"] = id;
                item["order"] = 10;
                submittedIds.Add(id);
                tiedItems.Add(item);
            }
            root["items"] = tiedItems;

            var catalog = CosmeticCatalog.Parse(root.ToString(), inventory.AssetIds,
                inventory.ProvenanceAssetIds);

            Assert.That(catalog.RejectedRowCount, Is.Zero);
            CollectionAssert.AreEqual(submittedIds, catalog.Items.Select(item => item.Id));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not json")]
        [TestCase("[]")]
        public void InvalidCatalogueSource_IsTotalAndReturnsAnEmptyCatalogue(string json)
        {
            var catalog = CosmeticCatalog.Parse(json, Array.Empty<string>(), Array.Empty<string>());

            Assert.That(catalog.AdmittedCatCount, Is.Zero);
            Assert.That(catalog.AdmittedRowCount, Is.Zero);
            Assert.That(catalog.RejectedRowCount, Is.Zero);
            Assert.That(catalog.Cats, Is.Empty);
            Assert.That(catalog.Items, Is.Empty);
            Assert.That(catalog.Problems, Is.Not.Empty);
        }

        [TestCase("missing")]
        [TestCase("future")]
        public void MissingOrUnsupportedSchema_IsEmpty_NotPartiallyInterpreted(string mutation)
        {
            var inventory = ShippedInventory();
            var root = JObject.Parse(File.ReadAllText(CatalogPath()));
            if (mutation == "missing") root.Remove("schemaVersion");
            else root["schemaVersion"] = 99;

            var catalog = CosmeticCatalog.Parse(root.ToString(), inventory.AssetIds,
                inventory.ProvenanceAssetIds);

            Assert.That(catalog.AdmittedCatCount, Is.Zero);
            Assert.That(catalog.AdmittedRowCount, Is.Zero);
            Assert.That(catalog.RejectedRowCount, Is.Zero);
            Assert.That(catalog.Problems.Any(p => p.Contains("schemaVersion")), Is.True);
        }

        [Test]
        public void ProjectAuthoredAsset_RequiresSourcePathAndClearedCommercialDistribution()
        {
            var missingPath = ProjectAuthoredInventory();
            ((JObject)((JArray)missingPath["provenance"])[0]).Remove("sourcePath");
            var uncleared = ProjectAuthoredInventory();
            ((JObject)((JArray)uncleared["provenance"])[0])["commercialDistribution"] = "unknown";

            foreach (var root in new[] { missingPath, uncleared })
            {
                var inventory = CosmeticAssetInventory.Parse(root.ToString(), new[] { "cat.test" });
                Assert.That(inventory.AssetIds, Is.Empty);
                Assert.That(inventory.ProvenanceAssetIds, Is.Empty);
                Assert.That(inventory.Problems, Is.Not.Empty);
            }
        }

        [TestCase("valid_first")]
        [TestCase("invalid_first")]
        [TestCase("conflicting_valid")]
        public void DuplicateProvenanceId_IsNeverAdmitted_IndependentOfOrder(string permutation)
        {
            var root = ProjectAuthoredInventory();
            var valid = (JObject)((JArray)root["provenance"])[0];
            var invalid = (JObject)valid.DeepClone();
            invalid.Remove("sourcePath");
            var conflicting = (JObject)GeneratedPaidInventory()["provenance"][0];
            var rows = (JArray)root["provenance"];

            switch (permutation)
            {
                case "valid_first": rows.Add(invalid); break;
                case "invalid_first": root["provenance"] = new JArray(invalid, valid); break;
                case "conflicting_valid": rows.Add(conflicting); break;
            }

            var inventory = CosmeticAssetInventory.Parse(root.ToString(), new[] { "cat.test" });

            AssertRejectedTestAsset(inventory, "duplicate provenance id");
        }

        [TestCase("valid_first")]
        [TestCase("invalid_first")]
        [TestCase("conflicting_valid")]
        public void DuplicateAssetId_IsNeverAdmitted_IndependentOfOrder(string permutation)
        {
            var root = ProjectAuthoredInventory();
            var valid = (JObject)((JArray)root["assets"])[0];
            var invalid = (JObject)valid.DeepClone();
            invalid.Remove("rendererToken");
            var conflicting = (JObject)valid.DeepClone();
            conflicting["rendererToken"] = "cat.other";
            var rows = (JArray)root["assets"];

            switch (permutation)
            {
                case "valid_first": rows.Add(invalid); break;
                case "invalid_first": root["assets"] = new JArray(invalid, valid); break;
                case "conflicting_valid": rows.Add(conflicting); break;
            }

            var inventory = CosmeticAssetInventory.Parse(root.ToString(),
                new[] { "cat.test", "cat.other" });

            AssertRejectedTestAsset(inventory, "duplicate asset id");
        }

        private static IEnumerable<TestCaseData> WrongTypeProvenanceFields()
        {
            foreach (var field in new[] { "id", "sourceKind", "sourcePath", "commercialDistribution" })
                yield return new TestCaseData("project_authored", field).SetName(
                    "ProjectAuthored_" + field + "_MustBeAString");

            foreach (var field in new[]
            {
                "id", "sourceKind", "provider", "paidTier", "taskId", "prompt",
                "generationTimestamp", "sourceHash", "custodyLocation", "termsEvidence",
                "commercialDistribution",
            })
                yield return new TestCaseData("generated_paid", field).SetName(
                    "GeneratedPaid_" + field + "_MustBeAString");

            yield return new TestCaseData("generated_paid", "derivativeHashes").SetName(
                "GeneratedPaid_derivativeHashes_ElementsMustBeStrings");
            yield return new TestCaseData("generated_paid", "transformationChain").SetName(
                "GeneratedPaid_transformationChain_ElementsMustBeStrings");
        }

        [TestCaseSource(nameof(WrongTypeProvenanceFields))]
        public void ProvenanceEvidence_WrongJsonType_IsRejectedAndNamesTheField(
            string sourceKind, string field)
        {
            var root = sourceKind == "project_authored"
                ? ProjectAuthoredInventory()
                : GeneratedPaidInventory();
            var provenance = (JObject)((JArray)root["provenance"])[0];
            provenance[field] = field == "derivativeHashes" || field == "transformationChain"
                ? new JArray("valid-first-element", true)
                : new JValue(7);

            var inventory = CosmeticAssetInventory.Parse(root.ToString(), new[] { "cat.test" });

            Assert.That(inventory.AssetIds, Is.Empty);
            Assert.That(inventory.ProvenanceAssetIds, Is.Empty);
            Assert.That(inventory.TryGet("cat.test", out _), Is.False);
            Assert.That(inventory.Problems.Any(p => p.Contains(field)), Is.True,
                "the type failure must identify " + field);
        }

        private static IEnumerable<TestCaseData> GeneratedPaidRequiredFields()
        {
            foreach (var field in new[]
            {
                "provider", "paidTier", "taskId", "prompt", "generationTimestamp", "sourceHash",
                "derivativeHashes", "transformationChain", "custodyLocation", "termsEvidence",
                "commercialDistribution",
            })
                yield return new TestCaseData(field).SetName(
                    "GeneratedPaidAsset_Missing_" + field + "_IsRejected");
        }

        [TestCaseSource(nameof(GeneratedPaidRequiredFields))]
        public void GeneratedPaidAsset_MissingAnyFullProvenanceField_IsRejected(string field)
        {
            var root = GeneratedPaidInventory();
            ((JObject)((JArray)root["provenance"])[0]).Remove(field);

            var inventory = CosmeticAssetInventory.Parse(root.ToString(), new[] { "cat.test" });

            Assert.That(inventory.AssetIds, Is.Empty);
            Assert.That(inventory.ProvenanceAssetIds, Is.Empty);
            Assert.That(inventory.Problems.Any(p => p.Contains(field)), Is.True);
        }

        [Test]
        public void GeneratedPaidAsset_WithCompleteClearedProvenance_IsAdmitted()
        {
            var inventory = CosmeticAssetInventory.Parse(GeneratedPaidInventory().ToString(),
                new[] { "cat.test" });

            Assert.That(inventory.Problems, Is.Empty, string.Join(" | ", inventory.Problems));
            Assert.That(inventory.AssetIds, Is.EqualTo(new[] { "cat.test" }));
            Assert.That(inventory.ProvenanceAssetIds, Is.EqualTo(new[] { "cat.test" }));
            Assert.That(inventory.TryGet("cat.test", out var asset), Is.True);
            Assert.That(asset.RendererToken, Is.EqualTo("cat.test"));
            Assert.That(asset.ProvenanceId, Is.EqualTo("prov.test"));
            Assert.That(inventory.TryGet(null, out _), Is.False);
        }

        [Test]
        public void InventorySource_WithTrailingGarbage_IsRejectedAsAWhole()
        {
            var json = ProjectAuthoredInventory().ToString() + " trailing-garbage";

            var inventory = CosmeticAssetInventory.Parse(json, new[] { "cat.test" });

            Assert.That(inventory.AssetIds, Is.Empty);
            Assert.That(inventory.ProvenanceAssetIds, Is.Empty);
            Assert.That(inventory.TryGet("cat.test", out _), Is.False);
            Assert.That(inventory.Problems, Is.Not.Empty);
        }

        [Test]
        public void InventorySource_WithSecondTopLevelJsonValue_IsRejectedAsAWhole()
        {
            var json = ProjectAuthoredInventory().ToString() + "\n{ \"second\": true }";

            var inventory = CosmeticAssetInventory.Parse(json, new[] { "cat.test" });

            Assert.That(inventory.AssetIds, Is.Empty);
            Assert.That(inventory.ProvenanceAssetIds, Is.Empty);
            Assert.That(inventory.TryGet("cat.test", out _), Is.False);
            Assert.That(inventory.Problems, Is.Not.Empty);
        }

        [Test]
        public void InventorySource_WithOnlyTrailingWhitespace_RemainsValid()
        {
            var json = ProjectAuthoredInventory().ToString() + " \r\n\t  ";

            var inventory = CosmeticAssetInventory.Parse(json, new[] { "cat.test" });

            Assert.That(inventory.Problems, Is.Empty);
            Assert.That(inventory.AssetIds, Is.EqualTo(new[] { "cat.test" }));
            Assert.That(inventory.ProvenanceAssetIds, Is.EqualTo(new[] { "cat.test" }));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not json")]
        [TestCase("[]")]
        public void InvalidInventorySource_IsTotalAndReturnsEmpty(string json)
        {
            var inventory = CosmeticAssetInventory.Parse(json, Array.Empty<string>());

            Assert.That(inventory.AssetIds, Is.Empty);
            Assert.That(inventory.ProvenanceAssetIds, Is.Empty);
            Assert.That(inventory.Problems, Is.Not.Empty);
        }

        private static TestCaseData ItemMutation(string name, Action<JObject, JObject> mutate)
            => new TestCaseData(name, mutate).SetName("ItemMutation_" + name.Replace(' ', '_'));

        private static void AssertRejectedTestAsset(CosmeticAssetInventory inventory,
            string expectedProblem)
        {
            Assert.That(inventory.AssetIds, Does.Not.Contain("cat.test"));
            Assert.That(inventory.ProvenanceAssetIds, Does.Not.Contain("cat.test"));
            Assert.That(inventory.TryGet("cat.test", out _), Is.False);
            Assert.That(inventory.Problems.Any(p => p.Contains(expectedProblem)), Is.True);
        }

        private static CosmeticAssetInventory ShippedInventory()
            => CosmeticAssetInventory.Parse(File.ReadAllText(InventoryPath()), SupportedRendererTokens);

        private static CosmeticCatalog ShippedCatalog(CosmeticAssetInventory inventory)
            => CosmeticCatalog.Parse(File.ReadAllText(CatalogPath()), inventory.AssetIds,
                inventory.ProvenanceAssetIds);

        private static string CatalogPath() => Path.Combine(RepoRoot(), "unity", "Assets", "Resources",
            "Cosmetics", "cosmetic_catalog.json");

        private static string InventoryPath() => Path.Combine(RepoRoot(), "unity", "Assets", "Resources",
            "Cosmetics", "portrait_assets.json");

        private static string RepoRoot() => CatMetro.Tests.Domain.Fixtures.RepoRoot();

        private static JObject ProjectAuthoredInventory() => JObject.Parse(@"{
          ""schemaVersion"": 1,
          ""assets"": [
            { ""assetId"": ""cat.test"", ""rendererToken"": ""cat.test"", ""provenanceId"": ""prov.test"" }
          ],
          ""provenance"": [
            { ""id"": ""prov.test"", ""sourceKind"": ""project_authored"",
              ""sourcePath"": ""unity/Assets/Scripts/Presentation/Cosmetics/CosmeticPortraitPainter.cs"",
              ""commercialDistribution"": ""cleared"" }
          ]
        }");

        private static JObject GeneratedPaidInventory() => JObject.Parse(@"{
          ""schemaVersion"": 1,
          ""assets"": [
            { ""assetId"": ""cat.test"", ""rendererToken"": ""cat.test"", ""provenanceId"": ""prov.test"" }
          ],
          ""provenance"": [
            { ""id"": ""prov.test"", ""sourceKind"": ""generated_paid"",
              ""provider"": ""Provider"", ""paidTier"": ""Paid"", ""taskId"": ""task-1"",
              ""prompt"": ""A named cat"", ""generationTimestamp"": ""2026-08-29T00:00:00Z"",
              ""sourceHash"": ""sha256:source"", ""derivativeHashes"": [""sha256:derivative""],
              ""transformationChain"": [""source -> portrait""],
              ""custodyLocation"": ""unity/Assets/Art/Generated/incoming/curation-backups"",
              ""termsEvidence"": ""receipt-and-terms-snapshot"",
              ""commercialDistribution"": ""cleared"" }
          ]
        }");
    }
}
