using System;
using System.IO;
using System.Linq;
using CatMetro.Content;
using CatMetro.Content.Validation;
using CatMetro.Domain.Solver;
using NUnit.Framework;

namespace CatMetro.Tests.CM_C14a
{
    // CM-C14a acceptance: the locked L018 fixture is read directly with this file's own helper.
    // Tests may use the filesystem; the engine-free Content assembly may not.
    [TestFixture]
    public class L018MechanicsEnablementAcceptanceTests
    {
        [Test]
        public void LockedL018_Imports_ValidatesThroughAllStages_AndSolves()
        {
            var bytes = LockedL018Bytes();

            var imported = LevelImporter.Import(bytes);
            Assert.That(imported.Ok, Is.True,
                "L018 import must succeed; actual=" + (imported.Ok ? "OK" : imported.Error.ToString()));
            Assert.That(imported.Value.Dto.Id, Is.EqualTo("L018"));

            var report = CorpusValidator.Validate(new ValidationRequest(
                ReadRepoBytes("docs", "plan", "data", "level_schema.json"),
                ParseShippedConfig(),
                "2026-08-12T00:00:00-07:00",
                new[]
                {
                    new CorpusMember("content/levels/L001.json",
                        ReadRepoBytes("content", "levels", "L001.json"), true),
                    new CorpusMember("content/levels/L004.json",
                        ReadRepoBytes("content", "levels", "L004.json"), true),
                    new CorpusMember("tests/fixtures/corpus/l018-mechanics.json", bytes, true),
                },
                maxNodesExpanded: SolverBounds.MAX_NODES_EXPANDED));

            var level = report.Levels.Single(l => l.LevelId == "L018");
            Assert.That(level.Verdicts.Count, Is.EqualTo(11), "all validator stages run");
            Assert.That(level.Verdicts.Select(v => v.Stage), Is.EqualTo(
                Enumerable.Range(1, 11).Select(i => (Stage)i)), "all 11 stages, in order");
            Assert.That(level.Verdicts.Any(v => v.Code == StageVerdictCode.Skipped
                                               && v.Detail.Contains("import failed")), Is.False,
                "no post-import stage is skipped");
            Assert.That(level.Verdicts.Any(v => v.Blocks), Is.False,
                "UNCONFIGURED/STALE/PENDING may be truthful, but no L018 stage may block\n"
                + "optimalLog=" + string.Join(",", level.Solve == null
                    ? Array.Empty<string>()
                    : level.Solve.OptimalLog.Entries.Select(e => e.SwitchId + "@" + e.Tick))
                + "\n" + report.ToTable());
            Assert.That(level.Solve, Is.Not.Null, "the solver stage ran");
            Assert.That(level.Solve.Verdict, Is.EqualTo(SolveVerdict.Solved),
                "locked L018 must solve exactly");
            Assert.That(level.Solve.BeamWidthUsed, Is.EqualTo(0), "one-switch L018 uses exact BFS");

            var liveness = report.CampaignVerdicts.Single(v =>
                v.Value.StartsWith("tag=CM-R06.2-liveness:L018", StringComparison.Ordinal));
            Assert.That(liveness.Code, Is.EqualTo(StageVerdictCode.Pass), liveness.Detail);
            Assert.That(liveness.Value, Does.Contain("SA").And.Contain("SB"),
                "second-source evidence names both authored sources");
            Assert.That(report.ExitFailure, Is.False, "the complete campaign-context run is green");
        }

        private static byte[] LockedL018Bytes()
        {
            return ReadRepoBytes("tests", "fixtures", "corpus", "l018-mechanics.json");
        }

        private static ValidatorConfig ParseShippedConfig()
        {
            var parsed = ValidatorConfig.Parse(
                ReadRepoBytes("config", "validator_thresholds.json"));
            Assert.That(parsed.Ok, Is.True,
                "shipped validator config must parse: " + (parsed.Ok ? "OK" : parsed.Error.ToString()));
            return parsed.Value;
        }

        private static byte[] ReadRepoBytes(params string[] path)
        {
            var root = RepoRoot();
            var parts = new string[path.Length + 1];
            parts[0] = root;
            Array.Copy(path, 0, parts, 1, path.Length);
            return File.ReadAllBytes(Path.Combine(parts));
        }

        // Own helper by contract: search upward for the paired repo markers; do not delegate to a
        // shared corpus helper owned by another lane.
        private static string RepoRoot()
        {
            foreach (var start in new[]
            {
                TestContext.CurrentContext.TestDirectory,
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory(),
            })
            {
                var at = new DirectoryInfo(start);
                while (at != null)
                {
                    if (File.Exists(Path.Combine(at.FullName, "AGENTS.md"))
                        && Directory.Exists(Path.Combine(at.FullName, "unity"))
                        && Directory.Exists(Path.Combine(at.FullName, "dotnet")))
                        return at.FullName;
                    at = at.Parent;
                }
            }
            throw new DirectoryNotFoundException("Cat Metro repository root not found from NUnit, "
                + "AppContext, or working-directory anchors");
        }
    }
}
