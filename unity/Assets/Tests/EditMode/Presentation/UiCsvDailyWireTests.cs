using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using CatMetro.Presentation.Strings;

namespace CatMetro.Tests.Presentation
{
    // Pins the two daily rows this feature owns without claiming later features may not append
    // more localized copy after them.
    public sealed class UiCsvDailyWireTests
    {
        private const string CsvPath = "Assets/Resources/Strings/ui.csv";

        private static string[] Rows()
        {
            return File.ReadAllText(CsvPath, Encoding.UTF8)
                .Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray();
        }

        [Test]
        public void DailyRows_StayBytePinnedAtTheirAppendPositions()
        {
            var rows = Rows();
            Assert.That(rows.Length, Is.GreaterThanOrEqualTo(14),
                "the two daily rows remain present even when later features append strings");
            Assert.That(rows[12], Is.EqualTo("home.daily.label,Daily Line"), "DRAFT");
            Assert.That(rows[13], Is.EqualTo("results.daily.done,Home"), "DRAFT");
        }

        [Test]
        public void MergedRows_StayUntouched_AppendOnlyLaw()
        {
            var keys = Rows().Take(12).Select(r => r.Substring(0, r.IndexOf(','))).ToArray();
            Assert.That(keys, Is.EqualTo(new[]
            {
                "win.banner", "fail.banner", "fail.banner.timeout", "fail.queueoverflow",
                "fail.platformoverflow", "retry.cta", "halt.notice", "hint.tutorial",
                "results.next", "home.title", "intro.play", "intro.goal",
            }));
        }

        [Test]
        public void NewValues_RoundTrip_ThroughUiStrings()
        {
            Assert.That(UiStrings.Get("home.daily.label"), Is.EqualTo("Daily Line"));
            Assert.That(UiStrings.Get("results.daily.done"), Is.EqualTo("Home"));
        }

        [Test]
        public void NewValues_NeverQuotedLiterals_InPresentationComponents()
        {
            // P-4: components resolve KEYS. Quoted-literal form so "Home" cannot false-match a
            // bare identifier (GameRoot.Home, HomeScreenView, Stack.Push("home") is lowercase
            // and untouched by this needle) elsewhere in Presentation.
            var banned = new[] { "\"Daily Line\"", "\"Home\"" };
            foreach (var file in Directory.GetFiles(
                "Assets/Scripts/Presentation", "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                foreach (var literal in banned)
                    Assert.That(text.Contains(literal), Is.False,
                        file + " embeds the csv copy " + literal + " as a literal");
            }

            // positive controls (anti-vacuity): the detector detects
            Assert.That("var t = \"Daily Line\";".Contains(banned[0]), Is.True,
                "the quoted-literal scan can fire when a literal exists");
            Assert.That("var t = \"Home\";".Contains(banned[1]), Is.True,
                "the quoted-literal scan can fire when a literal exists");
        }
    }
}
