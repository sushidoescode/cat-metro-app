using System;
using System.Collections.Generic;
using CatMetro.Services.Purchases;
using NUnit.Framework;

namespace CatMetro.Tests.Purchases
{
    // "The game must never hard-fail because a purchase system is unreachable."
    //
    // Four ways it can be unreachable, and all four must be survivable: no SDK compiled in, no
    // API key configured, no network, and running in the Unity Editor (where RevenueCat's own
    // docs say the SDK does not work at all, so this is the state EVERY editor test and every
    // play-in-editor session is in).
    //
    // The result in all four cases is the same and is deliberately boring: the shop lists the
    // catalogue with no prices, every cosmetic reads as locked, every command calls back with
    // Unavailable, and nothing throws.
    public sealed class GracefulDegradationTests
    {
        // ---- no backend at all ------------------------------------------------------------

        [Test]
        public void AServiceWithNoBackend_IsStillFullyUsable()
        {
            var svc = new PurchaseService(PFixtures.TinyCatalog());

            Assert.That(svc.Availability, Is.EqualTo(BackendAvailability.NotCompiled));
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False);
            Assert.That(svc.ShopItems().Count, Is.EqualTo(3),
                "the shop still lists what is FOR SALE even with no store to price it");
            Assert.That(svc.TryGetPrice(ProductIds.OutfitConductor, out _), Is.False);
        }

        [Test]
        public void EveryCommand_CallsBackExactlyOnce_EvenWithNoBackend()
        {
            var svc = new PurchaseService(PFixtures.TinyCatalog());

            int purchases = 0, restores = 0, refreshes = 0;
            svc.Purchase(ProductIds.OutfitConductor, r =>
            {
                purchases++;
                Assert.That(r.Outcome, Is.EqualTo(PurchaseOutcome.Unavailable));
            });
            svc.Restore(r =>
            {
                restores++;
                Assert.That(r.Outcome, Is.EqualTo(RestoreOutcome.Unavailable));
            });
            svc.Refresh(() => refreshes++);

            Assert.That(purchases, Is.EqualTo(1), "a dropped callback hangs a UI spinner forever");
            Assert.That(restores, Is.EqualTo(1));
            Assert.That(refreshes, Is.EqualTo(1));
        }

        [Test]
        public void AServiceOverAnEmptyCatalogue_IsStillUsable()
        {
            var svc = new PurchaseService(PurchaseCatalog.Empty);

            Assert.That(svc.ShopItems(), Is.Empty);
            Assert.That(svc.IsUnlocked("anything"), Is.False);
            Assert.DoesNotThrow(() => svc.Refresh());
            Assert.That(svc.GrantRewardedAdEntitlement("anything"),
                Is.EqualTo(AdGrantOutcome.UnknownEntitlement));
        }

        [Test]
        public void ANullCatalogueArgument_IsTreatedAsEmpty_NotAsANullReference()
        {
            var svc = new PurchaseService(null, null, null);
            Assert.That(svc.ShopItems(), Is.Empty);
            Assert.That(svc.IsUnlocked("x"), Is.False);
        }

        // ---- the offline case, which is the dangerous one --------------------------------

        // THE most important test in this file. An unreachable RevenueCat reports "no
        // entitlements" — and if that were believed, launching the game on a plane would revoke
        // everything the player paid for. The non-authoritative flag is what prevents it.
        [Test]
        public void GoingOffline_DoesNotRevokeAlreadyKnownPurchases()
        {
            var (svc, backend, _) = PFixtures.Service();

            backend.WithEntitlement(EntitlementIds.OutfitConductor);
            svc.RefreshEntitlements();
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);

