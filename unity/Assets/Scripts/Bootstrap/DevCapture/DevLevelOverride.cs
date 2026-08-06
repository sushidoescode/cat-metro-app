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
                var imported = LevelImporter.Import(File.ReadAllBytes(path));
                if (!imported.Ok)
                {
                    UnityEngine.Debug.LogError("DEVCAP_LEVEL_OVERRIDE_INVALID " + path
                        + " — " + imported.Error + " — booting the shipped path");
                    return null;
                }
                UnityEngine.Debug.Log("DEVCAP_LEVEL_OVERRIDE " + path);
                return imported.Value;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("DEVCAP_LEVEL_OVERRIDE_INVALID " + path
                    + " — " + ex.Message + " — booting the shipped path");
                return null;
            }
        }
    }
}
#endif
