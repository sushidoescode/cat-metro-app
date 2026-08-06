using NUnit.Framework;
using UnityEngine;
using CatMetro.Presentation.Hud;

namespace CatMetro.Tests.Presentation
{
    // CM-UX-02 criteria 3 + 8: the tappable-rect law and BOTH band-divergence zones, pure and
    // dpi-INJECTED per row (no Screen reads in any EditMode leg — R2-N1). Red-first vs the
    // ChromeGeometry skeleton. Reference rows: 360x640 raw screen; insets in px.
    public sealed class ChromeGeometryTests
    {
        private const float Tol = 0.001f;
        private const float RawH = 640f;
        private const float RawW = 360f;

        private static Rect Safe(float bottomInset, float topInset) =>
            new Rect(0f, bottomInset, RawW, RawH - bottomInset - topInset);

        private static Rect RawBand() => new Rect(0f, 0f, RawW, RawH * 0.25f);

        // --- criterion 3: TappableRect (R2-N1 semantics) ---

        [Test]
        public void TappableRect_OverlapIsAxisAlignedMinMax()
        {
            var painted = new Rect(0f, 100f, 360f, 100f);
            var consuming = new Rect(0f, 0f, 360f, 160f);
            var tappable = ChromeGeometry.TappableRect(painted, consuming);
            Assert.That(tappable.yMin, Is.EqualTo(100f).Within(Tol));
            Assert.That(tappable.yMax, Is.EqualTo(160f).Within(Tol), "clipped at the consuming top");
            Assert.That(tappable.width, Is.EqualTo(360f).Within(Tol));
        }

        [Test]
        public void TappableRect_DisjointIsZeroSize_AndCannotMeetTheFloor()
        {
            var painted = new Rect(0f, 500f, 360f, 100f);
            var consuming = RawBand();
            var tappable = ChromeGeometry.TappableRect(painted, consuming);
            Assert.That(tappable.width * tappable.height, Is.EqualTo(0f).Within(Tol),
                "disjoint rects tappable area is zero");
            Assert.That(HudBands.MeetsMinTargetPx(tappable, 160f), Is.False,
                "a zero-size tappable rect can never meet the 48dp floor");
            // positive control in the same fixture: an overlapping pair does meet it
            var live = ChromeGeometry.TappableRect(new Rect(0f, 0f, 360f, 100f), consuming);
            Assert.That(HudBands.MeetsMinTargetPx(live, 160f), Is.True);
        }

        // --- criterion 3: the 48dp floor holds on the TAPPABLE rect over the inset table ---

        [TestCase(0f, 0f, TestName = "Floor_ZeroInset")]
        [TestCase(48f, 0f, TestName = "Floor_GestureNavInset")]
        [TestCase(48f, 32f, TestName = "Floor_GestureNavPlusCutout")]
        public void ChipTappableRect_MeetsFloor_PerInsetRow(float bottom, float top)
        {
            // the shipped chip layout law: full-width, anchored to the SAFE thumb band
            var chip = HudBands.ThumbBand(Safe(bottom, top));
            var tappable = ChromeGeometry.TappableRect(chip, RawBand());
            Assert.That(HudBands.MeetsMinTargetPx(tappable, 160f), Is.True,
                "the chip's TAPPABLE height must clear 48dp at this inset row");
            // painted-overhang bound: painted may exceed tappable ONLY by the pinned
            // divergence height for this row
            float overhang = chip.height - tappable.height;
            float pinned = ChromeGeometry.InertOverhangHeight(Safe(bottom, top), RawH);
            Assert.That(overhang, Is.EqualTo(pinned).Within(Tol),
                "painted overhang equals the pinned divergence height — bounded, not silent");
        }

        // --- criterion 8: BOTH divergence zones, numerically ---

        [TestCase(0f, 0f, 0f, TestName = "Inert_ZeroInset")]
        [TestCase(48f, 0f, 36f, TestName = "Inert_Bottom48")]      // 0.75*48 - 0 = 36
        [TestCase(48f, 32f, 28f, TestName = "Inert_Bottom48Top32")] // 0.75*48 - 0.25*32 = 28
        [TestCase(0f, 64f, 0f, TestName = "Inert_TopOnly_ClampedAtZero")] // negative clamps
        public void InertOverhang_MatchesTheFormula(float bottom, float top, float expected)
        {
            Assert.That(ChromeGeometry.InertOverhangHeight(Safe(bottom, top), RawH),
                Is.EqualTo(expected).Within(Tol),
                "max(0, 0.75*bottomInset - 0.25*topInset)");
        }

        [TestCase(0f, 0f, TestName = "OverConsume_ZeroInset")]
        [TestCase(48f, 48f, TestName = "OverConsume_Bottom48")]
        [TestCase(146f, 146f, TestName = "OverConsume_Pixel9ProClass")]
        public void OverConsumingZone_EqualsTheBottomInset(float bottom, float expected)
        {
            Assert.That(ChromeGeometry.OverConsumingHeight(Safe(bottom, 0f)),
                Is.EqualTo(expected).Within(Tol),
                "raw-band pixels below the safe area still retry — height = bottom inset");
        }
    }
}
