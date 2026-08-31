using System.Linq;
using CatMetro.Services.Purchases;
using NUnit.Framework;

namespace CatMetro.Tests.Purchases
{
    // The parser tests prove the CODE is right. These prove the DATA is right — the actual
    // product_catalog.json and rewarded_placements.json that ship in Resources. A failure here
    // means someone broke the catalogue, not the parser, and the split tells you which.
    //
    // This matters more than it looks: the catalogue is authored by hand and every id in it also
    // has to be typed into Google Play Console and the RevenueCat dashboard. A typo is silent at
    // runtime — the offering just comes back without that package — so it has to be loud here.
    public sealed class ShippedCatalogTests
    {
        private static PurchaseCatalog Shipped() => PurchaseCatalog.Parse(PFixtures.ShippedCatalogJson());

        [Test]
        public void TheShippedCatalogue_ParsesWithNoProblemsAtAll()
        {
            var c = Shipped();
            Assert.That(c.Problems, Is.Empty,
                "problems: " + string.Join(" | ", c.Problems));
            Assert.That(c.IsEmpty, Is.False);
        }

        // Every constant in code must name something that actually exists in the data. Without
        // this, renaming a SKU in JSON leaves ProductIds pointing at nothing and the shop quietly
        // loses an item.
        [Test]
        public void EveryProductIdConstant_ExistsInTheShippedCatalogue()
        {
            var c = Shipped();
            foreach (var id in new[]
                     {
                         ProductIds.OutfitConductor, ProductIds.OutfitEngineer,
                         ProductIds.AccessoryScarf, ProductIds.AccessoryGoggles,
                         ProductIds.FrameBrass, ProductIds.FrameLantern,
                         ProductIds.BundleStationmaster
                     })
            {
                Assert.That(c.TryGetProduct(id, out _), Is.True, "missing product: " + id);
            }
        }

        [Test]
        public void EveryEntitlementIdConstant_ExistsInTheShippedCatalogue()
        {
            var c = Shipped();
            foreach (var id in new[]
                     {
                         EntitlementIds.OutfitConductor, EntitlementIds.OutfitEngineer,
                         EntitlementIds.AccessoryScarf, EntitlementIds.AccessoryGoggles,
                         EntitlementIds.FrameBrass, EntitlementIds.FrameLantern,
                         EntitlementIds.Supporter
                     })
            {
                Assert.That(c.TryGetEntitlement(id, out _), Is.True, "missing entitlement: " + id);
            }
        }

        // The SKU that satisfies the Shipaton "RevenueCat SDK powers at least one purchase" rule.
        // If this one is not present, correctly typed, non-consumable and actually granting
        // something, the whole submission is ineligible — so it gets its own test.
        [Test]
        public void TheGateSku_IsPresent_NonConsumable_AndGrantsAnEntitlement()
        {
            var c = Shipped();

            Assert.That(c.TryGetProduct(ProductIds.Gate, out var gate), Is.True,
                "the eligibility SKU is missing from the catalogue");
            Assert.That(gate.StoreType, Is.EqualTo(PurchaseStoreType.NonConsumable),
                "must be non-consumable: with Billing Client 8 a CONSUMED one-time purchase " +
                "cannot be restored for an anonymous user, so the player would lose it forever");
            Assert.That(gate.IsRestorable, Is.True, "and both stores require a restore path for it");
            Assert.That(gate.Entitlements, Is.Not.Empty, "a purchase that unlocks nothing is money for nothing");
        }

        // Non-consumables are the whole launch set on purpose. Subscriptions and consumables need
        // materially more store configuration and more failure handling, and neither is needed to
        // sell a hat.
        [Test]
        public void EveryShippedProduct_IsANonConsumable()
        {
            var c = Shipped();
            foreach (var p in c.Products)
                Assert.That(p.StoreType, Is.EqualTo(PurchaseStoreType.NonConsumable), p.Id);
        }

        [Test]
        public void EveryProductId_UsesTheCmPrefix_AndEveryEntitlementIdDoesNot()
        {
            var c = Shipped();
            foreach (var p in c.Products)
                Assert.That(p.Id, Does.StartWith("cm_"), "store product ids carry the cm_ prefix");
            foreach (var e in c.Entitlements)
                Assert.That(e.Id, Does.Not.StartWith("cm_"),
                    "entitlement ids do not, so the two can never be confused in a console");
        }

        [Test]
        public void EveryEntitlement_IsGrantedByAtLeastOneProduct()
        {
            var c = Shipped();
            var granted = c.Products.SelectMany(p => p.Entitlements).ToHashSet();
            foreach (var e in c.Entitlements)
                Assert.That(granted.Contains(e.Id), Is.True,
                    "entitlement " + e.Id + " can never be obtained — no product grants it");
        }

        [Test]
        public void EveryEntitlement_HasAKnownKindAndADisplayName()
        {
            var c = Shipped();
            foreach (var e in c.Entitlements)
            {
                Assert.That(e.Kind, Is.Not.EqualTo(EntitlementKind.Unknown), e.Id);
                Assert.That(e.DisplayName, Is.Not.Null.And.Not.Empty, e.Id);
                Assert.That(e.DisplayName, Is.Not.EqualTo(e.Id),
                    "display names are for players, ids are for consoles: " + e.Id);
            }
        }

        // The bundle has to be worth more than its parts or it is a trap.
        [Test]
        public void TheBundle_GrantsMoreThanAnySingleProduct()
        {
            var c = Shipped();
            c.TryGetProduct(ProductIds.BundleStationmaster, out var bundle);
            Assert.That(bundle.Entitlements.Count, Is.GreaterThan(1));

            foreach (var p in c.Products.Where(p => p.Id != ProductIds.BundleStationmaster))
                Assert.That(bundle.Entitlements.Count, Is.GreaterThan(p.Entitlements.Count),
                    "the bundle must beat " + p.Id);
        }

