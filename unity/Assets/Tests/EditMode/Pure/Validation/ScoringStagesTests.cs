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
            // B: 4 nodes + 3 edges + 1 switch (A-C5-10).
            Assert.That(axes.B, Is.EqualTo(8));
            // E: one wave of 2 spawns 20 ticks apart -> both inside one 80-tick window; a
            // single red->red transition has zero entropy.
            Assert.That(axes.PeakTrains80, Is.EqualTo(2));
            Assert.That(axes.InterleaveEntropy, Is.EqualTo(0.0).Within(1e-12));
            // C: one switch, so at most one pending decision — and at least one while a train
            // is inbound to J1 (CM-C4 pins the proxy; here it is the axis input).
            Assert.That(axes.C, Is.EqualTo(1));
            // T: CompletionTicks 50 (CM-C4 hand computation) / timeLimitTicks 160.
            Assert.That(axes.T, Is.EqualTo(50.0 / 160.0).Within(1e-12));
            // H: zero-dwell pass-through keeps queues empty on the optimal trace, so the
            // zero-queue rule reports the minimum capacity — all L001 nodes default to 8.
            Assert.That(axes.HQueueSlack, Is.EqualTo(8));
            // R: remove the single toggle -> the pin fires (not won); shift +1 stays inside the
            // <=16 window (wins). tried 2, winnable 1.
            Assert.That(axes.RTried, Is.EqualTo(2));
            Assert.That(axes.RWinnable, Is.EqualTo(1));
        }

        [Test]
        public void L001_WeightedSum_UnderFixtureCaps_IsTheFrozenFormula()
        {
            var (dto, solve) = L001Solved();
            var axes = DifficultyStage.ComputeAxes(dto, solve);
            var caps = VFixtures.FullConfig().AxisBBandCaps["onboarding"];
            // Frozen normalisation (A-C5-10) with caps {20, 4, 4, 8}:
            // B 8/20 = .4 · E min(1,2/4)*.5 + (0/2)*.5 = .25 · C 1/4 = .25 · T .3125 ·
            // H 1 - 8/8 = 0 · R 1 - 1/2 = .5
            double expected = 0.20 * 0.4 + 0.25 * 0.25 + 0.20 * 0.25 + 0.15 * 0.3125
                + 0.15 * 0.0 + 0.05 * 0.5;
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
            Assert.That(v.Value, Does.Contain("B=8"), "raw axes always print");
        }

        [Test]
        public void CapsRowPresent_ComparisonRunsAndBlocksNormally()
        {
            // Under the fixture caps L001's computed value deviates from the authored 0.08 by
            // far more than 0.05 — the configured stage blocks (criterion 13's stage-8 pair).
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
