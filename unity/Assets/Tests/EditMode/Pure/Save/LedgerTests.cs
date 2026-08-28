using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Application.Save;

namespace CatMetro.Tests.Save
{
    // CM-C7 criteria 8 + 9 + 10: the domain-separated dedupe key, TryGrant's non-negotiable
    // order with one atomic write, and RK-20's refuse-never-trim cap.
    public sealed class LedgerTests
    {
        // --- criterion 8 ---

        // Pinned against an independent implementation (python hashlib, recorded in
        // state/handoffs/CM-C7.md): first 16 bytes of
        // SHA-256("cm-ledger-v1|rewind_pack_small|GPA.1234-5678-9012-34567"), lowercase hex.
        [Test]
        public void DedupeKey_PinnedVector() =>
            Assert.That(ConsumableLedger.DedupeKey("rewind_pack_small", "GPA.1234-5678-9012-34567"),
                Is.EqualTo("73cf1677ec4787a88391ca6dbf3ed6ab"));

        [Test]
        public void DedupeKey_ProductIdSeparatesTheRk19Collision()
        {
            var small = ConsumableLedger.DedupeKey("rewind_pack_small", "GPA.1234-5678-9012-34567");
            var large = ConsumableLedger.DedupeKey("rewind_pack_large", "GPA.1234-5678-9012-34567");
            Assert.That(small, Is.Not.EqualTo(large),
                "same transactionId, different productId => different keys (RK-19)");
            Assert.That(large, Is.EqualTo("8d077a4c78b899c9b97843a3d8c0cff8"),
                "python-pinned second vector");
        }

        [Test]
        public void DedupeKey_Is32LowercaseHex() =>
            Assert.That(Regex.IsMatch(
                ConsumableLedger.DedupeKey("p", "t"), "^[0-9a-f]{32}$"), Is.True);

        // --- criterion 9 ---

        [Test]
        public void DuplicateTransaction_GrantsZeroTheSecondTime()
        {
            using var root = new SFixtures.TempRoot();
            var (store, _) = SFixtures.CommittedStore(root);
            var ledger = new ConsumableLedger(store, SFixtures.RepoBounds());

            Assert.That(ledger.TryGrant("txn-1", "rewind_pack_small", 3, 1754300000000L),
                Is.EqualTo(3));
            Assert.That(ledger.TryGrant("txn-1", "rewind_pack_small", 3, 1754300000001L),
                Is.EqualTo(0), "the same store transaction can never grant twice");
            Assert.That(store.State.RewindBalance, Is.EqualTo(7 + 3));
        }

        [Test]
        public void Grant_LandsAllThreeMutationsInOneAtomicWrite()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs);
            store.Load();
            store.CommitAtomic();
            fs.Calls.Clear();

            var ledger = new ConsumableLedger(store, SFixtures.RepoBounds());
            ledger.TryGrant("txn-9", "rewind_pack_small", 2, 1754300000000L);

            Assert.That(fs.Calls, Is.EqualTo(new[]
            {
                "WriteTempDurable:save.dat.tmp",
                "Replace:save.dat",
            }), "exactly ONE atomic write carries dedupe + audit + balance");

