using System.Linq;
using CatMetro.Domain;
using CatMetro.Domain.Solver;
using NUnit.Framework;

namespace CatMetro.Tests.Solver
{
    [TestFixture]
    public sealed class RuntimeMechanicsSolverTests
    {
        [Test]
        public void HardCap_ZeroIsUnsolvable_OneFindsAcceptedRecovery()
        {
            var blocked = LevelSolver.Solve(RecoveryGraph(perfectMaxSwitches: 0), 401);
            Assert.That(blocked.Verdict, Is.EqualTo(SolveVerdict.Unsolvable));
            Assert.That(blocked.BeamWidthUsed, Is.Zero);
            Assert.That(blocked.PinnedPruned, Is.Zero);

            var graph = RecoveryGraph(perfectMaxSwitches: 1);
            var solve = LevelSolver.Solve(graph, 401);
            Assert.That(solve.Verdict, Is.EqualTo(SolveVerdict.Solved));
            Assert.That(solve.BeamWidthUsed, Is.Zero);
            Assert.That(solve.SwitchesUsed, Is.EqualTo(1));
            Assert.That(solve.OptimalLog.Entries.Count, Is.EqualTo(1));
            var end = ReplayHasher.RunToEnd(graph, 401, solve.OptimalLog);
            Assert.That(end.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            Assert.That(end.SwitchesUsed, Is.EqualTo(solve.OptimalLog.Entries.Count),
                "an optimal log contains accepted commands only");
        }

        [Test]
        public void CapZero_PrunesToggleSuccessorsWithinTheExactWorkCeiling()
        {
            var solve = LevelSolver.Solve(
                CapPruningGraph(), 402, maxNodesExpanded: 10);

            Assert.That(solve.Verdict, Is.EqualTo(SolveVerdict.Unsolvable),
                "five states plus five empty successors fit exactly; generating ignored taps would hit Budget");
            Assert.That(solve.NotFoundReason, Is.EqualTo(NotFoundReason.None));
            Assert.That(solve.NodesExpanded, Is.EqualTo(5));
            Assert.That(solve.OptimalLog.Entries, Is.Empty);
        }

        [Test]
        public void CooldownSolver_WaitsThroughBothLockedTicks_AndReturnsAppliedLog()
        {
            var graph = CooldownAlternationGraph();
            var solve = LevelSolver.Solve(graph, 403);

            Assert.That(solve.Verdict, Is.EqualTo(SolveVerdict.Solved));
            Assert.That(solve.BeamWidthUsed, Is.Zero);
            Assert.That(solve.SwitchesUsed, Is.EqualTo(2));
            Assert.That(solve.OptimalLog.Entries.Select(entry => entry.Tick),
                Is.EqualTo(new[] { 0, 3 }),
                "the second accepted command applies only after processing ticks 2 and 3 unlock it");

            var end = ReplayHasher.RunToEnd(graph, 403, solve.OptimalLog);
            Assert.That(end.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            Assert.That(end.SwitchesUsed, Is.EqualTo(solve.OptimalLog.Entries.Count));
        }

        [Test]
        public void EvaluateLog_ReportsAppliedCountWhenExcessReplayTapIsIgnored()
        {
            var graph = AcceptedBeforeArrivalGraph();
            var log = new CommandLog();
            log.Append(new ToggleSwitchCommand(0, 0));
            log.Append(new ToggleSwitchCommand(0, 0));

            var result = LevelSolver.EvaluateLog(graph, 404, log);

            Assert.That(result.Verdict, Is.EqualTo(SolveVerdict.Solved));
            Assert.That(result.OptimalLog.Entries.Count, Is.EqualTo(2),
                "EvaluateLog preserves the attempted external replay");
            Assert.That(result.SwitchesUsed, Is.EqualTo(1),
                "reporting reflects the one accepted command, not raw log length");
        }

        private static LevelGraph RecoveryGraph(int perfectMaxSwitches) => new LevelGraph(
            "FX-CAP-RECOVERY", 3, new[] { 8, 8, 8 },
            new[] { 0, 0 }, new[] { 1, 2 }, new[] { 1, 1 },
            new[] { 0 },
            new[] { new[] { 0, 1 } }, new[] { 0 }, new byte[] { 0 },
            new[] { 1, 2 },
            new[] { new[] { CatColor.Blue }, new[] { CatColor.Red } },
            new[] { 1, 1 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            1, 30, 8, 1,
            perfectMaxSwitches: perfectMaxSwitches);

        private static LevelGraph CapPruningGraph() => new LevelGraph(
            "FX-CAP-PRUNE", 3, new[] { 8, 8, 8 },
            new[] { 0, 0 }, new[] { 1, 2 }, new[] { 1, 1 },
            new[] { 0 },
            new[] { new[] { 0, 1 } }, new[] { 0 }, new byte[] { 0 },
            new[] { 1, 2 },
            new[] { new[] { CatColor.Red }, new[] { CatColor.Red } },
            new[] { 1, 1 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            2, 5, 8, 1,
            perfectMaxSwitches: 0);

        private static LevelGraph CooldownAlternationGraph() => new LevelGraph(
            "FX-COOLDOWN-SOLVE", 3, new[] { 8, 8, 8 },
            new[] { 0, 0 }, new[] { 1, 2 }, new[] { 1, 1 },
            new[] { 0 },
            new[] { new[] { 0, 1 } }, new[] { 0 }, new byte[] { 0 },
            new[] { 1, 2 },
            new[] { new[] { CatColor.Red }, new[] { CatColor.Blue } },
            new[] { 1, 1 },
            new[] { 0, 1, 4 },
            new[] { CatColor.Red, CatColor.Blue, CatColor.Red },
            new[] { 1, 1, 1 }, new[] { 1, 1, 1 },
            3, 12, 8, 3,
            perfectMaxSwitches: 2,
            switchCooldownTicks: new[] { 2 });

        private static LevelGraph AcceptedBeforeArrivalGraph() => new LevelGraph(
            "FX-CAP-EVAL", 3, new[] { 8, 8, 8 },
            new[] { 0, 0 }, new[] { 1, 2 }, new[] { 2, 2 },
            new[] { 0 },
            new[] { new[] { 0, 1 } }, new[] { 0 }, new byte[] { 0 },
            new[] { 1, 2 },
            new[] { new[] { CatColor.Red }, new[] { CatColor.Red } },
            new[] { 1, 1 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            1, 10, 8, 1,
            perfectMaxSwitches: 1);
    }
}
