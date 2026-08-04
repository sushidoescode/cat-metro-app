// Negative fixture for CM-C7 criterion 14 (never compiled; grep bait): proves the monetization
// scope-guard pattern fires. The save contract is a DATA STRUCTURE (Q-T); tokens like
// RevenueCat or BillingClient may never reach unity/Assets/Scripts/Application/Save/**.
class SaveBanned
{
    private object _client; // RevenueCat BillingClient GoogleMobileAds Purchases.
}
