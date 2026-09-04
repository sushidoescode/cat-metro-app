using NUnit.Framework;
using CatMetro.Domain;
using CatMetro.Domain.Solver;
using CatMetro.Tests.Domain;

namespace CatMetro.Tests.Solver
{
    // Unsupported Domain boundaries remain pinnable, while wrong-colour arrivals are ordinary
    // searchable states after refusal/dwell/reverse semantics landed.
    [TestFixture]
    public class SolverPinTests
    {
        [Test]
        public void L701Shape_WrongRoutesAreSearched_RunCompletesWithoutPins()
        {
            var graph = SolverFixtures.L701ShapeTruncated();
            SolveResult r = null;
            Assert.DoesNotThrow(() => r = LevelSolver.Solve(graph, 1701));
            Assert.That(r.PinnedPruned, Is.Zero,
                "wrong-colour routes now dwell and reverse instead of pruning search branches");
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Solved));
            Assert.That(r.FirstPinMessage, Is.Empty);
        }

        [Test]
        public void WinIsFoundDespiteRecoverableWrongRoutes()
        {
            // TwoSwitchTwoCmd has a wrong-station decoy route (blue -> RED); the win must still
            // be found while that recoverable branch remains in the state space.
            var r = LevelSolver.Solve(SolverFixtures.TwoSwitchTwoCmd(), 7);
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Solved));
            Assert.That(r.PinnedPruned, Is.Zero);
        }

        [Test]
        public void ZeroPins_UnsolvableStaysUnsolvable()
        {
            var r = LevelSolver.Solve(SolverFixtures.UnsolvableNoPins(), 3);
            Assert.That(r.PinnedPruned, Is.EqualTo(0));
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Unsolvable),
                "Indeterminate is reserved for pin-pruned searches (Q-N)");
        }

        [Test]
        public void AllRoutesRefuse_ExactBfsProvesUnsolvableWithoutPins()
        {
            var r = LevelSolver.Solve(SolverFixtures.AllPinned(), 13);
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Unsolvable));
            Assert.That(r.PinnedPruned, Is.Zero);
            Assert.That(r.FirstPinMessage, Is.Empty);
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
        public void L001_EmptyLogBaseline_IsANormalNonWinningReplay()
        {
            // L001's empty log rides the reds into BLU. Refusal makes that a fully simulated,
            // non-winning replay rather than an indeterminate pinned boundary.
            var graph = Fixtures.L001Shape();
            var baseline = LevelSolver.EvaluateLog(graph, 1001, SolverFixtures.Log());
            Assert.That(baseline.Verdict, Is.EqualTo(SolveVerdict.NotFound));
            Assert.That(baseline.NotFoundReason, Is.EqualTo(NotFoundReason.None));
            Assert.That(baseline.PinnedPruned, Is.Zero);
            Assert.That(baseline.FirstPinMessage, Is.Empty);

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
        public void SuccessorAttempts_StopPromptlyAtTheSharedWorkCeiling()
        {
            var r = LevelSolver.Solve(
                SolverFixtures.TieBreakBoard(), 11, maxNodesExpanded: 100);

            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.NotFound));
            Assert.That(r.NotFoundReason, Is.EqualTo(NotFoundReason.Budget));
            Assert.That(r.NodesExpanded, Is.GreaterThan(0).And.LessThanOrEqualTo(100),
                "the search stops within the shared work ceiling");
            Assert.That(r.OptimalLog.Entries.Count, Is.EqualTo(0));
        }

        [Test]
        public void CanonicalRefinement_UsesTheSameTotalWorkCeiling()
        {
            // Search reaches a raw win, but the remainder cannot certify and compare the
            // equal-primary histories. Expansion telemetry must match the unbudgeted control;
            // the separate refinement meter is what exhausts the total-work ceiling.
            var control = LevelSolver.Solve(SolverFixtures.TieBreakBoard(), 11);
            var r = LevelSolver.Solve(
                SolverFixtures.TieBreakBoard(), 11, maxNodesExpanded: 400);

            Assert.That(control.Verdict, Is.EqualTo(SolveVerdict.Solved));
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.NotFound));
            Assert.That(r.NotFoundReason, Is.EqualTo(NotFoundReason.Budget));
            Assert.That(r.NodesExpanded, Is.EqualTo(control.NodesExpanded),
                "NodesExpanded remains honest search accounting; refinement is separately metered");
            Assert.That(r.OptimalLog.Entries.Count, Is.EqualTo(0));
            Assert.That(r.PinnedPruned, Is.EqualTo(control.PinnedPruned),
                "the budget stop preserves genuine search pins but may not fabricate one");
            Assert.That(r.FirstPinMessage, Is.EqualTo(control.FirstPinMessage));
        }

        [Test]
        public void BudgetDefault_IsTheDeclaredConstant()
        {
            var p = typeof(LevelSolver).GetMethod("Solve").GetParameters()[2];
            Assert.That(p.DefaultValue, Is.EqualTo(SolverBounds.MAX_NODES_EXPANDED));
        }
    }
}
