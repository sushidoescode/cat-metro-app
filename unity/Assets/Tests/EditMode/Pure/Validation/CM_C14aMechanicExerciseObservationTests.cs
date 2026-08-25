using System.Linq;
using CatMetro.Content;
using CatMetro.Content.Validation;
using CatMetro.Domain.Solver;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CatMetro.Tests.Validation
{
    [TestFixture]
    public class CM_C14aMechanicExerciseObservationTests
    {
        [Test]
        public void SecondSource_OptimalTraceObservesTwoAuthoredOrigins()
        {
            var imported = Import(SecondSourceLevel(useSecondSource: true));
            var solve = Solve(imported);

            var record = MechanicExercise.Observe(
                imported.Graph, (ulong)imported.Dto.Seed, solve.OptimalLog);
            var verdict = MechanicExercise.Liveness(imported.Dto, imported.Graph, solve);

            Assert.That(record.EmittingSourceNodes,
                Is.EquivalentTo(imported.Graph.SourceNodes));
            Assert.That(verdict.Code, Is.EqualTo(StageVerdictCode.Pass));
            Assert.That(verdict.Value,
                Does.Contain("newMechanic=second-source; exercised=true")
                    .And.Contain("sources=SRC,J1"));
        }

        [Test]
        public void SecondSource_DeclaredButOnlyOneOriginEmits_FailsBlocking()
        {
            var imported = Import(SecondSourceLevel(useSecondSource: false));
            var verdict = MechanicExercise.Liveness(imported.Dto, imported.Graph, Solve(imported));

            Assert.That(verdict.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(verdict.Blocks, Is.True);
            Assert.That(verdict.Detail, Does.Contain("second-source")
                .And.Contain("never exercises"));
            Assert.That(verdict.Value, Does.Contain("sources=SRC")
                .And.Contain("exercised=false"));
        }

        [Test]
        public void Wildcard_OptimalTraceCountsAnActualWildDelivery()
        {
            var imported = Import(WildcardLevel(deliverWild: true));
            var solve = Solve(imported);

            var record = MechanicExercise.Observe(
                imported.Graph, (ulong)imported.Dto.Seed, solve.OptimalLog);
            var verdict = MechanicExercise.Liveness(imported.Dto, imported.Graph, solve);

            Assert.That(record.WildDeliveries, Is.EqualTo(1));
            Assert.That(record.FirstWildDeliveryAtTick, Is.GreaterThanOrEqualTo(0));
            Assert.That(verdict.Code, Is.EqualTo(StageVerdictCode.Pass));
            Assert.That(verdict.Value,
                Does.Contain("newMechanic=wildcard; exercised=true")
                    .And.Contain("wildDeliveries=1@tick"));
        }

        [Test]
        public void Wildcard_EmittedAfterConcreteOptimalWin_FailsBlocking()
        {
            var imported = Import(WildcardLevel(deliverWild: false));
            var solve = Solve(imported);
            Assert.That(solve.OptimalLog.Entries, Is.Empty,
                "the concrete train wins before the authored Wild emits");

            var record = MechanicExercise.Observe(
                imported.Graph, (ulong)imported.Dto.Seed, solve.OptimalLog);
            var verdict = MechanicExercise.Liveness(imported.Dto, imported.Graph, solve);

            Assert.That(record.WildDeliveries, Is.EqualTo(0));
            Assert.That(verdict.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(verdict.Blocks, Is.True);
            Assert.That(verdict.Value,
                Does.Contain("newMechanic=wildcard; exercised=false")
                    .And.Contain("wildDeliveries=0"));
        }

        private static byte[] SecondSourceLevel(bool useSecondSource) => VFixtures.Level(o =>
        {
            o["meta"]["mechanics"] = new JArray("switch", "second-source");
            o["meta"]["newMechanic"] = "second-source";
            o["switches"][0]["initialRoute"] = 0;
            ((JArray)o["sources"]).Add(new JObject
            {
                ["nodeId"] = "J1",
                ["allowedColors"] = new JArray("red"),
            });
            var first = VFixtures.Wave(0, "red", useSecondSource ? 1 : 2, 4);
            var waves = new JArray(first);
            if (useSecondSource)
            {
                var second = VFixtures.Wave(1, "red", 1, 1);
                second["sourceNode"] = "J1";
                waves.Add(second);
            }
            o["waves"] = waves;
            o["win"]["deliveries"] = 2;
            o["win"]["timeLimitTicks"] = 50;
        });

        private static byte[] WildcardLevel(bool deliverWild) => VFixtures.Level(o =>
        {
            o["meta"]["mechanics"] = new JArray("switch", "wildcard");
            o["meta"]["newMechanic"] = "wildcard";
            o["switches"][0]["initialRoute"] = 0;
            o["sources"][0]["allowedColors"] = deliverWild
                ? new JArray("wild")
                : new JArray("red", "wild");
            o["waves"] = deliverWild
                ? new JArray(VFixtures.Wave(0, "wild", 1, 1))
                : new JArray(
                    VFixtures.Wave(0, "red", 1, 1),
                    VFixtures.Wave(40, "wild", 1, 1));
            o["win"]["deliveries"] = 1;
            o["win"]["timeLimitTicks"] = 100;
        });

        private static ImportedLevel Import(byte[] bytes)
        {
            var imported = LevelImporter.Import(bytes);
            Assert.That(imported.Ok, Is.True,
                imported.Ok ? "" : imported.Error.ToString());
            return imported.Value;
        }

        private static SolveResult Solve(ImportedLevel imported)
        {
            var solve = LevelSolver.Solve(
                imported.Graph, (ulong)imported.Dto.Seed, maxNodesExpanded: 200000);
            Assert.That(solve.Verdict, Is.EqualTo(SolveVerdict.Solved));
            return solve;
        }
    }
}
