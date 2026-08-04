using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using CatMetro.Content;
using CatMetro.Content.Daily;
using CatMetro.Tests.Domain;
using CatMetro.Tests.Validation;

namespace CatMetro.Tests.Daily
{
    // CM-C6 test fixtures. Boards reuse the REAL L001 bytes through VFixtures (one truth);
    // pipeline configs are in-code JSON except the shipped-file assertions of criterion 3.
    // Tests may use file APIs; the shipped Daily code may not (criterion 6).
    public static class DFixtures
    {
        public static byte[] ShippedPipelineConfigBytes() =>
            File.ReadAllBytes(Path.Combine(Fixtures.RepoRoot(), "config", "daily_pipeline.json"));

        public static DailyPipelineConfig ShippedPipelineConfig()
        {
            var r = DailyPipelineConfig.Parse(ShippedPipelineConfigBytes());
            Assert.That(r.Ok, Is.True, $"shipped daily_pipeline.json must parse: {r.Error}");
            return r.Value;
        }

        public static DailyPipelineConfig Config(string json)
        {
            var r = DailyPipelineConfig.Parse(Encoding.UTF8.GetBytes(json));
            Assert.That(r.Ok, Is.True, $"fixture config must parse: {r.Error}");
            return r.Value;
        }

        // Small ceiling + tiny horizon for loop-behaviour tests.
        public static DailyPipelineConfig TinyConfig(int saltMaxK = 2) =>
            Config("{\"DAILY_PREVALIDATION_DAYS\": 3, \"SALT_MAX_K\": " + saltMaxK
                + ", \"PIPELINE_ANCHOR_DATE\": \"2026-08-24\"}");

        public static LevelDto L001Dto() => VFixtures.Import(VFixtures.L001Bytes()).Dto;

        public static LevelDto UnsolvableDto() => VFixtures.Import(VFixtures.UnsolvableLevel()).Dto;

        // Always the same imported corpus board — the shape the host's Q-S harness stub has.
        public sealed class FixedFactory : IBoardFactory
        {
            private readonly LevelDto _dto;
            public readonly List<(uint seed, string dateKey, int k)> Calls =
                new List<(uint, string, int)>();

            public FixedFactory(LevelDto dto) { _dto = dto; }

            public LevelDto Build(uint seed, string dateKey, int k)
            {
                Calls.Add((seed, dateKey, k));
                return _dto;
            }
        }

        // Fails every blocking attempt below passAtK, passes from passAtK on. passAtK beyond
        // SALT_MAX_K == always failing.
        public sealed class KeyedFactory : IBoardFactory
        {
            private readonly int _passAtK;
            private readonly LevelDto _good;
            private readonly LevelDto _bad;
            public int Builds;

            public KeyedFactory(int passAtK)
            {
                _passAtK = passAtK;
                _good = L001Dto();
                _bad = UnsolvableDto();
            }

            public LevelDto Build(uint seed, string dateKey, int k)
            {
                Builds++;
                return k >= _passAtK ? _good : _bad;
            }
        }

        public sealed class ThrowingFactory : IBoardFactory
        {
            public LevelDto Build(uint seed, string dateKey, int k) =>
                throw new System.InvalidOperationException("factory detonated (totality probe)");
        }

        public static DailyRunRequest Request(IReadOnlyList<string> dateKeys, IBoardFactory factory,
            DailyPipelineConfig config = null, byte[] curveBytes = null)
        {
            return new DailyRunRequest(
                VFixtures.SchemaBytes(),
                VFixtures.BareConfig(),
                config ?? TinyConfig(),
                curveBytes,
                dateKeys,
                factory,
                referenceTimestamp: null,
                boardProvenance: "test-stub");
        }

        public static DailyRunReport Run(DailyRunRequest request)
        {
            var r = DailyPipeline.Run(request);
            Assert.That(r.Ok, Is.True, $"pipeline run must succeed: {r.Error}");
            return r.Value;
        }
    }
}
