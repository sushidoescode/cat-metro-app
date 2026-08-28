using CatMetro.Domain;
using CatMetro.Presentation.Cats;
using NUnit.Framework;

namespace CatMetro.Tests.EditMode.Presentation
{
    public sealed class CatPresentationTrackTests
    {
        [Test]
        public void ObservingSnapshot_DoesNotMutateTheSimulationSlot()
        {
            var snapshot = LiveOnEdge();
            var before = snapshot;
            var track = new CatPresentationTrack();

            track.Observe(snapshot, 1, false, 3f);

            Assert.That(snapshot.Id, Is.EqualTo(before.Id));
            Assert.That(snapshot.Color, Is.EqualTo(before.Color));
            Assert.That(snapshot.EdgeId, Is.EqualTo(before.EdgeId));
            Assert.That(snapshot.ProgressTicks, Is.EqualTo(before.ProgressTicks));
            Assert.That(snapshot.NodeId, Is.EqualTo(before.NodeId));
            Assert.That(snapshot.State, Is.EqualTo(before.State));
        }

        [Test]
        public void NewLiveSlot_UsesTheSpawnSequenceBeforeSettlingOnItsSimulationPosition()
        {
            var track = new CatPresentationTrack();
            var slot = LiveOnEdge();

            track.Observe(slot, 1, false, 10f);
            Assert.That(track.State, Is.EqualTo(CatPresentationState.Walk));
            Assert.That(track.StateElapsed, Is.EqualTo(0f));

            track.Observe(slot, 1, false, 10.22f);
            Assert.That(track.State, Is.EqualTo(CatPresentationState.Board));
            Assert.That(track.StateElapsed, Is.EqualTo(0f));

            track.Observe(slot, 1, false, 10.40f);
            Assert.That(track.State, Is.EqualTo(CatPresentationState.RideIdle));

            slot.State = TrainState.AtNode;
            track.Observe(slot, 1, false, 10.41f);
            Assert.That(track.State, Is.EqualTo(CatPresentationState.WaitingIdle));
        }

        [Test]
        public void DeliveredSlot_UsesTheDepartureSequenceThenHides()
        {
            var track = new CatPresentationTrack();
            var live = LiveOnEdge();
            var empty = default(TrainSlot);

            track.Observe(live, 1, false, 0f);
            track.Observe(empty, 1, true, 1f);
            Assert.That(track.State, Is.EqualTo(CatPresentationState.Alight));

            track.Observe(empty, 1, true, 1.18f);
            Assert.That(track.State, Is.EqualTo(CatPresentationState.Walk));

            track.Observe(empty, 1, true, 1.46f);
            Assert.That(track.State, Is.EqualTo(CatPresentationState.Celebrate));

            track.Observe(empty, 1, true, 1.94f);
            Assert.That(track.State, Is.EqualTo(CatPresentationState.Hidden));
        }

        [Test]
        public void NewPresentationOccupantGeneration_InterruptsDepartureWithTheSameDomainSlotId()
        {
            var track = new CatPresentationTrack();
            var oldLive = LiveOnEdge(1);
            var newLive = LiveOnEdge(1);

            track.Observe(oldLive, 1, false, 0f);
            track.Observe(default, 1, true, 1f);
            Assert.That(track.State, Is.EqualTo(CatPresentationState.Alight));

            track.Observe(newLive, 2, false, 1.01f);

            Assert.That(track.State, Is.EqualTo(CatPresentationState.Walk));
            Assert.That(track.StateElapsed, Is.EqualTo(0f));
        }

        private static TrainSlot LiveOnEdge(short id = 1) => new TrainSlot
        {
            Id = id,
            Color = CatColor.Red,
            EdgeId = 2,
            ProgressTicks = 4,
            NodeId = 1,
            State = TrainState.OnEdge,
        };
    }
}
