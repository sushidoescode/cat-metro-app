using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using CatMetro.Services;

namespace CatMetro.Application.Save
{
    // One reported event (save_migrated / error_caught rows). CM-C8's typed analytics wrapper is
    // a later contract — until it lands, the store records what it WOULD emit and tests assert
    // the record; nothing is silently dropped.
    public sealed class SaveEventRecord
    {
        public readonly string Name;
        public readonly string Detail;
        public SaveEventRecord(string name, string detail) { Name = name; Detail = detail ?? ""; }
    }

    // CM-C7's ISave implementation (ADR-0003: declared in Services, implemented here, rooted by
    // an IStorageRoot the caller supplies — a temp dir in tests, the engine's persistent data
    // path from Bootstrap one day). Engine-free; durability lives behind the ISaveFileSystem seam.
    public sealed class SaveStore : ISave
    {
        private readonly IStorageRoot _root;
        private readonly ISaveFileSystem _fs;
        private readonly RuntimeBounds _bounds;
        private readonly MigrationTable _migrations;
        private readonly List<SaveEventRecord> _events = new List<SaveEventRecord>();

        public SaveStore(IStorageRoot root, ISaveFileSystem fs, RuntimeBounds bounds,
            MigrationTable migrations)
        {
            if (root == null || fs == null || bounds == null)
                throw new System.ArgumentException("root, fs and bounds are required");
            _root = root; _fs = fs; _bounds = bounds;
            _migrations = migrations ?? new MigrationTable();
            State = new SaveState(SaveDefaults.FreshPayload());
        }

        public SaveState State { get; private set; }
        public int LastCommittedBytes { get; private set; }
        public bool ReadOnlyMode { get; private set; } // downgrade refusal (criterion 7)
        public IReadOnlyList<SaveEventRecord> ReportedEvents => _events;
        public int PauseBudgetMs => _bounds.SavePauseBudgetMs;

        public string SavePath => Path.Combine(_root.SaveDirectory, "save.dat");
        public string TmpPath => SavePath + ".tmp";
        public string BakPath => SavePath + ".bak";

        public void Report(string name, string detail) =>
            _events.Add(new SaveEventRecord(name, detail));

        // Criterion 6: the fallback chain never throws — bad magic/length/CRC on save.dat falls
        // back to save.dat.bak; both bad starts fresh with error_caught(domain=save_corrupt); a
        // stale .tmp is deleted first (SI-6).
        public LoadResult Load()
        {
            try
            {
                if (_fs.Exists(TmpPath)) _fs.Delete(TmpPath);

                bool mainExists = _fs.Exists(SavePath);
                bool bakExists = _fs.Exists(BakPath);
                if (!mainExists && !bakExists) return Fresh(reportCorrupt: false);

                var main = TryRead(SavePath);
                if (main != null) return Adopt(main, LoadResult.Ok);

                var bak = TryRead(BakPath);
                if (bak != null) return Adopt(bak, LoadResult.RecoveredFromBackup);

                return Fresh(reportCorrupt: true);
            }
            catch (System.Exception ex)
            {
                // Absolute backstop: the boot path never throws (ADR-0006; RK-34).
                Report("error_caught", "domain=save_corrupt detail=" + ex.GetType().Name);
                State = new SaveState(SaveDefaults.FreshPayload());
                return LoadResult.Fresh;
            }
        }

        private sealed class ReadFile
        {
            public SaveHeader Header;
            public JObject Payload;
        }

        // Total: null on any structural failure. Header magic/length/CRC are checked BEFORE the
        // payload reaches the JSON parser (the RK-34 posture).
        private ReadFile TryRead(string path)
        {
            try
            {
                if (!_fs.Exists(path)) return null;
                var header = SaveHeader.TryParse(_fs.ReadAllBytes(path), SaveDefaults.MAGIC,
                    out var payloadBytes);
                if (header == null) return null;
                if (payloadBytes.Length >= 3 && payloadBytes[0] == 0xEF && payloadBytes[1] == 0xBB
                    && payloadBytes[2] == 0xBF)
                    return null; // BOM-free is part of the byte contract (criterion 1)
                var token = CatMetro.Content.ContentJson.LoadToken(
                    new System.Text.UTF8Encoding(false, true).GetString(payloadBytes));
                if (!(token is JObject o)) return null;
                return new ReadFile { Header = header, Payload = o };
            }
            catch
            {
                return null;
            }
        }

