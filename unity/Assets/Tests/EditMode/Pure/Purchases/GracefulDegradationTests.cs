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
    }
}
