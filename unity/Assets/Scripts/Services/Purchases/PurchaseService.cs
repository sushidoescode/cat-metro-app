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
        UnknownEntitlement,
        PersistenceFailed
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
        private IEntitlementLeasePersistence _leasePersistence;
        private IPurchaseBackendReadiness _readinessBackend;
        private IPurchaseBackendTransactionUpdates _transactionUpdatesBackend;

        private readonly Dictionary<string, StoreProductView> _storeProducts =
            new Dictionary<string, StoreProductView>(StringComparer.Ordinal);
        private readonly Queue<Action> _productRefreshQueue = new Queue<Action>();
        private readonly Queue<EntitlementRefreshRequest> _entitlementRefreshQueue =
            new Queue<EntitlementRefreshRequest>();

        private bool _purchaseInFlight;
        private bool _restoreInFlight;
        private bool _productRefreshInFlight;
        private bool _entitlementRefreshInFlight;
        private long _backendGeneration;
        private long _entitlementEpoch;

        // Diagnostics. Read by the device self-test and worth logging on launch: a shop that is
        // empty because the catalogue failed to parse and a shop that is empty because the store
        // has not configured any products look identical to a player, and very different here.
        public IReadOnlyList<string> CatalogProblems => _catalog.Problems;
        public int StoreProductCount => _storeProducts.Count;
        public BackendAvailability Availability => _backend.Availability;
        public EntitlementLedger Ledger => _ledger;
        public PurchaseCatalog Catalog => _catalog;
        public bool CanPersistRewardedAdGrants => _leasePersistence != null;

        // True once the backend has told us something authoritative. Until then the UI should
        // show cosmetics as locked but should NOT offer "restore" as if it had failed.
        public bool HasAuthoritativeEntitlements { get; private set; }

        public PurchaseService(PurchaseCatalog catalog, IPurchaseBackend backend = null,
            Func<long> clock = null, EntitlementLedger ledger = null)
        {
            _catalog = catalog ?? PurchaseCatalog.Empty;
            _clock = clock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            _ledger = ledger ?? new EntitlementLedger();
            _backend = new NullPurchaseBackend();
            AttachBackend(backend);
        }

        // Lets Integrations swap a live RevenueCat backend in after an async configure without
        // rebuilding the service and losing the ledger's ad leases. Null is accepted and means
        // "go back to degraded", which is what a failed configure should do.
        public void AttachBackend(IPurchaseBackend backend)
        {
            if (_readinessBackend != null)
                _readinessBackend.Ready -= OnBackendReady;
            if (_transactionUpdatesBackend != null)
                _transactionUpdatesBackend.TransactionEntitlementsConfirmed -=
                    OnTransactionEntitlementsConfirmed;

            unchecked
            {
                _backendGeneration++;
                _entitlementEpoch++;
            }
            _backend = backend ?? new NullPurchaseBackend();
            _readinessBackend = _backend as IPurchaseBackendReadiness;
            if (_readinessBackend != null)
                _readinessBackend.Ready += OnBackendReady;
            _transactionUpdatesBackend = _backend as IPurchaseBackendTransactionUpdates;
            if (_transactionUpdatesBackend != null)
                _transactionUpdatesBackend.TransactionEntitlementsConfirmed +=
                    OnTransactionEntitlementsConfirmed;
            _storeProducts.Clear();
        }

        // A rewarded lease is published to the ledger only after its replacement snapshot has
        // committed. A null adapter represents an unavailable local save and intentionally
        // makes awarding fail closed rather than granting a reward that disappears at restart.
        public void AttachLeasePersistence(IEntitlementLeasePersistence persistence)
        {
            _leasePersistence = persistence;
        }

        // ---- the query the whole game uses ----------------------------------------------

        // Bought, restored, promotionally granted, or currently leased from a rewarded ad — all
        // one answer. Callers cannot tell which, and that is the design.
        public bool IsUnlocked(string entitlementId) => _ledger.IsActive(entitlementId, _clock());

        // Seconds until this unlock lapses, or 0 if it never will. Source-blind: it counts down
        // a locally granted ad lease and a RevenueCat-granted timed entitlement identically.
        public long SecondsUntilExpiry(string entitlementId)
            => _ledger.SecondsUntilExpiry(entitlementId, _clock());

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

            RefreshProducts(Done);
            RefreshEntitlements(Done);
        }

        public void RefreshEntitlements(Action onDone = null)
        {
            RefreshEntitlementsWithSnapshot((_, __) => onDone?.Invoke());
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

            if (_purchaseInFlight || _restoreInFlight)
            {
                onDone?.Invoke(new PurchaseResult(PurchaseOutcome.Busy, productId));
                return;
            }

            var requestedProductId = productId;
            var backend = _backend;
            var generation = _backendGeneration;
            _purchaseInFlight = true;
            backend.Purchase(productId, result =>
            {
                _purchaseInFlight = false;

                if (!IsCurrentBackend(backend, generation))
                {
                    onDone?.Invoke(new PurchaseResult(result.Outcome, result.ProductId,
                        result.LocalizedPrice, result.DiagnosticMessage));
                    return;
                }

                if (!result.IsGrantable)
                {
                    onDone?.Invoke(result);
                    return;
                }

                // The store says yes — but entitlements are only ever believed from CustomerInfo,
                // never inferred from the product id. Prefer the authoritative snapshot carried
                // by this callback; refresh only when an integration cannot provide it. Both
                // purchase and restore still converge on ApplySnapshot and the same ledger.
                if (TryApplyConfirmedSnapshot(result.ConfirmedEntitlements))
                {
                    onDone?.Invoke(NormalizePurchaseConfirmation(result, requestedProductId,
                        result.ConfirmedEntitlements));
                    return;
                }

                RefreshEntitlementsWithSnapshot((snapshot, accepted) =>
                    onDone?.Invoke(NormalizePurchaseConfirmation(result, requestedProductId,
                        accepted ? snapshot : (EntitlementSnapshot?)null)));
            });
        }

        // Both stores require this for non-consumables, and Apple requires a visible control for
        // it. Not optional, and not a debug affordance.
        public void Restore(Action<RestoreResult> onDone)
        {
            if (_restoreInFlight || _purchaseInFlight)
            {
                onDone?.Invoke(new RestoreResult(RestoreOutcome.Busy));
                return;
            }

            var backend = _backend;
            var generation = _backendGeneration;
            _restoreInFlight = true;
            backend.Restore(result =>
            {
                _restoreInFlight = false;

                if (!IsCurrentBackend(backend, generation))
                {
                    bool completed = result.Outcome == RestoreOutcome.Completed;
                    onDone?.Invoke(new RestoreResult(
                        completed ? RestoreOutcome.Failure : result.Outcome,
                        0,
                        completed
                            ? "restore completed on a replaced purchase backend"
                            : result.DiagnosticMessage));
                    return;
                }

                if (result.Outcome != RestoreOutcome.Completed)
                {
                    onDone?.Invoke(result);
                    return;
                }

                if (TryApplyConfirmedSnapshot(result.ConfirmedEntitlements))
                {
                    onDone?.Invoke(new RestoreResult(RestoreOutcome.Completed,
                        CountCatalogEntitlements(result.ConfirmedEntitlements.Value)));
                    return;
                }

                RefreshEntitlementsWithSnapshot((snapshot, accepted) =>
                {
                    if (!accepted)
                    {
                        onDone?.Invoke(new RestoreResult(RestoreOutcome.Failure, 0,
                            "restore completed but entitlements could not be confirmed"));
                        return;
                    }

                    // Count exactly what the authoritative CustomerInfo restored. The merged
                    // access ledger may also contain an ad lease, which is wearable but was not
                    // a restored purchase.
                    onDone?.Invoke(new RestoreResult(RestoreOutcome.Completed,
                        CountCatalogEntitlements(snapshot)));
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
            long expiresAt = now + definition.AdLeaseSeconds;
            if (expiresAt <= 0L || expiresAt <= now)
                return AdGrantOutcome.AlreadyUnlocked;
            if (!_ledger.CanGrantLease(entitlementId, expiresAt, now))
                return AdGrantOutcome.AlreadyUnlocked;

            var candidate = ActiveLeaseCandidateWith(entitlementId, expiresAt, now);
            try
            {
                if (_leasePersistence == null ||
                    !_leasePersistence.TryReplaceRewardedAdLeases(candidate))
                    return AdGrantOutcome.PersistenceFailed;
            }
            catch
            {
                return AdGrantOutcome.PersistenceFailed;
            }

            return _ledger.GrantLease(entitlementId, expiresAt, now)
                ? AdGrantOutcome.Granted
                : AdGrantOutcome.AlreadyUnlocked;
        }

        // Lets the ad lane ask "is showing an ad for this worth the player's thirty seconds?"
        // before it loads one.
        public bool CanOfferAdFor(string entitlementId)
        {
            if (!_catalog.TryGetEntitlement(entitlementId, out var definition) ||
                !definition.IsAdGrantable)
                return false;

            long now = _clock();
            long expiresAt = now + definition.AdLeaseSeconds;
            return expiresAt > 0L && expiresAt > now &&
                _ledger.CanGrantLease(entitlementId, expiresAt, now);
        }

        public bool PruneExpiredLeases() => _ledger.PruneExpired(_clock());

        // Save parsing is deliberately permissive, but only a currently valid rewarded lease
        // for a live ad-grantable catalogue entitlement may enter the ledger. A SaveStore is the
        // authority for the complete local lease snapshot, so a new store replaces (not merges)
        // prior rewarded leases while store/promotional grants remain untouched. Validate the
        // full input before the single ledger mutation so a throwing source cannot partly swap.
        public void RestoreRewardedAdLeases(IReadOnlyList<EntitlementGrant> leases)
        {
            if (leases == null) return;
            long now = _clock();
            var valid = new List<EntitlementGrant>();
            for (int i = 0; i < leases.Count; i++)
            {
                var lease = leases[i];
                if (lease.Source != GrantSource.RewardedAd ||
                    lease.ExpiresAtUnixSeconds <= 0L ||
                    lease.ExpiresAtUnixSeconds <= now ||
                    !_catalog.TryGetEntitlement(lease.EntitlementId, out var definition) ||
                    !definition.IsAdGrantable)
                    continue;
                valid.Add(lease);
            }

            _ledger.ReplaceRewardedAdLeases(valid, now);
        }

        // ---- internals ------------------------------------------------------------------

        private void OnBackendReady() => Refresh();

        private IReadOnlyList<EntitlementGrant> ActiveLeaseCandidateWith(string entitlementId,
            long expiresAtUnixSeconds, long nowUnixSeconds)
        {
            var candidate = new List<EntitlementGrant>();
            var existing = _ledger.ExportLeases();
            for (int i = 0; i < existing.Count; i++)
            {
                var lease = existing[i];
                if (lease.Source != GrantSource.RewardedAd ||
                    lease.ExpiresAtUnixSeconds <= 0L ||
                    lease.ExpiresAtUnixSeconds <= nowUnixSeconds ||
                    !lease.IsActiveAt(nowUnixSeconds) ||
                    lease.EntitlementId == entitlementId)
                    continue;
                candidate.Add(lease);
            }
            candidate.Add(new EntitlementGrant(entitlementId, GrantSource.RewardedAd,
                expiresAtUnixSeconds));
            candidate.Sort((a, b) => string.CompareOrdinal(a.EntitlementId, b.EntitlementId));
            return candidate;
        }

        private void OnTransactionEntitlementsConfirmed(EntitlementSnapshot snapshot)
            => TryApplyConfirmedSnapshot(snapshot);

        // purchases-unity 9.9 keeps one native callback slot per operation. Calling the same
        // operation again before it completes overwrites that slot and strands the first caller
        // until our 30-second timeout. Queue at the engine-free seam so launch, resume, wardrobe,
        // purchase confirmation, and restore confirmation can never overlap at the SDK boundary.
        private void RefreshProducts(Action onDone)
        {
            _productRefreshQueue.Enqueue(onDone);
            PumpProductRefreshes();
        }

        private void PumpProductRefreshes()
        {
            if (_productRefreshInFlight || _productRefreshQueue.Count == 0) return;

            _productRefreshInFlight = true;
            var onDone = _productRefreshQueue.Dequeue();
            var backend = _backend;
            var generation = _backendGeneration;
            backend.FetchProducts(products =>
            {
                if (IsCurrentBackend(backend, generation)
                    && backend.Availability == BackendAvailability.Ready
                    && products != null)
                {
                    _storeProducts.Clear();
                    for (int i = 0; i < products.Count; i++)
                    {
                        var p = products[i];
                        // Ignore anything the store offers that our catalogue does not
                        // declare. A stray store-console product must not become purchasable.
                        if (_catalog.TryGetProduct(p.ProductId, out _))
                            _storeProducts[p.ProductId] = p;
                    }
                }

                _productRefreshInFlight = false;
                try { onDone?.Invoke(); }
                finally { PumpProductRefreshes(); }
            });
        }

        private void PumpEntitlementRefreshes()
        {
            if (_entitlementRefreshInFlight) return;

            // This flag owns both the native request and the stale-drain pump. A stale callback
            // may synchronously enqueue current work, but it cannot re-enter and start a second
            // RevenueCat request while this invocation still owns the one native slot.
            _entitlementRefreshInFlight = true;

            while (_entitlementRefreshQueue.Count > 0 &&
                   !IsCurrentRequest(_entitlementRefreshQueue.Peek()))
            {
                var stale = _entitlementRefreshQueue.Dequeue();
                try
                {
                    stale.OnDone?.Invoke(EntitlementSnapshot.Unreachable(), false);
                }
                catch
                {
                    _entitlementRefreshInFlight = false;
                    PumpEntitlementRefreshes();
                    throw;
                }
            }

            if (_entitlementRefreshQueue.Count == 0)
            {
                _entitlementRefreshInFlight = false;
                return;
            }

            var request = _entitlementRefreshQueue.Dequeue();
            request.Backend.RefreshEntitlements(snapshot =>
            {
                bool current = IsCurrentRequest(request);
                bool accepted = current && snapshot.IsAuthoritative;
                if (accepted) ApplySnapshot(snapshot);
                _entitlementRefreshInFlight = false;
                try
                {
                    request.OnDone?.Invoke(current
                        ? snapshot
                        : EntitlementSnapshot.Unreachable(), accepted);
                }
                finally { PumpEntitlementRefreshes(); }
            });
        }

        private void RefreshEntitlementsWithSnapshot(Action<EntitlementSnapshot, bool> onDone)
        {
            _entitlementRefreshQueue.Enqueue(
                new EntitlementRefreshRequest(_backend, _backendGeneration,
                    _entitlementEpoch, onDone));
            PumpEntitlementRefreshes();
        }

        private bool TryApplyConfirmedSnapshot(EntitlementSnapshot? candidate)
        {
            if (!candidate.HasValue || !candidate.Value.IsAuthoritative) return false;

            // Invalidate any CustomerInfo requested before this native transaction completed.
            // If that old request returns later, it cannot revoke the newer purchase truth.
            _entitlementEpoch++;
            ApplySnapshot(candidate.Value);
            return true;
        }

        private PurchaseResult NormalizePurchaseConfirmation(PurchaseResult result,
            string requestedProductId, EntitlementSnapshot? acceptedSnapshot)
        {
            EntitlementSnapshot? confirmation = acceptedSnapshot.HasValue
                && acceptedSnapshot.Value.IsAuthoritative
                && SnapshotFulfilsProduct(requestedProductId, acceptedSnapshot.Value)
                    ? acceptedSnapshot
                    : null;
            return new PurchaseResult(result.Outcome, result.ProductId,
                result.LocalizedPrice, result.DiagnosticMessage, confirmation);
        }

        private bool SnapshotFulfilsProduct(string productId, EntitlementSnapshot snapshot)
        {
            var promised = _catalog.EntitlementsFor(productId);
            if (promised.Count == 0) return false;
            var grants = snapshot.Grants;
            long now = _clock();
            for (int i = 0; i < promised.Count; i++)
            {
                bool found = false;
                if (grants != null)
                {
                    for (int j = 0; j < grants.Count; j++)
                    {
                        var grant = grants[j];
                        if (grant.Source == GrantSource.RewardedAd
                            || !grant.IsActiveAt(now)
                            || !string.Equals(grant.EntitlementId, promised[i],
                                StringComparison.Ordinal))
                            continue;
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
            return true;
        }

        private bool IsCurrentBackend(IPurchaseBackend backend, long generation)
            => ReferenceEquals(backend, _backend) && generation == _backendGeneration;

        private bool IsCurrentRequest(EntitlementRefreshRequest request)
            => IsCurrentBackend(request.Backend, request.BackendGeneration)
               && request.Epoch == _entitlementEpoch;

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

        private int CountCatalogEntitlements(EntitlementSnapshot snapshot)
        {
            long now = _clock();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int count = 0;
            var grants = snapshot.Grants;
            if (grants == null) return 0;
            for (int i = 0; i < grants.Count; i++)
            {
                var grant = grants[i];
                if (grant.Source == GrantSource.RewardedAd || !grant.IsActiveAt(now)) continue;
                if (!_catalog.TryGetEntitlement(grant.EntitlementId, out _)) continue;
                if (seen.Add(grant.EntitlementId)) count++;
            }
            return count;
        }

        private readonly struct EntitlementRefreshRequest
        {
            public readonly IPurchaseBackend Backend;
            public readonly long BackendGeneration;
            public readonly long Epoch;
            public readonly Action<EntitlementSnapshot, bool> OnDone;

            public EntitlementRefreshRequest(IPurchaseBackend backend, long backendGeneration,
                long epoch,
                Action<EntitlementSnapshot, bool> onDone)
            {
                Backend = backend;
                BackendGeneration = backendGeneration;
                Epoch = epoch;
                OnDone = onDone;
            }
        }
    }
}
