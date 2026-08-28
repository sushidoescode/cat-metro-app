using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using CatMetro.Presentation.Strings;

namespace CatMetro.Tests.Presentation
{
    // CM-DAILYWIRE: this slice's OWN csv discipline (the P-4 per-slice guard, the
    // UiCsvUx06Tests precedent). The merged UiCsvDisciplineTests/UiCsvUx06Tests pin rows 0-11
    // byte-exact and are left completely untouched; this file pins the two rows THIS contract
    // appends.
    public sealed class UiCsvDailyWireTests
    {
        private const string CsvPath = "Assets/Resources/Strings/ui.csv";

        private static string[] Rows()
        {
            return File.ReadAllText(CsvPath, Encoding.UTF8)
                .Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray();
        }

        [Test]
        public void DailyLiveRows_AreAppendOnlyAndBytePinned()
        {
            var rows = Rows();
            // This legacy slice owns rows 0-17. Later slices may only append after them.
            Assert.That(rows.Length, Is.GreaterThanOrEqualTo(18),
                "the frozen Daily Live prefix must remain present; later rows may append");
            Assert.That(rows[12], Is.EqualTo("home.daily.label,Daily Line"), "DRAFT");
            Assert.That(rows[13], Is.EqualTo("results.daily.done,Home"), "DRAFT");
            Assert.That(rows[14], Is.EqualTo(
                "home.daily.tally,Dailies completed: {count}"));
            Assert.That(rows[15], Is.EqualTo(
                "home.daily.unavailable,Daily unavailable — try again"));
            Assert.That(rows[16], Is.EqualTo(
                "daily.practice,Clock changed — practice run"));
            Assert.That(rows[17], Is.EqualTo(
                "home.daily.loading,Preparing today's Line…"));
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
