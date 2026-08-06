using UnityEngine;

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

        public static Rect PinRect(Rect safeArea, float dpi)
        {
            return new Rect(0f, 0f, 0f, 0f); // RED stub
        }
    }
}
