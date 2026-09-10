using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using CatMetro.Bootstrap;
using CatMetro.Bootstrap.DevCapture;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Presentation.Hud;
using CatMetro.Presentation.Screens;
using CatMetro.Presentation.Theme;
using Object = UnityEngine.Object;

namespace CatMetro.Tests.PlayMode
{
    public sealed class FlowPolishTests
    {
        private GameRoot _root;
        private GameObject _canvas;
        private string _temporary;

        [SetUp]
        public void SetUp()
        {
            GameRoot.DevSkipShippedHome = false;
            _temporary = Path.Combine(Path.GetTempPath(), "cm-flow-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporary);
            DevLevelOverride.DirectoryOverride = _temporary;
            GameRoot.DailyStorageRootOverride = () => new FlowStorage(_temporary);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
            if (_canvas != null) Object.DestroyImmediate(_canvas);
            DevLevelOverride.DirectoryOverride = null;
            GameRoot.DailyStorageRootOverride = null;
            GameRoot.DevSkipShippedHome = false;
            Time.timeScale = 1f;
            if (Directory.Exists(_temporary)) Directory.Delete(_temporary, true);
        }

        private sealed class FlowStorage : CatMetro.Services.IStorageRoot
        {
            public string SaveDirectory { get; }
            public string CacheDirectory => SaveDirectory;
            public FlowStorage(string root)
            {
                SaveDirectory = Path.Combine(root, "save");
                Directory.CreateDirectory(SaveDirectory);
            }
        }

        private static ImportedLevel ReadLevel(string id)
        {
            var imported = LevelImporter.Import(File.ReadAllBytes(Path.Combine(
                UnityEngine.Application.streamingAssetsPath, "content", "levels", id + ".json")));
            Assert.That(imported.Ok, Is.True, imported.Error?.ToString());
            return imported.Value;
        }

        private void EnterGameplayWithMotion()
        {
            _root.MotionOffToggle = false;
            _root.AnimatorDurationScale = 1f;
            Assert.That(_root.Input.HandleTapAtScreen(_root.Home.PinPaintedRectPx.center), Is.EqualTo(-3));
            var fx = _root.GetComponent<CatMetro.Presentation.Fx.BoardFx>();
            fx.Advance(.14f);
            Assert.That(_root.Intro.IsVisible, Is.True, "finish the Home press before tapping Play");
            Assert.That(_root.Input.HandleTapAtScreen(_root.Intro.PlayChipRectPx.center), Is.EqualTo(-3));
            fx.Advance(.14f);
            Assert.That(_root.ScreensVisible, Is.False, "the Play action must finish before forcing an outcome");
        }

        [TestCase("L001")]
        [TestCase("L005")]
        [TestCase("L009")]
        [TestCase("L013")]
        public void TeachingTickets_ShowAuthoredGoal_AndUseSingularForOneCat(string id)
        {
            var level = ReadLevel(id);
            _canvas = new GameObject("IntroTestCanvas", typeof(Canvas));
            var sheet = LevelIntroSheet.Create(_canvas.transform);
            sheet.Show(level.Dto.Name, level.Dto.Win.Deliveries, level.Dto.Meta.TeachingGoal);
            Assert.That(Array.Exists(sheet.GetComponentsInChildren<TMP_Text>(),
                text => text.text == level.Dto.Meta.TeachingGoal), Is.True);
            if (level.Dto.Win.Deliveries == 1)
                Assert.That(sheet.GoalText, Is.EqualTo("Deliver 1 cat"));
        }

        [UnityTest]
        public IEnumerator HomeToL001_ThenNextToL002_AnnouncesAndFreezesBothLevels()
        {
            _root = GameRoot.Launch();
            _root.MotionOffToggle = true;
            yield return null;
            _root.Input.HandleTapAtScreen(_root.Home.PinPaintedRectPx.center);
            Assert.That(_root.Intro.GoalText, Is.EqualTo("Deliver 1 cat"));
            Assert.That(_root.Session.State.Tick, Is.Zero);
            _root.Input.HandleTapAtScreen(_root.Intro.PlayChipRectPx.center);
            _root.Input.HandleTapAtScreen(_root.Cam.WorldToScreenPoint(_root.View.SwitchWorldPos(0)));
            _root.Session.AdvanceMs(200 * CatMetro.Application.Session.TickInterpolator.TICK_MS);
            yield return null;
            yield return null;
            Assert.That(_root.ScreenState, Is.EqualTo("Won"));
            var panel = _root.GetComponent<ResultsPanel>();
            _root.Input.HandleTapAtScreen(panel.ChipPaintedRectPx.center);
            Assert.That(_root.CurrentLevelId, Is.EqualTo("L002"));
            Assert.That(_root.Intro.IsVisible, Is.True, "Next must announce the next level");
            Assert.That(_root.Stack.Current, Is.EqualTo("intro"));
            Assert.That(_root.Intro.NameText, Is.EqualTo(ReadLevel("L002").Dto.Name));
            for (int i = 0; i < 5; i++)
            {
                yield return null;
                Assert.That(_root.Session.State.Tick, Is.Zero, "the intro holds tick zero");
            }
            _root.Input.HandleTapAtScreen(_root.Intro.PlayChipRectPx.center);
            Assert.That(_root.ScreensVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator ForcedL005Overflow_HasNoNodeIdInPlayerCopy()
        {
            _root = GameRoot.LaunchWith(ReadLevel("L005"));
            _root.MotionOffToggle = true;
            _root.Session.State.Outcome = SimOutcome.MakeFailed(FailReason.PlatformOverflow);
            yield return null;
            Assert.That(_root.ScreenState, Is.EqualTo("FailureReview"));
            Assert.That(_root.Banner.CurrentText, Is.EqualTo("A platform overflowed"));
            foreach (var node in _root.Session.Level.Dto.Nodes.ToArray())
                Assert.That(_root.Banner.CurrentText, Does.Not.Contain(node.Id));
            var retry = _root.GetComponent<ScreenChromeController>().Cta;
            Assert.That(retry.GetComponentsInChildren<Image>().Length, Is.GreaterThan(3),
                "Retry uses the shared stitched pin");
        }

        [UnityTest]
        public IEnumerator ShippedNext_RebuildsUnderTheVeil_AndDoubleTapLeavesIntroUp()
        {
            _root = GameRoot.Launch();
            yield return null;
            EnterGameplayWithMotion();
            _root.Session.State.Outcome = SimOutcome.Won;
            yield return null;
            yield return null;
            var point = _root.GetComponent<ResultsPanel>().ChipPaintedRectPx.center;
            Assert.That(_root.Input.HandleTapAtScreen(point), Is.EqualTo(-3));
            Assert.That(_root.CurrentLevelId, Is.EqualTo("L001"), "cover the old board first");
            var veil = _root.GetComponent<ScreenChromeController>().Transition;
            Assert.That(veil.IsInFlight, Is.False, "the Next press finishes before the cover starts");
            Assert.That(_root.Input.HandleTapAtScreen(point), Is.EqualTo(-3),
                "a repeated tap during the pending press is consumed");
            _root.GetComponent<CatMetro.Presentation.Fx.BoardFx>().Advance(.14f);
            Assert.That(veil.IsInFlight, Is.True);
            _root.Input.HandleTapAtScreen(point);
            veil.Advance(.22f, false);
            Assert.That(_root.CurrentLevelId, Is.EqualTo("L002"));
            Assert.That(_root.Intro.IsVisible, Is.True);
            _root.Input.HandleTapAtScreen(point);
            Assert.That(_root.Intro.IsVisible, Is.True, "a repeated Next tap cannot dismiss the new intro");
            veil.Advance(.28f, false);
            yield return null;
            Assert.That(_root.Session.State.Tick, Is.Zero);
            Assert.That(_root.CurrentLevelId, Is.EqualTo("L002"));
        }

        [UnityTest]
        public IEnumerator MotionOff_NextDoubleTap_DoesNotDismissTheNewTicket()
        {
            _root = GameRoot.Launch();
            _root.MotionOffToggle = true;
            yield return null;
            _root.Input.HandleTapAtScreen(_root.Home.PinPaintedRectPx.center);
            _root.Input.HandleTapAtScreen(_root.Intro.PlayChipRectPx.center);
            _root.Session.State.Outcome = SimOutcome.Won;
            yield return null;
            yield return null;
            var point = _root.GetComponent<ResultsPanel>().ChipPaintedRectPx.center;
            _root.Input.HandleTapAtScreen(point);
            _root.Input.HandleTapAtScreen(point);
            Assert.That(_root.CurrentLevelId, Is.EqualTo("L002"));
            Assert.That(_root.Intro.IsVisible, Is.True);
            yield return null;
            Assert.That(_root.Session.State.Tick, Is.Zero);
            _root.Input.HandleTapAtScreen(_root.Intro.PlayChipRectPx.center);
            Assert.That(_root.Intro.IsVisible, Is.False, "a subsequent deliberate Play tap works");
        }

        [UnityTest]
        public IEnumerator ShippedRetry_RestartsSameLevelAtOpaqueMidpoint_ThenUnfreezes()
        {
            _root = GameRoot.Launch();
            yield return null;
            EnterGameplayWithMotion();
            _root.Session.State.Outcome = SimOutcome.MakeFailed(FailReason.TimeOut);
            yield return null;
            yield return null;
            var oldSession = _root.Session;
            _root.Retry();
            Assert.That(_root.Session, Is.SameAs(oldSession));
            var veil = _root.GetComponent<ScreenChromeController>().Transition;
            veil.Advance(.22f, false);
            Assert.That(_root.Session, Is.Not.SameAs(oldSession));
            Assert.That(_root.CurrentLevelId, Is.EqualTo("L001"));
            yield return null;
            Assert.That(_root.Session.State.Tick, Is.Zero, "the rebuilt board is held while covered");
            veil.Advance(.28f, false);
            Assert.That(veil.IsInFlight, Is.False);
            Assert.That(_root.Intro.IsVisible, Is.False);
        }
    }
}
