using System;
using CatMetro.Application.Save;
using CatMetro.Services.Ads;
using CatMetro.Services.Purchases;

namespace CatMetro.Integrations
{
    // Owns the one rewarded coordinator around the already-existing purchase and save runtimes.
    // SaveRuntime can publish after monetization boot, so binding observes first and reads second.
    internal sealed class RewardedAdsComposition : IDisposable
    {
        private readonly PurchaseService _service;
        private readonly RewardedPlacementCatalog _placements;
        private readonly Func<IRewardedAdProvider> _providerFactory;
        private readonly IAdEventReporter _reporter;
        private readonly Func<string> _localDateKey;

        private SaveStore _boundStore;
        private RewardedAdCoordinator _coordinator;
        private IMainThreadRewardedAdEventDrain _mainThreadDrain;
        private double _lastMonotonicSeconds;
        private bool _hasMonotonicSeconds;
        private bool _subscribed;
        private bool _disposed;

        internal RewardedAdsComposition(PurchaseService service,
            RewardedPlacementCatalog placements, Func<IRewardedAdProvider> providerFactory,
            IAdEventReporter reporter, Func<string> localDateKey)
        {
            _service = service;
            _placements = placements;
            _providerFactory = providerFactory;
            _reporter = reporter;
            _localDateKey = localDateKey;
        }

        internal void Bind()
        {
            if (_disposed || _subscribed) return;
            _subscribed = true;
            SaveRuntime.Installed += OnSaveInstalled;

            // Subscribe first so an install at the edge cannot be missed, then consume Current
            // through the same reference guard used by the event callback.
            var current = SaveRuntime.Current;
            if (current != null) OnSaveInstalled(current);
        }

        internal void OnApplicationPause(bool paused)
        {
            if (_disposed) return;
            try
            {
                if (paused)
                    _boundStore?.TryCommitOnPause();
                else
                    _service?.RefreshEntitlements();
            }
            catch
            {
                // Save and store refresh are optional boundaries. Unity lifecycle must continue.
            }
        }

        internal void DrainMainThreadAdEvents()
        {
            if (_disposed) return;
            var drain = _mainThreadDrain;
            try { drain?.DrainMainThreadEvents(); }
            catch
            {
                // A mediation analytics handoff is optional and must not break the frame owner.
            }
        }

        internal void Tick(double monotonicSeconds)
        {
            if (_disposed) return;
            if (IsValidMonotonicSeconds(monotonicSeconds) &&
                (!_hasMonotonicSeconds || monotonicSeconds >= _lastMonotonicSeconds))
            {
                _lastMonotonicSeconds = monotonicSeconds;
                _hasMonotonicSeconds = true;
            }
            try { _coordinator?.Tick(monotonicSeconds); }
            catch
            {
                // Retry/retention maintenance is optional monetization work. It cannot own the
                // Unity frame even if a future coordinator implementation regresses.
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_subscribed)
            {
                SaveRuntime.Installed -= OnSaveInstalled;
                _subscribed = false;
            }
            _boundStore = null;
            TearDownCoordinator();
        }

        private void OnSaveInstalled(SaveStore store)
        {
            if (_disposed || store == null || ReferenceEquals(store, _boundStore)) return;

            // Claim this store before tearing down the prior coordinator. Provider/event accessors
            // are callback-capable; a teardown callback can install a newer store synchronously.
            // Every later boundary therefore verifies this exact store still owns the attempt.
            _boundStore = store;
            TearDownCoordinator();
            if (!OwnsStore(store)) return;

            IRewardedAdProvider provider = null;
            RewardedAdCoordinator coordinator = null;
            if (_service == null) return;

            // The prior writable store is no longer authoritative. Detach before parsing or
            // restoring because RestoreRewardedAdLeases can publish Ledger.Changed reentrantly.
            // Any read/restore exception before a new writable adapter is attached leaves ad
            // rewards fail-closed, never writing through the replaced store's adapter.
            _service.AttachLeasePersistence(null);
            if (!OwnsStore(store)) return;
            try
            {
                // Saved leases remain part of purchase truth even when ads are not configured in
                // this build, so restore and persistence attachment happen before ad viability.
                var saveData = new RewardedAdSaveStore(store);
                _service.RestoreRewardedAdLeases(saveData.ReadLocalLeases());
                if (!OwnsStore(store)) return;
                if (store.ReadOnlyMode) return;
                _service.AttachLeasePersistence(saveData);
                if (!OwnsStore(store)) return;

                if (_placements == null || _providerFactory == null || _reporter == null ||
                    _localDateKey == null)
                    return;

                provider = _providerFactory();
                if (provider == null) return;
                if (!OwnsStore(store))
                {
                    SafeDispose(provider);
                    return;
                }

                coordinator = new RewardedAdCoordinator(_placements, _service, provider,
                    _reporter, saveData, _localDateKey);
                _mainThreadDrain = provider as IMainThreadRewardedAdEventDrain;
                _coordinator = coordinator;
                if (_hasMonotonicSeconds) coordinator.Tick(_lastMonotonicSeconds);
                RewardedAdRuntime.Install(coordinator);
                if (!OwnsPublished(store, coordinator))
                {
                    ReleaseAttempt(coordinator);
                    return;
                }
                coordinator.Start();
                if (!OwnsPublished(store, coordinator)) ReleaseAttempt(coordinator);
            }
            catch
            {
                if (coordinator != null) ReleaseAttempt(coordinator);
                else SafeDispose(provider);
            }
        }

        private void TearDownCoordinator()
        {
            var coordinator = _coordinator;
            if (coordinator == null) return;
            _coordinator = null;
            _mainThreadDrain = null;
            try { coordinator?.Dispose(); }
            catch { }
            RewardedAdRuntime.Uninstall(coordinator);
        }

        private bool OwnsStore(SaveStore store)
            => !_disposed && ReferenceEquals(_boundStore, store);

        private bool OwnsPublished(SaveStore store, RewardedAdCoordinator coordinator)
            => OwnsStore(store) && ReferenceEquals(_coordinator, coordinator) &&
               ReferenceEquals(RewardedAdRuntime.Current, coordinator);

        private void ReleaseAttempt(RewardedAdCoordinator coordinator)
        {
            if (ReferenceEquals(_coordinator, coordinator))
            {
                _coordinator = null;
                _mainThreadDrain = null;
            }
            SafeDispose(coordinator);
            RewardedAdRuntime.Uninstall(coordinator);
        }

        private static void SafeDispose(IDisposable disposable)
        {
            try { disposable?.Dispose(); }
            catch { }
        }

        private static bool IsValidMonotonicSeconds(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
    }
}
