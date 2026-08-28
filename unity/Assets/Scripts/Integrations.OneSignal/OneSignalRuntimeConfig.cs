using System;
using UnityEngine;

namespace CatMetro.Integrations.OneSignal
{
    internal static class OneSignalAppId
    {
        internal static bool TryNormalize(string appId, out string normalizedAppId)
        {
            normalizedAppId = string.Empty;
            if (string.IsNullOrWhiteSpace(appId))
                return false;

            var candidate = appId.Trim();
            if (!Guid.TryParseExact(candidate, "D", out var parsed)
                || parsed == Guid.Empty)
                return false;

            normalizedAppId = parsed.ToString("D");
            return true;
        }
    }

    public static class OneSignalRuntimeConfig
    {
        private const string ResourcePath = "Config/onesignal";

        [Serializable]
        private sealed class Document
        {
            public string appId;
        }

        public static string LoadAppId()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            return asset != null && TryGetAppId(asset.text, out var appId)
                ? appId
                : string.Empty;
        }

        internal static bool TryGetAppId(string json, out string appId)
        {
            appId = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                var document = JsonUtility.FromJson<Document>(json);
                if (document == null || string.IsNullOrWhiteSpace(document.appId))
                    return false;

                return OneSignalAppId.TryNormalize(document.appId, out appId);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
