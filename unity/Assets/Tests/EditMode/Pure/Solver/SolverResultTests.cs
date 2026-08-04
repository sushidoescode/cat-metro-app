using System;
using System.Linq;
using NUnit.Framework;
using CatMetro.Domain;
using CatMetro.Domain.Solver;
using CatMetro.Tests.Domain;

namespace CatMetro.Tests.Solver
{
    // Criteria 6, 8, 9: the result record, the scoring pin, determinism against the shipped hasher.
    [TestFixture]
    public class SolverResultTests
    {
        [Test] // criterion 6 — every field populated for L001; CompletionTicks hand-computed
        public void L001_ResultRecord_FullyPopulated()
        {
            var graph = Fixtures.L001Shape();
            var r = LevelSolver.Solve(graph, 1001);
            Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Solved));
            // Hand computation: spawns at ticks 8 and 28 dominate — with the switch corrected at
            // any tick <= 16, cat 1 delivers at 30 and cat 2 at 50. Optimal completion = 50.
            Assert.That(r.CompletionTicks, Is.EqualTo(50), "hand-computed L001 optimum");
            Assert.That(r.SwitchesUsed, Is.EqualTo(1), "one toggle suffices");
            Assert.That(r.OptimalLog.Entries.Count, Is.EqualTo(1));
            Assert.That(r.BeamWidthUsed, Is.EqualTo(0), "BFS");
            Assert.That(r.NodesExpanded, Is.GreaterThan(0));
            Assert.That(r.Proxy.SolverOptimalTicks, Is.EqualTo(50));
            Assert.That(r.Proxy.TimeLimitTicks, Is.EqualTo(160));
            Assert.That(r.Proxy.MaxSimultaneousPendingDecisions, Is.GreaterThanOrEqualTo(1),
                "at least one switch has a pending decision while a train is inbound to J1");
            Assert.That(r.Proxy.MinQueueSlackAtPeak, Is.GreaterThanOrEqualTo(0));
            Assert.That(r.Proxy.SinglePerturbationsTried, Is.EqualTo(2 * r.OptimalLog.Entries.Count),
                "R: remove + shift(+1) per entry (handoff ruling)");
            Assert.That(r.Proxy.SinglePerturbationsWinnable, Is.InRange(0, r.Proxy.SinglePerturbationsTried));
        }

        [Test] // criterion 6/9 — the Tick − 1 identity, asserted as an equation
        public void CompletionTicks_EqualsRunToEndTickMinusOne()
        {
            var graph = Fixtures.L001Shape();
            var r = LevelSolver.Solve(graph, 1001);
            var end = ReplayHasher.RunToEnd(graph, 1001, r.OptimalLog);
            Assert.That(end.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            Assert.That(r.CompletionTicks, Is.EqualTo(end.Tick - 1),
                "Simulation sets Won AFTER incrementing Tick (Simulation.cs:155-162)");
        }

        [Test] // criterion 9 — replay-hash determinism of the optimal log
        public void OptimalLog_HashStableAcrossReplays()
        {
            var graph = SolverFixtures.TwoSwitchTwoCmd();
            var r = LevelSolver.Solve(graph, 7);
            var h1 = ReplayHasher.ComputeReplayHash(graph, 7, r.OptimalLog);
            var h2 = ReplayHasher.ComputeReplayHash(graph, 7, r.OptimalLog);
            Assert.That(h1, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(h2, Is.EqualTo(h1));
        }

        [Test] // criterion 8 — the scoring pin: no score-shaped member exists, ever
        public void SolveResult_CarriesNoScoreShapedMember()
        {
            var members = typeof(SolveResult).GetMembers()
                .Concat(typeof(DifficultyProxy).GetMembers())
                .Select(m => m.Name.ToLowerInvariant())
                .ToArray();
            foreach (var banned in new[] { "score", "star", "chain", "ticket" })
                Assert.That(members.Any(m => m.Contains(banned)), Is.False,
                    $"pins NEW-Q5/NEW-Q7: no '{banned}' member on the result surface");
        }
    }
}
