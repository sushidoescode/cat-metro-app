using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;

namespace CatMetro.Tests.PlayMode
{
    // UI-CHROME criterion 7. Reflection keeps the red phase compiling before the new
    // Presentation/Audio type exists; every assertion then exercises the real component.
    public sealed class UiAudioManagerTests
    {
        private const string ManagerTypeName =
            "CatMetro.Presentation.Audio.UiAudioManager";
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) UnityEngine.Object.Destroy(_host);
            _host = null;
        }

        [Test]
        public void Ensure_IsOneManagerWithExactlyThreePrewarmedSources()
        {
            _host = new GameObject("AudioManagerHost");
            var first = Ensure(_host.transform);
            var second = Ensure(_host.transform);
            Assert.That(second, Is.SameAs(first));
            Assert.That(ManagersUnder(_host).Length, Is.EqualTo(1));

            var sources = first.GetComponents<AudioSource>();
            Assert.That(sources.Length, Is.EqualTo(3),
                "tap, warning, and win sources are prewarmed once");
            Assert.That(sources.Select(s => s.clip).All(c => c != null), Is.True);
            Assert.That(sources.Select(s => s.clip.name).Distinct().Count(), Is.EqualTo(3));
            Assert.That(sources.All(s => !s.playOnAwake && !s.loop), Is.True);
        }

        [Test]
        public void Playback_ReusesSources_AndPublishesDistinctCueEdges()
        {
            _host = new GameObject("AudioPlaybackHost");
            var manager = Ensure(_host.transform);
            int sourceCount = manager.GetComponents<AudioSource>().Length;

            Invoke(manager, "PlayTap");
            AssertCue(manager, "Tap", 1);
            Invoke(manager, "PlayWarning");
            AssertCue(manager, "Warning", 2);
            Invoke(manager, "PlayWin");
            AssertCue(manager, "Win", 3);
            Assert.That(manager.GetComponents<AudioSource>().Length, Is.EqualTo(sourceCount),
                "playback allocates no additional source");
        }

        [UnityTest]
        public IEnumerator ScreenStateEdges_PlayWarningOnce_ThenTapOnRetryReturn()
        {
            _host = new GameObject("AudioStateEdgeHost");
            string state = "Playing";
            var chrome = _host.AddComponent<ScreenChromeController>();
            chrome.Attach(() => state);
            yield return null;

            var manager = ManagerUnder(_host);
            AssertCue(manager, "None", 0);

            state = "FailureReview";
            yield return null;
            AssertCue(manager, "Warning", 1);
            yield return null;
            AssertCue(manager, "Warning", 1,
                "the same state over another frame does not retrigger the cue");

            state = "Playing";
            yield return null;
            AssertCue(manager, "Tap", 2,
                "returning from the retry surface carries the tap acknowledgement");
        }

        [UnityTest]
        public IEnumerator HomePlayAndNextSeams_AllAcknowledgeWithTap_AndWinIsEdgeOnly()
        {
            _host = new GameObject("AudioActionSeamsHost");
            var canvas = _host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var homeRegions = new ChromeRegions();
            var home = HomeScreenView.Create(canvas.transform);
            home.Attach(homeRegions, () => true);
            home.Show();
            yield return null;
            Assert.That(homeRegions.TryResolve(home.PinPaintedRectPx.center, out var select),
                Is.True);
            select();
            AssertCue(ManagerUnder(_host), "Tap", 1, "Home pin");
            home.Hide();

            var introRegions = new ChromeRegions();
            var intro = LevelIntroSheet.Create(canvas.transform);
            intro.Attach(introRegions);
            intro.Show("First Switch", 3);
            yield return null;
            Assert.That(introRegions.TryResolve(intro.PlayChipRectPx.center, out var play),
                Is.True);
            play();
            AssertCue(ManagerUnder(_host), "Tap", 2, "Play CTA");
            intro.Hide();

            string state = "Won";
            var resultsRegions = new ChromeRegions();
            var results = _host.AddComponent<ResultsPanel>();
            results.Attach(() => state, resultsRegions);
            yield return null;
            AssertCue(ManagerUnder(_host), "Win", 3, "Won entry");
            yield return null;
            AssertCue(ManagerUnder(_host), "Win", 3, "Won does not retrigger per frame");

            Assert.That(resultsRegions.TryResolve(
                results.ChipPaintedRectPx.center, out var next), Is.True);
            next();
            AssertCue(ManagerUnder(_host), "Tap", 4, "Next CTA");
        }

        private static Type ManagerType()
        {
            return typeof(UiChromeMaterial).Assembly.GetType(ManagerTypeName);
        }

        private static Component Ensure(Transform root)
        {
            var type = ManagerType();
            Assert.That(type, Is.Not.Null,
                "UI-CHROME RED: Presentation/Audio/UiAudioManager does not exist yet");
            var method = type.GetMethod("Ensure", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return (Component)method.Invoke(null, new object[] { root });
        }

        private static Component[] ManagersUnder(GameObject root)
        {
            var type = ManagerType();
            Assert.That(type, Is.Not.Null,
                "UI-CHROME RED: manager type missing");
            return root.GetComponentsInChildren<Component>(true)
                .Where(c => c.GetType() == type).ToArray();
        }

        private static Component ManagerUnder(GameObject root)
        {
            var managers = ManagersUnder(root);
            Assert.That(managers.Length, Is.EqualTo(1),
                "the styled view tree owns exactly one audio manager");
            return managers[0];
        }

        private static void Invoke(Component manager, string method)
        {
            var info = manager.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(info, Is.Not.Null, method);
            info.Invoke(manager, null);
        }

        private static void AssertCue(Component manager, string cue, int count,
            string because = null)
        {
            var type = manager.GetType();
            var last = type.GetProperty("LastCueName")?.GetValue(manager) as string;
            var plays = (int)(type.GetProperty("PlayCount")?.GetValue(manager) ?? -1);
            Assert.That(last, Is.EqualTo(cue), because ?? cue);
            Assert.That(plays, Is.EqualTo(count), because ?? cue);
        }
    }
}
