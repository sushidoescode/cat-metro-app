using System.Collections.Generic;
using CatMetro.Content.Validation;

namespace CatMetro.Content.Daily
{
    // CM-C6's orchestrator: given a date-key list (INPUTS — criterion 2), derive each date's
    // seed, run the bounded deterministic salt loop (criterion 4), validate every candidate
    // board through CM-C5's REAL stages (criterion 5: each candidate LevelDto is serialised
    // in-memory and fed to CorpusValidator as a single-member non-campaign corpus, so the two
    // jobs cannot disagree), apply the weekday ramp check (criterion 9), and emit the artifact
    // (criterion 6) — all with zero file or console access; the DailyTools host owns that.
    public sealed class DailyRunRequest
    {
        public readonly byte[] SchemaBytes;
        public readonly ValidatorConfig ValidatorConfig;
        public readonly DailyPipelineConfig PipelineConfig;
        public readonly byte[] WeekdayCurveBytes;       // null == file absent (NEW-Q21)
        public readonly IReadOnlyList<string> DateKeys; // criterion 2: the date list is an input
        public readonly IBoardFactory Factory;
        public readonly string ReferenceTimestamp;      // host-computed or null (CM-C5 A-C5-4)
        public readonly string BoardProvenance;         // artifact honesty: names the Q-S stub
        public readonly int MaxNodesExpanded;

        public DailyRunRequest(byte[] schemaBytes, ValidatorConfig validatorConfig,
            DailyPipelineConfig pipelineConfig, byte[] weekdayCurveBytes,
            IReadOnlyList<string> dateKeys, IBoardFactory factory,
            string referenceTimestamp, string boardProvenance,
            int maxNodesExpanded = CatMetro.Domain.Solver.SolverBounds.MAX_NODES_EXPANDED)
        {
            SchemaBytes = schemaBytes; ValidatorConfig = validatorConfig;
            PipelineConfig = pipelineConfig; WeekdayCurveBytes = weekdayCurveBytes;
            DateKeys = dateKeys; Factory = factory;
            ReferenceTimestamp = referenceTimestamp; BoardProvenance = boardProvenance;
            MaxNodesExpanded = maxNodesExpanded;
        }
    }

    public sealed class DailyDateRecord
    {
        public readonly string DateKey;
        public readonly int K;          // resolved salt, or the last attempted salt on failure
        public readonly uint Seed;      // seed at K
        public readonly string Verdict; // "Pass" | "Fail"
        public readonly string Detail;  // on Fail: names the stage and the reason (criterion 5)
        public readonly IReadOnlyList<StageVerdict> StageVerdicts; // last attempt, CM-C5 order
        public readonly RampVerdict Ramp;
        public readonly int SolverCompletionTicks; // -1 when stage 4 did not solve
        public readonly bool Blocks;

        public DailyDateRecord(string dateKey, int k, uint seed, string verdict, string detail,
            IReadOnlyList<StageVerdict> stageVerdicts, RampVerdict ramp,
            int solverCompletionTicks, bool blocks)
        {
            DateKey = dateKey; K = k; Seed = seed; Verdict = verdict; Detail = detail;
            StageVerdicts = stageVerdicts; Ramp = ramp;
            SolverCompletionTicks = solverCompletionTicks; Blocks = blocks;
        }
    }

    public sealed class DailyRunReport
    {
        public readonly IReadOnlyList<DailyDateRecord> Records;
        public readonly string BoardProvenance;
        public readonly bool ExitFailure;

        public DailyRunReport(IReadOnlyList<DailyDateRecord> records, string boardProvenance,
            bool exitFailure)
        {
            Records = records; BoardProvenance = boardProvenance; ExitFailure = exitFailure;
        }

