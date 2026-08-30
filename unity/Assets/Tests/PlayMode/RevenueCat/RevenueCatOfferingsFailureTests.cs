#if CATMETRO_REVENUECAT && UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using CatMetro.Integrations.RevenueCat;
using CatMetro.Services.Purchases;
using NUnit.Framework;
using RevenueCat.SimpleJSON;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode.RevenueCat
{
    public sealed class RevenueCatOfferingsFailureTests
    {
        private GameObject _host;
        private RevenueCatBehaviour _backend;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("RevenueCatOfferingsFailureTests");
            _backend = _host.AddComponent<RevenueCatBehaviour>();
            var purchases = _host.AddComponent<Purchases>();
            purchases.enabled = false;
            purchases.useRuntimeSetup = true;
            typeof(Purchases).GetMethod("Start",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(purchases, null);
            SetField("_purchases", purchases);
            GetField<Dictionary<string, Purchases.Package>>("_packagesByProductId")
                [ProductIds.OutfitConductor] = new Purchases.Package(JSON.Parse(PackageJson));
            SetAvailability(BackendAvailability.Ready);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
        }

        [Test]
        public void OccupiedOfferingsSlot_SignalsFailureBeforeReturningEmptyProducts()
        {
            SetField("_offeringsSlotOccupied", true);
            int callbacks = 0;
            BackendAvailability observed = BackendAvailability.Ready;
            IReadOnlyList<StoreProductView> products = null;

            _backend.FetchProducts(value =>
            {
                callbacks++;
                observed = _backend.Availability;
                products = value;
            });
            _backend.enabled = false;

            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(products, Is.Empty);
            Assert.That(observed, Is.EqualTo(BackendAvailability.Unreachable),
                "PurchaseService must see failure state before it consumes the empty response");
            Assert.That(_backend.Availability, Is.EqualTo(BackendAvailability.Unreachable));
            Assert.That(GetField<bool>("_offeringsSlotOccupied"), Is.True,
                "occupied retry failure must not release or overwrite the native callback slot");
        }

        [UnityTest, Timeout(40000)]
        public IEnumerator OfferingsWatchdog_SignalsFailureBeforeItsSingleEmptyCallback()
        {
            int callbacks = 0;
            BackendAvailability observed = BackendAvailability.Ready;
            IReadOnlyList<StoreProductView> products = null;
            float startedAt = Time.realtimeSinceStartup;

            _backend.FetchProducts(value =>
            {
                callbacks++;
                observed = _backend.Availability;
                products = value;
            });
            // Prevent RevenueCatBehaviour.Start from configuring itself next frame. Disabling a
            // MonoBehaviour does not stop a coroutine it already started, so the Guard watchdog
            // continues against the real initialized Editor noop wrapper.
            _backend.enabled = false;

            float deadline = Time.realtimeSinceStartup + 35f;
            while (callbacks == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(callbacks, Is.EqualTo(1), "the real 30-second watchdog must release UI");
            Assert.That(Time.realtimeSinceStartup - startedAt, Is.GreaterThanOrEqualTo(29f),
                "a synchronous wrapper exception is not watchdog evidence");
            Assert.That(products, Is.Empty);
            Assert.That(observed, Is.EqualTo(BackendAvailability.Unreachable),
                "the failure signal must precede PurchaseService's empty-list callback");
            Assert.That(_backend.Availability, Is.EqualTo(BackendAvailability.Unreachable));

            // The native slot remains occupied after local timeout. A retry must fail locally,
            // not overwrite the SDK's still-live GetOfferings callback.
            var purchases = GetField<Purchases>("_purchases");
            var callbackProperty = typeof(Purchases).GetProperty("GetOfferingsCallback",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(callbackProperty, Is.Not.Null);
            var originalNativeCallback = callbackProperty.GetValue(purchases) as Delegate;
            Assert.That(originalNativeCallback, Is.Not.Null);
            int retryCallbacks = 0;
            _backend.FetchProducts(_ => retryCallbacks++);
            Assert.That(retryCallbacks, Is.EqualTo(1));
            Assert.That(GetField<bool>("_offeringsSlotOccupied"), Is.True);
            Assert.That(callbackProperty.GetValue(purchases), Is.SameAs(originalNativeCallback),
                "occupied retry must not overwrite purchases-unity's one callback delegate");

            typeof(Purchases).GetMethod("_getOfferings",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(purchases, new object[]
                {
                    "{\"offerings\":{\"all\":{\"cosmetics\":{\"identifier\":\"cosmetics\"," +
                    "\"serverDescription\":\"\",\"availablePackages\":[],\"metadata\":{}}}," +
                    "\"current\":null}}"
                });

            Assert.That(GetField<bool>("_offeringsSlotOccupied"), Is.False,
                "only the genuine native callback releases its one SDK slot");
            Assert.That(_backend.Availability, Is.EqualTo(BackendAvailability.Ready),
                "a later genuine offerings success restores availability");
            Assert.That(callbacks, Is.EqualTo(1),
                "late native success cannot refire the already-timed-out caller");
        }

        [UnityTest]
        public IEnumerator Guard_ThrowingTimeoutHookStillPrecedesAndFiresCallbackExactlyOnce()
        {
            var events = new List<string>();
            int callbacks = 0;
            Action<IReadOnlyList<StoreProductView>> callback = _ =>
            {
                callbacks++;
                events.Add("callback");
            };
            var guardDefinition = Array.Find(
                typeof(RevenueCatBehaviour).GetMethods(
                    BindingFlags.Instance | BindingFlags.NonPublic),
                method => method.Name == "Guard" && method.IsGenericMethodDefinition);
            Assert.That(guardDefinition, Is.Not.Null);
            var guard = guardDefinition.MakeGenericMethod(
                typeof(IReadOnlyList<StoreProductView>));

            object[] arguments;
            if (guard.GetParameters().Length == 4)
            {
                LogAssert.Expect(LogType.Error,
                    new Regex("timeout.*hook.*threw", RegexOptions.IgnoreCase));
                Action throwingHook = () =>
                {
                    events.Add("hook");
                    throw new InvalidOperationException("intentional test timeout hook failure");
                };
                arguments = new object[]
                    { callback, Array.Empty<StoreProductView>(), 0.01f, throwingHook };
            }
            else
            {
                // Current RED has no hook. Invoke the real Guard successfully and let the
                // behavioral ordering assertion below explain the missing failure signal.
                arguments = new object[]
                    { callback, Array.Empty<StoreProductView>(), 0.01f };
            }

            var fire = (Func<IReadOnlyList<StoreProductView>, bool>)guard.Invoke(
                _backend, arguments);
            _backend.enabled = false;
            float deadline = Time.realtimeSinceStartup + 1f;
            while (callbacks == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(events, Is.EqualTo(new[] { "hook", "callback" }),
                "the failure hook runs before the consumer observes its timeout value");
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(fire(Array.Empty<StoreProductView>()), Is.False);
            Assert.That(callbacks, Is.EqualTo(1));
        }

        [Test]
        public void InteractivePurchaseAndRestoreWatchdogs_RetainProductionDuration()
        {
            var interactiveTimeout = typeof(RevenueCatBehaviour).GetField(
                "InteractiveTimeoutSeconds", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(interactiveTimeout, Is.Not.Null);
            Assert.That(interactiveTimeout.GetRawConstantValue(), Is.EqualTo(300f),
                "public purchase and restore retain the production 300-second watchdog");
        }

        [UnityTest]
        public IEnumerator PurchaseLateCallback_PublishesItsOperationStartAuthoritySession()
        {
            yield return ProvePurchaseCapturesOperationStartSession();
        }

        [UnityTest]
        public IEnumerator RestoreLateCallback_PublishesItsOperationStartAuthoritySession()
        {
            yield return ProveRestoreCapturesOperationStartSession();
        }

        private IEnumerator ProvePurchaseCapturesOperationStartSession()
        {
            var updates = new List<TransactionEntitlementUpdate>();
            Action<TransactionEntitlementUpdate> handler = update => updates.Add(update);
            _backend.TransactionEntitlementsConfirmed += handler;
            long startedSession = _backend.BeginAuthoritySession();
            int localCallbacks = 0;

            _backend.enabled = true;
            InvokePrivate("PurchaseWithTimeout", ProductIds.OutfitConductor,
                (Action<PurchaseResult>)(_ => localCallbacks++), 0.01f);
            long laterSession = _backend.BeginAuthoritySession();
            _backend.enabled = false;
            Assert.That(laterSession, Is.Not.EqualTo(startedSession));

            float deadline = Time.realtimeSinceStartup + 1f;
            while (localCallbacks == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(localCallbacks, Is.EqualTo(1));

            InvokePurchasesCallback("_makePurchase", PurchaseResponseJson);

            Assert.That(updates.Count, Is.EqualTo(1));
            Assert.That(updates[0].AuthoritySessionId, Is.EqualTo(startedSession),
                "late purchase publishes operation-start S1, never callback-time S2");
            Assert.That(updates[0].Snapshot.IsAuthoritative, Is.True);
            Assert.That(updates[0].Snapshot.Grants.Count, Is.EqualTo(1));
            Assert.That(localCallbacks, Is.EqualTo(1),
                "late native purchase cannot refire its timed-out local callback");
            _backend.TransactionEntitlementsConfirmed -= handler;
        }

        private IEnumerator ProveRestoreCapturesOperationStartSession()
        {
            var updates = new List<TransactionEntitlementUpdate>();
            Action<TransactionEntitlementUpdate> handler = update => updates.Add(update);
            _backend.TransactionEntitlementsConfirmed += handler;
            long startedSession = _backend.BeginAuthoritySession();
            int localCallbacks = 0;

            _backend.enabled = true;
            InvokePrivate("RestoreWithTimeout",
                (Action<RestoreResult>)(_ => localCallbacks++), 0.01f);
            long laterSession = _backend.BeginAuthoritySession();
            _backend.enabled = false;
            Assert.That(laterSession, Is.Not.EqualTo(startedSession));

            float deadline = Time.realtimeSinceStartup + 1f;
            while (localCallbacks == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(localCallbacks, Is.EqualTo(1));

            InvokePurchasesCallback("_restorePurchases", CustomerInfoResponseJson);

            Assert.That(updates.Count, Is.EqualTo(1));
            Assert.That(updates[0].AuthoritySessionId, Is.EqualTo(startedSession),
                "late restore publishes operation-start S1, never callback-time S2");
            Assert.That(updates[0].Snapshot.IsAuthoritative, Is.True);
            Assert.That(updates[0].Snapshot.Grants.Count, Is.EqualTo(1));
            Assert.That(localCallbacks, Is.EqualTo(1),
                "late native restore cannot refire its timed-out local callback");
            _backend.TransactionEntitlementsConfirmed -= handler;
        }

        private void InvokePrivate(string methodName, params object[] arguments)
            => typeof(RevenueCatBehaviour)
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_backend, arguments);

        private void InvokePurchasesCallback(string methodName, string json)
            => typeof(Purchases)
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(GetField<Purchases>("_purchases"), new object[] { json });

        private void SetAvailability(BackendAvailability availability)
            => typeof(RevenueCatBehaviour)
                .GetField("<Availability>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_backend, availability);

        private void SetField(string name, object value)
            => typeof(RevenueCatBehaviour)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_backend, value);

        private T GetField<T>(string name)
            => (T)typeof(RevenueCatBehaviour)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_backend);

        private const string PackageJson = @"{
          ""identifier"": ""$rc_lifetime"",
          ""packageType"": ""LIFETIME"",
          ""presentedOfferingContext"": { ""offeringIdentifier"": ""cosmetics"" },
          ""product"": {
            ""identifier"": ""cm_outfit_conductor"",
            ""title"": ""Conductor's Coat"",
            ""description"": """",
            ""price"": 1.99,
            ""priceString"": ""$1.99"",
            ""currencyCode"": ""USD"",
            ""productCategory"": ""NON_SUBSCRIPTION"",
            ""presentedOfferingContext"": { ""offeringIdentifier"": ""cosmetics"" }
          }
        }";

        private const string CustomerInfoBody = @"{
          ""entitlements"": {
            ""all"": {
              ""outfit_conductor"": {
                ""identifier"": ""outfit_conductor"", ""isActive"": true,
                ""willRenew"": false, ""periodType"": ""NORMAL"",
                ""latestPurchaseDateMillis"": 1700000000000,
                ""originalPurchaseDateMillis"": 1700000000000,
                ""expirationDateMillis"": 0, ""store"": ""PLAY_STORE"",
                ""productIdentifier"": ""cm_outfit_conductor"", ""isSandbox"": true,
                ""verification"": ""VERIFIED""
              }
            },
            ""active"": {
              ""outfit_conductor"": {
                ""identifier"": ""outfit_conductor"", ""isActive"": true,
                ""willRenew"": false, ""periodType"": ""NORMAL"",
                ""latestPurchaseDateMillis"": 1700000000000,
                ""originalPurchaseDateMillis"": 1700000000000,
                ""expirationDateMillis"": 0, ""store"": ""PLAY_STORE"",
                ""productIdentifier"": ""cm_outfit_conductor"", ""isSandbox"": true,
                ""verification"": ""VERIFIED""
              }
            },
            ""verification"": ""VERIFIED""
          },
          ""activeSubscriptions"": [],
          ""allPurchasedProductIdentifiers"": [""cm_outfit_conductor""],
          ""firstSeenMillis"": 1700000000000,
          ""originalAppUserId"": ""test-user"",
          ""requestDateMillis"": 1700000000000,
          ""originalPurchaseDateMillis"": 1700000000000,
          ""latestExpirationDateMillis"": 0,
          ""allExpirationDatesMillis"": { ""cm_outfit_conductor"": null },
          ""allPurchaseDatesMillis"": { ""cm_outfit_conductor"": 1700000000000 },
          ""originalApplicationVersion"": ""1"",
          ""managementURL"": null,
          ""nonSubscriptionTransactions"": [],
          ""subscriptionsByProductIdentifier"": {}
        }";

        private const string CustomerInfoResponseJson =
            "{\"customerInfo\":" + CustomerInfoBody + "}";

        private const string PurchaseResponseJson =
            "{\"customerInfo\":" + CustomerInfoBody + ",\"userCancelled\":false}";
    }
}
#endif
