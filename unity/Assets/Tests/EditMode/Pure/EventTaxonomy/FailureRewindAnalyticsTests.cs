using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Application.EventTaxonomy;
using CatMetro.Services;
using CatMetro.Tests.Analytics;

namespace CatMetro.Tests.EventTaxonomy
{
    public sealed class FailureRewindAnalyticsTests
    {
        private static readonly Action<IAnalytics>[] Calls =
        {
            sink => FailureRewindAnalytics.OfferViewed(sink, "rewind_failure", "L007"),
            sink => FailureRewindAnalytics.OfferDeclined(sink, "rewind_failure"),
            sink => FailureRewindAnalytics.AdDisplayed(sink, "rewind_failure", "actual-network", "actual-unit"),
            sink => FailureRewindAnalytics.AdCompleted(sink, "rewind_failure", "actual-network"),
            sink => FailureRewindAnalytics.AdFailed(sink, "rewind_failure", "actual-network", "731"),
            sink => FailureRewindAnalytics.RewindApplied(sink, "L007", "7"),
        };

        [Test]
        public void EmitsOnlyTheExistingEventsWithExactFactualParameters()
        {
            var sink = new RecordingAnalytics();
            foreach (var call in Calls) call(sink);

            Assert.That(sink.Records.Select(e => e.Name), Is.EqualTo(new[]
            {
                "ad_offer_viewed", "ad_offer_declined", "rewarded_ad_started",
                "rewarded_ad_completed", "rewarded_ad_failed", "rewind_used",
            }));
            var expected = new[]
            {
                "{'placement':'rewind_failure','level_id':'L007'}",
                "{'placement':'rewind_failure'}",
                "{'placement':'rewind_failure','network':'actual-network','ad_unit':'actual-unit'}",
                "{'placement':'rewind_failure','network':'actual-network','reward_type':'rewind','reward_amount':1}",
                "{'placement':'rewind_failure','network':'actual-network','error_code':'731'}",
                "{'level_id':'L007','source':'rewarded','balance_after':'7'}",
            };
            for (int i = 0; i < expected.Length; i++)
                Assert.That(JToken.DeepEquals(sink.Records[i].Params, JObject.Parse(expected[i])),
                    Is.True, sink.Records[i].Name + ": " + sink.Records[i].Params);
        }

        [TestCase(null)]
        [TestCase("")]
        public void MissingProviderMetadataUsesUnknown(string metadata)
        {
            var sink = new RecordingAnalytics();
            FailureRewindAnalytics.AdDisplayed(sink, "rewind_failure", metadata, metadata);
            FailureRewindAnalytics.AdCompleted(sink, "rewind_failure", metadata);
            FailureRewindAnalytics.AdFailed(sink, "rewind_failure", metadata, "Cancelled");

            Assert.That(sink.Records, Has.Count.EqualTo(3));
            foreach (var record in sink.Records)
                Assert.That((string)record.Params["network"], Is.EqualTo("unknown"));
            Assert.That((string)sink.Records[0].Params["ad_unit"], Is.EqualTo("unknown"));
            Assert.That((string)sink.Records[2].Params["error_code"], Is.EqualTo("Cancelled"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EveryEventToleratesUnavailableAnalytics(bool throws)
        {
            IAnalytics sink = throws ? new FailingAnalytics() : null;
            foreach (var call in Calls)
                Assert.DoesNotThrow(() => call(sink));
        }

        [Test]
        public void CompletionLogFailureDoesNotBlockTheSubsequentRewindEvent()
        {
            var sink = new FailingAnalytics("rewarded_ad_completed");

            Assert.DoesNotThrow(() =>
            {
                FailureRewindAnalytics.AdCompleted(sink, "rewind_failure", "actual-network");
                FailureRewindAnalytics.RewindApplied(sink, "L007", "7");
            });

            Assert.That(sink.Recorded.Records.Select(e => e.Name),
                Is.EqualTo(new[] { "rewind_used" }));
            Assert.That((string)sink.Recorded.Records[0].Params["source"], Is.EqualTo("rewarded"));
        }

        private sealed class FailingAnalytics : IAnalytics
        {
            private readonly string _failedEvent;
            public readonly RecordingAnalytics Recorded = new RecordingAnalytics();
            public FailingAnalytics(string failedEvent = null) => _failedEvent = failedEvent;
            public int QueuedEventCount => Recorded.QueuedEventCount;
            public void SetUserProperty(UserPropertyKey key, string value) { }
            public void Log(in AnalyticsEvent e)
            {
                if (_failedEvent == null || e.Name == _failedEvent)
                    throw new InvalidOperationException("analytics unavailable");
                Recorded.Log(e);
            }
        }
    }
}
