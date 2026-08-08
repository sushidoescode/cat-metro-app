using NUnit.Framework;
using UnityEngine;
using CatMetro.Presentation.Hud;

namespace CatMetro.Tests.Presentation
{
    // CM-UX-05 criterion 4, red-first: the pure chip placement law — board-edge above the
    // SAFE-AREA thumb band, full safe-area width, exactly 48dp high at the INJECTED dpi
    // (no Screen reads in any EditMode leg — the CM-UX-01 injected-inputs law; dpi varies
    // per row per the CM-UX-02 R1-M4 lesson, one row is a real device-class frame).
    public sealed class HintChipGeometryTests
    {
        private const float Tol = 0.001f;

        [TestCase(640f, 360f, 0f, 0f, 160f, TestName = "ChipRect_ZeroInset_Ref")]
        [TestCase(640f, 360f, 48f, 0f, 160f, TestName = "ChipRect_GestureNavInset_Ref")]
        [TestCase(640f, 360f, 48f, 32f, 160f, TestName = "ChipRect_GestureNavPlusCutout_Ref")]
        [TestCase(2856f, 1280f, 146f, 146f, 486f, TestName = "ChipRect_Pixel9ProClass")]
        public void ChipRect_SitsOnTheThumbBandTop_FullWidth_48dpHigh(
            float rawH, float rawW, float bottom, float top, float dpi)
        {
            var safe = new Rect(0f, bottom, rawW, rawH - bottom - top);
            var chip = HintChipView.ChipRect(safe, dpi);
            var band = HudBands.ThumbBand(safe);

            Assert.That(chip.yMin, Is.EqualTo(band.yMax).Within(Tol),
                "board-edge law: the chip's bottom edge IS the safe thumb band's top");
            Assert.That(chip.x, Is.EqualTo(safe.x).Within(Tol));
            Assert.That(chip.width, Is.EqualTo(safe.width).Within(Tol), "full safe-area width");
            Assert.That(chip.height, Is.EqualTo(HudBands.MinTargetDp * HudBands.PxPerDp(dpi))
                .Within(Tol), "exactly 48dp at this row's dpi — A11Y-S01-4 dimensionally");
            Assert.That(HudBands.MeetsMinTargetPx(chip, dpi), Is.True,
                "the 48dp floor holds by construction at this row's dpi");
        }

        [Test]
        public void ChipRect_DpiZero_FallsBackTo48Px()
        {
            // PxPerDp's fallback (dpi 0 -> 1 px/dp, matching TapInput) keeps batchmode hosts
            // deterministic instead of collapsing the chip to zero height.
            var safe = new Rect(0f, 0f, 360f, 640f);
            Assert.That(HintChipView.ChipRect(safe, 0f).height, Is.EqualTo(48f).Within(Tol));
        }

        [Test]
        public void FloorAssert_CanFail_NegativeControl()
        {
            // anti-vacuity: the same floor helper rejects a half-height rect, so the table's
            // green rows are demonstrably able to go red.
            var safe = new Rect(0f, 0f, 360f, 640f);
            var chip = HintChipView.ChipRect(safe, 160f);
            var squashed = new Rect(chip.x, chip.y, chip.width, chip.height / 2f);
            Assert.That(HudBands.MeetsMinTargetPx(squashed, 160f), Is.False,
                "24dp must not clear a 48dp floor");
        }

        [Test]
        public void LiveRegionHook_IsPolite_A11yS01_5()
        {
            // hook only — the accessibility hierarchy + TalkBack pass stays UX-OPEN-11.
            Assert.That(HintChipView.LiveRegionPoliteness, Is.EqualTo("polite"));
        }
    }
}
