using System.Text;
using Newtonsoft.Json.Linq;
using CatMetro.Application.Analytics;
using CatMetro.Application.Save;
using CatMetro.Services;

namespace CatMetro.Bootstrap
{
    // Analytics owns this small artifact outright. It never serializes the game's whole save,
    // so future economy/reward composition cannot race a separately loaded SaveStore instance.
    public sealed class AnalyticsProfileFile
    {
        public const string Magic = "CMAP";
        public const ushort Version = 1;
        private const int MaximumFileBytes = 4096;

        private readonly ISaveFileSystem _fs;
        private readonly string _path;
        private readonly string _tmpPath;
        private readonly string _bakPath;

        public AnalyticsProfileFile(IStorageRoot root, ISaveFileSystem fs)
        {
            if (root == null || fs == null)
                throw new System.ArgumentException("root and filesystem are required");
            _fs = fs;
            _path = System.IO.Path.Combine(root.SaveDirectory, "analytics_profile.dat");
            _tmpPath = _path + ".tmp";
            _bakPath = _path + ".bak";
        }

        public string ProfilePath => _path;

        public JObject Load()
        {
            // Stale-temp cleanup is independent of recovery: its failure must not hide a valid
            // main or backup profile and silently rotate the analytics identifier.
            try { if (_fs.Exists(_tmpPath)) _fs.Delete(_tmpPath); } catch { }
            return TryRead(_path) ?? TryRead(_bakPath) ?? Defaults();
        }

        public bool TryWrite(JObject profile)
        {
            try
            {
                var canonical = Canonical(profile);
                if (!AnalyticsInstallIdentity.IsValid(
                    (string)canonical[AnalyticsInstallIdentity.ProfileKey])
                    || (long)canonical["createdAtUtc"] <= 0L)
                    return false;
                var payload = new UTF8Encoding(false).GetBytes(
                    canonical.ToString(Newtonsoft.Json.Formatting.None));
                var file = SaveHeader.Write(Magic, 1, Version, payload);
                if (file.Length > MaximumFileBytes) return false;
                _fs.WriteTempDurable(_tmpPath, file);
                _fs.Replace(_tmpPath, _path, _bakPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private JObject TryRead(string path)
        {
            try
            {
                if (!_fs.Exists(path)) return null;
                var bytes = _fs.ReadAllBytes(path);
                if (bytes == null || bytes.Length > MaximumFileBytes) return null;
                var header = SaveHeader.TryParse(bytes, Magic, out var payload);
                if (header == null || header.FormatVersion != 1
                    || header.SaveVersion != Version)
                    return null;
                var token = CatMetro.Content.ContentJson.LoadToken(
                    new UTF8Encoding(false, true).GetString(payload));
                if (!(token is JObject profile)) return null;
                var canonical = Canonical(profile);
                if (!AnalyticsInstallIdentity.IsValid(
                    (string)canonical[AnalyticsInstallIdentity.ProfileKey])
                    || (long)canonical["createdAtUtc"] <= 0L)
                    return null;
                return canonical;
            }
            catch
            {
                return null;
            }
        }

        private static JObject Canonical(JObject source)
        {
            var result = Defaults();
            if (source == null) return result;
            if (source[AnalyticsInstallIdentity.ProfileKey] is JValue idValue
                && idValue.Type == JTokenType.String
                && AnalyticsInstallIdentity.IsValid((string)idValue))
                result[AnalyticsInstallIdentity.ProfileKey] = (string)idValue;
            result["createdAtUtc"] = NonNegativeLong(source["createdAtUtc"]);
            result["lastSeenAtUtc"] = NonNegativeLong(source["lastSeenAtUtc"]);
            result["sessionCount"] = NonNegativeInt(source["sessionCount"]);
            return result;
        }

        private static long NonNegativeLong(JToken value)
        {
            if (!(value is JValue scalar) || scalar.Type != JTokenType.Integer) return 0L;
            try { return System.Math.Max(0L, (long)scalar); }
            catch { return 0L; }
        }

        private static int NonNegativeInt(JToken value)
        {
            long parsed = NonNegativeLong(value);
            return (int)System.Math.Min(int.MaxValue, parsed);
        }

        private static JObject Defaults() => new JObject
        {
            ["createdAtUtc"] = 0L,
            ["lastSeenAtUtc"] = 0L,
            ["sessionCount"] = 0,
        };
    }
}
