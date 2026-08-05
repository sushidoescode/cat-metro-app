using NUnit.Framework;
using UnityEngine;
using CatMetro.Presentation.Input;

namespace CatMetro.Tests.Presentation
{
    // CM-UX-01 criterion 3, R1-F1 closure: the nearest-center / lowest-index-tie law was named
    // by the contract but unpinned anywhere in the repo (every fixture has one switch, so a
    // tie-break regression had zero test signal). Pinned here as pure math — resolution- and
    // dpi-independent — while the PlayMode pin keeps guarding the live-wired integration.
    public sealed class TapInputDiscLawTests
    {
        private static readonly Vector2[] TwoCenters =
        {
            new Vector2(100f, 100f), // index 0
            new Vector2(140f, 100f), // index 1
        };

        [Test]
        public void NearestCenterWins_EitherSide_PositiveControlPair()
        {
            Assert.That(TapInput.ResolveNearestDisc(new Vector2(117f, 100f), TwoCenters, 24f),
                Is.EqualTo(0), "17px vs 23px — the nearer center claims the tap");
            Assert.That(TapInput.ResolveNearestDisc(new Vector2(123f, 100f), TwoCenters, 24f),
                Is.EqualTo(1), "23px vs 17px — same law, other winner");
        }

        [Test]
        public void ExactTie_LowestIndexWins_RegardlessOfArrayOrderProximity()
        {
            Assert.That(TapInput.ResolveNearestDisc(new Vector2(120f, 100f), TwoCenters, 24f),
                Is.EqualTo(0), "an exact 20px/20px tie resolves to the LOWEST index");

            var reversed = new[] { new Vector2(140f, 100f), new Vector2(100f, 100f) };
            Assert.That(TapInput.ResolveNearestDisc(new Vector2(120f, 100f), reversed, 24f),
                Is.EqualTo(0), "lowest INDEX, not lowest coordinate — deterministic replay law");
        }

        [Test]
        public void OutsideRadius_Misses_PositiveControlInSameFixture()
        {
            Assert.That(TapInput.ResolveNearestDisc(new Vector2(300f, 300f), TwoCenters, 24f),
                Is.EqualTo(-1), "outside every disc is a miss");
            Assert.That(TapInput.ResolveNearestDisc(new Vector2(100f, 100f), TwoCenters, 24f),
                Is.EqualTo(0), "positive control: dead-center is a hit");
        }

        [Test]
        public void RadiusBoundary_IsInclusive()
        {
            // (76,100) is exactly 24px from center 0 on the side AWAY from center 1 (64px) —
            // the boundary case measured clean of the nearest-center rule.
            Assert.That(TapInput.ResolveNearestDisc(new Vector2(76f, 100f), TwoCenters, 24f),
                Is.EqualTo(0), "exactly radius away still hits (d <= radiusPx)");
            Assert.That(TapInput.ResolveNearestDisc(new Vector2(75.9f, 100f), TwoCenters, 24f),
                Is.EqualTo(-1), "just past the radius misses");
        }
    }
}
