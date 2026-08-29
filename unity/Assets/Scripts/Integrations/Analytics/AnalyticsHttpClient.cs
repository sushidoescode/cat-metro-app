using System;
using UnityEngine.Networking;

namespace CatMetro.Integrations.Analytics
{
    public readonly struct AnalyticsHttpResponse
    {
        public readonly bool RequestSucceeded;
        public readonly long StatusCode;
        public readonly string Body;

        public AnalyticsHttpResponse(bool requestSucceeded, long statusCode, string body)
        {
            RequestSucceeded = requestSucceeded;
            StatusCode = statusCode;
            Body = body ?? "";
        }

        public bool IsHttpSuccess => RequestSucceeded && StatusCode >= 200L
            && StatusCode <= 299L;
    }

    public interface IAnalyticsHttpClient : IDisposable
    {
        bool IsBusy { get; }
        bool TrySend(UnityWebRequest request, Action<AnalyticsHttpResponse> completed);
        void Abort();
    }

    // Owns exactly one UnityWebRequest. SendWebRequest is asynchronous; every successful start
    // completes once on Unity's main thread. Abort deliberately suppresses the stale callback.
    public sealed class UnityAnalyticsHttpClient : IAnalyticsHttpClient
    {
        private UnityWebRequest _request;
        private Action<AnalyticsHttpResponse> _completed;
        private bool _disposed;

        public bool IsBusy => _request != null;

        public bool TrySend(UnityWebRequest request, Action<AnalyticsHttpResponse> completed)
        {
            if (_disposed || request == null || completed == null || IsBusy) return false;
            try
            {
                _request = request;
                _completed = completed;
                var operation = request.SendWebRequest();
                operation.completed += _ => Complete(request);
                return true;
            }
            catch
            {
                _request = null;
                _completed = null;
                try { request.Dispose(); } catch { }
                return false;
            }
        }

        private void Complete(UnityWebRequest request)
        {
            if (!ReferenceEquals(request, _request)) return;
            var callback = _completed;
            _request = null;
            _completed = null;
            AnalyticsHttpResponse response;
            try
            {
                response = new AnalyticsHttpResponse(
                    request.result == UnityWebRequest.Result.Success,
                    request.responseCode,
                    request.downloadHandler?.text);
            }
            catch
            {
                response = new AnalyticsHttpResponse(false, 0L, "");
            }
            finally
            {
                try { request.Dispose(); } catch { }
            }
            try { callback?.Invoke(response); } catch { }
        }

        public void Abort()
        {
            var request = _request;
            _request = null;
            _completed = null;
            try { request?.Abort(); } catch { }
            try { request?.Dispose(); } catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Abort();
        }
    }
}
