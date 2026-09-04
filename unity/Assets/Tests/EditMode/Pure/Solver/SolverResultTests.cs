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

        [Test] // review M5 — C/H/R asserted against an independent reference walk, not `>= 0`
        public void ProxyAxes_MatchIndependentReferenceComputation()
        {
            foreach (var (graph, seed) in new[]
            {
                (Fixtures.L001Shape(), 1001UL),
                (SolverFixtures.TwoSwitchTwoCmd(), 7UL),
            })
            {
                var r = LevelSolver.Solve(graph, seed);
                Assert.That(r.Verdict, Is.EqualTo(SolveVerdict.Solved), graph.LevelId);

                var (refC, refSlack) = ReferenceCAndH(graph, seed, r.OptimalLog);
                Assert.That(r.Proxy.MaxSimultaneousPendingDecisions, Is.EqualTo(refC),
                    $"{graph.LevelId}: axis C must equal the reference pending-decision walk");
                Assert.That(r.Proxy.MinQueueSlackAtPeak, Is.EqualTo(refSlack),
                    $"{graph.LevelId}: axis H must equal the reference peak-slack walk");

                var (refTried, refWinnable) = ReferenceR(graph, seed, r.OptimalLog);
                Assert.That(r.Proxy.SinglePerturbationsTried, Is.EqualTo(refTried), graph.LevelId);
                Assert.That(r.Proxy.SinglePerturbationsWinnable, Is.EqualTo(refWinnable),
                    $"{graph.LevelId}: axis R must equal the reference perturbation replays");

                Assert.That(r.Proxy.SolverOptimalTicks, Is.EqualTo(r.CompletionTicks), graph.LevelId);
                Assert.That(r.Proxy.TimeLimitTicks, Is.EqualTo(graph.TimeLimitTicks), graph.LevelId);
            }
        }

        // Reference implementation of the frozen axis-C/axis-H rules (handoff §proxy), written
        // against the public Domain surface only — deliberately duplicated from the solver so
        // drift in either copy fails this test.
        private static (int maxPending, int slackAtPeak) ReferenceCAndH(LevelGraph graph, ulong seed, CommandLog log)
        {
            int maxPending = 0, peakQueued = -1, slackAtPeak = 0, minCapacity = int.MaxValue;
            for (int n = 0; n < graph.NodeCount; n++)
                if (graph.NodeQueueCapacity[n] > 0 && graph.NodeQueueCapacity[n] < minCapacity)
                    minCapacity = graph.NodeQueueCapacity[n];

            var state = SimulationState.CreateInitial(graph, seed);
            while (state.Outcome.Kind == OutcomeKind.Running)
            {
                var due = log.Entries.Where(e => e.Tick == state.Tick - 1).ToArray();
                Simulation.Step(ref state, due);

                int pending = 0;
                for (int s = 0; s < graph.SwitchNode.Length; s++)
                {
                    int node = graph.SwitchNode[s];
                    bool active = state.NodeQueueCounts[node] > 0
                        || state.Trains.Any(tr =>
                            (tr.State == TrainState.OnEdge && graph.EdgeTo[tr.EdgeId] == node)
                            || (tr.State == TrainState.OnEdgeReverse
                                && graph.EdgeFrom[tr.EdgeId] == node)
                            || (tr.State == TrainState.ExpressHeldAtSource
                                && tr.NodeId == node));
                    if (active) pending++;
                }
                maxPending = Math.Max(maxPending, pending);

                int totalQueued = 0;
                for (int n = 0; n < graph.NodeCount; n++) totalQueued += state.NodeQueueCounts[n];
                if (totalQueued > peakQueued)
                {
                    peakQueued = totalQueued;
                    int minSlack = int.MaxValue;
                    for (int n = 0; n < graph.NodeCount; n++)
                    {
                        if (graph.NodeQueueCapacity[n] <= 0) continue;
                        minSlack = Math.Min(minSlack, graph.NodeQueueCapacity[n] - state.NodeQueueCounts[n]);
                    }
                    slackAtPeak = minSlack == int.MaxValue ? 0 : minSlack;
                }
            }
            if (peakQueued <= 0) slackAtPeak = minCapacity == int.MaxValue ? 0 : minCapacity;
            return (maxPending, slackAtPeak);
        }

        private static (int tried, int winnable) ReferenceR(LevelGraph graph, ulong seed, CommandLog log)
        {
            int tried = 0, winnable = 0;
            for (int i = 0; i < log.Entries.Count; i++)
            {
                var removed = new CommandLog();
                var shifted = new CommandLog();
                for (int j = 0; j < log.Entries.Count; j++)
                {
                    var e = log.Entries[j];
                    if (j != i) removed.Append(e);
                    shifted.Append(j == i ? new ToggleSwitchCommand(e.SwitchId, e.Tick + 1) : e);
                }
                tried += 2;
                if (SolverFixtures.RunsToWin(graph, seed, removed, out _)) winnable++;
                if (SolverFixtures.RunsToWin(graph, seed, shifted, out _)) winnable++;
            }
            return (tried, winnable);
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
