using System;
using NUnit.Framework;
using CatMetro.Content.Daily;

namespace CatMetro.Tests.Daily
{
    // CM-C6 criterion 1: fixed vectors + the pinned generator constant (CM-R11.1, CM-R11.7).
    // The three vector values below were computed with an INDEPENDENT tool (python hashlib,
    // recorded in state/handoffs/CM-C6.md A-C6-6): digest-tail big-endian, k in invariant
    // decimal. If Derive disagrees, Derive is wrong — never repin from the implementation.
    public sealed class DailySeedTests
    {
        [Test]
        public void Vector_2026_08_24_k0() =>
            Assert.That(DailySeed.Derive("2026-08-24", 0), Is.EqualTo(855770272u));

        [Test]
        public void Vector_2026_09_13_k0() =>
            Assert.That(DailySeed.Derive("2026-09-13", 0), Is.EqualTo(3336422503u));

        [Test]
        public void Vector_2026_12_31_k0() =>
            Assert.That(DailySeed.Derive("2026-12-31", 0), Is.EqualTo(2050439174u));

        // A-C6-3: the k component is the invariant decimal rendering — pinned with a k > 0 vector
        // (liveops_spec.md:55's "2026-09-13": 2 example entry).
        [Test]
        public void Vector_2026_09_13_k2_PinsTheSaltEncoding() =>
            Assert.That(DailySeed.Derive("2026-09-13", 2), Is.EqualTo(2383074624u));

        // CM-R11.7 / liveops_spec.md:57: the constant is frozen through Sep 30. This test fails
        // on ANY change — bumping it mid-event splits the playerbase across different boards.
        [Test]
        public void GeneratorConstant_IsFrozenVerbatim() =>
            Assert.That(DailySeed.GENERATOR_CONSTANT, Is.EqualTo("CM-DAILY-1"));

        // A-C6-4: the derivation refuses non-"yyyy-MM-dd" keys and negative salts outright —
        // a malformed key would silently derive a *different* seed from the one every correctly
        // formed client derives for the same calendar date.
        [TestCase("2026/08/24")]
        [TestCase("20260824")]
        [TestCase("2026-8-24")]
        [TestCase("2026-13-01")]
        [TestCase("2026-02-29")] // 2026 is not a leap year
        [TestCase("")]
        [TestCase(null)]
        public void Derive_RejectsMalformedDateKey(string bad) =>
            Assert.Throws<ArgumentException>(() => DailySeed.Derive(bad, 0));

        [Test]
        public void Derive_RejectsNegativeSalt() =>
            Assert.Throws<ArgumentException>(() => DailySeed.Derive("2026-08-24", -1));
    }
}
