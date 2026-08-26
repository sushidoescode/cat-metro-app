using System.IO;
using System.Linq;
using NUnit.Framework;
using CatMetro.Content;
using CatMetro.Domain;
using CatMetro.Tests.Domain;

namespace CatMetro.Tests.Content
{
    // `win.perfectMaxSwitches` has been authored in all 17 levels since the corpus was written
    // and loaded into WinDto and stopped there — the Domain never saw it. These tests assert the
    // one wire that changed: DTO -> LevelGraph.PerfectMaxSwitches, on real content, not fixtures.
    [TestFixture]
    public class FlipBudgetImportTests
    {
        private static string[] LevelFiles() =>
            Directory.GetFiles(Path.Combine(Fixtures.RepoRoot(), "content", "levels"), "L*.json")
                     .OrderBy(p => p, System.StringComparer.Ordinal)
                     .ToArray();

        private static ImportedLevel Import(string path)
        {
            var r = LevelImporter.Import(File.ReadAllBytes(path));
            Assert.That(r.Ok, Is.True, $"{Path.GetFileName(path)} must import: {r.Error}");
            return r.Value;
        }

        [Test]
        public void EverySeventeenLevels_ReachTheDomainWithTheirAuthoredPar()
        {
            var files = LevelFiles();
            Assert.That(files.Length, Is.EqualTo(17), "the authored corpus");

            foreach (var f in files)
            {
                var lvl = Import(f);
                Assert.That(lvl.Graph.PerfectMaxSwitches, Is.EqualTo(lvl.Dto.Win.PerfectMaxSwitches),
                    $"{Path.GetFileName(f)}: par must survive the DTO -> LevelGraph hop");
                Assert.That(lvl.Graph.PerfectMaxSwitches, Is.GreaterThanOrEqualTo(0),
                    $"{Path.GetFileName(f)}: every authored level has a real budget");
            }
        }

        [Test]
        public void AuthoredParsSpanOneToFour_SoEveryTierIsReachable()
        {
            var pars = LevelFiles().Select(f => Import(f).Graph.PerfectMaxSwitches).Distinct().OrderBy(x => x).ToArray();
            Assert.That(pars, Is.EqualTo(new[] { 1, 2, 3, 4 }),
                "the whole authored range — WithinMax ceilings 2..8");
        }

        [Test]
        public void L001_ImportsParOne_AndItsGoldenLogScoresPerfect()
        {
            var lvl = Import(Path.Combine(Fixtures.RepoRoot(), "content", "levels", "L001.json"));
            Assert.That(lvl.Graph.PerfectMaxSwitches, Is.EqualTo(1));

            var end = Fixtures.RunThroughTick(lvl.Graph, (ulong)lvl.Dto.Seed, Fixtures.GoldenLog(), 60);
            Assert.That(end.Outcome.Kind, Is.EqualTo(OutcomeKind.Won));
            Assert.That(end.FlipStatus.Tier, Is.EqualTo(FlipTier.Perfect),
                "the authored par and the authored solution agree — par is achievable, not decorative");
            Assert.That(end.FlipStars, Is.EqualTo(3));
        }

        [Test]
        public void ImportedContent_StaysOnTheMisdeliveryPin()
        {
            // Turning mis-delivery on is a content decision this lane does not take.
            foreach (var f in LevelFiles())
                Assert.That(Import(f).Graph.Misdelivery, Is.EqualTo(MisdeliveryPolicy.Pinned),
                    $"{Path.GetFileName(f)}: importer must not silently change semantics");
        }
    }
}
