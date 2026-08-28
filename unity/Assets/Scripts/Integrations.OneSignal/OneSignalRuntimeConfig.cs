using System;
using UnityEngine;

namespace CatMetro.Integrations.OneSignal
{
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

                var candidate = document.appId.Trim();
                if (!Guid.TryParse(candidate, out _))
                    return false;

                appId = candidate;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
