using System.Linq;
using CatMetro.Services.Purchases;
using NUnit.Framework;

namespace CatMetro.Tests.Purchases
{
    // The parser's contract: it NEVER throws, and every rejection is visible in Problems.
    // A purchase catalogue that blows up during boot is a black screen; one that silently
    // half-loads is the CatModelCatalog failure AGENTS.md warns about, where an empty-looking
    // screen has no log line explaining it.
    public sealed class PurchaseCatalogTests
    {
        [Test]
        public void CleanCatalogue_ParsesEveryProductAndEntitlement_WithNoProblems()
        {
            var c = PFixtures.TinyCatalog();

            Assert.That(c.Problems, Is.Empty, "the fixture catalogue is meant to be clean");
            Assert.That(c.Products.Count, Is.EqualTo(3));
            Assert.That(c.Entitlements.Count, Is.EqualTo(3));
            Assert.That(c.TryGetProduct("cm_outfit_conductor", out var p), Is.True);
            Assert.That(p.StoreType, Is.EqualTo(PurchaseStoreType.NonConsumable));
            Assert.That(p.DisplayName, Is.EqualTo("Conductor's Coat"));
        }

        [Test]
        public void Bundle_GrantsEveryEntitlementItLists()
        {
            var c = PFixtures.TinyCatalog();
            Assert.That(c.EntitlementsFor("cm_bundle"),
                Is.EqualTo(new[] { "outfit_conductor", "frame_brass", "supporter" }));
        }

        // The join that actually matters at runtime: the store hands back a product id and we
        // must turn it into entitlements. An id we do not know grants nothing — it must not
        // throw, and it must not grant something arbitrary.
        [Test]
        public void UnknownProductId_GrantsNothingAndDoesNotThrow()
        {
            var c = PFixtures.TinyCatalog();
            Assert.That(c.EntitlementsFor("cm_something_the_store_invented"), Is.Empty);
            Assert.That(c.TryGetProduct(null, out _), Is.False);
            Assert.That(c.TryGetEntitlement(null, out _), Is.False);
        }

        // ---- the never-throws contract, one input class per case ------------------------

        [Test]
        public void NullOrEmptySource_YieldsEmptyCatalogue_NotAnException()
        {
            foreach (var input in new[] { null, "", "   " })
            {
                var c = PurchaseCatalog.Parse(input);
                Assert.That(c.IsEmpty, Is.True);
                Assert.That(c.Problems, Is.Not.Empty, "an empty catalogue must say why");
            }
        }

        [Test]
        public void MalformedJson_YieldsEmptyCatalogue_NotAnException()
        {
            // A truncated file is the realistic corruption: an interrupted write, a bad merge.
            var c = PurchaseCatalog.Parse("{ \"products\": [ { \"id\": \"cm_x\"");
            Assert.That(c.IsEmpty, Is.True);
            Assert.That(c.Problems.Any(p => p.Contains("not valid JSON")), Is.True);
        }

        [Test]
        public void JsonThatIsNotAnObject_IsRejectedCleanly()
        {
            foreach (var input in new[] { "[]", "\"a string\"", "42", "null" })
            {
                var c = PurchaseCatalog.Parse(input);
                Assert.That(c.IsEmpty, Is.True, "input was: " + input);
                Assert.That(c.Problems, Is.Not.Empty, "input was: " + input);
            }
        }

        // ---- the rejections that protect the player -------------------------------------

        // The expensive mistake: a product that promises an entitlement nobody declared would
        // take real money and unlock nothing. The reference is dropped and the problem is loud.
        [Test]
        public void ProductGrantingAnUndeclaredEntitlement_DropsTheReferenceAndComplains()
        {
            var c = PurchaseCatalog.Parse(@"{
              ""entitlements"": [ { ""id"": ""outfit_conductor"", ""kind"": ""outfit"" } ],
              ""products"": [ { ""id"": ""cm_x"", ""storeType"": ""non_consumable"",
                                ""entitlements"": [""outfit_conductor"", ""outfit_ghost""] } ]
            }");

            Assert.That(c.EntitlementsFor("cm_x"), Is.EqualTo(new[] { "outfit_conductor" }),
                "the real entitlement survives; the phantom one does not");
            Assert.That(c.Problems.Any(p => p.Contains("outfit_ghost")), Is.True,
                "and the drop is reported by name, not silently");
        }

