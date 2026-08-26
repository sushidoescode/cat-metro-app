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

            if (key.StartsWith(Placeholder))
                return new MonetizationKeys(key, testStore, "config still holds the example placeholder key");

            if (key.Length == 0)
                return new MonetizationKeys(key, testStore,
                    "config has no API key for " + platform);

            // THE GUARD THAT MATTERS. RevenueCat, verbatim: "You must NEVER submit an app to the
            // App Store or Google Play that is configured with a Test Store API key." A Test
            // Store key makes every purchase fake, so shipping one means a store listing where
            // nobody can actually buy anything and no revenue is ever recorded.
            //
            // Refusing to configure at all is the safe failure: cosmetics stay locked, which is
            // visibly wrong in testing, rather than purchases silently succeeding for free.
            if (testStore && !isDebugBuild)
                return new MonetizationKeys(key, true,
                    "refusing a Test Store key in a release build — RevenueCat forbids shipping one");

            return new MonetizationKeys(key, testStore, null);
        }
    }
}
