using System;
using System.Collections.Generic;
using CatMetro.Services.Purchases;

namespace CatMetro.Services.Ads
{
    // Owns the policy around one reusable rewarded ad. Vendor adapters only translate callbacks;
    // this class owns attempt IDs, reload decisions, exact reward attribution, and caps.
    public sealed class RewardedAdCoordinator : IRewardedAds, IRewardedAdExactCompletionSource,
        IDisposable
    {
        // A callback that never arrives cannot retain memory forever. FIFO eviction releases the
        // placement but deliberately discards any later reward for that evicted exact attempt.
        private const int MaxRetainedClosedAttempts = 16;
        private const double InitialLoadRetryDelaySeconds = 2d;
        private const double MaxLoadRetryDelaySeconds = 30d;
        private const double RetainedAttemptGraceSeconds = 300d;

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
        private SubscriptionState _providerSubscription;
        private SubscriptionState _reporterSubscription;
        private double _lastMonotonicSeconds;
        private double _nextLoadRetrySeconds;
        private double _pendingLoadRetryDelaySeconds;
        private int _consecutiveLoadFailures;
        private bool _hasMonotonicSeconds;
        private bool _loadRetryPending;
        private bool _loadRetryDeadlineAnchored;
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
                _providerSubscription = SubscriptionState.Adding;
                bool addCompleted = false;
                try
                {
                    _provider.EventReceived += OnProviderEvent;
                    addCompleted = true;
                }
                catch
                {
                    CompensateProviderAddFailure();
                    if (!_disposed) _providerFailed = true;
                }
                if (!addCompleted)
                {
                    if (_disposed) return;
                }
                else if (_disposed || _providerSubscription != SubscriptionState.Adding)
                {
                    // Dispose can run inside the add accessor before the accessor attaches the
                    // handler. Its first remove then precedes the attachment, so compensate once
                    // the accessor returns without touching any other coordinator/provider.
                    SafeRemoveProviderHandler();
                    return;
                }
                else
                {
                    _providerSubscription = SubscriptionState.Attached;
                }
            }
            if (_reporter != null)
            {
                _reporterSubscription = SubscriptionState.Adding;
                bool addCompleted = false;
                try
                {
                    _reporter.ReadinessChanged += OnReporterReadinessChanged;
                    addCompleted = true;
                }
                catch
                {
                    CompensateReporterAddFailure();
                    if (!_disposed) _reporterFailed = true;
                }
                if (!addCompleted)
                {
                    if (_disposed) return;
                }
                else if (_disposed || _reporterSubscription != SubscriptionState.Adding)
                {
                    SafeRemoveReporterHandler();
                    return;
                }
                else
                {
                    _reporterSubscription = SubscriptionState.Attached;
                }
            }
            ObserveReporterReadiness(force: true);
        }

        public void Tick(double monotonicSeconds)
        {
            if (_disposed || !IsValidMonotonicSeconds(monotonicSeconds) ||
                (_hasMonotonicSeconds && monotonicSeconds < _lastMonotonicSeconds))
                return;

            _lastMonotonicSeconds = monotonicSeconds;
            _hasMonotonicSeconds = true;
            bool loadRetryDue = false;
            if (_loadRetryPending && !_loadRetryDeadlineAnchored)
            {
                // The callback may have waited in the main-thread queue across a long stall.
                // This observation frame starts the full delay and is never itself due.
                _nextLoadRetrySeconds = AddSaturated(monotonicSeconds,
                    _pendingLoadRetryDelaySeconds);
                _loadRetryDeadlineAnchored = true;
            }
            else if (_loadRetryPending && monotonicSeconds >= _nextLoadRetrySeconds)
            {
                loadRetryDue = true;
                // Clear ownership before crossing the provider boundary. A synchronous
                // LoadFailed callback can now schedule the next delayed retry without recursion.
                _loadRetryPending = false;
                _loadRetryDeadlineAnchored = false;
                _nextLoadRetrySeconds = 0d;
                _pendingLoadRetryDelaySeconds = 0d;
            }

            bool retainedAttemptExpired = ExpireRetainedAttempts(monotonicSeconds);
            if (loadRetryDue) SafeLoad();
            if (retainedAttemptExpired && !_disposed) RaiseAvailabilityChanged();
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

            try
            {
                if (_provider is IRewardedAdPlacementReadiness placementReadiness)
                    return placementReadiness.IsReadyForPlacement(placementId);
                return _provider.IsReady;
            }
            catch
            {
                _providerFailed = true;
                RaiseAvailabilityChanged();
                return false;
            }
        }

        public RewardedShowOutcome Show(string placementId)
        {
            return ShowCore(placementId, null, null);
        }

        public bool CanShow(string placementId, string entitlementId)
        {
            if (string.IsNullOrEmpty(entitlementId) || _placements == null ||
                !_placements.TryGet(placementId, out var placement) ||
                !string.Equals(placement.EntitlementId, entitlementId, StringComparison.Ordinal))
                return false;
            return CanShow(placementId);
        }

        public RewardedShowOutcome Show(string placementId, string entitlementId,
            Action<RewardedAdCompletion> completed)
        {
            if (_openAttempt != null)
            {
                CompleteImmediate(completed, placementId, entitlementId,
                    RewardedAdCompletionKind.Unavailable);
                return RewardedShowOutcome.Busy;
            }
            if (!CanShow(placementId, entitlementId))
            {
                CompleteImmediate(completed, placementId, entitlementId,
                    RewardedAdCompletionKind.Unavailable);
                return RewardedShowOutcome.Unavailable;
            }
            return ShowCore(placementId, entitlementId, completed);
        }

        private RewardedShowOutcome ShowCore(string placementId, string requestedEntitlementId,
            Action<RewardedAdCompletion> completed)
        {
            if (_openAttempt != null)
            {
                CompleteImmediate(completed, placementId, requestedEntitlementId,
                    RewardedAdCompletionKind.Unavailable);
                return RewardedShowOutcome.Busy;
            }
            if (!CanShow(placementId))
            {
                CompleteImmediate(completed, placementId, requestedEntitlementId,
                    RewardedAdCompletionKind.Unavailable);
                return RewardedShowOutcome.Unavailable;
            }
            if (!_placements.TryGet(placementId, out var placement))
            {
                CompleteImmediate(completed, placementId, requestedEntitlementId,
                    RewardedAdCompletionKind.Unavailable);
                return RewardedShowOutcome.Unavailable;
            }

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
                CompleteImmediate(completed, placementId, requestedEntitlementId,
                    RewardedAdCompletionKind.Unavailable);
                return RewardedShowOutcome.Unavailable;
            }

            var attempt = new Attempt(attemptId, placement, completed);
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

            // A provider may synchronously report a terminal display failure before returning
            // false. That callback already cleared this exact attempt and owns the reload.
            if (!ReferenceEquals(_openAttempt, attempt))
                return RewardedShowOutcome.Unavailable;

            if (ReferenceEquals(_openAttempt, attempt)) _openAttempt = null;
            RemoveRetained(attempt);
            _pendingPlacements.Remove(placement.Id);
            SafeLoad();
            RaiseAvailabilityChanged();
            CompleteAttempt(attempt, RewardedAdCompletionKind.DisplayFailed);
            return RewardedShowOutcome.Unavailable;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_providerSubscription != SubscriptionState.None)
            {
                // Clear ownership before the external remove accessor so reentrant disposal is
                // idempotent. An in-flight add is compensated again after that add returns.
                _providerSubscription = SubscriptionState.None;
                SafeRemoveProviderHandler();
            }
            if (_reporterSubscription != SubscriptionState.None)
            {
                _reporterSubscription = SubscriptionState.None;
                SafeRemoveReporterHandler();
            }
            CompleteAttempt(_openAttempt, RewardedAdCompletionKind.Cancelled);
            foreach (var retained in _retainedAttempts.Values)
                CompleteAttempt(retained, RewardedAdCompletionKind.Cancelled);
            _openAttempt = null;
            _retainedAttempts.Clear();
            _retainedOrder.Clear();
            _pendingPlacements.Clear();
            CancelLoadRetry();
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
                if (_disposed) return;
                _reporterFailed = true;
                _reporterReady = false;
                RaiseAvailabilityChanged();
                return;
            }
            if (_disposed) return;

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
                    }
                    catch
                    {
                        if (_disposed) return;
                        _providerFailed = true;
                    }
                    if (_disposed) return;
                    if (!_providerFailed && !SafeLoad()) return;
                }
                else if (changed && !force)
                {
                    if (!SafeLoad()) return;
                }
            }
            if (changed || force) RaiseAvailabilityChanged();
        }

        private void SafeRemoveProviderHandler()
        {
            if (_provider == null) return;
            try { _provider.EventReceived -= OnProviderEvent; }
            catch { }
        }

        private void CompensateProviderAddFailure()
        {
            // An add accessor may attach and then throw. Retain visible ownership while invoking
            // the conservative remove so a synchronous Dispose can finish the same cleanup; only
            // the invocation that still owns Removing relinquishes it after the boundary returns.
            if (_providerSubscription == SubscriptionState.Adding)
                _providerSubscription = SubscriptionState.Removing;
            SafeRemoveProviderHandler();
            if (_providerSubscription == SubscriptionState.Removing)
                _providerSubscription = SubscriptionState.None;
        }

        private void SafeRemoveReporterHandler()
        {
            if (_reporter == null) return;
            try { _reporter.ReadinessChanged -= OnReporterReadinessChanged; }
            catch { }
        }

        private void CompensateReporterAddFailure()
        {
            if (_reporterSubscription == SubscriptionState.Adding)
                _reporterSubscription = SubscriptionState.Removing;
            SafeRemoveReporterHandler();
            if (_reporterSubscription == SubscriptionState.Removing)
                _reporterSubscription = SubscriptionState.None;
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
                        OnRewarded(adEvent);
                        break;
                    case RewardedAdEventKind.Closed:
                        OnClosed(adEvent);
                        break;
                    case RewardedAdEventKind.DisplayFailed:
                        OnDisplayFailed(adEvent);
                        break;
                    case RewardedAdEventKind.Loaded:
                        CancelLoadRetry();
                        RaiseAvailabilityChanged();
                        break;
                    case RewardedAdEventKind.LoadFailed:
                        ScheduleLoadRetry();
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

        private void OnRewarded(RewardedAdEvent adEvent)
        {
            var attempt = FindAttempt(adEvent.AttemptId, adEvent.PlacementId);
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
            CompleteAttempt(attempt, outcome == AdGrantOutcome.Granted
                ? RewardedAdCompletionKind.Granted
                : RewardedAdCompletionKind.GrantFailed);
            RaiseAvailabilityChanged();
        }

        private void OnClosed(RewardedAdEvent adEvent)
        {
            if (_openAttempt == null || _openAttempt.Id != adEvent.AttemptId ||
                !string.Equals(_openAttempt.Placement.Id, adEvent.PlacementId,
                    StringComparison.Ordinal))
                return;
            var attempt = _openAttempt;
            _openAttempt = null;
            if (attempt.RewardLatched)
                _pendingPlacements.Remove(attempt.Placement.Id);
            else
                RetainClosed(attempt);
            if (!attempt.RewardLatched)
                CompleteAttempt(attempt, RewardedAdCompletionKind.ClosedWithoutReward);
            SafeLoad();
            RaiseAvailabilityChanged();
        }

        private void OnDisplayFailed(RewardedAdEvent adEvent)
        {
            var attempt = FindAttempt(adEvent.AttemptId, adEvent.PlacementId);
            if (attempt == null) return;
            if (ReferenceEquals(attempt, _openAttempt)) _openAttempt = null;
            CompleteAttempt(attempt, RewardedAdCompletionKind.DisplayFailed);
            RemoveRetained(attempt);
            _pendingPlacements.Remove(attempt.Placement.Id);
            SafeLoad();
            RaiseAvailabilityChanged();
        }

        private Attempt FindAttempt(long attemptId, string placementId)
        {
            Attempt attempt = null;
            if (_openAttempt != null && _openAttempt.Id == attemptId) attempt = _openAttempt;
            else _retainedAttempts.TryGetValue(attemptId, out attempt);
            return attempt != null && string.Equals(attempt.Placement.Id, placementId,
                StringComparison.Ordinal) ? attempt : null;
        }

        private static void CompleteImmediate(Action<RewardedAdCompletion> completed,
            string placementId, string entitlementId, RewardedAdCompletionKind kind)
        {
            try { completed?.Invoke(new RewardedAdCompletion(0L, placementId, entitlementId, kind)); }
            catch { }
        }

        private static void CompleteAttempt(Attempt attempt, RewardedAdCompletionKind kind)
        {
            if (attempt == null || attempt.CompletionLatched) return;
            attempt.CompletionLatched = true;
            var completed = attempt.Completed;
            attempt.Completed = null;
            try
            {
                completed?.Invoke(new RewardedAdCompletion(attempt.Id, attempt.Placement.Id,
                    attempt.Placement.EntitlementId, kind));
            }
            catch { }
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
            // Closed is a callback, not a clock sample. The next valid Tick anchors the grace
            // period so a queued callback cannot inherit a stale pre-background timestamp.
            attempt.RetainedNode = _retainedOrder.AddLast(attempt.Id);
            _retainedAttempts[attempt.Id] = attempt;
        }

        private bool ExpireRetainedAttempts(double monotonicSeconds)
        {
            bool removedAny = false;
            var node = _retainedOrder.First;
            while (node != null)
            {
                var next = node.Next;
                long attemptId = node.Value;
                if (!_retainedAttempts.TryGetValue(attemptId, out var attempt))
                {
                    node = next;
                    continue;
                }
                if (!attempt.RetainedDeadlineAnchored)
                {
                    attempt.RetainedUntilMonotonicSeconds = AddSaturated(
                        monotonicSeconds, RetainedAttemptGraceSeconds);
                    attempt.RetainedDeadlineAnchored = true;
                    node = next;
                    continue;
                }
                if (monotonicSeconds >= attempt.RetainedUntilMonotonicSeconds)
                {
                    RemoveRetained(attempt);
                    _pendingPlacements.Remove(attempt.Placement.Id);
                    removedAny = true;
                }
                node = next;
            }
            return removedAny;
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

        private bool SafeLoad()
        {
            if (_disposed || !_reporterReady || _reporterFailed || _providerFailed ||
                _provider == null) return !_disposed;
            try { _provider.Load(); }
            catch
            {
                if (_disposed) return false;
                _providerFailed = true;
            }
            return !_disposed;
        }

        private void ScheduleLoadRetry()
        {
            if (_disposed || _providerFailed || _loadRetryPending) return;

            double delay = LoadRetryDelay(_consecutiveLoadFailures);
            if (_consecutiveLoadFailures < int.MaxValue) _consecutiveLoadFailures++;
            _pendingLoadRetryDelaySeconds = delay;
            // Event delivery can lag the last Tick. Always let the next frame anchor this delay.
            _loadRetryDeadlineAnchored = false;
            _nextLoadRetrySeconds = 0d;
            _loadRetryPending = true;
        }

        private void CancelLoadRetry()
        {
            _loadRetryPending = false;
            _loadRetryDeadlineAnchored = false;
            _nextLoadRetrySeconds = 0d;
            _pendingLoadRetryDelaySeconds = 0d;
            _consecutiveLoadFailures = 0;
        }

        private static double LoadRetryDelay(int consecutiveLoadFailures)
        {
            if (consecutiveLoadFailures <= 0) return InitialLoadRetryDelaySeconds;
            if (consecutiveLoadFailures >= 4) return MaxLoadRetryDelaySeconds;
            return InitialLoadRetryDelaySeconds * (1 << consecutiveLoadFailures);
        }

        private static bool IsValidMonotonicSeconds(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;

        private static double AddSaturated(double value, double increment)
        {
            double sum = value + increment;
            return double.IsInfinity(sum) || sum > double.MaxValue ? double.MaxValue : sum;
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
            public bool CompletionLatched;
            public Action<RewardedAdCompletion> Completed;
            public LinkedListNode<long> RetainedNode;
            public double RetainedUntilMonotonicSeconds;
            public bool RetainedDeadlineAnchored;

            public Attempt(long id, RewardedPlacement placement,
                Action<RewardedAdCompletion> completed)
            {
                Id = id;
                Placement = placement;
                Completed = completed;
            }
        }

        private enum SubscriptionState
        {
            None,
            Adding,
            Attached,
            Removing,
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
