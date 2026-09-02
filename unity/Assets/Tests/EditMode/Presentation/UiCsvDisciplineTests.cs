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

        private static void AssertUniqueKey(string[] rows, string key)
        {
            Assert.That(rows.Count(row => row.StartsWith(key + ",")), Is.EqualTo(1),
                key + " must occur exactly once in the append-only csv");
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
        public void OwnedAppendedRows_StayBytePinned()
        {
            var rows = Rows();
            // CM-UX-02 owns the two rows immediately after the five-row frozen base. Other
            // slices own every later append, so their presence must not change this assertion.
            Assert.That(rows.Length, Is.GreaterThanOrEqualTo(FrozenBaseRows.Length + 2),
                "the csv must retain the five-row base and CM-UX-02's two rows");
            Assert.That(rows[5], Is.EqualTo("retry.cta,Try again"), "LOCKED");
            Assert.That(rows[6], Is.EqualTo("halt.notice,Signal fault — the line stopped"),
                "DRAFT, byte-pinned including U+2014");
            AssertUniqueKey(rows, "retry.cta");
            AssertUniqueKey(rows, "halt.notice");
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
