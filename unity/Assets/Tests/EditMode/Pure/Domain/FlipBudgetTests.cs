using System;
using NUnit.Framework;
using CatMetro.Domain;

namespace CatMetro.Tests.Domain
{
    // Flip budget: `win.perfectMaxSwitches` finally has a consumer (Domain/FlipBudget.cs).
    //
    // The tests are in three groups:
    //   1. the pure tier arithmetic, boundary by boundary;
    //   2. DIGEST INVARIANCE — proof that par cannot move a golden replay hash, which is the
    //      property that lets this land on top of the 857-test core;
    //   3. an end-to-end run over the L001 golden shape.
    [TestFixture]
    public class FlipBudgetTests
    {
        // ---- 1. tier arithmetic ---------------------------------------------------------------

        [TestCase(0, 1)]   // par 0 still gets a middle band rather than collapsing to pass/fail
        [TestCase(1, 2)]
        [TestCase(2, 4)]
        [TestCase(3, 6)]
        [TestCase(4, 8)]   // 1..4 is the whole authored range across the 17 levels
        public void WithinMax_IsTwicePar_FlooredAtParPlusOne(int par, int expected)
        {
            Assert.That(FlipBudget.WithinMaxFor(par), Is.EqualTo(expected));
        }

        [Test]
        public void WithinMax_Unbudgeted_IsTheSentinel()
        {
            Assert.That(FlipBudget.WithinMaxFor(FlipBudget.Unbudgeted), Is.EqualTo(FlipBudget.Unbudgeted));
        }

        [Test]
        public void TierFor_WalksTheBoundariesExactly()
        {
            const int par = 3; // Perfect <= 3, Within <= 6, Over from 7
            Assert.That(FlipBudget.TierFor(par, 0), Is.EqualTo(FlipTier.Perfect), "no flips at all");
            Assert.That(FlipBudget.TierFor(par, 3), Is.EqualTo(FlipTier.Perfect), "exactly par is Perfect");
            Assert.That(FlipBudget.TierFor(par, 4), Is.EqualTo(FlipTier.Within), "one over par drops a tier");
            Assert.That(FlipBudget.TierFor(par, 6), Is.EqualTo(FlipTier.Within), "the Within ceiling holds");
            Assert.That(FlipBudget.TierFor(par, 7), Is.EqualTo(FlipTier.Over), "one past the ceiling is Over");
            Assert.That(FlipBudget.TierFor(par, 999), Is.EqualTo(FlipTier.Over), "and it stays Over");
        }

        [Test]
        public void TierFor_ParZero_RequiresATouchlessSolve()
        {
            Assert.That(FlipBudget.TierFor(0, 0), Is.EqualTo(FlipTier.Perfect));
            Assert.That(FlipBudget.TierFor(0, 1), Is.EqualTo(FlipTier.Within));
            Assert.That(FlipBudget.TierFor(0, 2), Is.EqualTo(FlipTier.Over));
        }

        [Test]
        public void Unbudgeted_NeverJudges()
        {
            var s = FlipBudget.Evaluate(FlipBudget.Unbudgeted, 40);
            Assert.That(s.IsBudgeted, Is.False, "the HUD hides the counter on this");
            Assert.That(s.Tier, Is.EqualTo(FlipTier.Within), "a win is a win, but no free 3-star either");
            Assert.That(s.IsOverPar, Is.False, "you cannot exceed a budget that does not exist");
            Assert.That(s.Remaining, Is.EqualTo(0));
            Assert.That(FlipBudget.ExceedsHardWall(FlipBudget.Unbudgeted, 40), Is.False);
        }

        [Test]
        public void Remaining_GoesNegativeByExactlyTheOverspend()
        {
            Assert.That(FlipBudget.Evaluate(4, 1).Remaining, Is.EqualTo(3), "three flips in hand");
            Assert.That(FlipBudget.Evaluate(4, 4).Remaining, Is.EqualTo(0), "on the line");
            Assert.That(FlipBudget.Evaluate(4, 7).Remaining, Is.EqualTo(-3), "three over — the near-miss number");
        }

        [Test]
        public void IsOverPar_TripsExactlyOnePastPar()
        {
            Assert.That(FlipBudget.Evaluate(2, 2).IsOverPar, Is.False);
            Assert.That(FlipBudget.Evaluate(2, 3).IsOverPar, Is.True);
        }

