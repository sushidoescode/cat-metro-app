using System;
using System.Collections.Generic;

namespace CatMetro.Services.Purchases
{
    // Why a rewarded-ad grant was refused. The ad lane needs to tell these apart so it can
    // decide whether to even offer the ad, rather than showing thirty seconds of advertising and
    // then handing over nothing.
    public enum AdGrantOutcome
    {
        Granted,
        AlreadyUnlocked,
        NotAdGrantable,
        UnknownEntitlement
    }

    // The one object the rest of the game talks to about money.
    //
    // Engine-free on purpose: it lives in CatMetro.Services (noEngineReferences: true), takes its
    // clock as an injected function exactly like GameRoot.DailyClockUnixSeconds, and knows
    // nothing about RevenueCat. Everything here is exercised in EditMode against a fake backend.
    //
    // Degradation posture, which is the part that matters most: this class is CONSTRUCTIBLE AND
    // USEFUL WITH NO BACKEND AT ALL. Pass NullPurchaseBackend (or nothing) and every query still
    // answers, every command still calls back, and the game gets a shop full of locked cosmetics
    // instead of an exception. There is no code path in which a missing store, a missing network,
    // a missing API key, or a missing SDK produces a throw.
    public sealed class PurchaseService
    {
        private readonly PurchaseCatalog _catalog;
        private readonly EntitlementLedger _ledger;
        private readonly Func<long> _clock;
        private IPurchaseBackend _backend;

        private readonly Dictionary<string, StoreProductView> _storeProducts =
            new Dictionary<string, StoreProductView>(StringComparer.Ordinal);

        private bool _purchaseInFlight;
        private bool _restoreInFlight;

        // Diagnostics. Read by the device self-test and worth logging on launch: a shop that is
        // empty because the catalogue failed to parse and a shop that is empty because the store
        // has not configured any products look identical to a player, and very different here.
        public IReadOnlyList<string> CatalogProblems => _catalog.Problems;
        public int StoreProductCount => _storeProducts.Count;
        public BackendAvailability Availability => _backend.Availability;
        public EntitlementLedger Ledger => _ledger;
        public PurchaseCatalog Catalog => _catalog;

        // True once the backend has told us something authoritative. Until then the UI should
        // show cosmetics as locked but should NOT offer "restore" as if it had failed.
        public bool HasAuthoritativeEntitlements { get; private set; }

        public PurchaseService(PurchaseCatalog catalog, IPurchaseBackend backend = null,
            Func<long> clock = null, EntitlementLedger ledger = null)
        {
            _catalog = catalog ?? PurchaseCatalog.Empty;
            _backend = backend ?? new NullPurchaseBackend();
            _clock = clock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            _ledger = ledger ?? new EntitlementLedger();
        }

        // Lets Integrations swap a live RevenueCat backend in after an async configure without
        // rebuilding the service and losing the ledger's ad leases. Null is accepted and means
        // "go back to degraded", which is what a failed configure should do.
        public void AttachBackend(IPurchaseBackend backend)
        {
            _backend = backend ?? new NullPurchaseBackend();
            _storeProducts.Clear();
        }

        // ---- the query the whole game uses ----------------------------------------------

        // Bought, restored, promotionally granted, or currently leased from a rewarded ad — all
        // one answer. Callers cannot tell which, and that is the design.
        public bool IsUnlocked(string entitlementId) => _ledger.IsActive(entitlementId, _clock());

        public long LeaseSecondsRemaining(string entitlementId)
            => _ledger.LeaseSecondsRemaining(entitlementId, _clock());

        // ---- shop -----------------------------------------------------------------------

        // The catalogue joined to live store pricing, in catalogue order. Products the store has
        // never heard of are included with an unknown price so the shop can show them greyed
        // rather than silently shrinking — a cosmetic vanishing from the shop because a store
        // console entry was misconfigured is the failure this makes visible.
        public IReadOnlyList<StoreProductView> ShopItems()
        {
            var result = new List<StoreProductView>(_catalog.Products.Count);
            for (int i = 0; i < _catalog.Products.Count; i++)
            {
                var entry = _catalog.Products[i];
                result.Add(_storeProducts.TryGetValue(entry.Id, out var live)
                    ? live
                    : new StoreProductView(entry.Id, entry.DisplayName, default));
            }

            return result;
        }

        public bool TryGetPrice(string productId, out LocalizedPrice price)
        {
            if (productId != null && _storeProducts.TryGetValue(productId, out var view))
            {
                price = view.Price;
                return price.IsKnown;
            }

            price = default;
            return false;
        }

        // ---- commands -------------------------------------------------------------------

        // Launch and foreground path. Safe to call any number of times.
        public void Refresh(Action onDone = null)
        {
            int pending = 2;
            void Done() { if (--pending == 0) onDone?.Invoke(); }

            _backend.FetchProducts(products =>
            {
                _storeProducts.Clear();
                if (products != null)
                {
                    for (int i = 0; i < products.Count; i++)
                    {
                        var p = products[i];
                        // Ignore anything the store offers that our catalogue does not declare.
                        // A stray product in the store console must not become a purchasable
                        // item that grants nothing.
                        if (_catalog.TryGetProduct(p.ProductId, out _)) _storeProducts[p.ProductId] = p;
                    }
                }

                Done();
            });

            RefreshEntitlements(Done);
        }

