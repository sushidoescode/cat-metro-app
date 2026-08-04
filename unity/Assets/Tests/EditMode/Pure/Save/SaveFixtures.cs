using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CatMetro.Application.Save;
using CatMetro.Services;
using CatMetro.Tests.Domain;

namespace CatMetro.Tests.Save
{
    // CM-C7 test fixtures. Every test runs against a temp IStorageRoot (criterion 13: the store
    // cannot tell a test dir from a device path); the filesystem seam is either real or the
    // recording/faulting fake below. Tests may use file APIs; the shipped assembly reaches disk
    // only through the seam.
    public static class SFixtures
    {
        public sealed class TempRoot : IStorageRoot, IDisposable
        {
            public string SaveDirectory { get; }
            public string CacheDirectory { get; }

            public TempRoot()
            {
                SaveDirectory = Path.Combine(Path.GetTempPath(),
                    "cm-c7-" + Guid.NewGuid().ToString("N"));
                CacheDirectory = SaveDirectory;
                Directory.CreateDirectory(SaveDirectory);
            }

            public void Dispose()
            {
                try { Directory.Delete(SaveDirectory, recursive: true); } catch { }
            }
        }

        public enum Fault { None, InWriteTemp, InReplace }

        // Wraps the real seam: records every call in order, optionally detonates at a chosen
        // point (the two criterion-5 interruption points).
        public sealed class RecordingFs : ISaveFileSystem
        {
            private readonly RealSaveFileSystem _real = new RealSaveFileSystem();
            public readonly List<string> Calls = new List<string>();
            public Fault FaultPoint = Fault.None;

            public bool Exists(string path) => _real.Exists(path);
            public byte[] ReadAllBytes(string path) => _real.ReadAllBytes(path);
            public void Delete(string path) { Calls.Add("Delete:" + Path.GetFileName(path)); _real.Delete(path); }

            public void WriteTempDurable(string path, byte[] bytes)
            {
                if (FaultPoint == Fault.InWriteTemp)
                    throw new IOException("injected fault before the temp write (SI point A)");
                _real.WriteTempDurable(path, bytes);
                Calls.Add("WriteTempDurable:" + Path.GetFileName(path));
            }

            public void Replace(string tmpPath, string dstPath, string backupPath)
            {
                if (FaultPoint == Fault.InReplace)
                    throw new IOException("injected fault after the temp write, before Replace (SI point B)");
                _real.Replace(tmpPath, dstPath, backupPath);
                Calls.Add("Replace:" + Path.GetFileName(dstPath));
            }
        }

        public static byte[] RepoBoundsBytes() =>
            File.ReadAllBytes(Path.Combine(Fixtures.RepoRoot(), "config", "runtime_bounds.json"));

        public static RuntimeBounds RepoBounds()
        {
            var r = RuntimeBounds.Parse(RepoBoundsBytes());
            Assert.That(r.Ok, Is.True, $"shipped runtime_bounds.json must parse: {r.Error}");
            return r.Value;
        }

        // Synthetic bounds for cap tests (small dedupe/audit caps keep the tests fast); the
        // drift tests assert the SHIPPED file's rows separately.
        public static RuntimeBounds SmallBounds(int dedupeMax = 3, int auditMax = 2)
        {
            var o = JObject.Parse(Encoding.UTF8.GetString(RepoBoundsBytes()));
            o["LEDGER_DEDUPE_MAX_ENTRIES"] = dedupeMax;
            o["LEDGER_AUDIT_MAX_ENTRIES"] = auditMax;
            var r = RuntimeBounds.Parse(Encoding.UTF8.GetBytes(o.ToString()));
            Assert.That(r.Ok, Is.True);
            return r.Value;
        }

        public static SaveStore Store(IStorageRoot root, ISaveFileSystem fs = null,
            RuntimeBounds bounds = null, MigrationTable migrations = null) =>
            new SaveStore(root, fs ?? new RecordingFs(), bounds ?? RepoBounds(), migrations);

        // A store with recognisable state committed durably — the pre-interruption truth the SI
        // invariants compare against. COMMITS TWICE (review F10): the second commit takes the
        // File.Replace path, so a .bak exists and the SI battery runs against the realistic
        // main+bak disk state, not the first-commit File.Move special case.
        public static (SaveStore store, RecordingFs fs) CommittedStore(TempRoot root)
        {
            var fs = new RecordingFs();
            var store = Store(root, fs);
            store.Load();
            store.CommitAtomic(); // first commit: the A-C7-12 Move path
            store.State.Tickets = 42;
            store.State.RewindBalance = 7;
            ((JArray)store.State.Payload["ledger"]["dedupe"]).Add("a1b2c3d4a1b2c3d4a1b2c3d4a1b2c3d4");
            store.State.Payload["settings"]["equippedThemeId"] = "midnight";
            ((JArray)store.State.Payload["entitlements"]["active"]).Add("theme_pack");
            store.CommitAtomic(); // second commit: Replace — .bak now exists
            return (store, fs);
        }

        public static byte[] RawFile(string path) => File.ReadAllBytes(path);

        public static void WriteRaw(string path, byte[] bytes) => File.WriteAllBytes(path, bytes);

        // A structurally valid save file with an arbitrary header saveVersion (downgrade tests).
        public static byte[] FileWithVersion(ushort saveVersion, JObject payload = null)
        {
            var body = new UTF8Encoding(false).GetBytes(
                (payload ?? SaveDefaults.FreshPayload()).ToString(Newtonsoft.Json.Formatting.None));
            return SaveHeader.Write(SaveDefaults.MAGIC, SaveDefaults.FORMAT_VERSION, saveVersion, body);
        }
    }
}
