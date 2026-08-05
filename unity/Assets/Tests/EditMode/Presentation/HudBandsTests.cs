using NUnit.Framework;
using UnityEngine;
using CatMetro.Presentation.Hud;

namespace CatMetro.Tests.Presentation
{
    // CM-UX-01 criterion 4: the ux-flows §1.1 band law on the SAFE AREA, all inputs injected.
    // Red-first against the skeleton. Reference frame: 360x640dp at dpi 160 (px == dp).
    public sealed class HudBandsTests
    {
        private const float Tol = 0.001f;

        [Test]
        public void ReferenceFrame_ZeroInset_BandTable()
        {
            var safe = new Rect(0, 0, 360, 640);

            var thumb = HudBands.ThumbBand(safe);
            Assert.That(thumb.x, Is.EqualTo(0f).Within(Tol));
            Assert.That(thumb.y, Is.EqualTo(0f).Within(Tol), "thumb band starts at the safe-area bottom");
            Assert.That(thumb.width, Is.EqualTo(360f).Within(Tol));
            Assert.That(thumb.height, Is.EqualTo(160f).Within(Tol), "bottom 25% of 640");

            var status = HudBands.StatusBand(safe);
            Assert.That(status.y, Is.EqualTo(544f).Within(Tol), "top 15% of 640 starts at 544");
            Assert.That(status.height, Is.EqualTo(96f).Within(Tol));
            Assert.That(status.width, Is.EqualTo(360f).Within(Tol));
        }

        [Test]
        public void InsetSafeArea_BandsShiftWithIt()
        {
            // 48px bottom gesture-nav inset: the safe area starts at y=48 (ux-flows.md:32 — the
            // band law is defined AFTER insets; a raw-screen band would sit under the system bar).
            var safe = new Rect(0, 48, 360, 592);

            var thumb = HudBands.ThumbBand(safe);
            Assert.That(thumb.y, Is.EqualTo(48f).Within(Tol), "thumb band rides the inset up");
            Assert.That(thumb.height, Is.EqualTo(148f).Within(Tol), "25% of the SAFE height, not the raw height");

            var status = HudBands.StatusBand(safe);
            Assert.That(status.y, Is.EqualTo(48f + 592f - 88.8f).Within(Tol));
            Assert.That(status.height, Is.EqualTo(88.8f).Within(Tol));
        }

        [Test]
        public void PxPerDp_MatchesTheInputPathConvention()
        {
            Assert.That(HudBands.PxPerDp(160f), Is.EqualTo(1f).Within(Tol));
            Assert.That(HudBands.PxPerDp(320f), Is.EqualTo(2f).Within(Tol));
            // fallback matches TapInput.cs:53 — dpi unreadable resolves to 1 px/dp, never 0
            Assert.That(HudBands.PxPerDp(0f), Is.EqualTo(1f).Within(Tol));
            Assert.That(HudBands.PxPerDp(-1f), Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void MeetsMinTarget_48dpFloor()
        {
            // dpi 320: 48dp == 96px
            Assert.That(HudBands.MeetsMinTarget(new Rect(0, 0, 96, 96), 320f), Is.True);
            Assert.That(HudBands.MeetsMinTarget(new Rect(0, 0, 95, 96), 320f), Is.False,
                "one px under the 48dp floor on either axis fails");
            Assert.That(HudBands.MeetsMinTarget(new Rect(0, 0, 96, 95), 320f), Is.False);
            // fallback dpi: 48dp == 48px
            Assert.That(HudBands.MeetsMinTarget(new Rect(0, 0, 48, 48), 0f), Is.True);
            Assert.That(HudBands.MeetsMinTarget(new Rect(0, 0, 47, 48), 0f), Is.False);
        }
    }
}
