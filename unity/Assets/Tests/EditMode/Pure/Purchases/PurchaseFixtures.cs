using System;
using System.Collections.Generic;
using System.IO;
using CatMetro.Services.Purchases;

namespace CatMetro.Tests.Purchases
{
    // A scriptable IPurchaseBackend. Everything is synchronous — the real SDK delivers its
    // callbacks on the Unity main thread, so tests that assert immediately after a call are
    // testing the same ordering the device sees, without a coroutine or a WaitUntil.
    public sealed class FakePurchaseBackend : IPurchaseBackend, IPurchaseBackendReadiness
    {
        private readonly List<StoreProductView> _products = new List<StoreProductView>();
        private readonly List<EntitlementGrant> _entitlements = new List<EntitlementGrant>();

        public BackendAvailability Availability { get; set; } = BackendAvailability.Ready;
        public event Action Ready;

        // When false, RefreshEntitlements reports a NON-authoritative snapshot — the offline /
        // unreachable case. This is the flag the whole degradation story turns on.
        public bool EntitlementsAreAuthoritative { get; set; } = true;

        public PurchaseOutcome NextPurchaseOutcome { get; set; } = PurchaseOutcome.SuccessCandidate;
        public RestoreOutcome NextRestoreOutcome { get; set; } = RestoreOutcome.Completed;

        // Set to hold a callback instead of invoking it, so a test can assert on the in-flight
        // window (the double-tap guard) before letting it complete.
        public bool DeferCallbacks { get; set; }
        private Action _deferred;

        public int PurchaseCallCount { get; private set; }
        public int RestoreCallCount { get; private set; }
        public int RefreshEntitlementsCallCount { get; private set; }
        public string LastPurchasedProductId { get; private set; }

        public FakePurchaseBackend WithProduct(string id, string display, string price)
        {
            _products.Add(new StoreProductView(id, display, new LocalizedPrice(price)));
            return this;
        }

        // Simulates the store granting an entitlement — what CustomerInfo would report after a
        // real purchase. Tests call this to make the fake "have" something.
        public FakePurchaseBackend WithEntitlement(string entitlementId,
            GrantSource source = GrantSource.Store)
        {
            _entitlements.Add(new EntitlementGrant(entitlementId, source));
            return this;
        }

        public void RevokeAll() => _entitlements.Clear();

        public void CompleteDeferred()
        {
            var d = _deferred;
            _deferred = null;
            d?.Invoke();
        }

        public void SignalReady()
        {
            Availability = BackendAvailability.Ready;
            Ready?.Invoke();
        }

        private void Deliver(Action action)
        {
            if (DeferCallbacks) _deferred = action;
            else action();
        }

        public void FetchProducts(Action<IReadOnlyList<StoreProductView>> onDone)
            => Deliver(() => onDone?.Invoke(_products.ToArray()));

        public void Purchase(string productId, Action<PurchaseResult> onDone)
        {
            PurchaseCallCount++;
            LastPurchasedProductId = productId;
            var outcome = NextPurchaseOutcome;

            Deliver(() =>
            {
                // A successful purchase makes the store report the product's entitlements from
                // then on — modelling the real ordering, where the purchase callback returns and
                // CustomerInfo is what actually says you own it.
                if (outcome == PurchaseOutcome.SuccessCandidate && GrantOnPurchase != null)
                {
                    foreach (var e in GrantOnPurchase(productId))
                        _entitlements.Add(new EntitlementGrant(e, GrantSource.Store));
                }

                onDone?.Invoke(new PurchaseResult(outcome, productId, new LocalizedPrice("$1.99")));
            });
        }

        // Wired by tests to the catalogue, so the fake grants exactly what the data says.
        public Func<string, IReadOnlyList<string>> GrantOnPurchase { get; set; }

        public void Restore(Action<RestoreResult> onDone)
        {
            RestoreCallCount++;
            var outcome = NextRestoreOutcome;
            Deliver(() =>
            {
                if (outcome == RestoreOutcome.Completed && RestoreGrants != null)
                {
                    foreach (var e in RestoreGrants)
                        _entitlements.Add(new EntitlementGrant(e, GrantSource.Store));
                }

                onDone?.Invoke(new RestoreResult(outcome));
            });
        }

        public IReadOnlyList<string> RestoreGrants { get; set; }

        public void RefreshEntitlements(Action<EntitlementSnapshot> onDone)
        {
            RefreshEntitlementsCallCount++;
            Deliver(() => onDone?.Invoke(EntitlementsAreAuthoritative
                ? new EntitlementSnapshot(true, _entitlements.ToArray())
                : EntitlementSnapshot.Unreachable()));
        }
    }

    public static class PFixtures
    {
        // A pinned clock. Every test that involves time uses this rather than wall time, so a
        // lease test cannot flake by running across a second boundary.
        public sealed class Clock
        {
            public long Now = 1_700_000_000L;
            public Func<long> Fn => () => Now;
            public void Advance(long seconds) => Now += seconds;
        }

        // A small hand-written catalogue, independent of the shipped file, so parser tests do
        // not fail every time a designer edits a display name.
        public const string TinyCatalogJson = @"{
          ""schemaVersion"": 2,
          ""entitlements"": [
            { ""id"": ""outfit_conductor"", ""kind"": ""outfit"", ""display"": ""Conductor's Coat"", ""adLeaseSeconds"": 3600 },
            { ""id"": ""frame_brass"", ""kind"": ""frame"", ""display"": ""Brass Frame"", ""adLeaseSeconds"": 0 },
            { ""id"": ""supporter"", ""kind"": ""membership"", ""display"": ""Supporter"", ""adLeaseSeconds"": 0 }
          ],
          ""products"": [
            { ""id"": ""cm_outfit_conductor"", ""storeType"": ""non_consumable"", ""display"": ""Conductor's Coat"", ""entitlements"": [""outfit_conductor""] },
            { ""id"": ""cm_frame_brass"", ""storeType"": ""non_consumable"", ""display"": ""Brass Frame"", ""entitlements"": [""frame_brass""] },
            { ""id"": ""cm_bundle"", ""storeType"": ""non_consumable"", ""display"": ""Bundle"", ""entitlements"": [""outfit_conductor"", ""frame_brass"", ""supporter""] }
          ]
        }";

        public static PurchaseCatalog TinyCatalog() => PurchaseCatalog.Parse(TinyCatalogJson);

        // The catalogue the app will actually ship, read from Resources on disk. Kept separate
        // from TinyCatalog on purpose: one suite proves the parser is correct, another proves
        // the shipped data is correct, and a failure tells you which.
        public static string ShippedCatalogJson() => File.ReadAllText(Path.Combine(
            CatMetro.Tests.Domain.Fixtures.RepoRoot(),
            "unity", "Assets", "Resources", "Monetization", "product_catalog.json"));

        public static string ShippedPlacementsJson() => File.ReadAllText(Path.Combine(
            CatMetro.Tests.Domain.Fixtures.RepoRoot(),
            "unity", "Assets", "Resources", "Monetization", "rewarded_placements.json"));

        // A service over the tiny catalogue with a fake backend already wired to grant what the
        // catalogue says a product grants.
        public static (PurchaseService svc, FakePurchaseBackend backend, Clock clock) Service(
            PurchaseCatalog catalog = null)
        {
            var cat = catalog ?? TinyCatalog();
            var clock = new Clock();
            var backend = new FakePurchaseBackend { GrantOnPurchase = cat.EntitlementsFor };
            return (new PurchaseService(cat, backend, clock.Fn), backend, clock);
        }
    }
}
