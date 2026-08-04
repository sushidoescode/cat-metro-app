using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CatMetro.Content.Validation;
using CatMetro.Services;
using Newtonsoft.Json.Linq;

namespace CatMetro.Validator
{
    // CM-C5 criterion 15/17: the credential-free, licence-free, network-free CI entry point.
    // ALL file I/O lives here; the validation library receives bytes. Gate mode opens every
    // input read-only; --stamp is the ONLY path that writes meta.validatedAt, only under a
    // campaign corpus, preserving every other byte (Q-O analyst default (a)).
    public sealed class FileContentSource : IContentSource
    {
        public Task<byte[]> ReadAsync(string relativePath, CancellationToken ct) =>
            Task.FromResult(File.ReadAllBytes(relativePath));
        public bool Exists(string relativePath) => File.Exists(relativePath);
    }

    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                return Run(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("validator: ERROR — " + ex.Message);
                return 2;
            }
        }

        private static int Run(string[] args)
        {
            var corpusArgs = new List<string>();
            string outPath = null;
            bool stamp = false;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--corpus": corpusArgs.Add(args[++i]); break;
                    case "--out": outPath = args[++i]; break;
                    case "--stamp": stamp = true; break;
                    case "--assert-stamp-diff":
                        return AssertStampDiff(args[i + 1], args[i + 2]);
                    default:
                        Console.Error.WriteLine("validator: unknown argument " + args[i]);
                        return 2;
                }
            }

            // Review F8: the IContentSource seam IS the read path, not decoration.
            var source = new FileContentSource();
            byte[] Read(string path) => source.ReadAsync(path, CancellationToken.None).GetAwaiter().GetResult();
            byte[] schemaBytes = Read(Path.Combine("docs", "plan", "data", "level_schema.json"));
            var configResult = ValidatorConfig.Parse(
                Read(Path.Combine("config", "validator_thresholds.json")));
            if (!configResult.Ok)
            {
                Console.Error.WriteLine("validator: config unreadable — " + configResult.Error);
                return 2;
            }

            // Members: default corpus = content/levels/** (campaign) + the stress boards (Q-P).
            // Review F4: campaign status comes from the PATH (criterion 14 scopes the campaign
            // assertions to content/levels/**), not from how the corpus was passed — a fixture
            // file is validated but never enters the campaign-order/band/count assertions.
            var stampFiles = new List<string>();
            var members = new List<CorpusMember>();
            if (corpusArgs.Count == 0)
            {
                corpusArgs.Add(Path.Combine("content", "levels"));
                corpusArgs.Add(Path.Combine("docs", "plan", "data", "stress_boards.json"));
            }
            foreach (var arg in corpusArgs)
            {
                if (Directory.Exists(arg))
                {
                    foreach (var f in Directory.GetFiles(arg, "*.json").OrderBy(f => f, StringComparer.Ordinal))
                    {
                        members.Add(new CorpusMember(f, Read(f), IsCampaignPath(f)));
                        stampFiles.Add(f);
                    }
                }
                else if (Path.GetFileName(arg) == "stress_boards.json")
                {
                    var wrapper = JObject.Parse(System.Text.Encoding.UTF8.GetString(Read(arg)));
                    var levels = (JArray)wrapper["levels"];
                    for (int i = 0; i < levels.Count; i++)
                        members.Add(new CorpusMember(arg + "#" + i,
                            System.Text.Encoding.UTF8.GetBytes(levels[i].ToString()), isCampaign: false));
                }
                else if (File.Exists(arg))
                {
                    members.Add(new CorpusMember(arg, Read(arg), IsCampaignPath(arg)));
                    stampFiles.Add(arg);
                }
                else
                {
                    Console.Error.WriteLine("validator: corpus path not found: " + arg);
                    return 2;
                }
            }

            var request = new ValidationRequest(schemaBytes, configResult.Value,
                ReferenceTimestamp(), members);
            var report = CorpusValidator.Validate(request);

            Console.Write(report.ToTable());
            foreach (var level in report.Levels)
                foreach (var v in level.Verdicts.Where(v => v.Blocks))
                    Console.WriteLine("BLOCKING: " + level.LevelId + " stage " + (int)v.Stage
                        + " " + v.Stage + " — " + v.Detail);
            foreach (var v in report.CampaignVerdicts.Where(v => v.Blocks))
                Console.WriteLine("BLOCKING: campaign — " + v.Detail);

            if (outPath != null)
                File.WriteAllText(outPath, report.ToJson());

            if (stamp)
            {
                if (report.ExitFailure)
                {
                    Console.Error.WriteLine("validator: --stamp refused — a blocking stage failed");
                    return 1;
                }
                string ts = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
                foreach (var f in stampFiles)
                {
                    StampFile(f, ts);
                    Console.WriteLine("stamped " + f + " validatedAt=" + ts);
                }
            }

            return report.ExitFailure ? 1 : 0;
        }

        private static string Normalize(string path) => path.Replace('\\', '/');

        private static bool IsCampaignPath(string path)
        {
            var n = Normalize(path);
            return n.StartsWith("content/levels/", StringComparison.Ordinal)
                || n.Contains("/content/levels/");
        }

        // A-C5-4: the staleness reference = newest commit touching the sim or the schema. The
        // library never sees git — it receives this string (or null on any failure).
        private static string ReferenceTimestamp()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "log -1 --format=%cI -- unity/Assets/Scripts/Domain docs/plan/data/level_schema.json",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                using var p = System.Diagnostics.Process.Start(psi);
                string output = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(10000);
                if (p.ExitCode != 0 || output.Length == 0) return null;
                // Review F9: normalise to UTC "Z" so the library compares like against like even
                // on its ordinal fallback (git emits local offsets, our stamps emit Z).
                return DateTimeOffset.TryParse(output, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var instant)
                    ? instant.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")
                    : output;
            }
            catch
            {
                return null;
            }
        }

        // Byte-surgical upsert of meta.validatedAt: everything outside the touched key survives
        // byte-for-byte (criterion 17b). Never called on the stress wrapper; review F4: a
        // docs/plan/** path is refused outright (contract non-goal: no write under docs/plan/**).
        internal static void StampFile(string path, string timestamp)
        {
            if (Normalize(Path.GetFullPath(path)).Contains("/docs/plan/"))
                throw new InvalidOperationException(
                    "refusing to stamp under docs/plan/ (CM-C5 non-goal; Q-O)");
            string text = File.ReadAllText(path);
            var (metaOpen, metaClose) = FindMetaSpan(text);
            int existing = FindKeyInSpan(text, metaOpen, metaClose, "\"validatedAt\"");
            string updated;
            if (existing >= 0)
            {
                int colon = text.IndexOf(':', existing);
                int valStart = colon + 1;
                while (char.IsWhiteSpace(text[valStart])) valStart++;
                if (text[valStart] != '"') throw new InvalidOperationException("validatedAt value is not a string");
                int valEnd = valStart + 1;
                while (text[valEnd] != '"' || text[valEnd - 1] == '\\') valEnd++;
                updated = text.Substring(0, valStart) + "\"" + timestamp + "\"" + text.Substring(valEnd + 1);
            }
            else
            {
                // Insert after the last property, reusing the indentation of the line holding it.
                int lastContent = metaClose - 1;
                while (char.IsWhiteSpace(text[lastContent])) lastContent--;
                int lineStart = text.LastIndexOf('\n', lastContent) + 1;
                int ws = lineStart;
                while (text[ws] == ' ' || text[ws] == '\t') ws++;
                string indent = text.Substring(lineStart, ws - lineStart);
                string insertion = ",\n" + indent + "\"validatedAt\": \"" + timestamp + "\"";
                updated = text.Substring(0, lastContent + 1) + insertion + text.Substring(lastContent + 1);
            }
            File.WriteAllText(path, updated);
        }

        // Minimal string-aware scanner: the span (open brace index, close brace index) of the
        // top-level "meta" object.
        private static (int open, int close) FindMetaSpan(string text)
        {
            int depth = 0;
            bool inString = false, escaped = false;
            int metaDepthKeyAt = -1;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }
                if (c == '"')
                {
                    if (depth == 1 && metaDepthKeyAt < 0
                        && string.CompareOrdinal(text, i, "\"meta\"", 0, 6) == 0)
                        metaDepthKeyAt = i;
                    inString = true;
                }
                else if (c == '{' || c == '[')
                {
                    depth++;
                    if (metaDepthKeyAt >= 0 && depth == 2 && c == '{')
                    {
                        int close = MatchBrace(text, i);
                        return (i, close);
                    }
                }
                else if (c == '}' || c == ']') depth--;
            }
            throw new InvalidOperationException("no meta object found");
        }

        private static int MatchBrace(string text, int open)
        {
            int depth = 0;
            bool inString = false, escaped = false;
            for (int i = open; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') inString = true;
                else if (c == '{') depth++;
                else if (c == '}') { depth--; if (depth == 0) return i; }
            }
            throw new InvalidOperationException("unbalanced meta object");
        }

        private static int FindKeyInSpan(string text, int open, int close, string quotedKey)
        {
            bool inString = false, escaped = false;
            int depth = 0;
            for (int i = open; i < close; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }
                if (c == '"')
                {
                    if (depth == 1 && string.CompareOrdinal(text, i, quotedKey, 0, quotedKey.Length) == 0)
                        return i;
                    inString = true;
                }
                else if (c == '{') depth++;
                else if (c == '}') depth--;
            }
            return -1;
        }

        // criterion 17b's verifier: the stamped file differs from the original by exactly the
        // meta.validatedAt key — structurally identical otherwise, and line-wise by at most one
        // changed line (the comma) plus one inserted line (the key), or one changed line on
        // replacement. Exit 0 iff both hold.
        private static int AssertStampDiff(string origPath, string stampedPath)
        {
            var orig = JObject.Parse(File.ReadAllText(origPath));
            var stamped = JObject.Parse(File.ReadAllText(stampedPath));
            var stampedClone = (JObject)stamped.DeepClone();
            ((JObject)stampedClone["meta"]).Remove("validatedAt");
            var origClone = (JObject)orig.DeepClone();
            ((JObject)origClone["meta"])?.Remove("validatedAt");
            if (stamped["meta"]?["validatedAt"] == null)
            {
                Console.Error.WriteLine("assert-stamp-diff: stamped file carries no validatedAt");
                return 3;
            }
            if (!JToken.DeepEquals(stampedClone, origClone))
            {
                Console.Error.WriteLine("assert-stamp-diff: files differ beyond meta.validatedAt");
                return 3;
            }

            var a = File.ReadAllLines(origPath);
            var b = File.ReadAllLines(stampedPath);
            int inserted = b.Length - a.Length;
            if (inserted != 0 && inserted != 1)
            {
                Console.Error.WriteLine("assert-stamp-diff: line delta " + inserted + " (want 0 or 1)");
                return 3;
            }
            int changed = 0;
            for (int i = 0, j = 0; i < a.Length; i++, j++)
            {
                if (a[i] == b[j]) continue;
                if (inserted == 1 && b[j].Contains("validatedAt")) { i--; inserted = 0; continue; }
                changed++;
                if (a[i] + "," != b[j] && !b[j].Contains("validatedAt"))
                {
                    Console.Error.WriteLine("assert-stamp-diff: unexpected change at line " + (i + 1));
                    return 3;
                }
            }
            if (changed > 1)
            {
                Console.Error.WriteLine("assert-stamp-diff: " + changed + " changed lines (want <= 1)");
                return 3;
            }
            Console.WriteLine("assert-stamp-diff: OK — exactly the one key differs");
            return 0;
        }
    }
}
