using System;
using System.Collections.Generic;
using CatMetro.Application.Session;
using CatMetro.Domain;
using UnityEngine;

namespace CatMetro.Presentation.Audio
{
    [Flags]
    public enum GameplayAudioCues : byte
    {
        None = 0,
        Delivery = 1,
        Celebrate = 2,
        WrongStation = 4,
    }

    // Pure edge detector over presentation-readable state. Tests assert state transitions only;
    // clip length, DSP scheduling, and wall-clock timing never enter the gameplay contract.
    public sealed class GameplayAudioCueTracker
    {
        private bool _hasBaseline;
        private int _deliveries;
        private int _rejections;
        private OutcomeKind _outcome;

        public void Rebaseline(int deliveries, int rejections, OutcomeKind outcome)
        {
            _deliveries = deliveries;
            _rejections = rejections;
            _outcome = outcome;
            _hasBaseline = true;
        }

        public GameplayAudioCues Observe(int deliveries, int rejections, OutcomeKind outcome)
        {
            if (!_hasBaseline)
            {
                Rebaseline(deliveries, rejections, outcome);
                return GameplayAudioCues.None;
            }

            GameplayAudioCues cues = GameplayAudioCues.None;
            if (deliveries > _deliveries) cues |= GameplayAudioCues.Delivery;
            if (rejections > _rejections) cues |= GameplayAudioCues.WrongStation;
            if (outcome == OutcomeKind.Won && _outcome != OutcomeKind.Won)
                cues |= GameplayAudioCues.Celebrate;

            _deliveries = deliveries;
            _rejections = rejections;
            _outcome = outcome;
            return cues;
        }
    }

    // Presentation-only audio owner. It reads snapshots and plays local clips; it has no
    // reference to Simulation and no path back into the session or command log.
    public sealed class GameAudio : MonoBehaviour
    {
        public const string ResourceRoot = "Audio/CatMetro/";
        public const int ExpectedClipCount = 7;

        private const float TapVolume = 0.48f;
        private const float SwitchVolume = 0.62f;
        private const float ChuffVolume = 0.22f;
        private const float DeliveryVolume = 0.60f;
        private const float WrongStationVolume = 0.58f;
        private const float CelebrateVolume = 0.56f;
        private const float PurchaseVolume = 0.62f;

        private static AudioListener _activeManagedListener;
        private static readonly List<AudioListener> ManagedListeners =
            new List<AudioListener>();

        private readonly GameplayAudioCueTracker _cueTracker =
            new GameplayAudioCueTracker();

        private AudioSource _oneShotSource;
        private AudioSource _flourishSource;
        private AudioSource _chuffSource;
        private AudioListener _listener;
        private AudioClip _woodTap;
        private AudioClip _switchClunk;
        private AudioClip _trainChuff;
        private AudioClip _deliveryChime;
        private AudioClip _wrongStationThud;
        private AudioClip _celebrateFlourish;
        private AudioClip _purchaseSuccess;
        private bool _enabled = true;
        private bool _applicationPaused;

        public bool Enabled => _enabled;
        public bool ChuffPlaying => _chuffSource != null && _chuffSource.isPlaying;
        public int SnapshotObservationCount { get; private set; }

        public int LoadedClipCount
        {
            get
            {
                int count = 0;
                if (_woodTap != null) count++;
                if (_switchClunk != null) count++;
                if (_trainChuff != null) count++;
                if (_deliveryChime != null) count++;
                if (_wrongStationThud != null) count++;
                if (_celebrateFlourish != null) count++;
                if (_purchaseSuccess != null) count++;
                return count;
            }
        }

        public void Initialize(Camera camera)
        {
            EnsureSources();
            AttachListener(camera);

            _woodTap = LoadClip("wooden-tap");
            _switchClunk = LoadClip("switch-clunk");
            _trainChuff = LoadClip("train-chuff-loop");
            _deliveryChime = LoadClip("delivery-chime");
            _wrongStationThud = LoadClip("wrong-station-thud");
            _celebrateFlourish = LoadClip("celebrate-flourish");
            _purchaseSuccess = LoadClip("purchase-success");

            _chuffSource.clip = _trainChuff;
        }

        public void BindSession(GameSession session)
        {
            StopChuff();
            if (session == null)
            {
                _cueTracker.Rebaseline(0, 0, OutcomeKind.Running);
                return;
            }
            _cueTracker.Rebaseline(session.State.Deliveries, session.State.Rejections,
                session.State.Outcome.Kind);
        }

