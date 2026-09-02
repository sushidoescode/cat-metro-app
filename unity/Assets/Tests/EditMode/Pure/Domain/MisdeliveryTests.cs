using System.Collections.Generic;
using System.Linq;
using CatMetro.Application.Retry;
using CatMetro.Domain;
using NUnit.Framework;

namespace CatMetro.Tests.Domain
{
    [TestFixture]
    public sealed class MisdeliveryTests
    {
        [Test]
        public void WrongArrival_DwellsEightTicks_ReversesOneWayEdge_ThenUsesCurrentRoute()
        {
            var graph = BounceGraph();
            Assert.That(graph.EdgeOneWay[1], Is.True,
                "the refusal reversal is an explicit exception to normal one-way travel");
            Assert.That(graph.EdgeReversible[1], Is.False,
                "the station refusal does not depend on the authored reversible mechanic");

            var log = new CommandLog();
            log.Append(new ToggleSwitchCommand(0, 5)); // applied during the platform dwell
            var snapshots = new List<TrainSnapshot>();
            var end = ReplayHasher.RunToEnd(graph, 101, log, state =>
            {
                var train = state.Trains.FirstOrDefault(candidate => candidate.Id == 1);
                snapshots.Add(new TrainSnapshot(state.Tick, train));
            });

            var dwell = snapshots.Where(snapshot =>
                snapshot.State == TrainState.RejectedAtStation).ToArray();
            Assert.That(dwell.Length, Is.EqualTo(Simulation.RejectionDwellTicks));
            Assert.That(dwell.Select(snapshot => snapshot.Tick),
                Is.EqualTo(Enumerable.Range(3, Simulation.RejectionDwellTicks)),
                "post-step snapshots cover the rejection tick and exactly seven following ticks");
            Assert.That(dwell.Select(snapshot => (int)snapshot.ProgressTicks),
                Is.EqualTo(Enumerable.Range(0, Simulation.RejectionDwellTicks)),
                "the existing ProgressTicks field is the complete dwell clock");

            var reverseEntry = snapshots.Single(snapshot => snapshot.Tick == 11);
            Assert.That(reverseEntry.State, Is.EqualTo(TrainState.OnEdgeReverse));
            Assert.That(reverseEntry.EdgeId, Is.EqualTo(1));
            Assert.That(reverseEntry.ProgressTicks, Is.Zero);
            Assert.That(reverseEntry.NodeId, Is.EqualTo(2),
                "the train remains station-positioned until reverse traversal starts rendering");

            var rerouted = snapshots.Single(snapshot => snapshot.Tick == 12);
            Assert.That(rerouted.State, Is.EqualTo(TrainState.OnEdge));
            Assert.That(rerouted.EdgeId, Is.EqualTo(2),
                "return to EdgeFrom must read the switch route at return time, not rejection time");
            Assert.That(rerouted.NodeId, Is.EqualTo(1));
            Assert.That(end.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            Assert.That(end.Deliveries, Is.EqualTo(1));
            Assert.That(end.Rejections, Is.EqualTo(1));
        }

        [Test]
        public void ReturningToUnchangedRoute_CanBeRejectedAgain()
        {
            var state = Fixtures.RunThroughTick(BounceGraph(), 102, new CommandLog(), 12);

            Assert.That(state.Outcome.Kind, Is.EqualTo(OutcomeKind.Running));
            Assert.That(state.Rejections, Is.EqualTo(2),
                "each wrong-colour station arrival is a distinct rejection");
            var train = state.Trains.Single(candidate => candidate.Id == 1);
            Assert.That(train.State, Is.EqualTo(TrainState.RejectedAtStation));
            Assert.That(train.NodeId, Is.EqualTo(2));
            Assert.That(train.EdgeId, Is.EqualTo(1));
            Assert.That(train.ProgressTicks, Is.Zero,
                "the second refusal starts a fresh eight-tick dwell");
        }

        [Test]
        public void ReverseArrival_DwellsThenDepartsForwardFromTheSameStationEndpoint()
        {
            var graph = ReverseArrivalBounceGraph();
            var log = new CommandLog();
            log.Append(new ToggleSwitchCommand(0, 5)); // choose the correct route during dwell
            var snapshots = new List<TrainSnapshot>();
            var end = ReplayHasher.RunToEnd(graph, 106, log, state =>
            {
                var train = state.Trains.FirstOrDefault(candidate => candidate.Id == 1);
                snapshots.Add(new TrainSnapshot(state.Tick, train));
            });

            var dwell = snapshots.Where(snapshot =>
                snapshot.State == TrainState.RejectedAtStation).ToArray();
            Assert.That(dwell.Length, Is.EqualTo(Simulation.RejectionDwellTicks));
            Assert.That(dwell.Select(snapshot => snapshot.Tick),
                Is.EqualTo(Enumerable.Range(3, Simulation.RejectionDwellTicks)));
            Assert.That(dwell.Select(snapshot => (int)snapshot.ProgressTicks),
                Is.EqualTo(Enumerable.Range(0, Simulation.RejectionDwellTicks)));
            Assert.That(dwell.Select(snapshot => (int)snapshot.NodeId), Is.All.EqualTo(0),
                "the train stays on the rejecting platform for the whole dwell");

            var departure = snapshots.Single(snapshot => snapshot.Tick == 11);
            Assert.That(departure.State, Is.EqualTo(TrainState.OnEdge),
                "reversing an OnEdgeReverse arrival means forward traversal");
            Assert.That(departure.EdgeId, Is.Zero);
            Assert.That(departure.ProgressTicks, Is.Zero);
            Assert.That(departure.NodeId, Is.EqualTo(graph.EdgeFrom[departure.EdgeId]),
                "progress zero must render at the rejecting station, not teleport to EdgeTo");

            var inFlight = snapshots.Single(snapshot => snapshot.Tick == 12);
            Assert.That(inFlight.State, Is.EqualTo(TrainState.OnEdge));
            Assert.That(inFlight.EdgeId, Is.Zero);
            Assert.That(inFlight.ProgressTicks, Is.EqualTo(1));
            Assert.That(end.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            Assert.That(end.Deliveries, Is.EqualTo(1));
            Assert.That(end.Rejections, Is.EqualTo(1));
        }

        [Test]
        public void PlatformCapacity_AllowsEquality_ButFailsImmediatelyAboveCapacity()
        {
            var atCapacity = Fixtures.RunThroughTick(
                BounceGraph(stationCapacity: 2, waveCount: 2, waveSpacing: 1, trainsMax: 2),
                103, new CommandLog(), 3);
            Assert.That(atCapacity.Outcome.Kind, Is.EqualTo(OutcomeKind.Running),
                "capacity is an occupancy maximum; equality is valid");
            Assert.That(RejectedAtNode(atCapacity, 2), Is.EqualTo(2));

            var overflow = Fixtures.RunThroughTick(
                BounceGraph(stationCapacity: 1, waveCount: 2, waveSpacing: 1, trainsMax: 2),
                103, new CommandLog(), 3);
            Assert.That(RejectedAtNode(overflow, 2), Is.EqualTo(2));
            Assert.That(overflow.Outcome.Kind, Is.EqualTo(OutcomeKind.Failed));
            Assert.That(overflow.Outcome.Reason, Is.EqualTo(FailReason.PlatformOverflow));
            Assert.That(overflow.Tick, Is.EqualTo(4),
                "overflow fails in the same step as the over-capacity arrival");
            Assert.That(CauseAttribution.CausalNode(overflow), Is.EqualTo(2));
        }

        [Test]
        public void ForwardAndReverseTrains_CanPassOnTheSameEdge()
        {
            var state = Fixtures.RunThroughTick(
                BounceGraph(
                    stationCapacity: 2,
                    waveCount: 2,
                    waveSpacing: 10,
                    wrongEdgeTravel: 3,
                    trainsMax: 2),
                104, new CommandLog(), 12);

            var first = state.Trains.Single(candidate => candidate.Id == 1);
            var second = state.Trains.Single(candidate => candidate.Id == 2);
            Assert.That(first.EdgeId, Is.EqualTo(1));
            Assert.That(second.EdgeId, Is.EqualTo(1));
            Assert.That(first.State, Is.EqualTo(TrainState.OnEdgeReverse));
            Assert.That(second.State, Is.EqualTo(TrainState.OnEdge));
            Assert.That(first.ProgressTicks, Is.Zero);
            Assert.That(second.ProgressTicks, Is.EqualTo(1));
            Assert.That(state.Outcome.Kind, Is.EqualTo(OutcomeKind.Running),
                "Cat Metro has no collision failure; opposing trains coexist deterministically");
        }

        [Test]
        public void RefusalStates_PreserveTheTenByteTrainDigestSlot()
        {
            int oneTrain = SimulationState.DigestLength(1, 4, 1, 8);
            int twoTrains = SimulationState.DigestLength(1, 4, 2, 8);
            Assert.That(twoTrains - oneTrain, Is.EqualTo(10));

            var initial = SimulationState.CreateInitial(BounceGraph(), 105);
            int digestLength = initial.DigestLength();
            Simulation.Step(ref initial, System.ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Simulation.Step(ref initial, System.ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Simulation.Step(ref initial, System.ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Assert.That(initial.Trains[0].State, Is.EqualTo(TrainState.RejectedAtStation));
            Assert.That(initial.DigestLength(), Is.EqualTo(digestLength));
            Assert.That(new byte[digestLength].Length, Is.EqualTo(initial.DigestLength()));
        }

        private static int RejectedAtNode(SimulationState state, int node) =>
            state.Trains.Count(train =>
                train.State == TrainState.RejectedAtStation && train.NodeId == node);

        private static LevelGraph BounceGraph(
            int stationCapacity = 2,
            int waveCount = 1,
            int waveSpacing = 1,
            int wrongEdgeTravel = 1,
            int trainsMax = 1) => new LevelGraph(
                "FX-REFUSE", 4,
                new[] { 8, 8, 8, 8 },                    // SRC, J1, wrong BLUE, correct RED
                new[] { 0, 1, 1 },
                new[] { 1, 2, 3 },
                new[] { 1, wrongEdgeTravel, 1 },
                new[] { 0 },
                new[] { new[] { 1, 2 } }, new[] { 1 }, new byte[] { 0 },
                new[] { 2, 3 },
                new[] { new[] { CatColor.Blue }, new[] { CatColor.Red } },
                new[] { stationCapacity, 2 },
                new[] { 0 }, new[] { CatColor.Red }, new[] { waveCount }, new[] { waveSpacing },
                waveCount, 80, qCapBound: 8, trainsMax: trainsMax);

        private static LevelGraph ReverseArrivalBounceGraph() => new LevelGraph(
            "FX-REFUSE-REVERSE-ARRIVAL", 3,
            new[] { 8, 8, 8 },                       // wrong BLUE, source/junction, correct RED
            new[] { 0, 1 },
            new[] { 1, 2 },
            new[] { 2, 1 },
            new[] { 1 },
            new[] { new[] { 0, 1 } }, new[] { 1 }, new byte[] { 0 },
            new[] { 0, 2 },
            new[] { new[] { CatColor.Blue }, new[] { CatColor.Red } },
            new[] { 2, 2 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            1, 40, qCapBound: 8, trainsMax: 1,
            edgeOneWay: new[] { true, true },
            edgeReversible: new[] { true, false });

        private readonly struct TrainSnapshot
        {
            public readonly int Tick;
            public readonly byte State;
            public readonly short EdgeId;
            public readonly short ProgressTicks;
            public readonly short NodeId;

            public TrainSnapshot(int tick, TrainSlot train)
            {
                Tick = tick;
                State = train.State;
                EdgeId = train.EdgeId;
                ProgressTicks = train.ProgressTicks;
                NodeId = train.NodeId;
            }
        }
    }
}
