using UnityEngine;
using CatMetro.Presentation.Audio;

namespace CatMetro.Presentation.Hud
{
    // CM-UX-02 criterion 2: the state-bound chrome controller. Attached by the composition
    // root in CM-UX-07 (forward obligation: the SAME PR must bind TapInput.BoardInputActive);
    // tests attach directly over real wiring. State map: FailureReview -> CTA · Halted -> veil
    // · Playing/Won -> neither (CM-UX-04 MAY extend Won to its results panel and may NOT relax
    // the other rows — bounded supersession). Renders react to the state source WITHIN ONE
    // PUMPED FRAME of a change (P-6 language; the map is applied by polling in Update, never
    // eagerly at Attach alone).
    public sealed class ScreenChromeController : MonoBehaviour
    {
        private System.Func<string> _screenState;
        private Canvas _canvas;
        private UiAudioManager _audio;
        private string _previousState;

        public RetryCtaView Cta { get; private set; }
        public HaltVeilView Veil { get; private set; }

        // R1-H2: the whole chrome tree hangs off this root — tests walk IT, never a subtree.
        public GameObject ChromeRoot => _canvas != null ? _canvas.gameObject : null;

        public void Attach(System.Func<string> screenState)
        {
            _screenState = screenState;
            EnsureViews();
            _previousState = _screenState();
            Apply();
        }

        private void Update()
        {
            if (_screenState != null) Apply();
        }

        private void EnsureViews()
        {
            if (_canvas != null) return;
            var go = new GameObject("ChromeCanvas");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            // Screen Space - Camera when the wired camera exists (renders into capture RTs —
            // the #33 visual-verification rule needs real frames); overlay only as a fallback.
            var cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceCamera;
                _canvas.worldCamera = cam;
                _canvas.planeDistance = 1f;
            }
            else
            {
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            _canvas.sortingOrder = 100;
            Cta = RetryCtaView.Create(_canvas.transform);
            Veil = HaltVeilView.Create(_canvas.transform);
            _audio = UiAudioManager.Ensure(transform);
        }

        private void Apply()
        {
            var state = _screenState();
            if (_previousState != null && state != _previousState)
            {
                // F-6 (round-1 review): explicit null checks, not `?.` — a destroyed
                // UiAudioManager is Unity fake-null (its overloaded `==` reports null, but the
                // C# reference the `?.` operator sees is not), so `?.` would still dispatch
                // into it. Audio never gates a visual state either way; this only changes how
                // the null case is detected.
                if (state == "FailureReview")
                {
                    if (_audio != null) _audio.PlayWarning();
                }
                else if (_previousState == "FailureReview" && state == "Playing")
                {
                    if (_audio != null) _audio.PlayTap();
                }
            }
            if (Cta != null) Cta.SetVisible(state == "FailureReview");
            if (Veil != null) Veil.SetVisible(state == "Halted");
            _previousState = state;
        }
    }
}
