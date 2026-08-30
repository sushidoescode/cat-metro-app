using System;
using System.Collections.Generic;
using System.Threading;
using CatMetro.Integrations;
using CatMetro.Services.Ads;
using NUnit.Framework;

namespace CatMetro.Tests
{
    public sealed class LevelPlayPayloadMapperTests
    {
        [TestCase("BID", AdRevenuePrecision.Exact)]
        [TestCase(" bid ", AdRevenuePrecision.Exact)]
        [TestCase("RATE", AdRevenuePrecision.PublisherDefined)]
        [TestCase(" rate ", AdRevenuePrecision.PublisherDefined)]
        [TestCase("CPM", AdRevenuePrecision.Estimated)]
        [TestCase(" cPm ", AdRevenuePrecision.Estimated)]
        [TestCase(null, AdRevenuePrecision.Unknown)]
        [TestCase("", AdRevenuePrecision.Unknown)]
        [TestCase("   ", AdRevenuePrecision.Unknown)]
        [TestCase("auction", AdRevenuePrecision.Unknown)]
        public void Precision_MapsOnlyDocumentedLabels(string raw,
            AdRevenuePrecision expected)
        {
            Assert.That(LevelPlayPayloadMapper.MapPrecision(raw), Is.EqualTo(expected));
        }

        [Test]
        public void UsdMicros_UsesAwayFromZeroRoundingAndAcceptsTheLargestSafeDouble()
        {
            Assert.That(LevelPlayPayloadMapper.TryUsdMicros(0.001234d, out long ordinary),
                Is.True);
            Assert.That(ordinary, Is.EqualTo(1_234L));

            Assert.That(LevelPlayPayloadMapper.TryUsdMicros(0.0000005d, out long halfMicro),
                Is.True);
            Assert.That(halfMicro, Is.EqualTo(1L),
                "a positive half-micro must round away from zero, not to even");

            const double largestSafeUsd = 9_223_372_036_854.773d;
            Assert.That(LevelPlayPayloadMapper.TryUsdMicros(largestSafeUsd,
                out long largestSafeMicros), Is.True);
            Assert.That(largestSafeMicros, Is.EqualTo(9_223_372_036_854_773_760L));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-0.000001d)]
        [TestCase(9_223_372_036_854.775d)]
        public void UsdMicros_RejectsInvalidOrOverflowingValuesWithoutThrowing(double value)
        {
            bool accepted = true;
            long micros = 17L;

            Assert.DoesNotThrow(() => accepted =
                LevelPlayPayloadMapper.TryUsdMicros(value, out micros));
            Assert.That(accepted, Is.False);
            Assert.That(micros, Is.Zero);
        }

