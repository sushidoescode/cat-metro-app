using CatMetro.Domain;
using CatMetro.Domain.Solver;
using NUnit.Framework;

namespace CatMetro.Tests.Solver
{
    [TestFixture]
    public sealed class AdvancedRuntimeSolverTests
    {
        [Test]
        public void ExactSolver_AvoidsSameNodeCollisionWithOneAcceptedFlip()
        {
            var graph = CollisionAvoidanceGraph();

            var solve = LevelSolver.Solve(graph, 601);

            Assert.That(solve.Verdict, Is.EqualTo(SolveVerdict.Solved));
            Assert.That(solve.BeamWidthUsed, Is.Zero);
            Assert.That(solve.PinnedPruned, Is.Zero);
            Assert.That(solve.SwitchesUsed, Is.EqualTo(1));
            Assert.That(solve.OptimalLog.Entries.Count, Is.EqualTo(1));
            Assert.That(solve.OptimalLog.Entries[0].SwitchId, Is.Zero);
            Assert.That(solve.OptimalLog.Entries[0].Tick, Is.Zero);

            var replay = ReplayHasher.RunToEnd(graph, 601, solve.OptimalLog);
            Assert.That(replay.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            Assert.That(replay.Deliveries, Is.EqualTo(2));
            Assert.That(replay.SwitchesUsed, Is.EqualTo(1));

            var collision = ReplayHasher.RunToEnd(graph, 601, new CommandLog());
            Assert.That(collision.Outcome.Kind, Is.EqualTo(OutcomeKind.Failed));
            Assert.That(collision.Outcome.Reason, Is.EqualTo(FailReason.Collision));
            Assert.That(collision.Deliveries, Is.Zero);
        }

        private static LevelGraph CollisionAvoidanceGraph() => new LevelGraph(
            "FX-SOLVER-COLLISION", 4, new[] { 2, 2, 2, 2 },
            new[] { 0, 0, 3, 1 },
            new[] { 2, 3, 2, 2 },
            new[] { 2, 1, 2, 2 },
            new[] { 0, 1 },
            new[] { new[] { 0, 1 } }, new[] { 0 }, new byte[] { 0 },
            new[] { 2 }, new[] { new[] { CatColor.Red } }, new[] { 2 },
            new[] { 1, 1 }, new[] { CatColor.Red, CatColor.Red },
            new[] { 1, 1 }, new[] { 1, 1 },
            2, 10, 2, 2,
            waveSourceNode: new[] { 0, 1 },
            perfectMaxSwitches: 1,
            collisionsEnabled: true);
    }
}
