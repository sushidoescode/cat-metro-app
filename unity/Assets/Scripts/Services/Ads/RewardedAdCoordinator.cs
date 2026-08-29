using System;
using System.Collections.Generic;
using CatMetro.Services.Purchases;

namespace CatMetro.Services.Ads
{
    // Owns the policy around one reusable rewarded ad. Vendor adapters only translate callbacks;
    // this class owns attempt IDs, reload decisions, exact reward attribution, and caps.
    public sealed class RewardedAdCoordinator : IRewardedAds, IDisposable
    {
        // A callback that never arrives cannot retain memory forever. FIFO eviction releases the
        // placement but deliberately discards any later reward for that evicted exact attempt.
        private const int MaxRetainedClosedAttempts = 16;

        private readonly RewardedPlacementCatalog _placements;
        private readonly PurchaseService _purchases;
        private readonly IRewardedAdProvider _provider;
        private readonly IAdEventReporter _reporter;
        private readonly IRewardedAdCapStore _capStore;
        private readonly Func<string> _localDateKey;
        private readonly Dictionary<string, int> _sessionGrantCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<LocalDatePlacementKey, int> _failedLocalDateCounts =
            new Dictionary<LocalDatePlacementKey, int>();
        private readonly Dictionary<string, int> _unscopedFailedLocalDateCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> _pendingPlacements =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<long, Attempt> _retainedAttempts =
            new Dictionary<long, Attempt>();
        private readonly LinkedList<long> _retainedOrder = new LinkedList<long>();

        private Attempt _openAttempt;
        private long _nextAttemptId;
        private bool _started;
        private bool _initialized;
        private bool _reporterReady;
        private bool _reporterFailed;
        private bool _providerFailed;
        private bool _providerSubscribed;
        private bool _reporterSubscribed;
        private bool _disposed;

        public event Action AvailabilityChanged;

        public RewardedAdCoordinator(RewardedPlacementCatalog placements,
            PurchaseService purchases, IRewardedAdProvider provider, IAdEventReporter reporter,
            IRewardedAdCapStore capStore, Func<string> localDateKey)
        {
            _placements = placements;
            _purchases = purchases;
            _provider = provider;
            _reporter = reporter;
            _capStore = capStore;
            _localDateKey = localDateKey;
        }

        public void Start()
        {
            if (_started || _disposed) return;
            _started = true;

            // Subscribe before observing current readiness: the reporter may already be ready,
            // and a transition between observation and subscription must not be lost.
            if (_provider != null)
            {
                try
                {
                    _provider.EventReceived += OnProviderEvent;
                    _providerSubscribed = true;
                }
                catch { _providerFailed = true; }
            }
            if (_reporter != null)
            {
                try
                {
                    _reporter.ReadinessChanged += OnReporterReadinessChanged;
                    _reporterSubscribed = true;
                }
                catch { _reporterFailed = true; }
            }
            ObserveReporterReadiness(force: true);
        }

        public bool CanShow(string placementId)
        {
            if (!_started || _disposed || _providerFailed || _reporterFailed ||
                !_reporterReady || _openAttempt != null || _placements == null ||
                _purchases == null || _provider == null || _reporter == null ||
                _capStore == null || _localDateKey == null ||
                string.IsNullOrEmpty(placementId) ||
                !_placements.TryGet(placementId, out var placement) || !placement.Enabled ||
                _pendingPlacements.Contains(placementId) ||
                !_purchases.CanPersistRewardedAdGrants)
                return false;

            try
            {
                if (!_purchases.CanOfferAdFor(placement.EntitlementId)) return false;
                if (IsCapped(placement)) return false;
            }
            catch
            {
                // Every dependency here represents optional monetization. Malformed catalogue,
                // clock, or persistence state removes the offer rather than entering gameplay.
                return false;
            }

            try { return _provider.IsReady; }
            catch
            {
                _providerFailed = true;
                RaiseAvailabilityChanged();
                return false;
            }
        }

