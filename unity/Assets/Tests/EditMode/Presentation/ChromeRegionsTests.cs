using System;
using NUnit.Framework;
using UnityEngine;
using CatMetro.Presentation.Input;

namespace CatMetro.Tests.Presentation
{
    // CM-UX-01 criterion 1 (resolver unit half — the live-wired HandleTapAtScreen legs are in
    // PlayMode/Input/TapInputRoutingTests). Red-first: written against the skeleton. This file
    // is ALSO criterion 5's proof that the EditMode assembly can construct a Presentation type.
    public sealed class ChromeRegionsTests
    {
        [Test]
        public void Hit_FiresExactlyThatRegionsAction()
        {
            var regions = new ChromeRegions();
            int fired = 0;
            regions.Register("cta", () => new Rect(10, 10, 100, 50), () => fired++, 0);

            bool hit = regions.TryResolve(new Vector2(60, 35), out var onTap);

            Assert.That(hit, Is.True, "point inside the region must resolve");
            Assert.That(onTap, Is.Not.Null);
            onTap();
            Assert.That(fired, Is.EqualTo(1), "the resolved action is the registered one");
        }

        [Test]
        public void Miss_ResolvesNothing_PositiveControlInSameFixture()
        {
            var regions = new ChromeRegions();
            int fired = 0;
            regions.Register("cta", () => new Rect(10, 10, 100, 50), () => fired++, 0);

            Assert.That(regions.TryResolve(new Vector2(500, 500), out _), Is.False,
                "point outside every region must not resolve");
            // positive control: the same registry does resolve inside — the miss above is
            // a real miss, not a dead registry (live-wiring rule's vacuity discipline).
            Assert.That(regions.TryResolve(new Vector2(60, 35), out var onTap), Is.True);
            onTap();
            Assert.That(fired, Is.EqualTo(1));
        }

        [Test]
        public void Overlap_HighestPriorityWins()
        {
            var regions = new ChromeRegions();
            string won = null;
            regions.Register("low", () => new Rect(0, 0, 100, 100), () => won = "low", 1);
            regions.Register("high", () => new Rect(0, 0, 100, 100), () => won = "high", 5);

            Assert.That(regions.TryResolve(new Vector2(50, 50), out var onTap), Is.True);
            onTap();
            Assert.That(won, Is.EqualTo("high"), "higher priority claims the overlap");
        }

        [Test]
        public void PriorityTie_EarliestRegistrationWins()
        {
            var regions = new ChromeRegions();
            string won = null;
            regions.Register("first", () => new Rect(0, 0, 100, 100), () => won = "first", 3);
            regions.Register("second", () => new Rect(0, 0, 100, 100), () => won = "second", 3);

            Assert.That(regions.TryResolve(new Vector2(50, 50), out var onTap), Is.True);
            onTap();
            Assert.That(won, Is.EqualTo("first"), "ties resolve to the earliest registration");
        }

        [Test]
        public void Unregister_RemovesTheRegion_AndReportsHonestly()
        {
            var regions = new ChromeRegions();
            regions.Register("cta", () => new Rect(0, 0, 100, 100), () => { }, 0);
            Assert.That(regions.Count, Is.EqualTo(1));

            Assert.That(regions.Unregister("cta"), Is.True, "removing a live id reports true");
            Assert.That(regions.Count, Is.EqualTo(0));
            Assert.That(regions.TryResolve(new Vector2(50, 50), out _), Is.False,
                "an unregistered region never resolves");
            Assert.That(regions.Unregister("cta"), Is.False, "removing a dead id reports false");
        }

        [Test]
        public void RectProviderIsLive_ResolutionTracksTheCurrentRect()
        {
            var regions = new ChromeRegions();
            var rect = new Rect(0, 0, 100, 100);
            int fired = 0;
            regions.Register("moving", () => rect, () => fired++, 0);

            Assert.That(regions.TryResolve(new Vector2(50, 50), out var onTap), Is.True);
            onTap();
            rect = new Rect(200, 200, 100, 100); // the provider is consulted per tap
            Assert.That(regions.TryResolve(new Vector2(50, 50), out _), Is.False);
            Assert.That(regions.TryResolve(new Vector2(250, 250), out var onTap2), Is.True);
            onTap2();
            Assert.That(fired, Is.EqualTo(2));
        }

        [Test]
        public void DuplicateId_Throws()
        {
            var regions = new ChromeRegions();
            regions.Register("cta", () => new Rect(0, 0, 10, 10), () => { }, 0);
            Assert.Throws<ArgumentException>(
                () => regions.Register("cta", () => new Rect(0, 0, 10, 10), () => { }, 1),
                "a duplicate id is a wiring defect, never a silent replace (A-UX1-3)");
        }

        [Test]
        public void NullArguments_Throw()
        {
            var regions = new ChromeRegions();
            Assert.Throws<ArgumentException>(() => regions.Register(null, () => default, () => { }, 0));
            Assert.Throws<ArgumentException>(() => regions.Register("a", null, () => { }, 0));
            Assert.Throws<ArgumentException>(() => regions.Register("a", () => default, null, 0));
        }
    }
}
