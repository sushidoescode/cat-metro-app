namespace CatMetro.Services.Ads
{
    // ADR-0006 §2 and threat-model SEC-24: durable availability/account-health bounds,
    // not anti-cheat. All times are Unix seconds; localDateKey is yyyy-MM-dd.
    public interface IFailureRewindCapStore
    {
        // Call at startup and lifecycle/activity boundaries. Failure disables this placement
        // until a successful durable touch. A quick process relaunch retains session usage.
        bool TryTouchFailureRewindSession(long nowUnixSeconds, string localDateKey);
        // Read-only; requires successful initialization and an unexpired durable session.
        bool CanOfferFailureRewind(long nowUnixSeconds, string localDateKey);
        // One transaction validates 2/session and 5/local-date, resets expired axes,
        // increments both counters and touches last-seen. A failed commit grants nothing.
        bool TryConsumeFailureRewind(long nowUnixSeconds, string localDateKey);
    }
}