        // Called explicitly after GameRoot has advanced and painted a snapshot, so ordering is
        // deterministic without relying on MonoBehaviour Update order.
        public void Observe(GameSession session, bool gameplayVisible)
        {
            if (session == null)
            {
                StopChuff();
                return;
            }

            var state = session.State;
            SnapshotObservationCount++;
            GameplayAudioCues cues = _cueTracker.Observe(
                state.Deliveries, state.Rejections, state.Outcome.Kind);

            bool shouldChuff = gameplayVisible
                && state.Outcome.Kind == OutcomeKind.Running
                && HasMovingTrain(state);
            SetChuffPlaying(shouldChuff);

            if (!_enabled || _applicationPaused) return;
            if ((cues & GameplayAudioCues.Delivery) != 0)
                PlayOneShot(_deliveryChime, DeliveryVolume);
            if ((cues & GameplayAudioCues.WrongStation) != 0)
                PlayWrongStationThud();
            if ((cues & GameplayAudioCues.Celebrate) != 0)
                PlayCelebrate();
        }

        public void SetEnabled(bool enabled)
        {
            if (_enabled == enabled) return;
            _enabled = enabled;
            if (!enabled) StopOwnedPlayback();
        }

        public void PlayButtonTap() => PlayOneShot(_woodTap, TapVolume);

        public void PlaySwitchClunk() => PlayOneShot(_switchClunk, SwitchVolume);

        public void PlayWrongStationThud()
        {
            StopChuff();
            PlayOneShot(_wrongStationThud, WrongStationVolume);
        }

        public void PlayPurchaseSuccess() => PlayOneShot(_purchaseSuccess, PurchaseVolume);

        public void StopGameplayLoop() => StopChuff();

        public static bool HasMovingTrain(SimulationState state)
        {
            if (state?.Trains == null) return false;
            for (int i = 0; i < state.Trains.Length; i++)
                if (state.Trains[i].Id != 0
                    && (state.Trains[i].State == TrainState.OnEdge
                        || state.Trains[i].State == TrainState.OnEdgeReverse))
                    return true;
            return false;
        }

        private void EnsureSources()
        {
            if (_oneShotSource == null)
                _oneShotSource = MakeSource(loop: false, priority: 128);
            if (_flourishSource == null)
                _flourishSource = MakeSource(loop: false, priority: 128);
            if (_chuffSource == null)
                _chuffSource = MakeSource(loop: true, priority: 160);
        }

        private AudioSource MakeSource(bool loop, int priority)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = priority;
            source.volume = 1f;
            return source;
        }

        private void AttachListener(Camera camera)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            _listener = camera.GetComponent<AudioListener>();
            if (_listener == null) _listener = camera.gameObject.AddComponent<AudioListener>();

            if (_activeManagedListener != null && _activeManagedListener != _listener)
                _activeManagedListener.enabled = false;
            ManagedListeners.Remove(_listener);
            ManagedListeners.Add(_listener);
            _listener.enabled = true;
            _activeManagedListener = _listener;
        }

        private static AudioClip LoadClip(string name)
        {
            var clip = Resources.Load<AudioClip>(ResourceRoot + name);
            if (clip == null)
                Debug.LogWarning("audio clip unavailable: " + ResourceRoot + name);
            return clip;
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (!_enabled || _applicationPaused || clip == null || _oneShotSource == null) return;
            _oneShotSource.PlayOneShot(clip, volume);
        }

        private void PlayCelebrate()
        {
            if (_celebrateFlourish == null || _flourishSource == null) return;
            _flourishSource.Stop();
            _flourishSource.clip = _celebrateFlourish;
            _flourishSource.volume = CelebrateVolume;
            _flourishSource.PlayDelayed(0.16f);
        }

        private void SetChuffPlaying(bool shouldPlay)
        {
            if (!_enabled || _applicationPaused || !shouldPlay || _trainChuff == null)
            {
                StopChuff();
                return;
            }
            if (_chuffSource.isPlaying) return;
            _chuffSource.clip = _trainChuff;
            _chuffSource.volume = ChuffVolume;
            _chuffSource.Play();
        }

        private void StopChuff()
        {
            if (_chuffSource != null) _chuffSource.Stop();
        }

        private void StopOwnedPlayback()
        {
            if (_oneShotSource != null) _oneShotSource.Stop();
            if (_flourishSource != null) _flourishSource.Stop();
            StopChuff();
        }

        private void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            if (paused) StopOwnedPlayback();
        }

        // Android notification ducking is deliberately not inferred from
        // OnApplicationFocus: it is not an audio-focus callback. Unity's Android audio-focus
        // request is enabled by PlayerSettings.muteOtherAudioSources, allowing the OS to apply
        // its synchronized transient duck/restore behavior; only a real device can verify it.

        private void OnDisable() => StopOwnedPlayback();

        private void OnDestroy()
        {
            StopOwnedPlayback();
            ManagedListeners.Remove(_listener);
            if (_activeManagedListener != _listener) return;

            _activeManagedListener = null;
            for (int i = ManagedListeners.Count - 1; i >= 0; i--)
            {
                var candidate = ManagedListeners[i];
                if (candidate == null)
                {
                    ManagedListeners.RemoveAt(i);
                    continue;
                }
                candidate.enabled = true;
                _activeManagedListener = candidate;
                break;
            }
        }
    }
}
