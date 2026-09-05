using System;
using CatMetro.Services.Ads;

namespace CatMetro.Services.Retry
{
    // Gameplay owns the request token; the coordinator owns provider attribution and durable caps.
    public sealed class RewardedAdFailureRewindRoute : IFailureRewindRewardedRoute, IDisposable
    {
        private Binding _binding;
        private long _bindingGeneration;
        private RequestToken _activeRequest;
        private bool _disposed;

        public event Action AvailabilityChanged;

        public RewardedAdFailureRewindRoute()
        {
            RewardedAdRuntime.Changed += OnRuntimeChanged;
            Rebind(RewardedAdRuntime.Current);
        }

        public bool CanOffer(string placementId)
        {
            var binding = Resolve();
            var source = binding?.Source as IRewardedAdFailureRewindSource;
            if (_activeRequest != null || source == null || !IsLive(binding)) return false;
            try { return source.CanShowFailureRewind(placementId) && IsLive(binding); }
            catch { return false; }
        }

        public void Request(string placementId, Action<RewardedAdEvent> providerLifecycle,
            Action<FailureRewindAdCompletion> completed)
        {
            var request = new RequestToken(placementId, providerLifecycle, completed);
            if (_activeRequest != null)
            {
                Complete(request, Result(request, RewardedAdCompletionKind.Unavailable));
                return;
            }
            // Claim before resolving or asking readiness: either boundary can reenter this route.
            _activeRequest = request;
            var binding = Resolve();
            request.Binding = binding;
            var source = binding?.Source as IRewardedAdFailureRewindSource;
            if (!Owns(request) || source == null || !IsLive(binding))
            {
                Complete(request, Result(request, RewardedAdCompletionKind.Unavailable));
                return;
            }
            bool available;
            try { available = source.CanShowFailureRewind(placementId); }
            catch { available = false; }
            if (!Owns(request) || !available || !IsLive(binding))
            {
                Complete(request, Result(request, RewardedAdCompletionKind.Unavailable));
                return;
            }
            long attemptId = 0L;
            // The coordinator assigns its out ID before crossing the provider boundary. Read
            // that live slot if runtime replacement/cancellation happens inside synchronous Show.
            request.ReadPendingAttemptId = () => attemptId;
            RewardedShowOutcome shown;
            try
            {
                shown = source.ShowFailureRewind(placementId,
                    adEvent => OnProviderLifecycle(request, adEvent),
                    result => OnCompleted(request, result), out attemptId);
            }
            catch
            {
                request.AttemptId = attemptId > 0 ? attemptId : request.AttemptId;
                CancelRequest(request);
                return;
            }
            request.ShowReturned = true;
            if (request.AttemptId > 0 && request.AttemptId != attemptId)
            {
                CancelRequest(request);
                request.AttemptId = attemptId;
                Abandon(request);
                return;
            }
            request.AttemptId = attemptId;
            if (request.AbandonRequested) Abandon(request);
            if (!Owns(request)) return;
            if (!IsLive(binding))
            {
                CancelRequest(request);
                return;
            }
            if (request.PendingCompletion.HasValue)
                OnCompleted(request, request.PendingCompletion.Value);
            if (Owns(request) && shown != RewardedShowOutcome.Started)
                Complete(request, Result(request, RewardedAdCompletionKind.Unavailable), abandon: true);
        }

        public void Cancel() => CancelRequest(_activeRequest);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            RewardedAdRuntime.Changed -= OnRuntimeChanged;
            Rebind(null);
        }

        private void OnProviderLifecycle(RequestToken request, RewardedAdEvent adEvent)
        {
            if (!Owns(request) || !IsLive(request.Binding) || request.Displayed ||
                adEvent.Kind != RewardedAdEventKind.Displayed || adEvent.AttemptId <= 0 ||
                !string.Equals(adEvent.PlacementId, request.PlacementId, StringComparison.Ordinal) ||
                (request.AttemptId > 0 && request.AttemptId != adEvent.AttemptId)) return;
            request.AttemptId = adEvent.AttemptId;
            request.Displayed = true;
            request.LastDisplayed = adEvent;
            try { request.ProviderLifecycle?.Invoke(adEvent); }
            catch { }
        }

        private void OnCompleted(RequestToken request, FailureRewindAdCompletion result)
        {
            if (!Owns(request)) return;
            // A provider can reward synchronously inside Show. Validate its returned attempt ID
            // before delivering that terminal result to gameplay.
            if (!request.ShowReturned)
            {
                if (!request.PendingCompletion.HasValue) request.PendingCompletion = result;
                return;
            }
            bool exact = IsLive(request.Binding) && result.AttemptId == request.AttemptId &&
                string.Equals(result.PlacementId, request.PlacementId, StringComparison.Ordinal) &&
                (result.AttemptId > 0 || result.Kind == RewardedAdCompletionKind.Unavailable);
            if (!exact) CancelRequest(request);
            else Complete(request, result);
        }