        public RewardedShowOutcome Show(string placementId)
        {
            if (_openAttempt != null) return RewardedShowOutcome.Busy;
            if (!CanShow(placementId)) return RewardedShowOutcome.Unavailable;
            if (!_placements.TryGet(placementId, out var placement))
                return RewardedShowOutcome.Unavailable;

            long attemptId;
            try
            {
                attemptId = checked(_nextAttemptId + 1L);
                _nextAttemptId = attemptId;
            }
            catch
            {
                _providerFailed = true;
                RaiseAvailabilityChanged();
                return RewardedShowOutcome.Unavailable;
            }

            var attempt = new Attempt(attemptId, placement);
            _openAttempt = attempt;
            _pendingPlacements.Add(placement.Id);
            try
            {
                if (_provider.TryShow(attemptId, placement.Id))
                {
                    RaiseAvailabilityChanged();
                    return RewardedShowOutcome.Started;
                }
            }
            catch
            {
                _providerFailed = true;
            }

            if (ReferenceEquals(_openAttempt, attempt)) _openAttempt = null;
            RemoveRetained(attempt);
            _pendingPlacements.Remove(placement.Id);
            SafeLoad();
            RaiseAvailabilityChanged();
            return RewardedShowOutcome.Unavailable;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_providerSubscribed)
            {
                try { _provider.EventReceived -= OnProviderEvent; }
                catch { }
                _providerSubscribed = false;
            }
            if (_reporterSubscribed)
            {
                try { _reporter.ReadinessChanged -= OnReporterReadinessChanged; }
                catch { }
                _reporterSubscribed = false;
            }
            _openAttempt = null;
            _retainedAttempts.Clear();
            _retainedOrder.Clear();
            _pendingPlacements.Clear();
            try { _provider?.Dispose(); }
            catch { }
        }

        private void OnReporterReadinessChanged() => ObserveReporterReadiness(force: false);

        private void ObserveReporterReadiness(bool force)
        {
            if (_disposed || _reporter == null)
            {
                _reporterReady = false;
                return;
            }

            bool ready;
            try { ready = _reporter.IsReady; }
            catch
            {
                _reporterFailed = true;
                _reporterReady = false;
                RaiseAvailabilityChanged();
                return;
            }

            bool changed = ready != _reporterReady;
            _reporterReady = ready;
            if (ready && !_reporterFailed && !_providerFailed)
            {
                if (!_initialized)
                {
                    _initialized = true;
                    try
                    {
                        _provider?.Initialize();
                        SafeLoad();
                    }
                    catch
                    {
                        _providerFailed = true;
                    }
                }
                else if (changed && !force)
                {
                    SafeLoad();
                }
            }
            if (changed || force) RaiseAvailabilityChanged();
        }

        private void OnProviderEvent(RewardedAdEvent adEvent)
        {
            if (_disposed) return;
            try
            {
                if (IsReportable(adEvent.Kind)) SafeReport(adEvent);

                switch (adEvent.Kind)
                {
                    case RewardedAdEventKind.Rewarded:
                        OnRewarded(adEvent.AttemptId);
                        break;
                    case RewardedAdEventKind.Closed:
                        OnClosed(adEvent.AttemptId);
                        break;
                    case RewardedAdEventKind.DisplayFailed:
                        OnDisplayFailed(adEvent.AttemptId);
                        break;
                    case RewardedAdEventKind.Loaded:
                    case RewardedAdEventKind.LoadFailed:
                        RaiseAvailabilityChanged();
                        break;
                }
            }
            catch
            {
                // A vendor callback is an optional boundary. Its exception never reaches the
                // main-thread gameplay caller or undoes a lease already committed by purchases.
                _providerFailed = true;
                RaiseAvailabilityChanged();
            }
        }

