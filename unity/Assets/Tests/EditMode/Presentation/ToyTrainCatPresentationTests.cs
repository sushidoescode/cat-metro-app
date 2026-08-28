using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Cats;
using CatMetro.Presentation.Props;
using CatMetro.Tests.Validation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatMetro.Tests.EditMode.Presentation
{
    public sealed class ToyTrainCatPresentationTests
    {
        private GameObject _host;
        private ToyTrainView _view;
        private TrackSplineGraph _paths;
        private Vector3 _eyeBaseline;
        private GameObject _boardHost;
        private BoardView _board;
        private GameSession _session;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("cat-presentation-host");
            _paths = TrackSplineGraph.Build(new[] { Vector3.zero, new Vector3(3f, 0f, 0f) },
                new[] { 0 }, new[] { 1 });
            _view = ToyTrainView.Create(_host.transform, "train:cat", new[] { 0 }, new[] { 1 });
            _view.SyncSlot(41, CatMetro.Domain.CatColor.Red);
            _eyeBaseline = EyeLeft().localScale;
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_boardHost != null) Object.DestroyImmediate(_boardHost);
        }

        [Test]
        public void ExplicitPresentationInput_NeverFeedsBobBackIntoTheAuthoritativeRootPose()
        {
            _view.PlaceOnEdge(_paths, 0, 1.5f);
            Vector3 authoritativeRoot = _view.transform.localPosition;

            _view.ApplyPresentation(CatPresentationState.Alight, 0.73f, false);

            Assert.That(_view.transform.localPosition, Is.EqualTo(authoritativeRoot));
            Assert.That(Cat().localPosition, Is.Not.EqualTo(Vector3.zero));
        }

        [Test]
        public void DeliveryAdvance_IsDerivedFromCopiedSlotValuesAndCounterWithoutSlotMutation()
        {
            var previous = new TrainSlot { Id = 41, State = TrainState.AtNode };
            var current = default(TrainSlot);

            bool advanced = BoardView.DeliveryAdvancedForPresentation(previous, current, 2, 3);

            Assert.That(advanced, Is.True);
            Assert.That(previous.Id, Is.EqualTo(41));
            Assert.That(previous.State, Is.EqualTo(TrainState.AtNode));
            Assert.That(current.Id, Is.EqualTo(0));
        }

        [Test]
        public void MotionOff_ResetsExactNeutralPartsAndHidesDepartureVisualImmediately()
        {
            _view.ApplyPresentation(CatPresentationState.Celebrate, 0.73f, false);
            _view.ApplyPresentation(CatPresentationState.Hidden, 0.73f, true);

            Assert.That(Cat().gameObject.activeSelf, Is.False);
            Assert.That(Cat().localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(Head().localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(EyeLeft().localScale, Is.EqualTo(_eyeBaseline));
        }

        [Test]
        public void ReusedSlot_InterruptsLingerWithNeutralPoseAndNewTintHistory()
        {
            _view.ApplyPresentation(CatPresentationState.Celebrate, 0.73f, false);
            _view.SyncSlot(42, CatMetro.Domain.CatColor.Blue);

            Assert.That(Cat().localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(Head().localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(_view.PresentationState, Is.EqualTo(CatPresentationState.Hidden));
        }

        [Test]
        public void PlaceholderBodyAndLegs_UseBuiltinBoundsForTheirWorldDimensionsWithoutInteractionComponents()
        {
            _view.ApplyPresentation(CatPresentationState.Walk, 0f, false);

            Assert.That(Part("Body").gameObject.activeSelf, Is.True);
            Assert.That(Part("LegLeft").gameObject.activeSelf, Is.True);
            Assert.That(Vector3.Distance(WorldMeshSize(Part("Body")),
                ToyTrainView.PlaceholderBodyWorldSize), Is.LessThan(0.0001f));
            Assert.That(Cat().GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(Cat().GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(Cat().GetComponentsInChildren<BoardElementId>(true), Is.Empty);
            Assert.That(Cat().GetComponentsInChildren<Selectable>(true), Is.Empty);
            Assert.That(Cat().GetComponentsInChildren<BaseRaycaster>(true), Is.Empty);
        }

        [Test]
        public void IdleStates_HidePlaceholderBodyAndLegs()
        {
            _view.ApplyPresentation(CatPresentationState.RideIdle, 0f, false);

            Assert.That(Part("Body").gameObject.activeSelf, Is.False);
            Assert.That(Part("LegLeft").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void NonzeroHeading_BobProjectsVerticallyAtTheExactBoardAmplitude()
        {
            var vertical = TrackSplineGraph.Build(new[] { Vector3.zero, new Vector3(0f, 3f, 0f) },
                new[] { 0 }, new[] { 1 });
            _view.PlaceOnEdge(vertical, 0, 1.5f);
            Vector3 neutral = BoardSceneLook.BoardTilt * Cat().position;
            float expectedAmplitude = Mathf.Abs(new CatMicroMotion(41u)
                .Evaluate(0.73f, false, true).Bob) * 0.021f;

            _view.ApplyPresentation(CatPresentationState.Alight, 0.73f, false);
            Vector3 bobbed = BoardSceneLook.BoardTilt * Cat().position;

            Assert.That(Mathf.Abs(bobbed.x - neutral.x), Is.LessThan(0.0001f));
            Assert.That(Mathf.Abs(bobbed.y - neutral.y), Is.EqualTo(expectedAmplitude).Within(0.0001f));
        }

        [Test]
        public void BlinkScaleAxis_ProjectsVerticallyAtTheFixedCatYaw()
        {
            Vector3 projected = BoardSceneLook.BoardTilt * EyeLeft().TransformDirection(Vector3.up);

            Assert.That(Mathf.Abs(projected.x), Is.LessThan(0.0001f));
            Assert.That(projected.y, Is.GreaterThan(0f));
        }

        [Test]
        public void ArrivalHeadTurn_MovesFaceFeaturesWithoutChangingTheCatYaw()
        {
            Quaternion catYaw = Cat().localRotation;
            Vector3 neutralEye = EyeLeft().localPosition;
            CatMicroPose pose = new CatMicroMotion(41u).Evaluate(0.73f, false, true);
            Assert.That(Mathf.Abs(pose.ArrivalHeadTurnDegrees), Is.GreaterThan(0.001f));

            _view.ApplyPresentation(CatPresentationState.Alight, 0.73f, false);

            Assert.That(Cat().localRotation, Is.EqualTo(catYaw));
            Assert.That(EyeLeft().localPosition, Is.Not.EqualTo(neutralEye));
            Assert.That(Quaternion.Angle(Head().localRotation, Quaternion.identity), Is.LessThanOrEqualTo(16.001f));
        }

        [Test]
        public void BoardUpdateFrom_DeliveryLingerMotionOffAndResumeDoNotResurrectTheDeadSlot()
        {
            BuildBoard();
            SetLiveSlot(17, 0);
            _board.UpdateFrom(_session, 0f);
            Transform train = BoardTrain();
            Vector3 lastAuthoritativePose = train.localPosition;

            _session.State.Trains[0] = default;
            _session.State.Deliveries = 1;
            _board.UpdateFrom(_session, 0.1f);
            Assert.That(train.gameObject.activeSelf, Is.True);
            Assert.That(train.localPosition, Is.EqualTo(lastAuthoritativePose));

            _board.MotionOffSource = () => true;
            _board.UpdateFrom(_session, 0.2f);
            Assert.That(train.gameObject.activeSelf, Is.False);

            _board.MotionOffSource = () => false;
            _board.UpdateFrom(_session, 1f);
            Assert.That(train.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void BoardUpdateFrom_NewIdInterruptsDeadLingerAndDoesNotMutateTheSession()
        {
            BuildBoard();
            SetLiveSlot(17, 0);
            _board.UpdateFrom(_session, 0f);
            _session.State.Trains[0] = default;
            _session.State.Deliveries = 1;
            _board.UpdateFrom(_session, 0.1f);

            SetLiveSlot(99, 1);
            TrainSlot before = _session.State.Trains[0];
            int deliveriesBefore = _session.State.Deliveries;
            _board.UpdateFrom(_session, 0.2f);

            var train = BoardTrain().GetComponent<ToyTrainView>();
            Assert.That(train.gameObject.activeSelf, Is.True);
            Assert.That(train.PresentationState, Is.EqualTo(CatPresentationState.Walk));
            Assert.That(_session.State.Trains[0].Id, Is.EqualTo(before.Id));
            Assert.That(_session.State.Trains[0].NodeId, Is.EqualTo(before.NodeId));
            Assert.That(_session.State.Trains[0].State, Is.EqualTo(before.State));
            Assert.That(_session.State.Deliveries, Is.EqualTo(deliveriesBefore));
        }

        private Transform Cat() => _view.transform.Find("Carriage/Cat");
        private Transform Head() => Part("Head");
        private Transform EyeLeft() => Part("EyeLeft");
        private Transform Part(string name) => Cat().Find(name);

        private void BuildBoard()
        {
            _boardHost = new GameObject("board-presentation-host");
            ImportedLevel level = VFixtures.Import(VFixtures.L001Bytes());
            _session = new GameSession(level);
            _board = BoardView.Build(level, _boardHost.transform, _session, PropModelCatalog.Empty);
        }

        private void SetLiveSlot(short id, short nodeId)
        {
            _session.State.Trains[0] = new TrainSlot
            {
                Id = id,
                Color = CatColor.Red,
                NodeId = nodeId,
                State = TrainState.AtNode,
            };
        }

        private Transform BoardTrain() => _board.transform.Find("train:0");
        private static Vector3 WorldMeshSize(Transform part)
        {
            Vector3 bounds = part.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            return Vector3.Scale(bounds, part.lossyScale);
        }
    }
}
