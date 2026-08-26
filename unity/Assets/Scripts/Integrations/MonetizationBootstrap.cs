using System;
using System.Collections.Generic;
using CatMetro.Services.Purchases;
using UnityEngine;

namespace CatMetro.Integrations
{
    // How a live backend gets in without this assembly referencing the SDK.
    //
    // CatMetro.Integrations.RevenueCat references THIS assembly, not the other way round, and
    // registers its factory during RuntimeInitializeLoadType.AfterAssembliesLoaded. When the SDK
    // package is not installed that assembly is not compiled at all (its asmdef carries
    // defineConstraints: ["CATMETRO_REVENUECAT"]), nothing registers, and Register is simply
    // never called — so this file has no #if in it and no knowledge that RevenueCat exists.
    public static class PurchaseBackendFactory
    {
        private static Func<PurchaseService, IPurchaseBackend> _factory;

        public static bool HasFactory => _factory != null;

        public static void Register(Func<PurchaseService, IPurchaseBackend> factory)
            => _factory = factory;

        internal static IPurchaseBackend Create(PurchaseService service)
        {
            if (_factory == null) return null;
            try
            {
                return _factory(service);
            }
            catch (Exception e)
            {
                // A backend that throws while being constructed must not take the game with it.
                Debug.LogError("[Monetization] purchase backend factory threw; " +
                               "continuing without a store. " + e);
                return null;
            }
        }
    }

    // Boots the purchase layer with no changes to GameRoot.
    //
    // GameRoot is the composition root for the game SESSION, and nothing about purchases belongs
    // in a tick loop — so this uses RuntimeInitializeOnLoadMethod instead. That also keeps this
    // lane out of a file six other lanes are editing.
    public static class MonetizationBootstrap
    {
        public const string CatalogResourcePath = "Monetization/product_catalog";
        public const string PlacementsResourcePath = "Monetization/rewarded_placements";

        private static bool _booted;

        public static RewardedPlacementCatalog Placements { get; private set; } =
            RewardedPlacementCatalog.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Boot()
        {
            if (_booted) return;
            _booted = true;

            // Wrapped whole. This runs before the first scene loads, so an exception here would
            // be a black screen on launch — the single worst outcome for a system whose entire
            // job is optional.
            try
            {
                BootInner();
            }
            catch (Exception e)
            {
                Debug.LogError("[Monetization] boot failed; the game continues with purchases " +
                               "unavailable. " + e);
            }
        }

        private static void BootInner()
        {
            var catalog = PurchaseCatalog.Parse(ReadResource(CatalogResourcePath));
            LogProblems("product catalogue", catalog.Problems);

            Placements = RewardedPlacementCatalog.Parse(ReadResource(PlacementsResourcePath), catalog);
            LogProblems("rewarded placements", Placements.Problems);

            var service = new PurchaseService(catalog);
            PurchaseRuntime.Install(service);

            var backend = PurchaseBackendFactory.Create(service);
            if (backend != null) service.AttachBackend(backend);

            // The pump owns the two things that need a frame: re-reading entitlements when the
            // app comes back to the foreground, and noticing that a timed unlock has lapsed.
            var host = new GameObject("[Monetization]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<MonetizationPump>().Bind(service);

            service.Refresh();

            Debug.Log("[Monetization] ready — " + catalog.Products.Count + " products, " +
                      Placements.Placements.Count + " rewarded placements, backend: " +
                      service.Availability);
        }

        private static string ReadResource(string path)
        {
            var asset = Resources.Load<TextAsset>(path);
            if (asset != null) return asset.text;

            Debug.LogWarning("[Monetization] Resources/" + path + " is missing");
            return null;
        }

        private static void LogProblems(string what, IReadOnlyList<string> problems)
        {
            if (problems == null || problems.Count == 0) return;

            // Loud on purpose. AGENTS.md records the cost of the opposite: CatModelCatalog
            // rejects a bad prefab silently, so an empty-looking screen has no log line
            // explaining it. An empty shop must never be that.
            for (int i = 0; i < problems.Count; i++)
                Debug.LogWarning("[Monetization] " + what + ": " + problems[i]);
        }

        // Test seam only. Lets an EditMode test boot a second time against fresh Resources.
        internal static void ResetForTests()
        {
            _booted = false;
            Placements = RewardedPlacementCatalog.Empty;
        }
    }

    // Small and deliberately dull.
    internal sealed class MonetizationPump : MonoBehaviour
    {
        private const float PruneIntervalSeconds = 5f;

        private PurchaseService _service;
        private float _nextPrune;

        internal void Bind(PurchaseService service) => _service = service;

        private void Update()
        {
            if (_service == null) return;
            if (Time.unscaledTime < _nextPrune) return;
            _nextPrune = Time.unscaledTime + PruneIntervalSeconds;

            // RevenueCat does not push an expiry event, so a timed unlock (a rewarded-ad grant,
            // a lapsed subscription) only stops being active because something checks the clock.
            // A player can sit on the wardrobe screen while one runs out.
            _service.PruneExpiredLeases();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) _service?.RefreshEntitlements();
        }

        private void OnApplicationPause(bool paused)
        {
            // Coming back from the Play purchase flow lands here on Android, and it is the most
            // reliable moment to notice that a purchase completed in another activity.
            if (!paused) _service?.RefreshEntitlements();
        }
    }
}
