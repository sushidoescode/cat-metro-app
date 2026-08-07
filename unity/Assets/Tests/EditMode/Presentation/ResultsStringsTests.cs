using System.IO;
using NUnit.Framework;
using CatMetro.Presentation.Strings;

namespace CatMetro.Tests.Presentation
{
    // CM-UX-04 criterion 8: the one new LOCKED row + the P-4 per-slice literal guard.
    // The csv byte-pins (rows 0-7 frozen, row 8 pinned) live in UiCsvDisciplineTests, amended
    // per its own R1-L6 declared-evolution clause; this file owns the slice's round-trip and
    // literal legs. The literal-guard legs are green on arrival by design (P-7 label).
    public sealed class ResultsStringsTests
    {
        [Test]
        public void ResultsNext_RoundTripsByteExact_ThroughUiStrings()
        {
            // Red-first: the skeleton tree has no results.next row, so this resolves to the
            // loud missing-key sentinel until the green commit appends the LOCKED row.
            Assert.That(UiStrings.Get("results.next"), Is.EqualTo("Next"), "LOCKED");
        }

        [Test]
        public void LockedCtaCopy_NeverAppearsAsALiteral_InHudComponents()
        {
            // P-4: components resolve keys, never carry copy. The scan needle is the QUOTED
            // literal, so the NextRequested identifier and the results.next key string can
            // never false-positive. Guard: green on arrival, by design.
            foreach (var file in Directory.GetFiles("Assets/Scripts/Presentation/Hud", "*.cs"))
                Assert.That(File.ReadAllText(file).Contains("\"Next\""), Is.False,
                    file + " embeds the LOCKED CTA copy as a literal");
        }

        [Test]
        public void LiteralGuard_CanDetectTheLiteral_PositiveControl()
        {
            Assert.That("var t = \"Next\";".Contains("\"Next\""), Is.True,
                "the guard's needle matches the quoted literal form it exists to catch");
        }
    }
}
