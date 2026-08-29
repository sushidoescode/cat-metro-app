using System;
using Newtonsoft.Json.Linq;

namespace CatMetro.Application.Analytics
{
    public static class AnalyticsInstallIdentity
    {
        public const string ProfileKey = "analyticsInstallId";

        public static bool TryGetOrCreate(IAnalyticsProfileStore profileStore,
            Func<string> newIdentifier,
            out string identifier)
        {
            identifier = null;
            if (profileStore == null || newIdentifier == null) return false;
            try
            {
                var profile = profileStore.Profile;
                if (profile == null) return false;
                string existing = (string)profile[ProfileKey];
                if (IsValid(existing))
                {
                    identifier = existing;
                    return true;
                }

                string candidate = newIdentifier();
                if (!IsValid(candidate)) return false;
                var previous = profile[ProfileKey]?.DeepClone();
                profile[ProfileKey] = candidate;
                bool committed;
                try { committed = profileStore.CommitDurable(); }
                catch { committed = false; }
                if (committed)
                {
                    identifier = candidate;
                    return true;
                }
                if (previous == null) profile.Remove(ProfileKey);
                else profile[ProfileKey] = previous;
                return false;
            }
            catch
            {
                return false;
            }
        }

        // Release launch preparation makes the install marker and its creation time one durable
        // pre-network fact. Without this, a kill between two writes could emit first_open twice
        // for the same persistent identifier on the next launch.
        public static bool TryPrepareLaunch(IAnalyticsProfileStore profileStore,
            Func<string> newIdentifier, long nowUnixSeconds,
            out string identifier, out bool firstOpen)
        {
            identifier = null;
            firstOpen = false;
            if (profileStore == null || newIdentifier == null || nowUnixSeconds <= 0L)
                return false;
            try
            {
                var profile = profileStore.Profile;
                if (profile == null) return false;
                string existing = (string)profile[ProfileKey];
                bool needsIdentifier = !IsValid(existing);
                long createdAt = (long?)profile["createdAtUtc"] ?? 0L;
                bool needsCreationTime = createdAt <= 0L;
                if (!needsIdentifier && !needsCreationTime)
                {
                    identifier = existing;
                    return true;
                }

                var previousIdentifier = profile[ProfileKey]?.DeepClone();
                var previousCreatedAt = profile["createdAtUtc"]?.DeepClone();
                string selected = existing;
                if (needsIdentifier)
                {
                    selected = newIdentifier();
                    if (!IsValid(selected)) return false;
                    profile[ProfileKey] = selected;
                }
                if (needsCreationTime) profile["createdAtUtc"] = nowUnixSeconds;

                bool committed;
                try { committed = profileStore.CommitDurable(); }
                catch { committed = false; }
                if (committed)
                {
                    identifier = selected;
                    firstOpen = needsCreationTime;
                    return true;
                }

                Restore(profile, ProfileKey, previousIdentifier);
                Restore(profile, "createdAtUtc", previousCreatedAt);
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static void Restore(JObject profile, string key, JToken previous)
        {
            if (previous == null) profile.Remove(key);
            else profile[key] = previous;
        }

        public static bool IsValid(string value)
        {
            if (value == null || value.Length != 32) return false;
            foreach (char c in value)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }
    }
}
