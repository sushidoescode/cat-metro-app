#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CatMetro.Content;

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
                // CM-SEAMS (closes the F8/security-S1 debt row, PR #52 round-1 review): a
                // pre-read size cap (ContentBounds.CONTENT_MAX_FILE_BYTES, mirrors LevelImporter's
                // belt) rejects an oversize file before it is ever read into memory, and the
                // parse itself is depth-bounded via JsonTextReader.MaxDepth = ContentBounds.
                // CONTENT_MAX_JSON_DEPTH — the same constant, the same guard shape as
                // ContentJson.Settings.MaxDepth (the pattern used everywhere else content-shaped
                // bytes are parsed in this tree). A bare JObject.Parse has no MaxDepth parameter,
                // so an equivalent JsonTextReader construction is the way to reach the same
                // guard for a JObject-shaped read. Still narrower than the full LevelImporter
                // belt — no BOM-tolerant decode, no comment/duplicate-key rejection, no pre-scan
                // — because this file's schema is one flat boolean key and this seam is dev-only,
                // human-pushed via `adb push` (never shipped, never remote-supplied): a
                // materially smaller trust boundary than shipped content.
                var info = new FileInfo(path);
                if (info.Length > ContentBounds.CONTENT_MAX_FILE_BYTES)
                {
                    UnityEngine.Debug.LogError("DEVCAP_BOOT_OVERRIDE_INVALID " + path
                        + " — payload " + info.Length + " bytes exceeds CONTENT_MAX_FILE_BYTES"
                        + " — booting the shipped path");
                    return false;
                }
                JObject root;
                using (var stringReader = new StringReader(File.ReadAllText(path)))
                using (var jsonReader = new JsonTextReader(stringReader)
                    { MaxDepth = ContentBounds.CONTENT_MAX_JSON_DEPTH })
                {
                    root = JObject.Load(jsonReader);
                }
                var flag = root["bootToHome"];
                // criterion 3(b): a missing/renamed key (flag == null) falls through to `return
                // false` below WITHOUT logging — not an error, the well-formed shape of "no
                // override requested" (the same shape as the file being absent entirely).
                if (flag == null) return false;
                // F2 (PR #52 round-1 review): a PRESENT key with a non-boolean value ("true" the
                // string, 1 the int, etc.) is a genuinely unusable file — the same loud-fallback
                // discipline as a JSON syntax error, naming the actual type so a human who wrote
                // the file via a quoted shell one-liner gets a diagnosable message instead of
                // silence.
                if (flag.Type != JTokenType.Boolean)
                {
                    UnityEngine.Debug.LogError("DEVCAP_BOOT_OVERRIDE_INVALID " + path
                        + " — bootToHome must be a boolean, found " + flag.Type
                        + " — booting the shipped path");
                    return false;
                }
                // criterion 3(c): a well-formed, explicit `false` is ALSO not an error — same
                // silent "no override requested" shape.
                if (!(bool)flag) return false;
                UnityEngine.Debug.Log("DEVCAP_BOOT_OVERRIDE " + path);
                return true;
            }
            catch (System.Exception ex)
            {
                // security S2 (PR #52 round-1 review): sanitize before concatenating into a log
                // line — strip newlines (no multi-line log injection / spoofed extra lines) and
                // cap length (an adversarial/corrupt file can't blow up the log with a giant
                // exception message).
                string safeMsg = ex.Message.Replace('\n', ' ').Replace('\r', ' ');
                if (safeMsg.Length > 200) safeMsg = safeMsg.Substring(0, 200) + "…";
                UnityEngine.Debug.LogError("DEVCAP_BOOT_OVERRIDE_INVALID " + path
                    + " — " + safeMsg + " — booting the shipped path");
                return false;
            }
        }
    }
}
#endif
