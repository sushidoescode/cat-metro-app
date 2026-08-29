using System.Text;
using NUnit.Framework;
using CatMetro.Application.Analytics;
using CatMetro.Content;

namespace CatMetro.Tests.Analytics
{
    public sealed class AnalyticsTransportConfigTests
    {
        private static byte[] Json(string enabled = "true", string token = "phc_public",
            string host = "https://eu.i.posthog.com",
            string flag = "cat-metro-analytics-enabled") => Encoding.UTF8.GetBytes(
                "{\"schemaVersion\":1,\"enabled\":" + enabled
                + ",\"projectToken\":\"" + token + "\",\"host\":\"" + host
                + "\",\"remoteKillSwitchFlag\":\"" + flag + "\"}");

        [Test]
        public void EnabledConfig_ParsesOnlyTheApprovedHttpsCollector()
        {
            var result = AnalyticsTransportConfig.Parse(Json());

            Assert.That(result.Ok, Is.True, result.Error?.ToString());
            Assert.That(result.Value.Enabled, Is.True);
            Assert.That(result.Value.ProjectToken, Is.EqualTo("phc_public"));
            Assert.That(result.Value.Host, Is.EqualTo("https://eu.i.posthog.com"));
            Assert.That(result.Value.RemoteKillSwitchFlag,
                Is.EqualTo("cat-metro-analytics-enabled"));
        }

        [TestCase("http://eu.i.posthog.com")]
        [TestCase("https://example.invalid")]
        public void EnabledConfig_UnapprovedCollectorFailsClosed(string host)
        {
            var result = AnalyticsTransportConfig.Parse(Json(host: host));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error.Kind, Is.EqualTo(ContentErrorKind.BoundViolation));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void EnabledConfig_MissingPublicProjectTokenFailsClosed(string token)
        {
            var result = AnalyticsTransportConfig.Parse(Json(token: token));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error.Kind, Is.EqualTo(ContentErrorKind.MissingField));
        }

        [Test]
        public void DisabledConfig_AllowsAnEmptyTokenWithoutInitializingACollector()
        {
            var result = AnalyticsTransportConfig.Parse(Json(enabled: "false", token: ""));

            Assert.That(result.Ok, Is.True, result.Error?.ToString());
            Assert.That(result.Value.Enabled, Is.False);
        }

        [Test]
        public void MalformedConfig_FailsWithoutThrowing()
        {
            var result = AnalyticsTransportConfig.Parse(Encoding.UTF8.GetBytes("{broken"));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error.Kind, Is.EqualTo(ContentErrorKind.MalformedJson));
        }
    }
}
