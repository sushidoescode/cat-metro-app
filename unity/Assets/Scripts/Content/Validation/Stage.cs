namespace CatMetro.Content.Validation
{
    // CM-C5 criterion 1: exactly the 11 stages of product_spec.md:637-647, in order, with these
    // names. A contract test pins members and ordinals (the CM-C1 FailReason shape).
    public enum Stage
    {
        Schema = 1,
        StaticAnalysis = 2,
        LowerBoundFeasibility = 3,
        Solver = 4,
        TrivialityReject = 5,
        BrittlenessAccessibility = 6,
        StarCheck = 7,
        DifficultyCheck = 8,
        NoveltyCheck = 9,
        Staleness = 10,
        HumanPlaytest = 11,
    }

    // Verdict vocabulary (handoff §Verdict model). Only Fail on a blocking stage contributes to
    // the exit code; Unconfigured/Pinned/Skipped/Stale/Fresh/Pending/Warn never block (Q-R, Q-N,
    // Q-O, NEW-Q5, D-6 — each printed, none silently dropped).
    public enum StageVerdictCode
    {
        Pass = 1,
        Fail = 2,
        Warn = 3,
        Unconfigured = 4,
        Skipped = 5,
        Pinned = 6,
        Stale = 7,
        Fresh = 8,
        Pending = 9,
    }

    public sealed class StageVerdict
    {
        public readonly Stage Stage;
        public readonly StageVerdictCode Code;
        public readonly string Detail;   // human-readable; carries UNCONFIGURED(<row>)/PINNED(<id>)/... verbatim
        public readonly string Value;    // the computed value, always printed even when not compared
        public readonly bool Blocks;

        public StageVerdict(Stage stage, StageVerdictCode code, string detail, string value, bool blocks)
        {
            Stage = stage; Code = code; Detail = detail ?? ""; Value = value ?? ""; Blocks = blocks;
        }

        public static StageVerdict Pass(Stage s, string value = "", string detail = "") =>
            new StageVerdict(s, StageVerdictCode.Pass, detail, value, false);
        public static StageVerdict Fail(Stage s, string detail, string value = "") =>
            new StageVerdict(s, StageVerdictCode.Fail, detail, value, true);
        public static StageVerdict NonBlockingFail(Stage s, string detail, string value = "") =>
            new StageVerdict(s, StageVerdictCode.Fail, detail, value, false);
        public static StageVerdict Warn(Stage s, string detail, string value = "") =>
            new StageVerdict(s, StageVerdictCode.Warn, detail, value, false);
        public static StageVerdict Unconfigured(Stage s, string row, string value) =>
            new StageVerdict(s, StageVerdictCode.Unconfigured, "UNCONFIGURED(" + row + ")", value, false);
        public static StageVerdict Skipped(Stage s, string why) =>
            new StageVerdict(s, StageVerdictCode.Skipped, "SKIPPED(" + why + ")", "", false);
        public static StageVerdict Pinned(Stage s, string pin, string value = "") =>
            new StageVerdict(s, StageVerdictCode.Pinned, "PINNED(" + pin + ")", value, false);
    }
}
