using System;
using System.Globalization;
using UnityEngine;
using CatMetro.Application.Analytics;
using CatMetro.Application.Save;
using CatMetro.Content;
using CatMetro.Integrations.Analytics;
using CatMetro.Services;

namespace CatMetro.Bootstrap
{
    public sealed class GameAnalyticsRuntime : IDisposable
    {
        private sealed class DisabledAnalytics : IAnalytics
        {
            public int QueuedEventCount => 0;
            public void Log(in AnalyticsEvent e) { }
            public void SetUserProperty(UserPropertyKey key, string value) { }
        }

        private readonly GameplayAnalytics _gameplay;
        private readonly AnalyticsQueue _queue;
        private readonly AnalyticsAppSession _appSession;
        private readonly PostHogAnalyticsTransport _transport;
        private bool _backgrounded;
        private bool _disposed;

        public GameAnalyticsRuntime(IAnalytics sink)
        {
            Sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _gameplay = new GameplayAnalytics(Sink);
        }

        public GameAnalyticsRuntime(AnalyticsQueue queue, PostHogAnalyticsTransport transport,
            AnalyticsAppSession appSession = null)
        {
            if (queue == null || transport == null)
                throw new ArgumentException("queue and transport are required");
            Sink = queue;
            _queue = queue;
            _appSession = appSession;
            _transport = transport;
            _gameplay = new GameplayAnalytics(queue);
            _transport.RemoteStateChanged += OnRemoteStateChanged;
            _transport.DeliveryRequested += OnDeliveryRequested;
        }

        public IAnalytics Sink { get; }
        public string AnonymousId => _transport?.AnonymousId;

