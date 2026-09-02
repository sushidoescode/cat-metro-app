using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using CatMetro.Presentation.Strings;

namespace CatMetro.Tests.Presentation
{
    // Pins the six Daily Live rows this feature owns without claiming later features may not append
    // more localized copy after them.
    public sealed class UiCsvDailyWireTests
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
        public void DailyLiveRows_AreAppendOnlyAndBytePinned()
        {
            var rows = Rows();
            // This slice owns rows 12-13. Later slices may append rows, but may not move,
            // rewrite, reorder, or duplicate either owned key.
            Assert.That(rows.Length, Is.GreaterThanOrEqualTo(14),
                "the csv must retain the 12 merged rows and this slice's two rows");
            Assert.That(rows[12], Is.EqualTo("home.daily.label,Daily Line"), "DRAFT");
            Assert.That(rows[13], Is.EqualTo("results.daily.done,Home"), "DRAFT");
            AssertUniqueKey(rows, "home.daily.label");
            AssertUniqueKey(rows, "results.daily.done");
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
            Assert.That(UiStrings.Get("home.daily.tally"),
                Is.EqualTo("Dailies completed: {count}"));
            Assert.That(UiStrings.Get("home.daily.unavailable"),
                Is.EqualTo("Daily unavailable — try again"));
            Assert.That(UiStrings.Get("daily.practice"),
                Is.EqualTo("Clock changed — practice run"));
            Assert.That(UiStrings.Get("home.daily.loading"),
                Is.EqualTo("Preparing today's Line…"));
        }

        [Test]
        public void NewValues_NeverQuotedLiterals_InPresentationComponents()
        {
            // P-4: components resolve KEYS. Quoted-literal form so "Home" cannot false-match a
            // bare identifier (GameRoot.Home, HomeScreenView, Stack.Push("home") is lowercase
            // and untouched by this needle) elsewhere in Presentation.
            var banned = new[]
            {
                "\"Daily Line\"", "\"Home\"", "\"Dailies completed: {count}\"",
                "\"Daily unavailable — try again\"", "\"Clock changed — practice run\"",
                "\"Preparing today's Line…\"",
            };
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
