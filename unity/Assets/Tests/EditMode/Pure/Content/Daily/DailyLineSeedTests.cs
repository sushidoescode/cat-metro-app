using System;
using NUnit.Framework;
using CatMetro.Content.Daily;

namespace CatMetro.Tests.Daily
{
    public sealed class DailyLineSeedTests
    {
        [TestCase("2026-08-10", 252386339u)]
        [TestCase("2026-08-24", 1449106418u)]
        [TestCase("2026-12-31", 1117928761u)]
        [TestCase("2028-02-29", 3895508439u)]
        public void Derive_UsesExactProductSpecPreimage(string date, uint expected) =>
            Assert.That(DailyLineSeed.Derive(date), Is.EqualTo(expected));

        [TestCase(1786319999L, "2026-08-09")]
        [TestCase(1786320000L, "2026-08-10")]
        [TestCase(1786320001L, "2026-08-10")]
        [TestCase(1798761599L, "2026-12-31")]
        [TestCase(1798761600L, "2027-01-01")]
        [TestCase(1835395199L, "2028-02-28")]
        [TestCase(1835395200L, "2028-02-29")]
        [TestCase(1835481599L, "2028-02-29")]
        [TestCase(1835481600L, "2028-03-01")]
        [TestCase(1801439999L, "2027-01-31")]
        [TestCase(1801440000L, "2027-02-01")]
        [TestCase(1803859199L, "2027-02-28")]
        [TestCase(1803859200L, "2027-03-01")]
        [TestCase(-62135596800L, "0001-01-01")]
        [TestCase(253402300799L, "9999-12-31")]
        [TestCase(-86401L, "1969-12-30")]
        [TestCase(-86400L, "1969-12-31")]
        [TestCase(-86399L, "1969-12-31")]
        [TestCase(-1L, "1969-12-31")]
        public void DateKeyFromUnixSeconds_UsesUtcMidnight(long instant, string expected) =>
            Assert.That(DailyLineSeed.DateKeyFromUnixSeconds(instant), Is.EqualTo(expected));

        [TestCase(-62135596801L)]
        [TestCase(253402300800L)]
        public void DateKeyFromUnixSeconds_RejectsOutsideSupportedCivilRange(long instant) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => DailyLineSeed.DateKeyFromUnixSeconds(instant));

        [Test]
        public void GeneratorLabel_IsFrozenVerbatim() =>
            Assert.That(DailyLineSeed.GeneratorLabel, Is.EqualTo("CM-DAILY-"));
    }
}
