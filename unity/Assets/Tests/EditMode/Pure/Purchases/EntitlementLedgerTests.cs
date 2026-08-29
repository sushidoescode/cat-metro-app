using System;
using CatMetro.Services.Purchases;
using NUnit.Framework;

namespace CatMetro.Tests.Purchases
{
    // The ledger is where a refund becomes real and where an ad reward survives. Both of those
    // depend on store grants and ad leases having DIFFERENT write semantics while sharing one
    // read. These tests pin that asymmetry, because collapsing it in either direction is a bug
    // that only shows up in production:
    //   merge store grants instead of replacing -> refunds never take effect
    //   let a store refresh clear leases        -> ad rewards vanish on foreground
    public sealed class EntitlementLedgerTests
    {
        private static EntitlementGrant Owned(string id) => new EntitlementGrant(id, GrantSource.Store);

        [Test]
        public void StoreGrants_AreReplacedWholesale_SoARefundActuallyRevokes()
        {
            var ledger = new EntitlementLedger();
            ledger.ReplaceStoreGrants(new[] { Owned("outfit_conductor"), Owned("frame_brass") });
            Assert.That(ledger.IsActive("outfit_conductor", 100), Is.True);

            // The next CustomerInfo no longer lists the coat — a refund or chargeback.
            ledger.ReplaceStoreGrants(new[] { Owned("frame_brass") });

            Assert.That(ledger.IsActive("outfit_conductor", 100), Is.False,
                "an entitlement missing from the new snapshot is GONE; merging would make refunds unenforceable");
            Assert.That(ledger.IsActive("frame_brass", 100), Is.True);
        }

        [Test]
        public void AdLease_SurvivesAStoreRefresh_ThatHasNeverHeardOfIt()
        {
            var ledger = new EntitlementLedger();
            Assert.That(ledger.GrantLease("outfit_conductor", expiresAtUnixSeconds: 500, nowUnixSeconds: 100),
                Is.True);

            // RevenueCat reports the truth it knows: this customer has bought nothing.
            ledger.ReplaceStoreGrants(new EntitlementGrant[0]);

            Assert.That(ledger.IsActive("outfit_conductor", 100), Is.True,
                "a CustomerInfo refresh must not wipe a reward earned thirty seconds ago");
        }

        [Test]
        public void Lease_ExpiresOnTheClock()
        {
            var ledger = new EntitlementLedger();
            ledger.GrantLease("outfit_conductor", expiresAtUnixSeconds: 500, nowUnixSeconds: 100);

            Assert.That(ledger.IsActive("outfit_conductor", 499), Is.True);
            Assert.That(ledger.IsActive("outfit_conductor", 500), Is.False, "expiry is exclusive");
            Assert.That(ledger.IsActive("outfit_conductor", 501), Is.False);
        }

        [Test]
        public void SecondsUntilExpiry_CountsDown_AndIsZeroForSomethingOwned()
        {
            var ledger = new EntitlementLedger();
            ledger.GrantLease("outfit_conductor", 500, 100);
            Assert.That(ledger.SecondsUntilExpiry("outfit_conductor", 100), Is.EqualTo(400));
            Assert.That(ledger.SecondsUntilExpiry("outfit_conductor", 450), Is.EqualTo(50));
            Assert.That(ledger.SecondsUntilExpiry("outfit_conductor", 500), Is.EqualTo(0));

            // Buying it outright ends the countdown — an owned thing has no timer on it.
            ledger.ReplaceStoreGrants(new[] { Owned("outfit_conductor") });
            Assert.That(ledger.SecondsUntilExpiry("outfit_conductor", 200), Is.EqualTo(0));
            Assert.That(ledger.IsActive("outfit_conductor", 200), Is.True);
        }

        [Test]
        public void OwningSomething_RefusesAPointlessLease()
        {
            var ledger = new EntitlementLedger();
            ledger.ReplaceStoreGrants(new[] { Owned("outfit_conductor") });

            Assert.That(ledger.GrantLease("outfit_conductor", 500, 100), Is.False,
                "false lets the ad lane decline to show an ad that would sell the player nothing");
            Assert.That(ledger.IsActive("outfit_conductor", 100), Is.True);
        }

