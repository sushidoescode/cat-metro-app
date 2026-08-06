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
        public void NewRows_ExactlyTheTwoPinned_Appended()
        {
            var rows = Rows();
            // R1-L6: the exact count is SLICE-SCOPED evidence for CM-UX-02's "gains exactly
            // two rows" criterion. A later append-slice amends this bound as declared contract
            // evolution (raise the count + pin its own rows); it may never touch rows 0-6.
            // + 1 — CM-UX-05's declared append (this comment's own sanctioned path): its row
            // is pinned in HintStringsDisciplineTests; rows 0-6 stay untouched.
            Assert.That(rows.Length, Is.EqualTo(FrozenBaseRows.Length + 3),
                "CM-UX-02's two rows + CM-UX-05's one — each append-slice pins its own");
            Assert.That(rows[5], Is.EqualTo("retry.cta,Try again"), "LOCKED");
            Assert.That(rows[6], Is.EqualTo("halt.notice,Signal fault — the line stopped"),
                "DRAFT, byte-pinned including U+2014");
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
