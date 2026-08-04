using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Application.Save;
using CatMetro.Services;

namespace CatMetro.Tests.Save
{
    // CM-C7 criteria 4 + 5 + 6: the three-call atomic write, the SI-1..SI-7 kill-during-write
    // battery at both interruption points, and the never-throwing load fallback chain.
    public sealed class SaveAtomicityTests
    {
        // --- criterion 4 ---

        [Test]
        public void SecondCommit_LeavesPreviousContentsInBak()
        {
            using var root = new SFixtures.TempRoot();
            var store = SFixtures.Store(root);
            store.Load();
            store.State.Tickets = 1;
            store.CommitAtomic();
            store.State.Tickets = 2;
            store.CommitAtomic();

            var bak = SaveHeader.TryParse(SFixtures.RawFile(store.BakPath), SaveDefaults.MAGIC,
                out var bakPayload);
            Assert.That(bak, Is.Not.Null, ".bak must be a complete valid save");
            var bakState = JObject.Parse(System.Text.Encoding.UTF8.GetString(bakPayload));
            Assert.That((int)bakState["economy"]["tickets"], Is.EqualTo(1),
                ".bak holds the PREVIOUS version");
            Assert.That(File.Exists(store.TmpPath), Is.False, "no .tmp after a successful commit");
        }

        [Test]
        public void CommitSequence_IsWriteTempThenReplace_ViaTheSeam()
        {
            using var root = new SFixtures.TempRoot();
            var fs = new SFixtures.RecordingFs();
            var store = SFixtures.Store(root, fs);
            store.Load();
            fs.Calls.Clear();
            store.CommitAtomic();

            Assert.That(fs.Calls, Is.EqualTo(new[]
            {
                "WriteTempDurable:save.dat.tmp",
                "Replace:save.dat",
            }), "exactly the ADR-0006:48-51 sequence, nothing else, never in place");
        }

        // --- criterion 5: SI-1..SI-7, x2 interruption points ---
        // Point A: fault before the temp write lands. Point B: temp written, File.Replace not
        // reached. Both must leave the COMPLETE previous version loadable.

        private static (SaveStore reloaded, JObject preInterruption, SFixtures.TempRoot root)
            InterruptedCommit(SFixtures.Fault point)
        {
            var root = new SFixtures.TempRoot();
            var (store, fs) = SFixtures.CommittedStore(root);
            var pre = (JObject)store.State.Payload.DeepClone();

            // mutate + attempt the doomed commit
            store.State.Tickets = 9999;
            ((JArray)store.State.Payload["ledger"]["dedupe"]).Add("ffffffffffffffffffffffffffffffff");
            fs.FaultPoint = point;
            Assert.Throws<IOException>(store.CommitAtomic);

            var reloaded = SFixtures.Store(root);
            return (reloaded, pre, root);
        }

        [TestCase(SFixtures.Fault.InWriteTemp)]
        [TestCase(SFixtures.Fault.InReplace)]
        public void SI1_LoadedFileParsesUnderTheShippedSchema(SFixtures.Fault point)
        {
            var (reloaded, _, root) = InterruptedCommit(point);
            using (root)
                Assert.That(reloaded.Load(), Is.EqualTo(LoadResult.Ok),
                    "the complete previous version must load cleanly");
        }

        [TestCase(SFixtures.Fault.InWriteTemp)]
        [TestCase(SFixtures.Fault.InReplace)]
        public void SI2_SaveVersionEqualsTheBuilds(SFixtures.Fault point)
        {
            var (reloaded, _, root) = InterruptedCommit(point);
            using (root)
            {
                reloaded.Load();
                Assert.That((int)reloaded.State.Payload["saveVersion"],
                    Is.EqualTo((int)SaveDefaults.SAVE_VERSION));
            }
        }

        [TestCase(SFixtures.Fault.InWriteTemp)]
        [TestCase(SFixtures.Fault.InReplace)]
        public void SI3_TicketBalanceEqualsPreInterruptionValue(SFixtures.Fault point)
        {
            var (reloaded, pre, root) = InterruptedCommit(point);
            using (root)
            {
                reloaded.Load();
                Assert.That(reloaded.State.Tickets, Is.EqualTo((int)pre["economy"]["tickets"]));
            }
        }

        [TestCase(SFixtures.Fault.InWriteTemp)]
        [TestCase(SFixtures.Fault.InReplace)]
        public void SI4_RewindBalanceEqualsPreInterruptionValue(SFixtures.Fault point)
        {
            var (reloaded, pre, root) = InterruptedCommit(point);
            using (root)
            {
                reloaded.Load();
                Assert.That(reloaded.State.RewindBalance,
                    Is.EqualTo((int)pre["economy"]["rewindBalance"]));
            }
        }

        [TestCase(SFixtures.Fault.InWriteTemp)]
        [TestCase(SFixtures.Fault.InReplace)]
        public void SI5_DedupeSetEqualsPreInterruptionSetExactly(SFixtures.Fault point)
        {
            var (reloaded, pre, root) = InterruptedCommit(point);
            using (root)
            {
                reloaded.Load();
                var loaded = ((JArray)reloaded.State.Payload["ledger"]["dedupe"])
                    .Select(t => (string)t).ToArray();
                var expected = ((JArray)pre["ledger"]["dedupe"]).Select(t => (string)t).ToArray();
                Assert.That(loaded, Is.EqualTo(expected), "no additions, no losses");
            }
        }

        [TestCase(SFixtures.Fault.InWriteTemp)]
        [TestCase(SFixtures.Fault.InReplace)]
        public void SI6_NoTempOrPartialFileRemains(SFixtures.Fault point)
        {
            var (reloaded, _, root) = InterruptedCommit(point);
            using (root)
            {
                reloaded.Load(); // boot deletes a stale .tmp
                Assert.That(File.Exists(reloaded.TmpPath), Is.False);
            }
        }

