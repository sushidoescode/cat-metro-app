using System;
using NUnit.Framework;
using CatMetro.Domain;

namespace CatMetro.Tests.Domain
{
    // Mis-delivery: what a station does with a cat it will not take.
    //
    // The default is unchanged and MUST stay unchanged — LevelSolver catches the NEW-Q4
    // NotSupportedException and counts it in PinnedPruned, so the pin is part of the solver's
    // search space, not just a gameplay rule. The first test here is a regression guard for that.
    //
    // MisdeliveryPolicy.RefuseAndSendHome is the recommended semantics (see
    // docs/design/MISDELIVERY.md) and is opt-in per level. It is the only candidate the current
    // Domain expresses without widening the digest: `Rejections` has been sitting in the frozen
    // byte layout since CM-C1, reserved for exactly this and pinned to 0.
    [TestFixture]
    public class MisdeliveryTests
    {
        [Test]
        public void Default_StillThrowsTheNEWQ4Pin_TheSolverDependsOnIt()
        {
            var ex = Assert.Throws<NotSupportedException>(() =>
                Fixtures.RunThroughTick(MismatchShape(MisdeliveryPolicy.Pinned), 3, new CommandLog(), 50));
            Assert.That(ex.Message, Does.Contain("NEW-Q4"),
                "LevelSolver greps this string into SolveResult.FirstPinMessage");
        }

        [Test]
        public void PinnedMessage_IsTheOneTheSimulationActuallyThrows()
        {
            var ex = Assert.Throws<NotSupportedException>(() =>
                Fixtures.RunThroughTick(MismatchShape(MisdeliveryPolicy.Pinned), 3, new CommandLog(), 50));
            Assert.That(ex.Message, Is.EqualTo(Misdelivery.PinnedMessage),
                "one constant, so the message cannot drift away from what tests assert");
        }

        [Test]
        public void RefuseAndSendHome_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                Fixtures.RunThroughTick(MismatchShape(MisdeliveryPolicy.RefuseAndSendHome), 3, new CommandLog(), 50));
        }

        [Test]
        public void RefuseAndSendHome_CountsARejection_AndDeliversNothing()
        {
            var end = Fixtures.RunThroughTick(
                MismatchShape(MisdeliveryPolicy.RefuseAndSendHome), 3, new CommandLog(), 50);

            Assert.That(end.Rejections, Is.EqualTo(1), "the field CM-C1 reserved finally moves");
            Assert.That(end.Deliveries, Is.EqualTo(0), "a refused cat is not a delivery");
        }

        [Test]
        public void RefuseAndSendHome_ClearsTheCatOffTheBoard()
        {
            var end = Fixtures.RunThroughTick(
                MismatchShape(MisdeliveryPolicy.RefuseAndSendHome), 3, new CommandLog(), 50);

            foreach (var t in end.Trains)
            {
                Assert.That(t.Id, Is.EqualTo(0), "the refused slot is zeroed, exactly like a delivery");
                Assert.That(t.State, Is.EqualTo(TrainState.None));
            }
        }

        [Test]
        public void RefuseAndSendHome_NeverFailsTheRunByItself()
        {
            // The cosy stance, and the same one the flip budget takes: a mistake costs rating,
            // not the run. This board can no longer be won (its only cat is gone), so it runs
            // out the clock — TimeOut, never a rejection-triggered failure.
            var end = Fixtures.RunThroughTick(
                MismatchShape(MisdeliveryPolicy.RefuseAndSendHome), 3, new CommandLog(), 200);

            Assert.That(end.Outcome.Kind, Is.EqualTo(OutcomeKind.Failed));
            Assert.That(end.Outcome.Reason, Is.EqualTo(FailReason.TimeOut),
                "there is no rejection failure reason, and this lane does not add one");
        }

        [Test]
        public void RefuseAndSendHome_BreaksPerfectFlow()
        {
            var end = Fixtures.RunThroughTick(
                MismatchShape(MisdeliveryPolicy.RefuseAndSendHome), 3, new CommandLog(), 200);

            Assert.That(end.Rejections, Is.GreaterThan(0));
            Assert.That(FlipBudget.IsPerfectFlow(1, end.SwitchesUsed, end.Rejections, end.Overloads, won: true),
                Is.False, "product_spec.md:238 gates Perfect Flow on zero rejections");
        }

        [Test]
        public void Policy_DoesNotChangeDigestLength()
        {
            int pinned = SimulationState.CreateInitial(MismatchShape(MisdeliveryPolicy.Pinned), 3).DigestLength();
            int refused = SimulationState.CreateInitial(MismatchShape(MisdeliveryPolicy.RefuseAndSendHome), 3).DigestLength();
            Assert.That(refused, Is.EqualTo(pinned),
                "Rejections was always in the layout — nothing widens");
        }

        [Test]
        public void IsSurvivable_NamesTheTwoPolicies()
        {
            Assert.That(Misdelivery.IsSurvivable(MisdeliveryPolicy.Pinned), Is.False);
            Assert.That(Misdelivery.IsSurvivable(MisdeliveryPolicy.RefuseAndSendHome), Is.True);
        }

        [Test]
        public void MatchingArrivals_AreUnaffectedByThePolicy()
        {
            // Guard against a policy branch that accidentally changes the happy path.
            var a = Fixtures.RunThroughTick(Fixtures.L001Shape(), Fixtures.L001Seed, Fixtures.GoldenLog(), 60);
            var b = Fixtures.RunThroughTick(MatchingShape(MisdeliveryPolicy.RefuseAndSendHome), 3, new CommandLog(), 50);

            Assert.That(a.Rejections, Is.EqualTo(0), "the golden path never rejects");
            Assert.That(b.Rejections, Is.EqualTo(0));
            Assert.That(b.Deliveries, Is.EqualTo(1), "a matching cat is delivered under either policy");
        }

        // FX-MM with a policy: one red cat, one station that only takes blue.
        private static LevelGraph MismatchShape(MisdeliveryPolicy policy) => new LevelGraph(
            "FX-MM", 2,
            new[] { 8, 8 },
            new[] { 0 }, new[] { 1 }, new[] { 5 },
            new[] { 0 },
            new int[0][], new int[0], new byte[0],
            new[] { 1 }, new[] { new[] { CatColor.Blue } }, new[] { 6 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            1, 100, qCapBound: 8, trainsMax: 1,
            perfectMaxSwitches: FlipBudget.Unbudgeted, misdelivery: policy);

        // The same board with the station accepting the cat it is sent.
        private static LevelGraph MatchingShape(MisdeliveryPolicy policy) => new LevelGraph(
            "FX-MATCH", 2,
            new[] { 8, 8 },
            new[] { 0 }, new[] { 1 }, new[] { 5 },
            new[] { 0 },
            new int[0][], new int[0], new byte[0],
            new[] { 1 }, new[] { new[] { CatColor.Red } }, new[] { 6 },
            new[] { 0 }, new[] { CatColor.Red }, new[] { 1 }, new[] { 1 },
            1, 100, qCapBound: 8, trainsMax: 1,
            perfectMaxSwitches: FlipBudget.Unbudgeted, misdelivery: policy);
    }
}
