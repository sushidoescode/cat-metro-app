using UnityEngine;

namespace CatMetro.Presentation.Hud
{
    // CM-UX-02 criterion 4: the F-DEV-4 fix — Halted renders a visible, semantics-neutral
    // veil (scrim + halt.notice) instead of nothing. Zero registered regions, zero
    // affordances: the veil is inert by construction; the input block is CM-UX-07's
    // BoardInputActive binding (forward obligation) and the restart escape is CM-UX-07's
    // wiring line (human answer Q-2). Nothing here may decide Q-B/NEW-Q4 semantics.
    public sealed class HaltVeilView : MonoBehaviour
    {
        public bool IsVisible => false; // skeleton (red phase)

        public string RenderedText => ""; // skeleton (red phase)
    }
}
