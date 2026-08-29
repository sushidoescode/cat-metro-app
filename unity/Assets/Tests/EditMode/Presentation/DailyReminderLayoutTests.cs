using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Screens;

namespace CatMetro.Tests.Presentation
{
    public sealed class DailyReminderLayoutTests
    {
        private static IEnumerable<TestCaseData> PortraitViewports()
        {
            yield return new TestCaseData(new Rect(0f, 0f, 360f, 640f), 160f)
                .SetName("Reference360x640_Dpi160");
            yield return new TestCaseData(new Rect(0f, 24f, 360f, 592f), 160f)
                .SetName("Reference360x640_WithInsets");
            yield return new TestCaseData(new Rect(0f, 64f, 917f, 1920f), 408f)
                .SetName("Capture917x2048_Dpi408");
            yield return new TestCaseData(new Rect(0f, 0f, 1344f, 2992f), 495f)
                .SetName("Pixel9Class1344x2992_Dpi495");
            yield return new TestCaseData(new Rect(0f, 96f, 1344f, 2760f), 495f)
                .SetName("Pixel9Class1344x2992_WithInsets");
        }

        [TestCaseSource(nameof(PortraitViewports))]
        public void Prompt_CardAndEveryTargetStaySafe_AndTargetsClear48Dp(
            Rect safeArea, float dpi)
        {
            var layout = DailyReminderLayout.Calculate(
                safeArea, dpi, DailyReminderLayout.SheetMode.Prompt, false);

            AssertContained(layout.Card, safeArea, "prompt card");
            AssertTarget(layout.Morning, safeArea, dpi, "Morning");
            AssertTarget(layout.Afternoon, safeArea, dpi, "Afternoon");
            AssertTarget(layout.Evening, safeArea, dpi, "Evening");
            AssertTarget(layout.Accept, safeArea, dpi, "Remind me");
            AssertTarget(layout.Dismiss, safeArea, dpi, "Not now");
            Assert.That(layout.On.width, Is.Zero, "settings-only targets stay absent");
            Assert.That(layout.Close.width, Is.Zero, "prompt has exactly its two actions");
        }

        [TestCaseSource(nameof(PortraitViewports))]
        public void Settings_CardAndEveryTargetStaySafe_AndTargetsClear48Dp(
            Rect safeArea, float dpi)
        {
            var layout = DailyReminderLayout.Calculate(
                safeArea, dpi, DailyReminderLayout.SheetMode.Settings, true);

            AssertContained(layout.Card, safeArea, "settings card");
            AssertTarget(layout.On, safeArea, dpi, "On");
            AssertTarget(layout.Off, safeArea, dpi, "Off");
            AssertTarget(layout.Morning, safeArea, dpi, "Morning");
            AssertTarget(layout.Afternoon, safeArea, dpi, "Afternoon");
            AssertTarget(layout.Evening, safeArea, dpi, "Evening");
            AssertTarget(layout.OpenSettings, safeArea, dpi, "Open notification settings");
            AssertTarget(layout.Close, safeArea, dpi, "Close");
            Assert.That(layout.Accept.width, Is.Zero, "prompt-only targets stay absent");
        }

        [Test]
        public void Settings_WithoutFallback_RemovesOnlyFallbackTarget()
        {
            var safeArea = new Rect(0f, 64f, 917f, 1920f);
            var layout = DailyReminderLayout.Calculate(
                safeArea, 408f, DailyReminderLayout.SheetMode.Settings, false);

            Assert.That(layout.OpenSettings, Is.EqualTo(Rect.zero));
            AssertTarget(layout.Close, safeArea, 408f, "Close");
        }

        [TestCaseSource(nameof(PortraitViewports))]
        public void HomeGear_StaysSafe_AndClears48Dp(Rect safeArea, float dpi)
        {
            AssertTarget(DailyReminderLayout.GearRect(safeArea, dpi), safeArea, dpi,
                "Home reminder gear");
        }

        private static void AssertTarget(Rect target, Rect safeArea, float dpi, string name)
        {
            AssertContained(target, safeArea, name);
            Assert.That(HudBands.MeetsMinTargetPx(target, dpi), Is.True,
                name + " must clear the existing 48dp target law");
        }

        private static void AssertContained(Rect inner, Rect outer, string name)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin), name + " left");
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax), name + " right");
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin), name + " bottom");
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax), name + " top");
        }
    }
}
