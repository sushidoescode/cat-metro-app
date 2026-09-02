using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using CatMetro.Presentation.Strings;

namespace CatMetro.Tests.Presentation
{
    // CM-UX-06 criterion 7, red-first: this slice's OWN csv discipline (the P-4 per-slice
    // guard). The merged UiCsvDisciplineTests keeps rows 0-6 byte-pinned; ITS count bound
    // rises 7 -> 10 under its own declared R1-L6 evolution clause; THIS file pins the three
    // appended rows byte-exactly. All three values are DRAFT — queued for the TG-5 voice
    // sitting before any device exposure.
    public sealed class UiCsvUx06Tests
    {
        private const string CsvPath = "Assets/Resources/Strings/ui.csv";

        private static string[] Rows()
        {
            return File.ReadAllText(CsvPath, Encoding.UTF8)
                .Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray();
        }

        private static void AssertUniqueKey(string[] rows, string key)
        {
            Assert.That(rows.Count(row => row.StartsWith(key + ",")), Is.EqualTo(1),
                key + " must occur exactly once in the append-only csv");
        }

        [Test]
        public void ThisSlice_AppendsExactlyThreeRows_BytePinned()
        {
            var rows = Rows();
            // Adoption shift (2026-08-06): anchored at 7 merged rows; main gained
            // hint.tutorial + results.next before this slice merged, so 9 merged + 3 = 12
            // and this slice's pins sit at rows 9-11 (merge order governs; the #39 law).
            // Later slices may append freely; they may not move, rewrite, reorder, or duplicate
            // this slice's three owned rows.
            Assert.That(rows.Length, Is.GreaterThanOrEqualTo(12),
                "the csv must retain the nine merged rows and this slice's three rows");
            Assert.That(rows[9], Is.EqualTo("home.title,Cat Metro"), "DRAFT");
            Assert.That(rows[10], Is.EqualTo("intro.play,Play"), "DRAFT");
            Assert.That(rows[11], Is.EqualTo("intro.goal,Deliver {count} cats"), "DRAFT");
            AssertUniqueKey(rows, "home.title");
            AssertUniqueKey(rows, "intro.play");
            AssertUniqueKey(rows, "intro.goal");
        }

        [Test]
        public void MergedRows_StayUntouched_AppendOnlyLaw()
        {
            // The slice's own guard on the rows it must NOT touch (belt on top of the merged
            // pins): the first seven rows still start with the frozen keys, in order.
            var keys = Rows().Take(7).Select(r => r.Substring(0, r.IndexOf(','))).ToArray();
            Assert.That(keys, Is.EqualTo(new[]
            {
                "win.banner", "fail.banner", "fail.banner.timeout", "fail.queueoverflow",
                "fail.platformoverflow", "retry.cta", "halt.notice",
            }));
        }

        [Test]
        public void NewValues_RoundTrip_ThroughUiStrings()
        {
            Assert.That(UiStrings.Get("home.title"), Is.EqualTo("Cat Metro"));
            Assert.That(UiStrings.Get("intro.play"), Is.EqualTo("Play"));
            Assert.That(UiStrings.Get("intro.goal"), Is.EqualTo("Deliver {count} cats"),
                "the {count} token survives the TextAsset round-trip for substitution");
        }

        [Test]
        public void NewValues_NeverQuotedLiterals_InPresentationComponents()
        {
            // P-4: components resolve KEYS. Quoted-literal form so "Play" cannot false-match
            // identifiers like PlayRequested or the quoted state literal "Playing".
            var banned = new[] { "\"Cat Metro\"", "\"Play\"", "\"Deliver {count} cats\"" };
            foreach (var file in Directory.GetFiles(
                "Assets/Scripts/Presentation", "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                foreach (var literal in banned)
                    Assert.That(text.Contains(literal), Is.False,
                        file + " embeds the csv copy " + literal + " as a literal");
            }

            // positive control (anti-vacuity): the detector detects
            Assert.That("var t = \"Play\";".Contains(banned[1]), Is.True,
                "the quoted-literal scan can fire when a literal exists");
            Assert.That("state == \"Playing\"".Contains(banned[1]), Is.False,
                "and the quoted form does not false-match the merged state literal");
        }
    }
}
