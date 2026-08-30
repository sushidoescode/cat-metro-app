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

            Assert.That(catalog.TryGetCat("red_tabby", out var cat), Is.True);
            Assert.That(cat.Starter, Is.True);
            Assert.That(cat.PortraitAssetId, Is.EqualTo("cat.red_tabby"));
            Assert.That(catalog.TryGetItem("outfit_conductor", out var conductor), Is.True);
            Assert.That(conductor.Slot, Is.EqualTo(CosmeticSlot.Outfit));
            Assert.That(conductor.Acquisition, Is.EqualTo(CosmeticAcquisition.Entitlement));
            Assert.That(conductor.EntitlementId, Is.EqualTo("outfit_conductor"));
            Assert.That(conductor.ProductId, Is.EqualTo("cm_outfit_conductor"));
            Assert.That(conductor.CompatibleCatIds,
                Is.EqualTo(new[] { "red_tabby", "blue_siamese", "yellow_longhair" }));
            Assert.That(catalog.TryGetCat(null, out _), Is.False);
            Assert.That(catalog.TryGetItem(null, out _), Is.False);
        }

        [Test]
        public void RealResources_ContainNoPriceCurrencyRandomGachaRarityOrStats()
        {
            foreach (var path in new[] { CatalogPath(), InventoryPath() })
            {
                var root = JObject.Parse(File.ReadAllText(path));
                var properties = root.DescendantsAndSelf().OfType<JProperty>().ToArray();

                foreach (var forbiddenName in new[] { "price", "currency", "rarity", "stat" })
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
        public void MalformedAndDuplicateCatRows_ChangeOnlyCatAdmissionCounts()
        {
            var inventory = ShippedInventory();
            var root = JObject.Parse(File.ReadAllText(CatalogPath()));
            var cats = (JArray)root["cats"];
            ((JObject)cats[1]).Remove("displayNameKey");
            cats.Add(((JObject)cats[0]).DeepClone());

            var catalog = CosmeticCatalog.Parse(root.ToString(), inventory.AssetIds,
                inventory.ProvenanceAssetIds);

            Assert.That(catalog.AdmittedCatCount, Is.EqualTo(2));
            Assert.That(catalog.AdmittedRowCount, Is.EqualTo(3));
            Assert.That(catalog.RejectedRowCount, Is.Zero,
                "RejectedRowCount is defined only over submitted item rows");
            Assert.That(catalog.Problems.Count, Is.GreaterThanOrEqualTo(2));
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

            Assert.That(inventory.AssetIds, Is.EqualTo(new[] { "cat.test" }));
            Assert.That(inventory.ProvenanceAssetIds, Is.EqualTo(new[] { "cat.test" }));
            Assert.That(inventory.Problems, Is.Empty);
            Assert.That(inventory.TryGet("cat.test", out var asset), Is.True);
            Assert.That(asset.RendererToken, Is.EqualTo("cat.test"));
            Assert.That(asset.ProvenanceId, Is.EqualTo("prov.test"));
            Assert.That(inventory.TryGet(null, out _), Is.False);
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
