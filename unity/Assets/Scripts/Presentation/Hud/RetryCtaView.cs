using UnityEngine;

namespace CatMetro.Presentation.Hud
{
    // CM-UX-02 criterion 3: the LOCKED Try-again chip. Render-only by law (CM-UX-01: inside
    // the retry band during FailureReview the band's own RetryTapped IS the action; registering
    // a region here is dead code). Full-width in HudBands.ThumbBand(safeArea); the 48dp floor
    // holds on the TAPPABLE rect (ChromeGeometry.TappableRect against the raw band), painted
    // overhang bounded by the pinned divergence height. Text is TMP; background material comes
    // through the Resources-loaded UI material (the GreyboxMaterial F-DEV-2 lesson: a
    // runtime-created canvas must never depend on a strippable engine default).
    public sealed class RetryCtaView : MonoBehaviour
    {
        public bool IsVisible => false; // skeleton (red phase)

        public Rect PaintedRectPx => default; // skeleton (red phase)

        public string RenderedText => ""; // skeleton (red phase)
    }
}