        private void OnRewarded(long attemptId)
        {
            var attempt = FindAttempt(attemptId);
            if (attempt == null || attempt.RewardLatched) return;

            // Set first: lease persistence can synchronously publish observers that re-enter the
            // provider fake/native bridge with a duplicate reward callback.
            attempt.RewardLatched = true;
            AdGrantOutcome outcome;
            try { outcome = _purchases.GrantRewardedAdEntitlement(attempt.Placement.EntitlementId); }
            catch { outcome = AdGrantOutcome.PersistenceFailed; }

            if (outcome == AdGrantOutcome.Granted) AdvanceCaps(attempt.Placement);
            if (!ReferenceEquals(attempt, _openAttempt))
            {
                RemoveRetained(attempt);
                _pendingPlacements.Remove(attempt.Placement.Id);
            }
            RaiseAvailabilityChanged();
        }

        private void OnClosed(long attemptId)
        {
            if (_openAttempt == null || _openAttempt.Id != attemptId) return;
            var attempt = _openAttempt;
            _openAttempt = null;
            if (attempt.RewardLatched)
                _pendingPlacements.Remove(attempt.Placement.Id);
            else
                RetainClosed(attempt);
            SafeLoad();
            RaiseAvailabilityChanged();
        }

        private void OnDisplayFailed(long attemptId)
        {
            var attempt = FindAttempt(attemptId);
            if (attempt == null) return;
            if (ReferenceEquals(attempt, _openAttempt)) _openAttempt = null;
            RemoveRetained(attempt);
            _pendingPlacements.Remove(attempt.Placement.Id);
            SafeLoad();
            RaiseAvailabilityChanged();
        }

        private Attempt FindAttempt(long attemptId)
        {
            if (_openAttempt != null && _openAttempt.Id == attemptId) return _openAttempt;
            return _retainedAttempts.TryGetValue(attemptId, out var retained) ? retained : null;
        }

        private void RetainClosed(Attempt attempt)
        {
            while (_retainedAttempts.Count >= MaxRetainedClosedAttempts)
            {
                var oldestNode = _retainedOrder.First;
                if (oldestNode == null) break;
                long oldestId = oldestNode.Value;
                _retainedOrder.RemoveFirst();
                if (_retainedAttempts.TryGetValue(oldestId, out var evicted))
                {
                    _retainedAttempts.Remove(oldestId);
                    evicted.RetainedNode = null;
                    _pendingPlacements.Remove(evicted.Placement.Id);
                }
            }
            attempt.RetainedNode = _retainedOrder.AddLast(attempt.Id);
            _retainedAttempts[attempt.Id] = attempt;
        }

        private void RemoveRetained(Attempt attempt)
        {
            if (attempt == null || attempt.RetainedNode == null) return;
            _retainedOrder.Remove(attempt.RetainedNode);
            attempt.RetainedNode = null;
            _retainedAttempts.Remove(attempt.Id);
        }

        private bool IsCapped(RewardedPlacement placement)
        {
            if (placement.Caps == null) return false;
            for (int i = 0; i < placement.Caps.Count; i++)
            {
                var cap = placement.Caps[i];
                if (cap.Limit <= 0) return true;
                if (string.Equals(cap.Scope, "session", StringComparison.Ordinal))
                {
                    if (Count(_sessionGrantCounts, placement.Id) >= cap.Limit) return true;
                    continue;
                }
                if (string.Equals(cap.Scope, "localDate", StringComparison.Ordinal))
                {
                    string dateKey = _localDateKey();
                    if (string.IsNullOrEmpty(dateKey)) return true;
                    int durable = _capStore.ReadLocalDateCount(placement.Id, dateKey);
                    if (durable < 0) return true;
                    int failed = Count(_failedLocalDateCounts,
                        new LocalDatePlacementKey(dateKey, placement.Id));
                    int unscoped = Count(_unscopedFailedLocalDateCounts, placement.Id);
                    failed = failed > int.MaxValue - unscoped
                        ? int.MaxValue
                        : failed + unscoped;
                    if (durable >= cap.Limit || failed >= cap.Limit - durable) return true;
                    continue;
                }

                // An authored cap scope the runtime does not understand is configuration failure,
                // not permission to ignore the cap.
                return true;
            }
            return false;
        }

