using NUnit.Framework;
using CatMetro.Application.Session;

namespace CatMetro.Tests.Session
{
    // CM-C2b criterion 6: the interpolation law at a 60 Hz render rate against the 8 tps sim —
    // alpha stays in [0,1), increases monotonically between snapshots, resets exactly once per
    // consumed tick. Runs in BOTH hosts (engine-free).
    public sealed class InterpolatorTests
    {
        [Test]
        public void Alpha_LawHolds_At60HzAgainst8Tps()
        {
            var clock = new TickInterpolator();
            double frameMs = 1000.0 / 60.0;
            double lastAlpha = -1.0;
            int resets = 0, ticks = 0;

            for (int frame = 0; frame < 600; frame++) // 10 seconds
            {
                int steps = clock.AdvanceMs(frameMs);
                ticks += steps;
                double alpha = clock.Alpha;
                Assert.That(alpha, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0),
                    "alpha must stay in [0,1)");
                if (steps == 0)
                    Assert.That(alpha, Is.GreaterThan(lastAlpha),
                        "monotonically increasing between snapshots");
                else
                {
                    resets += steps;
                    Assert.That(steps, Is.LessThanOrEqualTo(3), "60 Hz never owes multiple resets");
                    // Review F7: the RESET itself observed — alpha actually dropped, which the
                    // resets-equals-ticks restatement never proved.
                    Assert.That(alpha, Is.LessThan(lastAlpha + 1e-9),
                        "a consumed tick resets alpha below the previous frame's value");
                }
                lastAlpha = alpha;
            }
            // Review F7: 600 * (1000/60) sits 1.1e-12 ms past the 80th boundary — one ULP of
            // FMA/reassociation across the two compilers flips 80 to 79. ±1 is the honest law.
            Assert.That(ticks, Is.EqualTo(80).Within(1), "10 s at 8 tps consumes 80 ticks (±1 ULP-edge)");
            Assert.That(resets, Is.EqualTo(ticks), "exactly one reset per consumed tick");
        }

        [Test]
        public void NegativeAndZeroDt_AreInert()
        {
            var clock = new TickInterpolator();
            Assert.That(clock.AdvanceMs(-50), Is.Zero);
            Assert.That(clock.AdvanceMs(0), Is.Zero);
            Assert.That(clock.Alpha, Is.Zero);
        }
    }
}
