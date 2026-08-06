using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CatMetro.Tests.EventTaxonomy
{
    // CM-C9 fixtures: the test-side RFC4180 reader over the SOURCE OF TRUTH
    // (docs/plan/data/analytics_event_taxonomy.csv). The shipped table is compiled C# data;
    // THESE fixtures re-read the CSV at test time so criterion 1 is a drift test, not a copy
    // of a copy. Quoted fields with commas and inline value domains (`source(a|b|c)`) are the
    // two shapes the reader must handle (contract criterion 1).
    public static class TaxonomyFixtures
    {
        public sealed class ParsedRow
        {
            public string Name;
            public string Area;
            public List<string> RequiredParams = new List<string>();   // bare names
            public List<string> OptionalParams = new List<string>();   // bare names
            public Dictionary<string, List<string>> ValueDomains =
                new Dictionary<string, List<string>>();                // bare name -> allowed
            public List<string> UserProperties = new List<string>();
            public List<string> Destinations = new List<string>();
            public string PrivacyClass;
            public string QaProcedure;
        }

        public static string RepoRoot()
        {
            foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    if (Directory.Exists(Path.Combine(dir.FullName, "dotnet"))
                        && Directory.Exists(Path.Combine(dir.FullName, "scripts"))
                        && File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
                        return dir.FullName;
                    dir = dir.Parent;
                }
            }
            throw new InvalidOperationException("repo root not found above "
                + AppContext.BaseDirectory + " or " + Directory.GetCurrentDirectory());
        }

        public static string CsvPath() =>
            Path.Combine(RepoRoot(), "docs", "plan", "data", "analytics_event_taxonomy.csv");

        public static List<string> NonEmptyCsvLines() =>
            File.ReadAllLines(CsvPath()).Where(l => l.Trim().Length > 0).ToList();

        // Minimal RFC4180: quoted fields may contain commas; doubled quotes escape quotes.
        // The taxonomy CSV has no embedded newlines (asserted by the line-count case).
        public static List<string> SplitCsvLine(string line)
        {
            var fields = new List<string>();
            var cur = new System.Text.StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else cur.Append(c);
                }
                else if (c == '"') inQuotes = true;
                else if (c == ',') { fields.Add(cur.ToString()); cur.Length = 0; }
                else cur.Append(c);
            }
            fields.Add(cur.ToString());
            return fields;
        }

        // A param token is `name` or `name(v1|v2|v3)` — the bare name is the token before '(';
        // the domain, when present, is the pipe-split list inside the parens.
        private static void AddParamToken(string token, List<string> names,
            Dictionary<string, List<string>> domains)
        {
            token = token.Trim();
            if (token.Length == 0) return;
            int paren = token.IndexOf('(');
            if (paren < 0) { names.Add(token); return; }
            string bare = token.Substring(0, paren);
            string inner = token.Substring(paren + 1).TrimEnd(')');
            names.Add(bare);
            domains[bare] = inner.Split('|').Select(v => v.Trim()).ToList();
        }

        private static List<string> SplitList(string field) =>
            field.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();

        public static List<ParsedRow> ParseRows()
        {
            var lines = NonEmptyCsvLines();
            var rows = new List<ParsedRow>();
            for (int i = 1; i < lines.Count; i++) // skip header
            {
                var f = SplitCsvLine(lines[i]);
                if (f.Count != 9)
                    throw new InvalidOperationException(
                        "taxonomy csv line " + (i + 1) + " has " + f.Count + " fields, expected 9");
                var row = new ParsedRow
                {
                    Name = f[0].Trim(),
                    Area = f[1].Trim(),
                    // f[2] is `trigger` — prose documentation, not carried by the table
                    PrivacyClass = f[7].Trim(),
                    QaProcedure = f[8].Trim(),
                };
                foreach (var t in f[3].Split(','))
                    AddParamToken(t, row.RequiredParams, row.ValueDomains);
                foreach (var t in f[4].Split(','))
                    AddParamToken(t, row.OptionalParams, row.ValueDomains);
                row.UserProperties = SplitList(f[5]);
                row.Destinations = SplitList(f[6]);
                rows.Add(row);
            }
            return rows;
        }

        // ---- case sources ----

        public static IEnumerable<string> RowNames() => ParseRows().Select(r => r.Name);

        public static IEnumerable<object[]> RequiredPairs() =>
            ParseRows().SelectMany(r => r.RequiredParams.Select(p => new object[] { r.Name, p }));

        public static IEnumerable<object[]> DomainCases() =>
            ParseRows().SelectMany(r => r.ValueDomains.Select(kv =>
                new object[] { r.Name, kv.Key, kv.Value.ToArray() }));

        public static IEnumerable<string> DarkRowNames() =>
            ParseRows().Where(r => r.Area == "economy" || r.Area == "ads" || r.Area == "monetization")
                .Select(r => r.Name);

        // Representative scalar for a param: domain-constrained params take their first allowed
        // value; everything else takes a compact scalar. Test data, not a product decision
        // (contract A-C9-9).
        public static Newtonsoft.Json.Linq.JObject CanonicalParams(ParsedRow row, bool includeOptional)
        {
            var o = new Newtonsoft.Json.Linq.JObject();
            foreach (var p in includeOptional
                ? row.RequiredParams.Concat(row.OptionalParams) : row.RequiredParams)
            {
                if (row.ValueDomains.TryGetValue(p, out var domain)) o[p] = domain[0];
                else if (p.EndsWith("_s") || p.EndsWith("_count") || p == "amount" || p == "attempt"
                    || p == "tick" || p == "score" || p == "stars" || p == "retries")
                    o[p] = 3;
                else o[p] = "v";
            }
            return o;
        }
    }
}
