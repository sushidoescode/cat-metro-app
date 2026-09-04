using System.IO;
using System.Linq;
using CatMetro.Content;
using CatMetro.Content.Validation;
using CatMetro.Domain.Solver;
using CatMetro.Tests.Domain;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CatMetro.Tests.Validation
{
    [TestFixture]
    public class MechanicExerciseTests
    {
        private static byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(
            Fixtures.RepoRoot(), "tests", "validation", "fixtures", "dead-mechanic", name));

        private static ImportedLevel Import(byte[] bytes)
        {
            var imported = LevelImporter.Import(bytes);
            Assert.That(imported.Ok, Is.True, imported.Ok ? "" : imported.Error.ToString());
            return imported.Value;
        }

        private static SolveResult Solve(ImportedLevel imported) => LevelSolver.Solve(
            imported.Graph, (ulong)imported.Dto.Seed, maxNodesExpanded: 200000);

        [Test]
        public void DispositionTable_EqualsTheSchemaEnum_AndEveryMechanicIsObservable()
        {
            var schema = JObject.Parse(System.Text.Encoding.UTF8.GetString(VFixtures.SchemaBytes()));
            var values = ((JArray)schema["properties"]["meta"]["properties"]
                    ["mechanics"]["items"]["enum"])
                .Select(t => (string)t).ToArray();
            Assert.That(MechanicExercise.Dispositions.Keys, Is.EquivalentTo(values));
            Assert.That(MechanicExercise.Dispositions.Values,
                Is.All.EqualTo(MechanicDisposition.Observable),
                "wired runtime fields must never regress to a pinned/unobservable pass");
        }

        [Test]
        public void Observer_ReplayingTheSameWinningArtifact_IsDeterministic()
        {
            var imported = Import(VFixtures.L001Bytes());
            var solve = Solve(imported);
            Assert.That(solve.Verdict, Is.EqualTo(SolveVerdict.Solved));

            var a = MechanicExercise.Observe(
                imported.Graph, (ulong)imported.Dto.Seed, solve.OptimalLog);
            var b = MechanicExercise.Observe(
                imported.Graph, (ulong)imported.Dto.Seed, solve.OptimalLog);

            Assert.That(a.RouteChanged, Is.EqualTo(b.RouteChanged));
            Assert.That(a.RouteChangedAtTick, Is.EqualTo(b.RouteChangedAtTick));
            Assert.That(a.SwitchesUsed, Is.EqualTo(b.SwitchesUsed));
            Assert.That(a.MaxActiveTrains, Is.EqualTo(b.MaxActiveTrains));
            Assert.That(a.MaxQueued, Is.EqualTo(b.MaxQueued));
            Assert.That(a.MaxQueuedAtTick, Is.EqualTo(b.MaxQueuedAtTick));
        }

        [Test]
        public void AuthoredL001_WinningReplayExercisesItsDeclaredSwitch()
        {
            var imported = Import(VFixtures.L001Bytes());
            var verdict = MechanicExercise.Liveness(imported.Dto, imported.Graph, Solve(imported));
            Assert.That(verdict.Code, Is.EqualTo(StageVerdictCode.Pass));
            Assert.That(verdict.Value,
                Does.Contain("newMechanic=switch; exercised=true")
                    .And.Contain("toggles=1,routeChangedAtTick="));
        }

        [Test]
        public void QueueWitness_Passes_WhileItsNoQueueMutationFailsBlocking()
        {
            var live = Import(Fixture("L004-live-queue.json"));
            var dead = Import(Fixture("L004-dead-queue.json"));
            var liveVerdict = MechanicExercise.Liveness(live.Dto, live.Graph, Solve(live));
            var deadVerdict = MechanicExercise.Liveness(dead.Dto, dead.Graph, Solve(dead));

            Assert.That(liveVerdict.Code, Is.EqualTo(StageVerdictCode.Pass));
            Assert.That(liveVerdict.Value, Does.Contain("maxQueued=1@tick 8"));
            Assert.That(deadVerdict.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(deadVerdict.Blocks, Is.True);
            Assert.That(deadVerdict.Value, Does.Contain("maxQueued=0@tick -1"));
        }

        [Test]
        public void EveryDeclaredMechanic_IsMeasured_NotOnlyTheIntroductionLabel()
        {
            var live = Import(Fixture("L004-live-queue.json"));
            var dead = Import(Fixture("L004-dead-queue.json"));
            var liveVerdict = MechanicExercise.DeclaredMechanicsLiveness(
                live.Dto, live.Graph, Solve(live));
            var deadVerdict = MechanicExercise.DeclaredMechanicsLiveness(
                dead.Dto, dead.Graph, Solve(dead));

            Assert.That(liveVerdict.Code, Is.EqualTo(StageVerdictCode.Pass));
            Assert.That(liveVerdict.Value,
                Does.StartWith("tag=CM-LADDER-declared-mechanics:")
                    .And.Contain("queue=true(maxQueued=1@tick 8)"));
            Assert.That(deadVerdict.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(deadVerdict.Blocks, Is.True);
            Assert.That(deadVerdict.Value, Does.Contain("queue=false(maxQueued=0@tick -1)"));
        }

        [Test]
        public void DeclaredGateWithoutAClosedWaitAndTraversal_FailsBlocking()
        {
            var bytes = VFixtures.Level(level =>
            {
                level["meta"]["mechanics"] = new JArray("gate");
                level["meta"]["newMechanic"] = "gate";
                level["switches"][0]["initialRoute"] = 0;
            });
            var imported = Import(bytes);
            var verdict = MechanicExercise.Liveness(imported.Dto, imported.Graph, Solve(imported));
            Assert.That(verdict.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(verdict.Blocks, Is.True);
            Assert.That(verdict.Value, Does.Contain("closedWaitThenTraverse=false"));
        }

        [Test]
        public void NullNewMechanic_IsExplicitlySkipped()
        {
            var bytes = VFixtures.Level(level =>
            {
                level["meta"]["newMechanic"] = null;
                level["switches"][0]["initialRoute"] = 0;
            });
            var imported = Import(bytes);
            var verdict = MechanicExercise.Liveness(imported.Dto, imported.Graph, Solve(imported));
            Assert.That(verdict.Code, Is.EqualTo(StageVerdictCode.Skipped));
            Assert.That(verdict.Blocks, Is.False);
            Assert.That(verdict.Detail, Is.EqualTo("SKIPPED(no declared newMechanic)"));
        }

        [Test]
        public void MissingWinningArtifact_IsExplicitlySkipped()
        {
            var imported = Import(VFixtures.BeamMissLevel());
            var solve = Solve(imported);
            Assert.That(solve.Verdict, Is.Not.EqualTo(SolveVerdict.Solved));
            var verdict = MechanicExercise.Liveness(imported.Dto, imported.Graph, solve);
            Assert.That(verdict.Code, Is.EqualTo(StageVerdictCode.Skipped));
            Assert.That(verdict.Blocks, Is.False);
            Assert.That(verdict.Detail, Is.EqualTo("SKIPPED(no winning log)"));
        }
    }
}
