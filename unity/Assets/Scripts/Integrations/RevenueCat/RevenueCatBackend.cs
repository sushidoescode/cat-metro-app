// The RevenueCat adapter. Everything in this file is inert unless the SDK package is installed:
// the assembly definition beside it carries defineConstraints ["CATMETRO_REVENUECAT"], and that
// define is produced by a versionDefines entry keyed on com.revenuecat.purchases-unity. With the
// package absent the whole assembly is skipped by the compiler, its reference to
// revenuecat.purchases-unity is never resolved, and the game runs on NullPurchaseBackend.
//
// So there is exactly one guard protecting the 1250 existing tests, and it is at assembly
// granularity rather than sprinkled through the code.
//
// Verified against purchases-unity 9.9.0 SOURCE, not against the docs. Three published RevenueCat
// Unity snippets do not compile against 9.9.0 and are called out at their call sites below.

#if CATMETRO_REVENUECAT
using System;
using System.Collections;
using System.Collections.Generic;
using CatMetro.Services.Purchases;
using UnityEngine;

namespace CatMetro.Integrations.RevenueCat
{
    // Registers itself with CatMetro.Integrations so the bootstrap never has to know this
    // assembly exists. AfterAssembliesLoaded runs before the bootstrap's BeforeSceneLoad.
    public static class RevenueCatRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        public static void Register()
        {
            // THE EDITOR RULE, and the single most important line in this integration.
            //
            // RevenueCat's docs: "Running the Purchases SDK is unsupported in the Unity Editor at
            // this time." What that means concretely, from the SDK source, is worse than an
            // exception: Purchases.Start() installs PurchasesWrapperNoop in the Editor, whose
            // methods have EMPTY BODIES — no throw, no log. Callbacks are delivered from native
            // code via UnitySendMessage, and with the noop wrapper nothing native runs, so
            // GetOfferings / PurchasePackage / RestorePurchases callbacks NEVER FIRE AT ALL.
            //
            // A UI waiting on one of those sits on a spinner forever with a clean console. So we
            // do not install the live backend in the Editor at all; the null backend answers
            // every callback immediately and every EditMode and PlayMode test stays honest.
#if UNITY_EDITOR
            Debug.Log("[Monetization] RevenueCat SDK present but not used in the Editor " +
                      "(its callbacks never fire there); running on the null backend.");
            return;
#elif UNITY_ANDROID || UNITY_IOS
            PurchaseBackendFactory.Register(_ => RevenueCatBehaviour.Create());
#else
            Debug.Log("[Monetization] RevenueCat purchases are enabled only for Android and iOS; " +
                      "running on the null backend for this player target.");
            return;
#endif
        }
    }

    // Owns the SDK's Purchases component and adapts it to IPurchaseBackend.
    //
    // A MonoBehaviour because two things here need frames: the SDK must be configured a frame
    // after its own Start() runs (see Configure below), and every call needs a timeout.
    public sealed class RevenueCatBehaviour : MonoBehaviour, IPurchaseBackend,
        IPurchaseBackendReadiness, IPurchaseBackendTransactionUpdates
    {
        // Queries should release the UI promptly, while an OS purchase sheet can legitimately
        // remain open during banking-app or parental verification. These are LOCAL watchdogs;
        // they never free an SDK callback slot, which only the native callback may do.
        private const float QueryTimeoutSeconds = 30f;
        private const float InteractiveTimeoutSeconds = 300f;

        private Purchases _purchases;
        private MonetizationKeys _config;

        // Offerings are cached so a purchase can find the Package object it needs. RevenueCat
        // purchases a Package, not a product id, and PurchasePackage is the supported path.
        private readonly Dictionary<string, Purchases.Package> _packagesByProductId =
            new Dictionary<string, Purchases.Package>(StringComparer.Ordinal);

        // purchases-unity 9.9 stores one callback per operation in Purchases. Starting a second
        // native call after our watchdog fired would overwrite the still-live first callback.
        // A timed-out slot therefore stays occupied until native code really answers (or this
        // process restarts).
        private bool _offeringsSlotOccupied;
        private bool _customerInfoSlotOccupied;
        private bool _purchaseSlotOccupied;
        private bool _restoreSlotOccupied;

        public BackendAvailability Availability { get; private set; } = BackendAvailability.Initializing;
        public event Action Ready;
        public event Action<EntitlementSnapshot> TransactionEntitlementsConfirmed;

        internal static IPurchaseBackend Create()
        {
            var config = MonetizationConfig.Load();
            if (!config.IsConfigured)
            {
                Debug.LogWarning("[Monetization] RevenueCat not configured: " + config.Problem);
                return new NullPurchaseBackend(BackendAvailability.NotConfigured, config.Problem);
            }

            var host = new GameObject("[RevenueCat]");
            DontDestroyOnLoad(host);
            var behaviour = host.AddComponent<RevenueCatBehaviour>();
            behaviour._config = config;
            return behaviour;
        }

        private void Start() => StartCoroutine(ConfigureNextFrame());

        private IEnumerator ConfigureNextFrame()
        {
            // Purchases is a MonoBehaviour in the GLOBAL namespace (its asmdef sets
            // rootNamespace to ""), so there is no using directive for it.
            try
            {
                _purchases = gameObject.AddComponent<Purchases>();

                // Must be set before the SDK's own Start() runs, or it self-configures from the
                // Inspector fields — which are empty here, because we build this GameObject at
                // runtime rather than authoring it into a scene. Setting it now is safe:
                // AddComponent runs Awake immediately, Start only at the end of this frame.
                _purchases.useRuntimeSetup = true;
            }
            catch (Exception e)
            {
                FailConfiguration("creating the Purchases component", e);
                yield break;
            }

            // THE ORDERING HAZARD. Purchases.Configure() dereferences a wrapper field that is
            // assigned in Purchases.Start(). Calling Configure before that — from our Awake, or
            // synchronously right here — is a NullReferenceException, and it is almost certainly
            // the error RevenueCat's Editor warning is describing. One frame is the fix.
            yield return null;

            try
            {
                var builder = Purchases.PurchasesConfiguration.Builder.Init(_config.ApiKey)
                    // The 9.9 Unity builder's zero-value is StoreKit1, despite `Default` being a
                    // named enum member. Make the settled StoreKit 2 direction explicit; Android's
                    // wrapper ignores this field. RevenueCat falls back only on iOS devices where
                    // StoreKit 2 is unavailable.
                    .SetStoreKitVersion(Purchases.StoreKitVersion.StoreKit2);
                _purchases.Configure(builder.Build());
            }
            catch (Exception e)
            {
                FailConfiguration("configuring the native SDK", e);
                yield break;
            }

            Availability = BackendAvailability.Ready;
            try
            {
                Ready?.Invoke();
            }
            catch (Exception e)
            {
                // A consumer refresh failure cannot retroactively make native configuration
                // false. SDK calls have their own guards and will degrade independently.
                Debug.LogError("[Monetization] ready subscriber threw: " + e);
            }
            Debug.Log("[Monetization] RevenueCat configured" +
                      (_config.UseTestStore ? " against the TEST STORE (not a real store)" : ""));
        }

        private void FailConfiguration(string stage, Exception error)
        {
            Availability = BackendAvailability.Unreachable;
            if (_purchases != null) Destroy(_purchases);
            _purchases = null;
            Debug.LogError("[Monetization] RevenueCat failed while " + stage +
                           "; continuing without a store. " + error);
        }

        // ---- IPurchaseBackend -------------------------------------------------------------

        public void FetchProducts(Action<IReadOnlyList<StoreProductView>> onDone)
        {
            var once = Guard(onDone, Array.Empty<StoreProductView>());
            if (_purchases == null) { once(Array.Empty<StoreProductView>()); return; }
            if (_offeringsSlotOccupied) { once(Array.Empty<StoreProductView>()); return; }

            _offeringsSlotOccupied = true;
            try
            {
                _purchases.GetOfferings((offerings, error) =>
                {
                    _offeringsSlotOccupied = false;
                    try
                    {
                        if (error != null || offerings == null)
                        {
                            // Transient, not fatal: keep the shop on screen and let the player retry.
                            Availability = BackendAvailability.Unreachable;
                            Debug.LogWarning("[Monetization] GetOfferings failed: " + Describe(error));
                            once(Array.Empty<StoreProductView>());
                            return;
                        }

                        Availability = BackendAvailability.Ready;
                        once(ReadOffering(offerings));
                    }
                    catch (Exception e)
                    {
                        Availability = BackendAvailability.Unreachable;
                        Debug.LogError("[Monetization] GetOfferings callback failed: " + e);
                        once(Array.Empty<StoreProductView>());
                    }
                });
            }
            catch (Exception e)
            {
                _offeringsSlotOccupied = false;
                Availability = BackendAvailability.Unreachable;
                Debug.LogError("[Monetization] GetOfferings threw: " + e);
                once(Array.Empty<StoreProductView>());
            }
        }

        private IReadOnlyList<StoreProductView> ReadOffering(Purchases.Offerings offerings)
        {
            _packagesByProductId.Clear();
            var result = new List<StoreProductView>();

            // Fetched by NAME rather than through Offerings.Current, so that someone flipping
            // the dashboard's current offering cannot silently redirect this fixed wardrobe to
            // an unrelated product set. Missing configuration fails closed.
            Purchases.Offering offering = null;
            if (offerings.All == null || !offerings.All.TryGetValue(
                    RevenueCatNames.CosmeticsOffering, out offering) || offering == null)
            {
                Debug.LogWarning("[Monetization] no offering named '" +
                                 RevenueCatNames.CosmeticsOffering +
                                 "'; purchases remain unavailable until the RevenueCat " +
                                 "dashboard matches");
                return result;
            }

            // Cosmetics use CUSTOM package identifiers, not the $rc_* durations, so the typed
            // convenience slots (Monthly, Annual, Lifetime) are all null and AvailablePackages
            // is the only way to reach them.
            var packages = offering.AvailablePackages;
            if (packages == null) return result;

            foreach (var package in packages)
            {
                // `package.StoreProduct`, NOT `package.Product`. RevenueCat's own
                // displaying-products doc shows `.Product`, which does not exist in 9.9.0; the
                // compiled API test in their repo uses `.StoreProduct`.
                var product = package.StoreProduct;
                if (product == null || string.IsNullOrEmpty(product.Identifier)) continue;

                _packagesByProductId[product.Identifier] = package;
                result.Add(new StoreProductView(product.Identifier, product.Title,
                    new LocalizedPrice(product.PriceString)));
            }

            Debug.Log("[Monetization] RevenueCat offering '" + offering.Identifier +
                      "' loaded " + result.Count + " product(s)");

            return result;
        }

        public void Purchase(string productId, Action<PurchaseResult> onDone)
        {
            var once = Guard(onDone,
                new PurchaseResult(PurchaseOutcome.UnknownUnsettled, productId, default,
                    "the store did not answer within " + InteractiveTimeoutSeconds + "s"),
                InteractiveTimeoutSeconds);

            if (_purchases == null)
            {
                once(PurchaseResult.Unavailable(productId, "RevenueCat is not configured yet"));
                return;
            }

            if (_purchaseSlotOccupied || _restoreSlotOccupied)
            {
                once(new PurchaseResult(PurchaseOutcome.Busy, productId));
                return;
            }

            if (!_packagesByProductId.TryGetValue(productId, out var package))
            {
                // We never saw this product in the offering. Buying it would mean asking the
                // store for something the dashboard has not published — the usual cause is a
                // product id typed differently in Play Console and RevenueCat.
                once(PurchaseResult.Unavailable(productId,
                    "no package for this product in the '" + RevenueCatNames.CosmeticsOffering +
                    "' offering — check the id matches in Play Console and RevenueCat"));
                return;
            }

            // PurchasePackage rather than PurchaseProduct. PurchaseProduct's `type` parameter
            // DEFAULTS TO "subs", so buying a one-time cosmetic through it without passing
            // "inapp" would ask the store for a subscription that does not exist.
            _purchaseSlotOccupied = true;
            try
            {
                _purchases.PurchasePackage(package, result =>
                {
                    _purchaseSlotOccupied = false;
                    try
                    {
                        var translated = Translate(result, productId);
                        if (!once(translated))
                            PublishLateEntitlements(translated.ConfirmedEntitlements);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("[Monetization] purchase callback failed: " + e);
                        once(new PurchaseResult(PurchaseOutcome.Failure, productId, default,
                            "RevenueCat purchase callback could not be read"));
                    }
                });
            }
            catch (Exception e)
            {
                _purchaseSlotOccupied = false;
                Debug.LogError("[Monetization] PurchasePackage threw: " + e);
                once(new PurchaseResult(PurchaseOutcome.Failure, productId, default,
                    "RevenueCat could not open the store purchase"));
            }
        }

        // The 9.9.0 callback shape: ONE PurchaseResult. RevenueCat's installation page still
        // shows the pre-8.x four-argument form (productIdentifier, customerInfo, userCancelled,
        // error), which no longer compiles.
        private PurchaseResult Translate(Purchases.PurchaseResult result, string productId)
        {
            if (result == null)
                return new PurchaseResult(PurchaseOutcome.UnknownUnsettled, productId);

            // Checked before the error, because a cancellation ALSO populates Error. Reading the
            // error first would report every cancelled purchase as a failure.
            if (result.UserCancelled)
                return new PurchaseResult(PurchaseOutcome.UserCancelled, productId);

            if (result.Error != null)
            {
                var outcome = IsPending(result.Error) ? PurchaseOutcome.Pending : PurchaseOutcome.Failure;
                return new PurchaseResult(outcome, productId, default, Describe(result.Error));
            }

            // Success as far as the store is concerned. CustomerInfo remains the authority —
            // never the product id — hence SuccessCandidate even though that authoritative
            // snapshot normally travels in this same native callback.
            return new PurchaseResult(PurchaseOutcome.SuccessCandidate, productId,
                confirmedEntitlements: result.CustomerInfo == null
                    ? null
                    : new EntitlementSnapshot(true, ReadEntitlements(result.CustomerInfo)));
        }

        // Google Play defers a purchase when payment needs another step — a slow card, parental
        // approval. The player has not been charged and owns nothing yet.
        private static bool IsPending(Purchases.Error error)
            => error?.ReadableErrorCode == "PaymentPendingError";

        public void Restore(Action<RestoreResult> onDone)
        {
            var once = Guard(onDone,
                new RestoreResult(RestoreOutcome.Failure, 0, "the store did not answer in time"),
                InteractiveTimeoutSeconds);

            if (_purchases == null)
            {
                once(new RestoreResult(RestoreOutcome.Unavailable, 0, "RevenueCat is not configured yet"));
                return;
            }

            if (_restoreSlotOccupied || _purchaseSlotOccupied)
            {
                once(new RestoreResult(RestoreOutcome.Busy));
                return;
            }

            // RestorePurchases, not SyncPurchases: this is only ever reached from a button the
            // player pressed, and RevenueCat is explicit that RestorePurchases "should not be
            // triggered programmatically, since it may cause OS level sign-in prompts to appear".
            _restoreSlotOccupied = true;
            try
            {
                _purchases.RestorePurchases((info, error) =>
                {
                    _restoreSlotOccupied = false;
                    try
                    {
                        if (error != null || info == null)
                        {
                            once(new RestoreResult(RestoreOutcome.Failure, 0,
                                error != null ? Describe(error) : "restore returned no CustomerInfo"));
                            return;
                        }

                        // The count is recomputed by PurchaseService from the snapshot, so the
                        // number shown to the player matches what actually came from the store.
                        var restored = new RestoreResult(RestoreOutcome.Completed,
                            confirmedEntitlements: new EntitlementSnapshot(true,
                                ReadEntitlements(info)));
                        if (!once(restored))
                            PublishLateEntitlements(restored.ConfirmedEntitlements);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("[Monetization] restore callback failed: " + e);
                        once(new RestoreResult(RestoreOutcome.Failure, 0,
                            "RevenueCat restore callback could not be read"));
                    }
                });
            }
            catch (Exception e)
            {
                _restoreSlotOccupied = false;
                Debug.LogError("[Monetization] RestorePurchases threw: " + e);
                once(new RestoreResult(RestoreOutcome.Failure, 0,
                    "RevenueCat could not open restore purchases"));
            }
        }

        public void RefreshEntitlements(Action<EntitlementSnapshot> onDone)
        {
            var once = Guard(onDone, EntitlementSnapshot.Unreachable());
            if (_purchases == null) { once(EntitlementSnapshot.Unreachable()); return; }
            if (_customerInfoSlotOccupied) { once(EntitlementSnapshot.Unreachable()); return; }

            _customerInfoSlotOccupied = true;
            try
            {
                _purchases.GetCustomerInfo((info, error) =>
                {
                    _customerInfoSlotOccupied = false;
                    try
                    {
                        if (error != null || info == null)
                        {
                            Availability = BackendAvailability.Unreachable;
                            // Unreachable(), NOT an empty authoritative snapshot. An empty authoritative
                            // snapshot would tell the ledger to revoke everything the player has paid
                            // for, every time the network hiccups.
                            once(EntitlementSnapshot.Unreachable());
                            return;
                        }

                        Availability = BackendAvailability.Ready;
                        var snapshot = new EntitlementSnapshot(true, ReadEntitlements(info));
                        // Unlike a completed purchase/restore, this query can carry data captured
                        // before a newer transaction. Its service request epoch is the only safe
                        // ordering token, so a response after the guard fired is simply ignored.
                        once(snapshot);
                    }
                    catch (Exception e)
                    {
                        Availability = BackendAvailability.Unreachable;
                        Debug.LogError("[Monetization] GetCustomerInfo callback failed: " + e);
                        once(EntitlementSnapshot.Unreachable());
                    }
                });
            }
            catch (Exception e)
            {
                _customerInfoSlotOccupied = false;
                Availability = BackendAvailability.Unreachable;
                Debug.LogError("[Monetization] GetCustomerInfo threw: " + e);
                once(EntitlementSnapshot.Unreachable());
            }
        }

        private static IReadOnlyList<EntitlementGrant> ReadEntitlements(Purchases.CustomerInfo info)
        {
            var grants = new List<EntitlementGrant>();
            var active = info.Entitlements?.Active;
            if (active == null) return grants;

            foreach (var kv in active)
            {
                var entitlement = kv.Value;
                if (entitlement == null) continue;

                // The expiry matters as much as the identifier. RevenueCat's own Ad Monetization
                // grants a TIME-LIMITED entitlement server-side and delivers it right here, and a
                // subscription behaves the same way. Carrying ExpirationDate through is what lets
                // the ledger lapse it locally — RevenueCat sends no event when it runs out.
                long expiresAt = 0L;
                if (entitlement.ExpirationDate.HasValue)
                {
                    expiresAt = new DateTimeOffset(
                        DateTime.SpecifyKind(entitlement.ExpirationDate.Value, DateTimeKind.Utc))
                        .ToUnixTimeSeconds();
                }

                grants.Add(new EntitlementGrant(kv.Key, GrantSource.Store, expiresAt));
            }

            return grants;
        }

        // ---- the timeout, and the fire-exactly-once guarantee ------------------------------

        // IPurchaseBackend promises every callback runs exactly once. Two things threaten that:
        // a callback that never arrives (the Editor noop wrapper, a wedged store), and one that
        // arrives after we have already given up. This wraps both.
        private Func<T, bool> Guard<T>(Action<T> onDone, T timeoutValue,
            float timeoutSeconds = QueryTimeoutSeconds)
        {
            bool fired = false;
            Coroutine watchdog = null;

            bool Fire(T value)
            {
                if (fired) return false;
                fired = true;
                if (watchdog != null) StopCoroutine(watchdog);
                try
                {
                    onDone?.Invoke(value);
                }
                catch (Exception e)
                {
                    // A throw inside a caller's callback must not escape into the SDK's native
                    // callback dispatch, where it would be swallowed or take the channel down.
                    Debug.LogError("[Monetization] callback threw: " + e);
                }

                return true;
            }

            watchdog = StartCoroutine(Timeout(timeoutSeconds, () => { Fire(timeoutValue); }));
            return Fire;
        }

        private static IEnumerator Timeout(float seconds, Action onTimeout)
        {
            yield return new WaitForSecondsRealtime(seconds);
            onTimeout();
        }

        private void PublishLateEntitlements(EntitlementSnapshot? candidate)
        {
            if (!candidate.HasValue) return;
            PublishLateEntitlements(candidate.Value);
        }

        private void PublishLateEntitlements(EntitlementSnapshot snapshot)
        {
            if (!snapshot.IsAuthoritative) return;
            try
            {
                TransactionEntitlementsConfirmed?.Invoke(snapshot);
            }
            catch (Exception e)
            {
                // The store callback has already been consumed; a subscriber must not break
                // RevenueCat's native dispatch or leave the slot marked occupied.
                Debug.LogError("[Monetization] entitlement update subscriber threw: " + e);
            }
        }

        private static string Describe(Purchases.Error error)
            => error == null
                ? "unknown error"
                : error.ReadableErrorCode + " (" + error.Code + "): " + error.Message;
    }
}
#endif
