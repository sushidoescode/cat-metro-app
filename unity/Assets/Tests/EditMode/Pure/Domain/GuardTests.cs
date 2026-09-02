using System;
using NUnit.Framework;
using CatMetro.Domain;

namespace CatMetro.Tests.Domain
{
    // Criterion 14: the three-member failure taxonomy and the remaining envelope guards.
    [TestFixture]
    public class GuardTests
    {
        [Test]
        public void FailReason_HasExactlyThreeMembers()
        {
            var names = Enum.GetNames(typeof(FailReason));
            Assert.That(names.Length, Is.EqualTo(3), "adding a member is an ADR-0002 change");
            Assert.That(names, Is.EquivalentTo(new[] { "QueueOverflow", "PlatformOverflow", "TimeOut" }));
        }

        [Test]
        public void NonMatchingStationArrival_IsARecoverableRefusal()
        {
            SimulationState state = null;
            Assert.DoesNotThrow(() =>
                state = Fixtures.RunThroughTick(Fixtures.MismatchShape(), 3, new CommandLog(), 50));
            Assert.That(state.Rejections, Is.GreaterThan(1),
                "the unchanged route sends the cat through repeated deterministic refusals");
            Assert.That(state.Outcome.Kind, Is.EqualTo(OutcomeKind.Running));
        }

        [Test]
        public void SecondSource_ConstructsAndPreservesPerWaveOrigins()
        {
            var graph = new LevelGraph(
                "FX-2SRC", 3,
                new[] { 8, 8, 8 },
                new[] { 0, 1 }, new[] { 2, 2 }, new[] { 5, 5 },
                new[] { 0, 1 },
                new int[0][], new int[0], new byte[0],
                new[] { 2 }, new[] { new[] { CatColor.Red } }, new[] { 6 },
                new[] { 0, 1 }, new[] { CatColor.Red, CatColor.Red },
                new[] { 1, 1 }, new[] { 1, 1 },
                2, 100, 8, 2,
                waveSourceNode: new[] { 0, 1 });
            Assert.That(graph.SourceNodes, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(graph.WaveSourceNode, Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void WildColorWave_ConstructsAndAutoAccepts()
        {
            var graph = new LevelGraph(
                "FX-WILD", 2,
                new[] { 8, 8 },
                new[] { 0 }, new[] { 1 }, new[] { 5 },
                new[] { 0 },
                new int[0][], new int[0], new byte[0],
                new[] { 1 }, new[] { new[] { CatColor.Red } }, new[] { 6 },
                new[] { 0 }, new[] { CatColor.Wild }, new[] { 1 }, new[] { 1 },
                1, 100, 8, 1,
                waveSourceNode: new[] { 0 });
            var end = ReplayHasher.RunToEnd(graph, 35, new CommandLog());
            Assert.That(end.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            Assert.That(end.Deliveries, Is.EqualTo(1));
        }

        [Test]
        public void EveryPublishedFailureReason_ConstructsNormally()
        {
            Assert.That(SimOutcome.MakeFailed(FailReason.QueueOverflow).Reason, Is.EqualTo(FailReason.QueueOverflow));
            Assert.That(SimOutcome.MakeFailed(FailReason.PlatformOverflow).Reason, Is.EqualTo(FailReason.PlatformOverflow));
            Assert.That(SimOutcome.MakeFailed(FailReason.TimeOut).Reason, Is.EqualTo(FailReason.TimeOut));
        }
    }
}
