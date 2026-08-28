using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Presentation.Board;
using CatMetro.Presentation.Cats;
using CatMetro.Presentation.Props;
using CatMetro.Tests.Validation;
using Newtonsoft.Json.Linq;
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
            _view.SyncSlot(41L, CatMetro.Domain.CatColor.Red);
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
            var previous = new TrainSlot { Id = 1, State = TrainState.AtNode };
            var current = default(TrainSlot);

            bool advanced = BoardView.DeliveryAdvancedForPresentation(previous, current, 2, 3);

            Assert.That(advanced, Is.True);
            Assert.That(previous.Id, Is.EqualTo(1));
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
        public void NewPresentationOccupantKey_InterruptsLingerWithNeutralPoseAndNewTintHistory()
        {
            _view.ApplyPresentation(CatPresentationState.Celebrate, 0.73f, false);
            _view.SyncSlot(42L, CatMetro.Domain.CatColor.Blue);

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
            Assert.That(Cat().GetComponentsInChildren<Collider2D>(true), Is.Empty);
            Assert.That(Cat().GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
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
            SetLiveSlot(1, 0);
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

            _board.UpdateFrom(_session, 0.3f);
            _board.UpdateFrom(_session, 0.4f);
            Assert.That(train.gameObject.activeSelf, Is.False,
                "repeated motion-off frames keep the cancelled slot hidden");

            _board.MotionOffSource = () => false;
            _board.UpdateFrom(_session, 1f);
            Assert.That(train.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void BoardUpdateFrom_SameSlotIdReuseGetsANewPresentationKeyWithoutMutatingSession()
        {
            BuildBoard();
            SetLiveSlot(1, 0);
            _board.UpdateFrom(_session, 0f);
            long firstOccupantKey = BoardTrain().GetComponent<ToyTrainView>().PresentationOccupantKey;
            _session.State.Trains[0] = default;
            _session.State.Deliveries = 1;
            _board.UpdateFrom(_session, 0.1f);

            SetLiveSlot(1, 1);
            _session.EnqueueToggle(0);
            _session.AdvanceMs(42d);
            byte[] stateDigestBefore = StateDigest(_session);
            TrainSlot[] previousTrainsBefore = CopySlots(_session.PrevTrains);
            ToggleSwitchCommand[] logBefore = CopyLog(_session);
            int logFormatBefore = _session.Log.FormatVersion;
            double alphaBefore = _session.Alpha;
            _board.UpdateFrom(_session, 0.2f);

            var train = BoardTrain().GetComponent<ToyTrainView>();
            Assert.That(train.gameObject.activeSelf, Is.True);
            Assert.That(train.PresentationState, Is.EqualTo(CatPresentationState.Walk));
            Assert.That(train.PresentationOccupantKey, Is.Not.EqualTo(firstOccupantKey));
            Assert.That(StateDigest(_session), Is.EqualTo(stateDigestBefore));
            AssertTrainSlotsEqual(_session.PrevTrains, previousTrainsBefore);
            Assert.That(_session.Log.FormatVersion, Is.EqualTo(logFormatBefore));
            Assert.That(_session.Log.Entries.Count, Is.EqualTo(logBefore.Length));
            for (int i = 0; i < logBefore.Length; i++)
            {
                Assert.That(_session.Log.Entries[i].SwitchId, Is.EqualTo(logBefore[i].SwitchId));
                Assert.That(_session.Log.Entries[i].Tick, Is.EqualTo(logBefore[i].Tick));
            }
            Assert.That(_session.Alpha, Is.EqualTo(alphaBefore));
        }

        [Test]
        public void BoardUpdateFrom_FinalStepEmptySnapshotProvesSameIdCatchUpReplacement()
        {
            BuildBoard(ImmediateReuseLevel());
            _session.AdvanceMs(125d); // tick 0: slot 0 emits with Domain id 1
            Assert.That(_session.State.Trains[0].Id, Is.EqualTo(1));
            _board.UpdateFrom(_session, 0f);
            long firstOccupantKey = BoardTrain(0).GetComponent<ToyTrainView>()
                .PresentationOccupantKey;

            // tick 1 delivers the first cat; tick 2 emits the second into the same slot. The
            // rendered endpoints are both live with id 1, while PrevTrains retains the empty
            // slot copied immediately before the final (emission) step.
            _session.AdvanceMs(250d);
            Assert.That(_session.State.Deliveries, Is.EqualTo(1));
            Assert.That(_session.PrevTrains[0].Id, Is.EqualTo(0));
            Assert.That(_session.State.Trains[0].Id, Is.EqualTo(1));

            _board.UpdateFrom(_session, 0.1f);

            var replacement = BoardTrain(0).GetComponent<ToyTrainView>();
            Assert.That(replacement.PresentationOccupantKey, Is.Not.EqualTo(firstOccupantKey));
            Assert.That(replacement.PresentationState, Is.EqualTo(CatPresentationState.Walk));
        }

        [Test]
        public void BoardUpdateFrom_TwoDeliveredSlotsDepartWhileUnchangedLiveSlotKeepsItsGeneration()
        {
            BuildBoard(TwoDeliveriesAndOneRiderLevel());
            // One source emits on ticks 0, 1 and 2 onto a three-tick edge. After tick 2 the
            // real simulation has three staggered live slots with fixed ids 1, 2 and 3.
            _session.AdvanceMs(375d);
            _board.UpdateFrom(_session, 0f);
            long ridingKey = BoardTrain(2).GetComponent<ToyTrainView>().PresentationOccupantKey;

            _session.AdvanceMs(250d); // ticks 3 and 4 deliver slots 0 and 1; slot 2 remains live
            Assert.That(_session.State.Deliveries, Is.EqualTo(2));
            Assert.That(_session.State.Trains[0].Id, Is.EqualTo(0));
            Assert.That(_session.State.Trains[1].Id, Is.EqualTo(0));
            Assert.That(_session.State.Trains[2].Id, Is.EqualTo(3));

            _board.UpdateFrom(_session, 0.3f);

            Assert.That(BoardTrain(0).GetComponent<ToyTrainView>().PresentationState,
                Is.EqualTo(CatPresentationState.Alight));
            Assert.That(BoardTrain(1).GetComponent<ToyTrainView>().PresentationState,
                Is.EqualTo(CatPresentationState.Alight));
            var rider = BoardTrain(2).GetComponent<ToyTrainView>();
            Assert.That(rider.PresentationOccupantKey, Is.EqualTo(ridingKey),
                "a delivery elsewhere must not globally restart a live cat");
            Assert.That(rider.PresentationState, Is.EqualTo(CatPresentationState.Board));
        }

        private Transform Cat() => _view.transform.Find("Carriage/Cat");
        private Transform Head() => Part("Head");
        private Transform EyeLeft() => Part("EyeLeft");
        private Transform Part(string name) => Cat().Find(name);

        private void BuildBoard(byte[] levelBytes = null)
        {
            _boardHost = new GameObject("board-presentation-host");
            ImportedLevel level = VFixtures.Import(levelBytes ?? VFixtures.L001Bytes());
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

        private Transform BoardTrain(int slot = 0) => _board.transform.Find("train:" + slot);

        private static byte[] ImmediateReuseLevel() => VFixtures.Level(o =>
        {
            o["meta"]["mechanics"] = new JArray();
            o["meta"]["newMechanic"] = null;
            o["board"]["nodes"] = new JArray(
                VFixtures.Node("SRC", 0, 1), VFixtures.Node("RED", 0, 0));
            o["board"]["edges"] = new JArray(VFixtures.Edge("E1", "SRC", "RED", 1));
            o["sources"] = new JArray(new JObject
            {
                ["nodeId"] = "SRC", ["allowedColors"] = new JArray("red"),
            });
            o["stations"] = new JArray(VFixtures.Station("RED", 3, "red"));
            o["switches"] = new JArray();
            o["waves"] = new JArray(VFixtures.Wave(0, "red", 2, 2));
            o["win"]["deliveries"] = 2;
            o["win"]["timeLimitTicks"] = 20;
        });

        private static byte[] TwoDeliveriesAndOneRiderLevel() => VFixtures.Level(o =>
        {
            o["meta"]["mechanics"] = new JArray();
            o["meta"]["newMechanic"] = null;
            o["board"]["nodes"] = new JArray(
                VFixtures.Node("SRC", 0, 3), VFixtures.Node("RED", 0, 0));
            o["board"]["edges"] = new JArray(VFixtures.Edge("E1", "SRC", "RED", 3));
            o["sources"] = new JArray(Source("SRC", "red"));
            o["stations"] = new JArray(VFixtures.Station("RED", 3, "red"));
            o["switches"] = new JArray();
            o["waves"] = new JArray(VFixtures.Wave(0, "red", 3, 1));
            o["win"]["deliveries"] = 3;
            o["win"]["timeLimitTicks"] = 20;
        });

        private static JObject Source(string nodeId, string color) => new JObject
        {
            ["nodeId"] = nodeId,
            ["allowedColors"] = new JArray(color),
        };

        private static byte[] StateDigest(GameSession session)
        {
            var digest = new byte[session.State.DigestLength()];
            session.State.WriteDigest(digest);
            return digest;
        }

        private static TrainSlot[] CopySlots(TrainSlot[] slots)
        {
            var copy = new TrainSlot[slots.Length];
            for (int i = 0; i < slots.Length; i++) copy[i] = slots[i];
            return copy;
        }

        private static ToggleSwitchCommand[] CopyLog(GameSession session)
        {
            var copy = new ToggleSwitchCommand[session.Log.Entries.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = session.Log.Entries[i];
            return copy;
        }

        private static void AssertTrainSlotsEqual(TrainSlot[] actual, TrainSlot[] expected)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < actual.Length; i++)
            {
                Assert.That(actual[i].Id, Is.EqualTo(expected[i].Id));
                Assert.That(actual[i].Color, Is.EqualTo(expected[i].Color));
                Assert.That(actual[i].EdgeId, Is.EqualTo(expected[i].EdgeId));
                Assert.That(actual[i].ProgressTicks, Is.EqualTo(expected[i].ProgressTicks));
                Assert.That(actual[i].NodeId, Is.EqualTo(expected[i].NodeId));
                Assert.That(actual[i].State, Is.EqualTo(expected[i].State));
            }
        }

        private static Vector3 WorldMeshSize(Transform part)
        {
            Vector3 bounds = part.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            return Vector3.Scale(bounds, part.lossyScale);
        }
    }
}
