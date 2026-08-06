using System.IO;
using NUnit.Framework;

namespace CatMetro.Tests.Presentation
{
    // CM-UX-02 criterion 4's vocabulary guard (R1-F5 + R2-N2): the halt surface must stay
    // Q-B/NEW-Q4-neutral — the csv value and the slice's new sources carry none of the banned
    // tokens. Exemption list, argued day-one (the CM-UX-03 precedent):
    //   (a) the merged screen-state literal "FailureReview" — criterion 2's state map REQUIRES
    //       it (the state strings live in Bootstrap, which Presentation does not reference);
    //   (b) the NUnit surface (Assert.*, LogAssert, test names derived from state names);
    //   (c) THIS FILE (it must name the tokens to ban them) and UiCsvDisciplineTests.cs (it
    //       must quote the merged LOCKED legacy keys byte-exactly) — both excluded from the
    //       scan with this recorded reason, never silently.
    // Banned everywhere else in the slice: player-facing copy and any NEW identifier naming
    // the HALT as a failure.
    public sealed class HaltVocabularyGuardTests
    {
        private static readonly string[] SliceSources =
        {
            "Assets/Scripts/Presentation/Hud/ChromeGeometry.cs",
            "Assets/Scripts/Presentation/Hud/ScreenChromeController.cs",
            "Assets/Scripts/Presentation/Hud/RetryCtaView.cs",
            "Assets/Scripts/Presentation/Hud/HaltVeilView.cs",
            "Assets/Scripts/Presentation/Hud/UiChromeMaterial.cs",
            "Assets/Tests/EditMode/Presentation/ChromeGeometryTests.cs",
            "Assets/Tests/PlayMode/Hud/ChromeStateTests.cs",
            // exemption (c): HaltVocabularyGuardTests.cs and UiCsvDisciplineTests.cs are
            // deliberately absent — reason in the header.
        };

        private static readonly string[] Banned =
        {
            "fail", "lost", "lose", "loss", "game over", "crash",
        };

        private static string Sanitize(string text)
        {
            // exemptions (a) + (b): strip before scanning so the required literals never trip
            return text.Replace("FailureReview", "")
                       .Replace("Assert.", "")
                       .Replace("LogAssert", "");
        }

        [Test]
        public void SliceSources_CarryNoBannedVocabulary()
        {
            foreach (var rel in SliceSources)
            {
                var text = Sanitize(File.ReadAllText(rel)).ToLowerInvariant();
                foreach (var token in Banned)
                    Assert.That(text.Contains(token), Is.False,
                        rel + " carries the banned token '" + token + "'");
            }
        }

        [Test]
        public void HaltNoticeCsvValue_IsNeutral()
        {
            var value = CatMetro.Presentation.Strings.UiStrings.Get("halt.notice");
            var lowered = value.ToLowerInvariant();
            foreach (var token in Banned)
                Assert.That(lowered.Contains(token), Is.False,
                    "halt.notice carries the banned token '" + token + "'");
            // 'fault' is deliberately LEGAL: the approved copy attributes fault to the SYSTEM
            // ("Signal fault"), the Q-B-neutral posture — do not add it to Banned.
            Assert.That(value, Does.Not.Contain("??"),
                "halt.notice must resolve through the csv, not the missing-key sentinel");
        }

        [Test]
        public void Guard_CanDetectItsOwnTokens_PositiveControl()
        {
            Assert.That(Sanitize("the run FAILed here").ToLowerInvariant().Contains("fail"),
                Is.True, "the scan pipeline detects a banned token when one exists");
            Assert.That(Sanitize("entered FailureReview cleanly").ToLowerInvariant()
                .Contains("fail"), Is.False,
                "the exemption strip removes the required state literal before scanning");
        }
    }
}
