using UnityEngine;

namespace CatMetro.Presentation.Hud
{
    // CM-UX-05 criteria 2/3: the state-bound hint chip controller. Attached by the
    // composition root in CM-UX-07 (enumerated line: Attach(() => root.ScreenState) + reset
    // on level load); tests attach directly over real wiring. Visibility law: the chip
    // renders after the 2nd FailureReview entry of this level attempt-run, during Playing
    // and FailureReview only — S-01 flow node L returns the player to play WITH the hint.
    // Halted renders nothing here (the CM-UX-02 veil owns that surface; count untouched).
    // Renders react to the state source WITHIN ONE PUMPED FRAME (P-6 language; polled in
    // Update like the merged ScreenChromeController, never eagerly at Attach alone).
    public sealed class HintChipController : MonoBehaviour
    {
        public const int ChipVisibleAfterEntries = 2;

        private System.Func<string> _screenState;
        private readonly HintAttemptCounter _attempts = new HintAttemptCounter();
        private Canvas _canvas;

        public HintChipView Chip { get; private set; }
        public int AttemptCount => _attempts.Count;

        // The whole chip tree hangs off this root — tests walk IT, never a subtree
        // (the CM-UX-02 R1-H2 lesson).
        public GameObject ChipRoot => _canvas != null ? _canvas.gameObject : null;

        public void Attach(System.Func<string> screenState)
        {
        }

        // The per-level attempt-run seam: CM-UX-07/level-advance calls this when a new level
        // loads; retries of the same level never reset (that accumulation is the mechanic).
        public void ResetForNewLevel()
        {
        }

        private void Update()
        {
        }
    }
}
