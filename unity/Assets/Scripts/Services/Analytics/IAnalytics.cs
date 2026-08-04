using Newtonsoft.Json.Linq;

namespace CatMetro.Services
{
    // ADR-0003:42,75-77: IAnalytics is declared here; SDK types live only in Integrations.*
    // (which does not exist yet — no SDK is touched by any contract in this queue). Signature
    // per docs/architecture/overview.md:245-249. The EVENT TAXONOMY (typed constructors,
    // required params, 45 events) is CM-R43.1-.3 — a SEPARATE contract; this type is the
    // minimal metrics-only envelope the queue transports.
    public readonly struct AnalyticsEvent
    {
        public readonly string Name;
        public readonly JObject Params;

        public AnalyticsEvent(string name, JObject parameters)
        {
            Name = name; Params = parameters;
        }
    }

    // Members land with the taxonomy contract (the OneSignal tag set is RK-30's open flow —
    // enumerating keys here would invent taxonomy). Empty by design, not by omission.
    public enum UserPropertyKey
    {
    }

    public interface IAnalytics
    {
        void Log(in AnalyticsEvent e);
        void SetUserProperty(UserPropertyKey key, string value);
        int QueuedEventCount { get; }
    }
}
