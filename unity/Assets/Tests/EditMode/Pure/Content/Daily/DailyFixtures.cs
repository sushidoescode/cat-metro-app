using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        public static LevelDto L001Dto() => DailySerializableDto(VFixtures.L001Bytes());

        public static LevelDto UnsolvableDto() => DailySerializableDto(VFixtures.UnsolvableLevel());

        public static LevelDto TrivialWinDto() => DailySerializableDto(VFixtures.TrivialWinLevel());

        public static LevelDto AllFieldsDto() => new LevelDto(
            2, "L800", "Daily Field Probe", 8080,
            new MetaDto("daily", 0.625,
                new[] { "switch", "reversible", "tunnel", "hold", "cooldown", "gate",
                    "express", "shape", "stray" },
                "gate", "Preserve every daily DTO field", 5, "generator+validator",
                "2026-09-02", true),
            new[]
            {
                new NodeDto("SRC", 1, 6, 3, true),
                new NodeDto("J1", 2, 3, 0, false),
                new NodeDto("ST", 4, 0, 0, false),
            },
            new[]
            {
                new EdgeDto("E_REV", "SRC", "J1", 3,
                    oneWay: false, reversible: true, tunnel: true),
                new EdgeDto("E_HOLD", "J1", "ST", 4, hold: true),
            },
            new[] { new SourceDto("SRC", new[] { "red", "wild" }) },
            new[] { new StationDto("ST", new[] { "red" }, 4, "triangle") },
            new[] { new SwitchDto("S1", "J1", new[] { "E_REV", "E_HOLD" }, 1, 5) },
            new[] { new WaveDto(2, "SRC", "red", 1, 8,
                express: true, shape: "square", stray: true) },
            new WinDto(1, 40, 2, new StarsDto(120, 220)),
            new EconomyDto(30, 12),
            new[]
            {
                new GateDto("E_HOLD", new[]
                {
                    new GateWindowDto(2, 6),
                    new GateWindowDto(10, 14),
                }, 9),
            },
            new[] { "daily", "field-probe" });

        private static LevelDto DailySerializableDto(byte[] bytes)
        {
            var json = JObject.Parse(Encoding.UTF8.GetString(bytes));
            json["win"]["perfectMaxSwitches"] = 1;
            return VFixtures.Import(Encoding.UTF8.GetBytes(json.ToString(Formatting.None))).Dto;
        }

        public static LevelDto NullMetaDto()
        {
            var dto = L001Dto();
            return new LevelDto(dto.SchemaVersion, dto.Id, dto.Name, dto.Seed, meta: null,
                dto.Nodes.Span.ToArray(), dto.Edges.Span.ToArray(), dto.Sources.Span.ToArray(),
                dto.Stations.Span.ToArray(), dto.Switches.Span.ToArray(), dto.Waves.Span.ToArray(),
                dto.Win, dto.Economy);
        }

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

        // Task 4's controlled seam: candidate/fallback DTO selection and call capture only.
        // Admission remains the production pipeline's real CorpusValidator responsibility.
        public sealed class ExhaustingFallbackFactory : IBoardFactory, IDailyFallbackBoardFactory
        {
            private readonly LevelDto _candidate;
            private readonly LevelDto _fallback;

            public readonly List<(uint seed, string dateKey, int k)> CandidateCalls =
                new List<(uint, string, int)>();
            public readonly List<(uint seed, string dateKey)> FallbackCalls =
                new List<(uint, string)>();

            public ExhaustingFallbackFactory(LevelDto candidate, LevelDto fallback)
            {
                _candidate = candidate;
                _fallback = fallback;
            }

            public LevelDto Build(uint seed, string dateKey, int k)
            {
                CandidateCalls.Add((seed, dateKey, k));
                return _candidate;
            }

            public LevelDto BuildFallback(uint seed, string dateKey)
            {
                FallbackCalls.Add((seed, dateKey));
                return _fallback;
            }
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

        public static DailyRunRequest RuntimeRequest(IReadOnlyList<string> dateKeys,
            IBoardFactory factory, DailyPipelineConfig config = null,
            byte[] curveBytes = null,
            int maxNodesExpanded = CatMetro.Domain.Solver.SolverBounds.MAX_NODES_EXPANDED)
        {
            return new DailyRunRequest(
                VFixtures.SchemaBytes(),
                VFixtures.BareConfig(),
                config ?? TinyConfig(),
                curveBytes,
                dateKeys,
                factory,
                referenceTimestamp: null,
                boardProvenance: "runtime-test",
                DailyLineSeedScheme.Instance,
                maxNodesExpanded);
        }

        public static DailyRunReport Run(DailyRunRequest request)
        {
            var r = DailyPipeline.Run(request);
            Assert.That(r.Ok, Is.True, $"pipeline run must succeed: {r.Error}");
            return r.Value;
        }
    }
}
