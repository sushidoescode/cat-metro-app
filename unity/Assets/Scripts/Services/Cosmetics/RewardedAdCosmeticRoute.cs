using System;
using CatMetro.Services.Ads;

namespace CatMetro.Services.Cosmetics
{
    // Presentation-safe bridge: ownership remains solely in RewardedAdCoordinator/PurchaseService.
    public sealed class RewardedAdCosmeticRoute : ICosmeticRewardedRoute, IDisposable
    {
        private Binding _binding;
        private long _bindingGeneration;
        private RequestToken _activeRequest;
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
            var request = new RequestToken(completed);
            if (_activeRequest != null)
            {
                Complete(request, CosmeticRewardedCompletion.NotGranted);
                return;
            }

            // Claim the route before Resolve or CanShow: both can invoke arbitrary callbacks.
            // A nested Request must fail without replacing or clearing this request's token.
            _activeRequest = request;
            var binding = Resolve();
            request.Binding = binding;
            var source = binding?.Source as IRewardedAdExactCompletionSource;
            if (!Owns(request) || source == null || !IsLive(binding))
            {
                Complete(request, CosmeticRewardedCompletion.NotGranted);
                return;
            }

            bool canShow;
            try { canShow = source.CanShow(placementId, entitlementId); }
            catch
            {
                Complete(request, CosmeticRewardedCompletion.NotGranted);
                return;
            }
            if (!Owns(request) || !canShow || !IsLive(binding))
            {
                Complete(request, CosmeticRewardedCompletion.NotGranted);
                return;
            }

            RewardedShowOutcome shown;
            try
            {
                shown = source.Show(placementId, entitlementId, result =>
                {
                    bool exact = Owns(request) && IsLive(binding) && result.AttemptId > 0L &&
                        string.Equals(result.PlacementId, placementId, StringComparison.Ordinal) &&
                        string.Equals(result.EntitlementId, entitlementId,
                            StringComparison.Ordinal);
                    Complete(request, exact && result.Kind == RewardedAdCompletionKind.Granted
                        ? CosmeticRewardedCompletion.Granted
                        : CosmeticRewardedCompletion.NotGranted);
                });
            }
            catch
            {
                Complete(request, CosmeticRewardedCompletion.NotGranted);
                return;
            }
            if (!Owns(request) || !IsLive(binding) || shown != RewardedShowOutcome.Started)
                Complete(request, CosmeticRewardedCompletion.NotGranted);
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
            var activeRequest = _activeRequest;
            if (activeRequest?.Binding?.Source != null &&
                !ReferenceEquals(activeRequest.Binding.Source, next))
            {
                Complete(activeRequest, CosmeticRewardedCompletion.NotGranted);
            }

            // The callback above may install another binding or dispose the route. It must not
            // strand the exact binding that was current when this rebind began.
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

        private bool Owns(RequestToken request)
            => request != null && !request.Completed &&
               ReferenceEquals(_activeRequest, request);

        private void Complete(RequestToken request, CosmeticRewardedCompletion outcome)
        {
            if (request == null || request.Completed) return;
            request.Completed = true;
            if (ReferenceEquals(_activeRequest, request)) _activeRequest = null;
            try { request.CompletedCallback?.Invoke(outcome); }
            catch { }
        }

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

        private sealed class RequestToken
        {
            public Action<CosmeticRewardedCompletion> CompletedCallback { get; }
            public Binding Binding { get; set; }
            public bool Completed { get; set; }

            public RequestToken(Action<CosmeticRewardedCompletion> completedCallback)
            {
                CompletedCallback = completedCallback;
            }
        }
    }
}
