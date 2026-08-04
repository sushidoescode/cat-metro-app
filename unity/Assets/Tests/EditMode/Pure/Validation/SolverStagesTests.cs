using NUnit.Framework;
using CatMetro.Content;
using CatMetro.Content.Validation;
using CatMetro.Domain;
using CatMetro.Domain.Solver;
using CatMetro.Tests.Solver;
using Newtonsoft.Json.Linq;

namespace CatMetro.Tests.Validation
{
    // Criterion 5: stage 4 blocks ONLY on Unsolvable; NotFound and Indeterminate print their
    // counts and never block (Q-N; ADR-0008:117).
    [TestFixture]
    public class SolverStageTests
    {
        private static SolveResult Solve(byte[] level)
        {
            var imported = VFixtures.Import(level);
            return LevelSolver.Solve(imported.Graph, (ulong)imported.Dto.Seed);
        }

        [Test]
        public void Unsolvable_Blocks()
        {
            var solve = Solve(VFixtures.UnsolvableLevel());
            Assert.That(solve.Verdict, Is.EqualTo(SolveVerdict.Unsolvable), "fixture sanity");
            var v = SolverStage.Verdict(solve);
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(v.Blocks, Is.True);
        }

        [Test]
        public void Indeterminate_PrintsCountsAndDoesNotBlock()
        {
            var solve = Solve(VFixtures.AllPinnedLevel());
            Assert.That(solve.Verdict, Is.EqualTo(SolveVerdict.Indeterminate), "fixture sanity");
            var v = SolverStage.Verdict(solve);
            Assert.That(v.Blocks, Is.False, "Q-N: pinned exhaustion is not a proof");
            Assert.That(v.Detail, Does.Contain("Indeterminate").And.Contain("pinned"));
            Assert.That(v.Detail, Does.Contain(solve.PinnedPruned.ToString()), "the count is printed");
        }

        [Test]
        public void NotFound_PrintsWidthAndDoesNotBlock()
        {
            var solve = Solve(VFixtures.BeamMissLevel());
            Assert.That(solve.Verdict, Is.EqualTo(SolveVerdict.NotFound), "fixture sanity");
            var v = SolverStage.Verdict(solve);
            Assert.That(v.Blocks, Is.False, "ADR-0008:117 admits a human witness replay");
            Assert.That(v.Detail, Does.Contain("NotFound"));
        }

