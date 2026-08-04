using NUnit.Framework;
using CatMetro.Domain;
using CatMetro.Domain.Solver;
using CatMetro.Tests.Domain;

namespace CatMetro.Tests.Solver
{
    // Criterion 3: BFS is EXACT for <=2-switch boards — provably tick-minimal, compared against
    // an independent brute-force enumeration (<=2-command logs; fixtures designed so the true
    // optimum needs <=2 commands — handoff planner ruling).
    [TestFixture]
    public class SolverBfsTests
    {
        [Test]
        public void OneSwitch_L001_MatchesBruteForce()
        {
            var graph = Fixtures.L001Shape();
            var brute = SolverFixtures.BruteForceBest(graph, 1001);
            Assert.That(brute, Is.Not.Null, "L001 is solvable");

            var r = LevelSolver.Solve(graph, 1001);
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Solved));
            Assert.That(r.BeamWidthUsed, Is.EqualTo(0), "<=2 switches is BFS territory");
            Assert.That(r.CompletionTicks, Is.EqualTo(brute.Value.ticks), "tick-minimality vs brute force");
            SolverFixtures.AssertSameLog(brute.Value.log, r.OptimalLog, "optimal log under the criterion-7 order");
        }

        [Test]
        public void TwoSwitch_MatchesBruteForce()
        {
            var graph = SolverFixtures.TwoSwitchTwoCmd();
            var brute = SolverFixtures.BruteForceBest(graph, 7);
            Assert.That(brute, Is.Not.Null, "the two-command board is solvable");
            Assert.That(brute.Value.log.Entries.Count, Is.EqualTo(2), "fixture sanity: optimum needs both toggles");

            var r = LevelSolver.Solve(graph, 7);
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Solved));
            Assert.That(r.BeamWidthUsed, Is.EqualTo(0));
            Assert.That(r.CompletionTicks, Is.EqualTo(brute.Value.ticks));
            SolverFixtures.AssertSameLog(brute.Value.log, r.OptimalLog, "two-switch optimal log");
        }

        [Test]
        public void OneSwitch_Unsolvable_IsProvenUnsolvable()
        {
            var r = LevelSolver.Solve(SolverFixtures.UnsolvableNoPins(), 3);
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Unsolvable), "BFS exhaustion is a proof");
            Assert.That(r.PinnedPruned, Is.EqualTo(0), "no pinned branch on this board");
            Assert.That(r.OptimalLog.Entries.Count, Is.EqualTo(0));
        }
    }

    // Criterion 4: beam beyond 2 switches at the authored widths, ascending; miss is NotFound,
    // never Unsolvable.
    [TestFixture]
    public class SolverBeamTests
    {
        [Test]
        public void ThreeSwitch_SolvedAtFirstWidth_ReportsWidth()
        {
            var r = LevelSolver.Solve(SolverFixtures.ThreeSwitchEscalation(), 5); // default widths
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Solved));
            Assert.That(r.BeamWidthUsed, Is.EqualTo(SolverBounds.BEAM_WIDTHS[0]),
                "the tiny board solves at the first authored width, and the width is reported");
        }

        [Test]
        public void Escalation_ObservedWithForcedNarrowFirstWidth()
        {
            // Escalation law (handoff): a width-1 beam always keeps the least-toggled state
            // (SwitchesUsed at digest byte 24 precedes SwitchRoutes at 44 in the lex order), and
            // this board's zero-toggle line dies pinned — so width 1 MUST fail and escalate.
            var r = LevelSolver.Solve(SolverFixtures.ThreeSwitchEscalation(), 5, beamWidths: new[] { 1, 2500 });
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Solved));
            Assert.That(r.BeamWidthUsed, Is.EqualTo(2500), "escalation occurred past the width-1 leg");
        }

        [Test]
        public void BeamMiss_IsNotFoundNeverUnsolvable()
        {
            var r = LevelSolver.Solve(SolverFixtures.BeamMissNoPins(), 5);
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.NotFound),
                "ADR-0008:117: a human witness replay is admissible where beam fails — beam never proves unsolvability");
            Assert.That(r.NotFoundReason, Is.EqualTo(NotFoundReason.Beam));
            Assert.That(r.BeamWidthUsed, Is.EqualTo(SolverBounds.BEAM_WIDTHS[SolverBounds.BEAM_WIDTHS.Length - 1]),
                "the miss is reported at the last width tried");
        }

        [Test]
        public void DefaultWidths_AreTheAuthoredConstants()
        {
            // The injection point cannot drift from the declared constant (mirrors criterion 11).
            var p = typeof(LevelSolver).GetMethod("Solve").GetParameters();
            Assert.That(p[2].HasDefaultValue, Is.True);
            Assert.That(p[2].DefaultValue, Is.EqualTo(SolverBounds.MAX_NODES_EXPANDED), "budget default == constant");
            Assert.That(p[3].HasDefaultValue, Is.True);
            Assert.That(p[3].DefaultValue, Is.Null, "null widths == the authored BEAM_WIDTHS at runtime");
            Assert.That(SolverBounds.BEAM_WIDTHS, Is.EqualTo(new[] { 1000, 2500, 5000 }), "ADR-0008:116 verbatim");
        }
    }
}
