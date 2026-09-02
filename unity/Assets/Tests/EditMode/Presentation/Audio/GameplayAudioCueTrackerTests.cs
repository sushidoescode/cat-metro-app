using CatMetro.Domain;
using CatMetro.Presentation.Audio;
using NUnit.Framework;

namespace CatMetro.Tests.Presentation.Audio
{
    public sealed class GameplayAudioCueTrackerTests
    {
        [Test]
        public void DeliveryAndWin_AreEdges_NotFrameOrClipTiming()
        {
            var tracker = new GameplayAudioCueTracker();
            tracker.Rebaseline(0, OutcomeKind.Running);

            Assert.That(tracker.Observe(0, OutcomeKind.Running),
                Is.EqualTo(GameplayAudioCues.None));
            Assert.That(tracker.Observe(1, OutcomeKind.Running),
                Is.EqualTo(GameplayAudioCues.Delivery));
            Assert.That(tracker.Observe(1, OutcomeKind.Running),
                Is.EqualTo(GameplayAudioCues.None), "a later frame cannot replay a delivery");

            Assert.That(tracker.Observe(2, OutcomeKind.Won),
                Is.EqualTo(GameplayAudioCues.Delivery | GameplayAudioCues.Celebrate));
            Assert.That(tracker.Observe(2, OutcomeKind.Won),
                Is.EqualTo(GameplayAudioCues.None), "win flourish is a transition, not a loop");
        }

        [Test]
        public void Rebaseline_DoesNotReplayARebuiltSessionsHistory()
        {
            var tracker = new GameplayAudioCueTracker();
            tracker.Rebaseline(4, OutcomeKind.Won);
            Assert.That(tracker.Observe(4, OutcomeKind.Won),
                Is.EqualTo(GameplayAudioCues.None));

            tracker.Rebaseline(0, OutcomeKind.Running);
            Assert.That(tracker.Observe(0, OutcomeKind.Running),
                Is.EqualTo(GameplayAudioCues.None));
        }

        [Test]
        public void SnapshotQueries_DistinguishMovementAndWrongStation_WithoutMutation()
        {
            var graph = AudioGraph();
            var state = SimulationState.CreateInitial(graph, 23UL);
            state.Trains[0] = new TrainSlot
            {
                Id = 1,
                Color = CatColor.Blue,
                EdgeId = 0,
                State = TrainState.OnEdge,
            };

            Assert.That(GameAudio.HasMovingTrain(state), Is.True);
            Assert.That(GameAudio.HasWrongStationArrival(state), Is.False);

            state.Trains[0].State = TrainState.AtNode;
            state.Trains[0].NodeId = 1;
            state.Trains[0].EdgeId = -1;
            Assert.That(GameAudio.HasMovingTrain(state), Is.False);
            Assert.That(GameAudio.HasWrongStationArrival(state), Is.True,
                "blue at the red station is the pinned Domain halt snapshot");
            Assert.That(state.Trains[0].Color, Is.EqualTo(CatColor.Blue),
                "the audio query only reads the snapshot");

            state.Trains[0].Color = CatColor.Red;
            Assert.That(GameAudio.HasWrongStationArrival(state), Is.False);
            state.Trains[0].Color = CatColor.Wild;
            Assert.That(GameAudio.HasWrongStationArrival(state), Is.False,
                "Wild retains the Domain's universal station acceptance rule");
        }

        private static LevelGraph AudioGraph() => new LevelGraph(
            "audio-test",
            nodeCount: 2,
            nodeQueueCapacity: new[] { 0, 0 },
            edgeFrom: new[] { 0 },
            edgeTo: new[] { 1 },
            edgeTravelTicks: new[] { 2 },
            sourceNodes: new[] { 0 },
            switchRoutes: new int[0][],
            switchNode: new int[0],
            switchInitialRoute: new byte[0],
            stationNode: new[] { 1 },
            stationAccepts: new[] { new[] { CatColor.Red } },
            stationCapacity: new[] { 1 },
            waveTick: new int[0],
            waveColor: new byte[0],
            waveCount: new int[0],
            waveSpacingTicks: new int[0],
            winDeliveries: 1,
            timeLimitTicks: 100,
            qCapBound: 1,
            trainsMax: 1);
    }
}
