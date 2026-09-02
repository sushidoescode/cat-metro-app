using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Domain.Solver;
using CatMetro.Tests.Domain;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CatMetro.Tests.Content
{
    [TestFixture]
    public sealed class PerfectMaxSwitchesCorpusTests
    {
        private static IEnumerable<string> LevelFiles() =>
            Directory.GetFiles(Path.Combine(Fixtures.RepoRoot(), "content", "levels"), "L*.json")
                .OrderBy(path => path, System.StringComparer.Ordinal);

        [TestCaseSource(nameof(LevelFiles))]
        public void AuthoredPar_HasASolverProvedWinningRun(string path)
        {
            var import = LevelImporter.Import(File.ReadAllBytes(path));
            Assert.That(import.Ok, Is.True, $"{Path.GetFileName(path)} must import: {import.Error}");

            AssertAuthoredParIsAchievable(import.Value);
        }

        [Test]
        public void OmittedPar_RemainsUngatedInsteadOfBecomingAnImpossibleTarget()
        {
            string path = Path.Combine(Fixtures.RepoRoot(), "content", "levels", "L001.json");
            var json = JObject.Parse(File.ReadAllText(path));
            ((JObject)json["win"]).Property("perfectMaxSwitches")?.Remove();
            var import = LevelImporter.Import(Encoding.UTF8.GetBytes(json.ToString(Formatting.None)));
            Assert.That(import.Ok, Is.True, import.Error?.ToString());

            Assert.DoesNotThrow(() => AssertAuthoredParIsAchievable(import.Value));
        }

        private static void AssertAuthoredParIsAchievable(ImportedLevel level)
        {
            int authoredPar = level.Dto.Win.PerfectMaxSwitches;
            if (authoredPar == FlipBudget.Unbudgeted)
            {
                Assert.That(level.Graph.PerfectMaxSwitches, Is.EqualTo(FlipBudget.Unbudgeted),
                    $"{level.Dto.Id}: an omitted par must remain explicitly ungated");
                return;
            }

            var solve = LevelSolver.Solve(level.Graph, (ulong)level.Dto.Seed, maxNodesExpanded: 2_000_000);

            Assert.That(solve.Verdict, Is.EqualTo(SolveVerdict.Solved),
                $"{level.Dto.Id}: par cannot gate rating without a solver-proved win");
            Assert.That(solve.SwitchesUsed, Is.LessThanOrEqualTo(authoredPar),
                $"{level.Dto.Id}: solver needs {solve.SwitchesUsed} flips but authored par is " +
                $"{authoredPar}; leave this level ungated");
        }
    }
}
