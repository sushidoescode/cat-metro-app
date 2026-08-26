using System.IO;
using NUnit.Framework;
using UnityEngine;
using CatMetro.Application.Analytics;
using CatMetro.Tests.Domain;

namespace CatMetro.Tests.Engine
{
    public sealed class AnalyticsResourceConfigTests
    {
        [Test]
        public void ShippedAnalyticsConfig_ExistsAndFailsClosedUntilHumanSetup()
        {
            var asset = Resources.Load<TextAsset>("Config/analytics_transport");

            Assert.That(asset, Is.Not.Null);
            var parsed = AnalyticsTransportConfig.Parse(asset.bytes);
            Assert.That(parsed.Ok, Is.True, parsed.Error?.ToString());
            Assert.That(parsed.Value.Enabled, Is.False);
            Assert.That(parsed.Value.ProjectToken, Is.Empty);
        }

        [Test]
        public void ShippedPackageAndAssemblies_HaveNoSecondAnalyticsQueueDependency()
        {
            string root = Fixtures.RepoRoot();
            string manifest = File.ReadAllText(Path.Combine(root, "unity", "Packages",
                "manifest.json"));
            string packageLock = File.ReadAllText(Path.Combine(root, "unity", "Packages",
                "packages-lock.json"));
            string integrationAssembly = File.ReadAllText(Path.Combine(root, "unity", "Assets",
                "Scripts", "Integrations", "Analytics", "CatMetro.Integrations.Analytics.asmdef"));

            Assert.That(manifest, Does.Not.Contain("com.posthog.unity"));
            Assert.That(packageLock, Does.Not.Contain("com.posthog.unity"));
            Assert.That(integrationAssembly, Does.Not.Contain("\"PostHog\""));
        }
    }
}