        // The artifact, serialised in-memory and handed to the host (criterion 6): one record per
        // date {dateKey, k, seed, verdict, stageVerdicts, solverCompletionTicks}. The ramp check
        // rides inside stageVerdicts as the "WeekdayRamp" entry (it is a daily-leg stage, not one
        // of CM-C5's eleven).
        public string ToJson()
        {
            var root = new Newtonsoft.Json.Linq.JObject
            {
                ["generator"] = DailySeed.GENERATOR_CONSTANT,
                ["boardProvenance"] = BoardProvenance,
            };
            var dates = new Newtonsoft.Json.Linq.JArray();
            foreach (var r in Records)
            {
                var stages = new Newtonsoft.Json.Linq.JArray();
                foreach (var v in r.StageVerdicts)
                    stages.Add(new Newtonsoft.Json.Linq.JObject
                    {
                        ["stage"] = v.Stage.ToString(),
                        ["code"] = v.Code.ToString(),
                        ["detail"] = v.Detail,
                        ["value"] = v.Value,
                        ["blocks"] = v.Blocks,
                    });
                if (r.Ramp != null)
                    stages.Add(new Newtonsoft.Json.Linq.JObject
                    {
                        ["stage"] = "WeekdayRamp",
                        ["code"] = r.Ramp.Code.ToString(),
                        ["detail"] = r.Ramp.Detail,
                        ["value"] = r.Ramp.Value,
                        ["blocks"] = r.Ramp.Blocks,
                    });
                dates.Add(new Newtonsoft.Json.Linq.JObject
                {
                    ["dateKey"] = r.DateKey,
                    ["k"] = r.K,
                    ["seed"] = r.Seed,
                    ["verdict"] = r.Verdict,
                    ["detail"] = r.Detail,
                    ["stageVerdicts"] = stages,
                    ["solverCompletionTicks"] = r.SolverCompletionTicks,
                });
            }
            root["dates"] = dates;
            root["exitFailure"] = ExitFailure;
            return root.ToString(Newtonsoft.Json.Formatting.Indented);
        }

