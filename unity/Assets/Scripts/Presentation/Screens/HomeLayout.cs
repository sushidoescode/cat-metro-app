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
    }
}
