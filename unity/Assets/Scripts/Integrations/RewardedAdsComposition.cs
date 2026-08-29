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

            // Replacement ordering matters: the old coordinator owns and disposes its provider,
            // and the public runtime goes safe before any new save/provider work begins.
            TearDownCoordinator();
            _boundStore = store;

            IRewardedAdProvider provider = null;
            RewardedAdCoordinator coordinator = null;
            try
            {
                if (_service == null) return;

                // Saved leases remain part of purchase truth even when ads are not configured in
                // this build, so restore and persistence attachment happen before ad viability.
                var saveData = new RewardedAdSaveStore(store);
                _service.RestoreRewardedAdLeases(saveData.ReadLocalLeases());
                _service.AttachLeasePersistence(saveData);

                if (_placements == null || _providerFactory == null || _reporter == null ||
                    _localDateKey == null)
                    return;

                provider = _providerFactory();
                if (provider == null) return;

                coordinator = new RewardedAdCoordinator(_placements, _service, provider,
                    _reporter, saveData, _localDateKey);
                RewardedAdRuntime.Install(coordinator);
                coordinator.Start();
                _coordinator = coordinator;
            }
            catch
            {
                if (coordinator != null)
                    coordinator.Dispose();
                else
                {
                    try { provider?.Dispose(); }
                    catch { }
                }
                RewardedAdRuntime.ResetForTests();
            }
        }

        private void TearDownCoordinator()
        {
            var coordinator = _coordinator;
            _coordinator = null;
            try { coordinator?.Dispose(); }
            catch { }
            RewardedAdRuntime.ResetForTests();
        }
    }
}
