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
        public void ShortViewport_LeavesARealHolderBetweenTheHeaderAndEveryPin()
        {
            var safe = new Rect(0, 0, 640, 480);
            var hero = HomeLayout.HeroRect(safe, 255);
            Assert.That(hero.height, Is.GreaterThan(0f), "the first holder measurement cannot be empty");
            Assert.That(HomeLayout.PinRect(safe, 255).height / (255f / 160f), Is.EqualTo(64f));
            Assert.That(HomeLayout.DailyPinRect(safe, 255).height / (255f / 160f), Is.EqualTo(52f));
            Assert.That(HomeLayout.WardrobePinRect(safe, 255).height / (255f / 160f), Is.EqualTo(52f));
            Assert.That(hero.yMax, Is.LessThan(HomeLayout.HeaderRect(safe, 255).yMin));
            Assert.That(hero.yMin, Is.GreaterThan(HomeLayout.PinRect(safe, 255).yMax));
            Assert.That(hero.Overlaps(HomeLayout.DailyPinRect(safe, 255)), Is.False);
            Assert.That(hero.Overlaps(HomeLayout.WardrobePinRect(safe, 255)), Is.False);
        }

        [Test]
        public void Reference360x640_Dpi160_PinIsWidePrimaryCta_InsideThumbBand()
        {
            var safeArea = new Rect(0f, 0f, 360f, 640f); // 1 px per dp at 160 dpi
            var pin = HomeLayout.PinRect(safeArea, 160f);

            Assert.That(pin.width, Is.EqualTo(320f).Within(0.001f),
                "the primary action spans the safe card width, not a lone square");
            Assert.That(pin.height, Is.EqualTo(64f).Within(0.001f));
            Assert.That(pin.x, Is.EqualTo(20f).Within(0.001f), "20dp side inset");
            Assert.That(pin.y, Is.EqualTo(96f).Within(0.001f),
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

            Assert.That(pin.y, Is.EqualTo(144f).Within(0.001f),
                "the complete route stack rises with the safe bottom");
            Assert.That(HudBands.MeetsMinTargetPx(pin, 160f), Is.True);
            Assert.That(pin.yMin, Is.GreaterThanOrEqualTo(band.yMin));
            Assert.That(pin.yMax, Is.LessThan(HomeLayout.HeroRect(safeArea, 160f).yMin));
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

            Assert.That(header, Is.EqualTo(new Rect(51f, 1652.5f, 815f, 331.5f))
                .Using(RectComparer.Within(0.01f)));
            Assert.That(hero, Is.EqualTo(new Rect(51f, 512.8f, 815f, 1098.9f))
                .Using(RectComparer.Within(0.01f)));
            Assert.That(cta, Is.EqualTo(new Rect(51f, 308.8f, 815f, 163.2f))
                .Using(RectComparer.Within(0.01f)));

            Assert.That(hero.height, Is.GreaterThan(safeArea.height * 0.55f),
                "the depot stage is the visual focal point");
            Assert.That(hero.yMax, Is.LessThan(header.yMin));
            Assert.That(hero.yMin, Is.GreaterThan(cta.yMax));
            Assert.That(cta.xMin, Is.GreaterThanOrEqualTo(safeArea.xMin));
            Assert.That(cta.xMax, Is.LessThanOrEqualTo(safeArea.xMax));
        }

        [Test]
        public void DailyLayout_SharesTheWardrobeRow_WithoutLeavingTheSafeArea()
        {
            var safeArea = new Rect(0f, 64f, 917f, 1920f);
            const float dpi = 408f;

            var primary = HomeLayout.PrimaryPinRect(safeArea, dpi, dailyEntryUnlocked: true);
            var daily = HomeLayout.DailyPinRect(safeArea, dpi);
            var wardrobe = WardrobeLayout.EntryRect(safeArea, dpi);

            Assert.That(wardrobe.y, Is.EqualTo(daily.y));
            Assert.That(daily.xMax, Is.LessThan(wardrobe.xMin), "the secondary routes have separate tap regions");
            Assert.That(daily.yMax, Is.LessThanOrEqualTo(primary.yMin),
                "Play stacks above Daily");
            Assert.That(primary.xMin, Is.GreaterThanOrEqualTo(safeArea.xMin));
            Assert.That(primary.xMax, Is.LessThanOrEqualTo(safeArea.xMax));
            Assert.That(daily.x, Is.EqualTo(primary.x).Within(0.01f));
            Assert.That(daily.width, Is.EqualTo(wardrobe.width).Within(0.01f));
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

            Assert.That(toggle, Is.EqualTo(new Rect(300f, 580f, 44f, 44f))
                .Using(RectComparer.Within(0.001f)),
                "the compact sound control keeps its declared top/side inset");
            Assert.That(toggle.xMin, Is.GreaterThanOrEqualTo(safeArea.xMin));
            Assert.That(toggle.xMax, Is.LessThanOrEqualTo(safeArea.xMax));
            Assert.That(toggle.yMin, Is.GreaterThanOrEqualTo(safeArea.yMin));
            Assert.That(toggle.yMax, Is.LessThanOrEqualTo(safeArea.yMax));
            Assert.That(HudBands.MeetsMinTargetPx(HomeLayout.AudioToggleHitRect(safeArea, 160f), 160f), Is.True,
                "the 44dp speaker paint has a separate 48dp touch target");
        }

        [Test]
        public void Reference360x640_CarvedTitleAndShadow_ClearBothHeaderControlsBy8Dp()
        {
            var safeArea = new Rect(0f, 0f, 360f, 640f);
            var title = HomeLayout.TitleRect(safeArea, 160f,
                audioToggleVisible: true, reminderGearVisible: true);
            var shadow = HomeLayout.TitleShadowRect(title, 160f);
            var audio = HomeLayout.AudioToggleRect(safeArea, 160f);
            var reminder = HomeLayout.ReminderGearRect(safeArea, 160f);

            Assert.That(title, Is.EqualTo(new Rect(87f, 510f, 186f, 130f))
                .Using(RectComparer.Within(0.001f)));
            Assert.That(shadow, Is.EqualTo(new Rect(82f, 497f, 202f, 146f))
                .Using(RectComparer.Within(0.001f)));
            Assert.That(title.xMin - reminder.xMax, Is.GreaterThanOrEqualTo(8f),
                "the carved title face leaves at least 8dp after the reminder control");
            Assert.That(audio.xMin - shadow.xMax, Is.GreaterThanOrEqualTo(8f),
                "the plaque's visible toy shadow also clears the speaker by 8dp");
        }

        [Test]
        public void CaptureViewport_CarvedTitleClearance_ScalesTo8Dp()
        {
            var safeArea = new Rect(0f, 64f, 917f, 1920f);
            const float dpi = 408f;
            float expectedGap = HomeLayout.HeaderControlGapDp * HudBands.PxPerDp(dpi);
            var title = HomeLayout.TitleRect(safeArea, dpi,
                audioToggleVisible: true, reminderGearVisible: true);
            var shadow = HomeLayout.TitleShadowRect(title, dpi);
            var audio = HomeLayout.AudioToggleRect(safeArea, dpi);
            var reminder = HomeLayout.ReminderGearRect(safeArea, dpi);

            Assert.That(title, Is.EqualTo(new Rect(221.85f, 1652.5f, 473.3f, 331.5f))
                .Using(RectComparer.Within(0.01f)));
            Assert.That(title.xMin - reminder.xMax,
                Is.GreaterThanOrEqualTo(expectedGap));
            Assert.That(audio.xMin - shadow.xMax,
                Is.GreaterThanOrEqualTo(expectedGap));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SecondaryRow_IsTwoEqualHalves_WithOneFullWidthPlay(bool unlocked)
        {
            var safe = new Rect(0, 0, 360, 640);
            var daily = HomeLayout.DailyPinRect(safe, 160);
            var wardrobe = HomeLayout.WardrobePinRect(safe, 160);
            var play = HomeLayout.PrimaryPinRect(safe, 160, unlocked);
            Assert.That(daily, Is.EqualTo(new Rect(20, 36, 156, 52)).Using(RectComparer.Within(.001f)));
            Assert.That(wardrobe, Is.EqualTo(new Rect(184, 36, 156, 52)).Using(RectComparer.Within(.001f)));
            Assert.That(play, Is.EqualTo(new Rect(20, 96, 320, 64)).Using(RectComparer.Within(.001f)));
            Assert.That(HomeLayout.HeroRect(safe, 160, unlocked),
                Is.EqualTo(HomeLayout.HeroRect(safe, 160, !unlocked)), "unlock must not shrink the window");
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
