using System;
using UnityEngine;

namespace CatMetro.Presentation.Haptics
{
    // Android JNI stays here; the rest of presentation uses IHaptics on every platform.
    public sealed class AndroidHaptics : IHaptics
    {
        public static IHaptics Create()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { return new AndroidHaptics(); }
            catch (Exception ex) { Debug.LogWarning("haptics unavailable: " + ex.GetType().Name); }
#endif
            return new NullHaptics();
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _vibrator;
        private readonly int _sdk;
        private bool _unavailable;
        private AndroidHaptics()
        {
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            _sdk = version.GetStatic<int>("SDK_INT");
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            if (_sdk >= 31)
            {
                using var manager = activity.Call<AndroidJavaObject>("getSystemService", "vibrator_manager");
                _vibrator = manager.Call<AndroidJavaObject>("getDefaultVibrator");
            }
            else _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            _unavailable = _vibrator == null || !_vibrator.Call<bool>("hasVibrator");
        }
        public void Play(HapticCue cue)
        {
            if (_unavailable || _vibrator == null) return;
            try
            {
                var pattern = HapticPattern.For(cue);
                if (_sdk >= 26)
                {
                    using var effects = new AndroidJavaClass("android.os.VibrationEffect");
                    using var effect = _sdk >= 29 && pattern.Predefined >= 0
                        ? effects.CallStatic<AndroidJavaObject>("createPredefined", pattern.Predefined)
                        : pattern.Timings.Length == 2
                            ? effects.CallStatic<AndroidJavaObject>("createOneShot", pattern.Timings[1], pattern.Amplitudes[1])
                            : effects.CallStatic<AndroidJavaObject>("createWaveform", pattern.Timings, pattern.Amplitudes, -1);
                    _vibrator.Call("vibrate", effect);
                }
                // VibrationEffect was introduced in API 26; API 25 uses the legacy waveform.
                else _vibrator.Call("vibrate", pattern.Timings, -1);
            }
            catch (Exception ex)
            {
                _unavailable = true;
                Debug.LogWarning("haptics disabled: " + ex.GetType().Name);
            }
        }
        public void Cancel()
        {
            if (_vibrator == null) return;
            try { _vibrator.Call("cancel"); }
            catch (Exception) { _unavailable = true; }
        }
        public void Dispose() { Cancel(); _vibrator?.Dispose(); _vibrator = null; }
#else
        private AndroidHaptics() { }
        public void Play(HapticCue cue) { }
        public void Cancel() { }
        public void Dispose() { }
#endif
    }
}
