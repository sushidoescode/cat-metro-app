using System.Collections.Generic;
using System.Linq;
using CatMetro.Domain;
using CatMetro.Domain.Solver;
using NUnit.Framework;

namespace CatMetro.Tests.Solver
{
    [TestFixture]
    public sealed class RecoveryAndCycleTests
    {
        [Test]
        public void ExactBfs_FindsRefusalRecovery_WithZeroPins()
        {
            var graph = RefusalRecoveryGraph();
            var solve = LevelSolver.Solve(graph, 201);

            Assert.That(solve.Verdict, Is.EqualTo(SolveVerdict.Solved));
            Assert.That(solve.BeamWidthUsed, Is.Zero, "one switch uses exact BFS");
            Assert.That(solve.PinnedPruned, Is.Zero,
                "wrong-station states are searchable Domain states, not solver pins");
            Assert.That(solve.FirstPinMessage, Is.Empty);
            Assert.That(solve.OptimalLog.Entries.Count, Is.EqualTo(1));

            var end = ReplayHasher.RunToEnd(graph, 201, solve.OptimalLog);
            Assert.That(end.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            Assert.That(end.Rejections, Is.EqualTo(1),
                "the solver's winning replay must actually traverse the refusal state");
        }

        [Test]
        public void DirectedCycle_IsDetectedTraversedAndSolvedByExactBfs()
        {
            var graph = DirectedCycleGraph();
            Assert.That(HasDirectedCycle(graph), Is.True,
                "E0 SRC->LOOP and E1 LOOP->SRC are a real directed cycle, not a visual crossing");

            var solve = LevelSolver.Solve(graph, 202);
            Assert.That(solve.Verdict, Is.EqualTo(SolveVerdict.Solved));
            Assert.That(solve.BeamWidthUsed, Is.Zero);
            Assert.That(solve.PinnedPruned, Is.Zero);

            var traversedEdges = new HashSet<int>();
            var end = ReplayHasher.RunToEnd(graph, 202, solve.OptimalLog, state =>
            {
                foreach (var train in state.Trains)
                    if (train.State == TrainState.OnEdge) traversedEdges.Add(train.EdgeId);
            });
            Assert.That(traversedEdges, Does.Contain(0), "entered the cycle");
            Assert.That(traversedEdges, Does.Contain(1), "returned along the cycle's second arc");
            Assert.That(traversedEdges, Does.Contain(2), "left the cycle through the switched route");
            Assert.That(end.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            Assert.That(end.Deliveries, Is.EqualTo(1));
        }

        private static LevelGraph RefusalRecoveryGraph() => new LevelGraph(
            "FX-RECOVERY", 3,
            new[] { 8, 8, 8 },                          // switch/source, wrong BLUE, correct RED
            new[] { 0, 0 }, new[] { 1, 2 }, new[] { 1, 1 },
            new[] { 0 },
            new[] { new[] { 0, 1 } }, new[] { 0 }, new byte[] { 0 },
            new[] { 1, 2 },
            new[] { new[] { CatColor.Blue }, new[] { CatColor.Red } },
            new[] { 1, 1 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            1, 30, qCapBound: 8, trainsMax: 1);

        private static LevelGraph DirectedCycleGraph() => new LevelGraph(
            "FX-DIRECTED-CYCLE", 3,
            new[] { 8, 8, 8 },                          // switch/source, LOOP, RED
            new[] { 0, 1, 0 }, new[] { 1, 0, 2 }, new[] { 1, 1, 1 },
            new[] { 0 },
            new[] { new[] { 0, 2 } }, new[] { 0 }, new byte[] { 0 },
            new[] { 2 }, new[] { new[] { CatColor.Red } }, new[] { 1 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            1, 20, qCapBound: 8, trainsMax: 1);

        private static bool HasDirectedCycle(LevelGraph graph)
        {
            var colour = new byte[graph.NodeCount]; // 0 unvisited, 1 visiting, 2 complete
            bool Visit(int node)
            {
                colour[node] = 1;
                for (int edge = 0; edge < graph.EdgeFrom.Length; edge++)
                {
                    if (graph.EdgeFrom[edge] != node) continue;
                    int next = graph.EdgeTo[edge];
                    if (colour[next] == 1) return true;
                    if (colour[next] == 0 && Visit(next)) return true;
                }
                colour[node] = 2;
                return false;
            }

            return Enumerable.Range(0, graph.NodeCount)
                .Any(node => colour[node] == 0 && Visit(node));
        }
    }
}
