using System;
using NUnit.Framework;
using CatMetro.Domain;
using CatMetro.Domain.Solver;
using CatMetro.Tests.Domain;

namespace CatMetro.Tests.Solver
{
    // Criterion 7 (Q-W): optimality is defined and deterministic — minimal CompletionTicks, then
    // fewer commands, then lexicographic (Tick, SwitchId).
    [TestFixture]
    public class SolverDeterminismTests
    {
        [Test] // 7a — the tie-break picks the specified solution
        public void TieBreak_PicksEarliestLexicographicLog()
        {
            // Any single toggle at entry.Tick 0..4 wins at the same completion tick on this
            // board; the tie-break must return exactly [(SwitchId 0, Tick 0)].
            var graph = SolverFixtures.TieBreakBoard();
            var r = LevelSolver.Solve(graph, 11);
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Solved));
            SolverFixtures.AssertSameLog(SolverFixtures.Log((0, 0)), r.OptimalLog, "tie-break");
        }

        [Test] // 7b — in-process determinism
        public void TwoInProcessRuns_ByteIdenticalLogs()
        {
            var graph = SolverFixtures.TwoSwitchTwoCmd();
            var r1 = LevelSolver.Solve(graph, 7);
            var r2 = LevelSolver.Solve(graph, 7);
            Assert.That(SolverFixtures.LogHex(r2.OptimalLog), Is.EqualTo(SolverFixtures.LogHex(r1.OptimalLog)));
            Assert.That(r2.NodesExpanded, Is.EqualTo(r1.NodesExpanded), "even the work count is deterministic");
        }

        [Test] // 7c — the cross-process emission the wrapper diffs (exactly one line per run)
        public void CrossProcess_EmitsCanonicalSolverLog()
        {
            var graph = SolverFixtures.TwoSwitchTwoCmd();
            var r = LevelSolver.Solve(graph, 7);
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Solved));
            Console.Out.WriteLine($"SOLVER_LOG={SolverFixtures.LogHex(r.OptimalLog)}");
        }
    }
}
