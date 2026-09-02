using System.IO;
using System.Linq;
using System.Text;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Tests.Domain;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CatMetro.Tests.Content
{
    [TestFixture]
    public sealed class FlipBudgetImportTests
    {
        [Test]
        public void EveryAuthoredParReachesTheRuntimeGraph()
        {
            var files = Directory.GetFiles(
                    Path.Combine(Fixtures.RepoRoot(), "content", "levels"), "L*.json")
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .ToArray();

            Assert.That(files, Is.Not.Empty,
                "the flip-budget importer proof must inspect the authored corpus artifact");
            foreach (var path in files)
            {
                var import = LevelImporter.Import(File.ReadAllBytes(path));
                Assert.That(import.Ok, Is.True, $"{Path.GetFileName(path)} must import: {import.Error}");
                Assert.That(import.Value.Graph.PerfectMaxSwitches,
                    Is.EqualTo(import.Value.Dto.Win.PerfectMaxSwitches),
                    $"{Path.GetFileName(path)} must not drop win.perfectMaxSwitches");
            }
        }

        [Test]
        public void MissingOptionalParImportsAsUngated()
        {
            var json = L001Json();
            ((JObject)json["win"]).Property("perfectMaxSwitches").Remove();

            var import = LevelImporter.Import(Encoding.UTF8.GetBytes(json.ToString(Formatting.None)));

            Assert.That(import.Ok, Is.True, import.Error?.ToString());
            Assert.That(import.Value.Dto.Win.PerfectMaxSwitches, Is.EqualTo(FlipBudget.Unbudgeted));
            Assert.That(import.Value.Graph.PerfectMaxSwitches, Is.EqualTo(FlipBudget.Unbudgeted));
        }

        [TestCase(-2)]
        [TestCase(201)]
        public void AuthoredParOutsideSchemaBoundsIsRejected(int authoredPar)
        {
            var json = L001Json();
            json["win"]["perfectMaxSwitches"] = authoredPar;

            var import = LevelImporter.Import(Encoding.UTF8.GetBytes(json.ToString(Formatting.None)));

            Assert.That(import.Ok, Is.False);
            Assert.That(import.Error.Kind, Is.EqualTo(ContentErrorKind.BoundViolation));
        }

        private static JObject L001Json()
        {
            string path = Path.Combine(Fixtures.RepoRoot(), "content", "levels", "L001.json");
            return JObject.Parse(File.ReadAllText(path));
        }
    }
}
