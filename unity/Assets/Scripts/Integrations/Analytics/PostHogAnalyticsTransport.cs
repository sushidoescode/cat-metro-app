using System;
using System.Collections.Generic;
using CatMetro.Application.Analytics;

namespace CatMetro.Integrations.Analytics
{
    public enum AnalyticsRemoteState
    {
        Unknown = 0,
        Enabled = 1,
        Disabled = 2,
    }

    // Direct hosted-API transport. The application queue is the only durable event owner;
    // this type retains no hidden disk queue and acknowledges only an HTTP 2xx.
    public sealed class PostHogAnalyticsTransport : IAnalyticsTransport, IDisposable
    {
        private enum RequestKind { None, Flag, Batch }

        private const int BatchLimit = 50;
        private const double InitialRetrySeconds = 5d;
        private const double MaximumRetrySeconds = 30d;
        private const double RemoteFlagTtlSeconds = 15d * 60d;

        private readonly AnalyticsTransportConfig _config;
        private readonly string _anonymousId;
        private readonly IAnalyticsHttpClient _http;
        private readonly Func<double> _monotonicSeconds;
        private RequestKind _activeRequest;
        private Action<AnalyticsDeliveryResult> _deliveryCompletion;
        private double _nextFlagRetryAt;
        private double _nextBatchRetryAt;
        private double _nextFlagRefreshAt = double.PositiveInfinity;
        private int _flagFailures;
        private int _batchFailures;
        private bool _flagRetryPending;
        private bool _batchRetryPending;
        private bool _initialized;
        private bool _disposed;

        public PostHogAnalyticsTransport(AnalyticsTransportConfig config, string anonymousId,
            IAnalyticsHttpClient http = null, Func<double> monotonicSeconds = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(anonymousId))
                throw new ArgumentException("anonymous id is required", nameof(anonymousId));
            _anonymousId = anonymousId;
            _http = http ?? new UnityAnalyticsHttpClient();
            _monotonicSeconds = monotonicSeconds ?? (() => UnityEngine.Time.realtimeSinceStartupAsDouble);
        }

        public event Action<AnalyticsRemoteState> RemoteStateChanged;
        public event Action DeliveryRequested;
        public AnalyticsRemoteState RemoteState { get; private set; } =
            AnalyticsRemoteState.Unknown;
        public string AnonymousId => _anonymousId;
        public int MaxBatchSize => BatchLimit;

        public void Initialize()
        {
            if (_initialized || _disposed || !_config.Enabled) return;
            _initialized = true;
            StartFlagRequest();
        }

        public bool TryDeliver(IReadOnlyList<QueuedAnalyticsEvent> batch,
            Action<AnalyticsDeliveryResult> completed)
        {
            if (!_initialized || _disposed || RemoteState != AnalyticsRemoteState.Enabled
                || batch == null || batch.Count == 0 || completed == null)
                return false;

            double now = _monotonicSeconds();
            if (now >= _nextFlagRefreshAt)
            {
                RefreshRemoteFlag();
                return false;
            }
            if (_batchRetryPending)
            {
                if (now < _nextBatchRetryAt) return false;
                _batchRetryPending = false;
            }
            if (_activeRequest != RequestKind.None || _http.IsBusy) return false;

            int count = Math.Min(BatchLimit, batch.Count);
            var prefix = new QueuedAnalyticsEvent[count];
            for (int i = 0; i < count; i++) prefix[i] = batch[i];
            var request = PostHogWireProtocol.CreateBatchRequest(_config, _anonymousId,
                prefix);
            if (request == null) return false;

            _activeRequest = RequestKind.Batch;
            _deliveryCompletion = completed;
            bool started;
            try { started = _http.TrySend(request, OnBatchResponse); }
            catch { started = false; }
            if (started) return true;

            _activeRequest = RequestKind.None;
            _deliveryCompletion = null;
            try { request.Dispose(); } catch { }
            ScheduleBatchRetry();
            return false;
        }

        public void RefreshRemoteFlag()
        {
            if (!_initialized || _disposed) return;
            _flagRetryPending = false;
            _nextFlagRefreshAt = double.PositiveInfinity;
            SetRemoteState(AnalyticsRemoteState.Unknown);
            AbortActiveRequest();
            StartFlagRequest();
        }

