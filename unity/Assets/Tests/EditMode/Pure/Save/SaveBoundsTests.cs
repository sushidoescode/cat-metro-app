using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Application.Save;
using CatMetro.Services;

namespace CatMetro.Tests.Save
{
    // CM-C7 criteria 11 + 12 + 13: the size ceiling and pause budget, the four
    // runtime_bounds.json drift tests (Q-T — values COPIED from ADR-0006 §4, never chosen),
    // and the seam shapes.
    public sealed class SaveBoundsTests
    {
        // --- criterion 11 ---

        [Test]
        public void OverCapPayload_IsRefusedBeforeWriting()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs);
            store.Load();
            // a synthetic payload comfortably past SAVE_MAX_BYTES (524288)
            store.State.Payload["futurePadding"] = new string('x', 600000);
            fs.Calls.Clear();

            // Review F4: refusals are RECORDED, never thrown — the pinned void signature throws
            // only on unrecoverable IO, and the pause path must never crash the lifecycle caller.
            Assert.DoesNotThrow(store.CommitAtomic);
            Assert.That(fs.Calls, Is.Empty, "refused BEFORE any filesystem call");
            Assert.That(store.ReportedEvents.Any(e => e.Detail.Contains("save_over_cap")), Is.True);
            Assert.That(store.TryCommitAtomic(), Is.False, "the success channel reads false");
            Assert.That(store.TryCommitWithin(50), Is.False, "the pause path refuses too");
            Assert.That(fs.Calls, Is.Empty);
        }

        // Review F7 / A-C7-13: a payload deeper than the load ceiling must refuse LOUDLY at
        // commit — the alternative is a file the very next boot silently rejects as corrupt,
        // falling Fresh and losing the save.
        [Test]
        public void DeepPayload_IsRefusedAtCommit_NotLostAtNextBoot()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs);
            store.Load();
            store.State.Tickets = 42;
            store.CommitAtomic();

            var deep = new JObject();
            var at = deep;
            for (int i = 0; i < 20; i++) { var next = new JObject(); at["d"] = next; at = next; }
            store.State.Payload["futureExperiment"] = deep;
            fs.Calls.Clear();

            Assert.That(store.TryCommitAtomic(), Is.False);
            Assert.That(fs.Calls, Is.Empty, "refused before any filesystem call");
            Assert.That(store.ReportedEvents.Any(e => e.Detail.Contains("save_depth")), Is.True);

            var reload = SFixtures.Store(root);
            Assert.That(reload.Load(), Is.EqualTo(LoadResult.Ok), "the durable save is intact");
            Assert.That(reload.State.Tickets, Is.EqualTo(42));
        }

        [Test]
        public void TryCommitWithinZero_WritesNothingAndReturnsFalse()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs);
            store.Load();
            fs.Calls.Clear();

            Assert.That(store.TryCommitWithin(0), Is.False);
            Assert.That(fs.Calls, Is.Empty);
        }

        [Test]
        public void LastCommittedBytes_IsExposed_AndUnderTheCeiling()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            store.CommitAtomic();
            Assert.That(store.LastCommittedBytes, Is.GreaterThan(SaveHeader.SIZE));
            Assert.That(store.LastCommittedBytes,
                Is.LessThanOrEqualTo(SFixtures.RepoBounds().SaveMaxBytes));
        }

        [Test]
        public void PauseBudgetDefault_IsTheConfigRow()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            Assert.That(store.PauseBudgetMs, Is.EqualTo(SFixtures.RepoBounds().SavePauseBudgetMs));
            store.Load();
            Assert.That(store.TryCommitOnPause(), Is.True, "the default budget commits");
        }

        // --- criterion 12: the four drift tests ---

        // (a) exactly the 15 keys of ADR-0006 §4, no more, no fewer.
        [Test]
        public void DriftA_FileKeySet_IsExactlyThe15()
        {
            Assert.That(SFixtures.RepoBounds().Keys, Is.EquivalentTo(new[]
            {
                "schemaVersion", "SAVE_MAX_BYTES", "SAVE_PAUSE_BUDGET_MS",
                "LEDGER_DEDUPE_MAX_ENTRIES", "LEDGER_AUDIT_MAX_ENTRIES", "LEDGER_KEY_SCHEME",
                "QUEUE_MAX_EVENTS", "QUEUE_MAX_BYTES", "QUEUE_EVENT_MAX_BYTES",
                "QUEUE_FLUSH_HIGH_WATER", "QUEUE_FLUSH_TRIGGER", "ATTRIBUTION_MAX_RESIMS",
                "CONTENT_MAX_FILE_BYTES", "CONTENT_MAX_JSON_DEPTH", "CONTENT_BOUNDS_PROFILE",
            }));
        }

        // (b) every constant this contract uses equals the file's row — the ADR-0006 §4 values,
        // asserted verbatim so no literal can drift from the file.
        [Test]
        public void DriftB_RowsCarryTheAdrValues()
        {
            var b = SFixtures.RepoBounds();
            Assert.That(b.SaveMaxBytes, Is.EqualTo(524288));
            Assert.That(b.SavePauseBudgetMs, Is.EqualTo(50));
            Assert.That(b.LedgerDedupeMaxEntries, Is.EqualTo(5000));
            Assert.That(b.LedgerAuditMaxEntries, Is.EqualTo(200));
            Assert.That(b.LedgerKeyScheme, Is.EqualTo("cm-ledger-v1"));
            Assert.That(b.LedgerKeyScheme, Is.EqualTo(ConsumableLedger.KEY_SCHEME),
                "the ledger's compiled scheme equals the file's row");
            Assert.That(b.QueueMaxEvents, Is.EqualTo(2000));
            Assert.That(b.QueueMaxBytes, Is.EqualTo(1048576));
            Assert.That(b.QueueEventMaxBytes, Is.EqualTo(512));
            Assert.That(b.QueueFlushHighWater, Is.EqualTo(64));
            Assert.That(b.QueueFlushTrigger, Is.EqualTo(new[]
                { "network_reachable", "app_foreground", "app_pause", "high_water" }));
            Assert.That(b.AttributionMaxResims, Is.EqualTo(24));
            // Review F11: the two content rows verbatim too — (c) alone was only RELATIVE
            // coupling, so file+ContentBounds could drift together unnoticed by this suite.
            Assert.That(b.ContentMaxFileBytes, Is.EqualTo(262144));
            Assert.That(b.ContentMaxJsonDepth, Is.EqualTo(16));
            Assert.That(b.ContentBoundsProfile, Is.EqualTo("level-schema-v2"));
        }

        // (c) ContentBounds' compiled constants equal the file's rows (CM-C2a criterion 5).
        [Test]
        public void DriftC_ContentBoundsRowsMatch()
        {
            var b = SFixtures.RepoBounds();
            Assert.That(CatMetro.Content.ContentBounds.CONTENT_MAX_FILE_BYTES,
                Is.EqualTo(b.ContentMaxFileBytes));
            Assert.That(CatMetro.Content.ContentBounds.CONTENT_MAX_JSON_DEPTH,
                Is.EqualTo(b.ContentMaxJsonDepth));
        }

        // (d) the ADR-0006:228-238 inequality: a full queue of max-size events must fit.
        [Test]
        public void DriftD_QueueByteInequalityHolds()
        {
            var b = SFixtures.RepoBounds();
            Assert.That((long)b.QueueMaxBytes,
                Is.GreaterThanOrEqualTo((long)b.QueueMaxEvents * b.QueueEventMaxBytes));
        }

        // --- criterion 13 (shape half; the greps live in tests/save/save.test.sh) ---

        [Test]
        public void IStorageRoot_HasExactlyTheTwoProperties()
        {
            var props = typeof(IStorageRoot).GetProperties();
            Assert.That(props.Select(p => p.Name),
                Is.EquivalentTo(new[] { "SaveDirectory", "CacheDirectory" }));
            foreach (var p in props)
            {
                Assert.That(p.PropertyType, Is.EqualTo(typeof(string)));
                Assert.That(p.CanWrite, Is.False, "get-only (overview.md:235-238)");
            }
            Assert.That(typeof(IStorageRoot).GetMethods().Count(m => !m.IsSpecialName),
                Is.Zero, "two properties and NOTHING else");
        }

        [Test]
        public void WholeStoreRuns_AgainstATempStorageRoot()
        {
            // criterion 13's smoke: a full load-mutate-commit-reload cycle against a plain temp
            // directory — no engine, no conditional compilation, no path assumptions.
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            Assert.That(store.Load(), Is.EqualTo(LoadResult.Fresh));
            store.State.Tickets = 11;
            store.CommitAtomic();
            var again = SFixtures.Store(root);
            Assert.That(again.Load(), Is.EqualTo(LoadResult.Ok));
            Assert.That(again.State.Tickets, Is.EqualTo(11));
        }
    }
}
