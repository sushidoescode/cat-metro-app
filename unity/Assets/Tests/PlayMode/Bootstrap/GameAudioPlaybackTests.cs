using System.Collections;
using System.Linq;
using System.IO;
using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Presentation.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.PlayMode
{
    public sealed class GameAudioPlaybackTests
    {
        private GameObject _owner;
        private GameAudio _audio;
        [SetUp] public void SetUp()
        {
            _owner = new GameObject("SfxTest");
            _audio = _owner.AddComponent<GameAudio>();
            _audio.Initialize(_owner.AddComponent<Camera>());
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(_owner); Time.timeScale = 1f; }

        [UnityTest]
        public IEnumerator WinChorusWaitsForTheSharedBeat_AndCancellationSuppressesIt()
        {
            Time.timeScale = 0f;
            QueueWin();
            Assert.That(_audio.CelebratePending, Is.True);
            Assert.That(_owner.GetComponents<AudioSource>().All(s => !s.isPlaying), Is.True,
                "cat voices must wait with the cadence for D's shared win beat");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(_owner.GetComponents<AudioSource>().All(s => !s.isPlaying), Is.True);
            _audio.CancelCelebrate();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(_audio.CelebratePending, Is.False);
            Assert.That(_owner.GetComponents<AudioSource>().All(s => !s.isPlaying), Is.True,
                "navigation before the win beat cancels both cadence and chorus");
        }

        [UnityTest]
        public IEnumerator WinBeatStartsCadenceAndThreeMews_CancelPreservesReusedUiVoices()
        {
            QueueWin();
            yield return WaitForWinBeat();
            var sources = _owner.GetComponents<AudioSource>();
            Assert.That(sources.Count(s => s.clip != null && s.clip.name.StartsWith("cat-mew-")
                && s.isPlaying), Is.EqualTo(3));
            var cadence = sources.Single(s => s.clip != null && s.clip.name == "win-cadence");
            Assert.That(cadence.isPlaying, Is.True);
            _audio.PlayButtonTap();
            _audio.PlayButtonTap(); // reuses the first chorus slot in the four-source pool
            _audio.CancelCelebrate();
            Assert.That(cadence.isPlaying, Is.False);
            Assert.That(sources.Where(s => s.clip != null && s.clip.name.StartsWith("cat-mew-"))
                .All(s => !s.isPlaying), Is.True, "active and scheduled chorus voices stop immediately");
            Assert.That(sources.Where(s => s.clip != null && s.clip.name == "wooden-tap")
                .All(s => s.isPlaying), Is.True, "a recycled chorus slot now belongs to its newer UI tap");
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(sources.Where(s => s.clip != null && s.clip.name.StartsWith("cat-mew-"))
                .All(s => !s.isPlaying), Is.True);
        }

        private void QueueWin()
        {
            var session = Session();
            _audio.BindSession(session);
            session.State.Outcome = SimOutcome.Won;
            _audio.Observe(session, false);
        }

        private IEnumerator WaitForWinBeat()
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (_audio.CelebratePending && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(_audio.CelebratePending, Is.False, "the unscaled win beat must arrive");
        }

        [UnityTest]
        public IEnumerator ChuffStopsWithANinetyMillisecondRelease_ButOptOutStopsImmediately()
        {
            var chuff = _owner.GetComponents<AudioSource>().Single(s => s.loop);
            chuff.volume = .22f;
            chuff.Play();
            yield return null;
            _audio.StopGameplayLoop();
            Assert.That(chuff.isPlaying, Is.True, "a normal stop must release instead of clicking");
            yield return new WaitForSecondsRealtime(.045f);
            Assert.That(chuff.volume, Is.InRange(0f, .20f));
            yield return new WaitForSecondsRealtime(.07f);
            Assert.That(chuff.isPlaying, Is.False);
            chuff.Play();
            _audio.SetEnabled(false);
            Assert.That(chuff.isPlaying, Is.False, "the user's opt-out has no tail");
        }

        [Test]
        public void TransientsUseFourBoundedVoicesAndMuteStopsEveryVoice()
        {
            _audio.Initialize(_owner.GetComponent<Camera>());
            Assert.That(_owner.GetComponents<AudioSource>(), Has.Length.EqualTo(6),
                "four transient voices, cadence, and train loop; repeated Initialize is idempotent");
            for (int i = 0; i < 4; i++) _audio.PlayButtonTap();
            var playing = _owner.GetComponents<AudioSource>().Where(s => s.isPlaying).ToArray();
            Assert.That(playing, Has.Length.EqualTo(4), string.Join(", ", playing.Select(s =>
                (s.clip != null ? s.clip.name : "null") + " vol=" + s.volume + " loop=" + s.loop)));
            Assert.That(playing.All(s => s.pitch >= .96f && s.pitch <= 1.04f), Is.True);
            _audio.SetEnabled(false);
            Assert.That(_owner.GetComponents<AudioSource>().All(s => !s.isPlaying), Is.True);
            _audio.PlayPurchaseSuccess();
            Assert.That(_owner.GetComponents<AudioSource>().All(s => !s.isPlaying), Is.True);
        }

        [UnityTest]
        public IEnumerator AuthoredL001_BoardingMewDeliveryAndWonCadenceReachTheRealSourcesOnce()
        {
            var session = Session();
            _audio.BindSession(session);
            Assert.That(session.EnqueueToggle(0), Is.True);
            bool heardBoarding = false;
            for (int i = 0; i < 190 && session.State.Outcome.Kind == OutcomeKind.Running; i++)
            {
                session.AdvanceMs(TickInterpolator.TICK_MS);
                _audio.Observe(session, true);
                if (session.State.Deliveries == 0 && GameAudio.BoardedCount(session) > 0)
                    heardBoarding |= _owner.GetComponents<AudioSource>().Any(s =>
                        s.clip != null && s.clip.name.StartsWith("cat-mew-") && s.isPlaying);
            }
            Assert.That(heardBoarding, Is.True);
            Assert.That(session.State.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            var sources = _owner.GetComponents<AudioSource>();
            var cadence = sources.Single(s => s.clip != null && s.clip.name == "win-cadence");
            Assert.That(_audio.CelebratePending, Is.True);
            Assert.That(cadence.isPlaying, Is.False, "the cadence waits for D's win beat");
            yield return WaitForWinBeat();
            Assert.That(sources.Count(s => s.clip != null && s.clip.name.StartsWith("cat-mew-")), Is.EqualTo(3),
                "the Won chorus uses three separate scheduled voices");
            Assert.That(sources.Single(s => s.clip != null && s.clip.name == "delivery-chime").pitch,
                Is.EqualTo(GameAudio.DeliveryPitch(session.State.Deliveries)));
            Assert.That(cadence.isPlaying, Is.True);
            yield return new WaitForSecondsRealtime(.12f);
            int position = cadence.timeSamples;
            _audio.Observe(session, false);
            Assert.That(cadence.timeSamples, Is.GreaterThanOrEqualTo(position), "a later Won frame cannot restart the cadence");
        }

        [Test]
        public void AuthoredL001_WrongStationGrumblesAndFailureHasAFeltSting()
        {
            var session = Session();
            _audio.BindSession(session);
            bool grumbled = false;
            for (int i = 0; i < 190 && session.State.Outcome.Kind == OutcomeKind.Running; i++)
            {
                session.AdvanceMs(TickInterpolator.TICK_MS);
                _audio.Observe(session, true);
                grumbled |= _owner.GetComponents<AudioSource>().Any(s => s.clip != null && s.clip.name == "cat-grumble");
            }
            Assert.That(grumbled, Is.True);
            Assert.That(session.State.Outcome.Kind, Is.EqualTo(OutcomeKind.Failed));
            Assert.That(_owner.GetComponents<AudioSource>().Any(s => s.clip != null && s.clip.name == "fail-sting" && s.isPlaying), Is.True);
        }

        [Test]
        public void HomeMewWaitsEightToFourteenSecondsAndCannotBypassMute()
        {
            _audio.ObserveHome(true, 7);
            Assert.That(_owner.GetComponents<AudioSource>().All(s => !s.isPlaying), Is.True);
            _audio.ObserveHome(true, 7);
            Assert.That(_owner.GetComponents<AudioSource>().Any(s => s.clip != null && s.clip.name.StartsWith("cat-mew-") && s.isPlaying), Is.True);
            _audio.SetEnabled(false);
            _audio.ObserveHome(true, 100);
            Assert.That(_owner.GetComponents<AudioSource>().All(s => !s.isPlaying), Is.True);
        }

        private static GameSession Session()
        {
            var imported = LevelImporter.Import(File.ReadAllBytes(Path.Combine(
                UnityEngine.Application.streamingAssetsPath, "content/levels/L001.json")));
            Assert.That(imported.Ok, Is.True, imported.Error?.ToString());
            return new GameSession(imported.Value);
        }
    }
}
