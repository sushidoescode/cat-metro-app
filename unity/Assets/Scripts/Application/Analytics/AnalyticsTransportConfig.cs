using Newtonsoft.Json.Linq;

namespace CatMetro.Application.Analytics
{
    public sealed class AnalyticsTransportConfig
    {
        public readonly bool Enabled;
        public readonly string ProjectToken;
        public readonly string Host;
        public readonly string RemoteKillSwitchFlag;

        private AnalyticsTransportConfig(bool enabled, string projectToken, string host,
            string remoteKillSwitchFlag)
        {
            Enabled = enabled;
            ProjectToken = projectToken;
            Host = host;
            RemoteKillSwitchFlag = remoteKillSwitchFlag;
        }

        public static CatMetro.Content.ContentResult<AnalyticsTransportConfig> Parse(byte[] bytes)
        {
            try
            {
                if (bytes == null)
                    return Fail(CatMetro.Content.ContentErrorKind.MalformedJson,
                        "analytics transport config is null");
                var token = CatMetro.Content.ContentJson.LoadToken(
                    System.Text.Encoding.UTF8.GetString(bytes));
                if (!(token is JObject root))
                    return Fail(CatMetro.Content.ContentErrorKind.MalformedJson,
                        "analytics transport config root must be an object");
                if (!(root["schemaVersion"] is JValue schema)
                    || schema.Type != JTokenType.Integer || (int)schema != 1)
                    return Fail(CatMetro.Content.ContentErrorKind.SchemaVersionMismatch,
                        "analytics transport schemaVersion must be 1");
                if (!(root["enabled"] is JValue enabledValue)
                    || enabledValue.Type != JTokenType.Boolean)
                    return Fail(CatMetro.Content.ContentErrorKind.MissingField,
                        "enabled (boolean) is required");
                if (!(root["projectToken"] is JValue tokenValue)
                    || tokenValue.Type != JTokenType.String)
                    return Fail(CatMetro.Content.ContentErrorKind.MissingField,
                        "projectToken (string) is required");
                if (!(root["host"] is JValue hostValue)
                    || hostValue.Type != JTokenType.String)
                    return Fail(CatMetro.Content.ContentErrorKind.MissingField,
                        "host (string) is required");
                if (!(root["remoteKillSwitchFlag"] is JValue flagValue)
                    || flagValue.Type != JTokenType.String)
                    return Fail(CatMetro.Content.ContentErrorKind.MissingField,
                        "remoteKillSwitchFlag (string) is required");

                bool enabled = (bool)enabledValue;
                string projectToken = ((string)tokenValue)?.Trim() ?? "";
                string host = ((string)hostValue)?.Trim().TrimEnd('/') ?? "";
                string flag = ((string)flagValue)?.Trim() ?? "";
                if (host != "https://eu.i.posthog.com" && host != "https://us.i.posthog.com")
                    return Fail(CatMetro.Content.ContentErrorKind.BoundViolation,
                        "host must be an approved HTTPS analytics collector");
                if (enabled && projectToken.Length == 0)
                    return Fail(CatMetro.Content.ContentErrorKind.MissingField,
                        "projectToken is required when analytics is enabled");
                if (enabled && flag.Length == 0)
                    return Fail(CatMetro.Content.ContentErrorKind.MissingField,
                        "remoteKillSwitchFlag is required when analytics is enabled");

                return CatMetro.Content.ContentResult<AnalyticsTransportConfig>.Success(
                    new AnalyticsTransportConfig(enabled, projectToken, host, flag));
            }
            catch (System.Exception ex)
            {
                return Fail(CatMetro.Content.ContentErrorKind.MalformedJson,
                    "analytics transport config rejected: " + ex.GetType().Name);
            }
        }

        private static CatMetro.Content.ContentResult<AnalyticsTransportConfig> Fail(
            CatMetro.Content.ContentErrorKind kind, string detail) =>
            CatMetro.Content.ContentResult<AnalyticsTransportConfig>.Failure(kind, detail);
    }
}