        [Test]
        public void Stars_MapTiersAndRequireAWin()
        {
            Assert.That(FlipBudget.StarsFor(FlipTier.Perfect), Is.EqualTo(3));
            Assert.That(FlipBudget.StarsFor(FlipTier.Within), Is.EqualTo(2));
            Assert.That(FlipBudget.StarsFor(FlipTier.Over), Is.EqualTo(1), "a win over par is still a win");

            Assert.That(FlipBudget.StarsFor(FlipTier.Perfect, won: false), Is.EqualTo(0),
                "a perfect flip count on a lost run earns nothing");
            Assert.That(FlipBudget.StarsFor(FlipTier.Perfect, won: true), Is.EqualTo(3));
        }

        [Test]
        public void ExceedsHardWall_IsTheAlternativeSemanticsPredicate()
        {
            // Not wired into Simulation — kept honest so switching to a hard wall is wiring,
            // not design. See docs/design/FLIP-BUDGET.md.
            Assert.That(FlipBudget.ExceedsHardWall(2, 2), Is.False, "spending the last flip is legal");
            Assert.That(FlipBudget.ExceedsHardWall(2, 3), Is.True, "the flip after it is not");
        }

        [Test]
        public void IsPerfectFlow_RequiresAllFourGates_AsProductSpecAlreadySaid()
        {
            // product_spec.md:238 — win, zero rejections, zero Overloads, within par.
            Assert.That(FlipBudget.IsPerfectFlow(par: 2, switchesUsed: 2, rejections: 0, overloads: 0, won: true),
                Is.True, "all four gates open");

            Assert.That(FlipBudget.IsPerfectFlow(2, 3, 0, 0, true), Is.False, "over par");
            Assert.That(FlipBudget.IsPerfectFlow(2, 2, 1, 0, true), Is.False, "a rejection breaks it");
            Assert.That(FlipBudget.IsPerfectFlow(2, 2, 0, 1, true), Is.False, "an Overload breaks it");
            Assert.That(FlipBudget.IsPerfectFlow(2, 2, 0, 0, false), Is.False, "a loss breaks it");
        }

