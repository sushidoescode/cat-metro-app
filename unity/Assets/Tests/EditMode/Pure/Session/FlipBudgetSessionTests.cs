using System;
using System.Linq;
using CatMetro.Application.Session;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Tests.Domain;
using NUnit.Framework;

namespace CatMetro.Tests.Session
{
    [TestFixture]
    public sealed class FlipBudgetSessionTests
    {
        [Test]
        public void FlipStatusCountsACommittedTapBeforeTheSimulationAppliesIt()
        {
            var session = L001Session();

            Assert.That(session.EnqueueToggle(0), Is.True);

            Assert.That(session.State.SwitchesUsed, Is.Zero);
            Assert.That(session.FlipStatus.Used, Is.EqualTo(1));
            Assert.That(session.FlipStatus.RemainingToPerfect, Is.Zero);

            session.AdvanceMs(125);
            Assert.That(session.State.SwitchesUsed, Is.Zero, "the first step is uncommandable");
            Assert.That(session.FlipStatus.Used, Is.EqualTo(1));

            session.AdvanceMs(125);
            Assert.That(session.State.SwitchesUsed, Is.EqualTo(1));
            Assert.That(session.FlipStatus.Used, Is.EqualTo(1), "applied and committed counts converge");
        }

        [Test]
        public void FlipStatusAfterWinIgnoresAToggleThatCanNeverBeApplied()
        {
            var session = L001Session();
            var golden = Fixtures.GoldenLog().Entries.ToArray();

            while (session.State.Outcome.Kind == OutcomeKind.Running)
            {
                foreach (var command in golden)
                    if (command.Tick == session.State.Tick)
                        session.EnqueueToggle(command.SwitchId);
                session.AdvanceMs(125);
            }

            Assert.That(session.State.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            int appliedAtWin = session.State.SwitchesUsed;
            int logAtWin = session.Log.Entries.Count;

            Assert.That(session.EnqueueToggle(0), Is.False);

            Assert.That(session.FlipStatus.Used, Is.EqualTo(appliedAtWin),
                "terminal rating must ignore commands the stopped simulation cannot apply");
            Assert.That(session.Log.Entries.Count, Is.EqualTo(logAtWin));
        }

        [Test]
        public void HardCapRejectsBeforeLogPendingAndStatusChange()
        {
            var session = L001Session(perfectMaxSwitches: 1);

            Assert.That(session.EnqueueToggle(0), Is.True);
            Assert.That(session.EnqueueToggle(0), Is.False);

            Assert.That(session.Log.Entries.Count, Is.EqualTo(1));
            Assert.That(session.PendingToggleCount(0), Is.EqualTo(1));
            Assert.That(session.State.SwitchesUsed, Is.Zero);
            Assert.That(session.FlipStatus.Used, Is.EqualTo(1));
            Assert.That(session.FlipStatus.RemainingToPerfect, Is.Zero);
        }

        [Test]
        public void CooldownRejectsPendingAndRemainingTwo_ButAdmitsRemainingOne()
        {
            var session = L001Session(perfectMaxSwitches: 5, cooldownTicks: 2);

            Assert.That(session.EnqueueToggle(0), Is.True);
            Assert.That(session.EnqueueToggle(0), Is.False,
                "the pending accepted press will establish cooldown before a sibling applies");
            Assert.That(session.Log.Entries.Count, Is.EqualTo(1));
            Assert.That(session.PendingToggleCount(0), Is.EqualTo(1));

            session.AdvanceMs(250);
            Assert.That(session.State.SwitchesUsed, Is.EqualTo(1));
            Assert.That(SwitchState.Cooldown(session.State.SwitchRoutes[0]), Is.EqualTo(2));
            Assert.That(session.EnqueueToggle(0), Is.False,
                "remaining two is still one tick too early for a newly stamped press");
            Assert.That(session.Log.Entries.Count, Is.EqualTo(1));
            Assert.That(session.PendingToggleCount(0), Is.Zero);

            session.AdvanceMs(125);
            Assert.That(SwitchState.Cooldown(session.State.SwitchRoutes[0]), Is.EqualTo(1));
            Assert.That(session.EnqueueToggle(0), Is.True,
                "remaining one decays on the intervening tick before this press applies");
            Assert.That(session.Log.Entries.Count, Is.EqualTo(2));
            Assert.That(session.PendingToggleCount(0), Is.EqualTo(1));

            session.AdvanceMs(250);
            Assert.That(session.State.SwitchesUsed, Is.EqualTo(2));
            Assert.That(SwitchState.Cooldown(session.State.SwitchRoutes[0]), Is.EqualTo(2));
        }

        [Test]
        public void ZeroCooldownAcceptsTwoSameTickPresses()
        {
            var session = L001Session(perfectMaxSwitches: 5, cooldownTicks: 0);

            Assert.That(session.EnqueueToggle(0), Is.True);
            Assert.That(session.EnqueueToggle(0), Is.True);
            Assert.That(session.Log.Entries.Count, Is.EqualTo(2));
            Assert.That(session.PendingToggleCount(0), Is.EqualTo(2));

            session.AdvanceMs(250);
            Assert.That(session.State.SwitchesUsed, Is.EqualTo(2));
            Assert.That(SwitchState.Cooldown(session.State.SwitchRoutes[0]), Is.Zero);
        }

        [Test]
        public void AcceptedTap_OutranksFreshStrayCooldownWithoutLosingCommitOrBudget()
        {
            var session = StrayCooldownPrioritySession();
            var clearedOriginMutant = StrayCooldownPrioritySession();

            AdvanceToAcceptedTapAndAutomaticPress(session);
            AdvanceToAcceptedTapAndAutomaticPress(clearedOriginMutant);

            Assert.That(session.State.HasFreshAutomaticCooldown(0), Is.True);
            Assert.That(SwitchState.Route(session.State.SwitchRoutes[0]), Is.EqualTo(1));
            Assert.That(SwitchState.Cooldown(session.State.SwitchRoutes[0]), Is.EqualTo(2));
            Assert.That(session.Log.Entries.Count, Is.EqualTo(1));
            Assert.That(session.PendingToggleCount(0), Is.EqualTo(1));
            Assert.That(session.FlipStatus.Used, Is.EqualTo(1));
            Assert.That(session.FlipStatus.RemainingToPerfect, Is.Zero);
            int committedBeforeApplication = CommittedRoute(session, 0);
            Assert.That(committedBeforeApplication, Is.Zero,
                "the lever composes the automatic press with the still-pending player tap");

            // Mutation witness: deleting only the automatic-origin bit reproduces the old bug.
            // The accepted log still consumes the visible cap, but its route change disappears.
            clearedOriginMutant.State.FreshAutomaticCooldowns = 0;
            clearedOriginMutant.AdvanceMs(TickInterpolator.TICK_MS);
            Assert.That(clearedOriginMutant.State.SwitchesUsed, Is.Zero);
            Assert.That(SwitchState.Route(clearedOriginMutant.State.SwitchRoutes[0]), Is.EqualTo(1));
            Assert.That(clearedOriginMutant.Log.Entries.Count, Is.EqualTo(1));
            Assert.That(clearedOriginMutant.FlipStatus.Used, Is.EqualTo(1));

            session.AdvanceMs(TickInterpolator.TICK_MS);

            Assert.That(session.State.SwitchesUsed, Is.EqualTo(1));
            Assert.That(SwitchState.Route(session.State.SwitchRoutes[0]), Is.Zero,
                "the accepted tap changes the route despite the intervening automatic cooldown");
            Assert.That(SwitchState.Cooldown(session.State.SwitchRoutes[0]), Is.EqualTo(2),
                "the successful player application establishes a full cooldown");
            Assert.That(session.State.FreshAutomaticCooldowns, Is.Zero);
            Assert.That(session.PendingToggleCount(0), Is.Zero);
            Assert.That(session.Log.Entries.Count, Is.EqualTo(1));
            Assert.That(CommittedRoute(session, 0), Is.EqualTo(committedBeforeApplication),
                "the committed lever does not snap back at application");
            Assert.That(session.EnqueueToggle(0), Is.False, "the one-flip cap remains binding");
            Assert.That(session.Log.Entries.Count, Is.EqualTo(1));

            while (session.State.Outcome.Kind == OutcomeKind.Running)
                session.AdvanceMs(TickInterpolator.TICK_MS);
            Assert.That(session.State.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            Assert.That(session.State.Deliveries, Is.EqualTo(2));
        }

        [Test]
        public void FreshAutomaticCooldown_IsDigestVisibleFirstCommandOnlyAndOneBoundaryLong()
        {
            var graph = Fixtures.StrayCooldownPriorityShape(FlipBudget.Unbudgeted);
            var state = SimulationState.CreateInitial(graph, 7001);
            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty); // tick 0
            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty); // tick 1 stray press

            Assert.That(graph.TracksFreshAutomaticCooldowns, Is.True);
            Assert.That(state.HasFreshAutomaticCooldown(0), Is.True);
            Assert.That(state.DigestLength(), Is.EqualTo(
                SimulationState.DigestLength(1, 5, 3, 4, true)));
            Assert.That(state.DigestLength(), Is.EqualTo(
                SimulationState.DigestLength(1, 5, 3, 4) + sizeof(ushort)));

            var withOrigin = new byte[state.DigestLength()];
            state.WriteDigest(withOrigin);
            state.FreshAutomaticCooldowns = 0;
            var withoutOrigin = new byte[state.DigestLength()];
            state.WriteDigest(withoutOrigin);
            state.FreshAutomaticCooldowns = 1;
            Assert.That(withOrigin.Where((value, index) => value != withoutOrigin[index]).Count(),
                Is.EqualTo(1), "the serialized origin mask is the only changed state byte");
            int originOffset = sizeof(int) * 7 + sizeof(ulong) * 2
                + state.SwitchRoutes.Length;
            Assert.That(withOrigin[originOffset], Is.EqualTo(1));

            var sameBoundary = new[]
            {
                new ToggleSwitchCommand(0, 1),
                new ToggleSwitchCommand(0, 1),
            };
            Simulation.Step(ref state, sameBoundary);
            Assert.That(state.SwitchesUsed, Is.EqualTo(1),
                "only the first command may claim automatic-origin priority");
            Assert.That(SwitchState.Route(state.SwitchRoutes[0]), Is.Zero);
            Assert.That(SwitchState.Cooldown(state.SwitchRoutes[0]), Is.EqualTo(2));
            Assert.That(state.FreshAutomaticCooldowns, Is.Zero);

            var expired = SimulationState.CreateInitial(graph, 7001);
            Simulation.Step(ref expired, ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Simulation.Step(ref expired, ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Assert.That(expired.HasFreshAutomaticCooldown(0), Is.True);
            Simulation.Step(ref expired, ReadOnlySpan<ToggleSwitchCommand>.Empty);
            Assert.That(expired.FreshAutomaticCooldowns, Is.Zero);
            Assert.That(SwitchState.Cooldown(expired.SwitchRoutes[0]), Is.EqualTo(1));
            Simulation.Step(ref expired, new[] { new ToggleSwitchCommand(0, 2) });
            Assert.That(expired.SwitchesUsed, Is.Zero,
                "an ordinary later command remains rejected while authored cooldown is active");
            Assert.That(SwitchState.Route(expired.SwitchRoutes[0]), Is.EqualTo(1));
            Assert.That(SwitchState.Cooldown(expired.SwitchRoutes[0]), Is.Zero);

            Assert.Throws<ArgumentException>(() => OversizedStrayCooldownGraph(),
                "direct graph construction cannot escape the authored ten-switch mask bound");
        }

        private static GameSession L001Session(
            int perfectMaxSwitches = 1, int cooldownTicks = 0)
        {
            var source = Fixtures.L001Shape();
            var graph = new LevelGraph(
                source.LevelId, source.NodeCount, source.NodeQueueCapacity,
                source.EdgeFrom, source.EdgeTo, source.EdgeTravelTicks,
                source.SourceNodes,
                source.SwitchRoutes, source.SwitchNode, source.SwitchInitialRoute,
                source.StationNode, source.StationAccepts, source.StationCapacity,
                source.WaveTick, source.WaveColor, source.WaveCount, source.WaveSpacingTicks,
                source.WinDeliveries, source.TimeLimitTicks,
                source.QCapBound, source.TrainsMax,
                waveSourceNode: source.WaveSourceNode,
                perfectMaxSwitches: perfectMaxSwitches,
                switchCooldownTicks: new[] { cooldownTicks });
            var dto = new LevelDto(
                2, graph.LevelId, "session fixture", (long)Fixtures.L001Seed, null,
                Array.Empty<NodeDto>(), Array.Empty<EdgeDto>(), Array.Empty<SourceDto>(),
                Array.Empty<StationDto>(), Array.Empty<SwitchDto>(), Array.Empty<WaveDto>(),
                new WinDto(graph.WinDeliveries, graph.TimeLimitTicks,
                    graph.PerfectMaxSwitches, new StarsDto(0, 0)),
                new EconomyDto(0, 0));
            return new GameSession(new ImportedLevel(dto, graph, null));
        }

        private static GameSession StrayCooldownPrioritySession()
        {
            var graph = Fixtures.StrayCooldownPriorityShape();
            var dto = new LevelDto(
                2, graph.LevelId, "stray cooldown priority", 7001, null,
                Array.Empty<NodeDto>(), Array.Empty<EdgeDto>(), Array.Empty<SourceDto>(),
                Array.Empty<StationDto>(), Array.Empty<SwitchDto>(), Array.Empty<WaveDto>(),
                new WinDto(graph.WinDeliveries, graph.TimeLimitTicks,
                    graph.PerfectMaxSwitches, new StarsDto(0, 0)),
                new EconomyDto(0, 0));
            return new GameSession(new ImportedLevel(dto, graph, null));
        }

        private static void AdvanceToAcceptedTapAndAutomaticPress(GameSession session)
        {
            session.AdvanceMs(TickInterpolator.TICK_MS); // process tick 0; tap during tick 1
            Assert.That(session.EnqueueToggle(0), Is.True);
            session.AdvanceMs(TickInterpolator.TICK_MS); // tick 1 stray press; tap still pending
        }

        private static int CommittedRoute(GameSession session, int switchId)
        {
            int routes = session.Level.Graph.SwitchRoutes[switchId].Length;
            return (SwitchState.Route(session.State.SwitchRoutes[switchId])
                + session.PendingToggleCount(switchId)) % routes;
        }

        private static LevelGraph OversizedStrayCooldownGraph() => new LevelGraph(
            "FX-STRAY-COOLDOWN-OVERSIZED", 4, new[] { 4, 4, 4, 4 },
            new[] { 0, 1, 1 }, new[] { 1, 2, 3 }, new[] { 1, 2, 2 },
            new[] { 0 },
            Enumerable.Repeat(new[] { 1, 2 }, 11).ToArray(),
            Enumerable.Repeat(1, 11).ToArray(), new byte[11],
            new[] { 2, 3 },
            new[] { new[] { CatColor.Red }, new[] { CatColor.Blue } },
            new[] { 2, 2 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            1, 10, 4, 1,
            switchCooldownTicks: Enumerable.Repeat(2, 11).ToArray(),
            waveStray: new[] { true });
    }
}
