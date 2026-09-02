using Newtonsoft.Json.Linq;

namespace CatMetro.Services.Purchases
{
    public enum StorePlatform
    {
        GooglePlay,
        Apple
    }

    // Parsing and validation for the RevenueCat API key configuration.
    //
    // Pure, and in Services rather than Integrations, for one reason: the release-build guard
    // below is a real safety rule with a real consequence, and a rule nothing can test is a rule
    // that will eventually be wrong. Integrations keeps only the two facts that need Unity —
    // which platform we are on, and whether this is a debug build.
    //
    // NO KEY IS EVER COMMITTED. The file this parses is gitignored; see MonetizationConfig.
    public readonly struct MonetizationKeys
    {
        public readonly string ApiKey;
        public readonly bool UseTestStore;

        // Empty when the config is usable. Non-empty is the reason it is not, phrased for a log.
        public readonly string Problem;

        public bool IsConfigured => string.IsNullOrEmpty(Problem);

        // The placeholder shipped in revenuecat_config.example.json. Recognising it by name turns
        // "the shop is mysteriously empty" into one clear line at launch.
        public const string Placeholder = "REPLACE_ME";

        private MonetizationKeys(string apiKey, bool useTestStore, string problem)
        {
            ApiKey = apiKey;
            UseTestStore = useTestStore;
            Problem = problem;
        }

        public static MonetizationKeys Unconfigured(string problem)
            => new MonetizationKeys(null, false, problem);

        // Never throws. A hand-edited config with a trailing comma must not take the game down.
        public static MonetizationKeys Parse(string json, StorePlatform platform, bool isDebugBuild)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Unconfigured("config file is empty");

            JObject root;
            try { root = JObject.Parse(json); }
            catch (System.Exception e)
            {
                return Unconfigured("config file is not valid JSON: " + e.Message);
            }

            var key = ((string)root[platform == StorePlatform.Apple ? "appleApiKey" : "googleApiKey"]
                       ?? string.Empty).Trim();
            bool testStore = (bool?)root["useTestStore"] ?? false;

            if (key.StartsWith(Placeholder, System.StringComparison.Ordinal))
                return new MonetizationKeys(key, testStore, "config still holds the example placeholder key");

            if (key.Length == 0)
                return new MonetizationKeys(key, testStore,
                    "config has no API key for " + platform);

            if (key.StartsWith("sk_", System.StringComparison.Ordinal))
                return new MonetizationKeys(key, testStore,
                    "refusing a RevenueCat secret key — Unity may contain only public SDK keys");

            // THE GUARD THAT MATTERS. RevenueCat says never submit an app configured with a
            // Test Store API key. The prefix is authoritative: trusting only the editable flag
            // lets a real `test_` key masquerade as production and makes every live purchase fake.
            // Refusing to configure is the safe failure: cosmetics stay visibly locked instead
            // of fake purchases silently succeeding in the store build.
            bool isTestStoreKey = key.StartsWith("test_", System.StringComparison.Ordinal);
            if (isTestStoreKey && !isDebugBuild)
                return new MonetizationKeys(key, true,
                    "refusing a Test Store key in a release build — RevenueCat forbids shipping one");

            if (isTestStoreKey != testStore)
                return new MonetizationKeys(key, testStore,
                    "useTestStore must be true exactly when the SDK key starts with test_");

            if (!isTestStoreKey)
            {
                string requiredPrefix = platform == StorePlatform.Apple ? "appl_" : "goog_";
                if (!key.StartsWith(requiredPrefix, System.StringComparison.Ordinal))
                    return new MonetizationKeys(key, false,
                        "config key for " + platform + " must start with " + requiredPrefix);
            }

            return new MonetizationKeys(key, testStore, null);
        }
    }
}