            // and the durable bytes contain all three mutations
            var parsed = SaveHeader.TryParse(SFixtures.RawFile(store.SavePath),
                SaveDefaults.MAGIC, out var payloadBytes);
            Assert.That(parsed, Is.Not.Null);
            var payload = JObject.Parse(System.Text.Encoding.UTF8.GetString(payloadBytes));
            var key = ConsumableLedger.DedupeKey("rewind_pack_small", "txn-9");
            Assert.That(((JArray)payload["ledger"]["dedupe"]).Select(t => (string)t),
                Does.Contain(key));
            var audit = (JArray)payload["ledger"]["audit"];
            Assert.That(audit.Count, Is.EqualTo(1));
            Assert.That((string)audit[0]["txnHash"], Is.EqualTo(key));
            Assert.That((int)audit[0]["qty"], Is.EqualTo(2));
            Assert.That((int)payload["economy"]["rewindBalance"], Is.EqualTo(2));
        }

        // Review F1 (the round's blocking find): after a refused downgrade the profile is
        // read-only — a grant that "succeeds" with zero durability would let Play's redelivery
        // of the unconsumed purchase double-grant on every launch.
        [Test]
        public void TryGrant_AfterRefusedDowngrade_GrantsZeroAndWritesNothing()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs);
            var future = SFixtures.FileWithVersion(3);
            SFixtures.WriteRaw(store.SavePath, future);
            Assert.That(store.Load(), Is.EqualTo(CatMetro.Services.LoadResult.RefusedDowngrade));
            fs.Calls.Clear();

            var ledger = new ConsumableLedger(store, SFixtures.RepoBounds());
            int granted = ledger.TryGrant("txn-downgrade", "rewind_pack_small", 5, 0L);

            Assert.That(granted, Is.EqualTo(0), "a read-only profile can never grant");
            Assert.That(store.State.RewindBalance, Is.EqualTo(0), "no in-memory phantom balance");
            Assert.That(((JArray)store.State.Payload["ledger"]["dedupe"]).Count, Is.Zero);
            Assert.That(fs.Calls, Is.Empty, "nothing was written");
            Assert.That(SFixtures.RawFile(store.SavePath), Is.EqualTo(future),
                "the newer file's bytes stay untouched");
            Assert.That(store.ReportedEvents.Any(e => e.Detail.Contains("save_readonly")), Is.True);
        }

        [Test]
        public void FaultBeforeTheWriteLands_ProducesNeitherBalanceNorEvent()
        {
            using var root = new SFixtures.TempRoot();
            var (store, fs) = SFixtures.CommittedStore(root);
            var ledger = new ConsumableLedger(store, SFixtures.RepoBounds());
            int before = store.State.RewindBalance;
            var dedupeBefore = ((JArray)store.State.Payload["ledger"]["dedupe"])
                .Select(t => (string)t).ToArray();

            fs.FaultPoint = SFixtures.Fault.InReplace;
            Assert.Throws<IOException>(() =>
                ledger.TryGrant("txn-doomed", "rewind_pack_small", 5, 1754300000000L));

            Assert.That(store.State.RewindBalance, Is.EqualTo(before),
                "no balance change without a durable write");
            Assert.That(((JArray)store.State.Payload["ledger"]["dedupe"])
                .Select(t => (string)t).ToArray(), Is.EqualTo(dedupeBefore),
                "no dedupe change without a durable write");
        }

        // --- criterion 10 ---

        [Test]
        public void AtTheDedupeCap_TryGrantRefuses_NeverTrims()
        {
            using var root = new SFixtures.TempRoot();
            var (store, _) = SFixtures.CommittedStore(root);
            var bounds = SFixtures.SmallBounds(dedupeMax: 3, auditMax: 10);
            var ledger = new ConsumableLedger(store, bounds);

            ledger.TryGrant("txn-a", "p", 1, 0L);
            ledger.TryGrant("txn-b", "p", 1, 0L); // dedupe now holds 3 (1 seeded + 2)
            var setAtCap = ((JArray)store.State.Payload["ledger"]["dedupe"])
                .Select(t => (string)t).ToArray();
            Assert.That(setAtCap.Length, Is.EqualTo(3));

            int granted = ledger.TryGrant("txn-c", "p", 1, 0L);

            Assert.That(granted, Is.EqualTo(0), "RK-20: refuse at the cap");
            Assert.That(((JArray)store.State.Payload["ledger"]["dedupe"])
                .Select(t => (string)t).ToArray(), Is.EqualTo(setAtCap),
                "the dedupe set is NEVER trimmed");
            Assert.That(store.ReportedEvents.Any(e =>
                e.Name == "error_caught" && e.Detail.Contains("ledger_capacity")), Is.True);
        }

        [Test]
        public void AuditList_DropsOldestAtItsCap_DedupeDoesNot()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs, SFixtures.SmallBounds(dedupeMax: 100, auditMax: 2));
            store.Load();
            store.CommitAtomic();
            var ledger = new ConsumableLedger(store, SFixtures.SmallBounds(dedupeMax: 100, auditMax: 2));

            ledger.TryGrant("txn-1", "p", 1, 0L);
            ledger.TryGrant("txn-2", "p", 1, 0L);
            ledger.TryGrant("txn-3", "p", 1, 0L);

            var audit = (JArray)store.State.Payload["ledger"]["audit"];
            Assert.That(audit.Count, Is.EqualTo(2), "audit is FIFO-capped");
            Assert.That((string)audit[0]["txnHash"],
                Is.EqualTo(ConsumableLedger.DedupeKey("p", "txn-2")), "oldest dropped first");
            Assert.That(((JArray)store.State.Payload["ledger"]["dedupe"]).Count,
                Is.EqualTo(3), "all three keys stay deduped forever");
        }
    }
}
