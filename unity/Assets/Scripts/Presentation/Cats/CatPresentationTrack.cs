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

        private short _representedId;
        private bool _departureActive;
        private float _stateStartedAt;

        public CatPresentationState State { get; private set; } = CatPresentationState.Hidden;

        public float StateElapsed { get; private set; }

        /// <summary>
        /// Consumes a copied simulation snapshot. <paramref name="deliveryAdvanced"/> is an
        /// already-derived presentation input; it is never inferred by mutating simulation data.
        /// </summary>
        public void Observe(TrainSlot snapshot, bool deliveryAdvanced, float visualTime)
        {
            float now = float.IsNaN(visualTime) || float.IsInfinity(visualTime) ? 0f : visualTime;
            bool live = snapshot.Id != 0 && snapshot.State != TrainState.None;

            if (live)
            {
                if (_representedId != snapshot.Id || _departureActive || State == CatPresentationState.Hidden)
                {
                    _representedId = snapshot.Id;
                    _departureActive = false;
                    Enter(CatPresentationState.Walk, now);
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
            else if (_representedId != 0 && deliveryAdvanced)
            {
                _departureActive = true;
                Enter(CatPresentationState.Alight, now);
            }
            else
            {
                _representedId = 0;
                Enter(CatPresentationState.Hidden, now);
            }

            StateElapsed = Mathf.Max(0f, now - _stateStartedAt);
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
                    _representedId = 0;
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

        private bool HasElapsed(float now, float duration) => now >= _stateStartedAt + duration;
    }
}
