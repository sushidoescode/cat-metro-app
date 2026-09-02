using System.IO;
using System.Linq;
using CatMetro.Content;
using CatMetro.Content.Validation;
using CatMetro.Domain.Solver;
using NUnit.Framework;

namespace CatMetro.Tests.Corpus
{
    [TestFixture]
    public class CampaignArtifactTests
    {
        [Test]
        public void CampaignIsContiguousAndMirroredByteForByte()
        {
            var authoritative = BandFixtures.CampaignFiles();
            Assert.That(authoritative, Has.Length.EqualTo(60));

            var expectedIds = Enumerable.Range(1, authoritative.Length)
                .Select(number => "L" + number.ToString("000"))
                .ToArray();
            var actualIds = authoritative.Select(Path.GetFileNameWithoutExtension).ToArray();
            Assert.That(actualIds, Is.EqualTo(expectedIds));

            string stagedDirectory = Path.Combine(BandFixtures.RepoRoot(), "unity", "Assets",
                "StreamingAssets", "content", "levels");
            var staged = Directory.GetFiles(stagedDirectory, "L*.json")
                .OrderBy(path => Path.GetFileName(path), System.StringComparer.Ordinal)
                .ToArray();
            Assert.That(staged.Select(Path.GetFileName), Is.EqualTo(authoritative.Select(Path.GetFileName)));
            for (int i = 0; i < authoritative.Length; i++)
                CollectionAssert.AreEqual(File.ReadAllBytes(authoritative[i]), File.ReadAllBytes(staged[i]),
                    Path.GetFileName(authoritative[i]) + " differs from its shipped mirror");
        }

        [Test]
        public void EveryArtifactImportsToItsFilenameAndHasAUniqueTeachingGoal()
        {
            var imported = BandFixtures.CampaignFiles().Select(path =>
            {
                var result = LevelImporter.Import(File.ReadAllBytes(path));
                Assert.That(result.Ok, Is.True, Path.GetFileName(path) + ": " + result.Error);
                Assert.That(result.Value.Dto.Id, Is.EqualTo(Path.GetFileNameWithoutExtension(path)));
                Assert.That(result.Value.Dto.Meta.AuthoredBy, Is.EqualTo("llm+validator"), result.Value.Dto.Id);
                Assert.That(result.Value.Dto.Meta.TeachingGoal, Is.Not.Null.And.Not.Empty, result.Value.Dto.Id);
                return result.Value;
            }).ToArray();

            Assert.That(imported.Select(level => level.Dto.Meta.TeachingGoal), Is.Unique);
            Assert.That(imported.Select(level => level.Dto.Meta.DifficultyTarget), Is.Ordered.Ascending);
        }
    }

    [TestFixture]
    [Timeout(900000)]
    public class FullCampaignGateTests
    {
        [Test]
        public void CanonicalCampaignHasNoBlockingFailureAndExactSolvesEveryLevel()
        {
            var report = BandFixtures.FullCampaignReport();
            string blockers = string.Join("\n",
                report.Levels.SelectMany(level => level.Verdicts)
                    .Where(verdict => verdict.Blocks)
                    .Select(verdict => verdict.Stage + ": " + verdict.Detail)
                    .Concat(report.CampaignVerdicts.Where(verdict => verdict.Blocks)
                        .Select(verdict => "campaign: " + verdict.Detail)));

            Assert.That(report.Levels, Has.Count.EqualTo(60));
            Assert.That(report.ExitFailure, Is.False, blockers);
            Assert.That(report.Levels.All(level => level.Solve != null), Is.True);
            foreach (var level in report.Levels)
            {
                Assert.That(level.Solve.Verdict, Is.EqualTo(SolveVerdict.Solved), level.LevelId);
                Assert.That(level.Solve.BeamWidthUsed, Is.Zero, level.LevelId + " must use exact BFS");
                Assert.That(level.Verdicts.Any(verdict => verdict.Blocks), Is.False, level.LevelId);
            }
        }

        [Test]
        public void CampaignWideInvariantRowsPassAgainstTheMaterializedArtifact()
        {
            var report = BandFixtures.FullCampaignReport();
            foreach (string tag in new[]
                     { "tag=CM-R09.1", "tag=CM-R06.2", "tag=CM-R09.3", "tag=CM-LADDER-solve-proof" })
            {
                var verdict = report.CampaignVerdicts.Single(row => row.Value == tag);
                Assert.That(verdict.Code, Is.EqualTo(StageVerdictCode.Pass), verdict.Detail);
                Assert.That(verdict.Blocks, Is.False, verdict.Detail);
            }
        }

        [Test]
        public void EveryDeclaredMechanicIsExercisedByTheExactWinningReplay()
        {
            var report = BandFixtures.FullCampaignReport();
            var rows = report.CampaignVerdicts.Where(row =>
                row.Value.StartsWith("tag=CM-LADDER-declared-mechanics:",
                    System.StringComparison.Ordinal)).ToArray();

            Assert.That(rows, Has.Length.EqualTo(60));
            Assert.That(rows.All(row => row.Code == StageVerdictCode.Pass && !row.Blocks), Is.True,
                string.Join("\n", rows.Where(row => row.Code != StageVerdictCode.Pass || row.Blocks)
                    .Select(row => row.Detail)));
        }
    }
}
