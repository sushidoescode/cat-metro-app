using System.Collections;
using System.Linq;
using CatMetro.Presentation.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    public sealed class MusicDirectorLifecycleTests
    {
        private GameObject _owner;
        [TearDown] public void TearDown() { if (_owner != null) Object.DestroyImmediate(_owner); }

        [UnityTest]
        public IEnumerator HomePlaysAndEachStemStartsOnOneDspClock()
        {
            _owner = new GameObject("MusicTest");
            var director = _owner.AddComponent<MusicDirector>();
            double before = AudioSettings.dspTime;
            director.Initialize(() => false);
            director.Observe(null, MusicScene.Home);
            var sources = _owner.GetComponentsInChildren<AudioSource>();
            Assert.That(sources, Has.Length.EqualTo(5));
            Assert.That(sources.All(s => s.loop && !s.playOnAwake && s.spatialBlend == 0f), Is.True);
            Assert.That(director.ScheduledStartDspTime, Is.GreaterThan(before));
            yield return new WaitForSecondsRealtime(.45f);
            Assert.That(sources.Single(s => s.clip.name == "home").volume, Is.GreaterThan(.1f));
            Assert.That(sources.Where(s => s.clip.name != "home").All(s => s.volume < .001f), Is.True);
            director.Initialize(() => false);
            Assert.That(_owner.GetComponentsInChildren<AudioSource>(), Has.Length.EqualTo(5),
                "initialization cannot stack a second score");
        }

        [UnityTest]
        public IEnumerator OwnMusicAtBootSuppressesTheScoreUntilExplicitlyEnabled()
        {
            _owner = new GameObject("MusicTest");
            var director = _owner.AddComponent<MusicDirector>();
            director.Initialize(() => true);
            director.ApplyPreference(true);
            director.Observe(null, MusicScene.Home);
            yield return null;
            Assert.That(director.SuppressedForOtherMusic, Is.True);
            Assert.That(director.Enabled, Is.False);
            Assert.That(_owner.GetComponentsInChildren<AudioSource>().All(s => s.volume == 0f), Is.True);
            director.SetEnabled(true);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(director.Enabled, Is.True);
            Assert.That(director.SuppressedForOtherMusic, Is.False);
            Assert.That(_owner.GetComponentsInChildren<AudioSource>().Any(s => s.volume > 0f), Is.True);
            director.SetEnabled(false);
            Assert.That(_owner.GetComponentsInChildren<AudioSource>().All(s => s.volume == 0f), Is.True,
                "opting out mutes immediately, without a lingering fade");
        }

        [UnityTest]
        public IEnumerator PauseAndDisableStopAllStemsAndResumeAsOneScore()
        {
            _owner = new GameObject("MusicTest");
            var director = _owner.AddComponent<MusicDirector>();
            director.Initialize(() => false);
            director.Observe(null, MusicScene.Home);
            yield return new WaitForSecondsRealtime(.25f);
            _owner.SendMessage("OnApplicationPause", true, SendMessageOptions.DontRequireReceiver);
            Assert.That(_owner.GetComponentsInChildren<AudioSource>().All(s => !s.isPlaying), Is.True);
            double previousStart = director.ScheduledStartDspTime;
            _owner.SendMessage("OnApplicationPause", false, SendMessageOptions.DontRequireReceiver);
            Assert.That(director.ScheduledStartDspTime, Is.GreaterThan(previousStart));
            director.enabled = false;
            Assert.That(_owner.GetComponentsInChildren<AudioSource>().All(s => !s.isPlaying), Is.True);
        }
    }
}
