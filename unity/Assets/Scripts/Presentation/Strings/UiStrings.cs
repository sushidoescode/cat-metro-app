using System.Collections.Generic;

namespace CatMetro.Presentation.Strings
{
    // CM-C2b criterion 4: zero literal UI strings in components — every user-facing string
    // resolves through unity/Assets/Resources/Strings/ui.csv (created by CM-C2b; CM-C3 appends
    // rows only). Key,value per line; the LOCKED win banner lives in the csv, never in code.
    public static class UiStrings
    {
        private static Dictionary<string, string> _table;

        public static string Get(string key)
        {
            if (_table == null) Load();
            return _table.TryGetValue(key, out var value)
                ? value
                : "??" + key + "??"; // loud missing-key sentinel, never a silent empty
        }

        private static void Load()
        {
            _table = new Dictionary<string, string>();
            var asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Strings/ui");
            if (asset == null) return;
            foreach (var line in asset.text.Split('\n'))
            {
                var trimmed = line.TrimEnd('\r');
                int comma = trimmed.IndexOf(',');
                if (comma <= 0) continue;
                _table[trimmed.Substring(0, comma)] = trimmed.Substring(comma + 1);
            }
        }

        // Test seam: proves a banner text RESOLVED through the csv rather than a literal.
        public static bool CameFromCsv(string key, string text) => Get(key) == text;
    }
}