        private LoadResult Fresh(bool reportCorrupt)
        {
            if (reportCorrupt) Report("error_caught", "domain=save_corrupt");
            State = new SaveState(SaveDefaults.FreshPayload());
            return LoadResult.Fresh;
        }

        private LoadResult Adopt(ReadFile file, LoadResult origin)
        {
            int fileVersion = file.Header.SaveVersion;
            int buildVersion = SaveDefaults.SAVE_VERSION;

            if (fileVersion > buildVersion)
            {
                // Criterion 7: downgrade REFUSED — file untouched, read-only in-memory default
                // profile, save_migrated(from,to,success=false) recorded.
                Report("save_migrated", "from=" + fileVersion + " to=" + buildVersion + " success=false");
                State = new SaveState(SaveDefaults.FreshPayload());
                ReadOnlyMode = true;
                return LoadResult.RefusedDowngrade;
            }

            if (fileVersion < buildVersion)
            {
                var migrated = _migrations.Migrate(file.Payload, fileVersion, buildVersion);
                if (migrated == null)
                {
                    // A gap in the table is a build defect: loud fresh-fallback, never a guess.
                    Report("error_caught", "domain=save_corrupt detail=migration-gap "
                        + fileVersion + "->" + buildVersion);
                    State = new SaveState(SaveDefaults.FreshPayload());
                    return LoadResult.Fresh;
                }
                Report("save_migrated", "from=" + fileVersion + " to=" + buildVersion + " success=true");
                file.Payload = migrated;
            }

            State = new SaveState(file.Payload);
            return origin;
        }

        // Criterion 4: serialise -> WriteTempDurable(save.dat.tmp) -> Replace(tmp, dat, bak).
        // Never in place. Criterion 11: the size ceiling refuses BEFORE any write.
        public void CommitAtomic()
        {
            if (ReadOnlyMode)
            {
                Report("error_caught", "domain=save_readonly detail=commit refused after downgrade");
                return;
            }
            var file = SerializeFile();
            if (file.Length > _bounds.SaveMaxBytes)
            {
                Report("error_caught", "domain=save_over_cap detail=" + file.Length + " > "
                    + _bounds.SaveMaxBytes);
                throw new System.InvalidOperationException(
                    "save file " + file.Length + " bytes exceeds SAVE_MAX_BYTES "
                    + _bounds.SaveMaxBytes + " (CM-R05.5) — refused before writing");
            }
            _fs.WriteTempDurable(TmpPath, file);
            _fs.Replace(TmpPath, SavePath, BakPath);
            LastCommittedBytes = file.Length;
        }

        // Criterion 11: a non-positive budget writes nothing; over-cap refuses before writing.
        // Finer time estimation is device-tier work (recorded in the handoff note); the pause
        // path's contract-checkable halves are exactly these two refusals.
        public bool TryCommitWithin(int budgetMs)
        {
            if (ReadOnlyMode || budgetMs <= 0) return false;
            var file = SerializeFile();
            if (file.Length > _bounds.SaveMaxBytes)
            {
                Report("error_caught", "domain=save_over_cap detail=" + file.Length + " > "
                    + _bounds.SaveMaxBytes);
                return false;
            }
            _fs.WriteTempDurable(TmpPath, file);
            _fs.Replace(TmpPath, SavePath, BakPath);
            LastCommittedBytes = file.Length;
            return true;
        }

        // The OnApplicationPause entry: the default budget IS the config row (criterion 11).
        public bool TryCommitOnPause() => TryCommitWithin(_bounds.SavePauseBudgetMs);

        private byte[] SerializeFile()
        {
            // A-C7-3: serialised through the ONE settings site (ContentJson.CreateSerializer);
            // no JsonSerializerSettings is constructed anywhere in this assembly. UTF-8, no BOM.
            var sb = new System.Text.StringBuilder();
            using (var sw = new StringWriter(sb))
            using (var writer = new Newtonsoft.Json.JsonTextWriter(sw))
            {
                CatMetro.Content.ContentJson.CreateSerializer().Serialize(writer, State.Payload);
            }
            var payloadBytes = new System.Text.UTF8Encoding(false).GetBytes(sb.ToString());
            return SaveHeader.Write(SaveDefaults.MAGIC, SaveDefaults.FORMAT_VERSION,
                SaveDefaults.SAVE_VERSION, payloadBytes);
        }
    }
}
