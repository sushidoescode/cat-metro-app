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

        [Test] // review M3 — the Indeterminate path is now executed, not just possible
        public void AllLinesPinned_ReturnsIndeterminateNeverUnsolvable()
        {
            var r = LevelSolver.Solve(SolverFixtures.AllPinned(), 13);
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Indeterminate),
                "Q-N: pinned exhaustion is not a proof of unsolvability");
            Assert.That(r.PinnedPruned, Is.GreaterThan(0));
            Assert.That(r.FirstPinMessage, Does.Contain("NEW-Q4"));
        }

        [Test] // review H1 — envelope-guard throws are pruned dead branches, never escapes
        public void EnvelopeTrap_SearchPrunesAndStillFindsTheZeroInputWin()
        {
            SolveResult r = null;
            Assert.DoesNotThrow(() => r = LevelSolver.Solve(SolverFixtures.EnvelopeTrap(), 21),
                "an InvalidOperationException from the Domain's envelope guards must not escape Solve");
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Solved));
            Assert.That(r.OptimalLog.Entries.Count, Is.EqualTo(0),
                "the authored line wins with zero input; only exploratory branches blow the envelope");
            Assert.That(r.PinnedPruned, Is.EqualTo(0),
                "envelope prunes are dead branches, not pins — no wrong-colour arrival exists on this board");
        }
    }

    // Criterion 10: the zero-input baseline (CM-R12.2's input) has both limbs.
    [TestFixture]
    public class SolverBaselineTests
    {
        [Test] // review M4 — the baseline is a solver API (EvaluateLog), not a test-side replay
        public void L001_EmptyLogBaseline_IsIndeterminateViaThePin()
        {
            // Baseline nuance (handoff): L001's empty log rides the reds into BLU — the NEW-Q4
            // pin makes the baseline Indeterminate, which is still "does not win".
            var graph = Fixtures.L001Shape();
            var baseline = LevelSolver.EvaluateLog(graph, 1001, SolverFixtures.Log());
            Assert.That(baseline.Verdict, Is.EqualTo(SolveVerdict.Indeterminate), "the pin fires on the empty log");
            Assert.That(baseline.PinnedPruned, Is.EqualTo(1));
            Assert.That(baseline.FirstPinMessage, Does.Contain("NEW-Q4"));

            var r = LevelSolver.Solve(graph, 1001);
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Solved), "the board itself is solvable");
            Assert.That(r.OptimalLog.Entries.Count, Is.GreaterThan(0), "…but not with the empty log");
        }

        [Test]
        public void AlreadyCorrectBoard_EmptyLogWins()
        {
            var baseline = LevelSolver.EvaluateLog(SolverFixtures.AlreadyCorrect(), 2, SolverFixtures.Log());
            Assert.That(baseline.Verdict, Is.EqualTo(SolveVerdict.Solved), "the baseline API sees the zero-input win");

            var r = LevelSolver.Solve(SolverFixtures.AlreadyCorrect(), 2);
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Solved));
            Assert.That(r.OptimalLog.Entries.Count, Is.EqualTo(0), "the optimal log is empty — zero-input win");
            Assert.That(r.SwitchesUsed, Is.EqualTo(0));
            Assert.That(r.CompletionTicks, Is.EqualTo(baseline.CompletionTicks), "Solve and EvaluateLog agree");
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
        public void CanonicalRefinement_UsesTheSameTotalWorkCeiling()
        {
            // Search itself completes in 97 expansions. The remaining three work units are not
            // enough to compare the five equal-primary winners on this broad safe-window board.
            var r = LevelSolver.Solve(
                SolverFixtures.TieBreakBoard(), 11, maxNodesExpanded: 100);

            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.NotFound));
            Assert.That(r.NotFoundReason, Is.EqualTo(NotFoundReason.Budget));
            Assert.That(r.NodesExpanded, Is.EqualTo(97),
                "NodesExpanded remains honest search accounting; refinement is separately metered");
            Assert.That(r.OptimalLog.Entries.Count, Is.EqualTo(0));
            Assert.That(r.PinnedPruned, Is.EqualTo(0));
            Assert.That(r.FirstPinMessage, Is.Empty);
        }

        [Test]
        public void BudgetDefault_IsTheDeclaredConstant()
        {
            var p = typeof(LevelSolver).GetMethod("Solve").GetParameters()[2];
            Assert.That(p.DefaultValue, Is.EqualTo(SolverBounds.MAX_NODES_EXPANDED));
        }
    }
}