        private void CancelRequest(RequestToken request)
        {
            if (request == null || request.Completed) return;
            if (request.AttemptId <= 0) request.AttemptId = request.ReadPendingAttemptId?.Invoke() ?? 0L;
            Complete(request, Result(request, RewardedAdCompletionKind.Cancelled), abandon: true);
        }

        private static FailureRewindAdCompletion Result(RequestToken request,
            RewardedAdCompletionKind kind)
            => new FailureRewindAdCompletion(request.AttemptId, request.PlacementId, kind,
                request.LastDisplayed.AdUnitId, request.LastDisplayed.NetworkName,
                request.LastDisplayed.ErrorCode);

        private void Complete(RequestToken request, FailureRewindAdCompletion result, bool abandon = false)
        {
            if (request == null || request.Completed) return;
            request.Completed = true;
            if (ReferenceEquals(_activeRequest, request)) _activeRequest = null;
            if (abandon)
            {
                request.AbandonRequested = true;
                Abandon(request);
            }
            try { request.CompletedCallback?.Invoke(result); }
            catch { }
        }

        private static void Abandon(RequestToken request)
        {
            if (request.AttemptId <= 0 || request.AbandonedAttemptId == request.AttemptId) return;
            request.AbandonedAttemptId = request.AttemptId;
            try
            {
                (request.Binding?.Source as IRewardedAdFailureRewindSource)?
                    .AbandonFailureRewind(request.AttemptId);
            }
            catch { }
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
            _binding = replacement;
            var active = _activeRequest;
            if (active?.Binding?.Source != null && !ReferenceEquals(active.Binding.Source, next))
                CancelRequest(active);
            // Cancellation can install another runtime. Detach the old binding even then.
            Detach(previous);
            if (!Owns(replacement)) return;
            Attach(replacement);
            if (Owns(replacement)) RaiseAvailabilityChanged(replacement);
        }

        private void Attach(Binding binding)
        {
            if (binding?.Source == null || !IsLive(binding)) return;
            binding.Subscription = SubscriptionState.Adding;
            try { binding.Source.AvailabilityChanged += binding.Handler; }
            catch
            {
                binding.Subscription = SubscriptionState.None;
                binding.DetachRequested = true;
                SafeRemove(binding);
                return;
            }
            if (!IsLive(binding) || binding.DetachRequested ||
                binding.Subscription != SubscriptionState.Adding)
            {
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
            if (!IsLive(binding) || (binding.Subscription != SubscriptionState.Adding &&
                binding.Subscription != SubscriptionState.Attached)) return;
            RaiseAvailabilityChanged(binding);
        }

        private void RaiseAvailabilityChanged(Binding binding)
        {
            if (!IsLive(binding)) return;
            var handlers = AvailabilityChanged;
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                try { handler(); }
                catch { }
                if (!IsLive(binding)) return;
            }
        }

        private bool Owns(Binding binding)
            => binding != null && ReferenceEquals(_binding, binding) &&
                _bindingGeneration == binding.Generation;
        private bool Owns(RequestToken request)
            => request != null && !request.Completed && ReferenceEquals(_activeRequest, request);
        private bool IsLive(Binding binding)
            => !_disposed && Owns(binding) && binding.Source != null &&
                ReferenceEquals(binding.Source, RewardedAdRuntime.Current);

        private enum SubscriptionState { None, Adding, Attached }

        private sealed class Binding
        {
            public readonly IRewardedAds Source;
            public readonly long Generation;
            public readonly Action Handler;
            public SubscriptionState Subscription;
            public bool DetachRequested;
            public Binding(RewardedAdFailureRewindRoute owner, IRewardedAds source, long generation)
            {
                Source = source;
                Generation = generation;
                Handler = () => owner.OnAvailabilityChanged(this);
            }
        }

        private sealed class RequestToken
        {
            public readonly string PlacementId;
            public readonly Action<RewardedAdEvent> ProviderLifecycle;
            public readonly Action<FailureRewindAdCompletion> CompletedCallback;
            public Binding Binding;
            public Func<long> ReadPendingAttemptId;
            public long AttemptId, AbandonedAttemptId;
            public bool Completed, Displayed, ShowReturned, AbandonRequested;
            public RewardedAdEvent LastDisplayed;
            public FailureRewindAdCompletion? PendingCompletion;
            public RequestToken(string placementId, Action<RewardedAdEvent> providerLifecycle,
                Action<FailureRewindAdCompletion> completed)
            {
                PlacementId = placementId;
                ProviderLifecycle = providerLifecycle;
                CompletedCallback = completed;
            }
        }
    }
}
