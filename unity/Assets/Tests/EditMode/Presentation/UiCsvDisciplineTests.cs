using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using CatMetro.Presentation.Strings;

namespace CatMetro.Tests.Presentation
{
    // CM-UX-02 criterion 6: ui.csv is append-only and BYTE-pinned. The five merged rows stay
    // byte-identical; the two new rows land exactly as pinned (halt.notice carries U+2014 —
    // the first non-ASCII byte in the table — so the round-trip assert guards a BOM/re-encode).
    // This file quotes merged LOCKED keys and is therefore excluded from the vocabulary scan
    // (exemption (c), reason recorded in HaltVocabularyGuardTests).
    public sealed class UiCsvDisciplineTests
    {
        private const string CsvPath = "Assets/Resources/Strings/ui.csv";

        private static readonly string[] FrozenBaseRows =
        {
            "win.banner,All cats home!",
            "fail.banner,Overloaded!",
            "fail.banner.timeout,The last train left the depot",
            "fail.queueoverflow,Platform overflowed at {node}",
            "fail.platformoverflow,{station} platform overflowed",
        };

        private static string[] Rows()
        {
            return File.ReadAllText(CsvPath, Encoding.UTF8)
                .Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray();
        }

        [Test]
        public void BaseRows_ByteIdentical_InOrder()
        {
            var rows = Rows();
            Assert.That(rows.Length, Is.GreaterThanOrEqualTo(FrozenBaseRows.Length));
            for (int i = 0; i < FrozenBaseRows.Length; i++)
                Assert.That(rows[i], Is.EqualTo(FrozenBaseRows[i]),
                    "merged row " + i + " must never change — append-only law");
        }

        [Test]
        public void NewRows_ExactlyTheSevenPinned_Appended()
        {
            var rows = Rows();
            // R1-L6: this legacy slice owns its frozen prefix; later slices may append without
            // weakening the byte pins below.
            // Adoption-merge resolutions (2026-08-06, #39 and CM-UX-06): append order follows
            // MERGE order — hint.tutorial row 7 (transitively pinned, #39 F9), results.next
            // row 8, then CM-UX-06's three (home.title/intro.play/intro.goal) at rows 9-11,
            // byte-pinned in UiCsvUx06Tests (indices shifted at adoption). CM-DAILYWIRE's own
            // six rows (entry, return, tally, practice, error, loading) land at rows 12-17,
            // byte-pinned in UiCsvDailyWireTests. Those merged slices establish 18 frozen rows.
            Assert.That(rows.Length, Is.GreaterThanOrEqualTo(18),
                "the frozen legacy prefix must remain present; later rows may append");
            Assert.That(rows[5], Is.EqualTo("retry.cta,Try again"), "LOCKED");
            Assert.That(rows[6], Is.EqualTo("halt.notice,Signal fault — the line stopped"),
                "DRAFT, byte-pinned including U+2014");
            Assert.That(rows[8], Is.EqualTo("results.next,Next"),
                "LOCKED (CM-UX-04's declared append, row 8 after the CM-UX-05 merge-order shift)");
        }

        [Test]
        public void NewValues_RoundTripByteExact_ThroughUiStrings()
        {
            Assert.That(UiStrings.Get("retry.cta"), Is.EqualTo("Try again"));
            Assert.That(UiStrings.Get("halt.notice"),
                Is.EqualTo("Signal fault — the line stopped"),
                "the em dash must survive the TextAsset round-trip byte-exact");
        }

        [Test]
        public void NewValues_NeverAppearAsLiterals_InPresentationComponents()
        {
            // P-4's per-slice literal guard: chrome components resolve keys, never carry copy.
            var componentDirs = new[] { "Assets/Scripts/Presentation/Hud" };
            foreach (var dir in componentDirs)
            {
                foreach (var file in Directory.GetFiles(dir, "*.cs"))
                {
                    var text = File.ReadAllText(file);
                    Assert.That(text.Contains("Try again"), Is.False,
                        file + " embeds the LOCKED CTA copy as a literal");
                    Assert.That(text.Contains("the line stopped"), Is.False,
                        file + " embeds the halt copy as a literal");
                }
            }
        }
    }
}