        // ---- rewarded placements ---------------------------------------------------------

        [Test]
        public void TheShippedPlacements_ParseWithNoProblems()
        {
            var p = RewardedPlacementCatalog.Parse(PFixtures.ShippedPlacementsJson(), Shipped());
            Assert.That(p.Problems, Is.Empty, "problems: " + string.Join(" | ", p.Problems));
            Assert.That(p.Placements, Is.Not.Empty);
        }

        [Test]
        public void ExactlyFourWardrobePlacements_AreEnabledWithTheirApprovedRewardsAndCaps()
        {
            var p = RewardedPlacementCatalog.Parse(PFixtures.ShippedPlacementsJson(), Shipped());
            var expected = new[]
            {
                new { Id = "wardrobe_try_conductor", Entitlement = EntitlementIds.OutfitConductor,
                    Caps = new[] { new { Scope = "localDate", Limit = 1 } } },
                new { Id = "wardrobe_try_engineer", Entitlement = EntitlementIds.OutfitEngineer,
                    Caps = new[] { new { Scope = "localDate", Limit = 1 } } },
                new { Id = "wardrobe_try_scarf", Entitlement = EntitlementIds.AccessoryScarf,
                    Caps = new[] { new { Scope = "localDate", Limit = 1 } } },
                new { Id = "wardrobe_try_goggles", Entitlement = EntitlementIds.AccessoryGoggles,
                    Caps = new[]
                    {
                        new { Scope = "localDate", Limit = 1 },
                        new { Scope = "session", Limit = 1 }
                    }
                }
            };

            var enabled = p.Placements.Where(placement => placement.Enabled).ToArray();
            Assert.That(enabled.Select(placement => placement.Id),
                Is.EquivalentTo(expected.Select(row => row.Id)),
                "only genuine locked-item Wardrobe needs may ship enabled");

            foreach (var row in expected)
            {
                Assert.That(p.TryGet(row.Id, out var placement), Is.True, row.Id);
                Assert.That(placement.Enabled, Is.True, row.Id + " must be player-visible");
                Assert.That(placement.EntitlementId, Is.EqualTo(row.Entitlement), row.Id);
                Assert.That(placement.DisabledReason, Is.Null.Or.Empty,
                    row.Id + " cannot retain the obsolete no-network explanation");
                Assert.That(placement.Caps.Select(cap => new { cap.Scope, cap.Limit }),
                    Is.EquivalentTo(row.Caps), row.Id + " cap mapping changed");
            }

            Assert.That(enabled.Any(placement =>
                    !placement.Id.StartsWith("wardrobe_try_", System.StringComparison.Ordinal)),
                Is.False, "no level-boundary, rewind, or generic monetization placement is enabled");
        }

        [Test]
        public void EveryShippedPlacement_HasACapSoARewardCannotBeFarmed()
        {
            var p = RewardedPlacementCatalog.Parse(PFixtures.ShippedPlacementsJson(), Shipped());
            foreach (var placement in p.Placements)
                Assert.That(placement.Caps, Is.Not.Empty,
                    placement.Id + " has no cap; a player could watch ads until everything is free");
        }

        // Frames are premium — buying them is the only way. This pins that intent in a test so a
        // later data edit that makes them ad-grantable is a deliberate choice, not an accident.
        [Test]
        public void Frames_AreNotAdGrantable_ButOutfitsAndAccessoriesAre()
        {
            var c = Shipped();
            foreach (var e in c.Entitlements)
            {
                switch (e.Kind)
                {
                    case EntitlementKind.Frame:
                    case EntitlementKind.Membership:
                        Assert.That(e.IsAdGrantable, Is.False, e.Id + " should be bought, not lent");
                        break;
                    case EntitlementKind.Outfit:
                    case EntitlementKind.Accessory:
                        Assert.That(e.IsAdGrantable, Is.True, e.Id + " should be try-able via an ad");
                        break;
                }
            }
        }

        // ---- end to end over the real data -----------------------------------------------

        [Test]
        public void TheGateSku_UnlocksItsCosmetic_OverTheRealCatalogue()
        {
            var (svc, backend, _) = PFixtures.Service(Shipped());
            backend.WithProduct(ProductIds.Gate, "Conductor's Coat", "$2.99");
            svc.Refresh();

            Assert.That(svc.TryGetPrice(ProductIds.Gate, out var price), Is.True);
            Assert.That(price.DisplayText, Is.EqualTo("$2.99"));
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False);

            PurchaseResult result = default;
            svc.Purchase(ProductIds.Gate, r => result = r);

            Assert.That(result.Outcome, Is.EqualTo(PurchaseOutcome.SuccessCandidate));
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);
        }

        [Test]
        public void TheBundle_UnlocksAllFourOfItsCosmetics_OverTheRealCatalogue()
        {
            var (svc, _, _) = PFixtures.Service(Shipped());
            svc.Purchase(ProductIds.BundleStationmaster, _ => { });

            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);
            Assert.That(svc.IsUnlocked(EntitlementIds.AccessoryScarf), Is.True);
            Assert.That(svc.IsUnlocked(EntitlementIds.FrameBrass), Is.True);
            Assert.That(svc.IsUnlocked(EntitlementIds.Supporter), Is.True);
            Assert.That(svc.IsUnlocked(EntitlementIds.FrameLantern), Is.False,
                "and nothing it does not list");
        }
    }
}
