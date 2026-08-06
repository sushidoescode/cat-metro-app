using UnityEngine;

namespace CatMetro.Presentation.Hud
{
    // CM-UX-02 criterion 2: the state-bound chrome controller. Attached by the composition
    // root in CM-UX-07 (forward obligation: the SAME PR must bind TapInput.BoardInputActive);
    // tests attach directly over real wiring. State map: FailureReview -> CTA · Halted -> veil
    // · Playing/Won -> neither (CM-UX-04 MAY extend Won to its results panel and may NOT relax
    // the other rows — bounded supersession). Renders react to the state source WITHIN ONE
    // PUMPED FRAME of a change (P-6 language; polling in Update).
    public sealed class ScreenChromeController : MonoBehaviour
    {
        private System.Func<string> _screenState;

        public RetryCtaView Cta { get; private set; }
        public HaltVeilView Veil { get; private set; }

        public void Attach(System.Func<string> screenState)
        {
            _screenState = screenState; // skeleton (red phase): no views, no polling yet
        }
    }
}
