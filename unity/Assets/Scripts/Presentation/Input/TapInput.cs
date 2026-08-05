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