        [Test]
        public void Solved_Passes()
        {
            var v = SolverStage.Verdict(Solve(VFixtures.L001Bytes()));
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Pass));
        }
    }

    // Criterion 6: stage 5 — L001 passes (its zero-input run does not win) and an always-winning
    // board FAILS the stage.
    [TestFixture]
    public class TrivialityTests
    {
        [Test]
        public void L001_ZeroInputDoesNotWin_StagePasses()
        {
            var imported = VFixtures.Import(VFixtures.L001Bytes());
            var v = TrivialityStage.Check(imported.Graph, (ulong)imported.Dto.Seed);
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Pass), v.Detail);
        }

        [Test]
        public void ZeroInputWin_FailsTheStage()
        {
            var imported = VFixtures.Import(VFixtures.TrivialWinLevel());
            var v = TrivialityStage.Check(imported.Graph, (ulong)imported.Dto.Seed);
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Fail), "CM-R12.2: even L001 requires its one tap");
            Assert.That(v.Blocks, Is.True);
        }
    }

    // Criterion 7: stage 6 — robust fixture >= 70%, brittle fixture fails, onboarding window
    // bounds 12-16, plus jitter determinism (A-C5-2's whole point).
    [TestFixture]
    public class BrittlenessTests
    {
        private static StageVerdict Run(byte[] level, ValidatorConfig config)
        {
            var imported = VFixtures.Import(level);
            var solve = LevelSolver.Solve(imported.Graph, (ulong)imported.Dto.Seed);
            Assert.That(solve.Verdict, Is.EqualTo(SolveVerdict.Solved), "stage 6 needs a winning log");
            return BrittlenessStage.Check(imported.Dto, imported.Graph, solve.OptimalLog, config);
        }

        [Test]
        public void L001_IsRobust_Passes()
        {
            var v = Run(VFixtures.L001Bytes(), VFixtures.BareConfig());
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Pass), v.Detail);
            Assert.That(v.Value, Does.Contain("%"), "retention is printed");
        }

        [Test]
        public void BrittleWindow_Fails()
        {
            var v = Run(VFixtures.BrittleLevel(), VFixtures.BareConfig());
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Fail), v.Detail);
            Assert.That(v.Blocks, Is.True);
            Assert.That(v.Detail, Does.Contain("window").IgnoreCase,
                "this fixture fails the WINDOW limb specifically (review F1: no more Or)");
        }

        [Test] // review F1 — the >= 70% retention rule itself, on losses that are NOT pins
        public void JitterRetention_BelowSeventy_FailsTheRetentionLimb()
        {
            var imported = VFixtures.Import(VFixtures.JitterLossLevel());
            ulong seed = (ulong)imported.Dto.Seed;
            // The crafted log sits on both window edges — deliberately NOT the solver optimum
            // (which sits at the early edge where negative jitter clamps to safety).
            var crafted = SolverFixtures.Log((0, 3), (1, 5));
            Assert.That(SolverFixtures.RunsToWin(imported.Graph, seed, crafted, out _), Is.True,
                "fixture sanity: the crafted log wins as written");

            var v = BrittlenessStage.Check(imported.Dto, imported.Graph, crafted, VFixtures.BareConfig());
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Fail), v.Detail + " / " + v.Value);
            Assert.That(v.Blocks, Is.True);
            Assert.That(v.Detail, Does.Contain("retention"), "the RETENTION limb fired");
            Assert.That(v.Detail, Does.Not.Contain("window"), "…and only the retention limb");
            Assert.That(v.Value, Does.Contain("pinned=0"),
                "every loss is a timeout on this red-only board — the pin rule is not involved");
        }

        [Test]
        public void OnboardingBand_OutsideTwelveToSixteen_Fails()
        {
            var level = VFixtures.Level(o => o["meta"]["minActionWindowTicks"] = 8);
            var v = Run(level, VFixtures.BareConfig());
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(v.Detail, Does.Contain("12").And.Contain("16"), "the onboarding bound is named");
        }

        [Test]
        public void JitterVerdict_IsDeterministicAcrossRuns()
        {
            var a = Run(VFixtures.L001Bytes(), VFixtures.BareConfig());
            var b = Run(VFixtures.L001Bytes(), VFixtures.BareConfig());
            Assert.That(b.Value, Is.EqualTo(a.Value), "Pcg32-seeded jitter: same level, same figure");
            Assert.That(b.Code, Is.EqualTo(a.Code));
        }

        [Test]
        public void NoWinningLog_ReportsSkippedNotABlockingVerdict()
        {
            var imported = VFixtures.Import(VFixtures.L001Bytes());
            var v = BrittlenessStage.Check(imported.Dto, imported.Graph, null, VFixtures.BareConfig());
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Skipped));
            Assert.That(v.Blocks, Is.False, "stage 4 already carries the verdict that matters");
        }
    }

    // Criterion 8 (stage 7): two < three blocks today; the reachability limb is PINNED(NEW-Q5);
    // the band-slack row's absence/presence flips the marker (half of criterion 13's stage-7 pair).
    [TestFixture]
    public class StarCheckTests
    {
        [Test]
        public void TwoNotBelowThree_Fails()
        {
            var level = VFixtures.Level(o => o["win"]["stars"]["two"] = 300);
            var v = StarCheckStage.Check(VFixtures.Import(level).Dto, VFixtures.BareConfig());
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(v.Blocks, Is.True, "stars.two < stars.three blocks today");
        }

        [Test]
        public void L001_WithSlackRow_ReachabilityLimb_ReportsPinnedNewQ5()
        {
            // With the row present the comparison would run — but scoring is pinned, so the limb
            // reports the pin (the stronger fact) and never blocks.
            var v = StarCheckStage.Check(VFixtures.Import(VFixtures.L001Bytes()).Dto, VFixtures.FullConfig());
            Assert.That(v.Code, Is.EqualTo(StageVerdictCode.Pinned));
            Assert.That(v.Detail, Does.Contain("PINNED(NEW-Q5)"));
            Assert.That(v.Blocks, Is.False);
        }
    }
}
