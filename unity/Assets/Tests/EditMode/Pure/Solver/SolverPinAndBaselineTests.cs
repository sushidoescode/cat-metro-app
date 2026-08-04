using NUnit.Framework;
using CatMetro.Domain;
using CatMetro.Domain.Solver;
using CatMetro.Tests.Domain;

namespace CatMetro.Tests.Solver
{
    // Criterion 5 (Q-N): pinned branches are pruned and counted, never mistaken for
    // unsolvability, never escaped.
    [TestFixture]
    public class SolverPinTests
    {
        [Test]
        public void L701Shape_WrongRoutesPinned_RunCompletes()
        {
            var graph = SolverFixtures.L701ShapeTruncated();
            SolveResult r = null;
            Assert.DoesNotThrow(() => r = LevelSolver.Solve(graph, 1701), "a pinned branch may never escape");
            Assert.That(r.PinnedPruned, Is.GreaterThan(0), "wrong colours reach wrong stations on this board");
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Solved).Or.EqualTo(SolveVerdict.Indeterminate));
            Assert.That(r.FirstPinMessage, Does.Contain("NEW-Q4").Or.Contain("pinned"),
                "the first pin message is recorded for diagnosis");
        }

        [Test]
        public void WinIsFoundDespitePrunedBranches()
        {
            // TwoSwitchTwoCmd has a pinned decoy route (blue -> RED) on the direct path — the win
            // must still be found around it.
            var r = LevelSolver.Solve(SolverFixtures.TwoSwitchTwoCmd(), 7);
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Solved));
        }

        [Test]
        public void ZeroPins_UnsolvableStaysUnsolvable()
        {
            var r = LevelSolver.Solve(SolverFixtures.UnsolvableNoPins(), 3);
            Assert.That(r.PinnedPruned, Is.EqualTo(0));
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Unsolvable),
                "Indeterminate is reserved for pin-pruned searches (Q-N)");
        }
    }

    // Criterion 10: the zero-input baseline (CM-R12.2's input) has both limbs.
    [TestFixture]
    public class SolverBaselineTests
    {
        [Test]
        public void L001_EmptyLog_DoesNotWin()
        {
            // Baseline nuance (handoff): L001's empty log rides the reds into BLU — the NEW-Q4
            // pin makes the baseline Indeterminate, which is still "does not win".
            var graph = Fixtures.L001Shape();
            Assert.That(SolverFixtures.RunsToWin(graph, 1001, SolverFixtures.Log(), out _), Is.False,
                "fixture sanity: initialRoute 1 is deliberately wrong (example_levels.json:13)");
            var r = LevelSolver.Solve(graph, 1001);
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Solved), "the board itself is solvable");
            Assert.That(r.OptimalLog.Entries.Count, Is.GreaterThan(0), "…but not with the empty log");
        }

        [Test]
        public void AlreadyCorrectBoard_EmptyLogWins()
        {
            var r = LevelSolver.Solve(SolverFixtures.AlreadyCorrect(), 2);
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Solved));
            Assert.That(r.OptimalLog.Entries.Count, Is.EqualTo(0), "the optimal log is empty — zero-input win");
            Assert.That(r.SwitchesUsed, Is.EqualTo(0));
        }
    }

    // Criterion 11: work is bounded by a number, not a clock.
    [TestFixture]
    public class SolverBudgetTests
    {
        [Test]
        public void LowBudget_ReturnsNotFoundBudget()
        {
            var r = LevelSolver.Solve(SolverFixtures.TwoSwitchTwoCmd(), 7, maxNodesExpanded: 5);
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.NotFound));
            Assert.That(r.NotFoundReason, Is.EqualTo(NotFoundReason.Budget));
            Assert.That(r.NodesExpanded, Is.GreaterThan(0).And.LessThanOrEqualTo(6),
                "the reported expansion count reflects the budget cut-off");
        }

        [Test]
        public void BudgetDefault_IsTheDeclaredConstant()
        {
            var p = typeof(LevelSolver).GetMethod("Solve").GetParameters()[2];
            Assert.That(p.DefaultValue, Is.EqualTo(SolverBounds.MAX_NODES_EXPANDED));
        }
    }
}
