using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using CatMetro.Application.Analytics;
using CatMetro.Tests.Save;

namespace CatMetro.Tests.Analytics
{
    public sealed class AnalyticsInstallIdentityTests
    {
        private sealed class ManualExecutor : IAnalyticsPersistenceExecutor
        {
            public readonly Queue<Action> Pending = new Queue<Action>();
            public int DispatchCount;
            public void Dispatch(Action work) { DispatchCount++; Pending.Enqueue(work); }
            public bool TryDrain(int budgetMilliseconds) => Pending.Count == 0;
            public void PumpOne() => Pending.Dequeue()();
        }

        [Test]
        public void Identifier_IsCommittedOnceAndPersistsAcrossOrdinarySessions()
        {
            using var root = new SFixtures.TempRoot();
            var first = SFixtures.Store(root);
            first.Load();
            int generations = 0;

            Assert.That(AnalyticsInstallIdentity.TryGetOrCreate(
                new SaveBackedAnalyticsProfileStore(first), () =>
            {
                generations++;
                return "00112233445566778899aabbccddeeff";
            }, out string created), Is.True);
            Assert.That(created, Is.EqualTo("00112233445566778899aabbccddeeff"));

            var reloaded = SFixtures.Store(root);
            reloaded.Load();
            Assert.That(AnalyticsInstallIdentity.TryGetOrCreate(
                new SaveBackedAnalyticsProfileStore(reloaded), () =>
            {
                generations++;
                return "ffeeddccbbaa99887766554433221100";
            }, out string same), Is.True);

            Assert.That(same, Is.EqualTo(created));
            Assert.That(generations, Is.EqualTo(1));
        }

        [Test]
        public void InitialCommitFailure_DisablesIdentityRatherThanUsingAnEphemeralId()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs { FaultPoint = SFixtures.Fault.InWriteTemp };
            var save = SFixtures.Store(root, fs);
            save.Load();

            Assert.That(AnalyticsInstallIdentity.TryGetOrCreate(
                new SaveBackedAnalyticsProfileStore(save),
                () => "00112233445566778899aabbccddeeff", out string id), Is.False);
            Assert.That(id, Is.Null);
            Assert.That(save.State.Payload["profile"][AnalyticsInstallIdentity.ProfileKey],
                Is.Null, "failed persistence must not leave an in-memory-only release id");
        }

        [Test]
        public void RoutineProfileCommits_CoalesceAndDoNotWriteUntilTheWorkerRuns()
        {
            var executor = new ManualExecutor();
            var written = new List<JObject>();
            var store = new BufferedAnalyticsProfileStore(new JObject
            {
                ["createdAtUtc"] = 0L,
                ["lastSeenAtUtc"] = 0L,
                ["sessionCount"] = 0,
            }, snapshot =>
            {
                written.Add((JObject)snapshot.DeepClone());
                return true;
            }, executor);

            store.Profile["sessionCount"] = 1;
            store.RequestCommit();
            store.Profile["sessionCount"] = 2;
            store.RequestCommit();

            Assert.That(written, Is.Empty, "routine session bookkeeping must not write inline");
            Assert.That(executor.DispatchCount, Is.EqualTo(1));
            executor.PumpOne();
            Assert.That((int)written.Single()["sessionCount"], Is.EqualTo(2));
        }

        [Test]
        public void FirstIdentityCommit_IsDurableBeforeItCanBeUsed()
        {
            var executor = new ManualExecutor();
            int durableWrites = 0;
            var store = new BufferedAnalyticsProfileStore(new JObject(), _ =>
            {
                durableWrites++;
                return false;
            }, executor);

            Assert.That(AnalyticsInstallIdentity.TryGetOrCreate(store,
                () => "00112233445566778899aabbccddeeff", out string id), Is.False);

            Assert.That(id, Is.Null);
            Assert.That(durableWrites, Is.EqualTo(1), "identity creation is an immediate write");
            Assert.That(executor.Pending, Is.Empty);
            Assert.That(store.Profile[AnalyticsInstallIdentity.ProfileKey], Is.Null,
                "a failed durable write rolls the candidate back");
        }

        [Test]
        public void LaunchPreparation_CommitsIdentityAndFirstOpenTimeInOneDurableSnapshot()
        {
            var executor = new ManualExecutor();
            var written = new List<JObject>();
            var store = new BufferedAnalyticsProfileStore(new JObject
            {
                ["createdAtUtc"] = 0L,
            }, snapshot =>
            {
                written.Add((JObject)snapshot.DeepClone());
                return true;
            }, executor);

            Assert.That(AnalyticsInstallIdentity.TryPrepareLaunch(store,
                () => "00112233445566778899aabbccddeeff", 1_800_000_000L,
                out string id, out bool firstOpen), Is.True);

            Assert.That(firstOpen, Is.True);
            Assert.That(id, Is.EqualTo("00112233445566778899aabbccddeeff"));
            Assert.That(written.Count, Is.EqualTo(1),
                "identifier and createdAtUtc must land in the same pre-network commit");
            Assert.That((long)written[0]["createdAtUtc"], Is.EqualTo(1_800_000_000L));
            Assert.That((string)written[0][AnalyticsInstallIdentity.ProfileKey], Is.EqualTo(id));
        }

        [Test]
        public void LaunchPreparationFailure_RollsBackBothIdentityAndFirstOpenTime()
        {
            var store = new BufferedAnalyticsProfileStore(new JObject
            {
                ["createdAtUtc"] = 0L,
            }, _ => false, new ManualExecutor());

            Assert.That(AnalyticsInstallIdentity.TryPrepareLaunch(store,
                () => "00112233445566778899aabbccddeeff", 1_800_000_000L,
                out string id, out bool firstOpen), Is.False);

            Assert.That(id, Is.Null);
            Assert.That(firstOpen, Is.False);
            Assert.That((long)store.Profile["createdAtUtc"], Is.Zero);
            Assert.That(store.Profile[AnalyticsInstallIdentity.ProfileKey], Is.Null);
        }

        [TestCase("")]
        [TestCase("ABCDEF00112233445566778899AABBCC")]
        [TestCase("001122")]
        public void IdentifierFormat_IsExactlyLowercase32Hex(string value) =>
            Assert.That(AnalyticsInstallIdentity.IsValid(value), Is.False);
    }
}
