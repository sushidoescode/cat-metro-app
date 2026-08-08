#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.IO;
using CatMetro.Content;

namespace CatMetro.Bootstrap.DevCapture
{
    // CM-C3-DEVCAP2: the dev-only boot-level override. If <devcap dir>/level.json exists, the
    // scene-boot path imports THOSE bytes (through the real importer) instead of the shipped
    // seam — so a failable demo board or a measurement fixture reaches a dev build via
    // `adb push`, never via the shipped APK. Loud provenance either way; time never enters
    // this file.
    public static class DevLevelOverride
    {
        public static string DirectoryOverride; // tests inject; null = the devcap default

        public static ImportedLevel TryImport()
        {
            string dir = string.IsNullOrEmpty(DirectoryOverride)
                ? Path.Combine(UnityEngine.Application.persistentDataPath, "devcap")
                : DirectoryOverride;
            string path = Path.Combine(dir, "level.json");
            if (!File.Exists(path)) return null;
            try
            {
                // CM-SEAMS (closes the security-S1 debt row, PR #52 review): LevelImporter.Import
                // already rejects an oversize payload (ContentBounds.CONTENT_MAX_FILE_BYTES), but
                // only AFTER the full byte array below has already been read into memory — this
                // pre-read FileInfo.Length guard rejects an oversize file before that read.
                var info = new FileInfo(path);
                if (info.Length > ContentBounds.CONTENT_MAX_FILE_BYTES)
                {
                    UnityEngine.Debug.LogError("DEVCAP_LEVEL_OVERRIDE_INVALID " + path
                        + " — payload " + info.Length + " bytes exceeds CONTENT_MAX_FILE_BYTES"
                        + " — booting the shipped path");
                    return null;
                }
                var imported = LevelImporter.Import(File.ReadAllBytes(path));
                if (!imported.Ok)
                {
                    UnityEngine.Debug.LogError("DEVCAP_LEVEL_OVERRIDE_INVALID " + path
                        + " — " + Sanitize(imported.Error.ToString())
                        + " — booting the shipped path");
                    return null;
                }
                UnityEngine.Debug.Log("DEVCAP_LEVEL_OVERRIDE " + path);
                return imported.Value;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("DEVCAP_LEVEL_OVERRIDE_INVALID " + path
                    + " — " + Sanitize(ex.Message) + " — booting the shipped path");
                return null;
            }
        }

        // CM-SEAMS (closes the code-R1 debt row, PR #52 review): mirrors DevBootOverride's
        // security-S2 sanitization — strip newlines (no multi-line log injection / spoofed extra
        // lines) and cap length (an adversarial/corrupt file, or a pathological importer error
        // detail that echoes back attacker-controlled content, can't blow up the log or forge
        // extra lines).
        private static string Sanitize(string msg)
        {
            string safe = (msg ?? "").Replace('\n', ' ').Replace('\r', ' ');
            if (safe.Length > 200) safe = safe.Substring(0, 200) + "…";
            return safe;
        }
    }
}
#endif
