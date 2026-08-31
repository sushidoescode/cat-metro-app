using System;
using CatMetro.Services.Ads;

namespace CatMetro.Integrations
{
    // Vendor assemblies register here without putting vendor types, reflection, or native calls
    // in the always-compiled Integrations assembly.
    public static class RewardedAdProviderFactory
    {
        private static Func<RewardedAdsConfig, IRewardedAdProvider> _factory;

        public static void Register(Func<RewardedAdsConfig, IRewardedAdProvider> factory)
            => _factory = factory;

        internal static IRewardedAdProvider Create(RewardedAdsConfig config)
        {
            if (config == null || !config.IsConfigured || _factory == null) return null;
            try
            {
                return _factory(config);
            }
            catch
            {
                // Optional monetization construction is fail-closed. Do not include config
                // values or a vendor exception that may echo them in a shipped log.
                return null;
            }
        }

        internal static void ResetForTests() => _factory = null;
    }
}
