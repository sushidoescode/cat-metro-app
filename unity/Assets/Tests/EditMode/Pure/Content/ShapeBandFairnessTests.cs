using System.IO;
using System.Linq;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Domain.Solver;
using CatMetro.Tests.Domain;
using NUnit.Framework;

namespace CatMetro.Tests.Content
{
    public sealed class ShapeBandFairnessTests
    {
        [TestCase("L009")]
        [TestCase("L010")]
        [TestCase("L011")]
        [TestCase("L015")]
        public void ThreeWayLever_VisitsPlatformsFromLeftToRight(string id)
        {
            var level = Read(id);
            var nodes = level.Dto.Nodes.ToArray().ToDictionary(n => n.Id);
            var edges = level.Dto.Edges.ToArray().ToDictionary(e => e.Id);
            foreach (var lever in level.Dto.Switches.ToArray())
            {
                var x = lever.Routes.ToArray().Select(route => nodes[edges[route].To].X).ToArray();
                Assert.That(x, Is.Ordered.Ascending,
                    id + ": a tap must visit the centre before jumping to the right platform");
            }
        }

        [Test]
        public void Carousel_WavesVisitTheNextPlatformInSpatialOrder()
        {
            var level = Read("L010");
            Assert.That(level.Dto.Waves.ToArray().OrderBy(w => w.Tick).Select(w => w.Shape),
                Is.EqualTo(new[] { "round", "square", "triangle" }),
                "the first cat uses the set route; each later badge advances one stop");
        }

        [TestCase("L009")]
        [TestCase("L010")]
        [TestCase("L011")]
        [TestCase("L012")]
        public void ShapeLesson_AllowsAtLeastTwiceTheSolverCompletionTime(string id)
        {
            var level = Read(id);
            var solve = LevelSolver.Solve(level.Graph, (ulong)level.Dto.Seed, 2_000_000);
            Assert.That(solve.Verdict, Is.EqualTo(SolveVerdict.Solved), id);
            Assert.That(level.Graph.TimeLimitTicks, Is.GreaterThanOrEqualTo(2 * solve.CompletionTicks),
                id + ": leave time to recover from a wrong-shape refusal without a visible clock");
        }

        [Test]
        public void L004_EntryLeverCanStillBeSetAfterEighteenTicks()
        {
            var level = Read("L004");
            var log = new CommandLog();
            log.Append(new ToggleSwitchCommand(0, 18));
            log.Append(new ToggleSwitchCommand(1, 46));
            var result = LevelSolver.EvaluateLog(level.Graph, (ulong)level.Dto.Seed, log);
            Assert.That(result.Verdict, Is.EqualTo(SolveVerdict.Solved),
                "the opening decision must remain recoverable for four ticks longer");
        }

        private static ImportedLevel Read(string id)
        {
            var result = LevelImporter.Import(File.ReadAllBytes(
                Path.Combine(Fixtures.RepoRoot(), "content", "levels", id + ".json")));
            Assert.That(result.Ok, Is.True, result.Error?.ToString());
            return result.Value;
        }
    }
}
