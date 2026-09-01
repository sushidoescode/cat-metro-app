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
        public void Reference360x640_Dpi160_PinIsWidePrimaryCta_InsideThumbBand()
        {
            var safeArea = new Rect(0f, 0f, 360f, 640f); // 1 px per dp at 160 dpi
            var pin = HomeLayout.PinRect(safeArea, 160f);

            Assert.That(pin.width, Is.EqualTo(320f).Within(0.001f),
                "the primary action spans the safe card width, not a lone square");
            Assert.That(pin.height, Is.EqualTo(60f).Within(0.001f));
            Assert.That(pin.x, Is.EqualTo(20f).Within(0.001f), "20dp side inset");
            Assert.That(pin.y, Is.EqualTo(84f).Within(0.001f),
                "Play stacks immediately above the bottom Wardrobe route");

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
            // 48px gesture-nav inset: the CTA keeps its 16dp breathing room above the safe
            // bottom rather than treating the raw screen edge as available space.
            var safeArea = new Rect(0f, 48f, 360f, 592f);
            var pin = HomeLayout.PinRect(safeArea, 160f);
            var band = HudBands.ThumbBand(safeArea);

            Assert.That(pin.y, Is.EqualTo(132f).Within(0.001f),
                "the complete route stack rises with the safe bottom");
            Assert.That(HudBands.MeetsMinTargetPx(pin, 160f), Is.True);
            Assert.That(pin.yMin, Is.GreaterThanOrEqualTo(band.yMin));
            Assert.That(pin.yMax, Is.LessThanOrEqualTo(band.yMax));
        }

        [Test]
        public void HighDpi_PinScalesWithTheFloor()
        {
            // Pixel-9-Pro-class row: at ~495 dpi the floor and the pin scale through the SAME
            // PxPerDp, so the CTA height clears the 48dp floor at ANY dpi by construction.
            var safeArea = new Rect(0f, 0f, 1344f, 2992f);
            var pin = HomeLayout.PinRect(safeArea, 495f);
            Assert.That(pin.width, Is.EqualTo(
                1344f - 40f * HudBands.PxPerDp(495f)).Within(0.01f));
            Assert.That(HudBands.MeetsMinTargetPx(pin, 495f), Is.True);
        }

        [Test]
        public void CaptureViewport_HasSafeHeader_DominantHero_AndWidePrimaryCta()
        {
            var safeArea = new Rect(0f, 64f, 917f, 1920f);
            const float dpi = 408f;

            var header = HomeLayout.HeaderRect(safeArea, dpi);
            var hero = HomeLayout.HeroRect(safeArea, dpi);
            var cta = HomeLayout.PinRect(safeArea, dpi);

            Assert.That(header, Is.EqualTo(new Rect(51f, 1769.8f, 815f, 214.2f))
                .Using(RectComparer.Within(0.01f)));
            Assert.That(hero, Is.EqualTo(new Rect(51f, 472f, 815f, 1257f))
                .Using(RectComparer.Within(0.01f)));
            Assert.That(cta, Is.EqualTo(new Rect(51f, 278.2f, 815f, 153f))
                .Using(RectComparer.Within(0.01f)));

            Assert.That(hero.height, Is.GreaterThan(cta.height * 7f),
                "the depot stage is the visual focal point");
            Assert.That(hero.yMax, Is.LessThan(header.yMin));
            Assert.That(hero.yMin, Is.GreaterThan(cta.yMax));
            Assert.That(cta.xMin, Is.GreaterThanOrEqualTo(safeArea.xMin));
            Assert.That(cta.xMax, Is.LessThanOrEqualTo(safeArea.xMax));
        }

        [Test]
        public void DailyLayout_AddsAMiddleRoute_WithoutLeavingTheSafeArea()
        {
            var safeArea = new Rect(0f, 64f, 917f, 1920f);
            const float dpi = 408f;

            var primary = HomeLayout.PrimaryPinRect(safeArea, dpi, dailyEntryUnlocked: true);
            var daily = HomeLayout.DailyPinRect(safeArea, dpi);
            var wardrobe = WardrobeLayout.EntryRect(safeArea, dpi);

            Assert.That(wardrobe.yMax, Is.LessThanOrEqualTo(daily.yMin),
                "Daily stacks above Wardrobe");
            Assert.That(daily.yMax, Is.LessThanOrEqualTo(primary.yMin),
                "Play stacks above Daily");
            Assert.That(primary.xMin, Is.GreaterThanOrEqualTo(safeArea.xMin));
            Assert.That(primary.xMax, Is.LessThanOrEqualTo(safeArea.xMax));
            Assert.That(daily.x, Is.EqualTo(primary.x).Within(0.01f));
            Assert.That(daily.width, Is.EqualTo(primary.width).Within(0.01f));
            Assert.That(primary.yMin, Is.GreaterThanOrEqualTo(safeArea.yMin));
            Assert.That(daily.yMax, Is.LessThanOrEqualTo(safeArea.yMax));
            Assert.That(HudBands.MeetsMinTargetPx(primary, dpi), Is.True);
            Assert.That(HudBands.MeetsMinTargetPx(daily, dpi), Is.True);
        }

        [Test]
        public void HomeRoutes_StackAsFullWidthButtons_WithoutAnEmptyLockedDailySlot()
        {
            var safeArea = new Rect(0f, 0f, 360f, 640f);
            const float dpi = 160f;

            var wardrobe = WardrobeLayout.EntryRect(safeArea, dpi);
            var lockedPlay = HomeLayout.PrimaryPinRect(
                safeArea, dpi, dailyEntryUnlocked: false);
            var daily = HomeLayout.DailyPinRect(safeArea, dpi);
            var unlockedPlay = HomeLayout.PrimaryPinRect(
                safeArea, dpi, dailyEntryUnlocked: true);

            Assert.That(wardrobe, Is.EqualTo(new Rect(20f, 16f, 320f, 60f))
                .Using(RectComparer.Within(0.001f)),
                "Wardrobe owns the fixed bottom slot");
            Assert.That(lockedPlay, Is.EqualTo(new Rect(20f, 84f, 320f, 60f))
                .Using(RectComparer.Within(0.001f)),
                "before Daily unlocks, Play sits directly above Wardrobe");
            Assert.That(daily, Is.EqualTo(new Rect(20f, 84f, 320f, 60f))
                .Using(RectComparer.Within(0.001f)),
                "Daily occupies the middle slot only when it exists");
            Assert.That(unlockedPlay, Is.EqualTo(new Rect(20f, 152f, 320f, 60f))
                .Using(RectComparer.Within(0.001f)),
                "after Daily unlocks, Play rises by exactly one slot");
        }

        private sealed class RectComparer : System.Collections.IComparer
        {
            private readonly float _tolerance;

            private RectComparer(float tolerance) => _tolerance = tolerance;

            public static RectComparer Within(float tolerance) => new RectComparer(tolerance);

            public int Compare(object x, object y)
            {
                var actual = (Rect)x;
                var expected = (Rect)y;
                bool same = Mathf.Abs(actual.x - expected.x) <= _tolerance
                    && Mathf.Abs(actual.y - expected.y) <= _tolerance
                    && Mathf.Abs(actual.width - expected.width) <= _tolerance
                    && Mathf.Abs(actual.height - expected.height) <= _tolerance;
                return same ? 0 : 1;
            }
        }
    }
}
