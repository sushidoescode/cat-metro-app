using System;
using CatMetro.Application.Session;
using UnityEngine;

namespace CatMetro.Presentation.Audio
{
    public enum MusicScene { Home, Playing, Won, Failed, Quiet }

    public sealed class MusicDirector : MonoBehaviour
    {
        public const int BeatsPerMinute = 96;
        private static readonly string[] ClipNames = { "bed", "shaker", "melody", "sparkle", "home" };
        private readonly AudioSource[] _sources = new AudioSource[5];
        private readonly AudioLowPassFilter[] _filters = new AudioLowPassFilter[5];
        private bool _initialized;
        private bool _preference = true;
        private bool _paused;
        private bool _scheduled;
        private bool _moving;
        private int _delivered;
        private int _goal;
        private float _stateStarted;
        private MusicScene _scene = MusicScene.Home;

        public bool Enabled => _preference && !SuppressedForOtherMusic;
        public bool SuppressedForOtherMusic { get; private set; }
        public double ScheduledStartDspTime { get; private set; }
        public int LoadedClipCount { get; private set; }
        public int SnapshotObservationCount { get; private set; }
        public MusicScene Scene => _scene;

        public void Initialize(Func<bool> otherMusicActive = null)
        {
            if (_initialized) return;
            // Query before any owned source plays. Unity must not take audio focus at boot,
            // otherwise the other app is already paused by the time isMusicActive is read.
            SuppressedForOtherMusic = (otherMusicActive ?? AndroidMusicActivity.IsActive)();
            for (int i = 0; i < _sources.Length; i++)
            {
                var go = new GameObject("Music-" + ClipNames[i]);
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                source.priority = 96;
                source.volume = 0f;
                source.clip = Resources.Load<AudioClip>(GameAudio.ResourceRoot + "music/" + ClipNames[i]);
                if (source.clip != null) LoadedClipCount++;
                else Debug.LogWarning("Music stem unavailable: " + ClipNames[i]);
                _sources[i] = source;
                _filters[i] = go.AddComponent<AudioLowPassFilter>();
                _filters[i].cutoffFrequency = 22000f;
                _filters[i].lowpassResonanceQ = 1f;
            }
            _initialized = true;
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            ScheduleTogether();
        }

        // Boot/persistence refresh does not overwrite the temporary own-music courtesy mute.
        public void ApplyPreference(bool enabled)
        {
            _preference = enabled;
            if (!Enabled) Silence();
        }

        public void SetEnabled(bool enabled)
        {
            SuppressedForOtherMusic = false;
            ApplyPreference(enabled);
        }

        public void Observe(GameSession session, MusicScene scene)
        {
            SnapshotObservationCount++;
            if (_scene != scene)
            {
                _scene = scene;
                _stateStarted = Time.unscaledTime;
            }
            var state = session?.State;
            _moving = GameAudio.HasMovingTrain(state);
            _delivered = state?.Deliveries ?? 0;
            _goal = state?.Graph.WinDeliveries ?? 0;
        }

        private void Update()
        {
            if (!_initialized || _paused) return;
            if (!_scheduled) ScheduleTogether();
            float elapsed = Time.unscaledTime - _stateStarted;
            Vector4 targets = Enabled ? TargetsFor(_scene, _moving, _delivered, _goal, elapsed) : Vector4.zero;
            bool home = _scene == MusicScene.Home;
            float cutoff = LowPassFor(_scene, elapsed);
            for (int i = 0; i < _sources.Length; i++)
            {
                float target = i == 4 ? (home ? targets.x : 0f)
                    : i == 0 ? (home ? 0f : targets.x) : targets[i];
                _sources[i].volume = Fade(_sources[i].volume, target, Time.unscaledDeltaTime);
                _filters[i].cutoffFrequency = cutoff;
            }
        }

        private void ScheduleTogether()
        {
            if (!_initialized || _paused || !isActiveAndEnabled) return;
            ScheduledStartDspTime = AudioSettings.dspTime + .15d;
            for (int i = 0; i < _sources.Length; i++)
            {
                _sources[i].Stop();
                _sources[i].volume = 0f;
                if (_sources[i].clip != null) _sources[i].PlayScheduled(ScheduledStartDspTime);
            }
            _scheduled = true;
        }

        private void Silence()
        {
            for (int i = 0; i < _sources.Length; i++)
                if (_sources[i] != null) _sources[i].volume = 0f;
        }

        private void StopPlayback()
        {
            Silence();
            for (int i = 0; i < _sources.Length; i++)
                if (_sources[i] != null) _sources[i].Stop();
            _scheduled = false;
        }

        private void OnApplicationPause(bool paused)
        {
            _paused = paused;
            if (paused) StopPlayback();
            else ScheduleTogether();
        }
        private void OnAudioConfigurationChanged(bool deviceChanged) => ScheduleTogether();
        private void OnEnable() { if (_initialized) ScheduleTogether(); }
        private void OnDisable() => StopPlayback();
        private void OnDestroy()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            StopPlayback();
        }

        public static Vector4 TargetsFor(MusicScene scene, bool moving, int delivered,
            int goal, float outcomeSeconds)
        {
            switch (scene)
            {
                case MusicScene.Home: return new Vector4(.5f, 0f, 0f, 0f);
                case MusicScene.Playing:
                    return new Vector4(.7f, moving ? 1f : 0f, delivered > 0 ? 1f : 0f,
                        goal > 0 && delivered > 0 && delivered >= goal - 1 ? 1f : 0f);
                case MusicScene.Won:
                    return new Vector4(.7f, 0f, 1f, 1f) * (outcomeSeconds < 1.2f ? .35f : 1f);
                case MusicScene.Failed: return new Vector4(.5f, 0f, 0f, 0f);
                default: return Vector4.zero;
            }
        }
        public static float LowPassFor(MusicScene scene, float outcomeSeconds) =>
            scene == MusicScene.Failed ? Mathf.Lerp(22000f, 700f, outcomeSeconds / .4f) : 22000f;
        public static float Fade(float current, float target, float seconds) =>
            Mathf.Lerp(current, target, 1f - Mathf.Exp(-Mathf.Max(0f, seconds) / .6f));

    }

    internal static class AndroidMusicActivity
    {
        public static bool IsActive()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var manager = activity.Call<AndroidJavaObject>("getSystemService", "audio");
                return manager != null && manager.Call<bool>("isMusicActive");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Other music query unavailable: " + ex.GetType().Name);
            }
#endif
            return false;
        }
    }
}