        public void Tick()
        {
            if (!_initialized || _disposed) return;
            double now = _monotonicSeconds();
            if (RemoteState == AnalyticsRemoteState.Enabled && now >= _nextFlagRefreshAt)
            {
                RefreshRemoteFlag();
                return;
            }
            if (_activeRequest != RequestKind.None) return;
            if (_flagRetryPending && now >= _nextFlagRetryAt)
            {
                _flagRetryPending = false;
                StartFlagRequest();
                return;
            }
            if (_batchRetryPending && now >= _nextBatchRetryAt
                && RemoteState == AnalyticsRemoteState.Enabled)
            {
                _batchRetryPending = false;
                try { DeliveryRequested?.Invoke(); } catch { }
            }
        }

        private void StartFlagRequest()
        {
            if (!_initialized || _disposed || _activeRequest != RequestKind.None
                || _http.IsBusy)
                return;
            var request = PostHogWireProtocol.CreateKillSwitchRequest(_config, _anonymousId);
            if (request == null)
            {
                ScheduleFlagRetry();
                return;
            }
            _activeRequest = RequestKind.Flag;
            bool started;
            try { started = _http.TrySend(request, OnFlagResponse); }
            catch { started = false; }
            if (started) return;
            _activeRequest = RequestKind.None;
            try { request.Dispose(); } catch { }
            ScheduleFlagRetry();
        }

        private void OnFlagResponse(AnalyticsHttpResponse response)
        {
            if (_disposed || _activeRequest != RequestKind.Flag) return;
            _activeRequest = RequestKind.None;
            if (response.IsHttpSuccess && PostHogWireProtocol.TryParseKillSwitch(response.Body,
                _config.RemoteKillSwitchFlag, out bool enabled))
            {
                _flagRetryPending = false;
                _flagFailures = 0;
                _nextFlagRefreshAt = enabled
                    ? _monotonicSeconds() + RemoteFlagTtlSeconds
                    : double.PositiveInfinity;
                if (!enabled)
                {
                    _batchRetryPending = false;
                    _batchFailures = 0;
                }
                SetRemoteState(enabled ? AnalyticsRemoteState.Enabled
                    : AnalyticsRemoteState.Disabled);
                return;
            }
            _nextFlagRefreshAt = double.PositiveInfinity;
            SetRemoteState(AnalyticsRemoteState.Unknown);
            ScheduleFlagRetry();
        }

        private void OnBatchResponse(AnalyticsHttpResponse response)
        {
            if (_disposed || _activeRequest != RequestKind.Batch) return;
            _activeRequest = RequestKind.None;
            var callback = _deliveryCompletion;
            _deliveryCompletion = null;
            bool accepted = response.IsHttpSuccess;
            if (accepted)
            {
                _batchRetryPending = false;
                _batchFailures = 0;
            }
            else
            {
                ScheduleBatchRetry();
            }
            try { callback?.Invoke(new AnalyticsDeliveryResult(accepted)); } catch { }
        }

        private double RetryDelay(int failures)
        {
            double delay = InitialRetrySeconds
                * Math.Pow(2d, Math.Min(3, Math.Max(0, failures - 1)));
            return Math.Min(MaximumRetrySeconds, delay);
        }

        private void ScheduleFlagRetry()
        {
            _flagFailures++;
            _flagRetryPending = true;
            _nextFlagRetryAt = _monotonicSeconds() + RetryDelay(_flagFailures);
        }

        private void ScheduleBatchRetry()
        {
            _batchFailures++;
            _batchRetryPending = true;
            _nextBatchRetryAt = _monotonicSeconds() + RetryDelay(_batchFailures);
        }

        private void AbortActiveRequest()
        {
            var kind = _activeRequest;
            var callback = _deliveryCompletion;
            _activeRequest = RequestKind.None;
            _deliveryCompletion = null;
            try { _http.Abort(); } catch { }
            if (kind == RequestKind.Batch)
            {
                if (!_disposed) ScheduleBatchRetry();
                try { callback?.Invoke(new AnalyticsDeliveryResult(false)); } catch { }
            }
        }

        private void SetRemoteState(AnalyticsRemoteState state)
        {
            if (RemoteState == state) return;
            RemoteState = state;
            try { RemoteStateChanged?.Invoke(state); } catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _flagRetryPending = false;
            _batchRetryPending = false;
            AbortActiveRequest();
            try { _http.Dispose(); } catch { }
        }
    }
}
