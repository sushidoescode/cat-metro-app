using CatMetro.Services.Purchases;
using UnityEngine;

namespace CatMetro.Integrations
{
    // Loads the RevenueCat API key, and nothing else — the parsing and the safety rules live in
    // CatMetro.Services.Purchases.MonetizationKeys where they can be tested. This file supplies
    // the two facts that need Unity: which store platform we are built for, and whether this is
    // a debug build.
    //
    // NO KEY IS COMMITTED TO THIS REPOSITORY, EVER. The key is read at runtime from
    //     unity/Assets/Resources/Monetization/revenuecat_config.json
    // which is gitignored. A committed sibling, revenuecat_config.example.json, documents the
    // shape. If the real file is absent — the state of every fresh clone and of CI — the SDK is
    // never configured, the game runs on the null backend, and every cosmetic stays locked. That
    // is a working game, not a broken one.
    //
    // On which key this is: RevenueCat PUBLIC SDK keys (`goog_` for Google Play, `appl_` for
    // Apple) are designed to ship inside an app binary — they can only do what the SDK does. It
    // is the SECRET keys (`sk_`) that must never leave a server, and this project has no use for
    // one. If you ever want an `sk_` key in Unity, the answer is a server, not a build flag.
    public static class MonetizationConfig
    {
        public const string ResourcePath = "Monetization/revenuecat_config";

        public static MonetizationKeys Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                return MonetizationKeys.Unconfigured(
                    "no Resources/" + ResourcePath + ".json (expected on a fresh clone and in CI; " +
                    "see docs/runbooks/revenuecat-setup.md)");
            }

            return MonetizationKeys.Parse(asset.text, Platform, Debug.isDebugBuild);
        }

        private static StorePlatform Platform =>
#if UNITY_IOS || UNITY_VISIONOS
            StorePlatform.Apple;
#else
            StorePlatform.GooglePlay;
#endif
    }
}
