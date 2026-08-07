using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using CatMetro.Presentation.Strings;

namespace CatMetro.Tests.Presentation
{
    // CM-UX-05 criterion 6, red-first: ui.csv gains EXACTLY one appended row —
    // hint.tutorial (DRAFT: queued for the TG-5 voice sitting before any device exposure;
    // the only sanctioned tutorial text, CM-R13.5). The pin is index-tolerant (A-UX5-4:
    // sibling append-slices land in parallel; this row must sit AFTER the 7 merged rows,
    // byte-exact, exactly once) so a rebase re-derives neighbors without weakening it.
    public sealed class HintStringsDisciplineTests
    {
        private const string CsvPath = "Assets/Resources/Strings/ui.csv";
        private const string PinnedRow = "hint.tutorial,Tap the flashing switch";

        private static string[] Rows()
        {
            return File.ReadAllText(CsvPath, Encoding.UTF8)
                .Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray();
        }

        [Test]
        public void Csv_CarriesExactlyOneHintRow_AppendedAfterTheMergedSeven()
        {
            var rows = Rows();
            var hits = rows.Select((row, i) => (row, i))
                .Where(t => t.row.StartsWith("hint.tutorial,")).ToArray();
            Assert.That(hits.Length, Is.EqualTo(1), "exactly one hint.tutorial row exists");
            Assert.That(hits[0].row, Is.EqualTo(PinnedRow), "DRAFT copy, byte-pinned");
            Assert.That(hits[0].i, Is.GreaterThanOrEqualTo(7),
                "append-only law: the row sits after the 7 merged rows, never among them");
        }

        [Test]
        public void HintValue_RoundTripsByteExact_ThroughUiStrings()
        {
            Assert.That(UiStrings.Get("hint.tutorial"), Is.EqualTo("Tap the flashing switch"),
                "the chip copy resolves through the csv, never the missing-key sentinel");
        }

        [Test]
        public void HintValue_NeverALiteral_InAnyPresentationComponent()
        {
            // P-4's per-slice literal guard, over the WHOLE Presentation tree: components
            // resolve keys, never carry copy.
            foreach (var file in Directory.GetFiles(
                "Assets/Scripts/Presentation", "*.cs", SearchOption.AllDirectories))
            {
                Assert.That(File.ReadAllText(file).Contains("Tap the flashing switch"),
                    Is.False, file + " embeds the hint copy as a literal");
            }
        }

        [Test]
        public void LiteralGuard_CanFail_PositiveControl()
        {
            // anti-vacuity: the scan detects the copy when it exists — THIS file carries it
            // (legally: tests are not components) and the same Contains() finds it here.
            var self = Directory.GetFiles(
                    "Assets/Tests/EditMode/Presentation", "HintStringsDisciplineTests.cs",
                    SearchOption.TopDirectoryOnly)
                .Single();
            Assert.That(File.ReadAllText(self).Contains("Tap the flashing switch"), Is.True,
                "the guard's detection mechanism demonstrably fires on a present literal");
        }
    }
}