            // Network drops. RevenueCat cannot be reached, so nothing it "says" is authoritative.
            backend.EntitlementsAreAuthoritative = false;
            backend.Availability = BackendAvailability.Unreachable;
            svc.RefreshEntitlements();

            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True,
                "an unreachable store must never be able to revoke a paid entitlement");
        }

        [Test]
        public void AFreshOfflineLaunch_ShowsNothingUnlocked_WithoutClaimingAuthority()
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.EntitlementsAreAuthoritative = false;
            backend.WithEntitlement(EntitlementIds.OutfitConductor);

            svc.Refresh();

            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False,
                "conservative: we genuinely do not know yet");
            Assert.That(svc.HasAuthoritativeEntitlements, Is.False,
                "so the UI can say 'checking' rather than 'you own nothing'");
        }

        [Test]
        public void ComingBackOnline_AppliesTheTruth()
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.EntitlementsAreAuthoritative = false;
            backend.WithEntitlement(EntitlementIds.OutfitConductor);
            svc.RefreshEntitlements();
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False);

            backend.EntitlementsAreAuthoritative = true;
            svc.RefreshEntitlements();

            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);
            Assert.That(svc.HasAuthoritativeEntitlements, Is.True);
        }

        // An ad reward earned offline is still the player's — it was never RevenueCat's to know.
        [Test]
        public void AnAdRewardEarnedOffline_StillWorks()
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.EntitlementsAreAuthoritative = false;
            backend.Availability = BackendAvailability.Unreachable;

            Assert.That(svc.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor),
                Is.EqualTo(AdGrantOutcome.Granted));
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);

            svc.RefreshEntitlements();
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True,
                "and a failed sync does not take it away");
        }

        // ---- refusals that protect the player -------------------------------------------

        [Test]
        public void BuyingSomethingNotInTheCatalogue_NeverReachesTheStore()
        {
            var (svc, backend, _) = PFixtures.Service();

            PurchaseResult result = default;
            svc.Purchase("cm_not_a_real_product", r => result = r);

            Assert.That(result.Outcome, Is.EqualTo(PurchaseOutcome.Unavailable));
            Assert.That(backend.PurchaseCallCount, Is.EqualTo(0),
                "a purchase we could not honour must not be attempted; we would take money and grant nothing");
        }

        [Test]
        public void AProductTheStoreOffersButTheCatalogueDoesNot_IsIgnored()
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.WithProduct("cm_outfit_conductor", "Coat", "$1.99")
                   .WithProduct("cm_leftover_from_a_test", "Junk", "$99.99");

            svc.Refresh();

            Assert.That(svc.StoreProductCount, Is.EqualTo(1));
            Assert.That(svc.TryGetPrice("cm_leftover_from_a_test", out _), Is.False,
                "a stray product in the store console must not become purchasable");
        }

        [TestCase(BackendAvailability.Unreachable)]
        [TestCase(BackendAvailability.Initializing)]
        [TestCase(BackendAvailability.NotConfigured)]
        [TestCase(BackendAvailability.NotCompiled)]
        public void NonReadyEmptyOfferings_PreserveTheLastReadyLocalizedPrice(
            BackendAvailability availability)
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.WithProduct(ProductIds.OutfitConductor, "Conductor's Coat", "CA$2.79");
            svc.Refresh();
            Assert.That(svc.StoreProductCount, Is.EqualTo(1));
            Assert.That(svc.TryGetPrice(ProductIds.OutfitConductor, out var before), Is.True);
            Assert.That(before.DisplayText, Is.EqualTo("CA$2.79"));

            backend.ClearProducts();
            backend.Availability = availability;
            int callbacks = 0;
            svc.Refresh(() => callbacks++);

            Assert.That(callbacks, Is.EqualTo(1), "a preserved cache cannot strand refresh UI");
            Assert.That(svc.StoreProductCount, Is.EqualTo(1));
            Assert.That(svc.TryGetPrice(ProductIds.OutfitConductor, out var after), Is.True);
            Assert.That(after.DisplayText, Is.EqualTo("CA$2.79"));
        }

        [Test]
        public void ReadyEmptyOfferings_AuthoritativelyClearTheLocalizedPriceCache()
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.WithProduct(ProductIds.OutfitConductor, "Conductor's Coat", "CA$2.79");
            svc.Refresh();
            Assert.That(svc.StoreProductCount, Is.EqualTo(1));

            backend.ClearProducts();
            backend.Availability = BackendAvailability.Ready;
            int callbacks = 0;
            svc.Refresh(() => callbacks++);

            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(svc.StoreProductCount, Is.EqualTo(0));
            Assert.That(svc.TryGetPrice(ProductIds.OutfitConductor, out _), Is.False);
        }

        [Test]
        public void ReadyNullOfferings_PreserveTheLastReadyLocalizedPriceCache()
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.WithProduct(ProductIds.OutfitConductor, "Conductor's Coat", "CA$2.79");
            svc.Refresh();

            backend.ReturnNullProducts = true;
            int callbacks = 0;
            svc.Refresh(() => callbacks++);

            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(svc.StoreProductCount, Is.EqualTo(1));
            Assert.That(svc.TryGetPrice(ProductIds.OutfitConductor, out var price), Is.True);
            Assert.That(price.DisplayText, Is.EqualTo("CA$2.79"),
                "Ready plus null is a failed response, not authoritative empty offerings");
        }

        [Test]
        public void ReadyPartialOfferings_ReplaceRatherThanMergeTheLastReadyCache()
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.WithProduct(ProductIds.OutfitConductor, "Conductor's Coat", "CA$2.79")
                .WithProduct(ProductIds.FrameBrass, "Brass Frame", "CA$1.39");
            svc.Refresh();
            Assert.That(svc.StoreProductCount, Is.EqualTo(2));

            backend.ClearProducts();
            backend.WithProduct(ProductIds.FrameBrass, "Brass Frame", "CA$1.49");
            svc.Refresh();

            Assert.That(svc.StoreProductCount, Is.EqualTo(1));
            Assert.That(svc.TryGetPrice(ProductIds.OutfitConductor, out _), Is.False);
            Assert.That(svc.TryGetPrice(ProductIds.FrameBrass, out var price), Is.True);
            Assert.That(price.DisplayText, Is.EqualTo("CA$1.49"));
        }

        [Test]
        public void DoubleTappingBuy_IsRefusedWhileTheFirstIsInFlight()
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.DeferCallbacks = true;

            PurchaseResult first = default, second = default;
            svc.Purchase(ProductIds.OutfitConductor, r => first = r);
            svc.Purchase(ProductIds.OutfitConductor, r => second = r);

            Assert.That(second.Outcome, Is.EqualTo(PurchaseOutcome.Busy));
            Assert.That(backend.PurchaseCallCount, Is.EqualTo(1),
                "the store is asked once, not twice");

            backend.DeferCallbacks = false;
            backend.CompleteDeferred();
            Assert.That(first.Outcome, Is.EqualTo(PurchaseOutcome.SuccessCandidate));
        }

        [Test]
        public void BuyingWhileRestoreIsInFlight_IsRefusedBeforeTheStore()
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.DeferCallbacks = true;

            svc.Restore(_ => { });
            PurchaseResult purchase = default;
            svc.Purchase(ProductIds.OutfitConductor, r => purchase = r);

            Assert.That(purchase.Outcome, Is.EqualTo(PurchaseOutcome.Busy));
            Assert.That(backend.PurchaseCallCount, Is.EqualTo(0),
                "transactions cannot overlap and make a late restore revoke a newer purchase");
        }

        [Test]
        public void AfterAPurchaseSettles_TheNextOneIsAllowed()
        {
            var (svc, backend, _) = PFixtures.Service();
            svc.Purchase(ProductIds.OutfitConductor, _ => { });
            svc.Purchase(ProductIds.FrameBrass, _ => { });
            Assert.That(backend.PurchaseCallCount, Is.EqualTo(2), "the busy flag must clear");
        }

        // A cancelled or failed purchase must leave nothing unlocked and must not read as an
        // error to the player.
        [TestCase(PurchaseOutcome.UserCancelled)]
        [TestCase(PurchaseOutcome.Failure)]
        [TestCase(PurchaseOutcome.Pending)]
        public void ANonGrantableOutcome_UnlocksNothing(PurchaseOutcome outcome)
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.NextPurchaseOutcome = outcome;

            PurchaseResult result = default;
            svc.Purchase(ProductIds.OutfitConductor, r => result = r);

            Assert.That(result.Outcome, Is.EqualTo(outcome));
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False);
            Assert.That(backend.RefreshEntitlementsCallCount, Is.EqualTo(0),
                "nothing changed, so nothing is re-read");
        }

        // Pending deserves its own note: on Google Play a slow card or a parental approval
        // returns before payment settles. Granting here would give away cosmetics for free.
        [Test]
        public void APendingPurchase_GrantsNothingYet()
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.NextPurchaseOutcome = PurchaseOutcome.Pending;
            svc.Purchase(ProductIds.OutfitConductor, _ => { });
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False);
        }

        // ---- entitlements come from CustomerInfo, never from the purchase callback --------

        [Test]
        public void ASuccessfulPurchase_IsConfirmedByRereadingEntitlements()
        {
            var (svc, backend, _) = PFixtures.Service();

            svc.Purchase(ProductIds.OutfitConductor, _ => { });

            Assert.That(backend.RefreshEntitlementsCallCount, Is.EqualTo(1),
                "the store saying yes is not the same as owning it; CustomerInfo is the authority");
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);
        }

        // The subtle one: a store that reports success but whose CustomerInfo does not confirm
        // it. The honest outcome is that nothing unlocks — better than granting on a claim.
        [Test]
        public void SuccessWithoutConfirmation_UnlocksNothing()
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.GrantOnPurchase = null; // store says yes, CustomerInfo never agrees

            PurchaseResult result = default;
            svc.Purchase(ProductIds.OutfitConductor, r => result = r);

            Assert.That(result.Outcome, Is.EqualTo(PurchaseOutcome.SuccessCandidate));
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False,
                "hence the name SuccessCandidate rather than Success");
        }

        [Test]
        public void FallbackConfirmation_EnrichesTheResultAndPreservesItsStoreMetadata()
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.NextPurchaseDiagnostic = "candidate diagnostic";

            PurchaseResult result = default;
            svc.Purchase(ProductIds.OutfitConductor, r => result = r);

            Assert.That(result.Outcome, Is.EqualTo(PurchaseOutcome.SuccessCandidate));
            Assert.That(result.ProductId, Is.EqualTo(ProductIds.OutfitConductor));
            Assert.That(result.LocalizedPrice.DisplayText, Is.EqualTo("$1.99"));
            Assert.That(result.DiagnosticMessage, Is.EqualTo("candidate diagnostic"));
            Assert.That(result.ConfirmedEntitlements.HasValue, Is.True,
                "accepted fallback CustomerInfo must be returned as transaction confirmation");
            Assert.That(result.ConfirmedEntitlements.Value.IsAuthoritative, Is.True);
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);
        }

        [Test]
        public void FallbackSnapshotMissingThePromisedEntitlement_IsAppliedButNotConfirmation()
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.GrantOnPurchase = null;
            svc.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor);

            PurchaseResult result = default;
            svc.Purchase(ProductIds.OutfitConductor, r => result = r);

            Assert.That(result.Outcome, Is.EqualTo(PurchaseOutcome.SuccessCandidate));
            Assert.That(result.ConfirmedEntitlements.HasValue, Is.False);
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True,
                "the independent rewarded lease remains wearable but confirms no purchase");
            Assert.That(svc.SecondsUntilExpiry(EntitlementIds.OutfitConductor), Is.GreaterThan(0));
        }

        [Test]
        public void UnreachableFallback_PreservesAccessButReturnsNoTransactionConfirmation()
        {
            var (svc, backend, _) = PFixtures.Service();
            svc.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor);
            backend.EntitlementsAreAuthoritative = false;
            backend.Availability = BackendAvailability.Unreachable;

            PurchaseResult result = default;
            svc.Purchase(ProductIds.OutfitConductor, r => result = r);

            Assert.That(result.Outcome, Is.EqualTo(PurchaseOutcome.SuccessCandidate));
            Assert.That(result.ConfirmedEntitlements.HasValue, Is.False);
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);
            Assert.That(svc.SecondsUntilExpiry(EntitlementIds.OutfitConductor), Is.GreaterThan(0));
        }

        [Test]
        public void DirectSnapshotWithEveryPromisedEntitlement_RemainsConfirmation()
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.GrantOnPurchase = null;
            backend.NextPurchaseConfirmedEntitlements = new EntitlementSnapshot(true, new[]
            {
                new EntitlementGrant(EntitlementIds.OutfitConductor, GrantSource.Store),
            });

            PurchaseResult result = default;
            svc.Purchase(ProductIds.OutfitConductor, r => result = r);

            Assert.That(result.ConfirmedEntitlements.HasValue, Is.True);
            Assert.That(result.ConfirmedEntitlements.Value.IsAuthoritative, Is.True);
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);
            Assert.That(backend.RefreshEntitlementsCallCount, Is.EqualTo(0),
                "direct authoritative CustomerInfo needs no redundant fallback");
        }

        [Test]
        public void DirectSnapshotMissingAnyBundlePromise_IsTruthButNotConfirmation()
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.GrantOnPurchase = null;
            backend.NextPurchaseConfirmedEntitlements = new EntitlementSnapshot(true, new[]
            {
                new EntitlementGrant(EntitlementIds.OutfitConductor, GrantSource.Store),
            });

            PurchaseResult result = default;
            svc.Purchase("cm_bundle", r => result = r);

            Assert.That(result.Outcome, Is.EqualTo(PurchaseOutcome.SuccessCandidate));
            Assert.That(result.ConfirmedEntitlements.HasValue, Is.False,
                "one of three bundle grants cannot confirm fulfilment of the whole product");
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True,
                "the authoritative partial snapshot is still applied as account truth");
            Assert.That(svc.IsUnlocked(EntitlementIds.FrameBrass), Is.False);
            Assert.That(svc.IsUnlocked("supporter"), Is.False);
            Assert.That(backend.RefreshEntitlementsCallCount, Is.EqualTo(0));
        }

        [TestCase(null, false)]
        [TestCase("cm_unknown_backend_product", false)]
        [TestCase(null, true)]
        [TestCase("cm_unknown_backend_product", true)]
        public void NullOrUnknownBackendProductMetadata_CannotVacuouslyConfirmTheRequestedProduct(
            string returnedProductId, bool direct)
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.GrantOnPurchase = null;
            backend.OverridePurchaseResultProductId = true;
            backend.NextPurchaseResultProductId = returnedProductId;
            if (direct)
            {
                backend.NextPurchaseConfirmedEntitlements =
                    new EntitlementSnapshot(true, Array.Empty<EntitlementGrant>());
            }
            svc.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor);

            PurchaseResult result = default;
            svc.Purchase(ProductIds.OutfitConductor, r => result = r);

            Assert.That(result.ProductId, Is.EqualTo(returnedProductId),
                "backend metadata is preserved rather than silently rewritten");
            Assert.That(result.ConfirmedEntitlements.HasValue, Is.False,
                "zero promises for unknown metadata cannot confirm the caller's coat purchase");
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True,
                "the independent rewarded lease remains active but is not purchase truth");
            Assert.That(svc.SecondsUntilExpiry(EntitlementIds.OutfitConductor), Is.GreaterThan(0));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MismatchedBackendMetadata_StillConfirmsWhenTheRequestedProductIsFulfilled(
            bool direct)
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.OverridePurchaseResultProductId = true;
            backend.NextPurchaseResultProductId = ProductIds.FrameBrass;
            if (direct)
            {
                backend.GrantOnPurchase = null;
                backend.NextPurchaseConfirmedEntitlements = new EntitlementSnapshot(true, new[]
                {
                    new EntitlementGrant(EntitlementIds.OutfitConductor, GrantSource.Store)
                });
            }

            PurchaseResult result = default;
            svc.Purchase(ProductIds.OutfitConductor, r => result = r);

            Assert.That(result.ProductId, Is.EqualTo(ProductIds.FrameBrass));
            Assert.That(result.ConfirmedEntitlements.HasValue, Is.True,
                "confirmation is anchored to the caller-requested coat, not returned metadata");
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MismatchedBackendMetadata_CannotConfirmOnlyTheWrongProductTruth(bool direct)
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.OverridePurchaseResultProductId = true;
            backend.NextPurchaseResultProductId = ProductIds.FrameBrass;
            backend.GrantOnPurchase = _ => new[] { EntitlementIds.FrameBrass };
            if (direct)
            {
                backend.NextPurchaseConfirmedEntitlements = new EntitlementSnapshot(true, new[]
                {
                    new EntitlementGrant(EntitlementIds.FrameBrass, GrantSource.Store)
                });
            }

            PurchaseResult result = default;
            svc.Purchase(ProductIds.OutfitConductor, r => result = r);

            Assert.That(result.ProductId, Is.EqualTo(ProductIds.FrameBrass));
            Assert.That(result.ConfirmedEntitlements.HasValue, Is.False,
                "truth for the returned frame cannot confirm the requested coat");
            Assert.That(svc.IsUnlocked(EntitlementIds.FrameBrass), Is.True,
                "the authoritative account snapshot is still applied independently");
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False);
        }

        [Test]
        public void AdmittedZeroPromiseProduct_NeverProducesTransactionConfirmation()
        {
            var catalog = PurchaseCatalog.Parse(@"{
              ""schemaVersion"": 2,
              ""entitlements"": [],
              ""products"": [
                { ""id"": ""cm_consumable_no_durable_grant"", ""storeType"": ""consumable"",
                  ""display"": ""Token"", ""entitlements"": [] }
              ]
            }");
            Assert.That(catalog.TryGetProduct("cm_consumable_no_durable_grant", out _), Is.True,
                "the parser deliberately admits consumables without durable entitlements");
            var backend = new FakePurchaseBackend
            {
                GrantOnPurchase = catalog.EntitlementsFor,
                NextPurchaseConfirmedEntitlements =
                    new EntitlementSnapshot(true, Array.Empty<EntitlementGrant>())
            };
            var svc = new PurchaseService(catalog, backend);

            PurchaseResult result = default;
            svc.Purchase("cm_consumable_no_durable_grant", r => result = r);

            Assert.That(result.Outcome, Is.EqualTo(PurchaseOutcome.SuccessCandidate));
            Assert.That(result.ConfirmedEntitlements.HasValue, Is.False,
                "an empty promise set cannot pass confirmation vacuously");
        }

        // ---- the runtime locator ---------------------------------------------------------

        [Test]
        public void PurchaseRuntime_IsNeverNull_AndIsSafeBeforeAnythingIsInstalled()
        {
            PurchaseRuntime.ResetForTests();
            try
            {
                Assert.That(PurchaseRuntime.Current, Is.Not.Null);
                Assert.That(PurchaseRuntime.IsInstalled, Is.False);
                Assert.That(PurchaseRuntime.Current.IsUnlocked(EntitlementIds.OutfitConductor), Is.False);
                Assert.DoesNotThrow(() => PurchaseRuntime.Current.Refresh());
                Assert.DoesNotThrow(() => PurchaseRuntime.Current.Purchase("cm_x", _ => { }));

                var (svc, _, _) = PFixtures.Service();
                PurchaseRuntime.Install(svc);
                Assert.That(PurchaseRuntime.IsInstalled, Is.True);
                Assert.That(PurchaseRuntime.Current, Is.SameAs(svc));

                PurchaseRuntime.Install(null);
                Assert.That(PurchaseRuntime.Current, Is.SameAs(svc),
                    "a failed install must not blank out a working service");
            }
            finally
            {
                PurchaseRuntime.ResetForTests();
            }
        }

        // ---- the null backend's own contract ---------------------------------------------

        [Test]
        public void NullBackend_ReportsEntitlementsAsNonAuthoritative()
        {
            var backend = new NullPurchaseBackend();
            EntitlementSnapshot snapshot = default;
            backend.RefreshEntitlements(s => snapshot = s);

            Assert.That(snapshot.IsAuthoritative, Is.False,
                "this single flag is the difference between degrading and destroying purchases");
            Assert.That(snapshot.Grants, Is.Empty);
        }

        [Test]
        public void NullBackend_ToleratesNullCallbacks()
        {
            var backend = new NullPurchaseBackend();
            Assert.DoesNotThrow(() => backend.FetchProducts(null));
            Assert.DoesNotThrow(() => backend.Purchase("cm_x", null));
            Assert.DoesNotThrow(() => backend.Restore(null));
            Assert.DoesNotThrow(() => backend.RefreshEntitlements(null));
        }

        [Test]
        public void AttachingABackendLater_KeepsAdLeasesEarnedBeforeIt()
        {
            var svc = new PurchaseService(PFixtures.TinyCatalog());
            svc.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor);
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);

            // The SDK finishes configuring after boot and swaps itself in.
            svc.AttachBackend(new FakePurchaseBackend());
            svc.RefreshEntitlements();

            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True,
                "a late SDK arrival must not confiscate a reward already earned");
        }

        [Test]
        public void BackendBecomingReady_RefreshesOfferingsAndEntitlements()
        {
            var catalog = PFixtures.TinyCatalog();
            var backend = new FakePurchaseBackend
            {
                Availability = BackendAvailability.Initializing,
                GrantOnPurchase = catalog.EntitlementsFor
            };
            backend.WithProduct(ProductIds.OutfitConductor, "Conductor's Coat", "$1.99")
                .WithEntitlement(EntitlementIds.OutfitConductor);
            var svc = new PurchaseService(catalog);

            svc.AttachBackend(backend);
            Assert.That(svc.StoreProductCount, Is.EqualTo(0),
                "the store cannot be queried before the SDK has configured");

            backend.SignalReady();

            Assert.That(svc.StoreProductCount, Is.EqualTo(1),
                "the configure callback must trigger the first real offerings fetch");
            Assert.That(svc.TryGetPrice(ProductIds.OutfitConductor, out var price), Is.True);
            Assert.That(price.DisplayText, Is.EqualTo("$1.99"));
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True,
                "the same ready transition also restores existing ownership");
        }

        [Test]
        public void ReplacedBackend_CannotRefreshTheServiceWhenItsLateReadySignalArrives()
        {
            var first = new FakePurchaseBackend
                { Availability = BackendAvailability.Initializing };
            first.WithProduct(ProductIds.OutfitConductor, "Stale coat", "$99.99");
            var second = new FakePurchaseBackend
                { Availability = BackendAvailability.Initializing };
            second.WithProduct(ProductIds.OutfitConductor, "Current coat", "$1.99");
            var svc = new PurchaseService(PFixtures.TinyCatalog());

            svc.AttachBackend(first);
            svc.AttachBackend(second);
            first.SignalReady();

            Assert.That(svc.StoreProductCount, Is.EqualTo(0),
                "replacing a backend must detach the old readiness callback");

            second.SignalReady();
            Assert.That(svc.TryGetPrice(ProductIds.OutfitConductor, out var price), Is.True);
            Assert.That(price.DisplayText, Is.EqualTo("$1.99"));
        }

        [Test]
        public void DetachedBackend_LateReadyOfferingsCannotRepopulateTheClearedCache()
        {
            var first = new SingleSlotBackend();
            var second = new FakePurchaseBackend();
            var svc = new PurchaseService(PFixtures.TinyCatalog(), first);

            svc.Refresh();
            Assert.That(first.FetchProductsCalls, Is.EqualTo(1));
            svc.AttachBackend(second);
            Assert.That(svc.StoreProductCount, Is.EqualTo(0));

            first.CompleteProducts();

            Assert.That(svc.StoreProductCount, Is.EqualTo(0),
                "a detached backend is no longer the active offerings authority");
            Assert.That(svc.TryGetPrice(ProductIds.OutfitConductor, out _), Is.False);
        }

        [Test]
        public void AbaReattachedBackend_LateReadyOfferingsCannotRepopulateTheClearedCache()
        {
            var first = new SingleSlotBackend();
            var replacement = new SingleSlotBackend();
            var svc = new PurchaseService(PFixtures.TinyCatalog(), first);

            svc.Refresh();
            svc.AttachBackend(replacement);
            svc.AttachBackend(first);
            first.CompleteProducts();

            Assert.That(svc.StoreProductCount, Is.EqualTo(0),
                "identity alone is ABA-vulnerable; every attachment needs a new generation");
        }

        [Test]
        public void ReattachingTheSameBackend_InvalidatesItsAlreadyStartedOfferingsFetch()
        {
            var backend = new SingleSlotBackend();
            var svc = new PurchaseService(PFixtures.TinyCatalog(), backend);

            svc.Refresh();
            svc.AttachBackend(backend);
            backend.CompleteProducts();

            Assert.That(svc.StoreProductCount, Is.EqualTo(0),
                "even same-instance attachment is a new authority generation");
        }

        [Test]
        public void DetachedBackend_DirectPurchaseCallbackReturnsUnconfirmedAndAppliesNoTruth()
        {
            var first = new SingleSlotBackend
                { DeferPurchase = true, ReturnConfirmedSnapshot = true };
            var replacement = new SingleSlotBackend();
            var svc = new PurchaseService(PFixtures.TinyCatalog(), first);
            int callbacks = 0;
            PurchaseResult result = default;

            svc.Purchase(ProductIds.OutfitConductor, r => { callbacks++; result = r; });
            svc.AttachBackend(replacement);
            first.CompletePurchase();

            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(result.Outcome, Is.EqualTo(PurchaseOutcome.SuccessCandidate));
            Assert.That(result.ConfirmedEntitlements.HasValue, Is.False);
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False);
            Assert.That(replacement.RefreshEntitlementsCalls, Is.EqualTo(0));
        }

        [Test]
        public void AbaReattachedBackend_DirectPurchaseCallbackCannotApplyPriorGenerationTruth()
        {
            var first = new SingleSlotBackend
                { DeferPurchase = true, ReturnConfirmedSnapshot = true };
            var replacement = new SingleSlotBackend();
            var svc = new PurchaseService(PFixtures.TinyCatalog(), first);
            PurchaseResult result = default;

            svc.Purchase(ProductIds.OutfitConductor, r => result = r);
            svc.AttachBackend(replacement);
            svc.AttachBackend(first);
            first.CompletePurchase();

            Assert.That(result.ConfirmedEntitlements.HasValue, Is.False);
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False,
                "the same object returning after ABA is still an older authority generation");
            Assert.That(first.RefreshEntitlementsCalls, Is.EqualTo(0));
        }

        [Test]
        public void DetachedBackend_FallbackPurchaseNeverQueriesReplacementAuthority()
        {
            var first = new SingleSlotBackend { DeferPurchase = true };
            var replacement = new SingleSlotBackend { Owned = true };
            var svc = new PurchaseService(PFixtures.TinyCatalog(), first);
            int callbacks = 0;
            PurchaseResult result = default;

            svc.Purchase(ProductIds.OutfitConductor, r => { callbacks++; result = r; });
            svc.AttachBackend(replacement);
            first.CompletePurchase();

            Assert.That(callbacks, Is.EqualTo(1),
                "a stale purchase completes its caller without waiting on unrelated authority");
            Assert.That(result.ConfirmedEntitlements.HasValue, Is.False);
            Assert.That(replacement.RefreshEntitlementsCalls, Is.EqualTo(0),
                "detached transaction metadata must never initiate a replacement-backend query");
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False);
        }

        [Test]
        public void PurchaseFallbackStartedOnCurrentBackend_IsRejectedAfterAuthorityChanges()
        {
            var first = new SingleSlotBackend();
            var replacement = new SingleSlotBackend();
            var svc = new PurchaseService(PFixtures.TinyCatalog(), first);
            int callbacks = 0;
            PurchaseResult result = default;

            svc.Purchase(ProductIds.OutfitConductor, r => { callbacks++; result = r; });
            Assert.That(first.RefreshEntitlementsCalls, Is.EqualTo(1));
            Assert.That(callbacks, Is.EqualTo(0));

            svc.AttachBackend(replacement);
            first.CompleteEntitlements();

            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(result.ConfirmedEntitlements.HasValue, Is.False);
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False,
                "fallback CustomerInfo belongs to the backend generation that requested it");
            Assert.That(replacement.RefreshEntitlementsCalls, Is.EqualTo(0));
        }

        [Test]
        public void DetachedBackend_DirectCompletedRestoreBecomesTruthfulFailure()
        {
            var first = new SingleSlotBackend
                { DeferRestore = true, ReturnConfirmedSnapshot = true };
            var replacement = new SingleSlotBackend();
            var svc = new PurchaseService(PFixtures.TinyCatalog(), first);
            int callbacks = 0;
            RestoreResult result = default;

            svc.Restore(r => { callbacks++; result = r; });
            svc.AttachBackend(replacement);
            first.CompleteRestore();

            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(result.Outcome, Is.EqualTo(RestoreOutcome.Failure));
            Assert.That(result.DiagnosticMessage, Does.Contain("backend").IgnoreCase);
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False);
            Assert.That(replacement.RefreshEntitlementsCalls, Is.EqualTo(0));
        }

        [TestCase(RestoreOutcome.Failure)]
        [TestCase(RestoreOutcome.Unavailable)]
        public void DetachedBackend_NonCompletedRestoreStripsAllAuthorityMetadata(
            RestoreOutcome outcome)
        {
            var first = new FakePurchaseBackend
            {
                DeferCallbacks = true,
                NextRestoreOutcome = outcome,
                NextRestoreReportedCount = 7,
                NextRestoreDiagnostic = "store diagnostic",
                NextRestoreConfirmedEntitlements = new EntitlementSnapshot(true, new[]
                {
                    new EntitlementGrant(EntitlementIds.OutfitConductor, GrantSource.Store)
                })
            };
            var svc = new PurchaseService(PFixtures.TinyCatalog(), first);
            RestoreResult result = default;

            svc.Restore(r => result = r);
            svc.AttachBackend(new FakePurchaseBackend());
            first.DeferCallbacks = false;
            first.CompleteDeferred();

            Assert.That(result.Outcome, Is.EqualTo(outcome));
            Assert.That(result.RestoredEntitlementCount, Is.EqualTo(0));
            Assert.That(result.DiagnosticMessage, Is.EqualTo("store diagnostic"));
            Assert.That(result.ConfirmedEntitlements.HasValue, Is.False,
                "detached authority metadata is stripped for every stale restore outcome");
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False);
        }

        [Test]
        public void AbaReattachedBackend_DirectCompletedRestoreCannotApplyPriorGenerationTruth()
        {
            var first = new SingleSlotBackend
                { DeferRestore = true, ReturnConfirmedSnapshot = true };
            var replacement = new SingleSlotBackend();
            var svc = new PurchaseService(PFixtures.TinyCatalog(), first);
            RestoreResult result = default;

            svc.Restore(r => result = r);
            svc.AttachBackend(replacement);
            svc.AttachBackend(first);
            first.CompleteRestore();

            Assert.That(result.Outcome, Is.EqualTo(RestoreOutcome.Failure));
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False);
            Assert.That(first.RefreshEntitlementsCalls, Is.EqualTo(0));
        }

        [Test]
        public void DetachedBackend_FallbackCompletedRestoreNeverQueriesReplacementAuthority()
        {
            var first = new SingleSlotBackend { DeferRestore = true };
            var replacement = new SingleSlotBackend { Owned = true };
            var svc = new PurchaseService(PFixtures.TinyCatalog(), first);
            int callbacks = 0;
            RestoreResult result = default;

            svc.Restore(r => { callbacks++; result = r; });
            svc.AttachBackend(replacement);
            first.CompleteRestore();

            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(result.Outcome, Is.EqualTo(RestoreOutcome.Failure));
            Assert.That(replacement.RefreshEntitlementsCalls, Is.EqualTo(0));
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False);
        }

        [Test]
        public void RestoreFallbackStartedOnCurrentBackend_IsRejectedAfterAuthorityChanges()
        {
            var first = new SingleSlotBackend();
            var replacement = new SingleSlotBackend();
            var svc = new PurchaseService(PFixtures.TinyCatalog(), first);
            int callbacks = 0;
            RestoreResult result = default;

            svc.Restore(r => { callbacks++; result = r; });
            Assert.That(first.RefreshEntitlementsCalls, Is.EqualTo(1));
            Assert.That(callbacks, Is.EqualTo(0));

            svc.AttachBackend(replacement);
            first.CompleteEntitlements();

            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(result.Outcome, Is.EqualTo(RestoreOutcome.Failure));
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False);
            Assert.That(replacement.RefreshEntitlementsCalls, Is.EqualTo(0));
        }

        [Test]
        public void AbaReattachedBackend_LateEntitlementRefreshCannotReplaceCurrentLedger()
        {
            var first = new SingleSlotBackend { Owned = true };
            var replacement = new SingleSlotBackend();
            var svc = new PurchaseService(PFixtures.TinyCatalog(), first);
            int callbacks = 0;

            svc.RefreshEntitlements(() => callbacks++);
            svc.AttachBackend(replacement);
            svc.AttachBackend(first);
            first.CompleteEntitlements();

            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False,
                "a prior-generation CustomerInfo response is rejected even after ABA");
        }

        [Test]
        public void QueuedEntitlementRefresh_DoesNotMigrateToAReplacementBackend()
        {
            var first = new SingleSlotBackend();
            var replacement = new SingleSlotBackend { Owned = true };
            var svc = new PurchaseService(PFixtures.TinyCatalog(), first);
            int firstDone = 0, queuedDone = 0;

            svc.RefreshEntitlements(() => firstDone++);
            svc.RefreshEntitlements(() => queuedDone++);
            svc.AttachBackend(replacement);
            first.CompleteEntitlements();

            Assert.That(firstDone, Is.EqualTo(1));
            Assert.That(queuedDone, Is.EqualTo(1),
                "prior-authority queued work is rejected and completed, not migrated");
            Assert.That(replacement.RefreshEntitlementsCalls, Is.EqualTo(0));
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False);
        }

        [Test]
        public void EntitlementPump_ReservesTheNativeSlotWhileStaleCallbacksEnqueueCurrentWork()
        {
            var backend = new SingleSlotBackend();
            var svc = new PurchaseService(PFixtures.TinyCatalog(), backend);
            int oldInFlight = 0, staleOne = 0, staleTwo = 0, currentOne = 0, currentTwo = 0;

            svc.RefreshEntitlements(() => oldInFlight++);
            svc.RefreshEntitlements(() =>
            {
                staleOne++;
                svc.RefreshEntitlements(() => currentOne++);
                svc.RefreshEntitlements(() => currentTwo++);
            });
            svc.RefreshEntitlements(() => staleTwo++);
            svc.AttachBackend(backend);

            backend.CompleteEntitlements();

            Assert.That(oldInFlight, Is.EqualTo(1));
            Assert.That(staleOne, Is.EqualTo(1));
            Assert.That(staleTwo, Is.EqualTo(1));
            Assert.That(backend.RefreshEntitlementsCalls, Is.EqualTo(2),
                "stale callback reentrancy may start exactly one current native request");
            Assert.That(currentOne + currentTwo, Is.EqualTo(0));

            backend.CompleteEntitlements();
            Assert.That(currentOne + currentTwo, Is.EqualTo(1));
            Assert.That(backend.RefreshEntitlementsCalls, Is.EqualTo(3));

            backend.CompleteEntitlements();
            Assert.That(currentOne, Is.EqualTo(1));
            Assert.That(currentTwo, Is.EqualTo(1));
            Assert.That(backend.RefreshEntitlementsCalls, Is.EqualTo(3));
        }

        [Test]
        public void OverlappingRefreshes_AreSerializedAcrossTheSingleSlotSdkCallbacks()
        {
            var backend = new SingleSlotBackend();
            var svc = new PurchaseService(PFixtures.TinyCatalog(), backend);
            int first = 0, second = 0;

            svc.Refresh(() => first++);
            svc.Refresh(() => second++);

            Assert.That(backend.FetchProductsCalls, Is.EqualTo(1),
                "RevenueCat 9.9 stores one GetOfferings callback; overlap would orphan the first");
            Assert.That(backend.RefreshEntitlementsCalls, Is.EqualTo(1),
                "RevenueCat 9.9 stores one GetCustomerInfo callback; overlap would orphan the first");

            backend.CompleteProducts();
            backend.CompleteEntitlements();
            Assert.That(first, Is.EqualTo(1));
            Assert.That(second, Is.EqualTo(0));
            Assert.That(backend.FetchProductsCalls, Is.EqualTo(2));
            Assert.That(backend.RefreshEntitlementsCalls, Is.EqualTo(2));

            backend.CompleteProducts();
            backend.CompleteEntitlements();
            Assert.That(second, Is.EqualTo(1));
        }

        [Test]
        public void PurchaseConfirmation_WaitsBehindAnExistingEntitlementRefresh()
        {
            var backend = new SingleSlotBackend();
            var svc = new PurchaseService(PFixtures.TinyCatalog(), backend);
            int foregroundRefresh = 0, purchaseFinished = 0;

            svc.RefreshEntitlements(() => foregroundRefresh++);
            svc.Purchase(ProductIds.OutfitConductor, _ => purchaseFinished++);

            Assert.That(backend.RefreshEntitlementsCalls, Is.EqualTo(1),
                "purchase confirmation must queue, not overwrite a resume-time callback");
            Assert.That(purchaseFinished, Is.EqualTo(0));

            backend.CompleteEntitlements();
            Assert.That(foregroundRefresh, Is.EqualTo(1));
            Assert.That(purchaseFinished, Is.EqualTo(0));
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False,
                "the first request captured ownership before the purchase");
            Assert.That(backend.RefreshEntitlementsCalls, Is.EqualTo(2));

            backend.CompleteEntitlements();
            Assert.That(purchaseFinished, Is.EqualTo(1));
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);
        }

        [Test]
        public void RestoreConfirmation_WaitsBehindAnExistingEntitlementRefresh()
        {
            var backend = new SingleSlotBackend();
            var svc = new PurchaseService(PFixtures.TinyCatalog(), backend);
            int foregroundRefresh = 0, restoreFinished = 0;

            svc.RefreshEntitlements(() => foregroundRefresh++);
            svc.Restore(_ => restoreFinished++);

            Assert.That(backend.RefreshEntitlementsCalls, Is.EqualTo(1));
            Assert.That(restoreFinished, Is.EqualTo(0));

            backend.CompleteEntitlements();
            Assert.That(foregroundRefresh, Is.EqualTo(1));
            Assert.That(backend.RefreshEntitlementsCalls, Is.EqualTo(2));
            backend.CompleteEntitlements();

            Assert.That(restoreFinished, Is.EqualTo(1));
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);
        }

        [Test]
        public void PurchaseCallbackSnapshot_AppliesImmediatelyAndOutranksAnOlderRefresh()
        {
            var backend = new SingleSlotBackend { ReturnConfirmedSnapshot = true };
            var svc = new PurchaseService(PFixtures.TinyCatalog(), backend);
            int olderRefresh = 0, purchaseFinished = 0;

            svc.RefreshEntitlements(() => olderRefresh++);
            svc.Purchase(ProductIds.OutfitConductor, _ => purchaseFinished++);

            Assert.That(purchaseFinished, Is.EqualTo(1),
                "the native purchase callback already carried authoritative CustomerInfo");
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True,
                "the coat can paint on the purchase-return frame, not after a redundant request");
            Assert.That(backend.RefreshEntitlementsCalls, Is.EqualTo(1));

            backend.CompleteEntitlements();

            Assert.That(olderRefresh, Is.EqualTo(1));
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True,
                "a pre-purchase snapshot completing late cannot revoke the newer purchase truth");
        }

        [Test]
        public void LateTransactionUpdate_AppliesAndOutranksAnOlderRefresh()
        {
            var backend = new SingleSlotBackend();
            var svc = new PurchaseService(PFixtures.TinyCatalog(), backend);

            svc.RefreshEntitlements();
            backend.SignalTransactionUpdate(owned: true);

            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True,
                "a purchase callback arriving after its UI watchdog must still paint the coat");

            backend.CompleteEntitlements();
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True,
                "the refresh requested before the late purchase cannot revoke newer truth");
        }

        [Test]
        public void ReplacedBackend_CannotApplyALateTransactionUpdate()
        {
            var first = new SingleSlotBackend();
            var second = new SingleSlotBackend();
            var svc = new PurchaseService(PFixtures.TinyCatalog(), first);

            svc.AttachBackend(second);
            first.SignalTransactionUpdate(owned: true);
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False,
                "detached native callbacks cannot mutate the active customer's wardrobe");

            second.SignalTransactionUpdate(owned: true);
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);
        }

        [Test]
        public void EmptyConfirmedRestore_DoesNotCountAnActiveAdLeaseAsAPurchase()
        {
            var backend = new SingleSlotBackend
            {
                ReturnConfirmedSnapshot = true,
                GrantOnRestore = false
            };
            var svc = new PurchaseService(PFixtures.TinyCatalog(), backend);
            svc.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor);

            RestoreResult result = default;
            svc.Restore(r => result = r);

            Assert.That(result.Outcome, Is.EqualTo(RestoreOutcome.Completed));
            Assert.That(result.RestoredEntitlementCount, Is.EqualTo(0),
                "restore counts CustomerInfo grants, not source-blind wearable access");
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True,
                "the empty store snapshot must preserve the independently earned ad lease");
        }

        // ---- restore ---------------------------------------------------------------------

        [Test]
        public void Restore_UnlocksWhatWasPreviouslyBought_AndReportsTheCount()
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.RestoreGrants = new List<string>
                { EntitlementIds.OutfitConductor, EntitlementIds.FrameBrass };

            RestoreResult result = default;
            svc.Restore(r => result = r);

            Assert.That(result.Outcome, Is.EqualTo(RestoreOutcome.Completed));
            Assert.That(result.RestoredEntitlementCount, Is.EqualTo(2));
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);
            Assert.That(svc.IsUnlocked(EntitlementIds.FrameBrass), Is.True);
        }

        // "You have not bought anything" is a normal answer, not an error, and the UI must be
        // able to tell it apart from a failure.
        [Test]
        public void RestoringWithNothingToRestore_CompletesWithZero()
        {
            var (svc, _, _) = PFixtures.Service();

            RestoreResult result = default;
            svc.Restore(r => result = r);

            Assert.That(result.Outcome, Is.EqualTo(RestoreOutcome.Completed));
            Assert.That(result.RestoredEntitlementCount, Is.EqualTo(0));
        }

        [Test]
        public void AFailedRestore_ChangesNothing()
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.WithEntitlement(EntitlementIds.OutfitConductor);
            svc.RefreshEntitlements();

            backend.NextRestoreOutcome = RestoreOutcome.Failure;
            RestoreResult result = default;
            svc.Restore(r => result = r);

            Assert.That(result.Outcome, Is.EqualTo(RestoreOutcome.Failure));
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True,
                "a failed restore must not revoke what we already knew");
        }

        [Test]
        public void DoubleTappingRestore_IsRefusedWhileTheFirstIsInFlight()
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.DeferCallbacks = true;

            RestoreResult second = default;
            svc.Restore(_ => { });
            svc.Restore(r => second = r);

            Assert.That(second.Outcome, Is.EqualTo(RestoreOutcome.Busy));
            Assert.That(backend.RestoreCallCount, Is.EqualTo(1));
        }

        [Test]
        public void RestoringWhilePurchaseIsInFlight_IsRefusedBeforeTheStore()
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.DeferCallbacks = true;

            svc.Purchase(ProductIds.OutfitConductor, _ => { });
            RestoreResult restore = default;
            svc.Restore(r => restore = r);

            Assert.That(restore.Outcome, Is.EqualTo(RestoreOutcome.Busy));
            Assert.That(backend.RestoreCallCount, Is.EqualTo(0));
        }

        // Restore reports the count WE can see, so the number shown to the player matches the
        // number of cosmetics that actually just unlocked.
        [Test]
        public void RestoreCount_IgnoresEntitlementsOutsideOurCatalogue()
        {
            var (svc, backend, _) = PFixtures.Service();
            backend.RestoreGrants = new List<string>
                { EntitlementIds.OutfitConductor, "entitlement_from_another_app" };

            RestoreResult result = default;
            svc.Restore(r => result = r);

            Assert.That(result.RestoredEntitlementCount, Is.EqualTo(1));
        }

        // Models purchases-unity 9.9's native bridge: each operation has one callback field,
        // so issuing a second call before the first completes overwrites the first callback.
        private sealed class SingleSlotBackend : IPurchaseBackend,
            IPurchaseBackendTransactionUpdates
        {
            private Action _completeProducts;
            private Action _completePurchase;
            private Action _completeRestore;
            private Action _completeEntitlements;
            private bool _owned;

            public BackendAvailability Availability => BackendAvailability.Ready;
            public event Action<EntitlementSnapshot> TransactionEntitlementsConfirmed;
            public int FetchProductsCalls { get; private set; }
            public int PurchaseCalls { get; private set; }
            public int RestoreCalls { get; private set; }
            public int RefreshEntitlementsCalls { get; private set; }
            public bool ReturnConfirmedSnapshot { get; set; }
            public bool GrantOnRestore { get; set; } = true;
            public bool DeferPurchase { get; set; }
            public bool DeferRestore { get; set; }
            public bool Owned { get => _owned; set => _owned = value; }

            public void FetchProducts(Action<IReadOnlyList<StoreProductView>> onDone)
            {
                FetchProductsCalls++;
                _completeProducts = () => onDone?.Invoke(new[]
                {
                    new StoreProductView(ProductIds.OutfitConductor, "Conductor's Coat",
                        new LocalizedPrice("$1.99"))
                });
            }

            public void Purchase(string productId, Action<PurchaseResult> onDone)
            {
                PurchaseCalls++;
                _owned = true;
                void Complete() => onDone?.Invoke(new PurchaseResult(
                    PurchaseOutcome.SuccessCandidate, productId,
                    confirmedEntitlements: ReturnConfirmedSnapshot ? Snapshot(_owned) : null));
                if (DeferPurchase) _completePurchase = Complete;
                else Complete();
            }

            public void Restore(Action<RestoreResult> onDone)
            {
                RestoreCalls++;
                if (GrantOnRestore) _owned = true;
                void Complete() => onDone?.Invoke(new RestoreResult(RestoreOutcome.Completed,
                    confirmedEntitlements: ReturnConfirmedSnapshot ? Snapshot(_owned) : null));
                if (DeferRestore) _completeRestore = Complete;
                else Complete();
            }

            public void RefreshEntitlements(Action<EntitlementSnapshot> onDone)
            {
                RefreshEntitlementsCalls++;
                bool ownedWhenRequested = _owned;
                _completeEntitlements = () => onDone?.Invoke(Snapshot(ownedWhenRequested));
            }

            private static EntitlementSnapshot Snapshot(bool owned)
                => new EntitlementSnapshot(true,
                    owned
                        ? new[] { new EntitlementGrant(EntitlementIds.OutfitConductor, GrantSource.Store) }
                        : Array.Empty<EntitlementGrant>());

            public void CompleteProducts()
            {
                var callback = _completeProducts;
                _completeProducts = null;
                callback?.Invoke();
            }

            public void CompletePurchase()
            {
                var callback = _completePurchase;
                _completePurchase = null;
                callback?.Invoke();
            }

            public void CompleteRestore()
            {
                var callback = _completeRestore;
                _completeRestore = null;
                callback?.Invoke();
            }

            public void CompleteEntitlements()
            {
                var callback = _completeEntitlements;
                _completeEntitlements = null;
                callback?.Invoke();
            }

            public void SignalTransactionUpdate(bool owned)
                => TransactionEntitlementsConfirmed?.Invoke(Snapshot(owned));
        }
    }
}
