using System;
using CatMetro.Services.Ads;

namespace CatMetro.Services.Cosmetics
{
    // Presentation-safe bridge: ownership remains solely in RewardedAdCoordinator/PurchaseService.
    public sealed class RewardedAdCosmeticRoute : ICosmeticRewardedRoute, IDisposable
    {
        private IRewardedAds _subscribed;
        private IRewardedAds _pendingSource;
        private Action<CosmeticRewardedCompletion> _pendingFinish;
        private bool _disposed;

        public event Action AvailabilityChanged;

        public RewardedAdCosmeticRoute()
        {
            RewardedAdRuntime.Changed += OnRuntimeChanged;
            Rebind(RewardedAdRuntime.Current);
        }

        public bool CanOffer(string placementId, string entitlementId)
        {
            if (_disposed) return false;
            var source = Resolve() as IRewardedAdExactCompletionSource;
            try { return source != null && source.CanShow(placementId, entitlementId); }
            catch { return false; }
        }

        public void Request(string placementId, string entitlementId,
            Action<CosmeticRewardedCompletion> completed)
        {
            var ads = Resolve();
            var source = ads as IRewardedAdExactCompletionSource;
            bool completedOnce = false;
            Action<CosmeticRewardedCompletion> finish = null;
            finish = outcome =>
            {
                if (completedOnce) return;
                completedOnce = true;
                if (ReferenceEquals(_pendingFinish, finish))
                {
                    _pendingFinish = null;
                    _pendingSource = null;
                }
                try { completed?.Invoke(outcome); }
                catch { }
            };
            if (_pendingFinish != null)
            {
                finish(CosmeticRewardedCompletion.NotGranted);
                return;
            }
            if (_disposed || source == null || !CanOffer(placementId, entitlementId))
            {
                finish(CosmeticRewardedCompletion.NotGranted);
                return;
            }
            if (!ReferenceEquals(ads, Resolve()))
            {
                finish(CosmeticRewardedCompletion.NotGranted);
                return;
            }

            RewardedShowOutcome shown;
            try
            {
                _pendingSource = ads;
                _pendingFinish = finish;
                shown = source.Show(placementId, entitlementId, result =>
                {
                    bool exact = ReferenceEquals(ads, Resolve()) && result.AttemptId > 0L &&
                        string.Equals(result.PlacementId, placementId, StringComparison.Ordinal) &&
                        string.Equals(result.EntitlementId, entitlementId, StringComparison.Ordinal);
                    finish(exact && result.Kind == RewardedAdCompletionKind.Granted
                        ? CosmeticRewardedCompletion.Granted
                        : CosmeticRewardedCompletion.NotGranted);
                });
            }
            catch
            {
                finish(CosmeticRewardedCompletion.NotGranted);
                return;
            }
            if (shown != RewardedShowOutcome.Started)
                finish(CosmeticRewardedCompletion.NotGranted);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            RewardedAdRuntime.Changed -= OnRuntimeChanged;
            Rebind(null);
        }

        private IRewardedAds Resolve()
        {
            Rebind(RewardedAdRuntime.Current);
            return _subscribed;
        }

        private void OnRuntimeChanged() => Rebind(RewardedAdRuntime.Current);

        private void Rebind(IRewardedAds next)
        {
            if (ReferenceEquals(_subscribed, next)) return;
            if (_pendingSource != null && !ReferenceEquals(_pendingSource, next))
                _pendingFinish?.Invoke(CosmeticRewardedCompletion.NotGranted);
            if (_subscribed != null)
            {
                try { _subscribed.AvailabilityChanged -= OnAvailabilityChanged; }
                catch { }
            }
            _subscribed = next;
            if (!_disposed && _subscribed != null)
            {
                try { _subscribed.AvailabilityChanged += OnAvailabilityChanged; }
                catch { }
            }
            RaiseAvailabilityChanged();
        }

        private void OnAvailabilityChanged() => RaiseAvailabilityChanged();

        private void RaiseAvailabilityChanged()
        {
            if (_disposed) return;
            var handlers = AvailabilityChanged;
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                try { handler(); }
                catch { }
            }
        }
    }
}
