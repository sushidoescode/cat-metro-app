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
        public readonly IDailySeedScheme SeedScheme;
        public readonly int MaxNodesExpanded;

        public DailyRunRequest(byte[] schemaBytes, ValidatorConfig validatorConfig,
            DailyPipelineConfig pipelineConfig, byte[] weekdayCurveBytes,
            IReadOnlyList<string> dateKeys, IBoardFactory factory,
            string referenceTimestamp, string boardProvenance,
            int maxNodesExpanded = CatMetro.Domain.Solver.SolverBounds.MAX_NODES_EXPANDED)
            : this(schemaBytes, validatorConfig, pipelineConfig, weekdayCurveBytes, dateKeys,
                factory, referenceTimestamp, boardProvenance, HistoricalDailySeedScheme.Instance,
                maxNodesExpanded)
        {
        }

        public DailyRunRequest(byte[] schemaBytes, ValidatorConfig validatorConfig,
            DailyPipelineConfig pipelineConfig, byte[] weekdayCurveBytes,
            IReadOnlyList<string> dateKeys, IBoardFactory factory,
            string referenceTimestamp, string boardProvenance, IDailySeedScheme seedScheme,
            int maxNodesExpanded = CatMetro.Domain.Solver.SolverBounds.MAX_NODES_EXPANDED)
        {
            SchemaBytes = schemaBytes; ValidatorConfig = validatorConfig;
            PipelineConfig = pipelineConfig; WeekdayCurveBytes = weekdayCurveBytes;
            DateKeys = dateKeys; Factory = factory;
            ReferenceTimestamp = referenceTimestamp; BoardProvenance = boardProvenance;
            SeedScheme = seedScheme;
            MaxNodesExpanded = maxNodesExpanded;
        }
    }

    public sealed class DailyDateRecord
    {
        public readonly string DateKey;
        public readonly int K;          // resolved salt; fallback uses its base variation 0
        public readonly uint Seed;      // seed at K
        public readonly string Verdict; // "Pass" | "Fail"
        public readonly string Detail;  // on Fail: names the stage and the reason (criterion 5)
        public readonly IReadOnlyList<StageVerdict> StageVerdicts; // last attempt, CM-C5 order
        public readonly RampVerdict Ramp;
        public readonly int SolverCompletionTicks; // -1 when stage 4 did not solve
        public readonly bool Blocks;
        public readonly LevelDto Board;   // retained only after runtime admission
        public readonly string BoardJson; // the admitted board's one canonical serialization
        public readonly bool UsedFallback;

        public DailyDateRecord(string dateKey, int k, uint seed, string verdict, string detail,
            IReadOnlyList<StageVerdict> stageVerdicts, RampVerdict ramp,
            int solverCompletionTicks, bool blocks)
            : this(dateKey, k, seed, verdict, detail, stageVerdicts, ramp,
                solverCompletionTicks, blocks, board: null, boardJson: null,
                usedFallback: false)
        {
        }

        public DailyDateRecord(string dateKey, int k, uint seed, string verdict, string detail,
            IReadOnlyList<StageVerdict> stageVerdicts, RampVerdict ramp,
            int solverCompletionTicks, bool blocks, LevelDto board, string boardJson,
            bool usedFallback)
        {
            DateKey = dateKey; K = k; Seed = seed; Verdict = verdict; Detail = detail;
            StageVerdicts = stageVerdicts; Ramp = ramp;
            SolverCompletionTicks = solverCompletionTicks; Blocks = blocks;
            Board = board; BoardJson = boardJson; UsedFallback = usedFallback;
        }
    }

    public sealed class DailyRunReport
    {
        public readonly string Generator;
        public readonly IReadOnlyList<DailyDateRecord> Records;
        public readonly string BoardProvenance;
        public readonly bool ExitFailure;

        public DailyRunReport(string generator, IReadOnlyList<DailyDateRecord> records,
            string boardProvenance, bool exitFailure)
        {
            Generator = generator; Records = records;
            BoardProvenance = boardProvenance; ExitFailure = exitFailure;
        }

        // The artifact, serialised in-memory and handed to the host (criterion 6): one record per
        // date {dateKey, k, seed, verdict, stageVerdicts, solverCompletionTicks}. The ramp check
        // rides inside stageVerdicts as the "WeekdayRamp" entry (it is a daily-leg stage, not one
        // of CM-C5's eleven).
        public string ToJson()
        {
            var root = new Newtonsoft.Json.Linq.JObject
            {
                ["generator"] = Generator,
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

        // One line per RESOLVED date, "DAILY_SEED <dateKey> <k> <seed>" (ADR-0009:35 prints "the
        // RESOLVED seed per dateKey"; CM-R43.8's truth source) — single-sourced here so the host
        // and the tests agree on the format. Review F6: a blocked date resolved nothing, so it
        // emits NO line — otherwise a consumer grepping the markers out of a CI log could pin a
        // seed the job actually rejected. Blocked dates surface as DAILY_BLOCKING + exit 1.
        public IReadOnlyList<string> SeedLines()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var lines = new List<string>(Records.Count);
            foreach (var r in Records)
            {
                if (r.Blocks) continue;
                lines.Add("DAILY_SEED " + r.DateKey + " " + r.K.ToString(inv)
                    + " " + r.Seed.ToString(inv));
            }
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
                || request.Factory == null || request.SeedScheme == null)
                return Fail(ContentErrorKind.MissingField,
                    "request needs schema bytes, validator config, pipeline config, date keys, a factory and a seed scheme");

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
                new DailyRunReport(request.SeedScheme.ArtifactLabel, records,
                    request.BoardProvenance, exitFailure));
        }

        private static DailyDateRecord RunDate(DailyRunRequest request, string dateKey,
            int saltMaxK, IReadOnlyList<double> curve)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            AdmissionResult lastAdmission = AdmissionResult.Rejected("no attempt ran");
            AdmissionResult admitted = null;
            uint seed = 0;
            int k = 0;
            bool usedFallback = false;

            // Criterion 4: bounded, deterministic — k = 0..SALT_MAX_K inclusive, then exhaustion.
            for (k = 0; k <= saltMaxK; k++)
            {
                seed = request.SeedScheme.Derive(dateKey, k);

                LevelDto dto;
                try
                {
                    dto = request.Factory.Build(seed, dateKey, k);
                }
                catch (System.Exception ex)
                {
                    lastAdmission = AdmissionResult.Rejected(
                        "board factory threw " + ex.GetType().Name + ": " + ex.Message);
                    continue;
                }
                if (dto == null)
                {
                    lastAdmission = AdmissionResult.Rejected("board factory returned null");
                    continue;
                }

                lastAdmission = Admit(request, dateKey + "#k" + k.ToString(inv), dto);
                if (!lastAdmission.Accepted) continue;
                admitted = lastAdmission;
                break;
            }

            if (admitted == null)
            {
                k = saltMaxK; // the last attempted candidate salt
                if (request.Factory is IDailyFallbackBoardFactory fallbackFactory)
                {
                    k = 0;
                    seed = request.SeedScheme.Derive(dateKey, 0);
                    LevelDto fallback = null;
                    try
                    {
                        fallback = fallbackFactory.BuildFallback(seed, dateKey);
                    }
                    catch (System.Exception ex)
                    {
                        lastAdmission = AdmissionResult.Rejected(
                            "fallback factory threw " + ex.GetType().Name + ": " + ex.Message);
                    }

                    if (fallback == null)
                    {
                        if (!lastAdmission.Failure.StartsWith("fallback factory threw",
                            System.StringComparison.Ordinal))
                            lastAdmission = AdmissionResult.Rejected("fallback factory returned null");
                    }
                    else
                    {
                        lastAdmission = Admit(request, dateKey + "#fallback", fallback);
                        if (lastAdmission.Accepted)
                        {
                            admitted = lastAdmission;
                            usedFallback = true;
                        }
                    }
                }
            }

            LevelDto rampDto = admitted != null ? admitted.Board : null;
            RampVerdict ramp = rampDto != null
                ? WeekdayRamp.Check(curve, dateKey, rampDto.Meta.DifficultyTarget)
                : new RampVerdict(StageVerdictCode.Skipped, "SKIPPED(no board)", "", false);

            int solverTicks = lastAdmission.Solve != null
                && lastAdmission.Solve.Verdict == CatMetro.Domain.Solver.SolveVerdict.Solved
                ? lastAdmission.Solve.CompletionTicks : -1;

            if (admitted == null)
                return new DailyDateRecord(dateKey, k, seed, "Fail",
                    "SALT_MAX_K exhausted (k=0.." + saltMaxK.ToString(inv)
                    + ", A-C6-2) — "
                    + (request.Factory is IDailyFallbackBoardFactory
                        ? "fallback admission failed: " : "last attempt: ")
                    + lastAdmission.Failure, lastAdmission.Verdicts, ramp, solverTicks,
                    blocks: true, board: null, boardJson: null, usedFallback: false);

            if (ramp.Blocks)
                return new DailyDateRecord(dateKey, k, seed, "Fail",
                    "WeekdayRamp — " + ramp.Detail, admitted.Verdicts, ramp, solverTicks,
                    blocks: true, admitted.Board, admitted.BoardJson, usedFallback);

            return new DailyDateRecord(dateKey, k, seed, "Pass", "",
                admitted.Verdicts, ramp, solverTicks, blocks: false,
                admitted.Board, admitted.BoardJson, usedFallback);
        }

        // Candidate and fallback admission have one implementation: one canonical serialization,
        // one real CM-C5 validator call, exactly the eleven ordered level rows, no blocking
        // verdict, and an actual solver proof. Rejected board bytes never leave this helper.
        private static AdmissionResult Admit(DailyRunRequest request, string sourceName, LevelDto dto)
        {
            try
            {
                string json = DailyBoardJson.Serialize(dto);
                var member = new CorpusMember(sourceName,
                    System.Text.Encoding.UTF8.GetBytes(json), isCampaign: false);
                var report = CorpusValidator.Validate(new ValidationRequest(
                    request.SchemaBytes, request.ValidatorConfig, request.ReferenceTimestamp,
                    new[] { member }, request.MaxNodesExpanded));

                if (report.Levels.Count != 1)
                    return AdmissionResult.Rejected(
                        "validator returned " + report.Levels.Count + " levels; expected exactly one");

                var level = report.Levels[0];
                if (!HasElevenOrderedStages(level.Verdicts))
                    return AdmissionResult.Rejected("validator did not return stages 1..11 in order",
                        level.Verdicts, level.Solve);
                if (report.ExitFailure || HasBlockingVerdict(level.Verdicts))
                    return AdmissionResult.Rejected(BlockingSummary(level.Verdicts),
                        level.Verdicts, level.Solve);
                if (level.Solve == null
                    || level.Solve.Verdict != CatMetro.Domain.Solver.SolveVerdict.Solved)
                    return AdmissionResult.Rejected("Solver — "
                        + (level.Solve == null ? "missing solve result" : level.Solve.Verdict.ToString()),
                        level.Verdicts, level.Solve);

                return AdmissionResult.Admitted(dto, json, level.Verdicts, level.Solve);
            }
            catch (System.Exception ex)
            {
                return AdmissionResult.Rejected(
                    "board admission threw " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static bool HasElevenOrderedStages(IReadOnlyList<StageVerdict> verdicts)
        {
            if (verdicts == null || verdicts.Count != 11) return false;
            for (int i = 0; i < verdicts.Count; i++)
                if ((int)verdicts[i].Stage != i + 1) return false;
            return true;
        }

        private static bool HasBlockingVerdict(IReadOnlyList<StageVerdict> verdicts)
        {
            foreach (var verdict in verdicts)
                if (verdict.Blocks) return true;
            return false;
        }

        private sealed class AdmissionResult
        {
            public readonly bool Accepted;
            public readonly LevelDto Board;
            public readonly string BoardJson;
            public readonly IReadOnlyList<StageVerdict> Verdicts;
            public readonly CatMetro.Domain.Solver.SolveResult Solve;
            public readonly string Failure;

            private AdmissionResult(bool accepted, LevelDto board, string boardJson,
                IReadOnlyList<StageVerdict> verdicts,
                CatMetro.Domain.Solver.SolveResult solve, string failure)
            {
                Accepted = accepted; Board = board; BoardJson = boardJson;
                Verdicts = verdicts; Solve = solve; Failure = failure;
            }

            public static AdmissionResult Admitted(LevelDto board, string boardJson,
                IReadOnlyList<StageVerdict> verdicts,
                CatMetro.Domain.Solver.SolveResult solve) =>
                new AdmissionResult(true, board, boardJson, verdicts, solve, "");

            public static AdmissionResult Rejected(string failure,
                IReadOnlyList<StageVerdict> verdicts = null,
                CatMetro.Domain.Solver.SolveResult solve = null) =>
                new AdmissionResult(false, null, null,
                    verdicts ?? System.Array.Empty<StageVerdict>(), solve, failure);
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
