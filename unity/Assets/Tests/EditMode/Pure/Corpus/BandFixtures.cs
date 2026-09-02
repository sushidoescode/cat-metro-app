using System;
using System.IO;
using System.Linq;
using CatMetro.Content;
using CatMetro.Content.Validation;
using NUnit.Framework;

namespace CatMetro.Tests.Corpus
{
    public static class BandFixtures
    {
        public static readonly string[] FirstTwentyIds = Enumerable.Range(1, 20)
            .Select(number => "L" + number.ToString("000"))
            .ToArray();

        private static readonly Lazy<CorpusReport> CampaignReport =
            new Lazy<CorpusReport>(BuildCampaignReport);

        public static string RepoRoot() => CatMetro.Tests.Domain.Fixtures.RepoRoot();

        public static byte[] Bytes(string id) =>
            File.ReadAllBytes(Path.Combine(RepoRoot(), "content", "levels", id + ".json"));

        public static string[] CampaignFiles() =>
            Directory.GetFiles(Path.Combine(RepoRoot(), "content", "levels"), "L*.json")
                .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
                .ToArray();

        public static ImportedLevel Import(string id)
        {
            var result = LevelImporter.Import(Bytes(id));
            Assert.That(result.Ok, Is.True, $"{id} must import: {result.Error}");
            return result.Value;
        }

        public static CorpusReport FullCampaignReport() => CampaignReport.Value;

        private static CorpusReport BuildCampaignReport()
        {
            var config = ValidatorConfig.Parse(File.ReadAllBytes(
                Path.Combine(RepoRoot(), "config", "validator_thresholds.json")));
            Assert.That(config.Ok, Is.True, config.Error?.ToString());
            var members = CampaignFiles()
                .Select(path => new CorpusMember(
                    "content/levels/" + Path.GetFileName(path), File.ReadAllBytes(path), true))
                .ToArray();
            return CorpusValidator.Validate(new ValidationRequest(
                File.ReadAllBytes(Path.Combine(RepoRoot(), "docs", "plan", "data", "level_schema.json")),
                config.Value,
                "2026-09-01T00:00:00+00:00",
                members));
        }
    }
}
