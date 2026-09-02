using System;
using System.Linq;
using CatMetro.Domain;
using NUnit.Framework;

namespace CatMetro.Tests.Domain
{
    [TestFixture]
    public class CM_C14aMechanicsDomainTests
    {
        [Test]
        public void SecondSource_WavesSpawnAtTheirAuthoredSourcesAndPaths()
        {
            var graph = TwoSourceGraph();
            var state = SimulationState.CreateInitial(graph, 14);

            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty);

            var active = state.Trains.Where(t => t.State != TrainState.None).ToArray();
            Assert.That(active.Select(t => (int)t.NodeId), Is.EqualTo(new[] { 0, 1 }),
                "each new train retains its authored source node");
            Assert.That(active.Select(t => (int)t.EdgeId), Is.EqualTo(new[] { 0, 1 }),
                "each train enters that source's outgoing path");
            Assert.That(active.Select(t => t.Color),
                Is.EqualTo(new[] { CatColor.Red, CatColor.Blue }));

            var end = ReplayHasher.RunToEnd(graph, 14, new CommandLog());
            Assert.That(end.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            Assert.That(end.Deliveries, Is.EqualTo(2));
        }

        [Test]
        public void OmittedPerWaveOrigins_DefaultEveryWaveToFirstSource()
        {
            var graph = new LevelGraph(
                "C14A-DEFAULT", 3, new[] { 8, 8, 8 },
                new[] { 0, 1 }, new[] { 2, 2 }, new[] { 2, 2 },
                new[] { 0, 1 },
                new int[0][], new int[0], new byte[0],
                new[] { 2 }, new[] { new[] { CatColor.Red } }, new[] { 6 },
                new[] { 0, 2 }, new[] { CatColor.Red, CatColor.Red },
                new[] { 1, 1 }, new[] { 1, 1 },
                2, 10, 8, 2);

            Assert.That(graph.SourceNodes, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(graph.SourceNode, Is.EqualTo(0), "legacy first-source view remains stable");
            Assert.That(graph.WaveSourceNode, Is.EqualTo(new[] { 0, 0 }),
                "old one-source fixtures do not need constructor edits");
        }

        [Test]
        public void PerWaveOrigins_RejectMissingSourcesWrongLengthAndUndeclaredNode()
        {
            Assert.Throws<ArgumentException>(() => TwoSourceGraph(
                sourceNodes: Array.Empty<int>(), waveSourceNode: new[] { 0, 1 }));
            Assert.Throws<ArgumentException>(() => TwoSourceGraph(
                sourceNodes: new[] { 0, 1 }, waveSourceNode: new[] { 0 }));
            Assert.Throws<ArgumentException>(() => TwoSourceGraph(
                sourceNodes: new[] { 0, 1 }, waveSourceNode: new[] { 0, 2 }));
        }

        [Test]
        public void WaveArrays_RejectEveryInconsistentLengthBeforeSimulation()
        {
            var ex = Assert.Throws<ArgumentException>(() => new LevelGraph(
                "C14A-WAVE-LENGTH", 2, new[] { 8, 8 },
                new[] { 0 }, new[] { 1 }, new[] { 2 },
                new[] { 0 },
                new int[0][], new int[0], new byte[0],
                new[] { 1 }, new[] { new[] { CatColor.Red } }, new[] { 6 },
                new[] { 0, 1 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
                1, 10, 8, 1,
                waveSourceNode: new[] { 0 }));
            Assert.That(ex.Message, Does.Contain("wave arrays"));
        }

        [Test]
        public void Wild_RemainsWildInTransit_AutoAcceptsAtStepFive_AndAddsNoCommand()
        {
            var graph = WildGraph(CatColor.Wild, CatColor.Blue);
            var log = new CommandLog();
            var state = SimulationState.CreateInitial(graph, 35);

            Simulation.Step(ref state, ReadOnlySpan<ToggleSwitchCommand>.Empty);

            Assert.That(state.Trains.Single(t => t.State != TrainState.None).Color,
                Is.EqualTo(CatColor.Wild), "Wild remains train-side state in transit");
            Assert.That(log.Entries, Is.Empty, "W-auto-accept writes no command");
            Assert.That(CommandLog.CurrentFormatVersion, Is.EqualTo(1), "no log-format change");

            var end = ReplayHasher.RunToEnd(graph, 35, log);
            Assert.That(end.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            Assert.That(end.Deliveries, Is.EqualTo(1),
                "Wild is accepted by the first station even though it accepts only blue");
            Assert.That(log.Entries, Is.Empty, "the complete replay writes no resolution command");
            var firstHash = ReplayHasher.ComputeReplayHash(graph, 35, log);
            var secondHash = ReplayHasher.ComputeReplayHash(graph, 35, log);
            Assert.That(firstHash, Is.EqualTo(secondHash),
                "W-auto replay is deterministic without a golden rewrite");
        }

        [Test]
        public void StationSideWild_IsNotUniversalForAConcreteTrain()
        {
            var graph = WildGraph(CatColor.Red, CatColor.Wild);
            var end = ReplayHasher.RunToEnd(graph, 35, new CommandLog());
            Assert.That(end.Outcome.Kind, Is.EqualTo(OutcomeKind.Failed));
            Assert.That(end.Outcome.Reason, Is.EqualTo(FailReason.TimeOut));
            Assert.That(end.Deliveries, Is.Zero);
            Assert.That(end.Rejections, Is.GreaterThan(0),
                "a station Wild token stays exact-only, so a concrete arrival is refused");
        }

        private static LevelGraph TwoSourceGraph(
            int[] sourceNodes = null, int[] waveSourceNode = null)
        {
            return new LevelGraph(
                "C14A-2SRC", 4, new[] { 8, 8, 8, 8 },
                new[] { 0, 1 }, new[] { 2, 3 }, new[] { 2, 2 },
                sourceNodes ?? new[] { 0, 1 },
                new int[0][], new int[0], new byte[0],
                new[] { 2, 3 },
                new[] { new[] { CatColor.Red }, new[] { CatColor.Blue } },
                new[] { 6, 6 },
                new[] { 0, 0 }, new[] { CatColor.Red, CatColor.Blue },
                new[] { 1, 1 }, new[] { 1, 1 },
                2, 10, 8, 2,
                waveSourceNode: waveSourceNode ?? new[] { 0, 1 });
        }

        private static LevelGraph WildGraph(byte waveColor, byte stationAccepts)
        {
            return new LevelGraph(
                "C14A-WILD", 2, new[] { 8, 8 },
                new[] { 0 }, new[] { 1 }, new[] { 2 },
                new[] { 0 },
                new int[0][], new int[0], new byte[0],
                new[] { 1 }, new[] { new[] { stationAccepts } }, new[] { 6 },
                new[] { 0 }, new[] { waveColor }, new[] { 1 }, new[] { 1 },
                1, 10, 8, 1,
                waveSourceNode: new[] { 0 });
        }
    }
}
