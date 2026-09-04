using System.Linq;
using NUnit.Framework;
using CatMetro.Content;
using CatMetro.Content.Validation;
using CatMetro.Domain.Solver;
using Newtonsoft.Json.Linq;

namespace CatMetro.Tests.Validation
{
    // Criterion 9: stage 8 — every raw axis hand-derived on L001, the weighted sum under a
    // fixture caps row, UNCONFIGURED propagation without it, and axis H printed PARTIAL(Q-J).
    [TestFixture]
    public class DifficultyTests
    {
        private static (LevelDto dto, SolveResult solve) L001Solved()
        {
            var imported = VFixtures.Import(VFixtures.L001Bytes());
            var solve = LevelSolver.Solve(imported.Graph, (ulong)imported.Dto.Seed);
            Assert.That(solve.Verdict, Is.EqualTo(SolveVerdict.Solved));
            return (imported.Dto, solve);
        }

        [Test]
        public void L001_RawAxes_AreHandDerived()
        {
            var (dto, solve) = L001Solved();
            var axes = DifficultyStage.ComputeAxes(dto, solve);
            Assert.That(axes.B,
                Is.EqualTo(dto.Nodes.Length + dto.Edges.Length + dto.Switches.Length));
            int totalSpawns = dto.Waves.ToArray().Sum(wave => wave.Count);
            Assert.That(axes.PeakTrains80, Is.GreaterThan(0).And.LessThanOrEqualTo(totalSpawns));
            Assert.That(axes.InterleaveEntropy, Is.GreaterThanOrEqualTo(0.0));
            Assert.That(axes.C, Is.EqualTo(solve.Proxy.MaxSimultaneousPendingDecisions));
            Assert.That(axes.T,
                Is.EqualTo(solve.Proxy.SolverOptimalTicks / (double)dto.Win.TimeLimitTicks)
                    .Within(1e-12));
            Assert.That(axes.HQueueSlack, Is.EqualTo(solve.Proxy.MinQueueSlackAtPeak));
            Assert.That(axes.RTried, Is.EqualTo(solve.Proxy.SinglePerturbationsTried));
            Assert.That(axes.RWinnable, Is.EqualTo(solve.Proxy.SinglePerturbationsWinnable));
            Assert.That(axes.RWinnable, Is.LessThanOrEqualTo(axes.RTried));
        }

        [Test]
        public void L001_WeightedSum_UsesTheDocumentedNormalisedAxes()
        {
            var (dto, solve) = L001Solved();
            var axes = DifficultyStage.ComputeAxes(dto, solve);
            var caps = VFixtures.FullConfig().AxisBBandCaps["onboarding"];
            double nB = System.Math.Min(1.0, axes.B / (double)caps.MaxComplexity);
            double nE = System.Math.Min(1.0, axes.PeakTrains80 / (double)caps.PeakTrainsCap) * 0.5
                + System.Math.Min(1.0, axes.InterleaveEntropy / 2.0) * 0.5;
            double nC = System.Math.Min(1.0, axes.C / (double)caps.MaxConcurrency);
            double nT = System.Math.Min(1.0, axes.T);
            double nH = 1.0 - System.Math.Min(1.0,
                axes.HQueueSlack / (double)caps.MaxHeadroom);
            double nR = 1.0 - (axes.RTried == 0
                ? 1.0
                : axes.RWinnable / (double)axes.RTried);
            double expected = 0.20 * nB + 0.25 * nE + 0.20 * nC + 0.15 * nT
                + 0.15 * nH + 0.05 * nR;
            Assert.That(DifficultyStage.WeightedSum(axes, caps), Is.EqualTo(expected).Within(1e-12));
        }

