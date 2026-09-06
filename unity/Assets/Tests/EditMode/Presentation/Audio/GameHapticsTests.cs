using System.Collections.Generic;
using CatMetro.Presentation.Audio;
using CatMetro.Presentation.Haptics;
using NUnit.Framework;
using UnityEngine;

namespace CatMetro.Tests.Presentation.Audio
{
    public sealed class GameHapticsTests
    {
        private GameObject _owner;
        private GameHaptics _haptics;
        private RecordingHaptics _device;
        [SetUp] public void SetUp()
        {
            _owner = new GameObject("HapticsTest");
            _haptics = _owner.AddComponent<GameHaptics>();
            _haptics.Initialize(_device = new RecordingHaptics());
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(_owner); }

        [Test]
        public void OptOutCancelsAndSuppressesEveryHook_UntilReenabled()
        {
            _haptics.PlayButtonTap();
            Assert.That(_device.Cues, Is.EqualTo(new[] { HapticCue.Tap }));
            _device.Cues.Clear();
            _haptics.SetEnabled(false);
            Assert.That(_haptics.Enabled, Is.False);
            Assert.That(_device.Cancels, Is.GreaterThan(0));
            FireAll();
            Assert.That(_device.Cues, Is.Empty);
            _haptics.SetEnabled(true);
            FireAll();
            Assert.That(_device.Cues, Is.EqualTo(new[] { HapticCue.Tap, HapticCue.Switch,
                HapticCue.OverParSwitch, HapticCue.Purchase, HapticCue.Delivery,
                HapticCue.WrongStation, HapticCue.Win, HapticCue.Fail }));
        }

        [Test]
        public void NoCueDoesNotCreateATrainVibration()
        {
            _haptics.PlayGameplayCues(GameplayAudioCues.None);
            Assert.That(_device.Cues, Is.Empty);
        }

        [TestCase(HapticCue.Switch, new long[] { 0, 18 }, new[] { 0, 120 })]
        [TestCase(HapticCue.OverParSwitch, new long[] { 0, 8, 40, 8 }, new[] { 0, 100, 0, 100 })]
        [TestCase(HapticCue.Delivery, new long[] { 0, 12, 40, 20 }, new[] { 0, 90, 0, 170 })]
        [TestCase(HapticCue.Purchase, new long[] { 0, 12, 40, 20 }, new[] { 0, 90, 0, 170 })]
        [TestCase(HapticCue.WrongStation, new long[] { 0, 45 }, new[] { 0, 200 })]
        [TestCase(HapticCue.Fail, new long[] { 0, 80 }, new[] { 0, 120 })]
        [TestCase(HapticCue.Win, new long[] { 0, 12, 60, 18, 60, 26 }, new[] { 0, 80, 0, 140, 0, 210 })]
        public void PixelPatternsHaveTheAuthoredTimingAndAmplitude(HapticCue cue, long[] timing, int[] amplitude)
        {
            var pattern = HapticPattern.For(cue);
            Assert.That(pattern.Timings, Is.EqualTo(timing));
            Assert.That(pattern.Amplitudes, Is.EqualTo(amplitude));
        }

        [Test] public void UiTapUsesAndroidsTickWithAShortFallback()
        {
            var pattern = HapticPattern.For(HapticCue.Tap);
            Assert.That(pattern.Predefined, Is.EqualTo(2));
            Assert.That(pattern.Timings, Is.EqualTo(new long[] { 0, 8 }));
        }

        private void FireAll()
        {
            _haptics.PlayButtonTap(); _haptics.PlaySwitch(false); _haptics.PlaySwitch(true);
            _haptics.PlayPurchaseSuccess();
            _haptics.PlayGameplayCues(GameplayAudioCues.Delivery | GameplayAudioCues.WrongStation
                | GameplayAudioCues.Celebrate | GameplayAudioCues.Failed);
        }
        private sealed class RecordingHaptics : IHaptics
        {
            public readonly List<HapticCue> Cues = new List<HapticCue>();
            public int Cancels; public bool Disposed;
            public void Play(HapticCue cue) => Cues.Add(cue);
            public void Cancel() => Cancels++;
            public void Dispose() => Disposed = true;
        }
    }
}