        public static GameAnalyticsRuntime CreateProduction()
        {
            PostHogAnalyticsTransport transport = null;
            try
            {
                var asset = Resources.Load<TextAsset>("Config/analytics_transport");
                if (asset == null) return Disabled();
                var parsedConfig = AnalyticsTransportConfig.Parse(asset.bytes);
                if (!parsedConfig.Ok || !parsedConfig.Value.Enabled) return Disabled();

                var source = new StreamingAssetsContentSource();
                var boundsBytes = source.ReadAsync("config/runtime_bounds.json",
                    System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                var parsedBounds = RuntimeBounds.Parse(boundsBytes);
                if (!parsedBounds.Ok) return Disabled();

                var root = new EngineStorageRoot();
                var fs = new RealSaveFileSystem();
                var profileFile = new AnalyticsProfileFile(root, fs);
                var profile = profileFile.Load();
                var persistenceExecutor = new BackgroundAnalyticsPersistenceExecutor();
                var profileStore = new BufferedAnalyticsProfileStore(profile,
                    profileFile.TryWrite, persistenceExecutor);
                long launchUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (!AnalyticsInstallIdentity.TryPrepareLaunch(profileStore,
                    () => Guid.NewGuid().ToString("N"), launchUnixSeconds,
                    out string anonymousId, out bool firstOpen))
                    return Disabled();
                transport = new PostHogAnalyticsTransport(parsedConfig.Value, anonymousId);
                var queue = new AnalyticsQueue(root, fs, parsedBounds.Value, transport,
                    persistenceExecutor: persistenceExecutor, ownerId: anonymousId);
                var appSession = new AnalyticsAppSession(profileStore, queue,
                    () => DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    () => Guid.NewGuid().ToString("N"));
                var runtime = new GameAnalyticsRuntime(queue, transport, appSession);
                transport.Initialize();
                appSession.Start(UnityEngine.Application.version, DeviceTier(), OsApiLevel(),
                    Debug.isDebugBuild ? "development" : "production", firstOpen);
                queue.OnTrigger("app_foreground");
                return runtime;
            }
            catch (Exception ex)
            {
                try { transport?.Dispose(); } catch { }
                Debug.LogWarning("analytics disabled after safe initialization failure: "
                    + ex.GetType().Name);
                return Disabled();
            }
        }

        private static GameAnalyticsRuntime Disabled() =>
            new GameAnalyticsRuntime(new DisabledAnalytics());

        public void BeginCampaignLevel(ImportedLevel level, bool retry, string fromScreen)
        {
            if (level == null || _disposed) return;
            _gameplay.BeginCampaignLevel(level.Dto.Id, level.Dto.Meta.DifficultyTarget,
                retry, fromScreen);
        }

        public void BeginDailyLevel(ImportedLevel level, string dateKey)
        {
            if (level == null || _disposed) return;
            _gameplay.BeginDailyLevel(level.Dto.Id, level.Dto.Meta.DifficultyTarget,
                level.Dto.Seed, dateKey);
        }

        public void RetryLevel(ImportedLevel level, bool daily)
        {
            if (level == null || _disposed) return;
            _gameplay.BeginLevel(level.Dto.Id,
                daily ? GameplayAnalytics.DailyMode : GameplayAnalytics.CampaignMode,
                level.Dto.Meta.DifficultyTarget, retry: true, fromScreen: "failure_review");
        }

        public void CompleteLevel(ImportedLevel level, CatMetro.Domain.SimulationState state)
        {
            if (level == null || state == null || _disposed) return;
            _gameplay.CompleteLevel(level.Dto.Id, state.Tick, state.SwitchesUsed,
                state.Rejections, state.Overloads, state.Score, level.Dto.Win.PerfectMaxSwitches,
                level.Dto.Win.Stars.Two, level.Dto.Win.Stars.Three);
        }

        public void OnBackground()
        {
            if (_disposed || _backgrounded) return;
            _backgrounded = true;
            try { _appSession?.OnBackground(); } catch { }
            try { _queue?.OnTrigger("app_pause"); } catch { }
            try { _queue?.TryDrainPersistence(20); } catch { }
        }

        public void OnForeground()
        {
            if (_disposed || !_backgrounded) return;
            _backgrounded = false;
            try { _transport?.RefreshRemoteFlag(); } catch { }
            try { _appSession?.OnForeground(); } catch { }
            try { _queue?.OnTrigger("app_foreground"); } catch { }
        }

        public void OnNetworkReachable()
        {
            if (_disposed) return;
            try { _transport?.RefreshRemoteFlag(); } catch { }
            try { _queue?.OnTrigger("network_reachable"); } catch { }
        }

        public void Tick()
        {
            if (_disposed) return;
            try { _queue?.ContinuePendingDelivery(); } catch { }
            try { _transport?.Tick(); } catch { }
        }

        private void OnDeliveryRequested()
        {
            if (_queue == null || _disposed) return;
            try { _queue.OnTrigger("network_reachable"); } catch { }
        }

        private void OnRemoteStateChanged(AnalyticsRemoteState state)
        {
            if (_queue == null || _disposed) return;
            if (state == AnalyticsRemoteState.Disabled)
                _queue.DisableAndDiscard("remote_kill_switch");
            else if (state == AnalyticsRemoteState.Enabled)
            {
                _queue.Enable();
                _queue.OnTrigger("network_reachable");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            OnBackground();
            _disposed = true;
            if (_transport != null)
            {
                _transport.RemoteStateChanged -= OnRemoteStateChanged;
                _transport.DeliveryRequested -= OnDeliveryRequested;
                try { _transport.Dispose(); } catch { }
            }
        }

        private static string DeviceTier()
        {
            int memoryMb = SystemInfo.systemMemorySize;
            if (memoryMb <= 0) return "unknown";
            if (memoryMb < 4096) return "low";
            if (memoryMb < 6144) return "mid";
            return "high";
        }

        private static string OsApiLevel()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return UnityEngine.Android.AndroidInfo.deviceApiLevel
                .ToString(CultureInfo.InvariantCulture);
#elif UNITY_IOS && !UNITY_EDITOR
            string version = UnityEngine.iOS.Device.systemVersion ?? "";
            int dot = version.IndexOf('.');
            string major = dot >= 0 ? version.Substring(0, dot) : version;
            return "ios-" + (major.Length == 0 ? "unknown" : major);
#else
            return "editor";
#endif
        }
    }
}
