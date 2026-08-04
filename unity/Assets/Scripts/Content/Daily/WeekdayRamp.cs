using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace CatMetro.Content.Daily
{
    // CM-C6 criterion 9 (CM-R46.5, PIN NEW-Q21): the ramp check reads
    // config/daily_weekday_curve.json — a file that DOES NOT EXIST yet, because neither candidate
    // curve (liveops 0.35…0.75 vs product_spec 0.30…0.55) may be committed by an agent. Absent
    // bytes → UNCONFIGURED(NEW-Q21), printed, never blocking — exactly CM-C5 criterion 13's
    // semantics. When a curve IS supplied (test fixtures today; a human-ratified file later), the
    // board's meta difficultyTarget must sit within ±RAMP_TOLERANCE of the weekday's row, and a
    // miss blocks (CM-R46.5 is a MUST once configured). Curve format per A-C6-8:
    // {"mon": <num>, ..., "sun": <num>} — all seven rows required.
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

        private static readonly string[] DayKeys = { "mon", "tue", "wed", "thu", "fri", "sat", "sun" };

        // Curve rows indexed 0 = Monday .. 6 = Sunday (DateKeys.WeekdayIndex order). A malformed
        // or incomplete curve is a typed failure — a broken human-ratified file must fail the
        // run loudly, never silently downgrade to UNCONFIGURED.
        public static ContentResult<IReadOnlyList<double>> ParseCurve(byte[] bytes)
        {
            try
            {
                if (bytes == null)
                    return ContentResult<IReadOnlyList<double>>.Failure(
                        ContentErrorKind.MalformedJson, "null curve payload");
                var o = JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes));
                var rows = new double[DayKeys.Length];
                for (int i = 0; i < DayKeys.Length; i++)
                {
                    var t = o[DayKeys[i]];
                    if (t == null || (t.Type != JTokenType.Float && t.Type != JTokenType.Integer))
                        return ContentResult<IReadOnlyList<double>>.Failure(
                            ContentErrorKind.MissingField,
                            "weekday curve row '" + DayKeys[i] + "' (number) is required — A-C6-8");
                    rows[i] = (double)t;
                }
                return ContentResult<IReadOnlyList<double>>.Success(rows);
            }
            catch (System.Exception ex)
            {
                return ContentResult<IReadOnlyList<double>>.Failure(ContentErrorKind.MalformedJson,
                    ex.GetType().Name + ": " + ex.Message);
            }
        }

        // curve == null means the file is absent (NEW-Q21 open).
        public static RampVerdict Check(IReadOnlyList<double> curve, string dateKey,
            double difficultyTarget)
        {
            string dt = Num(difficultyTarget);
            if (curve == null)
                return new RampVerdict(Validation.StageVerdictCode.Unconfigured,
                    "UNCONFIGURED(NEW-Q21)", "dt=" + dt, false);

            int weekday = DateKeys.WeekdayIndex(dateKey);
            double row = curve[weekday];
            double diff = difficultyTarget - row;
            if (diff < 0) diff = -diff;
            string value = "dt=" + dt + " curve[" + DayKeys[weekday] + "]=" + Num(row)
                + " tol=" + Num(RAMP_TOLERANCE);
            return diff <= RAMP_TOLERANCE + 1e-9
                ? new RampVerdict(Validation.StageVerdictCode.Pass,
                    "weekday ramp within tolerance (CM-R46.5)", value, false)
                : new RampVerdict(Validation.StageVerdictCode.Fail,
                    "weekday ramp violation (CM-R46.5): |" + dt + " - " + Num(row) + "| > "
                    + Num(RAMP_TOLERANCE), value, true);
        }

        private static string Num(double v) =>
            v.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
    }
}
