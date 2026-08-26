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
#else
            PurchaseBackendFactory.Register(_ => RevenueCatBehaviour.Create());
#endif
        }
    }

    // Owns the SDK's Purchases component and adapts it to IPurchaseBackend.
    //
    // A MonoBehaviour because two things here need frames: the SDK must be configured a frame
    // after its own Start() runs (see Configure below), and every call needs a timeout.
    public sealed class RevenueCatBehaviour : MonoBehaviour, IPurchaseBackend
    {
        // If the store has not answered in this long, we answer for it. RevenueCat has no
        // timeout of its own, and a callback that never fires is a permanent spinner.
        private const float CallTimeoutSeconds = 30f;

        private Purchases _purchases;
        private MonetizationKeys _config;

        // Offerings are cached so a purchase can find the Package object it needs. RevenueCat
        // purchases a Package, not a product id, and PurchasePackage is the supported path.
        private readonly Dictionary<string, Purchases.Package> _packagesByProductId =
            new Dictionary<string, Purchases.Package>(StringComparer.Ordinal);

        public BackendAvailability Availability { get; private set; } = BackendAvailability.Initializing;

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
            _purchases = gameObject.AddComponent<Purchases>();

            // Must be set before the SDK's own Start() runs, or it self-configures from the
            // Inspector fields — which are empty here, because we build this GameObject at
            // runtime rather than authoring it into a scene. Setting it now is safe: AddComponent
            // runs Awake immediately, Start only at the end of this frame.
            _purchases.useRuntimeSetup = true;

            // THE ORDERING HAZARD. Purchases.Configure() dereferences a wrapper field that is
            // assigned in Purchases.Start(). Calling Configure before that — from our Awake, or
            // synchronously right here — is a NullReferenceException, and it is almost certainly
            // the error RevenueCat's Editor warning is describing. One frame is the fix.
            yield return null;

            var builder = Purchases.PurchasesConfiguration.Builder.Init(_config.ApiKey);
            _purchases.Configure(builder.Build());

            Availability = BackendAvailability.Ready;
            Debug.Log("[Monetization] RevenueCat configured" +
                      (_config.UseTestStore ? " against the TEST STORE (not a real store)" : ""));
        }

        // ---- IPurchaseBackend -------------------------------------------------------------

        public void FetchProducts(Action<IReadOnlyList<StoreProductView>> onDone)
        {
            var once = Guard(onDone, Array.Empty<StoreProductView>());
            if (_purchases == null) { once(Array.Empty<StoreProductView>()); return; }

            _purchases.GetOfferings((offerings, error) =>
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
            });
        }

        private IReadOnlyList<StoreProductView> ReadOffering(Purchases.Offerings offerings)
        {
            _packagesByProductId.Clear();
            var result = new List<StoreProductView>();

            // Fetched by NAME rather than through Offerings.Current, so that someone flipping
            // the dashboard's current offering cannot silently empty our shop. Falls back to
            // Current only if the named offering is missing, which at least degrades to
            // "something" rather than nothing.
            Purchases.Offering offering = null;
            if (offerings.All != null)
                offerings.All.TryGetValue(RevenueCatNames.CosmeticsOffering, out offering);
            offering ??= offerings.Current;

            if (offering == null)
            {
                Debug.LogWarning("[Monetization] no offering named '" +
                                 RevenueCatNames.CosmeticsOffering + "' and no current offering; " +
                                 "check the RevenueCat dashboard");
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

            return result;
        }

        public void Purchase(string productId, Action<PurchaseResult> onDone)
        {
            var once = Guard(onDone,
                new PurchaseResult(PurchaseOutcome.UnknownUnsettled, productId, default,
                    "the store did not answer within " + CallTimeoutSeconds + "s"));

            if (_purchases == null)
            {
                once(PurchaseResult.Unavailable(productId, "RevenueCat is not configured yet"));
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
            _purchases.PurchasePackage(package, result =>
            {
                once(Translate(result, productId));
            });
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

            // Success as far as the store is concerned. Entitlements are still re-read from
            // CustomerInfo by PurchaseService before anything unlocks — hence SuccessCandidate.
            return new PurchaseResult(PurchaseOutcome.SuccessCandidate, productId);
        }

        // Google Play defers a purchase when payment needs another step — a slow card, parental
        // approval. The player has not been charged and owns nothing yet.
        private static bool IsPending(Purchases.Error error)
            => error?.ReadableErrorCode == "PaymentPendingError";

        public void Restore(Action<RestoreResult> onDone)
        {
            var once = Guard(onDone,
                new RestoreResult(RestoreOutcome.Failure, 0, "the store did not answer in time"));

            if (_purchases == null)
            {
                once(new RestoreResult(RestoreOutcome.Unavailable, 0, "RevenueCat is not configured yet"));
                return;
            }

            // RestorePurchases, not SyncPurchases: this is only ever reached from a button the
            // player pressed, and RevenueCat is explicit that RestorePurchases "should not be
            // triggered programmatically, since it may cause OS level sign-in prompts to appear".
            _purchases.RestorePurchases((info, error) =>
            {
                if (error != null)
                {
                    once(new RestoreResult(RestoreOutcome.Failure, 0, Describe(error)));
                    return;
                }

                // The count is recomputed by PurchaseService from the ledger afterwards, so the
                // number shown to the player matches what actually unlocked.
                once(new RestoreResult(RestoreOutcome.Completed));
            });
        }

        public void RefreshEntitlements(Action<EntitlementSnapshot> onDone)
        {
            var once = Guard(onDone, EntitlementSnapshot.Unreachable());
            if (_purchases == null) { once(EntitlementSnapshot.Unreachable()); return; }

            _purchases.GetCustomerInfo((info, error) =>
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
                once(new EntitlementSnapshot(true, ReadEntitlements(info)));
            });
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
        private Action<T> Guard<T>(Action<T> onDone, T timeoutValue)
        {
            bool fired = false;
            Coroutine watchdog = null;

            void Fire(T value)
            {
                if (fired) return;
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
            }

            watchdog = StartCoroutine(Timeout(() => Fire(timeoutValue)));
            return Fire;
        }

        private static IEnumerator Timeout(Action onTimeout)
        {
            yield return new WaitForSecondsRealtime(CallTimeoutSeconds);
            onTimeout();
        }

        private static string Describe(Purchases.Error error)
            => error == null
                ? "unknown error"
                : error.ReadableErrorCode + " (" + error.Code + "): " + error.Message;
    }
}
#endif
