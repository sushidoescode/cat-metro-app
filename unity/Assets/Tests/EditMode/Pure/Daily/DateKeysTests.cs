using System;
using NUnit.Framework;
using CatMetro.Content.Daily;

namespace CatMetro.Tests.Daily
{
    // CM-C6 criterion 2 support: pure civil-calendar arithmetic (A-C6-10). No clock anywhere —
    // these tests pin the calendar rules the enumeration and the weekday ramp depend on.
    public sealed class DateKeysTests
    {
        [TestCase("2026-08-24", true)]
        [TestCase("2028-02-29", true)]  // 2028 is a leap year
        [TestCase("2026-02-29", false)] // 2026 is not
        [TestCase("2100-02-29", false)] // century rule
        [TestCase("2000-02-29", true)]  // 400-year rule
        [TestCase("2026-12-31", true)]
        [TestCase("2026-13-01", false)]
        [TestCase("2026-00-10", false)]
        [TestCase("2026-04-31", false)]
        [TestCase("2026-8-24", false)]
        [TestCase("2026/08/24", false)]
        [TestCase("2026-08-24 ", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsValid_AppliesRealCalendarRules(string key, bool expected) =>
            Assert.That(DateKeys.IsValid(key), Is.EqualTo(expected));

        [Test]
        public void Enumerate_StartsAtFromKey_Consecutive()
        {
            var keys = DateKeys.Enumerate("2026-08-24", 3);
            Assert.That(keys, Is.EqualTo(new[] { "2026-08-24", "2026-08-25", "2026-08-26" }));
        }

        [Test]
        public void Enumerate_CrossesMonthAndYearBoundaries()
        {
            Assert.That(DateKeys.Enumerate("2026-08-31", 2)[1], Is.EqualTo("2026-09-01"));
            Assert.That(DateKeys.Enumerate("2026-12-31", 2)[1], Is.EqualTo("2027-01-01"));
        }

        [Test]
        public void Enumerate_HonoursLeapFebruary()
        {
            Assert.That(DateKeys.Enumerate("2028-02-28", 3),
                Is.EqualTo(new[] { "2028-02-28", "2028-02-29", "2028-03-01" }));
            Assert.That(DateKeys.Enumerate("2026-02-28", 2)[1], Is.EqualTo("2026-03-01"));
        }

        [Test]
        public void Enumerate_RejectsBadInputs()
        {
            Assert.Throws<ArgumentException>(() => DateKeys.Enumerate("2026-02-30", 1));
            Assert.Throws<ArgumentException>(() => DateKeys.Enumerate("2026-08-24", 0));
            Assert.Throws<ArgumentException>(() => DateKeys.Enumerate("2026-08-24", -1));
        }

        // Known anchors: 2026-08-24 is a Monday (the liveops --from example / 1.0 window start);
        // 2026-08-04 is a Tuesday; 2026-08-30 is a Sunday. 0 = Monday .. 6 = Sunday (A-C6-10).
        [TestCase("2026-08-24", 0)]
        [TestCase("2026-08-04", 1)]
        [TestCase("2026-08-26", 2)]
        [TestCase("2026-08-27", 3)]
        [TestCase("2026-08-28", 4)]
        [TestCase("2026-08-29", 5)]
        [TestCase("2026-08-30", 6)]
        [TestCase("2028-02-29", 1)] // leap-day spot check: 2028-02-29 is a Tuesday
        public void WeekdayIndex_MatchesKnownDates(string key, int expected) =>
            Assert.That(DateKeys.WeekdayIndex(key), Is.EqualTo(expected));
    }
}
