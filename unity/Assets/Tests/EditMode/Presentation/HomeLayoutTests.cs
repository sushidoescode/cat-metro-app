using NUnit.Framework;
using UnityEngine;
using CatMetro.Presentation.Screens;
using CatMetro.Presentation.Hud;

namespace CatMetro.Tests.Presentation
{
    // CM-UX-06 criterion 3, red-first: the pin layout law over INJECTED inputs (the CM-UX-01
    // law — dpi injected per row, no Screen reads in any EditMode leg). Reference table on the
    // 360x640 frame plus the dpi-0 fallback and one bottom-inset case.
    public sealed class HomeLayoutTests
    {
        [Test]
        public void Reference360x640_Dpi160_PinIs72dpSquare_CenteredInThumbBand()
        {
            var safeArea = new Rect(0f, 0f, 360f, 640f); // 1 px per dp at 160 dpi
            var pin = HomeLayout.PinRect(safeArea, 160f);

            Assert.That(pin.width, Is.EqualTo(72f).Within(0.001f), "72dp side at 1 px/dp");
            Assert.That(pin.height, Is.EqualTo(72f).Within(0.001f));
            Assert.That(pin.x, Is.EqualTo(144f).Within(0.001f), "horizontally centered");
            Assert.That(pin.y, Is.EqualTo(44f).Within(0.001f),
                "vertically centered in the 160px thumb band");

            Assert.That(HudBands.MeetsMinTargetPx(pin, 160f), Is.True,
                "the pin clears the 48dp floor (A11Y-S01-4)");

            var band = HudBands.ThumbBand(safeArea);
            Assert.That(pin.xMin, Is.GreaterThanOrEqualTo(band.xMin));
            Assert.That(pin.xMax, Is.LessThanOrEqualTo(band.xMax));
            Assert.That(pin.yMin, Is.GreaterThanOrEqualTo(band.yMin));
            Assert.That(pin.yMax, Is.LessThanOrEqualTo(band.yMax),
                "the whole pin sits inside the thumb band — thumb-reachable by construction");
        }

        [Test]
        public void DpiZeroFallback_MatchesTheReferenceRow()
        {
            // HudBands.PxPerDp: unreadable dpi resolves to 1 px/dp, never 0 (TapInput law)
            var safeArea = new Rect(0f, 0f, 360f, 640f);
            Assert.That(HomeLayout.PinRect(safeArea, 0f),
                Is.EqualTo(HomeLayout.PinRect(safeArea, 160f)),
                "dpi 0 falls back to the 160-dpi identity, matching PxPerDp");
        }

        [Test]
        public void BottomInset_ShiftsThePinUp_FloorStillHolds()
        {
            // 48px gesture-nav inset: the safe area starts at y=48; the band — and the pin —
            // ride it up (the CM-UX-01 safe-area law, ux-flows:32).
            var safeArea = new Rect(0f, 48f, 360f, 592f);
            var pin = HomeLayout.PinRect(safeArea, 160f);
            var band = HudBands.ThumbBand(safeArea); // (0, 48, 360, 148)

            Assert.That(pin.y, Is.EqualTo(48f + (148f - 72f) / 2f).Within(0.001f),
                "centered in the INSET band, never the raw screen");
            Assert.That(HudBands.MeetsMinTargetPx(pin, 160f), Is.True);
            Assert.That(pin.yMin, Is.GreaterThanOrEqualTo(band.yMin));
            Assert.That(pin.yMax, Is.LessThanOrEqualTo(band.yMax));
        }

        [Test]
        public void HighDpi_PinScalesWithTheFloor()
        {
            // Pixel-9-Pro-class row: at ~495 dpi the floor and the pin scale through the SAME
            // PxPerDp, so the 72dp side clears the 48dp floor at ANY dpi by construction.
            var safeArea = new Rect(0f, 0f, 1344f, 2992f);
            var pin = HomeLayout.PinRect(safeArea, 495f);
            Assert.That(pin.width, Is.EqualTo(72f * HudBands.PxPerDp(495f)).Within(0.01f));
            Assert.That(HudBands.MeetsMinTargetPx(pin, 495f), Is.True);
        }
    }
}
