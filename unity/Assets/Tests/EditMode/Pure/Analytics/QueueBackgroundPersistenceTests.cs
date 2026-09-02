using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using CatMetro.Application.Analytics;
using CatMetro.Tests.Save;

namespace CatMetro.Tests.Analytics
{
    public sealed class QueueBackgroundPersistenceTests
    {
        private sealed class ManualExecutor : IAnalyticsPersistenceExecutor
        {
            private readonly Queue<Action> _work = new Queue<Action>();
            public int DispatchCount;
            public int PendingCount => _work.Count;

            public void Dispatch(Action work)
            {
                DispatchCount++;
                _work.Enqueue(work);
            }

            public bool TryDrain(int budgetMilliseconds) => _work.Count == 0;

            public void PumpOne() => _work.Dequeue()();
        }

        private sealed class GuardedStorageRoot : CatMetro.Services.IStorageRoot
        {
            private readonly string _directory;
            public bool AllowAccess = true;

            public GuardedStorageRoot(string directory)
            {
                _directory = directory;
            }

            public string SaveDirectory => AllowAccess ? _directory
                : throw new InvalidOperationException("engine storage accessed from worker");
            public string CacheDirectory => SaveDirectory;
        }

        [Test]
        public void Log_WithBackgroundExecutor_PerformsNoFilesystemCallUntilWorkerRuns()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var executor = new ManualExecutor();
            var queue = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), null,
                null, null, executor);

            queue.Log(QFixtures.Ev("level_started"));

            Assert.That(fs.Calls, Is.Empty, "gameplay capture must not perform durable IO");
            Assert.That(executor.PendingCount, Is.EqualTo(1));
            executor.PumpOne();
            Assert.That(fs.Calls.Select(x => x.Split(':')[0]),
                Is.EqualTo(new[] { "WriteTempDurable", "Replace" }));
        }

        [Test]
        public void Worker_UsesPathsResolvedDuringMainThreadConstruction()
        {
            using var temp = new SFixtures.TempRoot();
            var root = new GuardedStorageRoot(temp.SaveDirectory);
            var executor = new ManualExecutor();
            var queue = new AnalyticsQueue(root, new SFixtures.RecordingFs(),
                SFixtures.RepoBounds(), null, null, null, executor);
            root.AllowAccess = false;
            queue.Log(QFixtures.Ev("a"));

            Assert.DoesNotThrow(executor.PumpOne,
                "the worker must not evaluate an engine-backed storage property");
        }

        [Test]
        public void Trigger_WaitsForTheBackgroundSnapshotBeforeStartingDelivery()
        {
            using var root = new SFixtures.TempRoot();
            var executor = new ManualExecutor();
            var transport = new QFixtures.RecordingTransport { AutoComplete = false };
            var queue = new AnalyticsQueue(root, new SFixtures.RecordingFs(),
                SFixtures.RepoBounds(), transport, null, null, executor);
            queue.Log(QFixtures.Ev("a"));

            queue.OnTrigger("network_reachable");

            Assert.That(transport.Batches, Is.Empty,
                "an in-memory event must not outrun its first durable snapshot");
            executor.PumpOne();
            queue.ContinuePendingDelivery();
            Assert.That(transport.Batches.Single().Select(x => x.Name),
                Is.EqualTo(new[] { "a" }));
        }

        [Test]
        public void AcknowledgedPrefix_IsPersistedBeforeTheNextBatchStarts()
        {
            using var root = new SFixtures.TempRoot();
            var executor = new ManualExecutor();
            var transport = new QFixtures.RecordingTransport
            {
                AutoComplete = false,
                MaxBatchSize = 2,
            };
            var queue = new AnalyticsQueue(root, new SFixtures.RecordingFs(),
                QFixtures.SmallQueueBounds(maxEvents: 100), transport, null, null, executor);
            queue.Log(QFixtures.Ev("a"));
            queue.Log(QFixtures.Ev("b"));
            queue.Log(QFixtures.Ev("c"));
            queue.OnTrigger("network_reachable");
            executor.PumpOne();
            queue.ContinuePendingDelivery();

            transport.Complete(0, accepted: true);

            Assert.That(transport.Batches.Count, Is.EqualTo(1),
                "the shorter queue is not durable yet");
            executor.PumpOne();
            queue.ContinuePendingDelivery();
            Assert.That(transport.Batches.Count, Is.EqualTo(2));
            Assert.That(transport.Batches[1].Select(x => x.Name), Is.EqualTo(new[] { "c" }));
        }

        [Test]
        public void Burst_CoalescesToOneLatestOrderedDiskArtifact()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var executor = new ManualExecutor();
            var queue = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), null,
                null, null, executor);

            queue.Log(QFixtures.Ev("a"));
            queue.Log(QFixtures.Ev("b"));
            queue.Log(QFixtures.Ev("c"));

            Assert.That(executor.DispatchCount, Is.EqualTo(1), "one writer is in flight");
            executor.PumpOne();
            Assert.That(fs.Calls.Count(x => x.StartsWith("WriteTempDurable:")), Is.EqualTo(1));
            var reloaded = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), null);
            Assert.That(reloaded.Snapshot().Select(x => x.Name),
                Is.EqualTo(new[] { "a", "b", "c" }));
        }

        [Test]
        public void WriteFailure_DoesNotEscapeLog_AndConfiguredTriggerRetriesLatestState()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs { FaultPoint = SFixtures.Fault.InWriteTemp };
            var executor = new ManualExecutor();
            var queue = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), null,
                null, null, executor);

            Assert.DoesNotThrow(() => queue.Log(QFixtures.Ev("a")));
            Assert.DoesNotThrow(executor.PumpOne);
            Assert.That(queue.Notes.Any(x => x.Name == "queue_dropped"
                && x.Detail.Contains("persist_failed")), Is.True);

            fs.FaultPoint = SFixtures.Fault.None;
            queue.OnTrigger("app_pause");
            Assert.That(executor.PendingCount, Is.EqualTo(1));
            executor.PumpOne();
            var reloaded = new AnalyticsQueue(root, fs, SFixtures.RepoBounds(), null);
            Assert.That(reloaded.Snapshot().Select(x => x.Name), Is.EqualTo(new[] { "a" }));
        }

        [Test]
        public void Drain_IsBoundedByTheExecutorAndReportsOutstandingWork()
        {
            using var root = new SFixtures.TempRoot();
            var executor = new ManualExecutor();
            var queue = new AnalyticsQueue(root, new SFixtures.RecordingFs(),
                SFixtures.RepoBounds(), null, null, null, executor);
            queue.Log(QFixtures.Ev("a"));

            Assert.That(queue.TryDrainPersistence(0), Is.False);
            executor.PumpOne();
            Assert.That(queue.TryDrainPersistence(0), Is.True);
        }
    }
}
