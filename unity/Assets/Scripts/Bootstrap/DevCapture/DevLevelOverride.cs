#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.IO;
using CatMetro.Content;

namespace CatMetro.Bootstrap.DevCapture
{
    // CM-C3-DEVCAP2: the dev-only boot-level override. If <devcap dir>/level.json exists, the
    // scene-boot path imports THOSE bytes (through the real importer) instead of the shipped
    // seam — so a failable demo board or a measurement fixture reaches a dev build via
    // `adb push`, never via the shipped APK. Loud provenance either way; time never enters
    // this file. RED STUB.
    public static class DevLevelOverride
    {
        public static string DirectoryOverride; // tests inject; null = the devcap default

        public static ImportedLevel TryImport()
        {
            return null; // red stub: no override ever fires
        }
    }
}
#endif
