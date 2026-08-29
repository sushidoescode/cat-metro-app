using System;
using UnityEngine;

namespace CatMetro.Integrations
{
    // Selects one platform pair from a local Resources file. Values are deliberately never
    // included in diagnostics: app keys and ad-unit identifiers do not belong in logs.
    public sealed class RewardedAdsConfig
    {
        public const string ResourcePath = "Monetization/rewarded_ads_config";

        public string AppKey { get; }
        public string RewardedAdUnitId { get; }
        public string Problem { get; }
        public bool IsConfigured => string.IsNullOrEmpty(Problem);

        private RewardedAdsConfig(string appKey, string rewardedAdUnitId, string problem)
        {
            AppKey = appKey;
            RewardedAdUnitId = rewardedAdUnitId;
            Problem = problem;
        }

        public static RewardedAdsConfig Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
                return Unconfigured("no Resources/" + ResourcePath + ".json");
            return Parse(asset.text, UnityEngine.Application.platform);
        }

        public static RewardedAdsConfig Parse(string json, RuntimePlatform platform)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Unconfigured("rewarded ads config source was null or empty");

            Dto dto;
            try
            {
                dto = JsonUtility.FromJson<Dto>(json);
            }
            catch (Exception)
            {
                return Unconfigured("rewarded ads config is not valid JSON");
            }

            if (dto == null)
                return Unconfigured("rewarded ads config is not valid JSON");

            string appKey;
            string adUnitId;
            string platformName;
            switch (platform)
            {
                case RuntimePlatform.Android:
                    appKey = dto.androidAppKey;
                    adUnitId = dto.androidRewardedAdUnitId;
                    platformName = "Android";
                    break;
                case RuntimePlatform.IPhonePlayer:
                    appKey = dto.iosAppKey;
                    adUnitId = dto.iosRewardedAdUnitId;
                    platformName = "iOS";
                    break;
                default:
                    return Unconfigured("rewarded ads are unavailable on unsupported platform " +
                                        platform);
            }

            if (string.IsNullOrWhiteSpace(appKey))
                return Unconfigured(platformName + " app key is blank");
            if (string.IsNullOrWhiteSpace(adUnitId))
                return Unconfigured(platformName + " rewarded ad-unit ID is blank");
            return new RewardedAdsConfig(appKey, adUnitId, null);
        }

        private static RewardedAdsConfig Unconfigured(string problem)
            => new RewardedAdsConfig(null, null, problem);

        [Serializable]
        private sealed class Dto
        {
            public string iosAppKey;
            public string androidAppKey;
            public string iosRewardedAdUnitId;
            public string androidRewardedAdUnitId;
        }
    }
}
