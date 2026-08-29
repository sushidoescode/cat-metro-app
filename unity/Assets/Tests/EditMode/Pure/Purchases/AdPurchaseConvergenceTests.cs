using CatMetro.Services.Purchases;
using NUnit.Framework;

namespace CatMetro.Tests.Purchases
{
    // The requirement these exist for: a rewarded ad and a paid purchase must converge on ONE
    // code path, so that when the ads lane lands it does not build a second entitlement system
    // beside this one.
    //
    // The strongest available proof in C# is a negative one: there is no API on PurchaseService
    // or EntitlementLedger that reveals HOW an entitlement was obtained. IsUnlocked is the only
    // question, and it cannot be asked in a way that distinguishes the two. These tests pin the
    // behavioural half of that claim.
    public sealed class AdPurchaseConvergenceTests
    {
        [Test]
        public void MissingLeasePersistence_RefusesTheGrantWithoutChangingTheLedger()
        {
            var svc = new PurchaseService(PFixtures.TinyCatalog(), clock: () => 1_000L);

            Assert.That(svc.CanOfferAdFor(EntitlementIds.OutfitConductor), Is.True,
                "offer worthiness remains a catalogue and ledger question");
            Assert.That(svc.CanPersistRewardedAdGrants, Is.False);
            Assert.That(svc.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor),
                Is.EqualTo(AdGrantOutcome.PersistenceFailed));
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False);
            Assert.That(svc.Ledger.ExportLeases(), Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RefusingOrThrowingLeasePersistence_LeavesExistingLeasesUnchanged(bool throws)
        {
            const long now = 1_000L;
            var ledger = new EntitlementLedger();
            ledger.ImportLeases(new[]
            {
                new EntitlementGrant(EntitlementIds.OutfitConductor, GrantSource.RewardedAd, now + 10L)
            }, now);
            var svc = new PurchaseService(PFixtures.TinyCatalog(), clock: () => now, ledger: ledger);
            svc.AttachLeasePersistence(new PFixtures.RecordingLeasePersistence
            {
                Accept = false,
                ThrowOnPersist = throws,
            });
            var before = ledger.ExportLeases();

            Assert.That(svc.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor),
                Is.EqualTo(AdGrantOutcome.PersistenceFailed));
            CollectionAssert.AreEqual(before, ledger.ExportLeases());
        }

