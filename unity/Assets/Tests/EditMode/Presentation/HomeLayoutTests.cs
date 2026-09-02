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
            Assert.That(pin.height, Is.EqualTo(72f).Within(0.001f));
            Assert.That(pin.x, Is.EqualTo(20f).Within(0.001f), "20dp side inset");
            Assert.That(pin.y, Is.EqualTo(16f).Within(0.001f), "16dp above the safe bottom");

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

            Assert.That(pin.y, Is.EqualTo(64f).Within(0.001f),
                "16dp above the safe bottom, never the raw screen");
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
            Assert.That(hero, Is.EqualTo(new Rect(51f, 329.2f, 815f, 1399.8f))
                .Using(RectComparer.Within(0.01f)));
            Assert.That(cta, Is.EqualTo(new Rect(51f, 104.8f, 815f, 183.6f))
                .Using(RectComparer.Within(0.01f)));

            Assert.That(hero.height, Is.GreaterThan(cta.height * 7f),
                "the depot stage is the visual focal point");
            Assert.That(hero.yMax, Is.LessThan(header.yMin));
            Assert.That(hero.yMin, Is.GreaterThan(cta.yMax));
            Assert.That(cta.xMin, Is.GreaterThanOrEqualTo(safeArea.xMin));
            Assert.That(cta.xMax, Is.LessThanOrEqualTo(safeArea.xMax));
        }

        [Test]
        public void DailyLayout_SplitsTheCtaRow_WithoutLeavingTheSafeArea()
        {
            var safeArea = new Rect(0f, 64f, 917f, 1920f);
            const float dpi = 408f;

            var primary = HomeLayout.PrimaryPinRect(safeArea, dpi, dailyEntryUnlocked: true);
            var daily = HomeLayout.DailyPinRect(safeArea, dpi);

            Assert.That(primary.xMax, Is.LessThanOrEqualTo(daily.xMin),
                "Daily never overlaps the campaign action");
            Assert.That(primary.xMin, Is.GreaterThanOrEqualTo(safeArea.xMin));
            Assert.That(daily.xMax, Is.LessThanOrEqualTo(safeArea.xMax));
            Assert.That(primary.yMin, Is.GreaterThanOrEqualTo(safeArea.yMin));
            Assert.That(daily.yMax, Is.LessThanOrEqualTo(safeArea.yMax));
            Assert.That(HudBands.MeetsMinTargetPx(primary, dpi), Is.True);
            Assert.That(HudBands.MeetsMinTargetPx(daily, dpi), Is.True);
        }

        [Test]
        public void Reference360x640_AudioToggleStaysSafe_AndClearsThe48DpFloor()
        {
            var safeArea = new Rect(0f, 0f, 360f, 640f); // 1 px per dp at 160 dpi
            var toggle = HomeLayout.AudioToggleRect(safeArea, 160f);

            Assert.That(toggle, Is.EqualTo(new Rect(16f, 572f, 72f, 52f))
                .Using(RectComparer.Within(0.001f)),
                "the compact sound control keeps its declared top/side inset");
            Assert.That(toggle.xMin, Is.GreaterThanOrEqualTo(safeArea.xMin));
            Assert.That(toggle.xMax, Is.LessThanOrEqualTo(safeArea.xMax));
            Assert.That(toggle.yMin, Is.GreaterThanOrEqualTo(safeArea.yMin));
            Assert.That(toggle.yMax, Is.LessThanOrEqualTo(safeArea.yMax));
            Assert.That(HudBands.MeetsMinTargetPx(toggle, 160f), Is.True,
                "both sound-toggle dimensions clear the 48dp touch-target floor");
        }

        [Test]
        public void Reference360x640_TitleClearsBothHeaderControls()
        {
            var safeArea = new Rect(0f, 0f, 360f, 640f);
            var title = HomeLayout.TitleRect(safeArea, 160f,
                audioToggleVisible: true, reminderGearVisible: true);
            var audio = HomeLayout.AudioToggleRect(safeArea, 160f);
            var reminder = DailyReminderLayout.GearRect(safeArea, 160f);

            Assert.That(title, Is.EqualTo(new Rect(96f, 556f, 188f, 84f))
                .Using(RectComparer.Within(0.001f)));
            Assert.That(title.xMin, Is.GreaterThanOrEqualTo(audio.xMax + 8f),
                "the title leaves a tactile gap after the sound chip");
            Assert.That(title.xMax, Is.LessThanOrEqualTo(reminder.xMin - 8f),
                "the title leaves the same gap before the reminder gear");
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
