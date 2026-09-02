using CatMetro.Application.Session;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Input;
using CatMetro.Presentation.Props;
using CatMetro.Tests.Validation;
using NUnit.Framework;
using UnityEngine;

namespace CatMetro.Tests.Presentation
{
    public sealed class TapInputFeedbackTests
    {
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            _host = null;
        }

        [Test]
        public void RetryFeedback_IsNullSafeAndExceptionIsolated_FromTheExistingAction()
        {
            var input = CreateInput();
            int feedback = 0;
            int retried = 0;
            input.UiTapAccepted += () => throw new System.InvalidOperationException("audio fault");
            input.UiTapAccepted += () => feedback++;
            input.RetryRegionActive = () => true;
            input.RetryTapped = () => retried++;

            int result = input.HandleTapAtScreen(new Vector2(0f, -1f));

            Assert.That(result, Is.EqualTo(-2));
            Assert.That(feedback, Is.EqualTo(1),
                "one bad feedback subscriber cannot suppress another subscriber");
            Assert.That(retried, Is.EqualTo(1),
                "feedback failure must never block the established retry verb");

            input.UiTapAccepted = null;
            Assert.That(input.HandleTapAtScreen(new Vector2(0f, -1f)), Is.EqualTo(-2));
            Assert.That(retried, Is.EqualTo(2), "a missing feedback presenter is a no-op");
        }

        [Test]
        public void ChromeFeedback_CuesWoodTapOnly_ButBothRegionsKeepTheirActions()
        {
            var input = CreateInput();
            int feedback = 0;
            int actions = 0;
            input.UiTapAccepted = () => feedback++;
            var rect = new Rect(0f, 0f, 100f, 100f);
            input.Regions.Register("blocker", () => rect, () => actions++, 0,
                ChromeFeedback.None);

            Assert.That(input.HandleTapAtScreen(new Vector2(50f, 50f)), Is.EqualTo(-3));
            Assert.That(actions, Is.EqualTo(1));
            Assert.That(feedback, Is.Zero, "a consuming modal blocker is not a button");

            Assert.That(input.Regions.Unregister("blocker"), Is.True);
            input.Regions.Register("button", () => rect, () => actions++, 0);
            Assert.That(input.HandleTapAtScreen(new Vector2(50f, 50f)), Is.EqualTo(-3));
            Assert.That(actions, Is.EqualTo(2));
            Assert.That(feedback, Is.EqualTo(1), "default chrome feedback is one wood tap");

            Assert.That(input.HandleTapAtScreen(new Vector2(-10_000f, -10_000f)), Is.EqualTo(-1));
            Assert.That(feedback, Is.EqualTo(1), "a miss never requests feedback");
        }

        [Test]
        public void SwitchFeedback_FiresOnceForAcceptedTap_AndNeverForGateOrMiss()
        {
            var level = VFixtures.Import(VFixtures.L001Bytes());
            var session = new GameSession(level);
            var input = CreateInput();
            var view = BoardView.Build(level, _host.transform, session, PropModelCatalog.Empty);
            var cameraGo = new GameObject("FeedbackTestCamera");
            cameraGo.transform.SetParent(_host.transform, false);
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            Vector3 switchWorld = view.SwitchWorldPos(0);
            camera.transform.position = switchWorld + Vector3.back * 10f;
            input.Wire(session, view, camera);

            int feedback = 0;
            input.SwitchTapAccepted += () =>
                throw new System.InvalidOperationException("audio fault");
            input.SwitchTapAccepted += () => feedback++;
            Vector2 switchScreen = camera.WorldToScreenPoint(switchWorld);
            int commandsBefore = session.Log.Entries.Count;

            input.BoardInputActive = () => false;
            Assert.That(input.HandleTapAtScreen(switchScreen), Is.EqualTo(-1));
            Assert.That(feedback, Is.Zero);
            Assert.That(session.Log.Entries.Count, Is.EqualTo(commandsBefore));

            input.BoardInputActive = () => true;
            Assert.That(input.HandleTapAtScreen(new Vector2(-10_000f, -10_000f)), Is.EqualTo(-1));
            Assert.That(feedback, Is.Zero);

            Assert.That(input.HandleTapAtScreen(switchScreen), Is.EqualTo(0));
            Assert.That(feedback, Is.EqualTo(1));
            Assert.That(session.Log.Entries.Count, Is.EqualTo(commandsBefore + 1),
                "feedback failure must never block the accepted switch command");
        }

        private TapInput CreateInput()
        {
            _host = new GameObject("TapInputFeedbackTests");
            return _host.AddComponent<TapInput>();
        }
    }
}
