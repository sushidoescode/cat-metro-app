using UnityEngine;

namespace CatMetro.Presentation.Hud
{
    // CM-UX-02 criterion 3 (R2-N1): UnityEngine has no Rect intersection helper, so the
    // tappable-rect law lives here as pure math — axis-aligned overlap via max/min; DISJOINT
    // rects return a zero-size rect, which can never meet the 48dp floor. Pure class:
    // no Screen reads, no engine objects (the CM-UX-01 injected-inputs law). Prose note: the
    // criterion-4 vocabulary guard scans this file — keep its banned tokens out of comments.
    public static class ChromeGeometry
    {
        public static Rect TappableRect(Rect painted, Rect consuming)
        {
            float xMin = Mathf.Max(painted.xMin, consuming.xMin);
            float yMin = Mathf.Max(painted.yMin, consuming.yMin);
            float xMax = Mathf.Min(painted.xMax, consuming.xMax);
            float yMax = Mathf.Min(painted.yMax, consuming.yMax);
            if (xMax <= xMin || yMax <= yMin) return new Rect(xMin, yMin, 0f, 0f);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        // Criterion 8, zone (a): painted chip pixels ABOVE the raw consuming band — the chip
        // rides the safe area up while the merged band stays raw; height = the gap between
        // the safe thumb-band top and the raw band top, never negative.
        public static float InertOverhangHeight(Rect safeArea, float rawHeight)
        {
            float chipTop = safeArea.yMin + safeArea.height * HudBands.ThumbBandFraction;
            float rawBandTop = rawHeight * HudBands.ThumbBandFraction;
            return Mathf.Max(0f, chipTop - rawBandTop);
        }

        // Criterion 8, zone (b): raw-band pixels BELOW the safe area (system gesture space)
        // that still consume taps today — height = the bottom inset.
        public static float OverConsumingHeight(Rect safeArea)
        {
            return Mathf.Max(0f, safeArea.yMin);
        }
    }
}
