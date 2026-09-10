using System;
using CatMetro.Application.Session;
using CatMetro.Domain;
using CatMetro.Presentation.Audio;
using UnityEngine;

namespace CatMetro.Presentation.Haptics
{
    public enum HapticCue { Tap, Switch, OverParSwitch, Delivery, WrongStation, Win, Fail, Purchase }
    public interface IHaptics : IDisposable
    {
        void Play(HapticCue cue);
        void Cancel();
    }
    public sealed class NullHaptics : IHaptics
    {
        public void Play(HapticCue cue) { }
        public void Cancel() { }
        public void Dispose() { }
    }
    public readonly struct HapticPattern
    {
        public HapticPattern(long[] timings, int[] amplitudes, int predefined = -1)
        { Timings = timings; Amplitudes = amplitudes; Predefined = predefined; }
        public long[] Timings { get; }
        public int[] Amplitudes { get; }
        public int Predefined { get; }
        public static HapticPattern For(HapticCue cue)
        {
            switch (cue)
            {
                case HapticCue.Tap: return new HapticPattern(new long[] { 0, 8 }, new[] { 0, 70 }, 2);
                case HapticCue.Switch: return new HapticPattern(new long[] { 0, 18 }, new[] { 0, 120 });
                case HapticCue.OverParSwitch: return new HapticPattern(new long[] { 0, 8, 40, 8 }, new[] { 0, 100, 0, 100 });
                case HapticCue.Delivery:
                case HapticCue.Purchase: return new HapticPattern(new long[] { 0, 12, 40, 20 }, new[] { 0, 90, 0, 170 });
                case HapticCue.WrongStation: return new HapticPattern(new long[] { 0, 45 }, new[] { 0, 200 });
                case HapticCue.Win: return new HapticPattern(new long[] { 0, 12, 60, 18, 60, 26 }, new[] { 0, 80, 0, 140, 0, 210 });
                case HapticCue.Fail: return new HapticPattern(new long[] { 0, 80 }, new[] { 0, 120 });
                default: throw new ArgumentOutOfRangeException(nameof(cue));
            }
        }
    }
    public sealed class GameHaptics : MonoBehaviour
    {
        private readonly GameplayAudioCueTracker _tracker = new GameplayAudioCueTracker();
        private IHaptics _device;
        private bool _enabled = true;
        private bool _paused;
        public bool Enabled => _enabled;
        public int SnapshotObservationCount { get; private set; }
        public void Initialize(IHaptics device = null)
        {
            if (_device != null) return;
            _device = device ?? AndroidHaptics.Create();
        }
        public void SetEnabled(bool value)
        {
            _enabled = value;
            if (!value) _device?.Cancel();
        }
        public void BindSession(GameSession session) => _tracker.Rebaseline(
            session?.State.Deliveries ?? 0, session?.State.Rejections ?? 0,
            session?.State.Outcome.Kind ?? OutcomeKind.Running);
        public void Observe(GameSession session, bool gameplayVisible)
        {
            if (session == null) return;
            SnapshotObservationCount++;
            var state = session.State;
            var cues = _tracker.Observe(state.Deliveries, state.Rejections, state.Outcome.Kind);
            // Outcome frames have already switched away from "Playing" in GameRoot.
            if (gameplayVisible || state.Outcome.Kind != OutcomeKind.Running) PlayGameplayCues(cues);
        }
        public void PlayGameplayCues(GameplayAudioCues cues)
        {
            if ((cues & GameplayAudioCues.Delivery) != 0) Play(HapticCue.Delivery);
            if ((cues & GameplayAudioCues.WrongStation) != 0) Play(HapticCue.WrongStation);
            if ((cues & GameplayAudioCues.Celebrate) != 0) Play(HapticCue.Win);
            if ((cues & GameplayAudioCues.Failed) != 0) Play(HapticCue.Fail);
        }
        public void PlayButtonTap() => Play(HapticCue.Tap);
        public void PlaySwitch(bool overPar) => Play(overPar ? HapticCue.OverParSwitch : HapticCue.Switch);
        public void PlayPurchaseSuccess() => Play(HapticCue.Purchase);
        private void Play(HapticCue cue)
        {
            if (_enabled && !_paused && isActiveAndEnabled) _device?.Play(cue);
        }
        private void OnApplicationPause(bool paused)
        {
            _paused = paused;
            if (paused) _device?.Cancel();
        }
        private void OnDisable() => _device?.Cancel();
        private void OnDestroy() { _device?.Cancel(); _device?.Dispose(); _device = null; }
    }
}