        [Test]
        public void SuccessfulLeasePersistence_RecordsTheCandidateBeforeLedgerChangedPublishesUnlock()
        {
            var ledger = new EntitlementLedger();
            var persistence = new PFixtures.RecordingLeasePersistence();
            bool candidateRecorded = false;
            bool changed = false;
            persistence.OnPersist = leases => candidateRecorded = leases.Count == 1;
            ledger.Changed += () =>
            {
                changed = true;
                Assert.That(candidateRecorded, Is.True,
                    "durable candidate bytes must exist before observers can consume the unlock");
            };
            var svc = new PurchaseService(PFixtures.TinyCatalog(), clock: () => 1_000L, ledger: ledger);
            svc.AttachLeasePersistence(persistence);

            Assert.That(svc.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor),
                Is.EqualTo(AdGrantOutcome.Granted));
            Assert.That(changed, Is.True);
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);
        }

        [Test]
        public void SuccessfulLeasePersistence_NeverSerializesPaidOrPromotionalGrants()
        {
            var ledger = new EntitlementLedger();
            ledger.ReplaceStoreGrants(new[]
            {
                new EntitlementGrant("paid", GrantSource.Store),
                new EntitlementGrant("promo", GrantSource.Promotional),
            });
            var persistence = new PFixtures.RecordingLeasePersistence();
            var svc = new PurchaseService(PFixtures.TinyCatalog(), clock: () => 1_000L, ledger: ledger);
            svc.AttachLeasePersistence(persistence);

            Assert.That(svc.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor),
                Is.EqualTo(AdGrantOutcome.Granted));
            Assert.That(persistence.LastLeases, Has.Count.EqualTo(1));
            Assert.That(persistence.LastLeases[0].EntitlementId,
                Is.EqualTo(EntitlementIds.OutfitConductor));
            Assert.That(persistence.LastLeases[0].Source, Is.EqualTo(GrantSource.RewardedAd));
        }

        [Test]
        public void RestoredRewardedLease_KeepsItsSavedAbsoluteExpiry()
        {
            var clock = new PFixtures.Clock { Now = 5_000L };
            var svc = new PurchaseService(PFixtures.TinyCatalog(), clock: clock.Fn);

            svc.RestoreRewardedAdLeases(new[]
            {
                new EntitlementGrant(EntitlementIds.OutfitConductor, GrantSource.RewardedAd, 5_123L)
            });

            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);
            Assert.That(svc.SecondsUntilExpiry(EntitlementIds.OutfitConductor), Is.EqualTo(123L));
            clock.Advance(123L);
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False,
                "restore must not turn a saved absolute expiry into a fresh 24-hour lease");
        }

        [Test]
        public void RestoreRewardedAdLeases_RejectsInvalidRowsBeforeTheyReachTheLedger()
        {
            const long now = 5_000L;
            var svc = new PurchaseService(PFixtures.TinyCatalog(), clock: () => now);

            svc.RestoreRewardedAdLeases(new[]
            {
                new EntitlementGrant("not_in_catalogue", GrantSource.RewardedAd, now + 10L),
                new EntitlementGrant(EntitlementIds.OutfitConductor, GrantSource.Store, now + 10L),
                new EntitlementGrant(EntitlementIds.OutfitConductor, GrantSource.RewardedAd, now),
                new EntitlementGrant(EntitlementIds.OutfitConductor, GrantSource.RewardedAd),
                new EntitlementGrant(EntitlementIds.FrameBrass, GrantSource.RewardedAd, now + 10L),
            });

            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False);
            Assert.That(svc.IsUnlocked(EntitlementIds.FrameBrass), Is.False);
            Assert.That(svc.Ledger.ExportLeases(), Is.Empty);
        }

        [Test]
        public void APaidUnlockAndAnAdUnlock_AreIndistinguishableToTheGame()
        {
            var (paid, paidBackend, _) = PFixtures.Service();
            var (watched, _, _) = PFixtures.Service();

            // Path one: the player buys the coat.
            paid.Purchase(ProductIds.OutfitConductor, _ => { });
            // Path two: the player watches an ad for it.
            watched.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor);

            Assert.That(paid.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);
            Assert.That(watched.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);
            Assert.That(paidBackend.PurchaseCallCount, Is.EqualTo(1));

            // Same question, same answer, from two entirely different origins. Every consumer —
            // the wardrobe screen, the cat renderer — branches on exactly this and nothing else.
            Assert.That(paid.IsUnlocked(EntitlementIds.OutfitConductor),
                Is.EqualTo(watched.IsUnlocked(EntitlementIds.OutfitConductor)));
        }

        // The one place they legitimately differ, and it is visible only as a countdown, never
        // as a different unlock check.
        [Test]
        public void OnlyTheLeaseExpires_ThePurchaseDoesNot()
        {
            var (paid, _, paidClock) = PFixtures.Service();
            var (watched, _, watchedClock) = PFixtures.Service();

            paid.Purchase(ProductIds.OutfitConductor, _ => { });
            watched.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor);

            // TinyCatalog leases the coat for 3600s.
            paidClock.Advance(7200);
            watchedClock.Advance(7200);
            paid.PruneExpiredLeases();
            watched.PruneExpiredLeases();

            Assert.That(paid.IsUnlocked(EntitlementIds.OutfitConductor), Is.True,
                "bought is bought");
            Assert.That(watched.IsUnlocked(EntitlementIds.OutfitConductor), Is.False,
                "lent is lent");
        }

        [Test]
        public void LeaseLengthComesFromTheCatalogue_NotFromTheCaller()
        {
            var (svc, _, clock) = PFixtures.Service();

            Assert.That(svc.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor),
                Is.EqualTo(AdGrantOutcome.Granted));

            // 3600 in TinyCatalogJson. There is no overload that lets an ad integration pass its
            // own duration, because how long the coat is lent for is a balance decision that
            // belongs in data, not in whichever ad network happens to be wired.
            Assert.That(svc.SecondsUntilExpiry(EntitlementIds.OutfitConductor), Is.EqualTo(3600));
            clock.Advance(3599);
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);
            clock.Advance(1);
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.False);
        }

        // The refusals the ad lane needs in order to decide whether showing an ad is honest.
        [Test]
        public void AnEntitlementMarkedNotAdGrantable_CannotBeWonByWatchingAnAd()
        {
            var (svc, _, _) = PFixtures.Service();

            // frame_brass has adLeaseSeconds 0 — premium frames are bought, never lent.
            Assert.That(svc.GrantRewardedAdEntitlement(EntitlementIds.FrameBrass),
                Is.EqualTo(AdGrantOutcome.NotAdGrantable));
            Assert.That(svc.IsUnlocked(EntitlementIds.FrameBrass), Is.False);
            Assert.That(svc.CanOfferAdFor(EntitlementIds.FrameBrass), Is.False);
        }

        [Test]
        public void AnUnknownEntitlement_IsRefusedRatherThanInvented()
        {
            var (svc, _, _) = PFixtures.Service();
            Assert.That(svc.GrantRewardedAdEntitlement("outfit_that_does_not_exist"),
                Is.EqualTo(AdGrantOutcome.UnknownEntitlement));
            Assert.That(svc.GrantRewardedAdEntitlement(null),
                Is.EqualTo(AdGrantOutcome.UnknownEntitlement));
        }

        // Showing an ad for something the player already owns is taking thirty seconds of their
        // life for nothing. CanOfferAdFor exists so the ad is never loaded in the first place.
        [Test]
        public void AlreadyOwned_DeclinesTheAdBeforeItIsLoaded()
        {
            var (svc, _, _) = PFixtures.Service();
            Assert.That(svc.CanOfferAdFor(EntitlementIds.OutfitConductor), Is.True);

            svc.Purchase(ProductIds.OutfitConductor, _ => { });

            Assert.That(svc.CanOfferAdFor(EntitlementIds.OutfitConductor), Is.False);
            Assert.That(svc.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor),
                Is.EqualTo(AdGrantOutcome.AlreadyUnlocked));
        }

        [Test]
        public void ALongerRewardedLease_CanExtendAShortTimedStoreGrant()
        {
            long now = 100L;
            var ledger = new EntitlementLedger();
            ledger.ReplaceStoreGrants(new[]
            {
                new EntitlementGrant(EntitlementIds.OutfitConductor, GrantSource.Store, 200L)
            });
            var svc = new PurchaseService(PFixtures.TinyCatalog(), clock: () => now,
                ledger: ledger);
            svc.AttachLeasePersistence(new PFixtures.RecordingLeasePersistence());

            Assert.That(svc.CanOfferAdFor(EntitlementIds.OutfitConductor), Is.True,
                "a 3600-second reward is honest when current timed access has only 100 seconds left");
            Assert.That(svc.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor),
                Is.EqualTo(AdGrantOutcome.Granted),
                "TASK 11's only grant API must reach the ledger's supported extension path");
            Assert.That(svc.SecondsUntilExpiry(EntitlementIds.OutfitConductor), Is.EqualTo(3600),
                "the source-blind countdown reports the longest active access limb");

            now = 300L;
            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True,
                "the shared entitlement remains active after the shorter store grant expires");
            CollectionAssert.Contains(ledger.ActiveEntitlements(now),
                EntitlementIds.OutfitConductor,
                "an expired store limb must not hide the still-active lease from aggregate reads");
        }

        [Test]
        public void WatchingASecondAd_WhileALeaseIsRunning_IsNotOffered()
        {
            var (svc, _, clock) = PFixtures.Service();
            svc.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor);

            Assert.That(svc.CanOfferAdFor(EntitlementIds.OutfitConductor), Is.False);

            clock.Advance(3600);
            svc.PruneExpiredLeases();
            Assert.That(svc.CanOfferAdFor(EntitlementIds.OutfitConductor), Is.True,
                "once the lease lapses the offer is honest again");
        }

        // Buying something you are currently borrowing must upgrade you permanently — the lease
        // becoming irrelevant rather than the purchase being swallowed.
        [Test]
        public void BuyingWhatYouAreBorrowing_UpgradesToPermanent()
        {
            var (svc, _, clock) = PFixtures.Service();
            svc.GrantRewardedAdEntitlement(EntitlementIds.OutfitConductor);
            svc.Purchase(ProductIds.OutfitConductor, _ => { });

            clock.Advance(100000); // long past any lease
            svc.PruneExpiredLeases();

            Assert.That(svc.IsUnlocked(EntitlementIds.OutfitConductor), Is.True);
            Assert.That(svc.SecondsUntilExpiry(EntitlementIds.OutfitConductor), Is.EqualTo(0),
                "no countdown chip on something you own");
        }

        // ---- placements are checked against the same entitlement table -------------------

        [Test]
        public void APlacementTargetingAnUndeclaredEntitlement_IsDropped()
        {
            var placements = RewardedPlacementCatalog.Parse(@"{
              ""placements"": [ { ""id"": ""p"", ""entitlement"": ""outfit_ghost"", ""enabled"": true } ]
            }", PFixtures.TinyCatalog());

            Assert.That(placements.Placements, Is.Empty);
            Assert.That(placements.TryGet("p", out _), Is.False);
        }

        // The authoring mistake that would show an ad and then grant nothing.
        [Test]
        public void APlacementTargetingANonAdGrantableEntitlement_IsDropped()
        {
            var placements = RewardedPlacementCatalog.Parse(@"{
              ""placements"": [ { ""id"": ""p"", ""entitlement"": ""frame_brass"", ""enabled"": true } ]
            }", PFixtures.TinyCatalog());

            Assert.That(placements.Placements, Is.Empty);
            Assert.That(placements.Problems.Count, Is.EqualTo(1));
            Assert.That(placements.Problems[0], Does.Contain("not ad-grantable"));
        }

        [Test]
        public void PlacementParsing_NeverThrows()
        {
            foreach (var input in new[] { null, "", "not json", "[]", "{}" })
            {
                var p = RewardedPlacementCatalog.Parse(input, PFixtures.TinyCatalog());
                Assert.That(p.Placements, Is.Empty, "input was: " + (input ?? "null"));
                Assert.That(p.Problems, Is.Not.Empty, "input was: " + (input ?? "null"));
            }

            // And a null product catalogue must not throw either — it just drops everything,
            // because nothing can be cross-checked.
            Assert.DoesNotThrow(() =>
                RewardedPlacementCatalog.Parse(PFixtures.ShippedPlacementsJson(), null));
        }
    }
}
