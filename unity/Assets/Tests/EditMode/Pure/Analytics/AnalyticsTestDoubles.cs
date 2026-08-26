using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using CatMetro.Application.Analytics;
using CatMetro.Services;

namespace CatMetro.Tests.Analytics
{
    public sealed class RecordingAnalytics : IAnalytics
    {
        public sealed class Record
        {
            public readonly string Name;
            public readonly JObject Params;

            public Record(string name, JObject parameters)
            {
                Name = name;
                Params = parameters;
            }
        }

        public readonly List<Record> Records = new List<Record>();

        public int QueuedEventCount => Records.Count;

        public void Log(in AnalyticsEvent e) =>
            Records.Add(new Record(e.Name, e.Params == null
                ? new JObject()
                : (JObject)e.Params.DeepClone()));

        public void SetUserProperty(UserPropertyKey key, string value) { }
    }

    public sealed class MutableUnixClock
    {
        public long Seconds;
        public long NowSeconds() => Seconds;
        public long NowMilliseconds() => checked(Seconds * 1000L);
    }

    public sealed class SaveBackedAnalyticsProfileStore : IAnalyticsProfileStore
    {
        private readonly ISave _save;

        public SaveBackedAnalyticsProfileStore(ISave save)
        {
            _save = save;
        }

        public JObject Profile
        {
            get
            {
                var payload = _save.State.Payload;
                if (!(payload["profile"] is JObject profile))
                {
                    profile = new JObject
                    {
                        ["createdAtUtc"] = 0L,
                        ["lastSeenAtUtc"] = 0L,
                        ["sessionCount"] = 0,
                    };
                    payload["profile"] = profile;
                }
                return profile;
            }
        }

        public bool CommitDurable()
        {
            try { return _save.TryCommitWithin(int.MaxValue); }
            catch { return false; }
        }

        public void RequestCommit() => CommitDurable();
    }
}
