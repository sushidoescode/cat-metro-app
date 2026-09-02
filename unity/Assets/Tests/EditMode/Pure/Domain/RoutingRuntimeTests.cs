using System;
using System.Linq;
using CatMetro.Domain;
using NUnit.Framework;

namespace CatMetro.Tests.Domain
{
    [TestFixture]
    public sealed class RoutingRuntimeTests
    {
        [Test]
        public void SwitchState_PacksRouteAndCooldownIntoOneByte()
        {
            Assert.That(SwitchState.Pack(0, 0), Is.Zero);
            Assert.That(SwitchState.Pack(3, 0), Is.EqualTo(3),
                "legacy cooldown-zero route bytes are unchanged");
            Assert.That(SwitchState.Pack(3, 63), Is.EqualTo(byte.MaxValue));
            Assert.That(SwitchState.Route(SwitchState.Pack(2, 19)), Is.EqualTo(2));
            Assert.That(SwitchState.Cooldown(SwitchState.Pack(2, 19)), Is.EqualTo(19));
            Assert.Throws<ArgumentOutOfRangeException>(() => SwitchState.Pack(4, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => SwitchState.Pack(0, 64));
        }

        [Test]
        public void CooldownTwo_RejectsExactlyTwoFollowingProcessingTicks()
        {
            var state = SimulationState.CreateInitial(QuietSwitchGraph(cooldownTicks: 2), 301);
            var press = new[] { new ToggleSwitchCommand(0, 0) };

            Simulation.Step(ref state, press);
            Assert.That(state.SwitchesUsed, Is.EqualTo(1));
            Assert.That(SwitchState.Route(state.SwitchRoutes[0]), Is.EqualTo(1));
            Assert.That(SwitchState.Cooldown(state.SwitchRoutes[0]), Is.EqualTo(2));

            Simulation.Step(ref state, press);
            Assert.That(state.SwitchesUsed, Is.EqualTo(1));
            Assert.That(SwitchState.Cooldown(state.SwitchRoutes[0]), Is.EqualTo(1));

            Simulation.Step(ref state, press);
            Assert.That(state.SwitchesUsed, Is.EqualTo(1));
            Assert.That(SwitchState.Cooldown(state.SwitchRoutes[0]), Is.Zero);

            Simulation.Step(ref state, press);
            Assert.That(state.SwitchesUsed, Is.EqualTo(2));
            Assert.That(SwitchState.Route(state.SwitchRoutes[0]), Is.Zero);
            Assert.That(SwitchState.Cooldown(state.SwitchRoutes[0]), Is.EqualTo(2));
        }

        [Test]
        public void SameTickPair_CooldownAcceptsOne_ZeroCooldownAcceptsBoth()
        {
            var pair = new[]
            {
                new ToggleSwitchCommand(0, 0),
                new ToggleSwitchCommand(0, 0),
            };
            var cooling = SimulationState.CreateInitial(QuietSwitchGraph(cooldownTicks: 2), 302);
            Simulation.Step(ref cooling, pair);
            Assert.That(cooling.SwitchesUsed, Is.EqualTo(1));
            Assert.That(SwitchState.Route(cooling.SwitchRoutes[0]), Is.EqualTo(1));
            Assert.That(SwitchState.Cooldown(cooling.SwitchRoutes[0]), Is.EqualTo(2));

            var immediate = SimulationState.CreateInitial(QuietSwitchGraph(cooldownTicks: 0), 302);
            Simulation.Step(ref immediate, pair);
            Assert.That(immediate.SwitchesUsed, Is.EqualTo(2));
            Assert.That(SwitchState.Route(immediate.SwitchRoutes[0]), Is.Zero);
            Assert.That(SwitchState.Cooldown(immediate.SwitchRoutes[0]), Is.Zero);
        }

        [Test]
        public void HardCap_ZeroRejectsAll_OneAcceptsFirst_UnbudgetedAcceptsAll()
        {
            var pair = new[]
            {
                new ToggleSwitchCommand(0, 0),
                new ToggleSwitchCommand(0, 0),
            };
            var zero = SimulationState.CreateInitial(
                QuietSwitchGraph(perfectMaxSwitches: 0), 303);
            Simulation.Step(ref zero, pair);
            Assert.That(zero.SwitchesUsed, Is.Zero);
            Assert.That(SwitchState.Route(zero.SwitchRoutes[0]), Is.Zero);

            var one = SimulationState.CreateInitial(
                QuietSwitchGraph(perfectMaxSwitches: 1), 303);
            Simulation.Step(ref one, pair);
            Assert.That(one.SwitchesUsed, Is.EqualTo(1));
            Assert.That(SwitchState.Route(one.SwitchRoutes[0]), Is.EqualTo(1));

            var unlimited = SimulationState.CreateInitial(
                QuietSwitchGraph(perfectMaxSwitches: FlipBudget.Unbudgeted), 303);
            Simulation.Step(ref unlimited, pair);
            Assert.That(unlimited.SwitchesUsed, Is.EqualTo(2));
            Assert.That(SwitchState.Route(unlimited.SwitchRoutes[0]), Is.Zero);
        }

        [Test]
        public void GateWindow_IsHalfOpen_AndClosedRouteQueuesUntilStart()
        {
            var graph = ForwardGateGraph(waveTick: 0);
            var state = SimulationState.CreateInitial(graph, 304);

            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty); // tick 0 closed
            Assert.That(state.NodeQueueCounts[0], Is.EqualTo(1));
            Assert.That(state.Trains[0].State, Is.EqualTo(TrainState.AtNode));
            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty); // tick 1 closed
            Assert.That(state.NodeQueueCounts[0], Is.EqualTo(1));
            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty); // tick 2 open
            Assert.That(state.NodeQueueCounts[0], Is.Zero);
            Assert.That(state.Trains[0].State, Is.EqualTo(TrainState.OnEdge));
            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty); // tick 3 open
            Assert.That(state.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));

            var endClosed = Fixtures.RunThroughTick(
                ForwardGateGraph(waveTick: 4), 304, new CommandLog(), 4);
            Assert.That(endClosed.NodeQueueCounts[0], Is.EqualTo(1),
                "tick 4 equals the window end and is therefore closed");
            Assert.That(endClosed.Trains[0].State, Is.EqualTo(TrainState.AtNode));
        }

        [Test]
        public void OrdinaryReverseTraversal_ObeysDirectionFlagsAndGate()
        {
            var blocked = SimulationState.CreateInitial(
                ReverseGraph(oneWay: true, reversible: false), 305);
            Assert.Throws<InvalidOperationException>(() =>
                Simulation.Step(ref blocked, ReadOnlySpan<ToggleSwitchCommand>.Empty));

            foreach (var graph in new[]
            {
                ReverseGraph(oneWay: false, reversible: false),
                ReverseGraph(oneWay: true, reversible: true),
            })
            {
                var state = SimulationState.CreateInitial(graph, 305);
                Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty);
                Assert.That(state.Trains[0].State, Is.EqualTo(TrainState.OnEdgeReverse));
                Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty);
                Assert.That(state.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            }

            var gated = SimulationState.CreateInitial(
                ReverseGraph(oneWay: false, reversible: false, gateStart: 1, gateEnd: 2), 305);
            Simulation.Step(ref gated, ReadOnlySpan<ToggleSwitchCommand>.Empty); // tick 0 closed
            Assert.That(gated.NodeQueueCounts[1], Is.EqualTo(1));
            Simulation.Step(ref gated, ReadOnlySpan<ToggleSwitchCommand>.Empty); // tick 1 open
            Assert.That(gated.NodeQueueCounts[1], Is.Zero);
            Assert.That(gated.Trains[0].State, Is.EqualTo(TrainState.OnEdgeReverse));
            Simulation.Step(ref gated, ReadOnlySpan<ToggleSwitchCommand>.Empty); // in-flight continues
            Assert.That(gated.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
        }

        [Test]
        public void SwitchMaySelectIncidentReverse_AndSwitchlessPrefersForward()
        {
            var switched = SimulationState.CreateInitial(SwitchedReverseGraph(), 306);
            Simulation.Step(ref switched, ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Assert.That(switched.Trains[0].EdgeId, Is.Zero);
            Assert.That(switched.Trains[0].State, Is.EqualTo(TrainState.OnEdgeReverse));
            Simulation.Step(ref switched, ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Assert.That(switched.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));

            var switchless = SimulationState.CreateInitial(ForwardPreferredGraph(), 306);
            Simulation.Step(ref switchless, ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Assert.That(switchless.Trains[0].EdgeId, Is.EqualTo(1));
            Assert.That(switchless.Trains[0].State, Is.EqualTo(TrainState.OnEdge));
            Simulation.Step(ref switchless, ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Assert.That(switchless.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
        }

        [Test]
        public void RefusalReverse_IgnoresClosedGateAndKeepsExactEightTickDwell()
        {
            var graph = RefusalGateGraph();
            var states = new System.Collections.Generic.List<byte>();
            var end = Fixtures.RunThroughTick(graph, 307, new CommandLog(), 9,
                state => states.Add(state.Trains[0].State));

            Assert.That(states.Count(value => value == TrainState.RejectedAtStation),
                Is.EqualTo(Simulation.RejectionDwellTicks));
            Assert.That(end.Trains[0].State, Is.EqualTo(TrainState.OnEdgeReverse));
            Assert.That(end.Trains[0].ProgressTicks, Is.Zero);
        }

        [Test]
        public void LevelGraph_RejectsDuplicateGateEdges()
        {
            Assert.Throws<ArgumentException>(() => new LevelGraph(
                "FX-DUP-GATE", 2, new[] { 8, 8 },
                new[] { 0 }, new[] { 1 }, new[] { 1 },
                new[] { 0 },
                Array.Empty<int[]>(), Array.Empty<int>(), Array.Empty<byte>(),
                new[] { 1 }, new[] { new[] { CatColor.Red } }, new[] { 1 },
                new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
                1, 10, 8, 1,
                gateEdge: new[] { 0, 0 },
                gateOpenWindows: new[]
                {
                    new[] { new GateWindow(0, 1) },
                    new[] { new GateWindow(2, 3) },
                },
                gatePreviewTicks: new[] { 8, 8 }));
        }

        [Test]
        public void LevelGraph_RejectsCooldownThatCannotFitThePackedByte()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                QuietSwitchGraph(cooldownTicks: SwitchState.MaxCooldown + 1));
        }

        private static LevelGraph QuietSwitchGraph(
            int cooldownTicks = 0,
            int perfectMaxSwitches = FlipBudget.Unbudgeted) => new LevelGraph(
                "FX-SWITCH-RUNTIME", 3, new[] { 8, 8, 8 },
                new[] { 0, 0 }, new[] { 1, 2 }, new[] { 1, 1 },
                new[] { 0 },
                new[] { new[] { 0, 1 } }, new[] { 0 }, new byte[] { 0 },
                new[] { 1, 2 },
                new[] { new[] { CatColor.Red }, new[] { CatColor.Red } },
                new[] { 1, 1 },
                new[] { 100 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
                1, 20, 8, 1,
                perfectMaxSwitches: perfectMaxSwitches,
                switchCooldownTicks: new[] { cooldownTicks });

        private static LevelGraph ForwardGateGraph(int waveTick) => new LevelGraph(
            "FX-GATE-FWD", 2, new[] { 8, 8 },
            new[] { 0 }, new[] { 1 }, new[] { 1 },
            new[] { 0 },
            Array.Empty<int[]>(), Array.Empty<int>(), Array.Empty<byte>(),
            new[] { 1 }, new[] { new[] { CatColor.Red } }, new[] { 1 },
            new[] { waveTick }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            1, 10, 8, 1,
            gateEdge: new[] { 0 },
            gateOpenWindows: new[] { new[] { new GateWindow(2, 4) } },
            gatePreviewTicks: new[] { 8 });

        private static LevelGraph ReverseGraph(
            bool oneWay, bool reversible, int? gateStart = null, int? gateEnd = null) =>
            new LevelGraph(
                "FX-REVERSE", 2, new[] { 8, 8 },
                new[] { 0 }, new[] { 1 }, new[] { 1 },
                new[] { 1 },
                Array.Empty<int[]>(), Array.Empty<int>(), Array.Empty<byte>(),
                new[] { 0 }, new[] { new[] { CatColor.Red } }, new[] { 1 },
                new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
                1, 10, 8, 1,
                edgeOneWay: new[] { oneWay },
                edgeReversible: new[] { reversible },
                gateEdge: gateStart.HasValue ? new[] { 0 } : null,
                gateOpenWindows: gateStart.HasValue
                    ? new[] { new[] { new GateWindow(gateStart.Value, gateEnd.Value) } }
                    : null,
                gatePreviewTicks: gateStart.HasValue ? new[] { 8 } : null);

        private static LevelGraph SwitchedReverseGraph() => new LevelGraph(
            "FX-SWITCH-REVERSE", 3, new[] { 8, 8, 8 },
            new[] { 0, 1 }, new[] { 1, 2 }, new[] { 1, 1 },
            new[] { 1 },
            new[] { new[] { 0, 1 } }, new[] { 1 }, new byte[] { 0 },
            new[] { 0, 2 },
            new[] { new[] { CatColor.Red }, new[] { CatColor.Blue } },
            new[] { 1, 1 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            1, 10, 8, 1,
            edgeOneWay: new[] { true, true },
            edgeReversible: new[] { true, false });

        private static LevelGraph ForwardPreferredGraph() => new LevelGraph(
            "FX-FORWARD-FIRST", 3, new[] { 8, 8, 8 },
            new[] { 0, 1 }, new[] { 1, 2 }, new[] { 1, 1 },
            new[] { 1 },
            Array.Empty<int[]>(), Array.Empty<int>(), Array.Empty<byte>(),
            new[] { 0, 2 },
            new[] { new[] { CatColor.Blue }, new[] { CatColor.Red } },
            new[] { 1, 1 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            1, 10, 8, 1,
            edgeOneWay: new[] { false, true });

        private static LevelGraph RefusalGateGraph() => new LevelGraph(
            "FX-REFUSAL-GATE", 2, new[] { 8, 8 },
            new[] { 0 }, new[] { 1 }, new[] { 1 },
            new[] { 0 },
            Array.Empty<int[]>(), Array.Empty<int>(), Array.Empty<byte>(),
            new[] { 1 }, new[] { new[] { CatColor.Blue } }, new[] { 1 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            1, 20, 8, 1,
            gateEdge: new[] { 0 },
            gateOpenWindows: new[] { new[] { new GateWindow(0, 2) } },
            gatePreviewTicks: new[] { 8 });
    }
}