        // A product we cannot classify is a product we must not sell: we would not know whether
        // to offer restore for it, or whether it is consumed on use.
        [Test]
        public void ProductWithUnknownStoreType_IsDroppedEntirely()
        {
            var c = PurchaseCatalog.Parse(@"{
              ""entitlements"": [ { ""id"": ""e"", ""kind"": ""outfit"" } ],
              ""products"": [ { ""id"": ""cm_x"", ""storeType"": ""rental"", ""entitlements"": [""e""] } ]
            }");

            Assert.That(c.TryGetProduct("cm_x", out _), Is.False);
            Assert.That(c.Problems.Any(p => p.Contains("unknown storeType")), Is.True);
        }

        [Test]
        public void DuplicateIds_KeepTheFirstAndComplain()
        {
            var c = PurchaseCatalog.Parse(@"{
              ""entitlements"": [ { ""id"": ""e"", ""kind"": ""outfit"" }, { ""id"": ""e"", ""kind"": ""frame"" } ],
              ""products"": [ { ""id"": ""cm_x"", ""storeType"": ""non_consumable"", ""entitlements"": [""e""] },
                              { ""id"": ""cm_x"", ""storeType"": ""consumable"", ""entitlements"": [] } ]
            }");

            Assert.That(c.Entitlements.Count, Is.EqualTo(1));
            Assert.That(c.Products.Count, Is.EqualTo(1));
            Assert.That(c.TryGetEntitlement("e", out var e), Is.True);
            Assert.That(e.Kind, Is.EqualTo(EntitlementKind.Outfit), "the FIRST declaration wins");
            Assert.That(c.Problems.Count(p => p.Contains("duplicate")), Is.EqualTo(2));
        }

        // A non-consumable that unlocks nothing is money for nothing; a subscription that
        // unlocks nothing cannot be sold at all. Both are reported. (A consumable granting
        // nothing durable is legitimate and is NOT reported.)
        [Test]
        public void ProductsThatUnlockNothing_AreReported_ExceptConsumables()
        {
            var c = PurchaseCatalog.Parse(@"{
              ""entitlements"": [ { ""id"": ""e"", ""kind"": ""outfit"" } ],
              ""products"": [ { ""id"": ""cm_dud"", ""storeType"": ""non_consumable"", ""entitlements"": [] },
                              { ""id"": ""cm_sub"", ""storeType"": ""subscription"", ""entitlements"": [] },
                              { ""id"": ""cm_use"", ""storeType"": ""consumable"", ""entitlements"": [] } ]
            }");

            Assert.That(c.Problems.Any(p => p.Contains("cm_dud")), Is.True);
            Assert.That(c.Problems.Any(p => p.Contains("cm_sub")), Is.True);
            Assert.That(c.Problems.Any(p => p.Contains("cm_use")), Is.False,
                "a consumable is spent on use and legitimately grants nothing durable");
        }

        [Test]
        public void NegativeAdLease_IsClampedToNotAdGrantable_NotToAShorterLease()
        {
            var c = PurchaseCatalog.Parse(@"{
              ""entitlements"": [ { ""id"": ""e"", ""kind"": ""outfit"", ""adLeaseSeconds"": -60 } ],
              ""products"": [ { ""id"": ""cm_x"", ""storeType"": ""non_consumable"", ""entitlements"": [""e""] } ]
            }");

            Assert.That(c.TryGetEntitlement("e", out var e), Is.True);
            Assert.That(e.IsAdGrantable, Is.False,
                "a negative lease must not become an entitlement that expires before it is given");
            Assert.That(c.Problems.Any(p => p.Contains("negative")), Is.True);
        }

        // ---- restore classification is data, not a preference ---------------------------

        [Test]
        public void RestorabilityFollowsStoreType()
        {
            var c = PurchaseCatalog.Parse(@"{
              ""entitlements"": [ { ""id"": ""e"", ""kind"": ""outfit"" } ],
              ""products"": [ { ""id"": ""cm_n"", ""storeType"": ""non_consumable"", ""entitlements"": [""e""] },
                              { ""id"": ""cm_s"", ""storeType"": ""subscription"", ""entitlements"": [""e""] },
                              { ""id"": ""cm_c"", ""storeType"": ""consumable"", ""entitlements"": [] } ]
            }");

            c.TryGetProduct("cm_n", out var n);
            c.TryGetProduct("cm_s", out var s);
            c.TryGetProduct("cm_c", out var con);

            Assert.That(n.IsRestorable, Is.True, "both stores REQUIRE restore for non-consumables");
            Assert.That(s.IsRestorable, Is.True);
            Assert.That(con.IsRestorable, Is.False,
                "restoring a consumed purchase either no-ops or double-grants; never do it");
        }
    }
}
