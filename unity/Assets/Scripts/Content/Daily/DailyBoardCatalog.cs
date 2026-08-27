using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using CatMetro.Content.Validation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CatMetro.Content.Daily
{
    public sealed class DailyCatalogEntry
    {
        public readonly string DateKey;
        public readonly int K;
        public readonly uint Seed;
        public readonly bool UsedFallback;
        public readonly string BoardSha256;
        public readonly ImportedLevel Level;

        internal DailyCatalogEntry(string dateKey, int k, uint seed, bool usedFallback,
            string boardSha256, ImportedLevel level)
        {
            DateKey = dateKey;
            K = k;
            Seed = seed;
            UsedFallback = usedFallback;
            BoardSha256 = boardSha256;
            Level = level;
        }
    }

    public readonly struct DailyCatalogLookupResult
    {
        public readonly bool Found;
        public readonly DailyCatalogEntry Entry;
        public readonly string Detail;

        private DailyCatalogLookupResult(bool found, DailyCatalogEntry entry, string detail)
        {
            Found = found;
            Entry = entry;
            Detail = detail ?? "";
        }

        internal static DailyCatalogLookupResult Hit(DailyCatalogEntry entry) =>
            new DailyCatalogLookupResult(true, entry, "");

        internal static DailyCatalogLookupResult Miss(string detail) =>
            new DailyCatalogLookupResult(false, null, detail);
    }

    public readonly struct DailyCatalogLoadResult
    {
        public readonly bool Ok;
        public readonly DailyBoardCatalog Value;
        public readonly string Detail;

        private DailyCatalogLoadResult(bool ok, DailyBoardCatalog value, string detail)
        {
            Ok = ok;
            Value = value;
            Detail = detail ?? "";
        }

        internal static DailyCatalogLoadResult Success(DailyBoardCatalog value) =>
            new DailyCatalogLoadResult(true, value, "");

        internal static DailyCatalogLoadResult Miss(string detail) =>
            new DailyCatalogLoadResult(false, null, detail);
    }

    // A byte-fed, engine-free cache for Daily Line. Loading performs all integrity checks once;
    // callers then get an ImportedLevel without running the generator or solver. Any missing or
    // unproven artifact is a typed miss so the shipped composition can fall back to the existing
    // deterministic DailyBoardFactory pipeline.
    public sealed class DailyBoardCatalog
    {
        public const int CurrentCatalogVersion = 1;
        public const string RelativePath = "content/daily/precomputed.json";
        // Every validator stage ran and none blocked admission. This deliberately does not say
        // every stage "passed": the validator honestly emits non-blocking Pending/Unconfigured
        // verdicts for human-playtest/liveops rows.
        public const string ValidationLabel = "eleven-stage-admitted";
        public const string BoardProvenance = "DailyBoardFactory";
        private const int MaxEntries = 3660;

        private readonly IReadOnlyList<string> _dateKeys;
        private readonly Dictionary<string, DailyCatalogEntry> _entries;

        public readonly int CatalogVersion;
        public readonly string Generator;
        public readonly string FromDateKey;
        public readonly string ThroughDateKey;

        public int Count => _entries.Count;
        public IReadOnlyList<string> DateKeys => _dateKeys;

        private DailyBoardCatalog(int catalogVersion, string generator, string fromDateKey,
            string throughDateKey, IReadOnlyList<string> dateKeys,
            Dictionary<string, DailyCatalogEntry> entries)
        {
            CatalogVersion = catalogVersion;
            Generator = generator;
            FromDateKey = fromDateKey;
            ThroughDateKey = throughDateKey;
            _dateKeys = dateKeys;
            _entries = entries;
        }

        public DailyCatalogLookupResult Lookup(string dateKey)
        {
            if (!CatMetro.Content.Daily.DateKeys.IsValid(dateKey))
                return DailyCatalogLookupResult.Miss(
                    "date key is not a real yyyy-MM-dd UTC date: " + (dateKey ?? "<null>"));
            if (!_entries.TryGetValue(dateKey, out DailyCatalogEntry entry))
                return DailyCatalogLookupResult.Miss(
                    "date is outside precomputed horizon " + FromDateKey + ".." + ThroughDateKey);
            return DailyCatalogLookupResult.Hit(entry);
        }

        public static DailyCatalogLoadResult Load(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return DailyCatalogLoadResult.Miss("daily catalog is missing");

            try
            {
                string json = new UTF8Encoding(false, true).GetString(bytes);
                JToken token = ContentJson.LoadToken(json);
                if (!(token is JObject root))
                    return DailyCatalogLoadResult.Miss("daily catalog root must be an object");

                int version = RequiredInt(root, "catalogVersion", 1, int.MaxValue);
                if (version != CurrentCatalogVersion)
                    throw new CatalogWalk("catalogVersion=" + version + " is unsupported");

                string generator = RequiredString(root, "generator");
                if (!string.Equals(generator, DailyLineSeed.GeneratorLabel,
                    StringComparison.Ordinal))
                    throw new CatalogWalk("generator must be " + DailyLineSeed.GeneratorLabel);

                string validation = RequiredString(root, "validation");
                if (!string.Equals(validation, ValidationLabel, StringComparison.Ordinal))
                    throw new CatalogWalk("validation must be " + ValidationLabel);

                string provenance = RequiredString(root, "boardProvenance");
                if (!string.Equals(provenance, BoardProvenance, StringComparison.Ordinal))
                    throw new CatalogWalk("boardProvenance must be " + BoardProvenance);

                string fromDateKey = RequiredDate(root, "fromDateKey");
                string throughDateKey = RequiredDate(root, "throughDateKey");
                int entryCount = RequiredInt(root, "entryCount", 1, MaxEntries);
                if (!(root["entries"] is JArray entries))
                    throw new CatalogWalk("entries must be an array");
                if (entries.Count != entryCount)
                    throw new CatalogWalk("entryCount=" + entryCount
                        + " does not match entries count " + entries.Count);

                IReadOnlyList<string> expectedDates =
                    CatMetro.Content.Daily.DateKeys.Enumerate(fromDateKey, entryCount);
                if (!string.Equals(expectedDates[expectedDates.Count - 1], throughDateKey,
                    StringComparison.Ordinal))
                    throw new CatalogWalk("throughDateKey does not match the contiguous horizon");

                var admitted = new Dictionary<string, DailyCatalogEntry>(entryCount,
                    StringComparer.Ordinal);
                for (int i = 0; i < entries.Count; i++)
                {
                    if (!(entries[i] is JObject entry))
                        throw new CatalogWalk("entries[" + i + "] must be an object");
                    string dateKey = RequiredDate(entry, "dateKey");
                    if (!string.Equals(dateKey, expectedDates[i], StringComparison.Ordinal))
                        throw new CatalogWalk("entries[" + i + "].dateKey must be "
                            + expectedDates[i] + ", got " + dateKey);

                    int k = RequiredInt(entry, "k", 0, int.MaxValue);
                    uint seed = RequiredUInt(entry, "seed");
                    uint expectedSeed = DailyLineSeedScheme.Instance.Derive(dateKey, k);
                    if (seed != expectedSeed)
                        throw new CatalogWalk(dateKey + " seed=" + seed
                            + " does not match deterministic UTC seed " + expectedSeed);
                    bool usedFallback = RequiredBool(entry, "usedFallback");
                    string expectedHash = RequiredString(entry, "boardSha256");
                    if (!IsLowerSha256(expectedHash))
                        throw new CatalogWalk(dateKey + " boardSha256 must be 64 lowercase hex chars");
                    if (!(entry["board"] is JObject board))
                        throw new CatalogWalk(dateKey + " board must be an object");

                    string boardJson = board.ToString(Formatting.Indented);
                    string actualHash = Sha256(boardJson);
                    if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
                        throw new CatalogWalk(dateKey + " boardSha256 mismatch");

                    ContentResult<ImportedLevel> import = LevelImporter.Import(
                        Encoding.UTF8.GetBytes(boardJson));
                    if (!import.Ok)
                        throw new CatalogWalk(dateKey + " board import failed: " + import.Error);
                    ValidateBoardMetadata(dateKey, seed, import.Value);

                    if (!admitted.TryAdd(dateKey, new DailyCatalogEntry(dateKey, k, seed,
                        usedFallback, expectedHash, import.Value)))
                        throw new CatalogWalk("duplicate date " + dateKey);
                }

                return DailyCatalogLoadResult.Success(new DailyBoardCatalog(version, generator,
                    fromDateKey, throughDateKey, expectedDates, admitted));
            }
            catch (Exception ex)
            {
                return DailyCatalogLoadResult.Miss("daily catalog rejected: "
                    + ex.GetType().Name + ": " + ex.Message);
            }
        }

        // Tooling may round-trip smaller reports while authoring, but the player contract is
        // the full configured horizon. Keeping this as a distinct loader prevents a
        // self-consistent truncated or shifted artifact from silently narrowing shipped
        // offline coverage.
        public static DailyCatalogLoadResult LoadShipped(byte[] bytes,
            DailyPipelineConfig expectedConfig)
        {
            if (expectedConfig == null)
                return DailyCatalogLoadResult.Miss(
                    "shipped daily pipeline config is missing");
            if (expectedConfig.PrevalidationDays < 1
                || expectedConfig.PrevalidationDays > MaxEntries)
                return DailyCatalogLoadResult.Miss(
                    "configured shipped horizon must contain between 1 and " + MaxEntries
                    + " entries, got " + expectedConfig.PrevalidationDays);
            if (expectedConfig.SaltMaxK < 0)
                return DailyCatalogLoadResult.Miss(
                    "configured shipped salt ceiling must be non-negative");
            if (!CatMetro.Content.Daily.DateKeys.IsValid(expectedConfig.AnchorDateKey))
                return DailyCatalogLoadResult.Miss(
                    "configured shipped anchor is invalid: "
                    + (expectedConfig.AnchorDateKey ?? "<null>"));

            IReadOnlyList<string> expectedDates;
            try
            {
                expectedDates = CatMetro.Content.Daily.DateKeys.Enumerate(
                    expectedConfig.AnchorDateKey, expectedConfig.PrevalidationDays);
            }
            catch (Exception ex)
            {
                return DailyCatalogLoadResult.Miss(
                    "configured shipped horizon is invalid: " + ex.Message);
            }

            DailyCatalogLoadResult loaded = Load(bytes);
            if (!loaded.Ok) return loaded;
            if (loaded.Value.Count != expectedConfig.PrevalidationDays)
                return DailyCatalogLoadResult.Miss(
                    "shipped daily catalog must contain exactly "
                    + expectedConfig.PrevalidationDays
                    + " entries, got " + loaded.Value.Count);
            if (!string.Equals(loaded.Value.FromDateKey, expectedConfig.AnchorDateKey,
                StringComparison.Ordinal))
                return DailyCatalogLoadResult.Miss(
                    "shipped daily catalog must start at configured anchor "
                    + expectedConfig.AnchorDateKey + ", got " + loaded.Value.FromDateKey);

            string expectedThroughDateKey = expectedDates[expectedDates.Count - 1];
            if (!string.Equals(loaded.Value.ThroughDateKey, expectedThroughDateKey,
                StringComparison.Ordinal))
                return DailyCatalogLoadResult.Miss(
                    "shipped daily catalog must end at configured through date "
                    + expectedThroughDateKey + ", got " + loaded.Value.ThroughDateKey);

            foreach (string dateKey in expectedDates)
            {
                DailyCatalogEntry entry = loaded.Value._entries[dateKey];
                if (entry.K > expectedConfig.SaltMaxK)
                    return DailyCatalogLoadResult.Miss(
                        dateKey + " uses salt k=" + entry.K
                        + " above configured ceiling " + expectedConfig.SaltMaxK);
            }
            return loaded;
        }

        public static string CreateArtifactJson(DailyRunReport report)
        {
            if (report == null) throw new ArgumentException("report must not be null");
            if (report.ExitFailure) throw new ArgumentException("report contains blocking dates");
            if (!string.Equals(report.Generator, DailyLineSeed.GeneratorLabel,
                StringComparison.Ordinal))
                throw new ArgumentException("report must use " + DailyLineSeed.GeneratorLabel);
            if (!string.Equals(report.BoardProvenance, BoardProvenance,
                StringComparison.Ordinal))
                throw new ArgumentException("report must use " + BoardProvenance);
            if (report.Records == null || report.Records.Count == 0)
                throw new ArgumentException("report must contain at least one date");

            IReadOnlyList<string> dates = CatMetro.Content.Daily.DateKeys.Enumerate(
                report.Records[0].DateKey, report.Records.Count);
            var entries = new JArray();
            for (int i = 0; i < report.Records.Count; i++)
            {
                DailyDateRecord record = report.Records[i];
                if (!string.Equals(record.DateKey, dates[i], StringComparison.Ordinal))
                    throw new ArgumentException("records must be a contiguous date horizon");
                if (record.Blocks || record.Verdict != "Pass" || record.Board == null
                    || record.BoardJson == null)
                    throw new ArgumentException(record.DateKey + " has no admitted board");
                if (record.StageVerdicts == null || record.StageVerdicts.Count != 11)
                    throw new ArgumentException(record.DateKey
                        + " did not run all eleven validator stages");
                for (int stage = 0; stage < record.StageVerdicts.Count; stage++)
                {
                    StageVerdict verdict = record.StageVerdicts[stage];
                    if ((int)verdict.Stage != stage + 1 || verdict.Blocks)
                        throw new ArgumentException(record.DateKey
                            + " has an unproven or blocking validator stage");
                }
                uint expectedSeed = DailyLineSeedScheme.Instance.Derive(record.DateKey, record.K);
                if (record.Seed != expectedSeed)
                    throw new ArgumentException(record.DateKey + " seed does not match generator");
                string canonicalBoard = DailyBoardJson.Serialize(record.Board);
                if (!string.Equals(record.BoardJson, canonicalBoard, StringComparison.Ordinal))
                    throw new ArgumentException(record.DateKey + " board serialization drifted");

                entries.Add(new JObject
                {
                    ["dateKey"] = record.DateKey,
                    ["k"] = record.K,
                    ["seed"] = record.Seed,
                    ["usedFallback"] = record.UsedFallback,
                    ["boardSha256"] = Sha256(canonicalBoard),
                    ["board"] = JObject.Parse(canonicalBoard),
                });
            }

            var root = new JObject
            {
                ["catalogVersion"] = CurrentCatalogVersion,
                ["generator"] = DailyLineSeed.GeneratorLabel,
                ["validation"] = ValidationLabel,
                ["boardProvenance"] = BoardProvenance,
                ["fromDateKey"] = dates[0],
                ["throughDateKey"] = dates[dates.Count - 1],
                ["entryCount"] = entries.Count,
                ["entries"] = entries,
            };
            string artifact = root.ToString(Formatting.Indented);
            DailyCatalogLoadResult check = Load(Encoding.UTF8.GetBytes(artifact));
            if (!check.Ok)
                throw new ArgumentException("generated artifact did not load: " + check.Detail);
            return artifact;
        }

        private static void ValidateBoardMetadata(string dateKey, uint seed, ImportedLevel level)
        {
            if (level.Dto.Id != "L800")
                throw new CatalogWalk(dateKey + " board id must be L800");
            if (level.Dto.Name != "Daily Line")
                throw new CatalogWalk(dateKey + " board name must be Daily Line");
            if (level.Dto.Seed != seed)
                throw new CatalogWalk(dateKey + " board seed does not match catalog seed");
            if (level.Dto.Meta == null || level.Dto.Meta.Band != "daily")
                throw new CatalogWalk(dateKey + " board band must be daily");
            if (level.Dto.Meta.DifficultyTarget != DailyDifficulty.Target(dateKey))
                throw new CatalogWalk(dateKey + " board difficulty does not match UTC weekday");
            if (level.Dto.Meta.AuthoredBy != "generator+validator")
                throw new CatalogWalk(dateKey + " board authoredBy must name generator+validator");
        }

        private static int RequiredInt(JObject o, string name, int min, int max)
        {
            JToken token = o[name];
            if (token == null || token.Type != JTokenType.Integer)
                throw new CatalogWalk(name + " must be an integer");
            long value;
            try { value = token.Value<long>(); }
            catch (Exception) { throw new CatalogWalk(name + " is outside integer range"); }
            if (value < min || value > max)
                throw new CatalogWalk(name + "=" + value + " outside [" + min + "," + max + "]");
            return (int)value;
        }

        private static uint RequiredUInt(JObject o, string name)
        {
            JToken token = o[name];
            if (token == null || token.Type != JTokenType.Integer)
                throw new CatalogWalk(name + " must be an unsigned integer");
            ulong value;
            try { value = token.Value<ulong>(); }
            catch (Exception) { throw new CatalogWalk(name + " is outside uint range"); }
            if (value > uint.MaxValue) throw new CatalogWalk(name + " is outside uint range");
            return (uint)value;
        }

        private static bool RequiredBool(JObject o, string name)
        {
            JToken token = o[name];
            if (token == null || token.Type != JTokenType.Boolean)
                throw new CatalogWalk(name + " must be a boolean");
            return token.Value<bool>();
        }

        private static string RequiredString(JObject o, string name)
        {
            JToken token = o[name];
            if (token == null || token.Type != JTokenType.String)
                throw new CatalogWalk(name + " must be a string");
            return token.Value<string>();
        }

        private static string RequiredDate(JObject o, string name)
        {
            string value = RequiredString(o, name);
            if (!CatMetro.Content.Daily.DateKeys.IsValid(value))
                throw new CatalogWalk(name + " must be a real yyyy-MM-dd UTC date");
            return value;
        }

        private static bool IsLowerSha256(string value)
        {
            if (value == null || value.Length != 64) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            }
            return true;
        }

        private static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text)))
                    .Replace("-", "").ToLowerInvariant();
        }

        private sealed class CatalogWalk : Exception
        {
            public CatalogWalk(string detail) : base(detail) { }
        }
    }
}
