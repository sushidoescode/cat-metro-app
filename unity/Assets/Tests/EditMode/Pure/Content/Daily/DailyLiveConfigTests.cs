using System.IO;
using System.Text;
using NUnit.Framework;
using CatMetro.Content.Daily;
using CatMetro.Tests.Domain;

namespace CatMetro.Tests.Daily
{
    public sealed class DailyLiveConfigTests
    {
        [Test]
        public void ShippedConfig_DefaultsToSevenCampaignCompletions()
        {
            var bytes = File.ReadAllBytes(Path.Combine(
                Fixtures.RepoRoot(), "config", "daily_live.json"));
            var parsed = DailyLiveConfig.Parse(bytes);

            Assert.That(parsed.Ok, Is.True, parsed.Error?.ToString());
            Assert.That(parsed.Value.UnlockAfterCampaignCompletions, Is.EqualTo(7));
            Assert.That(parsed.Value.UnlockAfterCampaignCompletions, Is.EqualTo(
                DailyLiveConfig.ProductionDefaultUnlockAfterCampaignCompletions));
        }

        [Test]
        public void Zero_IsAValidDataOnlyDemoUnlock()
        {
            var parsed = Parse("{\"schemaVersion\":1,\"unlockAfterCampaignCompletions\":0}");

            Assert.That(parsed.Ok, Is.True, parsed.Error?.ToString());
            Assert.That(parsed.Value.UnlockAfterCampaignCompletions, Is.Zero);
        }

        [TestCase("{\"schemaVersion\":1,\"unlockAfterCampaignCompletions\":-1}")]
        [TestCase("{\"schemaVersion\":1,\"unlockAfterCampaignCompletions\":7.0}")]
        [TestCase("{\"schemaVersion\":1}")]
        [TestCase("{\"schemaVersion\":2,\"unlockAfterCampaignCompletions\":7}")]
        [TestCase("{\"schemaVersion\":1,\"unlockAfterCampaignCompletions\":7,\"unlockAtLevel\":\"L007\"}")]
        [TestCase("not-json")]
        public void InvalidConfig_IsATypedFailure(string json)
        {
            Assert.That(Parse(json).Ok, Is.False);
        }

        [Test]
        public void NullConfig_IsATypedFailure()
        {
            Assert.That(DailyLiveConfig.Parse(null).Ok, Is.False);
        }

        private static CatMetro.Content.ContentResult<DailyLiveConfig> Parse(string json) =>
            DailyLiveConfig.Parse(Encoding.UTF8.GetBytes(json));
    }
}