        [Test]
        public void ExtendingALease_OnlyEverMovesTheExpiryForward()
        {
            var ledger = new EntitlementLedger();
            ledger.GrantLease("outfit_conductor", 500, 100);

            Assert.That(ledger.GrantLease("outfit_conductor", 300, 100), Is.False,
                "a shorter lease arriving late must not cut the existing one short");
            Assert.That(ledger.SecondsUntilExpiry("outfit_conductor", 100), Is.EqualTo(400));

            Assert.That(ledger.GrantLease("outfit_conductor", 900, 100), Is.True);
            Assert.That(ledger.SecondsUntilExpiry("outfit_conductor", 100), Is.EqualTo(800));
        }

        [Test]
        public void AlreadyExpiredLease_IsRefusedRatherThanStored()
        {
            var ledger = new EntitlementLedger();
            Assert.That(ledger.GrantLease("outfit_conductor", expiresAtUnixSeconds: 100, nowUnixSeconds: 100),
                Is.False, "a zero-length lease is a clock-skew bug, not a grant");
            Assert.That(ledger.GrantLease("outfit_conductor", 50, 100), Is.False);
            Assert.That(ledger.IsActive("outfit_conductor", 100), Is.False);
        }

        // ---- change notification ---------------------------------------------------------

        [Test]
        public void ThrowingChangedObserver_DoesNotEscapeOrBlockOthers_AfterGrantLease()
        {
            var ledger = new EntitlementLedger();
            int observed = 0;
            ledger.Changed += () => throw new InvalidOperationException("injected observer fault");
            ledger.Changed += () => observed++;
            bool granted = false;

            Assert.DoesNotThrow(() => granted = ledger.GrantLease("outfit_conductor", 500, 100));

            Assert.That(granted, Is.True);
            Assert.That(ledger.IsActive("outfit_conductor", 100), Is.True);
            Assert.That(observed, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingChangedObserver_DoesNotEscapeOrBlockOthers_AfterReplaceStoreGrants()
        {
            var ledger = new EntitlementLedger();
            int observed = 0;
            ledger.Changed += () => throw new InvalidOperationException("injected observer fault");
            ledger.Changed += () => observed++;

            Assert.DoesNotThrow(() => ledger.ReplaceStoreGrants(new[] { Owned("outfit_conductor") }));

            Assert.That(ledger.IsActive("outfit_conductor", 100), Is.True);
            Assert.That(observed, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingChangedObserver_DoesNotEscapeOrBlockOthers_AfterPruneExpired()
        {
            var ledger = new EntitlementLedger();
            ledger.GrantLease("outfit_conductor", 500, 100);
            int observed = 0;
            ledger.Changed += () => throw new InvalidOperationException("injected observer fault");
            ledger.Changed += () => observed++;
            bool pruned = false;

            Assert.DoesNotThrow(() => pruned = ledger.PruneExpired(500));

            Assert.That(pruned, Is.True);
            Assert.That(ledger.IsActive("outfit_conductor", 500), Is.False);
            Assert.That(observed, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingChangedObserver_DoesNotEscapeOrBlockOthers_AfterImportLeases()
        {
            var ledger = new EntitlementLedger();
            int observed = 0;
            ledger.Changed += () => throw new InvalidOperationException("injected observer fault");
            ledger.Changed += () => observed++;

            Assert.DoesNotThrow(() => ledger.ImportLeases(new[]
            {
                new EntitlementGrant("outfit_conductor", GrantSource.RewardedAd, 500)
            }, 100));

            Assert.That(ledger.IsActive("outfit_conductor", 100), Is.True);
            Assert.That(observed, Is.EqualTo(1));
        }

        [Test]
        public void RedeliveringIdenticalCustomerInfo_DoesNotRaiseChanged()
        {
            var ledger = new EntitlementLedger();
            int changes = 0;
            ledger.Changed += () => changes++;

            ledger.ReplaceStoreGrants(new[] { Owned("outfit_conductor") });
            Assert.That(changes, Is.EqualTo(1));

            // RevenueCat re-delivers CustomerInfo on every foreground. If that raised Changed,
            // the wardrobe screen would rebuild itself every time the player alt-tabs.
            ledger.ReplaceStoreGrants(new[] { Owned("outfit_conductor") });
            Assert.That(changes, Is.EqualTo(1), "an identical snapshot is a no-op");

            ledger.ReplaceStoreGrants(new[] { Owned("outfit_conductor"), Owned("frame_brass") });
            Assert.That(changes, Is.EqualTo(2), "a real change does raise it");
        }

        [Test]
        public void PruneExpired_RemovesDeadLeases_AndOnlyThenRaisesChanged()
        {
            var ledger = new EntitlementLedger();
            ledger.GrantLease("outfit_conductor", 500, 100);
            int changes = 0;
            ledger.Changed += () => changes++;

            Assert.That(ledger.PruneExpired(200), Is.False, "nothing has expired yet");
            Assert.That(changes, Is.EqualTo(0));

            Assert.That(ledger.PruneExpired(600), Is.True);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(ledger.IsActive("outfit_conductor", 600), Is.False);
        }

        // ---- reads ------------------------------------------------------------------------

        [Test]
        public void ActiveEntitlements_MergesBothSources_WithoutDuplicates_Deterministically()
        {
            var ledger = new EntitlementLedger();
            ledger.ReplaceStoreGrants(new[] { Owned("frame_brass"), Owned("supporter") });
            ledger.GrantLease("outfit_conductor", 500, 100);
            ledger.GrantLease("frame_brass", 500, 100); // refused: already owned

            Assert.That(ledger.ActiveEntitlements(100),
                Is.EqualTo(new[] { "frame_brass", "outfit_conductor", "supporter" }),
                "sorted, so tests and UI ordering are stable");
        }

        [Test]
        public void NullAndEmptyIds_AreFalse_NotAThrow()
        {
            var ledger = new EntitlementLedger();
            Assert.That(ledger.IsActive(null, 100), Is.False);
            Assert.That(ledger.IsActive("", 100), Is.False);
            Assert.That(ledger.GrantLease(null, 500, 100), Is.False);
            Assert.That(ledger.SecondsUntilExpiry(null, 100), Is.EqualTo(0));
            Assert.DoesNotThrow(() => ledger.ReplaceStoreGrants(null));
        }

        // ---- persistence seam --------------------------------------------------------------

        // A player who watches a thirty-second ad and then backgrounds the game has been robbed
        // if the lease does not come back.
        [Test]
        public void Leases_RoundTripThroughExportImport()
        {
            var a = new EntitlementLedger();
            a.GrantLease("outfit_conductor", 500, 100);

            var b = new EntitlementLedger();
            b.ImportLeases(a.ExportLeases(), nowUnixSeconds: 200);

            Assert.That(b.IsActive("outfit_conductor", 200), Is.True);
            Assert.That(b.SecondsUntilExpiry("outfit_conductor", 200), Is.EqualTo(300),
                "the lease resumes where it was, it does not restart");
        }

        [Test]
        public void ImportingADeadLease_DoesNotResurrectIt()
        {
            var a = new EntitlementLedger();
            a.GrantLease("outfit_conductor", 500, 100);

            var b = new EntitlementLedger();
            b.ImportLeases(a.ExportLeases(), nowUnixSeconds: 900);

            Assert.That(b.IsActive("outfit_conductor", 900), Is.False);
        }

        // Store grants are deliberately NOT exported: they come back from RevenueCat on every
        // launch, and a locally cached "you own this" is exactly what an attacker edits.
        [Test]
        public void ExportLeases_NeverIncludesStoreGrants()
        {
            var ledger = new EntitlementLedger();
            ledger.ReplaceStoreGrants(new[] { Owned("frame_brass") });
            ledger.GrantLease("outfit_conductor", 500, 100);

            var exported = ledger.ExportLeases();
            Assert.That(exported.Count, Is.EqualTo(1));
            Assert.That(exported[0].EntitlementId, Is.EqualTo("outfit_conductor"));
            Assert.That(exported[0].Source, Is.EqualTo(GrantSource.RewardedAd));
        }

        // ---- timed STORE grants ------------------------------------------------------------
        //
        // RevenueCat's own Ad Monetization grants a time-limited entitlement server-side (AdMob
        // server-side verification) and delivers it through CustomerInfo, with an
        // EntitlementInfo.ExpirationDate. So a "store" grant is not always permanent, and the
        // ledger has to count one down exactly like a locally granted lease. A subscription
        // behaves the same way. Getting this wrong means a 30-minute ad unlock never lapses.

        [Test]
        public void ATimedStoreGrant_LapsesOnItsOwnExpiry()
        {
            var ledger = new EntitlementLedger();
            ledger.ReplaceStoreGrants(new[]
                { new EntitlementGrant("outfit_conductor", GrantSource.Store, expiresAtUnixSeconds: 500) });

            Assert.That(ledger.IsActive("outfit_conductor", 400), Is.True);
            Assert.That(ledger.SecondsUntilExpiry("outfit_conductor", 400), Is.EqualTo(100),
                "a timed store grant is a countdown, and the UI must be able to show it");
            Assert.That(ledger.IsActive("outfit_conductor", 500), Is.False,
                "RevenueCat does not push an expiry event; the local clock has to notice");
        }

        [Test]
        public void PruneExpired_AlsoDropsTimedStoreGrants_AndNotifies()
        {
            var ledger = new EntitlementLedger();
            ledger.ReplaceStoreGrants(new[]
                { new EntitlementGrant("outfit_conductor", GrantSource.Store, 500) });
            int changes = 0;
            ledger.Changed += () => changes++;

            Assert.That(ledger.PruneExpired(400), Is.False);
            Assert.That(ledger.PruneExpired(600), Is.True,
                "a player can sit on the wardrobe screen while a timed grant runs out");
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void APermanentPurchase_NeverShowsACountdown()
        {
            var ledger = new EntitlementLedger();
            ledger.ReplaceStoreGrants(new[] { Owned("outfit_conductor") });
            Assert.That(ledger.SecondsUntilExpiry("outfit_conductor", 400), Is.EqualTo(0));
            Assert.That(ledger.PruneExpired(long.MaxValue - 1), Is.False, "and never lapses");
        }

        // A longer local lease on top of a short RevenueCat ad grant is a real improvement, so
        // it must be accepted — unlike a lease on top of something owned outright.
        [Test]
        public void ALongerLease_IsAcceptedOverAShorterTimedStoreGrant()
        {
            var ledger = new EntitlementLedger();
            ledger.ReplaceStoreGrants(new[]
                { new EntitlementGrant("outfit_conductor", GrantSource.Store, 500) });

            Assert.That(ledger.GrantLease("outfit_conductor", 400, 100), Is.False,
                "a shorter lease adds nothing");
            Assert.That(ledger.GrantLease("outfit_conductor", 900, 100), Is.True,
                "a longer one does");
            Assert.That(ledger.IsActive("outfit_conductor", 700), Is.True,
                "and it keeps the cosmetic alive past the store grant's expiry");
        }

        [Test]
        public void NoLease_IsAcceptedOverAPermanentGrant_HoweverLong()
        {
            var ledger = new EntitlementLedger();
            ledger.ReplaceStoreGrants(new[] { Owned("outfit_conductor") });
            Assert.That(ledger.GrantLease("outfit_conductor", long.MaxValue, 100), Is.False);
        }

        // A promotional grant from the RevenueCat dashboard arrives down the same CustomerInfo
        // channel as a purchase, and must behave identically — including being revocable.
        [Test]
        public void PromotionalGrants_BehaveExactlyLikePurchasedOnes()
        {
            var ledger = new EntitlementLedger();
            ledger.ReplaceStoreGrants(new[]
                { new EntitlementGrant("outfit_conductor", GrantSource.Promotional) });

            Assert.That(ledger.IsActive("outfit_conductor", 100), Is.True);
            ledger.ReplaceStoreGrants(new EntitlementGrant[0]);
            Assert.That(ledger.IsActive("outfit_conductor", 100), Is.False);
        }
    }
}
