using System;
using System.Linq;
using CatMetro.Domain;
using NUnit.Framework;

namespace CatMetro.Tests.Domain
{
    [TestFixture]
    public sealed class AdvancedRuntimeMechanicsTests
    {
        [Test]
        public void CatToken_PacksIdentityWithoutChangingLegacyRoundBytes()
        {
            Assert.That(CatToken.Pack(CatColor.Red, CatShape.Round, false, false),
                Is.EqualTo(CatColor.Red));

            byte token = CatToken.Pack(
                CatColor.Wild, CatShape.Triangle, stray: true, express: true);
            Assert.That(CatToken.Color(token), Is.EqualTo(CatColor.Wild));
            Assert.That(CatToken.Shape(token), Is.EqualTo(CatShape.Triangle));
            Assert.That(CatToken.IsStray(token), Is.True);
            Assert.That(CatToken.IsExpress(token), Is.True);
            Assert.That(CatToken.Color(0), Is.EqualTo(CatColor.None));
            Assert.That(CatToken.Shape(0), Is.EqualTo(CatShape.Round));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CatToken.Pack(CatColor.None, CatShape.Round, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CatToken.Pack(CatColor.Red, 0, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => CatToken.Color(6));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CatToken.Shape((byte)(CatColor.Red | CatToken.ShapeMask)));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CatToken.IsExpress((byte)(CatColor.Red | 0x80)));
        }

        [Test]
        public void Spawn_PacksAuthoredShapeStrayAndExpressIntoTheExistingColorByte()
        {
            var graph = DirectStationGraph(
                waveColor: CatColor.Blue,
                waveShape: CatShape.Triangle,
                stationColor: CatColor.Blue,
                stationShape: CatShape.Triangle,
                stray: true,
                express: true,
                travelTicks: 5);
            var state = SimulationState.CreateInitial(graph, 501);

            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty);

