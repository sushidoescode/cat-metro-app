using CatMetro.Presentation.Board;
using CatMetro.Presentation.Cats;
using CatMetro.Domain;
using NUnit.Framework;
using UnityEngine;

namespace CatMetro.Tests.EditMode.Presentation
{
    public sealed class ToyTrainCatPresentationTests
    {
        private GameObject _host;
        private ToyTrainView _view;
        private TrackSplineGraph _paths;
        private Vector3 _eyeBaseline;

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
        }

        [Test]
        public void IdleStates_HidePlaceholderBodyAndLegs()
        {
            _view.ApplyPresentation(CatPresentationState.RideIdle, 0f, false);

            Assert.That(Part("Body").gameObject.activeSelf, Is.False);
            Assert.That(Part("LegLeft").gameObject.activeSelf, Is.False);
        }

        private Transform Cat() => _view.transform.Find("Carriage/Cat");
        private Transform Head() => Part("Head");
        private Transform EyeLeft() => Part("EyeLeft");
        private Transform Part(string name) => Cat().Find(name);
        private static Vector3 WorldMeshSize(Transform part)
        {
            Vector3 bounds = part.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            return Vector3.Scale(bounds, part.lossyScale);
        }
    }
}