        // One line per date, "DAILY_SEED <dateKey> <k> <seed>" (ADR-0009:35; CM-R43.8's truth
        // source) — single-sourced here so the host and the tests agree on the format.
        public IReadOnlyList<string> SeedLines()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var lines = new List<string>(Records.Count);
            foreach (var r in Records)
                lines.Add("DAILY_SEED " + r.DateKey + " " + r.K.ToString(inv)
                    + " " + r.Seed.ToString(inv));
            return lines;
        }
    }

    public static class DailyPipeline
    {
        public static ContentResult<DailyRunReport> Run(DailyRunRequest request)
        {
            if (request == null)
                return Fail(ContentErrorKind.MalformedJson, "null request");
            if (request.SchemaBytes == null || request.ValidatorConfig == null
                || request.PipelineConfig == null || request.DateKeys == null
                || request.Factory == null)
                return Fail(ContentErrorKind.MissingField,
                    "request needs schema bytes, validator config, pipeline config, date keys and a factory");

            // A-C6-4: every key validated up front; one malformed key rejects the run — deriving
            // from a malformed key would silently fork the shared board.
            var seen = new HashSet<string>();
            foreach (var key in request.DateKeys)
            {
                if (!DateKeys.IsValid(key))
                    return Fail(ContentErrorKind.BoundViolation,
                        "date key '" + (key ?? "<null>") + "' is not a yyyy-MM-dd calendar date (A-C6-4)");
                if (!seen.Add(key))
                    return Fail(ContentErrorKind.DuplicateId,
                        "date key '" + key + "' appears twice — the artifact is one record per date");
            }

            IReadOnlyList<double> curve = null;
            if (request.WeekdayCurveBytes != null)
            {
                var parsed = WeekdayRamp.ParseCurve(request.WeekdayCurveBytes);
                if (!parsed.Ok)
                    return Fail(parsed.Error.Kind, "weekday curve: " + parsed.Error.Detail);
                curve = parsed.Value;
            }

            int saltMaxK = request.PipelineConfig.SaltMaxK;
            var records = new List<DailyDateRecord>(request.DateKeys.Count);
            foreach (var dateKey in request.DateKeys)
                records.Add(RunDate(request, dateKey, saltMaxK, curve));

            bool exitFailure = false;
            foreach (var r in records) exitFailure |= r.Blocks;
            return ContentResult<DailyRunReport>.Success(
                new DailyRunReport(records, request.BoardProvenance, exitFailure));
        }

        private static DailyDateRecord RunDate(DailyRunRequest request, string dateKey,
            int saltMaxK, IReadOnlyList<double> curve)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            IReadOnlyList<StageVerdict> lastVerdicts = System.Array.Empty<StageVerdict>();
            CatMetro.Domain.Solver.SolveResult lastSolve = null;
            LevelDto lastDto = null;
            string lastFailure = "no attempt ran";
            uint seed = 0;
            int k = 0;
            bool resolved = false;

            // Criterion 4: bounded, deterministic — k = 0..SALT_MAX_K inclusive, then exhaustion.
            for (k = 0; k <= saltMaxK; k++)
            {
                seed = DailySeed.Derive(dateKey, k);

                LevelDto dto;
                try
                {
                    dto = request.Factory.Build(seed, dateKey, k);
                }
                catch (System.Exception ex)
                {
                    lastFailure = "board factory threw " + ex.GetType().Name + ": " + ex.Message;
                    lastVerdicts = System.Array.Empty<StageVerdict>();
                    lastSolve = null; lastDto = null;
                    continue;
                }
                if (dto == null)
                {
                    lastFailure = "board factory returned null";
                    lastVerdicts = System.Array.Empty<StageVerdict>();
                    lastSolve = null; lastDto = null;
                    continue;
                }

                string json;
                try
                {
                    json = DailyBoardJson.Serialize(dto);
                }
                catch (System.Exception ex)
                {
                    lastFailure = "board serialisation threw " + ex.GetType().Name + ": " + ex.Message;
                    lastVerdicts = System.Array.Empty<StageVerdict>();
                    lastSolve = null; lastDto = dto;
                    continue;
                }

                // Criterion 5: CM-C5's ACTUAL stages over the candidate, as a single-member
                // non-campaign corpus — identical blocking semantics by construction.
                var member = new CorpusMember(
                    dateKey + "#k" + k.ToString(inv),
                    System.Text.Encoding.UTF8.GetBytes(json),
                    isCampaign: false);
                var report = CorpusValidator.Validate(new ValidationRequest(
                    request.SchemaBytes, request.ValidatorConfig, request.ReferenceTimestamp,
                    new[] { member }, request.MaxNodesExpanded));
                var level = report.Levels[0];
                lastVerdicts = level.Verdicts;
                lastSolve = level.Solve;
                lastDto = dto;

                if (!report.ExitFailure) { resolved = true; break; }
                lastFailure = BlockingSummary(level.Verdicts);
            }
            if (!resolved) k = saltMaxK; // the last attempted salt

            RampVerdict ramp = lastDto != null
                ? WeekdayRamp.Check(curve, dateKey, lastDto.Meta.DifficultyTarget)
                : new RampVerdict(StageVerdictCode.Skipped, "SKIPPED(no board)", "", false);

            int solverTicks = lastSolve != null
                && lastSolve.Verdict == CatMetro.Domain.Solver.SolveVerdict.Solved
                ? lastSolve.CompletionTicks : -1;

            if (!resolved)
                return new DailyDateRecord(dateKey, k, seed, "Fail",
                    "SALT_MAX_K exhausted (k=0.." + saltMaxK.ToString(inv) + ", A-C6-2) — last attempt: "
                    + lastFailure, lastVerdicts, ramp, solverTicks, blocks: true);

            if (ramp.Blocks)
                return new DailyDateRecord(dateKey, k, seed, "Fail",
                    "WeekdayRamp — " + ramp.Detail, lastVerdicts, ramp, solverTicks, blocks: true);

            return new DailyDateRecord(dateKey, k, seed, "Pass", "",
                lastVerdicts, ramp, solverTicks, blocks: false);
        }

        private static string BlockingSummary(IReadOnlyList<StageVerdict> verdicts)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var v in verdicts)
            {
                if (!v.Blocks) continue;
                if (sb.Length > 0) sb.Append("; ");
                sb.Append("stage ").Append((int)v.Stage).Append(' ').Append(v.Stage)
                  .Append(" — ").Append(v.Detail);
            }
            return sb.Length > 0 ? sb.ToString() : "blocking stage unidentified";
        }

        private static ContentResult<DailyRunReport> Fail(ContentErrorKind kind, string detail) =>
            ContentResult<DailyRunReport>.Failure(kind, detail);
    }
}
