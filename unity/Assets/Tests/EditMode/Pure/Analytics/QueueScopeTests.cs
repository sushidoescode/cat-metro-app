using System.Linq;
using NUnit.Framework;
using CatMetro.Application.Analytics;
using CatMetro.Services;

namespace CatMetro.Tests.Analytics
{
    // CM-C8 criteria 7 + 10: metrics-only STATICALLY (the enqueue surface accepts only the
    // analytics event type) and the Services interface has exactly the pinned shape. The grep
    // halves live in tests/analytics/queue.test.sh.
    public sealed class QueueScopeTests
    {
        [Test]
        public void EnqueueSurface_AcceptsOnlyTheAnalyticsEventType()
        {
            var logMethods = typeof(AnalyticsQueue).GetMethods()
                .Where(m => m.Name == "Log").ToList();
            Assert.That(logMethods.Count, Is.EqualTo(1), "one enqueue entry point");
            var p = logMethods[0].GetParameters();
            Assert.That(p.Length, Is.EqualTo(1));
            Assert.That(p[0].ParameterType.GetElementType() ?? p[0].ParameterType,
                Is.EqualTo(typeof(AnalyticsEvent)),
                "no ledger, entitlement or cap type can ride the queue (CM-R43.4(d))");
        }

        [Test]
        public void IAnalytics_HasExactlyThePinnedShape()
        {
            var t = typeof(IAnalytics);
            var methods = t.GetMethods().Where(m => !m.IsSpecialName).Select(m => m.Name).ToArray();
            Assert.That(methods, Is.EquivalentTo(new[] { "Log", "SetUserProperty" }));
            var log = t.GetMethod("Log");
            Assert.That(log.GetParameters()[0].ParameterType.IsByRef, Is.True,
                "in-parameter per overview.md:245-249");
            var sup = t.GetMethod("SetUserProperty");
            Assert.That(sup.GetParameters().Select(p => p.ParameterType).ToArray(),
                Is.EqualTo(new[] { typeof(UserPropertyKey), typeof(string) }));
            var count = t.GetProperty("QueuedEventCount");
            Assert.That(count, Is.Not.Null);
            Assert.That(count.PropertyType, Is.EqualTo(typeof(int)));
            Assert.That(count.CanWrite, Is.False);
        }

        [Test]
        public void QueueImplements_IAnalytics() =>
            Assert.That(typeof(IAnalytics).IsAssignableFrom(typeof(AnalyticsQueue)), Is.True);
    }
}
