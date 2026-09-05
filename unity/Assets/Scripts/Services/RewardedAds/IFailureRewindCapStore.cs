namespace CatMetro.Services.Ads
{
    // ADR-0006 §2 and threat-model SEC-24: durable availability/account-health bounds,
    // not anti-cheat. All times are Unix seconds; localDateKey is yyyy-MM-dd.
    public interface IFailureRewindCapStore
    {
        // Only startup/foreground entry may roll a session. Heartbeats and background exit
        // touch last-seen without rollover. Failure disables offers until a durable touch.
        // A quick process relaunch retains session usage.
        bool TryTouchFailureRewindSession(long nowUnixSeconds, string localDateKey,
            bool allowSessionRollover);
        // Read-only; requires successful initialization. Observation never rolls the session.
        bool CanOfferFailureRewind(long nowUnixSeconds, string localDateKey);
        // One transaction validates 2/session and 5/local-date, resets the local-date axis,
        // increments both counters and touches last-seen. A failed commit grants nothing.
        bool TryConsumeFailureRewind(long nowUnixSeconds, string localDateKey);
    }
}
