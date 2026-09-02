using CatMetro.Domain;
using UnityEngine;

namespace CatMetro.Presentation.Cats
{
    // Visual-only state. A track receives TrainSlot VALUES and owns no simulation reference,
    // so its short phases cannot change a tick, replay, or delivery outcome.
    public enum CatPresentationState
    {
        Hidden,
        WaitingIdle,
        Walk,
        Board,
        RideIdle,
        Alight,
        Celebrate,
    }

    public sealed class CatPresentationTrack
    {
        public const float SpawnWalkDuration = 0.22f;
        public const float BoardDuration = 0.18f;
        public const float AlightDuration = 0.18f;
        public const float DeliveryWalkDuration = 0.28f;
        public const float CelebrateDuration = 0.48f;
        private const float BoardStartBlend = 0.35f;
        private const float DeliveryWalkMinimumBlend = 0.45f;
        // Absolute presentation-clock tolerance. Decimal phase endpoints such as 10.22 + 0.18
        // differ by about one microsecond after float32 rounding; the named duration still ends
        // at that endpoint, never one rendered frame later.
        private const float PhaseBoundaryTolerance = 0.00001f;

        private int _representedOccupantGeneration;
        private bool _departureActive;
        private bool _waitingOnPlatform;
        private float _departureAlightStartBlend;
        private float _departureWalkStartBlend;
        private float _stateStartedAt;

        public CatPresentationState State { get; private set; } = CatPresentationState.Hidden;

        public float StateElapsed { get; private set; }

        /// <summary>
        /// Presentation-only path from the carriage seat (0) to the adjacent platform (1).
        /// The simulation root keeps moving authoritatively while the visual cat follows this
        /// short local path; this value is never consumed by gameplay.
        /// </summary>
        public float PlatformBlend { get; private set; }

        /// <summary>
        /// Nonnegative presentation-path blend units per second while walking. BoardView passes
        /// this read-only rate to the view, which measures the actual seat/platform path in board
        /// units (including queue-lane displacement). It never changes simulation timing.
        /// </summary>
        public float PlatformBlendSpeed
        {
            get
            {
                if (State != CatPresentationState.Walk) return 0f;
                return _departureActive
                    ? Mathf.Max(0f, 1f - _departureWalkStartBlend) / DeliveryWalkDuration
                    : (1f - BoardStartBlend) / SpawnWalkDuration;
            }
        }

        /// <summary>
        /// True only for the alight/walk/celebrate half of the path. The view uses it to face
        /// the rig's +X presentation-forward axis along travel; gameplay never consumes it.
        /// </summary>
        public bool MovingToPlatform => _departureActive;

        /// <summary>
        /// Consumes a copied simulation snapshot plus the presentation-owned generation for
        /// that fixed Domain slot. <paramref name="deliveryAdvanced"/> is an already-derived
        /// presentation input; neither input is inferred by mutating simulation data.
        /// </summary>
        public void Observe(TrainSlot snapshot, int occupantGeneration,
            bool deliveryAdvanced, float visualTime) =>
            Observe(snapshot, occupantGeneration, deliveryAdvanced, visualTime, false);

        public void Observe(TrainSlot snapshot, int occupantGeneration,
            bool deliveryAdvanced, float visualTime, bool waitingOnPlatform)
        {
            float now = float.IsNaN(visualTime) || float.IsInfinity(visualTime) ? 0f : visualTime;
            bool live = snapshot.Id != 0 && snapshot.State != TrainState.None;

            if (live)
            {
                if (_representedOccupantGeneration != occupantGeneration
                    || _departureActive || State == CatPresentationState.Hidden)
                {
                    _representedOccupantGeneration = occupantGeneration;
                    _departureActive = false;
                    _waitingOnPlatform = waitingOnPlatform;
                    Enter(waitingOnPlatform
                        ? CatPresentationState.WaitingIdle : CatPresentationState.Walk, now);
                }
                else if (_waitingOnPlatform != waitingOnPlatform)
                {
                    _waitingOnPlatform = waitingOnPlatform;
                    Enter(waitingOnPlatform
                        ? CatPresentationState.WaitingIdle : CatPresentationState.Walk, now);
                }
                else
                {
                    AdvanceLive(snapshot.State, now);
                }
            }
            else if (_departureActive)
            {
                AdvanceDeparture(now);
            }
            else if (_representedOccupantGeneration != 0 && deliveryAdvanced)
            {
                // Delivery can arrive before the cosmetic spawn/board sequence finishes on a
                // short edge. Reverse from the last rendered path coordinate instead of
                // snapping to the seat; the simulation event still starts departure now.
                _departureAlightStartBlend = Mathf.Clamp01(PlatformBlend);
                _departureWalkStartBlend = Mathf.Max(
                    DeliveryWalkMinimumBlend, _departureAlightStartBlend);
                _waitingOnPlatform = false;
                _departureActive = true;
                Enter(CatPresentationState.Alight, now);
            }
            else
            {
                _representedOccupantGeneration = 0;
                _waitingOnPlatform = false;
                Enter(CatPresentationState.Hidden, now);
            }

            StateElapsed = Mathf.Max(0f, now - _stateStartedAt);
            PlatformBlend = ResolvePlatformBlend();
        }