        [TestCase(SFixtures.Fault.InWriteTemp)]
        [TestCase(SFixtures.Fault.InReplace)]
        public void SI7_ThemeAndEntitlementsUnchanged(SFixtures.Fault point)
        {
            var (reloaded, pre, root) = InterruptedCommit(point);
            using (root)
            {
                reloaded.Load();
                Assert.That((string)reloaded.State.Payload["settings"]["equippedThemeId"],
                    Is.EqualTo((string)pre["settings"]["equippedThemeId"]));
                Assert.That(JToken.DeepEquals(reloaded.State.Payload["entitlements"],
                    pre["entitlements"]), Is.True);
            }
        }

        [Test]
        public void CompletedReplace_LoadsTheCompleteNewVersion()
        {
            using var root = new SFixtures.TempRoot();
            var (store, _) = SFixtures.CommittedStore(root);
            store.State.Tickets = 777;
            store.CommitAtomic();

            var reloaded = SFixtures.Store(root);
            Assert.That(reloaded.Load(), Is.EqualTo(LoadResult.Ok));
            Assert.That(reloaded.State.Tickets, Is.EqualTo(777));
        }

        // --- criterion 6: the fallback chain never throws ---

        private static SaveStore CorruptMainThen(SFixtures.TempRoot root,
            System.Action<string> corrupt)
        {
            var (store, _) = SFixtures.CommittedStore(root);
            store.State.Tickets = 2;
            store.CommitAtomic(); // main = v(tickets 2), bak = v(tickets 42)
            corrupt(store.SavePath);
            return SFixtures.Store(root);
        }

        [Test]
        public void BadMagic_FallsBackToBak()
        {
            using var root = new SFixtures.TempRoot();
            var reloaded = CorruptMainThen(root, path =>
            {
                var b = SFixtures.RawFile(path); b[0] = (byte)'X'; SFixtures.WriteRaw(path, b);
            });
            LoadResult result = default;
            Assert.DoesNotThrow(() => result = reloaded.Load());
            Assert.That(result, Is.EqualTo(LoadResult.RecoveredFromBackup));
            Assert.That(reloaded.State.Tickets, Is.EqualTo(42), "the previous good file");
        }

        [Test]
        public void BadLength_FallsBackToBak()
        {
            using var root = new SFixtures.TempRoot();
            var reloaded = CorruptMainThen(root, path =>
            {
                var b = SFixtures.RawFile(path); b[8] ^= 0xFF; SFixtures.WriteRaw(path, b);
            });
            LoadResult result = default;
            Assert.DoesNotThrow(() => result = reloaded.Load());
            Assert.That(result, Is.EqualTo(LoadResult.RecoveredFromBackup));
        }

        [Test]
        public void BadCrc_FallsBackToBak()
        {
            using var root = new SFixtures.TempRoot();
            var reloaded = CorruptMainThen(root, path =>
            {
                var b = SFixtures.RawFile(path); b[b.Length - 1] ^= 0xFF; SFixtures.WriteRaw(path, b);
            });
            LoadResult result = default;
            Assert.DoesNotThrow(() => result = reloaded.Load());
            Assert.That(result, Is.EqualTo(LoadResult.RecoveredFromBackup));
        }

        [Test]
        public void BothFilesBad_StartsFresh_AndReportsSaveCorrupt()
        {
            using var root = new SFixtures.TempRoot();
            var reloaded = CorruptMainThen(root, path =>
            {
                var b = SFixtures.RawFile(path); b[0] = (byte)'X'; SFixtures.WriteRaw(path, b);
                var bak = path + ".bak";
                var b2 = SFixtures.RawFile(bak); b2[0] = (byte)'X'; SFixtures.WriteRaw(bak, b2);
            });
            LoadResult result = default;
            Assert.DoesNotThrow(() => result = reloaded.Load());
            Assert.That(result, Is.EqualTo(LoadResult.Fresh));
            Assert.That(reloaded.ReportedEvents.Any(e =>
                e.Name == "error_caught" && e.Detail.Contains("save_corrupt")), Is.True);
        }

        // Review F9: a structurally-valid JSON object missing the v1 spine (a future migration
        // bug's likeliest output) must fall down the chain, never load Ok and detonate later.
        [Test]
        public void MangledShape_FallsBackInsteadOfLoadingOk()
        {
            using var root = new SFixtures.TempRoot();
            var (store, _) = SFixtures.CommittedStore(root);
            var mangled = SaveDefaults.FreshPayload();
            mangled.Remove("ledger");
            SFixtures.WriteRaw(store.SavePath, SFixtures.FileWithVersion(1, mangled));

            var reloaded = SFixtures.Store(root);
            LoadResult result = default;
            Assert.DoesNotThrow(() => result = reloaded.Load());
            Assert.That(result, Is.Not.EqualTo(LoadResult.Ok),
                "a spine-less payload is corrupt, not adoptable");
            Assert.That(reloaded.State.Payload["ledger"], Is.Not.Null,
                "whatever loaded carries a real ledger object");
        }

        [Test]
        public void StaleTmp_IsDeletedOnBoot()
        {
            using var root = new SFixtures.TempRoot();
            var (store, _) = SFixtures.CommittedStore(root);
            File.WriteAllText(store.TmpPath, "half-written garbage");

            var reloaded = SFixtures.Store(root);
            Assert.DoesNotThrow(() => reloaded.Load());
            Assert.That(File.Exists(store.TmpPath), Is.False, "SI-6: stale .tmp deleted on boot");
        }
    }
}
