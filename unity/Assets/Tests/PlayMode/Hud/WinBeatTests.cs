using System.Collections;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Bootstrap;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Presentation.Hud;

namespace CatMetro.Tests.PlayMode
{
    public sealed class WinBeatTests
    {
        private GameRoot _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
            Time.timeScale = 1f;
        }

        private GameRoot Launch()
        {
            var imported = LevelImporter.Import(File.ReadAllBytes(Path.Combine(
                UnityEngine.Application.streamingAssetsPath, "content", "levels", "L001.json")));
            Assert.That(imported.Ok, Is.True);
            _root = GameRoot.LaunchWith(imported.Value);
            return _root;
        }

        [UnityTest]
        public IEnumerator WinTitle_PaintsAboveTheResultsShade()
        {
            Launch();
            _root.Session.State.Outcome = SimOutcome.Won;
            yield return null;
            yield return null;
            var results = _root.GetComponent<ResultsPanel>();
            Assert.That(_root.Banner.GetComponent<Canvas>().sortingOrder,
                Is.GreaterThan(results.PanelRoot.GetComponent<Canvas>().sortingOrder));
            yield return new WaitForSecondsRealtime(.4f);
            var title = _root.Banner.TextTransform.GetComponent<TMP_Text>();
            Assert.That(title.color.a, Is.EqualTo(1f).Within(.001f));
            Assert.That(.2126f * title.color.r + .7152f * title.color.g + .0722f * title.color.b,
                Is.GreaterThan(.9f));
        }

        [UnityTest]
        public IEnumerator WinCta_IsRegisteredImmediately_ButPaintWaitsForTheBeat()
        {
            Launch();
            Time.timeScale = 0f;
            _root.Session.State.Outcome = SimOutcome.Won;
            yield return null;
            yield return null;
            var results = _root.GetComponent<ResultsPanel>();
            Assert.That(results.IsVisible, Is.True);
            Assert.That(_root.Input.Regions.TryResolve(results.ChipPaintedRectPx.center, out _), Is.True);
            var label = results.PanelRoot.GetComponentInChildren<TMP_Text>();
            Assert.That(label.color.a, Is.Zero.Within(.001f));
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(label.color.a, Is.Zero.Within(.001f));
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(label.color.a, Is.EqualTo(1f).Within(.001f));
        }

        [UnityTest]
        public IEnumerator Flourish_WaitsForTheBeat_AndRetryCancelsPendingPlayback()
        {
            Launch();
            _root.Audio.SetEnabled(true);
            _root.Session.State.Outcome = SimOutcome.Won;
            yield return null;
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(_root.Audio.CelebratePending, Is.True);
            _root.Retry();
            Assert.That(_root.Audio.CelebratePending, Is.False);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(_root.Audio.CelebratePending, Is.False,
                "a dismissed win cannot sound over the next level");
        }

        [UnityTest]
        public IEnumerator WinConfetti_StartsWithTheCatHop_AndExpiresWithoutAdvancingTheSession()
        {
            Launch();
            Time.timeScale = 0f;
            _root.Session.EnqueueToggle(0);
            _root.Session.AdvanceMs(200 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            Assert.That(_root.Session.State.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            int tick = _root.Session.State.Tick;
            yield return null;
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(Confetti(), Is.Null, "the burst waits for the same beat as the cat");
            var cat = _root.View.transform.Find("delivered-cat:0/Carriage/Cat");
            Assert.That(cat, Is.Not.Null);
            var neutral = cat.localScale;
            yield return new WaitForSecondsRealtime(.3f);
            var confetti = Confetti();
            Assert.That(confetti, Is.Not.Null, "the shipped win beat must emit BoardFx confetti");
            Assert.That(confetti.particleCount, Is.EqualTo(100));
            Assert.That(cat.localScale.magnitude, Is.GreaterThan(neutral.magnitude * 1.05f),
                "the retained cat is hopping while the burst enters");
            yield return new WaitForSecondsRealtime(2f);
            Assert.That(confetti.particleCount, Is.Zero, "the one burst ends after two unscaled seconds");
            Assert.That(_root.Session.State.Tick, Is.EqualTo(tick));
            Assert.That(cat.gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator WinConfetti_RetryCancelsThePendingBeat_AndMotionOffSuppressesIt()
        {
            Launch();
            Time.timeScale = 0f;
            _root.Session.State.Outcome = SimOutcome.Won;
            yield return null;
            yield return new WaitForSecondsRealtime(.4f);
            _root.Retry();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(Confetti(), Is.Null, "the dismissed win cannot emit over the next board");
            _root.MotionOffToggle = true;
            _root.Session.State.Outcome = SimOutcome.Won;
            yield return null;
            yield return new WaitForSecondsRealtime(.7f);
            Assert.That(Confetti(), Is.Null);
        }

        [UnityTest]
        public IEnumerator WinConfetti_ActiveBurstStopsWhenMotionIsDisabledOrTheLevelIsDismissed()
        {
            Launch();
            Time.timeScale = 0f;
            _root.Session.State.Outcome = SimOutcome.Won;
            yield return null;
            yield return new WaitForSecondsRealtime(.7f);
            var confetti = Confetti();
            Assert.That(confetti, Is.Not.Null);
            _root.MotionOffToggle = true;
            yield return null;
            yield return null;
            Assert.That(confetti.particleCount, Is.Zero);
            _root.MotionOffToggle = false;
            _root.Retry();
            _root.Session.State.Outcome = SimOutcome.Won;
            yield return null;
            yield return new WaitForSecondsRealtime(.7f);
            confetti = Confetti();
            Assert.That(confetti.particleCount, Is.EqualTo(100));
            _root.Retry();
            Assert.That(confetti.particleCount, Is.Zero,
                "navigation clears the burst before the old board is destroyed");
            yield return null;
            Assert.That(confetti == null, Is.True);
        }

        private ParticleSystem Confetti() =>
            _root.View.transform.Find("Win confetti")?.GetComponent<ParticleSystem>();

        [UnityTest]
        public IEnumerator WinConfetti_DisablingTheRootCancelsItsPendingAndActiveBeat()
        {
            Launch();
            Time.timeScale = 0f;
            _root.Session.State.Outcome = SimOutcome.Won;
            yield return null;
            yield return new WaitForSecondsRealtime(.4f);
            _root.enabled = false;
            yield return new WaitForSecondsRealtime(.3f);
            _root.enabled = true;
            yield return null;
            yield return null;
            Assert.That(Confetti(), Is.Null, "enabling the root must not replay a cancelled win");
            _root.Retry();
            _root.Session.State.Outcome = SimOutcome.Won;
            yield return null;
            yield return new WaitForSecondsRealtime(.7f);
            var confetti = Confetti();
            Assert.That(confetti, Is.Not.Null);
            _root.enabled = false;
            Assert.That(confetti.particleCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator MotionOff_MakesTheWinPaintImmediate()
        {
            Launch();
            _root.MotionOffToggle = true;
            _root.Session.State.Outcome = SimOutcome.Won;
            yield return null;
            yield return null;
            var label = _root.GetComponent<ResultsPanel>().PanelRoot.GetComponentInChildren<TMP_Text>();
            Assert.That(label.color.a, Is.EqualTo(1f).Within(.001f));
            Assert.That(_root.Banner.TextTransform.GetComponent<TMP_Text>().color.a,
                Is.EqualTo(1f).Within(.001f));
        }
    }
}
