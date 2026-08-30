using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using CatMetro.Integrations.RevenueCat;
using CatMetro.Services.Ads;
using CatMetro.Services.Purchases;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RevenueCat.Tests
{
    public sealed class RevenueCatAdReporterTests
    {
        private GameObject _host;
        private RevenueCatBehaviour _behaviour;
        private IAdEventReporter _reporter;
        private Purchases _purchases;
        private RecordingWrapper _wrapper;
        private IEnumerator _configuration;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("[RevenueCatAdReporterTests]");
            _behaviour = _host.AddComponent<RevenueCatBehaviour>();
            _reporter = (object)_behaviour as IAdEventReporter;
            Assert.That(_reporter, Is.SameAs(_behaviour),
                "the existing purchase backend must itself be the ads reporter");

            var config = MonetizationKeys.Parse(
                @"{""googleApiKey"":""goog_test-public-key"",""useTestStore"":false}",
                StorePlatform.GooglePlay, isDebugBuild: true);
            Assert.That(config.IsConfigured, Is.True);
            Field("_config").SetValue(_behaviour, config);

            _configuration = (IEnumerator)typeof(RevenueCatBehaviour)
                .GetMethod("ConfigureNextFrame", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_behaviour, null);
            Assert.That(_configuration.MoveNext(), Is.True,
                "the backend must yield one frame before configuring Purchases");

            _purchases = _host.GetComponent<Purchases>();
            Assert.That(_purchases, Is.Not.Null);
            _wrapper = new RecordingWrapper();
            _purchases.SetWrapper(_wrapper);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
        }

        [Test]
        public void Behaviour_IsTheSamePurchaseBackendAndAdReporterObject()
        {
            Assert.That(_behaviour, Is.InstanceOf<IPurchaseBackend>());
            Assert.That(_reporter, Is.SameAs(_behaviour));
        }

        [Test]
        public void Configuration_WaitsForWrapperTracker_ThenPublishesReadyExactlyOnce()
        {
            int changes = 0;
            _reporter.ReadinessChanged += () => changes++;

            Assert.That(_reporter.IsReady, Is.False,
                "a wrapper-created tracker alone must not claim configuration succeeded");
            FinishConfiguration();

            Assert.That(_wrapper.SetupCalls, Is.EqualTo(1));
            Assert.That(_reporter.IsReady, Is.True);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(_behaviour.Availability, Is.EqualTo(BackendAvailability.Ready));

            LogAssert.Expect(LogType.Warning,
                new Regex("dropped malformed RevenueCat ad event"));
            _reporter.Report(Malformed(RewardedAdEventKind.Loaded));
            Assert.That(changes, Is.EqualTo(1),
                "a dropped event cannot republish unchanged readiness");
        }

        [Test]
        public void ConfigurationFailure_RemainsContainedAndNeverPublishesReady()
        {
            _wrapper.ThrowOnSetup = true;
            int changes = 0;
            _reporter.ReadinessChanged += () => changes++;
            LogAssert.Expect(LogType.Error,
                new Regex("RevenueCat failed while configuring the native SDK"));

            Assert.DoesNotThrow(FinishConfiguration);

            Assert.That(_reporter.IsReady, Is.False);
            Assert.That(changes, Is.Zero);
            Assert.That(_behaviour.Availability, Is.EqualTo(BackendAvailability.Unreachable));
        }

        [Test]
        public void Report_RoutesMatchingEventsWithExactRevenueCatPayloads()
        {
            FinishConfiguration();

            _reporter.Report(Valid(RewardedAdEventKind.Loaded));
            _reporter.Report(Valid(RewardedAdEventKind.Displayed));
            _reporter.Report(Valid(RewardedAdEventKind.Opened));
            _reporter.Report(Valid(RewardedAdEventKind.LoadFailed));
            _reporter.Report(Valid(RewardedAdEventKind.Revenue));
            _reporter.Report(Valid(RewardedAdEventKind.DisplayFailed));
            _reporter.Report(Valid(RewardedAdEventKind.Rewarded));
            _reporter.Report(Valid(RewardedAdEventKind.Closed));

            Assert.That(_wrapper.Loaded, Has.Count.EqualTo(1));
            Assert.That(_wrapper.Displayed, Has.Count.EqualTo(1));
            Assert.That(_wrapper.Opened, Has.Count.EqualTo(1));
            Assert.That(_wrapper.FailedToLoad, Has.Count.EqualTo(1));
            Assert.That(_wrapper.Revenue, Has.Count.EqualTo(1));
            Assert.That(_wrapper.TotalTrackCalls, Is.EqualTo(5),
                "display failure, reward, and close are internal-only events");

            AssertLifecycle(_wrapper.Loaded[0].MediatorName,
                _wrapper.Loaded[0].AdFormat, _wrapper.Loaded[0].AdUnitId,
                _wrapper.Loaded[0].ImpressionId, _wrapper.Loaded[0].NetworkName,
                _wrapper.Loaded[0].Placement);
            AssertLifecycle(_wrapper.Displayed[0].MediatorName,
                _wrapper.Displayed[0].AdFormat, _wrapper.Displayed[0].AdUnitId,
                _wrapper.Displayed[0].ImpressionId, _wrapper.Displayed[0].NetworkName,
                _wrapper.Displayed[0].Placement);
            AssertLifecycle(_wrapper.Opened[0].MediatorName,
                _wrapper.Opened[0].AdFormat, _wrapper.Opened[0].AdUnitId,
                _wrapper.Opened[0].ImpressionId, _wrapper.Opened[0].NetworkName,
                _wrapper.Opened[0].Placement);

            var failed = _wrapper.FailedToLoad[0];
            Assert.That(failed.MediatorName.Value, Is.EqualTo("LevelPlay"));
            Assert.That(failed.AdFormat, Is.EqualTo(global::RevenueCat.AdTracker.Format.Rewarded));
            Assert.That(failed.AdUnitId, Is.EqualTo("unit-1"));
            Assert.That(failed.Placement, Is.EqualTo("placement-1"));
            Assert.That(failed.MediatorErrorCode, Is.EqualTo(73));

            var revenue = _wrapper.Revenue[0];
            AssertLifecycle(revenue.MediatorName, revenue.AdFormat, revenue.AdUnitId,
                revenue.ImpressionId, revenue.NetworkName, revenue.Placement);
            Assert.That(revenue.RevenueMicros, Is.EqualTo(123_456L));
            Assert.That(revenue.Currency, Is.EqualTo("USD"),
                "LevelPlay impression revenue is mapped in USD, not trusted from caller text");
            Assert.That(revenue.Precision,
                Is.EqualTo(global::RevenueCat.AdTracker.Precision.PublisherDefined));
        }

        [TestCase(AdRevenuePrecision.Exact, "exact")]
        [TestCase(AdRevenuePrecision.PublisherDefined, "publisher_defined")]
        [TestCase(AdRevenuePrecision.Estimated, "estimated")]
        [TestCase(AdRevenuePrecision.Unknown, "unknown")]
        public void Revenue_MapsEveryNeutralPrecision(AdRevenuePrecision precision,
            string expected)
        {
            FinishConfiguration();
            _reporter.Report(Valid(RewardedAdEventKind.Revenue, precision));

            Assert.That(_wrapper.Revenue, Has.Count.EqualTo(1));
            Assert.That(_wrapper.Revenue[0].Precision.Value, Is.EqualTo(expected));
        }

        [TestCase(RewardedAdEventKind.Loaded, true, false)]
        [TestCase(RewardedAdEventKind.Loaded, false, true)]
        [TestCase(RewardedAdEventKind.Displayed, true, false)]
        [TestCase(RewardedAdEventKind.Displayed, false, true)]
        [TestCase(RewardedAdEventKind.Opened, true, false)]
        [TestCase(RewardedAdEventKind.Opened, false, true)]
        [TestCase(RewardedAdEventKind.Revenue, true, false)]
        [TestCase(RewardedAdEventKind.Revenue, false, true)]
        public void ImpressionEvents_RequireNonblankAdUnitAndAuctionIds(
            RewardedAdEventKind kind, bool blankAdUnit, bool blankAuction)
        {
            FinishConfiguration();
            int changes = 0;
            _reporter.ReadinessChanged += () => changes++;
            LogAssert.Expect(LogType.Warning,
                new Regex("dropped malformed RevenueCat ad event"));
            var adEvent = Event(kind, blankAdUnit ? " \t" : "unit-1",
                blankAuction ? " " : "auction-1");

            Assert.DoesNotThrow(() => _reporter.Report(adEvent));

            Assert.That(_wrapper.TotalTrackCalls, Is.Zero);
            Assert.That(_reporter.IsReady, Is.True);
            Assert.That(changes, Is.Zero,
                "malformed metadata is not an SDK failure");
        }

        [Test]
        public void LoadFailed_RequiresOnlyAdUnitId()
        {
            FinishConfiguration();
            _reporter.Report(Event(RewardedAdEventKind.LoadFailed, "unit-1", " "));
            Assert.That(_wrapper.FailedToLoad, Has.Count.EqualTo(1));

            LogAssert.Expect(LogType.Warning,
                new Regex("dropped malformed RevenueCat ad event"));
            _reporter.Report(Event(RewardedAdEventKind.LoadFailed, " ", "auction-1"));
            Assert.That(_wrapper.FailedToLoad, Has.Count.EqualTo(1));
            Assert.That(_reporter.IsReady, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingCurrentTrackerOrTrackingException_FailsFutureOffersOnce(
            bool throwFromSdk)
        {
            int changes = 0;
            _reporter.ReadinessChanged += () => changes++;
            FinishConfiguration();
            Assert.That(changes, Is.EqualTo(1));

            if (throwFromSdk)
                _wrapper.ThrowOnTrack = true;
            else
                typeof(Purchases).GetField("<AdTracker>k__BackingField",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(_purchases, null);
            LogAssert.Expect(LogType.Error,
                new Regex(throwFromSdk
                    ? "RevenueCat ad tracking failed"
                    : "RevenueCat AdTracker is unavailable"));

            Assert.DoesNotThrow(() => _reporter.Report(Valid(RewardedAdEventKind.Loaded)));

            Assert.That(_reporter.IsReady, Is.False);
            Assert.That(changes, Is.EqualTo(2),
                "ready and failed transitions must be balanced");
            Assert.DoesNotThrow(() => _reporter.Report(Valid(RewardedAdEventKind.Displayed)));
            Assert.That(changes, Is.EqualTo(2),
                "an unchanged failed state must not notify again");
            Assert.That(_wrapper.TrackAttempts, Is.EqualTo(throwFromSdk ? 1 : 0));
        }

        [Test]
        public void Destroy_ClearsReadiness_AndIsolatesThrowingObservers()
        {
            int throwingCalls = 0;
            int laterCalls = 0;
            _reporter.ReadinessChanged += () =>
            {
                throwingCalls++;
                throw new InvalidOperationException("injected readiness observer fault");
            };
            _reporter.ReadinessChanged += () => laterCalls++;
            LogAssert.Expect(LogType.Error,
                new Regex("RevenueCat ad readiness subscriber threw"));
            FinishConfiguration();
            Assert.That(throwingCalls, Is.EqualTo(1));
            Assert.That(laterCalls, Is.EqualTo(1));
            Assert.That(_reporter.IsReady, Is.True);

            LogAssert.Expect(LogType.Error,
                new Regex("RevenueCat ad readiness subscriber threw"));
            typeof(RevenueCatBehaviour)
                .GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_behaviour, null);
            UnityEngine.Object.DestroyImmediate(_host);
            _host = null;

            Assert.That(_reporter.IsReady, Is.False);
            Assert.That(throwingCalls, Is.EqualTo(2));
            Assert.That(laterCalls, Is.EqualTo(2),
                "one observer must not prevent a later observer");
        }

        private void FinishConfiguration()
        {
            Assert.That(_configuration.MoveNext(), Is.False);
        }

        private FieldInfo Field(string name)
            => typeof(RevenueCatBehaviour).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static RewardedAdEvent Valid(RewardedAdEventKind kind,
            AdRevenuePrecision precision = AdRevenuePrecision.PublisherDefined)
            => Event(kind, "unit-1", "auction-1", precision);

        private static RewardedAdEvent Malformed(RewardedAdEventKind kind)
            => Event(kind, " ", " ");

        private static RewardedAdEvent Event(RewardedAdEventKind kind, string adUnitId,
            string auctionId,
            AdRevenuePrecision precision = AdRevenuePrecision.PublisherDefined)
            => new RewardedAdEvent(kind, attemptId: 8L, placementId: "placement-1",
                adUnitId: adUnitId, adId: "ad-id-not-the-impression",
                auctionId: auctionId, networkName: "network-1", errorCode: 73,
                revenueMicros: 123_456L, currency: "EUR", revenuePrecision: precision);

        private static void AssertLifecycle(
            global::RevenueCat.AdTracker.MediatorName mediator,
            global::RevenueCat.AdTracker.Format format, string adUnitId,
            string impressionId, string networkName, string placement)
        {
            Assert.That(mediator.Value, Is.EqualTo("LevelPlay"));
            Assert.That(format, Is.EqualTo(global::RevenueCat.AdTracker.Format.Rewarded));
            Assert.That(adUnitId, Is.EqualTo("unit-1"));
            Assert.That(impressionId, Is.EqualTo("auction-1"),
                "AuctionId, never AdId, is RevenueCat's impression id");
            Assert.That(networkName, Is.EqualTo("network-1"));
            Assert.That(placement, Is.EqualTo("placement-1"));
        }

        private sealed class RecordingWrapper : IPurchasesWrapper
        {
            public readonly List<global::RevenueCat.AdLoadedData> Loaded = new();
            public readonly List<global::RevenueCat.AdDisplayedData> Displayed = new();
            public readonly List<global::RevenueCat.AdOpenedData> Opened = new();
            public readonly List<global::RevenueCat.AdFailedToLoadData> FailedToLoad = new();
            public readonly List<global::RevenueCat.AdRevenueData> Revenue = new();

            public bool ThrowOnSetup { get; set; }
            public bool ThrowOnTrack { get; set; }
            public int SetupCalls { get; private set; }
            public int TrackAttempts { get; private set; }
            public int TotalTrackCalls => Loaded.Count + Displayed.Count + Opened.Count +
                                          FailedToLoad.Count + Revenue.Count;

            public void Setup(string gameObject, string apiKey, string appUserId,
                Purchases.PurchasesAreCompletedBy purchasesAreCompletedBy,
                Purchases.StoreKitVersion storeKitVersion, string userDefaultsSuiteName,
                bool useAmazon, string dangerousSettingsJson,
                bool shouldShowInAppMessagesAutomatically,
                bool pendingTransactionsForPrepaidPlansEnabled, bool diagnosticsEnabled,
                bool automaticDeviceIdentifierCollectionEnabled, string preferredUILocaleOverride)
                => SetupCore();

            public void Setup(string gameObject, string apiKey, string appUserId,
                Purchases.PurchasesAreCompletedBy purchasesAreCompletedBy,
                Purchases.StoreKitVersion storeKitVersion, string userDefaultsSuiteName,
                bool useAmazon, string dangerousSettingsJson,
                bool shouldShowInAppMessagesAutomatically,
                Purchases.EntitlementVerificationMode entitlementVerificationMode,
                bool pendingTransactionsForPrepaidPlansEnabled, bool diagnosticsEnabled,
                bool automaticDeviceIdentifierCollectionEnabled, string preferredUILocaleOverride)
                => SetupCore();

            private void SetupCore()
            {
                SetupCalls++;
                if (ThrowOnSetup) throw new InvalidOperationException("injected setup fault");
            }

            public void TrackAdLoaded(global::RevenueCat.AdLoadedData data)
            {
                ThrowIfRequested();
                Loaded.Add(data);
            }

            public void TrackAdDisplayed(global::RevenueCat.AdDisplayedData data)
            {
                ThrowIfRequested();
                Displayed.Add(data);
            }

            public void TrackAdOpened(global::RevenueCat.AdOpenedData data)
            {
                ThrowIfRequested();
                Opened.Add(data);
            }

            public void TrackAdFailedToLoad(global::RevenueCat.AdFailedToLoadData data)
            {
                ThrowIfRequested();
                FailedToLoad.Add(data);
            }

            public void TrackAdRevenue(global::RevenueCat.AdRevenueData data)
            {
                ThrowIfRequested();
                Revenue.Add(data);
            }

            private void ThrowIfRequested()
            {
                TrackAttempts++;
                if (ThrowOnTrack) throw new InvalidOperationException("injected track fault");
            }

            public void GetStorefront() { }
            public void GetProducts(string[] productIdentifiers, string type = "subs") { }
            public void PurchaseProduct(string productIdentifier, string type = "subs",
                string oldSku = null,
                Purchases.ProrationMode prorationMode =
                    Purchases.ProrationMode.UnknownSubscriptionUpgradeDowngradePolicy,
                bool googleIsPersonalizedPrice = false,
                string presentedOfferingIdentifier = null,
                Purchases.PromotionalOffer discount = null) { }
            public void PurchasePackage(Purchases.Package packageToPurchase, string oldSku = null,
                Purchases.ProrationMode prorationMode =
                    Purchases.ProrationMode.UnknownSubscriptionUpgradeDowngradePolicy,
                bool googleIsPersonalizedPrice = false,
                Purchases.PromotionalOffer discount = null) { }
            public void PurchaseSubscriptionOption(Purchases.SubscriptionOption subscriptionOption,
                Purchases.GoogleProductChangeInfo googleProductChangeInfo = null,
                bool googleIsPersonalizedPrice = false) { }
            public void RestorePurchases() { }
            public void LogIn(string appUserId) { }
            public void LogOut() { }
            public void SetAllowSharingStoreAccount(bool allow) { }
            public void SetDebugLogsEnabled(bool enabled) { }
            public void SetLogLevel(Purchases.LogLevel level) { }
            public void SetLogHandler() { }
            public void SetProxyURL(string proxyURL) { }
            public string GetAppUserId() => null;
            public void GetCustomerInfo() { }
            public void GetOfferings() { }
            public void GetCurrentOfferingForPlacement(string placementIdentifier) { }
            public void SyncAttributesAndOfferingsIfNeeded() { }
            public void SyncPurchases() { }
            public void SyncAmazonPurchase(string productID, string receiptID, string amazonUserID,
                string isoCurrencyCode, double price) { }
            public void GetAmazonLWAConsentStatus() { }
            public void EnableAdServicesAttributionTokenCollection() { }
            public bool IsAnonymous() => false;
            public bool IsConfigured() => false;
            public void CheckTrialOrIntroductoryPriceEligibility(string[] productIdentifiers) { }
            public void InvalidateCustomerInfoCache() { }
            public void OverridePreferredUILocale(string locale) { }
            public void PresentCodeRedemptionSheet() { }
            public void RecordPurchase(string productID) { }
            public void SetSimulatesAskToBuyInSandbox(bool enabled) { }
            public void SetAttributes(string attributesJson) { }
            public void SetEmail(string email) { }
            public void SetPhoneNumber(string phoneNumber) { }
            public void SetDisplayName(string displayName) { }
            public void SetPushToken(string token) { }
            public void SetAdjustID(string adjustID) { }
            public void SetAppsflyerID(string appsflyerID) { }
            public void SetFBAnonymousID(string fbAnonymousID) { }
            public void SetMparticleID(string mparticleID) { }
            public void SetOnesignalID(string onesignalID) { }
            public void SetOnesignalUserID(string onesignalUserID) { }
            public void SetAirshipChannelID(string airshipChannelID) { }
            public void SetCleverTapID(string cleverTapID) { }
            public void SetMixpanelDistinctID(string mixpanelDistinctID) { }
            public void SetFirebaseAppInstanceID(string firebaseAppInstanceID) { }
            public void SetMediaSource(string mediaSource) { }
            public void SetCampaign(string campaign) { }
            public void SetAdGroup(string adGroup) { }
            public void SetAd(string ad) { }
            public void SetKeyword(string keyword) { }
            public void SetCreative(string creative) { }
            public void SetAppsFlyerConversionData(string conversionDataJson) { }
            public void CollectDeviceIdentifiers() { }
            public void CanMakePayments(Purchases.BillingFeature[] features) { }
            public void GetPromotionalOffer(string productIdentifier, string discountIdentifier) { }
            public void ShowInAppMessages(Purchases.InAppMessageType[] messageTypes) { }
            public void ParseAsWebPurchaseRedemption(string urlString) { }
            public void RedeemWebPurchase(Purchases.WebPurchaseRedemption webPurchaseRedemption) { }
            public void GetVirtualCurrencies() { }
            public string GetCachedVirtualCurrencies() => null;
            public void InvalidateVirtualCurrenciesCache() { }
            public void GetEligibleWinBackOffersForProduct(Purchases.StoreProduct storeProduct) { }
            public void GetEligibleWinBackOffersForPackage(Purchases.Package package) { }
            public void PurchaseProductWithWinBackOffer(Purchases.StoreProduct storeProduct,
                Purchases.WinBackOffer winBackOffer) { }
            public void PurchasePackageWithWinBackOffer(Purchases.Package package,
                Purchases.WinBackOffer winBackOffer) { }
            public void TrackCustomPaywallImpression(
                Purchases.CustomPaywallImpressionParams parameters) { }
            public void GenerateRewardVerificationToken(string impressionId) { }
            public void PollRewardVerification(string clientTransactionId,
                global::RevenueCat.RewardedAdTrackingMetadata trackingMetadata = null) { }
        }
    }
}
