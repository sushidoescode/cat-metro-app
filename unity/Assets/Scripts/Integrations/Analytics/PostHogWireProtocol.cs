using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;
using CatMetro.Application.Analytics;
using CatMetro.Application.EventTaxonomy;

namespace CatMetro.Integrations.Analytics
{
    // The complete analytics wire construction site. It emits no automatic properties and
    // accepts only fields declared by the named taxonomy row.
    public static class PostHogWireProtocol
    {
        private const int RequestTimeoutSeconds = 10;
        private const string UserAgent = "cat-metro-analytics/1";
        private const string EventUuidDomain = "cat-metro-posthog-event-v1";

        public static UnityWebRequest CreateKillSwitchRequest(AnalyticsTransportConfig config,
            string distinctId)
        {
            if (config == null || string.IsNullOrWhiteSpace(distinctId)) return null;
            var body = new JObject
            {
                ["api_key"] = config.ProjectToken,
                ["distinct_id"] = distinctId,
                ["geoip_disable"] = true,
                ["flag_keys_to_evaluate"] = new JArray(config.RemoteKillSwitchFlag),
            };
            return CreateRequest(config.Host.TrimEnd('/') + "/flags/?v=2", body);
        }

        public static UnityWebRequest CreateBatchRequest(AnalyticsTransportConfig config,
            string distinctId, IReadOnlyList<QueuedAnalyticsEvent> batch)
        {
            if (config == null || string.IsNullOrWhiteSpace(distinctId)
                || batch == null || batch.Count == 0)
                return null;
            try
            {
                var events = new JArray();
                foreach (var item in batch)
                {
                    if (!TryBuildEvent(distinctId, item, out var outbound))
                        return null;
                    events.Add(outbound);
                }
                var body = new JObject
                {
                    ["api_key"] = config.ProjectToken,
                    ["batch"] = events,
                };
                return CreateRequest(config.Host.TrimEnd('/') + "/batch", body);
            }
            catch
            {
                return null;
            }
        }

        private static bool TryBuildEvent(string distinctId, QueuedAnalyticsEvent item,
            out JObject outbound)
        {
            outbound = null;
            if (item == null || string.IsNullOrWhiteSpace(item.Name)
                || !IsQueueId(item.Id))
                return false;

            TaxonomyRow row = null;
            foreach (var candidate in Taxonomy.Rows)
                if (candidate.Name == item.Name) { row = candidate; break; }
            if (row == null) return false;

            var allowed = new HashSet<string>(row.RequiredParams);
            foreach (var optional in row.OptionalParams) allowed.Add(optional);
            var declared = new JObject();
            if (item.Params != null)
            {
                foreach (var property in item.Params.Properties())
                {
                    if (!allowed.Contains(property.Name)) continue;
                    if (!IsWirePrimitive(property.Value)) return false;
                    declared[property.Name] = property.Value.DeepClone();
                }
            }
            if (!Taxonomy.TryBuild(item.Name, declared, out _, out _)) return false;

            var properties = (JObject)declared.DeepClone();
            properties["cm_event_id"] = item.Id;
            properties["$geoip_disable"] = true;
            properties["$process_person_profile"] = false;
            // Old queue records predate CapturedAtUnixMs. Epoch is deliberately less accurate
            // than inventing a different capture time (and UUID) on every delivery attempt.
            long capturedAt = item.CapturedAtUnixMs > 0L
                ? item.CapturedAtUnixMs
                : 0L;
            outbound = new JObject
            {
                ["uuid"] = StableUuidV7(distinctId, item, capturedAt),
                ["event"] = item.Name,
                ["distinct_id"] = distinctId,
                ["timestamp"] = IsoTimestamp(capturedAt),
                ["properties"] = properties,
            };
            return true;
        }

        private static bool IsWirePrimitive(JToken token)
        {
            if (token == null) return false;
            switch (token.Type)
            {
                case JTokenType.String:
                    return ((string)token)?.Length <= 128;
                case JTokenType.Integer:
                case JTokenType.Float:
                case JTokenType.Boolean:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsQueueId(string value)
        {
            if (value == null || value.Length != 16) return false;
            foreach (char c in value)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }

        // UUIDv7 timestamp bytes come from the immutable capture time; the remaining bits are a
        // SHA-256-derived value over the install id and persisted queue identity. Retries and
        // process restarts therefore produce exactly the same valid UUID.
        private static string StableUuidV7(string distinctId, QueuedAnalyticsEvent item,
            long capturedAtUnixMs)
        {
            var bytes = new byte[16];
            ulong timestamp = (ulong)System.Math.Max(0L, capturedAtUnixMs);
            for (int i = 5; i >= 0; i--)
            {
                bytes[i] = (byte)(timestamp & 0xff);
                timestamp >>= 8;
            }
            string preimage = EventUuidDomain + "|" + distinctId + "|" + item.Id + "|"
                + item.Ord.ToString(CultureInfo.InvariantCulture) + "|"
                + capturedAtUnixMs.ToString(CultureInfo.InvariantCulture) + "|" + item.Name;
            byte[] digest;
            using (var sha = SHA256.Create())
                digest = sha.ComputeHash(Encoding.UTF8.GetBytes(preimage));
            bytes[6] = (byte)(0x70 | (digest[0] & 0x0f));
            bytes[7] = digest[1];
            bytes[8] = (byte)(0x80 | (digest[2] & 0x3f));
            for (int i = 9; i < 16; i++) bytes[i] = digest[i - 6];
            var hex = new StringBuilder(36);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i == 4 || i == 6 || i == 8 || i == 10) hex.Append('-');
                hex.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            }
            return hex.ToString();
        }

        private static string IsoTimestamp(long unixMilliseconds) =>
            DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).UtcDateTime
                .ToString("o", CultureInfo.InvariantCulture);

        private static UnityWebRequest CreateRequest(string url, JObject body)
        {
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(
                    body.ToString(Formatting.None))),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = RequestTimeoutSeconds,
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("User-Agent", UserAgent);
            return request;
        }

        public static bool TryParseKillSwitch(string json, string flagKey, out bool enabled)
        {
            enabled = false;
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(flagKey))
                return false;
            try
            {
                var root = JObject.Parse(json);
                if (root["errorsWhileComputingFlags"]?.Type == JTokenType.Boolean
                    && (bool)root["errorsWhileComputingFlags"])
                    return false;
                var value = root["flags"]?[flagKey]?["enabled"]
                    ?? root["featureFlags"]?[flagKey];
                if (value?.Type != JTokenType.Boolean) return false;
                enabled = (bool)value;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
