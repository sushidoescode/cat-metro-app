using System;
using System.Collections.Generic;
using CatMetro.Application.Session;
using CatMetro.Domain;
using CatMetro.Presentation.Cats;
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
        Failed = 8,
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
            if (outcome == OutcomeKind.Failed && _outcome != OutcomeKind.Failed)
                cues |= GameplayAudioCues.Failed;

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
        public const int ExpectedClipCount = 18;
        private static readonly int[] DeliverySteps = { 0, 2, 4, 7, 9, 12 };
        public static float DeliveryPitch(int deliveries) =>
            Mathf.Pow(2f, DeliverySteps[Mathf.Clamp(deliveries - 1, 0, 5)] / 12f);

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

        private readonly AudioSource[] _voices = new AudioSource[4];
        private readonly AudioClip[] _mews = new AudioClip[5];
        private readonly AudioClip[] _purrs = new AudioClip[2];
        private int _nextVoice;
        private int _nextMew;
        private int _boarded;
        private float _chuffRelease = -1f;
        private float _chuffReleaseVolume;
        private float _homeElapsed;
        private float _homeInterval = HomeInterval(0);
        private int _homeBeat;
        private AudioClip _grumble;
        private AudioClip _failSting;
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
        private float _celebrateAt = -1f;
        public bool CelebratePending => _celebrateAt >= 0f;

        public bool Enabled => _enabled;
        public bool ChuffPlaying => _chuffSource != null && _chuffSource.isPlaying;
        public int SnapshotObservationCount { get; private set; }

        public int LoadedClipCount { get; private set; }

        public void Initialize(Camera camera)
        {
            EnsureSources();
            StopOwnedPlayback();
            AttachListener(camera);
            LoadedClipCount = 0;

            _woodTap = LoadClip("wooden-tap");
            _switchClunk = LoadClip("switch-clunk");
            _trainChuff = LoadClip("train-chuff-loop");
            _deliveryChime = LoadClip("delivery-chime");
            _wrongStationThud = LoadClip("wrong-station-thud");
            _celebrateFlourish = LoadClip("celebrate-flourish");
            _purchaseSuccess = LoadClip("purchase-success");
            _trainChuff = LoadClip("train-chuff-96") ?? _trainChuff;
            _celebrateFlourish = LoadClip("win-cadence") ?? _celebrateFlourish;
            _failSting = LoadClip("fail-sting") ?? _wrongStationThud;
            _grumble = LoadClip("cat-grumble") ?? _wrongStationThud;
            for (int i = 0; i < _mews.Length; i++)
                _mews[i] = LoadClip("cat-mew-" + (i + 1)) ?? _deliveryChime;
            for (int i = 0; i < _purrs.Length; i++)
                _purrs[i] = LoadClip("cat-purr-" + (i + 1)) ?? _woodTap;

            _chuffSource.clip = _trainChuff;
        }

        public void BindSession(GameSession session)
        {
            StopOwnedPlayback();
            _boarded = BoardedCount(session);
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
            int boarded = BoardedCount(session);
            bool catBoarded = boarded > _boarded;
            _boarded = boarded;

            bool shouldChuff = gameplayVisible
                && state.Outcome.Kind == OutcomeKind.Running
                && HasMovingTrain(state);
            SetChuffPlaying(shouldChuff);

            if (!_enabled || _applicationPaused) return;
            if (catBoarded && gameplayVisible) PlayMew();
            if ((cues & GameplayAudioCues.Delivery) != 0)
            {
                PlayOneShot(_deliveryChime, DeliveryVolume, DeliveryPitch(state.Deliveries));
                if ((cues & GameplayAudioCues.Celebrate) == 0) PlayMew(3);
            }
            if ((cues & GameplayAudioCues.WrongStation) != 0)
                PlayWrongStationThud();
            if ((cues & GameplayAudioCues.Celebrate) != 0)
                PlayCelebrate();
            if ((cues & GameplayAudioCues.Failed) != 0)
                PlayCadence(_failSting, .52f);
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
            PlayOneShot(_grumble, .40f);
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
            for (int i = 0; i < _voices.Length; i++)
                if (_voices[i] == null) _voices[i] = MakeSource(loop: false, priority: 128);
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

        private AudioClip LoadClip(string name)
        {
            var clip = Resources.Load<AudioClip>(ResourceRoot + name);
            if (clip == null)
                Debug.LogWarning("audio clip unavailable: " + ResourceRoot + name);
            else LoadedClipCount++;
            return clip;
        }

        private void PlayOneShot(AudioClip clip, float volume, float pitch = 0f, float delay = 0f)
        {
            if (!_enabled || _applicationPaused || !isActiveAndEnabled || clip == null) return;
            var source = _voices[_nextVoice++ % _voices.Length];
            if (source == null) return;
            source.Stop();
            source.clip = clip;
            source.pitch = pitch > 0f ? pitch : UnityEngine.Random.Range(.96f, 1.04f);
            source.volume = volume;
            if (delay > 0f) source.PlayScheduled(AudioSettings.dspTime + delay);
            else source.Play();
        }

        private void PlayCelebrate()
        {
            if (_celebrateFlourish == null || _flourishSource == null) return;
            _flourishSource.Stop();
            _flourishSource.clip = _celebrateFlourish;
            _flourishSource.volume = CelebrateVolume;
            _celebrateAt = Time.unscaledTime + CatPresentationTrack.WinBeatDelay;
            for (int i = 0; i < 3; i++) PlayMew(i * 2, i * .08f);
        }

        private void PlayCadence(AudioClip clip, float volume)
        {
            if (clip == null || _flourishSource == null) return;
            _flourishSource.Stop();
            _flourishSource.clip = clip;
            _flourishSource.volume = volume;
            _flourishSource.Play();
        }

        private void PlayMew(int semitones = 0, float delay = 0f) =>
            PlayOneShot(_mews[_nextMew++ % _mews.Length], .43f,
                Mathf.Pow(2f, semitones / 12f) * UnityEngine.Random.Range(.96f, 1.04f), delay);

        // Use the rig's presentation blink clock, never the simulation RNG or game ticks.
        public void ObserveHome(bool visible, float elapsedSeconds)
        {
            if (!visible || !_enabled || _applicationPaused) { _homeElapsed = 0f; return; }
            _homeElapsed += Mathf.Max(0f, elapsedSeconds);
            if (_homeElapsed < _homeInterval) return;
            _homeElapsed = 0f;
            PlayMew();
            if (++_homeBeat % 3 == 0) PlayOneShot(_purrs[_homeBeat % 2], .3f, delay: .4f);
            _homeInterval = HomeInterval(_homeBeat);
        }

        private static float HomeInterval(int beat)
        {
            var clock = new CatMicroMotion((uint)(27 + beat));
            return Mathf.Lerp(8f, 14f, Mathf.InverseLerp(CatMicroMotion.BlinkIntervalMinimum,
                CatMicroMotion.BlinkIntervalMaximum, clock.BlinkInterval));
        }

        public static int BoardedCount(GameSession session)
        {
            if (session == null) return 0;
            int count = 0;
            for (int i = 0; i < session.State.Trains.Length; i++)
                count += session.TrainOccupantGeneration(i);
            return count;
        }

        private void SetChuffPlaying(bool shouldPlay)
        {
            if (!_enabled || _applicationPaused || !shouldPlay || _trainChuff == null)
            {
                StopChuff();
                return;
            }
            _chuffRelease = -1f;
            if (_chuffSource.isPlaying) { _chuffSource.volume = ChuffVolume; return; }
            _chuffSource.clip = _trainChuff;
            _chuffSource.volume = ChuffVolume;
            _chuffSource.Play();
        }

        private void StopChuff()
        {
            if (_chuffSource == null || !_chuffSource.isPlaying || _chuffRelease >= 0f) return;
            _chuffRelease = 0f;
            _chuffReleaseVolume = _chuffSource.volume;
        }

        public void CancelCelebrate()
        {
            _celebrateAt = -1f;
            if (_flourishSource != null) _flourishSource.Stop();
        }

        private void Update()
        {
            if (CelebratePending && Time.unscaledTime >= _celebrateAt)
            {
                _celebrateAt = -1f;
                if (_enabled && !_applicationPaused && _flourishSource != null)
                    _flourishSource.Play();
            }
            if (_chuffRelease < 0f || _chuffSource == null) return;
            _chuffRelease += Time.unscaledDeltaTime;
            _chuffSource.volume = _chuffReleaseVolume * Mathf.Clamp01(1f - _chuffRelease / .09f);
            if (_chuffRelease < .09f) return;
            _chuffSource.Stop();
            _chuffRelease = -1f;
        }

        private void StopOwnedPlayback()
        {
            CancelCelebrate();
            foreach (var voice in _voices) if (voice != null) voice.Stop();
            if (_flourishSource != null) _flourishSource.Stop();
            if (_chuffSource != null) { _chuffSource.Stop(); _chuffSource.volume = 0f; }
            _chuffRelease = -1f;
        }

        private void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            if (paused) StopOwnedPlayback();
        }

        // Do not interpret OnApplicationFocus as Android audio focus. MusicDirector handles
        // the boot courtesy check; every owned source stops on a real application pause.

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
