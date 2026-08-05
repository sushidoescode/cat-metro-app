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
        // R1-F3: registrations OUTLIVE Retry()'s view rebuild — TapInput persists while views
        // are destroyed and rebuilt, so every registering owner must Unregister in OnDestroy
        // (a live provider over a destroyed view throws on the next tap otherwise).
        public readonly ChromeRegions Regions = new ChromeRegions();
        public System.Func<bool> BoardInputActive;

        // R1-F1: the disc-resolution LAW as pure math, pinned resolution-independent in
        // EditMode — nearest center wins, an exact tie goes to the LOWEST index, outside the
        // radius is a miss. HandleTapAtScreen routes through this exact function.
        public static int ResolveNearestDisc(Vector2 screenPos, Vector2[] centers, float radiusPx)
        {
            int best = -1;
            float bestDist = float.MaxValue;
            for (int s = 0; s < centers.Length; s++)
            {
                float d = Vector2.Distance(centers[s], screenPos);
                if (d <= radiusPx && d < bestDist) // strict < keeps the lowest index on ties
                {
                    bestDist = d;
                    best = s;
                }
            }
            return best;
        }

        private Vector2[] _discCentersScratch; // sized at Wire; refilled per tap — no tap alloc

        public void Wire(GameSession session, BoardView view, Camera cam)
        {
            _session = session; _view = view; _camera = cam;
            _discCentersScratch = new Vector2[view != null ? view.SwitchCount : 0];
        }

        private void Update()
        {
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
                HandleTapAtScreen(pointer.position.ReadValue());
        }

        // Returns the switch index toggled, -2 (retry verb), -3 (chrome region), or -1 (miss).
        // Synchronous: the committed lever visual flips before this frame renders (criterion 3a).
        // CM-UX-01 resolution order is LAW: retry band (pinned, first claim) -> chrome regions ->
        // board discs; BoardInputActive gates only the disc scan.
        public int HandleTapAtScreen(Vector2 screenPos)
        {
            if (RetryRegionActive != null && RetryTapped != null && RetryRegionActive()
                && screenPos.y < Screen.height * 0.25f)
            {
                RetryTapped();
                return -2; // the retry verb consumed the tap
            }
            if (Regions.TryResolve(screenPos, out var onTap))
            {
                onTap();
                return -3; // a chrome region consumed the tap — never falls through to a disc
            }
            // Regions resolve above this line: chrome exists on screens with no board wired
            // (a later Home screen), and a closed gate must never darken chrome or retry.
            if (BoardInputActive != null && !BoardInputActive()) return -1;
            if (_session == null || _view == null || _camera == null) return -1;
            float pxPerDp = Screen.dpi > 0f ? Screen.dpi / 160f : 1f;
            float radiusPx = HIT_DIAMETER_DP * pxPerDp * 0.5f;

            for (int s = 0; s < _view.SwitchCount; s++)
                _discCentersScratch[s] = _camera.WorldToScreenPoint(_view.SwitchWorldPos(s));
            int best = ResolveNearestDisc(screenPos, _discCentersScratch, radiusPx);
            if (best < 0) return -1;
            _session.EnqueueToggle(best);
            _view.RefreshSwitches(); // the lever shows the committed route THIS frame
            return best;
        }
    }
}
