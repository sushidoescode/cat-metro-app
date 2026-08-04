using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CatMetro.Content;
using CatMetro.Content.Daily;
using CatMetro.Content.Validation;
using CatMetro.Services;

namespace CatMetro.DailyTools
{
    // CM-C6 criterion 6 / Q-X (A-C6-5): the console host owning ALL daily-pipeline file I/O —
    // the pipeline library under the Daily root opens and writes nothing; config arrives as
    // bytes through the IContentSource seam and the artifact arrives back as an in-memory
    // string. Clock-free by construction: date keys come from --from/--days or the config
    // anchor row (A-C6-7), never from the machine.
    public sealed class FileContentSource : IContentSource
    {
        public Task<byte[]> ReadAsync(string relativePath, CancellationToken ct) =>
            Task.FromResult(File.ReadAllBytes(relativePath));
        public bool Exists(string relativePath) => File.Exists(relativePath);
    }

    // Q-S (A-C6-9): the fixed-board HARNESS STUB — imports an existing corpus level once and
    // returns that DTO for every date. Zero board-shaping rules live here or anywhere else:
    // the real generator is stop-gated on Q-S and arrives as its own contract; the artifact
    // names this stub in boardProvenance so no one mistakes stub dailies for generated ones.
    internal sealed class FixedBoardFactory : IBoardFactory
    {
        private readonly LevelDto _dto;
        public FixedBoardFactory(LevelDto dto) { _dto = dto; }
        public LevelDto Build(uint seed, string dateKey, int k) => _dto;
    }

    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                return Run(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("validate-dailies: ERROR — " + ex.Message);
                return 2;
            }
        }

        private static int Run(string[] args)
        {
            string outPath = null, fromKey = null, boardPath = null, curvePath = null;
            int? days = null;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--out": outPath = args[++i]; break;
                    case "--from": fromKey = args[++i]; break;
                    case "--days":
                        days = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case "--board": boardPath = args[++i]; break;
                    case "--curve": curvePath = args[++i]; break;
                    default:
                        Console.Error.WriteLine("validate-dailies: unknown argument " + args[i]);
                        return 2;
                }
            }

            var source = new FileContentSource();
            byte[] Read(string path) =>
                source.ReadAsync(path, CancellationToken.None).GetAwaiter().GetResult();

            var configResult = DailyPipelineConfig.Parse(
                Read(Path.Combine("config", "daily_pipeline.json")));
            if (!configResult.Ok)
            {
                Console.Error.WriteLine("validate-dailies: config unreadable — " + configResult.Error);
                return 2;
            }
            var config = configResult.Value;

            byte[] schemaBytes = Read(Path.Combine("docs", "plan", "data", "level_schema.json"));
            var thresholds = ValidatorConfig.Parse(
                Read(Path.Combine("config", "validator_thresholds.json")));
            if (!thresholds.Ok)
            {
                Console.Error.WriteLine("validate-dailies: thresholds unreadable — " + thresholds.Error);
                return 2;
            }

            // NEW-Q21: config/daily_weekday_curve.json does not exist yet; absent bytes flow to
            // the pipeline as null and print UNCONFIGURED(NEW-Q21). An EXPLICIT --curve that is
            // missing is an operator error, not an open pin.
            string curveFile = curvePath ?? Path.Combine("config", "daily_weekday_curve.json");
            byte[] curveBytes = null;
            if (source.Exists(curveFile)) curveBytes = Read(curveFile);
            else if (curvePath != null)
            {
                Console.Error.WriteLine("validate-dailies: --curve file not found: " + curvePath);
                return 2;
            }

            string board = boardPath ?? Path.Combine("content", "levels", "L001.json");
            var import = LevelImporter.Import(Read(board));
            if (!import.Ok)
            {
                Console.Error.WriteLine("validate-dailies: stub board unusable — " + import.Error);
                return 2;
            }

            var dates = DateKeys.Enumerate(fromKey ?? config.AnchorDateKey,
                days ?? config.PrevalidationDays);
            var run = DailyPipeline.Run(new DailyRunRequest(
                schemaBytes, thresholds.Value, config, curveBytes, dates,
                new FixedBoardFactory(import.Value.Dto),
                referenceTimestamp: null,
                boardProvenance: "stub:" + board.Replace('\\', '/') + " (Q-S: no shipped generator)"));
            if (!run.Ok)
            {
                Console.Error.WriteLine("validate-dailies: " + run.Error);
                return 2;
            }
            var report = run.Value;

            // ADR-0009:35: the resolved seed per dateKey — the truth source CM-R43.8 compares a
            // device against. One anchored line per date; nothing else may start with the marker.
            foreach (var line in report.SeedLines())
                Console.WriteLine(line);

            // CM-C5 criterion-13 semantics carried over (criterion 5): non-blocking verdicts
            // PRINT (indented, never marker-anchored) and do not fail; blocking dates print the
            // date key, the stage and the reason.
            foreach (var rec in report.Records)
            {
                foreach (var v in rec.StageVerdicts)
                    if (v.Code != StageVerdictCode.Pass)
                        Console.WriteLine("  " + rec.DateKey + " " + v.Stage + " " + v.Code
                            + " " + v.Detail);
                if (rec.Ramp != null && rec.Ramp.Code != StageVerdictCode.Pass)
                    Console.WriteLine("  " + rec.DateKey + " WeekdayRamp " + rec.Ramp.Code
                        + " " + rec.Ramp.Detail);
                if (rec.Blocks)
                    Console.WriteLine("DAILY_BLOCKING: " + rec.DateKey + " — " + rec.Detail);
            }

            if (outPath != null) File.WriteAllText(outPath, report.ToJson());

            return report.ExitFailure ? 1 : 0;
        }
    }
}