        [Test]
        public void LifecyclePayload_KeepsAdAuctionAndUnitIdentifiersDistinct()
        {
            var payload = LevelPlayPayloadMapper.CreateLifecycle(
                RewardedAdEventKind.Displayed,
                attemptId: 41L,
                placementId: "wardrobe_try_scarf",
                adUnitId: "rewarded-unit",
                adId: "creative-ad-17",
                auctionId: "auction-29",
                networkName: "test-network");

            Assert.That(payload.Kind, Is.EqualTo(RewardedAdEventKind.Displayed));
            Assert.That(payload.AttemptId, Is.EqualTo(41L));
            Assert.That(payload.PlacementId, Is.EqualTo("wardrobe_try_scarf"));
            Assert.That(payload.AdUnitId, Is.EqualTo("rewarded-unit"));
            Assert.That(payload.AdId, Is.EqualTo("creative-ad-17"));
            Assert.That(payload.AuctionId, Is.EqualTo("auction-29"));
            Assert.That(payload.NetworkName, Is.EqualTo("test-network"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void RevenuePayload_BlankAuctionIdFailsInsteadOfFallingBack(string auctionId)
        {
            bool accepted = LevelPlayPayloadMapper.TryCreateRevenue(
                attemptId: 73L,
                placementId: "placement-fallback-forbidden",
                adUnitId: "unit-fallback-forbidden",
                adId: "ad-fallback-forbidden",
                auctionId: auctionId,
                networkName: "network-fallback-forbidden",
                revenueUsd: 0.001234d,
                precision: "BID",
                out var payload);

            Assert.That(accepted, Is.False);
            Assert.That(payload, Is.EqualTo(default(RewardedAdEvent)));
        }

        [Test]
        public void RevenuePayload_CopiesNeutralScalarValuesBeforeSourceMutation()
        {
            var source = new MutableCallback
            {
                Placement = "wardrobe_try_goggles",
                AdUnit = "rewarded-unit-original",
                Ad = "creative-original",
                Auction = "auction-original",
                Network = "network-original",
                Revenue = 0.000042d,
                Precision = "RATE",
            };

            Assert.That(LevelPlayPayloadMapper.TryCreateRevenue(
                attemptId: 91L,
                placementId: source.Placement,
                adUnitId: source.AdUnit,
                adId: source.Ad,
                auctionId: source.Auction,
                networkName: source.Network,
                revenueUsd: source.Revenue,
                precision: source.Precision,
                out var payload), Is.True);

            source.Placement = "mutated-placement";
            source.AdUnit = "mutated-unit";
            source.Ad = "mutated-ad";
            source.Auction = "mutated-auction";
            source.Network = "mutated-network";
            source.Revenue = 999d;
            source.Precision = "CPM";

            Assert.That(payload.Kind, Is.EqualTo(RewardedAdEventKind.Revenue));
            Assert.That(payload.AttemptId, Is.EqualTo(91L));
            Assert.That(payload.PlacementId, Is.EqualTo("wardrobe_try_goggles"));
            Assert.That(payload.AdUnitId, Is.EqualTo("rewarded-unit-original"));
            Assert.That(payload.AdId, Is.EqualTo("creative-original"));
            Assert.That(payload.AuctionId, Is.EqualTo("auction-original"));
            Assert.That(payload.NetworkName, Is.EqualTo("network-original"));
            Assert.That(payload.RevenueMicros, Is.EqualTo(42L));
            Assert.That(payload.Currency, Is.EqualTo("USD"));
            Assert.That(payload.RevenuePrecision,
                Is.EqualTo(AdRevenuePrecision.PublisherDefined));
        }

        [Test]
        public void Queue_WorkerEnqueueDoesNotDeliverUntilExplicitDrainOnCallingThread()
        {
            using var queue = new MainThreadAdEventQueue();
            int testThread = Thread.CurrentThread.ManagedThreadId;
            int producerThread = 0;
            int consumerThread = 0;
            int deliveries = 0;
            var worker = new Thread(() =>
            {
                producerThread = Thread.CurrentThread.ManagedThreadId;
                queue.Enqueue(() =>
                {
                    consumerThread = Thread.CurrentThread.ManagedThreadId;
                    deliveries++;
                });
            });

            worker.Start();
            Assert.That(worker.Join(2_000), Is.True);
            Assert.That(producerThread, Is.Not.EqualTo(testThread));
            Assert.That(deliveries, Is.Zero);

            Assert.That(queue.Drain(), Is.EqualTo(1));
            Assert.That(deliveries, Is.EqualTo(1));
            Assert.That(consumerThread, Is.EqualTo(testThread));
            Assert.That(queue.Drain(), Is.Zero);
            Assert.That(deliveries, Is.EqualTo(1));
        }

        [Test]
        public void Queue_DrainsSnapshotInOrderOutsideLockAndRetainsReentrantWork()
        {
            using var queue = new MainThreadAdEventQueue();
            var observed = new List<int>();
            bool workerEnqueued = false;
            queue.Enqueue(() =>
            {
                observed.Add(1);
                var worker = new Thread(() => workerEnqueued = queue.Enqueue(() =>
                    observed.Add(4)));
                worker.Start();
                Assert.That(worker.Join(2_000), Is.True,
                    "consumer invocation must happen after releasing the queue lock");
                queue.Enqueue(() => observed.Add(3));
            });
            queue.Enqueue(() => observed.Add(2));

            Assert.That(queue.Drain(), Is.EqualTo(2));
            Assert.That(workerEnqueued, Is.True);
            Assert.That(observed, Is.EqualTo(new[] { 1, 2 }));

            Assert.That(queue.Drain(), Is.EqualTo(2));
            Assert.That(observed, Is.EqualTo(new[] { 1, 2, 4, 3 }));
            Assert.That(queue.Drain(), Is.Zero);
        }

        [Test]
        public void Queue_ThrowingConsumerCannotStrandOrDuplicateLaterSnapshotEntries()
        {
            using var queue = new MainThreadAdEventQueue();
            int first = 0;
            int later = 0;
            queue.Enqueue(() =>
            {
                first++;
                throw new InvalidOperationException("injected consumer fault");
            });
            queue.Enqueue(() => later++);

            Assert.DoesNotThrow(() => Assert.That(queue.Drain(), Is.EqualTo(2)));
            Assert.That(first, Is.EqualTo(1));
            Assert.That(later, Is.EqualTo(1));
            Assert.That(queue.Drain(), Is.Zero);
            Assert.That(first, Is.EqualTo(1));
            Assert.That(later, Is.EqualTo(1));
        }

        [Test]
        public void Queue_DisposeDropsPendingWorkAndRejectsLaterEnqueue()
        {
            var queue = new MainThreadAdEventQueue();
            int deliveries = 0;
            Assert.That(queue.Enqueue(() => deliveries++), Is.True);

            queue.Dispose();
            queue.Dispose();

            Assert.That(queue.Enqueue(() => deliveries++), Is.False);
            Assert.That(queue.Drain(), Is.Zero);
            Assert.That(deliveries, Is.Zero);
        }

        private sealed class MutableCallback
        {
            public string Placement;
            public string AdUnit;
            public string Ad;
            public string Auction;
            public string Network;
            public double Revenue;
            public string Precision;
        }
    }
}
