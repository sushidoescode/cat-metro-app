using System;
using System.Linq;
using NUnit.Framework;
using CatMetro.Content.Validation;

namespace CatMetro.Tests.Validation
{
    // Criterion 1: the stage inventory is exactly 11 and matches product_spec.md:637-647 —
    // names AND ordinals, the CM-C1 FailReason shape. Plus the verdict model's exit rule and the
    // config surface (criterion 13's substrate).
    [TestFixture]
    public class StageModelTests
    {
        [Test]
        public void StageEnum_IsExactlyTheElevenAuthoredStagesInOrder()
        {
            var expected = new[]
            {
                ("Schema", 1), ("StaticAnalysis", 2), ("LowerBoundFeasibility", 3), ("Solver", 4),
                ("TrivialityReject", 5), ("BrittlenessAccessibility", 6), ("StarCheck", 7),
                ("DifficultyCheck", 8), ("NoveltyCheck", 9), ("Staleness", 10), ("HumanPlaytest", 11),
            };
            var actual = Enum.GetValues(typeof(Stage)).Cast<Stage>()
                .OrderBy(s => (int)s)
                .Select(s => (s.ToString(), (int)s))
                .ToArray();
            Assert.That(actual, Is.EqualTo(expected),
                "adding, removing, renaming or reordering a stage must fail this test");
        }

        [Test]
        public void VerdictModel_OnlyBlockingFailContributesToExit()
        {
            Assert.That(StageVerdict.Fail(Stage.Schema, "x").Blocks, Is.True);
            Assert.That(StageVerdict.NonBlockingFail(Stage.Staleness, "x").Blocks, Is.False);
            Assert.That(StageVerdict.Pass(Stage.Schema).Blocks, Is.False);
            Assert.That(StageVerdict.Warn(Stage.StaticAnalysis, "x").Blocks, Is.False);
            Assert.That(StageVerdict.Unconfigured(Stage.LowerBoundFeasibility, "lowerBoundSlack", "44").Blocks, Is.False);
            Assert.That(StageVerdict.Skipped(Stage.NoveltyCheck, "non-campaign").Blocks, Is.False);
            Assert.That(StageVerdict.Pinned(Stage.StarCheck, "NEW-Q5").Blocks, Is.False);
        }

        [Test]
        public void VerdictMarkers_CarryTheContractSpelling()
        {
            Assert.That(StageVerdict.Unconfigured(Stage.LowerBoundFeasibility, "lowerBoundSlack", "44").Detail,
                Is.EqualTo("UNCONFIGURED(lowerBoundSlack)"));
            Assert.That(StageVerdict.Skipped(Stage.NoveltyCheck, "non-campaign").Detail,
                Is.EqualTo("SKIPPED(non-campaign)"));
            Assert.That(StageVerdict.Pinned(Stage.StarCheck, "NEW-Q5").Detail,
                Is.EqualTo("PINNED(NEW-Q5)"));
        }

        [Test]
        public void Config_FixtureWithAllRows_ParsesEveryRow()
        {
            var c = VFixtures.FullConfig();
            Assert.That(c.JitterSampleCount, Is.EqualTo(20));
            Assert.That(c.LowerBoundSlack, Is.EqualTo(2));
            Assert.That(c.StarBandSlack, Is.EqualTo(50));
            Assert.That(c.NoveltyMinDistance, Is.EqualTo(5.0).Within(1e-12));
            Assert.That(c.AxisBBandCaps, Is.Not.Null);
            Assert.That(c.AxisBBandCaps["onboarding"].MaxComplexity, Is.EqualTo(20));
        }

        [Test]
        public void Config_ShippedFile_HasJitterRowAndNoQRRows()
        {
            // Stop condition 3: the four Q-R rows are DELIBERATELY absent from the shipped file.
            var r = ValidatorConfig.Parse(VFixtures.ShippedConfigBytes());
            Assert.That(r.Ok, Is.True, $"shipped config must parse: {r.Error}");
            Assert.That(r.Value.JitterSampleCount, Is.EqualTo(20), "A-C5-2's test-design number");
            Assert.That(r.Value.LowerBoundSlack, Is.Null);
            Assert.That(r.Value.StarBandSlack, Is.Null);
            Assert.That(r.Value.NoveltyMinDistance, Is.Null);
            Assert.That(r.Value.AxisBBandCaps, Is.Null);
        }
    }
}