        [Test]
        public void NoCapsRow_StageIsUnconfigured_ButRawAxesArePrinted()
        {
            var (dto, solve) = L001Solved();
            var v = DifficultyStage.Check(dto, solve, VFixtures.BareConfig());
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Unconfigured));
            Assert.That(v.Detail, Does.Contain("UNCONFIGURED(axisBBandCaps)"));
            Assert.That(v.Blocks, Is.False);
            int expectedB = dto.Nodes.Length + dto.Edges.Length + dto.Switches.Length;
            Assert.That(v.Value, Does.Contain("B=" + expectedB), "raw axes always print");
        }

        [Test]
        public void CapsRowPresent_ComparisonRunsAndBlocksNormally()
        {
            // Under the deliberately tight fixture caps the current L001 artifact deviates from
            // its authored target by more than 0.05, so the configured stage blocks.
            var (dto, solve) = L001Solved();
            var v = DifficultyStage.Check(dto, solve, VFixtures.FullConfig());
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(v.Blocks, Is.True);
            Assert.That(v.Detail, Does.Contain("0.05"), "the tolerance is named");
        }

        [Test]
        public void AxisH_PrintsPartialQJ()
        {
            var (dto, solve) = L001Solved();
            var v = DifficultyStage.Check(dto, solve, VFixtures.BareConfig());
            Assert.That(v.Value, Does.Contain("PARTIAL(Q-J)"),
                "the stage never claims to have computed the spec's H (queue term only)");
        }
    }

    // Criterion 10: stage 9 — near-identical pair closer than a dissimilar pair; UNCONFIGURED
    // without the row; with the row a recycled level blocks (criterion 13's stage-9 pair).
    [TestFixture]
    public class NoveltyTests
    {
        [Test]
        public void NearIdentical_IsCloserThanDissimilar()
        {
            var l001 = VFixtures.Import(VFixtures.L001Bytes()).Dto;
            var near = VFixtures.Import(VFixtures.Level(o => o["board"]["edges"][0]["travelTicks"] = 11)).Dto;
            var far = VFixtures.Import(VFixtures.StressLevelBytes(0)).Dto; // L701
            double dNear = NoveltyStage.Distance(NoveltyStage.Vector(l001), NoveltyStage.Vector(near));
            double dFar = NoveltyStage.Distance(NoveltyStage.Vector(l001), NoveltyStage.Vector(far));
            Assert.That(dNear, Is.LessThan(dFar));
        }

        [Test]
        public void NoThresholdRow_ReportsUnconfiguredWithDistancesPrinted()
        {
            var l001 = VFixtures.Import(VFixtures.L001Bytes()).Dto;
            var near = VFixtures.Import(VFixtures.Level(o => o["board"]["edges"][0]["travelTicks"] = 11)).Dto;
            var v = NoveltyStage.Check(near, new[] { l001 }, VFixtures.BareConfig());
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Unconfigured));
            Assert.That(v.Detail, Is.EqualTo("UNCONFIGURED(noveltyMinDistance)"));
            Assert.That(v.Blocks, Is.False);
            Assert.That(v.Value, Is.Not.Empty, "distances always print");
        }

        [Test]
        public void ThresholdRowPresent_RecycledLevelBlocksNormally()
        {
            var l001 = VFixtures.Import(VFixtures.L001Bytes()).Dto;
            var near = VFixtures.Import(VFixtures.Level(o => o["board"]["edges"][0]["travelTicks"] = 11)).Dto;
            var v = NoveltyStage.Check(near, new[] { l001 }, VFixtures.FullConfig()); // min distance 5.0
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(v.Blocks, Is.True);

            var first = NoveltyStage.Check(l001, new LevelDto[0], VFixtures.FullConfig());
            Assert.That(first.Code, Is.EqualTo(StageVerdictCode.Pass), "no priors: nothing to recycle");
        }
    }

    // Criterion 13's stage-7 pair lives here beside its siblings: the row's absence/presence
    // flips UNCONFIGURED(starBandSlack) <-> PINNED(NEW-Q5); neither blocks.
    [TestFixture]
    public class StarSlackConfigTests
    {
        [Test]
        public void NoSlackRow_ReportsUnconfiguredStarBandSlack()
        {
            var v = StarCheckStage.Check(VFixtures.Import(VFixtures.L001Bytes()).Dto, VFixtures.BareConfig());
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Unconfigured));
            Assert.That(v.Detail, Does.Contain("UNCONFIGURED(starBandSlack)"));
            Assert.That(v.Blocks, Is.False);
        }
    }
}
