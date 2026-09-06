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
        public IEnumerator WinFxHook_FiresOnceAtTheBeat_AndReducedMotionSuppressesIt()
        {
            Launch();
            int calls = 0;
            _root.WinConfettiRequested = (board, camera) => calls++;
            _root.Session.State.Outcome = SimOutcome.Won;
            yield return null;
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(calls, Is.Zero);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(calls, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(calls, Is.EqualTo(1));
            _root.Retry();
            _root.MotionOffToggle = true;
            _root.Session.State.Outcome = SimOutcome.Won;
            yield return null;
            yield return new WaitForSecondsRealtime(.7f);
            Assert.That(calls, Is.EqualTo(1));
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
