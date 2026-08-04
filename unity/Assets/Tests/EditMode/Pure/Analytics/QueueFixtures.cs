using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Application.Analytics;
using CatMetro.Application.Save;
using CatMetro.Services;
using CatMetro.Tests.Save;

namespace CatMetro.Tests.Analytics
{
    // CM-C8 fixtures: temp roots and the recording/faulting filesystem come from the CM-C7
    // suite (one truth); the transport seam records every batch it is handed.
    public static class QFixtures
    {
        public sealed class RecordingTransport : IAnalyticsTransport
        {
            public readonly List<List<QueuedAnalyticsEvent>> Batches =
                new List<List<QueuedAnalyticsEvent>>();
            public bool Available = true;

            public bool Deliver(IReadOnlyList<QueuedAnalyticsEvent> batch)
            {
                Batches.Add(batch.ToList());
                return Available;
            }
        }

        public static AnalyticsEvent Ev(string name, int size = 0)
        {
            var p = new JObject { ["v"] = 1 };
            if (size > 0) p["pad"] = new string('x', size);
            return new AnalyticsEvent(name, p);
        }

        public static (AnalyticsQueue queue, RecordingTransport transport, SFixtures.RecordingFs fs)
            Queue(SFixtures.TempRoot root, RuntimeBounds bounds = null)
        {
            var fs = new SFixtures.RecordingFs();
            var transport = new RecordingTransport();
            var q = new AnalyticsQueue(root, fs, bounds ?? SFixtures.RepoBounds(), transport);
            q.LoadFromDisk();
            return (q, transport, fs);
        }

        // Small synthetic bounds for overflow tests (the shipped byte cap is a backstop the
        // shipped event cap makes unreachable — the drift-(d) inequality guarantees it).
        public static RuntimeBounds SmallQueueBounds(int maxEvents = 5, int maxBytes = 100000,
            int eventMaxBytes = 512, int highWater = 64)
        {
            var o = JObject.Parse(System.Text.Encoding.UTF8.GetString(SFixtures.RepoBoundsBytes()));
            o["QUEUE_MAX_EVENTS"] = maxEvents;
            o["QUEUE_MAX_BYTES"] = maxBytes;
            o["QUEUE_EVENT_MAX_BYTES"] = eventMaxBytes;
            o["QUEUE_FLUSH_HIGH_WATER"] = highWater;
            var r = RuntimeBounds.Parse(System.Text.Encoding.UTF8.GetBytes(o.ToString()));
            Assert.That(r.Ok, Is.True);
            return r.Value;
        }
    }
}
