// NEGATIVE FIXTURE (never compiled — lives outside unity/Assets). Proves every CM-C9 wrapper
// guard fires: the construction-site count, the SDK-namespace ban, the save-token wall, the
// dark-factory reference ban, and the bound-literal ban.
public static class Banned
{
    public static object Construct()
    {
        var e = new AnalyticsEvent("zz", null);              // a second construction site
        Firebase.Analytics.Log(e);                           // an SDK namespace
        var s = new SaveStore();                             // a save/ledger token
        var p = Events.PaywallViewed("home", "off1", "v1");  // a dark factory call site
        int bound = 512;                                     // a re-declared queue bound
        return new object[] { e, s, p, bound };
    }
}
