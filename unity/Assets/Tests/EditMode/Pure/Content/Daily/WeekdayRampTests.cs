using System.Linq;
using System.Text;
using NUnit.Framework;
using CatMetro.Content.Daily;
using CatMetro.Content.Validation;

namespace CatMetro.Tests.Daily
{
    // CM-C6 criterion 9 (CM-R46.5, PIN NEW-Q21): with config/daily_weekday_curve.json ABSENT the
    // ramp prints UNCONFIGURED(NEW-Q21) and never blocks; with a fixture curve supplied the
    // ±0.05 comparison actually runs. Fixture values are SYNTHETIC — deliberately neither
    // candidate curve, which no agent may commit.
    public sealed class WeekdayRampTests
    {
        private static byte[] Curve(string json) => Encoding.UTF8.GetBytes(json);

        private static string Flat(double v) =>
            "{\"mon\":" + V(v) + ",\"tue\":" + V(v) + ",\"wed\":" + V(v) + ",\"thu\":" + V(v)
            + ",\"fri\":" + V(v) + ",\"sat\":" + V(v) + ",\"sun\":" + V(v) + "}";

        private static string V(double v) =>
            v.ToString("0.0###", System.Globalization.CultureInfo.InvariantCulture);

        // Criterion 9a: file absent -> UNCONFIGURED(NEW-Q21), printed, non-blocking.
        [Test]
        public void CurveAbsent_IsUnconfiguredNEWQ21_NonBlocking()
        {
            var report = DFixtures.Run(DFixtures.Request(
                new[] { "2026-08-24" }, new DFixtures.FixedFactory(DFixtures.L001Dto()),
                curveBytes: null));
            var rec = report.Records.Single();

            Assert.That(rec.Ramp.Code, Is.EqualTo(StageVerdictCode.Unconfigured));
            Assert.That(rec.Ramp.Detail, Is.EqualTo("UNCONFIGURED(NEW-Q21)"));
            Assert.That(rec.Ramp.Blocks, Is.False);
            Assert.That(report.ExitFailure, Is.False);
            Assert.That(report.ToJson(), Does.Contain("UNCONFIGURED(NEW-Q21)"),
                "the artifact must print the open pin, not drop it");
        }

        // Criterion 9b: the comparison runs when a curve is supplied — in-band passes
        // (L001 difficultyTarget 0.08 vs 0.10: |diff| = 0.02 <= 0.05)...
        [Test]
        public void CurveSupplied_InBand_Passes()
        {
            var report = DFixtures.Run(DFixtures.Request(
                new[] { "2026-08-24" }, new DFixtures.FixedFactory(DFixtures.L001Dto()),
                curveBytes: Curve(Flat(0.10))));
            var rec = report.Records.Single();

            Assert.That(rec.Ramp.Code, Is.EqualTo(StageVerdictCode.Pass));
            Assert.That(rec.Ramp.Blocks, Is.False);
            Assert.That(report.ExitFailure, Is.False);
        }

        // ...and out-of-band blocks (0.08 vs 0.50: 0.42 > 0.05). CM-R46.5 is a MUST once a
        // human-ratified curve exists.
        [Test]
        public void CurveSupplied_OutOfBand_Blocks()
        {
            var report = DFixtures.Run(DFixtures.Request(
                new[] { "2026-08-24" }, new DFixtures.FixedFactory(DFixtures.L001Dto()),
                curveBytes: Curve(Flat(0.50))));
            var rec = report.Records.Single();

            Assert.That(rec.Ramp.Code, Is.EqualTo(StageVerdictCode.Fail));
            Assert.That(rec.Ramp.Blocks, Is.True);
            Assert.That(report.ExitFailure, Is.True);
        }

        // The comparison selects the WEEKDAY's row: Monday 2026-08-24 sits in band under this
        // curve, Tuesday 2026-08-25 does not.
        [Test]
        public void Curve_SelectsTheWeekdayRow()
        {
            var curve = Curve("{\"mon\":0.10,\"tue\":0.50,\"wed\":0.10,\"thu\":0.10,"
                + "\"fri\":0.10,\"sat\":0.10,\"sun\":0.10}");
            var report = DFixtures.Run(DFixtures.Request(
                DateKeys.Enumerate("2026-08-24", 2),
                new DFixtures.FixedFactory(DFixtures.L001Dto()), curveBytes: curve));

            Assert.That(report.Records[0].Ramp.Code, Is.EqualTo(StageVerdictCode.Pass),
                "2026-08-24 is a Monday: row 0.10 vs 0.08 is in band");
            Assert.That(report.Records[1].Ramp.Code, Is.EqualTo(StageVerdictCode.Fail),
                "2026-08-25 is a Tuesday: row 0.50 vs 0.08 is out of band");
        }

        // A malformed curve is a typed run failure (config error, host exit 2) — not a silent
        // UNCONFIGURED downgrade, which would hide a broken human-ratified file.
        [Test]
        public void MalformedCurve_IsATypedRunFailure()
        {
            var r = DailyPipeline.Run(DFixtures.Request(
                new[] { "2026-08-24" }, new DFixtures.FixedFactory(DFixtures.L001Dto()),
                curveBytes: Curve("{\"mon\": \"high\"}")));
            Assert.That(r.Ok, Is.False);
        }
    }
}
