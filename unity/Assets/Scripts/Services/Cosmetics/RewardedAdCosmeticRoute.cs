using System;
using CatMetro.Services.Ads;

namespace CatMetro.Services.Cosmetics
{
    // Presentation-safe bridge: ownership remains solely in RewardedAdCoordinator/PurchaseService.
    public sealed class RewardedAdCosmeticRoute : ICosmeticRewardedRoute, IDisposable
    {
        private Binding _binding;
        private long _bindingGeneration;
        private IRewardedAds _pendingSource;
        private long _pendingBindingGeneration;
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
            var binding = Resolve();
            var source = binding?.Source as IRewardedAdExactCompletionSource;
            if (source == null || !IsLive(binding)) return false;

            bool canShow;
            try { canShow = source.CanShow(placementId, entitlementId); }
            catch { return false; }
            return canShow && IsLive(binding);
        }

        public void Request(string placementId, string entitlementId,
            Action<CosmeticRewardedCompletion> completed)
        {
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
                    _pendingBindingGeneration = 0L;
                }
                try { completed?.Invoke(outcome); }
                catch { }
            };
            if (_pendingFinish != null)
            {
                finish(CosmeticRewardedCompletion.NotGranted);
                return;
            }

            var binding = Resolve();
            var source = binding?.Source as IRewardedAdExactCompletionSource;
            if (source == null || !IsLive(binding))
            {
                finish(CosmeticRewardedCompletion.NotGranted);
                return;
            }

            bool canShow;
            try { canShow = source.CanShow(placementId, entitlementId); }
            catch
            {
                finish(CosmeticRewardedCompletion.NotGranted);
                return;
            }
            if (!canShow || !IsLive(binding))
            {
                finish(CosmeticRewardedCompletion.NotGranted);
                return;
            }

            RewardedShowOutcome shown;
            try
            {
                _pendingSource = binding.Source;
                _pendingBindingGeneration = binding.Generation;
                _pendingFinish = finish;
                shown = source.Show(placementId, entitlementId, result =>
                {
                    bool ownsRequest = ReferenceEquals(_pendingFinish, finish) &&
                        ReferenceEquals(_pendingSource, binding.Source) &&
                        _pendingBindingGeneration == binding.Generation;
                    bool exact = ownsRequest && IsLive(binding) && result.AttemptId > 0L &&
                        string.Equals(result.PlacementId, placementId, StringComparison.Ordinal) &&
                        string.Equals(result.EntitlementId, entitlementId,
                            StringComparison.Ordinal);
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
            if (!IsLive(binding) || shown != RewardedShowOutcome.Started)
                finish(CosmeticRewardedCompletion.NotGranted);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            RewardedAdRuntime.Changed -= OnRuntimeChanged;
            Rebind(null);
        }

        private Binding Resolve()
        {
            Rebind(RewardedAdRuntime.Current);
            return _binding;
        }

        private void OnRuntimeChanged() => Rebind(RewardedAdRuntime.Current);

        private void Rebind(IRewardedAds next)
        {
            if (_disposed) next = null;
            var previous = _binding;
            if (previous != null && ReferenceEquals(previous.Source, next)) return;

            long generation;
            unchecked { generation = ++_bindingGeneration; }
            var replacement = new Binding(this, next, generation);

            // Publish desired ownership before crossing any callback-capable boundary. A nested
            // Rebind can replace this token; every continuation below then fails its token check.
            _binding = replacement;
            if (_pendingSource != null && !ReferenceEquals(_pendingSource, next))
            {
                var pending = _pendingFinish;
                pending?.Invoke(CosmeticRewardedCompletion.NotGranted);
                if (!Owns(replacement)) return;
            }

            Detach(previous);
            if (!Owns(replacement)) return;
            Attach(replacement);
            if (!Owns(replacement)) return;
            RaiseAvailabilityChanged(replacement);
        }

        private void Attach(Binding binding)
        {
            if (binding?.Source == null || !IsLive(binding)) return;
            binding.Subscription = SubscriptionState.Adding;
            try
            {
                binding.Source.AvailabilityChanged += binding.Handler;
            }
            catch
            {
                // An accessor may attach and then throw. Relinquish visible ownership first so
                // that a synchronous callback during the conservative remove stays inert.
                binding.Subscription = SubscriptionState.None;
                binding.DetachRequested = true;
                SafeRemove(binding);
                return;
            }

            if (!IsLive(binding) || binding.DetachRequested ||
                binding.Subscription != SubscriptionState.Adding)
            {
                // A nested replacement can remove before this add actually attaches. Compensate
                // after the accessor returns, when the late attachment is now observable.
                binding.Subscription = SubscriptionState.None;
                SafeRemove(binding);
                return;
            }
            binding.Subscription = SubscriptionState.Attached;
        }

        private static void Detach(Binding binding)
        {
            if (binding?.Source == null) return;
            binding.DetachRequested = true;
            if (binding.Subscription != SubscriptionState.Attached) return;

            // Commit loss of ownership before invoking the remove accessor. The captured handler
            // is therefore inert even if the source invokes it synchronously while removing.
            binding.Subscription = SubscriptionState.None;
            SafeRemove(binding);
        }

        private static void SafeRemove(Binding binding)
        {
            try { binding.Source.AvailabilityChanged -= binding.Handler; }
            catch { }
        }

        private void OnAvailabilityChanged(Binding binding)
        {
            if (!IsLive(binding) ||
                (binding.Subscription != SubscriptionState.Adding &&
                 binding.Subscription != SubscriptionState.Attached))
                return;
            RaiseAvailabilityChanged(binding);
        }

        private void RaiseAvailabilityChanged(Binding expected)
        {
            if (!IsLive(expected)) return;
            var handlers = AvailabilityChanged;
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                try { handler(); }
                catch { }
                if (!IsLive(expected)) return;
            }
        }

        private bool Owns(Binding binding)
            => binding != null && ReferenceEquals(_binding, binding) &&
               _bindingGeneration == binding.Generation;

        private bool IsLive(Binding binding)
            => !_disposed && Owns(binding) && binding.Source != null &&
               ReferenceEquals(binding.Source, RewardedAdRuntime.Current);

        private enum SubscriptionState
        {
            None,
            Adding,
            Attached,
        }

        private sealed class Binding
        {
            public IRewardedAds Source { get; }
            public long Generation { get; }
            public Action Handler { get; }
            public SubscriptionState Subscription { get; set; }
            public bool DetachRequested { get; set; }

            public Binding(RewardedAdCosmeticRoute owner, IRewardedAds source, long generation)
            {
                Source = source;
                Generation = generation;
                Handler = () => owner.OnAvailabilityChanged(this);
            }
        }
    }
}
