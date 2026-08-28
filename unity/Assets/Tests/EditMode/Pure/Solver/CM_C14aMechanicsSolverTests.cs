using CatMetro.Domain;
using CatMetro.Domain.Solver;
using NUnit.Framework;

namespace CatMetro.Tests.Solver
{
    [TestFixture]
    public class CM_C14aMechanicsSolverTests
    {
        [TestCase("second-source")]
        [TestCase("wildcard")]
        public void ExactBfs_MatchesIndependentExhaustiveOracle(string mechanic)
        {
            var graph = Board(mechanic);
            var oracle = SolverFixtures.BruteForceBest(graph, 1414);
            Assert.That(oracle.HasValue, Is.True, "independent <=2-command oracle found a win");

            var actual = LevelSolver.Solve(graph, 1414, maxNodesExpanded: 200000);

            Assert.That(actual.Verdict, Is.EqualTo(SolveVerdict.Solved));
            Assert.That(actual.BeamWidthUsed, Is.EqualTo(0), "<=2 switches stays exact BFS");
            Assert.That(actual.CompletionTicks, Is.EqualTo(oracle.Value.ticks));
            SolverFixtures.AssertSameLog(oracle.Value.log, actual.OptimalLog,
                mechanic + " exact canonical log");
            Assert.That(ReplayHasher.RunToEnd(graph, 1414, actual.OptimalLog).Outcome.Kind,
                Is.EqualTo(OutcomeKind.Won));
        }

        [TestCase("second-source")]
        [TestCase("wildcard")]
        public void ExactBfs_TightBudgetStopsCanonicallyWithinOneExpansion(string mechanic)
        {
            var actual = LevelSolver.Solve(Board(mechanic), 1414, maxNodesExpanded: 1);

            Assert.That(actual.Verdict, Is.EqualTo(SolveVerdict.NotFound));
            Assert.That(actual.NotFoundReason, Is.EqualTo(NotFoundReason.Budget));
            Assert.That(actual.BeamWidthUsed, Is.EqualTo(0));
            Assert.That(actual.NodesExpanded, Is.EqualTo(1));
            Assert.That(actual.OptimalLog.Entries, Is.Empty, "budget stop fabricates no witness");
            Assert.That(actual.PinnedPruned, Is.EqualTo(0));
            Assert.That(actual.FirstPinMessage, Is.Empty);
        }

        private static LevelGraph Board(string mechanic) => mechanic == "second-source"
            ? TwoSourceDecisionBoard()
            : WildcardDecisionBoard();

        // Red reaches J at tick 2, blue at tick 4. Initial route is blue, so the exact win needs
        // red then blue route state and therefore exercises both authored sources.
        private static LevelGraph TwoSourceDecisionBoard() => new LevelGraph(
            "C14A-SOLVER-2SRC", 5, new[] { 8, 8, 8, 8, 8 },
            new[] { 0, 1, 2, 2 }, new[] { 2, 2, 3, 4 }, new[] { 2, 4, 2, 2 },
            new[] { 0, 1 },
            new[] { new[] { 2, 3 } }, new[] { 2 }, new byte[] { 1 },
            new[] { 3, 4 },
            new[] { new[] { CatColor.Red }, new[] { CatColor.Blue } },
            new[] { 6, 6 },
            new[] { 0, 0 }, new[] { CatColor.Red, CatColor.Blue },
            new[] { 1, 1 }, new[] { 1, 1 },
            2, 12, 8, 2,
            waveSourceNode: new[] { 0, 1 });

        // Red needs the route changed to RED; the later Wild follows that route but wins there even
        // though RED's accepts list contains only red. The oracle therefore tests W-auto semantics.
        private static LevelGraph WildcardDecisionBoard() => new LevelGraph(
            "C14A-SOLVER-WILD", 4, new[] { 8, 8, 8, 8 },
            new[] { 0, 1, 1 }, new[] { 1, 2, 3 }, new[] { 2, 2, 2 },
            new[] { 0 },
            new[] { new[] { 1, 2 } }, new[] { 1 }, new byte[] { 1 },
            new[] { 2, 3 },
            new[] { new[] { CatColor.Red }, new[] { CatColor.Blue } },
            new[] { 6, 6 },
            new[] { 0, 3 }, new[] { CatColor.Red, CatColor.Wild },
            new[] { 1, 1 }, new[] { 1, 1 },
            2, 12, 8, 2,
            waveSourceNode: new[] { 0, 0 });
    }
}
