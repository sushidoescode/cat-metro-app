using Newtonsoft.Json.Linq;
using CatMetro.Application.EventTaxonomy;
using CatMetro.Services;

namespace CatMetro.Application.Analytics
{
    public sealed class AnalyticsAppSession
    {
        public const int SessionTimeoutSeconds = 30 * 60;

        private readonly IAnalyticsProfileStore _profileStore;
        private readonly IAnalytics _analytics;
        private readonly System.Func<long> _nowUnixSeconds;
        private readonly System.Func<string> _newSessionId;
        private string _appVersion;
        private string _buildChannel;
        private bool _started;
        private bool _backgrounded;

        public AnalyticsAppSession(IAnalyticsProfileStore profileStore, IAnalytics analytics,
            System.Func<long> nowUnixSeconds, System.Func<string> newSessionId)
        {
            if (profileStore == null || analytics == null || nowUnixSeconds == null
                || newSessionId == null)
                throw new System.ArgumentException(
                    "profile store, analytics, clock and session-id source are required");
            _profileStore = profileStore;
            _analytics = analytics;
            _nowUnixSeconds = nowUnixSeconds;
            _newSessionId = newSessionId;
        }

        public void Start(string appVersion, string deviceTier, string osApiLevel,
            string buildChannel, bool emitFirstOpen = false)
        {
            if (_started) return;
            _started = true;
            _appVersion = appVersion ?? "";
            _buildChannel = buildChannel ?? "";
            long now = _nowUnixSeconds();
            var profile = Profile();
            long createdAt = (long?)profile["createdAtUtc"] ?? 0L;
            bool discoveredFirstOpen = createdAt <= 0L;
            if (discoveredFirstOpen)
            {
                profile["createdAtUtc"] = now;
                createdAt = now;
            }
            if (emitFirstOpen || discoveredFirstOpen)
            {
                SafeLog(Events.FirstOpen(_appVersion, deviceTier ?? "unknown",
                    osApiLevel ?? "unknown"));
            }
            EmitAppOpen(now, createdAt);
        }

        public void OnBackground()
        {
            if (!_started || _backgrounded) return;
            _backgrounded = true;
            Profile()["lastSeenAtUtc"] = _nowUnixSeconds();
            SafeCommit();
        }

        public void OnForeground()
        {
            if (!_started || !_backgrounded) return;
            _backgrounded = false;
            long now = _nowUnixSeconds();
            var profile = Profile();
            long lastSeen = (long?)profile["lastSeenAtUtc"] ?? now;
            long createdAt = (long?)profile["createdAtUtc"] ?? now;
            if (now - lastSeen >= SessionTimeoutSeconds
                || UtcDayNumber(now) > UtcDayNumber(lastSeen))
                EmitAppOpen(now, createdAt);
            else
            {
                profile["lastSeenAtUtc"] = now;
                SafeCommit();
            }
        }

        private static long UtcDayNumber(long unixSeconds) => unixSeconds / 86400L;

        private void EmitAppOpen(long now, long createdAt)
        {
            var profile = Profile();
            int count = (int?)profile["sessionCount"] ?? 0;
            if (count < int.MaxValue) count++;
            profile["sessionCount"] = count;
            profile["lastSeenAtUtc"] = now;
            long ageSeconds = System.Math.Max(0L, now - createdAt);
            int ageDays = (int)System.Math.Min(int.MaxValue, ageSeconds / 86400L);
            SafeLog(Events.AppOpen(_newSessionId() ?? "", _appVersion, ageDays,
                _buildChannel));
            SafeCommit();
        }

        private JObject Profile()
        {
            return _profileStore.Profile
                ?? throw new System.InvalidOperationException("analytics profile is unavailable");
        }

        private void SafeLog(in AnalyticsEvent e)
        {
            if (string.IsNullOrEmpty(e.Name)) return;
            try { _analytics.Log(e); } catch { }
        }

        private void SafeCommit()
        {
            try { _profileStore.RequestCommit(); } catch { }
        }
    }
}
