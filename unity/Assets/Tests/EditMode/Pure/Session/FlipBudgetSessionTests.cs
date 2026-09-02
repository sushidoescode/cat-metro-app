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
    }
}
