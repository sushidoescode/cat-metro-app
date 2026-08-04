using System.Collections.Generic;

namespace CatMetro.Content.Daily
{
    // CM-C6 criterion 9 (CM-R46.5, PIN NEW-Q21): the ramp check reads
    // config/daily_weekday_curve.json — a file that DOES NOT EXIST yet, because neither candidate
    // curve (liveops 0.35…0.75 vs product_spec 0.30…0.55) may be committed by an agent. Absent
    // bytes → UNCONFIGURED(NEW-Q21), printed, never blocking — exactly CM-C5 criterion 13's
    // semantics. When a curve IS supplied (test fixtures today; a human-ratified file later), the
    // board's meta difficultyTarget must sit within ±RAMP_TOLERANCE of the weekday's row, and a
    // miss blocks (CM-R46.5 is a MUST once configured). Curve format per A-C6-8:
    // {"mon": <num>, ..., "sun": <num>}.
    public sealed class RampVerdict
    {
        public readonly Validation.StageVerdictCode Code;
        public readonly string Detail;
        public readonly string Value;
        public readonly bool Blocks;

        public RampVerdict(Validation.StageVerdictCode code, string detail, string value, bool blocks)
        {
            Code = code; Detail = detail ?? ""; Value = value ?? ""; Blocks = blocks;
        }
    }

    public static class WeekdayRamp
    {
        public const double RAMP_TOLERANCE = 0.05; // CM-R46.5: authored curve value ±0.05

        // curveBytes == null means the file is absent (NEW-Q21 open).
        public static RampVerdict Check(byte[] curveBytes, string dateKey, double difficultyTarget)
        {
            throw new System.NotImplementedException();
        }
    }
}
