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
            _screenState = screenState;
            EnsureViews();
            Apply();
        }

        // The per-level attempt-run seam: CM-UX-07/level-advance calls this when a new level
        // loads; retries of the same level never reset (that accumulation is the mechanic).
        public void ResetForNewLevel()
        {
            _attempts.Reset();
            if (Chip != null) Chip.SetVisible(false);
        }

        private void Update()
        {
            if (_screenState != null) Apply();
        }

        private void EnsureViews()
        {
            if (_canvas != null) return;
            var go = new GameObject("HintChipCanvas");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            // ART-EVIDENCE canvas unification (PR #68 round-2 review E-1): ScreenSpaceOverlay,
            // unconditionally — see ScreenChromeController.EnsureViews for the full rationale
            // (mixed render modes made the sortingOrder ladder non-authoritative; the capture
            // path now reads the composited back buffer, which sees Overlay content).
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // A-UX5-3: below the CM-UX-02 chrome canvas (100) — the halt veil overlays the
            // chip if they are ever co-visible; the visibility law hides it on Halted anyway.
            _canvas.sortingOrder = 90;
            Chip = HintChipView.Create(_canvas.transform);
        }

        private void Apply()
        {
            var state = _screenState();
            _attempts.Observe(state);
            bool visible = _attempts.Count >= ChipVisibleAfterEntries
                && (state == "Playing" || state == HintAttemptCounter.CountedState);
            if (Chip != null) Chip.SetVisible(visible);
        }
    }
}
