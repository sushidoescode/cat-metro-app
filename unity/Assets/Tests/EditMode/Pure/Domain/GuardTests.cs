using System;
using NUnit.Framework;
using CatMetro.Domain;

namespace CatMetro.Tests.Domain
{
    // Criterion 14: three-member enum + four loud pin guards. Pinned behaviours must be absent
    // and NOISY, never silently invented (stop condition 1).
    [TestFixture]
    public class GuardTests
    {
        [Test]
        public void FailReason_HasExactlyThreeMembers()
        {
            var names = Enum.GetNames(typeof(FailReason));
            Assert.That(names.Length, Is.EqualTo(3), "adding a member is an ADR-0002 change");
            Assert.That(names, Is.EquivalentTo(new[] { "QueueOverflow", "PlatformOverflow", "TimeOut" }));
        }

        [Test]
        public void Guard_NonMatchingStationArrival_Throws_NEWQ4()
        {
            var ex = Assert.Throws<NotSupportedException>(() =>
                Fixtures.RunThroughTick(Fixtures.MismatchShape(), 3, new CommandLog(), 50));
            Assert.That(ex.Message, Does.Contain("NEW-Q4"));
        }

        [Test]
        public void Guard_SecondSource_ThrowsAtConstruction()
        {
            var ex = Assert.Throws<NotSupportedException>(() => new LevelGraph(
                "FX-2SRC", 3,
                new[] { 8, 8, 8 },
                new[] { 0, 1 }, new[] { 2, 2 }, new[] { 5, 5 },
                new[] { 0, 1 }, // two sources: pinned out of CM-C1 scope
                new int[0][], new int[0], new byte[0],
                new[] { 2 }, new[] { new[] { CatColor.Red } }, new[] { 6 },
                new int[0], new byte[0], new int[0], new int[0],
                1, 100, 8, 1));
            Assert.That(ex.Message, Does.Contain("second source").IgnoreCase);
        }

        [Test]
        public void Guard_WildColorWave_Throws_NEWQ35()
        {
            var ex = Assert.Throws<NotSupportedException>(() => new LevelGraph(
                "FX-WILD", 2,
                new[] { 8, 8 },
                new[] { 0 }, new[] { 1 }, new[] { 5 },
                new[] { 0 },
                new int[0][], new int[0], new byte[0],
                new[] { 1 }, new[] { new[] { CatColor.Red } }, new[] { 6 },
                new[] { 0 }, new[] { CatColor.Wild }, new[] { 1 }, new[] { 1 },
                1, 100, 8, 1));
            Assert.That(ex.Message, Does.Contain("NEW-Q35"));
        }

        [Test]
        public void Guard_FailedPlatformOverflow_Throws()
        {
            // The member exists (digest layout + enum test stay stable) but nothing may raise it
            // until Q-J is answered.
            var ex = Assert.Throws<NotSupportedException>(() => SimOutcome.MakeFailed(FailReason.PlatformOverflow));
            Assert.That(ex.Message, Does.Contain("Q-J"));
            // The two raisable members construct normally.
            Assert.That(SimOutcome.MakeFailed(FailReason.QueueOverflow).Reason, Is.EqualTo(FailReason.QueueOverflow));
            Assert.That(SimOutcome.MakeFailed(FailReason.TimeOut).Reason, Is.EqualTo(FailReason.TimeOut));
        }
    }
}
