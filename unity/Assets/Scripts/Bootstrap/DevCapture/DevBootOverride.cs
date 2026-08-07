#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.IO;
using Newtonsoft.Json.Linq;

namespace CatMetro.Bootstrap.DevCapture
{
    // CM-DEVCAP3: the dev-only boot-to-home FILE seam — mirrors DevLevelOverride's exact
    // injection/read pattern (DirectoryOverride test seam, persistentDataPath/devcap default,
    // loud provenance on success, loud fallback on a genuinely malformed file, SILENT fallback
    // on a well-formed file that simply doesn't request the override — same shape as absence).
    // GameRoot.BootToHome is a static test-only field with no runtime toggle anywhere on
    // device; a file `<devcap dir>/boot.json` containing `{"bootToHome": true}` is the ONLY
    // device-side way to reach the dev screen flow, so a human can `adb push` it into a dev
    // build without a rebuild. Q-5 law: this file NEVER changes WHICH level boots (that stays
    // DevLevelOverride's job) — only whether the dev screen flow composes on top of it.
    public static class DevBootOverride
    {
        public static string DirectoryOverride; // tests inject; null = the devcap default

        public static bool ShouldBootToHome()
        {
            string dir = string.IsNullOrEmpty(DirectoryOverride)
                ? Path.Combine(UnityEngine.Application.persistentDataPath, "devcap")
                : DirectoryOverride;
            string path = Path.Combine(dir, "boot.json");
            if (!File.Exists(path)) return false;
            try
            {
                var root = JObject.Parse(File.ReadAllText(path));
                var flag = root["bootToHome"];
                // criterion 3: a missing/renamed key (flag == null) and an explicit `false`
                // value both fall through to `return false` below WITHOUT logging — neither is
                // an error, both are the well-formed shape of "no override requested" (the same
                // shape as the file being absent entirely). Only a genuine parse exception
                // (caught below) is loud.
                if (flag != null && flag.Type == JTokenType.Boolean && (bool)flag)
                {
                    UnityEngine.Debug.Log("DEVCAP_BOOT_OVERRIDE " + path);
                    return true;
                }
                return false;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("DEVCAP_BOOT_OVERRIDE_INVALID " + path
                    + " — " + ex.Message + " — booting the shipped path");
                return false;
            }
        }
    }
}
#endif
