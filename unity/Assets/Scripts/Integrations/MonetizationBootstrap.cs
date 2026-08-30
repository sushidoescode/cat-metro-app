using System;
using System.Collections.Generic;
using System.Globalization;
using CatMetro.Services.Ads;
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

        internal static void ResetForTests() => _factory = null;
    }

    // Boots the purchase service and store backend independently of GameRoot. GameRoot composes
    // the visible Wardrobe surface, but it never owns SDK setup and no purchase work enters the
    // game session tick loop.
    public static class MonetizationBootstrap
    {
        public const string CatalogResourcePath = "Monetization/product_catalog";
        public const string PlacementsResourcePath = "Monetization/rewarded_placements";

        private static bool _booted;
        private static GameObject _host;
        private static RewardedAdsComposition _rewardedAds;
        private static RewardedAdsConfig _rewardedAdsConfigForTests;

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

            var rewardedConfig = _rewardedAdsConfigForTests ?? RewardedAdsConfig.Load();
            _rewardedAds = new RewardedAdsComposition(service, Placements,
                () => RewardedAdProviderFactory.Create(rewardedConfig),
                backend as IAdEventReporter, LocalDateKey);
            _rewardedAds.Bind();

            // The pump owns the two things that need a frame: re-reading entitlements when the
            // app comes back to the foreground, and noticing that a timed unlock has lapsed.
            _host = new GameObject("[Monetization]");
            if (UnityEngine.Application.isPlaying)
                UnityEngine.Object.DontDestroyOnLoad(_host);
            _host.hideFlags = HideFlags.HideAndDontSave;
            _host.AddComponent<MonetizationPump>().Bind(service, _rewardedAds);

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

        private static string LocalDateKey()
            => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        internal static void SetRewardedAdsConfigForTests(RewardedAdsConfig config)
            => _rewardedAdsConfigForTests = config;

        // Test seam only. Lets an EditMode test boot a second time against fresh Resources.
        internal static void ResetForTests()
        {
            _rewardedAds?.Dispose();
            _rewardedAds = null;
            if (_host != null)
            {
                UnityEngine.Object.DestroyImmediate(_host);
                _host = null;
            }
            _booted = false;
            _rewardedAdsConfigForTests = null;
            Placements = RewardedPlacementCatalog.Empty;
        }
    }

    // Small and deliberately dull.
    internal sealed class MonetizationPump : MonoBehaviour
    {
        private const float PruneIntervalSeconds = 5f;

        private PurchaseService _service;
        private RewardedAdsComposition _rewardedAds;
        private float _nextPrune;

        internal void Bind(PurchaseService service, RewardedAdsComposition rewardedAds)
        {
            _service = service;
            _rewardedAds = rewardedAds;
        }

        internal void Update()
        {
            _rewardedAds?.DrainMainThreadAdEvents();
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

        internal void OnApplicationPause(bool paused)
        {
            // Coming back from the Play purchase flow lands here on Android, and it is the most
            // reliable moment to notice that a purchase completed in another activity.
            if (_rewardedAds != null)
                _rewardedAds.OnApplicationPause(paused);
            else if (!paused)
                _service?.RefreshEntitlements();
        }

        internal void OnDestroy()
        {
            _rewardedAds?.Dispose();
            _rewardedAds = null;
        }
    }
}