        public void RefreshEntitlements(Action onDone = null)
        {
            _backend.RefreshEntitlements(snapshot =>
            {
                ApplySnapshot(snapshot);
                onDone?.Invoke();
            });
        }

        public void Purchase(string productId, Action<PurchaseResult> onDone)
        {
            if (!_catalog.TryGetProduct(productId, out _))
            {
                // Refusing to ask the store for something we cannot honour. If the purchase
                // succeeded we would have no entitlements to grant and would have taken money
                // for nothing.
                onDone?.Invoke(PurchaseResult.Unavailable(productId, "product is not in the catalogue"));
                return;
            }

            if (_purchaseInFlight)
            {
                onDone?.Invoke(new PurchaseResult(PurchaseOutcome.Busy, productId));
                return;
            }

            _purchaseInFlight = true;
            _backend.Purchase(productId, result =>
            {
                _purchaseInFlight = false;

                if (!result.IsGrantable)
                {
                    onDone?.Invoke(result);
                    return;
                }

                // The store says yes — but entitlements are only ever believed from CustomerInfo,
                // never inferred from a purchase callback. This second hop is what makes the
                // purchase path and the restore path identical downstream, and it is why a
                // refund later actually takes the coat off the cat.
                _backend.RefreshEntitlements(snapshot =>
                {
                    ApplySnapshot(snapshot);
                    onDone?.Invoke(result);
                });
            });
        }

        // Both stores require this for non-consumables, and Apple requires a visible control for
        // it. Not optional, and not a debug affordance.
        public void Restore(Action<RestoreResult> onDone)
        {
            if (_restoreInFlight)
            {
                onDone?.Invoke(new RestoreResult(RestoreOutcome.Busy));
                return;
            }

            _restoreInFlight = true;
            _backend.Restore(result =>
            {
                _restoreInFlight = false;

                if (result.Outcome != RestoreOutcome.Completed)
                {
                    onDone?.Invoke(result);
                    return;
                }

                _backend.RefreshEntitlements(snapshot =>
                {
                    ApplySnapshot(snapshot);
                    // Report the count WE can see after applying, not whatever the backend
                    // guessed: the number shown to the player should match the number of
                    // cosmetics that just unlocked.
                    onDone?.Invoke(new RestoreResult(RestoreOutcome.Completed,
                        CountActiveCatalogEntitlements()));
                });
            });
        }

        // ---- the rewarded-ad convergence -------------------------------------------------

        // Called by the ad lane after a rewarded ad completes. Note what is NOT here: no second
        // entitlement store, no "temporary unlock" flag for the UI to check, no parallel query.
        // The ad lands in the same ledger the purchase lands in, and IsUnlocked cannot tell the
        // difference afterwards.
        //
        // The lease length comes from the catalogue, not from the caller, so ad rewards stay a
        // data-driven balance decision rather than something the ad integration hard-codes.
        public AdGrantOutcome GrantRewardedAdEntitlement(string entitlementId)
        {
            if (!_catalog.TryGetEntitlement(entitlementId, out var definition))
                return AdGrantOutcome.UnknownEntitlement;

            if (!definition.IsAdGrantable) return AdGrantOutcome.NotAdGrantable;

            long now = _clock();
            if (_ledger.IsActive(entitlementId, now)) return AdGrantOutcome.AlreadyUnlocked;

            return _ledger.GrantLease(entitlementId, now + definition.AdLeaseSeconds, now)
                ? AdGrantOutcome.Granted
                : AdGrantOutcome.AlreadyUnlocked;
        }

        // Lets the ad lane ask "is showing an ad for this worth the player's thirty seconds?"
        // before it loads one.
        public bool CanOfferAdFor(string entitlementId)
            => _catalog.TryGetEntitlement(entitlementId, out var d) && d.IsAdGrantable &&
               !_ledger.IsActive(entitlementId, _clock());

        public bool PruneExpiredLeases() => _ledger.PruneExpired(_clock());

        // ---- internals ------------------------------------------------------------------

        private void ApplySnapshot(EntitlementSnapshot snapshot)
        {
            // The single most important branch in this file. A non-authoritative snapshot means
            // we could not reach RevenueCat — offline, no SDK, in the Editor. Applying it would
            // wipe every paid entitlement. So we do nothing at all and keep whatever we last
            // knew, which for a fresh offline launch is "nothing" (correctly conservative) and
            // for a session that has already synced is the truth.
            if (!snapshot.IsAuthoritative) return;

            HasAuthoritativeEntitlements = true;
            _ledger.ReplaceStoreGrants(snapshot.Grants);
        }

        private int CountActiveCatalogEntitlements()
        {
            long now = _clock();
            int count = 0;
            for (int i = 0; i < _catalog.Entitlements.Count; i++)
                if (_ledger.IsActive(_catalog.Entitlements[i].Id, now)) count++;
            return count;
        }
    }
}
