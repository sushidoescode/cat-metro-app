using UnityEngine;

namespace CatMetro.Presentation.Hud
{
    // CM-UX-01 criterion 4: the ux-flows §1.1 band law computed on the SAFE AREA (never the raw
    // screen) with every input injected — no Screen reads in here; the live safe-area/dpi
    // binding is CM-UX-02's deliverable (A-UX1-5). Screen-space convention matches the input
    // path: y = 0 at the BOTTOM, so the thumb band is the lowest 25% of the safe area and the
    // status band is the topmost 15%.
    public static class HudBands
    {
        public const float ThumbBandFraction = 0.25f;
        public const float StatusBandFraction = 0.15f;
        public const float MinTargetDp = 48f;
        public const float FallbackDpi = 160f;

        // Matches TapInput.cs:53 — an unreadable dpi resolves to 1 px/dp, never 0.
        public static float PxPerDp(float dpi)
        {
            return dpi > 0f ? dpi / FallbackDpi : 1f;
        }

        public static Rect ThumbBand(Rect safeArea)
        {
            return new Rect(safeArea.x, safeArea.y,
                safeArea.width, safeArea.height * ThumbBandFraction);
        }

        public static Rect StatusBand(Rect safeArea)
        {
            float height = safeArea.height * StatusBandFraction;
            return new Rect(safeArea.x, safeArea.yMax - height, safeArea.width, height);
        }

        // R1-F2: the second parameter is the SCREEN dpi, never a dp value — the name and both
        // parameter names exist so a caller cannot pass MinTargetDp into the dpi slot and turn
        // the 48dp floor into a 14.4px one.
        public static bool MeetsMinTargetPx(Rect rectPx, float screenDpi)
        {
            float minPx = MinTargetDp * PxPerDp(screenDpi);
            return rectPx.width >= minPx && rectPx.height >= minPx;
        }
    }
}
