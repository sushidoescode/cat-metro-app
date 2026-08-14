using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using CatMetro.Services.Purchases;

namespace CatMetro.Tests.Purchases
{
    // CM-MONETIZATION-CODE Task 1: these tests deliberately exercise only the SDK-free
    // vocabulary and resource data. Runtime adapters, persistence, and SDK work are out of scope.
    public sealed class PurchaseFoundationTests
    {
        [Test]
        public void ProductCatalog_HasExactlyTheSignedProductsAndEntitlementAttachments()
        {
            var products = ReadArray("product_catalog.json", "products");
            Assert.That(products.Select(p => (string)p["id"]), Is.EquivalentTo(new[]
            {
                "cm_all_access", "cm_supporter_pack", "cm_theme_sakura", "cm_theme_neon",
                "cm_rewind_5", "cm_rewind_20"
            }));
            Assert.That(products.Select(p => (string)p["id"]).Distinct().Count(), Is.EqualTo(6));

            AssertProduct(products, "cm_all_access", "non_consumable",
                "all_access", "theme_sakura", "theme_neon");
            AssertProduct(products, "cm_supporter_pack", "non_consumable",
                "supporter", "all_access", "theme_sakura", "theme_neon");
            AssertProduct(products, "cm_theme_sakura", "non_consumable", "theme_sakura");
            AssertProduct(products, "cm_theme_neon", "non_consumable", "theme_neon");
            AssertProduct(products, "cm_rewind_5", "consumable");
            AssertProduct(products, "cm_rewind_20", "consumable");
        }

        [Test]
        public void PlacementsAndOutcomeVocabulary_AreExactAndDisabled()
        {
            Assert.That(Enum.GetNames(typeof(PurchasePlacement)), Is.EquivalentTo(new[]
            {
                "PostLevel5", "ThemePreview", "BonusDistrict", "Shop", "RewindFailure"
            }));
            Assert.That(Enum.GetNames(typeof(PurchaseOutcome)), Does.Contain("SuccessCandidate"));
            Assert.That(Enum.GetNames(typeof(PurchaseOutcome)), Does.Contain("UserCancelled"));
            Assert.That(Enum.GetNames(typeof(PurchaseOutcome)), Does.Contain("Failure"));
            Assert.That(Enum.GetNames(typeof(PurchaseOutcome)), Does.Contain("Restored"));
            Assert.That(Enum.GetNames(typeof(PurchaseOutcome)), Does.Contain("Pending"));
            Assert.That(Enum.GetNames(typeof(PurchaseOutcome)), Does.Contain("UnknownUnsettled"));
            Assert.That(Enum.GetNames(typeof(PurchaseOutcome)), Does.Not.Contain("Granted"));
        }

        [Test]
        public void LocalizedPrice_IsDisplayTextOnlyAndDtosExposeNoOperationalMethods()
        {
            var fields = typeof(LocalizedPrice).GetFields();
            Assert.That(fields.Select(f => f.FieldType), Is.EquivalentTo(new[] { typeof(string) }));
            Assert.That(fields.Select(f => f.Name), Is.EquivalentTo(new[] { "DisplayText" }));

            var dtoTypes = new[]
            {
                typeof(PurchaseCatalogEntry), typeof(LocalizedPrice), typeof(PurchaseResult),
                typeof(RewardedPlacement), typeof(RewardCap)
            };
            foreach (var type in dtoTypes)
            {
                Assert.That(type.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                    BindingFlags.Static | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName), Is.Empty,
                    type.FullName + " is passive vocabulary only");
            }
        }

        [Test]
        public void RewardedCatalog_HasExactDisabledRowsAndSignedCaps()
        {
            var rewards = ReadArray("rewarded_placements.json", "placements");
            Assert.That(rewards.Select(r => (string)r["id"]), Is.EquivalentTo(new[]
            {
                "rewind_failure", "double_tickets", "daily_gift_double", "streak_saver",
                "theme_rental", "cat_skin_trial", "livery_trial", "district_guest_route"
            }));
            Assert.That(rewards.Select(r => (string)r["id"]).Distinct().Count(), Is.EqualTo(8));
            foreach (var reward in rewards)
            {
                Assert.That((bool)reward["sdkCallEnabled"], Is.False);
                Assert.That((string)reward["disabledReason"], Does.Contain("NEW-Q45"));
                Assert.That((string)reward["disabledReason"], Does.Contain("device gate"));
            }
            foreach (var id in new[] { "cat_skin_trial", "livery_trial", "district_guest_route" })
                Assert.That((string)rewards.Single(r => (string)r["id"] == id)["disabledReason"],
                    Does.Contain("ADR-0006 supersession"));
            foreach (var id in new[] { "rewind_failure", "double_tickets", "daily_gift_double", "streak_saver", "theme_rental" })
                Assert.That((string)rewards.Single(r => (string)r["id"] == id)["disabledReason"],
                    Does.Not.Contain("ADR-0006"));

            AssertReward(rewards, "rewind_failure", "one_rewind", "session", 2, "localDate", 5);
            AssertReward(rewards, "double_tickets", "ticket_double", "localDate", 3);
            AssertReward(rewards, "daily_gift_double", "gift_double", "localDate", 1);
            AssertReward(rewards, "streak_saver", "streak_repair", "localDate", 1);
            AssertReward(rewards, "theme_rental", "selected_theme_3_eligible_completed_levels", "perThemeLocalDate", 1);
            AssertReward(rewards, "cat_skin_trial", "selected_skin_3_eligible_completed_levels", "totalSkinLeaseLocalDate", 1);
            AssertReward(rewards, "livery_trial", "selected_livery_3_eligible_completed_levels", "totalLiveryLeaseLocalDate", 1);
            AssertReward(rewards, "district_guest_route", "signed_practice_route", "perDistrictLocalDate", 1, "session", 1);
        }

        private static JArray ReadArray(string fileName, string propertyName)
        {
            var root = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (root != null && !Directory.Exists(Path.Combine(root.FullName, "unity"))) root = root.Parent;
            Assert.That(root, Is.Not.Null, "repository root was not found");
            var path = Path.Combine(root.FullName, "unity", "Assets", "Resources", "Monetization", fileName);
            return (JArray)JObject.Parse(File.ReadAllText(path))[propertyName];
        }

        private static void AssertProduct(JArray products, string id, string storeType, params string[] entitlements)
        {
            var product = products.Single(p => (string)p["id"] == id);
            Assert.That((string)product["storeType"], Is.EqualTo(storeType));
            Assert.That(product["entitlements"].Values<string>(), Is.EquivalentTo(entitlements));
        }

        private static void AssertReward(JArray rewards, string id, string reward, params object[] caps)
        {
            var row = rewards.Single(r => (string)r["id"] == id);
            Assert.That((string)row["reward"], Is.EqualTo(reward));
            var actual = ((JObject)row["caps"]).Properties()
                .ToDictionary(p => p.Name, p => (int)p.Value);
            var expected = Enumerable.Range(0, caps.Length / 2)
                .ToDictionary(i => (string)caps[i * 2], i => (int)caps[i * 2 + 1]);
            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}
