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
            return default; // skeleton (red phase)
        }

        // Criterion 8: BOTH divergence zones between the safe-area thumb band and the raw
        // consuming band, per ux-flows band law. Heights, not prose.
        public static float InertOverhangHeight(Rect safeArea, float rawHeight)
        {
            return -1f; // skeleton (red phase)
        }

        public static float OverConsumingHeight(Rect safeArea)
        {
            return -1f; // skeleton (red phase)
        }
    }
}
