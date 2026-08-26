using System;
using CatMetro.Domain;
using NUnit.Framework;

namespace CatMetro.Tests.Domain
{
    [TestFixture]
    public sealed class FlipBudgetTests
    {
        [Test]
        public void Evaluate_AssignsTheThreeRatingBandsAtTheirBoundaries()
        {
            Assert.That(FlipBudget.Evaluate(3, 0).Rating, Is.EqualTo(FlipRating.Perfect));
            Assert.That(FlipBudget.Evaluate(3, 3).Rating, Is.EqualTo(FlipRating.Perfect));
            Assert.That(FlipBudget.Evaluate(3, 4).Rating, Is.EqualTo(FlipRating.Efficient));
            Assert.That(FlipBudget.Evaluate(3, 6).Rating, Is.EqualTo(FlipRating.Efficient));
            Assert.That(FlipBudget.Evaluate(3, 7).Rating, Is.EqualTo(FlipRating.Solved));
        }

        [Test]
        public void Evaluate_ParZeroRetainsARealTwoStarBand()
        {
            Assert.That(FlipBudget.Evaluate(0, 0).RatingStars, Is.EqualTo(3));
            Assert.That(FlipBudget.Evaluate(0, 1).RatingStars, Is.EqualTo(2));
            Assert.That(FlipBudget.Evaluate(0, 2).RatingStars, Is.EqualTo(1));
        }

        [Test]
        public void Evaluate_UnbudgetedDoesNotInventARating()
        {
            var status = FlipBudget.Evaluate(FlipBudget.Unbudgeted, 40);

            Assert.That(status.IsBudgeted, Is.False);
            Assert.That(status.Rating, Is.EqualTo(FlipRating.Ungated));
            Assert.That(status.RatingStars, Is.Zero);
            Assert.That(status.RemainingToPerfect, Is.Zero);
            Assert.That(status.IsOverPerfect, Is.False);
        }

        [Test]
        public void Evaluate_ReportsTheExactOverspendForTheHud()
        {
            var status = FlipBudget.Evaluate(4, 7);

            Assert.That(status.PerfectMaxSwitches, Is.EqualTo(4));
            Assert.That(status.Used, Is.EqualTo(7));
            Assert.That(status.TwoStarMaxSwitches, Is.EqualTo(8));
            Assert.That(status.RemainingToPerfect, Is.EqualTo(-3));
            Assert.That(status.IsOverPerfect, Is.True);
        }

        [Test]
        public void Evaluate_RejectsANegativeUsedCount()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FlipBudget.Evaluate(2, -1));
        }

        [Test]
        public void Evaluate_RejectsAParBelowTheUngatedSentinel()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FlipBudget.Evaluate(-2, 0));
        }

        [Test]
        public void DefaultGraph_RemainsUngatedForExistingDirectCallers()
        {
            Assert.That(Fixtures.L001Shape().PerfectMaxSwitches, Is.EqualTo(FlipBudget.Unbudgeted));
        }

        [Test]
        public void Graph_RejectsAParBelowTheUngatedSentinel()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WithPar(Fixtures.L001Shape(), -2));
        }

        [Test]
        public void BudgetConfiguration_DoesNotChangeDigestBytes()
        {
            var plain = Fixtures.RunThroughTick(
                Fixtures.L001Shape(), Fixtures.L001Seed, Fixtures.GoldenLog(), 60);
            var budgeted = Fixtures.RunThroughTick(
                WithPar(Fixtures.L001Shape(), 1), Fixtures.L001Seed, Fixtures.GoldenLog(), 60);

            var plainDigest = new byte[plain.DigestLength()];
            var budgetedDigest = new byte[budgeted.DigestLength()];
            plain.WriteDigest(plainDigest);
            budgeted.WriteDigest(budgetedDigest);

            Assert.That(budgetedDigest, Is.EqualTo(plainDigest));
        }

        [Test]
        public void GoingOverBudgetChangesRatingButDoesNotChangeAWin()
        {
            var log = new CommandLog();
            log.Append(new ToggleSwitchCommand(0, 12));
            log.Append(new ToggleSwitchCommand(0, 13));
            log.Append(new ToggleSwitchCommand(0, 14));

            var end = Fixtures.RunThroughTick(
                WithPar(Fixtures.L001Shape(), 1), Fixtures.L001Seed, log, 60);

            Assert.That(end.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            Assert.That(end.FlipStatus.Used, Is.EqualTo(3));
            Assert.That(end.FlipStatus.Rating, Is.EqualTo(FlipRating.Solved));
        }

        private static LevelGraph WithPar(LevelGraph graph, int par) => new LevelGraph(
            graph.LevelId,
            graph.NodeCount,
            graph.NodeQueueCapacity,
            graph.EdgeFrom,
            graph.EdgeTo,
            graph.EdgeTravelTicks,
            new[] { graph.SourceNode },
            graph.SwitchRoutes,
            graph.SwitchNode,
            graph.SwitchInitialRoute,
            graph.StationNode,
            graph.StationAccepts,
            graph.StationCapacity,
            graph.WaveTick,
            graph.WaveColor,
            graph.WaveCount,
            graph.WaveSpacingTicks,
            graph.WinDeliveries,
            graph.TimeLimitTicks,
            graph.QCapBound,
            graph.TrainsMax,
            perfectMaxSwitches: par);
    }
}
