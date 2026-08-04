// Negative fixture for CM-C8 criteria 7 + 10 (never compiled; grep bait): proves both
// scope-guard patterns fire — SDK namespaces and CM-C7 state types may never reach
// unity/Assets/Scripts/Application/Analytics/**. Tokens: Firebase OneSignalSDK GoogleMobileAds
// RevenueCat plus ConsumableLedger SaveStore SaveState ISave (the metrics-only wall).
class AnalyticsBanned
{
    private object _sdk; // Firebase.Analytics + OneSignalSDK + GoogleMobileAds + RevenueCat
    private object _state; // ConsumableLedger SaveStore SaveState ISave SaveDefaults
    private object _write; // File.WriteAllBytes FileStream StreamWriter — a second write path
    private object _file;  // "save.dat" — the queue may never name the save file
}