        [Test]
        public void Evaluate_NullState_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => FlipBudget.Evaluate((SimulationState)null));
        }

        // ---- 2. digest invariance -------------------------------------------------------------

        [Test]
        public void Par_IsNormalisedToTheSingleUnbudgetedSentinel()
        {
            var g = Budgeted(Fixtures.L001Shape(), -7);
            Assert.That(g.PerfectMaxSwitches, Is.EqualTo(FlipBudget.Unbudgeted),
                "downstream code tests one value, not a range of negatives");
        }

        [Test]
        public void DefaultGraph_IsUnbudgeted_SoNothingChangesForExistingFixtures()
        {
            Assert.That(Fixtures.L001Shape().PerfectMaxSwitches, Is.EqualTo(FlipBudget.Unbudgeted));
            Assert.That(Fixtures.L001Shape().Misdelivery, Is.EqualTo(MisdeliveryPolicy.Pinned));
        }

        [Test]
        public void Par_DoesNotChangeDigestLength()
        {
            var plain = Fixtures.L001Shape();
            var budgeted = Budgeted(Fixtures.L001Shape(), 1);
            Assert.That(SimulationState.CreateInitial(budgeted, Fixtures.L001Seed).DigestLength(),
                Is.EqualTo(SimulationState.CreateInitial(plain, Fixtures.L001Seed).DigestLength()));
        }

        [Test]
        public void Par_DoesNotChangeASingleDigestByte()
        {
            // THE load-bearing test. Par lives on LevelGraph, which is not digest material, so an
            // identical run under an identical log must hash identically with and without a budget.
            var a = Fixtures.RunThroughTick(Fixtures.L001Shape(), Fixtures.L001Seed, Fixtures.GoldenLog(), 60);
            var b = Fixtures.RunThroughTick(Budgeted(Fixtures.L001Shape(), 1), Fixtures.L001Seed, Fixtures.GoldenLog(), 60);

            var da = new byte[a.DigestLength()];
            var db = new byte[b.DigestLength()];
            a.WriteDigest(da);
            b.WriteDigest(db);

            Assert.That(db, Is.EqualTo(da), "a flip budget must never move a golden replay hash");
        }

        // ---- 3. end to end over the golden shape ----------------------------------------------

        [Test]
        public void GoldenL001Run_SpendsOneFlip_AndScoresPerfectAgainstItsAuthoredPar()
        {
            // L001.json authors perfectMaxSwitches: 1, and the golden log toggles S1 exactly once.
            var end = Fixtures.RunThroughTick(
                Budgeted(Fixtures.L001Shape(), 1), Fixtures.L001Seed, Fixtures.GoldenLog(), 60);

            Assert.That(end.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            Assert.That(end.SwitchesUsed, Is.EqualTo(1));

            var s = end.FlipStatus;
            Assert.That(s.Par, Is.EqualTo(1));
            Assert.That(s.Used, Is.EqualTo(1));
            Assert.That(s.Remaining, Is.EqualTo(0), "spent to the line");
            Assert.That(s.Tier, Is.EqualTo(FlipTier.Perfect));
            Assert.That(end.FlipStars, Is.EqualTo(3));
            Assert.That(end.IsPerfectFlow, Is.True, "won, no rejections, no Overloads, within par");
        }

        [Test]
        public void SameWin_WithWastedFlips_DropsTierWithoutLosingTheRun()
        {
            // Toggle S1 four times: ticks 12, 13, 14, 15 lands it back on E2 (RED) before the
            // first cat reaches J1 at tick 18, so the run still WINS — it just wins untidily.
            var log = new CommandLog();
            log.Append(new ToggleSwitchCommand(0, 12));
            log.Append(new ToggleSwitchCommand(0, 13));
            log.Append(new ToggleSwitchCommand(0, 14));
            log.Append(new ToggleSwitchCommand(0, 15));

            var end = Fixtures.RunThroughTick(Budgeted(Fixtures.L001Shape(), 1), Fixtures.L001Seed, log, 60);

            Assert.That(end.Outcome.Kind, Is.EqualTo(OutcomeKind.Won), "over budget NEVER costs the run");
            Assert.That(end.SwitchesUsed, Is.EqualTo(4));

            var s = end.FlipStatus;
            Assert.That(s.IsOverPar, Is.True);
            Assert.That(s.Remaining, Is.EqualTo(-3));
            Assert.That(s.Tier, Is.EqualTo(FlipTier.Over), "4 > WithinMax(1) == 2");
            Assert.That(end.FlipStars, Is.EqualTo(1), "still a win, still a star");
            Assert.That(end.IsPerfectFlow, Is.False);
        }

        [Test]
        public void FlipStatus_TracksTheCounterLiveMidRun()
        {
            var graph = Budgeted(Fixtures.L001Shape(), 1);
            var state = SimulationState.CreateInitial(graph, Fixtures.L001Seed);

            Assert.That(state.FlipStatus.Used, Is.EqualTo(0));
            Assert.That(state.FlipStatus.Remaining, Is.EqualTo(1), "one flip in hand before anything happens");
            Assert.That(state.FlipStars, Is.EqualTo(0), "nothing is earned until the run is won");

            var seen = new System.Collections.Generic.List<int>();
            var end = Fixtures.RunThroughTick(graph, Fixtures.L001Seed, Fixtures.GoldenLog(), 60,
                st => seen.Add(st.FlipStatus.Used));

            Assert.That(seen, Does.Contain(0), "counter starts at zero");
            Assert.That(seen, Does.Contain(1), "and reaches one when the toggle applies");
            Assert.That(end.FlipStatus.Used, Is.EqualTo(end.SwitchesUsed),
                "the status is a view of SwitchesUsed, never a second copy of it");
        }

        // A copy of the L001 shape carrying a par. Built here rather than in Fixtures.cs so this
        // lane does not edit a file every other Domain suite shares.
        private static LevelGraph Budgeted(LevelGraph _, int par) => new LevelGraph(
            "L001", 4,
            new[] { 8, 8, 8, 8 },
            new[] { 0, 1, 1 }, new[] { 1, 2, 3 }, new[] { 10, 12, 12 },
            new[] { 0 },
            new[] { new[] { 1, 2 } }, new[] { 1 }, new byte[] { 1 },
            new[] { 2, 3 },
            new[] { new[] { CatColor.Red }, new[] { CatColor.Blue } },
            new[] { 6, 6 },
            new[] { 8 }, new[] { CatColor.Red }, new[] { 2 }, new[] { 20 },
            2, 160, qCapBound: 8, trainsMax: 2,
            perfectMaxSwitches: par);
    }
}
