using UnityEngine;
using CatMetro.Presentation.Hud;

namespace CatMetro.Presentation.Screens
{
    // CM-UX-06 criterion 3: the Home pin's layout law as pure math over INJECTED inputs (the
    // CM-UX-01 law — no Screen reads in here; the live binding is the view's). The single
    // session-1 pin sits centered in the safe-area thumb band (S-01: "inside the thumb band or
    // reachable by thumb-arc") at 72dp square — clearing the 48dp floor (A11Y-S01-4) by
    // construction for any dpi, because side and floor scale through the same PxPerDp.
    public static class HomeLayout
    {
        public const float PinSideDp = 72f;
        public const float RingMarginDp = 8f;

        // CM-DAILYWIRE: the Daily entry's secondary pin — smaller than the L001 onboarding
        // pin (56dp, still clearing the 48dp floor by construction, A11Y-S01-4) and placed to
        // its right inside the SAME thumb band, never overlapping it.
        public const float DailyPinSideDp = 56f;
        public const float DailyPinGapDp = 16f;

        public static Rect PinRect(Rect safeArea, float dpi)
        {
            float side = PinSideDp * HudBands.PxPerDp(dpi);
            var band = HudBands.ThumbBand(safeArea);
            return new Rect(
                band.x + (band.width - side) / 2f,
                band.y + (band.height - side) / 2f,
                side, side);
        }

        // The raised-ring shape twin's rect (A11Y-S01-2): the pin rect expanded by a fixed
        // dp margin — a SIZE/SHAPE state, present with motion on AND off.
        public static Rect RingRect(Rect safeArea, float dpi)
        {
            float margin = RingMarginDp * HudBands.PxPerDp(dpi);
            var pin = PinRect(safeArea, dpi);
            return new Rect(pin.x - margin, pin.y - margin,
                pin.width + margin * 2f, pin.height + margin * 2f);
        }

        // Pure math over injected inputs (the A-UX1-5 law, same as PinRect/RingRect above) —
        // no live Screen read in here. Vertically centered in the same thumb band as the L001
        // pin; horizontally offset to its right by a fixed dp gap so the two never overlap for
        // any safe-area width/dpi this app ships on.
        public static Rect DailyPinRect(Rect safeArea, float dpi)
        {
            float pxPerDp = HudBands.PxPerDp(dpi);
            var l001 = PinRect(safeArea, dpi);
            float side = DailyPinSideDp * pxPerDp;
            float gap = DailyPinGapDp * pxPerDp;
            return new Rect(l001.xMax + gap, l001.y + (l001.height - side) / 2f, side, side);
        }
    }
}
