using System.Collections.Generic;
using System.Linq;
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
        public void FurnishedStations_KeepOneOwnedTintMaterialThroughDecorationAndTeardown()
        {
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(prefab.GetComponent<Collider>());
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null, "URP/Lit test precondition");
            var prefabMaterial = new Material(shader);
            prefabMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);
            prefab.GetComponent<Renderer>().sharedMaterial = prefabMaterial;
            var catalog = new PropModelCatalog(new[]
            {
                new PropModelCatalog.Entry(PropModelCatalog.StationKioskId,
                    prefab, 1f, 0f, Vector3.zero),
            });
            Assert.That(catalog.AdmittedEntryCount, Is.EqualTo(1));

            var before = new HashSet<int>(Resources.FindObjectsOfTypeAll<Material>()
                .Where(x => x.name.StartsWith("Board station — "))
                .Select(x => x.GetInstanceID()));
            var owned = new List<Material>();
            try
            {
                _boardHost = new GameObject("furnished-material-lifecycle-host");
                ImportedLevel level = VFixtures.Import(VFixtures.L001Bytes());
                _session = new GameSession(level);
                _board = BoardView.Build(level, _boardHost.transform, _session, catalog);

                foreach (BoardElementId station in _board
                    .GetComponentsInChildren<BoardElementId>(true)
                    .Where(x => x.Kind == "station"))
                {
                    Material anchor = station.GetComponent<Renderer>().sharedMaterial;
                    Renderer plate = station.transform.Find("station:plate-generated")
                        .GetComponent<Renderer>();
                    Assert.That(plate.sharedMaterial, Is.SameAs(anchor),
                        "the generated primary badge must retain the station's authoritative tint");
                    owned.Add(anchor);
                }

                Material[] created = Resources.FindObjectsOfTypeAll<Material>()
                    .Where(x => x.name.StartsWith("Board station — ")
                        && !before.Contains(x.GetInstanceID()))
                    .ToArray();
                Assert.That(created, Has.Length.EqualTo(owned.Count),
                    "decoration must not make a second renderer.material instance per station");

                Object.DestroyImmediate(_board.gameObject);
                _board = null;
                TestContext.Out.WriteLine("STATION_MATERIAL_TEARDOWN_READBACK created="
                    + created.Length + " destroyed=" + created.Count(x => x == null));
                foreach (Material material in created)
                    Assert.That(material == null, Is.True,
                        "BoardView must tear down every station material it creates");
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(prefabMaterial);
            }
        }

        [Test]
        public void PlatformBlend_MovesTheCatOutsideTheCarriageAndSwingsPlaceholderLegsOnly()
        {
            _view.PlaceOnEdge(_paths, 0, 1.5f);
            Vector3 authoritativeRoot = _view.transform.localPosition;
            _view.ApplyPresentation(CatPresentationState.Hidden, 0f, true);
            Vector3 seatedBaseline = Cat().localPosition;
            Vector3 seatedWorld = Cat().position;
            Vector3 pinBaseline = Pin().localPosition;
            Quaternion leftLegBaseline = Part("LegLeft").localRotation;
            Quaternion rightLegBaseline = Part("LegRight").localRotation;

            _view.ApplyPresentation(CatPresentationState.Walk, 1f, 0.73f, false);

            Assert.That(_view.transform.localPosition, Is.EqualTo(authoritativeRoot));
            Assert.That(Vector3.Distance(Cat().localPosition, seatedBaseline),
                Is.GreaterThan(ToyTrainView.PlatformSideOffset - 0.022f));
            Assert.That(Vector3.Distance(Pin().localPosition - pinBaseline,
                Cat().localPosition - seatedBaseline), Is.LessThan(0.0001f),
                "the destination card follows both the cat's path and its visual-only bob " +
                "instead of floating over the empty seat");
            Vector3 towardSeat = (seatedWorld - Cat().position).normalized;
            Assert.That(Vector3.Dot(Cat().TransformDirection(Vector3.right), towardSeat),
                Is.GreaterThan(0.9f), "Cat-local +X / rig forward faces along the path");
            Assert.That(Quaternion.Angle(Part("LegLeft").localRotation, leftLegBaseline),
                Is.GreaterThan(1f));
            Assert.That(Quaternion.Angle(Part("LegRight").localRotation, rightLegBaseline),
                Is.GreaterThan(1f));
            Assert.That(Quaternion.Angle(Part("LegLeft").localRotation,
                Part("LegRight").localRotation), Is.GreaterThan(2f));
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
            Vector3 pinBaseline = Pin().localPosition;
            Quaternion leftLegBaseline = Part("LegLeft").localRotation;
            _view.ApplyPresentation(CatPresentationState.Celebrate, 0.73f, false);
            _view.ApplyPresentation(CatPresentationState.Walk, 1f, 0.73f, false);
            _view.ApplyPresentation(CatPresentationState.Hidden, 0.73f, true);

            Assert.That(Cat().gameObject.activeSelf, Is.False);
            Assert.That(Cat().localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(Head().localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(EyeLeft().localScale, Is.EqualTo(_eyeBaseline));
            Assert.That(Pin().localPosition, Is.EqualTo(pinBaseline));
            Assert.That(Part("LegLeft").localRotation, Is.EqualTo(leftLegBaseline));
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
        public void BoardUpdateFrom_ManualDeliveryWalksOutWithoutMovingFallbackTrainRoot()
        {
            BuildBoard();
            SetLiveSlot(1, 0);
            _board.UpdateFrom(_session, 0f);
            Transform train = BoardTrain();
            Transform cat = train.Find("Carriage/Cat");
            Vector3 spawnPlatformPose = cat.localPosition;

            _board.UpdateFrom(_session, 0.5f);
            Vector3 seatedPose = cat.localPosition;
            Assert.That(Vector3.Distance(spawnPlatformPose, seatedPose),
                Is.GreaterThan(0.20f), "spawn presentation walks from platform into carriage");

            Vector3 authoritativeRoot = train.localPosition;
            _session.State.Trains[0] = default;
            _session.State.Deliveries = 1;
            _board.UpdateFrom(_session, 0.6f);
            _board.UpdateFrom(_session, 0.9f);

            Assert.That(train.localPosition, Is.EqualTo(authoritativeRoot));
            Assert.That(Vector3.Distance(cat.localPosition, seatedPose), Is.GreaterThan(0.15f),
                "delivered cat walks out to the adjacent platform while train root stays fixed");
        }

        [Test]
        public void BoardUpdateFrom_ActualSpawnKeepsPlatformEndpointFixedWhileTrainMoves()
        {
            BuildBoard(NonFinalReuseLevel());
            _session.AdvanceMs(TickInterpolator.TICK_MS); // tick 0 emits directly onto the edge
            Assert.That(_session.State.Trains[0].State, Is.EqualTo(TrainState.OnEdge));
            _board.UpdateFrom(_session, 0f);
            Transform train = BoardTrain();
            Transform cat = train.Find("Carriage/Cat");
            Vector3 platformWorld = cat.position;
            Vector3 firstRoot = train.position;

            // Keep presentation time fixed while the authoritative train advances one tick.
            // PlatformBlend remains 1, so only a world-anchored endpoint can keep Cat fixed.
            _session.AdvanceMs(TickInterpolator.TICK_MS);
            _board.UpdateFrom(_session, 0f);

            Assert.That(Vector3.Distance(train.position, firstRoot), Is.GreaterThan(0.1f));
            Assert.That(Vector3.Distance(cat.position, platformWorld), Is.LessThan(0.0001f));
        }

        [Test]
        public void BoardUpdateFrom_FirstObservationAfterHitchUsesRecordedSourcePlatform()
        {
            BuildBoard(NonFinalReuseLevel());
            _session.AdvanceMs(2 * TickInterpolator.TICK_MS); // emit, then advance mid-edge
            Assert.That(_session.State.Trains[0].ProgressTicks, Is.EqualTo(1));

            _board.UpdateFrom(_session, 0f);

            Transform train = BoardTrain();
            Transform cat = train.Find("Carriage/Cat");
            Vector3 catBoard = _board.transform.InverseTransformPoint(cat.position);
            Assert.That(catBoard.x, Is.EqualTo(-ToyTrainView.PlatformSideOffset).Within(0.04f));
            Assert.That(catBoard.y, Is.EqualTo(2f).Within(0.04f));
            Assert.That(Mathf.Abs(catBoard.y - train.localPosition.y), Is.GreaterThan(0.5f),
                "first observation anchors at source, not beside the mid-edge carriage");
        }

        [Test]
        public void BoardUpdateFrom_SourceQueueShowsAPlatformWaitThenBoardingWalk()
        {
            BuildBoard(SourceQueueWaitingLevel());
            _session.AdvanceMs(TickInterpolator.TICK_MS); // three tick-0 waves: one rides, two queue
            Assert.That(_session.State.Trains[1].State, Is.EqualTo(TrainState.AtNode));
            Assert.That(_session.State.Trains[2].State, Is.EqualTo(TrainState.AtNode));
            Assert.That(_session.TrainOccupantSpawnNode(1), Is.EqualTo(0));
            Assert.That(_session.TrainOccupantSpawnEdge(1), Is.EqualTo(0));
            _board.UpdateFrom(_session, 0f);

            var waiting = BoardTrain(1).GetComponent<ToyTrainView>();
            Transform carriage = waiting.transform.Find("Carriage");
            Transform cat = carriage.Find("Cat");
            Vector3 catBoard = _board.transform.InverseTransformPoint(cat.position);
            Transform boardingCat = BoardTrain(0).Find("Carriage/Cat");
            Transform boardingPin = BoardTrain(0).Find("Carriage/Pin");
            Transform waitingPin = BoardTrain(1).Find("Carriage/Pin");
            Assert.That(waiting.PresentationState, Is.EqualTo(CatPresentationState.WaitingIdle));
            Assert.That(catBoard.x,
                Is.EqualTo(-ToyTrainView.PlatformSideOffset).Within(0.04f));
            Assert.That(catBoard.y,
                Is.EqualTo(2.42f).Within(0.04f),
                "queued cat waits beside the source, not along its outgoing track");
            Assert.That(Vector3.Distance(boardingCat.position, cat.position),
                Is.GreaterThan(ToyTrainView.PlatformQueueSpacing - 0.04f),
                "the actively boarding cat and FIFO head occupy different platform lanes");
            Assert.That(Vector3.Distance(boardingPin.position, waitingPin.position),
                Is.GreaterThan(ToyTrainView.PlatformQueueSpacing - 0.04f),
                "their destination pins occupy those same distinct lanes");
            Assert.That(Vector3.Distance(cat.position, carriage.position), Is.GreaterThan(0.25f));
            Assert.That(cat.Find("Body").gameObject.activeSelf, Is.True,
                "platform wait shows the whole fallback cat, not a seated head");
            Transform secondCat = BoardTrain(2).Find("Carriage/Cat");
            Transform secondPin = BoardTrain(2).Find("Carriage/Pin");
            Vector3 secondCatBoard = _board.transform.InverseTransformPoint(secondCat.position);
            Assert.That(secondCatBoard.x,
                Is.EqualTo(-ToyTrainView.PlatformSideOffset).Within(0.04f));
            Assert.That(secondCatBoard.y,
                Is.EqualTo(1.58f).Within(0.04f));
            Assert.That(Vector3.Distance(secondCat.position, cat.position),
                Is.GreaterThan(ToyTrainView.PlatformQueueSpacing - 0.04f),
                "simultaneous source waiters do not coincide");
            Assert.That(Vector3.Distance(secondPin.position, waitingPin.position),
                Is.GreaterThan(ToyTrainView.PlatformQueueSpacing - 0.04f),
                "simultaneous source destination pins do not coincide");

            _session.AdvanceMs(TickInterpolator.TICK_MS); // queue head releases onto the edge
            _board.UpdateFrom(_session, 0.1f);

            Assert.That(waiting.PresentationState, Is.EqualTo(CatPresentationState.Walk));
            Transform advancedWaiter = BoardTrain(2).Find("Carriage/Cat");
            Transform newTail = BoardTrain(3).Find("Carriage/Cat");
            Vector3 advancedBoard = _board.transform.InverseTransformPoint(
                advancedWaiter.position);
            Vector3 newTailBoard = _board.transform.InverseTransformPoint(newTail.position);
            Assert.That(advancedBoard.y,
                Is.EqualTo(1.58f).Within(0.04f),
                "older waiter retains its non-colliding presentation lane through releases");
            Assert.That(Vector3.Distance(cat.position, advancedWaiter.position),
                Is.GreaterThan(ToyTrainView.PlatformQueueSpacing - 0.04f),
                "released boarding cat cannot collide with the new FIFO head");
            Assert.That(newTailBoard.y,
                Is.EqualTo(2.84f).Within(0.04f));
            Assert.That(Vector3.Distance(advancedWaiter.position, newTail.position),
                Is.GreaterThan(ToyTrainView.PlatformQueueSpacing - 0.04f),
                "release plus same-tick emission cannot collapse two waiters onto one anchor");
        }

        [Test]
        public void BoardUpdateFrom_MotionOffKeepsQueuedCatAtItsStaticPlatformEndpoint()
        {
            BuildBoard(SourceQueueWaitingLevel());
            _session.AdvanceMs(TickInterpolator.TICK_MS);
            _board.UpdateFrom(_session, 0f);
            var waiting = BoardTrain(1).GetComponent<ToyTrainView>();
            Transform cat = waiting.transform.Find("Carriage/Cat");

            _board.MotionOffSource = () => true;
            _board.UpdateFrom(_session, 0.4f);

            Vector3 catBoard = _board.transform.InverseTransformPoint(cat.position);
            Assert.That(waiting.PresentationState, Is.EqualTo(CatPresentationState.WaitingIdle));
            Assert.That(catBoard.x,
                Is.EqualTo(-ToyTrainView.PlatformSideOffset).Within(0.0001f));
            Assert.That(catBoard.y,
                Is.EqualTo(2.42f).Within(0.0001f));
            Assert.That(cat.Find("Body").gameObject.activeSelf, Is.True);
            Vector3 staticEndpoint = cat.position;

            _board.UpdateFrom(_session, 0.9f);

            Assert.That(cat.position, Is.EqualTo(staticEndpoint),
                "motion-off removes phase motion without removing the platform-wait information");
        }

        [Test]
        public void BoardUpdateFrom_ActualDeliveryPlacesRetainedConsistAtRecordedStation()
        {
            BuildBoard(NonFinalReuseLevel());
            _session.AdvanceMs(TickInterpolator.TICK_MS); // tick 0: emit
            _board.UpdateFrom(_session, 0f);
            _session.AdvanceMs(TickInterpolator.TICK_MS); // tick 1: approach
            _board.UpdateFrom(_session, 0.1f);
            _session.AdvanceMs(TickInterpolator.TICK_MS); // tick 2: deliver at node 1
            Assert.That(_session.State.Trains[0].State, Is.EqualTo(TrainState.None));
            Assert.That(_session.TrainDeliveryNode(0), Is.EqualTo(1));

            _board.UpdateFrom(_session, 0.2f);

            Transform train = BoardTrain();
            Vector3 stationLocal = _board.transform.InverseTransformPoint(_board.NodeWorldPos(1));
            Assert.That(train.localPosition.x, Is.EqualTo(stationLocal.x).Within(0.0001f));
            Assert.That(train.localPosition.y, Is.EqualTo(stationLocal.y).Within(0.0001f));
            Assert.That(train.GetComponent<ToyTrainView>().PresentationState,
                Is.EqualTo(CatPresentationState.Alight));
            Transform carriage = train.Find("Carriage");
            Transform cat = carriage.Find("Cat");
            Vector3 outward = (cat.position - carriage.position).normalized;
            Assert.That(Vector3.Distance(cat.position, carriage.position),
                Is.GreaterThan(0.15f), "delivery recaptures a platform-side station anchor");
            Assert.That(Vector3.Dot(cat.TransformDirection(Vector3.right), outward),
                Is.GreaterThan(0.9f), "alighting rig forward faces the destination platform");
        }

        [Test]
        public void BoardUpdateFrom_TwoCollapsedLifecyclesHidesTheStaleDeliveredCat()
        {
            BuildBoard(TwoCollapsedLifecyclesLevel());
            _session.AdvanceMs(TickInterpolator.TICK_MS); // tick 0: cat A emits
            _board.UpdateFrom(_session, 0f);
            var retained = BoardTrain().GetComponent<ToyTrainView>();
            long visibleKey = retained.PresentationOccupantKey;

            // tick 1 delivers A; tick 2 emits unseen B in the same fixed slot; tick 3
            // delivers B. The latest node belongs to B, but the retained renderer is still A.
            _session.AdvanceMs(3 * TickInterpolator.TICK_MS);
            Assert.That(_session.State.Trains[0].State, Is.EqualTo(TrainState.None));
            Assert.That(_session.State.Deliveries, Is.EqualTo(2));
            Assert.That(_session.TrainDeliveryGeneration(0), Is.EqualTo(2));

            _board.UpdateFrom(_session, 0.1f);

            Assert.That(retained.PresentationOccupantKey, Is.EqualTo(visibleKey));
            Assert.That(retained.PresentationState, Is.EqualTo(CatPresentationState.Hidden));
            Assert.That(retained.gameObject.activeSelf, Is.False,
                "never replay visible cat A at unseen cat B's latest delivery node");
        }

        [Test]
        public void BoardUpdateFrom_UnseenDeliveryDoesNotHijackAnOlderDeparture()
        {
            BuildBoard(TwoStationLingeringDepartureLevel());
            _session.AdvanceMs(TickInterpolator.TICK_MS); // tick 0: red emits
            _board.UpdateFrom(_session, 0f);
            _session.AdvanceMs(TickInterpolator.TICK_MS); // tick 1: red takes red route
            _board.UpdateFrom(_session, 0.1f);
            _session.AdvanceMs(TickInterpolator.TICK_MS); // tick 2: red delivers
            _board.UpdateFrom(_session, 0.2f);
            var retained = BoardTrain().GetComponent<ToyTrainView>();
            Vector3 redStationRoot = retained.transform.localPosition;
            Assert.That(retained.PresentationState, Is.EqualTo(CatPresentationState.Alight));

            // The rendered snapshot is now empty while red's departure lingers. Blue emits,
            // takes the toggled route, and delivers entirely between presentation frames.
            _session.EnqueueToggle(0);
            _session.AdvanceMs(3 * TickInterpolator.TICK_MS);
            Assert.That(_session.State.Deliveries, Is.EqualTo(2));
            Assert.That(_session.TrainDeliveryNode(0), Is.EqualTo(3));

            _board.UpdateFrom(_session, 0.3f);

            Assert.That(retained.transform.localPosition, Is.EqualTo(redStationRoot),
                "unseen blue delivery must not move lingering red to the blue station");
            Assert.That(retained.PresentationState, Is.EqualTo(CatPresentationState.Alight));
            Assert.That(retained.gameObject.activeSelf, Is.True);
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
            int occupantGenerationBefore = _session.TrainOccupantGeneration(0);
            int spawnNodeBefore = _session.TrainOccupantSpawnNode(0);
            int spawnEdgeBefore = _session.TrainOccupantSpawnEdge(0);
            int deliveryGenerationBefore = _session.TrainDeliveryGeneration(0);
            int deliveryNodeBefore = _session.TrainDeliveryNode(0);
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
            Assert.That(_session.TrainOccupantGeneration(0),
                Is.EqualTo(occupantGenerationBefore));
            Assert.That(_session.TrainOccupantSpawnNode(0), Is.EqualTo(spawnNodeBefore));
            Assert.That(_session.TrainOccupantSpawnEdge(0), Is.EqualTo(spawnEdgeBefore));
            Assert.That(_session.TrainDeliveryGeneration(0),
                Is.EqualTo(deliveryGenerationBefore));
            Assert.That(_session.TrainDeliveryNode(0), Is.EqualTo(deliveryNodeBefore));
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
        public void BoardUpdateFrom_SessionGenerationFindsSameColourRefillBeforeFinalSkippedStep()
        {
            BuildBoard(NonFinalReuseLevel());
            _session.AdvanceMs(125d); // tick 0: first red cat emits in fixed slot/id 1
            _board.UpdateFrom(_session, 0f);
            long firstOccupantKey = BoardTrain(0).GetComponent<ToyTrainView>()
                .PresentationOccupantKey;
            int firstGeneration = _session.TrainOccupantGeneration(0);

            // Delivery is tick 2, refill tick 3, and tick 4 advances the replacement. Both
            // endpoint slots and PrevTrains are now live red id 1; only GameSession's read-only
            // per-step generation preserves the otherwise-lost occupant boundary.
            _session.AdvanceMs(500d);
            Assert.That(_session.State.Deliveries, Is.EqualTo(1));
            Assert.That(_session.State.Trains[0].Id, Is.EqualTo(1));
            Assert.That(_session.State.Trains[0].Color, Is.EqualTo(CatColor.Red));
            Assert.That(_session.PrevTrains[0].Id, Is.EqualTo(1));
            Assert.That(_session.TrainOccupantGeneration(0), Is.EqualTo(firstGeneration + 1));

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
        private Transform Pin() => _view.transform.Find("Carriage/Pin");
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

        private static byte[] NonFinalReuseLevel() => VFixtures.Level(o =>
        {
            o["meta"]["mechanics"] = new JArray();
            o["meta"]["newMechanic"] = null;
            o["board"]["nodes"] = new JArray(
                VFixtures.Node("SRC", 0, 2), VFixtures.Node("RED", 0, 0));
            o["board"]["edges"] = new JArray(VFixtures.Edge("E1", "SRC", "RED", 2));
            o["sources"] = new JArray(Source("SRC", "red"));
            o["stations"] = new JArray(VFixtures.Station("RED", 3, "red"));
            o["switches"] = new JArray();
            o["waves"] = new JArray(VFixtures.Wave(0, "red", 2, 3));
            o["win"]["deliveries"] = 2;
            o["win"]["timeLimitTicks"] = 20;
        });

        private static byte[] SourceQueueWaitingLevel() => VFixtures.Level(o =>
        {
            o["meta"]["mechanics"] = new JArray("queue");
            o["meta"]["newMechanic"] = null;
            o["board"]["nodes"] = new JArray(
                VFixtures.Node("SRC", 0, 2), VFixtures.Node("RED", 0, 0));
            o["board"]["edges"] = new JArray(VFixtures.Edge("E1", "SRC", "RED", 3));
            o["sources"] = new JArray(Source("SRC", "red"));
            o["stations"] = new JArray(VFixtures.Station("RED", 3, "red"));
            o["switches"] = new JArray();
            o["waves"] = new JArray(
                VFixtures.Wave(0, "red", 1, 1),
                VFixtures.Wave(0, "red", 1, 1),
                VFixtures.Wave(0, "red", 1, 1),
                VFixtures.Wave(1, "red", 1, 1));
            o["win"]["deliveries"] = 4;
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

        private static byte[] TwoCollapsedLifecyclesLevel() => VFixtures.Level(o =>
        {
            o["meta"]["mechanics"] = new JArray();
            o["meta"]["newMechanic"] = null;
            o["board"]["nodes"] = new JArray(
                VFixtures.Node("SRC", 0, 1), VFixtures.Node("RED", 0, 0));
            o["board"]["edges"] = new JArray(VFixtures.Edge("E1", "SRC", "RED", 1));
            o["sources"] = new JArray(Source("SRC", "red"));
            o["stations"] = new JArray(VFixtures.Station("RED", 3, "red"));
            o["switches"] = new JArray();
            o["waves"] = new JArray(VFixtures.Wave(0, "red", 2, 2));
            o["win"]["deliveries"] = 2;
            o["win"]["timeLimitTicks"] = 20;
        });

        private static byte[] TwoStationLingeringDepartureLevel() => VFixtures.Level(o =>
        {
            o["meta"]["band"] = "alternation";
            o["meta"]["mechanics"] = new JArray("switch", "queue");
            o["meta"]["newMechanic"] = null;
            o["board"]["nodes"] = new JArray(
                VFixtures.Node("SRC", 0, 3), VFixtures.Node("J1", 0, 2),
                VFixtures.Node("RED", -1, 1), VFixtures.Node("BLU", 1, 1));
            o["board"]["edges"] = new JArray(
                VFixtures.Edge("E0", "SRC", "J1", 1),
                VFixtures.Edge("ER", "J1", "RED", 1),
                VFixtures.Edge("EB", "J1", "BLU", 1));
            o["sources"] = new JArray(new JObject
            {
                ["nodeId"] = "SRC", ["allowedColors"] = new JArray("red", "blue"),
            });
            o["stations"] = new JArray(
                VFixtures.Station("RED", 3, "red"),
                VFixtures.Station("BLU", 3, "blue"));
            o["switches"] = new JArray(VFixtures.Switch("S1", "J1", 0, "ER", "EB"));
            o["waves"] = new JArray(
                VFixtures.Wave(0, "red", 1, 1),
                VFixtures.Wave(3, "blue", 1, 1));
            o["win"]["deliveries"] = 2;
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