            byte token = state.Trains[0].Color;
            Assert.That(CatToken.Color(token), Is.EqualTo(CatColor.Blue));
            Assert.That(CatToken.Shape(token), Is.EqualTo(CatShape.Triangle));
            Assert.That(CatToken.IsStray(token), Is.True);
            Assert.That(CatToken.IsExpress(token), Is.True);
            Assert.That(state.DigestLength(),
                Is.EqualTo(SimulationState.DigestLength(0, 2, 1, 4)),
                "token attributes reuse the existing one-byte train field");
        }

        [Test]
        public void ConcreteShapeMustMatch_WildIgnoresShape_AndWrongShapeDwellsEightTicks()
        {
            var matching = ReplayHasher.RunToEnd(
                DirectStationGraph(CatColor.Red, CatShape.Square,
                    CatColor.Red, CatShape.Square), 502, new CommandLog());
            Assert.That(matching.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            Assert.That(matching.Deliveries, Is.EqualTo(1));

            var wild = ReplayHasher.RunToEnd(
                DirectStationGraph(CatColor.Wild, CatShape.Triangle,
                    CatColor.Blue, CatShape.Square), 502, new CommandLog());
            Assert.That(wild.Outcome.Kind, Is.EqualTo(OutcomeKind.Won),
                "train-side Wild accepts regardless of station color and shape");

            var wrongGraph = DirectStationGraph(CatColor.Red, CatShape.Triangle,
                CatColor.Red, CatShape.Square, timeLimitTicks: 30);
            var wrong = SimulationState.CreateInitial(wrongGraph, 502);
            Simulation.Step(ref wrong, ReadOnlySpan<ToggleSwitchCommand>.Empty); // spawn
            Simulation.Step(ref wrong, ReadOnlySpan<ToggleSwitchCommand>.Empty); // refusal
            Assert.That(wrong.Rejections, Is.EqualTo(1));
            Assert.That(wrong.Deliveries, Is.Zero);
            Assert.That(wrong.Trains[0].State, Is.EqualTo(TrainState.RejectedAtStation));
            Assert.That(wrong.Trains[0].ProgressTicks, Is.Zero);

            for (int dwell = 1; dwell < Simulation.RejectionDwellTicks; dwell++)
            {
                Simulation.Step(ref wrong, ReadOnlySpan<ToggleSwitchCommand>.Empty);
                Assert.That(wrong.Trains[0].State, Is.EqualTo(TrainState.RejectedAtStation));
                Assert.That(wrong.Trains[0].ProgressTicks, Is.EqualTo(dwell));
            }
            Simulation.Step(ref wrong, ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Assert.That(wrong.Trains[0].State, Is.EqualTo(TrainState.OnEdgeReverse));
            Assert.That(wrong.Trains[0].ProgressTicks, Is.Zero);
        }

        [Test]
        public void ExpressBlockedAtSource_HoldsOutsideQueueAndRetriesAtGateStart()
        {
            var graph = ExpressSourceGateGraph();
            var state = SimulationState.CreateInitial(graph, 503);

            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty); // tick 0 closed
            Assert.That(state.Trains[0].State, Is.EqualTo(TrainState.ExpressHeldAtSource));
            AssertNoQueuedTrain(state, 0);

            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty); // tick 1 closed
            Assert.That(state.Trains[0].State, Is.EqualTo(TrainState.ExpressHeldAtSource));
            AssertNoQueuedTrain(state, 0);

            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty); // tick 2 open
            Assert.That(state.Trains[0].State, Is.EqualTo(TrainState.OnEdge));
            Assert.That(state.Trains[0].ProgressTicks, Is.Zero);
            AssertNoQueuedTrain(state, 0);
        }

        [Test]
        public void ExpressBlockedAtJunction_ImmediatelyReversesInsteadOfQueueing()
        {
            var state = SimulationState.CreateInitial(ExpressJunctionGateGraph(), 504);
            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty); // source -> junction
            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty); // gate closed at tick 1

            Assert.That(state.Trains[0].State, Is.EqualTo(TrainState.OnEdgeReverse));
            Assert.That(state.Trains[0].EdgeId, Is.Zero);
            Assert.That(state.Trains[0].ProgressTicks, Is.Zero);
            AssertNoQueuedTrain(state, 1);
        }

        [Test]
        public void ExpressReverseArrival_BouncesForwardAlongItsIncomingEdge()
        {
            var state = ReverseExpressBounceState();

            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty);

            Assert.That(state.Trains[0].State, Is.EqualTo(TrainState.OnEdge));
            Assert.That(state.Trains[0].EdgeId, Is.Zero);
            Assert.That(state.Trains[0].ProgressTicks, Is.Zero,
                "reversing a reverse arrival travels back toward EdgeTo");
            AssertNoQueuedTrain(state, 0);
        }

        [Test]
        public void ExpressMismatchBouncesImmediately_ButWildExpressDelivers()
        {
            var mismatch = SimulationState.CreateInitial(
                DirectStationGraph(CatColor.Red, CatShape.Triangle,
                    CatColor.Red, CatShape.Square, express: true), 505);
            Simulation.Step(ref mismatch, ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Simulation.Step(ref mismatch, ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Assert.That(mismatch.Rejections, Is.EqualTo(1));
            Assert.That(mismatch.Trains[0].State, Is.EqualTo(TrainState.OnEdgeReverse));
            Assert.That(mismatch.Trains[0].ProgressTicks, Is.Zero);

            var wild = ReplayHasher.RunToEnd(
                DirectStationGraph(CatColor.Wild, CatShape.Triangle,
                    CatColor.Red, CatShape.Square, express: true),
                505, new CommandLog());
            Assert.That(wild.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            Assert.That(wild.Rejections, Is.Zero);
        }

        [Test]
        public void StrayTakesCapturedRouteThenPressesWithoutPlayerBudget_AndRespectsCooldown()
        {
            var graph = StraySwitchGraph(cooldownTicks: 2);
            var state = SimulationState.CreateInitial(graph, 506);
            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty); // spawn on E0
            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty); // first arrival

            Assert.That(state.Trains[0].EdgeId, Is.EqualTo(1),
                "the stray takes route zero captured before its press");
            Assert.That(SwitchState.Route(state.SwitchRoutes[0]), Is.EqualTo(1));
            Assert.That(SwitchState.Cooldown(state.SwitchRoutes[0]), Is.EqualTo(2));
            Assert.That(state.SwitchesUsed, Is.Zero,
                "automatic presses ignore the zero player cap and do not consume it");

            MutateStrayToApproachSwitch(ref state);
            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Assert.That(state.Trains[0].EdgeId, Is.EqualTo(2));
            Assert.That(SwitchState.Route(state.SwitchRoutes[0]), Is.EqualTo(1));
            Assert.That(SwitchState.Cooldown(state.SwitchRoutes[0]), Is.EqualTo(1));

            MutateStrayToApproachSwitch(ref state);
            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Assert.That(SwitchState.Route(state.SwitchRoutes[0]), Is.EqualTo(1));
            Assert.That(SwitchState.Cooldown(state.SwitchRoutes[0]), Is.Zero,
                "the second locked processing tick still rejects the automatic press");

            MutateStrayToApproachSwitch(ref state);
            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Assert.That(state.Trains[0].EdgeId, Is.EqualTo(2),
                "the later pass still takes the route captured before toggling");
            Assert.That(SwitchState.Route(state.SwitchRoutes[0]), Is.Zero);
            Assert.That(SwitchState.Cooldown(state.SwitchRoutes[0]), Is.EqualTo(2));
            Assert.That(state.SwitchesUsed, Is.Zero);
        }

        [Test]
        public void StrayNeverDelivers_EvenWhenWildAndExpressAreAlsoSet()
        {
            var state = SimulationState.CreateInitial(
                DirectStationGraph(CatColor.Wild, CatShape.Round,
                    CatColor.Red, CatShape.Round, stray: true, express: true), 507);
            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty);

            Assert.That(state.Deliveries, Is.Zero);
            Assert.That(state.Rejections, Is.EqualTo(1));
            Assert.That(state.Trains[0].State, Is.EqualTo(TrainState.RejectedAtStation),
                "stray refusal uses the ordinary dwell even when the express flag is present");
        }

        [Test]
        public void SecondTrain_SameNodeArrivalFailsBeforeDeliveryOnlyWhenEnabled()
        {
            var enabled = SimulationState.CreateInitial(SameNodeCollisionGraph(true), 508);
            Simulation.Step(ref enabled, ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Simulation.Step(ref enabled, ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Assert.That(enabled.Outcome.Kind, Is.EqualTo(OutcomeKind.Failed));
            Assert.That(enabled.Outcome.Reason, Is.EqualTo(FailReason.Collision));
            Assert.That(enabled.Deliveries, Is.Zero,
                "collision is decided before either same-tick station delivery");

            var disabled = ReplayHasher.RunToEnd(
                SameNodeCollisionGraph(false), 508, new CommandLog());
            Assert.That(disabled.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            Assert.That(disabled.Deliveries, Is.EqualTo(2));
        }

        [Test]
        public void SecondTrain_OpposingEdgeOccupancyFailsOnlyWhenEnabled()
        {
            var enabled = OpposingEdgeState(collisionsEnabled: true);
            Simulation.Step(ref enabled, ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Assert.That(enabled.Outcome.Kind, Is.EqualTo(OutcomeKind.Failed));
            Assert.That(enabled.Outcome.Reason, Is.EqualTo(FailReason.Collision));

            var disabled = OpposingEdgeState(collisionsEnabled: false);
            Simulation.Step(ref disabled, ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Assert.That(disabled.Outcome.Kind, Is.EqualTo(OutcomeKind.Running));
            Assert.That(disabled.Trains.Select(t => t.State),
                Is.EqualTo(new[] { TrainState.OnEdge, TrainState.OnEdgeReverse }));
            Assert.That(disabled.Trains.Select(t => (int)t.ProgressTicks),
                Is.EqualTo(new[] { 1, 1 }));
        }

        private static void AssertNoQueuedTrain(SimulationState state, int node)
        {
            Assert.That(state.NodeQueueCounts[node], Is.Zero);
            Assert.That(state.NodeQueueSlots[node], Is.All.Zero);
        }

        private static void MutateStrayToApproachSwitch(ref SimulationState state)
        {
            state.Trains[0].State = TrainState.OnEdge;
            state.Trains[0].EdgeId = 0;
            state.Trains[0].ProgressTicks = 0;
            state.Trains[0].NodeId = 0;
        }

        private static LevelGraph DirectStationGraph(
            byte waveColor, byte waveShape, byte stationColor, byte stationShape,
            bool stray = false, bool express = false, int travelTicks = 1,
            int timeLimitTicks = 20) => new LevelGraph(
                "FX-TOKEN-STATION", 2, new[] { 4, 4 },
                new[] { 0 }, new[] { 1 }, new[] { travelTicks },
                new[] { 0 },
                Array.Empty<int[]>(), Array.Empty<int>(), Array.Empty<byte>(),
                new[] { 1 }, new[] { new[] { stationColor } }, new[] { 2 },
                new[] { 0 }, new[] { waveColor }, new[] { 1 }, new[] { 1 },
                1, timeLimitTicks, 4, 1,
                stationShape: new[] { stationShape },
                waveShape: new[] { waveShape },
                waveStray: new[] { stray },
                waveExpress: new[] { express });

        private static LevelGraph ExpressSourceGateGraph() => new LevelGraph(
            "FX-EXPRESS-SOURCE", 2, new[] { 2, 2 },
            new[] { 0 }, new[] { 1 }, new[] { 1 },
            new[] { 0 },
            Array.Empty<int[]>(), Array.Empty<int>(), Array.Empty<byte>(),
            new[] { 1 }, new[] { new[] { CatColor.Red } }, new[] { 1 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            1, 10, 2, 1,
            gateEdge: new[] { 0 },
            gateOpenWindows: new[] { new[] { new GateWindow(2, 5) } },
            gatePreviewTicks: new[] { 1 },
            waveExpress: new[] { true });

        private static LevelGraph ExpressJunctionGateGraph() => new LevelGraph(
            "FX-EXPRESS-JUNCTION", 3, new[] { 2, 2, 2 },
            new[] { 0, 1 }, new[] { 1, 2 }, new[] { 1, 1 },
            new[] { 0 },
            Array.Empty<int[]>(), Array.Empty<int>(), Array.Empty<byte>(),
            new[] { 2 }, new[] { new[] { CatColor.Red } }, new[] { 1 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            1, 10, 2, 1,
            gateEdge: new[] { 1 },
            gateOpenWindows: new[] { new[] { new GateWindow(0, 1) } },
            gatePreviewTicks: new[] { 1 },
            waveExpress: new[] { true });

        private static LevelGraph StraySwitchGraph(int cooldownTicks) => new LevelGraph(
            "FX-STRAY-SWITCH", 4, new[] { 4, 4, 4, 4 },
            new[] { 0, 1, 1 }, new[] { 1, 2, 3 }, new[] { 1, 5, 5 },
            new[] { 0 },
            new[] { new[] { 1, 2 } }, new[] { 1 }, new byte[] { 0 },
            new[] { 2, 3 },
            new[] { new[] { CatColor.Red }, new[] { CatColor.Red } },
            new[] { 2, 2 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            1, 50, 4, 1,
            perfectMaxSwitches: 0,
            switchCooldownTicks: new[] { cooldownTicks },
            waveStray: new[] { true });

        private static LevelGraph SameNodeCollisionGraph(bool collisionsEnabled) => new LevelGraph(
            "FX-SAME-NODE-COLLISION", 3, new[] { 2, 2, 2 },
            new[] { 0, 1 }, new[] { 2, 2 }, new[] { 1, 1 },
            new[] { 0, 1 },
            Array.Empty<int[]>(), Array.Empty<int>(), Array.Empty<byte>(),
            new[] { 2 }, new[] { new[] { CatColor.Red } }, new[] { 2 },
            new[] { 0, 0 }, new[] { CatColor.Red, CatColor.Red },
            new[] { 1, 1 }, new[] { 1, 1 },
            2, 6, 2, 2,
            waveSourceNode: new[] { 0, 1 },
            collisionsEnabled: collisionsEnabled);

        private static SimulationState OpposingEdgeState(bool collisionsEnabled)
        {
            var graph = new LevelGraph(
                "FX-OPPOSING-COLLISION", 2, new[] { 2, 2 },
                new[] { 0 }, new[] { 1 }, new[] { 5 },
                new[] { 0 },
                Array.Empty<int[]>(), Array.Empty<int>(), Array.Empty<byte>(),
                Array.Empty<int>(), Array.Empty<byte[]>(), Array.Empty<int>(),
                new[] { 100 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
                99, 10, 2, 2,
                collisionsEnabled: collisionsEnabled);
            var state = SimulationState.CreateInitial(graph, 509);
            state.Trains[0] = new TrainSlot
            {
                Id = 1,
                Color = CatToken.Pack(CatColor.Red, CatShape.Round, false, false),
                EdgeId = 0,
                State = TrainState.OnEdge,
            };
            state.Trains[1] = new TrainSlot
            {
                Id = 2,
                Color = CatToken.Pack(CatColor.Red, CatShape.Round, false, false),
                EdgeId = 0,
                State = TrainState.OnEdgeReverse,
            };
            return state;
        }

        private static SimulationState ReverseExpressBounceState()
        {
            var graph = new LevelGraph(
                "FX-EXPRESS-REVERSE-BOUNCE", 2, new[] { 2, 2 },
                new[] { 0 }, new[] { 1 }, new[] { 1 },
                new[] { 0 },
                Array.Empty<int[]>(), Array.Empty<int>(), Array.Empty<byte>(),
                Array.Empty<int>(), Array.Empty<byte[]>(), Array.Empty<int>(),
                new[] { 100 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
                99, 10, 2, 1,
                gateEdge: new[] { 0 },
                gateOpenWindows: new[] { new[] { new GateWindow(1, 2) } },
                gatePreviewTicks: new[] { 1 });
            var state = SimulationState.CreateInitial(graph, 510);
            state.Trains[0] = new TrainSlot
            {
                Id = 1,
                Color = CatToken.Pack(CatColor.Red, CatShape.Round, false, true),
                EdgeId = 0,
                ProgressTicks = 0,
                NodeId = 1,
                State = TrainState.OnEdgeReverse,
            };
            return state;
        }
    }
}