        private void AdvanceCaps(RewardedPlacement placement)
        {
            IncrementSaturated(_sessionGrantCounts, placement.Id);
            bool hasLocalDateCap = false;
            if (placement.Caps != null)
            {
                for (int i = 0; i < placement.Caps.Count; i++)
                    if (string.Equals(placement.Caps[i].Scope, "localDate", StringComparison.Ordinal))
                        hasLocalDateCap = true;
            }
            if (!hasLocalDateCap) return;

            bool committed = false;
            string dateKey = null;
            try
            {
                dateKey = _localDateKey();
                if (!string.IsNullOrEmpty(dateKey))
                    committed = _capStore.TryIncrementLocalDateCount(placement.Id, dateKey);
            }
            catch { }
            if (committed) return;
            if (string.IsNullOrEmpty(dateKey))
                IncrementSaturated(_unscopedFailedLocalDateCounts, placement.Id);
            else
                IncrementSaturated(_failedLocalDateCounts,
                    new LocalDatePlacementKey(dateKey, placement.Id));
        }

        private void SafeLoad()
        {
            if (_disposed || !_reporterReady || _reporterFailed || _providerFailed ||
                _provider == null) return;
            try { _provider.Load(); }
            catch { _providerFailed = true; }
        }

        private void SafeReport(RewardedAdEvent adEvent)
        {
            if (!_reporterReady || _reporterFailed || _reporter == null) return;
            try { _reporter.Report(adEvent); }
            catch
            {
                _reporterFailed = true;
                _reporterReady = false;
                RaiseAvailabilityChanged();
            }
        }

        private static bool IsReportable(RewardedAdEventKind kind)
            => kind == RewardedAdEventKind.Loaded || kind == RewardedAdEventKind.Displayed ||
               kind == RewardedAdEventKind.Opened || kind == RewardedAdEventKind.LoadFailed ||
               kind == RewardedAdEventKind.Revenue;

        private static int Count(Dictionary<string, int> counts, string placementId)
            => counts.TryGetValue(placementId, out var count) ? count : 0;

        private static int Count(Dictionary<LocalDatePlacementKey, int> counts,
            LocalDatePlacementKey key)
            => counts.TryGetValue(key, out var count) ? count : 0;

        private static void IncrementSaturated(Dictionary<string, int> counts, string placementId)
        {
            int current = Count(counts, placementId);
            counts[placementId] = current == int.MaxValue ? int.MaxValue : current + 1;
        }

        private static void IncrementSaturated(Dictionary<LocalDatePlacementKey, int> counts,
            LocalDatePlacementKey key)
        {
            int current = Count(counts, key);
            counts[key] = current == int.MaxValue ? int.MaxValue : current + 1;
        }

        private void RaiseAvailabilityChanged()
        {
            var handlers = AvailabilityChanged;
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                try { handler(); }
                catch { }
            }
        }

        private sealed class Attempt
        {
            public readonly long Id;
            public readonly RewardedPlacement Placement;
            public bool RewardLatched;
            public LinkedListNode<long> RetainedNode;

            public Attempt(long id, RewardedPlacement placement)
            {
                Id = id;
                Placement = placement;
            }
        }

        private readonly struct LocalDatePlacementKey : IEquatable<LocalDatePlacementKey>
        {
            private readonly string _dateKey;
            private readonly string _placementId;

            public LocalDatePlacementKey(string dateKey, string placementId)
            {
                _dateKey = dateKey;
                _placementId = placementId;
            }

            public bool Equals(LocalDatePlacementKey other)
                => string.Equals(_dateKey, other._dateKey, StringComparison.Ordinal) &&
                   string.Equals(_placementId, other._placementId, StringComparison.Ordinal);

            public override bool Equals(object obj)
                => obj is LocalDatePlacementKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((_dateKey == null ? 0 : StringComparer.Ordinal.GetHashCode(_dateKey)) *
                            397) ^
                           (_placementId == null
                               ? 0
                               : StringComparer.Ordinal.GetHashCode(_placementId));
                }
            }
        }
    }
}
