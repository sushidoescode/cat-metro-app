using CatMetro.Application.Session;
using CatMetro.Presentation.Board;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatMetro.Presentation.Input
{
    // CM-C2b criterion 2: THE one gesture handler — tap only; no drag, no pinch, no
    // long-press-to-aim, no multi-touch (ux-flows S-02; CM-R07.1). The effective hit surface per
    // switch is an expanded disc of at least HIT_DIAMETER_DP (CM-R20.1: >=48dp targets);
    // overlapping discs resolve to the NEAREST CENTER, ties to the lowest switch index —
    // deterministic. Tests drive HandleTapAtScreen directly; play routes through the Input
    // System pointer in Update.
    public sealed class TapInput : MonoBehaviour
    {
        public const float HIT_DIAMETER_DP = 48f;

        private GameSession _session;
        private BoardView _view;
        private Camera _camera;

        public float EffectiveHitDiameterDp => HIT_DIAMETER_DP;

        // CM-C3 criterion 6: Try again is a tap on the bottom thumb band during FailureReview,
        // routed through THIS one handler (S-02's one-gesture discipline holds). Wired by the
        // composition root; hit-testable from the first FailureReview frame by construction.
        public System.Func<bool> RetryRegionActive;
        public System.Action RetryTapped;

        // CM-UX-01: chrome hit routing + the board-input gate. Resolution order is law
        // (criterion 1): legacy retry band first (pinned), registered regions next, board discs
        // last. BoardInputActive gates ONLY the disc scan (null = active; the composition root
        // binds it to the Playing state in CM-UX-07 — until then behavior is unchanged).
        public readonly ChromeRegions Regions = new ChromeRegions();
        public System.Func<bool> BoardInputActive;

        public void Wire(GameSession session, BoardView view, Camera cam)
        {
            _session = session; _view = view; _camera = cam;
        }

        private void Update()
        {
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
                HandleTapAtScreen(pointer.position.ReadValue());
        }

        // Returns the switch index toggled, or -1 (miss). Synchronous: the committed lever
        // visual flips before this frame renders (criterion 3a).
        public int HandleTapAtScreen(Vector2 screenPos)
        {
            if (RetryRegionActive != null && RetryTapped != null && RetryRegionActive()
                && screenPos.y < Screen.height * 0.25f)
            {
                RetryTapped();
                return -2; // the retry verb consumed the tap
            }
            if (_session == null || _view == null || _camera == null) return -1;
            float pxPerDp = Screen.dpi > 0f ? Screen.dpi / 160f : 1f;
            float radiusPx = HIT_DIAMETER_DP * pxPerDp * 0.5f;

            int best = -1;
            float bestDist = float.MaxValue;
            for (int s = 0; s < _view.SwitchCount; s++)
            {
                Vector2 sp = _camera.WorldToScreenPoint(_view.SwitchWorldPos(s));
                float d = Vector2.Distance(sp, screenPos);
                if (d <= radiusPx && d < bestDist) // strict < keeps the lowest index on ties
                {
                    bestDist = d;
                    best = s;
                }
            }
            if (best < 0) return -1;
            _session.EnqueueToggle(best);
            _view.RefreshSwitches(); // the lever shows the committed route THIS frame
            return best;
        }
    }
}