        private float ResolvePlatformBlend()
        {
            switch (State)
            {
                case CatPresentationState.Walk:
                    return _departureActive
                        ? Mathf.Lerp(_departureWalkStartBlend, 1f,
                            Mathf.Clamp01(StateElapsed / DeliveryWalkDuration))
                        : Mathf.Lerp(1f, BoardStartBlend,
                            Mathf.Clamp01(StateElapsed / SpawnWalkDuration));
                case CatPresentationState.Board:
                    return Mathf.Lerp(BoardStartBlend, 0f,
                        Mathf.Clamp01(StateElapsed / BoardDuration));
                case CatPresentationState.Alight:
                    return Mathf.Lerp(_departureAlightStartBlend, _departureWalkStartBlend,
                        Mathf.Clamp01(StateElapsed / AlightDuration));
                case CatPresentationState.Celebrate:
                    return 1f;
                case CatPresentationState.WaitingIdle:
                    return _waitingOnPlatform ? 1f : 0f;
                default:
                    return 0f;
            }
        }

        private void AdvanceLive(byte simulationState, float now)
        {
            // A catch-up frame may cross more than one presentation boundary. Carrying the
            // entered timestamp forward preserves state-local elapsed time rather than making a
            // missed frame restart a phase.
            while (true)
            {
                if (State == CatPresentationState.Walk
                    && HasElapsed(now, SpawnWalkDuration))
                {
                    Enter(CatPresentationState.Board, _stateStartedAt + SpawnWalkDuration);
                    continue;
                }

                if (State == CatPresentationState.Board
                    && HasElapsed(now, BoardDuration))
                {
                    Enter(SettledState(simulationState), _stateStartedAt + BoardDuration);
                    continue;
                }

                CatPresentationState settled = SettledState(simulationState);
                if ((State == CatPresentationState.RideIdle || State == CatPresentationState.WaitingIdle)
                    && State != settled)
                    Enter(settled, now);
                return;
            }
        }

        private void AdvanceDeparture(float now)
        {
            while (true)
            {
                if (State == CatPresentationState.Alight
                    && HasElapsed(now, AlightDuration))
                {
                    Enter(CatPresentationState.Walk, _stateStartedAt + AlightDuration);
                    continue;
                }

                if (State == CatPresentationState.Walk
                    && HasElapsed(now, DeliveryWalkDuration))
                {
                    Enter(CatPresentationState.Celebrate, _stateStartedAt + DeliveryWalkDuration);
                    continue;
                }

                if (State == CatPresentationState.Celebrate
                    && HasElapsed(now, CelebrateDuration))
                {
                    _departureActive = false;
                    _representedOccupantGeneration = 0;
                    Enter(CatPresentationState.Hidden, _stateStartedAt + CelebrateDuration);
                }
                return;
            }
        }

        private static CatPresentationState SettledState(byte simulationState) =>
            simulationState == TrainState.OnEdge
                ? CatPresentationState.RideIdle
                : CatPresentationState.WaitingIdle;

        private void Enter(CatPresentationState state, float startedAt)
        {
            State = state;
            _stateStartedAt = startedAt;
            StateElapsed = 0f;
        }

        private bool HasElapsed(float now, float duration) =>
            Mathf.Max(0f, now - _stateStartedAt) + PhaseBoundaryTolerance >= duration;
    }
}
