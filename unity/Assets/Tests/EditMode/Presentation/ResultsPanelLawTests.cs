using NUnit.Framework;
using UnityEngine;
using CatMetro.Presentation.Hud;

namespace CatMetro.Tests.Presentation
{
    // CM-UX-04 criterion 4 (EditMode half): the chip-rect law, pure and dpi-INJECTED per row
    // (the CM-UX-02 table discipline — no Screen reads in any EditMode leg). Red-first vs the
    // ResultsPanel skeleton. The constants pin is green on arrival by design (P-7 label).
    public sealed class ResultsPanelLawTests
    {
        // The one primary CTA is full-width in the SAFE-AREA thumb band and clears the 48dp
        // floor at every row's own dpi. Rows mirror the merged ChromeGeometryTests table:
        // zero-inset reference, gesture-nav, nav+cutout, and a Pixel-9-Pro-class frame.
        [TestCase(640f, 360f, 0f, 0f, 160f, TestName = "ResultsChip_ZeroInset_Ref")]
        [TestCase(640f, 360f, 48f, 0f, 160f, TestName = "ResultsChip_GestureNav_Ref")]
        [TestCase(640f, 360f, 48f, 32f, 160f, TestName = "ResultsChip_NavPlusCutout_Ref")]
        [TestCase(2856f, 1280f, 146f, 146f, 486f, TestName = "ResultsChip_Pixel9ProClass")]
        public void ChipRect_IsTheSafeThumbBand_AndMeetsTheFloor_PerInsetRow(
            float rawH, float rawW, float bottom, float top, float dpi)
        {
            var safe = new Rect(0f, bottom, rawW, rawH - bottom - top);
            var chip = ResultsPanel.ChipRect(safe);
            Assert.That(chip, Is.EqualTo(HudBands.ThumbBand(safe)),
                "the chip law IS the safe-area thumb band — no private geometry");
            Assert.That(HudBands.MeetsMinTargetPx(chip, dpi), Is.True,
                "the one CTA clears the 48dp floor at this row's dpi");
            // Criterion 4's no-divergence statement, made assertable: during Won the retry
            // band predicate is false (GameRoot.cs:127 — FailureReview-only), so the
            // REGISTERED rect equals the PAINTED rect and its self-overlap is itself. If the
            // law ever splits painted from registered, this row is where it must go red.
            Assert.That(ChromeGeometry.TappableRect(chip, chip), Is.EqualTo(chip),
                "registered == painted == tappable on the Won surface");
        }

        [Test]
        public void RegionConstants_ArePinned_ExplicitPriority()
        {
            // Green on arrival by design (P-7): pins the registry identity so a rename or an
            // order-reliant registration cannot slip in silently (A-UX1-3).
            Assert.That(ResultsPanel.RegionId, Is.EqualTo("results.next"));
            Assert.That(ResultsPanel.RegionPriority, Is.EqualTo(10),
                "priority is explicit per registration, never registration-order");
        }
    }
}
