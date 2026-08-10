using System;
using System.Collections.Generic;
using NUnit.Framework;
using CatMetro.Domain;
using CatMetro.Domain.Solver;
using CatMetro.Tests.Domain;

namespace CatMetro.Tests.Solver
{
    // SOLVER-TIEBREAK: optimality stays minimal CompletionTicks then fewer commands. Equal-primary
    // wins use the middle of each same-completion action window before the final lexicographic
    // (Tick, SwitchId) fallback.
    [TestFixture]
    public class SolverDeterminismTests
    {
        [Test]
        public void TieBreak_PicksTheMiddleOfTheSameCompletionWindow()
        {
            // Any single toggle at entry.Tick 0..4 wins at the same completion tick on this
            // board. The lower-middle rule has one answer: [(SwitchId 0, Tick 2)].
            var graph = SolverFixtures.TieBreakBoard();
            var r = LevelSolver.Solve(graph, 11);
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Solved));
            SolverFixtures.AssertSameLog(SolverFixtures.Log((0, 2)), r.OptimalLog,
                "mid-window tie-break (safe interval 0..4)");
            AssertNeighborIsSameCompletionWin(graph, 11, r, 0, -1);
            AssertNeighborIsSameCompletionWin(graph, 11, r, 0, +1);
        }

        [Test]
        public void TwoCommandTieBreak_KeepsOneTickOfMarginOnBothSidesOfEveryDecision()
        {
            var graph = SolverFixtures.TwoSwitchTwoCmd();
            var r = LevelSolver.Solve(graph, 7);
            var reference = SolverFixtures.BruteForceBest(graph, 7);

            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Solved));
            Assert.That(reference, Is.Not.Null);
            SolverFixtures.AssertSameLog(reference.Value.log, r.OptimalLog,
                "independent brute-force mid-window oracle");

            for (int i = 0; i < r.OptimalLog.Entries.Count; i++)
            {
                Assert.That(r.OptimalLog.Entries[i].Tick, Is.GreaterThan(0),
                    "this fixture gives every necessary command a real lower-side margin");
                AssertNeighborIsSameCompletionWin(graph, 7, r, i, -1);
                AssertNeighborIsSameCompletionWin(graph, 7, r, i, +1);
            }
        }

        [Test]
        public void TieBreak_AppliesFinalLexOnlyAfterEveryEqualPrimaryWinnerIsCentered()
        {
            // S0 is downstream: raw S0@0 beats raw S1@0, but its wide window centers later.
            // S1 is upstream: after both equal-primary alternatives are normalized, S1 wins
            // the FINAL (Tick, SwitchId) comparison. Selecting the raw winner first is wrong.
            var graph = SolverFixtures.FinalLexAfterCenterBoard();
            var r = LevelSolver.Solve(graph, 17);
            var reference = SolverFixtures.BruteForceBest(graph, 17);

            Assert.That(reference, Is.Not.Null);
            SolverFixtures.AssertSameLog(SolverFixtures.Log((1, 1)), reference.Value.log,
                "exhaustive fixed-point oracle discriminator");
            SolverFixtures.AssertSameLog(reference.Value.log, r.OptimalLog,
                "final lex must run after normalization");
        }

        [Test]
        public void TieBreak_RetainsCanonicalHistoryAcrossStateConvergence()
        {
            var graph = SolverFixtures.DedupeCanonicalBoard();
            var reference = SolverFixtures.BruteForceBest(graph, 17);
            var r = LevelSolver.Solve(graph, 17);

            Assert.That(reference, Is.Not.Null);
            SolverFixtures.AssertSameLog(SolverFixtures.Log((0, 0), (0, 1)), reference.Value.log,
                "exhaustive chronological fixed-point discriminator");
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Solved));
            Assert.That(r.CompletionTicks, Is.EqualTo(4));
            SolverFixtures.AssertSameLog(reference.Value.log, r.OptimalLog,
                "state dedupe may not discard the eventual centered canonical history");
        }

        [Test]
        public void MidWindowNormalizer_StopsWhenCenteringWouldReverseReceiptChronology()
        {
            var wins = new HashSet<string>
            {
                "1,2", "2,2", "3,2", "4,2", "5,2", "3,1"
            };

            bool converged = MidWindowNormalizer.TryCenter(
                new[] { 1, 2 }, 6,
                ticks => wins.Contains(ticks[0] + "," + ticks[1]),
                out var result);

            Assert.That(converged, Is.False,
                "Option A: unrestricted windows remain authoritative, but a reversed receipt order stops");
            Assert.That(result, Is.EqualTo(new[] { 1, 2 }),
                "a failed normalization returns the original receipt chronology, never a crossed log");
            Assert.That(result[0], Is.LessThanOrEqualTo(result[1]),
                "the original receipt chronology remains representable by command ticks");
        }

        [Test]
        public void InteractingWindowCycle_StopsInsteadOfReturningAChangingBoundary()
        {
            var wins = new HashSet<string>
            {
                "1,1", "2,1", "3,1", "1,2", "2,2", "2,3", "3,3"
            };

            bool converged = MidWindowNormalizer.TryCenter(
                new[] { 1, 1 }, 4,
                ticks => wins.Contains(ticks[0] + "," + ticks[1]),
                out var result);

            Assert.That(converged, Is.False,
                "the midpoint sweeps cycle (1,1) -> (2,2) -> (1,1); no boundary is canonical");
            Assert.That(result, Is.EqualTo(new[] { 1, 1 }),
                "the stop result is deterministic and never presented as a centered success");
        }

        [Test]
        public void InteractingWindowProbe_IsBoundedToAConstantThreeSweepEnvelope()
        {
            var wins = new HashSet<string>
            {
                "1,1", "2,1", "3,1", "1,2", "2,2", "2,3", "3,3"
            };
            int probes = 0;

            MidWindowNormalizer.TryCenter(new[] { 1, 1 }, 4, ticks =>
            {
                probes++;
                return wins.Contains(ticks[0] + "," + ticks[1]);
            }, out _);

            Assert.That(probes, Is.LessThanOrEqualTo(12),
                "refinement work may not multiply by an arbitrary command-count pass cap");
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

        private static void AssertNeighborIsSameCompletionWin(
            LevelGraph graph, ulong seed, SolveResult solved, int entryIndex, int delta)
        {
            var shifted = new CommandLog(solved.OptimalLog.FormatVersion);
            for (int i = 0; i < solved.OptimalLog.Entries.Count; i++)
            {
                var e = solved.OptimalLog.Entries[i];
                shifted.Append(i == entryIndex
                    ? new ToggleSwitchCommand(e.SwitchId, e.Tick + delta)
                    : e);
            }

            Assert.That(SolverFixtures.RunsToWin(graph, seed, shifted, out int completionTicks),
                Is.True, $"entry {entryIndex} shift {delta:+#;-#;0} must still win");
            Assert.That(completionTicks, Is.EqualTo(solved.CompletionTicks),
                $"entry {entryIndex} shift {delta:+#;-#;0} may not trade margin for a slower win");
        }
    }
}
