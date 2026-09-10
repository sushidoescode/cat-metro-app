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
            // The directly constructed view specifies the licence-neutral fallback. Inject it
            // explicitly so a workstation-only rig cannot silently turn its placeholder
            // geometry checks into claims about a different rendered cat.
            _view = ToyTrainView.Create(_host.transform, "train:cat", new[] { 0 }, new[] { 1 },
                new CatModelCatalog(null));
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
        public void FallbackPlatformEndpoint_KeepsTheReadableHeadClearOfTheWholeCarriage()
        {
            _view.PlaceOnEdge(_paths, 0, 1.5f);

            _view.ApplyPresentation(CatPresentationState.Walk, 1f, 0f, false);

            Bounds head = Head().GetComponent<Renderer>().bounds;
            foreach (string carriagePart in new[] { "Body", "Chassis" })
            {
                Bounds carriage = _view.transform.Find("Carriage/" + carriagePart)
                    .GetComponent<Renderer>().bounds;
                Assert.That(head.Intersects(carriage), Is.False,
                    $"the full platform endpoint must clear Carriage/{carriagePart}; "
                    + "the walking passenger cannot finish embedded in the vehicle");
            }
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
        public void WalkingFallbackBodyAndLegs_BreakTheOpaqueHeadSilhouette()
        {
            _view.PlaceOnEdge(_paths, 0, 1.5f);

            _view.ApplyPresentation(CatPresentationState.Walk, 1f, 0.73f, false);

            float headRadius = WorldMeshSize(Head()).x * 0.5f;
            foreach (string featureName in new[] { "Body", "LegLeft", "LegRight" })
            {
                Transform feature = Part(featureName);
                float farthest = 0f;
                foreach (Vector3 vertex in feature.GetComponent<MeshFilter>().sharedMesh.vertices)
                {
                    Vector3 offset = feature.TransformPoint(vertex) - Head().position;
                    Vector3 projected = BoardSceneLook.BoardTilt * offset;
                    farthest = Mathf.Max(farthest,
                        new Vector2(projected.x, projected.y).magnitude);
                }
                Assert.That(farthest, Is.GreaterThan(headRadius + 0.005f),
                    $"{featureName} must project beyond the opaque head while walking; "
                    + $"silhouette radius {farthest:F4} versus head {headRadius:F4}");
            }
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
        public void DepartureEndpoint_UsesBoardDownDespiteRetainedForeignCarriageHeading()
        {
            var vertical = TrackSplineGraph.Build(
                new[] { Vector3.zero, new Vector3(0f, 3f, 0f) },
                new[] { 0 }, new[] { 1 });
            _view.PlaceOnEdge(vertical, 0, vertical.Path(0).Length);
            _view.PlaceAtNode(vertical, 0, Vector3.zero);
            Transform carriage = _view.transform.Find("Carriage");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(90f,
                carriage.localEulerAngles.z)), Is.LessThan(0.001f),
                "the foreign-node clamp must retain the previously rendered heading");

            const float visualTime = 0.73f;
            _view.ApplyPresentation(CatPresentationState.RideIdle, visualTime, false);
            Vector3 seatedBoard = _host.transform.InverseTransformPoint(Cat().position);
            _view.ApplyPresentation(CatPresentationState.Alight, 1f, true,
                visualTime, false, 1f);
            Vector3 endpointBoard = _host.transform.InverseTransformPoint(Cat().position);
            Vector3 boardTravel = endpointBoard - seatedBoard;

            Assert.That(boardTravel.x, Is.EqualTo(0f).Within(0.0001f),
                "a retained carriage heading cannot turn departure into horizontal travel");
            Assert.That(boardTravel.y,
                Is.EqualTo(-ToyTrainView.PlatformSideOffset).Within(0.0001f));
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
            Assert.That(catBoard.y, Is.EqualTo(2.94f).Within(0.04f));
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
                Is.EqualTo(3.42f).Within(0.04f),
                "authored source Y=2 uses GridY=1.47, plus the enlarged 0.48-unit FIFO lane");
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
                Is.EqualTo(2.46f).Within(0.04f));
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
                Is.EqualTo(2.46f).Within(0.04f),
                "older waiter retains its non-colliding presentation lane through releases");
            Assert.That(Vector3.Distance(cat.position, advancedWaiter.position),
                Is.GreaterThan(ToyTrainView.PlatformQueueSpacing - 0.04f),
                "released boarding cat cannot collide with the new FIFO head");
            Assert.That(newTailBoard.y,
                Is.EqualTo(3.90f).Within(0.04f));
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
                Is.EqualTo(3.42f).Within(0.0001f));
            Assert.That(cat.Find("Body").gameObject.activeSelf, Is.True);
            Vector3 staticEndpoint = cat.position;

            _board.UpdateFrom(_session, 0.9f);

            Assert.That(cat.position, Is.EqualTo(staticEndpoint),
                "motion-off removes phase motion without removing the platform-wait information");
        }

        [Test]
        public void DeliveredPassenger_HitchUsesTheSameFloorLiftAsTheTrainPassenger()
        {
            BuildBoard(TwoCollapsedLifecyclesLevel(stationX: 3, stationY: 2));
            _session.AdvanceMs(4 * TickInterpolator.TICK_MS);
            _board.UpdateFrom(_session, 0f);
            var passenger = _board.transform.Find("delivered-cat:0");
            Assert.That(passenger.localPosition.x, Is.EqualTo(2.4f).Within(.0001f),
                "recorded station X=3 maps to 3 × GridX 0.8, even when both arrivals were unseen");
            Assert.That(passenger.localPosition.y, Is.EqualTo(2.284f).Within(.0001f),
                "station Y=2 maps to 2.94; the calibrated 0.656 platform offset stays in board units");
            Assert.That(passenger.localPosition.z, Is.EqualTo(-.2f).Within(.0001f),
                "HeadAnchorZ is the unchanged tabletop lift; the X/Y grid must not scale depth");
        }

        [TestCase(0, 2, 2.4f, 2.764f)]
        [TestCase(-2, 0, 1.92f, 2.284f)]
        [TestCase(-2, 2, 2.170553f, 2.705609f)]
        public void DeliveredPassenger_ObservedHandoffKeepsTheArrivalEndpoint(
            int approachX, int approachY, float endpointX, float endpointY)
        {
            BuildBoard(NonFinalReuseLevel(3, 2, approachX, approachY));
            _boardHost.transform.SetPositionAndRotation(new Vector3(2f, -3f, 5f),
                Quaternion.Euler(17f, -12f, 9f));
            for (int i = 0; i < 3; i++)
            {
                _session.AdvanceMs(TickInterpolator.TICK_MS);
                _board.UpdateFrom(_session, i * .1f);
            }
            var arriving = BoardTrain().GetComponent<ToyTrainView>();
            // Hand-derived from station (2.4,2.94), a 0.48 carriage trailing distance,
            // and board-down 0.656. The diagonal uses direction (1.6,-2.94), not (2,-2).
            Vector3 expectedWorld = _board.transform.TransformPoint(
                new Vector3(endpointX, endpointY, -.2f));
            Assert.That(Vector3.Distance(arriving.PlatformEndpointWorld, expectedWorld),
                Is.LessThan(.001f),
                "the arrival endpoint follows the anisotropic grid and transforms with the tilted board; "
                + $"expected={expectedWorld:F6} actual={arriving.PlatformEndpointWorld:F6}");
            _board.UpdateFrom(_session, .7f);
            var endpoint = BoardTrain().Find("Carriage/Cat").position;
            _board.UpdateFrom(_session, 1.2f);
            var resident = _board.transform.Find("delivered-cat:0/Carriage/Cat");
            var retained = _board.transform.Find("delivered-cat:0").GetComponent<ToyTrainView>();
            Assert.That(Vector3.Distance(retained.PlatformEndpointWorld, expectedWorld),
                Is.LessThan(.001f),
                "handoff preserves the measured carriage-side endpoint instead of rebuilding a node-centred guess");
            Assert.That(Vector3.Distance(resident.position, endpoint), Is.LessThan(.054f),
                "two 0.021 screen-unit bobs differ by at most 2 × 0.021 / cos(38°) ≈ 0.0533 board units");
        }

        [Test]
        public void DeliveredCats_SurviveSlotReuseAndDepartureExpiry_WithoutChangingTheRun()
        {
            BuildBoard(TwoCollapsedLifecyclesLevel());
            _session.AdvanceMs(4 * TickInterpolator.TICK_MS);
            Assert.That(_session.State.Deliveries, Is.EqualTo(2));
            var outcome = _session.State.Outcome;
            int tick = _session.State.Tick;
            _board.UpdateFrom(_session, 0f);
            _board.UpdateFrom(_session, 2f);
            var a = _board.transform.Find("delivered-cat:0");
            var b = _board.transform.Find("delivered-cat:1");
            Assert.That(a, Is.Not.Null, "a hitch must not lose the first delivered cat");
            Assert.That(b, Is.Not.Null, "slot reuse must not replace the first cat");
            Assert.That(a.gameObject.activeSelf && b.gameObject.activeSelf, Is.True);
            Assert.That(a.position, Is.Not.EqualTo(b.position));
            Assert.That(_session.State.Tick, Is.EqualTo(tick));
            Assert.That(_session.State.Outcome.Kind, Is.EqualTo(outcome.Kind));
            Assert.That(a.GetComponent<ToyTrainView>().PresentationState,
                Is.EqualTo(CatPresentationState.WaitingIdle));
            Assert.That(a.Find("Engine").gameObject.activeSelf, Is.False);
            var host = _boardHost;
            Object.DestroyImmediate(host);
            Assert.That(a == null && b == null, Is.True, "board teardown owns retained cats");
        }

        [Test]
        public void WonCats_HopAtSixTenthsWithNinetyMillisecondStagger_ThenRemain()
        {
            BuildBoard(TwoCollapsedLifecyclesLevel(stationX: 3, stationY: 2));
            _session.AdvanceMs(4 * TickInterpolator.TICK_MS);
            _session.State.Outcome = SimOutcome.Won;
            _board.UpdateFrom(_session, 10f);
            var a = _board.transform.Find("delivered-cat:0");
            var b = _board.transform.Find("delivered-cat:1");
            Assert.That(a, Is.Not.Null);
            Assert.That(b, Is.Not.Null);
            _board.UpdateFrom(_session, 10.59f);
            Vector3 anchor = a.localPosition;
            Vector3 neutralScale = a.Find("Carriage/Cat").localScale;
            Assert.That(Vector3.Distance(anchor, new Vector3(2.4f, 2.284f, -.2f)),
                Is.LessThan(.0001f),
                "the win starts on the scaled station's 0.656-offset platform, with unchanged depth");
            Assert.That(Vector3.Distance(b.localPosition, new Vector3(2.88f, 2.284f, -.2f)),
                Is.LessThan(.0001f),
                "the second retained cat keeps 0.48 board-unit lane spacing; GridX does not compress it");
            Assert.That(a.GetComponent<ToyTrainView>().PresentationState,
                Is.EqualTo(CatPresentationState.WaitingIdle), "the 0.6-second beat does not depend on board scale");
            _board.UpdateFrom(_session, 10.60f);
            Assert.That(a.GetComponent<ToyTrainView>().PresentationState,
                Is.EqualTo(CatPresentationState.Celebrate), "the first cat starts at 0.6 seconds");
            Assert.That(b.GetComponent<ToyTrainView>().PresentationState,
                Is.EqualTo(CatPresentationState.WaitingIdle), "the second cat waits for its 90ms stagger");
            _board.UpdateFrom(_session, 10.69f);
            Assert.That(b.GetComponent<ToyTrainView>().PresentationState,
                Is.EqualTo(CatPresentationState.Celebrate), "the second beat remains at 0.69 seconds");
            _board.UpdateFrom(_session, 10.84f);
            Assert.That(Vector3.Distance(a.Find("Carriage/Cat").localScale, neutralScale * 1.13f),
                Is.LessThan(.0001f),
                "the peak is 13% of the current rig/fallback scale, never the old absolute rig size");
            Assert.That(a.localPosition, Is.EqualTo(anchor),
                "the hop changes cat paint only; the calibrated platform anchor cannot move");
            _board.UpdateFrom(_session, 12f);
            Assert.That(a.gameObject.activeSelf && b.gameObject.activeSelf, Is.True,
                "both passengers remain on the platform after the beat");
            Assert.That(a.Find("Carriage/Cat").localScale, Is.EqualTo(neutralScale),
                "the hop settles to the newly calibrated base scale without compounding");
            Assert.That(a.localPosition, Is.EqualTo(anchor),
                "settling preserves the same station endpoint");
        }

        [Test]
        public void WonCats_MotionOffKeepsStaticPlatformPassengers()
        {
            BuildBoard(TwoCollapsedLifecyclesLevel());
            _session.AdvanceMs(4 * TickInterpolator.TICK_MS);
            _session.State.Outcome = SimOutcome.Won;
            _board.MotionOffSource = () => true;
            _board.UpdateFrom(_session, 10f);
            var a = _board.transform.Find("delivered-cat:0");
            Assert.That(a, Is.Not.Null);
            var cat = a.Find("Carriage/Cat");
            var pose = cat.position;
            var scale = cat.localScale;
            _board.UpdateFrom(_session, 10.8f);
            Assert.That(a.GetComponent<ToyTrainView>().PresentationState,
                Is.EqualTo(CatPresentationState.WaitingIdle));
            Assert.That(cat.position, Is.EqualTo(pose));
            Assert.That(cat.localScale, Is.EqualTo(scale));
        }

        [Test]
        public void BoardUpdateFrom_ActualDeliveryPlacesRetainedConsistAtRecordedStation()
        {
            BuildBoard(NonFinalReuseLevel(3, 2));
            _session.AdvanceMs(TickInterpolator.TICK_MS); // tick 0: emit
            _board.UpdateFrom(_session, 0f);
            _session.AdvanceMs(TickInterpolator.TICK_MS); // tick 1: approach
            _board.UpdateFrom(_session, 0.1f);
            _session.AdvanceMs(TickInterpolator.TICK_MS); // tick 2: deliver at node 1
            Assert.That(_session.State.Trains[0].State, Is.EqualTo(TrainState.None));
            Assert.That(_session.TrainDeliveryNode(0), Is.EqualTo(1));

            _board.UpdateFrom(_session, 0.2f);

            Transform train = BoardTrain();
            Assert.That(train.localPosition.x, Is.EqualTo(2.4f).Within(0.0001f),
                "the delivered head uses authored station X=3 × GridX 0.8");
            Assert.That(train.localPosition.y, Is.EqualTo(2.94f).Within(0.0001f),
                "the delivered head uses authored station Y=2 × GridY 1.47");
            Assert.That(train.localPosition.z, Is.EqualTo(-.2f).Within(0.0001f),
                "the same HeadAnchorZ lifts both the arriving train and retained passenger");
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
        public void RetainedPassenger_LongHitchUsesRecordedStation_WhenOldRendererNeverArrived()
        {
            BuildBoard(TwoCollapsedLifecyclesLevel(4, 5, 4));
            _session.AdvanceMs(TickInterpolator.TICK_MS);
            _board.UpdateFrom(_session, 0f);
            _session.AdvanceMs(9 * TickInterpolator.TICK_MS);
            Assert.That(_session.State.Deliveries, Is.EqualTo(2));
            Assert.That(_session.TrainOccupantGeneration(0), Is.EqualTo(2),
                "both complete lifecycles reused the rendered slot");
            _board.UpdateFrom(_session, .1f);
            Vector3 resident = _board.transform.Find("delivered-cat:0").position;
            Assert.That(Vector3.Distance(resident, _board.NodeWorldPos(1)),
                Is.LessThan(Vector3.Distance(resident, _board.NodeWorldPos(0))),
                "an unseen arrival cannot borrow the old source pose");
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
            Vector3 resident = _board.transform.Find("delivered-cat:0").position;
            Assert.That(Vector3.Distance(resident, _board.NodeWorldPos(1)),
                Is.LessThan(Vector3.Distance(resident, _board.NodeWorldPos(0))),
                "the retained cat belongs at its recorded destination, not its stale source pose");
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
            Assert.That(_board.transform.Find("delivered-cat:0").gameObject.activeSelf,
                Is.False, "the still-alighting red cat must not be duplicated");
            Assert.That(_board.transform.Find("delivered-cat:1").gameObject.activeSelf,
                Is.True, "the unseen blue cat uses its own recorded station");
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

        private static byte[] NonFinalReuseLevel(int stationX = 0, int stationY = 0,
            int approachX = 0, int approachY = 2) => VFixtures.Level(o =>
        {
            o["meta"]["mechanics"] = new JArray();
            o["meta"]["newMechanic"] = null;
            o["board"]["nodes"] = new JArray(
                VFixtures.Node("SRC", stationX + approachX, stationY + approachY),
                VFixtures.Node("RED", stationX, stationY));
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

        private static byte[] TwoCollapsedLifecyclesLevel(int travel = 1, int spacing = 2, int span = 1,
            int stationX = 0, int stationY = 0) => VFixtures.Level(o =>
        {
            o["meta"]["mechanics"] = new JArray();
            o["meta"]["newMechanic"] = null;
            o["board"]["nodes"] = new JArray(
                VFixtures.Node("SRC", stationX, stationY + span),
                VFixtures.Node("RED", stationX, stationY));
            o["board"]["edges"] = new JArray(VFixtures.Edge("E1", "SRC", "RED", travel));
            o["sources"] = new JArray(Source("SRC", "red"));
            o["stations"] = new JArray(VFixtures.Station("RED", 3, "red"));
            o["switches"] = new JArray();
            o["waves"] = new JArray(VFixtures.Wave(0, "red", 2, spacing));
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
