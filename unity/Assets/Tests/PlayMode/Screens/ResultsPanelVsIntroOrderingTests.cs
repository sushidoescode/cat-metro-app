using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Screens;

namespace CatMetro.Tests.PlayMode
{
    // CM-LOADNEXT D-1 (state/handoffs/CM-UX-07-delta-audit.md): once ResultsPanel attaches, its
    // "results.next" region and LevelIntroSheet's "intro.play" region can co-register in ONE
    // ChromeRegions (both live in the dev BootToHome flow if the sim happens to win in the
    // background before the player clears the Home/Intro overlay — GameRoot's sim advances
    // regardless of ScreensVisible). Both used to sit at the SAME priority (10) — a tie the
    // registry breaks by earliest-registration Seq (ChromeRegions.cs), an accident of WHICH
    // component happened to register first, never a stated law.
    //
    // The ordering law (D-1 + #46-F9): LevelIntroSheet's ScreensCanvas paints ABOVE
    // ResultsPanel's canvas (sortingOrder 120 vs 110, GameRoot.cs) — "intro.play" registers at
    // ChromeRegions.StackedModalPriority (11), strictly above ResultsPanel's
    // ChromeRegions.ModalPriority (10). The topmost-PAINTED modal always wins the TAP, by
    // priority, never by registration luck.
    public sealed class ResultsPanelVsIntroOrderingTests
    {
        private GameObject _resultsHost;
        private GameObject _introHost;

        [TearDown]
        public void TearDown()
        {
            if (_resultsHost != null) Object.Destroy(_resultsHost);
            if (_introHost != null) Object.Destroy(_introHost);
        }

        // The discriminating case: Results registers FIRST (the lower Seq) — a tie-break-only
        // implementation would hand the tap to Results here. Only an explicit priority gap
        // (11 > 10) can make Intro win under THIS registration order.
        [UnityTest]
        public IEnumerator IntroWins_OverResults_EvenWhenResultsRegisteredFirst()
        {
            var regions = new ChromeRegions();

            _resultsHost = new GameObject("results-host");
            var results = _resultsHost.AddComponent<ResultsPanel>();
            results.Attach(() => "Won", regions); // shown immediately — registers now (first)
            yield return null;

            _introHost = new GameObject("intro-canvas");
            var introCanvas = _introHost.AddComponent<Canvas>();
            introCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var intro = LevelIntroSheet.Create(introCanvas.transform);
            intro.Attach(regions);
            intro.Show("First Switch", 2); // registers second
            yield return null;

            Assert.That(regions.Count, Is.EqualTo(2),
                "precondition: both modals are live in ONE registry — otherwise this test "
                + "proves nothing about the tie-break");
            var resultsRect = results.ChipPaintedRectPx;
            var introRect = intro.PlayChipRectPx;
            Assert.That(introRect.Contains(resultsRect.center), Is.True,
                "precondition: the two chips overlap at Results' own center (both are the SAME "
                + "safe-area thumb band, HudBands.ThumbBand) — otherwise a tap there cannot "
                + "discriminate priority from geometry");

            int nextFired = 0, playFired = 0;
            results.NextRequested = () => nextFired++;
            intro.PlayRequested = () => playFired++;

            Assert.That(regions.TryResolve(resultsRect.center, out var onTap), Is.True);
            onTap();
            Assert.That(playFired, Is.EqualTo(1),
                "the topmost-PAINTED modal (Intro, ScreensCanvas sortingOrder 120) wins the "
                + "tap, by priority (11 > 10) — never by which one registered first");
            Assert.That(nextFired, Is.EqualTo(0),
                "the loser never fires — a co-registered tap resolves to exactly one handler, "
                + "never both");
        }

        // Consistency check: the SAME outcome holds under the opposite registration order,
        // proving the law is priority-driven both ways, not a lucky Seq coincidence either way.
        [UnityTest]
        public IEnumerator IntroWins_OverResults_WhenIntroRegisteredFirst()
        {
            var regions = new ChromeRegions();

            _introHost = new GameObject("intro-canvas");
            var introCanvas = _introHost.AddComponent<Canvas>();
            introCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var intro = LevelIntroSheet.Create(introCanvas.transform);
            intro.Attach(regions);
            intro.Show("First Switch", 2); // registers first this time
            yield return null;

            _resultsHost = new GameObject("results-host");
            var results = _resultsHost.AddComponent<ResultsPanel>();
            results.Attach(() => "Won", regions); // registers second
            yield return null;

            Assert.That(regions.Count, Is.EqualTo(2),
                "precondition: both modals are live — otherwise this test proves nothing");
            var overlapPoint = intro.PlayChipRectPx.center;
            Assert.That(results.ChipPaintedRectPx.Contains(overlapPoint), Is.True,
                "precondition: the chips still overlap here");

            int nextFired = 0, playFired = 0;
            results.NextRequested = () => nextFired++;
            intro.PlayRequested = () => playFired++;

            Assert.That(regions.TryResolve(overlapPoint, out var onTap), Is.True);
            onTap();
            Assert.That(playFired, Is.EqualTo(1), "Intro still wins — priority, not Seq luck");
            Assert.That(nextFired, Is.EqualTo(0));
        }
    }
}
